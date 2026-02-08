// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: MIDI trigger effect for note callbacks.

using System;

namespace MusicEngine.Effects.Midi;

/// <summary>
/// MIDI trigger effect that calls callbacks for note events.
/// </summary>
public sealed class MidiTriggerEffect : IMidiEffect
{
    private readonly Action<int, int>? _noteOn;
    private readonly Action<int>? _noteOff;

    public MidiTriggerEffect(Action<int, int>? noteOn = null, Action<int>? noteOff = null)
    {
        _noteOn = noteOn;
        _noteOff = noteOff;
    }

    public bool ProcessNoteOn(ref int note, ref int velocity)
    {
        _noteOn?.Invoke(note, velocity);
        return true;
    }

    public bool ProcessNoteOff(ref int note)
    {
        _noteOff?.Invoke(note);
        return true;
    }
}
