// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: FFT spectrum analysis.

using System;
using NAudio.Dsp;
using NAudio.Wave;

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Multi-band spectrum analyzer using FFT for real-time frequency analysis.
/// Provides configurable band magnitudes (e.g., 31-band graphic EQ style).
/// </summary>
public class SpectrumAnalyzer
{
    private readonly int _fftLength;
    private readonly int _sampleRate;
    private readonly Complex[] _fftBuffer;
    private readonly float[] _sampleBuffer;
    private int _sampleCount;
    private readonly int _bandCount;
    private readonly float[] _bandFrequencies;
    private readonly float[] _bandMagnitudes;
    private readonly float[] _smoothedMagnitudes;
    private readonly float[] _peakMagnitudes;
    private readonly object _lock = new();

    // Smoothing and decay parameters
    private float _smoothingFactor = 0.3f;
    private float _peakDecayRate = 0.95f;

    /// <summary>
    /// Gets the number of frequency bands.
    /// </summary>
    public int BandCount => _bandCount;

    /// <summary>
    /// Gets the current band magnitudes (0.0 to 1.0 range, normalized).
    /// </summary>
    public float[] BandMagnitudes
    {
        get
        {
            lock (_lock)
            {
                return (float[])_smoothedMagnitudes.Clone();
            }
        }
    }

    /// <summary>
    /// Gets the peak hold magnitudes for each band.
    /// </summary>
    public float[] PeakMagnitudes
    {
        get
        {
            lock (_lock)
            {
                return (float[])_peakMagnitudes.Clone();
            }
        }
    }

    /// <summary>
    /// Gets the center frequencies for each band in Hz.
    /// </summary>
    public float[] BandFrequencies => (float[])_bandFrequencies.Clone();

    /// <summary>
    /// Gets or sets the smoothing factor (0.0 = no smoothing, 1.0 = maximum smoothing).
    /// </summary>
    public float SmoothingFactor
    {
        get => _smoothingFactor;
        set => _smoothingFactor = Math.Clamp(value, 0f, 0.99f);
    }

    /// <summary>
    /// Gets or sets the peak decay rate (0.0 = instant decay, 1.0 = no decay).
    /// </summary>
    public float PeakDecayRate
    {
        get => _peakDecayRate;
        set => _peakDecayRate = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Event raised when spectrum data is updated.
    /// </summary>
    public event EventHandler<SpectrumEventArgs>? SpectrumUpdated;

    /// <summary>
    /// Creates a new spectrum analyzer with the specified configuration.
    /// </summary>
    /// <param name="bandCount">Number of frequency bands (default: 31 for standard graphic EQ).</param>
    /// <param name="fftLength">FFT window size, must be power of 2 (default: 4096).</param>
    /// <param name="sampleRate">Audio sample rate in Hz (default: 44100).</param>
    /// <param name="minFrequency">Minimum frequency in Hz (default: 20).</param>
    /// <param name="maxFrequency">Maximum frequency in Hz (default: 20000).</param>
    public SpectrumAnalyzer(
        int bandCount = 31,
        int fftLength = 4096,
        int sampleRate = 44100,
        float minFrequency = 20f,
        float maxFrequency = 20000f)
    {
        if (!IsPowerOfTwo(fftLength))
            throw new ArgumentException("FFT length must be a power of two.", nameof(fftLength));
        if (bandCount < 1)
            throw new ArgumentOutOfRangeException(nameof(bandCount), "Band count must be at least 1.");
        if (minFrequency >= maxFrequency)
            throw new ArgumentException("Minimum frequency must be less than maximum frequency.");

        _fftLength = fftLength;
        _sampleRate = sampleRate;
        _bandCount = bandCount;
        _fftBuffer = new Complex[fftLength];
        _sampleBuffer = new float[fftLength];
        _bandMagnitudes = new float[bandCount];
        _smoothedMagnitudes = new float[bandCount];
        _peakMagnitudes = new float[bandCount];
        _bandFrequencies = CalculateBandFrequencies(bandCount, minFrequency, maxFrequency);
    }

    /// <summary>
    /// Processes audio samples and updates the spectrum analysis.
    /// </summary>
    /// <param name="samples">Audio samples (mono or interleaved stereo - first channel used).</param>
    /// <param name="count">Number of samples to process.</param>
    /// <param name="channels">Number of audio channels.</param>
    public void ProcessSamples(float[] samples, int count, int channels = 1)
    {
        for (int i = 0; i < count; i += channels)
        {
            // Use first channel (mono or left channel of stereo)
            _sampleBuffer[_sampleCount] = samples[i];
            _sampleCount++;

            if (_sampleCount >= _fftLength)
            {
                PerformFFT();
                _sampleCount = 0;
            }
        }
    }

    /// <summary>
    /// Resets the analyzer state.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _sampleCount = 0;
            Array.Clear(_sampleBuffer, 0, _sampleBuffer.Length);
            Array.Clear(_fftBuffer, 0, _fftBuffer.Length);
            Array.Clear(_bandMagnitudes, 0, _bandMagnitudes.Length);
            Array.Clear(_smoothedMagnitudes, 0, _smoothedMagnitudes.Length);
            Array.Clear(_peakMagnitudes, 0, _peakMagnitudes.Length);
        }
    }

    /// <summary>
    /// Resets only the peak hold values.
    /// </summary>
    public void ResetPeaks()
    {
        lock (_lock)
        {
            Array.Clear(_peakMagnitudes, 0, _peakMagnitudes.Length);
        }
    }

    private void PerformFFT()
    {
        // Apply Hann window and copy to FFT buffer
        for (int i = 0; i < _fftLength; i++)
        {
            float window = (float)(0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (_fftLength - 1))));
            _fftBuffer[i].X = _sampleBuffer[i] * window;
            _fftBuffer[i].Y = 0;
        }

        // Perform FFT
        int m = (int)Math.Log(_fftLength, 2.0);
        FastFourierTransform.FFT(true, m, _fftBuffer);

        // Calculate magnitudes for each band
        lock (_lock)
        {
            CalculateBandMagnitudes();
            ApplySmoothing();
            UpdatePeaks();
        }

        // Raise event
        SpectrumUpdated?.Invoke(this, new SpectrumEventArgs(
            (float[])_smoothedMagnitudes.Clone(),
            (float[])_peakMagnitudes.Clone(),
            _bandFrequencies));
    }

    private void CalculateBandMagnitudes()
    {
        float binResolution = (float)_sampleRate / _fftLength;
        int maxBin = _fftLength / 2;

        for (int band = 0; band < _bandCount; band++)
        {
            // Calculate frequency range for this band
            float lowFreq = band == 0 ? 0 : (_bandFrequencies[band - 1] + _bandFrequencies[band]) / 2;
            float highFreq = band == _bandCount - 1
                ? _sampleRate / 2f
                : (_bandFrequencies[band] + _bandFrequencies[band + 1]) / 2;

            int lowBin = Math.Max(1, (int)(lowFreq / binResolution));
            int highBin = Math.Min(maxBin - 1, (int)(highFreq / binResolution));

            if (lowBin > highBin)
            {
                lowBin = highBin = Math.Max(1, (int)(_bandFrequencies[band] / binResolution));
            }

            // Sum magnitudes in the band
            float sum = 0;
            int binCount = 0;
            for (int bin = lowBin; bin <= highBin; bin++)
            {
                float magnitude = (float)Math.Sqrt(
                    _fftBuffer[bin].X * _fftBuffer[bin].X +
                    _fftBuffer[bin].Y * _fftBuffer[bin].Y);
                sum += magnitude;
                binCount++;
            }

            // Average and normalize (rough normalization, adjust as needed)
            float avgMagnitude = binCount > 0 ? sum / binCount : 0;
            // Convert to dB-like scale (0-1 range)
            float normalized = (float)(20 * Math.Log10(Math.Max(avgMagnitude, 1e-10)) + 60) / 60f;
            _bandMagnitudes[band] = Math.Clamp(normalized, 0f, 1f);
        }
    }

    private void ApplySmoothing()
    {
        for (int i = 0; i < _bandCount; i++)
        {
            _smoothedMagnitudes[i] = _smoothedMagnitudes[i] * _smoothingFactor +
                                     _bandMagnitudes[i] * (1f - _smoothingFactor);
        }
    }

    private void UpdatePeaks()
    {
        for (int i = 0; i < _bandCount; i++)
        {
            if (_smoothedMagnitudes[i] > _peakMagnitudes[i])
            {
                _peakMagnitudes[i] = _smoothedMagnitudes[i];
            }
            else
            {
                _peakMagnitudes[i] *= _peakDecayRate;
            }
        }
    }

    private static float[] CalculateBandFrequencies(int bandCount, float minFreq, float maxFreq)
    {
        // Logarithmic frequency distribution (octave-based)
        float[] frequencies = new float[bandCount];
        float logMin = (float)Math.Log10(minFreq);
        float logMax = (float)Math.Log10(maxFreq);
        float logStep = (logMax - logMin) / (bandCount - 1);

        for (int i = 0; i < bandCount; i++)
        {
            frequencies[i] = (float)Math.Pow(10, logMin + i * logStep);
        }

        return frequencies;
    }

    private static bool IsPowerOfTwo(int n) => n > 0 && (n & (n - 1)) == 0;
}

/// <summary>
/// Event arguments for spectrum analysis updates.
/// </summary>
public class SpectrumEventArgs : EventArgs
{
    /// <summary>
    /// Band magnitudes (0.0 to 1.0 range).
    /// </summary>
    public float[] Magnitudes { get; }

    /// <summary>
    /// Peak hold magnitudes for each band.
    /// </summary>
    public float[] Peaks { get; }

    /// <summary>
    /// Center frequencies for each band in Hz.
    /// </summary>
    public float[] Frequencies { get; }

    public SpectrumEventArgs(float[] magnitudes, float[] peaks, float[] frequencies)
    {
        Magnitudes = magnitudes;
        Peaks = peaks;
        Frequencies = frequencies;
    }
}
