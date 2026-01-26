// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MIDI handling component.

namespace MusicEngine.Core.Midi;

/// <summary>
/// Mode for handling overlapping notes.
/// </summary>
public enum OverlapMode
{
    /// <summary>Truncate earlier note to avoid overlap.</summary>
    TruncateEarlier,
    /// <summary>Truncate later note to avoid overlap.</summary>
    TruncateLater,
    /// <summary>Remove earlier note entirely.</summary>
    RemoveEarlier,
    /// <summary>Remove later note entirely.</summary>
    RemoveLater,
    /// <summary>Merge overlapping notes into one.</summary>
    Merge
}

/// <summary>
/// Mode for velocity smoothing.
/// </summary>
public enum VelocitySmoothMode
{
    /// <summary>Simple moving average.</summary>
    Average,
    /// <summary>Exponential moving average (more weight on recent values).</summary>
    Exponential,
    /// <summary>Gaussian weighted smoothing.</summary>
    Gaussian
}

/// <summary>
/// Options for MIDI polishing operations.
/// </summary>
public class MidiPolishOptions
{
    /// <summary>Fix overlapping notes of the same pitch.</summary>
    public bool FixOverlaps { get; set; } = true;

    /// <summary>Mode for handling overlapping notes.</summary>
    public OverlapMode OverlapMode { get; set; } = OverlapMode.TruncateEarlier;

    /// <summary>Remove duplicate notes at the same position.</summary>
    public bool RemoveDuplicates { get; set; } = true;

    /// <summary>Tolerance for duplicate detection in beats.</summary>
    public double DuplicateTolerance { get; set; } = 0.01;

    /// <summary>Apply quantization.</summary>
    public bool Quantize { get; set; } = false;

    /// <summary>Quantization grid in beats (e.g., 0.25 for 16th notes).</summary>
    public double QuantizeGrid { get; set; } = 0.25;

    /// <summary>Quantization strength (0-1). Lower values preserve humanization.</summary>
    public double QuantizeStrength { get; set; } = 0.5;

    /// <summary>Smooth velocity values.</summary>
    public bool SmoothVelocity { get; set; } = false;

    /// <summary>Velocity smoothing mode.</summary>
    public VelocitySmoothMode VelocitySmoothMode { get; set; } = VelocitySmoothMode.Average;

    /// <summary>Window size for velocity smoothing (number of notes).</summary>
    public int VelocitySmoothWindow { get; set; } = 3;

    /// <summary>Normalize note lengths.</summary>
    public bool NormalizeLength { get; set; } = false;

    /// <summary>Target note length in beats (0 = use grid).</summary>
    public double TargetLength { get; set; } = 0;

    /// <summary>Length normalization strength (0-1).</summary>
    public double LengthNormStrength { get; set; } = 0.5;

    /// <summary>Minimum velocity (notes below this are removed or boosted).</summary>
    public int MinVelocity { get; set; } = 1;

    /// <summary>Maximum velocity (notes above this are capped).</summary>
    public int MaxVelocity { get; set; } = 127;

    /// <summary>Boost very quiet notes instead of removing them.</summary>
    public bool BoostQuietNotes { get; set; } = true;

    /// <summary>Remove very short notes (below this duration in beats).</summary>
    public double MinDuration { get; set; } = 0.01;
}

/// <summary>
/// Result of MIDI polishing operation.
/// </summary>
public class MidiPolishResult
{
    /// <summary>Number of overlapping notes fixed.</summary>
    public int OverlapsFixed { get; set; }

    /// <summary>Number of duplicate notes removed.</summary>
    public int DuplicatesRemoved { get; set; }

    /// <summary>Number of notes quantized.</summary>
    public int NotesQuantized { get; set; }

    /// <summary>Number of velocities smoothed.</summary>
    public int VelocitiesSmoothed { get; set; }

    /// <summary>Number of lengths normalized.</summary>
    public int LengthsNormalized { get; set; }

    /// <summary>Number of notes removed (too short, too quiet, etc.).</summary>
    public int NotesRemoved { get; set; }

    /// <summary>Number of velocities clamped.</summary>
    public int VelocitiesClamped { get; set; }

    /// <summary>Total processing time in milliseconds.</summary>
    public double ProcessingTimeMs { get; set; }
}

/// <summary>
/// Intelligent MIDI cleanup and polish utility.
/// Fixes common MIDI issues like overlapping notes, duplicates, and inconsistent velocities.
/// </summary>
public class MidiPolisher
{
    private readonly Random _random;

    /// <summary>
    /// Creates a new MIDI polisher.
    /// </summary>
    public MidiPolisher()
    {
        _random = new Random();
    }

    /// <summary>
    /// Creates a new MIDI polisher with a specific random seed for reproducibility.
    /// </summary>
    /// <param name="seed">Random seed.</param>
    public MidiPolisher(int seed)
    {
        _random = new Random(seed);
    }

    /// <summary>
    /// Polish a pattern with default options.
    /// </summary>
    /// <param name="pattern">The pattern to polish.</param>
    /// <returns>A new polished pattern.</returns>
    public Pattern Polish(Pattern pattern)
    {
        return Polish(pattern, new MidiPolishOptions());
    }

    /// <summary>
    /// Polish a pattern with specified options.
    /// </summary>
    /// <param name="pattern">The pattern to polish.</param>
    /// <param name="options">Polish options.</param>
    /// <returns>A new polished pattern.</returns>
    public Pattern Polish(Pattern pattern, MidiPolishOptions options)
    {
        var result = PolishWithResult(pattern, options);
        return result.Pattern;
    }

    /// <summary>
    /// Polish a pattern and return detailed results.
    /// </summary>
    /// <param name="pattern">The pattern to polish.</param>
    /// <param name="options">Polish options.</param>
    /// <returns>Polished pattern and statistics.</returns>
    public (Pattern Pattern, MidiPolishResult Result) PolishWithResult(Pattern pattern, MidiPolishOptions options)
    {
        if (pattern == null)
            throw new ArgumentNullException(nameof(pattern));
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        var startTime = DateTime.Now;
        var result = new MidiPolishResult();

        // Create a copy of the events
        var events = pattern.Events.Select(e => new NoteEvent
        {
            Note = e.Note,
            Velocity = e.Velocity,
            Beat = e.Beat,
            Duration = e.Duration
        }).ToList();

        // Sort by beat position
        events = events.OrderBy(e => e.Beat).ThenBy(e => e.Note).ToList();

        // Apply operations in order
        if (options.RemoveDuplicates)
        {
            result.DuplicatesRemoved = RemoveDuplicates(events, options.DuplicateTolerance);
        }

        if (options.FixOverlaps)
        {
            result.OverlapsFixed = FixOverlaps(events, options.OverlapMode);
        }

        if (options.Quantize)
        {
            result.NotesQuantized = QuantizeNotes(events, options.QuantizeGrid, options.QuantizeStrength);
        }

        if (options.SmoothVelocity)
        {
            result.VelocitiesSmoothed = SmoothVelocities(events, options.VelocitySmoothMode, options.VelocitySmoothWindow);
        }

        if (options.NormalizeLength)
        {
            double targetLength = options.TargetLength > 0 ? options.TargetLength : options.QuantizeGrid;
            result.LengthsNormalized = NormalizeLengths(events, targetLength, options.LengthNormStrength);
        }

        // Clamp and filter velocities
        result.VelocitiesClamped = ClampVelocities(events, options.MinVelocity, options.MaxVelocity, options.BoostQuietNotes);

        // Remove short notes
        int removedCount = events.RemoveAll(e => e.Duration < options.MinDuration);
        result.NotesRemoved = removedCount;

        // Create new pattern
        var polishedPattern = new Pattern(pattern.Synth)
        {
            Name = pattern.Name + " (Polished)",
            LoopLength = pattern.LoopLength,
            IsLooping = pattern.IsLooping,
            Enabled = pattern.Enabled
        };
        polishedPattern.Events.AddRange(events);

        result.ProcessingTimeMs = (DateTime.Now - startTime).TotalMilliseconds;

        return (polishedPattern, result);
    }

    /// <summary>
    /// Polish a list of note events in place.
    /// </summary>
    /// <param name="events">The events to polish.</param>
    /// <param name="options">Polish options.</param>
    /// <returns>Polish statistics.</returns>
    public MidiPolishResult PolishInPlace(List<NoteEvent> events, MidiPolishOptions options)
    {
        if (events == null)
            throw new ArgumentNullException(nameof(events));
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        var startTime = DateTime.Now;
        var result = new MidiPolishResult();

        // Sort by beat position
        events.Sort((a, b) =>
        {
            int cmp = a.Beat.CompareTo(b.Beat);
            return cmp != 0 ? cmp : a.Note.CompareTo(b.Note);
        });

        if (options.RemoveDuplicates)
        {
            result.DuplicatesRemoved = RemoveDuplicates(events, options.DuplicateTolerance);
        }

        if (options.FixOverlaps)
        {
            result.OverlapsFixed = FixOverlaps(events, options.OverlapMode);
        }

        if (options.Quantize)
        {
            result.NotesQuantized = QuantizeNotes(events, options.QuantizeGrid, options.QuantizeStrength);
        }

        if (options.SmoothVelocity)
        {
            result.VelocitiesSmoothed = SmoothVelocities(events, options.VelocitySmoothMode, options.VelocitySmoothWindow);
        }

        if (options.NormalizeLength)
        {
            double targetLength = options.TargetLength > 0 ? options.TargetLength : options.QuantizeGrid;
            result.LengthsNormalized = NormalizeLengths(events, targetLength, options.LengthNormStrength);
        }

        result.VelocitiesClamped = ClampVelocities(events, options.MinVelocity, options.MaxVelocity, options.BoostQuietNotes);
        result.NotesRemoved = events.RemoveAll(e => e.Duration < options.MinDuration);

        result.ProcessingTimeMs = (DateTime.Now - startTime).TotalMilliseconds;

        return result;
    }

    private int RemoveDuplicates(List<NoteEvent> events, double tolerance)
    {
        int removed = 0;
        var toRemove = new HashSet<int>();

        for (int i = 0; i < events.Count; i++)
        {
            if (toRemove.Contains(i)) continue;

            for (int j = i + 1; j < events.Count; j++)
            {
                if (toRemove.Contains(j)) continue;

                if (events[j].Beat - events[i].Beat > tolerance) break;

                if (events[i].Note == events[j].Note &&
                    Math.Abs(events[i].Beat - events[j].Beat) <= tolerance)
                {
                    toRemove.Add(j);
                    removed++;
                }
            }
        }

        // Remove in reverse order to maintain indices
        foreach (int idx in toRemove.OrderByDescending(x => x))
        {
            events.RemoveAt(idx);
        }

        return removed;
    }

    private int FixOverlaps(List<NoteEvent> events, OverlapMode mode)
    {
        int fixed_count = 0;
        var toRemove = new HashSet<int>();

        // Group by pitch
        var byPitch = events.Select((e, i) => (Event: e, Index: i))
                           .GroupBy(x => x.Event.Note);

        foreach (var group in byPitch)
        {
            var pitchEvents = group.OrderBy(x => x.Event.Beat).ToList();

            for (int i = 0; i < pitchEvents.Count - 1; i++)
            {
                var current = pitchEvents[i];
                var next = pitchEvents[i + 1];

                double currentEnd = current.Event.Beat + current.Event.Duration;

                if (currentEnd > next.Event.Beat)
                {
                    // Overlap detected
                    fixed_count++;

                    switch (mode)
                    {
                        case OverlapMode.TruncateEarlier:
                            current.Event.Duration = next.Event.Beat - current.Event.Beat - 0.001;
                            if (current.Event.Duration < 0.01)
                            {
                                toRemove.Add(current.Index);
                            }
                            break;

                        case OverlapMode.TruncateLater:
                            double overlap = currentEnd - next.Event.Beat;
                            next.Event.Beat = currentEnd + 0.001;
                            next.Event.Duration -= overlap + 0.001;
                            if (next.Event.Duration < 0.01)
                            {
                                toRemove.Add(next.Index);
                            }
                            break;

                        case OverlapMode.RemoveEarlier:
                            toRemove.Add(current.Index);
                            break;

                        case OverlapMode.RemoveLater:
                            toRemove.Add(next.Index);
                            break;

                        case OverlapMode.Merge:
                            // Extend current to cover both, remove next
                            double newEnd = Math.Max(currentEnd, next.Event.Beat + next.Event.Duration);
                            current.Event.Duration = newEnd - current.Event.Beat;
                            current.Event.Velocity = Math.Max(current.Event.Velocity, next.Event.Velocity);
                            toRemove.Add(next.Index);
                            break;
                    }
                }
            }
        }

        foreach (int idx in toRemove.OrderByDescending(x => x))
        {
            events.RemoveAt(idx);
        }

        return fixed_count;
    }

    private int QuantizeNotes(List<NoteEvent> events, double grid, double strength)
    {
        int quantized = 0;

        foreach (var e in events)
        {
            double nearestGrid = Math.Round(e.Beat / grid) * grid;
            double diff = nearestGrid - e.Beat;

            if (Math.Abs(diff) > 0.001)
            {
                e.Beat += diff * strength;
                quantized++;
            }
        }

        return quantized;
    }

    private int SmoothVelocities(List<NoteEvent> events, VelocitySmoothMode mode, int window)
    {
        if (events.Count < 2 || window < 2) return 0;

        int smoothed = 0;
        var originalVelocities = events.Select(e => e.Velocity).ToArray();

        for (int i = 0; i < events.Count; i++)
        {
            int start = Math.Max(0, i - window / 2);
            int end = Math.Min(events.Count - 1, i + window / 2);
            int count = end - start + 1;

            double newVelocity;

            switch (mode)
            {
                case VelocitySmoothMode.Average:
                    newVelocity = 0;
                    for (int j = start; j <= end; j++)
                    {
                        newVelocity += originalVelocities[j];
                    }
                    newVelocity /= count;
                    break;

                case VelocitySmoothMode.Exponential:
                    double alpha = 2.0 / (count + 1);
                    newVelocity = originalVelocities[start];
                    for (int j = start + 1; j <= end; j++)
                    {
                        newVelocity = alpha * originalVelocities[j] + (1 - alpha) * newVelocity;
                    }
                    break;

                case VelocitySmoothMode.Gaussian:
                    double sigma = count / 4.0;
                    double weightSum = 0;
                    newVelocity = 0;
                    for (int j = start; j <= end; j++)
                    {
                        double dist = j - i;
                        double weight = Math.Exp(-(dist * dist) / (2 * sigma * sigma));
                        newVelocity += originalVelocities[j] * weight;
                        weightSum += weight;
                    }
                    newVelocity /= weightSum;
                    break;

                default:
                    newVelocity = originalVelocities[i];
                    break;
            }

            int newVel = Math.Clamp((int)Math.Round(newVelocity), 1, 127);
            if (newVel != events[i].Velocity)
            {
                events[i].Velocity = newVel;
                smoothed++;
            }
        }

        return smoothed;
    }

    private int NormalizeLengths(List<NoteEvent> events, double targetLength, double strength)
    {
        int normalized = 0;

        foreach (var e in events)
        {
            double diff = targetLength - e.Duration;
            if (Math.Abs(diff) > 0.001)
            {
                e.Duration += diff * strength;
                normalized++;
            }
        }

        return normalized;
    }

    private int ClampVelocities(List<NoteEvent> events, int min, int max, bool boostQuiet)
    {
        int clamped = 0;

        foreach (var e in events)
        {
            if (e.Velocity < min)
            {
                e.Velocity = boostQuiet ? min : 0;
                clamped++;
            }
            else if (e.Velocity > max)
            {
                e.Velocity = max;
                clamped++;
            }
        }

        // Remove notes with velocity 0
        events.RemoveAll(e => e.Velocity == 0);

        return clamped;
    }

    #region Static Helpers

    /// <summary>
    /// Quick fix for overlapping notes only.
    /// </summary>
    public static Pattern FixOverlaps(Pattern pattern, OverlapMode mode = OverlapMode.TruncateEarlier)
    {
        var polisher = new MidiPolisher();
        return polisher.Polish(pattern, new MidiPolishOptions
        {
            FixOverlaps = true,
            OverlapMode = mode,
            RemoveDuplicates = false,
            Quantize = false,
            SmoothVelocity = false,
            NormalizeLength = false
        });
    }

    /// <summary>
    /// Quick duplicate removal only.
    /// </summary>
    public static Pattern RemoveDuplicates(Pattern pattern, double tolerance = 0.01)
    {
        var polisher = new MidiPolisher();
        return polisher.Polish(pattern, new MidiPolishOptions
        {
            FixOverlaps = false,
            RemoveDuplicates = true,
            DuplicateTolerance = tolerance,
            Quantize = false,
            SmoothVelocity = false,
            NormalizeLength = false
        });
    }

    /// <summary>
    /// Quick quantize with humanization preservation.
    /// </summary>
    public static Pattern Quantize(Pattern pattern, double grid = 0.25, double strength = 0.5)
    {
        var polisher = new MidiPolisher();
        return polisher.Polish(pattern, new MidiPolishOptions
        {
            FixOverlaps = false,
            RemoveDuplicates = false,
            Quantize = true,
            QuantizeGrid = grid,
            QuantizeStrength = strength,
            SmoothVelocity = false,
            NormalizeLength = false
        });
    }

    #endregion
}
