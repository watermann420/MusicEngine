// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Simple reverb effect for audio chains.

using System;
using NAudio.Wave;

namespace MusicEngine.Effects.Audio;

/// <summary>
/// Simple reverb effect for audio chains.
/// </summary>
public sealed class SimpleReverbEffect : IAudioEffect
{
    private readonly object _lock = new();
    private ReverbProcessor? _processor;

    public string Name { get; }

    /// <summary>
    /// Reverb mix (0..1).
    /// </summary>
    public float Mix { get; set; } = 0.15f;

    /// <summary>
    /// Reverb size (0..1).
    /// </summary>
    public float Size { get; set; } = 0.5f;

    /// <summary>
    /// Reverb damping (0..1).
    /// </summary>
    public float Damping { get; set; } = 0.5f;

    public SimpleReverbEffect(string name = "Reverb")
    {
        Name = name;
    }

    public ISampleProvider Attach(ISampleProvider input, WaveFormat targetFormat)
    {
        lock (_lock)
        {
            _processor = new ReverbProcessor(this, input, targetFormat);
            return _processor;
        }
    }

    public void Detach()
    {
        lock (_lock)
        {
            _processor?.Dispose();
            _processor = null;
        }
    }

    public void Dispose() => Detach();

    private sealed class ReverbProcessor : ISampleProvider, IDisposable
    {
        private readonly SimpleReverbEffect _owner;
        private readonly ISampleProvider _input;
        private readonly float[] _buffer;
        private int _writePos;

        public ReverbProcessor(SimpleReverbEffect owner, ISampleProvider input, WaveFormat targetFormat)
        {
            _owner = owner;
            _input = AudioFormatAdapter.EnsureFormat(input, targetFormat.SampleRate, targetFormat.Channels);
            WaveFormat = targetFormat;
            int size = Math.Max(1, targetFormat.SampleRate);
            _buffer = new float[size];
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            int read = _input.Read(buffer, offset, count);
            if (read <= 0) return read;

            float mix = Math.Clamp(_owner.Mix, 0f, 1f);
            if (mix <= 0.0001f) return read;

            float damping = Math.Clamp(_owner.Damping, 0f, 1f);
            float size = Math.Clamp(_owner.Size, 0f, 1f);

            int delaySamples = (int)(size * 15000 + 1000);
            delaySamples = Math.Clamp(delaySamples, 1, _buffer.Length - 1);

            for (int i = 0; i < read; i++)
            {
                int readPos = _writePos - delaySamples;
                if (readPos < 0) readPos += _buffer.Length;

                float delayed = _buffer[readPos];
                float inputSample = buffer[offset + i];
                _buffer[_writePos] = inputSample + delayed * (1f - damping) * 0.6f;

                buffer[offset + i] = inputSample + delayed * mix;

                _writePos++;
                if (_writePos >= _buffer.Length) _writePos = 0;
            }

            return read;
        }

        public void Dispose()
        {
        }
    }
}
