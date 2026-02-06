// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Shared MIDI routing and control mapping for instruments.

using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.Midi;

namespace MusicEngine.Core;

public sealed class MidiRouter
{
    public bool Enabled { get; private set; } = true;
    private readonly Dictionary<int, ISynth> _routing = new();
    private readonly List<MidiMapping> _mappings = new();
    private readonly object _activityLock = new();
    private readonly Dictionary<int, MidiDeviceActivitySnapshot> _activity = new();

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

    public void SetEnabled(bool enabled, bool sendAllNotesOff = true)
    {
        if (Enabled == enabled) return;
        Enabled = enabled;
        if (!Enabled && sendAllNotesOff)
        {
            foreach (var synth in _routing.Values)
            {
                synth.AllNotesOff();
            }
        }
    }

    public void HandleMidiMessage(int deviceIndex, MidiInMessageEventArgs args)
    {
        UpdateActivity(deviceIndex, args);
        if (!Enabled)
        {
            return;
        }

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
        UpdateControlActivity(deviceIndex, controlId, value);

        for (int i = 0; i < _mappings.Count; i++)
        {
            var mapping = _mappings[i];
            if (mapping.DeviceIndex == deviceIndex && mapping.ControlId == controlId)
            {
                mapping.Action(value);
            }
        }
    }

    public IReadOnlyList<MidiDeviceActivitySnapshot> GetActivitySnapshot()
    {
        lock (_activityLock)
        {
            if (_activity.Count == 0) return Array.Empty<MidiDeviceActivitySnapshot>();
            return _activity.Values.OrderBy(entry => entry.DeviceIndex).ToArray();
        }
    }

    private void UpdateActivity(int deviceIndex, MidiInMessageEventArgs args)
    {
        var nowUtc = DateTime.UtcNow;
        int? note = null;
        int? velocity = null;
        int? controlId = null;
        float? controlValue = null;
        string messageType = args.MidiEvent?.CommandCode.ToString() ?? "Unknown";

        if (args.MidiEvent is NAudio.Midi.NoteEvent noteEvent)
        {
            note = noteEvent.NoteNumber;
            velocity = noteEvent.Velocity;
        }
        else if (args.MidiEvent is PitchWheelChangeEvent bend)
        {
            controlId = -1;
            controlValue = bend.Pitch / 16383f;
        }
        else if (args.MidiEvent is ControlChangeEvent cc)
        {
            controlId = (int)cc.Controller;
            controlValue = cc.ControllerValue / 127f;
        }

        lock (_activityLock)
        {
            _activity[deviceIndex] = new MidiDeviceActivitySnapshot
            {
                DeviceIndex = deviceIndex,
                LastMessageUtc = nowUtc,
                LastMessageType = messageType,
                LastNote = note,
                LastVelocity = velocity,
                LastControlId = controlId,
                LastControlValue = controlValue
            };
        }
    }

    private void UpdateControlActivity(int deviceIndex, int controlId, float value)
    {
        var nowUtc = DateTime.UtcNow;
        lock (_activityLock)
        {
            _activity[deviceIndex] = new MidiDeviceActivitySnapshot
            {
                DeviceIndex = deviceIndex,
                LastMessageUtc = nowUtc,
                LastMessageType = "Control",
                LastControlId = controlId,
                LastControlValue = value
            };
        }
    }
}

public sealed class MidiDeviceActivitySnapshot
{
    public int DeviceIndex { get; init; }
    public DateTime LastMessageUtc { get; init; }
    public string LastMessageType { get; init; } = string.Empty;
    public int? LastNote { get; init; }
    public int? LastVelocity { get; init; }
    public int? LastControlId { get; init; }
    public float? LastControlValue { get; init; }
}
