// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

namespace MusicEngine.Infrastructure.Cloud;

/// <summary>
/// Interface for cloud storage providers.
/// Implementations can connect to various cloud services (Azure, AWS, Google, local, etc.).
/// </summary>
public interface ICloudStorageProvider
{
    /// <summary>
    /// Gets the name of this provider.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets whether this provider is currently connected and available.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Uploads a project to cloud storage.
    /// </summary>
    /// <param name="projectPath">The local path to the project file or folder.</param>
    /// <param name="metadata">The project metadata to store.</param>
    /// <param name="progress">Optional progress callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cloud project with assigned CloudId.</returns>
    Task<CloudProject> UploadProjectAsync(
        string projectPath,
        CloudProject metadata,
        IProgress<CloudProgressEventArgs>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a project from cloud storage.
    /// </summary>
    /// <param name="cloudId">The cloud identifier of the project.</param>
    /// <param name="localPath">The local path to download to.</param>
    /// <param name="progress">Optional progress callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The downloaded project metadata.</returns>
    Task<CloudProject> DownloadProjectAsync(
        string cloudId,
        string localPath,
        IProgress<CloudProgressEventArgs>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all projects available in cloud storage.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of cloud projects.</returns>
    Task<IReadOnlyList<CloudProject>> ListProjectsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a project from cloud storage.
    /// </summary>
    /// <param name="cloudId">The cloud identifier of the project to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteProjectAsync(string cloudId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the metadata for a specific project.
    /// </summary>
    /// <param name="cloudId">The cloud identifier of the project.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The project metadata, or null if not found.</returns>
    Task<CloudProject?> GetProjectMetadataAsync(string cloudId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes a project between local and cloud storage.
    /// This performs bidirectional sync, detecting changes on both sides.
    /// </summary>
    /// <param name="localPath">The local path of the project.</param>
    /// <param name="cloudId">The cloud identifier of the project.</param>
    /// <param name="conflictStrategy">The strategy to use when conflicts are detected.</param>
    /// <param name="progress">Optional progress callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The sync result indicating what action was taken.</returns>
    Task<SyncResult> SyncProjectAsync(
        string localPath,
        string cloudId,
        ConflictResolutionStrategy conflictStrategy = ConflictResolutionStrategy.Ask,
        IProgress<CloudProgressEventArgs>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a project exists in cloud storage.
    /// </summary>
    /// <param name="cloudId">The cloud identifier to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the project exists.</returns>
    Task<bool> ExistsAsync(string cloudId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the content hash for a local project.
    /// Used for detecting changes during sync.
    /// </summary>
    /// <param name="projectPath">The local path to the project.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A hash string representing the project content.</returns>
    Task<string> ComputeContentHashAsync(string projectPath, CancellationToken cancellationToken = default);
}
