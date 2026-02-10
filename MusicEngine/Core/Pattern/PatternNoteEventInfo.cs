// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Lightweight note event data for editor feedback.

using System;

namespace MusicEngine.Core;

/// <summary>
/// Lightweight note event data for editor feedback.
/// </summary>
public readonly struct PatternNoteEventInfo
{
    /// <summary>
    /// Pattern identifier.
    /// </summary>
    public Guid PatternId { get; }
    /// <summary>
    /// MIDI note number.
    /// </summary>
    public int Note { get; }
    /// <summary>
    /// MIDI velocity.
    /// </summary>
    public int Velocity { get; }
    /// <summary>
    /// True when this is a note-on event.
    /// </summary>
    public bool IsOn { get; }
    /// <summary>
    /// UTC timestamp of the event.
    /// </summary>
    public DateTime TimestampUtc { get; }

    /// <summary>
    /// Create a new pattern note event info record.
    /// </summary>
    public PatternNoteEventInfo(Guid patternId, int note, int velocity, bool isOn, DateTime timestampUtc)
    {
        PatternId = patternId;
        Note = note;
        Velocity = velocity;
        IsOn = isOn;
        TimestampUtc = timestampUtc;
    }
}
