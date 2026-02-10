// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Custom MIDI effect callback wrapper.

using System;

namespace MusicEngine.Effects.Midi;

public delegate bool MidiNoteOnHandler(ref int note, ref int velocity);
public delegate bool MidiNoteOffHandler(ref int note);

/// <summary>
/// MIDI effect backed by custom callbacks.
/// </summary>
public sealed class CustomMidiEffect : IMidiEffect
{
    private readonly MidiNoteOnHandler? _noteOn;
    private readonly MidiNoteOffHandler? _noteOff;

    public CustomMidiEffect(MidiNoteOnHandler? noteOn = null, MidiNoteOffHandler? noteOff = null)
    {
        _noteOn = noteOn;
        _noteOff = noteOff;
    }

    public bool ProcessNoteOn(ref int note, ref int velocity)
    {
        if (_noteOn == null) return true;
        return _noteOn(ref note, ref velocity);
    }

    public bool ProcessNoteOff(ref int note)
    {
        if (_noteOff == null) return true;
        return _noteOff(ref note);
    }
}
