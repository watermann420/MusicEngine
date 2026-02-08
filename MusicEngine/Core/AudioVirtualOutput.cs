// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Secondary audio output for virtual mic routing.

using System;
using System.Buffers;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace MusicEngine.Core;

internal sealed class AudioVirtualOutput : IDisposable
{
    private readonly MMDevice _device;
    private readonly WasapiOut _output;
    private readonly BufferedWaveProvider _buffer;
    private byte[]? _byteBuffer;
    private bool _disposed;

    public AudioVirtualOutput(MMDevice device, WaveFormat format, int latencyMs)
    {
        _device = device;
        _buffer = new BufferedWaveProvider(format)
        {
            DiscardOnBufferOverflow = true,
            BufferDuration = TimeSpan.FromSeconds(2)
        };
        _output = new WasapiOut(_device, AudioClientShareMode.Shared, true, latencyMs);
        _output.Init(_buffer);
        _output.Play();
    }

    public string DeviceId => _device.ID;
    public string DeviceName => _device.FriendlyName;

    public void Push(float[] buffer, int offset, int count)
    {
        if (_disposed) return;
        if (count <= 0) return;

        int bytesNeeded = count * sizeof(float);
        if (_byteBuffer == null || _byteBuffer.Length < bytesNeeded)
        {
            _byteBuffer = ArrayPool<byte>.Shared.Rent(bytesNeeded);
        }

        Buffer.BlockCopy(buffer, offset * sizeof(float), _byteBuffer, 0, bytesNeeded);
        _buffer.AddSamples(_byteBuffer, 0, bytesNeeded);
    }

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
        if (_byteBuffer != null)
        {
            ArrayPool<byte>.Shared.Return(_byteBuffer);
            _byteBuffer = null;
        }
    }
}
