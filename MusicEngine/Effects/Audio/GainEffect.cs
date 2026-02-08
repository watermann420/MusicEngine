// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Simple gain effect.

using NAudio.Wave;

namespace MusicEngine.Effects.Audio;

/// <summary>
/// Simple gain (volume) effect.
/// </summary>
public sealed class GainEffect : IAudioEffect
{
    public string Name { get; set; } = "Gain";

    /// <summary>
    /// Gain multiplier (1 = unity).
    /// </summary>
    public float Gain { get; set; } = 1f;

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
        private readonly GainEffect _owner;
        private readonly ISampleProvider _source;

        public Processor(GainEffect owner, ISampleProvider source, WaveFormat targetFormat)
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

            float gain = _owner.Gain;
            for (int i = offset; i < offset + read; i++)
            {
                buffer[i] *= gain;
            }
            return read;
        }
    }
}
