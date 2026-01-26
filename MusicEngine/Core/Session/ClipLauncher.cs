//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Main clip launcher/session view controller for launching clips in a grid.

namespace MusicEngine.Core.Session;

/// <summary>
/// Event arguments for clip state changes.
/// </summary>
public class ClipStateChangedEventArgs : EventArgs
{
    /// <summary>The clip that changed state.</summary>
    public required LaunchClip Clip { get; init; }

    /// <summary>Previous state of the clip.</summary>
    public required ClipState OldState { get; init; }

    /// <summary>New state of the clip.</summary>
    public required ClipState NewState { get; init; }

    /// <summary>Track index of the clip.</summary>
    public int TrackIndex => Clip.TrackIndex;

    /// <summary>Scene index of the clip.</summary>
    public int SceneIndex => Clip.SceneIndex;
}

/// <summary>
/// Event arguments for track stop events.
/// </summary>
public class TrackStopEventArgs : EventArgs
{
    /// <summary>Index of the track that was stopped.</summary>
    public required int TrackIndex { get; init; }

    /// <summary>The clip that was stopped, if any.</summary>
    public LaunchClip? StoppedClip { get; init; }
}

/// <summary>
/// Main controller for the clip launcher / session view.
/// Manages a grid of clips organized by tracks (columns) and scenes (rows).
/// Similar to Ableton Live's Session View.
/// </summary>
public class ClipLauncher : IDisposable
{
    private readonly List<Scene> _scenes = new();
    private readonly List<List<ClipSlot>> _grid; // [track][scene]
    private readonly List<LaunchClip?> _playingClips; // Currently playing clip per track
    private readonly List<LaunchClip?> _queuedClips; // Queued clip per track
    private readonly object _lock = new();
    private readonly Random _random = new();
    private bool _disposed;
    private double _currentBeat;
    private double _lastQuantizeBeat;

    /// <summary>Number of tracks (columns) in the grid.</summary>
    public int TrackCount { get; }

    /// <summary>Number of scenes (rows) in the grid.</summary>
    public int SceneCount => _scenes.Count;

    /// <summary>Current tempo in BPM.</summary>
    public double Bpm { get; set; } = 120;

    /// <summary>Time signature numerator (beats per bar).</summary>
    public int TimeSignatureNumerator { get; set; } = 4;

    /// <summary>Time signature denominator.</summary>
    public int TimeSignatureDenominator { get; set; } = 4;

    /// <summary>Global quantization mode for clip launching.</summary>
    public QuantizeMode GlobalQuantize { get; set; } = QuantizeMode.Bar;

    /// <summary>Currently active scene (most recently launched).</summary>
    public Scene? CurrentScene { get; private set; }

    /// <summary>Whether the launcher is currently processing.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>Current playback position in beats.</summary>
    public double CurrentBeat => _currentBeat;

    /// <summary>All scenes in the launcher.</summary>
    public IReadOnlyList<Scene> Scenes => _scenes.AsReadOnly();

    /// <summary>Fired when a clip's state changes.</summary>
    public event EventHandler<ClipStateChangedEventArgs>? ClipStateChanged;

    /// <summary>Fired when a scene is launched.</summary>
    public event EventHandler<Scene>? SceneLaunched;

    /// <summary>Fired when a track is stopped.</summary>
    public event EventHandler<TrackStopEventArgs>? TrackStopped;

    /// <summary>Fired when all clips are stopped.</summary>
    public event EventHandler? AllStopped;

    /// <summary>
    /// Creates a new clip launcher with the specified number of tracks.
    /// </summary>
    /// <param name="trackCount">Number of tracks (columns).</param>
    public ClipLauncher(int trackCount)
    {
        if (trackCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(trackCount), "Track count must be positive.");

        TrackCount = trackCount;
        _grid = new List<List<ClipSlot>>(trackCount);
        _playingClips = new List<LaunchClip?>(trackCount);
        _queuedClips = new List<LaunchClip?>(trackCount);

        for (int i = 0; i < trackCount; i++)
        {
            _grid.Add(new List<ClipSlot>());
            _playingClips.Add(null);
            _queuedClips.Add(null);
        }
    }

    /// <summary>
    /// Adds a new scene to the launcher.
    /// </summary>
    /// <param name="name">Optional name for the scene.</param>
    /// <returns>The created scene.</returns>
    public Scene AddScene(string? name = null)
    {
        lock (_lock)
        {
            var scene = new Scene(_scenes.Count, name)
            {
                Launcher = this
            };

            // Create slots for each track
            for (int track = 0; track < TrackCount; track++)
            {
                var slot = new ClipSlot(track, scene.Index)
                {
                    Launcher = this
                };
                scene.Slots.Add(slot);
                _grid[track].Add(slot);
            }

            _scenes.Add(scene);
            return scene;
        }
    }

    /// <summary>
    /// Removes a scene from the launcher.
    /// </summary>
    /// <param name="scene">The scene to remove.</param>
    public void RemoveScene(Scene scene)
    {
        if (scene == null)
            throw new ArgumentNullException(nameof(scene));

        lock (_lock)
        {
            if (!_scenes.Contains(scene))
                return;

            // Stop any playing clips in this scene
            foreach (var slot in scene.Slots)
            {
                if (slot.IsPlaying || slot.IsQueued)
                {
                    StopClipInternal(slot.Clip!);
                }
                slot.Clear();
            }

            int removedIndex = scene.Index;
            _scenes.Remove(scene);

            // Remove slots from grid
            for (int track = 0; track < TrackCount; track++)
            {
                _grid[track].RemoveAt(removedIndex);
            }

            // Update indices for remaining scenes
            for (int i = removedIndex; i < _scenes.Count; i++)
            {
                _scenes[i].Index = i;
                foreach (var slot in _scenes[i].Slots)
                {
                    // Update slot scene index via reflection since it's read-only
                    // Actually, we need to recreate slots or make SceneIndex settable
                }
            }

            scene.Launcher = null;

            if (CurrentScene == scene)
            {
                CurrentScene = null;
            }
        }
    }

    /// <summary>
    /// Gets the clip slot at the specified position.
    /// </summary>
    /// <param name="track">Track index.</param>
    /// <param name="scene">Scene index.</param>
    /// <returns>The clip slot.</returns>
    public ClipSlot GetSlot(int track, int scene)
    {
        if (track < 0 || track >= TrackCount)
            throw new ArgumentOutOfRangeException(nameof(track));
        if (scene < 0 || scene >= SceneCount)
            throw new ArgumentOutOfRangeException(nameof(scene));

        lock (_lock)
        {
            return _grid[track][scene];
        }
    }

    /// <summary>
    /// Launches the clip at the specified position.
    /// </summary>
    /// <param name="track">Track index.</param>
    /// <param name="scene">Scene index.</param>
    public void LaunchClip(int track, int scene)
    {
        if (track < 0 || track >= TrackCount)
            throw new ArgumentOutOfRangeException(nameof(track));
        if (scene < 0 || scene >= SceneCount)
            throw new ArgumentOutOfRangeException(nameof(scene));

        lock (_lock)
        {
            var slot = _grid[track][scene];
            var clip = slot.Clip;
            if (clip == null) return;

            var quantize = clip.Quantize != QuantizeMode.None ? clip.Quantize : GlobalQuantize;

            if (quantize == QuantizeMode.None)
            {
                // Launch immediately
                LaunchClipImmediately(clip, track);
            }
            else
            {
                // Queue for next quantize point
                QueueClip(clip, track);
            }
        }
    }

    /// <summary>
    /// Launches all clips in a scene.
    /// </summary>
    /// <param name="sceneIndex">Scene index to launch.</param>
    public void LaunchScene(int sceneIndex)
    {
        if (sceneIndex < 0 || sceneIndex >= SceneCount)
            throw new ArgumentOutOfRangeException(nameof(sceneIndex));

        lock (_lock)
        {
            var scene = _scenes[sceneIndex];

            // Apply tempo override if set
            if (scene.TempoOverride.HasValue)
            {
                Bpm = scene.TempoOverride.Value;
            }

            // Apply time signature override if set
            if (scene.TimeSignatureNumerator.HasValue)
            {
                TimeSignatureNumerator = scene.TimeSignatureNumerator.Value;
            }
            if (scene.TimeSignatureDenominator.HasValue)
            {
                TimeSignatureDenominator = scene.TimeSignatureDenominator.Value;
            }

            // Launch or stop each track
            for (int track = 0; track < TrackCount; track++)
            {
                var slot = scene.Slots[track];
                if (slot.HasClip)
                {
                    LaunchClip(track, sceneIndex);
                }
                else if (slot.HasStopButton)
                {
                    StopTrack(track);
                }
            }

            CurrentScene = scene;
            SceneLaunched?.Invoke(this, scene);
        }
    }

    /// <summary>
    /// Stops all clips on the specified track.
    /// </summary>
    /// <param name="track">Track index.</param>
    public void StopTrack(int track)
    {
        if (track < 0 || track >= TrackCount)
            throw new ArgumentOutOfRangeException(nameof(track));

        lock (_lock)
        {
            var stoppedClip = _playingClips[track];

            // Clear queued clip
            if (_queuedClips[track] != null)
            {
                var queued = _queuedClips[track]!;
                var oldState = queued.State;
                queued.State = ClipState.Stopped;
                _queuedClips[track] = null;
                OnClipStateChanged(queued, oldState, ClipState.Stopped);
            }

            // Stop playing clip
            if (_playingClips[track] != null)
            {
                StopClipInternal(_playingClips[track]!);
                _playingClips[track] = null;
            }

            TrackStopped?.Invoke(this, new TrackStopEventArgs
            {
                TrackIndex = track,
                StoppedClip = stoppedClip
            });
        }
    }

    /// <summary>
    /// Stops all clips on all tracks.
    /// </summary>
    public void StopAll()
    {
        lock (_lock)
        {
            for (int track = 0; track < TrackCount; track++)
            {
                if (_queuedClips[track] != null)
                {
                    var queued = _queuedClips[track]!;
                    var oldState = queued.State;
                    queued.State = ClipState.Stopped;
                    _queuedClips[track] = null;
                    OnClipStateChanged(queued, oldState, ClipState.Stopped);
                }

                if (_playingClips[track] != null)
                {
                    StopClipInternal(_playingClips[track]!);
                    _playingClips[track] = null;
                }
            }

            CurrentScene = null;
            AllStopped?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Starts the launcher processing.
    /// </summary>
    public void Start()
    {
        IsRunning = true;
    }

    /// <summary>
    /// Stops the launcher processing.
    /// </summary>
    public void Stop()
    {
        IsRunning = false;
    }

    /// <summary>
    /// Resets the launcher to beat 0.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _currentBeat = 0;
            _lastQuantizeBeat = 0;

            foreach (var clip in _playingClips.Where(c => c != null))
            {
                clip!.Reset();
            }
        }
    }

    /// <summary>
    /// Processes clip playback for the given time delta.
    /// Should be called by the sequencer on each audio buffer.
    /// </summary>
    /// <param name="deltaBeats">Time elapsed in beats.</param>
    public void Process(double deltaBeats)
    {
        if (!IsRunning || deltaBeats <= 0) return;

        lock (_lock)
        {
            double previousBeat = _currentBeat;
            _currentBeat += deltaBeats;

            // Check for quantize boundaries and launch queued clips
            ProcessQuantizeBoundaries(previousBeat, _currentBeat);

            // Process playing clips
            for (int track = 0; track < TrackCount; track++)
            {
                var clip = _playingClips[track];
                if (clip == null || clip.State != ClipState.Playing) continue;

                // Advance play position
                clip.PlayPosition += deltaBeats;

                // Check for loop/end
                double effectiveLength = clip.EffectiveLength;
                if (clip.PlayPosition >= effectiveLength)
                {
                    if (clip.Loop)
                    {
                        clip.PlayPosition %= effectiveLength;
                        clip.FollowActionAccumulator += effectiveLength;
                    }
                    else
                    {
                        // Clip finished
                        StopClipInternal(clip);
                        _playingClips[track] = null;
                    }
                }

                // Check follow action
                if (clip.FollowAction && clip.FollowActionAccumulator >= clip.FollowTime)
                {
                    ProcessFollowAction(clip, track);
                }
            }
        }
    }

    /// <summary>
    /// Gets the currently playing clip on a track.
    /// </summary>
    /// <param name="track">Track index.</param>
    /// <returns>The playing clip, or null.</returns>
    public LaunchClip? GetPlayingClip(int track)
    {
        if (track < 0 || track >= TrackCount) return null;
        lock (_lock)
        {
            return _playingClips[track];
        }
    }

    /// <summary>
    /// Gets the queued clip on a track.
    /// </summary>
    /// <param name="track">Track index.</param>
    /// <returns>The queued clip, or null.</returns>
    public LaunchClip? GetQueuedClip(int track)
    {
        if (track < 0 || track >= TrackCount) return null;
        lock (_lock)
        {
            return _queuedClips[track];
        }
    }

    private void QueueClip(LaunchClip clip, int track)
    {
        // Cancel any previously queued clip
        if (_queuedClips[track] != null)
        {
            var prev = _queuedClips[track]!;
            var prevOldState = prev.State;
            prev.State = ClipState.Stopped;
            OnClipStateChanged(prev, prevOldState, ClipState.Stopped);
        }

        var oldState = clip.State;
        clip.State = ClipState.Queued;
        _queuedClips[track] = clip;
        OnClipStateChanged(clip, oldState, ClipState.Queued);
    }

    private void LaunchClipImmediately(LaunchClip clip, int track)
    {
        // Stop currently playing clip on this track
        if (_playingClips[track] != null)
        {
            StopClipInternal(_playingClips[track]!);
        }

        // Clear from queued if it was there
        if (_queuedClips[track] == clip)
        {
            _queuedClips[track] = null;
        }

        var oldState = clip.State;
        clip.State = ClipState.Playing;
        clip.Reset();
        _playingClips[track] = clip;
        OnClipStateChanged(clip, oldState, ClipState.Playing);
    }

    private void StopClipInternal(LaunchClip clip)
    {
        var oldState = clip.State;
        clip.State = ClipState.Stopped;
        clip.PlayPosition = 0;
        clip.FollowActionAccumulator = 0;
        OnClipStateChanged(clip, oldState, ClipState.Stopped);
    }

    private void ProcessQuantizeBoundaries(double fromBeat, double toBeat)
    {
        // Check each quantize mode
        foreach (QuantizeMode mode in Enum.GetValues(typeof(QuantizeMode)))
        {
            if (mode == QuantizeMode.None) continue;

            double quantizeInterval = GetQuantizeInterval(mode);
            double nextBoundary = Math.Ceiling(fromBeat / quantizeInterval) * quantizeInterval;

            if (nextBoundary > fromBeat && nextBoundary <= toBeat)
            {
                // We crossed a quantize boundary
                for (int track = 0; track < TrackCount; track++)
                {
                    var queued = _queuedClips[track];
                    if (queued == null) continue;

                    var clipQuantize = queued.Quantize != QuantizeMode.None ? queued.Quantize : GlobalQuantize;
                    if (clipQuantize == mode)
                    {
                        _queuedClips[track] = null;
                        LaunchClipImmediately(queued, track);
                    }
                }
            }
        }
    }

    private double GetQuantizeInterval(QuantizeMode mode)
    {
        return mode switch
        {
            QuantizeMode.Bar => TimeSignatureNumerator,
            QuantizeMode.Beat => 1.0,
            QuantizeMode.Eighth => 0.5,
            QuantizeMode.Sixteenth => 0.25,
            _ => 1.0
        };
    }

    private void ProcessFollowAction(LaunchClip clip, int track)
    {
        // Check probability
        if (clip.FollowActionChance < 1.0 && _random.NextDouble() > clip.FollowActionChance)
        {
            clip.FollowActionAccumulator = 0;
            return;
        }

        int targetScene = GetFollowActionTarget(clip, track);
        if (targetScene >= 0 && targetScene < SceneCount)
        {
            var targetSlot = _grid[track][targetScene];
            if (targetSlot.HasClip)
            {
                clip.FollowActionAccumulator = 0;
                LaunchClip(track, targetScene);
            }
        }
        else
        {
            clip.FollowActionAccumulator = 0;
        }
    }

    private int GetFollowActionTarget(LaunchClip clip, int track)
    {
        int currentScene = clip.SceneIndex;
        int trackClipCount = _grid[track].Count(s => s.HasClip);

        return clip.FollowActionType switch
        {
            FollowActionType.Next => (currentScene + 1) % SceneCount,
            FollowActionType.Previous => currentScene > 0 ? currentScene - 1 : SceneCount - 1,
            FollowActionType.First => FindFirstClipInTrack(track),
            FollowActionType.Last => FindLastClipInTrack(track),
            FollowActionType.Random => FindRandomClipInTrack(track, currentScene),
            FollowActionType.Other => clip.FollowActionOtherIndex,
            _ => -1
        };
    }

    private int FindFirstClipInTrack(int track)
    {
        for (int i = 0; i < SceneCount; i++)
        {
            if (_grid[track][i].HasClip) return i;
        }
        return -1;
    }

    private int FindLastClipInTrack(int track)
    {
        for (int i = SceneCount - 1; i >= 0; i--)
        {
            if (_grid[track][i].HasClip) return i;
        }
        return -1;
    }

    private int FindRandomClipInTrack(int track, int excludeScene)
    {
        var candidates = new List<int>();
        for (int i = 0; i < SceneCount; i++)
        {
            if (i != excludeScene && _grid[track][i].HasClip)
            {
                candidates.Add(i);
            }
        }
        if (candidates.Count == 0) return -1;
        return candidates[_random.Next(candidates.Count)];
    }

    internal void OnClipStateChanged(LaunchClip clip, ClipState oldState, ClipState newState)
    {
        ClipStateChanged?.Invoke(this, new ClipStateChangedEventArgs
        {
            Clip = clip,
            OldState = oldState,
            NewState = newState
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        StopAll();
        _scenes.Clear();
        _grid.Clear();
        _playingClips.Clear();
        _queuedClips.Clear();
    }
}
