// ============================================================================
// 08 - VST PLUGIN CONTROL
// ============================================================================
// This script demonstrates VST plugin loading and control
// SYNTAX TO REVIEW: VST functions, plugin loading, parameter control
// ============================================================================

Print("=== VST PLUGIN CONTROL TEST ===");
Print("");

// ============================================================================
// 1. LIST AVAILABLE PLUGINS
// ============================================================================
Print("1. List available VST plugins:");

var availablePlugins = Vst.ListPlugins();
Print($"   Found {availablePlugins.Count} plugins");

foreach (var plugin in availablePlugins)
{
    Print($"   - {plugin}");
}
Print("");

// ============================================================================
// 2. LIST LOADED PLUGINS
// ============================================================================
Print("2. List loaded VST plugins:");

var loadedPlugins = Vst.LoadedPlugins();
Print($"   Currently loaded: {loadedPlugins.Count} plugins");
Print("");

// ============================================================================
// 3. LOAD PLUGIN (By Name)
// ============================================================================
Print("3. Load VST plugin by name:");

var synth = Vst.Load("MySynth");
Print("   Vst.Load(\"MySynth\")");
Print($"   → Loaded plugin: {synth?.Name ?? "not found"}");
Print("");

// ============================================================================
// 4. LOAD PLUGIN (By Path)
// ============================================================================
Print("4. Load VST plugin by path:");

var effect = Vst.Load("C:\\VST\\Reverb.dll");
Print("   Vst.Load(\"C:\\\\VST\\\\Reverb.dll\")");
Print($"   → Loaded plugin from path");
Print("");

// ============================================================================
// 5. LOAD PLUGIN (By Index)
// ============================================================================
Print("5. Load VST plugin by index:");

var firstPlugin = Vst.Load(0);
Print("   Vst.Load(0)");
Print("   → Loaded first available plugin");
Print("");

// ============================================================================
// 6. GET ALREADY LOADED PLUGIN
// ============================================================================
Print("6. Get already loaded plugin:");

var existing = Vst.Get("MySynth");
Print("   Vst.Get(\"MySynth\")");
Print("   → Retrieved already loaded plugin");
Print("");

// ============================================================================
// 7. ROUTE MIDI TO VST PLUGIN
// ============================================================================
Print("7. Route MIDI to VST plugin:");

if (synth != null)
{
    // Route from MIDI device by index
    Vst.Plugin(synth).Midi().From(0);
    Print("   Vst.Plugin(synth).Midi().From(0)");
    Print("   → Routes MIDI device 0 to plugin");

    // Route from MIDI device by name
    Vst.Plugin(synth).Midi().From("My Keyboard");
    Print("   Vst.Plugin(synth).Midi().From(\"My Keyboard\")");
    Print("   → Routes specific MIDI device to plugin");
}
Print("");

// ============================================================================
// 8. SET VST PLUGIN PARAMETERS
// ============================================================================
Print("8. Set VST plugin parameters:");

if (synth != null)
{
    // Set by parameter name
    Vst.Plugin(synth).SetParameter("Volume", 0.8);
    Print("   Vst.Plugin(synth).SetParameter(\"Volume\", 0.8)");

    // Set by parameter index
    Vst.Plugin(synth).SetParameter(0, 0.5);
    Print("   Vst.Plugin(synth).SetParameter(0, 0.5)");

    // Fluent API for parameters
    Vst.Plugin(synth)
        .Param("Cutoff", 0.7)
        .Param("Resonance", 0.4)
        .Param("Volume", 0.9);
    Print("   Vst.Plugin(synth)");
    Print("       .Param(\"Cutoff\", 0.7)");
    Print("       .Param(\"Resonance\", 0.4)");
    Print("       .Param(\"Volume\", 0.9)");
}
Print("");

// ============================================================================
// 9. SEND NOTES TO VST PLUGIN
// ============================================================================
Print("9. Send notes to VST plugin:");

if (synth != null)
{
    // Note on
    Vst.Plugin(synth).NoteOn(60, 100);
    Print("   Vst.Plugin(synth).NoteOn(60, 100)");

    await Task.Delay(500);

    // Note off
    Vst.Plugin(synth).NoteOff(60);
    Print("   Vst.Plugin(synth).NoteOff(60)");

    // All notes off
    Vst.Plugin(synth).AllNotesOff();
    Print("   Vst.Plugin(synth).AllNotesOff()");
}
Print("");

// ============================================================================
// 10. SEND MIDI MESSAGES TO VST
// ============================================================================
Print("10. Send MIDI messages to VST:");

if (synth != null)
{
    // Control Change
    Vst.Plugin(synth).ControlChange(1, 64, 0);  // CC1 = 64 on channel 0
    Print("   Vst.Plugin(synth).ControlChange(1, 64, 0)");
    Print("   → Send CC1 = 64 on channel 0");

    // Program Change
    Vst.Plugin(synth).ProgramChange(5, 0);  // Program 5 on channel 0
    Print("   Vst.Plugin(synth).ProgramChange(5, 0)");
    Print("   → Send Program Change 5 on channel 0");
}
Print("");

// ============================================================================
// 11. COMBINED FLUENT API
// ============================================================================
Print("11. Combined VST control with fluent API:");

if (synth != null)
{
    Vst.Plugin(synth)
        .Midi().From(0)
        .Param("Volume", 0.8)
        .Param("Cutoff", 0.6)
        .NoteOn(60, 100);

    Print("   Vst.Plugin(synth)");
    Print("       .Midi().From(0)");
    Print("       .Param(\"Volume\", 0.8)");
    Print("       .Param(\"Cutoff\", 0.6)");
    Print("       .NoteOn(60, 100)");
}
Print("");

// ============================================================================
// 12. VST PLUGIN WITH PATTERN
// ============================================================================
Print("12. Create pattern for VST plugin:");

if (synth != null)
{
    // VST plugins implement ISynth, so they work with CreatePattern
    var vstPattern = CreatePattern(synth, "vst-melody");
    vstPattern.Loop = true;

    vstPattern.AddNote(0.0, 60, 100, 0.5);
    vstPattern.AddNote(1.0, 64, 90, 0.5);
    vstPattern.AddNote(2.0, 67, 95, 0.5);
    vstPattern.AddNote(3.0, 72, 100, 1.0);

    Print($"   Created pattern '{vstPattern.Name}' for VST plugin");
    Print($"   Notes: {vstPattern.NoteCount}");
}
Print("");

// ============================================================================
// 13. UNLOAD PLUGIN
// ============================================================================
Print("13. Unload VST plugin:");

if (synth != null)
{
    Vst.Unload(synth.Name);
    Print($"   Vst.Unload(\"{synth.Name}\")");
    Print("   → Plugin unloaded");
}
Print("");

Print("=== VST PLUGIN CONTROL TEST COMPLETED ===");

// ============================================================================
// SYNTAX ELEMENTS TO CUSTOMIZE:
// ============================================================================
// VST MANAGEMENT FUNCTIONS:
// - Vst.ListPlugins (could be: list, plugins, available, scan, discover)
// - Vst.LoadedPlugins (could be: loaded, active, running, instances)
// - Vst.Load (could be: load, open, create, add, loadPlugin)
// - Vst.Get (could be: get, find, retrieve, plugin)
// - Vst.Unload (could be: unload, close, remove, delete)
//
// VST CONTROL FLUENT API:
// - Vst.Plugin (could be: plugin, vst, instance, effect)
// - Midi (could be: midi, input, from, source)
// - From (could be: from, device, source, input)
// - SetParameter (could be: param, set, parameter, value)
// - Param (could be: param, set, value, property)
//
// VST NOTE FUNCTIONS:
// - NoteOn (could be: noteOn, play, trigger, start)
// - NoteOff (could be: noteOff, stop, release, end)
// - AllNotesOff (could be: allOff, panic, silence, stopAll)
//
// VST MIDI FUNCTIONS:
// - ControlChange (could be: cc, controlChange, sendCC, midi)
// - ProgramChange (could be: program, preset, patch, programChange)
//
// PARAMETER NAMES:
// - name/path (could be: name, path, file, plugin)
// - index (could be: index, id, number, slot)
// - parameterName (could be: name, param, parameter, property)
// - parameterIndex (could be: index, id, number, param)
// - value (could be: value, val, amount, level)
// ============================================================================
