// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio clip container and playback.

using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicEngine.Core.Warp;

/// <summary>
/// Extension of AudioClip with full warp/elastic audio support.
/// Maintains warp markers and provides warped playback capabilities.
/// </summary>
public class WarpedAudioClip : AudioClip
{
    private readonly AudioWarpProcessor _warpProcessor;
    private bool _warpEnabled = true;

    /// <summary>
    /// Gets the warp processor managing this clip's warp markers.
    /// </summary>
    public AudioWarpProcessor WarpProcessor => _warpProcessor;

    /// <summary>
    /// Gets or sets whether warping is enabled for this clip.
    /// </summary>
    public bool WarpEnabled
    {
        get => _warpEnabled;
        set
        {
            _warpEnabled = value;
            Touch();
        }
    }

    /// <summary>
    /// Gets the list of warp markers for this clip.
    /// </summary>
    public IReadOnlyList<WarpMarker> WarpMarkers => _warpProcessor.Markers;

    /// <summary>
    /// Gets the number of warp markers.
    /// </summary>
    public int WarpMarkerCount => _warpProcessor.MarkerCount;

    /// <summary>
    /// Gets the list of warp regions.
    /// </summary>
    public IReadOnlyList<WarpRegion> WarpRegions => _warpProcessor.Regions;

    /// <summary>
    /// Gets the original length of the audio in samples (before warping).
    /// </summary>
    public long OriginalLengthSamples => _warpProcessor.TotalOriginalSamples;

    /// <summary>
    /// Gets the warped length of the audio in samples (after warping).
    /// </summary>
    public long WarpedLengthSamples => _warpProcessor.TotalWarpedSamples;

    /// <summary>
    /// Gets or sets the default warp algorithm for new regions.
    /// </summary>
    public WarpAlgorithm DefaultWarpAlgorithm
    {
        get => _warpProcessor.DefaultAlgorithm;
        set => _warpProcessor.DefaultAlgorithm = value;
    }

    /// <summary>
    /// Gets the effective length of the clip (warped if enabled, original otherwise).
    /// </summary>
    public new double Length
    {
        get
        {
            if (!_warpEnabled || _warpProcessor.SampleRate == 0)
                return base.Length;

            // Convert warped samples to beats
            double warpedSeconds = (double)WarpedLengthSamples / _warpProcessor.SampleRate;
            return warpedSeconds * _warpProcessor.Bpm / 60.0;
        }
        set => base.Length = value;
    }

    /// <summary>
    /// Gets the end position accounting for warping.
    /// </summary>
    public new double EndPosition => StartPosition + Length;

    /// <summary>
    /// Creates a new warped audio clip.
    /// </summary>
    public WarpedAudioClip() : base()
    {
        _warpProcessor = new AudioWarpProcessor();
    }

    /// <summary>
    /// Creates a new warped audio clip with file path and position.
    /// </summary>
    /// <param name="filePath">Path to the audio file.</param>
    /// <param name="startPosition">Start position in beats.</param>
    /// <param name="length">Length in beats.</param>
    /// <param name="trackIndex">Track index.</param>
    /// <param name="sampleRate">Audio sample rate.</param>
    /// <param name="bpm">Project tempo in BPM.</param>
    public WarpedAudioClip(string filePath, double startPosition, double length, int trackIndex, int sampleRate, double bpm)
        : base(filePath, startPosition, length, trackIndex)
    {
        // Calculate total samples from length
        double seconds = length * 60.0 / bpm;
        long totalSamples = (long)(seconds * sampleRate);

        _warpProcessor = new AudioWarpProcessor(sampleRate, bpm, totalSamples);
    }

    /// <summary>
    /// Creates a new warped audio clip from an existing AudioClip.
    /// </summary>
    /// <param name="sourceClip">Source audio clip to convert.</param>
    /// <param name="sampleRate">Audio sample rate.</param>
    /// <param name="bpm">Project tempo in BPM.</param>
    public WarpedAudioClip(AudioClip sourceClip, int sampleRate, double bpm)
        : base(sourceClip.FilePath, sourceClip.StartPosition, sourceClip.Length, sourceClip.TrackIndex)
    {
        // Copy properties from source
        Name = sourceClip.Name;
        OriginalLength = sourceClip.OriginalLength;
        SourceOffset = sourceClip.SourceOffset;
        FadeInDuration = sourceClip.FadeInDuration;
        FadeInType = sourceClip.FadeInType;
        FadeOutDuration = sourceClip.FadeOutDuration;
        FadeOutType = sourceClip.FadeOutType;
        GainDb = sourceClip.GainDb;
        IsMuted = sourceClip.IsMuted;
        IsLocked = sourceClip.IsLocked;
        Color = sourceClip.Color;
        TimeStretchFactor = sourceClip.TimeStretchFactor;
        PitchShiftSemitones = sourceClip.PitchShiftSemitones;
        IsReversed = sourceClip.IsReversed;

        // Calculate total samples from length
        double seconds = sourceClip.Length * 60.0 / bpm;
        long totalSamples = (long)(seconds * sampleRate);

        _warpProcessor = new AudioWarpProcessor(sampleRate, bpm, totalSamples);
    }

    /// <summary>
    /// Sets up the warp processor with audio properties.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate.</param>
    /// <param name="bpm">Project tempo in BPM.</param>
    /// <param name="totalSamples">Total length of audio in samples.</param>
    public void InitializeWarp(int sampleRate, double bpm, long totalSamples)
    {
        _warpProcessor.SampleRate = sampleRate;
        _warpProcessor.Bpm = bpm;
        _warpProcessor.TotalOriginalSamples = totalSamples;
    }

    /// <summary>
    /// Adds a warp marker at the specified positions.
    /// </summary>
    /// <param name="originalPositionSamples">Position in original audio (samples).</param>
    /// <param name="warpedPositionSamples">Position in warped output (samples).</param>
    /// <param name="markerType">Type of marker.</param>
    /// <returns>The created marker.</returns>
    public WarpMarker AddWarpMarker(long originalPositionSamples, long warpedPositionSamples, WarpMarkerType markerType = WarpMarkerType.User)
    {
        var marker = _warpProcessor.AddMarker(originalPositionSamples, warpedPositionSamples, markerType);
        Touch();
        return marker;
    }

    /// <summary>
    /// Adds a warp marker at the specified positions in beats.
    /// </summary>
    /// <param name="originalPositionBeats">Position in original audio (beats).</param>
    /// <param name="warpedPositionBeats">Position in warped output (beats).</param>
    /// <param name="markerType">Type of marker.</param>
    /// <returns>The created marker.</returns>
    public WarpMarker AddWarpMarkerAtBeats(double originalPositionBeats, double warpedPositionBeats, WarpMarkerType markerType = WarpMarkerType.User)
    {
        var marker = _warpProcessor.AddMarkerAtBeats(originalPositionBeats, warpedPositionBeats, markerType);
        Touch();
        return marker;
    }

    /// <summary>
    /// Removes a warp marker.
    /// </summary>
    /// <param name="marker">The marker to remove.</param>
    /// <returns>True if the marker was removed.</returns>
    public bool RemoveWarpMarker(WarpMarker marker)
    {
        bool result = _warpProcessor.RemoveMarker(marker);
        if (result) Touch();
        return result;
    }

    /// <summary>
    /// Removes a warp marker by ID.
    /// </summary>
    /// <param name="markerId">ID of the marker to remove.</param>
    /// <returns>True if the marker was removed.</returns>
    public bool RemoveWarpMarker(Guid markerId)
    {
        bool result = _warpProcessor.RemoveMarker(markerId);
        if (result) Touch();
        return result;
    }

    /// <summary>
    /// Moves a warp marker to a new warped position.
    /// </summary>
    /// <param name="marker">The marker to move.</param>
    /// <param name="newWarpedPositionSamples">New warped position in samples.</param>
    /// <returns>True if the marker was moved.</returns>
    public bool MoveWarpMarker(WarpMarker marker, long newWarpedPositionSamples)
    {
        bool result = _warpProcessor.MoveMarker(marker, newWarpedPositionSamples);
        if (result) Touch();
        return result;
    }

    /// <summary>
    /// Detects transients in the audio and creates warp markers.
    /// </summary>
    /// <param name="audioSamples">Mono audio samples to analyze.</param>
    /// <returns>List of created transient markers.</returns>
    public List<WarpMarker> DetectTransients(float[] audioSamples)
    {
        var markers = _warpProcessor.DetectTransients(audioSamples);
        if (markers.Count > 0) Touch();
        return markers;
    }

    /// <summary>
    /// Quantizes all warp markers to the beat grid.
    /// </summary>
    /// <param name="gridResolution">Grid resolution in beats (e.g., 0.25 for 16th notes).</param>
    public void QuantizeWarpToGrid(double gridResolution = 0.25)
    {
        _warpProcessor.QuantizeToGrid(gridResolution);
        Touch();
    }

    /// <summary>
    /// Resets all warp markers to their original positions.
    /// </summary>
    public void ResetWarp()
    {
        _warpProcessor.ResetWarp();
        Touch();
    }

    /// <summary>
    /// Clears all user-placed warp markers.
    /// </summary>
    public void ClearUserWarpMarkers()
    {
        _warpProcessor.ClearUserMarkers();
        Touch();
    }

    /// <summary>
    /// Clears all transient-based warp markers.
    /// </summary>
    public void ClearTransientWarpMarkers()
    {
        _warpProcessor.ClearTransientMarkers();
        Touch();
    }

    /// <summary>
    /// Gets the source position for a given timeline position, accounting for warping.
    /// </summary>
    /// <param name="timelinePosition">Position in the timeline (beats).</param>
    /// <returns>Position in source audio (beats), or -1 if outside clip.</returns>
    public new double GetSourcePosition(double timelinePosition)
    {
        if (!ContainsPosition(timelinePosition))
            return -1;

        if (!_warpEnabled)
            return base.GetSourcePosition(timelinePosition);

        // Convert timeline position to samples
        double clipOffset = timelinePosition - StartPosition;
        double clipOffsetSeconds = clipOffset * 60.0 / _warpProcessor.Bpm;
        long clipOffsetSamples = (long)(clipOffsetSeconds * _warpProcessor.SampleRate);

        // Map through warp processor
        long sourcePositionSamples = _warpProcessor.WarpedToOriginal(clipOffsetSamples);

        // Convert back to beats
        double sourcePositionSeconds = (double)sourcePositionSamples / _warpProcessor.SampleRate;
        return SourceOffset + (sourcePositionSeconds * _warpProcessor.Bpm / 60.0);
    }

    /// <summary>
    /// Gets the warped position for a given original position.
    /// </summary>
    /// <param name="originalBeats">Position in original audio (beats from start).</param>
    /// <returns>Warped position (beats from start).</returns>
    public double GetWarpedPosition(double originalBeats)
    {
        if (!_warpEnabled)
            return originalBeats;

        double originalSeconds = originalBeats * 60.0 / _warpProcessor.Bpm;
        long originalSamples = (long)(originalSeconds * _warpProcessor.SampleRate);

        long warpedSamples = _warpProcessor.OriginalToWarped(originalSamples);

        double warpedSeconds = (double)warpedSamples / _warpProcessor.SampleRate;
        return warpedSeconds * _warpProcessor.Bpm / 60.0;
    }

    /// <summary>
    /// Gets the stretch ratio at a specific position in the clip.
    /// </summary>
    /// <param name="positionInClip">Position within the clip (beats from clip start).</param>
    /// <returns>Stretch ratio at the position.</returns>
    public double GetStretchRatioAt(double positionInClip)
    {
        if (!_warpEnabled)
            return 1.0;

        double positionSeconds = positionInClip * 60.0 / _warpProcessor.Bpm;
        long positionSamples = (long)(positionSeconds * _warpProcessor.SampleRate);

        return _warpProcessor.GetStretchRatioAt(positionSamples);
    }

    /// <summary>
    /// Calculates the fade gain at a position, accounting for warped length.
    /// </summary>
    /// <param name="positionInClip">Position within the clip (0 to Length).</param>
    /// <returns>Gain multiplier (0 to 1).</returns>
    public new float GetFadeGainAt(double positionInClip)
    {
        double effectiveLength = Length;

        if (positionInClip < 0 || positionInClip > effectiveLength)
            return 0f;

        float fadeGain = 1f;

        // Apply fade-in
        if (FadeInDuration > 0 && positionInClip < FadeInDuration)
        {
            var t = positionInClip / FadeInDuration;
            fadeGain *= CalculateFadeCurve(t, FadeInType);
        }

        // Apply fade-out
        var fadeOutStart = effectiveLength - FadeOutDuration;
        if (FadeOutDuration > 0 && positionInClip > fadeOutStart)
        {
            var t = (positionInClip - fadeOutStart) / FadeOutDuration;
            fadeGain *= CalculateFadeCurve(1 - t, FadeOutType);
        }

        return fadeGain;
    }

    /// <summary>
    /// Calculates the fade curve value for a given normalized position.
    /// </summary>
    private static float CalculateFadeCurve(double t, FadeType type)
    {
        t = Math.Clamp(t, 0, 1);

        return type switch
        {
            FadeType.Linear => (float)t,
            FadeType.Exponential => (float)(t * t),
            FadeType.Logarithmic => (float)Math.Sqrt(t),
            FadeType.SCurve => (float)(t * t * (3 - 2 * t)),
            FadeType.EqualPower => (float)Math.Sin(t * Math.PI / 2),
            _ => (float)t
        };
    }

    /// <summary>
    /// Processes audio samples with warping applied.
    /// </summary>
    /// <param name="inputSamples">Original audio samples.</param>
    /// <param name="outputBuffer">Output buffer for warped samples.</param>
    /// <param name="startWarpedSample">Starting position in warped output.</param>
    /// <param name="channels">Number of audio channels.</param>
    /// <returns>Number of samples written.</returns>
    public int ProcessWarped(float[] inputSamples, float[] outputBuffer, long startWarpedSample, int channels = 2)
    {
        if (!_warpEnabled)
        {
            // No warping, just copy
            int copyCount = Math.Min(inputSamples.Length, outputBuffer.Length);
            Array.Copy(inputSamples, outputBuffer, copyCount);
            return copyCount;
        }

        return _warpProcessor.Process(inputSamples, outputBuffer, startWarpedSample, channels);
    }

    /// <summary>
    /// Creates a duplicate of this warped clip.
    /// </summary>
    /// <param name="newStartPosition">Start position for the duplicate.</param>
    /// <returns>A new warped clip with the same properties.</returns>
    public new WarpedAudioClip Duplicate(double? newStartPosition = null)
    {
        var duplicate = new WarpedAudioClip
        {
            FilePath = FilePath,
            Name = Name + " (copy)",
            StartPosition = newStartPosition ?? EndPosition,
            OriginalLength = OriginalLength,
            SourceOffset = SourceOffset,
            TrackIndex = TrackIndex,
            FadeInDuration = FadeInDuration,
            FadeInType = FadeInType,
            FadeOutDuration = FadeOutDuration,
            FadeOutType = FadeOutType,
            GainDb = GainDb,
            Color = Color,
            TimeStretchFactor = TimeStretchFactor,
            PitchShiftSemitones = PitchShiftSemitones,
            IsReversed = IsReversed,
            WarpEnabled = WarpEnabled
        };

        // Copy warp markers
        duplicate.InitializeWarp(
            _warpProcessor.SampleRate,
            _warpProcessor.Bpm,
            _warpProcessor.TotalOriginalSamples);

        foreach (var marker in WarpMarkers)
        {
            if (marker.MarkerType != WarpMarkerType.Start && marker.MarkerType != WarpMarkerType.End)
            {
                duplicate.AddWarpMarker(
                    marker.OriginalPositionSamples,
                    marker.WarpedPositionSamples,
                    marker.MarkerType);
            }
        }

        // Set the base Length property
        duplicate.Length = base.Length;

        return duplicate;
    }

    /// <summary>
    /// Splits this warped clip at the specified position.
    /// </summary>
    /// <param name="splitPosition">Position to split at (in timeline beats).</param>
    /// <returns>The new clip created after the split point, or null if split is invalid.</returns>
    public new WarpedAudioClip? Split(double splitPosition)
    {
        if (IsLocked) return null;
        if (!ContainsPosition(splitPosition)) return null;
        if (splitPosition <= StartPosition || splitPosition >= EndPosition) return null;

        var splitOffset = splitPosition - StartPosition;

        // Get the corresponding original position
        double originalPosition = GetSourcePosition(splitPosition);
        if (originalPosition < 0) return null;

        // Create new clip for the second part
        var newClip = new WarpedAudioClip
        {
            FilePath = FilePath,
            Name = Name + " (split)",
            StartPosition = splitPosition,
            OriginalLength = OriginalLength,
            SourceOffset = originalPosition,
            TrackIndex = TrackIndex,
            GainDb = GainDb,
            Color = Color,
            TimeStretchFactor = TimeStretchFactor,
            PitchShiftSemitones = PitchShiftSemitones,
            IsReversed = IsReversed,
            WarpEnabled = WarpEnabled,
            FadeOutDuration = FadeOutDuration,
            FadeOutType = FadeOutType
        };

        // Calculate new lengths
        newClip.Length = Length - splitOffset;

        // Initialize warp for new clip
        long splitSampleOffset = (long)(originalPosition * 60.0 / _warpProcessor.Bpm * _warpProcessor.SampleRate);
        long remainingSamples = _warpProcessor.TotalOriginalSamples - splitSampleOffset;

        newClip.InitializeWarp(_warpProcessor.SampleRate, _warpProcessor.Bpm, remainingSamples);

        // Copy relevant warp markers to new clip (those after split point)
        foreach (var marker in WarpMarkers)
        {
            if (marker.OriginalPositionSamples > splitSampleOffset &&
                marker.MarkerType != WarpMarkerType.Start &&
                marker.MarkerType != WarpMarkerType.End)
            {
                newClip.AddWarpMarker(
                    marker.OriginalPositionSamples - splitSampleOffset,
                    marker.WarpedPositionSamples - splitSampleOffset,
                    marker.MarkerType);
            }
        }

        // Adjust this clip
        base.Length = splitOffset;
        FadeOutDuration = 0;

        // Remove warp markers after split point from this clip
        var markersToRemove = WarpMarkers
            .Where(m => m.OriginalPositionSamples > splitSampleOffset &&
                       m.MarkerType != WarpMarkerType.Start &&
                       m.MarkerType != WarpMarkerType.End)
            .ToList();

        foreach (var marker in markersToRemove)
        {
            RemoveWarpMarker(marker);
        }

        Touch();

        return newClip;
    }

    public override string ToString() =>
        $"[WarpedAudio] {Name} @{StartPosition:F2} ({Length:F2} beats, {WarpMarkerCount} markers) Track {TrackIndex}";
}
