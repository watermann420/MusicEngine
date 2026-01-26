// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MusicEngine.Core.Presets;

/// <summary>
/// Represents a bank (collection) of synth presets with save/load capabilities.
/// Supports the .mepb (MusicEngine Preset Bank) file format.
/// </summary>
public class SynthPresetBank
{
    /// <summary>
    /// The file extension for MusicEngine Preset Bank files.
    /// </summary>
    public const string FileExtension = ".mepb";

    /// <summary>
    /// The file extension for individual preset files.
    /// </summary>
    public const string PresetFileExtension = ".mepreset";

    /// <summary>
    /// Gets or sets the unique identifier for this bank.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid BankId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the display name of the bank.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the author of the bank.
    /// </summary>
    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the bank.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the bank version.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the presets in this bank.
    /// </summary>
    [JsonPropertyName("presets")]
    public List<SynthPreset> Presets { get; set; } = [];

    /// <summary>
    /// Gets or sets whether this is a factory bank (read-only).
    /// </summary>
    [JsonPropertyName("isFactory")]
    public bool IsFactory { get; set; }

    /// <summary>
    /// Gets or sets whether this is a user bank.
    /// </summary>
    [JsonPropertyName("isUser")]
    public bool IsUser { get; set; } = true;

    /// <summary>
    /// Gets or sets the creation date.
    /// </summary>
    [JsonPropertyName("createdDate")]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the modification date.
    /// </summary>
    [JsonPropertyName("modifiedDate")]
    public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the file path this bank was loaded from.
    /// </summary>
    [JsonIgnore]
    public string? FilePath { get; set; }

    /// <summary>
    /// Gets the count of presets in this bank.
    /// </summary>
    [JsonIgnore]
    public int Count => Presets.Count;

    /// <summary>
    /// Gets whether this bank has unsaved changes.
    /// </summary>
    [JsonIgnore]
    public bool IsDirty { get; private set; }

    /// <summary>
    /// Event raised when the bank is modified.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Creates a new empty preset bank.
    /// </summary>
    public SynthPresetBank()
    {
    }

    /// <summary>
    /// Creates a new preset bank with the specified name.
    /// </summary>
    /// <param name="name">The bank name.</param>
    public SynthPresetBank(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Creates a new preset bank with the specified name and author.
    /// </summary>
    /// <param name="name">The bank name.</param>
    /// <param name="author">The bank author.</param>
    public SynthPresetBank(string name, string author)
    {
        Name = name;
        Author = author;
    }

    /// <summary>
    /// Adds a preset to the bank.
    /// </summary>
    /// <param name="preset">The preset to add.</param>
    public void AddPreset(SynthPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        preset.BankId = BankId;
        Presets.Add(preset);
        MarkDirty();
    }

    /// <summary>
    /// Removes a preset from the bank.
    /// </summary>
    /// <param name="preset">The preset to remove.</param>
    /// <returns>True if the preset was removed.</returns>
    public bool RemovePreset(SynthPreset preset)
    {
        if (Presets.Remove(preset))
        {
            MarkDirty();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Removes a preset by ID.
    /// </summary>
    /// <param name="presetId">The preset ID.</param>
    /// <returns>True if the preset was removed.</returns>
    public bool RemovePresetById(Guid presetId)
    {
        var preset = Presets.FirstOrDefault(p => p.PresetId == presetId);
        if (preset != null)
        {
            Presets.Remove(preset);
            MarkDirty();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets a preset by ID.
    /// </summary>
    /// <param name="presetId">The preset ID.</param>
    /// <returns>The preset, or null if not found.</returns>
    public SynthPreset? GetPresetById(Guid presetId)
    {
        return Presets.FirstOrDefault(p => p.PresetId == presetId);
    }

    /// <summary>
    /// Gets a preset by name.
    /// </summary>
    /// <param name="name">The preset name.</param>
    /// <returns>The preset, or null if not found.</returns>
    public SynthPreset? GetPresetByName(string name)
    {
        return Presets.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets presets by category.
    /// </summary>
    /// <param name="category">The category to filter by.</param>
    /// <returns>Matching presets.</returns>
    public IEnumerable<SynthPreset> GetPresetsByCategory(SynthPresetCategory category)
    {
        return Presets.Where(p => p.Category == category);
    }

    /// <summary>
    /// Gets presets for a specific synth type.
    /// </summary>
    /// <param name="synthType">The synth type name.</param>
    /// <returns>Matching presets.</returns>
    public IEnumerable<SynthPreset> GetPresetsForSynth(string synthType)
    {
        return Presets.Where(p => p.SynthType.Equals(synthType, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets presets with a specific tag.
    /// </summary>
    /// <param name="tag">The tag to filter by.</param>
    /// <returns>Matching presets.</returns>
    public IEnumerable<SynthPreset> GetPresetsByTag(string tag)
    {
        return Presets.Where(p => p.HasTag(tag));
    }

    /// <summary>
    /// Gets all unique categories used in this bank.
    /// </summary>
    /// <returns>The unique categories.</returns>
    public IEnumerable<SynthPresetCategory> GetCategories()
    {
        return Presets.Select(p => p.Category).Distinct().OrderBy(c => c.ToString());
    }

    /// <summary>
    /// Gets all unique tags used in this bank.
    /// </summary>
    /// <returns>The unique tags.</returns>
    public IEnumerable<string> GetAllTags()
    {
        return Presets.SelectMany(p => p.Tags).Distinct().OrderBy(t => t);
    }

    /// <summary>
    /// Gets all unique synth types in this bank.
    /// </summary>
    /// <returns>The unique synth types.</returns>
    public IEnumerable<string> GetSynthTypes()
    {
        return Presets.Select(p => p.SynthType).Distinct().OrderBy(s => s);
    }

    /// <summary>
    /// Searches presets in this bank.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <returns>Matching presets.</returns>
    public IEnumerable<SynthPreset> SearchPresets(string query)
    {
        return Presets.Where(p => p.MatchesSearch(query));
    }

    /// <summary>
    /// Saves the bank to a .mepb file (compressed JSON).
    /// </summary>
    /// <param name="filePath">The file path.</param>
    public void Save(string filePath)
    {
        ModifiedDate = DateTime.UtcNow;

        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(this, options);

        // Create a compressed archive
        using var fileStream = File.Create(filePath);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create);

        // Add the bank.json entry
        var entry = archive.CreateEntry("bank.json", CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        using var writer = new StreamWriter(entryStream);
        writer.Write(json);

        FilePath = filePath;
        IsDirty = false;
    }

    /// <summary>
    /// Saves the bank as uncompressed JSON.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    public void SaveAsJson(string filePath)
    {
        ModifiedDate = DateTime.UtcNow;

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(this, options);
        File.WriteAllText(filePath, json);

        FilePath = filePath;
        IsDirty = false;
    }

    /// <summary>
    /// Loads a bank from a .mepb file.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    /// <returns>The loaded bank, or null if loading fails.</returns>
    public static SynthPresetBank? Load(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        try
        {
            string json;

            // Check if it's a compressed archive or plain JSON
            using (var fileStream = File.OpenRead(filePath))
            {
                // Try to open as ZIP first
                try
                {
                    using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);
                    var entry = archive.GetEntry("bank.json");
                    if (entry != null)
                    {
                        using var entryStream = entry.Open();
                        using var reader = new StreamReader(entryStream);
                        json = reader.ReadToEnd();
                    }
                    else
                    {
                        return null;
                    }
                }
                catch (InvalidDataException)
                {
                    // Not a ZIP file, try as plain JSON
                    fileStream.Position = 0;
                    using var reader = new StreamReader(fileStream);
                    json = reader.ReadToEnd();
                }
            }

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            var bank = JsonSerializer.Deserialize<SynthPresetBank>(json, options);
            if (bank != null)
            {
                bank.FilePath = filePath;
                bank.IsDirty = false;

                // Set bank ID on all presets
                foreach (var preset in bank.Presets)
                {
                    preset.BankId = bank.BankId;
                }
            }
            return bank;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Loads a bank from a JSON string.
    /// </summary>
    /// <param name="json">The JSON string.</param>
    /// <returns>The loaded bank, or null if loading fails.</returns>
    public static SynthPresetBank? FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<SynthPresetBank>(json, options);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Exports individual presets to a directory.
    /// </summary>
    /// <param name="directoryPath">The output directory.</param>
    public void ExportPresets(string directoryPath)
    {
        Directory.CreateDirectory(directoryPath);

        foreach (var preset in Presets)
        {
            var fileName = SanitizeFileName(preset.Name) + PresetFileExtension;
            var filePath = Path.Combine(directoryPath, fileName);
            preset.SaveToFile(filePath);
        }
    }

    /// <summary>
    /// Imports presets from .mepreset files in a directory.
    /// </summary>
    /// <param name="directoryPath">The directory to scan.</param>
    /// <returns>The number of presets imported.</returns>
    public int ImportPresets(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return 0;

        var files = Directory.GetFiles(directoryPath, "*" + PresetFileExtension, SearchOption.AllDirectories);
        var imported = 0;

        foreach (var file in files)
        {
            var preset = SynthPreset.LoadFromFile(file);
            if (preset != null)
            {
                preset.BankId = BankId;
                Presets.Add(preset);
                imported++;
            }
        }

        if (imported > 0)
        {
            MarkDirty();
        }

        return imported;
    }

    /// <summary>
    /// Merges another bank into this one.
    /// </summary>
    /// <param name="other">The bank to merge.</param>
    /// <param name="overwriteExisting">Whether to overwrite presets with matching names.</param>
    /// <returns>The number of presets added.</returns>
    public int MergeBank(SynthPresetBank other, bool overwriteExisting = false)
    {
        ArgumentNullException.ThrowIfNull(other);

        var added = 0;
        foreach (var preset in other.Presets)
        {
            var existing = GetPresetByName(preset.Name);
            if (existing != null)
            {
                if (overwriteExisting)
                {
                    RemovePreset(existing);
                }
                else
                {
                    continue;
                }
            }

            var clone = preset.Clone();
            clone.BankId = BankId;
            Presets.Add(clone);
            added++;
        }

        if (added > 0)
        {
            MarkDirty();
        }

        return added;
    }

    private void MarkDirty()
    {
        IsDirty = true;
        ModifiedDate = DateTime.UtcNow;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(c => !invalidChars.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "Unnamed" : sanitized;
    }
}
