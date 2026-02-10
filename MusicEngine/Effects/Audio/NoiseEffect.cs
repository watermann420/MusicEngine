// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Simple noise effect.

using System;
using NAudio.Wave;

namespace MusicEngine.Effects.Audio;

/// <summary>
/// Adds white noise to the signal.
/// </summary>
public sealed class NoiseEffect : IAudioEffect
{
    public string Name { get; set; } = "Noise";

    /// <summary>
    /// Noise amount (0..1 typical).
    /// </summary>
    public float Amount { get; set; } = 0.05f;

    /// <summary>
    /// Dry/Wet mix (0..1 typical).
    /// </summary>
    public float Mix { get; set; } = 1f;

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
        private readonly NoiseEffect _owner;
        private readonly ISampleProvider _source;
        private readonly Random _random = new();

        public Processor(NoiseEffect owner, ISampleProvider source, WaveFormat targetFormat)
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

            float amount = _owner.Amount;
            float mix = _owner.Mix;
            float dry = 1f - mix;

            for (int i = offset; i < offset + read; i++)
            {
                float noise = ((float)_random.NextDouble() * 2f - 1f) * amount;
                float wet = buffer[i] + noise;
                buffer[i] = buffer[i] * dry + wet * mix;
            }
            return read;
        }
    }
}
