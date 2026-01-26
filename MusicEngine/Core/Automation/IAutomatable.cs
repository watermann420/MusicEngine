// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

namespace MusicEngine.Core.Automation;

/// <summary>
/// Interface for objects that can have their parameters automated.
/// Implementing this interface allows an object to be targeted by the automation system.
/// </summary>
public interface IAutomatable
{
    /// <summary>
    /// Gets the unique identifier for this automatable object.
    /// Used to reference this object in automation lanes.
    /// </summary>
    string AutomationId { get; }

    /// <summary>
    /// Gets the display name for this automatable object.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets the list of automatable parameter names.
    /// </summary>
    IReadOnlyList<string> AutomatableParameters { get; }

    /// <summary>
    /// Gets the current value of an automatable parameter.
    /// </summary>
    /// <param name="parameterName">The name of the parameter.</param>
    /// <returns>The current value, or null if the parameter doesn't exist.</returns>
    float? GetParameterValue(string parameterName);

    /// <summary>
    /// Sets the value of an automatable parameter.
    /// </summary>
    /// <param name="parameterName">The name of the parameter.</param>
    /// <param name="value">The new value.</param>
    /// <returns>True if the parameter was set successfully, false otherwise.</returns>
    bool SetParameterValue(string parameterName, float value);

    /// <summary>
    /// Gets the minimum allowed value for a parameter.
    /// </summary>
    /// <param name="parameterName">The name of the parameter.</param>
    /// <returns>The minimum value, or 0 if the parameter doesn't exist.</returns>
    float GetParameterMinValue(string parameterName);

    /// <summary>
    /// Gets the maximum allowed value for a parameter.
    /// </summary>
    /// <param name="parameterName">The name of the parameter.</param>
    /// <returns>The maximum value, or 1 if the parameter doesn't exist.</returns>
    float GetParameterMaxValue(string parameterName);

    /// <summary>
    /// Gets the default value for a parameter.
    /// </summary>
    /// <param name="parameterName">The name of the parameter.</param>
    /// <returns>The default value, or 0 if the parameter doesn't exist.</returns>
    float GetParameterDefaultValue(string parameterName);

    /// <summary>
    /// Fired when a parameter value changes.
    /// </summary>
    event EventHandler<AutomationParameterChangedEventArgs>? ParameterChanged;
}

/// <summary>
/// Event arguments for parameter change notifications.
/// </summary>
public class AutomationParameterChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the name of the parameter that changed.
    /// </summary>
    public string ParameterName { get; }

    /// <summary>
    /// Gets the old value of the parameter.
    /// </summary>
    public float OldValue { get; }

    /// <summary>
    /// Gets the new value of the parameter.
    /// </summary>
    public float NewValue { get; }

    /// <summary>
    /// Creates a new parameter changed event.
    /// </summary>
    /// <param name="parameterName">The name of the parameter.</param>
    /// <param name="oldValue">The old value.</param>
    /// <param name="newValue">The new value.</param>
    public AutomationParameterChangedEventArgs(string parameterName, float oldValue, float newValue)
    {
        ParameterName = parameterName;
        OldValue = oldValue;
        NewValue = newValue;
    }
}

/// <summary>
/// Descriptor for an automatable parameter providing metadata.
/// </summary>
public class AutomatableParameterDescriptor
{
    /// <summary>
    /// Gets or sets the parameter name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name for UI.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the minimum value.
    /// </summary>
    public float MinValue { get; set; }

    /// <summary>
    /// Gets or sets the maximum value.
    /// </summary>
    public float MaxValue { get; set; } = 1f;

    /// <summary>
    /// Gets or sets the default value.
    /// </summary>
    public float DefaultValue { get; set; }

    /// <summary>
    /// Gets or sets the step size for discrete parameters.
    /// </summary>
    public float StepSize { get; set; } = 0.01f;

    /// <summary>
    /// Gets or sets the unit label (e.g., "dB", "Hz", "%").
    /// </summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this parameter is logarithmic.
    /// </summary>
    public bool IsLogarithmic { get; set; }

    /// <summary>
    /// Gets or sets the category/group for UI organization.
    /// </summary>
    public string Category { get; set; } = "General";
}
