//Engine License (MEL) – Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Music theory helpers for chords, scales, and note manipulation.


using System;
using System.Collections.Generic;
using System.Linq;


namespace MusicEngine.Core;


/// <summary>
/// Note names
/// </summary>
public enum NoteName
{
    C = 0, CSharp = 1, D = 2, DSharp = 3, E = 4, F = 5,
    FSharp = 6, G = 7, GSharp = 8, A = 9, ASharp = 10, B = 11
}


/// <summary>
/// Chord types
/// </summary>
public enum ChordType
{
    Major,
    Minor,
    Diminished,
    Augmented,
    Major7,
    Minor7,
    Dominant7,
    Diminished7,
    HalfDiminished7,
    MinorMajor7,
    Augmented7,
    Major9,
    Minor9,
    Dominant9,
    Add9,
    Sus2,
    Sus4,
    Power,
    Major6,
    Minor6,
    Dominant11,
    Major13,
    Minor13
}


/// <summary>
/// Scale types
/// </summary>
public enum ScaleType
{
    Major,
    NaturalMinor,
    HarmonicMinor,
    MelodicMinor,
    Dorian,
    Phrygian,
    Lydian,
    Mixolydian,
    Locrian,
    PentatonicMajor,
    PentatonicMinor,
    Blues,
    WholeTone,
    Chromatic,
    Diminished,
    HungarianMinor,
    Spanish,
    Arabic,
    Japanese,
    BebopDominant,
    BebopMajor
}


/// <summary>
/// Helper class for working with notes
/// </summary>
public static class Note
{
    /// <summary>
    /// Get MIDI note number from note name and octave
    /// </summary>
    public static int FromName(NoteName note, int octave = 4)
    {
        return (int)note + (octave + 1) * 12;
    }

    /// <summary>
    /// Get MIDI note number from string (e.g., "C4", "F#3", "Bb5")
    /// </summary>
    public static int FromString(string noteName)
    {
        if (string.IsNullOrWhiteSpace(noteName))
            throw new ArgumentException("Note name cannot be empty", nameof(noteName));

        noteName = noteName.Trim();
        int noteValue;
        int index = 0;

        // Parse note letter
        char letter = char.ToUpper(noteName[index++]);
        noteValue = letter switch
        {
            'C' => 0,
            'D' => 2,
            'E' => 4,
            'F' => 5,
            'G' => 7,
            'A' => 9,
            'B' => 11,
            _ => throw new ArgumentException($"Invalid note letter: {letter}")
        };

        // Check for sharp or flat
        if (index < noteName.Length)
        {
            char modifier = noteName[index];
            if (modifier == '#' || modifier == '♯')
            {
                noteValue++;
                index++;
            }
            else if (modifier == 'b' || modifier == '♭')
            {
                noteValue--;
                index++;
            }
        }

        // Parse octave
        int octave = 4; // Default octave
        if (index < noteName.Length)
        {
            if (int.TryParse(noteName.Substring(index), out int parsedOctave))
            {
                octave = parsedOctave;
            }
        }

        return noteValue + (octave + 1) * 12;
    }

    /// <summary>
    /// Get note name from MIDI note number
    /// </summary>
    public static string ToName(int midiNote)
    {
        string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        int note = midiNote % 12;
        int octave = (midiNote / 12) - 1;
        return $"{noteNames[note]}{octave}";
    }

    /// <summary>
    /// Get the note name without octave
    /// </summary>
    public static NoteName GetNoteName(int midiNote)
    {
        return (NoteName)(midiNote % 12);
    }

    /// <summary>
    /// Get the octave of a MIDI note
    /// </summary>
    public static int GetOctave(int midiNote)
    {
        return (midiNote / 12) - 1;
    }

    /// <summary>
    /// Transpose a note by semitones
    /// </summary>
    public static int Transpose(int midiNote, int semitones)
    {
        return Math.Clamp(midiNote + semitones, 0, 127);
    }

    /// <summary>
    /// Get the frequency of a MIDI note
    /// </summary>
    public static double GetFrequency(int midiNote)
    {
        return 440.0 * Math.Pow(2.0, (midiNote - 69) / 12.0);
    }

    /// <summary>
    /// Get the nearest MIDI note for a frequency
    /// </summary>
    public static int FromFrequency(double frequency)
    {
        return (int)Math.Round(69 + 12 * Math.Log2(frequency / 440.0));
    }

    /// <summary>
    /// Common note shortcuts
    /// </summary>
    public static int C(int octave = 4) => FromName(NoteName.C, octave);
    public static int D(int octave = 4) => FromName(NoteName.D, octave);
    public static int E(int octave = 4) => FromName(NoteName.E, octave);
    public static int F(int octave = 4) => FromName(NoteName.F, octave);
    public static int G(int octave = 4) => FromName(NoteName.G, octave);
    public static int A(int octave = 4) => FromName(NoteName.A, octave);
    public static int B(int octave = 4) => FromName(NoteName.B, octave);
}


/// <summary>
/// Helper class for working with chords
/// </summary>
public static class Chord
{
    // Chord intervals (semitones from root)
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
    /// Get chord notes from root note and chord type
    /// </summary>
    public static int[] GetNotes(int root, ChordType type)
    {
        if (!ChordIntervals.TryGetValue(type, out var intervals))
        {
            intervals = ChordIntervals[ChordType.Major];
        }

        return intervals.Select(i => Math.Clamp(root + i, 0, 127)).ToArray();
    }

    /// <summary>
    /// Get chord notes from note name string and chord type
    /// </summary>
    public static int[] GetNotes(string rootNote, ChordType type)
    {
        int root = Note.FromString(rootNote);
        return GetNotes(root, type);
    }

    /// <summary>
    /// Get chord notes from note name enum and chord type
    /// </summary>
    public static int[] GetNotes(NoteName root, int octave, ChordType type)
    {
        int rootNote = Note.FromName(root, octave);
        return GetNotes(rootNote, type);
    }

    /// <summary>
    /// Get an inversion of a chord
    /// </summary>
    /// <param name="notes">Original chord notes</param>
    /// <param name="inversion">Inversion number (1 = first, 2 = second, etc.)</param>
    public static int[] GetInversion(int[] notes, int inversion)
    {
        if (notes.Length == 0) return notes;

        var result = new int[notes.Length];
        inversion = inversion % notes.Length;

        for (int i = 0; i < notes.Length; i++)
        {
            int noteIndex = (i + inversion) % notes.Length;
            int octaveShift = (i + inversion) / notes.Length;
            result[i] = notes[noteIndex] + (octaveShift * 12);
        }

        return result.OrderBy(n => n).ToArray();
    }

    /// <summary>
    /// Spread a chord across octaves
    /// </summary>
    public static int[] Spread(int[] notes, int octaveSpread = 1)
    {
        var result = new List<int>();

        for (int i = 0; i < notes.Length; i++)
        {
            int octaveOffset = (i * octaveSpread * 12) / notes.Length;
            result.Add(Math.Clamp(notes[i] + octaveOffset, 0, 127));
        }

        return result.ToArray();
    }

    /// <summary>
    /// Drop voicing (drop 2, drop 3, etc.)
    /// </summary>
    public static int[] Drop(int[] notes, int dropVoice)
    {
        if (dropVoice <= 0 || dropVoice >= notes.Length) return notes;

        var sorted = notes.OrderByDescending(n => n).ToArray();
        sorted[dropVoice] -= 12;

        return sorted.OrderBy(n => n).ToArray();
    }

    /// <summary>
    /// Common chord shortcuts
    /// </summary>
    public static int[] CMaj(int octave = 4) => GetNotes(NoteName.C, octave, ChordType.Major);
    public static int[] CMin(int octave = 4) => GetNotes(NoteName.C, octave, ChordType.Minor);
    public static int[] DMaj(int octave = 4) => GetNotes(NoteName.D, octave, ChordType.Major);
    public static int[] DMin(int octave = 4) => GetNotes(NoteName.D, octave, ChordType.Minor);
    public static int[] EMaj(int octave = 4) => GetNotes(NoteName.E, octave, ChordType.Major);
    public static int[] EMin(int octave = 4) => GetNotes(NoteName.E, octave, ChordType.Minor);
    public static int[] FMaj(int octave = 4) => GetNotes(NoteName.F, octave, ChordType.Major);
    public static int[] FMin(int octave = 4) => GetNotes(NoteName.F, octave, ChordType.Minor);
    public static int[] GMaj(int octave = 4) => GetNotes(NoteName.G, octave, ChordType.Major);
    public static int[] GMin(int octave = 4) => GetNotes(NoteName.G, octave, ChordType.Minor);
    public static int[] AMaj(int octave = 4) => GetNotes(NoteName.A, octave, ChordType.Major);
    public static int[] AMin(int octave = 4) => GetNotes(NoteName.A, octave, ChordType.Minor);
    public static int[] BMaj(int octave = 4) => GetNotes(NoteName.B, octave, ChordType.Major);
    public static int[] BMin(int octave = 4) => GetNotes(NoteName.B, octave, ChordType.Minor);
}


/// <summary>
/// Helper class for working with scales
/// </summary>
public static class Scale
{
    // Scale intervals (semitones from root)
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
    /// Get scale notes from root note and scale type
    /// </summary>
    public static int[] GetNotes(int root, ScaleType type, int octaves = 1)
    {
        if (!ScaleIntervals.TryGetValue(type, out var intervals))
        {
            intervals = ScaleIntervals[ScaleType.Major];
        }

        var notes = new List<int>();

        for (int octave = 0; octave < octaves; octave++)
        {
            foreach (var interval in intervals)
            {
                int note = root + interval + (octave * 12);
                if (note <= 127)
                {
                    notes.Add(note);
                }
            }
        }

        return notes.ToArray();
    }

    /// <summary>
    /// Get scale notes from note name string and scale type
    /// </summary>
    public static int[] GetNotes(string rootNote, ScaleType type, int octaves = 1)
    {
        int root = Note.FromString(rootNote);
        return GetNotes(root, type, octaves);
    }

    /// <summary>
    /// Get scale notes from note name enum and scale type
    /// </summary>
    public static int[] GetNotes(NoteName root, int octave, ScaleType type, int octaves = 1)
    {
        int rootNote = Note.FromName(root, octave);
        return GetNotes(rootNote, type, octaves);
    }

    /// <summary>
    /// Get the scale degree (1-based) of a note in a scale
    /// </summary>
    /// <returns>Scale degree (1-7 for diatonic), or -1 if not in scale</returns>
    public static int GetDegree(int note, int root, ScaleType type)
    {
        if (!ScaleIntervals.TryGetValue(type, out var intervals))
        {
            return -1;
        }

        int interval = (note - root) % 12;
        if (interval < 0) interval += 12;

        for (int i = 0; i < intervals.Length; i++)
        {
            if (intervals[i] == interval)
            {
                return i + 1;
            }
        }

        return -1;
    }

    /// <summary>
    /// Check if a note is in a scale
    /// </summary>
    public static bool IsInScale(int note, int root, ScaleType type)
    {
        return GetDegree(note, root, type) > 0;
    }

    /// <summary>
    /// Quantize a note to the nearest scale note
    /// </summary>
    public static int Quantize(int note, int root, ScaleType type)
    {
        var scaleNotes = GetNotes(root % 12, type, 11); // Get all possible scale notes

        int closest = scaleNotes[0];
        int minDistance = Math.Abs(note - closest);

        foreach (var scaleNote in scaleNotes)
        {
            int distance = Math.Abs(note - scaleNote);
            if (distance < minDistance)
            {
                minDistance = distance;
                closest = scaleNote;
            }
        }

        return closest;
    }

    /// <summary>
    /// Get diatonic chords in a scale
    /// </summary>
    public static (int root, ChordType type)[] GetDiatonicChords(int root, ScaleType scaleType)
    {
        // For major scale, the chord types are: I-maj, ii-min, iii-min, IV-maj, V-maj, vi-min, vii-dim
        ChordType[] majorChordTypes = { ChordType.Major, ChordType.Minor, ChordType.Minor, ChordType.Major, ChordType.Major, ChordType.Minor, ChordType.Diminished };
        ChordType[] minorChordTypes = { ChordType.Minor, ChordType.Diminished, ChordType.Major, ChordType.Minor, ChordType.Minor, ChordType.Major, ChordType.Major };

        ChordType[] chordTypes = scaleType switch
        {
            ScaleType.Major => majorChordTypes,
            ScaleType.NaturalMinor => minorChordTypes,
            ScaleType.Dorian => new[] { ChordType.Minor, ChordType.Minor, ChordType.Major, ChordType.Major, ChordType.Minor, ChordType.Diminished, ChordType.Major },
            _ => majorChordTypes
        };

        var scaleNotes = GetNotes(root, scaleType);
        var result = new (int, ChordType)[Math.Min(scaleNotes.Length, chordTypes.Length)];

        for (int i = 0; i < result.Length; i++)
        {
            result[i] = (scaleNotes[i], chordTypes[i]);
        }

        return result;
    }

    /// <summary>
    /// Get a random note from a scale
    /// </summary>
    public static int RandomNote(int root, ScaleType type, int minNote = 36, int maxNote = 96)
    {
        var notes = GetNotes(root % 12, type, 10)
            .Where(n => n >= minNote && n <= maxNote)
            .ToArray();

        if (notes.Length == 0) return root;

        return notes[new Random().Next(notes.Length)];
    }

    /// <summary>
    /// Get the relative minor/major of a scale
    /// </summary>
    public static int GetRelative(int root, ScaleType type)
    {
        return type switch
        {
            ScaleType.Major => root - 3, // Relative minor is 3 semitones down
            ScaleType.NaturalMinor => root + 3, // Relative major is 3 semitones up
            _ => root
        };
    }

    /// <summary>
    /// Get the parallel minor/major of a scale (same root)
    /// </summary>
    public static ScaleType GetParallel(ScaleType type)
    {
        return type switch
        {
            ScaleType.Major => ScaleType.NaturalMinor,
            ScaleType.NaturalMinor => ScaleType.Major,
            _ => type
        };
    }
}


/// <summary>
/// Chord progression builder
/// </summary>
public class ChordProgression
{
    private readonly List<(int root, ChordType type, double duration)> _chords = new();
    private int _root;
    private ScaleType _scale;

    /// <summary>
    /// Creates a new chord progression
    /// </summary>
    public ChordProgression(int root = 60, ScaleType scale = ScaleType.Major)
    {
        _root = root;
        _scale = scale;
    }

    /// <summary>
    /// Creates a new chord progression from a key string (e.g., "C major")
    /// </summary>
    public ChordProgression(string key)
    {
        var parts = key.Split(' ');
        _root = Note.FromString(parts[0] + "4");
        _scale = parts.Length > 1 && parts[1].ToLower().Contains("min")
            ? ScaleType.NaturalMinor
            : ScaleType.Major;
    }

    /// <summary>
    /// Add a chord by scale degree (1-7)
    /// </summary>
    public ChordProgression Add(int degree, double duration = 1.0, ChordType? overrideType = null)
    {
        var diatonicChords = Scale.GetDiatonicChords(_root, _scale);
        degree = Math.Clamp(degree, 1, diatonicChords.Length) - 1;

        var (chordRoot, chordType) = diatonicChords[degree];
        _chords.Add((chordRoot, overrideType ?? chordType, duration));

        return this;
    }

    /// <summary>
    /// Add a specific chord
    /// </summary>
    public ChordProgression AddChord(int root, ChordType type, double duration = 1.0)
    {
        _chords.Add((root, type, duration));
        return this;
    }

    /// <summary>
    /// Add a chord by name (e.g., "Cmaj", "Am7")
    /// </summary>
    public ChordProgression AddChord(string chordName, double duration = 1.0)
    {
        // Simple parsing - could be expanded
        int root = Note.FromString(chordName.Substring(0, chordName.Any(char.IsDigit) ? chordName.IndexOfAny("0123456789".ToCharArray()) : Math.Min(2, chordName.Length)) + "4");
        ChordType type = ChordType.Major;

        string suffix = chordName.ToLower();
        if (suffix.Contains("min") || suffix.Contains("m7") || (suffix.Contains("m") && !suffix.Contains("maj")))
            type = ChordType.Minor;
        if (suffix.Contains("7"))
            type = suffix.Contains("maj7") ? ChordType.Major7 : (type == ChordType.Minor ? ChordType.Minor7 : ChordType.Dominant7);
        if (suffix.Contains("dim"))
            type = ChordType.Diminished;
        if (suffix.Contains("aug"))
            type = ChordType.Augmented;

        _chords.Add((root, type, duration));
        return this;
    }

    /// <summary>
    /// Get all chord notes for this progression
    /// </summary>
    public List<(int[] notes, double duration)> GetChords()
    {
        return _chords.Select(c => (Chord.GetNotes(c.root, c.type), c.duration)).ToList();
    }

    /// <summary>
    /// Create a pattern from this progression
    /// </summary>
    public Pattern ToPattern(ISynth synth, int velocity = 100)
    {
        var pattern = new Pattern(synth);

        double beat = 0;
        foreach (var (root, type, duration) in _chords)
        {
            var notes = Chord.GetNotes(root, type);
            foreach (var note in notes)
            {
                pattern.Note(note, beat, duration * 0.9, velocity);
            }
            beat += duration;
        }

        pattern.LoopLength = beat;
        return pattern;
    }

    /// <summary>
    /// Common progressions
    /// </summary>
    public static ChordProgression I_IV_V_I(int root = 60) =>
        new ChordProgression(root).Add(1).Add(4).Add(5).Add(1);

    public static ChordProgression I_V_vi_IV(int root = 60) =>
        new ChordProgression(root).Add(1).Add(5).Add(6).Add(4);

    public static ChordProgression ii_V_I(int root = 60) =>
        new ChordProgression(root).Add(2).Add(5).Add(1);

    public static ChordProgression I_vi_IV_V(int root = 60) =>
        new ChordProgression(root).Add(1).Add(6).Add(4).Add(5);

    public static ChordProgression vi_IV_I_V(int root = 60) =>
        new ChordProgression(root).Add(6).Add(4).Add(1).Add(5);

    public static ChordProgression I_IV_vi_V(int root = 60) =>
        new ChordProgression(root).Add(1).Add(4).Add(6).Add(5);

    public static ChordProgression Blues12Bar(int root = 60) =>
        new ChordProgression(root)
            .Add(1).Add(1).Add(1).Add(1)
            .Add(4).Add(4).Add(1).Add(1)
            .Add(5).Add(4).Add(1).Add(5);
}
