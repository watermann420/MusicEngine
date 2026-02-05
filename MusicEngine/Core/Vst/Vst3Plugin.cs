// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann
// https://github.com/watermann420/MusicEngine
// Description: VST3 plugin wrapper backed by the native C++ layer.

using System;
using System.Collections.Generic;
using MusicEngine.Core.Automation;
using MusicEngine.CppLayer;
using NAudio.Wave;

namespace MusicEngine.Core;

public sealed class Vst3Plugin : IVst3Plugin
{
    private readonly NativeVstPluginAdapter _adapter;

    public Vst3Plugin(INativeVstPlugin nativePlugin, string pluginPath, int sampleRate = 0)
    {
        _adapter = new NativeVstPluginAdapter(nativePlugin, pluginPath, sampleRate);
    }

    public Vst3Plugin(string pluginPath, int sampleRate = 0)
    {
        throw new NotSupportedException("Use VstHost to load VST3 plugins via the native C++ layer.");
    }

    public string Name
    {
        get => _adapter.Name;
        set => _adapter.Name = value;
    }

    public string PluginPath => _adapter.PluginPath;
    public string Vendor => _adapter.Vendor;
    public string Version => _adapter.Version;
    public bool IsVst3 => true;
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

    public bool HasEditor => _adapter.HasEditor;

    public IntPtr OpenEditor(IntPtr parentWindow) => _adapter.OpenEditor(parentWindow);

    public void CloseEditor() => _adapter.CloseEditor();

    public bool GetEditorSize(out int width, out int height) => _adapter.GetEditorSize(out width, out height);

    public bool LoadPreset(string path) => _adapter.LoadPreset(path);

    public bool SavePreset(string path) => _adapter.SavePreset(path);

    public IReadOnlyList<string> GetPresetNames() => _adapter.GetPresetNames();

    public void SetPreset(int index) => _adapter.SetPreset(index);

    public int CurrentPresetIndex => _adapter.CurrentPresetIndex;

    public string CurrentPresetName => _adapter.CurrentPresetName;

    public void SendControlChange(int channel, int controller, int value) => _adapter.SendControlChange(channel, controller, value);

    public void SendPitchBend(int channel, int value) => _adapter.SendPitchBend(channel, value);

    public void SendProgramChange(int channel, int program) => _adapter.SendProgramChange(channel, program);

    public ISampleProvider? InputProvider
    {
        get => _adapter.InputProvider;
        set => _adapter.InputProvider = value;
    }

    public void Activate() => _adapter.Activate();

    public void Deactivate() => _adapter.Deactivate();

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

    public void NoteOn(int note, int velocity) => _adapter.NoteOn(note, velocity);

    public void NoteOff(int note) => _adapter.NoteOff(note);

    public void AllNotesOff() => _adapter.AllNotesOff();

    public void SetParameter(string name, float value) => _adapter.SetParameter(name, value);

    public IReadOnlyList<Vst3UnitInfo> GetUnits() => Array.Empty<Vst3UnitInfo>();

    public IReadOnlyList<int> GetParametersInUnit(int unitId) => Array.Empty<int>();

    public bool SupportsNoteExpression => false;

    public void SendNoteExpression(int noteId, Vst3NoteExpressionType type, double value)
    {
    }

    public int GetBusCount(Vst3MediaType mediaType, Vst3BusDirection direction) => 0;

    public Vst3BusInfo GetBusInfo(Vst3MediaType mediaType, Vst3BusDirection direction, int index)
    {
        return new Vst3BusInfo
        {
            Name = string.Empty,
            MediaType = mediaType,
            Direction = direction,
            ChannelCount = 0,
            BusType = Vst3BusType.Main,
            IsDefaultActive = false
        };
    }

    public bool SetBusActive(Vst3MediaType mediaType, Vst3BusDirection direction, int index, bool active) => false;

    public bool SupportsSidechain => false;

    public int SidechainBusIndex => -1;

    public void Dispose() => _adapter.Dispose();
}
