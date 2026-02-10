// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Simple one-pole filter effect.

using System;
using NAudio.Wave;

namespace MusicEngine.Effects.Audio;

public enum SimpleFilterType
{
    LowPass,
    HighPass
}

/// <summary>
/// One-pole low/high-pass filter.
/// </summary>
public sealed class SimpleFilterEffect : IAudioEffect
{
    public string Name { get; set; } = "Filter";

    /// <summary>
    /// Filter type.
    /// </summary>
    public SimpleFilterType Type { get; set; } = SimpleFilterType.LowPass;

    /// <summary>
    /// Cutoff frequency in Hz.
    /// </summary>
    public float CutoffHz { get; set; } = 1200f;

    private Processor? _processor;

    public ISampleProvider Attach(ISampleProvider input, WaveFormat targetFormat)
    {
        _processor = new Processor(this, input, targetFormat);
        return _processor;
    }

    public void Detach()
    {
        _processor = null;
    }

    public void Dispose() => Detach();

    private sealed class Processor : ISampleProvider
    {
        private readonly SimpleFilterEffect _owner;
        private readonly ISampleProvider _source;
        private float _stateL;
        private float _stateR;

        public Processor(SimpleFilterEffect owner, ISampleProvider source, WaveFormat targetFormat)
        {
            _owner = owner;
            _source = source;
            WaveFormat = targetFormat;
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            int read = _source.Read(buffer, offset, count);
            if (read <= 0) return read;

            int channels = WaveFormat.Channels;
            float cutoff = Math.Max(0.001f, _owner.CutoffHz);
            float nyquist = WaveFormat.SampleRate * 0.5f;
            float safeCutoff = Math.Min(cutoff, nyquist * 0.98f);
            float rc = 1f / (2f * (float)Math.PI * safeCutoff);
            float dt = 1f / WaveFormat.SampleRate;
            float alpha = dt / (rc + dt);

            if (_owner.Type == SimpleFilterType.LowPass)
            {
                for (int i = offset; i < offset + read; i += channels)
                {
                    float xL = buffer[i];
                    _stateL += (xL - _stateL) * alpha;
                    buffer[i] = _stateL;

                    if (channels > 1)
                    {
                        float xR = buffer[i + 1];
                        _stateR += (xR - _stateR) * alpha;
                        buffer[i + 1] = _stateR;
                    }
                }
            }
            else
            {
                for (int i = offset; i < offset + read; i += channels)
                {
                    float xL = buffer[i];
                    _stateL += (xL - _stateL) * alpha;
                    buffer[i] = xL - _stateL;

                    if (channels > 1)
                    {
                        float xR = buffer[i + 1];
                        _stateR += (xR - _stateR) * alpha;
                        buffer[i + 1] = xR - _stateR;
                    }
                }
            }

            return read;
        }
    }
}
