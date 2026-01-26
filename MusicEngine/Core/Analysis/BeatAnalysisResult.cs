// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio analysis component.

using System;
using System.Collections.Generic;

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Contains the combined results of beat and tempo analysis.
/// Includes detected BPM, confidence level, and a list of beat positions.
/// </summary>
public class BeatAnalysisResult
{
    /// <summary>
    /// Gets or sets the detected tempo in beats per minute.
    /// </summary>
    public double DetectedBpm { get; set; }

    /// <summary>
    /// Gets or sets the confidence level of the BPM detection (0.0 to 1.0).
    /// Higher values indicate more reliable detection.
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Gets or sets the list of detected beat positions in seconds.
    /// </summary>
    public List<double> Beats { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of detected downbeat positions in seconds.
    /// Downbeats are typically the first beat of each bar.
    /// </summary>
    public List<double> Downbeats { get; set; } = new();

    /// <summary>
    /// Gets or sets the estimated time signature numerator (beats per bar).
    /// Common values: 3, 4, 6, etc.
    /// </summary>
    public int TimeSignatureNumerator { get; set; } = 4;

    /// <summary>
    /// Gets or sets the estimated time signature denominator.
    /// Common values: 4 (quarter note), 8 (eighth note).
    /// </summary>
    public int TimeSignatureDenominator { get; set; } = 4;

    /// <summary>
    /// Gets or sets the detected start offset (time of first beat) in seconds.
    /// </summary>
    public double StartOffset { get; set; }

    /// <summary>
    /// Gets or sets the total duration of the analyzed audio in seconds.
    /// </summary>
    public double DurationSeconds { get; set; }

    /// <summary>
    /// Gets the number of detected beats.
    /// </summary>
    public int BeatCount => Beats.Count;

    /// <summary>
    /// Gets the number of detected downbeats (bars).
    /// </summary>
    public int BarCount => Downbeats.Count;

    /// <summary>
    /// Gets the average tempo based on beat intervals.
    /// May differ from DetectedBpm if tempo varies.
    /// </summary>
    public double AverageBpm
    {
        get
        {
            if (Beats.Count < 2)
                return DetectedBpm;

            double totalInterval = 0;
            for (int i = 1; i < Beats.Count; i++)
            {
                totalInterval += Beats[i] - Beats[i - 1];
            }

            double avgInterval = totalInterval / (Beats.Count - 1);
            return avgInterval > 0 ? 60.0 / avgInterval : 0;
        }
    }

    /// <summary>
    /// Gets whether the detection is considered reliable (confidence >= 0.5).
    /// </summary>
    public bool IsReliable => Confidence >= 0.5;

    /// <summary>
    /// Gets the beat duration in seconds based on detected BPM.
    /// </summary>
    public double BeatDuration => DetectedBpm > 0 ? 60.0 / DetectedBpm : 0;

    /// <summary>
    /// Gets the bar duration in seconds based on detected BPM and time signature.
    /// </summary>
    public double BarDuration => BeatDuration * TimeSignatureNumerator;

    /// <summary>
    /// Creates an empty beat analysis result.
    /// </summary>
    public BeatAnalysisResult()
    {
    }

    /// <summary>
    /// Creates a beat analysis result with the specified BPM and confidence.
    /// </summary>
    /// <param name="bpm">Detected tempo in BPM.</param>
    /// <param name="confidence">Confidence level (0.0 to 1.0).</param>
    public BeatAnalysisResult(double bpm, double confidence)
    {
        DetectedBpm = bpm;
        Confidence = confidence;
    }

    /// <summary>
    /// Gets the beat position (index) for a given time in seconds.
    /// Returns -1 if no beat is found near the specified time.
    /// </summary>
    /// <param name="timeSeconds">Time position in seconds.</param>
    /// <param name="toleranceMs">Tolerance in milliseconds for matching.</param>
    /// <returns>Beat index or -1 if not found.</returns>
    public int GetBeatAtTime(double timeSeconds, double toleranceMs = 50)
    {
        double toleranceSec = toleranceMs / 1000.0;

        for (int i = 0; i < Beats.Count; i++)
        {
            if (Math.Abs(Beats[i] - timeSeconds) <= toleranceSec)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Gets the bar number for a given beat index.
    /// </summary>
    /// <param name="beatIndex">Beat index.</param>
    /// <returns>Bar number (1-based).</returns>
    public int GetBarNumber(int beatIndex)
    {
        if (beatIndex < 0)
            return 0;

        return (beatIndex / TimeSignatureNumerator) + 1;
    }

    /// <summary>
    /// Gets the beat within the bar for a given beat index.
    /// </summary>
    /// <param name="beatIndex">Beat index.</param>
    /// <returns>Beat within bar (1-based).</returns>
    public int GetBeatInBar(int beatIndex)
    {
        if (beatIndex < 0)
            return 0;

        return (beatIndex % TimeSignatureNumerator) + 1;
    }

    /// <summary>
    /// Calculates the time position for a given bar and beat.
    /// </summary>
    /// <param name="bar">Bar number (1-based).</param>
    /// <param name="beat">Beat within bar (1-based).</param>
    /// <returns>Time position in seconds.</returns>
    public double GetTimeForBarBeat(int bar, int beat)
    {
        int totalBeats = ((bar - 1) * TimeSignatureNumerator) + (beat - 1);

        if (totalBeats < Beats.Count && totalBeats >= 0)
        {
            return Beats[totalBeats];
        }

        // Extrapolate if beyond detected beats
        return StartOffset + (totalBeats * BeatDuration);
    }

    /// <summary>
    /// Generates warp markers from this analysis result.
    /// </summary>
    /// <returns>List of warp markers.</returns>
    public List<WarpMarker> ToWarpMarkers()
    {
        List<WarpMarker> markers = new();

        for (int i = 0; i < Beats.Count; i++)
        {
            bool isDownbeat = (i % TimeSignatureNumerator) == 0;

            markers.Add(new WarpMarker
            {
                TimePosition = Beats[i],
                BeatPosition = i,
                IsDownbeat = isDownbeat,
                IsManual = false,
                Confidence = Confidence
            });
        }

        return markers;
    }

    /// <summary>
    /// Creates a deep copy of this result.
    /// </summary>
    public BeatAnalysisResult Clone()
    {
        return new BeatAnalysisResult
        {
            DetectedBpm = DetectedBpm,
            Confidence = Confidence,
            Beats = new List<double>(Beats),
            Downbeats = new List<double>(Downbeats),
            TimeSignatureNumerator = TimeSignatureNumerator,
            TimeSignatureDenominator = TimeSignatureDenominator,
            StartOffset = StartOffset,
            DurationSeconds = DurationSeconds
        };
    }

    public override string ToString()
    {
        return $"BeatAnalysis: {DetectedBpm:F1} BPM, {Confidence:P0} confidence, " +
               $"{BeatCount} beats, {BarCount} bars, {TimeSignatureNumerator}/{TimeSignatureDenominator}";
    }
}
