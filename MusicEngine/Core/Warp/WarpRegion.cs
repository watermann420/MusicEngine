//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Represents a region between two warp markers with calculated stretch ratio.

using System;

namespace MusicEngine.Core.Warp;

/// <summary>
/// Algorithm used for time stretching within a warp region.
/// </summary>
public enum WarpAlgorithm
{
    /// <summary>
    /// Phase vocoder algorithm for high-quality tonal content.
    /// Best for sustained notes, pads, and melodic material.
    /// </summary>
    PhaseVocoder,

    /// <summary>
    /// Transient-optimized algorithm for percussive content.
    /// Preserves attack transients, better for drums and rhythmic material.
    /// </summary>
    Transient,

    /// <summary>
    /// Complex algorithm that adapts between tonal and transient modes.
    /// Automatically detects and preserves both transients and tonal content.
    /// </summary>
    Complex,

    /// <summary>
    /// Repitch mode - changes speed by changing pitch (no time stretch).
    /// Fastest but pitch varies with speed like a record player.
    /// </summary>
    Repitch,

    /// <summary>
    /// Texture mode for ambient/textural content.
    /// Best for pads, drones, and atmospheric sounds.
    /// </summary>
    Texture
}

/// <summary>
/// Represents a region between two warp markers.
/// Calculates and applies the stretch ratio for the region based on marker positions.
/// </summary>
public class WarpRegion
{
    /// <summary>Unique identifier for this region.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>The starting warp marker of this region.</summary>
    public WarpMarker StartMarker { get; }

    /// <summary>The ending warp marker of this region.</summary>
    public WarpMarker EndMarker { get; }

    /// <summary>Algorithm to use for time stretching in this region.</summary>
    public WarpAlgorithm Algorithm { get; set; } = WarpAlgorithm.Complex;

    /// <summary>Audio sample rate for time calculations.</summary>
    public int SampleRate { get; set; } = 44100;

    /// <summary>
    /// Gets the stretch ratio for this region.
    /// Values > 1.0 mean the audio is sped up (original plays faster).
    /// Values < 1.0 mean the audio is slowed down (original plays slower).
    /// </summary>
    public double StretchRatio
    {
        get
        {
            long originalDuration = EndMarker.OriginalPositionSamples - StartMarker.OriginalPositionSamples;
            long warpedDuration = EndMarker.WarpedPositionSamples - StartMarker.WarpedPositionSamples;

            if (warpedDuration == 0)
                return 1.0;

            return (double)originalDuration / warpedDuration;
        }
    }

    /// <summary>
    /// Gets the inverse stretch ratio (factor to multiply playback speed by).
    /// Values > 1.0 mean playback is faster.
    /// Values < 1.0 mean playback is slower.
    /// </summary>
    public double PlaybackSpeedFactor => 1.0 / StretchRatio;

    /// <summary>
    /// Gets the original duration of this region in samples.
    /// </summary>
    public long OriginalDurationSamples =>
        EndMarker.OriginalPositionSamples - StartMarker.OriginalPositionSamples;

    /// <summary>
    /// Gets the warped (output) duration of this region in samples.
    /// </summary>
    public long WarpedDurationSamples =>
        EndMarker.WarpedPositionSamples - StartMarker.WarpedPositionSamples;

    /// <summary>
    /// Gets the original duration of this region in seconds.
    /// </summary>
    public double OriginalDurationSeconds => (double)OriginalDurationSamples / SampleRate;

    /// <summary>
    /// Gets the warped duration of this region in seconds.
    /// </summary>
    public double WarpedDurationSeconds => (double)WarpedDurationSamples / SampleRate;

    /// <summary>
    /// Creates a new warp region between two markers.
    /// </summary>
    /// <param name="startMarker">The starting warp marker.</param>
    /// <param name="endMarker">The ending warp marker.</param>
    /// <param name="sampleRate">Audio sample rate.</param>
    public WarpRegion(WarpMarker startMarker, WarpMarker endMarker, int sampleRate = 44100)
    {
        StartMarker = startMarker ?? throw new ArgumentNullException(nameof(startMarker));
        EndMarker = endMarker ?? throw new ArgumentNullException(nameof(endMarker));
        SampleRate = sampleRate;

        // Validate that end comes after start
        if (endMarker.OriginalPositionSamples <= startMarker.OriginalPositionSamples)
        {
            throw new ArgumentException("End marker must come after start marker in original position.");
        }
    }

    /// <summary>
    /// Creates a new warp region between two markers with a specified algorithm.
    /// </summary>
    /// <param name="startMarker">The starting warp marker.</param>
    /// <param name="endMarker">The ending warp marker.</param>
    /// <param name="algorithm">Time stretch algorithm to use.</param>
    /// <param name="sampleRate">Audio sample rate.</param>
    public WarpRegion(WarpMarker startMarker, WarpMarker endMarker, WarpAlgorithm algorithm, int sampleRate = 44100)
        : this(startMarker, endMarker, sampleRate)
    {
        Algorithm = algorithm;
    }

    /// <summary>
    /// Checks if a given original position (in samples) falls within this region.
    /// </summary>
    /// <param name="originalPositionSamples">Position in original audio (samples).</param>
    /// <returns>True if the position is within this region.</returns>
    public bool ContainsOriginalPosition(long originalPositionSamples)
    {
        return originalPositionSamples >= StartMarker.OriginalPositionSamples &&
               originalPositionSamples < EndMarker.OriginalPositionSamples;
    }

    /// <summary>
    /// Checks if a given warped position (in samples) falls within this region.
    /// </summary>
    /// <param name="warpedPositionSamples">Position in warped output (samples).</param>
    /// <returns>True if the position is within this region.</returns>
    public bool ContainsWarpedPosition(long warpedPositionSamples)
    {
        return warpedPositionSamples >= StartMarker.WarpedPositionSamples &&
               warpedPositionSamples < EndMarker.WarpedPositionSamples;
    }

    /// <summary>
    /// Maps a warped (output) position back to its original position in the source audio.
    /// </summary>
    /// <param name="warpedPositionSamples">Position in warped output (samples).</param>
    /// <returns>Corresponding position in original audio (samples).</returns>
    public long WarpedToOriginal(long warpedPositionSamples)
    {
        // Calculate progress through the warped region (0.0 to 1.0)
        long warpedOffset = warpedPositionSamples - StartMarker.WarpedPositionSamples;
        double progress = WarpedDurationSamples > 0
            ? (double)warpedOffset / WarpedDurationSamples
            : 0.0;

        // Apply progress to original duration
        long originalOffset = (long)(progress * OriginalDurationSamples);
        return StartMarker.OriginalPositionSamples + originalOffset;
    }

    /// <summary>
    /// Maps a warped (output) position back to its original position with fractional precision.
    /// </summary>
    /// <param name="warpedPositionSamples">Position in warped output (samples).</param>
    /// <returns>Corresponding position in original audio (samples, fractional for interpolation).</returns>
    public double WarpedToOriginalPrecise(long warpedPositionSamples)
    {
        long warpedOffset = warpedPositionSamples - StartMarker.WarpedPositionSamples;
        double progress = WarpedDurationSamples > 0
            ? (double)warpedOffset / WarpedDurationSamples
            : 0.0;

        double originalOffset = progress * OriginalDurationSamples;
        return StartMarker.OriginalPositionSamples + originalOffset;
    }

    /// <summary>
    /// Maps an original position to its warped (output) position.
    /// </summary>
    /// <param name="originalPositionSamples">Position in original audio (samples).</param>
    /// <returns>Corresponding position in warped output (samples).</returns>
    public long OriginalToWarped(long originalPositionSamples)
    {
        // Calculate progress through the original region (0.0 to 1.0)
        long originalOffset = originalPositionSamples - StartMarker.OriginalPositionSamples;
        double progress = OriginalDurationSamples > 0
            ? (double)originalOffset / OriginalDurationSamples
            : 0.0;

        // Apply progress to warped duration
        long warpedOffset = (long)(progress * WarpedDurationSamples);
        return StartMarker.WarpedPositionSamples + warpedOffset;
    }

    /// <summary>
    /// Gets the stretch ratio at a specific warped position (for variable tempo within region).
    /// Currently returns constant ratio, but could be extended for curves.
    /// </summary>
    /// <param name="warpedPositionSamples">Position in warped output (samples).</param>
    /// <returns>Local stretch ratio at the position.</returns>
    public double GetStretchRatioAt(long warpedPositionSamples)
    {
        // Currently returns constant stretch ratio
        // Could be extended to support curved warping within a region
        return StretchRatio;
    }

    /// <summary>
    /// Calculates the instantaneous playback rate for the TimeStretchEffect at a given position.
    /// </summary>
    /// <returns>Playback rate (1.0 = normal, 0.5 = half speed, 2.0 = double speed).</returns>
    public float GetTimeStretchFactor()
    {
        // TimeStretchEffect uses inverse: < 1.0 = slower, > 1.0 = faster
        return (float)(1.0 / StretchRatio);
    }

    /// <summary>
    /// Gets the appropriate TimeStretchQuality based on the algorithm and stretch ratio.
    /// </summary>
    /// <returns>Recommended quality setting for the TimeStretchEffect.</returns>
    public Effects.Special.TimeStretchQuality GetRecommendedQuality()
    {
        return Algorithm switch
        {
            WarpAlgorithm.PhaseVocoder => Effects.Special.TimeStretchQuality.HighQuality,
            WarpAlgorithm.Transient => Effects.Special.TimeStretchQuality.Fast,
            WarpAlgorithm.Complex => Effects.Special.TimeStretchQuality.Normal,
            WarpAlgorithm.Texture => Effects.Special.TimeStretchQuality.HighQuality,
            WarpAlgorithm.Repitch => Effects.Special.TimeStretchQuality.Fast,
            _ => Effects.Special.TimeStretchQuality.Normal
        };
    }

    /// <summary>
    /// Gets whether transient preservation should be enabled for this region's algorithm.
    /// </summary>
    /// <returns>True if transients should be preserved.</returns>
    public bool ShouldPreserveTransients()
    {
        return Algorithm switch
        {
            WarpAlgorithm.Transient => true,
            WarpAlgorithm.Complex => true,
            WarpAlgorithm.PhaseVocoder => false,
            WarpAlgorithm.Texture => false,
            WarpAlgorithm.Repitch => false,
            _ => true
        };
    }

    public override string ToString() =>
        $"WarpRegion [{StartMarker.OriginalPositionSamples}-{EndMarker.OriginalPositionSamples}] " +
        $"Ratio: {StretchRatio:F3} ({Algorithm})";
}
