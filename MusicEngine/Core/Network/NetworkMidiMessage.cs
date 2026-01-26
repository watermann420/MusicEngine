// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Text;


namespace MusicEngine.Core.Network;


/// <summary>
/// Types of network MIDI messages.
/// </summary>
public enum NetworkMidiMessageType : byte
{
    /// <summary>MIDI Note On message (status 0x90-0x9F).</summary>
    NoteOn = 0x90,

    /// <summary>MIDI Note Off message (status 0x80-0x8F).</summary>
    NoteOff = 0x80,

    /// <summary>MIDI Control Change message (status 0xB0-0xBF).</summary>
    ControlChange = 0xB0,

    /// <summary>MIDI Program Change message (status 0xC0-0xCF).</summary>
    ProgramChange = 0xC0,

    /// <summary>MIDI Pitch Bend message (status 0xE0-0xEF).</summary>
    PitchBend = 0xE0,

    /// <summary>MIDI Channel Pressure (Aftertouch) message (status 0xD0-0xDF).</summary>
    ChannelPressure = 0xD0,

    /// <summary>MIDI Polyphonic Key Pressure message (status 0xA0-0xAF).</summary>
    PolyPressure = 0xA0,

    /// <summary>System Exclusive message.</summary>
    SysEx = 0xF0,

    /// <summary>MIDI Clock message.</summary>
    Clock = 0xF8,

    /// <summary>MIDI Start message.</summary>
    Start = 0xFA,

    /// <summary>MIDI Stop message.</summary>
    Stop = 0xFC,

    /// <summary>MIDI Continue message.</summary>
    Continue = 0xFB,

    /// <summary>Active Sensing message.</summary>
    ActiveSensing = 0xFE,

    /// <summary>System Reset message.</summary>
    SystemReset = 0xFF
}


/// <summary>
/// Represents a MIDI message transmitted over the network with timing information.
/// </summary>
public readonly struct NetworkMidiMessage
{
    /// <summary>
    /// The type of MIDI message.
    /// </summary>
    public NetworkMidiMessageType Type { get; }

    /// <summary>
    /// The MIDI channel (0-15). For channel messages only.
    /// </summary>
    public byte Channel { get; }

    /// <summary>
    /// First data byte (note number, CC number, program number, etc.).
    /// </summary>
    public byte Data1 { get; }

    /// <summary>
    /// Second data byte (velocity, CC value, pitch bend LSB, etc.).
    /// </summary>
    public byte Data2 { get; }

    /// <summary>
    /// Timestamp in microseconds from session start.
    /// </summary>
    public long TimestampMicroseconds { get; }

    /// <summary>
    /// System Exclusive data (for SysEx messages only).
    /// </summary>
    public byte[]? SysExData { get; }

    /// <summary>
    /// Creates a new NetworkMidiMessage.
    /// </summary>
    /// <param name="type">The MIDI message type.</param>
    /// <param name="channel">The MIDI channel (0-15).</param>
    /// <param name="data1">First data byte.</param>
    /// <param name="data2">Second data byte.</param>
    /// <param name="timestampMicroseconds">Timestamp in microseconds.</param>
    /// <param name="sysExData">Optional SysEx data.</param>
    public NetworkMidiMessage(
        NetworkMidiMessageType type,
        byte channel,
        byte data1,
        byte data2,
        long timestampMicroseconds,
        byte[]? sysExData = null)
    {
        Type = type;
        Channel = (byte)(channel & 0x0F);
        Data1 = (byte)(data1 & 0x7F);
        Data2 = (byte)(data2 & 0x7F);
        TimestampMicroseconds = timestampMicroseconds;
        SysExData = sysExData;
    }

    /// <summary>
    /// Creates a Note On message.
    /// </summary>
    /// <param name="channel">MIDI channel (0-15).</param>
    /// <param name="note">Note number (0-127).</param>
    /// <param name="velocity">Velocity (1-127).</param>
    /// <param name="timestampMicroseconds">Timestamp in microseconds.</param>
    public static NetworkMidiMessage NoteOn(byte channel, byte note, byte velocity, long timestampMicroseconds)
        => new(NetworkMidiMessageType.NoteOn, channel, note, velocity, timestampMicroseconds);

    /// <summary>
    /// Creates a Note Off message.
    /// </summary>
    /// <param name="channel">MIDI channel (0-15).</param>
    /// <param name="note">Note number (0-127).</param>
    /// <param name="velocity">Release velocity (0-127).</param>
    /// <param name="timestampMicroseconds">Timestamp in microseconds.</param>
    public static NetworkMidiMessage NoteOff(byte channel, byte note, byte velocity, long timestampMicroseconds)
        => new(NetworkMidiMessageType.NoteOff, channel, note, velocity, timestampMicroseconds);

    /// <summary>
    /// Creates a Control Change message.
    /// </summary>
    /// <param name="channel">MIDI channel (0-15).</param>
    /// <param name="controller">Controller number (0-127).</param>
    /// <param name="value">Controller value (0-127).</param>
    /// <param name="timestampMicroseconds">Timestamp in microseconds.</param>
    public static NetworkMidiMessage ControlChange(byte channel, byte controller, byte value, long timestampMicroseconds)
        => new(NetworkMidiMessageType.ControlChange, channel, controller, value, timestampMicroseconds);

    /// <summary>
    /// Creates a Program Change message.
    /// </summary>
    /// <param name="channel">MIDI channel (0-15).</param>
    /// <param name="program">Program number (0-127).</param>
    /// <param name="timestampMicroseconds">Timestamp in microseconds.</param>
    public static NetworkMidiMessage ProgramChange(byte channel, byte program, long timestampMicroseconds)
        => new(NetworkMidiMessageType.ProgramChange, channel, program, 0, timestampMicroseconds);

    /// <summary>
    /// Creates a Pitch Bend message.
    /// </summary>
    /// <param name="channel">MIDI channel (0-15).</param>
    /// <param name="value">Pitch bend value (0-16383, center is 8192).</param>
    /// <param name="timestampMicroseconds">Timestamp in microseconds.</param>
    public static NetworkMidiMessage PitchBend(byte channel, int value, long timestampMicroseconds)
    {
        value = Math.Clamp(value, 0, 16383);
        byte lsb = (byte)(value & 0x7F);
        byte msb = (byte)((value >> 7) & 0x7F);
        return new(NetworkMidiMessageType.PitchBend, channel, lsb, msb, timestampMicroseconds);
    }

    /// <summary>
    /// Creates a Channel Pressure (Aftertouch) message.
    /// </summary>
    /// <param name="channel">MIDI channel (0-15).</param>
    /// <param name="pressure">Pressure value (0-127).</param>
    /// <param name="timestampMicroseconds">Timestamp in microseconds.</param>
    public static NetworkMidiMessage ChannelPressure(byte channel, byte pressure, long timestampMicroseconds)
        => new(NetworkMidiMessageType.ChannelPressure, channel, pressure, 0, timestampMicroseconds);

    /// <summary>
    /// Creates a Polyphonic Key Pressure message.
    /// </summary>
    /// <param name="channel">MIDI channel (0-15).</param>
    /// <param name="note">Note number (0-127).</param>
    /// <param name="pressure">Pressure value (0-127).</param>
    /// <param name="timestampMicroseconds">Timestamp in microseconds.</param>
    public static NetworkMidiMessage PolyPressure(byte channel, byte note, byte pressure, long timestampMicroseconds)
        => new(NetworkMidiMessageType.PolyPressure, channel, note, pressure, timestampMicroseconds);

    /// <summary>
    /// Creates a System Exclusive message.
    /// </summary>
    /// <param name="data">The SysEx data (including F0 and F7 bytes).</param>
    /// <param name="timestampMicroseconds">Timestamp in microseconds.</param>
    public static NetworkMidiMessage SysEx(byte[] data, long timestampMicroseconds)
        => new(NetworkMidiMessageType.SysEx, 0, 0, 0, timestampMicroseconds, data);

    /// <summary>
    /// Creates a MIDI Clock message.
    /// </summary>
    /// <param name="timestampMicroseconds">Timestamp in microseconds.</param>
    public static NetworkMidiMessage Clock(long timestampMicroseconds)
        => new(NetworkMidiMessageType.Clock, 0, 0, 0, timestampMicroseconds);

    /// <summary>
    /// Creates a MIDI Start message.
    /// </summary>
    /// <param name="timestampMicroseconds">Timestamp in microseconds.</param>
    public static NetworkMidiMessage Start(long timestampMicroseconds)
        => new(NetworkMidiMessageType.Start, 0, 0, 0, timestampMicroseconds);

    /// <summary>
    /// Creates a MIDI Stop message.
    /// </summary>
    /// <param name="timestampMicroseconds">Timestamp in microseconds.</param>
    public static NetworkMidiMessage Stop(long timestampMicroseconds)
        => new(NetworkMidiMessageType.Stop, 0, 0, 0, timestampMicroseconds);

    /// <summary>
    /// Creates a MIDI Continue message.
    /// </summary>
    /// <param name="timestampMicroseconds">Timestamp in microseconds.</param>
    public static NetworkMidiMessage Continue(long timestampMicroseconds)
        => new(NetworkMidiMessageType.Continue, 0, 0, 0, timestampMicroseconds);

    /// <summary>
    /// Gets the full MIDI status byte (type + channel).
    /// </summary>
    public byte StatusByte
    {
        get
        {
            if (IsSystemMessage)
            {
                return (byte)Type;
            }
            return (byte)((byte)Type | Channel);
        }
    }

    /// <summary>
    /// Gets whether this is a system message (not channel-specific).
    /// </summary>
    public bool IsSystemMessage => (byte)Type >= 0xF0;

    /// <summary>
    /// Gets the pitch bend value as a 14-bit integer (0-16383).
    /// </summary>
    public int PitchBendValue => Type == NetworkMidiMessageType.PitchBend
        ? (Data2 << 7) | Data1
        : 0;

    /// <summary>
    /// Gets the message length in bytes (excluding SysEx).
    /// </summary>
    public int MessageLength => Type switch
    {
        NetworkMidiMessageType.ProgramChange => 2,
        NetworkMidiMessageType.ChannelPressure => 2,
        NetworkMidiMessageType.Clock => 1,
        NetworkMidiMessageType.Start => 1,
        NetworkMidiMessageType.Stop => 1,
        NetworkMidiMessageType.Continue => 1,
        NetworkMidiMessageType.ActiveSensing => 1,
        NetworkMidiMessageType.SystemReset => 1,
        NetworkMidiMessageType.SysEx => SysExData?.Length ?? 0,
        _ => 3
    };

    /// <summary>
    /// Serializes this message to a binary format for network transmission.
    /// Format: [Timestamp (8 bytes)] [Status (1 byte)] [Data1 (1 byte)] [Data2 (1 byte)] [SysExLength (4 bytes if SysEx)] [SysExData...]
    /// </summary>
    /// <returns>The serialized byte array.</returns>
    public byte[] ToBytes()
    {
        if (Type == NetworkMidiMessageType.SysEx && SysExData != null)
        {
            // SysEx message: timestamp + status + length + data
            var buffer = new byte[8 + 1 + 4 + SysExData.Length];
            WriteInt64(buffer, 0, TimestampMicroseconds);
            buffer[8] = StatusByte;
            WriteInt32(buffer, 9, SysExData.Length);
            Array.Copy(SysExData, 0, buffer, 13, SysExData.Length);
            return buffer;
        }
        else
        {
            // Standard message: timestamp + status + data1 + data2
            var buffer = new byte[11];
            WriteInt64(buffer, 0, TimestampMicroseconds);
            buffer[8] = StatusByte;
            buffer[9] = Data1;
            buffer[10] = Data2;
            return buffer;
        }
    }

    /// <summary>
    /// Parses a NetworkMidiMessage from binary data.
    /// </summary>
    /// <param name="data">The binary data.</param>
    /// <param name="offset">The offset to start reading from.</param>
    /// <param name="bytesRead">The number of bytes consumed.</param>
    /// <returns>The parsed message.</returns>
    public static NetworkMidiMessage Parse(byte[] data, int offset, out int bytesRead)
    {
        if (data.Length - offset < 11)
        {
            throw new ArgumentException("Insufficient data for MIDI message.");
        }

        long timestamp = ReadInt64(data, offset);
        byte status = data[offset + 8];

        // Extract message type and channel
        NetworkMidiMessageType type;
        byte channel = 0;

        if (status >= 0xF0)
        {
            // System message
            type = (NetworkMidiMessageType)status;
        }
        else
        {
            // Channel message
            type = (NetworkMidiMessageType)(status & 0xF0);
            channel = (byte)(status & 0x0F);
        }

        // Handle SysEx
        if (type == NetworkMidiMessageType.SysEx)
        {
            if (data.Length - offset < 13)
            {
                throw new ArgumentException("Insufficient data for SysEx message header.");
            }

            int sysExLength = ReadInt32(data, offset + 9);
            if (data.Length - offset < 13 + sysExLength)
            {
                throw new ArgumentException("Insufficient data for SysEx message body.");
            }

            var sysExData = new byte[sysExLength];
            Array.Copy(data, offset + 13, sysExData, 0, sysExLength);
            bytesRead = 13 + sysExLength;
            return new NetworkMidiMessage(type, channel, 0, 0, timestamp, sysExData);
        }
        else
        {
            byte data1 = data[offset + 9];
            byte data2 = data[offset + 10];
            bytesRead = 11;
            return new NetworkMidiMessage(type, channel, data1, data2, timestamp);
        }
    }

    private static void WriteInt64(byte[] buffer, int offset, long value)
    {
        buffer[offset] = (byte)(value >> 56);
        buffer[offset + 1] = (byte)(value >> 48);
        buffer[offset + 2] = (byte)(value >> 40);
        buffer[offset + 3] = (byte)(value >> 32);
        buffer[offset + 4] = (byte)(value >> 24);
        buffer[offset + 5] = (byte)(value >> 16);
        buffer[offset + 6] = (byte)(value >> 8);
        buffer[offset + 7] = (byte)value;
    }

    private static long ReadInt64(byte[] buffer, int offset)
    {
        return ((long)buffer[offset] << 56) |
               ((long)buffer[offset + 1] << 48) |
               ((long)buffer[offset + 2] << 40) |
               ((long)buffer[offset + 3] << 32) |
               ((long)buffer[offset + 4] << 24) |
               ((long)buffer[offset + 5] << 16) |
               ((long)buffer[offset + 6] << 8) |
               buffer[offset + 7];
    }

    private static void WriteInt32(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static int ReadInt32(byte[] buffer, int offset)
    {
        return (buffer[offset] << 24) |
               (buffer[offset + 1] << 16) |
               (buffer[offset + 2] << 8) |
               buffer[offset + 3];
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Type switch
        {
            NetworkMidiMessageType.NoteOn => $"NoteOn(ch={Channel}, note={Data1}, vel={Data2}, t={TimestampMicroseconds}us)",
            NetworkMidiMessageType.NoteOff => $"NoteOff(ch={Channel}, note={Data1}, vel={Data2}, t={TimestampMicroseconds}us)",
            NetworkMidiMessageType.ControlChange => $"CC(ch={Channel}, cc={Data1}, val={Data2}, t={TimestampMicroseconds}us)",
            NetworkMidiMessageType.ProgramChange => $"PC(ch={Channel}, prog={Data1}, t={TimestampMicroseconds}us)",
            NetworkMidiMessageType.PitchBend => $"PitchBend(ch={Channel}, val={PitchBendValue}, t={TimestampMicroseconds}us)",
            NetworkMidiMessageType.ChannelPressure => $"ChanPress(ch={Channel}, val={Data1}, t={TimestampMicroseconds}us)",
            NetworkMidiMessageType.PolyPressure => $"PolyPress(ch={Channel}, note={Data1}, val={Data2}, t={TimestampMicroseconds}us)",
            NetworkMidiMessageType.SysEx => $"SysEx(len={SysExData?.Length ?? 0}, t={TimestampMicroseconds}us)",
            NetworkMidiMessageType.Clock => $"Clock(t={TimestampMicroseconds}us)",
            NetworkMidiMessageType.Start => $"Start(t={TimestampMicroseconds}us)",
            NetworkMidiMessageType.Stop => $"Stop(t={TimestampMicroseconds}us)",
            NetworkMidiMessageType.Continue => $"Continue(t={TimestampMicroseconds}us)",
            _ => $"{Type}(t={TimestampMicroseconds}us)"
        };
    }
}


/// <summary>
/// Types of discovery/control protocol messages.
/// </summary>
public enum NetworkMidiProtocolMessageType
{
    /// <summary>Session announcement/discovery message.</summary>
    Announce,

    /// <summary>Request to join a session.</summary>
    JoinRequest,

    /// <summary>Response accepting a join request.</summary>
    JoinAccept,

    /// <summary>Response rejecting a join request.</summary>
    JoinReject,

    /// <summary>Notification that a peer is leaving.</summary>
    Leave,

    /// <summary>Heartbeat/keepalive message.</summary>
    Heartbeat,

    /// <summary>Time synchronization request.</summary>
    TimeSyncRequest,

    /// <summary>Time synchronization response.</summary>
    TimeSyncResponse
}


/// <summary>
/// Protocol message for session discovery and management.
/// Uses JSON serialization for human-readable debugging.
/// </summary>
public class NetworkMidiProtocolMessage
{
    /// <summary>Gets or sets the message type.</summary>
    public NetworkMidiProtocolMessageType Type { get; set; }

    /// <summary>Gets or sets the session name.</summary>
    public string? SessionName { get; set; }

    /// <summary>Gets or sets the unique session identifier.</summary>
    public Guid SessionId { get; set; }

    /// <summary>Gets or sets the peer identifier.</summary>
    public Guid PeerId { get; set; }

    /// <summary>Gets or sets the peer's display name.</summary>
    public string? PeerName { get; set; }

    /// <summary>Gets or sets the peer's TCP port for MIDI data.</summary>
    public int TcpPort { get; set; }

    /// <summary>Gets or sets the timestamp when the message was created.</summary>
    public long TimestampTicks { get; set; }

    /// <summary>Gets or sets the protocol version.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Gets or sets a rejection reason (for JoinReject).</summary>
    public string? RejectReason { get; set; }

    /// <summary>Gets or sets the maximum number of peers allowed.</summary>
    public int MaxPeers { get; set; } = 8;

    /// <summary>Gets or sets the current peer count.</summary>
    public int CurrentPeerCount { get; set; }

    /// <summary>Gets or sets time sync data (T1, T2, T3 values).</summary>
    public long[]? TimeSyncData { get; set; }

    /// <summary>
    /// Serializes this message to JSON.
    /// </summary>
    public string ToJson()
    {
        return System.Text.Json.JsonSerializer.Serialize(this);
    }

    /// <summary>
    /// Parses a protocol message from JSON.
    /// </summary>
    /// <param name="json">The JSON string.</param>
    /// <returns>The parsed message.</returns>
    public static NetworkMidiProtocolMessage? FromJson(string json)
    {
        return System.Text.Json.JsonSerializer.Deserialize<NetworkMidiProtocolMessage>(json);
    }
}
