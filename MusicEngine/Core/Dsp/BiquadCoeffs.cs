// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Description: Biquad filter coefficients structure for SIMD-optimized DSP processing.

using System.Runtime.InteropServices;

namespace MusicEngine.Core.Dsp;

/// <summary>
/// Represents the coefficients of a biquad filter.
/// Based on the Robert Bristow-Johnson Audio EQ Cookbook formulas.
/// </summary>
/// <remarks>
/// <para>
/// The biquad transfer function is:
/// <code>
///        b0 + b1*z^-1 + b2*z^-2
/// H(z) = -----------------------
///        a0 + a1*z^-1 + a2*z^-2
/// </code>
/// </para>
/// <para>
/// In this structure, coefficients are normalized (divided by a0),
/// so a0 is implicitly 1.0 and not stored.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct BiquadCoeffs
{
    /// <summary>
    /// Feedforward coefficient b0 (normalized by a0).
    /// </summary>
    public float B0;

    /// <summary>
    /// Feedforward coefficient b1 (normalized by a0).
    /// </summary>
    public float B1;

    /// <summary>
    /// Feedforward coefficient b2 (normalized by a0).
    /// </summary>
    public float B2;

    /// <summary>
    /// Feedback coefficient a1 (normalized by a0, negated for computation).
    /// </summary>
    public float A1;

    /// <summary>
    /// Feedback coefficient a2 (normalized by a0, negated for computation).
    /// </summary>
    public float A2;

    /// <summary>
    /// Creates a bypass (unity gain, no filtering) coefficient set.
    /// </summary>
    public static BiquadCoeffs Bypass => new()
    {
        B0 = 1f,
        B1 = 0f,
        B2 = 0f,
        A1 = 0f,
        A2 = 0f
    };

    /// <summary>
    /// Creates lowpass filter coefficients.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="frequency">Cutoff frequency in Hz.</param>
    /// <param name="q">Q factor (resonance). 0.707 is Butterworth (flat response).</param>
    public static BiquadCoeffs Lowpass(float sampleRate, float frequency, float q)
    {
        float w0 = 2f * MathF.PI * frequency / sampleRate;
        float cosW0 = MathF.Cos(w0);
        float sinW0 = MathF.Sin(w0);
        float alpha = sinW0 / (2f * q);

        float a0 = 1f + alpha;
        float a1 = -2f * cosW0;
        float a2 = 1f - alpha;
        float b0 = (1f - cosW0) / 2f;
        float b1 = 1f - cosW0;
        float b2 = (1f - cosW0) / 2f;

        return Normalize(b0, b1, b2, a0, a1, a2);
    }

    /// <summary>
    /// Creates highpass filter coefficients.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="frequency">Cutoff frequency in Hz.</param>
    /// <param name="q">Q factor (resonance). 0.707 is Butterworth (flat response).</param>
    public static BiquadCoeffs Highpass(float sampleRate, float frequency, float q)
    {
        float w0 = 2f * MathF.PI * frequency / sampleRate;
        float cosW0 = MathF.Cos(w0);
        float sinW0 = MathF.Sin(w0);
        float alpha = sinW0 / (2f * q);

        float a0 = 1f + alpha;
        float a1 = -2f * cosW0;
        float a2 = 1f - alpha;
        float b0 = (1f + cosW0) / 2f;
        float b1 = -(1f + cosW0);
        float b2 = (1f + cosW0) / 2f;

        return Normalize(b0, b1, b2, a0, a1, a2);
    }

    /// <summary>
    /// Creates bandpass filter coefficients (constant skirt gain, peak gain = Q).
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="frequency">Center frequency in Hz.</param>
    /// <param name="q">Q factor (bandwidth). Higher Q = narrower bandwidth.</param>
    public static BiquadCoeffs Bandpass(float sampleRate, float frequency, float q)
    {
        float w0 = 2f * MathF.PI * frequency / sampleRate;
        float cosW0 = MathF.Cos(w0);
        float sinW0 = MathF.Sin(w0);
        float alpha = sinW0 / (2f * q);

        float a0 = 1f + alpha;
        float a1 = -2f * cosW0;
        float a2 = 1f - alpha;
        float b0 = alpha;
        float b1 = 0f;
        float b2 = -alpha;

        return Normalize(b0, b1, b2, a0, a1, a2);
    }

    /// <summary>
    /// Creates notch (band-reject) filter coefficients.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="frequency">Notch frequency in Hz.</param>
    /// <param name="q">Q factor (notch width). Higher Q = narrower notch.</param>
    public static BiquadCoeffs Notch(float sampleRate, float frequency, float q)
    {
        float w0 = 2f * MathF.PI * frequency / sampleRate;
        float cosW0 = MathF.Cos(w0);
        float sinW0 = MathF.Sin(w0);
        float alpha = sinW0 / (2f * q);

        float a0 = 1f + alpha;
        float a1 = -2f * cosW0;
        float a2 = 1f - alpha;
        float b0 = 1f;
        float b1 = -2f * cosW0;
        float b2 = 1f;

        return Normalize(b0, b1, b2, a0, a1, a2);
    }

    /// <summary>
    /// Creates allpass filter coefficients.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="frequency">Center frequency in Hz.</param>
    /// <param name="q">Q factor.</param>
    public static BiquadCoeffs Allpass(float sampleRate, float frequency, float q)
    {
        float w0 = 2f * MathF.PI * frequency / sampleRate;
        float cosW0 = MathF.Cos(w0);
        float sinW0 = MathF.Sin(w0);
        float alpha = sinW0 / (2f * q);

        float a0 = 1f + alpha;
        float a1 = -2f * cosW0;
        float a2 = 1f - alpha;
        float b0 = 1f - alpha;
        float b1 = -2f * cosW0;
        float b2 = 1f + alpha;

        return Normalize(b0, b1, b2, a0, a1, a2);
    }

    /// <summary>
    /// Creates peaking EQ filter coefficients.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="frequency">Center frequency in Hz.</param>
    /// <param name="q">Q factor (bandwidth).</param>
    /// <param name="gainDb">Gain in decibels (positive = boost, negative = cut).</param>
    public static BiquadCoeffs PeakingEQ(float sampleRate, float frequency, float q, float gainDb)
    {
        float a = MathF.Pow(10f, gainDb / 40f); // sqrt of linear gain
        float w0 = 2f * MathF.PI * frequency / sampleRate;
        float cosW0 = MathF.Cos(w0);
        float sinW0 = MathF.Sin(w0);
        float alpha = sinW0 / (2f * q);

        float a0 = 1f + alpha / a;
        float a1 = -2f * cosW0;
        float a2 = 1f - alpha / a;
        float b0 = 1f + alpha * a;
        float b1 = -2f * cosW0;
        float b2 = 1f - alpha * a;

        return Normalize(b0, b1, b2, a0, a1, a2);
    }

    /// <summary>
    /// Creates low shelf filter coefficients.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="frequency">Shelf frequency in Hz.</param>
    /// <param name="q">Shelf slope (S parameter). S=1 is steepest without overshoot.</param>
    /// <param name="gainDb">Gain in decibels.</param>
    public static BiquadCoeffs LowShelf(float sampleRate, float frequency, float q, float gainDb)
    {
        float a = MathF.Pow(10f, gainDb / 40f);
        float w0 = 2f * MathF.PI * frequency / sampleRate;
        float cosW0 = MathF.Cos(w0);
        float sinW0 = MathF.Sin(w0);
        float alpha = sinW0 / 2f * MathF.Sqrt((a + 1f / a) * (1f / q - 1f) + 2f);
        float sqrtA = MathF.Sqrt(a);

        float a0 = (a + 1f) + (a - 1f) * cosW0 + 2f * sqrtA * alpha;
        float a1 = -2f * ((a - 1f) + (a + 1f) * cosW0);
        float a2 = (a + 1f) + (a - 1f) * cosW0 - 2f * sqrtA * alpha;
        float b0 = a * ((a + 1f) - (a - 1f) * cosW0 + 2f * sqrtA * alpha);
        float b1 = 2f * a * ((a - 1f) - (a + 1f) * cosW0);
        float b2 = a * ((a + 1f) - (a - 1f) * cosW0 - 2f * sqrtA * alpha);

        return Normalize(b0, b1, b2, a0, a1, a2);
    }

    /// <summary>
    /// Creates high shelf filter coefficients.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="frequency">Shelf frequency in Hz.</param>
    /// <param name="q">Shelf slope (S parameter). S=1 is steepest without overshoot.</param>
    /// <param name="gainDb">Gain in decibels.</param>
    public static BiquadCoeffs HighShelf(float sampleRate, float frequency, float q, float gainDb)
    {
        float a = MathF.Pow(10f, gainDb / 40f);
        float w0 = 2f * MathF.PI * frequency / sampleRate;
        float cosW0 = MathF.Cos(w0);
        float sinW0 = MathF.Sin(w0);
        float alpha = sinW0 / 2f * MathF.Sqrt((a + 1f / a) * (1f / q - 1f) + 2f);
        float sqrtA = MathF.Sqrt(a);

        float a0 = (a + 1f) - (a - 1f) * cosW0 + 2f * sqrtA * alpha;
        float a1 = 2f * ((a - 1f) - (a + 1f) * cosW0);
        float a2 = (a + 1f) - (a - 1f) * cosW0 - 2f * sqrtA * alpha;
        float b0 = a * ((a + 1f) + (a - 1f) * cosW0 + 2f * sqrtA * alpha);
        float b1 = -2f * a * ((a - 1f) + (a + 1f) * cosW0);
        float b2 = a * ((a + 1f) + (a - 1f) * cosW0 - 2f * sqrtA * alpha);

        return Normalize(b0, b1, b2, a0, a1, a2);
    }

    /// <summary>
    /// Normalizes coefficients by dividing by a0.
    /// </summary>
    private static BiquadCoeffs Normalize(float b0, float b1, float b2, float a0, float a1, float a2)
    {
        float invA0 = 1f / a0;
        return new BiquadCoeffs
        {
            B0 = b0 * invA0,
            B1 = b1 * invA0,
            B2 = b2 * invA0,
            A1 = a1 * invA0,
            A2 = a2 * invA0
        };
    }

    /// <summary>
    /// Checks if the coefficients are valid (not NaN or Infinity).
    /// </summary>
    public readonly bool IsValid =>
        !float.IsNaN(B0) && !float.IsInfinity(B0) &&
        !float.IsNaN(B1) && !float.IsInfinity(B1) &&
        !float.IsNaN(B2) && !float.IsInfinity(B2) &&
        !float.IsNaN(A1) && !float.IsInfinity(A1) &&
        !float.IsNaN(A2) && !float.IsInfinity(A2);
}
