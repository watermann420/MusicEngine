//Engine License (MEL) – Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: A class representing a parameter that can be controlled live via sliders.


using System;


namespace MusicEngine.Core;


/// <summary>
/// Represents a parameter that can be controlled live via sliders.
/// </summary>
public class LiveParameter
{
    public string Name { get; set; } = "";
    public double Value { get; set; }
    public double MinValue { get; set; }
    public double MaxValue { get; set; }
    public double Step { get; set; } = 1.0;
    public CodeSourceInfo? SourceInfo { get; set; }

    /// <summary>Callback invoked when value changes.</summary>
    public Action<double>? OnValueChanged { get; set; }

    public void SetValue(double newValue)
    {
        Value = Math.Clamp(newValue, MinValue, MaxValue);
        OnValueChanged?.Invoke(Value);
    }
}
