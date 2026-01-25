//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: OGG Vorbis-Encoder mit Reflection-basiertem Laden von NVorbis/NAudio.Vorbis.

using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MusicEngine.Core;
using NAudio.Wave;

namespace MusicEngine.Core.AudioEncoding;

/// <summary>
/// Encoder for OGG Vorbis format.
/// Uses reflection to load NVorbis/NAudio.Vorbis or OggVorbisEncoder packages.
/// OGG Vorbis provides high-quality lossy compression, typically used for game audio and streaming.
/// </summary>
public class OggVorbisEncoder : IFormatEncoder
{
    private static Type? _encoderType;
    private static bool _checkedAssembly;
    private static string? _unavailableReason;
    private static string? _availablePackage;
    // Thread-safe random for stream serial number generation
    private static readonly Random _streamIdRandom = new();
    private static readonly object _randomLock = new();

    private EncoderSettings? _settings;
    private bool _disposed;

    /// <inheritdoc />
    public AudioFormat[] SupportedFormats => [AudioFormat.Ogg];

    /// <inheritdoc />
    public bool IsAvailable
    {
        get
        {
            EnsureAssemblyChecked();
            return _encoderType != null;
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
        if (settings.Format != AudioFormat.Ogg)
            return false;

        if (!IsAvailable)
            return false;

        if (settings.SampleRate <= 0 || settings.Channels <= 0)
            return false;

        if (settings.Quality < 0 || settings.Quality > 1)
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
                System.Diagnostics.Debug.WriteLine($"OGG Vorbis encoding error: {ex.Message}");
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

                // Update settings from input if needed
                if (_settings.SampleRate == 0)
                {
                    _settings = new EncoderSettings
                    {
                        Format = AudioFormat.Ogg,
                        SampleRate = reader.WaveFormat.SampleRate,
                        Channels = reader.WaveFormat.Channels,
                        Quality = _settings.Quality,
                        Bitrate = _settings.Bitrate,
                        UseVbr = _settings.UseVbr
                    };
                }

                using var outputStream = File.Create(outputPath);

                // Try different encoding approaches based on available package
                if (_availablePackage == "OggVorbisEncoder")
                {
                    return EncodeWithOggVorbisEncoder(reader, outputStream, progress, cancellationToken);
                }
                else if (_availablePackage == "Concentus.OggFile")
                {
                    return EncodeWithConcentus(reader, outputStream, progress, cancellationToken);
                }

                // Generic reflection-based approach
                return EncodeWithReflection(reader, outputStream, progress, cancellationToken);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OGG Vorbis encoding error: {ex.Message}");
                return false;
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public string GetFileExtension(AudioFormat format) => ".ogg";

    /// <summary>
    /// Encodes using OggVorbisEncoder package.
    /// </summary>
    private bool EncodeWithOggVorbisEncoder(
        AudioFileReader reader,
        Stream outputStream,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        if (_encoderType == null || _settings == null)
            return false;

        try
        {
            // OggVorbisEncoder typical usage:
            // var oggStream = new OggStream(serialNumber);
            // ProcessingState = new ProcessingState(sampleRate, channels, quality);
            // Write header, then encode samples

            var assembly = _encoderType.Assembly;

            // Try to find OggStream and ProcessingState types
            var oggStreamType = assembly.GetType("OggVorbisEncoder.OggStream");
            var processingStateType = assembly.GetType("OggVorbisEncoder.ProcessingState");

            if (oggStreamType == null || processingStateType == null)
                return false;

            // Create ProcessingState
            var psConstructor = processingStateType.GetConstructor([typeof(int), typeof(int), typeof(float)]);
            if (psConstructor == null) return false;

            // Quality is -0.1 to 1.0 for Vorbis
            float vorbisQuality = (_settings.Quality * 1.1f) - 0.1f;

            using var processingState = (IDisposable)psConstructor.Invoke([
                _settings.SampleRate,
                _settings.Channels,
                vorbisQuality
            ]);

            // Create OggStream
            var ossConstructor = oggStreamType.GetConstructor([typeof(int)]);
            if (ossConstructor == null) return false;

            int streamId;
            lock (_randomLock)
            {
                streamId = _streamIdRandom.Next();
            }
            using var oggStream = (IDisposable)ossConstructor.Invoke([streamId]);

            // Get required methods
            var writeHeadersMethod = processingStateType.GetMethod("WriteHeaders");
            var writeDataMethod = processingStateType.GetMethod("WriteData");
            var pageOutMethod = oggStreamType.GetMethod("PageOut");

            if (writeHeadersMethod == null || writeDataMethod == null || pageOutMethod == null)
                return false;

            // Write headers
            var headerPackets = writeHeadersMethod.Invoke(processingState, null);
            // ... This requires complex interop with OggVorbisEncoder's API

            // For now, if the encoder is available but API is complex, report partial support
            progress?.Report(1.0);
            return false; // Mark as unsupported until full implementation
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OggVorbisEncoder error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Encodes using Concentus package (Opus in OGG container, not Vorbis).
    /// </summary>
    private bool EncodeWithConcentus(
        AudioFileReader reader,
        Stream outputStream,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        // Concentus is actually for Opus codec, not Vorbis
        // This would produce OGG Opus files, not OGG Vorbis
        // Include this for future extension but return false for now
        return false;
    }

    /// <summary>
    /// Generic reflection-based encoding attempt.
    /// </summary>
    private bool EncodeWithReflection(
        Stream inputStream,
        Stream outputStream,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        // Generic stream-based encoding - requires specific encoder implementation
        return false;
    }

    /// <summary>
    /// Generic reflection-based encoding attempt with AudioFileReader.
    /// </summary>
    private bool EncodeWithReflection(
        AudioFileReader reader,
        Stream outputStream,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        if (_encoderType == null || _settings == null)
            return false;

        // This is a placeholder for generic encoder support
        // Real implementation would depend on which encoder package is available
        return false;
    }

    /// <summary>
    /// Checks for available OGG Vorbis encoding packages.
    /// </summary>
    private static void EnsureAssemblyChecked()
    {
        if (_checkedAssembly) return;

        lock (typeof(OggVorbisEncoder))
        {
            if (_checkedAssembly) return;

            // Try different packages that support OGG Vorbis encoding

            // 1. Try OggVorbisEncoder (most common for encoding)
            try
            {
                var assembly = Assembly.Load("OggVorbisEncoder");
                _encoderType = assembly.GetType("OggVorbisEncoder.VorbisInfo");
                if (_encoderType != null)
                {
                    _availablePackage = "OggVorbisEncoder";
                    _checkedAssembly = true;
                    return;
                }
            }
            catch (FileNotFoundException)
            {
                // Not found, try next
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OggVorbisEncoder load error: {ex.Message}");
            }

            // 2. Try Concentus.OggFile (Opus codec, but similar API)
            try
            {
                var assembly = Assembly.Load("Concentus.OggFile");
                _encoderType = assembly.GetType("Concentus.Oggfile.OpusOggWriteStream");
                if (_encoderType != null)
                {
                    _availablePackage = "Concentus.OggFile";
                    _checkedAssembly = true;
                    return;
                }
            }
            catch (FileNotFoundException)
            {
                // Not found, try next
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Concentus load error: {ex.Message}");
            }

            // 3. Try NAudio.Vorbis (primarily for decoding, but check anyway)
            try
            {
                var assembly = Assembly.Load("NAudio.Vorbis");
                // NAudio.Vorbis is typically decode-only
                _encoderType = null;
            }
            catch (FileNotFoundException)
            {
                // Not found
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"NAudio.Vorbis load error: {ex.Message}");
            }

            _unavailableReason = "No OGG Vorbis encoder package found. Install with: dotnet add package OggVorbisEncoder";
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
