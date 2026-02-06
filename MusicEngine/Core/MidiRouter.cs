// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Shared MIDI routing and control mapping for instruments.

using System;
using System.Collections.Generic;
using NAudio.Midi;

namespace MusicEngine.Core;

public sealed class MidiRouter
{
    private readonly Dictionary<int, ISynth> _routing = new();
    private readonly List<MidiMapping> _mappings = new();

    private sealed class MidiMapping
    {
        public int DeviceIndex { get; init; }
        public int ControlId { get; init; }
        public Action<float> Action { get; init; } = null!;
    }

    public void Route(int deviceIndex, ISynth synth) => _routing[deviceIndex] = synth;

    public void MapControlAction(int deviceIndex, int controlId, Action<float> action)
    {
        _mappings.Add(new MidiMapping
        {
            DeviceIndex = deviceIndex,
            ControlId = controlId,
            Action = action
        });
    }

    public void Clear()
    {
        _routing.Clear();
        _mappings.Clear();
    }

    public void HandleMidiMessage(int deviceIndex, MidiInMessageEventArgs args)
    {
        if (_routing.TryGetValue(deviceIndex, out var synth))
        {
            if (args.MidiEvent is NAudio.Midi.NoteEvent noteEvent)
            {
                if (noteEvent.CommandCode == MidiCommandCode.NoteOn)
                {
                    if (noteEvent.Velocity > 0)
                    {
                        synth.NoteOn(noteEvent.NoteNumber, noteEvent.Velocity);
                    }
                    else
                    {
                        synth.NoteOff(noteEvent.NoteNumber);
                    }
                }
                else if (noteEvent.CommandCode == MidiCommandCode.NoteOff)
                {
                    synth.NoteOff(noteEvent.NoteNumber);
                }
            }
        }

        if (args.MidiEvent is PitchWheelChangeEvent bend)
        {
            float normalized = bend.Pitch / 16383f;
            DispatchControl(deviceIndex, -1, normalized);
        }
        else if (args.MidiEvent is ControlChangeEvent cc)
        {
            float normalized = cc.ControllerValue / 127f;
            DispatchControl(deviceIndex, (int)cc.Controller, normalized);
        }
    }

    private void DispatchControl(int deviceIndex, int controlId, float value)
    {
        for (int i = 0; i < _mappings.Count; i++)
        {
            var mapping = _mappings[i];
            if (mapping.DeviceIndex == deviceIndex && mapping.ControlId == controlId)
            {
                mapping.Action(value);
            }
        }
    }
}
