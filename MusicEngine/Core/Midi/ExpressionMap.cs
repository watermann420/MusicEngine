//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Expression map for managing instrument articulations.


using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace MusicEngine.Core.Midi;


/// <summary>
/// Represents an expression map that manages articulations for a virtual instrument.
/// Expression maps allow switching between different playing techniques (articulations)
/// using keyswitches, program changes, or control changes.
/// </summary>
public class ExpressionMap
{
    private readonly List<Articulation> _articulations = new();
    private Articulation? _currentArticulation;


    /// <summary>
    /// Gets the unique identifier for this expression map.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets or sets the name of this expression map.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the instrument name this expression map is designed for.
    /// </summary>
    public string InstrumentName { get; set; } = string.Empty;

    /// <summary>
    /// Gets the list of articulations in this expression map.
    /// </summary>
    public IReadOnlyList<Articulation> Articulations => _articulations;

    /// <summary>
    /// Gets the currently active articulation.
    /// </summary>
    public Articulation? CurrentArticulation => _currentArticulation;


    /// <summary>
    /// Event raised when the current articulation changes.
    /// </summary>
    public event EventHandler<ArticulationChangedEventArgs>? ArticulationChanged;


    /// <summary>
    /// Creates a new expression map with a unique ID.
    /// </summary>
    public ExpressionMap()
    {
        Id = Guid.NewGuid();
    }


    /// <summary>
    /// Creates a new expression map with a specific ID.
    /// </summary>
    /// <param name="id">The unique identifier.</param>
    public ExpressionMap(Guid id)
    {
        Id = id;
    }


    /// <summary>
    /// Creates a new expression map with the specified name.
    /// </summary>
    /// <param name="name">The name of the expression map.</param>
    /// <param name="instrumentName">The name of the target instrument.</param>
    public ExpressionMap(string name, string instrumentName = "")
    {
        Id = Guid.NewGuid();
        Name = name;
        InstrumentName = instrumentName;
    }


    /// <summary>
    /// Adds an articulation to this expression map.
    /// </summary>
    /// <param name="articulation">The articulation to add.</param>
    public void AddArticulation(Articulation articulation)
    {
        if (articulation == null)
            throw new ArgumentNullException(nameof(articulation));

        if (!_articulations.Contains(articulation))
        {
            _articulations.Add(articulation);

            // Set as current if this is the first articulation
            if (_currentArticulation == null)
            {
                _currentArticulation = articulation;
            }
        }
    }


    /// <summary>
    /// Removes an articulation from this expression map.
    /// </summary>
    /// <param name="articulation">The articulation to remove.</param>
    public void RemoveArticulation(Articulation articulation)
    {
        if (articulation == null)
            throw new ArgumentNullException(nameof(articulation));

        _articulations.Remove(articulation);

        // If the removed articulation was current, switch to first available
        if (_currentArticulation == articulation)
        {
            _currentArticulation = _articulations.Count > 0 ? _articulations[0] : null;
        }
    }


    /// <summary>
    /// Switches to the specified articulation.
    /// </summary>
    /// <param name="articulation">The articulation to switch to.</param>
    public void SwitchTo(Articulation articulation)
    {
        if (articulation == null)
            throw new ArgumentNullException(nameof(articulation));

        if (!_articulations.Contains(articulation))
            throw new ArgumentException("Articulation is not part of this expression map.", nameof(articulation));

        var previous = _currentArticulation;
        _currentArticulation = articulation;

        ArticulationChanged?.Invoke(this, new ArticulationChangedEventArgs(previous, articulation));
    }


    /// <summary>
    /// Switches to an articulation by its keyswitch note.
    /// </summary>
    /// <param name="note">The MIDI note number of the keyswitch.</param>
    public void SwitchByKeyswitch(int note)
    {
        foreach (var articulation in _articulations)
        {
            if (articulation.KeyswitchNote == note)
            {
                SwitchTo(articulation);
                return;
            }
        }
    }


    /// <summary>
    /// Switches to an articulation by name.
    /// </summary>
    /// <param name="name">The name of the articulation.</param>
    public void SwitchByName(string name)
    {
        foreach (var articulation in _articulations)
        {
            if (string.Equals(articulation.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                SwitchTo(articulation);
                return;
            }
        }
    }


    /// <summary>
    /// Gets the MIDI messages needed to switch to the specified articulation.
    /// </summary>
    /// <param name="target">The target articulation.</param>
    /// <returns>Array of MIDI bytes representing the switch messages.</returns>
    public byte[] GetSwitchMessages(Articulation target)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));

        var messages = new List<byte>();

        // Add keyswitch note-on and note-off
        if (target.UsesKeyswitch)
        {
            // Note On: Status (0x90 for channel 0), Note, Velocity
            messages.Add(0x90);
            messages.Add((byte)target.KeyswitchNote);
            messages.Add(100); // Default velocity

            // Note Off: Status (0x80 for channel 0), Note, Velocity
            messages.Add(0x80);
            messages.Add((byte)target.KeyswitchNote);
            messages.Add(0);
        }

        // Add program change
        if (target.UsesProgramChange)
        {
            // Program Change: Status (0xC0 for channel 0), Program
            messages.Add(0xC0);
            messages.Add((byte)target.ProgramChange);
        }

        // Add control change
        if (target.UsesControlChange)
        {
            // Control Change: Status (0xB0 for channel 0), Controller, Value
            messages.Add(0xB0);
            messages.Add((byte)target.ControlChange);
            messages.Add((byte)target.ControlValue);
        }

        return messages.ToArray();
    }


    /// <summary>
    /// Serializes this expression map to JSON.
    /// </summary>
    /// <returns>JSON string representation.</returns>
    public string ToJson()
    {
        var dto = new ExpressionMapDto
        {
            Id = Id,
            Name = Name,
            InstrumentName = InstrumentName,
            Articulations = new List<ArticulationDto>()
        };

        foreach (var art in _articulations)
        {
            dto.Articulations.Add(new ArticulationDto
            {
                Id = art.Id,
                Name = art.Name,
                Type = art.Type.ToString(),
                KeyswitchNote = art.KeyswitchNote,
                ProgramChange = art.ProgramChange,
                ControlChange = art.ControlChange,
                ControlValue = art.ControlValue,
                DisplayColorArgb = art.DisplayColor.ToArgb()
            });
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        return JsonSerializer.Serialize(dto, options);
    }


    /// <summary>
    /// Deserializes an expression map from JSON.
    /// </summary>
    /// <param name="json">The JSON string.</param>
    /// <returns>The deserialized expression map.</returns>
    public static ExpressionMap FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("JSON string cannot be null or empty.", nameof(json));

        var dto = JsonSerializer.Deserialize<ExpressionMapDto>(json);

        if (dto == null)
            throw new InvalidOperationException("Failed to deserialize expression map.");

        var map = new ExpressionMap(dto.Id)
        {
            Name = dto.Name ?? string.Empty,
            InstrumentName = dto.InstrumentName ?? string.Empty
        };

        if (dto.Articulations != null)
        {
            foreach (var artDto in dto.Articulations)
            {
                var articulation = new Articulation(artDto.Id)
                {
                    Name = artDto.Name ?? string.Empty,
                    Type = Enum.TryParse<ArticulationType>(artDto.Type, out var type) ? type : ArticulationType.Sustain,
                    KeyswitchNote = artDto.KeyswitchNote,
                    ProgramChange = artDto.ProgramChange,
                    ControlChange = artDto.ControlChange,
                    ControlValue = artDto.ControlValue,
                    DisplayColor = System.Drawing.Color.FromArgb(artDto.DisplayColorArgb)
                };

                map.AddArticulation(articulation);
            }
        }

        return map;
    }


    /// <summary>
    /// Loads an expression map from a file.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>The loaded expression map.</returns>
    public static ExpressionMap LoadFromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));

        var json = File.ReadAllText(path);
        return FromJson(json);
    }


    /// <summary>
    /// Saves this expression map to a file.
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
        return $"{Name} ({_articulations.Count} articulations)";
    }


    #region DTO Classes for JSON Serialization

    private class ExpressionMapDto
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? InstrumentName { get; set; }
        public List<ArticulationDto>? Articulations { get; set; }
    }

    private class ArticulationDto
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? Type { get; set; }
        public int KeyswitchNote { get; set; }
        public int ProgramChange { get; set; }
        public int ControlChange { get; set; }
        public int ControlValue { get; set; }
        public int DisplayColorArgb { get; set; }
    }

    #endregion
}


/// <summary>
/// Event arguments for articulation change events.
/// </summary>
public class ArticulationChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the previous articulation (may be null).
    /// </summary>
    public Articulation? Previous { get; }

    /// <summary>
    /// Gets the new articulation.
    /// </summary>
    public Articulation Current { get; }


    /// <summary>
    /// Creates new articulation changed event args.
    /// </summary>
    /// <param name="previous">The previous articulation.</param>
    /// <param name="current">The new current articulation.</param>
    public ArticulationChangedEventArgs(Articulation? previous, Articulation current)
    {
        Previous = previous;
        Current = current;
    }
}
