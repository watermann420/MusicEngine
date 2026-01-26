// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System.Buffers;

namespace MusicEngine.Infrastructure.Memory;

/// <summary>
/// IDisposable wrapper for rented buffers that automatically returns to pool.
/// </summary>
/// <typeparam name="T">The element type of the buffer.</typeparam>
public readonly struct RentedBuffer<T> : IDisposable
{
    private readonly T[] _buffer;
    private readonly int _length;
    private readonly IAudioBufferPool? _pool;
    private readonly ArrayPool<T>? _arrayPool;

    /// <summary>
    /// The actual buffer array (may be larger than requested length).
    /// </summary>
    public T[] Array => _buffer;

    /// <summary>
    /// The requested length (use this instead of Array.Length).
    /// </summary>
    public int Length => _length;

    /// <summary>
    /// Gets a span over the requested portion of the buffer.
    /// </summary>
    public Span<T> Span => _buffer.AsSpan(0, _length);

    /// <summary>
    /// Gets a memory over the requested portion of the buffer.
    /// </summary>
    public Memory<T> Memory => _buffer.AsMemory(0, _length);

    internal RentedBuffer(T[] buffer, int length, IAudioBufferPool pool)
    {
        _buffer = buffer;
        _length = length;
        _pool = pool;
        _arrayPool = null;
    }

    internal RentedBuffer(T[] buffer, int length, ArrayPool<T> pool)
    {
        _buffer = buffer;
        _length = length;
        _pool = null;
        _arrayPool = pool;
    }

    /// <summary>
    /// Clears the buffer contents.
    /// </summary>
    public void Clear()
    {
        System.Array.Clear(_buffer, 0, _length);
    }

    /// <summary>
    /// Returns the buffer to the pool.
    /// </summary>
    public void Dispose()
    {
        if (_pool != null)
        {
            _pool.Return(_buffer);
        }
        else if (_arrayPool != null)
        {
            _arrayPool.Return(_buffer);
        }
    }

    /// <summary>
    /// Indexer for direct access.
    /// </summary>
    public T this[int index]
    {
        get => _buffer[index];
        set => _buffer[index] = value;
    }

    /// <summary>
    /// Implicit conversion to Span.
    /// </summary>
    public static implicit operator Span<T>(RentedBuffer<T> buffer) => buffer.Span;

    /// <summary>
    /// Implicit conversion to ReadOnlySpan.
    /// </summary>
    public static implicit operator ReadOnlySpan<T>(RentedBuffer<T> buffer) => buffer.Span;
}
