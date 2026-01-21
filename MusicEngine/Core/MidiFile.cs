//Engine License (MEL) – Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: MIDI file import and export functionality.


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;


namespace MusicEngine.Core;


/// <summary>
/// MIDI file format types
/// </summary>
public enum MidiFileFormat
{
    /// <summary>Single track</summary>
    SingleTrack = 0,
    /// <summary>Multiple tracks, synchronous</summary>
    MultiTrack = 1,
    /// <summary>Multiple tracks, asynchronous</summary>
    MultiTrackAsync = 2
}


/// <summary>
/// MIDI event types
/// </summary>
public enum MidiEventType
{
    NoteOff = 0x80,
    NoteOn = 0x90,
    PolyPressure = 0xA0,
    ControlChange = 0xB0,
    ProgramChange = 0xC0,
    ChannelPressure = 0xD0,
    PitchBend = 0xE0,
    SysEx = 0xF0,
    Meta = 0xFF
}


/// <summary>
/// MIDI meta event types
/// </summary>
public enum MidiMetaType
{
    SequenceNumber = 0x00,
    TextEvent = 0x01,
    Copyright = 0x02,
    TrackName = 0x03,
    InstrumentName = 0x04,
    Lyric = 0x05,
    Marker = 0x06,
    CuePoint = 0x07,
    ChannelPrefix = 0x20,
    EndOfTrack = 0x2F,
    SetTempo = 0x51,
    SmpteOffset = 0x54,
    TimeSignature = 0x58,
    KeySignature = 0x59
}


/// <summary>
/// Represents a MIDI event in a file
/// </summary>
public class MidiFileEvent
{
    /// <summary>Delta time from previous event in ticks</summary>
    public long DeltaTime { get; set; }

    /// <summary>Absolute time in ticks</summary>
    public long AbsoluteTime { get; set; }

    /// <summary>Event type</summary>
    public MidiEventType EventType { get; set; }

    /// <summary>MIDI channel (0-15)</summary>
    public int Channel { get; set; }

    /// <summary>First data byte (note number, controller, etc.)</summary>
    public int Data1 { get; set; }

    /// <summary>Second data byte (velocity, value, etc.)</summary>
    public int Data2 { get; set; }

    /// <summary>Meta event type (if EventType is Meta)</summary>
    public MidiMetaType? MetaType { get; set; }

    /// <summary>Meta/SysEx data</summary>
    public byte[]? MetaData { get; set; }
}


/// <summary>
/// Represents a MIDI track
/// </summary>
public class MidiTrack
{
    /// <summary>Track name</summary>
    public string Name { get; set; } = "";

    /// <summary>Events in this track</summary>
    public List<MidiFileEvent> Events { get; } = new();

    /// <summary>Get note events only</summary>
    public IEnumerable<MidiFileEvent> NoteEvents =>
        Events.Where(e => e.EventType == MidiEventType.NoteOn || e.EventType == MidiEventType.NoteOff);
}


/// <summary>
/// Represents a complete MIDI file
/// </summary>
public class MidiFileData
{
    /// <summary>File format (0, 1, or 2)</summary>
    public MidiFileFormat Format { get; set; } = MidiFileFormat.MultiTrack;

    /// <summary>Ticks per quarter note (PPQN)</summary>
    public int TicksPerQuarterNote { get; set; } = 480;

    /// <summary>Tracks in the file</summary>
    public List<MidiTrack> Tracks { get; } = new();

    /// <summary>Initial tempo in BPM (from first tempo event)</summary>
    public double InitialTempo { get; set; } = 120.0;

    /// <summary>Time signature (beats per bar)</summary>
    public int BeatsPerBar { get; set; } = 4;

    /// <summary>Time signature (beat unit)</summary>
    public int BeatUnit { get; set; } = 4;

    /// <summary>Get total duration in ticks</summary>
    public long DurationTicks
    {
        get
        {
            long maxTime = 0;
            foreach (var track in Tracks)
            {
                if (track.Events.Count > 0)
                {
                    maxTime = Math.Max(maxTime, track.Events.Max(e => e.AbsoluteTime));
                }
            }
            return maxTime;
        }
    }

    /// <summary>Get total duration in beats</summary>
    public double DurationBeats => (double)DurationTicks / TicksPerQuarterNote;

    /// <summary>Convert ticks to beats</summary>
    public double TicksToBeats(long ticks) => (double)ticks / TicksPerQuarterNote;

    /// <summary>Convert beats to ticks</summary>
    public long BeatsToTicks(double beats) => (long)(beats * TicksPerQuarterNote);
}


/// <summary>
/// MIDI file reader and writer
/// </summary>
public static class MidiFile
{
    /// <summary>
    /// Load a MIDI file
    /// </summary>
    public static MidiFileData Load(string path)
    {
        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    /// <summary>
    /// Load a MIDI file from a stream
    /// </summary>
    public static MidiFileData Load(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.ASCII, true);
        var data = new MidiFileData();

        // Read header
        string headerChunk = new string(reader.ReadChars(4));
        if (headerChunk != "MThd")
        {
            throw new InvalidDataException("Invalid MIDI file: Missing MThd header");
        }

        int headerLength = ReadInt32BigEndian(reader);
        if (headerLength != 6)
        {
            throw new InvalidDataException($"Invalid MIDI header length: {headerLength}");
        }

        data.Format = (MidiFileFormat)ReadInt16BigEndian(reader);
        int trackCount = ReadInt16BigEndian(reader);
        int timeDivision = ReadInt16BigEndian(reader);

        // Check if SMPTE or PPQN
        if ((timeDivision & 0x8000) != 0)
        {
            // SMPTE - not commonly used, convert to approximate PPQN
            data.TicksPerQuarterNote = 480;
        }
        else
        {
            data.TicksPerQuarterNote = timeDivision;
        }

        // Read tracks
        for (int i = 0; i < trackCount; i++)
        {
            var track = ReadTrack(reader);
            data.Tracks.Add(track);

            // Extract tempo from first track if available
            foreach (var evt in track.Events)
            {
                if (evt.MetaType == MidiMetaType.SetTempo && evt.MetaData != null)
                {
                    int microsecondsPerBeat = (evt.MetaData[0] << 16) | (evt.MetaData[1] << 8) | evt.MetaData[2];
                    data.InitialTempo = 60000000.0 / microsecondsPerBeat;
                    break;
                }
                if (evt.MetaType == MidiMetaType.TimeSignature && evt.MetaData != null && evt.MetaData.Length >= 2)
                {
                    data.BeatsPerBar = evt.MetaData[0];
                    data.BeatUnit = (int)Math.Pow(2, evt.MetaData[1]);
                }
            }
        }

        return data;
    }

    /// <summary>
    /// Read a single track
    /// </summary>
    private static MidiTrack ReadTrack(BinaryReader reader)
    {
        var track = new MidiTrack();

        string trackChunk = new string(reader.ReadChars(4));
        if (trackChunk != "MTrk")
        {
            throw new InvalidDataException($"Invalid track header: {trackChunk}");
        }

        int trackLength = ReadInt32BigEndian(reader);
        long trackEnd = reader.BaseStream.Position + trackLength;

        long absoluteTime = 0;
        int runningStatus = 0;

        while (reader.BaseStream.Position < trackEnd)
        {
            long deltaTime = ReadVariableLengthQuantity(reader);
            absoluteTime += deltaTime;

            int statusByte = reader.ReadByte();

            // Handle running status
            if (statusByte < 0x80)
            {
                // This is a data byte, use running status
                reader.BaseStream.Position--;
                statusByte = runningStatus;
            }
            else
            {
                runningStatus = statusByte;
            }

            var evt = new MidiFileEvent
            {
                DeltaTime = deltaTime,
                AbsoluteTime = absoluteTime
            };

            if (statusByte == 0xFF)
            {
                // Meta event
                evt.EventType = MidiEventType.Meta;
                evt.MetaType = (MidiMetaType)reader.ReadByte();
                int length = (int)ReadVariableLengthQuantity(reader);
                evt.MetaData = reader.ReadBytes(length);

                if (evt.MetaType == MidiMetaType.TrackName && evt.MetaData != null)
                {
                    track.Name = Encoding.ASCII.GetString(evt.MetaData);
                }
            }
            else if (statusByte == 0xF0 || statusByte == 0xF7)
            {
                // SysEx event
                evt.EventType = MidiEventType.SysEx;
                int length = (int)ReadVariableLengthQuantity(reader);
                evt.MetaData = reader.ReadBytes(length);
            }
            else
            {
                // Channel event
                evt.EventType = (MidiEventType)(statusByte & 0xF0);
                evt.Channel = statusByte & 0x0F;
                evt.Data1 = reader.ReadByte();

                // Some events have only one data byte
                if (evt.EventType != MidiEventType.ProgramChange &&
                    evt.EventType != MidiEventType.ChannelPressure)
                {
                    evt.Data2 = reader.ReadByte();
                }
            }

            track.Events.Add(evt);

            // Check for end of track
            if (evt.MetaType == MidiMetaType.EndOfTrack)
            {
                break;
            }
        }

        // Ensure we're at the right position
        reader.BaseStream.Position = trackEnd;

        return track;
    }

    /// <summary>
    /// Save a MIDI file
    /// </summary>
    public static void Save(MidiFileData data, string path)
    {
        using var stream = File.Create(path);
        Save(data, stream);
    }

    /// <summary>
    /// Save a MIDI file to a stream
    /// </summary>
    public static void Save(MidiFileData data, Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.ASCII, true);

        // Write header
        writer.Write("MThd".ToCharArray());
        WriteInt32BigEndian(writer, 6);
        WriteInt16BigEndian(writer, (short)data.Format);
        WriteInt16BigEndian(writer, (short)data.Tracks.Count);
        WriteInt16BigEndian(writer, (short)data.TicksPerQuarterNote);

        // Write tracks
        foreach (var track in data.Tracks)
        {
            WriteTrack(writer, track, data);
        }
    }

    /// <summary>
    /// Write a single track
    /// </summary>
    private static void WriteTrack(BinaryWriter writer, MidiTrack track, MidiFileData data)
    {
        using var trackStream = new MemoryStream();
        using var trackWriter = new BinaryWriter(trackStream);

        long lastTime = 0;

        // Write tempo at start of first track
        if (data.Tracks.IndexOf(track) == 0)
        {
            // Tempo event
            WriteVariableLengthQuantity(trackWriter, 0);
            trackWriter.Write((byte)0xFF);
            trackWriter.Write((byte)MidiMetaType.SetTempo);
            trackWriter.Write((byte)3);
            int microsecondsPerBeat = (int)(60000000.0 / data.InitialTempo);
            trackWriter.Write((byte)((microsecondsPerBeat >> 16) & 0xFF));
            trackWriter.Write((byte)((microsecondsPerBeat >> 8) & 0xFF));
            trackWriter.Write((byte)(microsecondsPerBeat & 0xFF));

            // Time signature
            WriteVariableLengthQuantity(trackWriter, 0);
            trackWriter.Write((byte)0xFF);
            trackWriter.Write((byte)MidiMetaType.TimeSignature);
            trackWriter.Write((byte)4);
            trackWriter.Write((byte)data.BeatsPerBar);
            trackWriter.Write((byte)(int)Math.Log2(data.BeatUnit));
            trackWriter.Write((byte)24); // MIDI clocks per metronome
            trackWriter.Write((byte)8);  // 32nd notes per quarter
        }

        // Write track name if present
        if (!string.IsNullOrEmpty(track.Name))
        {
            WriteVariableLengthQuantity(trackWriter, 0);
            trackWriter.Write((byte)0xFF);
            trackWriter.Write((byte)MidiMetaType.TrackName);
            byte[] nameBytes = Encoding.ASCII.GetBytes(track.Name);
            WriteVariableLengthQuantity(trackWriter, nameBytes.Length);
            trackWriter.Write(nameBytes);
        }

        // Write events
        foreach (var evt in track.Events.OrderBy(e => e.AbsoluteTime))
        {
            long deltaTime = evt.AbsoluteTime - lastTime;
            lastTime = evt.AbsoluteTime;

            WriteVariableLengthQuantity(trackWriter, deltaTime);

            if (evt.EventType == MidiEventType.Meta)
            {
                trackWriter.Write((byte)0xFF);
                trackWriter.Write((byte)(evt.MetaType ?? MidiMetaType.TextEvent));
                if (evt.MetaData != null)
                {
                    WriteVariableLengthQuantity(trackWriter, evt.MetaData.Length);
                    trackWriter.Write(evt.MetaData);
                }
                else
                {
                    trackWriter.Write((byte)0);
                }
            }
            else if (evt.EventType == MidiEventType.SysEx)
            {
                trackWriter.Write((byte)0xF0);
                if (evt.MetaData != null)
                {
                    WriteVariableLengthQuantity(trackWriter, evt.MetaData.Length);
                    trackWriter.Write(evt.MetaData);
                }
            }
            else
            {
                byte status = (byte)((int)evt.EventType | evt.Channel);
                trackWriter.Write(status);
                trackWriter.Write((byte)evt.Data1);

                if (evt.EventType != MidiEventType.ProgramChange &&
                    evt.EventType != MidiEventType.ChannelPressure)
                {
                    trackWriter.Write((byte)evt.Data2);
                }
            }
        }

        // End of track
        WriteVariableLengthQuantity(trackWriter, 0);
        trackWriter.Write((byte)0xFF);
        trackWriter.Write((byte)MidiMetaType.EndOfTrack);
        trackWriter.Write((byte)0);

        // Write track chunk
        byte[] trackData = trackStream.ToArray();
        writer.Write("MTrk".ToCharArray());
        WriteInt32BigEndian(writer, trackData.Length);
        writer.Write(trackData);
    }

    /// <summary>
    /// Convert a Pattern to MIDI file data
    /// </summary>
    public static MidiFileData FromPattern(Pattern pattern, int channel = 0, double bpm = 120)
    {
        var data = new MidiFileData
        {
            Format = MidiFileFormat.SingleTrack,
            TicksPerQuarterNote = 480,
            InitialTempo = bpm
        };

        var track = new MidiTrack { Name = pattern.Name };

        foreach (var note in pattern.Events)
        {
            long startTicks = data.BeatsToTicks(note.Beat);
            long endTicks = data.BeatsToTicks(note.Beat + note.Duration);

            // Note On
            track.Events.Add(new MidiFileEvent
            {
                AbsoluteTime = startTicks,
                EventType = MidiEventType.NoteOn,
                Channel = channel,
                Data1 = note.Note,
                Data2 = note.Velocity
            });

            // Note Off
            track.Events.Add(new MidiFileEvent
            {
                AbsoluteTime = endTicks,
                EventType = MidiEventType.NoteOff,
                Channel = channel,
                Data1 = note.Note,
                Data2 = 0
            });
        }

        // Sort events and calculate delta times
        var sortedEvents = track.Events.OrderBy(e => e.AbsoluteTime).ThenByDescending(e => e.EventType).ToList();
        long lastTime = 0;
        foreach (var evt in sortedEvents)
        {
            evt.DeltaTime = evt.AbsoluteTime - lastTime;
            lastTime = evt.AbsoluteTime;
        }

        track.Events.Clear();
        track.Events.AddRange(sortedEvents);

        data.Tracks.Add(track);
        return data;
    }

    /// <summary>
    /// Convert MIDI file data to a Pattern
    /// </summary>
    public static Pattern ToPattern(MidiFileData data, ISynth synth, int trackIndex = 0, int channel = -1)
    {
        var pattern = new Pattern(synth);

        if (trackIndex >= data.Tracks.Count)
        {
            return pattern;
        }

        var track = data.Tracks[trackIndex];
        pattern.Name = track.Name;
        pattern.LoopLength = data.DurationBeats;

        // Track note-on events to pair with note-off
        var activeNotes = new Dictionary<int, (double startBeat, int velocity)>();

        foreach (var evt in track.Events)
        {
            if (channel >= 0 && evt.Channel != channel) continue;

            double beat = data.TicksToBeats(evt.AbsoluteTime);

            if (evt.EventType == MidiEventType.NoteOn && evt.Data2 > 0)
            {
                // Note on
                activeNotes[evt.Data1] = (beat, evt.Data2);
            }
            else if (evt.EventType == MidiEventType.NoteOff ||
                     (evt.EventType == MidiEventType.NoteOn && evt.Data2 == 0))
            {
                // Note off
                if (activeNotes.TryGetValue(evt.Data1, out var noteInfo))
                {
                    double duration = beat - noteInfo.startBeat;
                    pattern.Note(evt.Data1, noteInfo.startBeat, duration, noteInfo.velocity);
                    activeNotes.Remove(evt.Data1);
                }
            }
        }

        return pattern;
    }

    /// <summary>
    /// Get all patterns from a MIDI file (one per track)
    /// </summary>
    public static List<Pattern> ToPatterns(MidiFileData data, Func<int, ISynth> synthFactory)
    {
        var patterns = new List<Pattern>();

        for (int i = 0; i < data.Tracks.Count; i++)
        {
            var synth = synthFactory(i);
            var pattern = ToPattern(data, synth, i);
            if (pattern.Events.Count > 0)
            {
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    // Helper methods for big-endian reading/writing
    private static int ReadInt32BigEndian(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(4);
        return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
    }

    private static short ReadInt16BigEndian(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(2);
        return (short)((bytes[0] << 8) | bytes[1]);
    }

    private static void WriteInt32BigEndian(BinaryWriter writer, int value)
    {
        writer.Write((byte)((value >> 24) & 0xFF));
        writer.Write((byte)((value >> 16) & 0xFF));
        writer.Write((byte)((value >> 8) & 0xFF));
        writer.Write((byte)(value & 0xFF));
    }

    private static void WriteInt16BigEndian(BinaryWriter writer, short value)
    {
        writer.Write((byte)((value >> 8) & 0xFF));
        writer.Write((byte)(value & 0xFF));
    }

    private static long ReadVariableLengthQuantity(BinaryReader reader)
    {
        long value = 0;
        byte b;

        do
        {
            b = reader.ReadByte();
            value = (value << 7) | (long)(b & 0x7F);
        } while ((b & 0x80) != 0);

        return value;
    }

    private static void WriteVariableLengthQuantity(BinaryWriter writer, long value)
    {
        if (value < 0) value = 0;

        // Calculate the number of bytes needed
        var bytes = new List<byte>();
        do
        {
            bytes.Insert(0, (byte)(value & 0x7F));
            value >>= 7;
        } while (value > 0);

        // Set continuation bits
        for (int i = 0; i < bytes.Count - 1; i++)
        {
            bytes[i] |= 0x80;
        }

        writer.Write(bytes.ToArray());
    }
}
