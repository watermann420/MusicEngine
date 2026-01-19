//Engine License (MEL) – Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: A class for sequencing MIDI patterns and controlling playback with event emission.


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;


namespace MusicEngine.Core;


public class Sequencer
{
    private readonly List<Pattern> _patterns = new(); // patterns to play
    private bool _running; // is the sequencer running?
    private double _bpm = 120.0; // beats per minute
    private Thread? _thread; // playback thread
    private double _beatAccumulator = 0; // current beat position
    private bool _isScratching = false; // is scratching mode enabled?
    private double _defaultLoopLength = 4.0; // default loop length for beat events

    // Event emission for visualization
    private readonly object _eventLock = new();
    private readonly List<MusicalEvent> _activeEvents = new(); // currently playing events

    /// <summary>Fired when a note is triggered (NoteOn).</summary>
    public event EventHandler<MusicalEventArgs>? NoteTriggered;

    /// <summary>Fired when a note ends (NoteOff).</summary>
    public event EventHandler<MusicalEventArgs>? NoteEnded;

    /// <summary>Fired on every beat update (high frequency, ~60fps).</summary>
    public event EventHandler<BeatChangedEventArgs>? BeatChanged;

    /// <summary>Fired when playback starts.</summary>
    public event EventHandler<PlaybackStateEventArgs>? PlaybackStarted;

    /// <summary>Fired when playback stops.</summary>
    public event EventHandler<PlaybackStateEventArgs>? PlaybackStopped;

    /// <summary>Fired when BPM changes.</summary>
    public event EventHandler<ParameterChangedEventArgs>? BpmChanged;

    /// <summary>Fired when a pattern is added.</summary>
    public event EventHandler<Pattern>? PatternAdded;

    /// <summary>Fired when a pattern is removed.</summary>
    public event EventHandler<Pattern>? PatternRemoved;

    /// <summary>Fired when patterns are cleared.</summary>
    public event EventHandler? PatternsCleared;

    // Properties for BPM and current beat
    public double Bpm
    {
        get => _bpm;
        set
        {
            var oldBpm = _bpm;
            _bpm = Math.Max(1.0, value);
            if (Math.Abs(oldBpm - _bpm) > 0.001)
            {
                BpmChanged?.Invoke(this, new ParameterChangedEventArgs("Bpm", oldBpm, _bpm));
            }
        }
    }

    // Current beat position in the sequencer
    public double CurrentBeat
    {
        get => _beatAccumulator; // in beats
        set
        {
            lock (_patterns)
            {
                _beatAccumulator = value;
            }
        }
    }

    /// <summary>Whether the sequencer is currently running.</summary>
    public bool IsRunning => _running;

    /// <summary>Gets currently active (playing) events for visualization.</summary>
    public IReadOnlyList<MusicalEvent> ActiveEvents
    {
        get
        {
            lock (_eventLock)
            {
                // Clean up expired events
                _activeEvents.RemoveAll(e => !e.IsPlaying);
                return _activeEvents.ToArray();
            }
        }
    }

    /// <summary>Gets all patterns.</summary>
    public IReadOnlyList<Pattern> Patterns
    {
        get
        {
            lock (_patterns)
            {
                return _patterns.ToArray();
            }
        }
    }

    /// <summary>Default loop length for beat change events when no patterns exist.</summary>
    public double DefaultLoopLength
    {
        get => _defaultLoopLength;
        set => _defaultLoopLength = Math.Max(0.25, value);
    }

    // Scratching mode property to control playback behavior
    public bool IsScratching
    {
        get => _isScratching;
        set => _isScratching = value;
    }

    // Methods to add, clear patterns and control playback
    public void AddPattern(Pattern pattern)
    {
        lock (_patterns)
        {
            pattern.PatternIndex = _patterns.Count;
            pattern.Sequencer = this; // Link pattern to sequencer for event emission
            _patterns.Add(pattern);
        }
        PatternAdded?.Invoke(this, pattern);
    }

    // Clear all patterns and stop their notes
    public void ClearPatterns()
    {
        lock (_patterns)
        {
            foreach (var pattern in _patterns)
            {
                pattern.Synth.AllNotesOff();
            }
            _patterns.Clear();
        }
        lock (_eventLock)
        {
            _activeEvents.Clear();
        }
        PatternsCleared?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Removes a specific pattern.</summary>
    public void RemovePattern(Pattern pattern)
    {
        lock (_patterns)
        {
            pattern.Synth.AllNotesOff();
            _patterns.Remove(pattern);
            // Re-index remaining patterns
            for (int i = 0; i < _patterns.Count; i++)
            {
                _patterns[i].PatternIndex = i;
            }
        }
        PatternRemoved?.Invoke(this, pattern);
    }

    // Start the sequencer
    public void Start()
    {
        if (_running) return;
        _running = true;
        _thread = new Thread(Run) { IsBackground = true, Priority = ThreadPriority.Highest };
        _thread.Start();
        PlaybackStarted?.Invoke(this, new PlaybackStateEventArgs(true, _beatAccumulator, _bpm));
    }

    // Stop the sequencer
    public void Stop()
    {
        if (!_running) return;
        _running = false;
        _thread?.Join();

        // Clear active events
        lock (_eventLock)
        {
            _activeEvents.Clear();
        }

        PlaybackStopped?.Invoke(this, new PlaybackStateEventArgs(false, _beatAccumulator, _bpm));
    }

    // Skip ahead by a certain number of beats
    public void Skip(double beats)
    {
        lock (_patterns)
        {
            _beatAccumulator += beats;
        }
    }

    /// <summary>Internal method called by Pattern when a note is triggered.</summary>
    internal void OnNoteTriggered(MusicalEvent musicalEvent)
    {
        lock (_eventLock)
        {
            _activeEvents.Add(musicalEvent);
        }
        NoteTriggered?.Invoke(this, new MusicalEventArgs(musicalEvent));
    }

    /// <summary>Internal method called by Pattern when a note ends.</summary>
    internal void OnNoteEnded(MusicalEvent musicalEvent)
    {
        lock (_eventLock)
        {
            _activeEvents.RemoveAll(e => e.Id == musicalEvent.Id);
        }
        NoteEnded?.Invoke(this, new MusicalEventArgs(musicalEvent));
    }

    // Main playback loop
    private void Run()
    {
        var stopwatch = Stopwatch.StartNew(); // High-resolution timer
        double lastTime = stopwatch.Elapsed.TotalSeconds; // Last time checkpoint
        double lastProcessedBeat = _beatAccumulator; // Last processed beat position
        double lastBeatEventTime = 0; // For throttling beat events
        const double beatEventInterval = 1.0 / 60.0; // ~60fps for beat events

        while (_running)
        {
            double currentTime = stopwatch.Elapsed.TotalSeconds; // Current time
            double deltaTime = currentTime - lastTime; // Time delta
            lastTime = currentTime; // Update last time

            double nextBeat; // Next beat position
            lock (_patterns)
            {
                if (!_isScratching)
                {
                    double secondsPerBeat = 60.0 / _bpm; // Calculate seconds per beat
                    double beatsInDelta = deltaTime / secondsPerBeat; // Beats to advance
                    nextBeat = _beatAccumulator + beatsInDelta; // Update next beat
                }
                else
                {
                    nextBeat = _beatAccumulator; // In scratching mode, don't advance
                }

                if (nextBeat != lastProcessedBeat) // Process patterns if beat has changed
                {
                    foreach (var pattern in _patterns) // Process each pattern
                    {
                        pattern.Process(lastProcessedBeat, nextBeat, _bpm); // Process pattern for the beat range
                    }
                    lastProcessedBeat = nextBeat; // Update last processed beat
                }

                if (!_isScratching) // Update beat accumulator if not scratching
                {
                    _beatAccumulator = nextBeat; // Update current beat position
                }
            }

            // Emit beat changed event at ~60fps
            if (currentTime - lastBeatEventTime >= beatEventInterval)
            {
                lastBeatEventTime = currentTime;
                EmitBeatChanged();
            }

            // Thread.Sleep is acceptable here for the playback loop timing because:
            // 1. Actual beat timing is controlled by the high-resolution Stopwatch, not the sleep interval
            // 2. Sleep(1) yields the thread to prevent CPU spinning while allowing ~1000 iterations/second
            // 3. The deltaTime calculation compensates for any sleep timing variance
            // 4. Audio events are time-stamped independently of this loop's frequency
            // Alternative approaches like SpinWait or busy-waiting would consume excessive CPU
            Thread.Sleep(Settings.MidiRefreshRateMs);
        }
    }

    private void EmitBeatChanged()
    {
        // Calculate cycle position based on first pattern or default
        double loopLength;
        lock (_patterns)
        {
            loopLength = _patterns.Count > 0 ? _patterns[0].LoopLength : _defaultLoopLength;
        }

        double cyclePosition = _beatAccumulator % loopLength;
        if (cyclePosition < 0) cyclePosition += loopLength;

        BeatChanged?.Invoke(this, new BeatChangedEventArgs(_beatAccumulator, cyclePosition, loopLength, _bpm));
    }
}
