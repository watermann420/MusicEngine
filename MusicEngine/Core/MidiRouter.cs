// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Shared MIDI routing and control mapping for instruments.

using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.Midi;

namespace MusicEngine.Core;

/// <summary>
/// Shared MIDI routing and control mapping for instruments.
/// </summary>
public sealed class MidiRouter
{
    private const int AllChannels = -1;
    /// <summary>
    /// Whether MIDI routing is currently enabled.
    /// </summary>
    public bool Enabled { get; private set; } = true;
    private readonly Dictionary<int, Dictionary<int, HashSet<ISynth>>> _routing = new();
    private readonly List<MidiMapping> _mappings = new();
    private readonly object _activityLock = new();
    private readonly Dictionary<int, MidiDeviceActivitySnapshot> _activity = new();
    private readonly object _deviceActiveLock = new();
    private readonly Dictionary<int, long> _deviceActiveTicks = new();
    private bool _editorModeEnabled;
    private const int DeviceActiveDebounceMs = 100;

    /// <summary>
    /// Raised when a note event arrives while editor mode is enabled.
    /// </summary>
    public event Action<MidiNoteEventInfo>? EditorMidiNoteEvent;

    /// <summary>
    /// Raised when a device becomes active while editor mode is enabled.
    /// </summary>
    public event Action<int>? EditorMidiDeviceActive;

    private sealed class MidiMapping
    {
        public int DeviceIndex { get; init; }
        public int Channel { get; init; }
        public int ControlId { get; init; }
        public Action<float> Action { get; init; } = null!;
    }

    /// <summary>
    /// Route a MIDI device to a synth.
    /// </summary>
    /// <param name="deviceIndex">MIDI device index.</param>
    /// <param name="synth">Target synth.</param>
    public void Route(int deviceIndex, ISynth synth) => Route(deviceIndex, AllChannels, synth);

    /// <summary>
    /// Route a MIDI device channel to a synth.
    /// </summary>
    /// <param name="deviceIndex">MIDI device index.</param>
    /// <param name="channel">MIDI channel (0-15) or -1 for all.</param>
    /// <param name="synth">Target synth.</param>
    public void Route(int deviceIndex, int channel, ISynth synth)
    {
        if (synth == null) return;
        channel = NormalizeChannel(channel);
        if (!_routing.TryGetValue(deviceIndex, out var perChannel))
        {
            perChannel = new Dictionary<int, HashSet<ISynth>>();
            _routing[deviceIndex] = perChannel;
        }

        if (!perChannel.TryGetValue(channel, out var targets))
        {
            targets = new HashSet<ISynth>();
            perChannel[channel] = targets;
        }

        targets.Add(synth);
    }

    /// <summary>
    /// Map a control change to a custom action.
    /// </summary>
    /// <param name="deviceIndex">MIDI device index.</param>
    /// <param name="controlId">Control change ID.</param>
    /// <param name="action">Action invoked with normalized value.</param>
    public void MapControlAction(int deviceIndex, int controlId, Action<float> action)
    {
        MapControlAction(deviceIndex, AllChannels, controlId, action);
    }

    /// <summary>
    /// Map a control change to a custom action for a specific channel.
    /// </summary>
    /// <param name="deviceIndex">MIDI device index.</param>
    /// <param name="channel">MIDI channel (0-15) or -1 for all.</param>
    /// <param name="controlId">Control change ID.</param>
    /// <param name="action">Action invoked with normalized value.</param>
    public void MapControlAction(int deviceIndex, int channel, int controlId, Action<float> action)
    {
        _mappings.Add(new MidiMapping
        {
            DeviceIndex = deviceIndex,
            Channel = NormalizeChannel(channel),
            ControlId = controlId,
            Action = action
        });
    }

    /// <summary>
    /// Clear all routes and control mappings.
    /// </summary>
    public void Clear()
    {
        _routing.Clear();
        _mappings.Clear();
    }

    /// <summary>
    /// Enable or disable MIDI processing.
    /// </summary>
    /// <param name="enabled">True to enable.</param>
    /// <param name="sendAllNotesOff">Send all-notes-off on disable.</param>
    public void SetEnabled(bool enabled, bool sendAllNotesOff = true)
    {
        if (Enabled == enabled) return;
        Enabled = enabled;
        if (!Enabled && sendAllNotesOff)
        {
            foreach (var synth in GetRoutedSynths())
            {
                synth.AllNotesOff();
            }
        }
    }

    /// <summary>
    /// Enable or disable editor mode events.
    /// </summary>
    public void SetEditorMode(bool enabled)
    {
        _editorModeEnabled = enabled;
    }

    /// <summary>
    /// Process a MIDI message for routing, mapping, and editor events.
    /// </summary>
    /// <param name="deviceIndex">MIDI device index.</param>
    /// <param name="args">Incoming MIDI message event args.</param>
    public void HandleMidiMessage(int deviceIndex, MidiInMessageEventArgs args)
    {
        UpdateActivity(deviceIndex, args);
        if (_editorModeEnabled)
        {
            TryEmitDeviceActive(deviceIndex);
            TryEmitNoteEvent(deviceIndex, args);
        }
        if (!Enabled)
        {
            return;
        }

        if (args.MidiEvent is NAudio.Midi.NoteEvent noteEvent)
        {
            var channel = Math.Clamp(noteEvent.Channel, 0, 15);
            foreach (var synth in GetTargets(deviceIndex, channel))
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
            DispatchControl(deviceIndex, Math.Clamp(bend.Channel, 0, 15), -1, normalized);
        }
        else if (args.MidiEvent is ControlChangeEvent cc)
        {
            float normalized = cc.ControllerValue / 127f;
            DispatchControl(deviceIndex, Math.Clamp(cc.Channel, 0, 15), (int)cc.Controller, normalized);
        }
    }

    private void DispatchControl(int deviceIndex, int channel, int controlId, float value)
    {
        UpdateControlActivity(deviceIndex, controlId, value);

        for (int i = 0; i < _mappings.Count; i++)
        {
            var mapping = _mappings[i];
            if (mapping.DeviceIndex == deviceIndex &&
                (mapping.Channel == AllChannels || mapping.Channel == channel) &&
                mapping.ControlId == controlId)
            {
                mapping.Action(value);
            }
        }
    }

    private void TryEmitDeviceActive(int deviceIndex)
    {
        var nowTicks = DateTime.UtcNow.Ticks;
        bool shouldEmit = false;
        lock (_deviceActiveLock)
        {
            if (!_deviceActiveTicks.TryGetValue(deviceIndex, out var lastTicks) ||
                nowTicks - lastTicks >= TimeSpan.FromMilliseconds(DeviceActiveDebounceMs).Ticks)
            {
                _deviceActiveTicks[deviceIndex] = nowTicks;
                shouldEmit = true;
            }
        }

        if (!shouldEmit) return;
        var handler = EditorMidiDeviceActive;
        if (handler == null) return;
        try
        {
            handler(deviceIndex);
        }
        catch
        {
        }
    }

    private void TryEmitNoteEvent(int deviceIndex, MidiInMessageEventArgs args)
    {
        if (args.MidiEvent is not NAudio.Midi.NoteEvent noteEvent) return;

        bool isOn;
        int velocity;
        if (noteEvent.CommandCode == MidiCommandCode.NoteOn)
        {
            isOn = noteEvent.Velocity > 0;
            velocity = noteEvent.Velocity;
        }
        else if (noteEvent.CommandCode == MidiCommandCode.NoteOff)
        {
            isOn = false;
            velocity = 0;
        }
        else
        {
            return;
        }

        var handler = EditorMidiNoteEvent;
        if (handler == null) return;
        var info = new MidiNoteEventInfo(deviceIndex, noteEvent.NoteNumber, velocity, isOn, DateTime.UtcNow);
        try
        {
            handler(info);
        }
        catch
        {
        }
    }

    /// <summary>
    /// Snapshot of recent MIDI device activity.
    /// </summary>
    /// <returns>Sorted list of device activity snapshots.</returns>
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

    private static int NormalizeChannel(int channel)
    {
        if (channel < 0) return AllChannels;
        return Math.Clamp(channel, 0, 15);
    }

    private IEnumerable<ISynth> GetTargets(int deviceIndex, int channel)
    {
        if (!_routing.TryGetValue(deviceIndex, out var perChannel)) yield break;

        HashSet<ISynth>? any = null;
        if (perChannel.TryGetValue(AllChannels, out var anyTargets))
        {
            any = anyTargets;
            foreach (var synth in anyTargets)
            {
                yield return synth;
            }
        }

        if (!perChannel.TryGetValue(channel, out var channelTargets)) yield break;
        foreach (var synth in channelTargets)
        {
            if (any == null || !any.Contains(synth))
            {
                yield return synth;
            }
        }
    }

    private IEnumerable<ISynth> GetRoutedSynths()
    {
        var seen = new HashSet<ISynth>();
        foreach (var perChannel in _routing.Values)
        {
            foreach (var targets in perChannel.Values)
            {
                foreach (var synth in targets)
                {
                    if (seen.Add(synth))
                    {
                        yield return synth;
                    }
                }
            }
        }
    }
}

/// <summary>
/// Snapshot of the last activity seen for a MIDI device.
/// </summary>
public sealed class MidiDeviceActivitySnapshot
{
    /// <summary>
    /// MIDI device index.
    /// </summary>
    public int DeviceIndex { get; init; }
    /// <summary>
    /// UTC timestamp of the last message.
    /// </summary>
    public DateTime LastMessageUtc { get; init; }
    /// <summary>
    /// String description of the last message type.
    /// </summary>
    public string LastMessageType { get; init; } = string.Empty;
    /// <summary>
    /// Last MIDI note number, if applicable.
    /// </summary>
    public int? LastNote { get; init; }
    /// <summary>
    /// Last velocity value, if applicable.
    /// </summary>
    public int? LastVelocity { get; init; }
    /// <summary>
    /// Last control ID, if applicable.
    /// </summary>
    public int? LastControlId { get; init; }
    /// <summary>
    /// Last control value, normalized to 0..1 where possible.
    /// </summary>
    public float? LastControlValue { get; init; }
}

/// <summary>
/// Lightweight note event data for editor feedback.
/// </summary>
public readonly struct MidiNoteEventInfo
{
    /// <summary>
    /// MIDI device index.
    /// </summary>
    public int DeviceIndex { get; }
    /// <summary>
    /// MIDI note number.
    /// </summary>
    public int Note { get; }
    /// <summary>
    /// Velocity value.
    /// </summary>
    public int Velocity { get; }
    /// <summary>
    /// True when this is a note-on event.
    /// </summary>
    public bool IsOn { get; }
    /// <summary>
    /// UTC timestamp of the event.
    /// </summary>
    public DateTime TimestampUtc { get; }

    /// <summary>
    /// Create a new note event info record.
    /// </summary>
    public MidiNoteEventInfo(int deviceIndex, int note, int velocity, bool isOn, DateTime timestampUtc)
    {
        DeviceIndex = deviceIndex;
        Note = note;
        Velocity = velocity;
        IsOn = isOn;
        TimestampUtc = timestampUtc;
    }
}
