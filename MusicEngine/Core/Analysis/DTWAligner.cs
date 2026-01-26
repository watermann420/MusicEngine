//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Dynamic Time Warping algorithm for audio alignment, supporting feature extraction and path finding.

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Implements Dynamic Time Warping (DTW) algorithm for aligning two audio signals.
/// DTW finds the optimal alignment path between two sequences while allowing for
/// non-linear time warping within constraints.
/// </summary>
/// <remarks>
/// The algorithm uses chroma features (pitch class profiles) for alignment,
/// which makes it robust to timbral differences between performances.
/// Includes Sakoe-Chiba band constraint to limit warp path deviation.
/// </remarks>
public class DTWAligner
{
    private const int ChromaBins = 12;  // Pitch classes (C, C#, D, ... B)

    /// <summary>
    /// Gets or sets the FFT window size for feature extraction.
    /// </summary>
    public int WindowSize { get; set; } = 4096;

    /// <summary>
    /// Gets or sets the hop size for feature extraction in samples.
    /// </summary>
    public int HopSize { get; set; } = 512;

    /// <summary>
    /// Gets or sets the maximum warp ratio (constraint on path deviation).
    /// A value of 2.0 means the path cannot deviate more than 2x from the diagonal.
    /// </summary>
    public float MaxWarpRatio { get; set; } = 2.0f;

    /// <summary>
    /// Gets or sets whether to use the Sakoe-Chiba band constraint.
    /// </summary>
    public bool UseSakoeChibaBand { get; set; } = true;

    /// <summary>
    /// Gets or sets the reference frequency for chroma feature extraction.
    /// </summary>
    public float ReferenceFrequency { get; set; } = 440f;

    /// <summary>
    /// Gets or sets whether to normalize features before DTW.
    /// </summary>
    public bool NormalizeFeatures { get; set; } = true;

    /// <summary>
    /// Creates a new DTW aligner with default settings.
    /// </summary>
    public DTWAligner()
    {
    }

    /// <summary>
    /// Creates a new DTW aligner with specified settings.
    /// </summary>
    /// <param name="windowSize">FFT window size</param>
    /// <param name="hopSize">Hop size in samples</param>
    public DTWAligner(int windowSize, int hopSize)
    {
        WindowSize = windowSize;
        HopSize = hopSize;
    }

    /// <summary>
    /// Aligns the target audio to the reference audio using DTW.
    /// </summary>
    /// <param name="reference">Reference audio samples</param>
    /// <param name="target">Target audio samples to align</param>
    /// <param name="sampleRate">Sample rate in Hz</param>
    /// <returns>Alignment result with time mappings</returns>
    public AlignmentResult Align(float[] reference, float[] target, int sampleRate)
    {
        if (reference == null || reference.Length == 0)
            throw new ArgumentException("Reference audio cannot be null or empty.", nameof(reference));
        if (target == null || target.Length == 0)
            throw new ArgumentException("Target audio cannot be null or empty.", nameof(target));
        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "Sample rate must be positive.");

        // Extract chroma features from both signals
        var refFeatures = ExtractChromaFeatures(reference, sampleRate);
        var tgtFeatures = ExtractChromaFeatures(target, sampleRate);

        // Compute DTW cost matrix
        var costMatrix = ComputeDTWMatrix(refFeatures, tgtFeatures);

        // Backtrack to find optimal path
        var alignmentPoints = Backtrack(costMatrix, refFeatures.GetLength(0), tgtFeatures.GetLength(0));

        // Convert frame indices to time
        double refDuration = (double)reference.Length / sampleRate;
        double tgtDuration = (double)target.Length / sampleRate;
        double frameToTime = (double)HopSize / sampleRate;

        var result = new AlignmentResult
        {
            SampleRate = sampleRate,
            ReferenceDuration = refDuration,
            TargetDuration = tgtDuration,
            DTWCost = costMatrix[refFeatures.GetLength(0) - 1, tgtFeatures.GetLength(0) - 1]
        };

        // Convert frame indices to alignment points
        foreach (var point in alignmentPoints)
        {
            point.SourceTime = point.SourceTime * frameToTime;
            point.TargetTime = point.TargetTime * frameToTime;
            result.Points.Add(point);
        }

        // Calculate warp path for sample-level alignment
        result.WarpPath = GenerateWarpPath(result, reference.Length, target.Length, sampleRate);

        // Calculate statistics and confidence
        result.CalculateStatistics();
        result.OverallConfidence = CalculateConfidence(result, costMatrix);

        return result;
    }

    /// <summary>
    /// Applies alignment to the target audio using the computed result.
    /// </summary>
    /// <param name="audio">Target audio to warp</param>
    /// <param name="result">Alignment result from previous Align call</param>
    /// <param name="sampleRate">Sample rate in Hz</param>
    /// <returns>Time-warped audio aligned to reference</returns>
    public float[] ApplyAlignment(float[] audio, AlignmentResult result, int sampleRate)
    {
        if (audio == null || audio.Length == 0)
            throw new ArgumentException("Audio cannot be null or empty.", nameof(audio));
        if (result == null || result.WarpPath.Length == 0)
            throw new ArgumentException("Invalid alignment result.", nameof(result));

        int outputLength = result.WarpPath.Length;
        float[] output = new float[outputLength];

        // Apply warp path with linear interpolation
        for (int i = 0; i < outputLength; i++)
        {
            double sourcePos = result.WarpPath[i];
            int sourceIndex = (int)sourcePos;
            float fraction = (float)(sourcePos - sourceIndex);

            if (sourceIndex >= 0 && sourceIndex < audio.Length - 1)
            {
                // Linear interpolation
                output[i] = audio[sourceIndex] * (1f - fraction) + audio[sourceIndex + 1] * fraction;
            }
            else if (sourceIndex >= 0 && sourceIndex < audio.Length)
            {
                output[i] = audio[sourceIndex];
            }
        }

        return output;
    }

    /// <summary>
    /// Computes the DTW cost matrix between two feature sequences.
    /// </summary>
    public float[,] ComputeDTWMatrix(float[,] features1, float[,] features2)
    {
        int n = features1.GetLength(0);
        int m = features2.GetLength(0);

        // Cost matrix with accumulated costs
        float[,] cost = new float[n, m];

        // Initialize with infinity
        for (int i = 0; i < n; i++)
            for (int j = 0; j < m; j++)
                cost[i, j] = float.MaxValue;

        // Initialize first cell
        cost[0, 0] = ComputeDistance(features1, features2, 0, 0);

        // Sakoe-Chiba band width
        int bandWidth = UseSakoeChibaBand ? (int)(Math.Max(n, m) / MaxWarpRatio) : Math.Max(n, m);

        // Fill first row
        for (int j = 1; j < m; j++)
        {
            if (Math.Abs(j * n / m) <= bandWidth)
            {
                cost[0, j] = cost[0, j - 1] + ComputeDistance(features1, features2, 0, j);
            }
        }

        // Fill first column
        for (int i = 1; i < n; i++)
        {
            if (Math.Abs(i * m / n) <= bandWidth)
            {
                cost[i, 0] = cost[i - 1, 0] + ComputeDistance(features1, features2, i, 0);
            }
        }

        // Fill the rest of the matrix
        for (int i = 1; i < n; i++)
        {
            for (int j = 1; j < m; j++)
            {
                // Check Sakoe-Chiba band constraint
                if (UseSakoeChibaBand)
                {
                    float expectedJ = (float)i * m / n;
                    if (Math.Abs(j - expectedJ) > bandWidth)
                        continue;
                }

                float localCost = ComputeDistance(features1, features2, i, j);

                // Standard DTW recursion: min of three predecessors
                float min = float.MaxValue;
                if (cost[i - 1, j] < min) min = cost[i - 1, j];
                if (cost[i, j - 1] < min) min = cost[i, j - 1];
                if (cost[i - 1, j - 1] < min) min = cost[i - 1, j - 1];

                cost[i, j] = localCost + min;
            }
        }

        return cost;
    }

    /// <summary>
    /// Backtracks through the cost matrix to find the optimal alignment path.
    /// </summary>
    public List<AlignmentPoint> Backtrack(float[,] matrix)
    {
        return Backtrack(matrix, matrix.GetLength(0), matrix.GetLength(1));
    }

    private List<AlignmentPoint> Backtrack(float[,] matrix, int n, int m)
    {
        var path = new List<AlignmentPoint>();

        int i = n - 1;
        int j = m - 1;

        // Start from the end
        path.Add(new AlignmentPoint(i, j, 1.0f));

        while (i > 0 || j > 0)
        {
            if (i == 0)
            {
                j--;
            }
            else if (j == 0)
            {
                i--;
            }
            else
            {
                // Find minimum predecessor
                float diag = matrix[i - 1, j - 1];
                float left = matrix[i, j - 1];
                float up = matrix[i - 1, j];

                float min = Math.Min(diag, Math.Min(left, up));

                if (min == diag || (min != left && min != up))
                {
                    i--;
                    j--;
                }
                else if (min == left)
                {
                    j--;
                }
                else
                {
                    i--;
                }
            }

            // Calculate confidence based on local cost
            float localCost = matrix[i, j];
            float confidence = 1.0f / (1.0f + localCost * 0.1f);

            path.Add(new AlignmentPoint(i, j, Math.Clamp(confidence, 0f, 1f)));
        }

        // Reverse to get forward order
        path.Reverse();

        return path;
    }

    /// <summary>
    /// Extracts chroma features (pitch class profiles) from audio.
    /// </summary>
    private float[,] ExtractChromaFeatures(float[] audio, int sampleRate)
    {
        int numFrames = (audio.Length - WindowSize) / HopSize + 1;
        if (numFrames <= 0) numFrames = 1;

        float[,] chroma = new float[numFrames, ChromaBins];
        float[] window = CreateHannWindow(WindowSize);
        Complex[] fftBuffer = new Complex[WindowSize];

        for (int frame = 0; frame < numFrames; frame++)
        {
            int offset = frame * HopSize;

            // Window and prepare FFT input
            for (int i = 0; i < WindowSize; i++)
            {
                int sampleIdx = offset + i;
                float sample = sampleIdx < audio.Length ? audio[sampleIdx] : 0f;
                fftBuffer[i] = new Complex(sample * window[i], 0f);
            }

            // FFT
            FFT(fftBuffer, false);

            // Convert to chroma
            ExtractChromaFromSpectrum(fftBuffer, chroma, frame, sampleRate);
        }

        // Normalize features if requested
        if (NormalizeFeatures)
        {
            NormalizeChromaFeatures(chroma);
        }

        return chroma;
    }

    private void ExtractChromaFromSpectrum(Complex[] spectrum, float[,] chroma, int frame, int sampleRate)
    {
        int halfSize = WindowSize / 2;
        float freqResolution = (float)sampleRate / WindowSize;

        // Clear chroma bins for this frame
        for (int c = 0; c < ChromaBins; c++)
        {
            chroma[frame, c] = 0f;
        }

        // Map each FFT bin to its chroma bin
        for (int k = 1; k < halfSize; k++)
        {
            float frequency = k * freqResolution;
            if (frequency < 20 || frequency > 8000) continue; // Focus on musical range

            // Calculate pitch class from frequency
            float pitch = 12f * MathF.Log2(frequency / ReferenceFrequency) + 69f; // MIDI pitch
            int chromaBin = ((int)MathF.Round(pitch) % 12 + 12) % 12; // Wrap to 0-11

            // Get magnitude
            float magnitude = MathF.Sqrt(spectrum[k].Real * spectrum[k].Real + spectrum[k].Imag * spectrum[k].Imag);

            // Accumulate into chroma bin
            chroma[frame, chromaBin] += magnitude * magnitude; // Energy
        }

        // Take square root (RMS-like)
        for (int c = 0; c < ChromaBins; c++)
        {
            chroma[frame, c] = MathF.Sqrt(chroma[frame, c]);
        }
    }

    private void NormalizeChromaFeatures(float[,] chroma)
    {
        int numFrames = chroma.GetLength(0);

        for (int frame = 0; frame < numFrames; frame++)
        {
            // L2 normalization
            float sum = 0f;
            for (int c = 0; c < ChromaBins; c++)
            {
                sum += chroma[frame, c] * chroma[frame, c];
            }

            float norm = MathF.Sqrt(sum);
            if (norm > 1e-6f)
            {
                for (int c = 0; c < ChromaBins; c++)
                {
                    chroma[frame, c] /= norm;
                }
            }
        }
    }

    private float ComputeDistance(float[,] features1, float[,] features2, int i, int j)
    {
        // Cosine distance
        float dot = 0f;
        float norm1 = 0f;
        float norm2 = 0f;

        for (int c = 0; c < ChromaBins; c++)
        {
            dot += features1[i, c] * features2[j, c];
            norm1 += features1[i, c] * features1[i, c];
            norm2 += features2[j, c] * features2[j, c];
        }

        float normProduct = MathF.Sqrt(norm1 * norm2);
        if (normProduct < 1e-6f)
            return 1f;

        float cosineSimilarity = dot / normProduct;
        return 1f - cosineSimilarity; // Convert to distance
    }

    private double[] GenerateWarpPath(AlignmentResult result, int refLength, int tgtLength, int sampleRate)
    {
        // Generate sample-level warp path from alignment points
        double[] warpPath = new double[refLength];

        if (result.Points.Count < 2)
        {
            // No alignment - identity mapping
            for (int i = 0; i < refLength; i++)
            {
                warpPath[i] = (double)i * tgtLength / refLength;
            }
            return warpPath;
        }

        // Convert alignment points to sample indices
        var samplePoints = new List<(int refSample, int tgtSample)>();
        foreach (var point in result.Points)
        {
            int refSample = (int)(point.SourceTime * sampleRate);
            int tgtSample = (int)(point.TargetTime * sampleRate);
            refSample = Math.Clamp(refSample, 0, refLength - 1);
            tgtSample = Math.Clamp(tgtSample, 0, tgtLength - 1);
            samplePoints.Add((refSample, tgtSample));
        }

        // Interpolate between alignment points
        int pointIdx = 0;
        for (int i = 0; i < refLength; i++)
        {
            // Find surrounding points
            while (pointIdx < samplePoints.Count - 1 && samplePoints[pointIdx + 1].refSample < i)
            {
                pointIdx++;
            }

            if (pointIdx >= samplePoints.Count - 1)
            {
                // Extrapolate from last segment
                var last = samplePoints[^1];
                var prev = samplePoints.Count > 1 ? samplePoints[^2] : samplePoints[^1];
                int refDelta = last.refSample - prev.refSample;
                int tgtDelta = last.tgtSample - prev.tgtSample;
                double slope = refDelta > 0 ? (double)tgtDelta / refDelta : 1.0;
                warpPath[i] = last.tgtSample + (i - last.refSample) * slope;
            }
            else
            {
                // Interpolate
                var lower = samplePoints[pointIdx];
                var upper = samplePoints[pointIdx + 1];
                int refDelta = upper.refSample - lower.refSample;

                if (refDelta > 0)
                {
                    double t = (double)(i - lower.refSample) / refDelta;
                    warpPath[i] = lower.tgtSample + t * (upper.tgtSample - lower.tgtSample);
                }
                else
                {
                    warpPath[i] = lower.tgtSample;
                }
            }

            // Clamp to valid range
            warpPath[i] = Math.Clamp(warpPath[i], 0, tgtLength - 1);
        }

        return warpPath;
    }

    private float CalculateConfidence(AlignmentResult result, float[,] costMatrix)
    {
        // Confidence based on DTW cost and path consistency
        float totalCost = result.DTWCost;
        int pathLength = result.Points.Count;

        // Normalize cost by path length
        float normalizedCost = pathLength > 0 ? totalCost / pathLength : totalCost;

        // Convert to confidence (lower cost = higher confidence)
        float confidence = 1f / (1f + normalizedCost);

        // Penalize if max deviation is too high
        if (result.MaxDeviation > 0.5) // More than 500ms deviation
        {
            confidence *= 0.8f;
        }

        return Math.Clamp(confidence, 0f, 1f);
    }

    private static float[] CreateHannWindow(int size)
    {
        float[] window = new float[size];
        for (int i = 0; i < size; i++)
        {
            window[i] = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (size - 1)));
        }
        return window;
    }

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
