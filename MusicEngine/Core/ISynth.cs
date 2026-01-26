// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using NAudio.Wave;


namespace MusicEngine.Core;


public interface ISynth : ISampleProvider
{
    string Name { get; set; }
    void NoteOn(int note, int velocity); // velocity 0-127
    void NoteOff(int note); // velocity 0-127
    void AllNotesOff(); // Stops all currently playing notes
    void SetParameter(string name, float value); // Sets a synthesizer parameter by name
}
