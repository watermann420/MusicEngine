//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Polyrhythmic delay effect with multiple tempo-synchronized taps supporting complex rhythmic patterns.

using NAudio.Wave;
using MusicEngine.Infrastructure.Memory;

namespace MusicEngine.Core.Effects.TimeBased;

/// <summary>
/// Rhythmic subdivision type for polyrhythmic delay taps.
/// </summary>
public enum RhythmicSubdivision
{
    /// <summary>Whole note (4 beats)</summary>
    Whole = 1,
    /// <summary>Half note (2 beats)</summary>
    Half = 2,
    /// <summary>Dotted half note (3 beats)</summary>
    DottedHalf = 3,
    /// <summary>Quarter note (1 beat)</summary>
    Quarter = 4,
    /// <summary>Dotted quarter note (1.5 beats)</summary>
    DottedQuarter = 5,
    /// <summary>Eighth note (0.5 beats)</summary>
    Eighth = 6,
    /// <summary>Dotted eighth note (0.75 beats)</summary>
    DottedEighth = 7,
    /// <summary>Eighth note triplet (1/3 beat)</summary>
    EighthTriplet = 8,
    /// <summary>Sixteenth note (0.25 beats)</summary>
    Sixteenth = 9,
    /// <summary>Dotted sixteenth note (0.375 beats)</summary>
    DottedSixteenth = 10,
    /// <summary>Sixteenth note triplet (1/6 beat)</summary>
    SixteenthTriplet = 11,
    /// <summary>Quarter note triplet (2/3 beat)</summary>
    QuarterTriplet = 12,
    /// <summary>Half note triplet (4/3 beat)</summary>
    HalfTriplet = 13,
    /// <summary>Quintuplet (1/5 beat)</summary>
    Quintuplet = 14,
    /// <summary>Septuplet (1/7 beat)</summary>
    Septuplet = 15,
    /// <summary>Thirty-second note (0.125 beats)</summary>
    ThirtySecond = 16
}

/// <summary>
/// Filter type for delay tap filtering.
/// </summary>
public enum TapFilterType
{
    /// <summary>No filtering</summary>
    Off,
    /// <summary>Low-pass filter - removes high frequencies</summary>
    LowPass,
    /// <summary>High-pass filter - removes low frequencies</summary>
    HighPass,
    /// <summary>Band-pass filter - passes frequencies around cutoff</summary>
    BandPass
}

/// <summary>
/// Configuration for a single delay tap in the polyrhythmic delay.
/// </summary>
public class DelayTapConfig
{
    /// <summary>
    /// Rhythmic subdivision for this tap.
    /// </summary>
    public RhythmicSubdivision Subdivision { get; set; } = RhythmicSubdivision.Quarter;

    /// <summary>
    /// Multiplier for the subdivision (e.g., 3 for "3 against 4" patterns).
    /// </summary>
    public int Multiplier { get; set; } = 1;

    /// <summary>
    /// Feedback amount for this tap (0.0 - 0.95).
    /// </summary>
    public float Feedback { get; set; } = 0.3f;

    /// <summary>
    /// Filter type for this tap.
    /// </summary>
    public TapFilterType FilterType { get; set; } = TapFilterType.Off;

    /// <summary>
    /// Filter cutoff frequency in Hz.
    /// </summary>
    public float FilterCutoff { get; set; } = 2000f;

    /// <summary>
    /// Filter resonance (Q factor).
    /// </summary>
    public float FilterResonance { get; set; } = 0.707f;

    /// <summary>
    /// Stereo pan position (-1.0 = left, 0.0 = center, 1.0 = right).
    /// </summary>
    public float Pan { get; set; } = 0f;

    /// <summary>
    /// Output level for this tap (0.0 - 1.0).
    /// </summary>
    public float Level { get; set; } = 1f;

    /// <summary>
    /// Whether this tap is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Polyrhythmic delay effect with multiple tempo-synchronized taps supporting complex rhythmic patterns.
/// Creates polyrhythmic delay patterns such as 3 against 4, 5 against 4, 7 against 8, etc.
/// Each tap can have independent subdivision, feedback, filtering, and panning.
/// </summary>
/// <remarks>
/// The polyrhythmic delay allows creation of complex rhythmic textures:
/// - Multiple taps (2-8) with independent subdivisions
/// - Tempo-synchronized delays for musical timing
/// - Polyrhythmic patterns (3:4, 5:4, 7:4, etc.)
/// - Per-tap filtering, panning, and feedback
/// - Global feedback for all taps
/// - Cross-feedback between taps
/// </remarks>
public class PolyrhythmicDelay : EffectBase
{
    // Maximum supported taps
    private const int MinTaps = 2;
    private const int MaxTaps = 8;

    // Maximum delay time (10 seconds at 48kHz)
    private const int MaxDelaySamples = 480000;

    // Delay tap state
    private readonly DelayTap[] _taps;
    private int _tapCount;

    // Cross-feedback buffer
    private float _crossFeedbackAccumulator;

    /// <summary>
    /// Creates a new polyrhythmic delay effect with default 4 taps.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    public PolyrhythmicDelay(ISampleProvider source) : this(source, "Polyrhythmic Delay", 4)
    {
    }

    /// <summary>
    /// Creates a new polyrhythmic delay effect with specified tap count.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <param name="tapCount">Number of delay taps (2-8)</param>
    public PolyrhythmicDelay(ISampleProvider source, int tapCount) : this(source, "Polyrhythmic Delay", tapCount)
    {
    }

    /// <summary>
    /// Creates a new polyrhythmic delay effect with a custom name and tap count.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <param name="name">Effect name</param>
    /// <param name="tapCount">Number of delay taps (2-8)</param>
    public PolyrhythmicDelay(ISampleProvider source, string name, int tapCount = 4)
        : base(source, name)
    {
        _tapCount = Math.Clamp(tapCount, MinTaps, MaxTaps);

        // Initialize all taps (always allocate max to avoid reallocations)
        _taps = new DelayTap[MaxTaps];
        for (int i = 0; i < MaxTaps; i++)
        {
            _taps[i] = new DelayTap(MaxDelaySamples);
        }

        // Set up default polyrhythmic pattern (3 against 4)
        SetDefaultPolyrhythmicPattern();

        // Register global parameters
        RegisterParameter("Tempo", 120f);           // Tempo in BPM
        RegisterParameter("TapCount", _tapCount);   // Number of active taps
        RegisterParameter("GlobalFeedback", 0.3f);  // Global feedback amount
        RegisterParameter("CrossFeedback", 0.0f);   // Cross-feedback between taps
        RegisterParameter("Damping", 0.0f);         // Global high-frequency damping
        RegisterParameter("Mix", 0.5f);             // Dry/wet mix

        // Register per-tap parameters
        for (int i = 0; i < MaxTaps; i++)
        {
            RegisterParameter($"Tap{i}_Subdivision", (float)_taps[i].Config.Subdivision);
            RegisterParameter($"Tap{i}_Multiplier", _taps[i].Config.Multiplier);
            RegisterParameter($"Tap{i}_Feedback", _taps[i].Config.Feedback);
            RegisterParameter($"Tap{i}_FilterType", (float)_taps[i].Config.FilterType);
            RegisterParameter($"Tap{i}_FilterCutoff", _taps[i].Config.FilterCutoff);
            RegisterParameter($"Tap{i}_FilterResonance", _taps[i].Config.FilterResonance);
            RegisterParameter($"Tap{i}_Pan", _taps[i].Config.Pan);
            RegisterParameter($"Tap{i}_Level", _taps[i].Config.Level);
            RegisterParameter($"Tap{i}_Enabled", _taps[i].Config.Enabled ? 1f : 0f);
        }
    }

    #region Properties

    /// <summary>
    /// Tempo in BPM for tempo-synced delays (20 - 300).
    /// </summary>
    public float Tempo
    {
        get => GetParameter("Tempo");
        set => SetParameter("Tempo", Math.Clamp(value, 20f, 300f));
    }

    /// <summary>
    /// Number of active delay taps (2-8).
    /// </summary>
    public int TapCount
    {
        get => _tapCount;
        set
        {
            _tapCount = Math.Clamp(value, MinTaps, MaxTaps);
            SetParameter("TapCount", _tapCount);
        }
    }

    /// <summary>
    /// Global feedback amount applied to all taps (0.0 - 0.95).
    /// </summary>
    public float GlobalFeedback
    {
        get => GetParameter("GlobalFeedback");
        set => SetParameter("GlobalFeedback", Math.Clamp(value, 0f, 0.95f));
    }

    /// <summary>
    /// Cross-feedback between all taps (0.0 - 0.5).
    /// Routes output of all taps back into each other.
    /// </summary>
    public float CrossFeedback
    {
        get => GetParameter("CrossFeedback");
        set => SetParameter("CrossFeedback", Math.Clamp(value, 0f, 0.5f));
    }

    /// <summary>
    /// Global high-frequency damping (0.0 - 1.0).
    /// Higher values create darker, more muffled repeats.
    /// </summary>
    public float Damping
    {
        get => GetParameter("Damping");
        set => SetParameter("Damping", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Dry/wet mix (0.0 - 1.0).
    /// </summary>
    public float DryWet
    {
        get => Mix;
        set => Mix = value;
    }

    #endregion

    #region Tap Configuration

    /// <summary>
    /// Gets the configuration for a specific tap.
    /// </summary>
    /// <param name="tapIndex">Tap index (0 to TapCount-1)</param>
    /// <returns>The tap configuration</returns>
    public DelayTapConfig GetTapConfig(int tapIndex)
    {
        if (tapIndex < 0 || tapIndex >= MaxTaps)
            throw new ArgumentOutOfRangeException(nameof(tapIndex));

        return _taps[tapIndex].Config;
    }

    /// <summary>
    /// Sets the configuration for a specific tap.
    /// </summary>
    /// <param name="tapIndex">Tap index (0 to TapCount-1)</param>
    /// <param name="config">The tap configuration</param>
    public void SetTapConfig(int tapIndex, DelayTapConfig config)
    {
        if (tapIndex < 0 || tapIndex >= MaxTaps)
            throw new ArgumentOutOfRangeException(nameof(tapIndex));

        if (config == null)
            throw new ArgumentNullException(nameof(config));

        var tap = _taps[tapIndex];
        tap.Config = config;

        // Update parameters
        SetParameter($"Tap{tapIndex}_Subdivision", (float)config.Subdivision);
        SetParameter($"Tap{tapIndex}_Multiplier", config.Multiplier);
        SetParameter($"Tap{tapIndex}_Feedback", config.Feedback);
        SetParameter($"Tap{tapIndex}_FilterType", (float)config.FilterType);
        SetParameter($"Tap{tapIndex}_FilterCutoff", config.FilterCutoff);
        SetParameter($"Tap{tapIndex}_FilterResonance", config.FilterResonance);
        SetParameter($"Tap{tapIndex}_Pan", config.Pan);
        SetParameter($"Tap{tapIndex}_Level", config.Level);
        SetParameter($"Tap{tapIndex}_Enabled", config.Enabled ? 1f : 0f);
    }

    /// <summary>
    /// Sets the rhythmic subdivision for a tap.
    /// </summary>
    public void SetTapSubdivision(int tapIndex, RhythmicSubdivision subdivision)
    {
        if (tapIndex < 0 || tapIndex >= MaxTaps) return;
        _taps[tapIndex].Config.Subdivision = subdivision;
        SetParameter($"Tap{tapIndex}_Subdivision", (float)subdivision);
    }

    /// <summary>
    /// Sets the multiplier for a tap (for polyrhythmic patterns).
    /// </summary>
    public void SetTapMultiplier(int tapIndex, int multiplier)
    {
        if (tapIndex < 0 || tapIndex >= MaxTaps) return;
        _taps[tapIndex].Config.Multiplier = Math.Clamp(multiplier, 1, 16);
        SetParameter($"Tap{tapIndex}_Multiplier", multiplier);
    }

    /// <summary>
    /// Sets the feedback for a tap.
    /// </summary>
    public void SetTapFeedback(int tapIndex, float feedback)
    {
        if (tapIndex < 0 || tapIndex >= MaxTaps) return;
        feedback = Math.Clamp(feedback, 0f, 0.95f);
        _taps[tapIndex].Config.Feedback = feedback;
        SetParameter($"Tap{tapIndex}_Feedback", feedback);
    }

    /// <summary>
    /// Sets the filter for a tap.
    /// </summary>
    public void SetTapFilter(int tapIndex, TapFilterType filterType, float cutoff, float resonance = 0.707f)
    {
        if (tapIndex < 0 || tapIndex >= MaxTaps) return;
        _taps[tapIndex].Config.FilterType = filterType;
        _taps[tapIndex].Config.FilterCutoff = Math.Clamp(cutoff, 20f, 20000f);
        _taps[tapIndex].Config.FilterResonance = Math.Clamp(resonance, 0.1f, 10f);
        SetParameter($"Tap{tapIndex}_FilterType", (float)filterType);
        SetParameter($"Tap{tapIndex}_FilterCutoff", cutoff);
        SetParameter($"Tap{tapIndex}_FilterResonance", resonance);
    }

    /// <summary>
    /// Sets the pan position for a tap.
    /// </summary>
    public void SetTapPan(int tapIndex, float pan)
    {
        if (tapIndex < 0 || tapIndex >= MaxTaps) return;
        pan = Math.Clamp(pan, -1f, 1f);
        _taps[tapIndex].Config.Pan = pan;
        SetParameter($"Tap{tapIndex}_Pan", pan);
    }

    /// <summary>
    /// Sets the level for a tap.
    /// </summary>
    public void SetTapLevel(int tapIndex, float level)
    {
        if (tapIndex < 0 || tapIndex >= MaxTaps) return;
        level = Math.Clamp(level, 0f, 1f);
        _taps[tapIndex].Config.Level = level;
        SetParameter($"Tap{tapIndex}_Level", level);
    }

    /// <summary>
    /// Enables or disables a tap.
    /// </summary>
    public void SetTapEnabled(int tapIndex, bool enabled)
    {
        if (tapIndex < 0 || tapIndex >= MaxTaps) return;
        _taps[tapIndex].Config.Enabled = enabled;
        SetParameter($"Tap{tapIndex}_Enabled", enabled ? 1f : 0f);
    }

    #endregion

    #region Polyrhythmic Patterns

    /// <summary>
    /// Sets up a default polyrhythmic pattern (3 against 4 with stereo spread).
    /// </summary>
    private void SetDefaultPolyrhythmicPattern()
    {
        // Tap 0: Quarter notes (the "4" in 3:4)
        _taps[0].Config = new DelayTapConfig
        {
            Subdivision = RhythmicSubdivision.Quarter,
            Multiplier = 1,
            Feedback = 0.35f,
            Pan = -0.5f,
            Level = 1.0f,
            Enabled = true
        };

        // Tap 1: Quarter note triplets (the "3" in 3:4)
        _taps[1].Config = new DelayTapConfig
        {
            Subdivision = RhythmicSubdivision.QuarterTriplet,
            Multiplier = 1,
            Feedback = 0.3f,
            Pan = 0.5f,
            Level = 0.9f,
            Enabled = true
        };

        // Tap 2: Eighth notes (faster pulse)
        _taps[2].Config = new DelayTapConfig
        {
            Subdivision = RhythmicSubdivision.Eighth,
            Multiplier = 1,
            Feedback = 0.25f,
            FilterType = TapFilterType.LowPass,
            FilterCutoff = 3000f,
            Pan = -0.3f,
            Level = 0.7f,
            Enabled = true
        };

        // Tap 3: Dotted eighth notes
        _taps[3].Config = new DelayTapConfig
        {
            Subdivision = RhythmicSubdivision.DottedEighth,
            Multiplier = 1,
            Feedback = 0.25f,
            FilterType = TapFilterType.LowPass,
            FilterCutoff = 2500f,
            Pan = 0.3f,
            Level = 0.7f,
            Enabled = true
        };

        // Tap 4-7: Default configuration (disabled by default)
        for (int i = 4; i < MaxTaps; i++)
        {
            _taps[i].Config = new DelayTapConfig
            {
                Subdivision = RhythmicSubdivision.Quarter,
                Multiplier = 1,
                Feedback = 0.2f,
                Pan = (i % 2 == 0) ? -0.2f : 0.2f,
                Level = 0.5f,
                Enabled = false
            };
        }
    }

    /// <summary>
    /// Sets up a 3 against 4 polyrhythmic pattern.
    /// </summary>
    public void SetPattern_3Against4()
    {
        TapCount = 2;

        SetTapConfig(0, new DelayTapConfig
        {
            Subdivision = RhythmicSubdivision.Quarter,
            Multiplier = 1,
            Feedback = 0.4f,
            Pan = -0.5f,
            Level = 1.0f,
            Enabled = true
        });

        SetTapConfig(1, new DelayTapConfig
        {
            Subdivision = RhythmicSubdivision.QuarterTriplet,
            Multiplier = 1,
            Feedback = 0.4f,
            Pan = 0.5f,
            Level = 1.0f,
            Enabled = true
        });
    }

    /// <summary>
    /// Sets up a 5 against 4 polyrhythmic pattern.
    /// </summary>
    public void SetPattern_5Against4()
    {
        TapCount = 2;

        SetTapConfig(0, new DelayTapConfig
        {
            Subdivision = RhythmicSubdivision.Quarter,
            Multiplier = 1,
            Feedback = 0.4f,
            Pan = -0.5f,
            Level = 1.0f,
            Enabled = true
        });

        SetTapConfig(1, new DelayTapConfig
        {
            Subdivision = RhythmicSubdivision.Quintuplet,
            Multiplier = 1,
            Feedback = 0.4f,
            Pan = 0.5f,
            Level = 1.0f,
            Enabled = true
        });
    }

    /// <summary>
    /// Sets up a 7 against 4 polyrhythmic pattern.
    /// </summary>
    public void SetPattern_7Against4()
    {
        TapCount = 2;

        SetTapConfig(0, new DelayTapConfig
        {
            Subdivision = RhythmicSubdivision.Quarter,
            Multiplier = 1,
            Feedback = 0.4f,
            Pan = -0.5f,
            Level = 1.0f,
            Enabled = true
        });

        SetTapConfig(1, new DelayTapConfig
        {
            Subdivision = RhythmicSubdivision.Septuplet,
            Multiplier = 1,
            Feedback = 0.4f,
            Pan = 0.5f,
            Level = 1.0f,
            Enabled = true
        });
    }

    /// <summary>
    /// Sets up a complex 4-tap polyrhythmic pattern (3:4:5:7).
    /// </summary>
    public void SetPattern_Complex()
    {
        TapCount = 4;

        SetTapConfig(0, new DelayTapConfig
        {
            Subdivision = RhythmicSubdivision.Quarter,
            Multiplier = 1,
            Feedback = 0.35f,
            Pan = -0.7f,
            Level = 1.0f,
            Enabled = true
        });

        SetTapConfig(1, new DelayTapConfig
        {
            Subdivision = RhythmicSubdivision.QuarterTriplet,
            Multiplier = 1,
            Feedback = 0.3f,
            FilterType = TapFilterType.LowPass,
            FilterCutoff = 4000f,
            Pan = -0.3f,
            Level = 0.85f,
            Enabled = true
        });

        SetTapConfig(2, new DelayTapConfig
        {
            Subdivision = RhythmicSubdivision.Quintuplet,
            Multiplier = 1,
            Feedback = 0.3f,
            FilterType = TapFilterType.LowPass,
            FilterCutoff = 3000f,
            Pan = 0.3f,
            Level = 0.75f,
            Enabled = true
        });

        SetTapConfig(3, new DelayTapConfig
        {
            Subdivision = RhythmicSubdivision.Septuplet,
            Multiplier = 1,
            Feedback = 0.25f,
            FilterType = TapFilterType.LowPass,
            FilterCutoff = 2500f,
            Pan = 0.7f,
            Level = 0.65f,
            Enabled = true
        });
    }

    /// <summary>
    /// Sets up a stereo ping-pong pattern with dotted rhythms.
    /// </summary>
    public void SetPattern_DottedPingPong()
    {
        TapCount = 4;

        SetTapConfig(0, new DelayTapConfig
        {
            Subdivision = RhythmicSubdivision.DottedEighth,
            Multiplier = 1,
            Feedback = 0.4f,
            Pan = -0.9f,
            Level = 1.0f,
            Enabled = true
        });

        SetTapConfig(1, new DelayTapConfig
        {
            Subdivision = RhythmicSubdivision.DottedEighth,
            Multiplier = 2,
            Feedback = 0.35f,
            Pan = 0.9f,
            Level = 0.8f,
            Enabled = true
        });

        SetTapConfig(2, new DelayTapConfig
        {
            Subdivision = RhythmicSubdivision.DottedEighth,
            Multiplier = 3,
            Feedback = 0.3f,
            FilterType = TapFilterType.LowPass,
            FilterCutoff = 3500f,
            Pan = -0.6f,
            Level = 0.6f,
            Enabled = true
        });

        SetTapConfig(3, new DelayTapConfig
        {
            Subdivision = RhythmicSubdivision.DottedEighth,
            Multiplier = 4,
            Feedback = 0.25f,
            FilterType = TapFilterType.LowPass,
            FilterCutoff = 2500f,
            Pan = 0.6f,
            Level = 0.4f,
            Enabled = true
        });
    }

    /// <summary>
    /// Sets up an ambient/atmospheric pattern with slow rhythms.
    /// </summary>
    public void SetPattern_Ambient()
    {
        TapCount = 4;

        SetTapConfig(0, new DelayTapConfig
        {
            Subdivision = RhythmicSubdivision.Half,
            Multiplier = 1,
            Feedback = 0.5f,
            FilterType = TapFilterType.LowPass,
            FilterCutoff = 2000f,
            Pan = -0.4f,
            Level = 1.0f,
            Enabled = true
        });

        SetTapConfig(1, new DelayTapConfig
        {
            Subdivision = RhythmicSubdivision.DottedHalf,
            Multiplier = 1,
            Feedback = 0.45f,
            FilterType = TapFilterType.LowPass,
            FilterCutoff = 1800f,
            Pan = 0.4f,
            Level = 0.9f,
            Enabled = true
        });

        SetTapConfig(2, new DelayTapConfig
        {
            Subdivision = RhythmicSubdivision.Whole,
            Multiplier = 1,
            Feedback = 0.4f,
            FilterType = TapFilterType.LowPass,
            FilterCutoff = 1500f,
            Pan = -0.6f,
            Level = 0.7f,
            Enabled = true
        });

        SetTapConfig(3, new DelayTapConfig
        {
            Subdivision = RhythmicSubdivision.HalfTriplet,
            Multiplier = 1,
            Feedback = 0.4f,
            FilterType = TapFilterType.LowPass,
            FilterCutoff = 1200f,
            Pan = 0.6f,
            Level = 0.6f,
            Enabled = true
        });

        GlobalFeedback = 0.4f;
        CrossFeedback = 0.15f;
        Damping = 0.3f;
    }

    #endregion

    #region Timing Calculation

    /// <summary>
    /// Calculates the delay time in seconds for a given subdivision and tempo.
    /// </summary>
    /// <param name="subdivision">The rhythmic subdivision</param>
    /// <param name="multiplier">The subdivision multiplier</param>
    /// <param name="tempo">The tempo in BPM</param>
    /// <returns>Delay time in seconds</returns>
    public static float CalculateDelayTime(RhythmicSubdivision subdivision, int multiplier, float tempo)
    {
        // Calculate quarter note duration in seconds
        float quarterNoteDuration = 60f / tempo;

        // Calculate subdivision duration
        float subdivisionDuration = subdivision switch
        {
            RhythmicSubdivision.Whole => quarterNoteDuration * 4f,
            RhythmicSubdivision.Half => quarterNoteDuration * 2f,
            RhythmicSubdivision.DottedHalf => quarterNoteDuration * 3f,
            RhythmicSubdivision.Quarter => quarterNoteDuration,
            RhythmicSubdivision.DottedQuarter => quarterNoteDuration * 1.5f,
            RhythmicSubdivision.Eighth => quarterNoteDuration * 0.5f,
            RhythmicSubdivision.DottedEighth => quarterNoteDuration * 0.75f,
            RhythmicSubdivision.EighthTriplet => quarterNoteDuration / 3f,
            RhythmicSubdivision.Sixteenth => quarterNoteDuration * 0.25f,
            RhythmicSubdivision.DottedSixteenth => quarterNoteDuration * 0.375f,
            RhythmicSubdivision.SixteenthTriplet => quarterNoteDuration / 6f,
            RhythmicSubdivision.QuarterTriplet => quarterNoteDuration * 2f / 3f,
            RhythmicSubdivision.HalfTriplet => quarterNoteDuration * 4f / 3f,
            RhythmicSubdivision.Quintuplet => quarterNoteDuration / 5f,
            RhythmicSubdivision.Septuplet => quarterNoteDuration / 7f,
            RhythmicSubdivision.ThirtySecond => quarterNoteDuration * 0.125f,
            _ => quarterNoteDuration
        };

        return subdivisionDuration * Math.Max(1, multiplier);
    }

    #endregion

    #region Audio Processing

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;
        int sampleRate = SampleRate;
        float tempo = Tempo;
        float globalFeedback = GlobalFeedback;
        float crossFeedback = CrossFeedback;
        float damping = Damping;
        int activeTapCount = _tapCount;

        // Calculate damping coefficient for high-frequency attenuation
        float dampingCoeff = damping > 0f ? 1f - damping * 0.5f : 1f;

        // Process each sample frame
        for (int i = 0; i < count; i += channels)
        {
            // Get input samples (mono sum for processing)
            float inputMono = 0f;
            for (int ch = 0; ch < channels; ch++)
            {
                inputMono += sourceBuffer[i + ch];
            }
            inputMono /= channels;

            // Add cross-feedback from previous sample
            float inputWithCrossFeedback = inputMono + _crossFeedbackAccumulator * crossFeedback;

            // Reset accumulators for this sample
            float outputLeft = 0f;
            float outputRight = 0f;
            float newCrossFeedback = 0f;

            // Process each active tap
            for (int t = 0; t < activeTapCount; t++)
            {
                var tap = _taps[t];
                var config = tap.Config;

                if (!config.Enabled)
                    continue;

                // Calculate delay time for this tap
                float delayTime = CalculateDelayTime(config.Subdivision, config.Multiplier, tempo);
                float delaySamples = delayTime * sampleRate;
                delaySamples = Math.Clamp(delaySamples, 1f, MaxDelaySamples - 1);

                // Read from delay buffer with interpolation
                float delayed = tap.Buffer.ReadInterpolated(delaySamples);

                // Apply tap filter
                if (config.FilterType != TapFilterType.Off)
                {
                    delayed = ApplyFilter(tap, config.FilterType, delayed, config.FilterCutoff, config.FilterResonance, sampleRate);
                }

                // Apply damping to create darker repeats
                if (damping > 0f)
                {
                    delayed = tap.DampingState + dampingCoeff * (delayed - tap.DampingState);
                    tap.DampingState = delayed;
                }

                // Calculate feedback signal
                float feedbackSignal = delayed * (config.Feedback + globalFeedback);
                feedbackSignal = Math.Clamp(feedbackSignal, -1f, 1f);

                // Accumulate for cross-feedback
                newCrossFeedback += delayed * config.Level;

                // Write to delay buffer
                tap.Buffer.Write(inputWithCrossFeedback + feedbackSignal);

                // Apply level and panning
                float outputSample = delayed * config.Level;

                if (channels == 2)
                {
                    // Constant power panning
                    float panAngle = (config.Pan + 1f) * MathF.PI / 4f;
                    float leftGain = MathF.Cos(panAngle);
                    float rightGain = MathF.Sin(panAngle);

                    outputLeft += outputSample * leftGain;
                    outputRight += outputSample * rightGain;
                }
                else
                {
                    outputLeft += outputSample;
                }
            }

            // Update cross-feedback accumulator for next sample
            _crossFeedbackAccumulator = newCrossFeedback / Math.Max(1, activeTapCount);

            // Write to output buffer
            if (channels == 2)
            {
                destBuffer[offset + i] = outputLeft;
                destBuffer[offset + i + 1] = outputRight;
            }
            else
            {
                destBuffer[offset + i] = outputLeft;
            }
        }
    }

    /// <summary>
    /// Applies a state-variable filter to a sample.
    /// </summary>
    private static float ApplyFilter(DelayTap tap, TapFilterType filterType, float sample, float cutoff, float resonance, int sampleRate)
    {
        // Chamberlin state-variable filter coefficients
        float f = 2f * MathF.Sin(MathF.PI * Math.Min(cutoff, sampleRate * 0.45f) / sampleRate);
        float q = 1f / resonance;

        // Update filter state
        tap.FilterLow += f * tap.FilterBand;
        tap.FilterHigh = sample - tap.FilterLow - q * tap.FilterBand;
        tap.FilterBand += f * tap.FilterHigh;

        return filterType switch
        {
            TapFilterType.LowPass => tap.FilterLow,
            TapFilterType.HighPass => tap.FilterHigh,
            TapFilterType.BandPass => tap.FilterBand,
            _ => sample
        };
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Clears all delay buffers.
    /// </summary>
    public void Clear()
    {
        foreach (var tap in _taps)
        {
            tap.Buffer.Clear();
            tap.FilterLow = 0f;
            tap.FilterHigh = 0f;
            tap.FilterBand = 0f;
            tap.DampingState = 0f;
        }
        _crossFeedbackAccumulator = 0f;
    }

    /// <summary>
    /// Gets information about the current polyrhythmic pattern.
    /// </summary>
    /// <returns>A string describing the active pattern</returns>
    public string GetPatternDescription()
    {
        var activeTaps = new List<string>();
        for (int i = 0; i < _tapCount; i++)
        {
            if (_taps[i].Config.Enabled)
            {
                var config = _taps[i].Config;
                string multiplierStr = config.Multiplier > 1 ? $"x{config.Multiplier}" : "";
                activeTaps.Add($"{config.Subdivision}{multiplierStr}");
            }
        }
        return string.Join(" + ", activeTaps);
    }

    #endregion

    #region Presets

    /// <summary>
    /// Creates a classic 3:4 polyrhythmic delay.
    /// </summary>
    public static PolyrhythmicDelay Create3Against4(ISampleProvider source)
    {
        var delay = new PolyrhythmicDelay(source, "3:4 Polyrhythm", 2);
        delay.SetPattern_3Against4();
        delay.GlobalFeedback = 0.3f;
        delay.Mix = 0.5f;
        return delay;
    }

    /// <summary>
    /// Creates a 5:4 polyrhythmic delay.
    /// </summary>
    public static PolyrhythmicDelay Create5Against4(ISampleProvider source)
    {
        var delay = new PolyrhythmicDelay(source, "5:4 Polyrhythm", 2);
        delay.SetPattern_5Against4();
        delay.GlobalFeedback = 0.3f;
        delay.Mix = 0.5f;
        return delay;
    }

    /// <summary>
    /// Creates a complex multi-tap polyrhythmic delay.
    /// </summary>
    public static PolyrhythmicDelay CreateComplex(ISampleProvider source)
    {
        var delay = new PolyrhythmicDelay(source, "Complex Polyrhythm", 4);
        delay.SetPattern_Complex();
        delay.GlobalFeedback = 0.25f;
        delay.CrossFeedback = 0.1f;
        delay.Damping = 0.2f;
        delay.Mix = 0.5f;
        return delay;
    }

    /// <summary>
    /// Creates an ambient polyrhythmic delay with long tails.
    /// </summary>
    public static PolyrhythmicDelay CreateAmbient(ISampleProvider source)
    {
        var delay = new PolyrhythmicDelay(source, "Ambient Polyrhythm", 4);
        delay.SetPattern_Ambient();
        delay.Mix = 0.6f;
        return delay;
    }

    /// <summary>
    /// Creates a dotted ping-pong stereo delay.
    /// </summary>
    public static PolyrhythmicDelay CreateDottedPingPong(ISampleProvider source)
    {
        var delay = new PolyrhythmicDelay(source, "Dotted Ping Pong", 4);
        delay.SetPattern_DottedPingPong();
        delay.GlobalFeedback = 0.25f;
        delay.Mix = 0.5f;
        return delay;
    }

    #endregion

    #region Internal Classes

    /// <summary>
    /// Internal state for a single delay tap.
    /// </summary>
    private class DelayTap
    {
        public DelayTapConfig Config { get; set; }
        public CircularDelayBuffer Buffer { get; }

        // Filter state (state-variable filter)
        public float FilterLow;
        public float FilterHigh;
        public float FilterBand;

        // Damping state (one-pole lowpass)
        public float DampingState;

        public DelayTap(int maxSamples)
        {
            Config = new DelayTapConfig();
            Buffer = new CircularDelayBuffer(maxSamples);
            FilterLow = 0f;
            FilterHigh = 0f;
            FilterBand = 0f;
            DampingState = 0f;
        }
    }

    /// <summary>
    /// Circular buffer with linear interpolation for delay lines.
    /// </summary>
    private class CircularDelayBuffer
    {
        private readonly float[] _buffer;
        private int _writePos;

        public CircularDelayBuffer(int size)
        {
            _buffer = new float[size];
            _writePos = 0;
        }

        public void Write(float sample)
        {
            _buffer[_writePos] = sample;
            _writePos = (_writePos + 1) % _buffer.Length;
        }

        public float ReadInterpolated(float delaySamples)
        {
            // Clamp delay to buffer size
            delaySamples = Math.Clamp(delaySamples, 0f, _buffer.Length - 1);

            // Calculate read position
            float readPos = _writePos - delaySamples;
            if (readPos < 0) readPos += _buffer.Length;

            // Linear interpolation between two samples
            int pos1 = (int)readPos;
            int pos2 = (pos1 + 1) % _buffer.Length;
            float frac = readPos - pos1;

            return _buffer[pos1] * (1f - frac) + _buffer[pos2] * frac;
        }

        public void Clear()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _writePos = 0;
        }
    }

    #endregion
}
