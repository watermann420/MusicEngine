// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Runtime note activity information.

using System;
using System.Threading;

namespace MusicEngine.Core;

/// <summary>
/// Runtime note activity information.
/// </summary>
public sealed class NoteActivity
{
    /// <summary>
    /// MIDI note number.
    /// </summary>
    public int Note { get; init; }
    /// <summary>
    /// Velocity used when triggered.
    /// </summary>
    public int Velocity { get; init; }
    /// <summary>
    /// UTC timestamp when the note started.
    /// </summary>
    public DateTime StartedUtc { get; init; }

    internal CancellationTokenSource? SlideCancel { get; init; }
}
