// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using MusicEngine.Core;

namespace MusicEngine.Core.Presets;

/// <summary>
/// Sort options for preset listings.
/// </summary>
public enum PresetSortOption
{
    /// <summary>Sort by name alphabetically.</summary>
    Name,
    /// <summary>Sort by name descending.</summary>
    NameDescending,
    /// <summary>Sort by creation date (newest first).</summary>
    DateCreated,
    /// <summary>Sort by creation date (oldest first).</summary>
    DateCreatedAscending,
    /// <summary>Sort by modification date (newest first).</summary>
    DateModified,
    /// <summary>Sort by rating (highest first).</summary>
    Rating,
    /// <summary>Sort by usage count (most used first).</summary>
    MostUsed,
    /// <summary>Sort by last used date (most recent first).</summary>
    RecentlyUsed,
    /// <summary>Sort by category then name.</summary>
    Category,
    /// <summary>Sort by synth type then name.</summary>
    SynthType,
    /// <summary>Sort by author then name.</summary>
    Author
}

/// <summary>
/// Central manager for synth preset banks with scanning, searching, and loading capabilities.
/// </summary>
public class SynthPresetManager
{
    private readonly List<SynthPresetBank> _banks = [];
    private readonly Dictionary<Guid, SynthPreset> _presetCache = [];
    private readonly List<string> _scanPaths = [];
    private readonly List<SynthPreset> _recentPresets = [];
    private const int MaxRecentPresets = 50;

    /// <summary>
    /// Gets the loaded preset banks.
    /// </summary>
    public IReadOnlyList<SynthPresetBank> Banks => _banks.AsReadOnly();

    /// <summary>
    /// Gets all presets from all loaded banks.
    /// </summary>
    public IEnumerable<SynthPreset> AllPresets => _banks.SelectMany(b => b.Presets);

    /// <summary>
    /// Gets the scan paths for preset discovery.
    /// </summary>
    public IReadOnlyList<string> ScanPaths => _scanPaths.AsReadOnly();

    /// <summary>
    /// Gets the recently used presets.
    /// </summary>
    public IReadOnlyList<SynthPreset> RecentPresets => _recentPresets.AsReadOnly();

    /// <summary>
    /// Gets the category manager.
    /// </summary>
    public PresetCategoryManager CategoryManager { get; } = new();

    /// <summary>
    /// Event raised when banks are loaded or changed.
    /// </summary>
    public event EventHandler? BanksChanged;

    /// <summary>
    /// Event raised when a preset is saved.
    /// </summary>
    public event EventHandler<SynthPreset>? PresetSaved;

    /// <summary>
    /// Event raised when a preset is deleted.
    /// </summary>
    public event EventHandler<SynthPreset>? PresetDeleted;

    /// <summary>
    /// Event raised when a preset is applied to a synth.
    /// </summary>
    public event EventHandler<SynthPreset>? PresetApplied;

    /// <summary>
    /// Adds a scan path for preset discovery.
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
    /// Removes a scan path.
    /// </summary>
    /// <param name="path">The directory path to remove.</param>
    public void RemoveScanPath(string path)
    {
        _scanPaths.RemoveAll(p => p.Equals(path, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Scans a directory for preset banks and files.
    /// </summary>
    /// <param name="directory">The directory to scan.</param>
    /// <returns>The number of banks loaded.</returns>
    public int ScanPresetsFolder(string directory)
    {
        if (!Directory.Exists(directory))
            return 0;

        var loadedCount = 0;

        // Scan for .mepb bank files
        var bankFiles = Directory.GetFiles(directory, "*" + SynthPresetBank.FileExtension, SearchOption.AllDirectories);
        foreach (var bankFile in bankFiles)
        {
            var bank = SynthPresetBank.Load(bankFile);
            if (bank != null && bank.Count > 0)
            {
                AddBank(bank);
                loadedCount++;
            }
        }

        // Scan for loose .mepreset files and create a bank for them
        var presetFiles = Directory.GetFiles(directory, "*" + SynthPresetBank.PresetFileExtension, SearchOption.AllDirectories);
        if (presetFiles.Length > 0)
        {
            var looseBank = new SynthPresetBank
            {
                Name = Path.GetFileName(directory) + " (Loose Presets)",
                IsUser = true
            };

            foreach (var presetFile in presetFiles)
            {
                var preset = SynthPreset.LoadFromFile(presetFile);
                if (preset != null)
                {
                    looseBank.AddPreset(preset);
                }
            }

            if (looseBank.Count > 0)
            {
                AddBank(looseBank);
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
    /// Scans all registered scan paths.
    /// </summary>
    /// <returns>The total number of banks loaded.</returns>
    public int ScanAllPaths()
    {
        var total = 0;
        foreach (var path in _scanPaths)
        {
            total += ScanPresetsFolder(path);
        }
        return total;
    }

    /// <summary>
    /// Adds a preset bank to the manager.
    /// </summary>
    /// <param name="bank">The bank to add.</param>
    public void AddBank(SynthPresetBank bank)
    {
        ArgumentNullException.ThrowIfNull(bank);

        // Remove existing bank with same ID
        _banks.RemoveAll(b => b.BankId == bank.BankId);
        _banks.Add(bank);

        // Update cache
        foreach (var preset in bank.Presets)
        {
            _presetCache[preset.PresetId] = preset;
        }

        BanksChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Removes a preset bank.
    /// </summary>
    /// <param name="bank">The bank to remove.</param>
    public void RemoveBank(SynthPresetBank bank)
    {
        if (_banks.Remove(bank))
        {
            foreach (var preset in bank.Presets)
            {
                _presetCache.Remove(preset.PresetId);
            }
            BanksChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gets a bank by ID.
    /// </summary>
    /// <param name="bankId">The bank ID.</param>
    /// <returns>The bank, or null if not found.</returns>
    public SynthPresetBank? GetBankById(Guid bankId)
    {
        return _banks.FirstOrDefault(b => b.BankId == bankId);
    }

    /// <summary>
    /// Gets a bank by name.
    /// </summary>
    /// <param name="name">The bank name.</param>
    /// <returns>The bank, or null if not found.</returns>
    public SynthPresetBank? GetBankByName(string name)
    {
        return _banks.FirstOrDefault(b => b.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets a preset by ID.
    /// </summary>
    /// <param name="presetId">The preset ID.</param>
    /// <returns>The preset, or null if not found.</returns>
    public SynthPreset? GetPresetById(Guid presetId)
    {
        return _presetCache.TryGetValue(presetId, out var preset) ? preset : null;
    }

    /// <summary>
    /// Searches presets across all banks.
    /// </summary>
    /// <param name="query">The search query (optional).</param>
    /// <param name="category">The category filter (optional).</param>
    /// <param name="tags">The tags to filter by (optional).</param>
    /// <param name="synthType">The synth type to filter by (optional).</param>
    /// <returns>Matching presets.</returns>
    public IEnumerable<SynthPreset> SearchPresets(
        string? query = null,
        SynthPresetCategory? category = null,
        IEnumerable<string>? tags = null,
        string? synthType = null)
    {
        var results = AllPresets;

        // Filter by query
        if (!string.IsNullOrWhiteSpace(query))
        {
            results = results.Where(p => p.MatchesSearch(query));
        }

        // Filter by category
        if (category.HasValue)
        {
            results = results.Where(p => p.Category == category.Value);
        }

        // Filter by tags
        if (tags?.Any() == true)
        {
            var tagList = tags.ToList();
            results = results.Where(p => tagList.All(t => p.HasTag(t)));
        }

        // Filter by synth type
        if (!string.IsNullOrWhiteSpace(synthType))
        {
            results = results.Where(p => p.SynthType.Equals(synthType, StringComparison.OrdinalIgnoreCase));
        }

        return results;
    }

    /// <summary>
    /// Gets favorite presets.
    /// </summary>
    /// <returns>Presets marked as favorites.</returns>
    public IEnumerable<SynthPreset> GetFavorites()
    {
        return AllPresets.Where(p => p.IsFavorite);
    }

    /// <summary>
    /// Gets recently used presets.
    /// </summary>
    /// <param name="count">Maximum number to return.</param>
    /// <returns>Recently used presets.</returns>
    public IEnumerable<SynthPreset> GetRecent(int count = 10)
    {
        return _recentPresets.Take(count);
    }

    /// <summary>
    /// Gets most used presets.
    /// </summary>
    /// <param name="count">Maximum number to return.</param>
    /// <returns>Most used presets.</returns>
    public IEnumerable<SynthPreset> GetMostUsed(int count = 10)
    {
        return AllPresets
            .Where(p => p.UsageCount > 0)
            .OrderByDescending(p => p.UsageCount)
            .Take(count);
    }

    /// <summary>
    /// Gets top rated presets.
    /// </summary>
    /// <param name="count">Maximum number to return.</param>
    /// <returns>Top rated presets.</returns>
    public IEnumerable<SynthPreset> GetTopRated(int count = 10)
    {
        return AllPresets
            .Where(p => p.Rating > 0)
            .OrderByDescending(p => p.Rating)
            .ThenBy(p => p.Name)
            .Take(count);
    }

    /// <summary>
    /// Imports a preset from a file.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    /// <param name="targetBank">The bank to add the preset to.</param>
    /// <returns>The imported preset, or null if import fails.</returns>
    public SynthPreset? ImportPreset(string filePath, SynthPresetBank targetBank)
    {
        var preset = SynthPreset.LoadFromFile(filePath);
        if (preset != null)
        {
            targetBank.AddPreset(preset);
            _presetCache[preset.PresetId] = preset;
            PresetSaved?.Invoke(this, preset);
        }
        return preset;
    }

    /// <summary>
    /// Exports a preset to a file.
    /// </summary>
    /// <param name="preset">The preset to export.</param>
    /// <param name="filePath">The file path.</param>
    public void ExportPreset(SynthPreset preset, string filePath)
    {
        ArgumentNullException.ThrowIfNull(preset);
        preset.SaveToFile(filePath);
    }

    /// <summary>
    /// Creates a preset from a synth's current state.
    /// </summary>
    /// <param name="synth">The synth to capture.</param>
    /// <param name="name">The preset name.</param>
    /// <param name="category">The preset category.</param>
    /// <returns>The created preset.</returns>
    public SynthPreset CreatePresetFromSynth(ISynth synth, string name, SynthPresetCategory category)
    {
        ArgumentNullException.ThrowIfNull(synth);

        var preset = new SynthPreset(name, synth.GetType().Name)
        {
            Category = category
        };

        // Check if synth supports IPresetProvider
        if (synth is IPresetProvider provider)
        {
            var data = provider.GetPresetData();
            foreach (var kvp in data)
            {
                preset.SetParameter(kvp.Key, kvp.Value);
            }
        }

        return preset;
    }

    /// <summary>
    /// Applies a preset to a synth.
    /// </summary>
    /// <param name="preset">The preset to apply.</param>
    /// <param name="synth">The synth to apply to.</param>
    public void ApplyPresetToSynth(SynthPreset preset, ISynth synth)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(synth);

        // Check if synth supports IPresetProvider
        if (synth is IPresetProvider provider)
        {
            provider.LoadPresetData(preset.ParameterData);
        }
        else
        {
            // Fall back to SetParameter for each float value
            foreach (var kvp in preset.ParameterData)
            {
                if (kvp.Value is float floatValue)
                {
                    synth.SetParameter(kvp.Key, floatValue);
                }
                else if (kvp.Value is double doubleValue)
                {
                    synth.SetParameter(kvp.Key, (float)doubleValue);
                }
                else if (kvp.Value is int intValue)
                {
                    synth.SetParameter(kvp.Key, intValue);
                }
            }
        }

        // Record usage
        preset.RecordUsage();
        AddToRecent(preset);
        PresetApplied?.Invoke(this, preset);
    }

    /// <summary>
    /// Saves a preset to a bank.
    /// </summary>
    /// <param name="preset">The preset to save.</param>
    /// <param name="bank">The target bank.</param>
    /// <param name="saveBank">Whether to save the bank file.</param>
    public void SavePreset(SynthPreset preset, SynthPresetBank bank, bool saveBank = true)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(bank);

        // Remove existing preset with same ID
        bank.RemovePresetById(preset.PresetId);
        bank.AddPreset(preset);

        _presetCache[preset.PresetId] = preset;

        if (saveBank && !string.IsNullOrEmpty(bank.FilePath))
        {
            bank.Save(bank.FilePath);
        }

        PresetSaved?.Invoke(this, preset);
    }

    /// <summary>
    /// Deletes a preset from a bank.
    /// </summary>
    /// <param name="preset">The preset to delete.</param>
    /// <param name="bank">The bank containing the preset.</param>
    /// <param name="saveBank">Whether to save the bank file.</param>
    public void DeletePreset(SynthPreset preset, SynthPresetBank bank, bool saveBank = true)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(bank);

        bank.RemovePreset(preset);
        _presetCache.Remove(preset.PresetId);
        _recentPresets.Remove(preset);

        if (saveBank && !string.IsNullOrEmpty(bank.FilePath))
        {
            bank.Save(bank.FilePath);
        }

        PresetDeleted?.Invoke(this, preset);
    }

    /// <summary>
    /// Creates a new user bank.
    /// </summary>
    /// <param name="name">The bank name.</param>
    /// <param name="author">The bank author.</param>
    /// <returns>The new bank.</returns>
    public SynthPresetBank CreateBank(string name, string author = "")
    {
        var bank = new SynthPresetBank(name, author) { IsUser = true };
        AddBank(bank);
        return bank;
    }

    /// <summary>
    /// Gets all unique categories across all presets.
    /// </summary>
    /// <returns>The unique categories.</returns>
    public IEnumerable<SynthPresetCategory> GetAllCategories()
    {
        return AllPresets.Select(p => p.Category).Distinct().OrderBy(c => c.ToString());
    }

    /// <summary>
    /// Gets all unique tags across all presets.
    /// </summary>
    /// <returns>The unique tags.</returns>
    public IEnumerable<string> GetAllTags()
    {
        return AllPresets.SelectMany(p => p.Tags).Distinct().OrderBy(t => t);
    }

    /// <summary>
    /// Gets all unique synth types across all presets.
    /// </summary>
    /// <returns>The unique synth types.</returns>
    public IEnumerable<string> GetAllSynthTypes()
    {
        return AllPresets.Select(p => p.SynthType)
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct()
            .OrderBy(s => s);
    }

    /// <summary>
    /// Sorts presets according to the specified option.
    /// </summary>
    /// <param name="presets">The presets to sort.</param>
    /// <param name="sortOption">The sort option.</param>
    /// <returns>Sorted presets.</returns>
    public static IEnumerable<SynthPreset> SortPresets(IEnumerable<SynthPreset> presets, PresetSortOption sortOption)
    {
        return sortOption switch
        {
            PresetSortOption.Name => presets.OrderBy(p => p.Name),
            PresetSortOption.NameDescending => presets.OrderByDescending(p => p.Name),
            PresetSortOption.DateCreated => presets.OrderByDescending(p => p.CreatedDate),
            PresetSortOption.DateCreatedAscending => presets.OrderBy(p => p.CreatedDate),
            PresetSortOption.DateModified => presets.OrderByDescending(p => p.ModifiedDate),
            PresetSortOption.Rating => presets.OrderByDescending(p => p.Rating).ThenBy(p => p.Name),
            PresetSortOption.MostUsed => presets.OrderByDescending(p => p.UsageCount).ThenBy(p => p.Name),
            PresetSortOption.RecentlyUsed => presets.OrderByDescending(p => p.LastUsed ?? DateTime.MinValue).ThenBy(p => p.Name),
            PresetSortOption.Category => presets.OrderBy(p => p.Category).ThenBy(p => p.Name),
            PresetSortOption.SynthType => presets.OrderBy(p => p.SynthType).ThenBy(p => p.Name),
            PresetSortOption.Author => presets.OrderBy(p => p.Author).ThenBy(p => p.Name),
            _ => presets.OrderBy(p => p.Name)
        };
    }

    /// <summary>
    /// Clears all loaded banks.
    /// </summary>
    public void Clear()
    {
        _banks.Clear();
        _presetCache.Clear();
        _recentPresets.Clear();
        BanksChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Gets statistics about the loaded presets.
    /// </summary>
    /// <returns>A dictionary of statistics.</returns>
    public Dictionary<string, int> GetStatistics()
    {
        return new Dictionary<string, int>
        {
            ["TotalBanks"] = _banks.Count,
            ["TotalPresets"] = AllPresets.Count(),
            ["FactoryPresets"] = AllPresets.Count(p => p.IsFactory),
            ["UserPresets"] = AllPresets.Count(p => !p.IsFactory),
            ["FavoritePresets"] = AllPresets.Count(p => p.IsFavorite),
            ["RatedPresets"] = AllPresets.Count(p => p.Rating > 0),
            ["UniqueCategories"] = GetAllCategories().Count(),
            ["UniqueTags"] = GetAllTags().Count(),
            ["UniqueSynthTypes"] = GetAllSynthTypes().Count()
        };
    }

    private void AddToRecent(SynthPreset preset)
    {
        _recentPresets.Remove(preset);
        _recentPresets.Insert(0, preset);

        while (_recentPresets.Count > MaxRecentPresets)
        {
            _recentPresets.RemoveAt(_recentPresets.Count - 1);
        }
    }
}
