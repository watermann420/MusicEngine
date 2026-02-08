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
    public static EffectRack Create() => new();
}
