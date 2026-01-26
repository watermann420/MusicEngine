// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

namespace MusicEngine.Core.Synthesizers.Modular;

/// <summary>
/// Defines the type of signal carried by a module port.
/// </summary>
public enum PortType
{
    /// <summary>Audio signal (-1 to +1 range, processed at sample rate)</summary>
    Audio,
    /// <summary>Control voltage signal (typically -5V to +5V, or 0-10V)</summary>
    Control,
    /// <summary>Gate signal (0 or 1, used for sustaining notes)</summary>
    Gate,
    /// <summary>Trigger signal (short pulse, used for one-shot events)</summary>
    Trigger
}

/// <summary>
/// Defines the direction of a module port.
/// </summary>
public enum PortDirection
{
    /// <summary>Input port that receives signals</summary>
    Input,
    /// <summary>Output port that sends signals</summary>
    Output
}

/// <summary>
/// Represents a connection point on a modular synthesizer module.
/// </summary>
public class ModulePort
{
    private readonly float[] _buffer;
    private ModulePort? _connectedTo;

    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; set; }
    public PortType Type { get; }
    public PortDirection Direction { get; }
    public ModuleBase Owner { get; }

    public ModulePort? ConnectedTo
    {
        get => _connectedTo;
        internal set => _connectedTo = value;
    }

    /// <summary>
    /// Single sample value for the port (used for control-rate signals).
    /// </summary>
    public float Value { get; set; }

    /// <summary>
    /// Buffer for audio-rate signals.
    /// </summary>
    public float[] Buffer => _buffer;

    public bool IsConnected => ConnectedTo != null;

    public ModulePort(string name, PortType type, PortDirection direction, ModuleBase owner, int bufferSize = 1024)
    {
        Name = name;
        Type = type;
        Direction = direction;
        Owner = owner;
        _buffer = new float[bufferSize];
    }

    /// <summary>
    /// Gets the signal value at a specific sample index.
    /// If connected, reads from the source; otherwise returns the buffer value.
    /// </summary>
    public float GetValue(int sampleIndex)
    {
        if (Direction == PortDirection.Input && IsConnected && ConnectedTo != null)
        {
            return ConnectedTo.Buffer[sampleIndex];
        }
        return _buffer[sampleIndex];
    }

    /// <summary>
    /// Sets the signal value at a specific sample index.
    /// </summary>
    public void SetValue(int sampleIndex, float value)
    {
        _buffer[sampleIndex] = value;
        if (sampleIndex == 0)
        {
            Value = value;
        }
    }

    /// <summary>
    /// Clears the buffer to zero.
    /// </summary>
    public void ClearBuffer()
    {
        Array.Clear(_buffer, 0, _buffer.Length);
        Value = 0;
    }

    /// <summary>
    /// Copies values from a source buffer to this port's buffer.
    /// </summary>
    public void CopyFrom(float[] source, int count)
    {
        int copyCount = Math.Min(count, _buffer.Length);
        Array.Copy(source, _buffer, copyCount);
        if (copyCount > 0)
        {
            Value = _buffer[0];
        }
    }
}

/// <summary>
/// Base class for all modular synthesizer modules.
/// Provides common functionality for inputs, outputs, parameters, and processing.
/// </summary>
public abstract class ModuleBase : IDisposable
{
    private bool _disposed;
    protected readonly int SampleRate;
    protected readonly int BufferSize;

    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; set; }
    public List<ModulePort> Inputs { get; } = new();
    public List<ModulePort> Outputs { get; } = new();
    public Dictionary<string, float> Parameters { get; } = new();

    /// <summary>
    /// Parameter metadata including min, max, and default values.
    /// </summary>
    public Dictionary<string, ParameterInfo> ParameterInfos { get; } = new();

    protected ModuleBase(string name, int sampleRate = 44100, int bufferSize = 1024)
    {
        Name = name;
        SampleRate = sampleRate;
        BufferSize = bufferSize;
    }

    /// <summary>
    /// Adds an input port to the module.
    /// </summary>
    protected ModulePort AddInput(string name, PortType type)
    {
        var port = new ModulePort(name, type, PortDirection.Input, this, BufferSize);
        Inputs.Add(port);
        return port;
    }

    /// <summary>
    /// Adds an output port to the module.
    /// </summary>
    protected ModulePort AddOutput(string name, PortType type)
    {
        var port = new ModulePort(name, type, PortDirection.Output, this, BufferSize);
        Outputs.Add(port);
        return port;
    }

    /// <summary>
    /// Registers a parameter with metadata.
    /// </summary>
    protected void RegisterParameter(string name, float defaultValue, float minValue = 0f, float maxValue = 1f)
    {
        Parameters[name] = defaultValue;
        ParameterInfos[name] = new ParameterInfo(name, defaultValue, minValue, maxValue);
    }

    /// <summary>
    /// Gets a parameter value by name.
    /// </summary>
    public float GetParameter(string name)
    {
        return Parameters.TryGetValue(name, out var value) ? value : 0f;
    }

    /// <summary>
    /// Sets a parameter value by name, clamped to valid range.
    /// </summary>
    public void SetParameter(string name, float value)
    {
        if (ParameterInfos.TryGetValue(name, out var info))
        {
            value = Math.Clamp(value, info.MinValue, info.MaxValue);
        }
        Parameters[name] = value;
    }

    /// <summary>
    /// Gets an input port by name.
    /// </summary>
    public ModulePort? GetInput(string name)
    {
        return Inputs.FirstOrDefault(p => p.Name == name);
    }

    /// <summary>
    /// Gets an output port by name.
    /// </summary>
    public ModulePort? GetOutput(string name)
    {
        return Outputs.FirstOrDefault(p => p.Name == name);
    }

    /// <summary>
    /// Processes audio for the specified number of samples.
    /// </summary>
    public abstract void Process(int sampleCount);

    /// <summary>
    /// Resets the module to its initial state.
    /// </summary>
    public virtual void Reset()
    {
        foreach (var input in Inputs)
        {
            input.ClearBuffer();
        }
        foreach (var output in Outputs)
        {
            output.ClearBuffer();
        }
    }

    /// <summary>
    /// Clears all output buffers.
    /// </summary>
    protected void ClearOutputs()
    {
        foreach (var output in Outputs)
        {
            output.ClearBuffer();
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                Inputs.Clear();
                Outputs.Clear();
                Parameters.Clear();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Metadata for a module parameter.
/// </summary>
public class ParameterInfo
{
    public string Name { get; }
    public float DefaultValue { get; }
    public float MinValue { get; }
    public float MaxValue { get; }

    public ParameterInfo(string name, float defaultValue, float minValue, float maxValue)
    {
        Name = name;
        DefaultValue = defaultValue;
        MinValue = minValue;
        MaxValue = maxValue;
    }
}
