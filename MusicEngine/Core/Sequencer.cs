//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: A class for sequencing MIDI patterns and controlling playback with event emission.
//              Enhanced with high-resolution timing for sample-accurate music production.


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.Logging;


namespace MusicEngine.Core;


/// <summary>
/// Timing precision mode for the sequencer.
/// </summary>
public enum TimingPrecision
{
    /// <summary>Standard timing using Thread.Sleep (backward compatible, low CPU).</summary>
    Standard,

    /// <summary>High precision timing using HighResolutionTimer (sub-millisecond accuracy).</summary>
    HighPrecision,

    /// <summary>Audio-rate scheduling for sample-accurate timing (highest CPU usage).</summary>
    AudioRate
}

/// <summary>
/// Beat subdivision options for tighter timing resolution.
/// </summary>
public enum BeatSubdivision
{
    /// <summary>No subdivision - process at beat level.</summary>
    None = 1,

    /// <summary>Eighth note subdivision.</summary>
    Eighth = 2,

    /// <summary>Sixteenth note subdivision.</summary>
    Sixteenth = 4,

    /// <summary>Thirty-second note subdivision.</summary>
    ThirtySecond = 8,

    /// <summary>Sixty-fourth note subdivision.</summary>
    SixtyFourth = 16,

    /// <summary>Tick level - 24 PPQN (MIDI standard).</summary>
    Tick = 24,

    /// <summary>High resolution - 96 PPQN (DAW standard).</summary>
    HighResolution = 96,

    /// <summary>Ultra resolution - 480 PPQN (professional DAW standard).</summary>
    UltraResolution = 480
}


public class Sequencer : IDisposable
{
    private readonly List<Pattern> _patterns = new(); // patterns to play
    private bool _running; // is the sequencer running?
    private double _bpm = 120.0; // beats per minute
    private Thread? _thread; // playback thread
    private double _beatAccumulator = 0; // current beat position
    private bool _isScratching = false; // is scratching mode enabled?
    private double _defaultLoopLength = 4.0; // default loop length for beat events
    private bool _disposed = false;

    // High-resolution timing components
    private HighResolutionTimer? _highResTimer;
    private readonly object _timerLock = new();

    // Timing configuration
    private TimingPrecision _timingPrecision = TimingPrecision.Standard;
    private BeatSubdivision _beatSubdivision = BeatSubdivision.Sixteenth;
    private int _sampleRate = Settings.SampleRate;

    // Jitter compensation
    private bool _jitterCompensationEnabled = true;
    private readonly CircularBuffer<double> _timingJitterBuffer = new(64);
    private double _averageTimingJitter;
    private double _jitterCompensationMs;
    private double _lastActualTime;

    // Audio-rate scheduling
    private bool _audioRateEnabled = false;
    private int _samplesPerTick;
    private long _samplePosition;

    // MIDI clock sync
    private MidiClockSync? _midiClockSync;
    private bool _useMidiClockSync = false;

    // Event emission for visualization
    private readonly object _eventLock = new();
    private readonly List<MusicalEvent> _activeEvents = new(); // currently playing events

    // Logging
    private readonly ILogger? _logger;

    /// <summary>Fired when a note is triggered (NoteOn).</summary>
    public event EventHandler<MusicalEventArgs>? NoteTriggered;

    /// <summary>Fired when a note ends (NoteOff).</summary>
    public event EventHandler<MusicalEventArgs>? NoteEnded;

    /// <summary>Fired on every beat update (high frequency, ~60fps or higher).</summary>
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

    /// <summary>Fired when timing jitter is detected above threshold.</summary>
    public event EventHandler<TimingJitterEventArgs>? TimingJitterDetected;

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
                _logger?.LogDebug("BPM changed from {OldBpm} to {NewBpm}", oldBpm, _bpm);
                BpmChanged?.Invoke(this, new ParameterChangedEventArgs("Bpm", oldBpm, _bpm));
                UpdateTimingParameters();

                // Sync MIDI clock if enabled
                if (_midiClockSync != null)
                {
                    _midiClockSync.Bpm = _bpm;
                }
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

                // Update MIDI clock position if enabled
                _midiClockSync?.SetPosition(value);
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

    /// <summary>Gets or sets the timing precision mode.</summary>
    public TimingPrecision TimingPrecision
    {
        get => _timingPrecision;
        set
        {
            if (_running)
            {
                throw new InvalidOperationException("Cannot change timing precision while sequencer is running.");
            }
            _timingPrecision = value;
            UpdateTimingParameters();
        }
    }

    /// <summary>Gets or sets the beat subdivision for tighter timing.</summary>
    public BeatSubdivision Subdivision
    {
        get => _beatSubdivision;
        set
        {
            _beatSubdivision = value;
            UpdateTimingParameters();
        }
    }

    /// <summary>Gets or sets the sample rate for audio-rate scheduling.</summary>
    public int SampleRate
    {
        get => _sampleRate;
        set
        {
            _sampleRate = Math.Max(22050, Math.Min(192000, value));
            UpdateTimingParameters();
        }
    }

    /// <summary>Gets or sets whether jitter compensation is enabled.</summary>
    public bool JitterCompensationEnabled
    {
        get => _jitterCompensationEnabled;
        set => _jitterCompensationEnabled = value;
    }

    /// <summary>Gets the current average timing jitter in milliseconds.</summary>
    public double AverageTimingJitter => _averageTimingJitter;

    /// <summary>Gets the current jitter compensation value in milliseconds.</summary>
    public double JitterCompensation => _jitterCompensationMs;

    /// <summary>Gets or sets whether audio-rate scheduling is enabled.</summary>
    public bool AudioRateEnabled
    {
        get => _audioRateEnabled;
        set
        {
            if (_running)
            {
                throw new InvalidOperationException("Cannot change audio rate mode while sequencer is running.");
            }
            _audioRateEnabled = value;
            if (value)
            {
                _timingPrecision = TimingPrecision.AudioRate;
            }
        }
    }

    /// <summary>Gets the current sample position (for audio-rate scheduling).</summary>
    public long SamplePosition => _samplePosition;

    /// <summary>Gets or sets the MIDI clock sync instance for external synchronization.</summary>
    public MidiClockSync? MidiClock
    {
        get => _midiClockSync;
        set
        {
            if (_midiClockSync != null)
            {
                _midiClockSync.ClockPulse -= OnMidiClockPulse;
                _midiClockSync.Started -= OnMidiClockStarted;
                _midiClockSync.Stopped -= OnMidiClockStopped;
                _midiClockSync.Continued -= OnMidiClockContinued;
                _midiClockSync.PositionChanged -= OnMidiClockPositionChanged;
            }

            _midiClockSync = value;

            if (_midiClockSync != null)
            {
                _midiClockSync.Bpm = _bpm;
                _midiClockSync.ClockPulse += OnMidiClockPulse;
                _midiClockSync.Started += OnMidiClockStarted;
                _midiClockSync.Stopped += OnMidiClockStopped;
                _midiClockSync.Continued += OnMidiClockContinued;
                _midiClockSync.PositionChanged += OnMidiClockPositionChanged;
            }
        }
    }

    /// <summary>Gets or sets whether to use MIDI clock sync for timing.</summary>
    public bool UseMidiClockSync
    {
        get => _useMidiClockSync;
        set => _useMidiClockSync = value;
    }

    /// <summary>
    /// Creates a new sequencer with default settings (backward compatible).
    /// </summary>
    public Sequencer()
    {
        UpdateTimingParameters();
    }

    /// <summary>
    /// Creates a new sequencer with specified timing precision.
    /// </summary>
    /// <param name="precision">The timing precision mode to use.</param>
    public Sequencer(TimingPrecision precision) : this()
    {
        _timingPrecision = precision;
        UpdateTimingParameters();
    }

    /// <summary>
    /// Creates a new sequencer with specified timing precision and subdivision.
    /// </summary>
    public Sequencer(TimingPrecision precision, BeatSubdivision subdivision) : this()
    {
        _timingPrecision = precision;
        _beatSubdivision = subdivision;
        UpdateTimingParameters();
    }

    /// <summary>
    /// Creates a new sequencer with logging support.
    /// </summary>
    public Sequencer(ILogger? logger) : this()
    {
        _logger = logger;
    }

    /// <summary>
    /// Creates a new sequencer with specified timing precision and logging.
    /// </summary>
    public Sequencer(TimingPrecision precision, ILogger? logger) : this(precision)
    {
        _logger = logger;
    }

    private void UpdateTimingParameters()
    {
        // Calculate samples per tick for audio-rate scheduling
        double ticksPerBeat = (int)_beatSubdivision;
        double ticksPerSecond = (_bpm / 60.0) * ticksPerBeat;
        _samplesPerTick = (int)(_sampleRate / ticksPerSecond);

        // Update high-resolution timer if it exists
        lock (_timerLock)
        {
            if (_highResTimer != null && _timingPrecision != TimingPrecision.Standard)
            {
                // Target tick rate based on subdivision
                double targetTicksPerSecond = (_bpm / 60.0) * (int)_beatSubdivision;

                // For high precision, multiply by 4 for smoother timing
                if (_timingPrecision == TimingPrecision.HighPrecision)
                {
                    targetTicksPerSecond *= 4;
                }
                // For audio rate, use much higher rate
                else if (_timingPrecision == TimingPrecision.AudioRate)
                {
                    // Tick at audio buffer rate (e.g., every 128 samples at 44100 = ~344 Hz)
                    targetTicksPerSecond = _sampleRate / 128.0;
                }

                _highResTimer.TargetTicksPerSecond = Math.Min(10000, targetTicksPerSecond);
            }
        }
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
        _logger?.LogDebug("Pattern added: {PatternName}", pattern.Name);
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
        _logger?.LogDebug("All patterns cleared");
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
        _logger?.LogDebug("Pattern removed: {PatternName}", pattern.Name);
        PatternRemoved?.Invoke(this, pattern);
    }

    // Start the sequencer
    public void Start()
    {
        if (_running) return;
        _running = true;

        // Reset jitter tracking
        _timingJitterBuffer.Clear();
        _averageTimingJitter = 0;
        _jitterCompensationMs = 0;
        _samplePosition = 0;

        // Start MIDI clock if enabled
        if (_midiClockSync != null && _midiClockSync.Mode != MidiClockMode.External)
        {
            _midiClockSync.Start(true);
        }

        if (_timingPrecision == TimingPrecision.Standard)
        {
            // Use legacy thread-based timing
            _thread = new Thread(RunStandard) { IsBackground = true, Priority = ThreadPriority.Highest };
            _thread.Start();
        }
        else
        {
            // Use high-resolution timer
            _thread = new Thread(RunHighPrecision) { IsBackground = true, Priority = ThreadPriority.Highest };
            _thread.Start();
        }

        _logger?.LogInformation("Sequencer started at {Bpm} BPM", _bpm);
        PlaybackStarted?.Invoke(this, new PlaybackStateEventArgs(true, _beatAccumulator, _bpm));
    }

    // Stop the sequencer
    public void Stop()
    {
        if (!_running) return;
        _running = false;

        // Stop high-resolution timer
        lock (_timerLock)
        {
            _highResTimer?.Stop();
        }

        _thread?.Join();

        // Stop MIDI clock
        _midiClockSync?.Stop();

        // Clear active events
        lock (_eventLock)
        {
            _activeEvents.Clear();
        }

        _logger?.LogInformation("Sequencer stopped at beat {Beat}", _beatAccumulator);
        PlaybackStopped?.Invoke(this, new PlaybackStateEventArgs(false, _beatAccumulator, _bpm));
    }

    // Skip ahead by a certain number of beats
    public void Skip(double beats)
    {
        lock (_patterns)
        {
            _beatAccumulator += beats;
            _midiClockSync?.SetPosition(_beatAccumulator);
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

    // Standard playback loop (backward compatible)
    private void RunStandard()
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

                    // Apply jitter compensation
                    if (_jitterCompensationEnabled && _jitterCompensationMs > 0)
                    {
                        double jitterBeats = (_jitterCompensationMs / 1000.0) / secondsPerBeat;
                        beatsInDelta -= jitterBeats * 0.1; // Gradual compensation
                    }

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

    // High-precision playback loop with jitter compensation
    private void RunHighPrecision()
    {
        // Create and configure high-resolution timer
        lock (_timerLock)
        {
            _highResTimer = new HighResolutionTimer();
            _highResTimer.JitterCompensationEnabled = _jitterCompensationEnabled;

            // Configure based on precision mode
            if (_timingPrecision == TimingPrecision.AudioRate)
            {
                // Audio buffer rate (128 samples at sample rate)
                _highResTimer.TargetTicksPerSecond = _sampleRate / 128.0;
                _highResTimer.UseSpinWait = true;
            }
            else
            {
                // High precision mode - tick at subdivision rate * 4 for smooth timing
                double ticksPerSecond = (_bpm / 60.0) * (int)_beatSubdivision * 4;
                _highResTimer.TargetTicksPerSecond = Math.Min(10000, ticksPerSecond);
            }
        }

        var stopwatch = Stopwatch.StartNew();
        double lastProcessedBeat = _beatAccumulator;
        double lastBeatEventTime = 0;
        double beatEventInterval = 1.0 / 120.0; // ~120fps for beat events in high-precision mode

        // Track expected vs actual timing for jitter measurement
        double expectedTickInterval = _highResTimer!.TickIntervalMicroseconds / 1_000_000.0;
        double lastTickTime = 0;

        _highResTimer.Start();

        while (_running)
        {
            double currentTime = stopwatch.Elapsed.TotalSeconds;

            // Calculate timing jitter
            if (lastTickTime > 0)
            {
                double actualInterval = currentTime - lastTickTime;
                double jitter = (actualInterval - expectedTickInterval) * 1000.0; // In milliseconds

                _timingJitterBuffer.Push(jitter);
                _averageTimingJitter = _timingJitterBuffer.Average();

                // Update jitter compensation (smooth adjustment)
                if (_jitterCompensationEnabled)
                {
                    _jitterCompensationMs = _jitterCompensationMs * 0.95 + _averageTimingJitter * 0.05;
                }

                // Detect significant jitter
                double jitterThreshold = expectedTickInterval * 100.0; // 10% in ms
                if (Math.Abs(jitter) > jitterThreshold)
                {
                    _logger?.LogWarning("Timing jitter detected: {JitterMs:F3}ms (average: {AvgJitterMs:F3}ms)", jitter, _averageTimingJitter);
                    TimingJitterDetected?.Invoke(this, new TimingJitterEventArgs(
                        jitter, _averageTimingJitter, _beatAccumulator));
                }
            }
            lastTickTime = currentTime;

            double nextBeat;
            lock (_patterns)
            {
                if (!_isScratching)
                {
                    double secondsPerBeat = 60.0 / _bpm;

                    // Calculate beat advancement with jitter compensation
                    double deltaTime = currentTime - _lastActualTime;
                    if (_lastActualTime == 0) deltaTime = 0;
                    _lastActualTime = currentTime;

                    double beatsInDelta = deltaTime / secondsPerBeat;

                    // Apply jitter compensation
                    if (_jitterCompensationEnabled && Math.Abs(_jitterCompensationMs) > 0.001)
                    {
                        double compensationBeats = (_jitterCompensationMs / 1000.0) / secondsPerBeat;
                        beatsInDelta -= compensationBeats * 0.05; // Very gradual compensation
                    }

                    nextBeat = _beatAccumulator + beatsInDelta;

                    // Update sample position for audio-rate scheduling
                    if (_timingPrecision == TimingPrecision.AudioRate)
                    {
                        _samplePosition = (long)(nextBeat * secondsPerBeat * _sampleRate);
                    }
                }
                else
                {
                    nextBeat = _beatAccumulator;
                }

                // Process with finer granularity based on subdivision
                if (nextBeat != lastProcessedBeat)
                {
                    double subdivisionSize = 1.0 / (int)_beatSubdivision;
                    double startSubdiv = Math.Floor(lastProcessedBeat / subdivisionSize) * subdivisionSize;
                    double endSubdiv = Math.Ceiling(nextBeat / subdivisionSize) * subdivisionSize;

                    // Process patterns
                    foreach (var pattern in _patterns)
                    {
                        pattern.Process(lastProcessedBeat, nextBeat, _bpm);
                    }
                    lastProcessedBeat = nextBeat;
                }

                if (!_isScratching)
                {
                    _beatAccumulator = nextBeat;
                }
            }

            // Emit beat changed event at higher rate for high-precision mode
            if (currentTime - lastBeatEventTime >= beatEventInterval)
            {
                lastBeatEventTime = currentTime;
                EmitBeatChanged();
            }

            // Yield to prevent CPU spinning while maintaining precision
            if (_timingPrecision == TimingPrecision.AudioRate)
            {
                Thread.SpinWait(10);
            }
            else
            {
                Thread.Yield();
            }
        }

        lock (_timerLock)
        {
            _highResTimer?.Stop();
            _highResTimer?.Dispose();
            _highResTimer = null;
        }
    }

    // MIDI clock event handlers
    private void OnMidiClockPulse(object? sender, MidiClockEventArgs e)
    {
        if (!_useMidiClockSync || _midiClockSync?.Mode == MidiClockMode.Internal) return;

        // Update beat position from external clock
        lock (_patterns)
        {
            _beatAccumulator = e.BeatPosition;
        }
    }

    private void OnMidiClockStarted(object? sender, MidiClockEventArgs e)
    {
        if (!_useMidiClockSync || _midiClockSync?.Mode == MidiClockMode.Internal) return;

        // External clock started - start sequencer if not running
        if (!_running)
        {
            _beatAccumulator = 0;
            Start();
        }
    }

    private void OnMidiClockStopped(object? sender, MidiClockEventArgs e)
    {
        if (!_useMidiClockSync || _midiClockSync?.Mode == MidiClockMode.Internal) return;

        // External clock stopped - stop sequencer
        if (_running)
        {
            Stop();
        }
    }

    private void OnMidiClockContinued(object? sender, MidiClockEventArgs e)
    {
        if (!_useMidiClockSync || _midiClockSync?.Mode == MidiClockMode.Internal) return;

        // External clock continued - resume if not running
        if (!_running)
        {
            Start();
        }
    }

    private void OnMidiClockPositionChanged(object? sender, MidiClockEventArgs e)
    {
        if (!_useMidiClockSync || _midiClockSync?.Mode == MidiClockMode.Internal) return;

        // Update position from Song Position Pointer
        lock (_patterns)
        {
            _beatAccumulator = e.BeatPosition;
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

    /// <summary>
    /// Gets timing statistics for diagnostics.
    /// </summary>
    public TimingStatistics GetTimingStatistics()
    {
        return new TimingStatistics
        {
            AverageJitterMs = _averageTimingJitter,
            JitterCompensationMs = _jitterCompensationMs,
            TimingPrecision = _timingPrecision,
            Subdivision = _beatSubdivision,
            SampleRate = _sampleRate,
            SamplePosition = _samplePosition,
            HighResTimerActive = _highResTimer?.IsRunning ?? false,
            HighResTimerTicks = _highResTimer?.TotalTicks ?? 0,
            MidiClockActive = _midiClockSync?.IsRunning ?? false,
            MidiClockPulseCount = _midiClockSync?.PulseCount ?? 0
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();

        // Dispose MIDI clock sync
        _midiClockSync?.Dispose();
        _midiClockSync = null;

        GC.SuppressFinalize(this);
    }

    ~Sequencer()
    {
        Dispose();
    }
}

/// <summary>
/// Event arguments for timing jitter detection.
/// </summary>
public class TimingJitterEventArgs : EventArgs
{
    /// <summary>The jitter amount in milliseconds.</summary>
    public double JitterMs { get; }

    /// <summary>The average jitter over recent samples.</summary>
    public double AverageJitterMs { get; }

    /// <summary>The beat position when jitter was detected.</summary>
    public double BeatPosition { get; }

    public TimingJitterEventArgs(double jitterMs, double averageJitterMs, double beatPosition)
    {
        JitterMs = jitterMs;
        AverageJitterMs = averageJitterMs;
        BeatPosition = beatPosition;
    }
}

/// <summary>
/// Timing statistics for diagnostics and monitoring.
/// </summary>
public class TimingStatistics
{
    /// <summary>Average timing jitter in milliseconds.</summary>
    public double AverageJitterMs { get; init; }

    /// <summary>Current jitter compensation value in milliseconds.</summary>
    public double JitterCompensationMs { get; init; }

    /// <summary>Current timing precision mode.</summary>
    public TimingPrecision TimingPrecision { get; init; }

    /// <summary>Current beat subdivision.</summary>
    public BeatSubdivision Subdivision { get; init; }

    /// <summary>Sample rate for audio-rate scheduling.</summary>
    public int SampleRate { get; init; }

    /// <summary>Current sample position.</summary>
    public long SamplePosition { get; init; }

    /// <summary>Whether the high-resolution timer is active.</summary>
    public bool HighResTimerActive { get; init; }

    /// <summary>Total ticks from the high-resolution timer.</summary>
    public long HighResTimerTicks { get; init; }

    /// <summary>Whether MIDI clock sync is active.</summary>
    public bool MidiClockActive { get; init; }

    /// <summary>Total MIDI clock pulses.</summary>
    public long MidiClockPulseCount { get; init; }

    public override string ToString()
    {
        return $"Timing: {TimingPrecision}, Subdivision: {Subdivision}, " +
               $"Jitter: {AverageJitterMs:F3}ms, Compensation: {JitterCompensationMs:F3}ms, " +
               $"HiRes: {(HighResTimerActive ? "Active" : "Inactive")}, " +
               $"MIDI Clock: {(MidiClockActive ? "Active" : "Inactive")}";
    }
}
