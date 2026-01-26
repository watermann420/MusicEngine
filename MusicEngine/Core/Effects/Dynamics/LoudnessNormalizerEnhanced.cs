// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using NAudio.Wave;

namespace MusicEngine.Core.Effects.Dynamics;

/// <summary>
/// Compliance mode for loudness normalization.
/// </summary>
public enum LoudnessComplianceMode
{
    /// <summary>
    /// EBU R128 broadcast standard (-23 LUFS target).
    /// </summary>
    EBUR128,

    /// <summary>
    /// ATSC A/85 broadcast standard (-24 LKFS target).
    /// </summary>
    ATSCA85,

    /// <summary>
    /// Streaming platforms target (-14 LUFS).
    /// </summary>
    Streaming,

    /// <summary>
    /// Custom target loudness.
    /// </summary>
    Custom
}

/// <summary>
/// Enhanced loudness normalizer with EBU R128, ATSC A/85, and streaming compliance modes.
/// Provides true peak limiting, loudness range (LRA) analysis, and short-term/momentary tracking.
/// </summary>
/// <remarks>
/// Implements ITU-R BS.1770-4 compliant LUFS measurement with K-weighting filter.
/// Supports real-time gain adjustment to reach target loudness while preventing true peak clipping.
/// </remarks>
public class LoudnessNormalizerEnhanced : EffectBase
{
    // K-weighting filter coefficients
    private readonly double[] _hsB; // High shelf B coefficients
    private readonly double[] _hsA; // High shelf A coefficients
    private readonly double[] _hpB; // High pass B coefficients
    private readonly double[] _hpA; // High pass A coefficients

    // Filter state per channel
    private readonly double[,] _hsState;
    private readonly double[,] _hpState;

    // Measurement buffers
    private const int MomentaryBlockMs = 400;
    private const int ShortTermBlockMs = 3000;
    private readonly double[] _momentaryBuffer;
    private int _momentaryWritePos;
    private int _momentarySampleCount;

    // Short-term buffer (3 seconds of 400ms blocks)
    private readonly double[] _shortTermBlocks;
    private int _shortTermBlockPos;
    private int _shortTermBlockCount;
    private const int ShortTermBlockCapacity = 30;

    // LRA tracking
    private readonly System.Collections.Generic.List<double> _lraBlocks;
    private const double LraLowPercentile = 0.10;
    private const double LraHighPercentile = 0.95;

    // True peak detection
    private readonly TruePeakLimiter[] _truePeakLimiters;

    // Gain control
    private double _currentGain = 1.0;
    private double _targetGain = 1.0;
    private const double GainSmoothingCoeff = 0.9995;

    // Mode and targets
    private LoudnessComplianceMode _complianceMode = LoudnessComplianceMode.EBUR128;
    private double _targetLufs = -23.0;
    private double _truePeakLimit = -1.0; // dBTP

    /// <summary>
    /// Creates a new enhanced loudness normalizer.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    public LoudnessNormalizerEnhanced(ISampleProvider source) : this(source, "Loudness Normalizer") { }

    /// <summary>
    /// Creates a new enhanced loudness normalizer with a custom name.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <param name="name">Effect name.</param>
    public LoudnessNormalizerEnhanced(ISampleProvider source, string name) : base(source, name)
    {
        int channels = Channels;

        // Initialize K-weighting filter coefficients
        (_hsB, _hsA) = CalculateHighShelfCoefficients(SampleRate);
        (_hpB, _hpA) = CalculateHighPassCoefficients(SampleRate);

        // Initialize filter states
        _hsState = new double[channels, 2];
        _hpState = new double[channels, 2];

        // Initialize momentary buffer (400ms)
        int momentarySamples = (int)(SampleRate * MomentaryBlockMs / 1000.0);
        _momentaryBuffer = new double[momentarySamples];

        // Initialize short-term blocks
        _shortTermBlocks = new double[ShortTermBlockCapacity];

        // Initialize LRA tracking
        _lraBlocks = new System.Collections.Generic.List<double>();

        // Initialize true peak limiters
        _truePeakLimiters = new TruePeakLimiter[channels];
        for (int ch = 0; ch < channels; ch++)
        {
            _truePeakLimiters[ch] = new TruePeakLimiter(SampleRate);
        }

        // Register parameters
        RegisterParameter("TargetLufs", -23f);
        RegisterParameter("TruePeakLimit", -1f);
        RegisterParameter("AttackMs", 5f);
        RegisterParameter("ReleaseMs", 100f);
        RegisterParameter("EnableLimiter", 1f);

        Mix = 1.0f;
    }

    /// <summary>
    /// Gets or sets the compliance mode.
    /// </summary>
    public LoudnessComplianceMode ComplianceMode
    {
        get => _complianceMode;
        set
        {
            _complianceMode = value;
            _targetLufs = value switch
            {
                LoudnessComplianceMode.EBUR128 => -23.0,
                LoudnessComplianceMode.ATSCA85 => -24.0,
                LoudnessComplianceMode.Streaming => -14.0,
                _ => _targetLufs
            };
        }
    }

    /// <summary>
    /// Target loudness in LUFS (-60 to 0).
    /// </summary>
    public double TargetLufs
    {
        get => _targetLufs;
        set
        {
            _targetLufs = Math.Clamp(value, -60.0, 0.0);
            if (_complianceMode != LoudnessComplianceMode.Custom)
            {
                _complianceMode = LoudnessComplianceMode.Custom;
            }
            SetParameter("TargetLufs", (float)_targetLufs);
        }
    }

    /// <summary>
    /// True peak limit in dBTP (-10 to 0).
    /// </summary>
    public double TruePeakLimit
    {
        get => _truePeakLimit;
        set
        {
            _truePeakLimit = Math.Clamp(value, -10.0, 0.0);
            SetParameter("TruePeakLimit", (float)_truePeakLimit);
            foreach (var limiter in _truePeakLimiters)
            {
                limiter.SetCeiling(_truePeakLimit);
            }
        }
    }

    /// <summary>
    /// Gets whether the true peak limiter is enabled.
    /// </summary>
    public bool LimiterEnabled
    {
        get => GetParameter("EnableLimiter") > 0.5f;
        set => SetParameter("EnableLimiter", value ? 1f : 0f);
    }

    /// <summary>
    /// Gets the current momentary loudness in LUFS.
    /// </summary>
    public double MomentaryLoudness { get; private set; } = double.NegativeInfinity;

    /// <summary>
    /// Gets the current short-term loudness in LUFS.
    /// </summary>
    public double ShortTermLoudness { get; private set; } = double.NegativeInfinity;

    /// <summary>
    /// Gets the current integrated loudness in LUFS.
    /// </summary>
    public double IntegratedLoudness { get; private set; } = double.NegativeInfinity;

    /// <summary>
    /// Gets the loudness range (LRA) in LU.
    /// </summary>
    public double LoudnessRange { get; private set; }

    /// <summary>
    /// Gets the current gain being applied in dB.
    /// </summary>
    public double CurrentGainDb => 20.0 * Math.Log10(Math.Max(_currentGain, 1e-10));

    /// <summary>
    /// Gets the maximum true peak level detected in dBTP.
    /// </summary>
    public double MaxTruePeak { get; private set; } = double.NegativeInfinity;

    /// <summary>
    /// Event raised when loudness measurements are updated.
    /// </summary>
    public event EventHandler<LoudnessNormalizerEventArgs>? MeasurementUpdated;

    /// <summary>
    /// Resets all measurements and gain.
    /// </summary>
    public void Reset()
    {
        Array.Clear(_hsState, 0, _hsState.Length);
        Array.Clear(_hpState, 0, _hpState.Length);
        Array.Clear(_momentaryBuffer, 0, _momentaryBuffer.Length);
        Array.Clear(_shortTermBlocks, 0, _shortTermBlocks.Length);

        _momentaryWritePos = 0;
        _momentarySampleCount = 0;
        _shortTermBlockPos = 0;
        _shortTermBlockCount = 0;

        _lraBlocks.Clear();

        _currentGain = 1.0;
        _targetGain = 1.0;

        MomentaryLoudness = double.NegativeInfinity;
        ShortTermLoudness = double.NegativeInfinity;
        IntegratedLoudness = double.NegativeInfinity;
        LoudnessRange = 0;
        MaxTruePeak = double.NegativeInfinity;

        foreach (var limiter in _truePeakLimiters)
        {
            limiter.Reset();
        }
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;
        int frames = count / channels;
        int hopSamples = (int)(SampleRate * 0.1); // 100ms hop
        bool limiterEnabled = LimiterEnabled;

        for (int frame = 0; frame < frames; frame++)
        {
            double sumSquared = 0;

            for (int ch = 0; ch < channels; ch++)
            {
                int sampleIndex = frame * channels + ch;
                float sample = sourceBuffer[sampleIndex];

                // Apply K-weighting for measurement
                double filtered = ApplyKWeighting(sample, ch);
                double weight = GetChannelWeight(ch);
                sumSquared += weight * filtered * filtered;
            }

            // Add to momentary buffer
            _momentaryBuffer[_momentaryWritePos] = sumSquared;
            _momentaryWritePos = (_momentaryWritePos + 1) % _momentaryBuffer.Length;
            _momentarySampleCount++;

            // Update measurements every 100ms
            if (_momentarySampleCount >= hopSamples)
            {
                UpdateMeasurements();
                _momentarySampleCount -= hopSamples;
            }

            // Apply gain and limiting
            for (int ch = 0; ch < channels; ch++)
            {
                int sampleIndex = frame * channels + ch;
                float sample = sourceBuffer[sampleIndex];

                // Smooth gain transition
                _currentGain = _currentGain * GainSmoothingCoeff + _targetGain * (1.0 - GainSmoothingCoeff);

                // Apply gain
                sample *= (float)_currentGain;

                // Apply true peak limiting if enabled
                if (limiterEnabled)
                {
                    sample = _truePeakLimiters[ch].Process(sample);
                }

                destBuffer[offset + sampleIndex] = sample;
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
        if (Channels <= 2) return 1.0;

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
        _shortTermBlockPos = (_shortTermBlockPos + 1) % ShortTermBlockCapacity;
        if (_shortTermBlockCount < ShortTermBlockCapacity)
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

            // Update target gain based on short-term loudness
            if (ShortTermLoudness > -70.0)
            {
                double gainDb = _targetLufs - ShortTermLoudness;
                _targetGain = Math.Pow(10.0, gainDb / 20.0);

                // Limit gain range
                _targetGain = Math.Clamp(_targetGain, 0.01, 10.0);
            }
        }

        // Track LRA (blocks above -70 LUFS)
        if (MomentaryLoudness > -70.0)
        {
            _lraBlocks.Add(MomentaryLoudness);
            UpdateLoudnessRange();
            UpdateIntegratedLoudness();
        }

        // Update max true peak
        foreach (var limiter in _truePeakLimiters)
        {
            double peak = 20.0 * Math.Log10(Math.Max(limiter.MaxPeak, 1e-10));
            if (peak > MaxTruePeak)
            {
                MaxTruePeak = peak;
            }
        }

        // Raise event
        MeasurementUpdated?.Invoke(this, new LoudnessNormalizerEventArgs(
            IntegratedLoudness, ShortTermLoudness, MomentaryLoudness, LoudnessRange, MaxTruePeak, CurrentGainDb));
    }

    private void UpdateIntegratedLoudness()
    {
        if (_lraBlocks.Count == 0) return;

        // First pass: calculate ungated loudness
        double sum = 0;
        foreach (var block in _lraBlocks)
        {
            sum += Math.Pow(10.0, (block + 0.691) / 10.0);
        }
        double ungatedLoudness = -0.691 + 10.0 * Math.Log10(sum / _lraBlocks.Count);

        // Second pass: apply relative gating (-10 dB below ungated)
        double relativeThreshold = ungatedLoudness - 10.0;
        double gatedSum = 0;
        int gatedCount = 0;

        foreach (var block in _lraBlocks)
        {
            if (block > relativeThreshold)
            {
                gatedSum += Math.Pow(10.0, (block + 0.691) / 10.0);
                gatedCount++;
            }
        }

        if (gatedCount > 0)
        {
            IntegratedLoudness = -0.691 + 10.0 * Math.Log10(gatedSum / gatedCount);
        }
    }

    private void UpdateLoudnessRange()
    {
        if (_lraBlocks.Count < 2)
        {
            LoudnessRange = 0;
            return;
        }

        // Sort blocks for percentile calculation
        var sorted = new System.Collections.Generic.List<double>(_lraBlocks);
        sorted.Sort();

        // Apply relative gating for LRA
        double ungatedSum = 0;
        foreach (var block in sorted)
        {
            ungatedSum += Math.Pow(10.0, (block + 0.691) / 10.0);
        }
        double ungatedLoudness = -0.691 + 10.0 * Math.Log10(ungatedSum / sorted.Count);
        double relativeThreshold = ungatedLoudness - 20.0; // -20 dB for LRA

        var gated = new System.Collections.Generic.List<double>();
        foreach (var block in sorted)
        {
            if (block > relativeThreshold)
            {
                gated.Add(block);
            }
        }

        if (gated.Count < 2)
        {
            LoudnessRange = 0;
            return;
        }

        // Calculate percentiles
        int lowIndex = (int)(gated.Count * LraLowPercentile);
        int highIndex = (int)(gated.Count * LraHighPercentile);

        lowIndex = Math.Clamp(lowIndex, 0, gated.Count - 1);
        highIndex = Math.Clamp(highIndex, 0, gated.Count - 1);

        LoudnessRange = gated[highIndex] - gated[lowIndex];
    }

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

    #region True Peak Limiter

    /// <summary>
    /// True peak limiter with 4x oversampling and lookahead.
    /// </summary>
    private class TruePeakLimiter
    {
        private readonly int _sampleRate;
        private readonly double[] _oversampleCoeffs;
        private readonly double[] _history;
        private int _historyPos;

        // Lookahead buffer
        private readonly float[] _lookaheadBuffer;
        private int _lookaheadPos;
        private const int LookaheadSamples = 64;

        // Gain reduction
        private double _gainReduction = 1.0;
        private double _ceiling = 0.891; // -1 dBTP
        private const double AttackCoeff = 0.9;
        private const double ReleaseCoeff = 0.9995;

        public double MaxPeak { get; private set; }

        public TruePeakLimiter(int sampleRate)
        {
            _sampleRate = sampleRate;
            _oversampleCoeffs = GenerateOversamplingCoeffs();
            _history = new double[_oversampleCoeffs.Length / 4];
            _lookaheadBuffer = new float[LookaheadSamples];
        }

        public void SetCeiling(double dBTP)
        {
            _ceiling = Math.Pow(10.0, dBTP / 20.0);
        }

        public void Reset()
        {
            Array.Clear(_history, 0, _history.Length);
            Array.Clear(_lookaheadBuffer, 0, _lookaheadBuffer.Length);
            _historyPos = 0;
            _lookaheadPos = 0;
            _gainReduction = 1.0;
            MaxPeak = 0;
        }

        public float Process(float sample)
        {
            // Detect true peak
            double truePeak = DetectTruePeak(sample);
            if (truePeak > MaxPeak)
            {
                MaxPeak = truePeak;
            }

            // Calculate required gain reduction
            double targetGain = 1.0;
            if (truePeak > _ceiling)
            {
                targetGain = _ceiling / truePeak;
            }

            // Apply attack/release
            if (targetGain < _gainReduction)
            {
                _gainReduction = _gainReduction * AttackCoeff + targetGain * (1.0 - AttackCoeff);
            }
            else
            {
                _gainReduction = _gainReduction * ReleaseCoeff + targetGain * (1.0 - ReleaseCoeff);
            }

            // Apply gain with lookahead
            float delayed = _lookaheadBuffer[_lookaheadPos];
            _lookaheadBuffer[_lookaheadPos] = sample;
            _lookaheadPos = (_lookaheadPos + 1) % LookaheadSamples;

            return delayed * (float)_gainReduction;
        }

        private double DetectTruePeak(float sample)
        {
            _history[_historyPos] = sample;
            _historyPos = (_historyPos + 1) % _history.Length;

            double maxPeak = Math.Abs(sample);

            // Check 4 interpolated samples
            for (int phase = 0; phase < 4; phase++)
            {
                double interpolated = 0;
                int coeffIndex = phase;
                int histIndex = _historyPos;

                for (int i = 0; i < _history.Length; i++)
                {
                    histIndex--;
                    if (histIndex < 0) histIndex = _history.Length - 1;

                    if (coeffIndex < _oversampleCoeffs.Length)
                    {
                        interpolated += _history[histIndex] * _oversampleCoeffs[coeffIndex];
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
            const int taps = 48;
            const int oversampleFactor = 4;
            double[] coeffs = new double[taps];

            for (int i = 0; i < taps; i++)
            {
                double n = i - (taps - 1) / 2.0;
                double sincArg = n / oversampleFactor;
                double sinc = Math.Abs(sincArg) < 1e-10 ? 1.0 : Math.Sin(Math.PI * sincArg) / (Math.PI * sincArg);
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

    #endregion

    #region Presets

    /// <summary>
    /// Creates an EBU R128 broadcast preset (-23 LUFS).
    /// </summary>
    public static LoudnessNormalizerEnhanced CreateBroadcastPreset(ISampleProvider source)
    {
        var normalizer = new LoudnessNormalizerEnhanced(source, "Broadcast Normalizer");
        normalizer.ComplianceMode = LoudnessComplianceMode.EBUR128;
        normalizer.TruePeakLimit = -1.0;
        normalizer.LimiterEnabled = true;
        return normalizer;
    }

    /// <summary>
    /// Creates an ATSC A/85 preset (-24 LKFS).
    /// </summary>
    public static LoudnessNormalizerEnhanced CreateATSCPreset(ISampleProvider source)
    {
        var normalizer = new LoudnessNormalizerEnhanced(source, "ATSC Normalizer");
        normalizer.ComplianceMode = LoudnessComplianceMode.ATSCA85;
        normalizer.TruePeakLimit = -2.0;
        normalizer.LimiterEnabled = true;
        return normalizer;
    }

    /// <summary>
    /// Creates a streaming platform preset (-14 LUFS).
    /// </summary>
    public static LoudnessNormalizerEnhanced CreateStreamingPreset(ISampleProvider source)
    {
        var normalizer = new LoudnessNormalizerEnhanced(source, "Streaming Normalizer");
        normalizer.ComplianceMode = LoudnessComplianceMode.Streaming;
        normalizer.TruePeakLimit = -1.0;
        normalizer.LimiterEnabled = true;
        return normalizer;
    }

    #endregion
}

/// <summary>
/// Event arguments for loudness normalizer measurement updates.
/// </summary>
public class LoudnessNormalizerEventArgs : EventArgs
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
    /// Loudness range in LU.
    /// </summary>
    public double LoudnessRange { get; }

    /// <summary>
    /// Maximum true peak level in dBTP.
    /// </summary>
    public double MaxTruePeak { get; }

    /// <summary>
    /// Current gain being applied in dB.
    /// </summary>
    public double CurrentGainDb { get; }

    public LoudnessNormalizerEventArgs(
        double integrated, double shortTerm, double momentary,
        double lra, double truePeak, double gainDb)
    {
        IntegratedLoudness = integrated;
        ShortTermLoudness = shortTerm;
        MomentaryLoudness = momentary;
        LoudnessRange = lra;
        MaxTruePeak = truePeak;
        CurrentGainDb = gainDb;
    }
}
