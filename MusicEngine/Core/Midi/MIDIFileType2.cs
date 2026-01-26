// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MIDI handling component.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MusicEngine.Core.Midi;

/// <summary>
/// Represents a named pattern in a Type 2 MIDI file.
/// </summary>
public class MIDIPattern
{
    /// <summary>Pattern index.</summary>
    public int Index { get; set; }

    /// <summary>Pattern name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Tempo in BPM for this pattern.</summary>
    public double Tempo { get; set; } = 120.0;

    /// <summary>Time signature numerator.</summary>
    public int TimeSignatureNumerator { get; set; } = 4;

    /// <summary>Time signature denominator.</summary>
    public int TimeSignatureDenominator { get; set; } = 4;

    /// <summary>Length in ticks.</summary>
    public long LengthTicks { get; set; }

    /// <summary>Events in this pattern.</summary>
    public List<MidiFileEvent> Events { get; } = new();

    /// <summary>Gets length in beats.</summary>
    public double LengthBeats(int ppq) => (double)LengthTicks / ppq;

    /// <summary>Gets note events only.</summary>
    public IEnumerable<MidiFileEvent> NoteEvents =>
        Events.Where(e => e.EventType == MidiEventType.NoteOn || e.EventType == MidiEventType.NoteOff);
}

/// <summary>
/// Represents a pattern sequence order entry.
/// </summary>
public struct PatternSequenceEntry
{
    /// <summary>Pattern index to play.</summary>
    public int PatternIndex { get; set; }

    /// <summary>Number of times to repeat (1 = play once).</summary>
    public int Repeats { get; set; }

    /// <summary>Start time in the arrangement (beats).</summary>
    public double StartBeat { get; set; }

    /// <summary>Optional transpose amount in semitones.</summary>
    public int Transpose { get; set; }

    /// <summary>Creates a sequence entry.</summary>
    public PatternSequenceEntry(int patternIndex, int repeats = 1, double startBeat = 0, int transpose = 0)
    {
        PatternIndex = patternIndex;
        Repeats = repeats;
        StartBeat = startBeat;
        Transpose = transpose;
    }
}

/// <summary>
/// Container for Type 2 MIDI file data.
/// </summary>
public class MIDIFileType2Data
{
    /// <summary>Ticks per quarter note.</summary>
    public int TicksPerQuarterNote { get; set; } = 480;

    /// <summary>Patterns in the file.</summary>
    public List<MIDIPattern> Patterns { get; } = new();

    /// <summary>Pattern sequence order (optional).</summary>
    public List<PatternSequenceEntry> Sequence { get; } = new();

    /// <summary>Gets total duration of all patterns in beats.</summary>
    public double TotalDurationBeats
    {
        get
        {
            double total = 0;
            foreach (var pattern in Patterns)
            {
                total += pattern.LengthBeats(TicksPerQuarterNote);
            }
            return total;
        }
    }

    /// <summary>Gets a pattern by name.</summary>
    public MIDIPattern? GetPattern(string name)
    {
        return Patterns.Find(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Gets a pattern by index.</summary>
    public MIDIPattern? GetPattern(int index)
    {
        return index >= 0 && index < Patterns.Count ? Patterns[index] : null;
    }
}

/// <summary>
/// Reader and writer for MIDI File Type 2 (multi-pattern format).
/// </summary>
/// <remarks>
/// MIDI File Type 2 stores multiple independent patterns (tracks) that
/// are not synchronized. Each pattern can have its own tempo, time signature,
/// and length. This format is useful for:
///
/// - Pattern-based sequencers (like drum machines)
/// - Loop/sample libraries
/// - Groove templates
/// - Multi-song collections
///
/// Unlike Type 0 (single track) and Type 1 (synchronized tracks),
/// Type 2 patterns are completely independent sequences.
/// </remarks>
public static class MIDIFileType2
{
    /// <summary>
    /// Loads a Type 2 MIDI file.
    /// </summary>
    /// <param name="path">Path to the MIDI file.</param>
    /// <returns>Type 2 MIDI data or null if invalid.</returns>
    public static MIDIFileType2Data? Load(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            using var stream = File.OpenRead(path);
            return Load(stream);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading Type 2 MIDI file: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Loads a Type 2 MIDI file from a stream.
    /// </summary>
    /// <param name="stream">Input stream.</param>
    /// <returns>Type 2 MIDI data or null if invalid.</returns>
    public static MIDIFileType2Data? Load(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.ASCII, true);

        // Read header
        string headerChunk = new string(reader.ReadChars(4));
        if (headerChunk != "MThd")
            return null;

        int headerLength = ReadInt32BE(reader);
        if (headerLength != 6)
            return null;

        int format = ReadInt16BE(reader);
        // Allow Type 1 files to be loaded as Type 2 (patterns are tracks)
        if (format != 2 && format != 1)
            return null;

        int numTracks = ReadInt16BE(reader);
        int division = ReadInt16BE(reader);

        var data = new MIDIFileType2Data();

        // Handle SMPTE vs PPQ
        if ((division & 0x8000) != 0)
        {
            data.TicksPerQuarterNote = 480; // Default for SMPTE
        }
        else
        {
            data.TicksPerQuarterNote = division;
        }

        // Read tracks as patterns
        for (int i = 0; i < numTracks; i++)
        {
            var pattern = ReadPattern(reader, i);
            if (pattern != null)
            {
                data.Patterns.Add(pattern);
            }
        }

        return data;
    }

    private static MIDIPattern? ReadPattern(BinaryReader reader, int index)
    {
        string trackChunk = new string(reader.ReadChars(4));
        if (trackChunk != "MTrk")
            return null;

        int trackLength = ReadInt32BE(reader);
        long trackEnd = reader.BaseStream.Position + trackLength;

        var pattern = new MIDIPattern { Index = index };
        long absoluteTime = 0;
        int runningStatus = 0;

        while (reader.BaseStream.Position < trackEnd)
        {
            long deltaTime = ReadVariableLength(reader);
            absoluteTime += deltaTime;

            int statusByte = reader.ReadByte();

            // Handle running status
            if (statusByte < 0x80)
            {
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
                int length = (int)ReadVariableLength(reader);
                evt.MetaData = reader.ReadBytes(length);

                // Extract pattern info from meta events
                switch (evt.MetaType)
                {
                    case MidiMetaType.TrackName:
                        pattern.Name = Encoding.ASCII.GetString(evt.MetaData);
                        break;

                    case MidiMetaType.SetTempo:
                        if (evt.MetaData.Length >= 3)
                        {
                            int microsecondsPerBeat = (evt.MetaData[0] << 16) | (evt.MetaData[1] << 8) | evt.MetaData[2];
                            pattern.Tempo = 60000000.0 / microsecondsPerBeat;
                        }
                        break;

                    case MidiMetaType.TimeSignature:
                        if (evt.MetaData.Length >= 2)
                        {
                            pattern.TimeSignatureNumerator = evt.MetaData[0];
                            pattern.TimeSignatureDenominator = (int)Math.Pow(2, evt.MetaData[1]);
                        }
                        break;

                    case MidiMetaType.EndOfTrack:
                        pattern.LengthTicks = absoluteTime;
                        break;
                }
            }
            else if (statusByte == 0xF0 || statusByte == 0xF7)
            {
                // SysEx
                evt.EventType = MidiEventType.SysEx;
                int length = (int)ReadVariableLength(reader);
                evt.MetaData = reader.ReadBytes(length);
            }
            else
            {
                // Channel event
                evt.EventType = (MidiEventType)(statusByte & 0xF0);
                evt.Channel = statusByte & 0x0F;
                evt.Data1 = reader.ReadByte();

                if (evt.EventType != MidiEventType.ProgramChange &&
                    evt.EventType != MidiEventType.ChannelPressure)
                {
                    evt.Data2 = reader.ReadByte();
                }
            }

            pattern.Events.Add(evt);

            if (evt.MetaType == MidiMetaType.EndOfTrack)
                break;
        }

        reader.BaseStream.Position = trackEnd;

        // Set default name if not specified
        if (string.IsNullOrEmpty(pattern.Name))
        {
            pattern.Name = $"Pattern {index + 1}";
        }

        return pattern;
    }

    /// <summary>
    /// Saves Type 2 MIDI data to a file.
    /// </summary>
    /// <param name="data">Type 2 MIDI data.</param>
    /// <param name="path">Output file path.</param>
    public static void Save(MIDIFileType2Data data, string path)
    {
        using var stream = File.Create(path);
        Save(data, stream);
    }

    /// <summary>
    /// Saves Type 2 MIDI data to a stream.
    /// </summary>
    /// <param name="data">Type 2 MIDI data.</param>
    /// <param name="stream">Output stream.</param>
    public static void Save(MIDIFileType2Data data, Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.ASCII, true);

        // Write header
        writer.Write("MThd".ToCharArray());
        WriteInt32BE(writer, 6);
        WriteInt16BE(writer, 2); // Type 2
        WriteInt16BE(writer, (short)data.Patterns.Count);
        WriteInt16BE(writer, (short)data.TicksPerQuarterNote);

        // Write each pattern as a track
        foreach (var pattern in data.Patterns)
        {
            WritePattern(writer, pattern, data.TicksPerQuarterNote);
        }
    }

    private static void WritePattern(BinaryWriter writer, MIDIPattern pattern, int ppq)
    {
        using var trackStream = new MemoryStream();
        using var trackWriter = new BinaryWriter(trackStream);

        long lastTime = 0;

        // Write tempo meta event
        WriteVariableLength(trackWriter, 0);
        trackWriter.Write((byte)0xFF);
        trackWriter.Write((byte)MidiMetaType.SetTempo);
        trackWriter.Write((byte)3);
        int microsecondsPerBeat = (int)(60000000.0 / pattern.Tempo);
        trackWriter.Write((byte)((microsecondsPerBeat >> 16) & 0xFF));
        trackWriter.Write((byte)((microsecondsPerBeat >> 8) & 0xFF));
        trackWriter.Write((byte)(microsecondsPerBeat & 0xFF));

        // Write time signature meta event
        WriteVariableLength(trackWriter, 0);
        trackWriter.Write((byte)0xFF);
        trackWriter.Write((byte)MidiMetaType.TimeSignature);
        trackWriter.Write((byte)4);
        trackWriter.Write((byte)pattern.TimeSignatureNumerator);
        trackWriter.Write((byte)(int)Math.Log2(pattern.TimeSignatureDenominator));
        trackWriter.Write((byte)24);
        trackWriter.Write((byte)8);

        // Write track name meta event
        if (!string.IsNullOrEmpty(pattern.Name))
        {
            WriteVariableLength(trackWriter, 0);
            trackWriter.Write((byte)0xFF);
            trackWriter.Write((byte)MidiMetaType.TrackName);
            byte[] nameBytes = Encoding.ASCII.GetBytes(pattern.Name);
            WriteVariableLength(trackWriter, nameBytes.Length);
            trackWriter.Write(nameBytes);
        }

        // Write events
        foreach (var evt in pattern.Events.OrderBy(e => e.AbsoluteTime))
        {
            // Skip meta events we've already written
            if (evt.EventType == MidiEventType.Meta &&
                (evt.MetaType == MidiMetaType.SetTempo ||
                 evt.MetaType == MidiMetaType.TimeSignature ||
                 evt.MetaType == MidiMetaType.TrackName ||
                 evt.MetaType == MidiMetaType.EndOfTrack))
            {
                continue;
            }

            long deltaTime = evt.AbsoluteTime - lastTime;
            lastTime = evt.AbsoluteTime;

            WriteVariableLength(trackWriter, deltaTime);

            if (evt.EventType == MidiEventType.Meta)
            {
                trackWriter.Write((byte)0xFF);
                trackWriter.Write((byte)(evt.MetaType ?? MidiMetaType.TextEvent));
                if (evt.MetaData != null)
                {
                    WriteVariableLength(trackWriter, evt.MetaData.Length);
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
                    WriteVariableLength(trackWriter, evt.MetaData.Length);
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

        // Write end of track
        WriteVariableLength(trackWriter, 0);
        trackWriter.Write((byte)0xFF);
        trackWriter.Write((byte)MidiMetaType.EndOfTrack);
        trackWriter.Write((byte)0);

        // Write track chunk
        byte[] trackData = trackStream.ToArray();
        writer.Write("MTrk".ToCharArray());
        WriteInt32BE(writer, trackData.Length);
        writer.Write(trackData);
    }

    /// <summary>
    /// Creates Type 2 MIDI data from multiple MusicEngine patterns.
    /// </summary>
    /// <param name="patterns">Patterns to convert.</param>
    /// <param name="ppq">Ticks per quarter note.</param>
    /// <param name="bpm">Default tempo.</param>
    /// <returns>Type 2 MIDI data.</returns>
    public static MIDIFileType2Data FromPatterns(IEnumerable<Pattern> patterns, int ppq = 480, double bpm = 120)
    {
        var data = new MIDIFileType2Data { TicksPerQuarterNote = ppq };

        int index = 0;
        foreach (var pattern in patterns)
        {
            var midiPattern = new MIDIPattern
            {
                Index = index++,
                Name = pattern.Name ?? $"Pattern {index}",
                Tempo = bpm,
                LengthTicks = (long)(pattern.LoopLength * ppq)
            };

            foreach (var note in pattern.Events)
            {
                long startTicks = (long)(note.Beat * ppq);
                long endTicks = (long)((note.Beat + note.Duration) * ppq);

                // Note On
                midiPattern.Events.Add(new MidiFileEvent
                {
                    AbsoluteTime = startTicks,
                    EventType = MidiEventType.NoteOn,
                    Channel = 0,
                    Data1 = note.Note,
                    Data2 = note.Velocity
                });

                // Note Off
                midiPattern.Events.Add(new MidiFileEvent
                {
                    AbsoluteTime = endTicks,
                    EventType = MidiEventType.NoteOff,
                    Channel = 0,
                    Data1 = note.Note,
                    Data2 = 0
                });
            }

            // Sort and calculate delta times
            var sortedEvents = midiPattern.Events.OrderBy(e => e.AbsoluteTime).ThenByDescending(e => e.EventType).ToList();
            long lastTime = 0;
            foreach (var evt in sortedEvents)
            {
                evt.DeltaTime = evt.AbsoluteTime - lastTime;
                lastTime = evt.AbsoluteTime;
            }

            midiPattern.Events.Clear();
            midiPattern.Events.AddRange(sortedEvents);

            data.Patterns.Add(midiPattern);
        }

        return data;
    }

    /// <summary>
    /// Converts Type 2 MIDI patterns to MusicEngine patterns.
    /// </summary>
    /// <param name="data">Type 2 MIDI data.</param>
    /// <param name="synthFactory">Factory to create synths for each pattern.</param>
    /// <returns>List of patterns.</returns>
    public static List<Pattern> ToPatterns(MIDIFileType2Data data, Func<int, ISynth> synthFactory)
    {
        var result = new List<Pattern>();

        for (int i = 0; i < data.Patterns.Count; i++)
        {
            var midiPattern = data.Patterns[i];
            var synth = synthFactory(i);
            var pattern = new Pattern(synth)
            {
                Name = midiPattern.Name,
                LoopLength = midiPattern.LengthBeats(data.TicksPerQuarterNote)
            };

            // Track note-on events
            var activeNotes = new Dictionary<int, (double startBeat, int velocity)>();

            foreach (var evt in midiPattern.Events)
            {
                double beat = (double)evt.AbsoluteTime / data.TicksPerQuarterNote;

                if (evt.EventType == MidiEventType.NoteOn && evt.Data2 > 0)
                {
                    activeNotes[evt.Data1] = (beat, evt.Data2);
                }
                else if (evt.EventType == MidiEventType.NoteOff ||
                         (evt.EventType == MidiEventType.NoteOn && evt.Data2 == 0))
                {
                    if (activeNotes.TryGetValue(evt.Data1, out var noteInfo))
                    {
                        double duration = beat - noteInfo.startBeat;
                        pattern.Note(evt.Data1, noteInfo.startBeat, duration, noteInfo.velocity);
                        activeNotes.Remove(evt.Data1);
                    }
                }
            }

            result.Add(pattern);
        }

        return result;
    }

    /// <summary>
    /// Arranges patterns into a linear sequence.
    /// </summary>
    /// <param name="data">Type 2 MIDI data.</param>
    /// <param name="sequence">Pattern sequence.</param>
    /// <returns>Type 1 MIDI data with arranged patterns.</returns>
    public static MidiFileData ArrangeToType1(MIDIFileType2Data data, IEnumerable<PatternSequenceEntry>? sequence = null)
    {
        var result = new MidiFileData
        {
            Format = MidiFileFormat.MultiTrack,
            TicksPerQuarterNote = data.TicksPerQuarterNote
        };

        // Use provided sequence or create default (all patterns in order)
        var seq = sequence?.ToList() ?? data.Patterns.Select((p, i) => new PatternSequenceEntry(i)).ToList();

        // Calculate arrangement
        double currentBeat = 0;
        var track = new MidiTrack { Name = "Arranged" };

        foreach (var entry in seq)
        {
            var pattern = data.GetPattern(entry.PatternIndex);
            if (pattern == null) continue;

            for (int rep = 0; rep < entry.Repeats; rep++)
            {
                double startBeat = entry.StartBeat > 0 ? entry.StartBeat : currentBeat;
                long startTicks = (long)(startBeat * data.TicksPerQuarterNote);

                foreach (var evt in pattern.Events)
                {
                    if (evt.EventType == MidiEventType.Meta)
                        continue; // Skip meta events

                    var newEvent = new MidiFileEvent
                    {
                        AbsoluteTime = startTicks + evt.AbsoluteTime,
                        EventType = evt.EventType,
                        Channel = evt.Channel,
                        Data1 = evt.Data1,
                        Data2 = evt.Data2
                    };

                    // Apply transpose
                    if (entry.Transpose != 0 &&
                        (evt.EventType == MidiEventType.NoteOn || evt.EventType == MidiEventType.NoteOff))
                    {
                        newEvent.Data1 = Math.Clamp(evt.Data1 + entry.Transpose, 0, 127);
                    }

                    track.Events.Add(newEvent);
                }

                currentBeat = startBeat + pattern.LengthBeats(data.TicksPerQuarterNote);
            }
        }

        // Sort and calculate delta times
        var sortedEvents = track.Events.OrderBy(e => e.AbsoluteTime).ToList();
        long lastTime = 0;
        foreach (var evt in sortedEvents)
        {
            evt.DeltaTime = evt.AbsoluteTime - lastTime;
            lastTime = evt.AbsoluteTime;
        }

        track.Events.Clear();
        track.Events.AddRange(sortedEvents);

        result.Tracks.Add(track);
        result.InitialTempo = data.Patterns.FirstOrDefault()?.Tempo ?? 120;

        return result;
    }

    // Helper methods
    private static int ReadInt16BE(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(2);
        return (bytes[0] << 8) | bytes[1];
    }

    private static int ReadInt32BE(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(4);
        return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
    }

    private static void WriteInt16BE(BinaryWriter writer, short value)
    {
        writer.Write((byte)((value >> 8) & 0xFF));
        writer.Write((byte)(value & 0xFF));
    }

    private static void WriteInt32BE(BinaryWriter writer, int value)
    {
        writer.Write((byte)((value >> 24) & 0xFF));
        writer.Write((byte)((value >> 16) & 0xFF));
        writer.Write((byte)((value >> 8) & 0xFF));
        writer.Write((byte)(value & 0xFF));
    }

    private static long ReadVariableLength(BinaryReader reader)
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

    private static void WriteVariableLength(BinaryWriter writer, long value)
    {
        if (value < 0) value = 0;

        var bytes = new List<byte>();
        do
        {
            bytes.Insert(0, (byte)(value & 0x7F));
            value >>= 7;
        } while (value > 0);

        for (int i = 0; i < bytes.Count - 1; i++)
        {
            bytes[i] |= 0x80;
        }

        writer.Write(bytes.ToArray());
    }
}
