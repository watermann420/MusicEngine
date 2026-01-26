// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: External integration component.

using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MusicEngine.Core.Integration;

/// <summary>
/// Event arguments for client connection events.
/// </summary>
internal class LinkClientEventArgs : EventArgs
{
    /// <summary>Gets the connected client.</summary>
    public TcpClient Client { get; }

    /// <summary>Gets the client's unique identifier.</summary>
    public Guid ClientId { get; }

    /// <summary>
    /// Creates new client event args.
    /// </summary>
    public LinkClientEventArgs(TcpClient client, Guid clientId)
    {
        Client = client;
        ClientId = clientId;
    }
}

/// <summary>
/// Event arguments for messages received from clients.
/// </summary>
internal class LinkServerMessageEventArgs : EventArgs
{
    /// <summary>Gets the client that sent the message.</summary>
    public Guid ClientId { get; }

    /// <summary>Gets the received message.</summary>
    public LinkMessage Message { get; }

    /// <summary>
    /// Creates new message event args.
    /// </summary>
    public LinkServerMessageEventArgs(Guid clientId, LinkMessage message)
    {
        ClientId = clientId;
        Message = message;
    }
}

/// <summary>
/// Represents a connected client with its communication state.
/// </summary>
internal class ConnectedClient : IDisposable
{
    public Guid Id { get; }
    public TcpClient TcpClient { get; }
    public NetworkStream Stream { get; }
    public BinaryWriter Writer { get; }
    public BinaryReader Reader { get; }
    public CancellationTokenSource CancellationTokenSource { get; }
    public Task? ReceiveTask { get; set; }
    public DateTime ConnectedAt { get; }
    public DateTime LastActivity { get; set; }
    public string? RemoteEndPoint { get; }
    private readonly object _writeLock = new();
    private bool _disposed;

    public ConnectedClient(TcpClient client)
    {
        Id = Guid.NewGuid();
        TcpClient = client;
        TcpClient.NoDelay = true;
        Stream = client.GetStream();
        Writer = new BinaryWriter(Stream);
        Reader = new BinaryReader(Stream);
        CancellationTokenSource = new CancellationTokenSource();
        ConnectedAt = DateTime.UtcNow;
        LastActivity = DateTime.UtcNow;
        RemoteEndPoint = client.Client.RemoteEndPoint?.ToString();
    }

    public void Send(byte[] data)
    {
        lock (_writeLock)
        {
            if (_disposed)
                return;

            byte[] lengthBytes = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(lengthBytes, data.Length);
            Writer.Write(lengthBytes);
            Writer.Write(data);
            Writer.Flush();
        }
        LastActivity = DateTime.UtcNow;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        CancellationTokenSource.Cancel();
        CancellationTokenSource.Dispose();

        try { Writer.Dispose(); } catch { }
        try { Reader.Dispose(); } catch { }
        try { Stream.Dispose(); } catch { }
        try { TcpClient.Dispose(); } catch { }
    }
}

/// <summary>
/// TCP server for hosting VSTLinkBridge connections.
/// Manages multiple client connections and message broadcasting.
/// </summary>
internal class LinkServer : IDisposable
{
    private readonly int _port;
    private readonly ILogger? _logger;
    private readonly object _lock = new();
    private readonly ConcurrentDictionary<Guid, ConnectedClient> _clients = new();

    private TcpListener? _listener;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _acceptTask;
    private volatile bool _isRunning;
    private volatile bool _disposed;

    /// <summary>
    /// Fired when a client connects to the server.
    /// </summary>
    public event EventHandler<LinkClientEventArgs>? ClientConnected;

    /// <summary>
    /// Fired when a client disconnects from the server.
    /// </summary>
    public event EventHandler<LinkClientEventArgs>? ClientDisconnected;

    /// <summary>
    /// Fired when a message is received from any client.
    /// </summary>
    public event EventHandler<LinkServerMessageEventArgs>? MessageReceived;

    /// <summary>
    /// Fired when an error occurs.
    /// </summary>
    public event EventHandler<Exception>? Error;

    /// <summary>
    /// Gets the port number the server is listening on.
    /// </summary>
    public int Port => _port;

    /// <summary>
    /// Gets whether the server is currently running.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Gets the number of connected clients.
    /// </summary>
    public int ClientCount => _clients.Count;

    /// <summary>
    /// Gets the list of connected client IDs.
    /// </summary>
    public IReadOnlyCollection<Guid> ConnectedClientIds => _clients.Keys.ToList().AsReadOnly();

    /// <summary>
    /// Creates a new LinkServer instance.
    /// </summary>
    /// <param name="port">The TCP port to listen on.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public LinkServer(int port = LinkProtocol.DefaultPort, ILogger? logger = null)
    {
        _port = port;
        _logger = logger;
    }

    /// <summary>
    /// Starts the server and begins accepting connections.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(LinkServer));

        if (_isRunning)
        {
            _logger?.LogWarning("LinkServer is already running");
            return;
        }

        lock (_lock)
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, _port);
                _listener.Start();

                _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _isRunning = true;

                _acceptTask = Task.Run(() => AcceptLoopAsync(_cancellationTokenSource.Token),
                    _cancellationTokenSource.Token);

                _logger?.LogInformation("LinkServer started on port {Port}", _port);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "LinkServer failed to start on port {Port}", _port);
                CleanupResources();
                throw;
            }
        }
    }

    /// <summary>
    /// Starts the server synchronously.
    /// </summary>
    public void Start()
    {
        StartAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Stops the server and disconnects all clients.
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isRunning)
            return;

        _logger?.LogInformation("LinkServer stopping");

        lock (_lock)
        {
            _isRunning = false;
        }

        // Send disconnect to all clients
        var disconnectMessage = new LinkMessage
        {
            Type = LinkMessageType.Disconnect,
            Timestamp = GetTimestamp(),
            Payload = LinkProtocol.CreateDisconnectMessage("Server shutting down")
        };

        foreach (var client in _clients.Values)
        {
            try
            {
                client.Send(disconnectMessage.Serialize());
            }
            catch
            {
                // Ignore errors during shutdown
            }
        }

        _cancellationTokenSource?.Cancel();

        if (_acceptTask != null)
        {
            try
            {
                await _acceptTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        // Wait for all client receive tasks
        foreach (var client in _clients.Values)
        {
            if (client.ReceiveTask != null)
            {
                try
                {
                    await client.ReceiveTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            }
            client.Dispose();
        }
        _clients.Clear();

        CleanupResources();
        _logger?.LogInformation("LinkServer stopped");
    }

    /// <summary>
    /// Stops the server synchronously.
    /// </summary>
    public void Stop()
    {
        StopAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Broadcasts a message to all connected clients.
    /// </summary>
    /// <param name="message">The message to broadcast.</param>
    public void Broadcast(LinkMessage message)
    {
        byte[] data = message.Serialize();

        foreach (var client in _clients.Values)
        {
            try
            {
                client.Send(data);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "LinkServer failed to send to client {ClientId}", client.Id);
            }
        }
    }

    /// <summary>
    /// Broadcasts a message to all connected clients asynchronously.
    /// </summary>
    /// <param name="message">The message to broadcast.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task BroadcastAsync(LinkMessage message, CancellationToken cancellationToken = default)
    {
        byte[] data = message.Serialize();

        var tasks = _clients.Values.Select(client =>
            Task.Run(() =>
            {
                try
                {
                    client.Send(data);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "LinkServer failed to send to client {ClientId}", client.Id);
                }
            }, cancellationToken));

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a message to a specific client.
    /// </summary>
    /// <param name="clientId">The target client's ID.</param>
    /// <param name="message">The message to send.</param>
    /// <returns>True if the message was sent, false if client not found.</returns>
    public bool Send(Guid clientId, LinkMessage message)
    {
        if (!_clients.TryGetValue(clientId, out var client))
            return false;

        try
        {
            client.Send(message.Serialize());
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "LinkServer failed to send to client {ClientId}", clientId);
            return false;
        }
    }

    /// <summary>
    /// Disconnects a specific client.
    /// </summary>
    /// <param name="clientId">The client's ID.</param>
    /// <param name="reason">Optional disconnect reason.</param>
    public void DisconnectClient(Guid clientId, string? reason = null)
    {
        if (!_clients.TryRemove(clientId, out var client))
            return;

        try
        {
            var disconnectMessage = new LinkMessage
            {
                Type = LinkMessageType.Disconnect,
                Timestamp = GetTimestamp(),
                Payload = LinkProtocol.CreateDisconnectMessage(reason ?? "Disconnected by server")
            };
            client.Send(disconnectMessage.Serialize());
        }
        catch
        {
            // Ignore errors during disconnect
        }

        client.Dispose();
        _logger?.LogInformation("LinkServer disconnected client {ClientId}: {Reason}", clientId, reason ?? "No reason");
        ClientDisconnected?.Invoke(this, new LinkClientEventArgs(client.TcpClient, clientId));
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _isRunning && _listener != null)
        {
            try
            {
                var tcpClient = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);

                var client = new ConnectedClient(tcpClient);

                if (_clients.TryAdd(client.Id, client))
                {
                    _logger?.LogInformation("LinkServer accepted client {ClientId} from {EndPoint}",
                        client.Id, client.RemoteEndPoint);

                    client.ReceiveTask = Task.Run(
                        () => ClientReceiveLoopAsync(client, cancellationToken),
                        cancellationToken);

                    ClientConnected?.Invoke(this, new LinkClientEventArgs(tcpClient, client.Id));
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
                if (!cancellationToken.IsCancellationRequested && _isRunning)
                {
                    _logger?.LogError(ex, "LinkServer error accepting connection");
                    Error?.Invoke(this, ex);
                }
            }
        }
    }

    private async Task ClientReceiveLoopAsync(ConnectedClient client, CancellationToken serverCancellation)
    {
        byte[] lengthBuffer = new byte[4];

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            serverCancellation, client.CancellationTokenSource.Token);
        var cancellationToken = linkedCts.Token;

        while (!cancellationToken.IsCancellationRequested && _isRunning)
        {
            try
            {
                // Read length prefix
                int bytesRead = 0;
                while (bytesRead < 4)
                {
                    int read = await client.Stream.ReadAsync(
                        lengthBuffer.AsMemory(bytesRead, 4 - bytesRead),
                        cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        _logger?.LogInformation("LinkServer: Client {ClientId} disconnected", client.Id);
                        break;
                    }
                    bytesRead += read;
                }

                if (bytesRead < 4)
                    break;

                int messageLength = BinaryPrimitives.ReadInt32BigEndian(lengthBuffer);
                if (messageLength <= 0 || messageLength > 10 * 1024 * 1024) // 10MB max
                {
                    _logger?.LogWarning("LinkServer received invalid message length from client {ClientId}: {Length}",
                        client.Id, messageLength);
                    continue;
                }

                // Read message data
                byte[] messageData = new byte[messageLength];
                bytesRead = 0;
                while (bytesRead < messageLength)
                {
                    int read = await client.Stream.ReadAsync(
                        messageData.AsMemory(bytesRead, messageLength - bytesRead),
                        cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                        break;
                    bytesRead += read;
                }

                if (bytesRead < messageLength)
                    break;

                client.LastActivity = DateTime.UtcNow;

                // Parse and dispatch message
                if (LinkMessage.TryDeserialize(messageData, out var message) && message != null)
                {
                    _logger?.LogTrace("LinkServer received from client {ClientId}: {Type}", client.Id, message.Type);

                    if (message.Type == LinkMessageType.Disconnect)
                    {
                        _logger?.LogInformation("LinkServer: Client {ClientId} sent disconnect", client.Id);
                        break;
                    }

                    MessageReceived?.Invoke(this, new LinkServerMessageEventArgs(client.Id, message));
                }
                else
                {
                    _logger?.LogWarning("LinkServer failed to parse message from client {ClientId}", client.Id);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    _logger?.LogWarning(ex, "LinkServer: Connection lost with client {ClientId}", client.Id);
                }
                break;
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    _logger?.LogError(ex, "LinkServer: Error receiving from client {ClientId}", client.Id);
                    Error?.Invoke(this, ex);
                }
            }
        }

        // Client disconnected
        if (_clients.TryRemove(client.Id, out _))
        {
            client.Dispose();
            ClientDisconnected?.Invoke(this, new LinkClientEventArgs(client.TcpClient, client.Id));
        }
    }

    private void CleanupResources()
    {
        lock (_lock)
        {
            _listener?.Stop();
            _listener = null;

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private static long GetTimestamp()
    {
        return System.Diagnostics.Stopwatch.GetTimestamp() * 1_000_000 /
               System.Diagnostics.Stopwatch.Frequency;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the server and releases all resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            Stop();

            foreach (var client in _clients.Values)
            {
                client.Dispose();
            }
            _clients.Clear();
        }

        _disposed = true;
    }

    /// <summary>
    /// Finalizer.
    /// </summary>
    ~LinkServer()
    {
        Dispose(false);
    }
}
