// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MIDI handling component.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace MusicEngine.Core.Midi;

/// <summary>
/// MIDI 2.0 message types (Universal MIDI Packet).
/// </summary>
public enum UMPMessageType : byte
{
    /// <summary>Utility messages (32-bit)</summary>
    Utility = 0x0,
    /// <summary>System real-time and system common (32-bit)</summary>
    SystemRealTimeCommon = 0x1,
    /// <summary>MIDI 1.0 channel voice messages (32-bit)</summary>
    Midi1ChannelVoice = 0x2,
    /// <summary>Data messages including SysEx (64-bit)</summary>
    Data64 = 0x3,
    /// <summary>MIDI 2.0 channel voice messages (64-bit)</summary>
    Midi2ChannelVoice = 0x4,
    /// <summary>Data messages (128-bit)</summary>
    Data128 = 0x5,
    /// <summary>Flex Data (128-bit)</summary>
    FlexData = 0xD,
    /// <summary>UMP Stream (128-bit)</summary>
    UMPStream = 0xF
}

/// <summary>
/// MIDI 2.0 channel voice message status.
/// </summary>
public enum Midi2ChannelVoiceStatus : byte
{
    /// <summary>Registered Per-Note Controller</summary>
    RegisteredPerNoteController = 0x00,
    /// <summary>Assignable Per-Note Controller</summary>
    AssignablePerNoteController = 0x10,
    /// <summary>Registered Controller (RPN)</summary>
    RegisteredController = 0x20,
    /// <summary>Assignable Controller (NRPN)</summary>
    AssignableController = 0x30,
    /// <summary>Relative Registered Controller</summary>
    RelativeRegisteredController = 0x40,
    /// <summary>Relative Assignable Controller</summary>
    RelativeAssignableController = 0x50,
    /// <summary>Per-Note Pitch Bend</summary>
    PerNotePitchBend = 0x60,
    /// <summary>Note Off</summary>
    NoteOff = 0x80,
    /// <summary>Note On</summary>
    NoteOn = 0x90,
    /// <summary>Poly Pressure (Aftertouch)</summary>
    PolyPressure = 0xA0,
    /// <summary>Control Change</summary>
    ControlChange = 0xB0,
    /// <summary>Program Change</summary>
    ProgramChange = 0xC0,
    /// <summary>Channel Pressure (Aftertouch)</summary>
    ChannelPressure = 0xD0,
    /// <summary>Pitch Bend</summary>
    PitchBend = 0xE0,
    /// <summary>Per-Note Management</summary>
    PerNoteManagement = 0xF0
}

/// <summary>
/// MIDI 2.0 attribute type for Note On/Off messages.
/// </summary>
public enum NoteAttributeType : byte
{
    /// <summary>No attribute</summary>
    None = 0x00,
    /// <summary>Manufacturer specific</summary>
    ManufacturerSpecific = 0x01,
    /// <summary>Profile specific</summary>
    ProfileSpecific = 0x02,
    /// <summary>Pitch 7.9 (additional pitch resolution)</summary>
    Pitch79 = 0x03
}

/// <summary>
/// Per-note controller types.
/// </summary>
public enum PerNoteControllerType : byte
{
    /// <summary>Modulation</summary>
    Modulation = 1,
    /// <summary>Breath</summary>
    Breath = 2,
    /// <summary>Pitch 7.25</summary>
    Pitch725 = 3,
    /// <summary>Volume</summary>
    Volume = 7,
    /// <summary>Balance</summary>
    Balance = 8,
    /// <summary>Pan</summary>
    Pan = 10,
    /// <summary>Expression</summary>
    Expression = 11,
    /// <summary>Sound Controller 1-10 (70-79)</summary>
    SoundController1 = 70,
    /// <summary>Timbre/Harmonic Intensity</summary>
    Timbre = 74,
    /// <summary>Release Time</summary>
    ReleaseTime = 72,
    /// <summary>Attack Time</summary>
    AttackTime = 73,
    /// <summary>Brightness</summary>
    Brightness = 74,
    /// <summary>Decay Time</summary>
    DecayTime = 75,
    /// <summary>Vibrato Rate</summary>
    VibratoRate = 76,
    /// <summary>Vibrato Depth</summary>
    VibratoDepth = 77,
    /// <summary>Vibrato Delay</summary>
    VibratoDelay = 78
}

/// <summary>
/// MIDI 2.0 Universal MIDI Packet (32-bit word).
/// </summary>
public struct UMPWord
{
    public uint Data;

    public UMPWord(uint data) => Data = data;

    public UMPMessageType MessageType => (UMPMessageType)((Data >> 28) & 0x0F);
    public byte Group => (byte)((Data >> 24) & 0x0F);
    public byte Status => (byte)((Data >> 16) & 0xFF);
    public byte Channel => (byte)((Data >> 16) & 0x0F);
    public byte Byte2 => (byte)((Data >> 8) & 0xFF);
    public byte Byte3 => (byte)(Data & 0xFF);

    public static UMPWord Create(UMPMessageType type, byte group, byte status, byte b2, byte b3)
    {
        return new UMPWord(
            ((uint)type << 28) |
            ((uint)(group & 0x0F) << 24) |
            ((uint)status << 16) |
            ((uint)b2 << 8) |
            b3
        );
    }
}

/// <summary>
/// MIDI 2.0 Note On message with 16-bit velocity and attributes.
/// </summary>
public struct Midi2NoteOn
{
    /// <summary>MIDI group (0-15)</summary>
    public byte Group;

    /// <summary>MIDI channel (0-15)</summary>
    public byte Channel;

    /// <summary>Note number (0-127)</summary>
    public byte Note;

    /// <summary>16-bit velocity (0-65535)</summary>
    public ushort Velocity;

    /// <summary>Attribute type</summary>
    public NoteAttributeType AttributeType;

    /// <summary>16-bit attribute value</summary>
    public ushort AttributeValue;

    /// <summary>
    /// Gets velocity as a normalized float (0.0-1.0).
    /// </summary>
    public float VelocityNormalized => Velocity / 65535f;

    /// <summary>
    /// Gets velocity as MIDI 1.0 value (0-127).
    /// </summary>
    public byte VelocityMidi1 => (byte)(Velocity >> 9);

    /// <summary>
    /// Converts to Universal MIDI Packet (64-bit).
    /// </summary>
    public ulong ToUMP()
    {
        uint word1 = ((uint)UMPMessageType.Midi2ChannelVoice << 28) |
                     ((uint)(Group & 0x0F) << 24) |
                     ((uint)Midi2ChannelVoiceStatus.NoteOn << 16) |
                     ((uint)(Channel & 0x0F) << 16) |
                     ((uint)Note << 8) |
                     (uint)AttributeType;

        uint word2 = ((uint)Velocity << 16) | AttributeValue;

        return ((ulong)word1 << 32) | word2;
    }

    /// <summary>
    /// Creates from UMP data.
    /// </summary>
    public static Midi2NoteOn FromUMP(ulong ump)
    {
        uint word1 = (uint)(ump >> 32);
        uint word2 = (uint)ump;

        return new Midi2NoteOn
        {
            Group = (byte)((word1 >> 24) & 0x0F),
            Channel = (byte)((word1 >> 16) & 0x0F),
            Note = (byte)((word1 >> 8) & 0xFF),
            AttributeType = (NoteAttributeType)(word1 & 0xFF),
            Velocity = (ushort)(word2 >> 16),
            AttributeValue = (ushort)word2
        };
    }
}

/// <summary>
/// MIDI 2.0 Per-Note Pitch Bend message.
/// </summary>
public struct Midi2PerNotePitchBend
{
    /// <summary>MIDI group (0-15)</summary>
    public byte Group;

    /// <summary>MIDI channel (0-15)</summary>
    public byte Channel;

    /// <summary>Note number (0-127)</summary>
    public byte Note;

    /// <summary>32-bit pitch bend value (0x80000000 = center)</summary>
    public uint PitchBend;

    /// <summary>
    /// Gets pitch bend as semitones (-48 to +48 range typical).
    /// </summary>
    public float PitchBendSemitones => ((PitchBend / (float)uint.MaxValue) - 0.5f) * 96f;

    /// <summary>
    /// Creates from semitone offset.
    /// </summary>
    public static Midi2PerNotePitchBend FromSemitones(byte group, byte channel, byte note, float semitones)
    {
        float normalized = (semitones / 96f) + 0.5f;
        normalized = Math.Clamp(normalized, 0f, 1f);

        return new Midi2PerNotePitchBend
        {
            Group = group,
            Channel = channel,
            Note = note,
            PitchBend = (uint)(normalized * uint.MaxValue)
        };
    }

    /// <summary>
    /// Converts to Universal MIDI Packet (64-bit).
    /// </summary>
    public ulong ToUMP()
    {
        uint word1 = ((uint)UMPMessageType.Midi2ChannelVoice << 28) |
                     ((uint)(Group & 0x0F) << 24) |
                     ((uint)Midi2ChannelVoiceStatus.PerNotePitchBend << 16) |
                     ((uint)(Channel & 0x0F) << 16) |
                     ((uint)Note << 8);

        return ((ulong)word1 << 32) | PitchBend;
    }
}

/// <summary>
/// MIDI 2.0 Per-Note Controller message.
/// </summary>
public struct Midi2PerNoteController
{
    /// <summary>MIDI group (0-15)</summary>
    public byte Group;

    /// <summary>MIDI channel (0-15)</summary>
    public byte Channel;

    /// <summary>Note number (0-127)</summary>
    public byte Note;

    /// <summary>Controller index</summary>
    public byte Index;

    /// <summary>32-bit controller value</summary>
    public uint Value;

    /// <summary>Whether this is a registered (true) or assignable (false) controller</summary>
    public bool IsRegistered;

    /// <summary>
    /// Gets value as normalized float (0.0-1.0).
    /// </summary>
    public float ValueNormalized => Value / (float)uint.MaxValue;

    /// <summary>
    /// Converts to Universal MIDI Packet (64-bit).
    /// </summary>
    public ulong ToUMP()
    {
        var status = IsRegistered
            ? Midi2ChannelVoiceStatus.RegisteredPerNoteController
            : Midi2ChannelVoiceStatus.AssignablePerNoteController;

        uint word1 = ((uint)UMPMessageType.Midi2ChannelVoice << 28) |
                     ((uint)(Group & 0x0F) << 24) |
                     ((uint)status << 16) |
                     ((uint)(Channel & 0x0F) << 16) |
                     ((uint)Note << 8) |
                     Index;

        return ((ulong)word1 << 32) | Value;
    }
}

/// <summary>
/// MIDI 2.0 Articulation message (part of Note On attributes).
/// </summary>
public struct Midi2Articulation
{
    /// <summary>Articulation type (0-255)</summary>
    public byte ArticulationType;

    /// <summary>Articulation data</summary>
    public byte ArticulationData;

    // Common articulation types
    public const byte Staccato = 1;
    public const byte Legato = 2;
    public const byte Accent = 3;
    public const byte Tenuto = 4;
    public const byte Marcato = 5;
    public const byte Sforzando = 6;
    public const byte Pizzicato = 7;
    public const byte Tremolo = 8;
    public const byte Vibrato = 9;
    public const byte Trill = 10;
    public const byte Glissando = 11;
    public const byte Portamento = 12;
    public const byte HammerOn = 13;
    public const byte PullOff = 14;
    public const byte Bend = 15;
    public const byte Slide = 16;
    public const byte Harmonic = 17;
    public const byte Mute = 18;
    public const byte OpenString = 19;
}

/// <summary>
/// MIDI 2.0 Property Exchange message container.
/// </summary>
public class PropertyExchangeMessage
{
    /// <summary>Request ID for correlation</summary>
    public byte RequestId { get; set; }

    /// <summary>Property header</summary>
    public string Header { get; set; } = "";

    /// <summary>Property data (JSON)</summary>
    public string Data { get; set; } = "";

    /// <summary>Whether this is a complete message or chunk</summary>
    public bool IsComplete { get; set; } = true;

    /// <summary>Chunk index for multi-part messages</summary>
    public int ChunkIndex { get; set; }

    /// <summary>Total chunks for multi-part messages</summary>
    public int TotalChunks { get; set; } = 1;
}

/// <summary>
/// Processes MIDI 2.0 Universal MIDI Packets with backward compatibility to MIDI 1.0.
/// </summary>
public class MIDI2Processor
{
    // Active notes with per-note state
    private readonly Dictionary<(byte group, byte channel, byte note), NoteState> _activeNotes = new();

    // Per-note controllers
    private readonly Dictionary<(byte group, byte channel, byte note, byte controller), uint> _perNoteControllers = new();

    // Channel state
    private readonly Dictionary<(byte group, byte channel), ChannelState> _channelStates = new();

    // Property exchange buffers
    private readonly Dictionary<byte, PropertyExchangeBuffer> _propertyExchangeBuffers = new();

    // Event queues
    private readonly ConcurrentQueue<MIDI2Event> _eventQueue = new();

    /// <summary>
    /// Event raised when a MIDI 2.0 Note On is received.
    /// </summary>
    public event Action<Midi2NoteOn>? NoteOnReceived;

    /// <summary>
    /// Event raised when a MIDI 2.0 Note Off is received.
    /// </summary>
    public event Action<byte, byte, byte, ushort>? NoteOffReceived; // group, channel, note, velocity

    /// <summary>
    /// Event raised when a per-note pitch bend is received.
    /// </summary>
    public event Action<Midi2PerNotePitchBend>? PerNotePitchBendReceived;

    /// <summary>
    /// Event raised when a per-note controller is received.
    /// </summary>
    public event Action<Midi2PerNoteController>? PerNoteControllerReceived;

    /// <summary>
    /// Event raised when a property exchange message is complete.
    /// </summary>
    public event Action<PropertyExchangeMessage>? PropertyExchangeReceived;

    /// <summary>
    /// Processes a 32-bit UMP word.
    /// </summary>
    public void ProcessUMP32(uint data)
    {
        var word = new UMPWord(data);
        ProcessUMPWord(word);
    }

    /// <summary>
    /// Processes a 64-bit UMP (two words).
    /// </summary>
    public void ProcessUMP64(ulong data)
    {
        uint word1 = (uint)(data >> 32);
        uint word2 = (uint)data;

        var umpWord = new UMPWord(word1);

        switch (umpWord.MessageType)
        {
            case UMPMessageType.Midi2ChannelVoice:
                ProcessMidi2ChannelVoice(word1, word2);
                break;

            case UMPMessageType.Data64:
                ProcessData64(word1, word2);
                break;
        }
    }

    /// <summary>
    /// Processes a 128-bit UMP (four words).
    /// </summary>
    public void ProcessUMP128(uint[] words)
    {
        if (words.Length != 4)
            throw new ArgumentException("UMP128 requires exactly 4 words");

        var umpWord = new UMPWord(words[0]);

        switch (umpWord.MessageType)
        {
            case UMPMessageType.Data128:
                ProcessData128(words);
                break;

            case UMPMessageType.FlexData:
                ProcessFlexData(words);
                break;

            case UMPMessageType.UMPStream:
                ProcessUMPStream(words);
                break;
        }
    }

    private void ProcessUMPWord(UMPWord word)
    {
        switch (word.MessageType)
        {
            case UMPMessageType.Utility:
                ProcessUtility(word);
                break;

            case UMPMessageType.SystemRealTimeCommon:
                ProcessSystemRealTime(word);
                break;

            case UMPMessageType.Midi1ChannelVoice:
                ProcessMidi1ChannelVoice(word);
                break;
        }
    }

    private void ProcessUtility(UMPWord word)
    {
        byte status = (byte)((word.Data >> 16) & 0xF0);

        switch (status)
        {
            case 0x00: // NOOP
                break;
            case 0x10: // JR Clock
                // Jitter reduction clock - used for timing
                break;
            case 0x20: // JR Timestamp
                // Jitter reduction timestamp
                break;
        }
    }

    private void ProcessSystemRealTime(UMPWord word)
    {
        byte status = word.Status;

        switch (status)
        {
            case 0xF8: // Timing Clock
            case 0xFA: // Start
            case 0xFB: // Continue
            case 0xFC: // Stop
            case 0xFE: // Active Sensing
            case 0xFF: // Reset
                // Handle system real-time messages
                break;
        }
    }

    private void ProcessMidi1ChannelVoice(UMPWord word)
    {
        byte group = word.Group;
        byte status = (byte)(word.Status & 0xF0);
        byte channel = (byte)(word.Status & 0x0F);

        switch (status)
        {
            case 0x90: // Note On
                if (word.Byte3 > 0)
                {
                    // Convert MIDI 1.0 velocity to 16-bit
                    ushort velocity16 = (ushort)(word.Byte3 << 9);
                    var noteOn = new Midi2NoteOn
                    {
                        Group = group,
                        Channel = channel,
                        Note = word.Byte2,
                        Velocity = velocity16,
                        AttributeType = NoteAttributeType.None
                    };
                    HandleNoteOn(noteOn);
                }
                else
                {
                    // Note On with velocity 0 = Note Off
                    HandleNoteOff(group, channel, word.Byte2, 0);
                }
                break;

            case 0x80: // Note Off
                ushort offVelocity = (ushort)(word.Byte3 << 9);
                HandleNoteOff(group, channel, word.Byte2, offVelocity);
                break;

            case 0xA0: // Poly Pressure
                // Convert to 32-bit value
                uint pressure32 = (uint)word.Byte3 << 25;
                UpdatePerNoteController(group, channel, word.Byte2, (byte)PerNoteControllerType.Expression, pressure32, false);
                break;

            case 0xB0: // Control Change
                HandleControlChange(group, channel, word.Byte2, word.Byte3);
                break;

            case 0xC0: // Program Change
                HandleProgramChange(group, channel, word.Byte2);
                break;

            case 0xD0: // Channel Pressure
                UpdateChannelPressure(group, channel, (uint)word.Byte2 << 25);
                break;

            case 0xE0: // Pitch Bend
                int bend14 = word.Byte2 | (word.Byte3 << 7);
                uint bend32 = (uint)(bend14 << 18);
                UpdateChannelPitchBend(group, channel, bend32);
                break;
        }
    }

    private void ProcessMidi2ChannelVoice(uint word1, uint word2)
    {
        byte group = (byte)((word1 >> 24) & 0x0F);
        byte status = (byte)((word1 >> 20) & 0x0F);
        byte channel = (byte)((word1 >> 16) & 0x0F);
        byte noteOrIndex = (byte)((word1 >> 8) & 0xFF);
        byte attributeOrController = (byte)(word1 & 0xFF);

        switch (status)
        {
            case 0x0: // Registered Per-Note Controller
                UpdatePerNoteController(group, channel, noteOrIndex, attributeOrController, word2, true);
                break;

            case 0x1: // Assignable Per-Note Controller
                UpdatePerNoteController(group, channel, noteOrIndex, attributeOrController, word2, false);
                break;

            case 0x6: // Per-Note Pitch Bend
                var pitchBend = new Midi2PerNotePitchBend
                {
                    Group = group,
                    Channel = channel,
                    Note = noteOrIndex,
                    PitchBend = word2
                };
                HandlePerNotePitchBend(pitchBend);
                break;

            case 0x8: // Note Off
                ushort offVelocity = (ushort)(word2 >> 16);
                HandleNoteOff(group, channel, noteOrIndex, offVelocity);
                break;

            case 0x9: // Note On
                var noteOn = new Midi2NoteOn
                {
                    Group = group,
                    Channel = channel,
                    Note = noteOrIndex,
                    AttributeType = (NoteAttributeType)attributeOrController,
                    Velocity = (ushort)(word2 >> 16),
                    AttributeValue = (ushort)word2
                };
                HandleNoteOn(noteOn);
                break;

            case 0xA: // Poly Pressure
                UpdatePerNoteController(group, channel, noteOrIndex, (byte)PerNoteControllerType.Expression, word2, false);
                break;

            case 0xB: // Control Change
                HandleControlChange32(group, channel, attributeOrController, word2);
                break;

            case 0xD: // Channel Pressure
                UpdateChannelPressure(group, channel, word2);
                break;

            case 0xE: // Pitch Bend
                UpdateChannelPitchBend(group, channel, word2);
                break;

            case 0xF: // Per-Note Management
                HandlePerNoteManagement(group, channel, noteOrIndex, attributeOrController);
                break;
        }
    }

    private void ProcessData64(uint word1, uint word2)
    {
        // SysEx7 and SysEx8 messages
        byte status = (byte)((word1 >> 20) & 0x0F);

        // Process system exclusive data
    }

    private void ProcessData128(uint[] words)
    {
        // Mixed Data Set messages
    }

    private void ProcessFlexData(uint[] words)
    {
        // Flex Data for metadata, performance text, etc.
        byte group = (byte)((words[0] >> 24) & 0x0F);
        byte format = (byte)((words[0] >> 22) & 0x03);
        byte statusBank = (byte)((words[0] >> 8) & 0xFF);
        byte status = (byte)(words[0] & 0xFF);

        // Handle various flex data types
    }

    private void ProcessUMPStream(uint[] words)
    {
        // UMP Stream messages for device discovery, configuration
        byte status = (byte)((words[0] >> 16) & 0x3FF);

        switch (status)
        {
            case 0x00: // Endpoint Discovery
            case 0x01: // Endpoint Info Notification
            case 0x02: // Device Identity Notification
            case 0x03: // Endpoint Name Notification
            case 0x04: // Product Instance ID Notification
            case 0x05: // Stream Configuration Request
            case 0x06: // Stream Configuration Notification
                // Handle endpoint/stream messages
                break;
        }
    }

    private void HandleNoteOn(Midi2NoteOn noteOn)
    {
        var key = (noteOn.Group, noteOn.Channel, noteOn.Note);

        _activeNotes[key] = new NoteState
        {
            Velocity = noteOn.Velocity,
            AttributeType = noteOn.AttributeType,
            AttributeValue = noteOn.AttributeValue,
            StartTime = DateTime.UtcNow
        };

        NoteOnReceived?.Invoke(noteOn);
    }

    private void HandleNoteOff(byte group, byte channel, byte note, ushort velocity)
    {
        var key = (group, channel, note);
        _activeNotes.Remove(key);

        // Clear per-note controllers for this note
        var controllersToRemove = new List<(byte, byte, byte, byte)>();
        foreach (var k in _perNoteControllers.Keys)
        {
            if (k.group == group && k.channel == channel && k.note == note)
                controllersToRemove.Add(k);
        }
        foreach (var k in controllersToRemove)
            _perNoteControllers.Remove(k);

        NoteOffReceived?.Invoke(group, channel, note, velocity);
    }

    private void HandlePerNotePitchBend(Midi2PerNotePitchBend pitchBend)
    {
        PerNotePitchBendReceived?.Invoke(pitchBend);
    }

    private void UpdatePerNoteController(byte group, byte channel, byte note, byte controller, uint value, bool isRegistered)
    {
        var key = (group, channel, note, controller);
        _perNoteControllers[key] = value;

        PerNoteControllerReceived?.Invoke(new Midi2PerNoteController
        {
            Group = group,
            Channel = channel,
            Note = note,
            Index = controller,
            Value = value,
            IsRegistered = isRegistered
        });
    }

    private void HandleControlChange(byte group, byte channel, byte controller, byte value)
    {
        EnsureChannelState(group, channel);
        // Convert 7-bit to 32-bit
        uint value32 = (uint)value << 25;
        _channelStates[(group, channel)].Controllers[controller] = value32;
    }

    private void HandleControlChange32(byte group, byte channel, byte controller, uint value)
    {
        EnsureChannelState(group, channel);
        _channelStates[(group, channel)].Controllers[controller] = value;
    }

    private void HandleProgramChange(byte group, byte channel, byte program)
    {
        EnsureChannelState(group, channel);
        _channelStates[(group, channel)].Program = program;
    }

    private void UpdateChannelPressure(byte group, byte channel, uint pressure)
    {
        EnsureChannelState(group, channel);
        _channelStates[(group, channel)].Pressure = pressure;
    }

    private void UpdateChannelPitchBend(byte group, byte channel, uint pitchBend)
    {
        EnsureChannelState(group, channel);
        _channelStates[(group, channel)].PitchBend = pitchBend;
    }

    private void HandlePerNoteManagement(byte group, byte channel, byte note, byte options)
    {
        // Per-note management for detach/attach and reset
        bool detach = (options & 0x02) != 0;
        bool reset = (options & 0x01) != 0;

        if (reset)
        {
            // Reset per-note controllers for this note
            var controllersToReset = new List<(byte, byte, byte, byte)>();
            foreach (var k in _perNoteControllers.Keys)
            {
                if (k.group == group && k.channel == channel && k.note == note)
                    controllersToReset.Add(k);
            }
            foreach (var k in controllersToReset)
                _perNoteControllers.Remove(k);
        }
    }

    private void EnsureChannelState(byte group, byte channel)
    {
        var key = (group, channel);
        if (!_channelStates.ContainsKey(key))
        {
            _channelStates[key] = new ChannelState();
        }
    }

    /// <summary>
    /// Converts a MIDI 1.0 message to UMP format.
    /// </summary>
    public static uint ConvertMidi1ToUMP(byte group, byte status, byte data1, byte data2)
    {
        return UMPWord.Create(UMPMessageType.Midi1ChannelVoice, group, status, data1, data2).Data;
    }

    /// <summary>
    /// Converts a MIDI 2.0 Note On to MIDI 1.0 format.
    /// </summary>
    public static (byte status, byte note, byte velocity) ConvertNoteOnToMidi1(Midi2NoteOn noteOn)
    {
        byte status = (byte)(0x90 | (noteOn.Channel & 0x0F));
        return (status, noteOn.Note, noteOn.VelocityMidi1);
    }

    /// <summary>
    /// Gets the current state of a per-note controller.
    /// </summary>
    public uint GetPerNoteController(byte group, byte channel, byte note, byte controller)
    {
        var key = (group, channel, note, controller);
        return _perNoteControllers.TryGetValue(key, out var value) ? value : 0;
    }

    /// <summary>
    /// Gets the current state of a channel controller.
    /// </summary>
    public uint GetChannelController(byte group, byte channel, byte controller)
    {
        var key = (group, channel);
        if (_channelStates.TryGetValue(key, out var state))
        {
            return state.Controllers.TryGetValue(controller, out var value) ? value : 0;
        }
        return 0;
    }

    /// <summary>
    /// Gets all currently active notes.
    /// </summary>
    public IEnumerable<(byte group, byte channel, byte note, NoteState state)> GetActiveNotes()
    {
        foreach (var kvp in _activeNotes)
        {
            yield return (kvp.Key.group, kvp.Key.channel, kvp.Key.note, kvp.Value);
        }
    }

    /// <summary>
    /// Resets all state.
    /// </summary>
    public void Reset()
    {
        _activeNotes.Clear();
        _perNoteControllers.Clear();
        _channelStates.Clear();
        _propertyExchangeBuffers.Clear();
    }

    /// <summary>
    /// State for an active note.
    /// </summary>
    public class NoteState
    {
        public ushort Velocity { get; set; }
        public NoteAttributeType AttributeType { get; set; }
        public ushort AttributeValue { get; set; }
        public DateTime StartTime { get; set; }
    }

    /// <summary>
    /// State for a channel.
    /// </summary>
    private class ChannelState
    {
        public Dictionary<byte, uint> Controllers { get; } = new();
        public byte Program { get; set; }
        public uint Pressure { get; set; }
        public uint PitchBend { get; set; } = 0x80000000; // Center
    }

    /// <summary>
    /// Buffer for assembling multi-part property exchange messages.
    /// </summary>
    private class PropertyExchangeBuffer
    {
        public List<byte[]> Chunks { get; } = new();
        public int TotalChunks { get; set; }
    }

    /// <summary>
    /// Generic MIDI 2.0 event for queuing.
    /// </summary>
    private struct MIDI2Event
    {
        public UMPMessageType Type;
        public ulong Data;
        public DateTime Timestamp;
    }
}
