//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Comprehensive dynamics analysis tool for measuring loudness, dynamic range, compression detection, and transient characteristics.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using NAudio.Dsp;
using NAudio.Wave;

namespace MusicEngine.Core.Analysis;

#region Enums

/// <summary>
/// Genre categories for dynamics reference comparison.
/// </summary>
public enum DynamicsGenre
{
    /// <summary>Classical/Orchestral - High dynamic range expected.</summary>
    Classical,

    /// <summary>Jazz - Moderate to high dynamic range.</summary>
    Jazz,

    /// <summary>Acoustic/Folk - Natural dynamics.</summary>
    Acoustic,

    /// <summary>Rock - Moderate dynamics with some compression.</summary>
    Rock,

    /// <summary>Pop - Moderate compression, consistent levels.</summary>
    Pop,

    /// <summary>Electronic/EDM - Higher compression, punchy dynamics.</summary>
    Electronic,

    /// <summary>Hip-Hop/Trap - Heavy compression, loud masters.</summary>
    HipHop,

    /// <summary>Metal - High energy, moderate to heavy compression.</summary>
    Metal,

    /// <summary>Podcast/Speech - Consistent levels, moderate compression.</summary>
    Podcast,

    /// <summary>Broadcast/TV - Strict loudness standards.</summary>
    Broadcast
}

/// <summary>
/// Compression detection severity levels.
/// </summary>
public enum CompressionLevel
{
    /// <summary>No significant compression detected.</summary>
    None,

    /// <summary>Light compression - natural dynamics preserved.</summary>
    Light,

    /// <summary>Moderate compression - controlled dynamics.</summary>
    Moderate,

    /// <summary>Heavy compression - significantly reduced dynamics.</summary>
    Heavy,

    /// <summary>Over-compressed - dynamics severely limited.</summary>
    Extreme
}

/// <summary>
/// Limiting detection severity levels.
/// </summary>
public enum LimitingLevel
{
    /// <summary>No limiting detected.</summary>
    None,

    /// <summary>Gentle limiting - peak control only.</summary>
    Gentle,

    /// <summary>Moderate limiting - noticeable gain reduction.</summary>
    Moderate,

    /// <summary>Heavy limiting - significant level control.</summary>
    Heavy,

    /// <summary>Brickwall limiting - maximum loudness.</summary>
    Brickwall
}

#endregion

#region Result Classes

/// <summary>
/// Histogram data for level distribution analysis.
/// </summary>
public class LevelHistogram
{
    /// <summary>Gets the histogram bin counts (dB levels from -90 to 0).</summary>
    public int[] BinCounts { get; init; } = Array.Empty<int>();

    /// <summary>Gets the bin width in dB.</summary>
    public float BinWidthDb { get; init; }

    /// <summary>Gets the minimum dB value represented.</summary>
    public float MinDb { get; init; }

    /// <summary>Gets the maximum dB value represented.</summary>
    public float MaxDb { get; init; }

    /// <summary>Gets the total number of samples in the histogram.</summary>
    public long TotalSamples { get; init; }

    /// <summary>Gets the most common level in dB.</summary>
    public float ModeDb { get; init; }

    /// <summary>Gets the median level in dB.</summary>
    public float MedianDb { get; init; }

    /// <summary>Gets the histogram as normalized percentages.</summary>
    public float[] NormalizedBins
    {
        get
        {
            if (TotalSamples == 0) return new float[BinCounts.Length];
            float[] normalized = new float[BinCounts.Length];
            for (int i = 0; i < BinCounts.Length; i++)
            {
                normalized[i] = (float)BinCounts[i] / TotalSamples;
            }
            return normalized;
        }
    }
}

/// <summary>
/// Time-series data point for level over time graphs.
/// </summary>
public class LevelTimePoint
{
    /// <summary>Gets the time position in seconds.</summary>
    public float TimeSeconds { get; init; }

    /// <summary>Gets the RMS level in dB at this time.</summary>
    public float RmsDb { get; init; }

    /// <summary>Gets the peak level in dB at this time.</summary>
    public float PeakDb { get; init; }

    /// <summary>Gets the LUFS momentary loudness at this time.</summary>
    public float MomentaryLufs { get; init; }

    /// <summary>Gets the crest factor at this time.</summary>
    public float CrestFactorDb { get; init; }
}

/// <summary>
/// Per-frequency-band dynamics analysis result.
/// </summary>
public class BandDynamicsResult
{
    /// <summary>Gets the band index.</summary>
    public int BandIndex { get; init; }

    /// <summary>Gets the lower frequency boundary in Hz.</summary>
    public float LowFrequency { get; init; }

    /// <summary>Gets the upper frequency boundary in Hz.</summary>
    public float HighFrequency { get; init; }

    /// <summary>Gets the center frequency in Hz.</summary>
    public float CenterFrequency => (float)Math.Sqrt(LowFrequency * HighFrequency);

    /// <summary>Gets the RMS level in dB.</summary>
    public float RmsDb { get; init; }

    /// <summary>Gets the peak level in dB.</summary>
    public float PeakDb { get; init; }

    /// <summary>Gets the dynamic range for this band in dB.</summary>
    public float DynamicRangeDb { get; init; }

    /// <summary>Gets the crest factor for this band in dB.</summary>
    public float CrestFactorDb { get; init; }

    /// <summary>Gets the compression level detected in this band.</summary>
    public CompressionLevel Compression { get; init; }
}

/// <summary>
/// Transient characteristics analysis result.
/// </summary>
public class TransientCharacteristics
{
    /// <summary>Gets the number of transients detected.</summary>
    public int TransientCount { get; init; }

    /// <summary>Gets the average transients per second.</summary>
    public float TransientsPerSecond { get; init; }

    /// <summary>Gets the average attack time in milliseconds.</summary>
    public float AverageAttackTimeMs { get; init; }

    /// <summary>Gets the average release/decay time in milliseconds.</summary>
    public float AverageReleaseTimeMs { get; init; }

    /// <summary>Gets the average transient peak level in dB.</summary>
    public float AverageTransientPeakDb { get; init; }

    /// <summary>Gets the transient density score (0-1).</summary>
    public float TransientDensity { get; init; }

    /// <summary>Gets the transient sharpness score (0-1, 1 = very sharp attacks).</summary>
    public float TransientSharpness { get; init; }

    /// <summary>Gets the detected transient events.</summary>
    public TransientInfo[] Transients { get; init; } = Array.Empty<TransientInfo>();
}

/// <summary>
/// Information about a single detected transient.
/// </summary>
public class TransientInfo
{
    /// <summary>Gets the time position in seconds.</summary>
    public float TimeSeconds { get; init; }

    /// <summary>Gets the peak level in dB.</summary>
    public float PeakDb { get; init; }

    /// <summary>Gets the attack time in milliseconds.</summary>
    public float AttackTimeMs { get; init; }

    /// <summary>Gets the release time in milliseconds.</summary>
    public float ReleaseTimeMs { get; init; }

    /// <summary>Gets whether this is a strong transient.</summary>
    public bool IsStrong { get; init; }
}

/// <summary>
/// Sustain characteristics analysis result.
/// </summary>
public class SustainCharacteristics
{
    /// <summary>Gets the average sustain level in dB relative to peak.</summary>
    public float AverageSustainLevelDb { get; init; }

    /// <summary>Gets the sustain consistency score (0-1, 1 = very consistent).</summary>
    public float SustainConsistency { get; init; }

    /// <summary>Gets the average sustain duration in milliseconds.</summary>
    public float AverageSustainDurationMs { get; init; }

    /// <summary>Gets the sustain-to-peak ratio.</summary>
    public float SustainToPeakRatio { get; init; }
}

/// <summary>
/// Macro dynamics analysis (song sections).
/// </summary>
public class MacroDynamicsResult
{
    /// <summary>Gets the detected song sections with dynamics info.</summary>
    public SectionDynamics[] Sections { get; init; } = Array.Empty<SectionDynamics>();

    /// <summary>Gets the overall macro dynamic range in dB.</summary>
    public float MacroDynamicRangeDb { get; init; }

    /// <summary>Gets the loudest section.</summary>
    public SectionDynamics? LoudestSection { get; init; }

    /// <summary>Gets the quietest section.</summary>
    public SectionDynamics? QuietestSection { get; init; }

    /// <summary>Gets the average section loudness variation in dB.</summary>
    public float AverageLoudnessVariationDb { get; init; }
}

/// <summary>
/// Dynamics information for a song section.
/// </summary>
public class SectionDynamics
{
    /// <summary>Gets the section index.</summary>
    public int Index { get; init; }

    /// <summary>Gets the start time in seconds.</summary>
    public float StartTimeSeconds { get; init; }

    /// <summary>Gets the end time in seconds.</summary>
    public float EndTimeSeconds { get; init; }

    /// <summary>Gets the duration in seconds.</summary>
    public float DurationSeconds => EndTimeSeconds - StartTimeSeconds;

    /// <summary>Gets the average RMS level in dB.</summary>
    public float AverageRmsDb { get; init; }

    /// <summary>Gets the peak level in dB.</summary>
    public float PeakDb { get; init; }

    /// <summary>Gets the average LUFS.</summary>
    public float AverageLufs { get; init; }

    /// <summary>Gets the dynamic range within this section.</summary>
    public float DynamicRangeDb { get; init; }
}

/// <summary>
/// Micro dynamics analysis (within beats).
/// </summary>
public class MicroDynamicsResult
{
    /// <summary>Gets the average beat-level dynamic range in dB.</summary>
    public float AverageBeatDynamicRangeDb { get; init; }

    /// <summary>Gets the beat consistency score (0-1).</summary>
    public float BeatConsistency { get; init; }

    /// <summary>Gets the groove/swing amount (0-1).</summary>
    public float GrooveAmount { get; init; }

    /// <summary>Gets the detected tempo in BPM.</summary>
    public float TempoBypassedBpm { get; init; }

    /// <summary>Gets individual beat dynamics.</summary>
    public BeatDynamics[] Beats { get; init; } = Array.Empty<BeatDynamics>();
}

/// <summary>
/// Dynamics information for a single beat.
/// </summary>
public class BeatDynamics
{
    /// <summary>Gets the beat index.</summary>
    public int Index { get; init; }

    /// <summary>Gets the beat time in seconds.</summary>
    public float TimeSeconds { get; init; }

    /// <summary>Gets the peak level in dB.</summary>
    public float PeakDb { get; init; }

    /// <summary>Gets the RMS level in dB.</summary>
    public float RmsDb { get; init; }

    /// <summary>Gets whether this is a downbeat.</summary>
    public bool IsDownbeat { get; init; }
}

/// <summary>
/// Reference track comparison result.
/// </summary>
public class ReferenceComparisonResult
{
    /// <summary>Gets the loudness difference in LUFS.</summary>
    public float LoudnessDifferenceLufs { get; init; }

    /// <summary>Gets the dynamic range difference in dB.</summary>
    public float DynamicRangeDifferenceDb { get; init; }

    /// <summary>Gets the crest factor difference in dB.</summary>
    public float CrestFactorDifferenceDb { get; init; }

    /// <summary>Gets the true peak difference in dB.</summary>
    public float TruePeakDifferenceDb { get; init; }

    /// <summary>Gets the LRA difference in LU.</summary>
    public float LraDifferenceLu { get; init; }

    /// <summary>Gets the per-band differences.</summary>
    public float[] BandDifferencesDb { get; init; } = Array.Empty<float>();

    /// <summary>Gets suggestions based on comparison.</summary>
    public string[] Suggestions { get; init; } = Array.Empty<string>();

    /// <summary>Gets the similarity score (0-100).</summary>
    public float SimilarityScore { get; init; }
}

/// <summary>
/// Genre-appropriate dynamics check result.
/// </summary>
public class GenreCheckResult
{
    /// <summary>Gets the genre checked against.</summary>
    public DynamicsGenre Genre { get; init; }

    /// <summary>Gets the overall appropriateness score (0-100).</summary>
    public float AppropriatenessScore { get; init; }

    /// <summary>Gets whether loudness is appropriate.</summary>
    public bool LoudnessAppropriate { get; init; }

    /// <summary>Gets whether dynamic range is appropriate.</summary>
    public bool DynamicRangeAppropriate { get; init; }

    /// <summary>Gets whether compression level is appropriate.</summary>
    public bool CompressionAppropriate { get; init; }

    /// <summary>Gets the expected loudness range in LUFS.</summary>
    public (float Min, float Max) ExpectedLufsRange { get; init; }

    /// <summary>Gets the expected dynamic range in dB.</summary>
    public (float Min, float Max) ExpectedDynamicRangeDb { get; init; }

    /// <summary>Gets detailed feedback.</summary>
    public string[] Feedback { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Clipping detection result.
/// </summary>
public class ClippingResult
{
    /// <summary>Gets the total number of clipped samples.</summary>
    public long ClippedSampleCount { get; init; }

    /// <summary>Gets the percentage of samples that are clipped.</summary>
    public float ClippingPercentage { get; init; }

    /// <summary>Gets the number of clipping events (consecutive clips counted as one).</summary>
    public int ClippingEventCount { get; init; }

    /// <summary>Gets the maximum consecutive clipped samples.</summary>
    public int MaxConsecutiveClippedSamples { get; init; }

    /// <summary>Gets inter-sample peak count (peaks between samples exceeding 0dBFS).</summary>
    public int InterSamplePeakCount { get; init; }

    /// <summary>Gets the maximum inter-sample peak overshoot in dB.</summary>
    public float MaxInterSampleOvershootDb { get; init; }

    /// <summary>Gets whether clipping is problematic.</summary>
    public bool HasProblematicClipping => ClippingEventCount > 10 || ClippingPercentage > 0.01f;

    /// <summary>Gets the clipping severity description.</summary>
    public string Severity
    {
        get
        {
            if (ClippingEventCount == 0) return "None";
            if (ClippingEventCount < 5 && ClippingPercentage < 0.001f) return "Minimal";
            if (ClippingEventCount < 20 && ClippingPercentage < 0.01f) return "Light";
            if (ClippingEventCount < 100 && ClippingPercentage < 0.1f) return "Moderate";
            return "Severe";
        }
    }
}

/// <summary>
/// Dynamics processing recommendations.
/// </summary>
public class DynamicsRecommendations
{
    /// <summary>Gets whether compression is recommended.</summary>
    public bool CompressionRecommended { get; init; }

    /// <summary>Gets suggested compression ratio.</summary>
    public float SuggestedCompressionRatio { get; init; }

    /// <summary>Gets suggested compression threshold in dB.</summary>
    public float SuggestedThresholdDb { get; init; }

    /// <summary>Gets suggested attack time in milliseconds.</summary>
    public float SuggestedAttackMs { get; init; }

    /// <summary>Gets suggested release time in milliseconds.</summary>
    public float SuggestedReleaseMs { get; init; }

    /// <summary>Gets whether limiting is recommended.</summary>
    public bool LimitingRecommended { get; init; }

    /// <summary>Gets suggested limiter ceiling in dB.</summary>
    public float SuggestedLimiterCeilingDb { get; init; }

    /// <summary>Gets whether expansion is recommended.</summary>
    public bool ExpansionRecommended { get; init; }

    /// <summary>Gets whether transient shaping is recommended.</summary>
    public bool TransientShapingRecommended { get; init; }

    /// <summary>Gets suggested gain adjustment in dB.</summary>
    public float SuggestedGainAdjustmentDb { get; init; }

    /// <summary>Gets all recommendations as text.</summary>
    public string[] TextRecommendations { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Complete dynamics analysis result.
/// </summary>
public class DynamicsAnalysisResult
{
    // Basic measurements
    /// <summary>Gets the peak level in dB.</summary>
    public float PeakDb { get; init; }

    /// <summary>Gets the RMS level in dB.</summary>
    public float RmsDb { get; init; }

    /// <summary>Gets the integrated loudness in LUFS.</summary>
    public float IntegratedLufs { get; init; }

    /// <summary>Gets the short-term loudness in LUFS.</summary>
    public float ShortTermLufs { get; init; }

    /// <summary>Gets the momentary loudness in LUFS.</summary>
    public float MomentaryLufs { get; init; }

    /// <summary>Gets the true peak level in dBTP.</summary>
    public float TruePeakDbtp { get; init; }

    /// <summary>Gets the loudness range (LRA) in LU.</summary>
    public float LoudnessRangeLu { get; init; }

    /// <summary>Gets the dynamic range score (DR, TT DR meter style).</summary>
    public int DynamicRangeDr { get; init; }

    /// <summary>Gets the crest factor in dB.</summary>
    public float CrestFactorDb { get; init; }

    /// <summary>Gets the peak-to-loudness ratio (PLR) in dB.</summary>
    public float PeakToLoudnessRatioDb { get; init; }

    // Histograms and time-series
    /// <summary>Gets the level histogram.</summary>
    public LevelHistogram? Histogram { get; init; }

    /// <summary>Gets the level over time data.</summary>
    public LevelTimePoint[] LevelOverTime { get; init; } = Array.Empty<LevelTimePoint>();

    // Detection results
    /// <summary>Gets the detected compression level.</summary>
    public CompressionLevel CompressionDetected { get; init; }

    /// <summary>Gets the compression amount estimate (0-100%).</summary>
    public float CompressionAmountPercent { get; init; }

    /// <summary>Gets the detected limiting level.</summary>
    public LimitingLevel LimitingDetected { get; init; }

    /// <summary>Gets the limiting amount estimate (0-100%).</summary>
    public float LimitingAmountPercent { get; init; }

    /// <summary>Gets the clipping detection result.</summary>
    public ClippingResult? ClippingResult { get; init; }

    // Per-band analysis
    /// <summary>Gets per-frequency-band dynamics analysis.</summary>
    public BandDynamicsResult[] BandDynamics { get; init; } = Array.Empty<BandDynamicsResult>();

    // Transient and sustain analysis
    /// <summary>Gets transient characteristics.</summary>
    public TransientCharacteristics? TransientAnalysis { get; init; }

    /// <summary>Gets sustain characteristics.</summary>
    public SustainCharacteristics? SustainAnalysis { get; init; }

    // Macro and micro dynamics
    /// <summary>Gets macro dynamics analysis (song sections).</summary>
    public MacroDynamicsResult? MacroDynamics { get; init; }

    /// <summary>Gets micro dynamics analysis (within beats).</summary>
    public MicroDynamicsResult? MicroDynamics { get; init; }

    // Recommendations
    /// <summary>Gets dynamics processing recommendations.</summary>
    public DynamicsRecommendations? Recommendations { get; init; }

    // Metadata
    /// <summary>Gets the duration of analyzed audio in seconds.</summary>
    public float DurationSeconds { get; init; }

    /// <summary>Gets the sample rate of the analyzed audio.</summary>
    public int SampleRate { get; init; }

    /// <summary>Gets the number of channels analyzed.</summary>
    public int Channels { get; init; }

    /// <summary>Gets the analysis timestamp.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

#endregion

/// <summary>
/// Comprehensive dynamics analyzer for measuring loudness, dynamic range, compression detection,
/// and transient characteristics of audio.
/// </summary>
/// <remarks>
/// Features include:
/// - Peak and RMS level measurement
/// - LUFS measurements (Integrated, Short-term, Momentary)
/// - True peak measurement with inter-sample detection
/// - Loudness Range (LRA) calculation
/// - Dynamic Range (DR) score (TT DR meter style)
/// - Crest factor and Peak-to-Loudness ratio
/// - Level histogram and time-series data
/// - Compression and limiting detection
/// - Clipping detection with inter-sample peak analysis
/// - Per-frequency-band dynamics
/// - Transient and sustain analysis
/// - Macro dynamics (song sections) and micro dynamics (beats)
/// - Reference track comparison
/// - Genre-appropriate dynamics checking
/// - Dynamics report export
/// - Processing recommendations
/// </remarks>
public class DynamicsAnalyzer : IAnalyzer, ISampleProvider
{
    #region Constants

    // Frequency band definitions for per-band analysis
    private static readonly (float Low, float High)[] BandFrequencies =
    {
        (20f, 60f),      // Sub
        (60f, 250f),     // Bass
        (250f, 500f),    // Low-Mid
        (500f, 2000f),   // Mid
        (2000f, 4000f),  // High-Mid
        (4000f, 8000f),  // Presence
        (8000f, 16000f), // Brilliance
        (16000f, 20000f) // Air
    };

    // Genre-specific dynamics targets
    private static readonly Dictionary<DynamicsGenre, (float MinLufs, float MaxLufs, float MinDr, float MaxDr)> GenreTargets = new()
    {
        { DynamicsGenre.Classical, (-24f, -14f, 14f, 20f) },
        { DynamicsGenre.Jazz, (-20f, -12f, 10f, 16f) },
        { DynamicsGenre.Acoustic, (-18f, -10f, 10f, 16f) },
        { DynamicsGenre.Rock, (-14f, -8f, 6f, 12f) },
        { DynamicsGenre.Pop, (-12f, -6f, 5f, 10f) },
        { DynamicsGenre.Electronic, (-10f, -5f, 4f, 8f) },
        { DynamicsGenre.HipHop, (-10f, -5f, 4f, 8f) },
        { DynamicsGenre.Metal, (-12f, -6f, 4f, 8f) },
        { DynamicsGenre.Podcast, (-19f, -14f, 6f, 12f) },
        { DynamicsGenre.Broadcast, (-24f, -22f, 6f, 12f) }
    };

    private const int HistogramBins = 91; // -90dB to 0dB
    private const int DefaultFftSize = 4096;
    private const float ClippingThreshold = 0.9999f;
    private const int MomentaryBlockMs = 400;
    private const int ShortTermBlockMs = 3000;
    private const int SectionMinDurationMs = 5000;
    private const float TransientThreshold = 0.3f;

    #endregion

    #region Fields

    private readonly ISampleProvider? _source;
    private readonly int _sampleRate;
    private readonly int _channels;
    private readonly int _fftSize;
    private readonly Complex[] _fftBuffer;
    private readonly float[] _sampleBuffer;
    private readonly float[] _window;
    private readonly int[] _bandBinRanges;
    private readonly object _lock = new();

    // K-weighting filter coefficients and state
    private readonly double[] _hsB;
    private readonly double[] _hsA;
    private readonly double[] _hpB;
    private readonly double[] _hpA;
    private readonly double[,] _hsState;
    private readonly double[,] _hpState;

    // Level accumulators
    private double _sumSquared;
    private float _maxPeak;
    private float _maxTruePeak;
    private long _totalSamples;

    // Histogram
    private readonly long[] _levelHistogram;

    // Time-series data
    private readonly List<LevelTimePoint> _levelOverTime;
    private int _timeSeriesInterval;
    private int _timeSeriesSampleCount;
    private double _timeSeriesRmsSum;
    private float _timeSeriesPeak;
    // LUFS calculation
    private readonly double[] _momentaryBuffer;
    private int _momentaryWritePos;
    private readonly List<double> _gatedBlocks;
    private double _integratedLoudness;
    private double _shortTermLoudness;
    private double _momentaryLoudness;
    private readonly List<double> _shortTermBlocks;
    private readonly List<double> _allLufsBlocks;

    // Per-band accumulators
    private readonly double[] _bandRmsSum;
    private readonly float[] _bandPeaks;
    private readonly double[] _bandMinLevels;
    private readonly int[] _bandSampleCounts;

    // Clipping detection
    private long _clippedSamples;
    private int _clippingEvents;
    private int _currentClipRun;
    private int _maxClipRun;
    private int _interSamplePeaks;
    private float _maxInterSampleOvershoot;

    // Transient detection
    private readonly List<TransientInfo> _detectedTransients;
    private float _previousEnergy;
    private readonly float[] _energyHistory;
    private int _energyHistoryPos;

    // Section detection for macro dynamics
    private readonly List<SectionDynamics> _sections;
    private double _currentSectionRmsSum;
    private float _currentSectionPeak;
    private int _currentSectionSamples;
    private float _sectionStartTime;

    // True peak detection (oversampling)
    private readonly float[] _truePeakHistory;
    private int _truePeakHistoryPos;
    private static readonly float[] OversamplingCoeffs = GenerateOversamplingCoeffs();

    // Analysis state
    private int _sampleBufferPos;
    private int _frameCount;
    private volatile bool _isAnalyzing;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the wave format.
    /// </summary>
    public WaveFormat WaveFormat => _source?.WaveFormat ?? WaveFormat.CreateIeeeFloatWaveFormat(_sampleRate, _channels);

    /// <summary>
    /// Gets the sample rate used for analysis.
    /// </summary>
    public int SampleRate => _sampleRate;

    /// <summary>
    /// Gets the number of channels being analyzed.
    /// </summary>
    public int Channels => _channels;

    /// <summary>
    /// Gets whether analysis is currently running.
    /// </summary>
    public bool IsAnalyzing => _isAnalyzing;

    /// <summary>
    /// Gets or sets the time series interval in milliseconds.
    /// </summary>
    public int TimeSeriesIntervalMs { get; set; } = 100;

    /// <summary>
    /// Gets or sets the section detection threshold in dB.
    /// </summary>
    public float SectionDetectionThresholdDb { get; set; } = 3.0f;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new dynamics analyzer with the specified configuration.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate in Hz (default: 44100).</param>
    /// <param name="channels">Number of audio channels (default: 2).</param>
    /// <param name="fftSize">FFT window size for per-band analysis (default: 4096).</param>
    public DynamicsAnalyzer(int sampleRate = 44100, int channels = 2, int fftSize = DefaultFftSize)
    {
        if (!IsPowerOfTwo(fftSize))
            throw new ArgumentException("FFT size must be a power of two.", nameof(fftSize));

        _sampleRate = sampleRate;
        _channels = channels;
        _fftSize = fftSize;

        // Initialize FFT buffers
        _fftBuffer = new Complex[fftSize];
        _sampleBuffer = new float[fftSize];
        _window = GenerateHannWindow(fftSize);

        // Calculate band bin ranges
        float binResolution = (float)sampleRate / fftSize;
        _bandBinRanges = new int[BandFrequencies.Length * 2];
        for (int i = 0; i < BandFrequencies.Length; i++)
        {
            _bandBinRanges[i * 2] = Math.Max(1, (int)(BandFrequencies[i].Low / binResolution));
            _bandBinRanges[i * 2 + 1] = Math.Min(fftSize / 2 - 1, (int)(BandFrequencies[i].High / binResolution));
        }

        // Initialize K-weighting filter
        (_hsB, _hsA) = CalculateHighShelfCoefficients(sampleRate);
        (_hpB, _hpA) = CalculateHighPassCoefficients(sampleRate);
        _hsState = new double[channels, 2];
        _hpState = new double[channels, 2];

        // Initialize histogram
        _levelHistogram = new long[HistogramBins];

        // Initialize time series
        _levelOverTime = new List<LevelTimePoint>();
        _timeSeriesInterval = (int)(sampleRate * TimeSeriesIntervalMs / 1000.0);

        // Initialize LUFS buffers
        int momentarySamples = (int)(sampleRate * MomentaryBlockMs / 1000.0);
        _momentaryBuffer = new double[momentarySamples];
        _gatedBlocks = new List<double>();
        _shortTermBlocks = new List<double>();
        _allLufsBlocks = new List<double>();

        // Initialize per-band accumulators
        int bandCount = BandFrequencies.Length;
        _bandRmsSum = new double[bandCount];
        _bandPeaks = new float[bandCount];
        _bandMinLevels = new double[bandCount];
        _bandSampleCounts = new int[bandCount];
        for (int i = 0; i < bandCount; i++)
            _bandMinLevels[i] = float.MaxValue;

        // Initialize transient detection
        _detectedTransients = new List<TransientInfo>();
        _energyHistory = new float[43]; // ~1 second at hop size

        // Initialize section detection
        _sections = new List<SectionDynamics>();

        // Initialize true peak detection
        _truePeakHistory = new float[12];
    }

    /// <summary>
    /// Creates a new dynamics analyzer that wraps an audio source.
    /// </summary>
    /// <param name="source">The audio source to analyze.</param>
    /// <param name="fftSize">FFT window size (default: 4096).</param>
    public DynamicsAnalyzer(ISampleProvider source, int fftSize = DefaultFftSize)
        : this(source.WaveFormat.SampleRate, source.WaveFormat.Channels, fftSize)
    {
        _source = source;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Reads audio samples, performs analysis, and passes through unchanged.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        if (_source == null)
            throw new InvalidOperationException("Read is only available when constructed with a source.");

        int samplesRead = _source.Read(buffer, offset, count);
        if (samplesRead == 0) return 0;

        ProcessSamples(buffer, offset, samplesRead, _channels);
        return samplesRead;
    }

    /// <summary>
    /// Processes audio samples for analysis.
    /// Implements IAnalyzer interface.
    /// </summary>
    public void ProcessSamples(float[] samples, int offset, int count, int channels)
    {
        _isAnalyzing = true;
        int frames = count / channels;

        for (int frame = 0; frame < frames; frame++)
        {
            int sampleIndex = offset + frame * channels;

            // Mix to mono for most analysis
            float monoSample = 0;
            for (int ch = 0; ch < channels; ch++)
            {
                monoSample += samples[sampleIndex + ch];
            }
            monoSample /= channels;

            // Process sample
            ProcessSample(monoSample, samples, sampleIndex, channels);
        }
    }

    /// <summary>
    /// Analyzes a complete audio buffer and returns the analysis result.
    /// </summary>
    /// <param name="samples">Audio samples (interleaved if multi-channel).</param>
    /// <param name="sampleRate">Sample rate (uses analyzer's rate if 0).</param>
    /// <param name="channels">Number of channels (uses analyzer's channels if 0).</param>
    /// <returns>Complete dynamics analysis result.</returns>
    public DynamicsAnalysisResult AnalyzeBuffer(float[] samples, int sampleRate = 0, int channels = 0)
    {
        if (samples == null || samples.Length == 0)
            throw new ArgumentException("Samples cannot be null or empty.", nameof(samples));

        if (sampleRate == 0) sampleRate = _sampleRate;
        if (channels == 0) channels = _channels;

        Reset();

        ProcessSamples(samples, 0, samples.Length, channels);

        float duration = (float)samples.Length / channels / sampleRate;
        return CreateResult(duration);
    }

    /// <summary>
    /// Gets the current analysis result from real-time processing.
    /// </summary>
    public DynamicsAnalysisResult? GetCurrentResult()
    {
        lock (_lock)
        {
            if (_totalSamples == 0)
                return null;

            return CreateResult((float)_totalSamples / _sampleRate);
        }
    }

    /// <summary>
    /// Compares the analyzed audio to a reference track.
    /// </summary>
    /// <param name="referenceSamples">Reference audio samples.</param>
    /// <param name="referenceSampleRate">Reference sample rate.</param>
    /// <param name="referenceChannels">Reference channel count.</param>
    /// <returns>Comparison result.</returns>
    public ReferenceComparisonResult CompareToReference(float[] referenceSamples, int referenceSampleRate = 0, int referenceChannels = 0)
    {
        if (referenceSampleRate == 0) referenceSampleRate = _sampleRate;
        if (referenceChannels == 0) referenceChannels = _channels;

        // Analyze reference
        var refAnalyzer = new DynamicsAnalyzer(referenceSampleRate, referenceChannels, _fftSize);
        var refResult = refAnalyzer.AnalyzeBuffer(referenceSamples, referenceSampleRate, referenceChannels);

        // Get current result
        var currentResult = GetCurrentResult();
        if (currentResult == null)
        {
            return new ReferenceComparisonResult
            {
                Suggestions = new[] { "No audio has been analyzed yet." },
                SimilarityScore = 0
            };
        }

        // Calculate differences
        float loudnessDiff = currentResult.IntegratedLufs - refResult.IntegratedLufs;
        float drDiff = currentResult.DynamicRangeDr - refResult.DynamicRangeDr;
        float crestDiff = currentResult.CrestFactorDb - refResult.CrestFactorDb;
        float truePeakDiff = currentResult.TruePeakDbtp - refResult.TruePeakDbtp;
        float lraDiff = currentResult.LoudnessRangeLu - refResult.LoudnessRangeLu;

        // Band differences
        int bandCount = Math.Min(currentResult.BandDynamics.Length, refResult.BandDynamics.Length);
        float[] bandDiffs = new float[bandCount];
        for (int i = 0; i < bandCount; i++)
        {
            bandDiffs[i] = currentResult.BandDynamics[i].RmsDb - refResult.BandDynamics[i].RmsDb;
        }

        // Generate suggestions
        var suggestions = new List<string>();

        if (Math.Abs(loudnessDiff) > 2)
        {
            suggestions.Add(loudnessDiff > 0
                ? $"Your mix is {loudnessDiff:F1} LUFS louder than reference. Consider reducing overall level."
                : $"Your mix is {-loudnessDiff:F1} LUFS quieter than reference. Consider increasing overall level.");
        }

        if (Math.Abs(drDiff) > 2)
        {
            suggestions.Add(drDiff > 0
                ? $"Your mix has {drDiff} DR more dynamic range. Reference may be more compressed."
                : $"Your mix has {-drDiff} DR less dynamic range. Consider reducing compression.");
        }

        if (truePeakDiff > 1)
        {
            suggestions.Add($"True peak is {truePeakDiff:F1} dB higher than reference. Consider limiting.");
        }

        // Calculate similarity score
        float similarity = 100f;
        similarity -= Math.Min(30, Math.Abs(loudnessDiff) * 5);
        similarity -= Math.Min(20, Math.Abs(drDiff) * 3);
        similarity -= Math.Min(15, Math.Abs(crestDiff) * 2);
        similarity -= Math.Min(10, Math.Abs(truePeakDiff) * 2);
        similarity = Math.Max(0, similarity);

        return new ReferenceComparisonResult
        {
            LoudnessDifferenceLufs = loudnessDiff,
            DynamicRangeDifferenceDb = drDiff,
            CrestFactorDifferenceDb = crestDiff,
            TruePeakDifferenceDb = truePeakDiff,
            LraDifferenceLu = lraDiff,
            BandDifferencesDb = bandDiffs,
            Suggestions = suggestions.ToArray(),
            SimilarityScore = similarity
        };
    }

    /// <summary>
    /// Checks if the analyzed audio has appropriate dynamics for the specified genre.
    /// </summary>
    /// <param name="genre">Target genre.</param>
    /// <returns>Genre check result.</returns>
    public GenreCheckResult CheckGenreAppropriateness(DynamicsGenre genre)
    {
        var currentResult = GetCurrentResult();
        if (currentResult == null)
        {
            return new GenreCheckResult
            {
                Genre = genre,
                Feedback = new[] { "No audio has been analyzed yet." }
            };
        }

        var targets = GenreTargets[genre];
        var feedback = new List<string>();

        bool loudnessOk = currentResult.IntegratedLufs >= targets.MinLufs &&
                          currentResult.IntegratedLufs <= targets.MaxLufs;
        bool drOk = currentResult.DynamicRangeDr >= targets.MinDr &&
                    currentResult.DynamicRangeDr <= targets.MaxDr;

        // Check compression appropriateness
        CompressionLevel expectedCompression = genre switch
        {
            DynamicsGenre.Classical => CompressionLevel.None,
            DynamicsGenre.Jazz => CompressionLevel.Light,
            DynamicsGenre.Acoustic => CompressionLevel.Light,
            DynamicsGenre.Rock => CompressionLevel.Moderate,
            DynamicsGenre.Pop => CompressionLevel.Moderate,
            DynamicsGenre.Electronic => CompressionLevel.Heavy,
            DynamicsGenre.HipHop => CompressionLevel.Heavy,
            DynamicsGenre.Metal => CompressionLevel.Heavy,
            DynamicsGenre.Podcast => CompressionLevel.Moderate,
            DynamicsGenre.Broadcast => CompressionLevel.Moderate,
            _ => CompressionLevel.Moderate
        };

        bool compressionOk = Math.Abs((int)currentResult.CompressionDetected - (int)expectedCompression) <= 1;

        // Generate feedback
        if (!loudnessOk)
        {
            if (currentResult.IntegratedLufs < targets.MinLufs)
                feedback.Add($"Mix is too quiet for {genre} ({currentResult.IntegratedLufs:F1} LUFS). Target: {targets.MinLufs} to {targets.MaxLufs} LUFS.");
            else
                feedback.Add($"Mix is too loud for {genre} ({currentResult.IntegratedLufs:F1} LUFS). Target: {targets.MinLufs} to {targets.MaxLufs} LUFS.");
        }

        if (!drOk)
        {
            if (currentResult.DynamicRangeDr < targets.MinDr)
                feedback.Add($"Dynamic range too low for {genre} (DR{currentResult.DynamicRangeDr}). Target: DR{targets.MinDr} to DR{targets.MaxDr}.");
            else
                feedback.Add($"Dynamic range may be too high for {genre} (DR{currentResult.DynamicRangeDr}). Target: DR{targets.MinDr} to DR{targets.MaxDr}.");
        }

        if (!compressionOk)
        {
            feedback.Add($"Compression level ({currentResult.CompressionDetected}) may not be ideal for {genre}. Expected: {expectedCompression}.");
        }

        if (feedback.Count == 0)
        {
            feedback.Add($"Dynamics are appropriate for {genre}!");
        }

        // Calculate appropriateness score
        float score = 100f;
        if (!loudnessOk)
        {
            float lufsOff = currentResult.IntegratedLufs < targets.MinLufs
                ? targets.MinLufs - currentResult.IntegratedLufs
                : currentResult.IntegratedLufs - targets.MaxLufs;
            score -= Math.Min(40, lufsOff * 5);
        }
        if (!drOk)
        {
            float drOff = currentResult.DynamicRangeDr < targets.MinDr
                ? targets.MinDr - currentResult.DynamicRangeDr
                : currentResult.DynamicRangeDr - targets.MaxDr;
            score -= Math.Min(30, drOff * 3);
        }
        if (!compressionOk) score -= 15;
        score = Math.Max(0, score);

        return new GenreCheckResult
        {
            Genre = genre,
            AppropriatenessScore = score,
            LoudnessAppropriate = loudnessOk,
            DynamicRangeAppropriate = drOk,
            CompressionAppropriate = compressionOk,
            ExpectedLufsRange = (targets.MinLufs, targets.MaxLufs),
            ExpectedDynamicRangeDb = (targets.MinDr, targets.MaxDr),
            Feedback = feedback.ToArray()
        };
    }

    /// <summary>
    /// Exports a dynamics report to a file.
    /// </summary>
    /// <param name="filePath">Output file path (.txt, .json, or .html).</param>
    /// <param name="trackName">Optional track name for the report.</param>
    public void ExportReport(string filePath, string? trackName = null)
    {
        var result = GetCurrentResult();
        if (result == null)
            throw new InvalidOperationException("No analysis data available to export.");

        string extension = Path.GetExtension(filePath).ToLowerInvariant();

        string content = extension switch
        {
            ".json" => ExportJsonReport(result, trackName),
            ".html" => ExportHtmlReport(result, trackName),
            _ => ExportTextReport(result, trackName)
        };

        File.WriteAllText(filePath, content);
    }

    /// <summary>
    /// Resets the analyzer state.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _sumSquared = 0;
            _maxPeak = 0;
            _maxTruePeak = 0;
            _totalSamples = 0;
            _sampleBufferPos = 0;
            _frameCount = 0;
            _isAnalyzing = false;

            Array.Clear(_levelHistogram, 0, _levelHistogram.Length);
            _levelOverTime.Clear();
            _timeSeriesSampleCount = 0;
            _timeSeriesRmsSum = 0;
            _timeSeriesPeak = 0;

            Array.Clear(_momentaryBuffer, 0, _momentaryBuffer.Length);
            _momentaryWritePos = 0;
            _gatedBlocks.Clear();
            _shortTermBlocks.Clear();
            _allLufsBlocks.Clear();
            _integratedLoudness = double.NegativeInfinity;
            _shortTermLoudness = double.NegativeInfinity;
            _momentaryLoudness = double.NegativeInfinity;

            Array.Clear(_bandRmsSum, 0, _bandRmsSum.Length);
            Array.Clear(_bandPeaks, 0, _bandPeaks.Length);
            Array.Clear(_bandSampleCounts, 0, _bandSampleCounts.Length);
            for (int i = 0; i < _bandMinLevels.Length; i++)
                _bandMinLevels[i] = float.MaxValue;

            _clippedSamples = 0;
            _clippingEvents = 0;
            _currentClipRun = 0;
            _maxClipRun = 0;
            _interSamplePeaks = 0;
            _maxInterSampleOvershoot = 0;

            _detectedTransients.Clear();
            _previousEnergy = 0;
            Array.Clear(_energyHistory, 0, _energyHistory.Length);
            _energyHistoryPos = 0;

            _sections.Clear();
            _currentSectionRmsSum = 0;
            _currentSectionPeak = 0;
            _currentSectionSamples = 0;
            _sectionStartTime = 0;

            Array.Clear(_truePeakHistory, 0, _truePeakHistory.Length);
            _truePeakHistoryPos = 0;

            Array.Clear(_hsState, 0, _hsState.Length);
            Array.Clear(_hpState, 0, _hpState.Length);
            Array.Clear(_sampleBuffer, 0, _sampleBuffer.Length);
            Array.Clear(_fftBuffer, 0, _fftBuffer.Length);
        }
    }

    #endregion

    #region Private Methods

    private void ProcessSample(float monoSample, float[] allSamples, int sampleIndex, int channels)
    {
        lock (_lock)
        {
            _totalSamples++;
            float absSample = Math.Abs(monoSample);

            // Peak detection
            if (absSample > _maxPeak)
                _maxPeak = absSample;

            // True peak detection with oversampling
            float truePeak = ProcessTruePeak(monoSample);
            if (truePeak > _maxTruePeak)
                _maxTruePeak = truePeak;

            // RMS accumulator
            _sumSquared += monoSample * monoSample;

            // Histogram
            float levelDb = 20f * (float)Math.Log10(Math.Max(absSample, 1e-10f));
            int histBin = (int)Math.Floor(levelDb + 90);
            histBin = Math.Clamp(histBin, 0, HistogramBins - 1);
            _levelHistogram[histBin]++;

            // Clipping detection
            if (absSample >= ClippingThreshold)
            {
                _clippedSamples++;
                _currentClipRun++;
                if (_currentClipRun == 1)
                    _clippingEvents++;
                if (_currentClipRun > _maxClipRun)
                    _maxClipRun = _currentClipRun;
            }
            else
            {
                _currentClipRun = 0;
            }

            // Inter-sample peak detection
            if (truePeak > 1.0f)
            {
                _interSamplePeaks++;
                float overshootDb = 20f * (float)Math.Log10(truePeak);
                if (overshootDb > _maxInterSampleOvershoot)
                    _maxInterSampleOvershoot = overshootDb;
            }

            // K-weighted loudness (simplified single-channel)
            double kWeighted = ApplyKWeighting(monoSample, 0);

            // Momentary loudness buffer
            _momentaryBuffer[_momentaryWritePos] = kWeighted * kWeighted;
            _momentaryWritePos = (_momentaryWritePos + 1) % _momentaryBuffer.Length;

            // Update LUFS periodically
            int hopSamples = (int)(_sampleRate * 0.1); // 100ms hop
            if (_totalSamples % hopSamples == 0)
            {
                UpdateLufs();
            }

            // Time series data
            _timeSeriesSampleCount++;
            _timeSeriesRmsSum += monoSample * monoSample;
            if (absSample > _timeSeriesPeak)
                _timeSeriesPeak = absSample;

            if (_timeSeriesSampleCount >= _timeSeriesInterval)
            {
                float timeSeconds = (float)_totalSamples / _sampleRate;
                float rmsDb = 10f * (float)Math.Log10(Math.Max(_timeSeriesRmsSum / _timeSeriesSampleCount, 1e-10));
                float peakDb = 20f * (float)Math.Log10(Math.Max(_timeSeriesPeak, 1e-10f));

                _levelOverTime.Add(new LevelTimePoint
                {
                    TimeSeconds = timeSeconds,
                    RmsDb = rmsDb,
                    PeakDb = peakDb,
                    MomentaryLufs = (float)_momentaryLoudness,
                    CrestFactorDb = peakDb - rmsDb
                });

                _timeSeriesSampleCount = 0;
                _timeSeriesRmsSum = 0;
                _timeSeriesPeak = 0;
            }

            // Section detection
            _currentSectionSamples++;
            _currentSectionRmsSum += monoSample * monoSample;
            if (absSample > _currentSectionPeak)
                _currentSectionPeak = absSample;

            int sectionSamples = (int)(_sampleRate * SectionMinDurationMs / 1000.0);
            if (_currentSectionSamples >= sectionSamples)
            {
                float sectionRms = (float)Math.Sqrt(_currentSectionRmsSum / _currentSectionSamples);
                float sectionRmsDb = 20f * (float)Math.Log10(Math.Max(sectionRms, 1e-10f));

                // Check if this is a new section (significant level change)
                bool newSection = _sections.Count == 0;
                if (_sections.Count > 0)
                {
                    float lastRmsDb = _sections[^1].AverageRmsDb;
                    if (Math.Abs(sectionRmsDb - lastRmsDb) > SectionDetectionThresholdDb)
                        newSection = true;
                }

                if (newSection || _currentSectionSamples >= sectionSamples * 4)
                {
                    float endTime = (float)_totalSamples / _sampleRate;
                    _sections.Add(new SectionDynamics
                    {
                        Index = _sections.Count,
                        StartTimeSeconds = _sectionStartTime,
                        EndTimeSeconds = endTime,
                        AverageRmsDb = sectionRmsDb,
                        PeakDb = 20f * (float)Math.Log10(Math.Max(_currentSectionPeak, 1e-10f)),
                        AverageLufs = (float)_momentaryLoudness,
                        DynamicRangeDb = 20f * (float)Math.Log10(Math.Max(_currentSectionPeak, 1e-10f)) - sectionRmsDb
                    });

                    _sectionStartTime = endTime;
                    _currentSectionRmsSum = 0;
                    _currentSectionPeak = 0;
                    _currentSectionSamples = 0;
                }
            }

            // FFT buffer for per-band analysis
            _sampleBuffer[_sampleBufferPos] = monoSample;
            _sampleBufferPos++;

            if (_sampleBufferPos >= _fftSize)
            {
                ProcessFftFrame();
                _sampleBufferPos = 0;
            }

            // Transient detection
            DetectTransients(monoSample);
        }
    }

    private float ProcessTruePeak(float sample)
    {
        _truePeakHistory[_truePeakHistoryPos] = sample;
        _truePeakHistoryPos = (_truePeakHistoryPos + 1) % _truePeakHistory.Length;

        float maxPeak = Math.Abs(sample);

        // 4x oversampling interpolation
        for (int phase = 0; phase < 4; phase++)
        {
            float interpolated = 0;
            int coeffIndex = phase;
            int histIndex = _truePeakHistoryPos;

            for (int i = 0; i < _truePeakHistory.Length; i++)
            {
                histIndex--;
                if (histIndex < 0) histIndex = _truePeakHistory.Length - 1;

                if (coeffIndex < OversamplingCoeffs.Length)
                {
                    interpolated += _truePeakHistory[histIndex] * OversamplingCoeffs[coeffIndex];
                }
                coeffIndex += 4;
            }

            float absPeak = Math.Abs(interpolated);
            if (absPeak > maxPeak)
                maxPeak = absPeak;
        }

        return maxPeak;
    }

    private double ApplyKWeighting(float sample, int channel)
    {
        // Stage 1: High shelf filter
        double x = sample;
        double y1 = _hsB[0] * x + _hsState[channel, 0];
        _hsState[channel, 0] = _hsB[1] * x - _hsA[1] * y1 + _hsState[channel, 1];
        _hsState[channel, 1] = _hsB[2] * x - _hsA[2] * y1;

        // Stage 2: High pass filter
        double y2 = _hpB[0] * y1 + _hpState[channel, 0];
        _hpState[channel, 0] = _hpB[1] * y1 - _hpA[1] * y2 + _hpState[channel, 1];
        _hpState[channel, 1] = _hpB[2] * y1 - _hpA[2] * y2;

        return y2;
    }

    private void UpdateLufs()
    {
        // Momentary loudness (400ms)
        double momentarySum = 0;
        for (int i = 0; i < _momentaryBuffer.Length; i++)
        {
            momentarySum += _momentaryBuffer[i];
        }
        double momentaryMeanSquare = momentarySum / _momentaryBuffer.Length;
        _momentaryLoudness = -0.691 + 10.0 * Math.Log10(Math.Max(momentaryMeanSquare, 1e-10));

        // Store block for integrated and short-term
        _allLufsBlocks.Add(momentaryMeanSquare);
        _shortTermBlocks.Add(momentaryMeanSquare);

        // Keep only last 3 seconds of blocks for short-term
        int shortTermBlockCount = (int)(ShortTermBlockMs / 100.0);
        while (_shortTermBlocks.Count > shortTermBlockCount)
            _shortTermBlocks.RemoveAt(0);

        // Short-term loudness (3 seconds)
        if (_shortTermBlocks.Count > 0)
        {
            double shortTermSum = _shortTermBlocks.Sum();
            double shortTermMeanSquare = shortTermSum / _shortTermBlocks.Count;
            _shortTermLoudness = -0.691 + 10.0 * Math.Log10(Math.Max(shortTermMeanSquare, 1e-10));
        }

        // Integrated loudness with gating
        if (_momentaryLoudness > -70.0) // Absolute threshold
        {
            _gatedBlocks.Add(momentaryMeanSquare);
            UpdateIntegratedLoudness();
        }
    }

    private void UpdateIntegratedLoudness()
    {
        if (_gatedBlocks.Count == 0) return;

        // First pass: ungated loudness
        double ungatedSum = _gatedBlocks.Sum();
        double ungatedLoudness = -0.691 + 10.0 * Math.Log10(ungatedSum / _gatedBlocks.Count);

        // Relative threshold
        double relativeThreshold = ungatedLoudness - 10.0;

        // Second pass: gated loudness
        double gatedSum = 0;
        int gatedCount = 0;

        foreach (var block in _gatedBlocks)
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
            _integratedLoudness = -0.691 + 10.0 * Math.Log10(gatedSum / gatedCount);
        }
    }

    private void ProcessFftFrame()
    {
        // Apply window
        for (int i = 0; i < _fftSize; i++)
        {
            _fftBuffer[i].X = _sampleBuffer[i] * _window[i];
            _fftBuffer[i].Y = 0;
        }

        // FFT
        int m = (int)Math.Log(_fftSize, 2.0);
        FastFourierTransform.FFT(true, m, _fftBuffer);

        // Per-band analysis
        for (int band = 0; band < BandFrequencies.Length; band++)
        {
            int lowBin = _bandBinRanges[band * 2];
            int highBin = _bandBinRanges[band * 2 + 1];

            double energy = 0;
            float peak = 0;

            for (int bin = lowBin; bin <= highBin; bin++)
            {
                float magnitude = (float)Math.Sqrt(
                    _fftBuffer[bin].X * _fftBuffer[bin].X +
                    _fftBuffer[bin].Y * _fftBuffer[bin].Y);

                energy += magnitude * magnitude;
                peak = Math.Max(peak, magnitude);
            }

            _bandRmsSum[band] += energy;
            _bandPeaks[band] = Math.Max(_bandPeaks[band], peak);
            if (energy > 0 && energy < _bandMinLevels[band])
                _bandMinLevels[band] = energy;
            _bandSampleCounts[band]++;
        }

        _frameCount++;
    }

    private void DetectTransients(float sample)
    {
        // Simple energy-based transient detection
        float energy = sample * sample;

        // Calculate local average and variance
        float avgEnergy = 0;
        for (int i = 0; i < _energyHistory.Length; i++)
            avgEnergy += _energyHistory[i];
        avgEnergy /= _energyHistory.Length;

        _energyHistory[_energyHistoryPos] = energy;
        _energyHistoryPos = (_energyHistoryPos + 1) % _energyHistory.Length;

        // Detect onset
        float flux = Math.Max(0, energy - _previousEnergy);
        float threshold = avgEnergy * (1 + TransientThreshold * 10);

        if (flux > threshold && energy > 0.001f)
        {
            // Check minimum interval
            float currentTime = (float)_totalSamples / _sampleRate;
            bool canAdd = _detectedTransients.Count == 0 ||
                          (currentTime - _detectedTransients[^1].TimeSeconds) > 0.05f;

            if (canAdd)
            {
                float peakDb = 20f * (float)Math.Log10(Math.Max(Math.Sqrt(energy), 1e-10f));
                bool isStrong = flux > threshold * 2;

                _detectedTransients.Add(new TransientInfo
                {
                    TimeSeconds = currentTime,
                    PeakDb = peakDb,
                    AttackTimeMs = 5f, // Estimated
                    ReleaseTimeMs = 50f, // Estimated
                    IsStrong = isStrong
                });
            }
        }

        _previousEnergy = energy;
    }

    private DynamicsAnalysisResult CreateResult(float durationSeconds)
    {
        lock (_lock)
        {
            if (_totalSamples == 0)
            {
                return new DynamicsAnalysisResult
                {
                    DurationSeconds = 0,
                    SampleRate = _sampleRate,
                    Channels = _channels
                };
            }

            // Basic measurements
            float rms = (float)Math.Sqrt(_sumSquared / _totalSamples);
            float rmsDb = 20f * (float)Math.Log10(Math.Max(rms, 1e-10f));
            float peakDb = 20f * (float)Math.Log10(Math.Max(_maxPeak, 1e-10f));
            float truePeakDbtp = 20f * (float)Math.Log10(Math.Max(_maxTruePeak, 1e-10f));
            float crestFactorDb = peakDb - rmsDb;
            float plr = truePeakDbtp - (float)_integratedLoudness;

            // Calculate LRA (Loudness Range)
            float lra = CalculateLra();

            // Calculate DR (Dynamic Range score)
            int dr = CalculateDrScore();

            // Create histogram result
            var histogram = CreateHistogram();

            // Per-band dynamics
            var bandDynamics = CreateBandDynamics();

            // Detect compression level
            var (compression, compressionPercent) = DetectCompression(crestFactorDb, dr);

            // Detect limiting level
            var (limiting, limitingPercent) = DetectLimiting();

            // Create clipping result
            var clipping = new ClippingResult
            {
                ClippedSampleCount = _clippedSamples,
                ClippingPercentage = (float)_clippedSamples / _totalSamples * 100f,
                ClippingEventCount = _clippingEvents,
                MaxConsecutiveClippedSamples = _maxClipRun,
                InterSamplePeakCount = _interSamplePeaks,
                MaxInterSampleOvershootDb = _maxInterSampleOvershoot
            };

            // Transient analysis
            var transientAnalysis = CreateTransientAnalysis(durationSeconds);

            // Sustain analysis
            var sustainAnalysis = CreateSustainAnalysis();

            // Macro dynamics
            var macroDynamics = CreateMacroDynamics();

            // Micro dynamics
            var microDynamics = CreateMicroDynamics();

            // Recommendations
            var recommendations = CreateRecommendations(
                (float)_integratedLoudness, dr, crestFactorDb, compression, limiting, clipping);

            return new DynamicsAnalysisResult
            {
                PeakDb = peakDb,
                RmsDb = rmsDb,
                IntegratedLufs = (float)_integratedLoudness,
                ShortTermLufs = (float)_shortTermLoudness,
                MomentaryLufs = (float)_momentaryLoudness,
                TruePeakDbtp = truePeakDbtp,
                LoudnessRangeLu = lra,
                DynamicRangeDr = dr,
                CrestFactorDb = crestFactorDb,
                PeakToLoudnessRatioDb = plr,
                Histogram = histogram,
                LevelOverTime = _levelOverTime.ToArray(),
                CompressionDetected = compression,
                CompressionAmountPercent = compressionPercent,
                LimitingDetected = limiting,
                LimitingAmountPercent = limitingPercent,
                ClippingResult = clipping,
                BandDynamics = bandDynamics,
                TransientAnalysis = transientAnalysis,
                SustainAnalysis = sustainAnalysis,
                MacroDynamics = macroDynamics,
                MicroDynamics = microDynamics,
                Recommendations = recommendations,
                DurationSeconds = durationSeconds,
                SampleRate = _sampleRate,
                Channels = _channels
            };
        }
    }

    private float CalculateLra()
    {
        if (_allLufsBlocks.Count < 10) return 0;

        // Sort blocks by loudness
        var sortedBlocks = _allLufsBlocks
            .Select(b => -0.691 + 10.0 * Math.Log10(Math.Max(b, 1e-10)))
            .Where(l => l > -70) // Absolute gate
            .OrderBy(l => l)
            .ToList();

        if (sortedBlocks.Count < 2) return 0;

        // Calculate 10th and 95th percentiles
        int low = (int)(sortedBlocks.Count * 0.10);
        int high = (int)(sortedBlocks.Count * 0.95);

        return (float)(sortedBlocks[high] - sortedBlocks[low]);
    }

    private int CalculateDrScore()
    {
        // TT Dynamic Range Meter algorithm (simplified)
        // DR = 20 * log10(RMS of top 20% / RMS of entire audio)

        if (_allLufsBlocks.Count < 10) return 0;

        var sortedBlocks = _allLufsBlocks.OrderByDescending(b => b).ToList();
        int top20Count = Math.Max(1, sortedBlocks.Count / 5);

        double top20Rms = Math.Sqrt(sortedBlocks.Take(top20Count).Average());
        double overallRms = Math.Sqrt(sortedBlocks.Average());

        if (overallRms < 1e-10) return 0;

        float dr = 20f * (float)Math.Log10(top20Rms / overallRms);

        // DR scores typically range from 1-20
        return Math.Clamp((int)Math.Round(dr), 1, 20);
    }

    private LevelHistogram CreateHistogram()
    {
        // Find mode and median
        long maxCount = 0;
        int modeIndex = 0;
        long totalSamples = 0;

        for (int i = 0; i < HistogramBins; i++)
        {
            totalSamples += _levelHistogram[i];
            if (_levelHistogram[i] > maxCount)
            {
                maxCount = _levelHistogram[i];
                modeIndex = i;
            }
        }

        // Find median
        long halfTotal = totalSamples / 2;
        long runningSum = 0;
        int medianIndex = 0;
        for (int i = 0; i < HistogramBins; i++)
        {
            runningSum += _levelHistogram[i];
            if (runningSum >= halfTotal)
            {
                medianIndex = i;
                break;
            }
        }

        return new LevelHistogram
        {
            BinCounts = _levelHistogram.Select(l => (int)l).ToArray(),
            BinWidthDb = 1f,
            MinDb = -90f,
            MaxDb = 0f,
            TotalSamples = totalSamples,
            ModeDb = modeIndex - 90f,
            MedianDb = medianIndex - 90f
        };
    }

    private BandDynamicsResult[] CreateBandDynamics()
    {
        var results = new BandDynamicsResult[BandFrequencies.Length];

        for (int i = 0; i < BandFrequencies.Length; i++)
        {
            float rms = _bandSampleCounts[i] > 0
                ? (float)Math.Sqrt(_bandRmsSum[i] / _bandSampleCounts[i])
                : 0;

            float rmsDb = 20f * (float)Math.Log10(Math.Max(rms, 1e-10f));
            float peakDb = 20f * (float)Math.Log10(Math.Max(_bandPeaks[i], 1e-10f));
            float minDb = _bandMinLevels[i] < float.MaxValue
                ? 10f * (float)Math.Log10(Math.Max(_bandMinLevels[i], 1e-10))
                : -90f;

            float dynamicRange = peakDb - minDb;
            float crestFactor = peakDb - rmsDb;

            CompressionLevel bandCompression = crestFactor switch
            {
                < 3 => CompressionLevel.Extreme,
                < 6 => CompressionLevel.Heavy,
                < 10 => CompressionLevel.Moderate,
                < 14 => CompressionLevel.Light,
                _ => CompressionLevel.None
            };

            results[i] = new BandDynamicsResult
            {
                BandIndex = i,
                LowFrequency = BandFrequencies[i].Low,
                HighFrequency = BandFrequencies[i].High,
                RmsDb = rmsDb,
                PeakDb = peakDb,
                DynamicRangeDb = dynamicRange,
                CrestFactorDb = crestFactor,
                Compression = bandCompression
            };
        }

        return results;
    }

    private (CompressionLevel, float) DetectCompression(float crestFactor, int dr)
    {
        // Combine crest factor and DR to estimate compression
        float compressionScore = 0;

        // Lower crest factor = more compression
        if (crestFactor < 6) compressionScore += 40;
        else if (crestFactor < 10) compressionScore += 25;
        else if (crestFactor < 14) compressionScore += 10;

        // Lower DR = more compression
        if (dr < 6) compressionScore += 40;
        else if (dr < 10) compressionScore += 25;
        else if (dr < 14) compressionScore += 10;

        // Check histogram for flattened peaks
        if (_levelHistogram.Length > 0)
        {
            long peakBinCount = _levelHistogram[^1] + _levelHistogram[^2] + _levelHistogram[^3];
            float peakRatio = (float)peakBinCount / _totalSamples;
            if (peakRatio > 0.01f) compressionScore += 20;
        }

        CompressionLevel level = compressionScore switch
        {
            >= 80 => CompressionLevel.Extreme,
            >= 60 => CompressionLevel.Heavy,
            >= 40 => CompressionLevel.Moderate,
            >= 20 => CompressionLevel.Light,
            _ => CompressionLevel.None
        };

        return (level, compressionScore);
    }

    private (LimitingLevel, float) DetectLimiting()
    {
        // Check for brickwall limiting indicators
        float limitingScore = 0;

        // Many samples at or near 0dBFS
        long nearMaxSamples = 0;
        for (int i = HistogramBins - 3; i < HistogramBins; i++)
        {
            if (i >= 0) nearMaxSamples += _levelHistogram[i];
        }
        float nearMaxRatio = (float)nearMaxSamples / Math.Max(_totalSamples, 1);

        if (nearMaxRatio > 0.05f) limitingScore += 40;
        else if (nearMaxRatio > 0.01f) limitingScore += 25;
        else if (nearMaxRatio > 0.001f) limitingScore += 10;

        // True peak close to 0dBFS
        float truePeakDb = 20f * (float)Math.Log10(Math.Max(_maxTruePeak, 1e-10f));
        if (truePeakDb > -0.3f) limitingScore += 30;
        else if (truePeakDb > -1f) limitingScore += 15;

        // Inter-sample peaks indicate heavy limiting
        if (_interSamplePeaks > 100) limitingScore += 20;
        else if (_interSamplePeaks > 10) limitingScore += 10;

        LimitingLevel level = limitingScore switch
        {
            >= 80 => LimitingLevel.Brickwall,
            >= 60 => LimitingLevel.Heavy,
            >= 40 => LimitingLevel.Moderate,
            >= 20 => LimitingLevel.Gentle,
            _ => LimitingLevel.None
        };

        return (level, limitingScore);
    }

    private TransientCharacteristics CreateTransientAnalysis(float durationSeconds)
    {
        if (_detectedTransients.Count == 0)
        {
            return new TransientCharacteristics
            {
                TransientCount = 0,
                TransientsPerSecond = 0
            };
        }

        float avgAttack = _detectedTransients.Average(t => t.AttackTimeMs);
        float avgRelease = _detectedTransients.Average(t => t.ReleaseTimeMs);
        float avgPeak = _detectedTransients.Average(t => t.PeakDb);
        float density = _detectedTransients.Count / durationSeconds;
        float sharpness = 1f - Math.Min(1f, avgAttack / 20f); // Faster attack = sharper

        return new TransientCharacteristics
        {
            TransientCount = _detectedTransients.Count,
            TransientsPerSecond = density,
            AverageAttackTimeMs = avgAttack,
            AverageReleaseTimeMs = avgRelease,
            AverageTransientPeakDb = avgPeak,
            TransientDensity = Math.Min(1f, density / 10f),
            TransientSharpness = sharpness,
            Transients = _detectedTransients.ToArray()
        };
    }

    private SustainCharacteristics CreateSustainAnalysis()
    {
        // Analyze sustain from histogram and level data
        float peakDb = 20f * (float)Math.Log10(Math.Max(_maxPeak, 1e-10f));
        float rmsDb = 10f * (float)Math.Log10(Math.Max(_sumSquared / _totalSamples, 1e-10));

        float sustainLevel = rmsDb - peakDb; // How much below peak

        // Estimate consistency from histogram spread
        float[] normalized = new LevelHistogram { BinCounts = _levelHistogram.Select(l => (int)l).ToArray() }.NormalizedBins;
        float variance = 0;
        float mean = 0;
        for (int i = 0; i < normalized.Length; i++)
        {
            mean += (i - 45) * normalized[i]; // Center at -45dB
        }
        for (int i = 0; i < normalized.Length; i++)
        {
            float diff = (i - 45) - mean;
            variance += diff * diff * normalized[i];
        }
        float consistency = 1f - Math.Min(1f, (float)Math.Sqrt(variance) / 20f);

        return new SustainCharacteristics
        {
            AverageSustainLevelDb = sustainLevel,
            SustainConsistency = consistency,
            AverageSustainDurationMs = 100f, // Estimated
            SustainToPeakRatio = (float)Math.Pow(10, sustainLevel / 20)
        };
    }

    private MacroDynamicsResult CreateMacroDynamics()
    {
        if (_sections.Count == 0)
        {
            return new MacroDynamicsResult
            {
                Sections = Array.Empty<SectionDynamics>(),
                MacroDynamicRangeDb = 0
            };
        }

        var loudest = _sections.OrderByDescending(s => s.AverageRmsDb).First();
        var quietest = _sections.OrderBy(s => s.AverageRmsDb).First();

        float macroDr = loudest.AverageRmsDb - quietest.AverageRmsDb;

        float avgVariation = 0;
        for (int i = 1; i < _sections.Count; i++)
        {
            avgVariation += Math.Abs(_sections[i].AverageRmsDb - _sections[i - 1].AverageRmsDb);
        }
        if (_sections.Count > 1)
            avgVariation /= (_sections.Count - 1);

        return new MacroDynamicsResult
        {
            Sections = _sections.ToArray(),
            MacroDynamicRangeDb = macroDr,
            LoudestSection = loudest,
            QuietestSection = quietest,
            AverageLoudnessVariationDb = avgVariation
        };
    }

    private MicroDynamicsResult CreateMicroDynamics()
    {
        // Estimate beat-level dynamics from transients
        if (_detectedTransients.Count < 4)
        {
            return new MicroDynamicsResult
            {
                Beats = Array.Empty<BeatDynamics>()
            };
        }

        // Estimate tempo from transients
        var intervals = new List<float>();
        for (int i = 1; i < _detectedTransients.Count; i++)
        {
            float interval = _detectedTransients[i].TimeSeconds - _detectedTransients[i - 1].TimeSeconds;
            if (interval > 0.2f && interval < 2f) // Reasonable beat interval
                intervals.Add(interval);
        }

        float avgInterval = intervals.Count > 0 ? intervals.Average() : 0.5f;
        float estimatedBpm = 60f / avgInterval;

        // Create beat dynamics from strong transients
        var beats = _detectedTransients
            .Where(t => t.IsStrong)
            .Select((t, i) => new BeatDynamics
            {
                Index = i,
                TimeSeconds = t.TimeSeconds,
                PeakDb = t.PeakDb,
                RmsDb = t.PeakDb - 6, // Estimated
                IsDownbeat = i % 4 == 0
            })
            .ToArray();

        float beatDr = beats.Length > 1
            ? beats.Max(b => b.PeakDb) - beats.Min(b => b.PeakDb)
            : 0;

        // Beat consistency
        var beatLevels = beats.Select(b => b.PeakDb).ToList();
        float beatVariance = 0;
        if (beatLevels.Count > 1)
        {
            float mean = beatLevels.Average();
            beatVariance = beatLevels.Select(l => (l - mean) * (l - mean)).Average();
        }
        float consistency = 1f - Math.Min(1f, (float)Math.Sqrt(beatVariance) / 6f);

        return new MicroDynamicsResult
        {
            AverageBeatDynamicRangeDb = beatDr,
            BeatConsistency = consistency,
            GrooveAmount = Math.Min(1f, (1f - consistency) * 2), // Less consistent = more groove
            TempoBypassedBpm = estimatedBpm,
            Beats = beats
        };
    }

    private DynamicsRecommendations CreateRecommendations(
        float integratedLufs, int dr, float crestFactor,
        CompressionLevel compression, LimitingLevel limiting, ClippingResult clipping)
    {
        var textRecs = new List<string>();
        bool compRec = false;
        bool limitRec = false;
        bool expandRec = false;
        bool transientRec = false;
        float suggestedRatio = 1f;
        float suggestedThreshold = 0f;
        float suggestedAttack = 10f;
        float suggestedRelease = 100f;
        float suggestedCeiling = -1f;
        float suggestedGain = 0f;

        // Check if too quiet
        if (integratedLufs < -20)
        {
            suggestedGain = -14f - integratedLufs;
            textRecs.Add($"Mix is quiet ({integratedLufs:F1} LUFS). Consider adding {suggestedGain:F1} dB gain.");
        }

        // Check if over-compressed
        if (compression >= CompressionLevel.Heavy || dr < 6)
        {
            expandRec = true;
            textRecs.Add($"Mix appears over-compressed (DR{dr}). Consider using less compression or adding expansion.");
        }
        else if (compression == CompressionLevel.None && dr > 16)
        {
            compRec = true;
            suggestedRatio = 2f;
            suggestedThreshold = -18f;
            suggestedAttack = 20f;
            suggestedRelease = 200f;
            textRecs.Add($"High dynamic range (DR{dr}). Consider gentle compression (2:1, -18dB threshold).");
        }

        // Check if limiting needed
        if (limiting == LimitingLevel.None && _maxTruePeak > 0.9f)
        {
            limitRec = true;
            suggestedCeiling = -1f;
            textRecs.Add("Peaks are high. Consider using a limiter with -1dB ceiling.");
        }
        else if (limiting >= LimitingLevel.Heavy)
        {
            textRecs.Add("Heavy limiting detected. Consider backing off the limiter for more dynamics.");
        }

        // Check clipping
        if (clipping.HasProblematicClipping)
        {
            limitRec = true;
            suggestedGain = -3f;
            textRecs.Add($"Clipping detected ({clipping.ClippingEventCount} events). Reduce input gain and use a limiter.");
        }

        // Check inter-sample peaks
        if (clipping.InterSamplePeakCount > 50)
        {
            suggestedCeiling = -0.5f;
            textRecs.Add($"Inter-sample peaks detected ({clipping.InterSamplePeakCount}). Lower ceiling to -0.5 dBTP or use true-peak limiting.");
        }

        // Check crest factor
        if (crestFactor < 4)
        {
            transientRec = true;
            textRecs.Add("Low crest factor - transients may be squashed. Consider transient shaping or less compression.");
        }
        else if (crestFactor > 18)
        {
            compRec = true;
            suggestedAttack = 5f;
            textRecs.Add("Very high crest factor. Fast-attack compression may help control peaks.");
        }

        if (textRecs.Count == 0)
        {
            textRecs.Add("Dynamics appear well-balanced. No major issues detected.");
        }

        return new DynamicsRecommendations
        {
            CompressionRecommended = compRec,
            SuggestedCompressionRatio = suggestedRatio,
            SuggestedThresholdDb = suggestedThreshold,
            SuggestedAttackMs = suggestedAttack,
            SuggestedReleaseMs = suggestedRelease,
            LimitingRecommended = limitRec,
            SuggestedLimiterCeilingDb = suggestedCeiling,
            ExpansionRecommended = expandRec,
            TransientShapingRecommended = transientRec,
            SuggestedGainAdjustmentDb = suggestedGain,
            TextRecommendations = textRecs.ToArray()
        };
    }

    #endregion

    #region Export Methods

    private string ExportTextReport(DynamicsAnalysisResult result, string? trackName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=".PadRight(60, '='));
        sb.AppendLine("              DYNAMICS ANALYSIS REPORT");
        sb.AppendLine("=".PadRight(60, '='));
        sb.AppendLine();

        if (!string.IsNullOrEmpty(trackName))
            sb.AppendLine($"Track: {trackName}");

        sb.AppendLine($"Duration: {result.DurationSeconds:F2} seconds");
        sb.AppendLine($"Sample Rate: {result.SampleRate} Hz");
        sb.AppendLine($"Channels: {result.Channels}");
        sb.AppendLine($"Analysis Date: {result.Timestamp:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        sb.AppendLine("-".PadRight(60, '-'));
        sb.AppendLine("LOUDNESS MEASUREMENTS");
        sb.AppendLine("-".PadRight(60, '-'));
        sb.AppendLine($"  Integrated Loudness: {result.IntegratedLufs:F1} LUFS");
        sb.AppendLine($"  Short-term Loudness: {result.ShortTermLufs:F1} LUFS");
        sb.AppendLine($"  Momentary Loudness:  {result.MomentaryLufs:F1} LUFS");
        sb.AppendLine($"  Loudness Range (LRA): {result.LoudnessRangeLu:F1} LU");
        sb.AppendLine();

        sb.AppendLine("-".PadRight(60, '-'));
        sb.AppendLine("LEVEL MEASUREMENTS");
        sb.AppendLine("-".PadRight(60, '-'));
        sb.AppendLine($"  Peak Level:     {result.PeakDb:F1} dBFS");
        sb.AppendLine($"  True Peak:      {result.TruePeakDbtp:F1} dBTP");
        sb.AppendLine($"  RMS Level:      {result.RmsDb:F1} dBFS");
        sb.AppendLine($"  Crest Factor:   {result.CrestFactorDb:F1} dB");
        sb.AppendLine($"  Peak-to-LUFS:   {result.PeakToLoudnessRatioDb:F1} dB");
        sb.AppendLine();

        sb.AppendLine("-".PadRight(60, '-'));
        sb.AppendLine("DYNAMIC RANGE");
        sb.AppendLine("-".PadRight(60, '-'));
        sb.AppendLine($"  DR Score: DR{result.DynamicRangeDr}");
        sb.AppendLine($"  Compression Detected: {result.CompressionDetected} ({result.CompressionAmountPercent:F0}%)");
        sb.AppendLine($"  Limiting Detected: {result.LimitingDetected} ({result.LimitingAmountPercent:F0}%)");
        sb.AppendLine();

        if (result.ClippingResult != null)
        {
            sb.AppendLine("-".PadRight(60, '-'));
            sb.AppendLine("CLIPPING ANALYSIS");
            sb.AppendLine("-".PadRight(60, '-'));
            sb.AppendLine($"  Clipped Samples: {result.ClippingResult.ClippedSampleCount}");
            sb.AppendLine($"  Clipping Events: {result.ClippingResult.ClippingEventCount}");
            sb.AppendLine($"  Clipping Severity: {result.ClippingResult.Severity}");
            sb.AppendLine($"  Inter-sample Peaks: {result.ClippingResult.InterSamplePeakCount}");
            if (result.ClippingResult.MaxInterSampleOvershootDb > 0)
                sb.AppendLine($"  Max ISP Overshoot: +{result.ClippingResult.MaxInterSampleOvershootDb:F2} dB");
            sb.AppendLine();
        }

        if (result.BandDynamics.Length > 0)
        {
            sb.AppendLine("-".PadRight(60, '-'));
            sb.AppendLine("PER-BAND DYNAMICS");
            sb.AppendLine("-".PadRight(60, '-'));
            foreach (var band in result.BandDynamics)
            {
                sb.AppendLine($"  {band.LowFrequency:F0}-{band.HighFrequency:F0} Hz: DR {band.DynamicRangeDb:F1} dB, Crest {band.CrestFactorDb:F1} dB ({band.Compression})");
            }
            sb.AppendLine();
        }

        if (result.TransientAnalysis != null && result.TransientAnalysis.TransientCount > 0)
        {
            sb.AppendLine("-".PadRight(60, '-'));
            sb.AppendLine("TRANSIENT ANALYSIS");
            sb.AppendLine("-".PadRight(60, '-'));
            sb.AppendLine($"  Transient Count: {result.TransientAnalysis.TransientCount}");
            sb.AppendLine($"  Transients/Second: {result.TransientAnalysis.TransientsPerSecond:F1}");
            sb.AppendLine($"  Avg Attack Time: {result.TransientAnalysis.AverageAttackTimeMs:F1} ms");
            sb.AppendLine($"  Transient Sharpness: {result.TransientAnalysis.TransientSharpness * 100:F0}%");
            sb.AppendLine();
        }

        if (result.Recommendations != null)
        {
            sb.AppendLine("-".PadRight(60, '-'));
            sb.AppendLine("RECOMMENDATIONS");
            sb.AppendLine("-".PadRight(60, '-'));
            foreach (var rec in result.Recommendations.TextRecommendations)
            {
                sb.AppendLine($"  * {rec}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("=".PadRight(60, '='));
        sb.AppendLine("Generated by MusicEngine DynamicsAnalyzer");
        sb.AppendLine("=".PadRight(60, '='));

        return sb.ToString();
    }

    private string ExportJsonReport(DynamicsAnalysisResult result, string? trackName)
    {
        var report = new
        {
            TrackName = trackName ?? "Unknown",
            result.DurationSeconds,
            result.SampleRate,
            result.Channels,
            Timestamp = result.Timestamp.ToString("O"),
            Loudness = new
            {
                IntegratedLufs = result.IntegratedLufs,
                ShortTermLufs = result.ShortTermLufs,
                MomentaryLufs = result.MomentaryLufs,
                LoudnessRangeLu = result.LoudnessRangeLu
            },
            Levels = new
            {
                PeakDb = result.PeakDb,
                TruePeakDbtp = result.TruePeakDbtp,
                RmsDb = result.RmsDb,
                CrestFactorDb = result.CrestFactorDb,
                PeakToLoudnessRatioDb = result.PeakToLoudnessRatioDb
            },
            DynamicRange = new
            {
                DrScore = result.DynamicRangeDr,
                CompressionDetected = result.CompressionDetected.ToString(),
                CompressionAmountPercent = result.CompressionAmountPercent,
                LimitingDetected = result.LimitingDetected.ToString(),
                LimitingAmountPercent = result.LimitingAmountPercent
            },
            Clipping = result.ClippingResult != null ? new
            {
                ClippedSamples = result.ClippingResult.ClippedSampleCount,
                ClippingEvents = result.ClippingResult.ClippingEventCount,
                Severity = result.ClippingResult.Severity,
                InterSamplePeaks = result.ClippingResult.InterSamplePeakCount,
                MaxInterSampleOvershootDb = result.ClippingResult.MaxInterSampleOvershootDb
            } : null,
            Recommendations = result.Recommendations?.TextRecommendations ?? Array.Empty<string>()
        };

        return JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
    }

    private string ExportHtmlReport(DynamicsAnalysisResult result, string? trackName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html><head>");
        sb.AppendLine("<title>Dynamics Analysis Report</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: Arial, sans-serif; margin: 40px; background: #1a1a2e; color: #eee; }");
        sb.AppendLine("h1 { color: #00d4ff; }");
        sb.AppendLine("h2 { color: #ff6b6b; border-bottom: 1px solid #333; padding-bottom: 5px; }");
        sb.AppendLine("table { border-collapse: collapse; width: 100%; margin: 20px 0; }");
        sb.AppendLine("th, td { padding: 10px; text-align: left; border-bottom: 1px solid #333; }");
        sb.AppendLine("th { background: #16213e; color: #00d4ff; }");
        sb.AppendLine(".metric { font-size: 24px; font-weight: bold; color: #00ff88; }");
        sb.AppendLine(".warning { color: #ffaa00; }");
        sb.AppendLine(".error { color: #ff4444; }");
        sb.AppendLine(".good { color: #00ff88; }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head><body>");

        sb.AppendLine("<h1>Dynamics Analysis Report</h1>");
        if (!string.IsNullOrEmpty(trackName))
            sb.AppendLine($"<p><strong>Track:</strong> {trackName}</p>");
        sb.AppendLine($"<p><strong>Duration:</strong> {result.DurationSeconds:F2}s | <strong>Sample Rate:</strong> {result.SampleRate} Hz | <strong>Channels:</strong> {result.Channels}</p>");

        sb.AppendLine("<h2>Loudness</h2>");
        sb.AppendLine("<table>");
        sb.AppendLine($"<tr><th>Measurement</th><th>Value</th></tr>");
        sb.AppendLine($"<tr><td>Integrated Loudness</td><td class='metric'>{result.IntegratedLufs:F1} LUFS</td></tr>");
        sb.AppendLine($"<tr><td>Short-term Loudness</td><td>{result.ShortTermLufs:F1} LUFS</td></tr>");
        sb.AppendLine($"<tr><td>Loudness Range (LRA)</td><td>{result.LoudnessRangeLu:F1} LU</td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine("<h2>Levels</h2>");
        sb.AppendLine("<table>");
        string truePeakClass = result.TruePeakDbtp > -1 ? "warning" : "good";
        sb.AppendLine($"<tr><td>True Peak</td><td class='{truePeakClass}'>{result.TruePeakDbtp:F1} dBTP</td></tr>");
        sb.AppendLine($"<tr><td>Peak Level</td><td>{result.PeakDb:F1} dBFS</td></tr>");
        sb.AppendLine($"<tr><td>RMS Level</td><td>{result.RmsDb:F1} dBFS</td></tr>");
        sb.AppendLine($"<tr><td>Crest Factor</td><td>{result.CrestFactorDb:F1} dB</td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine("<h2>Dynamic Range</h2>");
        sb.AppendLine("<table>");
        string drClass = result.DynamicRangeDr < 6 ? "warning" : "good";
        sb.AppendLine($"<tr><td>DR Score</td><td class='metric {drClass}'>DR{result.DynamicRangeDr}</td></tr>");
        sb.AppendLine($"<tr><td>Compression</td><td>{result.CompressionDetected}</td></tr>");
        sb.AppendLine($"<tr><td>Limiting</td><td>{result.LimitingDetected}</td></tr>");
        sb.AppendLine("</table>");

        if (result.ClippingResult != null && result.ClippingResult.ClippingEventCount > 0)
        {
            sb.AppendLine("<h2>Clipping</h2>");
            sb.AppendLine("<table>");
            string clipClass = result.ClippingResult.HasProblematicClipping ? "error" : "warning";
            sb.AppendLine($"<tr><td>Severity</td><td class='{clipClass}'>{result.ClippingResult.Severity}</td></tr>");
            sb.AppendLine($"<tr><td>Clipping Events</td><td>{result.ClippingResult.ClippingEventCount}</td></tr>");
            sb.AppendLine($"<tr><td>Inter-sample Peaks</td><td>{result.ClippingResult.InterSamplePeakCount}</td></tr>");
            sb.AppendLine("</table>");
        }

        if (result.Recommendations != null && result.Recommendations.TextRecommendations.Length > 0)
        {
            sb.AppendLine("<h2>Recommendations</h2>");
            sb.AppendLine("<ul>");
            foreach (var rec in result.Recommendations.TextRecommendations)
            {
                sb.AppendLine($"<li>{rec}</li>");
            }
            sb.AppendLine("</ul>");
        }

        sb.AppendLine("<hr>");
        sb.AppendLine($"<p><em>Generated by MusicEngine DynamicsAnalyzer on {result.Timestamp:yyyy-MM-dd HH:mm:ss}</em></p>");
        sb.AppendLine("</body></html>");

        return sb.ToString();
    }

    #endregion

    #region Static Helpers

    private static float[] GenerateHannWindow(int length)
    {
        float[] window = new float[length];
        for (int i = 0; i < length; i++)
        {
            window[i] = (float)(0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (length - 1))));
        }
        return window;
    }

    private static float[] GenerateOversamplingCoeffs()
    {
        const int taps = 48;
        const int oversampleFactor = 4;
        float[] coeffs = new float[taps];

        for (int i = 0; i < taps; i++)
        {
            double n = i - (taps - 1) / 2.0;
            double sincArg = n / oversampleFactor;
            double sinc = Math.Abs(sincArg) < 1e-10 ? 1.0 : Math.Sin(Math.PI * sincArg) / (Math.PI * sincArg);
            double window = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (taps - 1)));
            coeffs[i] = (float)(sinc * window);
        }

        // Normalize
        float sum = 0;
        for (int phase = 0; phase < oversampleFactor; phase++)
        {
            float phaseSum = 0;
            for (int i = phase; i < taps; i += oversampleFactor)
                phaseSum += coeffs[i];
            if (phaseSum > sum) sum = phaseSum;
        }

        for (int i = 0; i < taps; i++)
            coeffs[i] /= sum;

        return coeffs;
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
        double[] b = new double[3];
        double[] a = new double[3];

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
        double[] b = new double[3];
        double[] a = new double[3];

        b[0] = 1.0 / a0;
        b[1] = -2.0 / a0;
        b[2] = 1.0 / a0;
        a[0] = 1.0;
        a[1] = 2.0 * (K * K - 1.0) / a0;
        a[2] = (1.0 - K / Q + K * K) / a0;

        return (b, a);
    }

    private static bool IsPowerOfTwo(int n) => n > 0 && (n & (n - 1)) == 0;

    #endregion
}
