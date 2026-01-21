//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: VST3 IBStream interface implementation for state transfer.

using System;
using System.IO;
using System.Runtime.InteropServices;
using MusicEngine.Core.Vst.Vst3.Structures;

namespace MusicEngine.Core.Vst.Vst3.Interfaces;

#region VTable Structure

/// <summary>
/// IBStream vtable structure
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct IBStreamVtbl
{
    // FUnknown methods
    public IntPtr QueryInterface;
    public IntPtr AddRef;
    public IntPtr Release;

    // IBStream methods
    public IntPtr Read;
    public IntPtr Write;
    public IntPtr Seek;
    public IntPtr Tell;
}

#endregion

#region Seek Mode Enum

/// <summary>
/// IBStream seek modes (matches stdio)
/// </summary>
internal enum IStreamSeekMode : int
{
    Set = 0,     // SEEK_SET - Seek from beginning
    Current = 1, // SEEK_CUR - Seek from current position
    End = 2      // SEEK_END - Seek from end
}

#endregion

#region Delegate Types

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int BStreamReadDelegate(IntPtr self, IntPtr buffer, int numBytes, out int numBytesRead);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int BStreamWriteDelegate(IntPtr self, IntPtr buffer, int numBytes, out int numBytesWritten);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int BStreamSeekDelegate(IntPtr self, long pos, int mode, out long result);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int BStreamTellDelegate(IntPtr self, out long pos);

#endregion

#region Managed Implementation

/// <summary>
/// Managed implementation of IBStream that wraps a MemoryStream.
/// This is used to transfer plugin state data to/from VST3 plugins.
/// </summary>
internal sealed class Vst3BStream : IDisposable
{
    // COM object layout
    private IntPtr _comObject;
    private IntPtr _vtblPtr;
    private GCHandle _gcHandle;
    private bool _disposed;

    // The underlying stream
    private readonly MemoryStream _stream;
    private readonly bool _ownsStream;

    // Delegate instances (must be kept alive)
    private readonly QueryInterfaceDelegate _queryInterface;
    private readonly AddRefDelegate _addRef;
    private readonly ReleaseDelegate _release;
    private readonly BStreamReadDelegate _read;
    private readonly BStreamWriteDelegate _write;
    private readonly BStreamSeekDelegate _seek;
    private readonly BStreamTellDelegate _tell;

    // Reference count for COM semantics
    private int _refCount;

    /// <summary>
    /// GUID for IBStream interface
    /// </summary>
    public static readonly Guid IBStreamGuid = new("C3BF6EA2-3099-4752-9B6B-F9901EE33E9B");

    /// <summary>
    /// Gets the native COM pointer that can be passed to plugins.
    /// </summary>
    public IntPtr NativePtr => _comObject;

    /// <summary>
    /// Gets the underlying MemoryStream.
    /// </summary>
    public MemoryStream Stream => _stream;

    /// <summary>
    /// Creates a new IBStream with an empty MemoryStream for writing.
    /// </summary>
    public Vst3BStream() : this(new MemoryStream(), ownsStream: true)
    {
    }

    /// <summary>
    /// Creates a new IBStream wrapping existing data for reading.
    /// </summary>
    /// <param name="data">The data to wrap.</param>
    public Vst3BStream(byte[] data) : this(new MemoryStream(data), ownsStream: true)
    {
    }

    /// <summary>
    /// Creates a new IBStream wrapping an existing MemoryStream.
    /// </summary>
    /// <param name="stream">The stream to wrap.</param>
    /// <param name="ownsStream">If true, the stream will be disposed when this object is disposed.</param>
    public Vst3BStream(MemoryStream stream, bool ownsStream = false)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _ownsStream = ownsStream;
        _refCount = 1;

        // Create delegate instances
        _queryInterface = QueryInterfaceImpl;
        _addRef = AddRefImpl;
        _release = ReleaseImpl;
        _read = ReadImpl;
        _write = WriteImpl;
        _seek = SeekImpl;
        _tell = TellImpl;

        // Pin this object so it won't move in memory
        _gcHandle = GCHandle.Alloc(this);

        // Allocate vtable
        _vtblPtr = Marshal.AllocHGlobal(Marshal.SizeOf<IBStreamVtbl>());

        // Populate vtable
        var vtbl = new IBStreamVtbl
        {
            QueryInterface = Marshal.GetFunctionPointerForDelegate(_queryInterface),
            AddRef = Marshal.GetFunctionPointerForDelegate(_addRef),
            Release = Marshal.GetFunctionPointerForDelegate(_release),
            Read = Marshal.GetFunctionPointerForDelegate(_read),
            Write = Marshal.GetFunctionPointerForDelegate(_write),
            Seek = Marshal.GetFunctionPointerForDelegate(_seek),
            Tell = Marshal.GetFunctionPointerForDelegate(_tell)
        };
        Marshal.StructureToPtr(vtbl, _vtblPtr, false);

        // Allocate COM object (just a pointer to the vtable)
        _comObject = Marshal.AllocHGlobal(IntPtr.Size);
        Marshal.WriteIntPtr(_comObject, _vtblPtr);
    }

    private int QueryInterfaceImpl(IntPtr self, ref Guid riid, out IntPtr ppvObject)
    {
        // Support IBStream and FUnknown
        if (riid == IBStreamGuid || riid == Vst3Guids.FUnknown)
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
        // Don't auto-dispose on release - we manage lifetime explicitly
        return (uint)Math.Max(0, newCount);
    }

    private int ReadImpl(IntPtr self, IntPtr buffer, int numBytes, out int numBytesRead)
    {
        numBytesRead = 0;

        if (buffer == IntPtr.Zero || numBytes <= 0)
            return (int)Vst3Result.InvalidArgument;

        try
        {
            // Read from our stream into a managed buffer
            byte[] tempBuffer = new byte[numBytes];
            int bytesRead = _stream.Read(tempBuffer, 0, numBytes);

            // Copy to unmanaged memory
            if (bytesRead > 0)
            {
                Marshal.Copy(tempBuffer, 0, buffer, bytesRead);
            }

            numBytesRead = bytesRead;
            return (int)Vst3Result.Ok;
        }
        catch
        {
            return (int)Vst3Result.InternalError;
        }
    }

    private int WriteImpl(IntPtr self, IntPtr buffer, int numBytes, out int numBytesWritten)
    {
        numBytesWritten = 0;

        if (buffer == IntPtr.Zero || numBytes <= 0)
            return (int)Vst3Result.InvalidArgument;

        try
        {
            // Copy from unmanaged memory to a managed buffer
            byte[] tempBuffer = new byte[numBytes];
            Marshal.Copy(buffer, tempBuffer, 0, numBytes);

            // Write to our stream
            _stream.Write(tempBuffer, 0, numBytes);

            numBytesWritten = numBytes;
            return (int)Vst3Result.Ok;
        }
        catch
        {
            return (int)Vst3Result.InternalError;
        }
    }

    private int SeekImpl(IntPtr self, long pos, int mode, out long result)
    {
        result = 0;

        try
        {
            SeekOrigin origin = mode switch
            {
                (int)IStreamSeekMode.Set => SeekOrigin.Begin,
                (int)IStreamSeekMode.Current => SeekOrigin.Current,
                (int)IStreamSeekMode.End => SeekOrigin.End,
                _ => SeekOrigin.Begin
            };

            result = _stream.Seek(pos, origin);
            return (int)Vst3Result.Ok;
        }
        catch
        {
            return (int)Vst3Result.InternalError;
        }
    }

    private int TellImpl(IntPtr self, out long pos)
    {
        try
        {
            pos = _stream.Position;
            return (int)Vst3Result.Ok;
        }
        catch
        {
            pos = 0;
            return (int)Vst3Result.InternalError;
        }
    }

    /// <summary>
    /// Gets the stream data as a byte array.
    /// </summary>
    public byte[] ToArray()
    {
        return _stream.ToArray();
    }

    /// <summary>
    /// Resets the stream position to the beginning.
    /// </summary>
    public void Reset()
    {
        _stream.Position = 0;
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

        if (_ownsStream)
        {
            _stream.Dispose();
        }
    }
}

#endregion
