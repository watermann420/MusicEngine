// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Pattern note event configuration.

namespace MusicEngine.Core;

/// <summary>
/// Pattern note event configuration.
/// </summary>
public sealed class NoteEvent
{
    /// <summary>
    /// Beat position of the note.
    /// </summary>
    public double Beat { get; set; }
    /// <summary>
    /// MIDI note number.
    /// </summary>
    public int Note { get; set; }
    /// <summary>
    /// MIDI velocity.
    /// </summary>
    public int Velocity { get; set; }
    /// <summary>
    /// Duration in beats.
    /// </summary>
    public double Duration { get; set; }

    /// <summary>
    /// Optional millisecond position override.
    /// </summary>
    public double? BeatMs { get; set; }

    /// <summary>
    /// Optional millisecond duration override.
    /// </summary>
    public double? DurationMs { get; set; }

    /// <summary>
    /// Optional slide target note.
    /// </summary>
    public int? SlideTo { get; set; }

    /// <summary>
    /// Optional slide time in beats.
    /// </summary>
    public double? SlideTimeMs { get; set; }
}
