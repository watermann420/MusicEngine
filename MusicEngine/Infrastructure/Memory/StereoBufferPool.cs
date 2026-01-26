// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System.Buffers;
using System.Runtime.CompilerServices;

namespace MusicEngine.Infrastructure.Memory;

/// <summary>
/// Specialized pool for stereo audio buffer pairs.
/// Ensures both left and right channels are from the same pool and have matching sizes.
/// </summary>
public sealed class StereoBufferPool : IDisposable
{
    private readonly OptimizedAudioBufferPool _pool;
    private bool _disposed;

    /// <summary>
    /// Gets the singleton instance using the shared pool.
    /// </summary>
    public static StereoBufferPool Instance { get; } = new(OptimizedAudioBufferPool.Instance);

    /// <summary>
    /// Creates a new stereo buffer pool using the specified underlying pool.
    /// </summary>
    /// <param name="pool">The underlying audio buffer pool.</param>
    public StereoBufferPool(OptimizedAudioBufferPool pool)
    {
        _pool = pool ?? throw new ArgumentNullException(nameof(pool));
    }

    /// <summary>
    /// Creates a new stereo buffer pool with default configuration.
    /// </summary>
    public StereoBufferPool() : this(OptimizedAudioBufferPool.Instance)
    {
    }

    /// <summary>
    /// Rents a stereo buffer pair (left and right channels).
    /// </summary>
    /// <param name="sizePerChannel">The minimum size for each channel.</param>
    /// <returns>A tuple containing the left and right channel buffers.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (float[] Left, float[] Right) Rent(int sizePerChannel)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var left = _pool.Rent(sizePerChannel);
        var right = _pool.Rent(sizePerChannel);
        return (left, right);
    }

    /// <summary>
    /// Rents a stereo buffer pair as a scoped disposable.
    /// </summary>
    /// <param name="sizePerChannel">The minimum size for each channel.</param>
    /// <returns>A disposable stereo buffer that returns to pool on dispose.</returns>
    public PooledStereoBuffer RentScoped(int sizePerChannel)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new PooledStereoBuffer(sizePerChannel, this);
    }

    /// <summary>
    /// Returns a stereo buffer pair to the pool.
    /// </summary>
    /// <param name="left">The left channel buffer.</param>
    /// <param name="right">The right channel buffer.</param>
    /// <param name="clearArrays">Whether to clear the arrays before returning.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(float[] left, float[] right, bool clearArrays = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _pool.Return(left, clearArrays);
        _pool.Return(right, clearArrays);
    }

    /// <summary>
    /// Returns a stereo buffer pair (tuple) to the pool.
    /// </summary>
    /// <param name="stereoBuffer">The stereo buffer tuple.</param>
    /// <param name="clearArrays">Whether to clear the arrays before returning.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return((float[] Left, float[] Right) stereoBuffer, bool clearArrays = false)
    {
        Return(stereoBuffer.Left, stereoBuffer.Right, clearArrays);
    }

    /// <summary>
    /// Gets statistics for the underlying pool.
    /// </summary>
    public BufferPoolStatistics GetStatistics() => _pool.GetStatistics();

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // Don't dispose the underlying pool if using the shared instance
    }
}

/// <summary>
/// Disposable wrapper for a stereo buffer pair that returns to pool on dispose.
/// </summary>
public sealed class PooledStereoBuffer : IDisposable
{
    private readonly float[] _left;
    private readonly float[] _right;
    private readonly int _length;
    private readonly StereoBufferPool _pool;
    private bool _disposed;

    /// <summary>
    /// Gets the left channel data as a Span.
    /// </summary>
    public Span<float> Left => _disposed
        ? throw new ObjectDisposedException(nameof(PooledStereoBuffer))
        : _left.AsSpan(0, _length);

    /// <summary>
    /// Gets the right channel data as a Span.
    /// </summary>
    public Span<float> Right => _disposed
        ? throw new ObjectDisposedException(nameof(PooledStereoBuffer))
        : _right.AsSpan(0, _length);

    /// <summary>
    /// Gets the left channel as an array (use with caution).
    /// </summary>
    public float[] LeftArray => _disposed
        ? throw new ObjectDisposedException(nameof(PooledStereoBuffer))
        : _left;

    /// <summary>
    /// Gets the right channel as an array (use with caution).
    /// </summary>
    public float[] RightArray => _disposed
        ? throw new ObjectDisposedException(nameof(PooledStereoBuffer))
        : _right;

    /// <summary>
    /// Gets the requested length per channel.
    /// </summary>
    public int Length => _length;

    /// <summary>
    /// Gets whether this buffer has been disposed.
    /// </summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// Creates a new pooled stereo buffer.
    /// </summary>
    internal PooledStereoBuffer(int length, StereoBufferPool pool)
    {
        _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        _length = length;
        var (left, right) = pool.Rent(length);
        _left = left;
        _right = right;
        _disposed = false;
    }

    /// <summary>
    /// Clears both channels to zero.
    /// </summary>
    public void Clear()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PooledStereoBuffer));
        Left.Clear();
        Right.Clear();
    }

    /// <summary>
    /// Interleaves the stereo channels into a single buffer.
    /// </summary>
    /// <param name="destination">The destination buffer (must be at least Length * 2).</param>
    public void Interleave(Span<float> destination)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PooledStereoBuffer));
        if (destination.Length < _length * 2)
            throw new ArgumentException("Destination too small", nameof(destination));

        for (int i = 0; i < _length; i++)
        {
            destination[i * 2] = _left[i];
            destination[i * 2 + 1] = _right[i];
        }
    }

    /// <summary>
    /// De-interleaves a stereo buffer into left and right channels.
    /// </summary>
    /// <param name="source">The interleaved source buffer.</param>
    public void DeInterleave(ReadOnlySpan<float> source)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PooledStereoBuffer));
        if (source.Length < _length * 2)
            throw new ArgumentException("Source too small", nameof(source));

        for (int i = 0; i < _length; i++)
        {
            _left[i] = source[i * 2];
            _right[i] = source[i * 2 + 1];
        }
    }

    /// <summary>
    /// Returns the buffers to the pool.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pool.Return(_left, _right);
    }

    /// <summary>
    /// Deconstruct for tuple-like usage.
    /// </summary>
    public void Deconstruct(out Span<float> left, out Span<float> right)
    {
        left = Left;
        right = Right;
    }
}
