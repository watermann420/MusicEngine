// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Minimal pattern container for note events.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MusicEngine.Timing;

namespace MusicEngine.Core;

/// <summary>
/// Pattern container for step-based note events and playback state.
/// </summary>
public sealed class Pattern
{
    /// <summary>
    /// Global note event callback for pattern playback.
    /// </summary>
    public static Action<int, bool, int>? NoteEvent;
    private static volatile bool _editorModeEnabled;
    /// <summary>
    /// Unique pattern identifier.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// Primary synth used for this pattern.
    /// </summary>
    public ISynth Synth { get; }

    /// <summary>
    /// Synths targeted by this pattern (includes <see cref="Synth"/>).
    /// </summary>
    public List<ISynth> SynthTargets { get; } = new();

    /// <summary>
    /// List of note events in the pattern.
    /// </summary>
    public List<NoteEvent> Events { get; } = new();

    /// <summary>
    /// Pattern length in beats.
    /// </summary>
    public double LoopLength { get; set; } = 4.0;

    /// <summary>
    /// Whether the pattern loops after <see cref="LoopLength"/>.
    /// </summary>
    public bool IsLooping { get; set; } = true;

    /// <summary>
    /// Optional absolute start beat for scheduling.
    /// </summary>
    public double? StartBeat { get; set; }

    /// <summary>
    /// Enable or disable pattern playback.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Sequencer instance driving this pattern, if any.
    /// </summary>
    public Sequencer? Sequencer { get; internal set; }

    /// <summary>
    /// Timing settings for this pattern.
    /// </summary>
    public TimingSettings Timing { get; } = new TimingSettings();

    /// <summary>
    /// Current playback beat position.
    /// </summary>
    public double CurrentBeat => _currentBeat;

    /// <summary>
    /// Seek the pattern to an absolute beat position.
    /// </summary>
    /// <param name="beat">Target beat position.</param>
    /// <param name="stopNotes">Stop any currently active notes.</param>
    public void SeekBeat(double beat, bool stopNotes = true)
    {
        if (stopNotes)
        {
            foreach (var target in SynthTargets)
            {
                target.AllNotesOff();
            }
        }

        lock (_stateLock)
        {
            _currentBeat = beat;
            StartBeat = beat;
            if (stopNotes)
            {
                _activeNotes.Clear();
                LastTriggeredNote = null;
                LastTriggeredBeat = null;
                LastTriggeredUtc = null;
            }
        }
    }

    /// <summary>
    /// Last note activity triggered by this pattern.
    /// </summary>
    public NoteActivity? LastTriggeredNote { get; private set; }

    /// <summary>
    /// Beat position of the last triggered note.
    /// </summary>
    public double? LastTriggeredBeat { get; private set; }

    /// <summary>
    /// UTC timestamp of the last triggered note.
    /// </summary>
    public DateTime? LastTriggeredUtc { get; private set; }

    private double _currentBeat;
    private Random? _humanizeRandom;
    private int? _humanizeSeed;
    private readonly object _stateLock = new();
    private readonly Dictionary<int, NoteActivity> _activeNotes = new();

    /// <summary>
    /// Raised when a note triggers in editor mode.
    /// </summary>
    public event Action<PatternNoteEventInfo>? EditorNoteEvent;

    /// <summary>
    /// Enable or disable editor events globally for patterns.
    /// </summary>
    /// <param name="enabled">True to enable editor events.</param>
    public static void SetEditorMode(bool enabled)
    {
        _editorModeEnabled = enabled;
    }

    /// <summary>
    /// Create a new pattern targeting one or more synths.
    /// </summary>
    /// <param name="synth">Primary synth.</param>
    /// <param name="moreSynths">Additional synth targets.</param>
    public Pattern(ISynth synth, params ISynth[] moreSynths)
    {
        Synth = synth;
        SynthTargets.Add(synth);
        if (moreSynths != null && moreSynths.Length > 0)
        {
            SynthTargets.AddRange(moreSynths);
        }
    }

    /// <summary>
    /// Add a note event to the pattern.
    /// </summary>
    /// <param name="note">MIDI note number.</param>
    /// <param name="beat">Beat position within the loop.</param>
    /// <param name="duration">Duration in beats.</param>
    /// <param name="velocity">MIDI velocity.</param>
    /// <returns>This pattern for chaining.</returns>
    public Pattern Note(int note, double beat, double duration, int velocity)
    {
        MidiValidation.ValidateNote(note);
        MidiValidation.ValidateVelocity(velocity);
        Guard.NotNegative(beat);

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
    /// Add this pattern to its sequencer and start playback.
    /// </summary>
    public void Play()
    {
        if (Sequencer == null) return;
        Sequencer.AddPattern(this);
        if (!Sequencer.IsRunning)
        {
            Sequencer.Start();
        }
    }

    /// <summary>
    /// Remove this pattern from its sequencer and stop all notes.
    /// </summary>
    public void Stop()
    {
        Sequencer?.RemovePattern(this);
        foreach (var target in SynthTargets)
        {
            target.AllNotesOff();
        }
    }

    /// <summary>
    /// Advance playback and trigger events for the current time slice.
    /// </summary>
    /// <param name="deltaSeconds">Delta time in seconds.</param>
    /// <param name="timingMaster">Timing master to pull BPM/groove from.</param>
    public void Process(double deltaSeconds, TimingMaster timingMaster)
    {
        if (!Enabled) return;
        var bpm = Timing.Bpm ?? timingMaster.Bpm;
        bpm = bpm <= 0 ? 120.0 : bpm;

        var beatDelta = deltaSeconds * bpm / 60.0;
        var startBeat = _currentBeat;
        var endBeat = _currentBeat + beatDelta;
        _currentBeat = endBeat;

        StartBeat ??= startBeat;

        double relativeStart = startBeat - StartBeat.Value;
        double relativeEnd = endBeat - StartBeat.Value;

        if (!IsLooping && relativeStart >= LoopLength) return;

        double startMod = ModBeat(relativeStart, LoopLength);
        double endMod = ModBeat(relativeEnd, LoopLength);
        bool wrapped = endMod < startMod;

        var groove = Timing.UseMasterGroove ? timingMaster.Groove : Timing.Groove;
        var swing = timingMaster.EnableGroove ? Math.Clamp(groove.Swing, 0.0, 1.0) : 0.0;
        var humanize = groove.Humanize;

        EnsureHumanizeRandom(humanize.Seed);

        foreach (var ev in Events)
        {
            var eventBeat = ApplySwing(ev.Beat, swing);
            bool trigger;
            if (!IsLooping)
            {
                trigger = eventBeat >= relativeStart && eventBeat < relativeEnd && eventBeat < LoopLength;
            }
            else if (!wrapped)
            {
                trigger = eventBeat >= startMod && eventBeat < endMod;
            }
            else
            {
                trigger = eventBeat >= startMod || eventBeat < endMod;
            }

            if (trigger)
            {
                TriggerNote(ev, bpm, timingMaster.EnableHumanize, humanize, eventBeat);
            }
        }
    }

    private static double ModBeat(double value, double mod)
    {
        var result = value % mod;
        return result < 0 ? result + mod : result;
    }

    private void TriggerNote(NoteEvent ev, double bpm, bool enableHumanize, HumanizeSettings humanize, double eventBeat)
    {
        var delayMs = 0.0;
        var velocity = ev.Velocity;

        if (enableHumanize && humanize.TimeMs > 0)
        {
            var jitter = NextHumanizeJitter();
            delayMs = Math.Max(0.0, jitter * humanize.TimeMs);
        }

        if (enableHumanize && humanize.Velocity > 0)
        {
            var jitter = NextHumanizeJitter();
            var velDelta = jitter * humanize.Velocity * velocity;
            velocity = (int)Math.Clamp(Math.Round(velocity + velDelta), 1, 127);
        }

        var durationMs = ev.Duration * (60000.0 / bpm);
        _ = Task.Run(async () =>
        {
            if (delayMs > 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(delayMs));
            }

            var nowUtc = DateTime.UtcNow;
            var activity = new NoteActivity
            {
                Note = ev.Note,
                Velocity = velocity,
                StartedUtc = nowUtc
            };
            lock (_stateLock)
            {
                _activeNotes[ev.Note] = activity;
                LastTriggeredNote = activity;
                LastTriggeredBeat = eventBeat;
                LastTriggeredUtc = nowUtc;
            }

            try
            {
                EmitEditorNoteEvent(ev.Note, velocity, isOn: true);
                NoteEvent?.Invoke(ev.Note, true, velocity);
            }
            catch
            {
            }

            foreach (var target in SynthTargets)
            {
                target.NoteOn(ev.Note, velocity);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(durationMs));
            foreach (var target in SynthTargets)
            {
                target.NoteOff(ev.Note);
            }

            try
            {
                EmitEditorNoteEvent(ev.Note, velocity, isOn: false);
                NoteEvent?.Invoke(ev.Note, false, velocity);
            }
            catch
            {
            }

            lock (_stateLock)
            {
                _activeNotes.Remove(ev.Note);
            }
        });
    }

    private void EmitEditorNoteEvent(int note, int velocity, bool isOn)
    {
        if (!_editorModeEnabled) return;
        var handler = EditorNoteEvent;
        if (handler == null) return;
        var info = new PatternNoteEventInfo(Id, note, velocity, isOn, DateTime.UtcNow);
        handler(info);
    }

    private static double ApplySwing(double beat, double swing)
    {
        if (swing <= 0) return beat;
        var frac = beat - Math.Floor(beat);
        if (frac < 0.5) return beat;
        var offset = 0.25 * swing;
        return beat + offset;
    }

    private void EnsureHumanizeRandom(int? seed)
    {
        if (_humanizeRandom == null || _humanizeSeed != seed)
        {
            _humanizeSeed = seed;
            _humanizeRandom = seed.HasValue ? new Random(seed.Value) : new Random();
        }
    }

    private double NextHumanizeJitter()
    {
        if (_humanizeRandom == null) return 0.0;
        return _humanizeRandom.NextDouble() * 2.0 - 1.0;
    }

    /// <summary>
    /// Snapshot of currently active notes.
    /// </summary>
    /// <returns>List of active note activities.</returns>
    public IReadOnlyList<NoteActivity> GetActiveNotesSnapshot()
    {
        lock (_stateLock)
        {
            if (_activeNotes.Count == 0) return Array.Empty<NoteActivity>();
            var snapshot = new NoteActivity[_activeNotes.Count];
            int index = 0;
            foreach (var entry in _activeNotes.Values)
            {
                snapshot[index++] = entry;
            }
            return snapshot;
        }
    }
}

/// <summary>
/// Pattern note event configuration.
/// </summary>
public sealed class NoteEvent
{
    /// <summary>
    /// Beat position of the note.
    /// </summary>
    public double Beat { get; set; }
    /// <summary>
    /// MIDI note number.
    /// </summary>
    public int Note { get; set; }
    /// <summary>
    /// MIDI velocity.
    /// </summary>
    public int Velocity { get; set; }
    /// <summary>
    /// Duration in beats.
    /// </summary>
    public double Duration { get; set; }
}

/// <summary>
/// Runtime note activity information.
/// </summary>
public sealed class NoteActivity
{
    /// <summary>
    /// MIDI note number.
    /// </summary>
    public int Note { get; init; }
    /// <summary>
    /// Velocity used when triggered.
    /// </summary>
    public int Velocity { get; init; }
    /// <summary>
    /// UTC timestamp when the note started.
    /// </summary>
    public DateTime StartedUtc { get; init; }
}

/// <summary>
/// Lightweight note event data for editor feedback.
/// </summary>
public readonly struct PatternNoteEventInfo
{
    /// <summary>
    /// Pattern identifier.
    /// </summary>
    public Guid PatternId { get; }
    /// <summary>
    /// MIDI note number.
    /// </summary>
    public int Note { get; }
    /// <summary>
    /// MIDI velocity.
    /// </summary>
    public int Velocity { get; }
    /// <summary>
    /// True when this is a note-on event.
    /// </summary>
    public bool IsOn { get; }
    /// <summary>
    /// UTC timestamp of the event.
    /// </summary>
    public DateTime TimestampUtc { get; }

    /// <summary>
    /// Create a new pattern note event info record.
    /// </summary>
    public PatternNoteEventInfo(Guid patternId, int note, int velocity, bool isOn, DateTime timestampUtc)
    {
        PatternId = patternId;
        Note = note;
        Velocity = velocity;
        IsOn = isOn;
        TimestampUtc = timestampUtc;
    }
}
