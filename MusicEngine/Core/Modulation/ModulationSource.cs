namespace MusicEngine.Core.Modulation;

/// <summary>
/// Types of modulation sources available in the modulation matrix.
/// </summary>
public enum ModulationSourceType
{
    /// <summary>Low Frequency Oscillator - periodic modulation</summary>
    LFO,
    /// <summary>ADSR Envelope - attack/decay/sustain/release curve</summary>
    Envelope,
    /// <summary>Note velocity - from MIDI note-on</summary>
    Velocity,
    /// <summary>Aftertouch/channel pressure</summary>
    Aftertouch,
    /// <summary>Mod wheel (CC1)</summary>
    ModWheel,
    /// <summary>Expression pedal (CC11)</summary>
    Expression,
    /// <summary>Random/sample-and-hold value</summary>
    Random,
    /// <summary>Pitch bend wheel</summary>
    PitchBend,
    /// <summary>Note number (for keyboard tracking)</summary>
    KeyTrack,
    /// <summary>MPE Slide (CC74)</summary>
    MPESlide,
    /// <summary>MPE Pressure (channel aftertouch)</summary>
    MPEPressure
}

/// <summary>
/// Waveform shapes for LFO modulation sources.
/// </summary>
public enum LFOWaveform
{
    /// <summary>Smooth sine wave</summary>
    Sine,
    /// <summary>Triangle wave</summary>
    Triangle,
    /// <summary>Rising sawtooth</summary>
    Sawtooth,
    /// <summary>Falling sawtooth</summary>
    ReverseSawtooth,
    /// <summary>Square wave (50% duty cycle)</summary>
    Square,
    /// <summary>Random sample-and-hold</summary>
    SampleAndHold,
    /// <summary>Smoothed random</summary>
    SmoothedRandom
}

/// <summary>
/// Represents a modulation source that generates values for the modulation matrix.
/// </summary>
public class ModulationSource
{
    private float _phase;
    private float _targetRandomValue;
    private float _currentRandomValue;
    private readonly Random _random = new();

    /// <summary>
    /// Unique identifier for this modulation source.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// Display name for this source.
    /// </summary>
    public string Name { get; set; } = "Unnamed";

    /// <summary>
    /// The type of modulation source.
    /// </summary>
    public ModulationSourceType Type { get; }

    /// <summary>
    /// Current output value (0-1 for unipolar, -1 to 1 for bipolar sources).
    /// </summary>
    public float Value { get; set; }

    /// <summary>
    /// LFO rate in Hz (for LFO type).
    /// </summary>
    public float Rate { get; set; } = 1f;

    /// <summary>
    /// LFO waveform shape (for LFO type).
    /// </summary>
    public LFOWaveform Waveform { get; set; } = LFOWaveform.Sine;

    /// <summary>
    /// LFO phase offset (0-1).
    /// </summary>
    public float PhaseOffset { get; set; }

    /// <summary>
    /// Whether the LFO syncs to tempo.
    /// </summary>
    public bool TempoSync { get; set; }

    /// <summary>
    /// Tempo sync division (e.g., 1 = quarter note, 0.5 = eighth note).
    /// </summary>
    public float SyncDivision { get; set; } = 1f;

    /// <summary>
    /// Envelope attack time in seconds (for Envelope type).
    /// </summary>
    public float Attack { get; set; } = 0.01f;

    /// <summary>
    /// Envelope decay time in seconds (for Envelope type).
    /// </summary>
    public float Decay { get; set; } = 0.1f;

    /// <summary>
    /// Envelope sustain level 0-1 (for Envelope type).
    /// </summary>
    public float Sustain { get; set; } = 0.7f;

    /// <summary>
    /// Envelope release time in seconds (for Envelope type).
    /// </summary>
    public float Release { get; set; } = 0.3f;

    /// <summary>
    /// Current envelope stage (for Envelope type).
    /// </summary>
    public EnvelopeStage Stage { get; private set; } = EnvelopeStage.Idle;

    /// <summary>
    /// Whether this source outputs bipolar values (-1 to 1).
    /// </summary>
    public bool IsBipolar { get; set; }

    /// <summary>
    /// Creates a new modulation source of the specified type.
    /// </summary>
    /// <param name="type">The type of modulation source</param>
    public ModulationSource(ModulationSourceType type)
    {
        Type = type;
        IsBipolar = type == ModulationSourceType.LFO || type == ModulationSourceType.PitchBend;
    }

    /// <summary>
    /// Updates the modulation source's internal state.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since last update in seconds</param>
    public void Update(float deltaTime)
    {
        switch (Type)
        {
            case ModulationSourceType.LFO:
                UpdateLFO(deltaTime);
                break;

            case ModulationSourceType.Envelope:
                UpdateEnvelope(deltaTime);
                break;

            case ModulationSourceType.Random:
                UpdateRandom(deltaTime);
                break;

            // Other types are set externally via Value property
        }
    }

    private void UpdateLFO(float deltaTime)
    {
        _phase += Rate * deltaTime;
        if (_phase >= 1f)
        {
            _phase -= 1f;
            if (Waveform == LFOWaveform.SampleAndHold)
            {
                _targetRandomValue = (float)_random.NextDouble();
            }
        }

        float phase = (_phase + PhaseOffset) % 1f;
        Value = CalculateLFOValue(phase);
    }

    private float CalculateLFOValue(float phase)
    {
        float value = Waveform switch
        {
            LFOWaveform.Sine => MathF.Sin(phase * MathF.PI * 2f),
            LFOWaveform.Triangle => 1f - 4f * MathF.Abs(phase - 0.5f),
            LFOWaveform.Sawtooth => 2f * phase - 1f,
            LFOWaveform.ReverseSawtooth => 1f - 2f * phase,
            LFOWaveform.Square => phase < 0.5f ? 1f : -1f,
            LFOWaveform.SampleAndHold => _targetRandomValue * 2f - 1f,
            LFOWaveform.SmoothedRandom => SmoothRandom(phase),
            _ => 0f
        };

        return IsBipolar ? value : (value + 1f) * 0.5f;
    }

    private float SmoothRandom(float phase)
    {
        // Interpolate between random values
        float target = _targetRandomValue * 2f - 1f;
        _currentRandomValue += (target - _currentRandomValue) * 0.1f;
        return _currentRandomValue;
    }

    private void UpdateEnvelope(float deltaTime)
    {
        switch (Stage)
        {
            case EnvelopeStage.Attack:
                Value += deltaTime / Math.Max(Attack, 0.001f);
                if (Value >= 1f)
                {
                    Value = 1f;
                    Stage = EnvelopeStage.Decay;
                }
                break;

            case EnvelopeStage.Decay:
                Value -= deltaTime / Math.Max(Decay, 0.001f) * (1f - Sustain);
                if (Value <= Sustain)
                {
                    Value = Sustain;
                    Stage = EnvelopeStage.Sustain;
                }
                break;

            case EnvelopeStage.Sustain:
                Value = Sustain;
                break;

            case EnvelopeStage.Release:
                Value -= deltaTime / Math.Max(Release, 0.001f) * Value;
                if (Value <= 0.001f)
                {
                    Value = 0f;
                    Stage = EnvelopeStage.Idle;
                }
                break;

            case EnvelopeStage.Idle:
                Value = 0f;
                break;
        }
    }

    private void UpdateRandom(float deltaTime)
    {
        // Random source changes at the specified rate
        _phase += Rate * deltaTime;
        if (_phase >= 1f)
        {
            _phase -= 1f;
            _targetRandomValue = (float)_random.NextDouble();
        }

        // Smooth interpolation
        _currentRandomValue += (_targetRandomValue - _currentRandomValue) * deltaTime * 10f;
        Value = IsBipolar ? _currentRandomValue * 2f - 1f : _currentRandomValue;
    }

    /// <summary>
    /// Triggers the envelope (for Envelope type).
    /// </summary>
    public void TriggerEnvelope()
    {
        if (Type == ModulationSourceType.Envelope)
        {
            Stage = EnvelopeStage.Attack;
            Value = 0f;
        }
    }

    /// <summary>
    /// Releases the envelope (for Envelope type).
    /// </summary>
    public void ReleaseEnvelope()
    {
        if (Type == ModulationSourceType.Envelope && Stage != EnvelopeStage.Idle)
        {
            Stage = EnvelopeStage.Release;
        }
    }

    /// <summary>
    /// Resets the LFO phase to the start.
    /// </summary>
    public void ResetPhase()
    {
        _phase = 0f;
    }

    /// <summary>
    /// Sets the current tempo for tempo-synced LFOs.
    /// </summary>
    /// <param name="bpm">Tempo in beats per minute</param>
    public void SetTempo(float bpm)
    {
        if (TempoSync && Type == ModulationSourceType.LFO)
        {
            // Calculate rate based on tempo and sync division
            Rate = (bpm / 60f) / SyncDivision;
        }
    }
}

/// <summary>
/// Envelope stages for ADSR envelopes.
/// </summary>
public enum EnvelopeStage
{
    /// <summary>Envelope is not active</summary>
    Idle,
    /// <summary>Rising to peak</summary>
    Attack,
    /// <summary>Falling to sustain level</summary>
    Decay,
    /// <summary>Holding at sustain level</summary>
    Sustain,
    /// <summary>Falling to zero after key release</summary>
    Release
}
