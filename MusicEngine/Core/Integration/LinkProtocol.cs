//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Protocol definitions for VSTLinkBridge inter-DAW communication.
//              Defines message types, serialization, and protocol constants for the ReWire alternative.

using System;
using System.Buffers.Binary;
using System.Text;

namespace MusicEngine.Core.Integration;

/// <summary>
/// Defines the types of messages used in the Link protocol.
/// </summary>
internal enum LinkMessageType : byte
{
    /// <summary>Initial handshake to establish connection parameters.</summary>
    Handshake = 0x01,

    /// <summary>Handshake acknowledgment with accepted parameters.</summary>
    HandshakeAck = 0x02,

    /// <summary>Transport state synchronization (play/stop/record).</summary>
    TransportSync = 0x03,

    /// <summary>Tempo synchronization.</summary>
    TempoSync = 0x04,

    /// <summary>Position synchronization in beats.</summary>
    PositionSync = 0x05,

    /// <summary>Audio data packet.</summary>
    AudioData = 0x10,

    /// <summary>MIDI data packet.</summary>
    MidiData = 0x20,

    /// <summary>Keep-alive ping.</summary>
    Ping = 0xFE,

    /// <summary>Graceful disconnect notification.</summary>
    Disconnect = 0xFF
}

/// <summary>
/// Represents a message in the Link protocol.
/// </summary>
internal class LinkMessage
{
    /// <summary>Gets or sets the message type.</summary>
    public LinkMessageType Type { get; set; }

    /// <summary>Gets or sets the message payload.</summary>
    public byte[] Payload { get; set; } = Array.Empty<byte>();

    /// <summary>Gets or sets the timestamp in microseconds.</summary>
    public long Timestamp { get; set; }

    /// <summary>
    /// Serializes the message to a byte array.
    /// Format: [Type:1][Timestamp:8][PayloadLength:4][Payload:N]
    /// </summary>
    /// <returns>The serialized message bytes.</returns>
    public byte[] Serialize()
    {
        int totalLength = 1 + 8 + 4 + Payload.Length;
        byte[] buffer = new byte[totalLength];
        int offset = 0;

        // Type (1 byte)
        buffer[offset++] = (byte)Type;

        // Timestamp (8 bytes, big-endian)
        BinaryPrimitives.WriteInt64BigEndian(buffer.AsSpan(offset), Timestamp);
        offset += 8;

        // Payload length (4 bytes, big-endian)
        BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(offset), Payload.Length);
        offset += 4;

        // Payload
        if (Payload.Length > 0)
        {
            Buffer.BlockCopy(Payload, 0, buffer, offset, Payload.Length);
        }

        return buffer;
    }

    /// <summary>
    /// Deserializes a message from a byte array.
    /// </summary>
    /// <param name="data">The serialized message data.</param>
    /// <returns>The deserialized message.</returns>
    /// <exception cref="ArgumentException">Thrown when data is invalid or too short.</exception>
    public static LinkMessage Deserialize(byte[] data)
    {
        if (data == null || data.Length < 13) // Minimum: Type(1) + Timestamp(8) + Length(4)
            throw new ArgumentException("Invalid message data: too short", nameof(data));

        int offset = 0;

        var message = new LinkMessage
        {
            Type = (LinkMessageType)data[offset++],
            Timestamp = BinaryPrimitives.ReadInt64BigEndian(data.AsSpan(offset))
        };
        offset += 8;

        int payloadLength = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset));
        offset += 4;

        if (payloadLength < 0 || payloadLength > data.Length - offset)
            throw new ArgumentException($"Invalid payload length: {payloadLength}", nameof(data));

        if (payloadLength > 0)
        {
            message.Payload = new byte[payloadLength];
            Buffer.BlockCopy(data, offset, message.Payload, 0, payloadLength);
        }

        return message;
    }

    /// <summary>
    /// Tries to deserialize a message from a byte array.
    /// </summary>
    /// <param name="data">The serialized message data.</param>
    /// <param name="message">The deserialized message if successful.</param>
    /// <returns>True if deserialization succeeded, false otherwise.</returns>
    public static bool TryDeserialize(byte[] data, out LinkMessage? message)
    {
        try
        {
            message = Deserialize(data);
            return true;
        }
        catch
        {
            message = null;
            return false;
        }
    }
}

/// <summary>
/// Provides helper methods for creating Link protocol messages.
/// </summary>
internal static class LinkProtocol
{
    /// <summary>Default TCP port for Link connections.</summary>
    public const int DefaultPort = 47808;

    /// <summary>Current protocol version.</summary>
    public const int ProtocolVersion = 1;

    /// <summary>Magic bytes for protocol identification.</summary>
    public static readonly byte[] MagicBytes = { 0x4D, 0x45, 0x4C, 0x4B }; // "MELK" (MusicEngine Link)

    /// <summary>Maximum audio buffer size in samples per channel.</summary>
    public const int MaxAudioBufferSize = 8192;

    /// <summary>Maximum MIDI packet size in bytes.</summary>
    public const int MaxMidiPacketSize = 1024;

    /// <summary>
    /// Creates a handshake message for initial connection setup.
    /// Format: [Magic:4][Version:4][Role:1][SampleRate:4][BufferSize:4][AudioChannels:4][MidiChannels:4]
    /// </summary>
    /// <param name="role">The role of the connecting party.</param>
    /// <param name="sampleRate">The sample rate in Hz.</param>
    /// <param name="bufferSize">The buffer size in samples.</param>
    /// <param name="audioChannels">Number of audio channels.</param>
    /// <param name="midiChannels">Number of MIDI channels.</param>
    /// <returns>The handshake payload bytes.</returns>
    public static byte[] CreateHandshake(LinkRole role, int sampleRate, int bufferSize,
        int audioChannels = 2, int midiChannels = 16)
    {
        byte[] payload = new byte[25];
        int offset = 0;

        // Magic bytes
        Buffer.BlockCopy(MagicBytes, 0, payload, offset, 4);
        offset += 4;

        // Protocol version
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(offset), ProtocolVersion);
        offset += 4;

        // Role
        payload[offset++] = (byte)role;

        // Sample rate
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(offset), sampleRate);
        offset += 4;

        // Buffer size
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(offset), bufferSize);
        offset += 4;

        // Audio channels
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(offset), audioChannels);
        offset += 4;

        // MIDI channels
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(offset), midiChannels);

        return payload;
    }

    /// <summary>
    /// Parses a handshake payload.
    /// </summary>
    /// <param name="payload">The handshake payload bytes.</param>
    /// <param name="role">The parsed role.</param>
    /// <param name="sampleRate">The parsed sample rate.</param>
    /// <param name="bufferSize">The parsed buffer size.</param>
    /// <param name="audioChannels">The parsed audio channel count.</param>
    /// <param name="midiChannels">The parsed MIDI channel count.</param>
    /// <returns>True if parsing succeeded and magic/version match.</returns>
    public static bool ParseHandshake(byte[] payload, out LinkRole role, out int sampleRate,
        out int bufferSize, out int audioChannels, out int midiChannels)
    {
        role = LinkRole.Client;
        sampleRate = 0;
        bufferSize = 0;
        audioChannels = 0;
        midiChannels = 0;

        if (payload == null || payload.Length < 25)
            return false;

        int offset = 0;

        // Verify magic bytes
        for (int i = 0; i < 4; i++)
        {
            if (payload[offset + i] != MagicBytes[i])
                return false;
        }
        offset += 4;

        // Verify protocol version
        int version = BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(offset));
        if (version != ProtocolVersion)
            return false;
        offset += 4;

        // Role
        role = (LinkRole)payload[offset++];

        // Sample rate
        sampleRate = BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(offset));
        offset += 4;

        // Buffer size
        bufferSize = BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(offset));
        offset += 4;

        // Audio channels
        audioChannels = BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(offset));
        offset += 4;

        // MIDI channels
        midiChannels = BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(offset));

        return true;
    }

    /// <summary>
    /// Creates a transport sync message.
    /// Format: [TransportState:1][Position:8 (double)]
    /// </summary>
    /// <param name="state">The transport state.</param>
    /// <param name="position">The current position in beats.</param>
    /// <returns>The transport message payload bytes.</returns>
    public static byte[] CreateTransportMessage(LinkTransport state, double position)
    {
        byte[] payload = new byte[9];
        payload[0] = (byte)state;
        BinaryPrimitives.WriteInt64BigEndian(payload.AsSpan(1), BitConverter.DoubleToInt64Bits(position));
        return payload;
    }

    /// <summary>
    /// Parses a transport sync message.
    /// </summary>
    /// <param name="payload">The transport message payload.</param>
    /// <param name="state">The parsed transport state.</param>
    /// <param name="position">The parsed position in beats.</param>
    /// <returns>True if parsing succeeded.</returns>
    public static bool ParseTransportMessage(byte[] payload, out LinkTransport state, out double position)
    {
        state = LinkTransport.Stop;
        position = 0;

        if (payload == null || payload.Length < 9)
            return false;

        state = (LinkTransport)payload[0];
        position = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64BigEndian(payload.AsSpan(1)));
        return true;
    }

    /// <summary>
    /// Creates a tempo sync message.
    /// Format: [Tempo:8 (double)]
    /// </summary>
    /// <param name="tempo">The tempo in BPM.</param>
    /// <returns>The tempo message payload bytes.</returns>
    public static byte[] CreateTempoMessage(double tempo)
    {
        byte[] payload = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(payload, BitConverter.DoubleToInt64Bits(tempo));
        return payload;
    }

    /// <summary>
    /// Parses a tempo sync message.
    /// </summary>
    /// <param name="payload">The tempo message payload.</param>
    /// <param name="tempo">The parsed tempo in BPM.</param>
    /// <returns>True if parsing succeeded.</returns>
    public static bool ParseTempoMessage(byte[] payload, out double tempo)
    {
        tempo = 0;
        if (payload == null || payload.Length < 8)
            return false;

        tempo = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64BigEndian(payload));
        return true;
    }

    /// <summary>
    /// Creates a position sync message.
    /// Format: [Position:8 (double in beats)]
    /// </summary>
    /// <param name="beats">The position in beats.</param>
    /// <returns>The position message payload bytes.</returns>
    public static byte[] CreatePositionMessage(double beats)
    {
        byte[] payload = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(payload, BitConverter.DoubleToInt64Bits(beats));
        return payload;
    }

    /// <summary>
    /// Parses a position sync message.
    /// </summary>
    /// <param name="payload">The position message payload.</param>
    /// <param name="beats">The parsed position in beats.</param>
    /// <returns>True if parsing succeeded.</returns>
    public static bool ParsePositionMessage(byte[] payload, out double beats)
    {
        beats = 0;
        if (payload == null || payload.Length < 8)
            return false;

        beats = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64BigEndian(payload));
        return true;
    }

    /// <summary>
    /// Creates an audio data packet.
    /// Format: [Channels:4][SampleCount:4][Samples:N*4 (interleaved floats)]
    /// </summary>
    /// <param name="samples">The audio samples (interleaved if multi-channel).</param>
    /// <param name="channels">Number of audio channels.</param>
    /// <returns>The audio packet payload bytes.</returns>
    public static byte[] CreateAudioPacket(float[] samples, int channels)
    {
        if (samples == null || samples.Length == 0)
            return Array.Empty<byte>();

        int sampleCount = samples.Length / channels;
        byte[] payload = new byte[8 + samples.Length * 4];
        int offset = 0;

        // Channels
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(offset), channels);
        offset += 4;

        // Sample count per channel
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(offset), sampleCount);
        offset += 4;

        // Interleaved samples
        for (int i = 0; i < samples.Length; i++)
        {
            BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(offset), BitConverter.SingleToInt32Bits(samples[i]));
            offset += 4;
        }

        return payload;
    }

    /// <summary>
    /// Parses an audio data packet.
    /// </summary>
    /// <param name="payload">The audio packet payload.</param>
    /// <param name="samples">The parsed audio samples (interleaved).</param>
    /// <param name="channels">The number of channels.</param>
    /// <param name="sampleCount">The number of samples per channel.</param>
    /// <returns>True if parsing succeeded.</returns>
    public static bool ParseAudioPacket(byte[] payload, out float[] samples, out int channels, out int sampleCount)
    {
        samples = Array.Empty<float>();
        channels = 0;
        sampleCount = 0;

        if (payload == null || payload.Length < 8)
            return false;

        int offset = 0;

        channels = BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(offset));
        offset += 4;

        sampleCount = BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(offset));
        offset += 4;

        int totalSamples = channels * sampleCount;
        int expectedBytes = totalSamples * 4;

        if (payload.Length < 8 + expectedBytes)
            return false;

        samples = new float[totalSamples];
        for (int i = 0; i < totalSamples; i++)
        {
            samples[i] = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(offset)));
            offset += 4;
        }

        return true;
    }

    /// <summary>
    /// Creates a MIDI data packet.
    /// Format: [MessageCount:4][Messages:N*[Length:4][Data:M]]
    /// </summary>
    /// <param name="midiData">The raw MIDI data bytes.</param>
    /// <returns>The MIDI packet payload bytes.</returns>
    public static byte[] CreateMidiPacket(byte[] midiData)
    {
        if (midiData == null || midiData.Length == 0)
            return Array.Empty<byte>();

        // Simple format: just length + data for single message
        byte[] payload = new byte[4 + midiData.Length];
        BinaryPrimitives.WriteInt32BigEndian(payload, midiData.Length);
        Buffer.BlockCopy(midiData, 0, payload, 4, midiData.Length);
        return payload;
    }

    /// <summary>
    /// Parses a MIDI data packet.
    /// </summary>
    /// <param name="payload">The MIDI packet payload.</param>
    /// <param name="midiData">The parsed MIDI data bytes.</param>
    /// <returns>True if parsing succeeded.</returns>
    public static bool ParseMidiPacket(byte[] payload, out byte[] midiData)
    {
        midiData = Array.Empty<byte>();

        if (payload == null || payload.Length < 4)
            return false;

        int length = BinaryPrimitives.ReadInt32BigEndian(payload);
        if (length <= 0 || length > payload.Length - 4)
            return false;

        midiData = new byte[length];
        Buffer.BlockCopy(payload, 4, midiData, 0, length);
        return true;
    }

    /// <summary>
    /// Creates a ping message for keep-alive.
    /// </summary>
    /// <returns>An empty ping payload.</returns>
    public static byte[] CreatePingMessage()
    {
        return Array.Empty<byte>();
    }

    /// <summary>
    /// Creates a disconnect message.
    /// Format: [Reason:UTF8 string]
    /// </summary>
    /// <param name="reason">Optional disconnect reason.</param>
    /// <returns>The disconnect message payload.</returns>
    public static byte[] CreateDisconnectMessage(string? reason = null)
    {
        if (string.IsNullOrEmpty(reason))
            return Array.Empty<byte>();

        return Encoding.UTF8.GetBytes(reason);
    }
}
