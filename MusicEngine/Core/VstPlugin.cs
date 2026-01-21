//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: VST Plugin wrapper with full DSP processing, preset management, and parameter automation.


using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using MusicEngine.Core.Automation;
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
    public bool IsInstrument { get; set; } // True for VSTi (instruments), false for effects
    public bool IsLoaded { get; set; }
    public int NumInputs { get; set; }
    public int NumOutputs { get; set; }
    public int NumParameters { get; set; }
    public int NumPrograms { get; set; }
}


/// <summary>
/// VST2 opcodes for plugin communication
/// </summary>
internal enum VstOpcode
{
    Open = 0,
    Close = 1,
    SetProgram = 2,
    GetProgram = 3,
    SetProgramName = 4,
    GetProgramName = 5,
    GetParamLabel = 6,
    GetParamDisplay = 7,
    GetParamName = 8,
    SetSampleRate = 10,
    SetBlockSize = 11,
    MainsChanged = 12,
    EditGetRect = 13,
    EditOpen = 14,
    EditClose = 15,
    GetChunk = 23,
    SetChunk = 24,
    ProcessEvents = 25,
    CanBeAutomated = 26,
    String2Parameter = 27,
    GetProgramNameIndexed = 29,
    GetInputProperties = 33,
    GetOutputProperties = 34,
    GetPlugCategory = 35,
    GetVendorString = 47,
    GetProductString = 48,
    GetVendorVersion = 49,
    CanDo = 51,
    GetTailSize = 52,
    GetParameterProperties = 56,
    GetVstVersion = 58,
    StartProcess = 71,
    StopProcess = 72,
    BeginSetProgram = 67,
    EndSetProgram = 68,
    BeginLoadBank = 69,
    BeginLoadProgram = 70
}


/// <summary>
/// VST plugin flags
/// </summary>
[Flags]
internal enum VstPluginFlags
{
    HasEditor = 1,
    CanReplacing = 1 << 4,
    ProgramChunks = 1 << 5,
    IsSynth = 1 << 8,
    NoSoundInStop = 1 << 9,
    CanDoubleReplacing = 1 << 12
}


/// <summary>
/// VST MIDI event structure
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct VstMidiEventStruct
{
    public int Type;           // kVstMidiType = 1
    public int ByteSize;       // sizeof(VstMidiEvent)
    public int DeltaFrames;    // sample frames related to the current block start sample position
    public int Flags;          // none defined yet
    public int NoteLength;     // (in sample frames) of entire note, if available
    public int NoteOffset;     // offset into note from note start if available
    public byte Midi0;         // MIDI status byte
    public byte Midi1;         // MIDI data byte 1
    public byte Midi2;         // MIDI data byte 2
    public byte Midi3;         // reserved (zero)
    public byte Detune;        // -64 to +63 cents
    public byte NoteOffVelocity;
    public byte Reserved1;
    public byte Reserved2;
}


/// <summary>
/// VST events structure for sending MIDI to plugin
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct VstEvents
{
    public int NumEvents;
    public IntPtr Reserved;
    // Events array follows (variable length)
}


/// <summary>
/// Parameter automation envelope point
/// </summary>
public struct AutomationPoint
{
    public double TimeBeats;
    public float Value;

    public AutomationPoint(double time, float value)
    {
        TimeBeats = time;
        Value = value;
    }
}


/// <summary>
/// Parameter automation data for a single parameter
/// </summary>
public class ParameterAutomation
{
    public int ParameterIndex { get; }
    public List<AutomationPoint> Points { get; } = new();
    public bool IsActive { get; set; } = true;

    public ParameterAutomation(int paramIndex)
    {
        ParameterIndex = paramIndex;
    }

    /// <summary>
    /// Get interpolated value at a specific time in beats
    /// </summary>
    public float GetValueAtTime(double timeBeats)
    {
        if (Points.Count == 0) return 0f;
        if (Points.Count == 1) return Points[0].Value;

        // Find surrounding points
        for (int i = 0; i < Points.Count - 1; i++)
        {
            if (timeBeats >= Points[i].TimeBeats && timeBeats <= Points[i + 1].TimeBeats)
            {
                // Linear interpolation
                double t = (timeBeats - Points[i].TimeBeats) / (Points[i + 1].TimeBeats - Points[i].TimeBeats);
                return Points[i].Value + (float)((Points[i + 1].Value - Points[i].Value) * t);
            }
        }

        // Return last value if past all points
        return Points[^1].Value;
    }

    /// <summary>
    /// Add a linear ramp from start to end value
    /// </summary>
    public void AddRamp(double startTime, float startValue, double endTime, float endValue)
    {
        Points.Add(new AutomationPoint(startTime, startValue));
        Points.Add(new AutomationPoint(endTime, endValue));
        Points.Sort((a, b) => a.TimeBeats.CompareTo(b.TimeBeats));
    }
}


/// <summary>
/// FXP/FXB preset file header
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct FxpHeader
{
    public uint ChunkMagic;    // 'CcnK'
    public uint ByteSize;      // Size of this chunk, excluding magic & byteSize
    public uint FxMagic;       // 'FxCk' (preset) or 'FBCh' (bank)
    public uint Version;       // Format version (1 or 2)
    public uint FxID;          // Plugin unique ID
    public uint FxVersion;     // Plugin version
    public uint NumParams;     // Number of parameters (for FxCk)
    // Program name follows (28 bytes for version 1)
}


/// <summary>
/// VST Plugin wrapper that implements ISynth for seamless integration.
/// Provides full DSP processing, preset management, and parameter automation.
/// </summary>
public class VstPlugin : IVstPlugin
{
    // VST SDK constants
    private const int kVstMidiType = 1;
    private const uint kFxpChunkMagic = 0x4B6E6343; // 'CcnK'
    private const uint kFxpPresetMagic = 0x6B437846; // 'FxCk'
    private const uint kFxpBankMagic = 0x68436246;   // 'FBCh'
    private const uint kFxpOpaquePreset = 0x50437846; // 'FxCP' (opaque preset chunk)
    private const uint kFxpOpaqueBank = 0x42437846;   // 'FxCB' (opaque bank chunk)

    // Plugin state
    private readonly VstPluginInfo _info;
    private readonly WaveFormat _waveFormat;
    private readonly object _lock = new();
    private readonly List<(int note, int velocity)> _activeNotes = new();
    private readonly ConcurrentQueue<VstMidiEventInternal> _midiEventQueue = new();
    private IntPtr _pluginHandle = IntPtr.Zero;
    private IntPtr _moduleHandle = IntPtr.Zero;
    private bool _isDisposed;
    private bool _isProcessing;
    private float _masterVolume = 1.0f;

    // Audio buffers
    private float[] _outputBufferLeft;
    private float[] _outputBufferRight;
    private float[] _inputBufferLeft;
    private float[] _inputBufferRight;
    private float[] _interleavedOutput;
    private IntPtr[] _inputPointers;
    private IntPtr[] _outputPointers;
    private GCHandle _inputLeftHandle;
    private GCHandle _inputRightHandle;
    private GCHandle _outputLeftHandle;
    private GCHandle _outputRightHandle;

    // Parameter storage (for simulated/offline parameters)
    private readonly Dictionary<int, float> _parameterValues = new();
    private readonly Dictionary<int, string> _parameterNames = new();
    private readonly Dictionary<int, string> _parameterLabels = new();

    // Preset management
    private readonly List<string> _presetNames = new();
    private int _currentPreset;
    private string _currentPresetName = "";

    // Parameter automation
    private readonly Dictionary<int, ParameterAutomation> _automations = new();
    private double _currentTimeBeats;

    // Input provider for effect processing
    private ISampleProvider? _inputProvider;
    private float[] _inputReadBuffer;

    // MIDI event structure for internal queuing
    private struct VstMidiEventInternal
    {
        public int DeltaFrames;
        public byte Status;
        public byte Data1;
        public byte Data2;

        public readonly int MidiData => Status | (Data1 << 8) | (Data2 << 16);
    }

    // P/Invoke declarations for loading VST DLLs
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibraryW(string lpFileName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr hModule);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    // VST main entry delegate
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr VstPluginMain(IntPtr audioMasterCallback);

    // Audio master callback delegate
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr AudioMasterCallbackDelegate(IntPtr effect, int opcode, int index, IntPtr value, IntPtr ptr, float opt);

    // VST dispatcher delegate
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr VstDispatcher(IntPtr effect, int opcode, int index, IntPtr value, IntPtr ptr, float opt);

    // VST process replacing delegate (float)
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void VstProcessReplacing(IntPtr effect, IntPtr inputs, IntPtr outputs, int sampleFrames);

    // VST set parameter delegate
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void VstSetParameter(IntPtr effect, int index, float parameter);

    // VST get parameter delegate
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate float VstGetParameter(IntPtr effect, int index);

    // Cached delegates
    private VstDispatcher? _dispatcher;
    private VstProcessReplacing? _processReplacing;
    private VstSetParameter? _setParameter;
    private VstGetParameter? _getParameter;
    private AudioMasterCallbackDelegate? _audioMasterCallback;
    private GCHandle _audioMasterCallbackHandle;

    // Properties
    public VstPluginInfo Info => _info;
    public WaveFormat WaveFormat => _waveFormat;
    public string Name { get => _info.Name; set => _info.Name = value; }
    public bool IsInstrument => _info.IsInstrument;
    public bool IsLoaded => _pluginHandle != IntPtr.Zero;
    public string CurrentPresetName => _currentPresetName;
    public int CurrentPresetIndex => _currentPreset;
    public float MasterVolume { get => _masterVolume; set => _masterVolume = Math.Clamp(value, 0f, 2f); }

    // IVstPlugin interface properties
    public string PluginPath => _info.Path;
    public string Vendor => _info.Vendor;
    public string Version => _info.Version;
    public bool IsVst3 => false; // Always false for VST2
    public int NumAudioInputs => _info.NumInputs;
    public int NumAudioOutputs => _info.NumOutputs;
    public int SampleRate => _waveFormat.SampleRate;
    public int BlockSize => Settings.VstBufferSize;
    public bool HasEditor => _pluginHandle != IntPtr.Zero && (Marshal.ReadInt32(_pluginHandle, IntPtr.Size * 5 + 16) & (int)VstPluginFlags.HasEditor) != 0;
    public bool IsActive => _isProcessing;

    /// <summary>
    /// Gets or sets the input provider for effect processing.
    /// When set, the Read method will process audio from this provider through the VST effect.
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

    public VstPlugin(VstPluginInfo info, int sampleRate = 0)
    {
        _info = info;
        int rate = sampleRate > 0 ? sampleRate : Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);

        int bufferSize = Settings.VstBufferSize;

        // Allocate audio buffers
        _outputBufferLeft = new float[bufferSize];
        _outputBufferRight = new float[bufferSize];
        _inputBufferLeft = new float[bufferSize];
        _inputBufferRight = new float[bufferSize];
        _interleavedOutput = new float[bufferSize * 2];
        _inputReadBuffer = new float[bufferSize * 2];

        // Allocate pointer arrays for VST processing
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

        // Initialize preset names with defaults
        for (int i = 0; i < Math.Max(1, info.NumPrograms); i++)
        {
            _presetNames.Add($"Preset {i + 1}");
        }

        // Try to load the actual VST plugin
        if (!string.IsNullOrEmpty(info.Path) && File.Exists(info.Path))
        {
            try
            {
                LoadVstDll(info.Path);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not load VST plugin '{info.Path}': {ex.Message}");
                // Continue with simulated mode
            }
        }
    }

    /// <summary>
    /// Load the actual VST DLL and initialize
    /// </summary>
    private void LoadVstDll(string path)
    {
        // Load the DLL
        _moduleHandle = LoadLibraryW(path);
        if (_moduleHandle == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error();
            throw new Exception($"Failed to load VST DLL. Error code: {error}");
        }

        // Get the main entry point
        IntPtr mainProc = GetProcAddress(_moduleHandle, "VSTPluginMain");
        if (mainProc == IntPtr.Zero)
        {
            mainProc = GetProcAddress(_moduleHandle, "main");
        }

        if (mainProc == IntPtr.Zero)
        {
            FreeLibrary(_moduleHandle);
            _moduleHandle = IntPtr.Zero;
            throw new Exception("VST plugin has no valid entry point");
        }

        // Create and pin the audio master callback
        _audioMasterCallback = AudioMasterCallback;
        _audioMasterCallbackHandle = GCHandle.Alloc(_audioMasterCallback);

        // Get function pointer for the callback
        IntPtr callbackPtr = Marshal.GetFunctionPointerForDelegate(_audioMasterCallback);

        // Call the main entry point
        var vstMain = Marshal.GetDelegateForFunctionPointer<VstPluginMain>(mainProc);
        _pluginHandle = vstMain(callbackPtr);

        if (_pluginHandle == IntPtr.Zero)
        {
            if (_audioMasterCallbackHandle.IsAllocated)
                _audioMasterCallbackHandle.Free();
            FreeLibrary(_moduleHandle);
            _moduleHandle = IntPtr.Zero;
            throw new Exception("VST plugin initialization failed");
        }

        // Read the AEffect structure to get function pointers
        ReadAEffectStructure();

        // Initialize the plugin
        InitializePlugin();
    }

    /// <summary>
    /// Read the AEffect structure from the plugin
    /// </summary>
    private void ReadAEffectStructure()
    {
        // AEffect structure layout (simplified):
        // int magic (0)
        // IntPtr dispatcher (4/8)
        // IntPtr deprecated_process (8/16)
        // IntPtr setParameter (12/24)
        // IntPtr getParameter (16/32)
        // int numPrograms (20/40)
        // int numParams (24/44)
        // int numInputs (28/48)
        // int numOutputs (32/52)
        // int flags (36/56)
        // ... more fields ...
        // IntPtr processReplacing (offset varies)

        int ptrSize = IntPtr.Size;

        // Read magic number (should be 'VstP')
        int magic = Marshal.ReadInt32(_pluginHandle, 0);
        if (magic != 0x50747356) // 'VstP'
        {
            Console.WriteLine($"Warning: Unexpected VST magic number: 0x{magic:X8}");
        }

        // Read function pointers
        IntPtr dispatcherPtr = Marshal.ReadIntPtr(_pluginHandle, ptrSize);
        IntPtr setParamPtr = Marshal.ReadIntPtr(_pluginHandle, ptrSize * 3);
        IntPtr getParamPtr = Marshal.ReadIntPtr(_pluginHandle, ptrSize * 4);

        // Read counts
        int numPrograms = Marshal.ReadInt32(_pluginHandle, ptrSize * 5);
        int numParams = Marshal.ReadInt32(_pluginHandle, ptrSize * 5 + 4);
        int numInputs = Marshal.ReadInt32(_pluginHandle, ptrSize * 5 + 8);
        int numOutputs = Marshal.ReadInt32(_pluginHandle, ptrSize * 5 + 12);
        int flags = Marshal.ReadInt32(_pluginHandle, ptrSize * 5 + 16);

        // Update plugin info
        _info.NumPrograms = numPrograms;
        _info.NumParameters = numParams;
        _info.NumInputs = numInputs;
        _info.NumOutputs = numOutputs;
        _info.IsInstrument = (flags & (int)VstPluginFlags.IsSynth) != 0;

        // Get processReplacing pointer (offset 160 on 64-bit, 100 on 32-bit approximately)
        // This varies by VST SDK version, so we try common offsets
        IntPtr processReplacingPtr = IntPtr.Zero;

        // Try offset for 64-bit
        if (ptrSize == 8)
        {
            processReplacingPtr = Marshal.ReadIntPtr(_pluginHandle, 160);
            if (processReplacingPtr == IntPtr.Zero)
            {
                processReplacingPtr = Marshal.ReadIntPtr(_pluginHandle, 168);
            }
        }
        else
        {
            processReplacingPtr = Marshal.ReadIntPtr(_pluginHandle, 100);
            if (processReplacingPtr == IntPtr.Zero)
            {
                processReplacingPtr = Marshal.ReadIntPtr(_pluginHandle, 104);
            }
        }

        // Create delegates
        if (dispatcherPtr != IntPtr.Zero)
            _dispatcher = Marshal.GetDelegateForFunctionPointer<VstDispatcher>(dispatcherPtr);
        if (setParamPtr != IntPtr.Zero)
            _setParameter = Marshal.GetDelegateForFunctionPointer<VstSetParameter>(setParamPtr);
        if (getParamPtr != IntPtr.Zero)
            _getParameter = Marshal.GetDelegateForFunctionPointer<VstGetParameter>(getParamPtr);
        if (processReplacingPtr != IntPtr.Zero)
            _processReplacing = Marshal.GetDelegateForFunctionPointer<VstProcessReplacing>(processReplacingPtr);

        // Update preset names
        _presetNames.Clear();
        for (int i = 0; i < numPrograms; i++)
        {
            string name = GetPresetNameByIndex(i);
            _presetNames.Add(string.IsNullOrEmpty(name) ? $"Preset {i + 1}" : name);
        }
    }

    /// <summary>
    /// Audio master callback for VST host communication
    /// </summary>
    private IntPtr AudioMasterCallback(IntPtr effect, int opcode, int index, IntPtr value, IntPtr ptr, float opt)
    {
        // Handle common audio master opcodes
        switch (opcode)
        {
            case 1: // audioMasterAutomate
                // Parameter automation notification
                return IntPtr.Zero;

            case 2: // audioMasterVersion
                return new IntPtr(2400); // VST 2.4

            case 6: // audioMasterWantMidi (deprecated)
                return new IntPtr(1);

            case 7: // audioMasterGetTime
                // Return null for now - could implement VstTimeInfo
                return IntPtr.Zero;

            case 8: // audioMasterProcessEvents
                return IntPtr.Zero;

            case 13: // audioMasterIOChanged
                return IntPtr.Zero;

            case 14: // audioMasterNeedIdle (deprecated)
                return IntPtr.Zero;

            case 15: // audioMasterSizeWindow
                return IntPtr.Zero;

            case 16: // audioMasterGetSampleRate
                return new IntPtr(_waveFormat.SampleRate);

            case 17: // audioMasterGetBlockSize
                return new IntPtr(Settings.VstBufferSize);

            case 23: // audioMasterGetCurrentProcessLevel
                return new IntPtr(_isProcessing ? 2 : 1);

            case 32: // audioMasterGetVendorString
                if (ptr != IntPtr.Zero)
                {
                    byte[] vendor = Encoding.ASCII.GetBytes("MusicEngine\0");
                    Marshal.Copy(vendor, 0, ptr, Math.Min(vendor.Length, 64));
                }
                return new IntPtr(1);

            case 33: // audioMasterGetProductString
                if (ptr != IntPtr.Zero)
                {
                    byte[] product = Encoding.ASCII.GetBytes("MusicEngine VST Host\0");
                    Marshal.Copy(product, 0, ptr, Math.Min(product.Length, 64));
                }
                return new IntPtr(1);

            case 34: // audioMasterGetVendorVersion
                return new IntPtr(1000);

            case 37: // audioMasterCanDo
                if (ptr != IntPtr.Zero)
                {
                    string canDoString = Marshal.PtrToStringAnsi(ptr) ?? "";
                    switch (canDoString)
                    {
                        case "sendVstEvents":
                        case "sendVstMidiEvent":
                        case "receiveVstEvents":
                        case "receiveVstMidiEvent":
                        case "sizeWindow":
                            return new IntPtr(1);
                    }
                }
                return IntPtr.Zero;
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Initialize the VST plugin
    /// </summary>
    private void InitializePlugin()
    {
        if (_dispatcher == null) return;

        // Open the plugin
        _dispatcher(_pluginHandle, (int)VstOpcode.Open, 0, IntPtr.Zero, IntPtr.Zero, 0);

        // Set sample rate
        _dispatcher(_pluginHandle, (int)VstOpcode.SetSampleRate, 0, IntPtr.Zero, IntPtr.Zero, _waveFormat.SampleRate);

        // Set block size
        _dispatcher(_pluginHandle, (int)VstOpcode.SetBlockSize, 0, new IntPtr(Settings.VstBufferSize), IntPtr.Zero, 0);

        // Resume (turn on)
        _dispatcher(_pluginHandle, (int)VstOpcode.MainsChanged, 0, new IntPtr(1), IntPtr.Zero, 0);

        // Start processing
        _dispatcher(_pluginHandle, (int)VstOpcode.StartProcess, 0, IntPtr.Zero, IntPtr.Zero, 0);

        // Get vendor string
        IntPtr vendorPtr = Marshal.AllocHGlobal(256);
        try
        {
            _dispatcher(_pluginHandle, (int)VstOpcode.GetVendorString, 0, IntPtr.Zero, vendorPtr, 0);
            _info.Vendor = Marshal.PtrToStringAnsi(vendorPtr) ?? "Unknown";
        }
        finally
        {
            Marshal.FreeHGlobal(vendorPtr);
        }

        // Get product string for name
        IntPtr productPtr = Marshal.AllocHGlobal(256);
        try
        {
            _dispatcher(_pluginHandle, (int)VstOpcode.GetProductString, 0, IntPtr.Zero, productPtr, 0);
            string productName = Marshal.PtrToStringAnsi(productPtr) ?? "";
            if (!string.IsNullOrEmpty(productName))
            {
                _info.Name = productName;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(productPtr);
        }

        // Cache parameter names
        CacheParameterInfo();

        _info.IsLoaded = true;
    }

    /// <summary>
    /// Cache parameter information for faster access
    /// </summary>
    private void CacheParameterInfo()
    {
        if (_dispatcher == null) return;

        IntPtr namePtr = Marshal.AllocHGlobal(256);
        IntPtr labelPtr = Marshal.AllocHGlobal(256);

        try
        {
            for (int i = 0; i < _info.NumParameters; i++)
            {
                // Clear buffers
                Marshal.WriteByte(namePtr, 0);
                Marshal.WriteByte(labelPtr, 0);

                // Get parameter name
                _dispatcher(_pluginHandle, (int)VstOpcode.GetParamName, i, IntPtr.Zero, namePtr, 0);
                _parameterNames[i] = Marshal.PtrToStringAnsi(namePtr) ?? $"Param {i}";

                // Get parameter label (unit)
                _dispatcher(_pluginHandle, (int)VstOpcode.GetParamLabel, i, IntPtr.Zero, labelPtr, 0);
                _parameterLabels[i] = Marshal.PtrToStringAnsi(labelPtr) ?? "";

                // Get current value
                if (_getParameter != null)
                {
                    _parameterValues[i] = _getParameter(_pluginHandle, i);
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(namePtr);
            Marshal.FreeHGlobal(labelPtr);
        }
    }

    #region MIDI Methods

    /// <summary>
    /// Send a MIDI note on event to the VST plugin
    /// </summary>
    public void NoteOn(int note, int velocity)
    {
        MidiValidation.ValidateNote(note);
        MidiValidation.ValidateVelocity(velocity);

        lock (_lock)
        {
            _activeNotes.Add((note, velocity));
            _midiEventQueue.Enqueue(new VstMidiEventInternal
            {
                DeltaFrames = 0,
                Status = 0x90, // Note On, channel 1
                Data1 = (byte)note,
                Data2 = (byte)velocity
            });
        }
    }

    /// <summary>
    /// Send a MIDI note off event to the VST plugin
    /// </summary>
    public void NoteOff(int note)
    {
        MidiValidation.ValidateNote(note);

        lock (_lock)
        {
            _activeNotes.RemoveAll(n => n.note == note);
            _midiEventQueue.Enqueue(new VstMidiEventInternal
            {
                DeltaFrames = 0,
                Status = 0x80, // Note Off, channel 1
                Data1 = (byte)note,
                Data2 = 0
            });
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
                _midiEventQueue.Enqueue(new VstMidiEventInternal
                {
                    DeltaFrames = 0,
                    Status = 0x80,
                    Data1 = (byte)note,
                    Data2 = 0
                });
            }
            _activeNotes.Clear();

            // Send All Notes Off CC (CC 123)
            _midiEventQueue.Enqueue(new VstMidiEventInternal
            {
                DeltaFrames = 0,
                Status = 0xB0, // Control Change, channel 1
                Data1 = 123,   // All Notes Off
                Data2 = 0
            });
        }
    }

    /// <summary>
    /// Send a Control Change message to the VST plugin
    /// </summary>
    public void SendControlChange(int channel, int controller, int value)
    {
        MidiValidation.ValidateChannel(channel);
        MidiValidation.ValidateController(controller);
        MidiValidation.ValidateVelocity(value); // CC values are 0-127 like velocity

        _midiEventQueue.Enqueue(new VstMidiEventInternal
        {
            DeltaFrames = 0,
            Status = (byte)(0xB0 | channel),
            Data1 = (byte)controller,
            Data2 = (byte)value
        });
    }

    /// <summary>
    /// Send a Pitch Bend message to the VST plugin
    /// </summary>
    public void SendPitchBend(int channel, int value)
    {
        MidiValidation.ValidateChannel(channel);
        MidiValidation.ValidatePitchBend(value);

        _midiEventQueue.Enqueue(new VstMidiEventInternal
        {
            DeltaFrames = 0,
            Status = (byte)(0xE0 | channel),
            Data1 = (byte)(value & 0x7F),
            Data2 = (byte)((value >> 7) & 0x7F)
        });
    }

    /// <summary>
    /// Send a Program Change message to the VST plugin
    /// </summary>
    public void SendProgramChange(int channel, int program)
    {
        MidiValidation.ValidateChannel(channel);
        MidiValidation.ValidateProgram(program);

        _midiEventQueue.Enqueue(new VstMidiEventInternal
        {
            DeltaFrames = 0,
            Status = (byte)(0xC0 | channel),
            Data1 = (byte)program,
            Data2 = 0
        });
    }

    #endregion

    #region Parameter Methods

    /// <summary>
    /// Set a VST parameter by name
    /// </summary>
    public void SetParameter(string name, float value)
    {
        lock (_lock)
        {
            switch (name.ToLowerInvariant())
            {
                case "volume":
                case "gain":
                case "level":
                    _masterVolume = Math.Clamp(value, 0f, 2f);
                    break;
                case "pitchbend":
                    int bendValue = (int)(value * 16383);
                    SendPitchBend(0, bendValue);
                    break;
                default:
                    // Try to find parameter by name
                    foreach (var kvp in _parameterNames)
                    {
                        if (kvp.Value.Equals(name, StringComparison.OrdinalIgnoreCase))
                        {
                            SetParameterValue(kvp.Key, value);
                            return;
                        }
                    }
                    // Try to parse as parameter index
                    if (int.TryParse(name, out int paramIndex) && paramIndex >= 0)
                    {
                        SetParameterValue(paramIndex, value);
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Set a VST parameter by index (internal use)
    /// </summary>
    public void SetParameterByIndex(int index, float value)
    {
        SetParameterValue(index, value);
    }

    /// <summary>
    /// Get the total number of parameters
    /// </summary>
    public int GetParameterCount()
    {
        return _info.NumParameters;
    }

    /// <summary>
    /// Get parameter name by index
    /// </summary>
    public string GetParameterName(int index)
    {
        if (_parameterNames.TryGetValue(index, out string? name))
        {
            return name;
        }

        if (_dispatcher != null && index >= 0 && index < _info.NumParameters)
        {
            IntPtr namePtr = Marshal.AllocHGlobal(256);
            try
            {
                _dispatcher(_pluginHandle, (int)VstOpcode.GetParamName, index, IntPtr.Zero, namePtr, 0);
                string paramName = Marshal.PtrToStringAnsi(namePtr) ?? $"Param {index}";
                _parameterNames[index] = paramName;
                return paramName;
            }
            finally
            {
                Marshal.FreeHGlobal(namePtr);
            }
        }

        return $"Param {index}";
    }

    /// <summary>
    /// Get parameter value (0-1 normalized)
    /// </summary>
    public float GetParameterValue(int index)
    {
        if (_getParameter != null && _pluginHandle != IntPtr.Zero && index >= 0 && index < _info.NumParameters)
        {
            lock (_lock)
            {
                float value = _getParameter(_pluginHandle, index);
                _parameterValues[index] = value;
                return value;
            }
        }

        if (_parameterValues.TryGetValue(index, out float cachedValue))
        {
            return cachedValue;
        }

        return 0f;
    }

    /// <summary>
    /// Set parameter value (0-1 normalized)
    /// </summary>
    public void SetParameterValue(int index, float value)
    {
        value = Math.Clamp(value, 0f, 1f);

        lock (_lock)
        {
            _parameterValues[index] = value;

            if (_setParameter != null && _pluginHandle != IntPtr.Zero && index >= 0 && index < _info.NumParameters)
            {
                _setParameter(_pluginHandle, index, value);
            }
        }
    }

    /// <summary>
    /// Get formatted parameter display string
    /// </summary>
    public string GetParameterDisplay(int index)
    {
        if (_dispatcher != null && _pluginHandle != IntPtr.Zero && index >= 0 && index < _info.NumParameters)
        {
            IntPtr displayPtr = Marshal.AllocHGlobal(256);
            try
            {
                lock (_lock)
                {
                    _dispatcher(_pluginHandle, (int)VstOpcode.GetParamDisplay, index, IntPtr.Zero, displayPtr, 0);
                }
                string display = Marshal.PtrToStringAnsi(displayPtr) ?? "";

                // Append label if available
                if (_parameterLabels.TryGetValue(index, out string? label) && !string.IsNullOrEmpty(label))
                {
                    display = $"{display} {label}";
                }

                return display;
            }
            finally
            {
                Marshal.FreeHGlobal(displayPtr);
            }
        }

        // Fallback to showing normalized value
        float value = GetParameterValue(index);
        return $"{value:F3}";
    }

    /// <summary>
    /// Get parameter label (unit)
    /// </summary>
    public string GetParameterLabel(int index)
    {
        if (_parameterLabels.TryGetValue(index, out string? label))
        {
            return label;
        }
        return "";
    }

    /// <summary>
    /// Get detailed information about a parameter
    /// </summary>
    /// <param name="index">Parameter index</param>
    /// <returns>VstParameterInfo containing parameter details, or null if index is invalid</returns>
    public VstParameterInfo? GetParameterInfo(int index)
    {
        if (index < 0 || index >= _info.NumParameters)
            return null;

        string name = GetParameterName(index);
        string label = GetParameterLabel(index);
        float currentValue = GetParameterValue(index);
        bool canAutomate = CanParameterBeAutomated(index);

        return new VstParameterInfo
        {
            Index = index,
            Name = name,
            ShortName = name.Length > 8 ? name[..8] : name,
            Label = label,
            MinValue = 0f,
            MaxValue = 1f,
            DefaultValue = currentValue, // VST2 doesn't expose default, use current
            StepCount = 0, // VST2 doesn't expose step count
            IsAutomatable = canAutomate,
            IsReadOnly = false,
            ParameterId = (uint)index
        };
    }

    /// <summary>
    /// Get information about all parameters
    /// </summary>
    /// <returns>Read-only list of all parameter info</returns>
    public IReadOnlyList<VstParameterInfo> GetAllParameterInfo()
    {
        var result = new List<VstParameterInfo>();
        for (int i = 0; i < _info.NumParameters; i++)
        {
            var info = GetParameterInfo(i);
            if (info != null)
            {
                result.Add(info);
            }
        }
        return result.AsReadOnly();
    }

    /// <summary>
    /// Check if a parameter can be automated
    /// </summary>
    /// <param name="index">Parameter index</param>
    /// <returns>True if the parameter supports automation</returns>
    public bool CanParameterBeAutomated(int index)
    {
        if (index < 0 || index >= _info.NumParameters)
            return false;

        if (_dispatcher != null && _pluginHandle != IntPtr.Zero)
        {
            lock (_lock)
            {
                IntPtr result = _dispatcher(_pluginHandle, (int)VstOpcode.CanBeAutomated, index, IntPtr.Zero, IntPtr.Zero, 0);
                return result.ToInt32() != 0;
            }
        }

        // Assume automatable if we can't query
        return true;
    }

    #endregion

    #region Automation Methods

    /// <summary>
    /// Create an automation envelope for a parameter
    /// </summary>
    public ParameterAutomation AutomateParameter(int index, float startValue, float endValue, double durationBeats)
    {
        var automation = new ParameterAutomation(index);
        automation.AddRamp(0, startValue, durationBeats, endValue);

        lock (_lock)
        {
            _automations[index] = automation;
        }

        return automation;
    }

    /// <summary>
    /// Get automation for a parameter
    /// </summary>
    public ParameterAutomation? GetAutomation(int index)
    {
        lock (_lock)
        {
            return _automations.TryGetValue(index, out var automation) ? automation : null;
        }
    }

    /// <summary>
    /// Clear all automation for a parameter
    /// </summary>
    public void ClearAutomation(int index)
    {
        lock (_lock)
        {
            _automations.Remove(index);
        }
    }

    /// <summary>
    /// Clear all automation
    /// </summary>
    public void ClearAllAutomation()
    {
        lock (_lock)
        {
            _automations.Clear();
        }
    }

    /// <summary>
    /// Update current playback time for automation
    /// </summary>
    public void SetCurrentTimeBeats(double timeBeats)
    {
        _currentTimeBeats = timeBeats;
    }

    /// <summary>
    /// Process automation updates
    /// </summary>
    private void ProcessAutomation()
    {
        foreach (var kvp in _automations)
        {
            if (kvp.Value.IsActive)
            {
                float value = kvp.Value.GetValueAtTime(_currentTimeBeats);
                SetParameterValue(kvp.Key, value);
            }
        }
    }

    #endregion

    #region Preset Methods

    /// <summary>
    /// Get list of available preset names
    /// </summary>
    public IReadOnlyList<string> GetPresetNames()
    {
        return _presetNames.AsReadOnly();
    }

    /// <summary>
    /// Get preset name by index
    /// </summary>
    private string GetPresetNameByIndex(int index)
    {
        if (_dispatcher == null || _pluginHandle == IntPtr.Zero) return "";

        IntPtr namePtr = Marshal.AllocHGlobal(256);
        try
        {
            lock (_lock)
            {
                _dispatcher(_pluginHandle, (int)VstOpcode.GetProgramNameIndexed, index, IntPtr.Zero, namePtr, 0);
            }
            return Marshal.PtrToStringAnsi(namePtr) ?? "";
        }
        finally
        {
            Marshal.FreeHGlobal(namePtr);
        }
    }

    /// <summary>
    /// Set the current preset by index
    /// </summary>
    public void SetPreset(int index)
    {
        if (index < 0 || index >= _presetNames.Count)
        {
            Console.WriteLine($"Invalid preset index: {index}");
            return;
        }

        lock (_lock)
        {
            if (_dispatcher != null && _pluginHandle != IntPtr.Zero)
            {
                _dispatcher(_pluginHandle, (int)VstOpcode.BeginSetProgram, 0, IntPtr.Zero, IntPtr.Zero, 0);
                _dispatcher(_pluginHandle, (int)VstOpcode.SetProgram, 0, new IntPtr(index), IntPtr.Zero, 0);
                _dispatcher(_pluginHandle, (int)VstOpcode.EndSetProgram, 0, IntPtr.Zero, IntPtr.Zero, 0);
            }

            _currentPreset = index;
            _currentPresetName = _presetNames[index];

            // Refresh parameter values
            CacheParameterInfo();
        }

        Console.WriteLine($"Preset changed to: {_currentPresetName}");
    }

    /// <summary>
    /// Load a preset from an .fxp file
    /// </summary>
    public bool LoadPreset(string path)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"Preset file not found: {path}");
            return false;
        }

        try
        {
            byte[] data = File.ReadAllBytes(path);

            if (data.Length < 60)
            {
                Console.WriteLine("Invalid preset file: too small");
                return false;
            }

            // Read header
            uint chunkMagic = BitConverter.ToUInt32(data, 0);
            if (chunkMagic != kFxpChunkMagic)
            {
                // Try byte-swapped (big endian)
                chunkMagic = SwapEndian(chunkMagic);
                if (chunkMagic != kFxpChunkMagic)
                {
                    Console.WriteLine("Invalid preset file: bad magic number");
                    return false;
                }
            }

            // Check fxMagic to determine format
            uint fxMagic = SwapEndian(BitConverter.ToUInt32(data, 8));
            bool isChunk = (fxMagic == kFxpOpaquePreset || fxMagic == kFxpOpaqueBank);
            bool isPreset = (fxMagic == kFxpPresetMagic || fxMagic == kFxpOpaquePreset);

            // Verify plugin ID matches
            uint fxId = SwapEndian(BitConverter.ToUInt32(data, 16));
            if (fxId != (uint)_info.UniqueId && _info.UniqueId != 0)
            {
                Console.WriteLine($"Warning: Preset may be for different plugin (ID: {fxId} vs {_info.UniqueId})");
            }

            lock (_lock)
            {
                if (isChunk && _dispatcher != null && _pluginHandle != IntPtr.Zero)
                {
                    // Opaque chunk format - send to plugin
                    int dataOffset = 60; // Header size
                    int chunkSize = data.Length - dataOffset;

                    if (chunkSize > 0)
                    {
                        IntPtr chunkPtr = Marshal.AllocHGlobal(chunkSize);
                        try
                        {
                            Marshal.Copy(data, dataOffset, chunkPtr, chunkSize);
                            _dispatcher(_pluginHandle, (int)VstOpcode.BeginSetProgram, 0, IntPtr.Zero, IntPtr.Zero, 0);
                            _dispatcher(_pluginHandle, (int)VstOpcode.SetChunk, isPreset ? 1 : 0, new IntPtr(chunkSize), chunkPtr, 0);
                            _dispatcher(_pluginHandle, (int)VstOpcode.EndSetProgram, 0, IntPtr.Zero, IntPtr.Zero, 0);
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(chunkPtr);
                        }
                    }
                }
                else if (!isChunk)
                {
                    // Regular format - read parameter values
                    uint numParams = SwapEndian(BitConverter.ToUInt32(data, 24));
                    int paramOffset = 56; // After header and program name

                    for (int i = 0; i < numParams && paramOffset + 4 <= data.Length; i++)
                    {
                        // Parameters are stored as big-endian floats
                        uint rawValue = SwapEndian(BitConverter.ToUInt32(data, paramOffset));
                        float value = BitConverter.ToSingle(BitConverter.GetBytes(rawValue), 0);
                        SetParameterValue(i, value);
                        paramOffset += 4;
                    }
                }

                // Extract preset name from header (bytes 28-55)
                int nameStart = 28;
                int nameEnd = Array.IndexOf(data, (byte)0, nameStart);
                if (nameEnd < 0 || nameEnd > 55) nameEnd = 55;
                _currentPresetName = Encoding.ASCII.GetString(data, nameStart, nameEnd - nameStart).TrimEnd('\0');
            }

            Console.WriteLine($"Loaded preset: {_currentPresetName} from {Path.GetFileName(path)}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading preset: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Save current state to an .fxp preset file
    /// </summary>
    public bool SavePreset(string path)
    {
        try
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            // Write header
            writer.Write(SwapEndian(kFxpChunkMagic)); // CcnK

            bool useChunk = false;
            byte[]? chunkData = null;

            // Try to get chunk from plugin
            if (_dispatcher != null && _pluginHandle != IntPtr.Zero)
            {
                lock (_lock)
                {
                    IntPtr chunkPtr = Marshal.AllocHGlobal(IntPtr.Size);
                    try
                    {
                        IntPtr result = _dispatcher(_pluginHandle, (int)VstOpcode.GetChunk, 1, IntPtr.Zero, chunkPtr, 0);
                        int chunkSize = result.ToInt32();

                        if (chunkSize > 0)
                        {
                            IntPtr dataPtr = Marshal.ReadIntPtr(chunkPtr);
                            if (dataPtr != IntPtr.Zero)
                            {
                                chunkData = new byte[chunkSize];
                                Marshal.Copy(dataPtr, chunkData, 0, chunkSize);
                                useChunk = true;
                            }
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(chunkPtr);
                    }
                }
            }

            if (useChunk && chunkData != null)
            {
                // Write opaque chunk format
                int totalSize = 52 + chunkData.Length;
                writer.Write(SwapEndian((uint)totalSize));
                writer.Write(SwapEndian(kFxpOpaquePreset)); // FxCP
                writer.Write(SwapEndian(1u)); // version
                writer.Write(SwapEndian((uint)_info.UniqueId));
                writer.Write(SwapEndian(1u)); // plugin version
                writer.Write(SwapEndian((uint)_info.NumParameters));

                // Write program name (28 bytes)
                byte[] nameBytes = new byte[28];
                byte[] nameData = Encoding.ASCII.GetBytes(_currentPresetName);
                Array.Copy(nameData, nameBytes, Math.Min(nameData.Length, 27));
                writer.Write(nameBytes);

                // Write chunk size and data
                writer.Write(SwapEndian((uint)chunkData.Length));
                writer.Write(chunkData);
            }
            else
            {
                // Write regular format
                int numParams = _info.NumParameters;
                int totalSize = 48 + numParams * 4;
                writer.Write(SwapEndian((uint)totalSize));
                writer.Write(SwapEndian(kFxpPresetMagic)); // FxCk
                writer.Write(SwapEndian(1u)); // version
                writer.Write(SwapEndian((uint)_info.UniqueId));
                writer.Write(SwapEndian(1u)); // plugin version
                writer.Write(SwapEndian((uint)numParams));

                // Write program name (28 bytes)
                byte[] nameBytes = new byte[28];
                byte[] nameData = Encoding.ASCII.GetBytes(_currentPresetName);
                Array.Copy(nameData, nameBytes, Math.Min(nameData.Length, 27));
                writer.Write(nameBytes);

                // Write parameters as big-endian floats
                for (int i = 0; i < numParams; i++)
                {
                    float value = GetParameterValue(i);
                    uint rawValue = BitConverter.ToUInt32(BitConverter.GetBytes(value), 0);
                    writer.Write(SwapEndian(rawValue));
                }
            }

            File.WriteAllBytes(path, stream.ToArray());
            Console.WriteLine($"Saved preset to: {path}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving preset: {ex.Message}");
            return false;
        }
    }

    private static uint SwapEndian(uint value)
    {
        return ((value & 0xFF) << 24) |
               ((value & 0xFF00) << 8) |
               ((value & 0xFF0000) >> 8) |
               ((value & 0xFF000000) >> 24);
    }

    #endregion

    #region Audio Processing

    /// <summary>
    /// Read audio samples from the VST plugin (ISampleProvider implementation)
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        lock (_lock)
        {
            if (_isDisposed) return 0;

            _isProcessing = true;

            try
            {
                int samplesPerChannel = count / _waveFormat.Channels;
                int totalSamplesProcessed = 0;

                while (totalSamplesProcessed < count)
                {
                    int samplesToProcess = Math.Min(samplesPerChannel, Settings.VstBufferSize);
                    int interleavedSamples = samplesToProcess * _waveFormat.Channels;

                    // Clear output buffers
                    Array.Clear(_outputBufferLeft, 0, samplesToProcess);
                    Array.Clear(_outputBufferRight, 0, samplesToProcess);

                    // Handle input (for effects)
                    if (_inputProvider != null && !IsInstrument)
                    {
                        // Read from input provider
                        int inputRead = _inputProvider.Read(_inputReadBuffer, 0, interleavedSamples);

                        // Deinterleave to separate channels
                        DeinterleaveAudio(_inputReadBuffer, _inputBufferLeft, _inputBufferRight, samplesToProcess);
                    }
                    else
                    {
                        // Clear input for instruments or when no input
                        Array.Clear(_inputBufferLeft, 0, samplesToProcess);
                        Array.Clear(_inputBufferRight, 0, samplesToProcess);
                    }

                    // Process MIDI events
                    ProcessMidiEvents();

                    // Process automation
                    ProcessAutomation();

                    // Process through VST plugin
                    if (_processReplacing != null && _pluginHandle != IntPtr.Zero)
                    {
                        // Pin pointer arrays
                        GCHandle inputPtrsHandle = GCHandle.Alloc(_inputPointers, GCHandleType.Pinned);
                        GCHandle outputPtrsHandle = GCHandle.Alloc(_outputPointers, GCHandleType.Pinned);

                        try
                        {
                            _processReplacing(
                                _pluginHandle,
                                inputPtrsHandle.AddrOfPinnedObject(),
                                outputPtrsHandle.AddrOfPinnedObject(),
                                samplesToProcess
                            );
                        }
                        finally
                        {
                            inputPtrsHandle.Free();
                            outputPtrsHandle.Free();
                        }
                    }

                    // Interleave output and apply master volume
                    InterleaveAudio(_outputBufferLeft, _outputBufferRight, buffer,
                        offset + totalSamplesProcessed, samplesToProcess, _masterVolume);

                    totalSamplesProcessed += interleavedSamples;
                }

                return count;
            }
            finally
            {
                _isProcessing = false;
            }
        }
    }

    /// <summary>
    /// Process audio input through the VST effect (for external callers)
    /// </summary>
    public void ProcessInput(float[] input, float[] output, int sampleCount)
    {
        lock (_lock)
        {
            if (_isDisposed) return;

            _isProcessing = true;

            try
            {
                int channels = _waveFormat.Channels;
                int samplesPerChannel = sampleCount / channels;

                // Deinterleave input
                DeinterleaveAudio(input, _inputBufferLeft, _inputBufferRight, samplesPerChannel);

                // Clear output buffers
                Array.Clear(_outputBufferLeft, 0, samplesPerChannel);
                Array.Clear(_outputBufferRight, 0, samplesPerChannel);

                // Process MIDI events
                ProcessMidiEvents();

                // Process automation
                ProcessAutomation();

                // Process through VST
                if (_processReplacing != null && _pluginHandle != IntPtr.Zero)
                {
                    GCHandle inputPtrsHandle = GCHandle.Alloc(_inputPointers, GCHandleType.Pinned);
                    GCHandle outputPtrsHandle = GCHandle.Alloc(_outputPointers, GCHandleType.Pinned);

                    try
                    {
                        _processReplacing(
                            _pluginHandle,
                            inputPtrsHandle.AddrOfPinnedObject(),
                            outputPtrsHandle.AddrOfPinnedObject(),
                            samplesPerChannel
                        );
                    }
                    finally
                    {
                        inputPtrsHandle.Free();
                        outputPtrsHandle.Free();
                    }
                }
                else
                {
                    // No plugin loaded - for instruments, output silence
                    // For effects, pass through input
                    if (!IsInstrument)
                    {
                        Array.Copy(_inputBufferLeft, _outputBufferLeft, samplesPerChannel);
                        Array.Copy(_inputBufferRight, _outputBufferRight, samplesPerChannel);
                    }
                }

                // Interleave output with master volume
                InterleaveAudio(_outputBufferLeft, _outputBufferRight, output, 0, samplesPerChannel, _masterVolume);
            }
            finally
            {
                _isProcessing = false;
            }
        }
    }

    /// <summary>
    /// Process pending MIDI events and send to plugin
    /// </summary>
    private void ProcessMidiEvents()
    {
        if (_midiEventQueue.IsEmpty) return;

        if (_dispatcher != null && _pluginHandle != IntPtr.Zero)
        {
            // Allocate events structure
            int eventCount = _midiEventQueue.Count;
            int eventsSize = 8 + IntPtr.Size + eventCount * IntPtr.Size;
            int midiEventSize = Marshal.SizeOf<VstMidiEventStruct>();

            IntPtr eventsPtr = Marshal.AllocHGlobal(eventsSize + eventCount * midiEventSize);

            try
            {
                // Write VstEvents header
                Marshal.WriteInt32(eventsPtr, eventCount);
                Marshal.WriteIntPtr(eventsPtr, 4, IntPtr.Zero);

                int eventOffset = 8 + IntPtr.Size;
                int dataOffset = eventsSize;

                while (_midiEventQueue.TryDequeue(out var evt))
                {
                    // Create MIDI event struct
                    var midiEvent = new VstMidiEventStruct
                    {
                        Type = kVstMidiType,
                        ByteSize = midiEventSize,
                        DeltaFrames = evt.DeltaFrames,
                        Flags = 0,
                        NoteLength = 0,
                        NoteOffset = 0,
                        Midi0 = evt.Status,
                        Midi1 = evt.Data1,
                        Midi2 = evt.Data2,
                        Midi3 = 0,
                        Detune = 0,
                        NoteOffVelocity = 0,
                        Reserved1 = 0,
                        Reserved2 = 0
                    };

                    // Write event data
                    IntPtr eventDataPtr = eventsPtr + dataOffset;
                    Marshal.StructureToPtr(midiEvent, eventDataPtr, false);

                    // Write pointer to event
                    Marshal.WriteIntPtr(eventsPtr, eventOffset, eventDataPtr);

                    eventOffset += IntPtr.Size;
                    dataOffset += midiEventSize;
                }

                // Send events to plugin
                _dispatcher(_pluginHandle, (int)VstOpcode.ProcessEvents, 0, IntPtr.Zero, eventsPtr, 0);
            }
            finally
            {
                Marshal.FreeHGlobal(eventsPtr);
            }
        }
        else
        {
            // No plugin - just clear the queue
            _midiEventQueue.Clear();
        }
    }

    /// <summary>
    /// Deinterleave stereo audio to separate left/right buffers
    /// </summary>
    private void DeinterleaveAudio(float[] interleaved, float[] left, float[] right, int samplesPerChannel)
    {
        for (int i = 0; i < samplesPerChannel; i++)
        {
            int srcIndex = i * 2;
            if (srcIndex < interleaved.Length)
            {
                left[i] = interleaved[srcIndex];
                right[i] = srcIndex + 1 < interleaved.Length ? interleaved[srcIndex + 1] : interleaved[srcIndex];
            }
            else
            {
                left[i] = 0;
                right[i] = 0;
            }
        }
    }

    /// <summary>
    /// Interleave separate left/right buffers to stereo output
    /// </summary>
    private void InterleaveAudio(float[] left, float[] right, float[] output, int offset, int samplesPerChannel, float gain)
    {
        for (int i = 0; i < samplesPerChannel; i++)
        {
            int dstIndex = offset + i * 2;
            if (dstIndex < output.Length)
            {
                output[dstIndex] = left[i] * gain;
                if (dstIndex + 1 < output.Length)
                {
                    output[dstIndex + 1] = right[i] * gain;
                }
            }
        }
    }

    #endregion

    #region IVstPlugin Interface Methods

    /// <summary>
    /// Set the sample rate for processing
    /// </summary>
    public void SetSampleRate(double sampleRate)
    {
        lock (_lock)
        {
            if (_dispatcher != null && _pluginHandle != IntPtr.Zero)
            {
                _dispatcher(_pluginHandle, (int)VstOpcode.SetSampleRate, 0, IntPtr.Zero, IntPtr.Zero, (float)sampleRate);
            }
        }
    }

    /// <summary>
    /// Set the block size for processing
    /// </summary>
    public void SetBlockSize(int blockSize)
    {
        lock (_lock)
        {
            if (_dispatcher != null && _pluginHandle != IntPtr.Zero)
            {
                _dispatcher(_pluginHandle, (int)VstOpcode.SetBlockSize, 0, new IntPtr(blockSize), IntPtr.Zero, 0);
            }
        }
    }

    /// <summary>
    /// Open the plugin editor GUI
    /// </summary>
    /// <param name="parentWindow">Handle to the parent window</param>
    /// <returns>Handle to the editor window, or IntPtr.Zero if failed</returns>
    public IntPtr OpenEditor(IntPtr parentWindow)
    {
        lock (_lock)
        {
            if (_dispatcher != null && _pluginHandle != IntPtr.Zero && HasEditor)
            {
                return _dispatcher(_pluginHandle, (int)VstOpcode.EditOpen, 0, IntPtr.Zero, parentWindow, 0);
            }
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// Close the plugin editor GUI
    /// </summary>
    public void CloseEditor()
    {
        lock (_lock)
        {
            if (_dispatcher != null && _pluginHandle != IntPtr.Zero)
            {
                _dispatcher(_pluginHandle, (int)VstOpcode.EditClose, 0, IntPtr.Zero, IntPtr.Zero, 0);
            }
        }
    }

    /// <summary>
    /// Get the preferred editor window size
    /// </summary>
    /// <param name="width">Output width</param>
    /// <param name="height">Output height</param>
    /// <returns>True if size was retrieved</returns>
    public bool GetEditorSize(out int width, out int height)
    {
        width = 0;
        height = 0;

        lock (_lock)
        {
            if (_dispatcher != null && _pluginHandle != IntPtr.Zero && HasEditor)
            {
                // Allocate memory for ERect pointer
                IntPtr rectPtrPtr = Marshal.AllocHGlobal(IntPtr.Size);
                try
                {
                    Marshal.WriteIntPtr(rectPtrPtr, IntPtr.Zero);
                    _dispatcher(_pluginHandle, (int)VstOpcode.EditGetRect, 0, IntPtr.Zero, rectPtrPtr, 0);

                    IntPtr rectPtr = Marshal.ReadIntPtr(rectPtrPtr);
                    if (rectPtr != IntPtr.Zero)
                    {
                        // ERect structure: short top, short left, short bottom, short right
                        short top = Marshal.ReadInt16(rectPtr, 0);
                        short left = Marshal.ReadInt16(rectPtr, 2);
                        short bottom = Marshal.ReadInt16(rectPtr, 4);
                        short right = Marshal.ReadInt16(rectPtr, 6);

                        width = right - left;
                        height = bottom - top;
                        return true;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(rectPtrPtr);
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Activate the plugin for processing
    /// </summary>
    public void Activate()
    {
        lock (_lock)
        {
            if (_dispatcher != null && _pluginHandle != IntPtr.Zero)
            {
                _dispatcher(_pluginHandle, (int)VstOpcode.MainsChanged, 0, new IntPtr(1), IntPtr.Zero, 0);
            }
        }
    }

    /// <summary>
    /// Deactivate the plugin
    /// </summary>
    public void Deactivate()
    {
        lock (_lock)
        {
            if (_dispatcher != null && _pluginHandle != IntPtr.Zero)
            {
                _dispatcher(_pluginHandle, (int)VstOpcode.MainsChanged, 0, IntPtr.Zero, IntPtr.Zero, 0);
            }
        }
    }

    #endregion

    #region Disposal

    public void Dispose()
    {
        if (_isDisposed) return;

        lock (_lock)
        {
            _isDisposed = true;
            AllNotesOff();

            // Stop and close the plugin
            if (_dispatcher != null && _pluginHandle != IntPtr.Zero)
            {
                try
                {
                    _dispatcher(_pluginHandle, (int)VstOpcode.StopProcess, 0, IntPtr.Zero, IntPtr.Zero, 0);
                    _dispatcher(_pluginHandle, (int)VstOpcode.MainsChanged, 0, IntPtr.Zero, IntPtr.Zero, 0);
                    _dispatcher(_pluginHandle, (int)VstOpcode.Close, 0, IntPtr.Zero, IntPtr.Zero, 0);
                }
                catch
                {
                    // Ignore errors during shutdown
                }
            }

            _pluginHandle = IntPtr.Zero;
            _dispatcher = null;
            _processReplacing = null;
            _setParameter = null;
            _getParameter = null;

            // Free audio master callback handle
            if (_audioMasterCallbackHandle.IsAllocated)
            {
                _audioMasterCallbackHandle.Free();
            }

            // Unload the DLL
            if (_moduleHandle != IntPtr.Zero)
            {
                FreeLibrary(_moduleHandle);
                _moduleHandle = IntPtr.Zero;
            }

            // Free pinned buffer handles
            if (_inputLeftHandle.IsAllocated) _inputLeftHandle.Free();
            if (_inputRightHandle.IsAllocated) _inputRightHandle.Free();
            if (_outputLeftHandle.IsAllocated) _outputLeftHandle.Free();
            if (_outputRightHandle.IsAllocated) _outputRightHandle.Free();
        }

        GC.SuppressFinalize(this);
    }

    ~VstPlugin()
    {
        Dispose();
    }

    #endregion
}
