//Engine License (MEL) – Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: A static class to launch the Music Engine with scripting capabilities.


using MusicEngine.Core;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;


namespace MusicEngine.Scripting;


public static class EngineLauncher
{
    /// <summary>
    /// Safe Mode flag - when true, audio engine initialization is skipped.
    /// Use --safe or --no-audio command line argument to enable.
    /// </summary>
    public static bool SafeMode { get; set; }

    /// <summary>
    /// Checks command line arguments for safe mode flags.
    /// Call this from Main() before LaunchAsync().
    /// </summary>
    public static void ParseArguments(string[] args)
    {
        SafeMode = args.Any(arg =>
            arg.Equals("--safe", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("--no-audio", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("/safe", StringComparison.OrdinalIgnoreCase));
    }

    public static async Task LaunchAsync(string defaultScript = "// Start coding music here...")
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║              MusicEngine - Audio Synthesis Suite          ║");
        Console.WriteLine("║                    Version 1.0.0                          ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        AudioEngine? engine = null;
        Sequencer? sequencer = null;

        if (SafeMode)
        {
            Console.WriteLine("[SAFE MODE] Audio engine disabled. Use --safe to start without audio.");
            Console.WriteLine("[SAFE MODE] Scripting features that require audio will not work.");
            Console.WriteLine();
        }
        else
        {
            Console.WriteLine("Initializing audio engine...");
            try
            {
                engine = new AudioEngine(sampleRate: null, logger: null); // Create the audio engine
                engine.Initialize(); // Initialize the audio engine (also scans VST plugins)
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to initialize audio engine: {ex.Message}");
                Console.WriteLine("[ERROR] Try running with --safe or --no-audio flag to skip audio initialization.");
                Console.WriteLine();
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }
        }

        if (!SafeMode)
        {
            Console.WriteLine();
            Console.WriteLine("Starting sequencer...");
            sequencer = new Sequencer(); // Create the sequencer
            sequencer.Start(); // Start the sequencer
        }

        // In safe mode, engine and sequencer are null - ScriptHost will handle this
        ScriptHost? host = null;
        if (engine != null && sequencer != null)
        {
            host = new ScriptHost(engine, sequencer); // Create the scripting host
        }

        Console.WriteLine();
        if (SafeMode)
        {
            Console.WriteLine("[SAFE MODE] Limited functionality - no audio or scripting available.");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            return;
        }

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

        await host!.ExecuteScriptAsync(activeScript); // Execute the initial script

        var ui = new ConsoleInterface(host, activeScript, () => sequencer!.Stop(), scriptPath); // Create the console interface
        await ui.RunAsync(); // Run the console interface
    }
}
