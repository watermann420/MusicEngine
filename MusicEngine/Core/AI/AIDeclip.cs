//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: ML-inspired clipping restoration using spline interpolation, harmonic prediction,
//              and waveform continuation algorithms.

using NAudio.Wave;

namespace MusicEngine.Core.AI;

/// <summary>
/// Reconstruction method for clipped audio restoration.
/// </summary>
public enum AIDeclipMethod
{
    /// <summary>
    /// Cubic spline interpolation using surrounding samples.
    /// </summary>
    Spline,

    /// <summary>
    /// Harmonic prediction using spectral analysis.
    /// </summary>
    Harmonic,

    /// <summary>
    /// Waveform continuation using pattern matching.
    /// </summary>
    WaveformContinuation,

    /// <summary>
    /// Hybrid approach combining all methods.
    /// </summary>
    Hybrid
}

/// <summary>
/// ML-inspired audio clipping restoration effect.
/// Detects clipped regions and reconstructs them using spline interpolation,
/// harmonic prediction, and waveform continuation algorithms.
/// </summary>
/// <remarks>
/// The algorithm works in several stages:
/// 1. Detection: Identifies clipped regions based on threshold and pattern analysis
/// 2. Analysis: Examines surrounding waveform characteristics
/// 3. Reconstruction: Uses one of several methods:
///    - Spline interpolation for smooth curve fitting
///    - Harmonic prediction using spectral analysis
///    - Waveform continuation using autocorrelation-based pattern matching
/// 4. Blending: Smoothly blends reconstructed audio with original
/// </remarks>
public class AIDeclip : EffectBase
{
    // Processing buffers (per channel)
    private CircularBuffer[] _inputBuffers = null!;
    private CircularBuffer[] _outputBuffers = null!;

    // Clipping detection state (per channel)
    private bool[] _inClipRegion = null!;
    private int[] _clipStartIndex = null!;
    private int[] _clipLength = null!;
    private float[] _clipPeakSign = null!;

    // Pattern analysis buffers
    private float[][] _patternBuffer = null!;
    private float[][] _harmonicBuffer = null!;

    // Configuration
    private const int BufferSize = 8192;
    private const int ContextSamples = 16; // Samples for context analysis
    private const int MaxClipLength = 512; // Maximum clip length to process
    private const int PatternWindowSize = 256; // Window for pattern matching

    // State
    private bool _initialized;
    private int _latencySamples;
    private long _processedSamples;
    private bool _previewMode;

    // Preview data
    private int _clipsDetected;
    private float _maxClipLevel;

    /// <summary>
    /// Creates a new AI declipping effect.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    public AIDeclip(ISampleProvider source) : this(source, "AI Declip")
    {
    }

    /// <summary>
    /// Creates a new AI declipping effect with a custom name.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <param name="name">Effect name.</param>
    public AIDeclip(ISampleProvider source, string name) : base(source, name)
    {
        RegisterParameter("Threshold", 0.95f);        // 0.7-1.0: Detection threshold
        RegisterParameter("Reconstruction", 0.8f);     // 0.0-1.0: Reconstruction amount
        RegisterParameter("Method", 3f);               // 0=Spline, 1=Harmonic, 2=WaveformCont, 3=Hybrid
        RegisterParameter("Sensitivity", 0.5f);        // 0.0-1.0: Detection sensitivity
        RegisterParameter("HarmonicOrder", 8f);        // 2-16: Harmonics to consider
        RegisterParameter("PatternLength", 0.5f);      // 0.0-1.0: Pattern match window length
        RegisterParameter("Mix", 1f);

        _initialized = false;
        _previewMode = false;
    }

    /// <summary>
    /// Detection threshold for clipped samples (0.7 - 1.0).
    /// </summary>
    public float Threshold
    {
        get => GetParameter("Threshold");
        set => SetParameter("Threshold", Math.Clamp(value, 0.7f, 1.0f));
    }

    /// <summary>
    /// Reconstruction amount (0.0 - 1.0).
    /// Controls how much of the reconstructed signal replaces the clipped signal.
    /// </summary>
    public float Reconstruction
    {
        get => GetParameter("Reconstruction");
        set => SetParameter("Reconstruction", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Reconstruction method to use.
    /// </summary>
    public AIDeclipMethod Method
    {
        get => (AIDeclipMethod)(int)GetParameter("Method");
        set => SetParameter("Method", (float)value);
    }

    /// <summary>
    /// Detection sensitivity (0.0 - 1.0).
    /// Higher values detect more subtle clipping.
    /// </summary>
    public float Sensitivity
    {
        get => GetParameter("Sensitivity");
        set => SetParameter("Sensitivity", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Number of harmonics to consider for harmonic prediction (2 - 16).
    /// </summary>
    public int HarmonicOrder
    {
        get => (int)GetParameter("HarmonicOrder");
        set => SetParameter("HarmonicOrder", Math.Clamp(value, 2, 16));
    }

    /// <summary>
    /// Pattern length for waveform continuation (0.0 - 1.0).
    /// </summary>
    public float PatternLength
    {
        get => GetParameter("PatternLength");
        set => SetParameter("PatternLength", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Gets or sets whether preview mode is enabled.
    /// In preview mode, statistics are collected but audio is passed through unchanged.
    /// </summary>
    public bool PreviewMode
    {
        get => _previewMode;
        set => _previewMode = value;
    }

    /// <summary>
    /// Gets the number of clips detected (in preview mode).
    /// </summary>
    public int ClipsDetected => _clipsDetected;

    /// <summary>
    /// Gets the maximum detected clip level (in preview mode).
    /// </summary>
    public float MaxClipLevel => _maxClipLevel;

    /// <summary>
    /// Resets the preview statistics.
    /// </summary>
    public void ResetStatistics()
    {
        _clipsDetected = 0;
        _maxClipLevel = 0;
    }

    private void Initialize()
    {
        int channels = Channels;

        _inputBuffers = new CircularBuffer[channels];
        _outputBuffers = new CircularBuffer[channels];
        _inClipRegion = new bool[channels];
        _clipStartIndex = new int[channels];
        _clipLength = new int[channels];
        _clipPeakSign = new float[channels];
        _patternBuffer = new float[channels][];
        _harmonicBuffer = new float[channels][];

        for (int ch = 0; ch < channels; ch++)
        {
            _inputBuffers[ch] = new CircularBuffer(BufferSize);
            _outputBuffers[ch] = new CircularBuffer(BufferSize);
            _patternBuffer[ch] = new float[PatternWindowSize];
            _harmonicBuffer[ch] = new float[MaxClipLength];
            _inClipRegion[ch] = false;
            _clipStartIndex[ch] = -1;
            _clipLength[ch] = 0;
            _clipPeakSign[ch] = 1f;
        }

        _latencySamples = ContextSamples + 16;
        _processedSamples = 0;
        _clipsDetected = 0;
        _maxClipLevel = 0;
        _initialized = true;
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        if (!_initialized)
        {
            Initialize();
        }

        int channels = Channels;
        float threshold = Threshold;
        float sensitivity = Sensitivity;
        float reconstruction = Reconstruction;
        var method = Method;

        // Effective threshold based on sensitivity
        float effectiveThreshold = threshold - sensitivity * 0.05f;
        float clipTolerance = 0.01f + sensitivity * 0.02f;

        for (int i = 0; i < count; i += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                float input = sourceBuffer[i + ch];
                _inputBuffers[ch].Write(input);

                // Detect clipping
                bool isClipped = IsClippedSample(input, effectiveThreshold, clipTolerance);

                if (isClipped)
                {
                    if (!_inClipRegion[ch])
                    {
                        // Start of new clip region
                        _inClipRegion[ch] = true;
                        _clipStartIndex[ch] = _inputBuffers[ch].WritePosition;
                        _clipLength[ch] = 1;
                        _clipPeakSign[ch] = MathF.Sign(input);
                        _maxClipLevel = MathF.Max(_maxClipLevel, MathF.Abs(input));
                    }
                    else
                    {
                        _clipLength[ch]++;
                    }
                }
                else
                {
                    if (_inClipRegion[ch])
                    {
                        // End of clip region - process it
                        _clipsDetected++;

                        if (!_previewMode && _clipLength[ch] <= MaxClipLength)
                        {
                            ProcessClippedRegion(ch, method, reconstruction);
                        }

                        _inClipRegion[ch] = false;
                        _clipStartIndex[ch] = -1;
                        _clipLength[ch] = 0;
                    }
                }

                // Read output with latency
                float output;
                if (_processedSamples >= _latencySamples)
                {
                    output = _outputBuffers[ch].Read(_latencySamples);
                    // If no reconstruction was written, use input
                    if (output == 0f)
                    {
                        output = _inputBuffers[ch].Read(_latencySamples);
                    }
                }
                else
                {
                    output = 0f;
                }

                destBuffer[offset + i + ch] = output;
            }

            _processedSamples++;
        }
    }

    private static bool IsClippedSample(float sample, float threshold, float tolerance)
    {
        float absSample = MathF.Abs(sample);
        return absSample >= threshold - tolerance;
    }

    private void ProcessClippedRegion(int channel, AIDeclipMethod method, float reconstruction)
    {
        if (_clipLength[channel] < 1)
            return;

        int margin = 4;
        int startIdx = _clipStartIndex[channel] - margin;
        int endIdx = _clipStartIndex[channel] + _clipLength[channel] + margin;
        int regionLength = endIdx - startIdx;

        if (regionLength <= 0 || regionLength > BufferSize / 2)
            return;

        // Collect context samples
        float[] beforeSamples = new float[ContextSamples];
        float[] afterSamples = new float[ContextSamples];
        float[] clippedSamples = new float[regionLength];

        for (int i = 0; i < ContextSamples; i++)
        {
            beforeSamples[i] = _inputBuffers[channel].ReadAt(startIdx - ContextSamples + i);
            afterSamples[i] = _inputBuffers[channel].ReadAt(endIdx + i);
        }

        for (int i = 0; i < regionLength; i++)
        {
            clippedSamples[i] = _inputBuffers[channel].ReadAt(startIdx + i);
        }

        // Reconstruct based on method
        float[] reconstructed = method switch
        {
            AIDeclipMethod.Spline => ReconstructWithSpline(beforeSamples, clippedSamples, afterSamples, regionLength),
            AIDeclipMethod.Harmonic => ReconstructWithHarmonic(beforeSamples, clippedSamples, afterSamples, regionLength, channel),
            AIDeclipMethod.WaveformContinuation => ReconstructWithWaveformContinuation(beforeSamples, clippedSamples, afterSamples, regionLength, channel),
            AIDeclipMethod.Hybrid => ReconstructHybrid(beforeSamples, clippedSamples, afterSamples, regionLength, channel),
            _ => ReconstructWithSpline(beforeSamples, clippedSamples, afterSamples, regionLength)
        };

        // Blend and write to output
        for (int i = 0; i < regionLength; i++)
        {
            float original = clippedSamples[i];
            float restored = reconstructed[i];

            // Only blend for actually clipped samples (in the core region)
            float blendAmount = (i >= margin && i < regionLength - margin)
                ? reconstruction
                : reconstruction * 0.5f;

            float output = original * (1f - blendAmount) + restored * blendAmount;
            output = SoftClip(output);

            _outputBuffers[channel].WriteAt(startIdx + i, output);
        }
    }

    /// <summary>
    /// Reconstructs using cubic spline interpolation.
    /// </summary>
    private float[] ReconstructWithSpline(float[] before, float[] clipped, float[] after, int regionLength)
    {
        float[] result = new float[regionLength];

        // Build knot points for spline
        int totalKnots = ContextSamples + 2 + ContextSamples;
        float[] x = new float[totalKnots];
        float[] y = new float[totalKnots];

        // Before context
        for (int i = 0; i < ContextSamples; i++)
        {
            x[i] = i;
            y[i] = before[i];
        }

        // Boundary estimates
        float startValue = before[ContextSamples - 1];
        float endValue = after[0];
        float peakEstimate = EstimatePeak(before, after, clipped, regionLength);

        x[ContextSamples] = ContextSamples;
        y[ContextSamples] = startValue;

        x[ContextSamples + 1] = ContextSamples + regionLength - 1;
        y[ContextSamples + 1] = endValue;

        // After context
        for (int i = 0; i < ContextSamples; i++)
        {
            x[ContextSamples + 2 + i] = ContextSamples + regionLength + i;
            y[ContextSamples + 2 + i] = after[i];
        }

        // Compute spline coefficients
        ComputeCubicSpline(x, y, out float[] a, out float[] b, out float[] c, out float[] d);

        // Evaluate spline for clipped region
        for (int i = 0; i < regionLength; i++)
        {
            float xi = ContextSamples + i;

            // Find correct interval
            int interval = ContextSamples;
            for (int j = 0; j < totalKnots - 1; j++)
            {
                if (xi >= x[j] && xi < x[j + 1])
                {
                    interval = j;
                    break;
                }
            }

            float dx = xi - x[interval];
            float splineValue = a[interval] + b[interval] * dx + c[interval] * dx * dx + d[interval] * dx * dx * dx;

            // Add sinusoidal peak correction for the middle of the region
            float t = regionLength > 1 ? (float)i / (regionLength - 1) : 0.5f;
            float envelope = MathF.Sin(t * MathF.PI);
            float peakCorrection = (peakEstimate - MathF.Abs(splineValue)) * envelope * MathF.Sign(clipped[i]);

            result[i] = splineValue + peakCorrection * 0.5f;
        }

        return result;
    }

    /// <summary>
    /// Reconstructs using harmonic prediction from spectral analysis.
    /// </summary>
    private float[] ReconstructWithHarmonic(float[] before, float[] clipped, float[] after, int regionLength, int channel)
    {
        float[] result = new float[regionLength];
        int harmonicOrder = HarmonicOrder;

        // Estimate fundamental frequency from before context using autocorrelation
        float fundamentalPeriod = EstimatePeriod(before);
        if (fundamentalPeriod < 2) fundamentalPeriod = 8; // Default if detection fails

        // Extract harmonic amplitudes and phases from before context
        float[] amplitudes = new float[harmonicOrder];
        float[] phases = new float[harmonicOrder];

        for (int h = 1; h <= harmonicOrder; h++)
        {
            float freq = 2f * MathF.PI * h / fundamentalPeriod;
            float cosSum = 0, sinSum = 0;

            for (int i = 0; i < before.Length; i++)
            {
                cosSum += before[i] * MathF.Cos(freq * i);
                sinSum += before[i] * MathF.Sin(freq * i);
            }

            amplitudes[h - 1] = 2f * MathF.Sqrt(cosSum * cosSum + sinSum * sinSum) / before.Length;
            phases[h - 1] = MathF.Atan2(sinSum, cosSum);
        }

        // Synthesize reconstruction using harmonics
        float startOffset = ContextSamples;
        for (int i = 0; i < regionLength; i++)
        {
            float sample = 0;
            for (int h = 1; h <= harmonicOrder; h++)
            {
                float freq = 2f * MathF.PI * h / fundamentalPeriod;
                sample += amplitudes[h - 1] * MathF.Cos(freq * (startOffset + i) + phases[h - 1]);
            }
            result[i] = sample;
        }

        // Blend with boundary values for smooth transition
        float startValue = before[ContextSamples - 1];
        float endValue = after[0];

        for (int i = 0; i < regionLength; i++)
        {
            float t = (float)i / regionLength;
            float boundary = startValue * (1f - t) + endValue * t;
            float blend = MathF.Sin(t * MathF.PI);
            result[i] = result[i] * blend + boundary * (1f - blend);
        }

        return result;
    }

    /// <summary>
    /// Reconstructs using waveform continuation via pattern matching.
    /// </summary>
    private float[] ReconstructWithWaveformContinuation(float[] before, float[] clipped, float[] after, int regionLength, int channel)
    {
        float[] result = new float[regionLength];

        // Find best matching pattern from before context
        int patternLength = (int)(PatternLength * PatternWindowSize);
        if (patternLength < 8) patternLength = 8;
        if (patternLength > before.Length) patternLength = before.Length;

        // Get the immediate context before the clip
        float[] pattern = new float[patternLength];
        for (int i = 0; i < patternLength; i++)
        {
            pattern[i] = before[before.Length - patternLength + i];
        }

        // Look for similar patterns earlier in the context
        int searchWindow = before.Length - patternLength;
        if (searchWindow < patternLength) searchWindow = patternLength;

        float bestCorrelation = -1;
        int bestOffset = 0;

        // Search for best matching pattern
        for (int offset = 0; offset < searchWindow - regionLength; offset++)
        {
            float correlation = 0;
            float norm1 = 0, norm2 = 0;

            for (int i = 0; i < patternLength; i++)
            {
                int idx = offset + i;
                if (idx < before.Length)
                {
                    correlation += pattern[i] * before[idx];
                    norm1 += pattern[i] * pattern[i];
                    norm2 += before[idx] * before[idx];
                }
            }

            if (norm1 > 0 && norm2 > 0)
            {
                correlation /= MathF.Sqrt(norm1 * norm2);
                if (correlation > bestCorrelation)
                {
                    bestCorrelation = correlation;
                    bestOffset = offset;
                }
            }
        }

        // Continue the waveform from the best matching point
        float peakSign = MathF.Sign(clipped[regionLength / 2]);
        float peakEstimate = EstimatePeak(before, after, clipped, regionLength);

        for (int i = 0; i < regionLength; i++)
        {
            int sourceIdx = bestOffset + patternLength + i;
            if (sourceIdx < before.Length)
            {
                result[i] = before[sourceIdx];
            }
            else
            {
                // Extrapolate using sinusoidal interpolation
                float t = (float)i / regionLength;
                float envelope = MathF.Sin(t * MathF.PI);
                float baseline = before[before.Length - 1] * (1f - t) + after[0] * t;
                result[i] = baseline + peakEstimate * envelope * peakSign * 0.5f;
            }
        }

        // Smooth transitions at boundaries
        int fadeLen = Math.Min(4, regionLength / 4);
        for (int i = 0; i < fadeLen; i++)
        {
            float fade = (float)i / fadeLen;
            result[i] = result[i] * fade + before[before.Length - 1] * (1f - fade);
        }
        for (int i = 0; i < fadeLen; i++)
        {
            float fade = (float)i / fadeLen;
            int idx = regionLength - fadeLen + i;
            result[idx] = result[idx] * (1f - fade) + after[0] * fade;
        }

        return result;
    }

    /// <summary>
    /// Hybrid reconstruction combining multiple methods with weighting.
    /// </summary>
    private float[] ReconstructHybrid(float[] before, float[] clipped, float[] after, int regionLength, int channel)
    {
        // Get results from all methods
        float[] splineResult = ReconstructWithSpline(before, clipped, after, regionLength);
        float[] harmonicResult = ReconstructWithHarmonic(before, clipped, after, regionLength, channel);
        float[] waveformResult = ReconstructWithWaveformContinuation(before, clipped, after, regionLength, channel);

        float[] result = new float[regionLength];

        // Analyze clip characteristics to weight methods
        float periodicity = EstimatePeriodicityScore(before);
        float transientScore = EstimateTransientScore(before);

        // Weights based on signal characteristics
        float splineWeight = 0.4f + transientScore * 0.3f;
        float harmonicWeight = periodicity * 0.4f;
        float waveformWeight = (1f - transientScore) * 0.3f;

        float totalWeight = splineWeight + harmonicWeight + waveformWeight;
        splineWeight /= totalWeight;
        harmonicWeight /= totalWeight;
        waveformWeight /= totalWeight;

        for (int i = 0; i < regionLength; i++)
        {
            result[i] = splineResult[i] * splineWeight +
                        harmonicResult[i] * harmonicWeight +
                        waveformResult[i] * waveformWeight;
        }

        return result;
    }

    private float EstimatePeak(float[] before, float[] after, float[] clipped, int regionLength)
    {
        // Calculate entry slope
        float entrySlope = 0;
        for (int i = 1; i < before.Length; i++)
        {
            entrySlope += MathF.Abs(before[i] - before[i - 1]);
        }
        entrySlope /= (before.Length - 1);

        float clipSign = MathF.Sign(clipped[regionLength / 2]);
        float baseLevel = MathF.Max(MathF.Abs(before[^1]), MathF.Abs(after[0]));
        float projectedPeak = baseLevel + entrySlope * regionLength / 2;

        return MathF.Min(projectedPeak, 1.5f) * clipSign;
    }

    private float EstimatePeriod(float[] samples)
    {
        // Autocorrelation-based period estimation
        float maxCorr = 0;
        int bestLag = 4;

        for (int lag = 4; lag < samples.Length / 2; lag++)
        {
            float corr = 0;
            float norm1 = 0, norm2 = 0;

            for (int i = 0; i < samples.Length - lag; i++)
            {
                corr += samples[i] * samples[i + lag];
                norm1 += samples[i] * samples[i];
                norm2 += samples[i + lag] * samples[i + lag];
            }

            if (norm1 > 0 && norm2 > 0)
            {
                corr /= MathF.Sqrt(norm1 * norm2);
                if (corr > maxCorr)
                {
                    maxCorr = corr;
                    bestLag = lag;
                }
            }
        }

        return bestLag;
    }

    private float EstimatePeriodicityScore(float[] samples)
    {
        float period = EstimatePeriod(samples);

        // Check how well samples repeat with this period
        float correlation = 0;
        int count = 0;

        for (int i = (int)period; i < samples.Length; i++)
        {
            int prevIdx = i - (int)period;
            if (prevIdx >= 0)
            {
                correlation += 1f - MathF.Abs(samples[i] - samples[prevIdx]);
                count++;
            }
        }

        return count > 0 ? correlation / count : 0;
    }

    private float EstimateTransientScore(float[] samples)
    {
        float maxDiff = 0;
        for (int i = 1; i < samples.Length; i++)
        {
            float diff = MathF.Abs(samples[i] - samples[i - 1]);
            maxDiff = MathF.Max(maxDiff, diff);
        }

        return MathF.Min(maxDiff * 5f, 1f);
    }

    private static void ComputeCubicSpline(float[] x, float[] y, out float[] a, out float[] b, out float[] c, out float[] d)
    {
        int n = x.Length - 1;

        a = new float[n + 1];
        b = new float[n];
        c = new float[n + 1];
        d = new float[n];

        for (int i = 0; i <= n; i++)
            a[i] = y[i];

        float[] h = new float[n];
        for (int i = 0; i < n; i++)
        {
            h[i] = x[i + 1] - x[i];
            if (h[i] == 0) h[i] = 1f;
        }

        float[] alpha = new float[n];
        for (int i = 1; i < n; i++)
        {
            alpha[i] = (3f / h[i]) * (a[i + 1] - a[i]) - (3f / h[i - 1]) * (a[i] - a[i - 1]);
        }

        float[] l = new float[n + 1];
        float[] mu = new float[n + 1];
        float[] z = new float[n + 1];

        l[0] = 1f;
        for (int i = 1; i < n; i++)
        {
            l[i] = 2f * (x[i + 1] - x[i - 1]) - h[i - 1] * mu[i - 1];
            if (MathF.Abs(l[i]) < 1e-10f) l[i] = 1e-10f;
            mu[i] = h[i] / l[i];
            z[i] = (alpha[i] - h[i - 1] * z[i - 1]) / l[i];
        }

        l[n] = 1f;
        c[n] = 0f;

        for (int j = n - 1; j >= 0; j--)
        {
            c[j] = z[j] - mu[j] * c[j + 1];
            b[j] = (a[j + 1] - a[j]) / h[j] - h[j] * (c[j + 1] + 2f * c[j]) / 3f;
            d[j] = (c[j + 1] - c[j]) / (3f * h[j]);
        }
    }

    private static float SoftClip(float sample)
    {
        const float threshold = 0.95f;
        if (MathF.Abs(sample) <= threshold)
            return sample;

        float sign = MathF.Sign(sample);
        float absSample = MathF.Abs(sample);
        float excess = absSample - threshold;
        float softened = threshold + (1f - threshold) * MathF.Tanh(excess / (1f - threshold));

        return sign * softened;
    }

    #region Presets

    /// <summary>
    /// Creates a preset for light clipping restoration.
    /// </summary>
    public static AIDeclip CreateLight(ISampleProvider source)
    {
        var effect = new AIDeclip(source, "Light AI Declip");
        effect.Threshold = 0.95f;
        effect.Reconstruction = 0.6f;
        effect.Method = AIDeclipMethod.Spline;
        effect.Sensitivity = 0.3f;
        effect.HarmonicOrder = 6;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for balanced clipping restoration.
    /// </summary>
    public static AIDeclip CreateBalanced(ISampleProvider source)
    {
        var effect = new AIDeclip(source, "Balanced AI Declip");
        effect.Threshold = 0.9f;
        effect.Reconstruction = 0.8f;
        effect.Method = AIDeclipMethod.Hybrid;
        effect.Sensitivity = 0.5f;
        effect.HarmonicOrder = 8;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for aggressive clipping restoration.
    /// </summary>
    public static AIDeclip CreateAggressive(ISampleProvider source)
    {
        var effect = new AIDeclip(source, "Aggressive AI Declip");
        effect.Threshold = 0.85f;
        effect.Reconstruction = 1.0f;
        effect.Method = AIDeclipMethod.Hybrid;
        effect.Sensitivity = 0.7f;
        effect.HarmonicOrder = 12;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset optimized for voice/dialog.
    /// </summary>
    public static AIDeclip CreateVoice(ISampleProvider source)
    {
        var effect = new AIDeclip(source, "Voice AI Declip");
        effect.Threshold = 0.92f;
        effect.Reconstruction = 0.75f;
        effect.Method = AIDeclipMethod.Harmonic;
        effect.Sensitivity = 0.5f;
        effect.HarmonicOrder = 10;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset optimized for music.
    /// </summary>
    public static AIDeclip CreateMusic(ISampleProvider source)
    {
        var effect = new AIDeclip(source, "Music AI Declip");
        effect.Threshold = 0.88f;
        effect.Reconstruction = 0.85f;
        effect.Method = AIDeclipMethod.Hybrid;
        effect.Sensitivity = 0.6f;
        effect.HarmonicOrder = 16;
        effect.PatternLength = 0.6f;
        effect.Mix = 1f;
        return effect;
    }

    #endregion

    #region Circular Buffer

    private class CircularBuffer
    {
        private readonly float[] _buffer;
        private int _writePos;

        public CircularBuffer(int size)
        {
            _buffer = new float[size];
            _writePos = 0;
        }

        public int WritePosition => _writePos;

        public void Write(float sample)
        {
            _buffer[_writePos] = sample;
            _writePos = (_writePos + 1) % _buffer.Length;
        }

        public void WriteAt(int index, float sample)
        {
            int pos = ((index % _buffer.Length) + _buffer.Length) % _buffer.Length;
            _buffer[pos] = sample;
        }

        public float Read(int delaySamples)
        {
            int readPos = _writePos - delaySamples - 1;
            if (readPos < 0) readPos += _buffer.Length;
            return _buffer[readPos];
        }

        public float ReadAt(int index)
        {
            int pos = ((index % _buffer.Length) + _buffer.Length) % _buffer.Length;
            return _buffer[pos];
        }
    }

    #endregion
}
