//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Specialized preset class for synthesizers with extended parameter support.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace MusicEngine.Core.Presets;

/// <summary>
/// Defines standard synthesizer categories for preset organization.
/// </summary>
public enum SynthPresetCategory
{
    /// <summary>Bass sounds (sub bass, synth bass, etc.)</summary>
    Bass,
    /// <summary>Lead sounds (mono leads, poly leads)</summary>
    Lead,
    /// <summary>Pad sounds (ambient, evolving)</summary>
    Pad,
    /// <summary>Keys sounds (piano, electric piano, organ)</summary>
    Keys,
    /// <summary>Pluck sounds (short, percussive)</summary>
    Pluck,
    /// <summary>Strings (orchestral, synth strings)</summary>
    Strings,
    /// <summary>Brass sounds</summary>
    Brass,
    /// <summary>Sound effects and experimental</summary>
    FX,
    /// <summary>Drum and percussion sounds</summary>
    Drums,
    /// <summary>Atmospheric and ambient textures</summary>
    Atmosphere,
    /// <summary>Arpeggio and sequence sounds</summary>
    Arpeggio,
    /// <summary>Bell and mallet sounds</summary>
    Bell,
    /// <summary>Synth sounds that don't fit other categories</summary>
    Synth,
    /// <summary>Voice and vocal sounds</summary>
    Vocal,
    /// <summary>Wind instrument sounds</summary>
    Wind,
    /// <summary>Uncategorized or miscellaneous</summary>
    Other
}

/// <summary>
/// Represents a synthesizer preset with comprehensive parameter storage
/// and extended metadata for preset browsing and management.
/// </summary>
public class SynthPreset
{
    /// <summary>
    /// Gets or sets the unique identifier for this preset.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid PresetId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the display name of the preset.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the author/creator of the preset.
    /// </summary>
    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a description of the sound and intended use.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the synthesizer type this preset is designed for.
    /// </summary>
    [JsonPropertyName("synthType")]
    public string SynthType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the preset category.
    /// </summary>
    [JsonPropertyName("category")]
    public SynthPresetCategory Category { get; set; } = SynthPresetCategory.Other;

    /// <summary>
    /// Gets or sets the searchable tags for this preset.
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

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
    /// Gets or sets the creation date of this preset.
    /// </summary>
    [JsonPropertyName("createdDate")]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the last modification date.
    /// </summary>
    [JsonPropertyName("modifiedDate")]
    public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the parameter data dictionary containing all synth parameters.
    /// Supports various value types: float, int, bool, string, arrays.
    /// </summary>
    [JsonPropertyName("parameters")]
    public Dictionary<string, object> ParameterData { get; set; } = [];

    /// <summary>
    /// Gets or sets additional metadata for extensibility.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = [];

    /// <summary>
    /// Gets or sets the preset format version.
    /// </summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number of times this preset has been used.
    /// </summary>
    [JsonPropertyName("usageCount")]
    public int UsageCount { get; set; }

    /// <summary>
    /// Gets or sets the last time this preset was used.
    /// </summary>
    [JsonPropertyName("lastUsed")]
    public DateTime? LastUsed { get; set; }

    /// <summary>
    /// Gets or sets whether this is a factory preset (read-only).
    /// </summary>
    [JsonPropertyName("isFactory")]
    public bool IsFactory { get; set; }

    /// <summary>
    /// Gets or sets the bank ID this preset belongs to.
    /// </summary>
    [JsonPropertyName("bankId")]
    public Guid? BankId { get; set; }

    /// <summary>
    /// Gets or sets the color hint for UI display (hex format).
    /// </summary>
    [JsonPropertyName("color")]
    public string? Color { get; set; }

    /// <summary>
    /// Creates a new empty preset.
    /// </summary>
    public SynthPreset()
    {
    }

    /// <summary>
    /// Creates a new preset with the specified name and synth type.
    /// </summary>
    /// <param name="name">The preset name.</param>
    /// <param name="synthType">The synthesizer type name.</param>
    public SynthPreset(string name, string synthType)
    {
        Name = name;
        SynthType = synthType;
    }

    /// <summary>
    /// Creates a deep clone of this preset with a new ID.
    /// </summary>
    /// <returns>A new SynthPreset with copied values and a new ID.</returns>
    public SynthPreset Clone()
    {
        return new SynthPreset
        {
            PresetId = Guid.NewGuid(),
            Name = Name,
            Author = Author,
            Description = Description,
            SynthType = SynthType,
            Category = Category,
            Tags = [.. Tags],
            IsFavorite = false,
            Rating = 0,
            CreatedDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow,
            ParameterData = CloneParameters(ParameterData),
            Metadata = new Dictionary<string, string>(Metadata),
            Version = Version,
            UsageCount = 0,
            LastUsed = null,
            IsFactory = false,
            BankId = BankId,
            Color = Color
        };
    }

    /// <summary>
    /// Sets a parameter value.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The parameter value.</param>
    public void SetParameter(string name, object value)
    {
        ParameterData[name] = value;
        ModifiedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets a parameter value as the specified type.
    /// </summary>
    /// <typeparam name="T">The expected type.</typeparam>
    /// <param name="name">The parameter name.</param>
    /// <param name="defaultValue">The default value if not found.</param>
    /// <returns>The parameter value or default.</returns>
    public T GetParameter<T>(string name, T defaultValue = default!)
    {
        if (!ParameterData.TryGetValue(name, out var value))
            return defaultValue;

        try
        {
            if (value is T typedValue)
                return typedValue;

            if (value is JsonElement jsonElement)
            {
                return ConvertJsonElement<T>(jsonElement, defaultValue);
            }

            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Gets a float parameter value.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="defaultValue">The default value if not found.</param>
    /// <returns>The parameter value.</returns>
    public float GetFloat(string name, float defaultValue = 0f)
    {
        return GetParameter(name, defaultValue);
    }

    /// <summary>
    /// Gets an integer parameter value.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="defaultValue">The default value if not found.</param>
    /// <returns>The parameter value.</returns>
    public int GetInt(string name, int defaultValue = 0)
    {
        return GetParameter(name, defaultValue);
    }

    /// <summary>
    /// Gets a boolean parameter value.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="defaultValue">The default value if not found.</param>
    /// <returns>The parameter value.</returns>
    public bool GetBool(string name, bool defaultValue = false)
    {
        return GetParameter(name, defaultValue);
    }

    /// <summary>
    /// Gets a string parameter value.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="defaultValue">The default value if not found.</param>
    /// <returns>The parameter value.</returns>
    public string GetString(string name, string defaultValue = "")
    {
        return GetParameter(name, defaultValue);
    }

    /// <summary>
    /// Checks if a parameter exists.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <returns>True if the parameter exists.</returns>
    public bool HasParameter(string name)
    {
        return ParameterData.ContainsKey(name);
    }

    /// <summary>
    /// Removes a parameter.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <returns>True if the parameter was removed.</returns>
    public bool RemoveParameter(string name)
    {
        var removed = ParameterData.Remove(name);
        if (removed)
            ModifiedDate = DateTime.UtcNow;
        return removed;
    }

    /// <summary>
    /// Adds a tag if it doesn't already exist.
    /// </summary>
    /// <param name="tag">The tag to add.</param>
    public void AddTag(string tag)
    {
        if (!string.IsNullOrWhiteSpace(tag) && !Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
        {
            Tags.Add(tag);
            ModifiedDate = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Removes a tag.
    /// </summary>
    /// <param name="tag">The tag to remove.</param>
    /// <returns>True if the tag was removed.</returns>
    public bool RemoveTag(string tag)
    {
        var removed = Tags.RemoveAll(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase)) > 0;
        if (removed)
            ModifiedDate = DateTime.UtcNow;
        return removed;
    }

    /// <summary>
    /// Checks if the preset has a specific tag.
    /// </summary>
    /// <param name="tag">The tag to check.</param>
    /// <returns>True if the tag exists.</returns>
    public bool HasTag(string tag)
    {
        return Tags.Contains(tag, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Records that this preset was used.
    /// </summary>
    public void RecordUsage()
    {
        UsageCount++;
        LastUsed = DateTime.UtcNow;
    }

    /// <summary>
    /// Serializes this preset to JSON.
    /// </summary>
    /// <returns>The JSON string representation.</returns>
    public string ToJson()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        return JsonSerializer.Serialize(this, options);
    }

    /// <summary>
    /// Deserializes a preset from JSON.
    /// </summary>
    /// <param name="json">The JSON string.</param>
    /// <returns>The deserialized preset, or null if invalid.</returns>
    public static SynthPreset? FromJson(string json)
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
            return JsonSerializer.Deserialize<SynthPreset>(json, options);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Saves this preset to a file.
    /// </summary>
    /// <param name="filePath">The file path (typically .mepreset extension).</param>
    public void SaveToFile(string filePath)
    {
        var json = ToJson();
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Loads a preset from a file.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    /// <returns>The loaded preset, or null if loading fails.</returns>
    public static SynthPreset? LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        try
        {
            var json = File.ReadAllText(filePath);
            return FromJson(json);
        }
        catch
        {
            return null;
        }
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

        var lowerQuery = query.ToLowerInvariant();
        return Name.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) ||
               Description.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) ||
               Author.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) ||
               SynthType.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) ||
               Category.ToString().Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) ||
               Tags.Any(t => t.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, object> CloneParameters(Dictionary<string, object> source)
    {
        var clone = new Dictionary<string, object>();
        foreach (var kvp in source)
        {
            clone[kvp.Key] = kvp.Value switch
            {
                ICloneable cloneable => cloneable.Clone(),
                _ => kvp.Value
            };
        }
        return clone;
    }

    private static T ConvertJsonElement<T>(JsonElement element, T defaultValue)
    {
        try
        {
            var type = typeof(T);

            if (type == typeof(float))
                return (T)(object)element.GetSingle();
            if (type == typeof(double))
                return (T)(object)element.GetDouble();
            if (type == typeof(int))
                return (T)(object)element.GetInt32();
            if (type == typeof(long))
                return (T)(object)element.GetInt64();
            if (type == typeof(bool))
                return (T)(object)element.GetBoolean();
            if (type == typeof(string))
                return (T)(object)(element.GetString() ?? string.Empty);

            return defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }
}
