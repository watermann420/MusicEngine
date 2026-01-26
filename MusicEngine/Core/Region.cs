// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using System;

namespace MusicEngine.Core;

/// <summary>
/// Defines the type of region.
/// </summary>
public enum RegionType
{
    /// <summary>General purpose region for organization.</summary>
    General,

    /// <summary>Selection region (temporary).</summary>
    Selection,

    /// <summary>Loop region for playback looping.</summary>
    Loop,

    /// <summary>Punch-in/out region for recording.</summary>
    Punch,

    /// <summary>Export region for bouncing/rendering.</summary>
    Export,

    /// <summary>Arrangement section region.</summary>
    Section,

    /// <summary>Automation region.</summary>
    Automation
}

/// <summary>
/// Represents a region in the arrangement timeline.
/// Regions can be used for organizing clips, defining loop points, punch regions, or export ranges.
/// </summary>
public class Region : IEquatable<Region>
{
    /// <summary>Unique identifier for this region.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Display name of the region.</summary>
    public string Name { get; set; } = "Region";

    /// <summary>Start position in beats.</summary>
    public double StartPosition { get; set; }

    /// <summary>End position in beats.</summary>
    public double EndPosition { get; set; }

    /// <summary>Type of region.</summary>
    public RegionType Type { get; set; } = RegionType.General;

    /// <summary>Color for visual representation (hex format).</summary>
    public string Color { get; set; } = "#9B59B6";

    /// <summary>Optional description or notes.</summary>
    public string? Description { get; set; }

    /// <summary>Whether this region is locked from editing.</summary>
    public bool IsLocked { get; set; }

    /// <summary>Whether this region is active (for loop/punch regions).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Optional track index if region is track-specific (-1 = all tracks).</summary>
    public int TrackIndex { get; set; } = -1;

    /// <summary>Z-order for overlapping regions (higher = on top).</summary>
    public int ZOrder { get; set; }

    /// <summary>Opacity for visual representation (0.0 to 1.0).</summary>
    public double Opacity { get; set; } = 0.3;

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    /// <summary>Last modification timestamp.</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the length of this region in beats.
    /// </summary>
    public double Length => EndPosition - StartPosition;

    /// <summary>
    /// Creates a new region.
    /// </summary>
    public Region()
    {
    }

    /// <summary>
    /// Creates a new region with position and type.
    /// </summary>
    /// <param name="startPosition">Start position in beats.</param>
    /// <param name="endPosition">End position in beats.</param>
    /// <param name="name">Region name.</param>
    /// <param name="type">Region type.</param>
    public Region(double startPosition, double endPosition, string name = "Region", RegionType type = RegionType.General)
    {
        StartPosition = startPosition;
        EndPosition = endPosition;
        Name = name;
        Type = type;
        Color = GetDefaultColor(type);
    }

    /// <summary>
    /// Creates a loop region.
    /// </summary>
    /// <param name="startPosition">Loop start in beats.</param>
    /// <param name="endPosition">Loop end in beats.</param>
    /// <param name="name">Optional name.</param>
    /// <returns>A new loop region.</returns>
    public static Region CreateLoop(double startPosition, double endPosition, string name = "Loop")
    {
        return new Region(startPosition, endPosition, name, RegionType.Loop)
        {
            Color = "#00AAFF",
            Opacity = 0.2
        };
    }

    /// <summary>
    /// Creates a punch region for recording.
    /// </summary>
    /// <param name="startPosition">Punch-in position in beats.</param>
    /// <param name="endPosition">Punch-out position in beats.</param>
    /// <returns>A new punch region.</returns>
    public static Region CreatePunch(double startPosition, double endPosition)
    {
        return new Region(startPosition, endPosition, "Punch", RegionType.Punch)
        {
            Color = "#FF4444",
            Opacity = 0.25
        };
    }

    /// <summary>
    /// Creates an export region.
    /// </summary>
    /// <param name="startPosition">Export start in beats.</param>
    /// <param name="endPosition">Export end in beats.</param>
    /// <param name="name">Export region name.</param>
    /// <returns>A new export region.</returns>
    public static Region CreateExport(double startPosition, double endPosition, string name = "Export")
    {
        return new Region(startPosition, endPosition, name, RegionType.Export)
        {
            Color = "#44FF44",
            Opacity = 0.2
        };
    }

    /// <summary>
    /// Checks if a given position falls within this region.
    /// </summary>
    /// <param name="position">Position in beats to check.</param>
    /// <returns>True if the position is within the region.</returns>
    public bool ContainsPosition(double position)
    {
        return position >= StartPosition && position < EndPosition;
    }

    /// <summary>
    /// Checks if this region overlaps with another range.
    /// </summary>
    /// <param name="start">Start of the range.</param>
    /// <param name="end">End of the range.</param>
    /// <returns>True if there is overlap.</returns>
    public bool Overlaps(double start, double end)
    {
        return StartPosition < end && EndPosition > start;
    }

    /// <summary>
    /// Checks if this region overlaps with another region.
    /// </summary>
    /// <param name="other">The other region.</param>
    /// <returns>True if the regions overlap.</returns>
    public bool Overlaps(Region other)
    {
        return Overlaps(other.StartPosition, other.EndPosition);
    }

    /// <summary>
    /// Gets the overlap amount with another range.
    /// </summary>
    /// <param name="start">Start of the range.</param>
    /// <param name="end">End of the range.</param>
    /// <returns>Length of overlap in beats, or 0 if no overlap.</returns>
    public double GetOverlap(double start, double end)
    {
        if (!Overlaps(start, end)) return 0;

        var overlapStart = Math.Max(StartPosition, start);
        var overlapEnd = Math.Min(EndPosition, end);
        return overlapEnd - overlapStart;
    }

    /// <summary>
    /// Moves the region to a new position.
    /// </summary>
    /// <param name="newStartPosition">New start position in beats.</param>
    public void MoveTo(double newStartPosition)
    {
        if (IsLocked) return;

        var length = Length;
        StartPosition = newStartPosition;
        EndPosition = newStartPosition + length;
        Touch();
    }

    /// <summary>
    /// Resizes the region.
    /// </summary>
    /// <param name="newLength">New length in beats.</param>
    public void Resize(double newLength)
    {
        if (IsLocked) return;
        if (newLength <= 0) return;

        EndPosition = StartPosition + newLength;
        Touch();
    }

    /// <summary>
    /// Sets the start position, adjusting length.
    /// </summary>
    /// <param name="newStart">New start position.</param>
    public void SetStart(double newStart)
    {
        if (IsLocked) return;
        if (newStart >= EndPosition) return;

        StartPosition = newStart;
        Touch();
    }

    /// <summary>
    /// Sets the end position.
    /// </summary>
    /// <param name="newEnd">New end position.</param>
    public void SetEnd(double newEnd)
    {
        if (IsLocked) return;
        if (newEnd <= StartPosition) return;

        EndPosition = newEnd;
        Touch();
    }

    /// <summary>
    /// Creates a copy of this region at a new position.
    /// </summary>
    /// <param name="newStartPosition">Start position for the copy.</param>
    /// <returns>A new region with the same properties.</returns>
    public Region Duplicate(double? newStartPosition = null)
    {
        var startPos = newStartPosition ?? EndPosition;
        return new Region
        {
            Name = Name + " (copy)",
            StartPosition = startPos,
            EndPosition = startPos + Length,
            Type = Type,
            Color = Color,
            Description = Description,
            IsActive = IsActive,
            TrackIndex = TrackIndex,
            ZOrder = ZOrder,
            Opacity = Opacity
        };
    }

    /// <summary>
    /// Gets the default color for a region type.
    /// </summary>
    public static string GetDefaultColor(RegionType type)
    {
        return type switch
        {
            RegionType.General => "#9B59B6",
            RegionType.Selection => "#3498DB",
            RegionType.Loop => "#00AAFF",
            RegionType.Punch => "#FF4444",
            RegionType.Export => "#44FF44",
            RegionType.Section => "#F39C12",
            RegionType.Automation => "#1ABC9C",
            _ => "#9B59B6"
        };
    }

    /// <summary>
    /// Updates the modification timestamp.
    /// </summary>
    public void Touch()
    {
        ModifiedAt = DateTime.UtcNow;
    }

    public bool Equals(Region? other)
    {
        if (other is null) return false;
        return Id.Equals(other.Id);
    }

    public override bool Equals(object? obj) => obj is Region other && Equals(other);

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(Region? left, Region? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Region? left, Region? right) => !(left == right);

    public override string ToString() =>
        $"[{Type}] {Name} ({StartPosition:F2} - {EndPosition:F2})";
}
