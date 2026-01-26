// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;

namespace MusicEngine.Core.PDC;

/// <summary>
/// Event arguments for latency change events.
/// Contains information about the previous and new latency values.
/// </summary>
public class LatencyChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the previous latency value in samples.
    /// </summary>
    public int OldLatency { get; }

    /// <summary>
    /// Gets the new latency value in samples.
    /// </summary>
    public int NewLatency { get; }

    /// <summary>
    /// Gets the difference between the new and old latency values.
    /// A positive value indicates increased latency; negative indicates reduced latency.
    /// </summary>
    public int LatencyDelta => NewLatency - OldLatency;

    /// <summary>
    /// Creates new latency changed event arguments.
    /// </summary>
    /// <param name="oldLatency">The previous latency value in samples.</param>
    /// <param name="newLatency">The new latency value in samples.</param>
    public LatencyChangedEventArgs(int oldLatency, int newLatency)
    {
        OldLatency = oldLatency;
        NewLatency = newLatency;
    }
}
