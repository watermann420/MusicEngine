using System.Collections.Frozen;

namespace MusicEngine.Infrastructure.Memory;

/// <summary>
/// Configuration for the audio buffer pool.
/// </summary>
public sealed class BufferPoolConfig
{
    /// <summary>
    /// Fixed buffer sizes available for pooling.
    /// These sizes are optimized for audio processing.
    /// </summary>
    public static readonly FrozenSet<int> StandardBufferSizes =
        new[] { 64, 128, 256, 512, 1024, 2048, 4096, 8192 }.ToFrozenSet();

    /// <summary>
    /// Default buffer sizes available for pooling.
    /// </summary>
    public int[] BufferSizes { get; init; } = [64, 128, 256, 512, 1024, 2048, 4096, 8192];

    /// <summary>
    /// Maximum number of buffers to retain per size bucket.
    /// Default is 50 buffers per size.
    /// </summary>
    public int MaxBuffersPerBucket { get; init; } = 50;

    /// <summary>
    /// Maximum total pool size in bytes.
    /// Default is 100MB.
    /// </summary>
    public long MaxPoolSizeBytes { get; init; } = 100 * 1024 * 1024;

    /// <summary>
    /// Interval in milliseconds for periodic trimming of unused buffers.
    /// Default is 30 seconds (30000ms).
    /// Set to 0 to disable automatic trimming.
    /// </summary>
    public int TrimIntervalMs { get; init; } = 30000;

    /// <summary>
    /// Whether to automatically clear buffers when returning them to the pool.
    /// Default is false for performance; set to true for security-sensitive applications.
    /// </summary>
    public bool ClearOnReturn { get; init; } = false;

    /// <summary>
    /// Whether to pre-allocate a minimum number of buffers at startup.
    /// Default is false.
    /// </summary>
    public bool PreAllocate { get; init; } = false;

    /// <summary>
    /// Number of buffers to pre-allocate per size if PreAllocate is enabled.
    /// Default is 5.
    /// </summary>
    public int PreAllocateCount { get; init; } = 5;

    /// <summary>
    /// Whether to track detailed statistics (may have slight performance impact).
    /// Default is true.
    /// </summary>
    public bool EnableStatistics { get; init; } = true;

    /// <summary>
    /// Default configuration instance.
    /// </summary>
    public static BufferPoolConfig Default { get; } = new();

    /// <summary>
    /// High-performance configuration with minimal overhead.
    /// </summary>
    public static BufferPoolConfig HighPerformance { get; } = new()
    {
        MaxBuffersPerBucket = 100,
        MaxPoolSizeBytes = 200 * 1024 * 1024,
        TrimIntervalMs = 60000,
        ClearOnReturn = false,
        PreAllocate = true,
        PreAllocateCount = 10,
        EnableStatistics = false
    };

    /// <summary>
    /// Low-memory configuration for resource-constrained environments.
    /// </summary>
    public static BufferPoolConfig LowMemory { get; } = new()
    {
        MaxBuffersPerBucket = 10,
        MaxPoolSizeBytes = 20 * 1024 * 1024,
        TrimIntervalMs = 10000,
        ClearOnReturn = false,
        PreAllocate = false,
        EnableStatistics = true
    };

    /// <summary>
    /// Gets the smallest standard buffer size that can accommodate the requested size.
    /// </summary>
    /// <param name="requestedSize">The minimum size needed.</param>
    /// <returns>The appropriate standard buffer size, or -1 if no standard size fits.</returns>
    public static int GetStandardSize(int requestedSize)
    {
        foreach (var size in new[] { 64, 128, 256, 512, 1024, 2048, 4096, 8192 })
        {
            if (size >= requestedSize)
                return size;
        }
        return -1; // Larger than any standard size
    }

    /// <summary>
    /// Rounds up to the next power of two, useful for non-standard buffer sizes.
    /// </summary>
    /// <param name="value">The value to round up.</param>
    /// <returns>The next power of two >= value.</returns>
    public static int RoundUpToPowerOfTwo(int value)
    {
        if (value <= 0) return 1;
        value--;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return value + 1;
    }
}
