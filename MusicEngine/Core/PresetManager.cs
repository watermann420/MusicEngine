// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

namespace MusicEngine.Core;

/// <summary>
/// Central manager for handling preset banks, scanning directories, and managing presets.
/// </summary>
public class PresetManager
{
    private readonly List<PresetBank> _banks = [];
    private readonly Dictionary<string, Preset> _presetCache = [];
    private readonly List<string> _scanPaths = [];

    /// <summary>
    /// Gets the list of loaded preset banks.
    /// </summary>
    public IReadOnlyList<PresetBank> Banks => _banks.AsReadOnly();

    /// <summary>
    /// Gets all presets from all loaded banks.
    /// </summary>
    public IEnumerable<Preset> AllPresets => _banks.SelectMany(b => b.Presets);

    /// <summary>
    /// Gets the list of directories being scanned for presets.
    /// </summary>
    public IReadOnlyList<string> ScanPaths => _scanPaths.AsReadOnly();

    /// <summary>
    /// Event raised when banks are loaded or changed.
    /// </summary>
    public event EventHandler? BanksChanged;

    /// <summary>
    /// Event raised when a preset is saved.
    /// </summary>
    public event EventHandler<Preset>? PresetSaved;

    /// <summary>
    /// Event raised when a preset is deleted.
    /// </summary>
    public event EventHandler<Preset>? PresetDeleted;

    /// <summary>
    /// Adds a directory path to scan for presets.
    /// </summary>
    /// <param name="path">The directory path to add.</param>
    public void AddScanPath(string path)
    {
        if (!_scanPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            _scanPaths.Add(path);
        }
    }

    /// <summary>
    /// Removes a directory path from the scan list.
    /// </summary>
    /// <param name="path">The directory path to remove.</param>
    public void RemoveScanPath(string path)
    {
        _scanPaths.RemoveAll(p => p.Equals(path, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Scans a directory for preset banks and loads them.
    /// </summary>
    /// <param name="directory">The directory to scan.</param>
    /// <returns>The number of banks loaded.</returns>
    public int ScanPresets(string directory)
    {
        if (!Directory.Exists(directory))
            return 0;

        var loadedCount = 0;

        // Check if this directory itself is a bank (contains bank.json or preset files)
        var bankMetadata = Path.Combine(directory, "bank.json");
        var presetFiles = Directory.GetFiles(directory, "*.preset.json", SearchOption.TopDirectoryOnly);

        if (File.Exists(bankMetadata) || presetFiles.Length > 0)
        {
            var bank = PresetBank.LoadFromDirectory(directory);
            if (bank != null && bank.Presets.Count > 0)
            {
                AddBank(bank);
                loadedCount++;
            }
        }

        // Scan subdirectories as potential banks
        foreach (var subDir in Directory.GetDirectories(directory))
        {
            var subBankMetadata = Path.Combine(subDir, "bank.json");
            var subPresetFiles = Directory.GetFiles(subDir, "*.preset.json", SearchOption.AllDirectories);

            if (File.Exists(subBankMetadata) || subPresetFiles.Length > 0)
            {
                var bank = PresetBank.LoadFromDirectory(subDir);
                if (bank != null && bank.Presets.Count > 0)
                {
                    AddBank(bank);
                    loadedCount++;
                }
            }
        }

        // Also check for single-file banks (.bank.json files)
        var bankFiles = Directory.GetFiles(directory, "*.bank.json", SearchOption.TopDirectoryOnly);
        foreach (var bankFile in bankFiles)
        {
            var bank = PresetBank.LoadFromFile(bankFile);
            if (bank != null)
            {
                AddBank(bank);
                loadedCount++;
            }
        }

        if (loadedCount > 0)
        {
            BanksChanged?.Invoke(this, EventArgs.Empty);
        }

        return loadedCount;
    }

    /// <summary>
    /// Scans all registered scan paths for presets.
    /// </summary>
    /// <returns>The total number of banks loaded.</returns>
    public int ScanAllPaths()
    {
        var totalLoaded = 0;
        foreach (var path in _scanPaths)
        {
            totalLoaded += ScanPresets(path);
        }
        return totalLoaded;
    }

    /// <summary>
    /// Adds a preset bank to the manager.
    /// </summary>
    /// <param name="bank">The bank to add.</param>
    public void AddBank(PresetBank bank)
    {
        ArgumentNullException.ThrowIfNull(bank);

        // Remove existing bank with same ID
        _banks.RemoveAll(b => b.Id == bank.Id);
        _banks.Add(bank);

        // Update preset cache
        foreach (var preset in bank.Presets)
        {
            _presetCache[preset.Id] = preset;
        }

        BanksChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Removes a preset bank from the manager.
    /// </summary>
    /// <param name="bank">The bank to remove.</param>
    public void RemoveBank(PresetBank bank)
    {
        if (_banks.Remove(bank))
        {
            // Remove presets from cache
            foreach (var preset in bank.Presets)
            {
                _presetCache.Remove(preset.Id);
            }

            BanksChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gets a bank by ID.
    /// </summary>
    /// <param name="bankId">The bank ID to find.</param>
    /// <returns>The bank, or null if not found.</returns>
    public PresetBank? GetBankById(string bankId)
    {
        return _banks.FirstOrDefault(b => b.Id == bankId);
    }

    /// <summary>
    /// Gets a bank by name.
    /// </summary>
    /// <param name="name">The bank name to find.</param>
    /// <returns>The bank, or null if not found.</returns>
    public PresetBank? GetBankByName(string name)
    {
        return _banks.FirstOrDefault(b => b.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets all presets for a specific target type.
    /// </summary>
    /// <param name="targetType">The target type to filter by.</param>
    /// <returns>An enumerable of matching presets.</returns>
    public IEnumerable<Preset> GetPresetsForType(PresetTargetType targetType)
    {
        return AllPresets.Where(p => p.TargetType == targetType);
    }

    /// <summary>
    /// Gets all presets for a specific target class name.
    /// </summary>
    /// <param name="targetClassName">The target class name to filter by.</param>
    /// <returns>An enumerable of matching presets.</returns>
    public IEnumerable<Preset> GetPresetsForClass(string targetClassName)
    {
        return AllPresets.Where(p => p.TargetClassName.Equals(targetClassName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets all presets in a specific category.
    /// </summary>
    /// <param name="category">The category to filter by.</param>
    /// <returns>An enumerable of matching presets.</returns>
    public IEnumerable<Preset> GetPresetsByCategory(string category)
    {
        return AllPresets.Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets all presets with a specific tag.
    /// </summary>
    /// <param name="tag">The tag to filter by.</param>
    /// <returns>An enumerable of matching presets.</returns>
    public IEnumerable<Preset> GetPresetsByTag(string tag)
    {
        return AllPresets.Where(p => p.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets all favorite presets.
    /// </summary>
    /// <returns>An enumerable of favorite presets.</returns>
    public IEnumerable<Preset> GetFavoritePresets()
    {
        return AllPresets.Where(p => p.IsFavorite);
    }

    /// <summary>
    /// Searches presets by name, description, category, or tags.
    /// </summary>
    /// <param name="searchTerm">The search term.</param>
    /// <returns>An enumerable of matching presets.</returns>
    public IEnumerable<Preset> SearchPresets(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return AllPresets;

        var term = searchTerm.ToLowerInvariant();
        return AllPresets.Where(p =>
            p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            p.Description.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            p.Category.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            p.Author.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            p.Tags.Any(t => t.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Gets a preset by ID from the cache.
    /// </summary>
    /// <param name="presetId">The preset ID to find.</param>
    /// <returns>The preset, or null if not found.</returns>
    public Preset? GetPresetById(string presetId)
    {
        return _presetCache.TryGetValue(presetId, out var preset) ? preset : null;
    }

    /// <summary>
    /// Gets all unique categories from all banks.
    /// </summary>
    /// <returns>An enumerable of category names.</returns>
    public IEnumerable<string> GetAllCategories()
    {
        return AllPresets.Select(p => p.Category).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().OrderBy(c => c);
    }

    /// <summary>
    /// Gets all unique tags from all banks.
    /// </summary>
    /// <returns>An enumerable of tags.</returns>
    public IEnumerable<string> GetAllTags()
    {
        return AllPresets.SelectMany(p => p.Tags).Distinct().OrderBy(t => t);
    }

    /// <summary>
    /// Saves a preset to a specific bank.
    /// </summary>
    /// <param name="preset">The preset to save.</param>
    /// <param name="bank">The bank to save to.</param>
    /// <param name="saveToFile">Whether to persist to disk.</param>
    public void SavePreset(Preset preset, PresetBank bank, bool saveToFile = true)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(bank);

        // Update modification date
        preset.ModifiedDate = DateTime.UtcNow;

        // Add or update in bank
        var existing = bank.GetPresetById(preset.Id);
        if (existing != null)
        {
            bank.RemovePreset(existing);
        }
        bank.AddPreset(preset);

        // Update cache
        _presetCache[preset.Id] = preset;

        // Persist to disk if requested
        if (saveToFile && !string.IsNullOrEmpty(bank.DirectoryPath))
        {
            var category = string.IsNullOrWhiteSpace(preset.Category) ? "Uncategorized" : preset.Category;
            var categoryDir = Path.Combine(bank.DirectoryPath, SanitizeFileName(category));
            Directory.CreateDirectory(categoryDir);

            var fileName = SanitizeFileName(preset.Name) + ".preset.json";
            var filePath = Path.Combine(categoryDir, fileName);
            File.WriteAllText(filePath, preset.ToJson());
        }

        PresetSaved?.Invoke(this, preset);
    }

    /// <summary>
    /// Loads a preset from a file.
    /// </summary>
    /// <param name="filePath">The file path to load from.</param>
    /// <returns>The loaded preset, or null if loading fails.</returns>
    public Preset? LoadPreset(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        try
        {
            var json = File.ReadAllText(filePath);
            return Preset.FromJson(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Deletes a preset from a bank.
    /// </summary>
    /// <param name="preset">The preset to delete.</param>
    /// <param name="bank">The bank to delete from.</param>
    /// <param name="deleteFile">Whether to delete from disk.</param>
    public void DeletePreset(Preset preset, PresetBank bank, bool deleteFile = true)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(bank);

        bank.RemovePreset(preset);
        _presetCache.Remove(preset.Id);

        if (deleteFile && !string.IsNullOrEmpty(bank.DirectoryPath))
        {
            // Find and delete the file
            var searchPattern = SanitizeFileName(preset.Name) + ".preset.json";
            var files = Directory.GetFiles(bank.DirectoryPath, searchPattern, SearchOption.AllDirectories);
            foreach (var file in files)
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Ignore deletion errors
                }
            }
        }

        PresetDeleted?.Invoke(this, preset);
    }

    /// <summary>
    /// Creates a new preset bank and optionally saves it.
    /// </summary>
    /// <param name="name">The name of the bank.</param>
    /// <param name="directoryPath">The directory to save the bank to (optional).</param>
    /// <returns>The created bank.</returns>
    public PresetBank CreateBank(string name, string? directoryPath = null)
    {
        var bank = new PresetBank
        {
            Name = name,
            DirectoryPath = directoryPath
        };

        if (!string.IsNullOrEmpty(directoryPath))
        {
            bank.SaveToDirectory(directoryPath);
        }

        AddBank(bank);
        return bank;
    }

    /// <summary>
    /// Clears all loaded banks and resets the manager.
    /// </summary>
    public void Clear()
    {
        _banks.Clear();
        _presetCache.Clear();
        BanksChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Gets tag usage statistics (tag name and count).
    /// </summary>
    /// <returns>A dictionary of tag names to their usage count.</returns>
    public Dictionary<string, int> GetTagStatistics()
    {
        return AllPresets
            .SelectMany(p => p.Tags)
            .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count());
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
