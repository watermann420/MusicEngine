// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace MusicEngine.Core;


/// <summary>
/// Curve type for MIDI CC value scaling.
/// </summary>
public enum MidiMappingCurve
{
    /// <summary>Linear scaling (default).</summary>
    Linear,

    /// <summary>Exponential scaling - slower start, faster end.</summary>
    Exponential,

    /// <summary>Logarithmic scaling - faster start, slower end.</summary>
    Logarithmic
}

/// <summary>
/// Represents a mapping between a MIDI CC message and a target parameter.
/// </summary>
public class MidiMapping
{
    /// <summary>
    /// The MIDI CC number (0-127).
    /// </summary>
    public int CcNumber { get; set; }

    /// <summary>
    /// The MIDI channel (0-15), or -1 for all channels (omni).
    /// </summary>
    public int Channel { get; set; } = -1;

    /// <summary>
    /// The target object to control.
    /// </summary>
    [JsonIgnore]
    public object? TargetObject { get; set; }

    /// <summary>
    /// Identifier for the target object (for serialization).
    /// </summary>
    public string TargetId { get; set; } = "";

    /// <summary>
    /// The name of the property to control on the target object.
    /// </summary>
    public string PropertyName { get; set; } = "";

    /// <summary>
    /// The minimum value for scaling (maps to CC value 0).
    /// </summary>
    public float MinValue { get; set; } = 0f;

    /// <summary>
    /// The maximum value for scaling (maps to CC value 127).
    /// </summary>
    public float MaxValue { get; set; } = 1f;

    /// <summary>
    /// The curve type for value scaling.
    /// </summary>
    public MidiMappingCurve Curve { get; set; } = MidiMappingCurve.Linear;

    /// <summary>
    /// Whether this mapping is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Cached property info for performance.
    /// </summary>
    [JsonIgnore]
    public PropertyInfo? CachedProperty { get; set; }

    /// <summary>
    /// Current smoothed value for interpolation.
    /// </summary>
    [JsonIgnore]
    public float CurrentValue { get; set; }

    /// <summary>
    /// Creates a new MIDI mapping.
    /// </summary>
    public MidiMapping() { }

    /// <summary>
    /// Creates a new MIDI mapping with the specified parameters.
    /// </summary>
    public MidiMapping(int ccNumber, int channel, object target, string propertyName, float minValue = 0f, float maxValue = 1f)
    {
        CcNumber = Math.Clamp(ccNumber, 0, 127);
        Channel = Math.Clamp(channel, -1, 15);
        TargetObject = target;
        PropertyName = propertyName;
        MinValue = minValue;
        MaxValue = maxValue;
    }

    /// <summary>
    /// Scales a MIDI CC value (0-127) to the target range using the configured curve.
    /// </summary>
    /// <param name="ccValue">The MIDI CC value (0-127).</param>
    /// <returns>The scaled value.</returns>
    public float ScaleValue(int ccValue)
    {
        // Normalize to 0-1
        float normalized = ccValue / 127f;

        // Apply curve
        float curved = Curve switch
        {
            MidiMappingCurve.Linear => normalized,
            MidiMappingCurve.Exponential => (float)(normalized > 0 ? Math.Pow(normalized, 3) : 0),
            MidiMappingCurve.Logarithmic => (float)(normalized > 0 ? Math.Pow(normalized, 1.0 / 3.0) : 0),
            _ => normalized
        };

        // Scale to target range
        return MinValue + (MaxValue - MinValue) * curved;
    }

    /// <summary>
    /// Applies a MIDI CC value to the target property.
    /// </summary>
    /// <param name="ccValue">The MIDI CC value (0-127).</param>
    /// <returns>True if the value was applied successfully.</returns>
    public bool ApplyValue(int ccValue)
    {
        if (!Enabled || TargetObject == null) return false;

        // Get or cache the property
        if (CachedProperty == null)
        {
            CachedProperty = TargetObject.GetType().GetProperty(PropertyName);
            if (CachedProperty == null) return false;
        }

        float scaledValue = ScaleValue(ccValue);
        CurrentValue = scaledValue;

        try
        {
            // Handle different property types
            var propertyType = CachedProperty.PropertyType;

            if (propertyType == typeof(float))
            {
                CachedProperty.SetValue(TargetObject, scaledValue);
            }
            else if (propertyType == typeof(double))
            {
                CachedProperty.SetValue(TargetObject, (double)scaledValue);
            }
            else if (propertyType == typeof(int))
            {
                CachedProperty.SetValue(TargetObject, (int)Math.Round(scaledValue));
            }
            else if (propertyType == typeof(bool))
            {
                CachedProperty.SetValue(TargetObject, ccValue >= 64);
            }
            else
            {
                // Try generic conversion
                CachedProperty.SetValue(TargetObject, Convert.ChangeType(scaledValue, propertyType));
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Creates a clone of this mapping.
    /// </summary>
    public MidiMapping Clone()
    {
        return new MidiMapping
        {
            CcNumber = CcNumber,
            Channel = Channel,
            TargetObject = TargetObject,
            TargetId = TargetId,
            PropertyName = PropertyName,
            MinValue = MinValue,
            MaxValue = MaxValue,
            Curve = Curve,
            Enabled = Enabled,
            CachedProperty = CachedProperty,
            CurrentValue = CurrentValue
        };
    }
}

/// <summary>
/// Event arguments for MIDI learn events.
/// </summary>
public class MidiLearnEventArgs : EventArgs
{
    /// <summary>The mapping that was created or modified.</summary>
    public MidiMapping Mapping { get; }

    /// <summary>The CC number that was learned.</summary>
    public int CcNumber { get; }

    /// <summary>The channel that was learned.</summary>
    public int Channel { get; }

    public MidiLearnEventArgs(MidiMapping mapping)
    {
        Mapping = mapping;
        CcNumber = mapping.CcNumber;
        Channel = mapping.Channel;
    }

    public MidiLearnEventArgs(MidiMapping mapping, int ccNumber, int channel)
    {
        Mapping = mapping;
        CcNumber = ccNumber;
        Channel = channel;
    }
}

/// <summary>
/// Event arguments for MIDI CC value changes.
/// </summary>
public class MidiCcEventArgs : EventArgs
{
    /// <summary>The MIDI channel (0-15).</summary>
    public int Channel { get; }

    /// <summary>The CC number (0-127).</summary>
    public int CcNumber { get; }

    /// <summary>The CC value (0-127).</summary>
    public int Value { get; }

    /// <summary>The scaled value after mapping.</summary>
    public float ScaledValue { get; }

    /// <summary>The mapping that processed this CC.</summary>
    public MidiMapping? Mapping { get; }

    public MidiCcEventArgs(int channel, int ccNumber, int value, float scaledValue = 0f, MidiMapping? mapping = null)
    {
        Channel = channel;
        CcNumber = ccNumber;
        Value = value;
        ScaledValue = scaledValue;
        Mapping = mapping;
    }
}

/// <summary>
/// MIDI Learn system for creating and managing MIDI CC to parameter mappings.
/// Thread-safe implementation for real-time use.
/// </summary>
public class MidiLearn : IDisposable
{
    private readonly object _lock = new();
    private readonly List<MidiMapping> _mappings = new();
    private readonly Dictionary<string, object> _targetRegistry = new();

    private bool _isLearning;
    private object? _learningTarget;
    private string _learningPropertyName = "";
    private float _learningMinValue;
    private float _learningMaxValue;
    private MidiMappingCurve _learningCurve = MidiMappingCurve.Linear;
    private bool _disposed;

    /// <summary>
    /// Fired when a mapping is learned (created during learn mode).
    /// </summary>
    public event EventHandler<MidiLearnEventArgs>? MappingLearned;

    /// <summary>
    /// Fired when a mapping is added.
    /// </summary>
    public event EventHandler<MidiLearnEventArgs>? MappingAdded;

    /// <summary>
    /// Fired when a mapping is removed.
    /// </summary>
    public event EventHandler<MidiLearnEventArgs>? MappingRemoved;

    /// <summary>
    /// Fired when a CC value is processed through a mapping.
    /// </summary>
    public event EventHandler<MidiCcEventArgs>? CcProcessed;

    /// <summary>
    /// Gets whether the system is currently in learn mode.
    /// </summary>
    public bool IsLearning
    {
        get
        {
            lock (_lock)
            {
                return _isLearning;
            }
        }
    }

    /// <summary>
    /// Gets the current learning target object.
    /// </summary>
    public object? CurrentLearningTarget
    {
        get
        {
            lock (_lock)
            {
                return _learningTarget;
            }
        }
    }

    /// <summary>
    /// Gets the current learning property name.
    /// </summary>
    public string CurrentLearningProperty
    {
        get
        {
            lock (_lock)
            {
                return _learningPropertyName;
            }
        }
    }

    /// <summary>
    /// Creates a new MIDI Learn instance.
    /// </summary>
    public MidiLearn() { }

    /// <summary>
    /// Registers a target object with an ID for serialization purposes.
    /// </summary>
    /// <param name="id">The unique identifier for the target.</param>
    /// <param name="target">The target object.</param>
    public void RegisterTarget(string id, object target)
    {
        lock (_lock)
        {
            _targetRegistry[id] = target;
        }
    }

    /// <summary>
    /// Unregisters a target object.
    /// </summary>
    /// <param name="id">The unique identifier to unregister.</param>
    public void UnregisterTarget(string id)
    {
        lock (_lock)
        {
            _targetRegistry.Remove(id);
        }
    }

    /// <summary>
    /// Starts learning mode for the specified target and property.
    /// The next CC message received will create a mapping.
    /// </summary>
    /// <param name="target">The target object to control.</param>
    /// <param name="property">The property name to control.</param>
    /// <param name="minValue">The minimum scaled value.</param>
    /// <param name="maxValue">The maximum scaled value.</param>
    /// <param name="curve">The curve type for scaling.</param>
    public void StartLearning(object target, string property, float minValue = 0f, float maxValue = 1f, MidiMappingCurve curve = MidiMappingCurve.Linear)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (string.IsNullOrEmpty(property)) throw new ArgumentException("Property name cannot be null or empty.", nameof(property));

        // Validate property exists
        var prop = target.GetType().GetProperty(property);
        if (prop == null)
        {
            throw new ArgumentException($"Property '{property}' not found on target type '{target.GetType().Name}'.", nameof(property));
        }

        lock (_lock)
        {
            _isLearning = true;
            _learningTarget = target;
            _learningPropertyName = property;
            _learningMinValue = minValue;
            _learningMaxValue = maxValue;
            _learningCurve = curve;
        }
    }

    /// <summary>
    /// Stops learning mode without creating a mapping.
    /// </summary>
    public void StopLearning()
    {
        lock (_lock)
        {
            _isLearning = false;
            _learningTarget = null;
            _learningPropertyName = "";
        }
    }

    /// <summary>
    /// Processes a raw MIDI message. If in learn mode and a CC is detected, creates a mapping.
    /// </summary>
    /// <param name="message">The raw MIDI message (1-3 bytes).</param>
    /// <returns>True if a mapping was created or a CC was processed.</returns>
    public bool ProcessMidiMessage(byte[] message)
    {
        if (message == null || message.Length < 1) return false;

        int status = message[0];
        int messageType = status & 0xF0;
        int channel = status & 0x0F;

        // Check for Control Change (0xB0-0xBF)
        if (messageType == 0xB0 && message.Length >= 3)
        {
            int ccNumber = message[1] & 0x7F;
            int ccValue = message[2] & 0x7F;

            return ProcessCC(channel, ccNumber, ccValue);
        }

        return false;
    }

    /// <summary>
    /// Processes a MIDI CC message. If in learn mode, creates a mapping.
    /// Otherwise, applies the value to any matching mappings.
    /// </summary>
    /// <param name="channel">The MIDI channel (0-15).</param>
    /// <param name="cc">The CC number (0-127).</param>
    /// <param name="value">The CC value (0-127).</param>
    /// <returns>True if a mapping was created or applied.</returns>
    public bool ProcessCC(int channel, int cc, int value)
    {
        channel = Math.Clamp(channel, 0, 15);
        cc = Math.Clamp(cc, 0, 127);
        value = Math.Clamp(value, 0, 127);

        lock (_lock)
        {
            // Learning mode - create a new mapping
            if (_isLearning && _learningTarget != null)
            {
                var mapping = new MidiMapping
                {
                    CcNumber = cc,
                    Channel = channel,
                    TargetObject = _learningTarget,
                    PropertyName = _learningPropertyName,
                    MinValue = _learningMinValue,
                    MaxValue = _learningMaxValue,
                    Curve = _learningCurve,
                    Enabled = true
                };

                // Find target ID if registered
                foreach (var kvp in _targetRegistry)
                {
                    if (ReferenceEquals(kvp.Value, _learningTarget))
                    {
                        mapping.TargetId = kvp.Key;
                        break;
                    }
                }

                // Remove any existing mapping for same target/property combination
                _mappings.RemoveAll(m =>
                    ReferenceEquals(m.TargetObject, _learningTarget) &&
                    m.PropertyName == _learningPropertyName);

                _mappings.Add(mapping);

                // Exit learning mode
                _isLearning = false;
                _learningTarget = null;
                _learningPropertyName = "";

                // Apply the initial value
                mapping.ApplyValue(value);

                // Fire events
                MappingLearned?.Invoke(this, new MidiLearnEventArgs(mapping, cc, channel));
                MappingAdded?.Invoke(this, new MidiLearnEventArgs(mapping, cc, channel));

                return true;
            }

            // Normal mode - apply to matching mappings
            bool applied = false;
            foreach (var mapping in _mappings)
            {
                if (!mapping.Enabled) continue;

                // Check if CC and channel match
                bool ccMatch = mapping.CcNumber == cc;
                bool channelMatch = mapping.Channel == -1 || mapping.Channel == channel;

                if (ccMatch && channelMatch)
                {
                    if (mapping.ApplyValue(value))
                    {
                        applied = true;

                        CcProcessed?.Invoke(this, new MidiCcEventArgs(
                            channel, cc, value,
                            mapping.ScaleValue(value),
                            mapping
                        ));
                    }
                }
            }

            return applied;
        }
    }

    /// <summary>
    /// Adds a mapping manually (not through learn mode).
    /// </summary>
    /// <param name="mapping">The mapping to add.</param>
    public void AddMapping(MidiMapping mapping)
    {
        if (mapping == null) throw new ArgumentNullException(nameof(mapping));

        lock (_lock)
        {
            _mappings.Add(mapping);
            MappingAdded?.Invoke(this, new MidiLearnEventArgs(mapping, mapping.CcNumber, mapping.Channel));
        }
    }

    /// <summary>
    /// Adds a mapping with the specified parameters.
    /// </summary>
    public MidiMapping AddMapping(int ccNumber, int channel, object target, string propertyName,
        float minValue = 0f, float maxValue = 1f, MidiMappingCurve curve = MidiMappingCurve.Linear)
    {
        var mapping = new MidiMapping(ccNumber, channel, target, propertyName, minValue, maxValue)
        {
            Curve = curve
        };

        // Find target ID if registered
        lock (_lock)
        {
            foreach (var kvp in _targetRegistry)
            {
                if (ReferenceEquals(kvp.Value, target))
                {
                    mapping.TargetId = kvp.Key;
                    break;
                }
            }
        }

        AddMapping(mapping);
        return mapping;
    }

    /// <summary>
    /// Removes a mapping.
    /// </summary>
    /// <param name="mapping">The mapping to remove.</param>
    /// <returns>True if the mapping was found and removed.</returns>
    public bool RemoveMapping(MidiMapping mapping)
    {
        if (mapping == null) return false;

        lock (_lock)
        {
            bool removed = _mappings.Remove(mapping);
            if (removed)
            {
                MappingRemoved?.Invoke(this, new MidiLearnEventArgs(mapping, mapping.CcNumber, mapping.Channel));
            }
            return removed;
        }
    }

    /// <summary>
    /// Removes all mappings for a specific CC number.
    /// </summary>
    /// <param name="ccNumber">The CC number.</param>
    /// <param name="channel">The channel (-1 for all channels).</param>
    /// <returns>The number of mappings removed.</returns>
    public int RemoveMappings(int ccNumber, int channel = -1)
    {
        lock (_lock)
        {
            var toRemove = _mappings.FindAll(m =>
                m.CcNumber == ccNumber &&
                (channel == -1 || m.Channel == -1 || m.Channel == channel));

            foreach (var mapping in toRemove)
            {
                _mappings.Remove(mapping);
                MappingRemoved?.Invoke(this, new MidiLearnEventArgs(mapping, mapping.CcNumber, mapping.Channel));
            }

            return toRemove.Count;
        }
    }

    /// <summary>
    /// Removes all mappings for a specific target object.
    /// </summary>
    /// <param name="target">The target object.</param>
    /// <returns>The number of mappings removed.</returns>
    public int RemoveMappingsForTarget(object target)
    {
        if (target == null) return 0;

        lock (_lock)
        {
            var toRemove = _mappings.FindAll(m => ReferenceEquals(m.TargetObject, target));

            foreach (var mapping in toRemove)
            {
                _mappings.Remove(mapping);
                MappingRemoved?.Invoke(this, new MidiLearnEventArgs(mapping, mapping.CcNumber, mapping.Channel));
            }

            return toRemove.Count;
        }
    }

    /// <summary>
    /// Gets all current mappings.
    /// </summary>
    /// <returns>A read-only list of all mappings.</returns>
    public IReadOnlyList<MidiMapping> GetMappings()
    {
        lock (_lock)
        {
            return _mappings.ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Gets mappings for a specific CC number.
    /// </summary>
    /// <param name="ccNumber">The CC number.</param>
    /// <returns>A list of mappings for the specified CC.</returns>
    public List<MidiMapping> GetMappingsForCc(int ccNumber)
    {
        lock (_lock)
        {
            return _mappings.FindAll(m => m.CcNumber == ccNumber);
        }
    }

    /// <summary>
    /// Gets mappings for a specific target object.
    /// </summary>
    /// <param name="target">The target object.</param>
    /// <returns>A list of mappings for the specified target.</returns>
    public List<MidiMapping> GetMappingsForTarget(object target)
    {
        if (target == null) return new List<MidiMapping>();

        lock (_lock)
        {
            return _mappings.FindAll(m => ReferenceEquals(m.TargetObject, target));
        }
    }

    /// <summary>
    /// Clears all mappings.
    /// </summary>
    public void ClearMappings()
    {
        lock (_lock)
        {
            _mappings.Clear();
        }
    }

    /// <summary>
    /// Saves all mappings to a JSON file.
    /// </summary>
    /// <param name="path">The file path to save to.</param>
    public void SaveMappings(string path)
    {
        List<MidiMappingData> data;

        lock (_lock)
        {
            data = _mappings.Select(m => new MidiMappingData
            {
                CcNumber = m.CcNumber,
                Channel = m.Channel,
                TargetId = m.TargetId,
                PropertyName = m.PropertyName,
                MinValue = m.MinValue,
                MaxValue = m.MaxValue,
                Curve = m.Curve.ToString(),
                Enabled = m.Enabled
            }).ToList();
        }

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Loads mappings from a JSON file.
    /// Note: Targets must be registered before loading for mappings to be functional.
    /// </summary>
    /// <param name="path">The file path to load from.</param>
    /// <returns>The number of mappings loaded.</returns>
    public int LoadMappings(string path)
    {
        if (!File.Exists(path)) return 0;

        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var data = JsonSerializer.Deserialize<List<MidiMappingData>>(json, options);

        if (data == null) return 0;

        lock (_lock)
        {
            int count = 0;
            foreach (var item in data)
            {
                var mapping = new MidiMapping
                {
                    CcNumber = item.CcNumber,
                    Channel = item.Channel,
                    TargetId = item.TargetId,
                    PropertyName = item.PropertyName,
                    MinValue = item.MinValue,
                    MaxValue = item.MaxValue,
                    Curve = Enum.TryParse<MidiMappingCurve>(item.Curve, out var curve) ? curve : MidiMappingCurve.Linear,
                    Enabled = item.Enabled
                };

                // Try to resolve target from registry
                if (!string.IsNullOrEmpty(item.TargetId) && _targetRegistry.TryGetValue(item.TargetId, out var target))
                {
                    mapping.TargetObject = target;
                }

                _mappings.Add(mapping);
                count++;
            }

            return count;
        }
    }

    /// <summary>
    /// Rebinds all mappings to their registered targets.
    /// Call this after registering targets to connect loaded mappings.
    /// </summary>
    public void RebindTargets()
    {
        lock (_lock)
        {
            foreach (var mapping in _mappings)
            {
                if (!string.IsNullOrEmpty(mapping.TargetId) && _targetRegistry.TryGetValue(mapping.TargetId, out var target))
                {
                    mapping.TargetObject = target;
                    mapping.CachedProperty = null; // Clear cached property to force refresh
                }
            }
        }
    }

    /// <summary>
    /// Enables or disables all mappings.
    /// </summary>
    /// <param name="enabled">Whether mappings should be enabled.</param>
    public void SetAllMappingsEnabled(bool enabled)
    {
        lock (_lock)
        {
            foreach (var mapping in _mappings)
            {
                mapping.Enabled = enabled;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lock)
        {
            _mappings.Clear();
            _targetRegistry.Clear();
            _isLearning = false;
            _learningTarget = null;
        }

        GC.SuppressFinalize(this);
    }

    ~MidiLearn()
    {
        Dispose();
    }

    /// <summary>
    /// Internal class for JSON serialization of mappings.
    /// </summary>
    private class MidiMappingData
    {
        public int CcNumber { get; set; }
        public int Channel { get; set; }
        public string TargetId { get; set; } = "";
        public string PropertyName { get; set; } = "";
        public float MinValue { get; set; }
        public float MaxValue { get; set; }
        public string Curve { get; set; } = "Linear";
        public bool Enabled { get; set; }
    }
}

/// <summary>
/// Common MIDI CC numbers for reference.
/// </summary>
public static class MidiCC
{
    public const int BankSelect = 0;
    public const int ModWheel = 1;
    public const int BreathController = 2;
    public const int FootController = 4;
    public const int PortamentoTime = 5;
    public const int DataEntry = 6;
    public const int Volume = 7;
    public const int Balance = 8;
    public const int Pan = 10;
    public const int Expression = 11;
    public const int EffectControl1 = 12;
    public const int EffectControl2 = 13;

    public const int SustainPedal = 64;
    public const int Portamento = 65;
    public const int Sostenuto = 66;
    public const int SoftPedal = 67;
    public const int LegatoFootswitch = 68;
    public const int Hold2 = 69;

    public const int SoundController1 = 70;  // Variation
    public const int SoundController2 = 71;  // Timbre/Resonance
    public const int SoundController3 = 72;  // Release Time
    public const int SoundController4 = 73;  // Attack Time
    public const int SoundController5 = 74;  // Brightness/Cutoff

    public const int ReverbLevel = 91;
    public const int TremoloLevel = 92;
    public const int ChorusLevel = 93;
    public const int DetuneLevel = 94;
    public const int PhaserLevel = 95;

    public const int AllSoundOff = 120;
    public const int ResetAllControllers = 121;
    public const int AllNotesOff = 123;
}
