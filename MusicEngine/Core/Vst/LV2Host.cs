// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace MusicEngine.Core.Vst;

/// <summary>
/// LV2 plugin class types.
/// </summary>
[Flags]
public enum LV2PluginClass
{
    /// <summary>Unknown/uncategorized.</summary>
    Unknown = 0,

    /// <summary>Instrument plugin.</summary>
    Instrument = 1 << 0,

    /// <summary>Oscillator.</summary>
    Oscillator = 1 << 1,

    /// <summary>Effect plugin.</summary>
    Effect = 1 << 2,

    /// <summary>Amplifier.</summary>
    Amplifier = 1 << 3,

    /// <summary>Distortion effect.</summary>
    Distortion = 1 << 4,

    /// <summary>Waveshaper.</summary>
    Waveshaper = 1 << 5,

    /// <summary>Dynamics processor.</summary>
    Dynamics = 1 << 6,

    /// <summary>Compressor.</summary>
    Compressor = 1 << 7,

    /// <summary>Expander.</summary>
    Expander = 1 << 8,

    /// <summary>Gate.</summary>
    Gate = 1 << 9,

    /// <summary>Limiter.</summary>
    Limiter = 1 << 10,

    /// <summary>EQ.</summary>
    EQ = 1 << 11,

    /// <summary>Parametric EQ.</summary>
    ParametricEQ = 1 << 12,

    /// <summary>Filter.</summary>
    Filter = 1 << 13,

    /// <summary>Highpass filter.</summary>
    Highpass = 1 << 14,

    /// <summary>Lowpass filter.</summary>
    Lowpass = 1 << 15,

    /// <summary>Bandpass filter.</summary>
    Bandpass = 1 << 16,

    /// <summary>Delay/echo.</summary>
    Delay = 1 << 17,

    /// <summary>Reverb.</summary>
    Reverb = 1 << 18,

    /// <summary>Modulator.</summary>
    Modulator = 1 << 19,

    /// <summary>Chorus.</summary>
    Chorus = 1 << 20,

    /// <summary>Flanger.</summary>
    Flanger = 1 << 21,

    /// <summary>Phaser.</summary>
    Phaser = 1 << 22,

    /// <summary>Spatial processor.</summary>
    Spatial = 1 << 23,

    /// <summary>Pitch shifter.</summary>
    PitchShifter = 1 << 24,

    /// <summary>Utility.</summary>
    Utility = 1 << 25,

    /// <summary>Analyzer.</summary>
    Analyzer = 1 << 26,

    /// <summary>Converter.</summary>
    Converter = 1 << 27,

    /// <summary>MIDI processor.</summary>
    MIDI = 1 << 28,

    /// <summary>Simulator (amp sim, etc.).</summary>
    Simulator = 1 << 29,

    /// <summary>Generator.</summary>
    Generator = 1 << 30
}

/// <summary>
/// LV2 port types.
/// </summary>
public enum LV2PortType
{
    /// <summary>Control port (parameter).</summary>
    Control,

    /// <summary>Audio port.</summary>
    Audio,

    /// <summary>CV (control voltage) port.</summary>
    CV,

    /// <summary>Atom port (events, MIDI, etc.).</summary>
    Atom
}

/// <summary>
/// LV2 port direction.
/// </summary>
public enum LV2PortDirection
{
    /// <summary>Input port.</summary>
    Input,

    /// <summary>Output port.</summary>
    Output
}

/// <summary>
/// Information about an LV2 plugin.
/// </summary>
public class LV2PluginInfo
{
    /// <summary>Plugin URI (unique identifier).</summary>
    public string Uri { get; set; } = string.Empty;

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Author name.</summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>Author homepage URL.</summary>
    public string AuthorHomepage { get; set; } = string.Empty;

    /// <summary>Plugin license.</summary>
    public string License { get; set; } = string.Empty;

    /// <summary>Plugin bundle path.</summary>
    public string BundlePath { get; set; } = string.Empty;

    /// <summary>Binary file path.</summary>
    public string BinaryPath { get; set; } = string.Empty;

    /// <summary>Plugin classes.</summary>
    public LV2PluginClass Classes { get; set; }

    /// <summary>Number of audio inputs.</summary>
    public int AudioInputs { get; set; }

    /// <summary>Number of audio outputs.</summary>
    public int AudioOutputs { get; set; }

    /// <summary>Number of control ports.</summary>
    public int ControlPorts { get; set; }

    /// <summary>Whether the plugin has a custom UI.</summary>
    public bool HasUI { get; set; }

    /// <summary>Whether the plugin supports MIDI.</summary>
    public bool SupportsMIDI { get; set; }

    /// <summary>Whether the plugin supports state saving.</summary>
    public bool SupportsState { get; set; }

    /// <summary>Gets whether this is an instrument.</summary>
    public bool IsInstrument => (Classes & LV2PluginClass.Instrument) != 0;
}

/// <summary>
/// LV2 port information.
/// </summary>
public class LV2PortInfo
{
    /// <summary>Port index.</summary>
    public int Index { get; set; }

    /// <summary>Port symbol (programmatic name).</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Port display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Port type.</summary>
    public LV2PortType Type { get; set; }

    /// <summary>Port direction.</summary>
    public LV2PortDirection Direction { get; set; }

    /// <summary>Minimum value (for control ports).</summary>
    public float MinValue { get; set; }

    /// <summary>Maximum value (for control ports).</summary>
    public float MaxValue { get; set; }

    /// <summary>Default value (for control ports).</summary>
    public float DefaultValue { get; set; }

    /// <summary>Whether the port is logarithmic.</summary>
    public bool IsLogarithmic { get; set; }

    /// <summary>Whether the port is an integer.</summary>
    public bool IsInteger { get; set; }

    /// <summary>Whether the port is a toggle (boolean).</summary>
    public bool IsToggle { get; set; }

    /// <summary>Whether the port is optional.</summary>
    public bool IsOptional { get; set; }

    /// <summary>Scale points for enumerated values.</summary>
    public List<(float Value, string Label)>? ScalePoints { get; set; }
}

/// <summary>
/// LV2 preset information.
/// </summary>
public class LV2Preset
{
    /// <summary>Preset URI.</summary>
    public string Uri { get; set; } = string.Empty;

    /// <summary>Preset label/name.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Preset bank (if applicable).</summary>
    public string? Bank { get; set; }

    /// <summary>Is this a factory preset?</summary>
    public bool IsFactory { get; set; }
}

/// <summary>
/// LV2 plugin instance (stub for Windows).
/// </summary>
public class LV2Plugin : ISampleProvider, IDisposable
{
    private readonly LV2PluginInfo _info;
    private readonly int _sampleRate;
    private readonly int _blockSize;
    private bool _disposed;
    private bool _isActive;
    private readonly List<LV2PortInfo> _ports = new();
    private readonly Dictionary<int, float> _portValues = new();
    private readonly List<LV2Preset> _presets = new();
    private float[] _inputBufferLeft = Array.Empty<float>();
    private float[] _inputBufferRight = Array.Empty<float>();
    private float[] _outputBufferLeft = Array.Empty<float>();
    private float[] _outputBufferRight = Array.Empty<float>();

    /// <summary>Gets the plugin information.</summary>
    public LV2PluginInfo Info => _info;

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

    /// <summary>Gets the port count.</summary>
    public int PortCount => _ports.Count;

    /// <summary>Gets available presets.</summary>
    public IReadOnlyList<LV2Preset> Presets => _presets.AsReadOnly();

    internal LV2Plugin(LV2PluginInfo info, int sampleRate, int blockSize)
    {
        _info = info;
        _sampleRate = sampleRate;
        _blockSize = blockSize;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2);

        _inputBufferLeft = new float[blockSize];
        _inputBufferRight = new float[blockSize];
        _outputBufferLeft = new float[blockSize];
        _outputBufferRight = new float[blockSize];
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
    /// Gets port information by index.
    /// </summary>
    public LV2PortInfo? GetPortInfo(int index)
    {
        if (index < 0 || index >= _ports.Count)
            return null;
        return _ports[index];
    }

    /// <summary>
    /// Gets port information by symbol.
    /// </summary>
    public LV2PortInfo? GetPortBySymbol(string symbol)
    {
        return _ports.Find(p => p.Symbol == symbol);
    }

    /// <summary>
    /// Gets a control port value.
    /// </summary>
    public float GetPortValue(int index)
    {
        return _portValues.TryGetValue(index, out var value) ? value : 0f;
    }

    /// <summary>
    /// Sets a control port value.
    /// </summary>
    public void SetPortValue(int index, float value)
    {
        var port = GetPortInfo(index);
        if (port != null && port.Type == LV2PortType.Control && port.Direction == LV2PortDirection.Input)
        {
            value = Math.Clamp(value, port.MinValue, port.MaxValue);
            _portValues[index] = value;
        }
    }

    /// <summary>
    /// Sets a control port value by symbol.
    /// </summary>
    public void SetPortValue(string symbol, float value)
    {
        var port = GetPortBySymbol(symbol);
        if (port != null)
        {
            SetPortValue(port.Index, value);
        }
    }

    /// <summary>
    /// Loads a preset by URI.
    /// </summary>
    public bool LoadPreset(string presetUri)
    {
        // Stub - would load preset state on supported platforms
        return false;
    }

    /// <summary>
    /// Saves the current state.
    /// </summary>
    public byte[]? SaveState()
    {
        // Stub - would save state on supported platforms
        return null;
    }

    /// <summary>
    /// Restores state from data.
    /// </summary>
    public bool RestoreState(byte[] state)
    {
        // Stub - would restore state on supported platforms
        return false;
    }

    /// <summary>
    /// Sends a MIDI note on event.
    /// </summary>
    public void NoteOn(int note, int velocity, int channel = 0)
    {
        // Stub - would send MIDI via atom port on supported platforms
    }

    /// <summary>
    /// Sends a MIDI note off event.
    /// </summary>
    public void NoteOff(int note, int channel = 0)
    {
        // Stub - would send MIDI via atom port on supported platforms
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

        int samplesPerChannel = count / 2;
        int totalProcessed = 0;

        while (totalProcessed < samplesPerChannel)
        {
            int toProcess = Math.Min(_blockSize, samplesPerChannel - totalProcessed);

            // Read input
            if (InputProvider != null)
            {
                var inputBuffer = new float[toProcess * 2];
                int read = InputProvider.Read(inputBuffer, 0, toProcess * 2);

                // Deinterleave
                for (int i = 0; i < toProcess; i++)
                {
                    _inputBufferLeft[i] = inputBuffer[i * 2];
                    _inputBufferRight[i] = inputBuffer[i * 2 + 1];
                }
            }
            else
            {
                Array.Clear(_inputBufferLeft, 0, toProcess);
                Array.Clear(_inputBufferRight, 0, toProcess);
            }

            // Stub processing - pass through
            Array.Copy(_inputBufferLeft, _outputBufferLeft, toProcess);
            Array.Copy(_inputBufferRight, _outputBufferRight, toProcess);

            // Interleave output
            int destOffset = offset + totalProcessed * 2;
            for (int i = 0; i < toProcess; i++)
            {
                buffer[destOffset + i * 2] = _outputBufferLeft[i] * MasterVolume;
                buffer[destOffset + i * 2 + 1] = _outputBufferRight[i] * MasterVolume;
            }

            totalProcessed += toProcess;
        }

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
    ~LV2Plugin()
    {
        Dispose();
    }
}

/// <summary>
/// LV2 plugin format host implementation.
/// </summary>
/// <remarks>
/// This is a stub/placeholder implementation for Windows.
/// LV2 plugins are primarily available on Linux, though some
/// cross-platform support exists.
///
/// On supported platforms (Linux primarily), this would use lilv
/// or a similar library to:
/// - Discover installed LV2 plugins
/// - Load plugin bundles
/// - Connect audio/control/atom ports
/// - Process audio
/// - Handle MIDI via atom ports
/// - Manage presets and state
/// </remarks>
public class LV2Host : IDisposable
{
    private readonly List<LV2PluginInfo> _discoveredPlugins = new();
    private readonly List<LV2Plugin> _loadedPlugins = new();
    private readonly int _sampleRate;
    private readonly int _blockSize;
    private bool _disposed;

    /// <summary>Gets discovered plugins.</summary>
    public IReadOnlyList<LV2PluginInfo> DiscoveredPlugins => _discoveredPlugins.AsReadOnly();

    /// <summary>Gets loaded plugin instances.</summary>
    public IReadOnlyList<LV2Plugin> LoadedPlugins => _loadedPlugins.AsReadOnly();

    /// <summary>Gets the sample rate.</summary>
    public int SampleRate => _sampleRate;

    /// <summary>Gets the block size.</summary>
    public int BlockSize => _blockSize;

    /// <summary>Gets whether LV2 is supported on this platform.</summary>
    public static bool IsPlatformSupported => false; // Would be true on Linux

    /// <summary>Event raised during plugin scanning.</summary>
    public event Action<float, string>? ScanProgress;

    /// <summary>Event raised when a plugin is discovered.</summary>
    public event Action<LV2PluginInfo>? PluginDiscovered;

    /// <summary>
    /// Creates a new LV2 host.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate.</param>
    /// <param name="blockSize">Processing block size.</param>
    public LV2Host(int sampleRate = 44100, int blockSize = 512)
    {
        _sampleRate = sampleRate;
        _blockSize = blockSize;
    }

    /// <summary>
    /// Scans for installed LV2 plugins.
    /// </summary>
    /// <param name="additionalPaths">Additional paths to scan.</param>
    /// <remarks>
    /// On Windows, this will complete immediately with no plugins found.
    /// On Linux, this would scan standard LV2 paths and any additional paths.
    /// Standard paths include:
    /// - ~/.lv2
    /// - /usr/lib/lv2
    /// - /usr/local/lib/lv2
    /// </remarks>
    public void ScanForPlugins(IEnumerable<string>? additionalPaths = null)
    {
        _discoveredPlugins.Clear();

        ScanProgress?.Invoke(0f, "Checking platform support...");

        if (!IsPlatformSupported)
        {
            ScanProgress?.Invoke(1f, "LV2 not supported on this platform");
            return;
        }

        // On Linux, this would:
        // 1. Create a lilv world
        // 2. Load all discovered plugins
        // 3. Enumerate and create LV2PluginInfo for each

        ScanProgress?.Invoke(1f, $"Found {_discoveredPlugins.Count} plugins");
    }

    /// <summary>
    /// Gets plugins by class.
    /// </summary>
    public IEnumerable<LV2PluginInfo> GetPluginsByClass(LV2PluginClass pluginClass)
    {
        foreach (var plugin in _discoveredPlugins)
        {
            if ((plugin.Classes & pluginClass) != 0)
                yield return plugin;
        }
    }

    /// <summary>
    /// Gets all instrument plugins.
    /// </summary>
    public IEnumerable<LV2PluginInfo> GetInstruments()
    {
        return GetPluginsByClass(LV2PluginClass.Instrument);
    }

    /// <summary>
    /// Gets all effect plugins.
    /// </summary>
    public IEnumerable<LV2PluginInfo> GetEffects()
    {
        foreach (var plugin in _discoveredPlugins)
        {
            if (!plugin.IsInstrument && plugin.AudioOutputs > 0)
                yield return plugin;
        }
    }

    /// <summary>
    /// Loads a plugin by URI.
    /// </summary>
    /// <param name="uri">The plugin's URI.</param>
    /// <returns>The loaded plugin or null if not found/supported.</returns>
    public LV2Plugin? LoadPlugin(string uri)
    {
        if (!IsPlatformSupported)
            return null;

        var info = _discoveredPlugins.Find(p => p.Uri == uri);
        if (info == null)
            return null;

        return LoadPluginFromInfo(info);
    }

    /// <summary>
    /// Loads a plugin from plugin info.
    /// </summary>
    public LV2Plugin? LoadPluginFromInfo(LV2PluginInfo info)
    {
        if (!IsPlatformSupported || info == null)
            return null;

        try
        {
            var plugin = new LV2Plugin(info, _sampleRate, _blockSize);
            _loadedPlugins.Add(plugin);
            return plugin;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load LV2 plugin {info.Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Unloads a plugin instance.
    /// </summary>
    public void UnloadPlugin(LV2Plugin plugin)
    {
        if (plugin == null) return;

        plugin.Dispose();
        _loadedPlugins.Remove(plugin);
    }

    /// <summary>
    /// Gets plugin info by URI.
    /// </summary>
    public LV2PluginInfo? GetPluginInfo(string uri)
    {
        return _discoveredPlugins.Find(p => p.Uri == uri);
    }

    /// <summary>
    /// Gets the default LV2 search paths for the current platform.
    /// </summary>
    public static IEnumerable<string> GetDefaultPaths()
    {
        var paths = new List<string>();

        // Linux paths
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home))
        {
            paths.Add(System.IO.Path.Combine(home, ".lv2"));
        }

        paths.Add("/usr/lib/lv2");
        paths.Add("/usr/local/lib/lv2");
        paths.Add("/usr/lib/x86_64-linux-gnu/lv2");

        // Check LV2_PATH environment variable
        var lv2Path = Environment.GetEnvironmentVariable("LV2_PATH");
        if (!string.IsNullOrEmpty(lv2Path))
        {
            foreach (var path in lv2Path.Split(':'))
            {
                if (!string.IsNullOrEmpty(path))
                    paths.Add(path);
            }
        }

        return paths;
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
    ~LV2Host()
    {
        Dispose();
    }
}
