// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json.Serialization;

namespace MusicEngine.Infrastructure.Collaboration;

/// <summary>
/// Represents a selection in the editor.
/// </summary>
public class EditorSelection
{
    /// <summary>View type where selection is made.</summary>
    public string ViewType { get; set; } = string.Empty;

    /// <summary>Selected item IDs (notes, clips, tracks, etc.).</summary>
    public List<Guid> SelectedIds { get; set; } = new();

    /// <summary>Selection start position.</summary>
    public (double X, double Y)? Start { get; set; }

    /// <summary>Selection end position.</summary>
    public (double X, double Y)? End { get; set; }

    /// <summary>Track ID if selection is within a specific track.</summary>
    public Guid? TrackId { get; set; }

    /// <summary>Pattern ID if selection is within a specific pattern.</summary>
    public Guid? PatternId { get; set; }
}

/// <summary>
/// Represents a cursor position in the editor.
/// </summary>
public class CursorPosition
{
    /// <summary>View type where cursor is located.</summary>
    public string ViewType { get; set; } = string.Empty;

    /// <summary>X position (e.g., beat position).</summary>
    public double X { get; set; }

    /// <summary>Y position (e.g., track index or note number).</summary>
    public double Y { get; set; }

    /// <summary>Track ID if cursor is within a specific track.</summary>
    public Guid? TrackId { get; set; }

    /// <summary>Timestamp of last update.</summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Connection state of a peer.
/// </summary>
public enum PeerConnectionState
{
    /// <summary>Peer is disconnected.</summary>
    Disconnected,

    /// <summary>Peer is connecting.</summary>
    Connecting,

    /// <summary>Peer is connected and active.</summary>
    Connected,

    /// <summary>Peer connection is temporarily lost, attempting to reconnect.</summary>
    Reconnecting
}

/// <summary>
/// Represents a peer/user in a real-time collaboration session.
/// </summary>
public class CollaborationPeer : IEquatable<CollaborationPeer>
{
    private readonly object _lock = new();
    private int _latencyMs;
    private DateTime _lastSeen;
    private PeerConnectionState _connectionState;
    private CursorPosition? _cursorPosition;
    private EditorSelection? _currentSelection;
    private readonly Dictionary<Guid, long> _vectorClock = new();
    private readonly Queue<long> _latencyHistory = new();
    private const int MaxLatencyHistorySize = 10;

    /// <summary>
    /// Unique identifier for this peer.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Display name of the peer.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Color for displaying this peer's cursor and selections (ARGB format).
    /// </summary>
    public uint Color { get; set; }

    /// <summary>
    /// Role of this peer in the session.
    /// </summary>
    public CollaborationRole Role { get; set; }

    /// <summary>
    /// Network endpoint of the peer.
    /// </summary>
    [JsonIgnore]
    public IPEndPoint? EndPoint { get; set; }

    /// <summary>
    /// Current connection state.
    /// </summary>
    public PeerConnectionState ConnectionState
    {
        get
        {
            lock (_lock)
            {
                return _connectionState;
            }
        }
        set
        {
            lock (_lock)
            {
                _connectionState = value;
                if (value == PeerConnectionState.Connected)
                {
                    _lastSeen = DateTime.UtcNow;
                }
            }
        }
    }

    /// <summary>
    /// Network latency in milliseconds.
    /// </summary>
    public int LatencyMs
    {
        get
        {
            lock (_lock)
            {
                return _latencyMs;
            }
        }
        set
        {
            lock (_lock)
            {
                _latencyMs = value;
                _latencyHistory.Enqueue(value);
                while (_latencyHistory.Count > MaxLatencyHistorySize)
                {
                    _latencyHistory.Dequeue();
                }
            }
        }
    }

    /// <summary>
    /// Average latency over recent measurements.
    /// </summary>
    public double AverageLatencyMs
    {
        get
        {
            lock (_lock)
            {
                if (_latencyHistory.Count == 0) return 0;
                long sum = 0;
                foreach (var latency in _latencyHistory)
                {
                    sum += latency;
                }
                return (double)sum / _latencyHistory.Count;
            }
        }
    }

    /// <summary>
    /// Last time this peer was seen (received a message from).
    /// </summary>
    public DateTime LastSeen
    {
        get
        {
            lock (_lock)
            {
                return _lastSeen;
            }
        }
        set
        {
            lock (_lock)
            {
                _lastSeen = value;
            }
        }
    }

    /// <summary>
    /// Time since last seen in milliseconds.
    /// </summary>
    public double TimeSinceLastSeenMs
    {
        get
        {
            lock (_lock)
            {
                return (DateTime.UtcNow - _lastSeen).TotalMilliseconds;
            }
        }
    }

    /// <summary>
    /// Whether the peer is considered active (seen recently).
    /// </summary>
    public bool IsActive => TimeSinceLastSeenMs < CollaborationProtocol.PeerTimeoutMs;

    /// <summary>
    /// Current cursor position in the editor.
    /// </summary>
    public CursorPosition? CursorPosition
    {
        get
        {
            lock (_lock)
            {
                return _cursorPosition;
            }
        }
        set
        {
            lock (_lock)
            {
                _cursorPosition = value;
            }
        }
    }

    /// <summary>
    /// Current selection in the editor.
    /// </summary>
    public EditorSelection? CurrentSelection
    {
        get
        {
            lock (_lock)
            {
                return _currentSelection;
            }
        }
        set
        {
            lock (_lock)
            {
                _currentSelection = value;
            }
        }
    }

    /// <summary>
    /// Vector clock for this peer (for causal ordering).
    /// </summary>
    public IReadOnlyDictionary<Guid, long> VectorClock
    {
        get
        {
            lock (_lock)
            {
                return new Dictionary<Guid, long>(_vectorClock);
            }
        }
    }

    /// <summary>
    /// Time when the peer joined the session.
    /// </summary>
    public DateTime JoinedAt { get; }

    /// <summary>
    /// Number of operations sent by this peer.
    /// </summary>
    public long OperationCount { get; private set; }

    /// <summary>
    /// Creates a new collaboration peer.
    /// </summary>
    /// <param name="id">Unique peer identifier.</param>
    /// <param name="name">Display name.</param>
    /// <param name="color">Display color (ARGB).</param>
    /// <param name="role">Peer role.</param>
    public CollaborationPeer(Guid id, string name, uint color, CollaborationRole role = CollaborationRole.Editor)
    {
        Id = id;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Color = color;
        Role = role;
        JoinedAt = DateTime.UtcNow;
        _lastSeen = DateTime.UtcNow;
        _connectionState = PeerConnectionState.Disconnected;
        _vectorClock[id] = 0;
    }

    /// <summary>
    /// Creates a collaboration peer from a join message.
    /// </summary>
    public static CollaborationPeer FromJoinMessage(JoinMessage message, IPEndPoint? endPoint = null)
    {
        return new CollaborationPeer(message.PeerId, message.PeerName, message.Color, message.Role)
        {
            EndPoint = endPoint,
            ConnectionState = PeerConnectionState.Connected
        };
    }

    /// <summary>
    /// Updates the cursor position.
    /// </summary>
    public void UpdateCursor(string viewType, double x, double y, Guid? trackId = null)
    {
        lock (_lock)
        {
            _cursorPosition = new CursorPosition
            {
                ViewType = viewType,
                X = x,
                Y = y,
                TrackId = trackId,
                LastUpdated = DateTime.UtcNow
            };
            _lastSeen = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Updates the current selection.
    /// </summary>
    public void UpdateSelection(EditorSelection? selection)
    {
        lock (_lock)
        {
            _currentSelection = selection;
            _lastSeen = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Increments the local vector clock counter.
    /// </summary>
    /// <returns>The new vector clock value for this peer.</returns>
    public long IncrementVectorClock()
    {
        lock (_lock)
        {
            OperationCount++;
            return ++_vectorClock[Id];
        }
    }

    /// <summary>
    /// Updates the vector clock based on a received message.
    /// </summary>
    /// <param name="receivedClock">The vector clock from the received message.</param>
    public void UpdateVectorClock(IReadOnlyDictionary<Guid, long> receivedClock)
    {
        lock (_lock)
        {
            foreach (var kvp in receivedClock)
            {
                if (_vectorClock.TryGetValue(kvp.Key, out long currentValue))
                {
                    _vectorClock[kvp.Key] = Math.Max(currentValue, kvp.Value);
                }
                else
                {
                    _vectorClock[kvp.Key] = kvp.Value;
                }
            }
        }
    }

    /// <summary>
    /// Gets the current vector clock as a dictionary.
    /// </summary>
    public Dictionary<Guid, long> GetVectorClockCopy()
    {
        lock (_lock)
        {
            return new Dictionary<Guid, long>(_vectorClock);
        }
    }

    /// <summary>
    /// Checks if this peer can edit (is Host or Editor).
    /// </summary>
    public bool CanEdit => Role == CollaborationRole.Host || Role == CollaborationRole.Editor;

    /// <summary>
    /// Checks if this peer is the host.
    /// </summary>
    public bool IsHost => Role == CollaborationRole.Host;

    /// <summary>
    /// Converts peer to PeerInfo for transmission.
    /// </summary>
    public PeerInfo ToPeerInfo()
    {
        return new PeerInfo
        {
            PeerId = Id,
            Name = Name,
            Role = Role,
            Color = Color
        };
    }

    /// <summary>
    /// Marks the peer as seen (updates LastSeen timestamp).
    /// </summary>
    public void MarkSeen()
    {
        lock (_lock)
        {
            _lastSeen = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Gets a color in System.Drawing.Color-compatible format.
    /// </summary>
    /// <returns>Tuple of (A, R, G, B) values.</returns>
    public (byte A, byte R, byte G, byte B) GetColorComponents()
    {
        return (
            (byte)((Color >> 24) & 0xFF),
            (byte)((Color >> 16) & 0xFF),
            (byte)((Color >> 8) & 0xFF),
            (byte)(Color & 0xFF)
        );
    }

    /// <summary>
    /// Sets the color from individual components.
    /// </summary>
    public void SetColor(byte a, byte r, byte g, byte b)
    {
        Color = ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
    }

    /// <summary>
    /// Generates a random color for a peer.
    /// </summary>
    public static uint GenerateRandomColor()
    {
        var random = new Random();
        // Generate bright, saturated colors
        int hue = random.Next(360);
        // Convert HSV to RGB (S=0.7, V=0.9)
        double h = hue / 60.0;
        double c = 0.9 * 0.7;
        double x = c * (1 - Math.Abs(h % 2 - 1));
        double m = 0.9 - c;

        double r, g, b;
        if (h < 1) { r = c; g = x; b = 0; }
        else if (h < 2) { r = x; g = c; b = 0; }
        else if (h < 3) { r = 0; g = c; b = x; }
        else if (h < 4) { r = 0; g = x; b = c; }
        else if (h < 5) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }

        return 0xFF000000 | // Full alpha
               ((uint)((r + m) * 255) << 16) |
               ((uint)((g + m) * 255) << 8) |
               (uint)((b + m) * 255);
    }

    public bool Equals(CollaborationPeer? other)
    {
        if (other is null) return false;
        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as CollaborationPeer);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public override string ToString()
    {
        return $"Peer[{Id:N8}] {Name} ({Role}, {ConnectionState})";
    }
}
