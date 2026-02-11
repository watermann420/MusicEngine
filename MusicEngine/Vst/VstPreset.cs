// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Lightweight VST preset container for scriptable recall.

namespace MusicEngine.Vst;

/// <summary>
/// Simple VST preset container (name + base64 state).
/// </summary>
public sealed class VstPreset
{
    public VstPreset(string name, string state)
    {
        Name = name ?? string.Empty;
        State = state ?? string.Empty;
    }

    /// <summary>
    /// Preset name for UI/debug use.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Base64 state blob.
    /// </summary>
    public string State { get; }
}
