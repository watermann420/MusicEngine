//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Dual delay effect with two independent delay lines, beat sync, cross-feedback, and advanced features.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.TimeBased;

/// <summary>
/// Filter type for per-delay filtering.
/// </summary>
public enum DelayFilterType
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
/// Beat sync note value for tempo-synced delays.
/// </summary>
public enum BeatSyncValue
{
    /// <summary>1/32 note</summary>
    ThirtySecond,
    /// <summary>1/16 note triplet</summary>
    SixteenthTriplet,
    /// <summary>1/16 note</summary>
    Sixteenth,
    /// <summary>1/8 note triplet</summary>
    EighthTriplet,
    /// <summary>Dotted 1/16 note</summary>
    DottedSixteenth,
    /// <summary>1/8 note</summary>
    Eighth,
    /// <summary>1/4 note triplet</summary>
    QuarterTriplet,
    /// <summary>Dotted 1/8 note</summary>
    DottedEighth,
    /// <summary>1/4 note</summary>
    Quarter,
    /// <summary>1/2 note triplet</summary>
    HalfTriplet,
    /// <summary>Dotted 1/4 note</summary>
    DottedQuarter,
    /// <summary>1/2 note</summary>
    Half,
    /// <summary>Dotted 1/2 note</summary>
    DottedHalf,
    /// <summary>Whole note</summary>
    Whole
}

/// <summary>
/// Advanced dual delay effect with two independent delay lines.
/// Features include beat sync, cross-feedback, per-delay filtering,
/// saturation, pitch shift, modulation, ping-pong mode, ducking, and freeze.
/// </summary>
/// <remarks>
/// The dual delay architecture allows for complex rhythmic patterns:
/// - Delay A and Delay B can have independent time, feedback, and processing
/// - Cross-feedback routes the output of each delay into the other
/// - Ping-pong mode alternates between left and right channels
/// - Beat sync locks delay times to musical note values
/// - Freeze mode captures and infinitely loops the current delay buffer
/// - Ducking reduces delay volume when input signal is present
/// </remarks>
public class DualDelay : EffectBase
{
    // Delay line state for each delay unit
    private DelayLine _delayA;
    private DelayLine _delayB;

    // Maximum delay time (10 seconds at 48kHz)
    private const int MaxDelaySamples = 480000;

    // Tap tempo state
    private readonly List<double> _tapTimes = new();
    private DateTime _lastTapTime = DateTime.MinValue;
    private const double TapTimeoutSeconds = 2.0;
    private const int MaxTapSamples = 4;

    // LFO state for modulation
    private float _lfoPhaseA;
    private float _lfoPhaseB;

    // Freeze state
    private bool _isFrozen;
    private int _freezePositionA;
    private int _freezePositionB;

    // Ducking state
    private float _duckingEnvelope;
    private const float DuckingAttack = 0.01f;
    private const float DuckingRelease = 0.1f;

    /// <summary>
    /// Creates a new dual delay effect.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    public DualDelay(ISampleProvider source) : this(source, "Dual Delay")
    {
    }

    /// <summary>
    /// Creates a new dual delay effect with a custom name.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <param name="name">Effect name</param>
    public DualDelay(ISampleProvider source, string name) : base(source, name)
    {
        int channels = source.WaveFormat.Channels;

        // Initialize delay lines
        _delayA = new DelayLine(MaxDelaySamples, channels);
        _delayB = new DelayLine(MaxDelaySamples, channels);

        // Initialize LFO phases
        _lfoPhaseA = 0f;
        _lfoPhaseB = 0f;
        _duckingEnvelope = 0f;
        _isFrozen = false;

        // Register Delay A parameters
        RegisterParameter("DelayTimeA", 0.25f);           // 250ms default
        RegisterParameter("FeedbackA", 0.4f);             // 40% feedback
        RegisterParameter("BeatSyncA", 0f);               // Beat sync off (0) or note value (1-14)
        RegisterParameter("FilterTypeA", 0f);             // Filter type (0=off, 1=LP, 2=HP, 3=BP)
        RegisterParameter("FilterCutoffA", 1000f);        // Filter cutoff frequency
        RegisterParameter("FilterResonanceA", 0.707f);    // Filter resonance/Q
        RegisterParameter("SaturationA", 0f);             // Saturation amount (0-1)
        RegisterParameter("PitchShiftA", 0f);             // Pitch shift in semitones (-12 to +12)
        RegisterParameter("ModRateA", 0f);                // Modulation rate in Hz (0-10)
        RegisterParameter("ModDepthA", 0f);               // Modulation depth (0-1)
        RegisterParameter("PanA", 0f);                    // Stereo pan (-1 to +1)
        RegisterParameter("LevelA", 1f);                  // Output level (0-1)

        // Register Delay B parameters
        RegisterParameter("DelayTimeB", 0.375f);          // 375ms default (dotted eighth feel)
        RegisterParameter("FeedbackB", 0.3f);             // 30% feedback
        RegisterParameter("BeatSyncB", 0f);               // Beat sync off
        RegisterParameter("FilterTypeB", 0f);             // Filter type
        RegisterParameter("FilterCutoffB", 2000f);        // Filter cutoff
        RegisterParameter("FilterResonanceB", 0.707f);    // Filter resonance
        RegisterParameter("SaturationB", 0f);             // Saturation
        RegisterParameter("PitchShiftB", 0f);             // Pitch shift
        RegisterParameter("ModRateB", 0f);                // Modulation rate
        RegisterParameter("ModDepthB", 0f);               // Modulation depth
        RegisterParameter("PanB", 0f);                    // Stereo pan
        RegisterParameter("LevelB", 1f);                  // Output level

        // Global parameters
        RegisterParameter("CrossFeedback", 0f);           // Cross-feedback amount (0-0.95)
        RegisterParameter("PingPong", 0f);                // Ping-pong mode (0=off, 1=on)
        RegisterParameter("Tempo", 120f);                 // Tempo in BPM for beat sync
        RegisterParameter("Ducking", 0f);                 // Ducking amount (0-1)
        RegisterParameter("DuckingThreshold", 0.1f);      // Input level to trigger ducking
        RegisterParameter("Freeze", 0f);                  // Freeze mode (0=off, 1=on)
        RegisterParameter("SerialMode", 0f);              // 0=parallel, 1=serial (A into B)
        RegisterParameter("Mix", 0.5f);                   // Dry/wet mix
    }

    #region Delay A Properties

    /// <summary>
    /// Delay A time in seconds (0.001 - 10.0).
    /// </summary>
    public float DelayTimeA
    {
        get => GetParameter("DelayTimeA");
        set => SetParameter("DelayTimeA", Math.Clamp(value, 0.001f, 10f));
    }

    /// <summary>
    /// Delay A feedback amount (0.0 - 0.95).
    /// </summary>
    public float FeedbackA
    {
        get => GetParameter("FeedbackA");
        set => SetParameter("FeedbackA", Math.Clamp(value, 0f, 0.95f));
    }

    /// <summary>
    /// Delay A beat sync note value.
    /// Set to null to disable beat sync and use manual delay time.
    /// </summary>
    public BeatSyncValue? BeatSyncA
    {
        get
        {
            float value = GetParameter("BeatSyncA");
            return value < 0.5f ? null : (BeatSyncValue)(int)(value - 1);
        }
        set
        {
            SetParameter("BeatSyncA", value.HasValue ? (float)value.Value + 1 : 0f);
        }
    }

    /// <summary>
    /// Delay A filter type.
    /// </summary>
    public DelayFilterType FilterTypeA
    {
        get => (DelayFilterType)(int)GetParameter("FilterTypeA");
        set => SetParameter("FilterTypeA", (float)value);
    }

    /// <summary>
    /// Delay A filter cutoff frequency in Hz (20 - 20000).
    /// </summary>
    public float FilterCutoffA
    {
        get => GetParameter("FilterCutoffA");
        set => SetParameter("FilterCutoffA", Math.Clamp(value, 20f, 20000f));
    }

    /// <summary>
    /// Delay A filter resonance (0.1 - 10.0).
    /// </summary>
    public float FilterResonanceA
    {
        get => GetParameter("FilterResonanceA");
        set => SetParameter("FilterResonanceA", Math.Clamp(value, 0.1f, 10f));
    }

    /// <summary>
    /// Delay A saturation amount (0.0 - 1.0).
    /// </summary>
    public float SaturationA
    {
        get => GetParameter("SaturationA");
        set => SetParameter("SaturationA", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Delay A pitch shift in semitones (-12 to +12).
    /// </summary>
    public float PitchShiftA
    {
        get => GetParameter("PitchShiftA");
        set => SetParameter("PitchShiftA", Math.Clamp(value, -12f, 12f));
    }

    /// <summary>
    /// Delay A modulation rate in Hz (0 - 10).
    /// </summary>
    public float ModRateA
    {
        get => GetParameter("ModRateA");
        set => SetParameter("ModRateA", Math.Clamp(value, 0f, 10f));
    }

    /// <summary>
    /// Delay A modulation depth (0.0 - 1.0).
    /// </summary>
    public float ModDepthA
    {
        get => GetParameter("ModDepthA");
        set => SetParameter("ModDepthA", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Delay A stereo pan position (-1.0 to +1.0).
    /// </summary>
    public float PanA
    {
        get => GetParameter("PanA");
        set => SetParameter("PanA", Math.Clamp(value, -1f, 1f));
    }

    /// <summary>
    /// Delay A output level (0.0 - 1.0).
    /// </summary>
    public float LevelA
    {
        get => GetParameter("LevelA");
        set => SetParameter("LevelA", Math.Clamp(value, 0f, 1f));
    }

    #endregion

    #region Delay B Properties

    /// <summary>
    /// Delay B time in seconds (0.001 - 10.0).
    /// </summary>
    public float DelayTimeB
    {
        get => GetParameter("DelayTimeB");
        set => SetParameter("DelayTimeB", Math.Clamp(value, 0.001f, 10f));
    }

    /// <summary>
    /// Delay B feedback amount (0.0 - 0.95).
    /// </summary>
    public float FeedbackB
    {
        get => GetParameter("FeedbackB");
        set => SetParameter("FeedbackB", Math.Clamp(value, 0f, 0.95f));
    }

    /// <summary>
    /// Delay B beat sync note value.
    /// Set to null to disable beat sync and use manual delay time.
    /// </summary>
    public BeatSyncValue? BeatSyncB
    {
        get
        {
            float value = GetParameter("BeatSyncB");
            return value < 0.5f ? null : (BeatSyncValue)(int)(value - 1);
        }
        set
        {
            SetParameter("BeatSyncB", value.HasValue ? (float)value.Value + 1 : 0f);
        }
    }

    /// <summary>
    /// Delay B filter type.
    /// </summary>
    public DelayFilterType FilterTypeB
    {
        get => (DelayFilterType)(int)GetParameter("FilterTypeB");
        set => SetParameter("FilterTypeB", (float)value);
    }

    /// <summary>
    /// Delay B filter cutoff frequency in Hz (20 - 20000).
    /// </summary>
    public float FilterCutoffB
    {
        get => GetParameter("FilterCutoffB");
        set => SetParameter("FilterCutoffB", Math.Clamp(value, 20f, 20000f));
    }

    /// <summary>
    /// Delay B filter resonance (0.1 - 10.0).
    /// </summary>
    public float FilterResonanceB
    {
        get => GetParameter("FilterResonanceB");
        set => SetParameter("FilterResonanceB", Math.Clamp(value, 0.1f, 10f));
    }

    /// <summary>
    /// Delay B saturation amount (0.0 - 1.0).
    /// </summary>
    public float SaturationB
    {
        get => GetParameter("SaturationB");
        set => SetParameter("SaturationB", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Delay B pitch shift in semitones (-12 to +12).
    /// </summary>
    public float PitchShiftB
    {
        get => GetParameter("PitchShiftB");
        set => SetParameter("PitchShiftB", Math.Clamp(value, -12f, 12f));
    }

    /// <summary>
    /// Delay B modulation rate in Hz (0 - 10).
    /// </summary>
    public float ModRateB
    {
        get => GetParameter("ModRateB");
        set => SetParameter("ModRateB", Math.Clamp(value, 0f, 10f));
    }

    /// <summary>
    /// Delay B modulation depth (0.0 - 1.0).
    /// </summary>
    public float ModDepthB
    {
        get => GetParameter("ModDepthB");
        set => SetParameter("ModDepthB", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Delay B stereo pan position (-1.0 to +1.0).
    /// </summary>
    public float PanB
    {
        get => GetParameter("PanB");
        set => SetParameter("PanB", Math.Clamp(value, -1f, 1f));
    }

    /// <summary>
    /// Delay B output level (0.0 - 1.0).
    /// </summary>
    public float LevelB
    {
        get => GetParameter("LevelB");
        set => SetParameter("LevelB", Math.Clamp(value, 0f, 1f));
    }

    #endregion

    #region Global Properties

    /// <summary>
    /// Cross-feedback amount between Delay A and B (0.0 - 0.95).
    /// Routes output of each delay into the input of the other.
    /// </summary>
    public float CrossFeedback
    {
        get => GetParameter("CrossFeedback");
        set => SetParameter("CrossFeedback", Math.Clamp(value, 0f, 0.95f));
    }

    /// <summary>
    /// Ping-pong mode enabled.
    /// When enabled, delays alternate between left and right channels.
    /// </summary>
    public bool PingPong
    {
        get => GetParameter("PingPong") > 0.5f;
        set => SetParameter("PingPong", value ? 1f : 0f);
    }

    /// <summary>
    /// Tempo in BPM for beat-synced delays (20 - 300).
    /// </summary>
    public float Tempo
    {
        get => GetParameter("Tempo");
        set => SetParameter("Tempo", Math.Clamp(value, 20f, 300f));
    }

    /// <summary>
    /// Ducking amount (0.0 - 1.0).
    /// Reduces delay volume when input signal is present.
    /// </summary>
    public float Ducking
    {
        get => GetParameter("Ducking");
        set => SetParameter("Ducking", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Input level threshold that triggers ducking (0.0 - 1.0).
    /// </summary>
    public float DuckingThreshold
    {
        get => GetParameter("DuckingThreshold");
        set => SetParameter("DuckingThreshold", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Freeze mode enabled.
    /// When enabled, captures and infinitely loops the current delay buffer.
    /// </summary>
    public bool Freeze
    {
        get => GetParameter("Freeze") > 0.5f;
        set
        {
            bool wasFreeze = Freeze;
            SetParameter("Freeze", value ? 1f : 0f);

            if (value && !wasFreeze)
            {
                // Entering freeze mode - capture current positions
                _isFrozen = true;
                _freezePositionA = _delayA.WritePosition;
                _freezePositionB = _delayB.WritePosition;
            }
            else if (!value && wasFreeze)
            {
                _isFrozen = false;
            }
        }
    }

    /// <summary>
    /// Serial mode enabled.
    /// When false (parallel), both delays process input independently.
    /// When true (serial), Delay A feeds into Delay B.
    /// </summary>
    public bool SerialMode
    {
        get => GetParameter("SerialMode") > 0.5f;
        set => SetParameter("SerialMode", value ? 1f : 0f);
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

    #region Tap Tempo

    /// <summary>
    /// Records a tap tempo event. Call this method each time the user taps.
    /// After multiple taps, the tempo will be calculated automatically.
    /// </summary>
    public void TapTempo()
    {
        var now = DateTime.UtcNow;
        double secondsSinceLastTap = (now - _lastTapTime).TotalSeconds;

        // Reset if too much time has passed
        if (secondsSinceLastTap > TapTimeoutSeconds)
        {
            _tapTimes.Clear();
        }

        _tapTimes.Add(Environment.TickCount64 / 1000.0);
        _lastTapTime = now;

        // Keep only the last N tap times
        while (_tapTimes.Count > MaxTapSamples)
        {
            _tapTimes.RemoveAt(0);
        }

        // Calculate tempo from tap intervals if we have enough samples
        if (_tapTimes.Count >= 2)
        {
            double totalInterval = 0;
            for (int i = 1; i < _tapTimes.Count; i++)
            {
                totalInterval += _tapTimes[i] - _tapTimes[i - 1];
            }

            double avgInterval = totalInterval / (_tapTimes.Count - 1);

            // Convert interval to BPM
            if (avgInterval > 0)
            {
                float newTempo = (float)(60.0 / avgInterval);
                Tempo = Math.Clamp(newTempo, 20f, 300f);
            }
        }
    }

    /// <summary>
    /// Clears the tap tempo buffer.
    /// </summary>
    public void ClearTapTempo()
    {
        _tapTimes.Clear();
        _lastTapTime = DateTime.MinValue;
    }

    #endregion

    #region Beat Sync Helpers

    /// <summary>
    /// Converts a beat sync value to delay time in seconds based on current tempo.
    /// </summary>
    /// <param name="syncValue">The beat sync note value</param>
    /// <returns>Delay time in seconds</returns>
    public float GetDelayTimeForBeatSync(BeatSyncValue syncValue)
    {
        float tempo = Tempo;
        float quarterNoteSeconds = 60f / tempo;

        return syncValue switch
        {
            BeatSyncValue.ThirtySecond => quarterNoteSeconds / 8f,
            BeatSyncValue.SixteenthTriplet => quarterNoteSeconds / 6f,
            BeatSyncValue.Sixteenth => quarterNoteSeconds / 4f,
            BeatSyncValue.EighthTriplet => quarterNoteSeconds / 3f,
            BeatSyncValue.DottedSixteenth => quarterNoteSeconds * 3f / 8f,
            BeatSyncValue.Eighth => quarterNoteSeconds / 2f,
            BeatSyncValue.QuarterTriplet => quarterNoteSeconds * 2f / 3f,
            BeatSyncValue.DottedEighth => quarterNoteSeconds * 3f / 4f,
            BeatSyncValue.Quarter => quarterNoteSeconds,
            BeatSyncValue.HalfTriplet => quarterNoteSeconds * 4f / 3f,
            BeatSyncValue.DottedQuarter => quarterNoteSeconds * 3f / 2f,
            BeatSyncValue.Half => quarterNoteSeconds * 2f,
            BeatSyncValue.DottedHalf => quarterNoteSeconds * 3f,
            BeatSyncValue.Whole => quarterNoteSeconds * 4f,
            _ => quarterNoteSeconds
        };
    }

    /// <summary>
    /// Gets the effective delay time for Delay A, considering beat sync.
    /// </summary>
    private float GetEffectiveDelayTimeA()
    {
        var beatSync = BeatSyncA;
        return beatSync.HasValue ? GetDelayTimeForBeatSync(beatSync.Value) : DelayTimeA;
    }

    /// <summary>
    /// Gets the effective delay time for Delay B, considering beat sync.
    /// </summary>
    private float GetEffectiveDelayTimeB()
    {
        var beatSync = BeatSyncB;
        return beatSync.HasValue ? GetDelayTimeForBeatSync(beatSync.Value) : DelayTimeB;
    }

    #endregion

    #region Audio Processing

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;
        int sampleRate = SampleRate;
        bool isPingPong = PingPong;
        bool isSerial = SerialMode;
        float crossFeedback = CrossFeedback;
        float ducking = Ducking;
        float duckingThreshold = DuckingThreshold;

        // Get effective delay times
        float delayTimeA = GetEffectiveDelayTimeA();
        float delayTimeB = GetEffectiveDelayTimeB();

        // Get delay parameters
        float feedbackA = FeedbackA;
        float feedbackB = FeedbackB;
        float saturationA = SaturationA;
        float saturationB = SaturationB;
        float pitchShiftA = PitchShiftA;
        float pitchShiftB = PitchShiftB;
        float modRateA = ModRateA;
        float modDepthA = ModDepthA;
        float modRateB = ModRateB;
        float modDepthB = ModDepthB;
        float panA = PanA;
        float panB = PanB;
        float levelA = LevelA;
        float levelB = LevelB;

        // Filter parameters
        DelayFilterType filterTypeA = FilterTypeA;
        DelayFilterType filterTypeB = FilterTypeB;
        float filterCutoffA = FilterCutoffA;
        float filterCutoffB = FilterCutoffB;
        float filterResonanceA = FilterResonanceA;
        float filterResonanceB = FilterResonanceB;

        // LFO increments
        float lfoIncrementA = 2f * MathF.PI * modRateA / sampleRate;
        float lfoIncrementB = 2f * MathF.PI * modRateB / sampleRate;

        // Pitch shift ratios (for simple resampling)
        float pitchRatioA = pitchShiftA != 0 ? MathF.Pow(2f, pitchShiftA / 12f) : 1f;
        float pitchRatioB = pitchShiftB != 0 ? MathF.Pow(2f, pitchShiftB / 12f) : 1f;

        // Calculate ducking attack/release coefficients
        float duckingAttackCoeff = 1f - MathF.Exp(-1f / (sampleRate * DuckingAttack));
        float duckingReleaseCoeff = 1f - MathF.Exp(-1f / (sampleRate * DuckingRelease));

        // Process samples
        for (int i = 0; i < count; i += channels)
        {
            // Get input samples (mono sum for processing)
            float inputMono = 0f;
            for (int ch = 0; ch < channels; ch++)
            {
                inputMono += sourceBuffer[i + ch];
            }
            inputMono /= channels;

            // Calculate input level for ducking
            float inputLevel = MathF.Abs(inputMono);

            // Update ducking envelope
            if (inputLevel > duckingThreshold)
            {
                _duckingEnvelope += duckingAttackCoeff * (1f - _duckingEnvelope);
            }
            else
            {
                _duckingEnvelope += duckingReleaseCoeff * (0f - _duckingEnvelope);
            }

            // Calculate ducking gain
            float duckingGain = 1f - (ducking * _duckingEnvelope);

            // Update LFO phases
            _lfoPhaseA += lfoIncrementA;
            if (_lfoPhaseA > 2f * MathF.PI) _lfoPhaseA -= 2f * MathF.PI;

            _lfoPhaseB += lfoIncrementB;
            if (_lfoPhaseB > 2f * MathF.PI) _lfoPhaseB -= 2f * MathF.PI;

            // Calculate modulated delay times
            float modulatedDelayA = delayTimeA + (modDepthA * delayTimeA * 0.1f * MathF.Sin(_lfoPhaseA));
            float modulatedDelayB = delayTimeB + (modDepthB * delayTimeB * 0.1f * MathF.Sin(_lfoPhaseB));

            // Convert to samples with pitch shift compensation
            float delaySamplesA = modulatedDelayA * sampleRate * pitchRatioA;
            float delaySamplesB = modulatedDelayB * sampleRate * pitchRatioB;

            // Clamp delay samples
            delaySamplesA = Math.Clamp(delaySamplesA, 1f, MaxDelaySamples - 1);
            delaySamplesB = Math.Clamp(delaySamplesB, 1f, MaxDelaySamples - 1);

            // Read from delay lines
            float delayedA, delayedB;

            if (_isFrozen)
            {
                // Freeze mode - read from fixed position with looping
                delayedA = _delayA.ReadFrozen(_freezePositionA, (int)delaySamplesA);
                delayedB = _delayB.ReadFrozen(_freezePositionB, (int)delaySamplesB);
            }
            else
            {
                delayedA = _delayA.ReadInterpolated(delaySamplesA);
                delayedB = _delayB.ReadInterpolated(delaySamplesB);
            }

            // Apply filters
            delayedA = ApplyFilter(_delayA, filterTypeA, delayedA, filterCutoffA, filterResonanceA, sampleRate);
            delayedB = ApplyFilter(_delayB, filterTypeB, delayedB, filterCutoffB, filterResonanceB, sampleRate);

            // Apply saturation
            if (saturationA > 0f)
            {
                delayedA = ApplySaturation(delayedA, saturationA);
            }
            if (saturationB > 0f)
            {
                delayedB = ApplySaturation(delayedB, saturationB);
            }

            // Calculate feedback signals with cross-feedback
            float feedbackSignalA, feedbackSignalB;

            if (isSerial)
            {
                // Serial mode: A -> B
                feedbackSignalA = delayedA * feedbackA;
                feedbackSignalB = (delayedB + delayedA * levelA) * feedbackB;
            }
            else
            {
                // Parallel mode with cross-feedback
                feedbackSignalA = delayedA * feedbackA + delayedB * crossFeedback;
                feedbackSignalB = delayedB * feedbackB + delayedA * crossFeedback;
            }

            // Write to delay lines (only if not frozen)
            if (!_isFrozen)
            {
                _delayA.Write(inputMono + feedbackSignalA);
                _delayB.Write(inputMono + feedbackSignalB);
            }

            // Apply levels and ducking
            float outputA = delayedA * levelA * duckingGain;
            float outputB = delayedB * levelB * duckingGain;

            // Apply panning and combine outputs
            for (int ch = 0; ch < channels; ch++)
            {
                float channelOutput;

                if (channels == 2)
                {
                    // Stereo panning
                    float panGainA_L = ch == 0 ? MathF.Cos((panA + 1f) * MathF.PI / 4f) : MathF.Sin((panA + 1f) * MathF.PI / 4f);
                    float panGainA_R = ch == 1 ? MathF.Cos((panA + 1f) * MathF.PI / 4f) : MathF.Sin((panA + 1f) * MathF.PI / 4f);
                    float panGainB_L = ch == 0 ? MathF.Cos((panB + 1f) * MathF.PI / 4f) : MathF.Sin((panB + 1f) * MathF.PI / 4f);
                    float panGainB_R = ch == 1 ? MathF.Cos((panB + 1f) * MathF.PI / 4f) : MathF.Sin((panB + 1f) * MathF.PI / 4f);

                    if (isPingPong)
                    {
                        // Ping-pong: Delay A to left, Delay B to right
                        if (ch == 0)
                        {
                            channelOutput = outputA;
                        }
                        else
                        {
                            channelOutput = outputB;
                        }
                    }
                    else
                    {
                        // Normal stereo with panning
                        float panGainA = ch == 0 ? panGainA_L : panGainA_R;
                        float panGainB = ch == 0 ? panGainB_L : panGainB_R;
                        channelOutput = outputA * panGainA + outputB * panGainB;
                    }
                }
                else
                {
                    // Mono: sum both delays
                    channelOutput = outputA + outputB;
                }

                destBuffer[offset + i + ch] = channelOutput;
            }
        }
    }

    /// <summary>
    /// Applies the selected filter type to a sample.
    /// </summary>
    private float ApplyFilter(DelayLine delay, DelayFilterType filterType, float sample, float cutoff, float resonance, int sampleRate)
    {
        if (filterType == DelayFilterType.Off)
        {
            return sample;
        }

        // Calculate filter coefficients (Chamberlin state-variable filter)
        float f = 2f * MathF.Sin(MathF.PI * cutoff / sampleRate);
        float q = 1f / resonance;

        // Get filter state
        ref var state = ref delay.FilterState;

        // State-variable filter
        state.Low += f * state.Band;
        state.High = sample - state.Low - q * state.Band;
        state.Band += f * state.High;

        return filterType switch
        {
            DelayFilterType.LowPass => state.Low,
            DelayFilterType.HighPass => state.High,
            DelayFilterType.BandPass => state.Band,
            _ => sample
        };
    }

    /// <summary>
    /// Applies saturation/distortion to a sample.
    /// </summary>
    private static float ApplySaturation(float sample, float amount)
    {
        // Soft clipping using tanh
        float drive = 1f + amount * 5f;
        return MathF.Tanh(sample * drive) / MathF.Tanh(drive);
    }

    #endregion

    #region Presets

    /// <summary>
    /// Creates a classic stereo slapback delay preset.
    /// </summary>
    public static DualDelay CreateSlapback(ISampleProvider source)
    {
        var delay = new DualDelay(source, "Slapback Delay");
        delay.DelayTimeA = 0.08f;
        delay.DelayTimeB = 0.12f;
        delay.FeedbackA = 0.1f;
        delay.FeedbackB = 0.1f;
        delay.PanA = -0.5f;
        delay.PanB = 0.5f;
        delay.Mix = 0.4f;
        return delay;
    }

    /// <summary>
    /// Creates a ping-pong delay preset.
    /// </summary>
    public static DualDelay CreatePingPong(ISampleProvider source)
    {
        var delay = new DualDelay(source, "Ping Pong Delay");
        delay.BeatSyncA = BeatSyncValue.Eighth;
        delay.BeatSyncB = BeatSyncValue.Quarter;
        delay.FeedbackA = 0.5f;
        delay.FeedbackB = 0.5f;
        delay.PingPong = true;
        delay.FilterTypeA = DelayFilterType.LowPass;
        delay.FilterCutoffA = 3000f;
        delay.Mix = 0.5f;
        return delay;
    }

    /// <summary>
    /// Creates a dub/reggae style delay preset.
    /// </summary>
    public static DualDelay CreateDubDelay(ISampleProvider source)
    {
        var delay = new DualDelay(source, "Dub Delay");
        delay.BeatSyncA = BeatSyncValue.DottedEighth;
        delay.BeatSyncB = BeatSyncValue.Quarter;
        delay.FeedbackA = 0.6f;
        delay.FeedbackB = 0.5f;
        delay.CrossFeedback = 0.3f;
        delay.FilterTypeA = DelayFilterType.LowPass;
        delay.FilterCutoffA = 2000f;
        delay.FilterTypeB = DelayFilterType.HighPass;
        delay.FilterCutoffB = 300f;
        delay.SaturationA = 0.3f;
        delay.Mix = 0.6f;
        return delay;
    }

    /// <summary>
    /// Creates a modulated tape delay preset.
    /// </summary>
    public static DualDelay CreateTapeDelay(ISampleProvider source)
    {
        var delay = new DualDelay(source, "Tape Delay");
        delay.DelayTimeA = 0.4f;
        delay.DelayTimeB = 0.6f;
        delay.FeedbackA = 0.5f;
        delay.FeedbackB = 0.4f;
        delay.ModRateA = 0.5f;
        delay.ModDepthA = 0.3f;
        delay.ModRateB = 0.4f;
        delay.ModDepthB = 0.2f;
        delay.FilterTypeA = DelayFilterType.LowPass;
        delay.FilterCutoffA = 4000f;
        delay.SaturationA = 0.2f;
        delay.SaturationB = 0.2f;
        delay.Mix = 0.5f;
        return delay;
    }

    /// <summary>
    /// Creates a shimmer delay preset with pitch shifting.
    /// </summary>
    public static DualDelay CreateShimmerDelay(ISampleProvider source)
    {
        var delay = new DualDelay(source, "Shimmer Delay");
        delay.DelayTimeA = 0.5f;
        delay.DelayTimeB = 0.75f;
        delay.FeedbackA = 0.6f;
        delay.FeedbackB = 0.5f;
        delay.PitchShiftA = 12f;  // Octave up
        delay.PitchShiftB = 7f;   // Fifth up
        delay.CrossFeedback = 0.2f;
        delay.FilterTypeA = DelayFilterType.LowPass;
        delay.FilterCutoffA = 6000f;
        delay.Mix = 0.5f;
        return delay;
    }

    /// <summary>
    /// Creates a ducking delay preset for vocals.
    /// </summary>
    public static DualDelay CreateDuckingDelay(ISampleProvider source)
    {
        var delay = new DualDelay(source, "Ducking Delay");
        delay.BeatSyncA = BeatSyncValue.Eighth;
        delay.BeatSyncB = BeatSyncValue.Quarter;
        delay.FeedbackA = 0.4f;
        delay.FeedbackB = 0.3f;
        delay.Ducking = 0.8f;
        delay.DuckingThreshold = 0.1f;
        delay.PanA = -0.3f;
        delay.PanB = 0.3f;
        delay.Mix = 0.5f;
        return delay;
    }

    /// <summary>
    /// Creates a rhythmic delay preset.
    /// </summary>
    public static DualDelay CreateRhythmicDelay(ISampleProvider source)
    {
        var delay = new DualDelay(source, "Rhythmic Delay");
        delay.BeatSyncA = BeatSyncValue.DottedEighth;
        delay.BeatSyncB = BeatSyncValue.EighthTriplet;
        delay.FeedbackA = 0.35f;
        delay.FeedbackB = 0.35f;
        delay.CrossFeedback = 0.15f;
        delay.PingPong = true;
        delay.Mix = 0.45f;
        return delay;
    }

    /// <summary>
    /// Creates an ambient/infinite delay preset.
    /// </summary>
    public static DualDelay CreateAmbientDelay(ISampleProvider source)
    {
        var delay = new DualDelay(source, "Ambient Delay");
        delay.DelayTimeA = 1.0f;
        delay.DelayTimeB = 1.5f;
        delay.FeedbackA = 0.8f;
        delay.FeedbackB = 0.75f;
        delay.CrossFeedback = 0.4f;
        delay.ModRateA = 0.1f;
        delay.ModDepthA = 0.2f;
        delay.ModRateB = 0.15f;
        delay.ModDepthB = 0.25f;
        delay.FilterTypeA = DelayFilterType.LowPass;
        delay.FilterCutoffA = 3000f;
        delay.FilterTypeB = DelayFilterType.LowPass;
        delay.FilterCutoffB = 2500f;
        delay.Mix = 0.6f;
        return delay;
    }

    #endregion

    #region Delay Line Class

    /// <summary>
    /// Circular buffer delay line with interpolation and filter state.
    /// </summary>
    private class DelayLine
    {
        private readonly float[] _buffer;
        private int _writePos;
        private readonly int _channels;

        public FilterState FilterState;

        public int WritePosition => _writePos;

        public DelayLine(int maxSamples, int channels)
        {
            _buffer = new float[maxSamples];
            _writePos = 0;
            _channels = channels;
            FilterState = new FilterState();
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

        public float ReadFrozen(int freezePosition, int loopLength)
        {
            // Read from frozen loop
            int readPos = (freezePosition + (_writePos % loopLength)) % _buffer.Length;
            return _buffer[readPos];
        }

        public void Clear()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _writePos = 0;
            FilterState = new FilterState();
        }
    }

    /// <summary>
    /// State-variable filter state.
    /// </summary>
    private struct FilterState
    {
        public float Low;
        public float High;
        public float Band;
    }

    #endregion

    /// <summary>
    /// Clears all delay buffers.
    /// </summary>
    public void Clear()
    {
        _delayA.Clear();
        _delayB.Clear();
        _lfoPhaseA = 0f;
        _lfoPhaseB = 0f;
        _duckingEnvelope = 0f;
        _isFrozen = false;
    }
}
