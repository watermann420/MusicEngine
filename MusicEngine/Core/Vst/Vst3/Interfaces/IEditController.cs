// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Runtime.InteropServices;
using MusicEngine.Core.Vst.Vst3.Structures;

namespace MusicEngine.Core.Vst.Vst3.Interfaces;

/// <summary>
/// Parameter info internal structure for marshaling
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct ParameterInfoInternal
{
    public uint Id;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string Title;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string ShortTitle;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string Units;

    public int StepCount;
    public double DefaultNormalizedValue;
    public int UnitId;
    public int Flags; // Vst3ParameterFlags
}

/// <summary>
/// Managed parameter info structure
/// </summary>
public class Vst3ParameterInfo
{
    public uint Id { get; set; }
    public string Title { get; set; } = "";
    public string ShortTitle { get; set; } = "";
    public string Units { get; set; } = "";
    public int StepCount { get; set; }
    public double DefaultNormalizedValue { get; set; }
    public int UnitId { get; set; }
    public Vst3ParameterFlags Flags { get; set; }
}

/// <summary>
/// IEditController vtable structure (extends IPluginBase)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct IEditControllerVtbl
{
    // FUnknown methods
    public IntPtr QueryInterface;
    public IntPtr AddRef;
    public IntPtr Release;

    // IPluginBase methods
    public IntPtr Initialize;
    public IntPtr Terminate;

    // IEditController methods
    public IntPtr SetComponentState;
    public IntPtr SetState;
    public IntPtr GetState;
    public IntPtr GetParameterCount;
    public IntPtr GetParameterInfo;
    public IntPtr GetParamStringByValue;
    public IntPtr GetParamValueByString;
    public IntPtr NormalizedParamToPlain;
    public IntPtr PlainParamToNormalized;
    public IntPtr GetParamNormalized;
    public IntPtr SetParamNormalized;
    public IntPtr SetComponentHandler;
    public IntPtr CreateView;
}

// Delegate types for IEditController methods
[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int SetComponentStateDelegate(IntPtr self, IntPtr state);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int EditController_SetStateDelegate(IntPtr self, IntPtr state);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int EditController_GetStateDelegate(IntPtr self, IntPtr state);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int GetParameterCountDelegate(IntPtr self);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int GetParameterInfoDelegate(IntPtr self, int paramIndex, out ParameterInfoInternal info);

[UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
internal delegate int GetParamStringByValueDelegate(IntPtr self, uint id, double valueNormalized, IntPtr stringBuffer);

[UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
internal delegate int GetParamValueByStringDelegate(IntPtr self, uint id, IntPtr stringValue, out double valueNormalized);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate double NormalizedParamToPlainDelegate(IntPtr self, uint id, double valueNormalized);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate double PlainParamToNormalizedDelegate(IntPtr self, uint id, double plainValue);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate double GetParamNormalizedDelegate(IntPtr self, uint id);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int SetParamNormalizedDelegate(IntPtr self, uint id, double value);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int SetComponentHandlerDelegate(IntPtr self, IntPtr handler);

[UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
internal delegate IntPtr CreateViewDelegate(IntPtr self, [MarshalAs(UnmanagedType.LPStr)] string name);

/// <summary>
/// Managed wrapper for IEditController COM interface
/// </summary>
internal class EditControllerWrapper : IDisposable
{
    private IntPtr _controllerPtr;
    private IntPtr _vtblPtr;
    private IEditControllerVtbl _vtbl;

    // Cached delegates - FUnknown
    private QueryInterfaceDelegate? _queryInterface;
    private AddRefDelegate? _addRef;
    private ReleaseDelegate? _release;

    // Cached delegates - IPluginBase
    private InitializeDelegate? _initialize;
    private TerminateDelegate? _terminate;

    // Cached delegates - IEditController
    private SetComponentStateDelegate? _setComponentState;
    private EditController_SetStateDelegate? _setState;
    private EditController_GetStateDelegate? _getState;
    private GetParameterCountDelegate? _getParameterCount;
    private GetParameterInfoDelegate? _getParameterInfo;
    private GetParamStringByValueDelegate? _getParamStringByValue;
    private GetParamValueByStringDelegate? _getParamValueByString;
    private NormalizedParamToPlainDelegate? _normalizedParamToPlain;
    private PlainParamToNormalizedDelegate? _plainParamToNormalized;
    private GetParamNormalizedDelegate? _getParamNormalized;
    private SetParamNormalizedDelegate? _setParamNormalized;
    private SetComponentHandlerDelegate? _setComponentHandler;
    private CreateViewDelegate? _createView;

    public IntPtr NativePtr => _controllerPtr;
    public bool IsInitialized { get; private set; }

    public EditControllerWrapper(IntPtr controllerPtr)
    {
        if (controllerPtr == IntPtr.Zero)
            throw new ArgumentNullException(nameof(controllerPtr));

        _controllerPtr = controllerPtr;

        // Read vtable pointer
        _vtblPtr = Marshal.ReadIntPtr(_controllerPtr);
        _vtbl = Marshal.PtrToStructure<IEditControllerVtbl>(_vtblPtr);

        CacheDelegates();
    }

    private void CacheDelegates()
    {
        // FUnknown
        _queryInterface = Marshal.GetDelegateForFunctionPointer<QueryInterfaceDelegate>(_vtbl.QueryInterface);
        _addRef = Marshal.GetDelegateForFunctionPointer<AddRefDelegate>(_vtbl.AddRef);
        _release = Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(_vtbl.Release);

        // IPluginBase
        _initialize = Marshal.GetDelegateForFunctionPointer<InitializeDelegate>(_vtbl.Initialize);
        _terminate = Marshal.GetDelegateForFunctionPointer<TerminateDelegate>(_vtbl.Terminate);

        // IEditController
        _setComponentState = Marshal.GetDelegateForFunctionPointer<SetComponentStateDelegate>(_vtbl.SetComponentState);
        _setState = Marshal.GetDelegateForFunctionPointer<EditController_SetStateDelegate>(_vtbl.SetState);
        _getState = Marshal.GetDelegateForFunctionPointer<EditController_GetStateDelegate>(_vtbl.GetState);
        _getParameterCount = Marshal.GetDelegateForFunctionPointer<GetParameterCountDelegate>(_vtbl.GetParameterCount);
        _getParameterInfo = Marshal.GetDelegateForFunctionPointer<GetParameterInfoDelegate>(_vtbl.GetParameterInfo);
        _getParamStringByValue = Marshal.GetDelegateForFunctionPointer<GetParamStringByValueDelegate>(_vtbl.GetParamStringByValue);
        _getParamValueByString = Marshal.GetDelegateForFunctionPointer<GetParamValueByStringDelegate>(_vtbl.GetParamValueByString);
        _normalizedParamToPlain = Marshal.GetDelegateForFunctionPointer<NormalizedParamToPlainDelegate>(_vtbl.NormalizedParamToPlain);
        _plainParamToNormalized = Marshal.GetDelegateForFunctionPointer<PlainParamToNormalizedDelegate>(_vtbl.PlainParamToNormalized);
        _getParamNormalized = Marshal.GetDelegateForFunctionPointer<GetParamNormalizedDelegate>(_vtbl.GetParamNormalized);
        _setParamNormalized = Marshal.GetDelegateForFunctionPointer<SetParamNormalizedDelegate>(_vtbl.SetParamNormalized);
        _setComponentHandler = Marshal.GetDelegateForFunctionPointer<SetComponentHandlerDelegate>(_vtbl.SetComponentHandler);
        _createView = Marshal.GetDelegateForFunctionPointer<CreateViewDelegate>(_vtbl.CreateView);
    }

    #region FUnknown Methods

    public int QueryInterface(Guid iid, out IntPtr ppvObject)
    {
        if (_queryInterface == null)
        {
            ppvObject = IntPtr.Zero;
            return (int)Vst3Result.NotImplemented;
        }
        return _queryInterface(_controllerPtr, ref iid, out ppvObject);
    }

    public uint AddRef()
    {
        return _addRef?.Invoke(_controllerPtr) ?? 0;
    }

    public uint Release()
    {
        return _release?.Invoke(_controllerPtr) ?? 0;
    }

    #endregion

    #region IPluginBase Methods

    public int Initialize(IntPtr hostContext)
    {
        if (_initialize == null)
            return (int)Vst3Result.NotImplemented;

        int result = _initialize(_controllerPtr, hostContext);
        if (result == (int)Vst3Result.Ok)
            IsInitialized = true;
        return result;
    }

    public int Terminate()
    {
        if (_terminate == null)
            return (int)Vst3Result.NotImplemented;

        int result = _terminate(_controllerPtr);
        if (result == (int)Vst3Result.Ok)
            IsInitialized = false;
        return result;
    }

    #endregion

    #region IEditController Methods

    /// <summary>
    /// Set the component state (processor state)
    /// </summary>
    public int SetComponentState(IntPtr state)
    {
        if (_setComponentState == null)
            return (int)Vst3Result.NotImplemented;
        return _setComponentState(_controllerPtr, state);
    }

    /// <summary>
    /// Set the controller state
    /// </summary>
    public int SetState(IntPtr state)
    {
        if (_setState == null)
            return (int)Vst3Result.NotImplemented;
        return _setState(_controllerPtr, state);
    }

    /// <summary>
    /// Get the controller state
    /// </summary>
    public int GetState(IntPtr state)
    {
        if (_getState == null)
            return (int)Vst3Result.NotImplemented;
        return _getState(_controllerPtr, state);
    }

    /// <summary>
    /// Get the number of parameters
    /// </summary>
    public int GetParameterCount()
    {
        return _getParameterCount?.Invoke(_controllerPtr) ?? 0;
    }

    /// <summary>
    /// Get parameter info by index
    /// </summary>
    public int GetParameterInfo(int paramIndex, out Vst3ParameterInfo paramInfo)
    {
        paramInfo = new Vst3ParameterInfo();

        if (_getParameterInfo == null)
            return (int)Vst3Result.NotImplemented;

        int result = _getParameterInfo(_controllerPtr, paramIndex, out ParameterInfoInternal internalInfo);

        if (result == (int)Vst3Result.Ok)
        {
            paramInfo.Id = internalInfo.Id;
            paramInfo.Title = internalInfo.Title ?? "";
            paramInfo.ShortTitle = internalInfo.ShortTitle ?? "";
            paramInfo.Units = internalInfo.Units ?? "";
            paramInfo.StepCount = internalInfo.StepCount;
            paramInfo.DefaultNormalizedValue = internalInfo.DefaultNormalizedValue;
            paramInfo.UnitId = internalInfo.UnitId;
            paramInfo.Flags = (Vst3ParameterFlags)internalInfo.Flags;
        }

        return result;
    }

    /// <summary>
    /// Get display string for a normalized parameter value
    /// </summary>
    public int GetParamStringByValue(uint id, double valueNormalized, out string displayString)
    {
        displayString = "";

        if (_getParamStringByValue == null)
            return (int)Vst3Result.NotImplemented;

        // Allocate buffer for string (128 Unicode characters)
        IntPtr buffer = Marshal.AllocHGlobal(256);
        try
        {
            int result = _getParamStringByValue(_controllerPtr, id, valueNormalized, buffer);
            if (result == (int)Vst3Result.Ok)
            {
                displayString = Marshal.PtrToStringUni(buffer) ?? "";
            }
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>
    /// Parse a string to a normalized parameter value
    /// </summary>
    public int GetParamValueByString(uint id, string stringValue, out double valueNormalized)
    {
        valueNormalized = 0.0;

        if (_getParamValueByString == null)
            return (int)Vst3Result.NotImplemented;

        IntPtr stringPtr = Marshal.StringToHGlobalUni(stringValue);
        try
        {
            return _getParamValueByString(_controllerPtr, id, stringPtr, out valueNormalized);
        }
        finally
        {
            Marshal.FreeHGlobal(stringPtr);
        }
    }

    /// <summary>
    /// Convert a normalized value to plain value
    /// </summary>
    public double NormalizedParamToPlain(uint id, double valueNormalized)
    {
        return _normalizedParamToPlain?.Invoke(_controllerPtr, id, valueNormalized) ?? valueNormalized;
    }

    /// <summary>
    /// Convert a plain value to normalized value
    /// </summary>
    public double PlainParamToNormalized(uint id, double plainValue)
    {
        return _plainParamToNormalized?.Invoke(_controllerPtr, id, plainValue) ?? plainValue;
    }

    /// <summary>
    /// Get the current normalized value of a parameter
    /// </summary>
    public double GetParamNormalized(uint id)
    {
        return _getParamNormalized?.Invoke(_controllerPtr, id) ?? 0.0;
    }

    /// <summary>
    /// Set the normalized value of a parameter
    /// </summary>
    public int SetParamNormalized(uint id, double value)
    {
        if (_setParamNormalized == null)
            return (int)Vst3Result.NotImplemented;
        return _setParamNormalized(_controllerPtr, id, value);
    }

    /// <summary>
    /// Set the component handler for host callbacks
    /// </summary>
    public int SetComponentHandler(IntPtr handler)
    {
        if (_setComponentHandler == null)
            return (int)Vst3Result.NotImplemented;
        return _setComponentHandler(_controllerPtr, handler);
    }

    /// <summary>
    /// Create an editor view
    /// </summary>
    /// <param name="name">View type name (typically "editor")</param>
    /// <returns>Pointer to IPlugView or IntPtr.Zero if not supported</returns>
    public IntPtr CreateView(string name)
    {
        return _createView?.Invoke(_controllerPtr, name) ?? IntPtr.Zero;
    }

    #endregion

    public void Dispose()
    {
        if (_controllerPtr != IntPtr.Zero)
        {
            if (IsInitialized)
                Terminate();
            Release();
            _controllerPtr = IntPtr.Zero;
        }
    }
}
