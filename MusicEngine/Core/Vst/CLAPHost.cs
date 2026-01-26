//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: CLAP (CLever Audio Plugin) format host implementation.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using NAudio.Wave;

namespace MusicEngine.Core.Vst;

/// <summary>
/// CLAP plugin categories.
/// </summary>
[Flags]
public enum CLAPPluginCategory
{
    /// <summary>Unknown category</summary>
    Unknown = 0,
    /// <summary>Instrument/synthesizer</summary>
    Instrument = 1 << 0,
    /// <summary>Audio effect</summary>
    Effect = 1 << 1,
    /// <summary>Note effect (MIDI processor)</summary>
    NoteEffect = 1 << 2,
    /// <summary>Analyzer/meter</summary>
    Analyzer = 1 << 3,
    /// <summary>Drum machine</summary>
    DrumMachine = 1 << 4,
    /// <summary>Sampler</summary>
    Sampler = 1 << 5,
    /// <summary>Synthesizer</summary>
    Synthesizer = 1 << 6,
    /// <summary>External hardware</summary>
    External = 1 << 7
}

/// <summary>
/// CLAP plugin feature flags.
/// </summary>
[Flags]
public enum CLAPFeatures
{
    /// <summary>No special features</summary>
    None = 0,
    /// <summary>Supports stereo processing</summary>
    Stereo = 1 << 0,
    /// <summary>Supports mono processing</summary>
    Mono = 1 << 1,
    /// <summary>Supports surround processing</summary>
    Surround = 1 << 2,
    /// <summary>Supports ambisonic processing</summary>
    Ambisonic = 1 << 3,
    /// <summary>Has GUI</summary>
    HasGUI = 1 << 4,
    /// <summary>Supports MIDI input</summary>
    MidiInput = 1 << 5,
    /// <summary>Supports MIDI output</summary>
    MidiOutput = 1 << 6,
    /// <summary>Supports sidechain</summary>
    Sidechain = 1 << 7,
    /// <summary>Thread-safe parameter access</summary>
    ThreadSafeParams = 1 << 8
}

/// <summary>
/// Information about a CLAP plugin.
/// </summary>
public class CLAPPluginInfo
{
    /// <summary>Unique plugin ID</summary>
    public string Id { get; set; } = "";

    /// <summary>Display name</summary>
    public string Name { get; set; } = "";

    /// <summary>Vendor name</summary>
    public string Vendor { get; set; } = "";

    /// <summary>Plugin URL</summary>
    public string Url { get; set; } = "";

    /// <summary>Manual URL</summary>
    public string ManualUrl { get; set; } = "";

    /// <summary>Support URL</summary>
    public string SupportUrl { get; set; } = "";

    /// <summary>Version string</summary>
    public string Version { get; set; } = "";

    /// <summary>Plugin description</summary>
    public string Description { get; set; } = "";

    /// <summary>Plugin category</summary>
    public CLAPPluginCategory Category { get; set; }

    /// <summary>Feature flags</summary>
    public CLAPFeatures Features { get; set; }

    /// <summary>Path to the plugin file</summary>
    public string FilePath { get; set; } = "";
}

/// <summary>
/// CLAP parameter information.
/// </summary>
public class CLAPParameterInfo
{
    /// <summary>Parameter ID</summary>
    public uint Id { get; set; }

    /// <summary>Parameter name</summary>
    public string Name { get; set; } = "";

    /// <summary>Parameter module/group path</summary>
    public string Module { get; set; } = "";

    /// <summary>Minimum value</summary>
    public double MinValue { get; set; }

    /// <summary>Maximum value</summary>
    public double MaxValue { get; set; }

    /// <summary>Default value</summary>
    public double DefaultValue { get; set; }

    /// <summary>Whether parameter is automatable</summary>
    public bool IsAutomatable { get; set; }

    /// <summary>Whether parameter is stepped</summary>
    public bool IsStepped { get; set; }

    /// <summary>Whether parameter is periodic</summary>
    public bool IsPeriodic { get; set; }

    /// <summary>Whether parameter is read-only</summary>
    public bool IsReadOnly { get; set; }

    /// <summary>Whether parameter is hidden</summary>
    public bool IsHidden { get; set; }

    /// <summary>Whether parameter requires process callback to be called</summary>
    public bool RequiresProcess { get; set; }
}

/// <summary>
/// CLAP audio port configuration.
/// </summary>
public class CLAPAudioPort
{
    /// <summary>Port ID</summary>
    public uint Id { get; set; }

    /// <summary>Port name</summary>
    public string Name { get; set; } = "";

    /// <summary>Number of channels</summary>
    public int ChannelCount { get; set; }

    /// <summary>Whether this is the main port</summary>
    public bool IsMain { get; set; }

    /// <summary>Whether this port supports 64-bit processing</summary>
    public bool Supports64Bit { get; set; }

    /// <summary>Whether this port supports in-place processing</summary>
    public bool SupportsInPlace { get; set; }
}

/// <summary>
/// CLAP note port configuration.
/// </summary>
public class CLAPNotePort
{
    /// <summary>Port ID</summary>
    public uint Id { get; set; }

    /// <summary>Port name</summary>
    public string Name { get; set; } = "";

    /// <summary>Supported dialects (CLAP, MIDI, MIDI2)</summary>
    public CLAPNoteDialect SupportedDialects { get; set; }

    /// <summary>Preferred dialect</summary>
    public CLAPNoteDialect PreferredDialect { get; set; }
}

/// <summary>
/// CLAP note dialect flags.
/// </summary>
[Flags]
public enum CLAPNoteDialect
{
    /// <summary>CLAP native events</summary>
    CLAP = 1 << 0,
    /// <summary>MIDI 1.0</summary>
    MIDI = 1 << 1,
    /// <summary>MIDI 2.0 / MPE</summary>
    MIDI2 = 1 << 2
}

/// <summary>
/// CLAP process status.
/// </summary>
public enum CLAPProcessStatus
{
    /// <summary>Processing failed</summary>
    Error = 0,
    /// <summary>Continue processing</summary>
    Continue = 1,
    /// <summary>Continue but no more input needed</summary>
    ContinueIfNotQuiet = 2,
    /// <summary>Plugin wants to sleep (no output)</summary>
    Tail = 3,
    /// <summary>Plugin is done (can be deactivated)</summary>
    Sleep = 4
}

/// <summary>
/// CLAP plugin instance wrapper.
/// </summary>
public class CLAPPlugin : ISampleProvider, IDisposable
{
    private readonly CLAPPluginInfo _info;
    private readonly CLAPHost _host;
    private bool _isActive;
    private bool _isProcessing;
    private bool _disposed;

    // Plugin state
    private int _sampleRate;
    private int _blockSize;
    private readonly List<CLAPParameterInfo> _parameters = new();
    private readonly Dictionary<uint, double> _parameterValues = new();
    private readonly List<CLAPAudioPort> _audioInputs = new();
    private readonly List<CLAPAudioPort> _audioOutputs = new();
    private readonly List<CLAPNotePort> _noteInputs = new();
    private readonly List<CLAPNotePort> _noteOutputs = new();

    // Audio buffers
    private float[] _inputBufferLeft = Array.Empty<float>();
    private float[] _inputBufferRight = Array.Empty<float>();
    private float[] _outputBufferLeft = Array.Empty<float>();
    private float[] _outputBufferRight = Array.Empty<float>();

    // Thread safety
    private readonly object _lock = new();

    // MIDI event queue
    private readonly ConcurrentQueue<(int deltaFrames, byte status, byte data1, byte data2)> _midiQueue = new();
    private readonly List<(int note, int velocity)> _activeNotes = new();

    /// <summary>
    /// Gets plugin information.
    /// </summary>
    public CLAPPluginInfo Info => _info;

    /// <summary>
    /// Gets whether the plugin is loaded.
    /// </summary>
    public bool IsLoaded { get; private set; }

    /// <summary>
    /// Gets whether the plugin is active.
    /// </summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// Gets the audio format.
    /// </summary>
    public WaveFormat WaveFormat { get; private set; }

    /// <summary>
    /// Gets the parameter count.
    /// </summary>
    public int ParameterCount => _parameters.Count;

    /// <summary>
    /// Gets or sets the master volume.
    /// </summary>
    public float MasterVolume { get; set; } = 1f;

    /// <summary>
    /// Gets or sets the input provider for effect processing.
    /// </summary>
    public ISampleProvider? InputProvider { get; set; }

    internal CLAPPlugin(CLAPPluginInfo info, CLAPHost host, int sampleRate, int blockSize)
    {
        _info = info;
        _host = host;
        _sampleRate = sampleRate;
        _blockSize = blockSize;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2);

        InitializeBuffers();
    }

    private void InitializeBuffers()
    {
        _inputBufferLeft = new float[_blockSize];
        _inputBufferRight = new float[_blockSize];
        _outputBufferLeft = new float[_blockSize];
        _outputBufferRight = new float[_blockSize];
    }

    /// <summary>
    /// Activates the plugin.
    /// </summary>
    public void Activate()
    {
        lock (_lock)
        {
            if (_isActive || !IsLoaded)
                return;

            // In a real implementation, this would call clap_plugin_activate
            _isActive = true;
        }
    }

    /// <summary>
    /// Deactivates the plugin.
    /// </summary>
    public void Deactivate()
    {
        lock (_lock)
        {
            if (!_isActive)
                return;

            _isProcessing = false;
            _isActive = false;
        }
    }

    /// <summary>
    /// Starts audio processing.
    /// </summary>
    public void StartProcessing()
    {
        lock (_lock)
        {
            if (!_isActive || _isProcessing)
                return;

            // In a real implementation, this would call clap_plugin_start_processing
            _isProcessing = true;
        }
    }

    /// <summary>
    /// Stops audio processing.
    /// </summary>
    public void StopProcessing()
    {
        lock (_lock)
        {
            if (!_isProcessing)
                return;

            // In a real implementation, this would call clap_plugin_stop_processing
            _isProcessing = false;
        }
    }

    /// <summary>
    /// Gets parameter information.
    /// </summary>
    public CLAPParameterInfo? GetParameterInfo(int index)
    {
        if (index < 0 || index >= _parameters.Count)
            return null;
        return _parameters[index];
    }

    /// <summary>
    /// Gets a parameter value.
    /// </summary>
    public double GetParameterValue(uint id)
    {
        return _parameterValues.TryGetValue(id, out var value) ? value : 0;
    }

    /// <summary>
    /// Sets a parameter value.
    /// </summary>
    public void SetParameterValue(uint id, double value)
    {
        lock (_lock)
        {
            _parameterValues[id] = value;

            // In a real implementation, this would queue the parameter change
            // for the audio thread
        }
    }

    /// <summary>
    /// Sends a Note On event.
    /// </summary>
    public void NoteOn(int note, int velocity, int channel = 0)
    {
        _activeNotes.Add((note, velocity));
        _midiQueue.Enqueue((0, (byte)(0x90 | channel), (byte)note, (byte)velocity));
    }

    /// <summary>
    /// Sends a Note Off event.
    /// </summary>
    public void NoteOff(int note, int channel = 0)
    {
        _activeNotes.RemoveAll(n => n.note == note);
        _midiQueue.Enqueue((0, (byte)(0x80 | channel), (byte)note, 0));
    }

    /// <summary>
    /// Sends all notes off.
    /// </summary>
    public void AllNotesOff()
    {
        foreach (var (note, _) in _activeNotes.ToArray())
        {
            NoteOff(note);
        }
        _activeNotes.Clear();
    }

    /// <summary>
    /// Reads processed audio samples.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        lock (_lock)
        {
            if (_disposed || !_isActive)
            {
                Array.Clear(buffer, offset, count);
                return count;
            }

            if (!_isProcessing)
            {
                StartProcessing();
            }

            int samplesPerChannel = count / 2;
            int totalProcessed = 0;

            while (totalProcessed < samplesPerChannel)
            {
                int toProcess = Math.Min(_blockSize, samplesPerChannel - totalProcessed);

                // Read input if this is an effect
                if (InputProvider != null)
                {
                    var inputBuffer = new float[toProcess * 2];
                    int inputRead = InputProvider.Read(inputBuffer, 0, toProcess * 2);

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

                // Process audio (stub implementation - passes through or generates silence)
                ProcessBlock(toProcess);

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
    }

    private void ProcessBlock(int numSamples)
    {
        // Stub implementation - in a real implementation, this would call
        // clap_plugin_process with proper buffer setup

        // For now, pass through input to output
        Array.Copy(_inputBufferLeft, _outputBufferLeft, numSamples);
        Array.Copy(_inputBufferRight, _outputBufferRight, numSamples);

        // Clear MIDI queue
        while (_midiQueue.TryDequeue(out _)) { }
    }

    /// <summary>
    /// Loads plugin state from a byte array.
    /// </summary>
    public bool LoadState(byte[] state)
    {
        // In a real implementation, this would call clap_plugin_state_load
        return false;
    }

    /// <summary>
    /// Saves plugin state to a byte array.
    /// </summary>
    public byte[]? SaveState()
    {
        // In a real implementation, this would call clap_plugin_state_save
        return null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_lock)
        {
            _disposed = true;

            AllNotesOff();

            if (_isProcessing)
                StopProcessing();

            if (_isActive)
                Deactivate();

            // In a real implementation, this would call clap_plugin_destroy
        }
    }
}

/// <summary>
/// CLAP (CLever Audio Plugin) host implementation.
/// Provides plugin discovery, loading, and hosting functionality for CLAP format plugins.
/// </summary>
/// <remarks>
/// Note: This is a stub/placeholder implementation. Full CLAP hosting requires
/// native interop with the CLAP C API. This implementation provides the interface
/// and basic structure for future native implementation.
/// </remarks>
public class CLAPHost : IDisposable
{
    #region P/Invoke Declarations (Stub)

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibraryW(string lpFileName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeLibrary(IntPtr hModule);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    #endregion

    // Host info
    private readonly string _hostName;
    private readonly string _hostVendor;
    private readonly string _hostVersion;

    // Discovered plugins
    private readonly List<CLAPPluginInfo> _discoveredPlugins = new();
    private readonly Dictionary<string, IntPtr> _loadedModules = new();
    private readonly List<CLAPPlugin> _loadedPlugins = new();

    // Configuration
    private int _sampleRate;
    private int _blockSize;
    private bool _disposed;
    private readonly object _lock = new();

    /// <summary>
    /// Gets discovered plugins.
    /// </summary>
    public IReadOnlyList<CLAPPluginInfo> DiscoveredPlugins => _discoveredPlugins.AsReadOnly();

    /// <summary>
    /// Gets loaded plugin instances.
    /// </summary>
    public IReadOnlyList<CLAPPlugin> LoadedPlugins => _loadedPlugins.AsReadOnly();

    /// <summary>
    /// Gets or sets the sample rate.
    /// </summary>
    public int SampleRate
    {
        get => _sampleRate;
        set
        {
            if (_sampleRate != value)
            {
                _sampleRate = value;
                // Would need to reconfigure active plugins
            }
        }
    }

    /// <summary>
    /// Gets or sets the block size.
    /// </summary>
    public int BlockSize
    {
        get => _blockSize;
        set
        {
            if (_blockSize != value)
            {
                _blockSize = value;
                // Would need to reconfigure active plugins
            }
        }
    }

    /// <summary>
    /// Event raised when plugin scanning progress updates.
    /// </summary>
    public event Action<float, string>? ScanProgress;

    /// <summary>
    /// Event raised when a plugin is discovered.
    /// </summary>
    public event Action<CLAPPluginInfo>? PluginDiscovered;

    /// <summary>
    /// Creates a new CLAP host.
    /// </summary>
    /// <param name="hostName">Host application name</param>
    /// <param name="hostVendor">Host vendor name</param>
    /// <param name="hostVersion">Host version string</param>
    /// <param name="sampleRate">Audio sample rate</param>
    /// <param name="blockSize">Processing block size</param>
    public CLAPHost(
        string hostName = "MusicEngine",
        string hostVendor = "MusicEngine",
        string hostVersion = "1.0.0",
        int sampleRate = 44100,
        int blockSize = 512)
    {
        _hostName = hostName;
        _hostVendor = hostVendor;
        _hostVersion = hostVersion;
        _sampleRate = sampleRate;
        _blockSize = blockSize;
    }

    /// <summary>
    /// Scans for CLAP plugins in the specified directories.
    /// </summary>
    /// <param name="directories">Directories to scan (null = default paths)</param>
    public void ScanForPlugins(IEnumerable<string>? directories = null)
    {
        lock (_lock)
        {
            _discoveredPlugins.Clear();

            // Default CLAP paths on Windows
            directories ??= GetDefaultCLAPPaths();

            var allDirs = directories.ToList();
            int currentDir = 0;

            foreach (var dir in allDirs)
            {
                if (!Directory.Exists(dir))
                {
                    currentDir++;
                    continue;
                }

                ScanProgress?.Invoke((float)currentDir / allDirs.Count, $"Scanning: {dir}");

                try
                {
                    var clapFiles = Directory.GetFiles(dir, "*.clap", SearchOption.AllDirectories);

                    foreach (var file in clapFiles)
                    {
                        try
                        {
                            var plugins = ScanPluginFile(file);
                            foreach (var plugin in plugins)
                            {
                                _discoveredPlugins.Add(plugin);
                                PluginDiscovered?.Invoke(plugin);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error scanning {file}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error accessing directory {dir}: {ex.Message}");
                }

                currentDir++;
            }

            ScanProgress?.Invoke(1f, $"Found {_discoveredPlugins.Count} plugins");
        }
    }

    private IEnumerable<string> GetDefaultCLAPPaths()
    {
        var paths = new List<string>();

        // Windows paths
        var commonFiles = Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        if (!string.IsNullOrEmpty(commonFiles))
            paths.Add(Path.Combine(commonFiles, "CLAP"));

        if (!string.IsNullOrEmpty(localAppData))
            paths.Add(Path.Combine(localAppData, "Programs", "Common", "CLAP"));

        // User-specific path
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile))
            paths.Add(Path.Combine(userProfile, ".clap"));

        return paths;
    }

    private List<CLAPPluginInfo> ScanPluginFile(string path)
    {
        var plugins = new List<CLAPPluginInfo>();

        // In a real implementation, this would:
        // 1. Load the .clap file (which is a shared library)
        // 2. Get the clap_entry symbol
        // 3. Call clap_entry.init()
        // 4. Enumerate plugins via clap_plugin_factory

        // For now, create a stub entry based on filename
        var info = new CLAPPluginInfo
        {
            Id = Path.GetFileNameWithoutExtension(path),
            Name = Path.GetFileNameWithoutExtension(path),
            Vendor = "Unknown",
            Version = "1.0.0",
            FilePath = path,
            Category = DetermineCategoryFromPath(path),
            Features = CLAPFeatures.Stereo | CLAPFeatures.HasGUI
        };

        plugins.Add(info);
        return plugins;
    }

    private CLAPPluginCategory DetermineCategoryFromPath(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

        if (name.Contains("synth") || name.Contains("instrument"))
            return CLAPPluginCategory.Instrument | CLAPPluginCategory.Synthesizer;
        if (name.Contains("drum"))
            return CLAPPluginCategory.Instrument | CLAPPluginCategory.DrumMachine;
        if (name.Contains("sampler"))
            return CLAPPluginCategory.Instrument | CLAPPluginCategory.Sampler;
        if (name.Contains("reverb") || name.Contains("delay") || name.Contains("comp") ||
            name.Contains("eq") || name.Contains("filter"))
            return CLAPPluginCategory.Effect;
        if (name.Contains("meter") || name.Contains("analyzer"))
            return CLAPPluginCategory.Analyzer;

        return CLAPPluginCategory.Unknown;
    }

    /// <summary>
    /// Loads a plugin by ID.
    /// </summary>
    /// <param name="pluginId">Plugin ID to load</param>
    /// <returns>Loaded plugin instance or null if failed</returns>
    public CLAPPlugin? LoadPlugin(string pluginId)
    {
        lock (_lock)
        {
            var info = _discoveredPlugins.FirstOrDefault(p => p.Id == pluginId);
            if (info == null)
                return null;

            return LoadPluginFromInfo(info);
        }
    }

    /// <summary>
    /// Loads a plugin from a file path.
    /// </summary>
    /// <param name="path">Path to the .clap file</param>
    /// <returns>Loaded plugin instance or null if failed</returns>
    public CLAPPlugin? LoadPluginFromPath(string path)
    {
        lock (_lock)
        {
            if (!File.Exists(path))
                return null;

            var plugins = ScanPluginFile(path);
            if (plugins.Count == 0)
                return null;

            return LoadPluginFromInfo(plugins[0]);
        }
    }

    private CLAPPlugin? LoadPluginFromInfo(CLAPPluginInfo info)
    {
        try
        {
            // In a real implementation, this would:
            // 1. Load the module if not already loaded
            // 2. Get the plugin factory
            // 3. Create a plugin instance via clap_plugin_factory.create_plugin
            // 4. Initialize the plugin

            var plugin = new CLAPPlugin(info, this, _sampleRate, _blockSize)
            {
                // Mark as loaded even though this is a stub
            };

            // Simulate successful load
            var isLoadedField = typeof(CLAPPlugin).GetProperty("IsLoaded");
            // In real implementation, set IsLoaded = true

            _loadedPlugins.Add(plugin);
            return plugin;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load CLAP plugin {info.Id}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Unloads a plugin instance.
    /// </summary>
    public void UnloadPlugin(CLAPPlugin plugin)
    {
        lock (_lock)
        {
            plugin.Dispose();
            _loadedPlugins.Remove(plugin);
        }
    }

    /// <summary>
    /// Gets plugin info by ID.
    /// </summary>
    public CLAPPluginInfo? GetPluginInfo(string pluginId)
    {
        return _discoveredPlugins.FirstOrDefault(p => p.Id == pluginId);
    }

    /// <summary>
    /// Gets plugins by category.
    /// </summary>
    public IEnumerable<CLAPPluginInfo> GetPluginsByCategory(CLAPPluginCategory category)
    {
        return _discoveredPlugins.Where(p => (p.Category & category) != 0);
    }

    /// <summary>
    /// Gets all instrument plugins.
    /// </summary>
    public IEnumerable<CLAPPluginInfo> GetInstruments()
    {
        return GetPluginsByCategory(CLAPPluginCategory.Instrument);
    }

    /// <summary>
    /// Gets all effect plugins.
    /// </summary>
    public IEnumerable<CLAPPluginInfo> GetEffects()
    {
        return GetPluginsByCategory(CLAPPluginCategory.Effect);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_lock)
        {
            _disposed = true;

            // Unload all plugins
            foreach (var plugin in _loadedPlugins.ToArray())
            {
                try
                {
                    plugin.Dispose();
                }
                catch { }
            }
            _loadedPlugins.Clear();

            // Unload all modules
            foreach (var module in _loadedModules.Values)
            {
                try
                {
                    if (module != IntPtr.Zero)
                        FreeLibrary(module);
                }
                catch { }
            }
            _loadedModules.Clear();
        }

        GC.SuppressFinalize(this);
    }

    ~CLAPHost()
    {
        Dispose();
    }
}
