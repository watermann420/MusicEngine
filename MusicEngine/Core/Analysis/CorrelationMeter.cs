// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio analysis component.

using System;
using NAudio.Wave;

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Stereo correlation meter that measures the phase correlation between left and right channels.
/// Also provides Mid/Side ratio analysis for stereo image evaluation.
/// Correlation: -1 (out of phase) to +1 (mono/in phase), 0 = uncorrelated stereo.
/// </summary>
public class CorrelationMeter : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly int _windowSize;
    private readonly float[] _leftBuffer;
    private readonly float[] _rightBuffer;
    private int _bufferPosition;
    private int _samplesInBuffer;
    private readonly object _lock = new();

    // Smoothed values
    private double _correlation;
    private double _midLevel;
    private double _sideLevel;
    private double _msRatio;

    // Smoothing factor
    private float _smoothingFactor = 0.9f;

    /// <summary>
    /// Gets the wave format of the audio stream.
    /// </summary>
    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>
    /// Gets the current stereo correlation value.
    /// Range: -1.0 (fully out of phase) to +1.0 (mono/fully in phase).
    /// Values around 0 indicate uncorrelated stereo content.
    /// </summary>
    public double Correlation
    {
        get
        {
            lock (_lock)
            {
                return _correlation;
            }
        }
    }

    /// <summary>
    /// Gets the current Mid (L+R) level in linear scale.
    /// </summary>
    public double MidLevel
    {
        get
        {
            lock (_lock)
            {
                return _midLevel;
            }
        }
    }

    /// <summary>
    /// Gets the current Side (L-R) level in linear scale.
    /// </summary>
    public double SideLevel
    {
        get
        {
            lock (_lock)
            {
                return _sideLevel;
            }
        }
    }

    /// <summary>
    /// Gets the Mid/Side ratio (0 = all side/stereo, 1 = all mid/mono).
    /// Values closer to 0.5 indicate a balanced stereo mix.
    /// </summary>
    public double MSRatio
    {
        get
        {
            lock (_lock)
            {
                return _msRatio;
            }
        }
    }

    /// <summary>
    /// Gets or sets the smoothing factor for metering (0.0 = no smoothing, close to 1.0 = heavy smoothing).
    /// </summary>
    public float SmoothingFactor
    {
        get => _smoothingFactor;
        set => _smoothingFactor = Math.Clamp(value, 0f, 0.99f);
    }

    /// <summary>
    /// Event raised when correlation values are updated.
    /// </summary>
    public event EventHandler<CorrelationEventArgs>? CorrelationUpdated;

    /// <summary>
    /// Creates a new correlation meter wrapping the specified stereo audio source.
    /// </summary>
    /// <param name="source">The stereo audio source to analyze. Must have 2 channels.</param>
    /// <param name="windowSizeMs">Analysis window size in milliseconds (default: 50ms).</param>
    public CorrelationMeter(ISampleProvider source, int windowSizeMs = 50)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));

        if (source.WaveFormat.Channels != 2)
            throw new ArgumentException("Correlation meter requires a stereo (2-channel) source.", nameof(source));

        _windowSize = (int)(source.WaveFormat.SampleRate * windowSizeMs / 1000.0);
        _leftBuffer = new float[_windowSize];
        _rightBuffer = new float[_windowSize];
    }

    /// <summary>
    /// Reads audio samples, calculates correlation, and passes through unchanged.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = _source.Read(buffer, offset, count);
        if (samplesRead == 0) return 0;

        ProcessSamples(buffer, offset, samplesRead);
        return samplesRead;
    }

    /// <summary>
    /// Process samples without being in the signal chain (for external analysis).
    /// </summary>
    /// <param name="samples">Interleaved stereo samples.</param>
    /// <param name="count">Number of samples (must be even for stereo).</param>
    public void AnalyzeSamples(float[] samples, int count)
    {
        ProcessSamples(samples, 0, count);
    }

    /// <summary>
    /// Resets the meter state.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            Array.Clear(_leftBuffer, 0, _leftBuffer.Length);
            Array.Clear(_rightBuffer, 0, _rightBuffer.Length);
            _bufferPosition = 0;
            _samplesInBuffer = 0;
            _correlation = 0;
            _midLevel = 0;
            _sideLevel = 0;
            _msRatio = 0.5;
        }
    }

    private void ProcessSamples(float[] buffer, int offset, int count)
    {
        int frames = count / 2; // Stereo interleaved

        for (int i = 0; i < frames; i++)
        {
            float left = buffer[offset + i * 2];
            float right = buffer[offset + i * 2 + 1];

            _leftBuffer[_bufferPosition] = left;
            _rightBuffer[_bufferPosition] = right;

            _bufferPosition = (_bufferPosition + 1) % _windowSize;
            _samplesInBuffer = Math.Min(_samplesInBuffer + 1, _windowSize);
        }

        // Calculate correlation when buffer has enough samples
        if (_samplesInBuffer >= _windowSize / 2)
        {
            CalculateCorrelation();
        }
    }

    private void CalculateCorrelation()
    {
        double sumL = 0, sumR = 0;
        double sumLR = 0;
        double sumL2 = 0, sumR2 = 0;
        double sumMid = 0, sumSide = 0;

        int n = _samplesInBuffer;

        for (int i = 0; i < n; i++)
        {
            double l = _leftBuffer[i];
            double r = _rightBuffer[i];

            sumL += l;
            sumR += r;
            sumLR += l * r;
            sumL2 += l * l;
            sumR2 += r * r;

            // Mid/Side calculation
            double mid = (l + r) * 0.5;
            double side = (l - r) * 0.5;
            sumMid += mid * mid;
            sumSide += side * side;
        }

        // Pearson correlation coefficient
        double meanL = sumL / n;
        double meanR = sumR / n;
        double varL = (sumL2 / n) - (meanL * meanL);
        double varR = (sumR2 / n) - (meanR * meanR);
        double covariance = (sumLR / n) - (meanL * meanR);

        double stdL = Math.Sqrt(Math.Max(varL, 1e-10));
        double stdR = Math.Sqrt(Math.Max(varR, 1e-10));

        double newCorrelation = covariance / (stdL * stdR);
        newCorrelation = Math.Clamp(newCorrelation, -1.0, 1.0);

        // RMS levels for Mid and Side
        double newMidLevel = Math.Sqrt(sumMid / n);
        double newSideLevel = Math.Sqrt(sumSide / n);

        // M/S Ratio (0 = all side, 1 = all mid)
        double totalLevel = newMidLevel + newSideLevel;
        double newMsRatio = totalLevel > 1e-10 ? newMidLevel / totalLevel : 0.5;

        // Apply smoothing
        lock (_lock)
        {
            _correlation = _correlation * _smoothingFactor + newCorrelation * (1 - _smoothingFactor);
            _midLevel = _midLevel * _smoothingFactor + newMidLevel * (1 - _smoothingFactor);
            _sideLevel = _sideLevel * _smoothingFactor + newSideLevel * (1 - _smoothingFactor);
            _msRatio = _msRatio * _smoothingFactor + newMsRatio * (1 - _smoothingFactor);
        }

        // Raise event
        CorrelationUpdated?.Invoke(this, new CorrelationEventArgs(
            _correlation, _midLevel, _sideLevel, _msRatio));
    }
}

/// <summary>
/// Event arguments for correlation meter updates.
/// </summary>
public class CorrelationEventArgs : EventArgs
{
    /// <summary>
    /// Stereo correlation value (-1.0 to +1.0).
    /// </summary>
    public double Correlation { get; }

    /// <summary>
    /// Mid (L+R) level in linear scale.
    /// </summary>
    public double MidLevel { get; }

    /// <summary>
    /// Side (L-R) level in linear scale.
    /// </summary>
    public double SideLevel { get; }

    /// <summary>
    /// Mid/Side ratio (0 = all side, 1 = all mid).
    /// </summary>
    public double MSRatio { get; }

    public CorrelationEventArgs(double correlation, double midLevel, double sideLevel, double msRatio)
    {
        Correlation = correlation;
        MidLevel = midLevel;
        SideLevel = sideLevel;
        MSRatio = msRatio;
    }
}
