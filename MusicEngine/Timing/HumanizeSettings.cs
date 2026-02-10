// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Humanize settings for timing and velocity.

namespace MusicEngine.Timing;

/// <summary>
/// Humanize settings for timing and velocity jitter.
/// </summary>
public sealed class HumanizeSettings
{
    /// <summary>
    /// Max timing jitter in milliseconds.
    /// </summary>
    public double TimeMs { get; set; }

    /// <summary>
    /// Velocity jitter amount (0-1 range multiplier).
    /// </summary>
    public double Velocity { get; set; }

    /// <summary>
    /// Optional RNG seed for deterministic humanize.
    /// </summary>
    public int? Seed { get; set; }
}
