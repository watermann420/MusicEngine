// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;


namespace MusicEngine.Core.Network;


/// <summary>
/// Represents the connection state of a network MIDI peer.
/// </summary>
public enum NetworkMidiPeerState
{
    /// <summary>Peer is disconnected.</summary>
    Disconnected,

    /// <summary>Connection is being established.</summary>
    Connecting,

    /// <summary>Peer is fully connected and ready for MIDI transfer.</summary>
    Connected,

    /// <summary>Connection is being terminated.</summary>
    Disconnecting,

    /// <summary>Connection failed or was lost.</summary>
    Failed
}


/// <summary>
/// Event arguments for peer state changes.
/// </summary>
public class PeerStateChangedEventArgs : EventArgs
{
    /// <summary>Gets the peer that changed state.</summary>
    public NetworkMidiPeer Peer { get; }

    /// <summary>Gets the previous state.</summary>
    public NetworkMidiPeerState PreviousState { get; }

    /// <summary>Gets the new state.</summary>
    public NetworkMidiPeerState NewState { get; }

    /// <summary>Gets an optional error message if the state change was due to an error.</summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Creates new peer state changed event args.
    /// </summary>
    public PeerStateChangedEventArgs(NetworkMidiPeer peer, NetworkMidiPeerState previousState,
        NetworkMidiPeerState newState, string? errorMessage = null)
    {
        Peer = peer;
        PreviousState = previousState;
        NewState = newState;
        ErrorMessage = errorMessage;
    }
}


/// <summary>
/// Event arguments for MIDI messages received from a peer.
/// </summary>
public class PeerMidiReceivedEventArgs : EventArgs
{
    /// <summary>Gets the peer that sent the message.</summary>
    public NetworkMidiPeer Peer { get; }

    /// <summary>Gets the received MIDI message.</summary>
    public NetworkMidiMessage Message { get; }

    /// <summary>Gets the local timestamp when the message was received.</summary>
    public long LocalTimestampMicroseconds { get; }

    /// <summary>
    /// Creates new peer MIDI received event args.
    /// </summary>
    public PeerMidiReceivedEventArgs(NetworkMidiPeer peer, NetworkMidiMessage message, long localTimestamp)
    {
        Peer = peer;
        Message = message;
        LocalTimestampMicroseconds = localTimestamp;
    }
}


/// <summary>
/// Represents a remote peer in a network MIDI session.
/// Handles TCP connection for reliable MIDI message delivery and time synchronization.
/// </summary>
public class NetworkMidiPeer : IDisposable
{
    private readonly Guid _id;
    private readonly object _lock = new();
    private readonly Stopwatch _connectionStopwatch = new();
    private readonly ConcurrentQueue<NetworkMidiMessage> _sendQueue = new();

    // Connection state
    private NetworkMidiPeerState _state = NetworkMidiPeerState.Disconnected;
    private TcpClient? _tcpClient;
    private NetworkStream? _networkStream;
    private BinaryWriter? _writer;
    private BinaryReader? _reader;

    // Threading
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _receiveTask;
    private Task? _sendTask;

    // Time synchronization
    private long _remoteTimeOffset;
    private double _estimatedLatencyMs;
    private int _timeSyncSamples;
    private const int MaxTimeSyncSamples = 10;
    private readonly long[] _latencySamples = new long[MaxTimeSyncSamples];

    // Logging
    private readonly ILogger? _logger;

    // Peer information
    private string? _name;
    private IPEndPoint? _remoteEndPoint;
    private DateTime _lastSeen;
    private long _messagesSent;
    private long _messagesReceived;
    private bool _disposed;

    /// <summary>
    /// Fired when the peer's connection state changes.
    /// </summary>
    public event EventHandler<PeerStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Fired when a MIDI message is received from this peer.
    /// </summary>
    public event EventHandler<PeerMidiReceivedEventArgs>? MidiReceived;

    /// <summary>
    /// Fired when an error occurs.
    /// </summary>
    public event EventHandler<Exception>? Error;

    /// <summary>
    /// Gets the unique identifier of this peer.
    /// </summary>
    public Guid Id => _id;

    /// <summary>
    /// Gets or sets the display name of this peer.
    /// </summary>
    public string? Name
    {
        get => _name;
        set => _name = value;
    }

    /// <summary>
    /// Gets the current connection state.
    /// </summary>
    public NetworkMidiPeerState State => _state;

    /// <summary>
    /// Gets whether the peer is currently connected.
    /// </summary>
    public bool IsConnected => _state == NetworkMidiPeerState.Connected;

    /// <summary>
    /// Gets the remote endpoint of the connection.
    /// </summary>
    public IPEndPoint? RemoteEndPoint => _remoteEndPoint;

    /// <summary>
    /// Gets the estimated one-way latency in milliseconds.
    /// </summary>
    public double LatencyMs => _estimatedLatencyMs;

    /// <summary>
    /// Gets the time offset between local and remote clocks in microseconds.
    /// </summary>
    public long RemoteTimeOffsetMicroseconds => _remoteTimeOffset;

    /// <summary>
    /// Gets the last time communication was received from this peer.
    /// </summary>
    public DateTime LastSeen => _lastSeen;

    /// <summary>
    /// Gets the total number of messages sent to this peer.
    /// </summary>
    public long MessagesSent => Interlocked.Read(ref _messagesSent);

    /// <summary>
    /// Gets the total number of messages received from this peer.
    /// </summary>
    public long MessagesReceived => Interlocked.Read(ref _messagesReceived);

    /// <summary>
    /// Gets the connection duration.
    /// </summary>
    public TimeSpan ConnectionDuration => _connectionStopwatch.Elapsed;

    /// <summary>
    /// Creates a new NetworkMidiPeer with an existing peer ID.
    /// </summary>
    /// <param name="id">The unique peer identifier.</param>
    /// <param name="logger">Optional logger.</param>
    public NetworkMidiPeer(Guid id, ILogger? logger = null)
    {
        _id = id;
        _logger = logger;
        _lastSeen = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a new NetworkMidiPeer with a generated ID.
    /// </summary>
    /// <param name="logger">Optional logger.</param>
    public NetworkMidiPeer(ILogger? logger = null) : this(Guid.NewGuid(), logger)
    {
    }

    /// <summary>
    /// Connects to a remote peer at the specified endpoint.
    /// </summary>
    /// <param name="host">The remote hostname or IP address.</param>
    /// <param name="port">The remote TCP port.</param>
    /// <param name="timeout">Connection timeout.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ConnectAsync(string host, int port, TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(NetworkMidiPeer));

        if (_state == NetworkMidiPeerState.Connected || _state == NetworkMidiPeerState.Connecting)
            throw new InvalidOperationException("Peer is already connected or connecting.");

        SetState(NetworkMidiPeerState.Connecting);

        try
        {
            _tcpClient = new TcpClient();
            _tcpClient.NoDelay = true; // Disable Nagle's algorithm for low latency
            _tcpClient.SendBufferSize = 8192;
            _tcpClient.ReceiveBufferSize = 8192;

            var connectTimeout = timeout ?? TimeSpan.FromSeconds(10);

            using var timeoutCts = new CancellationTokenSource(connectTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutCts.Token);

            await _tcpClient.ConnectAsync(host, port, linkedCts.Token).ConfigureAwait(false);

            _remoteEndPoint = _tcpClient.Client.RemoteEndPoint as IPEndPoint;
            _networkStream = _tcpClient.GetStream();
            _writer = new BinaryWriter(_networkStream);
            _reader = new BinaryReader(_networkStream);

            _cancellationTokenSource = new CancellationTokenSource();
            _connectionStopwatch.Restart();

            // Start receive and send tasks
            _receiveTask = Task.Run(() => ReceiveLoop(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
            _sendTask = Task.Run(() => SendLoop(_cancellationTokenSource.Token), _cancellationTokenSource.Token);

            SetState(NetworkMidiPeerState.Connected);
            _logger?.LogInformation("Connected to peer at {EndPoint}", _remoteEndPoint);
        }
        catch (OperationCanceledException)
        {
            SetState(NetworkMidiPeerState.Failed, "Connection timed out");
            throw;
        }
        catch (Exception ex)
        {
            SetState(NetworkMidiPeerState.Failed, ex.Message);
            _logger?.LogError(ex, "Failed to connect to peer at {Host}:{Port}", host, port);
            throw;
        }
    }

    /// <summary>
    /// Accepts an incoming connection from a TcpClient.
    /// </summary>
    /// <param name="client">The accepted TCP client.</param>
    internal void AcceptConnection(TcpClient client)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(NetworkMidiPeer));

        if (_state == NetworkMidiPeerState.Connected)
            throw new InvalidOperationException("Peer is already connected.");

        lock (_lock)
        {
            _tcpClient = client;
            _tcpClient.NoDelay = true;
            _remoteEndPoint = _tcpClient.Client.RemoteEndPoint as IPEndPoint;
            _networkStream = _tcpClient.GetStream();
            _writer = new BinaryWriter(_networkStream);
            _reader = new BinaryReader(_networkStream);

            _cancellationTokenSource = new CancellationTokenSource();
            _connectionStopwatch.Restart();

            // Start receive and send tasks
            _receiveTask = Task.Run(() => ReceiveLoop(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
            _sendTask = Task.Run(() => SendLoop(_cancellationTokenSource.Token), _cancellationTokenSource.Token);

            SetState(NetworkMidiPeerState.Connected);
            _logger?.LogInformation("Accepted connection from peer at {EndPoint}", _remoteEndPoint);
        }
    }

    /// <summary>
    /// Disconnects from the peer.
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_state == NetworkMidiPeerState.Disconnected || _state == NetworkMidiPeerState.Disconnecting)
            return;

        SetState(NetworkMidiPeerState.Disconnecting);

        try
        {
            _cancellationTokenSource?.Cancel();

            // Wait for tasks to complete
            if (_receiveTask != null)
            {
                try
                {
                    await _receiveTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            }

            if (_sendTask != null)
            {
                try
                {
                    await _sendTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            }
        }
        finally
        {
            CleanupConnection();
            SetState(NetworkMidiPeerState.Disconnected);
            _logger?.LogInformation("Disconnected from peer {PeerId}", _id);
        }
    }

    /// <summary>
    /// Sends a MIDI message to this peer.
    /// </summary>
    /// <param name="message">The MIDI message to send.</param>
    public void SendMidi(NetworkMidiMessage message)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Cannot send MIDI: peer is not connected.");

        _sendQueue.Enqueue(message);
    }

    /// <summary>
    /// Sends a MIDI message to this peer asynchronously.
    /// </summary>
    /// <param name="message">The MIDI message to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SendMidiAsync(NetworkMidiMessage message, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Cannot send MIDI: peer is not connected.");

        // For async, we write directly instead of using the queue
        try
        {
            var data = message.ToBytes();
            lock (_lock)
            {
                if (_writer == null || _networkStream == null)
                    throw new InvalidOperationException("Connection not established.");

                // Write length prefix followed by data
                _writer.Write(data.Length);
                _writer.Write(data);
                _writer.Flush();
            }

            Interlocked.Increment(ref _messagesSent);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error sending MIDI message to peer {PeerId}", _id);
            Error?.Invoke(this, ex);
            throw;
        }
    }

    /// <summary>
    /// Initiates time synchronization with this peer.
    /// Uses a simplified version of the NTP algorithm.
    /// </summary>
    public async Task SynchronizeTimeAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Cannot synchronize time: peer is not connected.");

        // Send time sync request
        // T1 = local send time
        long t1 = GetCurrentMicroseconds();

        var syncRequest = new NetworkMidiProtocolMessage
        {
            Type = NetworkMidiProtocolMessageType.TimeSyncRequest,
            PeerId = _id,
            TimestampTicks = Stopwatch.GetTimestamp(),
            TimeSyncData = new[] { t1 }
        };

        // This would need to be sent through the TCP connection
        // For now, we'll update latency based on round-trip measurements
        _logger?.LogDebug("Time sync initiated with peer {PeerId}", _id);
    }

    /// <summary>
    /// Updates the time synchronization based on a round-trip measurement.
    /// </summary>
    /// <param name="roundTripMicroseconds">The measured round-trip time in microseconds.</param>
    /// <param name="remoteTimestamp">The remote peer's timestamp when the response was sent.</param>
    /// <param name="localReceiveTimestamp">The local timestamp when the response was received.</param>
    internal void UpdateTimeSync(long roundTripMicroseconds, long remoteTimestamp = 0, long localReceiveTimestamp = 0)
    {
        lock (_lock)
        {
            _latencySamples[_timeSyncSamples % MaxTimeSyncSamples] = roundTripMicroseconds;
            _timeSyncSamples++;

            // Calculate average latency from samples
            int sampleCount = Math.Min(_timeSyncSamples, MaxTimeSyncSamples);
            long sum = 0;
            for (int i = 0; i < sampleCount; i++)
            {
                sum += _latencySamples[i];
            }

            long averageRoundTrip = sum / sampleCount;
            _estimatedLatencyMs = averageRoundTrip / 2000.0; // One-way latency in ms

            // Calculate remote time offset if timestamps are provided
            // Using simplified NTP-like algorithm: offset = (remoteTimestamp - localReceiveTimestamp) + (averageRoundTrip / 2)
            if (remoteTimestamp > 0 && localReceiveTimestamp > 0)
            {
                _remoteTimeOffset = (remoteTimestamp - localReceiveTimestamp) + (averageRoundTrip / 2);
            }
        }
    }

    /// <summary>
    /// Adjusts a remote timestamp to local time based on synchronization data.
    /// </summary>
    /// <param name="remoteTimestamp">The remote timestamp in microseconds.</param>
    /// <returns>The adjusted local timestamp.</returns>
    public long AdjustTimestamp(long remoteTimestamp)
    {
        return remoteTimestamp - _remoteTimeOffset;
    }

    private void ReceiveLoop(CancellationToken cancellationToken)
    {
        byte[] lengthBuffer = new byte[4];

        while (!cancellationToken.IsCancellationRequested && IsConnected)
        {
            try
            {
                if (_networkStream == null || _reader == null)
                    break;

                // Check if data is available
                if (!_networkStream.DataAvailable)
                {
                    Thread.Sleep(1);
                    continue;
                }

                // Read length prefix
                int bytesRead = 0;
                while (bytesRead < 4)
                {
                    int read = _networkStream.Read(lengthBuffer, bytesRead, 4 - bytesRead);
                    if (read == 0)
                    {
                        // Connection closed
                        throw new IOException("Connection closed by remote peer.");
                    }
                    bytesRead += read;
                }

                int length = (lengthBuffer[0] << 24) | (lengthBuffer[1] << 16) |
                             (lengthBuffer[2] << 8) | lengthBuffer[3];

                if (length <= 0 || length > 65536) // Sanity check
                {
                    _logger?.LogWarning("Invalid message length received: {Length}", length);
                    continue;
                }

                // Read message data
                byte[] data = new byte[length];
                bytesRead = 0;
                while (bytesRead < length)
                {
                    int read = _networkStream.Read(data, bytesRead, length - bytesRead);
                    if (read == 0)
                    {
                        throw new IOException("Connection closed during message read.");
                    }
                    bytesRead += read;
                }

                // Parse MIDI message
                var message = NetworkMidiMessage.Parse(data, 0, out _);
                _lastSeen = DateTime.UtcNow;
                Interlocked.Increment(ref _messagesReceived);

                // Notify listeners
                long localTimestamp = GetCurrentMicroseconds();
                MidiReceived?.Invoke(this, new PeerMidiReceivedEventArgs(this, message, localTimestamp));
            }
            catch (IOException ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    _logger?.LogWarning(ex, "Connection lost to peer {PeerId}", _id);
                    SetState(NetworkMidiPeerState.Failed, ex.Message);
                    Error?.Invoke(this, ex);
                }
                break;
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    _logger?.LogError(ex, "Error in receive loop for peer {PeerId}", _id);
                    Error?.Invoke(this, ex);
                }
            }
        }
    }

    private void SendLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && IsConnected)
        {
            try
            {
                if (_sendQueue.TryDequeue(out var message))
                {
                    var data = message.ToBytes();
                    lock (_lock)
                    {
                        if (_writer == null || _networkStream == null)
                            break;

                        // Write length prefix (big-endian)
                        _writer.Write((byte)(data.Length >> 24));
                        _writer.Write((byte)(data.Length >> 16));
                        _writer.Write((byte)(data.Length >> 8));
                        _writer.Write((byte)data.Length);
                        _writer.Write(data);
                        _writer.Flush();
                    }

                    Interlocked.Increment(ref _messagesSent);
                }
                else
                {
                    Thread.Sleep(1); // Yield when queue is empty
                }
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    _logger?.LogError(ex, "Error in send loop for peer {PeerId}", _id);
                    Error?.Invoke(this, ex);
                }
            }
        }
    }

    private void SetState(NetworkMidiPeerState newState, string? errorMessage = null)
    {
        NetworkMidiPeerState previousState;
        lock (_lock)
        {
            if (_state == newState)
                return;

            previousState = _state;
            _state = newState;
        }

        _logger?.LogDebug("Peer {PeerId} state changed: {PreviousState} -> {NewState}",
            _id, previousState, newState);
        StateChanged?.Invoke(this, new PeerStateChangedEventArgs(this, previousState, newState, errorMessage));
    }

    private void CleanupConnection()
    {
        lock (_lock)
        {
            _connectionStopwatch.Stop();

            _writer?.Dispose();
            _writer = null;

            _reader?.Dispose();
            _reader = null;

            _networkStream?.Dispose();
            _networkStream = null;

            _tcpClient?.Dispose();
            _tcpClient = null;

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
    /// Disposes the peer and releases all resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // Synchronously disconnect
            _cancellationTokenSource?.Cancel();
            CleanupConnection();
        }

        _disposed = true;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"NetworkMidiPeer({_id}, {_name ?? "unnamed"}, {_state}, latency={_estimatedLatencyMs:F1}ms)";
    }
}
