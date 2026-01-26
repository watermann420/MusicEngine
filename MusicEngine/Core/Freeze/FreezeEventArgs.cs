// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;


namespace MusicEngine.Core.Freeze;


/// <summary>
/// Event arguments for freeze operation started event.
/// </summary>
public class FreezeStartedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the track index being frozen.
    /// </summary>
    public int TrackIndex { get; }

    /// <summary>
    /// Gets the start position in beats for the freeze.
    /// </summary>
    public double StartPositionBeats { get; }

    /// <summary>
    /// Gets the end position in beats for the freeze.
    /// </summary>
    public double EndPositionBeats { get; }

    /// <summary>
    /// Gets the estimated duration of the freeze operation.
    /// </summary>
    public TimeSpan? EstimatedDuration { get; }

    /// <summary>
    /// Creates a new instance of <see cref="FreezeStartedEventArgs"/>.
    /// </summary>
    /// <param name="trackIndex">The track index being frozen.</param>
    /// <param name="startPositionBeats">The start position in beats.</param>
    /// <param name="endPositionBeats">The end position in beats.</param>
    /// <param name="estimatedDuration">The estimated duration of the operation.</param>
    public FreezeStartedEventArgs(
        int trackIndex,
        double startPositionBeats,
        double endPositionBeats,
        TimeSpan? estimatedDuration = null)
    {
        TrackIndex = trackIndex;
        StartPositionBeats = startPositionBeats;
        EndPositionBeats = endPositionBeats;
        EstimatedDuration = estimatedDuration;
    }
}


/// <summary>
/// Event arguments for freeze operation completed event.
/// </summary>
public class FreezeCompletedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the track index that was frozen.
    /// </summary>
    public int TrackIndex { get; }

    /// <summary>
    /// Gets whether the freeze operation was successful.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Gets the total duration of the freeze operation.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Gets the length of the frozen audio in seconds.
    /// </summary>
    public double FrozenLengthSeconds { get; }

    /// <summary>
    /// Gets the error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Gets the freeze data containing the frozen audio and original configuration.
    /// </summary>
    public FreezeData? FreezeData { get; }

    /// <summary>
    /// Creates a new instance of <see cref="FreezeCompletedEventArgs"/> for a successful operation.
    /// </summary>
    /// <param name="trackIndex">The track index that was frozen.</param>
    /// <param name="duration">The duration of the freeze operation.</param>
    /// <param name="frozenLengthSeconds">The length of the frozen audio in seconds.</param>
    /// <param name="freezeData">The freeze data.</param>
    public FreezeCompletedEventArgs(
        int trackIndex,
        TimeSpan duration,
        double frozenLengthSeconds,
        FreezeData? freezeData)
    {
        TrackIndex = trackIndex;
        Success = true;
        Duration = duration;
        FrozenLengthSeconds = frozenLengthSeconds;
        FreezeData = freezeData;
    }

    /// <summary>
    /// Creates a new instance of <see cref="FreezeCompletedEventArgs"/> for a failed operation.
    /// </summary>
    /// <param name="trackIndex">The track index where the freeze failed.</param>
    /// <param name="duration">The duration before failure.</param>
    /// <param name="errorMessage">The error message.</param>
    public FreezeCompletedEventArgs(int trackIndex, TimeSpan duration, string errorMessage)
    {
        TrackIndex = trackIndex;
        Success = false;
        Duration = duration;
        FrozenLengthSeconds = 0;
        ErrorMessage = errorMessage;
    }
}


/// <summary>
/// Event arguments for unfreeze operation completed event.
/// </summary>
public class UnfreezeCompletedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the track index that was unfrozen.
    /// </summary>
    public int TrackIndex { get; }

    /// <summary>
    /// Gets whether the unfreeze operation was successful.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Gets the total duration of the unfreeze operation.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Gets the error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Gets whether the original synth was restored successfully.
    /// </summary>
    public bool SynthRestored { get; }

    /// <summary>
    /// Gets whether the original effect chain was restored successfully.
    /// </summary>
    public bool EffectsRestored { get; }

    /// <summary>
    /// Creates a new instance of <see cref="UnfreezeCompletedEventArgs"/> for a successful operation.
    /// </summary>
    /// <param name="trackIndex">The track index that was unfrozen.</param>
    /// <param name="duration">The duration of the unfreeze operation.</param>
    /// <param name="synthRestored">Whether the synth was restored.</param>
    /// <param name="effectsRestored">Whether the effects were restored.</param>
    public UnfreezeCompletedEventArgs(
        int trackIndex,
        TimeSpan duration,
        bool synthRestored = true,
        bool effectsRestored = true)
    {
        TrackIndex = trackIndex;
        Success = true;
        Duration = duration;
        SynthRestored = synthRestored;
        EffectsRestored = effectsRestored;
    }

    /// <summary>
    /// Creates a new instance of <see cref="UnfreezeCompletedEventArgs"/> for a failed operation.
    /// </summary>
    /// <param name="trackIndex">The track index where the unfreeze failed.</param>
    /// <param name="duration">The duration before failure.</param>
    /// <param name="errorMessage">The error message.</param>
    public UnfreezeCompletedEventArgs(int trackIndex, TimeSpan duration, string errorMessage)
    {
        TrackIndex = trackIndex;
        Success = false;
        Duration = duration;
        ErrorMessage = errorMessage;
        SynthRestored = false;
        EffectsRestored = false;
    }
}


/// <summary>
/// Event arguments for freeze state changed event.
/// </summary>
public class FreezeStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the track index whose state changed.
    /// </summary>
    public int TrackIndex { get; }

    /// <summary>
    /// Gets the previous freeze state.
    /// </summary>
    public FreezeState PreviousState { get; }

    /// <summary>
    /// Gets the new freeze state.
    /// </summary>
    public FreezeState NewState { get; }

    /// <summary>
    /// Gets the timestamp of the state change.
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Creates a new instance of <see cref="FreezeStateChangedEventArgs"/>.
    /// </summary>
    /// <param name="trackIndex">The track index.</param>
    /// <param name="previousState">The previous state.</param>
    /// <param name="newState">The new state.</param>
    public FreezeStateChangedEventArgs(int trackIndex, FreezeState previousState, FreezeState newState)
    {
        TrackIndex = trackIndex;
        PreviousState = previousState;
        NewState = newState;
        Timestamp = DateTime.UtcNow;
    }
}
