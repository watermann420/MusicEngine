// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: VCV Rack-style modular effect base class. All parameters can be modulated and patched.

using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.Wave;
using MusicEngine.Infrastructure.Memory;

namespace MusicEngine.Core.Modulation;

/// <summary>
/// Base class for modular effects with VCV Rack-style parameter modulation.
/// Every parameter can receive CV input and be modulated by any source.
/// Supports audio-rate modulation for FM, AM, and other real-time effects.
/// </summary>
public abstract class ModularEffectBase : IEffect, IModulationSource
{
    private readonly ISampleProvider _source;
    private readonly Dictionary<string, ModularParameter> _parameters = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IModulationSource> _modulationSources = new();
    private readonly Dictionary<string, AudioInput> _audioInputs = new();
    private readonly Dictionary<string, AudioOutput> _audioOutputs = new();
    private readonly List<ModulationConnection> _connections = new();
    private readonly object _lock = new();
    private readonly IAudioBufferPool? _bufferPool;

    private float _mix = 1.0f;
    private bool _enabled = true;
    private float[] _sourceBuffer = Array.Empty<float>();
    private double _outputValue; // For use as modulation source

    /// <summary>
    /// Creates a new modular effect.
    /// </summary>
    protected ModularEffectBase(ISampleProvider source, string name)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        WaveFormat = source.WaveFormat;

        // Register main input/output
        RegisterAudioInput("main", "Main Input");
        RegisterAudioOutput("main", "Main Output");
    }

    /// <summary>
    /// Creates a new modular effect with buffer pool.
    /// </summary>
    protected ModularEffectBase(ISampleProvider source, string name, IAudioBufferPool? bufferPool)
        : this(source, name)
    {
        _bufferPool = bufferPool;
    }

    #region IEffect Implementation

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public WaveFormat WaveFormat { get; }

    /// <inheritdoc />
    public float Mix
    {
        get => _mix;
        set => _mix = Math.Clamp(value, 0f, 1f);
    }

    /// <inheritdoc />
    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    /// <inheritdoc />
    public virtual void SetParameter(string name, float value)
    {
        if (_parameters.TryGetValue(name, out var param))
        {
            param.Value = value;
        }
    }

    /// <inheritdoc />
    public virtual float GetParameter(string name)
    {
        return _parameters.TryGetValue(name, out var param) ? (float)param.Value : 0f;
    }

    #endregion

    #region IModulationSource Implementation

    /// <summary>
    /// Unique ID for this effect as a modulation source.
    /// </summary>
    public string Id => $"effect:{Name}";

    /// <summary>
    /// Whether output is bipolar (-1 to 1) or unipolar (0 to 1).
    /// </summary>
    public virtual bool IsBipolar => true;

    /// <summary>
    /// Gets the current output value for modulation.
    /// </summary>
    public double GetValue() => _outputValue;

    /// <summary>
    /// Gets the output value at a specific sample offset.
    /// </summary>
    public double GetValueAtSample(int sampleOffset) => _outputValue;

    /// <summary>
    /// Sets the output value (for follower effects, envelope followers, etc.)
    /// </summary>
    protected void SetOutputValue(double value)
    {
        _outputValue = Math.Clamp(value, IsBipolar ? -1 : 0, 1);
    }

    #endregion

    #region Modular Parameter System

    /// <summary>
    /// All modular parameters exposed by this effect.
    /// </summary>
    public IReadOnlyDictionary<string, ModularParameter> Parameters => _parameters;

    /// <summary>
    /// All modulation sources available from this effect.
    /// </summary>
    public IReadOnlyDictionary<string, IModulationSource> ModulationSources => _modulationSources;

    /// <summary>
    /// All audio inputs.
    /// </summary>
    public IReadOnlyDictionary<string, AudioInput> AudioInputs => _audioInputs;

    /// <summary>
    /// All audio outputs.
    /// </summary>
    public IReadOnlyDictionary<string, AudioOutput> AudioOutputs => _audioOutputs;

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
    /// Registers a modular parameter.
    /// </summary>
    protected ModularParameter RegisterModularParameter(string id, string name, double minValue, double maxValue,
        double defaultValue, ParameterType type = ParameterType.Generic, string unit = "", bool audioRate = false)
    {
        var param = new ModularParameter(id, name, minValue, maxValue, defaultValue)
        {
            Type = type,
            Unit = unit,
            AudioRate = audioRate
        };

        _parameters[id] = param;

        // Also register as modulation source (any parameter can modulate others)
        var paramSource = new ParameterModulationSource(param);
        _modulationSources[$"param:{id}"] = paramSource;

        return param;
    }

    /// <summary>
    /// Registers a modulation source.
    /// </summary>
    protected void RegisterModulationSource(IModulationSource source)
    {
        _modulationSources[source.Id] = source;
    }

    /// <summary>
    /// Registers an audio input port.
    /// </summary>
    protected AudioInput RegisterAudioInput(string id, string name)
    {
        var input = new AudioInput(id, name);
        _audioInputs[id] = input;
        return input;
    }

    /// <summary>
    /// Registers an audio output port.
    /// </summary>
    protected AudioOutput RegisterAudioOutput(string id, string name)
    {
        var output = new AudioOutput(id, name);
        _audioOutputs[id] = output;
        return output;
    }

    /// <summary>
    /// Gets a parameter by ID.
    /// </summary>
    public ModularParameter? GetModularParameter(string id)
    {
        return _parameters.TryGetValue(id, out var param) ? param : null;
    }

    /// <summary>
    /// Gets the modulated value of a parameter.
    /// </summary>
    protected double GetModulatedValue(string id, int sampleOffset = 0)
    {
        if (_parameters.TryGetValue(id, out var param))
        {
            return param.GetValueAtSample(sampleOffset);
        }
        return 0;
    }

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
        }
    }

    #endregion

    #region Audio Processing

    /// <summary>
    /// The audio source provider.
    /// </summary>
    protected ISampleProvider Source => _source;

    /// <summary>
    /// Number of channels.
    /// </summary>
    protected int Channels => WaveFormat.Channels;

    /// <summary>
    /// Sample rate.
    /// </summary>
    protected int SampleRate => WaveFormat.SampleRate;

    /// <inheritdoc />
    public int Read(float[] buffer, int offset, int count)
    {
        RentedBuffer<float>? rentedBuffer = null;
        float[] sourceBuffer;

        if (_bufferPool != null)
        {
            rentedBuffer = _bufferPool.Rent(count);
            sourceBuffer = rentedBuffer.Value.Array;
        }
        else
        {
            if (_sourceBuffer.Length < count)
            {
                _sourceBuffer = new float[count];
            }
            sourceBuffer = _sourceBuffer;
        }

        int samplesRead = _source.Read(sourceBuffer, 0, count);

        if (samplesRead == 0)
        {
            rentedBuffer?.Dispose();
            return 0;
        }

        if (!_enabled)
        {
            Array.Copy(sourceBuffer, 0, buffer, offset, samplesRead);
            rentedBuffer?.Dispose();
            return samplesRead;
        }

        // Process modulation for all parameters
        ProcessAllModulation();

        // Process the audio with modulated parameters
        ProcessBuffer(sourceBuffer, buffer, offset, samplesRead);

        // Apply dry/wet mix
        if (_mix < 1.0f)
        {
            float dry = 1.0f - _mix;
            for (int i = 0; i < samplesRead; i++)
            {
                buffer[offset + i] = (sourceBuffer[i] * dry) + (buffer[offset + i] * _mix);
            }
        }

        rentedBuffer?.Dispose();
        return samplesRead;
    }

    /// <summary>
    /// Processes all parameter modulation.
    /// </summary>
    protected void ProcessAllModulation()
    {
        foreach (var param in _parameters.Values)
        {
            param.Process();
        }
    }

    /// <summary>
    /// Processes a buffer of audio samples with modulated parameters.
    /// Override this to implement the effect.
    /// </summary>
    protected virtual void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        for (int i = 0; i < count; i++)
        {
            destBuffer[offset + i] = ProcessSample(sourceBuffer[i], i % Channels, i);
        }
    }

    /// <summary>
    /// Processes a single sample with modulated parameters.
    /// Override for simple per-sample effects.
    /// </summary>
    /// <param name="sample">Input sample</param>
    /// <param name="channel">Channel index</param>
    /// <param name="sampleIndex">Sample index in buffer (for audio-rate modulation)</param>
    protected virtual float ProcessSample(float sample, int channel, int sampleIndex)
    {
        return sample;
    }

    #endregion
}

/// <summary>
/// Audio input port for modular effects.
/// </summary>
public class AudioInput
{
    private float[] _buffer = Array.Empty<float>();
    private int _bufferSize;
    private bool _connected;

    public string Id { get; }
    public string Name { get; set; }
    public bool IsConnected => _connected;

    public AudioInput(string id, string name)
    {
        Id = id;
        Name = name;
    }

    /// <summary>
    /// Connects an audio source to this input.
    /// </summary>
    public void Connect(float[] buffer, int size)
    {
        _buffer = buffer;
        _bufferSize = size;
        _connected = true;
    }

    /// <summary>
    /// Disconnects the audio source.
    /// </summary>
    public void Disconnect()
    {
        _buffer = Array.Empty<float>();
        _bufferSize = 0;
        _connected = false;
    }

    /// <summary>
    /// Gets a sample from the input.
    /// </summary>
    public float GetSample(int index)
    {
        if (!_connected || index >= _bufferSize) return 0f;
        return _buffer[index];
    }

    /// <summary>
    /// Gets the full buffer.
    /// </summary>
    public ReadOnlySpan<float> GetBuffer() => new(_buffer, 0, _bufferSize);
}

/// <summary>
/// Audio output port for modular effects.
/// </summary>
public class AudioOutput
{
    private float[] _buffer = Array.Empty<float>();
    private int _bufferSize;

    public string Id { get; }
    public string Name { get; set; }

    public AudioOutput(string id, string name)
    {
        Id = id;
        Name = name;
    }

    /// <summary>
    /// Prepares the output buffer.
    /// </summary>
    public void Prepare(int size)
    {
        if (_buffer.Length < size)
        {
            _buffer = new float[size];
        }
        _bufferSize = size;
    }

    /// <summary>
    /// Sets a sample in the output.
    /// </summary>
    public void SetSample(int index, float value)
    {
        if (index < _bufferSize)
        {
            _buffer[index] = value;
        }
    }

    /// <summary>
    /// Gets the output buffer.
    /// </summary>
    public ReadOnlySpan<float> GetBuffer() => new(_buffer, 0, _bufferSize);

    /// <summary>
    /// Copies output to destination.
    /// </summary>
    public void CopyTo(float[] dest, int offset)
    {
        Array.Copy(_buffer, 0, dest, offset, _bufferSize);
    }
}

/// <summary>
/// Factory for creating modular effects.
/// </summary>
public static class ModularEffectFactory
{
    private static readonly Dictionary<string, Func<ISampleProvider, ModularEffectBase>> _factories = new();

    /// <summary>
    /// Registers an effect type.
    /// </summary>
    public static void Register<T>(string id) where T : ModularEffectBase
    {
        _factories[id] = source => (ModularEffectBase)Activator.CreateInstance(typeof(T), source)!;
    }

    /// <summary>
    /// Creates an effect by ID.
    /// </summary>
    public static ModularEffectBase? Create(string id, ISampleProvider source)
    {
        return _factories.TryGetValue(id, out var factory) ? factory(source) : null;
    }

    /// <summary>
    /// Gets all registered effect IDs.
    /// </summary>
    public static IEnumerable<string> GetRegisteredEffects() => _factories.Keys;
}
