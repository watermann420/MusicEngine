// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Midi;
using MidiEvent = NAudio.Midi.MidiEvent;


namespace MusicEngine.Core;


/// <summary>
/// Represents the time signature data from a MIDI file import.
/// Contains additional MIDI-specific timing information.
/// </summary>
/// <param name="Numerator">The numerator (beats per bar).</param>
/// <param name="Denominator">The denominator (beat unit, e.g., 4 = quarter note).</param>
/// <param name="ClocksPerClick">MIDI clocks per metronome click.</param>
/// <param name="ThirtySecondNotesPerBeat">Number of 32nd notes per MIDI quarter note.</param>
public sealed record MidiTimeSignature(
    int Numerator = 4,
    int Denominator = 4,
    int ClocksPerClick = 24,
    int ThirtySecondNotesPerBeat = 8)
{
    /// <summary>
    /// Standard 4/4 time signature.
    /// </summary>
    public static MidiTimeSignature Common => new(4, 4, 24, 8);

    /// <summary>
    /// Gets the duration of one bar in beats.
    /// </summary>
    public double BeatsPerBar => Numerator * (4.0 / Denominator);

    /// <summary>
    /// Converts to the engine's TimeSignature struct.
    /// </summary>
    public TimeSignature ToTimeSignature() => new(Numerator, Denominator);

    /// <inheritdoc/>
    public override string ToString() => $"{Numerator}/{Denominator}";
}


/// <summary>
/// Contains data for a single imported MIDI track.
/// </summary>
/// <param name="Name">The name of the track (from Track Name meta event).</param>
/// <param name="Channel">The primary MIDI channel used by this track (0-15).</param>
/// <param name="Notes">The list of note events in this track.</param>
/// <param name="Program">The MIDI program/instrument number (0-127).</param>
public sealed record MidiTrackData(
    string Name,
    int Channel,
    List<NoteEvent> Notes,
    int Program)
{
    /// <summary>
    /// Gets the total duration of this track in beats.
    /// </summary>
    public double DurationBeats => Notes.Count > 0
        ? Notes.Max(n => n.Beat + n.Duration)
        : 0.0;

    /// <summary>
    /// Gets the number of notes in this track.
    /// </summary>
    public int NoteCount => Notes.Count;

    /// <summary>
    /// Gets the lowest note in this track.
    /// </summary>
    public int? LowestNote => Notes.Count > 0 ? Notes.Min(n => n.Note) : null;

    /// <summary>
    /// Gets the highest note in this track.
    /// </summary>
    public int? HighestNote => Notes.Count > 0 ? Notes.Max(n => n.Note) : null;
}


/// <summary>
/// Result of a MIDI file import operation.
/// </summary>
/// <param name="Success">Whether the import was successful.</param>
/// <param name="Tracks">The list of imported tracks.</param>
/// <param name="TicksPerQuarterNote">The PPQN (Pulses Per Quarter Note) resolution.</param>
/// <param name="Bpm">The tempo in beats per minute (from first tempo event).</param>
/// <param name="MidiTimeSignature">The time signature (from first time signature event).</param>
/// <param name="ErrorMessage">Error message if import failed, null otherwise.</param>
public sealed record MidiImportResult(
    bool Success,
    List<MidiTrackData> Tracks,
    int TicksPerQuarterNote,
    double Bpm,
    MidiTimeSignature MidiTimeSignature,
    string? ErrorMessage = null)
{
    /// <summary>
    /// Gets the total duration of all tracks in beats.
    /// </summary>
    public double DurationBeats => Tracks.Count > 0
        ? Tracks.Max(t => t.DurationBeats)
        : 0.0;

    /// <summary>
    /// Gets the total number of notes across all tracks.
    /// </summary>
    public int TotalNotes => Tracks.Sum(t => t.NoteCount);

    /// <summary>
    /// Gets the MIDI file type (0 = single track, 1 = multi-track synchronous).
    /// </summary>
    public int FileType => Tracks.Count <= 1 ? 0 : 1;

    /// <summary>
    /// Creates a failed import result with an error message.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    /// <returns>A failed MidiImportResult.</returns>
    public static MidiImportResult Failed(string errorMessage) =>
        new(false, [], 480, 120.0, MidiTimeSignature.Common, errorMessage);

    /// <summary>
    /// Creates a successful import result.
    /// </summary>
    /// <param name="tracks">The imported tracks.</param>
    /// <param name="ticksPerQuarterNote">The PPQN resolution.</param>
    /// <param name="bpm">The tempo in BPM.</param>
    /// <param name="timeSignature">The time signature.</param>
    /// <returns>A successful MidiImportResult.</returns>
    public static MidiImportResult Succeeded(
        List<MidiTrackData> tracks,
        int ticksPerQuarterNote,
        double bpm,
        MidiTimeSignature timeSignature) =>
        new(true, tracks, ticksPerQuarterNote, bpm, timeSignature);
}


/// <summary>
/// Provides functionality for importing Standard MIDI Files (SMF) into MusicEngine patterns and clips.
/// </summary>
/// <remarks>
/// <para>
/// This importer supports SMF Type 0 (single track) and Type 1 (multi-track synchronous) MIDI files.
/// It uses NAudio.Midi for parsing and converts the data to MusicEngine's Pattern and MidiClip formats.
/// </para>
/// <para>
/// Features include:
/// <list type="bullet">
/// <item><description>Automatic tempo detection from tempo meta events</description></item>
/// <item><description>Time signature extraction</description></item>
/// <item><description>Track name and instrument program detection</description></item>
/// <item><description>Proper tick-to-beat conversion based on PPQN</description></item>
/// <item><description>Asynchronous import support with cancellation</description></item>
/// </list>
/// </para>
/// <para>
/// Example usage:
/// <code>
/// var importer = new MidiImporter();
/// var result = importer.ImportFile("song.mid");
/// if (result.Success)
/// {
///     Console.WriteLine($"Imported {result.TotalNotes} notes at {result.Bpm} BPM");
/// }
/// </code>
/// </para>
/// </remarks>
public class MidiImporter
{
    /// <summary>
    /// Default tempo used when no tempo event is found in the MIDI file.
    /// </summary>
    public const double DefaultTempo = 120.0;

    /// <summary>
    /// Default PPQN (Pulses Per Quarter Note) resolution.
    /// </summary>
    public const int DefaultPpqn = 480;

    /// <summary>
    /// Gets or sets whether to merge all tracks into a single track for Type 0 compatibility.
    /// Default is false.
    /// </summary>
    public bool MergeAllTracks { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to skip empty tracks during import.
    /// Default is true.
    /// </summary>
    public bool SkipEmptyTracks { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum note velocity to include. Notes with lower velocity are ignored.
    /// Default is 1.
    /// </summary>
    public int MinimumVelocity { get; set; } = 1;

    /// <summary>
    /// Gets or sets whether to quantize note positions to a grid.
    /// Default is false.
    /// </summary>
    public bool QuantizeOnImport { get; set; } = false;

    /// <summary>
    /// Gets or sets the quantization grid size in beats when QuantizeOnImport is true.
    /// Default is 0.0625 (1/64 note).
    /// </summary>
    public double QuantizationGrid { get; set; } = 0.0625;

    /// <summary>
    /// Imports a MIDI file and returns detailed import results.
    /// </summary>
    /// <param name="filePath">The path to the MIDI file to import.</param>
    /// <returns>A MidiImportResult containing the imported data or error information.</returns>
    /// <exception cref="ArgumentException">Thrown when filePath is null or empty.</exception>
    /// <remarks>
    /// <para>
    /// This method reads the MIDI file, extracts all tracks, tempo information, and time signature,
    /// and converts the note events to MusicEngine's NoteEvent format.
    /// </para>
    /// <para>
    /// The method handles both Type 0 (single track) and Type 1 (multi-track) MIDI files.
    /// For Type 0 files, notes may be on different channels within the same track.
    /// </para>
    /// </remarks>
    public MidiImportResult ImportFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            return MidiImportResult.Failed($"File not found: {filePath}");
        }

        try
        {
            var midiFile = new NAudio.Midi.MidiFile(filePath, false);
            return ProcessMidiFile(midiFile);
        }
        catch (Exception ex)
        {
            return MidiImportResult.Failed($"Failed to import MIDI file: {ex.Message}");
        }
    }

    /// <summary>
    /// Imports a MIDI file asynchronously and returns detailed import results.
    /// </summary>
    /// <param name="filePath">The path to the MIDI file to import.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the import operation.</param>
    /// <returns>A task containing the MidiImportResult.</returns>
    /// <exception cref="ArgumentException">Thrown when filePath is null or empty.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    /// <remarks>
    /// This async version is useful for importing large MIDI files without blocking the UI thread.
    /// The cancellation token can be used to abort the import if needed.
    /// </remarks>
    public async Task<MidiImportResult> ImportFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            return MidiImportResult.Failed($"File not found: {filePath}");
        }

        try
        {
            // Read file asynchronously
            byte[] fileData = await File.ReadAllBytesAsync(filePath, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            // Parse MIDI file on thread pool to avoid blocking
            return await Task.Run(() =>
            {
                using var memoryStream = new MemoryStream(fileData);
                var midiFile = new NAudio.Midi.MidiFile(memoryStream, false);
                return ProcessMidiFile(midiFile);
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return MidiImportResult.Failed($"Failed to import MIDI file: {ex.Message}");
        }
    }

    /// <summary>
    /// Imports a MIDI file and converts it directly to a list of Pattern objects.
    /// </summary>
    /// <param name="filePath">The path to the MIDI file to import.</param>
    /// <returns>A list of Pattern objects, one for each track with notes.</returns>
    /// <exception cref="ArgumentException">Thrown when filePath is null or empty.</exception>
    /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
    /// <exception cref="InvalidDataException">Thrown when the file is not a valid MIDI file.</exception>
    /// <remarks>
    /// <para>
    /// This method creates Pattern objects without synthesizers attached. You must set the
    /// Synth property on each pattern before playback.
    /// </para>
    /// <para>
    /// Each track in the MIDI file becomes a separate Pattern. Empty tracks are skipped
    /// unless SkipEmptyTracks is set to false.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var importer = new MidiImporter();
    /// var patterns = importer.ImportToPatterns("song.mid");
    /// foreach (var pattern in patterns)
    /// {
    ///     pattern.Synth = mySynth;
    ///     sequencer.AddPattern(pattern);
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public List<Pattern> ImportToPatterns(string filePath)
    {
        var result = ImportFile(filePath);
        if (!result.Success)
        {
            throw new InvalidDataException(result.ErrorMessage ?? "Failed to import MIDI file.");
        }

        return ConvertToPatterns(result);
    }

    /// <summary>
    /// Imports a MIDI file and converts it to a list of Pattern objects using the specified synth factory.
    /// </summary>
    /// <param name="filePath">The path to the MIDI file to import.</param>
    /// <param name="synthFactory">A factory function that creates a synth for each track index.</param>
    /// <returns>A list of Pattern objects with synthesizers assigned.</returns>
    /// <exception cref="ArgumentException">Thrown when filePath is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when synthFactory is null.</exception>
    /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
    /// <exception cref="InvalidDataException">Thrown when the file is not a valid MIDI file.</exception>
    /// <remarks>
    /// <para>
    /// The synthFactory is called for each track with the track index (0-based) as parameter.
    /// This allows you to create different synths for different tracks.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var patterns = importer.ImportToPatterns("song.mid", trackIndex => new PolySynth());
    /// </code>
    /// </para>
    /// </remarks>
    public List<Pattern> ImportToPatterns(string filePath, Func<int, ISynth> synthFactory)
    {
        if (synthFactory == null)
        {
            throw new ArgumentNullException(nameof(synthFactory));
        }

        var result = ImportFile(filePath);
        if (!result.Success)
        {
            throw new InvalidDataException(result.ErrorMessage ?? "Failed to import MIDI file.");
        }

        return ConvertToPatterns(result, synthFactory);
    }

    /// <summary>
    /// Imports a MIDI file and converts it directly to a list of MidiClip objects.
    /// </summary>
    /// <param name="filePath">The path to the MIDI file to import.</param>
    /// <returns>A list of MidiClip objects, one for each track with notes.</returns>
    /// <exception cref="ArgumentException">Thrown when filePath is null or empty.</exception>
    /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
    /// <exception cref="InvalidDataException">Thrown when the file is not a valid MIDI file.</exception>
    /// <remarks>
    /// <para>
    /// MidiClip objects are suitable for arrangement timeline use. Each clip contains
    /// the notes from one MIDI track and can be placed at any position in the timeline.
    /// </para>
    /// <para>
    /// The clips are created with StartPosition = 0 and Length = track duration.
    /// TrackIndex corresponds to the original MIDI track index.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var importer = new MidiImporter();
    /// var clips = importer.ImportToMidiClips("song.mid");
    /// foreach (var clip in clips)
    /// {
    ///     arrangement.AddClip(clip);
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public List<MidiClip> ImportToMidiClips(string filePath)
    {
        var result = ImportFile(filePath);
        if (!result.Success)
        {
            throw new InvalidDataException(result.ErrorMessage ?? "Failed to import MIDI file.");
        }

        return ConvertToMidiClips(result);
    }

    /// <summary>
    /// Imports a MIDI file from a stream.
    /// </summary>
    /// <param name="stream">The stream containing the MIDI file data.</param>
    /// <returns>A MidiImportResult containing the imported data or error information.</returns>
    /// <exception cref="ArgumentNullException">Thrown when stream is null.</exception>
    public MidiImportResult ImportFromStream(Stream stream)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        try
        {
            var midiFile = new NAudio.Midi.MidiFile(stream, false);
            return ProcessMidiFile(midiFile);
        }
        catch (Exception ex)
        {
            return MidiImportResult.Failed($"Failed to import MIDI from stream: {ex.Message}");
        }
    }

    /// <summary>
    /// Processes a NAudio MidiFile and extracts all track data.
    /// </summary>
    /// <param name="midiFile">The NAudio MidiFile to process.</param>
    /// <returns>A MidiImportResult with the extracted data.</returns>
    private MidiImportResult ProcessMidiFile(NAudio.Midi.MidiFile midiFile)
    {
        int ppqn = midiFile.DeltaTicksPerQuarterNote;
        double bpm = DefaultTempo;
        var timeSignature = MidiTimeSignature.Common;
        var tracks = new List<MidiTrackData>();

        // First pass: extract tempo and time signature from all tracks
        for (int trackIndex = 0; trackIndex < midiFile.Tracks; trackIndex++)
        {
            var events = midiFile.Events[trackIndex];
            foreach (var midiEvent in events)
            {
                if (midiEvent is TempoEvent tempoEvent)
                {
                    // MicrosecondsPerQuarterNote to BPM
                    bpm = 60_000_000.0 / tempoEvent.MicrosecondsPerQuarterNote;
                }
                else if (midiEvent is TimeSignatureEvent timeSigEvent)
                {
                    timeSignature = new MidiTimeSignature(
                        timeSigEvent.Numerator,
                        (int)Math.Pow(2, timeSigEvent.Denominator),
                        timeSigEvent.TicksInMetronomeClick,
                        timeSigEvent.No32ndNotesInQuarterNote);
                }
            }
        }

        // Second pass: extract notes from each track
        if (MergeAllTracks)
        {
            // Merge all tracks into one
            var mergedTrack = ExtractMergedTrack(midiFile, ppqn);
            if (mergedTrack.Notes.Count > 0 || !SkipEmptyTracks)
            {
                tracks.Add(mergedTrack);
            }
        }
        else
        {
            // Extract each track separately
            for (int trackIndex = 0; trackIndex < midiFile.Tracks; trackIndex++)
            {
                var trackData = ExtractTrackData(midiFile.Events[trackIndex], trackIndex, ppqn);
                if (trackData.Notes.Count > 0 || !SkipEmptyTracks)
                {
                    tracks.Add(trackData);
                }
            }
        }

        return MidiImportResult.Succeeded(tracks, ppqn, bpm, timeSignature);
    }

    /// <summary>
    /// Extracts note data from a single MIDI track.
    /// </summary>
    /// <param name="events">The list of MIDI events in the track.</param>
    /// <param name="trackIndex">The index of the track.</param>
    /// <param name="ppqn">The PPQN resolution of the file.</param>
    /// <returns>A MidiTrackData record with the extracted notes.</returns>
    private MidiTrackData ExtractTrackData(IList<MidiEvent> events, int trackIndex, int ppqn)
    {
        string trackName = $"Track {trackIndex + 1}";
        int channel = 0;
        int program = 0;
        var notes = new List<NoteEvent>();
        var activeNotes = new Dictionary<(int channel, int note), (long startTicks, int velocity)>();

        foreach (var midiEvent in events)
        {
            // Extract track name
            if (midiEvent is TextEvent textEvent &&
                textEvent.MetaEventType == MetaEventType.SequenceTrackName)
            {
                trackName = textEvent.Text;
            }
            // Extract program change (instrument)
            else if (midiEvent is PatchChangeEvent patchEvent)
            {
                program = patchEvent.Patch;
                channel = patchEvent.Channel - 1; // NAudio uses 1-based channels
            }
            // Process note on
            else if (midiEvent is NoteOnEvent noteOn)
            {
                int noteChannel = noteOn.Channel - 1;
                if (channel == 0)
                {
                    channel = noteChannel; // Use first channel encountered
                }

                if (noteOn.Velocity > 0 && noteOn.Velocity >= MinimumVelocity)
                {
                    // Note on
                    var key = (noteChannel, noteOn.NoteNumber);
                    activeNotes[key] = (noteOn.AbsoluteTime, noteOn.Velocity);
                }
                else
                {
                    // Note off (velocity 0)
                    var key = (noteChannel, noteOn.NoteNumber);
                    if (activeNotes.TryGetValue(key, out var noteInfo))
                    {
                        var noteEvent = CreateNoteEvent(
                            noteOn.NoteNumber,
                            noteInfo.startTicks,
                            noteOn.AbsoluteTime,
                            noteInfo.velocity,
                            ppqn);
                        notes.Add(noteEvent);
                        activeNotes.Remove(key);
                    }
                }
            }
            // Process explicit note off (NAudio.Midi.NoteEvent base class)
            else if (midiEvent is NAudio.Midi.NoteEvent noteOff && !(midiEvent is NoteOnEvent))
            {
                int noteChannel = noteOff.Channel - 1;
                var key = (noteChannel, noteOff.NoteNumber);
                if (activeNotes.TryGetValue(key, out var noteInfo))
                {
                    var noteEvent = CreateNoteEvent(
                        noteOff.NoteNumber,
                        noteInfo.startTicks,
                        noteOff.AbsoluteTime,
                        noteInfo.velocity,
                        ppqn);
                    notes.Add(noteEvent);
                    activeNotes.Remove(key);
                }
            }
        }

        // Sort notes by beat position
        notes = notes.OrderBy(n => n.Beat).ThenBy(n => n.Note).ToList();

        return new MidiTrackData(trackName, channel, notes, program);
    }

    /// <summary>
    /// Extracts and merges all tracks into a single track.
    /// </summary>
    /// <param name="midiFile">The MIDI file to process.</param>
    /// <param name="ppqn">The PPQN resolution.</param>
    /// <returns>A merged MidiTrackData with all notes from all tracks.</returns>
    private MidiTrackData ExtractMergedTrack(NAudio.Midi.MidiFile midiFile, int ppqn)
    {
        string trackName = "Merged Track";
        var allNotes = new List<NoteEvent>();

        for (int trackIndex = 0; trackIndex < midiFile.Tracks; trackIndex++)
        {
            var trackData = ExtractTrackData(midiFile.Events[trackIndex], trackIndex, ppqn);

            // Use first non-default track name
            if (trackIndex == 0 || (!trackName.StartsWith("Track ") && !string.IsNullOrEmpty(trackData.Name)))
            {
                trackName = trackData.Name;
            }

            allNotes.AddRange(trackData.Notes);
        }

        // Sort all notes by beat
        allNotes = allNotes.OrderBy(n => n.Beat).ThenBy(n => n.Note).ToList();

        return new MidiTrackData(trackName, 0, allNotes, 0);
    }

    /// <summary>
    /// Creates a NoteEvent from MIDI tick data.
    /// </summary>
    /// <param name="noteNumber">The MIDI note number (0-127).</param>
    /// <param name="startTicks">The start time in ticks.</param>
    /// <param name="endTicks">The end time in ticks.</param>
    /// <param name="velocity">The note velocity (1-127).</param>
    /// <param name="ppqn">The PPQN resolution.</param>
    /// <returns>A new NoteEvent.</returns>
    private NoteEvent CreateNoteEvent(int noteNumber, long startTicks, long endTicks, int velocity, int ppqn)
    {
        double beat = TicksToBeats(startTicks, ppqn);
        double duration = TicksToBeats(endTicks - startTicks, ppqn);

        if (QuantizeOnImport && QuantizationGrid > 0)
        {
            beat = Math.Round(beat / QuantizationGrid) * QuantizationGrid;
            duration = Math.Max(QuantizationGrid, Math.Round(duration / QuantizationGrid) * QuantizationGrid);
        }

        return new NoteEvent
        {
            Note = noteNumber,
            Beat = beat,
            Duration = Math.Max(0.001, duration), // Ensure minimum duration
            Velocity = Math.Clamp(velocity, 1, 127)
        };
    }

    /// <summary>
    /// Converts MIDI ticks to beats.
    /// </summary>
    /// <param name="ticks">The tick value.</param>
    /// <param name="ppqn">The PPQN resolution.</param>
    /// <returns>The beat value.</returns>
    private static double TicksToBeats(long ticks, int ppqn)
    {
        return (double)ticks / ppqn;
    }

    /// <summary>
    /// Converts a MidiImportResult to a list of Pattern objects.
    /// </summary>
    /// <param name="result">The import result to convert.</param>
    /// <returns>A list of Pattern objects.</returns>
    private List<Pattern> ConvertToPatterns(MidiImportResult result)
    {
        var patterns = new List<Pattern>();

        for (int i = 0; i < result.Tracks.Count; i++)
        {
            var trackData = result.Tracks[i];
            var pattern = CreatePatternFromTrackData(trackData, result);
            patterns.Add(pattern);
        }

        return patterns;
    }

    /// <summary>
    /// Converts a MidiImportResult to a list of Pattern objects with synthesizers.
    /// </summary>
    /// <param name="result">The import result to convert.</param>
    /// <param name="synthFactory">A factory function to create synths.</param>
    /// <returns>A list of Pattern objects with synthesizers assigned.</returns>
    private List<Pattern> ConvertToPatterns(MidiImportResult result, Func<int, ISynth> synthFactory)
    {
        var patterns = new List<Pattern>();

        for (int i = 0; i < result.Tracks.Count; i++)
        {
            var trackData = result.Tracks[i];
            var synth = synthFactory(i);
            var pattern = CreatePatternFromTrackData(trackData, result, synth);
            patterns.Add(pattern);
        }

        return patterns;
    }

    /// <summary>
    /// Creates a Pattern from MidiTrackData.
    /// </summary>
    /// <param name="trackData">The track data to convert.</param>
    /// <param name="result">The full import result for metadata.</param>
    /// <param name="synth">Optional synthesizer to assign to the pattern.</param>
    /// <returns>A new Pattern object.</returns>
    private Pattern CreatePatternFromTrackData(MidiTrackData trackData, MidiImportResult result, ISynth? synth = null)
    {
        // Create pattern with a null synth if none provided
        // The caller must assign a synth before playback
        var pattern = new Pattern(synth!)
        {
            Name = trackData.Name,
            InstrumentName = GetInstrumentName(trackData.Program),
            LoopLength = Math.Max(result.MidiTimeSignature.BeatsPerBar, trackData.DurationBeats),
            IsLooping = false // Imported patterns typically don't loop
        };

        // Add all notes
        foreach (var note in trackData.Notes)
        {
            pattern.Events.Add(new NoteEvent
            {
                Note = note.Note,
                Beat = note.Beat,
                Duration = note.Duration,
                Velocity = note.Velocity
            });
        }

        return pattern;
    }

    /// <summary>
    /// Converts a MidiImportResult to a list of MidiClip objects.
    /// </summary>
    /// <param name="result">The import result to convert.</param>
    /// <returns>A list of MidiClip objects.</returns>
    private List<MidiClip> ConvertToMidiClips(MidiImportResult result)
    {
        var clips = new List<MidiClip>();

        for (int i = 0; i < result.Tracks.Count; i++)
        {
            var trackData = result.Tracks[i];
            var clip = CreateMidiClipFromTrackData(trackData, i);
            clips.Add(clip);
        }

        return clips;
    }

    /// <summary>
    /// Creates a MidiClip from MidiTrackData.
    /// </summary>
    /// <param name="trackData">The track data to convert.</param>
    /// <param name="trackIndex">The original track index.</param>
    /// <returns>A new MidiClip object.</returns>
    private MidiClip CreateMidiClipFromTrackData(MidiTrackData trackData, int trackIndex)
    {
        var clip = new MidiClip
        {
            Name = trackData.Name,
            StartPosition = 0,
            Length = Math.Max(1.0, trackData.DurationBeats),
            TrackIndex = trackIndex,
            MidiChannel = trackData.Channel,
            IsLooping = false
        };

        // Copy all notes
        foreach (var note in trackData.Notes)
        {
            clip.Notes.Add(new NoteEvent
            {
                Note = note.Note,
                Beat = note.Beat,
                Duration = note.Duration,
                Velocity = note.Velocity
            });
        }

        return clip;
    }

    /// <summary>
    /// Gets the General MIDI instrument name for a program number.
    /// </summary>
    /// <param name="program">The MIDI program number (0-127).</param>
    /// <returns>The instrument name.</returns>
    private static string GetInstrumentName(int program)
    {
        // General MIDI instrument names
        string[] instruments =
        [
            // Piano (0-7)
            "Acoustic Grand Piano", "Bright Acoustic Piano", "Electric Grand Piano",
            "Honky-tonk Piano", "Electric Piano 1", "Electric Piano 2", "Harpsichord", "Clavinet",
            // Chromatic Percussion (8-15)
            "Celesta", "Glockenspiel", "Music Box", "Vibraphone",
            "Marimba", "Xylophone", "Tubular Bells", "Dulcimer",
            // Organ (16-23)
            "Drawbar Organ", "Percussive Organ", "Rock Organ", "Church Organ",
            "Reed Organ", "Accordion", "Harmonica", "Tango Accordion",
            // Guitar (24-31)
            "Acoustic Guitar (nylon)", "Acoustic Guitar (steel)", "Electric Guitar (jazz)",
            "Electric Guitar (clean)", "Electric Guitar (muted)", "Overdriven Guitar",
            "Distortion Guitar", "Guitar Harmonics",
            // Bass (32-39)
            "Acoustic Bass", "Electric Bass (finger)", "Electric Bass (pick)", "Fretless Bass",
            "Slap Bass 1", "Slap Bass 2", "Synth Bass 1", "Synth Bass 2",
            // Strings (40-47)
            "Violin", "Viola", "Cello", "Contrabass",
            "Tremolo Strings", "Pizzicato Strings", "Orchestral Harp", "Timpani",
            // Ensemble (48-55)
            "String Ensemble 1", "String Ensemble 2", "Synth Strings 1", "Synth Strings 2",
            "Choir Aahs", "Voice Oohs", "Synth Voice", "Orchestra Hit",
            // Brass (56-63)
            "Trumpet", "Trombone", "Tuba", "Muted Trumpet",
            "French Horn", "Brass Section", "Synth Brass 1", "Synth Brass 2",
            // Reed (64-71)
            "Soprano Sax", "Alto Sax", "Tenor Sax", "Baritone Sax",
            "Oboe", "English Horn", "Bassoon", "Clarinet",
            // Pipe (72-79)
            "Piccolo", "Flute", "Recorder", "Pan Flute",
            "Blown Bottle", "Shakuhachi", "Whistle", "Ocarina",
            // Synth Lead (80-87)
            "Lead 1 (square)", "Lead 2 (sawtooth)", "Lead 3 (calliope)", "Lead 4 (chiff)",
            "Lead 5 (charang)", "Lead 6 (voice)", "Lead 7 (fifths)", "Lead 8 (bass + lead)",
            // Synth Pad (88-95)
            "Pad 1 (new age)", "Pad 2 (warm)", "Pad 3 (polysynth)", "Pad 4 (choir)",
            "Pad 5 (bowed)", "Pad 6 (metallic)", "Pad 7 (halo)", "Pad 8 (sweep)",
            // Synth Effects (96-103)
            "FX 1 (rain)", "FX 2 (soundtrack)", "FX 3 (crystal)", "FX 4 (atmosphere)",
            "FX 5 (brightness)", "FX 6 (goblins)", "FX 7 (echoes)", "FX 8 (sci-fi)",
            // Ethnic (104-111)
            "Sitar", "Banjo", "Shamisen", "Koto",
            "Kalimba", "Bagpipe", "Fiddle", "Shanai",
            // Percussive (112-119)
            "Tinkle Bell", "Agogo", "Steel Drums", "Woodblock",
            "Taiko Drum", "Melodic Tom", "Synth Drum", "Reverse Cymbal",
            // Sound Effects (120-127)
            "Guitar Fret Noise", "Breath Noise", "Seashore", "Bird Tweet",
            "Telephone Ring", "Helicopter", "Applause", "Gunshot"
        ];

        if (program >= 0 && program < instruments.Length)
        {
            return instruments[program];
        }

        return $"Program {program}";
    }
}
