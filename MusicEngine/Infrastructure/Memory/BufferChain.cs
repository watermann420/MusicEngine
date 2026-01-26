// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System.Collections;
using System.Runtime.CompilerServices;

namespace MusicEngine.Infrastructure.Memory;

/// <summary>
/// A linked list of audio buffers for variable-length audio data.
/// Useful for recording, delay lines, and other scenarios where the total length is unknown upfront.
/// </summary>
public sealed class BufferChain : IEnumerable<BufferChainSegment>, IDisposable
{
    private readonly LinkedList<BufferChainSegment> _segments = new();
    private readonly OptimizedAudioBufferPool _pool;
    private readonly object _lock = new();
    private long _totalLength;
    private bool _disposed;

    /// <summary>
    /// Gets the total number of samples across all segments.
    /// </summary>
    public long TotalLength
    {
        get
        {
            lock (_lock)
            {
                return _totalLength;
            }
        }
    }

    /// <summary>
    /// Gets the number of segments in the chain.
    /// </summary>
    public int SegmentCount
    {
        get
        {
            lock (_lock)
            {
                return _segments.Count;
            }
        }
    }

    /// <summary>
    /// Gets whether the chain is empty.
    /// </summary>
    public bool IsEmpty
    {
        get
        {
            lock (_lock)
            {
                return _segments.Count == 0;
            }
        }
    }

    /// <summary>
    /// Creates a new buffer chain using the shared pool.
    /// </summary>
    public BufferChain() : this(OptimizedAudioBufferPool.Instance)
    {
    }

    /// <summary>
    /// Creates a new buffer chain using the specified pool.
    /// </summary>
    /// <param name="pool">The buffer pool to use.</param>
    public BufferChain(OptimizedAudioBufferPool pool)
    {
        _pool = pool ?? throw new ArgumentNullException(nameof(pool));
    }

    /// <summary>
    /// Adds a buffer to the end of the chain.
    /// The buffer is copied to a pooled buffer.
    /// </summary>
    /// <param name="buffer">The buffer data to add.</param>
    public void Add(ReadOnlySpan<float> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (buffer.IsEmpty) return;

        var pooledBuffer = _pool.Rent(buffer.Length);
        buffer.CopyTo(pooledBuffer.AsSpan());

        lock (_lock)
        {
            _segments.AddLast(new BufferChainSegment(pooledBuffer, buffer.Length, _pool));
            _totalLength += buffer.Length;
        }
    }

    /// <summary>
    /// Adds an existing pooled buffer to the chain.
    /// The buffer ownership is transferred to the chain.
    /// </summary>
    /// <param name="buffer">The buffer array.</param>
    /// <param name="length">The actual data length in the buffer.</param>
    public void AddPooledBuffer(float[] buffer, int length)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        if (length <= 0) return;

        lock (_lock)
        {
            _segments.AddLast(new BufferChainSegment(buffer, length, _pool));
            _totalLength += length;
        }
    }

    /// <summary>
    /// Prepends a buffer to the beginning of the chain.
    /// </summary>
    /// <param name="buffer">The buffer data to prepend.</param>
    public void Prepend(ReadOnlySpan<float> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (buffer.IsEmpty) return;

        var pooledBuffer = _pool.Rent(buffer.Length);
        buffer.CopyTo(pooledBuffer.AsSpan());

        lock (_lock)
        {
            _segments.AddFirst(new BufferChainSegment(pooledBuffer, buffer.Length, _pool));
            _totalLength += buffer.Length;
        }
    }

    /// <summary>
    /// Gets all data as a single contiguous array.
    /// Allocates a new array - use GetContiguous(Span) for zero-allocation version.
    /// </summary>
    /// <returns>A new array containing all the data.</returns>
    public float[] GetContiguous()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            if (_totalLength > int.MaxValue)
                throw new InvalidOperationException("Total length exceeds maximum array size");

            var result = new float[_totalLength];
            CopyToInternal(result.AsSpan());
            return result;
        }
    }

    /// <summary>
    /// Copies all data to a destination span.
    /// </summary>
    /// <param name="destination">The destination span.</param>
    /// <exception cref="ArgumentException">Thrown if destination is too small.</exception>
    public void GetContiguous(Span<float> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            if (destination.Length < _totalLength)
                throw new ArgumentException("Destination is too small", nameof(destination));

            CopyToInternal(destination);
        }
    }

    /// <summary>
    /// Gets a contiguous buffer from the pool (returns pooled buffer).
    /// Remember to return the buffer to the pool when done.
    /// </summary>
    /// <returns>A pooled buffer containing all the data.</returns>
    public PooledAudioBuffer GetContiguousPooled()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            if (_totalLength > int.MaxValue)
                throw new InvalidOperationException("Total length exceeds maximum array size");

            var pooledBuffer = _pool.RentScoped((int)_totalLength);
            CopyToInternal(pooledBuffer.Data);
            return pooledBuffer;
        }
    }

    /// <summary>
    /// Removes and returns the first segment from the chain.
    /// </summary>
    /// <returns>The first segment, or null if empty.</returns>
    public BufferChainSegment? PopFirst()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            if (_segments.First == null) return null;

            var segment = _segments.First.Value;
            _segments.RemoveFirst();
            _totalLength -= segment.Length;
            return segment;
        }
    }

    /// <summary>
    /// Removes and returns the last segment from the chain.
    /// </summary>
    /// <returns>The last segment, or null if empty.</returns>
    public BufferChainSegment? PopLast()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            if (_segments.Last == null) return null;

            var segment = _segments.Last.Value;
            _segments.RemoveLast();
            _totalLength -= segment.Length;
            return segment;
        }
    }

    /// <summary>
    /// Clears all segments and returns buffers to the pool.
    /// </summary>
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            foreach (var segment in _segments)
            {
                segment.Dispose();
            }
            _segments.Clear();
            _totalLength = 0;
        }
    }

    /// <summary>
    /// Trims the chain to keep only the specified number of samples from the end.
    /// </summary>
    /// <param name="keepSamples">The number of samples to keep.</param>
    public void TrimToLength(long keepSamples)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (keepSamples <= 0)
        {
            Clear();
            return;
        }

        lock (_lock)
        {
            while (_totalLength > keepSamples && _segments.First != null)
            {
                var first = _segments.First.Value;
                if (_totalLength - first.Length >= keepSamples)
                {
                    _segments.RemoveFirst();
                    _totalLength -= first.Length;
                    first.Dispose();
                }
                else
                {
                    break;
                }
            }
        }
    }

    /// <inheritdoc/>
    public IEnumerator<BufferChainSegment> GetEnumerator()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            // Return a copy to allow enumeration while chain is modified
            return _segments.ToList().GetEnumerator();
        }
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CopyToInternal(Span<float> destination)
    {
        int offset = 0;
        foreach (var segment in _segments)
        {
            segment.Data.CopyTo(destination.Slice(offset));
            offset += segment.Length;
        }
    }
}

/// <summary>
/// Represents a single segment in a buffer chain.
/// </summary>
public sealed class BufferChainSegment : IDisposable
{
    private readonly float[] _buffer;
    private readonly int _length;
    private readonly OptimizedAudioBufferPool? _pool;
    private bool _disposed;

    /// <summary>
    /// Gets the data in this segment as a span.
    /// </summary>
    public Span<float> Data => _disposed
        ? throw new ObjectDisposedException(nameof(BufferChainSegment))
        : _buffer.AsSpan(0, _length);

    /// <summary>
    /// Gets the data as a read-only span.
    /// </summary>
    public ReadOnlySpan<float> ReadOnlyData => Data;

    /// <summary>
    /// Gets the length of data in this segment.
    /// </summary>
    public int Length => _length;

    /// <summary>
    /// Gets the underlying array (use with caution).
    /// </summary>
    public float[] Array => _disposed
        ? throw new ObjectDisposedException(nameof(BufferChainSegment))
        : _buffer;

    /// <summary>
    /// Creates a new buffer chain segment.
    /// </summary>
    internal BufferChainSegment(float[] buffer, int length, OptimizedAudioBufferPool? pool)
    {
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        _length = length;
        _pool = pool;
    }

    /// <summary>
    /// Returns the buffer to the pool.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pool?.Return(_buffer);
    }
}
