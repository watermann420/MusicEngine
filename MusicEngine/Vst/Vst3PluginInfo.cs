// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: VST3 plugin info container.

namespace MusicEngine.Vst;

/// <summary>
/// VST3 plugin metadata discovered by scanning.
/// </summary>
public sealed class Vst3PluginInfo
{
    /// <summary>
    /// Display name of the plugin.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Filesystem path to the plugin.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// Index assigned within a registry list.
    /// </summary>
    public int Index { get; init; }
}
