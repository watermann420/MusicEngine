// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System.Collections.Generic;
using System.IO;
using System.Linq;


namespace MusicEngine.Core.Groove;


/// <summary>
/// Configuration options for groove extraction.
/// </summary>
public class GrooveExtractionOptions
{
    /// <summary>
    /// Quantization grid size in beats (e.g., 0.25 for 16th notes, 0.5 for 8th notes).
    /// </summary>
    public double QuantizeGrid { get; set; } = 0.25;

    /// <summary>
    /// Resolution in ticks per beat for the extracted groove.
    /// </summary>
    public int Resolution { get; set; } = 480;

    /// <summary>
    /// Length of the groove cycle to extract (in beats).
    /// Notes beyond this will wrap around.
    /// </summary>
    public double CycleLengthBeats { get; set; } = 1.0;

    /// <summary>
    /// Threshold for detecting swing (ratio difference from 0.5).
    /// </summary>
    public double SwingDetectionThreshold { get; set; } = 0.05;

    /// <summary>
    /// Whether to normalize velocity values relative to the average.
    /// </summary>
    public bool NormalizeVelocity { get; set; } = true;
}


/// <summary>
/// Extracts groove patterns from MIDI data or Pattern objects.
/// Analyzes timing deviations from a quantized grid and velocity curves.
/// </summary>
public static class GrooveExtractor
{
    /// <summary>
    /// Extracts a groove from an existing Pattern.
    /// Analyzes note positions vs. the quantized grid and calculates timing deviations.
    /// </summary>
    /// <param name="pattern">The source pattern to extract groove from.</param>
    /// <param name="options">Extraction options (null for defaults).</param>
    /// <returns>An ExtractedGroove containing timing and velocity data.</returns>
    public static ExtractedGroove ExtractFromPattern(Pattern pattern, GrooveExtractionOptions? options = null)
    {
        options ??= new GrooveExtractionOptions();

        var groove = new ExtractedGroove
        {
            Name = $"Groove from {pattern.Name}",
            Resolution = options.Resolution,
            CycleLengthBeats = options.CycleLengthBeats,
            SourceInfo = $"Extracted from pattern: {pattern.Name}",
            CreatedAt = DateTime.Now
        };

        if (pattern.Events.Count == 0)
            return groove;

        // Group notes by quantized position within the cycle
        var notesByPosition = GroupNotesByQuantizedPosition(pattern.Events, options);

        // Calculate timing deviations
        groove.TimingDeviations = CalculateTimingDeviations(notesByPosition, options);

        // Calculate velocity pattern
        groove.VelocityPattern = CalculateVelocityPattern(notesByPosition, options);

        // Calculate swing amount
        groove.SwingAmount = CalculateSwingAmount(pattern.Events, options);

        return groove;
    }

    /// <summary>
    /// Extracts a groove from a MIDI stream.
    /// </summary>
    /// <param name="midiStream">Stream containing MIDI data.</param>
    /// <param name="trackIndex">Index of the track to analyze (0-based).</param>
    /// <param name="channel">MIDI channel to filter (-1 for all channels).</param>
    /// <param name="options">Extraction options (null for defaults).</param>
    /// <returns>An ExtractedGroove containing timing and velocity data.</returns>
    public static ExtractedGroove ExtractFromMidi(Stream midiStream, int trackIndex = 0, int channel = -1, GrooveExtractionOptions? options = null)
    {
        options ??= new GrooveExtractionOptions();

        var midiData = MidiFile.Load(midiStream);
        return ExtractFromMidiData(midiData, trackIndex, channel, options);
    }

    /// <summary>
    /// Extracts a groove from a MIDI file path.
    /// </summary>
    /// <param name="midiPath">Path to the MIDI file.</param>
    /// <param name="trackIndex">Index of the track to analyze (0-based).</param>
    /// <param name="channel">MIDI channel to filter (-1 for all channels).</param>
    /// <param name="options">Extraction options (null for defaults).</param>
    /// <returns>An ExtractedGroove containing timing and velocity data.</returns>
    public static ExtractedGroove ExtractFromMidiFile(string midiPath, int trackIndex = 0, int channel = -1, GrooveExtractionOptions? options = null)
    {
        options ??= new GrooveExtractionOptions();

        var midiData = MidiFile.Load(midiPath);
        return ExtractFromMidiData(midiData, trackIndex, channel, options);
    }

    /// <summary>
    /// Extracts a groove from loaded MIDI data.
    /// </summary>
    private static ExtractedGroove ExtractFromMidiData(MidiFileData midiData, int trackIndex, int channel, GrooveExtractionOptions options)
    {
        var groove = new ExtractedGroove
        {
            Name = "Groove from MIDI",
            Resolution = midiData.TicksPerQuarterNote,
            CycleLengthBeats = options.CycleLengthBeats,
            SourceInfo = "Extracted from MIDI file",
            CreatedAt = DateTime.Now
        };

        if (trackIndex >= midiData.Tracks.Count)
            return groove;

        var track = midiData.Tracks[trackIndex];

        // Convert MIDI events to note data
        var notes = ExtractNotesFromTrack(track, midiData.TicksPerQuarterNote, channel);

        if (notes.Count == 0)
            return groove;

        // Group by quantized position
        var notesByPosition = GroupNotesByQuantizedPosition(notes, options);

        // Calculate timing deviations
        groove.TimingDeviations = CalculateTimingDeviations(notesByPosition, options);

        // Calculate velocity pattern
        groove.VelocityPattern = CalculateVelocityPattern(notesByPosition, options);

        // Calculate swing amount
        groove.SwingAmount = CalculateSwingAmount(notes, options);

        if (!string.IsNullOrEmpty(track.Name))
        {
            groove.Name = $"Groove from {track.Name}";
            groove.SourceInfo = $"Extracted from MIDI track: {track.Name}";
        }

        return groove;
    }

    /// <summary>
    /// Extracts note events from a MIDI track.
    /// </summary>
    private static List<NoteEvent> ExtractNotesFromTrack(MidiTrack track, int ppqn, int channel)
    {
        var notes = new List<NoteEvent>();
        var activeNotes = new Dictionary<int, (double startBeat, int velocity)>();

        foreach (var evt in track.Events)
        {
            if (channel >= 0 && evt.Channel != channel)
                continue;

            double beat = (double)evt.AbsoluteTime / ppqn;

            if (evt.EventType == MidiEventType.NoteOn && evt.Data2 > 0)
            {
                activeNotes[evt.Data1] = (beat, evt.Data2);
            }
            else if (evt.EventType == MidiEventType.NoteOff ||
                     (evt.EventType == MidiEventType.NoteOn && evt.Data2 == 0))
            {
                if (activeNotes.TryGetValue(evt.Data1, out var noteInfo))
                {
                    notes.Add(new NoteEvent
                    {
                        Beat = noteInfo.startBeat,
                        Note = evt.Data1,
                        Velocity = noteInfo.velocity,
                        Duration = beat - noteInfo.startBeat
                    });
                    activeNotes.Remove(evt.Data1);
                }
            }
        }

        return notes;
    }

    /// <summary>
    /// Groups notes by their quantized position within the cycle.
    /// </summary>
    private static Dictionary<double, List<(double actualPosition, int velocity)>> GroupNotesByQuantizedPosition(
        IEnumerable<NoteEvent> notes,
        GrooveExtractionOptions options)
    {
        var grouped = new Dictionary<double, List<(double actualPosition, int velocity)>>();

        foreach (var note in notes)
        {
            // Normalize to cycle
            double posInCycle = note.Beat % options.CycleLengthBeats;
            if (posInCycle < 0) posInCycle += options.CycleLengthBeats;

            // Find nearest quantized position
            double quantizedPos = Math.Round(posInCycle / options.QuantizeGrid) * options.QuantizeGrid;

            // Wrap if at end of cycle
            if (Math.Abs(quantizedPos - options.CycleLengthBeats) < 0.0001)
                quantizedPos = 0;

            if (!grouped.TryGetValue(quantizedPos, out var list))
            {
                list = [];
                grouped[quantizedPos] = list;
            }

            list.Add((posInCycle, note.Velocity));
        }

        return grouped;
    }

    /// <summary>
    /// Calculates timing deviations from grouped note data.
    /// </summary>
    private static List<TimingDeviation> CalculateTimingDeviations(
        Dictionary<double, List<(double actualPosition, int velocity)>> notesByPosition,
        GrooveExtractionOptions options)
    {
        var deviations = new List<TimingDeviation>();

        foreach (var kvp in notesByPosition.OrderBy(k => k.Key))
        {
            double quantizedPos = kvp.Key;
            var notesAtPosition = kvp.Value;

            // Calculate average deviation from quantized position
            double avgDeviation = notesAtPosition
                .Select(n => n.actualPosition - quantizedPos)
                .Average();

            // Convert to ticks
            double deviationInTicks = avgDeviation * options.Resolution;

            deviations.Add(new TimingDeviation
            {
                BeatPosition = quantizedPos,
                DeviationInTicks = deviationInTicks
            });
        }

        return deviations;
    }

    /// <summary>
    /// Calculates velocity pattern from grouped note data.
    /// </summary>
    private static List<VelocityPoint> CalculateVelocityPattern(
        Dictionary<double, List<(double actualPosition, int velocity)>> notesByPosition,
        GrooveExtractionOptions options)
    {
        var velocities = new List<VelocityPoint>();

        // Calculate overall average velocity for normalization
        double overallAvg = notesByPosition.Values
            .SelectMany(n => n)
            .Average(n => n.velocity);

        foreach (var kvp in notesByPosition.OrderBy(k => k.Key))
        {
            double position = kvp.Key;
            var notesAtPosition = kvp.Value;

            double avgVelocity = notesAtPosition.Average(n => n.velocity);

            // Calculate multiplier relative to average (if normalization enabled)
            double multiplier = options.NormalizeVelocity && overallAvg > 0
                ? avgVelocity / overallAvg
                : avgVelocity / 100.0; // Normalize to 0-1.27 range

            velocities.Add(new VelocityPoint
            {
                BeatPosition = position,
                VelocityMultiplier = multiplier
            });
        }

        return velocities;
    }

    /// <summary>
    /// Calculates the swing amount by analyzing 8th note pairs.
    /// Swing is the ratio of the duration of the first 8th note to the total
    /// duration of two 8th notes. 50% = straight, 67% = triplet swing.
    /// </summary>
    private static double CalculateSwingAmount(IEnumerable<NoteEvent> notes, GrooveExtractionOptions options)
    {
        var noteList = notes.ToList();
        if (noteList.Count < 2)
            return 50.0;

        // Group notes by their position relative to the beat
        // We look for notes on downbeats (0, 1, 2...) and upbeats (0.5, 1.5, 2.5...)
        var downbeatNotes = new List<double>();
        var upbeatNotes = new List<double>();

        foreach (var note in noteList)
        {
            double posInBeat = note.Beat % 1.0;

            // Downbeat region (close to 0)
            if (posInBeat < 0.25 || posInBeat >= 0.75)
            {
                double actualPos = posInBeat >= 0.75 ? posInBeat - 1.0 : posInBeat;
                downbeatNotes.Add(note.Beat - actualPos);
            }
            // Upbeat region (close to 0.5)
            else if (posInBeat >= 0.25 && posInBeat < 0.75)
            {
                upbeatNotes.Add(note.Beat);
            }
        }

        if (downbeatNotes.Count == 0 || upbeatNotes.Count == 0)
            return 50.0;

        // Calculate average position of upbeats relative to surrounding downbeats
        var swingRatios = new List<double>();

        foreach (var upbeat in upbeatNotes)
        {
            double upbeatBeat = Math.Floor(upbeat);
            double upbeatPos = upbeat % 1.0;

            // Find if there's a downbeat on this beat or the next
            bool hasDownbeatBefore = downbeatNotes.Any(d => Math.Abs(Math.Floor(d) - upbeatBeat) < 0.1);
            bool hasDownbeatAfter = downbeatNotes.Any(d => Math.Abs(Math.Floor(d) - (upbeatBeat + 1)) < 0.1);

            if (hasDownbeatBefore || hasDownbeatAfter)
            {
                // Swing ratio: how far into the beat the upbeat occurs
                // 0.5 = straight (50%), 0.67 = triplet swing (67%)
                swingRatios.Add(upbeatPos * 100);
            }
        }

        if (swingRatios.Count == 0)
            return 50.0;

        return swingRatios.Average();
    }

    /// <summary>
    /// Analyzes a pattern and returns detailed groove statistics.
    /// </summary>
    /// <param name="pattern">The pattern to analyze.</param>
    /// <param name="options">Extraction options.</param>
    /// <returns>A dictionary of groove statistics.</returns>
    public static Dictionary<string, double> AnalyzeGroove(Pattern pattern, GrooveExtractionOptions? options = null)
    {
        options ??= new GrooveExtractionOptions();

        var stats = new Dictionary<string, double>();

        if (pattern.Events.Count == 0)
        {
            stats["noteCount"] = 0;
            return stats;
        }

        var groove = ExtractFromPattern(pattern, options);

        stats["noteCount"] = pattern.Events.Count;
        stats["swingAmount"] = groove.SwingAmount;
        stats["avgTimingDeviation"] = groove.TimingDeviations.Count > 0
            ? groove.TimingDeviations.Average(d => Math.Abs(d.DeviationInTicks))
            : 0;
        stats["maxTimingDeviation"] = groove.TimingDeviations.Count > 0
            ? groove.TimingDeviations.Max(d => Math.Abs(d.DeviationInTicks))
            : 0;
        stats["avgVelocity"] = pattern.Events.Average(e => e.Velocity);
        stats["velocityRange"] = pattern.Events.Max(e => e.Velocity) - pattern.Events.Min(e => e.Velocity);
        stats["velocityStdDev"] = CalculateStdDev(pattern.Events.Select(e => (double)e.Velocity));

        return stats;
    }

    /// <summary>
    /// Calculates standard deviation of a sequence.
    /// </summary>
    private static double CalculateStdDev(IEnumerable<double> values)
    {
        var list = values.ToList();
        if (list.Count < 2) return 0;

        double avg = list.Average();
        double sumOfSquares = list.Sum(v => (v - avg) * (v - avg));
        return Math.Sqrt(sumOfSquares / (list.Count - 1));
    }
}
