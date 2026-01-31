// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Pattern container for musical events.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;


namespace MusicEngine.Core;


// Represents a musical pattern with note events and playback properties
public class Pattern
{
    /// <summary>Primary synth (for backward compatibility).</summary>
    public ISynth Synth { get; set; } // Synthesizer to play the pattern

    /// <summary>All synth targets this pattern should drive.</summary>
    public List<ISynth> SynthTargets { get; } = new(); // Multiple synth targets
    public List<NoteEvent> Events { get; set; } = new(); // Note events in the pattern
    public double LoopLength { get; set; } = 4.0; // in beats
    public bool IsLooping { get; set; } = true; // Looping flag
    public double? StartBeat { get; set; } = null; // When to start the pattern
    public bool Enabled { get; set; } = true;  // Is the pattern enabled?
    public double PlaybackSpeed { get; set; } = 1.0; // 1=normal, <0 scratch/reverse

    // New properties for visualization
    internal int PatternIndex { get; set; } = 0; // Index in sequencer's pattern list
    internal Sequencer? Sequencer { get; set; } // Reference to parent sequencer

    /// <summary>Name of this pattern for display purposes.</summary>
    public string Name { get; set; } = "";

    /// <summary>Name of the instrument/synth for this pattern.</summary>
    public string InstrumentName { get; set; } = "";

    /// <summary>Code source info for the entire pattern definition.</summary>
    public CodeSourceInfo? SourceInfo { get; set; }

    /// <summary>Unique identifier for this pattern.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Alias for IsLooping - simpler workshop syntax.</summary>
    public bool Loop
    {
        get => IsLooping;
        set => IsLooping = value;
    }

    // Constructor to initialize the pattern with one or more synths
    public Pattern(ISynth synth, params ISynth[] additionalSynths)
    {
        Synth = synth;
        SynthTargets.Add(synth);
        if (additionalSynths != null && additionalSynths.Length > 0)
        {
            SynthTargets.AddRange(additionalSynths);
        }
    }

    /// <summary>
    /// Add a note to the pattern using simple syntax.
    /// pattern.Note(note, beat, duration, velocity)
    /// </summary>
    /// <param name="note">MIDI note number (0-127)</param>
    /// <param name="beat">Beat position in pattern (0 to LoopLength)</param>
    /// <param name="duration">Duration in beats</param>
    /// <param name="velocity">Velocity (0-127)</param>
    /// <returns>This pattern for chaining</returns>
    public Pattern Note(int note, double beat, double duration, int velocity)
    {
        MidiValidation.ValidateNote(note);
        MidiValidation.ValidateVelocity(velocity);
        Guard.NotNegative((int)(beat * 1000), nameof(beat)); // beat must be non-negative

        Events.Add(new NoteEvent
        {
            Note = note,
            Beat = beat,
            Duration = duration,
            Velocity = velocity
        });
        return this;
    }

    /// <summary>
    /// Shorthand step sequencer: "1010" adds notes at stepLength apart where '1' or 'x' occurs.
    /// </summary>
    public Pattern Seq(string steps, Action<SeqOptions>? configure = null)
    {
        var opt = new SeqOptions();
        configure?.Invoke(opt);

        double beat = opt.StartBeat;
        for (int i = 0; i < steps.Length; i++)
        {
            var ch = steps[i];
            bool hit = ch == '1' || ch == 'x' || ch == 'X';
            if (hit)
            {
                Note(opt.Pitch, beat, opt.Duration, opt.Velocity);
            }
            beat += opt.StepLength;
        }
        return this;
    }

    /// <summary>Options for Seq().</summary>
    public class SeqOptions
    {
        public int Pitch { get; set; } = 60;
        public int Velocity { get; set; } = 100;
        public double Duration { get; set; } = 0.25;   // beats
        public double StepLength { get; set; } = 0.25; // beats
        public double StartBeat { get; set; } = 0.0;

        public SeqOptions pitch(int p) { Pitch = p; return this; }
        public SeqOptions velocity(int v) { Velocity = v; return this; }
        public SeqOptions duration(double d) { Duration = d; return this; }
        public SeqOptions step(double s) { StepLength = s; return this; }
        public SeqOptions start(double s) { StartBeat = s; return this; }
    }

    /// <summary>
    /// Start playing this pattern.
    /// Registers with the sequencer and starts playback.
    /// </summary>
    public void Play()
    {
        if (Sequencer != null)
        {
            Sequencer.AddPattern(this);
            if (!Sequencer.IsRunning)
            {
                Sequencer.Start();
            }
        }
    }

    /// <summary>
    /// Stop this pattern from playing.
    /// </summary>
    public void Stop()
    {
        Sequencer?.RemovePattern(this);
        Synth?.AllNotesOff();
    }


    // Process the pattern for the given beat range
    public void Process(double startBeat, double endBeat, double bpm = 120.0)
    {
        if (!Enabled)
        {
            foreach (var target in SynthTargets)
            {
                target.AllNotesOff(); // Stop all notes if disabled
            }
            return;
        }
        if (StartBeat == null) StartBeat = startBeat; // Initialize start beat if not set

        double relativeStart = startBeat - StartBeat.Value; // Relative start beat
        double relativeEnd = endBeat - StartBeat.Value; // Relative end beat

        if (!IsLooping && relativeStart >= LoopLength) return;

        double startMod = relativeStart % LoopLength; // Modulo for looping
        double endMod = relativeEnd % LoopLength; // Modulo for looping


        if (startMod < 0) startMod += LoopLength; // Adjust negative modulo
        if (endMod < 0) endMod += LoopLength; // Adjust negative modulo

        bool wrapped = endMod < startMod; // Check if wrapped around

        if (relativeEnd < relativeStart) // Backwards scratching
        {
            wrapped = endMod > startMod; // Adjust wrap check for backwards
        }

        int cycleNumber = (int)(relativeEnd / LoopLength);

        for (int noteIndex = 0; noteIndex < Events.Count; noteIndex++) // Process each note event
        {
            var ev = Events[noteIndex];
            bool trigger = false;

            if (!IsLooping)
            {
                // Only trigger if it's within the first iteration of the pattern's life
                if (relativeEnd >= relativeStart)
                {
                    if (ev.Beat >= relativeStart && ev.Beat < relativeEnd && ev.Beat < LoopLength) trigger = true; // Forward playback
                }
                else
                {
                    if (ev.Beat >= relativeEnd && ev.Beat < relativeStart && ev.Beat < LoopLength) trigger = true; // Backward playback
                }
            }
            else
            {
                if (relativeEnd >= relativeStart)
                {
                    if (!wrapped)
                    {
                        if (ev.Beat >= startMod && ev.Beat < endMod) trigger = true; // Normal case
                    }
                    else // Wrap around
                    {
                        if (ev.Beat >= startMod || ev.Beat < endMod) trigger = true; // Wrapped case
                    }
                }
                else // Backwards
                {
                    if (!wrapped)
                    {
                        if (ev.Beat >= endMod && ev.Beat < startMod) trigger = true; // Normal backwards
                    }
                    else // Wrap around backwards
                    {
                        if (ev.Beat >= endMod || ev.Beat < startMod) trigger = true; // Wrapped backwards
                    }
                }
            }

            if (trigger)
            {
                TriggerNote(ev, noteIndex, endBeat, cycleNumber, bpm); // Trigger the note
            }
        }
    }

    // Stopwatch for high-precision note-off timing
    private static readonly System.Diagnostics.Stopwatch _noteTimer = System.Diagnostics.Stopwatch.StartNew();

    private void TriggerNote(NoteEvent ev, int noteIndex, double absoluteBeat, int cycleNumber, double bpm)
    {
        // Create the musical event with full information
        var durationMs = ev.Duration * (60000.0 / bpm); // Convert duration from beats to milliseconds
        var now = DateTime.Now; // Current time for scheduling

        var musicalEvent = new MusicalEvent
        {
            Id = new EventId(PatternIndex, noteIndex), // Unique ID for this event
            SourcePattern = this, // Reference to the source pattern
            NoteEvent = ev, // Original note event
            Note = ev.Note, // MIDI note number
            NoteName = MusicalEvent.GetNoteName(ev.Note), // Note name (e.g., C4)
            Velocity = ev.Velocity, // Velocity
            Duration = ev.Duration, // Duration in beats
            CyclePosition = ev.Beat, // Position within the cycle
            AbsoluteBeat = absoluteBeat, // Absolute beat in the sequencer
            CycleNumber = cycleNumber, // Cycle iteration number
            LoopLength = LoopLength, // Loop length in beats
            InstrumentName = !string.IsNullOrEmpty(InstrumentName) ? InstrumentName : $"Pattern {PatternIndex}", // Instrument name
            SourceInfo = ev.SourceInfo ?? SourceInfo, // Source code info
            TriggeredAt = now, // Time when triggered
            EndsAt = now.AddMilliseconds(durationMs), // Scheduled end time
            IsNoteOn = true, // Note-on event
            Bpm = bpm // Current BPM
        };

        // Notify sequencer of the event
        Sequencer?.OnNoteTriggered(musicalEvent); // Notify sequencer

        // Trigger the note on the synth
            foreach (var target in SynthTargets)
            {
                target.NoteOn(ev.Note, ev.Velocity); // Note on
            }

        // Schedule note off with high-precision timing
        // Task.Delay has ~15ms resolution on Windows which causes audible gaps/overlaps.
        // For short durations (<50ms), use spin-wait for precise timing.
        // For longer durations, sleep most of the time then spin-wait the remainder.
        _ = ScheduleNoteOffAsync(ev.Note, durationMs, musicalEvent);
    }

    /// <summary>
    /// Schedules a note-off event after the specified duration with high-precision timing.
    /// Uses a hybrid approach: Task.Delay for the bulk of long waits, then a Stopwatch
    /// spin-wait for the final milliseconds to achieve sub-millisecond accuracy.
    /// </summary>
    private async Task ScheduleNoteOffAsync(int note, double durationMs, MusicalEvent musicalEvent)
    {
        try
        {
            long startTicks = _noteTimer.ElapsedTicks;
            double targetTicks = durationMs * System.Diagnostics.Stopwatch.Frequency / 1000.0;

            if (durationMs > 30)
            {
                // Sleep for most of the duration (leave 10ms margin for spin-wait precision)
                int sleepMs = (int)(durationMs - 10);
                await Task.Delay(sleepMs).ConfigureAwait(false);
            }

            // Spin-wait for the remaining time for precise note-off
            while ((_noteTimer.ElapsedTicks - startTicks) < targetTicks)
            {
                Thread.SpinWait(1);
            }

            foreach (var target in SynthTargets)
            {
                target.NoteOff(note);
            }

            // Notify sequencer that note ended
            musicalEvent.IsNoteOn = false;
            Sequencer?.OnNoteEnded(musicalEvent);
        }
        catch (TaskCanceledException)
        {
            // Task was cancelled, likely during shutdown - this is expected behavior
        }
    }
}
