//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Comprehensive audio diagnostics and quality analysis tool for detecting
// clipping, DC offset, phase issues, silence, noise floor, and crest factor measurement.

using System;
using System.Collections.Generic;
using System.Text;
using NAudio.Wave;

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Severity level for audio quality issues.
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>No issue detected.</summary>
    None,

    /// <summary>Minor issue that may not be audible.</summary>
    Minor,

    /// <summary>Moderate issue that may affect audio quality.</summary>
    Moderate,

    /// <summary>Severe issue that significantly impacts audio quality.</summary>
    Severe,

    /// <summary>Critical issue requiring immediate attention.</summary>
    Critical
}

/// <summary>
/// Represents a single diagnostic issue found in the audio.
/// </summary>
public class DiagnosticIssue
{
    /// <summary>Gets the type of diagnostic issue.</summary>
    public DiagnosticIssueType Type { get; init; }

    /// <summary>Gets the severity of the issue.</summary>
    public DiagnosticSeverity Severity { get; init; }

    /// <summary>Gets a human-readable description of the issue.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Gets the time position in seconds where the issue was detected (if applicable).</summary>
    public double? TimePositionSeconds { get; init; }

    /// <summary>Gets the channel index where the issue was detected (-1 for all channels).</summary>
    public int Channel { get; init; } = -1;

    /// <summary>Gets the measured value associated with the issue.</summary>
    public float MeasuredValue { get; init; }

    /// <summary>Gets the threshold that was exceeded (if applicable).</summary>
    public float? Threshold { get; init; }

    /// <summary>Gets a suggested fix for the issue.</summary>
    public string SuggestedFix { get; init; } = string.Empty;
}

/// <summary>
/// Types of diagnostic issues that can be detected.
/// </summary>
public enum DiagnosticIssueType
{
    /// <summary>Digital clipping (samples at or exceeding 0 dBFS).</summary>
    Clipping,

    /// <summary>Inter-sample peaks exceeding 0 dBTP.</summary>
    IntersampleClipping,

    /// <summary>DC offset in the audio signal.</summary>
    DCOffset,

    /// <summary>Phase correlation issues between channels.</summary>
    PhaseIssue,

    /// <summary>Phase cancellation in mono.</summary>
    MonoCancellation,

    /// <summary>Polarity inversion detected.</summary>
    PolarityInversion,

    /// <summary>Extended silence detected.</summary>
    Silence,

    /// <summary>High noise floor.</summary>
    HighNoiseFloor,

    /// <summary>Low dynamic range (over-compression).</summary>
    LowDynamicRange,

    /// <summary>Very high crest factor (may indicate issues).</summary>
    HighCrestFactor,

    /// <summary>Very low crest factor (over-limited).</summary>
    LowCrestFactor,

    /// <summary>Left/Right channel imbalance.</summary>
    ChannelImbalance,

    /// <summary>Audio is nearly or completely mono.</summary>
    MonoAudio,

    /// <summary>Excessive stereo width.</summary>
    ExcessiveStereoWidth,

    /// <summary>Dropout or gap detected.</summary>
    Dropout
}

/// <summary>
/// Complete audio diagnostics result.
/// </summary>
public class AudioDiagnosticsResult
{
    /// <summary>Gets whether any clipping was detected.</summary>
    public bool HasClipping { get; init; }

    /// <summary>Gets the number of clipped samples.</summary>
    public long ClippedSampleCount { get; init; }

    /// <summary>Gets the percentage of clipped samples.</summary>
    public float ClippingPercentage { get; init; }

    /// <summary>Gets the DC offset value (linear, -1 to 1).</summary>
    public float DCOffset { get; init; }

    /// <summary>Gets the DC offset severity.</summary>
    public DiagnosticSeverity DCOffsetSeverity { get; init; }

    /// <summary>Gets the stereo phase correlation (-1 to 1).</summary>
    public float PhaseCorrelation { get; init; }

    /// <summary>Gets whether phase issues exist.</summary>
    public bool HasPhaseIssues { get; init; }

    /// <summary>Gets the noise floor level in dBFS.</summary>
    public float NoiseFloorDbfs { get; init; }

    /// <summary>Gets the dynamic range in dB.</summary>
    public float DynamicRangeDb { get; init; }

    /// <summary>Gets the crest factor in dB (peak to RMS ratio).</summary>
    public float CrestFactorDb { get; init; }

    /// <summary>Gets the peak level in dBFS.</summary>
    public float PeakLevelDbfs { get; init; }

    /// <summary>Gets the RMS level in dBFS.</summary>
    public float RmsLevelDbfs { get; init; }

    /// <summary>Gets the true peak level in dBTP.</summary>
    public float TruePeakDbtp { get; init; }

    /// <summary>Gets the total silence duration in seconds.</summary>
    public double SilenceDurationSeconds { get; init; }

    /// <summary>Gets the percentage of audio that is silence.</summary>
    public float SilencePercentage { get; init; }

    /// <summary>Gets the left channel RMS level in dBFS.</summary>
    public float LeftChannelRmsDbfs { get; init; }

    /// <summary>Gets the right channel RMS level in dBFS.</summary>
    public float RightChannelRmsDbfs { get; init; }

    /// <summary>Gets the channel balance (0 = centered, negative = left heavy, positive = right heavy).</summary>
    public float ChannelBalance { get; init; }

    /// <summary>Gets the stereo width indicator (0 = mono, 1 = wide stereo).</summary>
    public float StereoWidth { get; init; }

    /// <summary>Gets the overall audio quality score (0-100).</summary>
    public float QualityScore { get; init; }

    /// <summary>Gets the list of detected issues.</summary>
    public List<DiagnosticIssue> Issues { get; init; } = new();

    /// <summary>Gets the duration of audio analyzed in seconds.</summary>
    public double DurationSeconds { get; init; }

    /// <summary>Gets the sample rate of the analyzed audio.</summary>
    public int SampleRate { get; init; }

    /// <summary>Gets the number of channels in the analyzed audio.</summary>
    public int Channels { get; init; }

    /// <summary>Gets the total number of samples analyzed.</summary>
    public long TotalSamples { get; init; }

    /// <summary>Gets the analysis timestamp.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>Gets a summary of the audio quality.</summary>
    public string QualitySummary
    {
        get
        {
            if (QualityScore >= 90) return "Excellent";
            if (QualityScore >= 75) return "Good";
            if (QualityScore >= 60) return "Fair";
            if (QualityScore >= 40) return "Poor";
            return "Critical";
        }
    }
}

/// <summary>
/// Event arguments for real-time diagnostics updates.
/// </summary>
public class DiagnosticsEventArgs : EventArgs
{
    /// <summary>Gets the current diagnostics result.</summary>
    public AudioDiagnosticsResult Result { get; }

    /// <summary>Gets the time position in seconds.</summary>
    public double TimePositionSeconds { get; }

    /// <summary>
    /// Creates new diagnostics event arguments.
    /// </summary>
    public DiagnosticsEventArgs(AudioDiagnosticsResult result, double timePositionSeconds)
    {
        Result = result;
        TimePositionSeconds = timePositionSeconds;
    }
}

/// <summary>
/// Comprehensive audio diagnostics and quality analysis tool.
/// Detects clipping, DC offset, phase issues, silence, noise floor,
/// crest factor, and other audio quality issues.
/// </summary>
/// <remarks>
/// The AudioDiagnostics analyzer provides:
/// - Clipping detection (digital and inter-sample)
/// - DC offset measurement and severity classification
/// - Stereo phase correlation analysis
/// - Noise floor estimation
/// - Dynamic range measurement
/// - Crest factor calculation
/// - Silence detection
/// - Channel balance analysis
/// - Stereo width measurement
/// - Overall quality scoring
/// - Detailed issue reporting with suggested fixes
///
/// Use cases include:
/// - Pre-mastering quality checks
/// - Audio file validation
/// - Broadcast compliance testing
/// - Recording quality assessment
/// - Mix diagnostics
/// </remarks>
public class AudioDiagnostics : ISampleProvider
{
    private readonly ISampleProvider? _source;
    private readonly int _sampleRate;
    private readonly int _channels;
    private readonly int _windowSize;
    private readonly object _lock = new();

    // Clipping detection
    private long _clippedSampleCount;
    private float _clippingThreshold = 0.99f;
    private readonly List<double> _clippingPositions = new();

    // DC offset
    private double _dcSum;
    private double _dcSumLeft;
    private double _dcSumRight;

    // Peak/RMS tracking
    private float _peakLevel;
    private float _peakLevelLeft;
    private float _peakLevelRight;
    private double _rmsSum;
    private double _rmsSumLeft;
    private double _rmsSumRight;

    // True peak (4x oversampling)
    private float _truePeakLevel;
    private readonly TruePeakDetector? _truePeakDetector;

    // Phase correlation
    private double _correlationSum;
    private double _leftSquaredSum;
    private double _rightSquaredSum;
    private double _leftRightProductSum;

    // Noise floor estimation (using histogram)
    private const int NoiseHistogramBins = 120; // -120 to 0 dBFS
    private readonly long[] _levelHistogram = new long[NoiseHistogramBins];

    // Silence detection
    private const float SilenceThresholdDbfs = -60f;
    private long _silenceSampleCount;
    private bool _inSilence;
    private long _silenceStartSample;
    private readonly List<(long start, long end)> _silenceRegions = new();

    // Dropout detection
    private const int DropoutMinSamples = 44; // ~1ms at 44.1kHz
    private int _consecutiveZeroSamples;
    private readonly List<double> _dropoutPositions = new();

    // Sample counting
    private long _totalSamplesProcessed;
    private long _frameCount;

    /// <summary>
    /// Gets the wave format (only available when wrapping a source).
    /// </summary>
    public WaveFormat WaveFormat => _source?.WaveFormat ?? WaveFormat.CreateIeeeFloatWaveFormat(_sampleRate, _channels);

    /// <summary>
    /// Gets the sample rate used for analysis.
    /// </summary>
    public int SampleRate => _sampleRate;

    /// <summary>
    /// Gets the number of channels.
    /// </summary>
    public int Channels => _channels;

    /// <summary>
    /// Gets whether clipping has been detected.
    /// </summary>
    public bool HasClipping
    {
        get
        {
            lock (_lock)
            {
                return _clippedSampleCount > 0;
            }
        }
    }

    /// <summary>
    /// Gets the current DC offset value (linear scale).
    /// </summary>
    public float DCOffset
    {
        get
        {
            lock (_lock)
            {
                return _totalSamplesProcessed > 0
                    ? (float)(_dcSum / _totalSamplesProcessed)
                    : 0f;
            }
        }
    }

    /// <summary>
    /// Gets the current stereo phase correlation (-1 to 1).
    /// Only valid for stereo audio.
    /// </summary>
    public float PhaseCorrelation
    {
        get
        {
            lock (_lock)
            {
                if (_channels < 2 || _totalSamplesProcessed == 0)
                    return 1f;

                double leftStd = Math.Sqrt(_leftSquaredSum / _totalSamplesProcessed);
                double rightStd = Math.Sqrt(_rightSquaredSum / _totalSamplesProcessed);

                if (leftStd < 1e-10 || rightStd < 1e-10)
                    return 1f;

                double covariance = _leftRightProductSum / _totalSamplesProcessed;
                return (float)(covariance / (leftStd * rightStd));
            }
        }
    }

    /// <summary>
    /// Gets the estimated noise floor in dBFS.
    /// </summary>
    public float NoiseFloorDbfs
    {
        get
        {
            lock (_lock)
            {
                return EstimateNoiseFloor();
            }
        }
    }

    /// <summary>
    /// Gets the dynamic range in dB.
    /// </summary>
    public float DynamicRangeDb
    {
        get
        {
            lock (_lock)
            {
                float peakDb = LinearToDbfs(_peakLevel);
                float noiseDb = EstimateNoiseFloor();
                return peakDb - noiseDb;
            }
        }
    }

    /// <summary>
    /// Gets or sets the clipping threshold (linear, 0-1). Default is 0.99.
    /// </summary>
    public float ClippingThreshold
    {
        get => _clippingThreshold;
        set => _clippingThreshold = Math.Clamp(value, 0.9f, 1.0f);
    }

    /// <summary>
    /// Gets or sets the phase correlation threshold for issue detection. Default is 0.0.
    /// </summary>
    public float PhaseIssueThreshold { get; set; } = 0.0f;

    /// <summary>
    /// Gets or sets the DC offset threshold (linear) for issue detection. Default is 0.01.
    /// </summary>
    public float DCOffsetThreshold { get; set; } = 0.01f;

    /// <summary>
    /// Gets or sets the minimum dynamic range (dB) before flagging as over-compressed. Default is 6.
    /// </summary>
    public float MinDynamicRangeDb { get; set; } = 6f;

    /// <summary>
    /// Gets or sets the minimum crest factor (dB) before flagging as over-limited. Default is 3.
    /// </summary>
    public float MinCrestFactorDb { get; set; } = 3f;

    /// <summary>
    /// Gets or sets the maximum crest factor (dB) before flagging as potentially problematic. Default is 20.
    /// </summary>
    public float MaxCrestFactorDb { get; set; } = 20f;

    /// <summary>
    /// Event raised when diagnostics are updated during real-time processing.
    /// </summary>
    public event EventHandler<DiagnosticsEventArgs>? DiagnosticsUpdated;

    /// <summary>
    /// Event raised when clipping is detected.
    /// </summary>
    public event EventHandler<double>? ClippingDetected;

    /// <summary>
    /// Creates a new audio diagnostics analyzer with the specified configuration.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate in Hz (default: 44100).</param>
    /// <param name="channels">Number of audio channels (default: 2).</param>
    /// <param name="windowSizeMs">Analysis window size in milliseconds (default: 100ms).</param>
    public AudioDiagnostics(int sampleRate = 44100, int channels = 2, int windowSizeMs = 100)
    {
        _sampleRate = sampleRate;
        _channels = channels;
        _windowSize = (int)(sampleRate * windowSizeMs / 1000.0);
        _truePeakDetector = new TruePeakDetector();
    }

    /// <summary>
    /// Creates a new audio diagnostics analyzer that wraps an audio source for inline analysis.
    /// </summary>
    /// <param name="source">Audio source to analyze.</param>
    /// <param name="windowSizeMs">Analysis window size in milliseconds (default: 100ms).</param>
    public AudioDiagnostics(ISampleProvider source, int windowSizeMs = 100)
        : this(source.WaveFormat.SampleRate, source.WaveFormat.Channels, windowSizeMs)
    {
        _source = source;
    }

    /// <summary>
    /// Reads audio samples, performs diagnostics, and passes through unchanged.
    /// Only available when constructed with a source.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        if (_source == null)
            throw new InvalidOperationException("Read is only available when constructed with a source.");

        int samplesRead = _source.Read(buffer, offset, count);
        if (samplesRead == 0) return 0;

        Analyze(buffer, offset, samplesRead);
        return samplesRead;
    }

    /// <summary>
    /// Analyzes audio samples for diagnostics.
    /// Call this method directly when not using the ISampleProvider interface.
    /// </summary>
    /// <param name="samples">Audio samples (interleaved if multi-channel).</param>
    /// <param name="offset">Starting offset in the array.</param>
    /// <param name="count">Number of samples to process.</param>
    public void Analyze(float[] samples, int offset, int count)
    {
        int frames = count / _channels;

        for (int frame = 0; frame < frames; frame++)
        {
            float monoSample = 0;
            float leftSample = 0;
            float rightSample = 0;
            bool frameHasClipping = false;

            for (int ch = 0; ch < _channels; ch++)
            {
                int sampleIndex = offset + frame * _channels + ch;
                float sample = samples[sampleIndex];
                float absSample = Math.Abs(sample);

                monoSample += sample;

                // Per-channel tracking for stereo
                if (ch == 0)
                {
                    leftSample = sample;
                    _dcSumLeft += sample;
                    _rmsSumLeft += sample * sample;
                    if (absSample > _peakLevelLeft)
                        _peakLevelLeft = absSample;
                }
                else if (ch == 1)
                {
                    rightSample = sample;
                    _dcSumRight += sample;
                    _rmsSumRight += sample * sample;
                    if (absSample > _peakLevelRight)
                        _peakLevelRight = absSample;
                }

                // Clipping detection
                if (absSample >= _clippingThreshold)
                {
                    lock (_lock)
                    {
                        _clippedSampleCount++;
                        if (!frameHasClipping)
                        {
                            frameHasClipping = true;
                            double timePos = (double)(_totalSamplesProcessed + frame) / _sampleRate;
                            _clippingPositions.Add(timePos);
                            ClippingDetected?.Invoke(this, timePos);
                        }
                    }
                }

                // Peak tracking
                if (absSample > _peakLevel)
                    _peakLevel = absSample;

                // True peak (simplified 4x oversampling check)
                if (_truePeakDetector != null)
                {
                    float truePeak = _truePeakDetector.ProcessSample(sample);
                    if (truePeak > _truePeakLevel)
                        _truePeakLevel = truePeak;
                }
            }

            monoSample /= _channels;
            float absMonoSample = Math.Abs(monoSample);

            // DC offset accumulation
            _dcSum += monoSample;

            // RMS accumulation
            _rmsSum += monoSample * monoSample;

            // Phase correlation (stereo only)
            if (_channels >= 2)
            {
                _leftSquaredSum += leftSample * leftSample;
                _rightSquaredSum += rightSample * rightSample;
                _leftRightProductSum += leftSample * rightSample;
            }

            // Level histogram for noise floor estimation
            float levelDbfs = LinearToDbfs(absMonoSample);
            int histBin = Math.Clamp((int)(levelDbfs + 120), 0, NoiseHistogramBins - 1);
            lock (_lock)
            {
                _levelHistogram[histBin]++;
            }

            // Silence detection
            bool isSilent = levelDbfs < SilenceThresholdDbfs;
            if (isSilent)
            {
                if (!_inSilence)
                {
                    _inSilence = true;
                    _silenceStartSample = _totalSamplesProcessed + frame;
                }
                _silenceSampleCount++;
            }
            else
            {
                if (_inSilence)
                {
                    _inSilence = false;
                    lock (_lock)
                    {
                        _silenceRegions.Add((_silenceStartSample, _totalSamplesProcessed + frame));
                    }
                }
            }

            // Dropout detection (consecutive zeros)
            if (absMonoSample < 1e-10f)
            {
                _consecutiveZeroSamples++;
            }
            else
            {
                if (_consecutiveZeroSamples >= DropoutMinSamples)
                {
                    double dropoutTime = (double)(_totalSamplesProcessed + frame - _consecutiveZeroSamples) / _sampleRate;
                    lock (_lock)
                    {
                        _dropoutPositions.Add(dropoutTime);
                    }
                }
                _consecutiveZeroSamples = 0;
            }
        }

        _totalSamplesProcessed += frames;
        _frameCount++;

        // Raise update event periodically
        if (_frameCount % 10 == 0)
        {
            var result = GetReport();
            double timePos = (double)_totalSamplesProcessed / _sampleRate;
            DiagnosticsUpdated?.Invoke(this, new DiagnosticsEventArgs(result, timePos));
        }
    }

    /// <summary>
    /// Analyzes a complete audio buffer and returns the diagnostics result.
    /// </summary>
    /// <param name="samples">Complete audio buffer (interleaved if multi-channel).</param>
    /// <returns>Complete diagnostics result.</returns>
    public AudioDiagnosticsResult AnalyzeBuffer(float[] samples)
    {
        Reset();
        Analyze(samples, 0, samples.Length);
        return GetReport();
    }

    /// <summary>
    /// Gets the current diagnostics report.
    /// </summary>
    /// <returns>Current diagnostics result with all detected issues.</returns>
    public AudioDiagnosticsResult GetReport()
    {
        lock (_lock)
        {
            // Finalize any ongoing silence region
            if (_inSilence && _silenceStartSample < _totalSamplesProcessed)
            {
                _silenceRegions.Add((_silenceStartSample, _totalSamplesProcessed));
            }

            // Calculate metrics
            float dcOffset = _totalSamplesProcessed > 0 ? (float)(_dcSum / _totalSamplesProcessed) : 0f;
            float rmsLevel = _totalSamplesProcessed > 0 ? (float)Math.Sqrt(_rmsSum / _totalSamplesProcessed) : 0f;
            float rmsLevelLeft = _totalSamplesProcessed > 0 ? (float)Math.Sqrt(_rmsSumLeft / _totalSamplesProcessed) : 0f;
            float rmsLevelRight = _totalSamplesProcessed > 0 ? (float)Math.Sqrt(_rmsSumRight / _totalSamplesProcessed) : 0f;

            float peakDbfs = LinearToDbfs(_peakLevel);
            float rmsDbfs = LinearToDbfs(rmsLevel);
            float truePeakDbtp = LinearToDbfs(_truePeakLevel);
            float crestFactorDb = peakDbfs - rmsDbfs;
            float noiseFloorDbfs = EstimateNoiseFloor();
            float dynamicRangeDb = peakDbfs - noiseFloorDbfs;

            float phaseCorrelation = CalculatePhaseCorrelation();
            float stereoWidth = (1f - phaseCorrelation) / 2f;

            float channelBalance = 0f;
            if (_channels >= 2 && rmsLevelLeft > 0 && rmsLevelRight > 0)
            {
                channelBalance = 20f * (float)Math.Log10(rmsLevelRight / rmsLevelLeft);
            }

            double silenceDuration = (double)_silenceSampleCount / _sampleRate;
            float silencePercentage = _totalSamplesProcessed > 0
                ? (float)_silenceSampleCount / _totalSamplesProcessed * 100f
                : 0f;

            float clippingPercentage = _totalSamplesProcessed > 0
                ? (float)_clippedSampleCount / _totalSamplesProcessed * 100f
                : 0f;

            // Detect issues
            var issues = DetectIssues(
                dcOffset, phaseCorrelation, noiseFloorDbfs, dynamicRangeDb,
                crestFactorDb, channelBalance, stereoWidth, clippingPercentage, silencePercentage);

            // Calculate quality score
            float qualityScore = CalculateQualityScore(issues, clippingPercentage, dcOffset,
                phaseCorrelation, dynamicRangeDb, crestFactorDb);

            return new AudioDiagnosticsResult
            {
                HasClipping = _clippedSampleCount > 0,
                ClippedSampleCount = _clippedSampleCount,
                ClippingPercentage = clippingPercentage,
                DCOffset = dcOffset,
                DCOffsetSeverity = ClassifyDCOffsetSeverity(Math.Abs(dcOffset)),
                PhaseCorrelation = phaseCorrelation,
                HasPhaseIssues = phaseCorrelation < PhaseIssueThreshold,
                NoiseFloorDbfs = noiseFloorDbfs,
                DynamicRangeDb = dynamicRangeDb,
                CrestFactorDb = crestFactorDb,
                PeakLevelDbfs = peakDbfs,
                RmsLevelDbfs = rmsDbfs,
                TruePeakDbtp = truePeakDbtp,
                SilenceDurationSeconds = silenceDuration,
                SilencePercentage = silencePercentage,
                LeftChannelRmsDbfs = LinearToDbfs(rmsLevelLeft),
                RightChannelRmsDbfs = LinearToDbfs(rmsLevelRight),
                ChannelBalance = channelBalance,
                StereoWidth = stereoWidth,
                QualityScore = qualityScore,
                Issues = issues,
                DurationSeconds = (double)_totalSamplesProcessed / _sampleRate,
                SampleRate = _sampleRate,
                Channels = _channels,
                TotalSamples = _totalSamplesProcessed
            };
        }
    }

    /// <summary>
    /// Generates a human-readable diagnostic report as a string.
    /// </summary>
    /// <returns>Formatted diagnostic report.</returns>
    public string GetReportText()
    {
        var result = GetReport();
        var sb = new StringBuilder();

        sb.AppendLine("=== Audio Diagnostics Report ===");
        sb.AppendLine();
        sb.AppendLine($"Duration: {result.DurationSeconds:F2} seconds");
        sb.AppendLine($"Sample Rate: {result.SampleRate} Hz");
        sb.AppendLine($"Channels: {result.Channels}");
        sb.AppendLine($"Total Samples: {result.TotalSamples:N0}");
        sb.AppendLine();

        sb.AppendLine("--- Level Metrics ---");
        sb.AppendLine($"Peak Level: {result.PeakLevelDbfs:F1} dBFS");
        sb.AppendLine($"True Peak: {result.TruePeakDbtp:F1} dBTP");
        sb.AppendLine($"RMS Level: {result.RmsLevelDbfs:F1} dBFS");
        sb.AppendLine($"Crest Factor: {result.CrestFactorDb:F1} dB");
        sb.AppendLine($"Dynamic Range: {result.DynamicRangeDb:F1} dB");
        sb.AppendLine($"Noise Floor: {result.NoiseFloorDbfs:F1} dBFS");
        sb.AppendLine();

        sb.AppendLine("--- Quality Issues ---");
        sb.AppendLine($"Clipping: {(result.HasClipping ? $"YES ({result.ClippedSampleCount:N0} samples, {result.ClippingPercentage:F3}%)" : "No")}");
        sb.AppendLine($"DC Offset: {result.DCOffset:F4} ({result.DCOffsetSeverity})");
        sb.AppendLine($"Phase Correlation: {result.PhaseCorrelation:F2} {(result.HasPhaseIssues ? "(ISSUE)" : "(OK)")}");
        sb.AppendLine($"Silence: {result.SilenceDurationSeconds:F2}s ({result.SilencePercentage:F1}%)");
        sb.AppendLine();

        if (result.Channels >= 2)
        {
            sb.AppendLine("--- Stereo Analysis ---");
            sb.AppendLine($"Left Channel RMS: {result.LeftChannelRmsDbfs:F1} dBFS");
            sb.AppendLine($"Right Channel RMS: {result.RightChannelRmsDbfs:F1} dBFS");
            sb.AppendLine($"Channel Balance: {result.ChannelBalance:F1} dB");
            sb.AppendLine($"Stereo Width: {result.StereoWidth:F2}");
            sb.AppendLine();
        }

        sb.AppendLine($"--- Overall Quality Score: {result.QualityScore:F0}/100 ({result.QualitySummary}) ---");
        sb.AppendLine();

        if (result.Issues.Count > 0)
        {
            sb.AppendLine("--- Detected Issues ---");
            foreach (var issue in result.Issues)
            {
                sb.AppendLine($"[{issue.Severity}] {issue.Type}: {issue.Description}");
                if (!string.IsNullOrEmpty(issue.SuggestedFix))
                {
                    sb.AppendLine($"  -> Fix: {issue.SuggestedFix}");
                }
            }
        }
        else
        {
            sb.AppendLine("No significant issues detected.");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Resets the analyzer state.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _clippedSampleCount = 0;
            _clippingPositions.Clear();

            _dcSum = 0;
            _dcSumLeft = 0;
            _dcSumRight = 0;

            _peakLevel = 0;
            _peakLevelLeft = 0;
            _peakLevelRight = 0;
            _rmsSum = 0;
            _rmsSumLeft = 0;
            _rmsSumRight = 0;
            _truePeakLevel = 0;

            _correlationSum = 0;
            _leftSquaredSum = 0;
            _rightSquaredSum = 0;
            _leftRightProductSum = 0;

            Array.Clear(_levelHistogram, 0, _levelHistogram.Length);

            _silenceSampleCount = 0;
            _inSilence = false;
            _silenceStartSample = 0;
            _silenceRegions.Clear();

            _consecutiveZeroSamples = 0;
            _dropoutPositions.Clear();

            _totalSamplesProcessed = 0;
            _frameCount = 0;

            _truePeakDetector?.Reset();
        }
    }

    private float CalculatePhaseCorrelation()
    {
        if (_channels < 2 || _totalSamplesProcessed == 0)
            return 1f;

        double leftStd = Math.Sqrt(_leftSquaredSum / _totalSamplesProcessed);
        double rightStd = Math.Sqrt(_rightSquaredSum / _totalSamplesProcessed);

        if (leftStd < 1e-10 || rightStd < 1e-10)
            return 1f;

        double covariance = _leftRightProductSum / _totalSamplesProcessed;
        return Math.Clamp((float)(covariance / (leftStd * rightStd)), -1f, 1f);
    }

    private float EstimateNoiseFloor()
    {
        // Find the lowest significant level in the histogram
        // Use the 5th percentile as the noise floor estimate
        long totalSamples = 0;
        foreach (var count in _levelHistogram)
            totalSamples += count;

        if (totalSamples == 0)
            return -90f;

        long threshold = totalSamples / 20; // 5th percentile
        long cumulative = 0;

        for (int i = 0; i < NoiseHistogramBins; i++)
        {
            cumulative += _levelHistogram[i];
            if (cumulative >= threshold)
            {
                return i - 120f; // Convert bin to dBFS
            }
        }

        return -90f;
    }

    private List<DiagnosticIssue> DetectIssues(
        float dcOffset, float phaseCorrelation, float noiseFloorDbfs, float dynamicRangeDb,
        float crestFactorDb, float channelBalance, float stereoWidth, float clippingPercentage, float silencePercentage)
    {
        var issues = new List<DiagnosticIssue>();

        // Clipping
        if (_clippedSampleCount > 0)
        {
            var severity = clippingPercentage < 0.01f ? DiagnosticSeverity.Minor
                : clippingPercentage < 0.1f ? DiagnosticSeverity.Moderate
                : clippingPercentage < 1f ? DiagnosticSeverity.Severe
                : DiagnosticSeverity.Critical;

            issues.Add(new DiagnosticIssue
            {
                Type = DiagnosticIssueType.Clipping,
                Severity = severity,
                Description = $"Digital clipping detected: {_clippedSampleCount:N0} samples ({clippingPercentage:F3}%)",
                MeasuredValue = clippingPercentage,
                Threshold = 0f,
                SuggestedFix = "Reduce gain before the clipping stage or use a limiter."
            });
        }

        // Inter-sample clipping
        if (_truePeakLevel > 1.0f)
        {
            issues.Add(new DiagnosticIssue
            {
                Type = DiagnosticIssueType.IntersampleClipping,
                Severity = DiagnosticSeverity.Moderate,
                Description = $"Inter-sample peaks detected: {LinearToDbfs(_truePeakLevel):F1} dBTP",
                MeasuredValue = LinearToDbfs(_truePeakLevel),
                Threshold = 0f,
                SuggestedFix = "Use a true peak limiter to prevent inter-sample clipping."
            });
        }

        // DC Offset
        float absDcOffset = Math.Abs(dcOffset);
        if (absDcOffset > DCOffsetThreshold)
        {
            var severity = ClassifyDCOffsetSeverity(absDcOffset);
            issues.Add(new DiagnosticIssue
            {
                Type = DiagnosticIssueType.DCOffset,
                Severity = severity,
                Description = $"DC offset detected: {dcOffset:F4} (linear)",
                MeasuredValue = dcOffset,
                Threshold = DCOffsetThreshold,
                SuggestedFix = "Apply a high-pass filter or DC offset removal tool."
            });
        }

        // Phase issues (stereo only)
        if (_channels >= 2)
        {
            if (phaseCorrelation < -0.5f)
            {
                issues.Add(new DiagnosticIssue
                {
                    Type = DiagnosticIssueType.PolarityInversion,
                    Severity = DiagnosticSeverity.Critical,
                    Description = $"Likely polarity inversion: correlation = {phaseCorrelation:F2}",
                    MeasuredValue = phaseCorrelation,
                    Threshold = -0.5f,
                    SuggestedFix = "Check phase/polarity settings on one of the channels."
                });
            }
            else if (phaseCorrelation < PhaseIssueThreshold)
            {
                var severity = phaseCorrelation < -0.2f ? DiagnosticSeverity.Severe
                    : phaseCorrelation < 0f ? DiagnosticSeverity.Moderate
                    : DiagnosticSeverity.Minor;

                issues.Add(new DiagnosticIssue
                {
                    Type = DiagnosticIssueType.PhaseIssue,
                    Severity = severity,
                    Description = $"Phase correlation issue: {phaseCorrelation:F2}",
                    MeasuredValue = phaseCorrelation,
                    Threshold = PhaseIssueThreshold,
                    SuggestedFix = "Check for comb filtering, excessive stereo widening, or microphone placement issues."
                });

                if (phaseCorrelation < -0.2f)
                {
                    issues.Add(new DiagnosticIssue
                    {
                        Type = DiagnosticIssueType.MonoCancellation,
                        Severity = DiagnosticSeverity.Severe,
                        Description = "Significant level loss will occur in mono playback.",
                        MeasuredValue = phaseCorrelation,
                        SuggestedFix = "Reduce stereo width or check for phase issues before mono summing."
                    });
                }
            }

            // Channel imbalance
            if (Math.Abs(channelBalance) > 3f)
            {
                var severity = Math.Abs(channelBalance) > 6f ? DiagnosticSeverity.Moderate : DiagnosticSeverity.Minor;
                issues.Add(new DiagnosticIssue
                {
                    Type = DiagnosticIssueType.ChannelImbalance,
                    Severity = severity,
                    Description = $"Channel imbalance: {channelBalance:F1} dB {(channelBalance > 0 ? "(right heavy)" : "(left heavy)")}",
                    MeasuredValue = channelBalance,
                    Threshold = 3f,
                    SuggestedFix = "Adjust panning or channel levels to balance the stereo image."
                });
            }

            // Mono audio
            if (phaseCorrelation > 0.95f && stereoWidth < 0.05f)
            {
                issues.Add(new DiagnosticIssue
                {
                    Type = DiagnosticIssueType.MonoAudio,
                    Severity = DiagnosticSeverity.Minor,
                    Description = "Audio is nearly or completely mono.",
                    MeasuredValue = stereoWidth,
                    SuggestedFix = "Consider adding stereo width if a stereo presentation is desired."
                });
            }

            // Excessive stereo width
            if (stereoWidth > 0.8f)
            {
                issues.Add(new DiagnosticIssue
                {
                    Type = DiagnosticIssueType.ExcessiveStereoWidth,
                    Severity = DiagnosticSeverity.Minor,
                    Description = $"Very wide stereo image: {stereoWidth:F2}",
                    MeasuredValue = stereoWidth,
                    Threshold = 0.8f,
                    SuggestedFix = "May have mono compatibility issues. Check in mono."
                });
            }
        }

        // High noise floor
        if (noiseFloorDbfs > -40f)
        {
            var severity = noiseFloorDbfs > -30f ? DiagnosticSeverity.Severe
                : noiseFloorDbfs > -35f ? DiagnosticSeverity.Moderate
                : DiagnosticSeverity.Minor;

            issues.Add(new DiagnosticIssue
            {
                Type = DiagnosticIssueType.HighNoiseFloor,
                Severity = severity,
                Description = $"High noise floor: {noiseFloorDbfs:F1} dBFS",
                MeasuredValue = noiseFloorDbfs,
                Threshold = -40f,
                SuggestedFix = "Apply noise reduction or re-record in a quieter environment."
            });
        }

        // Low dynamic range
        if (dynamicRangeDb < MinDynamicRangeDb)
        {
            var severity = dynamicRangeDb < 3f ? DiagnosticSeverity.Severe
                : dynamicRangeDb < 5f ? DiagnosticSeverity.Moderate
                : DiagnosticSeverity.Minor;

            issues.Add(new DiagnosticIssue
            {
                Type = DiagnosticIssueType.LowDynamicRange,
                Severity = severity,
                Description = $"Low dynamic range: {dynamicRangeDb:F1} dB (may be over-compressed)",
                MeasuredValue = dynamicRangeDb,
                Threshold = MinDynamicRangeDb,
                SuggestedFix = "Reduce compression/limiting or check for over-processing."
            });
        }

        // Crest factor issues
        if (crestFactorDb < MinCrestFactorDb)
        {
            issues.Add(new DiagnosticIssue
            {
                Type = DiagnosticIssueType.LowCrestFactor,
                Severity = DiagnosticSeverity.Moderate,
                Description = $"Low crest factor: {crestFactorDb:F1} dB (may be over-limited)",
                MeasuredValue = crestFactorDb,
                Threshold = MinCrestFactorDb,
                SuggestedFix = "Reduce limiting or compression to preserve transients."
            });
        }
        else if (crestFactorDb > MaxCrestFactorDb)
        {
            issues.Add(new DiagnosticIssue
            {
                Type = DiagnosticIssueType.HighCrestFactor,
                Severity = DiagnosticSeverity.Minor,
                Description = $"High crest factor: {crestFactorDb:F1} dB (may indicate sparse or impulsive audio)",
                MeasuredValue = crestFactorDb,
                Threshold = MaxCrestFactorDb,
                SuggestedFix = "This is not necessarily an issue, but may indicate sparse audio content."
            });
        }

        // Excessive silence
        if (silencePercentage > 50f)
        {
            issues.Add(new DiagnosticIssue
            {
                Type = DiagnosticIssueType.Silence,
                Severity = DiagnosticSeverity.Minor,
                Description = $"Audio contains {silencePercentage:F1}% silence",
                MeasuredValue = silencePercentage,
                Threshold = 50f,
                SuggestedFix = "Consider trimming silence if not intentional."
            });
        }

        // Dropouts
        if (_dropoutPositions.Count > 0)
        {
            var severity = _dropoutPositions.Count > 10 ? DiagnosticSeverity.Severe
                : _dropoutPositions.Count > 3 ? DiagnosticSeverity.Moderate
                : DiagnosticSeverity.Minor;

            issues.Add(new DiagnosticIssue
            {
                Type = DiagnosticIssueType.Dropout,
                Severity = severity,
                Description = $"Detected {_dropoutPositions.Count} potential dropout(s)/gap(s)",
                MeasuredValue = _dropoutPositions.Count,
                SuggestedFix = "Check for buffer underruns, corrupted audio, or editing artifacts."
            });
        }

        return issues;
    }

    private DiagnosticSeverity ClassifyDCOffsetSeverity(float absDcOffset)
    {
        if (absDcOffset < 0.001f) return DiagnosticSeverity.None;
        if (absDcOffset < 0.01f) return DiagnosticSeverity.Minor;
        if (absDcOffset < 0.05f) return DiagnosticSeverity.Moderate;
        if (absDcOffset < 0.1f) return DiagnosticSeverity.Severe;
        return DiagnosticSeverity.Critical;
    }

    private float CalculateQualityScore(List<DiagnosticIssue> issues, float clippingPercentage,
        float dcOffset, float phaseCorrelation, float dynamicRangeDb, float crestFactorDb)
    {
        float score = 100f;

        // Deduct points for each issue based on severity
        foreach (var issue in issues)
        {
            switch (issue.Severity)
            {
                case DiagnosticSeverity.Minor:
                    score -= 2f;
                    break;
                case DiagnosticSeverity.Moderate:
                    score -= 5f;
                    break;
                case DiagnosticSeverity.Severe:
                    score -= 15f;
                    break;
                case DiagnosticSeverity.Critical:
                    score -= 30f;
                    break;
            }
        }

        // Additional deductions based on specific metrics
        if (clippingPercentage > 0)
            score -= Math.Min(20f, clippingPercentage * 10f);

        if (phaseCorrelation < 0)
            score -= Math.Min(20f, Math.Abs(phaseCorrelation) * 20f);

        return Math.Max(0f, Math.Min(100f, score));
    }

    private static float LinearToDbfs(float linear)
    {
        if (linear <= 0f)
            return -120f;
        return 20f * (float)Math.Log10(linear);
    }

    /// <summary>
    /// Internal true peak detector using 4x oversampling.
    /// </summary>
    private class TruePeakDetector
    {
        private const int FilterLength = 48;
        private const int OversampleFactor = 4;
        private const int TapsPerPhase = FilterLength / OversampleFactor;

        private static readonly float[][] PhaseCoefficients = GeneratePhaseCoefficients();
        private readonly float[] _history = new float[TapsPerPhase];
        private int _historyIndex;

        public void Reset()
        {
            Array.Clear(_history, 0, _history.Length);
            _historyIndex = 0;
        }

        public float ProcessSample(float sample)
        {
            _history[_historyIndex] = sample;
            _historyIndex = (_historyIndex + 1) % TapsPerPhase;

            float maxPeak = Math.Abs(sample);

            for (int phase = 0; phase < OversampleFactor; phase++)
            {
                float interpolated = 0;
                int histIdx = _historyIndex;

                for (int tap = 0; tap < TapsPerPhase; tap++)
                {
                    histIdx--;
                    if (histIdx < 0) histIdx = TapsPerPhase - 1;
                    interpolated += _history[histIdx] * PhaseCoefficients[phase][tap];
                }

                float absPeak = Math.Abs(interpolated);
                if (absPeak > maxPeak)
                    maxPeak = absPeak;
            }

            return maxPeak;
        }

        private static float[][] GeneratePhaseCoefficients()
        {
            float[][] phases = new float[OversampleFactor][];
            float[] fullFilter = new float[FilterLength];

            const double beta = 5.0;
            double halfLength = (FilterLength - 1) / 2.0;

            for (int i = 0; i < FilterLength; i++)
            {
                double n = i - halfLength;
                double sincArg = n / OversampleFactor;

                double sinc = Math.Abs(sincArg) < 1e-10
                    ? 1.0
                    : Math.Sin(Math.PI * sincArg) / (Math.PI * sincArg);

                double x = 2.0 * i / (FilterLength - 1) - 1.0;
                double kaiser = BesselI0(beta * Math.Sqrt(1.0 - x * x)) / BesselI0(beta);

                fullFilter[i] = (float)(sinc * kaiser);
            }

            for (int phase = 0; phase < OversampleFactor; phase++)
            {
                phases[phase] = new float[TapsPerPhase];
                float sum = 0;

                for (int tap = 0; tap < TapsPerPhase; tap++)
                {
                    int filterIdx = tap * OversampleFactor + phase;
                    if (filterIdx < FilterLength)
                    {
                        phases[phase][tap] = fullFilter[filterIdx];
                        sum += Math.Abs(phases[phase][tap]);
                    }
                }

                if (sum > 0)
                {
                    for (int tap = 0; tap < TapsPerPhase; tap++)
                    {
                        phases[phase][tap] /= sum;
                    }
                }
            }

            return phases;
        }

        private static double BesselI0(double x)
        {
            double sum = 1.0;
            double term = 1.0;
            double xSquaredOver4 = x * x / 4.0;

            for (int k = 1; k <= 25; k++)
            {
                term *= xSquaredOver4 / (k * k);
                sum += term;
                if (term < 1e-12 * sum) break;
            }

            return sum;
        }
    }
}
