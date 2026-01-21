//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: VST3 Plugin wrapper implementing full VST3 hosting.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using NAudio.Wave;
using MusicEngine.Core.Vst.Vst3.Interfaces;
using MusicEngine.Core.Vst.Vst3.Presets;
using MusicEngine.Core.Vst.Vst3.Structures;

namespace MusicEngine.Core;

/// <summary>
/// VST3 Plugin wrapper implementing full VST3 hosting with audio processing,
/// parameter management, and GUI support.
/// </summary>
public class Vst3Plugin : IVst3Plugin
{
    #region P/Invoke Declarations

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibraryW(string lpFileName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeLibrary(IntPtr hModule);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    /// <summary>
    /// Delegate type for GetPluginFactory export
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr GetModuleExportDelegate();

    #endregion

    #region Private Fields

    // DLL handle
    private IntPtr _moduleHandle;

    // VST3 Interface Wrappers
    private PluginFactoryWrapper? _factory;
    private ComponentWrapper? _component;
    private AudioProcessorWrapper? _audioProcessor;
    private EditControllerWrapper? _editController;

    // Host interfaces
    private Vst3ComponentHandler? _componentHandler;
    private Vst3HostApplication? _hostApplication;

    // GUI interfaces
    private PlugViewWrapper? _plugView;
    private Vst3PlugFrame? _plugFrame;

    // Processing interfaces
    private Vst3ParameterChanges? _parameterChanges;
    private Vst3EventList? _eventList;

    // Audio format
    private WaveFormat _waveFormat;

    // Audio buffers
    private float[]? _outputBufferLeft;
    private float[]? _outputBufferRight;
    private float[]? _inputBufferLeft;
    private float[]? _inputBufferRight;
    private float[]? _interleavedOutput;
    private float[]? _inputReadBuffer;
    private IntPtr[]? _inputPointers;
    private IntPtr[]? _outputPointers;
    private GCHandle _inputLeftHandle;
    private GCHandle _inputRightHandle;
    private GCHandle _outputLeftHandle;
    private GCHandle _outputRightHandle;

    // State flags
    private bool _isActive;
    private bool _isProcessing;
    private bool _isDisposed;
    private readonly object _lock = new();

    // Plugin info fields
    private readonly string _pluginPath;
    private string _name = "";
    private string _vendor = "";
    private string _version = "";
    private Guid _classId = Guid.Empty;
    private bool _isInstrument;
    private int _numAudioInputs;
    private int _numAudioOutputs;
    private int _sampleRate;
    private int _blockSize;
    private float _masterVolume = 1.0f;
    private bool _hasEditor;

    // Parameter cache
    private readonly List<Vst3ParameterInfo> _parameterInfoCache = new();
    private readonly Dictionary<uint, int> _parameterIdToIndex = new();

    // Preset management
    private readonly List<string> _presetNames = new();
    private int _currentPresetIndex;
    private string _currentPresetName = "";

    // MIDI event queue (thread-safe for multi-threaded access)
    private readonly ConcurrentQueue<MidiEventData> _midiEventQueue = new();
    private readonly List<(int note, int velocity)> _activeNotes = new();

    // Unit info
    private readonly List<Vst3UnitInfo> _units = new();
    private readonly Dictionary<int, List<int>> _unitParameters = new();

    // Input provider for effect processing
    private ISampleProvider? _inputProvider;

    // Note Expression support
    private bool _supportsNoteExpression;

    // Sidechain support
    private bool _supportsSidechain;
    private int _sidechainBusIndex = -1;

    #endregion

    #region Internal Structures

    /// <summary>
    /// Internal MIDI event data structure
    /// </summary>
    private struct MidiEventData
    {
        public int DeltaFrames;
        public byte Status;
        public byte Data1;
        public byte Data2;
    }

    #endregion

    #region Properties (IVstPlugin Implementation)

    /// <summary>
    /// Plugin name
    /// </summary>
    public string Name
    {
        get => _name;
        set => _name = value ?? "";
    }

    /// <summary>
    /// Full path to the plugin file
    /// </summary>
    public string PluginPath => _pluginPath;

    /// <summary>
    /// Plugin vendor name
    /// </summary>
    public string Vendor => _vendor;

    /// <summary>
    /// Plugin version string
    /// </summary>
    public string Version => _version;

    /// <summary>
    /// True - this is a VST3 plugin
    /// </summary>
    public bool IsVst3 => true;

    /// <summary>
    /// True if the plugin is successfully loaded and initialized
    /// </summary>
    public bool IsLoaded => _component != null && _component.IsInitialized;

    /// <summary>
    /// True if this plugin is an instrument (synthesizer), false for effects
    /// </summary>
    public bool IsInstrument => _isInstrument;

    /// <summary>
    /// Number of audio input channels
    /// </summary>
    public int NumAudioInputs => _numAudioInputs;

    /// <summary>
    /// Number of audio output channels
    /// </summary>
    public int NumAudioOutputs => _numAudioOutputs;

    /// <summary>
    /// Current sample rate
    /// </summary>
    public int SampleRate => _sampleRate;

    /// <summary>
    /// Current block size
    /// </summary>
    public int BlockSize => _blockSize;

    /// <summary>
    /// Master volume (0.0 to 2.0)
    /// </summary>
    public float MasterVolume
    {
        get => _masterVolume;
        set => _masterVolume = Math.Clamp(value, 0f, 2f);
    }

    /// <summary>
    /// True if the plugin has an editor GUI
    /// </summary>
    public bool HasEditor => _hasEditor;

    /// <summary>
    /// Current preset index
    /// </summary>
    public int CurrentPresetIndex => _currentPresetIndex;

    /// <summary>
    /// Current preset name
    /// </summary>
    public string CurrentPresetName => _currentPresetName;

    /// <summary>
    /// Gets or sets the input provider for effect processing
    /// </summary>
    public ISampleProvider? InputProvider
    {
        get => _inputProvider;
        set
        {
            lock (_lock)
            {
                _inputProvider = value;
            }
        }
    }

    /// <summary>
    /// Check if the plugin is currently active
    /// </summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// Audio format for ISampleProvider
    /// </summary>
    public WaveFormat WaveFormat => _waveFormat;

    #endregion

    #region Properties (IVst3Plugin Implementation)

    /// <summary>
    /// Check if the plugin supports Note Expression
    /// </summary>
    public bool SupportsNoteExpression => _supportsNoteExpression;

    /// <summary>
    /// Check if the plugin supports sidechain input
    /// </summary>
    public bool SupportsSidechain => _supportsSidechain;

    /// <summary>
    /// Get the sidechain bus index, or -1 if not available
    /// </summary>
    public int SidechainBusIndex => _sidechainBusIndex;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new VST3 plugin wrapper
    /// </summary>
    /// <param name="pluginPath">Path to the VST3 plugin (.vst3 file or bundle)</param>
    /// <param name="sampleRate">Sample rate to use (0 = use default from Settings)</param>
    public Vst3Plugin(string pluginPath, int sampleRate = 0)
    {
        if (string.IsNullOrEmpty(pluginPath))
            throw new ArgumentNullException(nameof(pluginPath));

        _pluginPath = pluginPath;
        _sampleRate = sampleRate > 0 ? sampleRate : Settings.SampleRate;
        _blockSize = Settings.VstBufferSize;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(_sampleRate, Settings.Channels);

        // Extract name from path if not set later
        _name = Path.GetFileNameWithoutExtension(pluginPath);

        // Initialize audio buffers
        InitializeAudioBuffers();

        // Initialize processing interfaces
        _parameterChanges = new Vst3ParameterChanges();
        _eventList = new Vst3EventList();

        // Load the plugin
        if (File.Exists(pluginPath) || Directory.Exists(pluginPath))
        {
            try
            {
                LoadPlugin(pluginPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not load VST3 plugin '{pluginPath}': {ex.Message}");
                // Plugin remains unloaded but object is still valid
            }
        }
    }

    #endregion

    #region Plugin Loading

    /// <summary>
    /// Load and initialize the VST3 plugin
    /// </summary>
    private void LoadPlugin(string path)
    {
        // Resolve the actual DLL path (VST3 can be a bundle on some platforms)
        string dllPath = ResolveVst3DllPath(path);

        // Load the DLL
        _moduleHandle = LoadLibraryW(dllPath);
        if (_moduleHandle == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error();
            throw new Exception($"Failed to load VST3 DLL '{dllPath}'. Error code: {error}");
        }

        try
        {
            // Get GetPluginFactory export
            IntPtr factoryProc = GetProcAddress(_moduleHandle, "GetPluginFactory");
            if (factoryProc == IntPtr.Zero)
            {
                throw new Exception("VST3 plugin has no GetPluginFactory export");
            }

            // Call GetPluginFactory to get the factory interface
            var getFactoryDelegate = Marshal.GetDelegateForFunctionPointer<GetModuleExportDelegate>(factoryProc);
            IntPtr factoryPtr = getFactoryDelegate();

            if (factoryPtr == IntPtr.Zero)
            {
                throw new Exception("GetPluginFactory returned null");
            }

            // Create PluginFactoryWrapper
            _factory = new PluginFactoryWrapper(factoryPtr);

            // Get factory info
            if (_factory.GetFactoryInfo(out Vst3FactoryInfo factoryInfo) == (int)Vst3Result.Ok)
            {
                _vendor = factoryInfo.Vendor ?? "";
            }

            // Create host application
            _hostApplication = new Vst3HostApplication();

            // Set host context if factory supports it (IPluginFactory3)
            if (_factory.IsFactory3)
            {
                _factory.SetHostContext(_hostApplication.NativePtr);
            }

            // Find audio component class and create component instance
            FindAndCreateAudioComponent();

            // Initialize component with host context
            InitializeComponent();

            // Setup audio buses
            SetupAudioBuses();

            // Get controller class ID and create/connect controller
            CreateAndConnectController();

            // Cache parameter information
            CacheParameterInfo();

            // Check for optional interfaces
            CheckOptionalInterfaces();

            // Setup processing
            SetupProcessing();

            Console.WriteLine($"Loaded VST3 plugin: {_name} by {_vendor} v{_version}");
        }
        catch
        {
            // Clean up on failure
            CleanupPlugin();
            throw;
        }
    }

    /// <summary>
    /// Resolve the actual DLL path for a VST3 plugin (handles bundles)
    /// </summary>
    private string ResolveVst3DllPath(string path)
    {
        // On Windows, VST3 can be either a .vst3 bundle (folder) or a direct DLL
        if (Directory.Exists(path))
        {
            // It's a bundle - look for the DLL inside
            // Standard VST3 bundle structure: plugin.vst3/Contents/x86_64-win/plugin.vst3
            string arch = Environment.Is64BitProcess ? "x86_64-win" : "x86-win";
            string bundleDll = Path.Combine(path, "Contents", arch, Path.GetFileName(path));

            if (File.Exists(bundleDll))
                return bundleDll;

            // Try alternative naming
            bundleDll = Path.Combine(path, "Contents", arch,
                Path.GetFileNameWithoutExtension(path) + ".vst3");
            if (File.Exists(bundleDll))
                return bundleDll;

            // Fallback: look for any .vst3 in the architecture folder
            string archFolder = Path.Combine(path, "Contents", arch);
            if (Directory.Exists(archFolder))
            {
                var vst3Files = Directory.GetFiles(archFolder, "*.vst3");
                if (vst3Files.Length > 0)
                    return vst3Files[0];
            }

            throw new FileNotFoundException($"Could not find VST3 DLL in bundle: {path}");
        }

        // It's a direct file
        if (!File.Exists(path))
            throw new FileNotFoundException($"VST3 file not found: {path}");

        return path;
    }

    /// <summary>
    /// Find the audio component class and create an instance
    /// </summary>
    private void FindAndCreateAudioComponent()
    {
        if (_factory == null)
            throw new InvalidOperationException("Factory not initialized");

        int classCount = _factory.CountClasses();

        Vst3Tuid? audioClassCid = null;
        string className = "";
        string classVersion = "";
        string subCategories = "";

        // Search for an audio processor class
        for (int i = 0; i < classCount; i++)
        {
            // Try to get extended info first (Factory2/3)
            if (_factory.IsFactory2)
            {
                if (_factory.GetClassInfo2(i, out Vst3ClassInfo2 info2) == (int)Vst3Result.Ok)
                {
                    // Check if this is an audio module component
                    if (info2.Category != null &&
                        info2.Category.Equals("Audio Module Class", StringComparison.OrdinalIgnoreCase))
                    {
                        audioClassCid = info2.Cid;
                        className = info2.Name ?? "";
                        classVersion = info2.Version ?? "";
                        subCategories = info2.SubCategories ?? "";
                        break;
                    }
                }
            }

            // Fall back to basic info
            if (_factory.GetClassInfo(i, out Vst3ClassInfo info) == (int)Vst3Result.Ok)
            {
                if (info.Category != null &&
                    info.Category.Equals("Audio Module Class", StringComparison.OrdinalIgnoreCase))
                {
                    audioClassCid = info.Cid;
                    className = info.Name ?? "";
                    break;
                }
            }
        }

        if (!audioClassCid.HasValue)
        {
            throw new Exception("No audio component class found in VST3 plugin");
        }

        // Store class info
        _classId = audioClassCid.Value.ToGuid();
        if (!string.IsNullOrEmpty(className))
            _name = className;
        if (!string.IsNullOrEmpty(classVersion))
            _version = classVersion;

        // Determine if this is an instrument based on sub-categories
        _isInstrument = subCategories.Contains("Instrument", StringComparison.OrdinalIgnoreCase) ||
                        subCategories.Contains("Synth", StringComparison.OrdinalIgnoreCase);

        // Create the component instance
        var cid = audioClassCid.Value;
        var iid = Vst3Guids.IComponent;

        int result = _factory.CreateInstance(cid, iid, out IntPtr componentPtr);
        if (result != (int)Vst3Result.Ok || componentPtr == IntPtr.Zero)
        {
            throw new Exception($"Failed to create component instance. Result: {result}");
        }

        _component = new ComponentWrapper(componentPtr);
    }

    /// <summary>
    /// Initialize the component with host context
    /// </summary>
    private void InitializeComponent()
    {
        if (_component == null || _hostApplication == null)
            throw new InvalidOperationException("Component or host not initialized");

        int result = _component.Initialize(_hostApplication.NativePtr);
        if (result != (int)Vst3Result.Ok)
        {
            throw new Exception($"Failed to initialize component. Result: {result}");
        }

        // Query for IAudioProcessor interface
        if (_component.QueryInterface(Vst3Guids.IAudioProcessor, out IntPtr processorPtr) == (int)Vst3Result.Ok &&
            processorPtr != IntPtr.Zero)
        {
            _audioProcessor = new AudioProcessorWrapper(processorPtr);
        }
        else
        {
            throw new Exception("Component does not support IAudioProcessor");
        }
    }

    /// <summary>
    /// Setup audio buses (inputs and outputs)
    /// </summary>
    private void SetupAudioBuses()
    {
        if (_component == null)
            return;

        // Count audio buses
        int inputBusCount = _component.GetBusCount(Vst3MediaType.Audio, Vst3BusDirection.Input);
        int outputBusCount = _component.GetBusCount(Vst3MediaType.Audio, Vst3BusDirection.Output);

        _numAudioInputs = 0;
        _numAudioOutputs = 0;

        // Activate and count channels for input buses
        for (int i = 0; i < inputBusCount; i++)
        {
            if (_component.GetBusInfo(Vst3MediaType.Audio, Vst3BusDirection.Input, i, out Vst3BusInfo busInfo) ==
                (int)Vst3Result.Ok)
            {
                // Check for sidechain
                if (busInfo.BusType == Vst3BusType.Aux)
                {
                    _supportsSidechain = true;
                    _sidechainBusIndex = i;
                }

                // Activate the bus
                _component.ActivateBus(Vst3MediaType.Audio, Vst3BusDirection.Input, i,
                    busInfo.IsDefaultActive || busInfo.BusType == Vst3BusType.Main);

                if (busInfo.BusType == Vst3BusType.Main)
                {
                    _numAudioInputs += busInfo.ChannelCount;
                }
            }
        }

        // Activate and count channels for output buses
        for (int i = 0; i < outputBusCount; i++)
        {
            if (_component.GetBusInfo(Vst3MediaType.Audio, Vst3BusDirection.Output, i, out Vst3BusInfo busInfo) ==
                (int)Vst3Result.Ok)
            {
                // Activate the bus
                _component.ActivateBus(Vst3MediaType.Audio, Vst3BusDirection.Output, i,
                    busInfo.IsDefaultActive || busInfo.BusType == Vst3BusType.Main);

                if (busInfo.BusType == Vst3BusType.Main)
                {
                    _numAudioOutputs += busInfo.ChannelCount;
                }
            }
        }

        // Activate event bus if present (for instruments)
        int eventInputBusCount = _component.GetBusCount(Vst3MediaType.Event, Vst3BusDirection.Input);
        for (int i = 0; i < eventInputBusCount; i++)
        {
            _component.ActivateBus(Vst3MediaType.Event, Vst3BusDirection.Input, i, true);
        }

        // Set speaker arrangements on the audio processor
        if (_audioProcessor != null)
        {
            // Default to stereo
            ulong[] inputArrangements = inputBusCount > 0 ? new ulong[inputBusCount] : Array.Empty<ulong>();
            ulong[] outputArrangements = outputBusCount > 0 ? new ulong[outputBusCount] : Array.Empty<ulong>();

            for (int i = 0; i < inputArrangements.Length; i++)
            {
                inputArrangements[i] = Vst3SpeakerArrangement.Stereo;
            }

            for (int i = 0; i < outputArrangements.Length; i++)
            {
                outputArrangements[i] = Vst3SpeakerArrangement.Stereo;
            }

            _audioProcessor.SetBusArrangements(inputArrangements, outputArrangements);
        }
    }

    /// <summary>
    /// Get controller class ID and create/connect the edit controller
    /// </summary>
    private void CreateAndConnectController()
    {
        if (_component == null || _factory == null)
            return;

        // Get the controller class ID from the component
        Vst3Tuid controllerClassId = default;
        bool hasControllerClass = _component.GetControllerClassId(out controllerClassId) == (int)Vst3Result.Ok &&
                                  !controllerClassId.IsEmpty();

        IntPtr controllerPtr = IntPtr.Zero;

        if (hasControllerClass)
        {
            // Create separate controller instance
            var iid = Vst3Guids.IEditController;
            if (_factory.CreateInstance(controllerClassId, iid, out controllerPtr) != (int)Vst3Result.Ok)
            {
                controllerPtr = IntPtr.Zero;
            }
        }

        // If no separate controller, try to query from component (single component design)
        if (controllerPtr == IntPtr.Zero)
        {
            if (_component.QueryInterface(Vst3Guids.IEditController, out controllerPtr) != (int)Vst3Result.Ok)
            {
                controllerPtr = IntPtr.Zero;
            }
        }

        if (controllerPtr != IntPtr.Zero)
        {
            _editController = new EditControllerWrapper(controllerPtr);

            // Initialize the controller if it's a separate instance
            if (hasControllerClass && _hostApplication != null)
            {
                _editController.Initialize(_hostApplication.NativePtr);
            }

            // Create and set component handler
            _componentHandler = new Vst3ComponentHandler();
            _componentHandler.ParameterEdited += OnParameterEdited;
            _componentHandler.RestartRequested += OnRestartRequested;

            _editController.SetComponentHandler(_componentHandler.NativePtr);

            // Connect component and controller via IConnectionPoint if available
            ConnectComponentAndController();

            // Check if editor is available
            IntPtr viewPtr = _editController.CreateView("editor");
            if (viewPtr != IntPtr.Zero)
            {
                _hasEditor = true;
                // Release the view for now - we'll create it again when needed
                _plugView = new PlugViewWrapper(viewPtr);
                _plugView.Release();
                _plugView = null;
            }
        }
    }

    /// <summary>
    /// Connect component and controller using IConnectionPoint if available
    /// </summary>
    private void ConnectComponentAndController()
    {
        if (_component == null || _editController == null)
            return;

        // Try to get IConnectionPoint from component
        if (_component.QueryInterface(Vst3Guids.IConnectionPoint, out IntPtr compConnPtPtr) == (int)Vst3Result.Ok &&
            compConnPtPtr != IntPtr.Zero)
        {
            // Try to get IConnectionPoint from controller
            if (_editController.QueryInterface(Vst3Guids.IConnectionPoint, out IntPtr ctrlConnPtPtr) ==
                (int)Vst3Result.Ok && ctrlConnPtPtr != IntPtr.Zero)
            {
                // Note: Full IConnectionPoint implementation would connect them here
                // For now, we rely on the component handler for parameter changes
            }
        }
    }

    /// <summary>
    /// Cache parameter information from the edit controller
    /// </summary>
    private void CacheParameterInfo()
    {
        if (_editController == null)
            return;

        _parameterInfoCache.Clear();
        _parameterIdToIndex.Clear();

        int paramCount = _editController.GetParameterCount();

        for (int i = 0; i < paramCount; i++)
        {
            if (_editController.GetParameterInfo(i, out Vst3ParameterInfo paramInfo) == (int)Vst3Result.Ok)
            {
                _parameterInfoCache.Add(paramInfo);
                _parameterIdToIndex[paramInfo.Id] = i;

                // Group by unit
                if (!_unitParameters.ContainsKey(paramInfo.UnitId))
                {
                    _unitParameters[paramInfo.UnitId] = new List<int>();
                }
                _unitParameters[paramInfo.UnitId].Add(i);
            }
        }
    }

    /// <summary>
    /// Check for optional interfaces (Note Expression, etc.)
    /// </summary>
    private void CheckOptionalInterfaces()
    {
        if (_editController == null)
            return;

        // Check for Note Expression support
        if (_editController.QueryInterface(Vst3Guids.INoteExpressionController, out IntPtr noteExprPtr) ==
            (int)Vst3Result.Ok && noteExprPtr != IntPtr.Zero)
        {
            _supportsNoteExpression = true;
            // Release the reference - we'll query again when needed
        }

        // Check for Unit Info
        if (_editController.QueryInterface(Vst3Guids.IUnitInfo, out IntPtr unitInfoPtr) == (int)Vst3Result.Ok &&
            unitInfoPtr != IntPtr.Zero)
        {
            // Could enumerate units here
        }
    }

    /// <summary>
    /// Setup the audio processing configuration
    /// </summary>
    private void SetupProcessing()
    {
        if (_audioProcessor == null)
            return;

        // Check if 32-bit processing is supported
        if (_audioProcessor.CanProcessSampleSize(Vst3SymbolicSampleSize.Sample32) != (int)Vst3Result.Ok)
        {
            Console.WriteLine("Warning: Plugin may not support 32-bit float processing");
        }

        // Setup processing parameters
        var setup = new ProcessSetup
        {
            ProcessMode = (int)Vst3ProcessMode.Realtime,
            SymbolicSampleSize = (int)Vst3SymbolicSampleSize.Sample32,
            MaxSamplesPerBlock = _blockSize,
            SampleRate = _sampleRate
        };

        int result = _audioProcessor.SetupProcessing(ref setup);
        if (result != (int)Vst3Result.Ok)
        {
            Console.WriteLine($"Warning: SetupProcessing returned {result}");
        }
    }

    /// <summary>
    /// Initialize audio buffers for processing
    /// </summary>
    private void InitializeAudioBuffers()
    {
        int bufferSize = _blockSize > 0 ? _blockSize : Settings.VstBufferSize;

        _outputBufferLeft = new float[bufferSize];
        _outputBufferRight = new float[bufferSize];
        _inputBufferLeft = new float[bufferSize];
        _inputBufferRight = new float[bufferSize];
        _interleavedOutput = new float[bufferSize * 2];
        _inputReadBuffer = new float[bufferSize * 2];

        _inputPointers = new IntPtr[2];
        _outputPointers = new IntPtr[2];

        // Pin buffers for native access
        _inputLeftHandle = GCHandle.Alloc(_inputBufferLeft, GCHandleType.Pinned);
        _inputRightHandle = GCHandle.Alloc(_inputBufferRight, GCHandleType.Pinned);
        _outputLeftHandle = GCHandle.Alloc(_outputBufferLeft, GCHandleType.Pinned);
        _outputRightHandle = GCHandle.Alloc(_outputBufferRight, GCHandleType.Pinned);

        _inputPointers[0] = _inputLeftHandle.AddrOfPinnedObject();
        _inputPointers[1] = _inputRightHandle.AddrOfPinnedObject();
        _outputPointers[0] = _outputLeftHandle.AddrOfPinnedObject();
        _outputPointers[1] = _outputRightHandle.AddrOfPinnedObject();
    }

    /// <summary>
    /// Cleanup plugin resources on failure or dispose
    /// </summary>
    private void CleanupPlugin()
    {
        // Deactivate first
        if (_isActive)
        {
            try
            {
                Deactivate();
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }

        // Dispose edit controller
        _editController?.Dispose();
        _editController = null;

        // Dispose audio processor
        _audioProcessor?.Dispose();
        _audioProcessor = null;

        // Dispose component
        _component?.Dispose();
        _component = null;

        // Dispose factory
        _factory?.Dispose();
        _factory = null;

        // Dispose host interfaces
        _componentHandler?.Dispose();
        _componentHandler = null;

        _hostApplication?.Dispose();
        _hostApplication = null;

        // Dispose processing interfaces
        _parameterChanges?.Dispose();
        _parameterChanges = null;

        _eventList?.Dispose();
        _eventList = null;

        // Dispose GUI
        _plugView?.Dispose();
        _plugView = null;

        _plugFrame?.Dispose();
        _plugFrame = null;

        // Unload DLL
        if (_moduleHandle != IntPtr.Zero)
        {
            FreeLibrary(_moduleHandle);
            _moduleHandle = IntPtr.Zero;
        }
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handle parameter edit notifications from the plugin
    /// </summary>
    private void OnParameterEdited(object? sender, ParameterEditEventArgs e)
    {
        // The plugin has changed a parameter value
        // Could use this to update UI or record automation
    }

    /// <summary>
    /// Handle restart requests from the plugin
    /// </summary>
    private void OnRestartRequested(object? sender, RestartComponentEventArgs e)
    {
        if ((e.Flags & Vst3RestartFlags.LatencyChanged) != 0)
        {
            // Latency has changed - may need to notify host
        }

        if ((e.Flags & Vst3RestartFlags.ParamValuesChanged) != 0)
        {
            // Parameters have changed - refresh cache
            CacheParameterInfo();
        }

        if ((e.Flags & Vst3RestartFlags.IoChanged) != 0)
        {
            // I/O configuration has changed
            SetupAudioBuses();
        }
    }

    #endregion

    #region IVstPlugin Implementation - Activation

    /// <summary>
    /// Activate the plugin for processing
    /// </summary>
    public void Activate()
    {
        lock (_lock)
        {
            if (_isActive || _component == null || _audioProcessor == null)
                return;

            // Activate the component
            _component.SetActive(true);

            // Start processing
            _audioProcessor.SetProcessing(true);

            _isActive = true;
        }
    }

    /// <summary>
    /// Deactivate the plugin
    /// </summary>
    public void Deactivate()
    {
        lock (_lock)
        {
            if (!_isActive || _audioProcessor == null || _component == null)
                return;

            // Stop processing
            _audioProcessor.SetProcessing(false);

            // Deactivate the component
            _component.SetActive(false);

            _isActive = false;
        }
    }

    /// <summary>
    /// Set the sample rate for processing
    /// </summary>
    public void SetSampleRate(double sampleRate)
    {
        lock (_lock)
        {
            bool wasActive = _isActive;

            if (wasActive)
                Deactivate();

            _sampleRate = (int)sampleRate;
            _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(_sampleRate, Settings.Channels);

            // Re-setup processing with new sample rate
            SetupProcessing();

            if (wasActive)
                Activate();
        }
    }

    /// <summary>
    /// Set the block size for processing
    /// </summary>
    public void SetBlockSize(int blockSize)
    {
        lock (_lock)
        {
            bool wasActive = _isActive;

            if (wasActive)
                Deactivate();

            _blockSize = blockSize;

            // Free old buffer handles
            if (_inputLeftHandle.IsAllocated) _inputLeftHandle.Free();
            if (_inputRightHandle.IsAllocated) _inputRightHandle.Free();
            if (_outputLeftHandle.IsAllocated) _outputLeftHandle.Free();
            if (_outputRightHandle.IsAllocated) _outputRightHandle.Free();

            // Reallocate buffers
            InitializeAudioBuffers();

            // Re-setup processing with new block size
            SetupProcessing();

            if (wasActive)
                Activate();
        }
    }

    #endregion

    #region IVstPlugin Implementation - Parameters

    /// <summary>
    /// Get the total number of parameters
    /// </summary>
    public int GetParameterCount()
    {
        return _parameterInfoCache.Count;
    }

    /// <summary>
    /// Get parameter name by index
    /// </summary>
    public string GetParameterName(int index)
    {
        if (index < 0 || index >= _parameterInfoCache.Count)
            return $"Param {index}";

        return _parameterInfoCache[index].Title;
    }

    /// <summary>
    /// Get parameter value (0-1 normalized) by index
    /// </summary>
    public float GetParameterValue(int index)
    {
        if (_editController == null || index < 0 || index >= _parameterInfoCache.Count)
            return 0f;

        uint paramId = _parameterInfoCache[index].Id;
        return (float)_editController.GetParamNormalized(paramId);
    }

    /// <summary>
    /// Set parameter value (0-1 normalized) by index
    /// </summary>
    public void SetParameterValue(int index, float value)
    {
        if (_editController == null || index < 0 || index >= _parameterInfoCache.Count)
            return;

        value = Math.Clamp(value, 0f, 1f);
        uint paramId = _parameterInfoCache[index].Id;

        lock (_lock)
        {
            _editController.SetParamNormalized(paramId, value);

            // Queue parameter change for next process call
            _parameterChanges?.AddParameterChange(paramId, 0, value);
        }
    }

    /// <summary>
    /// Get the formatted parameter display string
    /// </summary>
    public string GetParameterDisplay(int index)
    {
        if (_editController == null || index < 0 || index >= _parameterInfoCache.Count)
            return "";

        uint paramId = _parameterInfoCache[index].Id;
        double value = _editController.GetParamNormalized(paramId);

        if (_editController.GetParamStringByValue(paramId, value, out string displayString) == (int)Vst3Result.Ok)
        {
            string units = _parameterInfoCache[index].Units;
            if (!string.IsNullOrEmpty(units))
                return $"{displayString} {units}";
            return displayString;
        }

        return $"{value:F3}";
    }

    /// <summary>
    /// Set a parameter by name
    /// </summary>
    public void SetParameter(string name, float value)
    {
        lock (_lock)
        {
            // Check for special parameters
            switch (name.ToLowerInvariant())
            {
                case "volume":
                case "gain":
                case "level":
                    _masterVolume = Math.Clamp(value, 0f, 2f);
                    return;
            }

            // Find parameter by name
            for (int i = 0; i < _parameterInfoCache.Count; i++)
            {
                if (_parameterInfoCache[i].Title.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                    _parameterInfoCache[i].ShortTitle.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    SetParameterValue(i, value);
                    return;
                }
            }

            // Try to parse as index
            if (int.TryParse(name, out int paramIndex))
            {
                SetParameterValue(paramIndex, value);
            }
        }
    }

    #endregion

    #region IVstPlugin Implementation - MIDI

    /// <summary>
    /// Send a MIDI note on event
    /// </summary>
    public void NoteOn(int note, int velocity)
    {
        lock (_lock)
        {
            _activeNotes.Add((note, velocity));

            // Queue as VST3 event
            _eventList?.AddNoteOn(0, (short)note, velocity / 127f, 0, -1, 0.0f, 0);
        }
    }

    /// <summary>
    /// Send a MIDI note off event
    /// </summary>
    public void NoteOff(int note)
    {
        lock (_lock)
        {
            _activeNotes.RemoveAll(n => n.note == note);

            // Queue as VST3 event
            _eventList?.AddNoteOff(0, (short)note, 0.5f, 0, -1, 0.0f);
        }
    }

    /// <summary>
    /// Stop all playing notes
    /// </summary>
    public void AllNotesOff()
    {
        lock (_lock)
        {
            foreach (var (note, _) in _activeNotes.ToArray())
            {
                _eventList?.AddNoteOff(0, (short)note, 0.5f, 0, -1, 0.0f);
            }
            _activeNotes.Clear();
        }
    }

    /// <summary>
    /// Send a MIDI Control Change message
    /// </summary>
    public void SendControlChange(int channel, int controller, int value)
    {
        _midiEventQueue.Enqueue(new MidiEventData
        {
            DeltaFrames = 0,
            Status = (byte)(0xB0 | (channel & 0x0F)),
            Data1 = (byte)Math.Clamp(controller, 0, 127),
            Data2 = (byte)Math.Clamp(value, 0, 127)
        });
    }

    /// <summary>
    /// Send a MIDI Pitch Bend message
    /// </summary>
    public void SendPitchBend(int channel, int value)
    {
        int clampedValue = Math.Clamp(value, 0, 16383);
        _midiEventQueue.Enqueue(new MidiEventData
        {
            DeltaFrames = 0,
            Status = (byte)(0xE0 | (channel & 0x0F)),
            Data1 = (byte)(clampedValue & 0x7F),
            Data2 = (byte)((clampedValue >> 7) & 0x7F)
        });
    }

    /// <summary>
    /// Send a MIDI Program Change message
    /// </summary>
    public void SendProgramChange(int channel, int program)
    {
        _midiEventQueue.Enqueue(new MidiEventData
        {
            DeltaFrames = 0,
            Status = (byte)(0xC0 | (channel & 0x0F)),
            Data1 = (byte)Math.Clamp(program, 0, 127),
            Data2 = 0
        });
    }

    #endregion

    #region IVstPlugin Implementation - Editor

    /// <summary>
    /// Open the plugin editor GUI
    /// </summary>
    public IntPtr OpenEditor(IntPtr parentWindow)
    {
        lock (_lock)
        {
            if (_editController == null || !_hasEditor)
                return IntPtr.Zero;

            // Create the view
            IntPtr viewPtr = _editController.CreateView("editor");
            if (viewPtr == IntPtr.Zero)
                return IntPtr.Zero;

            _plugView = new PlugViewWrapper(viewPtr);

            // Check platform support
            if (!_plugView.IsPlatformTypeSupported(PlugViewWrapper.kPlatformTypeHWND))
            {
                _plugView.Dispose();
                _plugView = null;
                return IntPtr.Zero;
            }

            // Create plug frame for resize handling
            _plugFrame = new Vst3PlugFrame();
            _plugFrame.ResizeRequested += (view, rect) =>
            {
                // Handle resize request from plugin
            };

            // Set the frame
            _plugView.SetFrame(_plugFrame.NativePtr);

            // Attach to parent window
            int result = _plugView.Attached(parentWindow, PlugViewWrapper.kPlatformTypeHWND);
            if (result != 0)
            {
                _plugView.Dispose();
                _plugView = null;
                _plugFrame.Dispose();
                _plugFrame = null;
                return IntPtr.Zero;
            }

            return _plugView.NativePtr;
        }
    }

    /// <summary>
    /// Close the plugin editor GUI
    /// </summary>
    public void CloseEditor()
    {
        lock (_lock)
        {
            if (_plugView != null)
            {
                _plugView.Removed();
                _plugView.Dispose();
                _plugView = null;
            }

            if (_plugFrame != null)
            {
                _plugFrame.Dispose();
                _plugFrame = null;
            }
        }
    }

    /// <summary>
    /// Get the preferred editor window size
    /// </summary>
    public bool GetEditorSize(out int width, out int height)
    {
        width = 0;
        height = 0;

        lock (_lock)
        {
            if (_plugView == null)
            {
                // Try to create a temporary view to get size
                if (_editController == null || !_hasEditor)
                    return false;

                IntPtr viewPtr = _editController.CreateView("editor");
                if (viewPtr == IntPtr.Zero)
                    return false;

                using var tempView = new PlugViewWrapper(viewPtr);
                if (tempView.GetSize(out ViewRect rect) == 0)
                {
                    width = rect.Width;
                    height = rect.Height;
                    return true;
                }
                return false;
            }

            if (_plugView.GetSize(out ViewRect size) == 0)
            {
                width = size.Width;
                height = size.Height;
                return true;
            }
        }

        return false;
    }

    #endregion

    #region IVstPlugin Implementation - Presets

    /// <summary>
    /// Load a preset file (.vstpreset format)
    /// </summary>
    /// <param name="path">Path to the .vstpreset file</param>
    /// <returns>True if the preset was loaded successfully</returns>
    public bool LoadPreset(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        if (!File.Exists(path))
        {
            Console.WriteLine($"Preset file not found: {path}");
            return false;
        }

        if (_component == null)
        {
            Console.WriteLine("Cannot load preset: plugin component is not initialized");
            return false;
        }

        try
        {
            // Read the preset file
            var presetReader = new VstPresetReader();
            var presetData = presetReader.Read(path);

            // Verify the preset is for this plugin
            if (presetData.ClassId != _classId)
            {
                Console.WriteLine($"Preset class ID mismatch. Expected: {_classId}, Got: {presetData.ClassId}");
                return false;
            }

            // Load component state (processor state)
            if (presetData.ComponentState != null && presetData.ComponentState.Length > 0)
            {
                using var componentStream = new Vst3BStream(presetData.ComponentState);
                int result = _component.SetState(componentStream.NativePtr);
                if (result != (int)Vst3Result.Ok)
                {
                    Console.WriteLine($"Failed to set component state. Result: {result}");
                    return false;
                }

                // Also notify the edit controller about the component state
                if (_editController != null)
                {
                    componentStream.Reset();
                    _editController.SetComponentState(componentStream.NativePtr);
                }
            }

            // Load controller state
            if (_editController != null && presetData.ControllerState != null && presetData.ControllerState.Length > 0)
            {
                using var controllerStream = new Vst3BStream(presetData.ControllerState);
                int result = _editController.SetState(controllerStream.NativePtr);
                if (result != (int)Vst3Result.Ok)
                {
                    Console.WriteLine($"Warning: Failed to set controller state. Result: {result}");
                    // Controller state failure is not critical, continue
                }
            }

            // Update preset name
            _currentPresetName = presetData.Name ?? Path.GetFileNameWithoutExtension(path);

            Console.WriteLine($"Loaded preset: {_currentPresetName}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading preset '{path}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Save current state to a preset file (.vstpreset format)
    /// </summary>
    /// <param name="path">Path for the .vstpreset file</param>
    /// <returns>True if the preset was saved successfully</returns>
    public bool SavePreset(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        if (_component == null)
        {
            Console.WriteLine("Cannot save preset: plugin component is not initialized");
            return false;
        }

        try
        {
            var presetData = new VstPresetData
            {
                ClassId = _classId,
                Name = !string.IsNullOrEmpty(_currentPresetName) ? _currentPresetName : Path.GetFileNameWithoutExtension(path)
            };

            // Get component state (processor state)
            using (var componentStream = new Vst3BStream())
            {
                int result = _component.GetState(componentStream.NativePtr);
                if (result == (int)Vst3Result.Ok)
                {
                    presetData.ComponentState = componentStream.ToArray();
                }
                else
                {
                    Console.WriteLine($"Warning: Failed to get component state. Result: {result}");
                }
            }

            // Get controller state
            if (_editController != null)
            {
                using var controllerStream = new Vst3BStream();
                int result = _editController.GetState(controllerStream.NativePtr);
                if (result == (int)Vst3Result.Ok)
                {
                    presetData.ControllerState = controllerStream.ToArray();
                }
                // Controller state is optional, don't warn if it fails
            }

            // Ensure we have at least some state data to save
            if ((presetData.ComponentState == null || presetData.ComponentState.Length == 0) &&
                (presetData.ControllerState == null || presetData.ControllerState.Length == 0))
            {
                Console.WriteLine("Warning: No state data available to save");
                return false;
            }

            // Write the preset file
            var presetWriter = new VstPresetWriter();
            presetWriter.Write(path, presetData);

            Console.WriteLine($"Saved preset to: {path}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving preset '{path}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Get the list of available preset names
    /// </summary>
    public IReadOnlyList<string> GetPresetNames()
    {
        return _presetNames.AsReadOnly();
    }

    /// <summary>
    /// Set the current preset by index
    /// </summary>
    public void SetPreset(int index)
    {
        if (index < 0 || index >= _presetNames.Count)
            return;

        _currentPresetIndex = index;
        _currentPresetName = _presetNames[index];
    }

    #endregion

    #region IVst3Plugin Implementation

    /// <summary>
    /// Get the list of parameter units/groups
    /// </summary>
    public IReadOnlyList<Vst3UnitInfo> GetUnits()
    {
        return _units.AsReadOnly();
    }

    /// <summary>
    /// Get parameters in a specific unit
    /// </summary>
    public IReadOnlyList<int> GetParametersInUnit(int unitId)
    {
        if (_unitParameters.TryGetValue(unitId, out var parameters))
            return parameters.AsReadOnly();
        return Array.Empty<int>();
    }

    /// <summary>
    /// Send a Note Expression value
    /// </summary>
    public void SendNoteExpression(int noteId, Vst3NoteExpressionType type, double value)
    {
        if (!_supportsNoteExpression || _eventList == null)
            return;

        lock (_lock)
        {
            // Create note expression event
            var evt = new Event
            {
                BusIndex = 0,
                SampleOffset = 0,
                PpqPosition = 0.0,
                Flags = (ushort)Vst3EventFlags.None,
                Type = (ushort)Vst3EventType.NoteExpressionValue
            };

            evt.Data.NoteExpressionValue = new NoteExpressionValueEvent
            {
                TypeId = (uint)type,
                NoteId = noteId,
                Value = Math.Clamp(value, 0.0, 1.0)
            };

            _eventList.AddEvent(evt);
        }
    }

    /// <summary>
    /// Get the number of audio buses
    /// </summary>
    public int GetBusCount(Vst3MediaType mediaType, Vst3BusDirection direction)
    {
        if (_component == null)
            return 0;

        return _component.GetBusCount(mediaType, direction);
    }

    /// <summary>
    /// Get bus info
    /// </summary>
    public Vst3BusInfo GetBusInfo(Vst3MediaType mediaType, Vst3BusDirection direction, int index)
    {
        if (_component == null)
            return new Vst3BusInfo();

        _component.GetBusInfo(mediaType, direction, index, out Vst3BusInfo info);
        return info;
    }

    /// <summary>
    /// Activate or deactivate a bus
    /// </summary>
    public bool SetBusActive(Vst3MediaType mediaType, Vst3BusDirection direction, int index, bool active)
    {
        if (_component == null)
            return false;

        return _component.ActivateBus(mediaType, direction, index, active) == (int)Vst3Result.Ok;
    }

    #endregion

    #region ISampleProvider Implementation (Audio Processing)

    /// <summary>
    /// Read audio samples from the VST3 plugin
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        lock (_lock)
        {
            if (_isDisposed || _audioProcessor == null)
            {
                Array.Clear(buffer, offset, count);
                return count;
            }

            // Auto-activate if not active
            if (!_isActive)
            {
                try
                {
                    Activate();
                }
                catch
                {
                    Array.Clear(buffer, offset, count);
                    return count;
                }
            }

            int samplesPerChannel = count / 2; // Stereo
            int samplesProcessed = 0;

            while (samplesProcessed < samplesPerChannel)
            {
                int samplesToProcess = Math.Min(_blockSize, samplesPerChannel - samplesProcessed);

                // Process one block
                ProcessBlock(samplesToProcess);

                // Interleave output to destination buffer
                int destOffset = offset + (samplesProcessed * 2);
                for (int i = 0; i < samplesToProcess; i++)
                {
                    buffer[destOffset + (i * 2)] = _outputBufferLeft![i] * _masterVolume;
                    buffer[destOffset + (i * 2) + 1] = _outputBufferRight![i] * _masterVolume;
                }

                samplesProcessed += samplesToProcess;
            }

            return count;
        }
    }

    /// <summary>
    /// Process a single block of audio
    /// </summary>
    private void ProcessBlock(int numSamples)
    {
        if (_audioProcessor == null || _outputBufferLeft == null || _outputBufferRight == null)
            return;

        // Clear output buffers
        Array.Clear(_outputBufferLeft, 0, numSamples);
        Array.Clear(_outputBufferRight, 0, numSamples);

        // Read input from input provider if this is an effect
        if (!_isInstrument && _inputProvider != null && _inputReadBuffer != null &&
            _inputBufferLeft != null && _inputBufferRight != null)
        {
            int inputSamples = _inputProvider.Read(_inputReadBuffer, 0, numSamples * 2);

            // Deinterleave input
            int inputFrames = inputSamples / 2;
            for (int i = 0; i < inputFrames; i++)
            {
                _inputBufferLeft[i] = _inputReadBuffer[i * 2];
                _inputBufferRight[i] = _inputReadBuffer[i * 2 + 1];
            }

            // Zero remaining input if not enough samples
            for (int i = inputFrames; i < numSamples; i++)
            {
                _inputBufferLeft[i] = 0f;
                _inputBufferRight[i] = 0f;
            }
        }
        else if (_inputBufferLeft != null && _inputBufferRight != null)
        {
            // Clear input buffers for instruments
            Array.Clear(_inputBufferLeft, 0, numSamples);
            Array.Clear(_inputBufferRight, 0, numSamples);
        }

        // Setup audio bus buffers
        var inputBusBuffers = CreateAudioBusBuffers(_inputPointers!, 2, false);
        var outputBusBuffers = CreateAudioBusBuffers(_outputPointers!, 2, true);

        // Pin bus buffer structures
        GCHandle inputBusHandle = GCHandle.Alloc(inputBusBuffers, GCHandleType.Pinned);
        GCHandle outputBusHandle = GCHandle.Alloc(outputBusBuffers, GCHandleType.Pinned);

        try
        {
            // Create ProcessData structure
            var processData = new ProcessData
            {
                ProcessMode = (int)Vst3ProcessMode.Realtime,
                SymbolicSampleSize = (int)Vst3SymbolicSampleSize.Sample32,
                NumSamples = numSamples,
                NumInputs = _numAudioInputs > 0 ? 1 : 0,
                NumOutputs = _numAudioOutputs > 0 ? 1 : 0,
                Inputs = _numAudioInputs > 0 ? inputBusHandle.AddrOfPinnedObject() : IntPtr.Zero,
                Outputs = _numAudioOutputs > 0 ? outputBusHandle.AddrOfPinnedObject() : IntPtr.Zero,
                InputParameterChanges = _parameterChanges?.NativePtr ?? IntPtr.Zero,
                OutputParameterChanges = IntPtr.Zero,
                InputEvents = _eventList?.NativePtr ?? IntPtr.Zero,
                OutputEvents = IntPtr.Zero,
                ProcessContext = IntPtr.Zero
            };

            // Process the audio
            int result = _audioProcessor.Process(ref processData);

            if (result != (int)Vst3Result.Ok)
            {
                // Processing failed - output silence
                Array.Clear(_outputBufferLeft, 0, numSamples);
                Array.Clear(_outputBufferRight, 0, numSamples);
            }

            // Clear events and parameter changes after processing
            _eventList?.Clear();
            _parameterChanges?.Clear();
        }
        finally
        {
            // Free pinned handles
            if (inputBusHandle.IsAllocated)
                inputBusHandle.Free();
            if (outputBusHandle.IsAllocated)
                outputBusHandle.Free();

            // Free allocated channel buffer pointers
            FreeAudioBusBuffers(ref inputBusBuffers);
            FreeAudioBusBuffers(ref outputBusBuffers);
        }
    }

    /// <summary>
    /// Create an AudioBusBuffers structure for audio processing
    /// </summary>
    private AudioBusBuffers CreateAudioBusBuffers(IntPtr[] channelPointers, int numChannels, bool isOutput)
    {
        // Allocate pointer to array of channel pointers
        IntPtr channelBuffersPtr = Marshal.AllocHGlobal(IntPtr.Size * numChannels);

        for (int i = 0; i < numChannels; i++)
        {
            Marshal.WriteIntPtr(channelBuffersPtr, i * IntPtr.Size, channelPointers[i]);
        }

        return new AudioBusBuffers
        {
            NumChannels = numChannels,
            SilenceFlags = isOutput ? 0UL : 0UL, // No silence flags
            ChannelBuffers32 = channelBuffersPtr,
            ChannelBuffers64 = IntPtr.Zero // We use 32-bit float
        };
    }

    /// <summary>
    /// Free the channel buffer pointer allocated in CreateAudioBusBuffers
    /// </summary>
    private void FreeAudioBusBuffers(ref AudioBusBuffers buffers)
    {
        if (buffers.ChannelBuffers32 != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(buffers.ChannelBuffers32);
            buffers.ChannelBuffers32 = IntPtr.Zero;
        }
    }

    #endregion

    #region IDisposable Implementation

    /// <summary>
    /// Dispose the plugin and release all resources
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        lock (_lock)
        {
            _isDisposed = true;

            // Stop all notes
            AllNotesOff();

            // Cleanup plugin
            CleanupPlugin();

            // Free pinned buffer handles
            if (_inputLeftHandle.IsAllocated) _inputLeftHandle.Free();
            if (_inputRightHandle.IsAllocated) _inputRightHandle.Free();
            if (_outputLeftHandle.IsAllocated) _outputLeftHandle.Free();
            if (_outputRightHandle.IsAllocated) _outputRightHandle.Free();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer
    /// </summary>
    ~Vst3Plugin()
    {
        Dispose();
    }

    #endregion
}
