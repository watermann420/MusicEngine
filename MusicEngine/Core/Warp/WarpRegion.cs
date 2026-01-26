// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Collections.Generic;
using System.Linq;

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

/// <summary>
/// Represents a collection of warp markers for an audio region with time-stretching capabilities.
/// Manages the relationship between original audio time and warped musical time.
/// This class implements the container/manager pattern for warp markers.
/// </summary>
public class WarpMarkerCollection
{
    private readonly List<WarpMarker> _markers = new();
    private readonly object _lock = new();

    /// <summary>Audio sample rate for time calculations.</summary>
    public int SampleRate { get; }

    /// <summary>Total length of the audio in samples.</summary>
    public long TotalSamples { get; }

    /// <summary>Total duration of the audio in seconds.</summary>
    public double TotalDurationSeconds => (double)TotalSamples / SampleRate;

    /// <summary>BPM used for beat calculations.</summary>
    public double Bpm { get; set; } = 120.0;

    /// <summary>Whether warping is enabled for this region.</summary>
    public bool WarpEnabled { get; set; } = true;

    /// <summary>Event raised when a marker is added.</summary>
    public event EventHandler<WarpMarkerEventArgs>? MarkerAdded;

    /// <summary>Event raised when a marker is removed.</summary>
    public event EventHandler<WarpMarkerEventArgs>? MarkerRemoved;

    /// <summary>Event raised when a marker is moved.</summary>
    public event EventHandler<WarpMarkerEventArgs>? MarkerMoved;

    /// <summary>Event raised when warp configuration changes.</summary>
    public event EventHandler? WarpChanged;

    /// <summary>
    /// Creates a new warp marker collection for the specified audio.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate.</param>
    /// <param name="totalSamples">Total length of the audio in samples.</param>
    public WarpMarkerCollection(int sampleRate, long totalSamples)
    {
        SampleRate = sampleRate;
        TotalSamples = totalSamples;

        // Create anchor markers at start and end
        var startMarker = new WarpMarker(0, sampleRate, 0, WarpMarkerType.Beat)
        {
            IsAnchor = true,
            IsLocked = true,
            Label = "Start"
        };
        var endMarker = new WarpMarker(totalSamples, sampleRate, TotalDurationSeconds * Bpm / 60.0, WarpMarkerType.Beat)
        {
            IsAnchor = true,
            Label = "End"
        };
        _markers.Add(startMarker);
        _markers.Add(endMarker);
    }

    /// <summary>
    /// Adds a marker at the specified original sample position.
    /// The warped beat position is calculated via interpolation from existing markers.
    /// </summary>
    /// <param name="originalSamplePosition">Position in original audio (samples).</param>
    /// <param name="type">Type of marker to create.</param>
    /// <returns>The created warp marker.</returns>
    public WarpMarker AddMarker(long originalSamplePosition, WarpMarkerType type = WarpMarkerType.User)
    {
        // Calculate the warped beat position via interpolation
        double warpedBeatPosition = OriginalToWarped(originalSamplePosition);

        var marker = new WarpMarker(originalSamplePosition, SampleRate, warpedBeatPosition, type);

        lock (_lock)
        {
            _markers.Add(marker);
            _markers.Sort();
        }

        MarkerAdded?.Invoke(this, new WarpMarkerEventArgs(marker, _markers.IndexOf(marker)));
        WarpChanged?.Invoke(this, EventArgs.Empty);

        return marker;
    }

    /// <summary>
    /// Adds a marker at a beat position (calculates original position via interpolation).
    /// </summary>
    /// <param name="beatPosition">Beat position for the marker.</param>
    /// <param name="type">Type of marker to create.</param>
    /// <returns>The created warp marker.</returns>
    public WarpMarker AddMarkerAtBeat(double beatPosition, WarpMarkerType type = WarpMarkerType.User)
    {
        // Calculate original position from beat via interpolation
        long originalSamplePosition = WarpedToOriginal(beatPosition);

        var marker = new WarpMarker(originalSamplePosition, SampleRate, beatPosition, type);

        lock (_lock)
        {
            _markers.Add(marker);
            _markers.Sort();
        }

        MarkerAdded?.Invoke(this, new WarpMarkerEventArgs(marker, _markers.IndexOf(marker)));
        WarpChanged?.Invoke(this, EventArgs.Empty);

        return marker;
    }

    /// <summary>
    /// Removes a marker by its ID.
    /// </summary>
    /// <param name="markerId">ID of the marker to remove.</param>
    /// <returns>True if the marker was removed.</returns>
    public bool RemoveMarker(string markerId)
    {
        lock (_lock)
        {
            var marker = _markers.FirstOrDefault(m => m.MarkerId == markerId);
            if (marker == null || marker.IsLocked)
                return false;

            int index = _markers.IndexOf(marker);
            _markers.Remove(marker);

            MarkerRemoved?.Invoke(this, new WarpMarkerEventArgs(marker, index));
            WarpChanged?.Invoke(this, EventArgs.Empty);

            return true;
        }
    }

    /// <summary>
    /// Gets all markers sorted by original position.
    /// </summary>
    /// <returns>Read-only list of markers.</returns>
    public IReadOnlyList<WarpMarker> GetMarkers()
    {
        lock (_lock)
        {
            return _markers.OrderBy(m => m.OriginalPositionSamples).ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Gets a marker at or near the specified position.
    /// </summary>
    /// <param name="samplePosition">Position in samples.</param>
    /// <param name="tolerance">Search tolerance in samples.</param>
    /// <returns>The nearest marker, or null if none found within tolerance.</returns>
    public WarpMarker? GetMarkerNear(long samplePosition, long tolerance = 1000)
    {
        lock (_lock)
        {
            return _markers
                .Where(m => Math.Abs(m.OriginalPositionSamples - samplePosition) <= tolerance)
                .OrderBy(m => Math.Abs(m.OriginalPositionSamples - samplePosition))
                .FirstOrDefault();
        }
    }

    /// <summary>
    /// Moves a marker's warped position to a new beat position.
    /// </summary>
    /// <param name="markerId">ID of the marker to move.</param>
    /// <param name="newBeatPosition">New warped beat position.</param>
    /// <returns>True if the marker was moved.</returns>
    public bool MoveMarker(string markerId, double newBeatPosition)
    {
        lock (_lock)
        {
            var marker = _markers.FirstOrDefault(m => m.MarkerId == markerId);
            if (marker == null || marker.IsLocked)
                return false;

            // Validate the new position doesn't cross adjacent markers
            var sortedMarkers = _markers.OrderBy(m => m.OriginalPositionSamples).ToList();
            int index = sortedMarkers.IndexOf(marker);

            if (index > 0)
            {
                var prevMarker = sortedMarkers[index - 1];
                if (newBeatPosition <= prevMarker.WarpedBeatPosition)
                    return false;
            }

            if (index < sortedMarkers.Count - 1)
            {
                var nextMarker = sortedMarkers[index + 1];
                if (newBeatPosition >= nextMarker.WarpedBeatPosition)
                    return false;
            }

            marker.WarpedBeatPosition = newBeatPosition;
            marker.Touch();

            MarkerMoved?.Invoke(this, new WarpMarkerEventArgs(marker, index));
            WarpChanged?.Invoke(this, EventArgs.Empty);

            return true;
        }
    }

    /// <summary>
    /// Converts original sample position to warped beat position (interpolated).
    /// </summary>
    /// <param name="originalSamplePosition">Position in original audio (samples).</param>
    /// <returns>Warped beat position.</returns>
    public double OriginalToWarped(long originalSamplePosition)
    {
        var (before, after) = GetSurroundingMarkers(originalSamplePosition);

        if (before == null && after == null)
        {
            // No markers, use linear mapping
            double originalSeconds = (double)originalSamplePosition / SampleRate;
            return originalSeconds * Bpm / 60.0;
        }

        if (before == null)
            return after!.WarpedBeatPosition;

        if (after == null || before == after)
            return before.WarpedBeatPosition;

        // Linear interpolation between markers
        double t = (double)(originalSamplePosition - before.OriginalPositionSamples) /
                   (after.OriginalPositionSamples - before.OriginalPositionSamples);
        return before.WarpedBeatPosition + t * (after.WarpedBeatPosition - before.WarpedBeatPosition);
    }

    /// <summary>
    /// Converts warped beat position to original sample position (interpolated).
    /// </summary>
    /// <param name="warpedBeatPosition">Warped beat position.</param>
    /// <returns>Original sample position.</returns>
    public long WarpedToOriginal(double warpedBeatPosition)
    {
        var sortedMarkers = GetMarkers();

        if (sortedMarkers.Count < 2)
        {
            // No markers, use linear mapping
            double seconds = warpedBeatPosition * 60.0 / Bpm;
            return (long)(seconds * SampleRate);
        }

        // Find surrounding markers by beat position
        WarpMarker? before = null;
        WarpMarker? after = null;

        foreach (var marker in sortedMarkers)
        {
            if (marker.WarpedBeatPosition <= warpedBeatPosition)
                before = marker;
            if (marker.WarpedBeatPosition >= warpedBeatPosition && after == null)
                after = marker;
        }

        if (before == null)
            return sortedMarkers[0].OriginalPositionSamples;

        if (after == null || before == after)
            return before.OriginalPositionSamples;

        // Linear interpolation between markers
        double t = (warpedBeatPosition - before.WarpedBeatPosition) /
                   (after.WarpedBeatPosition - before.WarpedBeatPosition);
        return (long)(before.OriginalPositionSamples +
                      t * (after.OriginalPositionSamples - before.OriginalPositionSamples));
    }

    /// <summary>
    /// Gets the time stretch ratio at a given original position.
    /// Ratio > 1.0 means stretched (slower), Ratio < 1.0 means compressed (faster).
    /// </summary>
    /// <param name="originalSamplePosition">Position in original audio (samples).</param>
    /// <returns>Stretch ratio at the position.</returns>
    public double GetStretchRatioAt(long originalSamplePosition)
    {
        var (before, after) = GetSurroundingMarkers(originalSamplePosition);

        if (before == null || after == null || before == after)
            return 1.0;

        // Calculate stretch ratio between markers
        // Stretch ratio = (WarpedDelta / BPM * 60) / OriginalDelta
        double warpedDeltaBeats = after.WarpedBeatPosition - before.WarpedBeatPosition;
        double warpedDeltaSeconds = warpedDeltaBeats * 60.0 / Bpm;
        double originalDeltaSeconds = (double)(after.OriginalPositionSamples - before.OriginalPositionSamples) / SampleRate;

        if (originalDeltaSeconds <= 0)
            return 1.0;

        return warpedDeltaSeconds / originalDeltaSeconds;
    }

    /// <summary>
    /// Gets the two markers surrounding a position.
    /// </summary>
    /// <param name="originalSamplePosition">Position in original audio (samples).</param>
    /// <returns>Tuple of (marker before, marker after).</returns>
    public (WarpMarker? before, WarpMarker? after) GetSurroundingMarkers(long originalSamplePosition)
    {
        lock (_lock)
        {
            var sortedMarkers = _markers.OrderBy(m => m.OriginalPositionSamples).ToList();

            WarpMarker? before = null;
            WarpMarker? after = null;

            foreach (var marker in sortedMarkers)
            {
                if (marker.OriginalPositionSamples <= originalSamplePosition)
                    before = marker;
                if (marker.OriginalPositionSamples >= originalSamplePosition && after == null)
                    after = marker;
            }

            return (before, after);
        }
    }

    /// <summary>
    /// Auto-detects transients and adds markers at transient positions.
    /// </summary>
    /// <param name="audioData">Mono audio samples to analyze.</param>
    /// <param name="threshold">Detection threshold (0.0 to 1.0).</param>
    public void AutoWarpFromTransients(float[] audioData, float threshold = 0.3f)
    {
        var detector = new MusicEngine.Core.Analysis.TransientDetector(SampleRate);
        detector.Threshold = threshold;

        var transients = detector.AnalyzeBuffer(audioData, SampleRate);

        lock (_lock)
        {
            foreach (var transient in transients)
            {
                long positionSamples = (long)(transient.TimeSeconds * SampleRate);

                // Check if a marker already exists near this position
                if (GetMarkerNear(positionSamples, (long)(SampleRate * 0.01)) != null)
                    continue;

                var marker = AddMarker(positionSamples, WarpMarkerType.Transient);
                marker.TransientStrength = transient.Strength;
            }
        }

        WarpChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Quantizes all non-locked markers to the beat grid.
    /// </summary>
    /// <param name="gridSize">Grid size in beats (e.g., 0.25 for 1/16 note).</param>
    public void QuantizeToGrid(double gridSize = 0.25)
    {
        lock (_lock)
        {
            foreach (var marker in _markers)
            {
                if (marker.IsLocked)
                    continue;

                double quantizedBeat = Math.Round(marker.WarpedBeatPosition / gridSize) * gridSize;
                marker.WarpedBeatPosition = quantizedBeat;
                marker.Touch();
            }
        }

        WarpChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Resets all markers to original positions (no stretch).
    /// </summary>
    public void ResetToOriginal()
    {
        lock (_lock)
        {
            foreach (var marker in _markers)
            {
                if (!marker.IsLocked || !marker.IsAnchor || marker.MarkerType == WarpMarkerType.End)
                {
                    double originalSeconds = (double)marker.OriginalPositionSamples / SampleRate;
                    marker.WarpedBeatPosition = originalSeconds * Bpm / 60.0;
                    marker.Touch();
                }
            }
        }

        WarpChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Clears all non-anchor markers.
    /// </summary>
    public void ClearMarkers()
    {
        lock (_lock)
        {
            var markersToRemove = _markers.Where(m => !m.IsAnchor).ToList();
            foreach (var marker in markersToRemove)
            {
                int index = _markers.IndexOf(marker);
                _markers.Remove(marker);
                MarkerRemoved?.Invoke(this, new WarpMarkerEventArgs(marker, index));
            }
        }

        WarpChanged?.Invoke(this, EventArgs.Empty);
    }
}
