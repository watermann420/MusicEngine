// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio effect processor.

using NAudio.Wave;

namespace MusicEngine.Core.Effects;

/// <summary>
/// Tape saturation emulation with warmth, compression, and optional hiss
/// </summary>
public class TapeSaturation : EffectBase
{
    private readonly Random _random = new();

    // Filter states
    private float _lpStateL, _lpStateR;     // Lowpass for tape roll-off
    private float _hpStateL, _hpStateR;     // Highpass for DC blocking
    private float _warmStateL, _warmStateR; // Warmth filter

    // Flutter LFO
    private double _flutterPhase = 0;
    private const float FlutterRate = 3f; // Hz

    // Compression envelope
    private float _envL, _envR;

    /// <summary>
    /// Input drive/saturation amount (0-1)
    /// </summary>
    public float Drive
    {
        get => GetParameter("Drive");
        set => SetParameter("Drive", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Low frequency boost (0-1)
    /// </summary>
    public float Warmth
    {
        get => GetParameter("Warmth");
        set => SetParameter("Warmth", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Saturation curve softness (0-1)
    /// </summary>
    public float Softness
    {
        get => GetParameter("Softness");
        set => SetParameter("Softness", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Output gain (0-2)
    /// </summary>
    public float OutputLevel
    {
        get => GetParameter("OutputLevel");
        set => SetParameter("OutputLevel", Math.Clamp(value, 0f, 2f));
    }

    /// <summary>
    /// Tape speed affecting high frequency response (0.5-2)
    /// </summary>
    public float TapeSpeed
    {
        get => GetParameter("TapeSpeed");
        set => SetParameter("TapeSpeed", Math.Clamp(value, 0.5f, 2f));
    }

    /// <summary>
    /// Tape bias affecting distortion character (-1 to 1)
    /// </summary>
    public float Bias
    {
        get => GetParameter("Bias");
        set => SetParameter("Bias", Math.Clamp(value, -1f, 1f));
    }

    /// <summary>
    /// Tape hiss amount (0-1)
    /// </summary>
    public float Hiss
    {
        get => GetParameter("Hiss");
        set => SetParameter("Hiss", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Wow and flutter pitch modulation (0-1)
    /// </summary>
    public float Flutter
    {
        get => GetParameter("Flutter");
        set => SetParameter("Flutter", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Natural tape compression (0-1)
    /// </summary>
    public float Compression
    {
        get => GetParameter("Compression");
        set => SetParameter("Compression", Math.Clamp(value, 0f, 1f));
    }

    public TapeSaturation(ISampleProvider source) : base(source, "Tape Saturation")
    {
        RegisterParameter("Drive", 0.5f);
        RegisterParameter("Warmth", 0.5f);
        RegisterParameter("Softness", 0.7f);
        RegisterParameter("OutputLevel", 1f);
        RegisterParameter("TapeSpeed", 1f);
        RegisterParameter("Bias", 0f);
        RegisterParameter("Hiss", 0f);
        RegisterParameter("Flutter", 0f);
        RegisterParameter("Compression", 0.3f);
        RegisterParameter("Mix", 1f);
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;
        int sampleRate = SampleRate;

        float drive = Drive;
        float warmth = Warmth;
        float softness = Softness;
        float outputLevel = OutputLevel;
        float tapeSpeed = TapeSpeed;
        float bias = Bias;
        float hiss = Hiss;
        float flutter = Flutter;
        float compression = Compression;

        float lpCoeff = (float)Math.Exp(-2 * Math.PI * (8000 * tapeSpeed) / sampleRate);
        float hpCoeff = (float)Math.Exp(-2 * Math.PI * 20 / sampleRate);
        float warmCoeff = (float)Math.Exp(-2 * Math.PI * 200 / sampleRate);

        float flutterInc = (float)(2 * Math.PI * FlutterRate / sampleRate);
        float compressionAttack = (float)Math.Exp(-1.0 / (0.01 * sampleRate));
        float compressionRelease = (float)Math.Exp(-1.0 / (0.1 * sampleRate));

        for (int n = 0; n < count; n += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                float input = sourceBuffer[n + ch];

                // Apply drive
                float driven = input * (1 + drive * 3);

                // Add bias
                driven += bias * 0.1f;

                // Tape compression (soft limiting before saturation)
                if (compression > 0)
                {
                    float level = Math.Abs(driven);
                    ref float env = ref (ch == 0 ? ref _envL : ref _envR);

                    // Envelope follower
                    env = level > env
                        ? level + compressionAttack * (env - level)
                        : level + compressionRelease * (env - level);

                    // Soft compression
                    float threshold = 1f / (1f + env * compression * 2);
                    driven *= threshold;
                }

                // Tape saturation curve
                float saturated = TapeSaturate(driven, softness);

                // Remove bias DC offset
                saturated -= TapeSaturate(bias * 0.1f, softness);

                // Warmth (low frequency boost)
                ref float warmState = ref (ch == 0 ? ref _warmStateL : ref _warmStateR);
                warmState = warmState * warmCoeff + saturated * (1 - warmCoeff);
                saturated += warmState * warmth * 0.5f;

                // Tape high frequency roll-off
                ref float lpState = ref (ch == 0 ? ref _lpStateL : ref _lpStateR);
                lpState = lpState * lpCoeff + saturated * (1 - lpCoeff);
                float output = lpState;

                // DC blocking highpass
                ref float hpState = ref (ch == 0 ? ref _hpStateL : ref _hpStateR);
                float hp = output - hpState;
                hpState = hpState * hpCoeff + output * (1 - hpCoeff);
                output = hp;

                // Add flutter (pitch modulation) - only compute once per sample frame
                if (flutter > 0 && ch == 0)
                {
                    float flutterMod = (float)(Math.Sin(_flutterPhase) * flutter * 0.002);
                    output *= 1 + flutterMod;
                    _flutterPhase += flutterInc;
                    if (_flutterPhase > 2 * Math.PI) _flutterPhase -= 2 * Math.PI;
                }
                else if (flutter > 0)
                {
                    float flutterMod = (float)(Math.Sin(_flutterPhase) * flutter * 0.002);
                    output *= 1 + flutterMod;
                }

                // Add tape hiss
                if (hiss > 0)
                {
                    float hissAmount = (float)(_random.NextDouble() * 2 - 1) * hiss * 0.01f;
                    output += hissAmount;
                }

                // Output level
                destBuffer[offset + n + ch] = output * outputLevel;
            }
        }
    }

    private float TapeSaturate(float x, float softness)
    {
        // Tape-style saturation using soft clipping
        float soft = 0.5f + softness * 0.5f;

        if (Math.Abs(x) < soft)
        {
            return x;
        }
        else if (x > 0)
        {
            return soft + (1 - soft) * (float)Math.Tanh((x - soft) / (1 - soft));
        }
        else
        {
            return -soft - (1 - soft) * (float)Math.Tanh((-x - soft) / (1 - soft));
        }
    }

    /// <summary>
    /// Create preset configurations
    /// </summary>
    public static TapeSaturation CreatePreset(ISampleProvider source, string presetName)
    {
        var tape = new TapeSaturation(source);

        switch (presetName.ToLowerInvariant())
        {
            case "subtle":
                tape.Drive = 0.3f;
                tape.Warmth = 0.3f;
                tape.Softness = 0.8f;
                tape.TapeSpeed = 1f;
                tape.Compression = 0.2f;
                break;

            case "warm":
                tape.Drive = 0.5f;
                tape.Warmth = 0.7f;
                tape.Softness = 0.7f;
                tape.TapeSpeed = 0.8f;
                tape.Compression = 0.3f;
                break;

            case "hot":
                tape.Drive = 0.8f;
                tape.Warmth = 0.5f;
                tape.Softness = 0.5f;
                tape.TapeSpeed = 1f;
                tape.Compression = 0.4f;
                tape.Bias = 0.2f;
                break;

            case "lofi":
                tape.Drive = 0.6f;
                tape.Warmth = 0.8f;
                tape.Softness = 0.4f;
                tape.TapeSpeed = 0.6f;
                tape.Compression = 0.5f;
                tape.Hiss = 0.4f;
                tape.Flutter = 0.3f;
                break;

            case "vintage":
                tape.Drive = 0.4f;
                tape.Warmth = 0.6f;
                tape.Softness = 0.6f;
                tape.TapeSpeed = 0.7f;
                tape.Compression = 0.35f;
                tape.Hiss = 0.15f;
                tape.Flutter = 0.1f;
                tape.Bias = 0.1f;
                break;

            case "mastering":
                tape.Drive = 0.25f;
                tape.Warmth = 0.4f;
                tape.Softness = 0.9f;
                tape.TapeSpeed = 1.2f;
                tape.Compression = 0.15f;
                break;
        }

        return tape;
    }
}
