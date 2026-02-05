// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann
// https://github.com/watermann420/MusicEngine
// Description: VST plugin wrapper.

using System;
using System.Collections.Generic;
using MusicEngine.Core.Automation;
using MusicEngine.CppLayer;
using NAudio.Wave;

namespace MusicEngine.Core;

/// <summary>
/// Represents information about a discovered VST plugin
/// </summary>
public class VstPluginInfo
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string Vendor { get; set; } = "";
    public string Version { get; set; } = "";
    public int UniqueId { get; set; }
    public bool IsInstrument { get; set; }
    public bool IsLoaded { get; set; }
    public int NumInputs { get; set; }
    public int NumOutputs { get; set; }
    public int NumParameters { get; set; }
    public int NumPrograms { get; set; }
}

/// <summary>
/// Parameter automation envelope point (legacy compatibility).
/// </summary>
public readonly struct AutomationPoint
{
    public double TimeBeats { get; }
    public float Value { get; }

    public AutomationPoint(double time, float value)
    {
        TimeBeats = time;
        Value = value;
    }
}

/// <summary>
/// Parameter automation data for a single parameter (legacy compatibility).
/// </summary>
public sealed class ParameterAutomation
{
    public int ParameterIndex { get; }
    public List<AutomationPoint> Points { get; } = new();
    public bool IsActive { get; set; } = true;

    public ParameterAutomation(int paramIndex)
    {
        ParameterIndex = paramIndex;
    }

    public float GetValueAtTime(double timeBeats)
    {
        if (Points.Count == 0) return 0f;
        if (Points.Count == 1) return Points[0].Value;

        for (int i = 0; i < Points.Count - 1; i++)
        {
            if (timeBeats >= Points[i].TimeBeats && timeBeats <= Points[i + 1].TimeBeats)
            {
                double t = (timeBeats - Points[i].TimeBeats) / (Points[i + 1].TimeBeats - Points[i].TimeBeats);
                return Points[i].Value + (float)((Points[i + 1].Value - Points[i].Value) * t);
            }
        }

        return Points[^1].Value;
    }

    public void AddRamp(double startTime, float startValue, double endTime, float endValue)
    {
        Points.Add(new AutomationPoint(startTime, startValue));
        Points.Add(new AutomationPoint(endTime, endValue));
        Points.Sort((a, b) => a.TimeBeats.CompareTo(b.TimeBeats));
    }
}

/// <summary>
/// VST2 wrapper backed by the native C++ layer.
/// </summary>
public sealed class VstPlugin : IVstPlugin
{
    private readonly NativeVstPluginAdapter _adapter;
    private readonly Dictionary<int, ParameterAutomation> _automations = new();
    private readonly object _automationLock = new();
    private double _currentTimeBeats;

    public VstPlugin(INativeVstPlugin nativePlugin, string pluginPath, int sampleRate = 0)
    {
        _adapter = new NativeVstPluginAdapter(nativePlugin, pluginPath, sampleRate);
        Info = new VstPluginInfo
        {
            Name = _adapter.Name,
            Path = pluginPath,
            Vendor = _adapter.Vendor,
            Version = _adapter.Version,
            UniqueId = nativePlugin.UniqueId == 0 ? pluginPath.GetHashCode() : unchecked((int)nativePlugin.UniqueId),
            IsInstrument = _adapter.IsInstrument,
            IsLoaded = _adapter.IsLoaded,
            NumInputs = _adapter.NumAudioInputs,
            NumOutputs = _adapter.NumAudioOutputs,
            NumParameters = _adapter.GetParameterCount(),
            NumPrograms = _adapter.GetPresetNames().Count
        };
    }

    public VstPluginInfo Info { get; }

    public string Name
    {
        get => _adapter.Name;
        set => _adapter.Name = value;
    }

    public string PluginPath => _adapter.PluginPath;
    public string Vendor => _adapter.Vendor;
    public string Version => _adapter.Version;
    public bool IsVst3 => _adapter.IsVst3;
    public bool IsLoaded => _adapter.IsLoaded;
    public bool IsInstrument => _adapter.IsInstrument;
    public int NumAudioInputs => _adapter.NumAudioInputs;
    public int NumAudioOutputs => _adapter.NumAudioOutputs;
    public int SampleRate => _adapter.SampleRate;
    public int BlockSize => _adapter.BlockSize;

    public float MasterVolume
    {
        get => _adapter.MasterVolume;
        set => _adapter.MasterVolume = value;
    }

    public WaveFormat WaveFormat => _adapter.WaveFormat;

    public int Read(float[] buffer, int offset, int count) => _adapter.Read(buffer, offset, count);

    public bool HasEditor => _adapter.HasEditor;

    public int CurrentPresetIndex => _adapter.CurrentPresetIndex;

    public string CurrentPresetName => _adapter.CurrentPresetName;

    public ISampleProvider? InputProvider
    {
        get => _adapter.InputProvider;
        set => _adapter.InputProvider = value;
    }

    public bool IsActive => _adapter.IsActive;

    public bool IsBypassed
    {
        get => _adapter.IsBypassed;
        set => _adapter.IsBypassed = value;
    }

    public event EventHandler<bool>? BypassChanged
    {
        add => _adapter.BypassChanged += value;
        remove => _adapter.BypassChanged -= value;
    }

    public int LatencySamples => _adapter.LatencySamples;

    public void SetSampleRate(double sampleRate) => _adapter.SetSampleRate(sampleRate);

    public void SetBlockSize(int blockSize) => _adapter.SetBlockSize(blockSize);

    public int GetParameterCount() => _adapter.GetParameterCount();

    public string GetParameterName(int index) => _adapter.GetParameterName(index);

    public float GetParameterValue(int index) => _adapter.GetParameterValue(index);

    public void SetParameterValue(int index, float value) => _adapter.SetParameterValue(index, value);

    public string GetParameterDisplay(int index) => _adapter.GetParameterDisplay(index);

    public VstParameterInfo? GetParameterInfo(int index) => _adapter.GetParameterInfo(index);

    public IReadOnlyList<VstParameterInfo> GetAllParameterInfo() => _adapter.GetAllParameterInfo();

    public bool CanParameterBeAutomated(int index) => _adapter.CanParameterBeAutomated(index);

    public IntPtr OpenEditor(IntPtr parentWindow) => _adapter.OpenEditor(parentWindow);

    public void CloseEditor() => _adapter.CloseEditor();

    public bool GetEditorSize(out int width, out int height) => _adapter.GetEditorSize(out width, out height);

    public bool LoadPreset(string path) => _adapter.LoadPreset(path);

    public bool SavePreset(string path) => _adapter.SavePreset(path);

    public IReadOnlyList<string> GetPresetNames() => _adapter.GetPresetNames();

    public void SetPreset(int index) => _adapter.SetPreset(index);

    public void SendControlChange(int channel, int controller, int value) => _adapter.SendControlChange(channel, controller, value);

    public void SendPitchBend(int channel, int value) => _adapter.SendPitchBend(channel, value);

    public void SendProgramChange(int channel, int program) => _adapter.SendProgramChange(channel, program);

    public void Activate() => _adapter.Activate();

    public void Deactivate() => _adapter.Deactivate();

    public void NoteOn(int note, int velocity) => _adapter.NoteOn(note, velocity);

    public void NoteOff(int note) => _adapter.NoteOff(note);

    public void AllNotesOff() => _adapter.AllNotesOff();

    public void SetParameter(string name, float value) => _adapter.SetParameter(name, value);

    public ParameterAutomation AutomateParameter(int index, float startValue, float endValue, double durationBeats)
    {
        lock (_automationLock)
        {
            var automation = new ParameterAutomation(index);
            automation.AddRamp(_currentTimeBeats, startValue, _currentTimeBeats + durationBeats, endValue);
            _automations[index] = automation;
            return automation;
        }
    }

    public ParameterAutomation? GetAutomation(int index)
    {
        lock (_automationLock)
        {
            return _automations.TryGetValue(index, out var automation) ? automation : null;
        }
    }

    public void ClearAutomation(int index)
    {
        lock (_automationLock)
        {
            _automations.Remove(index);
        }
    }

    public void ClearAllAutomation()
    {
        lock (_automationLock)
        {
            _automations.Clear();
        }
    }

    public void SetCurrentTimeBeats(double timeBeats)
    {
        lock (_automationLock)
        {
            _currentTimeBeats = timeBeats;
            foreach (var automation in _automations.Values)
            {
                if (!automation.IsActive)
                {
                    continue;
                }

                var value = automation.GetValueAtTime(timeBeats);
                _adapter.SetParameterValue(automation.ParameterIndex, value);
            }
        }
    }

    public void Dispose() => _adapter.Dispose();
}
