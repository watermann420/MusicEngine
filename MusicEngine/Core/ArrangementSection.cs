// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Song arrangement management.

using System;

namespace MusicEngine.Core;

/// <summary>
/// Predefined section types for common song structures.
/// </summary>
public enum SectionType
{
    /// <summary>Custom section with user-defined name.</summary>
    Custom,

    /// <summary>Introduction section.</summary>
    Intro,

    /// <summary>Verse section.</summary>
    Verse,

    /// <summary>Pre-chorus section.</summary>
    PreChorus,

    /// <summary>Chorus section.</summary>
    Chorus,

    /// <summary>Post-chorus section.</summary>
    PostChorus,

    /// <summary>Bridge section.</summary>
    Bridge,

    /// <summary>Breakdown section.</summary>
    Breakdown,

    /// <summary>Build-up section.</summary>
    Buildup,

    /// <summary>Drop section (EDM).</summary>
    Drop,

    /// <summary>Solo section.</summary>
    Solo,

    /// <summary>Interlude section.</summary>
    Interlude,

    /// <summary>Outro section.</summary>
    Outro
}

/// <summary>
/// Represents a section of a song arrangement.
/// Sections define logical parts of a song with start/end positions, colors, and repeat counts.
/// </summary>
public class ArrangementSection : IEquatable<ArrangementSection>
{
    /// <summary>Unique identifier for this section.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Start position in beats.</summary>
    public double StartPosition { get; set; }

    /// <summary>End position in beats.</summary>
    public double EndPosition { get; set; }

    /// <summary>Display name of the section.</summary>
    public string Name { get; set; } = "Section";

    /// <summary>Color for visual representation (hex format).</summary>
    public string Color { get; set; } = "#3498DB";

    /// <summary>Predefined section type.</summary>
    public SectionType Type { get; set; } = SectionType.Custom;

    /// <summary>Number of times this section repeats (1 = plays once, no repeat).</summary>
    public int RepeatCount { get; set; } = 1;

    /// <summary>Optional description or notes for this section.</summary>
    public string? Description { get; set; }

    /// <summary>Whether this section is muted during playback.</summary>
    public bool IsMuted { get; set; }

    /// <summary>Whether this section is locked from editing.</summary>
    public bool IsLocked { get; set; }

    /// <summary>Order index for sorting (lower = earlier).</summary>
    public int OrderIndex { get; set; }

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    /// <summary>Last modification timestamp.</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a new arrangement section.
    /// </summary>
    /// <param name="startPosition">Start position in beats.</param>
    /// <param name="endPosition">End position in beats.</param>
    /// <param name="name">Section name.</param>
    public ArrangementSection(double startPosition, double endPosition, string name = "Section")
    {
        StartPosition = startPosition;
        EndPosition = endPosition;
        Name = name;
    }

    /// <summary>
    /// Creates a new arrangement section with type.
    /// </summary>
    /// <param name="startPosition">Start position in beats.</param>
    /// <param name="endPosition">End position in beats.</param>
    /// <param name="type">Section type.</param>
    public ArrangementSection(double startPosition, double endPosition, SectionType type)
        : this(startPosition, endPosition, GetDefaultName(type))
    {
        Type = type;
        Color = GetDefaultColor(type);
    }

    /// <summary>
    /// Gets the length of this section in beats.
    /// </summary>
    public double Length => EndPosition - StartPosition;

    /// <summary>
    /// Gets the total length including all repeats.
    /// </summary>
    public double TotalLength => Length * RepeatCount;

    /// <summary>
    /// Gets the effective end position including all repeats.
    /// </summary>
    public double EffectiveEndPosition => StartPosition + TotalLength;

    /// <summary>
    /// Checks if a given position falls within this section (single iteration).
    /// </summary>
    /// <param name="position">Position in beats to check.</param>
    /// <returns>True if the position is within the section.</returns>
    public bool ContainsPosition(double position)
    {
        return position >= StartPosition && position < EndPosition;
    }

    /// <summary>
    /// Checks if a given position falls within this section (including repeats).
    /// </summary>
    /// <param name="position">Position in beats to check.</param>
    /// <returns>True if the position is within the section or its repeats.</returns>
    public bool ContainsPositionWithRepeats(double position)
    {
        return position >= StartPosition && position < EffectiveEndPosition;
    }

    /// <summary>
    /// Gets the repeat iteration for a given position (0-based).
    /// </summary>
    /// <param name="position">Position in beats.</param>
    /// <returns>The repeat iteration, or -1 if position is outside the section.</returns>
    public int GetRepeatIteration(double position)
    {
        if (!ContainsPositionWithRepeats(position))
            return -1;

        var offset = position - StartPosition;
        return (int)(offset / Length);
    }

    /// <summary>
    /// Gets the position within a single section iteration.
    /// </summary>
    /// <param name="position">Absolute position in beats.</param>
    /// <returns>Position relative to section start (0 to Length).</returns>
    public double GetLocalPosition(double position)
    {
        if (!ContainsPositionWithRepeats(position))
            return -1;

        var offset = position - StartPosition;
        return offset % Length;
    }

    /// <summary>
    /// Updates the modification timestamp.
    /// </summary>
    public void Touch()
    {
        ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Moves the section to a new start position, preserving length.
    /// </summary>
    /// <param name="newStartPosition">New start position in beats.</param>
    public void MoveTo(double newStartPosition)
    {
        var length = Length;
        StartPosition = newStartPosition;
        EndPosition = newStartPosition + length;
        Touch();
    }

    /// <summary>
    /// Resizes the section.
    /// </summary>
    /// <param name="newLength">New length in beats.</param>
    public void Resize(double newLength)
    {
        if (newLength <= 0)
            throw new ArgumentException("Length must be positive.", nameof(newLength));

        EndPosition = StartPosition + newLength;
        Touch();
    }

    /// <summary>
    /// Creates a copy of this section at a new position.
    /// </summary>
    /// <param name="newStartPosition">Start position for the copy.</param>
    /// <returns>A new section with the same properties.</returns>
    public ArrangementSection Clone(double newStartPosition)
    {
        return new ArrangementSection(newStartPosition, newStartPosition + Length, Name)
        {
            Color = Color,
            Type = Type,
            RepeatCount = RepeatCount,
            Description = Description
        };
    }

    /// <summary>
    /// Gets the default name for a section type.
    /// </summary>
    public static string GetDefaultName(SectionType type)
    {
        return type switch
        {
            SectionType.Custom => "Section",
            SectionType.Intro => "Intro",
            SectionType.Verse => "Verse",
            SectionType.PreChorus => "Pre-Chorus",
            SectionType.Chorus => "Chorus",
            SectionType.PostChorus => "Post-Chorus",
            SectionType.Bridge => "Bridge",
            SectionType.Breakdown => "Breakdown",
            SectionType.Buildup => "Build-up",
            SectionType.Drop => "Drop",
            SectionType.Solo => "Solo",
            SectionType.Interlude => "Interlude",
            SectionType.Outro => "Outro",
            _ => "Section"
        };
    }

    /// <summary>
    /// Gets the default color for a section type.
    /// </summary>
    public static string GetDefaultColor(SectionType type)
    {
        return type switch
        {
            SectionType.Custom => "#3498DB",
            SectionType.Intro => "#9B59B6",
            SectionType.Verse => "#2ECC71",
            SectionType.PreChorus => "#F39C12",
            SectionType.Chorus => "#E74C3C",
            SectionType.PostChorus => "#E67E22",
            SectionType.Bridge => "#1ABC9C",
            SectionType.Breakdown => "#34495E",
            SectionType.Buildup => "#F1C40F",
            SectionType.Drop => "#C0392B",
            SectionType.Solo => "#8E44AD",
            SectionType.Interlude => "#16A085",
            SectionType.Outro => "#7F8C8D",
            _ => "#3498DB"
        };
    }

    public bool Equals(ArrangementSection? other)
    {
        if (other is null) return false;
        return Id.Equals(other.Id);
    }

    public override bool Equals(object? obj) => obj is ArrangementSection other && Equals(other);

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(ArrangementSection? left, ArrangementSection? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(ArrangementSection? left, ArrangementSection? right) => !(left == right);

    public override string ToString() =>
        $"[{Type}] {Name} ({StartPosition:F2} - {EndPosition:F2})" +
        (RepeatCount > 1 ? $" x{RepeatCount}" : "");
}
