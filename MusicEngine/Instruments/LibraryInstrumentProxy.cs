// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann
// Description: Generic silent instrument stub for missing library instruments.

using System;
using System.Dynamic;
using MusicEngine.Core;
using NAudio.Wave;

namespace MusicEngine.Instruments;

public sealed class LibraryInstrumentProxy : DynamicObject, ISynth
{
    private readonly WaveFormat _waveFormat;
    private bool _logged;

    public LibraryInstrumentProxy(string typeName)
    {
        Name = typeName;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(Settings.SampleRate, Settings.Channels);
    }

    public string Name { get; set; }
    public float Volume { get; set; } = 1f;
    public float Pan { get; set; } = 0f;
    public float ModWheel { get; set; } = 0f;
    public int Channel { get; set; } = -1;
    public float Reverb { get; set; } = 0f;
    public float Chorus { get; set; } = 0f;
    public WaveFormat WaveFormat => _waveFormat;

    public void NoteOn(int note, int velocity) => LogMissing();
    public void NoteOff(int note) => LogMissing();
    public void AllNotesOff()
    {
    }

    public void SetParameter(string name, float value) => LogMissing();

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

    public override bool TryInvokeMember(InvokeMemberBinder binder, object?[] args, out object? result)
    {
        LogMissing();
        result = null;
        return true;
    }

    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        result = null;
        return true;
    }

    public override bool TrySetMember(SetMemberBinder binder, object? value)
    {
        return true;
    }

    private void LogMissing()
    {
        if (_logged) return;
        _logged = true;
        Console.WriteLine($"Script Warning: Library instrument '{Name}' is not available.");
    }
}
