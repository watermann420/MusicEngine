// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Secondary audio output for virtual mic routing.

using System;
using System.Buffers;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MusicEngine.Core;

internal sealed class AudioVirtualOutput : IDisposable
{
    private readonly MMDevice _device;
    private readonly WasapiOut _output;
    private readonly BufferedWaveProvider _inputBuffer;
    private readonly ISampleProvider _outputProvider;
    private readonly int _outputChannelOffset;
    private bool _disposed;

    /// <summary>
    /// Create a virtual output routed to a specific device.
    /// </summary>
    public AudioVirtualOutput(MMDevice device, WaveFormat format, int latencyMs, int outputChannelOffset = 0)
    {
        _device = device;
        _outputChannelOffset = outputChannelOffset;
        _inputBuffer = new BufferedWaveProvider(format)
        {
            DiscardOnBufferOverflow = true,
            BufferDuration = TimeSpan.FromSeconds(2)
        };

        ISampleProvider current = _inputBuffer.ToSampleProvider();
        var mixFormat = device.AudioClient.MixFormat;
        int deviceChannels = Math.Max(1, mixFormat.Channels);

        int clampedOffset = Math.Clamp(outputChannelOffset, 0, Math.Max(0, deviceChannels - 1));
        int sourceChannels = current.WaveFormat.Channels;
        if (clampedOffset + sourceChannels > deviceChannels)
        {
            clampedOffset = Math.Max(0, deviceChannels - sourceChannels);
        }

        if (deviceChannels != sourceChannels || clampedOffset != 0)
        {
            current = new ChannelMappingSampleProvider(current, deviceChannels, clampedOffset);
        }

        if (current.WaveFormat.SampleRate != mixFormat.SampleRate)
        {
            current = new WdlResamplingSampleProvider(current, mixFormat.SampleRate);
        }

        _outputProvider = current;
        _output = new WasapiOut(_device, AudioClientShareMode.Shared, true, latencyMs);
        _output.Init(_outputProvider.ToWaveProvider());
        _output.Play();
    }

    /// <summary>
    /// Target device ID.
    /// </summary>
    public string DeviceId => _device.ID;
    /// <summary>
    /// Target device name.
    /// </summary>
    public string DeviceName => _device.FriendlyName;
    /// <summary>
    /// Output channel offset on the target device.
    /// </summary>
    public int OutputChannelOffset => _outputChannelOffset;

    /// <summary>
    /// Push interleaved float samples into the virtual output buffer.
    /// </summary>
    public void Push(float[] buffer, int offset, int count)
    {
        if (_disposed) return;
        if (count <= 0) return;

        int bytesNeeded = count * sizeof(float);
        var byteBuffer = ArrayPool<byte>.Shared.Rent(bytesNeeded);
        try
        {
            Buffer.BlockCopy(buffer, offset * sizeof(float), byteBuffer, 0, bytesNeeded);
            _inputBuffer.AddSamples(byteBuffer, 0, bytesNeeded);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(byteBuffer);
        }
    }

    /// <summary>
    /// Stop output and release resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            _output.Stop();
        }
        catch
        {
        }
        _output.Dispose();
        _device.Dispose();
    }
}

internal sealed class ChannelMappingSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly int _outputChannels;
    private readonly int _outputOffset;
    private float[]? _sourceBuffer;

    /// <summary>
    /// Map a source provider into a larger output channel layout.
    /// </summary>
    public ChannelMappingSampleProvider(ISampleProvider source, int outputChannels, int outputOffset)
    {
        _source = source;
        _outputChannels = Math.Max(1, outputChannels);
        _outputOffset = Math.Clamp(outputOffset, 0, _outputChannels - 1);
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(_source.WaveFormat.SampleRate, _outputChannels);
    }

    /// <summary>
    /// Output wave format.
    /// </summary>
    public WaveFormat WaveFormat { get; }

    /// <summary>
    /// Read and map samples into the output buffer.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        int framesRequested = count / _outputChannels;
        int sourceChannels = _source.WaveFormat.Channels;
        int neededSamples = framesRequested * sourceChannels;
        if (_sourceBuffer == null || _sourceBuffer.Length < neededSamples)
        {
            if (_sourceBuffer != null)
            {
                ArrayPool<float>.Shared.Return(_sourceBuffer);
            }
            _sourceBuffer = ArrayPool<float>.Shared.Rent(neededSamples);
        }

        int read = _source.Read(_sourceBuffer, 0, neededSamples);
        int framesRead = read / sourceChannels;
        int outputSamples = framesRead * _outputChannels;

        Array.Clear(buffer, offset, outputSamples);
        int copyChannels = Math.Min(sourceChannels, _outputChannels - _outputOffset);
        if (copyChannels <= 0) return outputSamples;

        for (int frame = 0; frame < framesRead; frame++)
        {
            int sourceBase = frame * sourceChannels;
            int outputBase = offset + frame * _outputChannels + _outputOffset;
            for (int ch = 0; ch < copyChannels; ch++)
            {
                buffer[outputBase + ch] = _sourceBuffer[sourceBase + ch];
            }
        }

        return outputSamples;
    }
}
