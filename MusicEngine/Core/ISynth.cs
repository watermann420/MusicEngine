// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core synth interface.

using NAudio.Wave;

namespace MusicEngine.Core;

public interface ISynth : ISampleProvider
{
    string Name { get; set; }
    void NoteOn(int note, int velocity);
    void NoteOff(int note);
    void AllNotesOff();
    void SetParameter(string name, float value);
}
