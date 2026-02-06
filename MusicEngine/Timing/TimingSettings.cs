// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Per-pattern timing settings.

namespace MusicEngine.Timing;

public sealed class TimingSettings
{
    public double? Bpm { get; set; }
    public bool UseMasterGroove { get; set; } = true;
    public GrooveSettings Groove { get; } = new GrooveSettings();
}
