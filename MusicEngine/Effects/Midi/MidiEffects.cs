// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Modular MIDI effects and effect chain.

using System;
using System.Collections.Generic;

namespace MusicEngine.Effects.Midi;

/// <summary>
/// MIDI effect interface for note processing.
/// </summary>
public interface IMidiEffect
{
    /// <summary>
    /// Process a note-on event. Return false to block the event.
    /// </summary>
    bool ProcessNoteOn(ref int note, ref int velocity);

    /// <summary>
    /// Process a note-off event. Return false to block the event.
    /// </summary>
    bool ProcessNoteOff(ref int note);
}

/// <summary>
/// Simple MIDI effect chain.
/// </summary>
public sealed class MidiEffectChain
{
    private readonly List<IMidiEffect> _effects = new();

    public void Add(IMidiEffect effect)
    {
        if (effect == null) return;
        _effects.Add(effect);
    }

    public void Clear() => _effects.Clear();

    public bool ProcessNoteOn(ref int note, ref int velocity)
    {
        for (int i = 0; i < _effects.Count; i++)
        {
            if (!_effects[i].ProcessNoteOn(ref note, ref velocity)) return false;
        }
        return true;
    }

    public bool ProcessNoteOff(ref int note)
    {
        for (int i = 0; i < _effects.Count; i++)
        {
            if (!_effects[i].ProcessNoteOff(ref note)) return false;
        }
        return true;
    }
}

/// <summary>
/// MIDI effect that transposes notes by a fixed number of semitones.
/// </summary>
public sealed class TransposeEffect : IMidiEffect
{
    public int Semitones { get; set; }

    public bool ProcessNoteOn(ref int note, ref int velocity)
    {
        note = Math.Clamp(note + Semitones, 0, 127);
        return true;
    }

    public bool ProcessNoteOff(ref int note)
    {
        note = Math.Clamp(note + Semitones, 0, 127);
        return true;
    }
}

/// <summary>
/// MIDI effect that applies random velocity variation.
/// </summary>
public sealed class VelocityHumanizeEffect : IMidiEffect
{
    private readonly Random _random = new();
    public int Range { get; set; } = 8;

    public bool ProcessNoteOn(ref int note, ref int velocity)
    {
        int delta = _random.Next(-Range, Range + 1);
        velocity = Math.Clamp(velocity + delta, 1, 127);
        return true;
    }

    public bool ProcessNoteOff(ref int note) => true;
}

/// <summary>
/// MIDI effect that randomly drops note-on events.
/// </summary>
public sealed class RandomGateEffect : IMidiEffect
{
    private readonly Random _random = new();
    public float Probability { get; set; } = 0.8f;

    public bool ProcessNoteOn(ref int note, ref int velocity)
    {
        var p = Math.Clamp(Probability, 0f, 1f);
        return _random.NextDouble() <= p;
    }

    public bool ProcessNoteOff(ref int note) => true;
}
