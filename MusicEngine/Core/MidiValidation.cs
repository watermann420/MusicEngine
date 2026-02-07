// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Minimal MIDI validation helpers.

namespace MusicEngine.Core;

/// <summary>
/// MIDI value validation helpers.
/// </summary>
public static class MidiValidation
{
    /// <summary>
    /// Minimum MIDI note value.
    /// </summary>
    public const int MinNote = 0;
    /// <summary>
    /// Maximum MIDI note value.
    /// </summary>
    public const int MaxNote = 127;
    /// <summary>
    /// Minimum MIDI velocity value.
    /// </summary>
    public const int MinVelocity = 0;
    /// <summary>
    /// Maximum MIDI velocity value.
    /// </summary>
    public const int MaxVelocity = 127;

    /// <summary>
    /// Validate a MIDI note number.
    /// </summary>
    public static int ValidateNote(int note) => Guard.InRange(note, MinNote, MaxNote);

    /// <summary>
    /// Validate a MIDI velocity value.
    /// </summary>
    public static int ValidateVelocity(int velocity) => Guard.InRange(velocity, MinVelocity, MaxVelocity);
}
