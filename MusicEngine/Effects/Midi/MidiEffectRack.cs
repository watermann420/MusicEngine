// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Modular MIDI effect rack.

using System;

namespace MusicEngine.Effects.Midi;

/// <summary>
/// Modular MIDI effect rack.
/// </summary>
public sealed class MidiEffectRack : IMidiEffect
{
    private readonly MidiEffectChain _chain = new();

    public MidiEffectRack Add(IMidiEffect effect)
    {
        if (effect != null)
        {
            _chain.Add(effect);
        }
        return this;
    }

    public MidiEffectRack Clear()
    {
        _chain.Clear();
        return this;
    }

    public MidiEffectRack Transpose(int semitones)
        => Add(new TransposeEffect { Semitones = semitones });

    public MidiEffectRack Humanize(int range = 8)
        => Add(new VelocityHumanizeEffect { Range = range });

    public MidiEffectRack Gate(float probability = 0.8f)
        => Add(new RandomGateEffect { Probability = probability });

    public MidiEffectRack Trigger(Action<int, int>? noteOn = null, Action<int>? noteOff = null)
        => Add(new MidiTriggerEffect(noteOn, noteOff));

    public MidiEffectRack Custom(MidiNoteOnHandler? noteOn = null, MidiNoteOffHandler? noteOff = null)
        => Add(new CustomMidiEffect(noteOn, noteOff));

    public bool ProcessNoteOn(ref int note, ref int velocity)
        => _chain.ProcessNoteOn(ref note, ref velocity);

    public bool ProcessNoteOff(ref int note)
        => _chain.ProcessNoteOff(ref note);
}
