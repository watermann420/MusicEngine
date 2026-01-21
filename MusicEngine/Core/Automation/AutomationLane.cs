//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Represents an automation lane for a single parameter on a target object.

using System.Reflection;

namespace MusicEngine.Core.Automation;

/// <summary>
/// Time mode for automation values.
/// </summary>
public enum AutomationTimeMode
{
    /// <summary>
    /// Time values are in beats (quarter notes).
    /// </summary>
    Beats,

    /// <summary>
    /// Time values are in seconds.
    /// </summary>
    Seconds
}

/// <summary>
/// Represents an automation lane for a single parameter on a target object.
/// Contains an AutomationCurve and handles applying values to the target.
/// </summary>
public class AutomationLane
{
    private static long _nextId;
    private readonly long _id;
    private readonly object _targetLock = new();
    private PropertyInfo? _cachedProperty;
    private IAutomatable? _automatableTarget;

    /// <summary>
    /// Gets the unique identifier for this lane.
    /// </summary>
    public long Id => _id;

    /// <summary>
    /// Gets or sets the name of this lane.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target object ID (for IAutomatable targets).
    /// </summary>
    public string TargetId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target object whose property will be automated.
    /// </summary>
    public object? TargetObject { get; private set; }

    /// <summary>
    /// Gets or sets the name of the property to automate.
    /// </summary>
    public string ParameterName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the automation curve containing the points.
    /// </summary>
    public AutomationCurve Curve { get; set; } = new();

    /// <summary>
    /// Gets or sets whether this lane is enabled for playback.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether this lane is muted (bypassed during playback).
    /// </summary>
    public bool IsMuted { get; set; }

    /// <summary>
    /// Gets or sets whether this lane is soloed (only soloed lanes play).
    /// </summary>
    public bool IsSoloed { get; set; }

    /// <summary>
    /// Gets or sets whether this lane is armed for recording.
    /// </summary>
    public bool IsArmed { get; set; }

    /// <summary>
    /// Gets or sets the time mode (beats or seconds).
    /// </summary>
    public AutomationTimeMode TimeMode { get; set; } = AutomationTimeMode.Beats;

    /// <summary>
    /// Gets or sets the minimum allowed value for the parameter.
    /// </summary>
    public float MinValue { get; set; }

    /// <summary>
    /// Gets or sets the maximum allowed value for the parameter.
    /// </summary>
    public float MaxValue { get; set; } = 1f;

    /// <summary>
    /// Gets or sets the default value for the parameter.
    /// </summary>
    public float DefaultValue { get; set; }

    /// <summary>
    /// Gets or sets the display color for this lane in the UI.
    /// </summary>
    public string Color { get; set; } = "#4B6EAF";

    /// <summary>
    /// Gets the last applied value.
    /// </summary>
    public float LastAppliedValue { get; private set; }

    /// <summary>
    /// Fired when a value is applied to the target.
    /// </summary>
    public event EventHandler<AutomationValueAppliedEventArgs>? ValueApplied;

    /// <summary>
    /// Fired when the lane configuration changes.
    /// </summary>
    public event EventHandler? LaneChanged;

    /// <summary>
    /// Creates a new automation lane.
    /// </summary>
    public AutomationLane()
    {
        _id = Interlocked.Increment(ref _nextId);
        Curve.CurveChanged += (_, _) => OnLaneChanged();
    }

    /// <summary>
    /// Creates a new automation lane for an IAutomatable target.
    /// </summary>
    /// <param name="target">The automatable target object.</param>
    /// <param name="parameterName">The parameter name to automate.</param>
    public AutomationLane(IAutomatable target, string parameterName) : this()
    {
        SetTarget(target, parameterName);
    }

    /// <summary>
    /// Creates a new automation lane for a generic object with a property.
    /// </summary>
    /// <param name="target">The target object.</param>
    /// <param name="propertyName">The property name to automate.</param>
    public AutomationLane(object target, string propertyName) : this()
    {
        SetTarget(target, propertyName);
    }

    /// <summary>
    /// Sets the target object and parameter for automation.
    /// </summary>
    /// <param name="target">The IAutomatable target.</param>
    /// <param name="parameterName">The parameter name.</param>
    public void SetTarget(IAutomatable target, string parameterName)
    {
        lock (_targetLock)
        {
            _automatableTarget = target;
            TargetObject = target;
            TargetId = target.AutomationId;
            ParameterName = parameterName;
            Name = $"{target.DisplayName}.{parameterName}";

            // Get parameter metadata
            MinValue = target.GetParameterMinValue(parameterName);
            MaxValue = target.GetParameterMaxValue(parameterName);
            DefaultValue = target.GetParameterDefaultValue(parameterName);

            _cachedProperty = null; // Clear property cache
        }
        OnLaneChanged();
    }

    /// <summary>
    /// Sets the target object and property for automation (reflection-based).
    /// </summary>
    /// <param name="target">The target object.</param>
    /// <param name="propertyName">The property name.</param>
    public void SetTarget(object target, string propertyName)
    {
        lock (_targetLock)
        {
            if (target is IAutomatable automatable)
            {
                SetTarget(automatable, propertyName);
                return;
            }

            _automatableTarget = null;
            TargetObject = target;
            TargetId = target.GetType().Name + "_" + target.GetHashCode();
            ParameterName = propertyName;
            Name = $"{target.GetType().Name}.{propertyName}";

            CacheProperty();
        }
        OnLaneChanged();
    }

    /// <summary>
    /// Adds a point to the automation curve.
    /// </summary>
    /// <param name="time">The time position.</param>
    /// <param name="value">The parameter value.</param>
    /// <param name="curveType">The curve type.</param>
    /// <returns>The created point.</returns>
    public AutomationPoint AddPoint(double time, float value, AutomationCurveType curveType = AutomationCurveType.Linear)
    {
        float clampedValue = Math.Clamp(value, MinValue, MaxValue);
        return Curve.AddPoint(time, clampedValue, curveType);
    }

    /// <summary>
    /// Removes a point from the automation curve.
    /// </summary>
    /// <param name="point">The point to remove.</param>
    /// <returns>True if removed, false otherwise.</returns>
    public bool RemovePoint(AutomationPoint point)
    {
        return Curve.RemovePoint(point);
    }

    /// <summary>
    /// Clears all points from the automation curve.
    /// </summary>
    public void ClearPoints()
    {
        Curve.Clear();
    }

    /// <summary>
    /// Gets the interpolated value at the specified time.
    /// </summary>
    /// <param name="time">The time position.</param>
    /// <returns>The interpolated value.</returns>
    public float GetValueAtTime(double time)
    {
        if (Curve.Count == 0)
            return DefaultValue;

        float value = Curve.GetValueAtTime(time);
        return Math.Clamp(value, MinValue, MaxValue);
    }

    /// <summary>
    /// Applies the automation value at the specified time to the target.
    /// </summary>
    /// <param name="time">The current time position.</param>
    /// <returns>True if the value was applied successfully, false otherwise.</returns>
    public bool Apply(double time)
    {
        if (!Enabled || IsMuted)
            return false;

        float value = GetValueAtTime(time);
        return ApplyValue(value);
    }

    /// <summary>
    /// Applies a specific value to the target.
    /// </summary>
    /// <param name="value">The value to apply.</param>
    /// <returns>True if applied successfully, false otherwise.</returns>
    public bool ApplyValue(float value)
    {
        lock (_targetLock)
        {
            if (TargetObject == null)
                return false;

            float clampedValue = Math.Clamp(value, MinValue, MaxValue);
            bool success = false;

            try
            {
                if (_automatableTarget != null)
                {
                    // Use IAutomatable interface
                    success = _automatableTarget.SetParameterValue(ParameterName, clampedValue);
                }
                else
                {
                    // Use reflection
                    CacheProperty();
                    if (_cachedProperty?.CanWrite == true)
                    {
                        object convertedValue = Convert.ChangeType(clampedValue, _cachedProperty.PropertyType);
                        _cachedProperty.SetValue(TargetObject, convertedValue);
                        success = true;
                    }
                }

                if (success)
                {
                    LastAppliedValue = clampedValue;
                    OnValueApplied(clampedValue);
                }
            }
            catch
            {
                // Silently ignore conversion or access errors
                success = false;
            }

            return success;
        }
    }

    /// <summary>
    /// Gets the current value from the target.
    /// </summary>
    /// <returns>The current value, or the default value if unavailable.</returns>
    public float GetCurrentValue()
    {
        lock (_targetLock)
        {
            if (TargetObject == null)
                return DefaultValue;

            try
            {
                if (_automatableTarget != null)
                {
                    return _automatableTarget.GetParameterValue(ParameterName) ?? DefaultValue;
                }

                CacheProperty();
                if (_cachedProperty?.CanRead == true)
                {
                    var value = _cachedProperty.GetValue(TargetObject);
                    if (value != null)
                    {
                        return Convert.ToSingle(value);
                    }
                }
            }
            catch
            {
                // Ignore errors
            }

            return DefaultValue;
        }
    }

    /// <summary>
    /// Resets the target to the default value.
    /// </summary>
    public void ResetToDefault()
    {
        ApplyValue(DefaultValue);
    }

    /// <summary>
    /// Creates a normalized (0-1) version of a value.
    /// </summary>
    /// <param name="value">The absolute value.</param>
    /// <returns>The normalized value.</returns>
    public float NormalizeValue(float value)
    {
        if (Math.Abs(MaxValue - MinValue) < float.Epsilon)
            return 0f;
        return (value - MinValue) / (MaxValue - MinValue);
    }

    /// <summary>
    /// Converts a normalized (0-1) value to absolute.
    /// </summary>
    /// <param name="normalizedValue">The normalized value.</param>
    /// <returns>The absolute value.</returns>
    public float DenormalizeValue(float normalizedValue)
    {
        return MinValue + normalizedValue * (MaxValue - MinValue);
    }

    /// <summary>
    /// Creates a deep copy of this lane.
    /// </summary>
    /// <returns>A new AutomationLane with the same settings.</returns>
    public AutomationLane Clone()
    {
        var clone = new AutomationLane
        {
            Name = Name,
            TargetId = TargetId,
            ParameterName = ParameterName,
            Enabled = Enabled,
            IsMuted = IsMuted,
            IsSoloed = IsSoloed,
            TimeMode = TimeMode,
            MinValue = MinValue,
            MaxValue = MaxValue,
            DefaultValue = DefaultValue,
            Color = Color,
            Curve = Curve.Clone()
        };
        return clone;
    }

    private void CacheProperty()
    {
        if (_cachedProperty == null && TargetObject != null && !string.IsNullOrEmpty(ParameterName))
        {
            _cachedProperty = TargetObject.GetType().GetProperty(ParameterName);
        }
    }

    private void OnValueApplied(float value)
    {
        ValueApplied?.Invoke(this, new AutomationValueAppliedEventArgs(ParameterName, value));
    }

    private void OnLaneChanged()
    {
        LaneChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// Event arguments for automation value applied events.
/// </summary>
public class AutomationValueAppliedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public string ParameterName { get; }

    /// <summary>
    /// Gets the applied value.
    /// </summary>
    public float Value { get; }

    /// <summary>
    /// Creates a new value applied event.
    /// </summary>
    /// <param name="parameterName">The parameter name.</param>
    /// <param name="value">The applied value.</param>
    public AutomationValueAppliedEventArgs(string parameterName, float value)
    {
        ParameterName = parameterName;
        Value = value;
    }
}
