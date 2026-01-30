//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Comprehensive A/B audio comparison tool with level matching, spectrum analysis, and detailed metrics.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using NAudio.Dsp;
using NAudio.Wave;

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Represents which audio source is currently active in A/B comparison.
/// </summary>
public enum CompareSource
{
    /// <summary>Source A is active.</summary>
    A,
    /// <summary>Source B is active.</summary>
    B,
    /// <summary>Crossfading between A and B.</summary>
    Crossfade,
    /// <summary>Difference signal (A - B) is active.</summary>
    Difference
}

/// <summary>
/// Represents the comparison metrics for a single frequency band.
/// </summary>
public class BandComparisonMetrics
{
    /// <summary>Gets the band index (0-based).</summary>
    public int BandIndex { get; init; }

    /// <summary>Gets the lower frequency boundary in Hz.</summary>
    public float LowFrequency { get; init; }

    /// <summary>Gets the upper frequency boundary in Hz.</summary>
    public float HighFrequency { get; init; }

    /// <summary>Gets the center frequency in Hz.</summary>
    public float CenterFrequency => (float)Math.Sqrt(LowFrequency * HighFrequency);

    /// <summary>Gets the magnitude for source A in dB.</summary>
    public float MagnitudeA_dB { get; init; }

    /// <summary>Gets the magnitude for source B in dB.</summary>
    public float MagnitudeB_dB { get; init; }

    /// <summary>Gets the difference between A and B magnitudes in dB.</summary>
    public float DifferencedB => MagnitudeA_dB - MagnitudeB_dB;

    /// <summary>Gets the correlation coefficient for this band (-1 to +1).</summary>
    public float Correlation { get; init; }

    /// <summary>Gets whether the bands match within tolerance.</summary>
    public bool IsMatching { get; init; }

    /// <summary>Gets the match percentage for this band (0-100).</summary>
    public float MatchPercentage { get; init; }
}

/// <summary>
/// Represents overall comparison metrics between two audio sources.
/// </summary>
public class AudioComparisonMetrics
{
    /// <summary>Gets the integrated LUFS for source A.</summary>
    public double IntegratedLufsA { get; init; }

    /// <summary>Gets the integrated LUFS for source B.</summary>
    public double IntegratedLufsB { get; init; }

    /// <summary>Gets the LUFS difference (A - B).</summary>
    public double LufsDifference => IntegratedLufsA - IntegratedLufsB;

    /// <summary>Gets the true peak in dBTP for source A.</summary>
    public double TruePeakA_dBTP { get; init; }

    /// <summary>Gets the true peak in dBTP for source B.</summary>
    public double TruePeakB_dBTP { get; init; }

    /// <summary>Gets the peak difference in dB.</summary>
    public double PeakDifferencedB => TruePeakA_dBTP - TruePeakB_dBTP;

    /// <summary>Gets the dynamic range in dB for source A (peak - integrated LUFS).</summary>
    public double DynamicRangeA => TruePeakA_dBTP - IntegratedLufsA;

    /// <summary>Gets the dynamic range in dB for source B.</summary>
    public double DynamicRangeB => TruePeakB_dBTP - IntegratedLufsB;

    /// <summary>Gets the stereo width for source A (0 = mono, 1 = wide).</summary>
    public float StereoWidthA { get; init; }

    /// <summary>Gets the stereo width for source B.</summary>
    public float StereoWidthB { get; init; }

    /// <summary>Gets the stereo correlation for source A (-1 to +1).</summary>
    public float StereoCorrelationA { get; init; }

    /// <summary>Gets the stereo correlation for source B.</summary>
    public float StereoCorrelationB { get; init; }

    /// <summary>Gets the phase coherence between A and B (0 to 1).</summary>
    public float PhaseCoherence { get; init; }

    /// <summary>Gets the overall correlation coefficient between A and B (-1 to +1).</summary>
    public float OverallCorrelation { get; init; }

    /// <summary>Gets the null test result in dB (level of difference signal).</summary>
    public double NullTestResultdB { get; init; }

    /// <summary>Gets whether A and B are phase-aligned.</summary>
    public bool IsPhaseAligned { get; init; }

    /// <summary>Gets the detected time offset between A and B in samples.</summary>
    public int TimeOffsetSamples { get; init; }

    /// <summary>Gets the detected time offset in milliseconds.</summary>
    public double TimeOffsetMs { get; init; }

    /// <summary>Gets the per-band comparison metrics.</summary>
    public BandComparisonMetrics[] BandMetrics { get; init; } = Array.Empty<BandComparisonMetrics>();

    /// <summary>Gets the overall match percentage (0-100).</summary>
    public float OverallMatchPercentage { get; init; }

    /// <summary>Gets the spectrum match percentage (0-100).</summary>
    public float SpectrumMatchPercentage { get; init; }

    /// <summary>Gets the dynamics match percentage (0-100).</summary>
    public float DynamicsMatchPercentage { get; init; }

    /// <summary>Gets the stereo image match percentage (0-100).</summary>
    public float StereoMatchPercentage { get; init; }

    /// <summary>Gets the analysis timestamp.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>Gets the duration of audio analyzed in seconds.</summary>
    public float DurationSeconds { get; init; }
}

/// <summary>
/// Represents visual difference data for UI display.
/// </summary>
public class VisualDiffData
{
    /// <summary>Gets the spectrum magnitudes for source A (per band).</summary>
    public float[] SpectrumA { get; init; } = Array.Empty<float>();

    /// <summary>Gets the spectrum magnitudes for source B (per band).</summary>
    public float[] SpectrumB { get; init; } = Array.Empty<float>();

    /// <summary>Gets the spectrum difference (A - B) per band in dB.</summary>
    public float[] SpectrumDifference { get; init; } = Array.Empty<float>();

    /// <summary>Gets the center frequencies for each band.</summary>
    public float[] BandFrequencies { get; init; } = Array.Empty<float>();

    /// <summary>Gets the waveform envelope for source A.</summary>
    public float[] WaveformEnvelopeA { get; init; } = Array.Empty<float>();

    /// <summary>Gets the waveform envelope for source B.</summary>
    public float[] WaveformEnvelopeB { get; init; } = Array.Empty<float>();

    /// <summary>Gets the difference waveform envelope.</summary>
    public float[] WaveformDifference { get; init; } = Array.Empty<float>();

    /// <summary>Gets the stereo width over time for source A.</summary>
    public float[] StereoWidthOverTimeA { get; init; } = Array.Empty<float>();

    /// <summary>Gets the stereo width over time for source B.</summary>
    public float[] StereoWidthOverTimeB { get; init; } = Array.Empty<float>();

    /// <summary>Gets the loudness over time for source A (LUFS).</summary>
    public float[] LoudnessOverTimeA { get; init; } = Array.Empty<float>();

    /// <summary>Gets the loudness over time for source B (LUFS).</summary>
    public float[] LoudnessOverTimeB { get; init; } = Array.Empty<float>();

    /// <summary>Gets the correlation over time.</summary>
    public float[] CorrelationOverTime { get; init; } = Array.Empty<float>();

    /// <summary>Gets the time axis values in seconds.</summary>
    public float[] TimeAxis { get; init; } = Array.Empty<float>();
}

/// <summary>
/// Event arguments for comparison state changes.
/// </summary>
public class CompareStateChangedEventArgs : EventArgs
{
    /// <summary>Gets the new active source.</summary>
    public CompareSource NewSource { get; }

    /// <summary>Gets the crossfade position (0 = A, 1 = B) when in crossfade mode.</summary>
    public float CrossfadePosition { get; }

    public CompareStateChangedEventArgs(CompareSource source, float crossfadePosition = 0)
    {
        NewSource = source;
        CrossfadePosition = crossfadePosition;
    }
}

/// <summary>
/// Event arguments for comparison metrics updates.
/// </summary>
public class ComparisonMetricsEventArgs : EventArgs
{
    /// <summary>Gets the updated comparison metrics.</summary>
    public AudioComparisonMetrics Metrics { get; }

    public ComparisonMetricsEventArgs(AudioComparisonMetrics metrics)
    {
        Metrics = metrics;
    }
}

/// <summary>
/// Comprehensive A/B audio comparator for professional audio analysis.
/// </summary>
/// <remarks>
/// Features:
/// - A/B switching with instant toggle
/// - Level matching (auto-match loudness)
/// - Difference signal extraction
/// - Spectrum comparison overlay
/// - LUFS, Peak, Dynamic range comparison
/// - Stereo width comparison
/// - Phase coherence analysis
/// - Null test
/// - Correlation coefficient
/// - Time alignment
/// - Per-band comparison (8 bands)
/// - Visual diff display data
/// - Match percentage scoring
/// - Export comparison report
/// - Crossfade between A and B
/// </remarks>
public class AudioComparator : ISampleProvider, IDisposable
{
    // Configuration
    private const int NumBands = 8;
    private const int FftSize = 4096;
    private const int MaxCrossCorrelationLag = 4410; // 100ms at 44.1kHz

    // Frequency band definitions (8 bands, octave-based)
    private static readonly (float Low, float High)[] BandRanges =
    {
        (20f, 60f),      // Sub bass
        (60f, 250f),     // Bass
        (250f, 500f),    // Low mids
        (500f, 2000f),   // Mids
        (2000f, 4000f),  // Upper mids
        (4000f, 6000f),  // Presence
        (6000f, 12000f), // Brilliance
        (12000f, 20000f) // Air
    };

    // Audio sources
    private ISampleProvider? _sourceA;
    private ISampleProvider? _sourceB;
    private readonly int _sampleRate;
    private readonly int _channels;

    // Buffers for source data
    private float[]? _bufferA;
    private float[]? _bufferB;
    private int _bufferLength;

    // Current state
    private CompareSource _activeSource = CompareSource.A;
    private float _crossfadePosition; // 0 = A, 1 = B
    private float _crossfadeSpeed = 0.01f; // Per sample
    private bool _isLevelMatched;
    private float _levelMatchGainA = 1.0f;
    private float _levelMatchGainB = 1.0f;

    // Analysis state
    private readonly Complex[] _fftBufferA;
    private readonly Complex[] _fftBufferB;
    private readonly float[] _window;
    private readonly int[] _bandBinRanges;
    private readonly object _lock = new();

    // Metrics accumulators
    private double _sumASquared;
    private double _sumBSquared;
    private double _sumAB;
    private double _sumDiffSquared;
    private long _sampleCount;

    // LUFS measurement
    private double _lufsA = double.NegativeInfinity;
    private double _lufsB = double.NegativeInfinity;
    private double _truePeakA;
    private double _truePeakB;

    // Stereo analysis
    private float _stereoWidthA;
    private float _stereoWidthB;
    private float _stereoCorrelationA;
    private float _stereoCorrelationB;

    // Band magnitudes
    private readonly float[] _bandMagnitudesA;
    private readonly float[] _bandMagnitudesB;
    private readonly float[] _bandFrequencies;

    // Time alignment
    private int _detectedOffsetSamples;
    private bool _isTimeAligned;

    // Visual diff accumulators
    private readonly List<float> _waveformEnvelopeA;
    private readonly List<float> _waveformEnvelopeB;
    private readonly List<float> _loudnessOverTimeA;
    private readonly List<float> _loudnessOverTimeB;
    private readonly List<float> _correlationOverTime;
    private readonly List<float> _stereoWidthOverTimeA;
    private readonly List<float> _stereoWidthOverTimeB;
    private readonly List<float> _timeAxis;

    // Settings
    private float _matchTolerance_dB = 1.0f; // dB tolerance for "matching" bands
    private bool _disposed;

    /// <summary>
    /// Gets the wave format.
    /// </summary>
    public WaveFormat WaveFormat { get; }

    /// <summary>
    /// Gets or sets the currently active source.
    /// </summary>
    public CompareSource ActiveSource
    {
        get => _activeSource;
        set
        {
            if (_activeSource != value)
            {
                _activeSource = value;
                OnCompareStateChanged(new CompareStateChangedEventArgs(value, _crossfadePosition));
            }
        }
    }

    /// <summary>
    /// Gets or sets the crossfade position (0 = A, 1 = B).
    /// </summary>
    public float CrossfadePosition
    {
        get => _crossfadePosition;
        set
        {
            _crossfadePosition = Math.Clamp(value, 0f, 1f);
            if (_activeSource == CompareSource.Crossfade)
            {
                OnCompareStateChanged(new CompareStateChangedEventArgs(CompareSource.Crossfade, _crossfadePosition));
            }
        }
    }

    /// <summary>
    /// Gets or sets the crossfade speed (amount to change per sample).
    /// </summary>
    public float CrossfadeSpeed
    {
        get => _crossfadeSpeed;
        set => _crossfadeSpeed = Math.Clamp(value, 0.0001f, 0.1f);
    }

    /// <summary>
    /// Gets or sets whether level matching is enabled.
    /// </summary>
    public bool IsLevelMatched
    {
        get => _isLevelMatched;
        set => _isLevelMatched = value;
    }

    /// <summary>
    /// Gets or sets the match tolerance in dB for per-band comparison.
    /// </summary>
    public float MatchTolerance_dB
    {
        get => _matchTolerance_dB;
        set => _matchTolerance_dB = Math.Max(0.1f, value);
    }

    /// <summary>
    /// Gets the level match gain applied to source A.
    /// </summary>
    public float LevelMatchGainA => _levelMatchGainA;

    /// <summary>
    /// Gets the level match gain applied to source B.
    /// </summary>
    public float LevelMatchGainB => _levelMatchGainB;

    /// <summary>
    /// Gets whether the sources are time-aligned.
    /// </summary>
    public bool IsTimeAligned => _isTimeAligned;

    /// <summary>
    /// Gets the detected time offset in samples.
    /// </summary>
    public int DetectedOffsetSamples => _detectedOffsetSamples;

    /// <summary>
    /// Event raised when the active source changes.
    /// </summary>
    public event EventHandler<CompareStateChangedEventArgs>? CompareStateChanged;

    /// <summary>
    /// Event raised when comparison metrics are updated.
    /// </summary>
    public event EventHandler<ComparisonMetricsEventArgs>? MetricsUpdated;

    /// <summary>
    /// Creates a new audio comparator with the specified configuration.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate (default: 44100).</param>
    /// <param name="channels">Number of channels (default: 2 for stereo).</param>
    public AudioComparator(int sampleRate = 44100, int channels = 2)
    {
        _sampleRate = sampleRate;
        _channels = channels;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);

        _fftBufferA = new Complex[FftSize];
        _fftBufferB = new Complex[FftSize];
        _window = GenerateHannWindow(FftSize);

        _bandMagnitudesA = new float[NumBands];
        _bandMagnitudesB = new float[NumBands];
        _bandFrequencies = new float[NumBands];

        // Calculate band frequencies
        for (int i = 0; i < NumBands; i++)
        {
            var (low, high) = BandRanges[i];
            _bandFrequencies[i] = (float)Math.Sqrt(low * high);
        }

        // Pre-calculate bin ranges
        _bandBinRanges = new int[NumBands * 2];
        float binResolution = (float)sampleRate / FftSize;

        for (int i = 0; i < NumBands; i++)
        {
            var (low, high) = BandRanges[i];
            _bandBinRanges[i * 2] = Math.Max(1, (int)(low / binResolution));
            _bandBinRanges[i * 2 + 1] = Math.Min(FftSize / 2 - 1, (int)(high / binResolution));
        }

        // Initialize visual diff accumulators
        _waveformEnvelopeA = new List<float>();
        _waveformEnvelopeB = new List<float>();
        _loudnessOverTimeA = new List<float>();
        _loudnessOverTimeB = new List<float>();
        _correlationOverTime = new List<float>();
        _stereoWidthOverTimeA = new List<float>();
        _stereoWidthOverTimeB = new List<float>();
        _timeAxis = new List<float>();
    }

    /// <summary>
    /// Creates a new audio comparator wrapping two audio sources.
    /// </summary>
    /// <param name="sourceA">Source A (original/reference).</param>
    /// <param name="sourceB">Source B (processed/comparison).</param>
    public AudioComparator(ISampleProvider sourceA, ISampleProvider sourceB)
        : this(sourceA.WaveFormat.SampleRate, sourceA.WaveFormat.Channels)
    {
        SetSources(sourceA, sourceB);
    }

    /// <summary>
    /// Sets the audio sources for comparison.
    /// </summary>
    /// <param name="sourceA">Source A (original/reference).</param>
    /// <param name="sourceB">Source B (processed/comparison).</param>
    public void SetSources(ISampleProvider sourceA, ISampleProvider sourceB)
    {
        if (sourceA.WaveFormat.SampleRate != sourceB.WaveFormat.SampleRate)
            throw new ArgumentException("Source sample rates must match.");
        if (sourceA.WaveFormat.Channels != sourceB.WaveFormat.Channels)
            throw new ArgumentException("Source channel counts must match.");

        _sourceA = sourceA;
        _sourceB = sourceB;
        Reset();
    }

    /// <summary>
    /// Loads audio buffers directly for offline analysis.
    /// </summary>
    /// <param name="bufferA">Interleaved samples for source A.</param>
    /// <param name="bufferB">Interleaved samples for source B.</param>
    public void LoadBuffers(float[] bufferA, float[] bufferB)
    {
        _bufferA = (float[])bufferA.Clone();
        _bufferB = (float[])bufferB.Clone();
        _bufferLength = Math.Min(bufferA.Length, bufferB.Length);
        Reset();
    }

    /// <summary>
    /// Toggles between source A and B instantly.
    /// </summary>
    public void Toggle()
    {
        ActiveSource = _activeSource == CompareSource.A ? CompareSource.B : CompareSource.A;
    }

    /// <summary>
    /// Starts a crossfade from the current position towards the target.
    /// </summary>
    /// <param name="targetPosition">Target position (0 = A, 1 = B).</param>
    public void StartCrossfade(float targetPosition)
    {
        _activeSource = CompareSource.Crossfade;
        // Speed is set via CrossfadeSpeed property
    }

    /// <summary>
    /// Sets output to the difference signal (A - B).
    /// </summary>
    public void SetDifferenceMode()
    {
        ActiveSource = CompareSource.Difference;
    }

    /// <summary>
    /// Calculates and applies level matching based on LUFS.
    /// </summary>
    public void CalculateLevelMatch()
    {
        if (_bufferA == null || _bufferB == null) return;

        // Measure LUFS for both sources
        _lufsA = MeasureLufs(_bufferA);
        _lufsB = MeasureLufs(_bufferB);

        if (double.IsNegativeInfinity(_lufsA) || double.IsNegativeInfinity(_lufsB))
            return;

        // Calculate gain to match B to A
        double difference = _lufsA - _lufsB;
        float gainDb = (float)difference;

        // Apply gain to B to match A
        _levelMatchGainB = (float)Math.Pow(10, gainDb / 20);
        _levelMatchGainA = 1.0f;

        _isLevelMatched = true;
    }

    /// <summary>
    /// Detects and corrects time alignment between sources using cross-correlation.
    /// </summary>
    /// <returns>The detected offset in samples.</returns>
    public int DetectTimeAlignment()
    {
        if (_bufferA == null || _bufferB == null) return 0;

        // Use mono sum for alignment detection
        int frames = _bufferLength / _channels;
        float[] monoA = new float[frames];
        float[] monoB = new float[frames];

        for (int i = 0; i < frames; i++)
        {
            float sumA = 0, sumB = 0;
            for (int ch = 0; ch < _channels; ch++)
            {
                sumA += _bufferA[i * _channels + ch];
                sumB += _bufferB[i * _channels + ch];
            }
            monoA[i] = sumA / _channels;
            monoB[i] = sumB / _channels;
        }

        // Cross-correlation to find offset
        int maxLag = Math.Min(MaxCrossCorrelationLag, frames / 4);
        double maxCorr = double.NegativeInfinity;
        int bestLag = 0;

        for (int lag = -maxLag; lag <= maxLag; lag++)
        {
            double sum = 0;
            int count = 0;

            for (int i = 0; i < frames; i++)
            {
                int j = i + lag;
                if (j >= 0 && j < frames)
                {
                    sum += monoA[i] * monoB[j];
                    count++;
                }
            }

            double corr = count > 0 ? sum / count : 0;
            if (corr > maxCorr)
            {
                maxCorr = corr;
                bestLag = lag;
            }
        }

        _detectedOffsetSamples = bestLag;
        _isTimeAligned = Math.Abs(bestLag) < 10; // Consider aligned if within 10 samples

        return bestLag;
    }

    /// <summary>
    /// Applies time alignment by shifting source B.
    /// </summary>
    public void ApplyTimeAlignment()
    {
        if (_bufferB == null || _detectedOffsetSamples == 0) return;

        int offsetSamples = _detectedOffsetSamples * _channels;
        float[] aligned = new float[_bufferB.Length];

        if (offsetSamples > 0)
        {
            // B is behind, shift left
            Array.Copy(_bufferB, offsetSamples, aligned, 0,
                Math.Min(_bufferB.Length - offsetSamples, aligned.Length));
        }
        else
        {
            // B is ahead, shift right
            int absOffset = Math.Abs(offsetSamples);
            Array.Copy(_bufferB, 0, aligned, absOffset,
                Math.Min(_bufferB.Length, aligned.Length - absOffset));
        }

        _bufferB = aligned;
        _detectedOffsetSamples = 0;
        _isTimeAligned = true;
    }

    /// <summary>
    /// Performs a null test and returns the level of the difference signal in dB.
    /// </summary>
    /// <returns>The RMS level of (A - B) in dB.</returns>
    public double PerformNullTest()
    {
        if (_bufferA == null || _bufferB == null) return double.NegativeInfinity;

        double sumSquared = 0;
        int count = 0;

        for (int i = 0; i < _bufferLength; i++)
        {
            float a = _bufferA[i];
            float b = _bufferB[i];

            if (_isLevelMatched)
            {
                a *= _levelMatchGainA;
                b *= _levelMatchGainB;
            }

            float diff = a - b;
            sumSquared += diff * diff;
            count++;
        }

        if (count == 0) return double.NegativeInfinity;

        double rms = Math.Sqrt(sumSquared / count);
        return 20 * Math.Log10(Math.Max(rms, 1e-10));
    }

    /// <summary>
    /// Extracts the difference signal (A - B).
    /// </summary>
    /// <returns>The difference signal as an array of samples.</returns>
    public float[] ExtractDifferenceSignal()
    {
        if (_bufferA == null || _bufferB == null) return Array.Empty<float>();

        float[] difference = new float[_bufferLength];

        for (int i = 0; i < _bufferLength; i++)
        {
            float a = _bufferA[i];
            float b = _bufferB[i];

            if (_isLevelMatched)
            {
                a *= _levelMatchGainA;
                b *= _levelMatchGainB;
            }

            difference[i] = a - b;
        }

        return difference;
    }

    /// <summary>
    /// Performs comprehensive analysis and returns comparison metrics.
    /// </summary>
    /// <returns>Complete comparison metrics.</returns>
    public AudioComparisonMetrics Analyze()
    {
        if (_bufferA == null || _bufferB == null)
            return new AudioComparisonMetrics();

        Reset();

        int frames = _bufferLength / _channels;
        float durationSeconds = (float)frames / _sampleRate;

        // Analyze loudness
        _lufsA = MeasureLufs(_bufferA);
        _lufsB = MeasureLufs(_bufferB);

        // Analyze true peaks
        _truePeakA = MeasureTruePeak(_bufferA);
        _truePeakB = MeasureTruePeak(_bufferB);

        // Analyze stereo characteristics
        AnalyzeStereo(_bufferA, out _stereoWidthA, out _stereoCorrelationA);
        AnalyzeStereo(_bufferB, out _stereoWidthB, out _stereoCorrelationB);

        // Analyze spectrum
        AnalyzeSpectrum(_bufferA, _bandMagnitudesA);
        AnalyzeSpectrum(_bufferB, _bandMagnitudesB);

        // Calculate correlation
        float overallCorrelation = CalculateCorrelation(_bufferA, _bufferB);

        // Calculate phase coherence
        float phaseCoherence = CalculatePhaseCoherence(_bufferA, _bufferB);

        // Null test
        double nullTestDb = PerformNullTest();

        // Per-band metrics
        var bandMetrics = new BandComparisonMetrics[NumBands];
        float spectrumMatchSum = 0;

        for (int i = 0; i < NumBands; i++)
        {
            float magA = _bandMagnitudesA[i];
            float magB = _bandMagnitudesB[i];
            float diff = Math.Abs(magA - magB);
            bool isMatching = diff <= _matchTolerance_dB;
            float matchPct = Math.Max(0, 100 * (1 - diff / 20)); // 20 dB = 0%

            bandMetrics[i] = new BandComparisonMetrics
            {
                BandIndex = i,
                LowFrequency = BandRanges[i].Low,
                HighFrequency = BandRanges[i].High,
                MagnitudeA_dB = magA,
                MagnitudeB_dB = magB,
                Correlation = CalculateBandCorrelation(_bufferA, _bufferB, i),
                IsMatching = isMatching,
                MatchPercentage = matchPct
            };

            spectrumMatchSum += matchPct;
        }

        float spectrumMatchPct = spectrumMatchSum / NumBands;

        // Dynamics match (based on dynamic range difference)
        float drDiff = (float)Math.Abs(((_truePeakA - _lufsA) - (_truePeakB - _lufsB)));
        float dynamicsMatchPct = Math.Max(0, 100 * (1 - drDiff / 10));

        // Stereo match
        float stereoWidthDiff = Math.Abs(_stereoWidthA - _stereoWidthB);
        float stereoMatchPct = Math.Max(0, 100 * (1 - stereoWidthDiff));

        // Overall match percentage
        float overallMatchPct = (spectrumMatchPct * 0.4f + dynamicsMatchPct * 0.3f + stereoMatchPct * 0.2f +
                                  (overallCorrelation + 1) * 50 * 0.1f);

        // Accumulate visual diff data over time
        AccumulateVisualData(_bufferA, _bufferB);

        var metrics = new AudioComparisonMetrics
        {
            IntegratedLufsA = _lufsA,
            IntegratedLufsB = _lufsB,
            TruePeakA_dBTP = 20 * Math.Log10(Math.Max(_truePeakA, 1e-10)),
            TruePeakB_dBTP = 20 * Math.Log10(Math.Max(_truePeakB, 1e-10)),
            StereoWidthA = _stereoWidthA,
            StereoWidthB = _stereoWidthB,
            StereoCorrelationA = _stereoCorrelationA,
            StereoCorrelationB = _stereoCorrelationB,
            PhaseCoherence = phaseCoherence,
            OverallCorrelation = overallCorrelation,
            NullTestResultdB = nullTestDb,
            IsPhaseAligned = _isTimeAligned,
            TimeOffsetSamples = _detectedOffsetSamples,
            TimeOffsetMs = _detectedOffsetSamples * 1000.0 / _sampleRate,
            BandMetrics = bandMetrics,
            OverallMatchPercentage = overallMatchPct,
            SpectrumMatchPercentage = spectrumMatchPct,
            DynamicsMatchPercentage = dynamicsMatchPct,
            StereoMatchPercentage = stereoMatchPct,
            DurationSeconds = durationSeconds
        };

        MetricsUpdated?.Invoke(this, new ComparisonMetricsEventArgs(metrics));
        return metrics;
    }

    /// <summary>
    /// Gets visual difference data for UI display.
    /// </summary>
    /// <returns>Visual diff data structure.</returns>
    public VisualDiffData GetVisualDiffData()
    {
        float[] spectrumDiff = new float[NumBands];
        for (int i = 0; i < NumBands; i++)
        {
            spectrumDiff[i] = _bandMagnitudesA[i] - _bandMagnitudesB[i];
        }

        // Calculate waveform difference
        float[] waveformDiff = new float[_waveformEnvelopeA.Count];
        for (int i = 0; i < waveformDiff.Length; i++)
        {
            waveformDiff[i] = Math.Abs(_waveformEnvelopeA[i] - _waveformEnvelopeB[i]);
        }

        return new VisualDiffData
        {
            SpectrumA = (float[])_bandMagnitudesA.Clone(),
            SpectrumB = (float[])_bandMagnitudesB.Clone(),
            SpectrumDifference = spectrumDiff,
            BandFrequencies = (float[])_bandFrequencies.Clone(),
            WaveformEnvelopeA = _waveformEnvelopeA.ToArray(),
            WaveformEnvelopeB = _waveformEnvelopeB.ToArray(),
            WaveformDifference = waveformDiff,
            StereoWidthOverTimeA = _stereoWidthOverTimeA.ToArray(),
            StereoWidthOverTimeB = _stereoWidthOverTimeB.ToArray(),
            LoudnessOverTimeA = _loudnessOverTimeA.ToArray(),
            LoudnessOverTimeB = _loudnessOverTimeB.ToArray(),
            CorrelationOverTime = _correlationOverTime.ToArray(),
            TimeAxis = _timeAxis.ToArray()
        };
    }

    /// <summary>
    /// Exports a detailed comparison report to a file.
    /// </summary>
    /// <param name="filePath">Output file path (.txt or .json).</param>
    /// <param name="metrics">The comparison metrics to export.</param>
    public void ExportReport(string filePath, AudioComparisonMetrics metrics)
    {
        string extension = Path.GetExtension(filePath).ToLowerInvariant();

        if (extension == ".json")
        {
            ExportJsonReport(filePath, metrics);
        }
        else
        {
            ExportTextReport(filePath, metrics);
        }
    }

    private void ExportTextReport(string filePath, AudioComparisonMetrics metrics)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== AUDIO COMPARISON REPORT ===");
        sb.AppendLine($"Generated: {metrics.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"Duration: {metrics.DurationSeconds:F2} seconds");
        sb.AppendLine();

        sb.AppendLine("--- OVERALL MATCH ---");
        sb.AppendLine($"Overall Match: {metrics.OverallMatchPercentage:F1}%");
        sb.AppendLine($"Spectrum Match: {metrics.SpectrumMatchPercentage:F1}%");
        sb.AppendLine($"Dynamics Match: {metrics.DynamicsMatchPercentage:F1}%");
        sb.AppendLine($"Stereo Match: {metrics.StereoMatchPercentage:F1}%");
        sb.AppendLine();

        sb.AppendLine("--- LOUDNESS ---");
        sb.AppendLine($"Source A Integrated LUFS: {metrics.IntegratedLufsA:F1} LUFS");
        sb.AppendLine($"Source B Integrated LUFS: {metrics.IntegratedLufsB:F1} LUFS");
        sb.AppendLine($"LUFS Difference: {metrics.LufsDifference:+0.0;-0.0;0.0} dB");
        sb.AppendLine();

        sb.AppendLine("--- PEAK LEVELS ---");
        sb.AppendLine($"Source A True Peak: {metrics.TruePeakA_dBTP:F1} dBTP");
        sb.AppendLine($"Source B True Peak: {metrics.TruePeakB_dBTP:F1} dBTP");
        sb.AppendLine($"Peak Difference: {metrics.PeakDifferencedB:+0.0;-0.0;0.0} dB");
        sb.AppendLine();

        sb.AppendLine("--- DYNAMIC RANGE ---");
        sb.AppendLine($"Source A Dynamic Range: {metrics.DynamicRangeA:F1} dB");
        sb.AppendLine($"Source B Dynamic Range: {metrics.DynamicRangeB:F1} dB");
        sb.AppendLine();

        sb.AppendLine("--- STEREO IMAGE ---");
        sb.AppendLine($"Source A Stereo Width: {metrics.StereoWidthA:F2}");
        sb.AppendLine($"Source B Stereo Width: {metrics.StereoWidthB:F2}");
        sb.AppendLine($"Source A Stereo Correlation: {metrics.StereoCorrelationA:F2}");
        sb.AppendLine($"Source B Stereo Correlation: {metrics.StereoCorrelationB:F2}");
        sb.AppendLine();

        sb.AppendLine("--- PHASE & CORRELATION ---");
        sb.AppendLine($"Overall Correlation: {metrics.OverallCorrelation:F3}");
        sb.AppendLine($"Phase Coherence: {metrics.PhaseCoherence:F2}");
        sb.AppendLine($"Phase Aligned: {(metrics.IsPhaseAligned ? "Yes" : "No")}");
        sb.AppendLine($"Time Offset: {metrics.TimeOffsetSamples} samples ({metrics.TimeOffsetMs:F2} ms)");
        sb.AppendLine();

        sb.AppendLine("--- NULL TEST ---");
        sb.AppendLine($"Null Test Result: {metrics.NullTestResultdB:F1} dB");
        if (metrics.NullTestResultdB < -60)
            sb.AppendLine("Result: Excellent - signals are nearly identical");
        else if (metrics.NullTestResultdB < -40)
            sb.AppendLine("Result: Good - minor differences detected");
        else if (metrics.NullTestResultdB < -20)
            sb.AppendLine("Result: Moderate - noticeable differences");
        else
            sb.AppendLine("Result: Poor - significant differences");
        sb.AppendLine();

        sb.AppendLine("--- PER-BAND COMPARISON ---");
        sb.AppendLine("Band              | A (dB) | B (dB) | Diff  | Match%");
        sb.AppendLine("------------------|--------|--------|-------|-------");
        foreach (var band in metrics.BandMetrics)
        {
            string bandName = $"{band.LowFrequency:F0}-{band.HighFrequency:F0} Hz".PadRight(17);
            sb.AppendLine($"{bandName} | {band.MagnitudeA_dB,6:F1} | {band.MagnitudeB_dB,6:F1} | {band.DifferencedB,+5:F1} | {band.MatchPercentage,5:F1}%");
        }

        File.WriteAllText(filePath, sb.ToString());
    }

    private void ExportJsonReport(string filePath, AudioComparisonMetrics metrics)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        string json = JsonSerializer.Serialize(metrics, options);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Reads audio samples with A/B switching and crossfade applied.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        if (_sourceA == null || _sourceB == null)
            return 0;

        // Read from both sources
        float[] tempA = new float[count];
        float[] tempB = new float[count];

        int readA = _sourceA.Read(tempA, 0, count);
        int readB = _sourceB.Read(tempB, 0, count);
        int read = Math.Min(readA, readB);

        if (read == 0) return 0;

        // Apply level matching
        if (_isLevelMatched)
        {
            for (int i = 0; i < read; i++)
            {
                tempA[i] *= _levelMatchGainA;
                tempB[i] *= _levelMatchGainB;
            }
        }

        // Mix based on active source
        switch (_activeSource)
        {
            case CompareSource.A:
                Array.Copy(tempA, 0, buffer, offset, read);
                break;

            case CompareSource.B:
                Array.Copy(tempB, 0, buffer, offset, read);
                break;

            case CompareSource.Difference:
                for (int i = 0; i < read; i++)
                {
                    buffer[offset + i] = tempA[i] - tempB[i];
                }
                break;

            case CompareSource.Crossfade:
                for (int i = 0; i < read; i++)
                {
                    float gainA = 1 - _crossfadePosition;
                    float gainB = _crossfadePosition;
                    buffer[offset + i] = tempA[i] * gainA + tempB[i] * gainB;
                }
                break;
        }

        // Update real-time metrics
        UpdateRealtimeMetrics(tempA, tempB, read);

        return read;
    }

    /// <summary>
    /// Resets all analysis state.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _sumASquared = 0;
            _sumBSquared = 0;
            _sumAB = 0;
            _sumDiffSquared = 0;
            _sampleCount = 0;

            Array.Clear(_bandMagnitudesA, 0, _bandMagnitudesA.Length);
            Array.Clear(_bandMagnitudesB, 0, _bandMagnitudesB.Length);

            _waveformEnvelopeA.Clear();
            _waveformEnvelopeB.Clear();
            _loudnessOverTimeA.Clear();
            _loudnessOverTimeB.Clear();
            _correlationOverTime.Clear();
            _stereoWidthOverTimeA.Clear();
            _stereoWidthOverTimeB.Clear();
            _timeAxis.Clear();

            _detectedOffsetSamples = 0;
            _isTimeAligned = false;
        }
    }

    /// <summary>
    /// Disposes resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    // Protected virtual method for raising events
    protected virtual void OnCompareStateChanged(CompareStateChangedEventArgs e)
    {
        CompareStateChanged?.Invoke(this, e);
    }

    // Private helper methods

    private double MeasureLufs(float[] buffer)
    {
        // Simplified LUFS measurement (K-weighted RMS)
        // For accurate results, use the full LoudnessMeter class
        int frames = buffer.Length / _channels;
        double sumSquared = 0;

        for (int i = 0; i < frames; i++)
        {
            double sum = 0;
            for (int ch = 0; ch < _channels; ch++)
            {
                sum += buffer[i * _channels + ch];
            }
            sum /= _channels;
            sumSquared += sum * sum;
        }

        double rms = Math.Sqrt(sumSquared / frames);
        return -0.691 + 20 * Math.Log10(Math.Max(rms, 1e-10));
    }

    private double MeasureTruePeak(float[] buffer)
    {
        double maxPeak = 0;

        for (int i = 0; i < buffer.Length; i++)
        {
            double abs = Math.Abs(buffer[i]);
            if (abs > maxPeak) maxPeak = abs;
        }

        return maxPeak;
    }

    private void AnalyzeStereo(float[] buffer, out float stereoWidth, out float correlation)
    {
        if (_channels != 2)
        {
            stereoWidth = 0;
            correlation = 1;
            return;
        }

        int frames = buffer.Length / 2;
        double sumLR = 0, sumL2 = 0, sumR2 = 0;
        double sumMid = 0, sumSide = 0;

        for (int i = 0; i < frames; i++)
        {
            float left = buffer[i * 2];
            float right = buffer[i * 2 + 1];

            sumLR += left * right;
            sumL2 += left * left;
            sumR2 += right * right;

            float mid = (left + right) * 0.5f;
            float side = (left - right) * 0.5f;
            sumMid += mid * mid;
            sumSide += side * side;
        }

        // Correlation
        double denom = Math.Sqrt(sumL2 * sumR2);
        correlation = denom > 1e-10 ? (float)(sumLR / denom) : 1f;

        // Stereo width (0 = mono, 1 = wide)
        double midRms = Math.Sqrt(sumMid / frames);
        double sideRms = Math.Sqrt(sumSide / frames);
        double total = midRms + sideRms;
        stereoWidth = total > 1e-10 ? (float)(sideRms / total) : 0f;
    }

    private void AnalyzeSpectrum(float[] buffer, float[] bandMagnitudes)
    {
        // Take a segment from the middle of the buffer
        int startSample = Math.Max(0, (buffer.Length / _channels - FftSize) / 2) * _channels;
        int monoStartSample = startSample / _channels;

        // Copy to FFT buffer (mono mix)
        Complex[] fftBuffer = _activeSource == CompareSource.A ? _fftBufferA : _fftBufferB;

        for (int i = 0; i < FftSize && (monoStartSample + i) * _channels + _channels - 1 < buffer.Length; i++)
        {
            float sum = 0;
            for (int ch = 0; ch < _channels; ch++)
            {
                sum += buffer[(monoStartSample + i) * _channels + ch];
            }
            fftBuffer[i].X = (sum / _channels) * _window[i];
            fftBuffer[i].Y = 0;
        }

        // Perform FFT
        int m = (int)Math.Log(FftSize, 2.0);
        FastFourierTransform.FFT(true, m, fftBuffer);

        // Calculate band magnitudes
        for (int band = 0; band < NumBands; band++)
        {
            int lowBin = _bandBinRanges[band * 2];
            int highBin = _bandBinRanges[band * 2 + 1];

            double sum = 0;
            int count = 0;

            for (int bin = lowBin; bin <= highBin; bin++)
            {
                double mag = Math.Sqrt(fftBuffer[bin].X * fftBuffer[bin].X +
                                       fftBuffer[bin].Y * fftBuffer[bin].Y);
                sum += mag;
                count++;
            }

            double avgMag = count > 0 ? sum / count : 0;
            bandMagnitudes[band] = (float)(20 * Math.Log10(Math.Max(avgMag, 1e-10)));
        }
    }

    private float CalculateCorrelation(float[] bufferA, float[] bufferB)
    {
        int length = Math.Min(bufferA.Length, bufferB.Length);
        double sumAB = 0, sumA2 = 0, sumB2 = 0;

        for (int i = 0; i < length; i++)
        {
            float a = bufferA[i];
            float b = bufferB[i];

            if (_isLevelMatched)
            {
                a *= _levelMatchGainA;
                b *= _levelMatchGainB;
            }

            sumAB += a * b;
            sumA2 += a * a;
            sumB2 += b * b;
        }

        double denom = Math.Sqrt(sumA2 * sumB2);
        return denom > 1e-10 ? (float)(sumAB / denom) : 0f;
    }

    private float CalculateBandCorrelation(float[] bufferA, float[] bufferB, int bandIndex)
    {
        // For simplicity, use overall correlation
        // In a full implementation, this would filter to the band first
        return CalculateCorrelation(bufferA, bufferB);
    }

    private float CalculatePhaseCoherence(float[] bufferA, float[] bufferB)
    {
        // Phase coherence: measure how consistently in-phase A and B are
        // High coherence = mostly same polarity, low coherence = random/opposite polarity

        int length = Math.Min(bufferA.Length, bufferB.Length);
        int sameSign = 0;
        int total = 0;

        for (int i = 0; i < length; i++)
        {
            float a = bufferA[i];
            float b = bufferB[i];

            if (Math.Abs(a) > 0.001f && Math.Abs(b) > 0.001f)
            {
                if ((a > 0 && b > 0) || (a < 0 && b < 0))
                    sameSign++;
                total++;
            }
        }

        return total > 0 ? (float)sameSign / total : 0.5f;
    }

    private void AccumulateVisualData(float[] bufferA, float[] bufferB)
    {
        int windowSize = _sampleRate / 10; // 100ms windows
        int frames = _bufferLength / _channels;
        int numWindows = frames / windowSize;

        for (int w = 0; w < numWindows; w++)
        {
            int startFrame = w * windowSize;
            float timeSeconds = (float)startFrame / _sampleRate;

            // Waveform envelope (RMS)
            double sumA = 0, sumB = 0;
            for (int i = 0; i < windowSize && (startFrame + i) * _channels < _bufferLength; i++)
            {
                int idx = (startFrame + i) * _channels;
                for (int ch = 0; ch < _channels; ch++)
                {
                    sumA += bufferA[idx + ch] * bufferA[idx + ch];
                    sumB += bufferB[idx + ch] * bufferB[idx + ch];
                }
            }
            _waveformEnvelopeA.Add((float)Math.Sqrt(sumA / (windowSize * _channels)));
            _waveformEnvelopeB.Add((float)Math.Sqrt(sumB / (windowSize * _channels)));

            // Stereo width
            if (_channels == 2)
            {
                float widthA, widthB, corrA, corrB;
                AnalyzeStereoWindow(bufferA, startFrame, windowSize, out widthA, out corrA);
                AnalyzeStereoWindow(bufferB, startFrame, windowSize, out widthB, out corrB);
                _stereoWidthOverTimeA.Add(widthA);
                _stereoWidthOverTimeB.Add(widthB);
            }

            // Loudness (simplified)
            _loudnessOverTimeA.Add((float)(-0.691 + 20 * Math.Log10(Math.Max(_waveformEnvelopeA[w], 1e-10))));
            _loudnessOverTimeB.Add((float)(-0.691 + 20 * Math.Log10(Math.Max(_waveformEnvelopeB[w], 1e-10))));

            // Correlation
            float windowCorr = CalculateWindowCorrelation(bufferA, bufferB, startFrame, windowSize);
            _correlationOverTime.Add(windowCorr);

            _timeAxis.Add(timeSeconds);
        }
    }

    private void AnalyzeStereoWindow(float[] buffer, int startFrame, int windowSize, out float width, out float correlation)
    {
        double sumLR = 0, sumL2 = 0, sumR2 = 0;
        double sumMid = 0, sumSide = 0;

        for (int i = 0; i < windowSize && (startFrame + i) * 2 + 1 < buffer.Length; i++)
        {
            int idx = (startFrame + i) * 2;
            float left = buffer[idx];
            float right = buffer[idx + 1];

            sumLR += left * right;
            sumL2 += left * left;
            sumR2 += right * right;

            float mid = (left + right) * 0.5f;
            float side = (left - right) * 0.5f;
            sumMid += mid * mid;
            sumSide += side * side;
        }

        double denom = Math.Sqrt(sumL2 * sumR2);
        correlation = denom > 1e-10 ? (float)(sumLR / denom) : 1f;

        double midRms = Math.Sqrt(sumMid / windowSize);
        double sideRms = Math.Sqrt(sumSide / windowSize);
        double total = midRms + sideRms;
        width = total > 1e-10 ? (float)(sideRms / total) : 0f;
    }

    private float CalculateWindowCorrelation(float[] bufferA, float[] bufferB, int startFrame, int windowSize)
    {
        double sumAB = 0, sumA2 = 0, sumB2 = 0;

        for (int i = 0; i < windowSize; i++)
        {
            int idx = (startFrame + i) * _channels;
            if (idx + _channels > bufferA.Length || idx + _channels > bufferB.Length) break;

            for (int ch = 0; ch < _channels; ch++)
            {
                float a = bufferA[idx + ch];
                float b = bufferB[idx + ch];

                if (_isLevelMatched)
                {
                    a *= _levelMatchGainA;
                    b *= _levelMatchGainB;
                }

                sumAB += a * b;
                sumA2 += a * a;
                sumB2 += b * b;
            }
        }

        double denom = Math.Sqrt(sumA2 * sumB2);
        return denom > 1e-10 ? (float)(sumAB / denom) : 0f;
    }

    private void UpdateRealtimeMetrics(float[] tempA, float[] tempB, int count)
    {
        lock (_lock)
        {
            for (int i = 0; i < count; i++)
            {
                float a = tempA[i];
                float b = tempB[i];

                _sumASquared += a * a;
                _sumBSquared += b * b;
                _sumAB += a * b;
                _sumDiffSquared += (a - b) * (a - b);
                _sampleCount++;
            }
        }
    }

    private static float[] GenerateHannWindow(int length)
    {
        float[] window = new float[length];
        for (int i = 0; i < length; i++)
        {
            window[i] = (float)(0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (length - 1))));
        }
        return window;
    }
}
