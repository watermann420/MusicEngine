//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: MIDI note repeater/ratchet effect for tempo-synchronized note repetitions with velocity and pitch manipulation.


using System;
using System.Collections.Generic;


namespace MusicEngine.Core.MIDI;


/// <summary>
/// Velocity ramping modes for note repeats.
/// </summary>
public enum VelocityRampMode
{
    /// <summary>No velocity change between repeats.</summary>
    None,
    /// <summary>Velocity increases with each repeat.</summary>
    Up,
    /// <summary>Velocity decreases with each repeat.</summary>
    Down,
    /// <summary>Random velocity variation on each repeat.</summary>
    Random,
    /// <summary>Velocity alternates between high and low.</summary>
    Alternate,
    /// <summary>Custom velocity curve using VelocityCurve property.</summary>
    Custom
}


/// <summary>
/// MIDI note repeater/ratchet effect that creates tempo-synchronized note repetitions.
/// Useful for ratchet effects, note rolls, and rhythmic variations.
/// </summary>
/// <remarks>
/// The NoteRepeater provides:
/// - Tempo-synchronized repeat rates (1/4, 1/8, 1/16, 1/32, etc.)
/// - Configurable repeat count (1-32 repeats)
/// - Velocity decay or ramping (up, down, random, alternate)
/// - Pitch shifting per repeat (chromatic or custom intervals)
/// - Note filtering (trigger on specific notes or all notes)
/// - Gate length control for repeat notes
/// - Swing/humanization options
///
/// Usage:
/// 1. Create NoteRepeater and configure parameters
/// 2. Call SetTempo() to sync to project tempo
/// 3. Call ProcessNoteOn/ProcessNoteOff for incoming notes
/// 4. The effect outputs repeated notes via the callback
/// </remarks>
public class NoteRepeater : IMidiEffect
{
    private readonly Dictionary<int, RepeatingNote> _activeNotes = new();
    private readonly HashSet<int> _triggerNotes = new();
    private readonly Random _random = new();
    private readonly object _lock = new();
    private double _bpm = 120.0;

    /// <summary>
    /// Gets or sets whether the effect is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the rate at which notes are repeated (tempo-synchronized).
    /// </summary>
    public NoteDivision Rate { get; set; } = NoteDivision.Sixteenth;

    /// <summary>
    /// Gets or sets the number of times to repeat each note (1-32).
    /// A value of 1 means no extra repeats (just the original note).
    /// </summary>
    public int RepeatCount
    {
        get => _repeatCount;
        set => _repeatCount = Math.Clamp(value, 1, 32);
    }
    private int _repeatCount = 4;

    /// <summary>
    /// Gets or sets the velocity decay factor per repeat (0.0-1.0).
    /// 1.0 = no decay, 0.5 = half velocity each repeat, etc.
    /// Only used when VelocityRamp is None or Down.
    /// </summary>
    public double VelocityDecay
    {
        get => _velocityDecay;
        set => _velocityDecay = Math.Clamp(value, 0.0, 1.0);
    }
    private double _velocityDecay = 0.85;

    /// <summary>
    /// Gets or sets the velocity ramping mode.
    /// </summary>
    public VelocityRampMode VelocityRamp { get; set; } = VelocityRampMode.Down;

    /// <summary>
    /// Gets or sets the minimum velocity for decaying notes (1-127).
    /// Notes will not decay below this threshold.
    /// </summary>
    public int MinVelocity
    {
        get => _minVelocity;
        set => _minVelocity = Math.Clamp(value, 1, 127);
    }
    private int _minVelocity = 20;

    /// <summary>
    /// Gets or sets the maximum velocity for ramping up (1-127).
    /// </summary>
    public int MaxVelocity
    {
        get => _maxVelocity;
        set => _maxVelocity = Math.Clamp(value, 1, 127);
    }
    private int _maxVelocity = 127;

    /// <summary>
    /// Gets or sets the velocity variation range for random mode (0-64).
    /// </summary>
    public int VelocityVariation
    {
        get => _velocityVariation;
        set => _velocityVariation = Math.Clamp(value, 0, 64);
    }
    private int _velocityVariation = 20;

    /// <summary>
    /// Gets or sets the pitch shift in semitones per repeat (-24 to +24).
    /// Positive values shift up, negative values shift down.
    /// </summary>
    public int PitchShift
    {
        get => _pitchShift;
        set => _pitchShift = Math.Clamp(value, -24, 24);
    }
    private int _pitchShift = 0;

    /// <summary>
    /// Gets or sets custom pitch intervals for each repeat.
    /// When set, overrides PitchShift with custom intervals.
    /// Index corresponds to repeat number (0 = first repeat).
    /// </summary>
    public int[]? CustomPitchPattern { get; set; }

    /// <summary>
    /// Gets or sets the gate length for repeated notes (0.1-1.0).
    /// 1.0 = notes last until next repeat, lower values create staccato effect.
    /// </summary>
    public double GateLength
    {
        get => _gateLength;
        set => _gateLength = Math.Clamp(value, 0.1, 1.0);
    }
    private double _gateLength = 0.8;

    /// <summary>
    /// Gets or sets whether to filter which notes trigger repeats.
    /// When true, only notes in TriggerNotes set will be repeated.
    /// When false, all notes are repeated.
    /// </summary>
    public bool FilterByNote { get; set; } = false;

    /// <summary>
    /// Gets or sets the swing amount (0.0-1.0).
    /// Delays every other repeat for shuffle/swing feel.
    /// </summary>
    public double Swing
    {
        get => _swing;
        set => _swing = Math.Clamp(value, 0.0, 1.0);
    }
    private double _swing = 0.0;

    /// <summary>
    /// Gets or sets the timing humanization amount (0.0-1.0).
    /// Adds random timing variation to repeats.
    /// </summary>
    public double Humanize
    {
        get => _humanize;
        set => _humanize = Math.Clamp(value, 0.0, 1.0);
    }
    private double _humanize = 0.0;

    /// <summary>
    /// Gets or sets whether the original note is included in output.
    /// When true, the original note is output before repeats.
    /// </summary>
    public bool IncludeOriginal { get; set; } = true;

    /// <summary>
    /// Gets or sets the custom velocity curve for VelocityRamp.Custom mode.
    /// Values should be normalized (0.0-1.0), length should match RepeatCount.
    /// </summary>
    public double[]? VelocityCurve { get; set; }

    /// <summary>
    /// Gets the set of notes that will trigger repeats when FilterByNote is true.
    /// </summary>
    public ISet<int> TriggerNotes => _triggerNotes;

    /// <summary>
    /// Event fired when a repeated note is triggered.
    /// </summary>
    public event EventHandler<NoteRepeatEventArgs>? NoteRepeated;

    /// <summary>
    /// Creates a new NoteRepeater with default settings.
    /// </summary>
    public NoteRepeater()
    {
    }

    /// <summary>
    /// Creates a new NoteRepeater with specified rate and repeat count.
    /// </summary>
    /// <param name="rate">The note division for repeat rate.</param>
    /// <param name="repeatCount">Number of repeats (1-32).</param>
    public NoteRepeater(NoteDivision rate, int repeatCount)
    {
        Rate = rate;
        RepeatCount = repeatCount;
    }

    /// <summary>
    /// Adds a note to the trigger filter.
    /// </summary>
    /// <param name="note">MIDI note number (0-127).</param>
    public void AddTriggerNote(int note)
    {
        MidiValidation.ValidateNote(note);
        lock (_lock)
        {
            _triggerNotes.Add(note);
        }
    }

    /// <summary>
    /// Removes a note from the trigger filter.
    /// </summary>
    /// <param name="note">MIDI note number (0-127).</param>
    public void RemoveTriggerNote(int note)
    {
        lock (_lock)
        {
            _triggerNotes.Remove(note);
        }
    }

    /// <summary>
    /// Clears all trigger notes.
    /// </summary>
    public void ClearTriggerNotes()
    {
        lock (_lock)
        {
            _triggerNotes.Clear();
        }
    }

    /// <summary>
    /// Sets trigger notes from a range of notes.
    /// </summary>
    /// <param name="lowNote">Lowest note in range (inclusive).</param>
    /// <param name="highNote">Highest note in range (inclusive).</param>
    public void SetTriggerRange(int lowNote, int highNote)
    {
        MidiValidation.ValidateNote(lowNote);
        MidiValidation.ValidateNote(highNote);

        lock (_lock)
        {
            _triggerNotes.Clear();
            for (int note = lowNote; note <= highNote; note++)
            {
                _triggerNotes.Add(note);
            }
        }
    }

    /// <inheritdoc />
    public void ProcessNoteOn(int note, int velocity, double time, Action<int, int, double> outputNoteOn)
    {
        MidiValidation.ValidateNote(note);
        MidiValidation.ValidateVelocity(velocity);

        if (!Enabled)
        {
            outputNoteOn(note, velocity, time);
            return;
        }

        // Check if this note should trigger repeats
        bool shouldRepeat;
        lock (_lock)
        {
            shouldRepeat = !FilterByNote || _triggerNotes.Contains(note);
        }

        if (!shouldRepeat)
        {
            outputNoteOn(note, velocity, time);
            return;
        }

        lock (_lock)
        {
            // Calculate step duration
            double stepDuration = GetStepDurationSeconds();

            // Create repeating note entry
            var repeatingNote = new RepeatingNote
            {
                OriginalNote = note,
                OriginalVelocity = velocity,
                StartTime = time,
                StepDuration = stepDuration
            };

            _activeNotes[note] = repeatingNote;

            // Output original note if enabled
            if (IncludeOriginal)
            {
                outputNoteOn(note, velocity, time);
                repeatingNote.OutputNotes.Add((note, time, time + (stepDuration * GateLength)));
            }

            // Generate repeated notes
            double currentTime = time;
            int currentVelocity = velocity;

            for (int i = 0; i < RepeatCount - (IncludeOriginal ? 1 : 0); i++)
            {
                // Calculate timing for this repeat
                double repeatDelay = stepDuration;

                // Apply swing to every other repeat
                if (Swing > 0 && (i % 2) == 0)
                {
                    repeatDelay += stepDuration * Swing * 0.5;
                }

                // Apply humanization
                if (Humanize > 0)
                {
                    double maxVariation = stepDuration * Humanize * 0.1;
                    repeatDelay += (_random.NextDouble() - 0.5) * 2 * maxVariation;
                }

                currentTime += repeatDelay;

                // Calculate velocity for this repeat
                int repeatVelocity = CalculateRepeatVelocity(velocity, i, RepeatCount);

                // Calculate pitch for this repeat
                int repeatNote = CalculateRepeatPitch(note, i);

                // Validate note is in MIDI range
                if (repeatNote < 0 || repeatNote > 127)
                {
                    continue;
                }

                // Output the repeated note
                double noteOffTime = currentTime + (stepDuration * GateLength);
                outputNoteOn(repeatNote, repeatVelocity, currentTime);
                repeatingNote.OutputNotes.Add((repeatNote, currentTime, noteOffTime));

                // Fire event
                NoteRepeated?.Invoke(this, new NoteRepeatEventArgs(
                    note, repeatNote, repeatVelocity, i + 1, currentTime));

                currentVelocity = repeatVelocity;
            }
        }
    }

    /// <inheritdoc />
    public void ProcessNoteOff(int note, double time, Action<int, double> outputNoteOff)
    {
        MidiValidation.ValidateNote(note);

        if (!Enabled)
        {
            outputNoteOff(note, time);
            return;
        }

        lock (_lock)
        {
            if (_activeNotes.TryGetValue(note, out var repeatingNote))
            {
                // Output note offs for all generated notes
                foreach (var (outputNote, _, noteOffTime) in repeatingNote.OutputNotes)
                {
                    // Use the scheduled note off time or the current time, whichever is later
                    double actualOffTime = Math.Max(time, noteOffTime);
                    outputNoteOff(outputNote, actualOffTime);
                }

                _activeNotes.Remove(note);
            }
            else
            {
                // Pass through if not a repeated note
                outputNoteOff(note, time);
            }
        }
    }

    /// <inheritdoc />
    public void SetTempo(double bpm)
    {
        if (bpm > 0)
        {
            _bpm = bpm;
        }
    }

    /// <inheritdoc />
    public void Reset()
    {
        lock (_lock)
        {
            _activeNotes.Clear();
        }
    }

    private int CalculateRepeatVelocity(int originalVelocity, int repeatIndex, int totalRepeats)
    {
        int velocity;

        switch (VelocityRamp)
        {
            case VelocityRampMode.None:
                velocity = originalVelocity;
                break;

            case VelocityRampMode.Down:
                // Exponential decay
                double decayFactor = Math.Pow(VelocityDecay, repeatIndex + 1);
                velocity = (int)Math.Round(originalVelocity * decayFactor);
                velocity = Math.Max(velocity, MinVelocity);
                break;

            case VelocityRampMode.Up:
                // Linear ramp up from original to max
                double upRatio = (double)(repeatIndex + 1) / totalRepeats;
                velocity = (int)Math.Round(originalVelocity + (MaxVelocity - originalVelocity) * upRatio);
                break;

            case VelocityRampMode.Random:
                // Random variation around original
                int variation = _random.Next(-VelocityVariation, VelocityVariation + 1);
                velocity = originalVelocity + variation;
                break;

            case VelocityRampMode.Alternate:
                // Alternate between original and reduced velocity
                if ((repeatIndex % 2) == 0)
                {
                    velocity = originalVelocity;
                }
                else
                {
                    velocity = (int)Math.Round(originalVelocity * VelocityDecay);
                }
                break;

            case VelocityRampMode.Custom:
                if (VelocityCurve != null && repeatIndex < VelocityCurve.Length)
                {
                    velocity = (int)Math.Round(originalVelocity * VelocityCurve[repeatIndex]);
                }
                else
                {
                    velocity = originalVelocity;
                }
                break;

            default:
                velocity = originalVelocity;
                break;
        }

        // Clamp to valid MIDI range
        return Math.Clamp(velocity, 1, 127);
    }

    private int CalculateRepeatPitch(int originalNote, int repeatIndex)
    {
        // Use custom pattern if available
        if (CustomPitchPattern != null && repeatIndex < CustomPitchPattern.Length)
        {
            return originalNote + CustomPitchPattern[repeatIndex];
        }

        // Use linear pitch shift
        return originalNote + (PitchShift * (repeatIndex + 1));
    }

    private double GetStepDurationSeconds()
    {
        double beatsPerSecond = _bpm / 60.0;
        double beatsPerStep = GetBeatsForDivision(Rate);
        return beatsPerStep / beatsPerSecond;
    }

    private static double GetBeatsForDivision(NoteDivision division)
    {
        return division switch
        {
            NoteDivision.Whole => 4.0,
            NoteDivision.Half => 2.0,
            NoteDivision.Quarter => 1.0,
            NoteDivision.Eighth => 0.5,
            NoteDivision.Sixteenth => 0.25,
            NoteDivision.ThirtySecond => 0.125,
            NoteDivision.DottedHalf => 3.0,
            NoteDivision.DottedQuarter => 1.5,
            NoteDivision.DottedEighth => 0.75,
            NoteDivision.TripletQuarter => 2.0 / 3.0,
            NoteDivision.TripletEighth => 1.0 / 3.0,
            NoteDivision.TripletSixteenth => 1.0 / 6.0,
            _ => 0.25
        };
    }

    private class RepeatingNote
    {
        public int OriginalNote { get; init; }
        public int OriginalVelocity { get; init; }
        public double StartTime { get; init; }
        public double StepDuration { get; init; }
        public List<(int Note, double OnTime, double OffTime)> OutputNotes { get; } = new();
    }
}


/// <summary>
/// Event arguments for note repeat events.
/// </summary>
public class NoteRepeatEventArgs : EventArgs
{
    /// <summary>
    /// Gets the original MIDI note number that triggered the repeat.
    /// </summary>
    public int OriginalNote { get; }

    /// <summary>
    /// Gets the MIDI note number of the repeated note (may differ due to pitch shift).
    /// </summary>
    public int RepeatedNote { get; }

    /// <summary>
    /// Gets the velocity of the repeated note.
    /// </summary>
    public int Velocity { get; }

    /// <summary>
    /// Gets the repeat index (1-based).
    /// </summary>
    public int RepeatIndex { get; }

    /// <summary>
    /// Gets the time when the repeat was triggered.
    /// </summary>
    public double Time { get; }

    /// <summary>
    /// Creates a new NoteRepeatEventArgs instance.
    /// </summary>
    public NoteRepeatEventArgs(int originalNote, int repeatedNote, int velocity, int repeatIndex, double time)
    {
        OriginalNote = originalNote;
        RepeatedNote = repeatedNote;
        Velocity = velocity;
        RepeatIndex = repeatIndex;
        Time = time;
    }
}
