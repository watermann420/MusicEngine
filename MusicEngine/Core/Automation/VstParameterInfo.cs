// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

namespace MusicEngine.Core.Automation;

/// <summary>
/// Contains detailed information about a VST plugin parameter.
/// Used for parameter discovery, display, and automation configuration.
/// </summary>
public sealed class VstParameterInfo
{
    /// <summary>
    /// Parameter index within the plugin (0-based).
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// Display name of the parameter.
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Short name or abbreviation for the parameter (for compact displays).
    /// </summary>
    public string ShortName { get; init; } = "";

    /// <summary>
    /// Unit label for the parameter value (e.g., "dB", "Hz", "%", "ms").
    /// </summary>
    public string Label { get; init; } = "";

    /// <summary>
    /// Minimum value of the parameter (normalized 0-1 for VST, actual value for display).
    /// </summary>
    public float MinValue { get; init; }

    /// <summary>
    /// Maximum value of the parameter (normalized 0-1 for VST, actual value for display).
    /// </summary>
    public float MaxValue { get; init; } = 1.0f;

    /// <summary>
    /// Default value of the parameter (normalized 0-1).
    /// </summary>
    public float DefaultValue { get; init; }

    /// <summary>
    /// Number of discrete steps for the parameter (0 = continuous).
    /// </summary>
    public int StepCount { get; init; }

    /// <summary>
    /// Whether this parameter can be automated.
    /// </summary>
    public bool IsAutomatable { get; init; } = true;

    /// <summary>
    /// Whether this parameter is read-only (cannot be modified by the host).
    /// </summary>
    public bool IsReadOnly { get; init; }

    /// <summary>
    /// Whether this parameter wraps around (e.g., phase parameters).
    /// </summary>
    public bool IsWrapAround { get; init; }

    /// <summary>
    /// Whether this is a list/enum parameter with discrete string values.
    /// </summary>
    public bool IsList { get; init; }

    /// <summary>
    /// Whether this is a program change parameter (VST3).
    /// </summary>
    public bool IsProgramChange { get; init; }

    /// <summary>
    /// Whether this is a bypass parameter.
    /// </summary>
    public bool IsBypass { get; init; }

    /// <summary>
    /// Unit/Group ID this parameter belongs to (VST3).
    /// </summary>
    public int UnitId { get; init; }

    /// <summary>
    /// Unique parameter ID (VST3 uses uint, VST2 uses index).
    /// </summary>
    public uint ParameterId { get; init; }

    /// <summary>
    /// Creates a new VstParameterInfo instance.
    /// </summary>
    public VstParameterInfo()
    {
    }

    /// <summary>
    /// Creates a VstParameterInfo with basic properties.
    /// </summary>
    /// <param name="index">Parameter index.</param>
    /// <param name="name">Parameter name.</param>
    /// <param name="label">Unit label.</param>
    /// <param name="minValue">Minimum value.</param>
    /// <param name="maxValue">Maximum value.</param>
    /// <param name="defaultValue">Default value.</param>
    /// <param name="isAutomatable">Whether the parameter can be automated.</param>
    public VstParameterInfo(
        int index,
        string name,
        string label = "",
        float minValue = 0f,
        float maxValue = 1f,
        float defaultValue = 0f,
        bool isAutomatable = true)
    {
        Index = index;
        Name = name;
        ShortName = name.Length > 8 ? name[..8] : name;
        Label = label;
        MinValue = minValue;
        MaxValue = maxValue;
        DefaultValue = defaultValue;
        IsAutomatable = isAutomatable;
        ParameterId = (uint)index;
    }

    /// <summary>
    /// Converts a normalized value (0-1) to the display value range.
    /// </summary>
    /// <param name="normalizedValue">Normalized value between 0 and 1.</param>
    /// <returns>Value in the display range.</returns>
    public float NormalizedToDisplay(float normalizedValue)
    {
        return MinValue + normalizedValue * (MaxValue - MinValue);
    }

    /// <summary>
    /// Converts a display value to normalized value (0-1).
    /// </summary>
    /// <param name="displayValue">Value in the display range.</param>
    /// <returns>Normalized value between 0 and 1.</returns>
    public float DisplayToNormalized(float displayValue)
    {
        float range = MaxValue - MinValue;
        if (Math.Abs(range) < float.Epsilon)
            return 0f;
        return (displayValue - MinValue) / range;
    }

    /// <summary>
    /// Formats a normalized value as a display string with the unit label.
    /// </summary>
    /// <param name="normalizedValue">Normalized value between 0 and 1.</param>
    /// <returns>Formatted display string.</returns>
    public string FormatValue(float normalizedValue)
    {
        float displayValue = NormalizedToDisplay(normalizedValue);
        string valueStr = StepCount > 0
            ? ((int)Math.Round(displayValue)).ToString()
            : displayValue.ToString("F2");

        return string.IsNullOrEmpty(Label)
            ? valueStr
            : $"{valueStr} {Label}";
    }

    /// <summary>
    /// Returns a string representation of the parameter info.
    /// </summary>
    public override string ToString()
    {
        return $"[{Index}] {Name} ({MinValue:F2} - {MaxValue:F2} {Label})";
    }

    /// <summary>
    /// Creates a deep copy of this parameter info.
    /// </summary>
    public VstParameterInfo Clone()
    {
        return new VstParameterInfo
        {
            Index = Index,
            Name = Name,
            ShortName = ShortName,
            Label = Label,
            MinValue = MinValue,
            MaxValue = MaxValue,
            DefaultValue = DefaultValue,
            StepCount = StepCount,
            IsAutomatable = IsAutomatable,
            IsReadOnly = IsReadOnly,
            IsWrapAround = IsWrapAround,
            IsList = IsList,
            IsProgramChange = IsProgramChange,
            IsBypass = IsBypass,
            UnitId = UnitId,
            ParameterId = ParameterId
        };
    }
}
