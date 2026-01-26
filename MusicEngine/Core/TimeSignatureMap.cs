// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicEngine.Core;

/// <summary>
/// Represents a time signature change at a specific bar.
/// </summary>
public sealed class TimeSignatureChange
{
    /// <summary>Gets the bar number where this time signature change occurs (0-indexed).</summary>
    public int Bar { get; }

    /// <summary>Gets the time signature at this bar.</summary>
    public TimeSignature TimeSignature { get; }

    /// <summary>Gets the position in quarter notes where this change occurs (calculated).</summary>
    public double PositionQuarterNotes { get; internal set; }

    /// <summary>
    /// Creates a new time signature change.
    /// </summary>
    /// <param name="bar">The bar number (0-indexed).</param>
    /// <param name="timeSignature">The time signature.</param>
    public TimeSignatureChange(int bar, TimeSignature timeSignature)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bar, nameof(bar));

        Bar = bar;
        TimeSignature = timeSignature;
    }

    public override string ToString() => $"Bar {Bar}: {TimeSignature}";
}

/// <summary>
/// Manages time signature changes over the timeline.
/// Time signatures are specified by bar number, not by beat position.
/// Thread-safe for concurrent access.
/// </summary>
public sealed class TimeSignatureMap
{
    private readonly object _lock = new();
    private readonly SortedList<int, TimeSignatureChange> _timeSignatureChanges = new();
    private TimeSignature _defaultTimeSignature = TimeSignature.Common;

    // Cache for quarter note positions
    private bool _positionsCacheValid = false;

    /// <summary>
    /// Gets or sets the default time signature used when no changes are defined.
    /// </summary>
    public TimeSignature DefaultTimeSignature
    {
        get
        {
            lock (_lock)
            {
                return _defaultTimeSignature;
            }
        }
        set
        {
            lock (_lock)
            {
                _defaultTimeSignature = value;
                InvalidateCache();
            }
        }
    }

    /// <summary>
    /// Gets all time signature changes in order of bar number.
    /// </summary>
    public IReadOnlyList<TimeSignatureChange> TimeSignatureChanges
    {
        get
        {
            lock (_lock)
            {
                EnsureCacheValid();
                return _timeSignatureChanges.Values.ToArray();
            }
        }
    }

    /// <summary>
    /// Gets the number of time signature changes.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _timeSignatureChanges.Count;
            }
        }
    }

    /// <summary>
    /// Creates a new TimeSignatureMap with the specified default time signature.
    /// </summary>
    /// <param name="defaultTimeSignature">The default time signature.</param>
    public TimeSignatureMap(TimeSignature defaultTimeSignature = default)
    {
        _defaultTimeSignature = defaultTimeSignature == default ? TimeSignature.Common : defaultTimeSignature;
    }

    /// <summary>
    /// Adds a time signature change at the specified bar.
    /// If a change already exists at this bar, it will be replaced.
    /// </summary>
    /// <param name="bar">The bar number (0-indexed).</param>
    /// <param name="timeSignature">The time signature.</param>
    /// <returns>The created time signature change.</returns>
    public TimeSignatureChange AddTimeSignatureChange(int bar, TimeSignature timeSignature)
    {
        var change = new TimeSignatureChange(bar, timeSignature);

        lock (_lock)
        {
            _timeSignatureChanges[bar] = change;
            InvalidateCache();
        }

        return change;
    }

    /// <summary>
    /// Adds a time signature change at the specified bar using numerator and denominator.
    /// </summary>
    /// <param name="bar">The bar number (0-indexed).</param>
    /// <param name="numerator">The time signature numerator.</param>
    /// <param name="denominator">The time signature denominator.</param>
    /// <returns>The created time signature change.</returns>
    public TimeSignatureChange AddTimeSignatureChange(int bar, int numerator, int denominator)
    {
        return AddTimeSignatureChange(bar, new TimeSignature(numerator, denominator));
    }

    /// <summary>
    /// Removes the time signature change at the specified bar.
    /// </summary>
    /// <param name="bar">The bar number.</param>
    /// <returns>True if a change was removed, false otherwise.</returns>
    public bool RemoveTimeSignatureChange(int bar)
    {
        lock (_lock)
        {
            bool removed = _timeSignatureChanges.Remove(bar);
            if (removed)
            {
                InvalidateCache();
            }
            return removed;
        }
    }

    /// <summary>
    /// Clears all time signature changes.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _timeSignatureChanges.Clear();
            InvalidateCache();
        }
    }

    /// <summary>
    /// Gets the time signature at the specified bar.
    /// </summary>
    /// <param name="bar">The bar number (0-indexed).</param>
    /// <returns>The time signature at the specified bar.</returns>
    public TimeSignature GetTimeSignatureAt(int bar)
    {
        lock (_lock)
        {
            if (_timeSignatureChanges.Count == 0)
            {
                return _defaultTimeSignature;
            }

            TimeSignatureChange? result = null;

            foreach (var change in _timeSignatureChanges.Values)
            {
                if (change.Bar <= bar)
                {
                    result = change;
                }
                else
                {
                    break;
                }
            }

            return result?.TimeSignature ?? _defaultTimeSignature;
        }
    }

    /// <summary>
    /// Gets the time signature at the specified quarter note position.
    /// </summary>
    /// <param name="quarterNotes">The position in quarter notes.</param>
    /// <returns>The time signature at the specified position.</returns>
    public TimeSignature GetTimeSignatureAtQuarterNotes(double quarterNotes)
    {
        int bar = QuarterNotesToBar(quarterNotes);
        return GetTimeSignatureAt(bar);
    }

    /// <summary>
    /// Gets the time signature change at or before the specified bar.
    /// </summary>
    /// <param name="bar">The bar number.</param>
    /// <returns>The time signature change, or null if none exists before this bar.</returns>
    public TimeSignatureChange? GetTimeSignatureChangeAt(int bar)
    {
        lock (_lock)
        {
            EnsureCacheValid();
            TimeSignatureChange? result = null;

            foreach (var change in _timeSignatureChanges.Values)
            {
                if (change.Bar <= bar)
                {
                    result = change;
                }
                else
                {
                    break;
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Gets the next time signature change after the specified bar.
    /// </summary>
    /// <param name="bar">The bar number.</param>
    /// <returns>The next time signature change, or null if none exists.</returns>
    public TimeSignatureChange? GetNextTimeSignatureChange(int bar)
    {
        lock (_lock)
        {
            EnsureCacheValid();

            foreach (var change in _timeSignatureChanges.Values)
            {
                if (change.Bar > bar)
                {
                    return change;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Converts a bar number to a position in quarter notes.
    /// </summary>
    /// <param name="bar">The bar number (0-indexed).</param>
    /// <returns>The position in quarter notes.</returns>
    public double BarToQuarterNotes(int bar)
    {
        lock (_lock)
        {
            if (_timeSignatureChanges.Count == 0)
            {
                return bar * _defaultTimeSignature.BarLengthInQuarterNotes;
            }

            EnsureCacheValid();

            double quarterNotes = 0.0;
            int currentBar = 0;
            TimeSignature currentTimeSignature = _timeSignatureChanges.Count > 0 && _timeSignatureChanges.Values[0].Bar == 0
                ? _timeSignatureChanges.Values[0].TimeSignature
                : _defaultTimeSignature;

            foreach (var change in _timeSignatureChanges.Values)
            {
                if (change.Bar > bar)
                {
                    break;
                }

                // Add quarter notes for bars before this change
                if (change.Bar > currentBar)
                {
                    int barsInSegment = Math.Min(change.Bar, bar) - currentBar;
                    quarterNotes += barsInSegment * currentTimeSignature.BarLengthInQuarterNotes;
                    currentBar = change.Bar;

                    if (currentBar >= bar)
                    {
                        return quarterNotes;
                    }
                }

                currentTimeSignature = change.TimeSignature;
            }

            // Add remaining bars
            if (currentBar < bar)
            {
                int remainingBars = bar - currentBar;
                quarterNotes += remainingBars * currentTimeSignature.BarLengthInQuarterNotes;
            }

            return quarterNotes;
        }
    }

    /// <summary>
    /// Converts a bar and beat position to quarter notes.
    /// </summary>
    /// <param name="bar">The bar number (0-indexed).</param>
    /// <param name="beat">The beat within the bar (0-indexed).</param>
    /// <returns>The position in quarter notes.</returns>
    public double BarBeatToQuarterNotes(int bar, double beat)
    {
        double barPosition = BarToQuarterNotes(bar);
        TimeSignature timeSignature = GetTimeSignatureAt(bar);
        return barPosition + beat * timeSignature.BeatLengthInQuarterNotes;
    }

    /// <summary>
    /// Converts a position in quarter notes to a bar number.
    /// </summary>
    /// <param name="quarterNotes">The position in quarter notes.</param>
    /// <returns>The bar number (0-indexed).</returns>
    public int QuarterNotesToBar(double quarterNotes)
    {
        lock (_lock)
        {
            if (_timeSignatureChanges.Count == 0)
            {
                return (int)(quarterNotes / _defaultTimeSignature.BarLengthInQuarterNotes);
            }

            EnsureCacheValid();

            int currentBar = 0;
            double currentQuarterNotes = 0.0;
            TimeSignature currentTimeSignature = _timeSignatureChanges.Count > 0 && _timeSignatureChanges.Values[0].Bar == 0
                ? _timeSignatureChanges.Values[0].TimeSignature
                : _defaultTimeSignature;

            foreach (var change in _timeSignatureChanges.Values)
            {
                if (change.PositionQuarterNotes > quarterNotes)
                {
                    break;
                }

                // Calculate bars in segment before this change
                if (change.PositionQuarterNotes > currentQuarterNotes)
                {
                    double segmentQuarterNotes = Math.Min(change.PositionQuarterNotes, quarterNotes) - currentQuarterNotes;
                    int barsInSegment = (int)(segmentQuarterNotes / currentTimeSignature.BarLengthInQuarterNotes);
                    currentBar += barsInSegment;
                    currentQuarterNotes = change.PositionQuarterNotes;

                    if (currentQuarterNotes >= quarterNotes)
                    {
                        return currentBar;
                    }
                }

                currentBar = change.Bar;
                currentTimeSignature = change.TimeSignature;
            }

            // Calculate remaining bars
            if (currentQuarterNotes < quarterNotes)
            {
                double remainingQuarterNotes = quarterNotes - currentQuarterNotes;
                int remainingBars = (int)(remainingQuarterNotes / currentTimeSignature.BarLengthInQuarterNotes);
                currentBar += remainingBars;
            }

            return currentBar;
        }
    }

    /// <summary>
    /// Converts a position in quarter notes to bar and beat.
    /// </summary>
    /// <param name="quarterNotes">The position in quarter notes.</param>
    /// <returns>A tuple of (bar, beat) where both are 0-indexed.</returns>
    public (int Bar, double Beat) QuarterNotesToBarBeat(double quarterNotes)
    {
        int bar = QuarterNotesToBar(quarterNotes);
        double barStartQuarterNotes = BarToQuarterNotes(bar);
        TimeSignature timeSignature = GetTimeSignatureAt(bar);
        double beatInQuarterNotes = quarterNotes - barStartQuarterNotes;
        double beat = beatInQuarterNotes / timeSignature.BeatLengthInQuarterNotes;
        return (bar, beat);
    }

    /// <summary>
    /// Gets the bar length in quarter notes at the specified bar.
    /// </summary>
    /// <param name="bar">The bar number.</param>
    /// <returns>The bar length in quarter notes.</returns>
    public double GetBarLengthAt(int bar)
    {
        return GetTimeSignatureAt(bar).BarLengthInQuarterNotes;
    }

    /// <summary>
    /// Gets the total length in quarter notes up to and including the specified bar.
    /// </summary>
    /// <param name="endBar">The end bar (inclusive).</param>
    /// <returns>The total length in quarter notes.</returns>
    public double GetTotalQuarterNotes(int endBar)
    {
        return BarToQuarterNotes(endBar + 1);
    }

    /// <summary>
    /// Gets information about a specific beat position.
    /// </summary>
    /// <param name="quarterNotes">The position in quarter notes.</param>
    /// <returns>Beat information including bar, beat, time signature, and accent.</returns>
    public BeatInfo GetBeatInfo(double quarterNotes)
    {
        var (bar, beat) = QuarterNotesToBarBeat(quarterNotes);
        var timeSignature = GetTimeSignatureAt(bar);
        var accentPattern = timeSignature.GetAccentPattern();
        int beatIndex = (int)beat % timeSignature.BeatsPerBar;
        double accent = beatIndex < accentPattern.Length ? accentPattern[beatIndex] : 0.25;

        return new BeatInfo
        {
            Bar = bar,
            Beat = beat,
            BeatIndex = beatIndex,
            TimeSignature = timeSignature,
            Accent = accent,
            IsDownbeat = beatIndex == 0,
            QuarterNotePosition = quarterNotes
        };
    }

    private void InvalidateCache()
    {
        _positionsCacheValid = false;
    }

    private void EnsureCacheValid()
    {
        if (_positionsCacheValid)
        {
            return;
        }

        // Recalculate quarter note positions for all changes
        double quarterNotes = 0.0;
        int currentBar = 0;
        TimeSignature currentTimeSignature = _defaultTimeSignature;

        foreach (var change in _timeSignatureChanges.Values)
        {
            // Calculate quarter notes up to this change
            if (change.Bar > currentBar)
            {
                int barsInSegment = change.Bar - currentBar;
                quarterNotes += barsInSegment * currentTimeSignature.BarLengthInQuarterNotes;
                currentBar = change.Bar;
            }

            change.PositionQuarterNotes = quarterNotes;
            currentTimeSignature = change.TimeSignature;
        }

        _positionsCacheValid = true;
    }

    public override string ToString()
    {
        lock (_lock)
        {
            return $"TimeSignatureMap: {_timeSignatureChanges.Count} changes, default {_defaultTimeSignature}";
        }
    }
}

/// <summary>
/// Information about a specific beat position.
/// </summary>
public sealed class BeatInfo
{
    /// <summary>Gets the bar number (0-indexed).</summary>
    public int Bar { get; init; }

    /// <summary>Gets the beat position within the bar (0-indexed, fractional).</summary>
    public double Beat { get; init; }

    /// <summary>Gets the integer beat index within the bar (0-indexed).</summary>
    public int BeatIndex { get; init; }

    /// <summary>Gets the time signature at this position.</summary>
    public TimeSignature TimeSignature { get; init; }

    /// <summary>Gets the accent value for this beat (0.0-1.0).</summary>
    public double Accent { get; init; }

    /// <summary>Gets whether this is the downbeat (first beat of the bar).</summary>
    public bool IsDownbeat { get; init; }

    /// <summary>Gets the position in quarter notes.</summary>
    public double QuarterNotePosition { get; init; }

    /// <summary>Gets a display string for the bar:beat position (1-indexed for display).</summary>
    public string DisplayPosition => $"{Bar + 1}:{BeatIndex + 1}";

    public override string ToString() => $"Bar {Bar + 1}, Beat {BeatIndex + 1} ({TimeSignature})";
}
