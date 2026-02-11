// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Minimal engine launcher for scripts.

using System;
using System.IO;
using System.Threading.Tasks;
using MusicEngine.Core;
using NAudio.Midi;
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
    /// <param name="enableIpc">Enable IPC server for editor integration.</param>
    public static async Task LaunchAsync(string defaultScript = "// Start coding music here...",
        bool executeScriptOnStartup = true, bool startSequencerOnStartup = true, bool startSleeping = false,
        bool editorMode = false, bool enableIpc = true)
    {
        Console.WriteLine("MusicEngine minimal mode");
        Console.WriteLine("Initializing audio engine...");

        string scriptPath = Path.Combine(AppContext.BaseDirectory, "Scripts", "test_script.cs");

        string? projectDir = AppContext.BaseDirectory;
        while (projectDir != null && !File.Exists(Path.Combine(projectDir, "MusicEngine.csproj")))
        {
            projectDir = Path.GetDirectoryName(projectDir);
        }

        if (projectDir != null)
        {
            scriptPath = Path.Combine(projectDir, "Scripts", "test_script.cs");
        }

        if (!File.Exists(scriptPath))
        {
            var fallback = Path.ChangeExtension(scriptPath, ".csx");
            if (File.Exists(fallback))
            {
                scriptPath = fallback;
            }
        }

        using var engine = new EngineScriptInterface(new EngineScriptInterfaceOptions
        {
            StartSequencerOnStartup = startSequencerOnStartup,
            ScriptFilePath = scriptPath
        });
        await engine.StartupAsync();
        engine.SetEditorMode(editorMode);
        var audioEngine = engine.Engine;
        var sequencer = engine.Sequencer;
        var host = engine.Host;
        var registry = engine.VstRegistry;
        EngineIpcServer? ipcServer = null;
        if (enableIpc)
        {
            ipcServer = new EngineIpcServer(engine);
            ipcServer.Start();
        }

        var outputDevices = audioEngine.ListOutputDevices();
        Console.WriteLine();
        Console.WriteLine("Audio Outputs:");
        if (outputDevices.Count == 0)
        {
            Console.WriteLine("  (none)");
        }
        else
        {
            foreach (var device in outputDevices)
            {
                Console.WriteLine(
                    $"  [{device.Index}] {device.Name} ({device.Channels}ch @ {device.SampleRate}Hz) " +
                    $"(use Audio.Channel(n).VirtualOut({device.Index}) or Audio.Channel(n).VirtualOut({device.Index}, offset))");
            }
        }

        var inputDevices = audioEngine.ListInputDevices();
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

        Console.WriteLine();
        Console.WriteLine("MIDI Inputs:");
        if (MidiIn.NumberOfDevices == 0)
        {
            Console.WriteLine("  (none)");
        }
        else
        {
            for (int i = 0; i < MidiIn.NumberOfDevices; i++)
            {
                var info = MidiIn.DeviceInfo(i);
                Console.WriteLine($"  [{i}] {info.ProductName}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("MIDI Outputs:");
        if (MidiOut.NumberOfDevices == 0)
        {
            Console.WriteLine("  (none)");
        }
        else
        {
            for (int i = 0; i < MidiOut.NumberOfDevices; i++)
            {
                var info = MidiOut.DeviceInfo(i);
                Console.WriteLine($"  [{i}] {info.ProductName}");
            }
        }

        Console.WriteLine();
        if (registry == null || registry.Plugins.Count == 0)
        {
            Console.WriteLine("No VST3 plugins found.");
        }
        else
        {
            Console.WriteLine("VST3 plugins:");
            foreach (var plugin in registry.Plugins)
            {
                Console.WriteLine($"  [{plugin.Index}] {plugin.Name}");
            }
        }

        AppDomain.CurrentDomain.ProcessExit += (_, _) => host.SaveVstState();
        Console.CancelKeyPress += (_, e) =>
        {
            host.SaveVstState();
            e.Cancel = false;
        };

        var directory = Path.GetDirectoryName(scriptPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string activeScript = string.Empty;

        if (executeScriptOnStartup)
        {
            await host.RefreshMainScriptsAsync();
        }

        if (editorMode)
        {
            sequencer.Stop();
            audioEngine.SetTransportMuted(true);
            audioEngine.SetMidiEnabled(false, sendAllNotesOff: false);
            audioEngine.EditorPatternNote += info =>
            {
                Console.WriteLine(info.IsOn ? $"NOTE_ON {info.Note}" : $"NOTE_OFF {info.Note}");
            };
            audioEngine.EditorMidiNote += info =>
            {
                Console.WriteLine(info.IsOn
                    ? $"MIDI_IN {info.DeviceIndex} NOTE_ON {info.Note} {info.Velocity}"
                    : $"MIDI_IN {info.DeviceIndex} NOTE_OFF {info.Note}");
            };
            audioEngine.EditorMidiDeviceActive += deviceIndex =>
            {
                Console.WriteLine($"MIDI_DEVICE_ACTIVE {deviceIndex}");
            };
        }
        else if (startSleeping)
        {
            engine.Sleep();
        }

        var ui = new ConsoleInterface(host, activeScript, () => sequencer.Stop(), scriptPath, registry,
            editorMode: editorMode, useMainScripts: true);
        try
        {
            await ui.RunAsync();
        }
        finally
        {
            ipcServer?.Dispose();
        }
    }
}
