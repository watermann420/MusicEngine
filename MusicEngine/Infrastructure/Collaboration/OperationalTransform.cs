//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Operational Transform (OT) algorithm for resolving concurrent edits in collaboration.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace MusicEngine.Infrastructure.Collaboration;

/// <summary>
/// Type of operation for OT.
/// </summary>
public enum OperationType
{
    /// <summary>Insert/add operation.</summary>
    Insert,

    /// <summary>Delete/remove operation.</summary>
    Delete,

    /// <summary>Update/modify operation.</summary>
    Update,

    /// <summary>Move operation (change position).</summary>
    Move,

    /// <summary>No operation (identity).</summary>
    NoOp
}

/// <summary>
/// Target domain for the operation.
/// </summary>
public enum OperationDomain
{
    /// <summary>Note operations in a pattern.</summary>
    Note,

    /// <summary>Track operations.</summary>
    Track,

    /// <summary>Clip operations in arrangement.</summary>
    Clip,

    /// <summary>Parameter value changes.</summary>
    Parameter
}

/// <summary>
/// Represents an operation that can be transformed.
/// </summary>
public class Operation
{
    /// <summary>Unique operation identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>ID of the peer that created this operation.</summary>
    public Guid PeerId { get; set; }

    /// <summary>Type of operation.</summary>
    public OperationType Type { get; set; }

    /// <summary>Domain/target type of the operation.</summary>
    public OperationDomain Domain { get; set; }

    /// <summary>ID of the target object (pattern, track, etc.).</summary>
    public Guid TargetId { get; set; }

    /// <summary>ID of the specific item being operated on.</summary>
    public Guid ItemId { get; set; }

    /// <summary>Position/index for the operation.</summary>
    public double Position { get; set; }

    /// <summary>Secondary position (e.g., note number for notes).</summary>
    public double SecondaryPosition { get; set; }

    /// <summary>Properties being changed (for updates).</summary>
    public Dictionary<string, object?> Properties { get; set; } = new();

    /// <summary>Vector clock at the time of operation.</summary>
    public Dictionary<Guid, long> VectorClock { get; set; } = new();

    /// <summary>Timestamp of operation creation.</summary>
    public long Timestamp { get; set; } = DateTime.UtcNow.Ticks;

    /// <summary>Whether this operation has been applied locally.</summary>
    public bool IsApplied { get; set; }

    /// <summary>Whether this operation has been acknowledged by the server.</summary>
    public bool IsAcknowledged { get; set; }

    /// <summary>
    /// Creates a deep clone of the operation.
    /// </summary>
    public Operation Clone()
    {
        return new Operation
        {
            Id = Id,
            PeerId = PeerId,
            Type = Type,
            Domain = Domain,
            TargetId = TargetId,
            ItemId = ItemId,
            Position = Position,
            SecondaryPosition = SecondaryPosition,
            Properties = new Dictionary<string, object?>(Properties),
            VectorClock = new Dictionary<Guid, long>(VectorClock),
            Timestamp = Timestamp,
            IsApplied = IsApplied,
            IsAcknowledged = IsAcknowledged
        };
    }

    /// <summary>
    /// Creates a NoOp operation.
    /// </summary>
    public static Operation NoOp(Guid peerId)
    {
        return new Operation
        {
            PeerId = peerId,
            Type = OperationType.NoOp
        };
    }

    /// <summary>
    /// Creates a note insert operation.
    /// </summary>
    public static Operation InsertNote(Guid peerId, Guid patternId, Guid noteId, double startBeat, int noteNumber,
        double duration, int velocity, int channel)
    {
        return new Operation
        {
            PeerId = peerId,
            Type = OperationType.Insert,
            Domain = OperationDomain.Note,
            TargetId = patternId,
            ItemId = noteId,
            Position = startBeat,
            SecondaryPosition = noteNumber,
            Properties = new Dictionary<string, object?>
            {
                ["duration"] = duration,
                ["velocity"] = velocity,
                ["channel"] = channel
            }
        };
    }

    /// <summary>
    /// Creates a note delete operation.
    /// </summary>
    public static Operation DeleteNote(Guid peerId, Guid patternId, Guid noteId, double startBeat, int noteNumber)
    {
        return new Operation
        {
            PeerId = peerId,
            Type = OperationType.Delete,
            Domain = OperationDomain.Note,
            TargetId = patternId,
            ItemId = noteId,
            Position = startBeat,
            SecondaryPosition = noteNumber
        };
    }

    /// <summary>
    /// Creates a note update operation.
    /// </summary>
    public static Operation UpdateNote(Guid peerId, Guid patternId, Guid noteId, Dictionary<string, object?> changes)
    {
        return new Operation
        {
            PeerId = peerId,
            Type = OperationType.Update,
            Domain = OperationDomain.Note,
            TargetId = patternId,
            ItemId = noteId,
            Properties = new Dictionary<string, object?>(changes)
        };
    }

    /// <summary>
    /// Creates a track insert operation.
    /// </summary>
    public static Operation InsertTrack(Guid peerId, Guid trackId, int index, string name, string trackType)
    {
        return new Operation
        {
            PeerId = peerId,
            Type = OperationType.Insert,
            Domain = OperationDomain.Track,
            ItemId = trackId,
            Position = index,
            Properties = new Dictionary<string, object?>
            {
                ["name"] = name,
                ["trackType"] = trackType
            }
        };
    }

    /// <summary>
    /// Creates a track delete operation.
    /// </summary>
    public static Operation DeleteTrack(Guid peerId, Guid trackId, int index)
    {
        return new Operation
        {
            PeerId = peerId,
            Type = OperationType.Delete,
            Domain = OperationDomain.Track,
            ItemId = trackId,
            Position = index
        };
    }

    /// <summary>
    /// Creates a clip insert operation.
    /// </summary>
    public static Operation InsertClip(Guid peerId, Guid trackId, Guid clipId, double startBeat, double length)
    {
        return new Operation
        {
            PeerId = peerId,
            Type = OperationType.Insert,
            Domain = OperationDomain.Clip,
            TargetId = trackId,
            ItemId = clipId,
            Position = startBeat,
            Properties = new Dictionary<string, object?>
            {
                ["length"] = length
            }
        };
    }

    /// <summary>
    /// Creates a parameter change operation.
    /// </summary>
    public static Operation ChangeParameter(Guid peerId, Guid targetId, string paramName, object? oldValue, object? newValue)
    {
        return new Operation
        {
            PeerId = peerId,
            Type = OperationType.Update,
            Domain = OperationDomain.Parameter,
            TargetId = targetId,
            Properties = new Dictionary<string, object?>
            {
                ["parameterName"] = paramName,
                ["oldValue"] = oldValue,
                ["newValue"] = newValue
            }
        };
    }
}

/// <summary>
/// Result of a transformation.
/// </summary>
public class TransformResult
{
    /// <summary>The transformed local operation.</summary>
    public Operation TransformedLocal { get; set; } = null!;

    /// <summary>The transformed remote operation.</summary>
    public Operation TransformedRemote { get; set; } = null!;

    /// <summary>Whether a conflict was detected.</summary>
    public bool HasConflict { get; set; }

    /// <summary>Description of the conflict (if any).</summary>
    public string? ConflictDescription { get; set; }
}

/// <summary>
/// Operational Transform implementation for concurrent edit conflict resolution.
/// </summary>
/// <remarks>
/// This implementation uses a simplified OT algorithm suitable for music production operations.
/// It handles note, track, clip, and parameter operations with proper conflict detection.
/// </remarks>
public class OperationalTransform
{
    private readonly ILogger? _logger;
    private readonly object _lock = new();
    private readonly List<Operation> _history = new();
    private readonly Dictionary<Guid, long> _serverVectorClock = new();

    /// <summary>
    /// Maximum history size to keep.
    /// </summary>
    public int MaxHistorySize { get; set; } = 10000;

    /// <summary>
    /// Event fired when a conflict is detected.
    /// </summary>
    public event EventHandler<ConflictEventArgs>? ConflictDetected;

    /// <summary>
    /// Creates a new OperationalTransform instance.
    /// </summary>
    /// <param name="logger">Optional logger.</param>
    public OperationalTransform(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Transforms a local operation against a remote operation.
    /// </summary>
    /// <param name="localOp">The local operation to transform.</param>
    /// <param name="remoteOp">The remote operation to transform against.</param>
    /// <returns>The transformation result with both transformed operations.</returns>
    public TransformResult Transform(Operation localOp, Operation remoteOp)
    {
        if (localOp == null) throw new ArgumentNullException(nameof(localOp));
        if (remoteOp == null) throw new ArgumentNullException(nameof(remoteOp));

        // If operations are in different domains, they don't conflict
        if (localOp.Domain != remoteOp.Domain)
        {
            return new TransformResult
            {
                TransformedLocal = localOp.Clone(),
                TransformedRemote = remoteOp.Clone(),
                HasConflict = false
            };
        }

        // If operations target different objects (except for tracks), they don't conflict
        if (localOp.Domain != OperationDomain.Track &&
            localOp.TargetId != remoteOp.TargetId)
        {
            return new TransformResult
            {
                TransformedLocal = localOp.Clone(),
                TransformedRemote = remoteOp.Clone(),
                HasConflict = false
            };
        }

        // Transform based on domain
        return localOp.Domain switch
        {
            OperationDomain.Note => TransformNoteOperations(localOp, remoteOp),
            OperationDomain.Track => TransformTrackOperations(localOp, remoteOp),
            OperationDomain.Clip => TransformClipOperations(localOp, remoteOp),
            OperationDomain.Parameter => TransformParameterOperations(localOp, remoteOp),
            _ => new TransformResult
            {
                TransformedLocal = localOp.Clone(),
                TransformedRemote = remoteOp.Clone(),
                HasConflict = false
            }
        };
    }

    /// <summary>
    /// Transforms note operations.
    /// </summary>
    private TransformResult TransformNoteOperations(Operation localOp, Operation remoteOp)
    {
        var result = new TransformResult
        {
            TransformedLocal = localOp.Clone(),
            TransformedRemote = remoteOp.Clone()
        };

        // Same note being operated on
        if (localOp.ItemId == remoteOp.ItemId)
        {
            return TransformSameNoteOperations(localOp, remoteOp);
        }

        // Different notes - no transformation needed for most cases
        // However, we might need to handle overlapping notes in the future
        return result;
    }

    /// <summary>
    /// Transforms operations on the same note.
    /// </summary>
    private TransformResult TransformSameNoteOperations(Operation localOp, Operation remoteOp)
    {
        var result = new TransformResult
        {
            TransformedLocal = localOp.Clone(),
            TransformedRemote = remoteOp.Clone()
        };

        // Both delete the same note
        if (localOp.Type == OperationType.Delete && remoteOp.Type == OperationType.Delete)
        {
            // Both operations become NoOps - note is already deleted
            result.TransformedLocal = Operation.NoOp(localOp.PeerId);
            result.TransformedRemote = Operation.NoOp(remoteOp.PeerId);
            return result;
        }

        // Local deletes, remote updates
        if (localOp.Type == OperationType.Delete && remoteOp.Type == OperationType.Update)
        {
            // Remote update becomes NoOp - note is being deleted
            result.TransformedRemote = Operation.NoOp(remoteOp.PeerId);
            return result;
        }

        // Local updates, remote deletes
        if (localOp.Type == OperationType.Update && remoteOp.Type == OperationType.Delete)
        {
            // Local update becomes NoOp - note is being deleted
            result.TransformedLocal = Operation.NoOp(localOp.PeerId);
            return result;
        }

        // Both update the same note
        if (localOp.Type == OperationType.Update && remoteOp.Type == OperationType.Update)
        {
            return TransformSameNoteUpdates(localOp, remoteOp);
        }

        // Local inserts, remote operates - shouldn't happen (different IDs)
        // Just return as-is
        return result;
    }

    /// <summary>
    /// Transforms concurrent updates to the same note.
    /// </summary>
    private TransformResult TransformSameNoteUpdates(Operation localOp, Operation remoteOp)
    {
        var result = new TransformResult
        {
            TransformedLocal = localOp.Clone(),
            TransformedRemote = remoteOp.Clone()
        };

        // Check for conflicting property changes
        var localProps = localOp.Properties;
        var remoteProps = remoteOp.Properties;

        var conflictingProps = localProps.Keys.Intersect(remoteProps.Keys).ToList();

        if (conflictingProps.Count > 0)
        {
            result.HasConflict = true;
            result.ConflictDescription = $"Concurrent updates to same note properties: {string.Join(", ", conflictingProps)}";

            // Resolve by timestamp (last-write-wins) or peer ID for deterministic ordering
            bool localWins = ShouldLocalWin(localOp, remoteOp);

            if (localWins)
            {
                // Remove conflicting props from remote operation
                foreach (var prop in conflictingProps)
                {
                    result.TransformedRemote.Properties.Remove(prop);
                }
            }
            else
            {
                // Remove conflicting props from local operation
                foreach (var prop in conflictingProps)
                {
                    result.TransformedLocal.Properties.Remove(prop);
                }
            }

            // If all properties were conflicting and removed, convert to NoOp
            if (result.TransformedLocal.Properties.Count == 0)
            {
                result.TransformedLocal = Operation.NoOp(localOp.PeerId);
            }
            if (result.TransformedRemote.Properties.Count == 0)
            {
                result.TransformedRemote = Operation.NoOp(remoteOp.PeerId);
            }

            OnConflictDetected(localOp, remoteOp, result.ConflictDescription);
        }

        return result;
    }

    /// <summary>
    /// Transforms track operations.
    /// </summary>
    private TransformResult TransformTrackOperations(Operation localOp, Operation remoteOp)
    {
        var result = new TransformResult
        {
            TransformedLocal = localOp.Clone(),
            TransformedRemote = remoteOp.Clone()
        };

        // Same track operations
        if (localOp.ItemId == remoteOp.ItemId)
        {
            // Both delete same track
            if (localOp.Type == OperationType.Delete && remoteOp.Type == OperationType.Delete)
            {
                result.TransformedLocal = Operation.NoOp(localOp.PeerId);
                result.TransformedRemote = Operation.NoOp(remoteOp.PeerId);
                return result;
            }

            // One deletes, one updates
            if (localOp.Type == OperationType.Delete && remoteOp.Type == OperationType.Update)
            {
                result.TransformedRemote = Operation.NoOp(remoteOp.PeerId);
                return result;
            }
            if (localOp.Type == OperationType.Update && remoteOp.Type == OperationType.Delete)
            {
                result.TransformedLocal = Operation.NoOp(localOp.PeerId);
                return result;
            }
        }

        // Track index adjustments for insert/delete
        if (localOp.Type == OperationType.Insert && remoteOp.Type == OperationType.Insert)
        {
            // Both inserting at same position - adjust one
            if (Math.Abs(localOp.Position - remoteOp.Position) < 0.001)
            {
                bool localWins = ShouldLocalWin(localOp, remoteOp);
                if (!localWins)
                {
                    result.TransformedLocal.Position = localOp.Position + 1;
                }
                else
                {
                    result.TransformedRemote.Position = remoteOp.Position + 1;
                }
            }
            // Local inserts before remote
            else if (localOp.Position <= remoteOp.Position)
            {
                result.TransformedRemote.Position = remoteOp.Position + 1;
            }
            // Remote inserts before local
            else
            {
                result.TransformedLocal.Position = localOp.Position + 1;
            }
        }
        else if (localOp.Type == OperationType.Delete && remoteOp.Type == OperationType.Insert)
        {
            // Local deletes before remote insert position
            if (localOp.Position < remoteOp.Position)
            {
                result.TransformedRemote.Position = remoteOp.Position - 1;
            }
        }
        else if (localOp.Type == OperationType.Insert && remoteOp.Type == OperationType.Delete)
        {
            // Remote deletes before local insert position
            if (remoteOp.Position < localOp.Position)
            {
                result.TransformedLocal.Position = localOp.Position - 1;
            }
        }

        return result;
    }

    /// <summary>
    /// Transforms clip operations.
    /// </summary>
    private TransformResult TransformClipOperations(Operation localOp, Operation remoteOp)
    {
        var result = new TransformResult
        {
            TransformedLocal = localOp.Clone(),
            TransformedRemote = remoteOp.Clone()
        };

        // Same clip operations
        if (localOp.ItemId == remoteOp.ItemId)
        {
            // Both delete same clip
            if (localOp.Type == OperationType.Delete && remoteOp.Type == OperationType.Delete)
            {
                result.TransformedLocal = Operation.NoOp(localOp.PeerId);
                result.TransformedRemote = Operation.NoOp(remoteOp.PeerId);
                return result;
            }

            // One deletes, one updates
            if (localOp.Type == OperationType.Delete && remoteOp.Type == OperationType.Update)
            {
                result.TransformedRemote = Operation.NoOp(remoteOp.PeerId);
                return result;
            }
            if (localOp.Type == OperationType.Update && remoteOp.Type == OperationType.Delete)
            {
                result.TransformedLocal = Operation.NoOp(localOp.PeerId);
                return result;
            }

            // Both update - merge non-conflicting, use LWW for conflicts
            if (localOp.Type == OperationType.Update && remoteOp.Type == OperationType.Update)
            {
                var conflictingProps = localOp.Properties.Keys.Intersect(remoteOp.Properties.Keys).ToList();
                if (conflictingProps.Count > 0)
                {
                    result.HasConflict = true;
                    result.ConflictDescription = $"Concurrent clip updates: {string.Join(", ", conflictingProps)}";

                    bool localWins = ShouldLocalWin(localOp, remoteOp);
                    foreach (var prop in conflictingProps)
                    {
                        if (localWins)
                            result.TransformedRemote.Properties.Remove(prop);
                        else
                            result.TransformedLocal.Properties.Remove(prop);
                    }

                    OnConflictDetected(localOp, remoteOp, result.ConflictDescription);
                }
            }

            // Both move clip - one wins
            if (localOp.Type == OperationType.Move && remoteOp.Type == OperationType.Move)
            {
                result.HasConflict = true;
                result.ConflictDescription = "Concurrent clip moves";
                if (!ShouldLocalWin(localOp, remoteOp))
                {
                    result.TransformedLocal = Operation.NoOp(localOp.PeerId);
                }
                else
                {
                    result.TransformedRemote = Operation.NoOp(remoteOp.PeerId);
                }
                OnConflictDetected(localOp, remoteOp, result.ConflictDescription);
            }
        }

        return result;
    }

    /// <summary>
    /// Transforms parameter operations.
    /// </summary>
    private TransformResult TransformParameterOperations(Operation localOp, Operation remoteOp)
    {
        var result = new TransformResult
        {
            TransformedLocal = localOp.Clone(),
            TransformedRemote = remoteOp.Clone()
        };

        // Same parameter being changed
        var localParam = localOp.Properties.GetValueOrDefault("parameterName") as string;
        var remoteParam = remoteOp.Properties.GetValueOrDefault("parameterName") as string;

        if (localOp.TargetId == remoteOp.TargetId && localParam == remoteParam)
        {
            result.HasConflict = true;
            result.ConflictDescription = $"Concurrent changes to parameter '{localParam}'";

            // Last-write-wins or peer ID ordering
            bool localWins = ShouldLocalWin(localOp, remoteOp);
            if (localWins)
            {
                result.TransformedRemote = Operation.NoOp(remoteOp.PeerId);
            }
            else
            {
                result.TransformedLocal = Operation.NoOp(localOp.PeerId);
            }

            OnConflictDetected(localOp, remoteOp, result.ConflictDescription);
        }

        return result;
    }

    /// <summary>
    /// Determines if the local operation should win in a conflict.
    /// Uses timestamp first, then peer ID for deterministic ordering.
    /// </summary>
    private static bool ShouldLocalWin(Operation localOp, Operation remoteOp)
    {
        // First compare timestamps
        if (localOp.Timestamp != remoteOp.Timestamp)
        {
            return localOp.Timestamp > remoteOp.Timestamp;
        }

        // Use peer ID for deterministic tie-breaking
        return localOp.PeerId.CompareTo(remoteOp.PeerId) > 0;
    }

    /// <summary>
    /// Checks if op1 happened before op2 based on vector clocks.
    /// </summary>
    public bool HappenedBefore(Operation op1, Operation op2)
    {
        bool atLeastOneLess = false;

        foreach (var kvp in op1.VectorClock)
        {
            if (!op2.VectorClock.TryGetValue(kvp.Key, out long op2Value))
            {
                op2Value = 0;
            }

            if (kvp.Value > op2Value)
            {
                return false; // op1 has a greater value, so it didn't happen before
            }

            if (kvp.Value < op2Value)
            {
                atLeastOneLess = true;
            }
        }

        // Check for any keys in op2 that aren't in op1
        foreach (var kvp in op2.VectorClock)
        {
            if (!op1.VectorClock.ContainsKey(kvp.Key) && kvp.Value > 0)
            {
                atLeastOneLess = true;
            }
        }

        return atLeastOneLess;
    }

    /// <summary>
    /// Checks if two operations are concurrent (neither happened before the other).
    /// </summary>
    public bool AreConcurrent(Operation op1, Operation op2)
    {
        return !HappenedBefore(op1, op2) && !HappenedBefore(op2, op1);
    }

    /// <summary>
    /// Adds an operation to the history.
    /// </summary>
    public void AddToHistory(Operation operation)
    {
        lock (_lock)
        {
            _history.Add(operation);

            // Trim history if too large
            while (_history.Count > MaxHistorySize)
            {
                _history.RemoveAt(0);
            }

            // Update server vector clock
            foreach (var kvp in operation.VectorClock)
            {
                if (!_serverVectorClock.TryGetValue(kvp.Key, out long current) || kvp.Value > current)
                {
                    _serverVectorClock[kvp.Key] = kvp.Value;
                }
            }
        }
    }

    /// <summary>
    /// Gets operations since a given vector clock state.
    /// </summary>
    public List<Operation> GetOperationsSince(Dictionary<Guid, long> vectorClock)
    {
        lock (_lock)
        {
            return _history.Where(op => !HappenedBeforeOrEqual(op, vectorClock)).ToList();
        }
    }

    /// <summary>
    /// Checks if an operation happened before or at the same time as a vector clock state.
    /// </summary>
    private bool HappenedBeforeOrEqual(Operation op, Dictionary<Guid, long> vectorClock)
    {
        foreach (var kvp in op.VectorClock)
        {
            if (!vectorClock.TryGetValue(kvp.Key, out long clockValue) || kvp.Value > clockValue)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Gets the current server vector clock.
    /// </summary>
    public Dictionary<Guid, long> GetServerVectorClock()
    {
        lock (_lock)
        {
            return new Dictionary<Guid, long>(_serverVectorClock);
        }
    }

    /// <summary>
    /// Clears the operation history.
    /// </summary>
    public void ClearHistory()
    {
        lock (_lock)
        {
            _history.Clear();
        }
    }

    private void OnConflictDetected(Operation localOp, Operation remoteOp, string description)
    {
        _logger?.LogWarning("OT conflict detected: {Description} between {LocalPeer} and {RemotePeer}",
            description, localOp.PeerId, remoteOp.PeerId);

        ConflictDetected?.Invoke(this, new ConflictEventArgs(localOp, remoteOp, description));
    }
}

/// <summary>
/// Event arguments for conflict detection.
/// </summary>
public class ConflictEventArgs : EventArgs
{
    /// <summary>The local operation involved in the conflict.</summary>
    public Operation LocalOperation { get; }

    /// <summary>The remote operation involved in the conflict.</summary>
    public Operation RemoteOperation { get; }

    /// <summary>Description of the conflict.</summary>
    public string Description { get; }

    /// <summary>Time when the conflict was detected.</summary>
    public DateTime DetectedAt { get; }

    public ConflictEventArgs(Operation localOp, Operation remoteOp, string description)
    {
        LocalOperation = localOp;
        RemoteOperation = remoteOp;
        Description = description;
        DetectedAt = DateTime.UtcNow;
    }
}
