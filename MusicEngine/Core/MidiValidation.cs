// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Minimal MIDI validation helpers.

namespace MusicEngine.Core;

public static class MidiValidation
{
    public const int MinNote = 0;
    public const int MaxNote = 127;
    public const int MinVelocity = 0;
    public const int MaxVelocity = 127;

    public static int ValidateNote(int note) => Guard.InRange(note, MinNote, MaxNote);
    public static int ValidateVelocity(int velocity) => Guard.InRange(velocity, MinVelocity, MaxVelocity);
}
