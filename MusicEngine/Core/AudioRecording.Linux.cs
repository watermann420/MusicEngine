#if !WINDOWS
// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann
// Description: Recording stubs for non-Windows builds.

using System;
using NAudio.Wave;

namespace MusicEngine.Core;

/// <summary>
/// Optional render settings for recording sessions.
/// </summary>
public sealed class RecordingOptions
{
    public int? SampleRate { get; set; }
    public int? Channels { get; set; }
    public int? WavBitDepth { get; set; }
    public int? BitRateKbps { get; set; }
    public int? ResamplerQuality { get; set; }
}

/// <summary>
/// Represents an active audio recording session.
/// </summary>
public sealed class RecordingSession : IDisposable
{
    internal RecordingSession(string targetPath, string format, WaveFormat formatInfo, RecordingOptions? options)
    {
        TargetPath = targetPath;
        Format = format;
    }

    public string TargetPath { get; }
    public string Format { get; }

    public void Dispose()
    {
    }
}

internal sealed class RecordingTap : ISampleProvider
{
    private readonly ISampleProvider _source;

    public RecordingTap(ISampleProvider source)
    {
        _source = source;
        WaveFormat = source.WaveFormat;
    }

    public event Action<float[], int, int>? SamplesAvailable;

    public WaveFormat WaveFormat { get; }

    public RecordingSession StartRecording(string path, string? format = null, RecordingOptions? options = null)
    {
        throw new PlatformNotSupportedException("Recording is not supported on this platform.");
    }

    public void StopRecording(RecordingSession? session = null)
    {
    }

    public void StopAll()
    {
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);
        if (read > 0)
        {
            SamplesAvailable?.Invoke(buffer, offset, read);
        }
        return read;
    }
}
#endif
