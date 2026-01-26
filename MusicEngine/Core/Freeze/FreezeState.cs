// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

namespace MusicEngine.Core.Freeze;


/// <summary>
/// Represents the current state of a track's freeze status.
/// </summary>
public enum FreezeState
{
    /// <summary>
    /// Track is playing live with real-time synthesis and effects processing.
    /// </summary>
    Live,

    /// <summary>
    /// Track is currently being rendered to an audio buffer (freeze in progress).
    /// </summary>
    Freezing,

    /// <summary>
    /// Track has been frozen and is playing back pre-rendered audio.
    /// </summary>
    Frozen,

    /// <summary>
    /// Track is being restored from frozen state to live (unfreeze in progress).
    /// </summary>
    Unfreezing
}
