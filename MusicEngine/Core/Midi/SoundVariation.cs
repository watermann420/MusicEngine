// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MIDI handling component.

using System;
using System.Collections.Generic;


namespace MusicEngine.Core.Midi;


/// <summary>
/// Types of MIDI messages.
/// </summary>
public enum MidiMessageType
{
    /// <summary>
    /// Note On message (status 0x90-0x9F).
    /// </summary>
    NoteOn,

    /// <summary>
    /// Note Off message (status 0x80-0x8F).
    /// </summary>
    NoteOff,

    /// <summary>
    /// Control Change message (status 0xB0-0xBF).
    /// </summary>
    ControlChange,

    /// <summary>
    /// Program Change message (status 0xC0-0xCF).
    /// </summary>
    ProgramChange,

    /// <summary>
    /// Pitch Bend message (status 0xE0-0xEF).
    /// </summary>
    PitchBend
}


/// <summary>
/// Represents a single MIDI message.
/// </summary>
public class MidiMessage
{
    /// <summary>
    /// Gets or sets the type of MIDI message.
    /// </summary>
    public MidiMessageType Type { get; set; }

    /// <summary>
    /// Gets or sets the MIDI channel (0-15).
    /// </summary>
    public int Channel { get; set; }

    /// <summary>
    /// Gets or sets the first data byte.
    /// For NoteOn/NoteOff: Note number (0-127)
    /// For ControlChange: Controller number (0-127)
    /// For ProgramChange: Program number (0-127)
    /// For PitchBend: LSB (0-127)
    /// </summary>
    public int Data1 { get; set; }

    /// <summary>
    /// Gets or sets the second data byte.
    /// For NoteOn/NoteOff: Velocity (0-127)
    /// For ControlChange: Value (0-127)
    /// For ProgramChange: Not used
    /// For PitchBend: MSB (0-127)
    /// </summary>
    public int Data2 { get; set; }


    /// <summary>
    /// Creates a new MIDI message with default values.
    /// </summary>
    public MidiMessage()
    {
    }


    /// <summary>
    /// Creates a new MIDI message with the specified values.
    /// </summary>
    /// <param name="type">The message type.</param>
    /// <param name="channel">The MIDI channel.</param>
    /// <param name="data1">The first data byte.</param>
    /// <param name="data2">The second data byte.</param>
    public MidiMessage(MidiMessageType type, int channel, int data1, int data2 = 0)
    {
        Type = type;
        Channel = Math.Clamp(channel, 0, 15);
        Data1 = Math.Clamp(data1, 0, 127);
        Data2 = Math.Clamp(data2, 0, 127);
    }


    /// <summary>
    /// Converts this message to raw MIDI bytes.
    /// </summary>
    /// <returns>Array of MIDI bytes.</returns>
    public byte[] ToBytes()
    {
        byte status = Type switch
        {
            MidiMessageType.NoteOff => (byte)(0x80 | (Channel & 0x0F)),
            MidiMessageType.NoteOn => (byte)(0x90 | (Channel & 0x0F)),
            MidiMessageType.ControlChange => (byte)(0xB0 | (Channel & 0x0F)),
            MidiMessageType.ProgramChange => (byte)(0xC0 | (Channel & 0x0F)),
            MidiMessageType.PitchBend => (byte)(0xE0 | (Channel & 0x0F)),
            _ => throw new InvalidOperationException($"Unknown message type: {Type}")
        };

        // Program Change only has one data byte
        if (Type == MidiMessageType.ProgramChange)
        {
            return new[] { status, (byte)Data1 };
        }

        return new[] { status, (byte)Data1, (byte)Data2 };
    }


    /// <summary>
    /// Creates a Note On message.
    /// </summary>
    public static MidiMessage NoteOn(int note, int velocity, int channel = 0)
    {
        return new MidiMessage(MidiMessageType.NoteOn, channel, note, velocity);
    }


    /// <summary>
    /// Creates a Note Off message.
    /// </summary>
    public static MidiMessage NoteOff(int note, int velocity = 0, int channel = 0)
    {
        return new MidiMessage(MidiMessageType.NoteOff, channel, note, velocity);
    }


    /// <summary>
    /// Creates a Control Change message.
    /// </summary>
    public static MidiMessage CC(int controller, int value, int channel = 0)
    {
        return new MidiMessage(MidiMessageType.ControlChange, channel, controller, value);
    }


    /// <summary>
    /// Creates a Program Change message.
    /// </summary>
    public static MidiMessage PC(int program, int channel = 0)
    {
        return new MidiMessage(MidiMessageType.ProgramChange, channel, program, 0);
    }


    /// <inheritdoc/>
    public override string ToString()
    {
        return Type switch
        {
            MidiMessageType.NoteOn => $"NoteOn Ch{Channel + 1} Note{Data1} Vel{Data2}",
            MidiMessageType.NoteOff => $"NoteOff Ch{Channel + 1} Note{Data1} Vel{Data2}",
            MidiMessageType.ControlChange => $"CC Ch{Channel + 1} CC{Data1}={Data2}",
            MidiMessageType.ProgramChange => $"PC Ch{Channel + 1} Prog{Data1}",
            MidiMessageType.PitchBend => $"PitchBend Ch{Channel + 1} {(Data2 << 7) | Data1}",
            _ => $"Unknown MIDI Message"
        };
    }
}


/// <summary>
/// Represents a sound variation that can be triggered via MIDI messages.
/// Sound variations are used to switch between different sounds or playing techniques
/// within a virtual instrument.
/// </summary>
public class SoundVariation
{
    private readonly List<MidiMessage> _triggerMessages = new();


    /// <summary>
    /// Gets the unique identifier for this sound variation.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets or sets the name of this sound variation.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of this sound variation.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the slot number (typically 1-8) for this variation.
    /// </summary>
    public int Slot { get; set; } = 1;

    /// <summary>
    /// Gets the list of MIDI messages that trigger this variation.
    /// </summary>
    public IReadOnlyList<MidiMessage> TriggerMessages => _triggerMessages;


    /// <summary>
    /// Creates a new sound variation with a unique ID.
    /// </summary>
    public SoundVariation()
    {
        Id = Guid.NewGuid();
    }


    /// <summary>
    /// Creates a new sound variation with a specific ID.
    /// </summary>
    /// <param name="id">The unique identifier.</param>
    public SoundVariation(Guid id)
    {
        Id = id;
    }


    /// <summary>
    /// Creates a new sound variation with the specified name.
    /// </summary>
    /// <param name="name">The name of the variation.</param>
    /// <param name="slot">The slot number.</param>
    public SoundVariation(string name, int slot = 1)
    {
        Id = Guid.NewGuid();
        Name = name;
        Slot = slot;
    }


    /// <summary>
    /// Adds a control change message to the trigger messages.
    /// </summary>
    /// <param name="controller">The CC controller number (0-127).</param>
    /// <param name="value">The CC value (0-127).</param>
    /// <param name="channel">The MIDI channel (0-15).</param>
    public void AddCC(int controller, int value, int channel = 0)
    {
        _triggerMessages.Add(MidiMessage.CC(controller, value, channel));
    }


    /// <summary>
    /// Adds a program change message to the trigger messages.
    /// </summary>
    /// <param name="program">The program number (0-127).</param>
    /// <param name="channel">The MIDI channel (0-15).</param>
    public void AddProgramChange(int program, int channel = 0)
    {
        _triggerMessages.Add(MidiMessage.PC(program, channel));
    }


    /// <summary>
    /// Adds a keyswitch (note on/off pair) to the trigger messages.
    /// </summary>
    /// <param name="note">The note number (0-127).</param>
    /// <param name="velocity">The velocity (0-127).</param>
    /// <param name="channel">The MIDI channel (0-15).</param>
    public void AddKeyswitch(int note, int velocity = 100, int channel = 0)
    {
        _triggerMessages.Add(MidiMessage.NoteOn(note, velocity, channel));
        _triggerMessages.Add(MidiMessage.NoteOff(note, 0, channel));
    }


    /// <summary>
    /// Adds a custom MIDI message to the trigger messages.
    /// </summary>
    /// <param name="message">The MIDI message to add.</param>
    public void AddMessage(MidiMessage message)
    {
        if (message != null)
        {
            _triggerMessages.Add(message);
        }
    }


    /// <summary>
    /// Clears all trigger messages.
    /// </summary>
    public void ClearMessages()
    {
        _triggerMessages.Clear();
    }


    /// <summary>
    /// Gets all trigger messages as raw MIDI bytes.
    /// </summary>
    /// <returns>Array of byte arrays, one per message.</returns>
    public byte[][] GetAllMessageBytes()
    {
        var result = new byte[_triggerMessages.Count][];

        for (int i = 0; i < _triggerMessages.Count; i++)
        {
            result[i] = _triggerMessages[i].ToBytes();
        }

        return result;
    }


    /// <inheritdoc/>
    public override string ToString()
    {
        return $"Slot {Slot}: {Name}";
    }


    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is SoundVariation other && Id == other.Id;
    }


    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}
