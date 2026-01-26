// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

namespace MusicEngine.Core.Presets;

/// <summary>
/// Interface for synthesizers and effects that support preset save/load operations.
/// Implement this interface to enable full preset functionality including capturing
/// and restoring the complete state of a synth or effect.
/// </summary>
public interface IPresetProvider
{
    /// <summary>
    /// Gets the current state as a dictionary of parameters.
    /// This should capture all settings needed to recreate the current sound.
    /// </summary>
    /// <returns>A dictionary of parameter names to values.</returns>
    Dictionary<string, object> GetPresetData();

    /// <summary>
    /// Loads preset data and applies it to the synth/effect.
    /// </summary>
    /// <param name="data">The preset data dictionary.</param>
    void LoadPresetData(Dictionary<string, object> data);

    /// <summary>
    /// Event raised when the preset state changes (any parameter modification).
    /// </summary>
    event EventHandler? PresetChanged;
}

/// <summary>
/// Provides extension methods for IPresetProvider implementations.
/// </summary>
public static class PresetProviderExtensions
{
    /// <summary>
    /// Creates a SynthPreset from the current state of a preset provider.
    /// </summary>
    /// <param name="provider">The preset provider (synth or effect).</param>
    /// <param name="name">The preset name.</param>
    /// <param name="category">The preset category.</param>
    /// <returns>A new SynthPreset with the current state.</returns>
    public static SynthPreset ToPreset(this IPresetProvider provider, string name, SynthPresetCategory category)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var preset = new SynthPreset
        {
            Name = name,
            Category = category,
            SynthType = provider.GetType().Name
        };

        var data = provider.GetPresetData();
        foreach (var kvp in data)
        {
            preset.SetParameter(kvp.Key, kvp.Value);
        }

        return preset;
    }

    /// <summary>
    /// Applies a preset to a preset provider.
    /// </summary>
    /// <param name="provider">The preset provider (synth or effect).</param>
    /// <param name="preset">The preset to apply.</param>
    public static void ApplyPreset(this IPresetProvider provider, SynthPreset preset)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(preset);

        provider.LoadPresetData(preset.ParameterData);
        preset.RecordUsage();
    }

    /// <summary>
    /// Checks if the current state matches a preset.
    /// </summary>
    /// <param name="provider">The preset provider.</param>
    /// <param name="preset">The preset to compare.</param>
    /// <returns>True if the states match.</returns>
    public static bool MatchesPreset(this IPresetProvider provider, SynthPreset preset)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(preset);

        var currentData = provider.GetPresetData();
        var presetData = preset.ParameterData;

        if (currentData.Count != presetData.Count)
            return false;

        foreach (var kvp in currentData)
        {
            if (!presetData.TryGetValue(kvp.Key, out var presetValue))
                return false;

            if (!Equals(kvp.Value, presetValue))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Saves the current state to a file.
    /// </summary>
    /// <param name="provider">The preset provider.</param>
    /// <param name="filePath">The file path.</param>
    /// <param name="name">The preset name.</param>
    /// <param name="category">The preset category.</param>
    public static void SaveToFile(this IPresetProvider provider, string filePath, string name, SynthPresetCategory category)
    {
        var preset = provider.ToPreset(name, category);
        preset.SaveToFile(filePath);
    }

    /// <summary>
    /// Loads state from a preset file.
    /// </summary>
    /// <param name="provider">The preset provider.</param>
    /// <param name="filePath">The file path.</param>
    /// <returns>True if loaded successfully.</returns>
    public static bool LoadFromFile(this IPresetProvider provider, string filePath)
    {
        var preset = SynthPreset.LoadFromFile(filePath);
        if (preset == null)
            return false;

        provider.LoadPresetData(preset.ParameterData);
        return true;
    }
}
