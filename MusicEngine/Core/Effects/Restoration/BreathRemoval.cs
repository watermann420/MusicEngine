// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio restoration processor.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Restoration;

/// <summary>
/// Breath removal mode for controlling processing behavior.
/// </summary>
public enum BreathRemovalMode
{
    /// <summary>
    /// Normal mode - actively detecting and reducing breaths.
    /// </summary>
    Normal,

    /// <summary>
    /// Learn mode - learning breath characteristics from input.
    /// </summary>
    Learn,

    /// <summary>
    /// Bypass mode - passes audio through without processing.
    /// </summary>
    Bypass
}

/// <summary>
/// Real-time breath removal effect for vocal recordings.
/// Uses envelope following and spectral analysis to detect and reduce breath sounds.
/// </summary>
/// <remarks>
/// The breath detection algorithm works by:
/// 1. Analyzing the spectral content to identify breath-like frequencies (2kHz-8kHz range)
/// 2. Tracking amplitude envelope to detect sudden increases in the breath frequency range
/// 3. Using duration analysis to distinguish breaths from consonants (typically 100-500ms)
/// 4. Applying smooth gain reduction to reduce breath volume while preserving natural character
///
/// The effect reduces breath volume rather than completely removing breaths,
/// which maintains a more natural sound.
/// </remarks>
public class BreathRemoval : EffectBase
{
    // Analysis buffers
    private readonly int _analysisWindowSize;
    private readonly float[] _analysisBuffer;
    private int _analysisBufferPos;

    // Envelope followers
    private float[] _lowEnvelope;
    private float[] _highEnvelope;
    private float[] _breathEnvelope;

    // Breath detection state
    private float[] _breathGain;
    private float[] _breathDuration;
    private bool[] _breathActive;

    // FFT for spectral analysis (simplified band-pass approach)
    private readonly float _lowCutoff;
    private readonly float _highCutoff;

    // Filter states for band-pass
    private float[][] _lowPassState;
    private float[][] _highPassState;

    // Learning state
    private float _learnedBreathLevel;
    private float _learnedBreathRatio;
    private int _learnFrameCount;
    private const int MinLearnFrames = 20;

    /// <summary>
    /// Creates a new breath removal effect.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    public BreathRemoval(ISampleProvider source) : this(source, "Breath Removal")
    {
    }

    /// <summary>
    /// Creates a new breath removal effect with a custom name.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <param name="name">Effect name.</param>
    public BreathRemoval(ISampleProvider source, string name) : base(source, name)
    {
        int channels = Channels;

        // Register parameters
        RegisterParameter("Sensitivity", 0.5f);           // 0.1-1.0: Detection sensitivity
        RegisterParameter("ReductionAmount", 0.7f);       // 0.0-1.0: How much to reduce breath volume
        RegisterParameter("MinBreathDuration", 0.15f);    // 0.1-0.5s: Minimum duration for breath detection
        RegisterParameter("AttackTime", 5f);              // 1-50ms: Attack time for gain reduction
        RegisterParameter("ReleaseTime", 50f);            // 10-200ms: Release time after breath ends
        RegisterParameter("FrequencyLow", 2000f);         // 1000-4000Hz: Low frequency of breath band
        RegisterParameter("FrequencyHigh", 6000f);        // 4000-12000Hz: High frequency of breath band
        RegisterParameter("Mix", 1f);                     // Dry/wet mix

        // Initialize analysis
        _analysisWindowSize = SampleRate / 20; // 50ms analysis window
        _analysisBuffer = new float[_analysisWindowSize * channels];
        _analysisBufferPos = 0;

        // Initialize per-channel state
        _lowEnvelope = new float[channels];
        _highEnvelope = new float[channels];
        _breathEnvelope = new float[channels];
        _breathGain = new float[channels];
        _breathDuration = new float[channels];
        _breathActive = new bool[channels];

        // Initialize filter states
        _lowPassState = new float[channels][];
        _highPassState = new float[channels][];
        for (int ch = 0; ch < channels; ch++)
        {
            _lowPassState[ch] = new float[2];
            _highPassState[ch] = new float[2];
            _breathGain[ch] = 1.0f;
        }

        // Default frequency range for breath detection
        _lowCutoff = 2000f;
        _highCutoff = 6000f;

        // Initialize learning state
        _learnedBreathLevel = 0.1f;
        _learnedBreathRatio = 2.0f;
        _learnFrameCount = 0;

        Mode = BreathRemovalMode.Normal;
    }

    /// <summary>
    /// Gets or sets the current operating mode.
    /// </summary>
    public BreathRemovalMode Mode { get; set; }

    /// <summary>
    /// Gets or sets the detection sensitivity (0.1 - 1.0).
    /// Higher values detect more breaths but may affect other sounds.
    /// </summary>
    public float Sensitivity
    {
        get => GetParameter("Sensitivity");
        set => SetParameter("Sensitivity", Math.Clamp(value, 0.1f, 1f));
    }

    /// <summary>
    /// Gets or sets the reduction amount (0.0 - 1.0).
    /// Controls how much breath volume is reduced.
    /// 0 = no reduction, 1 = maximum reduction.
    /// </summary>
    public float ReductionAmount
    {
        get => GetParameter("ReductionAmount");
        set => SetParameter("ReductionAmount", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Gets or sets the minimum breath duration in seconds (0.1 - 0.5s).
    /// Shorter sounds are not treated as breaths.
    /// </summary>
    public float MinBreathDuration
    {
        get => GetParameter("MinBreathDuration");
        set => SetParameter("MinBreathDuration", Math.Clamp(value, 0.1f, 0.5f));
    }

    /// <summary>
    /// Gets or sets the attack time in milliseconds (1 - 50ms).
    /// How quickly gain reduction is applied when a breath is detected.
    /// </summary>
    public float AttackTime
    {
        get => GetParameter("AttackTime");
        set => SetParameter("AttackTime", Math.Clamp(value, 1f, 50f));
    }

    /// <summary>
    /// Gets or sets the release time in milliseconds (10 - 200ms).
    /// How quickly gain returns to normal after a breath ends.
    /// </summary>
    public float ReleaseTime
    {
        get => GetParameter("ReleaseTime");
        set => SetParameter("ReleaseTime", Math.Clamp(value, 10f, 200f));
    }

    /// <summary>
    /// Gets or sets the low frequency of the breath detection band in Hz.
    /// </summary>
    public float FrequencyLow
    {
        get => GetParameter("FrequencyLow");
        set => SetParameter("FrequencyLow", Math.Clamp(value, 1000f, 4000f));
    }

    /// <summary>
    /// Gets or sets the high frequency of the breath detection band in Hz.
    /// </summary>
    public float FrequencyHigh
    {
        get => GetParameter("FrequencyHigh");
        set => SetParameter("FrequencyHigh", Math.Clamp(value, 4000f, 12000f));
    }

    /// <summary>
    /// Gets whether breath characteristics have been learned.
    /// </summary>
    public bool IsLearned => _learnFrameCount >= MinLearnFrames;

    /// <summary>
    /// Gets the current breath detection status for each channel.
    /// </summary>
    public bool[] BreathDetected
    {
        get
        {
            bool[] result = new bool[Channels];
            Array.Copy(_breathActive, result, Channels);
            return result;
        }
    }

    /// <summary>
    /// Gets the current gain reduction for each channel (1.0 = no reduction).
    /// </summary>
    public float[] CurrentGainReduction
    {
        get
        {
            float[] result = new float[Channels];
            Array.Copy(_breathGain, result, Channels);
            return result;
        }
    }

    /// <summary>
    /// Starts learning breath characteristics from the input.
    /// </summary>
    public void StartLearning()
    {
        Mode = BreathRemovalMode.Learn;
        _learnFrameCount = 0;
        _learnedBreathLevel = 0f;
        _learnedBreathRatio = 0f;
    }

    /// <summary>
    /// Stops learning and switches to normal mode.
    /// </summary>
    public void StopLearning()
    {
        if (Mode == BreathRemovalMode.Learn)
        {
            // Finalize learned values
            if (_learnFrameCount > 0)
            {
                _learnedBreathLevel /= _learnFrameCount;
                _learnedBreathRatio /= _learnFrameCount;
            }
            else
            {
                // Default values if nothing was learned
                _learnedBreathLevel = 0.1f;
                _learnedBreathRatio = 2.0f;
            }
            Mode = BreathRemovalMode.Normal;
        }
    }

    /// <summary>
    /// Resets learned breath characteristics.
    /// </summary>
    public void ResetLearning()
    {
        _learnFrameCount = 0;
        _learnedBreathLevel = 0.1f;
        _learnedBreathRatio = 2.0f;
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        // Bypass mode
        if (Mode == BreathRemovalMode.Bypass)
        {
            Array.Copy(sourceBuffer, 0, destBuffer, offset, count);
            return;
        }

        int channels = Channels;
        int sampleRate = SampleRate;

        // Get parameters
        float sensitivity = Sensitivity;
        float reductionAmount = ReductionAmount;
        float minBreathDuration = MinBreathDuration;
        float attackTime = AttackTime / 1000f;
        float releaseTime = ReleaseTime / 1000f;
        float freqLow = FrequencyLow;
        float freqHigh = FrequencyHigh;

        // Calculate envelope coefficients
        float attackCoef = MathF.Exp(-1f / (attackTime * sampleRate));
        float releaseCoef = MathF.Exp(-1f / (releaseTime * sampleRate));

        // Calculate filter coefficients (simple 1-pole filters)
        float lowPassCoef = CalculateFilterCoef(freqHigh, sampleRate);
        float highPassCoef = CalculateFilterCoef(freqLow, sampleRate);

        // Samples needed for minimum breath duration
        int minBreathSamples = (int)(minBreathDuration * sampleRate);

        for (int i = 0; i < count; i += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                int srcIndex = i + ch;
                float input = sourceBuffer[srcIndex];
                float output = input;

                // Apply band-pass filter to isolate breath frequencies
                // High-pass to remove low frequencies
                float highPassed = input - _highPassState[ch][0];
                _highPassState[ch][0] += highPassed * highPassCoef;

                // Low-pass to remove high frequencies
                _lowPassState[ch][0] += (highPassed - _lowPassState[ch][0]) * lowPassCoef;
                float breathBand = _lowPassState[ch][0];

                // Calculate envelope of the breath band
                float breathBandAbs = MathF.Abs(breathBand);
                float lowBandAbs = MathF.Abs(input - highPassed); // Low frequency content

                // Envelope followers
                float envCoef = breathBandAbs > _breathEnvelope[ch] ? 0.99f : 0.9999f;
                _breathEnvelope[ch] = breathBandAbs + envCoef * (_breathEnvelope[ch] - breathBandAbs);

                envCoef = lowBandAbs > _lowEnvelope[ch] ? 0.99f : 0.9999f;
                _lowEnvelope[ch] = lowBandAbs + envCoef * (_lowEnvelope[ch] - lowBandAbs);

                // Breath detection logic
                bool isBreath = false;

                if (_lowEnvelope[ch] > 1e-6f)
                {
                    // Calculate ratio of breath band to low frequency content
                    float breathRatio = _breathEnvelope[ch] / (_lowEnvelope[ch] + 1e-6f);

                    // Threshold based on sensitivity and learned values
                    float threshold = (1.5f + _learnedBreathRatio * 0.5f) * (1f - sensitivity * 0.5f);
                    float levelThreshold = _learnedBreathLevel * 0.3f * (1f - sensitivity * 0.5f);

                    // Detect breath: high ratio of breath frequencies to low frequencies
                    // and breath envelope above threshold
                    if (breathRatio > threshold && _breathEnvelope[ch] > levelThreshold)
                    {
                        _breathDuration[ch] += 1f / sampleRate;

                        // Only consider it a breath if duration exceeds minimum
                        if (_breathDuration[ch] >= minBreathDuration)
                        {
                            isBreath = true;
                        }
                    }
                    else
                    {
                        _breathDuration[ch] = 0f;
                    }
                }
                else
                {
                    _breathDuration[ch] = 0f;
                }

                _breathActive[ch] = isBreath;

                // Learn mode: accumulate breath characteristics
                if (Mode == BreathRemovalMode.Learn && isBreath)
                {
                    _learnedBreathLevel += _breathEnvelope[ch];
                    if (_lowEnvelope[ch] > 1e-6f)
                    {
                        _learnedBreathRatio += _breathEnvelope[ch] / _lowEnvelope[ch];
                    }
                    _learnFrameCount++;
                }

                // Apply gain reduction
                float targetGain = isBreath ? (1f - reductionAmount) : 1f;

                // Smooth gain changes
                float gainCoef = targetGain < _breathGain[ch] ? attackCoef : releaseCoef;
                _breathGain[ch] = targetGain + gainCoef * (_breathGain[ch] - targetGain);

                // Apply gain reduction to output
                output = input * _breathGain[ch];

                destBuffer[offset + i + ch] = output;
            }
        }
    }

    /// <summary>
    /// Calculates a simple one-pole filter coefficient for a given cutoff frequency.
    /// </summary>
    private static float CalculateFilterCoef(float cutoffHz, int sampleRate)
    {
        float rc = 1f / (2f * MathF.PI * cutoffHz);
        float dt = 1f / sampleRate;
        return dt / (rc + dt);
    }

    #region Presets

    /// <summary>
    /// Creates a preset for gentle breath reduction.
    /// Suitable for subtle processing that maintains natural character.
    /// </summary>
    public static BreathRemoval CreateGentle(ISampleProvider source)
    {
        var effect = new BreathRemoval(source, "Gentle Breath Removal");
        effect.Sensitivity = 0.3f;
        effect.ReductionAmount = 0.4f;
        effect.MinBreathDuration = 0.2f;
        effect.AttackTime = 10f;
        effect.ReleaseTime = 100f;
        effect.FrequencyLow = 2500f;
        effect.FrequencyHigh = 5500f;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for moderate breath reduction.
    /// Balanced settings for general-purpose use.
    /// </summary>
    public static BreathRemoval CreateModerate(ISampleProvider source)
    {
        var effect = new BreathRemoval(source, "Moderate Breath Removal");
        effect.Sensitivity = 0.5f;
        effect.ReductionAmount = 0.7f;
        effect.MinBreathDuration = 0.15f;
        effect.AttackTime = 5f;
        effect.ReleaseTime = 50f;
        effect.FrequencyLow = 2000f;
        effect.FrequencyHigh = 6000f;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for aggressive breath reduction.
    /// Maximum breath removal for clean vocals.
    /// </summary>
    public static BreathRemoval CreateAggressive(ISampleProvider source)
    {
        var effect = new BreathRemoval(source, "Aggressive Breath Removal");
        effect.Sensitivity = 0.8f;
        effect.ReductionAmount = 0.9f;
        effect.MinBreathDuration = 0.1f;
        effect.AttackTime = 2f;
        effect.ReleaseTime = 30f;
        effect.FrequencyLow = 1500f;
        effect.FrequencyHigh = 7000f;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset optimized for podcast/voiceover.
    /// Tuned for spoken word content.
    /// </summary>
    public static BreathRemoval CreatePodcast(ISampleProvider source)
    {
        var effect = new BreathRemoval(source, "Podcast Breath Removal");
        effect.Sensitivity = 0.6f;
        effect.ReductionAmount = 0.8f;
        effect.MinBreathDuration = 0.12f;
        effect.AttackTime = 3f;
        effect.ReleaseTime = 40f;
        effect.FrequencyLow = 2200f;
        effect.FrequencyHigh = 5800f;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset optimized for singing vocals.
    /// Careful processing to preserve musical character.
    /// </summary>
    public static BreathRemoval CreateSinging(ISampleProvider source)
    {
        var effect = new BreathRemoval(source, "Singing Breath Removal");
        effect.Sensitivity = 0.4f;
        effect.ReductionAmount = 0.5f;
        effect.MinBreathDuration = 0.18f;
        effect.AttackTime = 8f;
        effect.ReleaseTime = 80f;
        effect.FrequencyLow = 2800f;
        effect.FrequencyHigh = 5000f;
        effect.Mix = 1f;
        return effect;
    }

    #endregion
}
