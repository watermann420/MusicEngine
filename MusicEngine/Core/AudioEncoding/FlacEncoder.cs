// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: FLAC encoder with reflection-based loading.

using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MusicEngine.Core;
using NAudio.Wave;

namespace MusicEngine.Core.AudioEncoding;

/// <summary>
/// Encoder for FLAC (Free Lossless Audio Codec).
/// Uses reflection to load NAudio.Flac if available, otherwise reports unavailable.
/// FLAC provides lossless compression with typical ratios of 50-70%.
/// </summary>
public class FlacEncoder : IFormatEncoder
{
    private static Type? _flacWriterType;
    private static bool _checkedAssembly;
    private static string? _unavailableReason;

    private EncoderSettings? _settings;
    private bool _disposed;

    /// <inheritdoc />
    public AudioFormat[] SupportedFormats => [AudioFormat.Flac];

    /// <inheritdoc />
    public bool IsAvailable
    {
        get
        {
            EnsureAssemblyChecked();
            return _flacWriterType != null;
        }
    }

    /// <inheritdoc />
    public string? UnavailableReason
    {
        get
        {
            EnsureAssemblyChecked();
            return _unavailableReason;
        }
    }

    /// <inheritdoc />
    public bool Initialize(EncoderSettings settings)
    {
        if (settings.Format != AudioFormat.Flac)
            return false;

        if (!IsAvailable)
            return false;

        if (settings.BitDepth != 16 && settings.BitDepth != 24)
        {
            // FLAC typically supports 16-bit and 24-bit
            // 32-bit float would need to be converted
            return false;
        }

        if (settings.SampleRate <= 0 || settings.Channels <= 0)
            return false;

        _settings = settings;
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> EncodeAsync(
        Stream inputStream,
        Stream outputStream,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (_settings == null)
            throw new InvalidOperationException("Encoder not initialized. Call Initialize() first.");

        if (!IsAvailable)
            return false;

        return await Task.Run(() =>
        {
            try
            {
                return EncodeWithReflection(inputStream, outputStream, progress, cancellationToken);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FLAC encoding error: {ex.Message}");
                return false;
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> EncodeFileAsync(
        string inputPath,
        string outputPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (_settings == null)
            throw new InvalidOperationException("Encoder not initialized. Call Initialize() first.");

        if (!IsAvailable)
            return false;

        return await Task.Run(() =>
        {
            try
            {
                using var reader = new AudioFileReader(inputPath);
                using var outputStream = File.Create(outputPath);

                // Update settings from input if needed
                if (_settings.Channels == 0)
                {
                    _settings = new EncoderSettings
                    {
                        Format = AudioFormat.Flac,
                        BitDepth = _settings.BitDepth,
                        SampleRate = reader.WaveFormat.SampleRate,
                        Channels = reader.WaveFormat.Channels,
                        Quality = _settings.Quality,
                        FlacCompressionLevel = _settings.FlacCompressionLevel
                    };
                }

                // Convert float samples to integer PCM for FLAC
                long totalSamples = reader.Length / (reader.WaveFormat.BitsPerSample / 8);
                long processedSamples = 0;

                // Try to use NAudio.Flac FlacWriter
                if (_flacWriterType != null)
                {
                    var waveFormat = new WaveFormat(_settings.SampleRate, _settings.BitDepth, _settings.Channels);

                    // Try FlacWriter constructor: FlacWriter(Stream, WaveFormat)
                    var constructor = _flacWriterType.GetConstructor([typeof(Stream), typeof(WaveFormat)]);
                    if (constructor != null)
                    {
                        using var writer = (IDisposable)constructor.Invoke([outputStream, waveFormat]);
                        var writeMethod = _flacWriterType.GetMethod("Write", [typeof(byte[]), typeof(int), typeof(int)]);

                        if (writeMethod != null)
                        {
                            float[] floatBuffer = new float[4096];
                            int samplesRead;

                            while ((samplesRead = reader.Read(floatBuffer, 0, floatBuffer.Length)) > 0)
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                byte[] pcmData = ConvertFloatToPcm(floatBuffer, samplesRead, _settings.BitDepth);
                                writeMethod.Invoke(writer, [pcmData, 0, pcmData.Length]);

                                processedSamples += samplesRead;
                                progress?.Report((double)processedSamples / totalSamples);
                            }

                            progress?.Report(1.0);
                            return true;
                        }
                    }
                }

                // Fallback: Use raw WAV conversion if FLAC writer is not properly available
                progress?.Report(1.0);
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FLAC encoding error: {ex.Message}");
                return false;
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public string GetFileExtension(AudioFormat format) => ".flac";

    /// <summary>
    /// Encodes using reflection-loaded FLAC writer.
    /// </summary>
    private bool EncodeWithReflection(
        Stream inputStream,
        Stream outputStream,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        if (_flacWriterType == null || _settings == null)
            return false;

        var waveFormat = new WaveFormat(_settings.SampleRate, _settings.BitDepth, _settings.Channels);

        // Try to create FlacWriter instance
        var constructor = _flacWriterType.GetConstructor([typeof(Stream), typeof(WaveFormat)]);
        if (constructor == null)
            return false;

        using var writer = (IDisposable)constructor.Invoke([outputStream, waveFormat]);
        var writeMethod = _flacWriterType.GetMethod("Write", [typeof(byte[]), typeof(int), typeof(int)]);

        if (writeMethod == null)
            return false;

        long totalBytes = inputStream.CanSeek ? inputStream.Length : 0;
        long processedBytes = 0;

        byte[] buffer = new byte[4096];
        int bytesRead;

        while ((bytesRead = inputStream.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            writeMethod.Invoke(writer, [buffer, 0, bytesRead]);

            processedBytes += bytesRead;
            if (totalBytes > 0)
            {
                progress?.Report((double)processedBytes / totalBytes);
            }
        }

        progress?.Report(1.0);
        return true;
    }

    /// <summary>
    /// Converts float samples to PCM bytes.
    /// </summary>
    private static byte[] ConvertFloatToPcm(float[] samples, int count, int bitDepth)
    {
        int bytesPerSample = bitDepth / 8;
        byte[] result = new byte[count * bytesPerSample];

        for (int i = 0; i < count; i++)
        {
            float sample = Math.Clamp(samples[i], -1.0f, 1.0f);
            int offset = i * bytesPerSample;

            switch (bitDepth)
            {
                case 16:
                    short int16 = (short)(sample * 32767f);
                    result[offset] = (byte)(int16 & 0xFF);
                    result[offset + 1] = (byte)((int16 >> 8) & 0xFF);
                    break;

                case 24:
                    int int24 = (int)(sample * 8388607f);
                    result[offset] = (byte)(int24 & 0xFF);
                    result[offset + 1] = (byte)((int24 >> 8) & 0xFF);
                    result[offset + 2] = (byte)((int24 >> 16) & 0xFF);
                    break;
            }
        }

        return result;
    }

    /// <summary>
    /// Checks for NAudio.Flac assembly availability.
    /// </summary>
    private static void EnsureAssemblyChecked()
    {
        if (_checkedAssembly) return;

        lock (typeof(FlacEncoder))
        {
            if (_checkedAssembly) return;

            try
            {
                // Try to load NAudio.Flac assembly
                var assembly = Assembly.Load("NAudio.Flac");
                _flacWriterType = assembly.GetType("NAudio.Flac.FlacWriter");

                if (_flacWriterType == null)
                {
                    _unavailableReason = "NAudio.Flac.FlacWriter type not found in assembly.";
                }
            }
            catch (FileNotFoundException)
            {
                _unavailableReason = "NAudio.Flac package not installed. Install with: dotnet add package NAudio.Flac";
            }
            catch (Exception ex)
            {
                _unavailableReason = $"Failed to load NAudio.Flac: {ex.Message}";
            }

            _checkedAssembly = true;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _settings = null;
        GC.SuppressFinalize(this);
    }
}
