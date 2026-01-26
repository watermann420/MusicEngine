// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System.Collections.Generic;
using System.Text.Json.Serialization;


namespace MusicEngine.Core.Groove;


/// <summary>
/// Represents a timing deviation at a specific beat position.
/// </summary>
public record TimingDeviation
{
    /// <summary>
    /// Position within the groove cycle (0.0 to resolution).
    /// Typically expressed as fractions of a beat (e.g., 0, 0.25, 0.5, 0.75 for 16th notes).
    /// </summary>
    public double BeatPosition { get; init; }

    /// <summary>
    /// Deviation from the quantized grid in ticks.
    /// Positive values indicate the note was played late, negative values indicate early.
    /// </summary>
    public double DeviationInTicks { get; init; }
}


/// <summary>
/// Represents a velocity value at a specific beat position.
/// </summary>
public record VelocityPoint
{
    /// <summary>
    /// Position within the groove cycle (0.0 to resolution).
    /// </summary>
    public double BeatPosition { get; init; }

    /// <summary>
    /// Velocity multiplier (1.0 = original velocity, 0.5 = half velocity, etc.).
    /// </summary>
    public double VelocityMultiplier { get; init; }
}


/// <summary>
/// Represents an extracted groove that captures the timing feel and velocity dynamics
/// from a musical performance or pattern. Can be applied to other patterns to transfer
/// the same musical feel.
/// </summary>
public class ExtractedGroove
{
    /// <summary>
    /// Name of the groove template for identification.
    /// </summary>
    public string Name { get; set; } = "Untitled Groove";

    /// <summary>
    /// Optional description of the groove characteristics.
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// List of timing deviations representing how each beat position deviates from the grid.
    /// </summary>
    public List<TimingDeviation> TimingDeviations { get; set; } = [];

    /// <summary>
    /// Velocity curve representing the dynamic pattern of the groove.
    /// </summary>
    public List<VelocityPoint> VelocityPattern { get; set; } = [];

    /// <summary>
    /// Resolution in ticks per beat (PPQN - Pulses Per Quarter Note).
    /// Standard values: 24, 48, 96, 120, 240, 480, 960.
    /// </summary>
    public int Resolution { get; set; } = 480;

    /// <summary>
    /// Length of the groove cycle in beats.
    /// Typically 1 for a single beat groove or 4 for a full bar.
    /// </summary>
    public double CycleLengthBeats { get; set; } = 1.0;

    /// <summary>
    /// Calculated swing amount as a percentage (0-100).
    /// 50% = straight, 67% = triplet swing.
    /// </summary>
    public double SwingAmount { get; set; }

    /// <summary>
    /// Source information about where this groove was extracted from.
    /// </summary>
    public string? SourceInfo { get; set; }

    /// <summary>
    /// Timestamp when the groove was created or extracted.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// Tags for categorizing the groove (e.g., "hip-hop", "jazz", "funk").
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Creates a deep copy of this groove.
    /// </summary>
    public ExtractedGroove Clone()
    {
        return new ExtractedGroove
        {
            Name = Name,
            Description = Description,
            TimingDeviations = [.. TimingDeviations],
            VelocityPattern = [.. VelocityPattern],
            Resolution = Resolution,
            CycleLengthBeats = CycleLengthBeats,
            SwingAmount = SwingAmount,
            SourceInfo = SourceInfo,
            CreatedAt = CreatedAt,
            Tags = [.. Tags]
        };
    }

    /// <summary>
    /// Gets the timing deviation for a specific beat position.
    /// Interpolates between known points if exact position is not available.
    /// </summary>
    /// <param name="beatPosition">Position within the groove cycle.</param>
    /// <returns>Deviation in ticks.</returns>
    public double GetTimingDeviationAt(double beatPosition)
    {
        if (TimingDeviations.Count == 0)
            return 0.0;

        // Normalize position to cycle
        double normalizedPos = beatPosition % CycleLengthBeats;
        if (normalizedPos < 0) normalizedPos += CycleLengthBeats;

        // Find surrounding points for interpolation
        TimingDeviation? before = null;
        TimingDeviation? after = null;

        foreach (var deviation in TimingDeviations)
        {
            if (deviation.BeatPosition <= normalizedPos)
            {
                if (before == null || deviation.BeatPosition > before.BeatPosition)
                    before = deviation;
            }
            if (deviation.BeatPosition >= normalizedPos)
            {
                if (after == null || deviation.BeatPosition < after.BeatPosition)
                    after = deviation;
            }
        }

        // Exact match or only one point
        if (before == null && after == null)
            return 0.0;
        if (before == null)
            return after!.DeviationInTicks;
        if (after == null)
            return before.DeviationInTicks;
        if (Math.Abs(before.BeatPosition - after.BeatPosition) < 0.0001)
            return before.DeviationInTicks;

        // Linear interpolation
        double t = (normalizedPos - before.BeatPosition) / (after.BeatPosition - before.BeatPosition);
        return before.DeviationInTicks + t * (after.DeviationInTicks - before.DeviationInTicks);
    }

    /// <summary>
    /// Gets the velocity multiplier for a specific beat position.
    /// Interpolates between known points if exact position is not available.
    /// </summary>
    /// <param name="beatPosition">Position within the groove cycle.</param>
    /// <returns>Velocity multiplier (1.0 = no change).</returns>
    public double GetVelocityMultiplierAt(double beatPosition)
    {
        if (VelocityPattern.Count == 0)
            return 1.0;

        // Normalize position to cycle
        double normalizedPos = beatPosition % CycleLengthBeats;
        if (normalizedPos < 0) normalizedPos += CycleLengthBeats;

        // Find surrounding points for interpolation
        VelocityPoint? before = null;
        VelocityPoint? after = null;

        foreach (var point in VelocityPattern)
        {
            if (point.BeatPosition <= normalizedPos)
            {
                if (before == null || point.BeatPosition > before.BeatPosition)
                    before = point;
            }
            if (point.BeatPosition >= normalizedPos)
            {
                if (after == null || point.BeatPosition < after.BeatPosition)
                    after = point;
            }
        }

        // Exact match or only one point
        if (before == null && after == null)
            return 1.0;
        if (before == null)
            return after!.VelocityMultiplier;
        if (after == null)
            return before.VelocityMultiplier;
        if (Math.Abs(before.BeatPosition - after.BeatPosition) < 0.0001)
            return before.VelocityMultiplier;

        // Linear interpolation
        double t = (normalizedPos - before.BeatPosition) / (after.BeatPosition - before.BeatPosition);
        return before.VelocityMultiplier + t * (after.VelocityMultiplier - before.VelocityMultiplier);
    }
}
