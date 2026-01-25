//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Category definition for preset organization with hierarchy support.

using System.Text.Json.Serialization;

namespace MusicEngine.Core.Presets;

/// <summary>
/// Represents a category for organizing presets in a hierarchical structure.
/// </summary>
public class PresetCategory
{
    /// <summary>
    /// Gets or sets the unique identifier for this category.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid CategoryId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the display name of the category.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the icon identifier for UI display (e.g., icon name or emoji).
    /// </summary>
    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of what sounds belong in this category.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the parent category ID for hierarchical organization.
    /// </summary>
    [JsonPropertyName("parentId")]
    public Guid? ParentCategoryId { get; set; }

    /// <summary>
    /// Gets or sets the sort order for display purposes.
    /// </summary>
    [JsonPropertyName("sortOrder")]
    public int SortOrder { get; set; }

    /// <summary>
    /// Gets or sets whether this is a built-in system category.
    /// </summary>
    [JsonPropertyName("isBuiltIn")]
    public bool IsBuiltIn { get; set; }

    /// <summary>
    /// Gets or sets the color hint for UI display (hex format).
    /// </summary>
    [JsonPropertyName("color")]
    public string? Color { get; set; }

    /// <summary>
    /// Gets or sets the subcategories.
    /// </summary>
    [JsonIgnore]
    public List<PresetCategory> SubCategories { get; set; } = [];

    /// <summary>
    /// Reference to the parent category.
    /// </summary>
    [JsonIgnore]
    public PresetCategory? Parent { get; set; }

    /// <summary>
    /// Gets the full path of the category including parents.
    /// </summary>
    [JsonIgnore]
    public string FullPath
    {
        get
        {
            if (Parent == null)
                return Name;
            return $"{Parent.FullPath}/{Name}";
        }
    }

    /// <summary>
    /// Gets the depth in the category hierarchy (0 = root).
    /// </summary>
    [JsonIgnore]
    public int Depth
    {
        get
        {
            int depth = 0;
            var current = Parent;
            while (current != null)
            {
                depth++;
                current = current.Parent;
            }
            return depth;
        }
    }

    /// <summary>
    /// Creates a new empty category.
    /// </summary>
    public PresetCategory()
    {
    }

    /// <summary>
    /// Creates a new category with the specified name.
    /// </summary>
    /// <param name="name">The category name.</param>
    public PresetCategory(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Creates a new category with the specified name and icon.
    /// </summary>
    /// <param name="name">The category name.</param>
    /// <param name="icon">The icon identifier.</param>
    public PresetCategory(string name, string icon)
    {
        Name = name;
        Icon = icon;
    }

    /// <summary>
    /// Adds a subcategory to this category.
    /// </summary>
    /// <param name="subCategory">The subcategory to add.</param>
    public void AddSubCategory(PresetCategory subCategory)
    {
        ArgumentNullException.ThrowIfNull(subCategory);
        subCategory.Parent = this;
        subCategory.ParentCategoryId = CategoryId;
        SubCategories.Add(subCategory);
    }

    /// <summary>
    /// Removes a subcategory from this category.
    /// </summary>
    /// <param name="subCategory">The subcategory to remove.</param>
    /// <returns>True if the subcategory was removed.</returns>
    public bool RemoveSubCategory(PresetCategory subCategory)
    {
        if (SubCategories.Remove(subCategory))
        {
            subCategory.Parent = null;
            subCategory.ParentCategoryId = null;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets all categories in this branch (this category and all descendants).
    /// </summary>
    /// <returns>An enumerable of all categories in the branch.</returns>
    public IEnumerable<PresetCategory> GetAllDescendants()
    {
        yield return this;
        foreach (var sub in SubCategories)
        {
            foreach (var desc in sub.GetAllDescendants())
            {
                yield return desc;
            }
        }
    }

    /// <summary>
    /// Checks if this category is a descendant of the specified category.
    /// </summary>
    /// <param name="potentialAncestor">The potential ancestor category.</param>
    /// <returns>True if this is a descendant of the ancestor.</returns>
    public bool IsDescendantOf(PresetCategory potentialAncestor)
    {
        var current = Parent;
        while (current != null)
        {
            if (current.CategoryId == potentialAncestor.CategoryId)
                return true;
            current = current.Parent;
        }
        return false;
    }

    /// <summary>
    /// Provides the built-in preset categories.
    /// </summary>
    public static class BuiltIn
    {
        /// <summary>Bass sounds (sub bass, synth bass, etc.)</summary>
        public static PresetCategory Bass { get; } = new("Bass", "bass")
        {
            CategoryId = new Guid("10000000-0000-0000-0000-000000000001"),
            Description = "Bass sounds including sub bass, synth bass, and bass leads",
            SortOrder = 1,
            IsBuiltIn = true,
            Color = "#4A90D9"
        };

        /// <summary>Lead sounds (mono leads, poly leads)</summary>
        public static PresetCategory Lead { get; } = new("Lead", "lead")
        {
            CategoryId = new Guid("10000000-0000-0000-0000-000000000002"),
            Description = "Lead sounds for melodies and solos",
            SortOrder = 2,
            IsBuiltIn = true,
            Color = "#E74C3C"
        };

        /// <summary>Pad sounds (ambient, evolving)</summary>
        public static PresetCategory Pad { get; } = new("Pad", "pad")
        {
            CategoryId = new Guid("10000000-0000-0000-0000-000000000003"),
            Description = "Pad sounds for harmonic backgrounds and atmospheres",
            SortOrder = 3,
            IsBuiltIn = true,
            Color = "#9B59B6"
        };

        /// <summary>Keys sounds (piano, electric piano, organ)</summary>
        public static PresetCategory Keys { get; } = new("Keys", "piano")
        {
            CategoryId = new Guid("10000000-0000-0000-0000-000000000004"),
            Description = "Keyboard and piano sounds",
            SortOrder = 4,
            IsBuiltIn = true,
            Color = "#3498DB"
        };

        /// <summary>Pluck sounds (short, percussive)</summary>
        public static PresetCategory Pluck { get; } = new("Pluck", "pluck")
        {
            CategoryId = new Guid("10000000-0000-0000-0000-000000000005"),
            Description = "Short, plucked sounds and stabs",
            SortOrder = 5,
            IsBuiltIn = true,
            Color = "#1ABC9C"
        };

        /// <summary>Strings (orchestral, synth strings)</summary>
        public static PresetCategory Strings { get; } = new("Strings", "strings")
        {
            CategoryId = new Guid("10000000-0000-0000-0000-000000000006"),
            Description = "String ensemble and synth string sounds",
            SortOrder = 6,
            IsBuiltIn = true,
            Color = "#8E44AD"
        };

        /// <summary>Brass sounds</summary>
        public static PresetCategory Brass { get; } = new("Brass", "brass")
        {
            CategoryId = new Guid("10000000-0000-0000-0000-000000000007"),
            Description = "Brass and horn sounds",
            SortOrder = 7,
            IsBuiltIn = true,
            Color = "#F39C12"
        };

        /// <summary>Sound effects and experimental</summary>
        public static PresetCategory FX { get; } = new("FX", "fx")
        {
            CategoryId = new Guid("10000000-0000-0000-0000-000000000008"),
            Description = "Sound effects, risers, impacts, and experimental sounds",
            SortOrder = 8,
            IsBuiltIn = true,
            Color = "#E91E63"
        };

        /// <summary>Drum and percussion sounds</summary>
        public static PresetCategory Drums { get; } = new("Drums", "drums")
        {
            CategoryId = new Guid("10000000-0000-0000-0000-000000000009"),
            Description = "Drum and percussion sounds",
            SortOrder = 9,
            IsBuiltIn = true,
            Color = "#795548"
        };

        /// <summary>Atmospheric and ambient textures</summary>
        public static PresetCategory Atmosphere { get; } = new("Atmosphere", "atmosphere")
        {
            CategoryId = new Guid("10000000-0000-0000-0000-000000000010"),
            Description = "Atmospheric textures and ambient sounds",
            SortOrder = 10,
            IsBuiltIn = true,
            Color = "#607D8B"
        };

        /// <summary>
        /// Gets all built-in categories.
        /// </summary>
        public static IReadOnlyList<PresetCategory> All { get; } =
        [
            Bass, Lead, Pad, Keys, Pluck, Strings, Brass, FX, Drums, Atmosphere
        ];

        /// <summary>
        /// Gets a built-in category by name.
        /// </summary>
        /// <param name="name">The category name.</param>
        /// <returns>The category, or null if not found.</returns>
        public static PresetCategory? GetByName(string name)
        {
            return All.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets a built-in category by its enum value.
        /// </summary>
        /// <param name="category">The category enum.</param>
        /// <returns>The category definition, or null if not found.</returns>
        public static PresetCategory? GetByEnum(SynthPresetCategory category)
        {
            return category switch
            {
                SynthPresetCategory.Bass => Bass,
                SynthPresetCategory.Lead => Lead,
                SynthPresetCategory.Pad => Pad,
                SynthPresetCategory.Keys => Keys,
                SynthPresetCategory.Pluck => Pluck,
                SynthPresetCategory.Strings => Strings,
                SynthPresetCategory.Brass => Brass,
                SynthPresetCategory.FX => FX,
                SynthPresetCategory.Drums => Drums,
                SynthPresetCategory.Atmosphere => Atmosphere,
                _ => null
            };
        }
    }

    /// <summary>
    /// Returns the category name for display.
    /// </summary>
    public override string ToString() => Name;

    /// <summary>
    /// Determines equality based on CategoryId.
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is PresetCategory other)
            return CategoryId == other.CategoryId;
        return false;
    }

    /// <summary>
    /// Gets hash code based on CategoryId.
    /// </summary>
    public override int GetHashCode() => CategoryId.GetHashCode();
}

/// <summary>
/// Manages a collection of preset categories.
/// </summary>
public class PresetCategoryManager
{
    private readonly Dictionary<Guid, PresetCategory> _categories = new();
    private readonly List<PresetCategory> _rootCategories = [];

    /// <summary>
    /// Gets all root-level categories.
    /// </summary>
    public IReadOnlyList<PresetCategory> RootCategories => _rootCategories.AsReadOnly();

    /// <summary>
    /// Gets all categories including subcategories.
    /// </summary>
    public IEnumerable<PresetCategory> AllCategories => _categories.Values;

    /// <summary>
    /// Creates a new category manager with built-in categories.
    /// </summary>
    public PresetCategoryManager()
    {
        // Add built-in categories
        foreach (var category in PresetCategory.BuiltIn.All)
        {
            AddCategory(category);
        }
    }

    /// <summary>
    /// Adds a category to the manager.
    /// </summary>
    /// <param name="category">The category to add.</param>
    public void AddCategory(PresetCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);

        _categories[category.CategoryId] = category;

        if (category.ParentCategoryId.HasValue)
        {
            if (_categories.TryGetValue(category.ParentCategoryId.Value, out var parent))
            {
                parent.AddSubCategory(category);
            }
        }
        else
        {
            if (!_rootCategories.Contains(category))
            {
                _rootCategories.Add(category);
                _rootCategories.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
            }
        }
    }

    /// <summary>
    /// Gets a category by ID.
    /// </summary>
    /// <param name="categoryId">The category ID.</param>
    /// <returns>The category, or null if not found.</returns>
    public PresetCategory? GetById(Guid categoryId)
    {
        return _categories.TryGetValue(categoryId, out var category) ? category : null;
    }

    /// <summary>
    /// Gets a category by name.
    /// </summary>
    /// <param name="name">The category name.</param>
    /// <returns>The category, or null if not found.</returns>
    public PresetCategory? GetByName(string name)
    {
        return _categories.Values.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Removes a category and all its subcategories.
    /// </summary>
    /// <param name="categoryId">The category ID to remove.</param>
    /// <returns>True if the category was removed.</returns>
    public bool RemoveCategory(Guid categoryId)
    {
        if (!_categories.TryGetValue(categoryId, out var category))
            return false;

        // Cannot remove built-in categories
        if (category.IsBuiltIn)
            return false;

        // Remove all descendants first
        foreach (var sub in category.SubCategories.ToList())
        {
            RemoveCategory(sub.CategoryId);
        }

        // Remove from parent or root
        if (category.Parent != null)
        {
            category.Parent.RemoveSubCategory(category);
        }
        else
        {
            _rootCategories.Remove(category);
        }

        _categories.Remove(categoryId);
        return true;
    }

    /// <summary>
    /// Creates a custom category.
    /// </summary>
    /// <param name="name">The category name.</param>
    /// <param name="parentId">Optional parent category ID.</param>
    /// <returns>The new category.</returns>
    public PresetCategory CreateCategory(string name, Guid? parentId = null)
    {
        var category = new PresetCategory(name)
        {
            ParentCategoryId = parentId,
            SortOrder = _categories.Count
        };

        AddCategory(category);
        return category;
    }

    /// <summary>
    /// Gets a flat list of all categories sorted for display.
    /// </summary>
    /// <returns>Categories sorted by hierarchy and sort order.</returns>
    public IEnumerable<PresetCategory> GetFlatList()
    {
        foreach (var root in _rootCategories.OrderBy(c => c.SortOrder))
        {
            foreach (var cat in root.GetAllDescendants())
            {
                yield return cat;
            }
        }
    }
}
