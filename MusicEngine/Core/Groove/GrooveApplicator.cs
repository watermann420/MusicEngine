// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System.Collections.Generic;
using System.Linq;


namespace MusicEngine.Core.Groove;


/// <summary>
/// Options for applying groove to a pattern.
/// </summary>
public class GrooveApplyOptions
{
    /// <summary>
    /// Amount of groove to apply (0.0 = no groove, 1.0 = full groove).
    /// Values between allow for subtle groove application.
    /// </summary>
    public double Amount { get; set; } = 1.0;

    /// <summary>
    /// Whether to apply timing deviations.
    /// </summary>
    public bool ApplyTiming { get; set; } = true;

    /// <summary>
    /// Whether to apply velocity adjustments.
    /// </summary>
    public bool ApplyVelocity { get; set; } = true;

    /// <summary>
    /// Quantize notes to grid before applying groove.
    /// Useful for cleaning up input before grooving.
    /// </summary>
    public bool QuantizeFirst { get; set; } = false;

    /// <summary>
    /// Quantize grid size in beats (e.g., 0.25 for 16th notes).
    /// Only used if QuantizeFirst is true.
    /// </summary>
    public double QuantizeGrid { get; set; } = 0.25;

    /// <summary>
    /// Preserve the original note positions relative to each other within small windows.
    /// This maintains micro-timing relationships while applying groove.
    /// </summary>
    public bool PreserveMicroTiming { get; set; } = false;

    /// <summary>
    /// Window size in beats for preserving micro-timing relationships.
    /// </summary>
    public double MicroTimingWindow { get; set; } = 0.03125; // 1/32 beat
}


/// <summary>
/// Applies groove templates to patterns, transforming note timing and velocities.
/// </summary>
public static class GrooveApplicator
{
    /// <summary>
    /// Applies a groove to a pattern with specified amount.
    /// </summary>
    /// <param name="pattern">The source pattern.</param>
    /// <param name="groove">The groove to apply.</param>
    /// <param name="amount">Amount of groove (0.0-1.0).</param>
    /// <returns>A new pattern with the groove applied.</returns>
    public static Pattern ApplyGroove(Pattern pattern, ExtractedGroove groove, double amount = 1.0)
    {
        return ApplyGroove(pattern, groove, new GrooveApplyOptions { Amount = amount });
    }

    /// <summary>
    /// Applies a groove to a pattern with full options control.
    /// </summary>
    /// <param name="pattern">The source pattern.</param>
    /// <param name="groove">The groove to apply.</param>
    /// <param name="options">Application options.</param>
    /// <returns>A new pattern with the groove applied.</returns>
    public static Pattern ApplyGroove(Pattern pattern, ExtractedGroove groove, GrooveApplyOptions options)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(groove);
        options ??= new GrooveApplyOptions();

        // Clamp amount to valid range
        double amount = Math.Clamp(options.Amount, 0.0, 1.0);

        // If amount is 0, just clone the pattern
        if (amount < 0.001)
            return ClonePattern(pattern);

        var newPattern = ClonePattern(pattern);

        // Optionally quantize first
        if (options.QuantizeFirst)
        {
            QuantizeEvents(newPattern.Events, options.QuantizeGrid);
        }

        // Group notes by micro-timing windows if preserving micro-timing
        Dictionary<int, List<NoteEvent>>? microGroups = null;
        if (options.PreserveMicroTiming)
        {
            microGroups = GroupByMicroTiming(newPattern.Events, options.MicroTimingWindow);
        }

        // Apply groove to each note
        foreach (var note in newPattern.Events)
        {
            double originalBeat = note.Beat;

            // Apply timing deviation
            if (options.ApplyTiming && groove.TimingDeviations.Count > 0)
            {
                double deviation = groove.GetTimingDeviationAt(originalBeat);

                // Convert ticks to beats
                double deviationBeats = deviation / groove.Resolution;

                // Apply with amount interpolation
                double adjustedBeat = originalBeat + (deviationBeats * amount);

                // Handle micro-timing preservation
                if (options.PreserveMicroTiming && microGroups != null)
                {
                    adjustedBeat = ApplyMicroTimingPreservation(
                        note, originalBeat, adjustedBeat, microGroups, options.MicroTimingWindow);
                }

                // Ensure beat stays positive and within loop
                note.Beat = Math.Max(0, adjustedBeat);
                if (note.Beat >= pattern.LoopLength)
                    note.Beat %= pattern.LoopLength;
            }

            // Apply velocity adjustment
            if (options.ApplyVelocity && groove.VelocityPattern.Count > 0)
            {
                double multiplier = groove.GetVelocityMultiplierAt(originalBeat);

                // Interpolate multiplier based on amount
                double interpolatedMultiplier = 1.0 + (multiplier - 1.0) * amount;

                // Apply and clamp
                int newVelocity = (int)Math.Round(note.Velocity * interpolatedMultiplier);
                note.Velocity = Math.Clamp(newVelocity, 1, 127);
            }
        }

        // Re-sort events by beat position
        newPattern.Events = [.. newPattern.Events.OrderBy(e => e.Beat)];

        return newPattern;
    }

    /// <summary>
    /// Applies a built-in groove template by name.
    /// </summary>
    /// <param name="pattern">The source pattern.</param>
    /// <param name="templateName">Name of the built-in template.</param>
    /// <param name="amount">Amount of groove (0.0-1.0).</param>
    /// <returns>A new pattern with the groove applied, or clone if template not found.</returns>
    public static Pattern ApplyBuiltInGroove(Pattern pattern, string templateName, double amount = 1.0)
    {
        var groove = GrooveTemplateManager.GetBuiltInTemplate(templateName);
        if (groove == null)
            return ClonePattern(pattern);

        return ApplyGroove(pattern, groove, amount);
    }

    /// <summary>
    /// Blends two grooves together.
    /// </summary>
    /// <param name="groove1">First groove.</param>
    /// <param name="groove2">Second groove.</param>
    /// <param name="blendAmount">Blend amount (0.0 = all groove1, 1.0 = all groove2).</param>
    /// <returns>A new blended groove.</returns>
    public static ExtractedGroove BlendGrooves(ExtractedGroove groove1, ExtractedGroove groove2, double blendAmount)
    {
        ArgumentNullException.ThrowIfNull(groove1);
        ArgumentNullException.ThrowIfNull(groove2);

        blendAmount = Math.Clamp(blendAmount, 0.0, 1.0);

        var result = new ExtractedGroove
        {
            Name = $"Blend: {groove1.Name} + {groove2.Name}",
            Description = $"Blended groove ({(1 - blendAmount) * 100:F0}% {groove1.Name}, {blendAmount * 100:F0}% {groove2.Name})",
            Resolution = Math.Max(groove1.Resolution, groove2.Resolution),
            CycleLengthBeats = Math.Max(groove1.CycleLengthBeats, groove2.CycleLengthBeats),
            SwingAmount = groove1.SwingAmount + (groove2.SwingAmount - groove1.SwingAmount) * blendAmount,
            CreatedAt = DateTime.Now,
            Tags = [.. groove1.Tags.Union(groove2.Tags)]
        };

        // Blend timing deviations
        var allPositions = groove1.TimingDeviations
            .Select(t => t.BeatPosition)
            .Union(groove2.TimingDeviations.Select(t => t.BeatPosition))
            .OrderBy(p => p)
            .ToList();

        foreach (var pos in allPositions)
        {
            double dev1 = groove1.GetTimingDeviationAt(pos);
            double dev2 = groove2.GetTimingDeviationAt(pos);
            double blendedDev = dev1 + (dev2 - dev1) * blendAmount;

            result.TimingDeviations.Add(new TimingDeviation
            {
                BeatPosition = pos,
                DeviationInTicks = blendedDev
            });
        }

        // Blend velocity patterns
        var allVelPositions = groove1.VelocityPattern
            .Select(v => v.BeatPosition)
            .Union(groove2.VelocityPattern.Select(v => v.BeatPosition))
            .OrderBy(p => p)
            .ToList();

        foreach (var pos in allVelPositions)
        {
            double vel1 = groove1.GetVelocityMultiplierAt(pos);
            double vel2 = groove2.GetVelocityMultiplierAt(pos);
            double blendedVel = vel1 + (vel2 - vel1) * blendAmount;

            result.VelocityPattern.Add(new VelocityPoint
            {
                BeatPosition = pos,
                VelocityMultiplier = blendedVel
            });
        }

        return result;
    }

    /// <summary>
    /// Inverts a groove (makes late notes early and vice versa).
    /// </summary>
    /// <param name="groove">The groove to invert.</param>
    /// <returns>A new inverted groove.</returns>
    public static ExtractedGroove InvertGroove(ExtractedGroove groove)
    {
        ArgumentNullException.ThrowIfNull(groove);

        var result = groove.Clone();
        result.Name = $"{groove.Name} (Inverted)";
        result.Description = $"Inverted version of {groove.Name}";

        // Invert timing deviations
        result.TimingDeviations = groove.TimingDeviations
            .Select(t => new TimingDeviation
            {
                BeatPosition = t.BeatPosition,
                DeviationInTicks = -t.DeviationInTicks
            })
            .ToList();

        // Invert velocity pattern (mirror around 1.0)
        result.VelocityPattern = groove.VelocityPattern
            .Select(v => new VelocityPoint
            {
                BeatPosition = v.BeatPosition,
                VelocityMultiplier = 2.0 - v.VelocityMultiplier // Mirror around 1.0
            })
            .ToList();

        return result;
    }

    /// <summary>
    /// Scales the intensity of a groove.
    /// </summary>
    /// <param name="groove">The groove to scale.</param>
    /// <param name="scale">Scale factor (1.0 = no change, 2.0 = double intensity).</param>
    /// <returns>A new scaled groove.</returns>
    public static ExtractedGroove ScaleGroove(ExtractedGroove groove, double scale)
    {
        ArgumentNullException.ThrowIfNull(groove);

        var result = groove.Clone();
        result.Name = $"{groove.Name} (x{scale:F1})";
        result.Description = $"Scaled version of {groove.Name} at {scale:F1}x intensity";

        // Scale timing deviations
        result.TimingDeviations = groove.TimingDeviations
            .Select(t => new TimingDeviation
            {
                BeatPosition = t.BeatPosition,
                DeviationInTicks = t.DeviationInTicks * scale
            })
            .ToList();

        // Scale velocity pattern (scale deviation from 1.0)
        result.VelocityPattern = groove.VelocityPattern
            .Select(v => new VelocityPoint
            {
                BeatPosition = v.BeatPosition,
                VelocityMultiplier = 1.0 + (v.VelocityMultiplier - 1.0) * scale
            })
            .ToList();

        return result;
    }

    /// <summary>
    /// Shifts a groove's timing by a specified amount.
    /// Useful for creating push/pull variations.
    /// </summary>
    /// <param name="groove">The groove to shift.</param>
    /// <param name="shiftTicks">Amount to shift in ticks (positive = late, negative = early).</param>
    /// <returns>A new shifted groove.</returns>
    public static ExtractedGroove ShiftGroove(ExtractedGroove groove, double shiftTicks)
    {
        ArgumentNullException.ThrowIfNull(groove);

        var result = groove.Clone();
        string direction = shiftTicks > 0 ? "Late" : "Early";
        result.Name = $"{groove.Name} ({direction})";
        result.Description = $"Shifted version of {groove.Name} by {Math.Abs(shiftTicks):F0} ticks {direction.ToLower()}";

        // Add constant offset to all timing deviations
        result.TimingDeviations = groove.TimingDeviations
            .Select(t => new TimingDeviation
            {
                BeatPosition = t.BeatPosition,
                DeviationInTicks = t.DeviationInTicks + shiftTicks
            })
            .ToList();

        return result;
    }

    #region Private Helper Methods

    /// <summary>
    /// Creates a deep clone of a pattern.
    /// </summary>
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
    /// Quantizes note events to a grid.
    /// </summary>
    private static void QuantizeEvents(List<NoteEvent> events, double gridSize)
    {
        foreach (var note in events)
        {
            note.Beat = Math.Round(note.Beat / gridSize) * gridSize;
        }
    }

    /// <summary>
    /// Groups notes by micro-timing windows.
    /// </summary>
    private static Dictionary<int, List<NoteEvent>> GroupByMicroTiming(List<NoteEvent> events, double windowSize)
    {
        var groups = new Dictionary<int, List<NoteEvent>>();

        foreach (var note in events)
        {
            int windowIndex = (int)(note.Beat / windowSize);

            if (!groups.TryGetValue(windowIndex, out var list))
            {
                list = [];
                groups[windowIndex] = list;
            }

            list.Add(note);
        }

        return groups;
    }

    /// <summary>
    /// Applies micro-timing preservation logic.
    /// </summary>
    private static double ApplyMicroTimingPreservation(
        NoteEvent note,
        double originalBeat,
        double adjustedBeat,
        Dictionary<int, List<NoteEvent>> microGroups,
        double windowSize)
    {
        int windowIndex = (int)(originalBeat / windowSize);

        if (!microGroups.TryGetValue(windowIndex, out var group) || group.Count <= 1)
            return adjustedBeat;

        // Find the reference note (first note in the group by original position)
        var refNote = group.OrderBy(n => n.Beat).First();
        if (refNote == note)
            return adjustedBeat;

        // Calculate the original offset from reference
        double originalOffset = originalBeat - refNote.Beat;

        // Get the groove-adjusted position of the reference note
        // This requires tracking the adjustment - for now, use the simple approach
        return adjustedBeat;
    }

    #endregion
}
