//Engine License (MEL) – Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Pattern transformation utilities including scale-lock, humanization, and groove templates.


using System;
using System.Collections.Generic;
using System.Linq;


namespace MusicEngine.Core;


/// <summary>
/// Groove template presets for classic hardware timing characteristics.
/// </summary>
public enum GrooveTemplate
{
    /// <summary>No groove applied - straight timing.</summary>
    None,

    /// <summary>MPC60 groove - classic hip-hop swing feel with slight late timing on off-beats.</summary>
    MPC60,

    /// <summary>TR-808 groove - tight electronic feel with subtle push on certain beats.</summary>
    TR808,

    /// <summary>Standard shuffle - even swing on 8th notes.</summary>
    Shuffle,

    /// <summary>Hip-hop groove - laid-back feel with pronounced swing and lazy off-beats.</summary>
    HipHop
}


/// <summary>
/// Options for humanizing a pattern with controlled randomization.
/// </summary>
public class HumanizeOptions
{
    /// <summary>
    /// Amount of timing variation to apply (0-1).
    /// 0 = no variation, 1 = maximum variation (up to ~50ms at 120 BPM).
    /// Default: 0.1
    /// </summary>
    public double TimingVariation { get; set; } = 0.1;

    /// <summary>
    /// Amount of velocity variation to apply (0-1).
    /// 0 = no variation, 1 = maximum variation (+/- 40 velocity).
    /// Default: 0.15
    /// </summary>
    public double VelocityVariation { get; set; } = 0.15;

    /// <summary>
    /// Amount of note length/duration variation to apply (0-1).
    /// 0 = no variation, 1 = maximum variation (+/- 50% of original duration).
    /// Default: 0.1
    /// </summary>
    public double NoteLengthVariation { get; set; } = 0.1;

    /// <summary>
    /// Optional seed for the random number generator.
    /// When set, produces reproducible humanization results.
    /// Default: null (random seed)
    /// </summary>
    public int? Seed { get; set; } = null;

    /// <summary>
    /// Creates default humanization options with subtle variations.
    /// </summary>
    public static HumanizeOptions Default => new();

    /// <summary>
    /// Creates humanization options for tight, programmed feel with minimal variation.
    /// </summary>
    public static HumanizeOptions Tight => new()
    {
        TimingVariation = 0.02,
        VelocityVariation = 0.05,
        NoteLengthVariation = 0.02
    };

    /// <summary>
    /// Creates humanization options for loose, natural feel with more variation.
    /// </summary>
    public static HumanizeOptions Loose => new()
    {
        TimingVariation = 0.2,
        VelocityVariation = 0.25,
        NoteLengthVariation = 0.15
    };

    /// <summary>
    /// Creates humanization options for drunk/sloppy feel with extreme variation.
    /// </summary>
    public static HumanizeOptions Drunk => new()
    {
        TimingVariation = 0.4,
        VelocityVariation = 0.4,
        NoteLengthVariation = 0.3
    };
}


/// <summary>
/// Provides static methods for transforming musical patterns including
/// scale quantization (scale-lock), humanization, and groove templates.
/// </summary>
public static class PatternTransform
{
    #region Scale-Lock Methods

    /// <summary>
    /// Quantizes all notes in a pattern to the nearest notes in a specified scale.
    /// This is useful for ensuring all notes fit within a musical scale (scale-lock).
    /// </summary>
    /// <param name="pattern">The pattern to transform.</param>
    /// <param name="rootNote">The root note of the scale (e.g., "C4", "F#3").</param>
    /// <param name="scale">The scale type to quantize to.</param>
    /// <returns>A new pattern with all notes quantized to the scale.</returns>
    /// <exception cref="ArgumentNullException">Thrown when pattern or rootNote is null.</exception>
    /// <example>
    /// <code>
    /// var quantized = PatternTransform.QuantizeToScale(myPattern, "C4", ScaleType.PentatonicMinor);
    /// </code>
    /// </example>
    public static Pattern QuantizeToScale(Pattern pattern, string rootNote, ScaleType scale)
    {
        if (pattern == null)
            throw new ArgumentNullException(nameof(pattern));
        if (string.IsNullOrWhiteSpace(rootNote))
            throw new ArgumentNullException(nameof(rootNote));

        int root = Note.FromString(rootNote);
        return QuantizeToScaleInternal(pattern, root, scale);
    }

    /// <summary>
    /// Quantizes all notes in a pattern to the nearest notes in a specified scale.
    /// </summary>
    /// <param name="pattern">The pattern to transform.</param>
    /// <param name="rootNote">The root note as a NoteName enum.</param>
    /// <param name="octave">The octave of the root note.</param>
    /// <param name="scale">The scale type to quantize to.</param>
    /// <returns>A new pattern with all notes quantized to the scale.</returns>
    public static Pattern QuantizeToScale(Pattern pattern, NoteName rootNote, int octave, ScaleType scale)
    {
        if (pattern == null)
            throw new ArgumentNullException(nameof(pattern));

        int root = Note.FromName(rootNote, octave);
        return QuantizeToScaleInternal(pattern, root, scale);
    }

    /// <summary>
    /// Quantizes a single MIDI note to the nearest note in a specified scale.
    /// </summary>
    /// <param name="midiNote">The MIDI note number to quantize (0-127).</param>
    /// <param name="rootNote">The root note of the scale (e.g., "C4", "F#3").</param>
    /// <param name="scale">The scale type to quantize to.</param>
    /// <returns>The quantized MIDI note number.</returns>
    /// <exception cref="ArgumentNullException">Thrown when rootNote is null.</exception>
    /// <example>
    /// <code>
    /// int quantized = PatternTransform.QuantizeToScale(61, "C4", ScaleType.Major); // C#4 -> C4 or D4
    /// </code>
    /// </example>
    public static int QuantizeToScale(int midiNote, string rootNote, ScaleType scale)
    {
        if (string.IsNullOrWhiteSpace(rootNote))
            throw new ArgumentNullException(nameof(rootNote));

        int root = Note.FromString(rootNote);
        return Scale.Quantize(midiNote, root, scale);
    }

    /// <summary>
    /// Quantizes a single MIDI note to the nearest note in a specified scale.
    /// </summary>
    /// <param name="midiNote">The MIDI note number to quantize (0-127).</param>
    /// <param name="rootNote">The root note as a NoteName enum.</param>
    /// <param name="scale">The scale type to quantize to.</param>
    /// <returns>The quantized MIDI note number.</returns>
    public static int QuantizeToScale(int midiNote, NoteName rootNote, ScaleType scale)
    {
        int root = (int)rootNote;
        return Scale.Quantize(midiNote, root, scale);
    }

    /// <summary>
    /// Internal implementation for scale quantization.
    /// </summary>
    private static Pattern QuantizeToScaleInternal(Pattern pattern, int root, ScaleType scale)
    {
        var newPattern = ClonePattern(pattern);

        foreach (var ev in newPattern.Events)
        {
            ev.Note = Scale.Quantize(ev.Note, root, scale);
        }

        return newPattern;
    }

    #endregion

    #region Humanization Methods

    /// <summary>
    /// Applies humanization to a pattern by adding controlled randomization
    /// to timing, velocity, and note length.
    /// </summary>
    /// <param name="pattern">The pattern to humanize.</param>
    /// <param name="options">The humanization options controlling the amount of variation.</param>
    /// <returns>A new pattern with humanization applied.</returns>
    /// <exception cref="ArgumentNullException">Thrown when pattern or options is null.</exception>
    /// <example>
    /// <code>
    /// var humanized = PatternTransform.Humanize(myPattern, new HumanizeOptions
    /// {
    ///     TimingVariation = 0.1,
    ///     VelocityVariation = 0.2,
    ///     NoteLengthVariation = 0.1,
    ///     Seed = 42 // For reproducible results
    /// });
    /// </code>
    /// </example>
    public static Pattern Humanize(Pattern pattern, HumanizeOptions options)
    {
        if (pattern == null)
            throw new ArgumentNullException(nameof(pattern));
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        var random = options.Seed.HasValue
            ? new Random(options.Seed.Value)
            : new Random();

        var newPattern = ClonePattern(pattern);

        // Maximum variations (at 1.0 setting)
        const double maxTimingVariation = 0.05;     // ~50ms at 120 BPM (in beats)
        const int maxVelocityVariation = 40;        // +/- 40 velocity units
        const double maxLengthVariation = 0.5;      // +/- 50% of original duration

        foreach (var ev in newPattern.Events)
        {
            // Apply timing variation (Gaussian-like distribution using Box-Muller)
            if (options.TimingVariation > 0)
            {
                double timingOffset = GetGaussianRandom(random) * maxTimingVariation * options.TimingVariation;
                ev.Beat = Math.Max(0, ev.Beat + timingOffset);

                // Wrap around if beat exceeds loop length
                if (ev.Beat >= newPattern.LoopLength)
                {
                    ev.Beat = ev.Beat % newPattern.LoopLength;
                }
            }

            // Apply velocity variation
            if (options.VelocityVariation > 0)
            {
                int velocityOffset = (int)(GetGaussianRandom(random) * maxVelocityVariation * options.VelocityVariation);
                ev.Velocity = Math.Clamp(ev.Velocity + velocityOffset, 1, 127);
            }

            // Apply note length variation
            if (options.NoteLengthVariation > 0)
            {
                double lengthFactor = 1.0 + (GetGaussianRandom(random) * maxLengthVariation * options.NoteLengthVariation);
                ev.Duration = Math.Max(0.01, ev.Duration * lengthFactor);
            }
        }

        // Re-sort events by beat position after timing changes
        newPattern.Events = newPattern.Events.OrderBy(e => e.Beat).ToList();

        return newPattern;
    }

    /// <summary>
    /// Applies humanization with default options.
    /// </summary>
    /// <param name="pattern">The pattern to humanize.</param>
    /// <returns>A new pattern with default humanization applied.</returns>
    public static Pattern Humanize(Pattern pattern)
    {
        return Humanize(pattern, HumanizeOptions.Default);
    }

    /// <summary>
    /// Generates a Gaussian-distributed random number using the Box-Muller transform.
    /// Returns values typically between -3 and 3, with most values between -1 and 1.
    /// </summary>
    private static double GetGaussianRandom(Random random)
    {
        // Box-Muller transform for Gaussian distribution
        double u1 = 1.0 - random.NextDouble(); // Avoid log(0)
        double u2 = random.NextDouble();
        double standardNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);

        // Clamp to reasonable range
        return Math.Clamp(standardNormal, -3.0, 3.0) / 3.0;
    }

    #endregion

    #region Groove Template Methods

    /// <summary>
    /// Applies swing to a pattern by delaying off-beat notes.
    /// </summary>
    /// <param name="pattern">The pattern to apply swing to.</param>
    /// <param name="swingAmount">The amount of swing (0-1). 0 = straight, 0.5 = moderate swing, 1 = extreme triplet swing.</param>
    /// <returns>A new pattern with swing applied.</returns>
    /// <exception cref="ArgumentNullException">Thrown when pattern is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when swingAmount is not between 0 and 1.</exception>
    /// <example>
    /// <code>
    /// var swung = PatternTransform.ApplySwing(myPattern, 0.6); // Nice moderate swing
    /// </code>
    /// </example>
    public static Pattern ApplySwing(Pattern pattern, double swingAmount)
    {
        if (pattern == null)
            throw new ArgumentNullException(nameof(pattern));
        if (swingAmount < 0 || swingAmount > 1)
            throw new ArgumentOutOfRangeException(nameof(swingAmount), "Swing amount must be between 0 and 1.");

        var newPattern = ClonePattern(pattern);

        // Maximum swing delay (at 1.0) - delays off-beat 8th notes by up to 1/3 of an 8th note
        // This creates a triplet feel at maximum swing
        const double maxSwingDelay = 0.167; // ~1/6 of a beat (1/3 of an 8th note)

        foreach (var ev in newPattern.Events)
        {
            // Calculate position within a beat (0-1)
            double positionInBeat = ev.Beat % 1.0;

            // Check if this is an off-beat 8th note (around 0.5)
            // Allow some tolerance for notes that might be slightly off
            if (positionInBeat >= 0.4 && positionInBeat <= 0.6)
            {
                double swingDelay = maxSwingDelay * swingAmount;
                ev.Beat += swingDelay;
            }
            // Also apply to 16th note off-beats (around 0.25 and 0.75)
            else if ((positionInBeat >= 0.2 && positionInBeat <= 0.3) ||
                     (positionInBeat >= 0.7 && positionInBeat <= 0.8))
            {
                double swingDelay = maxSwingDelay * swingAmount * 0.5; // Half swing for 16ths
                ev.Beat += swingDelay;
            }
        }

        // Re-sort events by beat position
        newPattern.Events = newPattern.Events.OrderBy(e => e.Beat).ToList();

        return newPattern;
    }

    /// <summary>
    /// Applies a groove template to a pattern for classic hardware timing characteristics.
    /// </summary>
    /// <param name="pattern">The pattern to apply the groove to.</param>
    /// <param name="template">The groove template to apply.</param>
    /// <returns>A new pattern with the groove template applied.</returns>
    /// <exception cref="ArgumentNullException">Thrown when pattern is null.</exception>
    /// <example>
    /// <code>
    /// var grooved = PatternTransform.ApplyGroove(myPattern, GrooveTemplate.MPC60);
    /// </code>
    /// </example>
    public static Pattern ApplyGroove(Pattern pattern, GrooveTemplate template)
    {
        if (pattern == null)
            throw new ArgumentNullException(nameof(pattern));

        if (template == GrooveTemplate.None)
            return ClonePattern(pattern);

        var grooveData = GetGrooveData(template);
        var newPattern = ClonePattern(pattern);

        foreach (var ev in newPattern.Events)
        {
            // Get position within a beat (quantized to 16th notes: 0, 0.25, 0.5, 0.75)
            double positionInBeat = ev.Beat % 1.0;
            int sixteenthIndex = (int)Math.Round(positionInBeat * 4) % 4;

            // Apply timing offset from groove template
            double timingOffset = grooveData.TimingOffsets[sixteenthIndex];
            ev.Beat += timingOffset;

            // Ensure beat doesn't go negative
            if (ev.Beat < 0)
                ev.Beat += newPattern.LoopLength;

            // Apply velocity adjustment from groove template
            double velocityFactor = grooveData.VelocityFactors[sixteenthIndex];
            ev.Velocity = Math.Clamp((int)(ev.Velocity * velocityFactor), 1, 127);
        }

        // Re-sort events by beat position
        newPattern.Events = newPattern.Events.OrderBy(e => e.Beat).ToList();

        return newPattern;
    }

    /// <summary>
    /// Gets the groove data for a specific template.
    /// Timing offsets are in beats (1/16th note = 0.25 beats).
    /// </summary>
    private static GrooveData GetGrooveData(GrooveTemplate template)
    {
        return template switch
        {
            GrooveTemplate.MPC60 => new GrooveData
            {
                // MPC60 has a distinctive late feel on off-beats
                // Positions: downbeat, e, and, a (1, 2, 3, 4 of 16th notes)
                TimingOffsets = new[] { 0.0, 0.01, 0.04, 0.015 },
                VelocityFactors = new[] { 1.0, 0.85, 0.95, 0.80 }
            },

            GrooveTemplate.TR808 => new GrooveData
            {
                // TR-808 is tighter with subtle push on certain beats
                TimingOffsets = new[] { 0.0, -0.005, 0.02, 0.005 },
                VelocityFactors = new[] { 1.0, 0.90, 0.92, 0.88 }
            },

            GrooveTemplate.Shuffle => new GrooveData
            {
                // Standard shuffle - delays the "and" significantly
                TimingOffsets = new[] { 0.0, 0.0, 0.083, 0.0 },  // ~triplet feel on 8th notes
                VelocityFactors = new[] { 1.0, 0.85, 0.90, 0.85 }
            },

            GrooveTemplate.HipHop => new GrooveData
            {
                // Hip-hop groove - laid back with pronounced swing
                TimingOffsets = new[] { 0.0, 0.02, 0.06, 0.025 },
                VelocityFactors = new[] { 1.0, 0.75, 0.88, 0.72 }
            },

            _ => new GrooveData
            {
                TimingOffsets = new[] { 0.0, 0.0, 0.0, 0.0 },
                VelocityFactors = new[] { 1.0, 1.0, 1.0, 1.0 }
            }
        };
    }

    /// <summary>
    /// Internal class to hold groove template data.
    /// </summary>
    private class GrooveData
    {
        /// <summary>Timing offsets for each 16th note position (in beats).</summary>
        public double[] TimingOffsets { get; init; } = new double[4];

        /// <summary>Velocity multipliers for each 16th note position.</summary>
        public double[] VelocityFactors { get; init; } = new double[4];
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Creates a deep clone of a pattern with all events copied.
    /// </summary>
    /// <param name="pattern">The pattern to clone.</param>
    /// <returns>A new pattern that is a copy of the original.</returns>
    private static Pattern ClonePattern(Pattern pattern)
    {
        var newPattern = new Pattern(pattern.Synth)
        {
            LoopLength = pattern.LoopLength,
            IsLooping = pattern.IsLooping,
            StartBeat = pattern.StartBeat,
            Enabled = pattern.Enabled,
            Name = pattern.Name,
            InstrumentName = pattern.InstrumentName,
            SourceInfo = pattern.SourceInfo
        };

        // Deep clone events
        foreach (var ev in pattern.Events)
        {
            newPattern.Events.Add(new NoteEvent
            {
                Beat = ev.Beat,
                Note = ev.Note,
                Velocity = ev.Velocity,
                Duration = ev.Duration,
                SourceInfo = ev.SourceInfo,
                Parameters = ev.Parameters != null
                    ? new Dictionary<string, LiveParameter>(ev.Parameters)
                    : null
            });
        }

        return newPattern;
    }

    /// <summary>
    /// Combines multiple transformations into a single operation.
    /// </summary>
    /// <param name="pattern">The pattern to transform.</param>
    /// <param name="rootNote">The root note for scale quantization (null to skip).</param>
    /// <param name="scale">The scale type for quantization.</param>
    /// <param name="humanizeOptions">Humanization options (null to skip).</param>
    /// <param name="grooveTemplate">Groove template to apply.</param>
    /// <returns>A new pattern with all transformations applied.</returns>
    /// <example>
    /// <code>
    /// var transformed = PatternTransform.Transform(
    ///     myPattern,
    ///     "C4",
    ///     ScaleType.PentatonicMinor,
    ///     HumanizeOptions.Default,
    ///     GrooveTemplate.MPC60
    /// );
    /// </code>
    /// </example>
    public static Pattern Transform(
        Pattern pattern,
        string? rootNote,
        ScaleType scale,
        HumanizeOptions? humanizeOptions,
        GrooveTemplate grooveTemplate = GrooveTemplate.None)
    {
        if (pattern == null)
            throw new ArgumentNullException(nameof(pattern));

        Pattern result = pattern;

        // Apply scale quantization first
        if (!string.IsNullOrWhiteSpace(rootNote))
        {
            result = QuantizeToScale(result, rootNote, scale);
        }

        // Apply groove template
        if (grooveTemplate != GrooveTemplate.None)
        {
            result = ApplyGroove(result, grooveTemplate);
        }

        // Apply humanization last (adds random variation on top)
        if (humanizeOptions != null)
        {
            result = Humanize(result, humanizeOptions);
        }

        return result;
    }

    /// <summary>
    /// Transposes all notes in a pattern by a specified number of semitones.
    /// </summary>
    /// <param name="pattern">The pattern to transpose.</param>
    /// <param name="semitones">The number of semitones to transpose (positive = up, negative = down).</param>
    /// <returns>A new pattern with all notes transposed.</returns>
    public static Pattern Transpose(Pattern pattern, int semitones)
    {
        if (pattern == null)
            throw new ArgumentNullException(nameof(pattern));

        var newPattern = ClonePattern(pattern);

        foreach (var ev in newPattern.Events)
        {
            ev.Note = Math.Clamp(ev.Note + semitones, 0, 127);
        }

        return newPattern;
    }

    /// <summary>
    /// Reverses the order of notes in a pattern (retrograde).
    /// </summary>
    /// <param name="pattern">The pattern to reverse.</param>
    /// <returns>A new pattern with notes in reverse order.</returns>
    public static Pattern Reverse(Pattern pattern)
    {
        if (pattern == null)
            throw new ArgumentNullException(nameof(pattern));

        var newPattern = ClonePattern(pattern);

        foreach (var ev in newPattern.Events)
        {
            ev.Beat = pattern.LoopLength - ev.Beat - ev.Duration;
            if (ev.Beat < 0)
                ev.Beat = 0;
        }

        newPattern.Events = newPattern.Events.OrderBy(e => e.Beat).ToList();

        return newPattern;
    }

    /// <summary>
    /// Inverts the melodic contour of a pattern around a pivot note.
    /// </summary>
    /// <param name="pattern">The pattern to invert.</param>
    /// <param name="pivotNote">The MIDI note number to invert around (default: middle of note range).</param>
    /// <returns>A new pattern with inverted melodic contour.</returns>
    public static Pattern Invert(Pattern pattern, int? pivotNote = null)
    {
        if (pattern == null)
            throw new ArgumentNullException(nameof(pattern));

        var newPattern = ClonePattern(pattern);

        if (newPattern.Events.Count == 0)
            return newPattern;

        // Calculate pivot as middle of note range if not specified
        int pivot = pivotNote ?? (newPattern.Events.Min(e => e.Note) + newPattern.Events.Max(e => e.Note)) / 2;

        foreach (var ev in newPattern.Events)
        {
            int distance = ev.Note - pivot;
            ev.Note = Math.Clamp(pivot - distance, 0, 127);
        }

        return newPattern;
    }

    #endregion
}
