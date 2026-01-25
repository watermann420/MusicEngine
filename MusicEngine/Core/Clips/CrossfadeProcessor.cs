//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Processes audio crossfades between overlapping clips with multiple curve types.

using System;

namespace MusicEngine.Core.Clips;

/// <summary>
/// Types of crossfade curves supported.
/// </summary>
public enum CrossfadeType
{
    /// <summary>Linear crossfade (constant rate).</summary>
    Linear,

    /// <summary>Equal power crossfade (maintains constant loudness).</summary>
    EqualPower,

    /// <summary>S-Curve crossfade (smooth transition with eased start and end).</summary>
    SCurve,

    /// <summary>Logarithmic crossfade (slow start, fast end for outgoing clip).</summary>
    Logarithmic
}

/// <summary>
/// Represents a crossfade region between two audio clips.
/// </summary>
public class CrossfadeRegion
{
    /// <summary>Unique identifier for this crossfade.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Start position of the crossfade in beats.</summary>
    public double StartPosition { get; set; }

    /// <summary>End position of the crossfade in beats.</summary>
    public double EndPosition { get; set; }

    /// <summary>Type of crossfade curve to apply.</summary>
    public CrossfadeType Type { get; set; } = CrossfadeType.EqualPower;

    /// <summary>ID of the outgoing (fading out) audio clip.</summary>
    public Guid OutgoingClipId { get; set; }

    /// <summary>ID of the incoming (fading in) audio clip.</summary>
    public Guid IncomingClipId { get; set; }

    /// <summary>Track index where the crossfade occurs.</summary>
    public int TrackIndex { get; set; }

    /// <summary>Whether this crossfade is locked from editing.</summary>
    public bool IsLocked { get; set; }

    /// <summary>Whether this crossfade is enabled.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets the length of the crossfade in beats.
    /// </summary>
    public double Length => EndPosition - StartPosition;

    /// <summary>
    /// Creates a new crossfade region.
    /// </summary>
    public CrossfadeRegion()
    {
    }

    /// <summary>
    /// Creates a new crossfade region with specified parameters.
    /// </summary>
    /// <param name="startPosition">Start position in beats.</param>
    /// <param name="endPosition">End position in beats.</param>
    /// <param name="outgoingClipId">ID of the outgoing clip.</param>
    /// <param name="incomingClipId">ID of the incoming clip.</param>
    /// <param name="type">Crossfade curve type.</param>
    public CrossfadeRegion(double startPosition, double endPosition, Guid outgoingClipId, Guid incomingClipId, CrossfadeType type = CrossfadeType.EqualPower)
    {
        StartPosition = startPosition;
        EndPosition = endPosition;
        OutgoingClipId = outgoingClipId;
        IncomingClipId = incomingClipId;
        Type = type;
    }

    /// <summary>
    /// Checks if a position is within this crossfade region.
    /// </summary>
    /// <param name="position">Position in beats.</param>
    /// <returns>True if position is within the crossfade.</returns>
    public bool ContainsPosition(double position)
    {
        return position >= StartPosition && position < EndPosition;
    }
}

/// <summary>
/// Processes audio crossfades between overlapping clips.
/// Supports multiple crossfade curve types for smooth transitions.
/// </summary>
public class CrossfadeProcessor
{
    /// <summary>
    /// Default crossfade length in beats when auto-creating crossfades.
    /// </summary>
    public double DefaultCrossfadeLength { get; set; } = 0.25; // 1/4 beat = 1/16 note at 4/4

    /// <summary>
    /// Whether to automatically create crossfades when clips overlap.
    /// </summary>
    public bool AutoCrossfade { get; set; } = true;

    /// <summary>
    /// Default crossfade type for auto-created crossfades.
    /// </summary>
    public CrossfadeType DefaultCrossfadeType { get; set; } = CrossfadeType.EqualPower;

    /// <summary>
    /// Creates a new crossfade processor.
    /// </summary>
    public CrossfadeProcessor()
    {
    }

    /// <summary>
    /// Creates a new crossfade processor with custom settings.
    /// </summary>
    /// <param name="defaultLength">Default crossfade length in beats.</param>
    /// <param name="defaultType">Default crossfade type.</param>
    /// <param name="autoCrossfade">Whether to auto-create crossfades.</param>
    public CrossfadeProcessor(double defaultLength, CrossfadeType defaultType, bool autoCrossfade = true)
    {
        DefaultCrossfadeLength = defaultLength;
        DefaultCrossfadeType = defaultType;
        AutoCrossfade = autoCrossfade;
    }

    /// <summary>
    /// Calculates the gain for the outgoing clip at a normalized position within the crossfade.
    /// </summary>
    /// <param name="t">Normalized position (0.0 = start, 1.0 = end of crossfade).</param>
    /// <param name="type">Crossfade curve type.</param>
    /// <returns>Gain multiplier for the outgoing clip (1.0 to 0.0).</returns>
    public static float CalculateOutgoingGain(double t, CrossfadeType type)
    {
        t = Math.Clamp(t, 0.0, 1.0);

        return type switch
        {
            CrossfadeType.Linear => (float)(1.0 - t),
            CrossfadeType.EqualPower => (float)Math.Cos(t * Math.PI / 2.0),
            CrossfadeType.SCurve => (float)(1.0 - (t * t * (3.0 - 2.0 * t))),
            CrossfadeType.Logarithmic => (float)Math.Pow(1.0 - t, 2.0),
            _ => (float)(1.0 - t)
        };
    }

    /// <summary>
    /// Calculates the gain for the incoming clip at a normalized position within the crossfade.
    /// </summary>
    /// <param name="t">Normalized position (0.0 = start, 1.0 = end of crossfade).</param>
    /// <param name="type">Crossfade curve type.</param>
    /// <returns>Gain multiplier for the incoming clip (0.0 to 1.0).</returns>
    public static float CalculateIncomingGain(double t, CrossfadeType type)
    {
        t = Math.Clamp(t, 0.0, 1.0);

        return type switch
        {
            CrossfadeType.Linear => (float)t,
            CrossfadeType.EqualPower => (float)Math.Sin(t * Math.PI / 2.0),
            CrossfadeType.SCurve => (float)(t * t * (3.0 - 2.0 * t)),
            CrossfadeType.Logarithmic => (float)(1.0 - Math.Pow(1.0 - t, 2.0)),
            _ => (float)t
        };
    }

    /// <summary>
    /// Processes a crossfade between two audio sample arrays at a specific position.
    /// </summary>
    /// <param name="outgoingSamples">Audio samples from the outgoing (fading out) clip.</param>
    /// <param name="incomingSamples">Audio samples from the incoming (fading in) clip.</param>
    /// <param name="outputBuffer">Output buffer for the crossfaded audio.</param>
    /// <param name="crossfadeRegion">The crossfade region parameters.</param>
    /// <param name="sampleRate">Audio sample rate.</param>
    /// <param name="bpm">Current tempo in BPM.</param>
    /// <param name="startBeat">Starting beat position within the crossfade.</param>
    /// <param name="channels">Number of audio channels.</param>
    /// <returns>Number of samples written to the output buffer.</returns>
    public int ProcessCrossfade(
        float[] outgoingSamples,
        float[] incomingSamples,
        float[] outputBuffer,
        CrossfadeRegion crossfadeRegion,
        int sampleRate,
        double bpm,
        double startBeat,
        int channels = 2)
    {
        if (!crossfadeRegion.IsEnabled)
        {
            // If crossfade is disabled, just copy incoming samples
            Array.Copy(incomingSamples, outputBuffer, Math.Min(incomingSamples.Length, outputBuffer.Length));
            return Math.Min(incomingSamples.Length, outputBuffer.Length);
        }

        double crossfadeLength = crossfadeRegion.Length;
        if (crossfadeLength <= 0)
        {
            Array.Copy(incomingSamples, outputBuffer, Math.Min(incomingSamples.Length, outputBuffer.Length));
            return Math.Min(incomingSamples.Length, outputBuffer.Length);
        }

        // Calculate samples per beat
        double samplesPerBeat = sampleRate * 60.0 / bpm;
        int crossfadeSamples = (int)(crossfadeLength * samplesPerBeat) * channels;

        int samplesToProcess = Math.Min(Math.Min(outgoingSamples.Length, incomingSamples.Length), outputBuffer.Length);

        for (int i = 0; i < samplesToProcess; i += channels)
        {
            // Calculate current beat position
            double currentBeat = startBeat + (i / channels) / samplesPerBeat;

            // Calculate normalized position within crossfade (0.0 to 1.0)
            double t = (currentBeat - crossfadeRegion.StartPosition) / crossfadeLength;
            t = Math.Clamp(t, 0.0, 1.0);

            // Get gains for both clips
            float outgoingGain = CalculateOutgoingGain(t, crossfadeRegion.Type);
            float incomingGain = CalculateIncomingGain(t, crossfadeRegion.Type);

            // Apply crossfade to each channel
            for (int ch = 0; ch < channels && (i + ch) < samplesToProcess; ch++)
            {
                float outgoingSample = i + ch < outgoingSamples.Length ? outgoingSamples[i + ch] : 0f;
                float incomingSample = i + ch < incomingSamples.Length ? incomingSamples[i + ch] : 0f;

                outputBuffer[i + ch] = (outgoingSample * outgoingGain) + (incomingSample * incomingGain);
            }
        }

        return samplesToProcess;
    }

    /// <summary>
    /// Processes a crossfade sample by sample for real-time use.
    /// </summary>
    /// <param name="outgoingSample">Sample from the outgoing clip.</param>
    /// <param name="incomingSample">Sample from the incoming clip.</param>
    /// <param name="normalizedPosition">Position within crossfade (0.0 to 1.0).</param>
    /// <param name="type">Crossfade curve type.</param>
    /// <returns>Crossfaded sample value.</returns>
    public static float ProcessSample(float outgoingSample, float incomingSample, double normalizedPosition, CrossfadeType type)
    {
        float outgoingGain = CalculateOutgoingGain(normalizedPosition, type);
        float incomingGain = CalculateIncomingGain(normalizedPosition, type);

        return (outgoingSample * outgoingGain) + (incomingSample * incomingGain);
    }

    /// <summary>
    /// Creates a crossfade region for two overlapping clips.
    /// </summary>
    /// <param name="outgoingClip">The clip that is ending (fading out).</param>
    /// <param name="incomingClip">The clip that is starting (fading in).</param>
    /// <returns>A new crossfade region, or null if clips don't overlap.</returns>
    public CrossfadeRegion? CreateCrossfadeForClips(AudioClip outgoingClip, AudioClip incomingClip)
    {
        if (outgoingClip.TrackIndex != incomingClip.TrackIndex)
            return null;

        // Check for overlap
        double overlapStart = Math.Max(outgoingClip.StartPosition, incomingClip.StartPosition);
        double overlapEnd = Math.Min(outgoingClip.EndPosition, incomingClip.EndPosition);

        if (overlapEnd <= overlapStart)
            return null; // No overlap

        double overlapLength = overlapEnd - overlapStart;

        // Determine crossfade length (use overlap length or default, whichever is smaller)
        double crossfadeLength = Math.Min(overlapLength, DefaultCrossfadeLength);

        // Position crossfade at the start of the overlap
        double crossfadeStart = overlapStart;
        double crossfadeEnd = crossfadeStart + crossfadeLength;

        return new CrossfadeRegion(
            crossfadeStart,
            crossfadeEnd,
            outgoingClip.Id,
            incomingClip.Id,
            DefaultCrossfadeType)
        {
            TrackIndex = outgoingClip.TrackIndex
        };
    }

    /// <summary>
    /// Determines if two clips need a crossfade based on their positions.
    /// </summary>
    /// <param name="clip1">First audio clip.</param>
    /// <param name="clip2">Second audio clip.</param>
    /// <returns>True if the clips overlap and need a crossfade.</returns>
    public static bool NeedsCrossfade(AudioClip clip1, AudioClip clip2)
    {
        if (clip1.TrackIndex != clip2.TrackIndex)
            return false;

        // Check if they overlap
        return clip1.StartPosition < clip2.EndPosition && clip1.EndPosition > clip2.StartPosition;
    }

    /// <summary>
    /// Calculates the overlap region between two clips.
    /// </summary>
    /// <param name="clip1">First audio clip.</param>
    /// <param name="clip2">Second audio clip.</param>
    /// <returns>Tuple of (overlapStart, overlapEnd) or null if no overlap.</returns>
    public static (double Start, double End)? GetOverlapRegion(AudioClip clip1, AudioClip clip2)
    {
        if (!NeedsCrossfade(clip1, clip2))
            return null;

        double overlapStart = Math.Max(clip1.StartPosition, clip2.StartPosition);
        double overlapEnd = Math.Min(clip1.EndPosition, clip2.EndPosition);

        return (overlapStart, overlapEnd);
    }
}
