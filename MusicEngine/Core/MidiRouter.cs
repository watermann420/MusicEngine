// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Shared MIDI routing and control mapping for instruments.

using System;
using System.Collections.Generic;
using System.Linq;
using MusicEngine.Vst;
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
    private readonly Dictionary<int, Dictionary<int, Dictionary<ISynth, MidiRoute>>> _routing = new();
    private readonly Dictionary<int, Dictionary<int, List<MidiPriorityGroup>>> _priorityRouting = new();
    private readonly List<MidiMapping> _mappings = new();
    private readonly object _activityLock = new();
    private readonly Dictionary<int, MidiDeviceActivitySnapshot> _activity = new();
    private readonly object _deviceActiveLock = new();
    private readonly Dictionary<int, long> _deviceActiveTicks = new();
    private readonly HashSet<int> _disabledDevices = new();
    private readonly HashSet<(int Device, int Channel)> _disabledChannels = new();
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
        /// <summary>
        /// Source device index.
        /// </summary>
        public int DeviceIndex { get; init; }
        /// <summary>
        /// Source channel (0-15 or -1).
        /// </summary>
        public int Channel { get; init; }
        /// <summary>
        /// Control change ID.
        /// </summary>
        public int ControlId { get; init; }
        /// <summary>
        /// Callback invoked with normalized values.
        /// </summary>
        public Action<float> Action { get; init; } = null!;
    }

    private sealed class MidiRoute
    {
        /// <summary>
        /// Target synth.
        /// </summary>
        public ISynth Synth { get; }
        /// <summary>
        /// Whether this route is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Create a route to a synth.
        /// </summary>
        public MidiRoute(ISynth synth)
        {
            Synth = synth;
        }
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
            perChannel = new Dictionary<int, Dictionary<ISynth, MidiRoute>>();
            _routing[deviceIndex] = perChannel;
        }

        if (!perChannel.TryGetValue(channel, out var targets))
        {
            targets = new Dictionary<ISynth, MidiRoute>();
            perChannel[channel] = targets;
        }

        if (targets.TryGetValue(synth, out var route))
        {
            route.Enabled = true;
        }
        else
        {
            targets[synth] = new MidiRoute(synth);
        }
    }

    /// <summary>
    /// Create an exclusive priority group (first enabled target wins).
    /// </summary>
    public MidiPriorityGroup CreatePriorityGroup(int deviceIndex, int channel, params ISynth[] synths)
    {
        var group = new MidiPriorityGroup();
        if (synths != null)
        {
            foreach (var synth in synths)
            {
                if (synth == null) continue;
                group.Routes.Add(new MidiPriorityRoute(synth));
            }
        }

        channel = NormalizeChannel(channel);
        if (!_priorityRouting.TryGetValue(deviceIndex, out var perChannel))
        {
            perChannel = new Dictionary<int, List<MidiPriorityGroup>>();
            _priorityRouting[deviceIndex] = perChannel;
        }

        if (!perChannel.TryGetValue(channel, out var groups))
        {
            groups = new List<MidiPriorityGroup>();
            perChannel[channel] = groups;
        }

        groups.Add(group);
        return group;
    }

    /// <summary>
    /// Remove a priority group.
    /// </summary>
    public bool RemovePriorityGroup(int deviceIndex, int channel, MidiPriorityGroup group)
    {
        if (group == null) return false;
        channel = NormalizeChannel(channel);
        if (!_priorityRouting.TryGetValue(deviceIndex, out var perChannel)) return false;
        if (!perChannel.TryGetValue(channel, out var groups)) return false;
        if (!groups.Remove(group)) return false;
        if (groups.Count == 0)
        {
            perChannel.Remove(channel);
        }
        if (perChannel.Count == 0)
        {
            _priorityRouting.Remove(deviceIndex);
        }
        return true;
    }

    /// <summary>
    /// Remove a MIDI route.
    /// </summary>
    /// <param name="deviceIndex">MIDI device index.</param>
    /// <param name="channel">MIDI channel (0-15) or -1 for all.</param>
    /// <param name="synth">Target synth.</param>
    /// <param name="sendAllNotesOff">Send all-notes-off when removing.</param>
    public bool Unroute(int deviceIndex, int channel, ISynth synth, bool sendAllNotesOff = true)
    {
        if (synth == null) return false;
        channel = NormalizeChannel(channel);
        if (!_routing.TryGetValue(deviceIndex, out var perChannel)) return false;
        if (!perChannel.TryGetValue(channel, out var targets)) return false;
        if (!targets.Remove(synth)) return false;

        if (targets.Count == 0)
        {
            perChannel.Remove(channel);
        }
        if (perChannel.Count == 0)
        {
            _routing.Remove(deviceIndex);
        }

        if (sendAllNotesOff)
        {
            synth.AllNotesOff();
        }
        return true;
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
        _priorityRouting.Clear();
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
    /// Enable or disable a specific MIDI device.
    /// </summary>
    public void SetDeviceEnabled(int deviceIndex, bool enabled, bool sendAllNotesOff = true)
    {
        if (enabled)
        {
            _disabledDevices.Remove(deviceIndex);
            return;
        }

        _disabledDevices.Add(deviceIndex);
        if (!sendAllNotesOff) return;
        foreach (var synth in GetRoutedSynths(deviceIndex))
        {
            synth.AllNotesOff();
        }
    }

    /// <summary>
    /// Enable or disable a specific MIDI device channel.
    /// </summary>
    public void SetChannelEnabled(int deviceIndex, int channel, bool enabled, bool sendAllNotesOff = true)
    {
        channel = NormalizeChannel(channel);
        if (channel == AllChannels)
        {
            SetDeviceEnabled(deviceIndex, enabled, sendAllNotesOff);
            return;
        }

        var key = (deviceIndex, channel);
        if (enabled)
        {
            _disabledChannels.Remove(key);
            return;
        }

        _disabledChannels.Add(key);
        if (!sendAllNotesOff) return;
        foreach (var synth in GetTargets(deviceIndex, channel, includeDisabledRoutes: true))
        {
            synth.AllNotesOff();
        }
    }

    /// <summary>
    /// Enable or disable a specific routed synth on a device/channel.
    /// </summary>
    public void SetRouteEnabled(int deviceIndex, int channel, ISynth synth, bool enabled, bool sendAllNotesOff = true)
    {
        if (synth == null) return;
        channel = NormalizeChannel(channel);
        if (!_routing.TryGetValue(deviceIndex, out var perChannel)) return;
        if (!perChannel.TryGetValue(channel, out var routes)) return;
        if (!routes.TryGetValue(synth, out var route)) return;

        route.Enabled = enabled;
        if (!enabled && sendAllNotesOff)
        {
            synth.AllNotesOff();
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
        if (_disabledDevices.Contains(deviceIndex))
        {
            return;
        }

        var channel = TryGetChannel(args);
        if (channel.HasValue && _disabledChannels.Contains((deviceIndex, channel.Value)))
        {
            return;
        }

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
            var normalizedChannel = Math.Clamp(noteEvent.Channel, 0, 15);
            foreach (var synth in GetTargets(deviceIndex, normalizedChannel))
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
        => GetTargets(deviceIndex, channel, includeDisabledRoutes: false);

    private IEnumerable<ISynth> GetTargets(int deviceIndex, int channel, bool includeDisabledRoutes)
    {
        HashSet<ISynth>? any = null;

        if (_routing.TryGetValue(deviceIndex, out var perChannel))
        {
            if (perChannel.TryGetValue(AllChannels, out var anyTargets))
            {
                any = new HashSet<ISynth>();
                foreach (var route in anyTargets.Values)
                {
                    if (route.Enabled || includeDisabledRoutes)
                    {
                        any.Add(route.Synth);
                        yield return route.Synth;
                    }
                }
            }

            if (perChannel.TryGetValue(channel, out var channelTargets))
            {
                foreach (var route in channelTargets.Values)
                {
                    if ((route.Enabled || includeDisabledRoutes) && (any == null || !any.Contains(route.Synth)))
                    {
                        yield return route.Synth;
                    }
                }
            }
        }

        if (!_priorityRouting.TryGetValue(deviceIndex, out var priorityPerChannel)) yield break;

        if (priorityPerChannel.TryGetValue(AllChannels, out var anyGroups))
        {
            foreach (var target in GetPriorityTargets(anyGroups, includeDisabledRoutes))
            {
                if (any == null) any = new HashSet<ISynth>();
                if (any.Add(target))
                {
                    yield return target;
                }
            }
        }

        if (priorityPerChannel.TryGetValue(channel, out var channelGroups))
        {
            foreach (var target in GetPriorityTargets(channelGroups, includeDisabledRoutes))
            {
                if (any == null || any.Add(target))
                {
                    yield return target;
                }
            }
        }
    }

    private static IEnumerable<ISynth> GetPriorityTargets(List<MidiPriorityGroup> groups, bool includeDisabledRoutes)
    {
        foreach (var group in groups)
        {
            if (group == null) continue;
            for (int i = 0; i < group.Routes.Count; i++)
            {
                var route = group.Routes[i];
                if (route == null) continue;
                if (route.Enabled || includeDisabledRoutes)
                {
                    if (route.Synth is MissingVstInstrument)
                    {
                        continue;
                    }
                    yield return route.Synth;
                    break;
                }
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
                foreach (var route in targets.Values)
                {
                    if (seen.Add(route.Synth))
                    {
                        yield return route.Synth;
                    }
                }
            }
        }
    }

    private IEnumerable<ISynth> GetRoutedSynths(int deviceIndex)
    {
        if (!_routing.TryGetValue(deviceIndex, out var perChannel)) yield break;
        var seen = new HashSet<ISynth>();
        foreach (var targets in perChannel.Values)
        {
            foreach (var route in targets.Values)
            {
                if (seen.Add(route.Synth))
                {
                    yield return route.Synth;
                }
            }
        }
    }

    private static int? TryGetChannel(MidiInMessageEventArgs args)
    {
        return args.MidiEvent switch
        {
            NAudio.Midi.NoteEvent note => Math.Clamp(note.Channel, 0, 15),
            PitchWheelChangeEvent bend => Math.Clamp(bend.Channel, 0, 15),
            ControlChangeEvent cc => Math.Clamp(cc.Channel, 0, 15),
            _ => null
        };
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
