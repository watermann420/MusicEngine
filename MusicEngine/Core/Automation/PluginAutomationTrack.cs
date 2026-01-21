//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Container for managing multiple parameter automations for a VST plugin.

using MusicEngine.Core;

namespace MusicEngine.Core.Automation;

/// <summary>
/// Contains all parameter automations for a single VST plugin instance.
/// Provides a unified interface for managing, playing back, and recording automation.
/// Thread-safe for concurrent access.
/// </summary>
public sealed class PluginAutomationTrack
{
    private readonly object _lock = new();
    private readonly Dictionary<int, VstParameterAutomation> _automations = new();
    private readonly string _pluginId;
    private IVstPlugin? _plugin;
    private bool _enabled = true;
    private bool _isRecording;
    private int _recordingParameterIndex = -1;
    private double _lastProcessedPosition = -1;

    /// <summary>
    /// Unique identifier for this automation track.
    /// </summary>
    public string TrackId { get; }

    /// <summary>
    /// The plugin instance ID this track is associated with.
    /// </summary>
    public string PluginId => _pluginId;

    /// <summary>
    /// Display name for this automation track.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Whether this automation track is enabled for playback.
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
    /// Whether automation is currently being recorded.
    /// </summary>
    public bool IsRecording
    {
        get
        {
            lock (_lock)
            {
                return _isRecording;
            }
        }
    }

    /// <summary>
    /// The parameter index currently being recorded, or -1 if not recording.
    /// </summary>
    public int RecordingParameterIndex
    {
        get
        {
            lock (_lock)
            {
                return _recordingParameterIndex;
            }
        }
    }

    /// <summary>
    /// Gets the number of parameters with automation data.
    /// </summary>
    public int AutomationCount
    {
        get
        {
            lock (_lock)
            {
                return _automations.Count;
            }
        }
    }

    /// <summary>
    /// Gets all parameter indices that have automation data.
    /// </summary>
    public IReadOnlyList<int> AutomatedParameterIndices
    {
        get
        {
            lock (_lock)
            {
                return _automations.Keys.ToList().AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Event raised when automation is applied to a parameter.
    /// </summary>
    public event EventHandler<ParameterAutomationEventArgs>? ParameterAutomated;

    /// <summary>
    /// Creates a new plugin automation track.
    /// </summary>
    /// <param name="pluginId">Unique identifier for the plugin instance.</param>
    /// <param name="plugin">Optional plugin reference for direct automation application.</param>
    public PluginAutomationTrack(string pluginId, IVstPlugin? plugin = null)
    {
        _pluginId = pluginId ?? throw new ArgumentNullException(nameof(pluginId));
        TrackId = Guid.NewGuid().ToString("N");
        _plugin = plugin;
        Name = plugin?.Name ?? pluginId;
    }

    /// <summary>
    /// Sets or updates the plugin reference.
    /// </summary>
    /// <param name="plugin">The VST plugin instance.</param>
    public void SetPlugin(IVstPlugin? plugin)
    {
        lock (_lock)
        {
            _plugin = plugin;
            if (plugin != null && string.IsNullOrEmpty(Name))
            {
                Name = plugin.Name;
            }
        }
    }

    /// <summary>
    /// Gets the automation for a specific parameter, creating it if it doesn't exist.
    /// </summary>
    /// <param name="parameterIndex">The parameter index.</param>
    /// <returns>The VstParameterAutomation for the parameter.</returns>
    public VstParameterAutomation GetOrCreateAutomation(int parameterIndex)
    {
        lock (_lock)
        {
            if (!_automations.TryGetValue(parameterIndex, out var automation))
            {
                automation = new VstParameterAutomation(_pluginId, parameterIndex);

                // Try to get parameter info from plugin
                if (_plugin != null)
                {
                    var paramInfo = _plugin.GetParameterInfo(parameterIndex);
                    if (paramInfo != null)
                    {
                        automation.Name = paramInfo.Name;
                    }
                }

                _automations[parameterIndex] = automation;
            }
            return automation;
        }
    }

    /// <summary>
    /// Gets the automation for a specific parameter if it exists.
    /// </summary>
    /// <param name="parameterIndex">The parameter index.</param>
    /// <returns>The VstParameterAutomation, or null if no automation exists.</returns>
    public VstParameterAutomation? GetAutomation(int parameterIndex)
    {
        lock (_lock)
        {
            return _automations.TryGetValue(parameterIndex, out var automation) ? automation : null;
        }
    }

    /// <summary>
    /// Checks if a parameter has automation data.
    /// </summary>
    /// <param name="parameterIndex">The parameter index.</param>
    /// <returns>True if the parameter has automation.</returns>
    public bool HasAutomation(int parameterIndex)
    {
        lock (_lock)
        {
            return _automations.TryGetValue(parameterIndex, out var automation) && automation.HasAutomation;
        }
    }

    /// <summary>
    /// Removes automation for a specific parameter.
    /// </summary>
    /// <param name="parameterIndex">The parameter index.</param>
    /// <returns>True if automation was removed.</returns>
    public bool RemoveAutomation(int parameterIndex)
    {
        lock (_lock)
        {
            return _automations.Remove(parameterIndex);
        }
    }

    /// <summary>
    /// Clears all automation data.
    /// </summary>
    public void ClearAllAutomation()
    {
        lock (_lock)
        {
            _automations.Clear();
        }
    }

    /// <summary>
    /// Adds an automation point for a parameter.
    /// </summary>
    /// <param name="parameterIndex">The parameter index.</param>
    /// <param name="positionBeats">Time position in beats.</param>
    /// <param name="value">Normalized value (0-1).</param>
    /// <param name="curveType">Interpolation curve type.</param>
    public void AddAutomationPoint(int parameterIndex, double positionBeats, float value, AutomationCurveType curveType = AutomationCurveType.Linear)
    {
        var automation = GetOrCreateAutomation(parameterIndex);
        automation.AddPoint(positionBeats, value, curveType);
    }

    /// <summary>
    /// Processes all automations at the specified position and applies values to the plugin.
    /// </summary>
    /// <param name="positionBeats">Current playback position in beats.</param>
    public void Process(double positionBeats)
    {
        lock (_lock)
        {
            if (!_enabled || _plugin == null)
                return;

            _lastProcessedPosition = positionBeats;

            foreach (var kvp in _automations)
            {
                var automation = kvp.Value;
                if (automation.Enabled && automation.HasAutomation)
                {
                    bool applied = automation.Apply(_plugin, positionBeats);
                    if (applied)
                    {
                        OnParameterAutomated(kvp.Key, automation.LastAppliedValue, positionBeats);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Processes automations and returns the values without applying them.
    /// Useful for preview or when the plugin is managed externally.
    /// </summary>
    /// <param name="positionBeats">Current position in beats.</param>
    /// <returns>Dictionary of parameter index to value.</returns>
    public Dictionary<int, float> GetValuesAt(double positionBeats)
    {
        var result = new Dictionary<int, float>();

        lock (_lock)
        {
            foreach (var kvp in _automations)
            {
                if (kvp.Value.HasAutomation)
                {
                    result[kvp.Key] = kvp.Value.GetValueAt(positionBeats);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Starts recording automation for a specific parameter.
    /// </summary>
    /// <param name="parameterIndex">The parameter index to record.</param>
    /// <param name="clearExisting">Whether to clear existing automation first.</param>
    public void StartRecording(int parameterIndex, bool clearExisting = false)
    {
        lock (_lock)
        {
            if (_isRecording)
            {
                StopRecording();
            }

            _recordingParameterIndex = parameterIndex;
            _isRecording = true;

            var automation = GetOrCreateAutomation(parameterIndex);
            if (clearExisting)
            {
                automation.Clear();
            }
        }
    }

    /// <summary>
    /// Records a value at the current position (call this during playback).
    /// </summary>
    /// <param name="positionBeats">Current playback position.</param>
    /// <param name="value">Value to record (normalized 0-1).</param>
    public void RecordValue(double positionBeats, float value)
    {
        lock (_lock)
        {
            if (!_isRecording || _recordingParameterIndex < 0)
                return;

            var automation = GetOrCreateAutomation(_recordingParameterIndex);
            automation.AddPoint(positionBeats, value, AutomationCurveType.Linear);
        }
    }

    /// <summary>
    /// Stops recording automation.
    /// </summary>
    public void StopRecording()
    {
        lock (_lock)
        {
            _isRecording = false;
            _recordingParameterIndex = -1;
        }
    }

    /// <summary>
    /// Resets all automation states (call when playback is stopped or restarted).
    /// </summary>
    public void ResetState()
    {
        lock (_lock)
        {
            _lastProcessedPosition = -1;
            foreach (var automation in _automations.Values)
            {
                automation.ResetState();
            }
        }
    }

    /// <summary>
    /// Creates automations for all automatable parameters from the plugin.
    /// Does not overwrite existing automations.
    /// </summary>
    public void InitializeFromPlugin()
    {
        lock (_lock)
        {
            if (_plugin == null)
                return;

            var allParams = _plugin.GetAllParameterInfo();
            foreach (var param in allParams)
            {
                if (param.IsAutomatable && !_automations.ContainsKey(param.Index))
                {
                    var automation = new VstParameterAutomation(_pluginId, param);
                    _automations[param.Index] = automation;
                }
            }
        }
    }

    /// <summary>
    /// Gets all automations as a read-only collection.
    /// </summary>
    public IReadOnlyDictionary<int, VstParameterAutomation> GetAllAutomations()
    {
        lock (_lock)
        {
            return new Dictionary<int, VstParameterAutomation>(_automations);
        }
    }

    /// <summary>
    /// Creates a deep copy of this automation track.
    /// </summary>
    /// <param name="newPluginId">Optional new plugin ID for the cloned track.</param>
    public PluginAutomationTrack Clone(string? newPluginId = null)
    {
        lock (_lock)
        {
            var clone = new PluginAutomationTrack(newPluginId ?? _pluginId)
            {
                Name = Name,
                Enabled = _enabled
            };

            foreach (var kvp in _automations)
            {
                var clonedAutomation = kvp.Value.Clone();
                clone._automations[kvp.Key] = clonedAutomation;
            }

            return clone;
        }
    }

    /// <summary>
    /// Merges automation from another track into this track.
    /// </summary>
    /// <param name="other">The track to merge from.</param>
    /// <param name="overwrite">If true, overwrites existing automations; if false, skips them.</param>
    public void MergeFrom(PluginAutomationTrack other, bool overwrite = false)
    {
        if (other == null)
            throw new ArgumentNullException(nameof(other));

        lock (_lock)
        {
            foreach (var kvp in other._automations)
            {
                if (overwrite || !_automations.ContainsKey(kvp.Key))
                {
                    _automations[kvp.Key] = kvp.Value.Clone();
                }
            }
        }
    }

    private void OnParameterAutomated(int parameterIndex, float value, double position)
    {
        ParameterAutomated?.Invoke(this, new ParameterAutomationEventArgs(parameterIndex, value, position));
    }

    /// <summary>
    /// Returns a string representation of this track.
    /// </summary>
    public override string ToString()
    {
        return $"PluginAutomationTrack: {Name} (Plugin: {_pluginId}, Automations: {AutomationCount})";
    }
}

/// <summary>
/// Event arguments for parameter automation events.
/// </summary>
public sealed class ParameterAutomationEventArgs : EventArgs
{
    /// <summary>
    /// The parameter index that was automated.
    /// </summary>
    public int ParameterIndex { get; }

    /// <summary>
    /// The value that was applied.
    /// </summary>
    public float Value { get; }

    /// <summary>
    /// The position in beats where the automation occurred.
    /// </summary>
    public double PositionBeats { get; }

    /// <summary>
    /// Creates a new ParameterAutomationEventArgs.
    /// </summary>
    public ParameterAutomationEventArgs(int parameterIndex, float value, double positionBeats)
    {
        ParameterIndex = parameterIndex;
        Value = value;
        PositionBeats = positionBeats;
    }
}
