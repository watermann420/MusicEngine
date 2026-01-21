//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Manages a collection of automation points with various interpolation methods.

using System.Collections;

namespace MusicEngine.Core.Automation;

/// <summary>
/// Represents a curve of automation points with interpolation capabilities.
/// Thread-safe for concurrent read/write operations.
/// </summary>
public class AutomationCurve : IEnumerable<AutomationPoint>
{
    private readonly object _lock = new();
    private readonly List<AutomationPoint> _points = [];
    private bool _isDirty = true;
    private List<AutomationPoint>? _sortedCache;

    /// <summary>
    /// Gets the number of points in the curve.
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
    /// Gets all points sorted by time.
    /// </summary>
    public IReadOnlyList<AutomationPoint> Points
    {
        get
        {
            lock (_lock)
            {
                EnsureSorted();
                return _sortedCache!.AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Gets the minimum time in the curve.
    /// </summary>
    public double MinTime
    {
        get
        {
            lock (_lock)
            {
                if (_points.Count == 0) return 0;
                EnsureSorted();
                return _sortedCache![0].Time;
            }
        }
    }

    /// <summary>
    /// Gets the maximum time in the curve.
    /// </summary>
    public double MaxTime
    {
        get
        {
            lock (_lock)
            {
                if (_points.Count == 0) return 0;
                EnsureSorted();
                return _sortedCache![^1].Time;
            }
        }
    }

    /// <summary>
    /// Gets the duration of the curve (MaxTime - MinTime).
    /// </summary>
    public double Duration => MaxTime - MinTime;

    /// <summary>
    /// Fired when points are added, removed, or modified.
    /// </summary>
    public event EventHandler? CurveChanged;

    /// <summary>
    /// Creates a new empty automation curve.
    /// </summary>
    public AutomationCurve()
    {
    }

    /// <summary>
    /// Creates a new automation curve with initial points.
    /// </summary>
    /// <param name="points">The initial points.</param>
    public AutomationCurve(IEnumerable<AutomationPoint> points)
    {
        foreach (var point in points)
        {
            _points.Add(point.Clone());
        }
        _isDirty = true;
    }

    /// <summary>
    /// Adds a point to the curve.
    /// </summary>
    /// <param name="point">The point to add.</param>
    public void AddPoint(AutomationPoint point)
    {
        lock (_lock)
        {
            _points.Add(point);
            _isDirty = true;
        }
        OnCurveChanged();
    }

    /// <summary>
    /// Adds a point at the specified time and value.
    /// </summary>
    /// <param name="time">The time position.</param>
    /// <param name="value">The parameter value.</param>
    /// <param name="curveType">The curve type for interpolation.</param>
    /// <returns>The created point.</returns>
    public AutomationPoint AddPoint(double time, float value, AutomationCurveType curveType = AutomationCurveType.Linear)
    {
        var point = new AutomationPoint(time, value, curveType);
        AddPoint(point);
        return point;
    }

    /// <summary>
    /// Removes a point from the curve.
    /// </summary>
    /// <param name="point">The point to remove.</param>
    /// <returns>True if the point was removed, false otherwise.</returns>
    public bool RemovePoint(AutomationPoint point)
    {
        bool removed;
        lock (_lock)
        {
            removed = _points.Remove(point);
            if (removed) _isDirty = true;
        }
        if (removed) OnCurveChanged();
        return removed;
    }

    /// <summary>
    /// Removes a point at the specified time (within tolerance).
    /// </summary>
    /// <param name="time">The time to search for.</param>
    /// <param name="tolerance">The time tolerance for matching.</param>
    /// <returns>True if a point was removed, false otherwise.</returns>
    public bool RemovePointAtTime(double time, double tolerance = 0.001)
    {
        int removed;
        lock (_lock)
        {
            removed = _points.RemoveAll(p => Math.Abs(p.Time - time) < tolerance);
            if (removed > 0) _isDirty = true;
        }
        if (removed > 0) OnCurveChanged();
        return removed > 0;
    }

    /// <summary>
    /// Removes all points in the specified time range.
    /// </summary>
    /// <param name="startTime">The start time (inclusive).</param>
    /// <param name="endTime">The end time (inclusive).</param>
    /// <returns>The number of points removed.</returns>
    public int RemovePointsInRange(double startTime, double endTime)
    {
        int removed;
        lock (_lock)
        {
            removed = _points.RemoveAll(p => p.Time >= startTime && p.Time <= endTime);
            if (removed > 0) _isDirty = true;
        }
        if (removed > 0) OnCurveChanged();
        return removed;
    }

    /// <summary>
    /// Clears all points from the curve.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _points.Clear();
            _sortedCache = null;
            _isDirty = true;
        }
        OnCurveChanged();
    }

    /// <summary>
    /// Gets the interpolated value at the specified time.
    /// </summary>
    /// <param name="time">The time position.</param>
    /// <returns>The interpolated value.</returns>
    public float GetValueAtTime(double time)
    {
        lock (_lock)
        {
            if (_points.Count == 0)
                return 0f;

            EnsureSorted();
            var sorted = _sortedCache!;

            // Before first point - return first value
            if (time <= sorted[0].Time)
                return sorted[0].Value;

            // After last point - return last value
            if (time >= sorted[^1].Time)
                return sorted[^1].Value;

            // Find surrounding points using binary search
            int index = FindPointIndexBefore(time);
            var p1 = sorted[index];
            var p2 = sorted[index + 1];

            // Calculate normalized position between points
            double t = (time - p1.Time) / (p2.Time - p1.Time);

            // Interpolate based on curve type
            return Interpolate(p1, p2, (float)t);
        }
    }

    /// <summary>
    /// Gets the point at or just before the specified time.
    /// </summary>
    /// <param name="time">The time position.</param>
    /// <returns>The point, or null if no points exist.</returns>
    public AutomationPoint? GetPointAtOrBefore(double time)
    {
        lock (_lock)
        {
            if (_points.Count == 0) return null;

            EnsureSorted();
            var sorted = _sortedCache!;

            if (time < sorted[0].Time) return null;

            int index = FindPointIndexBefore(time);
            return sorted[index];
        }
    }

    /// <summary>
    /// Gets the closest point to the specified time.
    /// </summary>
    /// <param name="time">The time position.</param>
    /// <returns>The closest point, or null if no points exist.</returns>
    public AutomationPoint? GetClosestPoint(double time)
    {
        lock (_lock)
        {
            if (_points.Count == 0) return null;

            EnsureSorted();
            var sorted = _sortedCache!;

            AutomationPoint? closest = null;
            double minDistance = double.MaxValue;

            foreach (var point in sorted)
            {
                double distance = Math.Abs(point.Time - time);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = point;
                }
            }

            return closest;
        }
    }

    /// <summary>
    /// Gets points in the specified time range.
    /// </summary>
    /// <param name="startTime">The start time (inclusive).</param>
    /// <param name="endTime">The end time (inclusive).</param>
    /// <returns>List of points in the range.</returns>
    public IReadOnlyList<AutomationPoint> GetPointsInRange(double startTime, double endTime)
    {
        lock (_lock)
        {
            EnsureSorted();
            return _sortedCache!.Where(p => p.Time >= startTime && p.Time <= endTime).ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Shifts all points by the specified time offset.
    /// </summary>
    /// <param name="offset">The time offset to apply.</param>
    public void ShiftTime(double offset)
    {
        lock (_lock)
        {
            foreach (var point in _points)
            {
                point.Time += offset;
            }
            _isDirty = true;
        }
        OnCurveChanged();
    }

    /// <summary>
    /// Scales all point times by the specified factor.
    /// </summary>
    /// <param name="factor">The scale factor.</param>
    public void ScaleTime(double factor)
    {
        lock (_lock)
        {
            foreach (var point in _points)
            {
                point.Time *= factor;
            }
            _isDirty = true;
        }
        OnCurveChanged();
    }

    /// <summary>
    /// Scales all point values by the specified factor.
    /// </summary>
    /// <param name="factor">The scale factor.</param>
    public void ScaleValues(float factor)
    {
        lock (_lock)
        {
            foreach (var point in _points)
            {
                point.Value *= factor;
            }
        }
        OnCurveChanged();
    }

    /// <summary>
    /// Clamps all values to the specified range.
    /// </summary>
    /// <param name="min">The minimum value.</param>
    /// <param name="max">The maximum value.</param>
    public void ClampValues(float min, float max)
    {
        lock (_lock)
        {
            foreach (var point in _points)
            {
                point.Value = Math.Clamp(point.Value, min, max);
            }
        }
        OnCurveChanged();
    }

    /// <summary>
    /// Creates a deep copy of this curve.
    /// </summary>
    /// <returns>A new AutomationCurve with cloned points.</returns>
    public AutomationCurve Clone()
    {
        lock (_lock)
        {
            var clone = new AutomationCurve();
            foreach (var point in _points)
            {
                clone._points.Add(point.Clone());
            }
            clone._isDirty = true;
            return clone;
        }
    }

    private void EnsureSorted()
    {
        if (_isDirty || _sortedCache == null)
        {
            _sortedCache = [.. _points.OrderBy(p => p.Time)];
            _isDirty = false;
        }
    }

    private int FindPointIndexBefore(double time)
    {
        var sorted = _sortedCache!;
        int left = 0;
        int right = sorted.Count - 1;

        while (left < right)
        {
            int mid = (left + right + 1) / 2;
            if (sorted[mid].Time <= time)
                left = mid;
            else
                right = mid - 1;
        }

        return left;
    }

    private static float Interpolate(AutomationPoint p1, AutomationPoint p2, float t)
    {
        float interpolatedT = p1.CurveType switch
        {
            AutomationCurveType.Linear => t,
            AutomationCurveType.Step => 0f,
            AutomationCurveType.Bezier => BezierInterpolation(t, p1.BezierX1, p1.BezierY1, p1.BezierX2, p1.BezierY2),
            AutomationCurveType.Exponential => ExponentialInterpolation(t, p1.Tension),
            AutomationCurveType.Logarithmic => LogarithmicInterpolation(t, p1.Tension),
            AutomationCurveType.SCurve => SCurveInterpolation(t),
            AutomationCurveType.FastAttack => FastAttackInterpolation(t),
            AutomationCurveType.SlowAttack => SlowAttackInterpolation(t),
            _ => t
        };

        return p1.Value + (p2.Value - p1.Value) * interpolatedT;
    }

    private static float BezierInterpolation(float t, float x1, float y1, float x2, float y2)
    {
        // Cubic bezier from (0,0) through (x1,y1), (x2,y2) to (1,1)
        float t2 = t * t;
        float t3 = t2 * t;
        float mt = 1 - t;
        float mt2 = mt * mt;

        // Calculate Y coordinate on the bezier curve
        float y = 3 * mt2 * t * y1 + 3 * mt * t2 * y2 + t3;
        return Math.Clamp(y, 0f, 1f);
    }

    private static float ExponentialInterpolation(float t, float tension)
    {
        if (t <= 0) return 0;
        if (t >= 1) return 1;

        // Tension affects the curve shape: positive = convex, negative = concave
        float exp = 2f + tension * 2f;
        return MathF.Pow(t, exp);
    }

    private static float LogarithmicInterpolation(float t, float tension)
    {
        if (t <= 0) return 0;
        if (t >= 1) return 1;

        float exp = 1f / (2f + tension * 2f);
        return MathF.Pow(t, exp);
    }

    private static float SCurveInterpolation(float t)
    {
        // Smooth S-curve using smoothstep
        return t * t * (3f - 2f * t);
    }

    private static float FastAttackInterpolation(float t)
    {
        // Fast attack (logarithmic-like)
        if (t <= 0) return 0;
        if (t >= 1) return 1;
        return MathF.Pow(t, 0.5f);
    }

    private static float SlowAttackInterpolation(float t)
    {
        // Slow attack (exponential-like)
        if (t <= 0) return 0;
        if (t >= 1) return 1;
        return MathF.Pow(t, 2f);
    }

    private void OnCurveChanged()
    {
        CurveChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public IEnumerator<AutomationPoint> GetEnumerator()
    {
        lock (_lock)
        {
            EnsureSorted();
            return _sortedCache!.GetEnumerator();
        }
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
