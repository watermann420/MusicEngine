// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio bus routing and mixing.

using NAudio.Wave;
using MusicEngine.Core.Effects;

namespace MusicEngine.Core.Routing;

/// <summary>
/// Audio bus - a mixing point for multiple audio sources with effects.
/// Can be used for grouping instruments, creating submixes, or routing to effects.
/// </summary>
public class AudioBus : ISampleProvider
{
    private readonly List<ISampleProvider> _inputs;
    private readonly List<EffectBase> _insertEffects;
    private readonly List<Send> _sends;
    private readonly float[] _mixBuffer;

    /// <summary>
    /// Bus name
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Bus volume (0.0 - 2.0)
    /// </summary>
    public float Volume { get; set; }

    /// <summary>
    /// Bus pan (-1.0 = left, 0.0 = center, 1.0 = right)
    /// Only applies to stereo buses
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

    public WaveFormat WaveFormat { get; }

    /// <summary>
    /// Creates a new audio bus
    /// </summary>
    /// <param name="name">Bus name</param>
    /// <param name="waveFormat">Audio format</param>
    public AudioBus(string name, WaveFormat waveFormat)
    {
        Name = name;
        WaveFormat = waveFormat;
        Volume = 1.0f;
        Pan = 0.0f;
        Mute = false;
        Solo = false;

        _inputs = new List<ISampleProvider>();
        _insertEffects = new List<EffectBase>();
        _sends = new List<Send>();
        _mixBuffer = new float[waveFormat.SampleRate * waveFormat.Channels]; // 1 second buffer
    }

    /// <summary>
    /// Adds an input source to this bus
    /// </summary>
    public void AddInput(ISampleProvider source)
    {
        if (source.WaveFormat.SampleRate != WaveFormat.SampleRate ||
            source.WaveFormat.Channels != WaveFormat.Channels)
        {
            throw new ArgumentException("Source wave format must match bus wave format");
        }

        _inputs.Add(source);
    }

    /// <summary>
    /// Removes an input source from this bus
    /// </summary>
    public void RemoveInput(ISampleProvider source)
    {
        _inputs.Remove(source);
    }

    /// <summary>
    /// Adds an insert effect to this bus
    /// Insert effects are applied in series after mixing all inputs
    /// </summary>
    public void AddInsertEffect(EffectBase effect)
    {
        _insertEffects.Add(effect);
    }

    /// <summary>
    /// Removes an insert effect from this bus
    /// </summary>
    public void RemoveInsertEffect(EffectBase effect)
    {
        _insertEffects.Remove(effect);
    }

    /// <summary>
    /// Adds a send to another bus (for parallel effects routing)
    /// </summary>
    /// <param name="targetBus">Destination bus</param>
    /// <param name="sendLevel">Send level (0.0 - 1.0)</param>
    public void AddSend(AudioBus targetBus, float sendLevel)
    {
        _sends.Add(new Send { TargetBus = targetBus, Level = sendLevel });
    }

    /// <summary>
    /// Removes a send to another bus
    /// </summary>
    public void RemoveSend(AudioBus targetBus)
    {
        _sends.RemoveAll(s => s.TargetBus == targetBus);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        // Clear mix buffer
        Array.Clear(_mixBuffer, 0, count);

        // Mix all inputs
        float[] inputBuffer = new float[count];
        foreach (var input in _inputs)
        {
            int samplesRead = input.Read(inputBuffer, 0, count);

            for (int i = 0; i < samplesRead; i++)
            {
                _mixBuffer[i] += inputBuffer[i];
            }
        }

        // If there are insert effects, read through the effect chain instead
        // Note: Effects should be pre-chained when added to the bus
        if (_insertEffects.Count > 0)
        {
            // The last effect in the chain is the output
            ISampleProvider effectOutput = _insertEffects[_insertEffects.Count - 1];
            effectOutput.Read(_mixBuffer, 0, count);
        }

        // Apply volume, pan, and mute
        int channels = WaveFormat.Channels;
        float leftGain = Volume;
        float rightGain = Volume;

        if (channels == 2 && Pan != 0f)
        {
            // Constant power panning
            float panAngle = (Pan + 1f) * MathF.PI * 0.25f; // -1..1 -> 0..π/2
            leftGain *= MathF.Cos(panAngle);
            rightGain *= MathF.Sin(panAngle);
        }

        if (Mute)
        {
            leftGain = 0f;
            rightGain = 0f;
        }

        for (int i = 0; i < count; i += channels)
        {
            if (channels == 1)
            {
                _mixBuffer[i] *= leftGain;
            }
            else if (channels == 2)
            {
                _mixBuffer[i] *= leftGain;
                _mixBuffer[i + 1] *= rightGain;
            }
        }

        // Process sends (parallel routing)
        foreach (var send in _sends)
        {
            float[] sendBuffer = new float[count];
            Array.Copy(_mixBuffer, sendBuffer, count);

            // Apply send level
            for (int i = 0; i < count; i++)
            {
                sendBuffer[i] *= send.Level;
            }

            // Add to target bus input
            send.TargetBus.AddInput(new BufferSampleProvider(sendBuffer, count, WaveFormat));
        }

        // Copy to output buffer
        Array.Copy(_mixBuffer, 0, buffer, offset, count);

        return count;
    }

    /// <summary>
    /// Send configuration
    /// </summary>
    private class Send
    {
        public required AudioBus TargetBus { get; set; }
        public float Level { get; set; }
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
