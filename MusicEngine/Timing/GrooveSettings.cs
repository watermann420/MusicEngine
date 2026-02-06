// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Groove settings (swing + humanize).

namespace MusicEngine.Timing;

public sealed class GrooveSettings
{
    public double Swing { get; set; }
    public HumanizeSettings Humanize { get; } = new HumanizeSettings();
}
