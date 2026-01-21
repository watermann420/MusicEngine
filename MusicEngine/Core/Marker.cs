//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Represents a single marker in a track for navigation and arrangement.

using System;

namespace MusicEngine.Core;

/// <summary>
/// Defines the type of marker.
/// </summary>
public enum MarkerType
{
    /// <summary>A cue point for navigation.</summary>
    Cue,

    /// <summary>A loop marker defining loop start/end points.</summary>
    Loop,

    /// <summary>A section marker dividing the song into logical parts.</summary>
    Section
}

/// <summary>
/// Represents a single marker in the timeline.
/// Markers can be used for navigation, loop points, or section indicators.
/// </summary>
public class Marker : IEquatable<Marker>
{
    /// <summary>Unique identifier for this marker.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Position in beats from the start of the track.</summary>
    public double Position { get; set; }

    /// <summary>Display name of the marker.</summary>
    public string Name { get; set; } = "Marker";

    /// <summary>Color for visual representation (hex format, e.g., "#FF5500").</summary>
    public string Color { get; set; } = "#FF9500";

    /// <summary>Type of marker (Cue, Loop, Section).</summary>
    public MarkerType Type { get; set; } = MarkerType.Cue;

    /// <summary>Optional end position for loop markers (in beats).</summary>
    public double? EndPosition { get; set; }

    /// <summary>Optional description or notes for this marker.</summary>
    public string? Description { get; set; }

    /// <summary>Whether this marker is locked from editing.</summary>
    public bool IsLocked { get; set; }

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    /// <summary>Last modification timestamp.</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a new marker at the specified position.
    /// </summary>
    /// <param name="position">Position in beats.</param>
    /// <param name="name">Display name.</param>
    /// <param name="type">Marker type.</param>
    public Marker(double position, string name = "Marker", MarkerType type = MarkerType.Cue)
    {
        Position = position;
        Name = name;
        Type = type;
    }

    /// <summary>
    /// Creates a new marker with all properties.
    /// </summary>
    public Marker(double position, string name, MarkerType type, string color)
        : this(position, name, type)
    {
        Color = color;
    }

    /// <summary>
    /// Creates a loop marker with start and end positions.
    /// </summary>
    /// <param name="startPosition">Start position in beats.</param>
    /// <param name="endPosition">End position in beats.</param>
    /// <param name="name">Display name.</param>
    /// <returns>A new loop marker.</returns>
    public static Marker CreateLoop(double startPosition, double endPosition, string name = "Loop")
    {
        return new Marker(startPosition, name, MarkerType.Loop)
        {
            EndPosition = endPosition,
            Color = "#00AAFF"
        };
    }

    /// <summary>
    /// Creates a section marker.
    /// </summary>
    /// <param name="position">Position in beats.</param>
    /// <param name="name">Section name (e.g., "Intro", "Verse", "Chorus").</param>
    /// <param name="color">Optional color.</param>
    /// <returns>A new section marker.</returns>
    public static Marker CreateSection(double position, string name, string? color = null)
    {
        return new Marker(position, name, MarkerType.Section)
        {
            Color = color ?? "#9B59B6"
        };
    }

    /// <summary>
    /// Creates a cue marker.
    /// </summary>
    /// <param name="position">Position in beats.</param>
    /// <param name="name">Cue name.</param>
    /// <returns>A new cue marker.</returns>
    public static Marker CreateCue(double position, string name = "Cue")
    {
        return new Marker(position, name, MarkerType.Cue)
        {
            Color = "#FF9500"
        };
    }

    /// <summary>
    /// Gets the length of this marker (for loop markers).
    /// </summary>
    public double Length => EndPosition.HasValue ? EndPosition.Value - Position : 0;

    /// <summary>
    /// Checks if a given position falls within this marker's range (for loop markers).
    /// </summary>
    /// <param name="position">Position in beats to check.</param>
    /// <returns>True if the position is within the marker range.</returns>
    public bool ContainsPosition(double position)
    {
        if (!EndPosition.HasValue)
            return Math.Abs(position - Position) < 0.001;

        return position >= Position && position <= EndPosition.Value;
    }

    /// <summary>
    /// Updates the modification timestamp.
    /// </summary>
    public void Touch()
    {
        ModifiedAt = DateTime.UtcNow;
    }

    public bool Equals(Marker? other)
    {
        if (other is null) return false;
        return Id.Equals(other.Id);
    }

    public override bool Equals(object? obj) => obj is Marker other && Equals(other);

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(Marker? left, Marker? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Marker? left, Marker? right) => !(left == right);

    public override string ToString() => $"[{Type}] {Name} @{Position:F2}";
}
