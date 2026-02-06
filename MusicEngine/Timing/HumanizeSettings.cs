// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Humanize settings for timing and velocity.

namespace MusicEngine.Timing;

public sealed class HumanizeSettings
{
    public double TimeMs { get; set; }
    public double Velocity { get; set; }
    public int? Seed { get; set; }
}
