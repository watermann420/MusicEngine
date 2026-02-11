// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Minimal entry point.

using System.Collections.Generic;
using MusicEngine.Scripting;

var argList = new List<string>(args);
var projectDirIndex = argList.FindIndex(arg => arg.Equals("--project-dir", StringComparison.OrdinalIgnoreCase));
if (projectDirIndex >= 0 && projectDirIndex < argList.Count - 1)
{
    var projectDir = argList[projectDirIndex + 1];
    Environment.SetEnvironmentVariable("MUSICENGINE_PROJECT_DIR", projectDir);
    argList.RemoveAt(projectDirIndex + 1);
    argList.RemoveAt(projectDirIndex);
}
args = argList.ToArray();

if (args.Length > 0 && args[0].Equals("--play-note", StringComparison.OrdinalIgnoreCase))
{
    int note = args.Length > 1 && int.TryParse(args[1], out var n) ? n : 60;
    int velocity = args.Length > 2 && int.TryParse(args[2], out var v) ? v : 100;
    double duration = args.Length > 3 && double.TryParse(args[3], out var d) ? d : 0.6;

    await QuickNotePlayer.PlayOnceAsync(note, velocity, duration);
    return;
}

if (args.Length > 0 && args[0].Equals("--editor", StringComparison.OrdinalIgnoreCase))
{
    await EngineLauncher.LaunchAsync(executeScriptOnStartup: false, startSequencerOnStartup: false,
        startSleeping: false, editorMode: true);
    return;
}

if (args.Length > 0 && args[0].Equals("--ipc", StringComparison.OrdinalIgnoreCase))
{
    using var engine = new EngineScriptInterface(new EngineScriptInterfaceOptions
    {
        StartSequencerOnStartup = true
    });
    await engine.StartupAsync();
    engine.SetEditorMode(true);

    using var server = new EngineIpcServer(engine);
    server.Start();

    Console.WriteLine("Press Ctrl+C to stop.");

    var done = new TaskCompletionSource<bool>();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        done.TrySetResult(true);
    };
    await done.Task;
    return;
}

await EngineLauncher.LaunchAsync();
