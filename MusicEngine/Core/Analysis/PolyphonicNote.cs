//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Represents a single note extracted from polyphonic audio analysis for Melodyne DNA-style editing.

using System;

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Represents a single note extracted from polyphonic audio analysis.
/// Contains pitch, timing, amplitude, and contour information for detailed editing.
/// This is the fundamental unit for Melodyne DNA-style polyphonic pitch editing.
/// </summary>
public class PolyphonicNote
{
    /// <summary>
    /// Unique identifier for this note.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// Start time of the note in seconds from the beginning of the audio.
    /// </summary>
    public double StartTime { get; set; }

    /// <summary>
    /// End time of the note in seconds from the beginning of the audio.
    /// </summary>
    public double EndTime { get; set; }

    /// <summary>
    /// Duration of the note in seconds.
    /// </summary>
    public double Duration => EndTime - StartTime;

    /// <summary>
    /// Current pitch in MIDI note number (can be fractional for micro-tuning).
    /// For example, 60.5 would be halfway between C4 and C#4.
    /// </summary>
    public float Pitch { get; set; }

    /// <summary>
    /// Original detected pitch in MIDI note number before any editing.
    /// This value is immutable after analysis.
    /// </summary>
    public float OriginalPitch { get; }

    /// <summary>
    /// Pitch deviation from original in semitones.
    /// Positive values indicate pitch shift up, negative values indicate pitch shift down.
    /// </summary>
    public float PitchDeviation => Pitch - OriginalPitch;

    /// <summary>
    /// Average amplitude of the note (0.0 to 1.0).
    /// </summary>
    public float Amplitude { get; set; }

    /// <summary>
    /// Pitch contour over the duration of the note.
    /// Each element represents the pitch (in MIDI note number) at that time point.
    /// The array length determines the temporal resolution.
    /// </summary>
    public float[] PitchContour { get; set; } = Array.Empty<float>();

    /// <summary>
    /// Amplitude contour over the duration of the note.
    /// Each element represents the amplitude (0.0 to 1.0) at that time point.
    /// Corresponds to the same time points as PitchContour.
    /// </summary>
    public float[] AmplitudeContour { get; set; } = Array.Empty<float>();

    /// <summary>
    /// Amount of vibrato detected or applied (0.0 to 1.0).
    /// 0.0 = no vibrato, 1.0 = maximum vibrato.
    /// </summary>
    public float Vibrato { get; set; }

    /// <summary>
    /// Formant shift in semitones.
    /// Positive values shift formants up (smaller vocal tract),
    /// negative values shift formants down (larger vocal tract).
    /// </summary>
    public float Formant { get; set; }

    /// <summary>
    /// Index of the voice/strand this note belongs to.
    /// Used to group notes that belong to the same melodic line.
    /// </summary>
    public int VoiceIndex { get; set; }

    /// <summary>
    /// Whether this note is currently selected in the UI.
    /// </summary>
    public bool IsSelected { get; set; }

    /// <summary>
    /// Whether this note has been modified from its original state.
    /// </summary>
    public bool IsModified => Math.Abs(PitchDeviation) > 0.001f || Math.Abs(Formant) > 0.001f;

    /// <summary>
    /// Confidence of the pitch detection (0.0 to 1.0).
    /// Higher values indicate more certain detection.
    /// </summary>
    public float Confidence { get; set; } = 1.0f;

    /// <summary>
    /// The fundamental frequency in Hz corresponding to the current pitch.
    /// </summary>
    public float FrequencyHz => MidiNoteToFrequency(Pitch);

    /// <summary>
    /// The original fundamental frequency in Hz.
    /// </summary>
    public float OriginalFrequencyHz => MidiNoteToFrequency(OriginalPitch);

    /// <summary>
    /// Start sample position in the original audio buffer.
    /// </summary>
    public long StartSample { get; set; }

    /// <summary>
    /// End sample position in the original audio buffer.
    /// </summary>
    public long EndSample { get; set; }

    /// <summary>
    /// Creates a new PolyphonicNote with the specified original pitch.
    /// </summary>
    /// <param name="originalPitch">The detected pitch in MIDI note number.</param>
    public PolyphonicNote(float originalPitch)
    {
        OriginalPitch = originalPitch;
        Pitch = originalPitch;
    }

    /// <summary>
    /// Creates a new PolyphonicNote with full initialization.
    /// </summary>
    /// <param name="originalPitch">The detected pitch in MIDI note number.</param>
    /// <param name="startTime">Start time in seconds.</param>
    /// <param name="endTime">End time in seconds.</param>
    /// <param name="amplitude">Average amplitude (0.0 to 1.0).</param>
    /// <param name="voiceIndex">Voice/strand index.</param>
    public PolyphonicNote(float originalPitch, double startTime, double endTime, float amplitude, int voiceIndex)
        : this(originalPitch)
    {
        StartTime = startTime;
        EndTime = endTime;
        Amplitude = amplitude;
        VoiceIndex = voiceIndex;
    }

    /// <summary>
    /// Resets the pitch to the original detected value.
    /// </summary>
    public void ResetPitch()
    {
        Pitch = OriginalPitch;
    }

    /// <summary>
    /// Resets all edits (pitch and formant) to original values.
    /// </summary>
    public void ResetAll()
    {
        Pitch = OriginalPitch;
        Formant = 0f;
        Vibrato = 0f;
    }

    /// <summary>
    /// Gets the pitch at a specific time within the note.
    /// </summary>
    /// <param name="time">Time in seconds (relative to note start).</param>
    /// <returns>Pitch in MIDI note number at the specified time.</returns>
    public float GetPitchAtTime(double time)
    {
        if (PitchContour == null || PitchContour.Length == 0)
            return Pitch;

        if (time <= 0)
            return PitchContour[0];

        if (time >= Duration)
            return PitchContour[^1];

        // Linear interpolation between contour points
        double normalizedTime = time / Duration;
        double index = normalizedTime * (PitchContour.Length - 1);
        int lowIndex = (int)index;
        int highIndex = Math.Min(lowIndex + 1, PitchContour.Length - 1);
        float fraction = (float)(index - lowIndex);

        return PitchContour[lowIndex] * (1f - fraction) + PitchContour[highIndex] * fraction;
    }

    /// <summary>
    /// Gets the amplitude at a specific time within the note.
    /// </summary>
    /// <param name="time">Time in seconds (relative to note start).</param>
    /// <returns>Amplitude (0.0 to 1.0) at the specified time.</returns>
    public float GetAmplitudeAtTime(double time)
    {
        if (AmplitudeContour == null || AmplitudeContour.Length == 0)
            return Amplitude;

        if (time <= 0)
            return AmplitudeContour[0];

        if (time >= Duration)
            return AmplitudeContour[^1];

        // Linear interpolation between contour points
        double normalizedTime = time / Duration;
        double index = normalizedTime * (AmplitudeContour.Length - 1);
        int lowIndex = (int)index;
        int highIndex = Math.Min(lowIndex + 1, AmplitudeContour.Length - 1);
        float fraction = (float)(index - lowIndex);

        return AmplitudeContour[lowIndex] * (1f - fraction) + AmplitudeContour[highIndex] * fraction;
    }

    /// <summary>
    /// Quantizes the pitch to the nearest semitone.
    /// </summary>
    /// <param name="strength">Quantization strength (0.0 to 1.0). 1.0 = full quantization.</param>
    public void QuantizeToSemitone(float strength = 1.0f)
    {
        strength = Math.Clamp(strength, 0f, 1f);
        float targetPitch = MathF.Round(Pitch);
        Pitch = Pitch + (targetPitch - Pitch) * strength;
    }

    /// <summary>
    /// Quantizes the pitch to a specific scale.
    /// </summary>
    /// <param name="scaleNotes">Array of pitch classes (0-11) that are in the scale.</param>
    /// <param name="strength">Quantization strength (0.0 to 1.0). 1.0 = full quantization.</param>
    public void QuantizeToScale(int[] scaleNotes, float strength = 1.0f)
    {
        if (scaleNotes == null || scaleNotes.Length == 0)
            return;

        strength = Math.Clamp(strength, 0f, 1f);
        int pitchClass = (int)MathF.Round(Pitch) % 12;
        int octave = (int)MathF.Round(Pitch) / 12;

        // Find nearest scale note
        int nearestScaleNote = scaleNotes[0];
        int minDistance = 12;

        foreach (int scaleNote in scaleNotes)
        {
            int distance = Math.Abs(pitchClass - scaleNote);
            if (distance > 6) distance = 12 - distance; // Wrap around

            if (distance < minDistance)
            {
                minDistance = distance;
                nearestScaleNote = scaleNote;
            }
        }

        float targetPitch = octave * 12 + nearestScaleNote;

        // Handle octave boundary
        if (targetPitch - Pitch > 6)
            targetPitch -= 12;
        else if (Pitch - targetPitch > 6)
            targetPitch += 12;

        Pitch = Pitch + (targetPitch - Pitch) * strength;
    }

    /// <summary>
    /// Creates a deep copy of this note.
    /// </summary>
    public PolyphonicNote Clone()
    {
        var clone = new PolyphonicNote(OriginalPitch)
        {
            StartTime = StartTime,
            EndTime = EndTime,
            Pitch = Pitch,
            Amplitude = Amplitude,
            Vibrato = Vibrato,
            Formant = Formant,
            VoiceIndex = VoiceIndex,
            IsSelected = IsSelected,
            Confidence = Confidence,
            StartSample = StartSample,
            EndSample = EndSample
        };

        if (PitchContour != null && PitchContour.Length > 0)
        {
            clone.PitchContour = new float[PitchContour.Length];
            Array.Copy(PitchContour, clone.PitchContour, PitchContour.Length);
        }

        if (AmplitudeContour != null && AmplitudeContour.Length > 0)
        {
            clone.AmplitudeContour = new float[AmplitudeContour.Length];
            Array.Copy(AmplitudeContour, clone.AmplitudeContour, AmplitudeContour.Length);
        }

        return clone;
    }

    /// <summary>
    /// Converts a MIDI note number to frequency in Hz.
    /// </summary>
    private static float MidiNoteToFrequency(float midiNote)
    {
        return 440f * MathF.Pow(2f, (midiNote - 69f) / 12f);
    }

    /// <summary>
    /// Converts a frequency in Hz to MIDI note number.
    /// </summary>
    public static float FrequencyToMidiNote(float frequency)
    {
        if (frequency <= 0)
            return 0;
        return 69f + 12f * MathF.Log2(frequency / 440f);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"Note {Note.ToName((int)MathF.Round(Pitch))} [{StartTime:F3}s - {EndTime:F3}s] Voice {VoiceIndex}";
    }
}
