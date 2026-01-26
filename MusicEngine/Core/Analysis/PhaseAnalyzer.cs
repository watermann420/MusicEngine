// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio analysis component.

using System;
using System.Collections.Generic;
using NAudio.Dsp;
using NAudio.Wave;

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Represents phase correlation for a specific frequency band.
/// </summary>
public class BandPhaseCorrelation
{
    /// <summary>Gets the band index (0-based).</summary>
    public int BandIndex { get; init; }

    /// <summary>Gets the lower frequency boundary in Hz.</summary>
    public float LowFrequency { get; init; }

    /// <summary>Gets the upper frequency boundary in Hz.</summary>
    public float HighFrequency { get; init; }

    /// <summary>Gets the center frequency in Hz.</summary>
    public float CenterFrequency => (float)Math.Sqrt(LowFrequency * HighFrequency);

    /// <summary>
    /// Gets the phase correlation value (-1.0 to +1.0).
    /// +1.0 = fully correlated (mono compatible)
    /// 0.0 = uncorrelated (stereo)
    /// -1.0 = fully anti-correlated (phase cancellation in mono)
    /// </summary>
    public float Correlation { get; init; }

    /// <summary>Gets whether this band has phase issues (correlation below threshold).</summary>
    public bool HasPhaseIssue { get; init; }

    /// <summary>Gets the severity of the phase issue (0 = none, 1 = severe).</summary>
    public float IssueSeverity { get; init; }

    /// <summary>Gets the average phase difference in degrees for this band.</summary>
    public float AveragePhaseDifferenceDegrees { get; init; }

    /// <summary>Gets the phase difference standard deviation in degrees.</summary>
    public float PhaseDifferenceStdDevDegrees { get; init; }

    /// <summary>Gets the energy level in this band (dB).</summary>
    public float EnergyDb { get; init; }
}

/// <summary>
/// Represents the phase difference histogram data.
/// </summary>
public class PhaseDifferenceHistogram
{
    /// <summary>Gets the histogram bin counts (-180 to +180 degrees).</summary>
    public int[] BinCounts { get; init; } = Array.Empty<int>();

    /// <summary>Gets the bin width in degrees.</summary>
    public float BinWidthDegrees { get; init; }

    /// <summary>Gets the total number of samples in the histogram.</summary>
    public int TotalSamples { get; init; }

    /// <summary>Gets the peak bin index (most common phase difference).</summary>
    public int PeakBinIndex { get; init; }

    /// <summary>Gets the phase difference at the peak bin in degrees.</summary>
    public float PeakPhaseDegrees { get; init; }

    /// <summary>Gets the percentage of samples at the peak bin.</summary>
    public float PeakPercentage { get; init; }

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
/// Complete phase analysis result.
/// </summary>
public class PhaseAnalysisResult
{
    /// <summary>Gets the overall stereo correlation (-1.0 to +1.0).</summary>
    public float OverallCorrelation { get; init; }

    /// <summary>Gets the mono compatibility score (0-100, where 100 is fully mono compatible).</summary>
    public float MonoCompatibilityScore { get; init; }

    /// <summary>Gets the phase correlation for each frequency band.</summary>
    public BandPhaseCorrelation[] BandCorrelations { get; init; } = Array.Empty<BandPhaseCorrelation>();

    /// <summary>Gets the list of problem frequency ranges (where phase issues exist).</summary>
    public (float LowHz, float HighHz, float Severity)[] ProblemFrequencies { get; init; }
        = Array.Empty<(float, float, float)>();

    /// <summary>Gets the phase difference histogram.</summary>
    public PhaseDifferenceHistogram? Histogram { get; init; }

    /// <summary>Gets the average phase difference in degrees across all frequencies.</summary>
    public float AveragePhaseDifferenceDegrees { get; init; }

    /// <summary>Gets whether the audio is likely inverted polarity on one channel.</summary>
    public bool LikelyPolarityInverted { get; init; }

    /// <summary>Gets whether significant phase cancellation would occur in mono.</summary>
    public bool SignificantMonoCancellation { get; init; }

    /// <summary>Gets the estimated mono level reduction in dB due to phase cancellation.</summary>
    public float EstimatedMonoLossDb { get; init; }

    /// <summary>Gets the stereo width indicator (0 = mono, 1 = wide stereo).</summary>
    public float StereoWidth { get; init; }

    /// <summary>Gets suggestions for addressing phase issues.</summary>
    public string[] Suggestions { get; init; } = Array.Empty<string>();

    /// <summary>Gets the duration of audio analyzed in seconds.</summary>
    public float DurationSeconds { get; init; }

    /// <summary>Gets the analysis timestamp.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Event arguments for real-time phase analysis updates.
/// </summary>
public class PhaseAnalysisEventArgs : EventArgs
{
    /// <summary>Gets the current analysis result.</summary>
    public PhaseAnalysisResult Result { get; }

    /// <summary>
    /// Creates new phase analysis event arguments.
    /// </summary>
    public PhaseAnalysisEventArgs(PhaseAnalysisResult result)
    {
        Result = result;
    }
}

/// <summary>
/// Detailed stereo phase analyzer for measuring phase correlation per frequency band
/// and assessing mono compatibility of stereo audio.
/// </summary>
/// <remarks>
/// The phase analyzer provides:
/// - Overall stereo correlation measurement
/// - Per-frequency-band phase correlation
/// - Phase difference histogram
/// - Mono compatibility scoring
/// - Problem frequency identification
/// - Real-time streaming analysis capability
///
/// Use cases include:
/// - Checking mixes for mono compatibility (radio, phone, club systems)
/// - Identifying phase-problematic frequencies
/// - Detecting polarity inversion issues
/// - Analyzing stereo width and imaging
/// </remarks>
public class PhaseAnalyzer : ISampleProvider
{
    // Frequency bands for analysis (logarithmically spaced)
    private const int NumBands = 12;

    private static readonly (float Low, float High)[] BandRanges =
    {
        (20f, 40f),
        (40f, 80f),
        (80f, 160f),
        (160f, 315f),
        (315f, 630f),
        (630f, 1250f),
        (1250f, 2500f),
        (2500f, 5000f),
        (5000f, 10000f),
        (10000f, 14000f),
        (14000f, 18000f),
        (18000f, 20000f)
    };

    private readonly ISampleProvider? _source;
    private readonly int _sampleRate;
    private readonly int _fftSize;
    private readonly Complex[] _leftFftBuffer;
    private readonly Complex[] _rightFftBuffer;
    private readonly float[] _leftSampleBuffer;
    private readonly float[] _rightSampleBuffer;
    private readonly float[] _window;
    private readonly int[] _bandBinRanges;
    private readonly object _lock = new();

    // Histogram
    private const int HistogramBins = 72; // 5 degrees per bin
    private readonly int[] _phaseHistogram;
    private int _histogramSampleCount;

    // Real-time analysis state
    private int _sampleCount;
    private int _frameCount;
    private long _totalSamplesProcessed;

    // Per-band accumulators
    private readonly double[] _bandCorrelationSum;
    private readonly double[] _bandPhaseDiffSum;
    private readonly double[] _bandPhaseDiffSqSum;
    private readonly double[] _bandEnergySum;
    private readonly int[] _bandSampleCounts;

    // Overall accumulators
    private double _overallCorrelationSum;
    private int _overallSampleCount;
    private double _sumLeftRight;
    private double _sumLeftSq;
    private double _sumRightSq;

    /// <summary>
    /// Gets the wave format (only available when wrapping a source).
    /// </summary>
    public WaveFormat WaveFormat => _source?.WaveFormat ?? WaveFormat.CreateIeeeFloatWaveFormat(_sampleRate, 2);

    /// <summary>
    /// Gets the sample rate used for analysis.
    /// </summary>
    public int SampleRate => _sampleRate;

    /// <summary>
    /// Gets the FFT size used for analysis.
    /// </summary>
    public int FftSize => _fftSize;

    /// <summary>
    /// Gets the number of frequency bands in the analysis.
    /// </summary>
    public int NumFrequencyBands => NumBands;

    /// <summary>
    /// Gets or sets the correlation threshold below which a band is flagged as having issues.
    /// Default is 0.0 (only negative correlation is flagged).
    /// </summary>
    public float PhaseIssueThreshold { get; set; } = 0.0f;

    /// <summary>
    /// Event raised when analysis is updated during real-time processing.
    /// </summary>
    public event EventHandler<PhaseAnalysisEventArgs>? AnalysisUpdated;

    /// <summary>
    /// Creates a new phase analyzer with the specified configuration.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate in Hz (default: 44100).</param>
    /// <param name="fftSize">FFT window size, must be power of 2 (default: 4096).</param>
    public PhaseAnalyzer(int sampleRate = 44100, int fftSize = 4096)
    {
        if (!IsPowerOfTwo(fftSize))
            throw new ArgumentException("FFT size must be a power of two.", nameof(fftSize));

        _sampleRate = sampleRate;
        _fftSize = fftSize;

        _leftFftBuffer = new Complex[fftSize];
        _rightFftBuffer = new Complex[fftSize];
        _leftSampleBuffer = new float[fftSize];
        _rightSampleBuffer = new float[fftSize];
        _window = GenerateHannWindow(fftSize);
        _phaseHistogram = new int[HistogramBins];

        _bandCorrelationSum = new double[NumBands];
        _bandPhaseDiffSum = new double[NumBands];
        _bandPhaseDiffSqSum = new double[NumBands];
        _bandEnergySum = new double[NumBands];
        _bandSampleCounts = new int[NumBands];

        // Pre-calculate bin ranges
        _bandBinRanges = new int[NumBands * 2];
        float binResolution = (float)sampleRate / fftSize;

        for (int i = 0; i < NumBands; i++)
        {
            var (low, high) = BandRanges[i];
            _bandBinRanges[i * 2] = Math.Max(1, (int)(low / binResolution));
            _bandBinRanges[i * 2 + 1] = Math.Min(fftSize / 2 - 1, (int)(high / binResolution));
        }
    }

    /// <summary>
    /// Creates a new phase analyzer that wraps a stereo audio source for inline analysis.
    /// </summary>
    /// <param name="source">Stereo audio source (must have 2 channels).</param>
    /// <param name="fftSize">FFT window size (default: 4096).</param>
    public PhaseAnalyzer(ISampleProvider source, int fftSize = 4096)
        : this(source.WaveFormat.SampleRate, fftSize)
    {
        if (source.WaveFormat.Channels != 2)
            throw new ArgumentException("Phase analyzer requires a stereo (2-channel) source.", nameof(source));

        _source = source;
    }

    /// <summary>
    /// Reads audio samples, performs phase analysis, and passes through unchanged.
    /// Only available when constructed with a source.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        if (_source == null)
            throw new InvalidOperationException("Read is only available when constructed with a source.");

        int samplesRead = _source.Read(buffer, offset, count);
        if (samplesRead == 0) return 0;

        AnalyzeSamples(buffer, offset, samplesRead);
        return samplesRead;
    }

    /// <summary>
    /// Analyzes interleaved stereo samples for phase correlation.
    /// Call this method directly when not using the ISampleProvider interface.
    /// </summary>
    /// <param name="samples">Interleaved stereo samples (L, R, L, R, ...).</param>
    /// <param name="offset">Starting offset in the array.</param>
    /// <param name="count">Number of samples to process (must be even).</param>
    public void AnalyzeSamples(float[] samples, int offset, int count)
    {
        int frames = count / 2;

        for (int frame = 0; frame < frames; frame++)
        {
            float left = samples[offset + frame * 2];
            float right = samples[offset + frame * 2 + 1];

            _leftSampleBuffer[_sampleCount] = left;
            _rightSampleBuffer[_sampleCount] = right;

            // Time-domain correlation
            _sumLeftRight += left * right;
            _sumLeftSq += left * left;
            _sumRightSq += right * right;
            _overallSampleCount++;

            _sampleCount++;
            _totalSamplesProcessed++;

            if (_sampleCount >= _fftSize)
            {
                ProcessFrame();
                _sampleCount = 0;
            }
        }
    }

    /// <summary>
    /// Analyzes a complete stereo audio buffer and returns the phase analysis result.
    /// This is the preferred method for offline (non-real-time) analysis.
    /// </summary>
    /// <param name="leftChannel">Left channel samples.</param>
    /// <param name="rightChannel">Right channel samples.</param>
    /// <returns>Complete phase analysis result.</returns>
    public PhaseAnalysisResult AnalyzeBuffer(float[] leftChannel, float[] rightChannel)
    {
        if (leftChannel == null || leftChannel.Length == 0)
            throw new ArgumentException("Left channel cannot be null or empty.", nameof(leftChannel));
        if (rightChannel == null || rightChannel.Length == 0)
            throw new ArgumentException("Right channel cannot be null or empty.", nameof(rightChannel));

        int length = Math.Min(leftChannel.Length, rightChannel.Length);

        Reset();

        // Interleave for processing
        float[] interleaved = new float[length * 2];
        for (int i = 0; i < length; i++)
        {
            interleaved[i * 2] = leftChannel[i];
            interleaved[i * 2 + 1] = rightChannel[i];
        }

        AnalyzeSamples(interleaved, 0, interleaved.Length);

        return CreateResult((float)length / _sampleRate);
    }

    /// <summary>
    /// Gets the current analysis result from real-time processing.
    /// </summary>
    /// <returns>Current phase analysis result, or null if no data has been processed.</returns>
    public PhaseAnalysisResult? GetCurrentResult()
    {
        lock (_lock)
        {
            if (_frameCount == 0 && _overallSampleCount == 0)
                return null;

            return CreateResult((float)_totalSamplesProcessed / _sampleRate);
        }
    }

    /// <summary>
    /// Resets the analyzer state.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _sampleCount = 0;
            _frameCount = 0;
            _totalSamplesProcessed = 0;
            _histogramSampleCount = 0;

            _overallCorrelationSum = 0;
            _overallSampleCount = 0;
            _sumLeftRight = 0;
            _sumLeftSq = 0;
            _sumRightSq = 0;

            Array.Clear(_leftSampleBuffer, 0, _leftSampleBuffer.Length);
            Array.Clear(_rightSampleBuffer, 0, _rightSampleBuffer.Length);
            Array.Clear(_leftFftBuffer, 0, _leftFftBuffer.Length);
            Array.Clear(_rightFftBuffer, 0, _rightFftBuffer.Length);
            Array.Clear(_phaseHistogram, 0, _phaseHistogram.Length);

            Array.Clear(_bandCorrelationSum, 0, _bandCorrelationSum.Length);
            Array.Clear(_bandPhaseDiffSum, 0, _bandPhaseDiffSum.Length);
            Array.Clear(_bandPhaseDiffSqSum, 0, _bandPhaseDiffSqSum.Length);
            Array.Clear(_bandEnergySum, 0, _bandEnergySum.Length);
            Array.Clear(_bandSampleCounts, 0, _bandSampleCounts.Length);
        }
    }

    private void ProcessFrame()
    {
        // Apply window and copy to FFT buffers
        for (int i = 0; i < _fftSize; i++)
        {
            float w = _window[i];
            _leftFftBuffer[i].X = _leftSampleBuffer[i] * w;
            _leftFftBuffer[i].Y = 0;
            _rightFftBuffer[i].X = _rightSampleBuffer[i] * w;
            _rightFftBuffer[i].Y = 0;
        }

        // Perform FFTs
        int m = (int)Math.Log(_fftSize, 2.0);
        FastFourierTransform.FFT(true, m, _leftFftBuffer);
        FastFourierTransform.FFT(true, m, _rightFftBuffer);

        // Analyze per-band phase correlation
        lock (_lock)
        {
            for (int band = 0; band < NumBands; band++)
            {
                int lowBin = _bandBinRanges[band * 2];
                int highBin = _bandBinRanges[band * 2 + 1];

                double correlationSum = 0;
                double phaseDiffSum = 0;
                double phaseDiffSqSum = 0;
                double energySum = 0;
                int binCount = 0;

                for (int bin = lowBin; bin <= highBin; bin++)
                {
                    float leftReal = _leftFftBuffer[bin].X;
                    float leftImag = _leftFftBuffer[bin].Y;
                    float rightReal = _rightFftBuffer[bin].X;
                    float rightImag = _rightFftBuffer[bin].Y;

                    float leftMag = (float)Math.Sqrt(leftReal * leftReal + leftImag * leftImag);
                    float rightMag = (float)Math.Sqrt(rightReal * rightReal + rightImag * rightImag);

                    if (leftMag > 1e-10 && rightMag > 1e-10)
                    {
                        // Phase of each channel
                        float leftPhase = (float)Math.Atan2(leftImag, leftReal);
                        float rightPhase = (float)Math.Atan2(rightImag, rightReal);

                        // Phase difference
                        float phaseDiff = rightPhase - leftPhase;

                        // Wrap to -PI to +PI
                        while (phaseDiff > Math.PI) phaseDiff -= (float)(2 * Math.PI);
                        while (phaseDiff < -Math.PI) phaseDiff += (float)(2 * Math.PI);

                        float phaseDiffDeg = phaseDiff * (float)(180 / Math.PI);

                        // Cross-correlation in frequency domain
                        float crossReal = leftReal * rightReal + leftImag * rightImag;
                        float crossImag = leftImag * rightReal - leftReal * rightImag;
                        float crossMag = (float)Math.Sqrt(crossReal * crossReal + crossImag * crossImag);

                        float correlation = crossReal / (leftMag * rightMag);

                        correlationSum += correlation;
                        phaseDiffSum += phaseDiffDeg;
                        phaseDiffSqSum += phaseDiffDeg * phaseDiffDeg;
                        energySum += (leftMag + rightMag) / 2;
                        binCount++;

                        // Update histogram
                        int histBin = (int)((phaseDiffDeg + 180) / 5);
                        histBin = Math.Clamp(histBin, 0, HistogramBins - 1);
                        _phaseHistogram[histBin]++;
                        _histogramSampleCount++;
                    }
                }

                if (binCount > 0)
                {
                    _bandCorrelationSum[band] += correlationSum / binCount;
                    _bandPhaseDiffSum[band] += phaseDiffSum / binCount;
                    _bandPhaseDiffSqSum[band] += phaseDiffSqSum / binCount;
                    _bandEnergySum[band] += energySum;
                    _bandSampleCounts[band]++;
                }
            }

            _frameCount++;
        }

        // Raise event periodically
        if (_frameCount % 10 == 0)
        {
            var result = CreateResult((float)_totalSamplesProcessed / _sampleRate);
            AnalysisUpdated?.Invoke(this, new PhaseAnalysisEventArgs(result));
        }
    }

    private PhaseAnalysisResult CreateResult(float durationSeconds)
    {
        lock (_lock)
        {
            // Overall correlation from time domain
            double overallCorrelation = 0;
            if (_sumLeftSq > 1e-10 && _sumRightSq > 1e-10)
            {
                overallCorrelation = _sumLeftRight / Math.Sqrt(_sumLeftSq * _sumRightSq);
            }

            // Band correlations
            var bandCorrelations = new BandPhaseCorrelation[NumBands];
            var problemFrequencies = new List<(float LowHz, float HighHz, float Severity)>();
            double totalPhaseDiff = 0;
            int validBands = 0;

            for (int band = 0; band < NumBands; band++)
            {
                var (lowFreq, highFreq) = BandRanges[band];
                float correlation = 0;
                float avgPhaseDiff = 0;
                float phaseDiffStdDev = 0;
                float energyDb = -90f;
                float severity = 0;
                bool hasIssue = false;

                if (_bandSampleCounts[band] > 0)
                {
                    int count = _bandSampleCounts[band];
                    correlation = (float)(_bandCorrelationSum[band] / count);
                    avgPhaseDiff = (float)(_bandPhaseDiffSum[band] / count);

                    double variance = (_bandPhaseDiffSqSum[band] / count) - (avgPhaseDiff * avgPhaseDiff);
                    phaseDiffStdDev = (float)Math.Sqrt(Math.Max(0, variance));

                    float energy = (float)(_bandEnergySum[band] / count);
                    energyDb = 20f * (float)Math.Log10(Math.Max(energy, 1e-10f));

                    hasIssue = correlation < PhaseIssueThreshold;
                    if (hasIssue)
                    {
                        severity = Math.Clamp(-correlation, 0, 1);
                        problemFrequencies.Add((lowFreq, highFreq, severity));
                    }

                    totalPhaseDiff += Math.Abs(avgPhaseDiff);
                    validBands++;
                }

                bandCorrelations[band] = new BandPhaseCorrelation
                {
                    BandIndex = band,
                    LowFrequency = lowFreq,
                    HighFrequency = highFreq,
                    Correlation = correlation,
                    HasPhaseIssue = hasIssue,
                    IssueSeverity = severity,
                    AveragePhaseDifferenceDegrees = avgPhaseDiff,
                    PhaseDifferenceStdDevDegrees = phaseDiffStdDev,
                    EnergyDb = energyDb
                };
            }

            // Create histogram result
            PhaseDifferenceHistogram? histogram = null;
            if (_histogramSampleCount > 0)
            {
                int peakBin = 0;
                int peakCount = 0;
                for (int i = 0; i < HistogramBins; i++)
                {
                    if (_phaseHistogram[i] > peakCount)
                    {
                        peakCount = _phaseHistogram[i];
                        peakBin = i;
                    }
                }

                histogram = new PhaseDifferenceHistogram
                {
                    BinCounts = (int[])_phaseHistogram.Clone(),
                    BinWidthDegrees = 5f,
                    TotalSamples = _histogramSampleCount,
                    PeakBinIndex = peakBin,
                    PeakPhaseDegrees = peakBin * 5f - 180f + 2.5f,
                    PeakPercentage = (float)peakCount / _histogramSampleCount
                };
            }

            // Calculate mono compatibility and other metrics
            float monoCompatibility = Math.Max(0, 100 * (float)((overallCorrelation + 1) / 2));

            bool likelyPolarityInverted = overallCorrelation < -0.5;
            bool significantMonoCancellation = overallCorrelation < -0.2;

            // Estimate mono loss (rough approximation)
            // At correlation = 1, mono level = sum of L+R
            // At correlation = -1, mono level = 0 (complete cancellation)
            float monoLossDb = overallCorrelation >= 0
                ? 0
                : (float)(20 * Math.Log10(Math.Max(1 + overallCorrelation, 0.01)));

            // Stereo width: 0 = mono, 1 = very wide
            float stereoWidth = (float)((1 - overallCorrelation) / 2);

            float avgPhaseDiffOverall = validBands > 0 ? (float)(totalPhaseDiff / validBands) : 0;

            // Generate suggestions
            var suggestions = new List<string>();

            if (likelyPolarityInverted)
            {
                suggestions.Add("CRITICAL: One channel appears to have inverted polarity. Check phase/polarity settings.");
            }

            if (significantMonoCancellation && !likelyPolarityInverted)
            {
                suggestions.Add("WARNING: Significant phase cancellation will occur in mono playback.");
            }

            if (problemFrequencies.Count > 0)
            {
                var sortedProblems = problemFrequencies.FindAll(p => p.Severity > 0.3f);
                if (sortedProblems.Count > 0)
                {
                    sortedProblems.Sort((a, b) => b.Severity.CompareTo(a.Severity));
                    var worst = sortedProblems[0];
                    suggestions.Add($"Phase issues detected in {worst.LowHz:F0}-{worst.HighHz:F0} Hz range (severity: {worst.Severity:P0}).");
                }
            }

            if (stereoWidth > 0.8)
            {
                suggestions.Add("Very wide stereo image - may have mono compatibility issues.");
            }

            if (stereoWidth < 0.1 && overallCorrelation > 0.9)
            {
                suggestions.Add("Audio is nearly mono - consider adding stereo width if desired.");
            }

            if (suggestions.Count == 0 && monoCompatibility > 70)
            {
                suggestions.Add("Phase relationship is healthy. Good mono compatibility.");
            }

            return new PhaseAnalysisResult
            {
                OverallCorrelation = (float)overallCorrelation,
                MonoCompatibilityScore = monoCompatibility,
                BandCorrelations = bandCorrelations,
                ProblemFrequencies = problemFrequencies.ToArray(),
                Histogram = histogram,
                AveragePhaseDifferenceDegrees = avgPhaseDiffOverall,
                LikelyPolarityInverted = likelyPolarityInverted,
                SignificantMonoCancellation = significantMonoCancellation,
                EstimatedMonoLossDb = monoLossDb,
                StereoWidth = stereoWidth,
                Suggestions = suggestions.ToArray(),
                DurationSeconds = durationSeconds
            };
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

    private static bool IsPowerOfTwo(int n) => n > 0 && (n & (n - 1)) == 0;
}
