// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Equalizer effect.

using System;
using System.Collections.Generic;
using System.Linq;


namespace MusicEngine.Core;


/// <summary>
/// Direction for quantizing notes when the note is between two scale tones.
/// </summary>
public enum QuantizeDirection
{
    /// <summary>Quantize to the nearest scale tone.</summary>
    Nearest,

    /// <summary>Always quantize upward to the next scale tone.</summary>
    Up,

    /// <summary>Always quantize downward to the previous scale tone.</summary>
    Down
}


/// <summary>
/// Static class providing methods for quantizing MIDI notes to scales and chords.
/// Supports standard scale types, custom scales, and configurable quantization strength.
/// </summary>
public static class ScaleQuantizer
{
    #region Single Note Quantization

    /// <summary>
    /// Quantizes a MIDI note to the nearest note in a scale.
    /// </summary>
    /// <param name="midiNote">The MIDI note number to quantize (0-127).</param>
    /// <param name="scale">The scale type to quantize to.</param>
    /// <param name="root">The root note of the scale.</param>
    /// <param name="direction">Direction preference when quantizing (Nearest, Up, or Down).</param>
    /// <returns>The quantized MIDI note number.</returns>
    public static int QuantizeNote(int midiNote, ScaleType scale, NoteName root, QuantizeDirection direction = QuantizeDirection.Nearest)
    {
        return QuantizeNote(midiNote, scale, root, direction, 100);
    }

    /// <summary>
    /// Quantizes a MIDI note to the nearest note in a scale with adjustable strength.
    /// </summary>
    /// <param name="midiNote">The MIDI note number to quantize (0-127).</param>
    /// <param name="scale">The scale type to quantize to.</param>
    /// <param name="root">The root note of the scale.</param>
    /// <param name="direction">Direction preference when quantizing (Nearest, Up, or Down).</param>
    /// <param name="strengthPercent">Quantization strength (0-100%). 0% = no change, 100% = full quantization.</param>
    /// <returns>The quantized MIDI note number.</returns>
    public static int QuantizeNote(int midiNote, ScaleType scale, NoteName root, QuantizeDirection direction, int strengthPercent)
    {
        bool[] scaleMap = GetScaleMap(scale, root);
        return QuantizeNoteInternal(midiNote, scaleMap, direction, strengthPercent);
    }

    /// <summary>
    /// Quantizes a MIDI note using a custom scale definition.
    /// </summary>
    /// <param name="midiNote">The MIDI note number to quantize (0-127).</param>
    /// <param name="customScale">A boolean array of 12 elements where true indicates a valid scale tone.
    /// Index 0 = C, 1 = C#, 2 = D, etc.</param>
    /// <param name="direction">Direction preference when quantizing (Nearest, Up, or Down).</param>
    /// <param name="strengthPercent">Quantization strength (0-100%). 0% = no change, 100% = full quantization.</param>
    /// <returns>The quantized MIDI note number.</returns>
    /// <exception cref="ArgumentNullException">Thrown when customScale is null.</exception>
    /// <exception cref="ArgumentException">Thrown when customScale does not have exactly 12 elements or has no valid tones.</exception>
    public static int QuantizeNote(int midiNote, bool[] customScale, QuantizeDirection direction = QuantizeDirection.Nearest, int strengthPercent = 100)
    {
        ValidateCustomScale(customScale);
        return QuantizeNoteInternal(midiNote, customScale, direction, strengthPercent);
    }

    /// <summary>
    /// Internal implementation for quantizing a single note.
    /// </summary>
    private static int QuantizeNoteInternal(int midiNote, bool[] scaleMap, QuantizeDirection direction, int strengthPercent)
    {
        midiNote = Math.Clamp(midiNote, 0, 127);
        strengthPercent = Math.Clamp(strengthPercent, 0, 100);

        if (strengthPercent == 0)
            return midiNote;

        int pitchClass = midiNote % 12;

        // If already in scale, no quantization needed
        if (scaleMap[pitchClass])
            return midiNote;

        int targetNote = direction switch
        {
            QuantizeDirection.Up => FindNoteUp(midiNote, scaleMap),
            QuantizeDirection.Down => FindNoteDown(midiNote, scaleMap),
            _ => FindNearestNote(midiNote, scaleMap)
        };

        // Apply strength - for partial quantization
        if (strengthPercent < 100)
        {
            // Probabilistic quantization: only quantize if random check passes
            // For a deterministic approach, we could round-robin, but probabilistic
            // matches musical expectations for "partial" quantization
            double threshold = strengthPercent / 100.0;
            int distance = Math.Abs(targetNote - midiNote);

            // Use note position as pseudo-random seed for deterministic behavior
            double pseudoRandom = ((midiNote * 7 + distance * 13) % 100) / 100.0;

            if (pseudoRandom > threshold)
                return midiNote;
        }

        return Math.Clamp(targetNote, 0, 127);
    }

    /// <summary>
    /// Finds the nearest scale note to the given MIDI note.
    /// </summary>
    private static int FindNearestNote(int midiNote, bool[] scaleMap)
    {
        int pitchClass = midiNote % 12;
        int octave = midiNote / 12;

        int distanceUp = 0;
        int distanceDown = 0;

        // Search upward
        for (int i = 1; i <= 12; i++)
        {
            int checkPitch = (pitchClass + i) % 12;
            if (scaleMap[checkPitch])
            {
                distanceUp = i;
                break;
            }
        }

        // Search downward
        for (int i = 1; i <= 12; i++)
        {
            int checkPitch = (pitchClass - i + 12) % 12;
            if (scaleMap[checkPitch])
            {
                distanceDown = i;
                break;
            }
        }

        // Choose the closest; prefer down (lower note) on tie
        if (distanceDown <= distanceUp)
        {
            return midiNote - distanceDown;
        }
        else
        {
            return midiNote + distanceUp;
        }
    }

    /// <summary>
    /// Finds the next scale note above the given MIDI note.
    /// </summary>
    private static int FindNoteUp(int midiNote, bool[] scaleMap)
    {
        int pitchClass = midiNote % 12;

        for (int i = 1; i <= 12; i++)
        {
            int checkPitch = (pitchClass + i) % 12;
            if (scaleMap[checkPitch])
            {
                return midiNote + i;
            }
        }

        return midiNote; // Should never reach here if scale has at least one note
    }

    /// <summary>
    /// Finds the next scale note below the given MIDI note.
    /// </summary>
    private static int FindNoteDown(int midiNote, bool[] scaleMap)
    {
        int pitchClass = midiNote % 12;

        for (int i = 1; i <= 12; i++)
        {
            int checkPitch = (pitchClass - i + 12) % 12;
            if (scaleMap[checkPitch])
            {
                return midiNote - i;
            }
        }

        return midiNote; // Should never reach here if scale has at least one note
    }

    #endregion

    #region Pattern Quantization

    /// <summary>
    /// Quantizes all notes in a pattern to a scale.
    /// </summary>
    /// <param name="pattern">The pattern to quantize.</param>
    /// <param name="scale">The scale type to quantize to.</param>
    /// <param name="root">The root note of the scale.</param>
    /// <param name="direction">Direction preference when quantizing (Nearest, Up, or Down).</param>
    /// <returns>A new pattern with all notes quantized to the scale.</returns>
    /// <exception cref="ArgumentNullException">Thrown when pattern is null.</exception>
    public static Pattern QuantizePattern(Pattern pattern, ScaleType scale, NoteName root, QuantizeDirection direction = QuantizeDirection.Nearest)
    {
        return QuantizePattern(pattern, scale, root, direction, 100);
    }

    /// <summary>
    /// Quantizes all notes in a pattern to a scale with adjustable strength.
    /// </summary>
    /// <param name="pattern">The pattern to quantize.</param>
    /// <param name="scale">The scale type to quantize to.</param>
    /// <param name="root">The root note of the scale.</param>
    /// <param name="direction">Direction preference when quantizing (Nearest, Up, or Down).</param>
    /// <param name="strengthPercent">Quantization strength (0-100%). 0% = no change, 100% = full quantization.</param>
    /// <returns>A new pattern with all notes quantized to the scale.</returns>
    /// <exception cref="ArgumentNullException">Thrown when pattern is null.</exception>
    public static Pattern QuantizePattern(Pattern pattern, ScaleType scale, NoteName root, QuantizeDirection direction, int strengthPercent)
    {
        if (pattern == null)
            throw new ArgumentNullException(nameof(pattern));

        bool[] scaleMap = GetScaleMap(scale, root);
        return QuantizePatternInternal(pattern, scaleMap, direction, strengthPercent);
    }

    /// <summary>
    /// Quantizes all notes in a pattern using a custom scale definition.
    /// </summary>
    /// <param name="pattern">The pattern to quantize.</param>
    /// <param name="customScale">A boolean array of 12 elements where true indicates a valid scale tone.</param>
    /// <param name="direction">Direction preference when quantizing (Nearest, Up, or Down).</param>
    /// <param name="strengthPercent">Quantization strength (0-100%). 0% = no change, 100% = full quantization.</param>
    /// <returns>A new pattern with all notes quantized to the custom scale.</returns>
    /// <exception cref="ArgumentNullException">Thrown when pattern or customScale is null.</exception>
    /// <exception cref="ArgumentException">Thrown when customScale does not have exactly 12 elements.</exception>
    public static Pattern QuantizePattern(Pattern pattern, bool[] customScale, QuantizeDirection direction = QuantizeDirection.Nearest, int strengthPercent = 100)
    {
        if (pattern == null)
            throw new ArgumentNullException(nameof(pattern));

        ValidateCustomScale(customScale);
        return QuantizePatternInternal(pattern, customScale, direction, strengthPercent);
    }

    /// <summary>
    /// Internal implementation for pattern quantization.
    /// </summary>
    private static Pattern QuantizePatternInternal(Pattern pattern, bool[] scaleMap, QuantizeDirection direction, int strengthPercent)
    {
        var newPattern = ClonePattern(pattern);

        foreach (var ev in newPattern.Events)
        {
            ev.Note = QuantizeNoteInternal(ev.Note, scaleMap, direction, strengthPercent);
        }

        return newPattern;
    }

    #endregion

    #region Chord Quantization

    /// <summary>
    /// Quantizes a MIDI note to the nearest note in a chord.
    /// </summary>
    /// <param name="midiNote">The MIDI note number to quantize (0-127).</param>
    /// <param name="chordRoot">The root note of the chord.</param>
    /// <param name="chordType">The type of chord.</param>
    /// <param name="direction">Direction preference when quantizing (Nearest, Up, or Down).</param>
    /// <returns>The quantized MIDI note number.</returns>
    public static int QuantizeToChord(int midiNote, int chordRoot, ChordType chordType, QuantizeDirection direction = QuantizeDirection.Nearest)
    {
        return QuantizeToChord(midiNote, chordRoot, chordType, direction, 100);
    }

    /// <summary>
    /// Quantizes a MIDI note to the nearest note in a chord with adjustable strength.
    /// </summary>
    /// <param name="midiNote">The MIDI note number to quantize (0-127).</param>
    /// <param name="chordRoot">The root note of the chord.</param>
    /// <param name="chordType">The type of chord.</param>
    /// <param name="direction">Direction preference when quantizing (Nearest, Up, or Down).</param>
    /// <param name="strengthPercent">Quantization strength (0-100%). 0% = no change, 100% = full quantization.</param>
    /// <returns>The quantized MIDI note number.</returns>
    public static int QuantizeToChord(int midiNote, int chordRoot, ChordType chordType, QuantizeDirection direction, int strengthPercent)
    {
        bool[] chordMap = GetChordMap(chordRoot, chordType);
        return QuantizeNoteInternal(midiNote, chordMap, direction, strengthPercent);
    }

    /// <summary>
    /// Quantizes a MIDI note to the nearest note in a chord using NoteName for the root.
    /// </summary>
    /// <param name="midiNote">The MIDI note number to quantize (0-127).</param>
    /// <param name="chordRoot">The root note name of the chord.</param>
    /// <param name="chordType">The type of chord.</param>
    /// <param name="direction">Direction preference when quantizing (Nearest, Up, or Down).</param>
    /// <param name="strengthPercent">Quantization strength (0-100%). 0% = no change, 100% = full quantization.</param>
    /// <returns>The quantized MIDI note number.</returns>
    public static int QuantizeToChord(int midiNote, NoteName chordRoot, ChordType chordType, QuantizeDirection direction = QuantizeDirection.Nearest, int strengthPercent = 100)
    {
        bool[] chordMap = GetChordMap((int)chordRoot, chordType);
        return QuantizeNoteInternal(midiNote, chordMap, direction, strengthPercent);
    }

    /// <summary>
    /// Quantizes all notes in a pattern to a chord.
    /// </summary>
    /// <param name="pattern">The pattern to quantize.</param>
    /// <param name="chordRoot">The root note of the chord.</param>
    /// <param name="chordType">The type of chord.</param>
    /// <param name="direction">Direction preference when quantizing (Nearest, Up, or Down).</param>
    /// <param name="strengthPercent">Quantization strength (0-100%). 0% = no change, 100% = full quantization.</param>
    /// <returns>A new pattern with all notes quantized to the chord.</returns>
    /// <exception cref="ArgumentNullException">Thrown when pattern is null.</exception>
    public static Pattern QuantizePatternToChord(Pattern pattern, int chordRoot, ChordType chordType, QuantizeDirection direction = QuantizeDirection.Nearest, int strengthPercent = 100)
    {
        if (pattern == null)
            throw new ArgumentNullException(nameof(pattern));

        bool[] chordMap = GetChordMap(chordRoot, chordType);
        return QuantizePatternInternal(pattern, chordMap, direction, strengthPercent);
    }

    #endregion

    #region Scale/Chord Map Generation

    /// <summary>
    /// Gets the scale intervals for a given scale type.
    /// </summary>
    private static readonly Dictionary<ScaleType, int[]> ScaleIntervals = new()
    {
        { ScaleType.Major, new[] { 0, 2, 4, 5, 7, 9, 11 } },
        { ScaleType.NaturalMinor, new[] { 0, 2, 3, 5, 7, 8, 10 } },
        { ScaleType.HarmonicMinor, new[] { 0, 2, 3, 5, 7, 8, 11 } },
        { ScaleType.MelodicMinor, new[] { 0, 2, 3, 5, 7, 9, 11 } },
        { ScaleType.Dorian, new[] { 0, 2, 3, 5, 7, 9, 10 } },
        { ScaleType.Phrygian, new[] { 0, 1, 3, 5, 7, 8, 10 } },
        { ScaleType.Lydian, new[] { 0, 2, 4, 6, 7, 9, 11 } },
        { ScaleType.Mixolydian, new[] { 0, 2, 4, 5, 7, 9, 10 } },
        { ScaleType.Locrian, new[] { 0, 1, 3, 5, 6, 8, 10 } },
        { ScaleType.PentatonicMajor, new[] { 0, 2, 4, 7, 9 } },
        { ScaleType.PentatonicMinor, new[] { 0, 3, 5, 7, 10 } },
        { ScaleType.Blues, new[] { 0, 3, 5, 6, 7, 10 } },
        { ScaleType.WholeTone, new[] { 0, 2, 4, 6, 8, 10 } },
        { ScaleType.Chromatic, new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 } },
        { ScaleType.Diminished, new[] { 0, 2, 3, 5, 6, 8, 9, 11 } },
        { ScaleType.HungarianMinor, new[] { 0, 2, 3, 6, 7, 8, 11 } },
        { ScaleType.Spanish, new[] { 0, 1, 4, 5, 7, 8, 10 } },
        { ScaleType.Arabic, new[] { 0, 1, 4, 5, 7, 8, 11 } },
        { ScaleType.Japanese, new[] { 0, 1, 5, 7, 8 } },
        { ScaleType.BebopDominant, new[] { 0, 2, 4, 5, 7, 9, 10, 11 } },
        { ScaleType.BebopMajor, new[] { 0, 2, 4, 5, 7, 8, 9, 11 } }
    };

    /// <summary>
    /// Gets the chord intervals for a given chord type.
    /// </summary>
    private static readonly Dictionary<ChordType, int[]> ChordIntervals = new()
    {
        { ChordType.Major, new[] { 0, 4, 7 } },
        { ChordType.Minor, new[] { 0, 3, 7 } },
        { ChordType.Diminished, new[] { 0, 3, 6 } },
        { ChordType.Augmented, new[] { 0, 4, 8 } },
        { ChordType.Major7, new[] { 0, 4, 7, 11 } },
        { ChordType.Minor7, new[] { 0, 3, 7, 10 } },
        { ChordType.Dominant7, new[] { 0, 4, 7, 10 } },
        { ChordType.Diminished7, new[] { 0, 3, 6, 9 } },
        { ChordType.HalfDiminished7, new[] { 0, 3, 6, 10 } },
        { ChordType.MinorMajor7, new[] { 0, 3, 7, 11 } },
        { ChordType.Augmented7, new[] { 0, 4, 8, 10 } },
        { ChordType.Major9, new[] { 0, 4, 7, 11, 14 } },
        { ChordType.Minor9, new[] { 0, 3, 7, 10, 14 } },
        { ChordType.Dominant9, new[] { 0, 4, 7, 10, 14 } },
        { ChordType.Add9, new[] { 0, 4, 7, 14 } },
        { ChordType.Sus2, new[] { 0, 2, 7 } },
        { ChordType.Sus4, new[] { 0, 5, 7 } },
        { ChordType.Power, new[] { 0, 7 } },
        { ChordType.Major6, new[] { 0, 4, 7, 9 } },
        { ChordType.Minor6, new[] { 0, 3, 7, 9 } },
        { ChordType.Dominant11, new[] { 0, 4, 7, 10, 14, 17 } },
        { ChordType.Major13, new[] { 0, 4, 7, 11, 14, 21 } },
        { ChordType.Minor13, new[] { 0, 3, 7, 10, 14, 21 } }
    };

    /// <summary>
    /// Generates a 12-element boolean array representing which pitch classes are in the scale.
    /// </summary>
    /// <param name="scale">The scale type.</param>
    /// <param name="root">The root note of the scale.</param>
    /// <returns>A boolean array where index corresponds to pitch class (0=C, 1=C#, etc.) and true means the pitch is in the scale.</returns>
    public static bool[] GetScaleMap(ScaleType scale, NoteName root)
    {
        bool[] map = new bool[12];

        if (!ScaleIntervals.TryGetValue(scale, out var intervals))
        {
            intervals = ScaleIntervals[ScaleType.Major];
        }

        int rootPitch = (int)root;
        foreach (int interval in intervals)
        {
            int pitchClass = (rootPitch + interval) % 12;
            map[pitchClass] = true;
        }

        return map;
    }

    /// <summary>
    /// Generates a 12-element boolean array representing which pitch classes are in the chord.
    /// </summary>
    /// <param name="rootPitch">The root pitch class (0-11).</param>
    /// <param name="chordType">The chord type.</param>
    /// <returns>A boolean array where index corresponds to pitch class (0=C, 1=C#, etc.) and true means the pitch is in the chord.</returns>
    public static bool[] GetChordMap(int rootPitch, ChordType chordType)
    {
        bool[] map = new bool[12];

        if (!ChordIntervals.TryGetValue(chordType, out var intervals))
        {
            intervals = ChordIntervals[ChordType.Major];
        }

        rootPitch = ((rootPitch % 12) + 12) % 12; // Normalize to 0-11
        foreach (int interval in intervals)
        {
            int pitchClass = (rootPitch + interval) % 12;
            map[pitchClass] = true;
        }

        return map;
    }

    /// <summary>
    /// Creates a custom scale map from a set of intervals.
    /// </summary>
    /// <param name="root">The root note.</param>
    /// <param name="intervals">The intervals from the root (e.g., 0, 2, 4, 5, 7, 9, 11 for major).</param>
    /// <returns>A boolean array representing the custom scale.</returns>
    public static bool[] CreateCustomScale(NoteName root, params int[] intervals)
    {
        bool[] map = new bool[12];

        int rootPitch = (int)root;
        foreach (int interval in intervals)
        {
            int pitchClass = (rootPitch + interval) % 12;
            if (pitchClass >= 0 && pitchClass < 12)
            {
                map[pitchClass] = true;
            }
        }

        // Ensure at least one note is in the scale
        if (!map.Any(b => b))
        {
            map[rootPitch] = true;
        }

        return map;
    }

    /// <summary>
    /// Creates a custom scale map from specific note names.
    /// </summary>
    /// <param name="notes">The notes that should be in the scale.</param>
    /// <returns>A boolean array representing the custom scale.</returns>
    public static bool[] CreateCustomScale(params NoteName[] notes)
    {
        bool[] map = new bool[12];

        foreach (var note in notes)
        {
            map[(int)note] = true;
        }

        // Ensure at least one note is in the scale
        if (!map.Any(b => b))
        {
            map[0] = true; // Default to C
        }

        return map;
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Checks if a MIDI note is in a given scale.
    /// </summary>
    /// <param name="midiNote">The MIDI note to check.</param>
    /// <param name="scale">The scale type.</param>
    /// <param name="root">The root note of the scale.</param>
    /// <returns>True if the note is in the scale.</returns>
    public static bool IsInScale(int midiNote, ScaleType scale, NoteName root)
    {
        bool[] scaleMap = GetScaleMap(scale, root);
        int pitchClass = ((midiNote % 12) + 12) % 12;
        return scaleMap[pitchClass];
    }

    /// <summary>
    /// Checks if a MIDI note is in a given chord.
    /// </summary>
    /// <param name="midiNote">The MIDI note to check.</param>
    /// <param name="chordRoot">The root note of the chord (pitch class 0-11).</param>
    /// <param name="chordType">The chord type.</param>
    /// <returns>True if the note is in the chord.</returns>
    public static bool IsInChord(int midiNote, int chordRoot, ChordType chordType)
    {
        bool[] chordMap = GetChordMap(chordRoot, chordType);
        int pitchClass = ((midiNote % 12) + 12) % 12;
        return chordMap[pitchClass];
    }

    /// <summary>
    /// Gets all MIDI notes in a scale within a specified range.
    /// </summary>
    /// <param name="scale">The scale type.</param>
    /// <param name="root">The root note of the scale.</param>
    /// <param name="minNote">Minimum MIDI note (default 0).</param>
    /// <param name="maxNote">Maximum MIDI note (default 127).</param>
    /// <returns>An array of MIDI note numbers that are in the scale.</returns>
    public static int[] GetScaleNotes(ScaleType scale, NoteName root, int minNote = 0, int maxNote = 127)
    {
        bool[] scaleMap = GetScaleMap(scale, root);
        var notes = new List<int>();

        for (int note = minNote; note <= maxNote; note++)
        {
            if (scaleMap[note % 12])
            {
                notes.Add(note);
            }
        }

        return notes.ToArray();
    }

    /// <summary>
    /// Gets the distance in semitones to the nearest scale tone.
    /// </summary>
    /// <param name="midiNote">The MIDI note to check.</param>
    /// <param name="scale">The scale type.</param>
    /// <param name="root">The root note of the scale.</param>
    /// <returns>The distance in semitones (0 if already in scale).</returns>
    public static int GetDistanceToScale(int midiNote, ScaleType scale, NoteName root)
    {
        bool[] scaleMap = GetScaleMap(scale, root);
        int pitchClass = ((midiNote % 12) + 12) % 12;

        if (scaleMap[pitchClass])
            return 0;

        // Find minimum distance
        int minDistance = 12;
        for (int i = 1; i <= 6; i++)
        {
            if (scaleMap[(pitchClass + i) % 12] || scaleMap[(pitchClass - i + 12) % 12])
            {
                minDistance = i;
                break;
            }
        }

        return minDistance;
    }

    /// <summary>
    /// Validates a custom scale array.
    /// </summary>
    private static void ValidateCustomScale(bool[] customScale)
    {
        if (customScale == null)
            throw new ArgumentNullException(nameof(customScale));

        if (customScale.Length != 12)
            throw new ArgumentException("Custom scale must have exactly 12 elements (one for each semitone).", nameof(customScale));

        if (!customScale.Any(b => b))
            throw new ArgumentException("Custom scale must have at least one valid tone.", nameof(customScale));
    }

    /// <summary>
    /// Creates a deep clone of a pattern with all events copied.
    /// </summary>
    private static Pattern ClonePattern(Pattern pattern)
    {
        var newPattern = new Pattern(pattern.Synth)
        {
            LoopLength = pattern.LoopLength,
            IsLooping = pattern.IsLooping,
            StartBeat = pattern.StartBeat,
            Enabled = pattern.Enabled,
            Name = pattern.Name,
            InstrumentName = pattern.InstrumentName,
            SourceInfo = pattern.SourceInfo
        };

        // Deep clone events
        foreach (var ev in pattern.Events)
        {
            newPattern.Events.Add(new NoteEvent
            {
                Beat = ev.Beat,
                Note = ev.Note,
                Velocity = ev.Velocity,
                Duration = ev.Duration,
                SourceInfo = ev.SourceInfo,
                Parameters = ev.Parameters != null
                    ? new Dictionary<string, LiveParameter>(ev.Parameters)
                    : null
            });
        }

        return newPattern;
    }

    #endregion
}
