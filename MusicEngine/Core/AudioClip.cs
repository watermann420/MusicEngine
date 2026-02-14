// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Simple audio file clip for routing.

using System;
using NAudio.Wave;

namespace MusicEngine.Core;

/// <summary>
/// Simple audio file clip for playback and routing.
/// </summary>
public sealed class AudioClip : ISampleProvider, IDisposable
{
#if WINDOWS
    private readonly AudioFileReader _reader;
#else
    private readonly SimpleWaveFileReader _reader;
#endif
    private bool _disposed;

    /// <summary>
    /// Load an audio file for playback.
    /// </summary>
    public AudioClip(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Audio file path is required.", nameof(path));
        }

#if WINDOWS
        _reader = new AudioFileReader(path);
#else
        _reader = new SimpleWaveFileReader(path);
#endif
    }

    /// <summary>
    /// When enabled, the clip restarts automatically at the end.
    /// </summary>
    public bool Loop { get; set; }

    /// <summary>
    /// Wave format reported by the underlying file reader.
    /// </summary>
    public WaveFormat WaveFormat => _reader.WaveFormat;

    /// <summary>
    /// Read audio samples into the buffer.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        if (_disposed) return 0;
        int read = _reader.Read(buffer, offset, count);
        if (read == 0 && Loop)
        {
            _reader.Position = 0;
            read = _reader.Read(buffer, offset, count);
        }
        return read;
    }

    /// <summary>
    /// Dispose the underlying file reader.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _reader.Dispose();
    }
}
