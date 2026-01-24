//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: OSC (Open Sound Control) UDP client for sending OSC messages and bundles.
//              Provides synchronous and asynchronous send operations per OSC 1.0 spec.


using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;


namespace MusicEngine.Core.Osc;


/// <summary>
/// OSC UDP client for sending OSC messages and bundles to a remote endpoint.
/// </summary>
/// <example>
/// <code>
/// using var client = new OscClient("localhost", 8000);
/// client.Send(new OscMessage("/transport/play"));
/// client.Send(new OscMessage("/mixer/track1/volume", 0.75f));
///
/// // Async usage
/// await client.SendAsync(new OscMessage("/status", "playing"));
///
/// // Bundle usage
/// var bundle = new OscBundle()
///     .Add(new OscMessage("/note/on", 60, 100))
///     .Add(new OscMessage("/note/on", 64, 100));
/// await client.SendBundleAsync(bundle);
/// </code>
/// </example>
public class OscClient : IDisposable
{
    private readonly string _host;
    private readonly int _port;
    private UdpClient? _udpClient;
    private IPEndPoint? _remoteEndPoint;
    private bool _disposed;
    private readonly ILogger? _logger;
    private readonly object _lock = new();

    /// <summary>Gets the target host.</summary>
    public string Host => _host;

    /// <summary>Gets the target port.</summary>
    public int Port => _port;

    /// <summary>Gets whether the client is connected.</summary>
    public bool IsConnected => _udpClient != null;

    /// <summary>Gets or sets the send buffer size in bytes.</summary>
    public int SendBufferSize { get; set; } = 65536;

    /// <summary>Gets or sets whether to enable broadcast mode.</summary>
    public bool EnableBroadcast { get; set; }

    /// <summary>
    /// Creates a new OSC client targeting the specified host and port.
    /// </summary>
    /// <param name="host">The target hostname or IP address.</param>
    /// <param name="port">The target UDP port.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public OscClient(string host, int port, ILogger? logger = null)
    {
        if (string.IsNullOrEmpty(host))
            throw new ArgumentException("Host cannot be null or empty.", nameof(host));
        if (port < 0 || port > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 0 and 65535.");

        _host = host;
        _port = port;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new OSC client targeting the specified IP address and port.
    /// </summary>
    /// <param name="address">The target IP address.</param>
    /// <param name="port">The target UDP port.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public OscClient(IPAddress address, int port, ILogger? logger = null)
        : this(address.ToString(), port, logger)
    {
    }

    /// <summary>
    /// Creates a new OSC client targeting the specified endpoint.
    /// </summary>
    /// <param name="endpoint">The target endpoint.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public OscClient(IPEndPoint endpoint, ILogger? logger = null)
        : this(endpoint.Address, endpoint.Port, logger)
    {
    }

    /// <summary>
    /// Ensures the UDP client is initialized and connected.
    /// </summary>
    private void EnsureConnected()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(OscClient));

        if (_udpClient != null)
            return;

        lock (_lock)
        {
            if (_udpClient != null)
                return;

            _udpClient = new UdpClient();

            if (SendBufferSize > 0)
            {
                _udpClient.Client.SendBufferSize = SendBufferSize;
            }

            if (EnableBroadcast)
            {
                _udpClient.EnableBroadcast = true;
            }

            // Resolve hostname
            var addresses = Dns.GetHostAddresses(_host);
            if (addresses.Length == 0)
                throw new SocketException((int)SocketError.HostNotFound);

            _remoteEndPoint = new IPEndPoint(addresses[0], _port);

            _logger?.LogDebug("OSC client initialized for {Host}:{Port}", _host, _port);
        }
    }

    /// <summary>
    /// Sends an OSC message synchronously.
    /// </summary>
    /// <param name="message">The OSC message to send.</param>
    public void Send(OscMessage message)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        EnsureConnected();

        byte[] data = message.ToBytes();
        int sent = _udpClient!.Send(data, data.Length, _remoteEndPoint);

        _logger?.LogTrace("Sent OSC message {Address} ({Bytes} bytes)", message.Address, sent);
    }

    /// <summary>
    /// Sends an OSC message asynchronously.
    /// </summary>
    /// <param name="message">The OSC message to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SendAsync(OscMessage message, CancellationToken cancellationToken = default)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        EnsureConnected();

        byte[] data = message.ToBytes();
        int sent = await _udpClient!.SendAsync(data, data.Length, _remoteEndPoint).ConfigureAwait(false);

        _logger?.LogTrace("Sent OSC message {Address} ({Bytes} bytes)", message.Address, sent);
    }

    /// <summary>
    /// Sends an OSC bundle synchronously.
    /// </summary>
    /// <param name="bundle">The OSC bundle to send.</param>
    public void SendBundle(OscBundle bundle)
    {
        if (bundle == null)
            throw new ArgumentNullException(nameof(bundle));

        EnsureConnected();

        byte[] data = bundle.ToBytes();
        int sent = _udpClient!.Send(data, data.Length, _remoteEndPoint);

        _logger?.LogTrace("Sent OSC bundle ({Elements} elements, {Bytes} bytes)", bundle.Elements.Count, sent);
    }

    /// <summary>
    /// Sends an OSC bundle asynchronously.
    /// </summary>
    /// <param name="bundle">The OSC bundle to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SendBundleAsync(OscBundle bundle, CancellationToken cancellationToken = default)
    {
        if (bundle == null)
            throw new ArgumentNullException(nameof(bundle));

        EnsureConnected();

        byte[] data = bundle.ToBytes();
        int sent = await _udpClient!.SendAsync(data, data.Length, _remoteEndPoint).ConfigureAwait(false);

        _logger?.LogTrace("Sent OSC bundle ({Elements} elements, {Bytes} bytes)", bundle.Elements.Count, sent);
    }

    /// <summary>
    /// Sends raw OSC data synchronously.
    /// </summary>
    /// <param name="data">The raw OSC data to send.</param>
    public void SendRaw(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        EnsureConnected();

        _udpClient!.Send(data, data.Length, _remoteEndPoint);
    }

    /// <summary>
    /// Sends raw OSC data asynchronously.
    /// </summary>
    /// <param name="data">The raw OSC data to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SendRawAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        EnsureConnected();

        await _udpClient!.SendAsync(data, data.Length, _remoteEndPoint).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a simple message with the specified address and arguments.
    /// </summary>
    /// <param name="address">The OSC address pattern.</param>
    /// <param name="args">The message arguments.</param>
    public void Send(string address, params object[] args)
    {
        Send(new OscMessage(address, args));
    }

    /// <summary>
    /// Sends a simple message asynchronously with the specified address and arguments.
    /// </summary>
    /// <param name="address">The OSC address pattern.</param>
    /// <param name="args">The message arguments.</param>
    public Task SendAsync(string address, params object[] args)
    {
        return SendAsync(new OscMessage(address, args));
    }

    /// <summary>
    /// Closes the UDP client connection.
    /// </summary>
    public void Close()
    {
        lock (_lock)
        {
            _udpClient?.Close();
            _udpClient?.Dispose();
            _udpClient = null;
            _remoteEndPoint = null;
        }

        _logger?.LogDebug("OSC client closed");
    }

    /// <summary>
    /// Reconnects the client (closes and re-establishes the connection).
    /// </summary>
    public void Reconnect()
    {
        Close();
        EnsureConnected();
    }

    /// <summary>
    /// Changes the target endpoint.
    /// </summary>
    /// <param name="host">The new target hostname.</param>
    /// <param name="port">The new target port.</param>
    public void SetTarget(string host, int port)
    {
        lock (_lock)
        {
            Close();

            // Resolve hostname and update endpoint
            var addresses = Dns.GetHostAddresses(host);
            if (addresses.Length == 0)
                throw new SocketException((int)SocketError.HostNotFound);

            _remoteEndPoint = new IPEndPoint(addresses[0], port);
        }

        EnsureConnected();
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
            Close();
        }

        _disposed = true;
    }
}


/// <summary>
/// Extension methods for common OSC message patterns.
/// </summary>
public static class OscClientExtensions
{
    /// <summary>
    /// Sends a transport play command.
    /// </summary>
    public static void SendPlay(this OscClient client)
    {
        client.Send(new OscMessage("/transport/play"));
    }

    /// <summary>
    /// Sends a transport stop command.
    /// </summary>
    public static void SendStop(this OscClient client)
    {
        client.Send(new OscMessage("/transport/stop"));
    }

    /// <summary>
    /// Sends a transport record command.
    /// </summary>
    public static void SendRecord(this OscClient client)
    {
        client.Send(new OscMessage("/transport/record"));
    }

    /// <summary>
    /// Sends a transport position command.
    /// </summary>
    /// <param name="client">The OSC client.</param>
    /// <param name="position">Position in beats or seconds.</param>
    public static void SendPosition(this OscClient client, float position)
    {
        client.Send(new OscMessage("/transport/position", position));
    }

    /// <summary>
    /// Sends a tempo/BPM command.
    /// </summary>
    /// <param name="client">The OSC client.</param>
    /// <param name="bpm">Tempo in beats per minute.</param>
    public static void SendTempo(this OscClient client, float bpm)
    {
        client.Send(new OscMessage("/transport/tempo", bpm));
    }

    /// <summary>
    /// Sends a mixer track volume command.
    /// </summary>
    /// <param name="client">The OSC client.</param>
    /// <param name="track">Track number (1-based).</param>
    /// <param name="volume">Volume level (0.0 to 1.0).</param>
    public static void SendTrackVolume(this OscClient client, int track, float volume)
    {
        client.Send(new OscMessage($"/mixer/track{track}/volume", volume));
    }

    /// <summary>
    /// Sends a mixer track pan command.
    /// </summary>
    /// <param name="client">The OSC client.</param>
    /// <param name="track">Track number (1-based).</param>
    /// <param name="pan">Pan value (-1.0 to 1.0).</param>
    public static void SendTrackPan(this OscClient client, int track, float pan)
    {
        client.Send(new OscMessage($"/mixer/track{track}/pan", pan));
    }

    /// <summary>
    /// Sends a mixer track mute command.
    /// </summary>
    /// <param name="client">The OSC client.</param>
    /// <param name="track">Track number (1-based).</param>
    /// <param name="muted">Mute state.</param>
    public static void SendTrackMute(this OscClient client, int track, bool muted)
    {
        client.Send(new OscMessage($"/mixer/track{track}/mute", muted ? 1 : 0));
    }

    /// <summary>
    /// Sends a mixer track solo command.
    /// </summary>
    /// <param name="client">The OSC client.</param>
    /// <param name="track">Track number (1-based).</param>
    /// <param name="soloed">Solo state.</param>
    public static void SendTrackSolo(this OscClient client, int track, bool soloed)
    {
        client.Send(new OscMessage($"/mixer/track{track}/solo", soloed ? 1 : 0));
    }

    /// <summary>
    /// Sends a master volume command.
    /// </summary>
    /// <param name="client">The OSC client.</param>
    /// <param name="volume">Master volume level (0.0 to 1.0).</param>
    public static void SendMasterVolume(this OscClient client, float volume)
    {
        client.Send(new OscMessage("/mixer/master/volume", volume));
    }

    /// <summary>
    /// Sends a MIDI note-on command.
    /// </summary>
    /// <param name="client">The OSC client.</param>
    /// <param name="channel">MIDI channel (1-16).</param>
    /// <param name="note">MIDI note number (0-127).</param>
    /// <param name="velocity">Note velocity (0-127).</param>
    public static void SendNoteOn(this OscClient client, int channel, int note, int velocity)
    {
        client.Send(new OscMessage($"/midi/ch{channel}/note/on", note, velocity));
    }

    /// <summary>
    /// Sends a MIDI note-off command.
    /// </summary>
    /// <param name="client">The OSC client.</param>
    /// <param name="channel">MIDI channel (1-16).</param>
    /// <param name="note">MIDI note number (0-127).</param>
    public static void SendNoteOff(this OscClient client, int channel, int note)
    {
        client.Send(new OscMessage($"/midi/ch{channel}/note/off", note, 0));
    }

    /// <summary>
    /// Sends a MIDI control change command.
    /// </summary>
    /// <param name="client">The OSC client.</param>
    /// <param name="channel">MIDI channel (1-16).</param>
    /// <param name="controller">Controller number (0-127).</param>
    /// <param name="value">Controller value (0-127).</param>
    public static void SendControlChange(this OscClient client, int channel, int controller, int value)
    {
        client.Send(new OscMessage($"/midi/ch{channel}/cc/{controller}", value));
    }
}
