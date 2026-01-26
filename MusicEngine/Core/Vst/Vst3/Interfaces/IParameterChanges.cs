// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MusicEngine.Core.Vst.Vst3.Structures;

namespace MusicEngine.Core.Vst.Vst3.Interfaces;

#region Vtable Structures

/// <summary>
/// IParameterChanges vtable structure.
/// Represents changes to parameters during a processing block.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct IParameterChangesVtbl
{
    // FUnknown methods
    public IntPtr QueryInterface;
    public IntPtr AddRef;
    public IntPtr Release;

    // IParameterChanges methods
    public IntPtr GetParameterCount;
    public IntPtr GetParameterData;
    public IntPtr AddParameterData;
}

/// <summary>
/// IParamValueQueue vtable structure.
/// Represents a queue of value changes for a single parameter.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct IParamValueQueueVtbl
{
    // FUnknown methods
    public IntPtr QueryInterface;
    public IntPtr AddRef;
    public IntPtr Release;

    // IParamValueQueue methods
    public IntPtr GetParameterId;
    public IntPtr GetPointCount;
    public IntPtr GetPoint;
    public IntPtr AddPoint;
}

/// <summary>
/// IEventList vtable structure.
/// Represents a list of events (MIDI notes, etc.) during a processing block.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct IEventListVtbl
{
    // FUnknown methods
    public IntPtr QueryInterface;
    public IntPtr AddRef;
    public IntPtr Release;

    // IEventList methods
    public IntPtr GetEventCount;
    public IntPtr GetEvent;
    public IntPtr AddEvent;
}

#endregion

#region Delegate Types

// IParameterChanges delegates
[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int GetParameterCountDelegate_Changes(IntPtr self);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate IntPtr GetParameterDataDelegate(IntPtr self, int index);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate IntPtr AddParameterDataDelegate(IntPtr self, uint id, out int index);

// IParamValueQueue delegates
[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate uint GetParameterIdDelegate(IntPtr self);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int GetPointCountDelegate(IntPtr self);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int GetPointDelegate(IntPtr self, int index, out int sampleOffset, out double value);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int AddPointDelegate(IntPtr self, int sampleOffset, double value, out int index);

// IEventList delegates
[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int GetEventCountDelegate(IntPtr self);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int GetEventDelegate(IntPtr self, int index, out Event evt);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int AddEventDelegate(IntPtr self, ref Event evt);

#endregion

#region Native Wrappers (for reading from native pointers)

/// <summary>
/// Managed wrapper for reading from a native IParameterChanges pointer.
/// </summary>
internal class ParameterChangesWrapper : IDisposable
{
    private IntPtr _changesPtr;
    private IntPtr _vtblPtr;
    private IParameterChangesVtbl _vtbl;

    // Cached delegates
    private QueryInterfaceDelegate? _queryInterface;
    private AddRefDelegate? _addRef;
    private ReleaseDelegate? _release;
    private GetParameterCountDelegate_Changes? _getParameterCount;
    private GetParameterDataDelegate? _getParameterData;
    private AddParameterDataDelegate? _addParameterData;

    public IntPtr NativePtr => _changesPtr;

    public ParameterChangesWrapper(IntPtr changesPtr)
    {
        if (changesPtr == IntPtr.Zero)
            throw new ArgumentNullException(nameof(changesPtr));

        _changesPtr = changesPtr;

        // Read vtable pointer
        _vtblPtr = Marshal.ReadIntPtr(_changesPtr);
        _vtbl = Marshal.PtrToStructure<IParameterChangesVtbl>(_vtblPtr);

        CacheDelegates();
    }

    private void CacheDelegates()
    {
        _queryInterface = Marshal.GetDelegateForFunctionPointer<QueryInterfaceDelegate>(_vtbl.QueryInterface);
        _addRef = Marshal.GetDelegateForFunctionPointer<AddRefDelegate>(_vtbl.AddRef);
        _release = Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(_vtbl.Release);
        _getParameterCount = Marshal.GetDelegateForFunctionPointer<GetParameterCountDelegate_Changes>(_vtbl.GetParameterCount);
        _getParameterData = Marshal.GetDelegateForFunctionPointer<GetParameterDataDelegate>(_vtbl.GetParameterData);
        _addParameterData = Marshal.GetDelegateForFunctionPointer<AddParameterDataDelegate>(_vtbl.AddParameterData);
    }

    public int QueryInterface(Guid iid, out IntPtr ppvObject)
    {
        if (_queryInterface == null)
        {
            ppvObject = IntPtr.Zero;
            return (int)Vst3Result.NotImplemented;
        }
        return _queryInterface(_changesPtr, ref iid, out ppvObject);
    }

    public uint AddRef() => _addRef?.Invoke(_changesPtr) ?? 0;

    public uint Release() => _release?.Invoke(_changesPtr) ?? 0;

    /// <summary>
    /// Get the number of parameters with changes.
    /// </summary>
    public int GetParameterCount() => _getParameterCount?.Invoke(_changesPtr) ?? 0;

    /// <summary>
    /// Get parameter value queue at the specified index.
    /// </summary>
    /// <param name="index">Index of the parameter (0 to GetParameterCount()-1)</param>
    /// <returns>Pointer to IParamValueQueue or IntPtr.Zero if invalid</returns>
    public IntPtr GetParameterData(int index) => _getParameterData?.Invoke(_changesPtr, index) ?? IntPtr.Zero;

    /// <summary>
    /// Add parameter data for a given parameter ID.
    /// </summary>
    /// <param name="id">Parameter ID</param>
    /// <param name="index">Output index of the created queue</param>
    /// <returns>Pointer to IParamValueQueue or IntPtr.Zero on failure</returns>
    public IntPtr AddParameterData(uint id, out int index)
    {
        index = -1;
        return _addParameterData?.Invoke(_changesPtr, id, out index) ?? IntPtr.Zero;
    }

    public void Dispose()
    {
        _changesPtr = IntPtr.Zero;
    }
}

/// <summary>
/// Managed wrapper for reading from a native IParamValueQueue pointer.
/// </summary>
internal class ParamValueQueueWrapper : IDisposable
{
    private IntPtr _queuePtr;
    private IntPtr _vtblPtr;
    private IParamValueQueueVtbl _vtbl;

    // Cached delegates
    private QueryInterfaceDelegate? _queryInterface;
    private AddRefDelegate? _addRef;
    private ReleaseDelegate? _release;
    private GetParameterIdDelegate? _getParameterId;
    private GetPointCountDelegate? _getPointCount;
    private GetPointDelegate? _getPoint;
    private AddPointDelegate? _addPoint;

    public IntPtr NativePtr => _queuePtr;

    public ParamValueQueueWrapper(IntPtr queuePtr)
    {
        if (queuePtr == IntPtr.Zero)
            throw new ArgumentNullException(nameof(queuePtr));

        _queuePtr = queuePtr;

        // Read vtable pointer
        _vtblPtr = Marshal.ReadIntPtr(_queuePtr);
        _vtbl = Marshal.PtrToStructure<IParamValueQueueVtbl>(_vtblPtr);

        CacheDelegates();
    }

    private void CacheDelegates()
    {
        _queryInterface = Marshal.GetDelegateForFunctionPointer<QueryInterfaceDelegate>(_vtbl.QueryInterface);
        _addRef = Marshal.GetDelegateForFunctionPointer<AddRefDelegate>(_vtbl.AddRef);
        _release = Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(_vtbl.Release);
        _getParameterId = Marshal.GetDelegateForFunctionPointer<GetParameterIdDelegate>(_vtbl.GetParameterId);
        _getPointCount = Marshal.GetDelegateForFunctionPointer<GetPointCountDelegate>(_vtbl.GetPointCount);
        _getPoint = Marshal.GetDelegateForFunctionPointer<GetPointDelegate>(_vtbl.GetPoint);
        _addPoint = Marshal.GetDelegateForFunctionPointer<AddPointDelegate>(_vtbl.AddPoint);
    }

    public int QueryInterface(Guid iid, out IntPtr ppvObject)
    {
        if (_queryInterface == null)
        {
            ppvObject = IntPtr.Zero;
            return (int)Vst3Result.NotImplemented;
        }
        return _queryInterface(_queuePtr, ref iid, out ppvObject);
    }

    public uint AddRef() => _addRef?.Invoke(_queuePtr) ?? 0;

    public uint Release() => _release?.Invoke(_queuePtr) ?? 0;

    /// <summary>
    /// Get the parameter ID for this queue.
    /// </summary>
    public uint GetParameterId() => _getParameterId?.Invoke(_queuePtr) ?? 0;

    /// <summary>
    /// Get the number of points in this queue.
    /// </summary>
    public int GetPointCount() => _getPointCount?.Invoke(_queuePtr) ?? 0;

    /// <summary>
    /// Get a point at the specified index.
    /// </summary>
    /// <param name="index">Point index (0 to GetPointCount()-1)</param>
    /// <param name="sampleOffset">Output sample offset within the block</param>
    /// <param name="value">Output normalized parameter value (0.0 to 1.0)</param>
    /// <returns>Result code</returns>
    public int GetPoint(int index, out int sampleOffset, out double value)
    {
        sampleOffset = 0;
        value = 0.0;
        if (_getPoint == null)
            return (int)Vst3Result.NotImplemented;
        return _getPoint(_queuePtr, index, out sampleOffset, out value);
    }

    /// <summary>
    /// Add a point to this queue.
    /// </summary>
    /// <param name="sampleOffset">Sample offset within the block</param>
    /// <param name="value">Normalized parameter value (0.0 to 1.0)</param>
    /// <param name="index">Output index of the added point</param>
    /// <returns>Result code</returns>
    public int AddPoint(int sampleOffset, double value, out int index)
    {
        index = -1;
        if (_addPoint == null)
            return (int)Vst3Result.NotImplemented;
        return _addPoint(_queuePtr, sampleOffset, value, out index);
    }

    public void Dispose()
    {
        _queuePtr = IntPtr.Zero;
    }
}

/// <summary>
/// Managed wrapper for reading from a native IEventList pointer.
/// </summary>
internal class EventListWrapper : IDisposable
{
    private IntPtr _listPtr;
    private IntPtr _vtblPtr;
    private IEventListVtbl _vtbl;

    // Cached delegates
    private QueryInterfaceDelegate? _queryInterface;
    private AddRefDelegate? _addRef;
    private ReleaseDelegate? _release;
    private GetEventCountDelegate? _getEventCount;
    private GetEventDelegate? _getEvent;
    private AddEventDelegate? _addEvent;

    public IntPtr NativePtr => _listPtr;

    public EventListWrapper(IntPtr listPtr)
    {
        if (listPtr == IntPtr.Zero)
            throw new ArgumentNullException(nameof(listPtr));

        _listPtr = listPtr;

        // Read vtable pointer
        _vtblPtr = Marshal.ReadIntPtr(_listPtr);
        _vtbl = Marshal.PtrToStructure<IEventListVtbl>(_vtblPtr);

        CacheDelegates();
    }

    private void CacheDelegates()
    {
        _queryInterface = Marshal.GetDelegateForFunctionPointer<QueryInterfaceDelegate>(_vtbl.QueryInterface);
        _addRef = Marshal.GetDelegateForFunctionPointer<AddRefDelegate>(_vtbl.AddRef);
        _release = Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(_vtbl.Release);
        _getEventCount = Marshal.GetDelegateForFunctionPointer<GetEventCountDelegate>(_vtbl.GetEventCount);
        _getEvent = Marshal.GetDelegateForFunctionPointer<GetEventDelegate>(_vtbl.GetEvent);
        _addEvent = Marshal.GetDelegateForFunctionPointer<AddEventDelegate>(_vtbl.AddEvent);
    }

    public int QueryInterface(Guid iid, out IntPtr ppvObject)
    {
        if (_queryInterface == null)
        {
            ppvObject = IntPtr.Zero;
            return (int)Vst3Result.NotImplemented;
        }
        return _queryInterface(_listPtr, ref iid, out ppvObject);
    }

    public uint AddRef() => _addRef?.Invoke(_listPtr) ?? 0;

    public uint Release() => _release?.Invoke(_listPtr) ?? 0;

    /// <summary>
    /// Get the number of events in the list.
    /// </summary>
    public int GetEventCount() => _getEventCount?.Invoke(_listPtr) ?? 0;

    /// <summary>
    /// Get an event at the specified index.
    /// </summary>
    /// <param name="index">Event index (0 to GetEventCount()-1)</param>
    /// <param name="evt">Output event data</param>
    /// <returns>Result code</returns>
    public int GetEvent(int index, out Event evt)
    {
        evt = default;
        if (_getEvent == null)
            return (int)Vst3Result.NotImplemented;
        return _getEvent(_listPtr, index, out evt);
    }

    /// <summary>
    /// Add an event to the list.
    /// </summary>
    /// <param name="evt">Event to add</param>
    /// <returns>Result code</returns>
    public int AddEvent(ref Event evt)
    {
        if (_addEvent == null)
            return (int)Vst3Result.NotImplemented;
        return _addEvent(_listPtr, ref evt);
    }

    public void Dispose()
    {
        _listPtr = IntPtr.Zero;
    }
}

#endregion

#region Managed COM Implementations (for passing to plugins)

/// <summary>
/// Represents a single point in a parameter value queue.
/// </summary>
public struct ParameterValuePoint
{
    /// <summary>
    /// Sample offset within the processing block.
    /// </summary>
    public int SampleOffset;

    /// <summary>
    /// Normalized parameter value (0.0 to 1.0).
    /// </summary>
    public double Value;

    public ParameterValuePoint(int sampleOffset, double value)
    {
        SampleOffset = sampleOffset;
        Value = value;
    }
}

/// <summary>
/// Managed implementation of IParamValueQueue for a single parameter's value queue.
/// Can be used as a native COM object when passed to plugins.
/// </summary>
public class Vst3ParamValueQueue : IDisposable
{
    private readonly uint _parameterId;
    private readonly List<ParameterValuePoint> _points;
    private IntPtr _nativePtr;
    private IntPtr _vtblPtr;
    private GCHandle _thisHandle;
    private bool _disposed;

    // Keep delegates alive
    private QueryInterfaceDelegate? _queryInterfaceDelegate;
    private AddRefDelegate? _addRefDelegate;
    private ReleaseDelegate? _releaseDelegate;
    private GetParameterIdDelegate? _getParameterIdDelegate;
    private GetPointCountDelegate? _getPointCountDelegate;
    private GetPointDelegate? _getPointDelegate;
    private AddPointDelegate? _addPointDelegate;

    private int _refCount = 1;

    /// <summary>
    /// Gets the parameter ID for this queue.
    /// </summary>
    public uint ParameterId => _parameterId;

    /// <summary>
    /// Gets the number of points in this queue.
    /// </summary>
    public int PointCount => _points.Count;

    /// <summary>
    /// Gets the native COM pointer for this object.
    /// </summary>
    public IntPtr NativePtr => _nativePtr;

    public Vst3ParamValueQueue(uint parameterId)
    {
        _parameterId = parameterId;
        _points = new List<ParameterValuePoint>();
        CreateNativeInterface();
    }

    private void CreateNativeInterface()
    {
        // Create delegates that call back into this managed object
        _queryInterfaceDelegate = QueryInterfaceImpl;
        _addRefDelegate = AddRefImpl;
        _releaseDelegate = ReleaseImpl;
        _getParameterIdDelegate = GetParameterIdImpl;
        _getPointCountDelegate = GetPointCountImpl;
        _getPointDelegate = GetPointImpl;
        _addPointDelegate = AddPointImpl;

        // Allocate vtable
        _vtblPtr = Marshal.AllocHGlobal(Marshal.SizeOf<IParamValueQueueVtbl>());

        var vtbl = new IParamValueQueueVtbl
        {
            QueryInterface = Marshal.GetFunctionPointerForDelegate(_queryInterfaceDelegate),
            AddRef = Marshal.GetFunctionPointerForDelegate(_addRefDelegate),
            Release = Marshal.GetFunctionPointerForDelegate(_releaseDelegate),
            GetParameterId = Marshal.GetFunctionPointerForDelegate(_getParameterIdDelegate),
            GetPointCount = Marshal.GetFunctionPointerForDelegate(_getPointCountDelegate),
            GetPoint = Marshal.GetFunctionPointerForDelegate(_getPointDelegate),
            AddPoint = Marshal.GetFunctionPointerForDelegate(_addPointDelegate)
        };

        Marshal.StructureToPtr(vtbl, _vtblPtr, false);

        // Allocate COM object (just a pointer to vtable)
        _nativePtr = Marshal.AllocHGlobal(IntPtr.Size);
        Marshal.WriteIntPtr(_nativePtr, _vtblPtr);

        // Keep this object alive
        _thisHandle = GCHandle.Alloc(this);
    }

    // COM interface implementations
    private int QueryInterfaceImpl(IntPtr self, ref Guid riid, out IntPtr ppvObject)
    {
        if (riid == Vst3Guids.FUnknown || riid == Vst3Guids.IParamValueQueue)
        {
            ppvObject = _nativePtr;
            _refCount++;
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
        if (newCount == 0)
        {
            Dispose();
        }
        return (uint)newCount;
    }

    private uint GetParameterIdImpl(IntPtr self) => _parameterId;

    private int GetPointCountImpl(IntPtr self) => _points.Count;

    private int GetPointImpl(IntPtr self, int index, out int sampleOffset, out double value)
    {
        if (index < 0 || index >= _points.Count)
        {
            sampleOffset = 0;
            value = 0.0;
            return (int)Vst3Result.InvalidArgument;
        }

        var point = _points[index];
        sampleOffset = point.SampleOffset;
        value = point.Value;
        return (int)Vst3Result.Ok;
    }

    private int AddPointImpl(IntPtr self, int sampleOffset, double value, out int index)
    {
        // Insert in sorted order by sample offset
        int insertIndex = _points.Count;
        for (int i = 0; i < _points.Count; i++)
        {
            if (_points[i].SampleOffset > sampleOffset)
            {
                insertIndex = i;
                break;
            }
        }

        _points.Insert(insertIndex, new ParameterValuePoint(sampleOffset, value));
        index = insertIndex;
        return (int)Vst3Result.Ok;
    }

    /// <summary>
    /// Add a point to this queue.
    /// </summary>
    /// <param name="sampleOffset">Sample offset within the block</param>
    /// <param name="value">Normalized parameter value (0.0 to 1.0)</param>
    /// <returns>Index of the added point</returns>
    public int AddPoint(int sampleOffset, double value)
    {
        AddPointImpl(IntPtr.Zero, sampleOffset, value, out int index);
        return index;
    }

    /// <summary>
    /// Get a point at the specified index.
    /// </summary>
    /// <param name="index">Point index</param>
    /// <returns>The point data, or default if invalid index</returns>
    public ParameterValuePoint GetPoint(int index)
    {
        if (index < 0 || index >= _points.Count)
            return default;
        return _points[index];
    }

    /// <summary>
    /// Clear all points from this queue.
    /// </summary>
    public void Clear()
    {
        _points.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_thisHandle.IsAllocated)
            _thisHandle.Free();

        if (_nativePtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_nativePtr);
            _nativePtr = IntPtr.Zero;
        }

        if (_vtblPtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_vtblPtr);
            _vtblPtr = IntPtr.Zero;
        }
    }
}

/// <summary>
/// Managed implementation of IParameterChanges for sending parameter changes to plugins.
/// Can be used as a native COM object when passed to plugins.
/// </summary>
public class Vst3ParameterChanges : IDisposable
{
    private readonly List<Vst3ParamValueQueue> _queues;
    private readonly Dictionary<uint, int> _parameterIndexMap;
    private IntPtr _nativePtr;
    private IntPtr _vtblPtr;
    private GCHandle _thisHandle;
    private bool _disposed;

    // Keep delegates alive
    private QueryInterfaceDelegate? _queryInterfaceDelegate;
    private AddRefDelegate? _addRefDelegate;
    private ReleaseDelegate? _releaseDelegate;
    private GetParameterCountDelegate_Changes? _getParameterCountDelegate;
    private GetParameterDataDelegate? _getParameterDataDelegate;
    private AddParameterDataDelegate? _addParameterDataDelegate;

    private int _refCount = 1;

    /// <summary>
    /// Gets the number of parameters with changes.
    /// </summary>
    public int ParameterCount => _queues.Count;

    /// <summary>
    /// Gets the native COM pointer for this object.
    /// </summary>
    public IntPtr NativePtr => _nativePtr;

    public Vst3ParameterChanges()
    {
        _queues = new List<Vst3ParamValueQueue>();
        _parameterIndexMap = new Dictionary<uint, int>();
        CreateNativeInterface();
    }

    private void CreateNativeInterface()
    {
        // Create delegates that call back into this managed object
        _queryInterfaceDelegate = QueryInterfaceImpl;
        _addRefDelegate = AddRefImpl;
        _releaseDelegate = ReleaseImpl;
        _getParameterCountDelegate = GetParameterCountImpl;
        _getParameterDataDelegate = GetParameterDataImpl;
        _addParameterDataDelegate = AddParameterDataImpl;

        // Allocate vtable
        _vtblPtr = Marshal.AllocHGlobal(Marshal.SizeOf<IParameterChangesVtbl>());

        var vtbl = new IParameterChangesVtbl
        {
            QueryInterface = Marshal.GetFunctionPointerForDelegate(_queryInterfaceDelegate),
            AddRef = Marshal.GetFunctionPointerForDelegate(_addRefDelegate),
            Release = Marshal.GetFunctionPointerForDelegate(_releaseDelegate),
            GetParameterCount = Marshal.GetFunctionPointerForDelegate(_getParameterCountDelegate),
            GetParameterData = Marshal.GetFunctionPointerForDelegate(_getParameterDataDelegate),
            AddParameterData = Marshal.GetFunctionPointerForDelegate(_addParameterDataDelegate)
        };

        Marshal.StructureToPtr(vtbl, _vtblPtr, false);

        // Allocate COM object (just a pointer to vtable)
        _nativePtr = Marshal.AllocHGlobal(IntPtr.Size);
        Marshal.WriteIntPtr(_nativePtr, _vtblPtr);

        // Keep this object alive
        _thisHandle = GCHandle.Alloc(this);
    }

    // COM interface implementations
    private int QueryInterfaceImpl(IntPtr self, ref Guid riid, out IntPtr ppvObject)
    {
        if (riid == Vst3Guids.FUnknown || riid == Vst3Guids.IParameterChanges)
        {
            ppvObject = _nativePtr;
            _refCount++;
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
        if (newCount == 0)
        {
            Dispose();
        }
        return (uint)newCount;
    }

    private int GetParameterCountImpl(IntPtr self) => _queues.Count;

    private IntPtr GetParameterDataImpl(IntPtr self, int index)
    {
        if (index < 0 || index >= _queues.Count)
            return IntPtr.Zero;
        return _queues[index].NativePtr;
    }

    private IntPtr AddParameterDataImpl(IntPtr self, uint id, out int index)
    {
        // Check if queue already exists for this parameter
        if (_parameterIndexMap.TryGetValue(id, out index))
        {
            return _queues[index].NativePtr;
        }

        // Create new queue
        var queue = new Vst3ParamValueQueue(id);
        index = _queues.Count;
        _queues.Add(queue);
        _parameterIndexMap[id] = index;
        return queue.NativePtr;
    }

    /// <summary>
    /// Add or get the parameter value queue for a given parameter ID.
    /// </summary>
    /// <param name="parameterId">Parameter ID</param>
    /// <returns>The parameter value queue</returns>
    public Vst3ParamValueQueue AddParameterData(uint parameterId)
    {
        if (_parameterIndexMap.TryGetValue(parameterId, out int index))
        {
            return _queues[index];
        }

        var queue = new Vst3ParamValueQueue(parameterId);
        _parameterIndexMap[parameterId] = _queues.Count;
        _queues.Add(queue);
        return queue;
    }

    /// <summary>
    /// Get the parameter value queue at the specified index.
    /// </summary>
    /// <param name="index">Queue index</param>
    /// <returns>The queue, or null if invalid index</returns>
    public Vst3ParamValueQueue? GetParameterData(int index)
    {
        if (index < 0 || index >= _queues.Count)
            return null;
        return _queues[index];
    }

    /// <summary>
    /// Add a parameter value change.
    /// Convenience method that creates the queue if needed and adds the point.
    /// </summary>
    /// <param name="parameterId">Parameter ID</param>
    /// <param name="sampleOffset">Sample offset within the block</param>
    /// <param name="value">Normalized parameter value (0.0 to 1.0)</param>
    public void AddParameterChange(uint parameterId, int sampleOffset, double value)
    {
        var queue = AddParameterData(parameterId);
        queue.AddPoint(sampleOffset, value);
    }

    /// <summary>
    /// Clear all parameter changes.
    /// </summary>
    public void Clear()
    {
        foreach (var queue in _queues)
        {
            queue.Dispose();
        }
        _queues.Clear();
        _parameterIndexMap.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        foreach (var queue in _queues)
        {
            queue.Dispose();
        }
        _queues.Clear();
        _parameterIndexMap.Clear();

        if (_thisHandle.IsAllocated)
            _thisHandle.Free();

        if (_nativePtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_nativePtr);
            _nativePtr = IntPtr.Zero;
        }

        if (_vtblPtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_vtblPtr);
            _vtblPtr = IntPtr.Zero;
        }
    }
}

/// <summary>
/// Managed implementation of IEventList for sending events (MIDI notes, etc.) to plugins.
/// Can be used as a native COM object when passed to plugins.
/// </summary>
public class Vst3EventList : IDisposable
{
    private readonly List<Event> _events;
    private IntPtr _nativePtr;
    private IntPtr _vtblPtr;
    private GCHandle _thisHandle;
    private bool _disposed;

    // Keep delegates alive
    private QueryInterfaceDelegate? _queryInterfaceDelegate;
    private AddRefDelegate? _addRefDelegate;
    private ReleaseDelegate? _releaseDelegate;
    private GetEventCountDelegate? _getEventCountDelegate;
    private GetEventDelegate? _getEventDelegate;
    private AddEventDelegate? _addEventDelegate;

    private int _refCount = 1;

    /// <summary>
    /// Gets the number of events in the list.
    /// </summary>
    public int EventCount => _events.Count;

    /// <summary>
    /// Gets the native COM pointer for this object.
    /// </summary>
    public IntPtr NativePtr => _nativePtr;

    public Vst3EventList()
    {
        _events = new List<Event>();
        CreateNativeInterface();
    }

    private void CreateNativeInterface()
    {
        // Create delegates that call back into this managed object
        _queryInterfaceDelegate = QueryInterfaceImpl;
        _addRefDelegate = AddRefImpl;
        _releaseDelegate = ReleaseImpl;
        _getEventCountDelegate = GetEventCountImpl;
        _getEventDelegate = GetEventImpl;
        _addEventDelegate = AddEventImpl;

        // Allocate vtable
        _vtblPtr = Marshal.AllocHGlobal(Marshal.SizeOf<IEventListVtbl>());

        var vtbl = new IEventListVtbl
        {
            QueryInterface = Marshal.GetFunctionPointerForDelegate(_queryInterfaceDelegate),
            AddRef = Marshal.GetFunctionPointerForDelegate(_addRefDelegate),
            Release = Marshal.GetFunctionPointerForDelegate(_releaseDelegate),
            GetEventCount = Marshal.GetFunctionPointerForDelegate(_getEventCountDelegate),
            GetEvent = Marshal.GetFunctionPointerForDelegate(_getEventDelegate),
            AddEvent = Marshal.GetFunctionPointerForDelegate(_addEventDelegate)
        };

        Marshal.StructureToPtr(vtbl, _vtblPtr, false);

        // Allocate COM object (just a pointer to vtable)
        _nativePtr = Marshal.AllocHGlobal(IntPtr.Size);
        Marshal.WriteIntPtr(_nativePtr, _vtblPtr);

        // Keep this object alive
        _thisHandle = GCHandle.Alloc(this);
    }

    // COM interface implementations
    private int QueryInterfaceImpl(IntPtr self, ref Guid riid, out IntPtr ppvObject)
    {
        if (riid == Vst3Guids.FUnknown || riid == Vst3Guids.IEventList)
        {
            ppvObject = _nativePtr;
            _refCount++;
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
        if (newCount == 0)
        {
            Dispose();
        }
        return (uint)newCount;
    }

    private int GetEventCountImpl(IntPtr self) => _events.Count;

    private int GetEventImpl(IntPtr self, int index, out Event evt)
    {
        if (index < 0 || index >= _events.Count)
        {
            evt = default;
            return (int)Vst3Result.InvalidArgument;
        }

        evt = _events[index];
        return (int)Vst3Result.Ok;
    }

    private int AddEventImpl(IntPtr self, ref Event evt)
    {
        // Insert in sorted order by sample offset
        int insertIndex = _events.Count;
        for (int i = 0; i < _events.Count; i++)
        {
            if (_events[i].SampleOffset > evt.SampleOffset)
            {
                insertIndex = i;
                break;
            }
        }

        _events.Insert(insertIndex, evt);
        return (int)Vst3Result.Ok;
    }

    /// <summary>
    /// Add an event to the list.
    /// </summary>
    /// <param name="evt">Event to add</param>
    public void AddEvent(Event evt)
    {
        AddEventImpl(IntPtr.Zero, ref evt);
    }

    /// <summary>
    /// Add a note-on event.
    /// </summary>
    /// <param name="channel">MIDI channel (0-15)</param>
    /// <param name="pitch">MIDI note number (0-127)</param>
    /// <param name="velocity">Velocity (0.0 to 1.0)</param>
    /// <param name="sampleOffset">Sample offset within the block</param>
    /// <param name="noteId">Note identifier (-1 for unspecified)</param>
    /// <param name="tuning">Tuning offset in cents</param>
    /// <param name="length">Note length in samples (0 for unknown)</param>
    public void AddNoteOn(short channel, short pitch, float velocity, int sampleOffset,
        int noteId = -1, float tuning = 0.0f, int length = 0)
    {
        var evt = new Event
        {
            BusIndex = 0,
            SampleOffset = sampleOffset,
            PpqPosition = 0.0,
            Flags = (ushort)Vst3EventFlags.None,
            Type = (ushort)Vst3EventType.NoteOn
        };

        evt.Data.NoteOn = new NoteOnEvent
        {
            Channel = channel,
            Pitch = pitch,
            Velocity = velocity,
            NoteId = noteId,
            Tuning = tuning,
            Length = length
        };

        AddEvent(evt);
    }

    /// <summary>
    /// Add a note-off event.
    /// </summary>
    /// <param name="channel">MIDI channel (0-15)</param>
    /// <param name="pitch">MIDI note number (0-127)</param>
    /// <param name="velocity">Release velocity (0.0 to 1.0)</param>
    /// <param name="sampleOffset">Sample offset within the block</param>
    /// <param name="noteId">Note identifier (-1 for unspecified)</param>
    /// <param name="tuning">Tuning offset in cents</param>
    public void AddNoteOff(short channel, short pitch, float velocity, int sampleOffset,
        int noteId = -1, float tuning = 0.0f)
    {
        var evt = new Event
        {
            BusIndex = 0,
            SampleOffset = sampleOffset,
            PpqPosition = 0.0,
            Flags = (ushort)Vst3EventFlags.None,
            Type = (ushort)Vst3EventType.NoteOff
        };

        evt.Data.NoteOff = new NoteOffEvent
        {
            Channel = channel,
            Pitch = pitch,
            Velocity = velocity,
            NoteId = noteId,
            Tuning = tuning
        };

        AddEvent(evt);
    }

    /// <summary>
    /// Add a polyphonic aftertouch event.
    /// </summary>
    /// <param name="channel">MIDI channel (0-15)</param>
    /// <param name="pitch">MIDI note number (0-127)</param>
    /// <param name="pressure">Pressure value (0.0 to 1.0)</param>
    /// <param name="sampleOffset">Sample offset within the block</param>
    /// <param name="noteId">Note identifier (-1 for unspecified)</param>
    public void AddPolyPressure(short channel, short pitch, float pressure, int sampleOffset,
        int noteId = -1)
    {
        var evt = new Event
        {
            BusIndex = 0,
            SampleOffset = sampleOffset,
            PpqPosition = 0.0,
            Flags = (ushort)Vst3EventFlags.None,
            Type = (ushort)Vst3EventType.PolyPressure
        };

        evt.Data.PolyPressure = new PolyPressureEvent
        {
            Channel = channel,
            Pitch = pitch,
            Pressure = pressure,
            NoteId = noteId
        };

        AddEvent(evt);
    }

    /// <summary>
    /// Get an event at the specified index.
    /// </summary>
    /// <param name="index">Event index</param>
    /// <returns>The event, or default if invalid index</returns>
    public Event GetEvent(int index)
    {
        if (index < 0 || index >= _events.Count)
            return default;
        return _events[index];
    }

    /// <summary>
    /// Clear all events from the list.
    /// </summary>
    public void Clear()
    {
        _events.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _events.Clear();

        if (_thisHandle.IsAllocated)
            _thisHandle.Free();

        if (_nativePtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_nativePtr);
            _nativePtr = IntPtr.Zero;
        }

        if (_vtblPtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_vtblPtr);
            _vtblPtr = IntPtr.Zero;
        }
    }
}

#endregion
