// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Delay/echo effect processor.

using System;
using NAudio.Wave;


namespace MusicEngine.Core;


/// <summary>
/// A delay/echo effect using a circular buffer implementation.
/// Supports variable delay time with feedback.
/// </summary>
public class DelayEffect : EffectBase
{
    // Maximum delay time in milliseconds
    private const float MaxDelayTimeMs = 5000f;

    // Circular delay buffers (one per channel)
    private readonly float[][] _delayBuffers;
    private readonly int[] _writeIndices;
    private readonly int _maxDelaySamples;

    // Parameters
    private float _delayTimeMs = 500f;
    private float _feedback = 0.5f;
    private int _delaySamples;

    /// <summary>
    /// Creates a new delay effect.
    /// </summary>
    /// <param name="source">The audio source to process</param>
    public DelayEffect(ISampleProvider source) : base(source, "Delay")
    {
        // Register parameters with initial values
        RegisterParameter("DelayTime", _delayTimeMs);
        RegisterParameter("Feedback", _feedback);
        RegisterParameter("Mix", Mix);

        // Calculate maximum buffer size
        _maxDelaySamples = (int)((MaxDelayTimeMs / 1000f) * SampleRate);

        // Initialize delay buffers for each channel
        _delayBuffers = new float[Channels][];
        _writeIndices = new int[Channels];

        for (int ch = 0; ch < Channels; ch++)
        {
            _delayBuffers[ch] = new float[_maxDelaySamples];
            _writeIndices[ch] = 0;
        }

        // Calculate initial delay in samples
        UpdateDelaySamples();
    }

    /// <summary>
    /// Gets or sets the delay time in milliseconds.
    /// </summary>
    public float DelayTime
    {
        get => _delayTimeMs;
        set
        {
            _delayTimeMs = Math.Clamp(value, 1f, MaxDelayTimeMs);
            UpdateDelaySamples();
            SetParameter("DelayTime", _delayTimeMs);
        }
    }

    /// <summary>
    /// Gets or sets the feedback amount (0.0 to 1.0).
    /// Higher values produce more echo repetitions.
    /// </summary>
    public float Feedback
    {
        get => _feedback;
        set
        {
            _feedback = Math.Clamp(value, 0f, 0.99f); // Limit to prevent runaway feedback
            SetParameter("Feedback", _feedback);
        }
    }

    /// <inheritdoc />
    protected override void OnParameterChanged(string name, float value)
    {
        switch (name.ToLower())
        {
            case "delaytime":
                _delayTimeMs = Math.Clamp(value, 1f, MaxDelayTimeMs);
                UpdateDelaySamples();
                break;
            case "feedback":
                _feedback = Math.Clamp(value, 0f, 0.99f);
                break;
            case "mix":
                Mix = value;
                break;
        }
    }

    private void UpdateDelaySamples()
    {
        _delaySamples = (int)((_delayTimeMs / 1000f) * SampleRate);
        _delaySamples = Math.Clamp(_delaySamples, 1, _maxDelaySamples - 1);
    }

    /// <inheritdoc />
    protected override float ProcessSample(float sample, int channel)
    {
        // Calculate read position (behind write position by delay amount)
        int readIndex = _writeIndices[channel] - _delaySamples;
        if (readIndex < 0)
            readIndex += _maxDelaySamples;

        // Read delayed sample
        float delayedSample = _delayBuffers[channel][readIndex];

        // Write to buffer: input + feedback
        _delayBuffers[channel][_writeIndices[channel]] = sample + (delayedSample * _feedback);

        // Advance write position
        _writeIndices[channel]++;
        if (_writeIndices[channel] >= _maxDelaySamples)
            _writeIndices[channel] = 0;

        // Output is the delayed sample (wet signal)
        return delayedSample;
    }

    /// <summary>
    /// Clears the delay buffers.
    /// </summary>
    public void Clear()
    {
        for (int ch = 0; ch < Channels; ch++)
        {
            Array.Clear(_delayBuffers[ch], 0, _delayBuffers[ch].Length);
            _writeIndices[ch] = 0;
        }
    }
}
