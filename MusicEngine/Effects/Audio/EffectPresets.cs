// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Audio effect presets and factories.

namespace MusicEngine.Effects.Audio;

/// <summary>
/// Preset effect factory for quick use in scripts.
/// </summary>
public static class Effect
{
    public static SimpleReverbEffect Reverb => new();
    public static SimpleDelayEffect Delay => new();
    public static TremoloEffect Tremolo => new();
    public static BitCrusherEffect BitCrush => new();
    public static NoiseEffect Noise => new();
    public static GainEffect Gain => new();
    public static DriveEffect Drive => new();
    public static SimpleFilterEffect Filter => new();
    public static SimpleReverbEffect ReverbPreset(string name, Action<SimpleReverbEffect>? configure = null)
        => EffectPresets.ReverbPreset(name, configure);
    public static SimpleDelayEffect DelayPreset(string name, Action<SimpleDelayEffect>? configure = null)
        => EffectPresets.DelayPreset(name, configure);
    public static TremoloEffect TremoloPreset(string name, Action<TremoloEffect>? configure = null)
        => EffectPresets.TremoloPreset(name, configure);
    public static BitCrusherEffect BitCrushPreset(string name, Action<BitCrusherEffect>? configure = null)
        => EffectPresets.BitCrushPreset(name, configure);
    public static NoiseEffect NoisePreset(string name, Action<NoiseEffect>? configure = null)
        => EffectPresets.NoisePreset(name, configure);
    public static DriveEffect DrivePreset(string name, Action<DriveEffect>? configure = null)
        => EffectPresets.DrivePreset(name, configure);
    public static SimpleFilterEffect FilterPreset(string name, Action<SimpleFilterEffect>? configure = null)
        => EffectPresets.FilterPreset(name, configure);
    public static EffectRack Create() => new();
}

internal static class EffectPresets
{
    public static SimpleReverbEffect ReverbPreset(string name, Action<SimpleReverbEffect>? configure)
    {
        var preset = name?.Trim().ToLowerInvariant() ?? "room";
        var effect = preset switch
        {
            "room" => new SimpleReverbEffect { Mix = 0.15f, Size = 0.3f, Damping = 0.4f },
            "hall" => new SimpleReverbEffect { Mix = 0.25f, Size = 0.7f, Damping = 0.35f },
            "large" => new SimpleReverbEffect { Mix = 0.35f, Size = 0.85f, Damping = 0.3f },
            "plate" => new SimpleReverbEffect { Mix = 0.2f, Size = 0.6f, Damping = 0.2f },
            _ => new SimpleReverbEffect()
        };
        configure?.Invoke(effect);
        return effect;
    }

    public static SimpleDelayEffect DelayPreset(string name, Action<SimpleDelayEffect>? configure)
    {
        var preset = name?.Trim().ToLowerInvariant() ?? "slap";
        var effect = preset switch
        {
            "slap" => new SimpleDelayEffect { Mix = 0.15f, TimeMs = 80f, Feedback = 0.2f },
            "echo" => new SimpleDelayEffect { Mix = 0.25f, TimeMs = 350f, Feedback = 0.4f },
            "pingpong" => new SimpleDelayEffect { Mix = 0.3f, TimeMs = 420f, Feedback = 0.45f },
            _ => new SimpleDelayEffect()
        };
        configure?.Invoke(effect);
        return effect;
    }

    public static TremoloEffect TremoloPreset(string name, Action<TremoloEffect>? configure)
    {
        var preset = name?.Trim().ToLowerInvariant() ?? "soft";
        var effect = preset switch
        {
            "soft" => new TremoloEffect { Depth = 0.3f, Rate = 4f },
            "hard" => new TremoloEffect { Depth = 0.7f, Rate = 8f },
            "slow" => new TremoloEffect { Depth = 0.4f, Rate = 1.5f },
            _ => new TremoloEffect()
        };
        configure?.Invoke(effect);
        return effect;
    }

    public static BitCrusherEffect BitCrushPreset(string name, Action<BitCrusherEffect>? configure)
    {
        var preset = name?.Trim().ToLowerInvariant() ?? "lofi";
        var effect = preset switch
        {
            "lofi" => new BitCrusherEffect { BitDepth = 6, Downsample = 4, Mix = 1f },
            "mild" => new BitCrusherEffect { BitDepth = 10, Downsample = 2, Mix = 0.7f },
            _ => new BitCrusherEffect()
        };
        configure?.Invoke(effect);
        return effect;
    }

    public static NoiseEffect NoisePreset(string name, Action<NoiseEffect>? configure)
    {
        var preset = name?.Trim().ToLowerInvariant() ?? "light";
        var effect = preset switch
        {
            "light" => new NoiseEffect { Amount = 0.02f, Mix = 1f },
            "tape" => new NoiseEffect { Amount = 0.04f, Mix = 1f },
            _ => new NoiseEffect()
        };
        configure?.Invoke(effect);
        return effect;
    }

    public static DriveEffect DrivePreset(string name, Action<DriveEffect>? configure)
    {
        var preset = name?.Trim().ToLowerInvariant() ?? "warm";
        var effect = preset switch
        {
            "warm" => new DriveEffect { Drive = 1.5f, Mix = 0.7f },
            "hard" => new DriveEffect { Drive = 4f, Mix = 1f },
            _ => new DriveEffect()
        };
        configure?.Invoke(effect);
        return effect;
    }

    public static SimpleFilterEffect FilterPreset(string name, Action<SimpleFilterEffect>? configure)
    {
        var preset = name?.Trim().ToLowerInvariant() ?? "low";
        var effect = preset switch
        {
            "low" => new SimpleFilterEffect { Type = SimpleFilterType.LowPass, CutoffHz = 1200f },
            "high" => new SimpleFilterEffect { Type = SimpleFilterType.HighPass, CutoffHz = 200f },
            _ => new SimpleFilterEffect()
        };
        configure?.Invoke(effect);
        return effect;
    }
}
