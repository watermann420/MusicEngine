// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Minimal MIDI control fluent API.

using System;
using MusicEngine.Core;

namespace MusicEngine.Scripting.FluentApi;

public sealed class MidiControl
{
    private readonly ScriptGlobals _globals;
    public MidiControl(ScriptGlobals globals) => _globals = globals;

    public DeviceControl device(int index) => new DeviceControl(_globals, index);
}

public sealed class DeviceControl
{
    private readonly ScriptGlobals _globals;
    private readonly int _deviceIndex;

    public DeviceControl(ScriptGlobals globals, int deviceIndex)
    {
        _globals = globals;
        _deviceIndex = deviceIndex;
    }

    public void to(ISynth synth) => _globals.RouteMidi(_deviceIndex, synth);

    public ControlMapping pitchbend() => new ControlMapping(_globals, _deviceIndex, -1);

    public ControlMapping control(int controlId) => new ControlMapping(_globals, _deviceIndex, controlId);
}

public sealed class ControlMapping
{
    private readonly ScriptGlobals _globals;
    private readonly int _deviceIndex;
    private readonly int _controlId;

    public ControlMapping(ScriptGlobals globals, int deviceIndex, int controlId)
    {
        _globals = globals;
        _deviceIndex = deviceIndex;
        _controlId = controlId;
    }

    public void to(Action<float> action) => _globals.MapControlAction(_deviceIndex, _controlId, action);
}
