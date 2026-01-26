// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Main sequencer for pattern playback and scheduling.

namespace MusicEngine.Core;

/// <summary>
/// A step in the probability sequencer
/// </summary>
public class ProbabilityStep
{
    /// <summary>MIDI note (0-127)</summary>
    public int Note { get; set; } = 60;

    /// <summary>Velocity (0-127)</summary>
    public int Velocity { get; set; } = 100;

    /// <summary>Trigger probability (0-1, 1 = always plays)</summary>
    public float Probability { get; set; } = 1.0f;

    /// <summary>Duration in beats</summary>
    public double Duration { get; set; } = 0.25;

    /// <summary>Step is enabled</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Velocity range for randomization (actual velocity = Velocity +/- VelocityRange)</summary>
    public int VelocityRange { get; set; } = 0;

    /// <summary>Note range for randomization (actual note = Note +/- NoteRange)</summary>
    public int NoteRange { get; set; } = 0;

    /// <summary>Slide/legato to next note</summary>
    public bool Slide { get; set; } = false;

    /// <summary>Accent (increases velocity)</summary>
    public bool Accent { get; set; } = false;

    /// <summary>Ratchet count (1 = normal, 2+ = repeat within step)</summary>
    public int Ratchet { get; set; } = 1;

    /// <summary>Condition for triggering (e.g., "every 2", "1:4")</summary>
    public StepCondition Condition { get; set; } = StepCondition.Always;

    /// <summary>Condition parameter (e.g., for "every N")</summary>
    public int ConditionParam { get; set; } = 2;

    public ProbabilityStep() { }

    public ProbabilityStep(int note, int velocity = 100, float probability = 1.0f)
    {
        Note = note;
        Velocity = velocity;
        Probability = probability;
    }
}

/// <summary>
/// Condition for step triggering
/// </summary>
public enum StepCondition
{
    /// <summary>Always trigger (if probability passes)</summary>
    Always,
    /// <summary>Trigger every N iterations</summary>
    EveryN,
    /// <summary>Trigger on iteration X of N (e.g., 1:4 = first of every 4)</summary>
    NofM,
    /// <summary>Trigger only on first iteration</summary>
    FirstOnly,
    /// <summary>Trigger only after first iteration</summary>
    NotFirst,
    /// <summary>Random - 50% chance independent of probability</summary>
    Random50,
    /// <summary>Fill - only trigger when fill mode is active</summary>
    Fill
}

/// <summary>
/// Probability Sequencer for generative music patterns.
/// Each step can have its own probability, conditions, and variations.
/// </summary>
public class ProbabilitySequencer
{
    private readonly List<ProbabilityStep> _steps = new();
    private int _currentStep;
    private int _iterationCount;
    private double _currentBeat;
    private double _stepLength = 0.25; // Default: 16th notes
    private bool _isPlaying;
    private bool _fillMode;

    /// <summary>Event fired when a note should be triggered</summary>
    public event Action<int, int, double>? NoteTriggered; // note, velocity, duration

    /// <summary>Event fired when step changes</summary>
    public event Action<int>? StepChanged;

    /// <summary>Number of steps in the sequence</summary>
    public int StepCount
    {
        get => _steps.Count;
        set => SetStepCount(value);
    }

    /// <summary>Step length in beats (0.25 = 16th, 0.5 = 8th, 1 = quarter)</summary>
    public double StepLength
    {
        get => _stepLength;
        set => _stepLength = Math.Max(0.0625, value);
    }

    /// <summary>Current step index</summary>
    public int CurrentStep => _currentStep;

    /// <summary>Current iteration count</summary>
    public int IterationCount => _iterationCount;

    /// <summary>Whether the sequencer is playing</summary>
    public bool IsPlaying => _isPlaying;

    /// <summary>Fill mode active (affects Fill condition)</summary>
    public bool FillMode
    {
        get => _fillMode;
        set => _fillMode = value;
    }

    /// <summary>Swing amount (0-1)</summary>
    public float Swing { get; set; } = 0f;

    /// <summary>Global probability multiplier (0-1)</summary>
    public float GlobalProbability { get; set; } = 1.0f;

    /// <summary>Accent velocity boost</summary>
    public int AccentBoost { get; set; } = 20;

    /// <summary>Seed for reproducible randomization</summary>
    public int? Seed
    {
        get => _seed;
        set
        {
            _seed = value;
            if (value.HasValue)
            {
                _random = new Random(value.Value);
            }
        }
    }
    private int? _seed;
    private Random _random;

    public ProbabilitySequencer(int steps = 16)
    {
        _random = new Random();
        SetStepCount(steps);
    }

    /// <summary>
    /// Set the number of steps
    /// </summary>
    public void SetStepCount(int count)
    {
        count = Math.Clamp(count, 1, 64);

        while (_steps.Count < count)
        {
            _steps.Add(new ProbabilityStep());
        }

        while (_steps.Count > count)
        {
            _steps.RemoveAt(_steps.Count - 1);
        }
    }

    /// <summary>
    /// Get a step by index
    /// </summary>
    public ProbabilityStep GetStep(int index)
    {
        if (index < 0 || index >= _steps.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        return _steps[index];
    }

    /// <summary>
    /// Set a step
    /// </summary>
    public void SetStep(int index, ProbabilityStep step)
    {
        if (index < 0 || index >= _steps.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        _steps[index] = step;
    }

    /// <summary>
    /// Set step probability
    /// </summary>
    public void SetProbability(int index, float probability)
    {
        if (index >= 0 && index < _steps.Count)
        {
            _steps[index].Probability = Math.Clamp(probability, 0f, 1f);
        }
    }

    /// <summary>
    /// Set step note
    /// </summary>
    public void SetNote(int index, int note, int velocity = 100)
    {
        if (index >= 0 && index < _steps.Count)
        {
            _steps[index].Note = Math.Clamp(note, 0, 127);
            _steps[index].Velocity = Math.Clamp(velocity, 0, 127);
        }
    }

    /// <summary>
    /// Enable/disable a step
    /// </summary>
    public void SetEnabled(int index, bool enabled)
    {
        if (index >= 0 && index < _steps.Count)
        {
            _steps[index].Enabled = enabled;
        }
    }

    /// <summary>
    /// Start playback
    /// </summary>
    public void Start()
    {
        _isPlaying = true;
        _currentStep = 0;
        _currentBeat = 0;
        _iterationCount = 0;
    }

    /// <summary>
    /// Stop playback
    /// </summary>
    public void Stop()
    {
        _isPlaying = false;
    }

    /// <summary>
    /// Reset to beginning
    /// </summary>
    public void Reset()
    {
        _currentStep = 0;
        _currentBeat = 0;
        _iterationCount = 0;

        if (_seed.HasValue)
        {
            _random = new Random(_seed.Value);
        }
    }

    /// <summary>
    /// Process a tick at the current beat position
    /// </summary>
    public void Process(double currentBeat)
    {
        if (!_isPlaying || _steps.Count == 0) return;

        // Calculate which step we should be on
        double beatsPerStep = _stepLength;

        // Apply swing to off-beats
        double swingOffset = 0;
        if (Swing > 0 && _currentStep % 2 == 1)
        {
            swingOffset = beatsPerStep * Swing * 0.5;
        }

        double stepBeat = _currentStep * beatsPerStep + swingOffset;
        double nextStepBeat = (_currentStep + 1) * beatsPerStep;

        // Check if we've passed the current step
        if (currentBeat >= stepBeat && _currentBeat < stepBeat)
        {
            TriggerStep(_currentStep);
            StepChanged?.Invoke(_currentStep);
        }

        // Move to next step if needed
        if (currentBeat >= nextStepBeat)
        {
            _currentStep++;
            if (_currentStep >= _steps.Count)
            {
                _currentStep = 0;
                _iterationCount++;
            }
        }

        _currentBeat = currentBeat;
    }

    /// <summary>
    /// Manually trigger a step (for external sync)
    /// </summary>
    public void TriggerStep(int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= _steps.Count) return;

        var step = _steps[stepIndex];
        if (!step.Enabled) return;

        // Check condition
        if (!CheckCondition(step)) return;

        // Check probability
        float effectiveProbability = step.Probability * GlobalProbability;
        if (_random.NextDouble() > effectiveProbability) return;

        // Calculate note with variation
        int note = step.Note;
        if (step.NoteRange > 0)
        {
            note += _random.Next(-step.NoteRange, step.NoteRange + 1);
            note = Math.Clamp(note, 0, 127);
        }

        // Calculate velocity with variation
        int velocity = step.Velocity;
        if (step.VelocityRange > 0)
        {
            velocity += _random.Next(-step.VelocityRange, step.VelocityRange + 1);
        }
        if (step.Accent)
        {
            velocity += AccentBoost;
        }
        velocity = Math.Clamp(velocity, 1, 127);

        // Handle ratchet
        if (step.Ratchet > 1)
        {
            double ratchetDuration = step.Duration / step.Ratchet;
            for (int i = 0; i < step.Ratchet; i++)
            {
                // Note: In real implementation, these would be scheduled
                NoteTriggered?.Invoke(note, velocity, ratchetDuration * 0.8);
            }
        }
        else
        {
            NoteTriggered?.Invoke(note, velocity, step.Duration);
        }
    }

    private bool CheckCondition(ProbabilityStep step)
    {
        switch (step.Condition)
        {
            case StepCondition.Always:
                return true;

            case StepCondition.EveryN:
                return (_iterationCount % step.ConditionParam) == 0;

            case StepCondition.NofM:
                return (_iterationCount % step.ConditionParam) == 0;

            case StepCondition.FirstOnly:
                return _iterationCount == 0;

            case StepCondition.NotFirst:
                return _iterationCount > 0;

            case StepCondition.Random50:
                return _random.NextDouble() < 0.5;

            case StepCondition.Fill:
                return _fillMode;

            default:
                return true;
        }
    }

    /// <summary>
    /// Generate a pattern from current sequence state.
    /// Note: Returns a pattern with null Synth - caller must assign a synth before playback.
    /// </summary>
    public Pattern GeneratePattern(ISynth? synth, int iterations = 1)
    {
        var pattern = new Pattern(synth!)
        {
            Name = "Generated Pattern",
            LoopLength = _steps.Count * _stepLength * iterations
        };

        for (int iter = 0; iter < iterations; iter++)
        {
            _iterationCount = iter;

            for (int i = 0; i < _steps.Count; i++)
            {
                var step = _steps[i];
                if (!step.Enabled) continue;
                if (!CheckCondition(step)) continue;

                float effectiveProbability = step.Probability * GlobalProbability;
                if (_random.NextDouble() > effectiveProbability) continue;

                int note = step.Note;
                if (step.NoteRange > 0)
                {
                    note += _random.Next(-step.NoteRange, step.NoteRange + 1);
                    note = Math.Clamp(note, 0, 127);
                }

                int velocity = step.Velocity;
                if (step.VelocityRange > 0)
                {
                    velocity += _random.Next(-step.VelocityRange, step.VelocityRange + 1);
                }
                if (step.Accent)
                {
                    velocity += AccentBoost;
                }
                velocity = Math.Clamp(velocity, 1, 127);

                double startBeat = (iter * _steps.Count + i) * _stepLength;

                // Apply swing
                if (Swing > 0 && i % 2 == 1)
                {
                    startBeat += _stepLength * Swing * 0.5;
                }

                if (step.Ratchet > 1)
                {
                    double ratchetDuration = step.Duration / step.Ratchet;
                    for (int r = 0; r < step.Ratchet; r++)
                    {
                        pattern.Events.Add(new NoteEvent { Note = note, Velocity = velocity,
                            Beat = startBeat + r * ratchetDuration, Duration = ratchetDuration * 0.8 });
                    }
                }
                else
                {
                    pattern.Events.Add(new NoteEvent { Note = note, Velocity = velocity, Beat = startBeat, Duration = step.Duration });
                }
            }
        }

        _iterationCount = 0;
        return pattern;
    }

    /// <summary>
    /// Copy step settings from another step
    /// </summary>
    public void CopyStep(int sourceIndex, int destIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= _steps.Count) return;
        if (destIndex < 0 || destIndex >= _steps.Count) return;

        var source = _steps[sourceIndex];
        _steps[destIndex] = new ProbabilityStep
        {
            Note = source.Note,
            Velocity = source.Velocity,
            Probability = source.Probability,
            Duration = source.Duration,
            Enabled = source.Enabled,
            VelocityRange = source.VelocityRange,
            NoteRange = source.NoteRange,
            Slide = source.Slide,
            Accent = source.Accent,
            Ratchet = source.Ratchet,
            Condition = source.Condition,
            ConditionParam = source.ConditionParam
        };
    }

    /// <summary>
    /// Randomize all probabilities
    /// </summary>
    public void RandomizeProbabilities(float min = 0.3f, float max = 1.0f)
    {
        foreach (var step in _steps)
        {
            step.Probability = min + (float)_random.NextDouble() * (max - min);
        }
    }

    /// <summary>
    /// Set all steps to same probability
    /// </summary>
    public void SetAllProbabilities(float probability)
    {
        foreach (var step in _steps)
        {
            step.Probability = Math.Clamp(probability, 0f, 1f);
        }
    }

    #region Presets

    /// <summary>Create a basic kick pattern</summary>
    public static ProbabilitySequencer CreateKickPattern()
    {
        var seq = new ProbabilitySequencer(16);
        seq.StepLength = 0.25;

        // 4-on-the-floor with variations
        seq.SetNote(0, 36, 120); seq.SetProbability(0, 1.0f);
        seq.SetNote(4, 36, 115); seq.SetProbability(4, 1.0f);
        seq.SetNote(8, 36, 118); seq.SetProbability(8, 1.0f);
        seq.SetNote(12, 36, 115); seq.SetProbability(12, 1.0f);

        // Ghost notes
        seq.SetNote(3, 36, 60); seq.SetProbability(3, 0.3f);
        seq.SetNote(7, 36, 55); seq.SetProbability(7, 0.2f);
        seq.SetNote(11, 36, 50); seq.SetProbability(11, 0.25f);
        seq.SetNote(15, 36, 65); seq.SetProbability(15, 0.4f);

        return seq;
    }

    /// <summary>Create a hi-hat pattern with probability</summary>
    public static ProbabilitySequencer CreateHiHatPattern()
    {
        var seq = new ProbabilitySequencer(16);
        seq.StepLength = 0.25;

        for (int i = 0; i < 16; i++)
        {
            seq.SetNote(i, 42, i % 2 == 0 ? 100 : 70);
            seq.SetProbability(i, i % 2 == 0 ? 1.0f : 0.7f);
            seq.GetStep(i).VelocityRange = 15;
        }

        // Open hi-hat with low probability
        seq.SetNote(6, 46, 90);
        seq.SetProbability(6, 0.4f);
        seq.SetNote(14, 46, 85);
        seq.SetProbability(14, 0.3f);

        return seq;
    }

    /// <summary>Create a generative melodic pattern</summary>
    public static ProbabilitySequencer CreateMelodicPattern(int[] scale)
    {
        var seq = new ProbabilitySequencer(16);
        seq.StepLength = 0.25;

        var random = new Random();

        for (int i = 0; i < 16; i++)
        {
            int scaleIndex = random.Next(scale.Length);
            int octave = random.Next(2) * 12;
            seq.SetNote(i, scale[scaleIndex] + 48 + octave, 80 + random.Next(40));
            seq.SetProbability(i, 0.4f + (float)random.NextDouble() * 0.5f);
            seq.GetStep(i).NoteRange = 0;
            seq.GetStep(i).VelocityRange = 20;
        }

        return seq;
    }

    #endregion
}
