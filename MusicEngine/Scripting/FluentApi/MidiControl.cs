// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Minimal MIDI control fluent API.

using System;
using MusicEngine.Core;
using MusicEngine.Effects.Midi;
using MusicEngine.Scripting;

namespace MusicEngine.Scripting.FluentApi;

/// <summary>
/// Fluent API entry point for MIDI routing and mapping.
/// </summary>
public sealed class MidiControl
{
    private readonly ScriptGlobals _globals;
    public MidiControl(ScriptGlobals globals) => _globals = globals;

    /// <summary>
    /// Shared MIDI mapping helper.
    /// </summary>
    public MidiMap Map => _globals.MidiMap;

    /// <summary>
    /// Shared MIDI mapping helper.
    /// </summary>
    public MidiMap map => _globals.MidiMap;

    /// <summary>
    /// Access a MIDI device by index.
    /// </summary>
    public DeviceControl device(int index) => new DeviceControl(_globals, index);

    /// <summary>
    /// Access a MIDI device by index.
    /// </summary>
    public DeviceControl Device(int index) => new DeviceControl(_globals, index);

    /// <summary>
    /// Access a MIDI device by index.
    /// </summary>
    public DeviceControl Divice(int index) => new DeviceControl(_globals, index);
}

/// <summary>
/// MIDI device routing and control mapping.
/// </summary>
public sealed class DeviceControl
{
    private readonly ScriptGlobals _globals;
    private readonly int _deviceIndex;

    public DeviceControl(ScriptGlobals globals, int deviceIndex)
    {
        _globals = globals;
        _deviceIndex = deviceIndex;
    }

    /// <summary>
    /// Route the device to a synth.
    /// </summary>
    public MidiSend to(ISynth synth)
    {
        _globals.RouteMidi(_deviceIndex, synth);
        return new MidiSend(_deviceIndex, -1, synth, _globals.Engine);
    }
    /// <summary>
    /// Route the device to a synth.
    /// </summary>
    public MidiSend To(ISynth synth) => to(synth);
    /// <summary>
    /// Route the device to a synth.
    /// </summary>
    public MidiSend TO(ISynth synth) => to(synth);

    /// <summary>
    /// Access a specific MIDI channel on this device.
    /// </summary>
    public ChannelControl channel(int channel) => new ChannelControl(_globals, _deviceIndex, channel);

    /// <summary>
    /// Access a specific MIDI channel on this device.
    /// </summary>
    public ChannelControl Channel(int channel) => new ChannelControl(_globals, _deviceIndex, channel);

    /// <summary>
    /// Enable or disable this device.
    /// </summary>
    public void Active(bool enabled, bool sendAllNotesOff = true)
        => _globals.SetMidiDeviceEnabled(_deviceIndex, enabled, sendAllNotesOff);

    /// <summary>
    /// Enable or disable this device.
    /// </summary>
    public void active(bool enabled, bool sendAllNotesOff = true) => Active(enabled, sendAllNotesOff);

    /// <summary>
    /// Enable this device.
    /// </summary>
    public void Enable(bool sendAllNotesOff = true) => Active(true, sendAllNotesOff);

    /// <summary>
    /// Disable this device.
    /// </summary>
    public void Disable(bool sendAllNotesOff = true) => Active(false, sendAllNotesOff);

    /// <summary>
    /// Map pitch bend to a control action.
    /// </summary>
    public ControlMapping pitchbend() => new ControlMapping(_globals, _deviceIndex, -1);

    /// <summary>
    /// Map pitch bend to a control action.
    /// </summary>
    public ControlMapping Pitchbend() => new ControlMapping(_globals, _deviceIndex, -1);

    /// <summary>
    /// Map pitch bend to a control action.
    /// </summary>
    public ControlMapping PitchBend() => new ControlMapping(_globals, _deviceIndex, -1);

    /// <summary>
    /// Map a control change ID to a control action.
    /// </summary>
    public ControlMapping control(int controlId) => new ControlMapping(_globals, _deviceIndex, controlId);

    /// <summary>
    /// Map a control change ID to a control action.
    /// </summary>
    public ControlMapping Control(int controlId) => new ControlMapping(_globals, _deviceIndex, controlId);

    /// <summary>
    /// Alias for Control (CC).
    /// </summary>
    public ControlMapping cc(int controlId) => control(controlId);

    /// <summary>
    /// Alias for Control (CC).
    /// </summary>
    public ControlMapping CC(int controlId) => control(controlId);

    /// <summary>
    /// Map a jog wheel control to delta ticks.
    /// </summary>
    public JogControl jog(int controlId, JogMode mode = JogMode.RelativeSigned, int scale = 1)
        => new JogControl(_globals, _deviceIndex, controlId, mode, scale);

    /// <summary>
    /// Map a jog wheel control to delta ticks.
    /// </summary>
    public JogControl Jog(int controlId, JogMode mode = JogMode.RelativeSigned, int scale = 1)
        => new JogControl(_globals, _deviceIndex, controlId, mode, scale);

    /// <summary>
    /// Map a jog wheel control using a named mapping.
    /// </summary>
    public JogControl jog(MidiMap map, string name, JogMode fallbackMode = JogMode.RelativeSigned, int fallbackScale = 1)
    {
        if (map.TryGetJog(name, out var jog))
        {
            return new JogControl(_globals, _deviceIndex, jog.ControlId, jog.Mode, jog.Scale);
        }
        var controlId = map.Get(name);
        return new JogControl(_globals, _deviceIndex, controlId, fallbackMode, fallbackScale);
    }

    /// <summary>
    /// Map a jog wheel control using a named mapping.
    /// </summary>
    public JogControl Jog(MidiMap map, string name, JogMode fallbackMode = JogMode.RelativeSigned, int fallbackScale = 1)
        => jog(map, name, fallbackMode, fallbackScale);
}

/// <summary>
/// MIDI channel routing and control mapping.
/// </summary>
public sealed class ChannelControl
{
    private readonly ScriptGlobals _globals;
    private readonly int _deviceIndex;
    private readonly int _channel;

    public ChannelControl(ScriptGlobals globals, int deviceIndex, int channel)
    {
        _globals = globals;
        _deviceIndex = deviceIndex;
        _channel = channel;
    }

    /// <summary>
    /// Route the device channel to a synth.
    /// </summary>
    public MidiSend to(ISynth synth)
    {
        _globals.RouteMidi(_deviceIndex, _channel, synth);
        return new MidiSend(_deviceIndex, _channel, synth, _globals.Engine);
    }
    /// <summary>
    /// Route the device channel to a synth.
    /// </summary>
    public MidiSend To(ISynth synth) => to(synth);
    /// <summary>
    /// Route the device channel to a synth.
    /// </summary>
    public MidiSend TO(ISynth synth) => to(synth);

    /// <summary>
    /// Map pitch bend to a control action.
    /// </summary>
    public ControlMapping pitchbend() => new ControlMapping(_globals, _deviceIndex, -1, _channel);

    /// <summary>
    /// Map pitch bend to a control action.
    /// </summary>
    public ControlMapping Pitchbend() => new ControlMapping(_globals, _deviceIndex, -1, _channel);

    /// <summary>
    /// Map pitch bend to a control action.
    /// </summary>
    public ControlMapping PitchBend() => new ControlMapping(_globals, _deviceIndex, -1, _channel);

    /// <summary>
    /// Map a control change ID to a control action.
    /// </summary>
    public ControlMapping control(int controlId) => new ControlMapping(_globals, _deviceIndex, controlId, _channel);

    /// <summary>
    /// Map a control change ID to a control action.
    /// </summary>
    public ControlMapping Control(int controlId) => new ControlMapping(_globals, _deviceIndex, controlId, _channel);

    /// <summary>
    /// Alias for Control (CC).
    /// </summary>
    public ControlMapping cc(int controlId) => control(controlId);

    /// <summary>
    /// Alias for Control (CC).
    /// </summary>
    public ControlMapping CC(int controlId) => control(controlId);

    /// <summary>
    /// Map a jog wheel control to delta ticks.
    /// </summary>
    public JogControl jog(int controlId, JogMode mode = JogMode.RelativeSigned, int scale = 1)
        => new JogControl(_globals, _deviceIndex, controlId, mode, scale, _channel);

    /// <summary>
    /// Map a jog wheel control to delta ticks.
    /// </summary>
    public JogControl Jog(int controlId, JogMode mode = JogMode.RelativeSigned, int scale = 1)
        => new JogControl(_globals, _deviceIndex, controlId, mode, scale, _channel);

    /// <summary>
    /// Map a jog wheel control using a named mapping.
    /// </summary>
    public JogControl jog(MidiMap map, string name, JogMode fallbackMode = JogMode.RelativeSigned, int fallbackScale = 1)
    {
        if (map.TryGetJog(name, out var jog))
        {
            return new JogControl(_globals, _deviceIndex, jog.ControlId, jog.Mode, jog.Scale, _channel);
        }
        var controlId = map.Get(name);
        return new JogControl(_globals, _deviceIndex, controlId, fallbackMode, fallbackScale, _channel);
    }

    /// <summary>
    /// Map a jog wheel control using a named mapping.
    /// </summary>
    public JogControl Jog(MidiMap map, string name, JogMode fallbackMode = JogMode.RelativeSigned, int fallbackScale = 1)
        => jog(map, name, fallbackMode, fallbackScale);

    /// <summary>
    /// Enable or disable this device channel.
    /// </summary>
    public void Active(bool enabled, bool sendAllNotesOff = true)
        => _globals.SetMidiChannelEnabled(_deviceIndex, _channel, enabled, sendAllNotesOff);

    /// <summary>
    /// Enable or disable this device channel.
    /// </summary>
    public void active(bool enabled, bool sendAllNotesOff = true) => Active(enabled, sendAllNotesOff);

    /// <summary>
    /// Enable this device channel.
    /// </summary>
    public void Enable(bool sendAllNotesOff = true) => Active(true, sendAllNotesOff);

    /// <summary>
    /// Disable this device channel.
    /// </summary>
    public void Disable(bool sendAllNotesOff = true) => Active(false, sendAllNotesOff);
}

/// <summary>
/// Jog wheel message interpretation.
/// </summary>
public enum JogMode
{
    RelativeSigned,
    RelativeBinaryOffset,
    Absolute
}

/// <summary>
/// Jog wheel mapping helper.
/// </summary>
public sealed class JogControl
{
    private readonly ScriptGlobals _globals;
    private readonly int _deviceIndex;
    private readonly int _controlId;
    private readonly int _channel;
    private readonly JogMode _mode;
    private readonly int _scale;
    private int? _lastRaw;

    public JogControl(ScriptGlobals globals, int deviceIndex, int controlId, JogMode mode, int scale, int channel = -1)
    {
        _globals = globals;
        _deviceIndex = deviceIndex;
        _controlId = controlId;
        _mode = mode;
        _scale = Math.Max(1, scale);
        _channel = channel;
    }

    /// <summary>
    /// Map the jog wheel to a delta tick callback.
    /// </summary>
    public void to(Action<int> onDelta)
    {
        _globals.MapControlAction(_deviceIndex, _channel, _controlId, value =>
        {
            var raw = (int)Math.Round(Math.Clamp(value, 0f, 1f) * 127f);
            int delta = _mode switch
            {
                JogMode.Absolute => AbsoluteDelta(raw),
                JogMode.RelativeBinaryOffset => raw - 64,
                _ => RelativeSignedDelta(raw)
            };

            if (delta != 0)
            {
                onDelta(delta * _scale);
            }
        });
    }

    private int AbsoluteDelta(int raw)
    {
        if (_lastRaw == null)
        {
            _lastRaw = raw;
            return 0;
        }

        int delta = raw - _lastRaw.Value;
        _lastRaw = raw;
        return delta;
    }

    private static int RelativeSignedDelta(int raw)
    {
        if (raw == 64 || raw == 0) return 0;
        if (raw > 64) return raw - 128;
        return raw;
    }
}

/// <summary>
/// Control mapping configuration.
/// </summary>
public sealed class ControlMapping
{
    private readonly ScriptGlobals _globals;
    private readonly int _deviceIndex;
    private readonly int _controlId;
    private readonly int _channel;

    public ControlMapping(ScriptGlobals globals, int deviceIndex, int controlId, int channel = -1)
    {
        _globals = globals;
        _deviceIndex = deviceIndex;
        _controlId = controlId;
        _channel = channel;
    }

    /// <summary>
    /// Map the control to an action.
    /// </summary>
    public void to(Action<float> action) => _globals.MapControlAction(_deviceIndex, _channel, _controlId, action);
    /// <summary>
    /// Map the control to an action.
    /// </summary>
    public void To(Action<float> action) => to(action);
    /// <summary>
    /// Map the control to an action.
    /// </summary>
    public void TO(Action<float> action) => to(action);
}
