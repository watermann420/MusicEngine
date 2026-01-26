//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Mock VST3 plugin implementation for unit testing VST3-specific features.

using MusicEngine.Core;
using MusicEngine.Core.Automation;
using NAudio.Wave;

namespace MusicEngine.Tests.Mocks;

/// <summary>
/// Mock implementation of IVst3Plugin for testing VST3-specific functionality.
/// Simulates a VST3 plugin without requiring actual native DLLs.
/// </summary>
public class MockVst3Plugin : IVst3Plugin
{
    private readonly WaveFormat _waveFormat;
    private readonly List<(int Note, int Velocity)> _activeNotes = new();
    private readonly Dictionary<int, float> _parameters = new();
    private readonly Dictionary<int, string> _parameterNames = new();
    private readonly Dictionary<int, Vst3ParameterFlags> _parameterFlags = new();
    private readonly List<string> _presetNames = new();
    private readonly List<Vst3UnitInfo> _units = new();
    private readonly Dictionary<int, List<int>> _unitParameters = new();
    private readonly List<Vst3BusInfo> _inputBuses = new();
    private readonly List<Vst3BusInfo> _outputBuses = new();
    private readonly List<(int NoteId, Vst3NoteExpressionType Type, double Value)> _noteExpressions = new();

    private bool _isDisposed;
    private bool _isActive;
    private bool _isBypassed;
    private int _currentPresetIndex;
    private string _currentPresetName = "Default";
    private float _masterVolume = 1.0f;

    public string Name { get; set; } = "MockVst3Plugin";
    public string PluginPath { get; set; } = @"C:\TestPlugins\MockPlugin.vst3";
    public string Vendor { get; set; } = "MockVst3Vendor";
    public string Version { get; set; } = "1.0.0";
    public bool IsVst3 => true;
    public bool IsLoaded { get; set; } = true;
    public bool IsInstrument { get; set; } = true;
    public int NumAudioInputs { get; set; } = 2;
    public int NumAudioOutputs { get; set; } = 2;
    public int SampleRate { get; set; } = 44100;
    public int BlockSize { get; set; } = 512;
    public float MasterVolume
    {
        get => _masterVolume;
        set => _masterVolume = Math.Clamp(value, 0f, 2f);
    }
    public bool HasEditor { get; set; } = true;
    public int CurrentPresetIndex => _currentPresetIndex;
    public string CurrentPresetName => _currentPresetName;
    public bool IsActive => _isActive;
    public bool IsBypassed
    {
        get => _isBypassed;
        set
        {
            if (_isBypassed != value)
            {
                _isBypassed = value;
                BypassChanged?.Invoke(this, value);
            }
        }
    }

    // VST3-specific properties
    public bool SupportsNoteExpression { get; set; } = true;
    public bool SupportsSidechain { get; set; }
    public int SidechainBusIndex { get; set; } = -1;
    public int LatencySamples { get; set; }

    public ISampleProvider? InputProvider { get; set; }
    public WaveFormat WaveFormat => _waveFormat;

    public event EventHandler<bool>? BypassChanged;

    // Tracking properties for test assertions
    public IReadOnlyList<(int Note, int Velocity)> ActiveNotes => _activeNotes;
    public IReadOnlyList<(int NoteId, Vst3NoteExpressionType Type, double Value)> RecordedNoteExpressions => _noteExpressions;
    public int NoteOnCount { get; private set; }
    public int NoteOffCount { get; private set; }
    public int AllNotesOffCount { get; private set; }
    public int ActivateCount { get; private set; }
    public int DeactivateCount { get; private set; }
    public string? LastLoadedPresetPath { get; private set; }
    public string? LastSavedPresetPath { get; private set; }
    public bool EditorIsOpen { get; private set; }

    /// <summary>
    /// Creates a new MockVst3Plugin with default settings.
    /// </summary>
    public MockVst3Plugin(int sampleRate = 44100, int channels = 2)
    {
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        SampleRate = sampleRate;

        // Initialize default parameters
        for (int i = 0; i < 10; i++)
        {
            _parameters[i] = 0.5f;
            _parameterNames[i] = $"Parameter {i}";
            _parameterFlags[i] = Vst3ParameterFlags.CanAutomate;
        }

        // Initialize presets
        for (int i = 0; i < 5; i++)
        {
            _presetNames.Add($"Preset {i + 1}");
        }

        // Initialize default unit
        _units.Add(new Vst3UnitInfo { Id = 0, ParentId = -1, Name = "Root" });
        _unitParameters[0] = Enumerable.Range(0, 10).ToList();

        // Initialize default buses
        _inputBuses.Add(new Vst3BusInfo
        {
            Name = "Main Input",
            MediaType = Vst3MediaType.Audio,
            Direction = Vst3BusDirection.Input,
            ChannelCount = 2,
            BusType = Vst3BusType.Main,
            IsDefaultActive = true
        });

        _outputBuses.Add(new Vst3BusInfo
        {
            Name = "Main Output",
            MediaType = Vst3MediaType.Audio,
            Direction = Vst3BusDirection.Output,
            ChannelCount = 2,
            BusType = Vst3BusType.Main,
            IsDefaultActive = true
        });
    }

    /// <summary>
    /// Creates a MockVst3Plugin configured as an effect with sidechain support.
    /// </summary>
    public static MockVst3Plugin CreateEffectWithSidechain(string name = "MockVst3Effect")
    {
        var plugin = new MockVst3Plugin
        {
            Name = name,
            IsInstrument = false,
            SupportsSidechain = true,
            SidechainBusIndex = 1
        };

        // Add sidechain bus
        plugin._inputBuses.Add(new Vst3BusInfo
        {
            Name = "Sidechain",
            MediaType = Vst3MediaType.Audio,
            Direction = Vst3BusDirection.Input,
            ChannelCount = 2,
            BusType = Vst3BusType.Aux,
            IsDefaultActive = false
        });

        return plugin;
    }

    /// <summary>
    /// Creates a MockVst3Plugin configured as an instrument with Note Expression support.
    /// </summary>
    public static MockVst3Plugin CreateInstrumentWithNoteExpression(string name = "MockVst3Synth")
    {
        return new MockVst3Plugin
        {
            Name = name,
            IsInstrument = true,
            SupportsNoteExpression = true
        };
    }

    public void NoteOn(int note, int velocity)
    {
        _activeNotes.Add((note, velocity));
        NoteOnCount++;
    }

    public void NoteOff(int note)
    {
        _activeNotes.RemoveAll(n => n.Note == note);
        NoteOffCount++;
    }

    public void AllNotesOff()
    {
        _activeNotes.Clear();
        AllNotesOffCount++;
    }

    public void SetParameter(string name, float value)
    {
        for (int i = 0; i < _parameterNames.Count; i++)
        {
            if (_parameterNames.TryGetValue(i, out var paramName) &&
                paramName.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                _parameters[i] = Math.Clamp(value, 0f, 1f);
                return;
            }
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        if (_isDisposed || _isBypassed)
        {
            Array.Clear(buffer, offset, count);
            return count;
        }

        // Generate simple sine wave if there are active notes
        if (_activeNotes.Count > 0 && IsInstrument)
        {
            double frequency = 440.0 * Math.Pow(2, (_activeNotes[0].Note - 69) / 12.0);
            for (int i = 0; i < count; i++)
            {
                buffer[offset + i] = (float)(Math.Sin(2 * Math.PI * frequency * i / SampleRate) * 0.5f * _masterVolume);
            }
        }
        else if (InputProvider != null && !IsInstrument)
        {
            int read = InputProvider.Read(buffer, offset, count);
            for (int i = 0; i < read; i++)
            {
                buffer[offset + i] *= _masterVolume;
            }
            return read;
        }
        else
        {
            Array.Clear(buffer, offset, count);
        }

        return count;
    }

    public void SetSampleRate(double sampleRate)
    {
        SampleRate = (int)sampleRate;
    }

    public void SetBlockSize(int blockSize)
    {
        BlockSize = blockSize;
    }

    public int GetParameterCount() => _parameters.Count;

    public string GetParameterName(int index)
    {
        return _parameterNames.TryGetValue(index, out var name) ? name : $"Param {index}";
    }

    public float GetParameterValue(int index)
    {
        return _parameters.TryGetValue(index, out var value) ? value : 0f;
    }

    public void SetParameterValue(int index, float value)
    {
        _parameters[index] = Math.Clamp(value, 0f, 1f);
    }

    public string GetParameterDisplay(int index)
    {
        if (_parameters.TryGetValue(index, out var value))
        {
            return $"{value:F2}";
        }
        return "0.00";
    }

    public VstParameterInfo? GetParameterInfo(int index)
    {
        if (index < 0 || index >= _parameters.Count)
            return null;

        var flags = _parameterFlags.TryGetValue(index, out var f) ? f : Vst3ParameterFlags.CanAutomate;

        return new VstParameterInfo
        {
            Index = index,
            Name = GetParameterName(index),
            ShortName = $"P{index}",
            Label = "",
            MinValue = 0f,
            MaxValue = 1f,
            DefaultValue = 0.5f,
            StepCount = 0,
            IsAutomatable = (flags & Vst3ParameterFlags.CanAutomate) != 0,
            IsReadOnly = (flags & Vst3ParameterFlags.IsReadOnly) != 0,
            IsBypass = (flags & Vst3ParameterFlags.IsBypass) != 0,
            ParameterId = (uint)index,
            UnitId = 0
        };
    }

    public IReadOnlyList<VstParameterInfo> GetAllParameterInfo()
    {
        var result = new List<VstParameterInfo>();
        for (int i = 0; i < _parameters.Count; i++)
        {
            var info = GetParameterInfo(i);
            if (info != null)
                result.Add(info);
        }
        return result.AsReadOnly();
    }

    public bool CanParameterBeAutomated(int index)
    {
        if (index < 0 || index >= _parameters.Count)
            return false;

        var flags = _parameterFlags.TryGetValue(index, out var f) ? f : Vst3ParameterFlags.CanAutomate;
        return (flags & Vst3ParameterFlags.CanAutomate) != 0;
    }

    public IntPtr OpenEditor(IntPtr parentWindow)
    {
        EditorIsOpen = true;
        return new IntPtr(12345);
    }

    public void CloseEditor()
    {
        EditorIsOpen = false;
    }

    public bool GetEditorSize(out int width, out int height)
    {
        width = 800;
        height = 600;
        return HasEditor;
    }

    public bool LoadPreset(string path)
    {
        LastLoadedPresetPath = path;
        if (path.EndsWith(".vstpreset", StringComparison.OrdinalIgnoreCase))
        {
            _currentPresetName = Path.GetFileNameWithoutExtension(path);
            return true;
        }
        return false;
    }

    public bool SavePreset(string path)
    {
        LastSavedPresetPath = path;
        return !string.IsNullOrEmpty(path) && path.EndsWith(".vstpreset", StringComparison.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> GetPresetNames() => _presetNames.AsReadOnly();

    public void SetPreset(int index)
    {
        if (index >= 0 && index < _presetNames.Count)
        {
            _currentPresetIndex = index;
            _currentPresetName = _presetNames[index];
        }
    }

    public void SendControlChange(int channel, int controller, int value) { }

    public void SendPitchBend(int channel, int value) { }

    public void SendProgramChange(int channel, int program) { }

    public void Activate()
    {
        _isActive = true;
        ActivateCount++;
    }

    public void Deactivate()
    {
        _isActive = false;
        DeactivateCount++;
    }

    // IVst3Plugin-specific methods

    public IReadOnlyList<Vst3UnitInfo> GetUnits() => _units.AsReadOnly();

    public IReadOnlyList<int> GetParametersInUnit(int unitId)
    {
        if (_unitParameters.TryGetValue(unitId, out var parameters))
            return parameters.AsReadOnly();
        return Array.Empty<int>();
    }

    public void SendNoteExpression(int noteId, Vst3NoteExpressionType type, double value)
    {
        if (SupportsNoteExpression)
        {
            _noteExpressions.Add((noteId, type, value));
        }
    }

    public int GetBusCount(Vst3MediaType mediaType, Vst3BusDirection direction)
    {
        if (mediaType == Vst3MediaType.Audio)
        {
            return direction == Vst3BusDirection.Input ? _inputBuses.Count : _outputBuses.Count;
        }
        return 0;
    }

    public Vst3BusInfo GetBusInfo(Vst3MediaType mediaType, Vst3BusDirection direction, int index)
    {
        if (mediaType == Vst3MediaType.Audio)
        {
            var buses = direction == Vst3BusDirection.Input ? _inputBuses : _outputBuses;
            if (index >= 0 && index < buses.Count)
                return buses[index];
        }
        return new Vst3BusInfo();
    }

    public bool SetBusActive(Vst3MediaType mediaType, Vst3BusDirection direction, int index, bool active)
    {
        if (mediaType == Vst3MediaType.Audio)
        {
            var buses = direction == Vst3BusDirection.Input ? _inputBuses : _outputBuses;
            if (index >= 0 && index < buses.Count)
            {
                buses[index].IsDefaultActive = active;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Adds a unit for testing unit-based parameter organization.
    /// </summary>
    public void AddUnit(int id, string name, int parentId = 0)
    {
        _units.Add(new Vst3UnitInfo { Id = id, ParentId = parentId, Name = name });
        _unitParameters[id] = new List<int>();
    }

    /// <summary>
    /// Adds a parameter with specific flags for testing.
    /// </summary>
    public void AddParameter(int index, string name, float value = 0.5f, Vst3ParameterFlags flags = Vst3ParameterFlags.CanAutomate, int unitId = 0)
    {
        _parameterNames[index] = name;
        _parameters[index] = value;
        _parameterFlags[index] = flags;

        if (_unitParameters.TryGetValue(unitId, out var unitParams))
        {
            if (!unitParams.Contains(index))
                unitParams.Add(index);
        }
    }

    /// <summary>
    /// Resets all tracking counters for test isolation.
    /// </summary>
    public void Reset()
    {
        _activeNotes.Clear();
        _noteExpressions.Clear();
        NoteOnCount = 0;
        NoteOffCount = 0;
        AllNotesOffCount = 0;
        ActivateCount = 0;
        DeactivateCount = 0;
        LastLoadedPresetPath = null;
        LastSavedPresetPath = null;
        EditorIsOpen = false;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _activeNotes.Clear();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// VST3 parameter flags for testing parameter behavior.
/// </summary>
[Flags]
public enum Vst3ParameterFlags
{
    None = 0,
    CanAutomate = 1 << 0,
    IsReadOnly = 1 << 1,
    IsWrapAround = 1 << 2,
    IsList = 1 << 3,
    IsProgramChange = 1 << 15,
    IsBypass = 1 << 16
}
