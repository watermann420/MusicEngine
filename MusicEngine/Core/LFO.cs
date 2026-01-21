//Engine License (MEL) – Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Low Frequency Oscillator for parameter modulation.


using System;


namespace MusicEngine.Core;


/// <summary>
/// LFO waveform shapes
/// </summary>
public enum LfoWaveform
{
    /// <summary>Smooth sine wave oscillation</summary>
    Sine,
    /// <summary>Square wave (on/off)</summary>
    Square,
    /// <summary>Rising sawtooth</summary>
    SawUp,
    /// <summary>Falling sawtooth</summary>
    SawDown,
    /// <summary>Triangle wave</summary>
    Triangle,
    /// <summary>Random values (sample & hold)</summary>
    SampleAndHold,
    /// <summary>Smoothed random (interpolated S&H)</summary>
    SmoothRandom,
    /// <summary>Exponential curve up</summary>
    ExpUp,
    /// <summary>Exponential curve down</summary>
    ExpDown
}


/// <summary>
/// LFO sync modes
/// </summary>
public enum LfoSyncMode
{
    /// <summary>Free running (uses Hz)</summary>
    Free,
    /// <summary>Synced to BPM</summary>
    Tempo,
    /// <summary>Triggered on note-on</summary>
    KeySync
}


/// <summary>
/// Low Frequency Oscillator for modulating parameters.
/// Can be used for vibrato, tremolo, filter sweeps, and other modulations.
/// </summary>
public class LFO
{
    private double _phase;
    private double _lastValue;
    private double _targetValue;
    private double _sampleHoldValue;
    private readonly Random _random = new();
    private readonly object _lock = new();
    private bool _triggered;
    private double _triggerPhase;

    /// <summary>
    /// Gets or sets the LFO waveform
    /// </summary>
    public LfoWaveform Waveform { get; set; } = LfoWaveform.Sine;

    /// <summary>
    /// Gets or sets the frequency in Hz (for Free mode)
    /// </summary>
    public double Frequency { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the tempo-synced rate as note division (e.g., 4 = quarter note, 8 = eighth)
    /// </summary>
    public double TempoRate { get; set; } = 4.0;

    /// <summary>
    /// Gets or sets the sync mode
    /// </summary>
    public LfoSyncMode SyncMode { get; set; } = LfoSyncMode.Free;

    /// <summary>
    /// Gets or sets the depth (modulation amount, 0-1)
    /// </summary>
    public float Depth { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the phase offset (0-1 representing 0-360 degrees)
    /// </summary>
    public float PhaseOffset { get; set; } = 0f;

    /// <summary>
    /// Gets or sets whether the output is unipolar (0 to 1) instead of bipolar (-1 to 1)
    /// </summary>
    public bool Unipolar { get; set; } = false;

    /// <summary>
    /// Gets or sets whether the LFO is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the fade-in time in seconds
    /// </summary>
    public double FadeInTime { get; set; } = 0;

    /// <summary>
    /// Gets or sets the delay time before LFO starts in seconds
    /// </summary>
    public double DelayTime { get; set; } = 0;

    /// <summary>
    /// Gets the current LFO value (last computed)
    /// </summary>
    public double CurrentValue => _lastValue;

    /// <summary>
    /// Gets the current phase (0-1)
    /// </summary>
    public double Phase => _phase;

    /// <summary>
    /// Creates a new LFO with default settings
    /// </summary>
    public LFO() { }

    /// <summary>
    /// Creates a new LFO with specified waveform and frequency
    /// </summary>
    public LFO(LfoWaveform waveform, double frequency)
    {
        Waveform = waveform;
        Frequency = frequency;
    }

    /// <summary>
    /// Trigger the LFO (for KeySync mode)
    /// </summary>
    public void Trigger()
    {
        lock (_lock)
        {
            _triggered = true;
            _triggerPhase = 0;
            if (SyncMode == LfoSyncMode.KeySync)
            {
                _phase = PhaseOffset;
            }
        }
    }

    /// <summary>
    /// Reset the LFO phase
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _phase = PhaseOffset;
            _triggered = false;
            _triggerPhase = 0;
            _lastValue = 0;
        }
    }

    /// <summary>
    /// Get the current LFO value based on time
    /// </summary>
    /// <param name="sampleRate">Audio sample rate</param>
    /// <returns>LFO value (-1 to 1 for bipolar, 0 to 1 for unipolar)</returns>
    public double GetValue(int sampleRate)
    {
        if (!Enabled) return Unipolar ? 0.5 : 0;

        lock (_lock)
        {
            // Calculate phase increment
            double phaseIncrement = Frequency / sampleRate;
            _phase += phaseIncrement;
            if (_phase >= 1.0) _phase -= 1.0;

            return CalculateValue();
        }
    }

    /// <summary>
    /// Get the current LFO value synced to tempo
    /// </summary>
    /// <param name="currentBeat">Current beat position</param>
    /// <param name="bpm">Current BPM</param>
    /// <returns>LFO value (-1 to 1 for bipolar, 0 to 1 for unipolar)</returns>
    public double GetValueAtBeat(double currentBeat, double bpm)
    {
        if (!Enabled) return Unipolar ? 0.5 : 0;

        lock (_lock)
        {
            double effectiveFrequency;

            if (SyncMode == LfoSyncMode.Tempo)
            {
                // Calculate LFO frequency from tempo and rate
                // TempoRate is the note division (4 = quarter, 8 = eighth, etc.)
                double beatsPerCycle = 4.0 / TempoRate;
                effectiveFrequency = (bpm / 60.0) / beatsPerCycle;
                _phase = (currentBeat / beatsPerCycle + PhaseOffset) % 1.0;
            }
            else if (SyncMode == LfoSyncMode.KeySync && _triggered)
            {
                // KeySync - use time since trigger
                double beatsPerCycle = 4.0 / TempoRate;
                _triggerPhase += (1.0 / beatsPerCycle) * 0.01; // Approximate increment
                _phase = (_triggerPhase + PhaseOffset) % 1.0;
            }
            else
            {
                // Free running
                double secondsPerBeat = 60.0 / bpm;
                _phase += Frequency * secondsPerBeat * 0.01; // Approximate increment
                if (_phase >= 1.0) _phase -= 1.0;
            }

            return CalculateValue();
        }
    }

    /// <summary>
    /// Process LFO for a given time delta
    /// </summary>
    /// <param name="deltaSeconds">Time delta in seconds</param>
    /// <param name="bpm">Current BPM (for tempo sync)</param>
    /// <returns>LFO value</returns>
    public double Process(double deltaSeconds, double bpm = 120)
    {
        if (!Enabled) return Unipolar ? 0.5 : 0;

        lock (_lock)
        {
            double effectiveFrequency;

            if (SyncMode == LfoSyncMode.Tempo)
            {
                double beatsPerCycle = 4.0 / TempoRate;
                effectiveFrequency = (bpm / 60.0) / beatsPerCycle;
            }
            else
            {
                effectiveFrequency = Frequency;
            }

            _phase += effectiveFrequency * deltaSeconds;
            if (_phase >= 1.0) _phase -= 1.0;
            if (_phase < 0) _phase += 1.0;

            return CalculateValue();
        }
    }

    /// <summary>
    /// Calculate the LFO value based on current phase and waveform
    /// </summary>
    private double CalculateValue()
    {
        double phase = (_phase + PhaseOffset) % 1.0;
        double rawValue;

        switch (Waveform)
        {
            case LfoWaveform.Sine:
                rawValue = Math.Sin(phase * 2 * Math.PI);
                break;

            case LfoWaveform.Square:
                rawValue = phase < 0.5 ? 1.0 : -1.0;
                break;

            case LfoWaveform.SawUp:
                rawValue = 2.0 * phase - 1.0;
                break;

            case LfoWaveform.SawDown:
                rawValue = 1.0 - 2.0 * phase;
                break;

            case LfoWaveform.Triangle:
                rawValue = phase < 0.5
                    ? 4.0 * phase - 1.0
                    : 3.0 - 4.0 * phase;
                break;

            case LfoWaveform.SampleAndHold:
                // Generate new random value at cycle start
                if (_phase < 0.01)
                {
                    _sampleHoldValue = _random.NextDouble() * 2.0 - 1.0;
                }
                rawValue = _sampleHoldValue;
                break;

            case LfoWaveform.SmoothRandom:
                // Interpolate between random values
                if (_phase < 0.01)
                {
                    _lastValue = _targetValue;
                    _targetValue = _random.NextDouble() * 2.0 - 1.0;
                }
                // Smooth interpolation using cosine
                double t = _phase;
                double smoothT = (1.0 - Math.Cos(t * Math.PI)) / 2.0;
                rawValue = _lastValue + smoothT * (_targetValue - _lastValue);
                break;

            case LfoWaveform.ExpUp:
                rawValue = Math.Pow(phase, 2.0) * 2.0 - 1.0;
                break;

            case LfoWaveform.ExpDown:
                rawValue = (1.0 - Math.Pow(1.0 - phase, 2.0)) * 2.0 - 1.0;
                break;

            default:
                rawValue = 0;
                break;
        }

        // Apply depth
        rawValue *= Depth;

        // Convert to unipolar if needed
        if (Unipolar)
        {
            rawValue = (rawValue + 1.0) / 2.0;
        }

        _lastValue = rawValue;
        return rawValue;
    }

    /// <summary>
    /// Modulate a parameter value with this LFO
    /// </summary>
    /// <param name="baseValue">The base parameter value</param>
    /// <param name="range">The modulation range</param>
    /// <returns>The modulated value</returns>
    public double Modulate(double baseValue, double range)
    {
        return baseValue + _lastValue * range;
    }

    /// <summary>
    /// Modulate a parameter value with this LFO (clamped)
    /// </summary>
    /// <param name="baseValue">The base parameter value</param>
    /// <param name="range">The modulation range</param>
    /// <param name="min">Minimum output value</param>
    /// <param name="max">Maximum output value</param>
    /// <returns>The modulated value, clamped to range</returns>
    public double ModulateClamped(double baseValue, double range, double min, double max)
    {
        double value = baseValue + _lastValue * range;
        return Math.Clamp(value, min, max);
    }
}


/// <summary>
/// Multi-LFO that can combine multiple LFOs
/// </summary>
public class MultiLFO
{
    private readonly LFO[] _lfos;
    private readonly float[] _weights;

    /// <summary>
    /// Gets the number of LFOs
    /// </summary>
    public int Count => _lfos.Length;

    /// <summary>
    /// Gets an individual LFO by index
    /// </summary>
    public LFO this[int index] => _lfos[index];

    /// <summary>
    /// Creates a multi-LFO with specified number of oscillators
    /// </summary>
    public MultiLFO(int count = 2)
    {
        _lfos = new LFO[count];
        _weights = new float[count];

        for (int i = 0; i < count; i++)
        {
            _lfos[i] = new LFO();
            _weights[i] = 1.0f / count;
        }
    }

    /// <summary>
    /// Set the weight for an individual LFO
    /// </summary>
    public void SetWeight(int index, float weight)
    {
        if (index >= 0 && index < _weights.Length)
        {
            _weights[index] = weight;
        }
    }

    /// <summary>
    /// Get the combined LFO value
    /// </summary>
    public double GetValue(int sampleRate)
    {
        double sum = 0;
        double totalWeight = 0;

        for (int i = 0; i < _lfos.Length; i++)
        {
            if (_lfos[i].Enabled)
            {
                sum += _lfos[i].GetValue(sampleRate) * _weights[i];
                totalWeight += _weights[i];
            }
        }

        return totalWeight > 0 ? sum / totalWeight : 0;
    }

    /// <summary>
    /// Get the combined LFO value synced to tempo
    /// </summary>
    public double GetValueAtBeat(double currentBeat, double bpm)
    {
        double sum = 0;
        double totalWeight = 0;

        for (int i = 0; i < _lfos.Length; i++)
        {
            if (_lfos[i].Enabled)
            {
                sum += _lfos[i].GetValueAtBeat(currentBeat, bpm) * _weights[i];
                totalWeight += _weights[i];
            }
        }

        return totalWeight > 0 ? sum / totalWeight : 0;
    }

    /// <summary>
    /// Reset all LFOs
    /// </summary>
    public void Reset()
    {
        foreach (var lfo in _lfos)
        {
            lfo.Reset();
        }
    }

    /// <summary>
    /// Trigger all LFOs
    /// </summary>
    public void Trigger()
    {
        foreach (var lfo in _lfos)
        {
            lfo.Trigger();
        }
    }
}
