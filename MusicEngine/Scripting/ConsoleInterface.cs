// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Minimal console interface.

using System;
using System.IO;
using System.Threading.Tasks;
using MusicEngine.Vst;

namespace MusicEngine.Scripting;

public sealed class ConsoleInterface
{
    private readonly ScriptHost _host;
    private readonly string? _scriptFilePath;
    private string _scriptContent;
    private readonly Action _onExit;
    private readonly Vst3Registry? _vst3Registry;

    public ConsoleInterface(ScriptHost host, string scriptContent, Action onExit, string? scriptFilePath = null, Vst3Registry? vst3Registry = null)
    {
        _host = host;
        _scriptContent = scriptContent;
        _scriptFilePath = scriptFilePath;
        _onExit = onExit;
        _vst3Registry = vst3Registry;
    }

    public async Task RunAsync()
    {
        Console.WriteLine("Music Engine Running.");
        Console.WriteLine("Commands: /S to Refresh, /exit to Stop, /vst to list, /open <name>.");

        while (true)
        {
            Console.Write("> ");
            string? input = Console.ReadLine();
            if (string.IsNullOrEmpty(input)) continue;

            string command = input.Trim().ToUpperInvariant();
            if (command == "/S")
            {
                await RefreshScript();
            }
            else if (command == "/EXIT")
            {
                _onExit();
                break;
            }
            else if (command == "/VST")
            {
                PrintVstList();
            }
            else if (command.StartsWith("/OPEN ", StringComparison.OrdinalIgnoreCase))
            {
                OpenVst(input.Substring(6).Trim());
            }
            else
            {
                Console.WriteLine($"Unknown command: {input}");
            }
        }
    }

    private async Task RefreshScript()
    {
        Console.WriteLine("Refreshing Script...");

        if (!string.IsNullOrEmpty(_scriptFilePath) && File.Exists(_scriptFilePath))
        {
            _scriptContent = await File.ReadAllTextAsync(_scriptFilePath);
        }

        _host.ClearState();
        await _host.ExecuteScriptAsync(_scriptContent);
        Console.WriteLine("Refresh Complete.");
    }

    private void PrintVstList()
    {
        if (_vst3Registry == null)
        {
            Console.WriteLine("VST disabled (native host not available).");
            return;
        }

        if (_vst3Registry.Plugins.Count == 0)
        {
            Console.WriteLine("No VST3 plugins found.");
            return;
        }

        Console.WriteLine("VST3 plugins:");
        foreach (var plugin in _vst3Registry.Plugins)
        {
            Console.WriteLine($"  [{plugin.Index}] {plugin.Name}");
        }
    }

    private void OpenVst(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            Console.WriteLine("Usage: /open <name>");
            return;
        }

        if (_host.TryOpenVstEditor(name))
        {
            Console.WriteLine($"Opening VST3 editor (existing instance): {name}");
            return;
        }

        if (_vst3Registry == null)
        {
            Console.WriteLine("VST disabled (native host not available).");
            return;
        }

        var plugin = _vst3Registry.FindByName(name);
        if (plugin == null)
        {
            Console.WriteLine($"VST3 not found: {name}");
            return;
        }

        Vst3EditorWindow.Open(plugin.Path, plugin.Name);
        Console.WriteLine($"Opening VST3 editor: {plugin.Name}");
    }
}
