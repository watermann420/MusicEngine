//Engine License (MEL) – Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: A class representing a single note event in a pattern.


using System;
using System.Collections.Generic;


namespace MusicEngine.Core;


// Represents a single note event in a pattern
public class NoteEvent
{
    public double Beat { get; set; } // in beats
    public int Note { get; set; } // MIDI note number
    public int Velocity { get; set; } // 0-127
    public double Duration { get; set; } = 0.25; // in beats

    /// <summary>Unique identifier for this note event.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Code source info for highlighting this specific note.</summary>
    public CodeSourceInfo? SourceInfo { get; set; }

    /// <summary>Parameter bindings for live slider control.</summary>
    public Dictionary<string, LiveParameter>? Parameters { get; set; }
}
