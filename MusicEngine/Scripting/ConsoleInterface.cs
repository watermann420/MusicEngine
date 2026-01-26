// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Threading.Tasks;


namespace MusicEngine.Scripting;


public class ConsoleInterface
{
    private readonly ScriptHost _host; // The scripting host
    private readonly string? _scriptFilePath; // Optional script file path for reloading
    private string _scriptContent; // The current script content
    private readonly Action _onExit; // Action to invoke on exit
    
    // Constructor to initialize the console interface
    public ConsoleInterface(ScriptHost host, string scriptContent, Action onExit, string? scriptFilePath = null)
    {
        _host = host; // Scripting host
        _scriptContent = scriptContent; // Initial script content
        _scriptFilePath = scriptFilePath; // Optional script file path
        _onExit = onExit; // Exit action
    }

    // Runs the console interface loop
    public async Task RunAsync()
    {
        Console.WriteLine("Music Engine Running.");
        Console.WriteLine("Commands: /S to Refresh, /exit to Stop.");

        while (true)
        {
            Console.Write("> "); // Prompt for input
            string? input = Console.ReadLine(); // Read user input
            if (string.IsNullOrEmpty(input)) continue; // Ignore empty input

            string command = input.Trim().ToUpperInvariant(); // Normalize command

            if (command == "/S") // Refresh command
            {
                await RefreshScript();
            }
            else if (command == "/EXIT") // Exit command
            {
                _onExit();
                break;
            }
            else
            {
                Console.WriteLine($"Unknown command: {input}"); // Handle unknown command
                Console.WriteLine("Available commands: /S (Refresh), /exit (Stop)");
            }
        }
    }
    
    // Refreshes the script by reloading from the file and re-executing
    private async Task RefreshScript()
    {
        Console.WriteLine("Refreshing Script...");
        
        if (!string.IsNullOrEmpty(_scriptFilePath) && File.Exists(_scriptFilePath))
        {
            try
            {
                _scriptContent = await File.ReadAllTextAsync(_scriptFilePath);
                Console.WriteLine($"Reloaded script from: {_scriptFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to reload script file: {ex.Message}");
            }
        }

        _host.ClearState();
        await _host.ExecuteScriptAsync(_scriptContent);
        Console.WriteLine("Refresh Complete.");
    }
}
