// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio analysis component.

using System;
using NAudio.Wave;

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Enhanced peak detector with 4x oversampling for true inter-sample peak detection.
/// Implements ITU-R BS.1770-4 compliant true peak measurement.
/// </summary>
public class EnhancedPeakDetector : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly int _channels;
    private readonly TruePeakChannel[] _channelDetectors;
    private readonly object _lock = new();

    // Peak values
    private readonly float[] _currentPeaks;
    private readonly float[] _maxPeaks;
    private float _maxTruePeak;

    // Update interval
    private int _samplesSinceUpdate;
    private readonly int _updateInterval;

    /// <summary>
    /// Gets the wave format of the audio stream.
    /// </summary>
    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>
    /// Gets the current true peak levels for each channel (linear scale).
    /// </summary>
    public float[] CurrentPeaks
    {
        get
        {
            lock (_lock)
            {
                return (float[])_currentPeaks.Clone();
            }
        }
    }

    /// <summary>
    /// Gets the maximum true peak levels since last reset for each channel (linear scale).
    /// </summary>
    public float[] MaxPeaks
    {
        get
        {
            lock (_lock)
            {
                return (float[])_maxPeaks.Clone();
            }
        }
    }

    /// <summary>
    /// Gets the overall maximum true peak in dBTP since last reset.
    /// </summary>
    public float MaxTruePeakDbtp
    {
        get
        {
            lock (_lock)
            {
                return 20f * (float)Math.Log10(Math.Max(_maxTruePeak, 1e-10f));
            }
        }
    }

    /// <summary>
    /// Gets the overall maximum true peak in linear scale since last reset.
    /// </summary>
    public float MaxTruePeakLinear
    {
        get
        {
            lock (_lock)
            {
                return _maxTruePeak;
            }
        }
    }

    /// <summary>
    /// Gets the current true peak levels in dBTP for each channel.
    /// </summary>
    public float[] CurrentPeaksDbtp
    {
        get
        {
            lock (_lock)
            {
                float[] result = new float[_channels];
                for (int i = 0; i < _channels; i++)
                {
                    result[i] = 20f * (float)Math.Log10(Math.Max(_currentPeaks[i], 1e-10f));
                }
                return result;
            }
        }
    }

    /// <summary>
    /// Event raised when peak values are updated.
    /// </summary>
    public event EventHandler<PeakEventArgs>? PeakUpdated;

    /// <summary>
    /// Creates a new enhanced peak detector wrapping the specified audio source.
    /// </summary>
    /// <param name="source">The audio source to analyze.</param>
    /// <param name="updateIntervalMs">Update interval in milliseconds (default: 50ms).</param>
    public EnhancedPeakDetector(ISampleProvider source, int updateIntervalMs = 50)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _channels = source.WaveFormat.Channels;

        _channelDetectors = new TruePeakChannel[_channels];
        for (int i = 0; i < _channels; i++)
        {
            _channelDetectors[i] = new TruePeakChannel();
        }

        _currentPeaks = new float[_channels];
        _maxPeaks = new float[_channels];
        _updateInterval = (int)(source.WaveFormat.SampleRate * updateIntervalMs / 1000.0);
    }

    /// <summary>
    /// Reads audio samples, detects true peaks, and passes through unchanged.
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
    /// <param name="samples">Audio samples (interleaved if multi-channel).</param>
    /// <param name="count">Number of samples.</param>
    public void AnalyzeSamples(float[] samples, int count)
    {
        ProcessSamples(samples, 0, count);
    }

    /// <summary>
    /// Resets all peak values.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            for (int i = 0; i < _channels; i++)
            {
                _channelDetectors[i].Reset();
                _currentPeaks[i] = 0;
                _maxPeaks[i] = 0;
            }
            _maxTruePeak = 0;
            _samplesSinceUpdate = 0;
        }
    }

    /// <summary>
    /// Resets only the maximum peak hold values.
    /// </summary>
    public void ResetMaxPeaks()
    {
        lock (_lock)
        {
            for (int i = 0; i < _channels; i++)
            {
                _maxPeaks[i] = 0;
            }
            _maxTruePeak = 0;
        }
    }

    private void ProcessSamples(float[] buffer, int offset, int count)
    {
        int frames = count / _channels;
        float[] framePeaks = new float[_channels];

        for (int frame = 0; frame < frames; frame++)
        {
            for (int ch = 0; ch < _channels; ch++)
            {
                float sample = buffer[offset + frame * _channels + ch];
                float truePeak = _channelDetectors[ch].ProcessSample(sample);

                if (truePeak > framePeaks[ch])
                {
                    framePeaks[ch] = truePeak;
                }
            }

            _samplesSinceUpdate++;

            if (_samplesSinceUpdate >= _updateInterval)
            {
                UpdatePeaks(framePeaks);
                Array.Clear(framePeaks, 0, framePeaks.Length);
                _samplesSinceUpdate = 0;
            }
        }
    }

    private void UpdatePeaks(float[] framePeaks)
    {
        lock (_lock)
        {
            for (int ch = 0; ch < _channels; ch++)
            {
                _currentPeaks[ch] = framePeaks[ch];

                if (framePeaks[ch] > _maxPeaks[ch])
                {
                    _maxPeaks[ch] = framePeaks[ch];
                }

                if (framePeaks[ch] > _maxTruePeak)
                {
                    _maxTruePeak = framePeaks[ch];
                }
            }
        }

        PeakUpdated?.Invoke(this, new PeakEventArgs(
            (float[])_currentPeaks.Clone(),
            (float[])_maxPeaks.Clone(),
            _maxTruePeak));
    }

    /// <summary>
    /// True peak detection for a single channel using 4x oversampling.
    /// Uses a polyphase FIR filter for efficient interpolation.
    /// </summary>
    private class TruePeakChannel
    {
        // 48-tap FIR filter coefficients for 4x oversampling (12 taps per phase)
        private const int FilterLength = 48;
        private const int OversampleFactor = 4;
        private const int TapsPerPhase = FilterLength / OversampleFactor;

        private static readonly float[][] PhaseCoefficients = GeneratePhaseCoefficients();
        private readonly float[] _history = new float[TapsPerPhase];
        private int _historyIndex;

        public void Reset()
        {
            Array.Clear(_history, 0, _history.Length);
            _historyIndex = 0;
        }

        public float ProcessSample(float sample)
        {
            // Add sample to history
            _history[_historyIndex] = sample;
            _historyIndex = (_historyIndex + 1) % TapsPerPhase;

            // Find maximum peak across all 4 interpolated samples
            float maxPeak = Math.Abs(sample);

            for (int phase = 0; phase < OversampleFactor; phase++)
            {
                float interpolated = 0;
                int histIdx = _historyIndex;

                for (int tap = 0; tap < TapsPerPhase; tap++)
                {
                    histIdx--;
                    if (histIdx < 0) histIdx = TapsPerPhase - 1;
                    interpolated += _history[histIdx] * PhaseCoefficients[phase][tap];
                }

                float absPeak = Math.Abs(interpolated);
                if (absPeak > maxPeak)
                {
                    maxPeak = absPeak;
                }
            }

            return maxPeak;
        }

        private static float[][] GeneratePhaseCoefficients()
        {
            // Generate sinc filter with Kaiser window for 4x oversampling
            float[][] phases = new float[OversampleFactor][];
            float[] fullFilter = new float[FilterLength];

            // Generate full filter (sinc with Kaiser window)
            const double beta = 5.0; // Kaiser window beta
            double halfLength = (FilterLength - 1) / 2.0;

            for (int i = 0; i < FilterLength; i++)
            {
                double n = i - halfLength;
                double sincArg = n / OversampleFactor;

                // Sinc function
                double sinc = Math.Abs(sincArg) < 1e-10
                    ? 1.0
                    : Math.Sin(Math.PI * sincArg) / (Math.PI * sincArg);

                // Kaiser window
                double x = 2.0 * i / (FilterLength - 1) - 1.0;
                double kaiser = BesselI0(beta * Math.Sqrt(1.0 - x * x)) / BesselI0(beta);

                fullFilter[i] = (float)(sinc * kaiser);
            }

            // Normalize each phase
            for (int phase = 0; phase < OversampleFactor; phase++)
            {
                phases[phase] = new float[TapsPerPhase];
                float sum = 0;

                for (int tap = 0; tap < TapsPerPhase; tap++)
                {
                    int filterIdx = tap * OversampleFactor + phase;
                    if (filterIdx < FilterLength)
                    {
                        phases[phase][tap] = fullFilter[filterIdx];
                        sum += Math.Abs(phases[phase][tap]);
                    }
                }

                // Normalize this phase
                if (sum > 0)
                {
                    for (int tap = 0; tap < TapsPerPhase; tap++)
                    {
                        phases[phase][tap] /= sum;
                    }
                }
            }

            return phases;
        }

        /// <summary>
        /// Modified Bessel function of the first kind, order 0.
        /// </summary>
        private static double BesselI0(double x)
        {
            double sum = 1.0;
            double term = 1.0;
            double xSquaredOver4 = x * x / 4.0;

            for (int k = 1; k <= 25; k++)
            {
                term *= xSquaredOver4 / (k * k);
                sum += term;
                if (term < 1e-12 * sum) break;
            }

            return sum;
        }
    }
}

/// <summary>
/// Event arguments for peak detection updates.
/// </summary>
public class PeakEventArgs : EventArgs
{
    /// <summary>
    /// Current true peak levels for each channel (linear scale).
    /// </summary>
    public float[] CurrentPeaks { get; }

    /// <summary>
    /// Maximum true peak levels since reset for each channel (linear scale).
    /// </summary>
    public float[] MaxPeaks { get; }

    /// <summary>
    /// Overall maximum true peak (linear scale).
    /// </summary>
    public float MaxTruePeak { get; }

    /// <summary>
    /// Overall maximum true peak in dBTP.
    /// </summary>
    public float MaxTruePeakDbtp => 20f * (float)Math.Log10(Math.Max(MaxTruePeak, 1e-10f));

    public PeakEventArgs(float[] currentPeaks, float[] maxPeaks, float maxTruePeak)
    {
        CurrentPeaks = currentPeaks;
        MaxPeaks = maxPeaks;
        MaxTruePeak = maxTruePeak;
    }
}
