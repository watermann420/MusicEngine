// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Sequence driven by a base note event.

using System;

namespace MusicEngine.Core;

/// <summary>
/// Sequence driven by a base note event.
/// </summary>
public sealed class NoteSequence
{
    /// <summary>
    /// Sequence identifier.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// Base note settings (pitch, velocity, step duration).
    /// </summary>
    public NoteEvent Note { get; }

    /// <summary>
    /// Step string using 0/1.
    /// </summary>
    public string Steps { get; set; }

    /// <summary>
    /// Loop the sequence continuously after it starts.
    /// </summary>
    public bool Loop { get; set; }

    /// <summary>
    /// Enable or disable this sequence.
    /// </summary>
    public bool Enabled { get; set; } = true;

    public NoteSequence(NoteEvent note, string steps)
    {
        Note = note;
        Steps = steps;
    }
}
