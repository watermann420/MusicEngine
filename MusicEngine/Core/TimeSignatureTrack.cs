// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicEngine.Core;

/// <summary>
/// A time signature change point in the project timeline.
/// </summary>
public sealed class TimeSignaturePoint
{
    /// <summary>
    /// Gets or sets the bar number where this time signature change occurs (0-indexed).
    /// </summary>
    public int BarNumber { get; set; }

    /// <summary>
    /// Gets or sets the time signature at this point.
    /// </summary>
    public TimeSignature TimeSignature { get; set; }

    /// <summary>
    /// Gets the absolute beat position where this change occurs (calculated internally).
    /// </summary>
    public double AbsoluteBeat { get; internal set; }

    /// <summary>
    /// Creates a new time signature change point.
    /// </summary>
    /// <param name="barNumber">The bar number (0-indexed).</param>
    /// <param name="timeSignature">The time signature.</param>
    public TimeSignaturePoint(int barNumber, TimeSignature timeSignature)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(barNumber, nameof(barNumber));
        BarNumber = barNumber;
        TimeSignature = timeSignature;
    }

    public override string ToString() => $"Bar {BarNumber + 1}: {TimeSignature}";
}

/// <summary>
/// Event arguments for time signature changes.
/// </summary>
public sealed class TimeSignatureTrackChangedEventArgs : EventArgs
{
    /// <summary>Gets the bar number where the time signature changed.</summary>
    public int BarNumber { get; }

    /// <summary>Gets the new time signature at this bar.</summary>
    public TimeSignature NewTimeSignature { get; }

    /// <summary>
    /// Creates new time signature changed event args.
    /// </summary>
    /// <param name="barNumber">The bar number.</param>
    /// <param name="newTimeSignature">The new time signature.</param>
    public TimeSignatureTrackChangedEventArgs(int barNumber, TimeSignature newTimeSignature)
    {
        BarNumber = barNumber;
        NewTimeSignature = newTimeSignature;
    }
}

/// <summary>
/// Manages time signature changes throughout a project timeline.
/// Provides methods for adding, removing, and querying time signatures,
/// as well as converting between bar/beat positions and absolute beat positions.
/// Thread-safe for concurrent access.
/// </summary>
public sealed class TimeSignatureTrack
{
    private readonly object _lock = new();
    private readonly SortedList<int, TimeSignaturePoint> _points = new();
    private bool _cacheValid = false;

    /// <summary>
    /// Gets or sets the default time signature used when no changes are defined
    /// or for bars before the first time signature change.
    /// </summary>
    public TimeSignature DefaultTimeSignature { get; set; } = TimeSignature.Common;

    /// <summary>
    /// Event fired when a time signature change is added, removed, or modified.
    /// </summary>
    public event EventHandler<TimeSignatureTrackChangedEventArgs>? TimeSignatureChanged;

    /// <summary>
    /// Creates a new TimeSignatureTrack with 4/4 as the default time signature.
    /// </summary>
    public TimeSignatureTrack()
    {
    }

    /// <summary>
    /// Creates a new TimeSignatureTrack with the specified default time signature.
    /// </summary>
    /// <param name="defaultTimeSignature">The default time signature.</param>
    public TimeSignatureTrack(TimeSignature defaultTimeSignature)
    {
        DefaultTimeSignature = defaultTimeSignature;
    }

    /// <summary>
    /// Adds a time signature change at the specified bar.
    /// If a change already exists at this bar, it will be replaced.
    /// </summary>
    /// <param name="barNumber">The bar number (0-indexed).</param>
    /// <param name="timeSignature">The time signature.</param>
    public void AddTimeSignatureChange(int barNumber, TimeSignature timeSignature)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(barNumber, nameof(barNumber));

        lock (_lock)
        {
            _points[barNumber] = new TimeSignaturePoint(barNumber, timeSignature);
            InvalidateCache();
        }

        TimeSignatureChanged?.Invoke(this, new TimeSignatureTrackChangedEventArgs(barNumber, timeSignature));
    }

    /// <summary>
    /// Adds a time signature change using numerator and denominator.
    /// </summary>
    /// <param name="barNumber">The bar number (0-indexed).</param>
    /// <param name="numerator">The time signature numerator (beats per bar).</param>
    /// <param name="denominator">The time signature denominator (note value).</param>
    public void AddTimeSignatureChange(int barNumber, int numerator, int denominator)
    {
        AddTimeSignatureChange(barNumber, new TimeSignature(numerator, denominator));
    }

    /// <summary>
    /// Removes the time signature change at the specified bar.
    /// </summary>
    /// <param name="barNumber">The bar number.</param>
    /// <returns>True if a change was removed, false otherwise.</returns>
    public bool RemoveTimeSignatureChange(int barNumber)
    {
        bool removed;
        TimeSignature tsAtBar;

        lock (_lock)
        {
            removed = _points.Remove(barNumber);
            if (removed)
            {
                InvalidateCache();
            }
            tsAtBar = GetTimeSignatureAtBarInternal(barNumber);
        }

        if (removed)
        {
            TimeSignatureChanged?.Invoke(this, new TimeSignatureTrackChangedEventArgs(barNumber, tsAtBar));
        }

        return removed;
    }

    /// <summary>
    /// Gets the time signature at the specified bar.
    /// </summary>
    /// <param name="barNumber">The bar number (0-indexed).</param>
    /// <returns>The time signature at the specified bar.</returns>
    public TimeSignature GetTimeSignatureAtBar(int barNumber)
    {
        lock (_lock)
        {
            return GetTimeSignatureAtBarInternal(barNumber);
        }
    }

    private TimeSignature GetTimeSignatureAtBarInternal(int barNumber)
    {
        if (_points.Count == 0)
        {
            return DefaultTimeSignature;
        }

        TimeSignaturePoint? result = null;

        foreach (var point in _points.Values)
        {
            if (point.BarNumber <= barNumber)
            {
                result = point;
            }
            else
            {
                break;
            }
        }

        return result?.TimeSignature ?? DefaultTimeSignature;
    }

    /// <summary>
    /// Gets the time signature at the specified absolute beat position.
    /// </summary>
    /// <param name="beat">The absolute beat position.</param>
    /// <returns>The time signature at the specified position.</returns>
    public TimeSignature GetTimeSignatureAtBeat(double beat)
    {
        lock (_lock)
        {
            EnsureCacheValid();
            var (bar, _) = AbsoluteBeatToBarBeatInternal(beat);
            return GetTimeSignatureAtBarInternal(bar);
        }
    }

    /// <summary>
    /// Gets all time signature points in bar order.
    /// </summary>
    /// <returns>A read-only list of time signature points.</returns>
    public IReadOnlyList<TimeSignaturePoint> GetTimeSignaturePoints()
    {
        lock (_lock)
        {
            EnsureCacheValid();
            return _points.Values.ToArray();
        }
    }

    /// <summary>
    /// Converts a bar and beat position to an absolute beat position (in quarter notes).
    /// </summary>
    /// <param name="bar">The bar number (0-indexed).</param>
    /// <param name="beat">The beat within the bar (0-indexed).</param>
    /// <returns>The absolute beat position in quarter notes.</returns>
    public double BarBeatToAbsoluteBeat(int bar, double beat)
    {
        lock (_lock)
        {
            EnsureCacheValid();
            return BarBeatToAbsoluteBeatInternal(bar, beat);
        }
    }

    private double BarBeatToAbsoluteBeatInternal(int bar, double beat)
    {
        double absoluteBeat = GetBarStartBeatInternal(bar);
        var timeSignature = GetTimeSignatureAtBarInternal(bar);
        absoluteBeat += beat * timeSignature.BeatLengthInQuarterNotes;
        return absoluteBeat;
    }

    /// <summary>
    /// Converts an absolute beat position to a bar and beat position.
    /// </summary>
    /// <param name="absoluteBeat">The absolute beat position in quarter notes.</param>
    /// <returns>A tuple of (bar, beat) where both are 0-indexed.</returns>
    public (int Bar, double Beat) AbsoluteBeatToBarBeat(double absoluteBeat)
    {
        lock (_lock)
        {
            EnsureCacheValid();
            return AbsoluteBeatToBarBeatInternal(absoluteBeat);
        }
    }

    private (int Bar, double Beat) AbsoluteBeatToBarBeatInternal(double absoluteBeat)
    {
        if (absoluteBeat < 0)
        {
            return (0, 0);
        }

        if (_points.Count == 0)
        {
            var barLength = DefaultTimeSignature.BarLengthInQuarterNotes;
            int bar = (int)(absoluteBeat / barLength);
            double beat = (absoluteBeat - bar * barLength) / DefaultTimeSignature.BeatLengthInQuarterNotes;
            return (bar, beat);
        }

        int currentBar = 0;
        double currentAbsoluteBeat = 0.0;
        var currentTimeSignature = _points.Count > 0 && _points.Values[0].BarNumber == 0
            ? _points.Values[0].TimeSignature
            : DefaultTimeSignature;

        foreach (var point in _points.Values)
        {
            if (point.AbsoluteBeat > absoluteBeat)
            {
                break;
            }

            // Calculate bars in segment before this change
            if (point.AbsoluteBeat > currentAbsoluteBeat)
            {
                double segmentBeats = Math.Min(point.AbsoluteBeat, absoluteBeat) - currentAbsoluteBeat;
                int barsInSegment = (int)(segmentBeats / currentTimeSignature.BarLengthInQuarterNotes);
                currentBar += barsInSegment;
                currentAbsoluteBeat = point.AbsoluteBeat;

                if (currentAbsoluteBeat >= absoluteBeat)
                {
                    double remainingBeats = absoluteBeat - (currentAbsoluteBeat - barsInSegment * currentTimeSignature.BarLengthInQuarterNotes);
                    int barFromRemaining = (int)(remainingBeats / currentTimeSignature.BarLengthInQuarterNotes);
                    double beatInBar = (remainingBeats - barFromRemaining * currentTimeSignature.BarLengthInQuarterNotes)
                                       / currentTimeSignature.BeatLengthInQuarterNotes;
                    return (currentBar - barsInSegment + barFromRemaining, beatInBar);
                }
            }

            currentBar = point.BarNumber;
            currentTimeSignature = point.TimeSignature;
        }

        // Calculate remaining bars
        if (currentAbsoluteBeat < absoluteBeat)
        {
            double remainingBeats = absoluteBeat - currentAbsoluteBeat;
            int remainingBars = (int)(remainingBeats / currentTimeSignature.BarLengthInQuarterNotes);
            double beatInBar = (remainingBeats - remainingBars * currentTimeSignature.BarLengthInQuarterNotes)
                               / currentTimeSignature.BeatLengthInQuarterNotes;
            currentBar += remainingBars;
            return (currentBar, beatInBar);
        }

        return (currentBar, 0);
    }

    /// <summary>
    /// Gets the absolute beat position where the specified bar starts.
    /// </summary>
    /// <param name="barNumber">The bar number (0-indexed).</param>
    /// <returns>The absolute beat position at the start of the bar.</returns>
    public double GetBarStartBeat(int barNumber)
    {
        lock (_lock)
        {
            EnsureCacheValid();
            return GetBarStartBeatInternal(barNumber);
        }
    }

    private double GetBarStartBeatInternal(int barNumber)
    {
        if (_points.Count == 0)
        {
            return barNumber * DefaultTimeSignature.BarLengthInQuarterNotes;
        }

        double absoluteBeat = 0.0;
        int currentBar = 0;
        var currentTimeSignature = _points.Count > 0 && _points.Values[0].BarNumber == 0
            ? _points.Values[0].TimeSignature
            : DefaultTimeSignature;

        foreach (var point in _points.Values)
        {
            if (point.BarNumber > barNumber)
            {
                break;
            }

            // Add beats for bars before this change
            if (point.BarNumber > currentBar)
            {
                int barsInSegment = Math.Min(point.BarNumber, barNumber) - currentBar;
                absoluteBeat += barsInSegment * currentTimeSignature.BarLengthInQuarterNotes;
                currentBar = point.BarNumber;

                if (currentBar >= barNumber)
                {
                    return absoluteBeat;
                }
            }

            currentTimeSignature = point.TimeSignature;
        }

        // Add remaining bars
        if (currentBar < barNumber)
        {
            int remainingBars = barNumber - currentBar;
            absoluteBeat += remainingBars * currentTimeSignature.BarLengthInQuarterNotes;
        }

        return absoluteBeat;
    }

    /// <summary>
    /// Gets the total beats from the start to the specified bar.
    /// </summary>
    /// <param name="barNumber">The bar number (0-indexed).</param>
    /// <returns>The total beats up to (but not including) the specified bar.</returns>
    public double GetTotalBeatsToBar(int barNumber)
    {
        return GetBarStartBeat(barNumber);
    }

    /// <summary>
    /// Clears all time signature changes, leaving only the default time signature.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _points.Clear();
            InvalidateCache();
        }
    }

    /// <summary>
    /// Gets the number of time signature change points.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _points.Count;
            }
        }
    }

    /// <summary>
    /// Checks if a time signature change exists at the specified bar.
    /// </summary>
    /// <param name="barNumber">The bar number.</param>
    /// <returns>True if a change exists at this bar.</returns>
    public bool HasChangeAtBar(int barNumber)
    {
        lock (_lock)
        {
            return _points.ContainsKey(barNumber);
        }
    }

    /// <summary>
    /// Gets the time signature point at the specified bar, if one exists.
    /// </summary>
    /// <param name="barNumber">The bar number.</param>
    /// <returns>The time signature point, or null if no change exists at this bar.</returns>
    public TimeSignaturePoint? GetPointAtBar(int barNumber)
    {
        lock (_lock)
        {
            EnsureCacheValid();
            return _points.TryGetValue(barNumber, out var point) ? point : null;
        }
    }

    /// <summary>
    /// Gets the next time signature change after the specified bar.
    /// </summary>
    /// <param name="barNumber">The bar number.</param>
    /// <returns>The next time signature point, or null if none exists.</returns>
    public TimeSignaturePoint? GetNextChange(int barNumber)
    {
        lock (_lock)
        {
            EnsureCacheValid();
            foreach (var point in _points.Values)
            {
                if (point.BarNumber > barNumber)
                {
                    return point;
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Gets the previous time signature change at or before the specified bar.
    /// </summary>
    /// <param name="barNumber">The bar number.</param>
    /// <returns>The previous time signature point, or null if none exists.</returns>
    public TimeSignaturePoint? GetPreviousChange(int barNumber)
    {
        lock (_lock)
        {
            EnsureCacheValid();
            TimeSignaturePoint? result = null;
            foreach (var point in _points.Values)
            {
                if (point.BarNumber <= barNumber)
                {
                    result = point;
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
    /// Gets the bar length in quarter notes at the specified bar.
    /// </summary>
    /// <param name="barNumber">The bar number.</param>
    /// <returns>The bar length in quarter notes.</returns>
    public double GetBarLengthAt(int barNumber)
    {
        return GetTimeSignatureAtBar(barNumber).BarLengthInQuarterNotes;
    }

    /// <summary>
    /// Gets beat information at the specified absolute beat position.
    /// </summary>
    /// <param name="absoluteBeat">The absolute beat position.</param>
    /// <returns>A BeatInfo object with position details.</returns>
    public BeatInfo GetBeatInfo(double absoluteBeat)
    {
        lock (_lock)
        {
            EnsureCacheValid();
            var (bar, beat) = AbsoluteBeatToBarBeatInternal(absoluteBeat);
            var timeSignature = GetTimeSignatureAtBarInternal(bar);
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
                QuarterNotePosition = absoluteBeat
            };
        }
    }

    private void InvalidateCache()
    {
        _cacheValid = false;
    }

    private void EnsureCacheValid()
    {
        if (_cacheValid)
        {
            return;
        }

        // Recalculate absolute beat positions for all changes
        double absoluteBeat = 0.0;
        int currentBar = 0;
        var currentTimeSignature = DefaultTimeSignature;

        foreach (var point in _points.Values)
        {
            // Calculate absolute beats up to this change
            if (point.BarNumber > currentBar)
            {
                int barsInSegment = point.BarNumber - currentBar;
                absoluteBeat += barsInSegment * currentTimeSignature.BarLengthInQuarterNotes;
                currentBar = point.BarNumber;
            }

            point.AbsoluteBeat = absoluteBeat;
            currentTimeSignature = point.TimeSignature;
        }

        _cacheValid = true;
    }

    public override string ToString()
    {
        lock (_lock)
        {
            return $"TimeSignatureTrack: {_points.Count} changes, default {DefaultTimeSignature}";
        }
    }
}
