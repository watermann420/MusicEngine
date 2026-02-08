// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Silent VST placeholder when a plugin is missing.

using System;
using MusicEngine.Core;
using NAudio.Wave;

namespace MusicEngine.Vst;

/// <summary>
/// Silent VST placeholder when a plugin is missing.
/// </summary>
public sealed class MissingVstInstrument : IVstInstrument, IDisposable
{
    private readonly WaveFormat _waveFormat;

    public MissingVstInstrument(string name)
    {
        Name = name;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(Settings.SampleRate, 2);
    }

    public string Name { get; set; }
    public float Volume { get; set; } = 1f;
    public float Pan { get; set; } = 0f;
    public float ModWheel { get; set; } = 0f;
    public int Channel { get; set; } = -1;
    public float Reverb { get; set; } = 0f;
    public float Chorus { get; set; } = 0f;
    public WaveFormat WaveFormat => _waveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || count < 0 || offset + count > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (count == 0) return 0;
        Array.Clear(buffer, offset, count);
        return count;
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

    public void SetParameterNormalized(string name, float value)
    {
    }

    public void OpenEditor()
    {
    }

    public void PitchBend(float normalized)
    {
    }

    public void ResetState()
    {
    }

    public Action<float> Param(string name, float min = 0f, float max = 1f)
    {
        return _ => { };
    }

    public byte[] GetState()
    {
        return Array.Empty<byte>();
    }

    public void SetState(byte[] data)
    {
    }

    public void SaveState(string path)
    {
    }

    public void LoadState(string path)
    {
    }

    public void SaveStateNow()
    {
    }

    public void NoSave()
    {
    }

    public void Dispose()
    {
    }
}
