// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Reverb effect processor.

using System;
using NAudio.Wave;


namespace MusicEngine.Core;


/// <summary>
/// A reverb effect implementation using the Schroeder reverb algorithm.
/// Uses parallel comb filters followed by series allpass filters.
/// </summary>
public class ReverbEffect : EffectBase
{
    // Schroeder reverb constants - delay times in samples at 44100 Hz (scaled for actual sample rate)
    private static readonly int[] CombDelayTimes = { 1557, 1617, 1491, 1422, 1277, 1356, 1188, 1116 };
    private static readonly int[] AllpassDelayTimes = { 225, 556, 441, 341 };

    // Comb filters (one set per channel)
    private readonly CombFilter[][] _combFilters;

    // Allpass filters (one set per channel)
    private readonly AllpassFilter[][] _allpassFilters;

    // Parameters
    private float _roomSize = 0.5f;
    private float _damping = 0.5f;

    /// <summary>
    /// Creates a new reverb effect.
    /// </summary>
    /// <param name="source">The audio source to process</param>
    public ReverbEffect(ISampleProvider source) : base(source, "Reverb")
    {
        // Register parameters with initial values
        RegisterParameter("RoomSize", _roomSize);
        RegisterParameter("Damping", _damping);
        RegisterParameter("Mix", Mix);

        // Calculate delay scaling factor for sample rate
        float delayScale = SampleRate / 44100f;

        // Initialize comb filters for each channel
        _combFilters = new CombFilter[Channels][];
        for (int ch = 0; ch < Channels; ch++)
        {
            _combFilters[ch] = new CombFilter[CombDelayTimes.Length];
            for (int i = 0; i < CombDelayTimes.Length; i++)
            {
                int delayLength = (int)(CombDelayTimes[i] * delayScale);
                // Slightly vary delay times per channel for stereo width
                if (ch == 1) delayLength += (int)(23 * delayScale);
                _combFilters[ch][i] = new CombFilter(delayLength, _roomSize, _damping);
            }
        }

        // Initialize allpass filters for each channel
        _allpassFilters = new AllpassFilter[Channels][];
        for (int ch = 0; ch < Channels; ch++)
        {
            _allpassFilters[ch] = new AllpassFilter[AllpassDelayTimes.Length];
            for (int i = 0; i < AllpassDelayTimes.Length; i++)
            {
                int delayLength = (int)(AllpassDelayTimes[i] * delayScale);
                _allpassFilters[ch][i] = new AllpassFilter(delayLength, 0.5f);
            }
        }
    }

    /// <summary>
    /// Gets or sets the room size (0.0 to 1.0).
    /// Larger values produce longer reverb tails.
    /// </summary>
    public float RoomSize
    {
        get => _roomSize;
        set
        {
            _roomSize = Math.Clamp(value, 0f, 1f);
            UpdateCombFilters();
            SetParameter("RoomSize", _roomSize);
        }
    }

    /// <summary>
    /// Gets or sets the damping amount (0.0 to 1.0).
    /// Higher values produce a darker reverb sound.
    /// </summary>
    public float Damping
    {
        get => _damping;
        set
        {
            _damping = Math.Clamp(value, 0f, 1f);
            UpdateCombFilters();
            SetParameter("Damping", _damping);
        }
    }

    /// <inheritdoc />
    protected override void OnParameterChanged(string name, float value)
    {
        switch (name.ToLower())
        {
            case "roomsize":
                _roomSize = Math.Clamp(value, 0f, 1f);
                UpdateCombFilters();
                break;
            case "damping":
                _damping = Math.Clamp(value, 0f, 1f);
                UpdateCombFilters();
                break;
            case "mix":
                Mix = value;
                break;
        }
    }

    private void UpdateCombFilters()
    {
        for (int ch = 0; ch < Channels; ch++)
        {
            for (int i = 0; i < _combFilters[ch].Length; i++)
            {
                _combFilters[ch][i].SetFeedback(_roomSize);
                _combFilters[ch][i].SetDamping(_damping);
            }
        }
    }

    /// <inheritdoc />
    protected override float ProcessSample(float sample, int channel)
    {
        // Sum the output of all comb filters in parallel
        float combSum = 0f;
        for (int i = 0; i < _combFilters[channel].Length; i++)
        {
            combSum += _combFilters[channel][i].Process(sample);
        }

        // Normalize comb output
        combSum /= _combFilters[channel].Length;

        // Pass through allpass filters in series
        float output = combSum;
        for (int i = 0; i < _allpassFilters[channel].Length; i++)
        {
            output = _allpassFilters[channel][i].Process(output);
        }

        return output;
    }

    /// <summary>
    /// Comb filter implementation for reverb.
    /// </summary>
    private class CombFilter
    {
        private readonly float[] _buffer;
        private int _writeIndex;
        private float _feedback;
        private float _damping;
        private float _dampingInv;
        private float _filterStore;

        public CombFilter(int delayLength, float feedback, float damping)
        {
            _buffer = new float[delayLength];
            _writeIndex = 0;
            _filterStore = 0f;
            SetFeedback(feedback);
            SetDamping(damping);
        }

        public void SetFeedback(float feedback)
        {
            // Scale feedback to avoid self-oscillation
            _feedback = feedback * 0.8f + 0.1f;
        }

        public void SetDamping(float damping)
        {
            _damping = damping * 0.4f;
            _dampingInv = 1f - _damping;
        }

        public float Process(float input)
        {
            float output = _buffer[_writeIndex];

            // Apply damping (lowpass filter in feedback path)
            _filterStore = (output * _dampingInv) + (_filterStore * _damping);

            // Write to buffer with feedback
            _buffer[_writeIndex] = input + (_filterStore * _feedback);

            // Advance write position
            _writeIndex++;
            if (_writeIndex >= _buffer.Length)
                _writeIndex = 0;

            return output;
        }
    }

    /// <summary>
    /// Allpass filter implementation for reverb diffusion.
    /// </summary>
    private class AllpassFilter
    {
        private readonly float[] _buffer;
        private int _writeIndex;
        private readonly float _feedback;

        public AllpassFilter(int delayLength, float feedback)
        {
            _buffer = new float[delayLength];
            _writeIndex = 0;
            _feedback = feedback;
        }

        public float Process(float input)
        {
            float bufferedSample = _buffer[_writeIndex];
            float output = -input + bufferedSample;

            _buffer[_writeIndex] = input + (bufferedSample * _feedback);

            _writeIndex++;
            if (_writeIndex >= _buffer.Length)
                _writeIndex = 0;

            return output;
        }
    }
}
