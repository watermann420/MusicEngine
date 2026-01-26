// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Noise gate processor.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Dynamics;

/// <summary>
/// Noise gate effect - attenuates signals below a threshold.
/// Useful for removing noise and controlling dynamics.
/// </summary>
public class GateEffect : EffectBase
{
    private float[] _envelope;     // Envelope follower state per channel
    private float[] _gateState;    // Gate state (open/closed) per channel

    /// <summary>
    /// Creates a new gate effect
    /// </summary>
    /// <param name="source">Audio source to gate</param>
    /// <param name="name">Effect name</param>
    public GateEffect(ISampleProvider source, string name)
        : base(source, name)
    {
        int channels = source.WaveFormat.Channels;
        _envelope = new float[channels];
        _gateState = new float[channels];

        // Initialize parameters
        RegisterParameter("Threshold", -40f);     // -40 dB threshold
        RegisterParameter("Ratio", 10f);          // 10:1 ratio (strong gating)
        RegisterParameter("Attack", 0.001f);      // 1ms attack
        RegisterParameter("Hold", 0.05f);         // 50ms hold time
        RegisterParameter("Release", 0.1f);       // 100ms release
        RegisterParameter("Range", -80f);         // -80 dB attenuation when closed
        RegisterParameter("Mix", 1.0f);           // 100% wet
    }

    /// <summary>
    /// Threshold in dB (-80 to 0)
    /// Signals below this level will be attenuated
    /// </summary>
    public float Threshold
    {
        get => GetParameter("Threshold");
        set => SetParameter("Threshold", Math.Clamp(value, -80f, 0f));
    }

    /// <summary>
    /// Gate ratio (1.0 - 100.0)
    /// 1.0 = no gating, 100.0 = hard gating
    /// </summary>
    public float Ratio
    {
        get => GetParameter("Ratio");
        set => SetParameter("Ratio", Math.Clamp(value, 1f, 100f));
    }

    /// <summary>
    /// Attack time in seconds (0.0001 - 0.1)
    /// How fast the gate opens
    /// </summary>
    public float Attack
    {
        get => GetParameter("Attack");
        set => SetParameter("Attack", Math.Clamp(value, 0.0001f, 0.1f));
    }

    /// <summary>
    /// Hold time in seconds (0.001 - 2.0)
    /// Minimum time gate stays open
    /// </summary>
    public float Hold
    {
        get => GetParameter("Hold");
        set => SetParameter("Hold", Math.Clamp(value, 0.001f, 2f));
    }

    /// <summary>
    /// Release time in seconds (0.001 - 5.0)
    /// How fast the gate closes
    /// </summary>
    public float Release
    {
        get => GetParameter("Release");
        set => SetParameter("Release", Math.Clamp(value, 0.001f, 5f));
    }

    /// <summary>
    /// Range in dB (-80 to 0)
    /// Maximum attenuation when gate is closed
    /// </summary>
    public float Range
    {
        get => GetParameter("Range");
        set => SetParameter("Range", Math.Clamp(value, -80f, 0f));
    }

    /// <summary>
    /// Dry/Wet mix (0.0 - 1.0)
    /// 0 = fully dry (bypassed), 1 = fully wet (gated)
    /// </summary>
    public float DryWet
    {
        get => Mix;
        set => Mix = value;
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;
        int sampleRate = SampleRate;

        float threshold = Threshold;
        float ratio = Ratio;
        float attack = Attack;
        float hold = Hold;
        float release = Release;
        float range = Range;

        // Calculate attack and release coefficients
        float attackCoeff = MathF.Exp(-1f / (attack * sampleRate));
        float releaseCoeff = MathF.Exp(-1f / (release * sampleRate));

        // Convert range from dB to linear
        float rangeLinear = MathF.Pow(10f, range / 20f);

        for (int i = 0; i < count; i += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                float input = sourceBuffer[i + ch];

                // Envelope detection (peak detector)
                float inputAbs = MathF.Abs(input);
                float coeff = inputAbs > _envelope[ch] ? attackCoeff : releaseCoeff;
                _envelope[ch] = inputAbs + coeff * (_envelope[ch] - inputAbs);

                // Convert to dB
                float inputDb = 20f * MathF.Log10(_envelope[ch] + 1e-6f);

                // Calculate gate gain
                float targetGain = 1f;

                if (inputDb < threshold)
                {
                    // Below threshold - calculate attenuation
                    float reduction = (threshold - inputDb) * (1f - 1f / ratio);
                    reduction = Math.Min(reduction, -range); // Clamp to range
                    targetGain = MathF.Pow(10f, -reduction / 20f);
                    targetGain = Math.Max(targetGain, rangeLinear);
                }

                // Smooth gate state transitions
                float gateCoeff = targetGain > _gateState[ch] ? attackCoeff : releaseCoeff;
                _gateState[ch] = targetGain + gateCoeff * (_gateState[ch] - targetGain);

                // Apply gate
                float output = input * _gateState[ch];

                destBuffer[offset + i + ch] = output;
            }
        }
    }
}
