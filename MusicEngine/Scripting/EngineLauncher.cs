// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Minimal engine launcher for scripts.

using System;
using System.IO;
using System.Threading.Tasks;
using MusicEngine.Core;
using MusicEngine.Vst;

namespace MusicEngine.Scripting;

/// <summary>
/// Minimal engine launcher for script-driven sessions.
/// </summary>
public static class EngineLauncher
{
    /// <summary>
    /// Launch the engine with optional default script and runtime options.
    /// </summary>
    /// <param name="defaultScript">Default script content.</param>
    /// <param name="executeScriptOnStartup">Execute the script immediately.</param>
    /// <param name="startSequencerOnStartup">Start the sequencer immediately.</param>
    /// <param name="startSleeping">Start with audio output suspended.</param>
    /// <param name="editorMode">Enable editor mode hooks.</param>
    public static async Task LaunchAsync(string defaultScript = "// Start coding music here...",
        bool executeScriptOnStartup = true, bool startSequencerOnStartup = true, bool startSleeping = false,
        bool editorMode = false)
    {
        Console.WriteLine("MusicEngine minimal mode");
        Console.WriteLine("Initializing audio engine...");

        using var engine = new AudioEngine();
        engine.Initialize();
        engine.SetEditorMode(editorMode);

        var sequencer = new Sequencer();
        if (startSequencerOnStartup)
        {
            sequencer.Start();
        }

        Vst3Registry? registry = null;
        var inputDevices = engine.ListInputDevices();
        Console.WriteLine();
        Console.WriteLine("Audio Inputs:");
        if (inputDevices.Count == 0)
        {
            Console.WriteLine("  (none)");
        }
        else
        {
            foreach (var device in inputDevices)
            {
                Console.WriteLine($"  [{device.Index}] {device.Name}");
            }
        }

        if (VstSystem.TryScan(out var scannedRegistry, out var scanMessage))
        {
            registry = scannedRegistry;
            Console.WriteLine();
            Console.WriteLine(scanMessage);
            foreach (var plugin in registry.Plugins)
            {
                Console.WriteLine($"  [{plugin.Index}] {plugin.Name}");
            }
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine(scanMessage);
        }

        string scriptFileName = "test_script.csx";
        string scriptPath = Path.Combine(AppContext.BaseDirectory, scriptFileName);

        string? projectDir = AppContext.BaseDirectory;
        while (projectDir != null && !File.Exists(Path.Combine(projectDir, "MusicEngine.csproj")))
        {
            projectDir = Path.GetDirectoryName(projectDir);
        }

        if (projectDir != null)
        {
            scriptPath = Path.Combine(projectDir, scriptFileName);
        }

        var host = new ScriptHost(engine, sequencer, registry, scriptPath);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => host.SaveVstState();
        Console.CancelKeyPress += (_, e) =>
        {
            host.SaveVstState();
            e.Cancel = false;
        };

        string activeScript = defaultScript;
        if (!File.Exists(scriptPath))
        {
            File.WriteAllText(scriptPath, defaultScript);
        }
        else
        {
            activeScript = File.ReadAllText(scriptPath);
        }

        if (executeScriptOnStartup)
        {
            await host.ExecuteScriptAsync(activeScript);
        }

        if (editorMode)
        {
            sequencer.Stop();
            engine.SetTransportMuted(true);
            engine.SetMidiEnabled(false, sendAllNotesOff: false);
            engine.EditorPatternNote += info =>
            {
                Console.WriteLine(info.IsOn ? $"NOTE_ON {info.Note}" : $"NOTE_OFF {info.Note}");
            };
            engine.EditorMidiNote += info =>
            {
                Console.WriteLine(info.IsOn
                    ? $"MIDI_IN {info.DeviceIndex} NOTE_ON {info.Note} {info.Velocity}"
                    : $"MIDI_IN {info.DeviceIndex} NOTE_OFF {info.Note}");
            };
            engine.EditorMidiDeviceActive += deviceIndex =>
            {
                Console.WriteLine($"MIDI_DEVICE_ACTIVE {deviceIndex}");
            };
        }
        else if (startSleeping)
        {
            sequencer.Stop();
            engine.SuspendOutput();
        }

        var ui = new ConsoleInterface(host, activeScript, () => sequencer.Stop(), scriptPath, registry,
            editorMode: editorMode);
        await ui.RunAsync();
    }
}
