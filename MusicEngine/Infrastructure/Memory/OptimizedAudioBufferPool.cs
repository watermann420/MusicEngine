// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace MusicEngine.Infrastructure.Memory;

/// <summary>
/// High-performance audio buffer pool with statistics tracking and lock-free operations.
/// Uses ArrayPool internally with fixed bucket sizes optimized for audio processing.
/// </summary>
public sealed class OptimizedAudioBufferPool : IDisposable
{
    private readonly ArrayPool<float> _arrayPool;
    private readonly BufferPoolConfig _config;
    private readonly ConcurrentDictionary<int, ConcurrentStack<float[]>> _buckets;
    private readonly Timer? _trimTimer;
    private bool _disposed;

    // Statistics (using Interlocked for lock-free updates)
    private long _totalRented;
    private long _totalReturned;
    private long _currentInUse;
    private long _peakUsage;
    private long _cacheHits;
    private long _cacheMisses;
    private long _totalBytesRented;

    /// <summary>
    /// Gets the singleton instance with default configuration.
    /// </summary>
    public static OptimizedAudioBufferPool Instance { get; } = new(BufferPoolConfig.Default);

    /// <summary>
    /// Creates a new optimized audio buffer pool with the specified configuration.
    /// </summary>
    /// <param name="config">Pool configuration.</param>
    public OptimizedAudioBufferPool(BufferPoolConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _arrayPool = ArrayPool<float>.Create(
            maxArrayLength: 8192 * 4,  // Support arrays up to 32K samples
            maxArraysPerBucket: config.MaxBuffersPerBucket);
        _buckets = new ConcurrentDictionary<int, ConcurrentStack<float[]>>();

        // Initialize buckets for standard sizes
        foreach (var size in config.BufferSizes)
        {
            _buckets[size] = new ConcurrentStack<float[]>();
        }

        // Setup periodic trim timer
        if (config.TrimIntervalMs > 0)
        {
            _trimTimer = new Timer(
                _ => Trim(),
                null,
                config.TrimIntervalMs,
                config.TrimIntervalMs);
        }

        // Pre-allocate if configured
        if (config.PreAllocate)
        {
            PreAllocateBuffers();
        }

        // Register with memory pressure monitor
        MemoryPressureMonitor.Instance.RegisterPool(this);
    }

    /// <summary>
    /// Creates a new pool with default configuration.
    /// </summary>
    public OptimizedAudioBufferPool() : this(BufferPoolConfig.Default)
    {
    }

    /// <summary>
    /// Rents a buffer of at least the specified minimum size.
    /// </summary>
    /// <param name="minSize">Minimum buffer size needed.</param>
    /// <returns>A buffer of at least minSize elements.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float[] Rent(int minSize)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (minSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(minSize), "Size must be positive");

        // Find the appropriate bucket size
        int bucketSize = GetBucketSize(minSize);
        float[]? buffer = null;

        // Try to get from our cache first
        if (_buckets.TryGetValue(bucketSize, out var stack) && stack.TryPop(out buffer))
        {
            if (_config.EnableStatistics)
            {
                Interlocked.Increment(ref _cacheHits);
            }
        }

        // Fall back to ArrayPool
        if (buffer == null)
        {
            buffer = _arrayPool.Rent(bucketSize);
            if (_config.EnableStatistics)
            {
                Interlocked.Increment(ref _cacheMisses);
            }
        }

        // Update statistics
        if (_config.EnableStatistics)
        {
            Interlocked.Increment(ref _totalRented);
            var currentlyInUse = Interlocked.Increment(ref _currentInUse);
            Interlocked.Add(ref _totalBytesRented, buffer.Length * sizeof(float));

            // Update peak using compare-exchange loop
            long current = _peakUsage;
            while (currentlyInUse > current)
            {
                long existing = Interlocked.CompareExchange(ref _peakUsage, currentlyInUse, current);
                if (existing == current) break;
                current = existing;
            }
        }

        return buffer;
    }

    /// <summary>
    /// Rents a buffer wrapped in a disposable for automatic return.
    /// </summary>
    /// <param name="minSize">Minimum buffer size needed.</param>
    /// <returns>A pooled buffer that returns to pool on dispose.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PooledAudioBuffer RentScoped(int minSize)
    {
        var buffer = Rent(minSize);
        return new PooledAudioBuffer(buffer, minSize, this);
    }

    /// <summary>
    /// Returns a buffer to the pool.
    /// </summary>
    /// <param name="buffer">The buffer to return.</param>
    /// <param name="clearArray">Whether to clear the array contents.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(float[] buffer, bool clearArray = false)
    {
        if (_disposed || buffer == null) return;

        if (clearArray || _config.ClearOnReturn)
        {
            Array.Clear(buffer, 0, buffer.Length);
        }

        // Try to return to our cache
        int bucketSize = GetBucketSize(buffer.Length);
        if (_buckets.TryGetValue(bucketSize, out var stack))
        {
            // Only cache if under limit
            if (stack.Count < _config.MaxBuffersPerBucket)
            {
                stack.Push(buffer);
                UpdateReturnStatistics();
                return;
            }
        }

        // Return to ArrayPool if our cache is full
        _arrayPool.Return(buffer, clearArray: false);
        UpdateReturnStatistics();
    }

    /// <summary>
    /// Releases unused buffers from the pool to reduce memory usage.
    /// </summary>
    public void Trim()
    {
        if (_disposed) return;

        int trimmedCount = 0;
        foreach (var kvp in _buckets)
        {
            var stack = kvp.Value;
            // Keep at least half the buffers
            int targetCount = Math.Max(1, stack.Count / 2);

            while (stack.Count > targetCount && stack.TryPop(out var buffer))
            {
                _arrayPool.Return(buffer, clearArray: false);
                trimmedCount++;
            }
        }

        if (trimmedCount > 0)
        {
            // Help the GC
            GC.Collect(0, GCCollectionMode.Optimized, blocking: false);
        }
    }

    /// <summary>
    /// Gets comprehensive statistics about the pool.
    /// </summary>
    /// <returns>Current pool statistics.</returns>
    public BufferPoolStatistics GetStatistics()
    {
        long cacheSize = 0;
        foreach (var kvp in _buckets)
        {
            cacheSize += kvp.Value.Count * kvp.Key * sizeof(float);
        }

        return new BufferPoolStatistics
        {
            TotalRented = Interlocked.Read(ref _totalRented),
            TotalReturned = Interlocked.Read(ref _totalReturned),
            CurrentInUse = Interlocked.Read(ref _currentInUse),
            PeakUsage = Interlocked.Read(ref _peakUsage),
            CacheHits = Interlocked.Read(ref _cacheHits),
            CacheMisses = Interlocked.Read(ref _cacheMisses),
            TotalBytesRented = Interlocked.Read(ref _totalBytesRented),
            CacheSizeBytes = cacheSize,
            HitRatio = _cacheHits + _cacheMisses > 0
                ? (double)_cacheHits / (_cacheHits + _cacheMisses)
                : 0
        };
    }

    /// <summary>
    /// Resets all statistics counters.
    /// </summary>
    public void ResetStatistics()
    {
        Interlocked.Exchange(ref _totalRented, 0);
        Interlocked.Exchange(ref _totalReturned, 0);
        Interlocked.Exchange(ref _currentInUse, 0);
        Interlocked.Exchange(ref _peakUsage, 0);
        Interlocked.Exchange(ref _cacheHits, 0);
        Interlocked.Exchange(ref _cacheMisses, 0);
        Interlocked.Exchange(ref _totalBytesRented, 0);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _trimTimer?.Dispose();
        MemoryPressureMonitor.Instance.UnregisterPool(this);

        // Clear all cached buffers
        foreach (var kvp in _buckets)
        {
            while (kvp.Value.TryPop(out var buffer))
            {
                _arrayPool.Return(buffer, clearArray: false);
            }
        }
        _buckets.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetBucketSize(int minSize)
    {
        // Find the smallest standard bucket size >= minSize
        if (minSize <= 64) return 64;
        if (minSize <= 128) return 128;
        if (minSize <= 256) return 256;
        if (minSize <= 512) return 512;
        if (minSize <= 1024) return 1024;
        if (minSize <= 2048) return 2048;
        if (minSize <= 4096) return 4096;
        if (minSize <= 8192) return 8192;

        // For larger sizes, round up to next power of two
        return BufferPoolConfig.RoundUpToPowerOfTwo(minSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateReturnStatistics()
    {
        if (_config.EnableStatistics)
        {
            Interlocked.Increment(ref _totalReturned);
            Interlocked.Decrement(ref _currentInUse);
        }
    }

    private void PreAllocateBuffers()
    {
        foreach (var size in _config.BufferSizes)
        {
            if (_buckets.TryGetValue(size, out var stack))
            {
                for (int i = 0; i < _config.PreAllocateCount; i++)
                {
                    var buffer = _arrayPool.Rent(size);
                    stack.Push(buffer);
                }
            }
        }
    }
}

/// <summary>
/// Statistics for the audio buffer pool.
/// </summary>
public readonly struct BufferPoolStatistics
{
    /// <summary>
    /// Total number of buffers rented.
    /// </summary>
    public long TotalRented { get; init; }

    /// <summary>
    /// Total number of buffers returned.
    /// </summary>
    public long TotalReturned { get; init; }

    /// <summary>
    /// Number of buffers currently in use.
    /// </summary>
    public long CurrentInUse { get; init; }

    /// <summary>
    /// Peak number of buffers in use at any time.
    /// </summary>
    public long PeakUsage { get; init; }

    /// <summary>
    /// Number of times a buffer was found in cache.
    /// </summary>
    public long CacheHits { get; init; }

    /// <summary>
    /// Number of times a new buffer had to be allocated.
    /// </summary>
    public long CacheMisses { get; init; }

    /// <summary>
    /// Total bytes rented over the lifetime.
    /// </summary>
    public long TotalBytesRented { get; init; }

    /// <summary>
    /// Current size of the buffer cache in bytes.
    /// </summary>
    public long CacheSizeBytes { get; init; }

    /// <summary>
    /// Cache hit ratio (0.0 to 1.0).
    /// </summary>
    public double HitRatio { get; init; }

    /// <summary>
    /// Returns a string representation of the statistics.
    /// </summary>
    public override string ToString()
    {
        return $"BufferPool: Rented={TotalRented}, Returned={TotalReturned}, InUse={CurrentInUse}, " +
               $"Peak={PeakUsage}, HitRatio={HitRatio:P1}, Cache={CacheSizeBytes / 1024.0:F1}KB";
    }
}
