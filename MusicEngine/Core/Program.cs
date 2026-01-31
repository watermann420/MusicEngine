// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using MusicEngine.Scripting;

// CLI fast path: play a single note and exit
if (args.Length > 0 && args[0].Equals("--play-note", StringComparison.OrdinalIgnoreCase))
{
    int note = args.Length > 1 && int.TryParse(args[1], out var n) ? n : 60;
    int velocity = args.Length > 2 && int.TryParse(args[2], out var v) ? v : 100;
    double duration = args.Length > 3 && double.TryParse(args[3], out var d) ? d : 0.6;

    await QuickNotePlayer.PlayOnceAsync(note, velocity, duration);
    return;
}

await EngineLauncher.LaunchAsync(); // Start the Music Engine application
