//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Base class for MPE-aware synthesizer voices with per-note expression support.


using System;
using MusicEngine.Core;


namespace MusicEngine.Core.Midi;


/// <summary>
/// Base class for MPE-aware synthesizer voices.
/// Provides per-note expression handling with smooth parameter interpolation.
/// </summary>
/// <remarks>
/// MPE voices differ from standard polyphonic voices in that each voice has:
/// - Independent pitch bend (typically 48 semitones range)
/// - Per-voice slide (Y-axis, CC74) for timbre control
/// - Per-voice pressure (Z-axis, aftertouch) for modulation
/// - Smooth glide/portamento per voice
///
/// Inherit from this class and override ProcessSample() to implement the audio generation.
/// </remarks>
public abstract class MpeVoice
{
    /// <summary>
    /// Sample rate for audio generation.
    /// </summary>
    protected readonly int SampleRate;

    private double _currentFrequency;
    private double _targetFrequency;
    private float _currentSlide;
    private float _currentPressure;
    private float _currentVolume;

    /// <summary>
    /// Gets the unique note ID for this voice.
    /// </summary>
    public int NoteId { get; private set; }

    /// <summary>
    /// Gets the MIDI channel this voice is playing on.
    /// </summary>
    public int Channel { get; private set; }

    /// <summary>
    /// Gets the MIDI note number.
    /// </summary>
    public int NoteNumber { get; private set; }

    /// <summary>
    /// Gets the initial strike velocity (0-1).
    /// </summary>
    public float StrikeVelocity { get; private set; }

    /// <summary>
    /// Gets the release velocity (0-1).
    /// </summary>
    public float LiftVelocity { get; private set; }

    /// <summary>
    /// Gets the current per-note expression data.
    /// </summary>
    public PerNoteExpression? CurrentExpression { get; private set; }

    /// <summary>
    /// Gets whether this voice is currently active.
    /// </summary>
    public bool IsActive { get; protected set; }

    /// <summary>
    /// Gets whether this voice is in the release phase.
    /// </summary>
    public bool IsReleasing { get; protected set; }

    /// <summary>
    /// Gets the time when this voice was triggered.
    /// </summary>
    public DateTime TriggerTime { get; private set; }

    /// <summary>
    /// Gets the current frequency in Hz (with pitch bend applied).
    /// </summary>
    public double Frequency => _currentFrequency;

    /// <summary>
    /// Gets the current slide value (0-1).
    /// </summary>
    public float Slide => _currentSlide;

    /// <summary>
    /// Gets the current pressure value (0-1).
    /// </summary>
    public float Pressure => _currentPressure;

    /// <summary>
    /// Gets or sets the glide time in seconds for pitch transitions.
    /// </summary>
    public float GlideTime { get; set; } = 0f;

    /// <summary>
    /// Gets or sets the expression smoothing time in seconds.
    /// </summary>
    public float ExpressionSmoothTime { get; set; } = 0.005f;

    /// <summary>
    /// Gets or sets the pitch bend range in semitones.
    /// </summary>
    public int PitchBendRange { get; set; } = 48;

    /// <summary>
    /// Amplitude envelope for this voice.
    /// </summary>
    public Envelope AmpEnvelope { get; }

    /// <summary>
    /// Filter envelope for this voice.
    /// </summary>
    public Envelope FilterEnvelope { get; }

    /// <summary>
    /// Creates a new MPE voice.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    protected MpeVoice(int sampleRate)
    {
        SampleRate = sampleRate;
        AmpEnvelope = new Envelope(0.01, 0.1, 0.7, 0.3);
        FilterEnvelope = new Envelope(0.01, 0.2, 0.5, 0.5);
    }

    /// <summary>
    /// Triggers the voice with the given expression data.
    /// </summary>
    /// <param name="expression">Per-note expression data.</param>
    public virtual void Trigger(PerNoteExpression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        NoteId = expression.NoteId;
        Channel = expression.Channel;
        NoteNumber = expression.NoteNumber;
        StrikeVelocity = expression.Strike;
        CurrentExpression = expression;

        // Set initial frequency
        _targetFrequency = expression.Frequency;
        if (GlideTime <= 0 || !IsActive)
        {
            _currentFrequency = _targetFrequency;
        }

        // Set initial expression values
        _currentSlide = expression.Slide;
        _currentPressure = expression.Pressure;
        _currentVolume = StrikeVelocity;

        TriggerTime = DateTime.Now;
        IsActive = true;
        IsReleasing = false;

        // Trigger envelopes
        int velocity = (int)(StrikeVelocity * 127);
        AmpEnvelope.Trigger(velocity);
        FilterEnvelope.Trigger(velocity);

        OnTriggered();
    }

    /// <summary>
    /// Releases the voice.
    /// </summary>
    /// <param name="releaseVelocity">Release velocity (0-1).</param>
    public virtual void Release(float releaseVelocity = 0.5f)
    {
        if (!IsActive) return;

        LiftVelocity = releaseVelocity;
        IsReleasing = true;

        AmpEnvelope.Release_Gate();
        FilterEnvelope.Release_Gate();

        if (CurrentExpression != null)
        {
            CurrentExpression.Release((int)(releaseVelocity * 127));
        }

        OnReleased();
    }

    /// <summary>
    /// Updates the voice with new expression data.
    /// </summary>
    /// <param name="expression">Updated expression data.</param>
    public virtual void ApplyExpression(PerNoteExpression expression)
    {
        if (expression == null || expression.NoteId != NoteId) return;

        CurrentExpression = expression;
        _targetFrequency = expression.Frequency;
    }

    /// <summary>
    /// Processes one sample of audio output.
    /// Call UpdateExpressionSmoothing() before generating audio.
    /// </summary>
    /// <param name="deltaTime">Time since last sample in seconds.</param>
    /// <returns>The audio sample value.</returns>
    public float Process(double deltaTime)
    {
        if (!IsActive) return 0f;

        // Update expression smoothing
        UpdateExpressionSmoothing(deltaTime);

        // Process envelopes
        double ampEnv = AmpEnvelope.Process(deltaTime);
        double filterEnv = FilterEnvelope.Process(deltaTime);

        // Check if voice should become inactive
        if (AmpEnvelope.Stage == EnvelopeStage.Idle)
        {
            IsActive = false;
            OnDeactivated();
            return 0f;
        }

        // Generate the actual audio sample
        float sample = ProcessSample(deltaTime, ampEnv, filterEnv);

        return sample;
    }

    /// <summary>
    /// Override this method to generate the audio sample.
    /// </summary>
    /// <param name="deltaTime">Time since last sample in seconds.</param>
    /// <param name="ampEnvelope">Current amplitude envelope value (0-1).</param>
    /// <param name="filterEnvelope">Current filter envelope value (0-1).</param>
    /// <returns>The generated audio sample.</returns>
    protected abstract float ProcessSample(double deltaTime, double ampEnvelope, double filterEnvelope);

    /// <summary>
    /// Updates expression parameter smoothing.
    /// </summary>
    protected virtual void UpdateExpressionSmoothing(double deltaTime)
    {
        if (CurrentExpression == null) return;

        // Calculate smoothing coefficient
        double smoothCoeff = 1.0;
        if (ExpressionSmoothTime > 0)
        {
            smoothCoeff = 1.0 - Math.Exp(-deltaTime / ExpressionSmoothTime);
        }

        // Smooth frequency (glide)
        if (GlideTime > 0 && _currentFrequency != _targetFrequency)
        {
            double glideCoeff = 1.0 - Math.Exp(-deltaTime / GlideTime);
            _currentFrequency += (_targetFrequency - _currentFrequency) * glideCoeff;
        }
        else
        {
            _currentFrequency = _targetFrequency;
        }

        // Smooth slide and pressure
        float targetSlide = CurrentExpression.Slide;
        float targetPressure = CurrentExpression.Pressure;

        _currentSlide += (targetSlide - _currentSlide) * (float)smoothCoeff;
        _currentPressure += (targetPressure - _currentPressure) * (float)smoothCoeff;
    }

    /// <summary>
    /// Resets the voice to its initial state.
    /// </summary>
    public virtual void Reset()
    {
        IsActive = false;
        IsReleasing = false;
        NoteId = -1;
        Channel = -1;
        NoteNumber = -1;
        CurrentExpression = null;
        _currentFrequency = 0;
        _targetFrequency = 0;
        _currentSlide = 0.5f;
        _currentPressure = 0;
        _currentVolume = 0;
        StrikeVelocity = 0;
        LiftVelocity = 0;

        AmpEnvelope.Reset();
        FilterEnvelope.Reset();
    }

    /// <summary>
    /// Called when the voice is triggered. Override for custom initialization.
    /// </summary>
    protected virtual void OnTriggered() { }

    /// <summary>
    /// Called when the voice is released. Override for custom release behavior.
    /// </summary>
    protected virtual void OnReleased() { }

    /// <summary>
    /// Called when the voice becomes inactive. Override for cleanup.
    /// </summary>
    protected virtual void OnDeactivated() { }

    /// <summary>
    /// Converts the current frequency to a MIDI note number (may be fractional).
    /// </summary>
    public double GetMidiNoteNumber()
    {
        if (_currentFrequency <= 0) return 0;
        return 69.0 + 12.0 * Math.Log2(_currentFrequency / 440.0);
    }

    /// <summary>
    /// Gets the pitch bend amount in semitones from the base note.
    /// </summary>
    public double GetPitchBendSemitones()
    {
        return CurrentExpression?.PitchBendSemitones ?? 0;
    }
}


/// <summary>
/// Simple implementation of an MPE voice with basic oscillator.
/// Can be used as a reference implementation.
/// </summary>
public class SimpleMpeVoice : MpeVoice
{
    private double _phase;
    private readonly Random _random = new();

    /// <summary>
    /// Gets or sets the waveform type.
    /// </summary>
    public WaveType Waveform { get; set; } = WaveType.Sawtooth;

    /// <summary>
    /// Gets or sets the base filter cutoff (0-1).
    /// </summary>
    public float FilterCutoff { get; set; } = 0.8f;

    /// <summary>
    /// Gets or sets the filter resonance (0-1).
    /// </summary>
    public float FilterResonance { get; set; } = 0.2f;

    /// <summary>
    /// Gets or sets how much slide affects the filter cutoff.
    /// </summary>
    public float SlideToFilterAmount { get; set; } = 0.5f;

    /// <summary>
    /// Gets or sets how much pressure affects the volume.
    /// </summary>
    public float PressureToVolumeAmount { get; set; } = 0.3f;

    /// <summary>
    /// Gets or sets how much pressure adds vibrato.
    /// </summary>
    public float PressureToVibratoAmount { get; set; } = 0.1f;

    // Simple filter state
    private double _filterState;

    public SimpleMpeVoice(int sampleRate) : base(sampleRate)
    {
    }

    protected override void OnTriggered()
    {
        _phase = 0;
        _filterState = 0;
    }

    protected override float ProcessSample(double deltaTime, double ampEnvelope, double filterEnvelope)
    {
        // Calculate vibrato from pressure
        double vibrato = 0;
        if (PressureToVibratoAmount > 0 && Pressure > 0.1f)
        {
            vibrato = Math.Sin(_phase * 6) * Pressure * PressureToVibratoAmount * 0.5;
        }

        // Calculate frequency with vibrato
        double freq = Frequency * Math.Pow(2.0, vibrato / 12.0);

        // Phase increment
        double phaseInc = 2.0 * Math.PI * freq / SampleRate;
        _phase += phaseInc;
        if (_phase >= 2.0 * Math.PI) _phase -= 2.0 * Math.PI;

        // Generate waveform
        double sample = GenerateWaveform(_phase);

        // Apply filter (slide affects cutoff)
        double effectiveCutoff = FilterCutoff + (Slide - 0.5f) * 2 * SlideToFilterAmount;
        effectiveCutoff = Math.Clamp(effectiveCutoff + filterEnvelope * 0.3, 0, 1);
        sample = ApplyFilter(sample, effectiveCutoff);

        // Apply amplitude envelope
        sample *= ampEnvelope;

        // Apply velocity
        sample *= StrikeVelocity;

        // Apply pressure to volume
        if (PressureToVolumeAmount > 0)
        {
            double pressureVolume = 1.0 + Pressure * PressureToVolumeAmount;
            sample *= pressureVolume;
        }

        return (float)sample;
    }

    private double GenerateWaveform(double phase)
    {
        return Waveform switch
        {
            WaveType.Sine => Math.Sin(phase),
            WaveType.Square => phase < Math.PI ? 1.0 : -1.0,
            WaveType.Sawtooth => (phase / Math.PI) - 1.0,
            WaveType.Triangle => phase < Math.PI
                ? (2.0 * phase / Math.PI) - 1.0
                : 3.0 - (2.0 * phase / Math.PI),
            WaveType.Noise => _random.NextDouble() * 2.0 - 1.0,
            _ => Math.Sin(phase)
        };
    }

    private double ApplyFilter(double input, double cutoff)
    {
        // Simple one-pole lowpass
        double freq = 20.0 * Math.Pow(1000.0, cutoff);
        freq = Math.Min(freq, SampleRate * 0.45);
        double rc = 1.0 / (2.0 * Math.PI * freq);
        double dt = 1.0 / SampleRate;
        double alpha = dt / (rc + dt);

        _filterState = _filterState + alpha * (input - _filterState);
        return _filterState;
    }
}
