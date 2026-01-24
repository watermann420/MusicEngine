//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: De-esser effect for reducing sibilance in vocals.


using NAudio.Wave;


namespace MusicEngine.Core.Effects.Dynamics;


/// <summary>
/// De-esser processing mode
/// </summary>
public enum DeEsserMode
{
    /// <summary>
    /// Wideband mode - reduces the full signal when sibilance is detected.
    /// More transparent but affects the entire frequency spectrum.
    /// </summary>
    Wideband,

    /// <summary>
    /// Split-band mode - only reduces the sibilant frequencies.
    /// More precise but can sound less natural on extreme settings.
    /// </summary>
    SplitBand
}


/// <summary>
/// De-esser effect for reducing harsh sibilant sounds ("s", "t", "ch") in vocals.
/// Uses a bandpass filter to detect sibilant frequencies and applies dynamic gain reduction.
/// </summary>
public class DeEsserEffect : EffectBase
{
    // Biquad filter state for detection (bandpass)
    private readonly BiquadFilterState[] _detectionFilterStates;

    // Biquad filter state for split-band processing (bandpass for sibilant band)
    private readonly BiquadFilterState[] _splitBandFilterStates;

    // Envelope follower state
    private readonly float[] _envelope;

    // Smoothed gain reduction
    private readonly float[] _gainSmooth;

    // Current gain reduction for metering (linear scale)
    private readonly float[] _currentGainReduction;

    // Filter coefficients (shared across channels)
    private float _b0, _b1, _b2, _a1, _a2;

    /// <summary>
    /// Creates a new de-esser effect
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <param name="name">Effect name</param>
    public DeEsserEffect(ISampleProvider source, string name = "De-Esser")
        : base(source, name)
    {
        int channels = source.WaveFormat.Channels;

        _detectionFilterStates = new BiquadFilterState[channels];
        _splitBandFilterStates = new BiquadFilterState[channels];
        _envelope = new float[channels];
        _gainSmooth = new float[channels];
        _currentGainReduction = new float[channels];

        for (int i = 0; i < channels; i++)
        {
            _detectionFilterStates[i] = new BiquadFilterState();
            _splitBandFilterStates[i] = new BiquadFilterState();
            _gainSmooth[i] = 1f;
            _currentGainReduction[i] = 1f;
        }

        // Initialize parameters with typical de-esser settings
        RegisterParameter("Frequency", 6000f);      // Center frequency for sibilance detection (Hz)
        RegisterParameter("Bandwidth", 2000f);       // Bandwidth of detection filter (Hz)
        RegisterParameter("Threshold", -20f);        // Threshold in dB
        RegisterParameter("Reduction", 6f);          // Maximum reduction amount in dB
        RegisterParameter("Attack", 0.001f);         // Attack time in seconds (1ms - fast for sibilance)
        RegisterParameter("Release", 0.05f);         // Release time in seconds (50ms)
        RegisterParameter("Mode", (float)DeEsserMode.SplitBand);
        RegisterParameter("Listen", 0f);             // Listen mode: 0 = normal, 1 = hear removed signal
        RegisterParameter("Range", 12f);             // Maximum gain reduction range in dB

        Mix = 1.0f;

        // Calculate initial filter coefficients
        UpdateFilterCoefficients();
    }

    /// <summary>
    /// Center frequency for sibilance detection in Hz (2000 - 16000)
    /// Typical sibilance range is 4000-10000 Hz, with 6000-8000 Hz being most common.
    /// </summary>
    public float Frequency
    {
        get => GetParameter("Frequency");
        set
        {
            SetParameter("Frequency", Math.Clamp(value, 2000f, 16000f));
            UpdateFilterCoefficients();
        }
    }

    /// <summary>
    /// Bandwidth of the detection filter in Hz (500 - 8000)
    /// Wider bandwidth catches more sibilant sounds but may affect other frequencies.
    /// </summary>
    public float Bandwidth
    {
        get => GetParameter("Bandwidth");
        set
        {
            SetParameter("Bandwidth", Math.Clamp(value, 500f, 8000f));
            UpdateFilterCoefficients();
        }
    }

    /// <summary>
    /// Threshold in dB (-60 to 0)
    /// De-essing activates when the detected sibilant level exceeds this threshold.
    /// </summary>
    public float Threshold
    {
        get => GetParameter("Threshold");
        set => SetParameter("Threshold", Math.Clamp(value, -60f, 0f));
    }

    /// <summary>
    /// Reduction amount in dB (0 - 24)
    /// How much the sibilant frequencies are reduced when de-essing is active.
    /// </summary>
    public float Reduction
    {
        get => GetParameter("Reduction");
        set => SetParameter("Reduction", Math.Clamp(value, 0f, 24f));
    }

    /// <summary>
    /// Attack time in seconds (0.0001 - 0.1)
    /// How fast the de-esser responds to sibilance. Should be fast (1-10ms).
    /// </summary>
    public float Attack
    {
        get => GetParameter("Attack");
        set => SetParameter("Attack", Math.Clamp(value, 0.0001f, 0.1f));
    }

    /// <summary>
    /// Release time in seconds (0.01 - 0.5)
    /// How fast the de-esser returns to normal after sibilance ends.
    /// </summary>
    public float Release
    {
        get => GetParameter("Release");
        set => SetParameter("Release", Math.Clamp(value, 0.01f, 0.5f));
    }

    /// <summary>
    /// De-esser operating mode (Wideband or SplitBand)
    /// </summary>
    public DeEsserMode Mode
    {
        get => (DeEsserMode)GetParameter("Mode");
        set => SetParameter("Mode", (float)value);
    }

    /// <summary>
    /// Listen mode - when enabled, outputs only the removed sibilant signal.
    /// Useful for tuning the frequency and threshold settings.
    /// </summary>
    public bool Listen
    {
        get => GetParameter("Listen") > 0.5f;
        set => SetParameter("Listen", value ? 1f : 0f);
    }

    /// <summary>
    /// Maximum gain reduction range in dB (0 - 24)
    /// Limits how much the signal can be reduced.
    /// </summary>
    public float Range
    {
        get => GetParameter("Range");
        set => SetParameter("Range", Math.Clamp(value, 0f, 24f));
    }

    /// <summary>
    /// Gets the current gain reduction in dB for metering.
    /// </summary>
    /// <param name="channel">Channel index (0 for left, 1 for right in stereo)</param>
    /// <returns>Gain reduction in dB (negative value when reducing)</returns>
    public float GetGainReductionDb(int channel = 0)
    {
        if (channel < 0 || channel >= _currentGainReduction.Length)
            return 0f;

        float gain = _currentGainReduction[channel];
        if (gain <= 0f || gain >= 1f)
            return gain >= 1f ? 0f : -100f;

        return 20f * MathF.Log10(gain);
    }

    /// <summary>
    /// Gets the current gain reduction as a linear value (0.0 - 1.0) for metering.
    /// </summary>
    /// <param name="channel">Channel index</param>
    /// <returns>Linear gain reduction (1.0 = no reduction, 0.0 = full reduction)</returns>
    public float GetGainReductionLinear(int channel = 0)
    {
        if (channel < 0 || channel >= _currentGainReduction.Length)
            return 1f;

        return _currentGainReduction[channel];
    }

    /// <summary>
    /// Resets the de-esser state.
    /// </summary>
    public void Reset()
    {
        for (int i = 0; i < Channels; i++)
        {
            _detectionFilterStates[i].Reset();
            _splitBandFilterStates[i].Reset();
            _envelope[i] = 0f;
            _gainSmooth[i] = 1f;
            _currentGainReduction[i] = 1f;
        }
    }

    protected override void OnParameterChanged(string name, float value)
    {
        if (name.Equals("Frequency", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Bandwidth", StringComparison.OrdinalIgnoreCase))
        {
            UpdateFilterCoefficients();
        }
    }

    private void UpdateFilterCoefficients()
    {
        float frequency = GetParameter("Frequency");
        float bandwidth = GetParameter("Bandwidth");

        // Calculate Q from bandwidth
        // Q = frequency / bandwidth
        float q = frequency / bandwidth;
        q = Math.Clamp(q, 0.5f, 10f);

        // Calculate bandpass filter coefficients (biquad)
        float w0 = 2f * MathF.PI * frequency / SampleRate;
        float cosW0 = MathF.Cos(w0);
        float sinW0 = MathF.Sin(w0);
        float alpha = sinW0 / (2f * q);

        // Bandpass filter (constant skirt gain, peak gain = Q)
        float b0 = alpha;
        float b1 = 0f;
        float b2 = -alpha;
        float a0 = 1f + alpha;
        float a1 = -2f * cosW0;
        float a2 = 1f - alpha;

        // Normalize coefficients
        _b0 = b0 / a0;
        _b1 = b1 / a0;
        _b2 = b2 / a0;
        _a1 = a1 / a0;
        _a2 = a2 / a0;
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;
        int sampleRate = SampleRate;

        float threshold = Threshold;
        float reduction = Reduction;
        float attack = Attack;
        float release = Release;
        float range = Range;
        DeEsserMode mode = Mode;
        bool listenMode = Listen;

        // Convert threshold and range to linear
        float thresholdLinear = MathF.Pow(10f, threshold / 20f);
        float maxReductionLinear = MathF.Pow(10f, -range / 20f);

        // Calculate attack and release coefficients
        float attackCoeff = MathF.Exp(-1f / (attack * sampleRate));
        float releaseCoeff = MathF.Exp(-1f / (release * sampleRate));

        // Calculate reduction ratio (how much to reduce per dB over threshold)
        float reductionRatio = reduction / 20f; // Normalized reduction amount

        for (int i = 0; i < count; i += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                int srcIndex = i + ch;
                int destIndex = offset + i + ch;

                float input = sourceBuffer[srcIndex];

                // Apply bandpass filter for sibilance detection
                ref BiquadFilterState detState = ref _detectionFilterStates[ch];
                float detected = ApplyBiquad(input, ref detState);

                // Envelope follower on the detected sibilant signal
                float detectedAbs = MathF.Abs(detected);
                float coeff = detectedAbs > _envelope[ch] ? attackCoeff : releaseCoeff;
                _envelope[ch] = detectedAbs + coeff * (_envelope[ch] - detectedAbs);

                // Calculate gain reduction based on envelope
                float gainReduction = 1f;

                if (_envelope[ch] > thresholdLinear)
                {
                    // Calculate how much over threshold (in dB)
                    float overThresholdDb = 20f * MathF.Log10(_envelope[ch] / thresholdLinear);

                    // Calculate gain reduction (in dB)
                    float reductionDb = overThresholdDb * reductionRatio;
                    reductionDb = Math.Min(reductionDb, range);

                    // Convert to linear gain
                    gainReduction = MathF.Pow(10f, -reductionDb / 20f);
                    gainReduction = Math.Max(gainReduction, maxReductionLinear);
                }

                // Smooth the gain reduction
                float smoothCoeff = gainReduction < _gainSmooth[ch] ? attackCoeff : releaseCoeff;
                _gainSmooth[ch] = gainReduction + smoothCoeff * (_gainSmooth[ch] - gainReduction);

                // Store for metering
                _currentGainReduction[ch] = _gainSmooth[ch];

                // Apply gain reduction based on mode
                float output;

                if (listenMode)
                {
                    // Listen mode: output only what's being removed
                    if (mode == DeEsserMode.SplitBand)
                    {
                        // Filter the signal and show the reduced portion
                        ref BiquadFilterState splitState = ref _splitBandFilterStates[ch];
                        float sibilantBand = ApplyBiquad(input, ref splitState);
                        output = sibilantBand * (1f - _gainSmooth[ch]);
                    }
                    else
                    {
                        // Wideband: show the full signal reduction
                        output = input * (1f - _gainSmooth[ch]);
                    }
                }
                else
                {
                    // Normal processing
                    if (mode == DeEsserMode.SplitBand)
                    {
                        // Split-band mode: only reduce the sibilant frequencies
                        ref BiquadFilterState splitState = ref _splitBandFilterStates[ch];
                        float sibilantBand = ApplyBiquad(input, ref splitState);

                        // Subtract original sibilant band and add reduced version
                        output = input - sibilantBand + (sibilantBand * _gainSmooth[ch]);
                    }
                    else
                    {
                        // Wideband mode: reduce the entire signal
                        output = input * _gainSmooth[ch];
                    }
                }

                destBuffer[destIndex] = output;
            }
        }
    }

    private float ApplyBiquad(float input, ref BiquadFilterState state)
    {
        float output = _b0 * input + _b1 * state.x1 + _b2 * state.x2
                     - _a1 * state.y1 - _a2 * state.y2;

        state.x2 = state.x1;
        state.x1 = input;
        state.y2 = state.y1;
        state.y1 = output;

        return output;
    }

    /// <summary>
    /// Creates a preset optimized for male vocals
    /// </summary>
    public static DeEsserEffect CreateMaleVocalPreset(ISampleProvider source)
    {
        var deesser = new DeEsserEffect(source, "De-Esser (Male Vocal)");
        deesser.Frequency = 5500f;
        deesser.Bandwidth = 2500f;
        deesser.Threshold = -24f;
        deesser.Reduction = 6f;
        deesser.Attack = 0.001f;
        deesser.Release = 0.04f;
        deesser.Mode = DeEsserMode.SplitBand;
        return deesser;
    }

    /// <summary>
    /// Creates a preset optimized for female vocals
    /// </summary>
    public static DeEsserEffect CreateFemaleVocalPreset(ISampleProvider source)
    {
        var deesser = new DeEsserEffect(source, "De-Esser (Female Vocal)");
        deesser.Frequency = 7000f;
        deesser.Bandwidth = 3000f;
        deesser.Threshold = -22f;
        deesser.Reduction = 8f;
        deesser.Attack = 0.001f;
        deesser.Release = 0.05f;
        deesser.Mode = DeEsserMode.SplitBand;
        return deesser;
    }

    /// <summary>
    /// Creates a gentle preset for subtle de-essing
    /// </summary>
    public static DeEsserEffect CreateGentlePreset(ISampleProvider source)
    {
        var deesser = new DeEsserEffect(source, "De-Esser (Gentle)");
        deesser.Frequency = 6000f;
        deesser.Bandwidth = 2000f;
        deesser.Threshold = -18f;
        deesser.Reduction = 4f;
        deesser.Attack = 0.002f;
        deesser.Release = 0.06f;
        deesser.Mode = DeEsserMode.SplitBand;
        return deesser;
    }

    /// <summary>
    /// Creates an aggressive preset for heavy sibilance
    /// </summary>
    public static DeEsserEffect CreateAggressivePreset(ISampleProvider source)
    {
        var deesser = new DeEsserEffect(source, "De-Esser (Aggressive)");
        deesser.Frequency = 6500f;
        deesser.Bandwidth = 4000f;
        deesser.Threshold = -30f;
        deesser.Reduction = 12f;
        deesser.Attack = 0.0005f;
        deesser.Release = 0.03f;
        deesser.Mode = DeEsserMode.Wideband;
        return deesser;
    }

    /// <summary>
    /// Internal biquad filter state
    /// </summary>
    private struct BiquadFilterState
    {
        public float x1, x2; // Input history
        public float y1, y2; // Output history

        public void Reset()
        {
            x1 = x2 = y1 = y2 = 0f;
        }
    }
}
