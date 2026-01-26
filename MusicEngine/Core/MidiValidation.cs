// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

namespace MusicEngine.Core;

public static class MidiValidation
{
    public const int MinNote = 0;
    public const int MaxNote = 127;
    public const int MinVelocity = 0;
    public const int MaxVelocity = 127;
    public const int MinChannel = 0;
    public const int MaxChannel = 15;
    public const int MinControlValue = 0;
    public const int MaxControlValue = 127;
    public const int MinController = 0;
    public const int MaxController = 127;
    public const int MinProgram = 0;
    public const int MaxProgram = 127;
    public const int MinPitchBend = 0;
    public const int MaxPitchBend = 16383;

    public static int ValidateNote(int note) => Guard.InRange(note, MinNote, MaxNote);
    public static int ValidateVelocity(int velocity) => Guard.InRange(velocity, MinVelocity, MaxVelocity);
    public static int ValidateChannel(int channel) => Guard.InRange(channel, MinChannel, MaxChannel);
    public static int ValidateControlValue(int value) => Guard.InRange(value, MinControlValue, MaxControlValue);
    public static int ValidateController(int controller) => Guard.InRange(controller, MinController, MaxController);
    public static int ValidateProgram(int program) => Guard.InRange(program, MinProgram, MaxProgram);
    public static int ValidatePitchBend(int pitchBend) => Guard.InRange(pitchBend, MinPitchBend, MaxPitchBend);

    public static bool IsValidNote(int note) => note >= MinNote && note <= MaxNote;
    public static bool IsValidVelocity(int velocity) => velocity >= MinVelocity && velocity <= MaxVelocity;
    public static bool IsValidChannel(int channel) => channel >= MinChannel && channel <= MaxChannel;
}
