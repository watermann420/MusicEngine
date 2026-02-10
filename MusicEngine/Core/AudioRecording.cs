// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Lightweight audio recording helpers.

using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MusicEngine.Core;

/// <summary>
/// Optional render settings for recording sessions.
/// </summary>
public sealed class RecordingOptions
{
    /// <summary>
    /// Target sample rate in Hz (optional).
    /// </summary>
    public int? SampleRate { get; set; }

    /// <summary>
    /// Target channel count (optional).
    /// </summary>
    public int? Channels { get; set; }

    /// <summary>
    /// Target WAV bit depth (16, 24, or 32). Only applies to WAV output.
    /// </summary>
    public int? WavBitDepth { get; set; }

    /// <summary>
    /// Target bitrate in kbps for compressed formats (mp3/aac/wma).
    /// </summary>
    public int? BitRateKbps { get; set; }

    /// <summary>
    /// Resampler quality (1..60) when resampling is used.
    /// </summary>
    public int? ResamplerQuality { get; set; }
}

/// <summary>
/// Represents an active audio recording session.
/// </summary>
public sealed class RecordingSession : IDisposable
{
    private readonly string _targetPath;
    private readonly string _format;
    private readonly string _tempPath;
    private readonly WaveFileWriter _writer;
    private readonly RecordingOptions? _options;
    private readonly bool _needsConversion;
    private bool _disposed;

    internal RecordingSession(string targetPath, string format, WaveFormat formatInfo, RecordingOptions? options)
    {
        _targetPath = targetPath;
        _format = format;
        _options = options;
        _needsConversion = NeedsConversion(format, options);
        _tempPath = _needsConversion ? Path.Combine(Path.GetTempPath(), $"me_rec_{Guid.NewGuid():N}.wav") : targetPath;
        _writer = new WaveFileWriter(_tempPath, formatInfo);
    }

    internal void Write(float[] buffer, int offset, int count)
    {
        if (_disposed) return;
        _writer.WriteSamples(buffer, offset, count);
    }

    /// <summary>
    /// Final output path for this recording.
    /// </summary>
    public string TargetPath => _targetPath;

    /// <summary>
    /// Render format (e.g. wav, mp3, m4a).
    /// </summary>
    public string Format => _format;

    /// <summary>
    /// Finalize and write the recording to disk.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _writer.Dispose();

        if (_needsConversion)
        {
            try
            {
                if (_format == "wav")
                {
                    ConvertWave(_tempPath, _targetPath, _options);
                }
                else
                {
                    EncodeCompressed(_tempPath, _targetPath, _format, _options);
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
        if (!_needsConversion) return;
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

    private static bool NeedsConversion(string format, RecordingOptions? options)
    {
        if (!string.Equals(format, "wav", StringComparison.OrdinalIgnoreCase)) return true;
        if (options == null) return false;
        if (options.SampleRate.HasValue) return true;
        if (options.Channels.HasValue) return true;
        int bitDepth = options.WavBitDepth ?? Settings.WavBitDepth;
        if (bitDepth != 32) return true;
        return false;
    }

    private static void ConvertWave(string sourcePath, string targetPath, RecordingOptions? options)
    {
        using var reader = new AudioFileReader(sourcePath);
        var provider = ApplySampleOptions(reader, options);
        int bitDepth = options?.WavBitDepth ?? Settings.WavBitDepth;
        WriteWaveFile(targetPath, provider, bitDepth);
    }

    private static void EncodeCompressed(string sourcePath, string targetPath, string format, RecordingOptions? options)
    {
        using var reader = new AudioFileReader(sourcePath);
        IWaveProvider waveProvider = reader;
        MediaFoundationResampler? resampler = null;

        if (options?.SampleRate != null || options?.Channels != null)
        {
            int targetRate = options?.SampleRate ?? reader.WaveFormat.SampleRate;
            int targetChannels = options?.Channels ?? reader.WaveFormat.Channels;
            var targetFormat = WaveFormat.CreateIeeeFloatWaveFormat(targetRate, targetChannels);
            resampler = new MediaFoundationResampler(reader, targetFormat)
            {
                ResamplerQuality = Math.Clamp(options?.ResamplerQuality ?? 60, 1, 60)
            };
            waveProvider = resampler;
        }

        int bitrate = (options?.BitRateKbps ?? Settings.BitRateKbps) * 1000;
        switch (format)
        {
            case "mp3":
                MediaFoundationEncoder.EncodeToMp3(waveProvider, targetPath, bitrate);
                break;
            case "aac":
            case "m4a":
                MediaFoundationEncoder.EncodeToAac(waveProvider, targetPath, bitrate);
                break;
            case "wma":
                MediaFoundationEncoder.EncodeToWma(waveProvider, targetPath, bitrate);
                break;
            default:
                throw new InvalidOperationException($"Unsupported render format: {format}");
        }

        resampler?.Dispose();
    }

    private static ISampleProvider ApplySampleOptions(ISampleProvider provider, RecordingOptions? options)
    {
        if (options == null) return provider;

        if (options.Channels.HasValue)
        {
            int channels = options.Channels.Value;
            if (channels == 1 && provider.WaveFormat.Channels == 2)
            {
                var mono = new StereoToMonoSampleProvider(provider)
                {
                    LeftVolume = 0.5f,
                    RightVolume = 0.5f
                };
                provider = mono;
            }
            else if (channels == 2 && provider.WaveFormat.Channels == 1)
            {
                provider = new MonoToStereoSampleProvider(provider);
            }
        }

        if (options.SampleRate.HasValue && options.SampleRate.Value > 0 &&
            options.SampleRate.Value != provider.WaveFormat.SampleRate)
        {
            provider = new WdlResamplingSampleProvider(provider, options.SampleRate.Value);
        }

        return provider;
    }

    private static void WriteWaveFile(string path, ISampleProvider provider, int bitDepth)
    {
        int channels = provider.WaveFormat.Channels;
        int sampleRate = provider.WaveFormat.SampleRate;
        bitDepth = bitDepth is 16 or 24 or 32 ? bitDepth : 32;

        WaveFormat format = bitDepth == 32
            ? WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels)
            : new WaveFormat(sampleRate, bitDepth, channels);

        using var writer = new WaveFileWriter(path, format);
        var buffer = new float[sampleRate * channels / 10];
        int read;
        while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (bitDepth == 32)
            {
                writer.WriteSamples(buffer, 0, read);
                continue;
            }

            int bytesPerSample = bitDepth / 8;
            var outBytes = new byte[read * bytesPerSample];
            for (int i = 0; i < read; i++)
            {
                float sample = Math.Clamp(buffer[i], -1f, 1f);
                if (bitDepth == 16)
                {
                    short value = (short)Math.Round(sample * short.MaxValue);
                    outBytes[i * 2] = (byte)(value & 0xFF);
                    outBytes[i * 2 + 1] = (byte)((value >> 8) & 0xFF);
                }
                else if (bitDepth == 24)
                {
                    int value = (int)Math.Round(sample * 8388607f);
                    int offset = i * 3;
                    outBytes[offset] = (byte)(value & 0xFF);
                    outBytes[offset + 1] = (byte)((value >> 8) & 0xFF);
                    outBytes[offset + 2] = (byte)((value >> 16) & 0xFF);
                }
            }
            writer.Write(outBytes, 0, outBytes.Length);
        }
    }

    private static int DefaultBitrateKbps(string format)
    {
        return Settings.BitRateKbps;
    }
}

internal sealed class RecordingTap : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly object _lock = new();
    private readonly List<RecordingSession> _sessions = new();

    public event Action<float[], int, int>? SamplesAvailable;

    public RecordingTap(ISampleProvider source)
    {
        _source = source;
        WaveFormat = source.WaveFormat;
    }

    public WaveFormat WaveFormat { get; }

    public RecordingSession StartRecording(string path, string? format = null, RecordingOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Recording path is required.", nameof(path));
        }

        var normalized = NormalizeFormat(path, format, options);
        var session = new RecordingSession(path, normalized, WaveFormat, options);
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

        SamplesAvailable?.Invoke(buffer, offset, read);

        return read;
    }

    private static string NormalizeFormat(string path, string? format, RecordingOptions? options)
    {
        if (!string.IsNullOrWhiteSpace(format))
        {
            var normalized = format.Trim().TrimStart('.').ToLowerInvariant();
            if (normalized.StartsWith("wav", StringComparison.OrdinalIgnoreCase))
            {
                ApplyWavBitDepthSuffix(normalized, options);
                return "wav";
            }
            return normalized;
        }

        var ext = Path.GetExtension(path);
        if (!string.IsNullOrWhiteSpace(ext))
        {
            var normalized = ext.TrimStart('.').ToLowerInvariant();
            if (normalized.StartsWith("wav", StringComparison.OrdinalIgnoreCase))
            {
                ApplyWavBitDepthSuffix(normalized, options);
                return "wav";
            }
            return normalized;
        }

        return "wav";
    }

    private static void ApplyWavBitDepthSuffix(string normalized, RecordingOptions? options)
    {
        if (options == null) return;
        if (normalized == "wav16")
        {
            options.WavBitDepth = 16;
        }
        else if (normalized == "wav24")
        {
            options.WavBitDepth = 24;
        }
        else if (normalized == "wav32" || normalized == "wav32f")
        {
            options.WavBitDepth = 32;
        }
    }
}
