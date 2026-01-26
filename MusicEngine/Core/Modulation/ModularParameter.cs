// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: VCV Rack-style modular parameter system. Every parameter can be a modulation source or destination.

using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicEngine.Core.Modulation;

/// <summary>
/// A parameter that can be modulated by any source.
/// Like VCV Rack, every parameter can receive modulation from multiple sources.
/// </summary>
public class ModularParameter
{
    private readonly List<ModulationConnection> _modulationSources = new();
    private readonly object _lock = new();
    private double _baseValue;
    private double _modulatedValue;
    private double _lastModulationSum;

    /// <summary>
    /// Unique identifier for this parameter.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Display name for the parameter.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Description of what this parameter controls.
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// The parameter type (for grouping in UI).
    /// </summary>
    public ParameterType Type { get; set; } = ParameterType.Generic;

    /// <summary>
    /// Minimum value for this parameter.
    /// </summary>
    public double MinValue { get; set; } = 0;

    /// <summary>
    /// Maximum value for this parameter.
    /// </summary>
    public double MaxValue { get; set; } = 1;

    /// <summary>
    /// Default value for this parameter.
    /// </summary>
    public double DefaultValue { get; set; } = 0;

    /// <summary>
    /// Step size for discrete values (0 for continuous).
    /// </summary>
    public double Step { get; set; } = 0;

    /// <summary>
    /// Unit label (e.g., "Hz", "dB", "ms", "%").
    /// </summary>
    public string Unit { get; set; } = "";

    /// <summary>
    /// Whether this parameter responds to audio rate modulation.
    /// </summary>
    public bool AudioRate { get; set; } = false;

    /// <summary>
    /// The base (unmodulated) value.
    /// </summary>
    public double BaseValue
    {
        get => _baseValue;
        set
        {
            _baseValue = Math.Clamp(value, MinValue, MaxValue);
            RecalculateModulatedValue();
        }
    }

    /// <summary>
    /// The current value including all modulation.
    /// </summary>
    public double Value
    {
        get => _modulatedValue;
        set => BaseValue = value; // Setting Value sets the BaseValue
    }

    /// <summary>
    /// Normalized base value (0-1).
    /// </summary>
    public double NormalizedValue
    {
        get => (BaseValue - MinValue) / (MaxValue - MinValue);
        set => BaseValue = MinValue + value * (MaxValue - MinValue);
    }

    /// <summary>
    /// The sum of all modulation amounts.
    /// </summary>
    public double ModulationAmount => _lastModulationSum;

    /// <summary>
    /// All modulation sources connected to this parameter.
    /// </summary>
    public IReadOnlyList<ModulationConnection> ModulationSources
    {
        get
        {
            lock (_lock) return _modulationSources.ToList();
        }
    }

    /// <summary>
    /// Event fired when the value changes.
    /// </summary>
    public event EventHandler<ParameterValueChangedEventArgs>? ValueChanged;

    /// <summary>
    /// Event fired when modulation is added/removed.
    /// </summary>
    public event EventHandler? ModulationChanged;

    public ModularParameter(string id, string name, double minValue = 0, double maxValue = 1, double defaultValue = 0)
    {
        Id = id;
        Name = name;
        MinValue = minValue;
        MaxValue = maxValue;
        DefaultValue = defaultValue;
        _baseValue = defaultValue;
        _modulatedValue = defaultValue;
    }

    /// <summary>
    /// Adds a modulation source to this parameter.
    /// </summary>
    public ModulationConnection AddModulation(IModulationSource source, double amount = 1.0)
    {
        lock (_lock)
        {
            var connection = new ModulationConnection(source, this, amount);
            _modulationSources.Add(connection);
            ModulationChanged?.Invoke(this, EventArgs.Empty);
            return connection;
        }
    }

    /// <summary>
    /// Removes a modulation connection.
    /// </summary>
    public bool RemoveModulation(ModulationConnection connection)
    {
        lock (_lock)
        {
            var removed = _modulationSources.Remove(connection);
            if (removed)
            {
                RecalculateModulatedValue();
                ModulationChanged?.Invoke(this, EventArgs.Empty);
            }
            return removed;
        }
    }

    /// <summary>
    /// Removes all modulation from a specific source.
    /// </summary>
    public void RemoveModulationSource(IModulationSource source)
    {
        lock (_lock)
        {
            _modulationSources.RemoveAll(c => c.Source == source);
            RecalculateModulatedValue();
            ModulationChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Clears all modulation from this parameter.
    /// </summary>
    public void ClearModulation()
    {
        lock (_lock)
        {
            _modulationSources.Clear();
            RecalculateModulatedValue();
            ModulationChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Processes modulation and returns the modulated value.
    /// Call this at sample rate for audio-rate modulation.
    /// </summary>
    public double Process()
    {
        RecalculateModulatedValue();
        return _modulatedValue;
    }

    /// <summary>
    /// Gets the modulated value for a specific sample offset.
    /// Used for audio-rate modulation.
    /// </summary>
    public double GetValueAtSample(int sampleOffset)
    {
        if (!AudioRate || _modulationSources.Count == 0)
        {
            return _modulatedValue;
        }

        double modSum = 0;
        lock (_lock)
        {
            foreach (var connection in _modulationSources)
            {
                if (connection.Enabled)
                {
                    var modValue = connection.Source.GetValueAtSample(sampleOffset);
                    modSum += modValue * connection.Amount * (MaxValue - MinValue);
                }
            }
        }

        return Math.Clamp(_baseValue + modSum, MinValue, MaxValue);
    }

    private void RecalculateModulatedValue()
    {
        double modSum = 0;

        lock (_lock)
        {
            foreach (var connection in _modulationSources)
            {
                if (connection.Enabled)
                {
                    var modValue = connection.Source.GetValue();
                    modSum += modValue * connection.Amount * (MaxValue - MinValue);
                }
            }
        }

        var oldValue = _modulatedValue;
        _lastModulationSum = modSum;
        _modulatedValue = Math.Clamp(_baseValue + modSum, MinValue, MaxValue);

        if (Math.Abs(oldValue - _modulatedValue) > 0.0001)
        {
            ValueChanged?.Invoke(this, new ParameterValueChangedEventArgs(Id, oldValue, _modulatedValue));
        }
    }

    /// <summary>
    /// Resets to default value and clears modulation.
    /// </summary>
    public void Reset()
    {
        ClearModulation();
        BaseValue = DefaultValue;
    }

    public override string ToString() => $"{Name}: {Value:F3} {Unit}";
}

/// <summary>
/// Types of parameters for categorization.
/// </summary>
public enum ParameterType
{
    Generic,
    Pitch,
    Amplitude,
    Filter,
    Time,
    Modulation,
    Effect,
    Oscillator,
    Envelope,
    LFO,
    Mixer,
    Spatial
}

/// <summary>
/// Event args for parameter value changes.
/// </summary>
public class ParameterValueChangedEventArgs : EventArgs
{
    public string ParameterId { get; }
    public double OldValue { get; }
    public double NewValue { get; }

    public ParameterValueChangedEventArgs(string parameterId, double oldValue, double newValue)
    {
        ParameterId = parameterId;
        OldValue = oldValue;
        NewValue = newValue;
    }
}

/// <summary>
/// A connection between a modulation source and destination parameter.
/// </summary>
public class ModulationConnection
{
    /// <summary>
    /// The modulation source.
    /// </summary>
    public IModulationSource Source { get; }

    /// <summary>
    /// The destination parameter.
    /// </summary>
    public ModularParameter Destination { get; }

    /// <summary>
    /// Amount of modulation (-1 to 1 for bipolar, 0 to 1 for unipolar).
    /// This scales how much the source affects the destination.
    /// </summary>
    public double Amount { get; set; }

    /// <summary>
    /// Whether this connection is currently enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Optional label for this connection.
    /// </summary>
    public string Label { get; set; } = "";

    public ModulationConnection(IModulationSource source, ModularParameter destination, double amount = 1.0)
    {
        Source = source;
        Destination = destination;
        Amount = amount;
    }
}

/// <summary>
/// Interface for anything that can be a modulation source.
/// </summary>
public interface IModulationSource
{
    /// <summary>
    /// Unique identifier for this source.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Display name for this source.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Whether this source outputs bipolar (-1 to 1) or unipolar (0 to 1).
    /// </summary>
    bool IsBipolar { get; }

    /// <summary>
    /// Gets the current output value (normalized -1 to 1 or 0 to 1).
    /// </summary>
    double GetValue();

    /// <summary>
    /// Gets the output value at a specific sample offset.
    /// Used for audio-rate modulation.
    /// </summary>
    double GetValueAtSample(int sampleOffset);
}

/// <summary>
/// A modulation source that wraps a parameter value.
/// Allows any parameter to be used as a modulation source.
/// </summary>
public class ParameterModulationSource : IModulationSource
{
    private readonly ModularParameter _parameter;

    public string Id => $"param:{_parameter.Id}";
    public string Name => _parameter.Name;
    public bool IsBipolar => _parameter.MinValue < 0;

    public ParameterModulationSource(ModularParameter parameter)
    {
        _parameter = parameter;
    }

    public double GetValue() => _parameter.NormalizedValue;
    public double GetValueAtSample(int sampleOffset) => _parameter.NormalizedValue;
}

/// <summary>
/// A constant value modulation source.
/// </summary>
public class ConstantModulationSource : IModulationSource
{
    private double _value;

    public string Id { get; }
    public string Name { get; set; }
    public bool IsBipolar { get; set; } = false;

    public double Value
    {
        get => _value;
        set => _value = Math.Clamp(value, IsBipolar ? -1 : 0, 1);
    }

    public ConstantModulationSource(string id, string name, double value = 0)
    {
        Id = id;
        Name = name;
        Value = value;
    }

    public double GetValue() => _value;
    public double GetValueAtSample(int sampleOffset) => _value;
}

/// <summary>
/// Envelope as modulation source.
/// </summary>
public class EnvelopeModulationSource : IModulationSource
{
    private readonly Envelope _envelope;

    public string Id { get; }
    public string Name { get; set; }
    public bool IsBipolar => false; // Envelopes are unipolar

    public EnvelopeModulationSource(string id, string name, Envelope envelope)
    {
        Id = id;
        Name = name;
        _envelope = envelope;
    }

    public double GetValue() => _envelope.Value;
    public double GetValueAtSample(int sampleOffset) => _envelope.Value;
}

/// <summary>
/// LFO as modulation source.
/// </summary>
public class LFOModulationSource : IModulationSource
{
    private readonly LFO _lfo;
    private readonly int _sampleRate;

    public string Id { get; }
    public string Name { get; set; }
    public bool IsBipolar => true; // LFOs are typically bipolar

    public LFOModulationSource(string id, string name, LFO lfo, int sampleRate)
    {
        Id = id;
        Name = name;
        _lfo = lfo;
        _sampleRate = sampleRate;
    }

    public double GetValue() => _lfo.GetValue(_sampleRate);
    public double GetValueAtSample(int sampleOffset) => _lfo.GetValue(_sampleRate);
}

/// <summary>
/// Random/noise modulation source.
/// </summary>
public class NoiseModulationSource : IModulationSource
{
    private readonly Random _random = new();
    private double _currentValue;
    private double _targetValue;
    private double _smoothing;
    private int _holdSamples;
    private int _sampleCounter;

    public string Id { get; }
    public string Name { get; set; }
    public bool IsBipolar { get; set; } = true;

    /// <summary>
    /// Rate of change (how often new random value is generated).
    /// </summary>
    public double Rate { get; set; } = 1.0; // Hz

    /// <summary>
    /// Smoothing factor (0 = sample & hold, 1 = smooth interpolation).
    /// </summary>
    public double Smoothing
    {
        get => _smoothing;
        set => _smoothing = Math.Clamp(value, 0, 1);
    }

    public NoiseModulationSource(string id, string name, double rate = 1.0, double smoothing = 0.5)
    {
        Id = id;
        Name = name;
        Rate = rate;
        _smoothing = smoothing;
        _targetValue = GenerateRandomValue();
        _currentValue = _targetValue;
    }

    public double GetValue() => _currentValue;

    public double GetValueAtSample(int sampleOffset)
    {
        _sampleCounter++;
        if (_sampleCounter >= _holdSamples)
        {
            _sampleCounter = 0;
            _targetValue = GenerateRandomValue();
        }

        // Smooth interpolation
        _currentValue += (_targetValue - _currentValue) * _smoothing * 0.1;
        return _currentValue;
    }

    public void SetSampleRate(int sampleRate)
    {
        _holdSamples = Math.Max(1, (int)(sampleRate / Rate));
    }

    private double GenerateRandomValue()
    {
        return IsBipolar
            ? _random.NextDouble() * 2 - 1
            : _random.NextDouble();
    }
}

/// <summary>
/// MIDI CC as modulation source.
/// </summary>
public class MidiCCModulationSource : IModulationSource
{
    private double _value;

    public string Id { get; }
    public string Name { get; set; }
    public bool IsBipolar { get; set; } = false;

    public int CCNumber { get; }
    public int Channel { get; set; } = -1; // -1 = all channels

    public MidiCCModulationSource(string id, int ccNumber, string? name = null)
    {
        Id = id;
        CCNumber = ccNumber;
        Name = name ?? $"CC {ccNumber}";
    }

    public void SetValue(int ccValue)
    {
        _value = ccValue / 127.0;
    }

    public double GetValue() => _value;
    public double GetValueAtSample(int sampleOffset) => _value;
}

/// <summary>
/// Velocity as modulation source.
/// </summary>
public class VelocityModulationSource : IModulationSource
{
    private double _value;

    public string Id => "velocity";
    public string Name => "Velocity";
    public bool IsBipolar => false;

    public void SetVelocity(int velocity)
    {
        _value = velocity / 127.0;
    }

    public double GetValue() => _value;
    public double GetValueAtSample(int sampleOffset) => _value;
}

/// <summary>
/// Note/pitch as modulation source (for keyboard tracking).
/// </summary>
public class KeyTrackModulationSource : IModulationSource
{
    private double _value;

    public string Id => "keytrack";
    public string Name => "Key Track";
    public bool IsBipolar => true; // Centered around middle C

    /// <summary>
    /// Center note (default: middle C = 60).
    /// </summary>
    public int CenterNote { get; set; } = 60;

    /// <summary>
    /// Scaling factor (semitones per unit).
    /// </summary>
    public double Scale { get; set; } = 12; // 1 octave = 1.0 output

    public void SetNote(int note)
    {
        _value = (note - CenterNote) / Scale;
    }

    public double GetValue() => _value;
    public double GetValueAtSample(int sampleOffset) => _value;
}
