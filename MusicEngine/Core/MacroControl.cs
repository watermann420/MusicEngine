// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace MusicEngine.Core;


/// <summary>
/// Defines how a parameter responds to macro changes.
/// </summary>
public enum MacroCurve
{
    /// <summary>Linear response (default).</summary>
    Linear,

    /// <summary>Exponential response - slower start, faster end.</summary>
    Exponential,

    /// <summary>Logarithmic response - faster start, slower end.</summary>
    Logarithmic,

    /// <summary>S-Curve response - slow at both ends, fast in middle.</summary>
    SCurve
}


/// <summary>
/// Event arguments for macro mapping events.
/// </summary>
public class MacroMappingEventArgs : EventArgs
{
    /// <summary>Gets the macro mapping.</summary>
    public MacroMapping Mapping { get; }

    /// <summary>Gets the macro control.</summary>
    public MacroControl? MacroControl { get; }

    /// <summary>
    /// Creates new macro mapping event arguments.
    /// </summary>
    /// <param name="mapping">The mapping.</param>
    /// <param name="macroControl">The macro control (optional).</param>
    public MacroMappingEventArgs(MacroMapping mapping, MacroControl? macroControl = null)
    {
        Mapping = mapping;
        MacroControl = macroControl;
    }
}


/// <summary>
/// Event arguments for macro value changes.
/// </summary>
public class MacroValueChangedEventArgs : EventArgs
{
    /// <summary>Gets the macro control.</summary>
    public MacroControl MacroControl { get; }

    /// <summary>Gets the old value.</summary>
    public float OldValue { get; }

    /// <summary>Gets the new value.</summary>
    public float NewValue { get; }

    /// <summary>
    /// Creates new macro value changed event arguments.
    /// </summary>
    /// <param name="macroControl">The macro control.</param>
    /// <param name="oldValue">The old value.</param>
    /// <param name="newValue">The new value.</param>
    public MacroValueChangedEventArgs(MacroControl macroControl, float oldValue, float newValue)
    {
        MacroControl = macroControl;
        OldValue = oldValue;
        NewValue = newValue;
    }
}


/// <summary>
/// Represents a mapping between a macro control and a target parameter.
/// </summary>
public class MacroMapping
{
    /// <summary>
    /// Unique identifier for this mapping.
    /// </summary>
    public string Id { get; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Identifier for the target parameter.
    /// </summary>
    public string TargetParameterId { get; set; } = "";

    /// <summary>
    /// The target object containing the property to control.
    /// </summary>
    [JsonIgnore]
    public object? TargetObject { get; set; }

    /// <summary>
    /// Identifier for the target object (for serialization).
    /// </summary>
    public string TargetObjectId { get; set; } = "";

    /// <summary>
    /// Name of the property to control on the target object.
    /// </summary>
    public string PropertyName { get; set; } = "";

    /// <summary>
    /// Display name for the mapping (for UI).
    /// </summary>
    public string DisplayName { get; set; } = "";

    /// <summary>
    /// Parameter value when macro is at minimum (0).
    /// </summary>
    public float MinValue { get; set; } = 0f;

    /// <summary>
    /// Parameter value when macro is at maximum (1).
    /// </summary>
    public float MaxValue { get; set; } = 1f;

    /// <summary>
    /// Curve type for value mapping.
    /// </summary>
    public MacroCurve Curve { get; set; } = MacroCurve.Linear;

    /// <summary>
    /// Whether this mapping is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether to invert the mapping (1 maps to MinValue, 0 maps to MaxValue).
    /// </summary>
    public bool Inverted { get; set; } = false;

    /// <summary>
    /// Cached property info for performance.
    /// </summary>
    [JsonIgnore]
    public PropertyInfo? CachedProperty { get; set; }

    /// <summary>
    /// Last value applied by this mapping.
    /// </summary>
    [JsonIgnore]
    public float LastAppliedValue { get; private set; }

    /// <summary>
    /// Creates a new macro mapping.
    /// </summary>
    public MacroMapping()
    {
    }

    /// <summary>
    /// Creates a new macro mapping with specified parameters.
    /// </summary>
    /// <param name="target">The target object.</param>
    /// <param name="propertyName">The property name.</param>
    /// <param name="minValue">Minimum value (at macro = 0).</param>
    /// <param name="maxValue">Maximum value (at macro = 1).</param>
    public MacroMapping(object target, string propertyName, float minValue = 0f, float maxValue = 1f)
    {
        TargetObject = target;
        PropertyName = propertyName;
        MinValue = minValue;
        MaxValue = maxValue;
        DisplayName = propertyName;
    }

    /// <summary>
    /// Calculates the mapped parameter value from a macro value (0-1).
    /// </summary>
    /// <param name="macroValue">The macro value (0-1).</param>
    /// <returns>The calculated parameter value.</returns>
    public float GetMappedValue(float macroValue)
    {
        macroValue = Math.Clamp(macroValue, 0f, 1f);

        // Apply inversion if needed
        if (Inverted)
        {
            macroValue = 1f - macroValue;
        }

        // Apply curve transformation
        float curved = Curve switch
        {
            MacroCurve.Linear => macroValue,
            MacroCurve.Exponential => ExponentialCurve(macroValue),
            MacroCurve.Logarithmic => LogarithmicCurve(macroValue),
            MacroCurve.SCurve => SCurve(macroValue),
            _ => macroValue
        };

        // Map to output range
        return MinValue + (MaxValue - MinValue) * curved;
    }

    /// <summary>
    /// Applies the macro value to the target property.
    /// </summary>
    /// <param name="macroValue">The macro value (0-1).</param>
    /// <returns>True if the value was applied successfully.</returns>
    public bool ApplyValue(float macroValue)
    {
        if (!Enabled || TargetObject == null || string.IsNullOrEmpty(PropertyName))
        {
            return false;
        }

        // Get or cache the property
        if (CachedProperty == null)
        {
            CachedProperty = TargetObject.GetType().GetProperty(PropertyName);
            if (CachedProperty == null || !CachedProperty.CanWrite)
            {
                return false;
            }
        }

        float mappedValue = GetMappedValue(macroValue);
        LastAppliedValue = mappedValue;

        try
        {
            var propertyType = CachedProperty.PropertyType;

            if (propertyType == typeof(float))
            {
                CachedProperty.SetValue(TargetObject, mappedValue);
            }
            else if (propertyType == typeof(double))
            {
                CachedProperty.SetValue(TargetObject, (double)mappedValue);
            }
            else if (propertyType == typeof(int))
            {
                CachedProperty.SetValue(TargetObject, (int)Math.Round(mappedValue));
            }
            else if (propertyType == typeof(bool))
            {
                CachedProperty.SetValue(TargetObject, mappedValue >= 0.5f);
            }
            else
            {
                CachedProperty.SetValue(TargetObject, Convert.ChangeType(mappedValue, propertyType));
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
    /// <returns>A new mapping with the same properties.</returns>
    public MacroMapping Clone()
    {
        return new MacroMapping
        {
            TargetParameterId = TargetParameterId,
            TargetObject = TargetObject,
            TargetObjectId = TargetObjectId,
            PropertyName = PropertyName,
            DisplayName = DisplayName,
            MinValue = MinValue,
            MaxValue = MaxValue,
            Curve = Curve,
            Enabled = Enabled,
            Inverted = Inverted
        };
    }

    private static float ExponentialCurve(float x)
    {
        // Attempt an exponential ease-in curve (x^3)
        return x * x * x;
    }

    private static float LogarithmicCurve(float x)
    {
        // Logarithmic curve (cube root approximation)
        if (x <= 0) return 0;
        return (float)Math.Pow(x, 1.0 / 3.0);
    }

    private static float SCurve(float x)
    {
        // Smooth S-curve using smoothstep function
        // 3x^2 - 2x^3
        return x * x * (3f - 2f * x);
    }
}


/// <summary>
/// A macro control that can control multiple parameters simultaneously.
/// Thread-safe implementation for real-time use.
/// </summary>
public class MacroControl
{
    private readonly object _lock = new();
    private readonly List<MacroMapping> _mappings = new();
    private float _value;

    /// <summary>
    /// Unique identifier for this macro control.
    /// </summary>
    public string Id { get; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Display name of the macro control.
    /// </summary>
    public string Name { get; set; } = "Macro";

    /// <summary>
    /// Index of the macro (typically 0-7 for an 8-macro bank).
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Gets or sets the macro value (0-1).
    /// Setting this value applies it to all mapped parameters.
    /// </summary>
    public float Value
    {
        get
        {
            lock (_lock)
            {
                return _value;
            }
        }
        set
        {
            float oldValue;
            lock (_lock)
            {
                oldValue = _value;
                _value = Math.Clamp(value, 0f, 1f);
            }

            ApplyToMappings();
            ValueChanged?.Invoke(this, new MacroValueChangedEventArgs(this, oldValue, _value));
        }
    }

    /// <summary>
    /// Default value for reset operations.
    /// </summary>
    public float DefaultValue { get; set; } = 0.5f;

    /// <summary>
    /// Color for UI display (hex format).
    /// </summary>
    public string Color { get; set; } = "#55AAFF";

    /// <summary>
    /// MIDI CC number for external control (-1 if not assigned).
    /// </summary>
    public int? MidiCC { get; set; }

    /// <summary>
    /// MIDI channel for external control (-1 for all channels, 0-15 for specific).
    /// </summary>
    public int? MidiChannel { get; set; }

    /// <summary>
    /// Whether the macro is currently being edited via MIDI Learn.
    /// </summary>
    public bool IsLearning { get; set; }

    /// <summary>
    /// Fired when the macro value changes.
    /// </summary>
    public event EventHandler<MacroValueChangedEventArgs>? ValueChanged;

    /// <summary>
    /// Fired when a mapping is added.
    /// </summary>
    public event EventHandler<MacroMappingEventArgs>? MappingAdded;

    /// <summary>
    /// Fired when a mapping is removed.
    /// </summary>
    public event EventHandler<MacroMappingEventArgs>? MappingRemoved;

    /// <summary>
    /// Creates a new macro control.
    /// </summary>
    public MacroControl()
    {
    }

    /// <summary>
    /// Creates a new macro control with specified name and index.
    /// </summary>
    /// <param name="name">Display name.</param>
    /// <param name="index">Macro index (0-7).</param>
    public MacroControl(string name, int index)
    {
        Name = name;
        Index = index;
    }

    /// <summary>
    /// Adds a mapping to control a parameter.
    /// </summary>
    /// <param name="target">The target object.</param>
    /// <param name="propertyName">The property name to control.</param>
    /// <param name="minValue">Value when macro is 0.</param>
    /// <param name="maxValue">Value when macro is 1.</param>
    /// <returns>The created mapping.</returns>
    public MacroMapping AddMapping(object target, string propertyName, float minValue = 0f, float maxValue = 1f)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (string.IsNullOrEmpty(propertyName))
        {
            throw new ArgumentException("Property name cannot be null or empty.", nameof(propertyName));
        }

        // Validate property exists
        var property = target.GetType().GetProperty(propertyName);
        if (property == null)
        {
            throw new ArgumentException($"Property '{propertyName}' not found on target type '{target.GetType().Name}'.", nameof(propertyName));
        }

        var mapping = new MacroMapping(target, propertyName, minValue, maxValue)
        {
            CachedProperty = property,
            DisplayName = $"{target.GetType().Name}.{propertyName}"
        };

        lock (_lock)
        {
            _mappings.Add(mapping);
        }

        // Apply current value to the new mapping
        mapping.ApplyValue(_value);

        MappingAdded?.Invoke(this, new MacroMappingEventArgs(mapping, this));

        return mapping;
    }

    /// <summary>
    /// Adds an existing mapping to this macro.
    /// </summary>
    /// <param name="mapping">The mapping to add.</param>
    public void AddMapping(MacroMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        lock (_lock)
        {
            _mappings.Add(mapping);
        }

        mapping.ApplyValue(_value);
        MappingAdded?.Invoke(this, new MacroMappingEventArgs(mapping, this));
    }

    /// <summary>
    /// Removes a mapping by its ID.
    /// </summary>
    /// <param name="mappingId">The mapping ID to remove.</param>
    /// <returns>True if the mapping was found and removed.</returns>
    public bool RemoveMapping(string mappingId)
    {
        MacroMapping? removed = null;

        lock (_lock)
        {
            for (int i = _mappings.Count - 1; i >= 0; i--)
            {
                if (_mappings[i].Id == mappingId)
                {
                    removed = _mappings[i];
                    _mappings.RemoveAt(i);
                    break;
                }
            }
        }

        if (removed != null)
        {
            MappingRemoved?.Invoke(this, new MacroMappingEventArgs(removed, this));
            return true;
        }

        return false;
    }

    /// <summary>
    /// Removes a mapping.
    /// </summary>
    /// <param name="mapping">The mapping to remove.</param>
    /// <returns>True if the mapping was found and removed.</returns>
    public bool RemoveMapping(MacroMapping mapping)
    {
        if (mapping == null) return false;
        return RemoveMapping(mapping.Id);
    }

    /// <summary>
    /// Gets all mappings for this macro.
    /// </summary>
    /// <returns>A read-only list of mappings.</returns>
    public IReadOnlyList<MacroMapping> GetMappings()
    {
        lock (_lock)
        {
            return _mappings.ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Gets the number of mappings.
    /// </summary>
    public int MappingCount
    {
        get
        {
            lock (_lock)
            {
                return _mappings.Count;
            }
        }
    }

    /// <summary>
    /// Applies the current macro value to all mapped parameters.
    /// </summary>
    public void ApplyToMappings()
    {
        List<MacroMapping> mappings;

        lock (_lock)
        {
            mappings = _mappings.ToList();
        }

        foreach (var mapping in mappings)
        {
            mapping.ApplyValue(_value);
        }
    }

    /// <summary>
    /// Resets the macro to its default value.
    /// </summary>
    public void Reset()
    {
        Value = DefaultValue;
    }

    /// <summary>
    /// Clears all mappings.
    /// </summary>
    public void ClearMappings()
    {
        List<MacroMapping> removed;

        lock (_lock)
        {
            removed = _mappings.ToList();
            _mappings.Clear();
        }

        foreach (var mapping in removed)
        {
            MappingRemoved?.Invoke(this, new MacroMappingEventArgs(mapping, this));
        }
    }

    /// <summary>
    /// Processes a MIDI CC message for this macro.
    /// </summary>
    /// <param name="channel">MIDI channel (0-15).</param>
    /// <param name="cc">CC number (0-127).</param>
    /// <param name="value">CC value (0-127).</param>
    /// <returns>True if the message was processed.</returns>
    public bool ProcessMidiCC(int channel, int cc, int value)
    {
        // Learning mode - assign CC
        if (IsLearning)
        {
            MidiCC = cc;
            MidiChannel = channel;
            IsLearning = false;
            return true;
        }

        // Check if this CC is assigned to this macro
        if (MidiCC == null || MidiCC != cc)
        {
            return false;
        }

        // Check channel (null = omni)
        if (MidiChannel != null && MidiChannel != channel)
        {
            return false;
        }

        // Apply value
        Value = value / 127f;
        return true;
    }
}


/// <summary>
/// Manages a bank of macro controls (typically 8).
/// </summary>
public class MacroBank
{
    private readonly MacroControl[] _macros;
    private readonly object _lock = new();
    private readonly Dictionary<string, object> _targetRegistry = new();
    private Random? _random;

    /// <summary>
    /// Name of the macro bank.
    /// </summary>
    public string Name { get; set; } = "Macros";

    /// <summary>
    /// Gets the number of macros in the bank.
    /// </summary>
    public int Count => _macros.Length;

    /// <summary>
    /// Default colors for macros.
    /// </summary>
    private static readonly string[] DefaultColors = new[]
    {
        "#FF5555", // Red
        "#FFAA55", // Orange
        "#FFFF55", // Yellow
        "#55FF55", // Green
        "#55FFFF", // Cyan
        "#5555FF", // Blue
        "#AA55FF", // Purple
        "#FF55FF"  // Magenta
    };

    /// <summary>
    /// Creates a new macro bank with the specified number of macros.
    /// </summary>
    /// <param name="count">Number of macros (default 8).</param>
    public MacroBank(int count = 8)
    {
        _macros = new MacroControl[count];
        for (int i = 0; i < count; i++)
        {
            _macros[i] = new MacroControl($"Macro {i + 1}", i)
            {
                Color = DefaultColors[i % DefaultColors.Length]
            };
        }
    }

    /// <summary>
    /// Gets the macro at the specified index.
    /// </summary>
    /// <param name="index">The macro index (0-based).</param>
    /// <returns>The macro control.</returns>
    public MacroControl this[int index]
    {
        get
        {
            if (index < 0 || index >= _macros.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return _macros[index];
        }
    }

    /// <summary>
    /// Gets all macros in the bank.
    /// </summary>
    /// <returns>A read-only list of macro controls.</returns>
    public IReadOnlyList<MacroControl> GetMacros()
    {
        return _macros.ToList().AsReadOnly();
    }

    /// <summary>
    /// Gets a macro by its ID.
    /// </summary>
    /// <param name="id">The macro ID.</param>
    /// <returns>The macro control, or null if not found.</returns>
    public MacroControl? GetMacroById(string id)
    {
        return _macros.FirstOrDefault(m => m.Id == id);
    }

    /// <summary>
    /// Resets all macros to their default values.
    /// </summary>
    public void ResetAll()
    {
        foreach (var macro in _macros)
        {
            macro.Reset();
        }
    }

    /// <summary>
    /// Randomizes all macro values for sound design experimentation.
    /// </summary>
    /// <param name="amount">Amount of randomization (0-1, where 1 is full random).</param>
    public void Randomize(float amount = 1.0f)
    {
        _random ??= new Random();
        amount = Math.Clamp(amount, 0f, 1f);

        foreach (var macro in _macros)
        {
            float currentValue = macro.Value;
            float randomValue = (float)_random.NextDouble();
            float newValue = currentValue + (randomValue - currentValue) * amount;
            macro.Value = Math.Clamp(newValue, 0f, 1f);
        }
    }

    /// <summary>
    /// Randomizes a single macro.
    /// </summary>
    /// <param name="index">The macro index.</param>
    /// <param name="amount">Amount of randomization (0-1).</param>
    public void RandomizeMacro(int index, float amount = 1.0f)
    {
        if (index < 0 || index >= _macros.Length) return;

        _random ??= new Random();
        amount = Math.Clamp(amount, 0f, 1f);

        float currentValue = _macros[index].Value;
        float randomValue = (float)_random.NextDouble();
        float newValue = currentValue + (randomValue - currentValue) * amount;
        _macros[index].Value = Math.Clamp(newValue, 0f, 1f);
    }

    /// <summary>
    /// Registers a target object with an ID for serialization purposes.
    /// </summary>
    /// <param name="id">The unique identifier.</param>
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
    /// Gets a registered target by ID.
    /// </summary>
    /// <param name="id">The target ID.</param>
    /// <returns>The target object, or null if not found.</returns>
    public object? GetTarget(string id)
    {
        lock (_lock)
        {
            return _targetRegistry.TryGetValue(id, out var target) ? target : null;
        }
    }

    /// <summary>
    /// Processes a MIDI CC message for all macros.
    /// </summary>
    /// <param name="channel">MIDI channel (0-15).</param>
    /// <param name="cc">CC number (0-127).</param>
    /// <param name="value">CC value (0-127).</param>
    /// <returns>True if any macro processed the message.</returns>
    public bool ProcessMidiCC(int channel, int cc, int value)
    {
        bool processed = false;
        foreach (var macro in _macros)
        {
            if (macro.ProcessMidiCC(channel, cc, value))
            {
                processed = true;
            }
        }
        return processed;
    }

    /// <summary>
    /// Saves the macro bank state to a JSON string.
    /// </summary>
    /// <returns>JSON representation of the macro bank.</returns>
    public string SaveToJson()
    {
        var data = new MacroBankData
        {
            Name = Name,
            Macros = _macros.Select(m => new MacroData
            {
                Name = m.Name,
                Index = m.Index,
                Value = m.Value,
                DefaultValue = m.DefaultValue,
                Color = m.Color,
                MidiCC = m.MidiCC,
                MidiChannel = m.MidiChannel,
                Mappings = m.GetMappings().Select(map => new MacroMappingData
                {
                    TargetParameterId = map.TargetParameterId,
                    TargetObjectId = map.TargetObjectId,
                    PropertyName = map.PropertyName,
                    DisplayName = map.DisplayName,
                    MinValue = map.MinValue,
                    MaxValue = map.MaxValue,
                    Curve = map.Curve.ToString(),
                    Enabled = map.Enabled,
                    Inverted = map.Inverted
                }).ToList()
            }).ToList()
        };

        return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Loads macro bank state from a JSON string.
    /// Note: Target objects must be registered before loading.
    /// </summary>
    /// <param name="json">JSON representation of the macro bank.</param>
    public void LoadFromJson(string json)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var data = JsonSerializer.Deserialize<MacroBankData>(json, options);

        if (data == null) return;

        Name = data.Name ?? "Macros";

        foreach (var macroData in data.Macros ?? Enumerable.Empty<MacroData>())
        {
            if (macroData.Index < 0 || macroData.Index >= _macros.Length) continue;

            var macro = _macros[macroData.Index];
            macro.Name = macroData.Name ?? $"Macro {macroData.Index + 1}";
            macro.DefaultValue = macroData.DefaultValue;
            macro.Color = macroData.Color ?? DefaultColors[macroData.Index % DefaultColors.Length];
            macro.MidiCC = macroData.MidiCC;
            macro.MidiChannel = macroData.MidiChannel;

            // Clear existing mappings
            macro.ClearMappings();

            // Load mappings
            foreach (var mappingData in macroData.Mappings ?? Enumerable.Empty<MacroMappingData>())
            {
                var mapping = new MacroMapping
                {
                    TargetParameterId = mappingData.TargetParameterId ?? "",
                    TargetObjectId = mappingData.TargetObjectId ?? "",
                    PropertyName = mappingData.PropertyName ?? "",
                    DisplayName = mappingData.DisplayName ?? "",
                    MinValue = mappingData.MinValue,
                    MaxValue = mappingData.MaxValue,
                    Curve = Enum.TryParse<MacroCurve>(mappingData.Curve, out var curve) ? curve : MacroCurve.Linear,
                    Enabled = mappingData.Enabled,
                    Inverted = mappingData.Inverted
                };

                // Try to resolve target from registry
                if (!string.IsNullOrEmpty(mappingData.TargetObjectId))
                {
                    mapping.TargetObject = GetTarget(mappingData.TargetObjectId);
                }

                macro.AddMapping(mapping);
            }

            // Set value last to trigger all mappings
            macro.Value = macroData.Value;
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
            foreach (var macro in _macros)
            {
                foreach (var mapping in macro.GetMappings())
                {
                    if (!string.IsNullOrEmpty(mapping.TargetObjectId))
                    {
                        mapping.TargetObject = GetTarget(mapping.TargetObjectId);
                        mapping.CachedProperty = null; // Clear cache to force refresh
                    }
                }

                // Re-apply current value
                macro.ApplyToMappings();
            }
        }
    }

    #region Serialization Classes

    private class MacroBankData
    {
        public string? Name { get; set; }
        public List<MacroData>? Macros { get; set; }
    }

    private class MacroData
    {
        public string? Name { get; set; }
        public int Index { get; set; }
        public float Value { get; set; }
        public float DefaultValue { get; set; }
        public string? Color { get; set; }
        public int? MidiCC { get; set; }
        public int? MidiChannel { get; set; }
        public List<MacroMappingData>? Mappings { get; set; }
    }

    private class MacroMappingData
    {
        public string? TargetParameterId { get; set; }
        public string? TargetObjectId { get; set; }
        public string? PropertyName { get; set; }
        public string? DisplayName { get; set; }
        public float MinValue { get; set; }
        public float MaxValue { get; set; }
        public string? Curve { get; set; }
        public bool Enabled { get; set; }
        public bool Inverted { get; set; }
    }

    #endregion
}
