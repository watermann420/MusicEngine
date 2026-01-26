// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System.Drawing;

namespace MusicEngine.Core.Session;

/// <summary>
/// Represents a scene (horizontal row) in the clip launcher grid.
/// Launching a scene triggers all clips in that row simultaneously.
/// </summary>
public class Scene
{
    /// <summary>Unique identifier for this scene.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Index of this scene in the launcher.</summary>
    public int Index { get; internal set; }

    /// <summary>Display name of the scene.</summary>
    public string Name { get; set; }

    /// <summary>Optional tempo override when this scene is launched.</summary>
    public double? TempoOverride { get; set; }

    /// <summary>Optional time signature numerator override.</summary>
    public int? TimeSignatureNumerator { get; set; }

    /// <summary>Optional time signature denominator override.</summary>
    public int? TimeSignatureDenominator { get; set; }

    /// <summary>Color for visual identification.</summary>
    public Color Color { get; set; } = Color.FromArgb(255, 80, 80, 80);

    /// <summary>Slots in this scene (one per track).</summary>
    public List<ClipSlot> Slots { get; } = new();

    /// <summary>Reference to parent ClipLauncher.</summary>
    internal ClipLauncher? Launcher { get; set; }

    /// <summary>Whether this scene is currently active (any clip playing).</summary>
    public bool IsActive => Slots.Any(s => s.IsPlaying);

    /// <summary>
    /// Creates a new scene with the given index.
    /// </summary>
    /// <param name="index">Scene index.</param>
    /// <param name="name">Optional scene name.</param>
    public Scene(int index, string? name = null)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index), "Scene index must be non-negative.");

        Index = index;
        Name = name ?? $"Scene {index + 1}";
    }

    /// <summary>
    /// Launches all clips in this scene.
    /// </summary>
    public void Launch()
    {
        if (Launcher == null)
            throw new InvalidOperationException("Scene is not attached to a ClipLauncher.");

        Launcher.LaunchScene(Index);
    }

    /// <summary>
    /// Stops all clips in this scene.
    /// </summary>
    public void Stop()
    {
        if (Launcher == null)
            throw new InvalidOperationException("Scene is not attached to a ClipLauncher.");

        foreach (var slot in Slots)
        {
            if (slot.IsPlaying || slot.IsQueued)
            {
                slot.Stop();
            }
        }
    }

    /// <summary>
    /// Gets the slot at the specified track index.
    /// </summary>
    /// <param name="trackIndex">Track index.</param>
    /// <returns>The clip slot, or null if out of range.</returns>
    public ClipSlot? GetSlot(int trackIndex)
    {
        if (trackIndex < 0 || trackIndex >= Slots.Count)
            return null;
        return Slots[trackIndex];
    }

    /// <summary>
    /// Gets the number of clips in this scene.
    /// </summary>
    public int ClipCount => Slots.Count(s => s.HasClip);

    /// <summary>
    /// Gets all clips in this scene.
    /// </summary>
    public IEnumerable<LaunchClip> GetClips()
    {
        return Slots.Where(s => s.Clip != null).Select(s => s.Clip!);
    }

    /// <summary>
    /// Creates a copy of this scene.
    /// </summary>
    /// <param name="newIndex">Index for the new scene.</param>
    public Scene Clone(int newIndex)
    {
        var clone = new Scene(newIndex, Name + " (Copy)")
        {
            TempoOverride = TempoOverride,
            TimeSignatureNumerator = TimeSignatureNumerator,
            TimeSignatureDenominator = TimeSignatureDenominator,
            Color = Color
        };

        // Note: Slots are created by the ClipLauncher, clips are duplicated separately
        return clone;
    }

    public override string ToString()
    {
        var clipCount = ClipCount;
        var tempo = TempoOverride.HasValue ? $" @ {TempoOverride.Value:F1} BPM" : "";
        return $"Scene {Index + 1}: {Name} ({clipCount} clips){tempo}";
    }
}
