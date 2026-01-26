// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio analysis component.

using System;

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Represents a warp marker that maps a time position in the audio to a beat position.
/// Used for time-stretching and tempo alignment similar to Ableton Live's warping system.
/// </summary>
public class WarpMarker : IComparable<WarpMarker>
{
    /// <summary>
    /// Gets or sets the time position in the audio file in seconds.
    /// This is the actual position in the unwarped audio.
    /// </summary>
    public double TimePosition { get; set; }

    /// <summary>
    /// Gets or sets the beat position (in beats from the start).
    /// This defines where this time position should align in the musical grid.
    /// </summary>
    public double BeatPosition { get; set; }

    /// <summary>
    /// Gets or sets whether this marker represents a downbeat (first beat of a bar).
    /// </summary>
    public bool IsDownbeat { get; set; }

    /// <summary>
    /// Gets or sets whether this marker was manually placed by the user.
    /// Manual markers take precedence over auto-generated markers.
    /// </summary>
    public bool IsManual { get; set; }

    /// <summary>
    /// Gets or sets the confidence level of auto-generated markers (0.0 to 1.0).
    /// Only relevant for automatically detected markers.
    /// </summary>
    public double Confidence { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets an optional label for the marker.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Gets the time position in milliseconds.
    /// </summary>
    public double TimePositionMs => TimePosition * 1000.0;

    /// <summary>
    /// Creates a new warp marker with default values.
    /// </summary>
    public WarpMarker()
    {
    }

    /// <summary>
    /// Creates a new warp marker with the specified time and beat positions.
    /// </summary>
    /// <param name="timePosition">Time position in seconds.</param>
    /// <param name="beatPosition">Beat position (in beats).</param>
    /// <param name="isDownbeat">Whether this is a downbeat.</param>
    public WarpMarker(double timePosition, double beatPosition, bool isDownbeat = false)
    {
        TimePosition = timePosition;
        BeatPosition = beatPosition;
        IsDownbeat = isDownbeat;
    }

    /// <summary>
    /// Creates a copy of this warp marker.
    /// </summary>
    public WarpMarker Clone()
    {
        return new WarpMarker
        {
            TimePosition = TimePosition,
            BeatPosition = BeatPosition,
            IsDownbeat = IsDownbeat,
            IsManual = IsManual,
            Confidence = Confidence,
            Label = Label
        };
    }

    /// <summary>
    /// Compares this marker to another by time position.
    /// </summary>
    public int CompareTo(WarpMarker? other)
    {
        if (other == null) return 1;
        return TimePosition.CompareTo(other.TimePosition);
    }

    /// <summary>
    /// Calculates the instantaneous tempo at this marker given the next marker.
    /// </summary>
    /// <param name="nextMarker">The next warp marker in sequence.</param>
    /// <returns>Tempo in BPM between this marker and the next.</returns>
    public double CalculateTempoToNext(WarpMarker nextMarker)
    {
        if (nextMarker == null)
            throw new ArgumentNullException(nameof(nextMarker));

        double timeDelta = nextMarker.TimePosition - TimePosition;
        double beatDelta = nextMarker.BeatPosition - BeatPosition;

        if (timeDelta <= 0 || beatDelta <= 0)
            return 0;

        // BPM = beats per minute = (beatDelta / timeDelta) * 60
        return (beatDelta / timeDelta) * 60.0;
    }

    public override string ToString()
    {
        string type = IsDownbeat ? "Downbeat" : "Beat";
        string manual = IsManual ? " (Manual)" : "";
        return $"WarpMarker[{type}{manual}]: Time={TimePosition:F3}s, Beat={BeatPosition:F2}";
    }

    public override bool Equals(object? obj)
    {
        if (obj is WarpMarker other)
        {
            return Math.Abs(TimePosition - other.TimePosition) < 0.0001 &&
                   Math.Abs(BeatPosition - other.BeatPosition) < 0.0001;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(TimePosition, BeatPosition);
    }
}
