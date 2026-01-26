// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Special;

/// <summary>
/// Detection mode for identifying clipped samples.
/// </summary>
public enum ClipDetectionMode
{
    /// <summary>
    /// Soft detection - detects samples close to threshold.
    /// Better for slightly overdriven signals.
    /// </summary>
    Soft,

    /// <summary>
    /// Medium detection - balanced detection for most cases.
    /// </summary>
    Medium,

    /// <summary>
    /// Hard detection - only detects samples at or very near the threshold.
    /// For heavily clipped signals.
    /// </summary>
    Hard
}

/// <summary>
/// Oversampling factor for reconstruction quality.
/// </summary>
public enum DeclipOversampling
{
    /// <summary>
    /// No oversampling (1x). Fastest processing.
    /// </summary>
    None = 1,

    /// <summary>
    /// 2x oversampling. Good balance of quality and performance.
    /// </summary>
    TwoX = 2,

    /// <summary>
    /// 4x oversampling. Best reconstruction quality.
    /// </summary>
    FourX = 4
}

/// <summary>
/// Audio restoration effect that detects and reconstructs clipped regions
/// using cubic spline interpolation based on surrounding sample context.
/// </summary>
/// <remarks>
/// The declipping algorithm works in the following stages:
/// 1. Detection: Identifies clipped regions where consecutive samples are at or near the threshold
/// 2. Extension: Expands the detected region by the margin to include transition samples
/// 3. Interpolation: Uses cubic spline interpolation to reconstruct the clipped waveform
/// 4. Blending: Smoothly blends the reconstructed signal with the original based on reconstruction amount
///
/// Cubic spline interpolation provides smooth, continuous curves through the surrounding
/// unclipped samples, producing natural-sounding restoration without harsh artifacts.
/// </remarks>
public class DeclippingEffect : EffectBase
{
    // Processing state per channel
    private CircularBuffer[] _inputBuffers = null!;
    private CircularBuffer[] _outputBuffers = null!;
    private bool[] _isClipped = null!;
    private int[] _clipStartIndex = null!;
    private int[] _clipLength = null!;

    // Buffer sizes
    private const int BufferSize = 4096;
    private const int MaxMargin = 10;
    private const int ContextSamples = 8; // Samples on each side for spline fitting

    // State
    private bool _initialized;
    private int _processedSamples;
    private int _latencySamples;

    // Oversampling
    private float[][] _oversampleBuffer = null!;
    private float[][] _downsampleBuffer = null!;

    /// <summary>
    /// Creates a new declipping effect.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    public DeclippingEffect(ISampleProvider source) : this(source, "Declipping")
    {
    }

    /// <summary>
    /// Creates a new declipping effect with a custom name.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <param name="name">Effect name</param>
    public DeclippingEffect(ISampleProvider source, string name) : base(source, name)
    {
        // Register parameters with defaults
        RegisterParameter("Threshold", 0.95f);       // 0.7 - 1.0: Detection threshold
        RegisterParameter("Reconstruction", 0.8f);    // 0.0 - 1.0: How much to reconstruct
        RegisterParameter("Margin", 3f);              // 1 - 10: Extra samples around clip
        RegisterParameter("Mode", 1f);                // 0 = Soft, 1 = Medium, 2 = Hard
        RegisterParameter("Oversampling", 1f);        // 0 = 1x, 1 = 2x, 2 = 4x
        RegisterParameter("Mix", 1f);                 // Dry/wet mix

        _initialized = false;
    }

    /// <summary>
    /// Detection threshold for clipped samples (0.7 - 1.0).
    /// Samples at or above this level (in absolute value) are considered clipped.
    /// </summary>
    public float Threshold
    {
        get => GetParameter("Threshold");
        set => SetParameter("Threshold", Math.Clamp(value, 0.7f, 1.0f));
    }

    /// <summary>
    /// Reconstruction amount (0.0 - 1.0).
    /// Controls how much of the reconstructed signal replaces the clipped signal.
    /// 0 = no reconstruction, 1 = full reconstruction.
    /// </summary>
    public float Reconstruction
    {
        get => GetParameter("Reconstruction");
        set => SetParameter("Reconstruction", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Margin samples around detected clips (1 - 10).
    /// Extra samples processed on each side of detected clipped regions.
    /// </summary>
    public int Margin
    {
        get => (int)GetParameter("Margin");
        set => SetParameter("Margin", Math.Clamp(value, 1, MaxMargin));
    }

    /// <summary>
    /// Clip detection mode.
    /// </summary>
    public ClipDetectionMode Mode
    {
        get => (ClipDetectionMode)(int)GetParameter("Mode");
        set => SetParameter("Mode", (float)value);
    }

    /// <summary>
    /// Oversampling factor for reconstruction.
    /// Higher values provide better quality but more CPU usage.
    /// </summary>
    public DeclipOversampling Oversampling
    {
        get
        {
            int val = (int)GetParameter("Oversampling");
            return val switch
            {
                0 => DeclipOversampling.None,
                1 => DeclipOversampling.TwoX,
                2 => DeclipOversampling.FourX,
                _ => DeclipOversampling.TwoX
            };
        }
        set
        {
            int val = value switch
            {
                DeclipOversampling.None => 0,
                DeclipOversampling.TwoX => 1,
                DeclipOversampling.FourX => 2,
                _ => 1
            };
            SetParameter("Oversampling", val);
        }
    }

    /// <summary>
    /// Gets the effective detection threshold based on mode.
    /// </summary>
    private float EffectiveThreshold
    {
        get
        {
            float baseThreshold = Threshold;
            return Mode switch
            {
                ClipDetectionMode.Soft => baseThreshold - 0.05f,   // More sensitive
                ClipDetectionMode.Medium => baseThreshold,         // As specified
                ClipDetectionMode.Hard => baseThreshold + 0.02f,   // Less sensitive
                _ => baseThreshold
            };
        }
    }

    /// <summary>
    /// Gets the tolerance for consecutive clipped sample detection based on mode.
    /// </summary>
    private float ClipTolerance
    {
        get
        {
            return Mode switch
            {
                ClipDetectionMode.Soft => 0.02f,   // Wider tolerance
                ClipDetectionMode.Medium => 0.01f, // Normal tolerance
                ClipDetectionMode.Hard => 0.005f,  // Tight tolerance
                _ => 0.01f
            };
        }
    }

    /// <summary>
    /// Initializes internal buffers.
    /// </summary>
    private void Initialize()
    {
        int channels = Channels;

        _inputBuffers = new CircularBuffer[channels];
        _outputBuffers = new CircularBuffer[channels];
        _isClipped = new bool[channels];
        _clipStartIndex = new int[channels];
        _clipLength = new int[channels];

        for (int ch = 0; ch < channels; ch++)
        {
            _inputBuffers[ch] = new CircularBuffer(BufferSize);
            _outputBuffers[ch] = new CircularBuffer(BufferSize);
            _isClipped[ch] = false;
            _clipStartIndex[ch] = -1;
            _clipLength[ch] = 0;
        }

        // Oversampling buffers
        int maxOversample = 4;
        _oversampleBuffer = new float[channels][];
        _downsampleBuffer = new float[channels][];
        for (int ch = 0; ch < channels; ch++)
        {
            _oversampleBuffer[ch] = new float[BufferSize * maxOversample];
            _downsampleBuffer[ch] = new float[BufferSize];
        }

        // Latency for lookahead (context + margin)
        _latencySamples = ContextSamples + MaxMargin;
        _processedSamples = 0;

        _initialized = true;
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        if (!_initialized)
        {
            Initialize();
        }

        int channels = Channels;
        float threshold = EffectiveThreshold;
        float tolerance = ClipTolerance;
        int margin = Margin;
        float reconstruction = Reconstruction;
        int oversampleFactor = (int)Oversampling;

        // Process interleaved samples
        for (int i = 0; i < count; i += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                float input = sourceBuffer[i + ch];

                // Write to input buffer
                _inputBuffers[ch].Write(input);

                // Check if current sample is clipped
                bool isCurrentClipped = IsClippedSample(input, threshold, tolerance);

                if (isCurrentClipped)
                {
                    if (!_isClipped[ch])
                    {
                        // Start of new clipped region
                        _isClipped[ch] = true;
                        _clipStartIndex[ch] = _inputBuffers[ch].WritePosition;
                        _clipLength[ch] = 1;
                    }
                    else
                    {
                        // Continue clipped region
                        _clipLength[ch]++;
                    }
                }
                else
                {
                    if (_isClipped[ch])
                    {
                        // End of clipped region - process it
                        ProcessClippedRegion(ch, margin, reconstruction, oversampleFactor);
                        _isClipped[ch] = false;
                        _clipStartIndex[ch] = -1;
                        _clipLength[ch] = 0;
                    }
                }

                // Read output (with latency for lookahead)
                float output;
                if (_processedSamples >= _latencySamples)
                {
                    output = _outputBuffers[ch].Read(_latencySamples);
                }
                else
                {
                    output = 0f;
                }

                // If no reconstruction happened, pass through the original
                if (_outputBuffers[ch].ReadAt(_inputBuffers[ch].WritePosition - _latencySamples) == 0f)
                {
                    output = _inputBuffers[ch].Read(_latencySamples);
                }

                destBuffer[offset + i + ch] = output;
            }

            _processedSamples++;
        }
    }

    /// <summary>
    /// Determines if a sample is considered clipped.
    /// </summary>
    private static bool IsClippedSample(float sample, float threshold, float tolerance)
    {
        float absSample = MathF.Abs(sample);

        // Check if sample is at or above threshold
        if (absSample >= threshold)
            return true;

        // For very near threshold (within tolerance), also consider clipped
        // This helps catch samples that were clipped but slightly reduced
        if (absSample >= threshold - tolerance)
            return true;

        return false;
    }

    /// <summary>
    /// Processes a detected clipped region using cubic spline interpolation.
    /// </summary>
    private void ProcessClippedRegion(int channel, int margin, float reconstruction, int oversampleFactor)
    {
        if (_clipLength[channel] < 1)
            return;

        // Expand region by margin
        int startIdx = _clipStartIndex[channel] - margin;
        int endIdx = _clipStartIndex[channel] + _clipLength[channel] + margin;
        int regionLength = endIdx - startIdx;

        if (regionLength <= 0 || regionLength > BufferSize / 2)
            return;

        // Collect context samples before and after the clipped region
        float[] beforeSamples = new float[ContextSamples];
        float[] afterSamples = new float[ContextSamples];
        float[] clippedSamples = new float[regionLength];

        // Read samples before clipped region
        for (int i = 0; i < ContextSamples; i++)
        {
            beforeSamples[i] = _inputBuffers[channel].ReadAt(startIdx - ContextSamples + i);
        }

        // Read clipped samples
        for (int i = 0; i < regionLength; i++)
        {
            clippedSamples[i] = _inputBuffers[channel].ReadAt(startIdx + i);
        }

        // Read samples after clipped region
        for (int i = 0; i < ContextSamples; i++)
        {
            afterSamples[i] = _inputBuffers[channel].ReadAt(endIdx + i);
        }

        // Perform cubic spline interpolation
        float[] reconstructed;
        if (oversampleFactor > 1)
        {
            reconstructed = ReconstructWithOversampling(beforeSamples, clippedSamples, afterSamples,
                regionLength, oversampleFactor);
        }
        else
        {
            reconstructed = ReconstructWithSpline(beforeSamples, clippedSamples, afterSamples, regionLength);
        }

        // Blend reconstructed samples with original based on reconstruction amount
        for (int i = 0; i < regionLength; i++)
        {
            float original = clippedSamples[i];
            float restored = reconstructed[i];

            // Blend based on how clipped the sample was
            float clipAmount = MathF.Abs(original) >= EffectiveThreshold ? 1f : 0f;
            float blendAmount = clipAmount * reconstruction;

            float output = original * (1f - blendAmount) + restored * blendAmount;

            // Soft clip the output to prevent new clipping
            output = SoftClip(output);

            // Write to output buffer
            _outputBuffers[channel].WriteAt(startIdx + i, output);
        }
    }

    /// <summary>
    /// Reconstructs clipped samples using cubic spline interpolation.
    /// </summary>
    private float[] ReconstructWithSpline(float[] before, float[] clipped, float[] after, int regionLength)
    {
        float[] result = new float[regionLength];

        // Build the complete sample array for spline fitting
        int totalLength = ContextSamples + regionLength + ContextSamples;
        float[] x = new float[totalLength];
        float[] y = new float[totalLength];

        // Fill x values (sample indices)
        for (int i = 0; i < totalLength; i++)
        {
            x[i] = i;
        }

        // Fill y values - use context samples, estimate clipped region
        for (int i = 0; i < ContextSamples; i++)
        {
            y[i] = before[i];
        }

        // For clipped region, we'll interpolate from endpoints
        // First, get the boundary values
        float startValue = before[ContextSamples - 1];
        float endValue = after[0];

        // Estimate the peak of the clipped region based on surrounding samples
        float peakEstimate = EstimateClippedPeak(before, after, clipped, regionLength);

        // Create smooth transition through the clipped region
        for (int i = 0; i < regionLength; i++)
        {
            float t = regionLength > 1 ? (float)i / (regionLength - 1) : 0.5f;
            // Use a sinusoidal curve to estimate the waveform shape
            float envelope = MathF.Sin(t * MathF.PI);
            float baseline = startValue * (1f - t) + endValue * t;
            y[ContextSamples + i] = baseline + (peakEstimate - MathF.Abs(baseline)) * envelope * MathF.Sign(clipped[i]);
        }

        for (int i = 0; i < ContextSamples; i++)
        {
            y[ContextSamples + regionLength + i] = after[i];
        }

        // Compute cubic spline coefficients
        float[] a, b, c, d;
        ComputeCubicSplineCoefficients(x, y, out a, out b, out c, out d);

        // Evaluate spline for the clipped region
        for (int i = 0; i < regionLength; i++)
        {
            float xi = ContextSamples + i;
            result[i] = EvaluateCubicSpline(x, a, b, c, d, xi);
        }

        return result;
    }

    /// <summary>
    /// Reconstructs with oversampling for better quality.
    /// </summary>
    private float[] ReconstructWithOversampling(float[] before, float[] clipped, float[] after,
        int regionLength, int factor)
    {
        // Upsample the context
        int upLength = regionLength * factor;
        float[] upBefore = Upsample(before, factor);
        float[] upAfter = Upsample(after, factor);

        // Reconstruct at higher sample rate
        float[] upClipped = new float[upLength];
        for (int i = 0; i < regionLength; i++)
        {
            for (int j = 0; j < factor; j++)
            {
                upClipped[i * factor + j] = clipped[i];
            }
        }

        float[] upResult = ReconstructWithSpline(upBefore, upClipped, upAfter, upLength);

        // Downsample result
        return Downsample(upResult, factor);
    }

    /// <summary>
    /// Simple linear upsample.
    /// </summary>
    private static float[] Upsample(float[] input, int factor)
    {
        int outLength = input.Length * factor;
        float[] output = new float[outLength];

        for (int i = 0; i < input.Length - 1; i++)
        {
            for (int j = 0; j < factor; j++)
            {
                float t = (float)j / factor;
                output[i * factor + j] = input[i] * (1f - t) + input[i + 1] * t;
            }
        }

        // Last sample
        for (int j = 0; j < factor; j++)
        {
            output[(input.Length - 1) * factor + j] = input[input.Length - 1];
        }

        return output;
    }

    /// <summary>
    /// Simple averaging downsample.
    /// </summary>
    private static float[] Downsample(float[] input, int factor)
    {
        int outLength = input.Length / factor;
        float[] output = new float[outLength];

        for (int i = 0; i < outLength; i++)
        {
            float sum = 0f;
            for (int j = 0; j < factor; j++)
            {
                sum += input[i * factor + j];
            }
            output[i] = sum / factor;
        }

        return output;
    }

    /// <summary>
    /// Estimates the peak value of a clipped region based on surrounding waveform.
    /// </summary>
    private float EstimateClippedPeak(float[] before, float[] after, float[] clipped, int regionLength)
    {
        // Look at the derivative of the waveform entering and exiting the clip
        // to estimate what the peak should have been

        // Calculate entry slope
        float entrySlope = 0f;
        for (int i = 1; i < ContextSamples; i++)
        {
            entrySlope += before[i] - before[i - 1];
        }
        entrySlope /= (ContextSamples - 1);

        // Calculate exit slope
        float exitSlope = 0f;
        for (int i = 1; i < ContextSamples; i++)
        {
            exitSlope += after[i] - after[i - 1];
        }
        exitSlope /= (ContextSamples - 1);

        // Estimate based on clip length and slopes
        float clipSign = MathF.Sign(clipped[regionLength / 2]);
        float baseLevel = MathF.Max(MathF.Abs(before[ContextSamples - 1]), MathF.Abs(after[0]));

        // Project where the peak would be based on the entry slope
        float projectedPeak = baseLevel + MathF.Abs(entrySlope) * regionLength / 2;

        // Clamp to reasonable range (don't over-reconstruct)
        float maxPeak = 1.5f; // Allow some overshoot but not excessive
        projectedPeak = MathF.Min(projectedPeak, maxPeak);

        return projectedPeak * clipSign;
    }

    /// <summary>
    /// Computes cubic spline coefficients using the natural spline method.
    /// </summary>
    private static void ComputeCubicSplineCoefficients(float[] x, float[] y,
        out float[] a, out float[] b, out float[] c, out float[] d)
    {
        int n = x.Length - 1;

        a = new float[n + 1];
        b = new float[n];
        c = new float[n + 1];
        d = new float[n];

        // Copy y values to a
        for (int i = 0; i <= n; i++)
        {
            a[i] = y[i];
        }

        // Calculate h (step sizes)
        float[] h = new float[n];
        for (int i = 0; i < n; i++)
        {
            h[i] = x[i + 1] - x[i];
            if (h[i] == 0) h[i] = 1f; // Prevent division by zero
        }

        // Calculate alpha
        float[] alpha = new float[n];
        for (int i = 1; i < n; i++)
        {
            alpha[i] = (3f / h[i]) * (a[i + 1] - a[i]) - (3f / h[i - 1]) * (a[i] - a[i - 1]);
        }

        // Solve tridiagonal system for c
        float[] l = new float[n + 1];
        float[] mu = new float[n + 1];
        float[] z = new float[n + 1];

        l[0] = 1f;
        mu[0] = 0f;
        z[0] = 0f;

        for (int i = 1; i < n; i++)
        {
            l[i] = 2f * (x[i + 1] - x[i - 1]) - h[i - 1] * mu[i - 1];
            if (MathF.Abs(l[i]) < 1e-10f) l[i] = 1e-10f; // Prevent division by zero
            mu[i] = h[i] / l[i];
            z[i] = (alpha[i] - h[i - 1] * z[i - 1]) / l[i];
        }

        l[n] = 1f;
        z[n] = 0f;
        c[n] = 0f;

        // Back substitution
        for (int j = n - 1; j >= 0; j--)
        {
            c[j] = z[j] - mu[j] * c[j + 1];
            b[j] = (a[j + 1] - a[j]) / h[j] - h[j] * (c[j + 1] + 2f * c[j]) / 3f;
            d[j] = (c[j + 1] - c[j]) / (3f * h[j]);
        }
    }

    /// <summary>
    /// Evaluates the cubic spline at a given point.
    /// </summary>
    private static float EvaluateCubicSpline(float[] x, float[] a, float[] b, float[] c, float[] d, float xi)
    {
        int n = x.Length - 1;

        // Find the interval
        int i = 0;
        for (int j = 0; j < n; j++)
        {
            if (xi >= x[j] && xi < x[j + 1])
            {
                i = j;
                break;
            }
        }

        // Handle edge case
        if (xi >= x[n])
        {
            i = n - 1;
        }

        float dx = xi - x[i];
        return a[i] + b[i] * dx + c[i] * dx * dx + d[i] * dx * dx * dx;
    }

    /// <summary>
    /// Applies soft clipping to prevent new hard clips.
    /// </summary>
    private static float SoftClip(float sample)
    {
        // Use tanh-style soft clip
        const float threshold = 0.9f;

        if (MathF.Abs(sample) <= threshold)
            return sample;

        float sign = MathF.Sign(sample);
        float absSample = MathF.Abs(sample);

        // Soft knee compression above threshold
        float excess = absSample - threshold;
        float softened = threshold + (1f - threshold) * MathF.Tanh(excess / (1f - threshold));

        return sign * softened;
    }

    /// <summary>
    /// Creates a preset for light audio restoration.
    /// Suitable for slightly clipped recordings.
    /// </summary>
    public static DeclippingEffect CreateLightRestoration(ISampleProvider source)
    {
        var effect = new DeclippingEffect(source, "Light Restoration");
        effect.Threshold = 0.95f;
        effect.Reconstruction = 0.5f;
        effect.Margin = 2;
        effect.Mode = ClipDetectionMode.Soft;
        effect.Oversampling = DeclipOversampling.None;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for heavy audio restoration.
    /// Suitable for severely clipped recordings.
    /// </summary>
    public static DeclippingEffect CreateHeavyRestoration(ISampleProvider source)
    {
        var effect = new DeclippingEffect(source, "Heavy Restoration");
        effect.Threshold = 0.85f;
        effect.Reconstruction = 1.0f;
        effect.Margin = 5;
        effect.Mode = ClipDetectionMode.Medium;
        effect.Oversampling = DeclipOversampling.FourX;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for broadcast audio restoration.
    /// Optimized for voice and broadcast material.
    /// </summary>
    public static DeclippingEffect CreateBroadcastFix(ISampleProvider source)
    {
        var effect = new DeclippingEffect(source, "Broadcast Fix");
        effect.Threshold = 0.92f;
        effect.Reconstruction = 0.75f;
        effect.Margin = 3;
        effect.Mode = ClipDetectionMode.Medium;
        effect.Oversampling = DeclipOversampling.TwoX;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Circular buffer for sample storage with random access.
    /// </summary>
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
}
