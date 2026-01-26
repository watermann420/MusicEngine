// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MusicEngine.Infrastructure.Collaboration;

/// <summary>
/// Event arguments for server connection events.
/// </summary>
public class ServerConnectionEventArgs : EventArgs
{
    /// <summary>The connected client endpoint.</summary>
    public IPEndPoint EndPoint { get; }

    /// <summary>The peer ID if available.</summary>
    public Guid? PeerId { get; }

    public ServerConnectionEventArgs(IPEndPoint endPoint, Guid? peerId = null)
    {
        EndPoint = endPoint;
        PeerId = peerId;
    }
}

/// <summary>
/// Event arguments for received messages.
/// </summary>
public class MessageReceivedEventArgs : EventArgs
{
    /// <summary>The received message.</summary>
    public CollaborationMessage Message { get; }

    /// <summary>The source endpoint.</summary>
    public IPEndPoint Source { get; }

    /// <summary>The connection ID.</summary>
    public Guid ConnectionId { get; }

    public MessageReceivedEventArgs(CollaborationMessage message, IPEndPoint source, Guid connectionId)
    {
        Message = message;
        Source = source;
        ConnectionId = connectionId;
    }
}

/// <summary>
/// Represents a connected client.
/// </summary>
internal class ConnectedClient : IDisposable
{
    public Guid ConnectionId { get; } = Guid.NewGuid();
    public TcpClient TcpClient { get; }
    public NetworkStream Stream { get; }
    public IPEndPoint EndPoint { get; }
    public Guid? PeerId { get; set; }
    public DateTime ConnectedAt { get; } = DateTime.UtcNow;
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    public CancellationTokenSource CancellationTokenSource { get; } = new();
    public bool IsDisposed { get; private set; }

    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public ConnectedClient(TcpClient client)
    {
        TcpClient = client;
        Stream = client.GetStream();
        EndPoint = (IPEndPoint)client.Client.RemoteEndPoint!;
    }

    public async Task SendAsync(byte[] data, CancellationToken cancellationToken)
    {
        if (IsDisposed) return;

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            // Write length prefix (4 bytes, big-endian)
            var lengthPrefix = new byte[4];
            lengthPrefix[0] = (byte)((data.Length >> 24) & 0xFF);
            lengthPrefix[1] = (byte)((data.Length >> 16) & 0xFF);
            lengthPrefix[2] = (byte)((data.Length >> 8) & 0xFF);
            lengthPrefix[3] = (byte)(data.Length & 0xFF);

            await Stream.WriteAsync(lengthPrefix, cancellationToken);
            await Stream.WriteAsync(data, cancellationToken);
            await Stream.FlushAsync(cancellationToken);

            LastActivity = DateTime.UtcNow;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;

        CancellationTokenSource.Cancel();
        CancellationTokenSource.Dispose();
        _writeLock.Dispose();

        try
        {
            Stream.Close();
            TcpClient.Close();
        }
        catch
        {
            // Ignore errors during cleanup
        }
    }
}

/// <summary>
/// TCP server for hosting real-time collaboration sessions.
/// </summary>
public class CollaborationServer : IDisposable
{
    private readonly ILogger? _logger;
    private readonly CollaborationSession _session;
    private readonly ConcurrentDictionary<Guid, ConnectedClient> _clients = new();
    private readonly ConcurrentDictionary<Guid, Guid> _peerToConnection = new();

    private TcpListener? _listener;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _acceptTask;
    private Task? _maintenanceTask;
    private bool _isRunning;
    private bool _disposed;
    private int _port;

    /// <summary>
    /// Gets the port the server is listening on.
    /// </summary>
    public int Port => _port;

    /// <summary>
    /// Gets whether the server is running.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Gets the number of connected clients.
    /// </summary>
    public int ClientCount => _clients.Count;

    /// <summary>
    /// Gets the collaboration session.
    /// </summary>
    public CollaborationSession Session => _session;

    /// <summary>
    /// Fired when a client connects.
    /// </summary>
    public event EventHandler<ServerConnectionEventArgs>? ClientConnected;

    /// <summary>
    /// Fired when a client disconnects.
    /// </summary>
    public event EventHandler<ServerConnectionEventArgs>? ClientDisconnected;

    /// <summary>
    /// Fired when a message is received.
    /// </summary>
    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;

    /// <summary>
    /// Fired when an error occurs.
    /// </summary>
    public event EventHandler<Exception>? Error;

    /// <summary>
    /// Creates a new collaboration server.
    /// </summary>
    /// <param name="session">The collaboration session to host.</param>
    /// <param name="logger">Optional logger.</param>
    public CollaborationServer(CollaborationSession session, ILogger? logger = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _logger = logger;

        // Subscribe to session events
        _session.PeerJoined += OnPeerJoined;
        _session.PeerLeft += OnPeerLeft;
    }

    /// <summary>
    /// Starts the server on the specified port.
    /// </summary>
    /// <param name="port">The port to listen on (0 for any available port).</param>
    /// <param name="bindAddress">The IP address to bind to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StartAsync(int port = 0, IPAddress? bindAddress = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_isRunning)
        {
            throw new InvalidOperationException("Server is already running.");
        }

        bindAddress ??= IPAddress.Any;

        try
        {
            _listener = new TcpListener(bindAddress, port);
            _listener.Start();

            _port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _isRunning = true;

            _logger?.LogInformation("Collaboration server started on {Address}:{Port}", bindAddress, _port);

            // Start accept loop
            _acceptTask = AcceptLoopAsync(_cancellationTokenSource.Token);

            // Start maintenance loop (ping/timeout checking)
            _maintenanceTask = MaintenanceLoopAsync(_cancellationTokenSource.Token);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start collaboration server");
            _isRunning = false;
            throw;
        }
    }

    /// <summary>
    /// Stops the server.
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isRunning) return;

        _isRunning = false;
        _cancellationTokenSource?.Cancel();

        // Close all client connections
        foreach (var client in _clients.Values)
        {
            client.Dispose();
        }
        _clients.Clear();
        _peerToConnection.Clear();

        _listener?.Stop();

        // Wait for tasks to complete
        try
        {
            if (_acceptTask != null)
            {
                await _acceptTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            if (_maintenanceTask != null)
            {
                await _maintenanceTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
        catch (TimeoutException)
        {
            _logger?.LogWarning("Server shutdown timed out");
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;

        _logger?.LogInformation("Collaboration server stopped");
    }

    /// <summary>
    /// Sends a message to a specific client.
    /// </summary>
    public async Task SendToClientAsync(Guid connectionId, CollaborationMessage message, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_clients.TryGetValue(connectionId, out var client))
        {
            var json = message.ToJson();
            var data = Encoding.UTF8.GetBytes(json);

            if (data.Length > CollaborationProtocol.MaxMessageSize)
            {
                throw new InvalidOperationException($"Message exceeds maximum size of {CollaborationProtocol.MaxMessageSize} bytes.");
            }

            await client.SendAsync(data, cancellationToken);
        }
    }

    /// <summary>
    /// Sends a message to a specific peer.
    /// </summary>
    public async Task SendToPeerAsync(Guid peerId, CollaborationMessage message, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_peerToConnection.TryGetValue(peerId, out var connectionId))
        {
            await SendToClientAsync(connectionId, message, cancellationToken);
        }
    }

    /// <summary>
    /// Broadcasts a message to all connected clients.
    /// </summary>
    public async Task BroadcastAsync(CollaborationMessage message, Guid? excludePeerId = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var json = message.ToJson();
        var data = Encoding.UTF8.GetBytes(json);

        if (data.Length > CollaborationProtocol.MaxMessageSize)
        {
            throw new InvalidOperationException($"Message exceeds maximum size of {CollaborationProtocol.MaxMessageSize} bytes.");
        }

        var tasks = new List<Task>();

        foreach (var client in _clients.Values)
        {
            if (excludePeerId.HasValue && client.PeerId == excludePeerId)
            {
                continue;
            }

            tasks.Add(SendDataToClientAsync(client, data, cancellationToken));
        }

        await Task.WhenAll(tasks);
    }

    private async Task SendDataToClientAsync(ConnectedClient client, byte[] data, CancellationToken cancellationToken)
    {
        try
        {
            await client.SendAsync(data, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to send to client {ConnectionId}", client.ConnectionId);
            DisconnectClient(client.ConnectionId);
        }
    }

    /// <summary>
    /// Disconnects a client.
    /// </summary>
    public void DisconnectClient(Guid connectionId)
    {
        if (_clients.TryRemove(connectionId, out var client))
        {
            if (client.PeerId.HasValue)
            {
                _peerToConnection.TryRemove(client.PeerId.Value, out _);
            }

            _logger?.LogInformation("Client {ConnectionId} disconnected from {EndPoint}", connectionId, client.EndPoint);

            ClientDisconnected?.Invoke(this, new ServerConnectionEventArgs(client.EndPoint, client.PeerId));

            client.Dispose();
        }
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener != null)
        {
            try
            {
                var tcpClient = await _listener.AcceptTcpClientAsync(cancellationToken);
                var client = new ConnectedClient(tcpClient);

                if (_clients.TryAdd(client.ConnectionId, client))
                {
                    _logger?.LogInformation("Client connected from {EndPoint}", client.EndPoint);
                    ClientConnected?.Invoke(this, new ServerConnectionEventArgs(client.EndPoint));

                    // Start handling this client
                    _ = HandleClientAsync(client, cancellationToken);
                }
                else
                {
                    client.Dispose();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error accepting connection");
                Error?.Invoke(this, ex);
            }
        }
    }

    private async Task HandleClientAsync(ConnectedClient client, CancellationToken serverCancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            serverCancellationToken, client.CancellationTokenSource.Token);
        var cancellationToken = linkedCts.Token;

        var lengthBuffer = new byte[4];

        try
        {
            while (!cancellationToken.IsCancellationRequested && !client.IsDisposed)
            {
                // Read length prefix
                int bytesRead = 0;
                while (bytesRead < 4)
                {
                    int read = await client.Stream.ReadAsync(
                        lengthBuffer.AsMemory(bytesRead, 4 - bytesRead), cancellationToken);

                    if (read == 0)
                    {
                        // Connection closed
                        return;
                    }
                    bytesRead += read;
                }

                int messageLength = (lengthBuffer[0] << 24) | (lengthBuffer[1] << 16) |
                                   (lengthBuffer[2] << 8) | lengthBuffer[3];

                if (messageLength <= 0 || messageLength > CollaborationProtocol.MaxMessageSize)
                {
                    _logger?.LogWarning("Invalid message length {Length} from {EndPoint}", messageLength, client.EndPoint);
                    break;
                }

                // Read message body
                var messageBuffer = ArrayPool<byte>.Shared.Rent(messageLength);
                try
                {
                    bytesRead = 0;
                    while (bytesRead < messageLength)
                    {
                        int read = await client.Stream.ReadAsync(
                            messageBuffer.AsMemory(bytesRead, messageLength - bytesRead), cancellationToken);

                        if (read == 0)
                        {
                            return;
                        }
                        bytesRead += read;
                    }

                    client.LastActivity = DateTime.UtcNow;

                    // Parse and process message
                    var json = Encoding.UTF8.GetString(messageBuffer, 0, messageLength);
                    var message = CollaborationMessage.FromJson(json);

                    if (message != null)
                    {
                        ProcessMessage(client, message);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(messageBuffer);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch (IOException ex)
        {
            _logger?.LogDebug(ex, "IO error with client {ConnectionId}", client.ConnectionId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error handling client {ConnectionId}", client.ConnectionId);
            Error?.Invoke(this, ex);
        }
        finally
        {
            DisconnectClient(client.ConnectionId);
        }
    }

    private void ProcessMessage(ConnectedClient client, CollaborationMessage message)
    {
        // Track peer ID
        if (message.PeerId != Guid.Empty && client.PeerId != message.PeerId)
        {
            client.PeerId = message.PeerId;
            _peerToConnection[message.PeerId] = client.ConnectionId;
        }

        _logger?.LogDebug("Received {MessageType} from {EndPoint}", message.Type, client.EndPoint);

        // Handle special messages
        switch (message.Type)
        {
            case CollaborationMessageType.Join:
                HandleJoinMessage(client, (JoinMessage)message);
                break;

            case CollaborationMessageType.Leave:
                HandleLeaveMessage(client, (LeaveMessage)message);
                break;

            case CollaborationMessageType.Ping:
                HandlePingMessage(client, (PingMessage)message);
                break;

            default:
                // Relay to session and broadcast to other clients
                _session.HandleChange(message);

                // Broadcast to all except sender
                _ = BroadcastAsync(message, message.PeerId);
                break;
        }

        MessageReceived?.Invoke(this, new MessageReceivedEventArgs(message, client.EndPoint, client.ConnectionId));
    }

    private void HandleJoinMessage(ConnectedClient client, JoinMessage message)
    {
        // Validate password
        if (!string.IsNullOrEmpty(_session.Password) && message.Password != _session.Password)
        {
            var errorMsg = new ErrorMessage
            {
                PeerId = _session.LocalPeer?.Id ?? Guid.Empty,
                SessionId = _session.SessionId,
                ErrorCode = CollaborationProtocol.ErrorCodes.InvalidPassword,
                ErrorDescription = "Invalid session password",
                RelatedMessageId = message.MessageId
            };

            _ = SendToClientAsync(client.ConnectionId, errorMsg);
            DisconnectClient(client.ConnectionId);
            return;
        }

        // Check capacity
        if (_clients.Count >= _session.MaxPeers)
        {
            var errorMsg = new ErrorMessage
            {
                PeerId = _session.LocalPeer?.Id ?? Guid.Empty,
                SessionId = _session.SessionId,
                ErrorCode = CollaborationProtocol.ErrorCodes.SessionFull,
                ErrorDescription = "Session is full",
                RelatedMessageId = message.MessageId
            };

            _ = SendToClientAsync(client.ConnectionId, errorMsg);
            DisconnectClient(client.ConnectionId);
            return;
        }

        // Add peer to session
        _session.HandlePeerJoin(message, client.EndPoint);

        // Send sync response to new peer
        var syncResponse = _session.CreateSyncResponse(null); // Project state would be serialized here
        _ = SendToClientAsync(client.ConnectionId, syncResponse);

        // Broadcast join to other clients
        _ = BroadcastAsync(message, message.PeerId);
    }

    private void HandleLeaveMessage(ConnectedClient client, LeaveMessage message)
    {
        _session.HandlePeerLeave(message);
        _ = BroadcastAsync(message, message.PeerId);
        DisconnectClient(client.ConnectionId);
    }

    private void HandlePingMessage(ConnectedClient client, PingMessage message)
    {
        var pong = _session.HandlePing(message);
        _ = SendToClientAsync(client.ConnectionId, pong);
    }

    private async Task MaintenanceLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(CollaborationProtocol.PingIntervalMs, cancellationToken);

                // Check for timed out clients
                var now = DateTime.UtcNow;
                foreach (var client in _clients.Values)
                {
                    var timeSinceActivity = (now - client.LastActivity).TotalMilliseconds;
                    if (timeSinceActivity > CollaborationProtocol.PeerTimeoutMs)
                    {
                        _logger?.LogWarning("Client {ConnectionId} timed out", client.ConnectionId);
                        DisconnectClient(client.ConnectionId);
                    }
                }

                // Also check session peer timeouts
                _session.CheckPeerTimeouts();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in maintenance loop");
            }
        }
    }

    private void OnPeerJoined(object? sender, PeerEventArgs e)
    {
        _logger?.LogDebug("Session peer joined: {PeerName}", e.Peer.Name);
    }

    private void OnPeerLeft(object? sender, PeerEventArgs e)
    {
        // Find and disconnect the client
        if (_peerToConnection.TryGetValue(e.Peer.Id, out var connectionId))
        {
            DisconnectClient(connectionId);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CollaborationServer));
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
            StopAsync().GetAwaiter().GetResult();

            _session.PeerJoined -= OnPeerJoined;
            _session.PeerLeft -= OnPeerLeft;
        }

        _disposed = true;
    }
}
