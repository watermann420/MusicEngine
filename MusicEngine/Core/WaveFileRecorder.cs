// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using System;
using System.IO;
using NAudio.Wave;

namespace MusicEngine.Core;

/// <summary>
/// Internal class for writing audio samples to WAV files.
/// Supports 16-bit, 24-bit, and 32-bit float formats.
/// Thread-safe implementation with proper buffer management.
/// </summary>
internal sealed class WaveFileRecorder : IDisposable
{
    private readonly WaveFileWriter _writer;
    private readonly int _sampleRate;
    private readonly int _channels;
    private readonly int _bitDepth;
    private readonly bool _isFloat;
    private readonly object _writeLock = new();
    private readonly byte[] _conversionBuffer;

    private long _totalSamplesWritten;
    private float _peakLevel;
    private bool _disposed;

    /// <summary>
    /// Gets the sample rate of the output file.
    /// </summary>
    public int SampleRate => _sampleRate;

    /// <summary>
    /// Gets the number of channels in the output file.
    /// </summary>
    public int Channels => _channels;

    /// <summary>
    /// Gets the bit depth of the output file.
    /// </summary>
    public int BitDepth => _bitDepth;

    /// <summary>
    /// Gets whether the output is 32-bit float format.
    /// </summary>
    public bool IsFloat => _isFloat;

    /// <summary>
    /// Gets the total number of samples written to the file.
    /// </summary>
    public long TotalSamplesWritten
    {
        get
        {
            lock (_writeLock)
            {
                return _totalSamplesWritten;
            }
        }
    }

    /// <summary>
    /// Gets the recording duration based on samples written.
    /// </summary>
    public TimeSpan Duration
    {
        get
        {
            long samples = TotalSamplesWritten;
            return TimeSpan.FromSeconds((double)samples / _sampleRate / _channels);
        }
    }

    /// <summary>
    /// Gets the current peak level in dB (0 dB = full scale).
    /// </summary>
    public float PeakLevelDb
    {
        get
        {
            lock (_writeLock)
            {
                if (_peakLevel <= 0) return float.NegativeInfinity;
                return 20f * (float)Math.Log10(_peakLevel);
            }
        }
    }

    /// <summary>
    /// Gets the current peak level as a linear value (0.0 to 1.0+).
    /// </summary>
    public float PeakLevel
    {
        get
        {
            lock (_writeLock)
            {
                return _peakLevel;
            }
        }
    }

    /// <summary>
    /// Gets the output file path.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets the current file size in bytes.
    /// </summary>
    public long FileSize
    {
        get
        {
            lock (_writeLock)
            {
                return _writer.Length;
            }
        }
    }

    /// <summary>
    /// Creates a new WaveFileRecorder.
    /// </summary>
    /// <param name="filePath">Path to the output WAV file.</param>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="channels">Number of channels (1 = mono, 2 = stereo).</param>
    /// <param name="format">The recording format (determines bit depth).</param>
    public WaveFileRecorder(string filePath, int sampleRate, int channels, RecordingFormat format)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "Sample rate must be positive.");
        if (channels <= 0)
            throw new ArgumentOutOfRangeException(nameof(channels), "Channels must be positive.");
        if (!format.IsWavFormat())
            throw new ArgumentException("WaveFileRecorder only supports WAV formats.", nameof(format));

        FilePath = filePath;
        _sampleRate = sampleRate;
        _channels = channels;
        _bitDepth = format.GetBitDepth();
        _isFloat = format.IsFloatFormat();
        _peakLevel = 0f;
        _totalSamplesWritten = 0;

        // Ensure directory exists
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Create appropriate wave format
        WaveFormat waveFormat = _isFloat
            ? WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels)
            : new WaveFormat(sampleRate, _bitDepth, channels);

        _writer = new WaveFileWriter(filePath, waveFormat);

        // Pre-allocate conversion buffer for typical buffer sizes
        // Buffer for 4096 samples at maximum bytes per sample (4 bytes for float/32-bit)
        _conversionBuffer = new byte[4096 * 4];
    }

    /// <summary>
    /// Writes float samples to the WAV file.
    /// Thread-safe operation.
    /// </summary>
    /// <param name="samples">Array of float samples to write.</param>
    /// <param name="offset">Offset in the samples array.</param>
    /// <param name="count">Number of samples to write.</param>
    public void WriteSamples(float[] samples, int offset, int count)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(WaveFileRecorder));
        if (samples == null)
            throw new ArgumentNullException(nameof(samples));
        if (offset < 0 || offset >= samples.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (count < 0 || offset + count > samples.Length)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (count == 0)
            return;

        lock (_writeLock)
        {
            // Update peak level
            for (int i = 0; i < count; i++)
            {
                float absSample = Math.Abs(samples[offset + i]);
                if (absSample > _peakLevel)
                {
                    _peakLevel = absSample;
                }
            }

            if (_isFloat)
            {
                // Write float samples directly
                _writer.WriteSamples(samples, offset, count);
            }
            else
            {
                // Convert float samples to the appropriate bit depth
                int bytesPerSample = _bitDepth / 8;
                int totalBytes = count * bytesPerSample;

                // Ensure buffer is large enough
                byte[] buffer = totalBytes <= _conversionBuffer.Length
                    ? _conversionBuffer
                    : new byte[totalBytes];

                int destIndex = 0;
                for (int i = 0; i < count; i++)
                {
                    float sample = samples[offset + i];
                    // Clamp sample to [-1, 1]
                    sample = Math.Clamp(sample, -1f, 1f);

                    switch (_bitDepth)
                    {
                        case 16:
                            short sample16 = (short)(sample * 32767f);
                            buffer[destIndex++] = (byte)(sample16 & 0xFF);
                            buffer[destIndex++] = (byte)((sample16 >> 8) & 0xFF);
                            break;

                        case 24:
                            int sample24 = (int)(sample * 8388607f);
                            buffer[destIndex++] = (byte)(sample24 & 0xFF);
                            buffer[destIndex++] = (byte)((sample24 >> 8) & 0xFF);
                            buffer[destIndex++] = (byte)((sample24 >> 16) & 0xFF);
                            break;
                    }
                }

                _writer.Write(buffer, 0, totalBytes);
            }

            _totalSamplesWritten += count;
        }
    }

    /// <summary>
    /// Writes float samples to the WAV file.
    /// Convenience method that writes the entire array.
    /// </summary>
    /// <param name="samples">Array of float samples to write.</param>
    public void WriteSamples(float[] samples)
    {
        WriteSamples(samples, 0, samples.Length);
    }

    /// <summary>
    /// Resets the peak level meter.
    /// </summary>
    public void ResetPeakLevel()
    {
        lock (_writeLock)
        {
            _peakLevel = 0f;
        }
    }

    /// <summary>
    /// Flushes any buffered data to the file.
    /// </summary>
    public void Flush()
    {
        lock (_writeLock)
        {
            if (!_disposed)
            {
                _writer.Flush();
            }
        }
    }

    /// <summary>
    /// Finalizes the WAV file by flushing the buffer.
    /// Call this before disposing to ensure proper file format.
    /// </summary>
    public void FinalizeFile()
    {
        lock (_writeLock)
        {
            if (!_disposed)
            {
                _writer.Flush();
            }
        }
    }

    /// <summary>
    /// Disposes of the recorder and finalizes the WAV file.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        lock (_writeLock)
        {
            _disposed = true;
            try
            {
                _writer.Flush();
                _writer.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disposing WaveFileRecorder: {ex.Message}");
            }
        }
    }
}
