//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Represents an audio clip in the arrangement timeline.

using System;
using MusicEngine.Core.Analysis;
using MusicEngine.Core.Warp;

using WarpMarker = MusicEngine.Core.Warp.WarpMarker;
using WarpMarkerType = MusicEngine.Core.Warp.WarpMarkerType;

namespace MusicEngine.Core;

/// <summary>
/// Defines the type of fade curve.
/// </summary>
public enum FadeType
{
    /// <summary>Linear fade (constant rate).</summary>
    Linear,

    /// <summary>Exponential fade (fast start, slow end).</summary>
    Exponential,

    /// <summary>Logarithmic fade (slow start, fast end).</summary>
    Logarithmic,

    /// <summary>S-Curve fade (slow start, fast middle, slow end).</summary>
    SCurve,

    /// <summary>Equal power crossfade curve.</summary>
    EqualPower
}

/// <summary>
/// Represents an audio clip in the arrangement timeline.
/// Audio clips reference audio files and can be positioned, trimmed, and faded.
/// </summary>
public class AudioClip : IEquatable<AudioClip>
{
    /// <summary>Unique identifier for this clip.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Path to the audio file.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Display name of the clip.</summary>
    public string Name { get; set; } = "Audio Clip";

    /// <summary>Start position in the timeline (in beats).</summary>
    public double StartPosition { get; set; }

    /// <summary>Length of the clip in beats (after trimming).</summary>
    public double Length { get; set; }

    /// <summary>Original length of the source audio in beats.</summary>
    public double OriginalLength { get; set; }

    /// <summary>Offset into the source audio (for trimming start, in beats).</summary>
    public double SourceOffset { get; set; }

    /// <summary>Index of the track this clip belongs to.</summary>
    public int TrackIndex { get; set; }

    /// <summary>Fade-in duration in beats.</summary>
    public double FadeInDuration { get; set; }

    /// <summary>Fade-in curve type.</summary>
    public FadeType FadeInType { get; set; } = FadeType.Linear;

    /// <summary>Fade-out duration in beats.</summary>
    public double FadeOutDuration { get; set; }

    /// <summary>Fade-out curve type.</summary>
    public FadeType FadeOutType { get; set; } = FadeType.Linear;

    /// <summary>Gain adjustment in dB (-inf to +12).</summary>
    public float GainDb { get; set; }

    /// <summary>Linear gain multiplier (calculated from GainDb).</summary>
    public float Gain => GainDb <= -96f ? 0f : (float)Math.Pow(10, GainDb / 20.0);

    /// <summary>Whether this clip is muted.</summary>
    public bool IsMuted { get; set; }

    /// <summary>Whether this clip is locked from editing.</summary>
    public bool IsLocked { get; set; }

    /// <summary>Whether this clip is selected in the UI.</summary>
    public bool IsSelected { get; set; }

    /// <summary>Color for visual representation (hex format).</summary>
    public string Color { get; set; } = "#3498DB";

    /// <summary>Time stretch factor (1.0 = original speed).</summary>
    public double TimeStretchFactor { get; set; } = 1.0;

    /// <summary>Pitch shift in semitones.</summary>
    public double PitchShiftSemitones { get; set; }

    /// <summary>Whether the clip audio is reversed.</summary>
    public bool IsReversed { get; set; }

    /// <summary>Whether warp/time-stretch is enabled.</summary>
    public bool IsWarpEnabled { get; set; }

    /// <summary>The audio warp processor for elastic audio support.</summary>
    public AudioWarpProcessor? WarpProcessor { get; private set; }

    /// <summary>Detected BPM of the original audio (set by tempo detection).</summary>
    public double? DetectedBpm { get; set; }

    /// <summary>Original BPM (user-set or detected).</summary>
    public double OriginalBpm { get; set; } = 120.0;

    /// <summary>Audio sample rate for warp calculations.</summary>
    public int SampleRate { get; set; } = 44100;

    /// <summary>Number of audio channels.</summary>
    public int Channels { get; set; } = 2;

    /// <summary>Total number of samples in the audio file.</summary>
    public long TotalSamples { get; set; }

    /// <summary>Raw audio data for processing (mono or interleaved stereo).</summary>
    public float[]? AudioData { get; set; }

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    /// <summary>Last modification timestamp.</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the end position of this clip in the timeline.
    /// </summary>
    public double EndPosition => StartPosition + Length;

    /// <summary>
    /// Creates a new audio clip.
    /// </summary>
    public AudioClip()
    {
    }

    /// <summary>
    /// Creates a new audio clip with file path and position.
    /// </summary>
    /// <param name="filePath">Path to the audio file.</param>
    /// <param name="startPosition">Start position in beats.</param>
    /// <param name="length">Length in beats.</param>
    /// <param name="trackIndex">Track index.</param>
    public AudioClip(string filePath, double startPosition, double length, int trackIndex = 0)
    {
        FilePath = filePath;
        Name = System.IO.Path.GetFileNameWithoutExtension(filePath);
        StartPosition = startPosition;
        Length = length;
        OriginalLength = length;
        TrackIndex = trackIndex;
    }

    /// <summary>
    /// Checks if a given position falls within this clip.
    /// </summary>
    /// <param name="position">Position in beats to check.</param>
    /// <returns>True if the position is within the clip.</returns>
    public bool ContainsPosition(double position)
    {
        return position >= StartPosition && position < EndPosition;
    }

    /// <summary>
    /// Gets the position within the source audio for a given timeline position.
    /// </summary>
    /// <param name="timelinePosition">Position in the timeline (in beats).</param>
    /// <returns>Position in the source audio (in beats), or -1 if outside clip.</returns>
    public double GetSourcePosition(double timelinePosition)
    {
        if (!ContainsPosition(timelinePosition))
            return -1;

        var clipOffset = timelinePosition - StartPosition;
        return SourceOffset + (clipOffset / TimeStretchFactor);
    }

    /// <summary>
    /// Calculates the fade gain at a given position within the clip.
    /// </summary>
    /// <param name="positionInClip">Position within the clip (0 to Length).</param>
    /// <returns>Gain multiplier (0 to 1).</returns>
    public float GetFadeGainAt(double positionInClip)
    {
        if (positionInClip < 0 || positionInClip > Length)
            return 0f;

        float fadeGain = 1f;

        // Apply fade-in
        if (FadeInDuration > 0 && positionInClip < FadeInDuration)
        {
            var t = positionInClip / FadeInDuration;
            fadeGain *= CalculateFadeCurve(t, FadeInType);
        }

        // Apply fade-out
        var fadeOutStart = Length - FadeOutDuration;
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
    /// Moves the clip to a new position.
    /// </summary>
    /// <param name="newStartPosition">New start position in beats.</param>
    public void MoveTo(double newStartPosition)
    {
        if (IsLocked) return;
        StartPosition = newStartPosition;
        Touch();
    }

    /// <summary>
    /// Trims the clip from the start.
    /// </summary>
    /// <param name="trimAmount">Amount to trim in beats.</param>
    public void TrimStart(double trimAmount)
    {
        if (IsLocked) return;
        if (trimAmount <= 0 || trimAmount >= Length) return;

        StartPosition += trimAmount;
        SourceOffset += trimAmount / TimeStretchFactor;
        Length -= trimAmount;
        Touch();
    }

    /// <summary>
    /// Trims the clip from the end.
    /// </summary>
    /// <param name="trimAmount">Amount to trim in beats.</param>
    public void TrimEnd(double trimAmount)
    {
        if (IsLocked) return;
        if (trimAmount <= 0 || trimAmount >= Length) return;

        Length -= trimAmount;
        Touch();
    }

    /// <summary>
    /// Sets the fade-in parameters.
    /// </summary>
    /// <param name="duration">Fade duration in beats.</param>
    /// <param name="type">Fade curve type.</param>
    public void SetFadeIn(double duration, FadeType type = FadeType.Linear)
    {
        FadeInDuration = Math.Max(0, Math.Min(duration, Length - FadeOutDuration));
        FadeInType = type;
        Touch();
    }

    /// <summary>
    /// Sets the fade-out parameters.
    /// </summary>
    /// <param name="duration">Fade duration in beats.</param>
    /// <param name="type">Fade curve type.</param>
    public void SetFadeOut(double duration, FadeType type = FadeType.Linear)
    {
        FadeOutDuration = Math.Max(0, Math.Min(duration, Length - FadeInDuration));
        FadeOutType = type;
        Touch();
    }

    /// <summary>
    /// Splits this clip at the specified position.
    /// </summary>
    /// <param name="splitPosition">Position to split at (in timeline beats).</param>
    /// <returns>The new clip created after the split point, or null if split is invalid.</returns>
    public AudioClip? Split(double splitPosition)
    {
        if (IsLocked) return null;
        if (!ContainsPosition(splitPosition)) return null;
        if (splitPosition <= StartPosition || splitPosition >= EndPosition) return null;

        var splitOffset = splitPosition - StartPosition;
        var newClipLength = Length - splitOffset;

        // Create new clip for the second part
        var newClip = new AudioClip
        {
            FilePath = FilePath,
            Name = Name + " (split)",
            StartPosition = splitPosition,
            Length = newClipLength,
            OriginalLength = OriginalLength,
            SourceOffset = SourceOffset + (splitOffset / TimeStretchFactor),
            TrackIndex = TrackIndex,
            GainDb = GainDb,
            Color = Color,
            TimeStretchFactor = TimeStretchFactor,
            PitchShiftSemitones = PitchShiftSemitones,
            IsReversed = IsReversed,
            IsWarpEnabled = IsWarpEnabled,
            FadeOutDuration = FadeOutDuration,
            FadeOutType = FadeOutType,
            DetectedBpm = DetectedBpm,
            OriginalBpm = OriginalBpm,
            SampleRate = SampleRate,
            Channels = Channels,
            TotalSamples = TotalSamples,
            AudioData = AudioData
        };

        // Handle warp processor split
        if (WarpProcessor != null && IsWarpEnabled)
        {
            // Calculate split position in samples
            double splitSeconds = splitOffset * 60.0 / WarpProcessor.Bpm;
            long splitSampleOffset = (long)(splitSeconds * SampleRate);
            long remainingSamples = TotalSamples - splitSampleOffset;

            // Initialize warp for new clip
            newClip.EnableWarping(false);
            if (newClip.WarpProcessor != null)
            {
                newClip.WarpProcessor.Bpm = WarpProcessor.Bpm;
                newClip.WarpProcessor.TotalOriginalSamples = remainingSamples;

                // Copy markers that are after split point
                foreach (var marker in WarpProcessor.Markers)
                {
                    if (marker.OriginalPositionSamples > splitSampleOffset &&
                        marker.MarkerType != WarpMarkerType.Start &&
                        marker.MarkerType != WarpMarkerType.End)
                    {
                        newClip.WarpProcessor.AddMarker(
                            marker.OriginalPositionSamples - splitSampleOffset,
                            marker.WarpedPositionSamples - splitSampleOffset,
                            marker.MarkerType);
                    }
                }
            }
        }

        // Adjust this clip
        Length = splitOffset;
        FadeOutDuration = 0;
        Touch();

        return newClip;
    }

    /// <summary>
    /// Creates a duplicate of this clip at a new position.
    /// </summary>
    /// <param name="newStartPosition">Start position for the duplicate.</param>
    /// <returns>A new clip with the same properties.</returns>
    public AudioClip Duplicate(double? newStartPosition = null)
    {
        var duplicate = new AudioClip
        {
            FilePath = FilePath,
            Name = Name + " (copy)",
            StartPosition = newStartPosition ?? EndPosition,
            Length = Length,
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
            IsWarpEnabled = IsWarpEnabled,
            DetectedBpm = DetectedBpm,
            OriginalBpm = OriginalBpm,
            SampleRate = SampleRate,
            Channels = Channels,
            TotalSamples = TotalSamples,
            AudioData = AudioData // Share reference to audio data
        };

        // Copy warp processor state if exists
        if (WarpProcessor != null && IsWarpEnabled)
        {
            duplicate.EnableWarping(false);
            if (duplicate.WarpProcessor != null)
            {
                duplicate.WarpProcessor.Bpm = WarpProcessor.Bpm;
                // Copy warp markers
                foreach (var marker in WarpProcessor.Markers)
                {
                    if (marker.MarkerType != WarpMarkerType.Start && marker.MarkerType != WarpMarkerType.End)
                    {
                        duplicate.WarpProcessor.AddMarker(
                            marker.OriginalPositionSamples,
                            marker.WarpedPositionSamples,
                            marker.MarkerType);
                    }
                }
            }
        }

        return duplicate;
    }

    /// <summary>
    /// Updates the modification timestamp.
    /// </summary>
    public void Touch()
    {
        ModifiedAt = DateTime.UtcNow;
    }

    #region Warping Methods

    /// <summary>
    /// Enables warping for this clip with optional auto-detection of transients.
    /// Creates a WarpProcessor if one does not exist.
    /// </summary>
    /// <param name="autoDetectTransients">Whether to auto-detect transients for warp markers.</param>
    public void EnableWarping(bool autoDetectTransients = true)
    {
        if (WarpProcessor == null)
        {
            WarpProcessor = new AudioWarpProcessor(SampleRate, OriginalBpm, TotalSamples);

            if (autoDetectTransients && AudioData != null)
            {
                // Convert to mono for transient detection if stereo
                float[] monoData;
                if (Channels == 2)
                {
                    monoData = new float[AudioData.Length / 2];
                    for (int i = 0; i < monoData.Length; i++)
                    {
                        monoData[i] = (AudioData[i * 2] + AudioData[i * 2 + 1]) * 0.5f;
                    }
                }
                else
                {
                    monoData = AudioData;
                }

                WarpProcessor.DetectTransients(monoData);
            }
        }

        IsWarpEnabled = true;
        Touch();
    }

    /// <summary>
    /// Disables warping (reverts to original audio playback).
    /// </summary>
    public void DisableWarping()
    {
        IsWarpEnabled = false;
        Touch();
    }

    /// <summary>
    /// Gets the stretch ratio at a specific position in the clip.
    /// </summary>
    /// <param name="positionSamples">Position in warped output (samples).</param>
    /// <returns>Stretch ratio (1.0 = no stretch, 0.5 = half speed, 2.0 = double speed).</returns>
    public double GetStretchRatioAt(long positionSamples)
    {
        if (WarpProcessor == null || !IsWarpEnabled)
            return 1.0;

        return WarpProcessor.GetStretchRatioAt(positionSamples);
    }

    /// <summary>
    /// Quantizes the audio to match the project tempo using warp markers.
    /// </summary>
    /// <param name="projectBpm">Target project tempo in BPM.</param>
    /// <param name="gridResolution">Grid resolution in beats (default: 0.25 for 16th notes).</param>
    public void QuantizeToTempo(double projectBpm, double gridResolution = 0.25)
    {
        if (WarpProcessor == null)
        {
            EnableWarping(true);
        }

        if (WarpProcessor != null)
        {
            WarpProcessor.Bpm = projectBpm;
            WarpProcessor.QuantizeToGrid(gridResolution);
        }

        Touch();
    }

    /// <summary>
    /// Adds a warp marker at the specified position.
    /// </summary>
    /// <param name="originalPositionSamples">Position in original audio (samples).</param>
    /// <param name="warpedPositionSamples">Position in warped output (samples).</param>
    /// <param name="markerType">Type of warp marker.</param>
    /// <returns>The created warp marker, or null if warping is not enabled.</returns>
    public Warp.WarpMarker? AddWarpMarker(long originalPositionSamples, long warpedPositionSamples, WarpMarkerType markerType = WarpMarkerType.User)
    {
        if (WarpProcessor == null)
        {
            EnableWarping(false);
        }

        var marker = WarpProcessor?.AddMarker(originalPositionSamples, warpedPositionSamples, markerType);
        if (marker != null)
        {
            Touch();
        }
        return marker;
    }

    /// <summary>
    /// Removes a warp marker.
    /// </summary>
    /// <param name="marker">The marker to remove.</param>
    /// <returns>True if the marker was removed.</returns>
    public bool RemoveWarpMarker(Warp.WarpMarker marker)
    {
        if (WarpProcessor == null)
            return false;

        bool result = WarpProcessor.RemoveMarker(marker);
        if (result)
        {
            Touch();
        }
        return result;
    }

    /// <summary>
    /// Clears all user-placed warp markers.
    /// </summary>
    public void ClearUserWarpMarkers()
    {
        WarpProcessor?.ClearUserMarkers();
        Touch();
    }

    /// <summary>
    /// Resets all warp markers to their original positions (no warping effect).
    /// </summary>
    public void ResetWarp()
    {
        WarpProcessor?.ResetWarp();
        Touch();
    }

    /// <summary>
    /// Detects the tempo of the audio and sets DetectedBpm.
    /// </summary>
    /// <returns>The detected BPM, or null if detection failed.</returns>
    public double? DetectTempo()
    {
        if (AudioData == null || AudioData.Length == 0)
            return null;

        // Convert to mono for tempo detection if stereo
        float[] monoData;
        if (Channels == 2)
        {
            monoData = new float[AudioData.Length / 2];
            for (int i = 0; i < monoData.Length; i++)
            {
                monoData[i] = (AudioData[i * 2] + AudioData[i * 2 + 1]) * 0.5f;
            }
        }
        else
        {
            monoData = AudioData;
        }

        var detector = new TempoDetector(SampleRate);
        var result = detector.AnalyzeBuffer(monoData, SampleRate);

        if (result.Confidence > 0.3)
        {
            DetectedBpm = result.DetectedBpm;
            OriginalBpm = result.DetectedBpm;
            return result.DetectedBpm;
        }

        return null;
    }

    /// <summary>
    /// Maps a warped (output) position back to its original position in the source audio.
    /// </summary>
    /// <param name="warpedPositionSamples">Position in warped output (samples).</param>
    /// <returns>Corresponding position in original audio (samples).</returns>
    public long WarpedToOriginal(long warpedPositionSamples)
    {
        if (WarpProcessor == null || !IsWarpEnabled)
            return warpedPositionSamples;

        return WarpProcessor.WarpedToOriginal(warpedPositionSamples);
    }

    /// <summary>
    /// Maps an original position to its warped (output) position.
    /// </summary>
    /// <param name="originalPositionSamples">Position in original audio (samples).</param>
    /// <returns>Corresponding position in warped output (samples).</returns>
    public long OriginalToWarped(long originalPositionSamples)
    {
        if (WarpProcessor == null || !IsWarpEnabled)
            return originalPositionSamples;

        return WarpProcessor.OriginalToWarped(originalPositionSamples);
    }

    #endregion

    public bool Equals(AudioClip? other)
    {
        if (other is null) return false;
        return Id.Equals(other.Id);
    }

    public override bool Equals(object? obj) => obj is AudioClip other && Equals(other);

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(AudioClip? left, AudioClip? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(AudioClip? left, AudioClip? right) => !(left == right);

    public override string ToString() =>
        $"[Audio] {Name} @{StartPosition:F2} ({Length:F2} beats) Track {TrackIndex}";
}
