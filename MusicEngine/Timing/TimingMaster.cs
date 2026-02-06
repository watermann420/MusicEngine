// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Global timing controller for BPM, swing, and humanize.

namespace MusicEngine.Timing;

public sealed class TimingMaster
{
    private double _bpm = 120.0;

    public double Bpm
    {
        get => _bpm;
        set => _bpm = value <= 0 ? 120.0 : value;
    }

    public GrooveSettings Groove { get; } = new GrooveSettings();

    public bool EnableGroove { get; set; } = true;
    public bool EnableHumanize { get; set; } = true;
}
