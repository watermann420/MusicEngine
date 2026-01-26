// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Dynamics;

/// <summary>
/// Transient shaper effect - controls the attack and sustain portions of audio independently.
/// Uses envelope followers to detect transients and separate the signal into transient
/// and sustain components, allowing independent gain control of each.
/// </summary>
public class TransientShaperEffect : EffectBase
{
    // Envelope followers per channel
    private float[] _fastEnvelope;      // Fast envelope for transient detection
    private float[] _slowEnvelope;      // Slow envelope for sustain detection
    private float[] _transientGain;     // Smoothed transient gain per channel
    private float[] _sustainGain;       // Smoothed sustain gain per channel

    /// <summary>
    /// Creates a new transient shaper effect
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <param name="name">Effect name</param>
    public TransientShaperEffect(ISampleProvider source, string name)
        : base(source, name)
    {
        int channels = source.WaveFormat.Channels;
        _fastEnvelope = new float[channels];
        _slowEnvelope = new float[channels];
        _transientGain = new float[channels];
        _sustainGain = new float[channels];

        // Initialize smoothed gains to 1.0 (unity)
        for (int i = 0; i < channels; i++)
        {
            _transientGain[i] = 1f;
            _sustainGain[i] = 1f;
        }

        // Initialize parameters
        RegisterParameter("Attack", 0f);           // 0% (neutral)
        RegisterParameter("Sustain", 0f);          // 0% (neutral)
        RegisterParameter("Sensitivity", 50f);     // 50% sensitivity
        RegisterParameter("OutputGain", 0f);       // 0 dB output gain
        RegisterParameter("FastAttack", 0.0001f);  // 0.1ms fast envelope attack
        RegisterParameter("FastRelease", 0.005f);  // 5ms fast envelope release
        RegisterParameter("SlowAttack", 0.02f);    // 20ms slow envelope attack
        RegisterParameter("SlowRelease", 0.2f);    // 200ms slow envelope release
        RegisterParameter("Mix", 1.0f);            // 100% wet
    }

    /// <summary>
    /// Attack control (-100 to +100)
    /// Negative values reduce attack/transients, positive values enhance them
    /// </summary>
    public float Attack
    {
        get => GetParameter("Attack");
        set => SetParameter("Attack", Math.Clamp(value, -100f, 100f));
    }

    /// <summary>
    /// Sustain control (-100 to +100)
    /// Negative values reduce sustain, positive values enhance it
    /// </summary>
    public float Sustain
    {
        get => GetParameter("Sustain");
        set => SetParameter("Sustain", Math.Clamp(value, -100f, 100f));
    }

    /// <summary>
    /// Sensitivity for transient detection (0 to 100)
    /// Higher values make the detector more sensitive to transients
    /// </summary>
    public float Sensitivity
    {
        get => GetParameter("Sensitivity");
        set => SetParameter("Sensitivity", Math.Clamp(value, 0f, 100f));
    }

    /// <summary>
    /// Output gain in dB (-24 to +24)
    /// Compensates for volume changes from transient shaping
    /// </summary>
    public float OutputGain
    {
        get => GetParameter("OutputGain");
        set => SetParameter("OutputGain", Math.Clamp(value, -24f, 24f));
    }

    /// <summary>
    /// Fast envelope attack time in seconds (0.0001 - 0.01)
    /// Controls how quickly the fast envelope responds to transients
    /// </summary>
    public float FastAttack
    {
        get => GetParameter("FastAttack");
        set => SetParameter("FastAttack", Math.Clamp(value, 0.0001f, 0.01f));
    }

    /// <summary>
    /// Fast envelope release time in seconds (0.001 - 0.05)
    /// Controls how quickly the fast envelope decays after a transient
    /// </summary>
    public float FastRelease
    {
        get => GetParameter("FastRelease");
        set => SetParameter("FastRelease", Math.Clamp(value, 0.001f, 0.05f));
    }

    /// <summary>
    /// Slow envelope attack time in seconds (0.005 - 0.1)
    /// Controls how quickly the slow envelope responds
    /// </summary>
    public float SlowAttack
    {
        get => GetParameter("SlowAttack");
        set => SetParameter("SlowAttack", Math.Clamp(value, 0.005f, 0.1f));
    }

    /// <summary>
    /// Slow envelope release time in seconds (0.05 - 1.0)
    /// Controls the slow envelope decay time
    /// </summary>
    public float SlowRelease
    {
        get => GetParameter("SlowRelease");
        set => SetParameter("SlowRelease", Math.Clamp(value, 0.05f, 1f));
    }

    /// <summary>
    /// Dry/Wet mix (0 = dry, 1 = wet)
    /// </summary>
    public float DryWet
    {
        get => Mix;
        set => Mix = value;
    }

    /// <summary>
    /// Resets the transient shaper state
    /// </summary>
    public void Reset()
    {
        Array.Clear(_fastEnvelope);
        Array.Clear(_slowEnvelope);

        for (int i = 0; i < _transientGain.Length; i++)
        {
            _transientGain[i] = 1f;
            _sustainGain[i] = 1f;
        }
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;
        int sampleRate = SampleRate;

        // Get parameters
        float attackAmount = Attack / 100f;     // -1 to +1
        float sustainAmount = Sustain / 100f;   // -1 to +1
        float sensitivity = Sensitivity / 100f; // 0 to 1
        float outputGain = OutputGain;

        float fastAttackTime = FastAttack;
        float fastReleaseTime = FastRelease;
        float slowAttackTime = SlowAttack;
        float slowReleaseTime = SlowRelease;

        // Calculate envelope coefficients
        // Using exponential decay: coeff = exp(-1 / (time * sampleRate))
        float fastAttackCoeff = MathF.Exp(-1f / (fastAttackTime * sampleRate));
        float fastReleaseCoeff = MathF.Exp(-1f / (fastReleaseTime * sampleRate));
        float slowAttackCoeff = MathF.Exp(-1f / (slowAttackTime * sampleRate));
        float slowReleaseCoeff = MathF.Exp(-1f / (slowReleaseTime * sampleRate));

        // Convert output gain from dB to linear
        float outputGainLinear = MathF.Pow(10f, outputGain / 20f);

        // Sensitivity affects the threshold for transient detection
        // Lower sensitivity = higher threshold = fewer detected transients
        float sensitivityScale = 0.1f + (sensitivity * 0.9f); // 0.1 to 1.0

        // Calculate gain multipliers from attack/sustain amounts
        // At 0%: multiplier = 1.0 (unity)
        // At +100%: multiplier = 2.0 (6dB boost)
        // At -100%: multiplier = 0.0 (full reduction)
        float transientBoost = 1f + attackAmount;   // 0 to 2
        float sustainBoost = 1f + sustainAmount;    // 0 to 2

        // Smooth gain changes to avoid clicks
        float gainSmoothCoeff = MathF.Exp(-1f / (0.005f * sampleRate)); // 5ms smoothing

        for (int i = 0; i < count; i += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                float input = sourceBuffer[i + ch];
                float inputAbs = MathF.Abs(input);

                // Update fast envelope (transient detection)
                float fastCoeff = inputAbs > _fastEnvelope[ch] ? fastAttackCoeff : fastReleaseCoeff;
                _fastEnvelope[ch] = inputAbs + fastCoeff * (_fastEnvelope[ch] - inputAbs);

                // Update slow envelope (sustain detection)
                float slowCoeff = inputAbs > _slowEnvelope[ch] ? slowAttackCoeff : slowReleaseCoeff;
                _slowEnvelope[ch] = inputAbs + slowCoeff * (_slowEnvelope[ch] - inputAbs);

                // Calculate transient component
                // Transient = difference between fast and slow envelopes
                // When there's a transient, fast envelope rises quickly while slow lags behind
                float envelopeDiff = _fastEnvelope[ch] - _slowEnvelope[ch];

                // Normalize the difference relative to the signal level
                float signalLevel = MathF.Max(_slowEnvelope[ch], 1e-6f);
                float normalizedDiff = envelopeDiff / signalLevel;

                // Apply sensitivity - clamp and scale the transient detection
                float transientAmount = MathF.Max(0f, normalizedDiff * sensitivityScale);
                transientAmount = MathF.Min(transientAmount, 1f);

                // Sustain is the inverse of transient
                float sustainAmountNorm = 1f - transientAmount;

                // Calculate target gains for transient and sustain components
                float targetTransientGain = transientBoost;
                float targetSustainGain = sustainBoost;

                // Smooth the gains to avoid clicks
                _transientGain[ch] = targetTransientGain + gainSmoothCoeff * (_transientGain[ch] - targetTransientGain);
                _sustainGain[ch] = targetSustainGain + gainSmoothCoeff * (_sustainGain[ch] - targetSustainGain);

                // Apply differential gain based on transient/sustain ratio
                // Output = input * (transientAmount * transientGain + sustainAmount * sustainGain)
                float combinedGain = (transientAmount * _transientGain[ch]) + (sustainAmountNorm * _sustainGain[ch]);

                // Apply output gain
                float output = input * combinedGain * outputGainLinear;

                destBuffer[offset + i + ch] = output;
            }
        }
    }
}
