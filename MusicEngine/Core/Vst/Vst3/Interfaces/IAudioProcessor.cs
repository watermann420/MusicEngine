// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Runtime.InteropServices;
using MusicEngine.Core.Vst.Vst3.Structures;

namespace MusicEngine.Core.Vst.Vst3.Interfaces;

/// <summary>
/// IAudioProcessor vtable structure (extends IPluginBase)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct IAudioProcessorVtbl
{
    // FUnknown methods
    public IntPtr QueryInterface;
    public IntPtr AddRef;
    public IntPtr Release;

    // IPluginBase methods
    public IntPtr Initialize;
    public IntPtr Terminate;

    // IAudioProcessor methods
    public IntPtr SetBusArrangements;
    public IntPtr GetBusArrangement;
    public IntPtr CanProcessSampleSize;
    public IntPtr GetLatencySamples;
    public IntPtr SetupProcessing;
    public IntPtr SetProcessing;
    public IntPtr Process;
}

// Delegate types for IAudioProcessor methods
[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int SetBusArrangementsDelegate(
    IntPtr self,
    IntPtr inputs,
    int numIns,
    IntPtr outputs,
    int numOuts);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int GetBusArrangementDelegate(
    IntPtr self,
    int dir,
    int index,
    out ulong arr);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int CanProcessSampleSizeDelegate(IntPtr self, int symbolicSampleSize);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate uint GetLatencySamplesDelegate(IntPtr self);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int SetupProcessingDelegate(IntPtr self, ref ProcessSetup setup);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int SetProcessingDelegate(IntPtr self, [MarshalAs(UnmanagedType.U1)] bool state);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int ProcessDelegate(IntPtr self, ref ProcessData data);

/// <summary>
/// Managed wrapper for IAudioProcessor COM interface
/// </summary>
internal class AudioProcessorWrapper : IDisposable
{
    private IntPtr _processorPtr;
    private IntPtr _vtblPtr;
    private IAudioProcessorVtbl _vtbl;

    // Cached delegates
    private QueryInterfaceDelegate? _queryInterface;
    private AddRefDelegate? _addRef;
    private ReleaseDelegate? _release;
    private InitializeDelegate? _initialize;
    private TerminateDelegate? _terminate;
    private SetBusArrangementsDelegate? _setBusArrangements;
    private GetBusArrangementDelegate? _getBusArrangement;
    private CanProcessSampleSizeDelegate? _canProcessSampleSize;
    private GetLatencySamplesDelegate? _getLatencySamples;
    private SetupProcessingDelegate? _setupProcessing;
    private SetProcessingDelegate? _setProcessing;
    private ProcessDelegate? _process;

    public IntPtr NativePtr => _processorPtr;
    public bool IsInitialized { get; private set; }
    public bool IsProcessing { get; private set; }

    public AudioProcessorWrapper(IntPtr processorPtr)
    {
        if (processorPtr == IntPtr.Zero)
            throw new ArgumentNullException(nameof(processorPtr));

        _processorPtr = processorPtr;

        // Read vtable pointer
        _vtblPtr = Marshal.ReadIntPtr(_processorPtr);
        _vtbl = Marshal.PtrToStructure<IAudioProcessorVtbl>(_vtblPtr);

        CacheDelegates();
    }

    private void CacheDelegates()
    {
        _queryInterface = Marshal.GetDelegateForFunctionPointer<QueryInterfaceDelegate>(_vtbl.QueryInterface);
        _addRef = Marshal.GetDelegateForFunctionPointer<AddRefDelegate>(_vtbl.AddRef);
        _release = Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(_vtbl.Release);
        _initialize = Marshal.GetDelegateForFunctionPointer<InitializeDelegate>(_vtbl.Initialize);
        _terminate = Marshal.GetDelegateForFunctionPointer<TerminateDelegate>(_vtbl.Terminate);
        _setBusArrangements = Marshal.GetDelegateForFunctionPointer<SetBusArrangementsDelegate>(_vtbl.SetBusArrangements);
        _getBusArrangement = Marshal.GetDelegateForFunctionPointer<GetBusArrangementDelegate>(_vtbl.GetBusArrangement);
        _canProcessSampleSize = Marshal.GetDelegateForFunctionPointer<CanProcessSampleSizeDelegate>(_vtbl.CanProcessSampleSize);
        _getLatencySamples = Marshal.GetDelegateForFunctionPointer<GetLatencySamplesDelegate>(_vtbl.GetLatencySamples);
        _setupProcessing = Marshal.GetDelegateForFunctionPointer<SetupProcessingDelegate>(_vtbl.SetupProcessing);
        _setProcessing = Marshal.GetDelegateForFunctionPointer<SetProcessingDelegate>(_vtbl.SetProcessing);
        _process = Marshal.GetDelegateForFunctionPointer<ProcessDelegate>(_vtbl.Process);
    }

    public int QueryInterface(Guid iid, out IntPtr ppvObject)
    {
        if (_queryInterface == null)
        {
            ppvObject = IntPtr.Zero;
            return (int)Vst3Result.NotImplemented;
        }
        return _queryInterface(_processorPtr, ref iid, out ppvObject);
    }

    public uint AddRef()
    {
        return _addRef?.Invoke(_processorPtr) ?? 0;
    }

    public uint Release()
    {
        return _release?.Invoke(_processorPtr) ?? 0;
    }

    public int Initialize(IntPtr hostContext)
    {
        if (_initialize == null)
            return (int)Vst3Result.NotImplemented;

        int result = _initialize(_processorPtr, hostContext);
        if (result == (int)Vst3Result.Ok)
            IsInitialized = true;
        return result;
    }

    public int Terminate()
    {
        if (_terminate == null)
            return (int)Vst3Result.NotImplemented;

        int result = _terminate(_processorPtr);
        if (result == (int)Vst3Result.Ok)
            IsInitialized = false;
        return result;
    }

    /// <summary>
    /// Set speaker arrangements for input and output buses.
    /// </summary>
    /// <param name="inputs">Pointer to array of speaker arrangements for inputs</param>
    /// <param name="numIns">Number of input buses</param>
    /// <param name="outputs">Pointer to array of speaker arrangements for outputs</param>
    /// <param name="numOuts">Number of output buses</param>
    public int SetBusArrangements(IntPtr inputs, int numIns, IntPtr outputs, int numOuts)
    {
        if (_setBusArrangements == null)
            return (int)Vst3Result.NotImplemented;
        return _setBusArrangements(_processorPtr, inputs, numIns, outputs, numOuts);
    }

    /// <summary>
    /// Set speaker arrangements using arrays of arrangements.
    /// </summary>
    public int SetBusArrangements(ulong[] inputArrangements, ulong[] outputArrangements)
    {
        if (_setBusArrangements == null)
            return (int)Vst3Result.NotImplemented;

        IntPtr inputsPtr = IntPtr.Zero;
        IntPtr outputsPtr = IntPtr.Zero;

        try
        {
            int numIns = inputArrangements?.Length ?? 0;
            int numOuts = outputArrangements?.Length ?? 0;

            if (numIns > 0 && inputArrangements != null)
            {
                inputsPtr = Marshal.AllocHGlobal(numIns * sizeof(ulong));
                Marshal.Copy((long[])(object)inputArrangements, 0, inputsPtr, numIns);
            }

            if (numOuts > 0 && outputArrangements != null)
            {
                outputsPtr = Marshal.AllocHGlobal(numOuts * sizeof(ulong));
                Marshal.Copy((long[])(object)outputArrangements, 0, outputsPtr, numOuts);
            }

            return _setBusArrangements(_processorPtr, inputsPtr, numIns, outputsPtr, numOuts);
        }
        finally
        {
            if (inputsPtr != IntPtr.Zero)
                Marshal.FreeHGlobal(inputsPtr);
            if (outputsPtr != IntPtr.Zero)
                Marshal.FreeHGlobal(outputsPtr);
        }
    }

    /// <summary>
    /// Get speaker arrangement for a specific bus.
    /// </summary>
    /// <param name="direction">Bus direction (input or output)</param>
    /// <param name="index">Bus index</param>
    /// <param name="arrangement">Output speaker arrangement</param>
    public int GetBusArrangement(Vst3BusDirection direction, int index, out ulong arrangement)
    {
        if (_getBusArrangement == null)
        {
            arrangement = 0;
            return (int)Vst3Result.NotImplemented;
        }
        return _getBusArrangement(_processorPtr, (int)direction, index, out arrangement);
    }

    /// <summary>
    /// Check if the processor can handle the specified sample size.
    /// </summary>
    /// <param name="symbolicSampleSize">Sample size (32-bit or 64-bit)</param>
    public int CanProcessSampleSize(Vst3SymbolicSampleSize symbolicSampleSize)
    {
        if (_canProcessSampleSize == null)
            return (int)Vst3Result.NotImplemented;
        return _canProcessSampleSize(_processorPtr, (int)symbolicSampleSize);
    }

    /// <summary>
    /// Get the processing latency in samples.
    /// </summary>
    public uint GetLatencySamples()
    {
        return _getLatencySamples?.Invoke(_processorPtr) ?? 0;
    }

    /// <summary>
    /// Setup processing parameters.
    /// </summary>
    /// <param name="setup">Processing setup configuration</param>
    public int SetupProcessing(ref ProcessSetup setup)
    {
        if (_setupProcessing == null)
            return (int)Vst3Result.NotImplemented;
        return _setupProcessing(_processorPtr, ref setup);
    }

    /// <summary>
    /// Enable or disable processing.
    /// </summary>
    /// <param name="state">True to enable processing, false to disable</param>
    public int SetProcessing(bool state)
    {
        if (_setProcessing == null)
            return (int)Vst3Result.NotImplemented;

        int result = _setProcessing(_processorPtr, state);
        if (result == (int)Vst3Result.Ok)
            IsProcessing = state;
        return result;
    }

    /// <summary>
    /// Process audio data.
    /// </summary>
    /// <param name="data">Process data containing audio buffers and events</param>
    public int Process(ref ProcessData data)
    {
        if (_process == null)
            return (int)Vst3Result.NotImplemented;
        return _process(_processorPtr, ref data);
    }

    public void Dispose()
    {
        if (_processorPtr != IntPtr.Zero)
        {
            if (IsProcessing)
                SetProcessing(false);
            if (IsInitialized)
                Terminate();
            Release();
            _processorPtr = IntPtr.Zero;
        }
    }
}
