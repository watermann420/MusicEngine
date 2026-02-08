// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Simple delay effect for audio chains.

using System;
using NAudio.Wave;

namespace MusicEngine.Effects.Audio;

/// <summary>
/// Simple delay effect for audio chains.
/// </summary>
public sealed class SimpleDelayEffect : IAudioEffect
{
    private readonly object _lock = new();
    private DelayProcessor? _processor;

    public string Name { get; }

    /// <summary>
    /// Delay mix (0..1).
    /// </summary>
    public float Mix { get; set; } = 0f;

    /// <summary>
    /// Delay time in milliseconds.
    /// </summary>
    public float TimeMs { get; set; } = 300f;

    /// <summary>
    /// Delay feedback (0..0.95).
    /// </summary>
    public float Feedback { get; set; } = 0.4f;

    public SimpleDelayEffect(string name = "Delay")
    {
        Name = name;
    }

    public ISampleProvider Attach(ISampleProvider input, WaveFormat targetFormat)
    {
        lock (_lock)
        {
            _processor = new DelayProcessor(this, input, targetFormat);
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

    private sealed class DelayProcessor : ISampleProvider, IDisposable
    {
        private readonly SimpleDelayEffect _owner;
        private readonly ISampleProvider _input;
        private readonly float[] _buffer;
        private int _writePos;

        public DelayProcessor(SimpleDelayEffect owner, ISampleProvider input, WaveFormat targetFormat)
        {
            _owner = owner;
            _input = AudioFormatAdapter.EnsureFormat(input, targetFormat.SampleRate, targetFormat.Channels);
            WaveFormat = targetFormat;

            int maxDelaySamples = Math.Max(1, targetFormat.SampleRate * targetFormat.Channels * 2);
            _buffer = new float[maxDelaySamples];
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            int read = _input.Read(buffer, offset, count);
            if (read <= 0) return read;

            float mix = Math.Clamp(_owner.Mix, 0f, 1f);
            if (mix <= 0.0001f) return read;

            float feedback = Math.Clamp(_owner.Feedback, 0f, 0.95f);
            int channels = WaveFormat.Channels;
            int delaySamples = Math.Clamp((int)(_owner.TimeMs * WaveFormat.SampleRate / 1000f) * channels, 1,
                _buffer.Length - 1);

            for (int i = 0; i < read; i++)
            {
                int readPos = _writePos - delaySamples;
                if (readPos < 0) readPos += _buffer.Length;

                float delayed = _buffer[readPos];
                float inputSample = buffer[offset + i];
                _buffer[_writePos] = inputSample + delayed * feedback;

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
