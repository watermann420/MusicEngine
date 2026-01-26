//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NAudio.Midi;


namespace MusicEngine.Core;


/// <summary>
/// Provides functionality for exporting Pattern objects to Standard MIDI Files (.mid).
/// Uses NAudio.Midi for MIDI file writing with industry-standard format compliance.
/// </summary>
/// <remarks>
/// <para>
/// This exporter creates Type 1 MIDI files (multi-track synchronous format) which are
/// compatible with all major DAWs and MIDI software. The default resolution is 480 ticks
/// per quarter note (PPQN), which is the standard resolution used by most professional DAWs.
/// </para>
/// <para>
/// Features include:
/// <list type="bullet">
/// <item><description>Single pattern export with tempo information</description></item>
/// <item><description>Multi-pattern export as separate tracks</description></item>
/// <item><description>Full session export with all patterns and tempo</description></item>
/// <item><description>Proper beat-to-tick conversion for accurate timing</description></item>
/// <item><description>Track naming for easy identification in DAWs</description></item>
/// </list>
/// </para>
/// </remarks>
public class MidiExporter
{
    /// <summary>
    /// Standard MIDI resolution: 480 ticks per quarter note (PPQN).
    /// This is the most common resolution used by professional DAWs.
    /// </summary>
    public const int TicksPerQuarterNote = 480;

    /// <summary>
    /// Default MIDI channel for exported patterns (0-15).
    /// Channel 0 is the first channel in MIDI specification.
    /// </summary>
    public const int DefaultChannel = 0;

    /// <summary>
    /// Default velocity for notes that have invalid velocity values.
    /// </summary>
    public const int DefaultVelocity = 100;

    /// <summary>
    /// Gets or sets the MIDI channel to use for single pattern exports.
    /// Valid range is 0-15.
    /// </summary>
    public int Channel { get; set; } = DefaultChannel;

    /// <summary>
    /// Gets or sets whether to include track names in the exported MIDI file.
    /// </summary>
    public bool IncludeTrackNames { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include time signature meta events.
    /// </summary>
    public bool IncludeTimeSignature { get; set; } = true;

    /// <summary>
    /// Gets or sets the time signature numerator (beats per bar).
    /// </summary>
    public int TimeSignatureNumerator { get; set; } = 4;

    /// <summary>
    /// Gets or sets the time signature denominator (beat unit, as power of 2).
    /// 4 = quarter note, 8 = eighth note, etc.
    /// </summary>
    public int TimeSignatureDenominator { get; set; } = 4;

    /// <summary>
    /// Exports a single pattern to a Standard MIDI File.
    /// </summary>
    /// <param name="pattern">The pattern to export. Must not be null.</param>
    /// <param name="filePath">The output file path. Must be a valid path with .mid extension recommended.</param>
    /// <param name="bpm">The tempo in beats per minute. Valid range is 20-300 BPM.</param>
    /// <exception cref="ArgumentNullException">Thrown when pattern is null.</exception>
    /// <exception cref="ArgumentException">Thrown when filePath is null or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when bpm is outside valid range.</exception>
    /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
    /// <remarks>
    /// <para>
    /// This method creates a Type 1 MIDI file with a tempo track (track 0) and a single
    /// note track (track 1). The tempo track contains the tempo and time signature
    /// meta events.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var exporter = new MidiExporter();
    /// exporter.ExportPattern(myPattern, "output.mid", 120.0);
    /// </code>
    /// </para>
    /// </remarks>
    public void ExportPattern(Pattern pattern, string filePath, double bpm)
    {
        if (pattern == null)
        {
            throw new ArgumentNullException(nameof(pattern));
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        ValidateBpm(bpm);

        ExportPatterns(new[] { pattern }, filePath, bpm);
    }

    /// <summary>
    /// Exports multiple patterns to a Standard MIDI File, with each pattern as a separate track.
    /// </summary>
    /// <param name="patterns">The patterns to export. Must not be null or empty.</param>
    /// <param name="filePath">The output file path. Must be a valid path with .mid extension recommended.</param>
    /// <param name="bpm">The tempo in beats per minute. Valid range is 20-300 BPM.</param>
    /// <exception cref="ArgumentNullException">Thrown when patterns is null.</exception>
    /// <exception cref="ArgumentException">Thrown when patterns is empty or filePath is null or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when bpm is outside valid range.</exception>
    /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
    /// <remarks>
    /// <para>
    /// This method creates a Type 1 MIDI file with:
    /// <list type="bullet">
    /// <item><description>Track 0: Tempo track with tempo and time signature meta events</description></item>
    /// <item><description>Track 1+: One track per pattern with note events</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Each pattern is assigned to a different MIDI channel (0-15) automatically, wrapping
    /// around if more than 16 patterns are provided. Patterns can be assigned custom channels
    /// by setting the Channel property before calling this method, though this affects all patterns.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var exporter = new MidiExporter();
    /// var patterns = new List&lt;Pattern&gt; { pattern1, pattern2, pattern3 };
    /// exporter.ExportPatterns(patterns, "multitrack.mid", 128.0);
    /// </code>
    /// </para>
    /// </remarks>
    public void ExportPatterns(IEnumerable<Pattern> patterns, string filePath, double bpm)
    {
        if (patterns == null)
        {
            throw new ArgumentNullException(nameof(patterns));
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        ValidateBpm(bpm);

        var patternList = patterns.ToList();
        if (patternList.Count == 0)
        {
            throw new ArgumentException("At least one pattern must be provided.", nameof(patterns));
        }

        // Ensure directory exists
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Create the MIDI event collection
        var midiEvents = new MidiEventCollection(1, TicksPerQuarterNote);

        // Track 0: Tempo track (conductor track)
        CreateTempoTrack(midiEvents, bpm, patternList);

        // Track 1+: Pattern tracks
        int trackIndex = 1;
        int channelIndex = 0;

        foreach (var pattern in patternList)
        {
            if (pattern == null)
            {
                continue;
            }

            CreatePatternTrack(midiEvents, pattern, trackIndex, channelIndex);
            trackIndex++;
            channelIndex = (channelIndex + 1) % 16; // Wrap around MIDI channels
        }

        // Write the MIDI file
        WriteMidiFile(filePath, midiEvents);
    }

    /// <summary>
    /// Exports an entire session to a Standard MIDI File.
    /// </summary>
    /// <param name="session">The session to export. Must not be null.</param>
    /// <param name="filePath">The output file path. Must be a valid path with .mid extension recommended.</param>
    /// <exception cref="ArgumentNullException">Thrown when session is null.</exception>
    /// <exception cref="ArgumentException">Thrown when filePath is null or empty.</exception>
    /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
    /// <remarks>
    /// <para>
    /// This method exports all patterns in the session's data, using the session's BPM
    /// and time signature settings. The session metadata (name, author) is included
    /// as text meta events in the tempo track.
    /// </para>
    /// <para>
    /// The export uses the session's:
    /// <list type="bullet">
    /// <item><description>BPM (tempo)</description></item>
    /// <item><description>Time signature numerator and denominator</description></item>
    /// <item><description>All patterns in SessionData.Patterns</description></item>
    /// <item><description>Session name as sequence/track name</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var session = new Session();
    /// session.Load("mysong.json");
    /// var exporter = new MidiExporter();
    /// exporter.ExportSession(session, "mysong.mid");
    /// </code>
    /// </para>
    /// </remarks>
    public void ExportSession(EngineSession session, string filePath)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        var sessionData = session.Data;
        double bpm = sessionData.BPM;

        ValidateBpm(bpm);

        // Store original time signature settings and apply session settings
        int originalNumerator = TimeSignatureNumerator;
        int originalDenominator = TimeSignatureDenominator;

        try
        {
            TimeSignatureNumerator = sessionData.TimeSignatureNumerator;
            TimeSignatureDenominator = sessionData.TimeSignatureDenominator;

            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Create the MIDI event collection
            var midiEvents = new MidiEventCollection(1, TicksPerQuarterNote);

            // Track 0: Tempo track with session metadata
            CreateSessionTempoTrack(midiEvents, sessionData);

            // Track 1+: Pattern tracks from session data
            int trackIndex = 1;
            int channelIndex = 0;

            foreach (var patternConfig in sessionData.Patterns)
            {
                if (patternConfig == null)
                {
                    continue;
                }

                CreatePatternTrackFromConfig(midiEvents, patternConfig, trackIndex, channelIndex);
                trackIndex++;
                channelIndex = (channelIndex + 1) % 16;
            }

            // Write the MIDI file
            WriteMidiFile(filePath, midiEvents);
        }
        finally
        {
            // Restore original time signature settings
            TimeSignatureNumerator = originalNumerator;
            TimeSignatureDenominator = originalDenominator;
        }
    }

    /// <summary>
    /// Converts a beat position to MIDI ticks.
    /// </summary>
    /// <param name="beats">The beat position (can be fractional).</param>
    /// <returns>The equivalent position in MIDI ticks.</returns>
    /// <remarks>
    /// Uses the standard resolution of 480 ticks per quarter note.
    /// A beat value of 1.0 equals 480 ticks, 0.5 equals 240 ticks, etc.
    /// </remarks>
    public static long BeatsToTicks(double beats)
    {
        return (long)(beats * TicksPerQuarterNote);
    }

    /// <summary>
    /// Converts MIDI ticks to beat position.
    /// </summary>
    /// <param name="ticks">The MIDI tick position.</param>
    /// <returns>The equivalent beat position.</returns>
    public static double TicksToBeats(long ticks)
    {
        return (double)ticks / TicksPerQuarterNote;
    }

    /// <summary>
    /// Calculates the microseconds per beat for a given BPM.
    /// </summary>
    /// <param name="bpm">The tempo in beats per minute.</param>
    /// <returns>Microseconds per beat value for MIDI tempo meta event.</returns>
    public static int BpmToMicrosecondsPerBeat(double bpm)
    {
        return (int)(60_000_000.0 / bpm);
    }

    /// <summary>
    /// Creates the tempo (conductor) track with tempo and time signature events.
    /// </summary>
    /// <param name="midiEvents">The MIDI event collection to add events to.</param>
    /// <param name="bpm">The tempo in beats per minute.</param>
    /// <param name="patterns">The patterns being exported (used to determine track length).</param>
    private void CreateTempoTrack(MidiEventCollection midiEvents, double bpm, List<Pattern> patterns)
    {
        // Calculate the total length needed (longest pattern)
        double maxLength = patterns.Where(p => p != null).Max(p => p.LoopLength);
        long endTicks = BeatsToTicks(maxLength);

        // Add track 0 events
        midiEvents.AddTrack();

        // Tempo event at tick 0
        int microsecondsPerBeat = BpmToMicrosecondsPerBeat(bpm);
        var tempoEvent = new TempoEvent(microsecondsPerBeat, 0);
        midiEvents.AddEvent(tempoEvent, 0);

        // Time signature event at tick 0
        if (IncludeTimeSignature)
        {
            var timeSignatureEvent = new TimeSignatureEvent(
                0,
                TimeSignatureNumerator,
                (int)Math.Log2(TimeSignatureDenominator),
                24,  // MIDI clocks per metronome click
                8);  // Number of 32nd notes per beat
            midiEvents.AddEvent(timeSignatureEvent, 0);
        }

        // End of track
        var endOfTrack = new MetaEvent(MetaEventType.EndTrack, 0, endTicks);
        midiEvents.AddEvent(endOfTrack, 0);
    }

    /// <summary>
    /// Creates the tempo track for session export with session metadata.
    /// </summary>
    /// <param name="midiEvents">The MIDI event collection to add events to.</param>
    /// <param name="sessionData">The session data containing tempo and metadata.</param>
    private void CreateSessionTempoTrack(MidiEventCollection midiEvents, SessionData sessionData)
    {
        // Calculate the total length needed (longest pattern)
        double maxLength = sessionData.Patterns.Count > 0
            ? sessionData.Patterns.Max(p => p.LoopLength)
            : 4.0;
        long endTicks = BeatsToTicks(maxLength);

        // Add track 0 events
        midiEvents.AddTrack();

        // Track name (sequence name) from session metadata
        if (IncludeTrackNames && !string.IsNullOrEmpty(sessionData.Metadata.Name))
        {
            var trackNameEvent = new TextEvent(
                sessionData.Metadata.Name,
                MetaEventType.SequenceTrackName,
                0);
            midiEvents.AddEvent(trackNameEvent, 0);
        }

        // Copyright notice if author is specified
        if (!string.IsNullOrEmpty(sessionData.Metadata.Author))
        {
            var copyrightText = $"(c) {sessionData.Metadata.CreatedDate.Year} {sessionData.Metadata.Author}";
            var copyrightEvent = new TextEvent(
                copyrightText,
                MetaEventType.Copyright,
                0);
            midiEvents.AddEvent(copyrightEvent, 0);
        }

        // Tempo event at tick 0
        int microsecondsPerBeat = BpmToMicrosecondsPerBeat(sessionData.BPM);
        var tempoEvent = new TempoEvent(microsecondsPerBeat, 0);
        midiEvents.AddEvent(tempoEvent, 0);

        // Time signature event at tick 0
        if (IncludeTimeSignature)
        {
            var timeSignatureEvent = new TimeSignatureEvent(
                0,
                TimeSignatureNumerator,
                (int)Math.Log2(TimeSignatureDenominator),
                24,
                8);
            midiEvents.AddEvent(timeSignatureEvent, 0);
        }

        // End of track
        var endOfTrack = new MetaEvent(MetaEventType.EndTrack, 0, endTicks);
        midiEvents.AddEvent(endOfTrack, 0);
    }

    /// <summary>
    /// Creates a note track from a Pattern object.
    /// </summary>
    /// <param name="midiEvents">The MIDI event collection to add events to.</param>
    /// <param name="pattern">The pattern to convert.</param>
    /// <param name="trackIndex">The track index in the MIDI file.</param>
    /// <param name="channel">The MIDI channel for note events (0-15).</param>
    private void CreatePatternTrack(
        MidiEventCollection midiEvents,
        Pattern pattern,
        int trackIndex,
        int channel)
    {
        midiEvents.AddTrack();

        // Track name
        if (IncludeTrackNames)
        {
            string trackName = !string.IsNullOrEmpty(pattern.Name)
                ? pattern.Name
                : !string.IsNullOrEmpty(pattern.InstrumentName)
                    ? pattern.InstrumentName
                    : $"Track {trackIndex}";

            var trackNameEvent = new TextEvent(
                trackName,
                MetaEventType.SequenceTrackName,
                0);
            midiEvents.AddEvent(trackNameEvent, trackIndex);
        }

        // Convert note events
        foreach (var noteEvent in pattern.Events.OrderBy(e => e.Beat))
        {
            long startTicks = BeatsToTicks(noteEvent.Beat);
            long endTicks = BeatsToTicks(noteEvent.Beat + noteEvent.Duration);

            // Clamp values to valid MIDI range
            int note = Math.Clamp(noteEvent.Note, 0, 127);
            int velocity = noteEvent.Velocity > 0 ? Math.Clamp(noteEvent.Velocity, 1, 127) : DefaultVelocity;

            // Note On event
            var noteOn = new NoteOnEvent(startTicks, channel + 1, note, velocity, (int)(endTicks - startTicks));
            midiEvents.AddEvent(noteOn, trackIndex);

            // Note Off event (NoteOnEvent with velocity 0 is standard MIDI note-off)
            var noteOff = new NoteOnEvent(endTicks, channel + 1, note, 0, 0);
            midiEvents.AddEvent(noteOff, trackIndex);
        }

        // End of track
        long trackEndTicks = BeatsToTicks(pattern.LoopLength);
        var endOfTrack = new MetaEvent(MetaEventType.EndTrack, 0, trackEndTicks);
        midiEvents.AddEvent(endOfTrack, trackIndex);
    }

    /// <summary>
    /// Creates a note track from a PatternConfig object (for session export).
    /// </summary>
    /// <param name="midiEvents">The MIDI event collection to add events to.</param>
    /// <param name="patternConfig">The pattern configuration to convert.</param>
    /// <param name="trackIndex">The track index in the MIDI file.</param>
    /// <param name="channel">The MIDI channel for note events (0-15).</param>
    private void CreatePatternTrackFromConfig(
        MidiEventCollection midiEvents,
        PatternConfig patternConfig,
        int trackIndex,
        int channel)
    {
        midiEvents.AddTrack();

        // Track name
        if (IncludeTrackNames && !string.IsNullOrEmpty(patternConfig.Name))
        {
            var trackNameEvent = new TextEvent(
                patternConfig.Name,
                MetaEventType.SequenceTrackName,
                0);
            midiEvents.AddEvent(trackNameEvent, trackIndex);
        }

        // Convert note events
        foreach (var noteConfig in patternConfig.Events.OrderBy(e => e.Beat))
        {
            long startTicks = BeatsToTicks(noteConfig.Beat);
            long endTicks = BeatsToTicks(noteConfig.Beat + noteConfig.Duration);

            // Clamp values to valid MIDI range
            int note = Math.Clamp(noteConfig.Note, 0, 127);
            int velocity = noteConfig.Velocity > 0 ? Math.Clamp(noteConfig.Velocity, 1, 127) : DefaultVelocity;

            // Note On event
            var noteOn = new NoteOnEvent(startTicks, channel + 1, note, velocity, (int)(endTicks - startTicks));
            midiEvents.AddEvent(noteOn, trackIndex);

            // Note Off event (NoteOnEvent with velocity 0 is standard MIDI note-off)
            var noteOff = new NoteOnEvent(endTicks, channel + 1, note, 0, 0);
            midiEvents.AddEvent(noteOff, trackIndex);
        }

        // End of track
        long trackEndTicks = BeatsToTicks(patternConfig.LoopLength);
        var endOfTrack = new MetaEvent(MetaEventType.EndTrack, 0, trackEndTicks);
        midiEvents.AddEvent(endOfTrack, trackIndex);
    }

    /// <summary>
    /// Validates the BPM value is within acceptable range.
    /// </summary>
    /// <param name="bpm">The BPM value to validate.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when BPM is outside valid range.</exception>
    private static void ValidateBpm(double bpm)
    {
        if (bpm < 20.0 || bpm > 300.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bpm),
                bpm,
                "BPM must be between 20 and 300.");
        }
    }

    /// <summary>
    /// Writes the MIDI event collection to a file using standard MIDI file format.
    /// </summary>
    /// <param name="filePath">The output file path.</param>
    /// <param name="midiEvents">The MIDI event collection to write.</param>
    private static void WriteMidiFile(string filePath, MidiEventCollection midiEvents)
    {
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(stream);

        // Write MIDI file header
        // "MThd" chunk
        writer.Write(new[] { (byte)'M', (byte)'T', (byte)'h', (byte)'d' });
        WriteBigEndian32(writer, 6); // Header length is always 6
        WriteBigEndian16(writer, (short)midiEvents.MidiFileType); // Format type
        WriteBigEndian16(writer, (short)midiEvents.Tracks); // Number of tracks
        WriteBigEndian16(writer, (short)midiEvents.DeltaTicksPerQuarterNote); // Ticks per quarter note

        // Write each track
        for (int track = 0; track < midiEvents.Tracks; track++)
        {
            WriteTrack(writer, midiEvents.GetTrackEvents(track));
        }
    }

    /// <summary>
    /// Writes a single MIDI track to the stream.
    /// </summary>
    private static void WriteTrack(BinaryWriter writer, IList<MidiEvent> events)
    {
        // Write track header
        writer.Write(new[] { (byte)'M', (byte)'T', (byte)'r', (byte)'k' });

        // Calculate track data length and write it after we know the size
        using var trackStream = new MemoryStream();
        using var trackWriter = new BinaryWriter(trackStream);

        long previousAbsoluteTime = 0;

        foreach (var midiEvent in events.OrderBy(e => e.AbsoluteTime))
        {
            // Calculate delta time
            long deltaTime = midiEvent.AbsoluteTime - previousAbsoluteTime;
            previousAbsoluteTime = midiEvent.AbsoluteTime;

            // Write variable-length delta time
            WriteVariableLength(trackWriter, deltaTime);

            // Write event data
            WriteEvent(trackWriter, midiEvent);
        }

        // Get track data
        byte[] trackData = trackStream.ToArray();

        // Write track length and data
        WriteBigEndian32(writer, trackData.Length);
        writer.Write(trackData);
    }

    /// <summary>
    /// Writes a single MIDI event.
    /// </summary>
    private static void WriteEvent(BinaryWriter writer, MidiEvent midiEvent)
    {
        switch (midiEvent)
        {
            case TempoEvent tempo:
                writer.Write((byte)0xFF);
                writer.Write((byte)0x51);
                writer.Write((byte)0x03);
                int mpqn = tempo.MicrosecondsPerQuarterNote;
                writer.Write((byte)((mpqn >> 16) & 0xFF));
                writer.Write((byte)((mpqn >> 8) & 0xFF));
                writer.Write((byte)(mpqn & 0xFF));
                break;

            case TimeSignatureEvent timeSig:
                writer.Write((byte)0xFF);
                writer.Write((byte)0x58);
                writer.Write((byte)0x04);
                writer.Write((byte)timeSig.Numerator);
                writer.Write((byte)timeSig.Denominator);
                writer.Write((byte)timeSig.TicksInMetronomeClick);
                writer.Write((byte)timeSig.No32ndNotesInQuarterNote);
                break;

            case TextEvent textEvent:
                writer.Write((byte)0xFF);
                writer.Write((byte)textEvent.MetaEventType);
                byte[] textBytes = System.Text.Encoding.ASCII.GetBytes(textEvent.Text);
                WriteVariableLength(writer, textBytes.Length);
                writer.Write(textBytes);
                break;

            case MetaEvent meta when meta.MetaEventType == MetaEventType.EndTrack:
                writer.Write((byte)0xFF);
                writer.Write((byte)0x2F);
                writer.Write((byte)0x00);
                break;

            case NoteOnEvent noteOn:
                writer.Write((byte)(0x90 | ((noteOn.Channel - 1) & 0x0F)));
                writer.Write((byte)noteOn.NoteNumber);
                writer.Write((byte)noteOn.Velocity);
                break;

            default:
                // For other events, write raw data if available
                var rawData = midiEvent.GetAsShortMessage();
                writer.Write((byte)(rawData & 0xFF));
                writer.Write((byte)((rawData >> 8) & 0xFF));
                if (((rawData & 0xF0) != 0xC0) && ((rawData & 0xF0) != 0xD0))
                {
                    writer.Write((byte)((rawData >> 16) & 0xFF));
                }
                break;
        }
    }

    /// <summary>
    /// Writes a variable-length quantity (used for delta times in MIDI).
    /// </summary>
    private static void WriteVariableLength(BinaryWriter writer, long value)
    {
        if (value < 0)
        {
            value = 0;
        }

        // Build the variable length value from LSB to MSB
        var bytes = new List<byte>();
        bytes.Add((byte)(value & 0x7F));
        value >>= 7;

        while (value > 0)
        {
            bytes.Add((byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }

        // Write in reverse order (MSB first)
        for (int i = bytes.Count - 1; i >= 0; i--)
        {
            writer.Write(bytes[i]);
        }
    }

    /// <summary>
    /// Writes a 32-bit integer in big-endian format.
    /// </summary>
    private static void WriteBigEndian32(BinaryWriter writer, int value)
    {
        writer.Write((byte)((value >> 24) & 0xFF));
        writer.Write((byte)((value >> 16) & 0xFF));
        writer.Write((byte)((value >> 8) & 0xFF));
        writer.Write((byte)(value & 0xFF));
    }

    /// <summary>
    /// Writes a 16-bit integer in big-endian format.
    /// </summary>
    private static void WriteBigEndian16(BinaryWriter writer, short value)
    {
        writer.Write((byte)((value >> 8) & 0xFF));
        writer.Write((byte)(value & 0xFF));
    }
}
