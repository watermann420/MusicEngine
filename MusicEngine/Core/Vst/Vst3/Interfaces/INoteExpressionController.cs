// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Runtime.InteropServices;
using MusicEngine.Core.Vst.Vst3.Structures;

namespace MusicEngine.Core.Vst.Vst3.Interfaces;

/// <summary>
/// Flags for note expression types
/// </summary>
[Flags]
public enum NoteExpressionTypeFlags
{
    /// <summary>
    /// The note expression value is bipolar (centered around 0.5)
    /// </summary>
    IsBipolar = 1,

    /// <summary>
    /// The note expression is a one-shot (triggers once per note)
    /// </summary>
    IsOneShot = 2,

    /// <summary>
    /// The note expression value is absolute (not relative)
    /// </summary>
    IsAbsolute = 4,

    /// <summary>
    /// The note expression has an associated parameter ID
    /// </summary>
    AssociatedParameterId = 8
}

/// <summary>
/// Note expression value description structure
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct NoteExpressionValueDescription
{
    /// <summary>
    /// Default normalized value [0.0, 1.0]
    /// </summary>
    public double DefaultValue;

    /// <summary>
    /// Minimum normalized value (typically 0.0)
    /// </summary>
    public double Minimum;

    /// <summary>
    /// Maximum normalized value (typically 1.0)
    /// </summary>
    public double Maximum;

    /// <summary>
    /// Number of discrete steps (0 = continuous)
    /// </summary>
    public int StepCount;
}

/// <summary>
/// Note expression type info internal structure for marshaling
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct NoteExpressionTypeInfoInternal
{
    /// <summary>
    /// Unique identifier of the note expression type
    /// </summary>
    public uint TypeId;

    /// <summary>
    /// Display title (128 Unicode characters)
    /// </summary>
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string Title;

    /// <summary>
    /// Short title for compact display (128 Unicode characters)
    /// </summary>
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string ShortTitle;

    /// <summary>
    /// Unit string (e.g., "dB", "Hz") (128 Unicode characters)
    /// </summary>
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string Units;

    /// <summary>
    /// Associated unit ID
    /// </summary>
    public int UnitId;

    /// <summary>
    /// Value description (default, min, max, step count)
    /// </summary>
    public NoteExpressionValueDescription ValueDesc;

    /// <summary>
    /// Physical UI type ID (for hardware controllers)
    /// </summary>
    public uint Id;

    /// <summary>
    /// Note expression type flags
    /// </summary>
    public int Flags;
}

/// <summary>
/// Managed note expression type info
/// </summary>
public class NoteExpressionTypeInfo
{
    /// <summary>
    /// Unique identifier of the note expression type
    /// </summary>
    public uint TypeId { get; set; }

    /// <summary>
    /// Display title
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// Short title for compact display
    /// </summary>
    public string ShortTitle { get; set; } = "";

    /// <summary>
    /// Unit string (e.g., "dB", "Hz")
    /// </summary>
    public string Units { get; set; } = "";

    /// <summary>
    /// Associated unit ID
    /// </summary>
    public int UnitId { get; set; }

    /// <summary>
    /// Value description (default, min, max, step count)
    /// </summary>
    public NoteExpressionValueDescription ValueDesc { get; set; }

    /// <summary>
    /// Physical UI type ID (for hardware controllers)
    /// </summary>
    public uint Id { get; set; }

    /// <summary>
    /// Note expression type flags
    /// </summary>
    public NoteExpressionTypeFlags Flags { get; set; }
}

/// <summary>
/// INoteExpressionController vtable structure
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct INoteExpressionControllerVtbl
{
    // FUnknown methods
    public IntPtr QueryInterface;
    public IntPtr AddRef;
    public IntPtr Release;

    // INoteExpressionController methods
    public IntPtr GetNoteExpressionCount;
    public IntPtr GetNoteExpressionInfo;
    public IntPtr GetNoteExpressionStringByValue;
    public IntPtr GetNoteExpressionValueByString;
}

// Delegate types for INoteExpressionController methods

/// <summary>
/// Get the number of note expression types for a given bus and channel
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int GetNoteExpressionCountDelegate(IntPtr self, int busIndex, short channel);

/// <summary>
/// Get note expression info by index
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int GetNoteExpressionInfoDelegate(IntPtr self, int busIndex, short channel, int noteExpressionIndex, out NoteExpressionTypeInfoInternal info);

/// <summary>
/// Get display string for a note expression value
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
internal delegate int GetNoteExpressionStringByValueDelegate(IntPtr self, int busIndex, short channel, uint id, double valueNormalized, IntPtr stringBuffer);

/// <summary>
/// Parse a string to a note expression value
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
internal delegate int GetNoteExpressionValueByStringDelegate(IntPtr self, int busIndex, short channel, uint id, IntPtr stringValue, out double valueNormalized);

/// <summary>
/// Managed wrapper for INoteExpressionController COM interface
/// </summary>
internal class NoteExpressionControllerWrapper : IDisposable
{
    private IntPtr _controllerPtr;
    private IntPtr _vtblPtr;
    private INoteExpressionControllerVtbl _vtbl;

    // Cached delegates - FUnknown
    private QueryInterfaceDelegate? _queryInterface;
    private AddRefDelegate? _addRef;
    private ReleaseDelegate? _release;

    // Cached delegates - INoteExpressionController
    private GetNoteExpressionCountDelegate? _getNoteExpressionCount;
    private GetNoteExpressionInfoDelegate? _getNoteExpressionInfo;
    private GetNoteExpressionStringByValueDelegate? _getNoteExpressionStringByValue;
    private GetNoteExpressionValueByStringDelegate? _getNoteExpressionValueByString;

    public IntPtr NativePtr => _controllerPtr;

    public NoteExpressionControllerWrapper(IntPtr controllerPtr)
    {
        if (controllerPtr == IntPtr.Zero)
            throw new ArgumentNullException(nameof(controllerPtr));

        _controllerPtr = controllerPtr;

        // Read vtable pointer
        _vtblPtr = Marshal.ReadIntPtr(_controllerPtr);
        _vtbl = Marshal.PtrToStructure<INoteExpressionControllerVtbl>(_vtblPtr);

        CacheDelegates();
    }

    private void CacheDelegates()
    {
        // FUnknown
        _queryInterface = Marshal.GetDelegateForFunctionPointer<QueryInterfaceDelegate>(_vtbl.QueryInterface);
        _addRef = Marshal.GetDelegateForFunctionPointer<AddRefDelegate>(_vtbl.AddRef);
        _release = Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(_vtbl.Release);

        // INoteExpressionController
        _getNoteExpressionCount = Marshal.GetDelegateForFunctionPointer<GetNoteExpressionCountDelegate>(_vtbl.GetNoteExpressionCount);
        _getNoteExpressionInfo = Marshal.GetDelegateForFunctionPointer<GetNoteExpressionInfoDelegate>(_vtbl.GetNoteExpressionInfo);
        _getNoteExpressionStringByValue = Marshal.GetDelegateForFunctionPointer<GetNoteExpressionStringByValueDelegate>(_vtbl.GetNoteExpressionStringByValue);
        _getNoteExpressionValueByString = Marshal.GetDelegateForFunctionPointer<GetNoteExpressionValueByStringDelegate>(_vtbl.GetNoteExpressionValueByString);
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

    #region INoteExpressionController Methods

    /// <summary>
    /// Get the number of note expression types for a given bus and channel
    /// </summary>
    /// <param name="busIndex">Event bus index</param>
    /// <param name="channel">MIDI channel (0-15)</param>
    /// <returns>Number of note expression types</returns>
    public int GetNoteExpressionCount(int busIndex, short channel)
    {
        return _getNoteExpressionCount?.Invoke(_controllerPtr, busIndex, channel) ?? 0;
    }

    /// <summary>
    /// Get note expression info by index
    /// </summary>
    /// <param name="busIndex">Event bus index</param>
    /// <param name="channel">MIDI channel (0-15)</param>
    /// <param name="noteExpressionIndex">Index of the note expression type</param>
    /// <param name="info">Output note expression type info</param>
    /// <returns>Result code</returns>
    public int GetNoteExpressionInfo(int busIndex, short channel, int noteExpressionIndex, out NoteExpressionTypeInfo info)
    {
        info = new NoteExpressionTypeInfo();

        if (_getNoteExpressionInfo == null)
            return (int)Vst3Result.NotImplemented;

        int result = _getNoteExpressionInfo(_controllerPtr, busIndex, channel, noteExpressionIndex, out NoteExpressionTypeInfoInternal internalInfo);

        if (result == (int)Vst3Result.Ok)
        {
            info.TypeId = internalInfo.TypeId;
            info.Title = internalInfo.Title ?? "";
            info.ShortTitle = internalInfo.ShortTitle ?? "";
            info.Units = internalInfo.Units ?? "";
            info.UnitId = internalInfo.UnitId;
            info.ValueDesc = internalInfo.ValueDesc;
            info.Id = internalInfo.Id;
            info.Flags = (NoteExpressionTypeFlags)internalInfo.Flags;
        }

        return result;
    }

    /// <summary>
    /// Get display string for a note expression value
    /// </summary>
    /// <param name="busIndex">Event bus index</param>
    /// <param name="channel">MIDI channel (0-15)</param>
    /// <param name="id">Note expression type ID</param>
    /// <param name="valueNormalized">Normalized value [0.0, 1.0]</param>
    /// <param name="displayString">Output display string</param>
    /// <returns>Result code</returns>
    public int GetNoteExpressionStringByValue(int busIndex, short channel, uint id, double valueNormalized, out string displayString)
    {
        displayString = "";

        if (_getNoteExpressionStringByValue == null)
            return (int)Vst3Result.NotImplemented;

        // Allocate buffer for string (128 Unicode characters)
        IntPtr buffer = Marshal.AllocHGlobal(256);
        try
        {
            int result = _getNoteExpressionStringByValue(_controllerPtr, busIndex, channel, id, valueNormalized, buffer);
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
    /// Parse a string to a note expression value
    /// </summary>
    /// <param name="busIndex">Event bus index</param>
    /// <param name="channel">MIDI channel (0-15)</param>
    /// <param name="id">Note expression type ID</param>
    /// <param name="stringValue">String to parse</param>
    /// <param name="valueNormalized">Output normalized value</param>
    /// <returns>Result code</returns>
    public int GetNoteExpressionValueByString(int busIndex, short channel, uint id, string stringValue, out double valueNormalized)
    {
        valueNormalized = 0.0;

        if (_getNoteExpressionValueByString == null)
            return (int)Vst3Result.NotImplemented;

        IntPtr stringPtr = Marshal.StringToHGlobalUni(stringValue);
        try
        {
            return _getNoteExpressionValueByString(_controllerPtr, busIndex, channel, id, stringPtr, out valueNormalized);
        }
        finally
        {
            Marshal.FreeHGlobal(stringPtr);
        }
    }

    #endregion

    public void Dispose()
    {
        if (_controllerPtr != IntPtr.Zero)
        {
            Release();
            _controllerPtr = IntPtr.Zero;
        }
    }
}
