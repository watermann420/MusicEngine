// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Global timing controller for BPM, swing, and humanize.

namespace MusicEngine.Timing;

/// <summary>
/// Global timing controller for BPM, swing, and humanize.
/// </summary>
public sealed class TimingMaster
{
    private double _bpm = 120.0;

    /// <summary>
    /// Current tempo in beats per minute.
    /// </summary>
    public double Bpm
    {
        get => _bpm;
        set => _bpm = value <= 0 ? 120.0 : value;
    }

    /// <summary>
    /// Groove and humanize settings.
    /// </summary>
    public GrooveSettings Groove { get; } = new GrooveSettings();

    /// <summary>
    /// Enable or disable groove swing.
    /// </summary>
    public bool EnableGroove { get; set; } = true;

    /// <summary>
    /// Enable or disable humanize jitter.
    /// </summary>
    public bool EnableHumanize { get; set; } = true;
}
