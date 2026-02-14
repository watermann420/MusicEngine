#if !WINDOWS
// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann
// Description: Linux audio output adapter using PortAudio.

using System;
using System.Threading;
using NAudio.Wave;

namespace MusicEngine.Core;

internal sealed class PortAudioOutput : IAudioOutput
{
    private readonly object _lock = new();
    private ISampleProvider? _provider;
    private IntPtr _stream;
    private Thread? _thread;
    private volatile bool _running;
    private float[]? _buffer;
    private int _bufferFrames;
    private int _channels;

    public void Init(ISampleProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _channels = Math.Max(1, provider.WaveFormat.Channels);
        _bufferFrames = Math.Max(64, Settings.BufferSizeFrames);
        _buffer = new float[_bufferFrames * _channels];

        PortAudioNative.EnsureInitialized();
        var result = PortAudioNative.OpenDefaultStream(out _stream, 0, _channels, PortAudioNative.PaFloat32,
            provider.WaveFormat.SampleRate, (uint)_bufferFrames);
        if (result != 0 || _stream == IntPtr.Zero)
        {
            throw new InvalidOperationException($"PortAudio failed to open output stream ({result}).");
        }
    }

    public void Play()
    {
        if (_stream == IntPtr.Zero || _provider == null || _buffer == null) return;
        if (_running) return;

        var result = PortAudioNative.StartStream(_stream);
        if (result != 0)
        {
            throw new InvalidOperationException($"PortAudio failed to start output stream ({result}).");
        }

        _running = true;
        _thread = new Thread(RenderLoop)
        {
            IsBackground = true,
            Name = "MusicEngine.PortAudio"
        };
        _thread.Start();
    }

    public void Stop()
    {
        _running = false;
        if (_thread != null)
        {
            _thread.Join();
            _thread = null;
        }

        if (_stream != IntPtr.Zero)
        {
            PortAudioNative.StopStream(_stream);
        }
    }

    public void Dispose()
    {
        Stop();
        if (_stream != IntPtr.Zero)
        {
            PortAudioNative.CloseStream(_stream);
            _stream = IntPtr.Zero;
        }
        PortAudioNative.Release();
    }

    private void RenderLoop()
    {
        if (_provider == null || _buffer == null) return;
        var buffer = _buffer;
        var frames = _bufferFrames;
        var samples = buffer.Length;
        while (_running)
        {
            var read = _provider.Read(buffer, 0, samples);
            if (read < samples)
            {
                Array.Clear(buffer, read, samples - read);
            }

            var result = PortAudioNative.WriteStream(_stream, buffer, (uint)frames);
            if (result != 0)
            {
                Thread.Sleep(2);
            }
        }
    }
}
#endif
