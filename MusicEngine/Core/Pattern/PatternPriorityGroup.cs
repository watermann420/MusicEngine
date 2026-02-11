// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Priority routing groups for patterns.

using System.Collections.Generic;

namespace MusicEngine.Core;

/// <summary>
/// Ordered pattern group where only the first enabled target receives notes.
/// </summary>
public sealed class PatternPriorityGroup
{
    internal readonly List<PatternPriorityRoute> Routes = new();
}

/// <summary>
/// Route entry for priority pattern groups.
/// </summary>
public sealed class PatternPriorityRoute
{
    /// <summary>
    /// Create a priority route for a synth target.
    /// </summary>
    public PatternPriorityRoute(ISynth synth)
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
