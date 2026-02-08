// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Simple drive/saturation effect.

using System;
using NAudio.Wave;

namespace MusicEngine.Effects.Audio;

/// <summary>
/// Simple drive/saturation effect using tanh.
/// </summary>
public sealed class DriveEffect : IAudioEffect
{
    public string Name { get; set; } = "Drive";

    /// <summary>
    /// Drive amount (0..inf).
    /// </summary>
    public float Drive { get; set; } = 1f;

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
        private readonly DriveEffect _owner;
        private readonly ISampleProvider _source;

        public Processor(DriveEffect owner, ISampleProvider source, WaveFormat targetFormat)
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

            float drive = _owner.Drive;
            float mix = _owner.Mix;
            float dry = 1f - mix;

            for (int i = offset; i < offset + read; i++)
            {
                float x = buffer[i];
                float wet = (float)Math.Tanh(x * drive);
                buffer[i] = x * dry + wet * mix;
            }

            return read;
        }
    }
}
