// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Runtime.InteropServices;

namespace MusicEngine.Core.Vst.Vst3.Structures;

/// <summary>
/// VST3 event types enumeration.
/// </summary>
public enum Vst3EventType : ushort
{
    NoteOn = 0,
    NoteOff = 1,
    Data = 2,
    PolyPressure = 3,
    NoteExpressionValue = 4,
    NoteExpressionText = 5,
    Chord = 6,
    Scale = 7,
    LegacyMidiCCOut = 65535
}

/// <summary>
/// Event flags for VST3 events.
/// </summary>
[Flags]
public enum Vst3EventFlags : ushort
{
    None = 0,
    IsLive = 1 << 0,
    UserReserved1 = 1 << 14,
    UserReserved2 = 1 << 15
}

/// <summary>
/// Note-on event structure.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct NoteOnEvent
{
    /// <summary>
    /// Channel index (0-15).
    /// </summary>
    public short Channel;

    /// <summary>
    /// MIDI note number (0-127).
    /// </summary>
    public short Pitch;

    /// <summary>
    /// Tuning offset in cents (-1.0 to +1.0 semitones).
    /// </summary>
    public float Tuning;

    /// <summary>
    /// Note velocity (0.0 to 1.0).
    /// </summary>
    public float Velocity;

    /// <summary>
    /// Note length in samples (0 = unknown).
    /// </summary>
    public int Length;

    /// <summary>
    /// Note identifier (-1 = not specified).
    /// </summary>
    public int NoteId;
}

/// <summary>
/// Note-off event structure.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct NoteOffEvent
{
    /// <summary>
    /// Channel index (0-15).
    /// </summary>
    public short Channel;

    /// <summary>
    /// MIDI note number (0-127).
    /// </summary>
    public short Pitch;

    /// <summary>
    /// Release velocity (0.0 to 1.0).
    /// </summary>
    public float Velocity;

    /// <summary>
    /// Note identifier (-1 = not specified).
    /// </summary>
    public int NoteId;

    /// <summary>
    /// Tuning offset in cents.
    /// </summary>
    public float Tuning;
}

/// <summary>
/// Polyphonic pressure (aftertouch) event structure.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PolyPressureEvent
{
    /// <summary>
    /// Channel index (0-15).
    /// </summary>
    public short Channel;

    /// <summary>
    /// MIDI note number (0-127).
    /// </summary>
    public short Pitch;

    /// <summary>
    /// Pressure value (0.0 to 1.0).
    /// </summary>
    public float Pressure;

    /// <summary>
    /// Note identifier (-1 = not specified).
    /// </summary>
    public int NoteId;
}

/// <summary>
/// Note expression value event structure.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct NoteExpressionValueEvent
{
    /// <summary>
    /// Note expression type identifier.
    /// </summary>
    public uint TypeId;

    /// <summary>
    /// Note identifier.
    /// </summary>
    public int NoteId;

    /// <summary>
    /// Expression value (normalized 0.0 to 1.0).
    /// </summary>
    public double Value;
}

/// <summary>
/// Note expression text event structure.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct NoteExpressionTextEvent
{
    /// <summary>
    /// Note expression type identifier.
    /// </summary>
    public uint TypeId;

    /// <summary>
    /// Note identifier.
    /// </summary>
    public int NoteId;

    /// <summary>
    /// Length of text in characters.
    /// </summary>
    public uint TextLen;

    /// <summary>
    /// Pointer to UTF-16 text string.
    /// </summary>
    public IntPtr Text;
}

/// <summary>
/// Chord event structure containing chord information.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ChordEvent
{
    /// <summary>
    /// Root note of the chord (0-127).
    /// </summary>
    public short Root;

    /// <summary>
    /// Bass note of the chord (0-127).
    /// </summary>
    public short BassNote;

    /// <summary>
    /// Chord mask (each bit represents a semitone from root).
    /// </summary>
    public short Mask;

    /// <summary>
    /// Length of text description.
    /// </summary>
    public ushort TextLen;

    /// <summary>
    /// Pointer to UTF-16 chord name text.
    /// </summary>
    public IntPtr Text;
}

/// <summary>
/// Scale event structure containing scale information.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ScaleEvent
{
    /// <summary>
    /// Root note of the scale (0-127).
    /// </summary>
    public short Root;

    /// <summary>
    /// Scale mask (each bit represents a semitone from root).
    /// </summary>
    public short Mask;

    /// <summary>
    /// Length of text description.
    /// </summary>
    public ushort TextLen;

    /// <summary>
    /// Pointer to UTF-16 scale name text.
    /// </summary>
    public IntPtr Text;
}

/// <summary>
/// Union structure containing all possible event data types.
/// Uses explicit layout to overlay all event types at the same memory location.
/// </summary>
[StructLayout(LayoutKind.Explicit)]
public struct EventData
{
    /// <summary>
    /// Note-on event data.
    /// </summary>
    [FieldOffset(0)]
    public NoteOnEvent NoteOn;

    /// <summary>
    /// Note-off event data.
    /// </summary>
    [FieldOffset(0)]
    public NoteOffEvent NoteOff;

    /// <summary>
    /// Polyphonic pressure event data.
    /// </summary>
    [FieldOffset(0)]
    public PolyPressureEvent PolyPressure;

    /// <summary>
    /// Note expression value event data.
    /// </summary>
    [FieldOffset(0)]
    public NoteExpressionValueEvent NoteExpressionValue;

    /// <summary>
    /// Note expression text event data.
    /// </summary>
    [FieldOffset(0)]
    public NoteExpressionTextEvent NoteExpressionText;

    /// <summary>
    /// Chord event data.
    /// </summary>
    [FieldOffset(0)]
    public ChordEvent Chord;

    /// <summary>
    /// Scale event data.
    /// </summary>
    [FieldOffset(0)]
    public ScaleEvent Scale;
}

/// <summary>
/// Main VST3 event structure representing a single event.
/// Uses explicit layout to include the union of event data.
/// </summary>
[StructLayout(LayoutKind.Explicit)]
public struct Event
{
    /// <summary>
    /// Index of the event bus (0 = main bus).
    /// </summary>
    [FieldOffset(0)]
    public int BusIndex;

    /// <summary>
    /// Sample offset within the current processing block.
    /// </summary>
    [FieldOffset(4)]
    public int SampleOffset;

    /// <summary>
    /// Position in quarter notes (PPQ).
    /// </summary>
    [FieldOffset(8)]
    public double PpqPosition;

    /// <summary>
    /// Event flags (see Vst3EventFlags).
    /// </summary>
    [FieldOffset(16)]
    public ushort Flags;

    /// <summary>
    /// Event type (see Vst3EventType).
    /// </summary>
    [FieldOffset(18)]
    public ushort Type;

    /// <summary>
    /// Union of all event data types.
    /// </summary>
    [FieldOffset(20)]
    public EventData Data;

    /// <summary>
    /// Gets the event type as a Vst3EventType enum.
    /// </summary>
    public Vst3EventType EventType
    {
        get => (Vst3EventType)Type;
        set => Type = (ushort)value;
    }

    /// <summary>
    /// Gets the event flags as a Vst3EventFlags enum.
    /// </summary>
    public Vst3EventFlags EventFlags
    {
        get => (Vst3EventFlags)Flags;
        set => Flags = (ushort)value;
    }
}
