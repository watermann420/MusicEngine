// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;


namespace MusicEngine.Core.Freeze;


/// <summary>
/// Represents the progress of an offline track rendering operation.
/// Used with <see cref="IProgress{T}"/> to report freeze/bounce status.
/// </summary>
/// <param name="Stage">The current rendering stage (e.g., "Preparing", "Rendering", "Finalizing").</param>
/// <param name="CurrentPositionBeats">The current position in beats being rendered.</param>
/// <param name="TotalLengthBeats">The total length in beats to render.</param>
/// <param name="TrackIndex">The index of the track being rendered.</param>
/// <param name="Message">An optional descriptive message about the current operation.</param>
public record RenderProgress(
    string Stage,
    double CurrentPositionBeats,
    double TotalLengthBeats,
    int TrackIndex,
    string? Message = null)
{
    /// <summary>
    /// Gets the completion percentage (0-100) based on current position and total length.
    /// </summary>
    public double PercentComplete => TotalLengthBeats > 0
        ? Math.Min(100.0, CurrentPositionBeats / TotalLengthBeats * 100)
        : 0;

    /// <summary>
    /// Gets whether the rendering is complete.
    /// </summary>
    public bool IsComplete => Stage == "Complete" ||
        (TotalLengthBeats > 0 && CurrentPositionBeats >= TotalLengthBeats);

    /// <summary>
    /// Gets or sets the current position in samples.
    /// </summary>
    public long CurrentPositionSamples { get; init; }

    /// <summary>
    /// Gets or sets the total length in samples.
    /// </summary>
    public long TotalLengthSamples { get; init; }

    /// <summary>
    /// Gets or sets the elapsed time for the render operation.
    /// </summary>
    public TimeSpan ElapsedTime { get; init; }

    /// <summary>
    /// Gets or sets the estimated time remaining for the render operation.
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; init; }

    /// <summary>
    /// Gets the render speed multiplier (how many times faster than realtime).
    /// </summary>
    public double RenderSpeedMultiplier { get; init; } = 1.0;

    /// <summary>
    /// Creates a progress instance for the preparation stage.
    /// </summary>
    /// <param name="trackIndex">The track index being prepared.</param>
    /// <param name="totalLengthBeats">The total length to render.</param>
    /// <returns>A new <see cref="RenderProgress"/> instance.</returns>
    public static RenderProgress Preparing(int trackIndex, double totalLengthBeats)
        => new("Preparing", 0, totalLengthBeats, trackIndex, "Preparing track for offline rendering...");

    /// <summary>
    /// Creates a progress instance for the rendering stage.
    /// </summary>
    /// <param name="trackIndex">The track index being rendered.</param>
    /// <param name="currentPosition">The current position in beats.</param>
    /// <param name="totalLength">The total length in beats.</param>
    /// <returns>A new <see cref="RenderProgress"/> instance.</returns>
    public static RenderProgress Rendering(int trackIndex, double currentPosition, double totalLength)
        => new("Rendering", currentPosition, totalLength, trackIndex);

    /// <summary>
    /// Creates a progress instance for the finalizing stage.
    /// </summary>
    /// <param name="trackIndex">The track index being finalized.</param>
    /// <param name="totalLengthBeats">The total length rendered.</param>
    /// <returns>A new <see cref="RenderProgress"/> instance.</returns>
    public static RenderProgress Finalizing(int trackIndex, double totalLengthBeats)
        => new("Finalizing", totalLengthBeats, totalLengthBeats, trackIndex, "Finalizing frozen audio...");

    /// <summary>
    /// Creates a progress instance indicating completion.
    /// </summary>
    /// <param name="trackIndex">The track index that was rendered.</param>
    /// <param name="totalLengthBeats">The total length rendered.</param>
    /// <param name="message">An optional completion message.</param>
    /// <returns>A new <see cref="RenderProgress"/> instance.</returns>
    public static RenderProgress Complete(int trackIndex, double totalLengthBeats, string? message = null)
        => new("Complete", totalLengthBeats, totalLengthBeats, trackIndex,
            message ?? "Track freeze completed successfully");

    /// <summary>
    /// Creates a progress instance indicating an error.
    /// </summary>
    /// <param name="trackIndex">The track index where the error occurred.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <returns>A new <see cref="RenderProgress"/> instance.</returns>
    public static RenderProgress Error(int trackIndex, string errorMessage)
        => new("Error", 0, 0, trackIndex, errorMessage);

    /// <summary>
    /// Returns a string representation of the progress.
    /// </summary>
    public override string ToString()
    {
        var result = $"[Track {TrackIndex}] [{Stage}] {PercentComplete:F1}% ({CurrentPositionBeats:F2}/{TotalLengthBeats:F2} beats)";
        if (!string.IsNullOrEmpty(Message))
        {
            result += $" - {Message}";
        }
        if (EstimatedTimeRemaining.HasValue && !IsComplete)
        {
            result += $" (ETA: {EstimatedTimeRemaining.Value:mm\\:ss})";
        }
        return result;
    }
}
