//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Channel strip preset for saving and loading complete effect chains.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace MusicEngine.Core.Presets;

/// <summary>
/// Represents a complete channel strip preset containing an ordered chain of effects
/// with their parameters. Can be serialized to/from JSON for saving and loading.
/// </summary>
public class ChannelStripPreset
{
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions _deserializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Gets or sets the unique identifier for this preset.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the display name of the preset.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the category of this preset (e.g., "Vocals", "Drums", "Guitar").
    /// </summary>
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a description of the preset and its intended use.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the author/creator of the preset.
    /// </summary>
    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the creation date of this preset.
    /// </summary>
    [JsonPropertyName("created")]
    public DateTime Created { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the last modification date.
    /// </summary>
    [JsonPropertyName("modified")]
    public DateTime Modified { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the preset format version.
    /// </summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    /// <summary>
    /// Gets or sets the list of effects in this channel strip.
    /// Effects are applied in order from first to last.
    /// </summary>
    [JsonPropertyName("effects")]
    public List<EffectPreset> Effects { get; set; } = new();

    /// <summary>
    /// Gets or sets searchable tags for this preset.
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Gets or sets whether this is a factory preset (read-only).
    /// </summary>
    [JsonPropertyName("isFactory")]
    public bool IsFactory { get; set; }

    /// <summary>
    /// Gets or sets whether this preset is marked as a favorite.
    /// </summary>
    [JsonPropertyName("isFavorite")]
    public bool IsFavorite { get; set; }

    /// <summary>
    /// Gets or sets the user rating (1-5 stars, 0 = unrated).
    /// </summary>
    [JsonPropertyName("rating")]
    public int Rating
    {
        get => _rating;
        set => _rating = Math.Clamp(value, 0, 5);
    }
    private int _rating;

    /// <summary>
    /// Gets or sets additional metadata for extensibility.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Creates a new empty channel strip preset.
    /// </summary>
    public ChannelStripPreset()
    {
    }

    /// <summary>
    /// Creates a new channel strip preset with the specified name.
    /// </summary>
    /// <param name="name">The preset name.</param>
    public ChannelStripPreset(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// Creates a new channel strip preset with the specified name and category.
    /// </summary>
    /// <param name="name">The preset name.</param>
    /// <param name="category">The preset category.</param>
    public ChannelStripPreset(string name, string category)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Category = category ?? string.Empty;
    }

    /// <summary>
    /// Adds an effect to the chain.
    /// </summary>
    /// <param name="effect">The effect preset to add.</param>
    public void AddEffect(EffectPreset effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        Effects.Add(effect);
        Modified = DateTime.UtcNow;
    }

    /// <summary>
    /// Inserts an effect at the specified position in the chain.
    /// </summary>
    /// <param name="index">The index to insert at.</param>
    /// <param name="effect">The effect preset to insert.</param>
    public void InsertEffect(int index, EffectPreset effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        Effects.Insert(index, effect);
        Modified = DateTime.UtcNow;
    }

    /// <summary>
    /// Removes an effect from the chain.
    /// </summary>
    /// <param name="index">The index of the effect to remove.</param>
    /// <returns>True if the effect was removed.</returns>
    public bool RemoveEffect(int index)
    {
        if (index < 0 || index >= Effects.Count)
            return false;

        Effects.RemoveAt(index);
        Modified = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Moves an effect within the chain.
    /// </summary>
    /// <param name="fromIndex">The current index of the effect.</param>
    /// <param name="toIndex">The target index.</param>
    /// <returns>True if the effect was moved.</returns>
    public bool MoveEffect(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= Effects.Count)
            return false;
        if (toIndex < 0 || toIndex >= Effects.Count)
            return false;
        if (fromIndex == toIndex)
            return true;

        var effect = Effects[fromIndex];
        Effects.RemoveAt(fromIndex);
        Effects.Insert(toIndex, effect);
        Modified = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Clears all effects from the chain.
    /// </summary>
    public void ClearEffects()
    {
        Effects.Clear();
        Modified = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a deep clone of this preset with a new ID.
    /// </summary>
    /// <returns>A new ChannelStripPreset with copied values and a new ID.</returns>
    public ChannelStripPreset Clone()
    {
        return new ChannelStripPreset
        {
            Id = Guid.NewGuid(),
            Name = Name,
            Category = Category,
            Description = Description,
            Author = Author,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            Version = Version,
            Effects = Effects.Select(e => e.Clone()).ToList(),
            Tags = new List<string>(Tags),
            IsFactory = false,
            IsFavorite = false,
            Rating = 0,
            Metadata = new Dictionary<string, string>(Metadata)
        };
    }

    /// <summary>
    /// Serializes this preset to a JSON string.
    /// </summary>
    /// <returns>The JSON representation of this preset.</returns>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, _serializerOptions);
    }

    /// <summary>
    /// Deserializes a preset from a JSON string.
    /// </summary>
    /// <param name="json">The JSON string.</param>
    /// <returns>The deserialized preset.</returns>
    /// <exception cref="ArgumentException">Thrown if the JSON is invalid.</exception>
    public static ChannelStripPreset FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("JSON string cannot be null or empty.", nameof(json));

        var preset = JsonSerializer.Deserialize<ChannelStripPreset>(json, _deserializerOptions);
        return preset ?? throw new ArgumentException("Failed to deserialize preset from JSON.", nameof(json));
    }

    /// <summary>
    /// Tries to deserialize a preset from a JSON string.
    /// </summary>
    /// <param name="json">The JSON string.</param>
    /// <param name="preset">The deserialized preset, or null if deserialization failed.</param>
    /// <returns>True if deserialization succeeded.</returns>
    public static bool TryFromJson(string json, out ChannelStripPreset? preset)
    {
        preset = null;

        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            preset = JsonSerializer.Deserialize<ChannelStripPreset>(json, _deserializerOptions);
            return preset != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Saves this preset to a file asynchronously.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="preset">The preset to save.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public static async Task SaveAsync(string path, ChannelStripPreset preset, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preset);

        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));

        var json = preset.ToJson();
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    /// <summary>
    /// Loads a preset from a file asynchronously.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The loaded preset.</returns>
    public static async Task<ChannelStripPreset> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));

        if (!File.Exists(path))
            throw new FileNotFoundException("Preset file not found.", path);

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return FromJson(json);
    }

    /// <summary>
    /// Saves this preset to a file synchronously.
    /// </summary>
    /// <param name="path">The file path.</param>
    public void Save(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));

        var json = ToJson();
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Loads a preset from a file synchronously.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>The loaded preset.</returns>
    public static ChannelStripPreset Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));

        if (!File.Exists(path))
            throw new FileNotFoundException("Preset file not found.", path);

        var json = File.ReadAllText(path);
        return FromJson(json);
    }

    /// <summary>
    /// Matches the preset against a search query.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <returns>True if the preset matches the query.</returns>
    public bool MatchesSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        return Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               Author.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
               Effects.Any(e => e.EffectType.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Creates a string representation of this preset.
    /// </summary>
    public override string ToString()
    {
        return $"{Name} ({Effects.Count} effects) - {Category}";
    }
}

/// <summary>
/// Represents a single effect configuration within a channel strip preset.
/// Contains the effect type and all parameter values.
/// </summary>
public class EffectPreset
{
    /// <summary>
    /// Gets or sets the unique identifier for this effect instance.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the full type name of the effect (e.g., "MusicEngine.Core.Effects.Compressor").
    /// </summary>
    [JsonPropertyName("effectType")]
    public string EffectType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name for this effect instance.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the parameter values for this effect.
    /// Keys are parameter names, values are the parameter values.
    /// </summary>
    [JsonPropertyName("parameters")]
    public Dictionary<string, float> Parameters { get; set; } = new();

    /// <summary>
    /// Gets or sets the dry/wet mix for this effect (0.0 - 1.0).
    /// </summary>
    [JsonPropertyName("mix")]
    public float Mix { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets whether this effect is enabled.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the order of this effect in the chain (for sorting).
    /// </summary>
    [JsonPropertyName("order")]
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets additional metadata for VST plugins (e.g., plugin path, preset name).
    /// </summary>
    [JsonPropertyName("vstMetadata")]
    public Dictionary<string, string>? VstMetadata { get; set; }

    /// <summary>
    /// Creates a new empty effect preset.
    /// </summary>
    public EffectPreset()
    {
    }

    /// <summary>
    /// Creates a new effect preset with the specified type.
    /// </summary>
    /// <param name="effectType">The full type name of the effect.</param>
    public EffectPreset(string effectType)
    {
        EffectType = effectType ?? throw new ArgumentNullException(nameof(effectType));
        Name = GetDefaultName(effectType);
    }

    /// <summary>
    /// Creates a new effect preset with the specified type and name.
    /// </summary>
    /// <param name="effectType">The full type name of the effect.</param>
    /// <param name="name">The display name.</param>
    public EffectPreset(string effectType, string name)
    {
        EffectType = effectType ?? throw new ArgumentNullException(nameof(effectType));
        Name = name ?? GetDefaultName(effectType);
    }

    /// <summary>
    /// Sets a parameter value.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The parameter value.</param>
    public void SetParameter(string name, float value)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Parameters[name] = value;
        }
    }

    /// <summary>
    /// Gets a parameter value.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="defaultValue">The default value if parameter not found.</param>
    /// <returns>The parameter value.</returns>
    public float GetParameter(string name, float defaultValue = 0f)
    {
        if (string.IsNullOrWhiteSpace(name))
            return defaultValue;

        return Parameters.TryGetValue(name, out var value) ? value : defaultValue;
    }

    /// <summary>
    /// Checks if a parameter exists.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <returns>True if the parameter exists.</returns>
    public bool HasParameter(string name)
    {
        return !string.IsNullOrWhiteSpace(name) && Parameters.ContainsKey(name);
    }

    /// <summary>
    /// Creates a deep clone of this effect preset.
    /// </summary>
    /// <returns>A new EffectPreset with copied values.</returns>
    public EffectPreset Clone()
    {
        return new EffectPreset
        {
            Id = Guid.NewGuid(),
            EffectType = EffectType,
            Name = Name,
            Parameters = new Dictionary<string, float>(Parameters),
            Mix = Mix,
            Enabled = Enabled,
            Order = Order,
            VstMetadata = VstMetadata != null
                ? new Dictionary<string, string>(VstMetadata)
                : null
        };
    }

    /// <summary>
    /// Gets a default display name from the effect type.
    /// </summary>
    private static string GetDefaultName(string effectType)
    {
        if (string.IsNullOrWhiteSpace(effectType))
            return "Unknown Effect";

        // Extract the class name from the full type name
        var lastDot = effectType.LastIndexOf('.');
        return lastDot >= 0 ? effectType[(lastDot + 1)..] : effectType;
    }

    /// <summary>
    /// Creates a string representation of this effect preset.
    /// </summary>
    public override string ToString()
    {
        var status = Enabled ? "" : " [OFF]";
        var mixInfo = Mix < 1.0f ? $" ({Mix * 100:F0}%)" : "";
        return $"{Name}{mixInfo}{status}";
    }
}
