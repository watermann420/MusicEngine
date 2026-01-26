// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Description: SIMD-optimized DSP operations using AVX/SSE intrinsics.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace MusicEngine.Core.Dsp;

/// <summary>
/// Provides SIMD-optimized DSP operations for high-performance audio processing.
/// </summary>
/// <remarks>
/// <para>
/// This class automatically selects the best available instruction set:
/// <list type="bullet">
/// <item><description>AVX/AVX2: Processes 8 floats at once (256-bit vectors)</description></item>
/// <item><description>SSE/SSE2: Processes 4 floats at once (128-bit vectors)</description></item>
/// <item><description>Scalar: Fallback for systems without SIMD support</description></item>
/// </list>
/// </para>
/// <para>
/// Use <see cref="GetOptimizationLevel"/> to check which instruction set is being used.
/// </para>
/// </remarks>
public static class SimdDsp
{
    #region Optimization Level Detection

    /// <summary>
    /// Returns the current SIMD optimization level being used.
    /// </summary>
    /// <returns>
    /// "AVX2" if AVX2 is supported,
    /// "AVX" if AVX is supported,
    /// "SSE" if SSE is supported,
    /// "Scalar" otherwise.
    /// </returns>
    public static string GetOptimizationLevel()
    {
        if (Avx2.IsSupported) return "AVX2";
        if (Avx.IsSupported) return "AVX";
        if (Sse.IsSupported) return "SSE";
        return "Scalar";
    }

    /// <summary>
    /// Returns true if any SIMD optimization is available.
    /// </summary>
    public static bool IsSimdSupported => Sse.IsSupported;

    /// <summary>
    /// Returns the vector width in floats (8 for AVX, 4 for SSE, 1 for scalar).
    /// </summary>
    public static int VectorWidth
    {
        get
        {
            if (Avx.IsSupported) return 8;
            if (Sse.IsSupported) return 4;
            return 1;
        }
    }

    #endregion

    #region Buffer Operations

    /// <summary>
    /// Adds source buffer to destination buffer: dest[i] += src[i].
    /// </summary>
    /// <param name="dest">Destination buffer (modified in place).</param>
    /// <param name="src">Source buffer to add.</param>
    /// <param name="length">Number of samples to process.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Add(float[] dest, float[] src, int length)
    {
        ArgumentNullException.ThrowIfNull(dest);
        ArgumentNullException.ThrowIfNull(src);

        if (length <= 0) return;
        length = Math.Min(length, Math.Min(dest.Length, src.Length));

        if (Avx.IsSupported)
        {
            AddAvx(dest, src, length);
        }
        else if (Sse.IsSupported)
        {
            AddSse(dest, src, length);
        }
        else
        {
            AddScalar(dest, src, length);
        }
    }

    private static unsafe void AddAvx(float[] dest, float[] src, int length)
    {
        fixed (float* pDest = dest, pSrc = src)
        {
            int i = 0;
            int vectorEnd = length - 7;

            // Process 8 floats at a time
            for (; i <= vectorEnd; i += 8)
            {
                var vDest = Avx.LoadVector256(pDest + i);
                var vSrc = Avx.LoadVector256(pSrc + i);
                var vResult = Avx.Add(vDest, vSrc);
                Avx.Store(pDest + i, vResult);
            }

            // Handle remaining elements
            for (; i < length; i++)
            {
                pDest[i] += pSrc[i];
            }
        }
    }

    private static unsafe void AddSse(float[] dest, float[] src, int length)
    {
        fixed (float* pDest = dest, pSrc = src)
        {
            int i = 0;
            int vectorEnd = length - 3;

            // Process 4 floats at a time
            for (; i <= vectorEnd; i += 4)
            {
                var vDest = Sse.LoadVector128(pDest + i);
                var vSrc = Sse.LoadVector128(pSrc + i);
                var vResult = Sse.Add(vDest, vSrc);
                Sse.Store(pDest + i, vResult);
            }

            // Handle remaining elements
            for (; i < length; i++)
            {
                pDest[i] += pSrc[i];
            }
        }
    }

    private static void AddScalar(float[] dest, float[] src, int length)
    {
        for (int i = 0; i < length; i++)
        {
            dest[i] += src[i];
        }
    }

    /// <summary>
    /// Multiplies destination buffer by source buffer: dest[i] *= src[i].
    /// </summary>
    /// <param name="dest">Destination buffer (modified in place).</param>
    /// <param name="src">Source buffer to multiply by.</param>
    /// <param name="length">Number of samples to process.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Multiply(float[] dest, float[] src, int length)
    {
        ArgumentNullException.ThrowIfNull(dest);
        ArgumentNullException.ThrowIfNull(src);

        if (length <= 0) return;
        length = Math.Min(length, Math.Min(dest.Length, src.Length));

        if (Avx.IsSupported)
        {
            MultiplyAvx(dest, src, length);
        }
        else if (Sse.IsSupported)
        {
            MultiplySse(dest, src, length);
        }
        else
        {
            MultiplyScalar(dest, src, length);
        }
    }

    private static unsafe void MultiplyAvx(float[] dest, float[] src, int length)
    {
        fixed (float* pDest = dest, pSrc = src)
        {
            int i = 0;
            int vectorEnd = length - 7;

            for (; i <= vectorEnd; i += 8)
            {
                var vDest = Avx.LoadVector256(pDest + i);
                var vSrc = Avx.LoadVector256(pSrc + i);
                var vResult = Avx.Multiply(vDest, vSrc);
                Avx.Store(pDest + i, vResult);
            }

            for (; i < length; i++)
            {
                pDest[i] *= pSrc[i];
            }
        }
    }

    private static unsafe void MultiplySse(float[] dest, float[] src, int length)
    {
        fixed (float* pDest = dest, pSrc = src)
        {
            int i = 0;
            int vectorEnd = length - 3;

            for (; i <= vectorEnd; i += 4)
            {
                var vDest = Sse.LoadVector128(pDest + i);
                var vSrc = Sse.LoadVector128(pSrc + i);
                var vResult = Sse.Multiply(vDest, vSrc);
                Sse.Store(pDest + i, vResult);
            }

            for (; i < length; i++)
            {
                pDest[i] *= pSrc[i];
            }
        }
    }

    private static void MultiplyScalar(float[] dest, float[] src, int length)
    {
        for (int i = 0; i < length; i++)
        {
            dest[i] *= src[i];
        }
    }

    /// <summary>
    /// Multiply-accumulate: dest[i] += src[i] * gain.
    /// </summary>
    /// <param name="dest">Destination buffer (modified in place).</param>
    /// <param name="src">Source buffer.</param>
    /// <param name="gain">Gain multiplier.</param>
    /// <param name="length">Number of samples to process.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MultiplyAdd(float[] dest, float[] src, float gain, int length)
    {
        ArgumentNullException.ThrowIfNull(dest);
        ArgumentNullException.ThrowIfNull(src);

        if (length <= 0) return;
        length = Math.Min(length, Math.Min(dest.Length, src.Length));

        if (Avx.IsSupported && Fma.IsSupported)
        {
            MultiplyAddFma(dest, src, gain, length);
        }
        else if (Avx.IsSupported)
        {
            MultiplyAddAvx(dest, src, gain, length);
        }
        else if (Sse.IsSupported)
        {
            MultiplyAddSse(dest, src, gain, length);
        }
        else
        {
            MultiplyAddScalar(dest, src, gain, length);
        }
    }

    private static unsafe void MultiplyAddFma(float[] dest, float[] src, float gain, int length)
    {
        fixed (float* pDest = dest, pSrc = src)
        {
            var vGain = Vector256.Create(gain);
            int i = 0;
            int vectorEnd = length - 7;

            for (; i <= vectorEnd; i += 8)
            {
                var vDest = Avx.LoadVector256(pDest + i);
                var vSrc = Avx.LoadVector256(pSrc + i);
                // FMA: dest = dest + (src * gain)
                var vResult = Fma.MultiplyAdd(vSrc, vGain, vDest);
                Avx.Store(pDest + i, vResult);
            }

            for (; i < length; i++)
            {
                pDest[i] += pSrc[i] * gain;
            }
        }
    }

    private static unsafe void MultiplyAddAvx(float[] dest, float[] src, float gain, int length)
    {
        fixed (float* pDest = dest, pSrc = src)
        {
            var vGain = Vector256.Create(gain);
            int i = 0;
            int vectorEnd = length - 7;

            for (; i <= vectorEnd; i += 8)
            {
                var vDest = Avx.LoadVector256(pDest + i);
                var vSrc = Avx.LoadVector256(pSrc + i);
                var vScaled = Avx.Multiply(vSrc, vGain);
                var vResult = Avx.Add(vDest, vScaled);
                Avx.Store(pDest + i, vResult);
            }

            for (; i < length; i++)
            {
                pDest[i] += pSrc[i] * gain;
            }
        }
    }

    private static unsafe void MultiplyAddSse(float[] dest, float[] src, float gain, int length)
    {
        fixed (float* pDest = dest, pSrc = src)
        {
            var vGain = Vector128.Create(gain);
            int i = 0;
            int vectorEnd = length - 3;

            for (; i <= vectorEnd; i += 4)
            {
                var vDest = Sse.LoadVector128(pDest + i);
                var vSrc = Sse.LoadVector128(pSrc + i);
                var vScaled = Sse.Multiply(vSrc, vGain);
                var vResult = Sse.Add(vDest, vScaled);
                Sse.Store(pDest + i, vResult);
            }

            for (; i < length; i++)
            {
                pDest[i] += pSrc[i] * gain;
            }
        }
    }

    private static void MultiplyAddScalar(float[] dest, float[] src, float gain, int length)
    {
        for (int i = 0; i < length; i++)
        {
            dest[i] += src[i] * gain;
        }
    }

    /// <summary>
    /// Scales buffer by a constant gain: buffer[i] *= gain.
    /// </summary>
    /// <param name="buffer">Buffer to scale (modified in place).</param>
    /// <param name="gain">Gain multiplier.</param>
    /// <param name="length">Number of samples to process.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Scale(float[] buffer, float gain, int length)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (length <= 0) return;
        length = Math.Min(length, buffer.Length);

        // Optimization: skip if gain is 1.0
        if (MathF.Abs(gain - 1f) < 1e-7f) return;

        if (Avx.IsSupported)
        {
            ScaleAvx(buffer, gain, length);
        }
        else if (Sse.IsSupported)
        {
            ScaleSse(buffer, gain, length);
        }
        else
        {
            ScaleScalar(buffer, gain, length);
        }
    }

    private static unsafe void ScaleAvx(float[] buffer, float gain, int length)
    {
        fixed (float* pBuffer = buffer)
        {
            var vGain = Vector256.Create(gain);
            int i = 0;
            int vectorEnd = length - 7;

            for (; i <= vectorEnd; i += 8)
            {
                var vData = Avx.LoadVector256(pBuffer + i);
                var vResult = Avx.Multiply(vData, vGain);
                Avx.Store(pBuffer + i, vResult);
            }

            for (; i < length; i++)
            {
                pBuffer[i] *= gain;
            }
        }
    }

    private static unsafe void ScaleSse(float[] buffer, float gain, int length)
    {
        fixed (float* pBuffer = buffer)
        {
            var vGain = Vector128.Create(gain);
            int i = 0;
            int vectorEnd = length - 3;

            for (; i <= vectorEnd; i += 4)
            {
                var vData = Sse.LoadVector128(pBuffer + i);
                var vResult = Sse.Multiply(vData, vGain);
                Sse.Store(pBuffer + i, vResult);
            }

            for (; i < length; i++)
            {
                pBuffer[i] *= gain;
            }
        }
    }

    private static void ScaleScalar(float[] buffer, float gain, int length)
    {
        for (int i = 0; i < length; i++)
        {
            buffer[i] *= gain;
        }
    }

    /// <summary>
    /// Mixes multiple source buffers into a destination buffer: dest = sum(sources).
    /// </summary>
    /// <param name="dest">Destination buffer (will be overwritten).</param>
    /// <param name="sources">Array of source buffers to mix.</param>
    /// <param name="length">Number of samples to process.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Mix(float[] dest, float[][] sources, int length)
    {
        ArgumentNullException.ThrowIfNull(dest);
        ArgumentNullException.ThrowIfNull(sources);

        if (length <= 0 || sources.Length == 0) return;
        length = Math.Min(length, dest.Length);

        // Clear destination first
        Clear(dest, length);

        // Add each source
        foreach (var source in sources)
        {
            if (source != null)
            {
                int srcLen = Math.Min(length, source.Length);
                Add(dest, source, srcLen);
            }
        }
    }

    /// <summary>
    /// Clears (zeros) a buffer.
    /// </summary>
    /// <param name="buffer">Buffer to clear.</param>
    /// <param name="length">Number of samples to clear.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Clear(float[] buffer, int length)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (length <= 0) return;
        length = Math.Min(length, buffer.Length);

        if (Avx.IsSupported)
        {
            ClearAvx(buffer, length);
        }
        else if (Sse.IsSupported)
        {
            ClearSse(buffer, length);
        }
        else
        {
            Array.Clear(buffer, 0, length);
        }
    }

    private static unsafe void ClearAvx(float[] buffer, int length)
    {
        fixed (float* pBuffer = buffer)
        {
            var vZero = Vector256<float>.Zero;
            int i = 0;
            int vectorEnd = length - 7;

            for (; i <= vectorEnd; i += 8)
            {
                Avx.Store(pBuffer + i, vZero);
            }

            for (; i < length; i++)
            {
                pBuffer[i] = 0f;
            }
        }
    }

    private static unsafe void ClearSse(float[] buffer, int length)
    {
        fixed (float* pBuffer = buffer)
        {
            var vZero = Vector128<float>.Zero;
            int i = 0;
            int vectorEnd = length - 3;

            for (; i <= vectorEnd; i += 4)
            {
                Sse.Store(pBuffer + i, vZero);
            }

            for (; i < length; i++)
            {
                pBuffer[i] = 0f;
            }
        }
    }

    /// <summary>
    /// Copies source buffer to destination buffer.
    /// </summary>
    /// <param name="dest">Destination buffer.</param>
    /// <param name="src">Source buffer.</param>
    /// <param name="length">Number of samples to copy.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Copy(float[] dest, float[] src, int length)
    {
        ArgumentNullException.ThrowIfNull(dest);
        ArgumentNullException.ThrowIfNull(src);

        if (length <= 0) return;
        length = Math.Min(length, Math.Min(dest.Length, src.Length));

        // For copying, Array.Copy is highly optimized and often uses SIMD internally
        Array.Copy(src, 0, dest, 0, length);
    }

    #endregion

    #region Filter Operations

    /// <summary>
    /// Applies a biquad filter to a buffer in place.
    /// </summary>
    /// <param name="buffer">Buffer to filter (modified in place).</param>
    /// <param name="state">Filter state (persists between calls).</param>
    /// <param name="coeffs">Filter coefficients.</param>
    /// <param name="length">Number of samples to process.</param>
    /// <remarks>
    /// <para>
    /// Uses Direct Form II Transposed implementation:
    /// <code>
    /// y[n] = b0*x[n] + z1
    /// z1 = b1*x[n] - a1*y[n] + z2
    /// z2 = b2*x[n] - a2*y[n]
    /// </code>
    /// </para>
    /// <para>
    /// Note: Biquad filters are inherently serial (each output depends on the previous),
    /// so SIMD optimization is limited. This implementation uses SIMD for coefficient
    /// multiplication where possible but processes samples sequentially.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ApplyBiquad(float[] buffer, ref BiquadState state, BiquadCoeffs coeffs, int length)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (length <= 0) return;
        length = Math.Min(length, buffer.Length);

        // Biquad filters are inherently serial, so we use scalar processing
        // but with optimized coefficient access
        ApplyBiquadScalar(buffer, ref state, coeffs, length);
    }

    private static void ApplyBiquadScalar(float[] buffer, ref BiquadState state, BiquadCoeffs coeffs, int length)
    {
        float b0 = coeffs.B0;
        float b1 = coeffs.B1;
        float b2 = coeffs.B2;
        float a1 = coeffs.A1;
        float a2 = coeffs.A2;
        float z1 = state.Z1;
        float z2 = state.Z2;

        for (int i = 0; i < length; i++)
        {
            float input = buffer[i];

            // Direct Form II Transposed
            float output = b0 * input + z1;
            z1 = b1 * input - a1 * output + z2;
            z2 = b2 * input - a2 * output;

            buffer[i] = output;
        }

        state.Z1 = z1;
        state.Z2 = z2;
    }

    /// <summary>
    /// Applies biquad filters to multiple channels in interleaved format.
    /// </summary>
    /// <param name="buffer">Interleaved buffer to filter.</param>
    /// <param name="states">Array of filter states (one per channel).</param>
    /// <param name="coeffs">Filter coefficients (same for all channels).</param>
    /// <param name="channels">Number of interleaved channels.</param>
    /// <param name="length">Total number of samples (all channels combined).</param>
    public static void ApplyBiquadInterleaved(
        float[] buffer,
        BiquadState[] states,
        BiquadCoeffs coeffs,
        int channels,
        int length)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(states);

        if (length <= 0 || channels <= 0) return;
        if (states.Length < channels)
            throw new ArgumentException("Not enough states for the number of channels.", nameof(states));

        length = Math.Min(length, buffer.Length);

        float b0 = coeffs.B0;
        float b1 = coeffs.B1;
        float b2 = coeffs.B2;
        float a1 = coeffs.A1;
        float a2 = coeffs.A2;

        for (int i = 0; i < length; i += channels)
        {
            for (int ch = 0; ch < channels && (i + ch) < length; ch++)
            {
                int idx = i + ch;
                float input = buffer[idx];

                ref BiquadState state = ref states[ch];
                float z1 = state.Z1;
                float z2 = state.Z2;

                // Direct Form II Transposed
                float output = b0 * input + z1;
                z1 = b1 * input - a1 * output + z2;
                z2 = b2 * input - a2 * output;

                buffer[idx] = output;
                state.Z1 = z1;
                state.Z2 = z2;
            }
        }
    }

    #endregion

    #region Analysis Operations

    /// <summary>
    /// Finds the peak absolute value in a buffer.
    /// </summary>
    /// <param name="buffer">Buffer to analyze.</param>
    /// <param name="length">Number of samples to analyze.</param>
    /// <returns>The maximum absolute value found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetPeak(float[] buffer, int length)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (length <= 0) return 0f;
        length = Math.Min(length, buffer.Length);

        if (Avx.IsSupported)
        {
            return GetPeakAvx(buffer, length);
        }
        else if (Sse.IsSupported)
        {
            return GetPeakSse(buffer, length);
        }
        else
        {
            return GetPeakScalar(buffer, length);
        }
    }

    private static unsafe float GetPeakAvx(float[] buffer, int length)
    {
        fixed (float* pBuffer = buffer)
        {
            // Mask for absolute value (clear sign bit)
            var absMask = Vector256.Create(0x7FFFFFFF).AsSingle();
            var vMax = Vector256<float>.Zero;

            int i = 0;
            int vectorEnd = length - 7;

            for (; i <= vectorEnd; i += 8)
            {
                var vData = Avx.LoadVector256(pBuffer + i);
                var vAbs = Avx.And(vData, absMask);
                vMax = Avx.Max(vMax, vAbs);
            }

            // Horizontal max reduction
            float max = HorizontalMaxAvx(vMax);

            // Handle remaining elements
            for (; i < length; i++)
            {
                float absVal = MathF.Abs(pBuffer[i]);
                if (absVal > max) max = absVal;
            }

            return max;
        }
    }

    private static float HorizontalMaxAvx(Vector256<float> v)
    {
        // Reduce 256-bit to 128-bit
        var high = Avx.ExtractVector128(v, 1);
        var low = v.GetLower();
        var max128 = Sse.Max(high, low);

        // Reduce 128-bit to scalar
        return HorizontalMaxSse(max128);
    }

    private static unsafe float GetPeakSse(float[] buffer, int length)
    {
        fixed (float* pBuffer = buffer)
        {
            var absMask = Vector128.Create(0x7FFFFFFF).AsSingle();
            var vMax = Vector128<float>.Zero;

            int i = 0;
            int vectorEnd = length - 3;

            for (; i <= vectorEnd; i += 4)
            {
                var vData = Sse.LoadVector128(pBuffer + i);
                var vAbs = Sse.And(vData, absMask);
                vMax = Sse.Max(vMax, vAbs);
            }

            float max = HorizontalMaxSse(vMax);

            for (; i < length; i++)
            {
                float absVal = MathF.Abs(pBuffer[i]);
                if (absVal > max) max = absVal;
            }

            return max;
        }
    }

    private static float HorizontalMaxSse(Vector128<float> v)
    {
        // Shuffle and max reduction
        var shuf = Sse.Shuffle(v, v, 0b10_11_00_01); // swap pairs
        var max1 = Sse.Max(v, shuf);
        var shuf2 = Sse.Shuffle(max1, max1, 0b00_00_10_10); // swap halves
        var max2 = Sse.Max(max1, shuf2);
        return max2.ToScalar();
    }

    private static float GetPeakScalar(float[] buffer, int length)
    {
        float max = 0f;
        for (int i = 0; i < length; i++)
        {
            float absVal = MathF.Abs(buffer[i]);
            if (absVal > max) max = absVal;
        }
        return max;
    }

    /// <summary>
    /// Calculates the RMS (Root Mean Square) of a buffer.
    /// </summary>
    /// <param name="buffer">Buffer to analyze.</param>
    /// <param name="length">Number of samples to analyze.</param>
    /// <returns>The RMS value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetRms(float[] buffer, int length)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (length <= 0) return 0f;
        length = Math.Min(length, buffer.Length);

        if (Avx.IsSupported)
        {
            return GetRmsAvx(buffer, length);
        }
        else if (Sse.IsSupported)
        {
            return GetRmsSse(buffer, length);
        }
        else
        {
            return GetRmsScalar(buffer, length);
        }
    }

    private static unsafe float GetRmsAvx(float[] buffer, int length)
    {
        fixed (float* pBuffer = buffer)
        {
            var vSum = Vector256<float>.Zero;

            int i = 0;
            int vectorEnd = length - 7;

            for (; i <= vectorEnd; i += 8)
            {
                var vData = Avx.LoadVector256(pBuffer + i);
                var vSquared = Avx.Multiply(vData, vData);
                vSum = Avx.Add(vSum, vSquared);
            }

            // Horizontal sum
            float sum = HorizontalSumAvx(vSum);

            // Handle remaining elements
            for (; i < length; i++)
            {
                float val = pBuffer[i];
                sum += val * val;
            }

            return MathF.Sqrt(sum / length);
        }
    }

    private static float HorizontalSumAvx(Vector256<float> v)
    {
        var high = Avx.ExtractVector128(v, 1);
        var low = v.GetLower();
        var sum128 = Sse.Add(high, low);
        return HorizontalSumSse(sum128);
    }

    private static unsafe float GetRmsSse(float[] buffer, int length)
    {
        fixed (float* pBuffer = buffer)
        {
            var vSum = Vector128<float>.Zero;

            int i = 0;
            int vectorEnd = length - 3;

            for (; i <= vectorEnd; i += 4)
            {
                var vData = Sse.LoadVector128(pBuffer + i);
                var vSquared = Sse.Multiply(vData, vData);
                vSum = Sse.Add(vSum, vSquared);
            }

            float sum = HorizontalSumSse(vSum);

            for (; i < length; i++)
            {
                float val = pBuffer[i];
                sum += val * val;
            }

            return MathF.Sqrt(sum / length);
        }
    }

    private static float HorizontalSumSse(Vector128<float> v)
    {
        // Shuffle and add reduction
        var shuf = Sse.Shuffle(v, v, 0b10_11_00_01);
        var sum1 = Sse.Add(v, shuf);
        var shuf2 = Sse.Shuffle(sum1, sum1, 0b00_00_10_10);
        var sum2 = Sse.Add(sum1, shuf2);
        return sum2.ToScalar();
    }

    private static float GetRmsScalar(float[] buffer, int length)
    {
        float sum = 0f;
        for (int i = 0; i < length; i++)
        {
            float val = buffer[i];
            sum += val * val;
        }
        return MathF.Sqrt(sum / length);
    }

    /// <summary>
    /// Finds the minimum and maximum values in a buffer.
    /// </summary>
    /// <param name="buffer">Buffer to analyze.</param>
    /// <param name="length">Number of samples to analyze.</param>
    /// <param name="min">Output: minimum value found.</param>
    /// <param name="max">Output: maximum value found.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetMinMax(float[] buffer, int length, out float min, out float max)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (length <= 0)
        {
            min = 0f;
            max = 0f;
            return;
        }

        length = Math.Min(length, buffer.Length);

        if (Avx.IsSupported)
        {
            GetMinMaxAvx(buffer, length, out min, out max);
        }
        else if (Sse.IsSupported)
        {
            GetMinMaxSse(buffer, length, out min, out max);
        }
        else
        {
            GetMinMaxScalar(buffer, length, out min, out max);
        }
    }

    private static unsafe void GetMinMaxAvx(float[] buffer, int length, out float min, out float max)
    {
        fixed (float* pBuffer = buffer)
        {
            var vMin = Vector256.Create(float.MaxValue);
            var vMax = Vector256.Create(float.MinValue);

            int i = 0;
            int vectorEnd = length - 7;

            for (; i <= vectorEnd; i += 8)
            {
                var vData = Avx.LoadVector256(pBuffer + i);
                vMin = Avx.Min(vMin, vData);
                vMax = Avx.Max(vMax, vData);
            }

            // Horizontal reduction
            min = HorizontalMinAvx(vMin);
            max = HorizontalMaxAvx(vMax);

            // Handle remaining elements
            for (; i < length; i++)
            {
                float val = pBuffer[i];
                if (val < min) min = val;
                if (val > max) max = val;
            }
        }
    }

    private static float HorizontalMinAvx(Vector256<float> v)
    {
        var high = Avx.ExtractVector128(v, 1);
        var low = v.GetLower();
        var min128 = Sse.Min(high, low);
        return HorizontalMinSse(min128);
    }

    private static unsafe void GetMinMaxSse(float[] buffer, int length, out float min, out float max)
    {
        fixed (float* pBuffer = buffer)
        {
            var vMin = Vector128.Create(float.MaxValue);
            var vMax = Vector128.Create(float.MinValue);

            int i = 0;
            int vectorEnd = length - 3;

            for (; i <= vectorEnd; i += 4)
            {
                var vData = Sse.LoadVector128(pBuffer + i);
                vMin = Sse.Min(vMin, vData);
                vMax = Sse.Max(vMax, vData);
            }

            min = HorizontalMinSse(vMin);
            max = HorizontalMaxSse(vMax);

            for (; i < length; i++)
            {
                float val = pBuffer[i];
                if (val < min) min = val;
                if (val > max) max = val;
            }
        }
    }

    private static float HorizontalMinSse(Vector128<float> v)
    {
        var shuf = Sse.Shuffle(v, v, 0b10_11_00_01);
        var min1 = Sse.Min(v, shuf);
        var shuf2 = Sse.Shuffle(min1, min1, 0b00_00_10_10);
        var min2 = Sse.Min(min1, shuf2);
        return min2.ToScalar();
    }

    private static void GetMinMaxScalar(float[] buffer, int length, out float min, out float max)
    {
        min = buffer[0];
        max = buffer[0];

        for (int i = 1; i < length; i++)
        {
            float val = buffer[i];
            if (val < min) min = val;
            if (val > max) max = val;
        }
    }

    #endregion

    #region Interpolation Operations

    /// <summary>
    /// Performs linear interpolation to resample a buffer.
    /// </summary>
    /// <param name="dest">Destination buffer.</param>
    /// <param name="src">Source buffer.</param>
    /// <param name="ratio">Resampling ratio (dest length / src length).</param>
    /// <param name="destLength">Number of samples to write to destination.</param>
    /// <remarks>
    /// A ratio &gt; 1 upsamples (increases sample rate), ratio &lt; 1 downsamples.
    /// </remarks>
    public static void LinearInterpolate(float[] dest, float[] src, float ratio, int destLength)
    {
        ArgumentNullException.ThrowIfNull(dest);
        ArgumentNullException.ThrowIfNull(src);

        if (destLength <= 0 || src.Length == 0) return;
        destLength = Math.Min(destLength, dest.Length);

        // Linear interpolation is hard to SIMD effectively due to non-contiguous memory access
        // Use scalar implementation with optimized inner loop
        LinearInterpolateScalar(dest, src, ratio, destLength);
    }

    private static void LinearInterpolateScalar(float[] dest, float[] src, float ratio, int destLength)
    {
        float srcPos = 0f;
        float srcIncrement = 1f / ratio;
        int srcLen = src.Length;

        for (int i = 0; i < destLength; i++)
        {
            int idx0 = (int)srcPos;
            int idx1 = idx0 + 1;

            // Clamp indices
            if (idx0 >= srcLen - 1)
            {
                dest[i] = src[srcLen - 1];
            }
            else
            {
                float frac = srcPos - idx0;
                dest[i] = src[idx0] * (1f - frac) + src[idx1] * frac;
            }

            srcPos += srcIncrement;
        }
    }

    /// <summary>
    /// Performs cubic (Hermite) interpolation to resample a buffer.
    /// </summary>
    /// <param name="dest">Destination buffer.</param>
    /// <param name="src">Source buffer.</param>
    /// <param name="ratio">Resampling ratio (dest length / src length).</param>
    /// <param name="destLength">Number of samples to write to destination.</param>
    /// <remarks>
    /// Cubic interpolation provides smoother results than linear interpolation,
    /// especially for audio pitch shifting. Uses Catmull-Rom spline interpolation.
    /// </remarks>
    public static void CubicInterpolate(float[] dest, float[] src, float ratio, int destLength)
    {
        ArgumentNullException.ThrowIfNull(dest);
        ArgumentNullException.ThrowIfNull(src);

        if (destLength <= 0 || src.Length < 4) return;
        destLength = Math.Min(destLength, dest.Length);

        CubicInterpolateScalar(dest, src, ratio, destLength);
    }

    private static void CubicInterpolateScalar(float[] dest, float[] src, float ratio, int destLength)
    {
        float srcPos = 0f;
        float srcIncrement = 1f / ratio;
        int srcLen = src.Length;

        for (int i = 0; i < destLength; i++)
        {
            int idx1 = (int)srcPos;
            int idx0 = idx1 - 1;
            int idx2 = idx1 + 1;
            int idx3 = idx1 + 2;

            // Clamp indices
            idx0 = Math.Max(0, idx0);
            idx1 = Math.Clamp(idx1, 0, srcLen - 1);
            idx2 = Math.Min(idx2, srcLen - 1);
            idx3 = Math.Min(idx3, srcLen - 1);

            float frac = srcPos - (int)srcPos;

            // Catmull-Rom spline interpolation
            float y0 = src[idx0];
            float y1 = src[idx1];
            float y2 = src[idx2];
            float y3 = src[idx3];

            float a0 = -0.5f * y0 + 1.5f * y1 - 1.5f * y2 + 0.5f * y3;
            float a1 = y0 - 2.5f * y1 + 2f * y2 - 0.5f * y3;
            float a2 = -0.5f * y0 + 0.5f * y2;
            float a3 = y1;

            dest[i] = ((a0 * frac + a1) * frac + a2) * frac + a3;

            srcPos += srcIncrement;
        }
    }

    #endregion

    #region Additional Utility Operations

    /// <summary>
    /// Clips (limits) buffer values to the specified range.
    /// </summary>
    /// <param name="buffer">Buffer to clip (modified in place).</param>
    /// <param name="min">Minimum allowed value.</param>
    /// <param name="max">Maximum allowed value.</param>
    /// <param name="length">Number of samples to process.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Clip(float[] buffer, float min, float max, int length)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (length <= 0) return;
        length = Math.Min(length, buffer.Length);

        if (Avx.IsSupported)
        {
            ClipAvx(buffer, min, max, length);
        }
        else if (Sse.IsSupported)
        {
            ClipSse(buffer, min, max, length);
        }
        else
        {
            ClipScalar(buffer, min, max, length);
        }
    }

    private static unsafe void ClipAvx(float[] buffer, float min, float max, int length)
    {
        fixed (float* pBuffer = buffer)
        {
            var vMin = Vector256.Create(min);
            var vMax = Vector256.Create(max);

            int i = 0;
            int vectorEnd = length - 7;

            for (; i <= vectorEnd; i += 8)
            {
                var vData = Avx.LoadVector256(pBuffer + i);
                vData = Avx.Max(vData, vMin);
                vData = Avx.Min(vData, vMax);
                Avx.Store(pBuffer + i, vData);
            }

            for (; i < length; i++)
            {
                pBuffer[i] = Math.Clamp(pBuffer[i], min, max);
            }
        }
    }

    private static unsafe void ClipSse(float[] buffer, float min, float max, int length)
    {
        fixed (float* pBuffer = buffer)
        {
            var vMin = Vector128.Create(min);
            var vMax = Vector128.Create(max);

            int i = 0;
            int vectorEnd = length - 3;

            for (; i <= vectorEnd; i += 4)
            {
                var vData = Sse.LoadVector128(pBuffer + i);
                vData = Sse.Max(vData, vMin);
                vData = Sse.Min(vData, vMax);
                Sse.Store(pBuffer + i, vData);
            }

            for (; i < length; i++)
            {
                pBuffer[i] = Math.Clamp(pBuffer[i], min, max);
            }
        }
    }

    private static void ClipScalar(float[] buffer, float min, float max, int length)
    {
        for (int i = 0; i < length; i++)
        {
            buffer[i] = Math.Clamp(buffer[i], min, max);
        }
    }

    /// <summary>
    /// Applies soft clipping (tanh-like saturation) to a buffer.
    /// </summary>
    /// <param name="buffer">Buffer to process (modified in place).</param>
    /// <param name="drive">Saturation amount (1.0 = no effect, higher = more saturation).</param>
    /// <param name="length">Number of samples to process.</param>
    public static void SoftClip(float[] buffer, float drive, int length)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (length <= 0) return;
        length = Math.Min(length, buffer.Length);

        // Soft clipping uses tanh which is hard to SIMD, use scalar
        for (int i = 0; i < length; i++)
        {
            buffer[i] = MathF.Tanh(buffer[i] * drive);
        }
    }

    /// <summary>
    /// Converts decibels to linear gain.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DbToLinear(float db) => MathF.Pow(10f, db / 20f);

    /// <summary>
    /// Converts linear gain to decibels.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float LinearToDb(float linear) => 20f * MathF.Log10(Math.Max(linear, 1e-10f));

    #endregion
}
