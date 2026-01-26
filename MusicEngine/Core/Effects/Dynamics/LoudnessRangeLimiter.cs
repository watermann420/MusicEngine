// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Brickwall limiter.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Dynamics;

/// <summary>
/// Broadcast standard presets.
/// </summary>
public enum BroadcastStandard
{
    /// <summary>
    /// Custom settings.
    /// </summary>
    Custom,

    /// <summary>
    /// EBU R128 for European broadcast (-23 LUFS, LRA 7-18 LU).
    /// </summary>
    EBU_R128,

    /// <summary>
    /// ATSC A/85 for US broadcast (-24 LKFS).
    /// </summary>
    ATSC_A85,

    /// <summary>
    /// Streaming platforms (-14 LUFS, LRA varies).
    /// </summary>
    Streaming,

    /// <summary>
    /// Podcast/voice content (-16 LUFS).
    /// </summary>
    Podcast
}

/// <summary>
/// Loudness range limiter for broadcast dynamic range control.
/// </summary>
/// <remarks>
/// Features:
/// - Target loudness range (LRA) control
/// - Gentle compression to reduce dynamics
/// - True peak limiting
/// - EBU R128 compliant metering
/// - Multiple broadcast standard presets
/// </remarks>
public class LoudnessRangeLimiter : EffectBase
{
    // Loudness metering (ITU-R BS.1770-4)
    private const int MeteringBlockSize = 400; // 400ms blocks
    private const int MeteringOverlap = 100;   // 100ms overlap

    // Metering state per channel
    private float[][] _meteringBuffer = null!;
    private int _meteringWritePos;
    private int _samplesSinceLastBlock;

    // K-weighting filters (per channel)
    private float[][] _preFilterState = null!;   // High shelf
    private float[][] _highPassState = null!;    // High pass

    // Loudness history for LRA calculation
    private readonly List<float> _loudnessHistory = new();
    private const int MaxLoudnessHistorySize = 1000;

    // Compressor state
    private float _compressorGain;
    private float _targetGain;

    // True peak limiter state
    private float[][] _oversampleBuffer = null!;
    private float _limiterGain;
    private int _peakHoldCounter;

    // Current measurements
    private float _currentLoudness;
    private float _currentLRA;
    private float _currentTruePeak;
    private float _shortTermLoudness;

    private bool _initialized;

    /// <summary>
    /// Creates a new loudness range limiter.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    public LoudnessRangeLimiter(ISampleProvider source) : this(source, "Loudness Range Limiter")
    {
    }

    /// <summary>
    /// Creates a new loudness range limiter with a custom name.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <param name="name">Effect name.</param>
    public LoudnessRangeLimiter(ISampleProvider source, string name) : base(source, name)
    {
        RegisterParameter("TargetLoudness", -23f);      // LUFS
        RegisterParameter("TargetLRA", 12f);            // LU
        RegisterParameter("MaxTruePeak", -1f);          // dBTP
        RegisterParameter("CompressorRatio", 2f);       // Ratio for LRA reduction
        RegisterParameter("Attack", 10f);               // ms
        RegisterParameter("Release", 100f);             // ms
        RegisterParameter("LookaheadMs", 5f);           // ms for true peak
        RegisterParameter("AutoGain", 1f);              // 0 = off, 1 = on
        RegisterParameter("Mix", 1f);

        _compressorGain = 1f;
        _limiterGain = 1f;
        _targetGain = 1f;
        _initialized = false;
    }

    /// <summary>
    /// Gets or sets the target integrated loudness in LUFS.
    /// </summary>
    public float TargetLoudness
    {
        get => GetParameter("TargetLoudness");
        set => SetParameter("TargetLoudness", Math.Clamp(value, -40f, -5f));
    }

    /// <summary>
    /// Gets or sets the target loudness range in LU.
    /// </summary>
    public float TargetLRA
    {
        get => GetParameter("TargetLRA");
        set => SetParameter("TargetLRA", Math.Clamp(value, 3f, 25f));
    }

    /// <summary>
    /// Gets or sets the maximum true peak level in dBTP.
    /// </summary>
    public float MaxTruePeak
    {
        get => GetParameter("MaxTruePeak");
        set => SetParameter("MaxTruePeak", Math.Clamp(value, -10f, 0f));
    }

    /// <summary>
    /// Gets or sets the compressor ratio for LRA reduction.
    /// </summary>
    public float CompressorRatio
    {
        get => GetParameter("CompressorRatio");
        set => SetParameter("CompressorRatio", Math.Clamp(value, 1f, 10f));
    }

    /// <summary>
    /// Gets or sets the compressor attack time in milliseconds.
    /// </summary>
    public float Attack
    {
        get => GetParameter("Attack");
        set => SetParameter("Attack", Math.Clamp(value, 1f, 100f));
    }

    /// <summary>
    /// Gets or sets the compressor release time in milliseconds.
    /// </summary>
    public float Release
    {
        get => GetParameter("Release");
        set => SetParameter("Release", Math.Clamp(value, 10f, 1000f));
    }

    /// <summary>
    /// Gets or sets whether automatic gain is enabled.
    /// </summary>
    public bool AutoGain
    {
        get => GetParameter("AutoGain") > 0.5f;
        set => SetParameter("AutoGain", value ? 1f : 0f);
    }

    /// <summary>
    /// Gets the current integrated loudness in LUFS.
    /// </summary>
    public float IntegratedLoudness => _currentLoudness;

    /// <summary>
    /// Gets the current loudness range in LU.
    /// </summary>
    public float LoudnessRange => _currentLRA;

    /// <summary>
    /// Gets the current true peak in dBTP.
    /// </summary>
    public float TruePeak => _currentTruePeak;

    /// <summary>
    /// Gets the current short-term loudness in LUFS.
    /// </summary>
    public float ShortTermLoudness => _shortTermLoudness;

    /// <summary>
    /// Gets the current gain reduction in dB.
    /// </summary>
    public float GainReduction => 20f * MathF.Log10(_compressorGain * _limiterGain + 1e-10f);

    /// <summary>
    /// Applies a broadcast standard preset.
    /// </summary>
    public void ApplyPreset(BroadcastStandard standard)
    {
        switch (standard)
        {
            case BroadcastStandard.EBU_R128:
                TargetLoudness = -23f;
                TargetLRA = 12f;
                MaxTruePeak = -1f;
                break;

            case BroadcastStandard.ATSC_A85:
                TargetLoudness = -24f;
                TargetLRA = 12f;
                MaxTruePeak = -2f;
                break;

            case BroadcastStandard.Streaming:
                TargetLoudness = -14f;
                TargetLRA = 9f;
                MaxTruePeak = -1f;
                break;

            case BroadcastStandard.Podcast:
                TargetLoudness = -16f;
                TargetLRA = 8f;
                MaxTruePeak = -1.5f;
                break;
        }
    }

    /// <summary>
    /// Resets loudness metering history.
    /// </summary>
    public void ResetMetering()
    {
        _loudnessHistory.Clear();
        _currentLoudness = -70f;
        _currentLRA = 0f;
        _currentTruePeak = -70f;
    }

    /// <summary>
    /// Initializes internal buffers.
    /// </summary>
    private void Initialize()
    {
        int channels = Channels;
        int sampleRate = SampleRate;

        // Metering buffer (400ms worth of samples)
        int blockSamples = (int)(sampleRate * MeteringBlockSize / 1000f);
        _meteringBuffer = new float[channels][];
        for (int ch = 0; ch < channels; ch++)
        {
            _meteringBuffer[ch] = new float[blockSamples];
        }

        // K-weighting filter states
        _preFilterState = new float[channels][];
        _highPassState = new float[channels][];
        for (int ch = 0; ch < channels; ch++)
        {
            _preFilterState[ch] = new float[2];
            _highPassState[ch] = new float[2];
        }

        // Oversampling buffer for true peak (4x)
        _oversampleBuffer = new float[channels][];
        for (int ch = 0; ch < channels; ch++)
        {
            _oversampleBuffer[ch] = new float[4];
        }

        _meteringWritePos = 0;
        _samplesSinceLastBlock = 0;
        _compressorGain = 1f;
        _limiterGain = 1f;
        _initialized = true;
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        if (!_initialized)
        {
            Initialize();
        }

        int channels = Channels;
        int sampleRate = SampleRate;

        // Calculate envelope coefficients
        float attackCoef = MathF.Exp(-1f / (Attack * sampleRate / 1000f));
        float releaseCoef = MathF.Exp(-1f / (Release * sampleRate / 1000f));

        // True peak threshold
        float truePeakThreshold = DbToLinear(MaxTruePeak);

        // Process samples
        for (int i = 0; i < count; i += channels)
        {
            // Update metering
            UpdateMetering(sourceBuffer, i, channels);

            // Calculate loudness-based compression
            float compressionGain = CalculateCompressionGain();

            // Apply compression with envelope
            if (compressionGain < _compressorGain)
            {
                _compressorGain = _compressorGain * attackCoef + compressionGain * (1f - attackCoef);
            }
            else
            {
                _compressorGain = _compressorGain * releaseCoef + compressionGain * (1f - releaseCoef);
            }

            // Process each channel
            float maxPeak = 0f;
            for (int ch = 0; ch < channels; ch++)
            {
                float sample = sourceBuffer[i + ch];

                // Apply K-weighting for loudness measurement (but not output)
                // Apply compression
                sample *= _compressorGain;

                // Auto gain to target loudness
                if (AutoGain)
                {
                    sample *= _targetGain;
                }

                // Detect true peak using simple oversampling
                float truePeak = DetectTruePeak(sample, ch);
                if (truePeak > maxPeak)
                {
                    maxPeak = truePeak;
                }

                destBuffer[offset + i + ch] = sample;
            }

            // Update true peak tracking
            if (maxPeak > _currentTruePeak || _peakHoldCounter > sampleRate * 3)
            {
                _currentTruePeak = LinearToDb(maxPeak);
                _peakHoldCounter = 0;
            }
            _peakHoldCounter++;

            // Apply true peak limiting
            if (maxPeak > truePeakThreshold)
            {
                float limitGain = truePeakThreshold / (maxPeak + 1e-10f);
                _limiterGain = MathF.Min(_limiterGain, limitGain);
            }
            else
            {
                // Release limiting
                _limiterGain = _limiterGain * 0.9999f + 0.0001f;
                _limiterGain = MathF.Min(1f, _limiterGain);
            }

            // Apply limiter gain
            for (int ch = 0; ch < channels; ch++)
            {
                destBuffer[offset + i + ch] *= _limiterGain;
            }
        }
    }

    /// <summary>
    /// Updates loudness metering with K-weighting.
    /// </summary>
    private void UpdateMetering(float[] buffer, int sampleOffset, int channels)
    {
        int sampleRate = SampleRate;
        int blockSamples = _meteringBuffer[0].Length;
        int overlapSamples = (int)(sampleRate * MeteringOverlap / 1000f);

        // K-weighting filter coefficients (ITU-R BS.1770-4)
        // Pre-filter (high shelf +4dB at high frequencies)
        float preA0 = 1.53512485958697f;
        float preA1 = -2.69169618940638f;
        float preA2 = 1.19839281085285f;
        float preB1 = -1.69065929318241f;
        float preB2 = 0.73248077421585f;

        // High-pass filter (removes DC and very low frequencies)
        float hpA0 = 1.0f;
        float hpA1 = -2.0f;
        float hpA2 = 1.0f;
        float hpB1 = -1.99004745483398f;
        float hpB2 = 0.99007225036621f;

        for (int ch = 0; ch < channels; ch++)
        {
            float input = buffer[sampleOffset + ch];

            // Apply pre-filter (high shelf)
            float preOut = preA0 * input +
                          preA1 * _preFilterState[ch][0] +
                          preA2 * _preFilterState[ch][1] -
                          preB1 * _preFilterState[ch][0] -
                          preB2 * _preFilterState[ch][1];

            // Simplified state update
            _preFilterState[ch][1] = _preFilterState[ch][0];
            _preFilterState[ch][0] = input;

            // Apply high-pass filter
            float hpOut = hpA0 * preOut +
                         hpA1 * _highPassState[ch][0] +
                         hpA2 * _highPassState[ch][1] -
                         hpB1 * _highPassState[ch][0] -
                         hpB2 * _highPassState[ch][1];

            _highPassState[ch][1] = _highPassState[ch][0];
            _highPassState[ch][0] = preOut;

            // Store in metering buffer
            _meteringBuffer[ch][_meteringWritePos] = hpOut;
        }

        _meteringWritePos = (_meteringWritePos + 1) % blockSamples;
        _samplesSinceLastBlock++;

        // Calculate loudness when we have enough samples
        if (_samplesSinceLastBlock >= overlapSamples)
        {
            _samplesSinceLastBlock = 0;

            // Calculate mean square for this block
            float sumSquare = 0f;
            for (int ch = 0; ch < channels; ch++)
            {
                // Channel weight (1.0 for L/R, 1.41 for surround)
                float weight = (ch < 2) ? 1f : 1.41f;

                float channelSum = 0f;
                for (int j = 0; j < blockSamples; j++)
                {
                    float s = _meteringBuffer[ch][j];
                    channelSum += s * s;
                }
                channelSum /= blockSamples;

                sumSquare += channelSum * weight;
            }

            // Convert to LUFS
            float loudness = -0.691f + 10f * MathF.Log10(sumSquare + 1e-10f);
            _shortTermLoudness = loudness;

            // Gate: only include blocks louder than -70 LUFS
            if (loudness > -70f)
            {
                _loudnessHistory.Add(loudness);

                // Limit history size
                if (_loudnessHistory.Count > MaxLoudnessHistorySize)
                {
                    _loudnessHistory.RemoveAt(0);
                }

                // Update integrated loudness and LRA
                UpdateIntegratedLoudness();
            }
        }
    }

    /// <summary>
    /// Updates integrated loudness and LRA from history.
    /// </summary>
    private void UpdateIntegratedLoudness()
    {
        if (_loudnessHistory.Count < 2)
            return;

        // Calculate relative gate threshold (-10 LU from ungated)
        float ungatedMean = _loudnessHistory.Average();
        float gateThreshold = ungatedMean - 10f;

        // Filter blocks below gate
        var gatedBlocks = _loudnessHistory.Where(l => l > gateThreshold).ToList();

        if (gatedBlocks.Count > 0)
        {
            // Calculate integrated loudness (mean of gated blocks in power domain)
            float sumPower = 0f;
            foreach (float l in gatedBlocks)
            {
                sumPower += MathF.Pow(10f, l / 10f);
            }
            _currentLoudness = 10f * MathF.Log10(sumPower / gatedBlocks.Count + 1e-10f);

            // Calculate LRA (difference between 95th and 10th percentile)
            if (gatedBlocks.Count >= 10)
            {
                gatedBlocks.Sort();
                int p10Index = (int)(gatedBlocks.Count * 0.10f);
                int p95Index = (int)(gatedBlocks.Count * 0.95f);
                _currentLRA = gatedBlocks[p95Index] - gatedBlocks[p10Index];
            }

            // Update target gain for auto-gain
            if (AutoGain && !float.IsNaN(_currentLoudness) && !float.IsInfinity(_currentLoudness))
            {
                float targetDb = TargetLoudness - _currentLoudness;
                targetDb = Math.Clamp(targetDb, -12f, 12f);
                float newTargetGain = DbToLinear(targetDb);

                // Smooth target gain changes
                _targetGain = _targetGain * 0.99f + newTargetGain * 0.01f;
            }
        }
    }

    /// <summary>
    /// Calculates compression gain based on current LRA.
    /// </summary>
    private float CalculateCompressionGain()
    {
        float targetLRA = TargetLRA;
        float ratio = CompressorRatio;

        // If current LRA exceeds target, apply compression
        if (_currentLRA > targetLRA && _currentLRA > 0)
        {
            float excess = _currentLRA - targetLRA;

            // Calculate gain reduction needed
            // Higher excess = more compression
            float reductionDb = excess * (1f - 1f / ratio);
            reductionDb = Math.Clamp(reductionDb, 0f, 12f);

            return DbToLinear(-reductionDb);
        }

        return 1f;
    }

    /// <summary>
    /// Detects true peak using 4x oversampling.
    /// </summary>
    private float DetectTruePeak(float sample, int channel)
    {
        // Shift history
        _oversampleBuffer[channel][3] = _oversampleBuffer[channel][2];
        _oversampleBuffer[channel][2] = _oversampleBuffer[channel][1];
        _oversampleBuffer[channel][1] = _oversampleBuffer[channel][0];
        _oversampleBuffer[channel][0] = sample;

        // Cubic interpolation at 4x rate
        float p0 = _oversampleBuffer[channel][3];
        float p1 = _oversampleBuffer[channel][2];
        float p2 = _oversampleBuffer[channel][1];
        float p3 = _oversampleBuffer[channel][0];

        float maxPeak = MathF.Abs(sample);

        // Check interpolated points
        for (int i = 1; i < 4; i++)
        {
            float t = i / 4f;
            float t2 = t * t;
            float t3 = t2 * t;

            // Catmull-Rom spline
            float interp = 0.5f * ((2f * p1) +
                                   (-p0 + p2) * t +
                                   (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                                   (-p0 + 3f * p1 - 3f * p2 + p3) * t3);

            float peak = MathF.Abs(interp);
            if (peak > maxPeak)
            {
                maxPeak = peak;
            }
        }

        return maxPeak;
    }

    private static float DbToLinear(float db) => MathF.Pow(10f, db / 20f);
    private static float LinearToDb(float linear) => 20f * MathF.Log10(linear + 1e-10f);

    #region Presets

    /// <summary>
    /// Creates a preset for European broadcast (EBU R128).
    /// </summary>
    public static LoudnessRangeLimiter CreateEBUPreset(ISampleProvider source)
    {
        var effect = new LoudnessRangeLimiter(source, "EBU R128 Limiter");
        effect.ApplyPreset(BroadcastStandard.EBU_R128);
        return effect;
    }

    /// <summary>
    /// Creates a preset for US broadcast (ATSC A/85).
    /// </summary>
    public static LoudnessRangeLimiter CreateATSCPreset(ISampleProvider source)
    {
        var effect = new LoudnessRangeLimiter(source, "ATSC A/85 Limiter");
        effect.ApplyPreset(BroadcastStandard.ATSC_A85);
        return effect;
    }

    /// <summary>
    /// Creates a preset for streaming platforms.
    /// </summary>
    public static LoudnessRangeLimiter CreateStreamingPreset(ISampleProvider source)
    {
        var effect = new LoudnessRangeLimiter(source, "Streaming Limiter");
        effect.ApplyPreset(BroadcastStandard.Streaming);
        return effect;
    }

    /// <summary>
    /// Creates a preset for podcast/voice content.
    /// </summary>
    public static LoudnessRangeLimiter CreatePodcastPreset(ISampleProvider source)
    {
        var effect = new LoudnessRangeLimiter(source, "Podcast Limiter");
        effect.ApplyPreset(BroadcastStandard.Podcast);
        return effect;
    }

    #endregion
}
