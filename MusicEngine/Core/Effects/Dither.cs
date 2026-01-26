// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio effect processor.

using NAudio.Wave;

namespace MusicEngine.Core.Effects;

/// <summary>
/// Dither type
/// </summary>
public enum DitherType
{
    /// <summary>No dithering (simple truncation)</summary>
    None,
    /// <summary>Rectangular probability distribution (RPDF)</summary>
    RPDF,
    /// <summary>Triangular probability distribution (TPDF) - most common</summary>
    TPDF,
    /// <summary>High-pass triangular (HP-TPDF) - reduces low frequency noise</summary>
    HPTPDF,
    /// <summary>Shaped dither with noise shaping</summary>
    Shaped
}

/// <summary>
/// Noise shaping curve for shaped dithering
/// </summary>
public enum NoiseShapingCurve
{
    /// <summary>No noise shaping</summary>
    None,
    /// <summary>Simple first-order highpass</summary>
    Simple,
    /// <summary>Modified E-weighted curve</summary>
    ModifiedE,
    /// <summary>F-weighted curve (psychoacoustic)</summary>
    FWeighted,
    /// <summary>Improved E-weighted (Sony SBM style)</summary>
    ImprovedE
}

/// <summary>
/// Dithering effect for professional bit-depth reduction.
/// Applies dither noise and optional noise shaping when converting from
/// higher bit depths (float/24-bit) to lower bit depths (16-bit).
/// </summary>
public class Dither : EffectBase
{
    private readonly Random _random = new();

    // State for HP-TPDF
    private float _lastDitherL, _lastDitherR;

    // State for noise shaping
    private readonly float[] _errorBufferL = new float[8];
    private readonly float[] _errorBufferR = new float[8];
    private int _errorIndex;

    // Noise shaping coefficients for different curves
    private static readonly float[] ModifiedECoeffs = { 1.623f, -0.982f, 0.109f, -0.044f };
    private static readonly float[] FWeightedCoeffs = { 2.033f, -2.165f, 1.959f, -1.590f, 0.6149f };
    private static readonly float[] ImprovedECoeffs = { 1.875f, -1.467f, 0.875f, -0.467f, 0.125f, -0.0625f };

    /// <summary>Dither type</summary>
    public DitherType Type { get; set; } = DitherType.TPDF;

    /// <summary>Noise shaping curve</summary>
    public NoiseShapingCurve NoiseShaping { get; set; } = NoiseShapingCurve.None;

    /// <summary>Target bit depth (8, 16, 20, 24)</summary>
    public int TargetBitDepth
    {
        get => _targetBitDepth;
        set => _targetBitDepth = Math.Clamp(value, 8, 24);
    }
    private int _targetBitDepth = 16;

    /// <summary>Dither amount (0-2, 1 = standard)</summary>
    public float Amount { get; set; } = 1.0f;

    /// <summary>Auto-blank: mute dither during silence</summary>
    public bool AutoBlank { get; set; } = true;

    /// <summary>Auto-blank threshold</summary>
    public float AutoBlankThreshold { get; set; } = -90f; // dB

    // For auto-blank
    private float _signalLevelL, _signalLevelR;
    private const float LevelDecay = 0.9995f;

    public Dither(ISampleProvider source) : base(source, "Dither")
    {
        RegisterParameter("TargetBitDepth", 16);
        RegisterParameter("Amount", 1.0f);
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;

        // Calculate quantization step size
        float quantizationLevels = MathF.Pow(2, _targetBitDepth - 1);
        float stepSize = 1f / quantizationLevels;

        for (int n = 0; n < count; n += channels)
        {
            float inputL = sourceBuffer[n];
            float inputR = channels > 1 ? sourceBuffer[n + 1] : inputL;

            // Track signal level for auto-blank
            _signalLevelL = MathF.Max(MathF.Abs(inputL), _signalLevelL * LevelDecay);
            _signalLevelR = MathF.Max(MathF.Abs(inputR), _signalLevelR * LevelDecay);

            // Calculate dither amount based on auto-blank
            float ditherAmountL = Amount;
            float ditherAmountR = Amount;

            if (AutoBlank)
            {
                float thresholdLinear = MathF.Pow(10f, AutoBlankThreshold / 20f);
                if (_signalLevelL < thresholdLinear) ditherAmountL = 0;
                if (_signalLevelR < thresholdLinear) ditherAmountR = 0;
            }

            // Generate dither noise
            float ditherL = GenerateDither(ref _lastDitherL) * stepSize * ditherAmountL;
            float ditherR = GenerateDither(ref _lastDitherR) * stepSize * ditherAmountR;

            // Apply noise shaping
            if (NoiseShaping != NoiseShapingCurve.None && Type == DitherType.Shaped)
            {
                ditherL += GetNoiseShapingError(_errorBufferL);
                ditherR += GetNoiseShapingError(_errorBufferR);
            }

            // Add dither to signal
            float ditheredL = inputL + ditherL;
            float ditheredR = inputR + ditherR;

            // Quantize
            float quantizedL = Quantize(ditheredL, stepSize);
            float quantizedR = Quantize(ditheredR, stepSize);

            // Store quantization error for noise shaping
            if (NoiseShaping != NoiseShapingCurve.None && Type == DitherType.Shaped)
            {
                _errorBufferL[_errorIndex] = quantizedL - inputL;
                _errorBufferR[_errorIndex] = quantizedR - inputR;
                _errorIndex = (_errorIndex + 1) % _errorBufferL.Length;
            }

            destBuffer[offset + n] = quantizedL;
            if (channels > 1)
                destBuffer[offset + n + 1] = quantizedR;
        }
    }

    private float GenerateDither(ref float lastDither)
    {
        switch (Type)
        {
            case DitherType.None:
                return 0f;

            case DitherType.RPDF:
                // Rectangular: uniform distribution -0.5 to 0.5
                return (float)(_random.NextDouble() - 0.5);

            case DitherType.TPDF:
                // Triangular: sum of two uniform distributions
                float r1 = (float)_random.NextDouble() - 0.5f;
                float r2 = (float)_random.NextDouble() - 0.5f;
                return r1 + r2;

            case DitherType.HPTPDF:
                // High-pass triangular: subtract previous sample
                float current = (float)(_random.NextDouble() - 0.5) +
                               (float)(_random.NextDouble() - 0.5);
                float hpDither = current - lastDither;
                lastDither = current;
                return hpDither;

            case DitherType.Shaped:
                // For shaped, use TPDF as base
                return (float)(_random.NextDouble() - 0.5) +
                       (float)(_random.NextDouble() - 0.5);

            default:
                return 0f;
        }
    }

    private float GetNoiseShapingError(float[] errorBuffer)
    {
        float[] coeffs = NoiseShaping switch
        {
            NoiseShapingCurve.Simple => new[] { 0.5f },
            NoiseShapingCurve.ModifiedE => ModifiedECoeffs,
            NoiseShapingCurve.FWeighted => FWeightedCoeffs,
            NoiseShapingCurve.ImprovedE => ImprovedECoeffs,
            _ => Array.Empty<float>()
        };

        if (coeffs.Length == 0) return 0f;

        float shaped = 0f;
        for (int i = 0; i < coeffs.Length && i < errorBuffer.Length; i++)
        {
            int idx = (_errorIndex - i - 1 + errorBuffer.Length) % errorBuffer.Length;
            shaped -= coeffs[i] * errorBuffer[idx];
        }

        return shaped;
    }

    private static float Quantize(float value, float stepSize)
    {
        // Clamp to valid range
        value = Math.Clamp(value, -1f, 1f);

        // Quantize to nearest step
        return MathF.Round(value / stepSize) * stepSize;
    }

    /// <summary>
    /// Reset dither state
    /// </summary>
    public void Reset()
    {
        _lastDitherL = _lastDitherR = 0;
        _signalLevelL = _signalLevelR = 0;
        Array.Clear(_errorBufferL);
        Array.Clear(_errorBufferR);
        _errorIndex = 0;
    }

    protected override void OnParameterChanged(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "targetbitdepth": TargetBitDepth = (int)value; break;
            case "amount": Amount = Math.Clamp(value, 0f, 2f); break;
        }
    }

    #region Presets

    /// <summary>Standard 16-bit dithering for CD quality</summary>
    public static Dither CreateCD16bit(ISampleProvider source)
    {
        return new Dither(source)
        {
            Type = DitherType.TPDF,
            NoiseShaping = NoiseShapingCurve.None,
            TargetBitDepth = 16,
            Amount = 1.0f,
            AutoBlank = true
        };
    }

    /// <summary>High-quality 16-bit with noise shaping</summary>
    public static Dither CreatePOW_R(ISampleProvider source)
    {
        return new Dither(source)
        {
            Type = DitherType.Shaped,
            NoiseShaping = NoiseShapingCurve.ModifiedE,
            TargetBitDepth = 16,
            Amount = 1.0f,
            AutoBlank = true
        };
    }

    /// <summary>Mastering-grade dithering with psychoacoustic noise shaping</summary>
    public static Dither CreateMastering(ISampleProvider source)
    {
        return new Dither(source)
        {
            Type = DitherType.Shaped,
            NoiseShaping = NoiseShapingCurve.ImprovedE,
            TargetBitDepth = 16,
            Amount = 1.0f,
            AutoBlank = true,
            AutoBlankThreshold = -96f
        };
    }

    /// <summary>20-bit dithering for high-resolution</summary>
    public static Dither Create20bit(ISampleProvider source)
    {
        return new Dither(source)
        {
            Type = DitherType.HPTPDF,
            NoiseShaping = NoiseShapingCurve.None,
            TargetBitDepth = 20,
            Amount = 1.0f,
            AutoBlank = true
        };
    }

    #endregion
}
