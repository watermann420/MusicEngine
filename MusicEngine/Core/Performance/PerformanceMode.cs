//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Live performance controller for real-time parameter control, scene management, and crossfading.

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Reflection;

namespace MusicEngine.Core.Performance;

#region Enumerations

/// <summary>
/// Scene transition mode for crossfading between scenes.
/// </summary>
public enum SceneTransitionMode
{
    /// <summary>Instant switch with no crossfade.</summary>
    Instant,
    /// <summary>Linear crossfade between scenes.</summary>
    Linear,
    /// <summary>Smooth ease-in-out crossfade.</summary>
    EaseInOut,
    /// <summary>Exponential crossfade - fast start, slow end.</summary>
    Exponential,
    /// <summary>Logarithmic crossfade - slow start, fast end.</summary>
    Logarithmic
}

/// <summary>
/// Trigger type for scene activation.
/// </summary>
public enum SceneTriggerType
{
    /// <summary>Triggered by MIDI note on.</summary>
    MidiNote,
    /// <summary>Triggered by MIDI CC message.</summary>
    MidiCC,
    /// <summary>Triggered by MIDI program change.</summary>
    MidiProgramChange,
    /// <summary>Triggered by keyboard shortcut.</summary>
    Keyboard,
    /// <summary>Triggered by OSC message.</summary>
    OSC,
    /// <summary>Manual trigger (no external mapping).</summary>
    Manual
}

/// <summary>
/// Parameter mapping curve type.
/// </summary>
public enum PerformanceCurve
{
    /// <summary>Linear mapping (default).</summary>
    Linear,
    /// <summary>Exponential curve - slower start, faster end.</summary>
    Exponential,
    /// <summary>Logarithmic curve - faster start, slower end.</summary>
    Logarithmic,
    /// <summary>S-curve for smooth transitions.</summary>
    SCurve
}

#endregion

#region Data Classes

/// <summary>
/// Represents a trigger configuration for a scene.
/// </summary>
public class SceneTrigger
{
    /// <summary>Type of trigger.</summary>
    [JsonPropertyName("type")]
    public SceneTriggerType Type { get; set; } = SceneTriggerType.Manual;

    /// <summary>MIDI channel (0-15, or -1 for omni).</summary>
    [JsonPropertyName("midiChannel")]
    public int MidiChannel { get; set; } = -1;

    /// <summary>MIDI note or CC number.</summary>
    [JsonPropertyName("midiValue")]
    public int MidiValue { get; set; }

    /// <summary>Keyboard key (e.g., "F1", "A", "1").</summary>
    [JsonPropertyName("keyboardKey")]
    public string KeyboardKey { get; set; } = string.Empty;

    /// <summary>Modifier keys required (Ctrl, Shift, Alt).</summary>
    [JsonPropertyName("modifiers")]
    public KeyModifiers Modifiers { get; set; } = KeyModifiers.None;

    /// <summary>OSC address pattern for OSC triggers.</summary>
    [JsonPropertyName("oscAddress")]
    public string OscAddress { get; set; } = string.Empty;

    /// <summary>Whether this trigger is enabled.</summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Creates an empty trigger.</summary>
    public SceneTrigger() { }

    /// <summary>Creates a MIDI note trigger.</summary>
    public static SceneTrigger MidiNoteTrigger(int note, int channel = -1) => new()
    {
        Type = SceneTriggerType.MidiNote,
        MidiValue = note,
        MidiChannel = channel
    };

    /// <summary>Creates a keyboard trigger.</summary>
    public static SceneTrigger KeyboardTrigger(string key, KeyModifiers modifiers = KeyModifiers.None) => new()
    {
        Type = SceneTriggerType.Keyboard,
        KeyboardKey = key,
        Modifiers = modifiers
    };

    /// <summary>Creates a MIDI CC trigger.</summary>
    public static SceneTrigger MidiCCTrigger(int cc, int channel = -1) => new()
    {
        Type = SceneTriggerType.MidiCC,
        MidiValue = cc,
        MidiChannel = channel
    };

    /// <summary>Creates a clone of this trigger.</summary>
    public SceneTrigger Clone() => new()
    {
        Type = Type,
        MidiChannel = MidiChannel,
        MidiValue = MidiValue,
        KeyboardKey = KeyboardKey,
        Modifiers = Modifiers,
        OscAddress = OscAddress,
        Enabled = Enabled
    };
}

/// <summary>
/// Keyboard modifier flags.
/// </summary>
[Flags]
public enum KeyModifiers
{
    None = 0,
    Ctrl = 1,
    Shift = 2,
    Alt = 4
}

/// <summary>
/// Represents a parameter value in a snapshot.
/// </summary>
public class ParameterValue
{
    /// <summary>Target object ID.</summary>
    [JsonPropertyName("targetId")]
    public string TargetId { get; set; } = string.Empty;

    /// <summary>Property name on the target object.</summary>
    [JsonPropertyName("propertyName")]
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>Parameter value.</summary>
    [JsonPropertyName("value")]
    public float Value { get; set; }

    /// <summary>Target object reference (not serialized).</summary>
    [JsonIgnore]
    public object? TargetObject { get; set; }

    /// <summary>Cached property info for performance.</summary>
    [JsonIgnore]
    public PropertyInfo? CachedProperty { get; set; }

    public ParameterValue() { }

    public ParameterValue(string targetId, string propertyName, float value)
    {
        TargetId = targetId;
        PropertyName = propertyName;
        Value = value;
    }

    /// <summary>Creates a clone of this parameter value.</summary>
    public ParameterValue Clone() => new()
    {
        TargetId = TargetId,
        PropertyName = PropertyName,
        Value = Value,
        TargetObject = TargetObject,
        CachedProperty = CachedProperty
    };
}

/// <summary>
/// Parameter mapping for real-time control.
/// </summary>
public class PerformanceParameterMapping
{
    /// <summary>Unique identifier for this mapping.</summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Display name for this mapping.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Target object ID.</summary>
    [JsonPropertyName("targetId")]
    public string TargetId { get; set; } = string.Empty;

    /// <summary>Property name on the target.</summary>
    [JsonPropertyName("propertyName")]
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>MIDI CC number for control (0-127).</summary>
    [JsonPropertyName("midiCC")]
    public int MidiCC { get; set; } = -1;

    /// <summary>MIDI channel for this mapping (0-15, or -1 for omni).</summary>
    [JsonPropertyName("midiChannel")]
    public int MidiChannel { get; set; } = -1;

    /// <summary>Minimum value for the parameter.</summary>
    [JsonPropertyName("minValue")]
    public float MinValue { get; set; } = 0f;

    /// <summary>Maximum value for the parameter.</summary>
    [JsonPropertyName("maxValue")]
    public float MaxValue { get; set; } = 1f;

    /// <summary>Curve type for value mapping.</summary>
    [JsonPropertyName("curve")]
    public PerformanceCurve Curve { get; set; } = PerformanceCurve.Linear;

    /// <summary>Whether this mapping is enabled.</summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Target object reference (not serialized).</summary>
    [JsonIgnore]
    public object? TargetObject { get; set; }

    /// <summary>Cached property info.</summary>
    [JsonIgnore]
    public PropertyInfo? CachedProperty { get; set; }

    /// <summary>Current parameter value.</summary>
    [JsonIgnore]
    public float CurrentValue { get; set; }

    public PerformanceParameterMapping() { }

    /// <summary>Scales a normalized value (0-1) using the configured curve.</summary>
    public float ScaleValue(float normalizedValue)
    {
        normalizedValue = Math.Clamp(normalizedValue, 0f, 1f);

        float curved = Curve switch
        {
            PerformanceCurve.Linear => normalizedValue,
            PerformanceCurve.Exponential => MathF.Pow(normalizedValue, 3f),
            PerformanceCurve.Logarithmic => MathF.Pow(normalizedValue, 1f / 3f),
            PerformanceCurve.SCurve => normalizedValue * normalizedValue * (3f - 2f * normalizedValue),
            _ => normalizedValue
        };

        return MinValue + (MaxValue - MinValue) * curved;
    }

    /// <summary>Creates a clone of this mapping.</summary>
    public PerformanceParameterMapping Clone() => new()
    {
        Id = Guid.NewGuid(),
        Name = Name,
        TargetId = TargetId,
        PropertyName = PropertyName,
        MidiCC = MidiCC,
        MidiChannel = MidiChannel,
        MinValue = MinValue,
        MaxValue = MaxValue,
        Curve = Curve,
        Enabled = Enabled,
        TargetObject = TargetObject,
        CachedProperty = CachedProperty,
        CurrentValue = CurrentValue
    };
}

/// <summary>
/// Represents a parameter snapshot containing saved parameter values.
/// </summary>
public class ParameterSnapshot
{
    /// <summary>Unique identifier.</summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Display name for this snapshot.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Description of this snapshot.</summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>When this snapshot was created.</summary>
    [JsonPropertyName("created")]
    public DateTime Created { get; set; } = DateTime.UtcNow;

    /// <summary>When this snapshot was last modified.</summary>
    [JsonPropertyName("modified")]
    public DateTime Modified { get; set; } = DateTime.UtcNow;

    /// <summary>Saved parameter values.</summary>
    [JsonPropertyName("parameters")]
    public List<ParameterValue> Parameters { get; set; } = new();

    /// <summary>Custom metadata.</summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = new();

    public ParameterSnapshot() { }

    public ParameterSnapshot(string name)
    {
        Name = name;
    }

    /// <summary>Gets a parameter value by target ID and property name.</summary>
    public ParameterValue? GetParameter(string targetId, string propertyName)
    {
        return Parameters.Find(p =>
            p.TargetId.Equals(targetId, StringComparison.OrdinalIgnoreCase) &&
            p.PropertyName.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Sets a parameter value in the snapshot.</summary>
    public void SetParameter(string targetId, string propertyName, float value)
    {
        var existing = GetParameter(targetId, propertyName);
        if (existing != null)
        {
            existing.Value = value;
        }
        else
        {
            Parameters.Add(new ParameterValue(targetId, propertyName, value));
        }
        Modified = DateTime.UtcNow;
    }

    /// <summary>Linearly interpolates between this snapshot and another.</summary>
    public ParameterSnapshot Lerp(ParameterSnapshot other, float t)
    {
        if (other == null)
            throw new ArgumentNullException(nameof(other));

        t = Math.Clamp(t, 0f, 1f);

        var result = new ParameterSnapshot
        {
            Name = t < 0.5f ? Name : other.Name,
            Description = $"Interpolation ({t:P0}) between '{Name}' and '{other.Name}'"
        };

        // Build lookup for other snapshot
        var otherParams = other.Parameters.ToDictionary(
            p => (p.TargetId.ToLowerInvariant(), p.PropertyName.ToLowerInvariant()),
            p => p);

        // Process all parameters from this snapshot
        foreach (var param in Parameters)
        {
            var key = (param.TargetId.ToLowerInvariant(), param.PropertyName.ToLowerInvariant());
            float value = param.Value;

            if (otherParams.TryGetValue(key, out var otherParam))
            {
                value = param.Value + (otherParam.Value - param.Value) * t;
                otherParams.Remove(key);
            }

            result.Parameters.Add(new ParameterValue
            {
                TargetId = param.TargetId,
                PropertyName = param.PropertyName,
                Value = value,
                TargetObject = param.TargetObject,
                CachedProperty = param.CachedProperty
            });
        }

        // Add remaining parameters from other snapshot
        foreach (var param in otherParams.Values)
        {
            result.Parameters.Add(param.Clone());
        }

        return result;
    }

    /// <summary>Creates a deep copy of this snapshot.</summary>
    public ParameterSnapshot Clone()
    {
        return new ParameterSnapshot
        {
            Id = Guid.NewGuid(),
            Name = Name + " (Copy)",
            Description = Description,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            Parameters = Parameters.Select(p => p.Clone()).ToList(),
            Metadata = new Dictionary<string, string>(Metadata)
        };
    }

    /// <summary>Serializes to JSON.</summary>
    public string ToJson()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        return JsonSerializer.Serialize(this, options);
    }

    /// <summary>Deserializes from JSON.</summary>
    public static ParameterSnapshot FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentNullException(nameof(json));

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        return JsonSerializer.Deserialize<ParameterSnapshot>(json, options)
               ?? throw new JsonException("Failed to deserialize ParameterSnapshot.");
    }
}

/// <summary>
/// Represents a performance scene with parameter snapshots and triggers.
/// </summary>
public class PerformanceScene
{
    /// <summary>Unique identifier.</summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Display name for this scene.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Scene index (for ordering).</summary>
    [JsonPropertyName("index")]
    public int Index { get; set; }

    /// <summary>Scene color for UI display (hex format).</summary>
    [JsonPropertyName("color")]
    public string Color { get; set; } = "#FF4444";

    /// <summary>Parameter snapshot for this scene.</summary>
    [JsonPropertyName("snapshot")]
    public ParameterSnapshot Snapshot { get; set; } = new();

    /// <summary>Triggers that activate this scene.</summary>
    [JsonPropertyName("triggers")]
    public List<SceneTrigger> Triggers { get; set; } = new();

    /// <summary>Transition mode when switching to this scene.</summary>
    [JsonPropertyName("transitionMode")]
    public SceneTransitionMode TransitionMode { get; set; } = SceneTransitionMode.EaseInOut;

    /// <summary>Transition time in milliseconds.</summary>
    [JsonPropertyName("transitionTimeMs")]
    public int TransitionTimeMs { get; set; } = 500;

    /// <summary>Whether this scene is enabled.</summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Custom metadata.</summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = new();

    public PerformanceScene() { }

    public PerformanceScene(string name, int index = 0)
    {
        Name = name;
        Index = index;
        Snapshot = new ParameterSnapshot(name);
    }

    /// <summary>Adds a MIDI note trigger to this scene.</summary>
    public void AddMidiNoteTrigger(int note, int channel = -1)
    {
        Triggers.Add(SceneTrigger.MidiNoteTrigger(note, channel));
    }

    /// <summary>Adds a keyboard trigger to this scene.</summary>
    public void AddKeyboardTrigger(string key, KeyModifiers modifiers = KeyModifiers.None)
    {
        Triggers.Add(SceneTrigger.KeyboardTrigger(key, modifiers));
    }

    /// <summary>Creates a deep copy of this scene.</summary>
    public PerformanceScene Clone()
    {
        return new PerformanceScene
        {
            Id = Guid.NewGuid(),
            Name = Name + " (Copy)",
            Index = Index,
            Color = Color,
            Snapshot = Snapshot.Clone(),
            Triggers = Triggers.Select(t => t.Clone()).ToList(),
            TransitionMode = TransitionMode,
            TransitionTimeMs = TransitionTimeMs,
            Enabled = Enabled,
            Metadata = new Dictionary<string, string>(Metadata)
        };
    }
}

#endregion

#region Event Arguments

/// <summary>
/// Event arguments for scene changes.
/// </summary>
public class SceneChangedEventArgs : EventArgs
{
    /// <summary>Previous scene (null if first scene).</summary>
    public PerformanceScene? PreviousScene { get; }

    /// <summary>New current scene.</summary>
    public PerformanceScene NewScene { get; }

    /// <summary>Whether a crossfade is in progress.</summary>
    public bool IsCrossfading { get; }

    public SceneChangedEventArgs(PerformanceScene? previousScene, PerformanceScene newScene, bool isCrossfading = false)
    {
        PreviousScene = previousScene;
        NewScene = newScene;
        IsCrossfading = isCrossfading;
    }
}

/// <summary>
/// Event arguments for crossfade progress.
/// </summary>
public class CrossfadeProgressEventArgs : EventArgs
{
    /// <summary>Source scene.</summary>
    public PerformanceScene FromScene { get; }

    /// <summary>Target scene.</summary>
    public PerformanceScene ToScene { get; }

    /// <summary>Crossfade progress (0.0 to 1.0).</summary>
    public float Progress { get; }

    public CrossfadeProgressEventArgs(PerformanceScene fromScene, PerformanceScene toScene, float progress)
    {
        FromScene = fromScene;
        ToScene = toScene;
        Progress = progress;
    }
}

/// <summary>
/// Event arguments for parameter value changes.
/// </summary>
public class ParameterChangedEventArgs : EventArgs
{
    /// <summary>The mapping that was changed.</summary>
    public PerformanceParameterMapping Mapping { get; }

    /// <summary>New value.</summary>
    public float Value { get; }

    /// <summary>MIDI CC value that triggered the change (0-127).</summary>
    public int MidiValue { get; }

    public ParameterChangedEventArgs(PerformanceParameterMapping mapping, float value, int midiValue)
    {
        Mapping = mapping;
        Value = value;
        MidiValue = midiValue;
    }
}

/// <summary>
/// Event arguments for MIDI learn events.
/// </summary>
public class PerformanceMidiLearnEventArgs : EventArgs
{
    /// <summary>The mapping being learned.</summary>
    public PerformanceParameterMapping? Mapping { get; }

    /// <summary>The scene being learned (for scene triggers).</summary>
    public PerformanceScene? Scene { get; }

    /// <summary>MIDI CC number learned.</summary>
    public int CC { get; }

    /// <summary>MIDI channel learned.</summary>
    public int Channel { get; }

    /// <summary>MIDI note learned (for note triggers).</summary>
    public int Note { get; }

    public PerformanceMidiLearnEventArgs(PerformanceParameterMapping? mapping, int cc, int channel)
    {
        Mapping = mapping;
        CC = cc;
        Channel = channel;
        Note = -1;
    }

    public PerformanceMidiLearnEventArgs(PerformanceScene scene, int note, int channel)
    {
        Scene = scene;
        Note = note;
        Channel = channel;
        CC = -1;
    }
}

#endregion

/// <summary>
/// Live performance controller for real-time parameter control, scene management, and crossfading.
/// Thread-safe implementation for audio-thread access.
/// </summary>
public class PerformanceMode : IDisposable
{
    private readonly object _lock = new();
    private readonly List<PerformanceScene> _scenes = new();
    private readonly List<PerformanceParameterMapping> _parameterMappings = new();
    private readonly Dictionary<string, object> _targetRegistry = new();
    private readonly List<ParameterSnapshot> _snapshots = new();

    private PerformanceScene? _currentScene;
    private PerformanceScene? _nextScene;
    private PerformanceScene? _crossfadeFromScene;
    private PerformanceScene? _crossfadeToScene;

    private bool _isCrossfading;
    private float _crossfadeProgress;
    private DateTime _crossfadeStartTime;
    private int _crossfadeDurationMs;
    private SceneTransitionMode _crossfadeMode;
    private CancellationTokenSource? _crossfadeCts;

    private bool _midiLearnEnabled;
    private object? _midiLearnTarget;
    private string _midiLearnPropertyName = string.Empty;
    private PerformanceScene? _midiLearnScene;

    private bool _disposed;

    #region Properties

    /// <summary>
    /// Gets the currently active scene.
    /// </summary>
    public PerformanceScene? CurrentScene
    {
        get
        {
            lock (_lock)
            {
                return _currentScene;
            }
        }
    }

    /// <summary>
    /// Gets the next scene queued for transition.
    /// </summary>
    public PerformanceScene? NextScene
    {
        get
        {
            lock (_lock)
            {
                return _nextScene;
            }
        }
    }

    /// <summary>
    /// Gets or sets the default crossfade time in milliseconds.
    /// </summary>
    public int CrossfadeTime { get; set; } = 500;

    /// <summary>
    /// Gets whether MIDI learn mode is enabled.
    /// </summary>
    public bool MidiLearnEnabled
    {
        get
        {
            lock (_lock)
            {
                return _midiLearnEnabled;
            }
        }
    }

    /// <summary>
    /// Gets whether a crossfade is currently in progress.
    /// </summary>
    public bool IsCrossfading
    {
        get
        {
            lock (_lock)
            {
                return _isCrossfading;
            }
        }
    }

    /// <summary>
    /// Gets the current crossfade progress (0.0 to 1.0).
    /// </summary>
    public float CrossfadeProgress
    {
        get
        {
            lock (_lock)
            {
                return _crossfadeProgress;
            }
        }
    }

    /// <summary>
    /// Gets all scenes.
    /// </summary>
    public IReadOnlyList<PerformanceScene> Scenes
    {
        get
        {
            lock (_lock)
            {
                return _scenes.ToList().AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Gets all parameter mappings.
    /// </summary>
    public IReadOnlyList<PerformanceParameterMapping> ParameterMappings
    {
        get
        {
            lock (_lock)
            {
                return _parameterMappings.ToList().AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Gets all saved snapshots.
    /// </summary>
    public IReadOnlyList<ParameterSnapshot> Snapshots
    {
        get
        {
            lock (_lock)
            {
                return _snapshots.ToList().AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Gets the number of scenes.
    /// </summary>
    public int SceneCount
    {
        get
        {
            lock (_lock)
            {
                return _scenes.Count;
            }
        }
    }

    #endregion

    #region Events

    /// <summary>Fired when the current scene changes.</summary>
    public event EventHandler<SceneChangedEventArgs>? SceneChanged;

    /// <summary>Fired during crossfade transitions.</summary>
    public event EventHandler<CrossfadeProgressEventArgs>? CrossfadeProgressChanged;

    /// <summary>Fired when a crossfade completes.</summary>
    public event EventHandler<PerformanceScene>? CrossfadeComplete;

    /// <summary>Fired when a parameter value changes via MIDI.</summary>
    public event EventHandler<ParameterChangedEventArgs>? ParameterChanged;

    /// <summary>Fired when MIDI learn completes.</summary>
    public event EventHandler<PerformanceMidiLearnEventArgs>? MidiLearned;

    /// <summary>Fired when a snapshot is saved.</summary>
    public event EventHandler<ParameterSnapshot>? SnapshotSaved;

    /// <summary>Fired when a snapshot is loaded.</summary>
    public event EventHandler<ParameterSnapshot>? SnapshotLoaded;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new PerformanceMode instance.
    /// </summary>
    public PerformanceMode()
    {
    }

    #endregion

    #region Target Registration

    /// <summary>
    /// Registers a target object with an ID for parameter mapping.
    /// </summary>
    /// <param name="id">Unique identifier for the target.</param>
    /// <param name="target">The target object.</param>
    public void RegisterTarget(string id, object target)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentNullException(nameof(id));
        if (target == null)
            throw new ArgumentNullException(nameof(target));

        lock (_lock)
        {
            _targetRegistry[id] = target;
            RebindMappingsInternal();
        }
    }

    /// <summary>
    /// Unregisters a target object.
    /// </summary>
    /// <param name="id">The target ID to unregister.</param>
    public void UnregisterTarget(string id)
    {
        lock (_lock)
        {
            _targetRegistry.Remove(id);
        }
    }

    /// <summary>
    /// Gets a registered target by ID.
    /// </summary>
    public object? GetTarget(string id)
    {
        lock (_lock)
        {
            return _targetRegistry.TryGetValue(id, out var target) ? target : null;
        }
    }

    private void RebindMappingsInternal()
    {
        foreach (var mapping in _parameterMappings)
        {
            if (!string.IsNullOrEmpty(mapping.TargetId) &&
                _targetRegistry.TryGetValue(mapping.TargetId, out var target))
            {
                mapping.TargetObject = target;
                mapping.CachedProperty = null;
            }
        }

        foreach (var scene in _scenes)
        {
            foreach (var param in scene.Snapshot.Parameters)
            {
                if (!string.IsNullOrEmpty(param.TargetId) &&
                    _targetRegistry.TryGetValue(param.TargetId, out var target))
                {
                    param.TargetObject = target;
                    param.CachedProperty = null;
                }
            }
        }
    }

    #endregion

    #region Scene Management

    /// <summary>
    /// Creates a new scene with the specified name.
    /// </summary>
    /// <param name="name">Scene name.</param>
    /// <returns>The created scene.</returns>
    public PerformanceScene CreateScene(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentNullException(nameof(name));

        lock (_lock)
        {
            var scene = new PerformanceScene(name, _scenes.Count);
            _scenes.Add(scene);
            return scene;
        }
    }

    /// <summary>
    /// Adds an existing scene to the performance mode.
    /// </summary>
    public void AddScene(PerformanceScene scene)
    {
        if (scene == null)
            throw new ArgumentNullException(nameof(scene));

        lock (_lock)
        {
            scene.Index = _scenes.Count;
            _scenes.Add(scene);
        }
    }

    /// <summary>
    /// Removes a scene by reference.
    /// </summary>
    public bool RemoveScene(PerformanceScene scene)
    {
        if (scene == null)
            return false;

        lock (_lock)
        {
            bool removed = _scenes.Remove(scene);
            if (removed)
            {
                ReindexScenes();
                if (_currentScene == scene)
                    _currentScene = null;
                if (_nextScene == scene)
                    _nextScene = null;
            }
            return removed;
        }
    }

    /// <summary>
    /// Removes a scene by index.
    /// </summary>
    public bool RemoveSceneAt(int index)
    {
        lock (_lock)
        {
            if (index < 0 || index >= _scenes.Count)
                return false;

            var scene = _scenes[index];
            _scenes.RemoveAt(index);
            ReindexScenes();

            if (_currentScene == scene)
                _currentScene = null;
            if (_nextScene == scene)
                _nextScene = null;

            return true;
        }
    }

    /// <summary>
    /// Gets a scene by index.
    /// </summary>
    public PerformanceScene? GetScene(int index)
    {
        lock (_lock)
        {
            return index >= 0 && index < _scenes.Count ? _scenes[index] : null;
        }
    }

    /// <summary>
    /// Gets a scene by name.
    /// </summary>
    public PerformanceScene? GetScene(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        lock (_lock)
        {
            return _scenes.Find(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Gets a scene by ID.
    /// </summary>
    public PerformanceScene? GetSceneById(Guid id)
    {
        lock (_lock)
        {
            return _scenes.Find(s => s.Id == id);
        }
    }

    private void ReindexScenes()
    {
        for (int i = 0; i < _scenes.Count; i++)
        {
            _scenes[i].Index = i;
        }
    }

    /// <summary>
    /// Reorders scenes by moving a scene to a new index.
    /// </summary>
    public void ReorderScene(int fromIndex, int toIndex)
    {
        lock (_lock)
        {
            if (fromIndex < 0 || fromIndex >= _scenes.Count)
                throw new ArgumentOutOfRangeException(nameof(fromIndex));
            if (toIndex < 0 || toIndex >= _scenes.Count)
                throw new ArgumentOutOfRangeException(nameof(toIndex));

            if (fromIndex == toIndex)
                return;

            var scene = _scenes[fromIndex];
            _scenes.RemoveAt(fromIndex);
            _scenes.Insert(toIndex, scene);
            ReindexScenes();
        }
    }

    #endregion

    #region Scene Triggering

    /// <summary>
    /// Triggers a scene immediately without crossfade.
    /// </summary>
    /// <param name="scene">The scene to trigger.</param>
    public void TriggerScene(PerformanceScene scene)
    {
        if (scene == null)
            throw new ArgumentNullException(nameof(scene));

        lock (_lock)
        {
            if (!scene.Enabled)
                return;

            // Cancel any ongoing crossfade
            _crossfadeCts?.Cancel();
            _isCrossfading = false;

            var previousScene = _currentScene;
            _currentScene = scene;
            _nextScene = null;

            ApplySceneSnapshot(scene);

            SceneChanged?.Invoke(this, new SceneChangedEventArgs(previousScene, scene, false));
        }
    }

    /// <summary>
    /// Triggers a scene by index.
    /// </summary>
    public void TriggerScene(int index)
    {
        var scene = GetScene(index);
        if (scene != null)
        {
            TriggerScene(scene);
        }
    }

    /// <summary>
    /// Triggers the next scene in sequence.
    /// </summary>
    public void TriggerNextScene()
    {
        lock (_lock)
        {
            if (_scenes.Count == 0)
                return;

            int currentIndex = _currentScene?.Index ?? -1;
            int nextIndex = (currentIndex + 1) % _scenes.Count;

            TriggerScene(_scenes[nextIndex]);
        }
    }

    /// <summary>
    /// Triggers the previous scene in sequence.
    /// </summary>
    public void TriggerPreviousScene()
    {
        lock (_lock)
        {
            if (_scenes.Count == 0)
                return;

            int currentIndex = _currentScene?.Index ?? 0;
            int prevIndex = (currentIndex - 1 + _scenes.Count) % _scenes.Count;

            TriggerScene(_scenes[prevIndex]);
        }
    }

    /// <summary>
    /// Queues a scene for the next transition.
    /// </summary>
    public void QueueScene(PerformanceScene scene)
    {
        if (scene == null)
            throw new ArgumentNullException(nameof(scene));

        lock (_lock)
        {
            _nextScene = scene;
        }
    }

    /// <summary>
    /// Triggers the queued scene if one exists.
    /// </summary>
    public void TriggerQueuedScene()
    {
        PerformanceScene? next;
        lock (_lock)
        {
            next = _nextScene;
        }

        if (next != null)
        {
            TriggerScene(next);
        }
    }

    private void ApplySceneSnapshot(PerformanceScene scene)
    {
        foreach (var param in scene.Snapshot.Parameters)
        {
            ApplyParameterValue(param);
        }
    }

    private void ApplyParameterValue(ParameterValue param)
    {
        if (param.TargetObject == null && !string.IsNullOrEmpty(param.TargetId))
        {
            _targetRegistry.TryGetValue(param.TargetId, out var target);
            param.TargetObject = target;
        }

        if (param.TargetObject == null)
            return;

        if (param.CachedProperty == null)
        {
            param.CachedProperty = param.TargetObject.GetType().GetProperty(param.PropertyName);
        }

        if (param.CachedProperty == null)
            return;

        try
        {
            var propertyType = param.CachedProperty.PropertyType;
            object value;

            if (propertyType == typeof(float))
                value = param.Value;
            else if (propertyType == typeof(double))
                value = (double)param.Value;
            else if (propertyType == typeof(int))
                value = (int)Math.Round(param.Value);
            else if (propertyType == typeof(bool))
                value = param.Value >= 0.5f;
            else
                value = Convert.ChangeType(param.Value, propertyType);

            param.CachedProperty.SetValue(param.TargetObject, value);
        }
        catch
        {
            // Ignore property set errors
        }
    }

    #endregion

    #region Crossfade

    /// <summary>
    /// Crossfades to a scene over the specified duration.
    /// </summary>
    /// <param name="scene">Target scene.</param>
    /// <param name="durationMs">Crossfade duration in milliseconds (uses scene default if not specified).</param>
    /// <param name="mode">Transition mode (uses scene default if not specified).</param>
    public void CrossfadeTo(PerformanceScene scene, int? durationMs = null, SceneTransitionMode? mode = null)
    {
        if (scene == null)
            throw new ArgumentNullException(nameof(scene));

        lock (_lock)
        {
            if (!scene.Enabled)
                return;

            // Cancel any ongoing crossfade
            _crossfadeCts?.Cancel();
            _crossfadeCts = new CancellationTokenSource();

            _crossfadeFromScene = _currentScene;
            _crossfadeToScene = scene;
            _crossfadeDurationMs = durationMs ?? scene.TransitionTimeMs;
            _crossfadeMode = mode ?? scene.TransitionMode;

            if (_crossfadeDurationMs <= 0 || _crossfadeMode == SceneTransitionMode.Instant)
            {
                TriggerScene(scene);
                return;
            }

            _isCrossfading = true;
            _crossfadeProgress = 0f;
            _crossfadeStartTime = DateTime.UtcNow;
            _nextScene = scene;

            SceneChanged?.Invoke(this, new SceneChangedEventArgs(_currentScene, scene, true));
        }

        // Start async crossfade
        _ = RunCrossfadeAsync(_crossfadeCts.Token);
    }

    /// <summary>
    /// Crossfades to a scene by index.
    /// </summary>
    public void CrossfadeTo(int index, int? durationMs = null, SceneTransitionMode? mode = null)
    {
        var scene = GetScene(index);
        if (scene != null)
        {
            CrossfadeTo(scene, durationMs, mode);
        }
    }

    /// <summary>
    /// Crossfades to the next scene.
    /// </summary>
    public void CrossfadeToNext(int? durationMs = null)
    {
        lock (_lock)
        {
            if (_scenes.Count == 0)
                return;

            int currentIndex = _currentScene?.Index ?? -1;
            int nextIndex = (currentIndex + 1) % _scenes.Count;

            CrossfadeTo(_scenes[nextIndex], durationMs);
        }
    }

    /// <summary>
    /// Crossfades to the previous scene.
    /// </summary>
    public void CrossfadeToPrevious(int? durationMs = null)
    {
        lock (_lock)
        {
            if (_scenes.Count == 0)
                return;

            int currentIndex = _currentScene?.Index ?? 0;
            int prevIndex = (currentIndex - 1 + _scenes.Count) % _scenes.Count;

            CrossfadeTo(_scenes[prevIndex], durationMs);
        }
    }

    /// <summary>
    /// Cancels any ongoing crossfade.
    /// </summary>
    public void CancelCrossfade()
    {
        lock (_lock)
        {
            _crossfadeCts?.Cancel();
            _isCrossfading = false;
            _crossfadeProgress = 0f;
            _nextScene = null;
        }
    }

    private async Task RunCrossfadeAsync(CancellationToken cancellationToken)
    {
        PerformanceScene? fromScene;
        PerformanceScene? toScene;
        int durationMs;
        SceneTransitionMode mode;

        lock (_lock)
        {
            fromScene = _crossfadeFromScene;
            toScene = _crossfadeToScene;
            durationMs = _crossfadeDurationMs;
            mode = _crossfadeMode;
        }

        if (toScene == null)
            return;

        var startTime = DateTime.UtcNow;
        var endTime = startTime.AddMilliseconds(durationMs);

        while (DateTime.UtcNow < endTime)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            var t = (float)(elapsed / durationMs);
            t = Math.Clamp(t, 0f, 1f);

            // Apply curve
            t = ApplyCurve(t, mode);

            lock (_lock)
            {
                _crossfadeProgress = t;
            }

            // Interpolate and apply parameters
            ApplyCrossfadeParameters(fromScene, toScene, t);

            CrossfadeProgressChanged?.Invoke(this, new CrossfadeProgressEventArgs(
                fromScene ?? toScene, toScene, t));

            try
            {
                await Task.Delay(16, cancellationToken); // ~60fps
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }

        // Complete the crossfade
        lock (_lock)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            _currentScene = toScene;
            _nextScene = null;
            _isCrossfading = false;
            _crossfadeProgress = 1f;
        }

        ApplySceneSnapshot(toScene);
        CrossfadeComplete?.Invoke(this, toScene);
    }

    private static float ApplyCurve(float t, SceneTransitionMode mode)
    {
        return mode switch
        {
            SceneTransitionMode.Linear => t,
            SceneTransitionMode.EaseInOut => t * t * (3f - 2f * t),
            SceneTransitionMode.Exponential => t * t * t,
            SceneTransitionMode.Logarithmic => MathF.Pow(t, 1f / 3f),
            _ => t
        };
    }

    private void ApplyCrossfadeParameters(PerformanceScene? fromScene, PerformanceScene toScene, float t)
    {
        if (fromScene == null)
        {
            // No source scene, just apply target scaled by t
            foreach (var param in toScene.Snapshot.Parameters)
            {
                ApplyParameterValue(param);
            }
            return;
        }

        // Interpolate between scenes
        var interpolated = fromScene.Snapshot.Lerp(toScene.Snapshot, t);
        foreach (var param in interpolated.Parameters)
        {
            ApplyParameterValue(param);
        }
    }

    #endregion

    #region Parameter Mapping

    /// <summary>
    /// Maps a parameter to MIDI CC control.
    /// </summary>
    /// <param name="targetId">Target object ID.</param>
    /// <param name="propertyName">Property name.</param>
    /// <param name="cc">MIDI CC number.</param>
    /// <param name="channel">MIDI channel (-1 for omni).</param>
    /// <param name="minValue">Minimum value.</param>
    /// <param name="maxValue">Maximum value.</param>
    /// <param name="curve">Mapping curve.</param>
    /// <returns>The created mapping.</returns>
    public PerformanceParameterMapping MapParameter(
        string targetId,
        string propertyName,
        int cc,
        int channel = -1,
        float minValue = 0f,
        float maxValue = 1f,
        PerformanceCurve curve = PerformanceCurve.Linear)
    {
        if (string.IsNullOrWhiteSpace(targetId))
            throw new ArgumentNullException(nameof(targetId));
        if (string.IsNullOrWhiteSpace(propertyName))
            throw new ArgumentNullException(nameof(propertyName));

        var mapping = new PerformanceParameterMapping
        {
            Name = $"{targetId}.{propertyName}",
            TargetId = targetId,
            PropertyName = propertyName,
            MidiCC = Math.Clamp(cc, 0, 127),
            MidiChannel = Math.Clamp(channel, -1, 15),
            MinValue = minValue,
            MaxValue = maxValue,
            Curve = curve
        };

        lock (_lock)
        {
            // Resolve target
            if (_targetRegistry.TryGetValue(targetId, out var target))
            {
                mapping.TargetObject = target;
            }

            _parameterMappings.Add(mapping);
        }

        return mapping;
    }

    /// <summary>
    /// Adds a parameter mapping.
    /// </summary>
    public void AddMapping(PerformanceParameterMapping mapping)
    {
        if (mapping == null)
            throw new ArgumentNullException(nameof(mapping));

        lock (_lock)
        {
            if (!string.IsNullOrEmpty(mapping.TargetId) &&
                _targetRegistry.TryGetValue(mapping.TargetId, out var target))
            {
                mapping.TargetObject = target;
            }

            _parameterMappings.Add(mapping);
        }
    }

    /// <summary>
    /// Removes a parameter mapping.
    /// </summary>
    public bool RemoveMapping(PerformanceParameterMapping mapping)
    {
        if (mapping == null)
            return false;

        lock (_lock)
        {
            return _parameterMappings.Remove(mapping);
        }
    }

    /// <summary>
    /// Removes all mappings for a specific CC number.
    /// </summary>
    public int RemoveMappingsForCC(int cc)
    {
        lock (_lock)
        {
            return _parameterMappings.RemoveAll(m => m.MidiCC == cc);
        }
    }

    /// <summary>
    /// Clears all parameter mappings.
    /// </summary>
    public void ClearMappings()
    {
        lock (_lock)
        {
            _parameterMappings.Clear();
        }
    }

    #endregion

    #region MIDI Learn

    /// <summary>
    /// Starts MIDI learn mode for a parameter mapping.
    /// </summary>
    /// <param name="targetId">Target object ID.</param>
    /// <param name="propertyName">Property name.</param>
    /// <param name="minValue">Minimum value.</param>
    /// <param name="maxValue">Maximum value.</param>
    public void StartMidiLearn(string targetId, string propertyName, float minValue = 0f, float maxValue = 1f)
    {
        if (string.IsNullOrWhiteSpace(targetId))
            throw new ArgumentNullException(nameof(targetId));
        if (string.IsNullOrWhiteSpace(propertyName))
            throw new ArgumentNullException(nameof(propertyName));

        lock (_lock)
        {
            _midiLearnEnabled = true;
            _midiLearnTarget = null;
            _midiLearnPropertyName = string.Empty;
            _midiLearnScene = null;

            // Store for later
            _targetRegistry.TryGetValue(targetId, out _midiLearnTarget);
            _midiLearnPropertyName = propertyName;
        }
    }

    /// <summary>
    /// Starts MIDI learn mode for a scene trigger.
    /// </summary>
    /// <param name="scene">The scene to learn a trigger for.</param>
    public void StartMidiLearnForScene(PerformanceScene scene)
    {
        if (scene == null)
            throw new ArgumentNullException(nameof(scene));

        lock (_lock)
        {
            _midiLearnEnabled = true;
            _midiLearnTarget = null;
            _midiLearnPropertyName = string.Empty;
            _midiLearnScene = scene;
        }
    }

    /// <summary>
    /// Stops MIDI learn mode without creating a mapping.
    /// </summary>
    public void StopMidiLearn()
    {
        lock (_lock)
        {
            _midiLearnEnabled = false;
            _midiLearnTarget = null;
            _midiLearnPropertyName = string.Empty;
            _midiLearnScene = null;
        }
    }

    #endregion

    #region MIDI Processing

    /// <summary>
    /// Processes a raw MIDI message.
    /// </summary>
    /// <param name="message">Raw MIDI bytes.</param>
    /// <returns>True if the message was handled.</returns>
    public bool ProcessMidiMessage(byte[] message)
    {
        if (message == null || message.Length < 1)
            return false;

        int status = message[0];
        int messageType = status & 0xF0;
        int channel = status & 0x0F;

        // Note On
        if (messageType == 0x90 && message.Length >= 3)
        {
            int note = message[1] & 0x7F;
            int velocity = message[2] & 0x7F;
            return ProcessNoteOn(channel, note, velocity);
        }

        // Control Change
        if (messageType == 0xB0 && message.Length >= 3)
        {
            int cc = message[1] & 0x7F;
            int value = message[2] & 0x7F;
            return ProcessCC(channel, cc, value);
        }

        // Program Change
        if (messageType == 0xC0 && message.Length >= 2)
        {
            int program = message[1] & 0x7F;
            return ProcessProgramChange(channel, program);
        }

        return false;
    }

    /// <summary>
    /// Processes a MIDI CC message.
    /// </summary>
    public bool ProcessCC(int channel, int cc, int value)
    {
        channel = Math.Clamp(channel, 0, 15);
        cc = Math.Clamp(cc, 0, 127);
        value = Math.Clamp(value, 0, 127);

        lock (_lock)
        {
            // MIDI learn mode for parameter
            if (_midiLearnEnabled && _midiLearnTarget != null)
            {
                var mapping = new PerformanceParameterMapping
                {
                    Name = _midiLearnPropertyName,
                    TargetObject = _midiLearnTarget,
                    PropertyName = _midiLearnPropertyName,
                    MidiCC = cc,
                    MidiChannel = channel
                };

                // Find target ID
                foreach (var kvp in _targetRegistry)
                {
                    if (ReferenceEquals(kvp.Value, _midiLearnTarget))
                    {
                        mapping.TargetId = kvp.Key;
                        break;
                    }
                }

                _parameterMappings.Add(mapping);
                _midiLearnEnabled = false;
                _midiLearnTarget = null;
                _midiLearnPropertyName = string.Empty;

                MidiLearned?.Invoke(this, new PerformanceMidiLearnEventArgs(mapping, cc, channel));
                return true;
            }

            // Check for scene CC triggers
            foreach (var scene in _scenes)
            {
                if (!scene.Enabled)
                    continue;

                foreach (var trigger in scene.Triggers)
                {
                    if (!trigger.Enabled)
                        continue;

                    if (trigger.Type == SceneTriggerType.MidiCC &&
                        trigger.MidiValue == cc &&
                        (trigger.MidiChannel == -1 || trigger.MidiChannel == channel) &&
                        value >= 64) // Trigger on value >= 64
                    {
                        CrossfadeTo(scene);
                        return true;
                    }
                }
            }

            // Process parameter mappings
            bool handled = false;
            foreach (var mapping in _parameterMappings)
            {
                if (!mapping.Enabled)
                    continue;

                if (mapping.MidiCC == cc &&
                    (mapping.MidiChannel == -1 || mapping.MidiChannel == channel))
                {
                    float normalizedValue = value / 127f;
                    float scaledValue = mapping.ScaleValue(normalizedValue);

                    ApplyMappedValue(mapping, scaledValue);
                    mapping.CurrentValue = scaledValue;

                    ParameterChanged?.Invoke(this, new ParameterChangedEventArgs(mapping, scaledValue, value));
                    handled = true;
                }
            }

            return handled;
        }
    }

    /// <summary>
    /// Processes a MIDI note on message.
    /// </summary>
    public bool ProcessNoteOn(int channel, int note, int velocity)
    {
        if (velocity == 0)
            return false; // Note off

        channel = Math.Clamp(channel, 0, 15);
        note = Math.Clamp(note, 0, 127);

        lock (_lock)
        {
            // MIDI learn mode for scene trigger
            if (_midiLearnEnabled && _midiLearnScene != null)
            {
                _midiLearnScene.Triggers.Add(SceneTrigger.MidiNoteTrigger(note, channel));
                var scene = _midiLearnScene;

                _midiLearnEnabled = false;
                _midiLearnScene = null;

                MidiLearned?.Invoke(this, new PerformanceMidiLearnEventArgs(scene, note, channel));
                return true;
            }

            // Check for scene note triggers
            foreach (var scene in _scenes)
            {
                if (!scene.Enabled)
                    continue;

                foreach (var trigger in scene.Triggers)
                {
                    if (!trigger.Enabled)
                        continue;

                    if (trigger.Type == SceneTriggerType.MidiNote &&
                        trigger.MidiValue == note &&
                        (trigger.MidiChannel == -1 || trigger.MidiChannel == channel))
                    {
                        CrossfadeTo(scene);
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Processes a MIDI program change message.
    /// </summary>
    public bool ProcessProgramChange(int channel, int program)
    {
        channel = Math.Clamp(channel, 0, 15);
        program = Math.Clamp(program, 0, 127);

        lock (_lock)
        {
            foreach (var scene in _scenes)
            {
                if (!scene.Enabled)
                    continue;

                foreach (var trigger in scene.Triggers)
                {
                    if (!trigger.Enabled)
                        continue;

                    if (trigger.Type == SceneTriggerType.MidiProgramChange &&
                        trigger.MidiValue == program &&
                        (trigger.MidiChannel == -1 || trigger.MidiChannel == channel))
                    {
                        CrossfadeTo(scene);
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private void ApplyMappedValue(PerformanceParameterMapping mapping, float value)
    {
        if (mapping.TargetObject == null && !string.IsNullOrEmpty(mapping.TargetId))
        {
            _targetRegistry.TryGetValue(mapping.TargetId, out var target);
            mapping.TargetObject = target;
        }

        if (mapping.TargetObject == null)
            return;

        if (mapping.CachedProperty == null)
        {
            mapping.CachedProperty = mapping.TargetObject.GetType().GetProperty(mapping.PropertyName);
        }

        if (mapping.CachedProperty == null)
            return;

        try
        {
            var propertyType = mapping.CachedProperty.PropertyType;
            object convertedValue;

            if (propertyType == typeof(float))
                convertedValue = value;
            else if (propertyType == typeof(double))
                convertedValue = (double)value;
            else if (propertyType == typeof(int))
                convertedValue = (int)Math.Round(value);
            else if (propertyType == typeof(bool))
                convertedValue = value >= 0.5f;
            else
                convertedValue = Convert.ChangeType(value, propertyType);

            mapping.CachedProperty.SetValue(mapping.TargetObject, convertedValue);
        }
        catch
        {
            // Ignore errors
        }
    }

    #endregion

    #region Keyboard Processing

    /// <summary>
    /// Processes a keyboard event.
    /// </summary>
    /// <param name="key">Key pressed.</param>
    /// <param name="modifiers">Active modifiers.</param>
    /// <returns>True if a scene was triggered.</returns>
    public bool ProcessKeyboard(string key, KeyModifiers modifiers = KeyModifiers.None)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        lock (_lock)
        {
            foreach (var scene in _scenes)
            {
                if (!scene.Enabled)
                    continue;

                foreach (var trigger in scene.Triggers)
                {
                    if (!trigger.Enabled)
                        continue;

                    if (trigger.Type == SceneTriggerType.Keyboard &&
                        trigger.KeyboardKey.Equals(key, StringComparison.OrdinalIgnoreCase) &&
                        trigger.Modifiers == modifiers)
                    {
                        CrossfadeTo(scene);
                        return true;
                    }
                }
            }
        }

        return false;
    }

    #endregion

    #region Snapshots

    /// <summary>
    /// Saves a snapshot of all mapped parameters.
    /// </summary>
    /// <param name="name">Snapshot name.</param>
    /// <returns>The saved snapshot.</returns>
    public ParameterSnapshot SaveSnapshot(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentNullException(nameof(name));

        var snapshot = new ParameterSnapshot(name);

        lock (_lock)
        {
            foreach (var mapping in _parameterMappings)
            {
                if (mapping.TargetObject == null)
                    continue;

                if (mapping.CachedProperty == null)
                {
                    mapping.CachedProperty = mapping.TargetObject.GetType().GetProperty(mapping.PropertyName);
                }

                if (mapping.CachedProperty == null)
                    continue;

                try
                {
                    var value = mapping.CachedProperty.GetValue(mapping.TargetObject);
                    float floatValue = Convert.ToSingle(value);

                    snapshot.Parameters.Add(new ParameterValue
                    {
                        TargetId = mapping.TargetId,
                        PropertyName = mapping.PropertyName,
                        Value = floatValue,
                        TargetObject = mapping.TargetObject,
                        CachedProperty = mapping.CachedProperty
                    });
                }
                catch
                {
                    // Skip parameters that can't be read
                }
            }

            _snapshots.Add(snapshot);
        }

        SnapshotSaved?.Invoke(this, snapshot);
        return snapshot;
    }

    /// <summary>
    /// Saves the current state to the active scene's snapshot.
    /// </summary>
    public void SaveToCurrentScene()
    {
        lock (_lock)
        {
            if (_currentScene == null)
                return;

            _currentScene.Snapshot.Parameters.Clear();

            foreach (var mapping in _parameterMappings)
            {
                if (mapping.TargetObject == null)
                    continue;

                if (mapping.CachedProperty == null)
                {
                    mapping.CachedProperty = mapping.TargetObject.GetType().GetProperty(mapping.PropertyName);
                }

                if (mapping.CachedProperty == null)
                    continue;

                try
                {
                    var value = mapping.CachedProperty.GetValue(mapping.TargetObject);
                    float floatValue = Convert.ToSingle(value);

                    _currentScene.Snapshot.SetParameter(mapping.TargetId, mapping.PropertyName, floatValue);
                }
                catch
                {
                    // Skip
                }
            }

            _currentScene.Snapshot.Modified = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Loads a snapshot by applying its values.
    /// </summary>
    /// <param name="snapshot">The snapshot to load.</param>
    public void LoadSnapshot(ParameterSnapshot snapshot)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));

        lock (_lock)
        {
            foreach (var param in snapshot.Parameters)
            {
                ApplyParameterValue(param);
            }
        }

        SnapshotLoaded?.Invoke(this, snapshot);
    }

    /// <summary>
    /// Loads a snapshot by name.
    /// </summary>
    public bool LoadSnapshot(string name)
    {
        ParameterSnapshot? snapshot;
        lock (_lock)
        {
            snapshot = _snapshots.Find(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        if (snapshot != null)
        {
            LoadSnapshot(snapshot);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets a snapshot by name.
    /// </summary>
    public ParameterSnapshot? GetSnapshot(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        lock (_lock)
        {
            return _snapshots.Find(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Deletes a snapshot.
    /// </summary>
    public bool DeleteSnapshot(ParameterSnapshot snapshot)
    {
        if (snapshot == null)
            return false;

        lock (_lock)
        {
            return _snapshots.Remove(snapshot);
        }
    }

    /// <summary>
    /// Clears all saved snapshots.
    /// </summary>
    public void ClearSnapshots()
    {
        lock (_lock)
        {
            _snapshots.Clear();
        }
    }

    #endregion

    #region Serialization

    /// <summary>
    /// Exports the performance mode configuration to JSON.
    /// </summary>
    public string ExportToJson()
    {
        var config = new PerformanceModeConfig();

        lock (_lock)
        {
            config.CrossfadeTime = CrossfadeTime;
            config.Scenes = _scenes.Select(s => s.Clone()).ToList();
            config.ParameterMappings = _parameterMappings.Select(m => m.Clone()).ToList();
            config.Snapshots = _snapshots.Select(s => s.Clone()).ToList();
            config.CurrentSceneIndex = _currentScene?.Index ?? -1;
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Serialize(config, options);
    }

    /// <summary>
    /// Imports performance mode configuration from JSON.
    /// </summary>
    public void ImportFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentNullException(nameof(json));

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var config = JsonSerializer.Deserialize<PerformanceModeConfig>(json, options)
                     ?? throw new JsonException("Failed to deserialize PerformanceModeConfig.");

        lock (_lock)
        {
            CrossfadeTime = config.CrossfadeTime;
            _scenes.Clear();
            _scenes.AddRange(config.Scenes);
            _parameterMappings.Clear();
            _parameterMappings.AddRange(config.ParameterMappings);
            _snapshots.Clear();
            _snapshots.AddRange(config.Snapshots);

            // Rebind targets
            RebindMappingsInternal();

            // Restore current scene
            if (config.CurrentSceneIndex >= 0 && config.CurrentSceneIndex < _scenes.Count)
            {
                _currentScene = _scenes[config.CurrentSceneIndex];
            }
        }
    }

    /// <summary>
    /// Saves the configuration to a file.
    /// </summary>
    public void SaveToFile(string path)
    {
        var json = ExportToJson();
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Loads configuration from a file.
    /// </summary>
    public void LoadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Configuration file not found.", path);

        var json = File.ReadAllText(path);
        ImportFromJson(json);
    }

    #endregion

    #region Dispose

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _crossfadeCts?.Cancel();
        _crossfadeCts?.Dispose();

        lock (_lock)
        {
            _scenes.Clear();
            _parameterMappings.Clear();
            _snapshots.Clear();
            _targetRegistry.Clear();
            _currentScene = null;
            _nextScene = null;
        }

        GC.SuppressFinalize(this);
    }

    ~PerformanceMode()
    {
        Dispose();
    }

    #endregion
}

#region Configuration Class

/// <summary>
/// Configuration class for JSON serialization.
/// </summary>
internal class PerformanceModeConfig
{
    [JsonPropertyName("crossfadeTime")]
    public int CrossfadeTime { get; set; } = 500;

    [JsonPropertyName("scenes")]
    public List<PerformanceScene> Scenes { get; set; } = new();

    [JsonPropertyName("parameterMappings")]
    public List<PerformanceParameterMapping> ParameterMappings { get; set; } = new();

    [JsonPropertyName("snapshots")]
    public List<ParameterSnapshot> Snapshots { get; set; } = new();

    [JsonPropertyName("currentSceneIndex")]
    public int CurrentSceneIndex { get; set; } = -1;
}

#endregion
