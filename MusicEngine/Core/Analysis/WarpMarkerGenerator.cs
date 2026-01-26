// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio analysis component.

using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Generates warp markers automatically from transient detection results and tempo analysis.
/// Creates a grid of beat-aligned markers for time-stretching operations.
/// </summary>
public class WarpMarkerGenerator
{
    private readonly TempoDetector _tempoDetector;
    private readonly TransientDetector _transientDetector;

    /// <summary>
    /// Gets or sets the time signature numerator (beats per bar, default: 4).
    /// </summary>
    public int BeatsPerBar { get; set; } = 4;

    /// <summary>
    /// Gets or sets the tolerance for snapping transients to beat grid in milliseconds.
    /// </summary>
    public double SnapToleranceMs { get; set; } = 50.0;

    /// <summary>
    /// Gets or sets whether to prefer transients near expected beat positions.
    /// </summary>
    public bool SnapToTransients { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum confidence threshold for including auto-generated markers.
    /// </summary>
    public double MinimumConfidence { get; set; } = 0.3;

    /// <summary>
    /// Creates a new warp marker generator.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate (default: 44100).</param>
    public WarpMarkerGenerator(int sampleRate = 44100)
    {
        _tempoDetector = new TempoDetector(sampleRate);
        _transientDetector = new TransientDetector(sampleRate);
    }

    /// <summary>
    /// Creates a new warp marker generator with existing detectors.
    /// </summary>
    /// <param name="tempoDetector">Tempo detector instance.</param>
    /// <param name="transientDetector">Transient detector instance.</param>
    public WarpMarkerGenerator(TempoDetector tempoDetector, TransientDetector transientDetector)
    {
        _tempoDetector = tempoDetector ?? throw new ArgumentNullException(nameof(tempoDetector));
        _transientDetector = transientDetector ?? throw new ArgumentNullException(nameof(transientDetector));
    }

    /// <summary>
    /// Generates warp markers from an audio buffer.
    /// </summary>
    /// <param name="samples">Audio samples (mono).</param>
    /// <param name="sampleRate">Sample rate of the audio.</param>
    /// <returns>List of generated warp markers.</returns>
    public List<WarpMarker> GenerateMarkers(float[] samples, int sampleRate)
    {
        // Detect tempo
        var tempoResult = _tempoDetector.AnalyzeBuffer(samples, sampleRate);

        // Detect transients
        var transients = _transientDetector.AnalyzeBuffer(samples, sampleRate);

        // Generate markers
        return GenerateMarkersFromAnalysis(tempoResult, transients, samples.Length / (double)sampleRate);
    }

    /// <summary>
    /// Generates warp markers from pre-computed analysis results.
    /// </summary>
    /// <param name="tempoResult">Beat analysis result containing BPM.</param>
    /// <param name="transients">List of detected transients.</param>
    /// <param name="durationSeconds">Total duration of the audio in seconds.</param>
    /// <returns>List of generated warp markers.</returns>
    public List<WarpMarker> GenerateMarkersFromAnalysis(
        BeatAnalysisResult tempoResult,
        IReadOnlyList<TransientEvent> transients,
        double durationSeconds)
    {
        List<WarpMarker> markers = new();

        if (tempoResult.DetectedBpm <= 0 || durationSeconds <= 0)
        {
            return markers;
        }

        double bpm = tempoResult.DetectedBpm;
        double beatDuration = 60.0 / bpm; // seconds per beat
        double confidence = tempoResult.Confidence;

        // Find the first strong transient as potential downbeat
        double firstBeatTime = FindFirstBeat(transients, beatDuration);

        // Generate beat grid
        int totalBeats = (int)Math.Ceiling((durationSeconds - firstBeatTime) / beatDuration) + 1;

        for (int beat = 0; beat < totalBeats; beat++)
        {
            double expectedTime = firstBeatTime + beat * beatDuration;

            if (expectedTime > durationSeconds)
                break;

            double actualTime = expectedTime;
            double markerConfidence = confidence;

            // Optionally snap to nearest transient
            if (SnapToTransients && transients.Count > 0)
            {
                var (snappedTime, snappedConfidence) = SnapToNearestTransient(
                    expectedTime, transients, beatDuration);
                actualTime = snappedTime;
                markerConfidence = Math.Min(markerConfidence, snappedConfidence);
            }

            if (markerConfidence >= MinimumConfidence)
            {
                bool isDownbeat = beat % BeatsPerBar == 0;

                markers.Add(new WarpMarker
                {
                    TimePosition = actualTime,
                    BeatPosition = beat,
                    IsDownbeat = isDownbeat,
                    IsManual = false,
                    Confidence = markerConfidence
                });
            }
        }

        return markers;
    }

    /// <summary>
    /// Refines existing markers using transient data.
    /// </summary>
    /// <param name="markers">Existing warp markers.</param>
    /// <param name="transients">Detected transients.</param>
    /// <returns>Refined list of warp markers.</returns>
    public List<WarpMarker> RefineMarkers(
        IReadOnlyList<WarpMarker> markers,
        IReadOnlyList<TransientEvent> transients)
    {
        List<WarpMarker> refined = new();

        foreach (var marker in markers)
        {
            // Don't modify manual markers
            if (marker.IsManual)
            {
                refined.Add(marker.Clone());
                continue;
            }

            // Calculate expected beat duration from surrounding markers
            double beatDuration = 0.5; // default 120 BPM

            var (snappedTime, snappedConfidence) = SnapToNearestTransient(
                marker.TimePosition, transients, beatDuration);

            var refinedMarker = marker.Clone();
            refinedMarker.TimePosition = snappedTime;
            refinedMarker.Confidence = Math.Min(marker.Confidence, snappedConfidence);

            refined.Add(refinedMarker);
        }

        return refined;
    }

    /// <summary>
    /// Generates a simple beat grid without transient analysis.
    /// </summary>
    /// <param name="bpm">Tempo in BPM.</param>
    /// <param name="durationSeconds">Duration of the audio in seconds.</param>
    /// <param name="startOffset">Time offset for the first beat in seconds.</param>
    /// <returns>List of warp markers on a regular grid.</returns>
    public List<WarpMarker> GenerateSimpleGrid(double bpm, double durationSeconds, double startOffset = 0)
    {
        List<WarpMarker> markers = new();

        if (bpm <= 0 || durationSeconds <= 0)
            return markers;

        double beatDuration = 60.0 / bpm;
        int totalBeats = (int)Math.Ceiling((durationSeconds - startOffset) / beatDuration) + 1;

        for (int beat = 0; beat < totalBeats; beat++)
        {
            double time = startOffset + beat * beatDuration;

            if (time > durationSeconds)
                break;

            markers.Add(new WarpMarker
            {
                TimePosition = time,
                BeatPosition = beat,
                IsDownbeat = beat % BeatsPerBar == 0,
                IsManual = false,
                Confidence = 1.0
            });
        }

        return markers;
    }

    /// <summary>
    /// Calculates the warped time for a given beat position.
    /// </summary>
    /// <param name="markers">Warp markers defining the time mapping.</param>
    /// <param name="beatPosition">Beat position to convert.</param>
    /// <returns>Time position in seconds.</returns>
    public static double BeatToTime(IReadOnlyList<WarpMarker> markers, double beatPosition)
    {
        if (markers == null || markers.Count == 0)
            return 0;

        if (markers.Count == 1)
        {
            // Single marker, assume constant tempo
            return markers[0].TimePosition;
        }

        // Sort markers by beat position
        var sorted = markers.OrderBy(m => m.BeatPosition).ToList();

        // Find surrounding markers
        WarpMarker? before = null;
        WarpMarker? after = null;

        for (int i = 0; i < sorted.Count; i++)
        {
            if (sorted[i].BeatPosition <= beatPosition)
            {
                before = sorted[i];
            }
            if (sorted[i].BeatPosition >= beatPosition && after == null)
            {
                after = sorted[i];
            }
        }

        if (before == null)
        {
            // Before first marker, extrapolate
            if (sorted.Count >= 2)
            {
                double tempo = sorted[0].CalculateTempoToNext(sorted[1]);
                double beatDiff = beatPosition - sorted[0].BeatPosition;
                return sorted[0].TimePosition + (beatDiff * 60.0 / tempo);
            }
            return sorted[0].TimePosition;
        }

        if (after == null || before == after)
        {
            // After last marker, extrapolate
            if (sorted.Count >= 2)
            {
                double tempo = sorted[^2].CalculateTempoToNext(sorted[^1]);
                double beatDiff = beatPosition - sorted[^1].BeatPosition;
                return sorted[^1].TimePosition + (beatDiff * 60.0 / tempo);
            }
            return before.TimePosition;
        }

        // Interpolate between markers
        double beatRange = after.BeatPosition - before.BeatPosition;
        double timeRange = after.TimePosition - before.TimePosition;
        double beatFraction = (beatPosition - before.BeatPosition) / beatRange;

        return before.TimePosition + beatFraction * timeRange;
    }

    /// <summary>
    /// Calculates the beat position for a given time.
    /// </summary>
    /// <param name="markers">Warp markers defining the time mapping.</param>
    /// <param name="timePosition">Time position in seconds.</param>
    /// <returns>Beat position.</returns>
    public static double TimeToBeat(IReadOnlyList<WarpMarker> markers, double timePosition)
    {
        if (markers == null || markers.Count == 0)
            return 0;

        if (markers.Count == 1)
        {
            return markers[0].BeatPosition;
        }

        // Sort markers by time position
        var sorted = markers.OrderBy(m => m.TimePosition).ToList();

        // Find surrounding markers
        WarpMarker? before = null;
        WarpMarker? after = null;

        for (int i = 0; i < sorted.Count; i++)
        {
            if (sorted[i].TimePosition <= timePosition)
            {
                before = sorted[i];
            }
            if (sorted[i].TimePosition >= timePosition && after == null)
            {
                after = sorted[i];
            }
        }

        if (before == null)
        {
            return sorted[0].BeatPosition;
        }

        if (after == null || before == after)
        {
            return before.BeatPosition;
        }

        // Interpolate between markers
        double timeRange = after.TimePosition - before.TimePosition;
        double beatRange = after.BeatPosition - before.BeatPosition;
        double timeFraction = (timePosition - before.TimePosition) / timeRange;

        return before.BeatPosition + timeFraction * beatRange;
    }

    private double FindFirstBeat(IReadOnlyList<TransientEvent> transients, double beatDuration)
    {
        // Look for the first strong transient that could be a downbeat
        var strongTransients = transients
            .Where(t => t.IsStrong && t.TimeSeconds < beatDuration * 2)
            .OrderBy(t => t.TimeSeconds)
            .ToList();

        if (strongTransients.Count > 0)
        {
            return strongTransients[0].TimeSeconds;
        }

        // Fall back to first transient or zero
        if (transients.Count > 0)
        {
            return transients[0].TimeSeconds;
        }

        return 0;
    }

    private (double time, double confidence) SnapToNearestTransient(
        double expectedTime,
        IReadOnlyList<TransientEvent> transients,
        double beatDuration)
    {
        double toleranceSeconds = SnapToleranceMs / 1000.0;
        double bestDistance = double.MaxValue;
        TransientEvent? bestTransient = null;

        foreach (var transient in transients)
        {
            double distance = Math.Abs(transient.TimeSeconds - expectedTime);

            if (distance < toleranceSeconds && distance < bestDistance)
            {
                bestDistance = distance;
                bestTransient = transient;
            }
        }

        if (bestTransient != null)
        {
            // Calculate confidence based on how close the transient is to expected position
            double distanceRatio = bestDistance / toleranceSeconds;
            double snapConfidence = 1.0 - distanceRatio * 0.5; // 50% confidence at tolerance edge

            return (bestTransient.TimeSeconds, snapConfidence * bestTransient.Strength);
        }

        // No nearby transient, return expected position with reduced confidence
        return (expectedTime, 0.5);
    }
}
