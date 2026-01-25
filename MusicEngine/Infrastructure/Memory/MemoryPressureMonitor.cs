using System.Runtime;
using Microsoft.Extensions.Logging;

namespace MusicEngine.Infrastructure.Memory;

/// <summary>
/// Monitors memory pressure and responds to low memory conditions.
/// Subscribes to GC notifications and provides automatic buffer pool trimming.
/// </summary>
public sealed class MemoryPressureMonitor : IDisposable
{
    private readonly ILogger? _logger;
    private readonly List<WeakReference<OptimizedAudioBufferPool>> _pools = new();
    private readonly object _lock = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _monitorTask;
    private bool _disposed;

    /// <summary>
    /// Event raised when high memory pressure is detected.
    /// </summary>
    public event EventHandler? OnHighMemoryPressure;

    /// <summary>
    /// Event raised when memory pressure returns to normal.
    /// </summary>
    public event EventHandler? OnMemoryPressureRelieved;

    /// <summary>
    /// Gets or sets whether automatic trimming is enabled.
    /// When enabled, buffer pools are automatically trimmed on high memory pressure.
    /// Default is true.
    /// </summary>
    public bool AutoTrimEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the threshold (0.0-1.0) at which high memory pressure is detected.
    /// Default is 0.9 (90% of available memory).
    /// </summary>
    public double HighMemoryThreshold { get; set; } = 0.9;

    /// <summary>
    /// Gets or sets the interval in milliseconds for memory checks.
    /// Default is 5000ms (5 seconds).
    /// </summary>
    public int CheckIntervalMs { get; set; } = 5000;

    /// <summary>
    /// Gets whether memory pressure is currently high.
    /// </summary>
    public bool IsHighPressure { get; private set; }

    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static MemoryPressureMonitor Instance { get; } = new();

    /// <summary>
    /// Creates a new memory pressure monitor.
    /// </summary>
    public MemoryPressureMonitor() : this(null)
    {
    }

    /// <summary>
    /// Creates a new memory pressure monitor with logging.
    /// </summary>
    /// <param name="logger">Optional logger.</param>
    public MemoryPressureMonitor(ILogger? logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Starts monitoring memory pressure.
    /// </summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_monitorTask != null) return;

        // Register for GC notifications
        try
        {
            GC.RegisterForFullGCNotification(50, 50);
        }
        catch (InvalidOperationException)
        {
            // GC notifications may not be available on all platforms
            _logger?.LogWarning("GC notifications not available on this platform");
        }

        _monitorTask = Task.Run(MonitorLoop, _cts.Token);
        _logger?.LogDebug("Memory pressure monitor started");
    }

    /// <summary>
    /// Stops monitoring memory pressure.
    /// </summary>
    public void Stop()
    {
        if (_monitorTask == null) return;

        _cts.Cancel();
        try
        {
            _monitorTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Task was cancelled
        }

        try
        {
            GC.CancelFullGCNotification();
        }
        catch (InvalidOperationException)
        {
            // Ignore if not registered
        }

        _monitorTask = null;
        _logger?.LogDebug("Memory pressure monitor stopped");
    }

    /// <summary>
    /// Registers a buffer pool to be automatically trimmed on high memory pressure.
    /// </summary>
    /// <param name="pool">The pool to register.</param>
    public void RegisterPool(OptimizedAudioBufferPool pool)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (pool == null) throw new ArgumentNullException(nameof(pool));

        lock (_lock)
        {
            // Clean up dead references
            _pools.RemoveAll(wr => !wr.TryGetTarget(out _));
            _pools.Add(new WeakReference<OptimizedAudioBufferPool>(pool));
        }
    }

    /// <summary>
    /// Unregisters a buffer pool.
    /// </summary>
    /// <param name="pool">The pool to unregister.</param>
    public void UnregisterPool(OptimizedAudioBufferPool pool)
    {
        if (pool == null) return;

        lock (_lock)
        {
            _pools.RemoveAll(wr =>
            {
                if (wr.TryGetTarget(out var target))
                    return ReferenceEquals(target, pool);
                return true; // Remove dead references too
            });
        }
    }

    /// <summary>
    /// Gets current memory usage information.
    /// </summary>
    /// <returns>Memory usage information.</returns>
    public MemoryUsageInfo GetCurrentMemoryUsage()
    {
        var gcInfo = GC.GetGCMemoryInfo();
        var process = System.Diagnostics.Process.GetCurrentProcess();

        return new MemoryUsageInfo
        {
            TotalAllocatedBytes = GC.GetTotalMemory(forceFullCollection: false),
            HeapSizeBytes = gcInfo.HeapSizeBytes,
            HighMemoryLoadThresholdBytes = gcInfo.HighMemoryLoadThresholdBytes,
            TotalAvailableMemoryBytes = gcInfo.TotalAvailableMemoryBytes,
            MemoryLoadBytes = gcInfo.MemoryLoadBytes,
            WorkingSetBytes = process.WorkingSet64,
            PrivateMemoryBytes = process.PrivateMemorySize64,
            Gen0CollectionCount = GC.CollectionCount(0),
            Gen1CollectionCount = GC.CollectionCount(1),
            Gen2CollectionCount = GC.CollectionCount(2),
            MemoryPressureRatio = gcInfo.TotalAvailableMemoryBytes > 0
                ? (double)gcInfo.MemoryLoadBytes / gcInfo.TotalAvailableMemoryBytes
                : 0
        };
    }

    /// <summary>
    /// Forces a garbage collection.
    /// Use sparingly - only in response to critical memory pressure.
    /// </summary>
    /// <param name="generation">The generation to collect (0, 1, or 2).</param>
    /// <param name="blocking">Whether to block until collection completes.</param>
    /// <param name="compacting">Whether to compact the large object heap.</param>
    public void ForceGarbageCollection(int generation = 2, bool blocking = true, bool compacting = false)
    {
        _logger?.LogWarning("Forcing garbage collection (Gen{Generation}, blocking={Blocking}, compacting={Compacting})",
            generation, blocking, compacting);

        GCCollectionMode mode = blocking ? GCCollectionMode.Forced : GCCollectionMode.Optimized;
        GC.Collect(generation, mode, blocking, compacting);

        if (blocking)
        {
            GC.WaitForPendingFinalizers();
        }
    }

    /// <summary>
    /// Trims all registered pools to release unused buffers.
    /// </summary>
    public void TrimAllPools()
    {
        _logger?.LogDebug("Trimming all registered buffer pools");

        lock (_lock)
        {
            foreach (var weakRef in _pools)
            {
                if (weakRef.TryGetTarget(out var pool))
                {
                    try
                    {
                        pool.Trim();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error trimming buffer pool");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Responds to critical memory pressure by trimming pools and forcing GC.
    /// </summary>
    public void RespondToCriticalPressure()
    {
        _logger?.LogWarning("Responding to critical memory pressure");

        // First, trim all pools
        TrimAllPools();

        // Then force a full GC with compaction
        ForceGarbageCollection(generation: 2, blocking: true, compacting: true);

        // Trim pools again after GC
        TrimAllPools();
    }

    private async Task MonitorLoop()
    {
        bool wasHighPressure = false;

        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(CheckIntervalMs, _cts.Token).ConfigureAwait(false);

                var memInfo = GetCurrentMemoryUsage();
                bool isHigh = memInfo.MemoryPressureRatio >= HighMemoryThreshold;

                if (isHigh && !wasHighPressure)
                {
                    IsHighPressure = true;
                    wasHighPressure = true;
                    _logger?.LogWarning("High memory pressure detected: {Ratio:P2}", memInfo.MemoryPressureRatio);

                    OnHighMemoryPressure?.Invoke(this, EventArgs.Empty);

                    if (AutoTrimEnabled)
                    {
                        TrimAllPools();
                    }
                }
                else if (!isHigh && wasHighPressure)
                {
                    IsHighPressure = false;
                    wasHighPressure = false;
                    _logger?.LogInformation("Memory pressure relieved: {Ratio:P2}", memInfo.MemoryPressureRatio);

                    OnMemoryPressureRelieved?.Invoke(this, EventArgs.Empty);
                }

                // Check for pending GC notification
                try
                {
                    var status = GC.WaitForFullGCApproach(0);
                    if (status == GCNotificationStatus.Succeeded)
                    {
                        _logger?.LogDebug("Full GC approaching, pre-emptive pool trim");
                        if (AutoTrimEnabled)
                        {
                            TrimAllPools();
                        }
                    }

                    status = GC.WaitForFullGCComplete(0);
                    if (status == GCNotificationStatus.Succeeded)
                    {
                        _logger?.LogDebug("Full GC completed");
                    }
                }
                catch (InvalidOperationException)
                {
                    // GC notifications not available
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in memory pressure monitor loop");
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
        _cts.Dispose();

        lock (_lock)
        {
            _pools.Clear();
        }
    }
}

/// <summary>
/// Contains information about current memory usage.
/// </summary>
public readonly struct MemoryUsageInfo
{
    /// <summary>
    /// Total bytes allocated on the managed heap.
    /// </summary>
    public long TotalAllocatedBytes { get; init; }

    /// <summary>
    /// Size of the managed heap in bytes.
    /// </summary>
    public long HeapSizeBytes { get; init; }

    /// <summary>
    /// Threshold at which memory load is considered high.
    /// </summary>
    public long HighMemoryLoadThresholdBytes { get; init; }

    /// <summary>
    /// Total memory available to the process.
    /// </summary>
    public long TotalAvailableMemoryBytes { get; init; }

    /// <summary>
    /// Current memory load in bytes.
    /// </summary>
    public long MemoryLoadBytes { get; init; }

    /// <summary>
    /// Process working set size in bytes.
    /// </summary>
    public long WorkingSetBytes { get; init; }

    /// <summary>
    /// Process private memory size in bytes.
    /// </summary>
    public long PrivateMemoryBytes { get; init; }

    /// <summary>
    /// Number of Gen 0 collections.
    /// </summary>
    public int Gen0CollectionCount { get; init; }

    /// <summary>
    /// Number of Gen 1 collections.
    /// </summary>
    public int Gen1CollectionCount { get; init; }

    /// <summary>
    /// Number of Gen 2 collections.
    /// </summary>
    public int Gen2CollectionCount { get; init; }

    /// <summary>
    /// Memory pressure as a ratio (0.0 to 1.0+).
    /// </summary>
    public double MemoryPressureRatio { get; init; }

    /// <summary>
    /// Returns a string representation of the memory usage info.
    /// </summary>
    public override string ToString()
    {
        return $"Memory: {TotalAllocatedBytes / (1024.0 * 1024):F1}MB allocated, " +
               $"Heap: {HeapSizeBytes / (1024.0 * 1024):F1}MB, " +
               $"Working Set: {WorkingSetBytes / (1024.0 * 1024):F1}MB, " +
               $"Pressure: {MemoryPressureRatio:P1}, " +
               $"GC: {Gen0CollectionCount}/{Gen1CollectionCount}/{Gen2CollectionCount}";
    }
}
