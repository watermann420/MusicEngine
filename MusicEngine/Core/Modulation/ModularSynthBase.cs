// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Base class for modular synthesizers with VCV Rack-style parameter modulation.

using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.Wave;

namespace MusicEngine.Core.Modulation;

/// <summary>
/// Base class for modular synthesizers with full parameter modulation support.
/// Every parameter is a ModularParameter that can receive modulation from any source.
/// </summary>
public abstract class ModularSynthBase : ISynth
{
    protected readonly Dictionary<string, ModularParameter> _parameters = new();
    protected readonly Dictionary<string, IModulationSource> _modulationSources = new();
    protected readonly List<ModulationConnection> _connections = new();
    protected readonly object _lock = new();

    /// <summary>
    /// The synth name.
    /// </summary>
    public string Name { get; set; } = "ModularSynth";

    /// <summary>
    /// Audio format.
    /// </summary>
    public abstract WaveFormat WaveFormat { get; }

    /// <summary>
    /// All parameters exposed by this synth.
    /// </summary>
    public IReadOnlyDictionary<string, ModularParameter> Parameters => _parameters;

    /// <summary>
    /// All modulation sources available from this synth.
    /// </summary>
    public IReadOnlyDictionary<string, IModulationSource> ModulationSources => _modulationSources;

    /// <summary>
    /// All active modulation connections.
    /// </summary>
    public IReadOnlyList<ModulationConnection> Connections
    {
        get
        {
            lock (_lock) return _connections.ToList();
        }
    }

    /// <summary>
    /// Event fired when a parameter value changes.
    /// </summary>
    public event EventHandler<ParameterValueChangedEventArgs>? ParameterChanged;

    /// <summary>
    /// Event fired when modulation routing changes.
    /// </summary>
    public event EventHandler? RoutingChanged;

    #region Parameter Registration

    /// <summary>
    /// Registers a parameter with the synth.
    /// </summary>
    protected ModularParameter RegisterParameter(string id, string name, double minValue, double maxValue, double defaultValue,
        ParameterType type = ParameterType.Generic, string unit = "", string description = "")
    {
        var param = new ModularParameter(id, name, minValue, maxValue, defaultValue)
        {
            Type = type,
            Unit = unit,
            Description = description
        };

        param.ValueChanged += (s, e) => ParameterChanged?.Invoke(this, e);
        _parameters[id] = param;
        return param;
    }

    /// <summary>
    /// Registers a modulation source.
    /// </summary>
    protected void RegisterModulationSource(IModulationSource source)
    {
        _modulationSources[source.Id] = source;
    }

    #endregion

    #region Parameter Access

    /// <summary>
    /// Gets a parameter by ID.
    /// </summary>
    public ModularParameter? GetParameter(string id)
    {
        return _parameters.TryGetValue(id, out var param) ? param : null;
    }

    /// <summary>
    /// Sets a parameter value by ID.
    /// </summary>
    public void SetParameter(string id, double value)
    {
        if (_parameters.TryGetValue(id, out var param))
        {
            param.Value = value;
        }
    }

    /// <summary>
    /// Gets all parameters of a specific type.
    /// </summary>
    public IEnumerable<ModularParameter> GetParametersByType(ParameterType type)
    {
        return _parameters.Values.Where(p => p.Type == type);
    }

    /// <summary>
    /// Sets parameter by name (legacy ISynth interface).
    /// </summary>
    public virtual void SetParameter(string name, float value)
    {
        // Try exact match first
        if (_parameters.TryGetValue(name, out var param))
        {
            param.Value = value;
            return;
        }

        // Try case-insensitive match
        var matchingParam = _parameters.Values.FirstOrDefault(p =>
            p.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
            p.Id.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (matchingParam != null)
        {
            matchingParam.Value = value;
        }
    }

    #endregion

    #region Modulation Routing

    /// <summary>
    /// Connects a modulation source to a destination parameter.
    /// </summary>
    public ModulationConnection Connect(string sourceId, string destParamId, double amount = 1.0)
    {
        if (!_modulationSources.TryGetValue(sourceId, out var source))
        {
            throw new ArgumentException($"Unknown modulation source: {sourceId}");
        }

        if (!_parameters.TryGetValue(destParamId, out var dest))
        {
            throw new ArgumentException($"Unknown parameter: {destParamId}");
        }

        return Connect(source, dest, amount);
    }

    /// <summary>
    /// Connects a modulation source to a destination parameter.
    /// </summary>
    public ModulationConnection Connect(IModulationSource source, ModularParameter dest, double amount = 1.0)
    {
        lock (_lock)
        {
            var connection = dest.AddModulation(source, amount);
            _connections.Add(connection);
            RoutingChanged?.Invoke(this, EventArgs.Empty);
            return connection;
        }
    }

    /// <summary>
    /// Disconnects a modulation connection.
    /// </summary>
    public bool Disconnect(ModulationConnection connection)
    {
        lock (_lock)
        {
            if (_connections.Remove(connection))
            {
                connection.Destination.RemoveModulation(connection);
                RoutingChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Clears all modulation connections.
    /// </summary>
    public void ClearAllModulation()
    {
        lock (_lock)
        {
            foreach (var param in _parameters.Values)
            {
                param.ClearModulation();
            }
            _connections.Clear();
            RoutingChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gets all connections from a specific source.
    /// </summary>
    public IEnumerable<ModulationConnection> GetConnectionsFrom(string sourceId)
    {
        lock (_lock)
        {
            return _connections.Where(c => c.Source.Id == sourceId).ToList();
        }
    }

    /// <summary>
    /// Gets all connections to a specific parameter.
    /// </summary>
    public IEnumerable<ModulationConnection> GetConnectionsTo(string paramId)
    {
        lock (_lock)
        {
            return _connections.Where(c => c.Destination.Id == paramId).ToList();
        }
    }

    #endregion

    #region Preset System

    /// <summary>
    /// Saves the current state as a preset.
    /// </summary>
    public virtual ModularPreset SavePreset(string name)
    {
        var preset = new ModularPreset
        {
            Name = name,
            SynthType = GetType().FullName ?? GetType().Name
        };

        foreach (var param in _parameters.Values)
        {
            preset.ParameterValues[param.Id] = param.BaseValue;
        }

        foreach (var connection in _connections)
        {
            preset.Connections.Add(new PresetConnection
            {
                SourceId = connection.Source.Id,
                DestinationId = connection.Destination.Id,
                Amount = connection.Amount,
                Enabled = connection.Enabled
            });
        }

        return preset;
    }

    /// <summary>
    /// Loads a preset.
    /// </summary>
    public virtual void LoadPreset(ModularPreset preset)
    {
        // Clear existing modulation
        ClearAllModulation();

        // Set parameter values
        foreach (var kvp in preset.ParameterValues)
        {
            if (_parameters.TryGetValue(kvp.Key, out var param))
            {
                param.BaseValue = kvp.Value;
            }
        }

        // Restore modulation connections
        foreach (var conn in preset.Connections)
        {
            if (_modulationSources.TryGetValue(conn.SourceId, out var source) &&
                _parameters.TryGetValue(conn.DestinationId, out var dest))
            {
                var connection = Connect(source, dest, conn.Amount);
                connection.Enabled = conn.Enabled;
            }
        }
    }

    #endregion

    #region Processing

    /// <summary>
    /// Processes all modulation for the current sample.
    /// Call this at the start of each audio buffer processing.
    /// </summary>
    protected void ProcessModulation()
    {
        foreach (var param in _parameters.Values)
        {
            param.Process();
        }
    }

    /// <summary>
    /// Gets a parameter's modulated value at a specific sample offset.
    /// </summary>
    protected double GetParameterValue(string id, int sampleOffset = 0)
    {
        if (_parameters.TryGetValue(id, out var param))
        {
            return param.GetValueAtSample(sampleOffset);
        }
        return 0;
    }

    #endregion

    #region ISynth Implementation

    public abstract void NoteOn(int note, int velocity);
    public abstract void NoteOff(int note);
    public abstract void AllNotesOff();
    public abstract int Read(float[] buffer, int offset, int count);

    #endregion
}

/// <summary>
/// Preset data for a modular synth.
/// </summary>
public class ModularPreset
{
    public string Name { get; set; } = "Init";
    public string SynthType { get; set; } = "";
    public Dictionary<string, double> ParameterValues { get; } = new();
    public List<PresetConnection> Connections { get; } = new();
    public string? Description { get; set; }
    public string? Author { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public List<string> Tags { get; } = new();
}

/// <summary>
/// Serializable modulation connection for presets.
/// </summary>
public class PresetConnection
{
    public string SourceId { get; set; } = "";
    public string DestinationId { get; set; } = "";
    public double Amount { get; set; } = 1.0;
    public bool Enabled { get; set; } = true;
}
