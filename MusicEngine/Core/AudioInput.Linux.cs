#if !WINDOWS
// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann
// Description: Linux stub for audio input (not supported yet).

using System;
using NAudio.Wave;

namespace MusicEngine.Core;

public sealed class AudioInput : ISampleProvider, IInstrumentControls, IDisposable
{
    public AudioInput(object device, int deviceIndex)
    {
        throw new PlatformNotSupportedException("Audio input is not yet supported on Linux.");
    }

    public int DeviceIndex => -1;
    public string DeviceId => string.Empty;
    public string Name { get; set; } = "AudioInput";
    public WaveFormat WaveFormat => WaveFormat.CreateIeeeFloatWaveFormat(Settings.SampleRate, Settings.Channels);

    public float Gain { get; set; } = 1f;
    public float gain { get => Gain; set => Gain = value; }
    public bool Mute { get; set; }
    public bool mute { get => Mute; set => Mute = value; }
    public float Volume { get => Gain; set => Gain = value; }
    public float Pan { get; set; }
    public float ModWheel { get; set; }
    public int Channel { get; set; } = -1;
    public float Reverb { get; set; }
    public float Chorus { get; set; }

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
