//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: VocAlign-style audio alignment for syncing vocal/instrument tracks to a reference using time warping.

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Provides VocAlign-style audio alignment capabilities.
/// Aligns a target audio track to a reference track by analyzing timing differences
/// and applying time warping to synchronize the performances.
/// </summary>
/// <remarks>
/// Common use cases:
/// - Tightening vocal doubles and harmonies
/// - Syncing dubbed dialogue to original
/// - Aligning multi-mic recordings
/// - Syncing live performances to studio tracks
///
/// The algorithm uses Dynamic Time Warping (DTW) with chroma features to find
/// the optimal alignment, then applies phase vocoder time stretching to warp
/// the target audio without affecting pitch.
/// </remarks>
public class AudioAlignment
{
    private readonly DTWAligner _dtw = new();

    // Phase vocoder settings
    private int _fftSize = 4096;
    private int _hopSize = 1024;

    /// <summary>
    /// Gets or sets how strictly the target should follow the reference timing (0.0 to 1.0).
    /// Higher values produce tighter sync but may introduce artifacts.
    /// </summary>
    public float SyncTightness { get; set; } = 0.8f;

    /// <summary>
    /// Gets or sets whether to preserve formants during time stretching.
    /// Recommended for vocals to maintain natural timbre.
    /// </summary>
    public bool PreserveFormants { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum allowed time stretch ratio.
    /// Limits how much the audio can be sped up or slowed down (e.g., 1.5 = 50% max change).
    /// </summary>
    public float MaxStretchRatio { get; set; } = 1.5f;

    /// <summary>
    /// Gets or sets the minimum stretch ratio (inverse of max speed-up).
    /// </summary>
    public float MinStretchRatio => 1f / MaxStretchRatio;

    /// <summary>
    /// Gets or sets the FFT size for phase vocoder processing.
    /// Larger values provide better quality but higher latency.
    /// </summary>
    public int FftSize
    {
        get => _fftSize;
        set => _fftSize = IsPowerOfTwo(value) ? value : 4096;
    }

    /// <summary>
    /// Gets or sets the hop size for phase vocoder processing.
    /// Smaller values provide better time resolution.
    /// </summary>
    public int HopSize
    {
        get => _hopSize;
        set => _hopSize = value > 0 ? value : 1024;
    }

    /// <summary>
    /// Gets or sets the smoothing amount for the alignment curve (0.0 to 1.0).
    /// Higher values produce smoother, more natural-sounding alignment.
    /// </summary>
    public float AlignmentSmoothing { get; set; } = 0.3f;

    /// <summary>
    /// Gets or sets the window size for DTW analysis.
    /// </summary>
    public int AnalysisWindowSize
    {
        get => _dtw.WindowSize;
        set => _dtw.WindowSize = value;
    }

    /// <summary>
    /// Gets or sets the hop size for DTW analysis.
    /// </summary>
    public int AnalysisHopSize
    {
        get => _dtw.HopSize;
        set => _dtw.HopSize = value;
    }

    /// <summary>
    /// Creates a new audio alignment processor.
    /// </summary>
    public AudioAlignment()
    {
    }

    /// <summary>
    /// Creates a new audio alignment processor with specified tightness.
    /// </summary>
    /// <param name="syncTightness">Sync tightness (0.0 to 1.0)</param>
    public AudioAlignment(float syncTightness)
    {
        SyncTightness = Math.Clamp(syncTightness, 0f, 1f);
    }

    /// <summary>
    /// Analyzes the alignment between reference and target audio.
    /// </summary>
    /// <param name="reference">Reference audio samples (mono)</param>
    /// <param name="target">Target audio samples to analyze (mono)</param>
    /// <param name="sampleRate">Sample rate in Hz</param>
    /// <returns>Alignment analysis result</returns>
    public AlignmentResult AnalyzeAlignment(float[] reference, float[] target, int sampleRate)
    {
        if (reference == null || reference.Length == 0)
            throw new ArgumentException("Reference audio cannot be null or empty.", nameof(reference));
        if (target == null || target.Length == 0)
            throw new ArgumentException("Target audio cannot be null or empty.", nameof(target));
        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "Sample rate must be positive.");

        // Configure DTW
        _dtw.MaxWarpRatio = MaxStretchRatio;

        // Perform DTW alignment
        var result = _dtw.Align(reference, target, sampleRate);

        // Apply smoothing to alignment points
        if (AlignmentSmoothing > 0 && result.Points.Count > 3)
        {
            SmoothAlignmentPoints(result, AlignmentSmoothing);
        }

        // Apply sync tightness (blend with identity mapping)
        if (SyncTightness < 1.0f)
        {
            BlendWithIdentity(result, 1.0f - SyncTightness);
        }

        // Recalculate statistics after modifications
        result.CalculateStatistics();

        return result;
    }

    /// <summary>
    /// Aligns the target audio to match the reference timing.
    /// </summary>
    /// <param name="target">Target audio samples to warp</param>
    /// <param name="alignment">Previously computed alignment result</param>
    /// <param name="sampleRate">Sample rate in Hz</param>
    /// <returns>Time-warped audio aligned to reference</returns>
    public float[] AlignAudio(float[] target, AlignmentResult alignment, int sampleRate)
    {
        if (target == null || target.Length == 0)
            throw new ArgumentException("Target audio cannot be null or empty.", nameof(target));
        if (alignment == null)
            throw new ArgumentNullException(nameof(alignment));
        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "Sample rate must be positive.");

        // Clamp stretch ratios in alignment
        var clampedAlignment = ClampStretchRatios(alignment);

        // Apply phase vocoder time warping
        float[] output = ApplyTimeWarp(target, clampedAlignment, sampleRate);

        return output;
    }

    /// <summary>
    /// Analyzes and aligns target audio to reference in one step.
    /// </summary>
    /// <param name="reference">Reference audio samples</param>
    /// <param name="target">Target audio samples</param>
    /// <param name="output">Output buffer for aligned audio (must match reference length)</param>
    /// <param name="sampleRate">Sample rate in Hz</param>
    public void AlignToReference(float[] reference, float[] target, float[] output, int sampleRate)
    {
        if (output == null)
            throw new ArgumentNullException(nameof(output));
        if (output.Length < reference.Length)
            throw new ArgumentException("Output buffer must be at least as long as reference.", nameof(output));

        // Analyze alignment
        var alignment = AnalyzeAlignment(reference, target, sampleRate);

        // Apply alignment
        float[] aligned = AlignAudio(target, alignment, sampleRate);

        // Copy to output, trimming or padding as needed
        int copyLength = Math.Min(aligned.Length, output.Length);
        Array.Copy(aligned, output, copyLength);

        // Zero-pad if output is longer
        if (output.Length > aligned.Length)
        {
            Array.Clear(output, aligned.Length, output.Length - aligned.Length);
        }
    }

    /// <summary>
    /// Gets an alignment preview showing time offsets without applying warping.
    /// </summary>
    /// <param name="reference">Reference audio</param>
    /// <param name="target">Target audio</param>
    /// <param name="sampleRate">Sample rate</param>
    /// <returns>Alignment result for preview</returns>
    public AlignmentResult GetAlignmentPreview(float[] reference, float[] target, int sampleRate)
    {
        return AnalyzeAlignment(reference, target, sampleRate);
    }

    /// <summary>
    /// Applies time warping using phase vocoder.
    /// </summary>
    private float[] ApplyTimeWarp(float[] input, AlignmentResult alignment, int sampleRate)
    {
        if (alignment.WarpPath.Length == 0)
            return (float[])input.Clone();

        int outputLength = alignment.WarpPath.Length;
        int halfSize = _fftSize / 2;

        // Prepare output buffer
        float[] output = new float[outputLength];
        float[] windowSum = new float[outputLength];

        // Create Hann window
        float[] window = new float[_fftSize];
        for (int i = 0; i < _fftSize; i++)
        {
            window[i] = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (_fftSize - 1)));
        }

        // Phase vocoder buffers
        Complex[] fftBuffer = new Complex[_fftSize];
        float[] lastInputPhase = new float[halfSize + 1];
        float[] accumulatedPhase = new float[halfSize + 1];

        // Process frame by frame
        int analysisHop = _hopSize;
        float expectedPhaseDiff = 2f * MathF.PI * analysisHop / _fftSize;

        int outputPos = 0;
        double inputPos = 0;
        double lastInputPos = 0;

        while (outputPos < outputLength - _fftSize)
        {
            // Calculate input position from warp path
            if (outputPos < alignment.WarpPath.Length)
            {
                inputPos = alignment.WarpPath[outputPos];
            }
            else
            {
                // Extrapolate
                inputPos += analysisHop;
            }

            // Calculate local stretch ratio
            float stretchRatio = (float)((inputPos - lastInputPos) / analysisHop);
            stretchRatio = Math.Clamp(stretchRatio, MinStretchRatio, MaxStretchRatio);

            int inputFrameStart = (int)inputPos;

            // Check bounds
            if (inputFrameStart < 0 || inputFrameStart + _fftSize > input.Length)
            {
                outputPos += analysisHop;
                lastInputPos = inputPos;
                continue;
            }

            // Apply window and prepare FFT
            for (int i = 0; i < _fftSize; i++)
            {
                int idx = inputFrameStart + i;
                float sample = idx >= 0 && idx < input.Length ? input[idx] : 0f;
                fftBuffer[i] = new Complex(sample * window[i], 0f);
            }

            // Forward FFT
            FFT(fftBuffer, false);

            // Phase vocoder processing
            float[] magnitude = new float[halfSize + 1];
            float[] trueFreq = new float[halfSize + 1];

            for (int k = 0; k <= halfSize; k++)
            {
                float real = fftBuffer[k].Real;
                float imag = fftBuffer[k].Imag;

                magnitude[k] = MathF.Sqrt(real * real + imag * imag);
                float phase = MathF.Atan2(imag, real);

                // Phase difference
                float phaseDiff = phase - lastInputPhase[k];
                lastInputPhase[k] = phase;

                phaseDiff -= k * expectedPhaseDiff;
                phaseDiff = WrapPhase(phaseDiff);

                // True frequency
                float deviation = phaseDiff / (2f * MathF.PI) * _fftSize / analysisHop;
                trueFreq[k] = k + deviation;
            }

            // Synthesis hop (scaled by stretch ratio)
            int synthesisHop = (int)(analysisHop / stretchRatio);
            synthesisHop = Math.Max(1, synthesisHop);
            float synthExpectedPhase = 2f * MathF.PI * synthesisHop / _fftSize;

            // Accumulate phase and reconstruct
            for (int k = 0; k <= halfSize; k++)
            {
                float phaseDelta = trueFreq[k] * synthExpectedPhase;
                accumulatedPhase[k] += phaseDelta;
                accumulatedPhase[k] = WrapPhase(accumulatedPhase[k]);

                float mag = magnitude[k];
                float ph = accumulatedPhase[k];

                fftBuffer[k] = new Complex(mag * MathF.Cos(ph), mag * MathF.Sin(ph));

                if (k > 0 && k < halfSize)
                {
                    fftBuffer[_fftSize - k] = new Complex(mag * MathF.Cos(ph), -mag * MathF.Sin(ph));
                }
            }

            // Inverse FFT
            FFT(fftBuffer, true);

            // Overlap-add
            float normFactor = 1f / (4f * 0.5f); // Normalization for 4x overlap with Hann window
            for (int i = 0; i < _fftSize; i++)
            {
                int outIdx = outputPos + i;
                if (outIdx >= 0 && outIdx < outputLength)
                {
                    output[outIdx] += fftBuffer[i].Real * window[i] * normFactor;
                    windowSum[outIdx] += window[i] * window[i];
                }
            }

            outputPos += analysisHop;
            lastInputPos = inputPos;
        }

        // Normalize by window sum
        for (int i = 0; i < outputLength; i++)
        {
            if (windowSum[i] > 1e-6f)
            {
                output[i] /= windowSum[i];
            }
        }

        return output;
    }

    /// <summary>
    /// Smooths alignment points using a moving average filter.
    /// </summary>
    private void SmoothAlignmentPoints(AlignmentResult result, float amount)
    {
        int windowSize = Math.Max(3, (int)(result.Points.Count * amount * 0.1f));
        if (windowSize % 2 == 0) windowSize++; // Make odd

        var smoothedOffsets = new double[result.Points.Count];

        for (int i = 0; i < result.Points.Count; i++)
        {
            double sum = 0;
            int count = 0;

            for (int j = -windowSize / 2; j <= windowSize / 2; j++)
            {
                int idx = i + j;
                if (idx >= 0 && idx < result.Points.Count)
                {
                    sum += result.Points[idx].TimeOffset;
                    count++;
                }
            }

            smoothedOffsets[i] = count > 0 ? sum / count : result.Points[i].TimeOffset;
        }

        // Apply smoothed offsets
        for (int i = 0; i < result.Points.Count; i++)
        {
            result.Points[i].TargetTime = result.Points[i].SourceTime + smoothedOffsets[i];
        }
    }

    /// <summary>
    /// Blends alignment with identity mapping for looser sync.
    /// </summary>
    private void BlendWithIdentity(AlignmentResult result, float blendAmount)
    {
        foreach (var point in result.Points)
        {
            double identityTarget = point.SourceTime; // Identity = no change
            point.TargetTime = point.TargetTime * (1f - blendAmount) + identityTarget * blendAmount;
        }

        // Regenerate warp path
        if (result.WarpPath.Length > 0)
        {
            for (int i = 0; i < result.WarpPath.Length; i++)
            {
                double identity = i;
                result.WarpPath[i] = result.WarpPath[i] * (1f - blendAmount) + identity * blendAmount;
            }
        }
    }

    /// <summary>
    /// Clamps stretch ratios to prevent excessive warping.
    /// </summary>
    private AlignmentResult ClampStretchRatios(AlignmentResult alignment)
    {
        var result = alignment.Clone();

        for (int i = 0; i < result.Points.Count; i++)
        {
            result.Points[i].LocalStretchRatio = Math.Clamp(
                result.Points[i].LocalStretchRatio,
                MinStretchRatio,
                MaxStretchRatio
            );
        }

        return result;
    }

    private static float WrapPhase(float phase)
    {
        while (phase > MathF.PI) phase -= 2f * MathF.PI;
        while (phase < -MathF.PI) phase += 2f * MathF.PI;
        return phase;
    }

    private static bool IsPowerOfTwo(int n) => n > 0 && (n & (n - 1)) == 0;

    private static void FFT(Complex[] data, bool inverse)
    {
        int n = data.Length;
        if (n <= 1) return;

        // Bit-reversal permutation
        int j = 0;
        for (int i = 0; i < n - 1; i++)
        {
            if (i < j)
            {
                (data[i], data[j]) = (data[j], data[i]);
            }
            int m = n >> 1;
            while (j >= m && m >= 1)
            {
                j -= m;
                m >>= 1;
            }
            j += m;
        }

        // Cooley-Tukey iterative FFT
        float direction = inverse ? 1f : -1f;
        for (int len = 2; len <= n; len <<= 1)
        {
            float theta = direction * 2f * MathF.PI / len;
            Complex wn = new Complex(MathF.Cos(theta), MathF.Sin(theta));

            for (int i = 0; i < n; i += len)
            {
                Complex w = new Complex(1f, 0f);
                int halfLen = len / 2;
                for (int k = 0; k < halfLen; k++)
                {
                    Complex t = w * data[i + k + halfLen];
                    Complex u = data[i + k];
                    data[i + k] = u + t;
                    data[i + k + halfLen] = u - t;
                    w = w * wn;
                }
            }
        }

        if (inverse)
        {
            for (int i = 0; i < n; i++)
            {
                data[i] = new Complex(data[i].Real / n, data[i].Imag / n);
            }
        }
    }

    private readonly struct Complex
    {
        public readonly float Real;
        public readonly float Imag;

        public Complex(float real, float imag)
        {
            Real = real;
            Imag = imag;
        }

        public static Complex operator +(Complex a, Complex b)
            => new Complex(a.Real + b.Real, a.Imag + b.Imag);

        public static Complex operator -(Complex a, Complex b)
            => new Complex(a.Real - b.Real, a.Imag - b.Imag);

        public static Complex operator *(Complex a, Complex b)
            => new Complex(a.Real * b.Real - a.Imag * b.Imag, a.Real * b.Imag + a.Imag * b.Real);
    }
}
