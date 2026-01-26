// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Distortion/overdrive effect.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Distortion;

/// <summary>
/// Distortion type
/// </summary>
public enum DistortionType
{
    /// <summary>Hard clipping - harsh, aggressive distortion</summary>
    HardClip,
    /// <summary>Soft clipping - smooth, tube-like saturation</summary>
    SoftClip,
    /// <summary>Overdrive - asymmetric, warm distortion</summary>
    Overdrive,
    /// <summary>Fuzz - heavy, squared-off distortion</summary>
    Fuzz,
    /// <summary>Waveshaping - sine-based folding distortion</summary>
    Waveshaper
}

/// <summary>
/// Distortion effect with multiple distortion types and tone control.
/// </summary>
public class DistortionEffect : EffectBase
{
    private float[] _dcBlocker; // DC blocker state per channel

    /// <summary>
    /// Creates a new distortion effect
    /// </summary>
    /// <param name="source">Audio source to distort</param>
    /// <param name="name">Effect name</param>
    /// <param name="distortionType">Type of distortion</param>
    public DistortionEffect(ISampleProvider source, string name, DistortionType distortionType = DistortionType.SoftClip)
        : base(source, name)
    {
        int channels = source.WaveFormat.Channels;
        _dcBlocker = new float[channels];

        // Initialize parameters
        RegisterParameter("Drive", 5f);           // 5x gain
        RegisterParameter("Tone", 0.5f);          // 50% tone (lowpass)
        RegisterParameter("OutputGain", 0.5f);    // 50% output level
        RegisterParameter("Type", (float)distortionType);
        RegisterParameter("Mix", 1.0f);        // 100% wet
    }

    /// <summary>
    /// Drive amount (1.0 - 100.0)
    /// How much gain is applied before distortion
    /// </summary>
    public float Drive
    {
        get => GetParameter("Drive");
        set => SetParameter("Drive", Math.Clamp(value, 1f, 100f));
    }

    /// <summary>
    /// Tone control (0.0 - 1.0)
    /// 0.0 = dark (more lowpass), 1.0 = bright (less filtering)
    /// </summary>
    public float Tone
    {
        get => GetParameter("Tone");
        set => SetParameter("Tone", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Output gain (0.0 - 1.0)
    /// Compensates for volume increase from distortion
    /// </summary>
    public float OutputGain
    {
        get => GetParameter("OutputGain");
        set => SetParameter("OutputGain", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Type of distortion
    /// </summary>
    public DistortionType Type
    {
        get => (DistortionType)GetParameter("Type");
        set => SetParameter("Type", (float)value);
    }

    /// <summary>
    /// Dry/Wet mix (0.0 - 1.0)
    /// 0.0 = dry (no effect), 1.0 = wet (full effect)
    /// </summary>
    public float DryWet
    {
        get => Mix;
        set => Mix = value;
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;

        float drive = Drive;
        float tone = Tone;
        float outputGain = OutputGain;
        DistortionType distType = Type;

        // Calculate tone filter coefficient (one-pole lowpass)
        float toneCoeff = 1f - tone;

        for (int i = 0; i < count; i += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                float input = sourceBuffer[i + ch];

                // Apply drive (pre-gain)
                float driven = input * drive;

                // Apply distortion algorithm
                float distorted = distType switch
                {
                    DistortionType.HardClip => HardClip(driven),
                    DistortionType.SoftClip => SoftClip(driven),
                    DistortionType.Overdrive => Overdrive(driven),
                    DistortionType.Fuzz => Fuzz(driven),
                    DistortionType.Waveshaper => Waveshaper(driven),
                    _ => driven
                };

                // Apply tone control (lowpass filter)
                distorted = _dcBlocker[ch] + toneCoeff * (distorted - _dcBlocker[ch]);
                _dcBlocker[ch] = distorted;

                // Apply output gain
                float output = distorted * outputGain;

                destBuffer[offset + i + ch] = output;
            }
        }
    }

    /// <summary>
    /// Hard clipping - simple threshold clipping
    /// </summary>
    private float HardClip(float input)
    {
        return Math.Clamp(input, -1f, 1f);
    }

    /// <summary>
    /// Soft clipping - smooth saturation using tanh
    /// </summary>
    private float SoftClip(float input)
    {
        return MathF.Tanh(input);
    }

    /// <summary>
    /// Overdrive - asymmetric soft clipping
    /// </summary>
    private float Overdrive(float input)
    {
        if (input > 0f)
        {
            return input / (1f + input);
        }
        else
        {
            return input / (1f - input * 0.7f);
        }
    }

    /// <summary>
    /// Fuzz - aggressive squaring with soft knee
    /// </summary>
    private float Fuzz(float input)
    {
        float abs = MathF.Abs(input);
        if (abs < 0.3f)
        {
            return input;
        }
        else if (abs < 1f)
        {
            float sign = input > 0f ? 1f : -1f;
            return sign * (0.3f + (abs - 0.3f) * 0.5f);
        }
        else
        {
            return input > 0f ? 0.65f : -0.65f;
        }
    }

    /// <summary>
    /// Waveshaper - sine-based folding distortion
    /// </summary>
    private float Waveshaper(float input)
    {
        // Fold the waveform using sine
        return MathF.Sin(input * MathF.PI * 0.5f);
    }
}
