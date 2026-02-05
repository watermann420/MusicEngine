// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann
// Description: VST parameter information from native layer.

namespace MusicEngine.CppLayer;

public readonly struct VstParameter
{
    public int Index { get; }
    public string Name { get; }
    public string Label { get; }
    public string Display { get; }
    public float Value { get; }
    public string DisplayValue => Display;

    public VstParameter(int index, string name, string label, string display, float value)
    {
        Index = index;
        Name = name ?? string.Empty;
        Label = label ?? string.Empty;
        Display = display ?? string.Empty;
        Value = value;
    }
}
