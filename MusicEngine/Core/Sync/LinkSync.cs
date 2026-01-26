// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using Microsoft.Extensions.Logging;


namespace MusicEngine.Core.Sync;


/// <summary>
/// Message types for the Link-style sync protocol.
/// </summary>
public enum LinkMessageType
{
    /// <summary>Peer announcement - sent periodically to maintain presence.</summary>
    Announce,

    /// <summary>Tempo change notification.</summary>
    TempoChange,

    /// <summary>Beat/phase synchronization message.</summary>
    BeatSync,

    /// <summary>Peer leaving the session.</summary>
    Goodbye,

    /// <summary>Request for tempo information from peers.</summary>
    TempoRequest,

    /// <summary>Response to tempo request.</summary>
    TempoResponse
}


/// <summary>
/// Event arguments for peer count changes.
/// </summary>
public class PeersChangedEventArgs : EventArgs
{
    /// <summary>The current number of connected peers.</summary>
    public int Count { get; }

    /// <summary>The peer ID that caused the change (if applicable).</summary>
    public Guid? ChangedPeerId { get; }

    /// <summary>Whether a peer joined (true) or left (false).</summary>
    public bool PeerJoined { get; }

    public PeersChangedEventArgs(int count, Guid? changedPeerId = null, bool peerJoined = true)
    {
        Count = count;
        ChangedPeerId = changedPeerId;
        PeerJoined = peerJoined;
    }
}


/// <summary>
/// Event arguments for tempo changes from the network.
/// </summary>
public class LinkTempoChangedEventArgs : EventArgs
{
    /// <summary>The new tempo in BPM.</summary>
    public double Tempo { get; }

    /// <summary>The peer that initiated the change.</summary>
    public Guid SourcePeerId { get; }

    /// <summary>Whether this change was initiated locally.</summary>
    public bool IsLocal { get; }

    public LinkTempoChangedEventArgs(double tempo, Guid sourcePeerId, bool isLocal)
    {
        Tempo = tempo;
        SourcePeerId = sourcePeerId;
        IsLocal = isLocal;
    }
}


/// <summary>
/// Event arguments for beat synchronization.
/// </summary>
public class BeatSyncEventArgs : EventArgs
{
    /// <summary>The current beat position (fractional).</summary>
    public double BeatPosition { get; }

    /// <summary>The phase within the current quantum (0.0 to Quantum).</summary>
    public double Phase { get; }

    /// <summary>The quantum (beats per bar).</summary>
    public int Quantum { get; }

    /// <summary>The tempo at which this beat was calculated.</summary>
    public double Tempo { get; }

    /// <summary>The timestamp when this sync was calculated.</summary>
    public long TimestampTicks { get; }

    public BeatSyncEventArgs(double beatPosition, double phase, int quantum, double tempo, long timestampTicks)
    {
        BeatPosition = beatPosition;
        Phase = phase;
        Quantum = quantum;
        Tempo = tempo;
        TimestampTicks = timestampTicks;
    }
}


/// <summary>
/// Represents a peer in the Link session.
/// </summary>
internal class LinkPeer
{
    /// <summary>Unique identifier for this peer.</summary>
    public Guid Id { get; init; }

    /// <summary>The peer's IP endpoint.</summary>
    public IPEndPoint EndPoint { get; init; } = null!;

    /// <summary>The peer's current tempo.</summary>
    public double Tempo { get; set; }

    /// <summary>The peer's current beat position.</summary>
    public double BeatPosition { get; set; }

    /// <summary>The peer's quantum setting.</summary>
    public int Quantum { get; set; }

    /// <summary>Last time we heard from this peer.</summary>
    public DateTime LastSeen { get; set; }

    /// <summary>The peer's session start time (for phase alignment).</summary>
    public long SessionStartTicks { get; set; }

    /// <summary>The peer's name (optional).</summary>
    public string? Name { get; set; }
}


/// <summary>
/// Network message for Link protocol.
/// </summary>
internal class LinkMessage
{
    [JsonPropertyName("type")]
    public LinkMessageType Type { get; set; }

    [JsonPropertyName("peerId")]
    public Guid PeerId { get; set; }

    [JsonPropertyName("tempo")]
    public double Tempo { get; set; }

    [JsonPropertyName("beatPosition")]
    public double BeatPosition { get; set; }

    [JsonPropertyName("quantum")]
    public int Quantum { get; set; }

    [JsonPropertyName("timestampTicks")]
    public long TimestampTicks { get; set; }

    [JsonPropertyName("sessionStartTicks")]
    public long SessionStartTicks { get; set; }

    [JsonPropertyName("peerName")]
    public string? PeerName { get; set; }

    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;
}


/// <summary>
/// Provides Ableton Link-style tempo synchronization for MusicEngine instances.
/// Uses UDP multicast for peer discovery and synchronization.
/// </summary>
/// <remarks>
/// This implementation is compatible with other MusicEngine instances but NOT with
/// actual Ableton Link applications, as it uses a simplified JSON-based protocol.
///
/// Default multicast address: 224.76.78.75 (same as Ableton Link)
/// Default port: 20808 (same as Ableton Link)
/// </remarks>
public class LinkSession : IDisposable
{
    /// <summary>Default multicast group address (same as Ableton Link).</summary>
    public const string DefaultMulticastAddress = "224.76.78.75";

    /// <summary>Default multicast port (same as Ableton Link).</summary>
    public const int DefaultPort = 20808;

    /// <summary>Protocol version for compatibility checking.</summary>
    public const int ProtocolVersion = 1;

    /// <summary>Peer timeout in seconds - peers not seen for this long are considered disconnected.</summary>
    public const double PeerTimeoutSeconds = 5.0;

    /// <summary>Announce interval in milliseconds.</summary>
    public const int AnnounceIntervalMs = 1000;

    /// <summary>Beat sync interval in milliseconds.</summary>
    public const int BeatSyncIntervalMs = 50;

    private readonly object _lock = new();
    private readonly Guid _peerId = Guid.NewGuid();
    private readonly ConcurrentDictionary<Guid, LinkPeer> _peers = new();
    private readonly Stopwatch _sessionStopwatch = new();

    // Network components
    private UdpClient? _udpClient;
    private IPEndPoint _multicastEndPoint;
    private IPAddress _multicastAddress;
    private int _port;

    // Timing state
    private double _tempo = 120.0;
    private double _beatPosition;
    private int _quantum = 4;
    private long _sessionStartTicks;

    // Thread management
    private Thread? _receiveThread;
    private Thread? _announceThread;
    private Thread? _beatSyncThread;
    private volatile bool _running;
    private volatile bool _enabled;
    private volatile bool _disposed;

    // Sequencer integration
    private Sequencer? _attachedSequencer;
    private bool _syncToSequencer = true;
    private bool _syncFromSequencer = true;

    // Logging
    private readonly ILogger? _logger;

    // Optional peer name
    private string? _peerName;

    /// <summary>
    /// Fired when the number of connected peers changes.
    /// </summary>
    public event EventHandler<PeersChangedEventArgs>? PeersChanged;

    /// <summary>
    /// Fired when tempo is changed (locally or from network).
    /// </summary>
    public event EventHandler<LinkTempoChangedEventArgs>? TempoChanged;

    /// <summary>
    /// Fired on beat synchronization updates.
    /// </summary>
    public event EventHandler<BeatSyncEventArgs>? BeatSynced;

    /// <summary>
    /// Fired when the session is enabled.
    /// </summary>
    public event EventHandler? Enabled;

    /// <summary>
    /// Fired when the session is disabled.
    /// </summary>
    public event EventHandler? Disabled;

    /// <summary>
    /// Gets whether the Link session is currently enabled.
    /// </summary>
    public bool IsEnabled => _enabled;

    /// <summary>
    /// Gets whether the session is connected to any peers.
    /// </summary>
    public bool IsConnected => _peers.Count > 0;

    /// <summary>
    /// Gets the number of connected peers (excluding self).
    /// </summary>
    public int PeerCount => _peers.Count;

    /// <summary>
    /// Gets this peer's unique identifier.
    /// </summary>
    public Guid PeerId => _peerId;

    /// <summary>
    /// Gets or sets the tempo in BPM. Setting this propagates to all peers.
    /// </summary>
    public double Tempo
    {
        get => _tempo;
        set => SetTempo(value, isLocal: true);
    }

    /// <summary>
    /// Gets the current beat position (fractional).
    /// </summary>
    public double BeatPosition
    {
        get
        {
            lock (_lock)
            {
                return _beatPosition;
            }
        }
    }

    /// <summary>
    /// Gets the current phase within the quantum (0.0 to Quantum).
    /// </summary>
    public double Phase
    {
        get
        {
            lock (_lock)
            {
                return _beatPosition % _quantum;
            }
        }
    }

    /// <summary>
    /// Gets or sets the quantum (beats per bar, typically 4).
    /// </summary>
    public int Quantum
    {
        get => _quantum;
        set
        {
            if (value < 1 || value > 16)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Quantum must be between 1 and 16.");
            }
            _quantum = value;
        }
    }

    /// <summary>
    /// Gets or sets whether to sync tempo to the attached sequencer.
    /// </summary>
    public bool SyncToSequencer
    {
        get => _syncToSequencer;
        set => _syncToSequencer = value;
    }

    /// <summary>
    /// Gets or sets whether to sync tempo from the attached sequencer.
    /// </summary>
    public bool SyncFromSequencer
    {
        get => _syncFromSequencer;
        set => _syncFromSequencer = value;
    }

    /// <summary>
    /// Gets or sets the peer name (displayed to other peers).
    /// </summary>
    public string? PeerName
    {
        get => _peerName;
        set => _peerName = value;
    }

    /// <summary>
    /// Gets the list of connected peer IDs.
    /// </summary>
    public IReadOnlyCollection<Guid> ConnectedPeerIds => _peers.Keys.ToList().AsReadOnly();

    /// <summary>
    /// Creates a new Link session with default settings.
    /// </summary>
    public LinkSession() : this(DefaultMulticastAddress, DefaultPort, null)
    {
    }

    /// <summary>
    /// Creates a new Link session with a logger.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public LinkSession(ILogger? logger) : this(DefaultMulticastAddress, DefaultPort, logger)
    {
    }

    /// <summary>
    /// Creates a new Link session with custom multicast settings.
    /// </summary>
    /// <param name="multicastAddress">The multicast group address.</param>
    /// <param name="port">The UDP port.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public LinkSession(string multicastAddress, int port, ILogger? logger)
    {
        _multicastAddress = IPAddress.Parse(multicastAddress);
        _port = port;
        _multicastEndPoint = new IPEndPoint(_multicastAddress, _port);
        _logger = logger;

        _logger?.LogDebug("LinkSession created with peer ID {PeerId}", _peerId);
    }

    /// <summary>
    /// Enables the Link session and starts synchronization.
    /// </summary>
    public void Enable()
    {
        if (_enabled) return;

        lock (_lock)
        {
            if (_enabled) return;

            try
            {
                // Create UDP client for multicast
                _udpClient = new UdpClient();
                _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, _port));
                _udpClient.JoinMulticastGroup(_multicastAddress);

                // Enable multicast loopback for local testing
                _udpClient.MulticastLoopback = true;

                // Start session timer
                _sessionStartTicks = Stopwatch.GetTimestamp();
                _sessionStopwatch.Restart();

                _enabled = true;
                _running = true;

                // Start background threads
                _receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = "LinkSession-Receive",
                    Priority = ThreadPriority.AboveNormal
                };
                _receiveThread.Start();

                _announceThread = new Thread(AnnounceLoop)
                {
                    IsBackground = true,
                    Name = "LinkSession-Announce"
                };
                _announceThread.Start();

                _beatSyncThread = new Thread(BeatSyncLoop)
                {
                    IsBackground = true,
                    Name = "LinkSession-BeatSync",
                    Priority = ThreadPriority.AboveNormal
                };
                _beatSyncThread.Start();

                _logger?.LogInformation("LinkSession enabled on {Address}:{Port}", _multicastAddress, _port);
                Enabled?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to enable LinkSession");
                _enabled = false;
                _running = false;
                _udpClient?.Dispose();
                _udpClient = null;
                throw;
            }
        }
    }

    /// <summary>
    /// Disables the Link session and stops synchronization.
    /// </summary>
    public void Disable()
    {
        if (!_enabled) return;

        lock (_lock)
        {
            if (!_enabled) return;

            _running = false;

            // Send goodbye message to peers
            try
            {
                SendMessage(new LinkMessage
                {
                    Type = LinkMessageType.Goodbye,
                    PeerId = _peerId,
                    Tempo = _tempo,
                    BeatPosition = _beatPosition,
                    Quantum = _quantum,
                    TimestampTicks = Stopwatch.GetTimestamp(),
                    PeerName = _peerName
                });
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to send goodbye message");
            }

            _enabled = false;

            // Leave multicast group and close socket
            try
            {
                _udpClient?.DropMulticastGroup(_multicastAddress);
                _udpClient?.Close();
                _udpClient?.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error closing UDP client");
            }

            _udpClient = null;

            // Wait for threads to finish
            _receiveThread?.Join(1000);
            _announceThread?.Join(1000);
            _beatSyncThread?.Join(1000);

            // Clear peers
            _peers.Clear();
            _sessionStopwatch.Stop();

            _logger?.LogInformation("LinkSession disabled");
            Disabled?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Attaches this Link session to a sequencer for automatic synchronization.
    /// </summary>
    /// <param name="sequencer">The sequencer to attach to.</param>
    public void AttachToSequencer(Sequencer sequencer)
    {
        if (sequencer == null)
        {
            throw new ArgumentNullException(nameof(sequencer));
        }

        lock (_lock)
        {
            // Detach from previous sequencer if any
            DetachFromSequencer();

            _attachedSequencer = sequencer;

            // Subscribe to sequencer events
            _attachedSequencer.BpmChanged += OnSequencerBpmChanged;
            _attachedSequencer.BeatChanged += OnSequencerBeatChanged;
            _attachedSequencer.PlaybackStarted += OnSequencerPlaybackStarted;
            _attachedSequencer.PlaybackStopped += OnSequencerPlaybackStopped;

            // Sync initial tempo
            if (_syncFromSequencer)
            {
                _tempo = _attachedSequencer.Bpm;
            }
            else if (_syncToSequencer)
            {
                _attachedSequencer.Bpm = _tempo;
            }

            _logger?.LogDebug("Attached to sequencer, tempo: {Tempo} BPM", _tempo);
        }
    }

    /// <summary>
    /// Detaches from the currently attached sequencer.
    /// </summary>
    public void DetachFromSequencer()
    {
        lock (_lock)
        {
            if (_attachedSequencer == null) return;

            _attachedSequencer.BpmChanged -= OnSequencerBpmChanged;
            _attachedSequencer.BeatChanged -= OnSequencerBeatChanged;
            _attachedSequencer.PlaybackStarted -= OnSequencerPlaybackStarted;
            _attachedSequencer.PlaybackStopped -= OnSequencerPlaybackStopped;

            _attachedSequencer = null;
            _logger?.LogDebug("Detached from sequencer");
        }
    }

    /// <summary>
    /// Requests the current beat position, adjusting for phase alignment with peers.
    /// </summary>
    /// <returns>The phase-aligned beat position.</returns>
    public double RequestBeatAtTime()
    {
        lock (_lock)
        {
            return _beatPosition;
        }
    }

    /// <summary>
    /// Forces a phase correction to align with peers.
    /// </summary>
    /// <param name="quantum">The quantum to align to.</param>
    public void ForceBeatAtTime(int? quantum = null)
    {
        int q = quantum ?? _quantum;

        lock (_lock)
        {
            // Align beat position to nearest quantum boundary
            double phase = _beatPosition % q;
            if (phase > q / 2.0)
            {
                _beatPosition = Math.Ceiling(_beatPosition / q) * q;
            }
            else
            {
                _beatPosition = Math.Floor(_beatPosition / q) * q;
            }

            // Update attached sequencer
            if (_attachedSequencer != null && _syncToSequencer)
            {
                _attachedSequencer.CurrentBeat = _beatPosition;
            }
        }

        // Broadcast the new position
        BroadcastBeatSync();
    }

    /// <summary>
    /// Gets information about a connected peer.
    /// </summary>
    /// <param name="peerId">The peer ID.</param>
    /// <returns>The peer's tempo and beat position, or null if not found.</returns>
    public (double Tempo, double BeatPosition, int Quantum)? GetPeerInfo(Guid peerId)
    {
        if (_peers.TryGetValue(peerId, out var peer))
        {
            return (peer.Tempo, peer.BeatPosition, peer.Quantum);
        }
        return null;
    }

    private void SetTempo(double value, bool isLocal)
    {
        if (value < 20.0 || value > 999.0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Tempo must be between 20 and 999 BPM.");
        }

        double oldTempo;
        lock (_lock)
        {
            oldTempo = _tempo;
            if (Math.Abs(oldTempo - value) < 0.001)
            {
                return; // No significant change
            }

            _tempo = value;

            // Update attached sequencer
            if (_attachedSequencer != null && _syncToSequencer && isLocal)
            {
                _attachedSequencer.Bpm = value;
            }
        }

        _logger?.LogDebug("Tempo changed: {OldTempo} -> {NewTempo} BPM (local: {IsLocal})", oldTempo, value, isLocal);

        // Broadcast tempo change to peers
        if (_enabled && isLocal)
        {
            BroadcastTempoChange();
        }

        TempoChanged?.Invoke(this, new LinkTempoChangedEventArgs(value, _peerId, isLocal));
    }

    private void BroadcastTempoChange()
    {
        var message = new LinkMessage
        {
            Type = LinkMessageType.TempoChange,
            PeerId = _peerId,
            Tempo = _tempo,
            BeatPosition = _beatPosition,
            Quantum = _quantum,
            TimestampTicks = Stopwatch.GetTimestamp(),
            SessionStartTicks = _sessionStartTicks,
            PeerName = _peerName,
            Version = ProtocolVersion
        };

        SendMessage(message);
    }

    private void BroadcastBeatSync()
    {
        var message = new LinkMessage
        {
            Type = LinkMessageType.BeatSync,
            PeerId = _peerId,
            Tempo = _tempo,
            BeatPosition = _beatPosition,
            Quantum = _quantum,
            TimestampTicks = Stopwatch.GetTimestamp(),
            SessionStartTicks = _sessionStartTicks,
            PeerName = _peerName,
            Version = ProtocolVersion
        };

        SendMessage(message);
    }

    private void SendMessage(LinkMessage message)
    {
        if (_udpClient == null || !_enabled) return;

        try
        {
            var json = JsonSerializer.Serialize(message);
            var bytes = Encoding.UTF8.GetBytes(json);
            _udpClient.Send(bytes, bytes.Length, _multicastEndPoint);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to send Link message");
        }
    }

    private void ReceiveLoop()
    {
        var receiveEndPoint = new IPEndPoint(IPAddress.Any, 0);

        while (_running && _udpClient != null)
        {
            try
            {
                if (_udpClient.Available > 0)
                {
                    var data = _udpClient.Receive(ref receiveEndPoint);
                    ProcessReceivedData(data, receiveEndPoint);
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut ||
                                              ex.SocketErrorCode == SocketError.Interrupted)
            {
                // Expected during shutdown
            }
            catch (ObjectDisposedException)
            {
                // Socket was closed
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error in receive loop");
            }
        }
    }

    private void ProcessReceivedData(byte[] data, IPEndPoint senderEndPoint)
    {
        try
        {
            var json = Encoding.UTF8.GetString(data);
            var message = JsonSerializer.Deserialize<LinkMessage>(json);

            if (message == null || message.PeerId == _peerId)
            {
                return; // Ignore our own messages
            }

            // Version check
            if (message.Version != ProtocolVersion)
            {
                _logger?.LogWarning("Received message with incompatible version {Version}", message.Version);
                return;
            }

            switch (message.Type)
            {
                case LinkMessageType.Announce:
                    HandleAnnounce(message, senderEndPoint);
                    break;

                case LinkMessageType.TempoChange:
                    HandleTempoChange(message);
                    break;

                case LinkMessageType.BeatSync:
                    HandleBeatSync(message);
                    break;

                case LinkMessageType.Goodbye:
                    HandleGoodbye(message);
                    break;

                case LinkMessageType.TempoRequest:
                    HandleTempoRequest(message, senderEndPoint);
                    break;

                case LinkMessageType.TempoResponse:
                    HandleTempoResponse(message);
                    break;
            }
        }
        catch (JsonException ex)
        {
            _logger?.LogWarning(ex, "Failed to parse Link message");
        }
    }

    private void HandleAnnounce(LinkMessage message, IPEndPoint senderEndPoint)
    {
        bool isNewPeer = !_peers.ContainsKey(message.PeerId);

        var peer = _peers.GetOrAdd(message.PeerId, _ => new LinkPeer
        {
            Id = message.PeerId,
            EndPoint = senderEndPoint
        });

        peer.Tempo = message.Tempo;
        peer.BeatPosition = message.BeatPosition;
        peer.Quantum = message.Quantum;
        peer.LastSeen = DateTime.UtcNow;
        peer.SessionStartTicks = message.SessionStartTicks;
        peer.Name = message.PeerName;

        if (isNewPeer)
        {
            _logger?.LogInformation("New peer joined: {PeerId} ({PeerName}) at {Tempo} BPM",
                message.PeerId, message.PeerName ?? "unnamed", message.Tempo);
            PeersChanged?.Invoke(this, new PeersChangedEventArgs(_peers.Count, message.PeerId, true));
        }
    }

    private void HandleTempoChange(LinkMessage message)
    {
        if (_peers.TryGetValue(message.PeerId, out var peer))
        {
            peer.Tempo = message.Tempo;
            peer.BeatPosition = message.BeatPosition;
            peer.LastSeen = DateTime.UtcNow;
        }

        // Apply tempo change
        lock (_lock)
        {
            if (Math.Abs(_tempo - message.Tempo) > 0.001)
            {
                _tempo = message.Tempo;

                // Update attached sequencer
                if (_attachedSequencer != null && _syncToSequencer)
                {
                    _attachedSequencer.Bpm = message.Tempo;
                }

                _logger?.LogDebug("Tempo changed by peer {PeerId}: {Tempo} BPM", message.PeerId, message.Tempo);
                TempoChanged?.Invoke(this, new LinkTempoChangedEventArgs(message.Tempo, message.PeerId, false));
            }
        }
    }

    private void HandleBeatSync(LinkMessage message)
    {
        if (_peers.TryGetValue(message.PeerId, out var peer))
        {
            peer.BeatPosition = message.BeatPosition;
            peer.Tempo = message.Tempo;
            peer.LastSeen = DateTime.UtcNow;
        }

        // Calculate phase difference and optionally adjust
        BeatSynced?.Invoke(this, new BeatSyncEventArgs(
            message.BeatPosition,
            message.BeatPosition % message.Quantum,
            message.Quantum,
            message.Tempo,
            message.TimestampTicks
        ));
    }

    private void HandleGoodbye(LinkMessage message)
    {
        if (_peers.TryRemove(message.PeerId, out var peer))
        {
            _logger?.LogInformation("Peer left: {PeerId} ({PeerName})",
                message.PeerId, peer.Name ?? "unnamed");
            PeersChanged?.Invoke(this, new PeersChangedEventArgs(_peers.Count, message.PeerId, false));
        }
    }

    private void HandleTempoRequest(LinkMessage message, IPEndPoint senderEndPoint)
    {
        // Respond with our current tempo
        var response = new LinkMessage
        {
            Type = LinkMessageType.TempoResponse,
            PeerId = _peerId,
            Tempo = _tempo,
            BeatPosition = _beatPosition,
            Quantum = _quantum,
            TimestampTicks = Stopwatch.GetTimestamp(),
            SessionStartTicks = _sessionStartTicks,
            PeerName = _peerName,
            Version = ProtocolVersion
        };

        SendMessage(response);
    }

    private void HandleTempoResponse(LinkMessage message)
    {
        // Update peer info
        if (_peers.TryGetValue(message.PeerId, out var peer))
        {
            peer.Tempo = message.Tempo;
            peer.BeatPosition = message.BeatPosition;
            peer.LastSeen = DateTime.UtcNow;
        }
    }

    private void AnnounceLoop()
    {
        while (_running)
        {
            try
            {
                // Send announce message
                var message = new LinkMessage
                {
                    Type = LinkMessageType.Announce,
                    PeerId = _peerId,
                    Tempo = _tempo,
                    BeatPosition = _beatPosition,
                    Quantum = _quantum,
                    TimestampTicks = Stopwatch.GetTimestamp(),
                    SessionStartTicks = _sessionStartTicks,
                    PeerName = _peerName,
                    Version = ProtocolVersion
                };

                SendMessage(message);

                // Clean up stale peers
                CleanupStalePeers();

                Thread.Sleep(AnnounceIntervalMs);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error in announce loop");
            }
        }
    }

    private void CleanupStalePeers()
    {
        var now = DateTime.UtcNow;
        var stalePeers = _peers.Where(kvp => (now - kvp.Value.LastSeen).TotalSeconds > PeerTimeoutSeconds)
                               .Select(kvp => kvp.Key)
                               .ToList();

        foreach (var peerId in stalePeers)
        {
            if (_peers.TryRemove(peerId, out var peer))
            {
                _logger?.LogInformation("Peer timed out: {PeerId} ({PeerName})",
                    peerId, peer.Name ?? "unnamed");
                PeersChanged?.Invoke(this, new PeersChangedEventArgs(_peers.Count, peerId, false));
            }
        }
    }

    private void BeatSyncLoop()
    {
        var lastSyncTime = _sessionStopwatch.Elapsed.TotalSeconds;

        while (_running)
        {
            try
            {
                var currentTime = _sessionStopwatch.Elapsed.TotalSeconds;
                var deltaTime = currentTime - lastSyncTime;
                lastSyncTime = currentTime;

                // Update beat position based on tempo
                lock (_lock)
                {
                    double beatsPerSecond = _tempo / 60.0;
                    _beatPosition += deltaTime * beatsPerSecond;
                }

                // Broadcast beat sync periodically
                BroadcastBeatSync();

                // Fire local beat sync event
                BeatSynced?.Invoke(this, new BeatSyncEventArgs(
                    _beatPosition,
                    _beatPosition % _quantum,
                    _quantum,
                    _tempo,
                    Stopwatch.GetTimestamp()
                ));

                Thread.Sleep(BeatSyncIntervalMs);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error in beat sync loop");
            }
        }
    }

    private void OnSequencerBpmChanged(object? sender, ParameterChangedEventArgs e)
    {
        if (!_syncFromSequencer) return;

        var newBpm = (double)e.NewValue;
        if (Math.Abs(_tempo - newBpm) > 0.001)
        {
            SetTempo(newBpm, isLocal: true);
        }
    }

    private void OnSequencerBeatChanged(object? sender, BeatChangedEventArgs e)
    {
        if (!_syncFromSequencer) return;

        lock (_lock)
        {
            _beatPosition = e.CurrentBeat;
        }
    }

    private void OnSequencerPlaybackStarted(object? sender, PlaybackStateEventArgs e)
    {
        // Reset beat position when playback starts from beginning
        if (e.CurrentBeat < 0.01)
        {
            lock (_lock)
            {
                _beatPosition = 0;
            }
        }
    }

    private void OnSequencerPlaybackStopped(object? sender, PlaybackStateEventArgs e)
    {
        // Optionally sync beat position when stopped
        lock (_lock)
        {
            _beatPosition = e.CurrentBeat;
        }
    }

    /// <summary>
    /// Disposes the Link session and releases all resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Disable();
        DetachFromSequencer();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer.
    /// </summary>
    ~LinkSession()
    {
        Dispose();
    }
}
