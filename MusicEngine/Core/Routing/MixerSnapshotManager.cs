// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio/MIDI routing component.

namespace MusicEngine.Core.Routing;

/// <summary>
/// Manages mixer snapshots - allows capturing, recalling, and interpolating mixer states.
/// </summary>
public class MixerSnapshotManager
{
    private readonly object _lock = new();
    private readonly List<MixerSnapshot> _snapshots = new();
    private MixerSnapshot? _currentSnapshot;
    private MixerSnapshot? _previousSnapshot;

    /// <summary>
    /// Event raised when a snapshot is captured.
    /// </summary>
    public event EventHandler<MixerSnapshot>? SnapshotCaptured;

    /// <summary>
    /// Event raised when a snapshot is recalled.
    /// </summary>
    public event EventHandler<MixerSnapshot>? SnapshotRecalled;

    /// <summary>
    /// Event raised when a snapshot is deleted.
    /// </summary>
    public event EventHandler<MixerSnapshot>? SnapshotDeleted;

    /// <summary>
    /// Event raised when interpolation is in progress.
    /// </summary>
    public event EventHandler<float>? InterpolationProgress;

    /// <summary>
    /// Gets the list of all stored snapshots.
    /// </summary>
    public IReadOnlyList<MixerSnapshot> Snapshots
    {
        get
        {
            lock (_lock)
            {
                return _snapshots.ToList().AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Gets the currently active snapshot.
    /// </summary>
    public MixerSnapshot? CurrentSnapshot
    {
        get
        {
            lock (_lock)
            {
                return _currentSnapshot;
            }
        }
    }

    /// <summary>
    /// Gets the number of stored snapshots.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _snapshots.Count;
            }
        }
    }

    /// <summary>
    /// Captures the current state of all channels as a snapshot.
    /// </summary>
    /// <param name="name">The name for the snapshot.</param>
    /// <param name="channels">The audio buses to capture.</param>
    /// <returns>The captured mixer snapshot.</returns>
    /// <exception cref="ArgumentNullException">Thrown if name or channels is null.</exception>
    public MixerSnapshot CaptureSnapshot(string name, IEnumerable<AudioBus> channels)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentNullException(nameof(name));
        if (channels == null)
            throw new ArgumentNullException(nameof(channels));

        var snapshot = new MixerSnapshot(name);

        foreach (var channel in channels)
        {
            var channelSnapshot = CaptureChannelState(channel);
            snapshot.Channels.Add(channelSnapshot);
        }

        lock (_lock)
        {
            _snapshots.Add(snapshot);
            _currentSnapshot = snapshot;
        }

        SnapshotCaptured?.Invoke(this, snapshot);
        return snapshot;
    }

    /// <summary>
    /// Captures a single channel's state.
    /// </summary>
    /// <param name="channel">The channel to capture.</param>
    /// <returns>The channel snapshot.</returns>
    private ChannelSnapshot CaptureChannelState(AudioBus channel)
    {
        var snapshot = new ChannelSnapshot(channel.Name)
        {
            Volume = channel.Volume,
            Pan = channel.Pan,
            Mute = channel.Mute,
            Solo = channel.Solo
        };

        // Note: Effect parameters and send levels would be captured here
        // if the AudioBus exposed them. This is a simplified implementation.

        return snapshot;
    }

    /// <summary>
    /// Recalls a snapshot by applying its values to the specified channels.
    /// </summary>
    /// <param name="snapshot">The snapshot to recall.</param>
    /// <param name="channels">The audio buses to apply the snapshot to.</param>
    /// <exception cref="ArgumentNullException">Thrown if snapshot or channels is null.</exception>
    public void RecallSnapshot(MixerSnapshot snapshot, IEnumerable<AudioBus> channels)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));
        if (channels == null)
            throw new ArgumentNullException(nameof(channels));

        var channelList = channels.ToList();
        var channelMap = channelList.ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);

        foreach (var channelSnapshot in snapshot.Channels)
        {
            if (channelMap.TryGetValue(channelSnapshot.ChannelName, out var channel))
            {
                ApplyChannelState(channel, channelSnapshot);
            }
        }

        lock (_lock)
        {
            _previousSnapshot = _currentSnapshot;
            _currentSnapshot = snapshot;
        }

        SnapshotRecalled?.Invoke(this, snapshot);
    }

    /// <summary>
    /// Applies a channel snapshot's values to an audio bus.
    /// </summary>
    /// <param name="channel">The channel to apply values to.</param>
    /// <param name="snapshot">The snapshot values to apply.</param>
    private void ApplyChannelState(AudioBus channel, ChannelSnapshot snapshot)
    {
        channel.Volume = snapshot.Volume;
        channel.Pan = snapshot.Pan;
        channel.Mute = snapshot.Mute;
        channel.Solo = snapshot.Solo;

        // Note: Effect parameters and send levels would be applied here
        // if the AudioBus exposed them. This is a simplified implementation.
    }

    /// <summary>
    /// Interpolates between two snapshots and applies the result to the channels.
    /// </summary>
    /// <param name="from">The starting snapshot.</param>
    /// <param name="to">The target snapshot.</param>
    /// <param name="t">Interpolation factor (0.0 = from, 1.0 = to).</param>
    /// <param name="channels">The audio buses to apply the interpolated values to.</param>
    /// <exception cref="ArgumentNullException">Thrown if any parameter is null.</exception>
    public void InterpolateSnapshots(MixerSnapshot from, MixerSnapshot to, float t, IEnumerable<AudioBus> channels)
    {
        if (from == null)
            throw new ArgumentNullException(nameof(from));
        if (to == null)
            throw new ArgumentNullException(nameof(to));
        if (channels == null)
            throw new ArgumentNullException(nameof(channels));

        t = Math.Clamp(t, 0f, 1f);

        var channelList = channels.ToList();
        var channelMap = channelList.ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);

        var fromChannels = from.Channels.ToDictionary(c => c.ChannelName, c => c, StringComparer.OrdinalIgnoreCase);
        var toChannels = to.Channels.ToDictionary(c => c.ChannelName, c => c, StringComparer.OrdinalIgnoreCase);

        foreach (var channel in channelList)
        {
            fromChannels.TryGetValue(channel.Name, out var fromChannel);
            toChannels.TryGetValue(channel.Name, out var toChannel);

            if (fromChannel != null && toChannel != null)
            {
                // Both snapshots have this channel - interpolate
                var interpolated = fromChannel.Lerp(toChannel, t);
                ApplyChannelState(channel, interpolated);
            }
            else if (fromChannel != null && t < 0.5f)
            {
                // Only from snapshot has this channel and we're closer to from
                ApplyChannelState(channel, fromChannel);
            }
            else if (toChannel != null && t >= 0.5f)
            {
                // Only to snapshot has this channel and we're closer to to
                ApplyChannelState(channel, toChannel);
            }
        }

        InterpolationProgress?.Invoke(this, t);
    }

    /// <summary>
    /// Performs a smooth crossfade between two snapshots over time.
    /// </summary>
    /// <param name="from">The starting snapshot.</param>
    /// <param name="to">The target snapshot.</param>
    /// <param name="channels">The audio buses to apply the interpolated values to.</param>
    /// <param name="durationMs">The duration of the crossfade in milliseconds.</param>
    /// <param name="cancellationToken">Cancellation token to stop the crossfade.</param>
    public async Task CrossfadeSnapshotsAsync(
        MixerSnapshot from,
        MixerSnapshot to,
        IEnumerable<AudioBus> channels,
        int durationMs = 1000,
        CancellationToken cancellationToken = default)
    {
        if (from == null)
            throw new ArgumentNullException(nameof(from));
        if (to == null)
            throw new ArgumentNullException(nameof(to));
        if (channels == null)
            throw new ArgumentNullException(nameof(channels));

        durationMs = Math.Max(10, durationMs);

        var channelList = channels.ToList();
        var startTime = DateTime.UtcNow;
        var endTime = startTime.AddMilliseconds(durationMs);

        while (DateTime.UtcNow < endTime)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            var t = (float)(elapsed / durationMs);
            t = Math.Clamp(t, 0f, 1f);

            // Apply smooth easing (ease-in-out)
            t = SmoothStep(t);

            InterpolateSnapshots(from, to, t, channelList);

            await Task.Delay(16, cancellationToken); // ~60fps update rate
        }

        // Ensure we end exactly at the target snapshot
        RecallSnapshot(to, channelList);
    }

    /// <summary>
    /// Smooth step function for easing.
    /// </summary>
    private static float SmoothStep(float t)
    {
        return t * t * (3f - 2f * t);
    }

    /// <summary>
    /// Deletes a snapshot from the manager.
    /// </summary>
    /// <param name="snapshot">The snapshot to delete.</param>
    /// <returns>True if deleted, false if not found.</returns>
    public bool DeleteSnapshot(MixerSnapshot snapshot)
    {
        if (snapshot == null)
            return false;

        bool removed;
        lock (_lock)
        {
            removed = _snapshots.Remove(snapshot);
            if (removed && _currentSnapshot == snapshot)
            {
                _currentSnapshot = null;
            }
            if (_previousSnapshot == snapshot)
            {
                _previousSnapshot = null;
            }
        }

        if (removed)
        {
            SnapshotDeleted?.Invoke(this, snapshot);
        }

        return removed;
    }

    /// <summary>
    /// Deletes a snapshot by ID.
    /// </summary>
    /// <param name="snapshotId">The ID of the snapshot to delete.</param>
    /// <returns>True if deleted, false if not found.</returns>
    public bool DeleteSnapshot(Guid snapshotId)
    {
        MixerSnapshot? snapshot;
        lock (_lock)
        {
            snapshot = _snapshots.Find(s => s.Id == snapshotId);
        }

        return snapshot != null && DeleteSnapshot(snapshot);
    }

    /// <summary>
    /// Gets a snapshot by ID.
    /// </summary>
    /// <param name="snapshotId">The ID of the snapshot.</param>
    /// <returns>The snapshot, or null if not found.</returns>
    public MixerSnapshot? GetSnapshot(Guid snapshotId)
    {
        lock (_lock)
        {
            return _snapshots.Find(s => s.Id == snapshotId);
        }
    }

    /// <summary>
    /// Gets a snapshot by name.
    /// </summary>
    /// <param name="name">The name of the snapshot.</param>
    /// <returns>The snapshot, or null if not found.</returns>
    public MixerSnapshot? GetSnapshot(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        lock (_lock)
        {
            return _snapshots.Find(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Updates an existing snapshot with new values from the channels.
    /// </summary>
    /// <param name="snapshot">The snapshot to update.</param>
    /// <param name="channels">The channels to capture values from.</param>
    /// <exception cref="ArgumentNullException">Thrown if snapshot or channels is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the snapshot is not managed by this manager.</exception>
    public void UpdateSnapshot(MixerSnapshot snapshot, IEnumerable<AudioBus> channels)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));
        if (channels == null)
            throw new ArgumentNullException(nameof(channels));

        lock (_lock)
        {
            if (!_snapshots.Contains(snapshot))
            {
                throw new InvalidOperationException("Snapshot is not managed by this MixerSnapshotManager.");
            }

            snapshot.Channels.Clear();
            foreach (var channel in channels)
            {
                var channelSnapshot = CaptureChannelState(channel);
                snapshot.Channels.Add(channelSnapshot);
            }
            snapshot.Modified = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Renames a snapshot.
    /// </summary>
    /// <param name="snapshot">The snapshot to rename.</param>
    /// <param name="newName">The new name.</param>
    /// <exception cref="ArgumentNullException">Thrown if snapshot or newName is null.</exception>
    public void RenameSnapshot(MixerSnapshot snapshot, string newName)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentNullException(nameof(newName));

        lock (_lock)
        {
            if (_snapshots.Contains(snapshot))
            {
                snapshot.Name = newName;
                snapshot.Modified = DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    /// Creates a copy of an existing snapshot.
    /// </summary>
    /// <param name="snapshot">The snapshot to copy.</param>
    /// <returns>The new snapshot copy.</returns>
    /// <exception cref="ArgumentNullException">Thrown if snapshot is null.</exception>
    public MixerSnapshot DuplicateSnapshot(MixerSnapshot snapshot)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));

        var copy = snapshot.Clone();

        lock (_lock)
        {
            _snapshots.Add(copy);
        }

        SnapshotCaptured?.Invoke(this, copy);
        return copy;
    }

    /// <summary>
    /// Imports a snapshot from JSON and adds it to the manager.
    /// </summary>
    /// <param name="json">The JSON string to import.</param>
    /// <returns>The imported snapshot.</returns>
    public MixerSnapshot ImportSnapshot(string json)
    {
        var snapshot = MixerSnapshot.FromJson(json);

        // Generate new ID to avoid conflicts
        snapshot.Id = Guid.NewGuid();

        lock (_lock)
        {
            _snapshots.Add(snapshot);
        }

        SnapshotCaptured?.Invoke(this, snapshot);
        return snapshot;
    }

    /// <summary>
    /// Exports a snapshot to JSON.
    /// </summary>
    /// <param name="snapshot">The snapshot to export.</param>
    /// <returns>The JSON string.</returns>
    public string ExportSnapshot(MixerSnapshot snapshot)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));

        return snapshot.ToJson();
    }

    /// <summary>
    /// Exports all snapshots to JSON.
    /// </summary>
    /// <returns>JSON string containing all snapshots.</returns>
    public string ExportAllSnapshots()
    {
        lock (_lock)
        {
            var options = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            };
            return System.Text.Json.JsonSerializer.Serialize(_snapshots, options);
        }
    }

    /// <summary>
    /// Imports multiple snapshots from JSON.
    /// </summary>
    /// <param name="json">The JSON string containing snapshots.</param>
    /// <returns>The number of snapshots imported.</returns>
    public int ImportSnapshots(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return 0;

        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        };

        var snapshots = System.Text.Json.JsonSerializer.Deserialize<List<MixerSnapshot>>(json, options);
        if (snapshots == null)
            return 0;

        lock (_lock)
        {
            foreach (var snapshot in snapshots)
            {
                snapshot.Id = Guid.NewGuid(); // New ID to avoid conflicts
                _snapshots.Add(snapshot);
                SnapshotCaptured?.Invoke(this, snapshot);
            }
        }

        return snapshots.Count;
    }

    /// <summary>
    /// Clears all snapshots from the manager.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _snapshots.Clear();
            _currentSnapshot = null;
            _previousSnapshot = null;
        }
    }

    /// <summary>
    /// Recalls the previous snapshot (undo last recall).
    /// </summary>
    /// <param name="channels">The channels to apply the snapshot to.</param>
    /// <returns>True if previous snapshot was recalled, false if no previous snapshot exists.</returns>
    public bool RecallPreviousSnapshot(IEnumerable<AudioBus> channels)
    {
        MixerSnapshot? previous;
        lock (_lock)
        {
            previous = _previousSnapshot;
        }

        if (previous != null)
        {
            RecallSnapshot(previous, channels);
            return true;
        }
        return false;
    }
}
