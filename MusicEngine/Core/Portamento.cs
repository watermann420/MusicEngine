// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using System;


namespace MusicEngine.Core;


/// <summary>
/// Portamento mode determining when glide is applied
/// </summary>
public enum PortamentoMode
{
    /// <summary>Always glide between notes</summary>
    Always,
    /// <summary>Only glide when notes overlap (legato playing)</summary>
    Legato
}


/// <summary>
/// Portamento curve type for pitch interpolation
/// </summary>
public enum PortamentoCurve
{
    /// <summary>Linear interpolation between pitches</summary>
    Linear,
    /// <summary>Exponential curve (more natural sounding for pitch)</summary>
    Exponential
}


/// <summary>
/// Portamento processor for smooth pitch gliding between notes.
/// Handles pitch transitions with configurable glide time and curve types.
/// </summary>
public class PortamentoProcessor
{
    private double _currentFrequency;
    private double _targetFrequency;
    private double _startFrequency;
    private double _glideProgress;
    private bool _isGliding;
    private readonly object _lock = new();

    /// <summary>
    /// Gets or sets the current frequency (Hz)
    /// </summary>
    public double CurrentFrequency
    {
        get => _currentFrequency;
        set
        {
            lock (_lock)
            {
                _currentFrequency = Math.Max(0.001, value);
            }
        }
    }

    /// <summary>
    /// Gets or sets the target frequency to glide to (Hz)
    /// </summary>
    public double TargetFrequency
    {
        get => _targetFrequency;
        set
        {
            lock (_lock)
            {
                _targetFrequency = Math.Max(0.001, value);
            }
        }
    }

    /// <summary>
    /// Gets or sets the glide time in seconds
    /// </summary>
    public double GlideTime { get; set; } = 0.1;

    /// <summary>
    /// Gets or sets the portamento mode
    /// </summary>
    public PortamentoMode Mode { get; set; } = PortamentoMode.Always;

    /// <summary>
    /// Gets or sets the portamento curve type
    /// </summary>
    public PortamentoCurve Curve { get; set; } = PortamentoCurve.Exponential;

    /// <summary>
    /// Gets or sets whether portamento is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets whether the processor is currently gliding between pitches
    /// </summary>
    public bool IsGliding => _isGliding;

    /// <summary>
    /// Gets the current glide progress (0-1)
    /// </summary>
    public double GlideProgress => _glideProgress;

    /// <summary>
    /// Creates a new portamento processor with default settings
    /// </summary>
    public PortamentoProcessor()
    {
        _currentFrequency = 440.0;
        _targetFrequency = 440.0;
        _startFrequency = 440.0;
    }

    /// <summary>
    /// Creates a new portamento processor with specified glide time
    /// </summary>
    /// <param name="glideTime">Glide time in seconds</param>
    public PortamentoProcessor(double glideTime)
    {
        GlideTime = Math.Max(0, glideTime);
        _currentFrequency = 440.0;
        _targetFrequency = 440.0;
        _startFrequency = 440.0;
    }

    /// <summary>
    /// Creates a new portamento processor with specified settings
    /// </summary>
    /// <param name="glideTime">Glide time in seconds</param>
    /// <param name="mode">Portamento mode</param>
    /// <param name="curve">Portamento curve type</param>
    public PortamentoProcessor(double glideTime, PortamentoMode mode, PortamentoCurve curve)
    {
        GlideTime = Math.Max(0, glideTime);
        Mode = mode;
        Curve = curve;
        _currentFrequency = 440.0;
        _targetFrequency = 440.0;
        _startFrequency = 440.0;
    }

    /// <summary>
    /// Trigger a note on event with the target frequency
    /// </summary>
    /// <param name="targetFrequency">Target frequency in Hz</param>
    /// <param name="isLegato">Whether this is a legato (overlapping) note</param>
    public void NoteOn(float targetFrequency, bool isLegato)
    {
        lock (_lock)
        {
            double newTarget = Math.Max(0.001, targetFrequency);

            // Determine if we should glide
            bool shouldGlide = Enabled && GlideTime > 0;

            if (Mode == PortamentoMode.Legato)
            {
                shouldGlide = shouldGlide && isLegato;
            }

            if (shouldGlide && Math.Abs(_currentFrequency - newTarget) > 0.001)
            {
                _startFrequency = _currentFrequency;
                _targetFrequency = newTarget;
                _glideProgress = 0;
                _isGliding = true;
            }
            else
            {
                // Instant jump to target
                _currentFrequency = newTarget;
                _targetFrequency = newTarget;
                _startFrequency = newTarget;
                _glideProgress = 1.0;
                _isGliding = false;
            }
        }
    }

    /// <summary>
    /// Trigger a note on event with MIDI note number
    /// </summary>
    /// <param name="midiNote">MIDI note number (0-127)</param>
    /// <param name="isLegato">Whether this is a legato (overlapping) note</param>
    public void NoteOn(int midiNote, bool isLegato)
    {
        float frequency = (float)(440.0 * Math.Pow(2.0, (midiNote - 69) / 12.0));
        NoteOn(frequency, isLegato);
    }

    /// <summary>
    /// Process the portamento and return the interpolated frequency
    /// </summary>
    /// <param name="deltaTime">Time elapsed since last process in seconds</param>
    /// <returns>Current interpolated frequency in Hz</returns>
    public double Process(double deltaTime)
    {
        lock (_lock)
        {
            if (!_isGliding || GlideTime <= 0)
            {
                _currentFrequency = _targetFrequency;
                return _currentFrequency;
            }

            // Update glide progress
            _glideProgress += deltaTime / GlideTime;

            if (_glideProgress >= 1.0)
            {
                _glideProgress = 1.0;
                _currentFrequency = _targetFrequency;
                _isGliding = false;
            }
            else
            {
                // Apply curve and interpolate
                double t = ApplyCurve(_glideProgress);

                if (Curve == PortamentoCurve.Exponential)
                {
                    // Exponential interpolation in frequency domain (sounds more natural)
                    double logStart = Math.Log(_startFrequency);
                    double logTarget = Math.Log(_targetFrequency);
                    _currentFrequency = Math.Exp(logStart + (logTarget - logStart) * t);
                }
                else
                {
                    // Linear interpolation
                    _currentFrequency = _startFrequency + (_targetFrequency - _startFrequency) * t;
                }
            }

            return _currentFrequency;
        }
    }

    /// <summary>
    /// Get the current pitch with glide applied
    /// </summary>
    /// <returns>Current frequency in Hz</returns>
    public double GetCurrentPitch()
    {
        lock (_lock)
        {
            return _currentFrequency;
        }
    }

    /// <summary>
    /// Get the current pitch as a MIDI note number (may be fractional during glide)
    /// </summary>
    /// <returns>MIDI note number (potentially fractional)</returns>
    public double GetCurrentMidiNote()
    {
        lock (_lock)
        {
            return 69.0 + 12.0 * Math.Log2(_currentFrequency / 440.0);
        }
    }

    /// <summary>
    /// Reset the portamento processor
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _currentFrequency = 440.0;
            _targetFrequency = 440.0;
            _startFrequency = 440.0;
            _glideProgress = 1.0;
            _isGliding = false;
        }
    }

    /// <summary>
    /// Reset the portamento processor to a specific frequency
    /// </summary>
    /// <param name="frequency">Starting frequency in Hz</param>
    public void Reset(double frequency)
    {
        lock (_lock)
        {
            _currentFrequency = Math.Max(0.001, frequency);
            _targetFrequency = _currentFrequency;
            _startFrequency = _currentFrequency;
            _glideProgress = 1.0;
            _isGliding = false;
        }
    }

    /// <summary>
    /// Apply curve shaping to the glide progress
    /// </summary>
    private double ApplyCurve(double t)
    {
        switch (Curve)
        {
            case PortamentoCurve.Exponential:
                // S-curve for smooth transitions
                return t * t * (3.0 - 2.0 * t);

            case PortamentoCurve.Linear:
            default:
                return t;
        }
    }

    /// <summary>
    /// Set the current frequency immediately without glide
    /// </summary>
    /// <param name="frequency">Frequency in Hz</param>
    public void SetFrequencyImmediate(double frequency)
    {
        lock (_lock)
        {
            _currentFrequency = Math.Max(0.001, frequency);
            _targetFrequency = _currentFrequency;
            _startFrequency = _currentFrequency;
            _glideProgress = 1.0;
            _isGliding = false;
        }
    }

    /// <summary>
    /// Convert MIDI note to frequency
    /// </summary>
    public static double MidiToFrequency(int midiNote)
    {
        return 440.0 * Math.Pow(2.0, (midiNote - 69) / 12.0);
    }

    /// <summary>
    /// Convert frequency to MIDI note
    /// </summary>
    public static double FrequencyToMidi(double frequency)
    {
        return 69.0 + 12.0 * Math.Log2(frequency / 440.0);
    }
}
