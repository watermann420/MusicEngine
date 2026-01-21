//Engine License (MEL) – Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Note quantizer for timing and scale quantization.


using System;
using System.Collections.Generic;


namespace MusicEngine.Core;


/// <summary>
/// Predefined scale types for note quantization
/// </summary>
public enum QuantizerScaleType
{
    /// <summary>Major scale (Ionian mode)</summary>
    Major,
    /// <summary>Natural minor scale (Aeolian mode)</summary>
    Minor,
    /// <summary>Harmonic minor scale</summary>
    HarmonicMinor,
    /// <summary>Melodic minor scale</summary>
    MelodicMinor,
    /// <summary>Major pentatonic scale</summary>
    Pentatonic,
    /// <summary>Minor pentatonic scale</summary>
    PentatonicMinor,
    /// <summary>Blues scale</summary>
    Blues,
    /// <summary>Dorian mode</summary>
    Dorian,
    /// <summary>Phrygian mode</summary>
    Phrygian,
    /// <summary>Lydian mode</summary>
    Lydian,
    /// <summary>Mixolydian mode</summary>
    Mixolydian,
    /// <summary>Locrian mode</summary>
    Locrian,
    /// <summary>Whole tone scale</summary>
    WholeTone,
    /// <summary>Diminished scale (half-whole)</summary>
    Diminished,
    /// <summary>Chromatic scale (all notes)</summary>
    Chromatic
}


/// <summary>
/// Note quantizer for timing and scale quantization.
/// Provides tools for snapping notes to a grid and quantizing pitches to scales.
/// </summary>
public class NoteQuantizer
{
    // Scale intervals (semitones from root) for each scale type
    private static readonly Dictionary<QuantizerScaleType, int[]> ScaleIntervals = new()
    {
        { QuantizerScaleType.Major, new[] { 0, 2, 4, 5, 7, 9, 11 } },
        { QuantizerScaleType.Minor, new[] { 0, 2, 3, 5, 7, 8, 10 } },
        { QuantizerScaleType.HarmonicMinor, new[] { 0, 2, 3, 5, 7, 8, 11 } },
        { QuantizerScaleType.MelodicMinor, new[] { 0, 2, 3, 5, 7, 9, 11 } },
        { QuantizerScaleType.Pentatonic, new[] { 0, 2, 4, 7, 9 } },
        { QuantizerScaleType.PentatonicMinor, new[] { 0, 3, 5, 7, 10 } },
        { QuantizerScaleType.Blues, new[] { 0, 3, 5, 6, 7, 10 } },
        { QuantizerScaleType.Dorian, new[] { 0, 2, 3, 5, 7, 9, 10 } },
        { QuantizerScaleType.Phrygian, new[] { 0, 1, 3, 5, 7, 8, 10 } },
        { QuantizerScaleType.Lydian, new[] { 0, 2, 4, 6, 7, 9, 11 } },
        { QuantizerScaleType.Mixolydian, new[] { 0, 2, 4, 5, 7, 9, 10 } },
        { QuantizerScaleType.Locrian, new[] { 0, 1, 3, 5, 6, 8, 10 } },
        { QuantizerScaleType.WholeTone, new[] { 0, 2, 4, 6, 8, 10 } },
        { QuantizerScaleType.Diminished, new[] { 0, 2, 3, 5, 6, 8, 9, 11 } },
        { QuantizerScaleType.Chromatic, new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 } }
    };

    private double _strength = 1.0;
    private double _gridSize = 0.25;

    /// <summary>
    /// Gets or sets the quantization strength (0-1).
    /// 0 = no quantization, 1 = full quantization
    /// </summary>
    public double Strength
    {
        get => _strength;
        set => _strength = Math.Clamp(value, 0, 1);
    }

    /// <summary>
    /// Gets or sets the grid size in beats (e.g., 1 = whole note, 0.5 = half note, 0.25 = quarter note)
    /// </summary>
    public double GridSize
    {
        get => _gridSize;
        set => _gridSize = Math.Max(0.001, value);
    }

    /// <summary>
    /// Gets or sets whether timing quantization is enabled
    /// </summary>
    public bool TimingEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether scale quantization is enabled
    /// </summary>
    public bool ScaleEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the current scale type for scale quantization
    /// </summary>
    public QuantizerScaleType CurrentScale { get; set; } = QuantizerScaleType.Major;

    /// <summary>
    /// Gets or sets the root note (0-11, where 0 = C)
    /// </summary>
    public int RootNote { get; set; } = 0;

    /// <summary>
    /// Creates a new note quantizer with default settings
    /// </summary>
    public NoteQuantizer() { }

    /// <summary>
    /// Creates a new note quantizer with specified grid size
    /// </summary>
    /// <param name="gridSize">Grid size in beats</param>
    public NoteQuantizer(double gridSize)
    {
        GridSize = gridSize;
    }

    /// <summary>
    /// Creates a new note quantizer with specified settings
    /// </summary>
    /// <param name="gridSize">Grid size in beats</param>
    /// <param name="strength">Quantization strength (0-1)</param>
    public NoteQuantizer(double gridSize, double strength)
    {
        GridSize = gridSize;
        Strength = strength;
    }

    /// <summary>
    /// Quantize a time value to the nearest grid position
    /// </summary>
    /// <param name="time">Time value in beats</param>
    /// <param name="gridSize">Grid size in beats (uses GridSize property if not specified)</param>
    /// <returns>Quantized time value</returns>
    public double QuantizeToGrid(double time, double gridSize = -1)
    {
        if (!TimingEnabled) return time;

        double effectiveGridSize = gridSize > 0 ? gridSize : _gridSize;
        double quantizedTime = Math.Round(time / effectiveGridSize) * effectiveGridSize;

        // Apply strength
        return time + (quantizedTime - time) * _strength;
    }

    /// <summary>
    /// Quantize a note to the nearest scale note
    /// </summary>
    /// <param name="note">MIDI note number (0-127)</param>
    /// <param name="scale">Scale intervals as semitones from root</param>
    /// <param name="rootNote">Root note (0-11, where 0 = C)</param>
    /// <returns>Quantized MIDI note number</returns>
    public int QuantizeToScale(int note, int[] scale, int rootNote)
    {
        if (!ScaleEnabled || scale == null || scale.Length == 0)
            return note;

        // Normalize the note to find its position relative to the root
        int noteClass = ((note % 12) - rootNote + 12) % 12;
        int octave = note / 12;

        // Find the nearest scale note
        int nearestScaleNote = scale[0];
        int minDistance = 12;

        foreach (int scaleInterval in scale)
        {
            int distance = Math.Abs(noteClass - scaleInterval);
            // Also check wrap-around distance
            int wrapDistance = 12 - distance;
            int effectiveDistance = Math.Min(distance, wrapDistance);

            if (effectiveDistance < minDistance)
            {
                minDistance = effectiveDistance;
                nearestScaleNote = scaleInterval;
            }
        }

        // Calculate the quantized note
        int quantizedNote = octave * 12 + rootNote + nearestScaleNote;

        // Adjust octave if we wrapped around
        if (nearestScaleNote > noteClass && nearestScaleNote - noteClass > 6)
        {
            quantizedNote -= 12;
        }
        else if (noteClass > nearestScaleNote && noteClass - nearestScaleNote > 6)
        {
            quantizedNote += 12;
        }

        // Apply strength for partial quantization
        if (_strength < 1.0)
        {
            // For pitch, we round based on strength threshold
            double diff = quantizedNote - note;
            if (Math.Abs(diff) * _strength < 0.5)
            {
                return note;
            }
        }

        return Math.Clamp(quantizedNote, 0, 127);
    }

    /// <summary>
    /// Quantize a note using the current scale and root settings
    /// </summary>
    /// <param name="note">MIDI note number (0-127)</param>
    /// <returns>Quantized MIDI note number</returns>
    public int QuantizeToScale(int note)
    {
        int[] scale = GetScaleNotes(CurrentScale, RootNote);
        return QuantizeToScale(note, scale, RootNote);
    }

    /// <summary>
    /// Get the scale notes (intervals) for a given scale type and root
    /// </summary>
    /// <param name="scaleType">The scale type</param>
    /// <param name="root">Root note (0-11, where 0 = C)</param>
    /// <returns>Array of MIDI note numbers in the scale (one octave)</returns>
    public static int[] GetScaleNotes(QuantizerScaleType scaleType, int root)
    {
        if (!ScaleIntervals.TryGetValue(scaleType, out var intervals))
        {
            intervals = ScaleIntervals[QuantizerScaleType.Major];
        }

        int[] notes = new int[intervals.Length];
        for (int i = 0; i < intervals.Length; i++)
        {
            notes[i] = (root + intervals[i]) % 12;
        }

        return notes;
    }

    /// <summary>
    /// Get the scale intervals for a given scale type
    /// </summary>
    /// <param name="scaleType">The scale type</param>
    /// <returns>Array of intervals (semitones from root)</returns>
    public static int[] GetScaleIntervals(QuantizerScaleType scaleType)
    {
        if (ScaleIntervals.TryGetValue(scaleType, out var intervals))
        {
            return (int[])intervals.Clone();
        }
        return (int[])ScaleIntervals[QuantizerScaleType.Major].Clone();
    }

    /// <summary>
    /// Check if a note is in the specified scale
    /// </summary>
    /// <param name="note">MIDI note number</param>
    /// <param name="scaleType">Scale type</param>
    /// <param name="root">Root note (0-11)</param>
    /// <returns>True if the note is in the scale</returns>
    public static bool IsInScale(int note, QuantizerScaleType scaleType, int root)
    {
        int[] scaleNotes = GetScaleNotes(scaleType, root);
        int noteClass = note % 12;

        foreach (int scaleNote in scaleNotes)
        {
            if (scaleNote == noteClass)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Quantize both timing and pitch of a note event
    /// </summary>
    /// <param name="time">Time in beats</param>
    /// <param name="note">MIDI note number</param>
    /// <returns>Tuple of (quantized time, quantized note)</returns>
    public (double time, int note) Quantize(double time, int note)
    {
        double quantizedTime = QuantizeToGrid(time);
        int quantizedNote = QuantizeToScale(note);
        return (quantizedTime, quantizedNote);
    }

    /// <summary>
    /// Get all MIDI notes in a scale across the full MIDI range
    /// </summary>
    /// <param name="scaleType">Scale type</param>
    /// <param name="root">Root note (0-11)</param>
    /// <param name="minNote">Minimum MIDI note (default 0)</param>
    /// <param name="maxNote">Maximum MIDI note (default 127)</param>
    /// <returns>Array of all MIDI notes in the scale</returns>
    public static int[] GetAllScaleNotes(QuantizerScaleType scaleType, int root, int minNote = 0, int maxNote = 127)
    {
        int[] intervals = GetScaleIntervals(scaleType);
        var notes = new List<int>();

        for (int octave = 0; octave <= 10; octave++)
        {
            foreach (int interval in intervals)
            {
                int note = root + interval + (octave * 12);
                if (note >= minNote && note <= maxNote)
                {
                    notes.Add(note);
                }
            }
        }

        return notes.ToArray();
    }

    /// <summary>
    /// Calculate the swing amount for a given time
    /// </summary>
    /// <param name="time">Time in beats</param>
    /// <param name="swingAmount">Swing amount (0-1, where 0 = no swing, 1 = full triplet swing)</param>
    /// <returns>Time with swing applied</returns>
    public double ApplySwing(double time, double swingAmount)
    {
        if (swingAmount <= 0) return time;

        // Calculate position within a half-beat
        double halfBeat = _gridSize * 2;
        double positionInHalfBeat = time % halfBeat;
        double halfBeatIndex = Math.Floor(time / halfBeat);

        // Determine if this is an off-beat (second eighth note in the pair)
        double gridPosition = positionInHalfBeat / _gridSize;
        bool isOffBeat = gridPosition >= 0.9 && gridPosition <= 1.1;

        if (isOffBeat)
        {
            // Shift the off-beat forward based on swing amount
            // Full swing (1.0) moves it to 2/3 position (triplet feel)
            double swingOffset = _gridSize * swingAmount * 0.33;
            return halfBeatIndex * halfBeat + _gridSize + swingOffset;
        }

        return time;
    }

    /// <summary>
    /// Predefined scales for quick access
    /// </summary>
    public static class Scales
    {
        /// <summary>Major scale intervals</summary>
        public static readonly int[] Major = { 0, 2, 4, 5, 7, 9, 11 };

        /// <summary>Natural minor scale intervals</summary>
        public static readonly int[] Minor = { 0, 2, 3, 5, 7, 8, 10 };

        /// <summary>Major pentatonic scale intervals</summary>
        public static readonly int[] Pentatonic = { 0, 2, 4, 7, 9 };

        /// <summary>Minor pentatonic scale intervals</summary>
        public static readonly int[] PentatonicMinor = { 0, 3, 5, 7, 10 };

        /// <summary>Blues scale intervals</summary>
        public static readonly int[] Blues = { 0, 3, 5, 6, 7, 10 };

        /// <summary>Harmonic minor scale intervals</summary>
        public static readonly int[] HarmonicMinor = { 0, 2, 3, 5, 7, 8, 11 };

        /// <summary>Melodic minor scale intervals</summary>
        public static readonly int[] MelodicMinor = { 0, 2, 3, 5, 7, 9, 11 };

        /// <summary>Dorian mode intervals</summary>
        public static readonly int[] Dorian = { 0, 2, 3, 5, 7, 9, 10 };

        /// <summary>Phrygian mode intervals</summary>
        public static readonly int[] Phrygian = { 0, 1, 3, 5, 7, 8, 10 };

        /// <summary>Lydian mode intervals</summary>
        public static readonly int[] Lydian = { 0, 2, 4, 6, 7, 9, 11 };

        /// <summary>Mixolydian mode intervals</summary>
        public static readonly int[] Mixolydian = { 0, 2, 4, 5, 7, 9, 10 };

        /// <summary>Locrian mode intervals</summary>
        public static readonly int[] Locrian = { 0, 1, 3, 5, 6, 8, 10 };

        /// <summary>Whole tone scale intervals</summary>
        public static readonly int[] WholeTone = { 0, 2, 4, 6, 8, 10 };

        /// <summary>Chromatic scale intervals</summary>
        public static readonly int[] Chromatic = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
    }
}
