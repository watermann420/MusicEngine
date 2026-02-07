// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Lightweight audio recording helpers.

using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;

namespace MusicEngine.Core;

/// <summary>
/// Represents an active audio recording session.
/// </summary>
public sealed class RecordingSession : IDisposable
{
    private readonly string _targetPath;
    private readonly string _format;
    private readonly string _tempPath;
    private readonly WaveFileWriter _writer;
    private bool _disposed;

    internal RecordingSession(string targetPath, string format, WaveFormat formatInfo)
    {
        _targetPath = targetPath;
        _format = format;
        _tempPath = format == "wav" ? targetPath : Path.Combine(Path.GetTempPath(), $"me_rec_{Guid.NewGuid():N}.wav");
        _writer = new WaveFileWriter(_tempPath, formatInfo);
    }

    internal void Write(float[] buffer, int offset, int count)
    {
        if (_disposed) return;
        _writer.WriteSamples(buffer, offset, count);
    }

    /// <summary>
    /// Finalize and write the recording to disk.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _writer.Dispose();

        if (_format != "wav")
        {
            try
            {
                using var reader = new AudioFileReader(_tempPath);
                switch (_format)
                {
                    case "mp3":
                        MediaFoundationEncoder.EncodeToMp3(reader, _targetPath);
                        break;
                    case "aac":
                    case "m4a":
                        MediaFoundationEncoder.EncodeToAac(reader, _targetPath);
                        break;
                    case "wma":
                        MediaFoundationEncoder.EncodeToWma(reader, _targetPath);
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported render format: {_format}");
                }
            }
            finally
            {
                TryDeleteTemp();
            }
        }
    }

    private void TryDeleteTemp()
    {
        if (_format == "wav") return;
        try
        {
            if (File.Exists(_tempPath))
            {
                File.Delete(_tempPath);
            }
        }
        catch
        {
            // Ignore temp cleanup errors.
        }
    }
}

internal sealed class RecordingTap : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly object _lock = new();
    private readonly List<RecordingSession> _sessions = new();

    public RecordingTap(ISampleProvider source)
    {
        _source = source;
        WaveFormat = source.WaveFormat;
    }

    public WaveFormat WaveFormat { get; }

    public RecordingSession StartRecording(string path, string? format = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Recording path is required.", nameof(path));
        }

        var normalized = NormalizeFormat(path, format);
        var session = new RecordingSession(path, normalized, WaveFormat);
        lock (_lock)
        {
            _sessions.Add(session);
        }
        return session;
    }

    public void StopRecording(RecordingSession? session = null)
    {
        RecordingSession? toStop = session;
        lock (_lock)
        {
            if (toStop == null)
            {
                if (_sessions.Count == 0) return;
                toStop = _sessions[^1];
            }
            _sessions.Remove(toStop);
        }

        toStop.Dispose();
    }

    public void StopAll()
    {
        RecordingSession[] sessions;
        lock (_lock)
        {
            sessions = _sessions.ToArray();
            _sessions.Clear();
        }

        foreach (var session in sessions)
        {
            session.Dispose();
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);
        if (read <= 0) return read;

        RecordingSession[] sessions;
        lock (_lock)
        {
            sessions = _sessions.ToArray();
        }

        foreach (var session in sessions)
        {
            session.Write(buffer, offset, read);
        }

        return read;
    }

    private static string NormalizeFormat(string path, string? format)
    {
        if (!string.IsNullOrWhiteSpace(format))
        {
            return format.Trim().TrimStart('.').ToLowerInvariant();
        }

        var ext = Path.GetExtension(path);
        if (!string.IsNullOrWhiteSpace(ext))
        {
            return ext.TrimStart('.').ToLowerInvariant();
        }

        return "wav";
    }
}
