// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System.Runtime.InteropServices;

namespace MusicEngine.Core.Vst.Vst3.Structures;

/// <summary>
/// VST3 Chord structure for musical chord information.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct Chord
{
    /// <summary>
    /// Key note (0-11, where 0=C, 1=C#, ... 11=B).
    /// </summary>
    public byte KeyNote;

    /// <summary>
    /// Root note (0-11, where 0=C, 1=C#, ... 11=B).
    /// </summary>
    public byte RootNote;

    /// <summary>
    /// Bit mask for chord tones.
    /// Each bit represents a semitone from the root note.
    /// </summary>
    public short ChordMask;

    /// <summary>
    /// Creates a new Chord structure.
    /// </summary>
    /// <param name="keyNote">Key note (0-11).</param>
    /// <param name="rootNote">Root note (0-11).</param>
    /// <param name="chordMask">Bit mask for chord tones.</param>
    public Chord(byte keyNote, byte rootNote, short chordMask)
    {
        KeyNote = keyNote;
        RootNote = rootNote;
        ChordMask = chordMask;
    }
}

/// <summary>
/// VST3 Process context structure containing transport and timing information.
/// Used during audio processing to provide timing, tempo, and transport state.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct ProcessContext
{
    /// <summary>
    /// Transport state flags indicating which fields are valid and current playback state.
    /// </summary>
    public Vst3ProcessContextFlags State;

    /// <summary>
    /// Current sample rate in Hz.
    /// </summary>
    public double SampleRate;

    /// <summary>
    /// Project time in samples (always valid).
    /// </summary>
    public long ProjectTimeSamples;

    /// <summary>
    /// System time in nanoseconds.
    /// Valid when <see cref="Vst3ProcessContextFlags.SystemTimeValid"/> is set.
    /// </summary>
    public long SystemTime;

    /// <summary>
    /// Continuous time samples (not affected by loop/cycle).
    /// Valid when <see cref="Vst3ProcessContextFlags.ContTimeValid"/> is set.
    /// </summary>
    public long ContinuousTimeSamples;

    /// <summary>
    /// Musical position in quarter notes (beats).
    /// Valid when <see cref="Vst3ProcessContextFlags.ProjectTimeMusicValid"/> is set.
    /// </summary>
    public double ProjectTimeMusic;

    /// <summary>
    /// Last bar start position in quarter notes.
    /// Valid when <see cref="Vst3ProcessContextFlags.BarPositionValid"/> is set.
    /// </summary>
    public double BarPositionMusic;

    /// <summary>
    /// Cycle start position in quarter notes.
    /// Valid when <see cref="Vst3ProcessContextFlags.CycleValid"/> is set.
    /// </summary>
    public double CycleStartMusic;

    /// <summary>
    /// Cycle end position in quarter notes.
    /// Valid when <see cref="Vst3ProcessContextFlags.CycleValid"/> is set.
    /// </summary>
    public double CycleEndMusic;

    /// <summary>
    /// Tempo in beats per minute (BPM).
    /// Valid when <see cref="Vst3ProcessContextFlags.TempoValid"/> is set.
    /// </summary>
    public double Tempo;

    /// <summary>
    /// Time signature numerator (e.g., 4 in 4/4 time).
    /// Valid when <see cref="Vst3ProcessContextFlags.TimeSigValid"/> is set.
    /// </summary>
    public int TimeSigNumerator;

    /// <summary>
    /// Time signature denominator (e.g., 4 in 4/4 time).
    /// Valid when <see cref="Vst3ProcessContextFlags.TimeSigValid"/> is set.
    /// </summary>
    public int TimeSigDenominator;

    /// <summary>
    /// Chord information packed as int (see <see cref="Structures.Chord"/>).
    /// Valid when <see cref="Vst3ProcessContextFlags.ChordValid"/> is set.
    /// </summary>
    public int Chord;

    /// <summary>
    /// SMPTE offset in subframes (1/80 of a frame).
    /// Valid when <see cref="Vst3ProcessContextFlags.SmpteValid"/> is set.
    /// </summary>
    public int SmpteOffsetSubframes;

    /// <summary>
    /// SMPTE frame rate.
    /// Valid when <see cref="Vst3ProcessContextFlags.SmpteValid"/> is set.
    /// </summary>
    public Vst3FrameRate FrameRate;

    /// <summary>
    /// Samples until next MIDI clock.
    /// Valid when <see cref="Vst3ProcessContextFlags.ClockValid"/> is set.
    /// </summary>
    public int SamplesToNextClock;

    /// <summary>
    /// Checks if the transport is currently playing.
    /// </summary>
    public readonly bool IsPlaying => (State & Vst3ProcessContextFlags.Playing) != 0;

    /// <summary>
    /// Checks if cycle/loop mode is active.
    /// </summary>
    public readonly bool IsCycleActive => (State & Vst3ProcessContextFlags.CycleActive) != 0;

    /// <summary>
    /// Checks if recording is active.
    /// </summary>
    public readonly bool IsRecording => (State & Vst3ProcessContextFlags.Recording) != 0;

    /// <summary>
    /// Gets the chord information as a Chord structure.
    /// </summary>
    public readonly Chord GetChord()
    {
        return new Chord(
            (byte)(Chord & 0xFF),
            (byte)((Chord >> 8) & 0xFF),
            (short)((Chord >> 16) & 0xFFFF)
        );
    }

    /// <summary>
    /// Sets the chord information from a Chord structure.
    /// </summary>
    /// <param name="chord">The chord to set.</param>
    public void SetChord(Chord chord)
    {
        Chord = chord.KeyNote | (chord.RootNote << 8) | (chord.ChordMask << 16);
    }
}
