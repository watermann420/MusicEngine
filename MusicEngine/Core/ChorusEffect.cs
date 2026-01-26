// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Chorus effect processor.

using System;
using NAudio.Wave;


namespace MusicEngine.Core;


/// <summary>
/// A chorus effect that uses an LFO-modulated delay line to create
/// a rich, detuned sound by mixing the original signal with a
/// pitch-modulated copy.
/// </summary>
public class ChorusEffect : EffectBase
{
    // Chorus delay parameters (in milliseconds)
    private const float BaseDelayMs = 7f;
    private const float MaxModulationMs = 5f;

    // Delay buffers (one per channel)
    private readonly float[][] _delayBuffers;
    private readonly int[] _writeIndices;
    private readonly int _bufferSize;

    // LFO state (one per channel for stereo width)
    private readonly double[] _lfoPhases;

    // Parameters
    private float _rate = 1.0f;       // LFO rate in Hz
    private float _depth = 0.5f;      // Modulation depth (0-1)
    private double _lfoIncrement;

    /// <summary>
    /// Creates a new chorus effect.
    /// </summary>
    /// <param name="source">The audio source to process</param>
    public ChorusEffect(ISampleProvider source) : base(source, "Chorus")
    {
        // Register parameters with initial values
        RegisterParameter("Rate", _rate);
        RegisterParameter("Depth", _depth);
        RegisterParameter("Mix", Mix);

        // Calculate buffer size for maximum delay
        float maxDelayMs = BaseDelayMs + MaxModulationMs;
        _bufferSize = (int)((maxDelayMs / 1000f) * SampleRate) + 1;

        // Initialize delay buffers for each channel
        _delayBuffers = new float[Channels][];
        _writeIndices = new int[Channels];
        _lfoPhases = new double[Channels];

        for (int ch = 0; ch < Channels; ch++)
        {
            _delayBuffers[ch] = new float[_bufferSize];
            _writeIndices[ch] = 0;
            // Offset phases for stereo width (90 degrees apart)
            _lfoPhases[ch] = ch * (Math.PI / 2.0);
        }

        // Calculate LFO increment per sample
        UpdateLfoIncrement();
    }

    /// <summary>
    /// Gets or sets the LFO rate in Hz (0.1 to 10.0).
    /// Controls how fast the pitch modulation oscillates.
    /// </summary>
    public float Rate
    {
        get => _rate;
        set
        {
            _rate = Math.Clamp(value, 0.1f, 10f);
            UpdateLfoIncrement();
            SetParameter("Rate", _rate);
        }
    }

    /// <summary>
    /// Gets or sets the modulation depth (0.0 to 1.0).
    /// Controls the intensity of the pitch modulation.
    /// </summary>
    public float Depth
    {
        get => _depth;
        set
        {
            _depth = Math.Clamp(value, 0f, 1f);
            SetParameter("Depth", _depth);
        }
    }

    /// <inheritdoc />
    protected override void OnParameterChanged(string name, float value)
    {
        switch (name.ToLower())
        {
            case "rate":
                _rate = Math.Clamp(value, 0.1f, 10f);
                UpdateLfoIncrement();
                break;
            case "depth":
                _depth = Math.Clamp(value, 0f, 1f);
                break;
            case "mix":
                Mix = value;
                break;
        }
    }

    private void UpdateLfoIncrement()
    {
        _lfoIncrement = (2.0 * Math.PI * _rate) / SampleRate;
    }

    /// <inheritdoc />
    protected override float ProcessSample(float sample, int channel)
    {
        // Write current sample to delay buffer
        _delayBuffers[channel][_writeIndices[channel]] = sample;

        // Calculate LFO modulation value (sine wave, 0 to 1)
        double lfoValue = (Math.Sin(_lfoPhases[channel]) + 1.0) * 0.5;

        // Calculate modulated delay time in samples
        float modulationMs = (float)(lfoValue * MaxModulationMs * _depth);
        float totalDelayMs = BaseDelayMs + modulationMs;
        float delaySamples = (totalDelayMs / 1000f) * SampleRate;

        // Calculate read position with fractional delay (linear interpolation)
        float readPos = _writeIndices[channel] - delaySamples;
        if (readPos < 0)
            readPos += _bufferSize;

        // Linear interpolation for fractional delay
        int readIndex1 = (int)readPos;
        int readIndex2 = readIndex1 + 1;
        if (readIndex2 >= _bufferSize)
            readIndex2 = 0;

        float fraction = readPos - readIndex1;
        float delayedSample = _delayBuffers[channel][readIndex1] * (1f - fraction) +
                              _delayBuffers[channel][readIndex2] * fraction;

        // Advance write position
        _writeIndices[channel]++;
        if (_writeIndices[channel] >= _bufferSize)
            _writeIndices[channel] = 0;

        // Advance LFO phase
        _lfoPhases[channel] += _lfoIncrement;
        if (_lfoPhases[channel] >= 2.0 * Math.PI)
            _lfoPhases[channel] -= 2.0 * Math.PI;

        // Output is the delayed/modulated sample (wet signal)
        return delayedSample;
    }

    /// <summary>
    /// Clears the delay buffers and resets LFO phases.
    /// </summary>
    public void Clear()
    {
        for (int ch = 0; ch < Channels; ch++)
        {
            Array.Clear(_delayBuffers[ch], 0, _delayBuffers[ch].Length);
            _writeIndices[ch] = 0;
            _lfoPhases[ch] = ch * (Math.PI / 2.0);
        }
    }
}
