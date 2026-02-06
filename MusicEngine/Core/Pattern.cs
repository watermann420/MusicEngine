// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Minimal pattern container for note events.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MusicEngine.Timing;

namespace MusicEngine.Core;

public sealed class Pattern
{
    public Guid Id { get; } = Guid.NewGuid();
    public ISynth Synth { get; }
    public List<ISynth> SynthTargets { get; } = new();
    public List<NoteEvent> Events { get; } = new();
    public double LoopLength { get; set; } = 4.0;
    public bool IsLooping { get; set; } = true;
    public double? StartBeat { get; set; }
    public bool Enabled { get; set; } = true;
    public Sequencer? Sequencer { get; internal set; }
    public TimingSettings Timing { get; } = new TimingSettings();
    public double CurrentBeat => _currentBeat;

    public NoteActivity? LastTriggeredNote { get; private set; }
    public double? LastTriggeredBeat { get; private set; }
    public DateTime? LastTriggeredUtc { get; private set; }

    private double _currentBeat;
    private Random? _humanizeRandom;
    private int? _humanizeSeed;
    private readonly object _stateLock = new();
    private readonly Dictionary<int, NoteActivity> _activeNotes = new();

    public Pattern(ISynth synth, params ISynth[] moreSynths)
    {
        Synth = synth;
        SynthTargets.Add(synth);
        if (moreSynths != null && moreSynths.Length > 0)
        {
            SynthTargets.AddRange(moreSynths);
        }
    }

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

    public void Play()
    {
        if (Sequencer == null) return;
        Sequencer.AddPattern(this);
        if (!Sequencer.IsRunning)
        {
            Sequencer.Start();
        }
    }

    public void Stop()
    {
        Sequencer?.RemovePattern(this);
        foreach (var target in SynthTargets)
        {
            target.AllNotesOff();
        }
    }

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

            foreach (var target in SynthTargets)
            {
                target.NoteOn(ev.Note, velocity);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(durationMs));
            foreach (var target in SynthTargets)
            {
                target.NoteOff(ev.Note);
            }

            lock (_stateLock)
            {
                _activeNotes.Remove(ev.Note);
            }
        });
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

public sealed class NoteEvent
{
    public double Beat { get; set; }
    public int Note { get; set; }
    public int Velocity { get; set; }
    public double Duration { get; set; }
}

public sealed class NoteActivity
{
    public int Note { get; init; }
    public int Velocity { get; init; }
    public DateTime StartedUtc { get; init; }
}
