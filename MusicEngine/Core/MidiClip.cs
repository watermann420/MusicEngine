// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicEngine.Core;

/// <summary>
/// Represents a MIDI clip in the arrangement timeline.
/// MIDI clips contain note events and can reference or embed a Pattern.
/// </summary>
public class MidiClip : IEquatable<MidiClip>
{
    /// <summary>Unique identifier for this clip.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Display name of the clip.</summary>
    public string Name { get; set; } = "MIDI Clip";

    /// <summary>Start position in the timeline (in beats).</summary>
    public double StartPosition { get; set; }

    /// <summary>Length of the clip in beats.</summary>
    public double Length { get; set; }

    /// <summary>Index of the track this clip belongs to.</summary>
    public int TrackIndex { get; set; }

    /// <summary>MIDI channel for this clip (0-15).</summary>
    public int MidiChannel { get; set; }

    /// <summary>Reference to the Pattern containing the note events.</summary>
    public Pattern? Pattern { get; set; }

    /// <summary>ID of the referenced pattern (for serialization).</summary>
    public Guid? PatternId { get; set; }

    /// <summary>Embedded note events (if not using a Pattern reference).</summary>
    public List<NoteEvent> Notes { get; set; } = [];

    /// <summary>Whether this clip loops its content within its length.</summary>
    public bool IsLooping { get; set; }

    /// <summary>Loop length in beats (if different from content length).</summary>
    public double? LoopLength { get; set; }

    /// <summary>Velocity offset applied to all notes (-127 to +127).</summary>
    public int VelocityOffset { get; set; }

    /// <summary>Velocity scale factor (0.0 to 2.0, 1.0 = no change).</summary>
    public double VelocityScale { get; set; } = 1.0;

    /// <summary>Transpose offset in semitones (-48 to +48).</summary>
    public int TransposeOffset { get; set; }

    /// <summary>Whether this clip is muted.</summary>
    public bool IsMuted { get; set; }

    /// <summary>Whether this clip is locked from editing.</summary>
    public bool IsLocked { get; set; }

    /// <summary>Whether this clip is selected in the UI.</summary>
    public bool IsSelected { get; set; }

    /// <summary>Color for visual representation (hex format).</summary>
    public string Color { get; set; } = "#2ECC71";

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    /// <summary>Last modification timestamp.</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the end position of this clip in the timeline.
    /// </summary>
    public double EndPosition => StartPosition + Length;

    /// <summary>
    /// Gets the effective loop length.
    /// </summary>
    public double EffectiveLoopLength => LoopLength ?? (Pattern?.LoopLength ?? Length);

    /// <summary>
    /// Gets all note events, either from the embedded list or the referenced Pattern.
    /// </summary>
    public IReadOnlyList<NoteEvent> AllNotes =>
        Pattern?.Events as IReadOnlyList<NoteEvent> ?? Notes;

    /// <summary>
    /// Creates a new MIDI clip.
    /// </summary>
    public MidiClip()
    {
    }

    /// <summary>
    /// Creates a new MIDI clip with position and length.
    /// </summary>
    /// <param name="startPosition">Start position in beats.</param>
    /// <param name="length">Length in beats.</param>
    /// <param name="trackIndex">Track index.</param>
    public MidiClip(double startPosition, double length, int trackIndex = 0)
    {
        StartPosition = startPosition;
        Length = length;
        TrackIndex = trackIndex;
    }

    /// <summary>
    /// Creates a new MIDI clip from a Pattern.
    /// </summary>
    /// <param name="pattern">The pattern to reference.</param>
    /// <param name="startPosition">Start position in beats.</param>
    /// <param name="trackIndex">Track index.</param>
    public MidiClip(Pattern pattern, double startPosition, int trackIndex = 0)
    {
        Pattern = pattern;
        PatternId = pattern.Id;
        Name = !string.IsNullOrEmpty(pattern.Name) ? pattern.Name : "MIDI Clip";
        StartPosition = startPosition;
        Length = pattern.LoopLength;
        TrackIndex = trackIndex;
        IsLooping = pattern.IsLooping;
    }

    /// <summary>
    /// Checks if a given position falls within this clip.
    /// </summary>
    /// <param name="position">Position in beats to check.</param>
    /// <returns>True if the position is within the clip.</returns>
    public bool ContainsPosition(double position)
    {
        return position >= StartPosition && position < EndPosition;
    }

    /// <summary>
    /// Gets the notes that should play at a given timeline position.
    /// </summary>
    /// <param name="startBeat">Start of the range (timeline position).</param>
    /// <param name="endBeat">End of the range (timeline position).</param>
    /// <returns>Notes that fall within the range, with adjusted timing and properties.</returns>
    public IEnumerable<NoteEvent> GetNotesInRange(double startBeat, double endBeat)
    {
        if (IsMuted) yield break;
        if (endBeat <= StartPosition || startBeat >= EndPosition) yield break;

        var clipStart = Math.Max(startBeat, StartPosition) - StartPosition;
        var clipEnd = Math.Min(endBeat, EndPosition) - StartPosition;
        var loopLen = EffectiveLoopLength;

        foreach (var note in AllNotes)
        {
            var noteBeat = note.Beat;

            if (IsLooping && loopLen > 0)
            {
                // Handle looping - find all instances of this note within the range
                var iterations = (int)Math.Ceiling(Length / loopLen);
                for (int i = 0; i < iterations; i++)
                {
                    var instanceBeat = noteBeat + (i * loopLen);
                    if (instanceBeat >= clipStart && instanceBeat < clipEnd && instanceBeat < Length)
                    {
                        yield return CreateAdjustedNote(note, instanceBeat + StartPosition);
                    }
                }
            }
            else
            {
                // Non-looping - just check if note is in range
                if (noteBeat >= clipStart && noteBeat < clipEnd)
                {
                    yield return CreateAdjustedNote(note, noteBeat + StartPosition);
                }
            }
        }
    }

    /// <summary>
    /// Creates an adjusted copy of a note with transformations applied.
    /// </summary>
    private NoteEvent CreateAdjustedNote(NoteEvent source, double absoluteBeat)
    {
        // Calculate adjusted velocity
        var adjustedVelocity = (int)(source.Velocity * VelocityScale) + VelocityOffset;
        adjustedVelocity = Math.Clamp(adjustedVelocity, 1, 127);

        // Calculate adjusted note (transpose)
        var adjustedNote = source.Note + TransposeOffset;
        adjustedNote = Math.Clamp(adjustedNote, 0, 127);

        return new NoteEvent
        {
            Note = adjustedNote,
            Beat = absoluteBeat,
            Duration = source.Duration,
            Velocity = adjustedVelocity,
            SourceInfo = source.SourceInfo
        };
    }

    /// <summary>
    /// Adds a note to the clip's embedded note list.
    /// </summary>
    /// <param name="note">MIDI note number (0-127).</param>
    /// <param name="beat">Beat position within the clip.</param>
    /// <param name="duration">Duration in beats.</param>
    /// <param name="velocity">Velocity (1-127).</param>
    /// <returns>This clip for chaining.</returns>
    public MidiClip AddNote(int note, double beat, double duration, int velocity = 100)
    {
        if (IsLocked) return this;

        MidiValidation.ValidateNote(note);
        MidiValidation.ValidateVelocity(velocity);

        Notes.Add(new NoteEvent
        {
            Note = note,
            Beat = beat,
            Duration = duration,
            Velocity = velocity
        });

        Touch();
        return this;
    }

    /// <summary>
    /// Removes a note from the clip.
    /// </summary>
    /// <param name="note">The note event to remove.</param>
    /// <returns>True if removed successfully.</returns>
    public bool RemoveNote(NoteEvent note)
    {
        if (IsLocked) return false;

        var removed = Notes.Remove(note);
        if (removed) Touch();
        return removed;
    }

    /// <summary>
    /// Clears all notes from the clip.
    /// </summary>
    public void ClearNotes()
    {
        if (IsLocked) return;

        Notes.Clear();
        Touch();
    }

    /// <summary>
    /// Moves the clip to a new position.
    /// </summary>
    /// <param name="newStartPosition">New start position in beats.</param>
    public void MoveTo(double newStartPosition)
    {
        if (IsLocked) return;
        StartPosition = newStartPosition;
        Touch();
    }

    /// <summary>
    /// Resizes the clip.
    /// </summary>
    /// <param name="newLength">New length in beats.</param>
    public void Resize(double newLength)
    {
        if (IsLocked) return;
        if (newLength <= 0) return;

        Length = newLength;
        Touch();
    }

    /// <summary>
    /// Splits this clip at the specified position.
    /// </summary>
    /// <param name="splitPosition">Position to split at (in timeline beats).</param>
    /// <returns>The new clip created after the split point, or null if split is invalid.</returns>
    public MidiClip? Split(double splitPosition)
    {
        if (IsLocked) return null;
        if (!ContainsPosition(splitPosition)) return null;
        if (splitPosition <= StartPosition || splitPosition >= EndPosition) return null;

        var splitOffset = splitPosition - StartPosition;
        var newClipLength = Length - splitOffset;

        // Create new clip for the second part
        var newClip = new MidiClip
        {
            Name = Name + " (split)",
            StartPosition = splitPosition,
            Length = newClipLength,
            TrackIndex = TrackIndex,
            MidiChannel = MidiChannel,
            IsLooping = IsLooping,
            LoopLength = LoopLength,
            VelocityOffset = VelocityOffset,
            VelocityScale = VelocityScale,
            TransposeOffset = TransposeOffset,
            Color = Color
        };

        // Copy notes that belong to the second part
        foreach (var note in Notes.Where(n => n.Beat >= splitOffset).ToList())
        {
            newClip.Notes.Add(new NoteEvent
            {
                Note = note.Note,
                Beat = note.Beat - splitOffset,
                Duration = note.Duration,
                Velocity = note.Velocity,
                SourceInfo = note.SourceInfo
            });
            Notes.Remove(note);
        }

        // Adjust this clip
        Length = splitOffset;

        // Trim notes that extend past the split point
        foreach (var note in Notes.Where(n => n.Beat + n.Duration > splitOffset))
        {
            note.Duration = splitOffset - note.Beat;
        }

        Touch();

        return newClip;
    }

    /// <summary>
    /// Creates a duplicate of this clip at a new position.
    /// </summary>
    /// <param name="newStartPosition">Start position for the duplicate.</param>
    /// <returns>A new clip with the same properties.</returns>
    public MidiClip Duplicate(double? newStartPosition = null)
    {
        var duplicate = new MidiClip
        {
            Name = Name + " (copy)",
            StartPosition = newStartPosition ?? EndPosition,
            Length = Length,
            TrackIndex = TrackIndex,
            MidiChannel = MidiChannel,
            Pattern = Pattern,
            PatternId = PatternId,
            IsLooping = IsLooping,
            LoopLength = LoopLength,
            VelocityOffset = VelocityOffset,
            VelocityScale = VelocityScale,
            TransposeOffset = TransposeOffset,
            Color = Color
        };

        // Copy embedded notes
        foreach (var note in Notes)
        {
            duplicate.Notes.Add(new NoteEvent
            {
                Note = note.Note,
                Beat = note.Beat,
                Duration = note.Duration,
                Velocity = note.Velocity,
                SourceInfo = note.SourceInfo
            });
        }

        return duplicate;
    }

    /// <summary>
    /// Quantizes all notes to a grid.
    /// </summary>
    /// <param name="gridSize">Grid size in beats (e.g., 0.25 for 1/16 notes).</param>
    public void Quantize(double gridSize = 0.25)
    {
        if (IsLocked) return;
        if (gridSize <= 0) return;

        foreach (var note in Notes)
        {
            note.Beat = Math.Round(note.Beat / gridSize) * gridSize;
        }

        Touch();
    }

    /// <summary>
    /// Updates the modification timestamp.
    /// </summary>
    public void Touch()
    {
        ModifiedAt = DateTime.UtcNow;
    }

    public bool Equals(MidiClip? other)
    {
        if (other is null) return false;
        return Id.Equals(other.Id);
    }

    public override bool Equals(object? obj) => obj is MidiClip other && Equals(other);

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(MidiClip? left, MidiClip? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(MidiClip? left, MidiClip? right) => !(left == right);

    public override string ToString() =>
        $"[MIDI] {Name} @{StartPosition:F2} ({Length:F2} beats) Track {TrackIndex} ({AllNotes.Count} notes)";
}
