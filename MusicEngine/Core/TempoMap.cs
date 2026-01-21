//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: A map of tempo changes over time with support for linear interpolation (tempo ramps).

using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicEngine.Core;

/// <summary>
/// Represents a tempo change at a specific position.
/// </summary>
public sealed class TempoChange
{
    /// <summary>Gets the position in beats where this tempo change occurs.</summary>
    public double PositionBeats { get; }

    /// <summary>Gets the tempo in BPM at this position.</summary>
    public double Bpm { get; }

    /// <summary>Gets whether this tempo change ramps (interpolates) to the next tempo change.</summary>
    public bool IsRamp { get; }

    /// <summary>Gets the curve type for the ramp (0 = linear, positive = ease-in, negative = ease-out).</summary>
    public double RampCurve { get; }

    /// <summary>
    /// Creates a new tempo change.
    /// </summary>
    /// <param name="positionBeats">The position in beats where this tempo change occurs.</param>
    /// <param name="bpm">The tempo in BPM.</param>
    /// <param name="isRamp">Whether this tempo ramps to the next tempo change.</param>
    /// <param name="rampCurve">The curve type for the ramp (0 = linear).</param>
    public TempoChange(double positionBeats, double bpm, bool isRamp = false, double rampCurve = 0.0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(positionBeats, nameof(positionBeats));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(bpm, 0, nameof(bpm));

        PositionBeats = positionBeats;
        Bpm = Math.Clamp(bpm, 1.0, 999.0);
        IsRamp = isRamp;
        RampCurve = Math.Clamp(rampCurve, -1.0, 1.0);
    }

    public override string ToString() => IsRamp
        ? $"Tempo: {Bpm:F1} BPM at beat {PositionBeats:F2} (ramp, curve={RampCurve:F2})"
        : $"Tempo: {Bpm:F1} BPM at beat {PositionBeats:F2}";
}

/// <summary>
/// Manages tempo changes over the timeline with support for tempo ramps (linear interpolation).
/// Thread-safe for concurrent access.
/// </summary>
public sealed class TempoMap
{
    private readonly object _lock = new();
    private readonly SortedList<double, TempoChange> _tempoChanges = new();
    private double _defaultBpm = 120.0;

    /// <summary>
    /// Gets or sets the default tempo used when no tempo changes are defined.
    /// </summary>
    public double DefaultBpm
    {
        get
        {
            lock (_lock)
            {
                return _defaultBpm;
            }
        }
        set
        {
            lock (_lock)
            {
                _defaultBpm = Math.Clamp(value, 1.0, 999.0);
            }
        }
    }

    /// <summary>
    /// Gets all tempo changes in order of position.
    /// </summary>
    public IReadOnlyList<TempoChange> TempoChanges
    {
        get
        {
            lock (_lock)
            {
                return _tempoChanges.Values.ToArray();
            }
        }
    }

    /// <summary>
    /// Gets the number of tempo changes.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _tempoChanges.Count;
            }
        }
    }

    /// <summary>
    /// Creates a new empty TempoMap with the specified default BPM.
    /// </summary>
    /// <param name="defaultBpm">The default tempo in BPM.</param>
    public TempoMap(double defaultBpm = 120.0)
    {
        _defaultBpm = Math.Clamp(defaultBpm, 1.0, 999.0);
    }

    /// <summary>
    /// Adds a tempo change at the specified position.
    /// If a tempo change already exists at this position, it will be replaced.
    /// </summary>
    /// <param name="positionBeats">The position in beats.</param>
    /// <param name="bpm">The tempo in BPM.</param>
    /// <returns>The created tempo change.</returns>
    public TempoChange AddTempoChange(double positionBeats, double bpm)
    {
        return AddTempoChange(positionBeats, bpm, false, 0.0);
    }

    /// <summary>
    /// Adds a tempo change with optional ramping at the specified position.
    /// If a tempo change already exists at this position, it will be replaced.
    /// </summary>
    /// <param name="positionBeats">The position in beats.</param>
    /// <param name="bpm">The tempo in BPM.</param>
    /// <param name="isRamp">Whether this tempo should ramp to the next tempo change.</param>
    /// <param name="rampCurve">The curve type for the ramp (0 = linear).</param>
    /// <returns>The created tempo change.</returns>
    public TempoChange AddTempoChange(double positionBeats, double bpm, bool isRamp, double rampCurve = 0.0)
    {
        var tempoChange = new TempoChange(positionBeats, bpm, isRamp, rampCurve);

        lock (_lock)
        {
            _tempoChanges[positionBeats] = tempoChange;
        }

        return tempoChange;
    }

    /// <summary>
    /// Adds a tempo ramp between two positions.
    /// Creates a tempo change at the start position that ramps to a tempo change at the end position.
    /// </summary>
    /// <param name="startPositionBeats">The start position in beats.</param>
    /// <param name="endPositionBeats">The end position in beats.</param>
    /// <param name="startBpm">The tempo at the start.</param>
    /// <param name="endBpm">The tempo at the end.</param>
    /// <param name="curve">The curve type (0 = linear).</param>
    public void AddTempoRamp(double startPositionBeats, double endPositionBeats, double startBpm, double endBpm, double curve = 0.0)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(startPositionBeats, endPositionBeats, nameof(startPositionBeats));

        lock (_lock)
        {
            _tempoChanges[startPositionBeats] = new TempoChange(startPositionBeats, startBpm, true, curve);
            _tempoChanges[endPositionBeats] = new TempoChange(endPositionBeats, endBpm, false, 0.0);
        }
    }

    /// <summary>
    /// Removes the tempo change at the specified position.
    /// </summary>
    /// <param name="positionBeats">The position in beats.</param>
    /// <returns>True if a tempo change was removed, false otherwise.</returns>
    public bool RemoveTempoChange(double positionBeats)
    {
        lock (_lock)
        {
            return _tempoChanges.Remove(positionBeats);
        }
    }

    /// <summary>
    /// Removes all tempo changes in the specified range.
    /// </summary>
    /// <param name="startBeats">The start of the range (inclusive).</param>
    /// <param name="endBeats">The end of the range (exclusive).</param>
    /// <returns>The number of tempo changes removed.</returns>
    public int RemoveTempoChangesInRange(double startBeats, double endBeats)
    {
        lock (_lock)
        {
            var toRemove = _tempoChanges.Keys
                .Where(k => k >= startBeats && k < endBeats)
                .ToList();

            foreach (var key in toRemove)
            {
                _tempoChanges.Remove(key);
            }

            return toRemove.Count;
        }
    }

    /// <summary>
    /// Clears all tempo changes.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _tempoChanges.Clear();
        }
    }

    /// <summary>
    /// Gets the tempo at the specified position, accounting for tempo ramps.
    /// </summary>
    /// <param name="positionBeats">The position in beats.</param>
    /// <returns>The tempo in BPM at the specified position.</returns>
    public double GetTempoAt(double positionBeats)
    {
        lock (_lock)
        {
            if (_tempoChanges.Count == 0)
            {
                return _defaultBpm;
            }

            // Find the tempo change at or before the position
            TempoChange? currentChange = null;
            TempoChange? nextChange = null;

            for (int i = 0; i < _tempoChanges.Count; i++)
            {
                var change = _tempoChanges.Values[i];

                if (change.PositionBeats <= positionBeats)
                {
                    currentChange = change;
                    nextChange = i + 1 < _tempoChanges.Count ? _tempoChanges.Values[i + 1] : null;
                }
                else
                {
                    break;
                }
            }

            // If no tempo change found, return default or first tempo
            if (currentChange == null)
            {
                return _tempoChanges.Count > 0 ? _tempoChanges.Values[0].Bpm : _defaultBpm;
            }

            // If this is a ramp and there's a next tempo change, interpolate
            if (currentChange.IsRamp && nextChange != null)
            {
                double startPos = currentChange.PositionBeats;
                double endPos = nextChange.PositionBeats;
                double startBpm = currentChange.Bpm;
                double endBpm = nextChange.Bpm;

                // Calculate normalized position within the ramp
                double t = (positionBeats - startPos) / (endPos - startPos);
                t = Math.Clamp(t, 0.0, 1.0);

                // Apply curve if specified
                t = ApplyCurve(t, currentChange.RampCurve);

                // Linear interpolation
                return startBpm + (endBpm - startBpm) * t;
            }

            return currentChange.Bpm;
        }
    }

    /// <summary>
    /// Gets the tempo change at or before the specified position (without interpolation).
    /// </summary>
    /// <param name="positionBeats">The position in beats.</param>
    /// <returns>The tempo change at or before the position, or null if none exists.</returns>
    public TempoChange? GetTempoChangeAt(double positionBeats)
    {
        lock (_lock)
        {
            TempoChange? result = null;

            foreach (var change in _tempoChanges.Values)
            {
                if (change.PositionBeats <= positionBeats)
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
    /// Gets the next tempo change after the specified position.
    /// </summary>
    /// <param name="positionBeats">The position in beats.</param>
    /// <returns>The next tempo change, or null if none exists.</returns>
    public TempoChange? GetNextTempoChange(double positionBeats)
    {
        lock (_lock)
        {
            foreach (var change in _tempoChanges.Values)
            {
                if (change.PositionBeats > positionBeats)
                {
                    return change;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Converts a position in beats to time in seconds, accounting for all tempo changes.
    /// </summary>
    /// <param name="positionBeats">The position in beats.</param>
    /// <returns>The time in seconds.</returns>
    public double BeatsToSeconds(double positionBeats)
    {
        lock (_lock)
        {
            if (_tempoChanges.Count == 0)
            {
                return positionBeats * (60.0 / _defaultBpm);
            }

            double totalSeconds = 0.0;
            double currentBeat = 0.0;
            double currentBpm = _tempoChanges.Count > 0 && _tempoChanges.Values[0].PositionBeats == 0
                ? _tempoChanges.Values[0].Bpm
                : _defaultBpm;

            for (int i = 0; i < _tempoChanges.Count; i++)
            {
                var change = _tempoChanges.Values[i];
                var nextChange = i + 1 < _tempoChanges.Count ? _tempoChanges.Values[i + 1] : null;

                // Handle segment before this tempo change
                if (change.PositionBeats > currentBeat)
                {
                    double segmentBeats = Math.Min(change.PositionBeats, positionBeats) - currentBeat;
                    totalSeconds += segmentBeats * (60.0 / currentBpm);
                    currentBeat = change.PositionBeats;

                    if (currentBeat >= positionBeats)
                    {
                        return totalSeconds;
                    }
                }

                currentBpm = change.Bpm;

                // Handle ramp segment
                if (change.IsRamp && nextChange != null)
                {
                    double rampEndBeat = Math.Min(nextChange.PositionBeats, positionBeats);
                    double rampBeats = rampEndBeat - currentBeat;

                    if (rampBeats > 0)
                    {
                        // For ramps, we need to integrate the tempo curve
                        // Using numerical integration for accuracy with curved ramps
                        totalSeconds += IntegrateTempoRamp(
                            currentBeat, rampEndBeat,
                            change.Bpm, nextChange.Bpm,
                            change.RampCurve);
                        currentBeat = rampEndBeat;

                        if (currentBeat >= positionBeats)
                        {
                            return totalSeconds;
                        }
                    }

                    currentBpm = nextChange.Bpm;
                }
            }

            // Handle remaining beats after last tempo change
            if (currentBeat < positionBeats)
            {
                double remainingBeats = positionBeats - currentBeat;
                totalSeconds += remainingBeats * (60.0 / currentBpm);
            }

            return totalSeconds;
        }
    }

    /// <summary>
    /// Converts a time in seconds to position in beats, accounting for all tempo changes.
    /// </summary>
    /// <param name="seconds">The time in seconds.</param>
    /// <returns>The position in beats.</returns>
    public double SecondsToBeats(double seconds)
    {
        lock (_lock)
        {
            if (_tempoChanges.Count == 0)
            {
                return seconds * (_defaultBpm / 60.0);
            }

            double totalBeats = 0.0;
            double currentTime = 0.0;
            double currentBpm = _tempoChanges.Count > 0 && _tempoChanges.Values[0].PositionBeats == 0
                ? _tempoChanges.Values[0].Bpm
                : _defaultBpm;

            for (int i = 0; i < _tempoChanges.Count; i++)
            {
                var change = _tempoChanges.Values[i];
                var nextChange = i + 1 < _tempoChanges.Count ? _tempoChanges.Values[i + 1] : null;

                // Calculate time at this tempo change
                double changeTime = BeatsToSeconds(change.PositionBeats);

                // Handle segment before this tempo change
                if (changeTime > currentTime)
                {
                    double segmentTime = Math.Min(changeTime, seconds) - currentTime;
                    totalBeats += segmentTime * (currentBpm / 60.0);
                    currentTime = changeTime;

                    if (currentTime >= seconds)
                    {
                        return totalBeats;
                    }
                }

                currentBpm = change.Bpm;

                // Handle ramp segment
                if (change.IsRamp && nextChange != null)
                {
                    double rampEndTime = BeatsToSeconds(nextChange.PositionBeats);
                    double rampDuration = Math.Min(rampEndTime, seconds) - currentTime;

                    if (rampDuration > 0)
                    {
                        // For ramps, use iterative approach to find beat position
                        double rampBeats = FindBeatsInTempoRamp(
                            currentTime, rampDuration,
                            change.Bpm, nextChange.Bpm,
                            change.RampCurve);
                        totalBeats += rampBeats;
                        currentTime += rampDuration;

                        if (currentTime >= seconds)
                        {
                            return totalBeats;
                        }
                    }

                    currentBpm = nextChange.Bpm;
                }
            }

            // Handle remaining time after last tempo change
            if (currentTime < seconds)
            {
                double remainingTime = seconds - currentTime;
                totalBeats += remainingTime * (currentBpm / 60.0);
            }

            return totalBeats;
        }
    }

    /// <summary>
    /// Applies a curve function to a normalized value.
    /// </summary>
    private static double ApplyCurve(double t, double curve)
    {
        if (Math.Abs(curve) < 0.001)
        {
            return t; // Linear
        }

        if (curve > 0)
        {
            // Ease-in (slow start, fast end)
            return Math.Pow(t, 1.0 + curve * 2.0);
        }
        else
        {
            // Ease-out (fast start, slow end)
            return 1.0 - Math.Pow(1.0 - t, 1.0 - curve * 2.0);
        }
    }

    /// <summary>
    /// Integrates a tempo ramp to calculate the time duration.
    /// Uses numerical integration for accuracy with curved ramps.
    /// </summary>
    private static double IntegrateTempoRamp(double startBeat, double endBeat, double startBpm, double endBpm, double curve)
    {
        const int steps = 100;
        double totalSeconds = 0.0;
        double beatRange = endBeat - startBeat;
        double stepSize = beatRange / steps;

        for (int i = 0; i < steps; i++)
        {
            double t = (i + 0.5) / steps;
            double curvedT = ApplyCurve(t, curve);
            double bpm = startBpm + (endBpm - startBpm) * curvedT;
            totalSeconds += stepSize * (60.0 / bpm);
        }

        return totalSeconds;
    }

    /// <summary>
    /// Finds the number of beats in a tempo ramp given a time duration.
    /// Uses iterative approach for accuracy.
    /// </summary>
    private static double FindBeatsInTempoRamp(double startTime, double duration, double startBpm, double endBpm, double curve)
    {
        // Use bisection method to find beats
        const int maxIterations = 50;
        const double tolerance = 0.0001;

        double lowBeats = 0.0;
        double highBeats = duration * Math.Max(startBpm, endBpm) / 60.0 * 2.0; // Upper bound estimate

        for (int i = 0; i < maxIterations; i++)
        {
            double midBeats = (lowBeats + highBeats) / 2.0;
            double calculatedTime = IntegrateTempoRamp(0, midBeats, startBpm, endBpm, curve);

            if (Math.Abs(calculatedTime - duration) < tolerance)
            {
                return midBeats;
            }

            if (calculatedTime < duration)
            {
                lowBeats = midBeats;
            }
            else
            {
                highBeats = midBeats;
            }
        }

        return (lowBeats + highBeats) / 2.0;
    }

    /// <summary>
    /// Creates a TempoMap from an array of BPM values at regular intervals.
    /// </summary>
    /// <param name="bpmValues">Array of BPM values.</param>
    /// <param name="intervalBeats">The interval between tempo changes in beats.</param>
    /// <param name="useRamps">Whether to use ramps between tempo changes.</param>
    /// <returns>A new TempoMap.</returns>
    public static TempoMap FromArray(double[] bpmValues, double intervalBeats = 1.0, bool useRamps = false)
    {
        var map = new TempoMap(bpmValues.Length > 0 ? bpmValues[0] : 120.0);

        for (int i = 0; i < bpmValues.Length; i++)
        {
            bool isRamp = useRamps && i < bpmValues.Length - 1;
            map.AddTempoChange(i * intervalBeats, bpmValues[i], isRamp);
        }

        return map;
    }

    public override string ToString()
    {
        lock (_lock)
        {
            return $"TempoMap: {_tempoChanges.Count} changes, default {_defaultBpm:F1} BPM";
        }
    }
}
