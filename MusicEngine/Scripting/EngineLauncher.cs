// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using MusicEngine.Core;
using System;
using System.IO;
using System.Threading.Tasks;


namespace MusicEngine.Scripting;


public static class EngineLauncher
{
    public static async Task LaunchAsync(string defaultScript = "// Start coding music here...")
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║              MusicEngine - Audio Synthesis Suite          ║");
        Console.WriteLine("║                    Version 1.0.0                          ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("Initializing audio engine...");

        using var engine = new AudioEngine(sampleRate: null, logger: null); // Create the audio engine
        engine.Initialize(); // Initialize the audio engine (also scans VST plugins)

        Console.WriteLine();
        Console.WriteLine("Starting sequencer...");
        var sequencer = new Sequencer(); // Create the sequencer
        sequencer.Start(); // Start the sequencer

        var host = new ScriptHost(engine, sequencer); // Create the scripting host

        Console.WriteLine();
        Console.WriteLine("Engine ready! Available commands:");
        Console.WriteLine("  - Type C# code to execute");
        Console.WriteLine("  - Use 'vst.list()' to show VST plugins");
        Console.WriteLine("  - Use 'vst.load(\"PluginName\")' to load a VST");
        Console.WriteLine("  - Use 'midi.output(0).noteOn(60, 100)' for MIDI output");
        Console.WriteLine();

        //Todo: Make it possible to have a list of script files to load from args or config
        string scriptFileName = "test_script.csx"; // Default script file name
        string scriptPath = Path.Combine(AppContext.BaseDirectory, scriptFileName); // Default script path


        string? projectDir = AppContext.BaseDirectory; // Start from the base directory
        while (projectDir != null && !File.Exists(Path.Combine(projectDir, "MusicEngine.csproj"))) // Look for the project file
        {
            projectDir = Path.GetDirectoryName(projectDir); // Move up one directory
        }

        if (projectDir != null)
        {
            string sourceScriptPath = Path.Combine(projectDir, scriptFileName); // Script path in the project directory
            scriptPath = sourceScriptPath; // Use the project directory script path
            Console.WriteLine($"Project directory detected: {projectDir}"); // Log the project directory
        }

        string activeScript = defaultScript; // Initialize with a default script

        // Ensure the script file exists.
        if (!File.Exists(scriptPath))
        {
            Console.WriteLine($"Creating initial script at: {scriptPath}"); // Log script creation
            File.WriteAllText(scriptPath, defaultScript); // Create the script file with the default content
        }
        else
        {
            activeScript = File.ReadAllText(scriptPath);  // Load existing script content
            Console.WriteLine($"Loading existing script from: {scriptPath}"); // Log script loading
        }

        await host.ExecuteScriptAsync(activeScript); // Execute the initial script

        var ui = new ConsoleInterface(host, activeScript, () => sequencer.Stop(), scriptPath); // Create the console interface
        await ui.RunAsync(); // Run the console interface
    }
}
