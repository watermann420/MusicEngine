// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MusicEngine.Infrastructure.Collaboration;

/// <summary>
/// Types of messages in the collaboration protocol.
/// </summary>
public enum CollaborationMessageType
{
    /// <summary>Peer joining a session.</summary>
    Join,

    /// <summary>Peer leaving a session.</summary>
    Leave,

    /// <summary>Keep-alive ping.</summary>
    Ping,

    /// <summary>Response to ping.</summary>
    Pong,

    /// <summary>Add a note to a pattern.</summary>
    NoteAdd,

    /// <summary>Remove a note from a pattern.</summary>
    NoteRemove,

    /// <summary>Modify an existing note.</summary>
    NoteModify,

    /// <summary>Add a new track.</summary>
    TrackAdd,

    /// <summary>Remove a track.</summary>
    TrackRemove,

    /// <summary>Modify track properties.</summary>
    TrackModify,

    /// <summary>Add a clip to the arrangement.</summary>
    ClipAdd,

    /// <summary>Remove a clip from the arrangement.</summary>
    ClipRemove,

    /// <summary>Modify clip properties.</summary>
    ClipModify,

    /// <summary>Change a parameter value.</summary>
    ParameterChange,

    /// <summary>Transport synchronization (play/stop/position).</summary>
    TransportSync,

    /// <summary>Chat message between peers.</summary>
    Chat,

    /// <summary>Cursor position update for collaborative editing.</summary>
    Cursor,

    /// <summary>Acknowledge receipt of an operation.</summary>
    Acknowledge,

    /// <summary>Request full state sync.</summary>
    SyncRequest,

    /// <summary>Full state sync response.</summary>
    SyncResponse,

    /// <summary>Error notification.</summary>
    Error
}

/// <summary>
/// Role of a peer in the collaboration session.
/// </summary>
public enum CollaborationRole
{
    /// <summary>Session host with full control.</summary>
    Host,

    /// <summary>Can edit the project.</summary>
    Editor,

    /// <summary>Can only view, no editing.</summary>
    Viewer
}

/// <summary>
/// Transport state for synchronization.
/// </summary>
public enum TransportState
{
    /// <summary>Playback stopped.</summary>
    Stopped,

    /// <summary>Playback in progress.</summary>
    Playing,

    /// <summary>Recording in progress.</summary>
    Recording,

    /// <summary>Paused.</summary>
    Paused
}

/// <summary>
/// Base class for collaboration messages.
/// </summary>
public class CollaborationMessage
{
    /// <summary>The type of message.</summary>
    [JsonPropertyName("type")]
    public CollaborationMessageType Type { get; set; }

    /// <summary>Unique message identifier.</summary>
    [JsonPropertyName("messageId")]
    public Guid MessageId { get; set; } = Guid.NewGuid();

    /// <summary>ID of the peer sending the message.</summary>
    [JsonPropertyName("peerId")]
    public Guid PeerId { get; set; }

    /// <summary>Timestamp when the message was created (UTC ticks).</summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; } = DateTime.UtcNow.Ticks;

    /// <summary>Session ID this message belongs to.</summary>
    [JsonPropertyName("sessionId")]
    public Guid SessionId { get; set; }

    /// <summary>Vector clock for causal ordering.</summary>
    [JsonPropertyName("vectorClock")]
    public Dictionary<Guid, long> VectorClock { get; set; } = new();

    /// <summary>Protocol version for compatibility.</summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = CollaborationProtocol.CurrentVersion;

    /// <summary>
    /// Serializes the message to JSON.
    /// </summary>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, CollaborationProtocol.JsonOptions);
    }

    /// <summary>
    /// Deserializes a message from JSON.
    /// </summary>
    public static CollaborationMessage? FromJson(string json)
    {
        return JsonSerializer.Deserialize<CollaborationMessage>(json, CollaborationProtocol.JsonOptions);
    }

    /// <summary>
    /// Deserializes a typed message from JSON.
    /// </summary>
    public static T? FromJson<T>(string json) where T : CollaborationMessage
    {
        return JsonSerializer.Deserialize<T>(json, CollaborationProtocol.JsonOptions);
    }
}

/// <summary>
/// Message for joining a session.
/// </summary>
public class JoinMessage : CollaborationMessage
{
    /// <summary>Name of the joining peer.</summary>
    [JsonPropertyName("peerName")]
    public string PeerName { get; set; } = string.Empty;

    /// <summary>Requested role.</summary>
    [JsonPropertyName("role")]
    public CollaborationRole Role { get; set; } = CollaborationRole.Editor;

    /// <summary>Color for cursor/selection display (ARGB).</summary>
    [JsonPropertyName("color")]
    public uint Color { get; set; }

    /// <summary>Session password (if required).</summary>
    [JsonPropertyName("password")]
    public string? Password { get; set; }

    public JoinMessage()
    {
        Type = CollaborationMessageType.Join;
    }
}

/// <summary>
/// Message for leaving a session.
/// </summary>
public class LeaveMessage : CollaborationMessage
{
    /// <summary>Optional reason for leaving.</summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    public LeaveMessage()
    {
        Type = CollaborationMessageType.Leave;
    }
}

/// <summary>
/// Ping message for keep-alive.
/// </summary>
public class PingMessage : CollaborationMessage
{
    /// <summary>Sequence number for latency calculation.</summary>
    [JsonPropertyName("sequence")]
    public long Sequence { get; set; }

    public PingMessage()
    {
        Type = CollaborationMessageType.Ping;
    }
}

/// <summary>
/// Pong message response to ping.
/// </summary>
public class PongMessage : CollaborationMessage
{
    /// <summary>Sequence number from the ping.</summary>
    [JsonPropertyName("sequence")]
    public long Sequence { get; set; }

    /// <summary>Server timestamp for latency calculation.</summary>
    [JsonPropertyName("serverTimestamp")]
    public long ServerTimestamp { get; set; }

    public PongMessage()
    {
        Type = CollaborationMessageType.Pong;
    }
}

/// <summary>
/// Message for note operations (add, remove, modify).
/// </summary>
public class NoteOperationMessage : CollaborationMessage
{
    /// <summary>ID of the pattern containing the note.</summary>
    [JsonPropertyName("patternId")]
    public Guid PatternId { get; set; }

    /// <summary>ID of the note being operated on.</summary>
    [JsonPropertyName("noteId")]
    public Guid NoteId { get; set; }

    /// <summary>MIDI note number (0-127).</summary>
    [JsonPropertyName("noteNumber")]
    public int NoteNumber { get; set; }

    /// <summary>Start time in beats.</summary>
    [JsonPropertyName("startBeat")]
    public double StartBeat { get; set; }

    /// <summary>Duration in beats.</summary>
    [JsonPropertyName("duration")]
    public double Duration { get; set; }

    /// <summary>Velocity (0-127).</summary>
    [JsonPropertyName("velocity")]
    public int Velocity { get; set; }

    /// <summary>MIDI channel (0-15).</summary>
    [JsonPropertyName("channel")]
    public int Channel { get; set; }

    /// <summary>Previous values for undo (only for modify operations).</summary>
    [JsonPropertyName("previousValues")]
    public Dictionary<string, object>? PreviousValues { get; set; }
}

/// <summary>
/// Message for track operations (add, remove, modify).
/// </summary>
public class TrackOperationMessage : CollaborationMessage
{
    /// <summary>ID of the track.</summary>
    [JsonPropertyName("trackId")]
    public Guid TrackId { get; set; }

    /// <summary>Track name.</summary>
    [JsonPropertyName("trackName")]
    public string TrackName { get; set; } = string.Empty;

    /// <summary>Track index/position.</summary>
    [JsonPropertyName("trackIndex")]
    public int TrackIndex { get; set; }

    /// <summary>Track type (audio, midi, bus, etc.).</summary>
    [JsonPropertyName("trackType")]
    public string TrackType { get; set; } = "midi";

    /// <summary>Track volume (0.0-1.0).</summary>
    [JsonPropertyName("volume")]
    public float Volume { get; set; } = 1.0f;

    /// <summary>Track pan (-1.0 to 1.0).</summary>
    [JsonPropertyName("pan")]
    public float Pan { get; set; }

    /// <summary>Whether track is muted.</summary>
    [JsonPropertyName("mute")]
    public bool Mute { get; set; }

    /// <summary>Whether track is soloed.</summary>
    [JsonPropertyName("solo")]
    public bool Solo { get; set; }

    /// <summary>Whether track is armed for recording.</summary>
    [JsonPropertyName("armed")]
    public bool Armed { get; set; }

    /// <summary>Previous values for undo.</summary>
    [JsonPropertyName("previousValues")]
    public Dictionary<string, object>? PreviousValues { get; set; }
}

/// <summary>
/// Message for clip operations (add, remove, modify).
/// </summary>
public class ClipOperationMessage : CollaborationMessage
{
    /// <summary>ID of the clip.</summary>
    [JsonPropertyName("clipId")]
    public Guid ClipId { get; set; }

    /// <summary>ID of the track containing the clip.</summary>
    [JsonPropertyName("trackId")]
    public Guid TrackId { get; set; }

    /// <summary>Clip name.</summary>
    [JsonPropertyName("clipName")]
    public string ClipName { get; set; } = string.Empty;

    /// <summary>Start position in beats.</summary>
    [JsonPropertyName("startBeat")]
    public double StartBeat { get; set; }

    /// <summary>Length in beats.</summary>
    [JsonPropertyName("lengthBeats")]
    public double LengthBeats { get; set; }

    /// <summary>Clip type (audio, midi).</summary>
    [JsonPropertyName("clipType")]
    public string ClipType { get; set; } = "midi";

    /// <summary>Pattern ID for MIDI clips.</summary>
    [JsonPropertyName("patternId")]
    public Guid? PatternId { get; set; }

    /// <summary>Audio file path for audio clips.</summary>
    [JsonPropertyName("audioPath")]
    public string? AudioPath { get; set; }

    /// <summary>Clip color (ARGB).</summary>
    [JsonPropertyName("color")]
    public uint Color { get; set; }

    /// <summary>Previous values for undo.</summary>
    [JsonPropertyName("previousValues")]
    public Dictionary<string, object>? PreviousValues { get; set; }
}

/// <summary>
/// Message for parameter changes.
/// </summary>
public class ParameterChangeMessage : CollaborationMessage
{
    /// <summary>Target object ID (track, effect, synth, etc.).</summary>
    [JsonPropertyName("targetId")]
    public Guid TargetId { get; set; }

    /// <summary>Target type (track, effect, synth, global).</summary>
    [JsonPropertyName("targetType")]
    public string TargetType { get; set; } = string.Empty;

    /// <summary>Parameter name.</summary>
    [JsonPropertyName("parameterName")]
    public string ParameterName { get; set; } = string.Empty;

    /// <summary>New value (serialized as JSON).</summary>
    [JsonPropertyName("value")]
    public JsonElement Value { get; set; }

    /// <summary>Previous value for undo.</summary>
    [JsonPropertyName("previousValue")]
    public JsonElement? PreviousValue { get; set; }

    public ParameterChangeMessage()
    {
        Type = CollaborationMessageType.ParameterChange;
    }
}

/// <summary>
/// Message for transport synchronization.
/// </summary>
public class TransportSyncMessage : CollaborationMessage
{
    /// <summary>Transport state.</summary>
    [JsonPropertyName("state")]
    public TransportState State { get; set; }

    /// <summary>Current position in beats.</summary>
    [JsonPropertyName("positionBeats")]
    public double PositionBeats { get; set; }

    /// <summary>Tempo in BPM.</summary>
    [JsonPropertyName("tempo")]
    public double Tempo { get; set; }

    /// <summary>Time signature numerator.</summary>
    [JsonPropertyName("timeSignatureNumerator")]
    public int TimeSignatureNumerator { get; set; } = 4;

    /// <summary>Time signature denominator.</summary>
    [JsonPropertyName("timeSignatureDenominator")]
    public int TimeSignatureDenominator { get; set; } = 4;

    /// <summary>Loop enabled.</summary>
    [JsonPropertyName("loopEnabled")]
    public bool LoopEnabled { get; set; }

    /// <summary>Loop start in beats.</summary>
    [JsonPropertyName("loopStart")]
    public double LoopStart { get; set; }

    /// <summary>Loop end in beats.</summary>
    [JsonPropertyName("loopEnd")]
    public double LoopEnd { get; set; }

    public TransportSyncMessage()
    {
        Type = CollaborationMessageType.TransportSync;
    }
}

/// <summary>
/// Chat message between peers.
/// </summary>
public class ChatMessage : CollaborationMessage
{
    /// <summary>Chat text content.</summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>Target peer ID for private messages (null for broadcast).</summary>
    [JsonPropertyName("targetPeerId")]
    public Guid? TargetPeerId { get; set; }

    public ChatMessage()
    {
        Type = CollaborationMessageType.Chat;
    }
}

/// <summary>
/// Cursor position message for showing other users' editing positions.
/// </summary>
public class CursorMessage : CollaborationMessage
{
    /// <summary>View type (arrangement, pianoroll, mixer, etc.).</summary>
    [JsonPropertyName("viewType")]
    public string ViewType { get; set; } = string.Empty;

    /// <summary>X position (e.g., beat position).</summary>
    [JsonPropertyName("x")]
    public double X { get; set; }

    /// <summary>Y position (e.g., track index or note number).</summary>
    [JsonPropertyName("y")]
    public double Y { get; set; }

    /// <summary>Target track ID (if applicable).</summary>
    [JsonPropertyName("trackId")]
    public Guid? TrackId { get; set; }

    /// <summary>Selection start (if selecting).</summary>
    [JsonPropertyName("selectionStart")]
    public (double X, double Y)? SelectionStart { get; set; }

    /// <summary>Selection end (if selecting).</summary>
    [JsonPropertyName("selectionEnd")]
    public (double X, double Y)? SelectionEnd { get; set; }

    public CursorMessage()
    {
        Type = CollaborationMessageType.Cursor;
    }
}

/// <summary>
/// Acknowledge message for confirming receipt.
/// </summary>
public class AcknowledgeMessage : CollaborationMessage
{
    /// <summary>ID of the message being acknowledged.</summary>
    [JsonPropertyName("acknowledgedMessageId")]
    public Guid AcknowledgedMessageId { get; set; }

    /// <summary>Whether the operation was successful.</summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>Error message if not successful.</summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    public AcknowledgeMessage()
    {
        Type = CollaborationMessageType.Acknowledge;
    }
}

/// <summary>
/// Request for full state synchronization.
/// </summary>
public class SyncRequestMessage : CollaborationMessage
{
    /// <summary>Whether to include full project data.</summary>
    [JsonPropertyName("includeProjectData")]
    public bool IncludeProjectData { get; set; } = true;

    public SyncRequestMessage()
    {
        Type = CollaborationMessageType.SyncRequest;
    }
}

/// <summary>
/// Full state synchronization response.
/// </summary>
public class SyncResponseMessage : CollaborationMessage
{
    /// <summary>Serialized project state.</summary>
    [JsonPropertyName("projectState")]
    public string? ProjectState { get; set; }

    /// <summary>List of connected peers.</summary>
    [JsonPropertyName("peers")]
    public List<PeerInfo>? Peers { get; set; }

    /// <summary>Current transport state.</summary>
    [JsonPropertyName("transport")]
    public TransportSyncMessage? Transport { get; set; }

    public SyncResponseMessage()
    {
        Type = CollaborationMessageType.SyncResponse;
    }
}

/// <summary>
/// Error message.
/// </summary>
public class ErrorMessage : CollaborationMessage
{
    /// <summary>Error code.</summary>
    [JsonPropertyName("errorCode")]
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>Error description.</summary>
    [JsonPropertyName("errorDescription")]
    public string ErrorDescription { get; set; } = string.Empty;

    /// <summary>Related message ID (if applicable).</summary>
    [JsonPropertyName("relatedMessageId")]
    public Guid? RelatedMessageId { get; set; }

    public ErrorMessage()
    {
        Type = CollaborationMessageType.Error;
    }
}

/// <summary>
/// Peer information for sync responses.
/// </summary>
public class PeerInfo
{
    /// <summary>Peer ID.</summary>
    [JsonPropertyName("peerId")]
    public Guid PeerId { get; set; }

    /// <summary>Peer name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Peer role.</summary>
    [JsonPropertyName("role")]
    public CollaborationRole Role { get; set; }

    /// <summary>Peer color (ARGB).</summary>
    [JsonPropertyName("color")]
    public uint Color { get; set; }
}

/// <summary>
/// Protocol constants and utilities.
/// </summary>
public static class CollaborationProtocol
{
    /// <summary>Current protocol version.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Default server port.</summary>
    public const int DefaultPort = 22420;

    /// <summary>Ping interval in milliseconds.</summary>
    public const int PingIntervalMs = 5000;

    /// <summary>Peer timeout in milliseconds.</summary>
    public const int PeerTimeoutMs = 15000;

    /// <summary>Reconnection delay in milliseconds.</summary>
    public const int ReconnectDelayMs = 2000;

    /// <summary>Maximum reconnection attempts.</summary>
    public const int MaxReconnectAttempts = 5;

    /// <summary>Maximum message size in bytes.</summary>
    public const int MaxMessageSize = 10 * 1024 * 1024; // 10 MB

    /// <summary>JSON serialization options.</summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Error codes.</summary>
    public static class ErrorCodes
    {
        public const string SessionNotFound = "SESSION_NOT_FOUND";
        public const string InvalidPassword = "INVALID_PASSWORD";
        public const string SessionFull = "SESSION_FULL";
        public const string NotAuthorized = "NOT_AUTHORIZED";
        public const string InvalidMessage = "INVALID_MESSAGE";
        public const string VersionMismatch = "VERSION_MISMATCH";
        public const string ConflictDetected = "CONFLICT_DETECTED";
        public const string InternalError = "INTERNAL_ERROR";
    }
}
