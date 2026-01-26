// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio/MIDI routing component.

using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using MusicEngine.Core.Effects;

namespace MusicEngine.Core.Routing;

/// <summary>
/// Audio channel - wraps a sample provider with volume, pan, mute, solo, and insert effects.
/// Used for individual instrument/track control in the mixer.
/// </summary>
public class AudioChannel : ISampleProvider
{
    private ISampleProvider _source;
    private readonly List<EffectBase> _insertEffects;
    private readonly float[] _panBuffer;

    /// <summary>
    /// Channel name
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Channel volume (0.0 - 2.0)
    /// </summary>
    public float Volume { get; set; }

    /// <summary>
    /// Channel pan (-1.0 = left, 0.0 = center, 1.0 = right)
    /// Only applies to stereo channels
    /// </summary>
    public float Pan { get; set; }

    /// <summary>
    /// Mute state
    /// </summary>
    public bool Mute { get; set; }

    /// <summary>
    /// Solo state
    /// </summary>
    public bool Solo { get; set; }

    /// <summary>
    /// Fader gain (used for automation and fades)
    /// </summary>
    public float Fader { get; set; }

    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>
    /// Creates a new audio channel
    /// </summary>
    /// <param name="name">Channel name</param>
    /// <param name="source">Audio source</param>
    public AudioChannel(string name, ISampleProvider source)
    {
        Name = name;
        _source = source;
        Volume = 1.0f;
        Pan = 0.0f;
        Mute = false;
        Solo = false;
        Fader = 1.0f;

        _insertEffects = new List<EffectBase>();
        _panBuffer = new float[source.WaveFormat.SampleRate * source.WaveFormat.Channels]; // 1 second buffer
    }

    /// <summary>
    /// Sets a new audio source for this channel
    /// </summary>
    public void SetSource(ISampleProvider source)
    {
        if (source.WaveFormat.SampleRate != WaveFormat.SampleRate ||
            source.WaveFormat.Channels != WaveFormat.Channels)
        {
            throw new ArgumentException("New source wave format must match channel wave format");
        }

        _source = source;
    }

    /// <summary>
    /// Adds an insert effect to this channel.
    /// Note: Effects must be created with the proper source chain.
    /// Example: new FilterEffect(channelSource, "filter")
    /// To chain: new ReverbEffect(new FilterEffect(source, "filter"), "reverb")
    /// </summary>
    public void AddInsertEffect(EffectBase effect)
    {
        // Effects are already constructed with their source
        // We just track them for management purposes
        _insertEffects.Add(effect);
    }

    /// <summary>
    /// Removes an insert effect from this channel
    /// </summary>
    public void RemoveInsertEffect(EffectBase effect)
    {
        _insertEffects.Remove(effect);
    }

    /// <summary>
    /// Clears all insert effects from this channel
    /// </summary>
    public void ClearInsertEffects()
    {
        _insertEffects.Clear();
    }

    /// <summary>
    /// Gets all insert effects on this channel
    /// </summary>
    public IReadOnlyList<EffectBase> InsertEffects => _insertEffects.AsReadOnly();

    public int Read(float[] buffer, int offset, int count)
    {
        // Determine the effective source (last effect in chain or original source)
        ISampleProvider effectiveSource = _source;

        // If we have insert effects, they are already chained
        // The last effect in the list is the output of the chain
        if (_insertEffects.Count > 0)
        {
            effectiveSource = _insertEffects[_insertEffects.Count - 1];
        }

        // Read from the effective source (which includes all effects)
        int samplesRead = effectiveSource.Read(buffer, offset, count);

        // Apply fader, volume, pan, and mute
        int channels = WaveFormat.Channels;
        float leftGain = Volume * Fader;
        float rightGain = Volume * Fader;

        if (channels == 2 && Pan != 0f)
        {
            // Constant power panning law
            float panAngle = (Pan + 1f) * MathF.PI * 0.25f; // -1..1 -> 0..π/2
            leftGain *= MathF.Cos(panAngle);
            rightGain *= MathF.Sin(panAngle);
        }

        if (Mute)
        {
            leftGain = 0f;
            rightGain = 0f;
        }

        for (int i = offset; i < offset + samplesRead; i += channels)
        {
            if (channels == 1)
            {
                buffer[i] *= leftGain;
            }
            else if (channels == 2)
            {
                buffer[i] *= leftGain;      // Left channel
                buffer[i + 1] *= rightGain; // Right channel
            }
        }

        return samplesRead;
    }

    /// <summary>
    /// Helper sample provider that wraps a buffer
    /// </summary>
    private class BufferSampleProvider : ISampleProvider
    {
        private readonly float[] _buffer;
        private readonly int _count;
        private int _position;

        public WaveFormat WaveFormat { get; }

        public BufferSampleProvider(float[] buffer, int count, WaveFormat waveFormat)
        {
            _buffer = buffer;
            _count = count;
            _position = 0;
            WaveFormat = waveFormat;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesToRead = Math.Min(count, _count - _position);
            Array.Copy(_buffer, _position, buffer, offset, samplesToRead);
            _position += samplesToRead;
            return samplesToRead;
        }
    }
}
