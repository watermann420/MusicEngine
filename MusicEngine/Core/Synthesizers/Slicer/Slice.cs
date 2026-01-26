// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

namespace MusicEngine.Core.Synthesizers.Slicer;

/// <summary>
/// Represents a single audio slice with playback properties.
/// Used for REX-style beat slicing where audio is divided into segments
/// that can be triggered independently via MIDI notes.
/// </summary>
public class Slice
{
    /// <summary>
    /// Index of this slice in the slice list.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Start position in samples.
    /// </summary>
    public long StartSample { get; set; }

    /// <summary>
    /// End position in samples (exclusive).
    /// </summary>
    public long EndSample { get; set; }

    /// <summary>
    /// Gain multiplier for this slice (0.0 to 2.0, default 1.0).
    /// </summary>
    public float Gain { get; set; } = 1.0f;

    /// <summary>
    /// Playback rate/pitch multiplier (1.0 = original pitch).
    /// Values less than 1.0 lower the pitch, greater than 1.0 raise it.
    /// </summary>
    public float Pitch { get; set; } = 1.0f;

    /// <summary>
    /// Whether to play this slice in reverse.
    /// </summary>
    public bool Reverse { get; set; }

    /// <summary>
    /// MIDI note assigned to trigger this slice (default: -1 = unassigned).
    /// </summary>
    public int MidiNote { get; set; } = -1;

    /// <summary>
    /// Optional name/label for this slice.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Length of this slice in samples.
    /// </summary>
    public long LengthSamples => EndSample - StartSample;

    /// <summary>
    /// Creates a new slice with the specified index.
    /// </summary>
    /// <param name="index">The slice index.</param>
    public Slice(int index)
    {
        Index = index;
    }

    /// <summary>
    /// Creates a new slice with the specified index and sample range.
    /// </summary>
    /// <param name="index">The slice index.</param>
    /// <param name="startSample">Start position in samples.</param>
    /// <param name="endSample">End position in samples.</param>
    public Slice(int index, long startSample, long endSample)
    {
        Index = index;
        StartSample = startSample;
        EndSample = endSample;
    }

    /// <summary>
    /// Gets the length of this slice in seconds.
    /// </summary>
    /// <param name="sampleRate">The audio sample rate.</param>
    /// <returns>Length in seconds.</returns>
    public double LengthSeconds(int sampleRate) => (double)LengthSamples / sampleRate;

    /// <summary>
    /// Gets the start time of this slice in seconds.
    /// </summary>
    /// <param name="sampleRate">The audio sample rate.</param>
    /// <returns>Start time in seconds.</returns>
    public double StartSeconds(int sampleRate) => (double)StartSample / sampleRate;

    /// <summary>
    /// Gets the end time of this slice in seconds.
    /// </summary>
    /// <param name="sampleRate">The audio sample rate.</param>
    /// <returns>End time in seconds.</returns>
    public double EndSeconds(int sampleRate) => (double)EndSample / sampleRate;

    /// <summary>
    /// Returns a string representation of this slice.
    /// </summary>
    public override string ToString()
    {
        string noteStr = MidiNote >= 0 ? $", Note={MidiNote}" : "";
        return $"Slice[{Index}]: {StartSample}-{EndSample} ({LengthSamples} samples){noteStr}";
    }
}
