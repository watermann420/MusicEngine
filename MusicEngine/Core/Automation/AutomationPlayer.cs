// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

namespace MusicEngine.Core.Automation;

/// <summary>
/// Playback state for the automation player.
/// </summary>
public enum AutomationPlaybackState
{
    /// <summary>
    /// Playback is stopped.
    /// </summary>
    Stopped,

    /// <summary>
    /// Playback is active.
    /// </summary>
    Playing,

    /// <summary>
    /// Playback is paused.
    /// </summary>
    Paused,

    /// <summary>
    /// Recording is active.
    /// </summary>
    Recording
}

/// <summary>
/// Plays automation tracks and lanes synchronized with the Sequencer.
/// Handles playback, recording, and synchronization of automation data.
/// </summary>
public class AutomationPlayer : IDisposable
{
    private readonly object _lock = new();
    private readonly List<AutomationTrack> _tracks = [];
    private readonly List<AutomationLane> _standaloneLanes = [];
    private readonly Dictionary<string, IAutomatable> _automatableTargets = new(StringComparer.OrdinalIgnoreCase);

    private Sequencer? _sequencer;
    private bool _syncWithSequencer = true;
    private AutomationPlaybackState _state = AutomationPlaybackState.Stopped;
    private double _currentTime;
    private double _bpm = 120.0;
    private bool _disposed;

    // Recording state
    private readonly Dictionary<AutomationLane, AutomationRecordingSession> _recordingSessions = [];
    private double _recordingStartTime;
    private float _recordingValueThreshold = 0.001f;
    private double _recordingMinTimeBetweenPoints = 0.01;

    /// <summary>
    /// Gets the current playback state.
    /// </summary>
    public AutomationPlaybackState State
    {
        get
        {
            lock (_lock)
            {
                return _state;
            }
        }
    }

    /// <summary>
    /// Gets whether playback is active.
    /// </summary>
    public bool IsPlaying => State == AutomationPlaybackState.Playing;

    /// <summary>
    /// Gets whether recording is active.
    /// </summary>
    public bool IsRecording => State == AutomationPlaybackState.Recording;

    /// <summary>
    /// Gets the current playback time.
    /// </summary>
    public double CurrentTime
    {
        get
        {
            lock (_lock)
            {
                return _currentTime;
            }
        }
    }

    /// <summary>
    /// Gets or sets whether to synchronize with the Sequencer.
    /// </summary>
    public bool SyncWithSequencer
    {
        get
        {
            lock (_lock)
            {
                return _syncWithSequencer;
            }
        }
        set
        {
            lock (_lock)
            {
                _syncWithSequencer = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets the BPM for time conversion.
    /// </summary>
    public double Bpm
    {
        get
        {
            lock (_lock)
            {
                return _bpm;
            }
        }
        set
        {
            lock (_lock)
            {
                _bpm = Math.Max(1.0, value);
            }
        }
    }

    /// <summary>
    /// Gets the number of tracks.
    /// </summary>
    public int TrackCount
    {
        get
        {
            lock (_lock)
            {
                return _tracks.Count;
            }
        }
    }

    /// <summary>
    /// Gets all tracks.
    /// </summary>
    public IReadOnlyList<AutomationTrack> Tracks
    {
        get
        {
            lock (_lock)
            {
                return _tracks.ToList().AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Gets all standalone lanes (not in tracks).
    /// </summary>
    public IReadOnlyList<AutomationLane> StandaloneLanes
    {
        get
        {
            lock (_lock)
            {
                return _standaloneLanes.ToList().AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Gets or sets the minimum value change threshold for recording.
    /// </summary>
    public float RecordingValueThreshold
    {
        get => _recordingValueThreshold;
        set => _recordingValueThreshold = Math.Max(0f, value);
    }

    /// <summary>
    /// Gets or sets the minimum time between recorded points.
    /// </summary>
    public double RecordingMinTimeBetweenPoints
    {
        get => _recordingMinTimeBetweenPoints;
        set => _recordingMinTimeBetweenPoints = Math.Max(0.001, value);
    }

    /// <summary>
    /// Fired when playback state changes.
    /// </summary>
    public event EventHandler<AutomationPlaybackStateEventArgs>? StateChanged;

    /// <summary>
    /// Fired when automation values are applied.
    /// </summary>
    public event EventHandler<AutomationProcessedEventArgs>? AutomationProcessed;

    /// <summary>
    /// Fired when a track is added.
    /// </summary>
    public event EventHandler<AutomationTrack>? TrackAdded;

    /// <summary>
    /// Fired when a track is removed.
    /// </summary>
    public event EventHandler<AutomationTrack>? TrackRemoved;

    /// <summary>
    /// Creates a new automation player.
    /// </summary>
    public AutomationPlayer()
    {
    }

    /// <summary>
    /// Creates a new automation player synchronized with a Sequencer.
    /// </summary>
    /// <param name="sequencer">The Sequencer to synchronize with.</param>
    public AutomationPlayer(Sequencer sequencer)
    {
        SetSequencer(sequencer);
    }

    /// <summary>
    /// Sets the Sequencer for synchronization.
    /// </summary>
    /// <param name="sequencer">The Sequencer to synchronize with.</param>
    public void SetSequencer(Sequencer? sequencer)
    {
        lock (_lock)
        {
            // Unsubscribe from old sequencer
            if (_sequencer != null)
            {
                _sequencer.BeatChanged -= OnBeatChanged;
                _sequencer.PlaybackStarted -= OnPlaybackStarted;
                _sequencer.PlaybackStopped -= OnPlaybackStopped;
                _sequencer.BpmChanged -= OnBpmChanged;
            }

            _sequencer = sequencer;

            // Subscribe to new sequencer
            if (_sequencer != null)
            {
                _sequencer.BeatChanged += OnBeatChanged;
                _sequencer.PlaybackStarted += OnPlaybackStarted;
                _sequencer.PlaybackStopped += OnPlaybackStopped;
                _sequencer.BpmChanged += OnBpmChanged;
                _bpm = _sequencer.Bpm;
            }
        }
    }

    /// <summary>
    /// Registers an automatable target for easy lookup.
    /// </summary>
    /// <param name="target">The automatable target.</param>
    public void RegisterTarget(IAutomatable target)
    {
        lock (_lock)
        {
            _automatableTargets[target.AutomationId] = target;
        }
    }

    /// <summary>
    /// Unregisters an automatable target.
    /// </summary>
    /// <param name="target">The automatable target.</param>
    public void UnregisterTarget(IAutomatable target)
    {
        lock (_lock)
        {
            _automatableTargets.Remove(target.AutomationId);
        }
    }

    /// <summary>
    /// Gets a registered automatable target by ID.
    /// </summary>
    /// <param name="targetId">The target ID.</param>
    /// <returns>The target, or null if not found.</returns>
    public IAutomatable? GetTarget(string targetId)
    {
        lock (_lock)
        {
            return _automatableTargets.GetValueOrDefault(targetId);
        }
    }

    /// <summary>
    /// Adds a track to the player.
    /// </summary>
    /// <param name="track">The track to add.</param>
    public void AddTrack(AutomationTrack track)
    {
        lock (_lock)
        {
            if (!_tracks.Contains(track))
            {
                _tracks.Add(track);

                // Try to bind to registered target
                if (track.TargetObject == null && !string.IsNullOrEmpty(track.TargetId))
                {
                    if (_automatableTargets.TryGetValue(track.TargetId, out var target))
                    {
                        track.SetTarget(target);
                    }
                }
            }
        }
        TrackAdded?.Invoke(this, track);
    }

    /// <summary>
    /// Creates and adds a new track for the specified target.
    /// </summary>
    /// <param name="target">The automatable target.</param>
    /// <returns>The created track.</returns>
    public AutomationTrack AddTrack(IAutomatable target)
    {
        RegisterTarget(target);
        var track = new AutomationTrack(target);
        AddTrack(track);
        return track;
    }

    /// <summary>
    /// Removes a track from the player.
    /// </summary>
    /// <param name="track">The track to remove.</param>
    /// <returns>True if removed, false otherwise.</returns>
    public bool RemoveTrack(AutomationTrack track)
    {
        bool removed;
        lock (_lock)
        {
            removed = _tracks.Remove(track);
        }
        if (removed)
        {
            TrackRemoved?.Invoke(this, track);
        }
        return removed;
    }

    /// <summary>
    /// Gets a track by target ID.
    /// </summary>
    /// <param name="targetId">The target ID.</param>
    /// <returns>The track, or null if not found.</returns>
    public AutomationTrack? GetTrack(string targetId)
    {
        lock (_lock)
        {
            return _tracks.FirstOrDefault(t =>
                t.TargetId.Equals(targetId, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Gets a track by index.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns>The track, or null if out of range.</returns>
    public AutomationTrack? GetTrack(int index)
    {
        lock (_lock)
        {
            if (index >= 0 && index < _tracks.Count)
            {
                return _tracks[index];
            }
            return null;
        }
    }

    /// <summary>
    /// Gets or creates a track for the specified target.
    /// </summary>
    /// <param name="target">The automatable target.</param>
    /// <returns>The existing or newly created track.</returns>
    public AutomationTrack GetOrCreateTrack(IAutomatable target)
    {
        lock (_lock)
        {
            var existing = _tracks.FirstOrDefault(t =>
                t.TargetId.Equals(target.AutomationId, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
                return existing;
        }
        return AddTrack(target);
    }

    /// <summary>
    /// Adds a standalone lane (not in a track).
    /// </summary>
    /// <param name="lane">The lane to add.</param>
    public void AddLane(AutomationLane lane)
    {
        lock (_lock)
        {
            if (!_standaloneLanes.Contains(lane))
            {
                _standaloneLanes.Add(lane);
            }
        }
    }

    /// <summary>
    /// Removes a standalone lane.
    /// </summary>
    /// <param name="lane">The lane to remove.</param>
    /// <returns>True if removed, false otherwise.</returns>
    public bool RemoveLane(AutomationLane lane)
    {
        lock (_lock)
        {
            return _standaloneLanes.Remove(lane);
        }
    }

    /// <summary>
    /// Clears all tracks and lanes.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _tracks.Clear();
            _standaloneLanes.Clear();
            _recordingSessions.Clear();
        }
    }

    /// <summary>
    /// Starts playback.
    /// </summary>
    public void Play()
    {
        lock (_lock)
        {
            if (_state != AutomationPlaybackState.Playing)
            {
                _state = AutomationPlaybackState.Playing;
            }
        }
        OnStateChanged();
    }

    /// <summary>
    /// Stops playback and resets position.
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            if (_state != AutomationPlaybackState.Stopped)
            {
                // Stop any active recordings
                StopRecordingInternal();
                _state = AutomationPlaybackState.Stopped;
                _currentTime = 0;
            }
        }
        OnStateChanged();
    }

    /// <summary>
    /// Pauses playback without resetting position.
    /// </summary>
    public void Pause()
    {
        lock (_lock)
        {
            if (_state == AutomationPlaybackState.Playing)
            {
                _state = AutomationPlaybackState.Paused;
            }
        }
        OnStateChanged();
    }

    /// <summary>
    /// Starts recording on armed lanes.
    /// </summary>
    public void StartRecording()
    {
        lock (_lock)
        {
            _recordingStartTime = _currentTime;
            _state = AutomationPlaybackState.Recording;

            // Create recording sessions for armed lanes
            foreach (var track in _tracks)
            {
                foreach (var lane in track.GetArmedLanes())
                {
                    StartRecordingLane(lane);
                }
            }

            foreach (var lane in _standaloneLanes.Where(l => l.IsArmed))
            {
                StartRecordingLane(lane);
            }
        }
        OnStateChanged();
    }

    /// <summary>
    /// Stops recording and commits recorded data.
    /// </summary>
    public void StopRecording()
    {
        lock (_lock)
        {
            StopRecordingInternal();
            _state = AutomationPlaybackState.Stopped;
        }
        OnStateChanged();
    }

    /// <summary>
    /// Processes automation at the specified time.
    /// </summary>
    /// <param name="time">The current time in beats.</param>
    public void Process(double time)
    {
        lock (_lock)
        {
            if (_state != AutomationPlaybackState.Playing && _state != AutomationPlaybackState.Recording)
                return;

            _currentTime = time;

            // Check for any soloed tracks
            bool hasSoloedTracks = _tracks.Any(t => t.IsSoloed);

            // Process all tracks
            foreach (var track in _tracks)
            {
                if (track.IsMuted)
                    continue;

                if (hasSoloedTracks && !track.IsSoloed)
                    continue;

                bool hasSoloedLanes = track.HasSoloedLanes();
                track.Apply(time, hasSoloedLanes);
            }

            // Process standalone lanes
            bool hasStandaloneSolos = _standaloneLanes.Any(l => l.IsSoloed);
            foreach (var lane in _standaloneLanes)
            {
                if (lane.IsMuted)
                    continue;

                if (hasStandaloneSolos && !lane.IsSoloed)
                    continue;

                lane.Apply(time);
            }

            // Record values if recording
            if (_state == AutomationPlaybackState.Recording)
            {
                RecordCurrentValues(time);
            }
        }

        AutomationProcessed?.Invoke(this, new AutomationProcessedEventArgs(time));
    }

    /// <summary>
    /// Seeks to the specified time.
    /// </summary>
    /// <param name="time">The time to seek to.</param>
    public void Seek(double time)
    {
        lock (_lock)
        {
            _currentTime = Math.Max(0, time);

            // Apply values at the new position if playing
            if (_state == AutomationPlaybackState.Playing || _state == AutomationPlaybackState.Paused)
            {
                Process(_currentTime);
            }
        }
    }

    /// <summary>
    /// Converts beats to seconds.
    /// </summary>
    /// <param name="beats">Time in beats.</param>
    /// <returns>Time in seconds.</returns>
    public double BeatsToSeconds(double beats)
    {
        return beats * (60.0 / Bpm);
    }

    /// <summary>
    /// Converts seconds to beats.
    /// </summary>
    /// <param name="seconds">Time in seconds.</param>
    /// <returns>Time in beats.</returns>
    public double SecondsToBeats(double seconds)
    {
        return seconds * (Bpm / 60.0);
    }

    /// <summary>
    /// Resets all lanes to their default values.
    /// </summary>
    public void ResetToDefaults()
    {
        lock (_lock)
        {
            foreach (var track in _tracks)
            {
                track.ResetToDefaults();
            }

            foreach (var lane in _standaloneLanes)
            {
                lane.ResetToDefault();
            }
        }
    }

    private void StartRecordingLane(AutomationLane lane)
    {
        if (!_recordingSessions.ContainsKey(lane))
        {
            _recordingSessions[lane] = new AutomationRecordingSession
            {
                Lane = lane,
                StartTime = _currentTime,
                LastRecordedTime = double.NegativeInfinity,
                LastRecordedValue = float.NaN
            };
        }
    }

    private void RecordCurrentValues(double time)
    {
        foreach (var kvp in _recordingSessions)
        {
            var lane = kvp.Key;
            var session = kvp.Value;

            float currentValue = lane.GetCurrentValue();

            bool timeOk = (time - session.LastRecordedTime) >= _recordingMinTimeBetweenPoints;
            bool valueChanged = float.IsNaN(session.LastRecordedValue) ||
                               Math.Abs(currentValue - session.LastRecordedValue) >= _recordingValueThreshold;

            if (timeOk && valueChanged)
            {
                lane.AddPoint(time, currentValue, AutomationCurveType.Linear);
                session.LastRecordedTime = time;
                session.LastRecordedValue = currentValue;
            }
        }
    }

    private void StopRecordingInternal()
    {
        _recordingSessions.Clear();
    }

    private void OnBeatChanged(object? sender, BeatChangedEventArgs e)
    {
        if (_syncWithSequencer && (_state == AutomationPlaybackState.Playing || _state == AutomationPlaybackState.Recording))
        {
            Process(e.CurrentBeat);
        }
    }

    private void OnPlaybackStarted(object? sender, PlaybackStateEventArgs e)
    {
        if (_syncWithSequencer)
        {
            Play();
        }
    }

    private void OnPlaybackStopped(object? sender, PlaybackStateEventArgs e)
    {
        if (_syncWithSequencer)
        {
            Stop();
        }
    }

    private void OnBpmChanged(object? sender, ParameterChangedEventArgs e)
    {
        Bpm = e.NewValue;
    }

    private void OnStateChanged()
    {
        StateChanged?.Invoke(this, new AutomationPlaybackStateEventArgs(_state, _currentTime));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        SetSequencer(null);
        Clear();

        GC.SuppressFinalize(this);
    }

    private class AutomationRecordingSession
    {
        public AutomationLane Lane { get; set; } = null!;
        public double StartTime { get; set; }
        public double LastRecordedTime { get; set; }
        public float LastRecordedValue { get; set; }
    }
}

/// <summary>
/// Event arguments for playback state changes.
/// </summary>
public class AutomationPlaybackStateEventArgs : EventArgs
{
    /// <summary>
    /// Gets the new playback state.
    /// </summary>
    public AutomationPlaybackState State { get; }

    /// <summary>
    /// Gets the current time position.
    /// </summary>
    public double CurrentTime { get; }

    /// <summary>
    /// Creates a new state changed event.
    /// </summary>
    /// <param name="state">The new state.</param>
    /// <param name="currentTime">The current time.</param>
    public AutomationPlaybackStateEventArgs(AutomationPlaybackState state, double currentTime)
    {
        State = state;
        CurrentTime = currentTime;
    }
}

/// <summary>
/// Event arguments for automation processed events.
/// </summary>
public class AutomationProcessedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the time at which automation was processed.
    /// </summary>
    public double Time { get; }

    /// <summary>
    /// Creates a new automation processed event.
    /// </summary>
    /// <param name="time">The processing time.</param>
    public AutomationProcessedEventArgs(double time)
    {
        Time = time;
    }
}
