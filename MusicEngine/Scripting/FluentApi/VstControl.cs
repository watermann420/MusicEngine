// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.IO;
using MusicEngine.Core;


namespace MusicEngine.Scripting.FluentApi;


// === VST Fluent API ===

// Main VST control access
public class VstControl
{
    private readonly ScriptGlobals _globals;
    public VstControl(ScriptGlobals globals) => _globals = globals;

    // Load a VST plugin by name or path (auto-detects VST3 by .vst3 extension)
    public VstPluginControl? load(string nameOrPath)
    {
        // Auto-detect VST3 by extension
        bool isVst3 = nameOrPath.EndsWith(".vst3", StringComparison.OrdinalIgnoreCase) ||
                      Directory.Exists(nameOrPath) && nameOrPath.EndsWith(".vst3", StringComparison.OrdinalIgnoreCase);

        var plugin = _globals.LoadVst(nameOrPath);
        return plugin != null ? new VstPluginControl(_globals, plugin) : null;
    }

    // Load a VST plugin by index
    public VstPluginControl? load(int index)
    {
        var plugin = _globals.LoadVstByIndex(index);
        return plugin != null ? new VstPluginControl(_globals, plugin) : null;
    }

    /// <summary>
    /// Explicitly load a VST3 plugin by path.
    /// Use this when you want to ensure a plugin is loaded as VST3.
    /// </summary>
    /// <param name="path">Path to the .vst3 file or bundle</param>
    /// <returns>VstPluginControl wrapper if successful, null otherwise</returns>
    public VstPluginControl? loadVst3(string path)
    {
        if (!path.EndsWith(".vst3", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Warning: Path does not have .vst3 extension: {path}");
        }

        var plugin = _globals.LoadVst(path);
        return plugin != null ? new VstPluginControl(_globals, plugin) : null;
    }

    /// <summary>Alias for loadVst3 - PascalCase version</summary>
    public VstPluginControl? LoadVst3(string path) => loadVst3(path);

    /// <summary>Alias for load - PascalCase version</summary>
    public VstPluginControl? Load(string nameOrPath) => load(nameOrPath);
    /// <summary>Alias for load - PascalCase version</summary>
    public VstPluginControl? Load(int index) => load(index);
    /// <summary>Alias for load - Loads a VST plugin</summary>
    public VstPluginControl? LoadPlugin(string nameOrPath) => load(nameOrPath);
    /// <summary>Alias for load - Loads a VST plugin</summary>
    public VstPluginControl? LoadPlugin(int index) => load(index);
    /// <summary>Alias for load - Short form</summary>
    public VstPluginControl? l(string nameOrPath) => load(nameOrPath);
    /// <summary>Alias for load - Short form</summary>
    public VstPluginControl? l(int index) => load(index);

    // Get a loaded VST plugin by name
    public VstPluginControl? get(string name)
    {
        var plugin = _globals.GetVst(name);
        return plugin != null ? new VstPluginControl(_globals, plugin) : null;
    }

    /// <summary>Alias for get - PascalCase version</summary>
    public VstPluginControl? Get(string name) => get(name);
    /// <summary>Alias for get - Access VST plugin by name</summary>
    public VstPluginControl? plugin(string name) => get(name);

    // List all discovered VST plugins
    public void list() => _globals.ListVstPlugins();

    /// <summary>Alias for list - PascalCase version</summary>
    public void List() => list();
    /// <summary>Alias for list - Show all VST plugins</summary>
    public void plugins() => list();

    // List all loaded VST plugins
    public void loaded() => _globals.ListLoadedVstPlugins();

    /// <summary>Alias for loaded - PascalCase version</summary>
    public void Loaded() => loaded();
    /// <summary>Alias for loaded - Show active VST plugins</summary>
    public void active() => loaded();
}


// Control for a specific VST plugin (supports both VST2 and VST3)
public class VstPluginControl
{
    private readonly ScriptGlobals _globals;
    private readonly IVstPlugin _plugin;

    public VstPluginControl(ScriptGlobals globals, IVstPlugin plugin)
    {
        _globals = globals;
        _plugin = plugin;
    }

    // Get the underlying plugin as IVstPlugin interface
    public IVstPlugin Plugin => _plugin;

    // Get the underlying plugin as VstPlugin (for backward compatibility)
    // Returns null if the plugin is not a VstPlugin (e.g., pure VST3)
    public VstPlugin? Vst2Plugin => _plugin as VstPlugin;

    // Get the underlying plugin as IVst3Plugin (returns null if not VST3)
    public IVst3Plugin? Vst3Plugin => _plugin as IVst3Plugin;

    // === VST3-Specific Methods ===

    /// <summary>
    /// Check if this plugin is a VST3 plugin
    /// </summary>
    /// <returns>True if the wrapped plugin is VST3</returns>
    public bool isVst3() => _plugin.IsVst3;

    /// <summary>Alias for isVst3 - PascalCase version</summary>
    public bool IsVst3() => isVst3();

    /// <summary>
    /// List parameter units/groups (VST3 only).
    /// Units are hierarchical groups that organize parameters.
    /// </summary>
    /// <returns>This control for chaining</returns>
    public VstPluginControl listUnits()
    {
        if (_plugin is IVst3Plugin vst3)
        {
            var units = vst3.GetUnits();
            Console.WriteLine($"\n=== Parameter Units for {_plugin.Name} ===");
            if (units.Count == 0)
            {
                Console.WriteLine("  No units defined (all parameters in root).");
            }
            else
            {
                foreach (var unit in units)
                {
                    string parent = unit.ParentId >= 0 ? $" (parent: {unit.ParentId})" : "";
                    string programList = unit.ProgramListId >= 0 ? $" [programs: {unit.ProgramListId}]" : "";
                    Console.WriteLine($"  [{unit.Id}] {unit.Name}{parent}{programList}");

                    // List parameters in this unit
                    var paramsInUnit = vst3.GetParametersInUnit(unit.Id);
                    if (paramsInUnit.Count > 0)
                    {
                        foreach (var paramIdx in paramsInUnit)
                        {
                            string paramName = _plugin.GetParameterName(paramIdx);
                            Console.WriteLine($"      - [{paramIdx}] {paramName}");
                        }
                    }
                }
            }
            Console.WriteLine("==============================\n");
        }
        else
        {
            Console.WriteLine($"Plugin '{_plugin.Name}' is not a VST3 plugin. Units are only available for VST3.");
        }
        return this;
    }

    /// <summary>Alias for listUnits - PascalCase version</summary>
    public VstPluginControl ListUnits() => listUnits();

    /// <summary>
    /// Send a Note Expression value (VST3 only).
    /// Note Expression allows per-note parameter modulation.
    /// </summary>
    /// <param name="noteId">The note ID to apply expression to</param>
    /// <param name="type">Expression type: "volume", "pan", "tuning", "vibrato", "expression", "brightness"</param>
    /// <param name="value">Expression value (0.0 to 1.0, or -1.0 to 1.0 for tuning)</param>
    /// <returns>This control for chaining</returns>
    public VstPluginControl noteExpression(int noteId, string type, double value)
    {
        if (_plugin is IVst3Plugin vst3)
        {
            if (!vst3.SupportsNoteExpression)
            {
                Console.WriteLine($"Plugin '{_plugin.Name}' does not support Note Expression.");
                return this;
            }

            var expressionType = ParseNoteExpressionType(type);
            vst3.SendNoteExpression(noteId, expressionType, value);
        }
        else
        {
            Console.WriteLine($"Plugin '{_plugin.Name}' is not a VST3 plugin. Note Expression is only available for VST3.");
        }
        return this;
    }

    /// <summary>Alias for noteExpression - PascalCase version</summary>
    public VstPluginControl NoteExpression(int noteId, string type, double value) => noteExpression(noteId, type, value);

    /// <summary>
    /// Get the number of buses for a given media type and direction (VST3 only).
    /// </summary>
    /// <param name="mediaType">"audio" or "event"</param>
    /// <param name="direction">"input" or "output"</param>
    /// <returns>Number of buses, or -1 if not VST3</returns>
    public int getBusCount(string mediaType, string direction)
    {
        if (_plugin is IVst3Plugin vst3)
        {
            var media = ParseMediaType(mediaType);
            var dir = ParseBusDirection(direction);
            return vst3.GetBusCount(media, dir);
        }

        Console.WriteLine($"Plugin '{_plugin.Name}' is not a VST3 plugin. Bus info is only available for VST3.");
        return -1;
    }

    /// <summary>Alias for getBusCount - PascalCase version</summary>
    public int GetBusCount(string mediaType, string direction) => getBusCount(mediaType, direction);

    /// <summary>
    /// Activate or deactivate a bus (VST3 only).
    /// </summary>
    /// <param name="mediaType">"audio" or "event"</param>
    /// <param name="direction">"input" or "output"</param>
    /// <param name="index">Bus index</param>
    /// <param name="active">True to activate, false to deactivate</param>
    /// <returns>This control for chaining</returns>
    public VstPluginControl setBusActive(string mediaType, string direction, int index, bool active)
    {
        if (_plugin is IVst3Plugin vst3)
        {
            var media = ParseMediaType(mediaType);
            var dir = ParseBusDirection(direction);
            bool result = vst3.SetBusActive(media, dir, index, active);
            if (!result)
            {
                Console.WriteLine($"Failed to set bus {index} active state to {active}.");
            }
        }
        else
        {
            Console.WriteLine($"Plugin '{_plugin.Name}' is not a VST3 plugin. Bus control is only available for VST3.");
        }
        return this;
    }

    /// <summary>Alias for setBusActive - PascalCase version</summary>
    public VstPluginControl SetBusActive(string mediaType, string direction, int index, bool active)
        => setBusActive(mediaType, direction, index, active);

    /// <summary>
    /// List all buses for this plugin (VST3 only).
    /// </summary>
    /// <returns>This control for chaining</returns>
    public VstPluginControl listBuses()
    {
        if (_plugin is IVst3Plugin vst3)
        {
            Console.WriteLine($"\n=== Buses for {_plugin.Name} ===");

            // Audio buses
            int audioInputs = vst3.GetBusCount(Vst3MediaType.Audio, Vst3BusDirection.Input);
            int audioOutputs = vst3.GetBusCount(Vst3MediaType.Audio, Vst3BusDirection.Output);

            Console.WriteLine("Audio Input Buses:");
            for (int i = 0; i < audioInputs; i++)
            {
                var info = vst3.GetBusInfo(Vst3MediaType.Audio, Vst3BusDirection.Input, i);
                string busType = info.BusType == Vst3BusType.Main ? "Main" : "Aux";
                Console.WriteLine($"  [{i}] {info.Name} ({info.ChannelCount}ch, {busType}, default: {info.IsDefaultActive})");
            }

            Console.WriteLine("Audio Output Buses:");
            for (int i = 0; i < audioOutputs; i++)
            {
                var info = vst3.GetBusInfo(Vst3MediaType.Audio, Vst3BusDirection.Output, i);
                string busType = info.BusType == Vst3BusType.Main ? "Main" : "Aux";
                Console.WriteLine($"  [{i}] {info.Name} ({info.ChannelCount}ch, {busType}, default: {info.IsDefaultActive})");
            }

            // Event buses
            int eventInputs = vst3.GetBusCount(Vst3MediaType.Event, Vst3BusDirection.Input);
            int eventOutputs = vst3.GetBusCount(Vst3MediaType.Event, Vst3BusDirection.Output);

            if (eventInputs > 0 || eventOutputs > 0)
            {
                Console.WriteLine("Event Input Buses:");
                for (int i = 0; i < eventInputs; i++)
                {
                    var info = vst3.GetBusInfo(Vst3MediaType.Event, Vst3BusDirection.Input, i);
                    Console.WriteLine($"  [{i}] {info.Name}");
                }

                Console.WriteLine("Event Output Buses:");
                for (int i = 0; i < eventOutputs; i++)
                {
                    var info = vst3.GetBusInfo(Vst3MediaType.Event, Vst3BusDirection.Output, i);
                    Console.WriteLine($"  [{i}] {info.Name}");
                }
            }

            // Sidechain info
            if (vst3.SupportsSidechain)
            {
                Console.WriteLine($"Sidechain: Supported (bus index: {vst3.SidechainBusIndex})");
            }

            Console.WriteLine("==============================\n");
        }
        else
        {
            Console.WriteLine($"Plugin '{_plugin.Name}' is not a VST3 plugin. Bus listing is only available for VST3.");
        }
        return this;
    }

    /// <summary>Alias for listBuses - PascalCase version</summary>
    public VstPluginControl ListBuses() => listBuses();

    /// <summary>
    /// Check if Note Expression is supported (VST3 only).
    /// </summary>
    /// <returns>True if supported, false otherwise</returns>
    public bool supportsNoteExpression()
    {
        if (_plugin is IVst3Plugin vst3)
        {
            return vst3.SupportsNoteExpression;
        }
        return false;
    }

    /// <summary>Alias for supportsNoteExpression - PascalCase version</summary>
    public bool SupportsNoteExpression() => supportsNoteExpression();

    /// <summary>
    /// Check if sidechain is supported (VST3 only).
    /// </summary>
    /// <returns>True if supported, false otherwise</returns>
    public bool supportsSidechain()
    {
        if (_plugin is IVst3Plugin vst3)
        {
            return vst3.SupportsSidechain;
        }
        return false;
    }

    /// <summary>Alias for supportsSidechain - PascalCase version</summary>
    public bool SupportsSidechain() => supportsSidechain();

    // Helper methods for parsing string arguments to enums
    private static Vst3NoteExpressionType ParseNoteExpressionType(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "volume" => Vst3NoteExpressionType.Volume,
            "pan" => Vst3NoteExpressionType.Pan,
            "tuning" => Vst3NoteExpressionType.Tuning,
            "vibrato" => Vst3NoteExpressionType.Vibrato,
            "expression" => Vst3NoteExpressionType.Expression,
            "brightness" => Vst3NoteExpressionType.Brightness,
            _ => Vst3NoteExpressionType.Custom
        };
    }

    private static Vst3MediaType ParseMediaType(string mediaType)
    {
        return mediaType.ToLowerInvariant() switch
        {
            "audio" => Vst3MediaType.Audio,
            "event" or "midi" => Vst3MediaType.Event,
            _ => Vst3MediaType.Audio
        };
    }

    private static Vst3BusDirection ParseBusDirection(string direction)
    {
        return direction.ToLowerInvariant() switch
        {
            "input" or "in" => Vst3BusDirection.Input,
            "output" or "out" => Vst3BusDirection.Output,
            _ => Vst3BusDirection.Input
        };
    }

    // === Common Methods (work with both VST2 and VST3 via IVstPlugin interface) ===

    // Route MIDI input to this plugin
    public VstPluginControl from(int deviceIndex)
    {
        // RouteToVst currently takes VstPlugin - cast if possible
        if (_plugin is VstPlugin vst2)
        {
            _globals.RouteToVst(deviceIndex, vst2);
        }
        else
        {
            // For VST3, MIDI routing would need to be implemented via the IVst3Plugin interface
            Console.WriteLine($"MIDI routing for VST3 plugins is handled through the event bus system. Use setBusActive(\"event\", \"input\", 0, true) to enable MIDI input.");
        }
        return this;
    }

    // Route MIDI input by device name
    public VstPluginControl from(string deviceName)
    {
        int index = _globals.Engine.GetMidiDeviceIndex(deviceName);
        if (index >= 0)
        {
            if (_plugin is VstPlugin vst2)
            {
                _globals.RouteToVst(index, vst2);
            }
            else
            {
                Console.WriteLine($"MIDI routing for VST3 plugins is handled through the event bus system. Use setBusActive(\"event\", \"input\", 0, true) to enable MIDI input.");
            }
        }
        return this;
    }

    // Set a parameter by name (ISynth interface)
    public VstPluginControl param(string name, float value)
    {
        // ISynth provides SetParameter by name
        _plugin.SetParameter(name, value);
        return this;
    }

    // Set a parameter by index (IVstPlugin interface)
    public VstPluginControl param(int index, float value)
    {
        _plugin.SetParameterValue(index, value);
        return this;
    }

    // Set volume/gain (ISynth interface)
    public VstPluginControl volume(float value)
    {
        _plugin.MasterVolume = Math.Clamp(value, 0f, 2f);
        return this;
    }

    // Send a note on (ISynth interface)
    public VstPluginControl noteOn(int note, int velocity = 100)
    {
        _plugin.NoteOn(note, velocity);
        return this;
    }

    // Send a note off (ISynth interface)
    public VstPluginControl noteOff(int note)
    {
        _plugin.NoteOff(note);
        return this;
    }

    // Send all notes off (ISynth interface)
    public VstPluginControl allNotesOff()
    {
        _plugin.AllNotesOff();
        return this;
    }

    // Send control change (IVstPlugin interface)
    public VstPluginControl cc(int controller, int value, int channel = 0)
    {
        _plugin.SendControlChange(channel, controller, value);
        return this;
    }

    // Send program change (IVstPlugin interface)
    public VstPluginControl program(int programNumber, int channel = 0)
    {
        _plugin.SendProgramChange(channel, programNumber);
        return this;
    }

    // Send pitch bend (IVstPlugin interface)
    public VstPluginControl pitchBend(int value, int channel = 0)
    {
        _plugin.SendPitchBend(channel, value);
        return this;
    }

    // === Preset Management (IVstPlugin interface) ===

    // Load a preset from file
    public VstPluginControl loadPreset(string path)
    {
        _plugin.LoadPreset(path);
        return this;
    }

    // Save current state to preset file
    public VstPluginControl savePreset(string path)
    {
        _plugin.SavePreset(path);
        return this;
    }

    // Set preset by index
    public VstPluginControl preset(int index)
    {
        _plugin.SetPreset(index);
        return this;
    }

    // Get current preset name
    public string currentPreset => _plugin.CurrentPresetName;

    // Get all preset names
    public IReadOnlyList<string> presets => _plugin.GetPresetNames();

    // List all available presets
    public VstPluginControl listPresets()
    {
        var names = _plugin.GetPresetNames();
        Console.WriteLine($"\n=== Presets for {_plugin.Name} ===");
        for (int i = 0; i < names.Count; i++)
        {
            var current = i == _plugin.CurrentPresetIndex ? " [CURRENT]" : "";
            Console.WriteLine($"  [{i}] {names[i]}{current}");
        }
        Console.WriteLine("==============================\n");
        return this;
    }

    // === Parameter Methods (IVstPlugin interface) ===

    // Get parameter count
    public int paramCount => _plugin.GetParameterCount();

    // Get parameter name by index
    public string paramName(int index) => _plugin.GetParameterName(index);

    // Get parameter value by index
    public float paramValue(int index) => _plugin.GetParameterValue(index);

    // Get parameter display string by index
    public string paramDisplay(int index) => _plugin.GetParameterDisplay(index);

    // List all parameters
    public VstPluginControl listParams()
    {
        Console.WriteLine($"\n=== Parameters for {_plugin.Name} ===");
        int count = _plugin.GetParameterCount();
        if (count == 0)
        {
            Console.WriteLine("  No parameters available.");
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                string name = _plugin.GetParameterName(i);
                string display = _plugin.GetParameterDisplay(i);
                float value = _plugin.GetParameterValue(i);
                Console.WriteLine($"  [{i}] {name}: {display} ({value:F3})");
            }
        }
        Console.WriteLine("==============================\n");
        return this;
    }

    // === Parameter Automation (VstPlugin-specific, for backward compatibility) ===

    // Create automation ramp for a parameter
    public VstPluginControl automate(int paramIndex, float startValue, float endValue, double durationBeats)
    {
        if (_plugin is VstPlugin vst2)
        {
            vst2.AutomateParameter(paramIndex, startValue, endValue, durationBeats);
        }
        else
        {
            Console.WriteLine("Automation is currently only supported for VST2 plugins through this API.");
        }
        return this;
    }

    // Clear automation for a parameter
    public VstPluginControl clearAutomation(int paramIndex)
    {
        if (_plugin is VstPlugin vst2)
        {
            vst2.ClearAutomation(paramIndex);
        }
        return this;
    }

    // Clear all automation
    public VstPluginControl clearAllAutomation()
    {
        if (_plugin is VstPlugin vst2)
        {
            vst2.ClearAllAutomation();
        }
        return this;
    }

    // Set current time for automation playback
    public VstPluginControl setTime(double beats)
    {
        if (_plugin is VstPlugin vst2)
        {
            vst2.SetCurrentTimeBeats(beats);
        }
        return this;
    }

    // === Plugin Info Properties ===

    /// <summary>Plugin name</summary>
    public string name => _plugin.Name;

    /// <summary>Plugin vendor</summary>
    public string vendor => _plugin.Vendor;

    /// <summary>Plugin version</summary>
    public string version => _plugin.Version;

    /// <summary>True if instrument, false if effect</summary>
    public bool isInstrument => _plugin.IsInstrument;

    /// <summary>Number of audio inputs</summary>
    public int numInputs => _plugin.NumAudioInputs;

    /// <summary>Number of audio outputs</summary>
    public int numOutputs => _plugin.NumAudioOutputs;

    // Explicit conversion to VstPlugin (may return null for pure VST3 plugins)
    public static explicit operator VstPlugin?(VstPluginControl control) => control._plugin as VstPlugin;
}
