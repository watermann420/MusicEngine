//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Loop finder analyzer for detecting optimal loop points in audio with zero-crossing,
// waveform matching, spectral analysis, and BPM-aware loop finding.

using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.Dsp;

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Loop type for analysis.
/// </summary>
public enum LoopType
{
    /// <summary>Standard forward loop.</summary>
    Forward,
    /// <summary>Backward (reverse) loop.</summary>
    Backward,
    /// <summary>Ping-pong (bidirectional) loop.</summary>
    PingPong
}

/// <summary>
/// Loop category for different instrument types.
/// </summary>
public enum LoopCategory
{
    /// <summary>General purpose loop.</summary>
    General,
    /// <summary>Sustain loop for sample instruments (holds during note on).</summary>
    Sustain,
    /// <summary>Release loop for sample instruments (plays after note off).</summary>
    Release
}

/// <summary>
/// Direction mode for loop analysis.
/// </summary>
public enum LoopDirection
{
    /// <summary>Forward playback only.</summary>
    Forward,
    /// <summary>Backward playback only.</summary>
    Backward,
    /// <summary>Forward-backward alternating (ping-pong).</summary>
    PingPong
}

/// <summary>
/// Represents a candidate loop point with quality metrics.
/// </summary>
public class LoopPointCandidate
{
    /// <summary>Loop start position in samples.</summary>
    public long StartSample { get; set; }

    /// <summary>Loop end position in samples.</summary>
    public long EndSample { get; set; }

    /// <summary>Loop start position in seconds.</summary>
    public double StartTimeSeconds { get; set; }

    /// <summary>Loop end position in seconds.</summary>
    public double EndTimeSeconds { get; set; }

    /// <summary>Loop length in samples.</summary>
    public long LengthSamples => EndSample - StartSample;

    /// <summary>Loop length in seconds.</summary>
    public double LengthSeconds => EndTimeSeconds - StartTimeSeconds;

    /// <summary>Overall quality score (0.0 to 1.0, higher is better).</summary>
    public float QualityScore { get; set; }

    /// <summary>Zero-crossing match score at boundaries (0.0 to 1.0).</summary>
    public float ZeroCrossingScore { get; set; }

    /// <summary>Waveform similarity score at boundaries (0.0 to 1.0).</summary>
    public float WaveformMatchScore { get; set; }

    /// <summary>Spectral similarity score (0.0 to 1.0).</summary>
    public float SpectralMatchScore { get; set; }

    /// <summary>Pitch continuity score (0.0 to 1.0).</summary>
    public float PitchMatchScore { get; set; }

    /// <summary>Loop smoothness score based on amplitude continuity (0.0 to 1.0).</summary>
    public float SmoothnessScore { get; set; }

    /// <summary>Artifact likelihood score (0.0 = no artifacts, 1.0 = severe artifacts).</summary>
    public float ArtifactScore { get; set; }

    /// <summary>Whether this loop point is at a zero crossing.</summary>
    public bool IsAtZeroCrossing { get; set; }

    /// <summary>Whether this loop aligns with beat grid (if BPM is set).</summary>
    public bool IsOnBeat { get; set; }

    /// <summary>Suggested crossfade length in samples for seamless looping.</summary>
    public int SuggestedCrossfadeSamples { get; set; }

    /// <summary>Suggested crossfade length in milliseconds.</summary>
    public double SuggestedCrossfadeMs { get; set; }

    /// <summary>Loop type (forward, backward, ping-pong).</summary>
    public LoopType LoopType { get; set; } = LoopType.Forward;

    /// <summary>Loop category (general, sustain, release).</summary>
    public LoopCategory Category { get; set; } = LoopCategory.General;

    /// <summary>User-defined marker name for export.</summary>
    public string MarkerName { get; set; } = "";

    /// <summary>Additional notes or comments.</summary>
    public string Notes { get; set; } = "";
}

/// <summary>
/// Result of loop finding analysis.
/// </summary>
public class LoopFinderResult
{
    /// <summary>List of loop point candidates ranked by quality.</summary>
    public List<LoopPointCandidate> Candidates { get; set; } = new();

    /// <summary>The best overall loop point candidate (highest quality score).</summary>
    public LoopPointCandidate? BestCandidate => Candidates.FirstOrDefault();

    /// <summary>Detected existing loops in the audio (if any).</summary>
    public List<LoopPointCandidate> DetectedExistingLoops { get; set; } = new();

    /// <summary>Sustain loop candidates for sample instruments.</summary>
    public List<LoopPointCandidate> SustainLoopCandidates { get; set; } = new();

    /// <summary>Release loop candidates for sample instruments.</summary>
    public List<LoopPointCandidate> ReleaseLoopCandidates { get; set; } = new();

    /// <summary>Detected BPM of the audio (if tempo detection enabled).</summary>
    public double? DetectedBpm { get; set; }

    /// <summary>Sample rate of the analyzed audio.</summary>
    public int SampleRate { get; set; }

    /// <summary>Total length of the analyzed audio in samples.</summary>
    public long TotalSamples { get; set; }

    /// <summary>Total length of the analyzed audio in seconds.</summary>
    public double TotalSeconds { get; set; }

    /// <summary>Whether analysis was successful.</summary>
    public bool Success { get; set; }

    /// <summary>Error message if analysis failed.</summary>
    public string ErrorMessage { get; set; } = "";
}

/// <summary>
/// Exported loop marker for DAW integration.
/// </summary>
public class LoopMarker
{
    /// <summary>Marker name/label.</summary>
    public string Name { get; set; } = "";

    /// <summary>Position in samples.</summary>
    public long PositionSamples { get; set; }

    /// <summary>Position in seconds.</summary>
    public double PositionSeconds { get; set; }

    /// <summary>Marker type (LoopStart, LoopEnd, etc.).</summary>
    public string MarkerType { get; set; } = "";

    /// <summary>Associated loop index (for matching start/end pairs).</summary>
    public int LoopIndex { get; set; }

    /// <summary>Color hint for visualization (ARGB).</summary>
    public uint Color { get; set; } = 0xFF00FF00; // Default green
}

/// <summary>
/// Preview playback state for loop audition.
/// </summary>
public class LoopPreviewState
{
    /// <summary>Current playback position in samples.</summary>
    public long CurrentPositionSamples { get; set; }

    /// <summary>Loop start in samples.</summary>
    public long LoopStartSamples { get; set; }

    /// <summary>Loop end in samples.</summary>
    public long LoopEndSamples { get; set; }

    /// <summary>Whether preview is currently playing.</summary>
    public bool IsPlaying { get; set; }

    /// <summary>Loop direction mode.</summary>
    public LoopDirection Direction { get; set; } = LoopDirection.Forward;

    /// <summary>Whether currently playing in reverse (for ping-pong).</summary>
    public bool IsReversed { get; set; }

    /// <summary>Number of complete loop cycles played.</summary>
    public int LoopCount { get; set; }

    /// <summary>Crossfade amount being applied (0.0 to 1.0).</summary>
    public float CrossfadeAmount { get; set; }
}

/// <summary>
/// Loop finder analyzer for detecting optimal loop points in audio.
/// Supports zero-crossing detection, waveform matching, spectral analysis,
/// pitch-aware finding, BPM alignment, and multiple loop types.
/// </summary>
public class LoopFinder : IAnalyzer
{
    private readonly int _sampleRate;
    private readonly int _fftLength;
    private readonly Complex[] _fftBuffer1;
    private readonly Complex[] _fftBuffer2;
    private readonly float[] _window;
    private readonly object _lock = new();

    // Analysis parameters
    private long _minLoopLengthSamples;
    private long _maxLoopLengthSamples;
    private long _searchRegionStartSamples;
    private long _searchRegionEndSamples;
    private int _maxCandidates = 10;
    private float _qualityThreshold = 0.5f;

    // BPM settings
    private double? _bpm;
    private bool _snapToBeats;
    private int _beatsPerBar = 4;

    // Crossfade settings
    private int _defaultCrossfadeSamples = 1024;
    private int _maxCrossfadeSamples = 4096;

    // Preview state
    private LoopPreviewState? _previewState;
    private float[]? _previewBuffer;

    /// <summary>
    /// Gets the sample rate.
    /// </summary>
    public int SampleRate => _sampleRate;

    /// <summary>
    /// Gets or sets the minimum loop length in samples.
    /// </summary>
    public long MinLoopLengthSamples
    {
        get => _minLoopLengthSamples;
        set => _minLoopLengthSamples = Math.Max(1, value);
    }

    /// <summary>
    /// Gets or sets the minimum loop length in seconds.
    /// </summary>
    public double MinLoopLengthSeconds
    {
        get => (double)_minLoopLengthSamples / _sampleRate;
        set => _minLoopLengthSamples = (long)(value * _sampleRate);
    }

    /// <summary>
    /// Gets or sets the minimum loop length in milliseconds.
    /// </summary>
    public double MinLoopLengthMs
    {
        get => MinLoopLengthSeconds * 1000.0;
        set => MinLoopLengthSeconds = value / 1000.0;
    }

    /// <summary>
    /// Gets or sets the maximum loop length in samples.
    /// </summary>
    public long MaxLoopLengthSamples
    {
        get => _maxLoopLengthSamples;
        set => _maxLoopLengthSamples = Math.Max(1, value);
    }

    /// <summary>
    /// Gets or sets the maximum loop length in seconds.
    /// </summary>
    public double MaxLoopLengthSeconds
    {
        get => (double)_maxLoopLengthSamples / _sampleRate;
        set => _maxLoopLengthSamples = (long)(value * _sampleRate);
    }

    /// <summary>
    /// Gets or sets the maximum loop length in milliseconds.
    /// </summary>
    public double MaxLoopLengthMs
    {
        get => MaxLoopLengthSeconds * 1000.0;
        set => MaxLoopLengthSeconds = value / 1000.0;
    }

    /// <summary>
    /// Gets or sets the search region start in samples.
    /// </summary>
    public long SearchRegionStartSamples
    {
        get => _searchRegionStartSamples;
        set => _searchRegionStartSamples = Math.Max(0, value);
    }

    /// <summary>
    /// Gets or sets the search region start in seconds.
    /// </summary>
    public double SearchRegionStartSeconds
    {
        get => (double)_searchRegionStartSamples / _sampleRate;
        set => _searchRegionStartSamples = (long)(value * _sampleRate);
    }

    /// <summary>
    /// Gets or sets the search region end in samples.
    /// </summary>
    public long SearchRegionEndSamples
    {
        get => _searchRegionEndSamples;
        set => _searchRegionEndSamples = Math.Max(0, value);
    }

    /// <summary>
    /// Gets or sets the search region end in seconds.
    /// </summary>
    public double SearchRegionEndSeconds
    {
        get => (double)_searchRegionEndSamples / _sampleRate;
        set => _searchRegionEndSamples = (long)(value * _sampleRate);
    }

    /// <summary>
    /// Gets or sets the maximum number of loop candidates to return.
    /// </summary>
    public int MaxCandidates
    {
        get => _maxCandidates;
        set => _maxCandidates = Math.Max(1, value);
    }

    /// <summary>
    /// Gets or sets the minimum quality threshold for candidates (0.0 to 1.0).
    /// </summary>
    public float QualityThreshold
    {
        get => _qualityThreshold;
        set => _qualityThreshold = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Gets or sets the BPM for beat-aware loop finding.
    /// Set to null to disable BPM alignment.
    /// </summary>
    public double? Bpm
    {
        get => _bpm;
        set => _bpm = value.HasValue ? Math.Clamp(value.Value, 20, 999) : null;
    }

    /// <summary>
    /// Gets or sets whether to snap loop points to beat grid.
    /// </summary>
    public bool SnapToBeats
    {
        get => _snapToBeats;
        set => _snapToBeats = value;
    }

    /// <summary>
    /// Gets or sets the number of beats per bar for BPM alignment.
    /// </summary>
    public int BeatsPerBar
    {
        get => _beatsPerBar;
        set => _beatsPerBar = Math.Max(1, value);
    }

    /// <summary>
    /// Gets or sets the default crossfade length in samples.
    /// </summary>
    public int DefaultCrossfadeSamples
    {
        get => _defaultCrossfadeSamples;
        set => _defaultCrossfadeSamples = Math.Max(0, value);
    }

    /// <summary>
    /// Gets or sets the default crossfade length in milliseconds.
    /// </summary>
    public double DefaultCrossfadeMs
    {
        get => (double)_defaultCrossfadeSamples / _sampleRate * 1000.0;
        set => _defaultCrossfadeSamples = (int)(value / 1000.0 * _sampleRate);
    }

    /// <summary>
    /// Gets or sets the maximum crossfade length in samples.
    /// </summary>
    public int MaxCrossfadeSamples
    {
        get => _maxCrossfadeSamples;
        set => _maxCrossfadeSamples = Math.Max(0, value);
    }

    /// <summary>
    /// Gets the current preview state (null if not previewing).
    /// </summary>
    public LoopPreviewState? PreviewState => _previewState;

    /// <summary>
    /// Event raised when loop candidates are found.
    /// </summary>
    public event EventHandler<LoopFinderEventArgs>? LoopCandidatesFound;

    /// <summary>
    /// Event raised during preview playback.
    /// </summary>
    public event EventHandler<LoopPreviewEventArgs>? PreviewPositionChanged;

    /// <summary>
    /// Creates a new loop finder with the specified configuration.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate in Hz (default: 44100).</param>
    /// <param name="fftLength">FFT window size for spectral analysis (default: 2048).</param>
    public LoopFinder(int sampleRate = 44100, int fftLength = 2048)
    {
        if (!IsPowerOfTwo(fftLength))
            throw new ArgumentException("FFT length must be a power of two.", nameof(fftLength));

        _sampleRate = sampleRate;
        _fftLength = fftLength;
        _fftBuffer1 = new Complex[fftLength];
        _fftBuffer2 = new Complex[fftLength];
        _window = CreateHannWindow(fftLength);

        // Default loop length limits
        _minLoopLengthSamples = (long)(0.1 * sampleRate);   // 100ms minimum
        _maxLoopLengthSamples = (long)(60.0 * sampleRate);  // 60 seconds maximum
        _searchRegionStartSamples = 0;
        _searchRegionEndSamples = long.MaxValue;
    }

    /// <summary>
    /// Processes audio samples for real-time analysis (IAnalyzer interface).
    /// </summary>
    public void ProcessSamples(float[] samples, int offset, int count, int channels)
    {
        // Real-time analysis could detect loop points incrementally
        // For now, this is primarily used for buffer-based analysis
    }

    /// <summary>
    /// Resets the analyzer state.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _previewState = null;
            _previewBuffer = null;
            Array.Clear(_fftBuffer1, 0, _fftBuffer1.Length);
            Array.Clear(_fftBuffer2, 0, _fftBuffer2.Length);
        }
    }

    /// <summary>
    /// Analyzes audio buffer and finds optimal loop points.
    /// </summary>
    /// <param name="samples">Audio samples (mono).</param>
    /// <returns>Analysis result with loop point candidates.</returns>
    public LoopFinderResult FindLoopPoints(float[] samples)
    {
        return FindLoopPoints(samples, LoopCategory.General);
    }

    /// <summary>
    /// Analyzes audio buffer and finds optimal loop points for a specific category.
    /// </summary>
    /// <param name="samples">Audio samples (mono).</param>
    /// <param name="category">Loop category to search for.</param>
    /// <returns>Analysis result with loop point candidates.</returns>
    public LoopFinderResult FindLoopPoints(float[] samples, LoopCategory category)
    {
        var result = new LoopFinderResult
        {
            SampleRate = _sampleRate,
            TotalSamples = samples.Length,
            TotalSeconds = (double)samples.Length / _sampleRate
        };

        try
        {
            // Determine search region
            long searchStart = Math.Max(0, _searchRegionStartSamples);
            long searchEnd = Math.Min(samples.Length, _searchRegionEndSamples);

            if (searchEnd <= searchStart)
            {
                searchEnd = samples.Length;
            }

            // Find zero crossings in the search region
            var zeroCrossings = FindZeroCrossings(samples, searchStart, searchEnd);

            // Detect BPM if not provided and snap to beats is enabled
            if (_snapToBeats && !_bpm.HasValue)
            {
                var tempoDetector = new TempoDetector(_sampleRate);
                var tempoResult = tempoDetector.AnalyzeBuffer(samples, _sampleRate);
                if (tempoResult.Confidence > 0.5)
                {
                    result.DetectedBpm = tempoResult.DetectedBpm;
                    _bpm = tempoResult.DetectedBpm;
                }
            }

            // Generate loop point candidates
            var candidates = GenerateCandidates(samples, zeroCrossings, searchStart, searchEnd, category);

            // Score each candidate
            foreach (var candidate in candidates)
            {
                ScoreCandidate(samples, candidate);
            }

            // Filter by quality threshold and sort by score
            result.Candidates = candidates
                .Where(c => c.QualityScore >= _qualityThreshold)
                .OrderByDescending(c => c.QualityScore)
                .Take(_maxCandidates)
                .ToList();

            // Find sustain and release loops if requested
            if (category == LoopCategory.General)
            {
                result.SustainLoopCandidates = FindSustainLoops(samples, zeroCrossings);
                result.ReleaseLoopCandidates = FindReleaseLoops(samples, zeroCrossings);
            }

            // Detect existing loops (repeated patterns)
            result.DetectedExistingLoops = DetectExistingLoops(samples);

            result.Success = result.Candidates.Count > 0;
            if (!result.Success)
            {
                result.ErrorMessage = "No suitable loop points found with current settings.";
            }

            // Raise event
            LoopCandidatesFound?.Invoke(this, new LoopFinderEventArgs(result));
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Analysis failed: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Finds sustain loop points optimized for sample instruments.
    /// Sustain loops are typically in the stable portion of the sound after the attack.
    /// </summary>
    /// <param name="samples">Audio samples (mono).</param>
    /// <returns>List of sustain loop candidates.</returns>
    public List<LoopPointCandidate> FindSustainLoops(float[] samples)
    {
        var zeroCrossings = FindZeroCrossings(samples, 0, samples.Length);
        return FindSustainLoops(samples, zeroCrossings);
    }

    /// <summary>
    /// Finds release loop points for sample instruments.
    /// Release loops are typically in the decay portion after the sustain.
    /// </summary>
    /// <param name="samples">Audio samples (mono).</param>
    /// <returns>List of release loop candidates.</returns>
    public List<LoopPointCandidate> FindReleaseLoops(float[] samples)
    {
        var zeroCrossings = FindZeroCrossings(samples, 0, samples.Length);
        return FindReleaseLoops(samples, zeroCrossings);
    }

    /// <summary>
    /// Detects existing loop patterns in audio (repeated sections).
    /// </summary>
    /// <param name="samples">Audio samples (mono).</param>
    /// <returns>List of detected existing loops.</returns>
    public List<LoopPointCandidate> DetectExistingLoops(float[] samples)
    {
        var detected = new List<LoopPointCandidate>();

        // Use autocorrelation to find repeating patterns
        int analysisLength = Math.Min(samples.Length, _sampleRate * 10); // Analyze up to 10 seconds
        int hopSize = _fftLength / 4;
        int minLagSamples = (int)_minLoopLengthSamples;
        int maxLagSamples = Math.Min((int)_maxLoopLengthSamples, analysisLength / 2);

        if (maxLagSamples <= minLagSamples)
            return detected;

        // Calculate autocorrelation
        float[] autocorr = new float[maxLagSamples - minLagSamples];

        for (int lag = minLagSamples; lag < maxLagSamples; lag += hopSize)
        {
            float correlation = 0;
            float energy1 = 0;
            float energy2 = 0;

            int compareLength = Math.Min(analysisLength - lag, analysisLength / 2);
            for (int i = 0; i < compareLength; i++)
            {
                float s1 = samples[i];
                float s2 = i + lag < samples.Length ? samples[i + lag] : 0;
                correlation += s1 * s2;
                energy1 += s1 * s1;
                energy2 += s2 * s2;
            }

            float normFactor = (float)Math.Sqrt(energy1 * energy2);
            if (normFactor > 1e-10)
            {
                float normalizedCorr = correlation / normFactor;
                if (normalizedCorr > 0.8f) // Strong correlation threshold
                {
                    var candidate = new LoopPointCandidate
                    {
                        StartSample = 0,
                        EndSample = lag,
                        StartTimeSeconds = 0,
                        EndTimeSeconds = (double)lag / _sampleRate,
                        QualityScore = normalizedCorr,
                        WaveformMatchScore = normalizedCorr,
                        Category = LoopCategory.General,
                        MarkerName = $"Detected Loop {detected.Count + 1}",
                        Notes = "Auto-detected repeating pattern"
                    };

                    // Verify it's not a duplicate
                    bool isDuplicate = detected.Any(d =>
                        Math.Abs(d.EndSample - candidate.EndSample) < hopSize);

                    if (!isDuplicate)
                    {
                        detected.Add(candidate);
                    }
                }
            }
        }

        return detected.OrderByDescending(c => c.QualityScore).Take(5).ToList();
    }

    /// <summary>
    /// Analyzes loop smoothness for a specific loop point.
    /// </summary>
    /// <param name="samples">Audio samples (mono).</param>
    /// <param name="loopStart">Loop start position in samples.</param>
    /// <param name="loopEnd">Loop end position in samples.</param>
    /// <returns>Smoothness score (0.0 to 1.0).</returns>
    public float AnalyzeLoopSmoothness(float[] samples, long loopStart, long loopEnd)
    {
        if (loopStart < 0 || loopEnd > samples.Length || loopStart >= loopEnd)
            return 0f;

        // Analyze amplitude continuity at loop boundary
        int analysisWindow = Math.Min(256, (int)(loopEnd - loopStart) / 4);

        // Get samples around loop end
        float[] endRegion = new float[analysisWindow];
        for (int i = 0; i < analysisWindow; i++)
        {
            long pos = loopEnd - analysisWindow + i;
            endRegion[i] = pos >= 0 && pos < samples.Length ? samples[pos] : 0;
        }

        // Get samples around loop start
        float[] startRegion = new float[analysisWindow];
        for (int i = 0; i < analysisWindow; i++)
        {
            long pos = loopStart + i;
            startRegion[i] = pos >= 0 && pos < samples.Length ? samples[pos] : 0;
        }

        // Calculate derivative continuity
        float endDerivative = endRegion.Length > 1 ?
            endRegion[^1] - endRegion[^2] : 0;
        float startDerivative = startRegion.Length > 1 ?
            startRegion[1] - startRegion[0] : 0;

        float derivativeMatch = 1.0f - Math.Min(1.0f,
            Math.Abs(endDerivative - startDerivative) * 10);

        // Calculate amplitude match
        float endAmplitude = endRegion.Length > 0 ? endRegion[^1] : 0;
        float startAmplitude = startRegion.Length > 0 ? startRegion[0] : 0;

        float amplitudeMatch = 1.0f - Math.Min(1.0f,
            Math.Abs(endAmplitude - startAmplitude) * 5);

        // Calculate RMS match
        float endRms = CalculateRms(endRegion, 0, endRegion.Length);
        float startRms = CalculateRms(startRegion, 0, startRegion.Length);

        float rmsMatch = 1.0f - Math.Min(1.0f,
            Math.Abs(endRms - startRms) / Math.Max(endRms, 0.001f));

        return (derivativeMatch * 0.4f + amplitudeMatch * 0.3f + rmsMatch * 0.3f);
    }

    /// <summary>
    /// Detects potential artifacts at a loop point.
    /// </summary>
    /// <param name="samples">Audio samples (mono).</param>
    /// <param name="loopStart">Loop start position in samples.</param>
    /// <param name="loopEnd">Loop end position in samples.</param>
    /// <returns>Artifact score (0.0 = no artifacts, 1.0 = severe artifacts).</returns>
    public float DetectArtifacts(float[] samples, long loopStart, long loopEnd)
    {
        if (loopStart < 0 || loopEnd > samples.Length || loopStart >= loopEnd)
            return 1f;

        float artifactScore = 0f;

        // Check for discontinuity (click)
        float endSample = loopEnd > 0 && loopEnd <= samples.Length ?
            samples[loopEnd - 1] : 0;
        float startSample = loopStart >= 0 && loopStart < samples.Length ?
            samples[loopStart] : 0;

        float discontinuity = Math.Abs(endSample - startSample);
        artifactScore += Math.Min(1.0f, discontinuity * 2) * 0.4f;

        // Check for DC offset difference
        int analysisWindow = Math.Min(512, (int)(loopEnd - loopStart) / 4);
        float endDc = 0, startDc = 0;

        for (int i = 0; i < analysisWindow; i++)
        {
            long endPos = loopEnd - analysisWindow + i;
            long startPos = loopStart + i;

            if (endPos >= 0 && endPos < samples.Length)
                endDc += samples[endPos];
            if (startPos >= 0 && startPos < samples.Length)
                startDc += samples[startPos];
        }

        endDc /= analysisWindow;
        startDc /= analysisWindow;

        float dcDiff = Math.Abs(endDc - startDc);
        artifactScore += Math.Min(1.0f, dcDiff * 5) * 0.3f;

        // Check for phase discontinuity using spectral analysis
        float phaseScore = CalculatePhaseDiscontinuity(samples, loopStart, loopEnd);
        artifactScore += phaseScore * 0.3f;

        return Math.Min(1.0f, artifactScore);
    }

    /// <summary>
    /// Fine-tunes a loop point to the nearest optimal position.
    /// </summary>
    /// <param name="samples">Audio samples (mono).</param>
    /// <param name="candidate">Initial loop point candidate.</param>
    /// <param name="searchRangeSamples">Range to search around the initial points.</param>
    /// <returns>Fine-tuned loop point candidate.</returns>
    public LoopPointCandidate FineTuneLoopPoint(float[] samples, LoopPointCandidate candidate,
        int searchRangeSamples = 1024)
    {
        var bestCandidate = new LoopPointCandidate
        {
            StartSample = candidate.StartSample,
            EndSample = candidate.EndSample,
            Category = candidate.Category,
            LoopType = candidate.LoopType,
            MarkerName = candidate.MarkerName,
            Notes = candidate.Notes
        };

        float bestScore = 0;

        // Search around current start position
        long startMin = Math.Max(0, candidate.StartSample - searchRangeSamples);
        long startMax = Math.Min(candidate.EndSample - _minLoopLengthSamples,
            candidate.StartSample + searchRangeSamples);

        // Search around current end position
        long endMin = Math.Max(candidate.StartSample + _minLoopLengthSamples,
            candidate.EndSample - searchRangeSamples);
        long endMax = Math.Min(samples.Length, candidate.EndSample + searchRangeSamples);

        // Find zero crossings in search ranges
        var startCrossings = FindZeroCrossings(samples, startMin, startMax);
        var endCrossings = FindZeroCrossings(samples, endMin, endMax);

        // If no zero crossings found, use original positions
        if (startCrossings.Count == 0)
            startCrossings.Add(candidate.StartSample);
        if (endCrossings.Count == 0)
            endCrossings.Add(candidate.EndSample);

        // Test combinations
        foreach (var startPos in startCrossings)
        {
            foreach (var endPos in endCrossings)
            {
                if (endPos - startPos < _minLoopLengthSamples)
                    continue;
                if (endPos - startPos > _maxLoopLengthSamples)
                    continue;

                var testCandidate = new LoopPointCandidate
                {
                    StartSample = startPos,
                    EndSample = endPos,
                    Category = candidate.Category,
                    LoopType = candidate.LoopType
                };

                ScoreCandidate(samples, testCandidate);

                if (testCandidate.QualityScore > bestScore)
                {
                    bestScore = testCandidate.QualityScore;
                    bestCandidate = testCandidate;
                    bestCandidate.MarkerName = candidate.MarkerName;
                    bestCandidate.Notes = candidate.Notes;
                }
            }
        }

        // Update time values
        bestCandidate.StartTimeSeconds = (double)bestCandidate.StartSample / _sampleRate;
        bestCandidate.EndTimeSeconds = (double)bestCandidate.EndSample / _sampleRate;

        return bestCandidate;
    }

    /// <summary>
    /// Starts loop preview playback.
    /// </summary>
    /// <param name="samples">Audio samples (mono).</param>
    /// <param name="candidate">Loop point to preview.</param>
    /// <param name="direction">Playback direction mode.</param>
    public void StartPreview(float[] samples, LoopPointCandidate candidate,
        LoopDirection direction = LoopDirection.Forward)
    {
        lock (_lock)
        {
            _previewBuffer = samples;
            _previewState = new LoopPreviewState
            {
                LoopStartSamples = candidate.StartSample,
                LoopEndSamples = candidate.EndSample,
                CurrentPositionSamples = candidate.StartSample,
                IsPlaying = true,
                Direction = direction,
                IsReversed = false,
                LoopCount = 0
            };
        }
    }

    /// <summary>
    /// Stops loop preview playback.
    /// </summary>
    public void StopPreview()
    {
        lock (_lock)
        {
            if (_previewState != null)
            {
                _previewState.IsPlaying = false;
            }
        }
    }

    /// <summary>
    /// Gets the next preview samples for playback.
    /// </summary>
    /// <param name="outputBuffer">Output buffer to fill.</param>
    /// <param name="count">Number of samples to generate.</param>
    /// <returns>Number of samples written.</returns>
    public int GetPreviewSamples(float[] outputBuffer, int count)
    {
        lock (_lock)
        {
            if (_previewState == null || !_previewState.IsPlaying || _previewBuffer == null)
            {
                Array.Clear(outputBuffer, 0, count);
                return 0;
            }

            int written = 0;
            int crossfadeSamples = _defaultCrossfadeSamples;

            while (written < count)
            {
                long pos = _previewState.CurrentPositionSamples;
                long loopStart = _previewState.LoopStartSamples;
                long loopEnd = _previewState.LoopEndSamples;
                long loopLength = loopEnd - loopStart;

                if (loopLength <= 0)
                    break;

                // Get sample value
                float sample = 0;
                if (pos >= 0 && pos < _previewBuffer.Length)
                {
                    sample = _previewBuffer[pos];
                }

                // Apply crossfade at loop boundary
                long distanceFromEnd = loopEnd - pos;
                if (distanceFromEnd >= 0 && distanceFromEnd < crossfadeSamples)
                {
                    float fadeOut = (float)distanceFromEnd / crossfadeSamples;
                    float fadeIn = 1.0f - fadeOut;

                    // Get corresponding sample from loop start
                    long crossfadePos = loopStart + (crossfadeSamples - distanceFromEnd);
                    float crossfadeSample = 0;
                    if (crossfadePos >= 0 && crossfadePos < _previewBuffer.Length)
                    {
                        crossfadeSample = _previewBuffer[crossfadePos];
                    }

                    sample = sample * fadeOut + crossfadeSample * fadeIn;
                    _previewState.CrossfadeAmount = fadeIn;
                }
                else
                {
                    _previewState.CrossfadeAmount = 0;
                }

                outputBuffer[written++] = sample;

                // Advance position based on direction
                switch (_previewState.Direction)
                {
                    case LoopDirection.Forward:
                        _previewState.CurrentPositionSamples++;
                        if (_previewState.CurrentPositionSamples >= loopEnd)
                        {
                            _previewState.CurrentPositionSamples = loopStart;
                            _previewState.LoopCount++;
                        }
                        break;

                    case LoopDirection.Backward:
                        _previewState.CurrentPositionSamples--;
                        if (_previewState.CurrentPositionSamples < loopStart)
                        {
                            _previewState.CurrentPositionSamples = loopEnd - 1;
                            _previewState.LoopCount++;
                        }
                        break;

                    case LoopDirection.PingPong:
                        if (!_previewState.IsReversed)
                        {
                            _previewState.CurrentPositionSamples++;
                            if (_previewState.CurrentPositionSamples >= loopEnd)
                            {
                                _previewState.CurrentPositionSamples = loopEnd - 1;
                                _previewState.IsReversed = true;
                            }
                        }
                        else
                        {
                            _previewState.CurrentPositionSamples--;
                            if (_previewState.CurrentPositionSamples < loopStart)
                            {
                                _previewState.CurrentPositionSamples = loopStart;
                                _previewState.IsReversed = false;
                                _previewState.LoopCount++;
                            }
                        }
                        break;
                }
            }

            // Raise position changed event
            PreviewPositionChanged?.Invoke(this, new LoopPreviewEventArgs(_previewState));

            return written;
        }
    }

    /// <summary>
    /// Exports loop points as markers.
    /// </summary>
    /// <param name="candidates">Loop point candidates to export.</param>
    /// <param name="prefix">Prefix for marker names.</param>
    /// <returns>List of loop markers.</returns>
    public List<LoopMarker> ExportAsMarkers(IEnumerable<LoopPointCandidate> candidates,
        string prefix = "Loop")
    {
        var markers = new List<LoopMarker>();
        int index = 0;

        foreach (var candidate in candidates)
        {
            // Start marker
            markers.Add(new LoopMarker
            {
                Name = string.IsNullOrEmpty(candidate.MarkerName) ?
                    $"{prefix} {index + 1} Start" : $"{candidate.MarkerName} Start",
                PositionSamples = candidate.StartSample,
                PositionSeconds = candidate.StartTimeSeconds,
                MarkerType = "LoopStart",
                LoopIndex = index,
                Color = GetMarkerColor(candidate.Category, true)
            });

            // End marker
            markers.Add(new LoopMarker
            {
                Name = string.IsNullOrEmpty(candidate.MarkerName) ?
                    $"{prefix} {index + 1} End" : $"{candidate.MarkerName} End",
                PositionSamples = candidate.EndSample,
                PositionSeconds = candidate.EndTimeSeconds,
                MarkerType = "LoopEnd",
                LoopIndex = index,
                Color = GetMarkerColor(candidate.Category, false)
            });

            index++;
        }

        return markers;
    }

    /// <summary>
    /// Analyzes loop for forward/backward/ping-pong playback quality.
    /// </summary>
    /// <param name="samples">Audio samples (mono).</param>
    /// <param name="candidate">Loop point candidate.</param>
    /// <returns>Dictionary of direction to quality score.</returns>
    public Dictionary<LoopDirection, float> AnalyzeLoopDirections(float[] samples,
        LoopPointCandidate candidate)
    {
        var scores = new Dictionary<LoopDirection, float>();

        // Forward analysis - already calculated in main score
        scores[LoopDirection.Forward] = candidate.QualityScore;

        // Backward analysis
        float backwardScore = AnalyzeBackwardLoop(samples, candidate.StartSample, candidate.EndSample);
        scores[LoopDirection.Backward] = backwardScore;

        // Ping-pong analysis
        float pingPongScore = AnalyzePingPongLoop(samples, candidate.StartSample, candidate.EndSample);
        scores[LoopDirection.PingPong] = pingPongScore;

        return scores;
    }

    #region Private Methods

    private List<long> FindZeroCrossings(float[] samples, long start, long end)
    {
        var crossings = new List<long>();
        end = Math.Min(end, samples.Length - 1);

        for (long i = Math.Max(1, start); i < end; i++)
        {
            // Check for zero crossing (sign change)
            if ((samples[i - 1] >= 0 && samples[i] < 0) ||
                (samples[i - 1] < 0 && samples[i] >= 0))
            {
                crossings.Add(i);
            }
        }

        return crossings;
    }

    private List<LoopPointCandidate> GenerateCandidates(float[] samples,
        List<long> zeroCrossings, long searchStart, long searchEnd, LoopCategory category)
    {
        var candidates = new List<LoopPointCandidate>();

        // Generate candidates based on zero crossings and BPM grid
        int step = Math.Max(1, zeroCrossings.Count / 100); // Limit search complexity

        for (int i = 0; i < zeroCrossings.Count; i += step)
        {
            for (int j = i + 1; j < zeroCrossings.Count; j += step)
            {
                long startPos = zeroCrossings[i];
                long endPos = zeroCrossings[j];
                long length = endPos - startPos;

                // Check length constraints
                if (length < _minLoopLengthSamples)
                    continue;
                if (length > _maxLoopLengthSamples)
                    break;

                // Check if on beat grid (if BPM is set)
                bool isOnBeat = false;
                if (_bpm.HasValue && _snapToBeats)
                {
                    double beatLengthSamples = 60.0 / _bpm.Value * _sampleRate;
                    double lengthInBeats = length / beatLengthSamples;
                    isOnBeat = Math.Abs(lengthInBeats - Math.Round(lengthInBeats)) < 0.1;

                    // Skip non-beat-aligned loops if strict snapping
                    if (!isOnBeat)
                        continue;
                }

                var candidate = new LoopPointCandidate
                {
                    StartSample = startPos,
                    EndSample = endPos,
                    StartTimeSeconds = (double)startPos / _sampleRate,
                    EndTimeSeconds = (double)endPos / _sampleRate,
                    IsAtZeroCrossing = true,
                    IsOnBeat = isOnBeat,
                    Category = category
                };

                candidates.Add(candidate);
            }
        }

        // Add some non-zero-crossing candidates if not enough found
        if (candidates.Count < _maxCandidates && !_snapToBeats)
        {
            long stepSize = (searchEnd - searchStart) / (_maxCandidates * 2);
            stepSize = Math.Max(stepSize, _minLoopLengthSamples / 10);

            for (long startPos = searchStart; startPos < searchEnd && candidates.Count < _maxCandidates * 10;
                startPos += stepSize)
            {
                for (long length = _minLoopLengthSamples; length <= _maxLoopLengthSamples &&
                    startPos + length <= searchEnd; length += stepSize)
                {
                    var candidate = new LoopPointCandidate
                    {
                        StartSample = startPos,
                        EndSample = startPos + length,
                        StartTimeSeconds = (double)startPos / _sampleRate,
                        EndTimeSeconds = (double)(startPos + length) / _sampleRate,
                        IsAtZeroCrossing = false,
                        IsOnBeat = false,
                        Category = category
                    };

                    candidates.Add(candidate);
                }
            }
        }

        return candidates;
    }

    private void ScoreCandidate(float[] samples, LoopPointCandidate candidate)
    {
        // Zero-crossing score
        candidate.ZeroCrossingScore = candidate.IsAtZeroCrossing ? 1.0f :
            CalculateZeroCrossingScore(samples, candidate.StartSample, candidate.EndSample);

        // Waveform match score (correlation at boundaries)
        candidate.WaveformMatchScore = CalculateWaveformMatchScore(samples,
            candidate.StartSample, candidate.EndSample);

        // Spectral match score
        candidate.SpectralMatchScore = CalculateSpectralMatchScore(samples,
            candidate.StartSample, candidate.EndSample);

        // Pitch match score
        candidate.PitchMatchScore = CalculatePitchMatchScore(samples,
            candidate.StartSample, candidate.EndSample);

        // Smoothness score
        candidate.SmoothnessScore = AnalyzeLoopSmoothness(samples,
            candidate.StartSample, candidate.EndSample);

        // Artifact score
        candidate.ArtifactScore = DetectArtifacts(samples,
            candidate.StartSample, candidate.EndSample);

        // Calculate suggested crossfade
        candidate.SuggestedCrossfadeSamples = CalculateSuggestedCrossfade(samples,
            candidate.StartSample, candidate.EndSample);
        candidate.SuggestedCrossfadeMs =
            (double)candidate.SuggestedCrossfadeSamples / _sampleRate * 1000.0;

        // Calculate overall quality score
        float weightedScore =
            candidate.ZeroCrossingScore * 0.15f +
            candidate.WaveformMatchScore * 0.25f +
            candidate.SpectralMatchScore * 0.20f +
            candidate.PitchMatchScore * 0.15f +
            candidate.SmoothnessScore * 0.15f +
            (1.0f - candidate.ArtifactScore) * 0.10f;

        // Bonus for beat alignment
        if (candidate.IsOnBeat)
        {
            weightedScore *= 1.1f;
        }

        candidate.QualityScore = Math.Clamp(weightedScore, 0f, 1f);
    }

    private float CalculateZeroCrossingScore(float[] samples, long startSample, long endSample)
    {
        if (startSample < 0 || endSample > samples.Length)
            return 0f;

        // Check how close start and end are to zero crossings
        float startSampleValue = startSample < samples.Length ? Math.Abs(samples[startSample]) : 1f;
        float endSampleValue = endSample > 0 && endSample <= samples.Length ?
            Math.Abs(samples[endSample - 1]) : 1f;

        float startScore = 1.0f - Math.Min(1.0f, startSampleValue * 2);
        float endScore = 1.0f - Math.Min(1.0f, endSampleValue * 2);

        return (startScore + endScore) / 2;
    }

    private float CalculateWaveformMatchScore(float[] samples, long startSample, long endSample)
    {
        if (startSample < 0 || endSample > samples.Length)
            return 0f;

        int windowSize = Math.Min(256, (int)(endSample - startSample) / 4);
        if (windowSize < 16)
            return 0f;

        float correlation = 0;
        float energy1 = 0;
        float energy2 = 0;

        for (int i = 0; i < windowSize; i++)
        {
            long pos1 = endSample - windowSize + i;
            long pos2 = startSample + i;

            if (pos1 < 0 || pos1 >= samples.Length || pos2 < 0 || pos2 >= samples.Length)
                continue;

            float s1 = samples[pos1];
            float s2 = samples[pos2];

            correlation += s1 * s2;
            energy1 += s1 * s1;
            energy2 += s2 * s2;
        }

        float normFactor = (float)Math.Sqrt(energy1 * energy2);
        if (normFactor < 1e-10)
            return 0f;

        return Math.Clamp(correlation / normFactor, 0f, 1f);
    }

    private float CalculateSpectralMatchScore(float[] samples, long startSample, long endSample)
    {
        if (startSample < 0 || endSample > samples.Length)
            return 0f;

        // Get spectral content at loop boundaries
        int fftSize = Math.Min(_fftLength, (int)(endSample - startSample) / 2);
        if (fftSize < 64)
            return 0.5f; // Default for very short loops

        // Get spectrum at end
        var endSpectrum = CalculateSpectrum(samples, endSample - fftSize, fftSize);

        // Get spectrum at start
        var startSpectrum = CalculateSpectrum(samples, startSample, fftSize);

        if (endSpectrum == null || startSpectrum == null)
            return 0.5f;

        // Calculate spectral distance
        float distance = 0;
        float maxMag = 0;

        for (int i = 0; i < endSpectrum.Length && i < startSpectrum.Length; i++)
        {
            distance += Math.Abs(endSpectrum[i] - startSpectrum[i]);
            maxMag = Math.Max(maxMag, Math.Max(endSpectrum[i], startSpectrum[i]));
        }

        if (maxMag < 1e-10)
            return 0.5f;

        float normalizedDistance = distance / (endSpectrum.Length * maxMag);
        return Math.Clamp(1.0f - normalizedDistance, 0f, 1f);
    }

    private float[]? CalculateSpectrum(float[] samples, long startPos, int length)
    {
        if (startPos < 0 || startPos + length > samples.Length)
            return null;

        int fftSize = 1;
        while (fftSize < length)
            fftSize *= 2;

        var fftBuffer = new Complex[fftSize];

        for (int i = 0; i < fftSize; i++)
        {
            if (i < length && startPos + i < samples.Length)
            {
                float window = (float)(0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (length - 1))));
                fftBuffer[i].X = samples[startPos + i] * window;
            }
            fftBuffer[i].Y = 0;
        }

        int m = (int)Math.Log(fftSize, 2.0);
        FastFourierTransform.FFT(true, m, fftBuffer);

        float[] magnitudes = new float[fftSize / 2];
        for (int i = 0; i < magnitudes.Length; i++)
        {
            magnitudes[i] = (float)Math.Sqrt(
                fftBuffer[i].X * fftBuffer[i].X +
                fftBuffer[i].Y * fftBuffer[i].Y);
        }

        return magnitudes;
    }

    private float CalculatePitchMatchScore(float[] samples, long startSample, long endSample)
    {
        if (startSample < 0 || endSample > samples.Length)
            return 0f;

        // Estimate pitch at boundaries using autocorrelation
        int analysisSize = Math.Min(2048, (int)(endSample - startSample) / 2);
        if (analysisSize < 256)
            return 0.5f;

        float endPitch = EstimatePitch(samples, endSample - analysisSize, analysisSize);
        float startPitch = EstimatePitch(samples, startSample, analysisSize);

        if (endPitch < 20 || startPitch < 20)
            return 0.5f; // Insufficient pitch information

        // Calculate pitch ratio (cents)
        float pitchRatio = startPitch / endPitch;
        float cents = (float)(1200 * Math.Log(pitchRatio) / Math.Log(2));

        // Score based on pitch deviation (within 50 cents is good)
        float score = 1.0f - Math.Min(1.0f, Math.Abs(cents) / 100);
        return score;
    }

    private float EstimatePitch(float[] samples, long startPos, int length)
    {
        if (startPos < 0 || startPos + length > samples.Length)
            return 0f;

        // Simple autocorrelation-based pitch detection
        int minLag = _sampleRate / 1000; // 1000 Hz max
        int maxLag = _sampleRate / 50;   // 50 Hz min

        maxLag = Math.Min(maxLag, length / 2);
        minLag = Math.Min(minLag, maxLag);

        float maxCorr = 0;
        int bestLag = minLag;

        for (int lag = minLag; lag < maxLag; lag++)
        {
            float corr = 0;
            float energy = 0;

            for (int i = 0; i < length - lag; i++)
            {
                long pos1 = startPos + i;
                long pos2 = startPos + i + lag;

                if (pos1 >= samples.Length || pos2 >= samples.Length)
                    break;

                corr += samples[pos1] * samples[pos2];
                energy += samples[pos1] * samples[pos1];
            }

            if (energy > 1e-10)
            {
                corr /= energy;
                if (corr > maxCorr)
                {
                    maxCorr = corr;
                    bestLag = lag;
                }
            }
        }

        if (maxCorr < 0.5f)
            return 0f; // Not confident

        return (float)_sampleRate / bestLag;
    }

    private float CalculatePhaseDiscontinuity(float[] samples, long startSample, long endSample)
    {
        // Calculate phase difference at loop boundary
        int analysisSize = Math.Min(_fftLength, (int)(endSample - startSample) / 2);
        if (analysisSize < 64)
            return 0f;

        var endSpectrum = new Complex[analysisSize];
        var startSpectrum = new Complex[analysisSize];

        for (int i = 0; i < analysisSize; i++)
        {
            long endPos = endSample - analysisSize + i;
            long startPos = startSample + i;

            float window = (float)(0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (analysisSize - 1))));

            if (endPos >= 0 && endPos < samples.Length)
                endSpectrum[i].X = samples[endPos] * window;
            if (startPos >= 0 && startPos < samples.Length)
                startSpectrum[i].X = samples[startPos] * window;
        }

        int m = (int)Math.Log(analysisSize, 2.0);
        if (m < 1)
            return 0f;

        // Pad to power of 2 if needed
        int fftSize = 1 << m;
        if (fftSize > analysisSize)
        {
            var paddedEnd = new Complex[fftSize];
            var paddedStart = new Complex[fftSize];
            Array.Copy(endSpectrum, paddedEnd, analysisSize);
            Array.Copy(startSpectrum, paddedStart, analysisSize);
            endSpectrum = paddedEnd;
            startSpectrum = paddedStart;
        }

        FastFourierTransform.FFT(true, m, endSpectrum);
        FastFourierTransform.FFT(true, m, startSpectrum);

        // Calculate average phase difference
        float totalPhaseDiff = 0;
        int count = 0;

        for (int i = 1; i < fftSize / 2; i++)
        {
            float endPhase = (float)Math.Atan2(endSpectrum[i].Y, endSpectrum[i].X);
            float startPhase = (float)Math.Atan2(startSpectrum[i].Y, startSpectrum[i].X);

            float phaseDiff = Math.Abs(endPhase - startPhase);
            if (phaseDiff > Math.PI)
                phaseDiff = (float)(2 * Math.PI - phaseDiff);

            float magnitude = (float)Math.Sqrt(
                endSpectrum[i].X * endSpectrum[i].X +
                endSpectrum[i].Y * endSpectrum[i].Y);

            if (magnitude > 0.01f)
            {
                totalPhaseDiff += phaseDiff * magnitude;
                count++;
            }
        }

        if (count == 0)
            return 0f;

        return Math.Min(1.0f, totalPhaseDiff / count / (float)Math.PI);
    }

    private int CalculateSuggestedCrossfade(float[] samples, long startSample, long endSample)
    {
        // Calculate optimal crossfade based on loop characteristics
        float artifactScore = DetectArtifacts(samples, startSample, endSample);

        // Base crossfade on artifact severity
        int baseCrossfade = _defaultCrossfadeSamples;

        if (artifactScore < 0.1f)
        {
            // Very clean loop, minimal crossfade needed
            baseCrossfade = Math.Max(64, _defaultCrossfadeSamples / 4);
        }
        else if (artifactScore < 0.3f)
        {
            // Good loop, small crossfade
            baseCrossfade = Math.Max(256, _defaultCrossfadeSamples / 2);
        }
        else if (artifactScore > 0.6f)
        {
            // Problematic loop, larger crossfade
            baseCrossfade = Math.Min(_maxCrossfadeSamples, _defaultCrossfadeSamples * 2);
        }

        // Ensure crossfade doesn't exceed loop length / 4
        int maxAllowed = (int)(endSample - startSample) / 4;
        return Math.Min(baseCrossfade, maxAllowed);
    }

    private List<LoopPointCandidate> FindSustainLoops(float[] samples, List<long> zeroCrossings)
    {
        var candidates = new List<LoopPointCandidate>();

        // Find attack end (where amplitude stabilizes)
        int attackEndSample = FindAttackEnd(samples);

        // Find decay start (where amplitude starts decreasing significantly)
        int decayStartSample = FindDecayStart(samples, attackEndSample);

        if (decayStartSample <= attackEndSample)
            return candidates;

        // Search for sustain loops in the stable region
        long sustainStart = attackEndSample;
        long sustainEnd = decayStartSample;

        var sustainCrossings = zeroCrossings
            .Where(z => z >= sustainStart && z <= sustainEnd)
            .ToList();

        int step = Math.Max(1, sustainCrossings.Count / 50);

        for (int i = 0; i < sustainCrossings.Count; i += step)
        {
            for (int j = i + 1; j < sustainCrossings.Count; j += step)
            {
                long startPos = sustainCrossings[i];
                long endPos = sustainCrossings[j];
                long length = endPos - startPos;

                if (length < _minLoopLengthSamples / 2) // Sustain loops can be shorter
                    continue;
                if (length > (sustainEnd - sustainStart) / 2)
                    break;

                var candidate = new LoopPointCandidate
                {
                    StartSample = startPos,
                    EndSample = endPos,
                    StartTimeSeconds = (double)startPos / _sampleRate,
                    EndTimeSeconds = (double)endPos / _sampleRate,
                    IsAtZeroCrossing = true,
                    Category = LoopCategory.Sustain,
                    MarkerName = $"Sustain Loop {candidates.Count + 1}"
                };

                ScoreCandidate(samples, candidate);

                if (candidate.QualityScore >= _qualityThreshold)
                {
                    candidates.Add(candidate);
                }
            }
        }

        return candidates
            .OrderByDescending(c => c.QualityScore)
            .Take(_maxCandidates / 2)
            .ToList();
    }

    private List<LoopPointCandidate> FindReleaseLoops(float[] samples, List<long> zeroCrossings)
    {
        var candidates = new List<LoopPointCandidate>();

        // Find decay region
        int decayStartSample = FindDecayStart(samples, 0);

        if (decayStartSample >= samples.Length - _minLoopLengthSamples)
            return candidates;

        // Search for release loops in the decay region
        var releaseCrossings = zeroCrossings
            .Where(z => z >= decayStartSample)
            .ToList();

        int step = Math.Max(1, releaseCrossings.Count / 30);

        for (int i = 0; i < releaseCrossings.Count; i += step)
        {
            for (int j = i + 1; j < releaseCrossings.Count; j += step)
            {
                long startPos = releaseCrossings[i];
                long endPos = releaseCrossings[j];
                long length = endPos - startPos;

                if (length < _minLoopLengthSamples / 4) // Release loops can be shorter
                    continue;
                if (length > _maxLoopLengthSamples / 2)
                    break;

                var candidate = new LoopPointCandidate
                {
                    StartSample = startPos,
                    EndSample = endPos,
                    StartTimeSeconds = (double)startPos / _sampleRate,
                    EndTimeSeconds = (double)endPos / _sampleRate,
                    IsAtZeroCrossing = true,
                    Category = LoopCategory.Release,
                    MarkerName = $"Release Loop {candidates.Count + 1}"
                };

                ScoreCandidate(samples, candidate);

                if (candidate.QualityScore >= _qualityThreshold * 0.8f) // Slightly lower threshold
                {
                    candidates.Add(candidate);
                }
            }
        }

        return candidates
            .OrderByDescending(c => c.QualityScore)
            .Take(_maxCandidates / 4)
            .ToList();
    }

    private int FindAttackEnd(float[] samples)
    {
        // Find where amplitude reaches and stabilizes at peak
        int windowSize = _sampleRate / 100; // 10ms window
        float maxRms = 0;
        int maxRmsPos = 0;

        // Find maximum RMS position
        for (int i = 0; i < samples.Length - windowSize; i += windowSize / 2)
        {
            float rms = CalculateRms(samples, i, windowSize);
            if (rms > maxRms)
            {
                maxRms = rms;
                maxRmsPos = i;
            }
        }

        // Find where we first reach 90% of max
        float threshold = maxRms * 0.9f;
        for (int i = 0; i < maxRmsPos; i += windowSize / 4)
        {
            float rms = CalculateRms(samples, i, windowSize);
            if (rms >= threshold)
            {
                return i + windowSize;
            }
        }

        return maxRmsPos;
    }

    private int FindDecayStart(float[] samples, int afterSample)
    {
        int windowSize = _sampleRate / 50; // 20ms window
        float previousRms = 0;
        int decayCount = 0;

        for (int i = afterSample; i < samples.Length - windowSize; i += windowSize / 2)
        {
            float rms = CalculateRms(samples, i, windowSize);

            if (rms < previousRms * 0.95f) // 5% decrease
            {
                decayCount++;
                if (decayCount > 5) // Consistent decay
                {
                    return i - windowSize * 2;
                }
            }
            else
            {
                decayCount = 0;
            }

            previousRms = rms;
        }

        return samples.Length * 3 / 4; // Default to 75% if no decay found
    }

    private float CalculateRms(float[] samples, int start, int length)
    {
        float sum = 0;
        int count = 0;

        for (int i = start; i < start + length && i < samples.Length; i++)
        {
            sum += samples[i] * samples[i];
            count++;
        }

        return count > 0 ? (float)Math.Sqrt(sum / count) : 0;
    }

    private float AnalyzeBackwardLoop(float[] samples, long startSample, long endSample)
    {
        // For backward loops, check if reversed playback creates smooth transitions
        if (startSample < 0 || endSample > samples.Length)
            return 0f;

        // Check smoothness at both boundaries (end-to-start when reversed)
        float endScore = 1.0f - Math.Min(1.0f,
            Math.Abs(samples[Math.Min(endSample - 1, samples.Length - 1)] -
                    samples[Math.Max(endSample - 2, 0)]) * 5);

        float startScore = 1.0f - Math.Min(1.0f,
            Math.Abs(samples[Math.Min(startSample + 1, samples.Length - 1)] -
                    samples[startSample]) * 5);

        return (endScore + startScore) / 2;
    }

    private float AnalyzePingPongLoop(float[] samples, long startSample, long endSample)
    {
        // For ping-pong, both boundaries need to be smooth for reversals
        if (startSample < 0 || endSample > samples.Length)
            return 0f;

        // Check derivative at boundaries (should approach zero for smooth reversals)
        float startDerivative = 0;
        float endDerivative = 0;

        if (startSample + 1 < samples.Length)
            startDerivative = Math.Abs(samples[startSample + 1] - samples[startSample]);
        if (endSample > 1 && endSample <= samples.Length)
            endDerivative = Math.Abs(samples[endSample - 1] - samples[endSample - 2]);

        float startScore = 1.0f - Math.Min(1.0f, startDerivative * 3);
        float endScore = 1.0f - Math.Min(1.0f, endDerivative * 3);

        return (startScore + endScore) / 2;
    }

    private uint GetMarkerColor(LoopCategory category, bool isStart)
    {
        return category switch
        {
            LoopCategory.Sustain => isStart ? 0xFF00FF00u : 0xFF00CC00u, // Green
            LoopCategory.Release => isStart ? 0xFFFF9900u : 0xFFCC7700u, // Orange
            _ => isStart ? 0xFF0099FFu : 0xFF0077CCu // Blue (General)
        };
    }

    private static float[] CreateHannWindow(int length)
    {
        var window = new float[length];
        for (int i = 0; i < length; i++)
        {
            window[i] = (float)(0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (length - 1))));
        }
        return window;
    }

    private static bool IsPowerOfTwo(int n) => n > 0 && (n & (n - 1)) == 0;

    #endregion
}

/// <summary>
/// Event arguments for loop finder results.
/// </summary>
public class LoopFinderEventArgs : EventArgs
{
    /// <summary>The loop finder result.</summary>
    public LoopFinderResult Result { get; }

    public LoopFinderEventArgs(LoopFinderResult result)
    {
        Result = result;
    }
}

/// <summary>
/// Event arguments for loop preview position updates.
/// </summary>
public class LoopPreviewEventArgs : EventArgs
{
    /// <summary>Current preview state.</summary>
    public LoopPreviewState State { get; }

    public LoopPreviewEventArgs(LoopPreviewState state)
    {
        State = state;
    }
}
