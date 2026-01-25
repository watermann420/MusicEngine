//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Classic step sequencer for trigger-based pattern creation.

namespace MusicEngine.Core;

/// <summary>
/// A single step in the sequencer
/// </summary>
public class SequencerStep
{
    /// <summary>Step is active (will trigger)</summary>
    public bool Active { get; set; }

    /// <summary>MIDI note (0-127)</summary>
    public int Note { get; set; } = 60;

    /// <summary>Velocity (0-127)</summary>
    public int Velocity { get; set; } = 100;

    /// <summary>Gate length as percentage of step (0-1)</summary>
    public float Gate { get; set; } = 0.5f;

    /// <summary>Slide/legato to next note</summary>
    public bool Slide { get; set; }

    /// <summary>Accent (boosts velocity)</summary>
    public bool Accent { get; set; }

    /// <summary>Retrigger count within step (1 = normal)</summary>
    public int Retrigger { get; set; } = 1;

    public SequencerStep() { }

    public SequencerStep(bool active, int note = 60, int velocity = 100)
    {
        Active = active;
        Note = note;
        Velocity = velocity;
    }
}

/// <summary>
/// A row/track in the step sequencer (for drum machines)
/// </summary>
public class SequencerRow
{
    /// <summary>Row name (e.g., "Kick", "Snare")</summary>
    public string Name { get; set; } = "";

    /// <summary>MIDI note for this row</summary>
    public int Note { get; set; } = 36;

    /// <summary>Steps in this row</summary>
    public SequencerStep[] Steps { get; }

    /// <summary>Row is muted</summary>
    public bool Muted { get; set; }

    /// <summary>Row is soloed</summary>
    public bool Soloed { get; set; }

    /// <summary>Row volume (0-1)</summary>
    public float Volume { get; set; } = 1.0f;

    public SequencerRow(int stepCount)
    {
        Steps = new SequencerStep[stepCount];
        for (int i = 0; i < stepCount; i++)
        {
            Steps[i] = new SequencerStep();
        }
    }

    public SequencerRow(string name, int note, int stepCount) : this(stepCount)
    {
        Name = name;
        Note = note;
    }
}

/// <summary>
/// Step sequencer direction/mode
/// </summary>
public enum SequencerDirection
{
    /// <summary>Play forward</summary>
    Forward,
    /// <summary>Play backward</summary>
    Backward,
    /// <summary>Ping-pong (forward then backward)</summary>
    PingPong,
    /// <summary>Random step selection</summary>
    Random
}

/// <summary>
/// Classic step sequencer for trigger-based pattern creation.
/// Supports both melodic (single row) and drum machine (multi-row) modes.
/// </summary>
public class StepSequencer
{
    private readonly List<SequencerRow> _rows = new();
    private int _currentStep;
    private bool _pingPongForward = true;
    private readonly Random _random = new();
    private double _lastBeat = -1;
    private int _lastTriggeredStep = -1;

    /// <summary>Event fired when a note should be triggered</summary>
    public event Action<int, int, float>? NoteTriggered; // note, velocity, gate

    /// <summary>Event fired when a note should be released</summary>
    public event Action<int>? NoteReleased;

    /// <summary>Event fired when step changes</summary>
    public event Action<int>? StepChanged;

    /// <summary>Number of steps</summary>
    public int StepCount { get; }

    /// <summary>Current step index</summary>
    public int CurrentStep => _currentStep;

    /// <summary>Playback direction</summary>
    public SequencerDirection Direction { get; set; } = SequencerDirection.Forward;

    /// <summary>Step length in beats (0.25 = 16th, 0.5 = 8th)</summary>
    public double StepLength { get; set; } = 0.25;

    /// <summary>Swing amount (0-1)</summary>
    public float Swing { get; set; }

    /// <summary>Accent velocity boost</summary>
    public int AccentBoost { get; set; } = 25;

    /// <summary>Default gate length for new steps</summary>
    public float DefaultGate { get; set; } = 0.5f;

    /// <summary>Is the sequencer running</summary>
    public bool IsRunning { get; private set; }

    /// <summary>Loop enabled</summary>
    public bool Loop { get; set; } = true;

    /// <summary>First step of loop range</summary>
    public int LoopStart { get; set; }

    /// <summary>Last step of loop range (exclusive)</summary>
    public int LoopEnd { get; set; }

    /// <summary>
    /// Creates a step sequencer
    /// </summary>
    public StepSequencer(int steps = 16)
    {
        StepCount = Math.Clamp(steps, 1, 64);
        LoopEnd = StepCount;
    }

    /// <summary>
    /// Add a row to the sequencer (drum machine mode)
    /// </summary>
    public SequencerRow AddRow(string name, int note)
    {
        var row = new SequencerRow(name, note, StepCount);
        _rows.Add(row);
        return row;
    }

    /// <summary>
    /// Get row by index
    /// </summary>
    public SequencerRow? GetRow(int index)
    {
        if (index >= 0 && index < _rows.Count)
            return _rows[index];
        return null;
    }

    /// <summary>
    /// Get row by name
    /// </summary>
    public SequencerRow? GetRow(string name)
    {
        return _rows.FirstOrDefault(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Remove a row
    /// </summary>
    public bool RemoveRow(SequencerRow row)
    {
        return _rows.Remove(row);
    }

    /// <summary>
    /// Get all rows
    /// </summary>
    public IReadOnlyList<SequencerRow> Rows => _rows;

    /// <summary>
    /// Set step active state
    /// </summary>
    public void SetStep(int rowIndex, int stepIndex, bool active)
    {
        if (rowIndex >= 0 && rowIndex < _rows.Count &&
            stepIndex >= 0 && stepIndex < StepCount)
        {
            _rows[rowIndex].Steps[stepIndex].Active = active;
        }
    }

    /// <summary>
    /// Toggle step active state
    /// </summary>
    public void ToggleStep(int rowIndex, int stepIndex)
    {
        if (rowIndex >= 0 && rowIndex < _rows.Count &&
            stepIndex >= 0 && stepIndex < StepCount)
        {
            var step = _rows[rowIndex].Steps[stepIndex];
            step.Active = !step.Active;
        }
    }

    /// <summary>
    /// Set step properties
    /// </summary>
    public void SetStepProperties(int rowIndex, int stepIndex,
        bool? active = null, int? velocity = null, float? gate = null,
        bool? slide = null, bool? accent = null, int? retrigger = null)
    {
        if (rowIndex >= 0 && rowIndex < _rows.Count &&
            stepIndex >= 0 && stepIndex < StepCount)
        {
            var step = _rows[rowIndex].Steps[stepIndex];

            if (active.HasValue) step.Active = active.Value;
            if (velocity.HasValue) step.Velocity = Math.Clamp(velocity.Value, 0, 127);
            if (gate.HasValue) step.Gate = Math.Clamp(gate.Value, 0f, 1f);
            if (slide.HasValue) step.Slide = slide.Value;
            if (accent.HasValue) step.Accent = accent.Value;
            if (retrigger.HasValue) step.Retrigger = Math.Clamp(retrigger.Value, 1, 8);
        }
    }

    /// <summary>
    /// Start playback
    /// </summary>
    public void Start()
    {
        IsRunning = true;
        _currentStep = LoopStart;
        _pingPongForward = true;
        _lastBeat = -1;
        _lastTriggeredStep = -1;
    }

    /// <summary>
    /// Stop playback
    /// </summary>
    public void Stop()
    {
        IsRunning = false;

        // Release all notes
        foreach (var row in _rows)
        {
            NoteReleased?.Invoke(row.Note);
        }
    }

    /// <summary>
    /// Reset to beginning
    /// </summary>
    public void Reset()
    {
        _currentStep = LoopStart;
        _pingPongForward = true;
        _lastBeat = -1;
        _lastTriggeredStep = -1;
    }

    /// <summary>
    /// Process at current beat position
    /// </summary>
    public void Process(double currentBeat)
    {
        if (!IsRunning || _rows.Count == 0) return;

        // Calculate step position from beat
        double beatsPerStep = StepLength;
        int effectiveSteps = LoopEnd - LoopStart;
        if (effectiveSteps <= 0) effectiveSteps = StepCount;

        // Apply swing to off-beats
        double swingOffset = 0;
        if (Swing > 0 && _currentStep % 2 == 1)
        {
            swingOffset = beatsPerStep * Swing * 0.5;
        }

        double stepBeat = (_currentStep - LoopStart) * beatsPerStep + swingOffset;

        // Normalize to loop
        double loopLength = effectiveSteps * beatsPerStep;
        double normalizedBeat = currentBeat % loopLength;

        // Check if we should trigger current step
        if (_lastTriggeredStep != _currentStep &&
            normalizedBeat >= stepBeat &&
            (_lastBeat < stepBeat || _lastBeat > normalizedBeat))
        {
            TriggerCurrentStep();
            _lastTriggeredStep = _currentStep;
            StepChanged?.Invoke(_currentStep);
        }

        // Advance step if needed
        double nextStepBeat = ((_currentStep - LoopStart + 1) % effectiveSteps) * beatsPerStep;
        if (normalizedBeat >= nextStepBeat && _lastBeat < nextStepBeat)
        {
            AdvanceStep();
        }

        _lastBeat = normalizedBeat;
    }

    private void TriggerCurrentStep()
    {
        bool anySoloed = _rows.Any(r => r.Soloed);

        foreach (var row in _rows)
        {
            // Skip muted rows
            if (row.Muted) continue;

            // If any row is soloed, only play soloed rows
            if (anySoloed && !row.Soloed) continue;

            if (_currentStep >= 0 && _currentStep < row.Steps.Length)
            {
                var step = row.Steps[_currentStep];

                if (step.Active)
                {
                    int velocity = step.Velocity;
                    if (step.Accent)
                    {
                        velocity = Math.Min(127, velocity + AccentBoost);
                    }
                    velocity = (int)(velocity * row.Volume);

                    float gate = step.Gate;

                    // Handle retrigger
                    if (step.Retrigger > 1)
                    {
                        float retriggeredGate = gate / step.Retrigger;
                        for (int i = 0; i < step.Retrigger; i++)
                        {
                            NoteTriggered?.Invoke(row.Note, velocity, retriggeredGate * 0.9f);
                        }
                    }
                    else
                    {
                        NoteTriggered?.Invoke(row.Note, velocity, gate);
                    }
                }
            }
        }
    }

    private void AdvanceStep()
    {
        int effectiveStart = LoopStart;
        int effectiveEnd = LoopEnd;

        switch (Direction)
        {
            case SequencerDirection.Forward:
                _currentStep++;
                if (_currentStep >= effectiveEnd)
                {
                    if (Loop)
                        _currentStep = effectiveStart;
                    else
                        Stop();
                }
                break;

            case SequencerDirection.Backward:
                _currentStep--;
                if (_currentStep < effectiveStart)
                {
                    if (Loop)
                        _currentStep = effectiveEnd - 1;
                    else
                        Stop();
                }
                break;

            case SequencerDirection.PingPong:
                if (_pingPongForward)
                {
                    _currentStep++;
                    if (_currentStep >= effectiveEnd)
                    {
                        _currentStep = effectiveEnd - 2;
                        if (_currentStep < effectiveStart) _currentStep = effectiveStart;
                        _pingPongForward = false;
                    }
                }
                else
                {
                    _currentStep--;
                    if (_currentStep < effectiveStart)
                    {
                        _currentStep = effectiveStart + 1;
                        if (_currentStep >= effectiveEnd) _currentStep = effectiveEnd - 1;
                        _pingPongForward = true;
                    }
                }
                break;

            case SequencerDirection.Random:
                _currentStep = _random.Next(effectiveStart, effectiveEnd);
                break;
        }

        _lastTriggeredStep = -1; // Allow next step to trigger
    }

    /// <summary>
    /// Generate a Pattern from current sequencer state.
    /// Note: Returns a pattern - caller must assign a synth before playback.
    /// </summary>
    public Pattern ToPattern(ISynth? synth, int iterations = 1)
    {
        var pattern = new Pattern(synth!)
        {
            Name = "Step Sequence",
            LoopLength = StepCount * StepLength * iterations
        };

        for (int iter = 0; iter < iterations; iter++)
        {
            for (int step = 0; step < StepCount; step++)
            {
                double startBeat = (iter * StepCount + step) * StepLength;

                // Apply swing
                if (Swing > 0 && step % 2 == 1)
                {
                    startBeat += StepLength * Swing * 0.5;
                }

                foreach (var row in _rows)
                {
                    if (row.Muted) continue;

                    var s = row.Steps[step];
                    if (!s.Active) continue;

                    int velocity = s.Velocity;
                    if (s.Accent)
                    {
                        velocity = Math.Min(127, velocity + AccentBoost);
                    }
                    velocity = (int)(velocity * row.Volume);

                    double duration = StepLength * s.Gate;

                    if (s.Retrigger > 1)
                    {
                        double retrigDuration = duration / s.Retrigger;
                        for (int r = 0; r < s.Retrigger; r++)
                        {
                            pattern.Events.Add(new NoteEvent {
                                Note = row.Note, Velocity = velocity,
                                Beat = startBeat + r * retrigDuration,
                                Duration = retrigDuration * 0.9 });
                        }
                    }
                    else
                    {
                        pattern.Events.Add(new NoteEvent { Note = row.Note, Velocity = velocity, Beat = startBeat, Duration = duration });
                    }
                }
            }
        }

        return pattern;
    }

    /// <summary>
    /// Load pattern from array of booleans (simple on/off)
    /// </summary>
    public void LoadPattern(int rowIndex, bool[] pattern)
    {
        if (rowIndex < 0 || rowIndex >= _rows.Count) return;

        var row = _rows[rowIndex];
        for (int i = 0; i < Math.Min(pattern.Length, StepCount); i++)
        {
            row.Steps[i].Active = pattern[i];
        }
    }

    /// <summary>
    /// Clear all steps
    /// </summary>
    public void Clear()
    {
        foreach (var row in _rows)
        {
            foreach (var step in row.Steps)
            {
                step.Active = false;
            }
        }
    }

    /// <summary>
    /// Copy row pattern to another row
    /// </summary>
    public void CopyRow(int sourceRow, int destRow)
    {
        if (sourceRow < 0 || sourceRow >= _rows.Count) return;
        if (destRow < 0 || destRow >= _rows.Count) return;
        if (sourceRow == destRow) return;

        var src = _rows[sourceRow];
        var dest = _rows[destRow];

        for (int i = 0; i < StepCount; i++)
        {
            dest.Steps[i].Active = src.Steps[i].Active;
            dest.Steps[i].Velocity = src.Steps[i].Velocity;
            dest.Steps[i].Gate = src.Steps[i].Gate;
            dest.Steps[i].Accent = src.Steps[i].Accent;
            dest.Steps[i].Slide = src.Steps[i].Slide;
            dest.Steps[i].Retrigger = src.Steps[i].Retrigger;
        }
    }

    #region Presets

    /// <summary>Create a basic 808-style drum machine</summary>
    public static StepSequencer Create808DrumMachine()
    {
        var seq = new StepSequencer(16);

        seq.AddRow("Kick", 36);
        seq.AddRow("Snare", 38);
        seq.AddRow("Closed HH", 42);
        seq.AddRow("Open HH", 46);
        seq.AddRow("Clap", 39);
        seq.AddRow("Tom Low", 41);
        seq.AddRow("Tom Mid", 45);
        seq.AddRow("Tom High", 48);

        return seq;
    }

    /// <summary>Create with basic 4/4 beat</summary>
    public static StepSequencer CreateBasicBeat()
    {
        var seq = Create808DrumMachine();

        // Kick: 1 and 3
        seq.LoadPattern(0, new[] { true, false, false, false, true, false, false, false,
                                   true, false, false, false, true, false, false, false });

        // Snare: 2 and 4
        seq.LoadPattern(1, new[] { false, false, false, false, true, false, false, false,
                                   false, false, false, false, true, false, false, false });

        // Hi-hat: all 8ths
        seq.LoadPattern(2, new[] { true, false, true, false, true, false, true, false,
                                   true, false, true, false, true, false, true, false });

        return seq;
    }

    #endregion
}
