//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Manages the complete song arrangement with sections, markers, and structure.

using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicEngine.Core;

/// <summary>
/// Event arguments for section-related events.
/// </summary>
public class SectionEventArgs : EventArgs
{
    /// <summary>The section associated with this event.</summary>
    public ArrangementSection Section { get; }

    /// <summary>The previous position (for move events).</summary>
    public double? PreviousStartPosition { get; }

    public SectionEventArgs(ArrangementSection section, double? previousStartPosition = null)
    {
        Section = section;
        PreviousStartPosition = previousStartPosition;
    }
}

/// <summary>
/// Event arguments for arrangement structure changes.
/// </summary>
public class ArrangementChangedEventArgs : EventArgs
{
    /// <summary>The type of change that occurred.</summary>
    public ArrangementChangeType ChangeType { get; }

    /// <summary>The affected section (if applicable).</summary>
    public ArrangementSection? Section { get; }

    public ArrangementChangedEventArgs(ArrangementChangeType changeType, ArrangementSection? section = null)
    {
        ChangeType = changeType;
        Section = section;
    }
}

/// <summary>
/// Types of arrangement changes.
/// </summary>
public enum ArrangementChangeType
{
    /// <summary>A section was added.</summary>
    SectionAdded,

    /// <summary>A section was removed.</summary>
    SectionRemoved,

    /// <summary>A section was modified.</summary>
    SectionModified,

    /// <summary>Sections were reordered.</summary>
    SectionsReordered,

    /// <summary>The arrangement was cleared.</summary>
    Cleared,

    /// <summary>The tempo map changed.</summary>
    TempoChanged,

    /// <summary>The time signature changed.</summary>
    TimeSignatureChanged
}

/// <summary>
/// Manages the complete song arrangement including sections, tempo, and time signature.
/// </summary>
public class Arrangement
{
    private readonly List<ArrangementSection> _sections = [];
    private readonly object _lock = new();

    /// <summary>Name of the arrangement.</summary>
    public string Name { get; set; } = "Untitled";

    /// <summary>Gets all sections in the arrangement, sorted by position.</summary>
    public IReadOnlyList<ArrangementSection> Sections
    {
        get
        {
            lock (_lock)
            {
                return _sections
                    .OrderBy(s => s.OrderIndex)
                    .ThenBy(s => s.StartPosition)
                    .ToList()
                    .AsReadOnly();
            }
        }
    }

    /// <summary>Gets the number of sections.</summary>
    public int SectionCount
    {
        get
        {
            lock (_lock)
            {
                return _sections.Count;
            }
        }
    }

    /// <summary>Gets the total length of the arrangement in beats.</summary>
    public double TotalLength
    {
        get
        {
            lock (_lock)
            {
                if (_sections.Count == 0)
                    return 0;

                return _sections.Max(s => s.EffectiveEndPosition);
            }
        }
    }

    /// <summary>Default BPM for the arrangement.</summary>
    public double Bpm { get; set; } = 120.0;

    /// <summary>Time signature numerator (beats per bar).</summary>
    public int TimeSignatureNumerator { get; set; } = 4;

    /// <summary>Time signature denominator (note value for one beat).</summary>
    public int TimeSignatureDenominator { get; set; } = 4;

    /// <summary>Marker track for navigation markers.</summary>
    public MarkerTrack MarkerTrack { get; } = new();

    /// <summary>Event raised when a section is added.</summary>
    public event EventHandler<SectionEventArgs>? SectionAdded;

    /// <summary>Event raised when a section is removed.</summary>
    public event EventHandler<SectionEventArgs>? SectionRemoved;

    /// <summary>Event raised when a section is modified.</summary>
    public event EventHandler<SectionEventArgs>? SectionModified;

    /// <summary>Event raised when the arrangement structure changes.</summary>
    public event EventHandler<ArrangementChangedEventArgs>? ArrangementChanged;

    /// <summary>
    /// Adds a section to the arrangement.
    /// </summary>
    /// <param name="section">The section to add.</param>
    /// <returns>True if added successfully.</returns>
    public bool AddSection(ArrangementSection section)
    {
        ArgumentNullException.ThrowIfNull(section);

        lock (_lock)
        {
            if (_sections.Any(s => s.Id == section.Id))
                return false;

            section.OrderIndex = _sections.Count;
            _sections.Add(section);
        }

        SectionAdded?.Invoke(this, new SectionEventArgs(section));
        ArrangementChanged?.Invoke(this, new ArrangementChangedEventArgs(ArrangementChangeType.SectionAdded, section));
        return true;
    }

    /// <summary>
    /// Creates and adds a new section.
    /// </summary>
    /// <param name="startPosition">Start position in beats.</param>
    /// <param name="endPosition">End position in beats.</param>
    /// <param name="name">Section name.</param>
    /// <returns>The created section.</returns>
    public ArrangementSection AddSection(double startPosition, double endPosition, string name = "Section")
    {
        var section = new ArrangementSection(startPosition, endPosition, name);
        AddSection(section);
        return section;
    }

    /// <summary>
    /// Creates and adds a new section with a predefined type.
    /// </summary>
    /// <param name="startPosition">Start position in beats.</param>
    /// <param name="endPosition">End position in beats.</param>
    /// <param name="type">Section type.</param>
    /// <returns>The created section.</returns>
    public ArrangementSection AddSection(double startPosition, double endPosition, SectionType type)
    {
        var section = new ArrangementSection(startPosition, endPosition, type);
        AddSection(section);
        return section;
    }

    /// <summary>
    /// Removes a section from the arrangement.
    /// </summary>
    /// <param name="section">The section to remove.</param>
    /// <returns>True if removed successfully.</returns>
    public bool RemoveSection(ArrangementSection section)
    {
        ArgumentNullException.ThrowIfNull(section);

        if (section.IsLocked)
            return false;

        bool removed;
        lock (_lock)
        {
            removed = _sections.Remove(section);
            if (removed)
            {
                ReindexSections();
            }
        }

        if (removed)
        {
            SectionRemoved?.Invoke(this, new SectionEventArgs(section));
            ArrangementChanged?.Invoke(this, new ArrangementChangedEventArgs(ArrangementChangeType.SectionRemoved, section));
        }

        return removed;
    }

    /// <summary>
    /// Removes a section by its ID.
    /// </summary>
    /// <param name="sectionId">The section ID to remove.</param>
    /// <returns>True if removed successfully.</returns>
    public bool RemoveSection(Guid sectionId)
    {
        ArrangementSection? section;
        lock (_lock)
        {
            section = _sections.FirstOrDefault(s => s.Id == sectionId);
        }

        return section != null && RemoveSection(section);
    }

    /// <summary>
    /// Gets the section at the specified position.
    /// </summary>
    /// <param name="position">Position in beats.</param>
    /// <returns>The section at the position, or null if none.</returns>
    public ArrangementSection? GetSectionAt(double position)
    {
        lock (_lock)
        {
            // First, try to find a section that directly contains the position
            var section = _sections.FirstOrDefault(s => s.ContainsPositionWithRepeats(position));
            if (section != null)
                return section;

            // If no direct match, find the most recent section before this position
            return _sections
                .Where(s => s.StartPosition <= position)
                .OrderByDescending(s => s.StartPosition)
                .FirstOrDefault();
        }
    }

    /// <summary>
    /// Gets a section by its ID.
    /// </summary>
    /// <param name="sectionId">The section ID.</param>
    /// <returns>The section, or null if not found.</returns>
    public ArrangementSection? GetSection(Guid sectionId)
    {
        lock (_lock)
        {
            return _sections.FirstOrDefault(s => s.Id == sectionId);
        }
    }

    /// <summary>
    /// Gets sections within a specified range.
    /// </summary>
    /// <param name="startPosition">Start position in beats.</param>
    /// <param name="endPosition">End position in beats.</param>
    /// <returns>List of sections within or overlapping the range.</returns>
    public IReadOnlyList<ArrangementSection> GetSectionsInRange(double startPosition, double endPosition)
    {
        lock (_lock)
        {
            return _sections
                .Where(s => s.StartPosition < endPosition && s.EffectiveEndPosition > startPosition)
                .OrderBy(s => s.StartPosition)
                .ToList()
                .AsReadOnly();
        }
    }

    /// <summary>
    /// Gets sections of a specific type.
    /// </summary>
    /// <param name="type">The section type to filter by.</param>
    /// <returns>List of sections of the specified type.</returns>
    public IReadOnlyList<ArrangementSection> GetSectionsByType(SectionType type)
    {
        lock (_lock)
        {
            return _sections
                .Where(s => s.Type == type)
                .OrderBy(s => s.StartPosition)
                .ToList()
                .AsReadOnly();
        }
    }

    /// <summary>
    /// Gets the next section after the specified position.
    /// </summary>
    /// <param name="position">Current position in beats.</param>
    /// <returns>The next section, or null if none exists.</returns>
    public ArrangementSection? GetNextSection(double position)
    {
        lock (_lock)
        {
            return _sections
                .Where(s => s.StartPosition > position)
                .OrderBy(s => s.StartPosition)
                .FirstOrDefault();
        }
    }

    /// <summary>
    /// Gets the previous section before the specified position.
    /// </summary>
    /// <param name="position">Current position in beats.</param>
    /// <returns>The previous section, or null if none exists.</returns>
    public ArrangementSection? GetPreviousSection(double position)
    {
        lock (_lock)
        {
            return _sections
                .Where(s => s.StartPosition < position)
                .OrderByDescending(s => s.StartPosition)
                .FirstOrDefault();
        }
    }

    /// <summary>
    /// Moves a section to a new position.
    /// </summary>
    /// <param name="section">The section to move.</param>
    /// <param name="newStartPosition">New start position in beats.</param>
    /// <returns>True if moved successfully.</returns>
    public bool MoveSection(ArrangementSection section, double newStartPosition)
    {
        ArgumentNullException.ThrowIfNull(section);

        if (section.IsLocked)
            return false;

        lock (_lock)
        {
            if (!_sections.Contains(section))
                return false;
        }

        var previousStart = section.StartPosition;
        section.MoveTo(newStartPosition);

        SectionModified?.Invoke(this, new SectionEventArgs(section, previousStart));
        ArrangementChanged?.Invoke(this, new ArrangementChangedEventArgs(ArrangementChangeType.SectionModified, section));
        return true;
    }

    /// <summary>
    /// Reorders sections by their order index.
    /// </summary>
    /// <param name="section">The section to move.</param>
    /// <param name="newOrderIndex">New order index.</param>
    public void ReorderSection(ArrangementSection section, int newOrderIndex)
    {
        ArgumentNullException.ThrowIfNull(section);

        lock (_lock)
        {
            if (!_sections.Contains(section))
                return;

            var currentIndex = section.OrderIndex;
            if (currentIndex == newOrderIndex)
                return;

            // Adjust other sections' order indices
            foreach (var s in _sections)
            {
                if (s.Id == section.Id)
                {
                    s.OrderIndex = newOrderIndex;
                }
                else if (currentIndex < newOrderIndex)
                {
                    // Moving down: shift items between old and new position up
                    if (s.OrderIndex > currentIndex && s.OrderIndex <= newOrderIndex)
                        s.OrderIndex--;
                }
                else
                {
                    // Moving up: shift items between new and old position down
                    if (s.OrderIndex >= newOrderIndex && s.OrderIndex < currentIndex)
                        s.OrderIndex++;
                }
            }
        }

        ArrangementChanged?.Invoke(this, new ArrangementChangedEventArgs(ArrangementChangeType.SectionsReordered));
    }

    /// <summary>
    /// Duplicates a section at a new position.
    /// </summary>
    /// <param name="section">The section to duplicate.</param>
    /// <param name="newStartPosition">Start position for the copy (null = append after original).</param>
    /// <returns>The duplicated section.</returns>
    public ArrangementSection DuplicateSection(ArrangementSection section, double? newStartPosition = null)
    {
        ArgumentNullException.ThrowIfNull(section);

        var startPos = newStartPosition ?? section.EffectiveEndPosition;
        var copy = section.Clone(startPos);
        AddSection(copy);
        return copy;
    }

    /// <summary>
    /// Clears all sections from the arrangement.
    /// </summary>
    /// <param name="includeLockedSections">Whether to remove locked sections.</param>
    /// <returns>Number of sections removed.</returns>
    public int Clear(bool includeLockedSections = false)
    {
        int count;
        lock (_lock)
        {
            if (includeLockedSections)
            {
                count = _sections.Count;
                _sections.Clear();
            }
            else
            {
                var toRemove = _sections.Where(s => !s.IsLocked).ToList();
                count = toRemove.Count;
                foreach (var section in toRemove)
                {
                    _sections.Remove(section);
                }
                ReindexSections();
            }
        }

        if (count > 0)
        {
            ArrangementChanged?.Invoke(this, new ArrangementChangedEventArgs(ArrangementChangeType.Cleared));
        }

        return count;
    }

    /// <summary>
    /// Validates the arrangement for overlapping sections and gaps.
    /// </summary>
    /// <returns>List of validation issues.</returns>
    public IReadOnlyList<string> Validate()
    {
        var issues = new List<string>();

        lock (_lock)
        {
            var sortedSections = _sections.OrderBy(s => s.StartPosition).ToList();

            for (int i = 0; i < sortedSections.Count; i++)
            {
                var current = sortedSections[i];

                // Check for invalid section
                if (current.EndPosition <= current.StartPosition)
                {
                    issues.Add($"Section '{current.Name}' has invalid position (end <= start).");
                }

                // Check for overlaps with subsequent sections
                for (int j = i + 1; j < sortedSections.Count; j++)
                {
                    var next = sortedSections[j];
                    if (current.EffectiveEndPosition > next.StartPosition)
                    {
                        issues.Add($"Section '{current.Name}' overlaps with '{next.Name}'.");
                    }
                }
            }
        }

        return issues.AsReadOnly();
    }

    /// <summary>
    /// Converts a beat position to time in seconds.
    /// </summary>
    /// <param name="beats">Position in beats.</param>
    /// <returns>Time in seconds.</returns>
    public double BeatsToSeconds(double beats)
    {
        return beats * 60.0 / Bpm;
    }

    /// <summary>
    /// Converts time in seconds to beat position.
    /// </summary>
    /// <param name="seconds">Time in seconds.</param>
    /// <returns>Position in beats.</returns>
    public double SecondsToBeats(double seconds)
    {
        return seconds * Bpm / 60.0;
    }

    /// <summary>
    /// Gets the bar number for a beat position.
    /// </summary>
    /// <param name="beats">Position in beats.</param>
    /// <returns>Bar number (1-based).</returns>
    public int GetBarNumber(double beats)
    {
        return (int)(beats / TimeSignatureNumerator) + 1;
    }

    /// <summary>
    /// Gets the beat within the bar for a beat position.
    /// </summary>
    /// <param name="beats">Position in beats.</param>
    /// <returns>Beat within bar (1-based).</returns>
    public int GetBeatInBar(double beats)
    {
        return (int)(beats % TimeSignatureNumerator) + 1;
    }

    /// <summary>
    /// Formats a beat position as bar:beat notation.
    /// </summary>
    /// <param name="beats">Position in beats.</param>
    /// <returns>Formatted string (e.g., "4:2").</returns>
    public string FormatPosition(double beats)
    {
        var bar = GetBarNumber(beats);
        var beat = GetBeatInBar(beats);
        return $"{bar}:{beat}";
    }

    /// <summary>
    /// Creates an arrangement from a standard song structure.
    /// </summary>
    /// <param name="barsPerSection">Bars per section (default 8).</param>
    /// <returns>A new arrangement with standard sections.</returns>
    public static Arrangement CreateStandardStructure(int barsPerSection = 8)
    {
        var arrangement = new Arrangement { Name = "Standard Song" };
        var beatsPerSection = barsPerSection * 4; // Assuming 4/4 time

        double position = 0;

        arrangement.AddSection(position, position + beatsPerSection, SectionType.Intro);
        position += beatsPerSection;

        arrangement.AddSection(position, position + beatsPerSection, SectionType.Verse);
        position += beatsPerSection;

        arrangement.AddSection(position, position + beatsPerSection / 2, SectionType.PreChorus);
        position += beatsPerSection / 2;

        arrangement.AddSection(position, position + beatsPerSection, SectionType.Chorus);
        position += beatsPerSection;

        arrangement.AddSection(position, position + beatsPerSection, SectionType.Verse);
        position += beatsPerSection;

        arrangement.AddSection(position, position + beatsPerSection / 2, SectionType.PreChorus);
        position += beatsPerSection / 2;

        arrangement.AddSection(position, position + beatsPerSection, SectionType.Chorus);
        position += beatsPerSection;

        arrangement.AddSection(position, position + beatsPerSection, SectionType.Bridge);
        position += beatsPerSection;

        arrangement.AddSection(position, position + beatsPerSection, SectionType.Chorus);
        position += beatsPerSection;

        arrangement.AddSection(position, position + beatsPerSection / 2, SectionType.Outro);

        return arrangement;
    }

    private void ReindexSections()
    {
        var sorted = _sections.OrderBy(s => s.OrderIndex).ThenBy(s => s.StartPosition).ToList();
        for (int i = 0; i < sorted.Count; i++)
        {
            sorted[i].OrderIndex = i;
        }
    }
}
