//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Analyzes frequency collisions (masking) between two audio sources and suggests EQ corrections.

using System;
using System.Collections.Generic;
using NAudio.Dsp;

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Represents a frequency band collision analysis result.
/// </summary>
public class BandCollision
{
    /// <summary>Center frequency of the band in Hz.</summary>
    public float CenterFrequency { get; set; }

    /// <summary>Low frequency boundary of the band in Hz.</summary>
    public float LowFrequency { get; set; }

    /// <summary>High frequency boundary of the band in Hz.</summary>
    public float HighFrequency { get; set; }

    /// <summary>Collision score (0.0 = no collision, 1.0 = full masking).</summary>
    public float CollisionScore { get; set; }

    /// <summary>Average magnitude of source A in this band (dB).</summary>
    public float SourceAMagnitudeDb { get; set; }

    /// <summary>Average magnitude of source B in this band (dB).</summary>
    public float SourceBMagnitudeDb { get; set; }

    /// <summary>Suggested EQ cut in dB for source A to reduce masking (negative value).</summary>
    public float SuggestedCutSourceADb { get; set; }

    /// <summary>Suggested EQ cut in dB for source B to reduce masking (negative value).</summary>
    public float SuggestedCutSourceBDb { get; set; }

    /// <summary>Whether this band has significant collision.</summary>
    public bool HasCollision => CollisionScore > 0.3f;

    /// <summary>Description of the collision severity.</summary>
    public string Severity => CollisionScore switch
    {
        < 0.2f => "None",
        < 0.4f => "Minor",
        < 0.6f => "Moderate",
        < 0.8f => "Significant",
        _ => "Severe"
    };
}

/// <summary>
/// Result of a frequency collision analysis between two audio sources.
/// </summary>
public class CollisionAnalysisResult
{
    /// <summary>Per-band collision analysis.</summary>
    public List<BandCollision> Bands { get; set; } = new();

    /// <summary>Overall collision score (0.0 to 1.0).</summary>
    public float OverallCollisionScore { get; set; }

    /// <summary>Number of bands with significant collision.</summary>
    public int CollisionBandCount { get; set; }

    /// <summary>Primary collision frequency (band with highest collision).</summary>
    public float PrimaryCollisionFrequency { get; set; }

    /// <summary>Sample rate used for analysis.</summary>
    public int SampleRate { get; set; }

    /// <summary>Total samples analyzed.</summary>
    public long SamplesAnalyzed { get; set; }

    /// <summary>Whether the sources have significant frequency collision.</summary>
    public bool HasSignificantCollision => OverallCollisionScore > 0.3f;

    /// <summary>
    /// Gets a summary of suggested EQ adjustments.
    /// </summary>
    public List<(float frequency, float cutDb, string source)> GetEqSuggestions()
    {
        var suggestions = new List<(float, float, string)>();

        foreach (var band in Bands)
        {
            if (band.HasCollision)
            {
                // Suggest cut for the source with higher magnitude
                if (band.SourceAMagnitudeDb > band.SourceBMagnitudeDb)
                {
                    suggestions.Add((band.CenterFrequency, band.SuggestedCutSourceADb, "A"));
                }
                else
                {
                    suggestions.Add((band.CenterFrequency, band.SuggestedCutSourceBDb, "B"));
                }
            }
        }

        return suggestions;
    }
}

/// <summary>
/// Detects frequency collisions (masking) between two audio sources and suggests EQ corrections.
/// Can be used for real-time monitoring or offline analysis.
/// </summary>
public class FrequencyCollisionDetector
{
    private readonly int _sampleRate;
    private readonly int _fftLength;
    private readonly int _bandCount;
    private readonly float[] _bandFrequencies;

    // FFT buffers for source A
    private readonly Complex[] _fftBufferA;
    private readonly float[] _sampleBufferA;
    private int _sampleCountA;

    // FFT buffers for source B
    private readonly Complex[] _fftBufferB;
    private readonly float[] _sampleBufferB;
    private int _sampleCountB;

    // Band magnitudes
    private readonly float[] _magnitudesA;
    private readonly float[] _magnitudesB;
    private readonly float[] _collisionScores;

    // Smoothing
    private readonly float[] _smoothedMagnitudesA;
    private readonly float[] _smoothedMagnitudesB;
    private float _smoothingFactor = 0.3f;

    private readonly object _lock = new();
    private long _totalSamplesProcessed;

    /// <summary>
    /// Gets or sets the collision threshold (0.0 to 1.0).
    /// Higher values require more overlap to be considered a collision.
    /// </summary>
    public float CollisionThreshold { get; set; } = 0.3f;

    /// <summary>
    /// Gets or sets the masking threshold in dB.
    /// When both sources exceed this level in a band, collision is evaluated.
    /// </summary>
    public float MaskingThresholdDb { get; set; } = -40f;

    /// <summary>
    /// Gets or sets the smoothing factor for real-time analysis (0.0 to 0.99).
    /// </summary>
    public float SmoothingFactor
    {
        get => _smoothingFactor;
        set => _smoothingFactor = Math.Clamp(value, 0f, 0.99f);
    }

    /// <summary>
    /// Gets or sets the maximum suggested EQ cut in dB.
    /// </summary>
    public float MaxSuggestedCutDb { get; set; } = -12f;

    /// <summary>
    /// Event raised when collision analysis is updated.
    /// </summary>
    public event EventHandler<CollisionAnalysisResult>? CollisionUpdated;

    /// <summary>
    /// Creates a new frequency collision detector.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate in Hz.</param>
    /// <param name="bandCount">Number of frequency bands to analyze.</param>
    /// <param name="fftLength">FFT window size (must be power of 2).</param>
    /// <param name="minFrequency">Minimum analysis frequency in Hz.</param>
    /// <param name="maxFrequency">Maximum analysis frequency in Hz.</param>
    public FrequencyCollisionDetector(
        int sampleRate = 44100,
        int bandCount = 31,
        int fftLength = 4096,
        float minFrequency = 20f,
        float maxFrequency = 20000f)
    {
        if ((fftLength & (fftLength - 1)) != 0)
            throw new ArgumentException("FFT length must be a power of 2", nameof(fftLength));

        _sampleRate = sampleRate;
        _fftLength = fftLength;
        _bandCount = bandCount;

        _fftBufferA = new Complex[fftLength];
        _sampleBufferA = new float[fftLength];
        _fftBufferB = new Complex[fftLength];
        _sampleBufferB = new float[fftLength];

        _magnitudesA = new float[bandCount];
        _magnitudesB = new float[bandCount];
        _collisionScores = new float[bandCount];
        _smoothedMagnitudesA = new float[bandCount];
        _smoothedMagnitudesB = new float[bandCount];

        _bandFrequencies = CalculateBandFrequencies(bandCount, minFrequency, maxFrequency);
    }

    /// <summary>
    /// Processes audio samples from both sources for real-time collision detection.
    /// </summary>
    /// <param name="samplesA">Samples from source A.</param>
    /// <param name="samplesB">Samples from source B.</param>
    /// <param name="count">Number of samples to process.</param>
    /// <param name="channels">Number of audio channels (both sources must match).</param>
    public void ProcessSamples(float[] samplesA, float[] samplesB, int count, int channels = 1)
    {
        for (int i = 0; i < count; i += channels)
        {
            // Mix to mono if stereo
            float sampleA = channels == 2 && i + 1 < count
                ? (samplesA[i] + samplesA[i + 1]) * 0.5f
                : samplesA[i];
            float sampleB = channels == 2 && i + 1 < count
                ? (samplesB[i] + samplesB[i + 1]) * 0.5f
                : samplesB[i];

            _sampleBufferA[_sampleCountA] = sampleA;
            _sampleBufferB[_sampleCountB] = sampleB;
            _sampleCountA++;
            _sampleCountB++;

            if (_sampleCountA >= _fftLength && _sampleCountB >= _fftLength)
            {
                PerformAnalysis();
                _sampleCountA = 0;
                _sampleCountB = 0;
            }
        }

        _totalSamplesProcessed += count / channels;
    }

    /// <summary>
    /// Analyzes complete audio buffers for collision (offline analysis).
    /// </summary>
    /// <param name="samplesA">Complete audio buffer for source A (mono).</param>
    /// <param name="samplesB">Complete audio buffer for source B (mono).</param>
    /// <returns>Collision analysis result.</returns>
    public CollisionAnalysisResult AnalyzeBuffers(float[] samplesA, float[] samplesB)
    {
        Reset();

        int minLength = Math.Min(samplesA.Length, samplesB.Length);
        int analysisCount = 0;

        // Process in FFT-sized chunks
        for (int i = 0; i <= minLength - _fftLength; i += _fftLength / 2)
        {
            Array.Copy(samplesA, i, _sampleBufferA, 0, _fftLength);
            Array.Copy(samplesB, i, _sampleBufferB, 0, _fftLength);

            PerformAnalysis();
            analysisCount++;
        }

        return BuildResult(minLength);
    }

    /// <summary>
    /// Gets the current collision analysis result.
    /// </summary>
    public CollisionAnalysisResult GetCurrentResult()
    {
        return BuildResult(_totalSamplesProcessed);
    }

    /// <summary>
    /// Resets the analyzer state.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _sampleCountA = 0;
            _sampleCountB = 0;
            _totalSamplesProcessed = 0;
            Array.Clear(_smoothedMagnitudesA);
            Array.Clear(_smoothedMagnitudesB);
            Array.Clear(_collisionScores);
        }
    }

    private void PerformAnalysis()
    {
        // Apply window and compute FFT for source A
        for (int i = 0; i < _fftLength; i++)
        {
            float window = (float)(0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (_fftLength - 1))));
            _fftBufferA[i].X = _sampleBufferA[i] * window;
            _fftBufferA[i].Y = 0;
            _fftBufferB[i].X = _sampleBufferB[i] * window;
            _fftBufferB[i].Y = 0;
        }

        int m = (int)Math.Log(_fftLength, 2.0);
        FastFourierTransform.FFT(true, m, _fftBufferA);
        FastFourierTransform.FFT(true, m, _fftBufferB);

        lock (_lock)
        {
            CalculateBandMagnitudes();
            CalculateCollisionScores();
        }

        // Raise event
        var result = BuildResult(_totalSamplesProcessed);
        CollisionUpdated?.Invoke(this, result);
    }

    private void CalculateBandMagnitudes()
    {
        float binResolution = (float)_sampleRate / _fftLength;
        int maxBin = _fftLength / 2;

        for (int band = 0; band < _bandCount; band++)
        {
            float lowFreq = band == 0 ? 0 : (_bandFrequencies[band - 1] + _bandFrequencies[band]) / 2;
            float highFreq = band == _bandCount - 1
                ? _sampleRate / 2f
                : (_bandFrequencies[band] + _bandFrequencies[band + 1]) / 2;

            int lowBin = Math.Max(1, (int)(lowFreq / binResolution));
            int highBin = Math.Min(maxBin - 1, (int)(highFreq / binResolution));

            if (lowBin > highBin)
                lowBin = highBin = Math.Max(1, (int)(_bandFrequencies[band] / binResolution));

            float sumA = 0, sumB = 0;
            int binCount = 0;

            for (int bin = lowBin; bin <= highBin; bin++)
            {
                float magA = MathF.Sqrt(_fftBufferA[bin].X * _fftBufferA[bin].X +
                                        _fftBufferA[bin].Y * _fftBufferA[bin].Y);
                float magB = MathF.Sqrt(_fftBufferB[bin].X * _fftBufferB[bin].X +
                                        _fftBufferB[bin].Y * _fftBufferB[bin].Y);
                sumA += magA;
                sumB += magB;
                binCount++;
            }

            float avgA = binCount > 0 ? sumA / binCount : 0;
            float avgB = binCount > 0 ? sumB / binCount : 0;

            // Apply smoothing
            _smoothedMagnitudesA[band] = _smoothedMagnitudesA[band] * _smoothingFactor +
                                         avgA * (1f - _smoothingFactor);
            _smoothedMagnitudesB[band] = _smoothedMagnitudesB[band] * _smoothingFactor +
                                         avgB * (1f - _smoothingFactor);

            // Convert to dB
            _magnitudesA[band] = 20f * MathF.Log10(_smoothedMagnitudesA[band] + 1e-10f);
            _magnitudesB[band] = 20f * MathF.Log10(_smoothedMagnitudesB[band] + 1e-10f);
        }
    }

    private void CalculateCollisionScores()
    {
        for (int band = 0; band < _bandCount; band++)
        {
            float magA = _magnitudesA[band];
            float magB = _magnitudesB[band];

            // Only consider collision if both sources are above threshold
            if (magA < MaskingThresholdDb || magB < MaskingThresholdDb)
            {
                _collisionScores[band] = 0;
                continue;
            }

            // Calculate overlap based on magnitude similarity
            float maxMag = Math.Max(magA, magB);
            float minMag = Math.Min(magA, magB);

            // Collision score based on how close the magnitudes are
            // and how loud they are
            float magnitudeDiff = maxMag - minMag;
            float proximity = 1f - Math.Min(magnitudeDiff / 20f, 1f); // 20dB range

            // Weight by overall loudness
            float loudnessFactor = Math.Min((maxMag + 60f) / 60f, 1f); // -60 to 0 dB range

            _collisionScores[band] = proximity * loudnessFactor;
        }
    }

    private CollisionAnalysisResult BuildResult(long samplesAnalyzed)
    {
        var result = new CollisionAnalysisResult
        {
            SampleRate = _sampleRate,
            SamplesAnalyzed = samplesAnalyzed
        };

        float totalScore = 0;
        int collisionCount = 0;
        float maxCollision = 0;
        float maxCollisionFreq = 0;

        lock (_lock)
        {
            for (int band = 0; band < _bandCount; band++)
            {
                float lowFreq = band == 0 ? 20f : (_bandFrequencies[band - 1] + _bandFrequencies[band]) / 2;
                float highFreq = band == _bandCount - 1
                    ? _sampleRate / 2f
                    : (_bandFrequencies[band] + _bandFrequencies[band + 1]) / 2;

                var collision = new BandCollision
                {
                    CenterFrequency = _bandFrequencies[band],
                    LowFrequency = lowFreq,
                    HighFrequency = highFreq,
                    CollisionScore = _collisionScores[band],
                    SourceAMagnitudeDb = _magnitudesA[band],
                    SourceBMagnitudeDb = _magnitudesB[band]
                };

                // Calculate suggested EQ cuts
                if (collision.HasCollision)
                {
                    collisionCount++;

                    // Suggest larger cuts for source with higher magnitude
                    float cutRatio = collision.CollisionScore;
                    if (collision.SourceAMagnitudeDb > collision.SourceBMagnitudeDb)
                    {
                        collision.SuggestedCutSourceADb = MaxSuggestedCutDb * cutRatio;
                        collision.SuggestedCutSourceBDb = MaxSuggestedCutDb * cutRatio * 0.5f;
                    }
                    else
                    {
                        collision.SuggestedCutSourceADb = MaxSuggestedCutDb * cutRatio * 0.5f;
                        collision.SuggestedCutSourceBDb = MaxSuggestedCutDb * cutRatio;
                    }
                }

                result.Bands.Add(collision);
                totalScore += collision.CollisionScore;

                if (collision.CollisionScore > maxCollision)
                {
                    maxCollision = collision.CollisionScore;
                    maxCollisionFreq = collision.CenterFrequency;
                }
            }
        }

        result.OverallCollisionScore = _bandCount > 0 ? totalScore / _bandCount : 0;
        result.CollisionBandCount = collisionCount;
        result.PrimaryCollisionFrequency = maxCollisionFreq;

        return result;
    }

    private static float[] CalculateBandFrequencies(int bandCount, float minFreq, float maxFreq)
    {
        float[] frequencies = new float[bandCount];
        float logMin = MathF.Log10(minFreq);
        float logMax = MathF.Log10(maxFreq);
        float logStep = (logMax - logMin) / (bandCount - 1);

        for (int i = 0; i < bandCount; i++)
        {
            frequencies[i] = MathF.Pow(10, logMin + i * logStep);
        }

        return frequencies;
    }

    /// <summary>
    /// Creates a detector preset for kick/bass collision analysis.
    /// </summary>
    public static FrequencyCollisionDetector CreateKickBassPreset(int sampleRate = 44100)
    {
        return new FrequencyCollisionDetector(
            sampleRate,
            bandCount: 16,
            fftLength: 4096,
            minFrequency: 20f,
            maxFrequency: 500f)
        {
            CollisionThreshold = 0.25f,
            MaskingThresholdDb = -36f,
            MaxSuggestedCutDb = -9f
        };
    }

    /// <summary>
    /// Creates a detector preset for vocal/instrument collision analysis.
    /// </summary>
    public static FrequencyCollisionDetector CreateVocalPreset(int sampleRate = 44100)
    {
        return new FrequencyCollisionDetector(
            sampleRate,
            bandCount: 24,
            fftLength: 4096,
            minFrequency: 150f,
            maxFrequency: 8000f)
        {
            CollisionThreshold = 0.3f,
            MaskingThresholdDb = -42f,
            MaxSuggestedCutDb = -6f
        };
    }

    /// <summary>
    /// Creates a detector preset for full-range mix analysis.
    /// </summary>
    public static FrequencyCollisionDetector CreateFullRangePreset(int sampleRate = 44100)
    {
        return new FrequencyCollisionDetector(
            sampleRate,
            bandCount: 31,
            fftLength: 8192,
            minFrequency: 20f,
            maxFrequency: 20000f)
        {
            CollisionThreshold = 0.35f,
            MaskingThresholdDb = -48f,
            MaxSuggestedCutDb = -12f
        };
    }
}
