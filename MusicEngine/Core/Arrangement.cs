//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Manages the complete song arrangement with sections, markers, and structure.

using System;
using System.Collections.Generic;
using System.Linq;
using MusicEngine.Core.Clips;

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
/// Manages the complete song arrangement including sections, clips, regions, tempo, and time signature.
/// </summary>
public class Arrangement
{
    private readonly List<ArrangementSection> _sections = [];
    private readonly List<AudioClip> _audioClips = [];
    private readonly List<MidiClip> _midiClips = [];
    private readonly List<Region> _regions = [];
    private readonly List<CrossfadeRegion> _crossfades = [];
    private readonly CrossfadeProcessor _crossfadeProcessor = new();
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

    /// <summary>Gets all audio clips in the arrangement.</summary>
    public IReadOnlyList<AudioClip> AudioClips
    {
        get
        {
            lock (_lock)
            {
                return _audioClips.OrderBy(c => c.StartPosition).ToList().AsReadOnly();
            }
        }
    }

    /// <summary>Gets all MIDI clips in the arrangement.</summary>
    public IReadOnlyList<MidiClip> MidiClips
    {
        get
        {
            lock (_lock)
            {
                return _midiClips.OrderBy(c => c.StartPosition).ToList().AsReadOnly();
            }
        }
    }

    /// <summary>Gets all regions in the arrangement.</summary>
    public IReadOnlyList<Region> Regions
    {
        get
        {
            lock (_lock)
            {
                return _regions.OrderBy(r => r.StartPosition).ToList().AsReadOnly();
            }
        }
    }

    /// <summary>Gets the number of audio clips.</summary>
    public int AudioClipCount
    {
        get { lock (_lock) { return _audioClips.Count; } }
    }

    /// <summary>Gets the number of MIDI clips.</summary>
    public int MidiClipCount
    {
        get { lock (_lock) { return _midiClips.Count; } }
    }

    /// <summary>Gets the number of regions.</summary>
    public int RegionCount
    {
        get { lock (_lock) { return _regions.Count; } }
    }

    /// <summary>Gets all crossfade regions in the arrangement.</summary>
    public IReadOnlyList<CrossfadeRegion> Crossfades
    {
        get
        {
            lock (_lock)
            {
                return _crossfades.OrderBy(c => c.StartPosition).ToList().AsReadOnly();
            }
        }
    }

    /// <summary>Gets the number of crossfade regions.</summary>
    public int CrossfadeCount
    {
        get { lock (_lock) { return _crossfades.Count; } }
    }

    /// <summary>Gets the crossfade processor for this arrangement.</summary>
    public CrossfadeProcessor CrossfadeProcessor => _crossfadeProcessor;

    /// <summary>Gets or sets whether auto-crossfade is enabled when clips overlap.</summary>
    public bool AutoCrossfadeEnabled
    {
        get => _crossfadeProcessor.AutoCrossfade;
        set => _crossfadeProcessor.AutoCrossfade = value;
    }

    /// <summary>Gets or sets the default crossfade length in beats.</summary>
    public double DefaultCrossfadeLength
    {
        get => _crossfadeProcessor.DefaultCrossfadeLength;
        set => _crossfadeProcessor.DefaultCrossfadeLength = value;
    }

    /// <summary>Gets or sets the default crossfade type.</summary>
    public CrossfadeType DefaultCrossfadeType
    {
        get => _crossfadeProcessor.DefaultCrossfadeType;
        set => _crossfadeProcessor.DefaultCrossfadeType = value;
    }

    /// <summary>Event raised when a section is added.</summary>
    public event EventHandler<SectionEventArgs>? SectionAdded;

    /// <summary>Event raised when a section is removed.</summary>
    public event EventHandler<SectionEventArgs>? SectionRemoved;

    /// <summary>Event raised when a section is modified.</summary>
    public event EventHandler<SectionEventArgs>? SectionModified;

    /// <summary>Event raised when the arrangement structure changes.</summary>
    public event EventHandler<ArrangementChangedEventArgs>? ArrangementChanged;

    /// <summary>Event raised when an audio clip is added.</summary>
    public event EventHandler<AudioClip>? AudioClipAdded;

    /// <summary>Event raised when an audio clip is removed.</summary>
    public event EventHandler<AudioClip>? AudioClipRemoved;

    /// <summary>Event raised when a MIDI clip is added.</summary>
    public event EventHandler<MidiClip>? MidiClipAdded;

    /// <summary>Event raised when a MIDI clip is removed.</summary>
    public event EventHandler<MidiClip>? MidiClipRemoved;

    /// <summary>Event raised when a region is added.</summary>
    public event EventHandler<Region>? RegionAdded;

    /// <summary>Event raised when a region is removed.</summary>
    public event EventHandler<Region>? RegionRemoved;

    /// <summary>Event raised when a crossfade is added.</summary>
    public event EventHandler<CrossfadeRegion>? CrossfadeAdded;

    /// <summary>Event raised when a crossfade is removed.</summary>
    public event EventHandler<CrossfadeRegion>? CrossfadeRemoved;

    /// <summary>Event raised when a crossfade is modified.</summary>
    public event EventHandler<CrossfadeRegion>? CrossfadeModified;

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

    #region Audio Clips

    /// <summary>
    /// Adds an audio clip to the arrangement.
    /// </summary>
    /// <param name="clip">The audio clip to add.</param>
    /// <returns>True if added successfully.</returns>
    public bool AddAudioClip(AudioClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);

        lock (_lock)
        {
            if (_audioClips.Any(c => c.Id == clip.Id))
                return false;

            _audioClips.Add(clip);
        }

        AudioClipAdded?.Invoke(this, clip);
        return true;
    }

    /// <summary>
    /// Creates and adds a new audio clip.
    /// </summary>
    /// <param name="filePath">Path to the audio file.</param>
    /// <param name="startPosition">Start position in beats.</param>
    /// <param name="length">Length in beats.</param>
    /// <param name="trackIndex">Track index.</param>
    /// <returns>The created audio clip.</returns>
    public AudioClip AddAudioClip(string filePath, double startPosition, double length, int trackIndex = 0)
    {
        var clip = new AudioClip(filePath, startPosition, length, trackIndex);
        AddAudioClip(clip);
        return clip;
    }

    /// <summary>
    /// Removes an audio clip from the arrangement.
    /// </summary>
    /// <param name="clip">The clip to remove.</param>
    /// <returns>True if removed successfully.</returns>
    public bool RemoveAudioClip(AudioClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);

        if (clip.IsLocked)
            return false;

        bool removed;
        lock (_lock)
        {
            removed = _audioClips.Remove(clip);
        }

        if (removed)
        {
            AudioClipRemoved?.Invoke(this, clip);
        }

        return removed;
    }

    /// <summary>
    /// Removes an audio clip by its ID.
    /// </summary>
    /// <param name="clipId">The clip ID to remove.</param>
    /// <returns>True if removed successfully.</returns>
    public bool RemoveAudioClip(Guid clipId)
    {
        AudioClip? clip;
        lock (_lock)
        {
            clip = _audioClips.FirstOrDefault(c => c.Id == clipId);
        }

        return clip != null && RemoveAudioClip(clip);
    }

    /// <summary>
    /// Gets an audio clip by its ID.
    /// </summary>
    /// <param name="clipId">The clip ID.</param>
    /// <returns>The clip, or null if not found.</returns>
    public AudioClip? GetAudioClip(Guid clipId)
    {
        lock (_lock)
        {
            return _audioClips.FirstOrDefault(c => c.Id == clipId);
        }
    }

    /// <summary>
    /// Gets audio clips at a specific position.
    /// </summary>
    /// <param name="position">Position in beats.</param>
    /// <returns>List of clips at the position.</returns>
    public IReadOnlyList<AudioClip> GetAudioClipsAt(double position)
    {
        lock (_lock)
        {
            return _audioClips
                .Where(c => c.ContainsPosition(position))
                .OrderBy(c => c.TrackIndex)
                .ToList()
                .AsReadOnly();
        }
    }

    /// <summary>
    /// Gets audio clips within a range.
    /// </summary>
    /// <param name="startPosition">Start position in beats.</param>
    /// <param name="endPosition">End position in beats.</param>
    /// <returns>List of clips within or overlapping the range.</returns>
    public IReadOnlyList<AudioClip> GetAudioClipsInRange(double startPosition, double endPosition)
    {
        lock (_lock)
        {
            return _audioClips
                .Where(c => c.StartPosition < endPosition && c.EndPosition > startPosition)
                .OrderBy(c => c.StartPosition)
                .ToList()
                .AsReadOnly();
        }
    }

    /// <summary>
    /// Gets audio clips on a specific track.
    /// </summary>
    /// <param name="trackIndex">Track index.</param>
    /// <returns>List of clips on the track.</returns>
    public IReadOnlyList<AudioClip> GetAudioClipsOnTrack(int trackIndex)
    {
        lock (_lock)
        {
            return _audioClips
                .Where(c => c.TrackIndex == trackIndex)
                .OrderBy(c => c.StartPosition)
                .ToList()
                .AsReadOnly();
        }
    }

    #endregion

    #region MIDI Clips

    /// <summary>
    /// Adds a MIDI clip to the arrangement.
    /// </summary>
    /// <param name="clip">The MIDI clip to add.</param>
    /// <returns>True if added successfully.</returns>
    public bool AddMidiClip(MidiClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);

        lock (_lock)
        {
            if (_midiClips.Any(c => c.Id == clip.Id))
                return false;

            _midiClips.Add(clip);
        }

        MidiClipAdded?.Invoke(this, clip);
        return true;
    }

    /// <summary>
    /// Creates and adds a new MIDI clip.
    /// </summary>
    /// <param name="startPosition">Start position in beats.</param>
    /// <param name="length">Length in beats.</param>
    /// <param name="trackIndex">Track index.</param>
    /// <returns>The created MIDI clip.</returns>
    public MidiClip AddMidiClip(double startPosition, double length, int trackIndex = 0)
    {
        var clip = new MidiClip(startPosition, length, trackIndex);
        AddMidiClip(clip);
        return clip;
    }

    /// <summary>
    /// Creates and adds a new MIDI clip from a Pattern.
    /// </summary>
    /// <param name="pattern">The pattern to reference.</param>
    /// <param name="startPosition">Start position in beats.</param>
    /// <param name="trackIndex">Track index.</param>
    /// <returns>The created MIDI clip.</returns>
    public MidiClip AddMidiClip(Pattern pattern, double startPosition, int trackIndex = 0)
    {
        var clip = new MidiClip(pattern, startPosition, trackIndex);
        AddMidiClip(clip);
        return clip;
    }

    /// <summary>
    /// Removes a MIDI clip from the arrangement.
    /// </summary>
    /// <param name="clip">The clip to remove.</param>
    /// <returns>True if removed successfully.</returns>
    public bool RemoveMidiClip(MidiClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);

        if (clip.IsLocked)
            return false;

        bool removed;
        lock (_lock)
        {
            removed = _midiClips.Remove(clip);
        }

        if (removed)
        {
            MidiClipRemoved?.Invoke(this, clip);
        }

        return removed;
    }

    /// <summary>
    /// Removes a MIDI clip by its ID.
    /// </summary>
    /// <param name="clipId">The clip ID to remove.</param>
    /// <returns>True if removed successfully.</returns>
    public bool RemoveMidiClip(Guid clipId)
    {
        MidiClip? clip;
        lock (_lock)
        {
            clip = _midiClips.FirstOrDefault(c => c.Id == clipId);
        }

        return clip != null && RemoveMidiClip(clip);
    }

    /// <summary>
    /// Gets a MIDI clip by its ID.
    /// </summary>
    /// <param name="clipId">The clip ID.</param>
    /// <returns>The clip, or null if not found.</returns>
    public MidiClip? GetMidiClip(Guid clipId)
    {
        lock (_lock)
        {
            return _midiClips.FirstOrDefault(c => c.Id == clipId);
        }
    }

    /// <summary>
    /// Gets MIDI clips at a specific position.
    /// </summary>
    /// <param name="position">Position in beats.</param>
    /// <returns>List of clips at the position.</returns>
    public IReadOnlyList<MidiClip> GetMidiClipsAt(double position)
    {
        lock (_lock)
        {
            return _midiClips
                .Where(c => c.ContainsPosition(position))
                .OrderBy(c => c.TrackIndex)
                .ToList()
                .AsReadOnly();
        }
    }

    /// <summary>
    /// Gets MIDI clips within a range.
    /// </summary>
    /// <param name="startPosition">Start position in beats.</param>
    /// <param name="endPosition">End position in beats.</param>
    /// <returns>List of clips within or overlapping the range.</returns>
    public IReadOnlyList<MidiClip> GetMidiClipsInRange(double startPosition, double endPosition)
    {
        lock (_lock)
        {
            return _midiClips
                .Where(c => c.StartPosition < endPosition && c.EndPosition > startPosition)
                .OrderBy(c => c.StartPosition)
                .ToList()
                .AsReadOnly();
        }
    }

    /// <summary>
    /// Gets MIDI clips on a specific track.
    /// </summary>
    /// <param name="trackIndex">Track index.</param>
    /// <returns>List of clips on the track.</returns>
    public IReadOnlyList<MidiClip> GetMidiClipsOnTrack(int trackIndex)
    {
        lock (_lock)
        {
            return _midiClips
                .Where(c => c.TrackIndex == trackIndex)
                .OrderBy(c => c.StartPosition)
                .ToList()
                .AsReadOnly();
        }
    }

    #endregion

    #region Regions

    /// <summary>
    /// Adds a region to the arrangement.
    /// </summary>
    /// <param name="region">The region to add.</param>
    /// <returns>True if added successfully.</returns>
    public bool AddRegion(Region region)
    {
        ArgumentNullException.ThrowIfNull(region);

        lock (_lock)
        {
            if (_regions.Any(r => r.Id == region.Id))
                return false;

            _regions.Add(region);
        }

        RegionAdded?.Invoke(this, region);
        return true;
    }

    /// <summary>
    /// Creates and adds a new region.
    /// </summary>
    /// <param name="startPosition">Start position in beats.</param>
    /// <param name="endPosition">End position in beats.</param>
    /// <param name="name">Region name.</param>
    /// <param name="type">Region type.</param>
    /// <returns>The created region.</returns>
    public Region AddRegion(double startPosition, double endPosition, string name = "Region", RegionType type = RegionType.General)
    {
        var region = new Region(startPosition, endPosition, name, type);
        AddRegion(region);
        return region;
    }

    /// <summary>
    /// Sets the loop region for playback.
    /// </summary>
    /// <param name="startPosition">Loop start in beats.</param>
    /// <param name="endPosition">Loop end in beats.</param>
    /// <returns>The loop region.</returns>
    public Region SetLoopRegion(double startPosition, double endPosition)
    {
        // Remove existing loop regions
        lock (_lock)
        {
            var existingLoops = _regions.Where(r => r.Type == RegionType.Loop).ToList();
            foreach (var loop in existingLoops)
            {
                _regions.Remove(loop);
                RegionRemoved?.Invoke(this, loop);
            }
        }

        var loopRegion = Region.CreateLoop(startPosition, endPosition);
        AddRegion(loopRegion);
        return loopRegion;
    }

    /// <summary>
    /// Gets the active loop region.
    /// </summary>
    /// <returns>The active loop region, or null if none.</returns>
    public Region? GetLoopRegion()
    {
        lock (_lock)
        {
            return _regions.FirstOrDefault(r => r.Type == RegionType.Loop && r.IsActive);
        }
    }

    /// <summary>
    /// Removes a region from the arrangement.
    /// </summary>
    /// <param name="region">The region to remove.</param>
    /// <returns>True if removed successfully.</returns>
    public bool RemoveRegion(Region region)
    {
        ArgumentNullException.ThrowIfNull(region);

        if (region.IsLocked)
            return false;

        bool removed;
        lock (_lock)
        {
            removed = _regions.Remove(region);
        }

        if (removed)
        {
            RegionRemoved?.Invoke(this, region);
        }

        return removed;
    }

    /// <summary>
    /// Removes a region by its ID.
    /// </summary>
    /// <param name="regionId">The region ID to remove.</param>
    /// <returns>True if removed successfully.</returns>
    public bool RemoveRegion(Guid regionId)
    {
        Region? region;
        lock (_lock)
        {
            region = _regions.FirstOrDefault(r => r.Id == regionId);
        }

        return region != null && RemoveRegion(region);
    }

    /// <summary>
    /// Gets a region by its ID.
    /// </summary>
    /// <param name="regionId">The region ID.</param>
    /// <returns>The region, or null if not found.</returns>
    public Region? GetRegion(Guid regionId)
    {
        lock (_lock)
        {
            return _regions.FirstOrDefault(r => r.Id == regionId);
        }
    }

    /// <summary>
    /// Gets regions at a specific position.
    /// </summary>
    /// <param name="position">Position in beats.</param>
    /// <returns>List of regions at the position.</returns>
    public IReadOnlyList<Region> GetRegionsAt(double position)
    {
        lock (_lock)
        {
            return _regions
                .Where(r => r.ContainsPosition(position))
                .OrderBy(r => r.ZOrder)
                .ToList()
                .AsReadOnly();
        }
    }

    /// <summary>
    /// Gets regions within a range.
    /// </summary>
    /// <param name="startPosition">Start position in beats.</param>
    /// <param name="endPosition">End position in beats.</param>
    /// <returns>List of regions within or overlapping the range.</returns>
    public IReadOnlyList<Region> GetRegionsInRange(double startPosition, double endPosition)
    {
        lock (_lock)
        {
            return _regions
                .Where(r => r.Overlaps(startPosition, endPosition))
                .OrderBy(r => r.StartPosition)
                .ToList()
                .AsReadOnly();
        }
    }

    /// <summary>
    /// Gets regions of a specific type.
    /// </summary>
    /// <param name="type">Region type to filter by.</param>
    /// <returns>List of regions of the specified type.</returns>
    public IReadOnlyList<Region> GetRegionsByType(RegionType type)
    {
        lock (_lock)
        {
            return _regions
                .Where(r => r.Type == type)
                .OrderBy(r => r.StartPosition)
                .ToList()
                .AsReadOnly();
        }
    }

    #endregion

    #region Bulk Operations

    /// <summary>
    /// Clears all clips (audio and MIDI) from the arrangement.
    /// </summary>
    /// <param name="includeLockedClips">Whether to remove locked clips.</param>
    /// <returns>Number of clips removed.</returns>
    public int ClearClips(bool includeLockedClips = false)
    {
        int count = 0;

        lock (_lock)
        {
            var audioToRemove = includeLockedClips
                ? _audioClips.ToList()
                : _audioClips.Where(c => !c.IsLocked).ToList();

            var midiToRemove = includeLockedClips
                ? _midiClips.ToList()
                : _midiClips.Where(c => !c.IsLocked).ToList();

            foreach (var clip in audioToRemove)
            {
                _audioClips.Remove(clip);
                count++;
            }

            foreach (var clip in midiToRemove)
            {
                _midiClips.Remove(clip);
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Clears all regions from the arrangement.
    /// </summary>
    /// <param name="includeLockedRegions">Whether to remove locked regions.</param>
    /// <returns>Number of regions removed.</returns>
    public int ClearRegions(bool includeLockedRegions = false)
    {
        int count;

        lock (_lock)
        {
            if (includeLockedRegions)
            {
                count = _regions.Count;
                _regions.Clear();
            }
            else
            {
                var toRemove = _regions.Where(r => !r.IsLocked).ToList();
                count = toRemove.Count;
                foreach (var region in toRemove)
                {
                    _regions.Remove(region);
                }
            }
        }

        return count;
    }

    /// <summary>
    /// Gets the total length of the arrangement including all clips.
    /// </summary>
    public double TotalLengthWithClips
    {
        get
        {
            lock (_lock)
            {
                var sectionEnd = _sections.Count > 0 ? _sections.Max(s => s.EffectiveEndPosition) : 0;
                var audioEnd = _audioClips.Count > 0 ? _audioClips.Max(c => c.EndPosition) : 0;
                var midiEnd = _midiClips.Count > 0 ? _midiClips.Max(c => c.EndPosition) : 0;

                return Math.Max(Math.Max(sectionEnd, audioEnd), midiEnd);
            }
        }
    }

    #endregion

    #region Crossfades

    /// <summary>
    /// Adds a crossfade region to the arrangement.
    /// </summary>
    /// <param name="crossfade">The crossfade region to add.</param>
    /// <returns>True if added successfully.</returns>
    public bool AddCrossfade(CrossfadeRegion crossfade)
    {
        ArgumentNullException.ThrowIfNull(crossfade);

        lock (_lock)
        {
            if (_crossfades.Any(c => c.Id == crossfade.Id))
                return false;

            _crossfades.Add(crossfade);
        }

        CrossfadeAdded?.Invoke(this, crossfade);
        return true;
    }

    /// <summary>
    /// Creates and adds a crossfade between two overlapping audio clips.
    /// </summary>
    /// <param name="outgoingClip">The clip that is ending (fading out).</param>
    /// <param name="incomingClip">The clip that is starting (fading in).</param>
    /// <returns>The created crossfade region, or null if clips don't overlap.</returns>
    public CrossfadeRegion? CreateCrossfade(AudioClip outgoingClip, AudioClip incomingClip)
    {
        var crossfade = _crossfadeProcessor.CreateCrossfadeForClips(outgoingClip, incomingClip);
        if (crossfade == null)
            return null;

        AddCrossfade(crossfade);
        return crossfade;
    }

    /// <summary>
    /// Creates a crossfade region with specified parameters.
    /// </summary>
    /// <param name="startPosition">Start position in beats.</param>
    /// <param name="endPosition">End position in beats.</param>
    /// <param name="outgoingClipId">ID of the outgoing clip.</param>
    /// <param name="incomingClipId">ID of the incoming clip.</param>
    /// <param name="type">Crossfade curve type.</param>
    /// <param name="trackIndex">Track index.</param>
    /// <returns>The created crossfade region.</returns>
    public CrossfadeRegion CreateCrossfade(
        double startPosition,
        double endPosition,
        Guid outgoingClipId,
        Guid incomingClipId,
        CrossfadeType type = CrossfadeType.EqualPower,
        int trackIndex = 0)
    {
        var crossfade = new CrossfadeRegion(startPosition, endPosition, outgoingClipId, incomingClipId, type)
        {
            TrackIndex = trackIndex
        };
        AddCrossfade(crossfade);
        return crossfade;
    }

    /// <summary>
    /// Removes a crossfade region from the arrangement.
    /// </summary>
    /// <param name="crossfade">The crossfade to remove.</param>
    /// <returns>True if removed successfully.</returns>
    public bool RemoveCrossfade(CrossfadeRegion crossfade)
    {
        ArgumentNullException.ThrowIfNull(crossfade);

        if (crossfade.IsLocked)
            return false;

        bool removed;
        lock (_lock)
        {
            removed = _crossfades.Remove(crossfade);
        }

        if (removed)
        {
            CrossfadeRemoved?.Invoke(this, crossfade);
        }

        return removed;
    }

    /// <summary>
    /// Removes a crossfade region by its ID.
    /// </summary>
    /// <param name="crossfadeId">The crossfade ID to remove.</param>
    /// <returns>True if removed successfully.</returns>
    public bool RemoveCrossfade(Guid crossfadeId)
    {
        CrossfadeRegion? crossfade;
        lock (_lock)
        {
            crossfade = _crossfades.FirstOrDefault(c => c.Id == crossfadeId);
        }

        return crossfade != null && RemoveCrossfade(crossfade);
    }

    /// <summary>
    /// Gets a crossfade region by its ID.
    /// </summary>
    /// <param name="crossfadeId">The crossfade ID.</param>
    /// <returns>The crossfade, or null if not found.</returns>
    public CrossfadeRegion? GetCrossfade(Guid crossfadeId)
    {
        lock (_lock)
        {
            return _crossfades.FirstOrDefault(c => c.Id == crossfadeId);
        }
    }

    /// <summary>
    /// Gets crossfades at a specific position.
    /// </summary>
    /// <param name="position">Position in beats.</param>
    /// <returns>List of crossfades at the position.</returns>
    public IReadOnlyList<CrossfadeRegion> GetCrossfadesAt(double position)
    {
        lock (_lock)
        {
            return _crossfades
                .Where(c => c.ContainsPosition(position))
                .OrderBy(c => c.TrackIndex)
                .ToList()
                .AsReadOnly();
        }
    }

    /// <summary>
    /// Gets crossfades within a range.
    /// </summary>
    /// <param name="startPosition">Start position in beats.</param>
    /// <param name="endPosition">End position in beats.</param>
    /// <returns>List of crossfades within or overlapping the range.</returns>
    public IReadOnlyList<CrossfadeRegion> GetCrossfadesInRange(double startPosition, double endPosition)
    {
        lock (_lock)
        {
            return _crossfades
                .Where(c => c.StartPosition < endPosition && c.EndPosition > startPosition)
                .OrderBy(c => c.StartPosition)
                .ToList()
                .AsReadOnly();
        }
    }

    /// <summary>
    /// Gets crossfades on a specific track.
    /// </summary>
    /// <param name="trackIndex">Track index.</param>
    /// <returns>List of crossfades on the track.</returns>
    public IReadOnlyList<CrossfadeRegion> GetCrossfadesOnTrack(int trackIndex)
    {
        lock (_lock)
        {
            return _crossfades
                .Where(c => c.TrackIndex == trackIndex)
                .OrderBy(c => c.StartPosition)
                .ToList()
                .AsReadOnly();
        }
    }

    /// <summary>
    /// Gets crossfades involving a specific clip.
    /// </summary>
    /// <param name="clipId">The clip ID.</param>
    /// <returns>List of crossfades involving the clip.</returns>
    public IReadOnlyList<CrossfadeRegion> GetCrossfadesForClip(Guid clipId)
    {
        lock (_lock)
        {
            return _crossfades
                .Where(c => c.OutgoingClipId == clipId || c.IncomingClipId == clipId)
                .OrderBy(c => c.StartPosition)
                .ToList()
                .AsReadOnly();
        }
    }

    /// <summary>
    /// Updates the crossfade type.
    /// </summary>
    /// <param name="crossfade">The crossfade to update.</param>
    /// <param name="newType">The new crossfade type.</param>
    /// <returns>True if updated successfully.</returns>
    public bool UpdateCrossfadeType(CrossfadeRegion crossfade, CrossfadeType newType)
    {
        if (crossfade.IsLocked)
            return false;

        lock (_lock)
        {
            if (!_crossfades.Contains(crossfade))
                return false;

            crossfade.Type = newType;
        }

        CrossfadeModified?.Invoke(this, crossfade);
        return true;
    }

    /// <summary>
    /// Updates the crossfade length.
    /// </summary>
    /// <param name="crossfade">The crossfade to update.</param>
    /// <param name="newLength">The new length in beats.</param>
    /// <returns>True if updated successfully.</returns>
    public bool UpdateCrossfadeLength(CrossfadeRegion crossfade, double newLength)
    {
        if (crossfade.IsLocked || newLength <= 0)
            return false;

        lock (_lock)
        {
            if (!_crossfades.Contains(crossfade))
                return false;

            crossfade.EndPosition = crossfade.StartPosition + newLength;
        }

        CrossfadeModified?.Invoke(this, crossfade);
        return true;
    }

    /// <summary>
    /// Detects and creates crossfades for all overlapping audio clips.
    /// </summary>
    /// <returns>Number of crossfades created.</returns>
    public int DetectAndCreateCrossfades()
    {
        int count = 0;
        List<AudioClip> clips;

        lock (_lock)
        {
            clips = _audioClips.ToList();
        }

        // Group clips by track
        var trackGroups = clips.GroupBy(c => c.TrackIndex);

        foreach (var trackGroup in trackGroups)
        {
            var trackClips = trackGroup.OrderBy(c => c.StartPosition).ToList();

            for (int i = 0; i < trackClips.Count - 1; i++)
            {
                var currentClip = trackClips[i];
                var nextClip = trackClips[i + 1];

                // Check if they overlap
                if (CrossfadeProcessor.NeedsCrossfade(currentClip, nextClip))
                {
                    // Check if a crossfade already exists
                    var existingCrossfade = GetCrossfadesForClip(currentClip.Id)
                        .FirstOrDefault(c => c.OutgoingClipId == currentClip.Id && c.IncomingClipId == nextClip.Id);

                    if (existingCrossfade == null)
                    {
                        var crossfade = CreateCrossfade(currentClip, nextClip);
                        if (crossfade != null)
                        {
                            count++;
                        }
                    }
                }
            }
        }

        return count;
    }

    /// <summary>
    /// Clears all crossfades from the arrangement.
    /// </summary>
    /// <param name="includeLockedCrossfades">Whether to remove locked crossfades.</param>
    /// <returns>Number of crossfades removed.</returns>
    public int ClearCrossfades(bool includeLockedCrossfades = false)
    {
        int count;

        lock (_lock)
        {
            if (includeLockedCrossfades)
            {
                count = _crossfades.Count;
                _crossfades.Clear();
            }
            else
            {
                var toRemove = _crossfades.Where(c => !c.IsLocked).ToList();
                count = toRemove.Count;
                foreach (var crossfade in toRemove)
                {
                    _crossfades.Remove(crossfade);
                }
            }
        }

        return count;
    }

    /// <summary>
    /// Removes crossfades associated with a removed audio clip.
    /// </summary>
    /// <param name="clipId">The ID of the removed clip.</param>
    /// <returns>Number of crossfades removed.</returns>
    public int RemoveCrossfadesForClip(Guid clipId)
    {
        var crossfadesToRemove = GetCrossfadesForClip(clipId).ToList();
        int count = 0;

        foreach (var crossfade in crossfadesToRemove)
        {
            if (RemoveCrossfade(crossfade))
            {
                count++;
            }
        }

        return count;
    }

    #endregion
}
