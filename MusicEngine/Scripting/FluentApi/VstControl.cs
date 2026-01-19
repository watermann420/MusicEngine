//Engine License (MEL) – Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Fluent API for VST plugin control operations.


using MusicEngine.Core;


namespace MusicEngine.Scripting.FluentApi;


// === VST Fluent API ===

// Main VST control access
public class VstControl
{
    private readonly ScriptGlobals _globals;
    public VstControl(ScriptGlobals globals) => _globals = globals;

    // Load a VST plugin by name or path
    public VstPluginControl? load(string nameOrPath)
    {
        var plugin = _globals.LoadVst(nameOrPath);
        return plugin != null ? new VstPluginControl(_globals, plugin) : null;
    }

    // Load a VST plugin by index
    public VstPluginControl? load(int index)
    {
        var plugin = _globals.LoadVstByIndex(index);
        return plugin != null ? new VstPluginControl(_globals, plugin) : null;
    }

    // Get a loaded VST plugin by name
    public VstPluginControl? get(string name)
    {
        var plugin = _globals.GetVst(name);
        return plugin != null ? new VstPluginControl(_globals, plugin) : null;
    }

    // List all discovered VST plugins
    public void list() => _globals.ListVstPlugins();

    // List all loaded VST plugins
    public void loaded() => _globals.ListLoadedVstPlugins();

    // Access VST plugin by name for fluent chaining
    public VstPluginControl? plugin(string name) => get(name);
}


// Control for a specific VST plugin
public class VstPluginControl
{
    private readonly ScriptGlobals _globals;
    private readonly VstPlugin _plugin;

    public VstPluginControl(ScriptGlobals globals, VstPlugin plugin)
    {
        _globals = globals;
        _plugin = plugin;
    }

    // Get the underlying plugin
    public VstPlugin Plugin => _plugin;

    // Route MIDI input to this plugin
    public VstPluginControl from(int deviceIndex)
    {
        _globals.RouteToVst(deviceIndex, _plugin);
        return this;
    }

    // Route MIDI input by device name
    public VstPluginControl from(string deviceName)
    {
        int index = _globals.Engine.GetMidiDeviceIndex(deviceName);
        if (index >= 0) _globals.RouteToVst(index, _plugin);
        return this;
    }

    // Set a parameter by name
    public VstPluginControl param(string name, float value)
    {
        _plugin.SetParameter(name, value);
        return this;
    }

    // Set a parameter by index
    public VstPluginControl param(int index, float value)
    {
        _plugin.SetParameterByIndex(index, value);
        return this;
    }

    // Set volume/gain
    public VstPluginControl volume(float value)
    {
        _plugin.SetParameter("volume", value);
        return this;
    }

    // Send a note on
    public VstPluginControl noteOn(int note, int velocity = 100)
    {
        _plugin.NoteOn(note, velocity);
        return this;
    }

    // Send a note off
    public VstPluginControl noteOff(int note)
    {
        _plugin.NoteOff(note);
        return this;
    }

    // Send all notes off
    public VstPluginControl allNotesOff()
    {
        _plugin.AllNotesOff();
        return this;
    }

    // Send control change
    public VstPluginControl cc(int controller, int value, int channel = 0)
    {
        _plugin.SendControlChange(channel, controller, value);
        return this;
    }

    // Send program change
    public VstPluginControl program(int programNumber, int channel = 0)
    {
        _plugin.SendProgramChange(channel, programNumber);
        return this;
    }

    // Send pitch bend
    public VstPluginControl pitchBend(int value, int channel = 0)
    {
        _plugin.SendPitchBend(channel, value);
        return this;
    }

    // Implicit conversion to VstPlugin for direct use
    public static implicit operator VstPlugin(VstPluginControl control) => control._plugin;
}
