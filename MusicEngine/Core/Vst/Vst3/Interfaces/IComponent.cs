// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Runtime.InteropServices;
using MusicEngine.Core.Vst.Vst3.Structures;

namespace MusicEngine.Core.Vst.Vst3.Interfaces;

/// <summary>
/// IPluginBase vtable structure
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct IPluginBaseVtbl
{
    // FUnknown methods
    public IntPtr QueryInterface;
    public IntPtr AddRef;
    public IntPtr Release;

    // IPluginBase methods
    public IntPtr Initialize;
    public IntPtr Terminate;
}

/// <summary>
/// IComponent vtable structure (extends IPluginBase)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct IComponentVtbl
{
    // FUnknown methods
    public IntPtr QueryInterface;
    public IntPtr AddRef;
    public IntPtr Release;

    // IPluginBase methods
    public IntPtr Initialize;
    public IntPtr Terminate;

    // IComponent methods
    public IntPtr GetControllerClassId;
    public IntPtr SetIoMode;
    public IntPtr GetBusCount;
    public IntPtr GetBusInfo;
    public IntPtr GetRoutingInfo;
    public IntPtr ActivateBus;
    public IntPtr SetActive;
    public IntPtr SetState;
    public IntPtr GetState;
}

/// <summary>
/// Bus info structure for VST3
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct Vst3BusInfoInternal
{
    public int MediaType;    // Vst3MediaType
    public int Direction;    // Vst3BusDirection
    public int ChannelCount;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string Name;
    public int BusType;      // Vst3BusType
    public uint Flags;       // kDefaultActive = 1
}

/// <summary>
/// Routing info structure
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct Vst3RoutingInfo
{
    public int MediaType;
    public int BusIndex;
    public int Channel;
}

// Delegate types for IComponent methods
[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int InitializeDelegate(IntPtr self, IntPtr context);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int TerminateDelegate(IntPtr self);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int GetControllerClassIdDelegate(IntPtr self, out Vst3Tuid classId);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int SetIoModeDelegate(IntPtr self, int mode);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int GetBusCountDelegate(IntPtr self, int mediaType, int direction);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int GetBusInfoDelegate(IntPtr self, int mediaType, int direction, int index, out Vst3BusInfoInternal bus);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int GetRoutingInfoDelegate(IntPtr self, ref Vst3RoutingInfo inInfo, out Vst3RoutingInfo outInfo);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int ActivateBusDelegate(IntPtr self, int mediaType, int direction, int index, [MarshalAs(UnmanagedType.U1)] bool state);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int SetActiveDelegate(IntPtr self, [MarshalAs(UnmanagedType.U1)] bool state);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int SetStateDelegate(IntPtr self, IntPtr state);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int GetStateDelegate(IntPtr self, IntPtr state);

/// <summary>
/// Managed wrapper for IComponent COM interface
/// </summary>
internal class ComponentWrapper : IDisposable
{
    private IntPtr _componentPtr;
    private IntPtr _vtblPtr;
    private IComponentVtbl _vtbl;

    // Cached delegates
    private QueryInterfaceDelegate? _queryInterface;
    private AddRefDelegate? _addRef;
    private ReleaseDelegate? _release;
    private InitializeDelegate? _initialize;
    private TerminateDelegate? _terminate;
    private GetControllerClassIdDelegate? _getControllerClassId;
    private SetIoModeDelegate? _setIoMode;
    private GetBusCountDelegate? _getBusCount;
    private GetBusInfoDelegate? _getBusInfo;
    private GetRoutingInfoDelegate? _getRoutingInfo;
    private ActivateBusDelegate? _activateBus;
    private SetActiveDelegate? _setActive;
    private SetStateDelegate? _setState;
    private GetStateDelegate? _getState;

    public IntPtr NativePtr => _componentPtr;
    public bool IsInitialized { get; private set; }

    public ComponentWrapper(IntPtr componentPtr)
    {
        if (componentPtr == IntPtr.Zero)
            throw new ArgumentNullException(nameof(componentPtr));

        _componentPtr = componentPtr;

        // Read vtable pointer
        _vtblPtr = Marshal.ReadIntPtr(_componentPtr);
        _vtbl = Marshal.PtrToStructure<IComponentVtbl>(_vtblPtr);

        CacheDelegates();
    }

    private void CacheDelegates()
    {
        _queryInterface = Marshal.GetDelegateForFunctionPointer<QueryInterfaceDelegate>(_vtbl.QueryInterface);
        _addRef = Marshal.GetDelegateForFunctionPointer<AddRefDelegate>(_vtbl.AddRef);
        _release = Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(_vtbl.Release);
        _initialize = Marshal.GetDelegateForFunctionPointer<InitializeDelegate>(_vtbl.Initialize);
        _terminate = Marshal.GetDelegateForFunctionPointer<TerminateDelegate>(_vtbl.Terminate);
        _getControllerClassId = Marshal.GetDelegateForFunctionPointer<GetControllerClassIdDelegate>(_vtbl.GetControllerClassId);
        _setIoMode = Marshal.GetDelegateForFunctionPointer<SetIoModeDelegate>(_vtbl.SetIoMode);
        _getBusCount = Marshal.GetDelegateForFunctionPointer<GetBusCountDelegate>(_vtbl.GetBusCount);
        _getBusInfo = Marshal.GetDelegateForFunctionPointer<GetBusInfoDelegate>(_vtbl.GetBusInfo);
        _getRoutingInfo = Marshal.GetDelegateForFunctionPointer<GetRoutingInfoDelegate>(_vtbl.GetRoutingInfo);
        _activateBus = Marshal.GetDelegateForFunctionPointer<ActivateBusDelegate>(_vtbl.ActivateBus);
        _setActive = Marshal.GetDelegateForFunctionPointer<SetActiveDelegate>(_vtbl.SetActive);
        _setState = Marshal.GetDelegateForFunctionPointer<SetStateDelegate>(_vtbl.SetState);
        _getState = Marshal.GetDelegateForFunctionPointer<GetStateDelegate>(_vtbl.GetState);
    }

    public int QueryInterface(Guid iid, out IntPtr ppvObject)
    {
        if (_queryInterface == null)
        {
            ppvObject = IntPtr.Zero;
            return (int)Vst3Result.NotImplemented;
        }
        return _queryInterface(_componentPtr, ref iid, out ppvObject);
    }

    public uint AddRef()
    {
        return _addRef?.Invoke(_componentPtr) ?? 0;
    }

    public uint Release()
    {
        return _release?.Invoke(_componentPtr) ?? 0;
    }

    public int Initialize(IntPtr hostContext)
    {
        if (_initialize == null)
            return (int)Vst3Result.NotImplemented;

        int result = _initialize(_componentPtr, hostContext);
        if (result == (int)Vst3Result.Ok)
            IsInitialized = true;
        return result;
    }

    public int Terminate()
    {
        if (_terminate == null)
            return (int)Vst3Result.NotImplemented;

        int result = _terminate(_componentPtr);
        if (result == (int)Vst3Result.Ok)
            IsInitialized = false;
        return result;
    }

    public int GetControllerClassId(out Vst3Tuid classId)
    {
        if (_getControllerClassId == null)
        {
            classId = default;
            return (int)Vst3Result.NotImplemented;
        }
        return _getControllerClassId(_componentPtr, out classId);
    }

    public int SetIoMode(Vst3IoMode mode)
    {
        if (_setIoMode == null)
            return (int)Vst3Result.NotImplemented;
        return _setIoMode(_componentPtr, (int)mode);
    }

    public int GetBusCount(Vst3MediaType mediaType, Vst3BusDirection direction)
    {
        if (_getBusCount == null)
            return 0;
        return _getBusCount(_componentPtr, (int)mediaType, (int)direction);
    }

    public int GetBusInfo(Vst3MediaType mediaType, Vst3BusDirection direction, int index, out Vst3BusInfo busInfo)
    {
        busInfo = new Vst3BusInfo();

        if (_getBusInfo == null)
            return (int)Vst3Result.NotImplemented;

        int result = _getBusInfo(_componentPtr, (int)mediaType, (int)direction, index, out Vst3BusInfoInternal internalInfo);

        if (result == (int)Vst3Result.Ok)
        {
            busInfo.Name = internalInfo.Name ?? "";
            busInfo.MediaType = (Vst3MediaType)internalInfo.MediaType;
            busInfo.Direction = (Vst3BusDirection)internalInfo.Direction;
            busInfo.ChannelCount = internalInfo.ChannelCount;
            busInfo.BusType = (Vst3BusType)internalInfo.BusType;
            busInfo.IsDefaultActive = (internalInfo.Flags & 1) != 0;
        }

        return result;
    }

    public int GetRoutingInfo(ref Vst3RoutingInfo inInfo, out Vst3RoutingInfo outInfo)
    {
        if (_getRoutingInfo == null)
        {
            outInfo = default;
            return (int)Vst3Result.NotImplemented;
        }
        return _getRoutingInfo(_componentPtr, ref inInfo, out outInfo);
    }

    public int ActivateBus(Vst3MediaType mediaType, Vst3BusDirection direction, int index, bool state)
    {
        if (_activateBus == null)
            return (int)Vst3Result.NotImplemented;
        return _activateBus(_componentPtr, (int)mediaType, (int)direction, index, state);
    }

    public int SetActive(bool state)
    {
        if (_setActive == null)
            return (int)Vst3Result.NotImplemented;
        return _setActive(_componentPtr, state);
    }

    public int SetState(IntPtr state)
    {
        if (_setState == null)
            return (int)Vst3Result.NotImplemented;
        return _setState(_componentPtr, state);
    }

    public int GetState(IntPtr state)
    {
        if (_getState == null)
            return (int)Vst3Result.NotImplemented;
        return _getState(_componentPtr, state);
    }

    public void Dispose()
    {
        if (_componentPtr != IntPtr.Zero)
        {
            if (IsInitialized)
                Terminate();
            Release();
            _componentPtr = IntPtr.Zero;
        }
    }
}
