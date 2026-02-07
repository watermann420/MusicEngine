// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core synth interface.

using NAudio.Wave;

namespace MusicEngine.Core;

/// <summary>
/// Minimal synth interface for note playback and parameter control.
/// </summary>
public interface ISynth : ISampleProvider
{
    /// <summary>
    /// Display name for the synth instance.
    /// </summary>
    string Name { get; set; }

    /// <summary>
    /// Trigger a MIDI note-on.
    /// </summary>
    /// <param name="note">MIDI note number (0-127).</param>
    /// <param name="velocity">MIDI velocity (0-127).</param>
    void NoteOn(int note, int velocity);

    /// <summary>
    /// Trigger a MIDI note-off.
    /// </summary>
    /// <param name="note">MIDI note number (0-127).</param>
    void NoteOff(int note);

    /// <summary>
    /// Immediately stop all notes on the synth.
    /// </summary>
    void AllNotesOff();

    /// <summary>
    /// Set a named parameter on the synth.
    /// </summary>
    /// <param name="name">Parameter name.</param>
    /// <param name="value">Normalized or raw value (implementation-specific).</param>
    void SetParameter(string name, float value);
}
