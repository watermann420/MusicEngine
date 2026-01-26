// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Runtime.InteropServices;

namespace MusicEngine.Core.Vst.Vst3.Interfaces
{
    /// <summary>
    /// Rectangle structure for plug view sizing.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ViewRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        /// <summary>
        /// Gets the width of the rectangle.
        /// </summary>
        public int Width => Right - Left;

        /// <summary>
        /// Gets the height of the rectangle.
        /// </summary>
        public int Height => Bottom - Top;

        public ViewRect(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public ViewRect(int width, int height)
        {
            Left = 0;
            Top = 0;
            Right = width;
            Bottom = height;
        }
    }

    /// <summary>
    /// IPlugView vtable structure for VST3 GUI hosting.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct IPlugViewVtbl
    {
        // IUnknown methods
        public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
        public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
        public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

        // IPlugView methods
        /// <summary>
        /// Check if platform type (e.g. "HWND") is supported.
        /// </summary>
        public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int> IsPlatformTypeSupported;

        /// <summary>
        /// Attach view to parent window.
        /// </summary>
        public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, int> Attached;

        /// <summary>
        /// Remove view from parent.
        /// </summary>
        public delegate* unmanaged[Stdcall]<IntPtr, int> Removed;

        /// <summary>
        /// Mouse wheel event.
        /// </summary>
        public delegate* unmanaged[Stdcall]<IntPtr, float, int> OnWheel;

        /// <summary>
        /// Key down event.
        /// </summary>
        public delegate* unmanaged[Stdcall]<IntPtr, char, short, short, int> OnKeyDown;

        /// <summary>
        /// Key up event.
        /// </summary>
        public delegate* unmanaged[Stdcall]<IntPtr, char, short, short, int> OnKeyUp;

        /// <summary>
        /// Get preferred size.
        /// </summary>
        public delegate* unmanaged[Stdcall]<IntPtr, ViewRect*, int> GetSize;

        /// <summary>
        /// Resize notification.
        /// </summary>
        public delegate* unmanaged[Stdcall]<IntPtr, ViewRect*, int> OnSize;

        /// <summary>
        /// Focus change notification.
        /// </summary>
        public delegate* unmanaged[Stdcall]<IntPtr, byte, int> OnFocus;

        /// <summary>
        /// Set the plug frame.
        /// </summary>
        public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int> SetFrame;

        /// <summary>
        /// Check if view is resizable.
        /// </summary>
        public delegate* unmanaged[Stdcall]<IntPtr, int> CanResize;

        /// <summary>
        /// Check size constraints.
        /// </summary>
        public delegate* unmanaged[Stdcall]<IntPtr, ViewRect*, int> CheckSizeConstraint;
    }

    /// <summary>
    /// IPlugFrame vtable structure for hosting plugin GUIs.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct IPlugFrameVtbl
    {
        // IUnknown methods
        public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
        public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
        public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

        // IPlugFrame methods
        /// <summary>
        /// Request resize from the host.
        /// </summary>
        public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, ViewRect*, int> ResizeView;
    }

    /// <summary>
    /// Managed wrapper for IPlugView COM interface.
    /// </summary>
    public unsafe class PlugViewWrapper : IDisposable
    {
        private IntPtr _plugViewPtr;
        private IPlugViewVtbl* _vtbl;
        private bool _disposed;

        /// <summary>
        /// Platform type string for Windows HWND.
        /// </summary>
        public const string kPlatformTypeHWND = "HWND";

        /// <summary>
        /// Platform type string for macOS NSView.
        /// </summary>
        public const string kPlatformTypeNSView = "NSView";

        /// <summary>
        /// Platform type string for macOS HIView.
        /// </summary>
        public const string kPlatformTypeHIView = "HIView";

        /// <summary>
        /// Platform type string for Linux X11 Embed.
        /// </summary>
        public const string kPlatformTypeX11EmbedWindowID = "X11EmbedWindowID";

        /// <summary>
        /// Gets the native pointer to the IPlugView interface.
        /// </summary>
        public IntPtr NativePtr => _plugViewPtr;

        /// <summary>
        /// Gets whether this wrapper has a valid plug view.
        /// </summary>
        public bool IsValid => _plugViewPtr != IntPtr.Zero;

        public PlugViewWrapper(IntPtr plugViewPtr)
        {
            _plugViewPtr = plugViewPtr;
            if (_plugViewPtr != IntPtr.Zero)
            {
                IntPtr vtblPtr = Marshal.ReadIntPtr(_plugViewPtr);
                _vtbl = (IPlugViewVtbl*)vtblPtr;
            }
        }

        /// <summary>
        /// Check if platform type (e.g. "HWND") is supported.
        /// </summary>
        public bool IsPlatformTypeSupported(string type)
        {
            if (_vtbl == null || _vtbl->IsPlatformTypeSupported == null)
                return false;

            IntPtr typePtr = Marshal.StringToHGlobalAnsi(type);
            try
            {
                int result = _vtbl->IsPlatformTypeSupported(_plugViewPtr, typePtr);
                return result == 0; // kResultOk
            }
            finally
            {
                Marshal.FreeHGlobal(typePtr);
            }
        }

        /// <summary>
        /// Attach view to parent window.
        /// </summary>
        public int Attached(IntPtr parent, string type)
        {
            if (_vtbl == null || _vtbl->Attached == null)
                return -1;

            IntPtr typePtr = Marshal.StringToHGlobalAnsi(type);
            try
            {
                return _vtbl->Attached(_plugViewPtr, parent, typePtr);
            }
            finally
            {
                Marshal.FreeHGlobal(typePtr);
            }
        }

        /// <summary>
        /// Remove view from parent.
        /// </summary>
        public int Removed()
        {
            if (_vtbl == null || _vtbl->Removed == null)
                return -1;

            return _vtbl->Removed(_plugViewPtr);
        }

        /// <summary>
        /// Mouse wheel event.
        /// </summary>
        public int OnWheel(float distance)
        {
            if (_vtbl == null || _vtbl->OnWheel == null)
                return -1;

            return _vtbl->OnWheel(_plugViewPtr, distance);
        }

        /// <summary>
        /// Key down event.
        /// </summary>
        public int OnKeyDown(char key, short keyCode, short modifiers)
        {
            if (_vtbl == null || _vtbl->OnKeyDown == null)
                return -1;

            return _vtbl->OnKeyDown(_plugViewPtr, key, keyCode, modifiers);
        }

        /// <summary>
        /// Key up event.
        /// </summary>
        public int OnKeyUp(char key, short keyCode, short modifiers)
        {
            if (_vtbl == null || _vtbl->OnKeyUp == null)
                return -1;

            return _vtbl->OnKeyUp(_plugViewPtr, key, keyCode, modifiers);
        }

        /// <summary>
        /// Get preferred size.
        /// </summary>
        public int GetSize(out ViewRect size)
        {
            size = default;
            if (_vtbl == null || _vtbl->GetSize == null)
                return -1;

            fixed (ViewRect* sizePtr = &size)
            {
                return _vtbl->GetSize(_plugViewPtr, sizePtr);
            }
        }

        /// <summary>
        /// Resize notification.
        /// </summary>
        public int OnSize(ref ViewRect newSize)
        {
            if (_vtbl == null || _vtbl->OnSize == null)
                return -1;

            fixed (ViewRect* sizePtr = &newSize)
            {
                return _vtbl->OnSize(_plugViewPtr, sizePtr);
            }
        }

        /// <summary>
        /// Focus change notification.
        /// </summary>
        public int OnFocus(bool state)
        {
            if (_vtbl == null || _vtbl->OnFocus == null)
                return -1;

            return _vtbl->OnFocus(_plugViewPtr, (byte)(state ? 1 : 0));
        }

        /// <summary>
        /// Set the plug frame.
        /// </summary>
        public int SetFrame(IntPtr frame)
        {
            if (_vtbl == null || _vtbl->SetFrame == null)
                return -1;

            return _vtbl->SetFrame(_plugViewPtr, frame);
        }

        /// <summary>
        /// Check if view is resizable.
        /// </summary>
        public bool CanResize()
        {
            if (_vtbl == null || _vtbl->CanResize == null)
                return false;

            return _vtbl->CanResize(_plugViewPtr) == 0; // kResultOk
        }

        /// <summary>
        /// Check size constraints.
        /// </summary>
        public int CheckSizeConstraint(ref ViewRect rect)
        {
            if (_vtbl == null || _vtbl->CheckSizeConstraint == null)
                return -1;

            fixed (ViewRect* rectPtr = &rect)
            {
                return _vtbl->CheckSizeConstraint(_plugViewPtr, rectPtr);
            }
        }

        /// <summary>
        /// Release the COM reference.
        /// </summary>
        public void Release()
        {
            if (_plugViewPtr != IntPtr.Zero && _vtbl != null && _vtbl->Release != null)
            {
                _vtbl->Release(_plugViewPtr);
                _plugViewPtr = IntPtr.Zero;
                _vtbl = null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                Release();
                _disposed = true;
            }
        }

        ~PlugViewWrapper()
        {
            Dispose(false);
        }
    }

    /// <summary>
    /// Implementation of IPlugFrame for hosting plugin GUIs.
    /// </summary>
    public unsafe class Vst3PlugFrame : IDisposable
    {
        private IntPtr _framePtr;
        private IntPtr _vtblPtr;
        private IPlugFrameVtbl* _vtbl;
        private bool _disposed;
        private uint _refCount;

        // GC handles to prevent delegate garbage collection
        private GCHandle _queryInterfaceHandle;
        private GCHandle _addRefHandle;
        private GCHandle _releaseHandle;
        private GCHandle _resizeViewHandle;

        // IPlugFrame IID: 367FAF01-AFA9-4693-8D4D-A2A0ED0882A3
        public static readonly Guid IID = new Guid(0x367FAF01, 0xAFA9, 0x4693, 0x8D, 0x4D, 0xA2, 0xA0, 0xED, 0x08, 0x82, 0xA3);

        /// <summary>
        /// Event raised when the plugin requests a resize.
        /// </summary>
        public event Action<IntPtr, ViewRect>? ResizeRequested;

        /// <summary>
        /// Gets the native pointer to the IPlugFrame interface.
        /// </summary>
        public IntPtr NativePtr => _framePtr;

        public Vst3PlugFrame()
        {
            _refCount = 1;
            InitializeVtbl();
        }

        private void InitializeVtbl()
        {
            // Allocate memory for the vtable
            _vtblPtr = Marshal.AllocHGlobal(sizeof(IPlugFrameVtbl));
            _vtbl = (IPlugFrameVtbl*)_vtblPtr;

            // Create delegates and pin them
            var queryInterface = new QueryInterfaceDelegate(QueryInterfaceImpl);
            var addRef = new AddRefDelegate(AddRefImpl);
            var release = new ReleaseDelegate(ReleaseImpl);
            var resizeView = new ResizeViewDelegate(ResizeViewImpl);

            _queryInterfaceHandle = GCHandle.Alloc(queryInterface);
            _addRefHandle = GCHandle.Alloc(addRef);
            _releaseHandle = GCHandle.Alloc(release);
            _resizeViewHandle = GCHandle.Alloc(resizeView);

            // Assign function pointers to vtable
            _vtbl->QueryInterface = (delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int>)
                Marshal.GetFunctionPointerForDelegate(queryInterface);
            _vtbl->AddRef = (delegate* unmanaged[Stdcall]<IntPtr, uint>)
                Marshal.GetFunctionPointerForDelegate(addRef);
            _vtbl->Release = (delegate* unmanaged[Stdcall]<IntPtr, uint>)
                Marshal.GetFunctionPointerForDelegate(release);
            _vtbl->ResizeView = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, ViewRect*, int>)
                Marshal.GetFunctionPointerForDelegate(resizeView);

            // Allocate the object structure (vtbl pointer + instance pointer)
            _framePtr = Marshal.AllocHGlobal(IntPtr.Size * 2);
            Marshal.WriteIntPtr(_framePtr, _vtblPtr);

            // Store 'this' reference for callbacks
            GCHandle thisHandle = GCHandle.Alloc(this);
            Marshal.WriteIntPtr(_framePtr, IntPtr.Size, GCHandle.ToIntPtr(thisHandle));
        }

        // Delegate types for COM methods
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int QueryInterfaceDelegate(IntPtr self, Guid* iid, IntPtr* obj);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint AddRefDelegate(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint ReleaseDelegate(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int ResizeViewDelegate(IntPtr self, IntPtr view, ViewRect* newSize);

        private static Vst3PlugFrame? GetInstance(IntPtr self)
        {
            IntPtr handlePtr = Marshal.ReadIntPtr(self, IntPtr.Size);
            if (handlePtr != IntPtr.Zero)
            {
                GCHandle handle = GCHandle.FromIntPtr(handlePtr);
                return handle.Target as Vst3PlugFrame;
            }
            return null;
        }

        private static int QueryInterfaceImpl(IntPtr self, Guid* iid, IntPtr* obj)
        {
            if (*iid == IID || *iid == Guid.Empty)
            {
                *obj = self;
                var instance = GetInstance(self);
                if (instance != null)
                    instance._refCount++;
                return 0; // kResultOk
            }
            *obj = IntPtr.Zero;
            return -1; // kNoInterface
        }

        private static uint AddRefImpl(IntPtr self)
        {
            var instance = GetInstance(self);
            if (instance != null)
            {
                return ++instance._refCount;
            }
            return 0;
        }

        private static uint ReleaseImpl(IntPtr self)
        {
            var instance = GetInstance(self);
            if (instance != null)
            {
                uint count = --instance._refCount;
                if (count == 0)
                {
                    instance.Dispose();
                }
                return count;
            }
            return 0;
        }

        private static int ResizeViewImpl(IntPtr self, IntPtr view, ViewRect* newSize)
        {
            var instance = GetInstance(self);
            if (instance != null && newSize != null)
            {
                instance.ResizeRequested?.Invoke(view, *newSize);
                return 0; // kResultOk
            }
            return -1; // kResultFalse
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                // Free the instance handle
                if (_framePtr != IntPtr.Zero)
                {
                    IntPtr handlePtr = Marshal.ReadIntPtr(_framePtr, IntPtr.Size);
                    if (handlePtr != IntPtr.Zero)
                    {
                        GCHandle.FromIntPtr(handlePtr).Free();
                    }
                    Marshal.FreeHGlobal(_framePtr);
                    _framePtr = IntPtr.Zero;
                }

                // Free delegate handles
                if (_queryInterfaceHandle.IsAllocated) _queryInterfaceHandle.Free();
                if (_addRefHandle.IsAllocated) _addRefHandle.Free();
                if (_releaseHandle.IsAllocated) _releaseHandle.Free();
                if (_resizeViewHandle.IsAllocated) _resizeViewHandle.Free();

                // Free vtable
                if (_vtblPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_vtblPtr);
                    _vtblPtr = IntPtr.Zero;
                    _vtbl = null;
                }

                _disposed = true;
            }
        }

        ~Vst3PlugFrame()
        {
            Dispose(false);
        }
    }
}
