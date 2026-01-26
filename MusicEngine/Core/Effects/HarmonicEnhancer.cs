// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio effect processor.

using NAudio.Wave;

namespace MusicEngine.Core.Effects;

/// <summary>
/// Harmonic generation mode
/// </summary>
public enum HarmonicMode
{
    /// <summary>Adds even harmonics (2nd, 4th, 6th) - warm, tube-like</summary>
    Even,
    /// <summary>Adds odd harmonics (3rd, 5th, 7th) - bright, transistor-like</summary>
    Odd,
    /// <summary>Both even and odd harmonics</summary>
    Both,
    /// <summary>Tape saturation simulation</summary>
    Tape,
    /// <summary>Tube saturation simulation</summary>
    Tube
}

/// <summary>
/// Harmonic Enhancer effect that adds musical harmonics for warmth and presence.
/// Can simulate tube or tape saturation characteristics.
/// </summary>
public class HarmonicEnhancer : EffectBase
{
    // Highpass filter state for separating bass
    private float _hpStateL, _hpStateR;
    private float _lpStateL, _lpStateR;

    // Previous sample for odd harmonic generation
    private float _prevL, _prevR;

    /// <summary>Harmonic generation mode</summary>
    public HarmonicMode Mode { get; set; } = HarmonicMode.Both;

    /// <summary>Amount of even harmonics (0-1)</summary>
    public float EvenAmount { get; set; } = 0.3f;

    /// <summary>Amount of odd harmonics (0-1)</summary>
    public float OddAmount { get; set; } = 0.2f;

    /// <summary>Drive/input gain (1-10)</summary>
    public float Drive { get; set; } = 2.0f;

    /// <summary>Output gain (0-2)</summary>
    public float OutputGain { get; set; } = 1.0f;

    /// <summary>Crossover frequency for bass protection (20-500 Hz)</summary>
    public float CrossoverFrequency { get; set; } = 100f;

    /// <summary>Presence boost frequency (1000-8000 Hz)</summary>
    public float PresenceFrequency { get; set; } = 3000f;

    /// <summary>Presence amount (0-1)</summary>
    public float PresenceAmount { get; set; } = 0f;

    /// <summary>Air (high frequency harmonics, 0-1)</summary>
    public float Air { get; set; } = 0f;

    public HarmonicEnhancer(ISampleProvider source) : base(source, "HarmonicEnhancer")
    {
        RegisterParameter("EvenAmount", 0.3f);
        RegisterParameter("OddAmount", 0.2f);
        RegisterParameter("Drive", 2.0f);
        RegisterParameter("OutputGain", 1.0f);
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;
        int sampleRate = SampleRate;

        // Calculate filter coefficients
        float hpCoeff = MathF.Exp(-2f * MathF.PI * CrossoverFrequency / sampleRate);
        float lpCoeff = MathF.Exp(-2f * MathF.PI * 10000f / sampleRate); // For air

        for (int n = 0; n < count; n += channels)
        {
            float inputL = sourceBuffer[n];
            float inputR = channels > 1 ? sourceBuffer[n + 1] : inputL;

            // Highpass to separate bass (protect from distortion)
            _hpStateL = inputL + hpCoeff * (_hpStateL - inputL);
            _hpStateR = inputR + hpCoeff * (_hpStateR - inputR);

            float bassL = _hpStateL;
            float bassR = _hpStateR;
            float highL = inputL - bassL;
            float highR = inputR - bassR;

            // Apply drive to high frequencies only
            float drivenL = highL * Drive;
            float drivenR = highR * Drive;

            // Generate harmonics
            float harmonicsL = 0f;
            float harmonicsR = 0f;

            switch (Mode)
            {
                case HarmonicMode.Even:
                    harmonicsL = GenerateEvenHarmonics(drivenL) * EvenAmount;
                    harmonicsR = GenerateEvenHarmonics(drivenR) * EvenAmount;
                    break;

                case HarmonicMode.Odd:
                    harmonicsL = GenerateOddHarmonics(drivenL, ref _prevL) * OddAmount;
                    harmonicsR = GenerateOddHarmonics(drivenR, ref _prevR) * OddAmount;
                    break;

                case HarmonicMode.Both:
                    harmonicsL = GenerateEvenHarmonics(drivenL) * EvenAmount +
                                GenerateOddHarmonics(drivenL, ref _prevL) * OddAmount;
                    harmonicsR = GenerateEvenHarmonics(drivenR) * EvenAmount +
                                GenerateOddHarmonics(drivenR, ref _prevR) * OddAmount;
                    break;

                case HarmonicMode.Tape:
                    harmonicsL = GenerateTapeSaturation(drivenL) * (EvenAmount + OddAmount);
                    harmonicsR = GenerateTapeSaturation(drivenR) * (EvenAmount + OddAmount);
                    break;

                case HarmonicMode.Tube:
                    harmonicsL = GenerateTubeSaturation(drivenL) * (EvenAmount + OddAmount);
                    harmonicsR = GenerateTubeSaturation(drivenR) * (EvenAmount + OddAmount);
                    break;
            }

            // Add air (high frequency enhancement)
            if (Air > 0)
            {
                // Extract high frequencies
                _lpStateL = highL + lpCoeff * (_lpStateL - highL);
                _lpStateR = highR + lpCoeff * (_lpStateR - highR);

                float airL = highL - _lpStateL;
                float airR = highR - _lpStateR;

                // Soft clip the air
                airL = MathF.Tanh(airL * 2f) * Air;
                airR = MathF.Tanh(airR * 2f) * Air;

                harmonicsL += airL;
                harmonicsR += airR;
            }

            // Mix original with harmonics and recombine with bass
            float outL = bassL + highL + harmonicsL;
            float outR = bassR + highR + harmonicsR;

            // Apply output gain
            outL *= OutputGain;
            outR *= OutputGain;

            // Soft limiting
            outL = SoftLimit(outL);
            outR = SoftLimit(outR);

            destBuffer[offset + n] = outL;
            if (channels > 1)
                destBuffer[offset + n + 1] = outR;
        }
    }

    /// <summary>
    /// Generate even harmonics (2nd, 4th, 6th) using asymmetric waveshaping
    /// </summary>
    private static float GenerateEvenHarmonics(float x)
    {
        // Asymmetric transfer function generates even harmonics
        // f(x) = x + a*x^2 generates 2nd harmonic
        float x2 = x * x;
        float x4 = x2 * x2;

        // Weight towards lower even harmonics (warmer)
        return x2 * 0.5f + x4 * 0.1f;
    }

    /// <summary>
    /// Generate odd harmonics (3rd, 5th, 7th) using symmetric waveshaping
    /// </summary>
    private static float GenerateOddHarmonics(float x, ref float prev)
    {
        // Symmetric transfer function generates odd harmonics
        // Soft clipping generates primarily odd harmonics
        float clipped = MathF.Tanh(x * 1.5f);

        // Cubic function for 3rd harmonic emphasis
        float x3 = x * x * x;

        // Differentiate for brighter harmonics
        float diff = x - prev;
        prev = x;

        return clipped * 0.3f + x3 * 0.1f + diff * 0.05f;
    }

    /// <summary>
    /// Tape saturation characteristics (soft, warm compression)
    /// </summary>
    private static float GenerateTapeSaturation(float x)
    {
        // Tape has soft knee compression with slight even harmonic emphasis
        // and high frequency roll-off under saturation

        // Soft S-curve with asymmetry
        float sign = MathF.Sign(x);
        float abs = MathF.Abs(x);

        // Tape-like soft saturation
        float saturated;
        if (abs < 0.5f)
        {
            saturated = abs;
        }
        else if (abs < 1.5f)
        {
            // Soft knee region
            float over = abs - 0.5f;
            saturated = 0.5f + over * (1f - over * 0.3f);
        }
        else
        {
            // Heavy saturation
            saturated = 0.85f + MathF.Tanh((abs - 1.5f) * 0.5f) * 0.15f;
        }

        return sign * saturated - x; // Return only the added harmonics
    }

    /// <summary>
    /// Tube saturation characteristics (warm with even harmonics)
    /// </summary>
    private static float GenerateTubeSaturation(float x)
    {
        // Tubes generate strong even harmonics with asymmetric clipping
        // Grid current limiting on positive swings

        // Asymmetric saturation (tubes clip differently on each half)
        float saturated;
        if (x >= 0)
        {
            // Positive half: softer clipping (plate saturation)
            saturated = MathF.Tanh(x * 1.2f);
        }
        else
        {
            // Negative half: harder clipping (grid current)
            saturated = -MathF.Tanh(-x * 1.5f) * 0.9f;
        }

        // Add some even harmonic content via squaring
        float even = x * MathF.Abs(x) * 0.2f;

        return saturated + even - x;
    }

    /// <summary>
    /// Soft limiter to prevent clipping
    /// </summary>
    private static float SoftLimit(float x)
    {
        if (MathF.Abs(x) < 0.9f)
            return x;

        return MathF.Sign(x) * (0.9f + MathF.Tanh((MathF.Abs(x) - 0.9f) * 5f) * 0.1f);
    }

    protected override void OnParameterChanged(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "evenamount": EvenAmount = Math.Clamp(value, 0f, 1f); break;
            case "oddamount": OddAmount = Math.Clamp(value, 0f, 1f); break;
            case "drive": Drive = Math.Clamp(value, 1f, 10f); break;
            case "outputgain": OutputGain = Math.Clamp(value, 0f, 2f); break;
            case "crossoverfrequency": CrossoverFrequency = Math.Clamp(value, 20f, 500f); break;
            case "presenceamount": PresenceAmount = Math.Clamp(value, 0f, 1f); break;
            case "air": Air = Math.Clamp(value, 0f, 1f); break;
        }
    }

    #region Presets

    /// <summary>Warm preset - subtle tube-like warmth</summary>
    public static HarmonicEnhancer CreateWarm(ISampleProvider source)
    {
        return new HarmonicEnhancer(source)
        {
            Mode = HarmonicMode.Even,
            EvenAmount = 0.25f,
            OddAmount = 0.05f,
            Drive = 1.5f,
            CrossoverFrequency = 150f,
            Air = 0f
        };
    }

    /// <summary>Presence preset - adds clarity and presence</summary>
    public static HarmonicEnhancer CreatePresence(ISampleProvider source)
    {
        return new HarmonicEnhancer(source)
        {
            Mode = HarmonicMode.Odd,
            EvenAmount = 0.1f,
            OddAmount = 0.3f,
            Drive = 2.0f,
            CrossoverFrequency = 200f,
            PresenceAmount = 0.4f,
            Air = 0.2f
        };
    }

    /// <summary>Tape preset - analog tape saturation</summary>
    public static HarmonicEnhancer CreateTape(ISampleProvider source)
    {
        return new HarmonicEnhancer(source)
        {
            Mode = HarmonicMode.Tape,
            EvenAmount = 0.4f,
            OddAmount = 0.2f,
            Drive = 2.5f,
            CrossoverFrequency = 80f,
            Air = 0f
        };
    }

    /// <summary>Tube preset - tube amplifier saturation</summary>
    public static HarmonicEnhancer CreateTube(ISampleProvider source)
    {
        return new HarmonicEnhancer(source)
        {
            Mode = HarmonicMode.Tube,
            EvenAmount = 0.5f,
            OddAmount = 0.15f,
            Drive = 3.0f,
            CrossoverFrequency = 100f,
            Air = 0.1f
        };
    }

    /// <summary>Exciter preset - bright and airy enhancement</summary>
    public static HarmonicEnhancer CreateExciter(ISampleProvider source)
    {
        return new HarmonicEnhancer(source)
        {
            Mode = HarmonicMode.Both,
            EvenAmount = 0.2f,
            OddAmount = 0.4f,
            Drive = 2.0f,
            CrossoverFrequency = 300f,
            PresenceAmount = 0.5f,
            Air = 0.4f
        };
    }

    /// <summary>Bass enhancer - warmth for bass frequencies</summary>
    public static HarmonicEnhancer CreateBassEnhancer(ISampleProvider source)
    {
        return new HarmonicEnhancer(source)
        {
            Mode = HarmonicMode.Even,
            EvenAmount = 0.5f,
            OddAmount = 0.1f,
            Drive = 2.5f,
            CrossoverFrequency = 40f,
            Air = 0f
        };
    }

    #endregion
}
