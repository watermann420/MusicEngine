//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Base class implementing common effect functionality.


using System;
using System.Collections.Generic;
using NAudio.Wave;
using MusicEngine.Infrastructure.Memory;


namespace MusicEngine.Core;


/// <summary>
/// Base class for audio effects implementing common functionality.
/// Derived classes should override ProcessSample or ProcessBuffer to implement the effect.
/// </summary>
public abstract class EffectBase : IEffect
{
    private readonly ISampleProvider _source;
    private readonly Dictionary<string, float> _parameters = new(StringComparer.OrdinalIgnoreCase);
    private float _mix = 1.0f;
    private bool _enabled = true;
    private float[] _sourceBuffer = Array.Empty<float>();
    private readonly IAudioBufferPool? _bufferPool;

    /// <summary>
    /// Creates a new effect with the specified audio source.
    /// </summary>
    /// <param name="source">The audio source to process</param>
    /// <param name="name">The name of the effect</param>
    protected EffectBase(ISampleProvider source, string name)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        WaveFormat = source.WaveFormat;
    }

    /// <summary>
    /// Creates a new effect with the specified audio source and buffer pool.
    /// </summary>
    /// <param name="source">The audio source to process</param>
    /// <param name="name">The name of the effect</param>
    /// <param name="bufferPool">Optional buffer pool for memory efficiency</param>
    protected EffectBase(ISampleProvider source, string name, IAudioBufferPool? bufferPool)
        : this(source, name)
    {
        _bufferPool = bufferPool;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public WaveFormat WaveFormat { get; }

    /// <inheritdoc />
    public float Mix
    {
        get => _mix;
        set => _mix = Math.Clamp(value, 0f, 1f);
    }

    /// <inheritdoc />
    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    /// <summary>
    /// Gets the source sample provider.
    /// </summary>
    protected ISampleProvider Source => _source;

    /// <summary>
    /// Gets the number of channels in the audio stream.
    /// </summary>
    protected int Channels => WaveFormat.Channels;

    /// <summary>
    /// Gets the sample rate of the audio stream.
    /// </summary>
    protected int SampleRate => WaveFormat.SampleRate;

    /// <inheritdoc />
    public virtual void SetParameter(string name, float value)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        _parameters[name] = value;
        OnParameterChanged(name, value);
    }

    /// <inheritdoc />
    public virtual float GetParameter(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return 0f;

        return _parameters.TryGetValue(name, out var value) ? value : 0f;
    }

    /// <summary>
    /// Called when a parameter value changes.
    /// Override this to react to parameter changes.
    /// </summary>
    /// <param name="name">The parameter name</param>
    /// <param name="value">The new value</param>
    protected virtual void OnParameterChanged(string name, float value)
    {
    }

    /// <summary>
    /// Registers a parameter with an initial value.
    /// </summary>
    /// <param name="name">The parameter name</param>
    /// <param name="initialValue">The initial value</param>
    protected void RegisterParameter(string name, float initialValue)
    {
        _parameters[name] = initialValue;
    }

    /// <inheritdoc />
    public int Read(float[] buffer, int offset, int count)
    {
        // Use buffer pool if available, otherwise use regular allocation
        RentedBuffer<float>? rentedBuffer = null;
        float[] sourceBuffer;

        if (_bufferPool != null)
        {
            rentedBuffer = _bufferPool.Rent(count);
            sourceBuffer = rentedBuffer.Value.Array;
        }
        else
        {
            // Ensure source buffer is large enough (legacy path)
            if (_sourceBuffer.Length < count)
            {
                _sourceBuffer = new float[count];
            }
            sourceBuffer = _sourceBuffer;
        }

        // Read from source
        int samplesRead = _source.Read(sourceBuffer, 0, count);

        if (samplesRead == 0)
        {
            // Return rented buffer to pool
            rentedBuffer?.Dispose();
            return 0;
        }

        // If effect is disabled, just copy the source
        if (!_enabled)
        {
            Array.Copy(sourceBuffer, 0, buffer, offset, samplesRead);
            // Return rented buffer to pool
            rentedBuffer?.Dispose();
            return samplesRead;
        }

        // Process the audio
        ProcessBuffer(sourceBuffer, buffer, offset, samplesRead);

        // Apply dry/wet mix
        if (_mix < 1.0f)
        {
            float dry = 1.0f - _mix;
            for (int i = 0; i < samplesRead; i++)
            {
                buffer[offset + i] = (sourceBuffer[i] * dry) + (buffer[offset + i] * _mix);
            }
        }

        // Return rented buffer to pool
        rentedBuffer?.Dispose();

        return samplesRead;
    }

    /// <summary>
    /// Processes a buffer of audio samples.
    /// Override this method to implement the effect's audio processing.
    /// </summary>
    /// <param name="sourceBuffer">The source samples (dry signal)</param>
    /// <param name="destBuffer">The destination buffer to write processed samples to</param>
    /// <param name="offset">The offset into the destination buffer</param>
    /// <param name="count">The number of samples to process</param>
    protected virtual void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        // Default implementation processes sample by sample
        for (int i = 0; i < count; i++)
        {
            destBuffer[offset + i] = ProcessSample(sourceBuffer[i], i % Channels);
        }
    }

    /// <summary>
    /// Processes a single audio sample.
    /// Override this method for simple per-sample effects.
    /// </summary>
    /// <param name="sample">The input sample</param>
    /// <param name="channel">The channel index (0 for left, 1 for right in stereo)</param>
    /// <returns>The processed sample</returns>
    protected virtual float ProcessSample(float sample, int channel)
    {
        return sample;
    }
}
