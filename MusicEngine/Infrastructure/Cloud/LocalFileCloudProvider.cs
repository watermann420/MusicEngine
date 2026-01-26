// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MusicEngine.Infrastructure.Cloud;

/// <summary>
/// A local file-based implementation of ICloudStorageProvider.
/// Useful for testing and offline mode.
/// </summary>
public class LocalFileCloudProvider : ICloudStorageProvider
{
    private readonly string _cloudFolder;
    private readonly string _metadataFolder;
    private readonly ILogger<LocalFileCloudProvider>? _logger;
    private readonly bool _simulateNetworkDelay;
    private readonly int _minDelayMs;
    private readonly int _maxDelayMs;
    private readonly Random _random = new();

    private const string MetadataFileName = "metadata.json";
    private const string ProjectDataFileName = "project.zip";

    /// <summary>
    /// Gets the name of this provider.
    /// </summary>
    public string ProviderName => "LocalFile";

    /// <summary>
    /// Gets whether this provider is available (always true for local).
    /// </summary>
    public bool IsAvailable => Directory.Exists(_cloudFolder);

    /// <summary>
    /// Creates a new instance of LocalFileCloudProvider.
    /// </summary>
    /// <param name="cloudFolder">The folder to use as "cloud" storage.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="simulateNetworkDelay">Whether to simulate network delays.</param>
    /// <param name="minDelayMs">Minimum simulated delay in milliseconds.</param>
    /// <param name="maxDelayMs">Maximum simulated delay in milliseconds.</param>
    public LocalFileCloudProvider(
        string cloudFolder,
        ILogger<LocalFileCloudProvider>? logger = null,
        bool simulateNetworkDelay = false,
        int minDelayMs = 100,
        int maxDelayMs = 500)
    {
        _cloudFolder = cloudFolder;
        _metadataFolder = Path.Combine(cloudFolder, ".metadata");
        _logger = logger;
        _simulateNetworkDelay = simulateNetworkDelay;
        _minDelayMs = minDelayMs;
        _maxDelayMs = maxDelayMs;

        EnsureDirectoriesExist();
    }

    /// <summary>
    /// Uploads a project to the local "cloud" storage.
    /// </summary>
    public async Task<CloudProject> UploadProjectAsync(
        string projectPath,
        CloudProject metadata,
        IProgress<CloudProgressEventArgs>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Uploading project from {ProjectPath}", projectPath);

        await SimulateNetworkDelayAsync(cancellationToken);

        if (!File.Exists(projectPath) && !Directory.Exists(projectPath))
        {
            throw new FileNotFoundException("Project path not found", projectPath);
        }

        // Generate CloudId if not set
        if (string.IsNullOrEmpty(metadata.CloudId))
        {
            metadata.CloudId = Guid.NewGuid().ToString();
        }

        var projectFolder = GetProjectFolder(metadata.CloudId);
        Directory.CreateDirectory(projectFolder);

        var projectDataPath = Path.Combine(projectFolder, ProjectDataFileName);

        // Report progress start
        ReportProgress(progress, CloudOperation.Upload, metadata.Name, 0, 0, 0);

        // Copy project data
        var fileInfo = new FileInfo(projectPath);
        long totalBytes = fileInfo.Exists ? fileInfo.Length : GetDirectorySize(projectPath);

        if (File.Exists(projectPath))
        {
            await CopyFileWithProgressAsync(projectPath, projectDataPath, progress, metadata.Name, totalBytes, cancellationToken);
        }
        else if (Directory.Exists(projectPath))
        {
            await ZipDirectoryWithProgressAsync(projectPath, projectDataPath, progress, metadata.Name, cancellationToken);
        }

        // Update metadata
        metadata.Size = new FileInfo(projectDataPath).Length;
        metadata.ModifiedAt = DateTime.UtcNow;
        metadata.ContentHash = await ComputeContentHashAsync(projectDataPath, cancellationToken);
        metadata.LocalPath = projectPath;
        metadata.LastSyncedAt = DateTime.UtcNow;
        metadata.HasLocalChanges = false;
        metadata.HasRemoteChanges = false;

        // Save metadata
        await SaveMetadataAsync(metadata, cancellationToken);

        // Report progress complete
        ReportProgress(progress, CloudOperation.Upload, metadata.Name, 100, metadata.Size, metadata.Size, true);

        _logger?.LogInformation("Project {ProjectName} uploaded with CloudId {CloudId}", metadata.Name, metadata.CloudId);

        return metadata;
    }

    /// <summary>
    /// Downloads a project from the local "cloud" storage.
    /// </summary>
    public async Task<CloudProject> DownloadProjectAsync(
        string cloudId,
        string localPath,
        IProgress<CloudProgressEventArgs>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Downloading project {CloudId} to {LocalPath}", cloudId, localPath);

        await SimulateNetworkDelayAsync(cancellationToken);

        var metadata = await GetProjectMetadataAsync(cloudId, cancellationToken);
        if (metadata == null)
        {
            throw new InvalidOperationException($"Project with CloudId {cloudId} not found");
        }

        var projectFolder = GetProjectFolder(cloudId);
        var projectDataPath = Path.Combine(projectFolder, ProjectDataFileName);

        if (!File.Exists(projectDataPath))
        {
            throw new FileNotFoundException("Project data not found in cloud storage", projectDataPath);
        }

        var fileInfo = new FileInfo(projectDataPath);

        // Report progress start
        ReportProgress(progress, CloudOperation.Download, metadata.Name, 0, 0, fileInfo.Length);

        // If project data is a zip, extract it
        if (Path.GetExtension(projectDataPath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            Directory.CreateDirectory(localPath);
            await ExtractZipWithProgressAsync(projectDataPath, localPath, progress, metadata.Name, cancellationToken);
        }
        else
        {
            await CopyFileWithProgressAsync(projectDataPath, localPath, progress, metadata.Name, fileInfo.Length, cancellationToken);
        }

        // Update metadata
        metadata.LocalPath = localPath;
        metadata.LastSyncedAt = DateTime.UtcNow;
        metadata.HasLocalChanges = false;
        metadata.HasRemoteChanges = false;

        await SaveMetadataAsync(metadata, cancellationToken);

        // Report progress complete
        ReportProgress(progress, CloudOperation.Download, metadata.Name, 100, fileInfo.Length, fileInfo.Length, true);

        _logger?.LogInformation("Project {ProjectName} downloaded to {LocalPath}", metadata.Name, localPath);

        return metadata;
    }

    /// <summary>
    /// Lists all projects in the local "cloud" storage.
    /// </summary>
    public async Task<IReadOnlyList<CloudProject>> ListProjectsAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Listing all cloud projects");

        await SimulateNetworkDelayAsync(cancellationToken);

        var projects = new List<CloudProject>();

        if (!Directory.Exists(_metadataFolder))
        {
            return projects;
        }

        var metadataFiles = Directory.GetFiles(_metadataFolder, "*.json");

        foreach (var metadataFile in metadataFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var json = await File.ReadAllTextAsync(metadataFile, cancellationToken);
                var metadata = JsonSerializer.Deserialize<CloudProject>(json);
                if (metadata != null)
                {
                    projects.Add(metadata);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to read metadata file {File}", metadataFile);
            }
        }

        _logger?.LogDebug("Found {Count} cloud projects", projects.Count);

        return projects;
    }

    /// <summary>
    /// Deletes a project from the local "cloud" storage.
    /// </summary>
    public async Task DeleteProjectAsync(string cloudId, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Deleting project {CloudId}", cloudId);

        await SimulateNetworkDelayAsync(cancellationToken);

        var projectFolder = GetProjectFolder(cloudId);
        if (Directory.Exists(projectFolder))
        {
            Directory.Delete(projectFolder, recursive: true);
        }

        var metadataPath = GetMetadataPath(cloudId);
        if (File.Exists(metadataPath))
        {
            File.Delete(metadataPath);
        }

        _logger?.LogInformation("Project {CloudId} deleted", cloudId);
    }

    /// <summary>
    /// Gets the metadata for a specific project.
    /// </summary>
    public async Task<CloudProject?> GetProjectMetadataAsync(string cloudId, CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Getting metadata for project {CloudId}", cloudId);

        await SimulateNetworkDelayAsync(cancellationToken);

        var metadataPath = GetMetadataPath(cloudId);
        if (!File.Exists(metadataPath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(metadataPath, cancellationToken);
        return JsonSerializer.Deserialize<CloudProject>(json);
    }

    /// <summary>
    /// Synchronizes a project between local and cloud storage.
    /// </summary>
    public async Task<SyncResult> SyncProjectAsync(
        string localPath,
        string cloudId,
        ConflictResolutionStrategy conflictStrategy = ConflictResolutionStrategy.Ask,
        IProgress<CloudProgressEventArgs>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Syncing project {CloudId} with local path {LocalPath}", cloudId, localPath);

        await SimulateNetworkDelayAsync(cancellationToken);

        var cloudMetadata = await GetProjectMetadataAsync(cloudId, cancellationToken);
        if (cloudMetadata == null)
        {
            return SyncResult.Failed($"Project with CloudId {cloudId} not found in cloud");
        }

        // Check if local file/folder exists
        bool localExists = File.Exists(localPath) || Directory.Exists(localPath);
        if (!localExists)
        {
            // Download from cloud
            var downloaded = await DownloadProjectAsync(cloudId, localPath, progress, cancellationToken);
            return SyncResult.Successful(downloaded, SyncAction.Downloaded);
        }

        // Compute local hash
        var localHash = await ComputeContentHashAsync(localPath, cancellationToken);

        // Check for changes
        bool localChanged = localHash != cloudMetadata.ContentHash;
        bool remoteChanged = cloudMetadata.HasRemoteChanges;

        if (!localChanged && !remoteChanged)
        {
            // No changes
            _logger?.LogDebug("Project {CloudId} is already in sync", cloudId);
            return SyncResult.Successful(cloudMetadata, SyncAction.None);
        }

        if (localChanged && remoteChanged)
        {
            // Conflict detected
            _logger?.LogWarning("Conflict detected for project {CloudId}", cloudId);

            switch (conflictStrategy)
            {
                case ConflictResolutionStrategy.LocalWins:
                    var uploadedLocal = await UploadProjectAsync(localPath, cloudMetadata, progress, cancellationToken);
                    return SyncResult.Successful(uploadedLocal, SyncAction.Uploaded);

                case ConflictResolutionStrategy.RemoteWins:
                    var downloadedRemote = await DownloadProjectAsync(cloudId, localPath, progress, cancellationToken);
                    return SyncResult.Successful(downloadedRemote, SyncAction.Downloaded);

                case ConflictResolutionStrategy.Merge:
                    // For a simple file-based system, merge is not straightforward
                    // Return conflict for the caller to handle
                    return SyncResult.Conflict(cloudMetadata);

                case ConflictResolutionStrategy.Ask:
                default:
                    return SyncResult.Conflict(cloudMetadata);
            }
        }

        if (localChanged)
        {
            // Upload local changes
            var uploaded = await UploadProjectAsync(localPath, cloudMetadata, progress, cancellationToken);
            return SyncResult.Successful(uploaded, SyncAction.Uploaded);
        }

        // Remote changed - download
        var downloadedLatest = await DownloadProjectAsync(cloudId, localPath, progress, cancellationToken);
        return SyncResult.Successful(downloadedLatest, SyncAction.Downloaded);
    }

    /// <summary>
    /// Checks if a project exists in cloud storage.
    /// </summary>
    public async Task<bool> ExistsAsync(string cloudId, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkDelayAsync(cancellationToken);

        var metadataPath = GetMetadataPath(cloudId);
        return File.Exists(metadataPath);
    }

    /// <summary>
    /// Computes the content hash for a local project.
    /// </summary>
    public async Task<string> ComputeContentHashAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        using var sha256 = SHA256.Create();

        if (File.Exists(projectPath))
        {
            await using var stream = File.OpenRead(projectPath);
            var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
            return Convert.ToBase64String(hash);
        }

        if (Directory.Exists(projectPath))
        {
            // For directories, hash all files combined
            var files = Directory.GetFiles(projectPath, "*", SearchOption.AllDirectories)
                .OrderBy(f => f)
                .ToList();

            using var combinedStream = new MemoryStream();
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var relativePath = Path.GetRelativePath(projectPath, file);
                var pathBytes = System.Text.Encoding.UTF8.GetBytes(relativePath);
                await combinedStream.WriteAsync(pathBytes, cancellationToken);

                var fileBytes = await File.ReadAllBytesAsync(file, cancellationToken);
                await combinedStream.WriteAsync(fileBytes, cancellationToken);
            }

            combinedStream.Position = 0;
            var hash = await sha256.ComputeHashAsync(combinedStream, cancellationToken);
            return Convert.ToBase64String(hash);
        }

        throw new FileNotFoundException("Project path not found", projectPath);
    }

    private void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(_cloudFolder);
        Directory.CreateDirectory(_metadataFolder);
    }

    private string GetProjectFolder(string cloudId)
        => Path.Combine(_cloudFolder, cloudId);

    private string GetMetadataPath(string cloudId)
        => Path.Combine(_metadataFolder, $"{cloudId}.json");

    private async Task SaveMetadataAsync(CloudProject metadata, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_metadataFolder);
        var metadataPath = GetMetadataPath(metadata.CloudId);
        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(metadataPath, json, cancellationToken);
    }

    private async Task SimulateNetworkDelayAsync(CancellationToken cancellationToken)
    {
        if (_simulateNetworkDelay)
        {
            var delay = _random.Next(_minDelayMs, _maxDelayMs);
            await Task.Delay(delay, cancellationToken);
        }
    }

    private void ReportProgress(
        IProgress<CloudProgressEventArgs>? progress,
        CloudOperation operation,
        string projectName,
        int percent,
        long bytesTransferred,
        long totalBytes,
        bool isComplete = false,
        string? errorMessage = null)
    {
        progress?.Report(new CloudProgressEventArgs
        {
            Operation = operation,
            ProjectName = projectName,
            ProgressPercent = percent,
            BytesTransferred = bytesTransferred,
            TotalBytes = totalBytes,
            IsComplete = isComplete,
            ErrorMessage = errorMessage
        });
    }

    private async Task CopyFileWithProgressAsync(
        string sourcePath,
        string destPath,
        IProgress<CloudProgressEventArgs>? progress,
        string projectName,
        long totalBytes,
        CancellationToken cancellationToken)
    {
        const int bufferSize = 81920; // 80 KB
        var buffer = new byte[bufferSize];
        long bytesTransferred = 0;

        await using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true);
        await using var destStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, useAsync: true);

        int bytesRead;
        while ((bytesRead = await sourceStream.ReadAsync(buffer.AsMemory(0, bufferSize), cancellationToken)) > 0)
        {
            await destStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            bytesTransferred += bytesRead;

            var percent = totalBytes > 0 ? (int)((bytesTransferred * 100) / totalBytes) : 0;
            ReportProgress(progress, CloudOperation.Upload, projectName, percent, bytesTransferred, totalBytes);
        }
    }

    private async Task ZipDirectoryWithProgressAsync(
        string sourceDir,
        string destZipPath,
        IProgress<CloudProgressEventArgs>? progress,
        string projectName,
        CancellationToken cancellationToken)
    {
        // Use System.IO.Compression for zipping
        await Task.Run(() =>
        {
            System.IO.Compression.ZipFile.CreateFromDirectory(sourceDir, destZipPath);
        }, cancellationToken);

        var fileInfo = new FileInfo(destZipPath);
        ReportProgress(progress, CloudOperation.Upload, projectName, 100, fileInfo.Length, fileInfo.Length);
    }

    private async Task ExtractZipWithProgressAsync(
        string sourceZipPath,
        string destDir,
        IProgress<CloudProgressEventArgs>? progress,
        string projectName,
        CancellationToken cancellationToken)
    {
        var fileInfo = new FileInfo(sourceZipPath);
        ReportProgress(progress, CloudOperation.Download, projectName, 50, fileInfo.Length / 2, fileInfo.Length);

        await Task.Run(() =>
        {
            System.IO.Compression.ZipFile.ExtractToDirectory(sourceZipPath, destDir, overwriteFiles: true);
        }, cancellationToken);

        ReportProgress(progress, CloudOperation.Download, projectName, 100, fileInfo.Length, fileInfo.Length);
    }

    private long GetDirectorySize(string path)
    {
        if (!Directory.Exists(path))
            return 0;

        return Directory.GetFiles(path, "*", SearchOption.AllDirectories)
            .Sum(file => new FileInfo(file).Length);
    }
}
