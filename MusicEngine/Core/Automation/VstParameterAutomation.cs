//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Automation system for VST plugin parameters using AutomationCurve.

namespace MusicEngine.Core.Automation;

/// <summary>
/// Manages automation for a single VST plugin parameter.
/// Wraps an AutomationCurve and provides VST-specific functionality.
/// </summary>
public sealed class VstParameterAutomation
{
    private readonly object _lock = new();
    private readonly AutomationCurve _automationCurve;
    private bool _enabled = true;
    private float _lastAppliedValue = float.NaN;

    /// <summary>
    /// Unique identifier for the plugin instance this automation belongs to.
    /// </summary>
    public string PluginId { get; }

    /// <summary>
    /// Index of the parameter within the plugin.
    /// </summary>
    public int ParameterIndex { get; }

    /// <summary>
    /// Optional parameter ID (for VST3 plugins which use uint IDs).
    /// </summary>
    public uint ParameterId { get; init; }

    /// <summary>
    /// Display name for this automation (typically the parameter name).
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Whether this automation is enabled for playback.
    /// </summary>
    public bool Enabled
    {
        get
        {
            lock (_lock)
            {
                return _enabled;
            }
        }
        set
        {
            lock (_lock)
            {
                _enabled = value;
            }
        }
    }

    /// <summary>
    /// The underlying automation curve containing the automation points.
    /// </summary>
    public AutomationCurve AutomationCurve => _automationCurve;

    /// <summary>
    /// Gets the number of automation points.
    /// </summary>
    public int PointCount => _automationCurve.Count;

    /// <summary>
    /// Whether this automation has any points defined.
    /// </summary>
    public bool HasAutomation => _automationCurve.Count > 0;

    /// <summary>
    /// The last value that was applied to the plugin.
    /// </summary>
    public float LastAppliedValue
    {
        get
        {
            lock (_lock)
            {
                return _lastAppliedValue;
            }
        }
    }

    /// <summary>
    /// Creates a new VST parameter automation.
    /// </summary>
    /// <param name="pluginId">Unique identifier for the plugin instance.</param>
    /// <param name="parameterIndex">Index of the parameter to automate.</param>
    public VstParameterAutomation(string pluginId, int parameterIndex)
    {
        PluginId = pluginId ?? throw new ArgumentNullException(nameof(pluginId));
        ParameterIndex = parameterIndex;
        ParameterId = (uint)parameterIndex;

        _automationCurve = new AutomationCurve();
    }

    /// <summary>
    /// Creates a new VST parameter automation with parameter info.
    /// </summary>
    /// <param name="pluginId">Unique identifier for the plugin instance.</param>
    /// <param name="parameterInfo">Parameter information.</param>
    public VstParameterAutomation(string pluginId, VstParameterInfo parameterInfo)
        : this(pluginId, parameterInfo.Index)
    {
        Name = parameterInfo.Name;
        ParameterId = parameterInfo.ParameterId;
    }

    /// <summary>
    /// Adds an automation point at the specified position.
    /// </summary>
    /// <param name="positionBeats">Time position in beats.</param>
    /// <param name="value">Normalized parameter value (0-1).</param>
    /// <param name="curveType">Interpolation curve type to the next point.</param>
    public void AddPoint(double positionBeats, float value, AutomationCurveType curveType = AutomationCurveType.Linear)
    {
        _automationCurve.AddPoint(positionBeats, Math.Clamp(value, 0f, 1f), curveType);
    }

    /// <summary>
    /// Adds an automation point.
    /// </summary>
    /// <param name="point">The automation point to add.</param>
    public void AddPoint(AutomationPoint point)
    {
        _automationCurve.AddPoint(point.Time, point.Value, point.CurveType);
    }

    /// <summary>
    /// Removes an automation point at the specified position.
    /// </summary>
    /// <param name="positionBeats">Time position in beats.</param>
    /// <param name="tolerance">Time tolerance for matching.</param>
    /// <returns>True if a point was removed.</returns>
    public bool RemovePoint(double positionBeats, double tolerance = 0.01)
    {
        return _automationCurve.RemovePointAtTime(positionBeats, tolerance);
    }

    /// <summary>
    /// Clears all automation points.
    /// </summary>
    public void Clear()
    {
        _automationCurve.Clear();
    }

    /// <summary>
    /// Gets the interpolated value at the specified position.
    /// </summary>
    /// <param name="positionBeats">Time position in beats.</param>
    /// <returns>Interpolated normalized value (0-1).</returns>
    public float GetValueAt(double positionBeats)
    {
        return _automationCurve.GetValueAtTime(positionBeats);
    }

    /// <summary>
    /// Applies the automation value at the specified position to the plugin.
    /// </summary>
    /// <param name="plugin">The VST plugin to apply the value to.</param>
    /// <param name="positionBeats">Current playback position in beats.</param>
    /// <returns>True if a value was applied, false if automation is disabled or has no points.</returns>
    public bool Apply(IVstPlugin plugin, double positionBeats)
    {
        if (plugin == null)
            throw new ArgumentNullException(nameof(plugin));

        lock (_lock)
        {
            if (!_enabled || _automationCurve.Count == 0)
                return false;

            float value = _automationCurve.GetValueAtTime(positionBeats);

            // Only apply if value has changed (avoid unnecessary parameter updates)
            if (float.IsNaN(_lastAppliedValue) || Math.Abs(value - _lastAppliedValue) > 0.0001f)
            {
                plugin.SetParameterValue(ParameterIndex, value);
                _lastAppliedValue = value;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Creates a linear ramp between two values.
    /// </summary>
    /// <param name="startPosition">Start position in beats.</param>
    /// <param name="startValue">Start value (0-1).</param>
    /// <param name="endPosition">End position in beats.</param>
    /// <param name="endValue">End value (0-1).</param>
    public void AddRamp(double startPosition, float startValue, double endPosition, float endValue)
    {
        _automationCurve.AddPoint(startPosition, Math.Clamp(startValue, 0f, 1f), AutomationCurveType.Linear);
        _automationCurve.AddPoint(endPosition, Math.Clamp(endValue, 0f, 1f), AutomationCurveType.Linear);
    }

    /// <summary>
    /// Creates an exponential curve between two values.
    /// </summary>
    /// <param name="startPosition">Start position in beats.</param>
    /// <param name="startValue">Start value (0-1).</param>
    /// <param name="endPosition">End position in beats.</param>
    /// <param name="endValue">End value (0-1).</param>
    public void AddExponentialRamp(double startPosition, float startValue, double endPosition, float endValue)
    {
        _automationCurve.AddPoint(startPosition, Math.Clamp(startValue, 0f, 1f), AutomationCurveType.Exponential);
        _automationCurve.AddPoint(endPosition, Math.Clamp(endValue, 0f, 1f), AutomationCurveType.Linear);
    }

    /// <summary>
    /// Creates a step (instant) change at the specified position.
    /// </summary>
    /// <param name="position">Position in beats.</param>
    /// <param name="value">Target value (0-1).</param>
    public void AddStep(double position, float value)
    {
        _automationCurve.AddPoint(position, Math.Clamp(value, 0f, 1f), AutomationCurveType.Step);
    }

    /// <summary>
    /// Gets all automation points as a read-only list.
    /// </summary>
    public IReadOnlyList<AutomationPoint> GetPoints()
    {
        return _automationCurve.Points;
    }

    /// <summary>
    /// Copies automation data from another VstParameterAutomation.
    /// </summary>
    /// <param name="source">Source automation to copy from.</param>
    public void CopyFrom(VstParameterAutomation source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        lock (_lock)
        {
            _automationCurve.Clear();
            foreach (var point in source.GetPoints())
            {
                _automationCurve.AddPoint(point.Time, point.Value, point.CurveType);
            }
            _enabled = source.Enabled;
            Name = source.Name;
        }
    }

    /// <summary>
    /// Creates a deep copy of this automation.
    /// </summary>
    public VstParameterAutomation Clone()
    {
        var clone = new VstParameterAutomation(PluginId, ParameterIndex)
        {
            ParameterId = ParameterId,
            Name = Name,
            Enabled = Enabled
        };

        foreach (var point in _automationCurve.Points)
        {
            clone.AddPoint(point.Clone());
        }

        return clone;
    }

    /// <summary>
    /// Resets the last applied value tracking.
    /// Call this when playback is stopped or restarted.
    /// </summary>
    public void ResetState()
    {
        lock (_lock)
        {
            _lastAppliedValue = float.NaN;
        }
    }

    /// <summary>
    /// Returns a string representation of this automation.
    /// </summary>
    public override string ToString()
    {
        return $"Automation: {Name} (Plugin: {PluginId}, Param: {ParameterIndex}, Points: {PointCount})";
    }
}
