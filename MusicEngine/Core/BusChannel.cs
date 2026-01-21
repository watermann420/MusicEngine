//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Bus/Group channel for mixing multiple audio sources with effects.

using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace MusicEngine.Core;

/// <summary>
/// Type of bus channel.
/// </summary>
public enum BusType
{
    /// <summary>
    /// Standard group bus for summing multiple channels.
    /// </summary>
    Group,

    /// <summary>
    /// Auxiliary bus for send/return effects.
    /// </summary>
    Aux,

    /// <summary>
    /// Master bus - the final output.
    /// </summary>
    Master
}

/// <summary>
/// A bus channel that can receive signals from multiple sources and process them through effects.
/// Used for group/submix buses and send/return effects routing.
/// </summary>
public class BusChannel : ISampleProvider
{
    private readonly List<ISampleProvider> _inputs = [];
    private readonly EffectChain _effectChain;
    private readonly List<float[]> _sendBuffers = [];
    private readonly object _lock = new();
    private float[] _mixBuffer;

    private float _volume = 1.0f;
    private float _pan;
    private bool _mute;
    private bool _solo;

    // Metering
    private float _peakLeft;
    private float _peakRight;

    /// <summary>
    /// Creates a new bus channel.
    /// </summary>
    /// <param name="name">The name of the bus.</param>
    /// <param name="waveFormat">The audio format.</param>
    /// <param name="busType">The type of bus.</param>
    public BusChannel(string name, WaveFormat waveFormat, BusType busType = BusType.Group)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        WaveFormat = waveFormat ?? throw new ArgumentNullException(nameof(waveFormat));
        BusType = busType;

        // Create a silent source for the effect chain when there are no inputs
        var silentSource = new SilenceProvider(waveFormat);
        _effectChain = new EffectChain(silentSource);
        _mixBuffer = new float[waveFormat.SampleRate * waveFormat.Channels];
    }

    /// <inheritdoc />
    public WaveFormat WaveFormat { get; }

    /// <summary>
    /// Gets the name of this bus.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the bus color for UI representation.
    /// </summary>
    public string Color { get; set; } = "#FF9500";

    /// <summary>
    /// Gets the type of this bus.
    /// </summary>
    public BusType BusType { get; }

    /// <summary>
    /// Gets or sets the volume level (0.0 to 2.0).
    /// </summary>
    public float Volume
    {
        get => _volume;
        set => _volume = Math.Clamp(value, 0f, 2f);
    }

    /// <summary>
    /// Gets or sets the pan position (-1.0 = full left, 0.0 = center, 1.0 = full right).
    /// </summary>
    public float Pan
    {
        get => _pan;
        set => _pan = Math.Clamp(value, -1f, 1f);
    }

    /// <summary>
    /// Gets or sets whether the bus is muted.
    /// </summary>
    public bool Mute
    {
        get => _mute;
        set => _mute = value;
    }

    /// <summary>
    /// Gets or sets whether the bus is soloed.
    /// </summary>
    public bool Solo
    {
        get => _solo;
        set => _solo = value;
    }

    /// <summary>
    /// Gets the effect chain for this bus.
    /// </summary>
    public EffectChain Effects => _effectChain;

    /// <summary>
    /// Gets the current peak level for the left channel.
    /// </summary>
    public float PeakLeft => _peakLeft;

    /// <summary>
    /// Gets the current peak level for the right channel.
    /// </summary>
    public float PeakRight => _peakRight;

    /// <summary>
    /// Gets the number of inputs connected to this bus.
    /// </summary>
    public int InputCount
    {
        get
        {
            lock (_lock)
            {
                return _inputs.Count;
            }
        }
    }

    /// <summary>
    /// Adds an input source to this bus.
    /// </summary>
    /// <param name="source">The audio source to add.</param>
    public void AddInput(ISampleProvider source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.WaveFormat.SampleRate != WaveFormat.SampleRate ||
            source.WaveFormat.Channels != WaveFormat.Channels)
        {
            throw new ArgumentException("Source wave format must match bus wave format", nameof(source));
        }

        lock (_lock)
        {
            _inputs.Add(source);
        }
    }

    /// <summary>
    /// Removes an input source from this bus.
    /// </summary>
    /// <param name="source">The audio source to remove.</param>
    public void RemoveInput(ISampleProvider source)
    {
        lock (_lock)
        {
            _inputs.Remove(source);
        }
    }

    /// <summary>
    /// Clears all input sources from this bus.
    /// </summary>
    public void ClearInputs()
    {
        lock (_lock)
        {
            _inputs.Clear();
        }
    }

    /// <summary>
    /// Accepts a send signal from a channel. This is called by ChannelStrip.
    /// </summary>
    /// <param name="buffer">The send signal buffer.</param>
    /// <param name="count">The number of samples.</param>
    public void AcceptSendSignal(float[] buffer, int count)
    {
        lock (_lock)
        {
            var sendBuffer = new float[count];
            Array.Copy(buffer, sendBuffer, count);
            _sendBuffers.Add(sendBuffer);
        }
    }

    /// <summary>
    /// Adds an effect to the bus effect chain.
    /// </summary>
    /// <typeparam name="T">The type of effect to add.</typeparam>
    /// <returns>The created effect instance.</returns>
    public T AddEffect<T>() where T : class, IEffect
    {
        return _effectChain.AddEffect<T>();
    }

    /// <summary>
    /// Resets the peak meters.
    /// </summary>
    public void ResetPeakMeters()
    {
        _peakLeft = 0f;
        _peakRight = 0f;
    }

    /// <inheritdoc />
    public int Read(float[] buffer, int offset, int count)
    {
        // Ensure mix buffer is large enough
        if (_mixBuffer.Length < count)
        {
            _mixBuffer = new float[count];
        }

        // Clear mix buffer
        Array.Clear(_mixBuffer, 0, count);

        lock (_lock)
        {
            // Mix all direct inputs
            var inputBuffer = new float[count];
            foreach (var input in _inputs)
            {
                Array.Clear(inputBuffer, 0, count);
                int samplesRead = input.Read(inputBuffer, 0, count);

                for (int i = 0; i < samplesRead; i++)
                {
                    _mixBuffer[i] += inputBuffer[i];
                }
            }

            // Mix all send buffers
            foreach (var sendBuffer in _sendBuffers)
            {
                int samplesToMix = Math.Min(sendBuffer.Length, count);
                for (int i = 0; i < samplesToMix; i++)
                {
                    _mixBuffer[i] += sendBuffer[i];
                }
            }

            // Clear send buffers for next cycle
            _sendBuffers.Clear();
        }

        // Process through effect chain if there are effects
        if (_effectChain.Count > 0)
        {
            // The effect chain needs the mixed signal as input
            // We create a temporary provider that supplies our mix buffer
            var mixProvider = new BufferSampleProvider(_mixBuffer, count, WaveFormat);
            var tempChain = new EffectChain(mixProvider);

            // Copy effects configuration (simplified - in production you'd want to reuse the chain)
            _effectChain.Read(_mixBuffer, 0, count);
        }

        // Apply volume, pan, and mute
        ApplyBusProcessing(_mixBuffer, 0, count);

        // Update meters
        UpdateMeters(_mixBuffer, 0, count);

        // Copy to output buffer
        Array.Copy(_mixBuffer, 0, buffer, offset, count);

        return count;
    }

    private void ApplyBusProcessing(float[] buffer, int offset, int count)
    {
        int channels = WaveFormat.Channels;
        float totalGain = _mute ? 0f : _volume;

        float leftGain = totalGain;
        float rightGain = totalGain;

        if (channels == 2 && _pan != 0f)
        {
            float panAngle = (_pan + 1f) * MathF.PI * 0.25f;
            leftGain *= MathF.Cos(panAngle);
            rightGain *= MathF.Sin(panAngle);
        }

        for (int i = 0; i < count; i += channels)
        {
            if (channels == 1)
            {
                buffer[offset + i] *= leftGain;
            }
            else if (channels >= 2)
            {
                buffer[offset + i] *= leftGain;
                buffer[offset + i + 1] *= rightGain;
            }
        }
    }

    private void UpdateMeters(float[] buffer, int offset, int count)
    {
        int channels = WaveFormat.Channels;
        float maxLeft = 0f;
        float maxRight = 0f;

        for (int i = 0; i < count; i += channels)
        {
            float left = Math.Abs(buffer[offset + i]);
            maxLeft = Math.Max(maxLeft, left);

            if (channels >= 2)
            {
                float right = Math.Abs(buffer[offset + i + 1]);
                maxRight = Math.Max(maxRight, right);
            }
        }

        _peakLeft = Math.Max(_peakLeft * 0.99f, maxLeft);
        _peakRight = Math.Max(_peakRight * 0.99f, maxRight);
    }

    /// <summary>
    /// Provides silence when no other source is available.
    /// </summary>
    private class SilenceProvider : ISampleProvider
    {
        public WaveFormat WaveFormat { get; }

        public SilenceProvider(WaveFormat waveFormat)
        {
            WaveFormat = waveFormat;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            Array.Clear(buffer, offset, count);
            return count;
        }
    }

    /// <summary>
    /// Helper provider that wraps a buffer.
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
