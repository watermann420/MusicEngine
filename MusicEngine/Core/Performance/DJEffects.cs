//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: DJ-style effects processor with filter sweep, echo out, brake, backspin, noise build, and flanger.

using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace MusicEngine.Core.Performance;

#region Enumerations

/// <summary>
/// Available DJ effect types
/// </summary>
public enum DJEffectType
{
    /// <summary>No effect active</summary>
    None,
    /// <summary>Filter sweep with resonance</summary>
    FilterSweep,
    /// <summary>Echo out with increasing feedback and decay</summary>
    EchoOut,
    /// <summary>Brake/Spindown effect - tape/vinyl slowdown</summary>
    Brake,
    /// <summary>Backspin effect - reverse spinback</summary>
    Backspin,
    /// <summary>Noise build with rising pitch</summary>
    NoiseBuild,
    /// <summary>Flanger sweep effect</summary>
    Flanger
}

/// <summary>
/// Filter sweep direction
/// </summary>
public enum FilterDirection
{
    /// <summary>Low to high frequency sweep</summary>
    LowToHigh,
    /// <summary>High to low frequency sweep</summary>
    HighToLow,
    /// <summary>Bipolar sweep (both directions)</summary>
    Bipolar
}

/// <summary>
/// Noise type for noise build effect
/// </summary>
public enum NoiseType
{
    /// <summary>White noise - equal energy at all frequencies</summary>
    White,
    /// <summary>Pink noise - equal energy per octave</summary>
    Pink,
    /// <summary>High-pass filtered noise for risers</summary>
    Riser
}

/// <summary>
/// Flanger sweep shape
/// </summary>
public enum FlangerShape
{
    /// <summary>Sine wave LFO</summary>
    Sine,
    /// <summary>Triangle wave LFO</summary>
    Triangle,
    /// <summary>Sawtooth wave LFO</summary>
    Sawtooth
}

#endregion

#region Sub-Effect Classes

/// <summary>
/// Filter sweep effect parameters and state
/// </summary>
public class FilterSweepEffect
{
    private float _lowState;
    private float _bandState;
    private float _highState;
    private float _lowStateR;
    private float _bandStateR;
    private float _highStateR;
    private float _currentCutoff;

    /// <summary>
    /// Whether this effect is currently active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Minimum cutoff frequency in Hz (20-2000)
    /// </summary>
    public float MinFrequency { get; set; } = 80f;

    /// <summary>
    /// Maximum cutoff frequency in Hz (1000-20000)
    /// </summary>
    public float MaxFrequency { get; set; } = 16000f;

    /// <summary>
    /// Filter resonance/Q factor (0.5-15.0)
    /// </summary>
    public float Resonance { get; set; } = 4f;

    /// <summary>
    /// Sweep direction
    /// </summary>
    public FilterDirection Direction { get; set; } = FilterDirection.LowToHigh;

    /// <summary>
    /// Current sweep position (0.0 to 1.0)
    /// </summary>
    public float SweepPosition { get; set; }

    /// <summary>
    /// Sweep rate in Hz (auto-sweep speed, 0 = manual)
    /// </summary>
    public float SweepRate { get; set; }

    /// <summary>
    /// Gets the current cutoff frequency based on sweep position
    /// </summary>
    public float CurrentCutoff
    {
        get
        {
            float position = Direction switch
            {
                FilterDirection.LowToHigh => SweepPosition,
                FilterDirection.HighToLow => 1f - SweepPosition,
                FilterDirection.Bipolar => MathF.Abs(SweepPosition * 2f - 1f),
                _ => SweepPosition
            };
            // Exponential interpolation for perceptually linear sweep
            float logMin = MathF.Log(MinFrequency);
            float logMax = MathF.Log(MaxFrequency);
            return MathF.Exp(logMin + position * (logMax - logMin));
        }
    }

    /// <summary>
    /// Resets the filter state
    /// </summary>
    public void Reset()
    {
        _lowState = 0f;
        _bandState = 0f;
        _highState = 0f;
        _lowStateR = 0f;
        _bandStateR = 0f;
        _highStateR = 0f;
        _currentCutoff = MinFrequency;
        SweepPosition = 0f;
    }

    /// <summary>
    /// Process a stereo sample pair through the filter
    /// </summary>
    public (float left, float right) Process(float left, float right, int sampleRate)
    {
        if (!IsActive) return (left, right);

        float cutoff = CurrentCutoff;
        _currentCutoff += (cutoff - _currentCutoff) * 0.001f; // Smooth cutoff changes

        // State-variable filter coefficients
        float f = 2f * MathF.Sin(MathF.PI * _currentCutoff / sampleRate);
        f = Math.Clamp(f, 0f, 1f);
        float q = 1f / Resonance;

        // Process left channel
        _lowState += f * _bandState;
        _highState = left - _lowState - q * _bandState;
        _bandState += f * _highState;

        // Process right channel
        _lowStateR += f * _bandStateR;
        _highStateR = right - _lowStateR - q * _bandStateR;
        _bandStateR += f * _highStateR;

        return (_lowState, _lowStateR);
    }
}

/// <summary>
/// Echo out effect parameters and state
/// </summary>
public class EchoOutEffect
{
    private float[] _delayBufferL = Array.Empty<float>();
    private float[] _delayBufferR = Array.Empty<float>();
    private int _writePos;
    private int _bufferSize;
    private float _currentFeedback;
    private float _filterStateL;
    private float _filterStateR;

    /// <summary>
    /// Whether this effect is currently active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Delay time in seconds (0.05-2.0)
    /// </summary>
    public float DelayTime { get; set; } = 0.25f;

    /// <summary>
    /// Initial feedback amount (0.0-0.95)
    /// </summary>
    public float Feedback { get; set; } = 0.6f;

    /// <summary>
    /// Final feedback amount when effect completes (0.0-0.99)
    /// </summary>
    public float FinalFeedback { get; set; } = 0.85f;

    /// <summary>
    /// Damping/filtering of echoes (0.0-1.0)
    /// </summary>
    public float Damping { get; set; } = 0.3f;

    /// <summary>
    /// Effect progress (0.0 to 1.0)
    /// </summary>
    public float Progress { get; set; }

    /// <summary>
    /// Whether to sync delay time to tempo
    /// </summary>
    public bool TempoSync { get; set; } = true;

    /// <summary>
    /// Tempo in BPM for sync calculations
    /// </summary>
    public float Tempo { get; set; } = 120f;

    /// <summary>
    /// Delay division for tempo sync (1=quarter, 2=eighth, 4=sixteenth)
    /// </summary>
    public int Division { get; set; } = 2;

    /// <summary>
    /// Gets the actual delay time considering tempo sync
    /// </summary>
    public float ActualDelayTime => TempoSync ? (60f / Tempo) / Division : DelayTime;

    /// <summary>
    /// Initialize or resize the delay buffer
    /// </summary>
    public void Initialize(int sampleRate)
    {
        _bufferSize = (int)(sampleRate * 2.5f); // Max 2.5 seconds
        if (_delayBufferL.Length != _bufferSize)
        {
            _delayBufferL = new float[_bufferSize];
            _delayBufferR = new float[_bufferSize];
            _writePos = 0;
        }
    }

    /// <summary>
    /// Resets the echo effect state
    /// </summary>
    public void Reset()
    {
        Array.Clear(_delayBufferL, 0, _delayBufferL.Length);
        Array.Clear(_delayBufferR, 0, _delayBufferR.Length);
        _writePos = 0;
        _currentFeedback = Feedback;
        _filterStateL = 0f;
        _filterStateR = 0f;
        Progress = 0f;
    }

    /// <summary>
    /// Process a stereo sample pair through the echo
    /// </summary>
    public (float left, float right) Process(float left, float right, int sampleRate, bool inputMuted)
    {
        if (_delayBufferL.Length == 0) return (left, right);

        // Calculate interpolated feedback based on progress
        float targetFeedback = IsActive ? Feedback + (FinalFeedback - Feedback) * Progress : Feedback;
        _currentFeedback += (targetFeedback - _currentFeedback) * 0.001f;

        // Calculate delay in samples
        int delaySamples = (int)(ActualDelayTime * sampleRate);
        delaySamples = Math.Clamp(delaySamples, 1, _bufferSize - 1);

        // Read from delay buffer
        int readPos = (_writePos - delaySamples + _bufferSize) % _bufferSize;
        float delayedL = _delayBufferL[readPos];
        float delayedR = _delayBufferR[readPos];

        // Apply damping filter
        if (Damping > 0f)
        {
            float alpha = 1f - Damping;
            delayedL = _filterStateL + alpha * (delayedL - _filterStateL);
            delayedR = _filterStateR + alpha * (delayedR - _filterStateR);
            _filterStateL = delayedL;
            _filterStateR = delayedR;
        }

        // Write to delay buffer (input + feedback, or just feedback if muted)
        float inputL = inputMuted ? 0f : left;
        float inputR = inputMuted ? 0f : right;
        _delayBufferL[_writePos] = inputL + delayedL * _currentFeedback;
        _delayBufferR[_writePos] = inputR + delayedR * _currentFeedback;

        // Advance write position
        _writePos = (_writePos + 1) % _bufferSize;

        // Mix dry and wet
        float wetMix = IsActive ? 0.5f + Progress * 0.5f : 0.5f;
        float dryMix = 1f - wetMix * 0.5f;

        return (left * dryMix + delayedL * wetMix, right * dryMix + delayedR * wetMix);
    }
}

/// <summary>
/// Brake/Spindown effect parameters and state
/// </summary>
public class BrakeEffect
{
    private float[] _bufferL = Array.Empty<float>();
    private float[] _bufferR = Array.Empty<float>();
    private int _writePos;
    private double _readPos;
    private int _bufferSize;
    private double _currentSpeed = 1.0;

    /// <summary>
    /// Whether this effect is currently active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Brake time in seconds (0.1-5.0)
    /// </summary>
    public float BrakeTime { get; set; } = 1.5f;

    /// <summary>
    /// Effect progress (0.0 to 1.0)
    /// </summary>
    public float Progress { get; set; }

    /// <summary>
    /// Curve type: 0=linear, 1=exponential, 2=s-curve
    /// </summary>
    public int CurveType { get; set; } = 1;

    /// <summary>
    /// Current playback speed (1.0 = normal, 0.0 = stopped)
    /// </summary>
    public double CurrentSpeed => _currentSpeed;

    /// <summary>
    /// Initialize or resize the buffer
    /// </summary>
    public void Initialize(int sampleRate)
    {
        _bufferSize = sampleRate * 6; // 6 seconds buffer
        if (_bufferL.Length != _bufferSize)
        {
            _bufferL = new float[_bufferSize];
            _bufferR = new float[_bufferSize];
            _writePos = 0;
            _readPos = 0;
        }
    }

    /// <summary>
    /// Resets the brake effect state
    /// </summary>
    public void Reset()
    {
        Array.Clear(_bufferL, 0, _bufferL.Length);
        Array.Clear(_bufferR, 0, _bufferR.Length);
        _writePos = 0;
        _readPos = 0;
        _currentSpeed = 1.0;
        Progress = 0f;
        IsActive = false;
    }

    /// <summary>
    /// Triggers the brake effect
    /// </summary>
    public void Trigger()
    {
        IsActive = true;
        Progress = 0f;
        _currentSpeed = 1.0;
        _readPos = _writePos;
    }

    /// <summary>
    /// Process a stereo sample pair through the brake effect
    /// </summary>
    public (float left, float right) Process(float left, float right, int sampleRate)
    {
        if (_bufferL.Length == 0) return (left, right);

        // Write incoming samples to buffer
        _bufferL[_writePos] = left;
        _bufferR[_writePos] = right;

        float outputL, outputR;

        if (IsActive)
        {
            // Calculate speed based on progress and curve type
            _currentSpeed = CurveType switch
            {
                0 => 1.0 - Progress, // Linear
                1 => Math.Exp(-3.0 * Progress), // Exponential
                2 => 1.0 - (3.0 * Progress * Progress - 2.0 * Progress * Progress * Progress), // S-curve
                _ => 1.0 - Progress
            };
            _currentSpeed = Math.Max(0.0, _currentSpeed);

            // Read from buffer with interpolation
            int pos1 = (int)_readPos;
            int pos2 = (pos1 + 1) % _bufferSize;
            double frac = _readPos - pos1;

            if (pos1 >= 0 && pos1 < _bufferSize)
            {
                outputL = (float)(_bufferL[pos1] * (1.0 - frac) + _bufferL[pos2] * frac);
                outputR = (float)(_bufferR[pos1] * (1.0 - frac) + _bufferR[pos2] * frac);
            }
            else
            {
                outputL = 0f;
                outputR = 0f;
            }

            // Advance read position at current speed
            _readPos += _currentSpeed;
            if (_readPos >= _bufferSize) _readPos -= _bufferSize;
            if (_readPos < 0) _readPos += _bufferSize;

            // Check if effect is complete
            if (Progress >= 1f)
            {
                _currentSpeed = 0.0;
                outputL = 0f;
                outputR = 0f;
            }
        }
        else
        {
            // Pass through
            outputL = left;
            outputR = right;
            _readPos = _writePos;
        }

        // Advance write position
        _writePos = (_writePos + 1) % _bufferSize;

        return (outputL, outputR);
    }
}

/// <summary>
/// Backspin effect parameters and state
/// </summary>
public class BackspinEffect
{
    private float[] _bufferL = Array.Empty<float>();
    private float[] _bufferR = Array.Empty<float>();
    private int _writePos;
    private double _readPos;
    private int _bufferSize;
    private double _currentSpeed;
    private int _spinStartPos;

    /// <summary>
    /// Whether this effect is currently active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Backspin duration in seconds (0.2-3.0)
    /// </summary>
    public float SpinTime { get; set; } = 0.8f;

    /// <summary>
    /// Effect progress (0.0 to 1.0)
    /// </summary>
    public float Progress { get; set; }

    /// <summary>
    /// How far back to spin in seconds (0.1-2.0)
    /// </summary>
    public float SpinLength { get; set; } = 0.5f;

    /// <summary>
    /// Current playback speed (negative when spinning back)
    /// </summary>
    public double CurrentSpeed => _currentSpeed;

    /// <summary>
    /// Initialize or resize the buffer
    /// </summary>
    public void Initialize(int sampleRate)
    {
        _bufferSize = sampleRate * 4; // 4 seconds buffer
        if (_bufferL.Length != _bufferSize)
        {
            _bufferL = new float[_bufferSize];
            _bufferR = new float[_bufferSize];
            _writePos = 0;
            _readPos = 0;
        }
    }

    /// <summary>
    /// Resets the backspin effect state
    /// </summary>
    public void Reset()
    {
        Array.Clear(_bufferL, 0, _bufferL.Length);
        Array.Clear(_bufferR, 0, _bufferR.Length);
        _writePos = 0;
        _readPos = 0;
        _currentSpeed = 1.0;
        Progress = 0f;
        IsActive = false;
    }

    /// <summary>
    /// Triggers the backspin effect
    /// </summary>
    public void Trigger()
    {
        IsActive = true;
        Progress = 0f;
        _spinStartPos = _writePos;
        _readPos = _writePos;
        _currentSpeed = -2.0; // Start spinning backward fast
    }

    /// <summary>
    /// Process a stereo sample pair through the backspin effect
    /// </summary>
    public (float left, float right) Process(float left, float right, int sampleRate)
    {
        if (_bufferL.Length == 0) return (left, right);

        // Write incoming samples to buffer
        _bufferL[_writePos] = left;
        _bufferR[_writePos] = right;

        float outputL, outputR;

        if (IsActive)
        {
            // Calculate speed curve: fast backward, slow down, then speed up forward
            double phase = Progress;
            if (phase < 0.3)
            {
                // Spinning backward, slowing down
                _currentSpeed = -2.0 * (1.0 - phase / 0.3);
            }
            else if (phase < 0.5)
            {
                // At stop point
                _currentSpeed = 0.0;
            }
            else
            {
                // Speeding up forward
                double forwardPhase = (phase - 0.5) / 0.5;
                _currentSpeed = forwardPhase * forwardPhase * 1.0; // Accelerate to normal speed
            }

            // Read from buffer with interpolation
            int pos1 = ((int)_readPos % _bufferSize + _bufferSize) % _bufferSize;
            int pos2 = (pos1 + 1) % _bufferSize;
            double frac = Math.Abs(_readPos - Math.Floor(_readPos));

            outputL = (float)(_bufferL[pos1] * (1.0 - frac) + _bufferL[pos2] * frac);
            outputR = (float)(_bufferR[pos1] * (1.0 - frac) + _bufferR[pos2] * frac);

            // Advance read position
            _readPos += _currentSpeed;
            if (_readPos >= _bufferSize) _readPos -= _bufferSize;
            if (_readPos < 0) _readPos += _bufferSize;

            // Check if effect is complete
            if (Progress >= 1f)
            {
                IsActive = false;
                _currentSpeed = 1.0;
                _readPos = _writePos;
            }
        }
        else
        {
            // Pass through
            outputL = left;
            outputR = right;
            _readPos = _writePos;
        }

        // Advance write position
        _writePos = (_writePos + 1) % _bufferSize;

        return (outputL, outputR);
    }
}

/// <summary>
/// Noise build/riser effect parameters and state
/// </summary>
public class NoiseBuildEffect
{
    private Random _random = new();
    private float _pinkState0;
    private float _pinkState1;
    private float _pinkState2;
    private float _filterState;
    private float _currentVolume;
    private float _currentCutoff;

    /// <summary>
    /// Whether this effect is currently active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Noise type
    /// </summary>
    public NoiseType NoiseType { get; set; } = NoiseType.White;

    /// <summary>
    /// Build time in seconds (0.5-8.0)
    /// </summary>
    public float BuildTime { get; set; } = 4f;

    /// <summary>
    /// Effect progress (0.0 to 1.0)
    /// </summary>
    public float Progress { get; set; }

    /// <summary>
    /// Starting filter cutoff frequency (Hz)
    /// </summary>
    public float StartFrequency { get; set; } = 200f;

    /// <summary>
    /// Ending filter cutoff frequency (Hz)
    /// </summary>
    public float EndFrequency { get; set; } = 12000f;

    /// <summary>
    /// Maximum volume level (0.0-1.0)
    /// </summary>
    public float MaxVolume { get; set; } = 0.7f;

    /// <summary>
    /// Filter resonance (0.5-10.0)
    /// </summary>
    public float Resonance { get; set; } = 2f;

    /// <summary>
    /// Resets the noise build effect state
    /// </summary>
    public void Reset()
    {
        _pinkState0 = 0f;
        _pinkState1 = 0f;
        _pinkState2 = 0f;
        _filterState = 0f;
        _currentVolume = 0f;
        _currentCutoff = StartFrequency;
        Progress = 0f;
        IsActive = false;
    }

    /// <summary>
    /// Triggers the noise build effect
    /// </summary>
    public void Trigger()
    {
        IsActive = true;
        Progress = 0f;
        _currentVolume = 0f;
        _currentCutoff = StartFrequency;
    }

    /// <summary>
    /// Process and generate noise build output
    /// </summary>
    public (float left, float right) Process(int sampleRate)
    {
        if (!IsActive) return (0f, 0f);

        // Generate base noise
        float noise = NoiseType switch
        {
            NoiseType.White => (float)(_random.NextDouble() * 2.0 - 1.0),
            NoiseType.Pink => GeneratePinkNoise(),
            NoiseType.Riser => (float)(_random.NextDouble() * 2.0 - 1.0),
            _ => (float)(_random.NextDouble() * 2.0 - 1.0)
        };

        // Calculate target values based on progress
        float targetCutoff = StartFrequency + (EndFrequency - StartFrequency) * Progress * Progress;
        float targetVolume = MaxVolume * Progress * Progress;

        // Smooth parameter changes
        _currentCutoff += (targetCutoff - _currentCutoff) * 0.01f;
        _currentVolume += (targetVolume - _currentVolume) * 0.01f;

        // Apply highpass filter for riser effect
        float f = 2f * MathF.Sin(MathF.PI * _currentCutoff / sampleRate);
        f = Math.Clamp(f, 0f, 1f);

        if (NoiseType == NoiseType.Riser)
        {
            // Highpass filter
            float highpass = noise - _filterState;
            _filterState += f * highpass;
            noise = highpass;
        }
        else
        {
            // Lowpass filter with resonance
            float q = 1f / Resonance;
            _filterState += f * (noise - _filterState - q * _filterState);
            noise = _filterState;
        }

        float output = noise * _currentVolume;

        // Add slight stereo variation
        float stereoOffset = (float)(_random.NextDouble() * 0.1 - 0.05);

        return (output, output + stereoOffset * output);
    }

    private float GeneratePinkNoise()
    {
        float white = (float)(_random.NextDouble() * 2.0 - 1.0);

        // Paul Kellet's pink noise algorithm
        _pinkState0 = 0.99886f * _pinkState0 + white * 0.0555179f;
        _pinkState1 = 0.99332f * _pinkState1 + white * 0.0750759f;
        _pinkState2 = 0.96900f * _pinkState2 + white * 0.1538520f;

        return (_pinkState0 + _pinkState1 + _pinkState2 + white * 0.5362f) * 0.25f;
    }
}

/// <summary>
/// Flanger sweep effect parameters and state
/// </summary>
public class FlangerSweepEffect
{
    private float[] _delayBufferL = Array.Empty<float>();
    private float[] _delayBufferR = Array.Empty<float>();
    private int _writePos;
    private int _bufferSize;
    private float _lfoPhase;

    private const int MaxDelaySamples = 882; // 20ms at 44.1kHz

    /// <summary>
    /// Whether this effect is currently active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// LFO rate in Hz (0.05-5.0)
    /// </summary>
    public float Rate { get; set; } = 0.5f;

    /// <summary>
    /// Modulation depth in seconds (0.0005-0.01)
    /// </summary>
    public float Depth { get; set; } = 0.003f;

    /// <summary>
    /// Feedback amount (0.0-0.95)
    /// </summary>
    public float Feedback { get; set; } = 0.7f;

    /// <summary>
    /// Base delay time in seconds (0.001-0.01)
    /// </summary>
    public float BaseDelay { get; set; } = 0.002f;

    /// <summary>
    /// LFO shape
    /// </summary>
    public FlangerShape Shape { get; set; } = FlangerShape.Sine;

    /// <summary>
    /// Stereo phase offset (0.0-1.0)
    /// </summary>
    public float StereoWidth { get; set; } = 0.5f;

    /// <summary>
    /// Dry/wet mix (0.0-1.0)
    /// </summary>
    public float Mix { get; set; } = 0.5f;

    /// <summary>
    /// Manual sweep position (0.0-1.0) when rate is 0
    /// </summary>
    public float ManualPosition { get; set; } = 0.5f;

    /// <summary>
    /// Initialize or resize the delay buffer
    /// </summary>
    public void Initialize(int sampleRate)
    {
        _bufferSize = (int)(sampleRate * 0.025f); // 25ms max
        if (_delayBufferL.Length != _bufferSize)
        {
            _delayBufferL = new float[_bufferSize];
            _delayBufferR = new float[_bufferSize];
            _writePos = 0;
        }
    }

    /// <summary>
    /// Resets the flanger effect state
    /// </summary>
    public void Reset()
    {
        Array.Clear(_delayBufferL, 0, _delayBufferL.Length);
        Array.Clear(_delayBufferR, 0, _delayBufferR.Length);
        _writePos = 0;
        _lfoPhase = 0f;
    }

    /// <summary>
    /// Process a stereo sample pair through the flanger
    /// </summary>
    public (float left, float right) Process(float left, float right, int sampleRate)
    {
        if (!IsActive || _delayBufferL.Length == 0) return (left, right);

        // Calculate LFO value
        float lfoL, lfoR;
        if (Rate > 0.001f)
        {
            _lfoPhase += 2f * MathF.PI * Rate / sampleRate;
            if (_lfoPhase > 2f * MathF.PI) _lfoPhase -= 2f * MathF.PI;

            lfoL = GetLfoValue(_lfoPhase);
            lfoR = GetLfoValue(_lfoPhase + StereoWidth * MathF.PI);
        }
        else
        {
            // Manual control
            lfoL = ManualPosition * 2f - 1f;
            lfoR = lfoL;
        }

        // Calculate modulated delay times
        float delayTimeL = BaseDelay + Depth * (lfoL * 0.5f + 0.5f);
        float delayTimeR = BaseDelay + Depth * (lfoR * 0.5f + 0.5f);
        float delaySamplesL = Math.Clamp(delayTimeL * sampleRate, 1f, _bufferSize - 2);
        float delaySamplesR = Math.Clamp(delayTimeR * sampleRate, 1f, _bufferSize - 2);

        // Read delayed samples with interpolation
        float delayedL = ReadInterpolated(_delayBufferL, delaySamplesL);
        float delayedR = ReadInterpolated(_delayBufferR, delaySamplesR);

        // Write to delay buffer with feedback
        _delayBufferL[_writePos] = left + delayedL * Feedback;
        _delayBufferR[_writePos] = right + delayedR * Feedback;

        // Advance write position
        _writePos = (_writePos + 1) % _bufferSize;

        // Mix dry and wet
        float outL = left * (1f - Mix) + delayedL * Mix;
        float outR = right * (1f - Mix) + delayedR * Mix;

        return (outL, outR);
    }

    private float GetLfoValue(float phase)
    {
        return Shape switch
        {
            FlangerShape.Sine => MathF.Sin(phase),
            FlangerShape.Triangle => 2f * MathF.Abs(2f * (phase / (2f * MathF.PI) - MathF.Floor(phase / (2f * MathF.PI) + 0.5f))) - 1f,
            FlangerShape.Sawtooth => 2f * (phase / (2f * MathF.PI) - MathF.Floor(phase / (2f * MathF.PI) + 0.5f)),
            _ => MathF.Sin(phase)
        };
    }

    private float ReadInterpolated(float[] buffer, float delaySamples)
    {
        float readPos = _writePos - delaySamples;
        if (readPos < 0) readPos += buffer.Length;

        int pos1 = (int)readPos;
        int pos2 = (pos1 + 1) % buffer.Length;
        float frac = readPos - pos1;

        return buffer[pos1] * (1f - frac) + buffer[pos2] * frac;
    }
}

#endregion

#region XY Pad Control

/// <summary>
/// XY pad parameter mapping for DJ effects
/// </summary>
public class XYPadMapping
{
    /// <summary>
    /// X-axis parameter name
    /// </summary>
    public string XParameter { get; set; } = "";

    /// <summary>
    /// Y-axis parameter name
    /// </summary>
    public string YParameter { get; set; } = "";

    /// <summary>
    /// X-axis minimum value
    /// </summary>
    public float XMin { get; set; }

    /// <summary>
    /// X-axis maximum value
    /// </summary>
    public float XMax { get; set; } = 1f;

    /// <summary>
    /// Y-axis minimum value
    /// </summary>
    public float YMin { get; set; }

    /// <summary>
    /// Y-axis maximum value
    /// </summary>
    public float YMax { get; set; } = 1f;

    /// <summary>
    /// Whether X-axis is inverted
    /// </summary>
    public bool XInvert { get; set; }

    /// <summary>
    /// Whether Y-axis is inverted
    /// </summary>
    public bool YInvert { get; set; }

    /// <summary>
    /// Calculate mapped X value from position (0-1)
    /// </summary>
    public float GetXValue(float position)
    {
        if (XInvert) position = 1f - position;
        return XMin + position * (XMax - XMin);
    }

    /// <summary>
    /// Calculate mapped Y value from position (0-1)
    /// </summary>
    public float GetYValue(float position)
    {
        if (YInvert) position = 1f - position;
        return YMin + position * (YMax - YMin);
    }
}

#endregion

/// <summary>
/// DJ-style effects processor with filter sweep, echo out, brake, backspin, noise build, and flanger.
/// Designed for live performance with quick toggles and XY pad control support.
/// </summary>
public class DJEffects : EffectBase
{
    private readonly FilterSweepEffect _filterSweep = new();
    private readonly EchoOutEffect _echoOut = new();
    private readonly BrakeEffect _brake = new();
    private readonly BackspinEffect _backspin = new();
    private readonly NoiseBuildEffect _noiseBuild = new();
    private readonly FlangerSweepEffect _flanger = new();

    private DJEffectType _activeEffect = DJEffectType.None;
    private long _effectStartSample;
    private long _totalSamplesProcessed;
    private float _tempo = 120f;
    private bool _muteInputDuringEffect;

    // XY pad state
    private float _xyPadX = 0.5f;
    private float _xyPadY = 0.5f;
    private readonly Dictionary<DJEffectType, XYPadMapping> _xyMappings = new();

    /// <summary>
    /// Event raised when an effect completes
    /// </summary>
    public event EventHandler<DJEffectType>? EffectCompleted;

    /// <summary>
    /// Event raised when an effect is triggered
    /// </summary>
    public event EventHandler<DJEffectType>? EffectTriggered;

    /// <summary>
    /// Creates a new DJ effects processor
    /// </summary>
    /// <param name="source">The audio source to process</param>
    public DJEffects(ISampleProvider source)
        : base(source, "DJ Effects")
    {
        InitializeEffects();
        InitializeXYMappings();
        RegisterParameters();
    }

    private void InitializeEffects()
    {
        int sampleRate = SampleRate;

        _echoOut.Initialize(sampleRate);
        _brake.Initialize(sampleRate);
        _backspin.Initialize(sampleRate);
        _flanger.Initialize(sampleRate);
    }

    private void InitializeXYMappings()
    {
        // Default XY pad mappings for each effect
        _xyMappings[DJEffectType.FilterSweep] = new XYPadMapping
        {
            XParameter = "FilterSweep.SweepPosition",
            YParameter = "FilterSweep.Resonance",
            XMin = 0f,
            XMax = 1f,
            YMin = 0.5f,
            YMax = 15f
        };

        _xyMappings[DJEffectType.EchoOut] = new XYPadMapping
        {
            XParameter = "EchoOut.DelayTime",
            YParameter = "EchoOut.Feedback",
            XMin = 0.05f,
            XMax = 0.5f,
            YMin = 0.3f,
            YMax = 0.9f
        };

        _xyMappings[DJEffectType.Flanger] = new XYPadMapping
        {
            XParameter = "Flanger.ManualPosition",
            YParameter = "Flanger.Feedback",
            XMin = 0f,
            XMax = 1f,
            YMin = 0f,
            YMax = 0.95f
        };

        _xyMappings[DJEffectType.NoiseBuild] = new XYPadMapping
        {
            XParameter = "NoiseBuild.EndFrequency",
            YParameter = "NoiseBuild.MaxVolume",
            XMin = 2000f,
            XMax = 16000f,
            YMin = 0.2f,
            YMax = 1f
        };
    }

    private void RegisterParameters()
    {
        // Global parameters
        RegisterParameter("Tempo", 120f);
        RegisterParameter("MuteInputDuringEffect", 0f);

        // Filter sweep
        RegisterParameter("FilterSweep.MinFrequency", 80f);
        RegisterParameter("FilterSweep.MaxFrequency", 16000f);
        RegisterParameter("FilterSweep.Resonance", 4f);
        RegisterParameter("FilterSweep.SweepPosition", 0f);
        RegisterParameter("FilterSweep.SweepRate", 0f);
        RegisterParameter("FilterSweep.Direction", 0f);

        // Echo out
        RegisterParameter("EchoOut.DelayTime", 0.25f);
        RegisterParameter("EchoOut.Feedback", 0.6f);
        RegisterParameter("EchoOut.FinalFeedback", 0.85f);
        RegisterParameter("EchoOut.Damping", 0.3f);
        RegisterParameter("EchoOut.TempoSync", 1f);
        RegisterParameter("EchoOut.Division", 2f);

        // Brake
        RegisterParameter("Brake.BrakeTime", 1.5f);
        RegisterParameter("Brake.CurveType", 1f);

        // Backspin
        RegisterParameter("Backspin.SpinTime", 0.8f);
        RegisterParameter("Backspin.SpinLength", 0.5f);

        // Noise build
        RegisterParameter("NoiseBuild.BuildTime", 4f);
        RegisterParameter("NoiseBuild.NoiseType", 0f);
        RegisterParameter("NoiseBuild.StartFrequency", 200f);
        RegisterParameter("NoiseBuild.EndFrequency", 12000f);
        RegisterParameter("NoiseBuild.MaxVolume", 0.7f);
        RegisterParameter("NoiseBuild.Resonance", 2f);

        // Flanger
        RegisterParameter("Flanger.Rate", 0.5f);
        RegisterParameter("Flanger.Depth", 0.003f);
        RegisterParameter("Flanger.Feedback", 0.7f);
        RegisterParameter("Flanger.BaseDelay", 0.002f);
        RegisterParameter("Flanger.Shape", 0f);
        RegisterParameter("Flanger.StereoWidth", 0.5f);
        RegisterParameter("Flanger.Mix", 0.5f);
        RegisterParameter("Flanger.ManualPosition", 0.5f);

        // XY pad
        RegisterParameter("XYPad.X", 0.5f);
        RegisterParameter("XYPad.Y", 0.5f);
    }

    #region Properties

    /// <summary>
    /// Gets or sets the tempo in BPM for tempo-synced effects
    /// </summary>
    public float Tempo
    {
        get => _tempo;
        set
        {
            _tempo = Math.Clamp(value, 20f, 300f);
            _echoOut.Tempo = _tempo;
            SetParameter("Tempo", _tempo);
        }
    }

    /// <summary>
    /// Gets the currently active effect type
    /// </summary>
    public DJEffectType ActiveEffect => _activeEffect;

    /// <summary>
    /// Gets the filter sweep sub-effect
    /// </summary>
    public FilterSweepEffect FilterSweep => _filterSweep;

    /// <summary>
    /// Gets the echo out sub-effect
    /// </summary>
    public EchoOutEffect EchoOut => _echoOut;

    /// <summary>
    /// Gets the brake sub-effect
    /// </summary>
    public BrakeEffect Brake => _brake;

    /// <summary>
    /// Gets the backspin sub-effect
    /// </summary>
    public BackspinEffect Backspin => _backspin;

    /// <summary>
    /// Gets the noise build sub-effect
    /// </summary>
    public NoiseBuildEffect NoiseBuild => _noiseBuild;

    /// <summary>
    /// Gets the flanger sub-effect
    /// </summary>
    public FlangerSweepEffect Flanger => _flanger;

    /// <summary>
    /// Gets or sets whether to mute the input signal during timed effects
    /// </summary>
    public bool MuteInputDuringEffect
    {
        get => _muteInputDuringEffect;
        set
        {
            _muteInputDuringEffect = value;
            SetParameter("MuteInputDuringEffect", value ? 1f : 0f);
        }
    }

    /// <summary>
    /// Gets or sets the XY pad X position (0.0-1.0)
    /// </summary>
    public float XYPadX
    {
        get => _xyPadX;
        set
        {
            _xyPadX = Math.Clamp(value, 0f, 1f);
            SetParameter("XYPad.X", _xyPadX);
            ApplyXYPadMapping();
        }
    }

    /// <summary>
    /// Gets or sets the XY pad Y position (0.0-1.0)
    /// </summary>
    public float XYPadY
    {
        get => _xyPadY;
        set
        {
            _xyPadY = Math.Clamp(value, 0f, 1f);
            SetParameter("XYPad.Y", _xyPadY);
            ApplyXYPadMapping();
        }
    }

    #endregion

    #region Effect Control Methods

    /// <summary>
    /// Triggers a DJ effect
    /// </summary>
    /// <param name="effectType">The effect type to trigger</param>
    public void TriggerEffect(DJEffectType effectType)
    {
        // Stop any currently active timed effect
        if (_activeEffect != DJEffectType.None && _activeEffect != DJEffectType.FilterSweep && _activeEffect != DJEffectType.Flanger)
        {
            StopEffect(_activeEffect);
        }

        _activeEffect = effectType;
        _effectStartSample = _totalSamplesProcessed;

        switch (effectType)
        {
            case DJEffectType.FilterSweep:
                _filterSweep.IsActive = true;
                _filterSweep.Reset();
                break;

            case DJEffectType.EchoOut:
                SyncEchoOutParameters();
                _echoOut.Reset();
                _echoOut.IsActive = true;
                break;

            case DJEffectType.Brake:
                SyncBrakeParameters();
                _brake.Trigger();
                break;

            case DJEffectType.Backspin:
                SyncBackspinParameters();
                _backspin.Trigger();
                break;

            case DJEffectType.NoiseBuild:
                SyncNoiseBuildParameters();
                _noiseBuild.Trigger();
                break;

            case DJEffectType.Flanger:
                SyncFlangerParameters();
                _flanger.IsActive = true;
                _flanger.Reset();
                break;
        }

        EffectTriggered?.Invoke(this, effectType);
    }

    /// <summary>
    /// Stops a DJ effect
    /// </summary>
    /// <param name="effectType">The effect type to stop</param>
    public void StopEffect(DJEffectType effectType)
    {
        switch (effectType)
        {
            case DJEffectType.FilterSweep:
                _filterSweep.IsActive = false;
                break;

            case DJEffectType.EchoOut:
                _echoOut.IsActive = false;
                break;

            case DJEffectType.Brake:
                _brake.Reset();
                break;

            case DJEffectType.Backspin:
                _backspin.Reset();
                break;

            case DJEffectType.NoiseBuild:
                _noiseBuild.Reset();
                break;

            case DJEffectType.Flanger:
                _flanger.IsActive = false;
                break;
        }

        if (_activeEffect == effectType)
        {
            _activeEffect = DJEffectType.None;
        }
    }

    /// <summary>
    /// Stops all active effects
    /// </summary>
    public void StopAllEffects()
    {
        _filterSweep.IsActive = false;
        _echoOut.IsActive = false;
        _brake.Reset();
        _backspin.Reset();
        _noiseBuild.Reset();
        _flanger.IsActive = false;
        _activeEffect = DJEffectType.None;
    }

    /// <summary>
    /// Quick toggle for an effect (toggle on/off)
    /// </summary>
    /// <param name="effectType">The effect type to toggle</param>
    public void ToggleEffect(DJEffectType effectType)
    {
        bool isActive = effectType switch
        {
            DJEffectType.FilterSweep => _filterSweep.IsActive,
            DJEffectType.EchoOut => _echoOut.IsActive,
            DJEffectType.Brake => _brake.IsActive,
            DJEffectType.Backspin => _backspin.IsActive,
            DJEffectType.NoiseBuild => _noiseBuild.IsActive,
            DJEffectType.Flanger => _flanger.IsActive,
            _ => false
        };

        if (isActive)
        {
            StopEffect(effectType);
        }
        else
        {
            TriggerEffect(effectType);
        }
    }

    /// <summary>
    /// Sets the XY pad mapping for an effect
    /// </summary>
    public void SetXYMapping(DJEffectType effectType, XYPadMapping mapping)
    {
        _xyMappings[effectType] = mapping;
    }

    /// <summary>
    /// Gets the XY pad mapping for an effect
    /// </summary>
    public XYPadMapping? GetXYMapping(DJEffectType effectType)
    {
        return _xyMappings.TryGetValue(effectType, out var mapping) ? mapping : null;
    }

    /// <summary>
    /// Sets the XY pad position
    /// </summary>
    public void SetXYPad(float x, float y)
    {
        XYPadX = x;
        XYPadY = y;
    }

    #endregion

    #region Parameter Synchronization

    private void SyncEchoOutParameters()
    {
        _echoOut.DelayTime = GetParameter("EchoOut.DelayTime");
        _echoOut.Feedback = GetParameter("EchoOut.Feedback");
        _echoOut.FinalFeedback = GetParameter("EchoOut.FinalFeedback");
        _echoOut.Damping = GetParameter("EchoOut.Damping");
        _echoOut.TempoSync = GetParameter("EchoOut.TempoSync") > 0.5f;
        _echoOut.Division = (int)GetParameter("EchoOut.Division");
        _echoOut.Tempo = _tempo;
    }

    private void SyncBrakeParameters()
    {
        _brake.BrakeTime = GetParameter("Brake.BrakeTime");
        _brake.CurveType = (int)GetParameter("Brake.CurveType");
    }

    private void SyncBackspinParameters()
    {
        _backspin.SpinTime = GetParameter("Backspin.SpinTime");
        _backspin.SpinLength = GetParameter("Backspin.SpinLength");
    }

    private void SyncNoiseBuildParameters()
    {
        _noiseBuild.BuildTime = GetParameter("NoiseBuild.BuildTime");
        _noiseBuild.NoiseType = (NoiseType)(int)GetParameter("NoiseBuild.NoiseType");
        _noiseBuild.StartFrequency = GetParameter("NoiseBuild.StartFrequency");
        _noiseBuild.EndFrequency = GetParameter("NoiseBuild.EndFrequency");
        _noiseBuild.MaxVolume = GetParameter("NoiseBuild.MaxVolume");
        _noiseBuild.Resonance = GetParameter("NoiseBuild.Resonance");
    }

    private void SyncFlangerParameters()
    {
        _flanger.Rate = GetParameter("Flanger.Rate");
        _flanger.Depth = GetParameter("Flanger.Depth");
        _flanger.Feedback = GetParameter("Flanger.Feedback");
        _flanger.BaseDelay = GetParameter("Flanger.BaseDelay");
        _flanger.Shape = (FlangerShape)(int)GetParameter("Flanger.Shape");
        _flanger.StereoWidth = GetParameter("Flanger.StereoWidth");
        _flanger.Mix = GetParameter("Flanger.Mix");
        _flanger.ManualPosition = GetParameter("Flanger.ManualPosition");
    }

    private void ApplyXYPadMapping()
    {
        if (_activeEffect == DJEffectType.None) return;

        if (!_xyMappings.TryGetValue(_activeEffect, out var mapping)) return;

        float xValue = mapping.GetXValue(_xyPadX);
        float yValue = mapping.GetYValue(_xyPadY);

        // Apply to the appropriate effect
        switch (_activeEffect)
        {
            case DJEffectType.FilterSweep:
                if (mapping.XParameter.Contains("SweepPosition"))
                    _filterSweep.SweepPosition = xValue;
                if (mapping.YParameter.Contains("Resonance"))
                    _filterSweep.Resonance = yValue;
                break;

            case DJEffectType.EchoOut:
                if (mapping.XParameter.Contains("DelayTime"))
                    _echoOut.DelayTime = xValue;
                if (mapping.YParameter.Contains("Feedback"))
                    _echoOut.Feedback = yValue;
                break;

            case DJEffectType.Flanger:
                if (mapping.XParameter.Contains("ManualPosition"))
                    _flanger.ManualPosition = xValue;
                if (mapping.YParameter.Contains("Feedback"))
                    _flanger.Feedback = yValue;
                break;

            case DJEffectType.NoiseBuild:
                if (mapping.XParameter.Contains("EndFrequency"))
                    _noiseBuild.EndFrequency = xValue;
                if (mapping.YParameter.Contains("MaxVolume"))
                    _noiseBuild.MaxVolume = yValue;
                break;
        }
    }

    #endregion

    #region Audio Processing

    /// <inheritdoc />
    protected override void OnParameterChanged(string name, float value)
    {
        base.OnParameterChanged(name, value);

        // Sync parameters when changed externally
        if (name.StartsWith("FilterSweep."))
        {
            switch (name)
            {
                case "FilterSweep.MinFrequency":
                    _filterSweep.MinFrequency = value;
                    break;
                case "FilterSweep.MaxFrequency":
                    _filterSweep.MaxFrequency = value;
                    break;
                case "FilterSweep.Resonance":
                    _filterSweep.Resonance = value;
                    break;
                case "FilterSweep.SweepPosition":
                    _filterSweep.SweepPosition = value;
                    break;
                case "FilterSweep.SweepRate":
                    _filterSweep.SweepRate = value;
                    break;
                case "FilterSweep.Direction":
                    _filterSweep.Direction = (FilterDirection)(int)value;
                    break;
            }
        }
        else if (name == "Tempo")
        {
            Tempo = value;
        }
        else if (name == "MuteInputDuringEffect")
        {
            _muteInputDuringEffect = value > 0.5f;
        }
        else if (name == "XYPad.X")
        {
            _xyPadX = value;
            ApplyXYPadMapping();
        }
        else if (name == "XYPad.Y")
        {
            _xyPadY = value;
            ApplyXYPadMapping();
        }
    }

    /// <inheritdoc />
    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;
        int sampleRate = SampleRate;

        for (int i = 0; i < count; i += channels)
        {
            float left = channels >= 1 ? sourceBuffer[i] : 0f;
            float right = channels >= 2 ? sourceBuffer[i + 1] : left;

            // Update timed effect progress
            UpdateEffectProgress(sampleRate);

            // Determine if input should be muted
            bool inputMuted = _muteInputDuringEffect && (_brake.IsActive || _backspin.IsActive);

            // Process through active effects
            (left, right) = ProcessEffects(left, right, sampleRate, inputMuted);

            // Write output
            destBuffer[offset + i] = left;
            if (channels >= 2)
            {
                destBuffer[offset + i + 1] = right;
            }

            _totalSamplesProcessed++;
        }
    }

    private void UpdateEffectProgress(int sampleRate)
    {
        // Update filter sweep (auto-sweep if rate > 0)
        if (_filterSweep.IsActive && _filterSweep.SweepRate > 0)
        {
            _filterSweep.SweepPosition += _filterSweep.SweepRate / sampleRate;
            if (_filterSweep.SweepPosition > 1f)
            {
                _filterSweep.SweepPosition = 0f;
            }
        }

        // Update echo out progress
        if (_echoOut.IsActive)
        {
            // Echo out is typically used until manually stopped
            // Progress can be used to gradually increase feedback
            float echoOutDuration = 4f; // 4 seconds to reach full effect
            _echoOut.Progress = Math.Min(1f, (_totalSamplesProcessed - _effectStartSample) / (sampleRate * echoOutDuration));
        }

        // Update brake progress
        if (_brake.IsActive)
        {
            _brake.Progress = Math.Min(1f, (_totalSamplesProcessed - _effectStartSample) / (sampleRate * _brake.BrakeTime));
            if (_brake.Progress >= 1f)
            {
                _brake.IsActive = false;
                EffectCompleted?.Invoke(this, DJEffectType.Brake);
            }
        }

        // Update backspin progress
        if (_backspin.IsActive)
        {
            _backspin.Progress = Math.Min(1f, (_totalSamplesProcessed - _effectStartSample) / (sampleRate * _backspin.SpinTime));
            if (_backspin.Progress >= 1f)
            {
                _backspin.IsActive = false;
                EffectCompleted?.Invoke(this, DJEffectType.Backspin);
            }
        }

        // Update noise build progress
        if (_noiseBuild.IsActive)
        {
            _noiseBuild.Progress = Math.Min(1f, (_totalSamplesProcessed - _effectStartSample) / (sampleRate * _noiseBuild.BuildTime));
            if (_noiseBuild.Progress >= 1f)
            {
                _noiseBuild.IsActive = false;
                EffectCompleted?.Invoke(this, DJEffectType.NoiseBuild);
            }
        }
    }

    private (float left, float right) ProcessEffects(float left, float right, int sampleRate, bool inputMuted)
    {
        // Apply filter sweep
        if (_filterSweep.IsActive)
        {
            (left, right) = _filterSweep.Process(left, right, sampleRate);
        }

        // Apply flanger
        if (_flanger.IsActive)
        {
            (left, right) = _flanger.Process(left, right, sampleRate);
        }

        // Apply echo out
        if (_echoOut.IsActive)
        {
            (left, right) = _echoOut.Process(left, right, sampleRate, inputMuted);
        }

        // Apply brake
        if (_brake.IsActive)
        {
            (left, right) = _brake.Process(left, right, sampleRate);
        }

        // Apply backspin
        if (_backspin.IsActive)
        {
            (left, right) = _backspin.Process(left, right, sampleRate);
        }

        // Add noise build
        if (_noiseBuild.IsActive)
        {
            var (noiseL, noiseR) = _noiseBuild.Process(sampleRate);
            left += noiseL;
            right += noiseR;
        }

        // Soft clip to prevent harsh distortion
        left = SoftClip(left);
        right = SoftClip(right);

        return (left, right);
    }

    private static float SoftClip(float sample)
    {
        // Gentle soft clipping using tanh approximation
        if (sample > 1f)
        {
            return 1f - 2f / (1f + MathF.Exp(2f * sample));
        }
        else if (sample < -1f)
        {
            return -1f + 2f / (1f + MathF.Exp(-2f * sample));
        }
        return sample;
    }

    #endregion

    #region Preset Factory Methods

    /// <summary>
    /// Creates preset configurations for common DJ effect settings
    /// </summary>
    /// <param name="source">The audio source to process</param>
    /// <param name="presetName">The preset name</param>
    /// <returns>A configured DJEffects instance</returns>
    public static DJEffects CreatePreset(ISampleProvider source, string presetName)
    {
        var djEffects = new DJEffects(source);

        switch (presetName.ToLowerInvariant())
        {
            case "edm":
                // EDM style with aggressive filter and echo
                djEffects.SetParameter("FilterSweep.Resonance", 8f);
                djEffects.SetParameter("FilterSweep.MinFrequency", 60f);
                djEffects.SetParameter("FilterSweep.MaxFrequency", 18000f);
                djEffects.SetParameter("EchoOut.Feedback", 0.7f);
                djEffects.SetParameter("EchoOut.Damping", 0.2f);
                djEffects.SetParameter("NoiseBuild.BuildTime", 4f);
                djEffects.SetParameter("NoiseBuild.NoiseType", (float)NoiseType.Riser);
                break;

            case "hiphop":
                // Hip-hop style with slower, groovier effects
                djEffects.SetParameter("FilterSweep.Resonance", 4f);
                djEffects.SetParameter("Brake.BrakeTime", 2f);
                djEffects.SetParameter("Brake.CurveType", 2f); // S-curve
                djEffects.SetParameter("Backspin.SpinTime", 1.2f);
                djEffects.SetParameter("EchoOut.TempoSync", 1f);
                djEffects.SetParameter("EchoOut.Division", 2f);
                break;

            case "house":
                // House style with smooth filter sweeps
                djEffects.SetParameter("FilterSweep.Resonance", 5f);
                djEffects.SetParameter("FilterSweep.SweepRate", 0.1f);
                djEffects.SetParameter("Flanger.Rate", 0.25f);
                djEffects.SetParameter("Flanger.Depth", 0.004f);
                djEffects.SetParameter("Flanger.Feedback", 0.6f);
                break;

            case "dnb":
                // Drum and bass style with fast, intense effects
                djEffects.SetParameter("FilterSweep.Resonance", 10f);
                djEffects.SetParameter("Brake.BrakeTime", 0.5f);
                djEffects.SetParameter("Brake.CurveType", 1f); // Exponential
                djEffects.SetParameter("Backspin.SpinTime", 0.4f);
                djEffects.SetParameter("NoiseBuild.BuildTime", 2f);
                djEffects.SetParameter("NoiseBuild.NoiseType", (float)NoiseType.White);
                break;

            case "techno":
                // Techno style with industrial flanger and filter
                djEffects.SetParameter("FilterSweep.Resonance", 12f);
                djEffects.SetParameter("FilterSweep.MinFrequency", 40f);
                djEffects.SetParameter("Flanger.Rate", 0.1f);
                djEffects.SetParameter("Flanger.Feedback", 0.85f);
                djEffects.SetParameter("Flanger.Shape", (float)FlangerShape.Triangle);
                djEffects.SetParameter("EchoOut.Damping", 0.5f);
                break;

            default:
                // Default balanced settings
                break;
        }

        return djEffects;
    }

    /// <summary>
    /// Gets a list of available preset names
    /// </summary>
    public static IReadOnlyList<string> AvailablePresets { get; } = new[]
    {
        "default",
        "edm",
        "hiphop",
        "house",
        "dnb",
        "techno"
    };

    #endregion
}
