// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Minimal helper to play a single note from the command line.

using System;
using System.Threading.Tasks;
using MusicEngine.Core;

namespace MusicEngine.Scripting;

/// <summary>
/// Lightweight entry point for one-shot note playback without starting the interactive console UI.
/// </summary>
public static class QuickNotePlayer
{
    /// <summary>
    /// Play a single MIDI note using a General MIDI Acoustic Grand Piano and then exit.
    /// </summary>
    /// <param name="note">MIDI note number (0-127), e.g., 60 = C4.</param>
    /// <param name="velocity">Velocity (0-127).</param>
    /// <param name="durationSeconds">How long to hold the note before note-off.</param>
    public static async Task PlayOnceAsync(int note = 60, int velocity = 100, double durationSeconds = 0.6)
    {
        // Guard arguments
        note = Math.Clamp(note, 0, 127);
        velocity = Math.Clamp(velocity, 0, 127);
        durationSeconds = Math.Clamp(durationSeconds, 0.05, 10.0);

        using var engine = new AudioEngine(sampleRate: null, logger: null);
        engine.Initialize();

        // Use the same sequencer as the interactive mode for consistency
        var sequencer = new Sequencer();
        sequencer.Start();

        // Acoustic Grand Piano (GM program 0) for a piano timbre.
        var piano = new GeneralMidiInstrument(GeneralMidiProgram.AcousticGrandPiano, channel: 0);
        engine.AddSampleProvider(piano);

        piano.NoteOn(note, velocity);
        await Task.Delay(TimeSpan.FromSeconds(durationSeconds));
        piano.NoteOff(note);

        // Let the release tail ring out a bit.
        await Task.Delay(150);

        sequencer.Stop();
    }
}
