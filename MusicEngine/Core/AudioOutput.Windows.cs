#if WINDOWS
// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann
// Description: Windows audio output adapters.

using System;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MusicEngine.Core;

internal sealed class WaveOutOutput : IAudioOutput
{
    private readonly WaveOutEvent _output;

    public WaveOutOutput(int latencyMs, int bufferCount)
    {
        _output = new WaveOutEvent
        {
            DesiredLatency = latencyMs,
            NumberOfBuffers = bufferCount
        };
    }

    public void Init(ISampleProvider provider)
    {
        _output.Init(provider);
    }

    public void Play() => _output.Play();

    public void Stop() => _output.Stop();

    public void Dispose() => _output.Dispose();
}

internal sealed class AsioOutput : IAudioOutput
{
    private readonly AsioOut _output;
    private readonly ISampleProvider _provider;

    public AsioOutput(string driverName, ISampleProvider provider)
    {
        _output = new AsioOut(driverName);
        _provider = provider;
    }

    public bool IsSampleRateSupported(int sampleRate)
        => _output.IsSampleRateSupported(sampleRate);

    public void Init(ISampleProvider provider)
    {
        _output.Init(new SampleToWaveProvider(_provider));
    }

    public void Play() => _output.Play();

    public void Stop() => _output.Stop();

    public void Dispose() => _output.Dispose();
}
#endif
