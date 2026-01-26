// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio/MIDI routing component.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace MusicEngine.Core.Routing;

/// <summary>
/// Stores the state of a single channel in a mixer snapshot.
/// </summary>
public class ChannelSnapshot
{
    /// <summary>
    /// Gets or sets the name of the channel.
    /// </summary>
    [JsonPropertyName("channelName")]
    public string ChannelName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the channel volume (0.0 - 2.0).
    /// </summary>
    [JsonPropertyName("volume")]
    public float Volume { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the channel pan (-1.0 = left, 0.0 = center, 1.0 = right).
    /// </summary>
    [JsonPropertyName("pan")]
    public float Pan { get; set; } = 0.0f;

    /// <summary>
    /// Gets or sets the mute state.
    /// </summary>
    [JsonPropertyName("mute")]
    public bool Mute { get; set; }

    /// <summary>
    /// Gets or sets the solo state.
    /// </summary>
    [JsonPropertyName("solo")]
    public bool Solo { get; set; }

    /// <summary>
    /// Gets or sets the effect parameters.
    /// Key: Effect name, Value: Dictionary of parameter name to value.
    /// </summary>
    [JsonPropertyName("effectParameters")]
    public Dictionary<string, Dictionary<string, float>> EffectParameters { get; set; } = new();

    /// <summary>
    /// Gets or sets the send levels.
    /// Key: Send/Return bus name, Value: Send level.
    /// </summary>
    [JsonPropertyName("sendLevels")]
    public Dictionary<string, float> SendLevels { get; set; } = new();

    /// <summary>
    /// Creates an empty channel snapshot.
    /// </summary>
    public ChannelSnapshot()
    {
    }

    /// <summary>
    /// Creates a channel snapshot with the specified name.
    /// </summary>
    /// <param name="channelName">The name of the channel.</param>
    public ChannelSnapshot(string channelName)
    {
        ChannelName = channelName ?? throw new ArgumentNullException(nameof(channelName));
    }

    /// <summary>
    /// Creates a deep copy of this channel snapshot.
    /// </summary>
    /// <returns>A new ChannelSnapshot with the same values.</returns>
    public ChannelSnapshot Clone()
    {
        return new ChannelSnapshot
        {
            ChannelName = ChannelName,
            Volume = Volume,
            Pan = Pan,
            Mute = Mute,
            Solo = Solo,
            EffectParameters = EffectParameters.ToDictionary(
                kvp => kvp.Key,
                kvp => new Dictionary<string, float>(kvp.Value)),
            SendLevels = new Dictionary<string, float>(SendLevels)
        };
    }

    /// <summary>
    /// Linearly interpolates between this snapshot and another.
    /// </summary>
    /// <param name="other">The target snapshot.</param>
    /// <param name="t">Interpolation factor (0.0 = this, 1.0 = other).</param>
    /// <returns>An interpolated channel snapshot.</returns>
    public ChannelSnapshot Lerp(ChannelSnapshot other, float t)
    {
        if (other == null)
            throw new ArgumentNullException(nameof(other));

        t = Math.Clamp(t, 0f, 1f);

        var result = new ChannelSnapshot
        {
            ChannelName = ChannelName,
            Volume = Volume + (other.Volume - Volume) * t,
            Pan = Pan + (other.Pan - Pan) * t,
            // Boolean states snap at midpoint
            Mute = t < 0.5f ? Mute : other.Mute,
            Solo = t < 0.5f ? Solo : other.Solo
        };

        // Interpolate effect parameters
        var allEffects = new HashSet<string>(EffectParameters.Keys);
        allEffects.UnionWith(other.EffectParameters.Keys);

        foreach (var effectName in allEffects)
        {
            var interpolatedParams = new Dictionary<string, float>();

            EffectParameters.TryGetValue(effectName, out var thisParams);
            other.EffectParameters.TryGetValue(effectName, out var otherParams);

            thisParams ??= new Dictionary<string, float>();
            otherParams ??= new Dictionary<string, float>();

            var allParams = new HashSet<string>(thisParams.Keys);
            allParams.UnionWith(otherParams.Keys);

            foreach (var paramName in allParams)
            {
                float thisValue = thisParams.TryGetValue(paramName, out var tv) ? tv : 0f;
                float otherValue = otherParams.TryGetValue(paramName, out var ov) ? ov : 0f;
                interpolatedParams[paramName] = thisValue + (otherValue - thisValue) * t;
            }

            result.EffectParameters[effectName] = interpolatedParams;
        }

        // Interpolate send levels
        var allSends = new HashSet<string>(SendLevels.Keys);
        allSends.UnionWith(other.SendLevels.Keys);

        foreach (var sendName in allSends)
        {
            float thisLevel = SendLevels.TryGetValue(sendName, out var tl) ? tl : 0f;
            float otherLevel = other.SendLevels.TryGetValue(sendName, out var ol) ? ol : 0f;
            result.SendLevels[sendName] = thisLevel + (otherLevel - thisLevel) * t;
        }

        return result;
    }
}

/// <summary>
/// Represents a complete mixer state snapshot that can be saved, recalled, and interpolated.
/// </summary>
public class MixerSnapshot
{
    /// <summary>
    /// Gets or sets the unique identifier for this snapshot.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the name of this snapshot.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of this snapshot.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when this snapshot was created.
    /// </summary>
    [JsonPropertyName("created")]
    public DateTime Created { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets when this snapshot was last modified.
    /// </summary>
    [JsonPropertyName("modified")]
    public DateTime Modified { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the list of channel snapshots.
    /// </summary>
    [JsonPropertyName("channels")]
    public List<ChannelSnapshot> Channels { get; set; } = new();

    /// <summary>
    /// Gets or sets custom metadata for this snapshot.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Creates an empty mixer snapshot.
    /// </summary>
    public MixerSnapshot()
    {
    }

    /// <summary>
    /// Creates a mixer snapshot with the specified name.
    /// </summary>
    /// <param name="name">The name of the snapshot.</param>
    public MixerSnapshot(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// Serializes this snapshot to JSON.
    /// </summary>
    /// <returns>JSON string representation of the snapshot.</returns>
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
    /// Deserializes a snapshot from JSON.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized MixerSnapshot.</returns>
    /// <exception cref="ArgumentNullException">Thrown if json is null or empty.</exception>
    /// <exception cref="JsonException">Thrown if the JSON is invalid.</exception>
    public static MixerSnapshot FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentNullException(nameof(json));

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Deserialize<MixerSnapshot>(json, options)
               ?? throw new JsonException("Failed to deserialize MixerSnapshot.");
    }

    /// <summary>
    /// Creates a deep copy of this snapshot.
    /// </summary>
    /// <returns>A new MixerSnapshot with the same values.</returns>
    public MixerSnapshot Clone()
    {
        return new MixerSnapshot
        {
            Id = Guid.NewGuid(), // New ID for clone
            Name = Name + " (Copy)",
            Description = Description,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            Channels = Channels.Select(c => c.Clone()).ToList(),
            Metadata = new Dictionary<string, string>(Metadata)
        };
    }

    /// <summary>
    /// Linearly interpolates between this snapshot and another.
    /// </summary>
    /// <param name="other">The target snapshot.</param>
    /// <param name="t">Interpolation factor (0.0 = this, 1.0 = other).</param>
    /// <returns>An interpolated mixer snapshot.</returns>
    public MixerSnapshot Lerp(MixerSnapshot other, float t)
    {
        if (other == null)
            throw new ArgumentNullException(nameof(other));

        t = Math.Clamp(t, 0f, 1f);

        var result = new MixerSnapshot
        {
            Name = t < 0.5f ? Name : other.Name,
            Description = $"Interpolation ({t:P0}) between '{Name}' and '{other.Name}'"
        };

        // Build a map of channels by name for both snapshots
        var thisChannels = Channels.ToDictionary(c => c.ChannelName, c => c);
        var otherChannels = other.Channels.ToDictionary(c => c.ChannelName, c => c);

        var allChannelNames = new HashSet<string>(thisChannels.Keys);
        allChannelNames.UnionWith(otherChannels.Keys);

        foreach (var channelName in allChannelNames)
        {
            thisChannels.TryGetValue(channelName, out var thisChannel);
            otherChannels.TryGetValue(channelName, out var otherChannel);

            if (thisChannel != null && otherChannel != null)
            {
                // Both snapshots have this channel - interpolate
                result.Channels.Add(thisChannel.Lerp(otherChannel, t));
            }
            else if (thisChannel != null)
            {
                // Only this snapshot has the channel - use it if t < 0.5
                if (t < 0.5f)
                {
                    result.Channels.Add(thisChannel.Clone());
                }
            }
            else if (otherChannel != null)
            {
                // Only other snapshot has the channel - use it if t >= 0.5
                if (t >= 0.5f)
                {
                    result.Channels.Add(otherChannel.Clone());
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Gets a channel snapshot by name.
    /// </summary>
    /// <param name="channelName">The name of the channel.</param>
    /// <returns>The channel snapshot, or null if not found.</returns>
    public ChannelSnapshot? GetChannel(string channelName)
    {
        if (string.IsNullOrWhiteSpace(channelName))
            return null;

        return Channels.Find(c => c.ChannelName.Equals(channelName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Adds or updates a channel snapshot.
    /// </summary>
    /// <param name="channel">The channel snapshot to add or update.</param>
    public void SetChannel(ChannelSnapshot channel)
    {
        if (channel == null)
            throw new ArgumentNullException(nameof(channel));

        var existing = GetChannel(channel.ChannelName);
        if (existing != null)
        {
            Channels.Remove(existing);
        }
        Channels.Add(channel);
        Modified = DateTime.UtcNow;
    }

    /// <summary>
    /// Removes a channel snapshot by name.
    /// </summary>
    /// <param name="channelName">The name of the channel to remove.</param>
    /// <returns>True if removed, false if not found.</returns>
    public bool RemoveChannel(string channelName)
    {
        var channel = GetChannel(channelName);
        if (channel != null)
        {
            Channels.Remove(channel);
            Modified = DateTime.UtcNow;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Returns a string representation of this snapshot.
    /// </summary>
    public override string ToString()
    {
        return $"{Name} ({Channels.Count} channels, created {Created:yyyy-MM-dd HH:mm})";
    }
}
