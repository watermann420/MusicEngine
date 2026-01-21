//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Represents a preset for synthesizers or effects with parameters and metadata.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace MusicEngine.Core;

/// <summary>
/// Defines the type of target for a preset.
/// </summary>
public enum PresetTargetType
{
    /// <summary>
    /// Preset for a synthesizer.
    /// </summary>
    Synth,

    /// <summary>
    /// Preset for an audio effect.
    /// </summary>
    Effect
}

/// <summary>
/// Represents a preset containing parameters and metadata for synths or effects.
/// </summary>
public class Preset
{
    /// <summary>
    /// Gets or sets the unique identifier for the preset.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the display name of the preset.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the author of the preset.
    /// </summary>
    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the preset.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the category (e.g., Bass, Lead, Pad, Ambient).
    /// </summary>
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tags for searching and filtering.
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Gets or sets the target type (Synth or Effect).
    /// </summary>
    [JsonPropertyName("targetType")]
    public PresetTargetType TargetType { get; set; } = PresetTargetType.Synth;

    /// <summary>
    /// Gets or sets the specific target class name (e.g., "FMSynth", "ReverbEffect").
    /// </summary>
    [JsonPropertyName("targetClassName")]
    public string TargetClassName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the parameters dictionary containing parameter name-value pairs.
    /// </summary>
    [JsonPropertyName("parameters")]
    public Dictionary<string, float> Parameters { get; set; } = [];

    /// <summary>
    /// Gets or sets the creation date of the preset.
    /// </summary>
    [JsonPropertyName("createdDate")]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the last modification date of the preset.
    /// </summary>
    [JsonPropertyName("modifiedDate")]
    public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the version number of the preset format.
    /// </summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    /// <summary>
    /// Gets or sets whether this preset is marked as a favorite.
    /// </summary>
    [JsonPropertyName("isFavorite")]
    public bool IsFavorite { get; set; }

    /// <summary>
    /// Gets or sets the rating (0-5 stars).
    /// </summary>
    [JsonPropertyName("rating")]
    public int Rating { get; set; }

    /// <summary>
    /// Gets or sets custom metadata for extended information.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = [];

    /// <summary>
    /// Creates a deep clone of this preset.
    /// </summary>
    /// <returns>A new Preset instance with copied values.</returns>
    public Preset Clone()
    {
        return new Preset
        {
            Id = Guid.NewGuid().ToString(),
            Name = Name,
            Author = Author,
            Description = Description,
            Category = Category,
            Tags = [.. Tags],
            TargetType = TargetType,
            TargetClassName = TargetClassName,
            Parameters = new Dictionary<string, float>(Parameters),
            CreatedDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow,
            Version = Version,
            IsFavorite = false,
            Rating = 0,
            Metadata = new Dictionary<string, string>(Metadata)
        };
    }

    /// <summary>
    /// Applies this preset's parameters to a synth.
    /// </summary>
    /// <param name="synth">The synth to apply parameters to.</param>
    public void ApplyTo(ISynth synth)
    {
        if (TargetType != PresetTargetType.Synth)
            throw new InvalidOperationException("This preset is not for synthesizers.");

        foreach (var (name, value) in Parameters)
        {
            synth.SetParameter(name, value);
        }
    }

    /// <summary>
    /// Applies this preset's parameters to an effect.
    /// </summary>
    /// <param name="effect">The effect to apply parameters to.</param>
    public void ApplyTo(IEffect effect)
    {
        if (TargetType != PresetTargetType.Effect)
            throw new InvalidOperationException("This preset is not for effects.");

        foreach (var (name, value) in Parameters)
        {
            effect.SetParameter(name, value);
        }
    }

    /// <summary>
    /// Creates a preset from a synth's current state.
    /// </summary>
    /// <param name="synth">The synth to capture parameters from.</param>
    /// <param name="name">The name for the preset.</param>
    /// <param name="parameterNames">The parameter names to capture.</param>
    /// <returns>A new Preset instance.</returns>
    public static Preset FromSynth(ISynth synth, string name, IEnumerable<string> parameterNames)
    {
        var preset = new Preset
        {
            Name = name,
            TargetType = PresetTargetType.Synth,
            TargetClassName = synth.GetType().Name
        };

        // Note: ISynth doesn't have GetParameter, so this would need to be implemented
        // by the caller providing parameter values
        return preset;
    }

    /// <summary>
    /// Creates a preset from an effect's current state.
    /// </summary>
    /// <param name="effect">The effect to capture parameters from.</param>
    /// <param name="name">The name for the preset.</param>
    /// <param name="parameterNames">The parameter names to capture.</param>
    /// <returns>A new Preset instance.</returns>
    public static Preset FromEffect(IEffect effect, string name, IEnumerable<string> parameterNames)
    {
        var preset = new Preset
        {
            Name = name,
            TargetType = PresetTargetType.Effect,
            TargetClassName = effect.GetType().Name
        };

        foreach (var paramName in parameterNames)
        {
            preset.Parameters[paramName] = effect.GetParameter(paramName);
        }

        return preset;
    }

    /// <summary>
    /// Serializes the preset to JSON.
    /// </summary>
    /// <returns>JSON string representation of the preset.</returns>
    public string ToJson()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        return JsonSerializer.Serialize(this, options);
    }

    /// <summary>
    /// Deserializes a preset from JSON.
    /// </summary>
    /// <param name="json">The JSON string to parse.</param>
    /// <returns>A Preset instance, or null if parsing fails.</returns>
    public static Preset? FromJson(string json)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
            return JsonSerializer.Deserialize<Preset>(json, options);
        }
        catch
        {
            return null;
        }
    }
}
