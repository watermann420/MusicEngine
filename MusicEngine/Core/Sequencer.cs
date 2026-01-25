//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: A class for sequencing MIDI patterns and controlling playback with event emission.
//              Enhanced with high-resolution timing for sample-accurate music production.
//              Extended with Arrangement support for AudioClips and MidiClips playback.


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    private volatile bool _running; // is the sequencer running? (volatile for thread-safe reads)
    private double _bpm = 120.0; // beats per minute (accessed under lock)
    private Thread? _thread; // playback thread
    private double _beatAccumulator = 0; // current beat position (accessed under lock)
    private volatile bool _isScratching = false; // is scratching mode enabled? (volatile for thread-safe reads)
    private double _defaultLoopLength = 4.0; // default loop length for beat events
    private volatile bool _disposed = false; // volatile for thread-safe dispose check

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

    // Tempo and time signature tracking
    private TempoTrack? _tempoTrack;

    // Arrangement support
    private Arrangement? _arrangement;
    private readonly object _arrangementLock = new();
    private readonly HashSet<Guid> _activeAudioClipIds = [];
    private readonly HashSet<Guid> _activeMidiClipIds = [];
    private readonly Dictionary<Guid, ISynth?> _midiClipSynths = new();
    private bool _loopingEnabled = true;

    // Metronome integration
    private Metronome? _metronome;
    private bool _enableMetronome = false;
    private bool _isWaitingForCountIn = false;

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

    /// <summary>Fired when time signature changes.</summary>
    public event EventHandler<TimeSignatureChangedEventArgs>? TimeSignatureChanged;

    /// <summary>Fired when a pattern is added.</summary>
    public event EventHandler<Pattern>? PatternAdded;

    /// <summary>Fired when a pattern is removed.</summary>
    public event EventHandler<Pattern>? PatternRemoved;

    /// <summary>Fired when patterns are cleared.</summary>
    public event EventHandler? PatternsCleared;

    /// <summary>Fired when timing jitter is detected above threshold.</summary>
    public event EventHandler<TimingJitterEventArgs>? TimingJitterDetected;

    /// <summary>Fired when an audio clip starts playing.</summary>
    public event EventHandler<AudioClipEventArgs>? AudioClipStarted;

    /// <summary>Fired when an audio clip stops playing.</summary>
    public event EventHandler<AudioClipEventArgs>? AudioClipEnded;

    /// <summary>Fired when a MIDI clip starts playing.</summary>
    public event EventHandler<MidiClipEventArgs>? MidiClipStarted;

    /// <summary>Fired when a MIDI clip stops playing.</summary>
    public event EventHandler<MidiClipEventArgs>? MidiClipEnded;

    /// <summary>Fired when the arrangement is changed.</summary>
    public event EventHandler<ArrangementChangedEventArgs>? ArrangementSet;

    // Properties for BPM and current beat
    public double Bpm
    {
        get => _tempoTrack != null ? _tempoTrack.GetTempoAt(_beatAccumulator) : _bpm;
        set
        {
            var oldBpm = _bpm;
            _bpm = Math.Max(1.0, value);

            // Update tempo track default if available
            if (_tempoTrack != null)
            {
                _tempoTrack.DefaultBpm = _bpm;
            }

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

    /// <summary>Gets the effective BPM at the current position, accounting for tempo changes.</summary>
    public double EffectiveBpm => _tempoTrack?.GetTempoAt(_beatAccumulator) ?? _bpm;

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

    /// <summary>Gets or sets the tempo track for advanced tempo and time signature control.</summary>
    public TempoTrack? TempoTrack
    {
        get => _tempoTrack;
        set
        {
            _tempoTrack = value;
            if (_tempoTrack != null)
            {
                _tempoTrack.SampleRate = _sampleRate;
                _bpm = _tempoTrack.DefaultBpm;
                UpdateTimingParameters();
            }
        }
    }

    /// <summary>Gets the current time signature at the current position.</summary>
    public TimeSignature CurrentTimeSignature =>
        _tempoTrack?.GetTimeSignatureAtBeats(_beatAccumulator) ?? TimeSignature.Common;

    /// <summary>Gets the current bar number (0-indexed).</summary>
    public int CurrentBar => _tempoTrack?.BeatsToBar(_beatAccumulator) ?? (int)(_beatAccumulator / 4.0);

    /// <summary>Gets the current beat within the current bar (0-indexed).</summary>
    public double CurrentBeatInBar
    {
        get
        {
            if (_tempoTrack != null)
            {
                var (_, beat) = _tempoTrack.BeatsToBarBeat(_beatAccumulator);
                return beat;
            }
            return _beatAccumulator % 4.0;
        }
    }

    /// <summary>Gets the current position in seconds.</summary>
    public double CurrentTimeSeconds => _tempoTrack?.BeatsToSeconds(_beatAccumulator) ??
        (_beatAccumulator * 60.0 / _bpm);

    /// <summary>Gets the current position in samples.</summary>
    public long CurrentTimeSamples => _tempoTrack?.BeatsToSamples(_beatAccumulator) ??
        (long)(CurrentTimeSeconds * _sampleRate);

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

    /// <summary>Gets the currently assigned arrangement.</summary>
    public Arrangement? Arrangement
    {
        get
        {
            lock (_arrangementLock)
            {
                return _arrangement;
            }
        }
    }

    /// <summary>Gets or sets whether looping is enabled when an arrangement loop region is set.</summary>
    public bool LoopingEnabled
    {
        get => _loopingEnabled;
        set => _loopingEnabled = value;
    }

    /// <summary>Gets the IDs of currently playing audio clips.</summary>
    public IReadOnlySet<Guid> ActiveAudioClipIds
    {
        get
        {
            lock (_arrangementLock)
            {
                return _activeAudioClipIds.ToHashSet();
            }
        }
    }

    /// <summary>Gets the IDs of currently playing MIDI clips.</summary>
    public IReadOnlySet<Guid> ActiveMidiClipIds
    {
        get
        {
            lock (_arrangementLock)
            {
                return _activeMidiClipIds.ToHashSet();
            }
        }
    }

    /// <summary>
    /// Gets or sets the metronome associated with this sequencer.
    /// When set, the metronome will automatically sync with the sequencer.
    /// </summary>
    public Metronome? Metronome
    {
        get => _metronome;
        set
        {
            // Detach previous metronome if it was attached
            if (_metronome != null && _metronome.IsAttachedToSequencer)
            {
                _metronome.DetachFromSequencer();
            }

            _metronome = value;

            // Attach new metronome if provided and EnableMetronome is true
            if (_metronome != null && _enableMetronome)
            {
                _metronome.AttachToSequencer(this);
            }
        }
    }

    /// <summary>
    /// Gets or sets whether the metronome is enabled.
    /// When enabled, the metronome will play clicks in sync with the sequencer.
    /// </summary>
    public bool EnableMetronome
    {
        get => _enableMetronome;
        set
        {
            _enableMetronome = value;

            if (_metronome != null)
            {
                if (value && !_metronome.IsAttachedToSequencer)
                {
                    _metronome.AttachToSequencer(this);
                }
                else if (!value && _metronome.IsAttachedToSequencer)
                {
                    _metronome.DetachFromSequencer();
                }

                _metronome.Enabled = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets the number of count-in bars before playback starts.
    /// Delegates to Metronome.CountIn if a metronome is assigned.
    /// Valid values are 0, 1, 2, or 4.
    /// </summary>
    public int MetronomeCountIn
    {
        get => _metronome?.CountIn ?? 0;
        set
        {
            if (_metronome != null)
            {
                _metronome.CountIn = value;
            }
        }
    }

    /// <summary>Gets whether the sequencer is currently waiting for count-in to complete.</summary>
    public bool IsWaitingForCountIn => _isWaitingForCountIn;

    /// <summary>
    /// Creates a new sequencer with default settings (backward compatible).
    /// </summary>
    public Sequencer()
    {
        _tempoTrack = new TempoTrack(_bpm, TimeSignature.Common, _sampleRate);
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
    /// Creates a new sequencer with a custom tempo track.
    /// </summary>
    /// <param name="tempoTrack">The tempo track to use.</param>
    public Sequencer(TempoTrack tempoTrack) : this()
    {
        _tempoTrack = tempoTrack ?? throw new ArgumentNullException(nameof(tempoTrack));
        _bpm = _tempoTrack.DefaultBpm;
        _sampleRate = _tempoTrack.SampleRate;
        UpdateTimingParameters();
    }

    /// <summary>
    /// Creates a new sequencer with specified BPM and time signature.
    /// </summary>
    /// <param name="bpm">The initial tempo in BPM.</param>
    /// <param name="timeSignature">The initial time signature.</param>
    public Sequencer(double bpm, TimeSignature timeSignature) : this()
    {
        _bpm = Math.Max(1.0, bpm);
        _tempoTrack = new TempoTrack(_bpm, timeSignature, _sampleRate);
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

    #region Arrangement Support

    /// <summary>
    /// Sets the arrangement for playback.
    /// </summary>
    /// <param name="arrangement">The arrangement to play, or null to clear.</param>
    public void SetArrangement(Arrangement? arrangement)
    {
        lock (_arrangementLock)
        {
            // Stop all active clips from previous arrangement
            StopAllClipsInternal();

            _arrangement = arrangement;
            _activeAudioClipIds.Clear();
            _activeMidiClipIds.Clear();
            _midiClipSynths.Clear();

            // Sync BPM from arrangement if available
            if (_arrangement != null)
            {
                _bpm = _arrangement.Bpm;
                _logger?.LogInformation("Arrangement set: {ArrangementName} at {Bpm} BPM",
                    _arrangement.Name, _arrangement.Bpm);
            }
            else
            {
                _logger?.LogInformation("Arrangement cleared");
            }
        }

        ArrangementSet?.Invoke(this, new ArrangementChangedEventArgs(
            arrangement != null ? ArrangementChangeType.SectionModified : ArrangementChangeType.Cleared));
    }

    /// <summary>
    /// Assigns a synthesizer to a MIDI clip for playback.
    /// </summary>
    /// <param name="clipId">The MIDI clip ID.</param>
    /// <param name="synth">The synthesizer to use for the clip.</param>
    public void AssignSynthToMidiClip(Guid clipId, ISynth synth)
    {
        lock (_arrangementLock)
        {
            _midiClipSynths[clipId] = synth;
        }
        _logger?.LogDebug("Synth assigned to MIDI clip {ClipId}: {SynthName}", clipId, synth.Name);
    }

    /// <summary>
    /// Assigns synthesizers to MIDI clips by track index.
    /// </summary>
    /// <param name="trackSynths">Dictionary mapping track index to synthesizer.</param>
    public void AssignSynthsByTrack(Dictionary<int, ISynth> trackSynths)
    {
        if (_arrangement == null) return;

        lock (_arrangementLock)
        {
            foreach (var clip in _arrangement.MidiClips)
            {
                if (trackSynths.TryGetValue(clip.TrackIndex, out var synth))
                {
                    _midiClipSynths[clip.Id] = synth;
                }
            }
        }
        _logger?.LogDebug("Assigned synths to {Count} tracks", trackSynths.Count);
    }

    /// <summary>
    /// Gets the synthesizer assigned to a MIDI clip.
    /// </summary>
    /// <param name="clipId">The MIDI clip ID.</param>
    /// <returns>The assigned synthesizer, or null if none.</returns>
    public ISynth? GetSynthForMidiClip(Guid clipId)
    {
        lock (_arrangementLock)
        {
            return _midiClipSynths.GetValueOrDefault(clipId);
        }
    }

    /// <summary>
    /// Stops all currently playing clips from the arrangement.
    /// </summary>
    public void StopAllClips()
    {
        lock (_arrangementLock)
        {
            StopAllClipsInternal();
        }
    }

    private void StopAllClipsInternal()
    {
        // Send note-off to all synths used by active MIDI clips
        foreach (var clipId in _activeMidiClipIds.ToList())
        {
            if (_midiClipSynths.TryGetValue(clipId, out var synth))
            {
                synth?.AllNotesOff();
            }

            // Fire clip ended event
            if (_arrangement != null)
            {
                var clip = _arrangement.GetMidiClip(clipId);
                if (clip != null)
                {
                    MidiClipEnded?.Invoke(this, new MidiClipEventArgs(clip, _beatAccumulator));
                }
            }
        }

        // Fire audio clip ended events
        foreach (var clipId in _activeAudioClipIds.ToList())
        {
            if (_arrangement != null)
            {
                var clip = _arrangement.GetAudioClip(clipId);
                if (clip != null)
                {
                    AudioClipEnded?.Invoke(this, new AudioClipEventArgs(clip, _beatAccumulator));
                }
            }
        }

        _activeAudioClipIds.Clear();
        _activeMidiClipIds.Clear();
    }

    /// <summary>
    /// Processes arrangement clips for the given beat range.
    /// </summary>
    /// <param name="startBeat">Start of the beat range.</param>
    /// <param name="endBeat">End of the beat range.</param>
    private void ProcessArrangementClips(double startBeat, double endBeat)
    {
        if (_arrangement == null) return;

        lock (_arrangementLock)
        {
            // Process Audio Clips
            ProcessAudioClips(startBeat, endBeat);

            // Process MIDI Clips
            ProcessMidiClips(startBeat, endBeat);
        }
    }

    /// <summary>
    /// Processes audio clips in the arrangement for the given beat range.
    /// </summary>
    private void ProcessAudioClips(double startBeat, double endBeat)
    {
        if (_arrangement == null) return;

        var audioClips = _arrangement.GetAudioClipsInRange(startBeat, endBeat);

        foreach (var clip in audioClips)
        {
            if (clip.IsMuted) continue;

            // Check if clip should start
            if (clip.StartPosition >= startBeat && clip.StartPosition < endBeat)
            {
                if (!_activeAudioClipIds.Contains(clip.Id))
                {
                    _activeAudioClipIds.Add(clip.Id);
                    _logger?.LogDebug("Audio clip started: {ClipName} at beat {Beat}", clip.Name, clip.StartPosition);
                    AudioClipStarted?.Invoke(this, new AudioClipEventArgs(clip, clip.StartPosition));
                }
            }

            // Check if clip should end
            if (clip.EndPosition > startBeat && clip.EndPosition <= endBeat)
            {
                if (_activeAudioClipIds.Contains(clip.Id))
                {
                    _activeAudioClipIds.Remove(clip.Id);
                    _logger?.LogDebug("Audio clip ended: {ClipName} at beat {Beat}", clip.Name, clip.EndPosition);
                    AudioClipEnded?.Invoke(this, new AudioClipEventArgs(clip, clip.EndPosition));
                }
            }
        }

        // Clean up clips that have ended (position moved past them)
        var clipIdsToRemove = new List<Guid>();
        foreach (var clipId in _activeAudioClipIds)
        {
            var clip = _arrangement.GetAudioClip(clipId);
            if (clip == null || endBeat >= clip.EndPosition)
            {
                clipIdsToRemove.Add(clipId);
                if (clip != null)
                {
                    _logger?.LogDebug("Audio clip ended (cleanup): {ClipName}", clip.Name);
                    AudioClipEnded?.Invoke(this, new AudioClipEventArgs(clip, clip.EndPosition));
                }
            }
        }
        foreach (var id in clipIdsToRemove)
        {
            _activeAudioClipIds.Remove(id);
        }
    }

    /// <summary>
    /// Processes MIDI clips in the arrangement for the given beat range.
    /// </summary>
    private void ProcessMidiClips(double startBeat, double endBeat)
    {
        if (_arrangement == null) return;

        var midiClips = _arrangement.GetMidiClipsInRange(startBeat, endBeat);

        foreach (var clip in midiClips)
        {
            if (clip.IsMuted) continue;

            // Check if clip should start
            if (clip.StartPosition >= startBeat && clip.StartPosition < endBeat)
            {
                if (!_activeMidiClipIds.Contains(clip.Id))
                {
                    _activeMidiClipIds.Add(clip.Id);
                    _logger?.LogDebug("MIDI clip started: {ClipName} at beat {Beat}", clip.Name, clip.StartPosition);
                    MidiClipStarted?.Invoke(this, new MidiClipEventArgs(clip, clip.StartPosition));
                }
            }

            // Process notes in the clip
            var synth = _midiClipSynths.GetValueOrDefault(clip.Id) ?? clip.Pattern?.Synth;
            if (synth != null)
            {
                // Get notes that should trigger in this range
                var notes = clip.GetNotesInRange(startBeat, endBeat);

                foreach (var note in notes)
                {
                    TriggerMidiClipNote(synth, note, clip);
                }
            }

            // Check if clip should end
            if (clip.EndPosition > startBeat && clip.EndPosition <= endBeat)
            {
                if (_activeMidiClipIds.Contains(clip.Id))
                {
                    _activeMidiClipIds.Remove(clip.Id);
                    _logger?.LogDebug("MIDI clip ended: {ClipName} at beat {Beat}", clip.Name, clip.EndPosition);

                    // Stop all notes from this clip's synth
                    synth?.AllNotesOff();
                    MidiClipEnded?.Invoke(this, new MidiClipEventArgs(clip, clip.EndPosition));
                }
            }
        }

        // Clean up MIDI clips that have ended
        var clipIdsToRemove = new List<Guid>();
        foreach (var clipId in _activeMidiClipIds)
        {
            var clip = _arrangement.GetMidiClip(clipId);
            if (clip == null || endBeat >= clip.EndPosition)
            {
                clipIdsToRemove.Add(clipId);
                if (clip != null)
                {
                    var synth = _midiClipSynths.GetValueOrDefault(clipId) ?? clip.Pattern?.Synth;
                    synth?.AllNotesOff();
                    _logger?.LogDebug("MIDI clip ended (cleanup): {ClipName}", clip.Name);
                    MidiClipEnded?.Invoke(this, new MidiClipEventArgs(clip, clip.EndPosition));
                }
            }
        }
        foreach (var id in clipIdsToRemove)
        {
            _activeMidiClipIds.Remove(id);
        }
    }

    /// <summary>
    /// Triggers a note from a MIDI clip.
    /// </summary>
    private void TriggerMidiClipNote(ISynth synth, NoteEvent note, MidiClip clip)
    {
        // Trigger note on
        synth.NoteOn(note.Note, note.Velocity);

        // Calculate duration in milliseconds
        var durationMs = note.Duration * (60000.0 / _bpm);

        // Schedule note off asynchronously
        _ = ScheduleMidiClipNoteOffAsync(synth, note.Note, (int)durationMs, clip);
    }

    /// <summary>
    /// Schedules a note-off for a MIDI clip note.
    /// </summary>
    private async Task ScheduleMidiClipNoteOffAsync(ISynth synth, int note, int durationMs, MidiClip clip)
    {
        try
        {
            await Task.Delay(durationMs).ConfigureAwait(false);
            synth.NoteOff(note);
        }
        catch (TaskCanceledException)
        {
            // Task was cancelled during shutdown - expected behavior
        }
    }

    /// <summary>
    /// Handles loop region from arrangement.
    /// </summary>
    /// <param name="currentBeat">Current beat position.</param>
    /// <returns>The adjusted beat position after loop handling.</returns>
    private double HandleArrangementLoop(double currentBeat)
    {
        if (!_loopingEnabled || _arrangement == null) return currentBeat;

        var loopRegion = _arrangement.GetLoopRegion();
        if (loopRegion == null || !loopRegion.IsActive) return currentBeat;

        if (currentBeat >= loopRegion.EndPosition)
        {
            // Loop back to start
            var loopLength = loopRegion.Length;
            var overshoot = currentBeat - loopRegion.EndPosition;
            var newPosition = loopRegion.StartPosition + (overshoot % loopLength);

            _logger?.LogDebug("Loop triggered: {OldBeat} -> {NewBeat}", currentBeat, newPosition);

            // Stop clips that are outside the loop region on loop back
            StopClipsOutsideRange(loopRegion.StartPosition, loopRegion.EndPosition);

            return newPosition;
        }

        return currentBeat;
    }

    /// <summary>
    /// Stops clips that are outside the specified range.
    /// </summary>
    private void StopClipsOutsideRange(double startPosition, double endPosition)
    {
        lock (_arrangementLock)
        {
            if (_arrangement == null) return;

            // Stop audio clips outside range
            var audioClipsToStop = _activeAudioClipIds
                .Select(id => _arrangement.GetAudioClip(id))
                .Where(clip => clip != null && (clip.EndPosition <= startPosition || clip.StartPosition >= endPosition))
                .ToList();

            foreach (var clip in audioClipsToStop)
            {
                if (clip != null)
                {
                    _activeAudioClipIds.Remove(clip.Id);
                    AudioClipEnded?.Invoke(this, new AudioClipEventArgs(clip, _beatAccumulator));
                }
            }

            // Stop MIDI clips outside range
            var midiClipsToStop = _activeMidiClipIds
                .Select(id => _arrangement.GetMidiClip(id))
                .Where(clip => clip != null && (clip.EndPosition <= startPosition || clip.StartPosition >= endPosition))
                .ToList();

            foreach (var clip in midiClipsToStop)
            {
                if (clip != null)
                {
                    _activeMidiClipIds.Remove(clip.Id);
                    var synth = _midiClipSynths.GetValueOrDefault(clip.Id) ?? clip.Pattern?.Synth;
                    synth?.AllNotesOff();
                    MidiClipEnded?.Invoke(this, new MidiClipEventArgs(clip, _beatAccumulator));
                }
            }
        }
    }

    #endregion

    // Start the sequencer
    public void Start()
    {
        if (_running) return;

        // Check if we need to do count-in
        if (_enableMetronome && _metronome != null && _metronome.CountIn > 0)
        {
            StartWithCountIn();
            return;
        }

        StartInternal();
    }

    /// <summary>
    /// Starts playback with count-in. The metronome will play count-in bars before pattern playback begins.
    /// </summary>
    private void StartWithCountIn()
    {
        if (_metronome == null || _metronome.CountIn <= 0) return;

        _isWaitingForCountIn = true;

        // Subscribe to count-in complete event
        _metronome.CountInComplete += OnCountInComplete;

        // Start the count-in
        _metronome.StartCountIn();

        // Start count-in playback loop (metronome only, no pattern processing)
        _running = true;

        if (_timingPrecision == TimingPrecision.Standard)
        {
            _thread = new Thread(RunCountIn) { IsBackground = true, Priority = ThreadPriority.Highest };
            _thread.Start();
        }
        else
        {
            _thread = new Thread(RunCountInHighPrecision) { IsBackground = true, Priority = ThreadPriority.Highest };
            _thread.Start();
        }

        _logger?.LogInformation("Sequencer count-in started: {CountInBars} bars at {Bpm} BPM",
            _metronome.CountIn, _bpm);
    }

    /// <summary>
    /// Handles count-in completion to start actual playback.
    /// </summary>
    private void OnCountInComplete(object? sender, EventArgs e)
    {
        if (_metronome != null)
        {
            _metronome.CountInComplete -= OnCountInComplete;
        }

        _isWaitingForCountIn = false;

        // The count-in loop will exit and StartInternal will be called
        _logger?.LogInformation("Count-in complete, starting playback");
    }

    /// <summary>
    /// Internal start method that initializes the sequencer.
    /// </summary>
    private void StartInternal()
    {
        _running = true;
        _isWaitingForCountIn = false;

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
        _isWaitingForCountIn = false;

        // Stop high-resolution timer
        lock (_timerLock)
        {
            _highResTimer?.Stop();
        }

        _thread?.Join();

        // Stop MIDI clock
        _midiClockSync?.Stop();

        // Reset metronome count-in state
        _metronome?.Reset();

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

    /// <summary>
    /// Count-in playback loop for standard timing mode.
    /// Only processes metronome clicks, no pattern playback.
    /// </summary>
    private void RunCountIn()
    {
        var stopwatch = Stopwatch.StartNew();
        double lastTime = stopwatch.Elapsed.TotalSeconds;

        while (_running && _isWaitingForCountIn)
        {
            double currentTime = stopwatch.Elapsed.TotalSeconds;
            double deltaTime = currentTime - lastTime;
            lastTime = currentTime;

            // Calculate beat delta
            double secondsPerBeat = 60.0 / _bpm;
            double beatsInDelta = deltaTime / secondsPerBeat;

            // Process count-in
            if (_metronome != null)
            {
                bool countInComplete = _metronome.ProcessCountIn(beatsInDelta);
                if (countInComplete)
                {
                    break;
                }
            }

            Thread.Sleep(Settings.MidiRefreshRateMs);
        }

        // If still running after count-in, start actual playback
        if (_running && !_isWaitingForCountIn)
        {
            // Reset beat accumulator to start of pattern
            _beatAccumulator = 0;

            // Continue with standard playback loop
            RunStandardInternal(Stopwatch.StartNew(), 0);
        }
    }

    /// <summary>
    /// Count-in playback loop for high-precision timing mode.
    /// </summary>
    private void RunCountInHighPrecision()
    {
        var stopwatch = Stopwatch.StartNew();
        double lastTime = stopwatch.Elapsed.TotalSeconds;

        while (_running && _isWaitingForCountIn)
        {
            double currentTime = stopwatch.Elapsed.TotalSeconds;
            double deltaTime = currentTime - lastTime;
            lastTime = currentTime;

            // Calculate beat delta
            double secondsPerBeat = 60.0 / _bpm;
            double beatsInDelta = deltaTime / secondsPerBeat;

            // Process count-in
            if (_metronome != null)
            {
                bool countInComplete = _metronome.ProcessCountIn(beatsInDelta);
                if (countInComplete)
                {
                    break;
                }
            }

            Thread.Yield();
        }

        // If still running after count-in, start actual playback
        if (_running && !_isWaitingForCountIn)
        {
            // Reset beat accumulator to start of pattern
            _beatAccumulator = 0;

            // Fire playback started event now that count-in is complete
            PlaybackStarted?.Invoke(this, new PlaybackStateEventArgs(true, _beatAccumulator, _bpm));

            // Initialize high-res timer and continue with high-precision playback
            InitializeHighResTimer();
            _highResTimer!.Start();

            // Continue with high-precision playback loop using local variables
            double lastProcessedBeat = 0;
            double lastBeatEventTime = 0;
            double beatEventInterval = 1.0 / 120.0;
            double expectedTickInterval = _highResTimer.TickIntervalMicroseconds / 1_000_000.0;
            double lastTickTime = 0;
            var playbackStopwatch = Stopwatch.StartNew();

            while (_running)
            {
                double currentTime = playbackStopwatch.Elapsed.TotalSeconds;

                // Calculate timing jitter
                if (lastTickTime > 0)
                {
                    double actualInterval = currentTime - lastTickTime;
                    double jitter = (actualInterval - expectedTickInterval) * 1000.0;

                    _timingJitterBuffer.Push(jitter);
                    _averageTimingJitter = _timingJitterBuffer.Average();

                    if (_jitterCompensationEnabled)
                    {
                        _jitterCompensationMs = _jitterCompensationMs * 0.95 + _averageTimingJitter * 0.05;
                    }
                }
                lastTickTime = currentTime;

                double nextBeat;
                lock (_patterns)
                {
                    if (!_isScratching)
                    {
                        double secondsPerBeat = 60.0 / _bpm;
                        double deltaTime = currentTime - _lastActualTime;
                        if (_lastActualTime == 0) deltaTime = 0;
                        _lastActualTime = currentTime;

                        double beatsInDelta = deltaTime / secondsPerBeat;

                        if (_jitterCompensationEnabled && Math.Abs(_jitterCompensationMs) > 0.001)
                        {
                            double compensationBeats = (_jitterCompensationMs / 1000.0) / secondsPerBeat;
                            beatsInDelta -= compensationBeats * 0.05;
                        }

                        nextBeat = _beatAccumulator + beatsInDelta;
                    }
                    else
                    {
                        nextBeat = _beatAccumulator;
                    }

                    if (nextBeat != lastProcessedBeat)
                    {
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

                if (currentTime - lastBeatEventTime >= beatEventInterval)
                {
                    lastBeatEventTime = currentTime;
                    EmitBeatChanged();
                }

                Thread.Yield();
            }

            lock (_timerLock)
            {
                _highResTimer?.Stop();
                _highResTimer?.Dispose();
                _highResTimer = null;
            }
        }
    }

    /// <summary>
    /// Initializes the high-resolution timer.
    /// </summary>
    private void InitializeHighResTimer()
    {
        lock (_timerLock)
        {
            _highResTimer = new HighResolutionTimer();
            _highResTimer.JitterCompensationEnabled = _jitterCompensationEnabled;

            if (_timingPrecision == TimingPrecision.AudioRate)
            {
                _highResTimer.TargetTicksPerSecond = _sampleRate / 128.0;
                _highResTimer.UseSpinWait = true;
            }
            else
            {
                double ticksPerSecond = (_bpm / 60.0) * (int)_beatSubdivision * 4;
                _highResTimer.TargetTicksPerSecond = Math.Min(10000, ticksPerSecond);
            }
        }
    }

    // Standard playback loop (backward compatible)
    private void RunStandard()
    {
        var stopwatch = Stopwatch.StartNew(); // High-resolution timer
        double lastTime = stopwatch.Elapsed.TotalSeconds; // Last time checkpoint
        double lastProcessedBeat = _beatAccumulator; // Last processed beat position
        double lastBeatEventTime = 0; // For throttling beat events
        const double beatEventInterval = 1.0 / 60.0; // ~60fps for beat events

        RunStandardInternal(stopwatch, lastProcessedBeat);
    }

    /// <summary>
    /// Internal standard playback loop implementation.
    /// </summary>
    private void RunStandardInternal(Stopwatch stopwatch, double lastProcessedBeat)
    {
        double lastTime = stopwatch.Elapsed.TotalSeconds;
        double lastBeatEventTime = 0;
        const double beatEventInterval = 1.0 / 60.0;

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

                // Handle arrangement loop region
                nextBeat = HandleArrangementLoop(nextBeat);

                if (nextBeat != lastProcessedBeat) // Process patterns if beat has changed
                {
                    // Process patterns
                    foreach (var pattern in _patterns) // Process each pattern
                    {
                        pattern.Process(lastProcessedBeat, nextBeat, _bpm); // Process pattern for the beat range
                    }

                    // Process arrangement clips (AudioClips and MidiClips)
                    ProcessArrangementClips(lastProcessedBeat, nextBeat);

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
        var stopwatch = Stopwatch.StartNew();
        double lastProcessedBeat = _beatAccumulator;

        RunHighPrecisionInternal(stopwatch, lastProcessedBeat);
    }

    /// <summary>
    /// Internal high-precision playback loop implementation.
    /// </summary>
    private void RunHighPrecisionInternal(Stopwatch stopwatch, double lastProcessedBeat)
    {
        // Create and configure high-resolution timer
        InitializeHighResTimer();

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

                // Handle arrangement loop region
                nextBeat = HandleArrangementLoop(nextBeat);

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

                    // Process arrangement clips (AudioClips and MidiClips)
                    ProcessArrangementClips(lastProcessedBeat, nextBeat);

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

    #region Tempo and Time Signature Methods

    /// <summary>
    /// Adds a tempo change at the specified beat position.
    /// </summary>
    /// <param name="positionBeats">The position in beats (quarter notes).</param>
    /// <param name="bpm">The tempo in BPM.</param>
    public void AddTempoChange(double positionBeats, double bpm)
    {
        EnsureTempoTrack();
        _tempoTrack!.AddTempoChange(positionBeats, bpm);
        _logger?.LogDebug("Tempo change added: {Bpm} BPM at beat {Position}", bpm, positionBeats);
    }

    /// <summary>
    /// Adds a tempo ramp between two positions.
    /// </summary>
    /// <param name="startBeats">The start position in beats.</param>
    /// <param name="endBeats">The end position in beats.</param>
    /// <param name="startBpm">The starting tempo.</param>
    /// <param name="endBpm">The ending tempo.</param>
    /// <param name="curve">The curve type (0 = linear).</param>
    public void AddTempoRamp(double startBeats, double endBeats, double startBpm, double endBpm, double curve = 0.0)
    {
        EnsureTempoTrack();
        _tempoTrack!.AddTempoRamp(startBeats, endBeats, startBpm, endBpm, curve);
        _logger?.LogDebug("Tempo ramp added: {StartBpm}->{EndBpm} BPM from beat {Start} to {End}",
            startBpm, endBpm, startBeats, endBeats);
    }

    /// <summary>
    /// Adds a time signature change at the specified bar.
    /// </summary>
    /// <param name="bar">The bar number (0-indexed).</param>
    /// <param name="timeSignature">The time signature.</param>
    public void AddTimeSignatureChange(int bar, TimeSignature timeSignature)
    {
        EnsureTempoTrack();
        var oldTimeSignature = _tempoTrack!.GetTimeSignatureAt(bar);
        _tempoTrack.AddTimeSignatureChange(bar, timeSignature);
        _logger?.LogDebug("Time signature change added: {TimeSignature} at bar {Bar}", timeSignature, bar);
        TimeSignatureChanged?.Invoke(this, new TimeSignatureChangedEventArgs(bar, oldTimeSignature, timeSignature));
    }

    /// <summary>
    /// Adds a time signature change at the specified bar.
    /// </summary>
    /// <param name="bar">The bar number (0-indexed).</param>
    /// <param name="numerator">The time signature numerator.</param>
    /// <param name="denominator">The time signature denominator.</param>
    public void AddTimeSignatureChange(int bar, int numerator, int denominator)
    {
        AddTimeSignatureChange(bar, new TimeSignature(numerator, denominator));
    }

    /// <summary>
    /// Gets the tempo at the specified position.
    /// </summary>
    /// <param name="positionBeats">The position in beats.</param>
    /// <returns>The tempo in BPM.</returns>
    public double GetTempoAt(double positionBeats)
    {
        return _tempoTrack?.GetTempoAt(positionBeats) ?? _bpm;
    }

    /// <summary>
    /// Gets the time signature at the specified bar.
    /// </summary>
    /// <param name="bar">The bar number (0-indexed).</param>
    /// <returns>The time signature.</returns>
    public TimeSignature GetTimeSignatureAt(int bar)
    {
        return _tempoTrack?.GetTimeSignatureAt(bar) ?? TimeSignature.Common;
    }

    /// <summary>
    /// Converts a position in beats to seconds.
    /// </summary>
    /// <param name="beats">The position in beats.</param>
    /// <returns>The time in seconds.</returns>
    public double BeatsToSeconds(double beats)
    {
        return _tempoTrack?.BeatsToSeconds(beats) ?? (beats * 60.0 / _bpm);
    }

    /// <summary>
    /// Converts a time in seconds to beats.
    /// </summary>
    /// <param name="seconds">The time in seconds.</param>
    /// <returns>The position in beats.</returns>
    public double SecondsToBeats(double seconds)
    {
        return _tempoTrack?.SecondsToBeats(seconds) ?? (seconds * _bpm / 60.0);
    }

    /// <summary>
    /// Converts a position in beats to samples.
    /// </summary>
    /// <param name="beats">The position in beats.</param>
    /// <returns>The position in samples.</returns>
    public long BeatsToSamples(double beats)
    {
        return _tempoTrack?.BeatsToSamples(beats) ?? (long)(BeatsToSeconds(beats) * _sampleRate);
    }

    /// <summary>
    /// Converts a position in samples to beats.
    /// </summary>
    /// <param name="samples">The position in samples.</param>
    /// <returns>The position in beats.</returns>
    public double SamplesToBeats(long samples)
    {
        return _tempoTrack?.SamplesToBeats(samples) ?? SecondsToBeats((double)samples / _sampleRate);
    }

    /// <summary>
    /// Converts a bar and beat position to beats (quarter notes).
    /// </summary>
    /// <param name="bar">The bar number (0-indexed).</param>
    /// <param name="beat">The beat within the bar.</param>
    /// <returns>The position in beats.</returns>
    public double BarBeatToBeats(int bar, double beat)
    {
        return _tempoTrack?.BarBeatToBeats(bar, beat) ?? (bar * 4.0 + beat);
    }

    /// <summary>
    /// Converts a position in beats to bar and beat.
    /// </summary>
    /// <param name="beats">The position in beats.</param>
    /// <returns>A tuple of (bar, beat).</returns>
    public (int Bar, double Beat) BeatsToBarBeat(double beats)
    {
        return _tempoTrack?.BeatsToBarBeat(beats) ?? ((int)(beats / 4.0), beats % 4.0);
    }

    /// <summary>
    /// Gets the full musical position information at the specified beat position.
    /// </summary>
    /// <param name="beats">The position in beats.</param>
    /// <returns>Complete position information.</returns>
    public MusicalPosition GetMusicalPosition(double beats)
    {
        if (_tempoTrack != null)
        {
            return _tempoTrack.GetPositionFromBeats(beats);
        }

        // Fallback without tempo track
        var (bar, beat) = BeatsToBarBeat(beats);
        double seconds = BeatsToSeconds(beats);
        long samples = (long)(seconds * _sampleRate);
        return new MusicalPosition(bar, beat, beats, seconds, samples, TimeSignature.Common, _bpm);
    }

    /// <summary>
    /// Gets the current musical position.
    /// </summary>
    /// <returns>Complete position information for the current position.</returns>
    public MusicalPosition GetCurrentMusicalPosition()
    {
        return GetMusicalPosition(_beatAccumulator);
    }

    /// <summary>
    /// Seeks to a specific bar and beat position.
    /// </summary>
    /// <param name="bar">The bar number (0-indexed).</param>
    /// <param name="beat">The beat within the bar.</param>
    public void SeekToBarBeat(int bar, double beat)
    {
        CurrentBeat = BarBeatToBeats(bar, beat);
    }

    /// <summary>
    /// Seeks to a specific time in seconds.
    /// </summary>
    /// <param name="seconds">The time in seconds.</param>
    public void SeekToSeconds(double seconds)
    {
        CurrentBeat = SecondsToBeats(seconds);
    }

    private void EnsureTempoTrack()
    {
        _tempoTrack ??= new TempoTrack(_bpm, TimeSignature.Common, _sampleRate);
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();

        // Detach metronome if attached
        if (_metronome != null && _metronome.IsAttachedToSequencer)
        {
            _metronome.DetachFromSequencer();
        }
        _metronome = null;

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

/// <summary>
/// Event arguments for time signature changes.
/// </summary>
public class TimeSignatureChangedEventArgs : EventArgs
{
    /// <summary>The bar where the time signature changed.</summary>
    public int Bar { get; }

    /// <summary>The previous time signature.</summary>
    public TimeSignature OldTimeSignature { get; }

    /// <summary>The new time signature.</summary>
    public TimeSignature NewTimeSignature { get; }

    public TimeSignatureChangedEventArgs(int bar, TimeSignature oldTimeSignature, TimeSignature newTimeSignature)
    {
        Bar = bar;
        OldTimeSignature = oldTimeSignature;
        NewTimeSignature = newTimeSignature;
    }
}

/// <summary>
/// Event arguments for audio clip events (start/end).
/// </summary>
public class AudioClipEventArgs : EventArgs
{
    /// <summary>The audio clip associated with this event.</summary>
    public AudioClip Clip { get; }

    /// <summary>The beat position when the event occurred.</summary>
    public double BeatPosition { get; }

    /// <summary>The time when the event occurred.</summary>
    public DateTime Timestamp { get; }

    public AudioClipEventArgs(AudioClip clip, double beatPosition)
    {
        Clip = clip ?? throw new ArgumentNullException(nameof(clip));
        BeatPosition = beatPosition;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Event arguments for MIDI clip events (start/end).
/// </summary>
public class MidiClipEventArgs : EventArgs
{
    /// <summary>The MIDI clip associated with this event.</summary>
    public MidiClip Clip { get; }

    /// <summary>The beat position when the event occurred.</summary>
    public double BeatPosition { get; }

    /// <summary>The time when the event occurred.</summary>
    public DateTime Timestamp { get; }

    public MidiClipEventArgs(MidiClip clip, double beatPosition)
    {
        Clip = clip ?? throw new ArgumentNullException(nameof(clip));
        BeatPosition = beatPosition;
        Timestamp = DateTime.UtcNow;
    }
}
