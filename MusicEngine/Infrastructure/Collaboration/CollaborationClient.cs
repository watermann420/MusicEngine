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
/// State of the client connection.
/// </summary>
public enum ClientConnectionState
{
    /// <summary>Not connected.</summary>
    Disconnected,

    /// <summary>Attempting to connect.</summary>
    Connecting,

    /// <summary>Connected to server.</summary>
    Connected,

    /// <summary>Connection lost, attempting to reconnect.</summary>
    Reconnecting,

    /// <summary>Connection failed.</summary>
    Failed
}

/// <summary>
/// Event arguments for client connection state changes.
/// </summary>
public class ClientConnectionStateChangedEventArgs : EventArgs
{
    /// <summary>Previous state.</summary>
    public ClientConnectionState PreviousState { get; }

    /// <summary>New state.</summary>
    public ClientConnectionState NewState { get; }

    /// <summary>Error message if applicable.</summary>
    public string? ErrorMessage { get; }

    public ClientConnectionStateChangedEventArgs(ClientConnectionState previousState, ClientConnectionState newState, string? errorMessage = null)
    {
        PreviousState = previousState;
        NewState = newState;
        ErrorMessage = errorMessage;
    }
}

/// <summary>
/// Event arguments for client message events.
/// </summary>
public class ClientMessageEventArgs : EventArgs
{
    /// <summary>The received message.</summary>
    public CollaborationMessage Message { get; }

    public ClientMessageEventArgs(CollaborationMessage message)
    {
        Message = message;
    }
}

/// <summary>
/// TCP client for participating in real-time collaboration sessions.
/// </summary>
public class CollaborationClient : IDisposable
{
    private readonly ILogger? _logger;
    private readonly CollaborationSession _session;
    private readonly ConcurrentQueue<CollaborationMessage> _outgoingQueue = new();
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<AcknowledgeMessage>> _pendingAcks = new();

    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _receiveTask;
    private Task? _sendTask;
    private Task? _pingTask;

    private ClientConnectionState _connectionState = ClientConnectionState.Disconnected;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    private string _serverAddress = string.Empty;
    private int _serverPort;
    private int _reconnectAttempts;
    private long _pingSequence;
    private long _lastPingSentTicks;
    private bool _disposed;

    /// <summary>
    /// Gets the current connection state.
    /// </summary>
    public ClientConnectionState ConnectionState
    {
        get => _connectionState;
        private set
        {
            var oldState = _connectionState;
            if (oldState == value) return;
            _connectionState = value;
            ConnectionStateChanged?.Invoke(this, new ClientConnectionStateChangedEventArgs(oldState, value));
        }
    }

    /// <summary>
    /// Gets whether the client is connected.
    /// </summary>
    public bool IsConnected => _connectionState == ClientConnectionState.Connected;

    /// <summary>
    /// Gets the collaboration session.
    /// </summary>
    public CollaborationSession Session => _session;

    /// <summary>
    /// Gets the server address.
    /// </summary>
    public string ServerAddress => _serverAddress;

    /// <summary>
    /// Gets the server port.
    /// </summary>
    public int ServerPort => _serverPort;

    /// <summary>
    /// Gets or sets whether automatic reconnection is enabled.
    /// </summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>
    /// Gets or sets the connection timeout in milliseconds.
    /// </summary>
    public int ConnectionTimeoutMs { get; set; } = 10000;

    /// <summary>
    /// Fired when the connection state changes.
    /// </summary>
    public event EventHandler<ClientConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <summary>
    /// Fired when a message is received from the server.
    /// </summary>
    public event EventHandler<ClientMessageEventArgs>? MessageReceived;

    /// <summary>
    /// Fired when an error occurs.
    /// </summary>
    public event EventHandler<Exception>? Error;

    /// <summary>
    /// Creates a new collaboration client.
    /// </summary>
    /// <param name="session">The collaboration session.</param>
    /// <param name="logger">Optional logger.</param>
    public CollaborationClient(CollaborationSession session, ILogger? logger = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _logger = logger;
    }

    /// <summary>
    /// Connects to a collaboration server.
    /// </summary>
    /// <param name="address">Server address.</param>
    /// <param name="port">Server port.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ConnectAsync(string address, int port, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _connectLock.WaitAsync(cancellationToken);
        try
        {
            if (_connectionState == ClientConnectionState.Connected)
            {
                throw new InvalidOperationException("Already connected. Disconnect first.");
            }

            _serverAddress = address;
            _serverPort = port;
            _reconnectAttempts = 0;

            await ConnectInternalAsync(cancellationToken);
        }
        finally
        {
            _connectLock.Release();
        }
    }

    private async Task ConnectInternalAsync(CancellationToken cancellationToken)
    {
        ConnectionState = ClientConnectionState.Connecting;

        try
        {
            _tcpClient = new TcpClient();
            _tcpClient.NoDelay = true;
            _tcpClient.ReceiveBufferSize = 65536;
            _tcpClient.SendBufferSize = 65536;

            using var timeoutCts = new CancellationTokenSource(ConnectionTimeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            await _tcpClient.ConnectAsync(_serverAddress, _serverPort, linkedCts.Token);

            _stream = _tcpClient.GetStream();
            _cancellationTokenSource = new CancellationTokenSource();

            ConnectionState = ClientConnectionState.Connected;
            _session.MarkConnected();
            _reconnectAttempts = 0;

            _logger?.LogInformation("Connected to collaboration server at {Address}:{Port}", _serverAddress, _serverPort);

            // Start background tasks
            _receiveTask = ReceiveLoopAsync(_cancellationTokenSource.Token);
            _sendTask = SendLoopAsync(_cancellationTokenSource.Token);
            _pingTask = PingLoopAsync(_cancellationTokenSource.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ConnectionState = ClientConnectionState.Disconnected;
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to connect to {Address}:{Port}", _serverAddress, _serverPort);
            ConnectionState = ClientConnectionState.Failed;

            CleanupConnection();

            if (AutoReconnect && _reconnectAttempts < CollaborationProtocol.MaxReconnectAttempts)
            {
                await TryReconnectAsync(cancellationToken);
            }
            else
            {
                throw;
            }
        }
    }

    private async Task TryReconnectAsync(CancellationToken cancellationToken)
    {
        _reconnectAttempts++;
        ConnectionState = ClientConnectionState.Reconnecting;

        _logger?.LogInformation("Attempting reconnection {Attempt}/{Max} in {Delay}ms",
            _reconnectAttempts, CollaborationProtocol.MaxReconnectAttempts, CollaborationProtocol.ReconnectDelayMs);

        try
        {
            await Task.Delay(CollaborationProtocol.ReconnectDelayMs, cancellationToken);
            await ConnectInternalAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            if (_reconnectAttempts >= CollaborationProtocol.MaxReconnectAttempts)
            {
                _logger?.LogError(ex, "Max reconnection attempts reached");
                ConnectionState = ClientConnectionState.Failed;
                Error?.Invoke(this, ex);
            }
        }
    }

    /// <summary>
    /// Disconnects from the server.
    /// </summary>
    public async Task DisconnectAsync()
    {
        ThrowIfDisposed();

        await _connectLock.WaitAsync();
        try
        {
            if (_connectionState == ClientConnectionState.Disconnected) return;

            // Send leave message
            if (_connectionState == ClientConnectionState.Connected && _session.LocalPeer != null)
            {
                var leaveMessage = new LeaveMessage
                {
                    PeerId = _session.LocalPeer.Id,
                    SessionId = _session.SessionId,
                    Reason = "User disconnected"
                };

                try
                {
                    await SendImmediateAsync(leaveMessage);
                    await Task.Delay(100); // Give time for message to be sent
                }
                catch
                {
                    // Ignore errors when disconnecting
                }
            }

            CleanupConnection();
            ConnectionState = ClientConnectionState.Disconnected;
            _session.LeaveSession("Disconnected from server");

            _logger?.LogInformation("Disconnected from collaboration server");
        }
        finally
        {
            _connectLock.Release();
        }
    }

    private void CleanupConnection()
    {
        _cancellationTokenSource?.Cancel();

        try
        {
            _stream?.Close();
            _tcpClient?.Close();
        }
        catch
        {
            // Ignore cleanup errors
        }

        _stream = null;
        _tcpClient = null;

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;

        // Clear pending acks
        foreach (var tcs in _pendingAcks.Values)
        {
            tcs.TrySetCanceled();
        }
        _pendingAcks.Clear();
    }

    /// <summary>
    /// Sends a message to the server.
    /// </summary>
    /// <param name="message">The message to send.</param>
    public void Send(CollaborationMessage message)
    {
        ThrowIfDisposed();

        if (_connectionState != ClientConnectionState.Connected)
        {
            throw new InvalidOperationException("Not connected to server.");
        }

        _outgoingQueue.Enqueue(message);
    }

    /// <summary>
    /// Sends a message and waits for acknowledgment.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="timeout">Timeout for acknowledgment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The acknowledgment message.</returns>
    public async Task<AcknowledgeMessage> SendAndWaitForAckAsync(CollaborationMessage message,
        TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        timeout ??= TimeSpan.FromSeconds(10);

        var tcs = new TaskCompletionSource<AcknowledgeMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingAcks[message.MessageId] = tcs;

        try
        {
            Send(message);

            using var timeoutCts = new CancellationTokenSource(timeout.Value);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var registration = linkedCts.Token.Register(() => tcs.TrySetCanceled());

            try
            {
                return await tcs.Task;
            }
            finally
            {
                await registration.DisposeAsync();
            }
        }
        finally
        {
            _pendingAcks.TryRemove(message.MessageId, out _);
        }
    }

    /// <summary>
    /// Sends a message immediately without queueing.
    /// </summary>
    private async Task SendImmediateAsync(CollaborationMessage message)
    {
        if (_stream == null || !_tcpClient?.Connected == true) return;

        var json = message.ToJson();
        var data = Encoding.UTF8.GetBytes(json);

        await _writeLock.WaitAsync();
        try
        {
            // Write length prefix
            var lengthPrefix = new byte[4];
            lengthPrefix[0] = (byte)((data.Length >> 24) & 0xFF);
            lengthPrefix[1] = (byte)((data.Length >> 16) & 0xFF);
            lengthPrefix[2] = (byte)((data.Length >> 8) & 0xFF);
            lengthPrefix[3] = (byte)(data.Length & 0xFF);

            await _stream.WriteAsync(lengthPrefix);
            await _stream.WriteAsync(data);
            await _stream.FlushAsync();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Joins the session by sending a join message.
    /// </summary>
    /// <param name="sessionId">Session ID to join.</param>
    /// <param name="peerName">Local peer name.</param>
    /// <param name="password">Session password.</param>
    /// <param name="role">Requested role.</param>
    public async Task JoinSessionAsync(Guid sessionId, string peerName, string? password = null,
        CollaborationRole role = CollaborationRole.Editor)
    {
        ThrowIfDisposed();

        if (_connectionState != ClientConnectionState.Connected)
        {
            throw new InvalidOperationException("Must be connected before joining a session.");
        }

        _session.JoinSession(sessionId, peerName, password, role);

        var joinMessage = new JoinMessage
        {
            PeerId = _session.LocalPeer!.Id,
            SessionId = sessionId,
            PeerName = peerName,
            Role = role,
            Color = _session.LocalPeer.Color,
            Password = password
        };

        Send(joinMessage);

        _logger?.LogInformation("Sent join request for session {SessionId} as {PeerName}", sessionId, peerName);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var lengthBuffer = new byte[4];

        try
        {
            while (!cancellationToken.IsCancellationRequested && _stream != null)
            {
                // Read length prefix
                int bytesRead = 0;
                while (bytesRead < 4)
                {
                    int read = await _stream.ReadAsync(
                        lengthBuffer.AsMemory(bytesRead, 4 - bytesRead), cancellationToken);

                    if (read == 0)
                    {
                        _logger?.LogWarning("Server closed connection");
                        HandleConnectionLost();
                        return;
                    }
                    bytesRead += read;
                }

                int messageLength = (lengthBuffer[0] << 24) | (lengthBuffer[1] << 16) |
                                   (lengthBuffer[2] << 8) | lengthBuffer[3];

                if (messageLength <= 0 || messageLength > CollaborationProtocol.MaxMessageSize)
                {
                    _logger?.LogWarning("Invalid message length {Length}", messageLength);
                    continue;
                }

                // Read message body
                var messageBuffer = ArrayPool<byte>.Shared.Rent(messageLength);
                try
                {
                    bytesRead = 0;
                    while (bytesRead < messageLength)
                    {
                        int read = await _stream.ReadAsync(
                            messageBuffer.AsMemory(bytesRead, messageLength - bytesRead), cancellationToken);

                        if (read == 0)
                        {
                            _logger?.LogWarning("Server closed connection during message read");
                            HandleConnectionLost();
                            return;
                        }
                        bytesRead += read;
                    }

                    var json = Encoding.UTF8.GetString(messageBuffer, 0, messageLength);
                    var message = CollaborationMessage.FromJson(json);

                    if (message != null)
                    {
                        ProcessReceivedMessage(message);
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
            _logger?.LogWarning(ex, "IO error in receive loop");
            HandleConnectionLost();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in receive loop");
            Error?.Invoke(this, ex);
            HandleConnectionLost();
        }
    }

    private void ProcessReceivedMessage(CollaborationMessage message)
    {
        _logger?.LogDebug("Received {MessageType} from server", message.Type);

        // Handle acknowledgments
        if (message.Type == CollaborationMessageType.Acknowledge)
        {
            var ack = (AcknowledgeMessage)message;
            if (_pendingAcks.TryRemove(ack.AcknowledgedMessageId, out var tcs))
            {
                tcs.TrySetResult(ack);
            }
            return;
        }

        // Handle pong
        if (message.Type == CollaborationMessageType.Pong)
        {
            var pong = (PongMessage)message;
            _session.HandlePong(pong, _lastPingSentTicks);
            return;
        }

        // Handle error
        if (message.Type == CollaborationMessageType.Error)
        {
            var error = (ErrorMessage)message;
            _logger?.LogWarning("Server error: {Code} - {Description}", error.ErrorCode, error.ErrorDescription);

            if (error.ErrorCode == CollaborationProtocol.ErrorCodes.InvalidPassword ||
                error.ErrorCode == CollaborationProtocol.ErrorCodes.SessionFull)
            {
                // Fatal errors - don't reconnect
                AutoReconnect = false;
            }
            return;
        }

        // Handle sync response
        if (message.Type == CollaborationMessageType.SyncResponse)
        {
            var syncResponse = (SyncResponseMessage)message;
            // Apply sync state (would be handled by the session/project)
            _logger?.LogInformation("Received sync response with {PeerCount} peers", syncResponse.Peers?.Count ?? 0);
        }

        // Handle join (other peer joined)
        if (message.Type == CollaborationMessageType.Join)
        {
            _session.HandlePeerJoin((JoinMessage)message);
        }

        // Handle leave
        if (message.Type == CollaborationMessageType.Leave)
        {
            _session.HandlePeerLeave((LeaveMessage)message);
        }

        // Pass to session for handling
        _session.HandleChange(message);

        MessageReceived?.Invoke(this, new ClientMessageEventArgs(message));
    }

    private async Task SendLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_outgoingQueue.TryDequeue(out var message))
                {
                    await SendImmediateAsync(message);
                }
                else
                {
                    await Task.Delay(1, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in send loop");
                HandleConnectionLost();
                break;
            }
        }
    }

    private async Task PingLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(CollaborationProtocol.PingIntervalMs, cancellationToken);

                if (_connectionState != ClientConnectionState.Connected) continue;

                var ping = new PingMessage
                {
                    PeerId = _session.LocalPeer?.Id ?? Guid.Empty,
                    SessionId = _session.SessionId,
                    Sequence = Interlocked.Increment(ref _pingSequence)
                };

                _lastPingSentTicks = DateTime.UtcNow.Ticks;
                Send(ping);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error in ping loop");
            }
        }
    }

    private void HandleConnectionLost()
    {
        if (_connectionState == ClientConnectionState.Disconnected ||
            _connectionState == ClientConnectionState.Reconnecting)
        {
            return;
        }

        CleanupConnection();

        if (AutoReconnect && _reconnectAttempts < CollaborationProtocol.MaxReconnectAttempts)
        {
            _ = TryReconnectAsync(CancellationToken.None);
        }
        else
        {
            ConnectionState = ClientConnectionState.Failed;
            _session.LeaveSession("Connection lost");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CollaborationClient));
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
            AutoReconnect = false;
            DisconnectAsync().GetAwaiter().GetResult();
            _writeLock.Dispose();
            _connectLock.Dispose();
        }

        _disposed = true;
    }
}
