// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: MIDI effect presets and factories.

namespace MusicEngine.Effects.Midi;

/// <summary>
/// Preset MIDI effect factory for quick use in scripts.
/// </summary>
public static class MidiEffect
{
    public static TransposeEffect Transpose => new();
    public static VelocityHumanizeEffect Humanize => new();
    public static RandomGateEffect Gate => new();
    public static MidiTriggerEffect Trigger => new();
    public static MidiEffectRack Create() => new();
}
