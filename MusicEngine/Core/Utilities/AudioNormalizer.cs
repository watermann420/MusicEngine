//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Comprehensive audio normalization utility with peak, RMS, and LUFS normalization modes.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace MusicEngine.Core.Utilities;

/// <summary>
/// Normalization mode for audio processing.
/// </summary>
public enum NormalizationMode
{
    /// <summary>
    /// Normalize to target peak level (dBFS).
    /// </summary>
    Peak,

    /// <summary>
    /// Normalize to target RMS level (dB).
    /// </summary>
    RMS,

    /// <summary>
    /// Normalize to target integrated LUFS level (ITU-R BS.1770).
    /// </summary>
    LUFS
}

/// <summary>
/// Configuration options for audio normalization.
/// </summary>
public class NormalizationOptions
{
    /// <summary>
    /// Normalization mode (Peak, RMS, or LUFS).
    /// </summary>
    public NormalizationMode Mode { get; set; } = NormalizationMode.Peak;

    /// <summary>
    /// Target level for peak normalization in dBFS (default: -1 dBFS).
    /// </summary>
    public double TargetPeakDbfs { get; set; } = -1.0;

    /// <summary>
    /// Target level for RMS normalization in dB (default: -18 dB).
    /// </summary>
    public double TargetRmsDb { get; set; } = -18.0;

    /// <summary>
    /// Target level for LUFS normalization (default: -14 LUFS).
    /// </summary>
    public double TargetLufs { get; set; } = -14.0;

    /// <summary>
    /// Maximum true peak limit in dBTP (default: -1 dBTP).
    /// </summary>
    public double TruePeakLimit { get; set; } = -1.0;

    /// <summary>
    /// Additional headroom in dB below the target (default: 0).
    /// </summary>
    public double HeadroomDb { get; set; } = 0.0;

    /// <summary>
    /// Enable dual-pass mode: analyze first, then apply normalization (default: true).
    /// </summary>
    public bool DualPass { get; set; } = true;

    /// <summary>
    /// Enable real-time mode with lookahead for streaming applications.
    /// </summary>
    public bool RealTimeMode { get; set; } = false;

    /// <summary>
    /// Lookahead time in milliseconds for real-time mode (default: 5ms).
    /// </summary>
    public double LookaheadMs { get; set; } = 5.0;

    /// <summary>
    /// Normalize each channel independently (default: false - linked stereo).
    /// </summary>
    public bool PerChannelNormalization { get; set; } = false;

    /// <summary>
    /// Link stereo channels for consistent gain (default: true).
    /// </summary>
    public bool StereoLink { get; set; } = true;

    /// <summary>
    /// Remove DC offset before normalization (default: true).
    /// </summary>
    public bool RemoveDcOffset { get; set; } = true;

    /// <summary>
    /// Enable soft clipping to prevent digital overs (default: false).
    /// </summary>
    public bool SoftClip { get; set; } = false;

    /// <summary>
    /// Soft clip threshold in dBFS (default: -0.5 dBFS).
    /// </summary>
    public double SoftClipThreshold { get; set; } = -0.5;

    /// <summary>
    /// Consider loudness range (LRA) in normalization (default: false).
    /// </summary>
    public bool ConsiderLoudnessRange { get; set; } = false;

    /// <summary>
    /// Maximum loudness range (LRA) in LU before reducing gain (default: 20 LU).
    /// </summary>
    public double MaxLoudnessRange { get; set; } = 20.0;

    /// <summary>
    /// Preserve dynamics by limiting maximum gain (default: true).
    /// </summary>
    public bool PreserveDynamics { get; set; } = true;

    /// <summary>
    /// Maximum gain to apply in dB (default: 24 dB).
    /// </summary>
    public double MaxGainDb { get; set; } = 24.0;

    /// <summary>
    /// Noise floor threshold in dBFS - don't amplify below this level (default: -60 dBFS).
    /// </summary>
    public double NoiseFloorDbfs { get; set; } = -60.0;

    /// <summary>
    /// Store original gain for undo functionality (default: true).
    /// </summary>
    public bool EnableUndo { get; set; } = true;

    /// <summary>
    /// Creates default options for peak normalization.
    /// </summary>
    public static NormalizationOptions PeakDefault => new()
    {
        Mode = NormalizationMode.Peak,
        TargetPeakDbfs = -1.0,
        TruePeakLimit = -1.0
    };

    /// <summary>
    /// Creates default options for RMS normalization.
    /// </summary>
    public static NormalizationOptions RmsDefault => new()
    {
        Mode = NormalizationMode.RMS,
        TargetRmsDb = -18.0,
        TruePeakLimit = -1.0
    };

    /// <summary>
    /// Creates default options for LUFS normalization (streaming platforms).
    /// </summary>
    public static NormalizationOptions LufsStreamingDefault => new()
    {
        Mode = NormalizationMode.LUFS,
        TargetLufs = -14.0,
        TruePeakLimit = -1.0
    };

    /// <summary>
    /// Creates default options for broadcast LUFS normalization (EBU R128).
    /// </summary>
    public static NormalizationOptions LufsBroadcastDefault => new()
    {
        Mode = NormalizationMode.LUFS,
        TargetLufs = -23.0,
        TruePeakLimit = -1.0
    };
}

/// <summary>
/// Results from audio level analysis.
/// </summary>
public class AudioAnalysisResult
{
    /// <summary>
    /// Peak level in dBFS.
    /// </summary>
    public double PeakDbfs { get; set; } = double.NegativeInfinity;

    /// <summary>
    /// Peak level as linear value.
    /// </summary>
    public double PeakLinear { get; set; } = 0;

    /// <summary>
    /// True peak level in dBTP (using 4x oversampling).
    /// </summary>
    public double TruePeakDbtp { get; set; } = double.NegativeInfinity;

    /// <summary>
    /// True peak level as linear value.
    /// </summary>
    public double TruePeakLinear { get; set; } = 0;

    /// <summary>
    /// RMS level in dB.
    /// </summary>
    public double RmsDb { get; set; } = double.NegativeInfinity;

    /// <summary>
    /// RMS level as linear value.
    /// </summary>
    public double RmsLinear { get; set; } = 0;

    /// <summary>
    /// Integrated loudness in LUFS.
    /// </summary>
    public double IntegratedLufs { get; set; } = double.NegativeInfinity;

    /// <summary>
    /// Short-term loudness in LUFS (last 3 seconds).
    /// </summary>
    public double ShortTermLufs { get; set; } = double.NegativeInfinity;

    /// <summary>
    /// Momentary loudness in LUFS (last 400ms).
    /// </summary>
    public double MomentaryLufs { get; set; } = double.NegativeInfinity;

    /// <summary>
    /// Loudness range (LRA) in LU.
    /// </summary>
    public double LoudnessRangeLu { get; set; } = 0;

    /// <summary>
    /// DC offset detected.
    /// </summary>
    public double DcOffset { get; set; } = 0;

    /// <summary>
    /// Per-channel peak levels in dBFS.
    /// </summary>
    public double[] ChannelPeaks { get; set; } = Array.Empty<double>();

    /// <summary>
    /// Per-channel RMS levels in dB.
    /// </summary>
    public double[] ChannelRms { get; set; } = Array.Empty<double>();

    /// <summary>
    /// Total number of samples analyzed.
    /// </summary>
    public long SampleCount { get; set; }

    /// <summary>
    /// Duration in seconds.
    /// </summary>
    public double DurationSeconds { get; set; }
}

/// <summary>
/// Results from normalization processing.
/// </summary>
public class NormalizationResult
{
    /// <summary>
    /// Whether normalization was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gain applied in dB.
    /// </summary>
    public double GainAppliedDb { get; set; }

    /// <summary>
    /// Gain applied as linear multiplier.
    /// </summary>
    public double GainAppliedLinear { get; set; } = 1.0;

    /// <summary>
    /// Analysis results before normalization.
    /// </summary>
    public AudioAnalysisResult? BeforeAnalysis { get; set; }

    /// <summary>
    /// Analysis results after normalization (if dual-pass).
    /// </summary>
    public AudioAnalysisResult? AfterAnalysis { get; set; }

    /// <summary>
    /// Whether the signal was clipped and limited.
    /// </summary>
    public bool LimiterEngaged { get; set; }

    /// <summary>
    /// Maximum gain reduction applied by limiter in dB.
    /// </summary>
    public double MaxLimiterReductionDb { get; set; }

    /// <summary>
    /// Whether DC offset was removed.
    /// </summary>
    public bool DcOffsetRemoved { get; set; }

    /// <summary>
    /// Amount of DC offset removed.
    /// </summary>
    public double DcOffsetAmount { get; set; }

    /// <summary>
    /// Whether gain was limited due to preserve dynamics setting.
    /// </summary>
    public bool GainLimited { get; set; }

    /// <summary>
    /// Original gain value before it was stored (for undo).
    /// </summary>
    public double OriginalGainDb { get; set; }

    /// <summary>
    /// Error message if normalization failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Processing time in milliseconds.
    /// </summary>
    public double ProcessingTimeMs { get; set; }
}

/// <summary>
/// Comprehensive audio normalization utility with peak, RMS, and LUFS normalization modes.
/// Supports true peak limiting, DC offset removal, soft clipping, and batch processing.
/// </summary>
public class AudioNormalizer
{
    private readonly NormalizationOptions _options;
    private double _storedOriginalGain = 1.0;
    private bool _hasStoredGain = false;

    // K-weighting filter coefficients for LUFS measurement
    private double[]? _hsB;
    private double[]? _hsA;
    private double[]? _hpB;
    private double[]? _hpA;

    /// <summary>
    /// Creates a new AudioNormalizer with default options.
    /// </summary>
    public AudioNormalizer() : this(new NormalizationOptions())
    {
    }

    /// <summary>
    /// Creates a new AudioNormalizer with specified options.
    /// </summary>
    /// <param name="options">Normalization options.</param>
    public AudioNormalizer(NormalizationOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Gets or sets the normalization options.
    /// </summary>
    public NormalizationOptions Options => _options;

    /// <summary>
    /// Event raised during analysis/processing to report progress (0.0 to 1.0).
    /// </summary>
    public event EventHandler<double>? ProgressChanged;

    /// <summary>
    /// Analyzes audio levels without modifying the audio.
    /// </summary>
    /// <param name="source">Audio source to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Analysis results containing peak, RMS, LUFS, and other measurements.</returns>
    public async Task<AudioAnalysisResult> AnalyzeAsync(ISampleProvider source, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => Analyze(source, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Analyzes audio levels without modifying the audio.
    /// </summary>
    /// <param name="source">Audio source to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Analysis results containing peak, RMS, LUFS, and other measurements.</returns>
    public AudioAnalysisResult Analyze(ISampleProvider source, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        var result = new AudioAnalysisResult();
        int sampleRate = source.WaveFormat.SampleRate;
        int channels = source.WaveFormat.Channels;

        // Initialize K-weighting filters
        InitializeKWeightingFilters(sampleRate);

        // Filter states for K-weighting
        var hsState = new double[channels, 2];
        var hpState = new double[channels, 2];

        // LUFS measurement buffers
        const int momentaryBlockMs = 400;
        int momentarySamples = (int)(sampleRate * momentaryBlockMs / 1000.0);
        var momentaryBuffer = new double[momentarySamples];
        int momentaryWritePos = 0;
        int momentarySampleCount = 0;

        const int shortTermBlockCapacity = 30;
        var shortTermBlocks = new double[shortTermBlockCapacity];
        int shortTermBlockPos = 0;
        int shortTermBlockCount = 0;

        var gatedBlocks = new List<double>();
        var lraBlocks = new List<double>();

        // Per-channel accumulators
        var channelPeaks = new double[channels];
        var channelSumSquared = new double[channels];
        var channelDcSum = new double[channels];
        double maxTruePeak = 0;

        // True peak detector
        var truePeakDetectors = new TruePeakDetector[channels];
        for (int ch = 0; ch < channels; ch++)
        {
            truePeakDetectors[ch] = new TruePeakDetector();
        }

        long totalSamples = 0;
        float[] buffer = new float[4096];
        int samplesRead;
        long estimatedSamples = EstimateTotalSamples(source);

        while ((samplesRead = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int frames = samplesRead / channels;

            for (int frame = 0; frame < frames; frame++)
            {
                double sumSquaredKWeighted = 0;

                for (int ch = 0; ch < channels; ch++)
                {
                    float sample = buffer[frame * channels + ch];

                    // DC offset accumulation
                    channelDcSum[ch] += sample;

                    // Sample peak
                    double absSample = Math.Abs(sample);
                    if (absSample > channelPeaks[ch])
                    {
                        channelPeaks[ch] = absSample;
                    }

                    // RMS accumulation
                    channelSumSquared[ch] += sample * sample;

                    // True peak detection
                    double truePeak = truePeakDetectors[ch].ProcessSample(sample);
                    if (truePeak > maxTruePeak)
                    {
                        maxTruePeak = truePeak;
                    }

                    // K-weighting for LUFS
                    double filtered = ApplyKWeighting(sample, ch, hsState, hpState);
                    double weight = GetChannelWeight(ch, channels);
                    sumSquaredKWeighted += weight * filtered * filtered;
                }

                // LUFS measurement
                momentaryBuffer[momentaryWritePos] = sumSquaredKWeighted;
                momentaryWritePos = (momentaryWritePos + 1) % momentaryBuffer.Length;
                momentarySampleCount++;

                int hopSamples = (int)(sampleRate * 0.1);
                if (momentarySampleCount >= hopSamples)
                {
                    double momentarySum = 0;
                    for (int i = 0; i < momentaryBuffer.Length; i++)
                    {
                        momentarySum += momentaryBuffer[i];
                    }
                    double momentaryMeanSquare = momentarySum / momentaryBuffer.Length;
                    double momentaryLufs = -0.691 + 10.0 * Math.Log10(Math.Max(momentaryMeanSquare, 1e-10));

                    result.MomentaryLufs = momentaryLufs;

                    shortTermBlocks[shortTermBlockPos] = momentaryMeanSquare;
                    shortTermBlockPos = (shortTermBlockPos + 1) % shortTermBlockCapacity;
                    if (shortTermBlockCount < shortTermBlockCapacity)
                    {
                        shortTermBlockCount++;
                    }

                    if (shortTermBlockCount > 0)
                    {
                        double shortTermSum = 0;
                        for (int i = 0; i < shortTermBlockCount; i++)
                        {
                            shortTermSum += shortTermBlocks[i];
                        }
                        double shortTermMeanSquare = shortTermSum / shortTermBlockCount;
                        result.ShortTermLufs = -0.691 + 10.0 * Math.Log10(Math.Max(shortTermMeanSquare, 1e-10));
                    }

                    if (momentaryLufs > -70.0)
                    {
                        gatedBlocks.Add(momentaryMeanSquare);
                        lraBlocks.Add(momentaryLufs);
                    }

                    momentarySampleCount -= hopSamples;
                }

                totalSamples++;
            }

            // Report progress
            if (estimatedSamples > 0)
            {
                ProgressChanged?.Invoke(this, Math.Min(1.0, (double)totalSamples / estimatedSamples));
            }
        }

        // Calculate final results
        double overallPeak = 0;
        double overallSumSquared = 0;
        double overallDcSum = 0;
        long samplesPerChannel = totalSamples;

        result.ChannelPeaks = new double[channels];
        result.ChannelRms = new double[channels];

        for (int ch = 0; ch < channels; ch++)
        {
            result.ChannelPeaks[ch] = 20.0 * Math.Log10(Math.Max(channelPeaks[ch], 1e-10));
            double channelRmsLinear = Math.Sqrt(channelSumSquared[ch] / Math.Max(samplesPerChannel, 1));
            result.ChannelRms[ch] = 20.0 * Math.Log10(Math.Max(channelRmsLinear, 1e-10));

            if (channelPeaks[ch] > overallPeak)
            {
                overallPeak = channelPeaks[ch];
            }
            overallSumSquared += channelSumSquared[ch];
            overallDcSum += channelDcSum[ch];
        }

        result.PeakLinear = overallPeak;
        result.PeakDbfs = 20.0 * Math.Log10(Math.Max(overallPeak, 1e-10));

        result.TruePeakLinear = maxTruePeak;
        result.TruePeakDbtp = 20.0 * Math.Log10(Math.Max(maxTruePeak, 1e-10));

        double overallRmsLinear = Math.Sqrt(overallSumSquared / Math.Max(samplesPerChannel * channels, 1));
        result.RmsLinear = overallRmsLinear;
        result.RmsDb = 20.0 * Math.Log10(Math.Max(overallRmsLinear, 1e-10));

        result.DcOffset = overallDcSum / Math.Max(samplesPerChannel * channels, 1);
        result.SampleCount = totalSamples * channels;
        result.DurationSeconds = (double)totalSamples / sampleRate;

        // Calculate integrated loudness with gating
        result.IntegratedLufs = CalculateIntegratedLoudness(gatedBlocks);
        result.LoudnessRangeLu = CalculateLoudnessRange(lraBlocks);

        ProgressChanged?.Invoke(this, 1.0);

        return result;
    }

    /// <summary>
    /// Analyzes an audio file.
    /// </summary>
    /// <param name="filePath">Path to the audio file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Analysis results.</returns>
    public async Task<AudioAnalysisResult> AnalyzeFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        using var reader = new AudioFileReader(filePath);
        return await AnalyzeAsync(reader, cancellationToken);
    }

    /// <summary>
    /// Analyzes an audio file.
    /// </summary>
    /// <param name="filePath">Path to the audio file.</param>
    /// <returns>Analysis results.</returns>
    public AudioAnalysisResult AnalyzeFile(string filePath)
    {
        using var reader = new AudioFileReader(filePath);
        return Analyze(reader);
    }

    /// <summary>
    /// Normalizes audio samples in a buffer.
    /// </summary>
    /// <param name="buffer">Buffer containing audio samples to normalize in place.</param>
    /// <param name="channels">Number of channels.</param>
    /// <param name="sampleRate">Sample rate.</param>
    /// <returns>Normalization result.</returns>
    public NormalizationResult NormalizeBuffer(float[] buffer, int channels, int sampleRate)
    {
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));

        var startTime = DateTime.UtcNow;
        var result = new NormalizationResult { Success = true };

        // Wrap buffer in a sample provider for analysis
        var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        var provider = new BufferSampleProvider(buffer, waveFormat);

        // Analyze the audio
        result.BeforeAnalysis = Analyze(provider);

        // Calculate gain based on mode
        double targetLevel = _options.Mode switch
        {
            NormalizationMode.Peak => _options.TargetPeakDbfs - _options.HeadroomDb,
            NormalizationMode.RMS => _options.TargetRmsDb - _options.HeadroomDb,
            NormalizationMode.LUFS => _options.TargetLufs - _options.HeadroomDb,
            _ => _options.TargetPeakDbfs - _options.HeadroomDb
        };

        double currentLevel = _options.Mode switch
        {
            NormalizationMode.Peak => result.BeforeAnalysis.TruePeakDbtp,
            NormalizationMode.RMS => result.BeforeAnalysis.RmsDb,
            NormalizationMode.LUFS => result.BeforeAnalysis.IntegratedLufs,
            _ => result.BeforeAnalysis.TruePeakDbtp
        };

        // Check noise floor
        if (result.BeforeAnalysis.PeakDbfs < _options.NoiseFloorDbfs)
        {
            result.GainAppliedDb = 0;
            result.GainAppliedLinear = 1.0;
            result.ErrorMessage = "Signal below noise floor threshold, no normalization applied.";
            result.ProcessingTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
            return result;
        }

        // Calculate required gain
        double gainDb = targetLevel - currentLevel;

        // Apply preserve dynamics limit
        if (_options.PreserveDynamics && gainDb > _options.MaxGainDb)
        {
            gainDb = _options.MaxGainDb;
            result.GainLimited = true;
        }

        // Store original gain for undo
        if (_options.EnableUndo)
        {
            result.OriginalGainDb = -gainDb;
            _storedOriginalGain = Math.Pow(10.0, -gainDb / 20.0);
            _hasStoredGain = true;
        }

        double gainLinear = Math.Pow(10.0, gainDb / 20.0);
        result.GainAppliedDb = gainDb;
        result.GainAppliedLinear = gainLinear;

        // DC offset removal
        double dcOffset = 0;
        if (_options.RemoveDcOffset && Math.Abs(result.BeforeAnalysis.DcOffset) > 1e-6)
        {
            dcOffset = result.BeforeAnalysis.DcOffset;
            result.DcOffsetRemoved = true;
            result.DcOffsetAmount = dcOffset;
        }

        // Apply normalization to buffer
        double truePeakCeiling = Math.Pow(10.0, _options.TruePeakLimit / 20.0);
        double softClipThreshold = Math.Pow(10.0, _options.SoftClipThreshold / 20.0);
        double maxLimiterReduction = 0;
        bool limiterEngaged = false;

        int frames = buffer.Length / channels;
        for (int frame = 0; frame < frames; frame++)
        {
            // Process all channels
            for (int ch = 0; ch < channels; ch++)
            {
                int idx = frame * channels + ch;
                float sample = buffer[idx];

                // Remove DC offset
                if (_options.RemoveDcOffset)
                {
                    sample -= (float)dcOffset;
                }

                // Apply gain
                sample *= (float)gainLinear;

                // Soft clipping
                if (_options.SoftClip && Math.Abs(sample) > softClipThreshold)
                {
                    sample = SoftClip(sample, (float)softClipThreshold);
                }

                // True peak limiting
                if (Math.Abs(sample) > truePeakCeiling)
                {
                    double reduction = truePeakCeiling / Math.Abs(sample);
                    double reductionDb = 20.0 * Math.Log10(reduction);
                    if (Math.Abs(reductionDb) > Math.Abs(maxLimiterReduction))
                    {
                        maxLimiterReduction = reductionDb;
                    }
                    sample = Math.Sign(sample) * (float)truePeakCeiling;
                    limiterEngaged = true;
                }

                buffer[idx] = sample;
            }
        }

        result.LimiterEngaged = limiterEngaged;
        result.MaxLimiterReductionDb = maxLimiterReduction;

        // Verify result if dual pass
        if (_options.DualPass)
        {
            var verifyProvider = new BufferSampleProvider(buffer, waveFormat);
            result.AfterAnalysis = Analyze(verifyProvider);
        }

        result.ProcessingTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
        return result;
    }

    /// <summary>
    /// Creates a sample provider that applies real-time normalization with lookahead.
    /// </summary>
    /// <param name="source">Source audio.</param>
    /// <param name="preAnalyzedLevel">Pre-analyzed level for the target mode (dB/LUFS).</param>
    /// <returns>Normalizing sample provider.</returns>
    public ISampleProvider CreateRealTimeNormalizer(ISampleProvider source, double preAnalyzedLevel)
    {
        return new RealTimeNormalizerProvider(source, _options, preAnalyzedLevel);
    }

    /// <summary>
    /// Matches the loudness of a source to a reference track.
    /// </summary>
    /// <param name="sourceBuffer">Source buffer to normalize.</param>
    /// <param name="referenceAnalysis">Analysis of the reference track.</param>
    /// <param name="channels">Number of channels.</param>
    /// <param name="sampleRate">Sample rate.</param>
    /// <returns>Normalization result.</returns>
    public NormalizationResult MatchLoudnessToReference(float[] sourceBuffer, AudioAnalysisResult referenceAnalysis, int channels, int sampleRate)
    {
        // Temporarily set target to reference level
        double originalTarget = _options.Mode switch
        {
            NormalizationMode.Peak => _options.TargetPeakDbfs,
            NormalizationMode.RMS => _options.TargetRmsDb,
            NormalizationMode.LUFS => _options.TargetLufs,
            _ => _options.TargetPeakDbfs
        };

        double referenceLevel = _options.Mode switch
        {
            NormalizationMode.Peak => referenceAnalysis.TruePeakDbtp,
            NormalizationMode.RMS => referenceAnalysis.RmsDb,
            NormalizationMode.LUFS => referenceAnalysis.IntegratedLufs,
            _ => referenceAnalysis.TruePeakDbtp
        };

        switch (_options.Mode)
        {
            case NormalizationMode.Peak:
                _options.TargetPeakDbfs = referenceLevel;
                break;
            case NormalizationMode.RMS:
                _options.TargetRmsDb = referenceLevel;
                break;
            case NormalizationMode.LUFS:
                _options.TargetLufs = referenceLevel;
                break;
        }

        var result = NormalizeBuffer(sourceBuffer, channels, sampleRate);

        // Restore original target
        switch (_options.Mode)
        {
            case NormalizationMode.Peak:
                _options.TargetPeakDbfs = originalTarget;
                break;
            case NormalizationMode.RMS:
                _options.TargetRmsDb = originalTarget;
                break;
            case NormalizationMode.LUFS:
                _options.TargetLufs = originalTarget;
                break;
        }

        return result;
    }

    /// <summary>
    /// Batch normalizes multiple audio files.
    /// </summary>
    /// <param name="inputPaths">Paths to input audio files.</param>
    /// <param name="outputPaths">Paths for output files.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Array of normalization results for each file.</returns>
    public async Task<NormalizationResult[]> BatchNormalizeFilesAsync(
        string[] inputPaths,
        string[] outputPaths,
        CancellationToken cancellationToken = default)
    {
        if (inputPaths == null)
            throw new ArgumentNullException(nameof(inputPaths));
        if (outputPaths == null)
            throw new ArgumentNullException(nameof(outputPaths));
        if (inputPaths.Length != outputPaths.Length)
            throw new ArgumentException("Input and output path arrays must have the same length.");

        var results = new NormalizationResult[inputPaths.Length];

        for (int i = 0; i < inputPaths.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results[i] = await NormalizeFileAsync(inputPaths[i], outputPaths[i], cancellationToken);
            ProgressChanged?.Invoke(this, (double)(i + 1) / inputPaths.Length);
        }

        return results;
    }

    /// <summary>
    /// Normalizes an audio file and saves the result.
    /// </summary>
    /// <param name="inputPath">Path to input file.</param>
    /// <param name="outputPath">Path for output file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Normalization result.</returns>
    public async Task<NormalizationResult> NormalizeFileAsync(string inputPath, string outputPath, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            using var reader = new AudioFileReader(inputPath);
            int sampleRate = reader.WaveFormat.SampleRate;
            int channels = reader.WaveFormat.Channels;

            // Read entire file into buffer
            var samples = new List<float>();
            float[] tempBuffer = new float[4096];
            int samplesRead;

            while ((samplesRead = reader.Read(tempBuffer, 0, tempBuffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (int i = 0; i < samplesRead; i++)
                {
                    samples.Add(tempBuffer[i]);
                }
            }

            float[] buffer = samples.ToArray();

            // Normalize
            var result = NormalizeBuffer(buffer, channels, sampleRate);

            // Write output
            var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
            using var writer = new WaveFileWriter(outputPath, waveFormat);
            writer.WriteSamples(buffer, 0, buffer.Length);

            return result;
        }, cancellationToken);
    }

    /// <summary>
    /// Undoes the last normalization by applying the inverse gain.
    /// </summary>
    /// <param name="buffer">Buffer to undo normalization on.</param>
    /// <returns>True if undo was applied, false if no stored gain.</returns>
    public bool UndoNormalization(float[] buffer)
    {
        if (!_hasStoredGain)
            return false;

        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] *= (float)_storedOriginalGain;
        }

        _hasStoredGain = false;
        return true;
    }

    /// <summary>
    /// Gets a report of the normalization that would be applied.
    /// </summary>
    /// <param name="analysis">Analysis results to base the report on.</param>
    /// <returns>Formatted report string.</returns>
    public string GetNormalizationReport(AudioAnalysisResult analysis)
    {
        double targetLevel = _options.Mode switch
        {
            NormalizationMode.Peak => _options.TargetPeakDbfs,
            NormalizationMode.RMS => _options.TargetRmsDb,
            NormalizationMode.LUFS => _options.TargetLufs,
            _ => _options.TargetPeakDbfs
        };

        double currentLevel = _options.Mode switch
        {
            NormalizationMode.Peak => analysis.TruePeakDbtp,
            NormalizationMode.RMS => analysis.RmsDb,
            NormalizationMode.LUFS => analysis.IntegratedLufs,
            _ => analysis.TruePeakDbtp
        };

        double gainRequired = targetLevel - currentLevel - _options.HeadroomDb;

        if (_options.PreserveDynamics && gainRequired > _options.MaxGainDb)
        {
            gainRequired = _options.MaxGainDb;
        }

        var report = new System.Text.StringBuilder();
        report.AppendLine("Audio Normalization Report");
        report.AppendLine("==========================");
        report.AppendLine($"Mode: {_options.Mode}");
        report.AppendLine();
        report.AppendLine("Current Levels:");
        report.AppendLine($"  Peak: {analysis.PeakDbfs:F1} dBFS");
        report.AppendLine($"  True Peak: {analysis.TruePeakDbtp:F1} dBTP");
        report.AppendLine($"  RMS: {analysis.RmsDb:F1} dB");
        report.AppendLine($"  Integrated LUFS: {analysis.IntegratedLufs:F1} LUFS");
        report.AppendLine($"  Loudness Range: {analysis.LoudnessRangeLu:F1} LU");
        report.AppendLine($"  DC Offset: {analysis.DcOffset:E2}");
        report.AppendLine();
        report.AppendLine($"Target Level ({_options.Mode}): {targetLevel:F1}");
        report.AppendLine($"Headroom: {_options.HeadroomDb:F1} dB");
        report.AppendLine($"True Peak Limit: {_options.TruePeakLimit:F1} dBTP");
        report.AppendLine();
        report.AppendLine($"Gain to Apply: {gainRequired:+0.0;-0.0;0.0} dB");

        if (_options.PreserveDynamics && gainRequired >= _options.MaxGainDb)
        {
            report.AppendLine($"  (Limited by max gain setting of {_options.MaxGainDb:F1} dB)");
        }

        if (_options.RemoveDcOffset && Math.Abs(analysis.DcOffset) > 1e-6)
        {
            report.AppendLine($"  DC offset will be removed");
        }

        return report.ToString();
    }

    #region Private Methods

    private void InitializeKWeightingFilters(int sampleRate)
    {
        (_hsB, _hsA) = CalculateHighShelfCoefficients(sampleRate);
        (_hpB, _hpA) = CalculateHighPassCoefficients(sampleRate);
    }

    private double ApplyKWeighting(float sample, int channel, double[,] hsState, double[,] hpState)
    {
        if (_hsB == null || _hsA == null || _hpB == null || _hpA == null)
            return sample;

        double x = sample;
        double y1 = _hsB[0] * x + hsState[channel, 0];
        hsState[channel, 0] = _hsB[1] * x - _hsA[1] * y1 + hsState[channel, 1];
        hsState[channel, 1] = _hsB[2] * x - _hsA[2] * y1;

        double y2 = _hpB[0] * y1 + hpState[channel, 0];
        hpState[channel, 0] = _hpB[1] * y1 - _hpA[1] * y2 + hpState[channel, 1];
        hpState[channel, 1] = _hpB[2] * y1 - _hpA[2] * y2;

        return y2;
    }

    private static double GetChannelWeight(int channel, int totalChannels)
    {
        if (totalChannels <= 2)
            return 1.0;

        return channel switch
        {
            0 => 1.0,
            1 => 1.0,
            2 => 1.0,
            3 => 0.0,
            4 => 1.41,
            5 => 1.41,
            _ => 1.0
        };
    }

    private static double CalculateIntegratedLoudness(List<double> gatedBlocks)
    {
        if (gatedBlocks.Count == 0)
            return double.NegativeInfinity;

        double ungatedSum = 0;
        foreach (var block in gatedBlocks)
        {
            ungatedSum += block;
        }
        double ungatedLoudness = -0.691 + 10.0 * Math.Log10(ungatedSum / gatedBlocks.Count);

        double relativeThreshold = ungatedLoudness - 10.0;
        double gatedSum = 0;
        int gatedCount = 0;

        foreach (var block in gatedBlocks)
        {
            double blockLoudness = -0.691 + 10.0 * Math.Log10(Math.Max(block, 1e-10));
            if (blockLoudness > relativeThreshold)
            {
                gatedSum += block;
                gatedCount++;
            }
        }

        if (gatedCount > 0)
        {
            return -0.691 + 10.0 * Math.Log10(gatedSum / gatedCount);
        }

        return double.NegativeInfinity;
    }

    private static double CalculateLoudnessRange(List<double> lraBlocks)
    {
        if (lraBlocks.Count < 2)
            return 0;

        var sorted = new List<double>(lraBlocks);
        sorted.Sort();

        double ungatedSum = 0;
        foreach (var block in sorted)
        {
            ungatedSum += Math.Pow(10.0, (block + 0.691) / 10.0);
        }
        double ungatedLoudness = -0.691 + 10.0 * Math.Log10(ungatedSum / sorted.Count);
        double relativeThreshold = ungatedLoudness - 20.0;

        var gated = new List<double>();
        foreach (var block in sorted)
        {
            if (block > relativeThreshold)
            {
                gated.Add(block);
            }
        }

        if (gated.Count < 2)
            return 0;

        int lowIndex = (int)(gated.Count * 0.10);
        int highIndex = (int)(gated.Count * 0.95);

        lowIndex = Math.Clamp(lowIndex, 0, gated.Count - 1);
        highIndex = Math.Clamp(highIndex, 0, gated.Count - 1);

        return gated[highIndex] - gated[lowIndex];
    }

    private static (double[] b, double[] a) CalculateHighShelfCoefficients(int sampleRate)
    {
        double fc = 1681.974450955533;
        double G = 3.999843853973347;
        double Q = 0.7071752369554196;

        double K = Math.Tan(Math.PI * fc / sampleRate);
        double Vh = Math.Pow(10.0, G / 20.0);
        double Vb = Math.Pow(Vh, 0.4996667741545416);

        double a0 = 1.0 + K / Q + K * K;
        var b = new double[3];
        var a = new double[3];

        b[0] = (Vh + Vb * K / Q + K * K) / a0;
        b[1] = 2.0 * (K * K - Vh) / a0;
        b[2] = (Vh - Vb * K / Q + K * K) / a0;
        a[0] = 1.0;
        a[1] = 2.0 * (K * K - 1.0) / a0;
        a[2] = (1.0 - K / Q + K * K) / a0;

        return (b, a);
    }

    private static (double[] b, double[] a) CalculateHighPassCoefficients(int sampleRate)
    {
        double fc = 38.13547087602444;
        double Q = 0.5003270373238773;

        double K = Math.Tan(Math.PI * fc / sampleRate);

        double a0 = 1.0 + K / Q + K * K;
        var b = new double[3];
        var a = new double[3];

        b[0] = 1.0 / a0;
        b[1] = -2.0 / a0;
        b[2] = 1.0 / a0;
        a[0] = 1.0;
        a[1] = 2.0 * (K * K - 1.0) / a0;
        a[2] = (1.0 - K / Q + K * K) / a0;

        return (b, a);
    }

    private static float SoftClip(float sample, float threshold)
    {
        float absValue = Math.Abs(sample);
        if (absValue <= threshold)
            return sample;

        float sign = Math.Sign(sample);
        float excess = absValue - threshold;
        float range = 1.0f - threshold;

        if (range <= 0)
            return sign * threshold;

        float normalized = excess / range;
        float compressed = (float)(1.0 - Math.Exp(-normalized));

        return sign * (threshold + compressed * range);
    }

    private static long EstimateTotalSamples(ISampleProvider source)
    {
        if (source is AudioFileReader audioFileReader)
        {
            return audioFileReader.Length / (audioFileReader.WaveFormat.BitsPerSample / 8);
        }
        return 10_000_000;
    }

    #endregion

    #region Nested Classes

    /// <summary>
    /// True peak detector with 4x oversampling.
    /// </summary>
    private class TruePeakDetector
    {
        private static readonly double[] OversamplingCoeffs = GenerateOversamplingCoeffs();
        private readonly double[] _history;
        private int _historyPos;

        public TruePeakDetector()
        {
            _history = new double[OversamplingCoeffs.Length / 4];
        }

        public void Reset()
        {
            Array.Clear(_history, 0, _history.Length);
            _historyPos = 0;
        }

        public double ProcessSample(float sample)
        {
            _history[_historyPos] = sample;
            _historyPos = (_historyPos + 1) % _history.Length;

            double maxPeak = Math.Abs(sample);

            for (int phase = 0; phase < 4; phase++)
            {
                double interpolated = 0;
                int coeffIndex = phase;
                int histIndex = _historyPos;

                for (int i = 0; i < _history.Length; i++)
                {
                    histIndex--;
                    if (histIndex < 0) histIndex = _history.Length - 1;

                    if (coeffIndex < OversamplingCoeffs.Length)
                    {
                        interpolated += _history[histIndex] * OversamplingCoeffs[coeffIndex];
                    }
                    coeffIndex += 4;
                }

                double absPeak = Math.Abs(interpolated);
                if (absPeak > maxPeak)
                {
                    maxPeak = absPeak;
                }
            }

            return maxPeak;
        }

        private static double[] GenerateOversamplingCoeffs()
        {
            const int taps = 48;
            const int oversampleFactor = 4;
            var coeffs = new double[taps];

            for (int i = 0; i < taps; i++)
            {
                double n = i - (taps - 1) / 2.0;
                double sincArg = n / oversampleFactor;
                double sinc = Math.Abs(sincArg) < 1e-10 ? 1.0 : Math.Sin(Math.PI * sincArg) / (Math.PI * sincArg);
                double window = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (taps - 1)));
                coeffs[i] = sinc * window;
            }

            double sum = 0;
            for (int phase = 0; phase < oversampleFactor; phase++)
            {
                double phaseSum = 0;
                for (int i = phase; i < taps; i += oversampleFactor)
                {
                    phaseSum += coeffs[i];
                }
                if (phaseSum > sum) sum = phaseSum;
            }

            for (int i = 0; i < taps; i++)
            {
                coeffs[i] /= sum;
            }

            return coeffs;
        }
    }

    /// <summary>
    /// Helper class to wrap a buffer as a sample provider.
    /// </summary>
    private class BufferSampleProvider : ISampleProvider
    {
        private readonly float[] _buffer;
        private int _position;

        public BufferSampleProvider(float[] buffer, WaveFormat waveFormat)
        {
            _buffer = buffer;
            WaveFormat = waveFormat;
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            int available = Math.Min(count, _buffer.Length - _position);
            if (available <= 0)
                return 0;

            Array.Copy(_buffer, _position, buffer, offset, available);
            _position += available;
            return available;
        }

        public void Reset()
        {
            _position = 0;
        }
    }

    /// <summary>
    /// Real-time normalizer with lookahead for streaming applications.
    /// </summary>
    private class RealTimeNormalizerProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly NormalizationOptions _options;
        private readonly double _preAnalyzedLevel;

        private readonly float[] _lookaheadBuffer;
        private int _lookaheadWritePos;
        private int _lookaheadReadPos;
        private readonly int _lookaheadSamples;
        private bool _lookaheadFilled;

        private double _gainLinear;
        private double _limiterEnvelope = 1.0;
        private readonly double _truePeakCeiling;

        public RealTimeNormalizerProvider(ISampleProvider source, NormalizationOptions options, double preAnalyzedLevel)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _preAnalyzedLevel = preAnalyzedLevel;

            _lookaheadSamples = (int)(source.WaveFormat.SampleRate * options.LookaheadMs / 1000.0) * source.WaveFormat.Channels;
            _lookaheadBuffer = new float[_lookaheadSamples];

            double targetLevel = options.Mode switch
            {
                NormalizationMode.Peak => options.TargetPeakDbfs - options.HeadroomDb,
                NormalizationMode.RMS => options.TargetRmsDb - options.HeadroomDb,
                NormalizationMode.LUFS => options.TargetLufs - options.HeadroomDb,
                _ => options.TargetPeakDbfs - options.HeadroomDb
            };

            double gainDb = targetLevel - preAnalyzedLevel;
            if (options.PreserveDynamics && gainDb > options.MaxGainDb)
            {
                gainDb = options.MaxGainDb;
            }

            _gainLinear = Math.Pow(10.0, gainDb / 20.0);
            _truePeakCeiling = Math.Pow(10.0, options.TruePeakLimit / 20.0);
        }

        public WaveFormat WaveFormat => _source.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = _source.Read(buffer, offset, count);
            if (samplesRead == 0)
            {
                // Flush lookahead buffer
                if (_lookaheadFilled)
                {
                    int remaining = _lookaheadSamples;
                    int toCopy = Math.Min(remaining, count);
                    for (int i = 0; i < toCopy; i++)
                    {
                        buffer[offset + i] = _lookaheadBuffer[(_lookaheadReadPos + i) % _lookaheadSamples];
                    }
                    _lookaheadReadPos = (_lookaheadReadPos + toCopy) % _lookaheadSamples;
                    _lookaheadFilled = false;
                    return toCopy;
                }
                return 0;
            }

            int channels = WaveFormat.Channels;
            double releaseCoeff = Math.Exp(-1.0 / (_source.WaveFormat.SampleRate * 0.05));

            for (int i = 0; i < samplesRead; i++)
            {
                float inputSample = buffer[offset + i];

                // Apply gain
                float amplified = inputSample * (float)_gainLinear;

                // Calculate limiter target
                double absAmplified = Math.Abs(amplified);
                double targetGain = 1.0;
                if (absAmplified > _truePeakCeiling)
                {
                    targetGain = _truePeakCeiling / absAmplified;
                }

                // Smooth limiter envelope
                if (targetGain < _limiterEnvelope)
                {
                    _limiterEnvelope = targetGain;
                }
                else
                {
                    _limiterEnvelope = targetGain + releaseCoeff * (_limiterEnvelope - targetGain);
                }

                // Read from lookahead buffer
                float outputSample = _lookaheadBuffer[_lookaheadReadPos];

                // Write to lookahead buffer
                _lookaheadBuffer[_lookaheadWritePos] = amplified * (float)_limiterEnvelope;

                // Update positions
                _lookaheadWritePos = (_lookaheadWritePos + 1) % _lookaheadSamples;
                _lookaheadReadPos = (_lookaheadReadPos + 1) % _lookaheadSamples;

                if (!_lookaheadFilled && _lookaheadWritePos == 0)
                {
                    _lookaheadFilled = true;
                }

                buffer[offset + i] = _lookaheadFilled ? outputSample : 0;
            }

            return samplesRead;
        }
    }

    #endregion
}

/// <summary>
/// Extension methods for AudioNormalizer.
/// </summary>
public static class AudioNormalizerExtensions
{
    /// <summary>
    /// Creates an AudioNormalizer configured for peak normalization.
    /// </summary>
    /// <param name="targetPeakDbfs">Target peak level in dBFS.</param>
    /// <returns>Configured AudioNormalizer.</returns>
    public static AudioNormalizer CreatePeakNormalizer(double targetPeakDbfs = -1.0)
    {
        return new AudioNormalizer(new NormalizationOptions
        {
            Mode = NormalizationMode.Peak,
            TargetPeakDbfs = targetPeakDbfs
        });
    }

    /// <summary>
    /// Creates an AudioNormalizer configured for RMS normalization.
    /// </summary>
    /// <param name="targetRmsDb">Target RMS level in dB.</param>
    /// <returns>Configured AudioNormalizer.</returns>
    public static AudioNormalizer CreateRmsNormalizer(double targetRmsDb = -18.0)
    {
        return new AudioNormalizer(new NormalizationOptions
        {
            Mode = NormalizationMode.RMS,
            TargetRmsDb = targetRmsDb
        });
    }

    /// <summary>
    /// Creates an AudioNormalizer configured for LUFS normalization.
    /// </summary>
    /// <param name="targetLufs">Target integrated LUFS level.</param>
    /// <returns>Configured AudioNormalizer.</returns>
    public static AudioNormalizer CreateLufsNormalizer(double targetLufs = -14.0)
    {
        return new AudioNormalizer(new NormalizationOptions
        {
            Mode = NormalizationMode.LUFS,
            TargetLufs = targetLufs
        });
    }

    /// <summary>
    /// Creates an AudioNormalizer configured for broadcast standards (EBU R128).
    /// </summary>
    /// <returns>Configured AudioNormalizer.</returns>
    public static AudioNormalizer CreateBroadcastNormalizer()
    {
        return new AudioNormalizer(NormalizationOptions.LufsBroadcastDefault);
    }

    /// <summary>
    /// Creates an AudioNormalizer configured for streaming platforms.
    /// </summary>
    /// <returns>Configured AudioNormalizer.</returns>
    public static AudioNormalizer CreateStreamingNormalizer()
    {
        return new AudioNormalizer(NormalizationOptions.LufsStreamingDefault);
    }
}
