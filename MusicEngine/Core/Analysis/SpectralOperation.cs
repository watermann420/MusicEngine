// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio analysis component.

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Types of spectral editing operations that can be applied to audio.
/// </summary>
public enum SpectralOperationType
{
    /// <summary>
    /// Erases (zeroes) magnitudes in the selected region.
    /// Useful for removing unwanted sounds like clicks, pops, or specific frequencies.
    /// </summary>
    Erase,

    /// <summary>
    /// Amplifies or attenuates magnitudes in the selected region.
    /// Amount parameter controls gain: less than 1 = attenuate, greater than 1 = amplify.
    /// </summary>
    Amplify,

    /// <summary>
    /// Shifts frequencies up or down by a specified amount.
    /// Amount parameter is in semitones (positive = up, negative = down).
    /// </summary>
    FrequencyShift,

    /// <summary>
    /// Enhances harmonics in the selected region.
    /// Amount parameter controls the strength of harmonic enhancement.
    /// </summary>
    HarmonicEnhance,

    /// <summary>
    /// Reduces noise in the selected region using spectral subtraction.
    /// Amount parameter controls the aggressiveness of noise reduction.
    /// </summary>
    NoiseReduce,

    /// <summary>
    /// Clones/copies spectral content from one region to another.
    /// Uses Parameters["SourceStartTime"] and Parameters["SourceEndTime"].
    /// </summary>
    Clone,

    /// <summary>
    /// Applies a fade (gain envelope) across the selection.
    /// Parameters["FadeType"]: 0 = fade in, 1 = fade out, 2 = crossfade.
    /// </summary>
    Fade,

    /// <summary>
    /// Inverts the phase of frequencies in the selected region.
    /// Useful for phase correction or creative effects.
    /// </summary>
    PhaseInvert,

    /// <summary>
    /// Blurs/smears the spectrum across time for a reverb-like effect.
    /// Amount parameter controls the blur amount.
    /// </summary>
    TimeBlur,

    /// <summary>
    /// Applies spectral smoothing to reduce harshness.
    /// Amount parameter controls the smoothing window size.
    /// </summary>
    Smooth
}

/// <summary>
/// Defines a time-frequency selection region in the spectrogram.
/// </summary>
public class SpectralSelection
{
    /// <summary>
    /// Gets or sets the start time of the selection in seconds.
    /// </summary>
    public double StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time of the selection in seconds.
    /// </summary>
    public double EndTime { get; set; }

    /// <summary>
    /// Gets or sets the minimum frequency of the selection in Hz.
    /// </summary>
    public float MinFrequency { get; set; }

    /// <summary>
    /// Gets or sets the maximum frequency of the selection in Hz.
    /// </summary>
    public float MaxFrequency { get; set; }

    /// <summary>
    /// Gets the duration of the selection in seconds.
    /// </summary>
    public double Duration => EndTime - StartTime;

    /// <summary>
    /// Gets the frequency bandwidth of the selection in Hz.
    /// </summary>
    public float FrequencyBandwidth => MaxFrequency - MinFrequency;

    /// <summary>
    /// Creates an empty spectral selection.
    /// </summary>
    public SpectralSelection()
    {
        StartTime = 0;
        EndTime = 0;
        MinFrequency = 0;
        MaxFrequency = 0;
    }

    /// <summary>
    /// Creates a spectral selection with the specified bounds.
    /// </summary>
    /// <param name="startTime">Start time in seconds</param>
    /// <param name="endTime">End time in seconds</param>
    /// <param name="minFreq">Minimum frequency in Hz</param>
    /// <param name="maxFreq">Maximum frequency in Hz</param>
    public SpectralSelection(double startTime, double endTime, float minFreq, float maxFreq)
    {
        StartTime = startTime;
        EndTime = endTime;
        MinFrequency = minFreq;
        MaxFrequency = maxFreq;
    }

    /// <summary>
    /// Creates a full-bandwidth selection (all frequencies).
    /// </summary>
    /// <param name="startTime">Start time in seconds</param>
    /// <param name="endTime">End time in seconds</param>
    /// <param name="sampleRate">Sample rate for determining Nyquist frequency</param>
    /// <returns>A selection covering all frequencies</returns>
    public static SpectralSelection FullBandwidth(double startTime, double endTime, int sampleRate)
    {
        return new SpectralSelection(startTime, endTime, 0, sampleRate / 2f);
    }

    /// <summary>
    /// Creates a selection covering the full time range.
    /// </summary>
    /// <param name="duration">Total duration in seconds</param>
    /// <param name="minFreq">Minimum frequency in Hz</param>
    /// <param name="maxFreq">Maximum frequency in Hz</param>
    /// <returns>A selection covering the full time range</returns>
    public static SpectralSelection FullDuration(double duration, float minFreq, float maxFreq)
    {
        return new SpectralSelection(0, duration, minFreq, maxFreq);
    }

    /// <summary>
    /// Checks if a given time and frequency falls within this selection.
    /// </summary>
    /// <param name="time">Time in seconds</param>
    /// <param name="frequency">Frequency in Hz</param>
    /// <returns>True if the point is within the selection</returns>
    public bool Contains(double time, float frequency)
    {
        return time >= StartTime && time <= EndTime &&
               frequency >= MinFrequency && frequency <= MaxFrequency;
    }

    /// <summary>
    /// Checks if this selection overlaps with another selection.
    /// </summary>
    /// <param name="other">Another spectral selection</param>
    /// <returns>True if the selections overlap</returns>
    public bool Overlaps(SpectralSelection other)
    {
        bool timeOverlap = StartTime < other.EndTime && EndTime > other.StartTime;
        bool freqOverlap = MinFrequency < other.MaxFrequency && MaxFrequency > other.MinFrequency;
        return timeOverlap && freqOverlap;
    }

    /// <summary>
    /// Gets the intersection of this selection with another.
    /// </summary>
    /// <param name="other">Another spectral selection</param>
    /// <returns>The intersection, or null if no overlap</returns>
    public SpectralSelection? Intersect(SpectralSelection other)
    {
        if (!Overlaps(other))
            return null;

        return new SpectralSelection(
            Math.Max(StartTime, other.StartTime),
            Math.Min(EndTime, other.EndTime),
            Math.Max(MinFrequency, other.MinFrequency),
            Math.Min(MaxFrequency, other.MaxFrequency)
        );
    }

    /// <summary>
    /// Expands the selection by a given amount in all directions.
    /// </summary>
    /// <param name="timePadding">Time padding in seconds</param>
    /// <param name="freqPadding">Frequency padding in Hz</param>
    public void Expand(double timePadding, float freqPadding)
    {
        StartTime -= timePadding;
        EndTime += timePadding;
        MinFrequency = Math.Max(0, MinFrequency - freqPadding);
        MaxFrequency += freqPadding;
    }

    /// <summary>
    /// Creates a copy of this selection.
    /// </summary>
    /// <returns>A new SpectralSelection with the same bounds</returns>
    public SpectralSelection Clone()
    {
        return new SpectralSelection(StartTime, EndTime, MinFrequency, MaxFrequency);
    }

    /// <summary>
    /// Validates the selection bounds.
    /// </summary>
    /// <returns>True if the selection is valid</returns>
    public bool IsValid()
    {
        return StartTime <= EndTime &&
               MinFrequency <= MaxFrequency &&
               MinFrequency >= 0 &&
               StartTime >= 0;
    }
}

/// <summary>
/// Represents a spectral editing operation with its parameters.
/// Operations can be applied to the SpectralEditor and support undo/redo.
/// </summary>
public class SpectralOperation
{
    /// <summary>
    /// Gets or sets the type of operation.
    /// </summary>
    public SpectralOperationType Type { get; set; }

    /// <summary>
    /// Gets or sets the time-frequency selection region for this operation.
    /// </summary>
    public SpectralSelection Selection { get; set; }

    /// <summary>
    /// Gets or sets the operation intensity/amount.
    /// Interpretation depends on operation type:
    /// - Erase: Not used (always complete erasure)
    /// - Amplify: Gain factor (0.5 = -6dB, 2.0 = +6dB)
    /// - FrequencyShift: Semitones to shift
    /// - HarmonicEnhance: Enhancement strength (0-1)
    /// - NoiseReduce: Reduction aggressiveness (0-1)
    /// </summary>
    public float Amount { get; set; }

    /// <summary>
    /// Gets additional parameters for the operation.
    /// Keys and values depend on operation type.
    /// </summary>
    public Dictionary<string, float> Parameters { get; } = new();

    /// <summary>
    /// Gets or sets the timestamp when this operation was created.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets a description of this operation for display in undo history.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Stores the original frame data for undo capability.
    /// </summary>
    internal List<SpectralFrame>? UndoData { get; set; }

    /// <summary>
    /// Creates a new spectral operation.
    /// </summary>
    public SpectralOperation()
    {
        Type = SpectralOperationType.Erase;
        Selection = new SpectralSelection();
        Amount = 1.0f;
        Timestamp = DateTime.Now;
        Description = string.Empty;
    }

    /// <summary>
    /// Creates a new spectral operation with specified parameters.
    /// </summary>
    /// <param name="type">Operation type</param>
    /// <param name="selection">Selection region</param>
    /// <param name="amount">Operation amount</param>
    public SpectralOperation(SpectralOperationType type, SpectralSelection selection, float amount)
    {
        Type = type;
        Selection = selection;
        Amount = amount;
        Timestamp = DateTime.Now;
        Description = GetDefaultDescription(type);
    }

    /// <summary>
    /// Creates an Erase operation.
    /// </summary>
    public static SpectralOperation CreateErase(SpectralSelection selection)
    {
        return new SpectralOperation(SpectralOperationType.Erase, selection, 0f)
        {
            Description = "Erase selection"
        };
    }

    /// <summary>
    /// Creates an Amplify operation.
    /// </summary>
    /// <param name="selection">Selection region</param>
    /// <param name="gainDb">Gain in decibels</param>
    public static SpectralOperation CreateAmplify(SpectralSelection selection, float gainDb)
    {
        float linearGain = MathF.Pow(10f, gainDb / 20f);
        return new SpectralOperation(SpectralOperationType.Amplify, selection, linearGain)
        {
            Description = $"Amplify by {gainDb:F1} dB"
        };
    }

    /// <summary>
    /// Creates a FrequencyShift operation.
    /// </summary>
    /// <param name="selection">Selection region</param>
    /// <param name="semitones">Semitones to shift (positive = up)</param>
    public static SpectralOperation CreateFrequencyShift(SpectralSelection selection, float semitones)
    {
        return new SpectralOperation(SpectralOperationType.FrequencyShift, selection, semitones)
        {
            Description = $"Shift {(semitones >= 0 ? "+" : "")}{semitones:F1} semitones"
        };
    }

    /// <summary>
    /// Creates a HarmonicEnhance operation.
    /// </summary>
    /// <param name="selection">Selection region</param>
    /// <param name="strength">Enhancement strength (0-1)</param>
    public static SpectralOperation CreateHarmonicEnhance(SpectralSelection selection, float strength)
    {
        return new SpectralOperation(SpectralOperationType.HarmonicEnhance, selection, Math.Clamp(strength, 0f, 1f))
        {
            Description = $"Enhance harmonics ({strength * 100:F0}%)"
        };
    }

    /// <summary>
    /// Creates a NoiseReduce operation.
    /// </summary>
    /// <param name="selection">Selection region</param>
    /// <param name="reduction">Reduction amount (0-1)</param>
    public static SpectralOperation CreateNoiseReduce(SpectralSelection selection, float reduction)
    {
        return new SpectralOperation(SpectralOperationType.NoiseReduce, selection, Math.Clamp(reduction, 0f, 1f))
        {
            Description = $"Reduce noise ({reduction * 100:F0}%)"
        };
    }

    /// <summary>
    /// Creates a Fade operation.
    /// </summary>
    /// <param name="selection">Selection region</param>
    /// <param name="fadeIn">True for fade in, false for fade out</param>
    public static SpectralOperation CreateFade(SpectralSelection selection, bool fadeIn)
    {
        var op = new SpectralOperation(SpectralOperationType.Fade, selection, fadeIn ? 1f : 0f)
        {
            Description = fadeIn ? "Fade in" : "Fade out"
        };
        op.Parameters["FadeType"] = fadeIn ? 0f : 1f;
        return op;
    }

    /// <summary>
    /// Creates a Clone operation.
    /// </summary>
    /// <param name="targetSelection">Target selection region</param>
    /// <param name="sourceStartTime">Source start time in seconds</param>
    /// <param name="sourceEndTime">Source end time in seconds</param>
    public static SpectralOperation CreateClone(SpectralSelection targetSelection, double sourceStartTime, double sourceEndTime)
    {
        var op = new SpectralOperation(SpectralOperationType.Clone, targetSelection, 1f)
        {
            Description = "Clone spectral content"
        };
        op.Parameters["SourceStartTime"] = (float)sourceStartTime;
        op.Parameters["SourceEndTime"] = (float)sourceEndTime;
        return op;
    }

    private static string GetDefaultDescription(SpectralOperationType type)
    {
        return type switch
        {
            SpectralOperationType.Erase => "Erase",
            SpectralOperationType.Amplify => "Amplify",
            SpectralOperationType.FrequencyShift => "Frequency Shift",
            SpectralOperationType.HarmonicEnhance => "Harmonic Enhance",
            SpectralOperationType.NoiseReduce => "Noise Reduce",
            SpectralOperationType.Clone => "Clone",
            SpectralOperationType.Fade => "Fade",
            SpectralOperationType.PhaseInvert => "Phase Invert",
            SpectralOperationType.TimeBlur => "Time Blur",
            SpectralOperationType.Smooth => "Smooth",
            _ => "Unknown Operation"
        };
    }
}
