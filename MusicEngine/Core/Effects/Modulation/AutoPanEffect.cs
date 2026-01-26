// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Modulation system component.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Modulation;

/// <summary>
/// LFO waveform shapes for modulation effects.
/// </summary>
public enum LfoWaveform
{
    /// <summary>Smooth sinusoidal wave</summary>
    Sine = 0,
    /// <summary>Linear triangle wave</summary>
    Triangle = 1,
    /// <summary>Hard-edged square wave</summary>
    Square = 2,
    /// <summary>Rising sawtooth wave</summary>
    SawUp = 3,
    /// <summary>Falling sawtooth wave</summary>
    SawDown = 4
}

/// <summary>
/// Tempo sync note values for LFO rate synchronization.
/// </summary>
public enum TempoSyncValue
{
    /// <summary>No sync - use Hz rate</summary>
    Off = 0,
    /// <summary>1/1 - Whole note</summary>
    Whole = 1,
    /// <summary>1/2 - Half note</summary>
    Half = 2,
    /// <summary>1/4 - Quarter note</summary>
    Quarter = 4,
    /// <summary>1/8 - Eighth note</summary>
    Eighth = 8,
    /// <summary>1/16 - Sixteenth note</summary>
    Sixteenth = 16,
    /// <summary>1/32 - Thirty-second note</summary>
    ThirtySecond = 32,
    /// <summary>1/4T - Quarter note triplet</summary>
    QuarterTriplet = 6,
    /// <summary>1/8T - Eighth note triplet</summary>
    EighthTriplet = 12,
    /// <summary>1/16T - Sixteenth note triplet</summary>
    SixteenthTriplet = 24,
    /// <summary>1/4D - Dotted quarter note</summary>
    QuarterDotted = 3,
    /// <summary>1/8D - Dotted eighth note</summary>
    EighthDotted = 5,
    /// <summary>1/16D - Dotted sixteenth note</summary>
    SixteenthDotted = 10
}

/// <summary>
/// Auto-Pan effect - LFO-controlled stereo panning that creates rhythmic
/// movement of audio between left and right channels.
/// </summary>
public class AutoPanEffect : EffectBase
{
    private float _lfoPhase;
    private float _smoothedPanLeft;
    private float _smoothedPanRight;
    private float _smoothingCoeff;

    private const float TwoPi = 2f * MathF.PI;
    private const float DefaultSmoothingMs = 5f;

    /// <summary>
    /// Creates a new auto-pan effect.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <param name="name">Effect name</param>
    public AutoPanEffect(ISampleProvider source, string name)
        : base(source, name)
    {
        _lfoPhase = 0f;
        _smoothedPanLeft = 0.5f;
        _smoothedPanRight = 0.5f;

        // Initialize parameters
        RegisterParameter("Rate", 2f);              // 2 Hz modulation rate
        RegisterParameter("Depth", 1.0f);           // 100% depth
        RegisterParameter("Waveform", 0f);          // Sine wave
        RegisterParameter("PhaseOffset", 0f);       // No phase offset
        RegisterParameter("SmoothingMs", DefaultSmoothingMs); // 5ms smoothing
        RegisterParameter("TempoSync", 0f);         // Off (use Hz)
        RegisterParameter("Bpm", 120f);             // Default tempo
        RegisterParameter("Mix", 1.0f);             // 100% wet

        UpdateSmoothingCoeff();
    }

    /// <summary>
    /// Modulation rate in Hz (0.1 - 20.0).
    /// Only used when TempoSync is Off.
    /// </summary>
    public float Rate
    {
        get => GetParameter("Rate");
        set => SetParameter("Rate", Math.Clamp(value, 0.1f, 20f));
    }

    /// <summary>
    /// Modulation depth (0.0 - 1.0).
    /// 0.0 = no panning effect, 1.0 = full left-right sweep.
    /// </summary>
    public float Depth
    {
        get => GetParameter("Depth");
        set => SetParameter("Depth", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// LFO waveform shape.
    /// </summary>
    public LfoWaveform Waveform
    {
        get => (LfoWaveform)(int)GetParameter("Waveform");
        set => SetParameter("Waveform", (int)value);
    }

    /// <summary>
    /// Phase offset between left and right channels (0.0 - 1.0).
    /// 0.0 = mono panning (both channels move together).
    /// 0.5 = opposite phase (maximum stereo width).
    /// 1.0 = full cycle offset (same as 0.0).
    /// </summary>
    public float PhaseOffset
    {
        get => GetParameter("PhaseOffset");
        set => SetParameter("PhaseOffset", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Smoothing time in milliseconds (0.0 - 50.0).
    /// Higher values reduce clicking but add latency to modulation.
    /// </summary>
    public float SmoothingMs
    {
        get => GetParameter("SmoothingMs");
        set
        {
            SetParameter("SmoothingMs", Math.Clamp(value, 0f, 50f));
            UpdateSmoothingCoeff();
        }
    }

    /// <summary>
    /// Tempo sync mode. When not Off, the Rate is ignored and
    /// LFO is synced to the specified note value at the current BPM.
    /// </summary>
    public TempoSyncValue TempoSync
    {
        get => (TempoSyncValue)(int)GetParameter("TempoSync");
        set => SetParameter("TempoSync", (int)value);
    }

    /// <summary>
    /// Tempo in beats per minute (20 - 300).
    /// Used when TempoSync is enabled.
    /// </summary>
    public float Bpm
    {
        get => GetParameter("Bpm");
        set => SetParameter("Bpm", Math.Clamp(value, 20f, 300f));
    }

    /// <summary>
    /// Dry/wet mix (0.0 - 1.0).
    /// Maps to Mix parameter.
    /// </summary>
    public float DryWet
    {
        get => Mix;
        set => Mix = value;
    }

    /// <summary>
    /// Resets the LFO phase to the beginning.
    /// </summary>
    public void ResetPhase()
    {
        _lfoPhase = 0f;
    }

    /// <summary>
    /// Sets the LFO phase to a specific position (0.0 - 1.0).
    /// </summary>
    /// <param name="phase">Phase position (0.0 = start, 1.0 = full cycle)</param>
    public void SetPhase(float phase)
    {
        _lfoPhase = Math.Clamp(phase, 0f, 1f) * TwoPi;
    }

    private void UpdateSmoothingCoeff()
    {
        float smoothingMs = GetParameter("SmoothingMs");
        if (smoothingMs <= 0f)
        {
            _smoothingCoeff = 1f; // No smoothing
        }
        else
        {
            float smoothingSamples = smoothingMs * SampleRate / 1000f;
            _smoothingCoeff = 1f - MathF.Exp(-1f / smoothingSamples);
        }
    }

    /// <summary>
    /// Calculates the effective LFO rate in Hz, accounting for tempo sync.
    /// </summary>
    private float GetEffectiveRate()
    {
        var syncValue = TempoSync;
        if (syncValue == TempoSyncValue.Off)
        {
            return Rate;
        }

        float bpm = Bpm;
        float beatsPerSecond = bpm / 60f;

        // Calculate rate based on note value
        // For straight notes: rate = beatsPerSecond * (noteValue / 4)
        // e.g., 1/4 note at 120 BPM = 2 beats/sec * 1 = 2 Hz (one cycle per beat)
        // e.g., 1/8 note at 120 BPM = 2 beats/sec * 2 = 4 Hz

        return syncValue switch
        {
            TempoSyncValue.Whole => beatsPerSecond / 4f,
            TempoSyncValue.Half => beatsPerSecond / 2f,
            TempoSyncValue.Quarter => beatsPerSecond,
            TempoSyncValue.Eighth => beatsPerSecond * 2f,
            TempoSyncValue.Sixteenth => beatsPerSecond * 4f,
            TempoSyncValue.ThirtySecond => beatsPerSecond * 8f,
            TempoSyncValue.QuarterTriplet => beatsPerSecond * (4f / 3f),
            TempoSyncValue.EighthTriplet => beatsPerSecond * (8f / 3f),
            TempoSyncValue.SixteenthTriplet => beatsPerSecond * (16f / 3f),
            TempoSyncValue.QuarterDotted => beatsPerSecond * (2f / 3f),
            TempoSyncValue.EighthDotted => beatsPerSecond * (4f / 3f),
            TempoSyncValue.SixteenthDotted => beatsPerSecond * (8f / 3f),
            _ => Rate
        };
    }

    /// <summary>
    /// Generates LFO value based on the selected waveform.
    /// </summary>
    /// <param name="phase">Phase in radians (0 to 2*PI)</param>
    /// <param name="waveform">Waveform type</param>
    /// <returns>LFO value in range -1 to 1</returns>
    private static float GenerateLfo(float phase, LfoWaveform waveform)
    {
        // Normalize phase to 0..1 range
        float normalizedPhase = phase / TwoPi;
        normalizedPhase -= MathF.Floor(normalizedPhase); // Wrap to 0..1

        return waveform switch
        {
            LfoWaveform.Sine => MathF.Sin(phase),

            LfoWaveform.Triangle =>
                normalizedPhase < 0.5f
                    ? 4f * normalizedPhase - 1f
                    : 3f - 4f * normalizedPhase,

            LfoWaveform.Square =>
                normalizedPhase < 0.5f ? 1f : -1f,

            LfoWaveform.SawUp =>
                2f * normalizedPhase - 1f,

            LfoWaveform.SawDown =>
                1f - 2f * normalizedPhase,

            _ => MathF.Sin(phase)
        };
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;
        int sampleRate = SampleRate;

        // Handle mono source - just pass through (no panning possible)
        if (channels == 1)
        {
            Array.Copy(sourceBuffer, 0, destBuffer, offset, count);
            return;
        }

        float effectiveRate = GetEffectiveRate();
        float depth = Depth;
        var waveform = Waveform;
        float phaseOffset = PhaseOffset;
        float smoothCoeff = _smoothingCoeff;

        float lfoIncrement = TwoPi * effectiveRate / sampleRate;

        for (int i = 0; i < count; i += channels)
        {
            // Advance LFO phase
            _lfoPhase += lfoIncrement;
            if (_lfoPhase > TwoPi)
            {
                _lfoPhase -= TwoPi;
            }

            // Calculate LFO values for left and right channels
            float lfoLeft = GenerateLfo(_lfoPhase, waveform);
            float lfoRight = GenerateLfo(_lfoPhase + phaseOffset * TwoPi, waveform);

            // Convert LFO (-1 to 1) to pan position (0 to 1)
            // When LFO = -1, pan = 0 (left), when LFO = 1, pan = 1 (right)
            float targetPanLeft = 0.5f + (lfoLeft * depth * 0.5f);
            float targetPanRight = 0.5f + (lfoRight * depth * 0.5f);

            // Apply smoothing to avoid clicks
            _smoothedPanLeft += smoothCoeff * (targetPanLeft - _smoothedPanLeft);
            _smoothedPanRight += smoothCoeff * (targetPanRight - _smoothedPanRight);

            // Calculate gain for each channel using constant-power panning law
            // Left channel: when pan = 0 (full left), gain = 1; when pan = 1 (full right), gain = 0
            // Right channel: when pan = 0 (full left), gain = 0; when pan = 1 (full right), gain = 1
            float leftGain = MathF.Cos(_smoothedPanLeft * MathF.PI * 0.5f);
            float rightGain = MathF.Sin(_smoothedPanRight * MathF.PI * 0.5f);

            // Read stereo input
            float inputLeft = sourceBuffer[i];
            float inputRight = sourceBuffer[i + 1];

            // Mix input to mono, then apply panning
            // This gives proper auto-pan behavior where the sound moves between speakers
            float mono = (inputLeft + inputRight) * 0.5f;

            // Apply panning gains
            destBuffer[offset + i] = mono * leftGain * MathF.Sqrt(2f);
            destBuffer[offset + i + 1] = mono * rightGain * MathF.Sqrt(2f);

            // Handle additional channels (surround) - pass through unmodified
            for (int ch = 2; ch < channels; ch++)
            {
                destBuffer[offset + i + ch] = sourceBuffer[i + ch];
            }
        }
    }
}
