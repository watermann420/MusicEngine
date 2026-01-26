// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio restoration processor.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Restoration;

/// <summary>
/// Base frequency for hum removal (power line frequency).
/// </summary>
public enum HumFrequency
{
    /// <summary>50 Hz - European and most of world power line frequency</summary>
    Hz50 = 50,

    /// <summary>60 Hz - North American power line frequency</summary>
    Hz60 = 60
}

/// <summary>
/// Hum removal effect using cascaded biquad notch filters.
/// </summary>
/// <remarks>
/// Removes power line hum and its harmonics using adaptive notch filters.
/// Each harmonic (50/60Hz, 100/120Hz, 150/180Hz, etc.) is filtered with
/// a biquad notch filter with configurable Q factor.
///
/// Higher Q values create narrower notches that affect less of the surrounding
/// frequencies, but may be less effective at removing hum that varies slightly.
/// </remarks>
public class HumRemoval : EffectBase
{
    // Maximum harmonics supported
    private const int MaxHarmonics = 8;

    // Biquad filter state per channel per harmonic
    // Each biquad needs 2 delay elements for x (input) and y (output)
    private float[,,] _filterState; // [channel, harmonic, state (0-3)]

    // Biquad coefficients per harmonic [b0, b1, b2, a1, a2]
    private float[,] _coefficients; // [harmonic, coefficient]

    // State tracking
    private bool _coefficientsValid;
    private int _activeHarmonics;

    /// <summary>
    /// Creates a new hum removal effect.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    public HumRemoval(ISampleProvider source) : this(source, "Hum Removal")
    {
    }

    /// <summary>
    /// Creates a new hum removal effect with a custom name.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <param name="name">Effect name</param>
    public HumRemoval(ISampleProvider source, string name) : base(source, name)
    {
        int channels = source.WaveFormat.Channels;

        // Initialize filter state: [channels][harmonics][4 state values]
        _filterState = new float[channels, MaxHarmonics, 4];

        // Initialize coefficients: [harmonics][5 coefficients]
        _coefficients = new float[MaxHarmonics, 5];

        // Register parameters with defaults
        RegisterParameter("BaseFrequency", (float)HumFrequency.Hz60);  // 50 or 60 Hz
        RegisterParameter("HarmonicCount", 4f);                         // 1-8, default 4
        RegisterParameter("Q", 20f);                                    // 1-50, default 20
        RegisterParameter("Mix", 1f);

        _coefficientsValid = false;
        _activeHarmonics = 4;

        UpdateCoefficients();
    }

    /// <summary>
    /// Base frequency for hum removal (50 or 60 Hz).
    /// </summary>
    public HumFrequency BaseFrequency
    {
        get => (HumFrequency)GetParameter("BaseFrequency");
        set
        {
            int freq = (int)value;
            if (freq == 50 || freq == 60)
            {
                SetParameter("BaseFrequency", freq);
            }
        }
    }

    /// <summary>
    /// Number of harmonics to filter (1 - 8).
    /// 1 = only base frequency
    /// 4 = base + 3 harmonics (covers most hum)
    /// 8 = maximum removal
    /// </summary>
    public int HarmonicCount
    {
        get => (int)GetParameter("HarmonicCount");
        set => SetParameter("HarmonicCount", Math.Clamp(value, 1, MaxHarmonics));
    }

    /// <summary>
    /// Q factor for notch filters (1 - 50).
    /// Higher Q = narrower notch = less effect on surrounding frequencies.
    /// Lower Q = wider notch = more effective but affects more frequencies.
    /// </summary>
    public float Q
    {
        get => GetParameter("Q");
        set => SetParameter("Q", Math.Clamp(value, 1f, 50f));
    }

    /// <summary>
    /// Updates all notch filter coefficients.
    /// </summary>
    private void UpdateCoefficients()
    {
        int baseFreq = (int)GetParameter("BaseFrequency");
        _activeHarmonics = (int)GetParameter("HarmonicCount");
        float q = GetParameter("Q");
        float sampleRate = SampleRate;

        for (int h = 0; h < _activeHarmonics; h++)
        {
            float frequency = baseFreq * (h + 1);

            // Don't filter above Nyquist
            if (frequency >= sampleRate / 2f)
            {
                // Set passthrough coefficients
                _coefficients[h, 0] = 1f;  // b0
                _coefficients[h, 1] = 0f;  // b1
                _coefficients[h, 2] = 0f;  // b2
                _coefficients[h, 3] = 0f;  // a1
                _coefficients[h, 4] = 0f;  // a2
                continue;
            }

            // Calculate biquad notch filter coefficients
            // Based on Audio EQ Cookbook by Robert Bristow-Johnson
            float w0 = 2f * MathF.PI * frequency / sampleRate;
            float cosW0 = MathF.Cos(w0);
            float sinW0 = MathF.Sin(w0);
            float alpha = sinW0 / (2f * q);

            float b0 = 1f;
            float b1 = -2f * cosW0;
            float b2 = 1f;
            float a0 = 1f + alpha;
            float a1 = -2f * cosW0;
            float a2 = 1f - alpha;

            // Normalize by a0
            _coefficients[h, 0] = b0 / a0;
            _coefficients[h, 1] = b1 / a0;
            _coefficients[h, 2] = b2 / a0;
            _coefficients[h, 3] = a1 / a0;
            _coefficients[h, 4] = a2 / a0;
        }

        _coefficientsValid = true;
    }

    protected override void OnParameterChanged(string name, float value)
    {
        if (name.Equals("BaseFrequency", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("HarmonicCount", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Q", StringComparison.OrdinalIgnoreCase))
        {
            _coefficientsValid = false;
        }
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        if (!_coefficientsValid)
        {
            UpdateCoefficients();
        }

        int channels = Channels;
        int harmonics = _activeHarmonics;

        for (int i = 0; i < count; i += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                float sample = sourceBuffer[i + ch];

                // Apply cascaded notch filters for each harmonic
                for (int h = 0; h < harmonics; h++)
                {
                    sample = ProcessBiquad(sample, ch, h);
                }

                destBuffer[offset + i + ch] = sample;
            }
        }
    }

    /// <summary>
    /// Processes a sample through a single biquad notch filter.
    /// </summary>
    /// <param name="input">Input sample</param>
    /// <param name="channel">Channel index</param>
    /// <param name="harmonic">Harmonic index</param>
    /// <returns>Filtered sample</returns>
    private float ProcessBiquad(float input, int channel, int harmonic)
    {
        // Get coefficients
        float b0 = _coefficients[harmonic, 0];
        float b1 = _coefficients[harmonic, 1];
        float b2 = _coefficients[harmonic, 2];
        float a1 = _coefficients[harmonic, 3];
        float a2 = _coefficients[harmonic, 4];

        // Get state (x[n-1], x[n-2], y[n-1], y[n-2])
        float x1 = _filterState[channel, harmonic, 0];
        float x2 = _filterState[channel, harmonic, 1];
        float y1 = _filterState[channel, harmonic, 2];
        float y2 = _filterState[channel, harmonic, 3];

        // Direct Form I biquad: y[n] = b0*x[n] + b1*x[n-1] + b2*x[n-2] - a1*y[n-1] - a2*y[n-2]
        float output = b0 * input + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;

        // Update state
        _filterState[channel, harmonic, 1] = x1;  // x[n-2] = x[n-1]
        _filterState[channel, harmonic, 0] = input;  // x[n-1] = x[n]
        _filterState[channel, harmonic, 3] = y1;  // y[n-2] = y[n-1]
        _filterState[channel, harmonic, 2] = output;  // y[n-1] = y[n]

        return output;
    }

    /// <summary>
    /// Resets all filter states.
    /// Call this when seeking or starting playback from a new position.
    /// </summary>
    public void Reset()
    {
        Array.Clear(_filterState, 0, _filterState.Length);
    }

    /// <summary>
    /// Gets the frequencies being filtered.
    /// </summary>
    /// <returns>Array of frequencies in Hz that are being notched out</returns>
    public float[] GetFilteredFrequencies()
    {
        int baseFreq = (int)GetParameter("BaseFrequency");
        int harmonics = (int)GetParameter("HarmonicCount");
        float[] frequencies = new float[harmonics];

        for (int h = 0; h < harmonics; h++)
        {
            frequencies[h] = baseFreq * (h + 1);
        }

        return frequencies;
    }

    #region Presets

    /// <summary>
    /// Creates a preset for 50Hz European hum removal.
    /// </summary>
    public static HumRemoval Create50Hz(ISampleProvider source)
    {
        var effect = new HumRemoval(source, "Hum Removal - 50Hz");
        effect.BaseFrequency = HumFrequency.Hz50;
        effect.HarmonicCount = 4;
        effect.Q = 20f;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for 60Hz North American hum removal.
    /// </summary>
    public static HumRemoval Create60Hz(ISampleProvider source)
    {
        var effect = new HumRemoval(source, "Hum Removal - 60Hz");
        effect.BaseFrequency = HumFrequency.Hz60;
        effect.HarmonicCount = 4;
        effect.Q = 20f;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset with narrow notches (high Q).
    /// Affects minimal surrounding frequencies but may miss slight frequency variations.
    /// </summary>
    public static HumRemoval CreateNarrow(ISampleProvider source, HumFrequency frequency = HumFrequency.Hz60)
    {
        var effect = new HumRemoval(source, "Hum Removal - Narrow");
        effect.BaseFrequency = frequency;
        effect.HarmonicCount = 4;
        effect.Q = 40f;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset with wide notches (low Q).
    /// More effective at catching frequency variations but affects more of the audio.
    /// </summary>
    public static HumRemoval CreateWide(ISampleProvider source, HumFrequency frequency = HumFrequency.Hz60)
    {
        var effect = new HumRemoval(source, "Hum Removal - Wide");
        effect.BaseFrequency = frequency;
        effect.HarmonicCount = 4;
        effect.Q = 10f;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset with full harmonic removal.
    /// Removes 8 harmonics for complete hum elimination.
    /// </summary>
    public static HumRemoval CreateFullHarmonic(ISampleProvider source, HumFrequency frequency = HumFrequency.Hz60)
    {
        var effect = new HumRemoval(source, "Hum Removal - Full Harmonic");
        effect.BaseFrequency = frequency;
        effect.HarmonicCount = 8;
        effect.Q = 25f;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for light hum removal.
    /// Only removes base frequency and first harmonic.
    /// </summary>
    public static HumRemoval CreateLight(ISampleProvider source, HumFrequency frequency = HumFrequency.Hz60)
    {
        var effect = new HumRemoval(source, "Hum Removal - Light");
        effect.BaseFrequency = frequency;
        effect.HarmonicCount = 2;
        effect.Q = 30f;
        effect.Mix = 1f;
        return effect;
    }

    #endregion
}
