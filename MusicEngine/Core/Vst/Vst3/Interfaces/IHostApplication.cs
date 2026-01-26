// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Runtime.InteropServices;
using MusicEngine.Core.Vst.Vst3.Structures;

namespace MusicEngine.Core.Vst.Vst3.Interfaces;

#region VTable Structures

/// <summary>
/// IHostApplication vtable structure
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct IHostApplicationVtbl
{
    // FUnknown methods
    public IntPtr QueryInterface;
    public IntPtr AddRef;
    public IntPtr Release;

    // IHostApplication methods
    public IntPtr GetName;
    public IntPtr CreateInstance;
}

/// <summary>
/// IAttributeList vtable structure (simplified)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct IAttributeListVtbl
{
    // FUnknown methods
    public IntPtr QueryInterface;
    public IntPtr AddRef;
    public IntPtr Release;

    // IAttributeList methods
    public IntPtr SetInt;
    public IntPtr GetInt;
    public IntPtr SetFloat;
    public IntPtr GetFloat;
    public IntPtr SetString;
    public IntPtr GetString;
    public IntPtr SetBinary;
    public IntPtr GetBinary;
}

/// <summary>
/// IMessage vtable structure (simplified)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct IMessageVtbl
{
    // FUnknown methods
    public IntPtr QueryInterface;
    public IntPtr AddRef;
    public IntPtr Release;

    // IMessage methods
    public IntPtr GetMessageID;
    public IntPtr SetMessageID;
    public IntPtr GetAttributes;
}

#endregion

#region Delegate Types

// IHostApplication delegates
[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int HostGetNameDelegate(IntPtr self, IntPtr name);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int HostCreateInstanceDelegate(IntPtr self, ref Vst3Tuid cid, ref Guid iid, out IntPtr obj);

// IAttributeList delegates
[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int AttributeSetIntDelegate(IntPtr self, IntPtr id, long value);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int AttributeGetIntDelegate(IntPtr self, IntPtr id, out long value);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int AttributeSetFloatDelegate(IntPtr self, IntPtr id, double value);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int AttributeGetFloatDelegate(IntPtr self, IntPtr id, out double value);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int AttributeSetStringDelegate(IntPtr self, IntPtr id, IntPtr value);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int AttributeGetStringDelegate(IntPtr self, IntPtr id, IntPtr value, uint sizeInBytes);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int AttributeSetBinaryDelegate(IntPtr self, IntPtr id, IntPtr data, uint sizeInBytes);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int AttributeGetBinaryDelegate(IntPtr self, IntPtr id, out IntPtr data, out uint sizeInBytes);

// IMessage delegates
[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate IntPtr MessageGetIdDelegate(IntPtr self);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate void MessageSetIdDelegate(IntPtr self, IntPtr id);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate IntPtr MessageGetAttributesDelegate(IntPtr self);

#endregion

#region Managed Implementation

/// <summary>
/// Managed implementation of IHostApplication that can be passed to VST3 plugins.
/// This is the host context that gets passed to plugins during initialization.
/// </summary>
internal sealed class Vst3HostApplication : IDisposable
{
    private const string HostName = "MusicEngine";

    // COM object layout: pointer to vtable followed by any instance data
    private IntPtr _comObject;
    private IntPtr _vtblPtr;
    private GCHandle _gcHandle;
    private bool _disposed;

    // Delegate instances (must be kept alive)
    private readonly QueryInterfaceDelegate _queryInterface;
    private readonly AddRefDelegate _addRef;
    private readonly ReleaseDelegate _release;
    private readonly HostGetNameDelegate _getName;
    private readonly HostCreateInstanceDelegate _createInstance;

    // Reference count for COM semantics
    private int _refCount;

    /// <summary>
    /// Gets the native COM pointer that can be passed to plugins.
    /// </summary>
    public IntPtr NativePtr => _comObject;

    public Vst3HostApplication()
    {
        _refCount = 1;

        // Create delegate instances
        _queryInterface = QueryInterfaceImpl;
        _addRef = AddRefImpl;
        _release = ReleaseImpl;
        _getName = GetNameImpl;
        _createInstance = CreateInstanceImpl;

        // Pin this object so it won't move in memory
        _gcHandle = GCHandle.Alloc(this);

        // Allocate vtable
        _vtblPtr = Marshal.AllocHGlobal(Marshal.SizeOf<IHostApplicationVtbl>());

        // Populate vtable
        var vtbl = new IHostApplicationVtbl
        {
            QueryInterface = Marshal.GetFunctionPointerForDelegate(_queryInterface),
            AddRef = Marshal.GetFunctionPointerForDelegate(_addRef),
            Release = Marshal.GetFunctionPointerForDelegate(_release),
            GetName = Marshal.GetFunctionPointerForDelegate(_getName),
            CreateInstance = Marshal.GetFunctionPointerForDelegate(_createInstance)
        };
        Marshal.StructureToPtr(vtbl, _vtblPtr, false);

        // Allocate COM object (just a pointer to the vtable)
        _comObject = Marshal.AllocHGlobal(IntPtr.Size);
        Marshal.WriteIntPtr(_comObject, _vtblPtr);
    }

    /// <summary>
    /// Gets the managed instance from a COM object pointer.
    /// </summary>
    private static Vst3HostApplication? GetInstance(IntPtr self)
    {
        // In our implementation, we use the GCHandle stored in the class
        // We need a way to get back to our managed instance from the COM pointer
        // This is a simplified approach - in production you might store additional data
        return null; // We'll use the delegate closure instead
    }

    private int QueryInterfaceImpl(IntPtr self, ref Guid riid, out IntPtr ppvObject)
    {
        // Support IHostApplication
        if (riid == Vst3Guids.IHostApplication || riid == Vst3Guids.FUnknown)
        {
            ppvObject = _comObject;
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
        if (newCount <= 0)
        {
            Dispose();
        }
        return (uint)Math.Max(0, newCount);
    }

    private int GetNameImpl(IntPtr self, IntPtr name)
    {
        if (name == IntPtr.Zero)
            return (int)Vst3Result.InvalidArgument;

        try
        {
            // VST3 expects a 128-character UTF-16 string buffer (String128)
            // Copy the host name as UTF-16
            var chars = HostName.ToCharArray();
            int maxChars = Math.Min(chars.Length, 127); // Leave room for null terminator

            for (int i = 0; i < maxChars; i++)
            {
                Marshal.WriteInt16(name, i * 2, chars[i]);
            }
            // Null terminate
            Marshal.WriteInt16(name, maxChars * 2, 0);

            return (int)Vst3Result.Ok;
        }
        catch
        {
            return (int)Vst3Result.InternalError;
        }
    }

    private int CreateInstanceImpl(IntPtr self, ref Vst3Tuid cid, ref Guid iid, out IntPtr obj)
    {
        // This host does not support creating instances
        // Most hosts don't need to implement this
        obj = IntPtr.Zero;
        return (int)Vst3Result.NotImplemented;
    }

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

        if (_vtblPtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_vtblPtr);
            _vtblPtr = IntPtr.Zero;
        }

        if (_gcHandle.IsAllocated)
        {
            _gcHandle.Free();
        }
    }
}

/// <summary>
/// Wrapper for reading IHostApplication from a native pointer (when receiving from plugins)
/// </summary>
internal class HostApplicationWrapper : IDisposable
{
    private IntPtr _hostPtr;
    private IntPtr _vtblPtr;
    private IHostApplicationVtbl _vtbl;

    // Cached delegates
    private QueryInterfaceDelegate? _queryInterface;
    private AddRefDelegate? _addRef;
    private ReleaseDelegate? _release;
    private HostGetNameDelegate? _getName;
    private HostCreateInstanceDelegate? _createInstance;

    public IntPtr NativePtr => _hostPtr;

    public HostApplicationWrapper(IntPtr hostPtr)
    {
        if (hostPtr == IntPtr.Zero)
            throw new ArgumentNullException(nameof(hostPtr));

        _hostPtr = hostPtr;

        // Read vtable pointer
        _vtblPtr = Marshal.ReadIntPtr(_hostPtr);
        _vtbl = Marshal.PtrToStructure<IHostApplicationVtbl>(_vtblPtr);

        CacheDelegates();
    }

    private void CacheDelegates()
    {
        _queryInterface = Marshal.GetDelegateForFunctionPointer<QueryInterfaceDelegate>(_vtbl.QueryInterface);
        _addRef = Marshal.GetDelegateForFunctionPointer<AddRefDelegate>(_vtbl.AddRef);
        _release = Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(_vtbl.Release);
        _getName = Marshal.GetDelegateForFunctionPointer<HostGetNameDelegate>(_vtbl.GetName);
        _createInstance = Marshal.GetDelegateForFunctionPointer<HostCreateInstanceDelegate>(_vtbl.CreateInstance);
    }

    public int QueryInterface(Guid iid, out IntPtr ppvObject)
    {
        if (_queryInterface == null)
        {
            ppvObject = IntPtr.Zero;
            return (int)Vst3Result.NotImplemented;
        }
        return _queryInterface(_hostPtr, ref iid, out ppvObject);
    }

    public uint AddRef()
    {
        return _addRef?.Invoke(_hostPtr) ?? 0;
    }

    public uint Release()
    {
        return _release?.Invoke(_hostPtr) ?? 0;
    }

    public string? GetName()
    {
        if (_getName == null)
            return null;

        // Allocate buffer for String128 (128 UTF-16 characters)
        IntPtr nameBuffer = Marshal.AllocHGlobal(256);
        try
        {
            int result = _getName(_hostPtr, nameBuffer);
            if (result == (int)Vst3Result.Ok)
            {
                return Marshal.PtrToStringUni(nameBuffer);
            }
            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(nameBuffer);
        }
    }

    public int CreateInstance(Vst3Tuid cid, Guid iid, out IntPtr obj)
    {
        if (_createInstance == null)
        {
            obj = IntPtr.Zero;
            return (int)Vst3Result.NotImplemented;
        }
        return _createInstance(_hostPtr, ref cid, ref iid, out obj);
    }

    public void Dispose()
    {
        if (_hostPtr != IntPtr.Zero)
        {
            Release();
            _hostPtr = IntPtr.Zero;
        }
    }
}

#endregion

#region Simplified IAttributeList Implementation

/// <summary>
/// Simplified IAttributeList implementation for passing attributes to plugins.
/// </summary>
internal sealed class Vst3AttributeList : IDisposable
{
    private IntPtr _comObject;
    private IntPtr _vtblPtr;
    private GCHandle _gcHandle;
    private bool _disposed;
    private int _refCount;

    // Storage for attributes
    private readonly System.Collections.Generic.Dictionary<string, object> _attributes = new();

    // Delegate instances
    private readonly QueryInterfaceDelegate _queryInterface;
    private readonly AddRefDelegate _addRef;
    private readonly ReleaseDelegate _release;
    private readonly AttributeSetIntDelegate _setInt;
    private readonly AttributeGetIntDelegate _getInt;
    private readonly AttributeSetFloatDelegate _setFloat;
    private readonly AttributeGetFloatDelegate _getFloat;
    private readonly AttributeSetStringDelegate _setString;
    private readonly AttributeGetStringDelegate _getString;
    private readonly AttributeSetBinaryDelegate _setBinary;
    private readonly AttributeGetBinaryDelegate _getBinary;

    public IntPtr NativePtr => _comObject;

    public Vst3AttributeList()
    {
        _refCount = 1;

        // Create delegate instances
        _queryInterface = QueryInterfaceImpl;
        _addRef = AddRefImpl;
        _release = ReleaseImpl;
        _setInt = SetIntImpl;
        _getInt = GetIntImpl;
        _setFloat = SetFloatImpl;
        _getFloat = GetFloatImpl;
        _setString = SetStringImpl;
        _getString = GetStringImpl;
        _setBinary = SetBinaryImpl;
        _getBinary = GetBinaryImpl;

        _gcHandle = GCHandle.Alloc(this);

        // Allocate vtable
        _vtblPtr = Marshal.AllocHGlobal(Marshal.SizeOf<IAttributeListVtbl>());

        var vtbl = new IAttributeListVtbl
        {
            QueryInterface = Marshal.GetFunctionPointerForDelegate(_queryInterface),
            AddRef = Marshal.GetFunctionPointerForDelegate(_addRef),
            Release = Marshal.GetFunctionPointerForDelegate(_release),
            SetInt = Marshal.GetFunctionPointerForDelegate(_setInt),
            GetInt = Marshal.GetFunctionPointerForDelegate(_getInt),
            SetFloat = Marshal.GetFunctionPointerForDelegate(_setFloat),
            GetFloat = Marshal.GetFunctionPointerForDelegate(_getFloat),
            SetString = Marshal.GetFunctionPointerForDelegate(_setString),
            GetString = Marshal.GetFunctionPointerForDelegate(_getString),
            SetBinary = Marshal.GetFunctionPointerForDelegate(_setBinary),
            GetBinary = Marshal.GetFunctionPointerForDelegate(_getBinary)
        };
        Marshal.StructureToPtr(vtbl, _vtblPtr, false);

        _comObject = Marshal.AllocHGlobal(IntPtr.Size);
        Marshal.WriteIntPtr(_comObject, _vtblPtr);
    }

    private int QueryInterfaceImpl(IntPtr self, ref Guid riid, out IntPtr ppvObject)
    {
        if (riid == Vst3Guids.IAttributeList || riid == Vst3Guids.FUnknown)
        {
            ppvObject = _comObject;
            _refCount++;
            return (int)Vst3Result.Ok;
        }
        ppvObject = IntPtr.Zero;
        return (int)Vst3Result.NotImplemented;
    }

    private uint AddRefImpl(IntPtr self) => (uint)System.Threading.Interlocked.Increment(ref _refCount);

    private uint ReleaseImpl(IntPtr self)
    {
        int newCount = System.Threading.Interlocked.Decrement(ref _refCount);
        if (newCount <= 0) Dispose();
        return (uint)Math.Max(0, newCount);
    }

    private int SetIntImpl(IntPtr self, IntPtr id, long value)
    {
        var key = Marshal.PtrToStringAnsi(id);
        if (key == null) return (int)Vst3Result.InvalidArgument;
        _attributes[key] = value;
        return (int)Vst3Result.Ok;
    }

    private int GetIntImpl(IntPtr self, IntPtr id, out long value)
    {
        var key = Marshal.PtrToStringAnsi(id);
        if (key != null && _attributes.TryGetValue(key, out var obj) && obj is long longValue)
        {
            value = longValue;
            return (int)Vst3Result.Ok;
        }
        value = 0;
        return (int)Vst3Result.False;
    }

    private int SetFloatImpl(IntPtr self, IntPtr id, double value)
    {
        var key = Marshal.PtrToStringAnsi(id);
        if (key == null) return (int)Vst3Result.InvalidArgument;
        _attributes[key] = value;
        return (int)Vst3Result.Ok;
    }

    private int GetFloatImpl(IntPtr self, IntPtr id, out double value)
    {
        var key = Marshal.PtrToStringAnsi(id);
        if (key != null && _attributes.TryGetValue(key, out var obj) && obj is double doubleValue)
        {
            value = doubleValue;
            return (int)Vst3Result.Ok;
        }
        value = 0;
        return (int)Vst3Result.False;
    }

    private int SetStringImpl(IntPtr self, IntPtr id, IntPtr value)
    {
        var key = Marshal.PtrToStringAnsi(id);
        if (key == null) return (int)Vst3Result.InvalidArgument;
        var str = Marshal.PtrToStringUni(value);
        _attributes[key] = str ?? string.Empty;
        return (int)Vst3Result.Ok;
    }

    private int GetStringImpl(IntPtr self, IntPtr id, IntPtr value, uint sizeInBytes)
    {
        var key = Marshal.PtrToStringAnsi(id);
        if (key != null && _attributes.TryGetValue(key, out var obj) && obj is string str)
        {
            var chars = str.ToCharArray();
            int maxChars = Math.Min(chars.Length, (int)(sizeInBytes / 2) - 1);
            for (int i = 0; i < maxChars; i++)
            {
                Marshal.WriteInt16(value, i * 2, chars[i]);
            }
            Marshal.WriteInt16(value, maxChars * 2, 0);
            return (int)Vst3Result.Ok;
        }
        return (int)Vst3Result.False;
    }

    private int SetBinaryImpl(IntPtr self, IntPtr id, IntPtr data, uint sizeInBytes)
    {
        var key = Marshal.PtrToStringAnsi(id);
        if (key == null) return (int)Vst3Result.InvalidArgument;
        var bytes = new byte[sizeInBytes];
        Marshal.Copy(data, bytes, 0, (int)sizeInBytes);
        _attributes[key] = bytes;
        return (int)Vst3Result.Ok;
    }

    private int GetBinaryImpl(IntPtr self, IntPtr id, out IntPtr data, out uint sizeInBytes)
    {
        var key = Marshal.PtrToStringAnsi(id);
        if (key != null && _attributes.TryGetValue(key, out var obj) && obj is byte[] bytes)
        {
            // Note: The caller is responsible for the memory in VST3 spec
            // This is a simplified implementation
            data = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, data, bytes.Length);
            sizeInBytes = (uint)bytes.Length;
            return (int)Vst3Result.Ok;
        }
        data = IntPtr.Zero;
        sizeInBytes = 0;
        return (int)Vst3Result.False;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_comObject != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_comObject);
            _comObject = IntPtr.Zero;
        }

        if (_vtblPtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_vtblPtr);
            _vtblPtr = IntPtr.Zero;
        }

        if (_gcHandle.IsAllocated)
        {
            _gcHandle.Free();
        }

        _attributes.Clear();
    }
}

#endregion

#region Simplified IMessage Implementation

/// <summary>
/// Simplified IMessage implementation for inter-component communication.
/// </summary>
internal sealed class Vst3Message : IDisposable
{
    private IntPtr _comObject;
    private IntPtr _vtblPtr;
    private GCHandle _gcHandle;
    private bool _disposed;
    private int _refCount;

    private string _messageId = string.Empty;
    private readonly Vst3AttributeList _attributes;

    // Delegate instances
    private readonly QueryInterfaceDelegate _queryInterface;
    private readonly AddRefDelegate _addRef;
    private readonly ReleaseDelegate _release;
    private readonly MessageGetIdDelegate _getMessageId;
    private readonly MessageSetIdDelegate _setMessageId;
    private readonly MessageGetAttributesDelegate _getAttributes;

    public IntPtr NativePtr => _comObject;

    public Vst3Message()
    {
        _refCount = 1;
        _attributes = new Vst3AttributeList();

        // Create delegate instances
        _queryInterface = QueryInterfaceImpl;
        _addRef = AddRefImpl;
        _release = ReleaseImpl;
        _getMessageId = GetMessageIdImpl;
        _setMessageId = SetMessageIdImpl;
        _getAttributes = GetAttributesImpl;

        _gcHandle = GCHandle.Alloc(this);

        // Allocate vtable
        _vtblPtr = Marshal.AllocHGlobal(Marshal.SizeOf<IMessageVtbl>());

        var vtbl = new IMessageVtbl
        {
            QueryInterface = Marshal.GetFunctionPointerForDelegate(_queryInterface),
            AddRef = Marshal.GetFunctionPointerForDelegate(_addRef),
            Release = Marshal.GetFunctionPointerForDelegate(_release),
            GetMessageID = Marshal.GetFunctionPointerForDelegate(_getMessageId),
            SetMessageID = Marshal.GetFunctionPointerForDelegate(_setMessageId),
            GetAttributes = Marshal.GetFunctionPointerForDelegate(_getAttributes)
        };
        Marshal.StructureToPtr(vtbl, _vtblPtr, false);

        _comObject = Marshal.AllocHGlobal(IntPtr.Size);
        Marshal.WriteIntPtr(_comObject, _vtblPtr);
    }

    private int QueryInterfaceImpl(IntPtr self, ref Guid riid, out IntPtr ppvObject)
    {
        if (riid == Vst3Guids.IMessage || riid == Vst3Guids.FUnknown)
        {
            ppvObject = _comObject;
            _refCount++;
            return (int)Vst3Result.Ok;
        }
        ppvObject = IntPtr.Zero;
        return (int)Vst3Result.NotImplemented;
    }

    private uint AddRefImpl(IntPtr self) => (uint)System.Threading.Interlocked.Increment(ref _refCount);

    private uint ReleaseImpl(IntPtr self)
    {
        int newCount = System.Threading.Interlocked.Decrement(ref _refCount);
        if (newCount <= 0) Dispose();
        return (uint)Math.Max(0, newCount);
    }

    private IntPtr GetMessageIdImpl(IntPtr self)
    {
        // Return a pointer to a null-terminated ANSI string
        // Note: This memory needs to be managed carefully in production code
        return Marshal.StringToHGlobalAnsi(_messageId);
    }

    private void SetMessageIdImpl(IntPtr self, IntPtr id)
    {
        _messageId = Marshal.PtrToStringAnsi(id) ?? string.Empty;
    }

    private IntPtr GetAttributesImpl(IntPtr self)
    {
        return _attributes.NativePtr;
    }

    /// <summary>
    /// Sets the message ID from managed code.
    /// </summary>
    public void SetMessageId(string id)
    {
        _messageId = id ?? string.Empty;
    }

    /// <summary>
    /// Gets the message ID from managed code.
    /// </summary>
    public string GetMessageId() => _messageId;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _attributes.Dispose();

        if (_comObject != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_comObject);
            _comObject = IntPtr.Zero;
        }

        if (_vtblPtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_vtblPtr);
            _vtblPtr = IntPtr.Zero;
        }

        if (_gcHandle.IsAllocated)
        {
            _gcHandle.Free();
        }
    }
}

#endregion
