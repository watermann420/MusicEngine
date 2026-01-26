//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Frequency-selective mono summing effect using Linkwitz-Riley crossover filters.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Special;

/// <summary>
/// Crossover slope options for the MonoMaker effect.
/// Higher slopes provide steeper frequency separation.
/// </summary>
public enum CrossoverSlope
{
    /// <summary>
    /// 12 dB/octave (2nd order Linkwitz-Riley)
    /// </summary>
    Slope12dB = 12,

    /// <summary>
    /// 24 dB/octave (4th order Linkwitz-Riley)
    /// </summary>
    Slope24dB = 24,

    /// <summary>
    /// 48 dB/octave (8th order Linkwitz-Riley)
    /// </summary>
    Slope48dB = 48
}

/// <summary>
/// Frequency-selective mono summing effect that makes bass frequencies mono
/// while preserving stereo width in the high frequencies.
/// Uses phase-aligned Linkwitz-Riley crossover filters to ensure clean frequency separation.
/// Commonly used in mastering to ensure mono compatibility of low frequencies.
/// </summary>
public class MonoMaker : EffectBase
{
    // Linkwitz-Riley crossover filter states
    // Each LR filter is 2 cascaded Butterworth filters
    private readonly LRCrossoverFilter _crossoverL;
    private readonly LRCrossoverFilter _crossoverR;

    // Current crossover slope
    private CrossoverSlope _crossoverSlope = CrossoverSlope.Slope24dB;

    // Cached parameter values for performance
    private float _cachedCrossoverFreq;
    private float _cachedLowMonoAmount;
    private float _cachedHighStereoWidth;

    /// <summary>
    /// Crossover frequency in Hz (20-500 Hz).
    /// Frequencies below this will be summed to mono.
    /// Default: 120 Hz
    /// </summary>
    public float CrossoverFrequency
    {
        get => GetParameter("CrossoverFrequency");
        set => SetParameter("CrossoverFrequency", Math.Clamp(value, 20f, 500f));
    }

    /// <summary>
    /// Crossover filter slope.
    /// Higher slopes provide steeper frequency separation.
    /// Default: 24 dB/octave (4th order Linkwitz-Riley)
    /// </summary>
    public CrossoverSlope CrossoverSlopeValue
    {
        get => _crossoverSlope;
        set
        {
            _crossoverSlope = value;
            UpdateFilters();
        }
    }

    /// <summary>
    /// Amount of mono summing applied to low frequencies (0-1).
    /// 0 = no mono summing (preserve original stereo)
    /// 1 = full mono (complete sum to center)
    /// Default: 1.0
    /// </summary>
    public float LowMonoAmount
    {
        get => GetParameter("LowMonoAmount");
        set => SetParameter("LowMonoAmount", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Stereo width applied to high frequencies (0-2).
    /// 0 = mono
    /// 1 = original stereo width
    /// 2 = enhanced stereo width
    /// Default: 1.0
    /// </summary>
    public float HighStereoWidth
    {
        get => GetParameter("HighStereoWidth");
        set => SetParameter("HighStereoWidth", Math.Clamp(value, 0f, 2f));
    }

    /// <summary>
    /// Creates a new MonoMaker effect.
    /// </summary>
    /// <param name="source">The stereo audio source to process</param>
    public MonoMaker(ISampleProvider source) : this(source, "Mono Maker") { }

    /// <summary>
    /// Creates a new MonoMaker effect with a custom name.
    /// </summary>
    /// <param name="source">The stereo audio source to process</param>
    /// <param name="name">The effect name</param>
    public MonoMaker(ISampleProvider source, string name) : base(source, name)
    {
        if (source.WaveFormat.Channels != 2)
            throw new ArgumentException("Source must be stereo (2 channels)", nameof(source));

        // Initialize crossover filters
        _crossoverL = new LRCrossoverFilter(SampleRate);
        _crossoverR = new LRCrossoverFilter(SampleRate);

        // Register parameters with defaults
        RegisterParameter("CrossoverFrequency", 120f);
        RegisterParameter("LowMonoAmount", 1f);
        RegisterParameter("HighStereoWidth", 1f);
        RegisterParameter("Mix", 1f);

        // Cache initial values
        _cachedCrossoverFreq = 120f;
        _cachedLowMonoAmount = 1f;
        _cachedHighStereoWidth = 1f;

        // Initialize filters
        UpdateFilters();
    }

    /// <summary>
    /// Updates the crossover filter coefficients when parameters change.
    /// </summary>
    private void UpdateFilters()
    {
        int order = _crossoverSlope switch
        {
            CrossoverSlope.Slope12dB => 1,
            CrossoverSlope.Slope24dB => 2,
            CrossoverSlope.Slope48dB => 4,
            _ => 2
        };

        _crossoverL.SetCrossover(_cachedCrossoverFreq, order);
        _crossoverR.SetCrossover(_cachedCrossoverFreq, order);
    }

    protected override void OnParameterChanged(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "crossoverfrequency":
                _cachedCrossoverFreq = value;
                UpdateFilters();
                break;
            case "lowmonoamount":
                _cachedLowMonoAmount = value;
                break;
            case "highstereowidth":
                _cachedHighStereoWidth = value;
                break;
        }
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        float lowMonoAmount = _cachedLowMonoAmount;
        float highStereoWidth = _cachedHighStereoWidth;

        for (int i = 0; i < count; i += 2)
        {
            float inputL = sourceBuffer[i];
            float inputR = sourceBuffer[i + 1];

            // Split into low and high bands using Linkwitz-Riley crossover
            _crossoverL.Process(inputL, out float lowL, out float highL);
            _crossoverR.Process(inputR, out float lowR, out float highR);

            // Process low band: apply mono summing
            float lowMono = (lowL + lowR) * 0.5f;
            float processedLowL = lowL + (lowMono - lowL) * lowMonoAmount;
            float processedLowR = lowR + (lowMono - lowR) * lowMonoAmount;

            // Process high band: apply stereo width using M/S encoding
            float highMid = (highL + highR) * 0.5f;
            float highSide = (highL - highR) * 0.5f;

            // Apply width to side signal
            highSide *= highStereoWidth;

            // Convert back to L/R
            float processedHighL = highMid + highSide;
            float processedHighR = highMid - highSide;

            // Combine low and high bands
            // Linkwitz-Riley crossovers sum flat when combined
            float outputL = processedLowL + processedHighL;
            float outputR = processedLowR + processedHighR;

            destBuffer[offset + i] = outputL;
            destBuffer[offset + i + 1] = outputR;
        }
    }

    /// <summary>
    /// Resets the filter states. Useful when seeking or starting playback.
    /// </summary>
    public void Reset()
    {
        _crossoverL.Reset();
        _crossoverR.Reset();
    }

    /// <summary>
    /// Creates a bass mono preset for typical mastering use.
    /// </summary>
    public static MonoMaker CreateBassMono(ISampleProvider source)
    {
        var effect = new MonoMaker(source);
        effect.CrossoverFrequency = 100f;
        effect.LowMonoAmount = 1f;
        effect.HighStereoWidth = 1f;
        effect.CrossoverSlopeValue = CrossoverSlope.Slope24dB;
        return effect;
    }

    /// <summary>
    /// Creates a vinyl-style preset with gentle mono summing.
    /// </summary>
    public static MonoMaker CreateVinylMono(ISampleProvider source)
    {
        var effect = new MonoMaker(source);
        effect.CrossoverFrequency = 150f;
        effect.LowMonoAmount = 0.8f;
        effect.HighStereoWidth = 1.1f;
        effect.CrossoverSlopeValue = CrossoverSlope.Slope12dB;
        return effect;
    }

    /// <summary>
    /// Creates a club-ready preset with aggressive mono summing.
    /// </summary>
    public static MonoMaker CreateClubReady(ISampleProvider source)
    {
        var effect = new MonoMaker(source);
        effect.CrossoverFrequency = 120f;
        effect.LowMonoAmount = 1f;
        effect.HighStereoWidth = 1.2f;
        effect.CrossoverSlopeValue = CrossoverSlope.Slope48dB;
        return effect;
    }

    /// <summary>
    /// Creates a subtle preset for gentle mono compatibility.
    /// </summary>
    public static MonoMaker CreateSubtle(ISampleProvider source)
    {
        var effect = new MonoMaker(source);
        effect.CrossoverFrequency = 80f;
        effect.LowMonoAmount = 0.5f;
        effect.HighStereoWidth = 1f;
        effect.CrossoverSlopeValue = CrossoverSlope.Slope24dB;
        return effect;
    }
}

/// <summary>
/// Linkwitz-Riley crossover filter implementation.
/// Provides phase-aligned low and high pass outputs that sum flat.
/// Supports 2nd, 4th, and 8th order (12, 24, 48 dB/octave slopes).
/// </summary>
internal class LRCrossoverFilter
{
    private readonly int _sampleRate;
    private readonly ButterworthBiquad[] _lowpassStages;
    private readonly ButterworthBiquad[] _highpassStages;
    private int _activeStages;

    public LRCrossoverFilter(int sampleRate)
    {
        _sampleRate = sampleRate;
        // Allocate maximum stages (8th order = 4 stages)
        _lowpassStages = new ButterworthBiquad[4];
        _highpassStages = new ButterworthBiquad[4];

        for (int i = 0; i < 4; i++)
        {
            _lowpassStages[i] = new ButterworthBiquad();
            _highpassStages[i] = new ButterworthBiquad();
        }

        _activeStages = 2; // Default to 4th order (24 dB/oct)
    }

    /// <summary>
    /// Sets the crossover frequency and order.
    /// </summary>
    /// <param name="frequency">Crossover frequency in Hz</param>
    /// <param name="order">Number of cascaded biquad stages (1=12dB, 2=24dB, 4=48dB)</param>
    public void SetCrossover(float frequency, int order)
    {
        _activeStages = Math.Clamp(order, 1, 4);

        // Pre-warp frequency for bilinear transform
        float w0 = 2f * MathF.PI * frequency / _sampleRate;
        float cosW0 = MathF.Cos(w0);
        float sinW0 = MathF.Sin(w0);

        // Q factor for Butterworth response (0.7071 for 2nd order)
        float q = 0.7071067811865476f;
        float alpha = sinW0 / (2f * q);

        // Calculate biquad coefficients for Butterworth lowpass
        float lpB0 = (1f - cosW0) / 2f;
        float lpB1 = 1f - cosW0;
        float lpB2 = (1f - cosW0) / 2f;
        float a0 = 1f + alpha;
        float a1 = -2f * cosW0;
        float a2 = 1f - alpha;

        // Normalize by a0
        lpB0 /= a0;
        lpB1 /= a0;
        lpB2 /= a0;
        a1 /= a0;
        a2 /= a0;

        // Calculate biquad coefficients for Butterworth highpass
        float hpB0 = (1f + cosW0) / 2f / a0;
        float hpB1 = -(1f + cosW0) / a0;
        float hpB2 = (1f + cosW0) / 2f / a0;

        // Set coefficients for all active stages
        for (int i = 0; i < _activeStages; i++)
        {
            _lowpassStages[i].SetCoefficients(lpB0, lpB1, lpB2, a1, a2);
            _highpassStages[i].SetCoefficients(hpB0, hpB1, hpB2, a1, a2);
        }
    }

    /// <summary>
    /// Processes a sample through the crossover filter.
    /// </summary>
    /// <param name="input">Input sample</param>
    /// <param name="lowOutput">Low frequency output (below crossover)</param>
    /// <param name="highOutput">High frequency output (above crossover)</param>
    public void Process(float input, out float lowOutput, out float highOutput)
    {
        // Process through cascaded lowpass stages
        float low = input;
        for (int i = 0; i < _activeStages; i++)
        {
            low = _lowpassStages[i].Process(low);
        }

        // Process through cascaded highpass stages
        float high = input;
        for (int i = 0; i < _activeStages; i++)
        {
            high = _highpassStages[i].Process(high);
        }

        lowOutput = low;
        highOutput = high;
    }

    /// <summary>
    /// Resets all filter states.
    /// </summary>
    public void Reset()
    {
        for (int i = 0; i < 4; i++)
        {
            _lowpassStages[i].Reset();
            _highpassStages[i].Reset();
        }
    }
}

/// <summary>
/// Butterworth biquad filter section.
/// Used as building blocks for Linkwitz-Riley crossover filters.
/// </summary>
internal class ButterworthBiquad
{
    private float _b0, _b1, _b2, _a1, _a2;
    private float _x1, _x2, _y1, _y2;

    /// <summary>
    /// Sets the biquad filter coefficients.
    /// </summary>
    public void SetCoefficients(float b0, float b1, float b2, float a1, float a2)
    {
        _b0 = b0;
        _b1 = b1;
        _b2 = b2;
        _a1 = a1;
        _a2 = a2;
    }

    /// <summary>
    /// Processes a single sample through the biquad filter.
    /// </summary>
    public float Process(float input)
    {
        // Direct Form II Transposed
        float output = _b0 * input + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;

        _x2 = _x1;
        _x1 = input;
        _y2 = _y1;
        _y1 = output;

        return output;
    }

    /// <summary>
    /// Resets the filter state.
    /// </summary>
    public void Reset()
    {
        _x1 = _x2 = _y1 = _y2 = 0f;
    }
}
