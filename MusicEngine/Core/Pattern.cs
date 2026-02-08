// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Minimal pattern container for note events.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MusicEngine.Timing;
using MusicEngine.Vst;

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
    /// List of note sequences in the pattern.
    /// </summary>
    public List<NoteSequence> Sequences { get; } = new();

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
    private readonly Dictionary<Guid, CancellationTokenSource> _activeSequences = new();
    private NoteEvent? _lastAddedEvent;

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
    public Pattern Note(int note, double beat, double duration, int velocity, int? slideTo = null, double? slideTimeMs = null)
    {
        MidiValidation.ValidateNote(note);
        MidiValidation.ValidateVelocity(velocity);
        Guard.NotNegative(beat);

        var useMs = duration > 8.0 || beat > 32.0;
        var ev = new NoteEvent
        {
            Note = note,
            Beat = useMs ? 0.0 : beat,
            Duration = useMs ? 0.0 : duration,
            BeatMs = useMs ? beat : null,
            DurationMs = useMs ? duration : null,
            Velocity = velocity,
            SlideTo = slideTo,
            SlideTimeMs = slideTimeMs
        };
        Events.Add(ev);
        _lastAddedEvent = ev;
        return this;
    }

    /// <summary>
    /// Add a note event using millisecond timing instead of beats.
    /// </summary>
    /// <param name="note">MIDI note number.</param>
    /// <param name="timeMs">Start time in milliseconds.</param>
    /// <param name="durationMs">Duration in milliseconds.</param>
    /// <param name="velocity">MIDI velocity.</param>
    /// <param name="slideTo">Optional slide target note.</param>
    /// <param name="slideTimeMs">Optional slide time in milliseconds.</param>
    /// <returns>This pattern for chaining.</returns>
    public Pattern NoteMs(int note, double timeMs, double durationMs, int velocity, int? slideTo = null, double? slideTimeMs = null)
    {
        MidiValidation.ValidateNote(note);
        MidiValidation.ValidateVelocity(velocity);
        Guard.NotNegative(timeMs);

        var ev = new NoteEvent
        {
            Note = note,
            BeatMs = timeMs,
            DurationMs = durationMs,
            Velocity = velocity,
            SlideTo = slideTo,
            SlideTimeMs = slideTimeMs
        };
        Events.Add(ev);
        _lastAddedEvent = ev;
        return this;
    }

    /// <summary>
    /// Convert the last added note into a sequence using 0/1 step text.
    /// </summary>
    /// <param name="steps">Sequence steps, e.g. "0010101".</param>
    public NoteSequence Siquenz(string steps) => CreateSequence(steps);

    /// <summary>
    /// Convert the last added note into a sequence using 0/1 step text.
    /// </summary>
    /// <param name="steps">Sequence steps, e.g. "0010101".</param>
    public NoteSequence Sequenz(string steps) => CreateSequence(steps);

    private NoteSequence CreateSequence(string steps)
    {
        if (_lastAddedEvent == null)
        {
            throw new InvalidOperationException("Siquenz requires a note before it.");
        }

        var cleaned = NormalizeSequenceSteps(steps);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            throw new InvalidOperationException("Siquenz steps are empty.");
        }

        Events.Remove(_lastAddedEvent);
        var sequence = new NoteSequence(_lastAddedEvent, cleaned);
        Sequences.Add(sequence);
        return sequence;
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
        CancelAllSequences();
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
            var eventBeat = ApplySwing(GetEventBeat(ev, bpm), swing);
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

        foreach (var seq in Sequences)
        {
            if (!seq.Enabled) continue;
            var eventBeat = ApplySwing(GetEventBeat(seq.Note, bpm), swing);
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
                StartSequence(seq, bpm, timingMaster.EnableHumanize, humanize);
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

        var durationMs = ev.DurationMs ?? (ev.Duration * (60000.0 / bpm));
        _ = Task.Run(async () =>
        {
            if (delayMs > 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(delayMs));
            }

            var nowUtc = DateTime.UtcNow;
            CancellationTokenSource? slideToken = null;
            if (ev.SlideTo.HasValue && ev.SlideTo.Value != ev.Note)
            {
                slideToken = new CancellationTokenSource();
            }
            var activity = new NoteActivity
            {
                Note = ev.Note,
                Velocity = velocity,
                StartedUtc = nowUtc,
                SlideCancel = slideToken
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

            if (slideToken != null)
            {
                var slideTimeMs = ev.SlideTimeMs ?? durationMs;
                var clampedMs = Math.Min(durationMs, Math.Max(0.0, slideTimeMs));
                if (clampedMs > 0.0)
                {
                    _ = RunSlide(ev.Note, ev.SlideTo!.Value, clampedMs, slideToken.Token);
                }
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

            slideToken?.Cancel();
            if (ev.SlideTo.HasValue && ev.SlideTo.Value != ev.Note)
            {
                foreach (var target in SynthTargets)
                {
                    ResetPitchBend(target);
                }
            }

            lock (_stateLock)
            {
                _activeNotes.Remove(ev.Note);
            }
        });
    }

    private void StartSequence(NoteSequence sequence, double bpm, bool enableHumanize, HumanizeSettings humanize)
    {
        lock (_stateLock)
        {
            if (_activeSequences.ContainsKey(sequence.Id)) return;
            var tokenSource = new CancellationTokenSource();
            _activeSequences[sequence.Id] = tokenSource;
            _ = RunSequenceAsync(sequence, bpm, enableHumanize, humanize, tokenSource);
        }
    }

    private async Task RunSequenceAsync(NoteSequence sequence, double bpm, bool enableHumanize, HumanizeSettings humanize,
        CancellationTokenSource tokenSource)
    {
        try
        {
            var token = tokenSource.Token;
            var note = sequence.Note;
            var stepDurationMs = note.DurationMs ?? (note.Duration * (60000.0 / bpm));
            if (stepDurationMs <= 0.0) return;

            do
            {
                for (int i = 0; i < sequence.Steps.Length; i++)
                {
                    if (token.IsCancellationRequested) return;

                    if (sequence.Steps[i] == '1')
                    {
                        var velocity = note.Velocity;
                        if (enableHumanize && humanize.Velocity > 0)
                        {
                            var jitter = NextHumanizeJitter();
                            var velDelta = jitter * humanize.Velocity * velocity;
                            velocity = (int)Math.Clamp(Math.Round(velocity + velDelta), 1, 127);
                        }

                        foreach (var target in SynthTargets)
                        {
                            target.NoteOn(note.Note, velocity);
                        }

                        await Task.Delay(TimeSpan.FromMilliseconds(stepDurationMs), token);

                        foreach (var target in SynthTargets)
                        {
                            target.NoteOff(note.Note);
                        }
                    }
                    else
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(stepDurationMs), token);
                    }
                }
            }
            while (sequence.Loop && !token.IsCancellationRequested);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            lock (_stateLock)
            {
                _activeSequences.Remove(sequence.Id);
            }
        }
    }

    private async Task RunSlide(int fromNote, int toNote, double slideTimeMs, CancellationToken token)
    {
        var diff = toNote - fromNote;
        if (diff == 0) return;

        var targets = new List<(ISynth synth, float targetBend)>();
        foreach (var synth in SynthTargets)
        {
            var range = GetPitchBendRangeSemitones(synth);
            if (range <= 0f) continue;
            var bend = Math.Clamp(diff / range, -1f, 1f);
            targets.Add((synth, bend));
        }

        if (targets.Count == 0) return;

        var steps = (int)Math.Max(1, Math.Round(slideTimeMs / 20.0));
        var stepDelay = slideTimeMs / steps;
        for (int i = 1; i <= steps; i++)
        {
            if (token.IsCancellationRequested) return;
            var progress = i / (float)steps;
            foreach (var entry in targets)
            {
                SendPitchBend(entry.synth, entry.targetBend * progress);
            }
            await Task.Delay(TimeSpan.FromMilliseconds(stepDelay), token);
        }
    }

    private static float GetPitchBendRangeSemitones(ISynth synth)
    {
        const float fallback = 2f;
        var prop = synth.GetType().GetProperty("PitchBendRange");
        if (prop == null) return fallback;
        try
        {
            var value = prop.GetValue(synth);
            return value switch
            {
                int i => i,
                float f => f,
                double d => (float)d,
                _ => fallback
            };
        }
        catch
        {
            return fallback;
        }
    }

    private static void SendPitchBend(ISynth synth, float normalized)
    {
        try
        {
            if (synth is IVstInstrument vst)
            {
                vst.PitchBend(normalized);
                return;
            }

            synth.SetParameter("pitchbend", normalized);
        }
        catch
        {
        }
    }

    private static void ResetPitchBend(ISynth synth)
    {
        SendPitchBend(synth, 0f);
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

    private static double GetEventBeat(NoteEvent ev, double bpm)
    {
        if (ev.BeatMs.HasValue)
        {
            return ev.BeatMs.Value * bpm / 60000.0;
        }

        return ev.Beat;
    }

    private void CancelAllSequences()
    {
        lock (_stateLock)
        {
            foreach (var entry in _activeSequences.Values)
            {
                entry.Cancel();
            }
            _activeSequences.Clear();
        }
    }

    private static string NormalizeSequenceSteps(string steps)
    {
        if (string.IsNullOrWhiteSpace(steps)) return string.Empty;
        var buffer = new System.Text.StringBuilder(steps.Length);
        foreach (var ch in steps)
        {
            if (ch == '0' || ch == '1')
            {
                buffer.Append(ch);
            }
        }
        return buffer.ToString();
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

    /// <summary>
    /// Optional millisecond position override.
    /// </summary>
    public double? BeatMs { get; set; }

    /// <summary>
    /// Optional millisecond duration override.
    /// </summary>
    public double? DurationMs { get; set; }

    /// <summary>
    /// Optional slide target note.
    /// </summary>
    public int? SlideTo { get; set; }

    /// <summary>
    /// Optional slide time in beats.
    /// </summary>
    public double? SlideTimeMs { get; set; }
}

/// <summary>
/// Sequence driven by a base note event.
/// </summary>
public sealed class NoteSequence
{
    /// <summary>
    /// Sequence identifier.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// Base note settings (pitch, velocity, step duration).
    /// </summary>
    public NoteEvent Note { get; }

    /// <summary>
    /// Step string using 0/1.
    /// </summary>
    public string Steps { get; set; }

    /// <summary>
    /// Loop the sequence continuously after it starts.
    /// </summary>
    public bool Loop { get; set; }

    /// <summary>
    /// Enable or disable this sequence.
    /// </summary>
    public bool Enabled { get; set; } = true;

    public NoteSequence(NoteEvent note, string steps)
    {
        Note = note;
        Steps = steps;
    }
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

    internal CancellationTokenSource? SlideCancel { get; init; }
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
