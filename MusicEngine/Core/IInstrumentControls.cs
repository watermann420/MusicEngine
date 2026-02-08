// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Common instrument control properties.

namespace MusicEngine.Core;

/// <summary>
/// Common instrument control properties.
/// </summary>
public interface IInstrumentControls
{
    /// <summary>
    /// Master volume (0..1).
    /// </summary>
    float Volume { get; set; }

    /// <summary>
    /// Pan position (-1..1).
    /// </summary>
    float Pan { get; set; }

    /// <summary>
    /// Mod wheel value (0..1).
    /// </summary>
    float ModWheel { get; set; }

    /// <summary>
    /// MIDI channel (0..15), or -1 for all.
    /// </summary>
    int Channel { get; set; }

    /// <summary>
    /// Reverb amount (0..1).
    /// </summary>
    float Reverb { get; set; }

    /// <summary>
    /// Chorus amount (0..1).
    /// </summary>
    float Chorus { get; set; }
}
