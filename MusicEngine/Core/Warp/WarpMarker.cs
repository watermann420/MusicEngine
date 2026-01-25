//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Represents a time anchor point for audio warping, linking original and warped positions.

using System;

namespace MusicEngine.Core.Warp;

/// <summary>
/// Defines the type of warp marker based on how it was created.
/// </summary>
public enum WarpMarkerType
{
    /// <summary>Marker automatically placed on a detected transient.</summary>
    Transient,

    /// <summary>Marker placed on a musical beat grid position.</summary>
    Beat,

    /// <summary>Marker manually placed by the user.</summary>
    User,

    /// <summary>Marker at the start of the audio clip (anchor point).</summary>
    Start,

    /// <summary>Marker at the end of the audio clip (anchor point).</summary>
    End
}

/// <summary>
/// Represents a time anchor point for audio warping.
/// Warp markers define the relationship between original audio positions and their
/// new time-stretched positions, enabling non-linear time stretching (elastic audio).
/// </summary>
public class WarpMarker : IEquatable<WarpMarker>, IComparable<WarpMarker>
{
    /// <summary>Unique identifier for this marker (string format for serialization compatibility).</summary>
    public string MarkerId { get; } = Guid.NewGuid().ToString();

    /// <summary>Unique identifier for this marker.</summary>
    public Guid Id => Guid.TryParse(MarkerId, out var guid) ? guid : Guid.Empty;

    /// <summary>Original position in the source audio (in samples). This never changes once the marker is created.</summary>
    public long OriginalPositionSamples { get; set; }

    /// <summary>Warped (target) position in the output (in samples).</summary>
    public long WarpedPositionSamples { get; set; }

    /// <summary>
    /// Original position in seconds (derived from sample position and sample rate).
    /// Requires SampleRate to be set for accurate calculation.
    /// </summary>
    public double OriginalTimeSeconds { get; private set; }

    /// <summary>
    /// Warped position in beats. Moving this stretches/compresses audio.
    /// This is an alternative representation to WarpedPositionSamples for beat-based workflows.
    /// </summary>
    public double WarpedBeatPosition { get; set; }

    /// <summary>Type of this warp marker.</summary>
    public WarpMarkerType MarkerType { get; set; } = WarpMarkerType.User;

    /// <summary>Whether this marker is locked from editing.</summary>
    public bool IsLocked { get; set; }

    /// <summary>
    /// Whether this is an anchor point (start/end of region).
    /// Anchor points define the boundaries of the warpable audio.
    /// </summary>
    public bool IsAnchor { get; set; }

    /// <summary>Whether this marker is selected in the UI.</summary>
    public bool IsSelected { get; set; }

    /// <summary>Optional label for this marker.</summary>
    public string? Label { get; set; }

    /// <summary>Transient strength if this marker was created from transient detection (0.0 to 1.0).</summary>
    public float TransientStrength { get; set; }

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    /// <summary>Last modification timestamp.</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a new warp marker with default values.
    /// </summary>
    public WarpMarker()
    {
    }

    /// <summary>
    /// Creates a new warp marker at the specified positions.
    /// </summary>
    /// <param name="originalPositionSamples">Original position in samples.</param>
    /// <param name="warpedPositionSamples">Warped position in samples.</param>
    /// <param name="markerType">Type of marker.</param>
    public WarpMarker(long originalPositionSamples, long warpedPositionSamples, WarpMarkerType markerType = WarpMarkerType.User)
    {
        OriginalPositionSamples = originalPositionSamples;
        WarpedPositionSamples = warpedPositionSamples;
        MarkerType = markerType;
        IsAnchor = markerType == WarpMarkerType.Start || markerType == WarpMarkerType.End;
    }

    /// <summary>
    /// Creates a new warp marker with beat-based warped position (as per user specification).
    /// </summary>
    /// <param name="originalSamplePosition">Original position in the source audio (in samples).</param>
    /// <param name="sampleRate">Audio sample rate for time calculations.</param>
    /// <param name="warpedBeatPosition">Warped position in beats.</param>
    /// <param name="type">Type of marker.</param>
    public WarpMarker(long originalSamplePosition, int sampleRate, double warpedBeatPosition, WarpMarkerType type = WarpMarkerType.User)
    {
        OriginalPositionSamples = originalSamplePosition;
        OriginalTimeSeconds = (double)originalSamplePosition / sampleRate;
        WarpedBeatPosition = warpedBeatPosition;
        MarkerType = type;
        IsAnchor = type == WarpMarkerType.Start || type == WarpMarkerType.End;
    }

    /// <summary>
    /// Gets the original position in seconds.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate.</param>
    /// <returns>Original position in seconds.</returns>
    public double GetOriginalPositionSeconds(int sampleRate)
    {
        return (double)OriginalPositionSamples / sampleRate;
    }

    /// <summary>
    /// Gets the warped position in seconds.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate.</param>
    /// <returns>Warped position in seconds.</returns>
    public double GetWarpedPositionSeconds(int sampleRate)
    {
        return (double)WarpedPositionSamples / sampleRate;
    }

    /// <summary>
    /// Gets the original position in beats.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate.</param>
    /// <param name="bpm">Tempo in BPM.</param>
    /// <returns>Original position in beats.</returns>
    public double GetOriginalPositionBeats(int sampleRate, double bpm)
    {
        double seconds = GetOriginalPositionSeconds(sampleRate);
        return seconds * bpm / 60.0;
    }

    /// <summary>
    /// Gets the warped position in beats.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate.</param>
    /// <param name="bpm">Tempo in BPM.</param>
    /// <returns>Warped position in beats.</returns>
    public double GetWarpedPositionBeats(int sampleRate, double bpm)
    {
        double seconds = GetWarpedPositionSeconds(sampleRate);
        return seconds * bpm / 60.0;
    }

    /// <summary>
    /// Sets the original position from seconds.
    /// </summary>
    /// <param name="seconds">Position in seconds.</param>
    /// <param name="sampleRate">Audio sample rate.</param>
    public void SetOriginalPositionFromSeconds(double seconds, int sampleRate)
    {
        OriginalPositionSamples = (long)(seconds * sampleRate);
        Touch();
    }

    /// <summary>
    /// Sets the warped position from seconds.
    /// </summary>
    /// <param name="seconds">Position in seconds.</param>
    /// <param name="sampleRate">Audio sample rate.</param>
    public void SetWarpedPositionFromSeconds(double seconds, int sampleRate)
    {
        WarpedPositionSamples = (long)(seconds * sampleRate);
        Touch();
    }

    /// <summary>
    /// Sets the original position from beats.
    /// </summary>
    /// <param name="beats">Position in beats.</param>
    /// <param name="sampleRate">Audio sample rate.</param>
    /// <param name="bpm">Tempo in BPM.</param>
    public void SetOriginalPositionFromBeats(double beats, int sampleRate, double bpm)
    {
        double seconds = beats * 60.0 / bpm;
        SetOriginalPositionFromSeconds(seconds, sampleRate);
    }

    /// <summary>
    /// Sets the warped position from beats.
    /// </summary>
    /// <param name="beats">Position in beats.</param>
    /// <param name="sampleRate">Audio sample rate.</param>
    /// <param name="bpm">Tempo in BPM.</param>
    public void SetWarpedPositionFromBeats(double beats, int sampleRate, double bpm)
    {
        double seconds = beats * 60.0 / bpm;
        SetWarpedPositionFromSeconds(seconds, sampleRate);
    }

    /// <summary>
    /// Moves the warped position by a delta amount in samples.
    /// </summary>
    /// <param name="deltaSamples">Number of samples to move (positive or negative).</param>
    public void MoveWarpedPosition(long deltaSamples)
    {
        if (IsLocked) return;

        WarpedPositionSamples = Math.Max(0, WarpedPositionSamples + deltaSamples);
        Touch();
    }

    /// <summary>
    /// Moves the warped position to align with a beat grid position.
    /// </summary>
    /// <param name="targetBeat">Target beat position.</param>
    /// <param name="sampleRate">Audio sample rate.</param>
    /// <param name="bpm">Tempo in BPM.</param>
    public void SnapToGrid(double targetBeat, int sampleRate, double bpm)
    {
        if (IsLocked) return;

        SetWarpedPositionFromBeats(targetBeat, sampleRate, bpm);
    }

    /// <summary>
    /// Calculates the local stretch ratio at this marker compared to the previous marker.
    /// </summary>
    /// <param name="previousMarker">The previous warp marker.</param>
    /// <returns>Stretch ratio (1.0 = no stretch, 0.5 = half speed, 2.0 = double speed).</returns>
    public double CalculateStretchRatioFrom(WarpMarker? previousMarker)
    {
        if (previousMarker == null)
            return 1.0;

        long originalDelta = OriginalPositionSamples - previousMarker.OriginalPositionSamples;
        long warpedDelta = WarpedPositionSamples - previousMarker.WarpedPositionSamples;

        if (originalDelta == 0 || warpedDelta == 0)
            return 1.0;

        // Stretch ratio = original / warped
        // > 1.0 means audio is sped up (compressed in time)
        // < 1.0 means audio is slowed down (stretched in time)
        return (double)originalDelta / warpedDelta;
    }

    /// <summary>
    /// Creates a duplicate of this marker.
    /// </summary>
    /// <returns>A new marker with the same values but a new ID.</returns>
    public WarpMarker Duplicate()
    {
        return new WarpMarker
        {
            OriginalPositionSamples = OriginalPositionSamples,
            WarpedPositionSamples = WarpedPositionSamples,
            MarkerType = MarkerType,
            Label = Label,
            TransientStrength = TransientStrength
        };
    }

    /// <summary>
    /// Updates the modification timestamp.
    /// </summary>
    public void Touch()
    {
        ModifiedAt = DateTime.UtcNow;
    }

    public bool Equals(WarpMarker? other)
    {
        if (other is null) return false;
        return Id.Equals(other.Id);
    }

    public override bool Equals(object? obj) => obj is WarpMarker other && Equals(other);

    public override int GetHashCode() => Id.GetHashCode();

    public int CompareTo(WarpMarker? other)
    {
        if (other == null) return 1;
        return OriginalPositionSamples.CompareTo(other.OriginalPositionSamples);
    }

    public static bool operator ==(WarpMarker? left, WarpMarker? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(WarpMarker? left, WarpMarker? right) => !(left == right);

    public static bool operator <(WarpMarker? left, WarpMarker? right) =>
        left is null ? right is not null : left.CompareTo(right) < 0;

    public static bool operator >(WarpMarker? left, WarpMarker? right) =>
        left is not null && left.CompareTo(right) > 0;

    public static bool operator <=(WarpMarker? left, WarpMarker? right) =>
        left is null || left.CompareTo(right) <= 0;

    public static bool operator >=(WarpMarker? left, WarpMarker? right) =>
        left is null ? right is null : left.CompareTo(right) >= 0;

    public override string ToString() =>
        $"[{MarkerType}] Original: {OriginalPositionSamples} -> Warped: {WarpedPositionSamples}";
}
