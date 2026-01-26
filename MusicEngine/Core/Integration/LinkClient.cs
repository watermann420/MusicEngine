// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: External integration component.

using System;
using System.Buffers.Binary;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MusicEngine.Core.Integration;

/// <summary>
/// TCP client for connecting to a VSTLinkBridge host.
/// Handles connection management, message sending, and receiving.
/// </summary>
internal class LinkClient : IDisposable
{
    private readonly object _lock = new();
    private readonly ILogger? _logger;

    private TcpClient? _client;
    private NetworkStream? _stream;
    private BinaryReader? _reader;
    private BinaryWriter? _writer;

    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _receiveTask;
    private volatile bool _disposed;

    /// <summary>
    /// Fired when a message is received from the host.
    /// </summary>
    public event EventHandler<LinkMessage>? MessageReceived;

    /// <summary>
    /// Fired when the connection is lost or disconnected.
    /// </summary>
    public event EventHandler? Disconnected;

    /// <summary>
    /// Fired when an error occurs.
    /// </summary>
    public event EventHandler<Exception>? Error;

    /// <summary>
    /// Gets whether the client is currently connected to a host.
    /// </summary>
    public bool IsConnected => _client?.Connected ?? false;

    /// <summary>
    /// Gets the remote endpoint address if connected.
    /// </summary>
    public string? RemoteEndPoint => _client?.Client?.RemoteEndPoint?.ToString();

    /// <summary>
    /// Creates a new LinkClient instance.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public LinkClient(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Connects to a Link host at the specified address and port.
    /// </summary>
    /// <param name="host">The hostname or IP address of the host.</param>
    /// <param name="port">The TCP port number.</param>
    /// <param name="timeout">Optional connection timeout.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if connection succeeded, false otherwise.</returns>
    public async Task<bool> ConnectAsync(string host, int port, TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(LinkClient));

        if (IsConnected)
        {
            _logger?.LogWarning("LinkClient is already connected");
            return true;
        }

        try
        {
            _client = new TcpClient
            {
                NoDelay = true,
                SendBufferSize = 65536,
                ReceiveBufferSize = 65536
            };

            var connectTimeout = timeout ?? TimeSpan.FromSeconds(10);

            using var timeoutCts = new CancellationTokenSource(connectTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutCts.Token);

            await _client.ConnectAsync(host, port, linkedCts.Token).ConfigureAwait(false);

            _stream = _client.GetStream();
            _reader = new BinaryReader(_stream);
            _writer = new BinaryWriter(_stream);

            _cancellationTokenSource = new CancellationTokenSource();
            _receiveTask = Task.Run(() => ReceiveLoopAsync(_cancellationTokenSource.Token),
                _cancellationTokenSource.Token);

            _logger?.LogInformation("LinkClient connected to {Host}:{Port}", host, port);
            return true;
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("LinkClient connection to {Host}:{Port} timed out", host, port);
            CleanupConnection();
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "LinkClient failed to connect to {Host}:{Port}", host, port);
            CleanupConnection();
            return false;
        }
    }

    /// <summary>
    /// Connects to a Link host synchronously.
    /// </summary>
    /// <param name="host">The hostname or IP address of the host.</param>
    /// <param name="port">The TCP port number.</param>
    /// <returns>True if connection succeeded, false otherwise.</returns>
    public bool Connect(string host, int port)
    {
        return ConnectAsync(host, port).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Disconnects from the host.
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (!IsConnected)
            return;

        _logger?.LogInformation("LinkClient disconnecting");

        try
        {
            // Try to send disconnect message
            await SendAsync(new LinkMessage
            {
                Type = LinkMessageType.Disconnect,
                Timestamp = GetTimestamp(),
                Payload = LinkProtocol.CreateDisconnectMessage("Client disconnecting")
            }).ConfigureAwait(false);
        }
        catch
        {
            // Ignore errors during disconnect message send
        }

        _cancellationTokenSource?.Cancel();

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

        CleanupConnection();
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Disconnects from the host synchronously.
    /// </summary>
    public void Disconnect()
    {
        DisconnectAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Sends a message to the host.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SendAsync(LinkMessage message, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("LinkClient is not connected");

        byte[] data = message.Serialize();

        try
        {
            lock (_lock)
            {
                if (_writer == null || _stream == null)
                    throw new InvalidOperationException("Connection not established");

                // Write length prefix (4 bytes, big-endian)
                byte[] lengthBytes = new byte[4];
                BinaryPrimitives.WriteInt32BigEndian(lengthBytes, data.Length);
                _writer.Write(lengthBytes);
                _writer.Write(data);
                _writer.Flush();
            }

            _logger?.LogTrace("LinkClient sent message: {Type}, {Length} bytes", message.Type, data.Length);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "LinkClient failed to send message");
            Error?.Invoke(this, ex);
            throw;
        }
    }

    /// <summary>
    /// Sends a message to the host synchronously.
    /// </summary>
    /// <param name="message">The message to send.</param>
    public void Send(LinkMessage message)
    {
        SendAsync(message).GetAwaiter().GetResult();
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        byte[] lengthBuffer = new byte[4];

        while (!cancellationToken.IsCancellationRequested && IsConnected)
        {
            try
            {
                if (_stream == null)
                    break;

                // Read length prefix
                int bytesRead = 0;
                while (bytesRead < 4)
                {
                    int read = await _stream.ReadAsync(lengthBuffer.AsMemory(bytesRead, 4 - bytesRead),
                        cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        _logger?.LogInformation("LinkClient connection closed by host");
                        break;
                    }
                    bytesRead += read;
                }

                if (bytesRead < 4)
                    break;

                int messageLength = BinaryPrimitives.ReadInt32BigEndian(lengthBuffer);
                if (messageLength <= 0 || messageLength > 10 * 1024 * 1024) // 10MB max
                {
                    _logger?.LogWarning("LinkClient received invalid message length: {Length}", messageLength);
                    continue;
                }

                // Read message data
                byte[] messageData = new byte[messageLength];
                bytesRead = 0;
                while (bytesRead < messageLength)
                {
                    int read = await _stream.ReadAsync(messageData.AsMemory(bytesRead, messageLength - bytesRead),
                        cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                        break;
                    bytesRead += read;
                }

                if (bytesRead < messageLength)
                    break;

                // Parse and dispatch message
                if (LinkMessage.TryDeserialize(messageData, out var message) && message != null)
                {
                    _logger?.LogTrace("LinkClient received message: {Type}", message.Type);

                    if (message.Type == LinkMessageType.Disconnect)
                    {
                        _logger?.LogInformation("LinkClient received disconnect from host");
                        break;
                    }

                    MessageReceived?.Invoke(this, message);
                }
                else
                {
                    _logger?.LogWarning("LinkClient failed to parse received message");
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
                    _logger?.LogWarning(ex, "LinkClient connection lost");
                    Error?.Invoke(this, ex);
                }
                break;
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    _logger?.LogError(ex, "LinkClient receive error");
                    Error?.Invoke(this, ex);
                }
            }
        }

        // Connection ended
        CleanupConnection();
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    private void CleanupConnection()
    {
        lock (_lock)
        {
            _writer?.Dispose();
            _writer = null;

            _reader?.Dispose();
            _reader = null;

            _stream?.Dispose();
            _stream = null;

            _client?.Dispose();
            _client = null;

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
    /// Disposes the client and releases all resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _cancellationTokenSource?.Cancel();
            CleanupConnection();
        }

        _disposed = true;
    }

    /// <summary>
    /// Finalizer.
    /// </summary>
    ~LinkClient()
    {
        Dispose(false);
    }
}
