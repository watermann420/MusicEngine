// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Simple tremolo (amplitude modulation) effect.

using System;
using NAudio.Wave;

namespace MusicEngine.Effects.Audio;

/// <summary>
/// Simple tremolo (amplitude modulation) effect.
/// </summary>
public sealed class TremoloEffect : IAudioEffect
{
    private TremoloProcessor? _processor;

    public string Name { get; }

    /// <summary>
    /// Modulation depth (0..1).
    /// </summary>
    public float Depth { get; set; } = 0.5f;

    /// <summary>
    /// LFO rate in Hz.
    /// </summary>
    public float Rate { get; set; } = 4f;

    public TremoloEffect(string name = "Tremolo")
    {
        Name = name;
    }

    public ISampleProvider Attach(ISampleProvider input, WaveFormat targetFormat)
    {
        _processor = new TremoloProcessor(this, input, targetFormat);
        return _processor;
    }

    public void Detach()
    {
        _processor = null;
    }

    public void Dispose() => Detach();

    private sealed class TremoloProcessor : ISampleProvider
    {
        private readonly TremoloEffect _owner;
        private readonly ISampleProvider _input;
        private double _phase;

        public TremoloProcessor(TremoloEffect owner, ISampleProvider input, WaveFormat targetFormat)
        {
            _owner = owner;
            _input = AudioFormatAdapter.EnsureFormat(input, targetFormat.SampleRate, targetFormat.Channels);
            WaveFormat = targetFormat;
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            int read = _input.Read(buffer, offset, count);
            if (read <= 0) return read;

            float depth = _owner.Depth;
            float rate = Math.Abs(_owner.Rate);
            double phaseInc = rate <= 0f ? 0.0 : (Math.PI * 2.0) * rate / WaveFormat.SampleRate;

            for (int i = 0; i < read; i++)
            {
                float lfo = (float)(Math.Sin(_phase) * 0.5 + 0.5);
                float gain = 1f - depth + depth * lfo;
                buffer[offset + i] *= gain;
                _phase += phaseInc;
                if (_phase >= Math.PI * 2.0) _phase -= Math.PI * 2.0;
            }

            return read;
        }
    }
}
