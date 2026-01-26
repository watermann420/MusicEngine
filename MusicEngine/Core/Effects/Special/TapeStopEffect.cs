// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Special;

/// <summary>
/// Curve types for the tape stop slowdown behavior.
/// </summary>
public enum TapeStopCurve
{
    /// <summary>
    /// Linear slowdown - constant deceleration.
    /// </summary>
    Linear,

    /// <summary>
    /// Exponential slowdown - starts fast, slows down gradually.
    /// </summary>
    Exponential,

    /// <summary>
    /// S-curve slowdown - smooth acceleration and deceleration.
    /// </summary>
    SCurve
}

/// <summary>
/// Tape stop effect that simulates a tape or vinyl slowing down and stopping.
/// This is a DJ/performance effect commonly used for transitions.
/// </summary>
/// <remarks>
/// The effect uses a variable-rate sample buffer to simulate the pitch drop
/// that occurs when playback speed decreases. When triggered, it gradually
/// reduces the playback rate from 1.0 to 0.0 over the specified stop time.
/// </remarks>
public class TapeStopEffect : EffectBase
{
    private const int MaxBufferSeconds = 10;

    private readonly float[] _circularBuffer;
    private readonly int _bufferSize;
    private int _writePosition;
    private double _readPosition;
    private bool _isActive;
    private bool _isReverse;
    private double _currentSpeed;
    private double _effectProgress;
    private long _triggerSample;
    private long _totalSamplesProcessed;

    // Pre-calculated curve values
    private double _stopTimeSamples;

    /// <summary>
    /// Gets the name of the effect.
    /// </summary>
    public new string Name => "Tape Stop";

    /// <summary>
    /// Gets or sets the time it takes to stop completely (in seconds).
    /// Range: 0.1 to 5.0 seconds.
    /// </summary>
    public float StopTime
    {
        get => GetParameter("StopTime");
        set
        {
            float clamped = Math.Clamp(value, 0.1f, 5f);
            SetParameter("StopTime", clamped);
            _stopTimeSamples = clamped * SampleRate;
        }
    }

    /// <summary>
    /// Gets or sets the curve type for the slowdown behavior.
    /// </summary>
    public TapeStopCurve Curve
    {
        get => (TapeStopCurve)(int)GetParameter("Curve");
        set => SetParameter("Curve", (float)(int)value);
    }

    /// <summary>
    /// Gets or sets the exponential curve factor (1.0 - 5.0).
    /// Higher values create a more aggressive initial slowdown.
    /// Only applies when Curve is set to Exponential.
    /// </summary>
    public float ExponentialFactor
    {
        get => GetParameter("ExponentialFactor");
        set => SetParameter("ExponentialFactor", Math.Clamp(value, 1f, 5f));
    }

    /// <summary>
    /// Gets whether the tape stop effect is currently active.
    /// </summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// Gets whether the effect is in reverse mode (tape starting up).
    /// </summary>
    public bool IsReverse => _isReverse;

    /// <summary>
    /// Gets the current playback speed (1.0 = normal, 0.0 = stopped).
    /// </summary>
    public double CurrentSpeed => _currentSpeed;

    /// <summary>
    /// Gets the current effect progress (0.0 to 1.0).
    /// </summary>
    public double Progress => _effectProgress;

    /// <summary>
    /// Event raised when the tape stop effect completes.
    /// </summary>
    public event EventHandler? EffectCompleted;

    /// <summary>
    /// Creates a new tape stop effect.
    /// </summary>
    /// <param name="source">The audio source to process.</param>
    public TapeStopEffect(ISampleProvider source)
        : base(source, "Tape Stop")
    {
        // Allocate buffer for maximum stop time at sample rate
        _bufferSize = MaxBufferSeconds * source.WaveFormat.SampleRate * source.WaveFormat.Channels;
        _circularBuffer = new float[_bufferSize];
        _writePosition = 0;
        _readPosition = 0;
        _currentSpeed = 1.0;
        _effectProgress = 0.0;
        _isActive = false;
        _isReverse = false;
        _totalSamplesProcessed = 0;

        // Register parameters with defaults
        RegisterParameter("StopTime", 1f);
        RegisterParameter("Curve", (float)(int)TapeStopCurve.Exponential);
        RegisterParameter("ExponentialFactor", 2f);
        RegisterParameter("Mix", 1f);

        _stopTimeSamples = 1f * SampleRate;
    }

    /// <summary>
    /// Triggers the tape stop effect.
    /// </summary>
    /// <param name="reverse">If true, simulates tape starting up instead of stopping.</param>
    public void Trigger(bool reverse = false)
    {
        _isActive = true;
        _isReverse = reverse;
        _triggerSample = _totalSamplesProcessed;
        _effectProgress = 0.0;
        _currentSpeed = reverse ? 0.0 : 1.0;

        // Recalculate stop time in samples
        _stopTimeSamples = StopTime * SampleRate;
    }

    /// <summary>
    /// Triggers the tape stop effect (tape slowing down).
    /// </summary>
    public void TriggerStop()
    {
        Trigger(reverse: false);
    }

    /// <summary>
    /// Triggers the reverse tape effect (tape starting up).
    /// </summary>
    public void TriggerStart()
    {
        Trigger(reverse: true);
    }

    /// <summary>
    /// Resets the effect to its initial state.
    /// </summary>
    public void Reset()
    {
        _isActive = false;
        _isReverse = false;
        _effectProgress = 0.0;
        _currentSpeed = 1.0;
        _readPosition = _writePosition;

        // Clear the buffer
        Array.Clear(_circularBuffer, 0, _circularBuffer.Length);
    }

    /// <summary>
    /// Immediately stops the effect without completing.
    /// </summary>
    public void Cancel()
    {
        _isActive = false;
        _effectProgress = 0.0;
        _currentSpeed = 1.0;
    }

    /// <inheritdoc />
    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;
        int sampleRate = SampleRate;

        for (int i = 0; i < count; i += channels)
        {
            // Write incoming samples to circular buffer
            for (int ch = 0; ch < channels; ch++)
            {
                _circularBuffer[_writePosition + ch] = sourceBuffer[i + ch];
            }

            if (_isActive)
            {
                // Calculate effect progress
                long samplesSinceTrigger = _totalSamplesProcessed - _triggerSample;
                _effectProgress = Math.Clamp(samplesSinceTrigger / _stopTimeSamples, 0.0, 1.0);

                // Calculate current speed based on curve type
                _currentSpeed = CalculateSpeed(_effectProgress);

                // If reverse mode, invert the speed curve
                if (_isReverse)
                {
                    _currentSpeed = CalculateSpeed(1.0 - _effectProgress);
                }

                // Read from circular buffer at variable rate
                for (int ch = 0; ch < channels; ch++)
                {
                    int readIndex = (int)_readPosition + ch;
                    if (readIndex < 0) readIndex += _bufferSize;
                    readIndex = readIndex % _bufferSize;

                    // Linear interpolation for smoother pitch shifting
                    int nextIndex = (readIndex + channels) % _bufferSize;
                    double frac = _readPosition - (int)_readPosition;
                    float sample1 = _circularBuffer[readIndex];
                    float sample2 = _circularBuffer[nextIndex];
                    float interpolated = (float)(sample1 * (1.0 - frac) + sample2 * frac);

                    destBuffer[offset + i + ch] = interpolated;
                }

                // Advance read position based on current speed
                _readPosition += _currentSpeed * channels;

                // Keep read position within buffer bounds
                if (_readPosition >= _bufferSize)
                    _readPosition -= _bufferSize;
                if (_readPosition < 0)
                    _readPosition += _bufferSize;

                // Check if effect is complete
                if (_effectProgress >= 1.0)
                {
                    _isActive = false;
                    EffectCompleted?.Invoke(this, EventArgs.Empty);

                    // If stopping, output silence
                    if (!_isReverse)
                    {
                        _currentSpeed = 0.0;
                    }
                    else
                    {
                        _currentSpeed = 1.0;
                    }
                }
            }
            else
            {
                // Effect not active - pass through or output silence if stopped
                if (_currentSpeed < 0.01 && !_isReverse)
                {
                    // Stopped state - output silence
                    for (int ch = 0; ch < channels; ch++)
                    {
                        destBuffer[offset + i + ch] = 0f;
                    }
                }
                else
                {
                    // Normal passthrough
                    for (int ch = 0; ch < channels; ch++)
                    {
                        destBuffer[offset + i + ch] = sourceBuffer[i + ch];
                    }
                    // Keep read position synced with write position
                    _readPosition = _writePosition;
                }
            }

            // Advance write position
            _writePosition = (_writePosition + channels) % _bufferSize;
            _totalSamplesProcessed++;
        }
    }

    /// <summary>
    /// Calculates the playback speed based on the effect progress and curve type.
    /// </summary>
    /// <param name="progress">Effect progress from 0.0 to 1.0.</param>
    /// <returns>Playback speed from 1.0 (normal) to 0.0 (stopped).</returns>
    private double CalculateSpeed(double progress)
    {
        double speed;
        TapeStopCurve curve = Curve;
        float expFactor = ExponentialFactor;

        switch (curve)
        {
            case TapeStopCurve.Linear:
                // Linear: constant deceleration
                speed = 1.0 - progress;
                break;

            case TapeStopCurve.Exponential:
                // Exponential: fast initial slowdown, gradual approach to zero
                // Using exponential decay: e^(-factor * progress)
                speed = Math.Exp(-expFactor * progress);
                // Normalize to ensure we reach 0 at progress = 1
                double endValue = Math.Exp(-expFactor);
                speed = (speed - endValue) / (1.0 - endValue);
                speed = Math.Max(0, speed);
                break;

            case TapeStopCurve.SCurve:
                // S-curve: smooth acceleration and deceleration
                // Using smoothstep: 3x^2 - 2x^3
                double t = progress;
                double smoothed = t * t * (3.0 - 2.0 * t);
                speed = 1.0 - smoothed;
                break;

            default:
                speed = 1.0 - progress;
                break;
        }

        return Math.Clamp(speed, 0.0, 1.0);
    }

    /// <summary>
    /// Creates preset configurations for common tape stop effects.
    /// </summary>
    /// <param name="source">The audio source to process.</param>
    /// <param name="presetName">The preset name (quick, normal, slow, dj, vinyl).</param>
    /// <returns>A configured TapeStopEffect instance.</returns>
    public static TapeStopEffect CreatePreset(ISampleProvider source, string presetName)
    {
        var effect = new TapeStopEffect(source);

        switch (presetName.ToLowerInvariant())
        {
            case "quick":
                // Quick DJ scratch-like stop
                effect.StopTime = 0.2f;
                effect.Curve = TapeStopCurve.Exponential;
                effect.ExponentialFactor = 3f;
                break;

            case "normal":
                // Standard tape stop
                effect.StopTime = 1f;
                effect.Curve = TapeStopCurve.Exponential;
                effect.ExponentialFactor = 2f;
                break;

            case "slow":
                // Slow dramatic stop
                effect.StopTime = 3f;
                effect.Curve = TapeStopCurve.SCurve;
                break;

            case "dj":
                // DJ-style turntable stop
                effect.StopTime = 0.5f;
                effect.Curve = TapeStopCurve.Exponential;
                effect.ExponentialFactor = 4f;
                break;

            case "vinyl":
                // Realistic vinyl record stop
                effect.StopTime = 2f;
                effect.Curve = TapeStopCurve.Exponential;
                effect.ExponentialFactor = 1.5f;
                break;

            case "linear":
                // Simple linear stop
                effect.StopTime = 1f;
                effect.Curve = TapeStopCurve.Linear;
                break;
        }

        return effect;
    }
}
