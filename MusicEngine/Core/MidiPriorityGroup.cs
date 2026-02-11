// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Priority MIDI routing group for exclusive/fallback targets.

using System.Collections.Generic;

namespace MusicEngine.Core;

/// <summary>
/// Ordered MIDI route group where only the first enabled target receives notes.
/// </summary>
public sealed class MidiPriorityGroup
{
    internal readonly List<MidiPriorityRoute> Routes = new();
}

/// <summary>
/// Route entry for priority groups.
/// </summary>
public sealed class MidiPriorityRoute
{
    /// <summary>
    /// Create a priority route for a synth target.
    /// </summary>
    public MidiPriorityRoute(ISynth synth)
    {
        Synth = synth;
    }

    /// <summary>
    /// Target synth.
    /// </summary>
    public ISynth Synth { get; }

    /// <summary>
    /// Whether this target is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
