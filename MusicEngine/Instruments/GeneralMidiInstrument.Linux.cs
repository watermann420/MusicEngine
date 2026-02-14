#if !WINDOWS
// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann
// Description: Linux stub for General MIDI output.

using System;
using MusicEngine.Core;
using NAudio.Wave;

namespace MusicEngine.Instruments;

/// <summary>
/// Silent General MIDI stub for non-Windows platforms.
/// </summary>
public sealed class GeneralMidiInstrument : ISampleProvider, ISynth, IDisposable
{
    public GeneralMidiInstrument() : this(GeneralMidiProgram.AcousticGrandPiano, 0)
    {
    }

    public GeneralMidiInstrument(GeneralMidiProgram program, int channel = 0)
    {
        Program = program;
        Channel = channel;
        Name = $"GM_{program}";
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(Settings.SampleRate, Settings.Channels);
    }

    public string Name { get; set; } = "GM";
    public WaveFormat WaveFormat { get; }
    public GeneralMidiProgram Program { get; set; }
    public int Channel { get; set; }
    public float Volume { get; set; } = 0.8f;
    public float Pan { get; set; }
    public float Reverb { get; set; }
    public float Chorus { get; set; }
    public float ModWheel { get; set; }

    public void PitchBend(float bend)
    {
    }

    public void NoteOn(int note, int velocity)
    {
    }

    public void NoteOff(int note)
    {
    }

    public void AllNotesOff()
    {
    }

    public void SetParameter(string name, float value)
    {
    }

    public Action<float> Param(string name, float min = 0f, float max = 1f)
        => _ => { };

    public int Read(float[] buffer, int offset, int count)
    {
        Array.Clear(buffer, offset, count);
        return count;
    }

    public void Dispose()
    {
    }
}
#endif
