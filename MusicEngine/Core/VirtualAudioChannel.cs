//Engine License (MEL) – Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Virtual audio channels for routing audio between applications.

using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace MusicEngine.Core;

/// <summary>
/// Represents a virtual audio channel that can route audio to other applications.
/// This creates a named pipe that other applications can read from.
/// </summary>
public class VirtualAudioChannel : IDisposable
{
    private readonly string _name;
    private readonly WaveFormat _format;
    private readonly int _bufferSize;
    private readonly Queue<float[]> _audioQueue = new();
    private readonly object _lock = new();
    private readonly CancellationTokenSource _cts = new();

    private NamedPipeServerStream? _pipeServer;
    private Task? _serverTask;
    private bool _isRunning;
    private float _volume = 1.0f;

    /// <summary>
    /// Creates a new virtual audio channel.
    /// </summary>
    /// <param name="name">Name of the channel (used for pipe name)</param>
    /// <param name="sampleRate">Sample rate in Hz</param>
    /// <param name="channels">Number of audio channels (1=mono, 2=stereo)</param>
    public VirtualAudioChannel(string name, int sampleRate = 44100, int channels = 2)
    {
        _name = name;
        _format = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        _bufferSize = sampleRate * channels * 4; // 1 second buffer
    }

    /// <summary>
    /// Name of the virtual channel.
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// Wave format of the channel.
    /// </summary>
    public WaveFormat WaveFormat => _format;

    /// <summary>
    /// Volume/gain of the channel (0.0 to 2.0).
    /// </summary>
    public float Volume
    {
        get => _volume;
        set => _volume = Math.Clamp(value, 0f, 2f);
    }

    /// <summary>
    /// Whether the channel is currently running.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// The pipe name for external applications to connect to.
    /// </summary>
    public string PipeName => $"MusicEngine_{_name}";

    /// <summary>
    /// Starts the virtual audio channel.
    /// </summary>
    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true;

        _serverTask = Task.Run(RunServerAsync);
        Console.WriteLine($"[VirtualChannel] Started: {_name} (Pipe: {PipeName})");
    }

    /// <summary>
    /// Stops the virtual audio channel.
    /// </summary>
    public void Stop()
    {
        if (!_isRunning) return;
        _isRunning = false;

        _cts.Cancel();
        _pipeServer?.Dispose();
        _pipeServer = null;

        Console.WriteLine($"[VirtualChannel] Stopped: {_name}");
    }

    /// <summary>
    /// Writes audio samples to the channel.
    /// </summary>
    public void Write(float[] samples)
    {
        if (!_isRunning) return;

        // Apply volume
        if (Math.Abs(_volume - 1.0f) > 0.001f)
        {
            var adjusted = new float[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                adjusted[i] = samples[i] * _volume;
            }
            samples = adjusted;
        }

        lock (_lock)
        {
            _audioQueue.Enqueue(samples);

            // Keep buffer from growing too large
            while (_audioQueue.Count > 10)
            {
                _audioQueue.Dequeue();
            }
        }
    }

    private async Task RunServerAsync()
    {
        try
        {
            while (_isRunning && !_cts.Token.IsCancellationRequested)
            {
                _pipeServer = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.Out,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                Console.WriteLine($"[VirtualChannel] Waiting for connection on {PipeName}...");

                try
                {
                    await _pipeServer.WaitForConnectionAsync(_cts.Token).ConfigureAwait(false);
                    Console.WriteLine($"[VirtualChannel] Client connected to {_name}");

                    // Send wave format header
                    await SendWaveHeader(_pipeServer).ConfigureAwait(false);

                    // Stream audio
                    while (_pipeServer.IsConnected && _isRunning)
                    {
                        float[]? samples = null;

                        lock (_lock)
                        {
                            if (_audioQueue.Count > 0)
                            {
                                samples = _audioQueue.Dequeue();
                            }
                        }

                        if (samples != null)
                        {
                            var bytes = new byte[samples.Length * 4];
                            Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
                            await _pipeServer.WriteAsync(bytes, _cts.Token).ConfigureAwait(false);
                        }
                        else
                        {
                            await Task.Delay(10, _cts.Token).ConfigureAwait(false);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (IOException)
                {
                    Console.WriteLine($"[VirtualChannel] Client disconnected from {_name}");
                }
                finally
                {
                    _pipeServer?.Dispose();
                    _pipeServer = null;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VirtualChannel] Error: {ex.Message}");
        }
    }

    private async Task SendWaveHeader(Stream stream)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Write minimal WAV header info as a header packet
        writer.Write(_format.SampleRate);
        writer.Write(_format.Channels);
        writer.Write(_format.BitsPerSample);

        var header = ms.ToArray();
        await stream.WriteAsync(header).ConfigureAwait(false);
    }

    public void Dispose()
    {
        Stop();
        _cts.Dispose();
    }
}

/// <summary>
/// Sample provider that captures audio and sends it to a virtual channel.
/// </summary>
public class VirtualChannelCapture : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly VirtualAudioChannel _channel;

    public VirtualChannelCapture(ISampleProvider source, VirtualAudioChannel channel)
    {
        _source = source;
        _channel = channel;
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);

        if (read > 0 && _channel.IsRunning)
        {
            var copy = new float[read];
            Array.Copy(buffer, offset, copy, 0, read);
            _channel.Write(copy);
        }

        return read;
    }
}

/// <summary>
/// Manages multiple virtual audio channels.
/// </summary>
public class VirtualChannelManager : IDisposable
{
    private readonly Dictionary<string, VirtualAudioChannel> _channels = new();
    private readonly object _lock = new();

    /// <summary>
    /// Creates a new virtual audio channel.
    /// </summary>
    public VirtualAudioChannel CreateChannel(string name, int sampleRate = 44100, int channels = 2)
    {
        lock (_lock)
        {
            if (_channels.ContainsKey(name))
            {
                Console.WriteLine($"[VirtualChannelManager] Channel '{name}' already exists");
                return _channels[name];
            }

            var channel = new VirtualAudioChannel(name, sampleRate, channels);
            _channels[name] = channel;
            return channel;
        }
    }

    /// <summary>
    /// Gets a virtual channel by name.
    /// </summary>
    public VirtualAudioChannel? GetChannel(string name)
    {
        lock (_lock)
        {
            return _channels.TryGetValue(name, out var channel) ? channel : null;
        }
    }

    /// <summary>
    /// Removes and disposes a virtual channel.
    /// </summary>
    public void RemoveChannel(string name)
    {
        lock (_lock)
        {
            if (_channels.TryGetValue(name, out var channel))
            {
                channel.Dispose();
                _channels.Remove(name);
            }
        }
    }

    /// <summary>
    /// Lists all virtual channels.
    /// </summary>
    public void ListChannels()
    {
        lock (_lock)
        {
            Console.WriteLine("\n=== Virtual Audio Channels ===");
            if (_channels.Count == 0)
            {
                Console.WriteLine("No virtual channels created.");
                return;
            }

            foreach (var kvp in _channels)
            {
                var ch = kvp.Value;
                string status = ch.IsRunning ? "Running" : "Stopped";
                Console.WriteLine($"  [{kvp.Key}] {ch.WaveFormat.SampleRate}Hz {ch.WaveFormat.Channels}ch - {status}");
                Console.WriteLine($"    Pipe: {ch.PipeName}");
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var channel in _channels.Values)
            {
                channel.Dispose();
            }
            _channels.Clear();
        }
    }
}
