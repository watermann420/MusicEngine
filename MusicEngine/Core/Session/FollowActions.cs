// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

namespace MusicEngine.Core.Session;

/// <summary>
/// Types of follow actions available.
/// </summary>
public enum FollowAction
{
    /// <summary>Do nothing.</summary>
    None,
    /// <summary>Stop the track.</summary>
    Stop,
    /// <summary>Play the next clip in the track.</summary>
    Next,
    /// <summary>Play the previous clip in the track.</summary>
    Previous,
    /// <summary>Play the first clip in the track.</summary>
    First,
    /// <summary>Play the last clip in the track.</summary>
    Last,
    /// <summary>Play a random clip in the track.</summary>
    Random,
    /// <summary>Play a random clip, but not the same one.</summary>
    RandomOther,
    /// <summary>Play the clip at a specific index.</summary>
    PlayIndex,
    /// <summary>Play a clip from a linked group.</summary>
    PlayGroup,
    /// <summary>Jump to a specific scene.</summary>
    JumpToScene,
    /// <summary>Repeat the current clip.</summary>
    Repeat,
    /// <summary>Play the opposite action (A plays B's action).</summary>
    PlayOpposite
}

/// <summary>
/// Trigger timing for follow actions.
/// </summary>
public enum FollowTrigger
{
    /// <summary>Trigger at the end of the clip.</summary>
    ClipEnd,
    /// <summary>Trigger after a specific number of bars.</summary>
    AfterBars,
    /// <summary>Trigger after a specific number of beats.</summary>
    AfterBeats,
    /// <summary>Trigger at a specific bar within the clip.</summary>
    AtBar,
    /// <summary>Trigger after a specific time in milliseconds.</summary>
    AfterTime,
    /// <summary>Trigger on a specific loop iteration.</summary>
    OnLoop
}

/// <summary>
/// Configuration for a single follow action.
/// </summary>
public class FollowActionConfig
{
    /// <summary>The action to perform.</summary>
    public FollowAction Action { get; set; } = FollowAction.None;

    /// <summary>Probability of this action (0-1).</summary>
    public double Probability { get; set; } = 1.0;

    /// <summary>Target index for PlayIndex action.</summary>
    public int TargetIndex { get; set; }

    /// <summary>Target scene index for JumpToScene action.</summary>
    public int TargetScene { get; set; }

    /// <summary>Group name for PlayGroup action.</summary>
    public string GroupName { get; set; } = "";

    /// <summary>Whether this action is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Creates a new follow action configuration.
    /// </summary>
    public FollowActionConfig()
    {
    }

    /// <summary>
    /// Creates a new follow action configuration with specified action.
    /// </summary>
    /// <param name="action">The follow action.</param>
    /// <param name="probability">Action probability.</param>
    public FollowActionConfig(FollowAction action, double probability = 1.0)
    {
        Action = action;
        Probability = Math.Clamp(probability, 0, 1);
    }

    /// <summary>
    /// Creates a copy of this configuration.
    /// </summary>
    public FollowActionConfig Clone()
    {
        return new FollowActionConfig
        {
            Action = Action,
            Probability = Probability,
            TargetIndex = TargetIndex,
            TargetScene = TargetScene,
            GroupName = GroupName,
            Enabled = Enabled
        };
    }
}

/// <summary>
/// Complete follow action settings for a clip.
/// </summary>
public class FollowActionSettings
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Whether follow actions are enabled for this clip.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Primary follow action (Action A).</summary>
    public FollowActionConfig ActionA { get; set; } = new();

    /// <summary>Secondary follow action (Action B).</summary>
    public FollowActionConfig ActionB { get; set; } = new();

    /// <summary>Chance of Action A (0-1). Action B gets remaining probability.</summary>
    public double ActionAChance { get; set; } = 1.0;

    /// <summary>When to trigger the follow action.</summary>
    public FollowTrigger Trigger { get; set; } = FollowTrigger.ClipEnd;

    /// <summary>Trigger value (bars, beats, time, or loop number depending on trigger type).</summary>
    public double TriggerValue { get; set; } = 1.0;

    /// <summary>Quantization for the triggered clip.</summary>
    public QuantizeMode Quantize { get; set; } = QuantizeMode.Bar;

    /// <summary>Whether to legato (don't retrigger if same clip).</summary>
    public bool Legato { get; set; }

    /// <summary>Number of times to repeat before executing follow action.</summary>
    public int RepeatCount { get; set; } = 1;

    /// <summary>Current repeat iteration.</summary>
    public int CurrentRepeat { get; internal set; }

    /// <summary>
    /// Creates a copy of these settings.
    /// </summary>
    public FollowActionSettings Clone()
    {
        return new FollowActionSettings
        {
            Enabled = Enabled,
            ActionA = ActionA.Clone(),
            ActionB = ActionB.Clone(),
            ActionAChance = ActionAChance,
            Trigger = Trigger,
            TriggerValue = TriggerValue,
            Quantize = Quantize,
            Legato = Legato,
            RepeatCount = RepeatCount
        };
    }
}

/// <summary>
/// Event arguments for follow action execution.
/// </summary>
public class FollowActionEventArgs : EventArgs
{
    /// <summary>The clip that triggered the action.</summary>
    public required LaunchClip SourceClip { get; init; }

    /// <summary>The follow action that was executed.</summary>
    public required FollowActionConfig ExecutedAction { get; init; }

    /// <summary>The target clip (if applicable).</summary>
    public LaunchClip? TargetClip { get; init; }

    /// <summary>Track index.</summary>
    public int TrackIndex { get; init; }

    /// <summary>Whether the action was Action A (true) or Action B (false).</summary>
    public bool WasActionA { get; init; }
}

/// <summary>
/// Manages follow actions for clips in a session.
/// Handles automatic clip triggering based on configured actions.
/// </summary>
public class FollowActionManager : IDisposable
{
    private readonly Dictionary<Guid, FollowActionSettings> _clipSettings = new();
    private readonly Dictionary<string, List<Guid>> _groups = new();
    private readonly object _lock = new();
    private readonly Random _random = new();
    private bool _disposed;

    /// <summary>Reference to the clip launcher.</summary>
    public ClipLauncher? Launcher { get; set; }

    /// <summary>Whether follow actions are globally enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Fired when a follow action is executed.</summary>
    public event EventHandler<FollowActionEventArgs>? ActionExecuted;

    /// <summary>Fired when a follow action fails (e.g., no target clip).</summary>
    public event EventHandler<FollowActionEventArgs>? ActionFailed;

    /// <summary>
    /// Creates a new follow action manager.
    /// </summary>
    public FollowActionManager()
    {
    }

    /// <summary>
    /// Creates a new follow action manager with a clip launcher.
    /// </summary>
    /// <param name="launcher">The clip launcher to manage.</param>
    public FollowActionManager(ClipLauncher launcher)
    {
        Launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
    }

    /// <summary>
    /// Sets follow action settings for a clip.
    /// </summary>
    /// <param name="clip">The clip.</param>
    /// <param name="settings">Follow action settings.</param>
    public void SetSettings(LaunchClip clip, FollowActionSettings settings)
    {
        if (clip == null)
            throw new ArgumentNullException(nameof(clip));
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        lock (_lock)
        {
            _clipSettings[clip.Id] = settings;
        }
    }

    /// <summary>
    /// Gets follow action settings for a clip.
    /// </summary>
    /// <param name="clip">The clip.</param>
    /// <returns>Settings if found, null otherwise.</returns>
    public FollowActionSettings? GetSettings(LaunchClip clip)
    {
        if (clip == null) return null;

        lock (_lock)
        {
            return _clipSettings.TryGetValue(clip.Id, out var settings) ? settings : null;
        }
    }

    /// <summary>
    /// Removes follow action settings for a clip.
    /// </summary>
    /// <param name="clip">The clip.</param>
    public void RemoveSettings(LaunchClip clip)
    {
        if (clip == null) return;

        lock (_lock)
        {
            _clipSettings.Remove(clip.Id);
        }
    }

    /// <summary>
    /// Adds a clip to a named group.
    /// </summary>
    /// <param name="groupName">Group name.</param>
    /// <param name="clip">The clip to add.</param>
    public void AddToGroup(string groupName, LaunchClip clip)
    {
        if (string.IsNullOrEmpty(groupName))
            throw new ArgumentException("Group name cannot be empty.", nameof(groupName));
        if (clip == null)
            throw new ArgumentNullException(nameof(clip));

        lock (_lock)
        {
            if (!_groups.TryGetValue(groupName, out var group))
            {
                group = new List<Guid>();
                _groups[groupName] = group;
            }

            if (!group.Contains(clip.Id))
            {
                group.Add(clip.Id);
            }
        }
    }

    /// <summary>
    /// Removes a clip from a group.
    /// </summary>
    /// <param name="groupName">Group name.</param>
    /// <param name="clip">The clip to remove.</param>
    public void RemoveFromGroup(string groupName, LaunchClip clip)
    {
        if (string.IsNullOrEmpty(groupName) || clip == null) return;

        lock (_lock)
        {
            if (_groups.TryGetValue(groupName, out var group))
            {
                group.Remove(clip.Id);
            }
        }
    }

    /// <summary>
    /// Gets all clips in a group.
    /// </summary>
    /// <param name="groupName">Group name.</param>
    /// <returns>List of clip IDs in the group.</returns>
    public IReadOnlyList<Guid> GetGroupClips(string groupName)
    {
        lock (_lock)
        {
            if (_groups.TryGetValue(groupName, out var group))
            {
                return group.ToList().AsReadOnly();
            }
            return Array.Empty<Guid>();
        }
    }

    /// <summary>
    /// Processes a clip's follow action when it reaches a trigger point.
    /// </summary>
    /// <param name="clip">The clip that reached the trigger.</param>
    /// <param name="currentBeat">Current beat position.</param>
    /// <param name="loopIteration">Current loop iteration.</param>
    /// <returns>The action that was executed, or null if none.</returns>
    public FollowActionConfig? ProcessTrigger(LaunchClip clip, double currentBeat, int loopIteration)
    {
        if (!Enabled || clip == null || Launcher == null) return null;

        var settings = GetSettings(clip);
        if (settings == null || !settings.Enabled) return null;

        // Check if we should trigger
        if (!ShouldTrigger(clip, settings, currentBeat, loopIteration))
            return null;

        // Check repeat count
        settings.CurrentRepeat++;
        if (settings.CurrentRepeat < settings.RepeatCount)
            return null;

        settings.CurrentRepeat = 0;

        // Choose action based on probability
        var action = ChooseAction(settings);
        if (action == null || action.Action == FollowAction.None)
            return null;

        // Execute the action
        ExecuteAction(clip, action, settings);

        return action;
    }

    private bool ShouldTrigger(LaunchClip clip, FollowActionSettings settings, double currentBeat, int loopIteration)
    {
        switch (settings.Trigger)
        {
            case FollowTrigger.ClipEnd:
                // Triggered externally when clip ends
                return true;

            case FollowTrigger.AfterBars:
                double barLength = 4.0; // Assuming 4/4
                return clip.PlayPosition >= settings.TriggerValue * barLength;

            case FollowTrigger.AfterBeats:
                return clip.PlayPosition >= settings.TriggerValue;

            case FollowTrigger.AtBar:
                double targetBeat = settings.TriggerValue * 4.0;
                double moduloPosition = clip.PlayPosition % clip.EffectiveLength;
                return Math.Abs(moduloPosition - targetBeat) < 0.1;

            case FollowTrigger.AfterTime:
                // Would need time tracking - simplified to beat-based
                return clip.PlayPosition >= settings.TriggerValue / 500.0; // Rough conversion

            case FollowTrigger.OnLoop:
                return loopIteration >= (int)settings.TriggerValue;

            default:
                return true;
        }
    }

    private FollowActionConfig? ChooseAction(FollowActionSettings settings)
    {
        if (!settings.ActionA.Enabled && !settings.ActionB.Enabled)
            return null;

        if (!settings.ActionA.Enabled)
            return settings.ActionB;

        if (!settings.ActionB.Enabled)
            return settings.ActionA;

        // Choose based on ActionAChance
        if (_random.NextDouble() < settings.ActionAChance)
            return settings.ActionA;
        else
            return settings.ActionB;
    }

    private void ExecuteAction(LaunchClip sourceClip, FollowActionConfig action, FollowActionSettings settings)
    {
        if (Launcher == null) return;

        int trackIndex = sourceClip.TrackIndex;
        LaunchClip? targetClip = null;

        switch (action.Action)
        {
            case FollowAction.Stop:
                Launcher.StopTrack(trackIndex);
                break;

            case FollowAction.Next:
                targetClip = GetNextClip(sourceClip);
                break;

            case FollowAction.Previous:
                targetClip = GetPreviousClip(sourceClip);
                break;

            case FollowAction.First:
                targetClip = GetFirstClip(sourceClip);
                break;

            case FollowAction.Last:
                targetClip = GetLastClip(sourceClip);
                break;

            case FollowAction.Random:
                targetClip = GetRandomClip(sourceClip, false);
                break;

            case FollowAction.RandomOther:
                targetClip = GetRandomClip(sourceClip, true);
                break;

            case FollowAction.PlayIndex:
                targetClip = GetClipAtIndex(trackIndex, action.TargetIndex);
                break;

            case FollowAction.JumpToScene:
                if (action.TargetScene >= 0 && action.TargetScene < Launcher.SceneCount)
                {
                    Launcher.LaunchScene(action.TargetScene);
                }
                break;

            case FollowAction.Repeat:
                targetClip = sourceClip;
                break;

            case FollowAction.PlayGroup:
                targetClip = GetRandomFromGroup(action.GroupName, sourceClip);
                break;

            case FollowAction.PlayOpposite:
                // If this is action A, play action B's target, and vice versa
                var oppositeAction = action == settings.ActionA ? settings.ActionB : settings.ActionA;
                ExecuteAction(sourceClip, oppositeAction, settings);
                return;
        }

        // Launch target clip if found
        if (targetClip != null)
        {
            // Check probability
            if (_random.NextDouble() > action.Probability)
                return;

            // Check legato
            if (settings.Legato && targetClip.Id == sourceClip.Id)
                return;

            Launcher.LaunchClip(targetClip.TrackIndex, targetClip.SceneIndex);

            ActionExecuted?.Invoke(this, new FollowActionEventArgs
            {
                SourceClip = sourceClip,
                ExecutedAction = action,
                TargetClip = targetClip,
                TrackIndex = trackIndex,
                WasActionA = action == settings.ActionA
            });
        }
        else if (action.Action != FollowAction.Stop && action.Action != FollowAction.JumpToScene)
        {
            ActionFailed?.Invoke(this, new FollowActionEventArgs
            {
                SourceClip = sourceClip,
                ExecutedAction = action,
                TrackIndex = trackIndex,
                WasActionA = action == settings.ActionA
            });
        }
    }

    private LaunchClip? GetNextClip(LaunchClip current)
    {
        if (Launcher == null) return null;

        for (int i = current.SceneIndex + 1; i < Launcher.SceneCount; i++)
        {
            var slot = Launcher.GetSlot(current.TrackIndex, i);
            if (slot?.Clip != null)
                return slot.Clip;
        }

        // Wrap to first
        return GetFirstClip(current);
    }

    private LaunchClip? GetPreviousClip(LaunchClip current)
    {
        if (Launcher == null) return null;

        for (int i = current.SceneIndex - 1; i >= 0; i--)
        {
            var slot = Launcher.GetSlot(current.TrackIndex, i);
            if (slot?.Clip != null)
                return slot.Clip;
        }

        // Wrap to last
        return GetLastClip(current);
    }

    private LaunchClip? GetFirstClip(LaunchClip current)
    {
        if (Launcher == null) return null;

        for (int i = 0; i < Launcher.SceneCount; i++)
        {
            var slot = Launcher.GetSlot(current.TrackIndex, i);
            if (slot?.Clip != null)
                return slot.Clip;
        }
        return null;
    }

    private LaunchClip? GetLastClip(LaunchClip current)
    {
        if (Launcher == null) return null;

        for (int i = Launcher.SceneCount - 1; i >= 0; i--)
        {
            var slot = Launcher.GetSlot(current.TrackIndex, i);
            if (slot?.Clip != null)
                return slot.Clip;
        }
        return null;
    }

    private LaunchClip? GetRandomClip(LaunchClip current, bool excludeCurrent)
    {
        if (Launcher == null) return null;

        var clips = new List<LaunchClip>();
        for (int i = 0; i < Launcher.SceneCount; i++)
        {
            var slot = Launcher.GetSlot(current.TrackIndex, i);
            if (slot?.Clip != null)
            {
                if (!excludeCurrent || slot.Clip.Id != current.Id)
                {
                    clips.Add(slot.Clip);
                }
            }
        }

        if (clips.Count == 0) return null;
        return clips[_random.Next(clips.Count)];
    }

    private LaunchClip? GetClipAtIndex(int trackIndex, int sceneIndex)
    {
        if (Launcher == null) return null;

        var slot = Launcher.GetSlot(trackIndex, sceneIndex);
        return slot?.Clip;
    }

    private LaunchClip? GetRandomFromGroup(string groupName, LaunchClip current)
    {
        if (Launcher == null || string.IsNullOrEmpty(groupName)) return null;

        var clipIds = GetGroupClips(groupName);
        if (clipIds.Count == 0) return null;

        // Find a random clip from the group that exists in the launcher
        var validClips = new List<LaunchClip>();
        for (int t = 0; t < Launcher.TrackCount; t++)
        {
            for (int s = 0; s < Launcher.SceneCount; s++)
            {
                var slot = Launcher.GetSlot(t, s);
                if (slot?.Clip != null && clipIds.Contains(slot.Clip.Id))
                {
                    validClips.Add(slot.Clip);
                }
            }
        }

        if (validClips.Count == 0) return null;
        return validClips[_random.Next(validClips.Count)];
    }

    #region Preset Configurations

    /// <summary>
    /// Creates settings for simple sequential playback.
    /// </summary>
    public static FollowActionSettings CreateSequential()
    {
        return new FollowActionSettings
        {
            Enabled = true,
            ActionA = new FollowActionConfig(FollowAction.Next, 1.0),
            ActionAChance = 1.0,
            Trigger = FollowTrigger.ClipEnd
        };
    }

    /// <summary>
    /// Creates settings for random playback.
    /// </summary>
    public static FollowActionSettings CreateRandom()
    {
        return new FollowActionSettings
        {
            Enabled = true,
            ActionA = new FollowActionConfig(FollowAction.RandomOther, 1.0),
            ActionAChance = 1.0,
            Trigger = FollowTrigger.ClipEnd
        };
    }

    /// <summary>
    /// Creates settings for A/B alternation.
    /// </summary>
    public static FollowActionSettings CreateAlternating(int otherIndex)
    {
        return new FollowActionSettings
        {
            Enabled = true,
            ActionA = new FollowActionConfig(FollowAction.PlayIndex) { TargetIndex = otherIndex },
            ActionAChance = 1.0,
            Trigger = FollowTrigger.ClipEnd
        };
    }

    /// <summary>
    /// Creates settings with weighted probability between two clips.
    /// </summary>
    public static FollowActionSettings CreateWeighted(int indexA, int indexB, double chanceA)
    {
        return new FollowActionSettings
        {
            Enabled = true,
            ActionA = new FollowActionConfig(FollowAction.PlayIndex) { TargetIndex = indexA },
            ActionB = new FollowActionConfig(FollowAction.PlayIndex) { TargetIndex = indexB },
            ActionAChance = chanceA,
            Trigger = FollowTrigger.ClipEnd
        };
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lock)
        {
            _clipSettings.Clear();
            _groups.Clear();
        }

        GC.SuppressFinalize(this);
    }
}
