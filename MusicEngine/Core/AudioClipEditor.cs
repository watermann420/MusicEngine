// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio clip container and playback.

using System;

namespace MusicEngine.Core;

/// <summary>
/// Provides static and instance methods for editing <see cref="AudioClip"/> instances.
/// Operations either modify the clip directly or return a new/modified clip.
/// </summary>
public static class AudioClipEditor
{
    #region Trim Operations

    /// <summary>
    /// Trims an audio clip from the start by the specified amount.
    /// </summary>
    /// <param name="clip">The clip to trim.</param>
    /// <param name="trimAmount">Amount to trim in beats.</param>
    /// <exception cref="ArgumentNullException">Thrown when clip is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when trim amount is invalid.</exception>
    public static void TrimStart(AudioClip clip, double trimAmount)
    {
        ArgumentNullException.ThrowIfNull(clip);

        if (clip.IsLocked)
            throw new InvalidOperationException("Cannot trim a locked clip.");

        if (trimAmount <= 0)
            throw new ArgumentOutOfRangeException(nameof(trimAmount), "Trim amount must be positive.");

        if (trimAmount >= clip.Length)
            throw new ArgumentOutOfRangeException(nameof(trimAmount), "Trim amount cannot exceed clip length.");

        clip.TrimStart(trimAmount);
    }

    /// <summary>
    /// Trims an audio clip from the end by the specified amount.
    /// </summary>
    /// <param name="clip">The clip to trim.</param>
    /// <param name="trimAmount">Amount to trim in beats.</param>
    /// <exception cref="ArgumentNullException">Thrown when clip is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when trim amount is invalid.</exception>
    public static void TrimEnd(AudioClip clip, double trimAmount)
    {
        ArgumentNullException.ThrowIfNull(clip);

        if (clip.IsLocked)
            throw new InvalidOperationException("Cannot trim a locked clip.");

        if (trimAmount <= 0)
            throw new ArgumentOutOfRangeException(nameof(trimAmount), "Trim amount must be positive.");

        if (trimAmount >= clip.Length)
            throw new ArgumentOutOfRangeException(nameof(trimAmount), "Trim amount cannot exceed clip length.");

        clip.TrimEnd(trimAmount);
    }

    /// <summary>
    /// Trims an audio clip to a specific region.
    /// </summary>
    /// <param name="clip">The clip to trim.</param>
    /// <param name="startOffset">Start offset within the clip in beats.</param>
    /// <param name="newLength">New length of the clip in beats.</param>
    /// <exception cref="ArgumentNullException">Thrown when clip is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when parameters are invalid.</exception>
    public static void TrimToRegion(AudioClip clip, double startOffset, double newLength)
    {
        ArgumentNullException.ThrowIfNull(clip);

        if (clip.IsLocked)
            throw new InvalidOperationException("Cannot trim a locked clip.");

        if (startOffset < 0)
            throw new ArgumentOutOfRangeException(nameof(startOffset), "Start offset cannot be negative.");

        if (newLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(newLength), "New length must be positive.");

        if (startOffset + newLength > clip.Length)
            throw new ArgumentOutOfRangeException(nameof(newLength), "Trim region exceeds clip boundaries.");

        // Trim from start first
        if (startOffset > 0)
        {
            clip.TrimStart(startOffset);
        }

        // Then trim from end to get the desired length
        var trimFromEnd = clip.Length - newLength;
        if (trimFromEnd > 0)
        {
            clip.TrimEnd(trimFromEnd);
        }
    }

    #endregion

    #region Normalize

    /// <summary>
    /// Normalizes the clip's gain to a target peak level in dB.
    /// This adjusts the GainDb property to achieve the target peak level.
    /// </summary>
    /// <param name="clip">The clip to normalize.</param>
    /// <param name="currentPeakDb">The current peak level of the audio in dB.</param>
    /// <param name="targetPeakDb">The target peak level in dB (default: 0 dB).</param>
    /// <exception cref="ArgumentNullException">Thrown when clip is null.</exception>
    public static void Normalize(AudioClip clip, float currentPeakDb, float targetPeakDb = 0f)
    {
        ArgumentNullException.ThrowIfNull(clip);

        if (clip.IsLocked)
            throw new InvalidOperationException("Cannot normalize a locked clip.");

        // Calculate the gain adjustment needed
        var gainAdjustment = targetPeakDb - currentPeakDb;

        // Apply the adjustment to the clip's gain
        clip.GainDb += gainAdjustment;

        // Clamp to reasonable range (-96 dB to +12 dB)
        clip.GainDb = Math.Clamp(clip.GainDb, -96f, 12f);

        clip.Touch();
    }

    /// <summary>
    /// Normalizes the clip's gain based on linear amplitude values.
    /// </summary>
    /// <param name="clip">The clip to normalize.</param>
    /// <param name="currentPeakAmplitude">The current peak amplitude (0.0 to 1.0+).</param>
    /// <param name="targetPeakAmplitude">The target peak amplitude (default: 1.0).</param>
    /// <exception cref="ArgumentNullException">Thrown when clip is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when amplitude values are invalid.</exception>
    public static void NormalizeByAmplitude(AudioClip clip, float currentPeakAmplitude, float targetPeakAmplitude = 1.0f)
    {
        ArgumentNullException.ThrowIfNull(clip);

        if (currentPeakAmplitude <= 0)
            throw new ArgumentOutOfRangeException(nameof(currentPeakAmplitude), "Current peak amplitude must be positive.");

        if (targetPeakAmplitude <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetPeakAmplitude), "Target peak amplitude must be positive.");

        // Convert amplitudes to dB and normalize
        var currentPeakDb = AmplitudeToDb(currentPeakAmplitude);
        var targetPeakDb = AmplitudeToDb(targetPeakAmplitude);

        Normalize(clip, currentPeakDb, targetPeakDb);
    }

    #endregion

    #region Reverse

    /// <summary>
    /// Toggles the reverse state of an audio clip.
    /// </summary>
    /// <param name="clip">The clip to reverse.</param>
    /// <exception cref="ArgumentNullException">Thrown when clip is null.</exception>
    public static void Reverse(AudioClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);

        if (clip.IsLocked)
            throw new InvalidOperationException("Cannot reverse a locked clip.");

        clip.IsReversed = !clip.IsReversed;
        clip.Touch();
    }

    /// <summary>
    /// Sets the reverse state of an audio clip.
    /// </summary>
    /// <param name="clip">The clip to modify.</param>
    /// <param name="reversed">True to reverse the clip, false for normal playback.</param>
    /// <exception cref="ArgumentNullException">Thrown when clip is null.</exception>
    public static void SetReversed(AudioClip clip, bool reversed)
    {
        ArgumentNullException.ThrowIfNull(clip);

        if (clip.IsLocked)
            throw new InvalidOperationException("Cannot modify a locked clip.");

        if (clip.IsReversed != reversed)
        {
            clip.IsReversed = reversed;
            clip.Touch();
        }
    }

    #endregion

    #region Fade Operations

    /// <summary>
    /// Applies a fade-in to the audio clip.
    /// </summary>
    /// <param name="clip">The clip to apply the fade to.</param>
    /// <param name="duration">Fade duration in beats.</param>
    /// <param name="fadeType">The type of fade curve to use.</param>
    /// <exception cref="ArgumentNullException">Thrown when clip is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when duration is invalid.</exception>
    public static void FadeIn(AudioClip clip, double duration, FadeType fadeType = FadeType.Linear)
    {
        ArgumentNullException.ThrowIfNull(clip);

        if (clip.IsLocked)
            throw new InvalidOperationException("Cannot apply fade to a locked clip.");

        if (duration < 0)
            throw new ArgumentOutOfRangeException(nameof(duration), "Fade duration cannot be negative.");

        if (duration > clip.Length - clip.FadeOutDuration)
            throw new ArgumentOutOfRangeException(nameof(duration), "Fade-in duration would overlap with fade-out.");

        clip.SetFadeIn(duration, fadeType);
    }

    /// <summary>
    /// Applies a fade-out to the audio clip.
    /// </summary>
    /// <param name="clip">The clip to apply the fade to.</param>
    /// <param name="duration">Fade duration in beats.</param>
    /// <param name="fadeType">The type of fade curve to use.</param>
    /// <exception cref="ArgumentNullException">Thrown when clip is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when duration is invalid.</exception>
    public static void FadeOut(AudioClip clip, double duration, FadeType fadeType = FadeType.Linear)
    {
        ArgumentNullException.ThrowIfNull(clip);

        if (clip.IsLocked)
            throw new InvalidOperationException("Cannot apply fade to a locked clip.");

        if (duration < 0)
            throw new ArgumentOutOfRangeException(nameof(duration), "Fade duration cannot be negative.");

        if (duration > clip.Length - clip.FadeInDuration)
            throw new ArgumentOutOfRangeException(nameof(duration), "Fade-out duration would overlap with fade-in.");

        clip.SetFadeOut(duration, fadeType);
    }

    /// <summary>
    /// Applies both fade-in and fade-out to the audio clip.
    /// </summary>
    /// <param name="clip">The clip to apply the fades to.</param>
    /// <param name="fadeInDuration">Fade-in duration in beats.</param>
    /// <param name="fadeOutDuration">Fade-out duration in beats.</param>
    /// <param name="fadeInType">The type of fade-in curve to use.</param>
    /// <param name="fadeOutType">The type of fade-out curve to use.</param>
    /// <exception cref="ArgumentNullException">Thrown when clip is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when fade durations would overlap.</exception>
    public static void ApplyFades(
        AudioClip clip,
        double fadeInDuration,
        double fadeOutDuration,
        FadeType fadeInType = FadeType.Linear,
        FadeType fadeOutType = FadeType.Linear)
    {
        ArgumentNullException.ThrowIfNull(clip);

        if (clip.IsLocked)
            throw new InvalidOperationException("Cannot apply fades to a locked clip.");

        if (fadeInDuration < 0)
            throw new ArgumentOutOfRangeException(nameof(fadeInDuration), "Fade-in duration cannot be negative.");

        if (fadeOutDuration < 0)
            throw new ArgumentOutOfRangeException(nameof(fadeOutDuration), "Fade-out duration cannot be negative.");

        if (fadeInDuration + fadeOutDuration > clip.Length)
            throw new ArgumentOutOfRangeException(nameof(fadeInDuration), "Combined fade durations exceed clip length.");

        clip.SetFadeIn(fadeInDuration, fadeInType);
        clip.SetFadeOut(fadeOutDuration, fadeOutType);
    }

    /// <summary>
    /// Removes all fades from the audio clip.
    /// </summary>
    /// <param name="clip">The clip to remove fades from.</param>
    /// <exception cref="ArgumentNullException">Thrown when clip is null.</exception>
    public static void RemoveFades(AudioClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);

        if (clip.IsLocked)
            throw new InvalidOperationException("Cannot modify a locked clip.");

        clip.SetFadeIn(0, FadeType.Linear);
        clip.SetFadeOut(0, FadeType.Linear);
    }

    #endregion

    #region Gain Operations

    /// <summary>
    /// Sets the gain of an audio clip in decibels.
    /// </summary>
    /// <param name="clip">The clip to modify.</param>
    /// <param name="gainDb">The gain value in dB (-96 to +12).</param>
    /// <exception cref="ArgumentNullException">Thrown when clip is null.</exception>
    public static void SetGain(AudioClip clip, float gainDb)
    {
        ArgumentNullException.ThrowIfNull(clip);

        if (clip.IsLocked)
            throw new InvalidOperationException("Cannot modify a locked clip.");

        clip.GainDb = Math.Clamp(gainDb, -96f, 12f);
        clip.Touch();
    }

    /// <summary>
    /// Adjusts the gain of an audio clip by a relative amount in decibels.
    /// </summary>
    /// <param name="clip">The clip to modify.</param>
    /// <param name="adjustmentDb">The gain adjustment in dB (positive or negative).</param>
    /// <exception cref="ArgumentNullException">Thrown when clip is null.</exception>
    public static void AdjustGain(AudioClip clip, float adjustmentDb)
    {
        ArgumentNullException.ThrowIfNull(clip);

        if (clip.IsLocked)
            throw new InvalidOperationException("Cannot modify a locked clip.");

        clip.GainDb = Math.Clamp(clip.GainDb + adjustmentDb, -96f, 12f);
        clip.Touch();
    }

    /// <summary>
    /// Sets the gain using a linear multiplier value.
    /// </summary>
    /// <param name="clip">The clip to modify.</param>
    /// <param name="linearGain">The linear gain multiplier (0.0 to ~4.0 for +12dB).</param>
    /// <exception cref="ArgumentNullException">Thrown when clip is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when gain is negative.</exception>
    public static void SetGainLinear(AudioClip clip, float linearGain)
    {
        ArgumentNullException.ThrowIfNull(clip);

        if (linearGain < 0)
            throw new ArgumentOutOfRangeException(nameof(linearGain), "Linear gain cannot be negative.");

        var gainDb = AmplitudeToDb(linearGain);
        SetGain(clip, gainDb);
    }

    /// <summary>
    /// Resets the gain to 0 dB (unity gain).
    /// </summary>
    /// <param name="clip">The clip to reset.</param>
    /// <exception cref="ArgumentNullException">Thrown when clip is null.</exception>
    public static void ResetGain(AudioClip clip)
    {
        SetGain(clip, 0f);
    }

    #endregion

    #region Time Stretch

    /// <summary>
    /// Sets the time stretch factor for an audio clip.
    /// A factor of 1.0 means original speed, 2.0 means double speed (half length),
    /// 0.5 means half speed (double length).
    /// </summary>
    /// <param name="clip">The clip to modify.</param>
    /// <param name="stretchFactor">The time stretch factor (0.25 to 4.0).</param>
    /// <param name="preservePitch">Whether to preserve the original pitch (requires warp enabled).</param>
    /// <exception cref="ArgumentNullException">Thrown when clip is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when stretch factor is out of range.</exception>
    /// <remarks>
    /// Note: Actual time stretching is performed by the audio engine during playback.
    /// This method only sets the stretch factor parameter on the clip.
    /// For high-quality time stretching, enable warp mode on the clip.
    /// </remarks>
    public static void TimeStretch(AudioClip clip, double stretchFactor, bool preservePitch = true)
    {
        ArgumentNullException.ThrowIfNull(clip);

        if (clip.IsLocked)
            throw new InvalidOperationException("Cannot modify a locked clip.");

        if (stretchFactor < 0.25 || stretchFactor > 4.0)
            throw new ArgumentOutOfRangeException(nameof(stretchFactor), "Time stretch factor must be between 0.25 and 4.0.");

        clip.TimeStretchFactor = stretchFactor;
        clip.IsWarpEnabled = preservePitch;
        clip.Touch();
    }

    /// <summary>
    /// Stretches the clip to fit a specific length in beats.
    /// </summary>
    /// <param name="clip">The clip to stretch.</param>
    /// <param name="targetLength">The target length in beats.</param>
    /// <param name="preservePitch">Whether to preserve the original pitch.</param>
    /// <exception cref="ArgumentNullException">Thrown when clip is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when target length is invalid.</exception>
    public static void StretchToLength(AudioClip clip, double targetLength, bool preservePitch = true)
    {
        ArgumentNullException.ThrowIfNull(clip);

        if (targetLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetLength), "Target length must be positive.");

        // Calculate the stretch factor needed
        var stretchFactor = clip.Length / targetLength;

        // Clamp to valid range
        stretchFactor = Math.Clamp(stretchFactor, 0.25, 4.0);

        TimeStretch(clip, stretchFactor, preservePitch);

        // Update the length to reflect the stretch
        clip.Length = targetLength;
    }

    /// <summary>
    /// Resets time stretch to original speed (1.0).
    /// </summary>
    /// <param name="clip">The clip to reset.</param>
    /// <exception cref="ArgumentNullException">Thrown when clip is null.</exception>
    public static void ResetTimeStretch(AudioClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);

        if (clip.IsLocked)
            throw new InvalidOperationException("Cannot modify a locked clip.");

        clip.TimeStretchFactor = 1.0;
        clip.Touch();
    }

    #endregion

    #region Split Operations

    /// <summary>
    /// Splits an audio clip at the specified position.
    /// </summary>
    /// <param name="clip">The clip to split.</param>
    /// <param name="splitPosition">The timeline position to split at (in beats).</param>
    /// <returns>A tuple containing the left part (original clip, modified) and the right part (new clip).</returns>
    /// <exception cref="ArgumentNullException">Thrown when clip is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when clip is locked.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when split position is invalid.</exception>
    public static (AudioClip Left, AudioClip Right) Split(AudioClip clip, double splitPosition)
    {
        ArgumentNullException.ThrowIfNull(clip);

        if (clip.IsLocked)
            throw new InvalidOperationException("Cannot split a locked clip.");

        if (!clip.ContainsPosition(splitPosition))
            throw new ArgumentOutOfRangeException(nameof(splitPosition), "Split position is outside the clip boundaries.");

        if (splitPosition <= clip.StartPosition || splitPosition >= clip.EndPosition)
            throw new ArgumentOutOfRangeException(nameof(splitPosition), "Split position must be inside the clip (not at boundaries).");

        var rightClip = clip.Split(splitPosition);

        if (rightClip == null)
            throw new InvalidOperationException("Failed to split the clip.");

        return (clip, rightClip);
    }

    /// <summary>
    /// Splits an audio clip at a relative position within the clip.
    /// </summary>
    /// <param name="clip">The clip to split.</param>
    /// <param name="relativePosition">The relative position within the clip (0.0 to 1.0).</param>
    /// <returns>A tuple containing the left part and the right part.</returns>
    /// <exception cref="ArgumentNullException">Thrown when clip is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when relative position is out of range.</exception>
    public static (AudioClip Left, AudioClip Right) SplitAtRelative(AudioClip clip, double relativePosition)
    {
        ArgumentNullException.ThrowIfNull(clip);

        if (relativePosition <= 0 || relativePosition >= 1.0)
            throw new ArgumentOutOfRangeException(nameof(relativePosition), "Relative position must be between 0 and 1 (exclusive).");

        var absolutePosition = clip.StartPosition + (clip.Length * relativePosition);
        return Split(clip, absolutePosition);
    }

    /// <summary>
    /// Splits an audio clip into multiple equal parts.
    /// </summary>
    /// <param name="clip">The clip to split.</param>
    /// <param name="numberOfParts">The number of parts to split into (2 or more).</param>
    /// <returns>An array of audio clips representing the parts.</returns>
    /// <exception cref="ArgumentNullException">Thrown when clip is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when number of parts is less than 2.</exception>
    public static AudioClip[] SplitIntoEqualParts(AudioClip clip, int numberOfParts)
    {
        ArgumentNullException.ThrowIfNull(clip);

        if (numberOfParts < 2)
            throw new ArgumentOutOfRangeException(nameof(numberOfParts), "Number of parts must be at least 2.");

        if (clip.IsLocked)
            throw new InvalidOperationException("Cannot split a locked clip.");

        var parts = new AudioClip[numberOfParts];
        var partLength = clip.Length / numberOfParts;

        // First part is the original clip (will be trimmed)
        var currentClip = clip;

        for (int i = 0; i < numberOfParts - 1; i++)
        {
            var splitPosition = currentClip.StartPosition + partLength;
            var splitResult = currentClip.Split(splitPosition);

            if (splitResult == null)
                throw new InvalidOperationException($"Failed to split clip at part {i + 1}.");

            parts[i] = currentClip;
            currentClip = splitResult;
        }

        // Last part
        parts[numberOfParts - 1] = currentClip;

        return parts;
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Converts an amplitude value (linear) to decibels.
    /// </summary>
    /// <param name="amplitude">The amplitude value (0.0 to infinity).</param>
    /// <returns>The value in decibels.</returns>
    public static float AmplitudeToDb(float amplitude)
    {
        if (amplitude <= 0)
            return -96f; // Effectively silence

        return (float)(20.0 * Math.Log10(amplitude));
    }

    /// <summary>
    /// Converts a decibel value to linear amplitude.
    /// </summary>
    /// <param name="db">The value in decibels.</param>
    /// <returns>The linear amplitude value.</returns>
    public static float DbToAmplitude(float db)
    {
        if (db <= -96f)
            return 0f;

        return (float)Math.Pow(10, db / 20.0);
    }

    /// <summary>
    /// Calculates the fade curve value for a given normalized position and fade type.
    /// </summary>
    /// <param name="t">Normalized position (0.0 to 1.0).</param>
    /// <param name="fadeType">The type of fade curve.</param>
    /// <returns>The fade multiplier (0.0 to 1.0).</returns>
    public static float CalculateFadeCurve(double t, FadeType fadeType)
    {
        t = Math.Clamp(t, 0, 1);

        return fadeType switch
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
    /// Creates a copy of an audio clip with all editable properties.
    /// </summary>
    /// <param name="clip">The clip to copy.</param>
    /// <param name="newName">Optional new name for the copy.</param>
    /// <returns>A new AudioClip with the same properties.</returns>
    /// <exception cref="ArgumentNullException">Thrown when clip is null.</exception>
    public static AudioClip CreateCopy(AudioClip clip, string? newName = null)
    {
        ArgumentNullException.ThrowIfNull(clip);

        var copy = clip.Duplicate(clip.StartPosition);
        copy.Name = newName ?? clip.Name;
        return copy;
    }

    #endregion
}
