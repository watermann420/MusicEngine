//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Mock VST2 plugin implementation for unit testing.

using MusicEngine.Core;
using MusicEngine.Core.Automation;
using NAudio.Wave;

namespace MusicEngine.Tests.Mocks;

/// <summary>
/// Mock implementation of IVstPlugin for testing VstHost and plugin interactions.
/// Simulates a VST2 plugin without requiring actual native DLLs.
/// </summary>
public class MockVstPlugin : IVstPlugin
{
    private readonly WaveFormat _waveFormat;
    private readonly List<(int Note, int Velocity)> _activeNotes = new();
    private readonly Dictionary<int, float> _parameters = new();
    private readonly Dictionary<int, string> _parameterNames = new();
    private readonly List<string> _presetNames = new();
    private readonly List<(int Channel, int Controller, int Value)> _controlChanges = new();
    private readonly List<(int Channel, int Value)> _pitchBends = new();
    private readonly List<(int Channel, int Program)> _programChanges = new();

    private bool _isDisposed;
    private bool _isActive;
    private bool _isBypassed;
    private int _currentPresetIndex;
    private string _currentPresetName = "Default";
    private float _masterVolume = 1.0f;

    public string Name { get; set; } = "MockVstPlugin";
    public string PluginPath { get; set; } = @"C:\TestPlugins\MockPlugin.dll";
    public string Vendor { get; set; } = "MockVendor";
    public string Version { get; set; } = "1.0.0";
    public bool IsVst3 => false;
    public bool IsLoaded { get; set; } = true;
    public bool IsInstrument { get; set; } = true;
    public int NumAudioInputs { get; set; } = 2;
    public int NumAudioOutputs { get; set; } = 2;
    public int SampleRate { get; set; } = 44100;
    public int BlockSize { get; set; } = 512;
    public int LatencySamples { get; set; } = 0;
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

    public ISampleProvider? InputProvider { get; set; }
    public WaveFormat WaveFormat => _waveFormat;

    public event EventHandler<bool>? BypassChanged;

    // Tracking properties for test assertions
    public IReadOnlyList<(int Note, int Velocity)> ActiveNotes => _activeNotes;
    public IReadOnlyList<(int Channel, int Controller, int Value)> RecordedControlChanges => _controlChanges;
    public IReadOnlyList<(int Channel, int Value)> RecordedPitchBends => _pitchBends;
    public IReadOnlyList<(int Channel, int Program)> RecordedProgramChanges => _programChanges;
    public int NoteOnCount { get; private set; }
    public int NoteOffCount { get; private set; }
    public int AllNotesOffCount { get; private set; }
    public int ActivateCount { get; private set; }
    public int DeactivateCount { get; private set; }
    public string? LastLoadedPresetPath { get; private set; }
    public string? LastSavedPresetPath { get; private set; }
    public IntPtr? LastEditorParentWindow { get; private set; }
    public bool EditorIsOpen { get; private set; }

    /// <summary>
    /// Creates a new MockVstPlugin with default settings.
    /// </summary>
    public MockVstPlugin(int sampleRate = 44100, int channels = 2)
    {
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        SampleRate = sampleRate;

        // Initialize default parameters
        for (int i = 0; i < 10; i++)
        {
            _parameters[i] = 0.5f;
            _parameterNames[i] = $"Parameter {i}";
        }

        // Initialize presets
        for (int i = 0; i < 5; i++)
        {
            _presetNames.Add($"Preset {i + 1}");
        }
    }

    /// <summary>
    /// Creates a MockVstPlugin configured as an effect (not an instrument).
    /// </summary>
    public static MockVstPlugin CreateEffect(string name = "MockEffect")
    {
        return new MockVstPlugin
        {
            Name = name,
            IsInstrument = false
        };
    }

    /// <summary>
    /// Creates a MockVstPlugin configured as an instrument.
    /// </summary>
    public static MockVstPlugin CreateInstrument(string name = "MockInstrument")
    {
        return new MockVstPlugin
        {
            Name = name,
            IsInstrument = true
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
            // Effect: pass through input
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
            IsAutomatable = true,
            IsReadOnly = false,
            ParameterId = (uint)index
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

    public bool CanParameterBeAutomated(int index) => index >= 0 && index < _parameters.Count;

    public IntPtr OpenEditor(IntPtr parentWindow)
    {
        LastEditorParentWindow = parentWindow;
        EditorIsOpen = true;
        return new IntPtr(12345); // Mock handle
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
        if (File.Exists(path))
        {
            _currentPresetName = Path.GetFileNameWithoutExtension(path);
            return true;
        }
        // For testing, allow paths that don't exist but contain "valid" in the name
        if (path.Contains("valid", StringComparison.OrdinalIgnoreCase))
        {
            _currentPresetName = Path.GetFileNameWithoutExtension(path);
            return true;
        }
        return false;
    }

    public bool SavePreset(string path)
    {
        LastSavedPresetPath = path;
        return !string.IsNullOrEmpty(path);
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

    public void SendControlChange(int channel, int controller, int value)
    {
        _controlChanges.Add((channel, controller, value));
    }

    public void SendPitchBend(int channel, int value)
    {
        _pitchBends.Add((channel, value));
    }

    public void SendProgramChange(int channel, int program)
    {
        _programChanges.Add((channel, program));
    }

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

    /// <summary>
    /// Adds a parameter with a specific name for testing parameter lookups.
    /// </summary>
    public void AddParameter(int index, string name, float value = 0.5f)
    {
        _parameterNames[index] = name;
        _parameters[index] = value;
    }

    /// <summary>
    /// Resets all tracking counters for test isolation.
    /// </summary>
    public void Reset()
    {
        _activeNotes.Clear();
        _controlChanges.Clear();
        _pitchBends.Clear();
        _programChanges.Clear();
        NoteOnCount = 0;
        NoteOffCount = 0;
        AllNotesOffCount = 0;
        ActivateCount = 0;
        DeactivateCount = 0;
        LastLoadedPresetPath = null;
        LastSavedPresetPath = null;
        LastEditorParentWindow = null;
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
