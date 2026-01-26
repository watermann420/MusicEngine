// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using System;
using System.Collections.Generic;
using System.Linq;


namespace MusicEngine.Core;


/// <summary>
/// Interface for MIDI effects that process note events in real-time.
/// </summary>
public interface IMidiEffect
{
    /// <summary>
    /// Process a Note On event and output resulting notes.
    /// </summary>
    /// <param name="note">MIDI note number (0-127)</param>
    /// <param name="velocity">Note velocity (0-127)</param>
    /// <param name="time">Current time in seconds</param>
    /// <param name="outputNoteOn">Callback to output Note On events (note, velocity, time)</param>
    void ProcessNoteOn(int note, int velocity, double time, Action<int, int, double> outputNoteOn);

    /// <summary>
    /// Process a Note Off event and output resulting note offs.
    /// </summary>
    /// <param name="note">MIDI note number (0-127)</param>
    /// <param name="time">Current time in seconds</param>
    /// <param name="outputNoteOff">Callback to output Note Off events (note, time)</param>
    void ProcessNoteOff(int note, double time, Action<int, double> outputNoteOff);

    /// <summary>
    /// Set the current tempo for tempo-synced effects.
    /// </summary>
    /// <param name="bpm">Tempo in beats per minute</param>
    void SetTempo(double bpm);

    /// <summary>
    /// Reset the effect state.
    /// </summary>
    void Reset();
}


/// <summary>
/// Note division values for tempo-synced effects.
/// </summary>
public enum NoteDivision
{
    /// <summary>Whole note (1/1)</summary>
    Whole = 1,
    /// <summary>Half note (1/2)</summary>
    Half = 2,
    /// <summary>Quarter note (1/4)</summary>
    Quarter = 4,
    /// <summary>Eighth note (1/8)</summary>
    Eighth = 8,
    /// <summary>Sixteenth note (1/16)</summary>
    Sixteenth = 16,
    /// <summary>Thirty-second note (1/32)</summary>
    ThirtySecond = 32,
    /// <summary>Dotted half note</summary>
    DottedHalf = 3,
    /// <summary>Dotted quarter note</summary>
    DottedQuarter = 6,
    /// <summary>Dotted eighth note</summary>
    DottedEighth = 12,
    /// <summary>Triplet quarter note</summary>
    TripletQuarter = 5,
    /// <summary>Triplet eighth note</summary>
    TripletEighth = 10,
    /// <summary>Triplet sixteenth note</summary>
    TripletSixteenth = 20
}


/// <summary>
/// Chord types for the MidiChord effect.
/// </summary>
public enum MidiChordType
{
    /// <summary>Octave above (+12 semitones)</summary>
    Octave,
    /// <summary>Perfect fifth (+7 semitones)</summary>
    Fifth,
    /// <summary>Major triad (+4, +7)</summary>
    Major,
    /// <summary>Minor triad (+3, +7)</summary>
    Minor,
    /// <summary>Suspended second (+2, +7)</summary>
    Sus2,
    /// <summary>Suspended fourth (+5, +7)</summary>
    Sus4,
    /// <summary>Major seventh (+4, +7, +11)</summary>
    Major7,
    /// <summary>Minor seventh (+3, +7, +10)</summary>
    Minor7,
    /// <summary>Dominant seventh (+4, +7, +10)</summary>
    Dominant7,
    /// <summary>Diminished triad (+3, +6)</summary>
    Diminished,
    /// <summary>Augmented triad (+4, +8)</summary>
    Augmented,
    /// <summary>Power chord (root + fifth)</summary>
    Power,
    /// <summary>Add9 chord (+4, +7, +14)</summary>
    Add9,
    /// <summary>Custom intervals specified by user</summary>
    Custom
}


/// <summary>
/// Arpeggiator patterns for MidiArpeggiator.
/// </summary>
public enum MidiArpPattern
{
    /// <summary>Play notes in ascending order</summary>
    Up,
    /// <summary>Play notes in descending order</summary>
    Down,
    /// <summary>Play notes up then down</summary>
    UpDown,
    /// <summary>Play notes down then up</summary>
    DownUp,
    /// <summary>Play notes in random order</summary>
    Random,
    /// <summary>Play notes in the order they were pressed</summary>
    Order
}


/// <summary>
/// MIDI delay effect that creates echo/delay on MIDI notes.
/// Supports tempo-synced delay times with feedback and velocity decay.
/// </summary>
public class MidiDelay : IMidiEffect
{
    private readonly List<ScheduledNote> _scheduledNotes = new();
    private readonly Dictionary<int, List<int>> _activeNotes = new(); // Original note -> delayed notes
    private readonly object _lock = new();
    private double _bpm = 120.0;

    /// <summary>
    /// Gets or sets whether the effect is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the delay time in milliseconds (when not synced to tempo).
    /// </summary>
    public double DelayTimeMs { get; set; } = 500.0;

    /// <summary>
    /// Gets or sets the note division for tempo-synced delay.
    /// </summary>
    public NoteDivision SyncDivision { get; set; } = NoteDivision.Eighth;

    /// <summary>
    /// Gets or sets whether to sync delay time to tempo.
    /// </summary>
    public bool SyncToTempo { get; set; } = true;

    /// <summary>
    /// Gets or sets the feedback amount (0.0-1.0). Higher values produce more echoes.
    /// </summary>
    public double Feedback { get; set; } = 0.5;

    /// <summary>
    /// Gets or sets the velocity decay per repeat (0.0-1.0).
    /// Each echo will have velocity multiplied by this value.
    /// </summary>
    public double VelocityDecay { get; set; } = 0.7;

    /// <summary>
    /// Gets or sets the maximum number of echoes to generate.
    /// </summary>
    public int MaxRepeats { get; set; } = 8;

    /// <summary>
    /// Gets or sets the dry/wet mix (0.0 = only original, 1.0 = only delayed).
    /// </summary>
    public double Mix { get; set; } = 0.5;

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

        lock (_lock)
        {
            // Always output the original note (dry signal)
            outputNoteOn(note, velocity, time);

            // Track active notes for note-off handling
            if (!_activeNotes.ContainsKey(note))
            {
                _activeNotes[note] = new List<int>();
            }

            // Calculate delay time
            double delaySeconds = GetDelayTimeSeconds();

            // Generate delayed echoes
            double currentTime = time;
            double currentVelocity = velocity;

            for (int i = 0; i < MaxRepeats; i++)
            {
                currentTime += delaySeconds;
                currentVelocity *= VelocityDecay;

                // Stop if velocity is too low
                int outputVelocity = (int)Math.Round(currentVelocity);
                if (outputVelocity < 1)
                    break;

                // Check feedback probability
                if (i > 0 && _random.NextDouble() > Feedback)
                    break;

                outputVelocity = Math.Min(127, outputVelocity);

                // Schedule the delayed note
                _scheduledNotes.Add(new ScheduledNote
                {
                    Note = note,
                    Velocity = outputVelocity,
                    Time = currentTime,
                    IsNoteOn = true,
                    OriginalNote = note
                });

                _activeNotes[note].Add(note);

                // Output the delayed note
                outputNoteOn(note, outputVelocity, currentTime);
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
            // Output original note off
            outputNoteOff(note, time);

            // Schedule delayed note offs
            double delaySeconds = GetDelayTimeSeconds();

            if (_activeNotes.TryGetValue(note, out var delayedNotes))
            {
                double currentTime = time;
                for (int i = 0; i < delayedNotes.Count; i++)
                {
                    currentTime += delaySeconds;
                    outputNoteOff(note, currentTime);
                }
                delayedNotes.Clear();
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
            _scheduledNotes.Clear();
            _activeNotes.Clear();
        }
    }

    private double GetDelayTimeSeconds()
    {
        if (!SyncToTempo)
        {
            return DelayTimeMs / 1000.0;
        }

        // Calculate delay based on tempo and note division
        double beatsPerSecond = _bpm / 60.0;
        double beatsPerNote = GetBeatsForDivision(SyncDivision);
        return beatsPerNote / beatsPerSecond;
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
            _ => 0.5
        };
    }

    private readonly Random _random = new();

    private class ScheduledNote
    {
        public int Note { get; init; }
        public int Velocity { get; init; }
        public double Time { get; init; }
        public bool IsNoteOn { get; init; }
        public int OriginalNote { get; init; }
    }
}


/// <summary>
/// Enhanced MIDI arpeggiator that creates automatic note patterns from held notes.
/// Supports multiple patterns, tempo sync, gate length, octave range, and swing.
/// </summary>
public class MidiArpeggiator : IMidiEffect
{
    private readonly List<HeldNote> _heldNotes = new();
    private readonly List<int> _currentPattern = new();
    private readonly Random _random = new();
    private readonly object _lock = new();

    private int _patternIndex;
    private bool _ascending = true;
    private double _bpm = 120.0;
    private double _lastTriggerTime;
    private int _lastPlayedNote = -1;

    /// <summary>
    /// Gets or sets whether the effect is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the arpeggiator pattern.
    /// </summary>
    public MidiArpPattern Pattern { get; set; } = MidiArpPattern.Up;

    /// <summary>
    /// Gets or sets the rate note division (synced to tempo).
    /// </summary>
    public NoteDivision Rate { get; set; } = NoteDivision.Sixteenth;

    /// <summary>
    /// Gets or sets the gate length as percentage of step (0.0-1.0).
    /// </summary>
    public double Gate { get; set; } = 0.8;

    /// <summary>
    /// Gets or sets the octave range (0 = same octave only, 1 = +1 octave, etc.).
    /// </summary>
    public int OctaveRange { get; set; } = 1;

    /// <summary>
    /// Gets or sets the swing amount (0.0-1.0). Delays every other note.
    /// </summary>
    public double Swing { get; set; } = 0.0;

    /// <summary>
    /// Gets or sets the velocity (0-127, or -1 to use input velocity).
    /// </summary>
    public int Velocity { get; set; } = -1;

    /// <summary>
    /// Gets or sets whether to latch notes (keep playing after note off).
    /// </summary>
    public bool Latch { get; set; } = false;

    /// <summary>
    /// Event fired when the arpeggiator triggers a note.
    /// </summary>
    public event EventHandler<MidiArpNoteEventArgs>? NotePlayed;

    /// <summary>
    /// Gets whether there are held notes to arpeggiate.
    /// </summary>
    public bool HasNotes
    {
        get
        {
            lock (_lock) { return _heldNotes.Count > 0; }
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

        lock (_lock)
        {
            // Add note to held notes if not already present
            if (!_heldNotes.Any(n => n.Note == note))
            {
                _heldNotes.Add(new HeldNote { Note = note, Velocity = velocity, Time = time });
                RebuildPattern();
            }

            // Trigger arpeggiator processing
            ProcessArpeggiator(time, outputNoteOn);
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

        if (Latch) return; // Don't remove notes in latch mode

        lock (_lock)
        {
            _heldNotes.RemoveAll(n => n.Note == note);

            if (_heldNotes.Count == 0)
            {
                // Turn off last played note
                if (_lastPlayedNote >= 0)
                {
                    outputNoteOff(_lastPlayedNote, time);
                    _lastPlayedNote = -1;
                }
                _patternIndex = 0;
                _ascending = true;
                _currentPattern.Clear();
            }
            else
            {
                RebuildPattern();
            }
        }
    }

    /// <summary>
    /// Process the arpeggiator at the current time and output notes.
    /// Call this method periodically (e.g., from a timer or audio callback).
    /// </summary>
    public void Process(double currentTime, Action<int, int, double> outputNoteOn, Action<int, double> outputNoteOff)
    {
        if (!Enabled) return;

        lock (_lock)
        {
            if (_currentPattern.Count == 0) return;

            double stepDuration = GetStepDurationSeconds();
            double swingOffset = GetSwingOffset(stepDuration);

            // Check if it's time for the next note
            double nextTriggerTime = _lastTriggerTime + stepDuration + swingOffset;

            if (currentTime >= nextTriggerTime || _lastTriggerTime == 0)
            {
                // Turn off previous note
                if (_lastPlayedNote >= 0)
                {
                    outputNoteOff(_lastPlayedNote, currentTime);
                }

                // Get next note
                int nextNote = GetNextNote();
                int outputVelocity = Velocity >= 0 ? Velocity : 100;

                // Output the note
                outputNoteOn(nextNote, outputVelocity, currentTime);
                _lastPlayedNote = nextNote;
                _lastTriggerTime = currentTime;

                // Fire event
                NotePlayed?.Invoke(this, new MidiArpNoteEventArgs(nextNote, outputVelocity, currentTime));

                // Schedule note off based on gate
                double noteOffTime = currentTime + (stepDuration * Gate);
                outputNoteOff(nextNote, noteOffTime);
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
            _heldNotes.Clear();
            _currentPattern.Clear();
            _patternIndex = 0;
            _ascending = true;
            _lastTriggerTime = 0;
            _lastPlayedNote = -1;
        }
    }

    /// <summary>
    /// Clear all held notes (useful for latch mode).
    /// </summary>
    public void Clear()
    {
        Reset();
    }

    private void ProcessArpeggiator(double time, Action<int, int, double> outputNoteOn)
    {
        if (_currentPattern.Count == 0) return;

        // Only trigger if this is the first note or enough time has passed
        if (_lastTriggerTime == 0)
        {
            int note = GetNextNote();
            int outputVelocity = Velocity >= 0 ? Velocity : 100;
            outputNoteOn(note, outputVelocity, time);
            _lastPlayedNote = note;
            _lastTriggerTime = time;
        }
    }

    private void RebuildPattern()
    {
        _currentPattern.Clear();

        if (_heldNotes.Count == 0) return;

        // Build base note list with octave expansion
        var notes = new List<int>();
        var sortedNotes = _heldNotes.OrderBy(n => n.Note).Select(n => n.Note).ToList();

        for (int octave = 0; octave <= OctaveRange; octave++)
        {
            foreach (var note in sortedNotes)
            {
                int transposedNote = note + (octave * 12);
                if (transposedNote <= 127)
                {
                    notes.Add(transposedNote);
                }
            }
        }

        // Apply pattern ordering
        switch (Pattern)
        {
            case MidiArpPattern.Up:
                _currentPattern.AddRange(notes);
                break;

            case MidiArpPattern.Down:
                notes.Reverse();
                _currentPattern.AddRange(notes);
                break;

            case MidiArpPattern.UpDown:
                _currentPattern.AddRange(notes);
                _ascending = true;
                break;

            case MidiArpPattern.DownUp:
                notes.Reverse();
                _currentPattern.AddRange(notes);
                _ascending = false;
                break;

            case MidiArpPattern.Random:
                _currentPattern.AddRange(notes);
                break;

            case MidiArpPattern.Order:
                // Use order of pressing with octave expansion
                for (int octave = 0; octave <= OctaveRange; octave++)
                {
                    foreach (var held in _heldNotes)
                    {
                        int transposedNote = held.Note + (octave * 12);
                        if (transposedNote <= 127)
                        {
                            _currentPattern.Add(transposedNote);
                        }
                    }
                }
                break;
        }

        // Reset pattern index if out of bounds
        if (_patternIndex >= _currentPattern.Count)
        {
            _patternIndex = 0;
        }
    }

    private int GetNextNote()
    {
        if (_currentPattern.Count == 0) return 60; // Default to middle C

        int note;

        switch (Pattern)
        {
            case MidiArpPattern.Up:
            case MidiArpPattern.Down:
            case MidiArpPattern.Order:
                note = _currentPattern[_patternIndex];
                _patternIndex = (_patternIndex + 1) % _currentPattern.Count;
                break;

            case MidiArpPattern.UpDown:
                note = _currentPattern[_patternIndex];
                if (_ascending)
                {
                    _patternIndex++;
                    if (_patternIndex >= _currentPattern.Count)
                    {
                        _patternIndex = Math.Max(0, _currentPattern.Count - 2);
                        _ascending = false;
                    }
                }
                else
                {
                    _patternIndex--;
                    if (_patternIndex < 0)
                    {
                        _patternIndex = Math.Min(1, _currentPattern.Count - 1);
                        _ascending = true;
                    }
                }
                break;

            case MidiArpPattern.DownUp:
                note = _currentPattern[_patternIndex];
                if (!_ascending)
                {
                    _patternIndex++;
                    if (_patternIndex >= _currentPattern.Count)
                    {
                        _patternIndex = Math.Max(0, _currentPattern.Count - 2);
                        _ascending = true;
                    }
                }
                else
                {
                    _patternIndex--;
                    if (_patternIndex < 0)
                    {
                        _patternIndex = Math.Min(1, _currentPattern.Count - 1);
                        _ascending = false;
                    }
                }
                break;

            case MidiArpPattern.Random:
                note = _currentPattern[_random.Next(_currentPattern.Count)];
                break;

            default:
                note = _currentPattern[0];
                break;
        }

        return note;
    }

    private double GetStepDurationSeconds()
    {
        double beatsPerSecond = _bpm / 60.0;
        double beatsPerStep = Rate switch
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
        return beatsPerStep / beatsPerSecond;
    }

    private double GetSwingOffset(double stepDuration)
    {
        if (Swing <= 0) return 0;

        // Apply swing to every other note
        bool isOffBeat = (_patternIndex % 2) == 1;
        return isOffBeat ? stepDuration * Swing * 0.5 : 0;
    }

    private class HeldNote
    {
        public int Note { get; init; }
        public int Velocity { get; init; }
        public double Time { get; init; }
    }
}


/// <summary>
/// Event args for MidiArpeggiator note events.
/// </summary>
public class MidiArpNoteEventArgs : EventArgs
{
    /// <summary>MIDI note number that was played.</summary>
    public int Note { get; }

    /// <summary>Velocity of the note.</summary>
    public int Velocity { get; }

    /// <summary>Time when the note was played.</summary>
    public double Time { get; }

    public MidiArpNoteEventArgs(int note, int velocity, double time)
    {
        Note = note;
        Velocity = velocity;
        Time = time;
    }
}


/// <summary>
/// MIDI chord effect that adds harmony notes to input notes.
/// Supports various chord types, custom intervals, velocity scaling, and strum effect.
/// </summary>
public class MidiChord : IMidiEffect
{
    private readonly Dictionary<int, List<int>> _activeChords = new(); // Root note -> chord notes
    private readonly object _lock = new();
    private double _bpm = 120.0;

    // Predefined chord intervals (semitones from root)
    private static readonly Dictionary<MidiChordType, int[]> ChordIntervals = new()
    {
        { MidiChordType.Octave, new[] { 12 } },
        { MidiChordType.Fifth, new[] { 7 } },
        { MidiChordType.Major, new[] { 4, 7 } },
        { MidiChordType.Minor, new[] { 3, 7 } },
        { MidiChordType.Sus2, new[] { 2, 7 } },
        { MidiChordType.Sus4, new[] { 5, 7 } },
        { MidiChordType.Major7, new[] { 4, 7, 11 } },
        { MidiChordType.Minor7, new[] { 3, 7, 10 } },
        { MidiChordType.Dominant7, new[] { 4, 7, 10 } },
        { MidiChordType.Diminished, new[] { 3, 6 } },
        { MidiChordType.Augmented, new[] { 4, 8 } },
        { MidiChordType.Power, new[] { 7, 12 } },
        { MidiChordType.Add9, new[] { 4, 7, 14 } }
    };

    /// <summary>
    /// Gets or sets whether the effect is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the chord type.
    /// </summary>
    public MidiChordType ChordType { get; set; } = MidiChordType.Major;

    /// <summary>
    /// Gets or sets custom intervals (semitones from root) when ChordType is Custom.
    /// </summary>
    public int[] CustomIntervals { get; set; } = Array.Empty<int>();

    /// <summary>
    /// Gets or sets the velocity scaling for added notes (0.0-1.0).
    /// 1.0 = same velocity as root, 0.5 = half velocity, etc.
    /// </summary>
    public double VelocityScale { get; set; } = 0.8;

    /// <summary>
    /// Gets or sets whether to include the root note in output.
    /// </summary>
    public bool IncludeRoot { get; set; } = true;

    /// <summary>
    /// Gets or sets the strum delay in milliseconds between chord notes.
    /// 0 = all notes play simultaneously.
    /// </summary>
    public double StrumDelayMs { get; set; } = 0.0;

    /// <summary>
    /// Gets or sets the strum direction (true = low to high, false = high to low).
    /// </summary>
    public bool StrumUp { get; set; } = true;

    /// <summary>
    /// Gets or sets the strum delay note division for tempo-synced strum.
    /// Set to null to use StrumDelayMs instead.
    /// </summary>
    public NoteDivision? StrumSyncDivision { get; set; } = null;

    /// <summary>
    /// Gets or sets the transpose amount in semitones for all chord notes (excluding root).
    /// </summary>
    public int Transpose { get; set; } = 0;

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

        lock (_lock)
        {
            var chordNotes = new List<int>();
            var intervals = GetIntervals();

            // Build chord notes
            if (IncludeRoot)
            {
                chordNotes.Add(note);
            }

            foreach (var interval in intervals)
            {
                int chordNote = note + interval + Transpose;
                if (chordNote >= 0 && chordNote <= 127)
                {
                    chordNotes.Add(chordNote);
                }
            }

            // Store active chord
            _activeChords[note] = chordNotes;

            // Calculate strum delay
            double strumDelay = GetStrumDelaySeconds();

            // Sort notes for strum direction
            var sortedNotes = StrumUp
                ? chordNotes.OrderBy(n => n).ToList()
                : chordNotes.OrderByDescending(n => n).ToList();

            // Output notes with strum
            for (int i = 0; i < sortedNotes.Count; i++)
            {
                int chordNote = sortedNotes[i];
                double noteTime = time + (i * strumDelay);

                // Calculate velocity (root uses original, others are scaled)
                int noteVelocity;
                if (chordNote == note)
                {
                    noteVelocity = velocity;
                }
                else
                {
                    noteVelocity = Math.Clamp((int)(velocity * VelocityScale), 1, 127);
                }

                outputNoteOn(chordNote, noteVelocity, noteTime);
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
            if (_activeChords.TryGetValue(note, out var chordNotes))
            {
                // Calculate strum delay for note offs
                double strumDelay = GetStrumDelaySeconds();

                // Sort notes for strum direction
                var sortedNotes = StrumUp
                    ? chordNotes.OrderBy(n => n).ToList()
                    : chordNotes.OrderByDescending(n => n).ToList();

                // Output note offs with strum
                for (int i = 0; i < sortedNotes.Count; i++)
                {
                    double noteTime = time + (i * strumDelay);
                    outputNoteOff(sortedNotes[i], noteTime);
                }

                _activeChords.Remove(note);
            }
            else
            {
                // No chord found, just pass through
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
            _activeChords.Clear();
        }
    }

    /// <summary>
    /// Set custom chord intervals.
    /// </summary>
    /// <param name="intervals">Array of semitone intervals from root</param>
    public void SetCustomChord(params int[] intervals)
    {
        ChordType = MidiChordType.Custom;
        CustomIntervals = intervals;
    }

    private int[] GetIntervals()
    {
        if (ChordType == MidiChordType.Custom)
        {
            return CustomIntervals;
        }

        return ChordIntervals.TryGetValue(ChordType, out var intervals)
            ? intervals
            : Array.Empty<int>();
    }

    private double GetStrumDelaySeconds()
    {
        if (StrumSyncDivision.HasValue)
        {
            double beatsPerSecond = _bpm / 60.0;
            double beatsPerStep = StrumSyncDivision.Value switch
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
                _ => 0.125
            };
            return beatsPerStep / beatsPerSecond;
        }

        return StrumDelayMs / 1000.0;
    }
}


/// <summary>
/// A chain of MIDI effects that processes notes through multiple effects in sequence.
/// </summary>
public class MidiEffectChain : IMidiEffect
{
    private readonly List<IMidiEffect> _effects = new();
    private readonly object _lock = new();

    /// <summary>
    /// Gets or sets whether the effect chain is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets the list of effects in the chain.
    /// </summary>
    public IReadOnlyList<IMidiEffect> Effects => _effects.AsReadOnly();

    /// <summary>
    /// Add an effect to the chain.
    /// </summary>
    public MidiEffectChain Add(IMidiEffect effect)
    {
        lock (_lock)
        {
            _effects.Add(effect);
        }
        return this;
    }

    /// <summary>
    /// Remove an effect from the chain.
    /// </summary>
    public bool Remove(IMidiEffect effect)
    {
        lock (_lock)
        {
            return _effects.Remove(effect);
        }
    }

    /// <summary>
    /// Clear all effects from the chain.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _effects.Clear();
        }
    }

    /// <inheritdoc />
    public void ProcessNoteOn(int note, int velocity, double time, Action<int, int, double> outputNoteOn)
    {
        if (!Enabled || _effects.Count == 0)
        {
            outputNoteOn(note, velocity, time);
            return;
        }

        lock (_lock)
        {
            // Create a list to collect output from each stage
            var pendingNotes = new List<(int note, int velocity, double time)> { (note, velocity, time) };

            foreach (var effect in _effects)
            {
                var nextStageNotes = new List<(int note, int velocity, double time)>();

                foreach (var (n, v, t) in pendingNotes)
                {
                    effect.ProcessNoteOn(n, v, t, (outNote, outVel, outTime) =>
                    {
                        nextStageNotes.Add((outNote, outVel, outTime));
                    });
                }

                pendingNotes = nextStageNotes;
            }

            // Output all final notes
            foreach (var (n, v, t) in pendingNotes)
            {
                outputNoteOn(n, v, t);
            }
        }
    }

    /// <inheritdoc />
    public void ProcessNoteOff(int note, double time, Action<int, double> outputNoteOff)
    {
        if (!Enabled || _effects.Count == 0)
        {
            outputNoteOff(note, time);
            return;
        }

        lock (_lock)
        {
            var pendingNoteOffs = new List<(int note, double time)> { (note, time) };

            foreach (var effect in _effects)
            {
                var nextStageNoteOffs = new List<(int note, double time)>();

                foreach (var (n, t) in pendingNoteOffs)
                {
                    effect.ProcessNoteOff(n, t, (outNote, outTime) =>
                    {
                        nextStageNoteOffs.Add((outNote, outTime));
                    });
                }

                pendingNoteOffs = nextStageNoteOffs;
            }

            // Output all final note offs
            foreach (var (n, t) in pendingNoteOffs)
            {
                outputNoteOff(n, t);
            }
        }
    }

    /// <inheritdoc />
    public void SetTempo(double bpm)
    {
        lock (_lock)
        {
            foreach (var effect in _effects)
            {
                effect.SetTempo(bpm);
            }
        }
    }

    /// <inheritdoc />
    public void Reset()
    {
        lock (_lock)
        {
            foreach (var effect in _effects)
            {
                effect.Reset();
            }
        }
    }
}
