using NAudio.Wave;
using MusicEngine.Core.Dsp;

namespace MusicEngine.Core.Effects.Filters;

/// <summary>
/// 3-band parametric equalizer effect.
/// Each band has independent frequency, gain, and Q (bandwidth) controls.
/// </summary>
/// <remarks>
/// <para>
/// Uses SIMD-optimized biquad filter processing via <see cref="SimdDsp"/> when available.
/// Call <see cref="SimdDsp.GetOptimizationLevel"/> to check the current optimization level.
/// </para>
/// <para>
/// Filter coefficients are calculated using the Robert Bristow-Johnson Audio EQ Cookbook formulas.
/// </para>
/// </remarks>
public class ParametricEQEffect : EffectBase
{
    // Filter states using SIMD-compatible structures
    private BiquadState[] _lowStates;     // Low band (per channel)
    private BiquadState[] _midStates;     // Mid band (per channel)
    private BiquadState[] _highStates;    // High band (per channel)

    // Cached coefficients (one set per band, shared across channels)
    private BiquadCoeffs _lowCoeffs;
    private BiquadCoeffs _midCoeffs;
    private BiquadCoeffs _highCoeffs;

    // Track if coefficients need update
    private bool _coefficientsNeedUpdate = true;
    private float _lastLowFreq, _lastLowGain, _lastLowQ;
    private float _lastMidFreq, _lastMidGain, _lastMidQ;
    private float _lastHighFreq, _lastHighGain, _lastHighQ;

    /// <summary>
    /// Creates a new 3-band parametric EQ effect
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <param name="name">Effect name</param>
    public ParametricEQEffect(ISampleProvider source, string name)
        : base(source, name)
    {
        int channels = source.WaveFormat.Channels;

        // Initialize filter states for each channel
        _lowStates = new BiquadState[channels];
        _midStates = new BiquadState[channels];
        _highStates = new BiquadState[channels];

        for (int i = 0; i < channels; i++)
        {
            _lowStates[i] = BiquadState.Create();
            _midStates[i] = BiquadState.Create();
            _highStates[i] = BiquadState.Create();
        }

        // Initialize coefficients to bypass
        _lowCoeffs = BiquadCoeffs.Bypass;
        _midCoeffs = BiquadCoeffs.Bypass;
        _highCoeffs = BiquadCoeffs.Bypass;

        // Initialize parameters
        // Low band (bass)
        RegisterParameter("LowFreq", 100f);      // 100 Hz
        RegisterParameter("LowGain", 0f);        // 0 dB (no boost/cut)
        RegisterParameter("LowQ", 0.707f);       // Bandwidth

        // Mid band
        RegisterParameter("MidFreq", 1000f);     // 1 kHz
        RegisterParameter("MidGain", 0f);        // 0 dB
        RegisterParameter("MidQ", 0.707f);

        // High band (treble)
        RegisterParameter("HighFreq", 10000f);   // 10 kHz
        RegisterParameter("HighGain", 0f);       // 0 dB
        RegisterParameter("HighQ", 0.707f);

        Mix = 1.0f;       // 100% wet
    }

    #region Low Band Properties

    /// <summary>
    /// Low band center frequency in Hz (20 - 500)
    /// </summary>
    public float LowFrequency
    {
        get => GetParameter("LowFreq");
        set => SetParameter("LowFreq", Math.Clamp(value, 20f, 500f));
    }

    /// <summary>
    /// Low band gain in dB (-24 to +24)
    /// </summary>
    public float LowGain
    {
        get => GetParameter("LowGain");
        set => SetParameter("LowGain", Math.Clamp(value, -24f, 24f));
    }

    /// <summary>
    /// Low band Q (bandwidth) (0.1 - 10.0)
    /// </summary>
    public float LowQ
    {
        get => GetParameter("LowQ");
        set => SetParameter("LowQ", Math.Clamp(value, 0.1f, 10f));
    }

    #endregion

    #region Mid Band Properties

    /// <summary>
    /// Mid band center frequency in Hz (200 - 5000)
    /// </summary>
    public float MidFrequency
    {
        get => GetParameter("MidFreq");
        set => SetParameter("MidFreq", Math.Clamp(value, 200f, 5000f));
    }

    /// <summary>
    /// Mid band gain in dB (-24 to +24)
    /// </summary>
    public float MidGain
    {
        get => GetParameter("MidGain");
        set => SetParameter("MidGain", Math.Clamp(value, -24f, 24f));
    }

    /// <summary>
    /// Mid band Q (bandwidth) (0.1 - 10.0)
    /// </summary>
    public float MidQ
    {
        get => GetParameter("MidQ");
        set => SetParameter("MidQ", Math.Clamp(value, 0.1f, 10f));
    }

    #endregion

    #region High Band Properties

    /// <summary>
    /// High band center frequency in Hz (2000 - 20000)
    /// </summary>
    public float HighFrequency
    {
        get => GetParameter("HighFreq");
        set => SetParameter("HighFreq", Math.Clamp(value, 2000f, 20000f));
    }

    /// <summary>
    /// High band gain in dB (-24 to +24)
    /// </summary>
    public float HighGain
    {
        get => GetParameter("HighGain");
        set => SetParameter("HighGain", Math.Clamp(value, -24f, 24f));
    }

    /// <summary>
    /// High band Q (bandwidth) (0.1 - 10.0)
    /// </summary>
    public float HighQ
    {
        get => GetParameter("HighQ");
        set => SetParameter("HighQ", Math.Clamp(value, 0.1f, 10f));
    }

    #endregion

    /// <summary>
    /// Dry/wet mix (0.0 = 100% dry, 1.0 = 100% wet)
    /// </summary>
    public float DryWet
    {
        get => Mix;
        set => Mix = value;
    }

    /// <summary>
    /// Called when a parameter changes to mark coefficients for update.
    /// </summary>
    protected override void OnParameterChanged(string name, float value)
    {
        base.OnParameterChanged(name, value);
        _coefficientsNeedUpdate = true;
    }

    /// <summary>
    /// Resets all filter states (clears delay lines).
    /// Call this when starting a new audio stream or after a discontinuity.
    /// </summary>
    public void ResetFilterStates()
    {
        for (int i = 0; i < _lowStates.Length; i++)
        {
            _lowStates[i].Reset();
            _midStates[i].Reset();
            _highStates[i].Reset();
        }
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;
        int sampleRate = SampleRate;

        // Update filter coefficients only if parameters changed
        UpdateCoefficientsIfNeeded(sampleRate);

        // Process each sample through the three EQ bands
        for (int i = 0; i < count; i += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                int index = i + ch;
                float input = sourceBuffer[index];

                // Process through all three bands using SIMD-compatible biquad processing
                float output = input;
                output = ProcessBiquad(ref _lowStates[ch], _lowCoeffs, output);
                output = ProcessBiquad(ref _midStates[ch], _midCoeffs, output);
                output = ProcessBiquad(ref _highStates[ch], _highCoeffs, output);

                destBuffer[offset + index] = output;
            }
        }
    }

    /// <summary>
    /// Updates filter coefficients if any parameters have changed.
    /// Uses caching to avoid unnecessary recalculation.
    /// </summary>
    private void UpdateCoefficientsIfNeeded(int sampleRate)
    {
        float lowFreq = LowFrequency;
        float lowGain = LowGain;
        float lowQ = LowQ;
        float midFreq = MidFrequency;
        float midGain = MidGain;
        float midQ = MidQ;
        float highFreq = HighFrequency;
        float highGain = HighGain;
        float highQ = HighQ;

        // Check if any parameters changed
        bool needsUpdate = _coefficientsNeedUpdate ||
            lowFreq != _lastLowFreq || lowGain != _lastLowGain || lowQ != _lastLowQ ||
            midFreq != _lastMidFreq || midGain != _lastMidGain || midQ != _lastMidQ ||
            highFreq != _lastHighFreq || highGain != _lastHighGain || highQ != _lastHighQ;

        if (!needsUpdate) return;

        // Update coefficients using BiquadCoeffs static methods
        _lowCoeffs = BiquadCoeffs.PeakingEQ(sampleRate, lowFreq, lowQ, lowGain);
        _midCoeffs = BiquadCoeffs.PeakingEQ(sampleRate, midFreq, midQ, midGain);
        _highCoeffs = BiquadCoeffs.PeakingEQ(sampleRate, highFreq, highQ, highGain);

        // Cache current values
        _lastLowFreq = lowFreq;
        _lastLowGain = lowGain;
        _lastLowQ = lowQ;
        _lastMidFreq = midFreq;
        _lastMidGain = midGain;
        _lastMidQ = midQ;
        _lastHighFreq = highFreq;
        _lastHighGain = highGain;
        _lastHighQ = highQ;
        _coefficientsNeedUpdate = false;
    }

    /// <summary>
    /// Process a single sample through a biquad filter (Direct Form II Transposed).
    /// This method is inlined for performance and uses the SIMD-compatible state structure.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static float ProcessBiquad(ref BiquadState state, BiquadCoeffs coeffs, float input)
    {
        // Direct Form II Transposed:
        // y[n] = b0*x[n] + z1
        // z1 = b1*x[n] - a1*y[n] + z2
        // z2 = b2*x[n] - a2*y[n]
        float output = coeffs.B0 * input + state.Z1;
        state.Z1 = coeffs.B1 * input - coeffs.A1 * output + state.Z2;
        state.Z2 = coeffs.B2 * input - coeffs.A2 * output;

        return output;
    }
}
