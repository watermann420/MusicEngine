// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Groove settings (swing + humanize).

namespace MusicEngine.Timing;

/// <summary>
/// Groove settings for swing and humanize.
/// </summary>
public sealed class GrooveSettings
{
    /// <summary>
    /// Swing amount in [0, 1].
    /// </summary>
    public double Swing { get; set; }

    /// <summary>
    /// Humanize settings for timing and velocity.
    /// </summary>
    public HumanizeSettings Humanize { get; } = new HumanizeSettings();
}
