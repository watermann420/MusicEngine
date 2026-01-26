// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio restoration processor.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Restoration;

/// <summary>
/// Selection mode for spectral regions.
/// </summary>
public enum SpectralSelectionMode
{
    /// <summary>
    /// Rectangular selection in time-frequency domain.
    /// </summary>
    Rectangle,

    /// <summary>
    /// Freeform lasso selection.
    /// </summary>
    Lasso,

    /// <summary>
    /// Magic wand selection based on spectral similarity.
    /// </summary>
    MagicWand
}

/// <summary>
/// Interpolation method for filling repaired regions.
/// </summary>
public enum SpectralInterpolationMode
{
    /// <summary>
    /// Linear interpolation from surrounding content.
    /// </summary>
    Linear,

    /// <summary>
    /// Cubic spline interpolation for smoother results.
    /// </summary>
    CubicSpline,

    /// <summary>
    /// Pattern-based fill using nearby spectral patterns.
    /// </summary>
    PatternFill,

    /// <summary>
    /// Noise fill using spectral noise matching.
    /// </summary>
    NoiseFill
}

/// <summary>
/// Represents a region in the spectral domain to be repaired.
/// </summary>
public class SpectralRegion
{
    /// <summary>
    /// Unique identifier for this region.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// Start time in seconds.
    /// </summary>
    public float StartTime { get; set; }

    /// <summary>
    /// End time in seconds.
    /// </summary>
    public float EndTime { get; set; }

    /// <summary>
    /// Lower frequency bound in Hz.
    /// </summary>
    public float LowFrequency { get; set; }

    /// <summary>
    /// Upper frequency bound in Hz.
    /// </summary>
    public float HighFrequency { get; set; }

    /// <summary>
    /// Attenuation amount (0 = full removal, 1 = no change).
    /// </summary>
    public float Attenuation { get; set; } = 0f;

    /// <summary>
    /// Feather amount for soft edges (in Hz and seconds).
    /// </summary>
    public float Feather { get; set; } = 50f;

    /// <summary>
    /// Whether this region is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Spectral repair effect for removing unwanted sounds by spectral editing.
/// Uses FFT-based analysis to identify and interpolate over unwanted spectral content.
/// </summary>
/// <remarks>
/// Features:
/// - Define rectangular regions in time-frequency space
/// - Interpolate content from surrounding spectral data
/// - Support for undo/redo through region management
/// - Multiple interpolation modes for different material
/// </remarks>
public class SpectralRepair : EffectBase
{
    // FFT configuration
    private int _fftSize;
    private int _hopSize;
    private float[] _analysisWindow = null!;
    private Complex[][] _fftBuffer = null!;

    // Processing buffers (per channel)
    private float[][] _inputBuffer = null!;
    private float[][] _outputBuffer = null!;
    private int[] _inputWritePos = null!;
    private int[] _outputReadPos = null!;
    private int _samplesUntilNextFrame;

    // Region management
    private readonly List<SpectralRegion> _regions = new();
    private readonly Stack<SpectralRegion> _undoStack = new();
    private readonly Stack<SpectralRegion> _redoStack = new();

    // Spectral history for pattern fill
    private float[][][] _spectralHistory = null!;
    private int _historyWriteIndex;
    private const int SpectralHistorySize = 16;

    // Current position tracking
    private double _currentTimeSeconds;
    private bool _initialized;

    /// <summary>
    /// Creates a new spectral repair effect.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    public SpectralRepair(ISampleProvider source) : this(source, "Spectral Repair")
    {
    }

    /// <summary>
    /// Creates a new spectral repair effect with a custom name.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <param name="name">Effect name.</param>
    public SpectralRepair(ISampleProvider source, string name) : base(source, name)
    {
        RegisterParameter("FFTSize", 2048);
        RegisterParameter("InterpolationMode", (float)SpectralInterpolationMode.CubicSpline);
        RegisterParameter("SmoothingAmount", 0.5f);
        RegisterParameter("Mix", 1f);

        _fftSize = 2048;
        _initialized = false;
    }

    /// <summary>
    /// Gets or sets the FFT size (512, 1024, 2048, 4096).
    /// </summary>
    public int FFTSize
    {
        get => (int)GetParameter("FFTSize");
        set
        {
            int validSize = value switch
            {
                <= 512 => 512,
                <= 1024 => 1024,
                <= 2048 => 2048,
                _ => 4096
            };
            SetParameter("FFTSize", validSize);
            if (validSize != _fftSize)
            {
                _fftSize = validSize;
                _initialized = false;
            }
        }
    }

    /// <summary>
    /// Gets or sets the interpolation mode.
    /// </summary>
    public SpectralInterpolationMode InterpolationMode
    {
        get => (SpectralInterpolationMode)GetParameter("InterpolationMode");
        set => SetParameter("InterpolationMode", (float)value);
    }

    /// <summary>
    /// Gets or sets the smoothing amount for interpolation (0-1).
    /// </summary>
    public float SmoothingAmount
    {
        get => GetParameter("SmoothingAmount");
        set => SetParameter("SmoothingAmount", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Gets the collection of repair regions.
    /// </summary>
    public IReadOnlyList<SpectralRegion> Regions => _regions.AsReadOnly();

    /// <summary>
    /// Adds a new spectral region for repair.
    /// </summary>
    /// <param name="region">The region to add.</param>
    public void AddRegion(SpectralRegion region)
    {
        if (region == null) throw new ArgumentNullException(nameof(region));

        _regions.Add(region);
        _undoStack.Push(region);
        _redoStack.Clear();
    }

    /// <summary>
    /// Removes a spectral region.
    /// </summary>
    /// <param name="regionId">The ID of the region to remove.</param>
    /// <returns>True if region was found and removed.</returns>
    public bool RemoveRegion(Guid regionId)
    {
        var region = _regions.FirstOrDefault(r => r.Id == regionId);
        if (region != null)
        {
            _regions.Remove(region);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Clears all repair regions.
    /// </summary>
    public void ClearRegions()
    {
        _regions.Clear();
        _undoStack.Clear();
        _redoStack.Clear();
    }

    /// <summary>
    /// Undoes the last region addition.
    /// </summary>
    /// <returns>True if undo was performed.</returns>
    public bool Undo()
    {
        if (_undoStack.Count == 0) return false;

        var region = _undoStack.Pop();
        _regions.Remove(region);
        _redoStack.Push(region);
        return true;
    }

    /// <summary>
    /// Redoes the last undone region addition.
    /// </summary>
    /// <returns>True if redo was performed.</returns>
    public bool Redo()
    {
        if (_redoStack.Count == 0) return false;

        var region = _redoStack.Pop();
        _regions.Add(region);
        _undoStack.Push(region);
        return true;
    }

    /// <summary>
    /// Gets whether undo is available.
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>
    /// Gets whether redo is available.
    /// </summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// Creates a rectangular repair region.
    /// </summary>
    public SpectralRegion CreateRectangularRegion(float startTime, float endTime, float lowFreq, float highFreq)
    {
        var region = new SpectralRegion
        {
            StartTime = startTime,
            EndTime = endTime,
            LowFrequency = lowFreq,
            HighFrequency = highFreq,
            Attenuation = 0f
        };
        AddRegion(region);
        return region;
    }

    /// <summary>
    /// Initializes internal buffers.
    /// </summary>
    private void Initialize()
    {
        int channels = Channels;
        _hopSize = _fftSize / 4;

        int halfSize = _fftSize / 2 + 1;

        // Allocate per-channel buffers
        _inputBuffer = new float[channels][];
        _outputBuffer = new float[channels][];
        _inputWritePos = new int[channels];
        _outputReadPos = new int[channels];
        _fftBuffer = new Complex[channels][];
        _spectralHistory = new float[channels][][];

        for (int ch = 0; ch < channels; ch++)
        {
            _inputBuffer[ch] = new float[_fftSize * 2];
            _outputBuffer[ch] = new float[_fftSize * 4];
            _inputWritePos[ch] = 0;
            _outputReadPos[ch] = 0;
            _fftBuffer[ch] = new Complex[_fftSize];

            // Spectral history for pattern fill
            _spectralHistory[ch] = new float[SpectralHistorySize][];
            for (int h = 0; h < SpectralHistorySize; h++)
            {
                _spectralHistory[ch][h] = new float[halfSize];
            }
        }

        // Generate Hann window
        _analysisWindow = new float[_fftSize];
        for (int i = 0; i < _fftSize; i++)
        {
            _analysisWindow[i] = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (_fftSize - 1)));
        }

        _samplesUntilNextFrame = 0;
        _historyWriteIndex = 0;
        _currentTimeSeconds = 0;
        _initialized = true;
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        if (!_initialized)
        {
            Initialize();
        }

        // If no regions defined, pass through
        if (_regions.Count == 0 || !_regions.Any(r => r.Enabled))
        {
            Array.Copy(sourceBuffer, 0, destBuffer, offset, count);
            return;
        }

        int channels = Channels;

        // Process samples
        for (int i = 0; i < count; i += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                float inputSample = sourceBuffer[i + ch];

                // Write to input buffer
                _inputBuffer[ch][_inputWritePos[ch]] = inputSample;
                _inputWritePos[ch] = (_inputWritePos[ch] + 1) % _inputBuffer[ch].Length;
            }

            _samplesUntilNextFrame--;

            // Process spectral frame
            if (_samplesUntilNextFrame <= 0)
            {
                _samplesUntilNextFrame = _hopSize;

                for (int ch = 0; ch < channels; ch++)
                {
                    ProcessSpectralFrame(ch);
                }

                _currentTimeSeconds += (double)_hopSize / SampleRate;
            }

            // Read from output buffer
            for (int ch = 0; ch < channels; ch++)
            {
                float outputSample = _outputBuffer[ch][_outputReadPos[ch]];
                _outputBuffer[ch][_outputReadPos[ch]] = 0f;
                _outputReadPos[ch] = (_outputReadPos[ch] + 1) % _outputBuffer[ch].Length;

                destBuffer[offset + i + ch] = outputSample;
            }
        }
    }

    /// <summary>
    /// Processes a single spectral frame.
    /// </summary>
    private void ProcessSpectralFrame(int channel)
    {
        int halfSize = _fftSize / 2 + 1;

        // Copy windowed input to FFT buffer
        int readStart = (_inputWritePos[channel] - _fftSize + _inputBuffer[channel].Length) % _inputBuffer[channel].Length;
        for (int i = 0; i < _fftSize; i++)
        {
            int readPos = (readStart + i) % _inputBuffer[channel].Length;
            float windowedSample = _inputBuffer[channel][readPos] * _analysisWindow[i];
            _fftBuffer[channel][i] = new Complex(windowedSample, 0f);
        }

        // Forward FFT
        FFT(_fftBuffer[channel], false);

        // Extract magnitudes and phases
        float[] magnitude = new float[halfSize];
        float[] phase = new float[halfSize];

        for (int k = 0; k < halfSize; k++)
        {
            float real = _fftBuffer[channel][k].Real;
            float imag = _fftBuffer[channel][k].Imag;
            magnitude[k] = MathF.Sqrt(real * real + imag * imag);
            phase[k] = MathF.Atan2(imag, real);
        }

        // Store in history for pattern fill
        Array.Copy(magnitude, _spectralHistory[channel][_historyWriteIndex], halfSize);

        // Apply repair to active regions
        ApplySpectralRepair(magnitude, phase, halfSize);

        // Reconstruct complex spectrum
        for (int k = 0; k < halfSize; k++)
        {
            float mag = magnitude[k];
            float ph = phase[k];
            _fftBuffer[channel][k] = new Complex(mag * MathF.Cos(ph), mag * MathF.Sin(ph));

            // Mirror for negative frequencies
            if (k > 0 && k < halfSize - 1)
            {
                _fftBuffer[channel][_fftSize - k] = new Complex(mag * MathF.Cos(ph), -mag * MathF.Sin(ph));
            }
        }

        // Inverse FFT
        FFT(_fftBuffer[channel], true);

        // Overlap-add to output buffer
        float normFactor = 1f / (4f * 0.5f);
        for (int i = 0; i < _fftSize; i++)
        {
            int outputPos = (_outputReadPos[channel] + i) % _outputBuffer[channel].Length;
            _outputBuffer[channel][outputPos] += _fftBuffer[channel][i].Real * _analysisWindow[i] * normFactor;
        }

        // Update history index
        if (channel == Channels - 1)
        {
            _historyWriteIndex = (_historyWriteIndex + 1) % SpectralHistorySize;
        }
    }

    /// <summary>
    /// Applies spectral repair to magnitude spectrum.
    /// </summary>
    private void ApplySpectralRepair(float[] magnitude, float[] phase, int halfSize)
    {
        float currentTime = (float)_currentTimeSeconds;
        float freqResolution = (float)SampleRate / _fftSize;

        foreach (var region in _regions.Where(r => r.Enabled))
        {
            // Check if current time is within region
            if (currentTime < region.StartTime - region.Feather / 1000f ||
                currentTime > region.EndTime + region.Feather / 1000f)
            {
                continue;
            }

            // Calculate time envelope for feathering
            float timeEnvelope = 1f;
            if (currentTime < region.StartTime)
            {
                timeEnvelope = (currentTime - (region.StartTime - region.Feather / 1000f)) / (region.Feather / 1000f);
            }
            else if (currentTime > region.EndTime)
            {
                timeEnvelope = ((region.EndTime + region.Feather / 1000f) - currentTime) / (region.Feather / 1000f);
            }
            timeEnvelope = Math.Clamp(timeEnvelope, 0f, 1f);

            // Process frequency bins
            int lowBin = Math.Max(1, (int)(region.LowFrequency / freqResolution));
            int highBin = Math.Min(halfSize - 1, (int)(region.HighFrequency / freqResolution));

            for (int k = lowBin; k <= highBin; k++)
            {
                float freq = k * freqResolution;

                // Calculate frequency envelope for feathering
                float freqEnvelope = 1f;
                if (freq < region.LowFrequency + region.Feather)
                {
                    freqEnvelope = (freq - (region.LowFrequency - region.Feather)) / (2f * region.Feather);
                }
                else if (freq > region.HighFrequency - region.Feather)
                {
                    freqEnvelope = ((region.HighFrequency + region.Feather) - freq) / (2f * region.Feather);
                }
                freqEnvelope = Math.Clamp(freqEnvelope, 0f, 1f);

                // Combined envelope
                float envelope = timeEnvelope * freqEnvelope;

                // Interpolate magnitude based on mode
                float interpolatedMag = InterpolateMagnitude(k, halfSize, magnitude);

                // Blend between original and interpolated based on attenuation and envelope
                float targetMag = interpolatedMag * (1f - region.Attenuation);
                magnitude[k] = magnitude[k] * (1f - envelope) + targetMag * envelope;
            }
        }
    }

    /// <summary>
    /// Interpolates magnitude for a frequency bin.
    /// </summary>
    private float InterpolateMagnitude(int bin, int halfSize, float[] magnitude)
    {
        var mode = InterpolationMode;
        float smoothing = SmoothingAmount;

        switch (mode)
        {
            case SpectralInterpolationMode.Linear:
                // Find nearest unaffected bins and interpolate
                int lowerBin = FindUnaffectedBin(bin, -1, halfSize);
                int upperBin = FindUnaffectedBin(bin, 1, halfSize);

                if (lowerBin >= 0 && upperBin < halfSize)
                {
                    float t = (float)(bin - lowerBin) / (upperBin - lowerBin);
                    return magnitude[lowerBin] * (1f - t) + magnitude[upperBin] * t;
                }
                break;

            case SpectralInterpolationMode.CubicSpline:
                // Cubic spline using 4 surrounding points
                return CubicSplineInterpolate(bin, halfSize, magnitude);

            case SpectralInterpolationMode.PatternFill:
                // Use spectral history to find matching pattern
                return PatternFillInterpolate(bin);

            case SpectralInterpolationMode.NoiseFill:
                // Fill with noise matching surrounding spectral characteristics
                return NoiseFillInterpolate(bin, halfSize, magnitude);
        }

        return magnitude[bin] * (1f - smoothing);
    }

    private int FindUnaffectedBin(int startBin, int direction, int halfSize)
    {
        float freqResolution = (float)SampleRate / _fftSize;

        for (int k = startBin + direction; k >= 0 && k < halfSize; k += direction)
        {
            float freq = k * freqResolution;
            bool inRegion = false;

            foreach (var region in _regions.Where(r => r.Enabled))
            {
                if (freq >= region.LowFrequency && freq <= region.HighFrequency)
                {
                    inRegion = true;
                    break;
                }
            }

            if (!inRegion) return k;
        }

        return direction < 0 ? 0 : halfSize - 1;
    }

    private float CubicSplineInterpolate(int bin, int halfSize, float[] magnitude)
    {
        // Get 4 control points
        int p0 = Math.Max(0, FindUnaffectedBin(bin, -1, halfSize) - 1);
        int p1 = Math.Max(0, FindUnaffectedBin(bin, -1, halfSize));
        int p2 = Math.Min(halfSize - 1, FindUnaffectedBin(bin, 1, halfSize));
        int p3 = Math.Min(halfSize - 1, FindUnaffectedBin(bin, 1, halfSize) + 1);

        if (p1 == p2) return magnitude[p1];

        float t = (float)(bin - p1) / (p2 - p1);
        float t2 = t * t;
        float t3 = t2 * t;

        // Catmull-Rom spline
        float v0 = magnitude[p0];
        float v1 = magnitude[p1];
        float v2 = magnitude[p2];
        float v3 = magnitude[p3];

        return 0.5f * ((2f * v1) +
                       (-v0 + v2) * t +
                       (2f * v0 - 5f * v1 + 4f * v2 - v3) * t2 +
                       (-v0 + 3f * v1 - 3f * v2 + v3) * t3);
    }

    private float PatternFillInterpolate(int bin)
    {
        // Average from spectral history
        float sum = 0f;
        int count = 0;

        for (int h = 0; h < SpectralHistorySize; h++)
        {
            if (h != _historyWriteIndex && _spectralHistory[0][h] != null)
            {
                sum += _spectralHistory[0][h][bin];
                count++;
            }
        }

        return count > 0 ? sum / count : 0f;
    }

    private float NoiseFillInterpolate(int bin, int halfSize, float[] magnitude)
    {
        // Estimate local noise floor from surrounding bins
        float sum = 0f;
        int count = 0;
        int range = 5;

        for (int k = Math.Max(0, bin - range); k <= Math.Min(halfSize - 1, bin + range); k++)
        {
            if (k != bin)
            {
                sum += magnitude[k];
                count++;
            }
        }

        float localAverage = count > 0 ? sum / count : 0f;

        // Add slight random variation
        float variation = localAverage * 0.1f * (2f * Random.Shared.NextSingle() - 1f);
        return Math.Max(0f, localAverage + variation);
    }

    /// <summary>
    /// In-place Cooley-Tukey FFT implementation.
    /// </summary>
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

        // Scale for inverse FFT
        if (inverse)
        {
            for (int i = 0; i < n; i++)
            {
                data[i] = new Complex(data[i].Real / n, data[i].Imag / n);
            }
        }
    }

    #region Complex Number Struct

    /// <summary>
    /// Simple complex number struct for FFT operations.
    /// </summary>
    private readonly struct Complex
    {
        public readonly float Real;
        public readonly float Imag;

        public Complex(float real, float imag)
        {
            Real = real;
            Imag = imag;
        }

        public static Complex operator +(Complex a, Complex b) =>
            new Complex(a.Real + b.Real, a.Imag + b.Imag);

        public static Complex operator -(Complex a, Complex b) =>
            new Complex(a.Real - b.Real, a.Imag - b.Imag);

        public static Complex operator *(Complex a, Complex b) =>
            new Complex(a.Real * b.Real - a.Imag * b.Imag,
                        a.Real * b.Imag + a.Imag * b.Real);
    }

    #endregion

    #region Presets

    /// <summary>
    /// Creates a preset for removing short clicks/pops.
    /// </summary>
    public static SpectralRepair CreateClickRemovalPreset(ISampleProvider source)
    {
        var effect = new SpectralRepair(source, "Click Removal");
        effect.FFTSize = 1024;
        effect.InterpolationMode = SpectralInterpolationMode.CubicSpline;
        effect.SmoothingAmount = 0.3f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for removing tonal noise (hum, buzz).
    /// </summary>
    public static SpectralRepair CreateTonalNoiseRemovalPreset(ISampleProvider source)
    {
        var effect = new SpectralRepair(source, "Tonal Noise Removal");
        effect.FFTSize = 4096;
        effect.InterpolationMode = SpectralInterpolationMode.PatternFill;
        effect.SmoothingAmount = 0.7f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for removing broadband noise regions.
    /// </summary>
    public static SpectralRepair CreateBroadbandRemovalPreset(ISampleProvider source)
    {
        var effect = new SpectralRepair(source, "Broadband Removal");
        effect.FFTSize = 2048;
        effect.InterpolationMode = SpectralInterpolationMode.NoiseFill;
        effect.SmoothingAmount = 0.5f;
        return effect;
    }

    #endregion
}
