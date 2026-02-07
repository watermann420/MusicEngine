// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Per-pattern timing settings.

namespace MusicEngine.Timing;

/// <summary>
/// Per-pattern timing settings that can override the master.
/// </summary>
public sealed class TimingSettings
{
    /// <summary>
    /// Optional BPM override for the pattern.
    /// </summary>
    public double? Bpm { get; set; }

    /// <summary>
    /// Whether to use the master's groove settings.
    /// </summary>
    public bool UseMasterGroove { get; set; } = true;

    /// <summary>
    /// Groove settings to use when <see cref="UseMasterGroove"/> is false.
    /// </summary>
    public GrooveSettings Groove { get; } = new GrooveSettings();
}
