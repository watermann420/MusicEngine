//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Represents a single slot in the clip launcher grid that can hold a clip.

namespace MusicEngine.Core.Session;

/// <summary>
/// Represents a single slot in the clip launcher grid.
/// Each slot is at the intersection of a track (column) and scene (row).
/// </summary>
public class ClipSlot
{
    /// <summary>Track index (column) of this slot.</summary>
    public int TrackIndex { get; }

    /// <summary>Scene index (row) of this slot.</summary>
    public int SceneIndex { get; }

    /// <summary>The clip contained in this slot, or null if empty.</summary>
    public LaunchClip? Clip { get; set; }

    /// <summary>Reference to parent ClipLauncher.</summary>
    internal ClipLauncher? Launcher { get; set; }

    /// <summary>Whether this slot is armed for recording.</summary>
    public bool IsArmed { get; set; }

    /// <summary>Whether this slot has a stop button (stops track on launch).</summary>
    public bool HasStopButton { get; set; } = true;

    /// <summary>
    /// Creates a new clip slot at the specified grid position.
    /// </summary>
    /// <param name="trackIndex">Track (column) index.</param>
    /// <param name="sceneIndex">Scene (row) index.</param>
    public ClipSlot(int trackIndex, int sceneIndex)
    {
        if (trackIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(trackIndex), "Track index must be non-negative.");
        if (sceneIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(sceneIndex), "Scene index must be non-negative.");

        TrackIndex = trackIndex;
        SceneIndex = sceneIndex;
    }

    /// <summary>
    /// Whether this slot contains a clip.
    /// </summary>
    public bool HasClip => Clip != null;

    /// <summary>
    /// Whether the clip in this slot is currently playing.
    /// </summary>
    public bool IsPlaying => Clip?.State == ClipState.Playing;

    /// <summary>
    /// Whether the clip in this slot is queued to play.
    /// </summary>
    public bool IsQueued => Clip?.State == ClipState.Queued;

    /// <summary>
    /// Whether the clip in this slot is recording.
    /// </summary>
    public bool IsRecording => Clip?.State == ClipState.Recording;

    /// <summary>
    /// Whether this slot is empty (no clip).
    /// </summary>
    public bool IsEmpty => Clip == null;

    /// <summary>
    /// Launches the clip in this slot.
    /// If empty and HasStopButton is true, stops any playing clip on this track.
    /// </summary>
    public void Launch()
    {
        if (Launcher == null)
            throw new InvalidOperationException("Slot is not attached to a ClipLauncher.");

        if (Clip != null)
        {
            Launcher.LaunchClip(TrackIndex, SceneIndex);
        }
        else if (HasStopButton)
        {
            Launcher.StopTrack(TrackIndex);
        }
    }

    /// <summary>
    /// Stops the clip in this slot.
    /// </summary>
    public void Stop()
    {
        if (Launcher == null)
            throw new InvalidOperationException("Slot is not attached to a ClipLauncher.");

        if (Clip != null && (Clip.State == ClipState.Playing || Clip.State == ClipState.Queued))
        {
            Launcher.StopTrack(TrackIndex);
        }
    }

    /// <summary>
    /// Starts recording into this slot.
    /// Creates a new clip if empty.
    /// </summary>
    public void Record()
    {
        if (Launcher == null)
            throw new InvalidOperationException("Slot is not attached to a ClipLauncher.");

        // Create empty clip if needed
        if (Clip == null)
        {
            Clip = new LaunchClip
            {
                Name = $"Recording {TrackIndex + 1}-{SceneIndex + 1}",
                TrackIndex = TrackIndex,
                SceneIndex = SceneIndex,
                Launcher = Launcher
            };
        }

        var oldState = Clip.State;
        Clip.State = ClipState.Recording;
        Clip.Reset();

        Launcher.OnClipStateChanged(Clip, oldState, ClipState.Recording);
    }

    /// <summary>
    /// Sets a clip in this slot.
    /// </summary>
    /// <param name="clip">The clip to set, or null to clear.</param>
    public void SetClip(LaunchClip? clip)
    {
        if (Clip != null)
        {
            Clip.Launcher = null;
            Clip.TrackIndex = -1;
            Clip.SceneIndex = -1;
        }

        Clip = clip;

        if (clip != null)
        {
            clip.Launcher = Launcher;
            clip.TrackIndex = TrackIndex;
            clip.SceneIndex = SceneIndex;
        }
    }

    /// <summary>
    /// Clears the clip from this slot.
    /// </summary>
    public void Clear()
    {
        if (Clip != null && (Clip.State == ClipState.Playing || Clip.State == ClipState.Recording))
        {
            Stop();
        }
        SetClip(null);
    }

    /// <summary>
    /// Duplicates the clip to another slot.
    /// </summary>
    /// <param name="targetSlot">The target slot.</param>
    public void DuplicateTo(ClipSlot targetSlot)
    {
        if (targetSlot == null)
            throw new ArgumentNullException(nameof(targetSlot));

        if (Clip != null)
        {
            targetSlot.SetClip(Clip.Clone());
        }
    }

    public override string ToString()
    {
        var clipInfo = Clip != null ? $"{Clip.Name} ({Clip.State})" : "Empty";
        return $"Slot[{TrackIndex},{SceneIndex}]: {clipInfo}";
    }
}
