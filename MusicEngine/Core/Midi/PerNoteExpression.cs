//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Per-note expression data for MPE (MIDI Polyphonic Expression).


using System;


namespace MusicEngine.Core.Midi;


/// <summary>
/// Represents per-note expression data for MPE-enabled synthesizers.
/// Each active note has its own expression parameters that can be independently modulated.
/// </summary>
/// <remarks>
/// MPE expression dimensions:
/// - Strike: Initial velocity (X-axis attack)
/// - Lift: Release velocity (X-axis release)
/// - Slide: Y-axis position, typically CC74, maps to timbre/brightness
/// - Pressure: Z-axis pressure (channel aftertouch)
/// - Pitch: Per-note pitch bend combined with base pitch
/// </remarks>
public class PerNoteExpression
{
    /// <summary>
    /// Unique identifier for this note, combining channel and note number.
    /// Format: (channel * 128) + noteNumber
    /// </summary>
    public int NoteId { get; }

    /// <summary>
    /// The MIDI channel this note is playing on (0-15).
    /// </summary>
    public int Channel { get; }

    /// <summary>
    /// The MIDI note number (0-127).
    /// </summary>
    public int NoteNumber { get; }

    /// <summary>
    /// The base frequency of the note in Hz (without pitch bend).
    /// </summary>
    public double BaseFrequency { get; }

    /// <summary>
    /// Strike velocity (0-1). Initial velocity when the note was triggered.
    /// Corresponds to Note On velocity.
    /// </summary>
    public float Strike { get; set; }

    /// <summary>
    /// Lift velocity (0-1). Release velocity when the note is released.
    /// Corresponds to Note Off velocity.
    /// </summary>
    public float Lift { get; set; }

    /// <summary>
    /// Slide value (0-1). Y-axis position, derived from CC74.
    /// Typically mapped to filter cutoff or timbre brightness.
    /// 0.5 is the neutral/center position.
    /// </summary>
    public float Slide { get; set; } = 0.5f;

    /// <summary>
    /// Pressure value (0-1). Z-axis pressure from channel aftertouch.
    /// Typically mapped to volume, vibrato depth, or modulation intensity.
    /// </summary>
    public float Pressure { get; set; }

    /// <summary>
    /// Per-note pitch bend in semitones (-range to +range).
    /// Combined with base pitch to get the final pitch.
    /// </summary>
    public float PitchBendSemitones { get; set; }

    /// <summary>
    /// Raw pitch bend value (-1 to +1) before conversion to semitones.
    /// </summary>
    public float PitchBendRaw { get; set; }

    /// <summary>
    /// The pitch bend range in semitones (typically 48 for MPE).
    /// </summary>
    public int PitchBendRange { get; set; } = 48;

    /// <summary>
    /// Gets the final pitch in semitones including pitch bend.
    /// </summary>
    public float PitchSemitones => NoteNumber + PitchBendSemitones;

    /// <summary>
    /// Gets the final frequency in Hz including pitch bend.
    /// </summary>
    public double Frequency
    {
        get
        {
            // Calculate frequency with pitch bend applied
            double semitoneOffset = PitchBendSemitones;
            return BaseFrequency * Math.Pow(2.0, semitoneOffset / 12.0);
        }
    }

    /// <summary>
    /// Timestamp when this expression was last updated.
    /// </summary>
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// Whether this note is currently active (not released).
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether this note is in the release phase.
    /// </summary>
    public bool IsReleasing { get; set; }

    /// <summary>
    /// Creates a new per-note expression instance.
    /// </summary>
    /// <param name="channel">MIDI channel (0-15).</param>
    /// <param name="noteNumber">MIDI note number (0-127).</param>
    /// <param name="velocity">Initial velocity (0-127).</param>
    /// <param name="pitchBendRange">Pitch bend range in semitones.</param>
    public PerNoteExpression(int channel, int noteNumber, int velocity = 127, int pitchBendRange = 48)
    {
        Channel = Math.Clamp(channel, 0, 15);
        NoteNumber = Math.Clamp(noteNumber, 0, 127);
        NoteId = (Channel * 128) + NoteNumber;
        PitchBendRange = pitchBendRange;

        // Calculate base frequency (A4 = 440 Hz)
        BaseFrequency = 440.0 * Math.Pow(2.0, (NoteNumber - 69.0) / 12.0);

        // Set initial strike velocity
        Strike = velocity / 127f;
        Lift = 0f;

        LastUpdated = DateTime.Now;
    }

    /// <summary>
    /// Updates the pitch bend from a raw 14-bit MIDI pitch bend value.
    /// </summary>
    /// <param name="pitchBendValue">14-bit pitch bend value (0-16383, center = 8192).</param>
    public void SetPitchBend(int pitchBendValue)
    {
        // Convert 14-bit value to -1 to +1 range
        PitchBendRaw = (pitchBendValue - 8192) / 8192f;
        PitchBendSemitones = PitchBendRaw * PitchBendRange;
        LastUpdated = DateTime.Now;
    }

    /// <summary>
    /// Updates the slide value from a CC74 MIDI value.
    /// </summary>
    /// <param name="slideValue">CC74 value (0-127).</param>
    public void SetSlide(int slideValue)
    {
        Slide = slideValue / 127f;
        LastUpdated = DateTime.Now;
    }

    /// <summary>
    /// Updates the pressure from channel aftertouch.
    /// </summary>
    /// <param name="pressureValue">Aftertouch value (0-127).</param>
    public void SetPressure(int pressureValue)
    {
        Pressure = pressureValue / 127f;
        LastUpdated = DateTime.Now;
    }

    /// <summary>
    /// Sets the release velocity when the note is released.
    /// </summary>
    /// <param name="releaseVelocity">Release velocity (0-127).</param>
    public void Release(int releaseVelocity = 64)
    {
        Lift = releaseVelocity / 127f;
        IsReleasing = true;
        LastUpdated = DateTime.Now;
    }

    /// <summary>
    /// Marks the note as inactive (fully released).
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        IsReleasing = false;
        LastUpdated = DateTime.Now;
    }

    /// <summary>
    /// Creates a copy of this expression data.
    /// </summary>
    public PerNoteExpression Clone()
    {
        return new PerNoteExpression(Channel, NoteNumber, (int)(Strike * 127), PitchBendRange)
        {
            Lift = Lift,
            Slide = Slide,
            Pressure = Pressure,
            PitchBendRaw = PitchBendRaw,
            PitchBendSemitones = PitchBendSemitones,
            IsActive = IsActive,
            IsReleasing = IsReleasing,
            LastUpdated = LastUpdated
        };
    }

    /// <summary>
    /// Interpolates between this expression and another for smooth transitions.
    /// </summary>
    /// <param name="target">Target expression state.</param>
    /// <param name="amount">Interpolation amount (0-1).</param>
    /// <returns>Interpolated expression values.</returns>
    public PerNoteExpression Lerp(PerNoteExpression target, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);

        var result = Clone();
        result.Slide = Slide + (target.Slide - Slide) * amount;
        result.Pressure = Pressure + (target.Pressure - Pressure) * amount;
        result.PitchBendRaw = PitchBendRaw + (target.PitchBendRaw - PitchBendRaw) * amount;
        result.PitchBendSemitones = PitchBendSemitones + (target.PitchBendSemitones - PitchBendSemitones) * amount;
        result.LastUpdated = DateTime.Now;

        return result;
    }

    /// <summary>
    /// Creates a unique note ID from channel and note number.
    /// </summary>
    public static int CreateNoteId(int channel, int noteNumber)
    {
        return (channel * 128) + noteNumber;
    }

    /// <summary>
    /// Extracts the channel from a note ID.
    /// </summary>
    public static int GetChannelFromId(int noteId)
    {
        return noteId / 128;
    }

    /// <summary>
    /// Extracts the note number from a note ID.
    /// </summary>
    public static int GetNoteNumberFromId(int noteId)
    {
        return noteId % 128;
    }

    public override string ToString()
    {
        return $"Note {NoteNumber} Ch{Channel + 1}: Pitch={PitchSemitones:F2}, Slide={Slide:F2}, Pressure={Pressure:F2}, Strike={Strike:F2}";
    }
}


/// <summary>
/// Event arguments for per-note expression changes.
/// </summary>
public class PerNoteExpressionEventArgs : EventArgs
{
    /// <summary>
    /// The expression data that changed.
    /// </summary>
    public PerNoteExpression Expression { get; }

    /// <summary>
    /// The type of change that occurred.
    /// </summary>
    public ExpressionChangeType ChangeType { get; }

    public PerNoteExpressionEventArgs(PerNoteExpression expression, ExpressionChangeType changeType)
    {
        Expression = expression;
        ChangeType = changeType;
    }
}


/// <summary>
/// Types of expression changes for events.
/// </summary>
public enum ExpressionChangeType
{
    /// <summary>Note was triggered (Note On).</summary>
    NoteOn,
    /// <summary>Note was released (Note Off).</summary>
    NoteOff,
    /// <summary>Pitch bend changed.</summary>
    PitchBend,
    /// <summary>Slide (CC74) changed.</summary>
    Slide,
    /// <summary>Pressure (aftertouch) changed.</summary>
    Pressure,
    /// <summary>Multiple parameters changed.</summary>
    Multiple
}
