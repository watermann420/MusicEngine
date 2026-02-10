// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Custom audio effect wrapper for scripted processing.

using System;
using NAudio.Wave;

namespace MusicEngine.Effects.Audio;

/// <summary>
/// Custom effect that calls a user-provided processor on each buffer.
/// </summary>
public sealed class CustomEffect : IAudioEffect
{
    private readonly Action<float[], int, int, WaveFormat> _process;
    private Processor? _processor;

    public CustomEffect(Action<float[], int, int, WaveFormat> process, string name = "CustomEffect")
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
        Name = name;
    }

    public string Name { get; }

    public ISampleProvider Attach(ISampleProvider input, WaveFormat targetFormat)
    {
        _processor = new Processor(input, targetFormat, _process);
        return _processor;
    }

    public void Detach()
    {
        _processor = null;
    }

    public void Dispose() => Detach();

    private sealed class Processor : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly Action<float[], int, int, WaveFormat> _process;

        public Processor(ISampleProvider source, WaveFormat targetFormat,
            Action<float[], int, int, WaveFormat> process)
        {
            _source = source;
            _process = process;
            WaveFormat = targetFormat;
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            int read = _source.Read(buffer, offset, count);
            if (read > 0)
            {
                _process(buffer, offset, read, WaveFormat);
            }
            return read;
        }
    }
}
