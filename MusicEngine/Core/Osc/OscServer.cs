//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: OSC (Open Sound Control) UDP server for receiving OSC messages and bundles.
//              Supports address pattern matching with wildcards (*, ?, [], {}) per OSC 1.0 spec.


using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;


namespace MusicEngine.Core.Osc;


/// <summary>
/// Event arguments for OSC message received events.
/// </summary>
public class OscMessageReceivedEventArgs : EventArgs
{
    /// <summary>The received OSC message.</summary>
    public OscMessage Message { get; }

    /// <summary>The source endpoint that sent the message.</summary>
    public IPEndPoint Source { get; }

    /// <summary>The timestamp when the message was received.</summary>
    public DateTime ReceivedAt { get; }

    public OscMessageReceivedEventArgs(OscMessage message, IPEndPoint source)
    {
        Message = message;
        Source = source;
        ReceivedAt = DateTime.UtcNow;
    }
}


/// <summary>
/// Event arguments for OSC bundle received events.
/// </summary>
public class OscBundleReceivedEventArgs : EventArgs
{
    /// <summary>The received OSC bundle.</summary>
    public OscBundle Bundle { get; }

    /// <summary>The source endpoint that sent the bundle.</summary>
    public IPEndPoint Source { get; }

    /// <summary>The timestamp when the bundle was received.</summary>
    public DateTime ReceivedAt { get; }

    public OscBundleReceivedEventArgs(OscBundle bundle, IPEndPoint source)
    {
        Bundle = bundle;
        Source = source;
        ReceivedAt = DateTime.UtcNow;
    }
}


/// <summary>
/// Represents a registered OSC message handler with pattern matching.
/// </summary>
internal class OscHandler
{
    public string Pattern { get; }
    public Action<OscMessage> Callback { get; }
    public Regex CompiledPattern { get; }

    public OscHandler(string pattern, Action<OscMessage> callback)
    {
        Pattern = pattern;
        Callback = callback;
        CompiledPattern = CompilePattern(pattern);
    }

    /// <summary>
    /// Compiles an OSC address pattern to a regular expression.
    /// Supports wildcards: * (any sequence), ? (any single char), [abc] (character class), {foo,bar} (alternatives).
    /// </summary>
    private static Regex CompilePattern(string pattern)
    {
        var regex = new System.Text.StringBuilder("^");

        for (int i = 0; i < pattern.Length; i++)
        {
            char c = pattern[i];
            switch (c)
            {
                case '*':
                    regex.Append("[^/]*"); // Match any characters except '/'
                    break;
                case '?':
                    regex.Append("[^/]"); // Match any single character except '/'
                    break;
                case '[':
                    // Character class: [abc] or [!abc] or [a-z]
                    int closeIndex = pattern.IndexOf(']', i);
                    if (closeIndex > i)
                    {
                        string charClass = pattern.Substring(i, closeIndex - i + 1);
                        // Handle negation: [!abc] -> [^abc]
                        if (charClass.Length > 2 && charClass[1] == '!')
                        {
                            regex.Append("[^");
                            regex.Append(Regex.Escape(charClass.Substring(2, charClass.Length - 3)));
                            regex.Append(']');
                        }
                        else
                        {
                            regex.Append('[');
                            regex.Append(Regex.Escape(charClass.Substring(1, charClass.Length - 2)));
                            regex.Append(']');
                        }
                        i = closeIndex;
                    }
                    else
                    {
                        regex.Append(Regex.Escape(c.ToString()));
                    }
                    break;
                case '{':
                    // Alternatives: {foo,bar,baz}
                    int closeBrace = pattern.IndexOf('}', i);
                    if (closeBrace > i)
                    {
                        string alternatives = pattern.Substring(i + 1, closeBrace - i - 1);
                        var parts = alternatives.Split(',');
                        regex.Append("(?:");
                        for (int j = 0; j < parts.Length; j++)
                        {
                            if (j > 0) regex.Append('|');
                            regex.Append(Regex.Escape(parts[j]));
                        }
                        regex.Append(')');
                        i = closeBrace;
                    }
                    else
                    {
                        regex.Append(Regex.Escape(c.ToString()));
                    }
                    break;
                default:
                    regex.Append(Regex.Escape(c.ToString()));
                    break;
            }
        }

        regex.Append('$');
        return new Regex(regex.ToString(), RegexOptions.Compiled);
    }

    public bool Matches(string address) => CompiledPattern.IsMatch(address);
}


/// <summary>
/// OSC UDP server for receiving and dispatching OSC messages.
/// Supports pattern matching with wildcards as per OSC 1.0 specification.
/// </summary>
/// <example>
/// <code>
/// var server = new OscServer(9000);
/// server.AddHandler("/transport/play", msg => sequencer.Start());
/// server.AddHandler("/mixer/*/volume", msg => HandleVolume(msg));
/// await server.StartAsync();
/// </code>
/// </example>
public class OscServer : IDisposable
{
    private readonly int _port;
    private readonly IPAddress _bindAddress;
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _receiveTask;
    private bool _isRunning;
    private bool _disposed;

    private readonly ConcurrentBag<OscHandler> _handlers = new();
    private readonly ILogger? _logger;
    private readonly object _lock = new();

    /// <summary>Fired when an OSC message is received.</summary>
    public event EventHandler<OscMessageReceivedEventArgs>? MessageReceived;

    /// <summary>Fired when an OSC bundle is received.</summary>
    public event EventHandler<OscBundleReceivedEventArgs>? BundleReceived;

    /// <summary>Fired when an error occurs during reception.</summary>
    public event EventHandler<Exception>? Error;

    /// <summary>Gets the port this server is listening on.</summary>
    public int Port => _port;

    /// <summary>Gets whether the server is currently running.</summary>
    public bool IsRunning => _isRunning;

    /// <summary>Gets or sets the receive buffer size in bytes.</summary>
    public int ReceiveBufferSize { get; set; } = 65536;

    /// <summary>
    /// Creates a new OSC server listening on the specified port.
    /// </summary>
    /// <param name="port">The UDP port to listen on.</param>
    /// <param name="bindAddress">The IP address to bind to (default: any).</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public OscServer(int port, IPAddress? bindAddress = null, ILogger? logger = null)
    {
        if (port < 0 || port > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 0 and 65535.");

        _port = port;
        _bindAddress = bindAddress ?? IPAddress.Any;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new OSC server listening on the specified port.
    /// </summary>
    /// <param name="port">The UDP port to listen on.</param>
    /// <param name="bindAddress">The IP address to bind to as a string.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public OscServer(int port, string bindAddress, ILogger? logger = null)
        : this(port, IPAddress.Parse(bindAddress), logger)
    {
    }

    /// <summary>
    /// Adds a handler for messages matching the specified address pattern.
    /// </summary>
    /// <param name="pattern">
    /// The OSC address pattern to match. Supports wildcards:
    /// * = any sequence of characters
    /// ? = any single character
    /// [abc] = any character in the set
    /// [!abc] = any character not in the set
    /// {foo,bar} = alternatives
    /// </param>
    /// <param name="callback">The callback to invoke when a matching message is received.</param>
    public void AddHandler(string pattern, Action<OscMessage> callback)
    {
        if (string.IsNullOrEmpty(pattern))
            throw new ArgumentException("Pattern cannot be null or empty.", nameof(pattern));
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));

        _handlers.Add(new OscHandler(pattern, callback));
        _logger?.LogDebug("Added OSC handler for pattern: {Pattern}", pattern);
    }

    /// <summary>
    /// Removes all handlers for the specified pattern.
    /// </summary>
    public void RemoveHandler(string pattern)
    {
        // ConcurrentBag doesn't support removal, so we need to rebuild
        var newHandlers = new ConcurrentBag<OscHandler>();
        foreach (var handler in _handlers)
        {
            if (handler.Pattern != pattern)
                newHandlers.Add(handler);
        }

        // Note: This is not atomic, but OSC handler removal is typically done during setup, not runtime
        lock (_lock)
        {
            while (_handlers.TryTake(out _)) { }
            foreach (var handler in newHandlers)
                _handlers.Add(handler);
        }

        _logger?.LogDebug("Removed OSC handlers for pattern: {Pattern}", pattern);
    }

    /// <summary>
    /// Starts the OSC server synchronously.
    /// </summary>
    public void Start()
    {
        StartAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Starts the OSC server asynchronously.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_isRunning)
                throw new InvalidOperationException("OSC server is already running.");

            if (_disposed)
                throw new ObjectDisposedException(nameof(OscServer));

            _udpClient = new UdpClient();
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpClient.Client.Bind(new IPEndPoint(_bindAddress, _port));

            if (ReceiveBufferSize > 0)
            {
                _udpClient.Client.ReceiveBufferSize = ReceiveBufferSize;
            }

            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _isRunning = true;

            _receiveTask = Task.Run(() => ReceiveLoop(_cancellationTokenSource.Token), _cancellationTokenSource.Token);

            _logger?.LogInformation("OSC server started on port {Port}", _port);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the OSC server synchronously.
    /// </summary>
    public void Stop()
    {
        StopAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Stops the OSC server asynchronously.
    /// </summary>
    public async Task StopAsync()
    {
        lock (_lock)
        {
            if (!_isRunning)
                return;

            _isRunning = false;
            _cancellationTokenSource?.Cancel();
        }

        if (_receiveTask != null)
        {
            try
            {
                await _receiveTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }
        }

        lock (_lock)
        {
            _udpClient?.Close();
            _udpClient?.Dispose();
            _udpClient = null;

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        _logger?.LogInformation("OSC server stopped");
    }

    /// <summary>
    /// The main receive loop that processes incoming UDP packets.
    /// </summary>
    private async Task ReceiveLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_udpClient == null)
                    break;

                UdpReceiveResult result;
                try
                {
                    result = await _udpClient.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted)
                {
                    // Socket was closed
                    break;
                }

                ProcessPacket(result.Buffer, result.RemoteEndPoint);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error receiving OSC packet");
                Error?.Invoke(this, ex);
            }
        }
    }

    /// <summary>
    /// Processes a received OSC packet (message or bundle).
    /// </summary>
    private void ProcessPacket(byte[] data, IPEndPoint source)
    {
        try
        {
            if (OscBundle.IsBundle(data))
            {
                var bundle = OscBundle.Parse(data);
                ProcessBundle(bundle, source);
            }
            else
            {
                var message = OscMessage.Parse(data);
                ProcessMessage(message, source);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error parsing OSC packet from {Source}", source);
            Error?.Invoke(this, ex);
        }
    }

    /// <summary>
    /// Processes an OSC bundle, dispatching its elements at the appropriate time.
    /// </summary>
    private void ProcessBundle(OscBundle bundle, IPEndPoint source)
    {
        BundleReceived?.Invoke(this, new OscBundleReceivedEventArgs(bundle, source));

        // Process bundle elements
        foreach (var element in bundle.Elements)
        {
            if (element is OscMessage message)
            {
                ProcessMessage(message, source);
            }
            else if (element is OscBundle nestedBundle)
            {
                ProcessBundle(nestedBundle, source);
            }
        }
    }

    /// <summary>
    /// Processes an OSC message, dispatching it to matching handlers.
    /// </summary>
    private void ProcessMessage(OscMessage message, IPEndPoint source)
    {
        MessageReceived?.Invoke(this, new OscMessageReceivedEventArgs(message, source));

        // Dispatch to matching handlers
        bool handled = false;
        foreach (var handler in _handlers)
        {
            if (handler.Matches(message.Address))
            {
                try
                {
                    handler.Callback(message);
                    handled = true;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error in OSC handler for address {Address}", message.Address);
                }
            }
        }

        if (!handled)
        {
            _logger?.LogDebug("No handler found for OSC address: {Address}", message.Address);
        }
    }

    /// <summary>
    /// Checks if an address matches a pattern.
    /// </summary>
    /// <param name="pattern">The OSC address pattern.</param>
    /// <param name="address">The address to check.</param>
    /// <returns>True if the address matches the pattern.</returns>
    public static bool MatchesPattern(string pattern, string address)
    {
        var handler = new OscHandler(pattern, _ => { });
        return handler.Matches(address);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            Stop();
        }

        _disposed = true;
    }
}
