//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Sound variation set for managing groups of variations.


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;


namespace MusicEngine.Core.Midi;


/// <summary>
/// Represents a collection of sound variations that can be activated for an instrument.
/// A variation set typically contains 8 slots for quickly switching between different sounds.
/// </summary>
public class SoundVariationSet
{
    private readonly List<SoundVariation> _variations = new();
    private SoundVariation? _activeVariation;


    /// <summary>
    /// Gets the unique identifier for this variation set.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets or sets the name of this variation set.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets the list of variations in this set.
    /// </summary>
    public IReadOnlyList<SoundVariation> Variations => _variations;

    /// <summary>
    /// Gets the currently active variation.
    /// </summary>
    public SoundVariation? ActiveVariation => _activeVariation;


    /// <summary>
    /// Event raised when the active variation changes.
    /// </summary>
    public event EventHandler<VariationChangedEventArgs>? VariationChanged;


    /// <summary>
    /// Creates a new variation set with a unique ID.
    /// </summary>
    public SoundVariationSet()
    {
        Id = Guid.NewGuid();
    }


    /// <summary>
    /// Creates a new variation set with a specific ID.
    /// </summary>
    /// <param name="id">The unique identifier.</param>
    public SoundVariationSet(Guid id)
    {
        Id = id;
    }


    /// <summary>
    /// Creates a new variation set with the specified name.
    /// </summary>
    /// <param name="name">The name of the variation set.</param>
    public SoundVariationSet(string name)
    {
        Id = Guid.NewGuid();
        Name = name;
    }


    /// <summary>
    /// Adds a variation to this set.
    /// </summary>
    /// <param name="variation">The variation to add.</param>
    public void AddVariation(SoundVariation variation)
    {
        if (variation == null)
            throw new ArgumentNullException(nameof(variation));

        if (!_variations.Contains(variation))
        {
            _variations.Add(variation);

            // Set as active if this is the first variation
            if (_activeVariation == null)
            {
                _activeVariation = variation;
            }
        }
    }


    /// <summary>
    /// Removes a variation from this set.
    /// </summary>
    /// <param name="variation">The variation to remove.</param>
    public void RemoveVariation(SoundVariation variation)
    {
        if (variation == null)
            throw new ArgumentNullException(nameof(variation));

        _variations.Remove(variation);

        // If the removed variation was active, switch to first available
        if (_activeVariation == variation)
        {
            _activeVariation = _variations.Count > 0 ? _variations[0] : null;
        }
    }


    /// <summary>
    /// Activates a variation by its slot number.
    /// </summary>
    /// <param name="slot">The slot number (typically 1-8).</param>
    public void ActivateVariation(int slot)
    {
        var variation = _variations.FirstOrDefault(v => v.Slot == slot);

        if (variation != null)
        {
            ActivateVariationInternal(variation);
        }
    }


    /// <summary>
    /// Activates a variation by its name.
    /// </summary>
    /// <param name="name">The name of the variation.</param>
    public void ActivateVariation(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        var variation = _variations.FirstOrDefault(v =>
            string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));

        if (variation != null)
        {
            ActivateVariationInternal(variation);
        }
    }


    /// <summary>
    /// Activates a specific variation.
    /// </summary>
    /// <param name="variation">The variation to activate.</param>
    public void ActivateVariation(SoundVariation variation)
    {
        if (variation == null)
            throw new ArgumentNullException(nameof(variation));

        if (!_variations.Contains(variation))
            throw new ArgumentException("Variation is not part of this set.", nameof(variation));

        ActivateVariationInternal(variation);
    }


    private void ActivateVariationInternal(SoundVariation variation)
    {
        var previous = _activeVariation;
        _activeVariation = variation;

        VariationChanged?.Invoke(this, new VariationChangedEventArgs(previous, variation));
    }


    /// <summary>
    /// Gets the MIDI messages needed to activate the specified variation.
    /// </summary>
    /// <param name="variation">The variation to activate.</param>
    /// <returns>Array of byte arrays containing MIDI messages.</returns>
    public byte[][] GetActivationMessages(SoundVariation variation)
    {
        if (variation == null)
            throw new ArgumentNullException(nameof(variation));

        return variation.GetAllMessageBytes();
    }


    /// <summary>
    /// Gets a variation by slot number.
    /// </summary>
    /// <param name="slot">The slot number.</param>
    /// <returns>The variation at the specified slot, or null if not found.</returns>
    public SoundVariation? GetVariationBySlot(int slot)
    {
        return _variations.FirstOrDefault(v => v.Slot == slot);
    }


    /// <summary>
    /// Gets a variation by name.
    /// </summary>
    /// <param name="name">The variation name.</param>
    /// <returns>The variation with the specified name, or null if not found.</returns>
    public SoundVariation? GetVariationByName(string name)
    {
        return _variations.FirstOrDefault(v =>
            string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));
    }


    /// <summary>
    /// Creates a default variation set with 8 empty slots.
    /// </summary>
    /// <param name="name">The name of the variation set.</param>
    /// <returns>A new variation set with 8 slots.</returns>
    public static SoundVariationSet CreateDefault(string name = "Default")
    {
        var set = new SoundVariationSet(name);

        for (int i = 1; i <= 8; i++)
        {
            set.AddVariation(new SoundVariation($"Variation {i}", i));
        }

        return set;
    }


    /// <summary>
    /// Serializes this variation set to JSON.
    /// </summary>
    /// <returns>JSON string representation.</returns>
    public string ToJson()
    {
        var dto = new VariationSetDto
        {
            Id = Id,
            Name = Name,
            Variations = _variations.Select(v => new VariationDto
            {
                Id = v.Id,
                Name = v.Name,
                Description = v.Description,
                Slot = v.Slot,
                Messages = v.TriggerMessages.Select(m => new MessageDto
                {
                    Type = m.Type.ToString(),
                    Channel = m.Channel,
                    Data1 = m.Data1,
                    Data2 = m.Data2
                }).ToList()
            }).ToList()
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        return JsonSerializer.Serialize(dto, options);
    }


    /// <summary>
    /// Deserializes a variation set from JSON.
    /// </summary>
    /// <param name="json">The JSON string.</param>
    /// <returns>The deserialized variation set.</returns>
    public static SoundVariationSet FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("JSON string cannot be null or empty.", nameof(json));

        var dto = JsonSerializer.Deserialize<VariationSetDto>(json);

        if (dto == null)
            throw new InvalidOperationException("Failed to deserialize variation set.");

        var set = new SoundVariationSet(dto.Id)
        {
            Name = dto.Name ?? string.Empty
        };

        if (dto.Variations != null)
        {
            foreach (var varDto in dto.Variations)
            {
                var variation = new SoundVariation(varDto.Id)
                {
                    Name = varDto.Name ?? string.Empty,
                    Description = varDto.Description ?? string.Empty,
                    Slot = varDto.Slot
                };

                if (varDto.Messages != null)
                {
                    foreach (var msgDto in varDto.Messages)
                    {
                        var msgType = Enum.TryParse<MidiMessageType>(msgDto.Type, out var type)
                            ? type
                            : MidiMessageType.ControlChange;

                        variation.AddMessage(new MidiMessage(msgType, msgDto.Channel, msgDto.Data1, msgDto.Data2));
                    }
                }

                set.AddVariation(variation);
            }
        }

        return set;
    }


    /// <summary>
    /// Loads a variation set from a file.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>The loaded variation set.</returns>
    public static SoundVariationSet LoadFromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));

        var json = File.ReadAllText(path);
        return FromJson(json);
    }


    /// <summary>
    /// Saves this variation set to a file.
    /// </summary>
    /// <param name="path">The file path.</param>
    public void SaveToFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = ToJson();
        File.WriteAllText(path, json);
    }


    /// <inheritdoc/>
    public override string ToString()
    {
        return $"{Name} ({_variations.Count} variations)";
    }


    #region DTO Classes for JSON Serialization

    private class VariationSetDto
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public List<VariationDto>? Variations { get; set; }
    }

    private class VariationDto
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public int Slot { get; set; }
        public List<MessageDto>? Messages { get; set; }
    }

    private class MessageDto
    {
        public string? Type { get; set; }
        public int Channel { get; set; }
        public int Data1 { get; set; }
        public int Data2 { get; set; }
    }

    #endregion
}


/// <summary>
/// Event arguments for variation change events.
/// </summary>
public class VariationChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the previous variation (may be null).
    /// </summary>
    public SoundVariation? Previous { get; }

    /// <summary>
    /// Gets the new active variation.
    /// </summary>
    public SoundVariation Current { get; }


    /// <summary>
    /// Creates new variation changed event args.
    /// </summary>
    /// <param name="previous">The previous variation.</param>
    /// <param name="current">The new active variation.</param>
    public VariationChangedEventArgs(SoundVariation? previous, SoundVariation current)
    {
        Previous = previous;
        Current = current;
    }
}
