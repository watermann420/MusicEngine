// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: LUFS loudness metering.

using System;
using NAudio.Wave;

namespace MusicEngine.Core;

/// <summary>
/// ITU-R BS.1770-4 compliant loudness meter implementing LUFS/LKFS measurement.
/// Provides integrated, short-term, momentary loudness and true peak measurements.
/// Uses K-weighting filter (high-shelf + high-pass) as per the standard.
/// </summary>
public class LoudnessMeter : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly int _sampleRate;
    private readonly int _channels;

    // K-weighting filter coefficients (ITU-R BS.1770-4)
    // First stage: high shelf filter
    private readonly double[] _hsB; // High shelf B coefficients
    private readonly double[] _hsA; // High shelf A coefficients

    // Second stage: high pass filter
    private readonly double[] _hpB; // High pass B coefficients
    private readonly double[] _hpA; // High pass A coefficients

    // Filter state for each channel (2 stages, 2 delay elements each)
    private readonly double[,] _hsState; // [channel, delay]
    private readonly double[,] _hpState;

    // Gating block parameters
    private const int MomentaryBlockMs = 400;  // 400ms blocks
    private const int ShortTermBlockMs = 3000; // 3 second sliding window
    private const double AbsoluteThreshold = -70.0; // LUFS absolute threshold
    private const double RelativeThreshold = -10.0; // dB below ungated loudness

    // Circular buffers for gating blocks
    private readonly double[] _momentaryBuffer;
    private int _momentaryWritePos;
    private int _momentarySampleCount;

    // Short-term (3 second) buffer of 400ms block loudness values
    private readonly double[] _shortTermBlocks;
    private int _shortTermBlockPos;
    private int _shortTermBlockCount;
    private readonly int _shortTermBlockCapacity;

    // Integrated loudness accumulator
    private readonly System.Collections.Generic.List<double> _gatedBlocks;
    private double _integratedLoudness;
    private bool _integratedValid;

    // Per-channel squared sum for momentary calculation
    private readonly double[] _channelSquaredSum;

    // True peak detection (oversampled)
    private readonly TruePeakDetector[] _truePeakDetectors;
    private double _maxTruePeak;

    /// <summary>
    /// Gets the wave format of the audio stream.
    /// </summary>
    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>
    /// Gets the integrated loudness (LUFS) since measurement started.
    /// This is the gated, program loudness as per ITU-R BS.1770-4.
    /// </summary>
    public double IntegratedLoudness => _integratedValid ? _integratedLoudness : double.NegativeInfinity;

    /// <summary>
    /// Gets the short-term loudness (LUFS) over the last 3 seconds.
    /// </summary>
    public double ShortTermLoudness { get; private set; } = double.NegativeInfinity;

    /// <summary>
    /// Gets the momentary loudness (LUFS) over the last 400ms.
    /// </summary>
    public double MomentaryLoudness { get; private set; } = double.NegativeInfinity;

    /// <summary>
    /// Gets the maximum true peak level in dBTP since measurement started.
    /// </summary>
    public double TruePeak => 20.0 * Math.Log10(Math.Max(_maxTruePeak, 1e-10));

    /// <summary>
    /// Gets the maximum true peak level in linear scale.
    /// </summary>
    public double TruePeakLinear => _maxTruePeak;

    /// <summary>
    /// Event raised when loudness values are updated (every 100ms).
    /// </summary>
    public event EventHandler<LoudnessEventArgs>? LoudnessUpdated;

    /// <summary>
    /// Creates a new loudness meter wrapping the specified audio source.
    /// </summary>
    /// <param name="source">The audio source to measure.</param>
    public LoudnessMeter(ISampleProvider source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _sampleRate = source.WaveFormat.SampleRate;
        _channels = source.WaveFormat.Channels;

        // Initialize K-weighting filter coefficients
        (_hsB, _hsA) = CalculateHighShelfCoefficients(_sampleRate);
        (_hpB, _hpA) = CalculateHighPassCoefficients(_sampleRate);

        // Initialize filter states
        _hsState = new double[_channels, 2];
        _hpState = new double[_channels, 2];

        // Initialize momentary buffer (400ms)
        int momentarySamples = (int)(_sampleRate * MomentaryBlockMs / 1000.0);
        _momentaryBuffer = new double[momentarySamples];
        _channelSquaredSum = new double[_channels];

        // Initialize short-term blocks (3 seconds worth of 400ms blocks with 75% overlap)
        // At 100ms hop, we need 30 blocks for 3 seconds
        _shortTermBlockCapacity = 30;
        _shortTermBlocks = new double[_shortTermBlockCapacity];

        // Initialize integrated loudness tracking
        _gatedBlocks = new System.Collections.Generic.List<double>();

        // Initialize true peak detectors (4x oversampling)
        _truePeakDetectors = new TruePeakDetector[_channels];
        for (int ch = 0; ch < _channels; ch++)
        {
            _truePeakDetectors[ch] = new TruePeakDetector();
        }
    }

    /// <summary>
    /// Resets all loudness measurements.
    /// </summary>
    public void Reset()
    {
        // Reset filter states
        Array.Clear(_hsState, 0, _hsState.Length);
        Array.Clear(_hpState, 0, _hpState.Length);

        // Reset buffers
        Array.Clear(_momentaryBuffer, 0, _momentaryBuffer.Length);
        _momentaryWritePos = 0;
        _momentarySampleCount = 0;

        Array.Clear(_shortTermBlocks, 0, _shortTermBlocks.Length);
        _shortTermBlockPos = 0;
        _shortTermBlockCount = 0;

        Array.Clear(_channelSquaredSum, 0, _channelSquaredSum.Length);

        // Reset integrated loudness
        _gatedBlocks.Clear();
        _integratedLoudness = double.NegativeInfinity;
        _integratedValid = false;

        // Reset loudness values
        ShortTermLoudness = double.NegativeInfinity;
        MomentaryLoudness = double.NegativeInfinity;

        // Reset true peak
        _maxTruePeak = 0;
        foreach (var detector in _truePeakDetectors)
        {
            detector.Reset();
        }
    }

    /// <summary>
    /// Reads audio samples, measures loudness, and passes through unchanged.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = _source.Read(buffer, offset, count);
        if (samplesRead == 0) return 0;

        ProcessSamples(buffer, offset, samplesRead);
        return samplesRead;
    }

    private void ProcessSamples(float[] buffer, int offset, int count)
    {
        int frames = count / _channels;

        for (int frame = 0; frame < frames; frame++)
        {
            double sumSquared = 0;

            for (int ch = 0; ch < _channels; ch++)
            {
                float sample = buffer[offset + frame * _channels + ch];

                // True peak detection (before K-weighting)
                double peak = _truePeakDetectors[ch].ProcessSample(sample);
                if (peak > _maxTruePeak)
                {
                    _maxTruePeak = peak;
                }

                // Apply K-weighting filter
                double filtered = ApplyKWeighting(sample, ch);

                // Channel weight (G weighting for surround - simplified for stereo)
                double weight = GetChannelWeight(ch);

                // Accumulate weighted squared sample
                sumSquared += weight * filtered * filtered;
            }

            // Add to momentary buffer
            _momentaryBuffer[_momentaryWritePos] = sumSquared;
            _momentaryWritePos = (_momentaryWritePos + 1) % _momentaryBuffer.Length;
            _momentarySampleCount++;

            // Check if we've completed a 100ms hop (for updating measurements)
            int hopSamples = (int)(_sampleRate * 0.1); // 100ms hop
            if (_momentarySampleCount >= hopSamples)
            {
                UpdateMeasurements();
                _momentarySampleCount -= hopSamples;
            }
        }
    }

    private double ApplyKWeighting(float sample, int channel)
    {
        // Stage 1: High shelf filter
        double x = sample;
        double y1 = _hsB[0] * x + _hsState[channel, 0];
        _hsState[channel, 0] = _hsB[1] * x - _hsA[1] * y1 + _hsState[channel, 1];
        _hsState[channel, 1] = _hsB[2] * x - _hsA[2] * y1;

        // Stage 2: High pass filter
        double y2 = _hpB[0] * y1 + _hpState[channel, 0];
        _hpState[channel, 0] = _hpB[1] * y1 - _hpA[1] * y2 + _hpState[channel, 1];
        _hpState[channel, 1] = _hpB[2] * y1 - _hpA[2] * y2;

        return y2;
    }

    private double GetChannelWeight(int channel)
    {
        // ITU-R BS.1770-4 channel weights
        // For stereo: L=1.0, R=1.0
        // For 5.1: L=1.0, R=1.0, C=1.0, LFE=0.0, Ls=1.41, Rs=1.41
        if (_channels <= 2)
        {
            return 1.0;
        }

        // 5.1 or more channels
        return channel switch
        {
            0 => 1.0,    // Left
            1 => 1.0,    // Right
            2 => 1.0,    // Center
            3 => 0.0,    // LFE (excluded)
            4 => 1.41,   // Left Surround
            5 => 1.41,   // Right Surround
            _ => 1.0
        };
    }

    private void UpdateMeasurements()
    {
        // Calculate momentary loudness (400ms window)
        double momentarySum = 0;
        for (int i = 0; i < _momentaryBuffer.Length; i++)
        {
            momentarySum += _momentaryBuffer[i];
        }
        double momentaryMeanSquare = momentarySum / _momentaryBuffer.Length;
        MomentaryLoudness = -0.691 + 10.0 * Math.Log10(Math.Max(momentaryMeanSquare, 1e-10));

        // Store block for short-term calculation
        _shortTermBlocks[_shortTermBlockPos] = momentaryMeanSquare;
        _shortTermBlockPos = (_shortTermBlockPos + 1) % _shortTermBlockCapacity;
        if (_shortTermBlockCount < _shortTermBlockCapacity)
        {
            _shortTermBlockCount++;
        }

        // Calculate short-term loudness (3 second window)
        if (_shortTermBlockCount > 0)
        {
            double shortTermSum = 0;
            for (int i = 0; i < _shortTermBlockCount; i++)
            {
                shortTermSum += _shortTermBlocks[i];
            }
            double shortTermMeanSquare = shortTermSum / _shortTermBlockCount;
            ShortTermLoudness = -0.691 + 10.0 * Math.Log10(Math.Max(shortTermMeanSquare, 1e-10));
        }

        // Add to gated blocks if above absolute threshold
        if (MomentaryLoudness > AbsoluteThreshold)
        {
            _gatedBlocks.Add(momentaryMeanSquare);
            UpdateIntegratedLoudness();
        }

        // Raise event
        LoudnessUpdated?.Invoke(this, new LoudnessEventArgs(
            IntegratedLoudness, ShortTermLoudness, MomentaryLoudness, TruePeak));
    }

    private void UpdateIntegratedLoudness()
    {
        if (_gatedBlocks.Count == 0)
        {
            _integratedValid = false;
            return;
        }

        // First pass: calculate ungated loudness (blocks above -70 LUFS)
        double ungatedSum = 0;
        foreach (var block in _gatedBlocks)
        {
            ungatedSum += block;
        }
        double ungatedLoudness = -0.691 + 10.0 * Math.Log10(ungatedSum / _gatedBlocks.Count);

        // Calculate relative threshold
        double relativeThresholdLufs = ungatedLoudness + RelativeThreshold;

        // Second pass: apply relative gating
        double gatedSum = 0;
        int gatedCount = 0;
        foreach (var block in _gatedBlocks)
        {
            double blockLoudness = -0.691 + 10.0 * Math.Log10(Math.Max(block, 1e-10));
            if (blockLoudness > relativeThresholdLufs)
            {
                gatedSum += block;
                gatedCount++;
            }
        }

        if (gatedCount > 0)
        {
            _integratedLoudness = -0.691 + 10.0 * Math.Log10(gatedSum / gatedCount);
            _integratedValid = true;
        }
    }

    /// <summary>
    /// Calculates the high shelf filter coefficients for K-weighting.
    /// ITU-R BS.1770-4: fc=1500Hz, gain=+4dB, Q=1/sqrt(2)
    /// </summary>
    private static (double[] b, double[] a) CalculateHighShelfCoefficients(int sampleRate)
    {
        double fc = 1681.974450955533;
        double G = 3.999843853973347;
        double Q = 0.7071752369554196;

        double K = Math.Tan(Math.PI * fc / sampleRate);
        double Vh = Math.Pow(10.0, G / 20.0);
        double Vb = Math.Pow(Vh, 0.4996667741545416);

        double a0 = 1.0 + K / Q + K * K;
        double[] b = new double[3];
        double[] a = new double[3];

        b[0] = (Vh + Vb * K / Q + K * K) / a0;
        b[1] = 2.0 * (K * K - Vh) / a0;
        b[2] = (Vh - Vb * K / Q + K * K) / a0;
        a[0] = 1.0;
        a[1] = 2.0 * (K * K - 1.0) / a0;
        a[2] = (1.0 - K / Q + K * K) / a0;

        return (b, a);
    }

    /// <summary>
    /// Calculates the high pass filter coefficients for K-weighting.
    /// ITU-R BS.1770-4: fc=38Hz, Q=0.5
    /// </summary>
    private static (double[] b, double[] a) CalculateHighPassCoefficients(int sampleRate)
    {
        double fc = 38.13547087602444;
        double Q = 0.5003270373238773;

        double K = Math.Tan(Math.PI * fc / sampleRate);

        double a0 = 1.0 + K / Q + K * K;
        double[] b = new double[3];
        double[] a = new double[3];

        b[0] = 1.0 / a0;
        b[1] = -2.0 / a0;
        b[2] = 1.0 / a0;
        a[0] = 1.0;
        a[1] = 2.0 * (K * K - 1.0) / a0;
        a[2] = (1.0 - K / Q + K * K) / a0;

        return (b, a);
    }

    /// <summary>
    /// True peak detector with 4x oversampling.
    /// </summary>
    private class TruePeakDetector
    {
        // FIR coefficients for 4x oversampling (48-tap sinc filter with Hann window)
        private static readonly double[] OversamplingCoeffs = GenerateOversamplingCoeffs();
        private readonly double[] _history;
        private int _historyPos;

        public TruePeakDetector()
        {
            _history = new double[OversamplingCoeffs.Length / 4];
        }

        public void Reset()
        {
            Array.Clear(_history, 0, _history.Length);
            _historyPos = 0;
        }

        public double ProcessSample(float sample)
        {
            // Add sample to history
            _history[_historyPos] = sample;
            _historyPos = (_historyPos + 1) % _history.Length;

            // Find peak across 4 interpolated samples
            double maxPeak = Math.Abs(sample);

            for (int phase = 0; phase < 4; phase++)
            {
                double interpolated = 0;
                int coeffIndex = phase;
                int histIndex = _historyPos;

                for (int i = 0; i < _history.Length; i++)
                {
                    histIndex--;
                    if (histIndex < 0) histIndex = _history.Length - 1;

                    if (coeffIndex < OversamplingCoeffs.Length)
                    {
                        interpolated += _history[histIndex] * OversamplingCoeffs[coeffIndex];
                    }
                    coeffIndex += 4;
                }

                double absPeak = Math.Abs(interpolated);
                if (absPeak > maxPeak)
                {
                    maxPeak = absPeak;
                }
            }

            return maxPeak;
        }

        private static double[] GenerateOversamplingCoeffs()
        {
            // Generate a 48-tap FIR filter for 4x oversampling
            const int taps = 48;
            const int oversampleFactor = 4;
            double[] coeffs = new double[taps];

            for (int i = 0; i < taps; i++)
            {
                double n = i - (taps - 1) / 2.0;
                double sincArg = n / oversampleFactor;

                // Sinc function
                double sinc = Math.Abs(sincArg) < 1e-10 ? 1.0 : Math.Sin(Math.PI * sincArg) / (Math.PI * sincArg);

                // Hann window
                double window = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (taps - 1)));

                coeffs[i] = sinc * window;
            }

            // Normalize
            double sum = 0;
            for (int phase = 0; phase < oversampleFactor; phase++)
            {
                double phaseSum = 0;
                for (int i = phase; i < taps; i += oversampleFactor)
                {
                    phaseSum += coeffs[i];
                }
                if (phaseSum > sum) sum = phaseSum;
            }

            for (int i = 0; i < taps; i++)
            {
                coeffs[i] /= sum;
            }

            return coeffs;
        }
    }
}

/// <summary>
/// Event arguments for loudness measurement updates.
/// </summary>
public class LoudnessEventArgs : EventArgs
{
    /// <summary>
    /// Integrated loudness in LUFS.
    /// </summary>
    public double IntegratedLoudness { get; }

    /// <summary>
    /// Short-term loudness in LUFS (3 second window).
    /// </summary>
    public double ShortTermLoudness { get; }

    /// <summary>
    /// Momentary loudness in LUFS (400ms window).
    /// </summary>
    public double MomentaryLoudness { get; }

    /// <summary>
    /// True peak level in dBTP.
    /// </summary>
    public double TruePeak { get; }

    public LoudnessEventArgs(double integrated, double shortTerm, double momentary, double truePeak)
    {
        IntegratedLoudness = integrated;
        ShortTermLoudness = shortTerm;
        MomentaryLoudness = momentary;
        TruePeak = truePeak;
    }
}
