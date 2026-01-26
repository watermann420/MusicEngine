// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Equalizer effect.

using System;
using NAudio.Wave;


namespace MusicEngine.Core.Effects.Filters;


/// <summary>
/// Filter type for dynamic EQ bands.
/// </summary>
public enum DynamicEQFilterType
{
    /// <summary>Peak/Bell filter</summary>
    Peak,
    /// <summary>Low shelf filter</summary>
    LowShelf,
    /// <summary>High shelf filter</summary>
    HighShelf,
    /// <summary>Low pass filter</summary>
    LowPass,
    /// <summary>High pass filter</summary>
    HighPass
}


/// <summary>
/// Represents a single band in the Dynamic EQ with its own dynamics processing.
/// </summary>
public class DynamicEQBand
{
    private readonly int _channels;

    // Filter coefficients
    internal float B0, B1, B2, A1, A2;

    // Filter state per channel (Direct Form II Transposed)
    internal float[] Z1;
    internal float[] Z2;

    // Envelope follower state per channel
    internal float[] Envelope;

    // Smoothed gain per channel
    internal float[] GainSmooth;

    /// <summary>
    /// Center/cutoff frequency in Hz (20 - 20000).
    /// </summary>
    public float Frequency { get; set; } = 1000f;

    /// <summary>
    /// Static gain in dB (-24 to +24).
    /// Applied regardless of dynamics processing.
    /// </summary>
    public float Gain { get; set; } = 0f;

    /// <summary>
    /// Q factor / bandwidth (0.1 - 10.0).
    /// Higher values = narrower bandwidth.
    /// </summary>
    public float Q { get; set; } = 1.0f;

    /// <summary>
    /// Threshold in dB (-60 to 0).
    /// Level at which dynamics processing begins.
    /// </summary>
    public float Threshold { get; set; } = -20f;

    /// <summary>
    /// Compression/expansion ratio (1:1 to 20:1).
    /// Values > 1 compress above threshold, values between 0 and 1 expand.
    /// </summary>
    public float Ratio { get; set; } = 4f;

    /// <summary>
    /// Attack time in milliseconds (0.1 - 100).
    /// How fast the dynamics respond to rising levels.
    /// </summary>
    public float AttackMs { get; set; } = 10f;

    /// <summary>
    /// Release time in milliseconds (10 - 1000).
    /// How fast the dynamics return to normal after level drops.
    /// </summary>
    public float ReleaseMs { get; set; } = 100f;

    /// <summary>
    /// Maximum dynamic gain change in dB (-24 to +24).
    /// Limits the range of dynamic gain modification.
    /// </summary>
    public float Range { get; set; } = -12f;

    /// <summary>
    /// Filter type for this band.
    /// </summary>
    public DynamicEQFilterType FilterType { get; set; } = DynamicEQFilterType.Peak;

    /// <summary>
    /// Whether this band is bypassed.
    /// </summary>
    public bool Bypass { get; set; } = false;

    /// <summary>
    /// Creates a new dynamic EQ band.
    /// </summary>
    /// <param name="channels">Number of audio channels</param>
    public DynamicEQBand(int channels)
    {
        _channels = channels;
        Z1 = new float[channels];
        Z2 = new float[channels];
        Envelope = new float[channels];
        GainSmooth = new float[channels];

        // Initialize smoothed gain to unity
        for (int i = 0; i < channels; i++)
        {
            GainSmooth[i] = 1f;
        }
    }

    /// <summary>
    /// Gets the current gain reduction/expansion in dB for the specified channel.
    /// </summary>
    /// <param name="channel">Channel index</param>
    /// <returns>Gain change in dB</returns>
    public float GetCurrentGainDb(int channel = 0)
    {
        if (channel < 0 || channel >= _channels)
            return 0f;

        float gain = GainSmooth[channel];
        if (gain <= 0f)
            return -100f;

        return 20f * MathF.Log10(gain);
    }

    /// <summary>
    /// Resets the filter and envelope state.
    /// </summary>
    public void Reset()
    {
        Array.Clear(Z1);
        Array.Clear(Z2);
        Array.Clear(Envelope);

        for (int i = 0; i < _channels; i++)
        {
            GainSmooth[i] = 1f;
        }
    }
}


/// <summary>
/// Dynamic EQ effect with 6 bands, each capable of frequency-dependent compression/expansion.
/// Each band has independent frequency, gain, Q, and dynamics controls.
/// </summary>
public class DynamicEQEffect : EffectBase
{
    /// <summary>
    /// Number of bands in the dynamic EQ.
    /// </summary>
    public const int NumBands = 6;

    private readonly DynamicEQBand[] _bands;
    private bool _coefficientsNeedUpdate = true;

    /// <summary>
    /// Creates a new 6-band Dynamic EQ effect.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <param name="name">Effect name</param>
    public DynamicEQEffect(ISampleProvider source, string name = "Dynamic EQ")
        : base(source, name)
    {
        int channels = Channels;

        // Initialize bands
        _bands = new DynamicEQBand[NumBands];
        for (int i = 0; i < NumBands; i++)
        {
            _bands[i] = new DynamicEQBand(channels);
        }

        // Set default frequencies spread across the spectrum
        _bands[0].Frequency = 60f;    _bands[0].FilterType = DynamicEQFilterType.LowShelf;
        _bands[1].Frequency = 250f;   _bands[1].FilterType = DynamicEQFilterType.Peak;
        _bands[2].Frequency = 1000f;  _bands[2].FilterType = DynamicEQFilterType.Peak;
        _bands[3].Frequency = 3000f;  _bands[3].FilterType = DynamicEQFilterType.Peak;
        _bands[4].Frequency = 8000f;  _bands[4].FilterType = DynamicEQFilterType.Peak;
        _bands[5].Frequency = 12000f; _bands[5].FilterType = DynamicEQFilterType.HighShelf;

        // Set default Q values
        for (int i = 0; i < NumBands; i++)
        {
            _bands[i].Q = 1.0f;
            _bands[i].Gain = 0f;
            _bands[i].Threshold = -20f;
            _bands[i].Ratio = 4f;
            _bands[i].AttackMs = 10f;
            _bands[i].ReleaseMs = 100f;
            _bands[i].Range = -12f;
        }

        // Register parameters
        RegisterParameter("OutputGain", 0f);

        Mix = 1.0f;
    }

    #region Band Access

    /// <summary>
    /// Gets a band by index (0-5).
    /// </summary>
    /// <param name="index">Band index (0-5)</param>
    /// <returns>The dynamic EQ band</returns>
    public DynamicEQBand GetBand(int index)
    {
        return _bands[Math.Clamp(index, 0, NumBands - 1)];
    }

    /// <summary>
    /// Band 1 (default: 60 Hz Low Shelf)
    /// </summary>
    public DynamicEQBand Band1 => _bands[0];

    /// <summary>
    /// Band 2 (default: 250 Hz Peak)
    /// </summary>
    public DynamicEQBand Band2 => _bands[1];

    /// <summary>
    /// Band 3 (default: 1 kHz Peak)
    /// </summary>
    public DynamicEQBand Band3 => _bands[2];

    /// <summary>
    /// Band 4 (default: 3 kHz Peak)
    /// </summary>
    public DynamicEQBand Band4 => _bands[3];

    /// <summary>
    /// Band 5 (default: 8 kHz Peak)
    /// </summary>
    public DynamicEQBand Band5 => _bands[4];

    /// <summary>
    /// Band 6 (default: 12 kHz High Shelf)
    /// </summary>
    public DynamicEQBand Band6 => _bands[5];

    #endregion

    #region Global Parameters

    /// <summary>
    /// Global output gain in dB (-24 to +24).
    /// </summary>
    public float OutputGain
    {
        get => GetParameter("OutputGain");
        set => SetParameter("OutputGain", Math.Clamp(value, -24f, 24f));
    }

    /// <summary>
    /// Dry/wet mix (0.0 = 100% dry, 1.0 = 100% wet).
    /// </summary>
    public float DryWet
    {
        get => Mix;
        set => Mix = value;
    }

    #endregion

    #region Band Configuration Methods

    /// <summary>
    /// Configures a band with basic EQ parameters.
    /// </summary>
    /// <param name="bandIndex">Band index (0-5)</param>
    /// <param name="frequency">Center frequency in Hz</param>
    /// <param name="gain">Static gain in dB</param>
    /// <param name="q">Q factor</param>
    /// <param name="filterType">Filter type</param>
    public void ConfigureBand(int bandIndex, float frequency, float gain, float q, DynamicEQFilterType filterType)
    {
        var band = GetBand(bandIndex);
        band.Frequency = Math.Clamp(frequency, 20f, 20000f);
        band.Gain = Math.Clamp(gain, -24f, 24f);
        band.Q = Math.Clamp(q, 0.1f, 10f);
        band.FilterType = filterType;
        _coefficientsNeedUpdate = true;
    }

    /// <summary>
    /// Configures a band's dynamics parameters.
    /// </summary>
    /// <param name="bandIndex">Band index (0-5)</param>
    /// <param name="threshold">Threshold in dB</param>
    /// <param name="ratio">Compression ratio</param>
    /// <param name="attackMs">Attack time in ms</param>
    /// <param name="releaseMs">Release time in ms</param>
    /// <param name="range">Max dynamic gain change in dB</param>
    public void ConfigureBandDynamics(int bandIndex, float threshold, float ratio, float attackMs, float releaseMs, float range)
    {
        var band = GetBand(bandIndex);
        band.Threshold = Math.Clamp(threshold, -60f, 0f);
        band.Ratio = Math.Clamp(ratio, 1f, 20f);
        band.AttackMs = Math.Clamp(attackMs, 0.1f, 100f);
        band.ReleaseMs = Math.Clamp(releaseMs, 10f, 1000f);
        band.Range = Math.Clamp(range, -24f, 24f);
    }

    /// <summary>
    /// Bypasses or enables a specific band.
    /// </summary>
    /// <param name="bandIndex">Band index (0-5)</param>
    /// <param name="bypass">True to bypass, false to enable</param>
    public void SetBandBypass(int bandIndex, bool bypass)
    {
        GetBand(bandIndex).Bypass = bypass;
    }

    #endregion

    /// <summary>
    /// Resets all band states.
    /// </summary>
    public void Reset()
    {
        foreach (var band in _bands)
        {
            band.Reset();
        }
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;
        int sampleRate = SampleRate;

        // Update filter coefficients if needed
        if (_coefficientsNeedUpdate)
        {
            UpdateAllCoefficients(sampleRate);
            _coefficientsNeedUpdate = false;
        }

        float outputGainLinear = MathF.Pow(10f, OutputGain / 20f);

        for (int i = 0; i < count; i += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                int index = i + ch;
                float sample = sourceBuffer[index];
                float processed = sample;

                // Process through all bands (serial processing)
                for (int b = 0; b < NumBands; b++)
                {
                    var band = _bands[b];

                    if (band.Bypass)
                        continue;

                    // First, detect the level in the current band using a parallel filter
                    float bandLevel = DetectBandLevel(band, processed, ch, sampleRate);

                    // Calculate dynamic gain based on level
                    float dynamicGainDb = CalculateDynamicGain(band, bandLevel, ch, sampleRate);

                    // Calculate total gain (static + dynamic)
                    float totalGainDb = band.Gain + dynamicGainDb;

                    // Update filter coefficients with the new gain
                    UpdateBandCoefficientsWithGain(band, totalGainDb, sampleRate);

                    // Apply the EQ filter
                    processed = ProcessBiquad(band, processed, ch);
                }

                // Apply output gain
                destBuffer[offset + index] = processed * outputGainLinear;
            }
        }
    }

    /// <summary>
    /// Detects the level in a specific frequency band using envelope following.
    /// </summary>
    private float DetectBandLevel(DynamicEQBand band, float input, int channel, int sampleRate)
    {
        // Simple peak detection with smoothing
        float inputAbs = MathF.Abs(input);

        // Calculate attack and release coefficients
        float attackCoeff = MathF.Exp(-1f / (band.AttackMs * 0.001f * sampleRate));
        float releaseCoeff = MathF.Exp(-1f / (band.ReleaseMs * 0.001f * sampleRate));

        // Envelope follower
        float coeff = inputAbs > band.Envelope[channel] ? attackCoeff : releaseCoeff;
        band.Envelope[channel] = inputAbs + coeff * (band.Envelope[channel] - inputAbs);

        return band.Envelope[channel];
    }

    /// <summary>
    /// Calculates the dynamic gain adjustment based on the detected level.
    /// </summary>
    private float CalculateDynamicGain(DynamicEQBand band, float level, int channel, int sampleRate)
    {
        // Convert level to dB
        float levelDb = 20f * MathF.Log10(level + 1e-6f);

        // Calculate gain adjustment
        float gainAdjustmentDb = 0f;

        if (levelDb > band.Threshold)
        {
            // Above threshold - apply compression/expansion
            float overshoot = levelDb - band.Threshold;

            if (band.Ratio > 1f)
            {
                // Compression: reduce gain above threshold
                gainAdjustmentDb = -overshoot * (1f - 1f / band.Ratio);
            }
            else if (band.Ratio > 0f && band.Ratio < 1f)
            {
                // Expansion: increase gain above threshold
                gainAdjustmentDb = overshoot * (1f / band.Ratio - 1f);
            }

            // Clamp to range
            gainAdjustmentDb = Math.Clamp(gainAdjustmentDb, -MathF.Abs(band.Range), MathF.Abs(band.Range));
        }

        // Smooth the gain change
        float targetGainLinear = MathF.Pow(10f, gainAdjustmentDb / 20f);
        float attackCoeff = MathF.Exp(-1f / (band.AttackMs * 0.001f * sampleRate));
        float releaseCoeff = MathF.Exp(-1f / (band.ReleaseMs * 0.001f * sampleRate));

        float smoothCoeff = targetGainLinear < band.GainSmooth[channel] ? attackCoeff : releaseCoeff;
        band.GainSmooth[channel] = targetGainLinear + smoothCoeff * (band.GainSmooth[channel] - targetGainLinear);

        // Convert back to dB
        return 20f * MathF.Log10(band.GainSmooth[channel] + 1e-6f);
    }

    /// <summary>
    /// Updates all filter coefficients.
    /// </summary>
    private void UpdateAllCoefficients(int sampleRate)
    {
        foreach (var band in _bands)
        {
            UpdateBandCoefficientsWithGain(band, band.Gain, sampleRate);
        }
    }

    /// <summary>
    /// Updates a band's biquad filter coefficients with a specific gain value.
    /// Uses RBJ Audio EQ Cookbook formulas.
    /// </summary>
    private void UpdateBandCoefficientsWithGain(DynamicEQBand band, float gainDb, int sampleRate)
    {
        float freq = Math.Clamp(band.Frequency, 20f, sampleRate / 2f - 100f);
        float q = band.Q;
        float A = MathF.Pow(10f, gainDb / 40f); // sqrt of gain

        float w0 = 2f * MathF.PI * freq / sampleRate;
        float cosW0 = MathF.Cos(w0);
        float sinW0 = MathF.Sin(w0);
        float alpha = sinW0 / (2f * q);

        float b0, b1, b2, a0, a1, a2;

        switch (band.FilterType)
        {
            case DynamicEQFilterType.Peak:
                // Peaking EQ
                b0 = 1f + alpha * A;
                b1 = -2f * cosW0;
                b2 = 1f - alpha * A;
                a0 = 1f + alpha / A;
                a1 = -2f * cosW0;
                a2 = 1f - alpha / A;
                break;

            case DynamicEQFilterType.LowShelf:
                // Low shelf
                float sqrtA = MathF.Sqrt(A);
                float twoSqrtAAlpha = 2f * sqrtA * alpha;
                b0 = A * ((A + 1f) - (A - 1f) * cosW0 + twoSqrtAAlpha);
                b1 = 2f * A * ((A - 1f) - (A + 1f) * cosW0);
                b2 = A * ((A + 1f) - (A - 1f) * cosW0 - twoSqrtAAlpha);
                a0 = (A + 1f) + (A - 1f) * cosW0 + twoSqrtAAlpha;
                a1 = -2f * ((A - 1f) + (A + 1f) * cosW0);
                a2 = (A + 1f) + (A - 1f) * cosW0 - twoSqrtAAlpha;
                break;

            case DynamicEQFilterType.HighShelf:
                // High shelf
                sqrtA = MathF.Sqrt(A);
                twoSqrtAAlpha = 2f * sqrtA * alpha;
                b0 = A * ((A + 1f) + (A - 1f) * cosW0 + twoSqrtAAlpha);
                b1 = -2f * A * ((A - 1f) + (A + 1f) * cosW0);
                b2 = A * ((A + 1f) + (A - 1f) * cosW0 - twoSqrtAAlpha);
                a0 = (A + 1f) - (A - 1f) * cosW0 + twoSqrtAAlpha;
                a1 = 2f * ((A - 1f) - (A + 1f) * cosW0);
                a2 = (A + 1f) - (A - 1f) * cosW0 - twoSqrtAAlpha;
                break;

            case DynamicEQFilterType.LowPass:
                // Low pass filter
                b0 = (1f - cosW0) / 2f;
                b1 = 1f - cosW0;
                b2 = (1f - cosW0) / 2f;
                a0 = 1f + alpha;
                a1 = -2f * cosW0;
                a2 = 1f - alpha;
                break;

            case DynamicEQFilterType.HighPass:
                // High pass filter
                b0 = (1f + cosW0) / 2f;
                b1 = -(1f + cosW0);
                b2 = (1f + cosW0) / 2f;
                a0 = 1f + alpha;
                a1 = -2f * cosW0;
                a2 = 1f - alpha;
                break;

            default:
                // Unity gain (no filtering)
                b0 = 1f; b1 = 0f; b2 = 0f;
                a0 = 1f; a1 = 0f; a2 = 0f;
                break;
        }

        // Normalize coefficients
        band.B0 = b0 / a0;
        band.B1 = b1 / a0;
        band.B2 = b2 / a0;
        band.A1 = a1 / a0;
        band.A2 = a2 / a0;
    }

    /// <summary>
    /// Processes a sample through the biquad filter (Direct Form II Transposed).
    /// </summary>
    private float ProcessBiquad(DynamicEQBand band, float input, int channel)
    {
        float output = band.B0 * input + band.Z1[channel];
        band.Z1[channel] = band.B1 * input - band.A1 * output + band.Z2[channel];
        band.Z2[channel] = band.B2 * input - band.A2 * output;

        return output;
    }

    #region Presets

    /// <summary>
    /// Creates a de-esser preset targeting sibilance frequencies.
    /// </summary>
    public static DynamicEQEffect CreateDeEsserPreset(ISampleProvider source)
    {
        var eq = new DynamicEQEffect(source, "De-Esser");

        // Configure band 4 and 5 for sibilance control
        eq.Band4.Frequency = 6000f;
        eq.Band4.Q = 2.0f;
        eq.Band4.Gain = 0f;
        eq.Band4.Threshold = -25f;
        eq.Band4.Ratio = 6f;
        eq.Band4.AttackMs = 1f;
        eq.Band4.ReleaseMs = 50f;
        eq.Band4.Range = -12f;
        eq.Band4.FilterType = DynamicEQFilterType.Peak;

        eq.Band5.Frequency = 8000f;
        eq.Band5.Q = 2.0f;
        eq.Band5.Gain = 0f;
        eq.Band5.Threshold = -25f;
        eq.Band5.Ratio = 6f;
        eq.Band5.AttackMs = 1f;
        eq.Band5.ReleaseMs = 50f;
        eq.Band5.Range = -12f;
        eq.Band5.FilterType = DynamicEQFilterType.Peak;

        // Bypass other bands
        eq.Band1.Bypass = true;
        eq.Band2.Bypass = true;
        eq.Band3.Bypass = true;
        eq.Band6.Bypass = true;

        return eq;
    }

    /// <summary>
    /// Creates a bass control preset for taming low-end resonances.
    /// </summary>
    public static DynamicEQEffect CreateBassControlPreset(ISampleProvider source)
    {
        var eq = new DynamicEQEffect(source, "Bass Control");

        // Low shelf for sub-bass control
        eq.Band1.Frequency = 40f;
        eq.Band1.Q = 0.7f;
        eq.Band1.Gain = 0f;
        eq.Band1.Threshold = -18f;
        eq.Band1.Ratio = 4f;
        eq.Band1.AttackMs = 20f;
        eq.Band1.ReleaseMs = 200f;
        eq.Band1.Range = -12f;
        eq.Band1.FilterType = DynamicEQFilterType.LowShelf;

        // Peak for bass resonance control
        eq.Band2.Frequency = 100f;
        eq.Band2.Q = 1.5f;
        eq.Band2.Gain = 0f;
        eq.Band2.Threshold = -15f;
        eq.Band2.Ratio = 3f;
        eq.Band2.AttackMs = 15f;
        eq.Band2.ReleaseMs = 150f;
        eq.Band2.Range = -10f;
        eq.Band2.FilterType = DynamicEQFilterType.Peak;

        // Bypass upper bands
        eq.Band3.Bypass = true;
        eq.Band4.Bypass = true;
        eq.Band5.Bypass = true;
        eq.Band6.Bypass = true;

        return eq;
    }

    /// <summary>
    /// Creates a vocal presence preset for dynamic presence control.
    /// </summary>
    public static DynamicEQEffect CreateVocalPresencePreset(ISampleProvider source)
    {
        var eq = new DynamicEQEffect(source, "Vocal Presence");

        // Mud reduction
        eq.Band2.Frequency = 300f;
        eq.Band2.Q = 1.2f;
        eq.Band2.Gain = -2f;
        eq.Band2.Threshold = -20f;
        eq.Band2.Ratio = 3f;
        eq.Band2.AttackMs = 10f;
        eq.Band2.ReleaseMs = 100f;
        eq.Band2.Range = -6f;
        eq.Band2.FilterType = DynamicEQFilterType.Peak;

        // Presence boost
        eq.Band4.Frequency = 3500f;
        eq.Band4.Q = 1.0f;
        eq.Band4.Gain = 2f;
        eq.Band4.Threshold = -25f;
        eq.Band4.Ratio = 2f;
        eq.Band4.AttackMs = 5f;
        eq.Band4.ReleaseMs = 80f;
        eq.Band4.Range = 6f;
        eq.Band4.FilterType = DynamicEQFilterType.Peak;

        // Air
        eq.Band6.Frequency = 12000f;
        eq.Band6.Q = 0.7f;
        eq.Band6.Gain = 1f;
        eq.Band6.Threshold = -30f;
        eq.Band6.Ratio = 2f;
        eq.Band6.AttackMs = 5f;
        eq.Band6.ReleaseMs = 60f;
        eq.Band6.Range = 4f;
        eq.Band6.FilterType = DynamicEQFilterType.HighShelf;

        // Bypass unused bands
        eq.Band1.Bypass = true;
        eq.Band3.Bypass = true;
        eq.Band5.Bypass = true;

        return eq;
    }

    /// <summary>
    /// Creates a mastering preset for full-spectrum dynamic EQ.
    /// </summary>
    public static DynamicEQEffect CreateMasteringPreset(ISampleProvider source)
    {
        var eq = new DynamicEQEffect(source, "Mastering Dynamic EQ");

        // Sub-bass taming
        eq.Band1.Frequency = 50f;
        eq.Band1.Q = 0.7f;
        eq.Band1.Gain = 0f;
        eq.Band1.Threshold = -12f;
        eq.Band1.Ratio = 2f;
        eq.Band1.AttackMs = 30f;
        eq.Band1.ReleaseMs = 300f;
        eq.Band1.Range = -6f;
        eq.Band1.FilterType = DynamicEQFilterType.LowShelf;

        // Low-mid cleanup
        eq.Band2.Frequency = 250f;
        eq.Band2.Q = 1.0f;
        eq.Band2.Gain = 0f;
        eq.Band2.Threshold = -18f;
        eq.Band2.Ratio = 2f;
        eq.Band2.AttackMs = 20f;
        eq.Band2.ReleaseMs = 150f;
        eq.Band2.Range = -4f;
        eq.Band2.FilterType = DynamicEQFilterType.Peak;

        // Mid presence
        eq.Band3.Frequency = 1000f;
        eq.Band3.Q = 1.0f;
        eq.Band3.Gain = 0f;
        eq.Band3.Threshold = -20f;
        eq.Band3.Ratio = 1.5f;
        eq.Band3.AttackMs = 15f;
        eq.Band3.ReleaseMs = 120f;
        eq.Band3.Range = -3f;
        eq.Band3.FilterType = DynamicEQFilterType.Peak;

        // Presence control
        eq.Band4.Frequency = 3000f;
        eq.Band4.Q = 1.0f;
        eq.Band4.Gain = 0f;
        eq.Band4.Threshold = -22f;
        eq.Band4.Ratio = 2f;
        eq.Band4.AttackMs = 10f;
        eq.Band4.ReleaseMs = 100f;
        eq.Band4.Range = -4f;
        eq.Band4.FilterType = DynamicEQFilterType.Peak;

        // Sibilance control
        eq.Band5.Frequency = 7000f;
        eq.Band5.Q = 1.5f;
        eq.Band5.Gain = 0f;
        eq.Band5.Threshold = -24f;
        eq.Band5.Ratio = 3f;
        eq.Band5.AttackMs = 5f;
        eq.Band5.ReleaseMs = 80f;
        eq.Band5.Range = -6f;
        eq.Band5.FilterType = DynamicEQFilterType.Peak;

        // Air
        eq.Band6.Frequency = 12000f;
        eq.Band6.Q = 0.7f;
        eq.Band6.Gain = 1f;
        eq.Band6.Threshold = -20f;
        eq.Band6.Ratio = 1.5f;
        eq.Band6.AttackMs = 5f;
        eq.Band6.ReleaseMs = 60f;
        eq.Band6.Range = 3f;
        eq.Band6.FilterType = DynamicEQFilterType.HighShelf;

        return eq;
    }

    #endregion
}
