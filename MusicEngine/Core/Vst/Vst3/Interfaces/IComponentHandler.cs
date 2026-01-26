// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Runtime.InteropServices;
using MusicEngine.Core.Vst.Vst3.Structures;

namespace MusicEngine.Core.Vst.Vst3.Interfaces;

#region Event Arguments

/// <summary>
/// Event arguments for parameter edit begin event.
/// </summary>
public class ParameterEditBeginEventArgs : EventArgs
{
    /// <summary>
    /// Gets the parameter ID being edited.
    /// </summary>
    public uint ParameterId { get; }

    public ParameterEditBeginEventArgs(uint parameterId)
    {
        ParameterId = parameterId;
    }
}

/// <summary>
/// Event arguments for parameter edit event.
/// </summary>
public class ParameterEditEventArgs : EventArgs
{
    /// <summary>
    /// Gets the parameter ID being edited.
    /// </summary>
    public uint ParameterId { get; }

    /// <summary>
    /// Gets the new normalized value (0.0 to 1.0).
    /// </summary>
    public double NormalizedValue { get; }

    public ParameterEditEventArgs(uint parameterId, double normalizedValue)
    {
        ParameterId = parameterId;
        NormalizedValue = normalizedValue;
    }
}

/// <summary>
/// Event arguments for parameter edit end event.
/// </summary>
public class ParameterEditEndEventArgs : EventArgs
{
    /// <summary>
    /// Gets the parameter ID that finished editing.
    /// </summary>
    public uint ParameterId { get; }

    public ParameterEditEndEventArgs(uint parameterId)
    {
        ParameterId = parameterId;
    }
}

/// <summary>
/// Event arguments for component restart request.
/// </summary>
public class RestartComponentEventArgs : EventArgs
{
    /// <summary>
    /// Gets the restart flags indicating what needs to be restarted.
    /// </summary>
    public Vst3RestartFlags Flags { get; }

    public RestartComponentEventArgs(Vst3RestartFlags flags)
    {
        Flags = flags;
    }
}

/// <summary>
/// Event arguments for dirty state change.
/// </summary>
public class DirtyStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets whether the state is dirty (has unsaved changes).
    /// </summary>
    public bool IsDirty { get; }

    public DirtyStateChangedEventArgs(bool isDirty)
    {
        IsDirty = isDirty;
    }
}

/// <summary>
/// Event arguments for editor open request.
/// </summary>
public class RequestOpenEditorEventArgs : EventArgs
{
    /// <summary>
    /// Gets the name/type of editor being requested.
    /// </summary>
    public string EditorName { get; }

    public RequestOpenEditorEventArgs(string editorName)
    {
        EditorName = editorName;
    }
}

/// <summary>
/// Event arguments for context menu creation request.
/// </summary>
public class CreateContextMenuEventArgs : EventArgs
{
    /// <summary>
    /// Gets the plug view requesting the context menu.
    /// </summary>
    public IntPtr PlugView { get; }

    /// <summary>
    /// Gets the parameter ID for the context menu (or null if not parameter-specific).
    /// </summary>
    public uint? ParameterId { get; }

    /// <summary>
    /// Gets or sets the created context menu pointer (set by the event handler).
    /// </summary>
    public IntPtr ContextMenu { get; set; }

    public CreateContextMenuEventArgs(IntPtr plugView, uint? parameterId)
    {
        PlugView = plugView;
        ParameterId = parameterId;
        ContextMenu = IntPtr.Zero;
    }
}

#endregion

#region VTable Structures

/// <summary>
/// IComponentHandler vtable structure
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct IComponentHandlerVtbl
{
    // FUnknown methods
    public IntPtr QueryInterface;
    public IntPtr AddRef;
    public IntPtr Release;

    // IComponentHandler methods
    public IntPtr BeginEdit;
    public IntPtr PerformEdit;
    public IntPtr EndEdit;
    public IntPtr RestartComponent;
}

/// <summary>
/// IComponentHandler2 vtable structure (extends IComponentHandler)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct IComponentHandler2Vtbl
{
    // FUnknown methods
    public IntPtr QueryInterface;
    public IntPtr AddRef;
    public IntPtr Release;

    // IComponentHandler methods
    public IntPtr BeginEdit;
    public IntPtr PerformEdit;
    public IntPtr EndEdit;
    public IntPtr RestartComponent;

    // IComponentHandler2 methods
    public IntPtr SetDirty;
    public IntPtr RequestOpenEditor;
    public IntPtr StartGroupEdit;
    public IntPtr FinishGroupEdit;
}

/// <summary>
/// IComponentHandler3 vtable structure
/// Note: IComponentHandler3 does NOT extend IComponentHandler2, it's a separate interface
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct IComponentHandler3Vtbl
{
    // FUnknown methods
    public IntPtr QueryInterface;
    public IntPtr AddRef;
    public IntPtr Release;

    // IComponentHandler3 methods
    public IntPtr CreateContextMenu;
}

#endregion

#region Delegate Types

// IComponentHandler delegates
[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int BeginEditDelegate(IntPtr self, uint id);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int PerformEditDelegate(IntPtr self, uint id, double valueNormalized);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int EndEditDelegate(IntPtr self, uint id);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int RestartComponentDelegate(IntPtr self, int flags);

// IComponentHandler2 delegates
[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int SetDirtyDelegate(IntPtr self, [MarshalAs(UnmanagedType.U1)] bool state);

[UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
internal delegate int RequestOpenEditorDelegate(IntPtr self, [MarshalAs(UnmanagedType.LPStr)] string name);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int StartGroupEditDelegate(IntPtr self);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int FinishGroupEditDelegate(IntPtr self);

// IComponentHandler3 delegates
[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate IntPtr CreateContextMenuDelegate(IntPtr self, IntPtr plugView, ref uint paramId);

#endregion

#region Managed Implementation

/// <summary>
/// Managed implementation of IComponentHandler that can be passed to VST3 plugins.
/// This handler allows the host to receive callbacks from the plugin when parameters
/// are being edited or when the component needs to be restarted.
/// </summary>
public sealed class Vst3ComponentHandler : IDisposable
{
    // COM object layout
    private IntPtr _comObject;
    private IntPtr _vtblPtr;
    private IntPtr _vtbl2Ptr;
    private IntPtr _vtbl3Ptr;
    private IntPtr _comObject2;
    private IntPtr _comObject3;
    private GCHandle _gcHandle;
    private bool _disposed;

    // Delegate instances (must be kept alive for the lifetime of the handler)
    private readonly QueryInterfaceDelegate _queryInterface;
    private readonly AddRefDelegate _addRef;
    private readonly ReleaseDelegate _release;
    private readonly BeginEditDelegate _beginEdit;
    private readonly PerformEditDelegate _performEdit;
    private readonly EndEditDelegate _endEdit;
    private readonly RestartComponentDelegate _restartComponent;

    // IComponentHandler2 delegates
    private readonly SetDirtyDelegate _setDirty;
    private readonly RequestOpenEditorDelegate _requestOpenEditor;
    private readonly StartGroupEditDelegate _startGroupEdit;
    private readonly FinishGroupEditDelegate _finishGroupEdit;

    // IComponentHandler3 delegates
    private readonly CreateContextMenuDelegate _createContextMenu;

    // Reference count for COM semantics
    private int _refCount;

    // Group edit tracking
    private int _groupEditDepth;

    #region Events

    /// <summary>
    /// Raised when a parameter edit begins (e.g., user starts dragging a knob).
    /// </summary>
    public event EventHandler<ParameterEditBeginEventArgs>? ParameterEditBegin;

    /// <summary>
    /// Raised when a parameter value changes during editing.
    /// </summary>
    public event EventHandler<ParameterEditEventArgs>? ParameterEdited;

    /// <summary>
    /// Raised when a parameter edit ends (e.g., user releases a knob).
    /// </summary>
    public event EventHandler<ParameterEditEndEventArgs>? ParameterEditEnd;

    /// <summary>
    /// Raised when the plugin requests a component restart.
    /// </summary>
    public event EventHandler<RestartComponentEventArgs>? RestartRequested;

    /// <summary>
    /// Raised when the plugin's dirty state changes.
    /// </summary>
    public event EventHandler<DirtyStateChangedEventArgs>? DirtyStateChanged;

    /// <summary>
    /// Raised when the plugin requests the editor to be opened.
    /// </summary>
    public event EventHandler<RequestOpenEditorEventArgs>? OpenEditorRequested;

    /// <summary>
    /// Raised when a grouped edit begins.
    /// </summary>
    public event EventHandler? GroupEditBegin;

    /// <summary>
    /// Raised when a grouped edit ends.
    /// </summary>
    public event EventHandler? GroupEditEnd;

    /// <summary>
    /// Raised when the plugin requests a context menu.
    /// </summary>
    public event EventHandler<CreateContextMenuEventArgs>? ContextMenuRequested;

    #endregion

    /// <summary>
    /// Gets the native COM pointer for IComponentHandler that can be passed to plugins.
    /// </summary>
    public IntPtr NativePtr => _comObject;

    /// <summary>
    /// Gets whether the handler is currently in a group edit operation.
    /// </summary>
    public bool IsInGroupEdit => _groupEditDepth > 0;

    /// <summary>
    /// Gets the current group edit nesting depth.
    /// </summary>
    public int GroupEditDepth => _groupEditDepth;

    public Vst3ComponentHandler()
    {
        _refCount = 1;
        _groupEditDepth = 0;

        // Create delegate instances
        _queryInterface = QueryInterfaceImpl;
        _addRef = AddRefImpl;
        _release = ReleaseImpl;
        _beginEdit = BeginEditImpl;
        _performEdit = PerformEditImpl;
        _endEdit = EndEditImpl;
        _restartComponent = RestartComponentImpl;
        _setDirty = SetDirtyImpl;
        _requestOpenEditor = RequestOpenEditorImpl;
        _startGroupEdit = StartGroupEditImpl;
        _finishGroupEdit = FinishGroupEditImpl;
        _createContextMenu = CreateContextMenuImpl;

        // Pin this object so it won't move in memory
        _gcHandle = GCHandle.Alloc(this);

        // Allocate and populate IComponentHandler vtable
        _vtblPtr = Marshal.AllocHGlobal(Marshal.SizeOf<IComponentHandlerVtbl>());
        var vtbl = new IComponentHandlerVtbl
        {
            QueryInterface = Marshal.GetFunctionPointerForDelegate(_queryInterface),
            AddRef = Marshal.GetFunctionPointerForDelegate(_addRef),
            Release = Marshal.GetFunctionPointerForDelegate(_release),
            BeginEdit = Marshal.GetFunctionPointerForDelegate(_beginEdit),
            PerformEdit = Marshal.GetFunctionPointerForDelegate(_performEdit),
            EndEdit = Marshal.GetFunctionPointerForDelegate(_endEdit),
            RestartComponent = Marshal.GetFunctionPointerForDelegate(_restartComponent)
        };
        Marshal.StructureToPtr(vtbl, _vtblPtr, false);

        // Allocate COM object (just a pointer to the vtable)
        _comObject = Marshal.AllocHGlobal(IntPtr.Size);
        Marshal.WriteIntPtr(_comObject, _vtblPtr);

        // Allocate and populate IComponentHandler2 vtable
        _vtbl2Ptr = Marshal.AllocHGlobal(Marshal.SizeOf<IComponentHandler2Vtbl>());
        var vtbl2 = new IComponentHandler2Vtbl
        {
            QueryInterface = Marshal.GetFunctionPointerForDelegate(_queryInterface),
            AddRef = Marshal.GetFunctionPointerForDelegate(_addRef),
            Release = Marshal.GetFunctionPointerForDelegate(_release),
            BeginEdit = Marshal.GetFunctionPointerForDelegate(_beginEdit),
            PerformEdit = Marshal.GetFunctionPointerForDelegate(_performEdit),
            EndEdit = Marshal.GetFunctionPointerForDelegate(_endEdit),
            RestartComponent = Marshal.GetFunctionPointerForDelegate(_restartComponent),
            SetDirty = Marshal.GetFunctionPointerForDelegate(_setDirty),
            RequestOpenEditor = Marshal.GetFunctionPointerForDelegate(_requestOpenEditor),
            StartGroupEdit = Marshal.GetFunctionPointerForDelegate(_startGroupEdit),
            FinishGroupEdit = Marshal.GetFunctionPointerForDelegate(_finishGroupEdit)
        };
        Marshal.StructureToPtr(vtbl2, _vtbl2Ptr, false);

        _comObject2 = Marshal.AllocHGlobal(IntPtr.Size);
        Marshal.WriteIntPtr(_comObject2, _vtbl2Ptr);

        // Allocate and populate IComponentHandler3 vtable
        _vtbl3Ptr = Marshal.AllocHGlobal(Marshal.SizeOf<IComponentHandler3Vtbl>());
        var vtbl3 = new IComponentHandler3Vtbl
        {
            QueryInterface = Marshal.GetFunctionPointerForDelegate(_queryInterface),
            AddRef = Marshal.GetFunctionPointerForDelegate(_addRef),
            Release = Marshal.GetFunctionPointerForDelegate(_release),
            CreateContextMenu = Marshal.GetFunctionPointerForDelegate(_createContextMenu)
        };
        Marshal.StructureToPtr(vtbl3, _vtbl3Ptr, false);

        _comObject3 = Marshal.AllocHGlobal(IntPtr.Size);
        Marshal.WriteIntPtr(_comObject3, _vtbl3Ptr);
    }

    #region COM Implementation

    private int QueryInterfaceImpl(IntPtr self, ref Guid riid, out IntPtr ppvObject)
    {
        if (riid == Vst3Guids.IComponentHandler || riid == Vst3Guids.FUnknown)
        {
            ppvObject = _comObject;
            System.Threading.Interlocked.Increment(ref _refCount);
            return (int)Vst3Result.Ok;
        }

        if (riid == Vst3Guids.IComponentHandler2)
        {
            ppvObject = _comObject2;
            System.Threading.Interlocked.Increment(ref _refCount);
            return (int)Vst3Result.Ok;
        }

        if (riid == Vst3Guids.IComponentHandler3)
        {
            ppvObject = _comObject3;
            System.Threading.Interlocked.Increment(ref _refCount);
            return (int)Vst3Result.Ok;
        }

        ppvObject = IntPtr.Zero;
        return (int)Vst3Result.NotImplemented;
    }

    private uint AddRefImpl(IntPtr self)
    {
        return (uint)System.Threading.Interlocked.Increment(ref _refCount);
    }

    private uint ReleaseImpl(IntPtr self)
    {
        int newCount = System.Threading.Interlocked.Decrement(ref _refCount);
        if (newCount <= 0)
        {
            Dispose();
        }
        return (uint)Math.Max(0, newCount);
    }

    #endregion

    #region IComponentHandler Implementation

    private int BeginEditImpl(IntPtr self, uint id)
    {
        try
        {
            ParameterEditBegin?.Invoke(this, new ParameterEditBeginEventArgs(id));
            return (int)Vst3Result.Ok;
        }
        catch
        {
            return (int)Vst3Result.InternalError;
        }
    }

    private int PerformEditImpl(IntPtr self, uint id, double valueNormalized)
    {
        try
        {
            ParameterEdited?.Invoke(this, new ParameterEditEventArgs(id, valueNormalized));
            return (int)Vst3Result.Ok;
        }
        catch
        {
            return (int)Vst3Result.InternalError;
        }
    }

    private int EndEditImpl(IntPtr self, uint id)
    {
        try
        {
            ParameterEditEnd?.Invoke(this, new ParameterEditEndEventArgs(id));
            return (int)Vst3Result.Ok;
        }
        catch
        {
            return (int)Vst3Result.InternalError;
        }
    }

    private int RestartComponentImpl(IntPtr self, int flags)
    {
        try
        {
            RestartRequested?.Invoke(this, new RestartComponentEventArgs((Vst3RestartFlags)flags));
            return (int)Vst3Result.Ok;
        }
        catch
        {
            return (int)Vst3Result.InternalError;
        }
    }

    #endregion

    #region IComponentHandler2 Implementation

    private int SetDirtyImpl(IntPtr self, bool state)
    {
        try
        {
            DirtyStateChanged?.Invoke(this, new DirtyStateChangedEventArgs(state));
            return (int)Vst3Result.Ok;
        }
        catch
        {
            return (int)Vst3Result.InternalError;
        }
    }

    private int RequestOpenEditorImpl(IntPtr self, string name)
    {
        try
        {
            OpenEditorRequested?.Invoke(this, new RequestOpenEditorEventArgs(name ?? "editor"));
            return (int)Vst3Result.Ok;
        }
        catch
        {
            return (int)Vst3Result.InternalError;
        }
    }

    private int StartGroupEditImpl(IntPtr self)
    {
        try
        {
            System.Threading.Interlocked.Increment(ref _groupEditDepth);
            GroupEditBegin?.Invoke(this, EventArgs.Empty);
            return (int)Vst3Result.Ok;
        }
        catch
        {
            return (int)Vst3Result.InternalError;
        }
    }

    private int FinishGroupEditImpl(IntPtr self)
    {
        try
        {
            int newDepth = System.Threading.Interlocked.Decrement(ref _groupEditDepth);
            if (newDepth < 0)
            {
                // Reset to 0 if we went negative (unbalanced calls)
                System.Threading.Interlocked.Exchange(ref _groupEditDepth, 0);
            }
            GroupEditEnd?.Invoke(this, EventArgs.Empty);
            return (int)Vst3Result.Ok;
        }
        catch
        {
            return (int)Vst3Result.InternalError;
        }
    }

    #endregion

    #region IComponentHandler3 Implementation

    private IntPtr CreateContextMenuImpl(IntPtr self, IntPtr plugView, ref uint paramId)
    {
        try
        {
            var args = new CreateContextMenuEventArgs(plugView, paramId);
            ContextMenuRequested?.Invoke(this, args);
            return args.ContextMenu;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    #endregion

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_comObject != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_comObject);
            _comObject = IntPtr.Zero;
        }

        if (_comObject2 != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_comObject2);
            _comObject2 = IntPtr.Zero;
        }

        if (_comObject3 != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_comObject3);
            _comObject3 = IntPtr.Zero;
        }

        if (_vtblPtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_vtblPtr);
            _vtblPtr = IntPtr.Zero;
        }

        if (_vtbl2Ptr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_vtbl2Ptr);
            _vtbl2Ptr = IntPtr.Zero;
        }

        if (_vtbl3Ptr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_vtbl3Ptr);
            _vtbl3Ptr = IntPtr.Zero;
        }

        if (_gcHandle.IsAllocated)
        {
            _gcHandle.Free();
        }
    }
}

#endregion

#region Wrapper for Reading from Native Pointer

/// <summary>
/// Wrapper for reading IComponentHandler from a native pointer (when receiving from plugins).
/// This is typically not needed as the host implements the handler, but provided for completeness.
/// </summary>
internal class ComponentHandlerWrapper : IDisposable
{
    private IntPtr _handlerPtr;
    private IntPtr _vtblPtr;
    private IComponentHandlerVtbl _vtbl;

    // Cached delegates
    private QueryInterfaceDelegate? _queryInterface;
    private AddRefDelegate? _addRef;
    private ReleaseDelegate? _release;
    private BeginEditDelegate? _beginEdit;
    private PerformEditDelegate? _performEdit;
    private EndEditDelegate? _endEdit;
    private RestartComponentDelegate? _restartComponent;

    public IntPtr NativePtr => _handlerPtr;

    public ComponentHandlerWrapper(IntPtr handlerPtr)
    {
        if (handlerPtr == IntPtr.Zero)
            throw new ArgumentNullException(nameof(handlerPtr));

        _handlerPtr = handlerPtr;

        // Read vtable pointer
        _vtblPtr = Marshal.ReadIntPtr(_handlerPtr);
        _vtbl = Marshal.PtrToStructure<IComponentHandlerVtbl>(_vtblPtr);

        CacheDelegates();
    }

    private void CacheDelegates()
    {
        _queryInterface = Marshal.GetDelegateForFunctionPointer<QueryInterfaceDelegate>(_vtbl.QueryInterface);
        _addRef = Marshal.GetDelegateForFunctionPointer<AddRefDelegate>(_vtbl.AddRef);
        _release = Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(_vtbl.Release);
        _beginEdit = Marshal.GetDelegateForFunctionPointer<BeginEditDelegate>(_vtbl.BeginEdit);
        _performEdit = Marshal.GetDelegateForFunctionPointer<PerformEditDelegate>(_vtbl.PerformEdit);
        _endEdit = Marshal.GetDelegateForFunctionPointer<EndEditDelegate>(_vtbl.EndEdit);
        _restartComponent = Marshal.GetDelegateForFunctionPointer<RestartComponentDelegate>(_vtbl.RestartComponent);
    }

    public int QueryInterface(Guid iid, out IntPtr ppvObject)
    {
        if (_queryInterface == null)
        {
            ppvObject = IntPtr.Zero;
            return (int)Vst3Result.NotImplemented;
        }
        return _queryInterface(_handlerPtr, ref iid, out ppvObject);
    }

    public uint AddRef()
    {
        return _addRef?.Invoke(_handlerPtr) ?? 0;
    }

    public uint Release()
    {
        return _release?.Invoke(_handlerPtr) ?? 0;
    }

    /// <summary>
    /// Called before a parameter edit begins.
    /// </summary>
    public int BeginEdit(uint id)
    {
        if (_beginEdit == null)
            return (int)Vst3Result.NotImplemented;
        return _beginEdit(_handlerPtr, id);
    }

    /// <summary>
    /// Called during parameter editing with the new value.
    /// </summary>
    public int PerformEdit(uint id, double valueNormalized)
    {
        if (_performEdit == null)
            return (int)Vst3Result.NotImplemented;
        return _performEdit(_handlerPtr, id, valueNormalized);
    }

    /// <summary>
    /// Called after a parameter edit ends.
    /// </summary>
    public int EndEdit(uint id)
    {
        if (_endEdit == null)
            return (int)Vst3Result.NotImplemented;
        return _endEdit(_handlerPtr, id);
    }

    /// <summary>
    /// Request the host to restart the component.
    /// </summary>
    public int RestartComponent(Vst3RestartFlags flags)
    {
        if (_restartComponent == null)
            return (int)Vst3Result.NotImplemented;
        return _restartComponent(_handlerPtr, (int)flags);
    }

    public void Dispose()
    {
        if (_handlerPtr != IntPtr.Zero)
        {
            Release();
            _handlerPtr = IntPtr.Zero;
        }
    }
}

/// <summary>
/// Wrapper for reading IComponentHandler2 from a native pointer.
/// </summary>
internal class ComponentHandler2Wrapper : IDisposable
{
    private IntPtr _handlerPtr;
    private IntPtr _vtblPtr;
    private IComponentHandler2Vtbl _vtbl;

    // Cached delegates
    private QueryInterfaceDelegate? _queryInterface;
    private AddRefDelegate? _addRef;
    private ReleaseDelegate? _release;
    private BeginEditDelegate? _beginEdit;
    private PerformEditDelegate? _performEdit;
    private EndEditDelegate? _endEdit;
    private RestartComponentDelegate? _restartComponent;
    private SetDirtyDelegate? _setDirty;
    private RequestOpenEditorDelegate? _requestOpenEditor;
    private StartGroupEditDelegate? _startGroupEdit;
    private FinishGroupEditDelegate? _finishGroupEdit;

    public IntPtr NativePtr => _handlerPtr;

    public ComponentHandler2Wrapper(IntPtr handlerPtr)
    {
        if (handlerPtr == IntPtr.Zero)
            throw new ArgumentNullException(nameof(handlerPtr));

        _handlerPtr = handlerPtr;

        // Read vtable pointer
        _vtblPtr = Marshal.ReadIntPtr(_handlerPtr);
        _vtbl = Marshal.PtrToStructure<IComponentHandler2Vtbl>(_vtblPtr);

        CacheDelegates();
    }

    private void CacheDelegates()
    {
        _queryInterface = Marshal.GetDelegateForFunctionPointer<QueryInterfaceDelegate>(_vtbl.QueryInterface);
        _addRef = Marshal.GetDelegateForFunctionPointer<AddRefDelegate>(_vtbl.AddRef);
        _release = Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(_vtbl.Release);
        _beginEdit = Marshal.GetDelegateForFunctionPointer<BeginEditDelegate>(_vtbl.BeginEdit);
        _performEdit = Marshal.GetDelegateForFunctionPointer<PerformEditDelegate>(_vtbl.PerformEdit);
        _endEdit = Marshal.GetDelegateForFunctionPointer<EndEditDelegate>(_vtbl.EndEdit);
        _restartComponent = Marshal.GetDelegateForFunctionPointer<RestartComponentDelegate>(_vtbl.RestartComponent);
        _setDirty = Marshal.GetDelegateForFunctionPointer<SetDirtyDelegate>(_vtbl.SetDirty);
        _requestOpenEditor = Marshal.GetDelegateForFunctionPointer<RequestOpenEditorDelegate>(_vtbl.RequestOpenEditor);
        _startGroupEdit = Marshal.GetDelegateForFunctionPointer<StartGroupEditDelegate>(_vtbl.StartGroupEdit);
        _finishGroupEdit = Marshal.GetDelegateForFunctionPointer<FinishGroupEditDelegate>(_vtbl.FinishGroupEdit);
    }

    public int QueryInterface(Guid iid, out IntPtr ppvObject)
    {
        if (_queryInterface == null)
        {
            ppvObject = IntPtr.Zero;
            return (int)Vst3Result.NotImplemented;
        }
        return _queryInterface(_handlerPtr, ref iid, out ppvObject);
    }

    public uint AddRef() => _addRef?.Invoke(_handlerPtr) ?? 0;

    public uint Release() => _release?.Invoke(_handlerPtr) ?? 0;

    public int BeginEdit(uint id) => _beginEdit?.Invoke(_handlerPtr, id) ?? (int)Vst3Result.NotImplemented;

    public int PerformEdit(uint id, double valueNormalized) =>
        _performEdit?.Invoke(_handlerPtr, id, valueNormalized) ?? (int)Vst3Result.NotImplemented;

    public int EndEdit(uint id) => _endEdit?.Invoke(_handlerPtr, id) ?? (int)Vst3Result.NotImplemented;

    public int RestartComponent(Vst3RestartFlags flags) =>
        _restartComponent?.Invoke(_handlerPtr, (int)flags) ?? (int)Vst3Result.NotImplemented;

    /// <summary>
    /// Set the dirty state of the component.
    /// </summary>
    public int SetDirty(bool state) =>
        _setDirty?.Invoke(_handlerPtr, state) ?? (int)Vst3Result.NotImplemented;

    /// <summary>
    /// Request the host to open the editor.
    /// </summary>
    public int RequestOpenEditor(string name) =>
        _requestOpenEditor?.Invoke(_handlerPtr, name) ?? (int)Vst3Result.NotImplemented;

    /// <summary>
    /// Start a grouped edit operation.
    /// </summary>
    public int StartGroupEdit() =>
        _startGroupEdit?.Invoke(_handlerPtr) ?? (int)Vst3Result.NotImplemented;

    /// <summary>
    /// Finish a grouped edit operation.
    /// </summary>
    public int FinishGroupEdit() =>
        _finishGroupEdit?.Invoke(_handlerPtr) ?? (int)Vst3Result.NotImplemented;

    public void Dispose()
    {
        if (_handlerPtr != IntPtr.Zero)
        {
            Release();
            _handlerPtr = IntPtr.Zero;
        }
    }
}

/// <summary>
/// Wrapper for reading IComponentHandler3 from a native pointer.
/// </summary>
internal class ComponentHandler3Wrapper : IDisposable
{
    private IntPtr _handlerPtr;
    private IntPtr _vtblPtr;
    private IComponentHandler3Vtbl _vtbl;

    // Cached delegates
    private QueryInterfaceDelegate? _queryInterface;
    private AddRefDelegate? _addRef;
    private ReleaseDelegate? _release;
    private CreateContextMenuDelegate? _createContextMenu;

    public IntPtr NativePtr => _handlerPtr;

    public ComponentHandler3Wrapper(IntPtr handlerPtr)
    {
        if (handlerPtr == IntPtr.Zero)
            throw new ArgumentNullException(nameof(handlerPtr));

        _handlerPtr = handlerPtr;

        // Read vtable pointer
        _vtblPtr = Marshal.ReadIntPtr(_handlerPtr);
        _vtbl = Marshal.PtrToStructure<IComponentHandler3Vtbl>(_vtblPtr);

        CacheDelegates();
    }

    private void CacheDelegates()
    {
        _queryInterface = Marshal.GetDelegateForFunctionPointer<QueryInterfaceDelegate>(_vtbl.QueryInterface);
        _addRef = Marshal.GetDelegateForFunctionPointer<AddRefDelegate>(_vtbl.AddRef);
        _release = Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(_vtbl.Release);
        _createContextMenu = Marshal.GetDelegateForFunctionPointer<CreateContextMenuDelegate>(_vtbl.CreateContextMenu);
    }

    public int QueryInterface(Guid iid, out IntPtr ppvObject)
    {
        if (_queryInterface == null)
        {
            ppvObject = IntPtr.Zero;
            return (int)Vst3Result.NotImplemented;
        }
        return _queryInterface(_handlerPtr, ref iid, out ppvObject);
    }

    public uint AddRef() => _addRef?.Invoke(_handlerPtr) ?? 0;

    public uint Release() => _release?.Invoke(_handlerPtr) ?? 0;

    /// <summary>
    /// Create a context menu for the given parameter.
    /// </summary>
    public IntPtr CreateContextMenu(IntPtr plugView, uint paramId)
    {
        if (_createContextMenu == null)
            return IntPtr.Zero;
        return _createContextMenu(_handlerPtr, plugView, ref paramId);
    }

    public void Dispose()
    {
        if (_handlerPtr != IntPtr.Zero)
        {
            Release();
            _handlerPtr = IntPtr.Zero;
        }
    }
}

#endregion
