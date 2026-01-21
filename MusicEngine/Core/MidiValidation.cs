namespace MusicEngine.Core;

/// <summary>
/// MIDI value validation helpers.
/// </summary>
public static class MidiValidation
{
    /// <summary>
    /// Validates that a MIDI note number is in range (0-127).
    /// </summary>
    public static int ValidateNote(int note, string? paramName = null)
    {
        return Guard.InRange(note, 0, 127, paramName ?? nameof(note));
    }

    /// <summary>
    /// Validates that a MIDI velocity is in range (0-127).
    /// </summary>
    public static int ValidateVelocity(int velocity, string? paramName = null)
    {
        return Guard.InRange(velocity, 0, 127, paramName ?? nameof(velocity));
    }

    /// <summary>
    /// Validates that a MIDI channel is in range (0-15).
    /// </summary>
    public static int ValidateChannel(int channel, string? paramName = null)
    {
        return Guard.InRange(channel, 0, 15, paramName ?? nameof(channel));
    }

    /// <summary>
    /// Validates that a MIDI controller number is in range (0-127).
    /// </summary>
    public static int ValidateController(int controller, string? paramName = null)
    {
        return Guard.InRange(controller, 0, 127, paramName ?? nameof(controller));
    }

    /// <summary>
    /// Validates that a MIDI program number is in range (0-127).
    /// </summary>
    public static int ValidateProgram(int program, string? paramName = null)
    {
        return Guard.InRange(program, 0, 127, paramName ?? nameof(program));
    }

    /// <summary>
    /// Validates that a MIDI pitch bend value is in range (0-16383).
    /// </summary>
    public static int ValidatePitchBend(int pitchBend, string? paramName = null)
    {
        return Guard.InRange(pitchBend, 0, 16383, paramName ?? nameof(pitchBend));
    }

    /// <summary>
    /// Clamps a MIDI note number to valid range (0-127).
    /// </summary>
    public static int ClampNote(int note)
    {
        return Math.Clamp(note, 0, 127);
    }

    /// <summary>
    /// Clamps a MIDI velocity to valid range (0-127).
    /// </summary>
    public static int ClampVelocity(int velocity)
    {
        return Math.Clamp(velocity, 0, 127);
    }

    /// <summary>
    /// Clamps a MIDI channel to valid range (0-15).
    /// </summary>
    public static int ClampChannel(int channel)
    {
        return Math.Clamp(channel, 0, 15);
    }

    /// <summary>
    /// Determines if a note number is valid.
    /// </summary>
    public static bool IsValidNote(int note)
    {
        return note >= 0 && note <= 127;
    }

    /// <summary>
    /// Determines if a velocity is valid.
    /// </summary>
    public static bool IsValidVelocity(int velocity)
    {
        return velocity >= 0 && velocity <= 127;
    }

    /// <summary>
    /// Determines if a channel is valid.
    /// </summary>
    public static bool IsValidChannel(int channel)
    {
        return channel >= 0 && channel <= 15;
    }
}
