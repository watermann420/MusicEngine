//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Common interface for VST2 and VST3 plugins.

using System;
using MusicEngine.Core.Automation;
using NAudio.Wave;

namespace MusicEngine.Core;

/// <summary>
/// Common interface for VST2 and VST3 plugins.
/// Provides a unified API regardless of the underlying VST version.
/// </summary>
public interface IVstPlugin : ISynth, IDisposable
{
    /// <summary>
    /// Full path to the plugin file
    /// </summary>
    string PluginPath { get; }

    /// <summary>
    /// Plugin vendor name
    /// </summary>
    string Vendor { get; }

    /// <summary>
    /// Plugin version string
    /// </summary>
    string Version { get; }

    /// <summary>
    /// True if this is a VST3 plugin, false for VST2
    /// </summary>
    bool IsVst3 { get; }

    /// <summary>
    /// True if the plugin is successfully loaded and initialized
    /// </summary>
    bool IsLoaded { get; }

    /// <summary>
    /// True if this plugin is an instrument (synthesizer), false for effects
    /// </summary>
    bool IsInstrument { get; }

    /// <summary>
    /// Number of audio input channels
    /// </summary>
    int NumAudioInputs { get; }

    /// <summary>
    /// Number of audio output channels
    /// </summary>
    int NumAudioOutputs { get; }

    /// <summary>
    /// Current sample rate
    /// </summary>
    int SampleRate { get; }

    /// <summary>
    /// Current block size
    /// </summary>
    int BlockSize { get; }

    /// <summary>
    /// Master volume (0.0 to 2.0)
    /// </summary>
    float MasterVolume { get; set; }

    /// <summary>
    /// Set the sample rate for processing
    /// </summary>
    void SetSampleRate(double sampleRate);

    /// <summary>
    /// Set the block size for processing
    /// </summary>
    void SetBlockSize(int blockSize);

    /// <summary>
    /// Get the total number of parameters
    /// </summary>
    int GetParameterCount();

    /// <summary>
    /// Get parameter name by index
    /// </summary>
    string GetParameterName(int index);

    /// <summary>
    /// Get parameter value (0-1 normalized) by index
    /// </summary>
    float GetParameterValue(int index);

    /// <summary>
    /// Set parameter value (0-1 normalized) by index
    /// </summary>
    void SetParameterValue(int index, float value);

    /// <summary>
    /// Get the formatted parameter display string
    /// </summary>
    string GetParameterDisplay(int index);

    /// <summary>
    /// Get detailed information about a parameter
    /// </summary>
    /// <param name="index">Parameter index</param>
    /// <returns>VstParameterInfo containing parameter details, or null if index is invalid</returns>
    VstParameterInfo? GetParameterInfo(int index);

    /// <summary>
    /// Get information about all parameters
    /// </summary>
    /// <returns>Read-only list of all parameter info</returns>
    IReadOnlyList<VstParameterInfo> GetAllParameterInfo();

    /// <summary>
    /// Check if a parameter can be automated
    /// </summary>
    /// <param name="index">Parameter index</param>
    /// <returns>True if the parameter supports automation</returns>
    bool CanParameterBeAutomated(int index);

    /// <summary>
    /// True if the plugin has an editor GUI
    /// </summary>
    bool HasEditor { get; }

    /// <summary>
    /// Open the plugin editor GUI
    /// </summary>
    /// <param name="parentWindow">Handle to the parent window</param>
    /// <returns>Handle to the editor window, or IntPtr.Zero if failed</returns>
    IntPtr OpenEditor(IntPtr parentWindow);

    /// <summary>
    /// Close the plugin editor GUI
    /// </summary>
    void CloseEditor();

    /// <summary>
    /// Get the preferred editor window size
    /// </summary>
    /// <param name="width">Output width</param>
    /// <param name="height">Output height</param>
    /// <returns>True if size was retrieved</returns>
    bool GetEditorSize(out int width, out int height);

    /// <summary>
    /// Load a preset file
    /// </summary>
    bool LoadPreset(string path);

    /// <summary>
    /// Save current state to a preset file
    /// </summary>
    bool SavePreset(string path);

    /// <summary>
    /// Get the list of available preset names
    /// </summary>
    IReadOnlyList<string> GetPresetNames();

    /// <summary>
    /// Set the current preset by index
    /// </summary>
    void SetPreset(int index);

    /// <summary>
    /// Current preset index
    /// </summary>
    int CurrentPresetIndex { get; }

    /// <summary>
    /// Current preset name
    /// </summary>
    string CurrentPresetName { get; }

    /// <summary>
    /// Send a MIDI Control Change message
    /// </summary>
    void SendControlChange(int channel, int controller, int value);

    /// <summary>
    /// Send a MIDI Pitch Bend message
    /// </summary>
    void SendPitchBend(int channel, int value);

    /// <summary>
    /// Send a MIDI Program Change message
    /// </summary>
    void SendProgramChange(int channel, int program);

    /// <summary>
    /// Set input sample provider for effect processing
    /// </summary>
    ISampleProvider? InputProvider { get; set; }

    /// <summary>
    /// Activate the plugin for processing
    /// </summary>
    void Activate();

    /// <summary>
    /// Deactivate the plugin
    /// </summary>
    void Deactivate();

    /// <summary>
    /// Check if the plugin is currently active
    /// </summary>
    bool IsActive { get; }
}

/// <summary>
/// Extended interface for VST3-specific features
/// </summary>
public interface IVst3Plugin : IVstPlugin
{
    /// <summary>
    /// Get the list of parameter units/groups
    /// </summary>
    IReadOnlyList<Vst3UnitInfo> GetUnits();

    /// <summary>
    /// Get parameters in a specific unit
    /// </summary>
    IReadOnlyList<int> GetParametersInUnit(int unitId);

    /// <summary>
    /// Check if the plugin supports Note Expression
    /// </summary>
    bool SupportsNoteExpression { get; }

    /// <summary>
    /// Send a Note Expression value
    /// </summary>
    void SendNoteExpression(int noteId, Vst3NoteExpressionType type, double value);

    /// <summary>
    /// Get the number of audio buses
    /// </summary>
    int GetBusCount(Vst3MediaType mediaType, Vst3BusDirection direction);

    /// <summary>
    /// Get bus info
    /// </summary>
    Vst3BusInfo GetBusInfo(Vst3MediaType mediaType, Vst3BusDirection direction, int index);

    /// <summary>
    /// Activate or deactivate a bus
    /// </summary>
    bool SetBusActive(Vst3MediaType mediaType, Vst3BusDirection direction, int index, bool active);

    /// <summary>
    /// Check if the plugin supports sidechain input
    /// </summary>
    bool SupportsSidechain { get; }

    /// <summary>
    /// Get the sidechain bus index, or -1 if not available
    /// </summary>
    int SidechainBusIndex { get; }
}

/// <summary>
/// VST3 Unit information
/// </summary>
public class Vst3UnitInfo
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public string Name { get; set; } = "";
    public int ProgramListId { get; set; } = -1;
}

/// <summary>
/// VST3 Bus information
/// </summary>
public class Vst3BusInfo
{
    public string Name { get; set; } = "";
    public Vst3MediaType MediaType { get; set; }
    public Vst3BusDirection Direction { get; set; }
    public int ChannelCount { get; set; }
    public Vst3BusType BusType { get; set; }
    public bool IsDefaultActive { get; set; }
}

/// <summary>
/// VST3 media types
/// </summary>
public enum Vst3MediaType
{
    Audio = 0,
    Event = 1
}

/// <summary>
/// VST3 bus direction
/// </summary>
public enum Vst3BusDirection
{
    Input = 0,
    Output = 1
}

/// <summary>
/// VST3 bus type
/// </summary>
public enum Vst3BusType
{
    Main = 0,
    Aux = 1
}

/// <summary>
/// VST3 Note Expression types
/// </summary>
public enum Vst3NoteExpressionType
{
    Volume = 0,
    Pan = 1,
    Tuning = 2,
    Vibrato = 3,
    Expression = 4,
    Brightness = 5,
    Custom = 0x10000
}
