// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;

namespace MusicEngine.Core;

/// <summary>
/// Information about a discovered VST3 plugin
/// </summary>
public class Vst3PluginInfo
{
    /// <summary>Plugin display name</summary>
    public string Name { get; set; } = "";

    /// <summary>Full path to the plugin file or bundle</summary>
    public string Path { get; set; } = "";

    /// <summary>Resolved path to the actual DLL (for bundles)</summary>
    public string ResolvedPath { get; set; } = "";

    /// <summary>Plugin vendor name</summary>
    public string Vendor { get; set; } = "";

    /// <summary>Plugin version string</summary>
    public string Version { get; set; } = "";

    /// <summary>SDK version string</summary>
    public string SdkVersion { get; set; } = "";

    /// <summary>Plugin class ID (TUID)</summary>
    public Guid ClassId { get; set; }

    /// <summary>Controller class ID if separate</summary>
    public Guid ControllerClassId { get; set; }

    /// <summary>Plugin category (Fx, Instrument, etc.)</summary>
    public string Category { get; set; } = "";

    /// <summary>Sub-categories (e.g. "Fx|Reverb")</summary>
    public string SubCategories { get; set; } = "";

    /// <summary>True for instruments, false for effects</summary>
    public bool IsInstrument { get; set; }

    /// <summary>True if plugin is loaded</summary>
    public bool IsLoaded { get; set; }

    /// <summary>Number of audio input channels</summary>
    public int NumInputs { get; set; }

    /// <summary>Number of audio output channels</summary>
    public int NumOutputs { get; set; }

    /// <summary>Number of parameters</summary>
    public int NumParameters { get; set; }

    /// <summary>True if this is a VST3 bundle directory</summary>
    public bool IsBundle { get; set; }

    /// <summary>Class flags from factory</summary>
    public uint ClassFlags { get; set; }

    /// <summary>Always true for VST3 plugins</summary>
    public bool IsVst3 => true;

    /// <summary>
    /// Determines if a plugin is an instrument based on its category and sub-categories
    /// </summary>
    /// <param name="category">The plugin category</param>
    /// <param name="subCategories">The plugin sub-categories</param>
    /// <returns>True if the plugin is an instrument, false otherwise</returns>
    public static bool DetermineIsInstrument(string category, string subCategories)
    {
        if (string.IsNullOrEmpty(category) && string.IsNullOrEmpty(subCategories))
            return false;

        return (category?.Contains("Instrument", StringComparison.OrdinalIgnoreCase) ?? false) ||
               (subCategories?.Contains("Instrument", StringComparison.OrdinalIgnoreCase) ?? false);
    }
}
