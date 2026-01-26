//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: 3D waterfall spectrogram data generator for visualizing frequency content over time.

using System;
using System.Collections.Generic;
using NAudio.Dsp;
using NAudio.Wave;

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Represents a single frame of spectrogram data at a specific time.
/// </summary>
public class SpectrogramFrame
{
    /// <summary>
    /// Gets the time position in seconds from the start of the audio.
    /// </summary>
    public double TimeSeconds { get; init; }

    /// <summary>
    /// Gets the sample position in the audio stream.
    /// </summary>
    public long SamplePosition { get; init; }

    /// <summary>
    /// Gets the frequency values for each bin in Hz.
    /// </summary>
    public float[] FrequencyBins { get; init; } = Array.Empty<float>();

    /// <summary>
    /// Gets the magnitude values in dB for each frequency bin.
    /// </summary>
    public float[] MagnitudesDb { get; init; } = Array.Empty<float>();

    /// <summary>
    /// Gets the linear magnitude values (0.0 to 1.0) for each frequency bin.
    /// </summary>
    public float[] MagnitudesLinear { get; init; } = Array.Empty<float>();

    /// <summary>
    /// Gets the RGB color values for each frequency bin based on magnitude.
    /// Each element is a tuple of (R, G, B) values from 0-255.
    /// </summary>
    public (byte R, byte G, byte B)[] Colors { get; init; } = Array.Empty<(byte, byte, byte)>();
}

/// <summary>
/// Color mapping styles for spectrogram visualization.
/// </summary>
public enum SpectrogramColorMap
{
    /// <summary>Grayscale from black (quiet) to white (loud).</summary>
    Grayscale,

    /// <summary>Heat map: black -> blue -> cyan -> green -> yellow -> red -> white.</summary>
    HeatMap,

    /// <summary>Classic spectrum analyzer: green -> yellow -> red.</summary>
    Classic,

    /// <summary>Cool colors: black -> blue -> cyan -> white.</summary>
    Cool,

    /// <summary>Warm colors: black -> red -> orange -> yellow -> white.</summary>
    Warm,

    /// <summary>Rainbow: violet -> blue -> cyan -> green -> yellow -> orange -> red.</summary>
    Rainbow,

    /// <summary>Plasma: black -> purple -> magenta -> orange -> yellow.</summary>
    Plasma
}

/// <summary>
/// Result of a complete spectrogram analysis.
/// </summary>
public class SpectrogramResult
{
    /// <summary>
    /// Gets all spectrogram frames.
    /// </summary>
    public SpectrogramFrame[] Frames { get; init; } = Array.Empty<SpectrogramFrame>();

    /// <summary>
    /// Gets the total duration of the analyzed audio in seconds.
    /// </summary>
    public double DurationSeconds { get; init; }

    /// <summary>
    /// Gets the minimum frequency in Hz.
    /// </summary>
    public float MinFrequency { get; init; }

    /// <summary>
    /// Gets the maximum frequency in Hz.
    /// </summary>
    public float MaxFrequency { get; init; }

    /// <summary>
    /// Gets the FFT size used for analysis.
    /// </summary>
    public int FftSize { get; init; }

    /// <summary>
    /// Gets the frames per second (time resolution).
    /// </summary>
    public float FramesPerSecond { get; init; }

    /// <summary>
    /// Gets the frequency resolution in Hz per bin.
    /// </summary>
    public float FrequencyResolution { get; init; }

    /// <summary>
    /// Gets the peak magnitude values across all frames for each frequency bin.
    /// </summary>
    public float[]? PeakHoldMagnitudes { get; init; }
}

/// <summary>
/// Event arguments for spectrogram frame updates during real-time analysis.
/// </summary>
public class SpectrogramFrameEventArgs : EventArgs
{
    /// <summary>Gets the new spectrogram frame.</summary>
    public SpectrogramFrame Frame { get; }

    /// <summary>
    /// Creates new spectrogram frame event arguments.
    /// </summary>
    public SpectrogramFrameEventArgs(SpectrogramFrame frame)
    {
        Frame = frame;
    }
}

/// <summary>
/// 3D waterfall spectrogram data generator for visualizing frequency content over time.
/// Provides configurable FFT size, time resolution, frequency range, and color mapping.
/// </summary>
/// <remarks>
/// The spectrogram shows frequency on one axis, time on another, and magnitude as the third
/// dimension (typically represented as color intensity). This is useful for:
/// - Visualizing audio content evolution over time
/// - Identifying transients, harmonics, and noise
/// - Audio forensics and editing
/// - Music production visualization
/// </remarks>
public class Spectrogram3D
{
    private readonly int _sampleRate;
    private readonly int _fftSize;
    private readonly float _framesPerSecond;
    private readonly float _minFrequency;
    private readonly float _maxFrequency;
    private readonly SpectrogramColorMap _colorMap;
    private readonly bool _enablePeakHold;

    private readonly Complex[] _fftBuffer;
    private readonly float[] _sampleBuffer;
    private readonly float[] _window;
    private readonly float[] _frequencyBins;
    private readonly int _minBin;
    private readonly int _maxBin;
    private readonly int _hopSize;

    private int _sampleCount;
    private long _totalSamplesProcessed;
    private readonly object _lock = new();

    // Peak hold data
    private float[]? _peakMagnitudes;

    // Real-time frame history
    private readonly List<SpectrogramFrame> _frameHistory = new();
    private readonly int _maxHistoryFrames;

    /// <summary>
    /// Gets the FFT size used for analysis.
    /// </summary>
    public int FftSize => _fftSize;

    /// <summary>
    /// Gets the sample rate.
    /// </summary>
    public int SampleRate => _sampleRate;

    /// <summary>
    /// Gets the frames per second (time resolution).
    /// </summary>
    public float FramesPerSecond => _framesPerSecond;

    /// <summary>
    /// Gets the minimum frequency in Hz.
    /// </summary>
    public float MinFrequency => _minFrequency;

    /// <summary>
    /// Gets the maximum frequency in Hz.
    /// </summary>
    public float MaxFrequency => _maxFrequency;

    /// <summary>
    /// Gets the frequency resolution in Hz per bin.
    /// </summary>
    public float FrequencyResolution => (float)_sampleRate / _fftSize;

    /// <summary>
    /// Gets the number of frequency bins in the analysis range.
    /// </summary>
    public int BinCount => _maxBin - _minBin + 1;

    /// <summary>
    /// Gets or sets the dB floor (minimum level considered as silence).
    /// Default is -90 dB.
    /// </summary>
    public float DbFloor { get; set; } = -90f;

    /// <summary>
    /// Gets or sets the dB ceiling (maximum level).
    /// Default is 0 dB.
    /// </summary>
    public float DbCeiling { get; set; } = 0f;

    /// <summary>
    /// Gets the current peak hold magnitudes if peak hold is enabled.
    /// </summary>
    public float[]? PeakMagnitudes
    {
        get
        {
            lock (_lock)
            {
                return _peakMagnitudes != null ? (float[])_peakMagnitudes.Clone() : null;
            }
        }
    }

    /// <summary>
    /// Gets recent frames from real-time analysis.
    /// </summary>
    public SpectrogramFrame[] RecentFrames
    {
        get
        {
            lock (_lock)
            {
                return _frameHistory.ToArray();
            }
        }
    }

    /// <summary>
    /// Event raised when a new spectrogram frame is generated during real-time analysis.
    /// </summary>
    public event EventHandler<SpectrogramFrameEventArgs>? FrameGenerated;

    /// <summary>
    /// Creates a new 3D spectrogram analyzer with the specified configuration.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate in Hz (default: 44100).</param>
    /// <param name="fftSize">FFT window size, must be power of 2 between 512-8192 (default: 2048).</param>
    /// <param name="framesPerSecond">Time resolution in frames per second (default: 30).</param>
    /// <param name="minFrequency">Minimum frequency to analyze in Hz (default: 20).</param>
    /// <param name="maxFrequency">Maximum frequency to analyze in Hz (default: 20000).</param>
    /// <param name="colorMap">Color mapping style for visualization (default: HeatMap).</param>
    /// <param name="enablePeakHold">Enable peak hold tracking (default: false).</param>
    /// <param name="maxHistorySeconds">Maximum history to keep for real-time mode in seconds (default: 10).</param>
    public Spectrogram3D(
        int sampleRate = 44100,
        int fftSize = 2048,
        float framesPerSecond = 30f,
        float minFrequency = 20f,
        float maxFrequency = 20000f,
        SpectrogramColorMap colorMap = SpectrogramColorMap.HeatMap,
        bool enablePeakHold = false,
        float maxHistorySeconds = 10f)
    {
        if (!IsPowerOfTwo(fftSize))
            throw new ArgumentException("FFT size must be a power of two.", nameof(fftSize));
        if (fftSize < 512 || fftSize > 8192)
            throw new ArgumentOutOfRangeException(nameof(fftSize), "FFT size must be between 512 and 8192.");
        if (framesPerSecond < 1 || framesPerSecond > 120)
            throw new ArgumentOutOfRangeException(nameof(framesPerSecond), "Frames per second must be between 1 and 120.");
        if (minFrequency < 1)
            throw new ArgumentOutOfRangeException(nameof(minFrequency), "Minimum frequency must be at least 1 Hz.");
        if (maxFrequency > sampleRate / 2)
            maxFrequency = sampleRate / 2f;
        if (minFrequency >= maxFrequency)
            throw new ArgumentException("Minimum frequency must be less than maximum frequency.");

        _sampleRate = sampleRate;
        _fftSize = fftSize;
        _framesPerSecond = framesPerSecond;
        _minFrequency = minFrequency;
        _maxFrequency = maxFrequency;
        _colorMap = colorMap;
        _enablePeakHold = enablePeakHold;

        _fftBuffer = new Complex[fftSize];
        _sampleBuffer = new float[fftSize];
        _window = GenerateHannWindow(fftSize);

        // Calculate hop size based on desired frame rate
        _hopSize = (int)(sampleRate / framesPerSecond);

        // Calculate bin range for frequency limits
        float binResolution = (float)sampleRate / fftSize;
        _minBin = Math.Max(1, (int)(minFrequency / binResolution));
        _maxBin = Math.Min(fftSize / 2 - 1, (int)(maxFrequency / binResolution));

        // Pre-calculate frequency bins
        int binCount = _maxBin - _minBin + 1;
        _frequencyBins = new float[binCount];
        for (int i = 0; i < binCount; i++)
        {
            _frequencyBins[i] = (_minBin + i) * binResolution;
        }

        if (_enablePeakHold)
        {
            _peakMagnitudes = new float[binCount];
            Array.Fill(_peakMagnitudes, float.MinValue);
        }

        _maxHistoryFrames = (int)(maxHistorySeconds * framesPerSecond);
    }

    /// <summary>
    /// Processes audio samples for real-time spectrogram generation.
    /// Call this continuously with incoming audio for streaming analysis.
    /// </summary>
    /// <param name="samples">Audio samples (mono or interleaved - first channel used).</param>
    /// <param name="count">Number of samples to process.</param>
    /// <param name="channels">Number of audio channels (default: 1 for mono).</param>
    public void ProcessSamples(float[] samples, int count, int channels = 1)
    {
        for (int i = 0; i < count; i += channels)
        {
            _sampleBuffer[_sampleCount] = samples[i];
            _sampleCount++;

            if (_sampleCount >= _fftSize)
            {
                GenerateFrame();

                // Shift buffer by hop size
                int remaining = _fftSize - _hopSize;
                Array.Copy(_sampleBuffer, _hopSize, _sampleBuffer, 0, remaining);
                _sampleCount = remaining;
            }

            _totalSamplesProcessed++;
        }
    }

    /// <summary>
    /// Analyzes a complete audio buffer and returns all spectrogram frames.
    /// This is the preferred method for offline (non-real-time) analysis.
    /// </summary>
    /// <param name="samples">Complete audio buffer (mono).</param>
    /// <param name="sampleRate">Sample rate of the audio (uses analyzer's sample rate if 0).</param>
    /// <returns>Complete spectrogram result with all frames.</returns>
    public SpectrogramResult AnalyzeBuffer(float[] samples, int sampleRate = 0)
    {
        if (samples == null || samples.Length == 0)
            throw new ArgumentException("Samples cannot be null or empty.", nameof(samples));

        if (sampleRate == 0)
            sampleRate = _sampleRate;

        Reset();

        var frames = new List<SpectrogramFrame>();
        int position = 0;

        while (position + _fftSize <= samples.Length)
        {
            Array.Copy(samples, position, _sampleBuffer, 0, _fftSize);
            _sampleCount = _fftSize;

            var frame = CreateFrame((double)position / sampleRate, position);
            frames.Add(frame);

            position += _hopSize;
        }

        return new SpectrogramResult
        {
            Frames = frames.ToArray(),
            DurationSeconds = (double)samples.Length / sampleRate,
            MinFrequency = _minFrequency,
            MaxFrequency = _maxFrequency,
            FftSize = _fftSize,
            FramesPerSecond = _framesPerSecond,
            FrequencyResolution = FrequencyResolution,
            PeakHoldMagnitudes = _enablePeakHold ? (float[])_peakMagnitudes!.Clone() : null
        };
    }

    /// <summary>
    /// Resets the analyzer state.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _sampleCount = 0;
            _totalSamplesProcessed = 0;
            Array.Clear(_sampleBuffer, 0, _sampleBuffer.Length);
            Array.Clear(_fftBuffer, 0, _fftBuffer.Length);
            _frameHistory.Clear();

            if (_peakMagnitudes != null)
            {
                Array.Fill(_peakMagnitudes, float.MinValue);
            }
        }
    }

    /// <summary>
    /// Resets only the peak hold values.
    /// </summary>
    public void ResetPeaks()
    {
        lock (_lock)
        {
            if (_peakMagnitudes != null)
            {
                Array.Fill(_peakMagnitudes, float.MinValue);
            }
        }
    }

    /// <summary>
    /// Maps a magnitude value in dB to RGB color based on the current color map.
    /// </summary>
    /// <param name="magnitudeDb">Magnitude in dB.</param>
    /// <returns>RGB color tuple.</returns>
    public (byte R, byte G, byte B) MapMagnitudeToColor(float magnitudeDb)
    {
        // Normalize to 0-1 range
        float normalized = Math.Clamp((magnitudeDb - DbFloor) / (DbCeiling - DbFloor), 0f, 1f);
        return MapNormalizedToColor(normalized);
    }

    private void GenerateFrame()
    {
        double timeSeconds = (double)_totalSamplesProcessed / _sampleRate;
        var frame = CreateFrame(timeSeconds, _totalSamplesProcessed);

        lock (_lock)
        {
            _frameHistory.Add(frame);

            // Trim history if needed
            while (_frameHistory.Count > _maxHistoryFrames)
            {
                _frameHistory.RemoveAt(0);
            }
        }

        FrameGenerated?.Invoke(this, new SpectrogramFrameEventArgs(frame));
    }

    private SpectrogramFrame CreateFrame(double timeSeconds, long samplePosition)
    {
        // Apply window and copy to FFT buffer
        for (int i = 0; i < _fftSize; i++)
        {
            _fftBuffer[i].X = _sampleBuffer[i] * _window[i];
            _fftBuffer[i].Y = 0;
        }

        // Perform FFT
        int m = (int)Math.Log(_fftSize, 2.0);
        FastFourierTransform.FFT(true, m, _fftBuffer);

        // Extract magnitudes for the frequency range
        int binCount = _maxBin - _minBin + 1;
        float[] magnitudesDb = new float[binCount];
        float[] magnitudesLinear = new float[binCount];
        var colors = new (byte R, byte G, byte B)[binCount];

        for (int i = 0; i < binCount; i++)
        {
            int bin = _minBin + i;
            float real = _fftBuffer[bin].X;
            float imag = _fftBuffer[bin].Y;
            float magnitude = (float)Math.Sqrt(real * real + imag * imag);

            // Convert to dB
            float db = 20f * (float)Math.Log10(Math.Max(magnitude, 1e-10f));
            magnitudesDb[i] = db;

            // Normalize to 0-1 range
            float normalizedDb = Math.Clamp((db - DbFloor) / (DbCeiling - DbFloor), 0f, 1f);
            magnitudesLinear[i] = normalizedDb;

            // Map to color
            colors[i] = MapNormalizedToColor(normalizedDb);

            // Update peak hold
            if (_enablePeakHold && _peakMagnitudes != null)
            {
                if (db > _peakMagnitudes[i])
                {
                    _peakMagnitudes[i] = db;
                }
            }
        }

        return new SpectrogramFrame
        {
            TimeSeconds = timeSeconds,
            SamplePosition = samplePosition,
            FrequencyBins = (float[])_frequencyBins.Clone(),
            MagnitudesDb = magnitudesDb,
            MagnitudesLinear = magnitudesLinear,
            Colors = colors
        };
    }

    private (byte R, byte G, byte B) MapNormalizedToColor(float value)
    {
        return _colorMap switch
        {
            SpectrogramColorMap.Grayscale => MapGrayscale(value),
            SpectrogramColorMap.HeatMap => MapHeatMap(value),
            SpectrogramColorMap.Classic => MapClassic(value),
            SpectrogramColorMap.Cool => MapCool(value),
            SpectrogramColorMap.Warm => MapWarm(value),
            SpectrogramColorMap.Rainbow => MapRainbow(value),
            SpectrogramColorMap.Plasma => MapPlasma(value),
            _ => MapHeatMap(value)
        };
    }

    private static (byte R, byte G, byte B) MapGrayscale(float value)
    {
        byte v = (byte)(value * 255);
        return (v, v, v);
    }

    private static (byte R, byte G, byte B) MapHeatMap(float value)
    {
        // Black -> Blue -> Cyan -> Green -> Yellow -> Red -> White
        if (value < 0.142f)
        {
            float t = value / 0.142f;
            return (0, 0, (byte)(t * 255));
        }
        else if (value < 0.285f)
        {
            float t = (value - 0.142f) / 0.143f;
            return (0, (byte)(t * 255), 255);
        }
        else if (value < 0.428f)
        {
            float t = (value - 0.285f) / 0.143f;
            return (0, 255, (byte)((1 - t) * 255));
        }
        else if (value < 0.571f)
        {
            float t = (value - 0.428f) / 0.143f;
            return ((byte)(t * 255), 255, 0);
        }
        else if (value < 0.714f)
        {
            float t = (value - 0.571f) / 0.143f;
            return (255, (byte)((1 - t) * 255), 0);
        }
        else if (value < 0.857f)
        {
            float t = (value - 0.714f) / 0.143f;
            return (255, (byte)(t * 255), (byte)(t * 255));
        }
        else
        {
            return (255, 255, 255);
        }
    }

    private static (byte R, byte G, byte B) MapClassic(float value)
    {
        // Green -> Yellow -> Red
        if (value < 0.5f)
        {
            float t = value * 2;
            return ((byte)(t * 255), 255, 0);
        }
        else
        {
            float t = (value - 0.5f) * 2;
            return (255, (byte)((1 - t) * 255), 0);
        }
    }

    private static (byte R, byte G, byte B) MapCool(float value)
    {
        // Black -> Blue -> Cyan -> White
        if (value < 0.33f)
        {
            float t = value / 0.33f;
            return (0, 0, (byte)(t * 255));
        }
        else if (value < 0.66f)
        {
            float t = (value - 0.33f) / 0.33f;
            return (0, (byte)(t * 255), 255);
        }
        else
        {
            float t = (value - 0.66f) / 0.34f;
            return ((byte)(t * 255), 255, 255);
        }
    }

    private static (byte R, byte G, byte B) MapWarm(float value)
    {
        // Black -> Red -> Orange -> Yellow -> White
        if (value < 0.25f)
        {
            float t = value / 0.25f;
            return ((byte)(t * 255), 0, 0);
        }
        else if (value < 0.5f)
        {
            float t = (value - 0.25f) / 0.25f;
            return (255, (byte)(t * 165), 0);
        }
        else if (value < 0.75f)
        {
            float t = (value - 0.5f) / 0.25f;
            return (255, (byte)(165 + t * 90), 0);
        }
        else
        {
            float t = (value - 0.75f) / 0.25f;
            return (255, 255, (byte)(t * 255));
        }
    }

    private static (byte R, byte G, byte B) MapRainbow(float value)
    {
        // Violet -> Blue -> Cyan -> Green -> Yellow -> Orange -> Red
        float hue = (1 - value) * 270; // 270 degrees = violet, 0 = red
        return HslToRgb(hue, 1.0f, 0.5f);
    }

    private static (byte R, byte G, byte B) MapPlasma(float value)
    {
        // Black -> Purple -> Magenta -> Orange -> Yellow
        if (value < 0.25f)
        {
            float t = value / 0.25f;
            return ((byte)(t * 75), 0, (byte)(t * 130));
        }
        else if (value < 0.5f)
        {
            float t = (value - 0.25f) / 0.25f;
            return ((byte)(75 + t * 180), 0, (byte)(130 + t * 125));
        }
        else if (value < 0.75f)
        {
            float t = (value - 0.5f) / 0.25f;
            return (255, (byte)(t * 140), (byte)((1 - t) * 255));
        }
        else
        {
            float t = (value - 0.75f) / 0.25f;
            return (255, (byte)(140 + t * 115), 0);
        }
    }

    private static (byte R, byte G, byte B) HslToRgb(float hue, float saturation, float lightness)
    {
        float c = (1 - Math.Abs(2 * lightness - 1)) * saturation;
        float x = c * (1 - Math.Abs((hue / 60) % 2 - 1));
        float m = lightness - c / 2;

        float r, g, b;

        if (hue < 60)
        {
            r = c; g = x; b = 0;
        }
        else if (hue < 120)
        {
            r = x; g = c; b = 0;
        }
        else if (hue < 180)
        {
            r = 0; g = c; b = x;
        }
        else if (hue < 240)
        {
            r = 0; g = x; b = c;
        }
        else if (hue < 300)
        {
            r = x; g = 0; b = c;
        }
        else
        {
            r = c; g = 0; b = x;
        }

        return (
            (byte)((r + m) * 255),
            (byte)((g + m) * 255),
            (byte)((b + m) * 255)
        );
    }

    private static float[] GenerateHannWindow(int length)
    {
        float[] window = new float[length];
        for (int i = 0; i < length; i++)
        {
            window[i] = (float)(0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (length - 1))));
        }
        return window;
    }

    private static bool IsPowerOfTwo(int n) => n > 0 && (n & (n - 1)) == 0;
}
