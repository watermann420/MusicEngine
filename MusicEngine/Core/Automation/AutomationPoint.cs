//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Represents a single automation point with time, value, and curve type.

namespace MusicEngine.Core.Automation;

/// <summary>
/// Defines the interpolation curve type between automation points.
/// </summary>
public enum AutomationCurveType
{
    /// <summary>
    /// Linear interpolation between points (straight line).
    /// </summary>
    Linear,

    /// <summary>
    /// Cubic bezier curve interpolation for smooth transitions.
    /// </summary>
    Bezier,

    /// <summary>
    /// Step/hold - instant change at the point time, no interpolation.
    /// </summary>
    Step,

    /// <summary>
    /// Exponential curve interpolation.
    /// </summary>
    Exponential,

    /// <summary>
    /// Logarithmic curve interpolation.
    /// </summary>
    Logarithmic,

    /// <summary>
    /// Smooth S-curve (ease-in-out) interpolation.
    /// </summary>
    SCurve,

    /// <summary>
    /// Fast attack, slow release curve.
    /// </summary>
    FastAttack,

    /// <summary>
    /// Slow attack, fast release curve.
    /// </summary>
    SlowAttack
}

/// <summary>
/// Represents a single automation point with time position, value, and curve settings.
/// </summary>
public class AutomationPoint : IComparable<AutomationPoint>, IEquatable<AutomationPoint>
{
    private static long _nextId;
    private readonly long _id;

    /// <summary>
    /// Gets the unique identifier for this automation point.
    /// </summary>
    public long Id => _id;

    /// <summary>
    /// Gets or sets the time position in beats or seconds.
    /// </summary>
    public double Time { get; set; }

    /// <summary>
    /// Gets or sets the parameter value at this point (normalized 0-1 or absolute).
    /// </summary>
    public float Value { get; set; }

    /// <summary>
    /// Gets or sets the interpolation curve type from this point to the next.
    /// </summary>
    public AutomationCurveType CurveType { get; set; } = AutomationCurveType.Linear;

    /// <summary>
    /// Gets or sets the tension value for bezier curves (-1 to 1).
    /// Negative values create concave curves, positive values create convex curves.
    /// </summary>
    public float Tension { get; set; }

    /// <summary>
    /// Gets or sets the first bezier control point X coordinate (0-1 relative to segment).
    /// </summary>
    public float BezierX1 { get; set; } = 0.25f;

    /// <summary>
    /// Gets or sets the first bezier control point Y coordinate (0-1 relative to segment).
    /// </summary>
    public float BezierY1 { get; set; } = 0.1f;

    /// <summary>
    /// Gets or sets the second bezier control point X coordinate (0-1 relative to segment).
    /// </summary>
    public float BezierX2 { get; set; } = 0.75f;

    /// <summary>
    /// Gets or sets the second bezier control point Y coordinate (0-1 relative to segment).
    /// </summary>
    public float BezierY2 { get; set; } = 0.9f;

    /// <summary>
    /// Gets or sets whether this point is selected in the UI.
    /// </summary>
    public bool IsSelected { get; set; }

    /// <summary>
    /// Gets or sets whether this point is locked and cannot be edited.
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>
    /// Gets or sets an optional label for this point.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Creates a new automation point with default values.
    /// </summary>
    public AutomationPoint()
    {
        _id = Interlocked.Increment(ref _nextId);
    }

    /// <summary>
    /// Creates a new automation point with the specified time and value.
    /// </summary>
    /// <param name="time">The time position in beats or seconds.</param>
    /// <param name="value">The parameter value at this point.</param>
    /// <param name="curveType">The interpolation curve type.</param>
    public AutomationPoint(double time, float value, AutomationCurveType curveType = AutomationCurveType.Linear)
        : this()
    {
        Time = time;
        Value = value;
        CurveType = curveType;
    }

    /// <summary>
    /// Creates a deep copy of this automation point.
    /// </summary>
    /// <returns>A new AutomationPoint with the same values but a new ID.</returns>
    public AutomationPoint Clone()
    {
        return new AutomationPoint
        {
            Time = Time,
            Value = Value,
            CurveType = CurveType,
            Tension = Tension,
            BezierX1 = BezierX1,
            BezierY1 = BezierY1,
            BezierX2 = BezierX2,
            BezierY2 = BezierY2,
            IsSelected = false, // Don't copy selection state
            IsLocked = IsLocked,
            Label = Label
        };
    }

    /// <summary>
    /// Creates a copy of this automation point with a specific time offset.
    /// </summary>
    /// <param name="timeOffset">The time offset to apply.</param>
    /// <returns>A new AutomationPoint at the offset time.</returns>
    public AutomationPoint CloneWithTimeOffset(double timeOffset)
    {
        var clone = Clone();
        clone.Time += timeOffset;
        return clone;
    }

    /// <summary>
    /// Compares this point to another by time.
    /// </summary>
    public int CompareTo(AutomationPoint? other)
    {
        if (other is null) return 1;
        return Time.CompareTo(other.Time);
    }

    /// <summary>
    /// Checks equality based on ID.
    /// </summary>
    public bool Equals(AutomationPoint? other)
    {
        return other is not null && _id == other._id;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is AutomationPoint other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return _id.GetHashCode();
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"AutomationPoint(Time={Time:F3}, Value={Value:F3}, Curve={CurveType})";
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(AutomationPoint? left, AutomationPoint? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(AutomationPoint? left, AutomationPoint? right)
    {
        return !(left == right);
    }

    /// <summary>
    /// Less than operator (compares by time).
    /// </summary>
    public static bool operator <(AutomationPoint? left, AutomationPoint? right)
    {
        if (left is null) return right is not null;
        return left.CompareTo(right) < 0;
    }

    /// <summary>
    /// Greater than operator (compares by time).
    /// </summary>
    public static bool operator >(AutomationPoint? left, AutomationPoint? right)
    {
        if (left is null) return false;
        return left.CompareTo(right) > 0;
    }

    /// <summary>
    /// Less than or equal operator (compares by time).
    /// </summary>
    public static bool operator <=(AutomationPoint? left, AutomationPoint? right)
    {
        if (left is null) return true;
        return left.CompareTo(right) <= 0;
    }

    /// <summary>
    /// Greater than or equal operator (compares by time).
    /// </summary>
    public static bool operator >=(AutomationPoint? left, AutomationPoint? right)
    {
        if (left is null) return right is null;
        return left.CompareTo(right) >= 0;
    }
}
