//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Network MIDI session management for RTP-MIDI style communication.
//              Provides UDP multicast peer discovery and TCP reliable MIDI message delivery.


using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;


namespace MusicEngine.Core.Network;


/// <summary>
/// Event arguments for peer connection events.
/// </summary>
public class NetworkMidiPeerEventArgs : EventArgs
{
    /// <summary>Gets the peer involved in the event.</summary>
    public NetworkMidiPeer Peer { get; }

    /// <summary>
    /// Creates new peer event args.
    /// </summary>
    public NetworkMidiPeerEventArgs(NetworkMidiPeer peer)
    {
        Peer = peer;
    }
}


/// <summary>
/// Event arguments for received MIDI messages.
/// </summary>
public class NetworkMidiReceivedEventArgs : EventArgs
{
    /// <summary>Gets the peer that sent the message.</summary>
    public NetworkMidiPeer Peer { get; }

    /// <summary>Gets the received MIDI message.</summary>
    public NetworkMidiMessage Message { get; }

    /// <summary>Gets the local timestamp when the message was received.</summary>
    public long LocalTimestampMicroseconds { get; }

    /// <summary>
    /// Creates new MIDI received event args.
    /// </summary>
    public NetworkMidiReceivedEventArgs(NetworkMidiPeer peer, NetworkMidiMessage message, long localTimestamp)
    {
        Peer = peer;
        Message = message;
        LocalTimestampMicroseconds = localTimestamp;
    }
}


/// <summary>
/// Manages a network MIDI session supporting RTP-MIDI style communication.
/// Uses UDP multicast for peer discovery and TCP for reliable MIDI message delivery.
/// </summary>
/// <remarks>
/// <para>
/// This implementation provides a simplified RTP-MIDI-like protocol for MIDI over network:
/// - UDP multicast is used for session discovery and peer announcements
/// - TCP connections are used for reliable MIDI message delivery
/// - JSON-based discovery protocol for easy debugging
/// - Binary MIDI message format with timestamps for accurate timing
/// </para>
/// <para>
/// Example usage:
/// <code>
/// var session = new NetworkMidiSession("MySession");
/// session.MidiReceived += (s, e) => ProcessMidi(e.Message);
/// session.PeerConnected += (s, e) => Console.WriteLine($"Peer joined: {e.Peer.Name}");
///
/// await session.StartAsync();
///
/// // Send MIDI to all peers
/// session.BroadcastMidi(NetworkMidiMessage.NoteOn(0, 60, 100, session.CurrentTimestamp));
///
/// // Send MIDI to specific peer
/// session.SendMidi(peerId, NetworkMidiMessage.ControlChange(0, 1, 64, session.CurrentTimestamp));
///
/// await session.StopAsync();
/// </code>
/// </para>
/// </remarks>
public class NetworkMidiSession : IDisposable
{
    /// <summary>Default multicast group address for session discovery.</summary>
    public const string DefaultMulticastAddress = "224.0.0.100";

    /// <summary>Default UDP port for discovery.</summary>
    public const int DefaultDiscoveryPort = 5004;

    /// <summary>Default TCP port for MIDI data.</summary>
    public const int DefaultMidiPort = 5005;

    /// <summary>Protocol version for compatibility checking.</summary>
    public const int ProtocolVersion = 1;

    /// <summary>Peer timeout in seconds.</summary>
    public const double PeerTimeoutSeconds = 10.0;

    /// <summary>Announce interval in milliseconds.</summary>
    public const int AnnounceIntervalMs = 2000;

    /// <summary>Heartbeat interval in milliseconds.</summary>
    public const int HeartbeatIntervalMs = 1000;

    private readonly object _lock = new();
    private readonly Guid _sessionId;
    private readonly Guid _localPeerId = Guid.NewGuid();
    private readonly string _sessionName;
    private readonly ConcurrentDictionary<Guid, NetworkMidiPeer> _peers = new();
    private readonly Stopwatch _sessionStopwatch = new();

    // Network components
    private UdpClient? _discoveryClient;
    private TcpListener? _tcpListener;
    private IPEndPoint _multicastEndPoint;
    private IPAddress _multicastAddress;
    private int _discoveryPort;
    private int _midiPort;

    // Threading
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _discoveryReceiveTask;
    private Task? _discoveryAnnounceTask;
    private Task? _tcpAcceptTask;
    private Task? _peerMaintenanceTask;

    // State
    private volatile bool _isRunning;
    private volatile bool _disposed;
    private int _maxPeers = 8;
    private string? _localPeerName;

    // Logging
    private readonly ILogger? _logger;

    /// <summary>
    /// Fired when a new peer connects to the session.
    /// </summary>
    public event EventHandler<NetworkMidiPeerEventArgs>? PeerConnected;

    /// <summary>
    /// Fired when a peer disconnects from the session.
    /// </summary>
    public event EventHandler<NetworkMidiPeerEventArgs>? PeerDisconnected;

    /// <summary>
    /// Fired when a MIDI message is received from any peer.
    /// </summary>
    public event EventHandler<NetworkMidiReceivedEventArgs>? MidiReceived;

    /// <summary>
    /// Fired when the session starts.
    /// </summary>
    public event EventHandler? Started;

    /// <summary>
    /// Fired when the session stops.
    /// </summary>
    public event EventHandler? Stopped;

    /// <summary>
    /// Fired when an error occurs.
    /// </summary>
    public event EventHandler<Exception>? Error;

    /// <summary>
    /// Gets the unique session identifier.
    /// </summary>
    public Guid SessionId => _sessionId;

    /// <summary>
    /// Gets the local peer identifier.
    /// </summary>
    public Guid LocalPeerId => _localPeerId;

    /// <summary>
    /// Gets the session name.
    /// </summary>
    public string SessionName => _sessionName;

    /// <summary>
    /// Gets whether the session is currently running.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Gets whether the session is connected to any peers.
    /// </summary>
    public bool IsConnected => _peers.Count > 0;

    /// <summary>
    /// Gets the current number of connected peers.
    /// </summary>
    public int PeerCount => _peers.Count;

    /// <summary>
    /// Gets the average latency across all connected peers in milliseconds.
    /// </summary>
    public double Latency
    {
        get
        {
            var peers = _peers.Values.ToList();
            if (peers.Count == 0)
                return 0;

            return peers.Average(p => p.LatencyMs);
        }
    }

    /// <summary>
    /// Gets the current session timestamp in microseconds.
    /// </summary>
    public long CurrentTimestamp => GetCurrentMicroseconds();

    /// <summary>
    /// Gets or sets the maximum number of peers allowed in the session.
    /// </summary>
    public int MaxPeers
    {
        get => _maxPeers;
        set
        {
            if (value < 1 || value > 64)
                throw new ArgumentOutOfRangeException(nameof(value), "MaxPeers must be between 1 and 64.");
            _maxPeers = value;
        }
    }

    /// <summary>
    /// Gets or sets the local peer's display name.
    /// </summary>
    public string? LocalPeerName
    {
        get => _localPeerName;
        set => _localPeerName = value;
    }

    /// <summary>
    /// Gets the list of connected peers.
    /// </summary>
    public IReadOnlyCollection<NetworkMidiPeer> ConnectedPeers => _peers.Values.ToList().AsReadOnly();

    /// <summary>
    /// Gets the discovery port.
    /// </summary>
    public int DiscoveryPort => _discoveryPort;

    /// <summary>
    /// Gets the MIDI data port.
    /// </summary>
    public int MidiPort => _midiPort;

    /// <summary>
    /// Creates a new network MIDI session with default settings.
    /// </summary>
    /// <param name="sessionName">The name of the session.</param>
    /// <param name="logger">Optional logger.</param>
    public NetworkMidiSession(string sessionName, ILogger? logger = null)
        : this(sessionName, DefaultMulticastAddress, DefaultDiscoveryPort, DefaultMidiPort, logger)
    {
    }

    /// <summary>
    /// Creates a new network MIDI session with custom settings.
    /// </summary>
    /// <param name="sessionName">The name of the session.</param>
    /// <param name="multicastAddress">The multicast address for discovery.</param>
    /// <param name="discoveryPort">The UDP port for discovery.</param>
    /// <param name="midiPort">The TCP port for MIDI data.</param>
    /// <param name="logger">Optional logger.</param>
    public NetworkMidiSession(string sessionName, string multicastAddress, int discoveryPort, int midiPort,
        ILogger? logger = null)
    {
        if (string.IsNullOrEmpty(sessionName))
            throw new ArgumentException("Session name cannot be null or empty.", nameof(sessionName));

        _sessionName = sessionName;
        _sessionId = Guid.NewGuid();
        _multicastAddress = IPAddress.Parse(multicastAddress);
        _discoveryPort = discoveryPort;
        _midiPort = midiPort;
        _multicastEndPoint = new IPEndPoint(_multicastAddress, _discoveryPort);
        _logger = logger;

        _logger?.LogDebug("NetworkMidiSession created: {SessionName} (ID: {SessionId})", sessionName, _sessionId);
    }

    /// <summary>
    /// Creates a new network MIDI session that joins an existing session.
    /// </summary>
    /// <param name="sessionId">The ID of the session to join.</param>
    /// <param name="sessionName">The name of the session.</param>
    /// <param name="logger">Optional logger.</param>
    public NetworkMidiSession(Guid sessionId, string sessionName, ILogger? logger = null)
        : this(sessionName, logger)
    {
        _sessionId = sessionId;
    }

    /// <summary>
    /// Starts the network MIDI session.
    /// </summary>
    public void Start()
    {
        StartAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Starts the network MIDI session asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(NetworkMidiSession));

        if (_isRunning)
            throw new InvalidOperationException("Session is already running.");

        lock (_lock)
        {
            try
            {
                // Initialize UDP discovery client
                _discoveryClient = new UdpClient();
                _discoveryClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _discoveryClient.Client.Bind(new IPEndPoint(IPAddress.Any, _discoveryPort));
                _discoveryClient.JoinMulticastGroup(_multicastAddress);
                _discoveryClient.MulticastLoopback = true;

                // Initialize TCP listener for incoming connections
                _tcpListener = new TcpListener(IPAddress.Any, _midiPort);
                _tcpListener.Start();

                _sessionStopwatch.Restart();
                _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _isRunning = true;

                // Start background tasks
                _discoveryReceiveTask = Task.Run(() => DiscoveryReceiveLoop(_cancellationTokenSource.Token),
                    _cancellationTokenSource.Token);
                _discoveryAnnounceTask = Task.Run(() => DiscoveryAnnounceLoop(_cancellationTokenSource.Token),
                    _cancellationTokenSource.Token);
                _tcpAcceptTask = Task.Run(() => TcpAcceptLoop(_cancellationTokenSource.Token),
                    _cancellationTokenSource.Token);
                _peerMaintenanceTask = Task.Run(() => PeerMaintenanceLoop(_cancellationTokenSource.Token),
                    _cancellationTokenSource.Token);

                _logger?.LogInformation("NetworkMidiSession started: {SessionName} on discovery port {DiscoveryPort}, MIDI port {MidiPort}",
                    _sessionName, _discoveryPort, _midiPort);

                Started?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to start NetworkMidiSession");
                CleanupResources();
                throw;
            }
        }
    }

    /// <summary>
    /// Stops the network MIDI session.
    /// </summary>
    public void Stop()
    {
        StopAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Stops the network MIDI session asynchronously.
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isRunning)
            return;

        lock (_lock)
        {
            if (!_isRunning)
                return;

            _isRunning = false;
        }

        // Send leave notification to peers
        await BroadcastLeaveAsync().ConfigureAwait(false);

        // Cancel all tasks
        _cancellationTokenSource?.Cancel();

        // Wait for tasks to complete
        var tasks = new List<Task?> { _discoveryReceiveTask, _discoveryAnnounceTask, _tcpAcceptTask, _peerMaintenanceTask };
        foreach (var task in tasks.Where(t => t != null))
        {
            try
            {
                await task!.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        // Disconnect all peers
        foreach (var peer in _peers.Values)
        {
            try
            {
                await peer.DisconnectAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error disconnecting peer {PeerId}", peer.Id);
            }
        }
        _peers.Clear();

        CleanupResources();

        _logger?.LogInformation("NetworkMidiSession stopped: {SessionName}", _sessionName);
        Stopped?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Connects to a specific peer at the given endpoint.
    /// </summary>
    /// <param name="host">The peer's hostname or IP address.</param>
    /// <param name="port">The peer's TCP port.</param>
    /// <param name="timeout">Connection timeout.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The connected peer.</returns>
    public async Task<NetworkMidiPeer> ConnectAsync(string host, int port, TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
            throw new InvalidOperationException("Session is not running.");

        if (_peers.Count >= _maxPeers)
            throw new InvalidOperationException($"Maximum peer count ({_maxPeers}) reached.");

        var peer = new NetworkMidiPeer(_logger);
        peer.StateChanged += OnPeerStateChanged;
        peer.MidiReceived += OnPeerMidiReceived;
        peer.Error += OnPeerError;

        try
        {
            await peer.ConnectAsync(host, port, timeout, cancellationToken).ConfigureAwait(false);

            if (_peers.TryAdd(peer.Id, peer))
            {
                _logger?.LogInformation("Connected to peer at {Host}:{Port}", host, port);
                PeerConnected?.Invoke(this, new NetworkMidiPeerEventArgs(peer));
                return peer;
            }
            else
            {
                await peer.DisconnectAsync().ConfigureAwait(false);
                peer.Dispose();
                throw new InvalidOperationException("Failed to add peer to session.");
            }
        }
        catch
        {
            peer.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Disconnects from a specific peer.
    /// </summary>
    /// <param name="peerId">The peer's ID.</param>
    public async Task DisconnectAsync(Guid peerId)
    {
        if (_peers.TryRemove(peerId, out var peer))
        {
            await peer.DisconnectAsync().ConfigureAwait(false);
            _logger?.LogInformation("Disconnected from peer {PeerId}", peerId);
            PeerDisconnected?.Invoke(this, new NetworkMidiPeerEventArgs(peer));
            peer.Dispose();
        }
    }

    /// <summary>
    /// Sends a MIDI message to a specific peer.
    /// </summary>
    /// <param name="peerId">The target peer's ID.</param>
    /// <param name="message">The MIDI message to send.</param>
    public void SendMidi(Guid peerId, NetworkMidiMessage message)
    {
        if (_peers.TryGetValue(peerId, out var peer) && peer.IsConnected)
        {
            peer.SendMidi(message);
        }
        else
        {
            throw new InvalidOperationException($"Peer {peerId} not found or not connected.");
        }
    }

    /// <summary>
    /// Sends a MIDI message to a specific peer asynchronously.
    /// </summary>
    /// <param name="peerId">The target peer's ID.</param>
    /// <param name="message">The MIDI message to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SendMidiAsync(Guid peerId, NetworkMidiMessage message,
        CancellationToken cancellationToken = default)
    {
        if (_peers.TryGetValue(peerId, out var peer) && peer.IsConnected)
        {
            await peer.SendMidiAsync(message, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            throw new InvalidOperationException($"Peer {peerId} not found or not connected.");
        }
    }

    /// <summary>
    /// Broadcasts a MIDI message to all connected peers.
    /// </summary>
    /// <param name="message">The MIDI message to broadcast.</param>
    public void BroadcastMidi(NetworkMidiMessage message)
    {
        foreach (var peer in _peers.Values.Where(p => p.IsConnected))
        {
            try
            {
                peer.SendMidi(message);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to send MIDI to peer {PeerId}", peer.Id);
            }
        }
    }

    /// <summary>
    /// Broadcasts a MIDI message to all connected peers asynchronously.
    /// </summary>
    /// <param name="message">The MIDI message to broadcast.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task BroadcastMidiAsync(NetworkMidiMessage message, CancellationToken cancellationToken = default)
    {
        var tasks = _peers.Values
            .Where(p => p.IsConnected)
            .Select(p => SendMidiSafeAsync(p, message, cancellationToken));

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task SendMidiSafeAsync(NetworkMidiPeer peer, NetworkMidiMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            await peer.SendMidiAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to send MIDI to peer {PeerId}", peer.Id);
        }
    }

    /// <summary>
    /// Gets a connected peer by ID.
    /// </summary>
    /// <param name="peerId">The peer's ID.</param>
    /// <returns>The peer, or null if not found.</returns>
    public NetworkMidiPeer? GetPeer(Guid peerId)
    {
        _peers.TryGetValue(peerId, out var peer);
        return peer;
    }

    private async Task DiscoveryReceiveLoop(CancellationToken cancellationToken)
    {
        var receiveEndPoint = new IPEndPoint(IPAddress.Any, 0);

        while (!cancellationToken.IsCancellationRequested && _isRunning)
        {
            try
            {
                if (_discoveryClient == null)
                    break;

                if (_discoveryClient.Available > 0)
                {
                    var result = _discoveryClient.Receive(ref receiveEndPoint);
                    await ProcessDiscoveryMessageAsync(result, receiveEndPoint, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await Task.Delay(10, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted ||
                                              ex.SocketErrorCode == SocketError.Interrupted)
            {
                // Expected during shutdown
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger?.LogWarning(ex, "Error in discovery receive loop");
            }
        }
    }

    private async Task ProcessDiscoveryMessageAsync(byte[] data, IPEndPoint sender, CancellationToken cancellationToken)
    {
        try
        {
            var json = Encoding.UTF8.GetString(data);
            var message = NetworkMidiProtocolMessage.FromJson(json);

            if (message == null || message.PeerId == _localPeerId)
                return; // Ignore our own messages

            if (message.Version != ProtocolVersion)
            {
                _logger?.LogWarning("Received discovery message with incompatible version {Version}", message.Version);
                return;
            }

            switch (message.Type)
            {
                case NetworkMidiProtocolMessageType.Announce:
                    await HandleAnnounceAsync(message, sender, cancellationToken).ConfigureAwait(false);
                    break;

                case NetworkMidiProtocolMessageType.JoinRequest:
                    await HandleJoinRequestAsync(message, sender, cancellationToken).ConfigureAwait(false);
                    break;

                case NetworkMidiProtocolMessageType.JoinAccept:
                    await HandleJoinAcceptAsync(message, sender, cancellationToken).ConfigureAwait(false);
                    break;

                case NetworkMidiProtocolMessageType.JoinReject:
                    HandleJoinReject(message);
                    break;

                case NetworkMidiProtocolMessageType.Leave:
                    await HandleLeaveAsync(message).ConfigureAwait(false);
                    break;

                case NetworkMidiProtocolMessageType.Heartbeat:
                    HandleHeartbeat(message);
                    break;
            }
        }
        catch (JsonException ex)
        {
            _logger?.LogWarning(ex, "Failed to parse discovery message");
        }
    }

    private async Task HandleAnnounceAsync(NetworkMidiProtocolMessage message, IPEndPoint sender,
        CancellationToken cancellationToken)
    {
        // Check if this is a session we want to join
        if (message.SessionId != _sessionId && message.SessionName == _sessionName)
        {
            // Found a matching session, request to join
            if (!_peers.ContainsKey(message.PeerId) && _peers.Count < _maxPeers)
            {
                var joinRequest = new NetworkMidiProtocolMessage
                {
                    Type = NetworkMidiProtocolMessageType.JoinRequest,
                    SessionId = message.SessionId,
                    SessionName = _sessionName,
                    PeerId = _localPeerId,
                    PeerName = _localPeerName,
                    TcpPort = _midiPort,
                    TimestampTicks = Stopwatch.GetTimestamp(),
                    Version = ProtocolVersion
                };

                SendDiscoveryMessage(joinRequest);
                _logger?.LogDebug("Sent join request to session {SessionId}", message.SessionId);
            }
        }
    }

    private async Task HandleJoinRequestAsync(NetworkMidiProtocolMessage message, IPEndPoint sender,
        CancellationToken cancellationToken)
    {
        if (message.SessionId != _sessionId)
            return;

        // Check if we can accept
        if (_peers.Count >= _maxPeers)
        {
            var reject = new NetworkMidiProtocolMessage
            {
                Type = NetworkMidiProtocolMessageType.JoinReject,
                SessionId = _sessionId,
                SessionName = _sessionName,
                PeerId = _localPeerId,
                PeerName = _localPeerName,
                TcpPort = _midiPort,
                TimestampTicks = Stopwatch.GetTimestamp(),
                Version = ProtocolVersion,
                RejectReason = "Session is full"
            };
            SendDiscoveryMessage(reject);
            return;
        }

        // Accept the join request
        var accept = new NetworkMidiProtocolMessage
        {
            Type = NetworkMidiProtocolMessageType.JoinAccept,
            SessionId = _sessionId,
            SessionName = _sessionName,
            PeerId = _localPeerId,
            PeerName = _localPeerName,
            TcpPort = _midiPort,
            TimestampTicks = Stopwatch.GetTimestamp(),
            Version = ProtocolVersion,
            MaxPeers = _maxPeers,
            CurrentPeerCount = _peers.Count
        };
        SendDiscoveryMessage(accept);

        // Connect to the peer's TCP port
        try
        {
            var peerHost = sender.Address.ToString();
            var peer = await ConnectAsync(peerHost, message.TcpPort, null, cancellationToken).ConfigureAwait(false);
            peer.Name = message.PeerName;

            _logger?.LogInformation("Accepted join from peer {PeerName} ({PeerId})", message.PeerName, message.PeerId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to connect to peer after accepting join");
        }
    }

    private async Task HandleJoinAcceptAsync(NetworkMidiProtocolMessage message, IPEndPoint sender,
        CancellationToken cancellationToken)
    {
        if (_peers.ContainsKey(message.PeerId))
            return;

        try
        {
            var peerHost = sender.Address.ToString();
            var peer = await ConnectAsync(peerHost, message.TcpPort, null, cancellationToken).ConfigureAwait(false);
            peer.Name = message.PeerName;

            _logger?.LogInformation("Joined session, connected to peer {PeerName} ({PeerId})",
                message.PeerName, message.PeerId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to connect to peer after join accept");
        }
    }

    private void HandleJoinReject(NetworkMidiProtocolMessage message)
    {
        _logger?.LogWarning("Join request rejected by {PeerId}: {Reason}", message.PeerId, message.RejectReason);
    }

    private async Task HandleLeaveAsync(NetworkMidiProtocolMessage message)
    {
        if (_peers.TryRemove(message.PeerId, out var peer))
        {
            await peer.DisconnectAsync().ConfigureAwait(false);
            _logger?.LogInformation("Peer {PeerName} ({PeerId}) left the session", peer.Name, peer.Id);
            PeerDisconnected?.Invoke(this, new NetworkMidiPeerEventArgs(peer));
            peer.Dispose();
        }
    }

    private void HandleHeartbeat(NetworkMidiProtocolMessage message)
    {
        // Update peer last seen time
        if (_peers.TryGetValue(message.PeerId, out var peer))
        {
            // Peer is still alive - latency can be estimated here
        }
    }

    private async Task DiscoveryAnnounceLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _isRunning)
        {
            try
            {
                // Send session announcement
                var announce = new NetworkMidiProtocolMessage
                {
                    Type = NetworkMidiProtocolMessageType.Announce,
                    SessionId = _sessionId,
                    SessionName = _sessionName,
                    PeerId = _localPeerId,
                    PeerName = _localPeerName,
                    TcpPort = _midiPort,
                    TimestampTicks = Stopwatch.GetTimestamp(),
                    Version = ProtocolVersion,
                    MaxPeers = _maxPeers,
                    CurrentPeerCount = _peers.Count
                };
                SendDiscoveryMessage(announce);

                // Send heartbeat to peers
                var heartbeat = new NetworkMidiProtocolMessage
                {
                    Type = NetworkMidiProtocolMessageType.Heartbeat,
                    SessionId = _sessionId,
                    SessionName = _sessionName,
                    PeerId = _localPeerId,
                    PeerName = _localPeerName,
                    TcpPort = _midiPort,
                    TimestampTicks = Stopwatch.GetTimestamp(),
                    Version = ProtocolVersion
                };
                SendDiscoveryMessage(heartbeat);

                await Task.Delay(AnnounceIntervalMs, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger?.LogWarning(ex, "Error in discovery announce loop");
            }
        }
    }

    private async Task TcpAcceptLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _isRunning && _tcpListener != null)
        {
            try
            {
                var client = await _tcpListener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);

                if (_peers.Count >= _maxPeers)
                {
                    _logger?.LogWarning("Rejecting incoming connection: session is full");
                    client.Close();
                    continue;
                }

                var peer = new NetworkMidiPeer(_logger);
                peer.StateChanged += OnPeerStateChanged;
                peer.MidiReceived += OnPeerMidiReceived;
                peer.Error += OnPeerError;

                peer.AcceptConnection(client);

                if (_peers.TryAdd(peer.Id, peer))
                {
                    _logger?.LogInformation("Accepted incoming connection from {EndPoint}", peer.RemoteEndPoint);
                    PeerConnected?.Invoke(this, new NetworkMidiPeerEventArgs(peer));
                }
                else
                {
                    peer.Dispose();
                }
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger?.LogWarning(ex, "Error in TCP accept loop");
            }
        }
    }

    private async Task PeerMaintenanceLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _isRunning)
        {
            try
            {
                var now = DateTime.UtcNow;
                var stalePeers = _peers.Values
                    .Where(p => (now - p.LastSeen).TotalSeconds > PeerTimeoutSeconds || !p.IsConnected)
                    .ToList();

                foreach (var peer in stalePeers)
                {
                    if (_peers.TryRemove(peer.Id, out _))
                    {
                        _logger?.LogInformation("Peer {PeerId} timed out or disconnected", peer.Id);
                        await peer.DisconnectAsync().ConfigureAwait(false);
                        PeerDisconnected?.Invoke(this, new NetworkMidiPeerEventArgs(peer));
                        peer.Dispose();
                    }
                }

                await Task.Delay(HeartbeatIntervalMs, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger?.LogWarning(ex, "Error in peer maintenance loop");
            }
        }
    }

    private void SendDiscoveryMessage(NetworkMidiProtocolMessage message)
    {
        if (_discoveryClient == null || !_isRunning)
            return;

        try
        {
            var json = message.ToJson();
            var bytes = Encoding.UTF8.GetBytes(json);
            _discoveryClient.Send(bytes, bytes.Length, _multicastEndPoint);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to send discovery message");
        }
    }

    private async Task BroadcastLeaveAsync()
    {
        var leave = new NetworkMidiProtocolMessage
        {
            Type = NetworkMidiProtocolMessageType.Leave,
            SessionId = _sessionId,
            SessionName = _sessionName,
            PeerId = _localPeerId,
            PeerName = _localPeerName,
            TcpPort = _midiPort,
            TimestampTicks = Stopwatch.GetTimestamp(),
            Version = ProtocolVersion
        };
        SendDiscoveryMessage(leave);
    }

    private void OnPeerStateChanged(object? sender, PeerStateChangedEventArgs e)
    {
        if (e.NewState == NetworkMidiPeerState.Failed || e.NewState == NetworkMidiPeerState.Disconnected)
        {
            if (_peers.TryRemove(e.Peer.Id, out _))
            {
                PeerDisconnected?.Invoke(this, new NetworkMidiPeerEventArgs(e.Peer));
            }
        }
    }

    private void OnPeerMidiReceived(object? sender, PeerMidiReceivedEventArgs e)
    {
        MidiReceived?.Invoke(this, new NetworkMidiReceivedEventArgs(e.Peer, e.Message, e.LocalTimestampMicroseconds));
    }

    private void OnPeerError(object? sender, Exception e)
    {
        Error?.Invoke(this, e);
    }

    private void CleanupResources()
    {
        lock (_lock)
        {
            _sessionStopwatch.Stop();

            try
            {
                _discoveryClient?.DropMulticastGroup(_multicastAddress);
            }
            catch
            {
                // Ignore
            }

            _discoveryClient?.Close();
            _discoveryClient?.Dispose();
            _discoveryClient = null;

            _tcpListener?.Stop();
            _tcpListener = null;

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private static long GetCurrentMicroseconds()
    {
        return Stopwatch.GetTimestamp() * 1_000_000 / Stopwatch.Frequency;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the session and releases all resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            Stop();

            foreach (var peer in _peers.Values)
            {
                peer.Dispose();
            }
            _peers.Clear();
        }

        _disposed = true;
    }

    /// <summary>
    /// Finalizer.
    /// </summary>
    ~NetworkMidiSession()
    {
        Dispose(false);
    }
}
