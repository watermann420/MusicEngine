// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace MusicEngine.Core;

/// <summary>
/// Represents a bank (collection) of presets with loading and saving capabilities.
/// </summary>
public class PresetBank
{
    /// <summary>
    /// Gets or sets the unique identifier for the bank.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the display name of the bank.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the bank.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the author of the bank.
    /// </summary>
    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the version of the bank.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the list of presets in this bank.
    /// </summary>
    [JsonPropertyName("presets")]
    public List<Preset> Presets { get; set; } = [];

    /// <summary>
    /// Gets or sets the file path this bank was loaded from (not serialized).
    /// </summary>
    [JsonIgnore]
    public string? FilePath { get; set; }

    /// <summary>
    /// Gets or sets the directory path this bank is associated with.
    /// </summary>
    [JsonIgnore]
    public string? DirectoryPath { get; set; }

    /// <summary>
    /// Gets the count of presets in this bank.
    /// </summary>
    [JsonIgnore]
    public int Count => Presets.Count;

    /// <summary>
    /// Adds a preset to the bank.
    /// </summary>
    /// <param name="preset">The preset to add.</param>
    public void AddPreset(Preset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        Presets.Add(preset);
    }

    /// <summary>
    /// Removes a preset from the bank.
    /// </summary>
    /// <param name="preset">The preset to remove.</param>
    /// <returns>True if the preset was removed, false otherwise.</returns>
    public bool RemovePreset(Preset preset)
    {
        return Presets.Remove(preset);
    }

    /// <summary>
    /// Removes a preset by ID.
    /// </summary>
    /// <param name="presetId">The ID of the preset to remove.</param>
    /// <returns>True if the preset was removed, false otherwise.</returns>
    public bool RemovePresetById(string presetId)
    {
        var preset = Presets.FirstOrDefault(p => p.Id == presetId);
        return preset != null && Presets.Remove(preset);
    }

    /// <summary>
    /// Gets a preset by ID.
    /// </summary>
    /// <param name="presetId">The preset ID to find.</param>
    /// <returns>The preset, or null if not found.</returns>
    public Preset? GetPresetById(string presetId)
    {
        return Presets.FirstOrDefault(p => p.Id == presetId);
    }

    /// <summary>
    /// Gets a preset by name.
    /// </summary>
    /// <param name="name">The preset name to find.</param>
    /// <returns>The preset, or null if not found.</returns>
    public Preset? GetPresetByName(string name)
    {
        return Presets.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets all presets in a specific category.
    /// </summary>
    /// <param name="category">The category to filter by.</param>
    /// <returns>An enumerable of matching presets.</returns>
    public IEnumerable<Preset> GetPresetsByCategory(string category)
    {
        return Presets.Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets all presets with a specific tag.
    /// </summary>
    /// <param name="tag">The tag to filter by.</param>
    /// <returns>An enumerable of matching presets.</returns>
    public IEnumerable<Preset> GetPresetsByTag(string tag)
    {
        return Presets.Where(p => p.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets all presets for a specific target type.
    /// </summary>
    /// <param name="targetType">The target type to filter by.</param>
    /// <returns>An enumerable of matching presets.</returns>
    public IEnumerable<Preset> GetPresetsByTargetType(PresetTargetType targetType)
    {
        return Presets.Where(p => p.TargetType == targetType);
    }

    /// <summary>
    /// Gets all unique categories in this bank.
    /// </summary>
    /// <returns>An enumerable of category names.</returns>
    public IEnumerable<string> GetCategories()
    {
        return Presets.Select(p => p.Category).Distinct().OrderBy(c => c);
    }

    /// <summary>
    /// Gets all unique tags in this bank.
    /// </summary>
    /// <returns>An enumerable of tags.</returns>
    public IEnumerable<string> GetAllTags()
    {
        return Presets.SelectMany(p => p.Tags).Distinct().OrderBy(t => t);
    }

    /// <summary>
    /// Loads a preset bank from a directory containing individual preset JSON files.
    /// </summary>
    /// <param name="directoryPath">The directory to load from.</param>
    /// <returns>A new PresetBank instance, or null if the directory doesn't exist.</returns>
    public static PresetBank? LoadFromDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return null;

        var bank = new PresetBank
        {
            DirectoryPath = directoryPath,
            Name = Path.GetFileName(directoryPath)
        };

        // Check for bank metadata file
        var metadataPath = Path.Combine(directoryPath, "bank.json");
        if (File.Exists(metadataPath))
        {
            try
            {
                var metadataJson = File.ReadAllText(metadataPath);
                var metadata = JsonSerializer.Deserialize<PresetBankMetadata>(metadataJson);
                if (metadata != null)
                {
                    bank.Id = metadata.Id ?? bank.Id;
                    bank.Name = metadata.Name ?? bank.Name;
                    bank.Description = metadata.Description ?? string.Empty;
                    bank.Author = metadata.Author ?? string.Empty;
                    bank.Version = metadata.Version ?? "1.0.0";
                }
            }
            catch
            {
                // Ignore metadata errors
            }
        }

        // Load all preset files
        var presetFiles = Directory.GetFiles(directoryPath, "*.preset.json", SearchOption.AllDirectories);
        foreach (var presetFile in presetFiles)
        {
            try
            {
                var json = File.ReadAllText(presetFile);
                var preset = Preset.FromJson(json);
                if (preset != null)
                {
                    bank.Presets.Add(preset);
                }
            }
            catch
            {
                // Skip invalid preset files
            }
        }

        return bank;
    }

    /// <summary>
    /// Saves the preset bank to a directory with individual preset JSON files.
    /// </summary>
    /// <param name="directoryPath">The directory to save to.</param>
    public void SaveToDirectory(string directoryPath)
    {
        Directory.CreateDirectory(directoryPath);
        DirectoryPath = directoryPath;

        // Save bank metadata
        var metadata = new PresetBankMetadata
        {
            Id = Id,
            Name = Name,
            Description = Description,
            Author = Author,
            Version = Version
        };

        var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(directoryPath, "bank.json"), metadataJson);

        // Create category subdirectories and save presets
        foreach (var preset in Presets)
        {
            var category = string.IsNullOrWhiteSpace(preset.Category) ? "Uncategorized" : preset.Category;
            var categoryDir = Path.Combine(directoryPath, SanitizeFileName(category));
            Directory.CreateDirectory(categoryDir);

            var fileName = SanitizeFileName(preset.Name) + ".preset.json";
            var filePath = Path.Combine(categoryDir, fileName);
            File.WriteAllText(filePath, preset.ToJson());
        }
    }

    /// <summary>
    /// Loads a preset bank from a single JSON file containing all presets.
    /// </summary>
    /// <param name="filePath">The file path to load from.</param>
    /// <returns>A PresetBank instance, or null if loading fails.</returns>
    public static PresetBank? LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        try
        {
            var json = File.ReadAllText(filePath);
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
            var bank = JsonSerializer.Deserialize<PresetBank>(json, options);
            if (bank != null)
            {
                bank.FilePath = filePath;
            }
            return bank;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Saves the preset bank to a single JSON file.
    /// </summary>
    /// <param name="filePath">The file path to save to.</param>
    public void SaveToFile(string filePath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        var json = JsonSerializer.Serialize(this, options);
        File.WriteAllText(filePath, json);
        FilePath = filePath;
    }

    /// <summary>
    /// Sanitizes a string for use as a file name.
    /// </summary>
    private static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(c => !invalidChars.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "Unnamed" : sanitized;
    }
}

/// <summary>
/// Metadata for a preset bank (used in bank.json files).
/// </summary>
internal class PresetBankMetadata
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("author")]
    public string? Author { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }
}
