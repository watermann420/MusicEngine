// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Minimal helper to play a single note from the command line.

using System;
using System.Threading.Tasks;
using MusicEngine.Core;
using MusicEngine.Instruments;

namespace MusicEngine.Scripting;

public static class QuickNotePlayer
{
    public static async Task PlayOnceAsync(int note = 60, int velocity = 100, double durationSeconds = 0.6)
    {
        note = Math.Clamp(note, 0, 127);
        velocity = Math.Clamp(velocity, 0, 127);
        durationSeconds = Math.Clamp(durationSeconds, 0.05, 10.0);

        using var engine = new AudioEngine();
        engine.Initialize();

        var sequencer = new Sequencer();
        sequencer.Start();

        var piano = new GeneralMidiInstrument();
        engine.AddSampleProvider(piano);

        piano.NoteOn(note, velocity);
        await Task.Delay(TimeSpan.FromSeconds(durationSeconds));
        piano.NoteOff(note);
        await Task.Delay(150);

        sequencer.Stop();
    }
}
