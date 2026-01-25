//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Main session management for real-time collaboration.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MusicEngine.Infrastructure.Collaboration;

/// <summary>
/// State of a collaboration session.
/// </summary>
public enum SessionState
{
    /// <summary>Session is not active.</summary>
    Inactive,

    /// <summary>Session is being created/initialized.</summary>
    Creating,

    /// <summary>Session is active and accepting connections.</summary>
    Active,

    /// <summary>Session is connecting to a host.</summary>
    Connecting,

    /// <summary>Session is being closed.</summary>
    Closing,

    /// <summary>Session encountered an error.</summary>
    Error
}

/// <summary>
/// Event arguments for peer events.
/// </summary>
public class PeerEventArgs : EventArgs
{
    /// <summary>The peer that triggered the event.</summary>
    public CollaborationPeer Peer { get; }

    /// <summary>Optional message associated with the event.</summary>
    public string? Message { get; }

    public PeerEventArgs(CollaborationPeer peer, string? message = null)
    {
        Peer = peer;
        Message = message;
    }
}

/// <summary>
/// Event arguments for change events.
/// </summary>
public class ChangeEventArgs : EventArgs
{
    /// <summary>The message containing the change.</summary>
    public CollaborationMessage Message { get; }

    /// <summary>The peer that made the change.</summary>
    public CollaborationPeer? SourcePeer { get; }

    /// <summary>Whether the change originated locally.</summary>
    public bool IsLocal { get; }

    public ChangeEventArgs(CollaborationMessage message, CollaborationPeer? sourcePeer, bool isLocal)
    {
        Message = message;
        SourcePeer = sourcePeer;
        IsLocal = isLocal;
    }
}

/// <summary>
/// Event arguments for session state changes.
/// </summary>
public class SessionStateChangedEventArgs : EventArgs
{
    /// <summary>Previous state.</summary>
    public SessionState PreviousState { get; }

    /// <summary>New state.</summary>
    public SessionState NewState { get; }

    /// <summary>Optional error message if state is Error.</summary>
    public string? ErrorMessage { get; }

    public SessionStateChangedEventArgs(SessionState previousState, SessionState newState, string? errorMessage = null)
    {
        PreviousState = previousState;
        NewState = newState;
        ErrorMessage = errorMessage;
    }
}

/// <summary>
/// Event arguments for chat messages.
/// </summary>
public class ChatReceivedEventArgs : EventArgs
{
    /// <summary>The peer that sent the message.</summary>
    public CollaborationPeer Sender { get; }

    /// <summary>The chat text.</summary>
    public string Text { get; }

    /// <summary>Whether this is a private message.</summary>
    public bool IsPrivate { get; }

    /// <summary>Time the message was received.</summary>
    public DateTime ReceivedAt { get; }

    public ChatReceivedEventArgs(CollaborationPeer sender, string text, bool isPrivate)
    {
        Sender = sender;
        Text = text;
        IsPrivate = isPrivate;
        ReceivedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Manages a real-time collaboration session for MusicEngine.
/// </summary>
public class CollaborationSession : IDisposable
{
    private readonly ILogger? _logger;
    private readonly object _lock = new();
    private readonly ConcurrentDictionary<Guid, CollaborationPeer> _peers = new();
    private readonly ConcurrentQueue<CollaborationMessage> _pendingOperations = new();
    private readonly OperationalTransform _ot;
    private readonly Dictionary<Guid, long> _localVectorClock = new();

    private SessionState _state = SessionState.Inactive;
    private CollaborationPeer? _localPeer;
    private CollaborationPeer? _host;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _disposed;

    /// <summary>
    /// Unique identifier for this session.
    /// </summary>
    public Guid SessionId { get; private set; }

    /// <summary>
    /// Name of the session.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Password for the session (null if no password required).
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Maximum number of peers allowed.
    /// </summary>
    public int MaxPeers { get; set; } = 10;

    /// <summary>
    /// Current state of the session.
    /// </summary>
    public SessionState State
    {
        get
        {
            lock (_lock) return _state;
        }
        private set
        {
            SessionState oldState;
            lock (_lock)
            {
                oldState = _state;
                if (oldState == value) return;
                _state = value;
            }
            StateChanged?.Invoke(this, new SessionStateChangedEventArgs(oldState, value));
        }
    }

    /// <summary>
    /// Whether this instance is the host of the session.
    /// </summary>
    public bool IsHost => _localPeer?.IsHost ?? false;

    /// <summary>
    /// The local peer instance.
    /// </summary>
    public CollaborationPeer? LocalPeer => _localPeer;

    /// <summary>
    /// The host peer.
    /// </summary>
    public CollaborationPeer? Host => _host;

    /// <summary>
    /// Connected peers (excluding local peer).
    /// </summary>
    public IReadOnlyCollection<CollaborationPeer> Peers => _peers.Values.ToList().AsReadOnly();

    /// <summary>
    /// Number of connected peers (including local peer).
    /// </summary>
    public int PeerCount => _peers.Count + (_localPeer != null ? 1 : 0);

    /// <summary>
    /// The operational transform instance for this session.
    /// </summary>
    public OperationalTransform OperationalTransform => _ot;

    /// <summary>
    /// Fired when the session state changes.
    /// </summary>
    public event EventHandler<SessionStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Fired when a peer joins the session.
    /// </summary>
    public event EventHandler<PeerEventArgs>? PeerJoined;

    /// <summary>
    /// Fired when a peer leaves the session.
    /// </summary>
    public event EventHandler<PeerEventArgs>? PeerLeft;

    /// <summary>
    /// Fired when a change is received from a peer.
    /// </summary>
    public event EventHandler<ChangeEventArgs>? ChangeReceived;

    /// <summary>
    /// Fired when a conflict is detected.
    /// </summary>
    public event EventHandler<ConflictEventArgs>? ConflictDetected;

    /// <summary>
    /// Fired when a chat message is received.
    /// </summary>
    public event EventHandler<ChatReceivedEventArgs>? ChatReceived;

    /// <summary>
    /// Fired when cursor/selection updates are received.
    /// </summary>
    public event EventHandler<PeerEventArgs>? CursorUpdated;

    /// <summary>
    /// Fired when transport sync is received.
    /// </summary>
    public event EventHandler<ChangeEventArgs>? TransportSyncReceived;

    /// <summary>
    /// Creates a new collaboration session.
    /// </summary>
    /// <param name="logger">Optional logger.</param>
    public CollaborationSession(ILogger? logger = null)
    {
        _logger = logger;
        _ot = new OperationalTransform(logger);
        _ot.ConflictDetected += (_, e) => ConflictDetected?.Invoke(this, e);
    }

    /// <summary>
    /// Creates a new session as the host.
    /// </summary>
    /// <param name="sessionName">Name of the session.</param>
    /// <param name="peerName">Name of the local peer (host).</param>
    /// <param name="password">Optional session password.</param>
    /// <returns>The session ID.</returns>
    public Guid CreateSession(string sessionName, string peerName, string? password = null)
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            if (_state != SessionState.Inactive)
            {
                throw new InvalidOperationException("Session is already active. Leave the current session first.");
            }

            State = SessionState.Creating;

            try
            {
                SessionId = Guid.NewGuid();
                Name = sessionName;
                Password = password;

                // Create local peer as host
                _localPeer = new CollaborationPeer(
                    Guid.NewGuid(),
                    peerName,
                    CollaborationPeer.GenerateRandomColor(),
                    CollaborationRole.Host
                )
                {
                    ConnectionState = PeerConnectionState.Connected
                };

                _host = _localPeer;
                _localVectorClock[_localPeer.Id] = 0;

                _cancellationTokenSource = new CancellationTokenSource();

                State = SessionState.Active;

                _logger?.LogInformation("Created collaboration session {SessionId} '{SessionName}' as host {PeerName}",
                    SessionId, Name, peerName);

                return SessionId;
            }
            catch (Exception ex)
            {
                State = SessionState.Error;
                _logger?.LogError(ex, "Failed to create collaboration session");
                throw;
            }
        }
    }

    /// <summary>
    /// Joins an existing session.
    /// </summary>
    /// <param name="sessionId">ID of the session to join.</param>
    /// <param name="peerName">Name of the local peer.</param>
    /// <param name="password">Session password (if required).</param>
    /// <param name="role">Requested role.</param>
    public void JoinSession(Guid sessionId, string peerName, string? password = null,
        CollaborationRole role = CollaborationRole.Editor)
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            if (_state != SessionState.Inactive)
            {
                throw new InvalidOperationException("Already in a session. Leave the current session first.");
            }

            State = SessionState.Connecting;

            SessionId = sessionId;
            Password = password;

            // Create local peer
            _localPeer = new CollaborationPeer(
                Guid.NewGuid(),
                peerName,
                CollaborationPeer.GenerateRandomColor(),
                role
            )
            {
                ConnectionState = PeerConnectionState.Connecting
            };

            _localVectorClock[_localPeer.Id] = 0;
            _cancellationTokenSource = new CancellationTokenSource();

            _logger?.LogInformation("Joining collaboration session {SessionId} as {PeerName} ({Role})",
                sessionId, peerName, role);
        }
    }

    /// <summary>
    /// Marks the session as connected (called after successful connection).
    /// </summary>
    public void MarkConnected()
    {
        lock (_lock)
        {
            if (_localPeer != null)
            {
                _localPeer.ConnectionState = PeerConnectionState.Connected;
            }
            State = SessionState.Active;
        }
    }

    /// <summary>
    /// Leaves the current session.
    /// </summary>
    /// <param name="reason">Optional reason for leaving.</param>
    public void LeaveSession(string? reason = null)
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            if (_state == SessionState.Inactive) return;

            State = SessionState.Closing;

            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            _peers.Clear();
            _localPeer = null;
            _host = null;
            SessionId = Guid.Empty;
            Name = string.Empty;
            Password = null;

            _localVectorClock.Clear();
            _ot.ClearHistory();

            while (_pendingOperations.TryDequeue(out _)) { }

            State = SessionState.Inactive;

            _logger?.LogInformation("Left collaboration session. Reason: {Reason}", reason ?? "User requested");
        }
    }

    /// <summary>
    /// Handles a peer joining the session.
    /// </summary>
    public void HandlePeerJoin(JoinMessage message, System.Net.IPEndPoint? endPoint = null)
    {
        ThrowIfDisposed();

        // Validate password if required
        if (!string.IsNullOrEmpty(Password) && message.Password != Password)
        {
            _logger?.LogWarning("Peer {PeerId} attempted to join with invalid password", message.PeerId);
            return;
        }

        // Check max peers
        if (_peers.Count >= MaxPeers)
        {
            _logger?.LogWarning("Session is full, rejecting peer {PeerId}", message.PeerId);
            return;
        }

        var peer = CollaborationPeer.FromJoinMessage(message, endPoint);

        if (_peers.TryAdd(peer.Id, peer))
        {
            _localVectorClock[peer.Id] = 0;

            _logger?.LogInformation("Peer {PeerName} ({PeerId}) joined the session as {Role}",
                peer.Name, peer.Id, peer.Role);

            PeerJoined?.Invoke(this, new PeerEventArgs(peer));
        }
    }

    /// <summary>
    /// Handles a peer leaving the session.
    /// </summary>
    public void HandlePeerLeave(LeaveMessage message)
    {
        ThrowIfDisposed();

        if (_peers.TryRemove(message.PeerId, out var peer))
        {
            peer.ConnectionState = PeerConnectionState.Disconnected;

            _logger?.LogInformation("Peer {PeerName} ({PeerId}) left the session. Reason: {Reason}",
                peer.Name, peer.Id, message.Reason ?? "None given");

            PeerLeft?.Invoke(this, new PeerEventArgs(peer, message.Reason));

            // If host left, we need to handle host migration or session end
            if (peer.IsHost)
            {
                HandleHostLeft();
            }
        }
    }

    /// <summary>
    /// Handles the host leaving the session.
    /// </summary>
    private void HandleHostLeft()
    {
        // For now, just close the session if host leaves
        // In the future, could implement host migration
        _logger?.LogWarning("Host left the session. Session will be closed.");
        LeaveSession("Host left");
    }

    /// <summary>
    /// Handles a ping message.
    /// </summary>
    public PongMessage HandlePing(PingMessage ping)
    {
        if (_peers.TryGetValue(ping.PeerId, out var peer))
        {
            peer.MarkSeen();
        }

        return new PongMessage
        {
            PeerId = _localPeer?.Id ?? Guid.Empty,
            SessionId = SessionId,
            Sequence = ping.Sequence,
            ServerTimestamp = DateTime.UtcNow.Ticks
        };
    }

    /// <summary>
    /// Handles a pong message (latency calculation).
    /// </summary>
    public void HandlePong(PongMessage pong, long sentTimestamp)
    {
        if (_peers.TryGetValue(pong.PeerId, out var peer))
        {
            peer.MarkSeen();
            var roundTripMs = (int)((DateTime.UtcNow.Ticks - sentTimestamp) / TimeSpan.TicksPerMillisecond);
            peer.LatencyMs = roundTripMs / 2; // One-way latency estimate
        }
    }

    /// <summary>
    /// Broadcasts a change to all peers.
    /// </summary>
    /// <param name="message">The message to broadcast.</param>
    /// <returns>The message with updated vector clock.</returns>
    public CollaborationMessage BroadcastChange(CollaborationMessage message)
    {
        ThrowIfDisposed();

        if (_localPeer == null)
        {
            throw new InvalidOperationException("Not in a session.");
        }

        if (!_localPeer.CanEdit && IsEditOperation(message.Type))
        {
            throw new InvalidOperationException("Viewer role cannot make changes.");
        }

        // Update vector clock
        lock (_lock)
        {
            _localPeer.IncrementVectorClock();
            message.VectorClock = _localPeer.GetVectorClockCopy();
        }

        message.PeerId = _localPeer.Id;
        message.SessionId = SessionId;
        message.Timestamp = DateTime.UtcNow.Ticks;

        // Add to pending operations for OT
        _pendingOperations.Enqueue(message);

        _logger?.LogDebug("Broadcasting {MessageType} from local peer", message.Type);

        return message;
    }

    /// <summary>
    /// Handles a received change message.
    /// </summary>
    public void HandleChange(CollaborationMessage message)
    {
        ThrowIfDisposed();

        CollaborationPeer? sourcePeer = null;
        if (_peers.TryGetValue(message.PeerId, out var peer))
        {
            sourcePeer = peer;
            peer.MarkSeen();
            peer.UpdateVectorClock(message.VectorClock);
        }

        // Update local vector clock
        lock (_lock)
        {
            foreach (var kvp in message.VectorClock)
            {
                if (!_localVectorClock.TryGetValue(kvp.Key, out long current) || kvp.Value > current)
                {
                    _localVectorClock[kvp.Key] = kvp.Value;
                }
            }
        }

        bool isLocal = message.PeerId == _localPeer?.Id;

        _logger?.LogDebug("Received {MessageType} from peer {PeerId} (local: {IsLocal})",
            message.Type, message.PeerId, isLocal);

        // Handle specific message types
        switch (message.Type)
        {
            case CollaborationMessageType.Chat:
                HandleChatMessage((ChatMessage)message, sourcePeer);
                break;

            case CollaborationMessageType.Cursor:
                HandleCursorMessage((CursorMessage)message, sourcePeer);
                break;

            case CollaborationMessageType.TransportSync:
                TransportSyncReceived?.Invoke(this, new ChangeEventArgs(message, sourcePeer, isLocal));
                break;

            default:
                ChangeReceived?.Invoke(this, new ChangeEventArgs(message, sourcePeer, isLocal));
                break;
        }
    }

    /// <summary>
    /// Handles a chat message.
    /// </summary>
    private void HandleChatMessage(ChatMessage message, CollaborationPeer? sender)
    {
        if (sender == null) return;

        bool isPrivate = message.TargetPeerId.HasValue &&
                        message.TargetPeerId == _localPeer?.Id;

        ChatReceived?.Invoke(this, new ChatReceivedEventArgs(sender, message.Text, isPrivate));
    }

    /// <summary>
    /// Handles a cursor message.
    /// </summary>
    private void HandleCursorMessage(CursorMessage message, CollaborationPeer? peer)
    {
        if (peer == null) return;

        peer.UpdateCursor(message.ViewType, message.X, message.Y, message.TrackId);

        if (message.SelectionStart.HasValue && message.SelectionEnd.HasValue)
        {
            peer.UpdateSelection(new EditorSelection
            {
                ViewType = message.ViewType,
                Start = message.SelectionStart,
                End = message.SelectionEnd,
                TrackId = message.TrackId
            });
        }
        else
        {
            peer.UpdateSelection(null);
        }

        CursorUpdated?.Invoke(this, new PeerEventArgs(peer));
    }

    /// <summary>
    /// Sends a chat message.
    /// </summary>
    /// <param name="text">The message text.</param>
    /// <param name="targetPeerId">Target peer for private message (null for broadcast).</param>
    /// <returns>The chat message.</returns>
    public ChatMessage SendChat(string text, Guid? targetPeerId = null)
    {
        ThrowIfDisposed();

        if (_localPeer == null)
        {
            throw new InvalidOperationException("Not in a session.");
        }

        return new ChatMessage
        {
            PeerId = _localPeer.Id,
            SessionId = SessionId,
            Text = text,
            TargetPeerId = targetPeerId,
            VectorClock = _localPeer.GetVectorClockCopy()
        };
    }

    /// <summary>
    /// Sends a cursor update.
    /// </summary>
    public CursorMessage SendCursorUpdate(string viewType, double x, double y, Guid? trackId = null,
        (double X, double Y)? selectionStart = null, (double X, double Y)? selectionEnd = null)
    {
        ThrowIfDisposed();

        if (_localPeer == null)
        {
            throw new InvalidOperationException("Not in a session.");
        }

        return new CursorMessage
        {
            PeerId = _localPeer.Id,
            SessionId = SessionId,
            ViewType = viewType,
            X = x,
            Y = y,
            TrackId = trackId,
            SelectionStart = selectionStart,
            SelectionEnd = selectionEnd,
            VectorClock = _localPeer.GetVectorClockCopy()
        };
    }

    /// <summary>
    /// Gets peer by ID.
    /// </summary>
    public CollaborationPeer? GetPeer(Guid peerId)
    {
        if (peerId == _localPeer?.Id) return _localPeer;
        _peers.TryGetValue(peerId, out var peer);
        return peer;
    }

    /// <summary>
    /// Gets all peers including local peer.
    /// </summary>
    public IReadOnlyList<CollaborationPeer> GetAllPeers()
    {
        var list = _peers.Values.ToList();
        if (_localPeer != null)
        {
            list.Insert(0, _localPeer);
        }
        return list.AsReadOnly();
    }

    /// <summary>
    /// Checks if a message type is an edit operation.
    /// </summary>
    private static bool IsEditOperation(CollaborationMessageType type)
    {
        return type switch
        {
            CollaborationMessageType.NoteAdd => true,
            CollaborationMessageType.NoteRemove => true,
            CollaborationMessageType.NoteModify => true,
            CollaborationMessageType.TrackAdd => true,
            CollaborationMessageType.TrackRemove => true,
            CollaborationMessageType.TrackModify => true,
            CollaborationMessageType.ClipAdd => true,
            CollaborationMessageType.ClipRemove => true,
            CollaborationMessageType.ClipModify => true,
            CollaborationMessageType.ParameterChange => true,
            _ => false
        };
    }

    /// <summary>
    /// Creates a sync request message.
    /// </summary>
    public SyncRequestMessage CreateSyncRequest()
    {
        ThrowIfDisposed();

        return new SyncRequestMessage
        {
            PeerId = _localPeer?.Id ?? Guid.Empty,
            SessionId = SessionId,
            IncludeProjectData = true
        };
    }

    /// <summary>
    /// Creates a sync response message.
    /// </summary>
    /// <param name="projectState">Serialized project state.</param>
    public SyncResponseMessage CreateSyncResponse(string? projectState)
    {
        ThrowIfDisposed();

        return new SyncResponseMessage
        {
            PeerId = _localPeer?.Id ?? Guid.Empty,
            SessionId = SessionId,
            ProjectState = projectState,
            Peers = GetAllPeers().Select(p => p.ToPeerInfo()).ToList(),
            Transport = null // Should be filled in by caller
        };
    }

    /// <summary>
    /// Marks a peer as timed out and removes them.
    /// </summary>
    public void TimeoutPeer(Guid peerId)
    {
        if (_peers.TryRemove(peerId, out var peer))
        {
            peer.ConnectionState = PeerConnectionState.Disconnected;
            _logger?.LogWarning("Peer {PeerName} ({PeerId}) timed out", peer.Name, peer.Id);
            PeerLeft?.Invoke(this, new PeerEventArgs(peer, "Timeout"));
        }
    }

    /// <summary>
    /// Checks for timed out peers.
    /// </summary>
    public void CheckPeerTimeouts()
    {
        var timedOutPeers = _peers.Values
            .Where(p => p.TimeSinceLastSeenMs > CollaborationProtocol.PeerTimeoutMs)
            .Select(p => p.Id)
            .ToList();

        foreach (var peerId in timedOutPeers)
        {
            TimeoutPeer(peerId);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CollaborationSession));
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            LeaveSession("Session disposed");
            _cancellationTokenSource?.Dispose();
        }

        _disposed = true;
    }
}
