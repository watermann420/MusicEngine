//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Polyphonic pitch editor for Melodyne DNA-style editing with undo/redo support.

using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Types of edit operations for undo/redo support.
/// </summary>
public enum EditOperationType
{
    /// <summary>Pitch was changed.</summary>
    PitchChange,
    /// <summary>Timing was changed.</summary>
    TimeChange,
    /// <summary>Formant was changed.</summary>
    FormantChange,
    /// <summary>Note was split into two.</summary>
    NoteSplit,
    /// <summary>Two notes were merged.</summary>
    NoteMerge,
    /// <summary>Note was deleted.</summary>
    NoteDelete,
    /// <summary>Note was added.</summary>
    NoteAdd,
    /// <summary>Multiple notes were batch edited.</summary>
    BatchEdit
}

/// <summary>
/// Represents a single edit operation for undo/redo.
/// </summary>
internal class EditOperation
{
    /// <summary>Type of edit operation.</summary>
    public EditOperationType Type { get; set; }

    /// <summary>The note being edited (may be null for batch operations).</summary>
    public PolyphonicNote? Note { get; set; }

    /// <summary>Additional notes involved (for merge, batch).</summary>
    public List<PolyphonicNote> AdditionalNotes { get; set; } = new();

    /// <summary>Voice index for the note.</summary>
    public int VoiceIndex { get; set; }

    /// <summary>Old pitch value.</summary>
    public float OldPitch { get; set; }

    /// <summary>New pitch value.</summary>
    public float NewPitch { get; set; }

    /// <summary>Old start time.</summary>
    public double OldStart { get; set; }

    /// <summary>Old end time.</summary>
    public double OldEnd { get; set; }

    /// <summary>New start time.</summary>
    public double NewStart { get; set; }

    /// <summary>New end time.</summary>
    public double NewEnd { get; set; }

    /// <summary>Old formant value.</summary>
    public float OldFormant { get; set; }

    /// <summary>New formant value.</summary>
    public float NewFormant { get; set; }

    /// <summary>Timestamp of the operation.</summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>Optional description of the operation.</summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Polyphonic pitch editor providing Melodyne DNA-style editing capabilities.
/// Supports pitch correction, time manipulation, formant shifting, and note operations.
/// All operations support undo/redo.
/// </summary>
public class PolyphonicPitchEdit : IDisposable
{
    private float[]? _originalAudio;
    private float[]? _processedAudio;
    private PolyphonicAnalysisResult? _analysis;
    private readonly Stack<EditOperation> _undoStack = new();
    private readonly Stack<EditOperation> _redoStack = new();
    private int _sampleRate;
    private readonly PitchSynthesizer _synthesizer = new();
    private readonly PolyphonicAnalyzer _analyzer = new();
    private bool _disposed;

    /// <summary>
    /// Gets the current analysis result with notes and voices.
    /// </summary>
    public PolyphonicAnalysisResult? Analysis => _analysis;

    /// <summary>
    /// Gets whether there are unsaved changes.
    /// </summary>
    public bool HasChanges { get; private set; }

    /// <summary>
    /// Gets whether undo is available.
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>
    /// Gets whether redo is available.
    /// </summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// Gets the sample rate of the loaded audio.
    /// </summary>
    public int SampleRate => _sampleRate;

    /// <summary>
    /// Gets or sets whether to preserve formants during pitch shifting.
    /// </summary>
    public bool PreserveFormants
    {
        get => _synthesizer.PreserveFormants;
        set => _synthesizer.PreserveFormants = value;
    }

    /// <summary>
    /// Maximum number of undo steps to keep.
    /// </summary>
    public int MaxUndoSteps { get; set; } = 100;

    /// <summary>
    /// Event raised when the analysis is updated.
    /// </summary>
    public event EventHandler? AnalysisChanged;

    /// <summary>
    /// Event raised when the undo/redo state changes.
    /// </summary>
    public event EventHandler? UndoStateChanged;

    /// <summary>
    /// Event raised when analysis progress updates.
    /// </summary>
    public event EventHandler<float>? AnalysisProgress;

    /// <summary>
    /// Loads audio data for editing.
    /// </summary>
    /// <param name="audioData">Mono audio samples.</param>
    /// <param name="sampleRate">Sample rate of the audio.</param>
    public void LoadAudio(float[] audioData, int sampleRate)
    {
        if (audioData == null || audioData.Length == 0)
            throw new ArgumentException("Audio data cannot be null or empty.", nameof(audioData));

        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "Sample rate must be positive.");

        _originalAudio = (float[])audioData.Clone();
        _processedAudio = null;
        _sampleRate = sampleRate;
        _analysis = null;
        _undoStack.Clear();
        _redoStack.Clear();
        HasChanges = false;
    }

    /// <summary>
    /// Analyzes the loaded audio to extract polyphonic notes.
    /// </summary>
    public void Analyze()
    {
        if (_originalAudio == null)
            throw new InvalidOperationException("No audio loaded. Call LoadAudio first.");

        _analyzer.ProgressChanged += OnAnalysisProgress;
        try
        {
            _analysis = _analyzer.Analyze(_originalAudio, _sampleRate);
        }
        finally
        {
            _analyzer.ProgressChanged -= OnAnalysisProgress;
        }

        AnalysisChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnAnalysisProgress(object? sender, float progress)
    {
        AnalysisProgress?.Invoke(this, progress);
    }

    /// <summary>
    /// Sets the pitch of a note.
    /// </summary>
    /// <param name="note">The note to modify.</param>
    /// <param name="newPitch">New pitch in MIDI note number.</param>
    public void SetNotePitch(PolyphonicNote note, float newPitch)
    {
        if (note == null)
            throw new ArgumentNullException(nameof(note));

        var operation = new EditOperation
        {
            Type = EditOperationType.PitchChange,
            Note = note,
            VoiceIndex = note.VoiceIndex,
            OldPitch = note.Pitch,
            NewPitch = newPitch,
            Description = $"Change pitch from {note.Pitch:F1} to {newPitch:F1}"
        };

        note.Pitch = newPitch;
        PushUndoOperation(operation);
        MarkChanged();
    }

    /// <summary>
    /// Sets the timing of a note.
    /// </summary>
    /// <param name="note">The note to modify.</param>
    /// <param name="newStart">New start time in seconds.</param>
    /// <param name="newEnd">New end time in seconds.</param>
    public void SetNoteTime(PolyphonicNote note, double newStart, double newEnd)
    {
        if (note == null)
            throw new ArgumentNullException(nameof(note));

        if (newEnd <= newStart)
            throw new ArgumentException("End time must be greater than start time.");

        var operation = new EditOperation
        {
            Type = EditOperationType.TimeChange,
            Note = note,
            VoiceIndex = note.VoiceIndex,
            OldStart = note.StartTime,
            OldEnd = note.EndTime,
            NewStart = newStart,
            NewEnd = newEnd,
            Description = $"Change time from [{note.StartTime:F3}s-{note.EndTime:F3}s] to [{newStart:F3}s-{newEnd:F3}s]"
        };

        // Update sample positions
        note.StartSample = (long)(newStart * _sampleRate);
        note.EndSample = (long)(newEnd * _sampleRate);
        note.StartTime = newStart;
        note.EndTime = newEnd;

        // Re-sort notes in voice
        if (_analysis != null && note.VoiceIndex < _analysis.Voices.Count)
        {
            _analysis.Voices[note.VoiceIndex].SortNotes();
        }

        PushUndoOperation(operation);
        MarkChanged();
    }

    /// <summary>
    /// Sets the formant shift of a note.
    /// </summary>
    /// <param name="note">The note to modify.</param>
    /// <param name="formantShift">Formant shift in semitones.</param>
    public void SetNoteFormant(PolyphonicNote note, float formantShift)
    {
        if (note == null)
            throw new ArgumentNullException(nameof(note));

        var operation = new EditOperation
        {
            Type = EditOperationType.FormantChange,
            Note = note,
            VoiceIndex = note.VoiceIndex,
            OldFormant = note.Formant,
            NewFormant = formantShift,
            Description = $"Change formant from {note.Formant:F1} to {formantShift:F1} semitones"
        };

        note.Formant = formantShift;
        PushUndoOperation(operation);
        MarkChanged();
    }

    /// <summary>
    /// Splits a note into two at the specified time.
    /// </summary>
    /// <param name="note">The note to split.</param>
    /// <param name="splitTime">Time in seconds where to split.</param>
    /// <returns>The newly created second note.</returns>
    public PolyphonicNote SplitNote(PolyphonicNote note, double splitTime)
    {
        if (note == null)
            throw new ArgumentNullException(nameof(note));

        if (splitTime <= note.StartTime || splitTime >= note.EndTime)
            throw new ArgumentException("Split time must be within the note duration.");

        if (_analysis == null)
            throw new InvalidOperationException("No analysis available.");

        // Create the second note
        var secondNote = note.Clone();
        secondNote.StartTime = splitTime;
        secondNote.StartSample = (long)(splitTime * _sampleRate);

        // Modify the first note
        double originalEnd = note.EndTime;
        long originalEndSample = note.EndSample;
        note.EndTime = splitTime;
        note.EndSample = (long)(splitTime * _sampleRate);

        // Adjust pitch contours if present
        if (note.PitchContour != null && note.PitchContour.Length > 2)
        {
            double splitRatio = (splitTime - note.StartTime) / note.Duration;
            int splitIndex = (int)(splitRatio * note.PitchContour.Length);
            splitIndex = Math.Clamp(splitIndex, 1, note.PitchContour.Length - 1);

            float[] firstContour = new float[splitIndex];
            float[] secondContour = new float[note.PitchContour.Length - splitIndex];

            Array.Copy(note.PitchContour, 0, firstContour, 0, splitIndex);
            Array.Copy(note.PitchContour, splitIndex, secondContour, 0, secondContour.Length);

            note.PitchContour = firstContour;
            secondNote.PitchContour = secondContour;

            // Same for amplitude contour
            if (note.AmplitudeContour != null && note.AmplitudeContour.Length == note.PitchContour.Length + secondContour.Length)
            {
                float[] firstAmpContour = new float[splitIndex];
                float[] secondAmpContour = new float[note.AmplitudeContour.Length - splitIndex];

                Array.Copy(note.AmplitudeContour, 0, firstAmpContour, 0, splitIndex);
                Array.Copy(note.AmplitudeContour, splitIndex, secondAmpContour, 0, secondAmpContour.Length);

                note.AmplitudeContour = firstAmpContour;
                secondNote.AmplitudeContour = secondAmpContour;
            }
        }

        // Add second note to voice
        var voice = _analysis.Voices[note.VoiceIndex];
        voice.AddNote(secondNote);

        var operation = new EditOperation
        {
            Type = EditOperationType.NoteSplit,
            Note = note,
            VoiceIndex = note.VoiceIndex,
            OldStart = note.StartTime,
            OldEnd = originalEnd,
            NewStart = note.StartTime,
            NewEnd = splitTime,
            Description = $"Split note at {splitTime:F3}s"
        };
        operation.AdditionalNotes.Add(secondNote);

        PushUndoOperation(operation);
        MarkChanged();
        AnalysisChanged?.Invoke(this, EventArgs.Empty);

        return secondNote;
    }

    /// <summary>
    /// Merges two adjacent notes into one.
    /// </summary>
    /// <param name="note1">First note (should come before note2).</param>
    /// <param name="note2">Second note (will be removed after merge).</param>
    public void MergeNotes(PolyphonicNote note1, PolyphonicNote note2)
    {
        if (note1 == null)
            throw new ArgumentNullException(nameof(note1));
        if (note2 == null)
            throw new ArgumentNullException(nameof(note2));

        if (_analysis == null)
            throw new InvalidOperationException("No analysis available.");

        // Ensure note1 comes first
        if (note1.StartTime > note2.StartTime)
        {
            (note1, note2) = (note2, note1);
        }

        // Store original values for undo
        var operation = new EditOperation
        {
            Type = EditOperationType.NoteMerge,
            Note = note1,
            VoiceIndex = note1.VoiceIndex,
            OldStart = note1.StartTime,
            OldEnd = note1.EndTime,
            NewStart = note1.StartTime,
            NewEnd = note2.EndTime,
            Description = "Merge two notes"
        };
        operation.AdditionalNotes.Add(note2.Clone());

        // Extend note1 to cover note2
        note1.EndTime = note2.EndTime;
        note1.EndSample = note2.EndSample;

        // Average pitch if different
        if (Math.Abs(note1.Pitch - note2.Pitch) > 0.001f)
        {
            note1.Pitch = (note1.Pitch + note2.Pitch) / 2f;
        }

        // Merge contours
        if (note1.PitchContour != null && note2.PitchContour != null)
        {
            float[] mergedContour = new float[note1.PitchContour.Length + note2.PitchContour.Length];
            Array.Copy(note1.PitchContour, 0, mergedContour, 0, note1.PitchContour.Length);
            Array.Copy(note2.PitchContour, 0, mergedContour, note1.PitchContour.Length, note2.PitchContour.Length);
            note1.PitchContour = mergedContour;
        }

        if (note1.AmplitudeContour != null && note2.AmplitudeContour != null)
        {
            float[] mergedAmpContour = new float[note1.AmplitudeContour.Length + note2.AmplitudeContour.Length];
            Array.Copy(note1.AmplitudeContour, 0, mergedAmpContour, 0, note1.AmplitudeContour.Length);
            Array.Copy(note2.AmplitudeContour, 0, mergedAmpContour, note1.AmplitudeContour.Length, note2.AmplitudeContour.Length);
            note1.AmplitudeContour = mergedAmpContour;
        }

        // Remove note2 from its voice
        var voice = _analysis.Voices.FirstOrDefault(v => v.Notes.Contains(note2));
        voice?.RemoveNote(note2);

        PushUndoOperation(operation);
        MarkChanged();
        AnalysisChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Deletes a note from the analysis.
    /// </summary>
    /// <param name="note">The note to delete.</param>
    public void DeleteNote(PolyphonicNote note)
    {
        if (note == null)
            throw new ArgumentNullException(nameof(note));

        if (_analysis == null)
            throw new InvalidOperationException("No analysis available.");

        var voice = _analysis.Voices.FirstOrDefault(v => v.Notes.Contains(note));
        if (voice == null)
            return;

        var operation = new EditOperation
        {
            Type = EditOperationType.NoteDelete,
            Note = note.Clone(),
            VoiceIndex = voice.Index,
            OldStart = note.StartTime,
            OldEnd = note.EndTime,
            OldPitch = note.Pitch,
            OldFormant = note.Formant,
            Description = $"Delete note at {note.StartTime:F3}s"
        };

        voice.RemoveNote(note);

        PushUndoOperation(operation);
        MarkChanged();
        AnalysisChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Quantizes a note's pitch to the nearest semitone.
    /// </summary>
    /// <param name="note">The note to quantize.</param>
    /// <param name="strength">Quantization strength (0.0 to 1.0).</param>
    public void QuantizePitch(PolyphonicNote note, float strength = 1.0f)
    {
        if (note == null)
            throw new ArgumentNullException(nameof(note));

        float oldPitch = note.Pitch;
        note.QuantizeToSemitone(strength);

        if (Math.Abs(note.Pitch - oldPitch) > 0.001f)
        {
            var operation = new EditOperation
            {
                Type = EditOperationType.PitchChange,
                Note = note,
                VoiceIndex = note.VoiceIndex,
                OldPitch = oldPitch,
                NewPitch = note.Pitch,
                Description = $"Quantize pitch from {oldPitch:F1} to {note.Pitch:F1}"
            };

            PushUndoOperation(operation);
            MarkChanged();
        }
    }

    /// <summary>
    /// Quantizes all notes to a musical scale.
    /// </summary>
    /// <param name="scale">Scale name (e.g., "Major", "NaturalMinor").</param>
    /// <param name="rootNote">Root note name (e.g., "C", "D#").</param>
    /// <param name="strength">Quantization strength (0.0 to 1.0).</param>
    public void QuantizeAllToScale(string scale, string rootNote, float strength = 1.0f)
    {
        if (_analysis == null)
            throw new InvalidOperationException("No analysis available.");

        // Parse scale type
        if (!Enum.TryParse<ScaleType>(scale, true, out var scaleType))
            scaleType = ScaleType.Major;

        // Parse root note
        int root = ParseRootNote(rootNote);

        // Get scale intervals
        int[] scaleNotes = GetScaleNotes(root, scaleType);

        // Batch operation
        var operations = new List<EditOperation>();

        foreach (var voice in _analysis.Voices)
        {
            foreach (var note in voice.Notes)
            {
                float oldPitch = note.Pitch;
                note.QuantizeToScale(scaleNotes, strength);

                if (Math.Abs(note.Pitch - oldPitch) > 0.001f)
                {
                    operations.Add(new EditOperation
                    {
                        Type = EditOperationType.PitchChange,
                        Note = note,
                        VoiceIndex = note.VoiceIndex,
                        OldPitch = oldPitch,
                        NewPitch = note.Pitch
                    });
                }
            }
        }

        if (operations.Count > 0)
        {
            var batchOp = new EditOperation
            {
                Type = EditOperationType.BatchEdit,
                Description = $"Quantize all to {rootNote} {scale}",
                AdditionalNotes = operations.Select(o => o.Note!).ToList()
            };
            // Store individual operations for proper undo
            foreach (var op in operations)
            {
                batchOp.AdditionalNotes.Add(op.Note!);
            }

            PushUndoOperation(batchOp);
            MarkChanged();
        }
    }

    /// <summary>
    /// Straightens (flattens) the pitch modulation of a note.
    /// </summary>
    /// <param name="note">The note to straighten.</param>
    /// <param name="amount">Amount of straightening (0.0 = none, 1.0 = completely flat).</param>
    public void StraightenPitch(PolyphonicNote note, float amount)
    {
        if (note == null)
            throw new ArgumentNullException(nameof(note));

        amount = Math.Clamp(amount, 0f, 1f);

        if (note.PitchContour == null || note.PitchContour.Length == 0)
            return;

        float targetPitch = note.Pitch;
        for (int i = 0; i < note.PitchContour.Length; i++)
        {
            note.PitchContour[i] = note.PitchContour[i] + (targetPitch - note.PitchContour[i]) * amount;
        }

        // Reduce vibrato accordingly
        note.Vibrato *= (1f - amount);

        MarkChanged();
    }

    /// <summary>
    /// Applies all edits and returns the resynthesized audio.
    /// </summary>
    /// <returns>Processed audio samples.</returns>
    public float[] Apply()
    {
        if (_originalAudio == null)
            throw new InvalidOperationException("No audio loaded.");

        if (_analysis == null)
            return (float[])_originalAudio.Clone();

        _processedAudio = _synthesizer.Synthesize(_analysis, _originalAudio, _sampleRate);
        return (float[])_processedAudio.Clone();
    }

    /// <summary>
    /// Gets a preview of the audio for a specific time range.
    /// </summary>
    /// <param name="startTime">Start time in seconds.</param>
    /// <param name="endTime">End time in seconds.</param>
    /// <returns>Audio samples for the specified range.</returns>
    public float[] GetPreview(double startTime, double endTime)
    {
        if (_originalAudio == null)
            throw new InvalidOperationException("No audio loaded.");

        // Apply changes to get processed audio
        float[] processed = Apply();

        // Extract the requested range
        int startSample = Math.Max(0, (int)(startTime * _sampleRate));
        int endSample = Math.Min(processed.Length, (int)(endTime * _sampleRate));
        int length = endSample - startSample;

        if (length <= 0)
            return Array.Empty<float>();

        float[] preview = new float[length];
        Array.Copy(processed, startSample, preview, 0, length);

        return preview;
    }

    /// <summary>
    /// Undoes the last edit operation.
    /// </summary>
    public void Undo()
    {
        if (_undoStack.Count == 0 || _analysis == null)
            return;

        var operation = _undoStack.Pop();
        ApplyUndoOperation(operation);
        _redoStack.Push(operation);

        HasChanges = _undoStack.Count > 0;
        UndoStateChanged?.Invoke(this, EventArgs.Empty);
        AnalysisChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Redoes the last undone operation.
    /// </summary>
    public void Redo()
    {
        if (_redoStack.Count == 0 || _analysis == null)
            return;

        var operation = _redoStack.Pop();
        ApplyRedoOperation(operation);
        _undoStack.Push(operation);

        HasChanges = true;
        UndoStateChanged?.Invoke(this, EventArgs.Empty);
        AnalysisChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Gets a description of the operation that would be undone.
    /// </summary>
    public string? GetUndoDescription()
    {
        return _undoStack.Count > 0 ? _undoStack.Peek().Description : null;
    }

    /// <summary>
    /// Gets a description of the operation that would be redone.
    /// </summary>
    public string? GetRedoDescription()
    {
        return _redoStack.Count > 0 ? _redoStack.Peek().Description : null;
    }

    private void PushUndoOperation(EditOperation operation)
    {
        _undoStack.Push(operation);
        _redoStack.Clear();

        // Limit undo history size
        while (_undoStack.Count > MaxUndoSteps)
        {
            var excess = _undoStack.ToArray();
            _undoStack.Clear();
            for (int i = 0; i < MaxUndoSteps; i++)
            {
                _undoStack.Push(excess[excess.Length - 1 - i]);
            }
        }

        UndoStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyUndoOperation(EditOperation operation)
    {
        if (_analysis == null || operation.Note == null)
            return;

        switch (operation.Type)
        {
            case EditOperationType.PitchChange:
                operation.Note.Pitch = operation.OldPitch;
                break;

            case EditOperationType.TimeChange:
                operation.Note.StartTime = operation.OldStart;
                operation.Note.EndTime = operation.OldEnd;
                operation.Note.StartSample = (long)(operation.OldStart * _sampleRate);
                operation.Note.EndSample = (long)(operation.OldEnd * _sampleRate);
                if (operation.VoiceIndex < _analysis.Voices.Count)
                {
                    _analysis.Voices[operation.VoiceIndex].SortNotes();
                }
                break;

            case EditOperationType.FormantChange:
                operation.Note.Formant = operation.OldFormant;
                break;

            case EditOperationType.NoteSplit:
                // Remove the second note and restore original timing
                if (operation.AdditionalNotes.Count > 0)
                {
                    var secondNote = operation.AdditionalNotes[0];
                    var voice = _analysis.Voices.FirstOrDefault(v => v.Notes.Any(n => n.Id == secondNote.Id));
                    voice?.RemoveNoteById(secondNote.Id);

                    operation.Note.EndTime = operation.OldEnd;
                    operation.Note.EndSample = (long)(operation.OldEnd * _sampleRate);
                }
                break;

            case EditOperationType.NoteMerge:
                // Restore original timing and re-add second note
                operation.Note.EndTime = operation.OldEnd;
                operation.Note.EndSample = (long)(operation.OldEnd * _sampleRate);

                if (operation.AdditionalNotes.Count > 0 && operation.VoiceIndex < _analysis.Voices.Count)
                {
                    var secondNote = operation.AdditionalNotes[0];
                    _analysis.Voices[operation.VoiceIndex].AddNote(secondNote);
                }
                break;

            case EditOperationType.NoteDelete:
                // Re-add the deleted note
                if (operation.VoiceIndex < _analysis.Voices.Count)
                {
                    _analysis.Voices[operation.VoiceIndex].AddNote(operation.Note);
                }
                break;
        }
    }

    private void ApplyRedoOperation(EditOperation operation)
    {
        if (_analysis == null || operation.Note == null)
            return;

        switch (operation.Type)
        {
            case EditOperationType.PitchChange:
                operation.Note.Pitch = operation.NewPitch;
                break;

            case EditOperationType.TimeChange:
                operation.Note.StartTime = operation.NewStart;
                operation.Note.EndTime = operation.NewEnd;
                operation.Note.StartSample = (long)(operation.NewStart * _sampleRate);
                operation.Note.EndSample = (long)(operation.NewEnd * _sampleRate);
                if (operation.VoiceIndex < _analysis.Voices.Count)
                {
                    _analysis.Voices[operation.VoiceIndex].SortNotes();
                }
                break;

            case EditOperationType.FormantChange:
                operation.Note.Formant = operation.NewFormant;
                break;

            case EditOperationType.NoteSplit:
                // Re-split: modify first note and add second
                operation.Note.EndTime = operation.NewEnd;
                operation.Note.EndSample = (long)(operation.NewEnd * _sampleRate);

                if (operation.AdditionalNotes.Count > 0 && operation.VoiceIndex < _analysis.Voices.Count)
                {
                    _analysis.Voices[operation.VoiceIndex].AddNote(operation.AdditionalNotes[0]);
                }
                break;

            case EditOperationType.NoteMerge:
                // Re-merge: extend first note and remove second
                operation.Note.EndTime = operation.NewEnd;
                operation.Note.EndSample = (long)(operation.NewEnd * _sampleRate);

                if (operation.AdditionalNotes.Count > 0)
                {
                    var voice = _analysis.Voices.FirstOrDefault(v =>
                        v.Notes.Any(n => n.Id == operation.AdditionalNotes[0].Id));
                    voice?.RemoveNoteById(operation.AdditionalNotes[0].Id);
                }
                break;

            case EditOperationType.NoteDelete:
                // Re-delete the note
                var noteVoice = _analysis.Voices.FirstOrDefault(v => v.Notes.Contains(operation.Note));
                noteVoice?.RemoveNote(operation.Note);
                break;
        }
    }

    private void MarkChanged()
    {
        HasChanges = true;
    }

    private static int ParseRootNote(string rootNote)
    {
        rootNote = rootNote.Trim().ToUpper();

        int note = rootNote[0] switch
        {
            'C' => 0,
            'D' => 2,
            'E' => 4,
            'F' => 5,
            'G' => 7,
            'A' => 9,
            'B' => 11,
            _ => 0
        };

        if (rootNote.Length > 1)
        {
            if (rootNote[1] == '#')
                note = (note + 1) % 12;
            else if (rootNote[1] == 'B')
                note = (note + 11) % 12;
        }

        return note;
    }

    private static int[] GetScaleNotes(int root, ScaleType scaleType)
    {
        int[] intervals = scaleType switch
        {
            ScaleType.Major => new[] { 0, 2, 4, 5, 7, 9, 11 },
            ScaleType.NaturalMinor => new[] { 0, 2, 3, 5, 7, 8, 10 },
            ScaleType.HarmonicMinor => new[] { 0, 2, 3, 5, 7, 8, 11 },
            ScaleType.MelodicMinor => new[] { 0, 2, 3, 5, 7, 9, 11 },
            ScaleType.Dorian => new[] { 0, 2, 3, 5, 7, 9, 10 },
            ScaleType.Phrygian => new[] { 0, 1, 3, 5, 7, 8, 10 },
            ScaleType.Lydian => new[] { 0, 2, 4, 6, 7, 9, 11 },
            ScaleType.Mixolydian => new[] { 0, 2, 4, 5, 7, 9, 10 },
            ScaleType.Locrian => new[] { 0, 1, 3, 5, 6, 8, 10 },
            ScaleType.PentatonicMajor => new[] { 0, 2, 4, 7, 9 },
            ScaleType.PentatonicMinor => new[] { 0, 3, 5, 7, 10 },
            ScaleType.Blues => new[] { 0, 3, 5, 6, 7, 10 },
            ScaleType.Chromatic => new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 },
            _ => new[] { 0, 2, 4, 5, 7, 9, 11 }
        };

        return intervals.Select(i => (i + root) % 12).ToArray();
    }

    /// <summary>
    /// Disposes resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _originalAudio = null;
        _processedAudio = null;
        _analysis = null;
        _undoStack.Clear();
        _redoStack.Clear();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
