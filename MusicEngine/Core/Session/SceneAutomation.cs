// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

namespace MusicEngine.Core.Session;

/// <summary>
/// Transition type between scenes.
/// </summary>
public enum SceneTransition
{
    /// <summary>Instant switch to new scene.</summary>
    Instant,
    /// <summary>Wait for current clips to finish.</summary>
    WaitForEnd,
    /// <summary>Crossfade audio between scenes.</summary>
    Crossfade,
    /// <summary>Fade out current, then fade in new.</summary>
    FadeOutIn
}

/// <summary>
/// Represents a scheduled scene change in the automation.
/// </summary>
public class SceneAutomationPoint
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Bar position (1-based) when this scene should launch.</summary>
    public int Bar { get; set; }

    /// <summary>Beat position within the bar (0-based).</summary>
    public int Beat { get; set; }

    /// <summary>Scene index to launch.</summary>
    public int SceneIndex { get; set; }

    /// <summary>Transition type to use.</summary>
    public SceneTransition Transition { get; set; } = SceneTransition.Instant;

    /// <summary>Crossfade duration in beats (for crossfade transitions).</summary>
    public double CrossfadeDuration { get; set; } = 4.0;

    /// <summary>Optional tempo change when this scene launches.</summary>
    public double? TempoChange { get; set; }

    /// <summary>Optional time signature numerator change.</summary>
    public int? TimeSignatureNumerator { get; set; }

    /// <summary>Optional time signature denominator change.</summary>
    public int? TimeSignatureDenominator { get; set; }

    /// <summary>Whether this point has been triggered.</summary>
    public bool Triggered { get; internal set; }

    /// <summary>Whether this point is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Optional name/description.</summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets the absolute beat position.
    /// </summary>
    /// <param name="beatsPerBar">Beats per bar (typically 4).</param>
    public double GetAbsoluteBeat(int beatsPerBar = 4)
    {
        return ((Bar - 1) * beatsPerBar) + Beat;
    }

    /// <summary>
    /// Creates a copy of this automation point.
    /// </summary>
    public SceneAutomationPoint Clone()
    {
        return new SceneAutomationPoint
        {
            Bar = Bar,
            Beat = Beat,
            SceneIndex = SceneIndex,
            Transition = Transition,
            CrossfadeDuration = CrossfadeDuration,
            TempoChange = TempoChange,
            TimeSignatureNumerator = TimeSignatureNumerator,
            TimeSignatureDenominator = TimeSignatureDenominator,
            Enabled = Enabled,
            Name = Name
        };
    }
}

/// <summary>
/// Automation sequence for multiple scenes.
/// </summary>
public class SceneAutomationSequence
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Name of this sequence.</summary>
    public string Name { get; set; } = "";

    /// <summary>List of automation points in order.</summary>
    public List<SceneAutomationPoint> Points { get; } = new();

    /// <summary>Whether to loop the sequence.</summary>
    public bool Loop { get; set; }

    /// <summary>Total length in bars (for looping).</summary>
    public int LoopLengthBars { get; set; } = 16;

    /// <summary>Whether this sequence is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Adds a scene change at a specific position.
    /// </summary>
    /// <param name="bar">Bar number (1-based).</param>
    /// <param name="sceneIndex">Scene to launch.</param>
    /// <param name="transition">Transition type.</param>
    /// <returns>The created automation point.</returns>
    public SceneAutomationPoint AddPoint(int bar, int sceneIndex, SceneTransition transition = SceneTransition.Instant)
    {
        var point = new SceneAutomationPoint
        {
            Bar = bar,
            SceneIndex = sceneIndex,
            Transition = transition
        };
        Points.Add(point);
        Points.Sort((a, b) => a.Bar.CompareTo(b.Bar) == 0 ? a.Beat.CompareTo(b.Beat) : a.Bar.CompareTo(b.Bar));
        return point;
    }

    /// <summary>
    /// Removes an automation point.
    /// </summary>
    /// <param name="point">The point to remove.</param>
    public bool RemovePoint(SceneAutomationPoint point)
    {
        return Points.Remove(point);
    }

    /// <summary>
    /// Clears all automation points.
    /// </summary>
    public void Clear()
    {
        Points.Clear();
    }

    /// <summary>
    /// Resets all trigger states.
    /// </summary>
    public void ResetTriggers()
    {
        foreach (var point in Points)
        {
            point.Triggered = false;
        }
    }

    /// <summary>
    /// Creates a copy of this sequence.
    /// </summary>
    public SceneAutomationSequence Clone()
    {
        var clone = new SceneAutomationSequence
        {
            Name = Name + " (Copy)",
            Loop = Loop,
            LoopLengthBars = LoopLengthBars,
            Enabled = Enabled
        };

        foreach (var point in Points)
        {
            clone.Points.Add(point.Clone());
        }

        return clone;
    }
}

/// <summary>
/// Event arguments for scene automation events.
/// </summary>
public class SceneAutomationEventArgs : EventArgs
{
    /// <summary>The automation point that triggered.</summary>
    public required SceneAutomationPoint Point { get; init; }

    /// <summary>The scene that was launched.</summary>
    public Scene? Scene { get; init; }

    /// <summary>Current bar position.</summary>
    public int CurrentBar { get; init; }

    /// <summary>Current beat position.</summary>
    public double CurrentBeat { get; init; }

    /// <summary>Whether a tempo change occurred.</summary>
    public bool TempoChanged { get; init; }

    /// <summary>Whether a time signature change occurred.</summary>
    public bool TimeSignatureChanged { get; init; }
}

/// <summary>
/// Event arguments for crossfade progress.
/// </summary>
public class CrossfadeProgressEventArgs : EventArgs
{
    /// <summary>Source scene index.</summary>
    public int SourceSceneIndex { get; init; }

    /// <summary>Target scene index.</summary>
    public int TargetSceneIndex { get; init; }

    /// <summary>Crossfade progress (0-1).</summary>
    public double Progress { get; init; }

    /// <summary>Source scene volume (0-1).</summary>
    public double SourceVolume { get; init; }

    /// <summary>Target scene volume (0-1).</summary>
    public double TargetVolume { get; init; }
}

/// <summary>
/// Manages automated scene launching and transitions.
/// Provides tempo-synced scene changes, crossfades, and scene-specific settings.
/// </summary>
public class SceneAutomation : IDisposable
{
    private readonly List<SceneAutomationSequence> _sequences = new();
    private readonly object _lock = new();
    private bool _disposed;
    private int _currentBar;
    private double _currentBeat;
    private SceneAutomationSequence? _activeSequence;
    private int _currentPointIndex;
    private bool _isCrossfading;
    private double _crossfadeProgress;
    private int _crossfadeSourceScene;
    private int _crossfadeTargetScene;
    private double _crossfadeDuration;

    /// <summary>Reference to the clip launcher.</summary>
    public ClipLauncher? Launcher { get; set; }

    /// <summary>Whether scene automation is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Whether automation is currently running.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>Current bar position (1-based).</summary>
    public int CurrentBar => _currentBar;

    /// <summary>Current beat position.</summary>
    public double CurrentBeat => _currentBeat;

    /// <summary>Currently active sequence.</summary>
    public SceneAutomationSequence? ActiveSequence => _activeSequence;

    /// <summary>Whether a crossfade is in progress.</summary>
    public bool IsCrossfading => _isCrossfading;

    /// <summary>Current crossfade progress (0-1).</summary>
    public double CrossfadeProgress => _crossfadeProgress;

    /// <summary>Beats per bar for timing calculations.</summary>
    public int BeatsPerBar { get; set; } = 4;

    /// <summary>Fired when a scene automation point is triggered.</summary>
    public event EventHandler<SceneAutomationEventArgs>? SceneTriggered;

    /// <summary>Fired during crossfade transitions.</summary>
    public event EventHandler<CrossfadeProgressEventArgs>? CrossfadeProgress_;

    /// <summary>Fired when tempo changes due to automation.</summary>
    public event EventHandler<double>? TempoChanged;

    /// <summary>Fired when time signature changes due to automation.</summary>
    public event EventHandler<(int Numerator, int Denominator)>? TimeSignatureChanged;

    /// <summary>Fired when a sequence completes.</summary>
    public event EventHandler<SceneAutomationSequence>? SequenceCompleted;

    /// <summary>
    /// Creates a new scene automation manager.
    /// </summary>
    public SceneAutomation()
    {
    }

    /// <summary>
    /// Creates a new scene automation manager with a clip launcher.
    /// </summary>
    /// <param name="launcher">The clip launcher.</param>
    public SceneAutomation(ClipLauncher launcher)
    {
        Launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
    }

    /// <summary>
    /// Adds a sequence to the manager.
    /// </summary>
    /// <param name="sequence">The sequence to add.</param>
    public void AddSequence(SceneAutomationSequence sequence)
    {
        if (sequence == null)
            throw new ArgumentNullException(nameof(sequence));

        lock (_lock)
        {
            _sequences.Add(sequence);
        }
    }

    /// <summary>
    /// Removes a sequence from the manager.
    /// </summary>
    /// <param name="sequence">The sequence to remove.</param>
    public bool RemoveSequence(SceneAutomationSequence sequence)
    {
        lock (_lock)
        {
            if (_activeSequence == sequence)
            {
                Stop();
            }
            return _sequences.Remove(sequence);
        }
    }

    /// <summary>
    /// Gets all sequences.
    /// </summary>
    public IReadOnlyList<SceneAutomationSequence> GetSequences()
    {
        lock (_lock)
        {
            return _sequences.ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Starts playback of a sequence.
    /// </summary>
    /// <param name="sequence">The sequence to play.</param>
    /// <param name="startBar">Starting bar (1-based).</param>
    public void Play(SceneAutomationSequence sequence, int startBar = 1)
    {
        if (sequence == null)
            throw new ArgumentNullException(nameof(sequence));

        lock (_lock)
        {
            _activeSequence = sequence;
            _currentBar = startBar;
            _currentBeat = 0;
            _currentPointIndex = 0;
            sequence.ResetTriggers();
            IsRunning = true;

            // Find first point at or after start position
            for (int i = 0; i < sequence.Points.Count; i++)
            {
                if (sequence.Points[i].Bar >= startBar)
                {
                    _currentPointIndex = i;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Stops the currently playing sequence.
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            IsRunning = false;
            _isCrossfading = false;
            _crossfadeProgress = 0;
        }
    }

    /// <summary>
    /// Pauses automation (maintains position).
    /// </summary>
    public void Pause()
    {
        IsRunning = false;
    }

    /// <summary>
    /// Resumes automation from current position.
    /// </summary>
    public void Resume()
    {
        if (_activeSequence != null)
        {
            IsRunning = true;
        }
    }

    /// <summary>
    /// Processes automation for the given beat range.
    /// Called from the sequencer's process loop.
    /// </summary>
    /// <param name="startBeat">Start beat of the range.</param>
    /// <param name="endBeat">End beat of the range.</param>
    public void Process(double startBeat, double endBeat)
    {
        if (!Enabled || !IsRunning || _activeSequence == null || Launcher == null)
            return;

        lock (_lock)
        {
            // Update current position
            _currentBeat = endBeat;
            _currentBar = (int)(endBeat / BeatsPerBar) + 1;

            // Process crossfade if active
            if (_isCrossfading)
            {
                ProcessCrossfade(endBeat - startBeat);
            }

            // Check for points to trigger
            while (_currentPointIndex < _activeSequence.Points.Count)
            {
                var point = _activeSequence.Points[_currentPointIndex];

                if (!point.Enabled)
                {
                    _currentPointIndex++;
                    continue;
                }

                double pointBeat = point.GetAbsoluteBeat(BeatsPerBar);

                // Handle looping
                if (_activeSequence.Loop)
                {
                    double loopLengthBeats = _activeSequence.LoopLengthBars * BeatsPerBar;
                    pointBeat = pointBeat % loopLengthBeats;
                    double currentModBeat = _currentBeat % loopLengthBeats;

                    if (currentModBeat >= pointBeat && !point.Triggered)
                    {
                        TriggerPoint(point);
                        _currentPointIndex++;
                    }
                    else
                    {
                        break;
                    }

                    // Reset for next loop
                    if (_currentPointIndex >= _activeSequence.Points.Count)
                    {
                        _currentPointIndex = 0;
                        _activeSequence.ResetTriggers();
                    }
                }
                else
                {
                    if (_currentBeat >= pointBeat && !point.Triggered)
                    {
                        TriggerPoint(point);
                        _currentPointIndex++;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            // Check for sequence completion
            if (!_activeSequence.Loop && _currentPointIndex >= _activeSequence.Points.Count)
            {
                IsRunning = false;
                SequenceCompleted?.Invoke(this, _activeSequence);
            }
        }
    }

    private void TriggerPoint(SceneAutomationPoint point)
    {
        point.Triggered = true;

        // Handle tempo change
        bool tempoChanged = false;
        if (point.TempoChange.HasValue && Launcher != null)
        {
            Launcher.Bpm = point.TempoChange.Value;
            TempoChanged?.Invoke(this, point.TempoChange.Value);
            tempoChanged = true;
        }

        // Handle time signature change
        bool tsChanged = false;
        if (point.TimeSignatureNumerator.HasValue || point.TimeSignatureDenominator.HasValue)
        {
            if (Launcher != null)
            {
                if (point.TimeSignatureNumerator.HasValue)
                    Launcher.TimeSignatureNumerator = point.TimeSignatureNumerator.Value;
                if (point.TimeSignatureDenominator.HasValue)
                    Launcher.TimeSignatureDenominator = point.TimeSignatureDenominator.Value;

                TimeSignatureChanged?.Invoke(this,
                    (Launcher.TimeSignatureNumerator, Launcher.TimeSignatureDenominator));
                tsChanged = true;
            }
        }

        // Launch the scene
        Scene? scene = null;
        if (Launcher != null && point.SceneIndex >= 0 && point.SceneIndex < Launcher.SceneCount)
        {
            scene = Launcher.Scenes[point.SceneIndex];

            switch (point.Transition)
            {
                case SceneTransition.Instant:
                    Launcher.LaunchScene(point.SceneIndex);
                    break;

                case SceneTransition.WaitForEnd:
                    // Queue the scene launch
                    Launcher.LaunchScene(point.SceneIndex);
                    break;

                case SceneTransition.Crossfade:
                    StartCrossfade(Launcher.CurrentScene?.Index ?? 0, point.SceneIndex, point.CrossfadeDuration);
                    break;

                case SceneTransition.FadeOutIn:
                    // Start fade-out, scene will launch at midpoint
                    StartCrossfade(Launcher.CurrentScene?.Index ?? 0, point.SceneIndex, point.CrossfadeDuration * 2);
                    break;
            }
        }

        SceneTriggered?.Invoke(this, new SceneAutomationEventArgs
        {
            Point = point,
            Scene = scene,
            CurrentBar = _currentBar,
            CurrentBeat = _currentBeat,
            TempoChanged = tempoChanged,
            TimeSignatureChanged = tsChanged
        });
    }

    private void StartCrossfade(int sourceScene, int targetScene, double durationBeats)
    {
        _isCrossfading = true;
        _crossfadeSourceScene = sourceScene;
        _crossfadeTargetScene = targetScene;
        _crossfadeDuration = durationBeats;
        _crossfadeProgress = 0;

        // Launch target scene at start of crossfade (muted initially)
        Launcher?.LaunchScene(targetScene);
    }

    private void ProcessCrossfade(double deltaBeats)
    {
        _crossfadeProgress += deltaBeats / _crossfadeDuration;

        if (_crossfadeProgress >= 1.0)
        {
            _crossfadeProgress = 1.0;
            _isCrossfading = false;
        }

        // Calculate volumes
        double sourceVol = 1.0 - _crossfadeProgress;
        double targetVol = _crossfadeProgress;

        // Apply equal-power crossfade curve
        sourceVol = Math.Sqrt(sourceVol);
        targetVol = Math.Sqrt(targetVol);

        CrossfadeProgress_?.Invoke(this, new CrossfadeProgressEventArgs
        {
            SourceSceneIndex = _crossfadeSourceScene,
            TargetSceneIndex = _crossfadeTargetScene,
            Progress = _crossfadeProgress,
            SourceVolume = sourceVol,
            TargetVolume = targetVol
        });
    }

    /// <summary>
    /// Seeks to a specific bar position.
    /// </summary>
    /// <param name="bar">Bar to seek to (1-based).</param>
    public void Seek(int bar)
    {
        lock (_lock)
        {
            _currentBar = Math.Max(1, bar);
            _currentBeat = (_currentBar - 1) * BeatsPerBar;

            if (_activeSequence != null)
            {
                // Find appropriate point index
                _currentPointIndex = 0;
                _activeSequence.ResetTriggers();

                for (int i = 0; i < _activeSequence.Points.Count; i++)
                {
                    if (_activeSequence.Points[i].Bar > _currentBar)
                    {
                        _currentPointIndex = i;
                        break;
                    }
                    _activeSequence.Points[i].Triggered = true;
                    _currentPointIndex = i + 1;
                }
            }
        }
    }

    #region Factory Methods

    /// <summary>
    /// Creates a simple sequence that cycles through scenes.
    /// </summary>
    /// <param name="sceneCount">Number of scenes.</param>
    /// <param name="barsPerScene">Bars to spend on each scene.</param>
    public static SceneAutomationSequence CreateCyclingSequence(int sceneCount, int barsPerScene = 8)
    {
        var sequence = new SceneAutomationSequence
        {
            Name = "Scene Cycle",
            Loop = true,
            LoopLengthBars = sceneCount * barsPerScene
        };

        for (int i = 0; i < sceneCount; i++)
        {
            sequence.AddPoint(i * barsPerScene + 1, i);
        }

        return sequence;
    }

    /// <summary>
    /// Creates a sequence from an array of (bar, sceneIndex) tuples.
    /// </summary>
    public static SceneAutomationSequence CreateFromList(IEnumerable<(int Bar, int SceneIndex)> points, string name = "Custom Sequence")
    {
        var sequence = new SceneAutomationSequence { Name = name };

        foreach (var (bar, sceneIndex) in points)
        {
            sequence.AddPoint(bar, sceneIndex);
        }

        return sequence;
    }

    /// <summary>
    /// Creates a sequence with tempo ramps between scenes.
    /// </summary>
    public static SceneAutomationSequence CreateWithTempoChanges(
        IEnumerable<(int Bar, int SceneIndex, double Tempo)> points,
        string name = "Tempo Sequence")
    {
        var sequence = new SceneAutomationSequence { Name = name };

        foreach (var (bar, sceneIndex, tempo) in points)
        {
            var point = sequence.AddPoint(bar, sceneIndex);
            point.TempoChange = tempo;
        }

        return sequence;
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();

        lock (_lock)
        {
            _sequences.Clear();
            _activeSequence = null;
        }

        GC.SuppressFinalize(this);
    }
}
