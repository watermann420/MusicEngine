//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Advanced MIDI Arpeggiator with extended patterns, step sequencer mode,
//              velocity curves, accents, latch/hold modes, and per-step customization.

using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicEngine.Core.MIDI;

/// <summary>
/// Advanced arpeggiator pattern types with extended options.
/// </summary>
public enum AdvancedArpPattern
{
    /// <summary>Play notes in ascending order.</summary>
    Up,
    /// <summary>Play notes in descending order.</summary>
    Down,
    /// <summary>Play notes up then down (excluding repeated notes at boundaries).</summary>
    UpDown,
    /// <summary>Play notes down then up (excluding repeated notes at boundaries).</summary>
    DownUp,
    /// <summary>Play notes up then down (including repeated notes at boundaries).</summary>
    UpDownInclusive,
    /// <summary>Play notes down then up (including repeated notes at boundaries).</summary>
    DownUpInclusive,
    /// <summary>Play notes in random order.</summary>
    Random,
    /// <summary>Play notes in random order but never repeat the same note consecutively.</summary>
    RandomNoRepeat,
    /// <summary>Play notes in the order they were pressed.</summary>
    OrderPlayed,
    /// <summary>Play notes in reverse order of pressing.</summary>
    OrderPlayedReverse,
    /// <summary>Play all held notes as a chord.</summary>
    Chord,
    /// <summary>Use custom step pattern.</summary>
    Custom,
    /// <summary>Play root note, then alternating high/low notes converging.</summary>
    Converge,
    /// <summary>Play from center notes outward.</summary>
    Diverge,
    /// <summary>Play bass note then others ascending.</summary>
    ThumbUp,
    /// <summary>Play bass note then others descending.</summary>
    ThumbDown,
    /// <summary>Play top note then others ascending.</summary>
    PinkyUp,
    /// <summary>Play top note then others descending.</summary>
    PinkyDown,
    /// <summary>Spiral pattern - alternates between lowest unplayed and highest unplayed.</summary>
    Spiral,
    /// <summary>Skip pattern - plays every other note then fills in.</summary>
    Skip,
    /// <summary>Mirror pattern - plays up and down simultaneously from center.</summary>
    Mirror
}

/// <summary>
/// Velocity curve types for shaping note dynamics.
/// </summary>
public enum VelocityCurve
{
    /// <summary>Linear velocity response.</summary>
    Linear,
    /// <summary>Exponential curve - soft touch, strong at high velocities.</summary>
    Exponential,
    /// <summary>Logarithmic curve - quick response, gentle at high velocities.</summary>
    Logarithmic,
    /// <summary>S-curve - smooth transition with soft and loud extremes.</summary>
    SCurve,
    /// <summary>Fixed velocity - ignores input, uses set velocity.</summary>
    Fixed,
    /// <summary>Ramp up - velocity increases through pattern.</summary>
    RampUp,
    /// <summary>Ramp down - velocity decreases through pattern.</summary>
    RampDown,
    /// <summary>Wave - velocity oscillates through pattern.</summary>
    Wave
}

/// <summary>
/// Note duration options for the arpeggiator.
/// </summary>
public enum AdvancedArpRate
{
    /// <summary>Whole note (4 beats).</summary>
    Whole,
    /// <summary>Half note (2 beats).</summary>
    Half,
    /// <summary>Dotted quarter note (1.5 beats).</summary>
    DottedQuarter,
    /// <summary>Quarter note (1 beat).</summary>
    Quarter,
    /// <summary>Triplet quarter note (2/3 beat).</summary>
    TripletQuarter,
    /// <summary>Dotted eighth note (0.75 beats).</summary>
    DottedEighth,
    /// <summary>Eighth note (0.5 beats).</summary>
    Eighth,
    /// <summary>Triplet eighth note (1/3 beat).</summary>
    TripletEighth,
    /// <summary>Dotted sixteenth note (0.375 beats).</summary>
    DottedSixteenth,
    /// <summary>Sixteenth note (0.25 beats).</summary>
    Sixteenth,
    /// <summary>Triplet sixteenth note (1/6 beat).</summary>
    TripletSixteenth,
    /// <summary>Thirty-second note (0.125 beats).</summary>
    ThirtySecond,
    /// <summary>Triplet thirty-second note (1/12 beat).</summary>
    TripletThirtySecond
}

/// <summary>
/// Represents a single step in a custom arpeggiator pattern.
/// </summary>
public class ArpStep
{
    /// <summary>Whether this step is enabled (plays a note).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Note offset from the base pattern note (in semitones).</summary>
    public int NoteOffset { get; set; } = 0;

    /// <summary>Octave offset for this step (-4 to +4).</summary>
    public int OctaveOffset { get; set; } = 0;

    /// <summary>Velocity multiplier for this step (0.0 to 2.0, 1.0 = no change).</summary>
    public float VelocityMultiplier { get; set; } = 1.0f;

    /// <summary>Gate multiplier for this step (0.0 to 2.0, 1.0 = no change).</summary>
    public float GateMultiplier { get; set; } = 1.0f;

    /// <summary>Whether this step should tie to the next step.</summary>
    public bool Tie { get; set; } = false;

    /// <summary>Whether this step is an accent (uses AccentVelocity).</summary>
    public bool Accent { get; set; } = false;

    /// <summary>Probability that this step will play (0.0 to 1.0).</summary>
    public float Probability { get; set; } = 1.0f;

    /// <summary>Number of ratchet repeats within this step (1 = normal, 2+ = ratchet).</summary>
    public int Ratchet { get; set; } = 1;

    /// <summary>Slide/glide to the next note.</summary>
    public bool Slide { get; set; } = false;

    /// <summary>Skip this step (rest).</summary>
    public bool Skip { get; set; } = false;

    /// <summary>Creates a default enabled step.</summary>
    public ArpStep() { }

    /// <summary>Creates a step with specified parameters.</summary>
    public ArpStep(bool enabled, int noteOffset = 0, int octaveOffset = 0, float velocityMultiplier = 1.0f)
    {
        Enabled = enabled;
        NoteOffset = noteOffset;
        OctaveOffset = octaveOffset;
        VelocityMultiplier = velocityMultiplier;
    }

    /// <summary>Creates a copy of this step.</summary>
    public ArpStep Clone()
    {
        return new ArpStep
        {
            Enabled = Enabled,
            NoteOffset = NoteOffset,
            OctaveOffset = OctaveOffset,
            VelocityMultiplier = VelocityMultiplier,
            GateMultiplier = GateMultiplier,
            Tie = Tie,
            Accent = Accent,
            Probability = Probability,
            Ratchet = Ratchet,
            Slide = Slide,
            Skip = Skip
        };
    }
}

/// <summary>
/// Represents a custom step pattern for the arpeggiator.
/// </summary>
public class ArpStepPattern
{
    /// <summary>Name of this pattern.</summary>
    public string Name { get; set; } = "Custom";

    /// <summary>List of steps in this pattern.</summary>
    public List<ArpStep> Steps { get; } = new();

    /// <summary>Number of steps in this pattern.</summary>
    public int Length => Steps.Count;

    /// <summary>Creates an empty pattern.</summary>
    public ArpStepPattern() { }

    /// <summary>Creates a pattern with the specified number of steps.</summary>
    public ArpStepPattern(int stepCount)
    {
        for (int i = 0; i < stepCount; i++)
        {
            Steps.Add(new ArpStep());
        }
    }

    /// <summary>Creates a pattern with the given name and step count.</summary>
    public ArpStepPattern(string name, int stepCount) : this(stepCount)
    {
        Name = name;
    }

    /// <summary>Clears all steps and adds new default steps.</summary>
    public void Reset(int stepCount)
    {
        Steps.Clear();
        for (int i = 0; i < stepCount; i++)
        {
            Steps.Add(new ArpStep());
        }
    }

    /// <summary>Creates a deep copy of this pattern.</summary>
    public ArpStepPattern Clone()
    {
        var clone = new ArpStepPattern { Name = Name };
        foreach (var step in Steps)
        {
            clone.Steps.Add(step.Clone());
        }
        return clone;
    }
}

/// <summary>
/// Event arguments for advanced arpeggiator note events.
/// </summary>
public class AdvancedArpNoteEventArgs : EventArgs
{
    /// <summary>MIDI note number (0-127).</summary>
    public int Note { get; }

    /// <summary>Note velocity (0-127).</summary>
    public int Velocity { get; }

    /// <summary>Current step index in the pattern.</summary>
    public int StepIndex { get; }

    /// <summary>Note duration in beats.</summary>
    public double Duration { get; }

    /// <summary>Whether this note is tied to the next.</summary>
    public bool IsTied { get; }

    /// <summary>Whether this note has slide/glide enabled.</summary>
    public bool HasSlide { get; }

    /// <summary>Whether this is an accented note.</summary>
    public bool IsAccent { get; }

    /// <summary>Creates event args with note information.</summary>
    public AdvancedArpNoteEventArgs(int note, int velocity, int stepIndex = 0,
        double duration = 0.25, bool isTied = false, bool hasSlide = false, bool isAccent = false)
    {
        Note = note;
        Velocity = velocity;
        StepIndex = stepIndex;
        Duration = duration;
        IsTied = isTied;
        HasSlide = hasSlide;
        IsAccent = isAccent;
    }
}

/// <summary>
/// Advanced MIDI Arpeggiator with extended patterns, step sequencer mode,
/// velocity curves, accents, latch/hold modes, and per-step customization.
/// </summary>
public class MidiArpeggiatorAdvanced : IDisposable
{
    private readonly ISynth _synth;
    private readonly List<int> _heldNotes = new();
    private readonly List<int> _heldVelocities = new();
    private readonly List<int> _orderNotes = new();
    private readonly List<int> _currentPattern = new();
    private readonly List<int> _currentVelocities = new();
    private readonly object _lock = new();

    [ThreadStatic]
    private static Random? _threadLocalRandom;
    private static Random ThreadRandom => _threadLocalRandom ??= new Random(
        Environment.TickCount ^ Thread.CurrentThread.ManagedThreadId);

    private int _patternIndex;
    private int _stepIndex;
    private bool _ascending = true;
    private int _lastPlayedNote = -1;
    private int _lastRandomNote = -1;
    private double _lastTriggerBeat;
    private bool _disposed;
    private bool _isHolding;
    private readonly List<int> _latchedNotes = new();
    private readonly List<int> _latchedVelocities = new();

    // Pattern and timing
    /// <summary>Current arpeggiator pattern type.</summary>
    public AdvancedArpPattern Pattern { get; set; } = AdvancedArpPattern.Up;

    /// <summary>Note rate (how often notes trigger).</summary>
    public AdvancedArpRate Rate { get; set; } = AdvancedArpRate.Sixteenth;

    /// <summary>Custom step pattern for Custom pattern mode.</summary>
    public ArpStepPattern CustomPattern { get; set; } = new ArpStepPattern(16);

    // Range and transposition
    /// <summary>Octave range to expand held notes (0 = same octave only).</summary>
    public int OctaveRange { get; set; } = 1;

    /// <summary>Semitone transpose for all output notes.</summary>
    public int Transpose { get; set; } = 0;

    // Gate and timing
    /// <summary>Gate percentage (0.0 to 1.0, how long each note plays relative to rate).</summary>
    public float Gate { get; set; } = 0.8f;

    /// <summary>Swing amount (0.0 to 1.0, 0 = no swing, affects even-numbered beats).</summary>
    public float Swing { get; set; } = 0.0f;

    /// <summary>Whether to tie notes together (legato mode).</summary>
    public bool TieNotes { get; set; } = false;

    // Velocity
    /// <summary>Fixed velocity value (used when VelocityCurve is Fixed, or if >= 0).</summary>
    public int FixedVelocity { get; set; } = -1;

    /// <summary>Velocity curve type for shaping dynamics.</summary>
    public VelocityCurve VelocityCurve { get; set; } = VelocityCurve.Linear;

    /// <summary>Velocity sensitivity multiplier (0.0 to 2.0).</summary>
    public float VelocitySensitivity { get; set; } = 1.0f;

    /// <summary>Minimum velocity output (0-127).</summary>
    public int MinVelocity { get; set; } = 1;

    /// <summary>Maximum velocity output (0-127).</summary>
    public int MaxVelocity { get; set; } = 127;

    // Accent
    /// <summary>Accent velocity (used when step has Accent enabled).</summary>
    public int AccentVelocity { get; set; } = 127;

    /// <summary>Accent pattern as bit flags (e.g., 0b1001 = accent on steps 0 and 3).</summary>
    public int AccentPattern { get; set; } = 0b0001;

    /// <summary>Length of accent pattern in steps.</summary>
    public int AccentPatternLength { get; set; } = 4;

    // Modes
    /// <summary>Whether the arpeggiator is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Latch mode - keeps playing after all notes are released until new notes pressed.</summary>
    public bool LatchMode { get; set; } = false;

    /// <summary>Hold mode - freezes current pattern when enabled, ignores new notes.</summary>
    public bool HoldMode
    {
        get => _isHolding;
        set
        {
            lock (_lock)
            {
                if (value && !_isHolding)
                {
                    // Entering hold mode - copy current notes
                    _latchedNotes.Clear();
                    _latchedNotes.AddRange(_heldNotes);
                    _latchedVelocities.Clear();
                    _latchedVelocities.AddRange(_heldVelocities);
                }
                _isHolding = value;
            }
        }
    }

    /// <summary>Step sequencer mode - uses CustomPattern steps for per-note modifications.</summary>
    public bool StepSequencerMode { get; set; } = false;

    /// <summary>Retrigger mode - restarts pattern when new notes are pressed.</summary>
    public bool RetriggerMode { get; set; } = false;

    /// <summary>Note priority for monophonic-style arpeggiator (Last, Low, High).</summary>
    public NotePriority NotePriority { get; set; } = NotePriority.Last;

    // State
    /// <summary>Whether there are notes to arpeggiate.</summary>
    public bool HasNotes
    {
        get
        {
            lock (_lock)
            {
                if (_isHolding) return _latchedNotes.Count > 0;
                if (LatchMode && _heldNotes.Count == 0) return _latchedNotes.Count > 0;
                return _heldNotes.Count > 0;
            }
        }
    }

    /// <summary>Current note count.</summary>
    public int NoteCount
    {
        get
        {
            lock (_lock)
            {
                if (_isHolding) return _latchedNotes.Count;
                if (LatchMode && _heldNotes.Count == 0) return _latchedNotes.Count;
                return _heldNotes.Count;
            }
        }
    }

    /// <summary>Current step index in the pattern.</summary>
    public int CurrentStepIndex => _stepIndex;

    /// <summary>Current pattern index.</summary>
    public int CurrentPatternIndex => _patternIndex;

    // Events
    /// <summary>Fired when a note is triggered by the arpeggiator.</summary>
    public event EventHandler<AdvancedArpNoteEventArgs>? NotePlayed;

    /// <summary>Fired when a note is released by the arpeggiator.</summary>
    public event EventHandler<AdvancedArpNoteEventArgs>? NoteReleased;

    /// <summary>Fired when the pattern restarts.</summary>
    public event EventHandler? PatternRestarted;

    /// <summary>Fired when a step is triggered (even if skipped/muted).</summary>
    public event EventHandler<int>? StepTriggered;

    /// <summary>
    /// Creates an advanced arpeggiator connected to a synth.
    /// </summary>
    /// <param name="synth">The synth to output notes to.</param>
    public MidiArpeggiatorAdvanced(ISynth synth)
    {
        _synth = synth ?? throw new ArgumentNullException(nameof(synth));
    }

    /// <summary>
    /// Add a note to the arpeggiator (called on NoteOn).
    /// </summary>
    public void NoteOn(int note, int velocity)
    {
        MidiValidation.ValidateNote(note);
        MidiValidation.ValidateVelocity(velocity);

        lock (_lock)
        {
            if (_isHolding) return; // Ignore notes in hold mode

            // Handle latch mode - if all notes were released and new note comes in, clear latch
            if (LatchMode && _heldNotes.Count == 0 && _latchedNotes.Count > 0)
            {
                _latchedNotes.Clear();
                _latchedVelocities.Clear();
            }

            if (!_heldNotes.Contains(note))
            {
                _heldNotes.Add(note);
                _heldVelocities.Add(velocity);
                _orderNotes.Add(note);

                // Store for latch mode
                if (LatchMode)
                {
                    _latchedNotes.Clear();
                    _latchedNotes.AddRange(_heldNotes);
                    _latchedVelocities.Clear();
                    _latchedVelocities.AddRange(_heldVelocities);
                }

                if (RetriggerMode)
                {
                    _patternIndex = 0;
                    _stepIndex = 0;
                    _ascending = true;
                    PatternRestarted?.Invoke(this, EventArgs.Empty);
                }

                RebuildPattern();
            }
        }
    }

    /// <summary>
    /// Remove a note from the arpeggiator (called on NoteOff).
    /// </summary>
    public void NoteOff(int note)
    {
        MidiValidation.ValidateNote(note);

        lock (_lock)
        {
            if (_isHolding) return; // Ignore note offs in hold mode

            int index = _heldNotes.IndexOf(note);
            if (index >= 0)
            {
                _heldNotes.RemoveAt(index);
                _heldVelocities.RemoveAt(index);
            }
            _orderNotes.Remove(note);

            // In latch mode, don't stop notes when released
            if (LatchMode && _latchedNotes.Count > 0)
            {
                return;
            }

            if (_heldNotes.Count == 0)
            {
                _patternIndex = 0;
                _stepIndex = 0;
                _ascending = true;

                if (_lastPlayedNote >= 0)
                {
                    _synth.NoteOff(_lastPlayedNote);
                    NoteReleased?.Invoke(this, new AdvancedArpNoteEventArgs(_lastPlayedNote, 0));
                    _lastPlayedNote = -1;
                }
            }
            else
            {
                RebuildPattern();
            }
        }
    }

    /// <summary>
    /// Clear all held notes and reset the arpeggiator.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            if (_lastPlayedNote >= 0)
            {
                _synth.NoteOff(_lastPlayedNote);
                NoteReleased?.Invoke(this, new AdvancedArpNoteEventArgs(_lastPlayedNote, 0));
                _lastPlayedNote = -1;
            }

            _heldNotes.Clear();
            _heldVelocities.Clear();
            _orderNotes.Clear();
            _latchedNotes.Clear();
            _latchedVelocities.Clear();
            _currentPattern.Clear();
            _currentVelocities.Clear();
            _patternIndex = 0;
            _stepIndex = 0;
            _ascending = true;
            _lastTriggerBeat = 0;
            _isHolding = false;
        }
    }

    /// <summary>
    /// Reset pattern position to the beginning.
    /// </summary>
    public void ResetPattern()
    {
        lock (_lock)
        {
            _patternIndex = 0;
            _stepIndex = 0;
            _ascending = true;
            _lastTriggerBeat = 0;
            PatternRestarted?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Process the arpeggiator at the given beat position.
    /// </summary>
    /// <param name="currentBeat">Current position in beats.</param>
    /// <param name="bpm">Beats per minute.</param>
    public void Process(double currentBeat, double bpm)
    {
        if (!Enabled || _disposed) return;

        lock (_lock)
        {
            if (_currentPattern.Count == 0) return;

            double beatsPerNote = GetBeatsPerNote();

            // Apply swing to odd steps
            double swingOffset = 0;
            int beatIndex = (int)(currentBeat / beatsPerNote);
            if (beatIndex % 2 == 1 && Swing > 0)
            {
                swingOffset = beatsPerNote * Swing * 0.5;
            }

            double adjustedBeat = currentBeat - swingOffset;
            double triggerBeat = Math.Floor(adjustedBeat / beatsPerNote) * beatsPerNote + swingOffset;

            // Check if we should trigger a new note
            if (triggerBeat > _lastTriggerBeat || _lastTriggerBeat == 0)
            {
                _lastTriggerBeat = triggerBeat;
                TriggerStep(bpm);
            }
        }
    }

    /// <summary>
    /// Get the beats per note based on current rate.
    /// </summary>
    private double GetBeatsPerNote()
    {
        return Rate switch
        {
            AdvancedArpRate.Whole => 4.0,
            AdvancedArpRate.Half => 2.0,
            AdvancedArpRate.DottedQuarter => 1.5,
            AdvancedArpRate.Quarter => 1.0,
            AdvancedArpRate.TripletQuarter => 2.0 / 3.0,
            AdvancedArpRate.DottedEighth => 0.75,
            AdvancedArpRate.Eighth => 0.5,
            AdvancedArpRate.TripletEighth => 1.0 / 3.0,
            AdvancedArpRate.DottedSixteenth => 0.375,
            AdvancedArpRate.Sixteenth => 0.25,
            AdvancedArpRate.TripletSixteenth => 1.0 / 6.0,
            AdvancedArpRate.ThirtySecond => 0.125,
            AdvancedArpRate.TripletThirtySecond => 1.0 / 12.0,
            _ => 0.25
        };
    }

    /// <summary>
    /// Trigger the next step in the arpeggiator.
    /// </summary>
    private void TriggerStep(double bpm)
    {
        if (_currentPattern.Count == 0) return;

        StepTriggered?.Invoke(this, _stepIndex);

        ArpStep? currentStep = null;
        if (StepSequencerMode && CustomPattern.Steps.Count > 0)
        {
            currentStep = CustomPattern.Steps[_stepIndex % CustomPattern.Steps.Count];
        }

        // Check step conditions
        if (currentStep != null)
        {
            // Check probability
            if (currentStep.Probability < 1.0f && ThreadRandom.NextDouble() > currentStep.Probability)
            {
                AdvanceStep();
                return;
            }

            // Check if step is skipped
            if (currentStep.Skip || !currentStep.Enabled)
            {
                AdvanceStep();
                return;
            }
        }

        // Handle chord mode - play all notes at once
        if (Pattern == AdvancedArpPattern.Chord)
        {
            PlayChord(bpm, currentStep);
            AdvanceStep();
            return;
        }

        // Stop previous note if not tying
        bool shouldTie = TieNotes || (currentStep?.Tie ?? false);
        if (!shouldTie && _lastPlayedNote >= 0)
        {
            _synth.NoteOff(_lastPlayedNote);
            NoteReleased?.Invoke(this, new AdvancedArpNoteEventArgs(_lastPlayedNote, 0));
        }

        // Get next note from pattern
        int note = GetNextNote();

        // Apply step modifications
        if (currentStep != null)
        {
            note += currentStep.NoteOffset;
            note += currentStep.OctaveOffset * 12;
        }

        // Apply global transpose
        note += Transpose;

        // Clamp note to valid range
        note = Math.Clamp(note, 0, 127);

        // Calculate velocity
        int velocity = CalculateVelocity(currentStep);

        // Handle ratchet
        int ratchetCount = currentStep?.Ratchet ?? 1;
        if (ratchetCount > 1)
        {
            PlayRatchet(note, velocity, ratchetCount, bpm, currentStep);
        }
        else
        {
            // Play the note normally
            PlayNote(note, velocity, bpm, currentStep);
        }

        AdvanceStep();
    }

    /// <summary>
    /// Play a chord (all held notes simultaneously).
    /// </summary>
    private void PlayChord(double bpm, ArpStep? step)
    {
        // Stop all previous notes
        if (_lastPlayedNote >= 0)
        {
            _synth.NoteOff(_lastPlayedNote);
            _lastPlayedNote = -1;
        }

        double beatsPerNote = GetBeatsPerNote();
        float gateMultiplier = step?.GateMultiplier ?? 1.0f;
        double noteDurationMs = (beatsPerNote * Gate * gateMultiplier * 60000.0) / bpm;

        for (int i = 0; i < _currentPattern.Count; i++)
        {
            int note = _currentPattern[i] + Transpose;
            note = Math.Clamp(note, 0, 127);

            int velocity = CalculateVelocity(step, i);

            _synth.NoteOn(note, velocity);
            NotePlayed?.Invoke(this, new AdvancedArpNoteEventArgs(note, velocity, _stepIndex, beatsPerNote * Gate * gateMultiplier));

            // Schedule note off
            int noteToOff = note;
            _ = Task.Run(async () =>
            {
                await Task.Delay((int)noteDurationMs);
                lock (_lock)
                {
                    _synth.NoteOff(noteToOff);
                    NoteReleased?.Invoke(this, new AdvancedArpNoteEventArgs(noteToOff, 0));
                }
            });
        }
    }

    /// <summary>
    /// Play ratcheted notes (multiple rapid notes within one step).
    /// </summary>
    private void PlayRatchet(int note, int velocity, int count, double bpm, ArpStep? step)
    {
        double beatsPerNote = GetBeatsPerNote();
        float gateMultiplier = step?.GateMultiplier ?? 1.0f;
        double ratchetDurationMs = (beatsPerNote * 60000.0) / (bpm * count);
        double noteOnDurationMs = ratchetDurationMs * Gate * gateMultiplier * 0.8;

        for (int i = 0; i < count; i++)
        {
            int delayMs = (int)(i * ratchetDurationMs);
            int capturedNote = note;
            int capturedVelocity = velocity - (i * 10); // Decrease velocity for each ratchet
            capturedVelocity = Math.Max(capturedVelocity, MinVelocity);

            _ = Task.Run(async () =>
            {
                await Task.Delay(delayMs);
                lock (_lock)
                {
                    if (_disposed) return;
                    _synth.NoteOn(capturedNote, capturedVelocity);
                    NotePlayed?.Invoke(this, new AdvancedArpNoteEventArgs(capturedNote, capturedVelocity, _stepIndex));
                }

                await Task.Delay((int)noteOnDurationMs);
                lock (_lock)
                {
                    _synth.NoteOff(capturedNote);
                    NoteReleased?.Invoke(this, new AdvancedArpNoteEventArgs(capturedNote, 0));
                }
            });
        }

        _lastPlayedNote = -1; // Ratchet handles its own note offs
    }

    /// <summary>
    /// Play a single note with proper gate timing.
    /// </summary>
    private void PlayNote(int note, int velocity, double bpm, ArpStep? step)
    {
        bool hasSlide = step?.Slide ?? false;
        bool isAccent = step?.Accent ?? false;
        bool isTied = TieNotes || (step?.Tie ?? false);

        _synth.NoteOn(note, velocity);
        _lastPlayedNote = note;

        double beatsPerNote = GetBeatsPerNote();
        float gateMultiplier = step?.GateMultiplier ?? 1.0f;
        double duration = beatsPerNote * Gate * gateMultiplier;

        NotePlayed?.Invoke(this, new AdvancedArpNoteEventArgs(note, velocity, _stepIndex, duration, isTied, hasSlide, isAccent));

        // Schedule note off based on gate (unless tying)
        if (!isTied)
        {
            double noteDurationMs = (duration * 60000.0) / bpm;

            int noteToOff = note;
            _ = Task.Run(async () =>
            {
                await Task.Delay((int)noteDurationMs);
                lock (_lock)
                {
                    if (_lastPlayedNote == noteToOff)
                    {
                        _synth.NoteOff(noteToOff);
                        NoteReleased?.Invoke(this, new AdvancedArpNoteEventArgs(noteToOff, 0));
                        _lastPlayedNote = -1;
                    }
                }
            });
        }
    }

    /// <summary>
    /// Calculate velocity based on curve and step settings.
    /// </summary>
    private int CalculateVelocity(ArpStep? step, int patternIndex = -1)
    {
        if (patternIndex < 0) patternIndex = _patternIndex;

        // Check for accent
        bool isAccent = step?.Accent ?? false;
        if (!isAccent && AccentPatternLength > 0)
        {
            int accentBit = _stepIndex % AccentPatternLength;
            isAccent = (AccentPattern & (1 << accentBit)) != 0;
        }

        if (isAccent)
        {
            return AccentVelocity;
        }

        // Get base velocity
        int baseVelocity;
        if (FixedVelocity >= 0)
        {
            baseVelocity = FixedVelocity;
        }
        else if (patternIndex < _currentVelocities.Count)
        {
            baseVelocity = _currentVelocities[patternIndex];
        }
        else
        {
            baseVelocity = 100;
        }

        // Apply velocity curve
        float normalizedVelocity = baseVelocity / 127.0f;
        float curvedVelocity = ApplyVelocityCurve(normalizedVelocity);

        // Apply sensitivity
        curvedVelocity *= VelocitySensitivity;

        // Apply step multiplier
        if (step != null)
        {
            curvedVelocity *= step.VelocityMultiplier;
        }

        // Convert back to 0-127 range and clamp
        int velocity = (int)(curvedVelocity * 127.0f);
        return Math.Clamp(velocity, MinVelocity, MaxVelocity);
    }

    /// <summary>
    /// Apply the selected velocity curve to a normalized velocity value.
    /// </summary>
    private float ApplyVelocityCurve(float velocity)
    {
        return VelocityCurve switch
        {
            VelocityCurve.Linear => velocity,
            VelocityCurve.Exponential => velocity * velocity,
            VelocityCurve.Logarithmic => MathF.Sqrt(velocity),
            VelocityCurve.SCurve => velocity < 0.5f
                ? 2.0f * velocity * velocity
                : 1.0f - MathF.Pow(-2.0f * velocity + 2.0f, 2) / 2.0f,
            VelocityCurve.Fixed => 1.0f,
            VelocityCurve.RampUp => (float)_stepIndex / Math.Max(1, CustomPattern.Length - 1),
            VelocityCurve.RampDown => 1.0f - (float)_stepIndex / Math.Max(1, CustomPattern.Length - 1),
            VelocityCurve.Wave => 0.5f + 0.5f * MathF.Sin(_stepIndex * MathF.PI / 4.0f),
            _ => velocity
        };
    }

    /// <summary>
    /// Advance to the next step in the pattern.
    /// </summary>
    private void AdvanceStep()
    {
        _stepIndex++;
        if (StepSequencerMode && CustomPattern.Steps.Count > 0)
        {
            if (_stepIndex >= CustomPattern.Steps.Count)
            {
                _stepIndex = 0;
                PatternRestarted?.Invoke(this, EventArgs.Empty);
            }
        }
        else
        {
            // For non-step-sequencer mode, step index just cycles for accent pattern
            if (_stepIndex >= AccentPatternLength)
            {
                _stepIndex = 0;
            }
        }
    }

    /// <summary>
    /// Get the next note based on current pattern type.
    /// </summary>
    private int GetNextNote()
    {
        if (_currentPattern.Count == 0) return 60; // Middle C as fallback

        int note;

        switch (Pattern)
        {
            case AdvancedArpPattern.Up:
            case AdvancedArpPattern.Down:
            case AdvancedArpPattern.OrderPlayed:
            case AdvancedArpPattern.OrderPlayedReverse:
            case AdvancedArpPattern.Converge:
            case AdvancedArpPattern.Diverge:
            case AdvancedArpPattern.ThumbUp:
            case AdvancedArpPattern.ThumbDown:
            case AdvancedArpPattern.PinkyUp:
            case AdvancedArpPattern.PinkyDown:
            case AdvancedArpPattern.Spiral:
            case AdvancedArpPattern.Skip:
            case AdvancedArpPattern.Mirror:
                note = _currentPattern[_patternIndex];
                _patternIndex = (_patternIndex + 1) % _currentPattern.Count;
                if (_patternIndex == 0)
                {
                    PatternRestarted?.Invoke(this, EventArgs.Empty);
                }
                break;

            case AdvancedArpPattern.UpDown:
            case AdvancedArpPattern.UpDownInclusive:
                note = _currentPattern[_patternIndex];
                note = HandleUpDownPattern(Pattern == AdvancedArpPattern.UpDownInclusive);
                break;

            case AdvancedArpPattern.DownUp:
            case AdvancedArpPattern.DownUpInclusive:
                note = _currentPattern[_patternIndex];
                note = HandleDownUpPattern(Pattern == AdvancedArpPattern.DownUpInclusive);
                break;

            case AdvancedArpPattern.Random:
                note = _currentPattern[ThreadRandom.Next(_currentPattern.Count)];
                break;

            case AdvancedArpPattern.RandomNoRepeat:
                note = GetRandomNoRepeat();
                break;

            case AdvancedArpPattern.Custom:
                note = GetCustomPatternNote();
                break;

            default:
                note = _currentPattern[0];
                break;
        }

        return note;
    }

    /// <summary>
    /// Handle UpDown pattern progression.
    /// </summary>
    private int HandleUpDownPattern(bool inclusive)
    {
        int note = _currentPattern[_patternIndex];

        if (_ascending)
        {
            _patternIndex++;
            if (_patternIndex >= _currentPattern.Count)
            {
                if (inclusive)
                {
                    _patternIndex = _currentPattern.Count - 1;
                }
                else
                {
                    _patternIndex = Math.Max(0, _currentPattern.Count - 2);
                }
                _ascending = false;

                if (_currentPattern.Count <= 1)
                {
                    _patternIndex = 0;
                    _ascending = true;
                }
            }
        }
        else
        {
            _patternIndex--;
            if (_patternIndex < 0)
            {
                if (inclusive)
                {
                    _patternIndex = 0;
                }
                else
                {
                    _patternIndex = Math.Min(1, _currentPattern.Count - 1);
                }
                _ascending = true;
                PatternRestarted?.Invoke(this, EventArgs.Empty);
            }
        }

        return note;
    }

    /// <summary>
    /// Handle DownUp pattern progression.
    /// </summary>
    private int HandleDownUpPattern(bool inclusive)
    {
        int note = _currentPattern[_patternIndex];

        if (!_ascending)
        {
            _patternIndex++;
            if (_patternIndex >= _currentPattern.Count)
            {
                if (inclusive)
                {
                    _patternIndex = _currentPattern.Count - 1;
                }
                else
                {
                    _patternIndex = Math.Max(0, _currentPattern.Count - 2);
                }
                _ascending = true;

                if (_currentPattern.Count <= 1)
                {
                    _patternIndex = 0;
                    _ascending = false;
                }
            }
        }
        else
        {
            _patternIndex--;
            if (_patternIndex < 0)
            {
                if (inclusive)
                {
                    _patternIndex = 0;
                }
                else
                {
                    _patternIndex = Math.Min(1, _currentPattern.Count - 1);
                }
                _ascending = false;
                PatternRestarted?.Invoke(this, EventArgs.Empty);
            }
        }

        return note;
    }

    /// <summary>
    /// Get a random note that is different from the last played note.
    /// </summary>
    private int GetRandomNoRepeat()
    {
        if (_currentPattern.Count <= 1)
        {
            return _currentPattern.Count > 0 ? _currentPattern[0] : 60;
        }

        int attempts = 0;
        int note;
        do
        {
            note = _currentPattern[ThreadRandom.Next(_currentPattern.Count)];
            attempts++;
        } while (note == _lastRandomNote && attempts < 10);

        _lastRandomNote = note;
        return note;
    }

    /// <summary>
    /// Get note from custom pattern.
    /// </summary>
    private int GetCustomPatternNote()
    {
        if (CustomPattern.Steps.Count == 0 || _currentPattern.Count == 0)
        {
            return _currentPattern.Count > 0 ? _currentPattern[0] : 60;
        }

        int stepIndex = _stepIndex % CustomPattern.Steps.Count;
        var step = CustomPattern.Steps[stepIndex];

        // In custom pattern mode, NoteOffset can index into held notes
        int noteIndex = Math.Abs(step.NoteOffset) % _currentPattern.Count;
        return _currentPattern[noteIndex];
    }

    /// <summary>
    /// Rebuild the pattern based on held notes and settings.
    /// </summary>
    private void RebuildPattern()
    {
        _currentPattern.Clear();
        _currentVelocities.Clear();

        var sourceNotes = _isHolding ? _latchedNotes :
            (LatchMode && _heldNotes.Count == 0 ? _latchedNotes : _heldNotes);
        var sourceVelocities = _isHolding ? _latchedVelocities :
            (LatchMode && _heldVelocities.Count == 0 ? _latchedVelocities : _heldVelocities);

        if (sourceNotes.Count == 0) return;

        // Build base note list with octave expansion
        var notes = new List<(int note, int velocity)>();
        var sortedPairs = sourceNotes
            .Select((n, i) => (note: n, velocity: i < sourceVelocities.Count ? sourceVelocities[i] : 100))
            .OrderBy(p => p.note)
            .ToList();

        for (int octave = 0; octave <= OctaveRange; octave++)
        {
            foreach (var pair in sortedPairs)
            {
                int transposedNote = pair.note + (octave * 12);
                if (transposedNote <= 127)
                {
                    notes.Add((transposedNote, pair.velocity));
                }
            }
        }

        // Apply pattern ordering
        switch (Pattern)
        {
            case AdvancedArpPattern.Up:
            case AdvancedArpPattern.UpDown:
            case AdvancedArpPattern.UpDownInclusive:
                AddNotesToPattern(notes);
                _ascending = true;
                break;

            case AdvancedArpPattern.Down:
            case AdvancedArpPattern.DownUp:
            case AdvancedArpPattern.DownUpInclusive:
                notes.Reverse();
                AddNotesToPattern(notes);
                _ascending = Pattern == AdvancedArpPattern.Down;
                break;

            case AdvancedArpPattern.Random:
            case AdvancedArpPattern.RandomNoRepeat:
            case AdvancedArpPattern.Chord:
            case AdvancedArpPattern.Custom:
                AddNotesToPattern(notes);
                break;

            case AdvancedArpPattern.OrderPlayed:
                BuildOrderPlayedPattern(sourceNotes, sourceVelocities);
                break;

            case AdvancedArpPattern.OrderPlayedReverse:
                BuildOrderPlayedReversePattern(sourceNotes, sourceVelocities);
                break;

            case AdvancedArpPattern.Converge:
                BuildConvergePattern(notes);
                break;

            case AdvancedArpPattern.Diverge:
                BuildDivergePattern(notes);
                break;

            case AdvancedArpPattern.ThumbUp:
                BuildThumbPattern(notes, true);
                break;

            case AdvancedArpPattern.ThumbDown:
                BuildThumbPattern(notes, false);
                break;

            case AdvancedArpPattern.PinkyUp:
                BuildPinkyPattern(notes, true);
                break;

            case AdvancedArpPattern.PinkyDown:
                BuildPinkyPattern(notes, false);
                break;

            case AdvancedArpPattern.Spiral:
                BuildSpiralPattern(notes);
                break;

            case AdvancedArpPattern.Skip:
                BuildSkipPattern(notes);
                break;

            case AdvancedArpPattern.Mirror:
                BuildMirrorPattern(notes);
                break;
        }

        // Reset pattern index if out of bounds
        if (_patternIndex >= _currentPattern.Count)
        {
            _patternIndex = 0;
        }
    }

    private void AddNotesToPattern(List<(int note, int velocity)> notes)
    {
        foreach (var pair in notes)
        {
            _currentPattern.Add(pair.note);
            _currentVelocities.Add(pair.velocity);
        }
    }

    private void BuildOrderPlayedPattern(List<int> sourceNotes, List<int> sourceVelocities)
    {
        for (int octave = 0; octave <= OctaveRange; octave++)
        {
            for (int i = 0; i < _orderNotes.Count; i++)
            {
                int note = _orderNotes[i];
                if (sourceNotes.Contains(note))
                {
                    int transposedNote = note + (octave * 12);
                    if (transposedNote <= 127)
                    {
                        _currentPattern.Add(transposedNote);
                        int velocityIndex = sourceNotes.IndexOf(note);
                        _currentVelocities.Add(velocityIndex >= 0 && velocityIndex < sourceVelocities.Count
                            ? sourceVelocities[velocityIndex] : 100);
                    }
                }
            }
        }
    }

    private void BuildOrderPlayedReversePattern(List<int> sourceNotes, List<int> sourceVelocities)
    {
        for (int octave = OctaveRange; octave >= 0; octave--)
        {
            for (int i = _orderNotes.Count - 1; i >= 0; i--)
            {
                int note = _orderNotes[i];
                if (sourceNotes.Contains(note))
                {
                    int transposedNote = note + (octave * 12);
                    if (transposedNote <= 127)
                    {
                        _currentPattern.Add(transposedNote);
                        int velocityIndex = sourceNotes.IndexOf(note);
                        _currentVelocities.Add(velocityIndex >= 0 && velocityIndex < sourceVelocities.Count
                            ? sourceVelocities[velocityIndex] : 100);
                    }
                }
            }
        }
    }

    private void BuildConvergePattern(List<(int note, int velocity)> notes)
    {
        int low = 0;
        int high = notes.Count - 1;
        bool fromLow = true;

        while (low <= high)
        {
            if (fromLow)
            {
                _currentPattern.Add(notes[low].note);
                _currentVelocities.Add(notes[low].velocity);
                low++;
            }
            else
            {
                _currentPattern.Add(notes[high].note);
                _currentVelocities.Add(notes[high].velocity);
                high--;
            }
            fromLow = !fromLow;
        }
    }

    private void BuildDivergePattern(List<(int note, int velocity)> notes)
    {
        int mid = notes.Count / 2;
        int low = mid;
        int high = mid + 1;

        while (low >= 0 || high < notes.Count)
        {
            if (low >= 0)
            {
                _currentPattern.Add(notes[low].note);
                _currentVelocities.Add(notes[low].velocity);
                low--;
            }
            if (high < notes.Count)
            {
                _currentPattern.Add(notes[high].note);
                _currentVelocities.Add(notes[high].velocity);
                high++;
            }
        }
    }

    private void BuildThumbPattern(List<(int note, int velocity)> notes, bool ascending)
    {
        if (notes.Count < 2)
        {
            AddNotesToPattern(notes);
            return;
        }

        var thumbNote = ascending ? notes[0] : notes[^1];
        var otherNotes = ascending ? notes.Skip(1).ToList() : notes.Take(notes.Count - 1).Reverse().ToList();

        foreach (var note in otherNotes)
        {
            _currentPattern.Add(thumbNote.note);
            _currentVelocities.Add(thumbNote.velocity);
            _currentPattern.Add(note.note);
            _currentVelocities.Add(note.velocity);
        }
    }

    private void BuildPinkyPattern(List<(int note, int velocity)> notes, bool ascending)
    {
        if (notes.Count < 2)
        {
            AddNotesToPattern(notes);
            return;
        }

        var pinkyNote = ascending ? notes[^1] : notes[0];
        var otherNotes = ascending ? notes.Take(notes.Count - 1).ToList() : notes.Skip(1).Reverse().ToList();

        foreach (var note in otherNotes)
        {
            _currentPattern.Add(note.note);
            _currentVelocities.Add(note.velocity);
            _currentPattern.Add(pinkyNote.note);
            _currentVelocities.Add(pinkyNote.velocity);
        }
    }

    private void BuildSpiralPattern(List<(int note, int velocity)> notes)
    {
        var remaining = new List<(int note, int velocity)>(notes);
        bool takeFromLow = true;

        while (remaining.Count > 0)
        {
            if (takeFromLow)
            {
                _currentPattern.Add(remaining[0].note);
                _currentVelocities.Add(remaining[0].velocity);
                remaining.RemoveAt(0);
            }
            else
            {
                _currentPattern.Add(remaining[^1].note);
                _currentVelocities.Add(remaining[^1].velocity);
                remaining.RemoveAt(remaining.Count - 1);
            }
            takeFromLow = !takeFromLow;
        }
    }

    private void BuildSkipPattern(List<(int note, int velocity)> notes)
    {
        // First pass: every other note starting from 0
        for (int i = 0; i < notes.Count; i += 2)
        {
            _currentPattern.Add(notes[i].note);
            _currentVelocities.Add(notes[i].velocity);
        }
        // Second pass: fill in the skipped notes
        for (int i = 1; i < notes.Count; i += 2)
        {
            _currentPattern.Add(notes[i].note);
            _currentVelocities.Add(notes[i].velocity);
        }
    }

    private void BuildMirrorPattern(List<(int note, int velocity)> notes)
    {
        // Interleave notes from both ends moving toward center
        int left = 0;
        int right = notes.Count - 1;

        while (left <= right)
        {
            _currentPattern.Add(notes[left].note);
            _currentVelocities.Add(notes[left].velocity);

            if (left != right)
            {
                _currentPattern.Add(notes[right].note);
                _currentVelocities.Add(notes[right].velocity);
            }

            left++;
            right--;
        }
    }

    /// <summary>
    /// Get a list of preset step patterns.
    /// </summary>
    public static List<ArpStepPattern> GetPresetPatterns()
    {
        var presets = new List<ArpStepPattern>();

        // Basic 16-step pattern
        var basic = new ArpStepPattern("Basic 16", 16);
        presets.Add(basic);

        // 8-step with accents on 1 and 5
        var accented = new ArpStepPattern("Accented 8", 8);
        accented.Steps[0].Accent = true;
        accented.Steps[4].Accent = true;
        presets.Add(accented);

        // Syncopated pattern
        var syncopated = new ArpStepPattern("Syncopated", 16);
        syncopated.Steps[0].Accent = true;
        syncopated.Steps[3].Accent = true;
        syncopated.Steps[6].Accent = true;
        syncopated.Steps[10].Accent = true;
        syncopated.Steps[12].Accent = true;
        for (int i = 0; i < 16; i += 4) syncopated.Steps[i + 2].Skip = true;
        presets.Add(syncopated);

        // Ratchet pattern
        var ratchet = new ArpStepPattern("Ratchet", 8);
        ratchet.Steps[2].Ratchet = 2;
        ratchet.Steps[5].Ratchet = 3;
        ratchet.Steps[7].Ratchet = 2;
        presets.Add(ratchet);

        // Probability pattern
        var probability = new ArpStepPattern("Random Feel", 16);
        for (int i = 0; i < 16; i++)
        {
            probability.Steps[i].Probability = i % 2 == 0 ? 1.0f : 0.7f;
        }
        presets.Add(probability);

        // Octave jump pattern
        var octaveJump = new ArpStepPattern("Octave Jump", 8);
        octaveJump.Steps[0].OctaveOffset = 0;
        octaveJump.Steps[1].OctaveOffset = 1;
        octaveJump.Steps[2].OctaveOffset = 0;
        octaveJump.Steps[3].OctaveOffset = -1;
        octaveJump.Steps[4].OctaveOffset = 0;
        octaveJump.Steps[5].OctaveOffset = 1;
        octaveJump.Steps[6].OctaveOffset = 2;
        octaveJump.Steps[7].OctaveOffset = 1;
        presets.Add(octaveJump);

        // Velocity swell
        var swell = new ArpStepPattern("Velocity Swell", 8);
        for (int i = 0; i < 8; i++)
        {
            swell.Steps[i].VelocityMultiplier = 0.4f + (i * 0.1f);
        }
        presets.Add(swell);

        // Gate variation
        var gateVariation = new ArpStepPattern("Gate Variation", 8);
        gateVariation.Steps[0].GateMultiplier = 1.0f;
        gateVariation.Steps[1].GateMultiplier = 0.5f;
        gateVariation.Steps[2].GateMultiplier = 0.25f;
        gateVariation.Steps[3].GateMultiplier = 0.5f;
        gateVariation.Steps[4].GateMultiplier = 1.5f;
        gateVariation.Steps[5].GateMultiplier = 0.5f;
        gateVariation.Steps[6].GateMultiplier = 0.25f;
        gateVariation.Steps[7].GateMultiplier = 0.5f;
        presets.Add(gateVariation);

        // Tied legato
        var legato = new ArpStepPattern("Legato", 8);
        legato.Steps[0].Tie = true;
        legato.Steps[1].Tie = true;
        legato.Steps[4].Tie = true;
        legato.Steps[5].Tie = true;
        presets.Add(legato);

        return presets;
    }

    /// <summary>
    /// Disposes the arpeggiator and releases all notes.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Clear();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer.
    /// </summary>
    ~MidiArpeggiatorAdvanced()
    {
        Dispose();
    }
}

/// <summary>
/// Note priority modes for the arpeggiator.
/// </summary>
public enum NotePriority
{
    /// <summary>Last note pressed has priority.</summary>
    Last,
    /// <summary>Lowest note has priority.</summary>
    Low,
    /// <summary>Highest note has priority.</summary>
    High
}
