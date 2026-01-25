//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Service for UI integration with preset browsing, filtering, and preview.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MusicEngine.Core;

namespace MusicEngine.Core.Presets;

/// <summary>
/// Service for UI integration with the preset browser.
/// Provides observable collections and filtering for data binding.
/// </summary>
public class PresetBrowserService : INotifyPropertyChanged
{
    private readonly SynthPresetManager _manager;
    private string _searchQuery = string.Empty;
    private SynthPresetCategory? _currentCategory;
    private string? _currentSynthType;
    private bool _showFavoritesOnly;
    private PresetSortOption _sortOption = PresetSortOption.Name;
    private SynthPreset? _selectedPreset;
    private SynthPreset? _previewPreset;
    private bool _isPreviewMode;
    private ISynth? _previewSynth;
    private Dictionary<string, object>? _originalSynthState;

    /// <summary>
    /// Gets the preset manager instance.
    /// </summary>
    public SynthPresetManager Manager => _manager;

    /// <summary>
    /// Gets the filtered presets collection for UI binding.
    /// </summary>
    public ObservableCollection<SynthPreset> FilteredPresets { get; } = [];

    /// <summary>
    /// Gets the available categories for filtering.
    /// </summary>
    public ObservableCollection<SynthPresetCategory> AvailableCategories { get; } = [];

    /// <summary>
    /// Gets the available synth types for filtering.
    /// </summary>
    public ObservableCollection<string> AvailableSynthTypes { get; } = [];

    /// <summary>
    /// Gets the available tags for filtering.
    /// </summary>
    public ObservableCollection<string> AvailableTags { get; } = [];

    /// <summary>
    /// Gets the selected tags for multi-tag filtering.
    /// </summary>
    public ObservableCollection<string> SelectedTags { get; } = [];

    /// <summary>
    /// Gets the favorite presets.
    /// </summary>
    public ObservableCollection<SynthPreset> FavoritePresets { get; } = [];

    /// <summary>
    /// Gets the recent presets.
    /// </summary>
    public ObservableCollection<SynthPreset> RecentPresets { get; } = [];

    /// <summary>
    /// Gets or sets the search query.
    /// </summary>
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                RefreshFilteredPresets();
            }
        }
    }

    /// <summary>
    /// Gets or sets the current category filter.
    /// </summary>
    public SynthPresetCategory? CurrentCategory
    {
        get => _currentCategory;
        set
        {
            if (SetProperty(ref _currentCategory, value))
            {
                RefreshFilteredPresets();
            }
        }
    }

    /// <summary>
    /// Gets or sets the current synth type filter.
    /// </summary>
    public string? CurrentSynthType
    {
        get => _currentSynthType;
        set
        {
            if (SetProperty(ref _currentSynthType, value))
            {
                RefreshFilteredPresets();
            }
        }
    }

    /// <summary>
    /// Gets or sets whether to show only favorites.
    /// </summary>
    public bool ShowFavoritesOnly
    {
        get => _showFavoritesOnly;
        set
        {
            if (SetProperty(ref _showFavoritesOnly, value))
            {
                RefreshFilteredPresets();
            }
        }
    }

    /// <summary>
    /// Gets or sets the sort option.
    /// </summary>
    public PresetSortOption SortOption
    {
        get => _sortOption;
        set
        {
            if (SetProperty(ref _sortOption, value))
            {
                RefreshFilteredPresets();
            }
        }
    }

    /// <summary>
    /// Gets or sets the selected preset.
    /// </summary>
    public SynthPreset? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (SetProperty(ref _selectedPreset, value))
            {
                OnPropertyChanged(nameof(HasSelection));
            }
        }
    }

    /// <summary>
    /// Gets whether a preset is selected.
    /// </summary>
    public bool HasSelection => _selectedPreset != null;

    /// <summary>
    /// Gets or sets whether preview mode is active.
    /// </summary>
    public bool IsPreviewMode
    {
        get => _isPreviewMode;
        private set => SetProperty(ref _isPreviewMode, value);
    }

    /// <summary>
    /// Gets the current preview preset.
    /// </summary>
    public SynthPreset? PreviewPreset
    {
        get => _previewPreset;
        private set => SetProperty(ref _previewPreset, value);
    }

    /// <summary>
    /// Gets the total number of presets matching current filters.
    /// </summary>
    public int FilteredCount => FilteredPresets.Count;

    /// <summary>
    /// Gets the total number of presets.
    /// </summary>
    public int TotalCount => _manager.AllPresets.Count();

    /// <summary>
    /// Event raised when property changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Event raised when filters are applied.
    /// </summary>
    public event EventHandler? FiltersApplied;

    /// <summary>
    /// Event raised when a preset is selected for loading (double-click or confirm).
    /// </summary>
    public event EventHandler<SynthPreset>? PresetLoadRequested;

    /// <summary>
    /// Creates a new preset browser service.
    /// </summary>
    /// <param name="manager">The preset manager to use.</param>
    public PresetBrowserService(SynthPresetManager manager)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));

        // Subscribe to manager events
        _manager.BanksChanged += OnBanksChanged;
        _manager.PresetSaved += OnPresetSaved;
        _manager.PresetDeleted += OnPresetDeleted;
        _manager.PresetApplied += OnPresetApplied;

        // Subscribe to selected tags changes
        SelectedTags.CollectionChanged += (s, e) => RefreshFilteredPresets();

        // Initial refresh
        RefreshAll();
    }

    /// <summary>
    /// Refreshes all collections and filters.
    /// </summary>
    public void RefreshAll()
    {
        RefreshAvailableCategories();
        RefreshAvailableSynthTypes();
        RefreshAvailableTags();
        RefreshFilteredPresets();
        RefreshFavorites();
        RefreshRecent();
    }

    /// <summary>
    /// Refreshes the filtered presets based on current filters.
    /// </summary>
    public void RefreshFilteredPresets()
    {
        var results = _manager.SearchPresets(
            _searchQuery,
            _currentCategory,
            SelectedTags.Count > 0 ? SelectedTags : null,
            _currentSynthType);

        if (_showFavoritesOnly)
        {
            results = results.Where(p => p.IsFavorite);
        }

        results = SynthPresetManager.SortPresets(results, _sortOption);

        FilteredPresets.Clear();
        foreach (var preset in results)
        {
            FilteredPresets.Add(preset);
        }

        OnPropertyChanged(nameof(FilteredCount));
        FiltersApplied?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Refreshes the available categories list.
    /// </summary>
    public void RefreshAvailableCategories()
    {
        AvailableCategories.Clear();
        foreach (var category in _manager.GetAllCategories())
        {
            AvailableCategories.Add(category);
        }
    }

    /// <summary>
    /// Refreshes the available synth types list.
    /// </summary>
    public void RefreshAvailableSynthTypes()
    {
        AvailableSynthTypes.Clear();
        foreach (var synthType in _manager.GetAllSynthTypes())
        {
            AvailableSynthTypes.Add(synthType);
        }
    }

    /// <summary>
    /// Refreshes the available tags list.
    /// </summary>
    public void RefreshAvailableTags()
    {
        AvailableTags.Clear();
        foreach (var tag in _manager.GetAllTags())
        {
            AvailableTags.Add(tag);
        }
    }

    /// <summary>
    /// Refreshes the favorites list.
    /// </summary>
    public void RefreshFavorites()
    {
        FavoritePresets.Clear();
        foreach (var preset in _manager.GetFavorites().OrderBy(p => p.Name))
        {
            FavoritePresets.Add(preset);
        }
    }

    /// <summary>
    /// Refreshes the recent presets list.
    /// </summary>
    public void RefreshRecent()
    {
        RecentPresets.Clear();
        foreach (var preset in _manager.GetRecent(20))
        {
            RecentPresets.Add(preset);
        }
    }

    /// <summary>
    /// Clears all filters.
    /// </summary>
    public void ClearFilters()
    {
        _searchQuery = string.Empty;
        _currentCategory = null;
        _currentSynthType = null;
        _showFavoritesOnly = false;
        SelectedTags.Clear();

        OnPropertyChanged(nameof(SearchQuery));
        OnPropertyChanged(nameof(CurrentCategory));
        OnPropertyChanged(nameof(CurrentSynthType));
        OnPropertyChanged(nameof(ShowFavoritesOnly));

        RefreshFilteredPresets();
    }

    /// <summary>
    /// Toggles a tag in the filter selection.
    /// </summary>
    /// <param name="tag">The tag to toggle.</param>
    public void ToggleTag(string tag)
    {
        if (SelectedTags.Contains(tag))
        {
            SelectedTags.Remove(tag);
        }
        else
        {
            SelectedTags.Add(tag);
        }
    }

    /// <summary>
    /// Toggles the favorite status of a preset.
    /// </summary>
    /// <param name="preset">The preset to toggle.</param>
    public void ToggleFavorite(SynthPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        preset.IsFavorite = !preset.IsFavorite;

        RefreshFavorites();

        if (_showFavoritesOnly)
        {
            RefreshFilteredPresets();
        }
    }

    /// <summary>
    /// Sets the rating for a preset.
    /// </summary>
    /// <param name="preset">The preset to rate.</param>
    /// <param name="rating">The rating (0-5).</param>
    public void SetRating(SynthPreset preset, int rating)
    {
        ArgumentNullException.ThrowIfNull(preset);
        preset.Rating = rating;

        if (_sortOption == PresetSortOption.Rating)
        {
            RefreshFilteredPresets();
        }
    }

    /// <summary>
    /// Enters preview mode for a preset.
    /// </summary>
    /// <param name="preset">The preset to preview.</param>
    /// <param name="targetSynth">The synth to apply the preview to.</param>
    public void EnterPreviewMode(SynthPreset preset, ISynth targetSynth)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(targetSynth);

        if (!IsPreviewMode)
        {
            // Store original state
            _previewSynth = targetSynth;
            if (targetSynth is IPresetProvider provider)
            {
                _originalSynthState = new Dictionary<string, object>(provider.GetPresetData());
            }
            IsPreviewMode = true;
        }

        // Apply preview preset
        _manager.ApplyPresetToSynth(preset, targetSynth);
        PreviewPreset = preset;
    }

    /// <summary>
    /// Confirms the preview and keeps the preset applied.
    /// </summary>
    public void ConfirmPreview()
    {
        if (IsPreviewMode && PreviewPreset != null)
        {
            // Keep the current state
            _originalSynthState = null;
            _previewSynth = null;
            PreviewPreset = null;
            IsPreviewMode = false;
        }
    }

    /// <summary>
    /// Cancels preview mode and restores original state.
    /// </summary>
    public void CancelPreview()
    {
        if (IsPreviewMode && _previewSynth != null && _originalSynthState != null)
        {
            // Restore original state
            if (_previewSynth is IPresetProvider provider)
            {
                provider.LoadPresetData(_originalSynthState);
            }

            _originalSynthState = null;
            _previewSynth = null;
            PreviewPreset = null;
            IsPreviewMode = false;
        }
    }

    /// <summary>
    /// Requests loading the selected preset.
    /// </summary>
    public void RequestLoadSelectedPreset()
    {
        if (_selectedPreset != null)
        {
            PresetLoadRequested?.Invoke(this, _selectedPreset);
        }
    }

    /// <summary>
    /// Navigates to the next preset in the filtered list.
    /// </summary>
    public void SelectNextPreset()
    {
        if (FilteredPresets.Count == 0)
            return;

        if (_selectedPreset == null)
        {
            SelectedPreset = FilteredPresets[0];
            return;
        }

        var currentIndex = FilteredPresets.IndexOf(_selectedPreset);
        if (currentIndex < FilteredPresets.Count - 1)
        {
            SelectedPreset = FilteredPresets[currentIndex + 1];
        }
    }

    /// <summary>
    /// Navigates to the previous preset in the filtered list.
    /// </summary>
    public void SelectPreviousPreset()
    {
        if (FilteredPresets.Count == 0)
            return;

        if (_selectedPreset == null)
        {
            SelectedPreset = FilteredPresets[^1];
            return;
        }

        var currentIndex = FilteredPresets.IndexOf(_selectedPreset);
        if (currentIndex > 0)
        {
            SelectedPreset = FilteredPresets[currentIndex - 1];
        }
    }

    /// <summary>
    /// Gets presets grouped by category.
    /// </summary>
    /// <returns>Grouped presets.</returns>
    public IEnumerable<IGrouping<SynthPresetCategory, SynthPreset>> GetPresetsByCategory()
    {
        return FilteredPresets.GroupBy(p => p.Category).OrderBy(g => g.Key.ToString());
    }

    /// <summary>
    /// Gets presets grouped by synth type.
    /// </summary>
    /// <returns>Grouped presets.</returns>
    public IEnumerable<IGrouping<string, SynthPreset>> GetPresetsBySynthType()
    {
        return FilteredPresets.GroupBy(p => p.SynthType).OrderBy(g => g.Key);
    }

    /// <summary>
    /// Gets presets grouped by author.
    /// </summary>
    /// <returns>Grouped presets.</returns>
    public IEnumerable<IGrouping<string, SynthPreset>> GetPresetsByAuthor()
    {
        return FilteredPresets.GroupBy(p => p.Author).OrderBy(g => g.Key);
    }

    private void OnBanksChanged(object? sender, EventArgs e)
    {
        RefreshAll();
        OnPropertyChanged(nameof(TotalCount));
    }

    private void OnPresetSaved(object? sender, SynthPreset e)
    {
        RefreshFilteredPresets();
        RefreshFavorites();
    }

    private void OnPresetDeleted(object? sender, SynthPreset e)
    {
        RefreshFilteredPresets();
        RefreshFavorites();
        RefreshRecent();

        if (_selectedPreset == e)
        {
            SelectedPreset = null;
        }
    }

    private void OnPresetApplied(object? sender, SynthPreset e)
    {
        RefreshRecent();
    }

    /// <summary>
    /// Raises the PropertyChanged event.
    /// </summary>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Sets a property value and raises PropertyChanged if the value changed.
    /// </summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
