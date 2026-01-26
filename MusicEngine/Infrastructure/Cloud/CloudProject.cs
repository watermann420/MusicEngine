// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

namespace MusicEngine.Infrastructure.Cloud;

/// <summary>
/// Represents metadata for a project stored in cloud storage.
/// </summary>
public class CloudProject
{
    /// <summary>
    /// Gets or sets the unique cloud identifier for the project.
    /// </summary>
    public string CloudId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the project name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the project description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the project was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets when the project was last modified.
    /// </summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the project version number.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Gets or sets the project size in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Gets or sets the owner of the project.
    /// </summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of users this project is shared with.
    /// </summary>
    public List<string> SharedWith { get; set; } = new();

    /// <summary>
    /// Gets or sets the tags associated with the project.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Gets or sets the thumbnail image as base64-encoded data.
    /// </summary>
    public string? Thumbnail { get; set; }

    /// <summary>
    /// Gets or sets the local file path associated with this cloud project.
    /// </summary>
    public string? LocalPath { get; set; }

    /// <summary>
    /// Gets or sets the hash of the project content for sync detection.
    /// </summary>
    public string? ContentHash { get; set; }

    /// <summary>
    /// Gets or sets whether this project has unsynchronized local changes.
    /// </summary>
    public bool HasLocalChanges { get; set; }

    /// <summary>
    /// Gets or sets whether this project has unsynchronized remote changes.
    /// </summary>
    public bool HasRemoteChanges { get; set; }

    /// <summary>
    /// Gets or sets the last sync timestamp.
    /// </summary>
    public DateTime? LastSyncedAt { get; set; }
}

/// <summary>
/// Represents the result of a sync operation.
/// </summary>
public class SyncResult
{
    /// <summary>
    /// Gets or sets whether the sync was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the sync action that was performed.
    /// </summary>
    public SyncAction Action { get; set; }

    /// <summary>
    /// Gets or sets the updated project metadata after sync.
    /// </summary>
    public CloudProject? Project { get; set; }

    /// <summary>
    /// Gets or sets the error message if sync failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets whether a conflict was detected.
    /// </summary>
    public bool ConflictDetected { get; set; }

    /// <summary>
    /// Creates a successful sync result.
    /// </summary>
    public static SyncResult Successful(CloudProject project, SyncAction action)
        => new() { Success = true, Project = project, Action = action };

    /// <summary>
    /// Creates a failed sync result.
    /// </summary>
    public static SyncResult Failed(string errorMessage)
        => new() { Success = false, ErrorMessage = errorMessage };

    /// <summary>
    /// Creates a conflict sync result.
    /// </summary>
    public static SyncResult Conflict(CloudProject project)
        => new() { Success = false, ConflictDetected = true, Project = project };
}

/// <summary>
/// Represents the action performed during sync.
/// </summary>
public enum SyncAction
{
    /// <summary>
    /// No action was needed, project was already in sync.
    /// </summary>
    None,

    /// <summary>
    /// Local changes were uploaded to cloud.
    /// </summary>
    Uploaded,

    /// <summary>
    /// Remote changes were downloaded to local.
    /// </summary>
    Downloaded,

    /// <summary>
    /// Changes were merged from both local and remote.
    /// </summary>
    Merged
}

/// <summary>
/// Defines the conflict resolution strategy.
/// </summary>
public enum ConflictResolutionStrategy
{
    /// <summary>
    /// Local changes take precedence over remote.
    /// </summary>
    LocalWins,

    /// <summary>
    /// Remote changes take precedence over local.
    /// </summary>
    RemoteWins,

    /// <summary>
    /// Attempt to merge changes automatically.
    /// </summary>
    Merge,

    /// <summary>
    /// Ask the user how to resolve the conflict.
    /// </summary>
    Ask
}

/// <summary>
/// Event args for cloud storage progress events.
/// </summary>
public class CloudProgressEventArgs : EventArgs
{
    /// <summary>
    /// Gets or sets the operation being performed.
    /// </summary>
    public CloudOperation Operation { get; set; }

    /// <summary>
    /// Gets or sets the project being processed.
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the progress percentage (0-100).
    /// </summary>
    public int ProgressPercent { get; set; }

    /// <summary>
    /// Gets or sets the bytes transferred so far.
    /// </summary>
    public long BytesTransferred { get; set; }

    /// <summary>
    /// Gets or sets the total bytes to transfer.
    /// </summary>
    public long TotalBytes { get; set; }

    /// <summary>
    /// Gets or sets whether the operation is complete.
    /// </summary>
    public bool IsComplete { get; set; }

    /// <summary>
    /// Gets or sets the error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Defines cloud storage operations.
/// </summary>
public enum CloudOperation
{
    /// <summary>
    /// Uploading a project to cloud.
    /// </summary>
    Upload,

    /// <summary>
    /// Downloading a project from cloud.
    /// </summary>
    Download,

    /// <summary>
    /// Synchronizing a project.
    /// </summary>
    Sync,

    /// <summary>
    /// Deleting a project from cloud.
    /// </summary>
    Delete
}

/// <summary>
/// Represents a pending cloud operation for offline queue.
/// </summary>
public class PendingCloudOperation
{
    /// <summary>
    /// Gets or sets the unique identifier for this operation.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the operation type.
    /// </summary>
    public CloudOperation Operation { get; set; }

    /// <summary>
    /// Gets or sets the cloud project ID.
    /// </summary>
    public string? CloudId { get; set; }

    /// <summary>
    /// Gets or sets the local path for the project.
    /// </summary>
    public string? LocalPath { get; set; }

    /// <summary>
    /// Gets or sets the project metadata.
    /// </summary>
    public CloudProject? Metadata { get; set; }

    /// <summary>
    /// Gets or sets when this operation was queued.
    /// </summary>
    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the number of retry attempts.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of retries.
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}
