// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: ADSR envelope generator.

using System;


namespace MusicEngine.Core;


/// <summary>
/// Envelope stages
/// </summary>
public enum EnvelopeStage
{
    /// <summary>Envelope is idle (not triggered)</summary>
    Idle,
    /// <summary>Delay before attack starts</summary>
    Delay,
    /// <summary>Attack phase - rising to peak</summary>
    Attack,
    /// <summary>Hold phase - stays at peak</summary>
    Hold,
    /// <summary>Decay phase - falling to sustain</summary>
    Decay,
    /// <summary>Sustain phase - holding at sustain level</summary>
    Sustain,
    /// <summary>Release phase - falling to zero</summary>
    Release
}


/// <summary>
/// Envelope curve types
/// </summary>
public enum EnvelopeCurve
{
    /// <summary>Linear interpolation</summary>
    Linear,
    /// <summary>Exponential curve (natural sounding)</summary>
    Exponential,
    /// <summary>Logarithmic curve</summary>
    Logarithmic,
    /// <summary>S-curve (smooth)</summary>
    SCurve
}


/// <summary>
/// ADSR Envelope generator with optional delay and hold stages.
/// Supports different curve types for natural-sounding envelopes.
/// </summary>
public class Envelope
{
    private EnvelopeStage _stage = EnvelopeStage.Idle;
    private double _value;
    private double _stageTime;
    private double _stageStartValue;
    private double _releaseStartValue;
    private bool _isGateOn;
    private readonly object _lock = new();

    // Timing parameters (in seconds)
    private double _delay = 0;
    private double _attack = 0.01;
    private double _hold = 0;
    private double _decay = 0.1;
    private double _release = 0.3;

    // Level parameters (0-1)
    private double _sustainLevel = 0.7;
    private double _peakLevel = 1.0;

    /// <summary>
    /// Gets or sets the delay time in seconds
    /// </summary>
    public double Delay
    {
        get => _delay;
        set => _delay = Math.Max(0, value);
    }

    /// <summary>
    /// Gets or sets the attack time in seconds
    /// </summary>
    public double Attack
    {
        get => _attack;
        set => _attack = Math.Max(0.001, value); // Minimum 1ms to avoid clicks
    }

    /// <summary>
    /// Gets or sets the hold time in seconds
    /// </summary>
    public double Hold
    {
        get => _hold;
        set => _hold = Math.Max(0, value);
    }

    /// <summary>
    /// Gets or sets the decay time in seconds
    /// </summary>
    public double Decay
    {
        get => _decay;
        set => _decay = Math.Max(0.001, value);
    }

    /// <summary>
    /// Gets or sets the sustain level (0-1)
    /// </summary>
    public double Sustain
    {
        get => _sustainLevel;
        set => _sustainLevel = Math.Clamp(value, 0, 1);
    }

    /// <summary>
    /// Gets or sets the release time in seconds
    /// </summary>
    public double Release
    {
        get => _release;
        set => _release = Math.Max(0.001, value);
    }

    /// <summary>
    /// Gets or sets the peak level (0-1)
    /// </summary>
    public double PeakLevel
    {
        get => _peakLevel;
        set => _peakLevel = Math.Clamp(value, 0, 1);
    }

    /// <summary>
    /// Gets or sets the curve type for attack
    /// </summary>
    public EnvelopeCurve AttackCurve { get; set; } = EnvelopeCurve.Linear;

    /// <summary>
    /// Gets or sets the curve type for decay
    /// </summary>
    public EnvelopeCurve DecayCurve { get; set; } = EnvelopeCurve.Exponential;

    /// <summary>
    /// Gets or sets the curve type for release
    /// </summary>
    public EnvelopeCurve ReleaseCurve { get; set; } = EnvelopeCurve.Exponential;

    /// <summary>
    /// Gets or sets velocity sensitivity (0-1, how much velocity affects peak level)
    /// </summary>
    public float VelocitySensitivity { get; set; } = 0.5f;

    /// <summary>
    /// Gets the current envelope stage
    /// </summary>
    public EnvelopeStage Stage => _stage;

    /// <summary>
    /// Gets the current envelope value (0-1)
    /// </summary>
    public double Value => _value;

    /// <summary>
    /// Gets whether the envelope is active (not idle)
    /// </summary>
    public bool IsActive => _stage != EnvelopeStage.Idle;

    /// <summary>
    /// Gets whether the gate is currently on
    /// </summary>
    public bool IsGateOn => _isGateOn;

    /// <summary>
    /// Creates a new envelope with default ADSR values
    /// </summary>
    public Envelope() { }

    /// <summary>
    /// Creates a new envelope with specified ADSR values
    /// </summary>
    public Envelope(double attack, double decay, double sustain, double release)
    {
        Attack = attack;
        Decay = decay;
        Sustain = sustain;
        Release = release;
    }

    /// <summary>
    /// Creates a new envelope with full DAHDSR values
    /// </summary>
    public Envelope(double delay, double attack, double hold, double decay, double sustain, double release)
    {
        Delay = delay;
        Attack = attack;
        Hold = hold;
        Decay = decay;
        Sustain = sustain;
        Release = release;
    }

    /// <summary>
    /// Trigger the envelope (gate on)
    /// </summary>
    /// <param name="velocity">Optional velocity (0-127) for velocity sensitivity</param>
    public void Trigger(int velocity = 127)
    {
        lock (_lock)
        {
            _isGateOn = true;
            _stageTime = 0;
            _stageStartValue = _value;

            // Calculate peak level based on velocity
            double velocityFactor = velocity / 127.0;
            _peakLevel = 1.0 - (1.0 - velocityFactor) * VelocitySensitivity;

            if (_delay > 0)
            {
                _stage = EnvelopeStage.Delay;
            }
            else
            {
                _stage = EnvelopeStage.Attack;
            }
        }
    }

    /// <summary>
    /// Release the envelope (gate off)
    /// </summary>
    public void Release_Gate()
    {
        lock (_lock)
        {
            if (_stage == EnvelopeStage.Idle) return;

            _isGateOn = false;
            _releaseStartValue = _value;
            _stageTime = 0;
            _stage = EnvelopeStage.Release;
        }
    }

    /// <summary>
    /// Force the envelope to idle state
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _stage = EnvelopeStage.Idle;
            _value = 0;
            _stageTime = 0;
            _isGateOn = false;
        }
    }

    /// <summary>
    /// Process the envelope and get the current value
    /// </summary>
    /// <param name="deltaTime">Time elapsed since last process in seconds</param>
    /// <returns>Current envelope value (0-1)</returns>
    public double Process(double deltaTime)
    {
        lock (_lock)
        {
            if (_stage == EnvelopeStage.Idle)
            {
                return 0;
            }

            _stageTime += deltaTime;

            switch (_stage)
            {
                case EnvelopeStage.Delay:
                    _value = 0;
                    if (_stageTime >= _delay)
                    {
                        _stage = EnvelopeStage.Attack;
                        _stageTime = 0;
                        _stageStartValue = 0;
                    }
                    break;

                case EnvelopeStage.Attack:
                    if (_attack > 0)
                    {
                        double t = Math.Min(1.0, _stageTime / _attack);
                        _value = ApplyCurve(t, _stageStartValue, _peakLevel, AttackCurve);
                    }
                    else
                    {
                        _value = _peakLevel;
                    }

                    if (_stageTime >= _attack)
                    {
                        _value = _peakLevel;
                        _stageTime = 0;
                        if (_hold > 0)
                        {
                            _stage = EnvelopeStage.Hold;
                        }
                        else
                        {
                            _stage = EnvelopeStage.Decay;
                            _stageStartValue = _peakLevel;
                        }
                    }
                    break;

                case EnvelopeStage.Hold:
                    _value = _peakLevel;
                    if (_stageTime >= _hold)
                    {
                        _stage = EnvelopeStage.Decay;
                        _stageTime = 0;
                        _stageStartValue = _peakLevel;
                    }
                    break;

                case EnvelopeStage.Decay:
                    if (_decay > 0)
                    {
                        double t = Math.Min(1.0, _stageTime / _decay);
                        _value = ApplyCurve(t, _stageStartValue, _sustainLevel, DecayCurve);
                    }
                    else
                    {
                        _value = _sustainLevel;
                    }

                    if (_stageTime >= _decay)
                    {
                        _value = _sustainLevel;
                        _stage = EnvelopeStage.Sustain;
                    }
                    break;

                case EnvelopeStage.Sustain:
                    _value = _sustainLevel;
                    // Stay in sustain until gate is released
                    break;

                case EnvelopeStage.Release:
                    if (_release > 0)
                    {
                        double t = Math.Min(1.0, _stageTime / _release);
                        _value = ApplyCurve(t, _releaseStartValue, 0, ReleaseCurve);
                    }
                    else
                    {
                        _value = 0;
                    }

                    if (_stageTime >= _release || _value <= 0.0001)
                    {
                        _value = 0;
                        _stage = EnvelopeStage.Idle;
                    }
                    break;
            }

            return _value;
        }
    }

    /// <summary>
    /// Get the envelope value at a given sample index
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz</param>
    /// <returns>Current envelope value (0-1)</returns>
    public double GetValue(int sampleRate)
    {
        double deltaTime = 1.0 / sampleRate;
        return Process(deltaTime);
    }

    /// <summary>
    /// Apply curve shaping to interpolation
    /// </summary>
    private double ApplyCurve(double t, double start, double end, EnvelopeCurve curve)
    {
        double shaped;

        switch (curve)
        {
            case EnvelopeCurve.Linear:
                shaped = t;
                break;

            case EnvelopeCurve.Exponential:
                // Use exponential curve that sounds natural
                // For decay/release (going down), use inverted exponential
                if (end < start)
                {
                    shaped = 1.0 - Math.Pow(1.0 - t, 3.0);
                }
                else
                {
                    shaped = Math.Pow(t, 0.5); // Faster attack curve
                }
                break;

            case EnvelopeCurve.Logarithmic:
                if (end < start)
                {
                    shaped = Math.Pow(t, 3.0);
                }
                else
                {
                    shaped = 1.0 - Math.Pow(1.0 - t, 0.5);
                }
                break;

            case EnvelopeCurve.SCurve:
                // Smooth S-curve using cosine interpolation
                shaped = (1.0 - Math.Cos(t * Math.PI)) / 2.0;
                break;

            default:
                shaped = t;
                break;
        }

        return start + (end - start) * shaped;
    }

    /// <summary>
    /// Set ADSR values in one call
    /// </summary>
    public void SetADSR(double attack, double decay, double sustain, double release)
    {
        Attack = attack;
        Decay = decay;
        Sustain = sustain;
        Release = release;
    }

    /// <summary>
    /// Create a quick/punchy envelope
    /// </summary>
    public static Envelope CreatePunchy()
    {
        return new Envelope(0.001, 0.05, 0.3, 0.1);
    }

    /// <summary>
    /// Create a pad/slow envelope
    /// </summary>
    public static Envelope CreatePad()
    {
        return new Envelope(0.5, 1.0, 0.8, 2.0);
    }

    /// <summary>
    /// Create an organ-like envelope (instant attack/release)
    /// </summary>
    public static Envelope CreateOrgan()
    {
        return new Envelope(0.001, 0.001, 1.0, 0.01);
    }

    /// <summary>
    /// Create a pluck-like envelope
    /// </summary>
    public static Envelope CreatePluck()
    {
        return new Envelope(0.001, 0.2, 0.0, 0.3);
    }

    /// <summary>
    /// Create a string-like envelope
    /// </summary>
    public static Envelope CreateStrings()
    {
        return new Envelope(0.3, 0.3, 0.9, 0.5);
    }
}


/// <summary>
/// Simple AR (Attack-Release) envelope for simpler modulation
/// </summary>
public class AREnvelope
{
    private double _value;
    private double _targetValue;
    private double _attackRate;
    private double _releaseRate;
    private bool _isRising;

    /// <summary>
    /// Gets or sets the attack time in seconds
    /// </summary>
    public double Attack
    {
        get => 1.0 / _attackRate;
        set => _attackRate = 1.0 / Math.Max(0.001, value);
    }

    /// <summary>
    /// Gets or sets the release time in seconds
    /// </summary>
    public double Release
    {
        get => 1.0 / _releaseRate;
        set => _releaseRate = 1.0 / Math.Max(0.001, value);
    }

    /// <summary>
    /// Gets the current envelope value (0-1)
    /// </summary>
    public double Value => _value;

    public AREnvelope(double attack = 0.01, double release = 0.1)
    {
        Attack = attack;
        Release = release;
    }

    /// <summary>
    /// Trigger the envelope
    /// </summary>
    public void Trigger()
    {
        _targetValue = 1.0;
        _isRising = true;
    }

    /// <summary>
    /// Release the envelope
    /// </summary>
    public void Release_Gate()
    {
        _targetValue = 0.0;
        _isRising = false;
    }

    /// <summary>
    /// Process the envelope
    /// </summary>
    public double Process(double deltaTime)
    {
        double rate = _isRising ? _attackRate : _releaseRate;
        double diff = _targetValue - _value;

        if (Math.Abs(diff) < 0.0001)
        {
            _value = _targetValue;
        }
        else
        {
            _value += diff * rate * deltaTime;
        }

        return _value;
    }

    /// <summary>
    /// Reset the envelope
    /// </summary>
    public void Reset()
    {
        _value = 0;
        _targetValue = 0;
        _isRising = false;
    }
}
