// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Pattern container for musical events.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace MusicEngine.Core;


// Represents a musical pattern with note events and playback properties
public class Pattern
{
    public ISynth Synth { get; set; } // Synthesizer to play the pattern
    public List<NoteEvent> Events { get; set; } = new(); // Note events in the pattern
    public double LoopLength { get; set; } = 4.0; // in beats
    public bool IsLooping { get; set; } = true; // Looping flag
    public double? StartBeat { get; set; } = null; // When to start the pattern
    public bool Enabled { get; set; } = true;  // Is the pattern enabled?

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

    // Constructor to initialize the pattern with a synth
    public Pattern(ISynth synth)
    {
        Synth = synth;
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
            Synth.AllNotesOff(); // Stop all notes if disabled
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
                TriggerNote(ev, noteIndex, endBeat, cycleNumber, bpm);
            }
        }
    }

    private void TriggerNote(NoteEvent ev, int noteIndex, double absoluteBeat, int cycleNumber, double bpm)
    {
        // Create the musical event with full information
        var durationMs = ev.Duration * (60000.0 / bpm);
        var now = DateTime.Now;

        var musicalEvent = new MusicalEvent
        {
            Id = new EventId(PatternIndex, noteIndex),
            SourcePattern = this,
            NoteEvent = ev,
            Note = ev.Note,
            NoteName = MusicalEvent.GetNoteName(ev.Note),
            Velocity = ev.Velocity,
            Duration = ev.Duration,
            CyclePosition = ev.Beat,
            AbsoluteBeat = absoluteBeat,
            CycleNumber = cycleNumber,
            LoopLength = LoopLength,
            InstrumentName = !string.IsNullOrEmpty(InstrumentName) ? InstrumentName : $"Pattern {PatternIndex}",
            SourceInfo = ev.SourceInfo ?? SourceInfo,
            TriggeredAt = now,
            EndsAt = now.AddMilliseconds(durationMs),
            IsNoteOn = true,
            Bpm = bpm
        };

        // Notify sequencer of the event
        Sequencer?.OnNoteTriggered(musicalEvent);

        // Trigger the note on the synth
        Synth.NoteOn(ev.Note, ev.Velocity);

        // Schedule note off using Task.Delay for non-blocking async timing
        // This is preferred over ThreadPool.QueueUserWorkItem + Thread.Sleep because:
        // 1. Task.Delay uses system timers instead of blocking a thread
        // 2. Reduces thread pool pressure when many notes are playing simultaneously
        // 3. More efficient resource usage for short-duration delays
        _ = ScheduleNoteOffAsync(ev.Note, (int)durationMs, musicalEvent);
    }

    /// <summary>
    /// Schedules a note-off event after the specified duration using async/await.
    /// Uses Task.Delay instead of Thread.Sleep for efficient non-blocking timing.
    /// </summary>
    private async Task ScheduleNoteOffAsync(int note, int durationMs, MusicalEvent musicalEvent)
    {
        try
        {
            await Task.Delay(durationMs).ConfigureAwait(false);
            Synth.NoteOff(note);

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
