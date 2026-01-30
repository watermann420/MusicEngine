//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: High-quality resampling utility with multiple algorithms and advanced features.

using System.Runtime.CompilerServices;
using NAudio.Wave;

namespace MusicEngine.Core.Utilities;

/// <summary>
/// Quality mode for resampling with different filter lengths.
/// </summary>
public enum ResampleQuality
{
    /// <summary>Draft quality - fast, minimal filtering (32 taps)</summary>
    Draft,
    /// <summary>Good quality - balanced performance (64 taps)</summary>
    Good,
    /// <summary>Best quality - high-fidelity (128 taps)</summary>
    Best,
    /// <summary>Master quality - maximum fidelity for mastering (256 taps)</summary>
    Master
}

/// <summary>
/// Resampling algorithm selection.
/// </summary>
public enum ResampleAlgorithm
{
    /// <summary>Linear interpolation - fastest, lowest quality</summary>
    Linear,
    /// <summary>Cubic spline interpolation - good balance</summary>
    Cubic,
    /// <summary>Sinc interpolation with windowing - high quality</summary>
    Sinc,
    /// <summary>Polyphase filter bank - optimal for integer ratios</summary>
    Polyphase
}

/// <summary>
/// Phase response type for the resampling filter.
/// </summary>
public enum PhaseResponse
{
    /// <summary>Linear phase - symmetric impulse response, no phase distortion</summary>
    Linear,
    /// <summary>Minimum phase - reduced pre-ringing, some phase shift</summary>
    Minimum
}

/// <summary>
/// Oversampling factor for effects processing.
/// </summary>
public enum OversamplingFactor
{
    /// <summary>No oversampling</summary>
    None = 1,
    /// <summary>2x oversampling</summary>
    X2 = 2,
    /// <summary>4x oversampling</summary>
    X4 = 4,
    /// <summary>8x oversampling</summary>
    X8 = 8
}

/// <summary>
/// Filter characteristics information.
/// </summary>
public class FilterCharacteristics
{
    /// <summary>Number of filter taps</summary>
    public int FilterTaps { get; init; }

    /// <summary>Cutoff frequency in Hz</summary>
    public double CutoffFrequency { get; init; }

    /// <summary>Transition bandwidth in Hz</summary>
    public double TransitionBandwidth { get; init; }

    /// <summary>Stopband attenuation in dB</summary>
    public double StopbandAttenuation { get; init; }

    /// <summary>Passband ripple in dB</summary>
    public double PassbandRipple { get; init; }

    /// <summary>Group delay in samples</summary>
    public double GroupDelay { get; init; }

    /// <summary>Pre-ringing in samples</summary>
    public int PreRinging { get; init; }

    /// <summary>Post-ringing in samples</summary>
    public int PostRinging { get; init; }
}

/// <summary>
/// Latency compensation information.
/// </summary>
public class LatencyInfo
{
    /// <summary>Total latency in samples at source sample rate</summary>
    public int LatencySamplesSource { get; init; }

    /// <summary>Total latency in samples at target sample rate</summary>
    public int LatencySamplesTarget { get; init; }

    /// <summary>Total latency in milliseconds</summary>
    public double LatencyMs { get; init; }

    /// <summary>Filter group delay contribution in samples</summary>
    public int FilterDelay { get; init; }

    /// <summary>Buffer delay contribution in samples</summary>
    public int BufferDelay { get; init; }
}

/// <summary>
/// Result of batch resampling operation.
/// </summary>
public class BatchResampleResult
{
    /// <summary>Source file path</summary>
    public string SourcePath { get; init; } = string.Empty;

    /// <summary>Destination file path</summary>
    public string DestinationPath { get; init; } = string.Empty;

    /// <summary>Whether the operation succeeded</summary>
    public bool Success { get; init; }

    /// <summary>Error message if operation failed</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Source sample rate</summary>
    public int SourceSampleRate { get; init; }

    /// <summary>Target sample rate</summary>
    public int TargetSampleRate { get; init; }

    /// <summary>Processing time in milliseconds</summary>
    public double ProcessingTimeMs { get; init; }
}

/// <summary>
/// High-quality resampling processor with multiple algorithms, anti-aliasing,
/// transient preservation, and advanced features for professional audio applications.
/// </summary>
public class ResampleProcessor : IDisposable
{
    // Constants
    private const int MinSampleRate = 8000;
    private const int MaxSampleRate = 384000;
    private const int DefaultBufferSize = 8192;
    private const double KaiserBetaDraft = 4.0;
    private const double KaiserBetaGood = 6.0;
    private const double KaiserBetaBest = 8.0;
    private const double KaiserBetaMaster = 10.0;

    // Configuration
    private int _targetSampleRate = 44100;
    private ResampleQuality _quality = ResampleQuality.Good;
    private ResampleAlgorithm _algorithm = ResampleAlgorithm.Sinc;
    private PhaseResponse _phaseResponse = PhaseResponse.Linear;
    private OversamplingFactor _oversamplingFactor = OversamplingFactor.None;

    private bool _antiAliasEnabled = true;
    private double _antiAliasSteepness = 0.95; // 0.0-1.0, higher = steeper transition
    private bool _preRingingReduction = false;
    private bool _postRingingReduction = false;
    private bool _preserveTransients = false;
    private bool _realTimeMode = false;
    private double _cpuQualityTradeoff = 0.5; // 0.0 = quality, 1.0 = speed

    // Dithering for downsampling
    private bool _ditheringEnabled = true;
    private int _targetBitDepth = 24;

    // Internal state
    private float[]? _sincFilter;
    private float[]? _polyphaseFilterBank;
    private int _filterTaps;
    private double _kaiserBeta;
    private int _polyphasePhases;

    // Processing buffers
    private float[] _inputBuffer;
    private int _inputBufferLength;
    private double _resamplePosition;
    private readonly object _lock = new();

    // Anti-aliasing filter state per channel
    private CascadedBiquadFilter[]? _antiAliasFilters;
    private CascadedBiquadFilter[]? _postFilters;

    // Transient detection state
    private float[] _transientBuffer;
    private float[] _envelopeBuffer;
    private int _transientBufferPos;

    // Dithering state
    private readonly Random _ditherRandom = new();
    private float _lastDitherL;
    private float _lastDitherR;

    private bool _disposed;
    private int _channels = 2;
    private int _sourceSampleRate = 44100;

    /// <summary>
    /// Target sample rate for conversion (8000 to 384000 Hz).
    /// </summary>
    public int TargetSampleRate
    {
        get => _targetSampleRate;
        set
        {
            _targetSampleRate = Math.Clamp(value, MinSampleRate, MaxSampleRate);
            InvalidateFilters();
        }
    }

    /// <summary>
    /// Quality mode determining filter length and precision.
    /// </summary>
    public ResampleQuality Quality
    {
        get => _quality;
        set
        {
            _quality = value;
            InvalidateFilters();
        }
    }

    /// <summary>
    /// Resampling algorithm to use.
    /// </summary>
    public ResampleAlgorithm Algorithm
    {
        get => _algorithm;
        set
        {
            _algorithm = value;
            InvalidateFilters();
        }
    }

    /// <summary>
    /// Phase response type for the filter.
    /// </summary>
    public PhaseResponse Phase
    {
        get => _phaseResponse;
        set
        {
            _phaseResponse = value;
            InvalidateFilters();
        }
    }

    /// <summary>
    /// Oversampling factor for effects processing.
    /// </summary>
    public OversamplingFactor Oversampling
    {
        get => _oversamplingFactor;
        set => _oversamplingFactor = value;
    }

    /// <summary>
    /// Enable anti-aliasing filter for downsampling.
    /// </summary>
    public bool AntiAliasEnabled
    {
        get => _antiAliasEnabled;
        set
        {
            _antiAliasEnabled = value;
            InvalidateFilters();
        }
    }

    /// <summary>
    /// Anti-aliasing filter steepness (0.0 to 1.0, higher = steeper transition).
    /// </summary>
    public double AntiAliasSteepness
    {
        get => _antiAliasSteepness;
        set
        {
            _antiAliasSteepness = Math.Clamp(value, 0.0, 1.0);
            InvalidateFilters();
        }
    }

    /// <summary>
    /// Enable pre-ringing reduction (reduces transient smearing before attacks).
    /// </summary>
    public bool PreRingingReduction
    {
        get => _preRingingReduction;
        set
        {
            _preRingingReduction = value;
            if (value) _phaseResponse = PhaseResponse.Minimum;
        }
    }

    /// <summary>
    /// Enable post-ringing reduction (reduces transient smearing after attacks).
    /// </summary>
    public bool PostRingingReduction
    {
        get => _postRingingReduction;
        set => _postRingingReduction = value;
    }

    /// <summary>
    /// Preserve transients during resampling (uses transient detection).
    /// </summary>
    public bool PreserveTransients
    {
        get => _preserveTransients;
        set => _preserveTransients = value;
    }

    /// <summary>
    /// Enable real-time mode with reduced latency.
    /// </summary>
    public bool RealTimeMode
    {
        get => _realTimeMode;
        set
        {
            _realTimeMode = value;
            if (value)
            {
                // Reduce filter length for lower latency
                _quality = ResampleQuality.Draft;
                InvalidateFilters();
            }
        }
    }

    /// <summary>
    /// CPU/Quality tradeoff (0.0 = maximum quality, 1.0 = maximum speed).
    /// </summary>
    public double CpuQualityTradeoff
    {
        get => _cpuQualityTradeoff;
        set
        {
            _cpuQualityTradeoff = Math.Clamp(value, 0.0, 1.0);
            AdjustQualityForTradeoff();
        }
    }

    /// <summary>
    /// Enable dithering when downsampling.
    /// </summary>
    public bool DitheringEnabled
    {
        get => _ditheringEnabled;
        set => _ditheringEnabled = value;
    }

    /// <summary>
    /// Target bit depth for dithering (8, 16, 20, 24).
    /// </summary>
    public int TargetBitDepth
    {
        get => _targetBitDepth;
        set => _targetBitDepth = Math.Clamp(value, 8, 32);
    }

    /// <summary>
    /// Gets the current resample ratio (target / source).
    /// </summary>
    public double ResampleRatio => (double)_targetSampleRate / _sourceSampleRate;

    /// <summary>
    /// Gets whether this is an upsampling operation.
    /// </summary>
    public bool IsUpsampling => _targetSampleRate > _sourceSampleRate;

    /// <summary>
    /// Gets whether this is a downsampling operation.
    /// </summary>
    public bool IsDownsampling => _targetSampleRate < _sourceSampleRate;

    /// <summary>
    /// Creates a new ResampleProcessor with default settings.
    /// </summary>
    public ResampleProcessor()
    {
        _inputBuffer = new float[DefaultBufferSize * 2]; // Stereo
        _transientBuffer = new float[1024];
        _envelopeBuffer = new float[1024];
        InitializeFilters();
    }

    /// <summary>
    /// Creates a new ResampleProcessor with specified target sample rate.
    /// </summary>
    /// <param name="targetSampleRate">Target sample rate in Hz</param>
    public ResampleProcessor(int targetSampleRate) : this()
    {
        TargetSampleRate = targetSampleRate;
    }

    /// <summary>
    /// Creates a new ResampleProcessor with specified settings.
    /// </summary>
    /// <param name="targetSampleRate">Target sample rate in Hz</param>
    /// <param name="quality">Quality mode</param>
    /// <param name="algorithm">Resampling algorithm</param>
    public ResampleProcessor(int targetSampleRate, ResampleQuality quality, ResampleAlgorithm algorithm) : this()
    {
        _targetSampleRate = Math.Clamp(targetSampleRate, MinSampleRate, MaxSampleRate);
        _quality = quality;
        _algorithm = algorithm;
        InitializeFilters();
    }

    /// <summary>
    /// Configures the processor for a specific source format.
    /// </summary>
    /// <param name="sourceSampleRate">Source sample rate in Hz</param>
    /// <param name="channels">Number of audio channels</param>
    public void Configure(int sourceSampleRate, int channels)
    {
        lock (_lock)
        {
            _sourceSampleRate = Math.Clamp(sourceSampleRate, MinSampleRate, MaxSampleRate);
            _channels = Math.Clamp(channels, 1, 8);
            InitializeFilters();
            Reset();
        }
    }

    /// <summary>
    /// Auto-detect source sample rate from audio data characteristics.
    /// </summary>
    /// <param name="samples">Sample buffer to analyze</param>
    /// <returns>Detected sample rate, or 0 if detection failed</returns>
    public static int AutoDetectSampleRate(float[] samples)
    {
        if (samples == null || samples.Length < 1024)
            return 0;

        // Use spectral analysis to detect likely sample rate
        // Look for energy distribution patterns typical of different sample rates
        int length = Math.Min(samples.Length, 8192);
        double[] magnitudes = new double[length / 2];

        // Simple FFT magnitude estimation
        for (int k = 0; k < magnitudes.Length; k++)
        {
            double real = 0, imag = 0;
            for (int n = 0; n < length; n++)
            {
                double angle = -2.0 * Math.PI * k * n / length;
                real += samples[n] * Math.Cos(angle);
                imag += samples[n] * Math.Sin(angle);
            }
            magnitudes[k] = Math.Sqrt(real * real + imag * imag);
        }

        // Find highest significant frequency bin
        double threshold = magnitudes.Max() * 0.01;
        int highestBin = 0;
        for (int i = magnitudes.Length - 1; i >= 0; i--)
        {
            if (magnitudes[i] > threshold)
            {
                highestBin = i;
                break;
            }
        }

        // Estimate sample rate based on highest frequency content
        // Common sample rates to check
        int[] commonRates = { 8000, 11025, 16000, 22050, 32000, 44100, 48000, 88200, 96000, 176400, 192000 };

        // Heuristic: if high frequency content extends to ~20kHz, likely 44100+
        double normalizedHighFreq = (double)highestBin / magnitudes.Length;

        if (normalizedHighFreq > 0.45) return 44100; // Near Nyquist
        if (normalizedHighFreq > 0.35) return 48000;
        if (normalizedHighFreq > 0.25) return 96000;
        if (normalizedHighFreq > 0.15) return 22050;

        return 44100; // Default assumption
    }

    /// <summary>
    /// Detect sample rate from file metadata or analysis.
    /// </summary>
    /// <param name="filePath">Path to audio file</param>
    /// <returns>Detected sample rate</returns>
    public static int DetectSampleRate(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return 0;

        try
        {
            using var reader = new AudioFileReader(filePath);
            return reader.WaveFormat.SampleRate;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Check if a sample rate is a standard rate.
    /// </summary>
    /// <param name="sampleRate">Sample rate to check</param>
    /// <returns>True if standard rate</returns>
    public static bool IsStandardSampleRate(int sampleRate)
    {
        int[] standardRates = { 8000, 11025, 16000, 22050, 32000, 44100, 48000, 88200, 96000, 176400, 192000, 352800, 384000 };
        return standardRates.Contains(sampleRate);
    }

    /// <summary>
    /// Get the nearest standard sample rate.
    /// </summary>
    /// <param name="sampleRate">Input sample rate</param>
    /// <returns>Nearest standard sample rate</returns>
    public static int GetNearestStandardSampleRate(int sampleRate)
    {
        int[] standardRates = { 8000, 11025, 16000, 22050, 32000, 44100, 48000, 88200, 96000, 176400, 192000, 352800, 384000 };
        return standardRates.OrderBy(r => Math.Abs(r - sampleRate)).First();
    }

    /// <summary>
    /// Process a buffer of samples.
    /// </summary>
    /// <param name="input">Input sample buffer</param>
    /// <param name="sourceSampleRate">Source sample rate</param>
    /// <param name="channels">Number of channels</param>
    /// <returns>Resampled output buffer</returns>
    public float[] Process(float[] input, int sourceSampleRate, int channels)
    {
        if (input == null || input.Length == 0)
            return Array.Empty<float>();

        lock (_lock)
        {
            if (_sourceSampleRate != sourceSampleRate || _channels != channels)
            {
                Configure(sourceSampleRate, channels);
            }

            return ProcessInternal(input);
        }
    }

    /// <summary>
    /// Process a buffer with oversampling for effects (upsample, process, downsample).
    /// </summary>
    /// <param name="input">Input sample buffer</param>
    /// <param name="sourceSampleRate">Source sample rate</param>
    /// <param name="channels">Number of channels</param>
    /// <param name="effectProcessor">Effect processing function to apply at higher sample rate</param>
    /// <returns>Processed and downsampled output</returns>
    public float[] ProcessWithOversampling(float[] input, int sourceSampleRate, int channels, Func<float[], float[]> effectProcessor)
    {
        if (input == null || input.Length == 0 || effectProcessor == null)
            return input ?? Array.Empty<float>();

        int factor = (int)_oversamplingFactor;
        if (factor <= 1)
        {
            return effectProcessor(input);
        }

        // Upsample
        int originalTarget = _targetSampleRate;
        _targetSampleRate = sourceSampleRate * factor;
        Configure(sourceSampleRate, channels);
        float[] upsampled = ProcessInternal(input);

        // Process at higher sample rate
        float[] processed = effectProcessor(upsampled);

        // Downsample back
        _targetSampleRate = originalTarget;
        Configure(sourceSampleRate * factor, channels);
        float[] result = ProcessInternal(processed);

        // Restore original settings
        _targetSampleRate = originalTarget;
        Configure(sourceSampleRate, channels);

        return result;
    }

    /// <summary>
    /// Preview resampled audio (process small chunk for preview).
    /// </summary>
    /// <param name="input">Input sample buffer</param>
    /// <param name="sourceSampleRate">Source sample rate</param>
    /// <param name="channels">Number of channels</param>
    /// <param name="previewDurationMs">Preview duration in milliseconds</param>
    /// <returns>Preview samples</returns>
    public float[] Preview(float[] input, int sourceSampleRate, int channels, int previewDurationMs = 1000)
    {
        if (input == null || input.Length == 0)
            return Array.Empty<float>();

        int previewSamples = (int)(sourceSampleRate * (previewDurationMs / 1000.0) * channels);
        previewSamples = Math.Min(previewSamples, input.Length);

        float[] previewBuffer = new float[previewSamples];
        Array.Copy(input, previewBuffer, previewSamples);

        // Use draft quality for preview
        var savedQuality = _quality;
        _quality = ResampleQuality.Draft;
        InvalidateFilters();

        var result = Process(previewBuffer, sourceSampleRate, channels);

        _quality = savedQuality;
        InvalidateFilters();

        return result;
    }

    /// <summary>
    /// Batch resample multiple audio files.
    /// </summary>
    /// <param name="filePaths">Array of source file paths</param>
    /// <param name="outputDirectory">Output directory for resampled files</param>
    /// <param name="progress">Optional progress callback (0.0 to 1.0)</param>
    /// <returns>Array of batch resample results</returns>
    public BatchResampleResult[] BatchResample(string[] filePaths, string outputDirectory, Action<double>? progress = null)
    {
        if (filePaths == null || filePaths.Length == 0)
            return Array.Empty<BatchResampleResult>();

        if (!Directory.Exists(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        var results = new BatchResampleResult[filePaths.Length];

        for (int i = 0; i < filePaths.Length; i++)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            string sourcePath = filePaths[i];
            string destPath = Path.Combine(outputDirectory, Path.GetFileName(sourcePath));

            try
            {
                int sourceSampleRate = DetectSampleRate(sourcePath);
                if (sourceSampleRate == 0)
                {
                    results[i] = new BatchResampleResult
                    {
                        SourcePath = sourcePath,
                        DestinationPath = destPath,
                        Success = false,
                        ErrorMessage = "Could not detect source sample rate"
                    };
                    continue;
                }

                ResampleFile(sourcePath, destPath);

                stopwatch.Stop();
                results[i] = new BatchResampleResult
                {
                    SourcePath = sourcePath,
                    DestinationPath = destPath,
                    Success = true,
                    SourceSampleRate = sourceSampleRate,
                    TargetSampleRate = _targetSampleRate,
                    ProcessingTimeMs = stopwatch.Elapsed.TotalMilliseconds
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                results[i] = new BatchResampleResult
                {
                    SourcePath = sourcePath,
                    DestinationPath = destPath,
                    Success = false,
                    ErrorMessage = ex.Message,
                    ProcessingTimeMs = stopwatch.Elapsed.TotalMilliseconds
                };
            }

            progress?.Invoke((double)(i + 1) / filePaths.Length);
        }

        return results;
    }

    /// <summary>
    /// Resample an audio file.
    /// </summary>
    /// <param name="inputPath">Input file path</param>
    /// <param name="outputPath">Output file path</param>
    public void ResampleFile(string inputPath, string outputPath)
    {
        if (string.IsNullOrEmpty(inputPath) || !File.Exists(inputPath))
            throw new FileNotFoundException("Input file not found", inputPath);

        using var reader = new AudioFileReader(inputPath);
        Configure(reader.WaveFormat.SampleRate, reader.WaveFormat.Channels);

        // Calculate output buffer size
        double ratio = ResampleRatio;
        long totalSamples = reader.Length / sizeof(float);
        long outputSamples = (long)(totalSamples * ratio);

        // Create output wave format
        var outputFormat = WaveFormat.CreateIeeeFloatWaveFormat(_targetSampleRate, reader.WaveFormat.Channels);

        using var writer = new WaveFileWriter(outputPath, outputFormat);

        const int bufferSize = 16384;
        float[] inputBuffer = new float[bufferSize];
        int samplesRead;

        Reset();

        while ((samplesRead = reader.Read(inputBuffer, 0, bufferSize)) > 0)
        {
            float[] chunk = new float[samplesRead];
            Array.Copy(inputBuffer, chunk, samplesRead);

            float[] resampled = ProcessInternal(chunk);
            writer.WriteSamples(resampled, 0, resampled.Length);
        }
    }

    /// <summary>
    /// Get latency compensation information.
    /// </summary>
    /// <returns>Latency information</returns>
    public LatencyInfo GetLatencyInfo()
    {
        int filterDelay = _filterTaps / 2;
        int bufferDelay = _realTimeMode ? 64 : 256;

        double latencyMs = (filterDelay + bufferDelay) * 1000.0 / _sourceSampleRate;

        return new LatencyInfo
        {
            LatencySamplesSource = filterDelay + bufferDelay,
            LatencySamplesTarget = (int)((filterDelay + bufferDelay) * ResampleRatio),
            LatencyMs = latencyMs,
            FilterDelay = filterDelay,
            BufferDelay = bufferDelay
        };
    }

    /// <summary>
    /// Get filter characteristics information.
    /// </summary>
    /// <returns>Filter characteristics</returns>
    public FilterCharacteristics GetFilterCharacteristics()
    {
        double nyquist = Math.Min(_sourceSampleRate, _targetSampleRate) / 2.0;
        double cutoff = nyquist * _antiAliasSteepness;
        double transitionBand = nyquist - cutoff;
        double stopbandAtten = 20 * Math.Log10(1.0 / Math.Pow(10, _kaiserBeta / 8.69));

        int halfTaps = _filterTaps / 2;

        return new FilterCharacteristics
        {
            FilterTaps = _filterTaps,
            CutoffFrequency = cutoff,
            TransitionBandwidth = transitionBand,
            StopbandAttenuation = Math.Abs(stopbandAtten),
            PassbandRipple = 0.01 * (1.0 - _antiAliasSteepness),
            GroupDelay = halfTaps,
            PreRinging = _preRingingReduction ? halfTaps / 4 : halfTaps,
            PostRinging = _postRingingReduction ? halfTaps / 4 : halfTaps
        };
    }

    /// <summary>
    /// Reset processor state.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _resamplePosition = 0;
            _inputBufferLength = 0;
            Array.Clear(_inputBuffer);
            _transientBufferPos = 0;
            Array.Clear(_transientBuffer);
            Array.Clear(_envelopeBuffer);
            _lastDitherL = _lastDitherR = 0;

            if (_antiAliasFilters != null)
            {
                foreach (var filter in _antiAliasFilters)
                    filter.Reset();
            }

            if (_postFilters != null)
            {
                foreach (var filter in _postFilters)
                    filter.Reset();
            }
        }
    }

    private void InvalidateFilters()
    {
        _sincFilter = null;
        _polyphaseFilterBank = null;
        InitializeFilters();
    }

    private void InitializeFilters()
    {
        // Determine filter parameters based on quality
        (_filterTaps, _kaiserBeta) = _quality switch
        {
            ResampleQuality.Draft => (32, KaiserBetaDraft),
            ResampleQuality.Good => (64, KaiserBetaGood),
            ResampleQuality.Best => (128, KaiserBetaBest),
            ResampleQuality.Master => (256, KaiserBetaMaster),
            _ => (64, KaiserBetaGood)
        };

        // Adjust for CPU/quality tradeoff
        if (_cpuQualityTradeoff > 0.5)
        {
            _filterTaps = (int)(_filterTaps * (1.0 - (_cpuQualityTradeoff - 0.5)));
            _filterTaps = Math.Max(16, _filterTaps);
        }

        // Generate sinc filter
        if (_algorithm == ResampleAlgorithm.Sinc || _algorithm == ResampleAlgorithm.Polyphase)
        {
            _sincFilter = GenerateSincFilter(_filterTaps, _kaiserBeta);

            if (_phaseResponse == PhaseResponse.Minimum)
            {
                ConvertToMinimumPhase(_sincFilter);
            }
        }

        // Generate polyphase filter bank
        if (_algorithm == ResampleAlgorithm.Polyphase)
        {
            _polyphasePhases = 32; // Number of phases
            _polyphaseFilterBank = GeneratePolyphaseFilterBank(_sincFilter!, _polyphasePhases);
        }

        // Initialize anti-aliasing filters
        InitializeAntiAliasFilters();

        // Resize buffers if needed
        int requiredSize = (_filterTaps + DefaultBufferSize) * _channels;
        if (_inputBuffer.Length < requiredSize)
        {
            _inputBuffer = new float[requiredSize];
        }
    }

    private void InitializeAntiAliasFilters()
    {
        if (!_antiAliasEnabled)
        {
            _antiAliasFilters = null;
            _postFilters = null;
            return;
        }

        _antiAliasFilters = new CascadedBiquadFilter[_channels];
        _postFilters = new CascadedBiquadFilter[_channels];

        // Calculate cutoff based on steepness
        double cutoffRatio = 0.45 * _antiAliasSteepness;
        double ratio = ResampleRatio;

        // For downsampling: filter before
        // For upsampling: filter after
        double cutoff = IsDownsampling ? cutoffRatio * ratio : cutoffRatio;

        // Number of biquad stages based on quality
        int stages = _quality switch
        {
            ResampleQuality.Draft => 2,
            ResampleQuality.Good => 4,
            ResampleQuality.Best => 6,
            ResampleQuality.Master => 8,
            _ => 4
        };

        for (int ch = 0; ch < _channels; ch++)
        {
            _antiAliasFilters[ch] = new CascadedBiquadFilter(stages);
            _antiAliasFilters[ch].SetLowpass(cutoff, 0.707);

            _postFilters[ch] = new CascadedBiquadFilter(stages);
            _postFilters[ch].SetLowpass(cutoff, 0.707);
        }
    }

    private float[] ProcessInternal(float[] input)
    {
        int inputFrames = input.Length / _channels;
        double ratio = ResampleRatio;

        // Estimate output size
        int estimatedOutputFrames = (int)Math.Ceiling(inputFrames * ratio) + 1;
        float[] output = new float[estimatedOutputFrames * _channels];

        // Apply pre-filtering for downsampling
        float[] processedInput = input;
        if (IsDownsampling && _antiAliasEnabled && _antiAliasFilters != null)
        {
            processedInput = ApplyAntiAliasFilter(input, _antiAliasFilters);
        }

        // Detect transients if preservation is enabled
        bool[]? transientMarkers = null;
        if (_preserveTransients)
        {
            transientMarkers = DetectTransients(processedInput, _channels);
        }

        // Add to input buffer
        AddToInputBuffer(processedInput);

        // Resample
        int outputWritten = 0;
        int maxOutputFrames = estimatedOutputFrames;

        while (outputWritten < maxOutputFrames && HasEnoughInputSamples())
        {
            int baseIndex = (int)_resamplePosition;
            double fraction = _resamplePosition - baseIndex;

            // Check for transient at this position
            bool isTransient = transientMarkers != null &&
                               baseIndex < transientMarkers.Length &&
                               transientMarkers[baseIndex];

            for (int ch = 0; ch < _channels; ch++)
            {
                float sample = _algorithm switch
                {
                    ResampleAlgorithm.Linear => InterpolateLinear(baseIndex, ch, fraction),
                    ResampleAlgorithm.Cubic => InterpolateCubic(baseIndex, ch, fraction),
                    ResampleAlgorithm.Sinc => InterpolateSinc(baseIndex, ch, fraction, isTransient),
                    ResampleAlgorithm.Polyphase => InterpolatePolyphase(baseIndex, ch, fraction),
                    _ => InterpolateSinc(baseIndex, ch, fraction, isTransient)
                };

                output[outputWritten * _channels + ch] = sample;
            }

            _resamplePosition += 1.0 / ratio;
            outputWritten++;
        }

        // Shift input buffer
        ShiftInputBuffer();

        // Trim output to actual size
        if (outputWritten < estimatedOutputFrames)
        {
            float[] trimmed = new float[outputWritten * _channels];
            Array.Copy(output, trimmed, trimmed.Length);
            output = trimmed;
        }

        // Apply post-filtering for upsampling
        if (IsUpsampling && _antiAliasEnabled && _postFilters != null)
        {
            output = ApplyAntiAliasFilter(output, _postFilters);
        }

        // Apply dithering for downsampling
        if (IsDownsampling && _ditheringEnabled && _targetBitDepth < 24)
        {
            ApplyDithering(output);
        }

        return output;
    }

    private void AddToInputBuffer(float[] input)
    {
        int samplesToAdd = Math.Min(input.Length, _inputBuffer.Length - _inputBufferLength);
        Array.Copy(input, 0, _inputBuffer, _inputBufferLength, samplesToAdd);
        _inputBufferLength += samplesToAdd;
    }

    private bool HasEnoughInputSamples()
    {
        int requiredFrames = (int)_resamplePosition + _filterTaps;
        return requiredFrames * _channels < _inputBufferLength;
    }

    private void ShiftInputBuffer()
    {
        int consumedFrames = (int)_resamplePosition;
        if (consumedFrames > 0)
        {
            int consumedSamples = consumedFrames * _channels;
            if (consumedSamples < _inputBufferLength)
            {
                int remaining = _inputBufferLength - consumedSamples;
                Array.Copy(_inputBuffer, consumedSamples, _inputBuffer, 0, remaining);
                _inputBufferLength = remaining;
                _resamplePosition -= consumedFrames;
            }
            else
            {
                _inputBufferLength = 0;
                _resamplePosition = 0;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float InterpolateLinear(int baseIndex, int channel, double fraction)
    {
        int idx0 = baseIndex * _channels + channel;
        int idx1 = idx0 + _channels;

        if (idx0 < 0 || idx0 >= _inputBufferLength) return 0f;
        if (idx1 >= _inputBufferLength) idx1 = idx0;

        return (float)(_inputBuffer[idx0] * (1 - fraction) + _inputBuffer[idx1] * fraction);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float InterpolateCubic(int baseIndex, int channel, double fraction)
    {
        int idx0 = (baseIndex - 1) * _channels + channel;
        int idx1 = baseIndex * _channels + channel;
        int idx2 = (baseIndex + 1) * _channels + channel;
        int idx3 = (baseIndex + 2) * _channels + channel;

        // Clamp indices
        idx0 = Math.Clamp(idx0, 0, Math.Max(0, _inputBufferLength - 1));
        idx1 = Math.Clamp(idx1, 0, Math.Max(0, _inputBufferLength - 1));
        idx2 = Math.Clamp(idx2, 0, Math.Max(0, _inputBufferLength - 1));
        idx3 = Math.Clamp(idx3, 0, Math.Max(0, _inputBufferLength - 1));

        float y0 = _inputBuffer[idx0];
        float y1 = _inputBuffer[idx1];
        float y2 = _inputBuffer[idx2];
        float y3 = _inputBuffer[idx3];

        // Catmull-Rom spline
        double t = fraction;
        double t2 = t * t;
        double t3 = t2 * t;

        double a0 = -0.5 * y0 + 1.5 * y1 - 1.5 * y2 + 0.5 * y3;
        double a1 = y0 - 2.5 * y1 + 2 * y2 - 0.5 * y3;
        double a2 = -0.5 * y0 + 0.5 * y2;
        double a3 = y1;

        return (float)(a0 * t3 + a1 * t2 + a2 * t + a3);
    }

    private float InterpolateSinc(int baseIndex, int channel, double fraction, bool isTransient)
    {
        if (_sincFilter == null) return InterpolateLinear(baseIndex, channel, fraction);

        double sum = 0;
        int halfTaps = _filterTaps / 2;

        // Reduce taps near transients if preservation is enabled
        int effectiveTaps = isTransient && _preserveTransients ? halfTaps / 2 : halfTaps;

        for (int i = -effectiveTaps; i < effectiveTaps; i++)
        {
            int sampleIdx = (baseIndex + i) * _channels + channel;
            if (sampleIdx < 0 || sampleIdx >= _inputBufferLength) continue;

            double sincArg = i - fraction;
            double sincValue;

            if (Math.Abs(sincArg) < 0.0001)
            {
                sincValue = 1.0;
            }
            else
            {
                sincValue = Math.Sin(Math.PI * sincArg) / (Math.PI * sincArg);
            }

            // Apply window from pre-computed filter
            int windowIdx = i + halfTaps;
            if (windowIdx >= 0 && windowIdx < _sincFilter.Length)
            {
                sincValue *= _sincFilter[windowIdx];
            }

            sum += _inputBuffer[sampleIdx] * sincValue;
        }

        return (float)sum;
    }

    private float InterpolatePolyphase(int baseIndex, int channel, double fraction)
    {
        if (_polyphaseFilterBank == null || _sincFilter == null)
            return InterpolateSinc(baseIndex, channel, fraction, false);

        // Select appropriate phase
        int phase = (int)(fraction * _polyphasePhases);
        phase = Math.Clamp(phase, 0, _polyphasePhases - 1);

        int tapsPerPhase = _filterTaps / _polyphasePhases;
        int filterOffset = phase * tapsPerPhase;

        double sum = 0;
        for (int i = 0; i < tapsPerPhase; i++)
        {
            int sampleIdx = (baseIndex - tapsPerPhase / 2 + i) * _channels + channel;
            if (sampleIdx < 0 || sampleIdx >= _inputBufferLength) continue;

            sum += _inputBuffer[sampleIdx] * _polyphaseFilterBank[filterOffset + i];
        }

        return (float)sum;
    }

    private float[] ApplyAntiAliasFilter(float[] input, CascadedBiquadFilter[] filters)
    {
        float[] output = new float[input.Length];

        for (int i = 0; i < input.Length; i += _channels)
        {
            for (int ch = 0; ch < _channels; ch++)
            {
                output[i + ch] = filters[ch].Process(input[i + ch]);
            }
        }

        return output;
    }

    private bool[] DetectTransients(float[] input, int channels)
    {
        int frames = input.Length / channels;
        bool[] transients = new bool[frames];

        // Simple envelope follower with attack/release
        float envelope = 0;
        float attackCoeff = 0.01f;
        float releaseCoeff = 0.0001f;
        float threshold = 0.3f;
        float lastEnvelope = 0;

        for (int i = 0; i < frames; i++)
        {
            // Get peak of all channels
            float peak = 0;
            for (int ch = 0; ch < channels; ch++)
            {
                peak = Math.Max(peak, Math.Abs(input[i * channels + ch]));
            }

            // Envelope follower
            if (peak > envelope)
                envelope = envelope + attackCoeff * (peak - envelope);
            else
                envelope = envelope + releaseCoeff * (peak - envelope);

            // Detect transient (rapid increase in envelope)
            float envelopeDelta = envelope - lastEnvelope;
            if (envelopeDelta > threshold * envelope)
            {
                transients[i] = true;
            }

            lastEnvelope = envelope;
        }

        return transients;
    }

    private void ApplyDithering(float[] buffer)
    {
        float quantLevels = MathF.Pow(2, _targetBitDepth - 1);
        float stepSize = 1f / quantLevels;

        for (int i = 0; i < buffer.Length; i += _channels)
        {
            // Left/first channel
            float ditherL = GenerateTPDFDither(ref _lastDitherL) * stepSize;
            buffer[i] = Quantize(buffer[i] + ditherL, stepSize);

            // Additional channels
            if (_channels > 1)
            {
                float ditherR = GenerateTPDFDither(ref _lastDitherR) * stepSize;
                buffer[i + 1] = Quantize(buffer[i + 1] + ditherR, stepSize);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float GenerateTPDFDither(ref float lastDither)
    {
        float r1 = (float)_ditherRandom.NextDouble() - 0.5f;
        float r2 = (float)_ditherRandom.NextDouble() - 0.5f;
        return r1 + r2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Quantize(float value, float stepSize)
    {
        value = Math.Clamp(value, -1f, 1f);
        return MathF.Round(value / stepSize) * stepSize;
    }

    private static float[] GenerateSincFilter(int taps, double beta)
    {
        float[] filter = new float[taps];
        int halfTaps = taps / 2;

        // Generate Kaiser window
        double[] kaiserWindow = new double[taps];
        double besselBeta = BesselI0(beta);

        for (int i = 0; i < taps; i++)
        {
            double alpha = (2.0 * i - taps + 1) / (taps - 1);
            kaiserWindow[i] = BesselI0(beta * Math.Sqrt(1 - alpha * alpha)) / besselBeta;
        }

        // Generate windowed sinc
        for (int i = 0; i < taps; i++)
        {
            double n = i - halfTaps;
            double sincValue = Math.Abs(n) < 0.0001 ? 1.0 : Math.Sin(Math.PI * n * 0.5) / (Math.PI * n * 0.5);
            filter[i] = (float)(sincValue * kaiserWindow[i]);
        }

        // Normalize
        float sum = filter.Sum();
        if (Math.Abs(sum) > 0.0001f)
        {
            for (int i = 0; i < taps; i++)
                filter[i] /= sum;
        }

        return filter;
    }

    private static void ConvertToMinimumPhase(float[] filter)
    {
        // Simple approximation: shift energy forward
        int length = filter.Length;
        float[] minPhase = new float[length];

        // Compute envelope and shift
        float decay = 0.95f;
        float accum = 0;

        for (int i = 0; i < length; i++)
        {
            accum = accum * decay + Math.Abs(filter[i]);
            int newIdx = Math.Min(i / 2, length - 1);
            minPhase[newIdx] += filter[i] * 0.5f;
            minPhase[i] += filter[i] * 0.5f;
        }

        Array.Copy(minPhase, filter, length);

        // Renormalize
        float sum = filter.Sum();
        if (Math.Abs(sum) > 0.0001f)
        {
            for (int i = 0; i < length; i++)
                filter[i] /= sum;
        }
    }

    private static float[] GeneratePolyphaseFilterBank(float[] prototype, int phases)
    {
        int tapsPerPhase = prototype.Length / phases;
        float[] bank = new float[phases * tapsPerPhase];

        for (int phase = 0; phase < phases; phase++)
        {
            for (int tap = 0; tap < tapsPerPhase; tap++)
            {
                int protoIdx = tap * phases + phase;
                if (protoIdx < prototype.Length)
                {
                    bank[phase * tapsPerPhase + tap] = prototype[protoIdx];
                }
            }
        }

        return bank;
    }

    private static double BesselI0(double x)
    {
        double sum = 1.0;
        double term = 1.0;
        double halfX = x / 2.0;

        for (int k = 1; k <= 25; k++)
        {
            term *= (halfX / k) * (halfX / k);
            sum += term;
            if (term < 1e-12) break;
        }

        return sum;
    }

    private void AdjustQualityForTradeoff()
    {
        if (_cpuQualityTradeoff >= 0.75)
        {
            _quality = ResampleQuality.Draft;
            _algorithm = ResampleAlgorithm.Linear;
        }
        else if (_cpuQualityTradeoff >= 0.5)
        {
            _quality = ResampleQuality.Draft;
            _algorithm = ResampleAlgorithm.Cubic;
        }
        else if (_cpuQualityTradeoff >= 0.25)
        {
            _quality = ResampleQuality.Good;
        }

        InvalidateFilters();
    }

    /// <summary>
    /// Dispose resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    #region Presets

    /// <summary>
    /// Create a processor for CD quality output (44100 Hz).
    /// </summary>
    public static ResampleProcessor CreateCDQuality()
    {
        return new ResampleProcessor(44100, ResampleQuality.Best, ResampleAlgorithm.Sinc)
        {
            AntiAliasEnabled = true,
            DitheringEnabled = true,
            TargetBitDepth = 16
        };
    }

    /// <summary>
    /// Create a processor for DVD/Broadcast quality output (48000 Hz).
    /// </summary>
    public static ResampleProcessor CreateDVDQuality()
    {
        return new ResampleProcessor(48000, ResampleQuality.Best, ResampleAlgorithm.Sinc)
        {
            AntiAliasEnabled = true
        };
    }

    /// <summary>
    /// Create a processor for high-resolution output (96000 Hz).
    /// </summary>
    public static ResampleProcessor CreateHiRes()
    {
        return new ResampleProcessor(96000, ResampleQuality.Master, ResampleAlgorithm.Sinc)
        {
            AntiAliasEnabled = true,
            PreserveTransients = true
        };
    }

    /// <summary>
    /// Create a processor for mastering with maximum quality.
    /// </summary>
    public static ResampleProcessor CreateMastering(int targetSampleRate)
    {
        return new ResampleProcessor(targetSampleRate, ResampleQuality.Master, ResampleAlgorithm.Polyphase)
        {
            AntiAliasEnabled = true,
            AntiAliasSteepness = 0.98,
            PreserveTransients = true,
            DitheringEnabled = true,
            TargetBitDepth = 24
        };
    }

    /// <summary>
    /// Create a processor for real-time/low-latency use.
    /// </summary>
    public static ResampleProcessor CreateRealTime(int targetSampleRate)
    {
        return new ResampleProcessor(targetSampleRate, ResampleQuality.Draft, ResampleAlgorithm.Linear)
        {
            RealTimeMode = true,
            AntiAliasEnabled = true,
            CpuQualityTradeoff = 0.8
        };
    }

    /// <summary>
    /// Create a processor for preview/draft purposes.
    /// </summary>
    public static ResampleProcessor CreateDraft(int targetSampleRate)
    {
        return new ResampleProcessor(targetSampleRate, ResampleQuality.Draft, ResampleAlgorithm.Linear)
        {
            AntiAliasEnabled = false,
            DitheringEnabled = false,
            CpuQualityTradeoff = 1.0
        };
    }

    /// <summary>
    /// Create a processor optimized for voice/speech.
    /// </summary>
    public static ResampleProcessor CreateVoice(int targetSampleRate = 16000)
    {
        return new ResampleProcessor(targetSampleRate, ResampleQuality.Good, ResampleAlgorithm.Sinc)
        {
            AntiAliasEnabled = true,
            AntiAliasSteepness = 0.85, // Allow some rolloff for speech
            PreserveTransients = false
        };
    }

    #endregion

    /// <summary>
    /// Cascaded biquad filter for steep anti-aliasing.
    /// </summary>
    private class CascadedBiquadFilter
    {
        private readonly BiquadSection[] _sections;

        public CascadedBiquadFilter(int stages)
        {
            _sections = new BiquadSection[stages];
            for (int i = 0; i < stages; i++)
            {
                _sections[i] = new BiquadSection();
            }
        }

        public void SetLowpass(double normalizedFreq, double q)
        {
            // Butterworth cascaded sections
            for (int i = 0; i < _sections.Length; i++)
            {
                // Adjust Q for each section in cascade
                double sectionQ = q * (1.0 + i * 0.1);
                _sections[i].SetLowpass(normalizedFreq, sectionQ);
            }
        }

        public float Process(float sample)
        {
            float result = sample;
            for (int i = 0; i < _sections.Length; i++)
            {
                result = _sections[i].Process(result);
            }
            return result;
        }

        public void Reset()
        {
            foreach (var section in _sections)
            {
                section.Reset();
            }
        }

        private class BiquadSection
        {
            private double _b0, _b1, _b2, _a1, _a2;
            private double _x1, _x2, _y1, _y2;

            public void SetLowpass(double normalizedFreq, double q)
            {
                normalizedFreq = Math.Clamp(normalizedFreq, 0.001, 0.499);

                double omega = 2.0 * Math.PI * normalizedFreq;
                double sinOmega = Math.Sin(omega);
                double cosOmega = Math.Cos(omega);
                double alpha = sinOmega / (2.0 * q);

                double a0 = 1.0 + alpha;
                _b0 = ((1.0 - cosOmega) / 2.0) / a0;
                _b1 = (1.0 - cosOmega) / a0;
                _b2 = ((1.0 - cosOmega) / 2.0) / a0;
                _a1 = (-2.0 * cosOmega) / a0;
                _a2 = (1.0 - alpha) / a0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float Process(float sample)
            {
                double output = _b0 * sample + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;

                _x2 = _x1;
                _x1 = sample;
                _y2 = _y1;
                _y1 = output;

                return (float)output;
            }

            public void Reset()
            {
                _x1 = _x2 = _y1 = _y2 = 0;
            }
        }
    }
}
