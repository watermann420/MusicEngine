// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace MusicEngine.Core.Vst;

/// <summary>
/// AudioUnit component types.
/// </summary>
public enum AUComponentType
{
    /// <summary>Audio effect.</summary>
    Effect,

    /// <summary>Music device (instrument/synthesizer).</summary>
    MusicDevice,

    /// <summary>Music effect (MIDI processor).</summary>
    MusicEffect,

    /// <summary>Audio generator.</summary>
    Generator,

    /// <summary>Audio format converter.</summary>
    FormatConverter,

    /// <summary>Audio mixer.</summary>
    Mixer,

    /// <summary>Audio output.</summary>
    Output
}

/// <summary>
/// AudioUnit parameter scope.
/// </summary>
public enum AUParameterScope
{
    /// <summary>Global parameters.</summary>
    Global,

    /// <summary>Input parameters.</summary>
    Input,

    /// <summary>Output parameters.</summary>
    Output
}

/// <summary>
/// AudioUnit parameter unit types.
/// </summary>
public enum AUParameterUnit
{
    /// <summary>Generic value.</summary>
    Generic,

    /// <summary>Indexed selection.</summary>
    Indexed,

    /// <summary>Boolean value.</summary>
    Boolean,

    /// <summary>Percentage (0-100).</summary>
    Percent,

    /// <summary>Time in seconds.</summary>
    Seconds,

    /// <summary>Sample frames.</summary>
    SampleFrames,

    /// <summary>Phase angle (0-360).</summary>
    Phase,

    /// <summary>Rate in Hz.</summary>
    Rate,

    /// <summary>Frequency in Hz.</summary>
    Hertz,

    /// <summary>Cents (1/100 semitone).</summary>
    Cents,

    /// <summary>Relative semitones.</summary>
    RelativeSemitones,

    /// <summary>MIDI note number.</summary>
    MIDINoteNumber,

    /// <summary>MIDI controller.</summary>
    MIDIController,

    /// <summary>Decibels.</summary>
    Decibels,

    /// <summary>Linear gain.</summary>
    LinearGain,

    /// <summary>Degrees.</summary>
    Degrees,

    /// <summary>Equal power crossfade.</summary>
    EqualPowerCrossfade,

    /// <summary>Mix level.</summary>
    MixerFaderCurve1,

    /// <summary>Pan position.</summary>
    Pan,

    /// <summary>Meters.</summary>
    Meters,

    /// <summary>Absolute cents.</summary>
    AbsoluteCents,

    /// <summary>Octaves.</summary>
    Octaves,

    /// <summary>BPM.</summary>
    BPM,

    /// <summary>Beats.</summary>
    Beats,

    /// <summary>Milliseconds.</summary>
    Milliseconds,

    /// <summary>Ratio.</summary>
    Ratio,

    /// <summary>Custom unit.</summary>
    CustomUnit
}

/// <summary>
/// Information about an AudioUnit plugin.
/// </summary>
public class AUPluginInfo
{
    /// <summary>Component manufacturer (four-character code).</summary>
    public string Manufacturer { get; set; } = string.Empty;

    /// <summary>Component type.</summary>
    public AUComponentType ComponentType { get; set; }

    /// <summary>Component sub-type (four-character code).</summary>
    public string SubType { get; set; } = string.Empty;

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Version number.</summary>
    public uint Version { get; set; }

    /// <summary>Bundle identifier (macOS/iOS).</summary>
    public string BundleId { get; set; } = string.Empty;

    /// <summary>Path to the plugin bundle.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Whether the plugin has a custom view.</summary>
    public bool HasCustomView { get; set; }

    /// <summary>Whether the plugin supports MIDI input.</summary>
    public bool SupportsMIDI { get; set; }

    /// <summary>Number of audio inputs.</summary>
    public int AudioInputs { get; set; }

    /// <summary>Number of audio outputs.</summary>
    public int AudioOutputs { get; set; }

    /// <summary>Gets whether this is an instrument.</summary>
    public bool IsInstrument => ComponentType == AUComponentType.MusicDevice;

    /// <summary>Gets a unique identifier string.</summary>
    public string UniqueId => $"{Manufacturer}.{SubType}";
}

/// <summary>
/// AudioUnit parameter information.
/// </summary>
public class AUParameterInfo
{
    /// <summary>Parameter ID.</summary>
    public uint Id { get; set; }

    /// <summary>Parameter name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Parameter scope.</summary>
    public AUParameterScope Scope { get; set; }

    /// <summary>Parameter unit type.</summary>
    public AUParameterUnit Unit { get; set; }

    /// <summary>Minimum value.</summary>
    public float MinValue { get; set; }

    /// <summary>Maximum value.</summary>
    public float MaxValue { get; set; }

    /// <summary>Default value.</summary>
    public float DefaultValue { get; set; }

    /// <summary>Whether the parameter is read-only.</summary>
    public bool IsReadOnly { get; set; }

    /// <summary>Whether the parameter is automatable.</summary>
    public bool IsAutomatable { get; set; } = true;

    /// <summary>Value strings for indexed parameters.</summary>
    public List<string>? ValueStrings { get; set; }

    /// <summary>Custom unit name.</summary>
    public string? CustomUnitName { get; set; }
}

/// <summary>
/// AudioUnit preset data.
/// </summary>
public class AUPreset
{
    /// <summary>Preset name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Preset number (-1 for user presets).</summary>
    public int Number { get; set; } = -1;

    /// <summary>Is this a factory preset?</summary>
    public bool IsFactory { get; set; }

    /// <summary>Preset data (state dictionary).</summary>
    public byte[]? Data { get; set; }
}

/// <summary>
/// AudioUnit v3 plugin instance (stub for Windows).
/// </summary>
public class AUv3Plugin : ISampleProvider, IDisposable
{
    private readonly AUPluginInfo _info;
    private readonly int _sampleRate;
    private readonly int _blockSize;
    private bool _disposed;
    private bool _isActive;
    private readonly List<AUParameterInfo> _parameters = new();
    private readonly Dictionary<uint, float> _parameterValues = new();
    private readonly List<AUPreset> _presets = new();

    /// <summary>Gets the plugin information.</summary>
    public AUPluginInfo Info => _info;

    /// <summary>Gets whether the plugin is loaded.</summary>
    public bool IsLoaded { get; private set; }

    /// <summary>Gets whether the plugin is active.</summary>
    public bool IsActive => _isActive;

    /// <summary>Gets the audio format.</summary>
    public WaveFormat WaveFormat { get; }

    /// <summary>Gets or sets the master volume.</summary>
    public float MasterVolume { get; set; } = 1.0f;

    /// <summary>Gets or sets the input provider for effects.</summary>
    public ISampleProvider? InputProvider { get; set; }

    /// <summary>Gets the parameter count.</summary>
    public int ParameterCount => _parameters.Count;

    /// <summary>Gets the preset count.</summary>
    public int PresetCount => _presets.Count;

    internal AUv3Plugin(AUPluginInfo info, int sampleRate, int blockSize)
    {
        _info = info;
        _sampleRate = sampleRate;
        _blockSize = blockSize;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2);
    }

    /// <summary>
    /// Activates the plugin for processing.
    /// </summary>
    public void Activate()
    {
        if (_disposed || _isActive) return;
        _isActive = true;
    }

    /// <summary>
    /// Deactivates the plugin.
    /// </summary>
    public void Deactivate()
    {
        if (!_isActive) return;
        _isActive = false;
    }

    /// <summary>
    /// Gets parameter information by index.
    /// </summary>
    public AUParameterInfo? GetParameterInfo(int index)
    {
        if (index < 0 || index >= _parameters.Count)
            return null;
        return _parameters[index];
    }

    /// <summary>
    /// Gets a parameter value.
    /// </summary>
    public float GetParameterValue(uint parameterId)
    {
        return _parameterValues.TryGetValue(parameterId, out var value) ? value : 0f;
    }

    /// <summary>
    /// Sets a parameter value.
    /// </summary>
    public void SetParameterValue(uint parameterId, float value)
    {
        _parameterValues[parameterId] = value;
    }

    /// <summary>
    /// Gets a preset by index.
    /// </summary>
    public AUPreset? GetPreset(int index)
    {
        if (index < 0 || index >= _presets.Count)
            return null;
        return _presets[index];
    }

    /// <summary>
    /// Loads a preset by index.
    /// </summary>
    public bool LoadPreset(int index)
    {
        // Stub - would load preset state on supported platforms
        return false;
    }

    /// <summary>
    /// Saves the current state as a preset.
    /// </summary>
    public AUPreset? SavePreset(string name)
    {
        // Stub - would save preset state on supported platforms
        return null;
    }

    /// <summary>
    /// Gets the full plugin state.
    /// </summary>
    public byte[]? GetState()
    {
        // Stub - would return state data on supported platforms
        return null;
    }

    /// <summary>
    /// Sets the full plugin state.
    /// </summary>
    public bool SetState(byte[] state)
    {
        // Stub - would restore state on supported platforms
        return false;
    }

    /// <summary>
    /// Sends a MIDI note on event.
    /// </summary>
    public void NoteOn(int note, int velocity, int channel = 0)
    {
        // Stub - would send MIDI on supported platforms
    }

    /// <summary>
    /// Sends a MIDI note off event.
    /// </summary>
    public void NoteOff(int note, int channel = 0)
    {
        // Stub - would send MIDI on supported platforms
    }

    /// <summary>
    /// Sends all notes off.
    /// </summary>
    public void AllNotesOff()
    {
        // Stub - would send all notes off on supported platforms
    }

    /// <summary>
    /// Reads processed audio samples.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        if (_disposed || !_isActive)
        {
            Array.Clear(buffer, offset, count);
            return count;
        }

        // Stub implementation - pass through input or silence
        if (InputProvider != null)
        {
            return InputProvider.Read(buffer, offset, count);
        }

        Array.Clear(buffer, offset, count);
        return count;
    }

    /// <summary>
    /// Disposes the plugin.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        AllNotesOff();
        Deactivate();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer.
    /// </summary>
    ~AUv3Plugin()
    {
        Dispose();
    }
}

/// <summary>
/// AudioUnit v3 (AUv3) host implementation.
/// </summary>
/// <remarks>
/// This is a stub/placeholder implementation for Windows.
/// AudioUnit plugins are only natively available on macOS and iOS.
/// This class provides the interface structure for potential future
/// cross-platform support or macOS builds.
///
/// On supported platforms (macOS/iOS), this would use the AudioToolbox
/// and AudioUnit frameworks to:
/// - Discover installed Audio Units
/// - Load and instantiate plugins
/// - Process audio through the plugin
/// - Handle MIDI events
/// - Manage presets and state
/// </remarks>
public class AUv3Host : IDisposable
{
    private readonly List<AUPluginInfo> _discoveredPlugins = new();
    private readonly List<AUv3Plugin> _loadedPlugins = new();
    private readonly int _sampleRate;
    private readonly int _blockSize;
    private bool _disposed;

    /// <summary>Gets discovered plugins.</summary>
    public IReadOnlyList<AUPluginInfo> DiscoveredPlugins => _discoveredPlugins.AsReadOnly();

    /// <summary>Gets loaded plugin instances.</summary>
    public IReadOnlyList<AUv3Plugin> LoadedPlugins => _loadedPlugins.AsReadOnly();

    /// <summary>Gets or sets the sample rate.</summary>
    public int SampleRate => _sampleRate;

    /// <summary>Gets or sets the block size.</summary>
    public int BlockSize => _blockSize;

    /// <summary>Gets whether AudioUnits are supported on this platform.</summary>
    public static bool IsPlatformSupported => false; // Only true on macOS/iOS

    /// <summary>Event raised during plugin scanning.</summary>
    public event Action<float, string>? ScanProgress;

    /// <summary>Event raised when a plugin is discovered.</summary>
    public event Action<AUPluginInfo>? PluginDiscovered;

    /// <summary>
    /// Creates a new AUv3 host.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate.</param>
    /// <param name="blockSize">Processing block size.</param>
    public AUv3Host(int sampleRate = 44100, int blockSize = 512)
    {
        _sampleRate = sampleRate;
        _blockSize = blockSize;
    }

    /// <summary>
    /// Scans for installed AudioUnit plugins.
    /// </summary>
    /// <param name="componentType">Optional filter by component type.</param>
    /// <remarks>
    /// On Windows, this will complete immediately with no plugins found.
    /// On macOS/iOS, this would query the system for installed Audio Units.
    /// </remarks>
    public void ScanForPlugins(AUComponentType? componentType = null)
    {
        _discoveredPlugins.Clear();

        ScanProgress?.Invoke(0f, "Checking platform support...");

        if (!IsPlatformSupported)
        {
            ScanProgress?.Invoke(1f, "AudioUnits not supported on this platform");
            return;
        }

        // On macOS, this would:
        // 1. Use AudioComponentCopyName to enumerate components
        // 2. Filter by type if specified
        // 3. Create AUPluginInfo for each discovered component

        ScanProgress?.Invoke(1f, $"Found {_discoveredPlugins.Count} plugins");
    }

    /// <summary>
    /// Gets plugins by component type.
    /// </summary>
    public IEnumerable<AUPluginInfo> GetPluginsByType(AUComponentType type)
    {
        foreach (var plugin in _discoveredPlugins)
        {
            if (plugin.ComponentType == type)
                yield return plugin;
        }
    }

    /// <summary>
    /// Gets all instrument plugins.
    /// </summary>
    public IEnumerable<AUPluginInfo> GetInstruments()
    {
        return GetPluginsByType(AUComponentType.MusicDevice);
    }

    /// <summary>
    /// Gets all effect plugins.
    /// </summary>
    public IEnumerable<AUPluginInfo> GetEffects()
    {
        return GetPluginsByType(AUComponentType.Effect);
    }

    /// <summary>
    /// Loads a plugin by its unique identifier.
    /// </summary>
    /// <param name="uniqueId">The plugin's unique identifier (Manufacturer.SubType).</param>
    /// <returns>The loaded plugin or null if not found/supported.</returns>
    public AUv3Plugin? LoadPlugin(string uniqueId)
    {
        if (!IsPlatformSupported)
            return null;

        var info = _discoveredPlugins.Find(p => p.UniqueId == uniqueId);
        if (info == null)
            return null;

        return LoadPluginFromInfo(info);
    }

    /// <summary>
    /// Loads a plugin from plugin info.
    /// </summary>
    public AUv3Plugin? LoadPluginFromInfo(AUPluginInfo info)
    {
        if (!IsPlatformSupported || info == null)
            return null;

        try
        {
            var plugin = new AUv3Plugin(info, _sampleRate, _blockSize);
            _loadedPlugins.Add(plugin);
            return plugin;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load AudioUnit {info.Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Unloads a plugin instance.
    /// </summary>
    public void UnloadPlugin(AUv3Plugin plugin)
    {
        if (plugin == null) return;

        plugin.Dispose();
        _loadedPlugins.Remove(plugin);
    }

    /// <summary>
    /// Gets plugin info by unique ID.
    /// </summary>
    public AUPluginInfo? GetPluginInfo(string uniqueId)
    {
        return _discoveredPlugins.Find(p => p.UniqueId == uniqueId);
    }

    /// <summary>
    /// Disposes the host and all loaded plugins.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var plugin in _loadedPlugins.ToArray())
        {
            try
            {
                plugin.Dispose();
            }
            catch { }
        }
        _loadedPlugins.Clear();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer.
    /// </summary>
    ~AUv3Host()
    {
        Dispose();
    }
}
