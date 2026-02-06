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

public static class EngineLauncher
{
    public static async Task LaunchAsync(string defaultScript = "// Start coding music here...")
    {
        Console.WriteLine("MusicEngine minimal mode");
        Console.WriteLine("Initializing audio engine...");

        using var engine = new AudioEngine();
        engine.Initialize();

        var sequencer = new Sequencer();
        sequencer.Start();

        Vst3Registry? registry = null;
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

        var host = new ScriptHost(engine, sequencer, registry);

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

        string activeScript = defaultScript;
        if (!File.Exists(scriptPath))
        {
            File.WriteAllText(scriptPath, defaultScript);
        }
        else
        {
            activeScript = File.ReadAllText(scriptPath);
        }

        await host.ExecuteScriptAsync(activeScript);

        var ui = new ConsoleInterface(host, activeScript, () => sequencer.Stop(), scriptPath, registry);
        await ui.RunAsync();
    }
}
