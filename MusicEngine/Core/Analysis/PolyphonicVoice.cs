// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio analysis component.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Represents a single voice or melodic strand in polyphonic audio.
/// A voice contains a sequence of non-overlapping notes that form a coherent melodic line.
/// This is used to separate and edit individual voices in polyphonic recordings.
/// </summary>
public class PolyphonicVoice
{
    /// <summary>
    /// Index of this voice (0-based).
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Display color for this voice in the UI.
    /// </summary>
    public Color Color { get; set; }

    /// <summary>
    /// Collection of notes belonging to this voice, ordered by start time.
    /// </summary>
    public List<PolyphonicNote> Notes { get; } = new();

    /// <summary>
    /// Whether this voice is muted (excluded from playback/export).
    /// </summary>
    public bool IsMuted { get; set; }

    /// <summary>
    /// Whether this voice is soloed (only soloed voices play).
    /// </summary>
    public bool IsSoloed { get; set; }

    /// <summary>
    /// Optional name for this voice (e.g., "Soprano", "Bass", "Guitar").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Volume adjustment for this voice (0.0 to 2.0, 1.0 = unity).
    /// </summary>
    public float Volume { get; set; } = 1.0f;

    /// <summary>
    /// Pan position for this voice (-1.0 = left, 0.0 = center, 1.0 = right).
    /// </summary>
    public float Pan { get; set; } = 0.0f;

    /// <summary>
    /// Gets the total duration of this voice (from first note start to last note end).
    /// </summary>
    public double Duration
    {
        get
        {
            if (Notes.Count == 0)
                return 0;
            return Notes.Max(n => n.EndTime) - Notes.Min(n => n.StartTime);
        }
    }

    /// <summary>
    /// Gets the start time of this voice (first note start).
    /// </summary>
    public double StartTime => Notes.Count > 0 ? Notes.Min(n => n.StartTime) : 0;

    /// <summary>
    /// Gets the end time of this voice (last note end).
    /// </summary>
    public double EndTime => Notes.Count > 0 ? Notes.Max(n => n.EndTime) : 0;

    /// <summary>
    /// Gets the average pitch of all notes in this voice.
    /// </summary>
    public float AveragePitch => Notes.Count > 0 ? Notes.Average(n => n.Pitch) : 0;

    /// <summary>
    /// Gets the pitch range (highest - lowest) of notes in this voice.
    /// </summary>
    public float PitchRange => Notes.Count > 0 ? Notes.Max(n => n.Pitch) - Notes.Min(n => n.Pitch) : 0;

    /// <summary>
    /// Gets the number of notes in this voice.
    /// </summary>
    public int NoteCount => Notes.Count;

    /// <summary>
    /// Creates a new PolyphonicVoice with the specified index.
    /// </summary>
    /// <param name="index">Zero-based index of this voice.</param>
    public PolyphonicVoice(int index)
    {
        Index = index;
        Color = GetDefaultColor(index);
        Name = $"Voice {index + 1}";
    }

    /// <summary>
    /// Creates a new PolyphonicVoice with the specified index and color.
    /// </summary>
    /// <param name="index">Zero-based index of this voice.</param>
    /// <param name="color">Display color for this voice.</param>
    public PolyphonicVoice(int index, Color color)
        : this(index)
    {
        Color = color;
    }

    /// <summary>
    /// Gets the note at a specific time, or null if no note is active at that time.
    /// </summary>
    /// <param name="time">Time in seconds from the start of the audio.</param>
    /// <returns>The note active at the specified time, or null.</returns>
    public PolyphonicNote? GetNoteAt(double time)
    {
        return Notes.FirstOrDefault(n => time >= n.StartTime && time < n.EndTime);
    }

    /// <summary>
    /// Gets all notes that overlap with the specified time range.
    /// </summary>
    /// <param name="startTime">Start of the time range in seconds.</param>
    /// <param name="endTime">End of the time range in seconds.</param>
    /// <returns>Notes that overlap with the time range.</returns>
    public IEnumerable<PolyphonicNote> GetNotesInRange(double startTime, double endTime)
    {
        return Notes.Where(n => n.StartTime < endTime && n.EndTime > startTime);
    }

    /// <summary>
    /// Gets the note before the specified time.
    /// </summary>
    /// <param name="time">Time in seconds.</param>
    /// <returns>The most recent note that ends before the specified time, or null.</returns>
    public PolyphonicNote? GetNoteBefore(double time)
    {
        return Notes
            .Where(n => n.EndTime <= time)
            .OrderByDescending(n => n.EndTime)
            .FirstOrDefault();
    }

    /// <summary>
    /// Gets the note after the specified time.
    /// </summary>
    /// <param name="time">Time in seconds.</param>
    /// <returns>The next note that starts after the specified time, or null.</returns>
    public PolyphonicNote? GetNoteAfter(double time)
    {
        return Notes
            .Where(n => n.StartTime >= time)
            .OrderBy(n => n.StartTime)
            .FirstOrDefault();
    }

    /// <summary>
    /// Adds a note to this voice, maintaining chronological order.
    /// </summary>
    /// <param name="note">The note to add.</param>
    public void AddNote(PolyphonicNote note)
    {
        note.VoiceIndex = Index;

        // Find the correct insertion position
        int insertIndex = Notes.Count;
        for (int i = 0; i < Notes.Count; i++)
        {
            if (Notes[i].StartTime > note.StartTime)
            {
                insertIndex = i;
                break;
            }
        }

        Notes.Insert(insertIndex, note);
    }

    /// <summary>
    /// Removes a note from this voice.
    /// </summary>
    /// <param name="note">The note to remove.</param>
    /// <returns>True if the note was removed, false if not found.</returns>
    public bool RemoveNote(PolyphonicNote note)
    {
        return Notes.Remove(note);
    }

    /// <summary>
    /// Removes a note by its ID.
    /// </summary>
    /// <param name="noteId">The ID of the note to remove.</param>
    /// <returns>True if the note was removed, false if not found.</returns>
    public bool RemoveNoteById(Guid noteId)
    {
        var note = Notes.FirstOrDefault(n => n.Id == noteId);
        return note != null && Notes.Remove(note);
    }

    /// <summary>
    /// Sorts notes by start time.
    /// Call this after modifying note times externally.
    /// </summary>
    public void SortNotes()
    {
        Notes.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
    }

    /// <summary>
    /// Transposes all notes in this voice by the specified number of semitones.
    /// </summary>
    /// <param name="semitones">Number of semitones to transpose (positive = up, negative = down).</param>
    public void Transpose(float semitones)
    {
        foreach (var note in Notes)
        {
            note.Pitch += semitones;

            // Also update pitch contour if present
            if (note.PitchContour != null)
            {
                for (int i = 0; i < note.PitchContour.Length; i++)
                {
                    note.PitchContour[i] += semitones;
                }
            }
        }
    }

    /// <summary>
    /// Quantizes all notes in this voice to the nearest semitone.
    /// </summary>
    /// <param name="strength">Quantization strength (0.0 to 1.0).</param>
    public void QuantizeAll(float strength = 1.0f)
    {
        foreach (var note in Notes)
        {
            note.QuantizeToSemitone(strength);
        }
    }

    /// <summary>
    /// Quantizes all notes in this voice to a specific scale.
    /// </summary>
    /// <param name="scaleNotes">Array of pitch classes (0-11) in the scale.</param>
    /// <param name="strength">Quantization strength (0.0 to 1.0).</param>
    public void QuantizeToScale(int[] scaleNotes, float strength = 1.0f)
    {
        foreach (var note in Notes)
        {
            note.QuantizeToScale(scaleNotes, strength);
        }
    }

    /// <summary>
    /// Resets all pitch modifications in this voice to original values.
    /// </summary>
    public void ResetAllPitches()
    {
        foreach (var note in Notes)
        {
            note.ResetPitch();
        }
    }

    /// <summary>
    /// Resets all modifications (pitch and formant) in this voice.
    /// </summary>
    public void ResetAll()
    {
        foreach (var note in Notes)
        {
            note.ResetAll();
        }
    }

    /// <summary>
    /// Gets all selected notes in this voice.
    /// </summary>
    public IEnumerable<PolyphonicNote> GetSelectedNotes()
    {
        return Notes.Where(n => n.IsSelected);
    }

    /// <summary>
    /// Selects all notes in this voice.
    /// </summary>
    public void SelectAll()
    {
        foreach (var note in Notes)
        {
            note.IsSelected = true;
        }
    }

    /// <summary>
    /// Deselects all notes in this voice.
    /// </summary>
    public void DeselectAll()
    {
        foreach (var note in Notes)
        {
            note.IsSelected = false;
        }
    }

    /// <summary>
    /// Creates a deep copy of this voice and all its notes.
    /// </summary>
    public PolyphonicVoice Clone()
    {
        var clone = new PolyphonicVoice(Index, Color)
        {
            Name = Name,
            IsMuted = IsMuted,
            IsSoloed = IsSoloed,
            Volume = Volume,
            Pan = Pan
        };

        foreach (var note in Notes)
        {
            clone.Notes.Add(note.Clone());
        }

        return clone;
    }

    /// <summary>
    /// Gets a default color for a voice based on its index.
    /// </summary>
    private static Color GetDefaultColor(int index)
    {
        // Predefined colors for first 8 voices, then cycle through with variations
        Color[] defaultColors =
        {
            Color.FromArgb(255, 100, 100),  // Red
            Color.FromArgb(100, 150, 255),  // Blue
            Color.FromArgb(100, 255, 100),  // Green
            Color.FromArgb(255, 200, 100),  // Orange
            Color.FromArgb(200, 100, 255),  // Purple
            Color.FromArgb(255, 255, 100),  // Yellow
            Color.FromArgb(100, 255, 255),  // Cyan
            Color.FromArgb(255, 150, 200),  // Pink
        };

        if (index < defaultColors.Length)
        {
            return defaultColors[index];
        }

        // Generate colors for additional voices using HSV color space
        float hue = (index * 137.5f) % 360; // Golden angle for good distribution
        return HsvToRgb(hue, 0.7f, 0.9f);
    }

    /// <summary>
    /// Converts HSV color values to RGB Color.
    /// </summary>
    private static Color HsvToRgb(float hue, float saturation, float value)
    {
        int hi = (int)(hue / 60) % 6;
        float f = hue / 60 - (int)(hue / 60);
        int v = (int)(value * 255);
        int p = (int)(value * (1 - saturation) * 255);
        int q = (int)(value * (1 - f * saturation) * 255);
        int t = (int)(value * (1 - (1 - f) * saturation) * 255);

        return hi switch
        {
            0 => Color.FromArgb(v, t, p),
            1 => Color.FromArgb(q, v, p),
            2 => Color.FromArgb(p, v, t),
            3 => Color.FromArgb(p, q, v),
            4 => Color.FromArgb(t, p, v),
            _ => Color.FromArgb(v, p, q)
        };
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{Name}: {Notes.Count} notes, {StartTime:F2}s - {EndTime:F2}s";
    }
}
