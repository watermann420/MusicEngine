// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MusicEngine.Infrastructure.Cloud;

/// <summary>
/// Main service for managing cloud storage providers and project synchronization.
/// </summary>
public class CloudStorageService : IDisposable
{
    private readonly ConcurrentDictionary<string, ICloudStorageProvider> _providers = new();
    private readonly ConcurrentQueue<PendingCloudOperation> _offlineQueue = new();
    private readonly ILogger<CloudStorageService>? _logger;
    private readonly string _queuePersistPath;
    private readonly object _syncLock = new();
    private readonly CancellationTokenSource _autoSyncCts = new();
    private Task? _autoSyncTask;

    private ICloudStorageProvider? _activeProvider;
    private bool _autoSyncEnabled;
    private TimeSpan _autoSyncInterval = TimeSpan.FromMinutes(5);
    private bool _disposed;

    /// <summary>
    /// Event raised when progress is made on a cloud operation.
    /// </summary>
    public event EventHandler<CloudProgressEventArgs>? ProgressChanged;

    /// <summary>
    /// Event raised when a conflict is detected during sync.
    /// </summary>
    public event EventHandler<ConflictEventArgs>? ConflictDetected;

    /// <summary>
    /// Event raised when an offline operation is queued.
    /// </summary>
    public event EventHandler<PendingCloudOperation>? OperationQueued;

    /// <summary>
    /// Event raised when the online status changes.
    /// </summary>
    public event EventHandler<bool>? OnlineStatusChanged;

    /// <summary>
    /// Gets the currently active provider.
    /// </summary>
    public ICloudStorageProvider? ActiveProvider => _activeProvider;

    /// <summary>
    /// Gets the name of the active provider.
    /// </summary>
    public string? ActiveProviderName => _activeProvider?.ProviderName;

    /// <summary>
    /// Gets or sets whether auto-sync is enabled.
    /// </summary>
    public bool AutoSyncEnabled
    {
        get => _autoSyncEnabled;
        set
        {
            if (_autoSyncEnabled == value) return;
            _autoSyncEnabled = value;

            if (_autoSyncEnabled)
            {
                StartAutoSync();
            }
            else
            {
                StopAutoSync();
            }
        }
    }

    /// <summary>
    /// Gets or sets the auto-sync interval.
    /// </summary>
    public TimeSpan AutoSyncInterval
    {
        get => _autoSyncInterval;
        set
        {
            if (value < TimeSpan.FromSeconds(30))
            {
                throw new ArgumentException("Auto-sync interval must be at least 30 seconds");
            }
            _autoSyncInterval = value;
        }
    }

    /// <summary>
    /// Gets or sets the conflict resolution strategy.
    /// </summary>
    public ConflictResolutionStrategy ConflictStrategy { get; set; } = ConflictResolutionStrategy.Ask;

    /// <summary>
    /// Gets the list of registered provider names.
    /// </summary>
    public IReadOnlyList<string> RegisteredProviders => _providers.Keys.ToList();

    /// <summary>
    /// Gets the pending operations in the offline queue.
    /// </summary>
    public IReadOnlyList<PendingCloudOperation> PendingOperations => _offlineQueue.ToList();

    /// <summary>
    /// Gets whether the service is currently online (active provider is available).
    /// </summary>
    public bool IsOnline => _activeProvider?.IsAvailable ?? false;

    /// <summary>
    /// Creates a new instance of CloudStorageService.
    /// </summary>
    /// <param name="logger">Optional logger.</param>
    /// <param name="queuePersistPath">Path to persist the offline queue.</param>
    public CloudStorageService(
        ILogger<CloudStorageService>? logger = null,
        string? queuePersistPath = null)
    {
        _logger = logger;
        _queuePersistPath = queuePersistPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MusicEngine",
            "cloud_queue.json");

        LoadOfflineQueue();
    }

    /// <summary>
    /// Registers a cloud storage provider.
    /// </summary>
    /// <param name="name">The name to register the provider under.</param>
    /// <param name="provider">The provider instance.</param>
    public void RegisterProvider(string name, ICloudStorageProvider provider)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Provider name cannot be empty", nameof(name));
        }

        if (provider == null)
        {
            throw new ArgumentNullException(nameof(provider));
        }

        _providers[name] = provider;
        _logger?.LogInformation("Registered cloud provider: {ProviderName}", name);

        // If no active provider, set this one as active
        if (_activeProvider == null)
        {
            SetActiveProvider(name);
        }
    }

    /// <summary>
    /// Unregisters a cloud storage provider.
    /// </summary>
    /// <param name="name">The name of the provider to unregister.</param>
    public void UnregisterProvider(string name)
    {
        if (_providers.TryRemove(name, out _))
        {
            _logger?.LogInformation("Unregistered cloud provider: {ProviderName}", name);

            if (_activeProvider?.ProviderName == name)
            {
                _activeProvider = _providers.Values.FirstOrDefault();
            }
        }
    }

    /// <summary>
    /// Sets the active cloud storage provider.
    /// </summary>
    /// <param name="name">The name of the provider to activate.</param>
    public void SetActiveProvider(string name)
    {
        if (!_providers.TryGetValue(name, out var provider))
        {
            throw new InvalidOperationException($"Provider '{name}' is not registered");
        }

        var wasOnline = IsOnline;
        _activeProvider = provider;
        _logger?.LogInformation("Active cloud provider set to: {ProviderName}", name);

        if (wasOnline != IsOnline)
        {
            OnlineStatusChanged?.Invoke(this, IsOnline);
        }
    }

    /// <summary>
    /// Gets a registered provider by name.
    /// </summary>
    public ICloudStorageProvider? GetProvider(string name)
    {
        _providers.TryGetValue(name, out var provider);
        return provider;
    }

    /// <summary>
    /// Uploads a project to cloud storage.
    /// </summary>
    public async Task<CloudProject> UploadProjectAsync(
        string projectPath,
        CloudProject metadata,
        CancellationToken cancellationToken = default)
    {
        EnsureActiveProvider();

        if (!_activeProvider!.IsAvailable)
        {
            return await QueueOperationAsync(CloudOperation.Upload, projectPath, null, metadata, cancellationToken);
        }

        var progress = new Progress<CloudProgressEventArgs>(e => ProgressChanged?.Invoke(this, e));

        try
        {
            return await _activeProvider.UploadProjectAsync(projectPath, metadata, progress, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to upload project, queueing for later");
            return await QueueOperationAsync(CloudOperation.Upload, projectPath, null, metadata, cancellationToken);
        }
    }

    /// <summary>
    /// Downloads a project from cloud storage.
    /// </summary>
    public async Task<CloudProject> DownloadProjectAsync(
        string cloudId,
        string localPath,
        CancellationToken cancellationToken = default)
    {
        EnsureActiveProvider();

        if (!_activeProvider!.IsAvailable)
        {
            throw new InvalidOperationException("Cloud storage is not available. Cannot download while offline.");
        }

        var progress = new Progress<CloudProgressEventArgs>(e => ProgressChanged?.Invoke(this, e));

        return await _activeProvider.DownloadProjectAsync(cloudId, localPath, progress, cancellationToken);
    }

    /// <summary>
    /// Lists all projects in cloud storage.
    /// </summary>
    public async Task<IReadOnlyList<CloudProject>> ListProjectsAsync(CancellationToken cancellationToken = default)
    {
        EnsureActiveProvider();

        if (!_activeProvider!.IsAvailable)
        {
            _logger?.LogWarning("Cloud storage is not available, returning empty list");
            return Array.Empty<CloudProject>();
        }

        return await _activeProvider.ListProjectsAsync(cancellationToken);
    }

    /// <summary>
    /// Deletes a project from cloud storage.
    /// </summary>
    public async Task DeleteProjectAsync(string cloudId, CancellationToken cancellationToken = default)
    {
        EnsureActiveProvider();

        if (!_activeProvider!.IsAvailable)
        {
            await QueueOperationAsync(CloudOperation.Delete, null, cloudId, null, cancellationToken);
            return;
        }

        await _activeProvider.DeleteProjectAsync(cloudId, cancellationToken);
    }

    /// <summary>
    /// Gets the metadata for a specific project.
    /// </summary>
    public async Task<CloudProject?> GetProjectMetadataAsync(string cloudId, CancellationToken cancellationToken = default)
    {
        EnsureActiveProvider();

        if (!_activeProvider!.IsAvailable)
        {
            return null;
        }

        return await _activeProvider.GetProjectMetadataAsync(cloudId, cancellationToken);
    }

    /// <summary>
    /// Synchronizes a project between local and cloud storage.
    /// </summary>
    public async Task<SyncResult> SyncProjectAsync(
        string localPath,
        string cloudId,
        CancellationToken cancellationToken = default)
    {
        EnsureActiveProvider();

        if (!_activeProvider!.IsAvailable)
        {
            await QueueOperationAsync(CloudOperation.Sync, localPath, cloudId, null, cancellationToken);
            return SyncResult.Failed("Cloud storage is not available. Operation queued for later.");
        }

        var progress = new Progress<CloudProgressEventArgs>(e => ProgressChanged?.Invoke(this, e));

        var result = await _activeProvider.SyncProjectAsync(localPath, cloudId, ConflictStrategy, progress, cancellationToken);

        if (result.ConflictDetected && ConflictStrategy == ConflictResolutionStrategy.Ask)
        {
            // Raise event for UI to handle conflict
            var conflictArgs = new ConflictEventArgs
            {
                LocalPath = localPath,
                CloudId = cloudId,
                Project = result.Project
            };
            ConflictDetected?.Invoke(this, conflictArgs);

            // If UI resolved the conflict, sync again with the chosen strategy
            if (conflictArgs.Resolution != ConflictResolutionStrategy.Ask)
            {
                result = await _activeProvider.SyncProjectAsync(localPath, cloudId, conflictArgs.Resolution, progress, cancellationToken);
            }
        }

        return result;
    }

    /// <summary>
    /// Synchronizes all tracked projects.
    /// </summary>
    public async Task<IReadOnlyList<SyncResult>> SyncAllProjectsAsync(CancellationToken cancellationToken = default)
    {
        EnsureActiveProvider();

        var results = new List<SyncResult>();
        var projects = await ListProjectsAsync(cancellationToken);

        foreach (var project in projects)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            if (!string.IsNullOrEmpty(project.LocalPath) && !string.IsNullOrEmpty(project.CloudId))
            {
                try
                {
                    var result = await SyncProjectAsync(project.LocalPath, project.CloudId, cancellationToken);
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to sync project {CloudId}", project.CloudId);
                    results.Add(SyncResult.Failed($"Failed to sync: {ex.Message}"));
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Processes the offline queue, attempting to complete pending operations.
    /// </summary>
    public async Task ProcessOfflineQueueAsync(CancellationToken cancellationToken = default)
    {
        if (!IsOnline)
        {
            _logger?.LogDebug("Cannot process offline queue: not online");
            return;
        }

        var processedCount = 0;
        var failedOperations = new List<PendingCloudOperation>();

        while (_offlineQueue.TryDequeue(out var operation))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await ExecutePendingOperationAsync(operation, cancellationToken);
                processedCount++;
                _logger?.LogInformation("Processed pending operation {OperationId}", operation.Id);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to process pending operation {OperationId}", operation.Id);

                operation.RetryCount++;
                if (operation.RetryCount < operation.MaxRetries)
                {
                    failedOperations.Add(operation);
                }
                else
                {
                    _logger?.LogError("Pending operation {OperationId} exceeded max retries", operation.Id);
                }
            }
        }

        // Re-queue failed operations
        foreach (var failed in failedOperations)
        {
            _offlineQueue.Enqueue(failed);
        }

        SaveOfflineQueue();

        _logger?.LogInformation("Processed {Count} pending operations, {Failed} re-queued",
            processedCount, failedOperations.Count);
    }

    /// <summary>
    /// Clears all pending operations from the offline queue.
    /// </summary>
    public void ClearOfflineQueue()
    {
        while (_offlineQueue.TryDequeue(out _)) { }
        SaveOfflineQueue();
        _logger?.LogInformation("Offline queue cleared");
    }

    private void EnsureActiveProvider()
    {
        if (_activeProvider == null)
        {
            throw new InvalidOperationException("No active cloud storage provider. Register and activate a provider first.");
        }
    }

    private async Task<CloudProject> QueueOperationAsync(
        CloudOperation operation,
        string? localPath,
        string? cloudId,
        CloudProject? metadata,
        CancellationToken cancellationToken)
    {
        var pending = new PendingCloudOperation
        {
            Operation = operation,
            LocalPath = localPath,
            CloudId = cloudId,
            Metadata = metadata
        };

        _offlineQueue.Enqueue(pending);
        SaveOfflineQueue();

        _logger?.LogInformation("Queued {Operation} operation for offline processing", operation);
        OperationQueued?.Invoke(this, pending);

        // Return metadata with a placeholder CloudId if needed
        if (metadata != null)
        {
            if (string.IsNullOrEmpty(metadata.CloudId))
            {
                metadata.CloudId = $"pending_{pending.Id}";
            }
            metadata.HasLocalChanges = true;
            return metadata;
        }

        return new CloudProject { CloudId = cloudId ?? $"pending_{pending.Id}" };
    }

    private async Task ExecutePendingOperationAsync(PendingCloudOperation operation, CancellationToken cancellationToken)
    {
        var progress = new Progress<CloudProgressEventArgs>(e => ProgressChanged?.Invoke(this, e));

        switch (operation.Operation)
        {
            case CloudOperation.Upload when operation.LocalPath != null && operation.Metadata != null:
                await _activeProvider!.UploadProjectAsync(operation.LocalPath, operation.Metadata, progress, cancellationToken);
                break;

            case CloudOperation.Download when operation.CloudId != null && operation.LocalPath != null:
                await _activeProvider!.DownloadProjectAsync(operation.CloudId, operation.LocalPath, progress, cancellationToken);
                break;

            case CloudOperation.Delete when operation.CloudId != null:
                await _activeProvider!.DeleteProjectAsync(operation.CloudId, cancellationToken);
                break;

            case CloudOperation.Sync when operation.LocalPath != null && operation.CloudId != null:
                await _activeProvider!.SyncProjectAsync(operation.LocalPath, operation.CloudId, ConflictStrategy, progress, cancellationToken);
                break;

            default:
                _logger?.LogWarning("Cannot execute pending operation: missing required parameters");
                break;
        }
    }

    private void StartAutoSync()
    {
        if (_autoSyncTask != null && !_autoSyncTask.IsCompleted)
        {
            return;
        }

        _autoSyncTask = Task.Run(async () =>
        {
            while (!_autoSyncCts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_autoSyncInterval, _autoSyncCts.Token);

                    if (IsOnline)
                    {
                        _logger?.LogDebug("Auto-sync triggered");
                        await ProcessOfflineQueueAsync(_autoSyncCts.Token);
                        await SyncAllProjectsAsync(_autoSyncCts.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Auto-sync failed");
                }
            }
        }, _autoSyncCts.Token);

        _logger?.LogInformation("Auto-sync started with interval {Interval}", _autoSyncInterval);
    }

    private void StopAutoSync()
    {
        _autoSyncCts.Cancel();
        _autoSyncTask = null;
        _logger?.LogInformation("Auto-sync stopped");
    }

    private void LoadOfflineQueue()
    {
        try
        {
            if (File.Exists(_queuePersistPath))
            {
                var json = File.ReadAllText(_queuePersistPath);
                var operations = JsonSerializer.Deserialize<List<PendingCloudOperation>>(json);
                if (operations != null)
                {
                    foreach (var op in operations)
                    {
                        _offlineQueue.Enqueue(op);
                    }
                    _logger?.LogInformation("Loaded {Count} pending operations from queue", operations.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load offline queue");
        }
    }

    private void SaveOfflineQueue()
    {
        try
        {
            var dir = Path.GetDirectoryName(_queuePersistPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var operations = _offlineQueue.ToList();
            var json = JsonSerializer.Serialize(operations, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_queuePersistPath, json);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to save offline queue");
        }
    }

    /// <summary>
    /// Disposes resources used by the service.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        StopAutoSync();
        _autoSyncCts.Dispose();
        SaveOfflineQueue();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Event args for conflict detection.
/// </summary>
public class ConflictEventArgs : EventArgs
{
    /// <summary>
    /// Gets or sets the local path of the conflicted project.
    /// </summary>
    public string LocalPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the cloud ID of the conflicted project.
    /// </summary>
    public string CloudId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the project metadata.
    /// </summary>
    public CloudProject? Project { get; set; }

    /// <summary>
    /// Gets or sets the resolution chosen by the user.
    /// Set this to indicate how to resolve the conflict.
    /// </summary>
    public ConflictResolutionStrategy Resolution { get; set; } = ConflictResolutionStrategy.Ask;
}
