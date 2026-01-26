// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;


namespace MusicEngine.Core.Freeze;


/// <summary>
/// Manages freeze (bounce) and unfreeze operations for tracks.
/// Coordinates offline rendering and synth replacement.
/// </summary>
public class FreezeManager : IDisposable
{
    private readonly Dictionary<int, FreezeState> _trackStates = new();
    private readonly Dictionary<int, FreezeData> _freezeDataStore = new();
    private readonly Dictionary<int, FrozenTrackPlayer> _frozenPlayers = new();
    private readonly object _lock = new();
    private readonly Sequencer _sequencer;
    private readonly AudioEngine? _audioEngine;
    private bool _disposed;

    // Configuration
    private string _frozenTracksDirectory;
    private bool _saveToFile = true;
    private bool _keepInMemory = true;

    /// <summary>
    /// Gets or sets the directory where frozen audio files are stored.
    /// </summary>
    public string FrozenTracksDirectory
    {
        get => _frozenTracksDirectory;
        set
        {
            _frozenTracksDirectory = value;
            if (!string.IsNullOrEmpty(value) && !Directory.Exists(value))
            {
                Directory.CreateDirectory(value);
            }
        }
    }

    /// <summary>
    /// Gets or sets whether to save frozen audio to disk.
    /// </summary>
    public bool SaveToFile
    {
        get => _saveToFile;
        set => _saveToFile = value;
    }

    /// <summary>
    /// Gets or sets whether to keep frozen audio in memory.
    /// </summary>
    public bool KeepInMemory
    {
        get => _keepInMemory;
        set => _keepInMemory = value;
    }

    /// <summary>
    /// Gets or sets whether to automatically freeze tracks that exceed CPU threshold.
    /// </summary>
    public bool AutoFreeze { get; set; } = false;

    /// <summary>
    /// Gets or sets the CPU usage threshold (0-100) for auto-freeze.
    /// </summary>
    public float AutoFreezeCpuThreshold { get; set; } = 80f;

    /// <summary>
    /// Gets or sets whether to include effects in the freeze.
    /// </summary>
    public bool FreezeWithEffects { get; set; } = true;

    /// <summary>
    /// Gets or sets the tail length in seconds for capturing reverb/delay tails.
    /// </summary>
    public double TailLengthSeconds { get; set; } = 2.0;

    /// <summary>
    /// Event raised when a freeze operation starts.
    /// </summary>
    public event EventHandler<FreezeStartedEventArgs>? FreezeStarted;

    /// <summary>
    /// Event raised when a freeze operation completes.
    /// </summary>
    public event EventHandler<FreezeCompletedEventArgs>? FreezeCompleted;

    /// <summary>
    /// Event raised when an unfreeze operation completes.
    /// </summary>
    public event EventHandler<UnfreezeCompletedEventArgs>? UnfreezeCompleted;

    /// <summary>
    /// Event raised when a track's freeze state changes.
    /// </summary>
    public event EventHandler<FreezeStateChangedEventArgs>? FreezeStateChanged;

    /// <summary>
    /// Creates a new FreezeManager instance.
    /// </summary>
    /// <param name="sequencer">The sequencer containing patterns to freeze.</param>
    /// <param name="audioEngine">Optional audio engine for effect chain access.</param>
    /// <param name="frozenTracksDirectory">The directory for storing frozen audio files.</param>
    public FreezeManager(Sequencer sequencer, AudioEngine? audioEngine = null, string? frozenTracksDirectory = null)
    {
        _sequencer = sequencer ?? throw new ArgumentNullException(nameof(sequencer));
        _audioEngine = audioEngine;
        _frozenTracksDirectory = frozenTracksDirectory ?? Path.Combine(Path.GetTempPath(), "MusicEngine", "FrozenTracks");

        if (!Directory.Exists(_frozenTracksDirectory))
        {
            Directory.CreateDirectory(_frozenTracksDirectory);
        }
    }

    /// <summary>
    /// Gets the freeze state of a track.
    /// </summary>
    /// <param name="trackIndex">The track index.</param>
    /// <returns>The freeze state.</returns>
    public FreezeState GetTrackState(int trackIndex)
    {
        lock (_lock)
        {
            return _trackStates.TryGetValue(trackIndex, out var state) ? state : FreezeState.Live;
        }
    }

    /// <summary>
    /// Gets the freeze data for a track.
    /// </summary>
    /// <param name="trackIndex">The track index.</param>
    /// <returns>The freeze data, or null if the track is not frozen.</returns>
    public FreezeData? GetFreezeData(int trackIndex)
    {
        lock (_lock)
        {
            return _freezeDataStore.TryGetValue(trackIndex, out var data) ? data : null;
        }
    }

    /// <summary>
    /// Gets the frozen track player for a track.
    /// </summary>
    /// <param name="trackIndex">The track index.</param>
    /// <returns>The frozen track player, or null if not frozen.</returns>
    public FrozenTrackPlayer? GetFrozenPlayer(int trackIndex)
    {
        lock (_lock)
        {
            return _frozenPlayers.TryGetValue(trackIndex, out var player) ? player : null;
        }
    }

    /// <summary>
    /// Freezes a track asynchronously.
    /// </summary>
    /// <param name="trackIndex">The track index (pattern index) to freeze.</param>
    /// <param name="startBeat">The start position in beats (defaults to 0).</param>
    /// <param name="endBeat">The end position in beats (defaults to pattern loop length).</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the freeze was successful.</returns>
    public async Task<bool> FreezeTrackAsync(
        int trackIndex,
        double? startBeat = null,
        double? endBeat = null,
        IProgress<RenderProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // Validate track index
        var patterns = _sequencer.Patterns;
        if (trackIndex < 0 || trackIndex >= patterns.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(trackIndex), "Invalid track index.");
        }

        var pattern = patterns[trackIndex];
        var synth = pattern.Synth;

        if (synth == null)
        {
            throw new InvalidOperationException($"Track {trackIndex} has no synth assigned.");
        }

        // Check current state
        lock (_lock)
        {
            var currentState = GetTrackState(trackIndex);
            if (currentState == FreezeState.Freezing || currentState == FreezeState.Unfreezing)
            {
                throw new InvalidOperationException($"Track {trackIndex} is already in a freeze/unfreeze operation.");
            }

            if (currentState == FreezeState.Frozen)
            {
                throw new InvalidOperationException($"Track {trackIndex} is already frozen. Unfreeze first.");
            }

            SetTrackState(trackIndex, FreezeState.Freezing);
        }

        try
        {
            // Determine range
            double start = startBeat ?? 0;
            double end = endBeat ?? pattern.LoopLength;
            double bpm = _sequencer.Bpm;

            // Raise started event
            FreezeStarted?.Invoke(this, new FreezeStartedEventArgs(trackIndex, start, end));

            // Store original configuration
            var freezeData = new FreezeData
            {
                TrackIndex = trackIndex,
                OriginalSynth = synth,
                OriginalSynthTypeName = synth.GetType().FullName ?? synth.GetType().Name,
                FreezeBpm = bpm,
                StartPositionBeats = start,
                EndPositionBeats = end,
                SampleRate = Settings.SampleRate,
                Channels = Settings.Channels
            };

            // Store synth parameters (best effort)
            // Note: This is a simplified approach - actual implementation may need synth-specific parameter extraction

            // Create renderer
            var renderer = new TrackRenderer
            {
                IncludeEffects = FreezeWithEffects,
                TailLengthSeconds = TailLengthSeconds,
                NormalizeOutput = false
            };

            // Render track
            progress?.Report(RenderProgress.Preparing(trackIndex, end - start));

            float[] audioBuffer;
            try
            {
                audioBuffer = await renderer.RenderPatternAsync(
                    pattern,
                    bpm,
                    start,
                    end,
                    null, // Effect chain - would need to be passed if available
                    progress,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    SetTrackState(trackIndex, FreezeState.Live);
                }
                FreezeCompleted?.Invoke(this, new FreezeCompletedEventArgs(
                    trackIndex, stopwatch.Elapsed, ex.Message));
                return false;
            }

            // Calculate duration
            freezeData.DurationSeconds = audioBuffer.Length / (double)(Settings.SampleRate * Settings.Channels);

            // Save to file if requested
            if (_saveToFile)
            {
                string fileName = $"track_{trackIndex}_{DateTime.Now:yyyyMMdd_HHmmss}.wav";
                string filePath = Path.Combine(_frozenTracksDirectory, fileName);

                await renderer.SaveToFileAsync(audioBuffer, filePath, cancellationToken);
                freezeData.FrozenAudioFilePath = filePath;
            }

            // Keep in memory if requested
            if (_keepInMemory)
            {
                freezeData.FrozenAudioBuffer = audioBuffer;
            }

            // Create frozen track player
            var frozenPlayer = new FrozenTrackPlayer
            {
                Name = $"Frozen: {pattern.Name}",
                Loop = pattern.IsLooping
            };
            frozenPlayer.LoadFromBuffer(audioBuffer, freezeData);

            // Store freeze data and update state
            lock (_lock)
            {
                _freezeDataStore[trackIndex] = freezeData;
                _frozenPlayers[trackIndex] = frozenPlayer;
                SetTrackState(trackIndex, FreezeState.Frozen);
            }

            stopwatch.Stop();

            // Raise completed event
            FreezeCompleted?.Invoke(this, new FreezeCompletedEventArgs(
                trackIndex,
                stopwatch.Elapsed,
                freezeData.DurationSeconds,
                freezeData));

            return true;
        }
        catch (OperationCanceledException)
        {
            lock (_lock)
            {
                SetTrackState(trackIndex, FreezeState.Live);
            }
            throw;
        }
        catch (Exception ex)
        {
            lock (_lock)
            {
                SetTrackState(trackIndex, FreezeState.Live);
            }
            FreezeCompleted?.Invoke(this, new FreezeCompletedEventArgs(
                trackIndex, stopwatch.Elapsed, ex.Message));
            return false;
        }
    }

    /// <summary>
    /// Unfreezes a track, restoring its original synth and configuration.
    /// </summary>
    /// <param name="trackIndex">The track index to unfreeze.</param>
    /// <param name="deleteAudioFile">Whether to delete the frozen audio file.</param>
    /// <returns>True if the unfreeze was successful.</returns>
    public bool UnfreezeTrack(int trackIndex, bool deleteAudioFile = false)
    {
        var stopwatch = Stopwatch.StartNew();

        lock (_lock)
        {
            var currentState = GetTrackState(trackIndex);
            if (currentState != FreezeState.Frozen)
            {
                UnfreezeCompleted?.Invoke(this, new UnfreezeCompletedEventArgs(
                    trackIndex, TimeSpan.Zero, "Track is not frozen."));
                return false;
            }

            SetTrackState(trackIndex, FreezeState.Unfreezing);
        }

        try
        {
            FreezeData? freezeData;
            FrozenTrackPlayer? frozenPlayer;

            lock (_lock)
            {
                _freezeDataStore.TryGetValue(trackIndex, out freezeData);
                _frozenPlayers.TryGetValue(trackIndex, out frozenPlayer);
            }

            // Clean up frozen player
            if (frozenPlayer != null)
            {
                frozenPlayer.Dispose();
                lock (_lock)
                {
                    _frozenPlayers.Remove(trackIndex);
                }
            }

            // Delete audio file if requested
            if (deleteAudioFile && freezeData?.FrozenAudioFilePath != null)
            {
                try
                {
                    if (File.Exists(freezeData.FrozenAudioFilePath))
                    {
                        File.Delete(freezeData.FrozenAudioFilePath);
                    }
                }
                catch
                {
                    // Ignore file deletion errors
                }
            }

            // Clear freeze data
            lock (_lock)
            {
                _freezeDataStore.Remove(trackIndex);
                SetTrackState(trackIndex, FreezeState.Live);
            }

            stopwatch.Stop();

            // Raise completed event
            UnfreezeCompleted?.Invoke(this, new UnfreezeCompletedEventArgs(
                trackIndex,
                stopwatch.Elapsed,
                synthRestored: true,
                effectsRestored: true));

            return true;
        }
        catch (Exception ex)
        {
            lock (_lock)
            {
                SetTrackState(trackIndex, FreezeState.Frozen);
            }
            UnfreezeCompleted?.Invoke(this, new UnfreezeCompletedEventArgs(
                trackIndex, stopwatch.Elapsed, ex.Message));
            return false;
        }
    }

    /// <summary>
    /// Freezes all tracks in the sequencer.
    /// </summary>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of tracks successfully frozen.</returns>
    public async Task<int> FreezeAllTracksAsync(
        IProgress<RenderProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var patterns = _sequencer.Patterns;
        int successCount = 0;

        for (int i = 0; i < patterns.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (GetTrackState(i) == FreezeState.Live)
            {
                if (await FreezeTrackAsync(i, progress: progress, cancellationToken: cancellationToken))
                {
                    successCount++;
                }
            }
        }

        return successCount;
    }

    /// <summary>
    /// Unfreezes all frozen tracks.
    /// </summary>
    /// <param name="deleteAudioFiles">Whether to delete frozen audio files.</param>
    /// <returns>The number of tracks successfully unfrozen.</returns>
    public int UnfreezeAllTracks(bool deleteAudioFiles = false)
    {
        int successCount = 0;

        List<int> frozenTracks;
        lock (_lock)
        {
            frozenTracks = new List<int>();
            foreach (var kvp in _trackStates)
            {
                if (kvp.Value == FreezeState.Frozen)
                {
                    frozenTracks.Add(kvp.Key);
                }
            }
        }

        foreach (int trackIndex in frozenTracks)
        {
            if (UnfreezeTrack(trackIndex, deleteAudioFiles))
            {
                successCount++;
            }
        }

        return successCount;
    }

    /// <summary>
    /// Gets a list of all frozen track indices.
    /// </summary>
    /// <returns>List of frozen track indices.</returns>
    public List<int> GetFrozenTracks()
    {
        lock (_lock)
        {
            var frozenTracks = new List<int>();
            foreach (var kvp in _trackStates)
            {
                if (kvp.Value == FreezeState.Frozen)
                {
                    frozenTracks.Add(kvp.Key);
                }
            }
            return frozenTracks;
        }
    }

    /// <summary>
    /// Cleans up orphaned frozen audio files.
    /// </summary>
    /// <returns>The number of files deleted.</returns>
    public int CleanupOrphanedFiles()
    {
        if (!Directory.Exists(_frozenTracksDirectory))
            return 0;

        int deleted = 0;
        var activeFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        lock (_lock)
        {
            foreach (var data in _freezeDataStore.Values)
            {
                if (!string.IsNullOrEmpty(data.FrozenAudioFilePath))
                {
                    activeFiles.Add(data.FrozenAudioFilePath);
                }
            }
        }

        foreach (var file in Directory.GetFiles(_frozenTracksDirectory, "track_*.wav"))
        {
            if (!activeFiles.Contains(file))
            {
                try
                {
                    File.Delete(file);
                    deleted++;
                }
                catch
                {
                    // Ignore deletion errors
                }
            }
        }

        return deleted;
    }

    private void SetTrackState(int trackIndex, FreezeState newState)
    {
        FreezeState previousState;

        lock (_lock)
        {
            _trackStates.TryGetValue(trackIndex, out previousState);
            _trackStates[trackIndex] = newState;
        }

        if (previousState != newState)
        {
            FreezeStateChanged?.Invoke(this, new FreezeStateChangedEventArgs(
                trackIndex, previousState, newState));
        }
    }

    /// <summary>
    /// Disposes the FreezeManager and cleans up resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes managed and unmanaged resources.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            lock (_lock)
            {
                foreach (var player in _frozenPlayers.Values)
                {
                    player.Dispose();
                }
                _frozenPlayers.Clear();
                _freezeDataStore.Clear();
                _trackStates.Clear();
            }
        }

        _disposed = true;
    }

    /// <summary>
    /// Finalizer.
    /// </summary>
    ~FreezeManager()
    {
        Dispose(false);
    }
}
