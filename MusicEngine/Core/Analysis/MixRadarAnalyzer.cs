//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Mix radar analyzer for frequency balance analysis with 8-band spectrum comparison (SPAN-style).

using System;
using System.Collections.Generic;
using NAudio.Dsp;
using NAudio.Wave;

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Frequency band definitions for mix analysis.
/// </summary>
public enum MixBand
{
    /// <summary>Sub bass (20-60 Hz) - Felt more than heard, adds weight.</summary>
    Sub = 0,

    /// <summary>Bass (60-250 Hz) - Fundamental bass frequencies.</summary>
    Bass = 1,

    /// <summary>Low-Mid (250-500 Hz) - Body of instruments, can cause muddiness.</summary>
    LowMid = 2,

    /// <summary>Mid (500-2000 Hz) - Main presence of most instruments.</summary>
    Mid = 3,

    /// <summary>High-Mid (2-4 kHz) - Attack, clarity, can be harsh.</summary>
    HighMid = 4,

    /// <summary>Presence (4-6 kHz) - Definition, sibilance range.</summary>
    Presence = 5,

    /// <summary>Brilliance (6-12 kHz) - Sparkle, harmonics.</summary>
    Brilliance = 6,

    /// <summary>Air (12-20 kHz) - Airiness, openness, shimmer.</summary>
    Air = 7
}

/// <summary>
/// Analysis result for a single frequency band.
/// </summary>
public class BandAnalysisResult
{
    /// <summary>Gets the band identifier.</summary>
    public MixBand Band { get; init; }

    /// <summary>Gets the band name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the lower frequency boundary in Hz.</summary>
    public float LowFrequency { get; init; }

    /// <summary>Gets the upper frequency boundary in Hz.</summary>
    public float HighFrequency { get; init; }

    /// <summary>Gets the center frequency in Hz.</summary>
    public float CenterFrequency => (float)Math.Sqrt(LowFrequency * HighFrequency);

    /// <summary>Gets the RMS level in dB.</summary>
    public float RmsDb { get; init; }

    /// <summary>Gets the RMS level as linear value (0.0 to 1.0).</summary>
    public float RmsLinear { get; init; }

    /// <summary>Gets the peak level in dB.</summary>
    public float PeakDb { get; init; }

    /// <summary>Gets the peak level as linear value.</summary>
    public float PeakLinear { get; init; }

    /// <summary>Gets the balance score (-1.0 = too quiet, 0 = balanced, +1.0 = too loud).</summary>
    public float BalanceScore { get; init; }

    /// <summary>Gets the difference from the reference curve in dB.</summary>
    public float DifferenceFromReferenceDb { get; init; }

    /// <summary>Gets the suggested correction in dB (negative = cut, positive = boost).</summary>
    public float SuggestedCorrectionDb { get; init; }

    /// <summary>Gets whether this band needs attention.</summary>
    public bool NeedsAttention => Math.Abs(BalanceScore) > 0.3f;
}

/// <summary>
/// Reference curve types for mix comparison.
/// </summary>
public enum ReferenceCurveType
{
    /// <summary>Flat response (equal energy across all bands).</summary>
    Flat,

    /// <summary>Pink noise reference (-3dB/octave).</summary>
    PinkNoise,

    /// <summary>Typical modern pop/rock mix curve.</summary>
    ModernPop,

    /// <summary>EDM/Electronic music curve (enhanced bass and highs).</summary>
    Electronic,

    /// <summary>Classical/Orchestral curve (natural balance).</summary>
    Classical,

    /// <summary>Hip-Hop/Trap curve (heavy bass emphasis).</summary>
    HipHop,

    /// <summary>Rock/Metal curve (emphasized mids and presence).</summary>
    Rock,

    /// <summary>Acoustic/Folk curve (natural, mid-focused).</summary>
    Acoustic,

    /// <summary>Custom reference curve (user-defined).</summary>
    Custom
}

/// <summary>
/// Complete mix analysis result.
/// </summary>
public class MixAnalysisResult
{
    /// <summary>Gets the analysis results for all 8 bands.</summary>
    public BandAnalysisResult[] Bands { get; init; } = Array.Empty<BandAnalysisResult>();

    /// <summary>Gets the overall balance score (0-100, where 100 is perfectly balanced).</summary>
    public float OverallBalanceScore { get; init; }

    /// <summary>Gets the reference curve type used for comparison.</summary>
    public ReferenceCurveType ReferenceCurve { get; init; }

    /// <summary>Gets the total RMS level in dB.</summary>
    public float TotalRmsDb { get; init; }

    /// <summary>Gets the total peak level in dB.</summary>
    public float TotalPeakDb { get; init; }

    /// <summary>Gets the crest factor (peak/RMS ratio) in dB.</summary>
    public float CrestFactorDb { get; init; }

    /// <summary>Gets the spectral centroid in Hz (brightness indicator).</summary>
    public float SpectralCentroidHz { get; init; }

    /// <summary>Gets the low/high frequency ratio (bass weight indicator).</summary>
    public float LowHighRatio { get; init; }

    /// <summary>Gets a list of correction suggestions.</summary>
    public string[] Suggestions { get; init; } = Array.Empty<string>();

    /// <summary>Gets the analysis timestamp.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>Gets the duration of audio analyzed in seconds.</summary>
    public float DurationSeconds { get; init; }
}

/// <summary>
/// Event arguments for real-time mix analysis updates.
/// </summary>
public class MixAnalysisEventArgs : EventArgs
{
    /// <summary>Gets the current analysis result.</summary>
    public MixAnalysisResult Result { get; }

    /// <summary>
    /// Creates new mix analysis event arguments.
    /// </summary>
    public MixAnalysisEventArgs(MixAnalysisResult result)
    {
        Result = result;
    }
}

/// <summary>
/// Mix radar analyzer for comprehensive frequency balance analysis.
/// Provides SPAN-style 8-band analysis with reference curve comparison and correction suggestions.
/// </summary>
/// <remarks>
/// The analyzer divides the frequency spectrum into 8 perceptually meaningful bands:
/// - Sub (20-60 Hz): Sub bass, felt more than heard
/// - Bass (60-250 Hz): Fundamental bass frequencies
/// - Low-Mid (250-500 Hz): Body, can cause muddiness
/// - Mid (500-2000 Hz): Main instrument presence
/// - High-Mid (2-4 kHz): Attack and clarity
/// - Presence (4-6 kHz): Definition and sibilance
/// - Brilliance (6-12 kHz): Sparkle and harmonics
/// - Air (12-20 kHz): Airiness and shimmer
/// </remarks>
public class MixRadarAnalyzer
{
    // Band frequency boundaries
    private static readonly (float Low, float High, string Name)[] BandDefinitions =
    {
        (20f, 60f, "Sub"),
        (60f, 250f, "Bass"),
        (250f, 500f, "Low-Mid"),
        (500f, 2000f, "Mid"),
        (2000f, 4000f, "High-Mid"),
        (4000f, 6000f, "Presence"),
        (6000f, 12000f, "Brilliance"),
        (12000f, 20000f, "Air")
    };

    // Reference curves (dB offsets for each band relative to flat)
    private static readonly Dictionary<ReferenceCurveType, float[]> ReferenceCurves = new()
    {
        { ReferenceCurveType.Flat, new[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f } },
        { ReferenceCurveType.PinkNoise, new[] { 3f, 0f, -1.5f, -3f, -4.5f, -5.5f, -6.5f, -7.5f } },
        { ReferenceCurveType.ModernPop, new[] { 2f, 3f, -1f, 0f, 1f, 0f, -1f, -2f } },
        { ReferenceCurveType.Electronic, new[] { 4f, 5f, -2f, -1f, 0f, 1f, 2f, 1f } },
        { ReferenceCurveType.Classical, new[] { 0f, 1f, 0f, 1f, 0f, 0f, 0f, -1f } },
        { ReferenceCurveType.HipHop, new[] { 6f, 6f, 0f, -1f, 1f, 0f, -1f, -2f } },
        { ReferenceCurveType.Rock, new[] { 1f, 2f, 1f, 2f, 3f, 1f, 0f, -1f } },
        { ReferenceCurveType.Acoustic, new[] { -1f, 1f, 2f, 2f, 1f, 0f, -1f, -2f } }
    };

    private readonly int _sampleRate;
    private readonly int _fftSize;
    private readonly Complex[] _fftBuffer;
    private readonly float[] _sampleBuffer;
    private readonly float[] _window;
    private readonly int[] _bandBinRanges;
    private readonly object _lock = new();

    private float[] _customReferenceCurve;
    private ReferenceCurveType _referenceCurveType = ReferenceCurveType.PinkNoise;

    // Real-time analysis state
    private int _sampleCount;
    private readonly double[] _bandRmsAccumulators;
    private readonly float[] _bandPeaks;
    private int _frameCount;
    private float _totalRmsAccumulator;
    private float _totalPeak;
    private double _spectralCentroidAccumulator;
    private long _totalSamplesProcessed;

    /// <summary>
    /// Gets the sample rate used for analysis.
    /// </summary>
    public int SampleRate => _sampleRate;

    /// <summary>
    /// Gets the FFT size used for analysis.
    /// </summary>
    public int FftSize => _fftSize;

    /// <summary>
    /// Gets or sets the reference curve type for comparison.
    /// </summary>
    public ReferenceCurveType ReferenceCurve
    {
        get => _referenceCurveType;
        set => _referenceCurveType = value;
    }

    /// <summary>
    /// Gets or sets a custom reference curve (8 values in dB).
    /// Only used when ReferenceCurve is set to Custom.
    /// </summary>
    public float[] CustomReferenceCurve
    {
        get => (float[])_customReferenceCurve.Clone();
        set
        {
            if (value == null || value.Length != 8)
                throw new ArgumentException("Custom reference curve must have exactly 8 values.");
            _customReferenceCurve = (float[])value.Clone();
        }
    }

    /// <summary>
    /// Event raised when analysis is updated during real-time processing.
    /// </summary>
    public event EventHandler<MixAnalysisEventArgs>? AnalysisUpdated;

    /// <summary>
    /// Creates a new mix radar analyzer with the specified configuration.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate in Hz (default: 44100).</param>
    /// <param name="fftSize">FFT window size, must be power of 2 (default: 4096).</param>
    /// <param name="referenceCurve">Initial reference curve type (default: PinkNoise).</param>
    public MixRadarAnalyzer(
        int sampleRate = 44100,
        int fftSize = 4096,
        ReferenceCurveType referenceCurve = ReferenceCurveType.PinkNoise)
    {
        if (!IsPowerOfTwo(fftSize))
            throw new ArgumentException("FFT size must be a power of two.", nameof(fftSize));

        _sampleRate = sampleRate;
        _fftSize = fftSize;
        _referenceCurveType = referenceCurve;

        _fftBuffer = new Complex[fftSize];
        _sampleBuffer = new float[fftSize];
        _window = GenerateHannWindow(fftSize);

        _customReferenceCurve = new float[8];
        _bandRmsAccumulators = new double[8];
        _bandPeaks = new float[8];

        // Pre-calculate bin ranges for each band
        _bandBinRanges = new int[16]; // 8 bands x 2 (low, high)
        float binResolution = (float)sampleRate / fftSize;

        for (int i = 0; i < 8; i++)
        {
            var (low, high, _) = BandDefinitions[i];
            _bandBinRanges[i * 2] = Math.Max(1, (int)(low / binResolution));
            _bandBinRanges[i * 2 + 1] = Math.Min(fftSize / 2 - 1, (int)(high / binResolution));
        }
    }

    /// <summary>
    /// Processes audio samples for real-time analysis.
    /// Call this continuously with incoming audio for streaming analysis.
    /// </summary>
    /// <param name="samples">Audio samples (mono or interleaved - first channel used).</param>
    /// <param name="count">Number of samples to process.</param>
    /// <param name="channels">Number of audio channels (default: 1 for mono).</param>
    public void ProcessSamples(float[] samples, int count, int channels = 1)
    {
        for (int i = 0; i < count; i += channels)
        {
            _sampleBuffer[_sampleCount] = samples[i];
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
    /// Analyzes a complete audio buffer and returns the mix analysis result.
    /// This is the preferred method for offline (non-real-time) analysis.
    /// </summary>
    /// <param name="samples">Complete audio buffer (mono).</param>
    /// <param name="sampleRate">Sample rate of the audio (uses analyzer's sample rate if 0).</param>
    /// <returns>Complete mix analysis result.</returns>
    public MixAnalysisResult AnalyzeBuffer(float[] samples, int sampleRate = 0)
    {
        if (samples == null || samples.Length == 0)
            throw new ArgumentException("Samples cannot be null or empty.", nameof(samples));

        if (sampleRate == 0)
            sampleRate = _sampleRate;

        Reset();

        int hopSize = _fftSize / 2;
        int position = 0;

        while (position + _fftSize <= samples.Length)
        {
            Array.Copy(samples, position, _sampleBuffer, 0, _fftSize);
            _sampleCount = _fftSize;
            ProcessFrame();
            _sampleCount = 0;
            position += hopSize;
        }

        return CreateResult((float)samples.Length / sampleRate);
    }

    /// <summary>
    /// Gets the current analysis result from real-time processing.
    /// </summary>
    /// <returns>Current mix analysis result, or null if no data has been processed.</returns>
    public MixAnalysisResult? GetCurrentResult()
    {
        lock (_lock)
        {
            if (_frameCount == 0)
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
            _totalRmsAccumulator = 0;
            _totalPeak = 0;
            _spectralCentroidAccumulator = 0;
            Array.Clear(_sampleBuffer, 0, _sampleBuffer.Length);
            Array.Clear(_fftBuffer, 0, _fftBuffer.Length);
            Array.Clear(_bandRmsAccumulators, 0, _bandRmsAccumulators.Length);
            Array.Clear(_bandPeaks, 0, _bandPeaks.Length);
        }
    }

    /// <summary>
    /// Gets the reference curve values for a specific curve type.
    /// </summary>
    /// <param name="curveType">The reference curve type.</param>
    /// <returns>Array of 8 dB values for each band.</returns>
    public static float[] GetReferenceCurveValues(ReferenceCurveType curveType)
    {
        if (ReferenceCurves.TryGetValue(curveType, out var curve))
            return (float[])curve.Clone();

        return new float[8]; // Return flat if not found
    }

    /// <summary>
    /// Learns a reference curve from a reference track.
    /// </summary>
    /// <param name="samples">Reference audio samples (mono).</param>
    /// <returns>Learned reference curve (8 dB values).</returns>
    public float[] LearnReferenceCurve(float[] samples)
    {
        var originalCurve = _referenceCurveType;
        _referenceCurveType = ReferenceCurveType.Flat;

        var result = AnalyzeBuffer(samples);

        _referenceCurveType = originalCurve;

        // Extract band levels as the new reference
        float[] learned = new float[8];
        float avgLevel = 0;

        for (int i = 0; i < 8; i++)
        {
            avgLevel += result.Bands[i].RmsDb;
        }
        avgLevel /= 8;

        for (int i = 0; i < 8; i++)
        {
            learned[i] = result.Bands[i].RmsDb - avgLevel;
        }

        return learned;
    }

    private void ProcessFrame()
    {
        // Apply window and copy to FFT buffer
        for (int i = 0; i < _fftSize; i++)
        {
            _fftBuffer[i].X = _sampleBuffer[i] * _window[i];
            _fftBuffer[i].Y = 0;
        }

        // Perform FFT
        int m = (int)Math.Log(_fftSize, 2.0);
        FastFourierTransform.FFT(true, m, _fftBuffer);

        // Calculate energy and centroid
        float binResolution = (float)_sampleRate / _fftSize;
        double totalEnergy = 0;
        double weightedFreqSum = 0;

        // Calculate band energies
        float[] bandEnergies = new float[8];
        float[] bandPeaksFrame = new float[8];

        for (int band = 0; band < 8; band++)
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

                // For spectral centroid
                float freq = bin * binResolution;
                totalEnergy += magnitude;
                weightedFreqSum += magnitude * freq;
            }

            bandEnergies[band] = (float)energy;
            bandPeaksFrame[band] = peak;
        }

        // Update accumulators
        lock (_lock)
        {
            for (int band = 0; band < 8; band++)
            {
                _bandRmsAccumulators[band] += bandEnergies[band];
                _bandPeaks[band] = Math.Max(_bandPeaks[band], bandPeaksFrame[band]);
            }

            // Total RMS
            float frameRms = 0;
            for (int i = 0; i < _fftSize; i++)
            {
                frameRms += _sampleBuffer[i] * _sampleBuffer[i];
            }
            _totalRmsAccumulator += frameRms / _fftSize;

            // Total peak
            for (int i = 0; i < _fftSize; i++)
            {
                _totalPeak = Math.Max(_totalPeak, Math.Abs(_sampleBuffer[i]));
            }

            // Spectral centroid
            if (totalEnergy > 1e-10)
            {
                _spectralCentroidAccumulator += weightedFreqSum / totalEnergy;
            }

            _frameCount++;
        }

        // Raise event periodically
        if (_frameCount % 10 == 0)
        {
            var result = CreateResult((float)_totalSamplesProcessed / _sampleRate);
            AnalysisUpdated?.Invoke(this, new MixAnalysisEventArgs(result));
        }
    }

    private MixAnalysisResult CreateResult(float durationSeconds)
    {
        lock (_lock)
        {
            if (_frameCount == 0)
            {
                return new MixAnalysisResult
                {
                    Bands = CreateEmptyBands(),
                    OverallBalanceScore = 0,
                    ReferenceCurve = _referenceCurveType,
                    DurationSeconds = 0
                };
            }

            // Get reference curve
            float[] reference = _referenceCurveType == ReferenceCurveType.Custom
                ? _customReferenceCurve
                : GetReferenceCurveValues(_referenceCurveType);

            var bands = new BandAnalysisResult[8];
            float[] bandRmsDb = new float[8];
            float overallDeviation = 0;
            float lowEnergy = 0;
            float highEnergy = 0;

            // Calculate band results
            for (int band = 0; band < 8; band++)
            {
                var (lowFreq, highFreq, name) = BandDefinitions[band];

                float rms = (float)Math.Sqrt(_bandRmsAccumulators[band] / _frameCount);
                float rmsDb = 20f * (float)Math.Log10(Math.Max(rms, 1e-10f));
                float peakDb = 20f * (float)Math.Log10(Math.Max(_bandPeaks[band], 1e-10f));

                bandRmsDb[band] = rmsDb;

                // Track low/high energy for ratio
                if (band < 2)
                    lowEnergy += (float)_bandRmsAccumulators[band];
                if (band >= 6)
                    highEnergy += (float)_bandRmsAccumulators[band];
            }

            // Normalize to average and compare to reference
            float avgBandDb = 0;
            for (int i = 0; i < 8; i++)
                avgBandDb += bandRmsDb[i];
            avgBandDb /= 8;

            var suggestions = new List<string>();

            for (int band = 0; band < 8; band++)
            {
                var (lowFreq, highFreq, name) = BandDefinitions[band];

                float normalizedDb = bandRmsDb[band] - avgBandDb;
                float differenceFromRef = normalizedDb - reference[band];
                float balanceScore = Math.Clamp(differenceFromRef / 6f, -1f, 1f); // +/-6dB = +/-1.0

                overallDeviation += Math.Abs(differenceFromRef);

                float suggestedCorrection = -differenceFromRef;
                suggestedCorrection = Math.Clamp(suggestedCorrection, -12f, 12f);

                // Generate suggestions for significant deviations
                if (Math.Abs(differenceFromRef) > 3f)
                {
                    string action = differenceFromRef > 0 ? "Cut" : "Boost";
                    suggestions.Add($"{action} {name} ({lowFreq}-{highFreq} Hz) by {Math.Abs(suggestedCorrection):F1} dB");
                }

                bands[band] = new BandAnalysisResult
                {
                    Band = (MixBand)band,
                    Name = name,
                    LowFrequency = lowFreq,
                    HighFrequency = highFreq,
                    RmsDb = bandRmsDb[band],
                    RmsLinear = (float)Math.Pow(10, bandRmsDb[band] / 20),
                    PeakDb = 20f * (float)Math.Log10(Math.Max(_bandPeaks[band], 1e-10f)),
                    PeakLinear = _bandPeaks[band],
                    BalanceScore = balanceScore,
                    DifferenceFromReferenceDb = differenceFromRef,
                    SuggestedCorrectionDb = suggestedCorrection
                };
            }

            // Calculate overall scores
            float maxPossibleDeviation = 8 * 12; // 8 bands, max 12dB deviation each
            float overallBalance = Math.Max(0, 100 * (1 - overallDeviation / maxPossibleDeviation));

            float totalRms = (float)Math.Sqrt(_totalRmsAccumulator / _frameCount);
            float totalRmsDb = 20f * (float)Math.Log10(Math.Max(totalRms, 1e-10f));
            float totalPeakDb = 20f * (float)Math.Log10(Math.Max(_totalPeak, 1e-10f));
            float crestFactor = totalPeakDb - totalRmsDb;

            float spectralCentroid = (float)(_spectralCentroidAccumulator / _frameCount);

            float lowHighRatio = highEnergy > 1e-10
                ? (float)(10 * Math.Log10(lowEnergy / highEnergy))
                : 0;

            // Add general suggestions
            if (crestFactor < 6)
            {
                suggestions.Add("Mix may be over-compressed (low dynamic range)");
            }
            else if (crestFactor > 20)
            {
                suggestions.Add("Mix has high dynamic range - consider gentle compression");
            }

            if (lowHighRatio > 10)
            {
                suggestions.Add("Mix is bass-heavy - consider high-frequency enhancement");
            }
            else if (lowHighRatio < -5)
            {
                suggestions.Add("Mix lacks low-end weight - consider bass enhancement");
            }

            return new MixAnalysisResult
            {
                Bands = bands,
                OverallBalanceScore = overallBalance,
                ReferenceCurve = _referenceCurveType,
                TotalRmsDb = totalRmsDb,
                TotalPeakDb = totalPeakDb,
                CrestFactorDb = crestFactor,
                SpectralCentroidHz = spectralCentroid,
                LowHighRatio = lowHighRatio,
                Suggestions = suggestions.ToArray(),
                DurationSeconds = durationSeconds
            };
        }
    }

    private static BandAnalysisResult[] CreateEmptyBands()
    {
        var bands = new BandAnalysisResult[8];
        for (int i = 0; i < 8; i++)
        {
            var (lowFreq, highFreq, name) = BandDefinitions[i];
            bands[i] = new BandAnalysisResult
            {
                Band = (MixBand)i,
                Name = name,
                LowFrequency = lowFreq,
                HighFrequency = highFreq,
                RmsDb = -90f,
                RmsLinear = 0,
                PeakDb = -90f,
                PeakLinear = 0,
                BalanceScore = 0,
                DifferenceFromReferenceDb = 0,
                SuggestedCorrectionDb = 0
            };
        }
        return bands;
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
