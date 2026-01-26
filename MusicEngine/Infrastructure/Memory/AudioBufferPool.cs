// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System.Buffers;

namespace MusicEngine.Infrastructure.Memory;

/// <summary>
/// ArrayPool-based implementation of audio buffer pooling.
/// This class provides both the legacy API (RentedBuffer) and the new optimized API.
/// </summary>
public class AudioBufferPool : IAudioBufferPool
{
    private readonly ArrayPool<float> _floatPool;
    private readonly ArrayPool<byte> _bytePool;
    private readonly ArrayPool<double> _doublePool;
    private readonly OptimizedAudioBufferPool _optimizedPool;

    /// <summary>
    /// Gets the singleton instance for shared access.
    /// </summary>
    public static AudioBufferPool Instance { get; } = new();

    public AudioBufferPool() : this(BufferPoolConfig.Default)
    {
    }

    public AudioBufferPool(BufferPoolConfig config)
    {
        // Use shared pools for efficiency
        _floatPool = ArrayPool<float>.Shared;
        _bytePool = ArrayPool<byte>.Shared;
        _doublePool = ArrayPool<double>.Shared;
        _optimizedPool = new OptimizedAudioBufferPool(config);
    }

    /// <summary>
    /// Rents a buffer wrapped in RentedBuffer (legacy API).
    /// </summary>
    public RentedBuffer<float> Rent(int minimumLength)
    {
        var buffer = _floatPool.Rent(minimumLength);
        return new RentedBuffer<float>(buffer, minimumLength, this);
    }

    /// <summary>
    /// Rents a typed buffer wrapped in RentedBuffer (legacy API).
    /// </summary>
    public RentedBuffer<T> Rent<T>(int minimumLength)
    {
        var pool = ArrayPool<T>.Shared;
        var buffer = pool.Rent(minimumLength);
        return new RentedBuffer<T>(buffer, minimumLength, pool);
    }

    /// <summary>
    /// Rents a raw float array from the optimized pool.
    /// </summary>
    public float[] RentArray(int minimumLength)
    {
        return _optimizedPool.Rent(minimumLength);
    }

    /// <summary>
    /// Rents a scoped buffer that auto-returns on dispose.
    /// </summary>
    public PooledAudioBuffer RentScoped(int minimumLength)
    {
        return _optimizedPool.RentScoped(minimumLength);
    }

    /// <summary>
    /// Returns a float buffer to the pool.
    /// </summary>
    public void Return(float[] buffer, bool clearArray = false)
    {
        _floatPool.Return(buffer, clearArray);
    }

    /// <summary>
    /// Returns a typed buffer to the pool.
    /// </summary>
    public void Return<T>(T[] buffer, bool clearArray = false)
    {
        ArrayPool<T>.Shared.Return(buffer, clearArray);
    }

    /// <summary>
    /// Returns a buffer to the optimized pool.
    /// </summary>
    public void ReturnToOptimized(float[] buffer, bool clearArray = false)
    {
        _optimizedPool.Return(buffer, clearArray);
    }

    /// <summary>
    /// Releases unused buffers to reduce memory usage.
    /// </summary>
    public void Trim()
    {
        _optimizedPool.Trim();
    }

    /// <summary>
    /// Gets statistics from the optimized pool.
    /// </summary>
    public BufferPoolStatistics GetStatistics()
    {
        return _optimizedPool.GetStatistics();
    }

    /// <summary>
    /// Gets the underlying optimized pool for direct access.
    /// </summary>
    public OptimizedAudioBufferPool OptimizedPool => _optimizedPool;
}
