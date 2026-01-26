// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Runtime.InteropServices;
using MusicEngine.Core.Vst.Vst3.Structures;

namespace MusicEngine.Core.Vst.Vst3.Interfaces;

/// <summary>
/// IPluginFactory vtable structure
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct IPluginFactoryVtbl
{
    // FUnknown methods
    public IntPtr QueryInterface;
    public IntPtr AddRef;
    public IntPtr Release;

    // IPluginFactory methods
    public IntPtr GetFactoryInfo;
    public IntPtr CountClasses;
    public IntPtr GetClassInfo;
    public IntPtr CreateInstance;
}

/// <summary>
/// IPluginFactory2 vtable structure
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct IPluginFactory2Vtbl
{
    // FUnknown methods
    public IntPtr QueryInterface;
    public IntPtr AddRef;
    public IntPtr Release;

    // IPluginFactory methods
    public IntPtr GetFactoryInfo;
    public IntPtr CountClasses;
    public IntPtr GetClassInfo;
    public IntPtr CreateInstance;

    // IPluginFactory2 methods
    public IntPtr GetClassInfo2;
}

/// <summary>
/// IPluginFactory3 vtable structure
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct IPluginFactory3Vtbl
{
    // FUnknown methods
    public IntPtr QueryInterface;
    public IntPtr AddRef;
    public IntPtr Release;

    // IPluginFactory methods
    public IntPtr GetFactoryInfo;
    public IntPtr CountClasses;
    public IntPtr GetClassInfo;
    public IntPtr CreateInstance;

    // IPluginFactory2 methods
    public IntPtr GetClassInfo2;

    // IPluginFactory3 methods
    public IntPtr GetClassInfoUnicode;
    public IntPtr SetHostContext;
}

// Delegate types for IPluginFactory methods
[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int QueryInterfaceDelegate(IntPtr self, ref Guid riid, out IntPtr ppvObject);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate uint AddRefDelegate(IntPtr self);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate uint ReleaseDelegate(IntPtr self);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int GetFactoryInfoDelegate(IntPtr self, out Vst3FactoryInfo info);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int CountClassesDelegate(IntPtr self);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int GetClassInfoDelegate(IntPtr self, int index, out Vst3ClassInfo info);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int GetClassInfo2Delegate(IntPtr self, int index, out Vst3ClassInfo2 info);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int GetClassInfoUnicodeDelegate(IntPtr self, int index, out Vst3ClassInfoW info);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int CreateInstanceDelegate(IntPtr self, ref Vst3Tuid cid, ref Guid iid, out IntPtr obj);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int SetHostContextDelegate(IntPtr self, IntPtr context);

/// <summary>
/// Managed wrapper for IPluginFactory COM interface
/// </summary>
internal class PluginFactoryWrapper : IDisposable
{
    private IntPtr _factoryPtr;
    private IntPtr _vtblPtr;
    private IPluginFactoryVtbl _vtbl;
    private IPluginFactory2Vtbl? _vtbl2;
    private IPluginFactory3Vtbl? _vtbl3;

    // Cached delegates
    private QueryInterfaceDelegate? _queryInterface;
    private AddRefDelegate? _addRef;
    private ReleaseDelegate? _release;
    private GetFactoryInfoDelegate? _getFactoryInfo;
    private CountClassesDelegate? _countClasses;
    private GetClassInfoDelegate? _getClassInfo;
    private GetClassInfo2Delegate? _getClassInfo2;
    private GetClassInfoUnicodeDelegate? _getClassInfoUnicode;
    private CreateInstanceDelegate? _createInstance;
    private SetHostContextDelegate? _setHostContext;

    public IntPtr NativePtr => _factoryPtr;
    public bool IsFactory2 => _vtbl2.HasValue;
    public bool IsFactory3 => _vtbl3.HasValue;

    public PluginFactoryWrapper(IntPtr factoryPtr)
    {
        if (factoryPtr == IntPtr.Zero)
            throw new ArgumentNullException(nameof(factoryPtr));

        _factoryPtr = factoryPtr;

        // Read vtable pointer (first field of COM object)
        _vtblPtr = Marshal.ReadIntPtr(_factoryPtr);
        _vtbl = Marshal.PtrToStructure<IPluginFactoryVtbl>(_vtblPtr);

        // Cache base delegates
        CacheBaseDelegates();

        // Try to query for IPluginFactory2
        if (QueryInterface(Vst3Guids.IPluginFactory2, out IntPtr factory2Ptr) == (int)Vst3Result.Ok)
        {
            var vtbl2Ptr = Marshal.ReadIntPtr(factory2Ptr);
            _vtbl2 = Marshal.PtrToStructure<IPluginFactory2Vtbl>(vtbl2Ptr);
            CacheFactory2Delegates();

            // Release the extra reference
            _release?.Invoke(factory2Ptr);

            // Try to query for IPluginFactory3
            if (QueryInterface(Vst3Guids.IPluginFactory3, out IntPtr factory3Ptr) == (int)Vst3Result.Ok)
            {
                var vtbl3Ptr = Marshal.ReadIntPtr(factory3Ptr);
                _vtbl3 = Marshal.PtrToStructure<IPluginFactory3Vtbl>(vtbl3Ptr);
                CacheFactory3Delegates();

                _release?.Invoke(factory3Ptr);
            }
        }
    }

    private void CacheBaseDelegates()
    {
        _queryInterface = Marshal.GetDelegateForFunctionPointer<QueryInterfaceDelegate>(_vtbl.QueryInterface);
        _addRef = Marshal.GetDelegateForFunctionPointer<AddRefDelegate>(_vtbl.AddRef);
        _release = Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(_vtbl.Release);
        _getFactoryInfo = Marshal.GetDelegateForFunctionPointer<GetFactoryInfoDelegate>(_vtbl.GetFactoryInfo);
        _countClasses = Marshal.GetDelegateForFunctionPointer<CountClassesDelegate>(_vtbl.CountClasses);
        _getClassInfo = Marshal.GetDelegateForFunctionPointer<GetClassInfoDelegate>(_vtbl.GetClassInfo);
        _createInstance = Marshal.GetDelegateForFunctionPointer<CreateInstanceDelegate>(_vtbl.CreateInstance);
    }

    private void CacheFactory2Delegates()
    {
        if (_vtbl2.HasValue)
        {
            _getClassInfo2 = Marshal.GetDelegateForFunctionPointer<GetClassInfo2Delegate>(_vtbl2.Value.GetClassInfo2);
        }
    }

    private void CacheFactory3Delegates()
    {
        if (_vtbl3.HasValue)
        {
            _getClassInfoUnicode = Marshal.GetDelegateForFunctionPointer<GetClassInfoUnicodeDelegate>(_vtbl3.Value.GetClassInfoUnicode);
            _setHostContext = Marshal.GetDelegateForFunctionPointer<SetHostContextDelegate>(_vtbl3.Value.SetHostContext);
        }
    }

    public int QueryInterface(Guid iid, out IntPtr ppvObject)
    {
        if (_queryInterface == null)
        {
            ppvObject = IntPtr.Zero;
            return (int)Vst3Result.NotImplemented;
        }
        return _queryInterface(_factoryPtr, ref iid, out ppvObject);
    }

    public uint AddRef()
    {
        return _addRef?.Invoke(_factoryPtr) ?? 0;
    }

    public uint Release()
    {
        return _release?.Invoke(_factoryPtr) ?? 0;
    }

    public int GetFactoryInfo(out Vst3FactoryInfo info)
    {
        if (_getFactoryInfo == null)
        {
            info = default;
            return (int)Vst3Result.NotImplemented;
        }
        return _getFactoryInfo(_factoryPtr, out info);
    }

    public int CountClasses()
    {
        return _countClasses?.Invoke(_factoryPtr) ?? 0;
    }

    public int GetClassInfo(int index, out Vst3ClassInfo info)
    {
        if (_getClassInfo == null)
        {
            info = default;
            return (int)Vst3Result.NotImplemented;
        }
        return _getClassInfo(_factoryPtr, index, out info);
    }

    public int GetClassInfo2(int index, out Vst3ClassInfo2 info)
    {
        if (_getClassInfo2 == null)
        {
            info = default;
            return (int)Vst3Result.NotImplemented;
        }
        return _getClassInfo2(_factoryPtr, index, out info);
    }

    public int GetClassInfoUnicode(int index, out Vst3ClassInfoW info)
    {
        if (_getClassInfoUnicode == null)
        {
            info = default;
            return (int)Vst3Result.NotImplemented;
        }
        return _getClassInfoUnicode(_factoryPtr, index, out info);
    }

    public int CreateInstance(Vst3Tuid cid, Guid iid, out IntPtr obj)
    {
        if (_createInstance == null)
        {
            obj = IntPtr.Zero;
            return (int)Vst3Result.NotImplemented;
        }
        return _createInstance(_factoryPtr, ref cid, ref iid, out obj);
    }

    public int SetHostContext(IntPtr context)
    {
        if (_setHostContext == null)
            return (int)Vst3Result.NotImplemented;
        return _setHostContext(_factoryPtr, context);
    }

    public void Dispose()
    {
        if (_factoryPtr != IntPtr.Zero)
        {
            Release();
            _factoryPtr = IntPtr.Zero;
        }
    }
}
