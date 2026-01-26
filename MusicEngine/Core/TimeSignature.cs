// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using System;

namespace MusicEngine.Core;

/// <summary>
/// Represents a musical time signature with numerator (beats per bar) and denominator (beat value).
/// Immutable value type for thread safety and efficient comparisons.
/// </summary>
public readonly struct TimeSignature : IEquatable<TimeSignature>
{
    /// <summary>Common time signature: 4/4.</summary>
    public static readonly TimeSignature Common = new(4, 4);

    /// <summary>Cut time (alla breve): 2/2.</summary>
    public static readonly TimeSignature CutTime = new(2, 2);

    /// <summary>Waltz time: 3/4.</summary>
    public static readonly TimeSignature Waltz = new(3, 4);

    /// <summary>Compound duple: 6/8.</summary>
    public static readonly TimeSignature CompoundDuple = new(6, 8);

    /// <summary>Compound triple: 9/8.</summary>
    public static readonly TimeSignature CompoundTriple = new(9, 8);

    /// <summary>Compound quadruple: 12/8.</summary>
    public static readonly TimeSignature CompoundQuadruple = new(12, 8);

    /// <summary>Five-four time: 5/4.</summary>
    public static readonly TimeSignature FiveFour = new(5, 4);

    /// <summary>Seven-eight time: 7/8.</summary>
    public static readonly TimeSignature SevenEight = new(7, 8);

    private readonly int _numerator;
    private readonly int _denominator;

    /// <summary>
    /// Gets the numerator (number of beats per bar).
    /// </summary>
    public int Numerator => _numerator;

    /// <summary>
    /// Gets the denominator (the note value that represents one beat).
    /// Must be a power of 2 (1, 2, 4, 8, 16, 32, 64).
    /// </summary>
    public int Denominator => _denominator;

    /// <summary>
    /// Gets the number of beats per bar (same as Numerator).
    /// </summary>
    public int BeatsPerBar => _numerator;

    /// <summary>
    /// Gets the beat value as a fraction of a whole note.
    /// For example, 4 = quarter note = 0.25, 8 = eighth note = 0.125.
    /// </summary>
    public double BeatValue => 1.0 / _denominator;

    /// <summary>
    /// Gets the length of one bar in quarter notes (beats).
    /// </summary>
    public double BarLengthInQuarterNotes => _numerator * (4.0 / _denominator);

    /// <summary>
    /// Gets the length of one beat in quarter notes.
    /// </summary>
    public double BeatLengthInQuarterNotes => 4.0 / _denominator;

    /// <summary>
    /// Gets whether this is a compound time signature (divisible by 3, typically felt in groups of 3).
    /// </summary>
    public bool IsCompound => _numerator >= 6 && _numerator % 3 == 0 && (_denominator == 8 || _denominator == 16);

    /// <summary>
    /// Gets whether this is a simple time signature.
    /// </summary>
    public bool IsSimple => !IsCompound;

    /// <summary>
    /// Gets whether this is an irregular/odd time signature.
    /// </summary>
    public bool IsIrregular => _numerator == 5 || _numerator == 7 || _numerator == 11 || _numerator == 13;

    /// <summary>
    /// Gets the number of strong beats per bar for compound time signatures.
    /// For compound time (6/8, 9/8, 12/8), returns the number of dotted-quarter beats.
    /// For simple time, returns the same as BeatsPerBar.
    /// </summary>
    public int StrongBeatsPerBar => IsCompound ? _numerator / 3 : _numerator;

    /// <summary>
    /// Creates a new time signature.
    /// </summary>
    /// <param name="numerator">The number of beats per bar (1-32).</param>
    /// <param name="denominator">The beat value (must be a power of 2: 1, 2, 4, 8, 16, 32, 64).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if numerator is not between 1 and 32, or denominator is not a valid power of 2.
    /// </exception>
    public TimeSignature(int numerator, int denominator)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(numerator, 1, nameof(numerator));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(numerator, 32, nameof(numerator));

        if (!IsPowerOfTwo(denominator) || denominator < 1 || denominator > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(denominator),
                "Denominator must be a power of 2 between 1 and 64 (1, 2, 4, 8, 16, 32, 64).");
        }

        _numerator = numerator;
        _denominator = denominator;
    }

    /// <summary>
    /// Converts a position in quarter notes to bar and beat position.
    /// </summary>
    /// <param name="quarterNotes">The position in quarter notes.</param>
    /// <returns>A tuple of (bar, beat) where bar is 0-indexed and beat is 0-indexed within the bar.</returns>
    public (int Bar, double Beat) QuarterNotesToBarBeat(double quarterNotes)
    {
        double barLength = BarLengthInQuarterNotes;
        int bar = (int)(quarterNotes / barLength);
        double beatInQuarters = quarterNotes - bar * barLength;
        double beat = beatInQuarters / BeatLengthInQuarterNotes;
        return (bar, beat);
    }

    /// <summary>
    /// Converts a bar and beat position to quarter notes.
    /// </summary>
    /// <param name="bar">The bar number (0-indexed).</param>
    /// <param name="beat">The beat within the bar (0-indexed).</param>
    /// <returns>The position in quarter notes.</returns>
    public double BarBeatToQuarterNotes(int bar, double beat)
    {
        return bar * BarLengthInQuarterNotes + beat * BeatLengthInQuarterNotes;
    }

    /// <summary>
    /// Gets the beat accent pattern for this time signature.
    /// Returns an array of accent values (0.0-1.0) for each beat in the bar.
    /// 1.0 = strong beat (downbeat), 0.5 = medium beat, 0.25 = weak beat.
    /// </summary>
    /// <returns>Array of accent values for each beat.</returns>
    public double[] GetAccentPattern()
    {
        var accents = new double[_numerator];

        if (IsCompound)
        {
            // Compound time: accent every 3 beats
            for (int i = 0; i < _numerator; i++)
            {
                accents[i] = i % 3 == 0 ? (i == 0 ? 1.0 : 0.5) : 0.25;
            }
        }
        else
        {
            // Simple time
            accents[0] = 1.0; // Downbeat always strong

            switch (_numerator)
            {
                case 2:
                    accents[1] = 0.5;
                    break;
                case 3:
                    accents[1] = 0.25;
                    accents[2] = 0.5;
                    break;
                case 4:
                    accents[1] = 0.25;
                    accents[2] = 0.5;
                    accents[3] = 0.25;
                    break;
                case 5:
                    // 5/4 typically grouped as 3+2 or 2+3
                    accents[1] = 0.25;
                    accents[2] = 0.25;
                    accents[3] = 0.5;
                    accents[4] = 0.25;
                    break;
                case 7:
                    // 7/8 typically grouped as 4+3 or 3+4 or 2+2+3
                    accents[1] = 0.25;
                    accents[2] = 0.25;
                    accents[3] = 0.25;
                    accents[4] = 0.5;
                    accents[5] = 0.25;
                    accents[6] = 0.25;
                    break;
                default:
                    // Default pattern: weak beats
                    for (int i = 1; i < _numerator; i++)
                    {
                        accents[i] = i == _numerator / 2 ? 0.5 : 0.25;
                    }
                    break;
            }
        }

        return accents;
    }

    /// <summary>
    /// Gets the grouping pattern for this time signature.
    /// Returns an array indicating how beats should be grouped for beaming/accents.
    /// </summary>
    /// <returns>Array of group sizes.</returns>
    public int[] GetGroupingPattern()
    {
        if (IsCompound)
        {
            // Compound time: groups of 3
            var groups = new int[_numerator / 3];
            Array.Fill(groups, 3);
            return groups;
        }

        return _numerator switch
        {
            5 => [3, 2], // 5/4 or 5/8: typically 3+2
            7 => [2, 2, 3], // 7/8: typically 2+2+3
            9 when _denominator != 8 => [3, 3, 3], // 9/4: 3+3+3
            11 => [3, 3, 3, 2], // 11/8: 3+3+3+2
            13 => [3, 3, 3, 2, 2], // 13/8: 3+3+3+2+2
            _ => [_numerator] // Simple: one group
        };
    }

    /// <summary>
    /// Parses a time signature from a string (e.g., "4/4", "6/8").
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <returns>The parsed time signature.</returns>
    /// <exception cref="FormatException">Thrown if the string format is invalid.</exception>
    public static TimeSignature Parse(string s)
    {
        ArgumentNullException.ThrowIfNull(s);

        var parts = s.Trim().Split('/');
        if (parts.Length != 2)
        {
            throw new FormatException($"Invalid time signature format: '{s}'. Expected format: 'N/D' (e.g., '4/4').");
        }

        if (!int.TryParse(parts[0], out int numerator))
        {
            throw new FormatException($"Invalid numerator in time signature: '{parts[0]}'.");
        }

        if (!int.TryParse(parts[1], out int denominator))
        {
            throw new FormatException($"Invalid denominator in time signature: '{parts[1]}'.");
        }

        return new TimeSignature(numerator, denominator);
    }

    /// <summary>
    /// Tries to parse a time signature from a string.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="result">The parsed time signature if successful.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParse(string? s, out TimeSignature result)
    {
        result = default;

        if (string.IsNullOrWhiteSpace(s))
        {
            return false;
        }

        var parts = s.Trim().Split('/');
        if (parts.Length != 2)
        {
            return false;
        }

        if (!int.TryParse(parts[0], out int numerator) ||
            !int.TryParse(parts[1], out int denominator))
        {
            return false;
        }

        if (numerator < 1 || numerator > 32 ||
            !IsPowerOfTwo(denominator) || denominator < 1 || denominator > 64)
        {
            return false;
        }

        result = new TimeSignature(numerator, denominator);
        return true;
    }

    private static bool IsPowerOfTwo(int value) => value > 0 && (value & (value - 1)) == 0;

    public bool Equals(TimeSignature other) => _numerator == other._numerator && _denominator == other._denominator;

    public override bool Equals(object? obj) => obj is TimeSignature other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(_numerator, _denominator);

    public static bool operator ==(TimeSignature left, TimeSignature right) => left.Equals(right);

    public static bool operator !=(TimeSignature left, TimeSignature right) => !left.Equals(right);

    public override string ToString() => $"{_numerator}/{_denominator}";

    /// <summary>
    /// Deconstructs the time signature into its numerator and denominator.
    /// </summary>
    public void Deconstruct(out int numerator, out int denominator)
    {
        numerator = _numerator;
        denominator = _denominator;
    }
}
