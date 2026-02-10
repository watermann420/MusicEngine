// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Simple bit crusher effect.

using System;
using NAudio.Wave;

namespace MusicEngine.Effects.Audio;

/// <summary>
/// Bit crusher with bit depth and downsampling controls.
/// </summary>
public sealed class BitCrusherEffect : IAudioEffect
{
    public string Name { get; set; } = "BitCrusher";

    /// <summary>
    /// Bit depth (1..24 typical).
    /// </summary>
    public int BitDepth { get; set; } = 8;

    /// <summary>
    /// Downsample factor (1 = no downsample).
    /// </summary>
    public int Downsample { get; set; } = 1;

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
        private readonly BitCrusherEffect _owner;
        private readonly ISampleProvider _source;
        private int _counter;
        private float _holdL;
        private float _holdR;

        public Processor(BitCrusherEffect owner, ISampleProvider source, WaveFormat targetFormat)
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
            int downsample = Math.Max(1, _owner.Downsample);
            int bitDepth = Math.Max(1, _owner.BitDepth);
            float mix = _owner.Mix;
            float dry = 1f - mix;
            float levels = bitDepth >= 30 ? (float)Math.Pow(2, bitDepth - 1) : (1 << (bitDepth - 1));
            float invLevels = levels == 0 ? 1f : 1f / levels;

            for (int i = offset; i < offset + read; i += channels)
            {
                if (_counter == 0)
                {
                    _holdL = Quantize(buffer[i], invLevels);
                    if (channels > 1)
                    {
                        _holdR = Quantize(buffer[i + 1], invLevels);
                    }
                }

                buffer[i] = buffer[i] * dry + _holdL * mix;
                if (channels > 1)
                {
                    buffer[i + 1] = buffer[i + 1] * dry + _holdR * mix;
                }

                _counter++;
                if (_counter >= downsample) _counter = 0;
            }

            return read;
        }

        private static float Quantize(float sample, float invLevels)
        {
            float clamped = Math.Clamp(sample, -1f, 1f);
            return MathF.Round(clamped / invLevels) * invLevels;
        }
    }
}
