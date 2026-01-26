// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Runtime.InteropServices;
using MusicEngine.Core.Vst.Vst3.Structures;

namespace MusicEngine.Core.Vst.Vst3.Interfaces;

/// <summary>
/// Unit info internal structure for marshaling
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct UnitInfoInternal
{
    public int Id;
    public int ParentUnitId;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string Name;

    public int ProgramListId;
}

/// <summary>
/// Managed unit info structure
/// </summary>
public class Vst3UnitInfo
{
    public int Id { get; set; }
    public int ParentUnitId { get; set; }
    public string Name { get; set; } = "";
    public int ProgramListId { get; set; }
}

/// <summary>
/// Program list info internal structure for marshaling
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct ProgramListInfoInternal
{
    public int Id;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string Name;

    public int ProgramCount;
}

/// <summary>
/// Managed program list info structure
/// </summary>
public class Vst3ProgramListInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int ProgramCount { get; set; }
}

/// <summary>
/// IUnitInfo vtable structure (extends FUnknown)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct IUnitInfoVtbl
{
    // FUnknown methods
    public IntPtr QueryInterface;
    public IntPtr AddRef;
    public IntPtr Release;

    // IUnitInfo methods
    public IntPtr GetUnitCount;
    public IntPtr GetUnitInfo;
    public IntPtr GetProgramListCount;
    public IntPtr GetProgramListInfo;
    public IntPtr GetProgramName;
    public IntPtr GetProgramInfo;
    public IntPtr HasProgramPitchNames;
    public IntPtr GetProgramPitchName;
    public IntPtr GetSelectedUnit;
    public IntPtr SelectUnit;
    public IntPtr GetUnitByBus;
}

// Delegate types for IUnitInfo methods
[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int GetUnitCountDelegate(IntPtr self);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int GetUnitInfoDelegate(IntPtr self, int unitIndex, out UnitInfoInternal info);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int GetProgramListCountDelegate(IntPtr self);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int GetProgramListInfoDelegate(IntPtr self, int listIndex, out ProgramListInfoInternal info);

[UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
internal delegate int GetProgramNameDelegate(IntPtr self, int listId, int programIndex, IntPtr name);

[UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
internal delegate int GetProgramInfoDelegate(IntPtr self, int listId, int programIndex, IntPtr attributeId, IntPtr attributeValue);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int HasProgramPitchNamesDelegate(IntPtr self, int listId, int programIndex);

[UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
internal delegate int GetProgramPitchNameDelegate(IntPtr self, int listId, int programIndex, short midiPitch, IntPtr name);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int GetSelectedUnitDelegate(IntPtr self);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int SelectUnitDelegate(IntPtr self, int unitId);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int GetUnitByBusDelegate(IntPtr self, int type, int dir, int busIndex, int channel, out int unitId);

/// <summary>
/// Managed wrapper for IUnitInfo COM interface
/// </summary>
internal class UnitInfoWrapper : IDisposable
{
    private IntPtr _unitInfoPtr;
    private IntPtr _vtblPtr;
    private IUnitInfoVtbl _vtbl;

    // Cached delegates - FUnknown
    private QueryInterfaceDelegate? _queryInterface;
    private AddRefDelegate? _addRef;
    private ReleaseDelegate? _release;

    // Cached delegates - IUnitInfo
    private GetUnitCountDelegate? _getUnitCount;
    private GetUnitInfoDelegate? _getUnitInfo;
    private GetProgramListCountDelegate? _getProgramListCount;
    private GetProgramListInfoDelegate? _getProgramListInfo;
    private GetProgramNameDelegate? _getProgramName;
    private GetProgramInfoDelegate? _getProgramInfo;
    private HasProgramPitchNamesDelegate? _hasProgramPitchNames;
    private GetProgramPitchNameDelegate? _getProgramPitchName;
    private GetSelectedUnitDelegate? _getSelectedUnit;
    private SelectUnitDelegate? _selectUnit;
    private GetUnitByBusDelegate? _getUnitByBus;

    public IntPtr NativePtr => _unitInfoPtr;

    public UnitInfoWrapper(IntPtr unitInfoPtr)
    {
        if (unitInfoPtr == IntPtr.Zero)
            throw new ArgumentNullException(nameof(unitInfoPtr));

        _unitInfoPtr = unitInfoPtr;

        // Read vtable pointer
        _vtblPtr = Marshal.ReadIntPtr(_unitInfoPtr);
        _vtbl = Marshal.PtrToStructure<IUnitInfoVtbl>(_vtblPtr);

        CacheDelegates();
    }

    private void CacheDelegates()
    {
        // FUnknown
        _queryInterface = Marshal.GetDelegateForFunctionPointer<QueryInterfaceDelegate>(_vtbl.QueryInterface);
        _addRef = Marshal.GetDelegateForFunctionPointer<AddRefDelegate>(_vtbl.AddRef);
        _release = Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(_vtbl.Release);

        // IUnitInfo
        _getUnitCount = Marshal.GetDelegateForFunctionPointer<GetUnitCountDelegate>(_vtbl.GetUnitCount);
        _getUnitInfo = Marshal.GetDelegateForFunctionPointer<GetUnitInfoDelegate>(_vtbl.GetUnitInfo);
        _getProgramListCount = Marshal.GetDelegateForFunctionPointer<GetProgramListCountDelegate>(_vtbl.GetProgramListCount);
        _getProgramListInfo = Marshal.GetDelegateForFunctionPointer<GetProgramListInfoDelegate>(_vtbl.GetProgramListInfo);
        _getProgramName = Marshal.GetDelegateForFunctionPointer<GetProgramNameDelegate>(_vtbl.GetProgramName);
        _getProgramInfo = Marshal.GetDelegateForFunctionPointer<GetProgramInfoDelegate>(_vtbl.GetProgramInfo);
        _hasProgramPitchNames = Marshal.GetDelegateForFunctionPointer<HasProgramPitchNamesDelegate>(_vtbl.HasProgramPitchNames);
        _getProgramPitchName = Marshal.GetDelegateForFunctionPointer<GetProgramPitchNameDelegate>(_vtbl.GetProgramPitchName);
        _getSelectedUnit = Marshal.GetDelegateForFunctionPointer<GetSelectedUnitDelegate>(_vtbl.GetSelectedUnit);
        _selectUnit = Marshal.GetDelegateForFunctionPointer<SelectUnitDelegate>(_vtbl.SelectUnit);
        _getUnitByBus = Marshal.GetDelegateForFunctionPointer<GetUnitByBusDelegate>(_vtbl.GetUnitByBus);
    }

    #region FUnknown Methods

    public int QueryInterface(Guid iid, out IntPtr ppvObject)
    {
        if (_queryInterface == null)
        {
            ppvObject = IntPtr.Zero;
            return (int)Vst3Result.NotImplemented;
        }
        return _queryInterface(_unitInfoPtr, ref iid, out ppvObject);
    }

    public uint AddRef()
    {
        return _addRef?.Invoke(_unitInfoPtr) ?? 0;
    }

    public uint Release()
    {
        return _release?.Invoke(_unitInfoPtr) ?? 0;
    }

    #endregion

    #region IUnitInfo Methods

    /// <summary>
    /// Get the number of units
    /// </summary>
    public int GetUnitCount()
    {
        return _getUnitCount?.Invoke(_unitInfoPtr) ?? 0;
    }

    /// <summary>
    /// Get unit info by index
    /// </summary>
    public int GetUnitInfo(int unitIndex, out Vst3UnitInfo unitInfo)
    {
        unitInfo = new Vst3UnitInfo();

        if (_getUnitInfo == null)
            return (int)Vst3Result.NotImplemented;

        int result = _getUnitInfo(_unitInfoPtr, unitIndex, out UnitInfoInternal internalInfo);

        if (result == (int)Vst3Result.Ok)
        {
            unitInfo.Id = internalInfo.Id;
            unitInfo.ParentUnitId = internalInfo.ParentUnitId;
            unitInfo.Name = internalInfo.Name ?? "";
            unitInfo.ProgramListId = internalInfo.ProgramListId;
        }

        return result;
    }

    /// <summary>
    /// Get the number of program lists
    /// </summary>
    public int GetProgramListCount()
    {
        return _getProgramListCount?.Invoke(_unitInfoPtr) ?? 0;
    }

    /// <summary>
    /// Get program list info by index
    /// </summary>
    public int GetProgramListInfo(int listIndex, out Vst3ProgramListInfo listInfo)
    {
        listInfo = new Vst3ProgramListInfo();

        if (_getProgramListInfo == null)
            return (int)Vst3Result.NotImplemented;

        int result = _getProgramListInfo(_unitInfoPtr, listIndex, out ProgramListInfoInternal internalInfo);

        if (result == (int)Vst3Result.Ok)
        {
            listInfo.Id = internalInfo.Id;
            listInfo.Name = internalInfo.Name ?? "";
            listInfo.ProgramCount = internalInfo.ProgramCount;
        }

        return result;
    }

    /// <summary>
    /// Get program name by list ID and program index
    /// </summary>
    public int GetProgramName(int listId, int programIndex, out string programName)
    {
        programName = "";

        if (_getProgramName == null)
            return (int)Vst3Result.NotImplemented;

        // Allocate buffer for string (128 Unicode characters)
        IntPtr buffer = Marshal.AllocHGlobal(256);
        try
        {
            int result = _getProgramName(_unitInfoPtr, listId, programIndex, buffer);
            if (result == (int)Vst3Result.Ok)
            {
                programName = Marshal.PtrToStringUni(buffer) ?? "";
            }
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>
    /// Get program attribute by list ID, program index, and attribute ID
    /// </summary>
    public int GetProgramInfo(int listId, int programIndex, string attributeId, out string attributeValue)
    {
        attributeValue = "";

        if (_getProgramInfo == null)
            return (int)Vst3Result.NotImplemented;

        IntPtr attrIdPtr = Marshal.StringToHGlobalUni(attributeId);
        IntPtr attrValuePtr = Marshal.AllocHGlobal(256);
        try
        {
            int result = _getProgramInfo(_unitInfoPtr, listId, programIndex, attrIdPtr, attrValuePtr);
            if (result == (int)Vst3Result.Ok)
            {
                attributeValue = Marshal.PtrToStringUni(attrValuePtr) ?? "";
            }
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(attrIdPtr);
            Marshal.FreeHGlobal(attrValuePtr);
        }
    }

    /// <summary>
    /// Check if program has pitch names
    /// </summary>
    public int HasProgramPitchNames(int listId, int programIndex)
    {
        if (_hasProgramPitchNames == null)
            return (int)Vst3Result.NotImplemented;
        return _hasProgramPitchNames(_unitInfoPtr, listId, programIndex);
    }

    /// <summary>
    /// Get program pitch name
    /// </summary>
    public int GetProgramPitchName(int listId, int programIndex, short midiPitch, out string pitchName)
    {
        pitchName = "";

        if (_getProgramPitchName == null)
            return (int)Vst3Result.NotImplemented;

        // Allocate buffer for string (128 Unicode characters)
        IntPtr buffer = Marshal.AllocHGlobal(256);
        try
        {
            int result = _getProgramPitchName(_unitInfoPtr, listId, programIndex, midiPitch, buffer);
            if (result == (int)Vst3Result.Ok)
            {
                pitchName = Marshal.PtrToStringUni(buffer) ?? "";
            }
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>
    /// Get the currently selected unit
    /// </summary>
    public int GetSelectedUnit()
    {
        return _getSelectedUnit?.Invoke(_unitInfoPtr) ?? -1;
    }

    /// <summary>
    /// Select a unit by ID
    /// </summary>
    public int SelectUnit(int unitId)
    {
        if (_selectUnit == null)
            return (int)Vst3Result.NotImplemented;
        return _selectUnit(_unitInfoPtr, unitId);
    }

    /// <summary>
    /// Get unit by bus location
    /// </summary>
    /// <param name="type">Media type (audio/event)</param>
    /// <param name="dir">Bus direction (input/output)</param>
    /// <param name="busIndex">Bus index</param>
    /// <param name="channel">Channel within bus</param>
    /// <param name="unitId">Output: Unit ID</param>
    public int GetUnitByBus(int type, int dir, int busIndex, int channel, out int unitId)
    {
        unitId = -1;

        if (_getUnitByBus == null)
            return (int)Vst3Result.NotImplemented;

        return _getUnitByBus(_unitInfoPtr, type, dir, busIndex, channel, out unitId);
    }

    #endregion

    public void Dispose()
    {
        if (_unitInfoPtr != IntPtr.Zero)
        {
            Release();
            _unitInfoPtr = IntPtr.Zero;
        }
    }
}
