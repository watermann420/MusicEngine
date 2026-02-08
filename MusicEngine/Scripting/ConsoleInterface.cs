// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Minimal console interface.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MusicEngine.Vst;

namespace MusicEngine.Scripting;

/// <summary>
/// Minimal interactive console interface for script execution.
/// </summary>
public sealed class ConsoleInterface
{
    private readonly ScriptHost _host;
    private readonly string? _scriptFilePath;
    private string _scriptContent;
    private readonly Action _onExit;
    private readonly Vst3Registry? _vst3Registry;
    private readonly bool _editorMode;
    private readonly bool _quietMode;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private bool _hasExecutedScript;

    /// <summary>
    /// Create a console interface bound to a script host.
    /// </summary>
    /// <param name="host">Script host instance.</param>
    /// <param name="scriptContent">Initial script content.</param>
    /// <param name="onExit">Callback when exiting.</param>
    /// <param name="scriptFilePath">Optional script file path.</param>
    /// <param name="vst3Registry">Optional VST3 registry for listing/opening plugins.</param>
    /// <param name="editorMode">Enable editor mode commands.</param>
    public ConsoleInterface(ScriptHost host, string scriptContent, Action onExit, string? scriptFilePath = null,
        Vst3Registry? vst3Registry = null, bool editorMode = false)
    {
        _host = host;
        _scriptContent = scriptContent;
        _scriptFilePath = scriptFilePath;
        _onExit = onExit;
        _vst3Registry = vst3Registry;
        _editorMode = editorMode;
        _quietMode = _editorMode || Console.IsInputRedirected || Console.IsOutputRedirected;
    }

    /// <summary>
    /// Run the interactive console loop.
    /// </summary>
    public async Task RunAsync()
    {
        Console.WriteLine("Music Engine Running.");
        if (!_quietMode)
        {
            if (_editorMode)
            {
                Console.WriteLine("Commands: /S to Refresh, /play, /stop, /exit to Stop, /vst to list, /open <name>.");
            }
            else
            {
                Console.WriteLine("Commands: /S to Refresh, /exit to Stop, /vst to list, /open <name>.");
            }
        }
        _host.DisposeVstOnClear = false;

        while (true)
        {
            if (!_quietMode)
            {
                Console.Write("> ");
            }
            string? input = Console.ReadLine();
            if (string.IsNullOrEmpty(input)) continue;

            string trimmed = input.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            string[] parts = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            string command = parts[0].ToUpperInvariant();
            string args = parts.Length > 1 ? parts[1].Trim() : string.Empty;

            if (command == "/S" || command == "/REFRESH")
            {
                await RefreshScript();
            }
            else if (_editorMode && command == "/PLAY")
            {
                await Play();
            }
            else if (_editorMode && command == "/STOP")
            {
                Stop();
            }
            else if (command == "/EXIT")
            {
                _host.SaveVstState();
                _onExit();
                break;
            }
            else if (command == "/VST")
            {
                PrintVstList();
            }
            else if (command == "/OPEN")
            {
                OpenVst(args);
            }
            else
            {
                Console.WriteLine($"Unknown command: {input}");
            }
        }
    }

    private async Task Play()
    {
        if (!_hasExecutedScript)
        {
            await RefreshScript();
            if (!_hasExecutedScript)
            {
                Console.WriteLine("Play blocked (no script executed).");
                return;
            }
        }

        _host.SetTransportMuted(false);
        _host.SetMidiEnabled(true);
        _host.StartSequencer();
        Console.WriteLine("Playing.");
    }

    private void Stop()
    {
        _host.StopSequencer();
        _host.SetTransportMuted(true);
        _host.SetMidiEnabled(false);
        _host.AllNotesOff();
        Console.WriteLine("Stopped.");
    }

    private async Task RefreshScript()
    {
        if (!await _refreshLock.WaitAsync(0))
        {
            return;
        }

        Console.WriteLine("Refreshing Script...");

        try
        {
            if (!string.IsNullOrEmpty(_scriptFilePath) && File.Exists(_scriptFilePath))
            {
                _scriptContent = await File.ReadAllTextAsync(_scriptFilePath);
            }

            bool executed = await _host.RefreshScriptAsync(_scriptContent, skipIfUnchanged: !_quietMode);
            Console.WriteLine(executed ? "Refresh Complete." : "No changes detected.");
            if (executed)
            {
                _hasExecutedScript = true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Refresh failed: {ex.Message}");
        }
        finally
        {
            _refreshLock.Release();
        }
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
