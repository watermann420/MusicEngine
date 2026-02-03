using System.Runtime.InteropServices;
using System.Text;

namespace MusicEngine.VstBridge;

/// <summary>
/// Wrapper for a native VST plugin instance.
/// </summary>
public sealed class NativeVstPlugin : INativeVstPlugin
{
    private nint _handle;
    private readonly NativeVstHost _host;
    private bool _disposed;

    // Cached plugin info
    private readonly string _name;
    private readonly string _vendor;
    private readonly string _product;
    private readonly int _version;
    private readonly VstPluginType _pluginType;
    private readonly uint _uniqueId;
    private readonly int _numInputs;
    private readonly int _numOutputs;
    private readonly bool _isSynth;
    private readonly bool _hasEditor;

    // Buffer management for audio processing
    private GCHandle[]? _inputHandles;
    private GCHandle[]? _outputHandles;
    private nint[]? _inputPtrs;
    private nint[]? _outputPtrs;

    internal NativeVstPlugin(nint handle, NativeVstHost host)
    {
        _handle = handle;
        _host = host;

        // Cache plugin info
        _name = GetPluginNameInternal();
        _vendor = GetVendorNameInternal();
        _product = GetProductNameInternal();

        _version = VstBridgeNative.GetPluginVersion(_handle);
        _pluginType = (VstPluginType)VstBridgeNative.GetPluginType(_handle);
        _uniqueId = VstBridgeNative.GetUniqueId(_handle);
        _numInputs = VstBridgeNative.GetNumInputs(_handle);
        _numOutputs = VstBridgeNative.GetNumOutputs(_handle);
        _isSynth = VstBridgeNative.IsSynth(_handle) != 0;
        _hasEditor = VstBridgeNative.HasEditor(_handle) != 0;
    }

    internal nint Handle => _handle;

    #region Plugin Info

    public string Name => _name;
    public string Vendor => _vendor;
    public string Product => _product;
    public int Version => _version;
    public VstPluginType PluginType => _pluginType;
    public uint UniqueId => _uniqueId;
    public int NumInputs => _numInputs;
    public int NumOutputs => _numOutputs;
    public bool IsSynth => _isSynth;
    public int Latency => VstBridgeNative.GetLatency(_handle);
    public int TailSize => VstBridgeNative.GetTailSize(_handle);
    public bool HasEditor => _hasEditor;
    public bool IsEditorOpen => VstBridgeNative.IsEditorOpen(_handle) != 0;
    public bool IsValid => _handle != nint.Zero && VstBridgeNative.IsPluginValid(_handle) != 0;

    private unsafe string GetPluginNameInternal()
    {
        const int maxLen = 256;
        byte* buffer = stackalloc byte[maxLen];
        VstBridgeNative.GetPluginName(_handle, buffer, maxLen);
        return GetStringFromBuffer(buffer, maxLen);
    }

    private unsafe string GetVendorNameInternal()
    {
        const int maxLen = 256;
        byte* buffer = stackalloc byte[maxLen];
        VstBridgeNative.GetVendorName(_handle, buffer, maxLen);
        return GetStringFromBuffer(buffer, maxLen);
    }

    private unsafe string GetProductNameInternal()
    {
        const int maxLen = 256;
        byte* buffer = stackalloc byte[maxLen];
        VstBridgeNative.GetProductName(_handle, buffer, maxLen);
        return GetStringFromBuffer(buffer, maxLen);
    }

    private static unsafe string GetStringFromBuffer(byte* buffer, int maxLen)
    {
        int len = 0;
        while (len < maxLen && buffer[len] != 0) len++;
        return Encoding.UTF8.GetString(buffer, len);
    }

    #endregion

    #region Parameters

    public int ParameterCount => VstBridgeNative.GetParamCount(_handle);

    public float this[int parameterIndex]
    {
        get
        {
            ThrowIfDisposed();
            if (parameterIndex < 0 || parameterIndex >= ParameterCount)
                throw new ArgumentOutOfRangeException(nameof(parameterIndex));
            return VstBridgeNative.GetParam(_handle, parameterIndex);
        }
        set
        {
            ThrowIfDisposed();
            if (parameterIndex < 0 || parameterIndex >= ParameterCount)
                throw new ArgumentOutOfRangeException(nameof(parameterIndex));
            VstBridgeNative.SetParam(_handle, parameterIndex, Math.Clamp(value, 0f, 1f));
        }
    }

    public unsafe VstParameter GetParameter(int index)
    {
        ThrowIfDisposed();
        if (index < 0 || index >= ParameterCount)
            throw new ArgumentOutOfRangeException(nameof(index));

        const int maxLen = 256;
        byte* buffer = stackalloc byte[maxLen];

        VstBridgeNative.GetParamName(_handle, index, buffer, maxLen);
        var name = GetStringFromBuffer(buffer, maxLen);

        VstBridgeNative.GetParamDisplay(_handle, index, buffer, maxLen);
        var display = GetStringFromBuffer(buffer, maxLen);

        VstBridgeNative.GetParamLabel(_handle, index, buffer, maxLen);
        var label = GetStringFromBuffer(buffer, maxLen);

        var value = VstBridgeNative.GetParam(_handle, index);

        return new VstParameter(index, name, display, label, value);
    }

    #endregion

    #region Audio Processing

    public unsafe void Process(float[][] inputs, float[][] outputs, int numSamples)
    {
        ThrowIfDisposed();

        int numInputChannels = Math.Min(inputs.Length, _numInputs);
        int numOutputChannels = Math.Min(outputs.Length, _numOutputs);

        // Ensure buffer arrays are allocated
        EnsureBufferArrays(numInputChannels, numOutputChannels);

        try
        {
            // Pin input buffers
            for (int i = 0; i < numInputChannels; i++)
            {
                _inputHandles![i] = GCHandle.Alloc(inputs[i], GCHandleType.Pinned);
                _inputPtrs![i] = _inputHandles[i].AddrOfPinnedObject();
            }

            // Pin output buffers
            for (int i = 0; i < numOutputChannels; i++)
            {
                _outputHandles![i] = GCHandle.Alloc(outputs[i], GCHandleType.Pinned);
                _outputPtrs![i] = _outputHandles[i].AddrOfPinnedObject();
            }

            // Process audio
            fixed (nint* inputsPtr = _inputPtrs)
            fixed (nint* outputsPtr = _outputPtrs)
            {
                VstBridgeNative.ProcessReplacing(_handle, (float**)inputsPtr, (float**)outputsPtr, numSamples);
            }
        }
        finally
        {
            // Unpin all buffers
            FreeBufferHandles(numInputChannels, numOutputChannels);
        }
    }

    private void EnsureBufferArrays(int numInputs, int numOutputs)
    {
        if (_inputHandles == null || _inputHandles.Length < numInputs)
        {
            _inputHandles = new GCHandle[numInputs];
            _inputPtrs = new nint[numInputs];
        }

        if (_outputHandles == null || _outputHandles.Length < numOutputs)
        {
            _outputHandles = new GCHandle[numOutputs];
            _outputPtrs = new nint[numOutputs];
        }
    }

    private void FreeBufferHandles(int numInputs, int numOutputs)
    {
        for (int i = 0; i < numInputs && _inputHandles != null && i < _inputHandles.Length; i++)
        {
            if (_inputHandles[i].IsAllocated)
                _inputHandles[i].Free();
        }

        for (int i = 0; i < numOutputs && _outputHandles != null && i < _outputHandles.Length; i++)
        {
            if (_outputHandles[i].IsAllocated)
                _outputHandles[i].Free();
        }
    }

    public void StartProcessing()
    {
        ThrowIfDisposed();
        VstBridgeNative.StartProcessing(_handle);
    }

    public void StopProcessing()
    {
        ThrowIfDisposed();
        VstBridgeNative.StopProcessing(_handle);
    }

    public void Suspend()
    {
        ThrowIfDisposed();
        VstBridgeNative.Suspend(_handle);
    }

    public void Resume()
    {
        ThrowIfDisposed();
        VstBridgeNative.Resume(_handle);
    }

    #endregion

    #region MIDI

    public void SendMidi(int status, int data1, int data2)
    {
        ThrowIfDisposed();
        VstBridgeNative.SendMidi(_handle, status, data1, data2);
    }

    public void SendMidiAt(int deltaFrames, int status, int data1, int data2)
    {
        ThrowIfDisposed();
        VstBridgeNative.SendMidiAt(_handle, deltaFrames, status, data1, data2);
    }

    public void NoteOn(int channel, int note, int velocity)
    {
        SendMidi(0x90 | (channel & 0x0F), note & 0x7F, velocity & 0x7F);
    }

    public void NoteOff(int channel, int note, int velocity = 0)
    {
        SendMidi(0x80 | (channel & 0x0F), note & 0x7F, velocity & 0x7F);
    }

    public void ControlChange(int channel, int cc, int value)
    {
        SendMidi(0xB0 | (channel & 0x0F), cc & 0x7F, value & 0x7F);
    }

    public void ProgramChange(int channel, int program)
    {
        SendMidi(0xC0 | (channel & 0x0F), program & 0x7F, 0);
    }

    public void AllNotesOff()
    {
        ThrowIfDisposed();
        VstBridgeNative.AllNotesOff(_handle);
    }

    public void ClearMidi()
    {
        ThrowIfDisposed();
        VstBridgeNative.ClearMidi(_handle);
    }

    #endregion

    #region State / Programs

    public unsafe byte[]? GetState()
    {
        ThrowIfDisposed();

        // First query the size
        int size = VstBridgeNative.GetState(_handle, null, 0);
        if (size <= 0) return null;

        // Allocate and get state
        byte[] data = new byte[size];
        fixed (byte* dataPtr = data)
        {
            int actualSize = VstBridgeNative.GetState(_handle, dataPtr, size);
            if (actualSize <= 0) return null;
            if (actualSize < size)
                Array.Resize(ref data, actualSize);
        }

        return data;
    }

    public unsafe bool SetState(byte[] data)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(data);

        fixed (byte* dataPtr = data)
        {
            return VstBridgeNative.SetState(_handle, dataPtr, data.Length) == VstBridgeNative.VSTBRIDGE_OK;
        }
    }

    public int ProgramCount => VstBridgeNative.GetProgramCount(_handle);

    public int CurrentProgram
    {
        get
        {
            ThrowIfDisposed();
            return VstBridgeNative.GetProgram(_handle);
        }
        set
        {
            ThrowIfDisposed();
            VstBridgeNative.SetProgram(_handle, value);
        }
    }

    public unsafe string GetProgramName(int index)
    {
        ThrowIfDisposed();
        const int maxLen = 256;
        byte* buffer = stackalloc byte[maxLen];
        VstBridgeNative.GetProgramName(_handle, index, buffer, maxLen);
        return GetStringFromBuffer(buffer, maxLen);
    }

    #endregion

    #region Editor

    public void OpenEditor(nint parentWindow)
    {
        ThrowIfDisposed();
        if (!_hasEditor) return;
        VstBridgeNative.OpenEditor(_handle, parentWindow);
    }

    public void CloseEditor()
    {
        ThrowIfDisposed();
        VstBridgeNative.CloseEditor(_handle);
    }

    public unsafe (int Width, int Height) GetEditorSize()
    {
        ThrowIfDisposed();
        int width = 0, height = 0;
        VstBridgeNative.GetEditorSize(_handle, &width, &height);
        return (width, height);
    }

    public void EditorIdle()
    {
        ThrowIfDisposed();
        VstBridgeNative.EditorIdle(_handle);
    }

    #endregion

    #region Transport

    public void SetTransport(double samplePos, double tempo, int timeSigNum = 4, int timeSigDen = 4)
    {
        ThrowIfDisposed();
        VstBridgeNative.SetTransport(_handle, samplePos, tempo, timeSigNum, timeSigDen);
    }

    public void SetTransportState(bool playing, bool recording = false, bool looping = false)
    {
        ThrowIfDisposed();
        VstBridgeNative.SetTransportState(_handle, playing ? 1 : 0, recording ? 1 : 0, looping ? 1 : 0);
    }

    #endregion

    #region Helpers

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_handle != nint.Zero)
        {
            // Close editor first if open
            if (IsEditorOpen)
            {
                VstBridgeNative.CloseEditor(_handle);
            }

            VstBridgeNative.UnloadPlugin(_handle);
            _handle = nint.Zero;
        }
    }

    #endregion
}
