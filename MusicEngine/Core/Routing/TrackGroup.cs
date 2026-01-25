//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Track group for grouping multiple tracks with shared controls.


using System;
using System.Drawing;
using NAudio.Wave;


namespace MusicEngine.Core.Routing;


/// <summary>
/// Interface for audio channels that can be added to track groups.
/// </summary>
public interface IAudioChannel : ISampleProvider
{
    /// <summary>
    /// Gets the name of the audio channel.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets or sets the volume level (0.0 to 2.0).
    /// </summary>
    float Volume { get; set; }

    /// <summary>
    /// Gets or sets the pan position (-1.0 to 1.0).
    /// </summary>
    float Pan { get; set; }

    /// <summary>
    /// Gets or sets whether the channel is muted.
    /// </summary>
    bool Mute { get; set; }

    /// <summary>
    /// Gets or sets whether the channel is soloed.
    /// </summary>
    bool Solo { get; set; }
}


/// <summary>
/// Represents a group of tracks that can be controlled together.
/// Provides group fader, mute, solo, and fold/unfold functionality.
/// Supports nested groups for hierarchical organization.
/// </summary>
public class TrackGroup : IDisposable
{
    private readonly object _lock = new();
    private readonly List<string> _memberTrackIds;
    private readonly List<AudioChannel> _memberChannels;
    private readonly List<IAudioChannel> _genericChannels;
    private readonly List<TrackGroup> _childGroups;
    private string _name;
    private float _volume;
    private float _pan;
    private bool _mute;
    private bool _solo;
    private bool _isFolded;
    private bool _disposed;

    /// <summary>
    /// Creates a new track group.
    /// </summary>
    /// <param name="name">The name of the group.</param>
    /// <param name="color">Display color for the group (as ARGB int).</param>
    /// <exception cref="ArgumentNullException">Thrown if name is null or empty.</exception>
    public TrackGroup(string name, int color = unchecked((int)0xFF4CAF50))
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentNullException(nameof(name));

        GroupId = Guid.NewGuid();
        Id = GroupId.ToString();
        _name = name;
        Color = color;
        _memberTrackIds = new List<string>();
        _memberChannels = new List<AudioChannel>();
        _genericChannels = new List<IAudioChannel>();
        _childGroups = new List<TrackGroup>();
        _volume = 1.0f;
        _pan = 0f;
        _mute = false;
        _solo = false;
        _isFolded = false;
    }

    /// <summary>
    /// Gets the unique identifier for this group.
    /// </summary>
    public Guid GroupId { get; }

    /// <summary>
    /// Gets the string identifier for this group.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets or sets the name of this group.
    /// </summary>
    public string Name
    {
        get
        {
            lock (_lock)
            {
                return _name;
            }
        }
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentNullException(nameof(value));

            lock (_lock)
            {
                _name = value;
            }

            OnPropertyChanged(nameof(Name));
        }
    }

    /// <summary>
    /// Gets or sets the display color for this group (as ARGB int).
    /// </summary>
    public int Color { get; set; }

    /// <summary>
    /// Gets or sets the display color for this group as a System.Drawing.Color.
    /// </summary>
    public Color DisplayColor
    {
        get => System.Drawing.Color.FromArgb(Color);
        set => Color = value.ToArgb();
    }

    /// <summary>
    /// Gets or sets the parent group for nested hierarchy.
    /// Null if this is a top-level group.
    /// </summary>
    public TrackGroup? Parent { get; private set; }

    /// <summary>
    /// Gets the list of member track IDs.
    /// </summary>
    public IReadOnlyList<string> MemberTrackIds
    {
        get
        {
            lock (_lock)
            {
                return _memberTrackIds.ToList();
            }
        }
    }

    /// <summary>
    /// Gets the list of member audio channels.
    /// </summary>
    public IReadOnlyList<AudioChannel> MemberChannels
    {
        get
        {
            lock (_lock)
            {
                return _memberChannels.ToList();
            }
        }
    }

    /// <summary>
    /// Gets the number of tracks in this group.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _memberTrackIds.Count;
            }
        }
    }

    /// <summary>
    /// Gets or sets the group volume (0.0 - 2.0).
    /// Affects all member tracks multiplicatively.
    /// </summary>
    public float Volume
    {
        get
        {
            lock (_lock)
            {
                return _volume;
            }
        }
        set
        {
            lock (_lock)
            {
                _volume = Math.Clamp(value, 0f, 2f);
            }

            ApplyVolumeToMembers();
            OnPropertyChanged(nameof(Volume));
        }
    }

    /// <summary>
    /// Gets or sets the group volume in decibels (-inf to +6 dB).
    /// </summary>
    public float VolumeDb
    {
        get
        {
            float vol = Volume;
            if (vol <= 0f) return -100f;
            return 20f * MathF.Log10(vol);
        }
        set
        {
            float db = Math.Clamp(value, -100f, 6f);
            Volume = db <= -100f ? 0f : MathF.Pow(10f, db / 20f);
        }
    }

    /// <summary>
    /// Gets or sets the group pan (-1.0 = left, 0.0 = center, 1.0 = right).
    /// Note: Pan is typically not applied as a group control; individual track pans are maintained.
    /// </summary>
    public float Pan
    {
        get
        {
            lock (_lock)
            {
                return _pan;
            }
        }
        set
        {
            lock (_lock)
            {
                _pan = Math.Clamp(value, -1f, 1f);
            }

            OnPropertyChanged(nameof(Pan));
        }
    }

    /// <summary>
    /// Gets or sets whether this group is muted.
    /// When muted, all member tracks are muted.
    /// </summary>
    public bool Mute
    {
        get
        {
            lock (_lock)
            {
                return _mute;
            }
        }
        set
        {
            lock (_lock)
            {
                _mute = value;
            }

            ApplyMuteToMembers();
            OnPropertyChanged(nameof(Mute));
        }
    }

    /// <summary>
    /// Gets or sets whether this group is soloed.
    /// When soloed, only member tracks (and other soloed tracks) are heard.
    /// </summary>
    public bool Solo
    {
        get
        {
            lock (_lock)
            {
                return _solo;
            }
        }
        set
        {
            lock (_lock)
            {
                _solo = value;
            }

            ApplySoloToMembers();
            OnPropertyChanged(nameof(Solo));
        }
    }

    /// <summary>
    /// Gets or sets whether this group is folded (collapsed) in the UI.
    /// </summary>
    public bool IsFolded
    {
        get
        {
            lock (_lock)
            {
                return _isFolded;
            }
        }
        set
        {
            lock (_lock)
            {
                _isFolded = value;
            }

            OnPropertyChanged(nameof(IsFolded));
            OnPropertyChanged(nameof(Collapsed));
        }
    }

    /// <summary>
    /// Gets or sets whether this group is collapsed (alias for IsFolded).
    /// </summary>
    public bool Collapsed
    {
        get => IsFolded;
        set => IsFolded = value;
    }

    /// <summary>
    /// Gets the list of child groups for nested hierarchy.
    /// </summary>
    public IReadOnlyList<TrackGroup> ChildGroups
    {
        get
        {
            lock (_lock)
            {
                return _childGroups.ToList();
            }
        }
    }

    /// <summary>
    /// Gets the number of child groups.
    /// </summary>
    public int ChildGroupCount
    {
        get
        {
            lock (_lock)
            {
                return _childGroups.Count;
            }
        }
    }

    /// <summary>
    /// Gets or sets arbitrary user data associated with this group.
    /// </summary>
    public object? Tag { get; set; }

    /// <summary>
    /// Event raised when a property changes.
    /// </summary>
    public event EventHandler<string>? PropertyChanged;

    /// <summary>
    /// Event raised when the group membership changes.
    /// </summary>
    public event EventHandler<GroupMembershipChangedEventArgs>? MembershipChanged;

    /// <summary>
    /// Adds a track to this group.
    /// </summary>
    /// <param name="trackId">The track identifier.</param>
    /// <param name="channel">Optional audio channel for the track.</param>
    /// <returns>True if added, false if already a member.</returns>
    public bool AddTrack(string trackId, AudioChannel? channel = null)
    {
        if (string.IsNullOrWhiteSpace(trackId))
            return false;

        lock (_lock)
        {
            if (_memberTrackIds.Contains(trackId))
            {
                return false;
            }

            _memberTrackIds.Add(trackId);

            if (channel != null)
            {
                _memberChannels.Add(channel);
            }
        }

        MembershipChanged?.Invoke(this, new GroupMembershipChangedEventArgs(trackId, true));
        return true;
    }

    /// <summary>
    /// Adds an audio channel to this group.
    /// </summary>
    /// <param name="channel">The audio channel to add.</param>
    /// <returns>True if added, false if already a member.</returns>
    public bool AddChannel(AudioChannel channel)
    {
        if (channel == null)
            return false;

        lock (_lock)
        {
            if (_memberChannels.Contains(channel))
            {
                return false;
            }

            _memberChannels.Add(channel);

            // Also add to track IDs if not present
            if (!_memberTrackIds.Contains(channel.Name))
            {
                _memberTrackIds.Add(channel.Name);
            }
        }

        MembershipChanged?.Invoke(this, new GroupMembershipChangedEventArgs(channel.Name, true));
        return true;
    }

    /// <summary>
    /// Removes a track from this group.
    /// </summary>
    /// <param name="trackId">The track identifier.</param>
    /// <returns>True if removed, false if not a member.</returns>
    public bool RemoveTrack(string trackId)
    {
        if (string.IsNullOrWhiteSpace(trackId))
            return false;

        bool removed;

        lock (_lock)
        {
            removed = _memberTrackIds.Remove(trackId);

            // Also remove any matching channel
            var channelToRemove = _memberChannels.FirstOrDefault(
                c => c.Name.Equals(trackId, StringComparison.OrdinalIgnoreCase));

            if (channelToRemove != null)
            {
                _memberChannels.Remove(channelToRemove);
            }
        }

        if (removed)
        {
            MembershipChanged?.Invoke(this, new GroupMembershipChangedEventArgs(trackId, false));
        }

        return removed;
    }

    /// <summary>
    /// Removes an audio channel from this group.
    /// </summary>
    /// <param name="channel">The audio channel to remove.</param>
    /// <returns>True if removed, false if not a member.</returns>
    public bool RemoveChannel(AudioChannel channel)
    {
        if (channel == null)
            return false;

        bool removed;

        lock (_lock)
        {
            removed = _memberChannels.Remove(channel);

            // Also remove from track IDs
            _memberTrackIds.Remove(channel.Name);
        }

        if (removed)
        {
            MembershipChanged?.Invoke(this, new GroupMembershipChangedEventArgs(channel.Name, false));
        }

        return removed;
    }

    /// <summary>
    /// Checks if a track is a member of this group.
    /// </summary>
    /// <param name="trackId">The track identifier.</param>
    /// <returns>True if the track is a member.</returns>
    public bool Contains(string trackId)
    {
        if (string.IsNullOrWhiteSpace(trackId))
            return false;

        lock (_lock)
        {
            return _memberTrackIds.Contains(trackId);
        }
    }

    /// <summary>
    /// Checks if an audio channel is a member of this group.
    /// </summary>
    /// <param name="channel">The audio channel.</param>
    /// <returns>True if the channel is a member.</returns>
    public bool Contains(AudioChannel channel)
    {
        if (channel == null)
            return false;

        lock (_lock)
        {
            return _memberChannels.Contains(channel);
        }
    }

    /// <summary>
    /// Clears all members from this group.
    /// </summary>
    public void ClearMembers()
    {
        List<string> removedIds;

        lock (_lock)
        {
            removedIds = _memberTrackIds.ToList();
            _memberTrackIds.Clear();
            _memberChannels.Clear();
            _genericChannels.Clear();
        }

        foreach (var trackId in removedIds)
        {
            MembershipChanged?.Invoke(this, new GroupMembershipChangedEventArgs(trackId, false));
        }
    }

    #region IAudioChannel Support

    /// <summary>
    /// Adds an IAudioChannel track to this group.
    /// </summary>
    /// <param name="track">The audio channel to add.</param>
    public void AddTrack(IAudioChannel track)
    {
        ArgumentNullException.ThrowIfNull(track);

        lock (_lock)
        {
            if (!_genericChannels.Contains(track))
            {
                _genericChannels.Add(track);
                if (!_memberTrackIds.Contains(track.Name))
                {
                    _memberTrackIds.Add(track.Name);
                }
            }
        }

        TrackAdded?.Invoke(this, new TrackAddedEventArgs(track));
    }

    /// <summary>
    /// Removes an IAudioChannel track from this group.
    /// </summary>
    /// <param name="track">The audio channel to remove.</param>
    /// <returns>True if removed, false if not a member.</returns>
    public bool RemoveTrack(IAudioChannel track)
    {
        if (track == null)
            return false;

        bool removed;

        lock (_lock)
        {
            removed = _genericChannels.Remove(track);
            if (removed)
            {
                _memberTrackIds.Remove(track.Name);
            }
        }

        if (removed)
        {
            TrackRemoved?.Invoke(this, new TrackRemovedEventArgs(track));
        }

        return removed;
    }

    /// <summary>
    /// Gets all IAudioChannel tracks in this group.
    /// </summary>
    /// <returns>A list of audio channels.</returns>
    public IReadOnlyList<IAudioChannel> GetTracks()
    {
        lock (_lock)
        {
            return _genericChannels.ToList();
        }
    }

    #endregion

    #region Nested Groups Support

    /// <summary>
    /// Creates a new child group within this group.
    /// </summary>
    /// <param name="name">The name of the child group.</param>
    /// <returns>The newly created child group.</returns>
    public TrackGroup CreateChildGroup(string name)
    {
        var childGroup = new TrackGroup(name)
        {
            Parent = this
        };

        lock (_lock)
        {
            _childGroups.Add(childGroup);
        }

        ChildGroupAdded?.Invoke(this, childGroup);
        return childGroup;
    }

    /// <summary>
    /// Adds an existing group as a child of this group.
    /// </summary>
    /// <param name="group">The group to add as a child.</param>
    /// <exception cref="ArgumentNullException">Thrown if group is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if adding would create a circular reference.</exception>
    public void AddChildGroup(TrackGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);

        // Check for circular reference
        if (IsDescendantOf(group))
        {
            throw new InvalidOperationException("Cannot add a group as a child of its descendant (circular reference).");
        }

        if (group == this)
        {
            throw new InvalidOperationException("Cannot add a group as a child of itself.");
        }

        lock (_lock)
        {
            if (!_childGroups.Contains(group))
            {
                // Remove from previous parent if any
                group.Parent?.RemoveChildGroup(group);

                group.Parent = this;
                _childGroups.Add(group);
            }
        }

        ChildGroupAdded?.Invoke(this, group);
    }

    /// <summary>
    /// Removes a child group from this group.
    /// </summary>
    /// <param name="group">The group to remove.</param>
    /// <returns>True if the group was removed, false if it was not a child.</returns>
    public bool RemoveChildGroup(TrackGroup group)
    {
        if (group == null)
            return false;

        bool removed;

        lock (_lock)
        {
            removed = _childGroups.Remove(group);
            if (removed)
            {
                group.Parent = null;
            }
        }

        if (removed)
        {
            ChildGroupRemoved?.Invoke(this, group);
        }

        return removed;
    }

    /// <summary>
    /// Gets a read-only list of child groups.
    /// </summary>
    /// <returns>A list of child groups.</returns>
    public IReadOnlyList<TrackGroup> GetChildGroups()
    {
        lock (_lock)
        {
            return _childGroups.ToList();
        }
    }

    /// <summary>
    /// Gets all tracks recursively, including tracks from all child groups.
    /// </summary>
    /// <returns>An enumerable of all tracks in this group and its descendants.</returns>
    public IEnumerable<IAudioChannel> GetAllTracksRecursive()
    {
        lock (_lock)
        {
            // Yield direct tracks
            foreach (var track in _genericChannels)
            {
                yield return track;
            }

            // Yield tracks from child groups recursively
            foreach (var childGroup in _childGroups)
            {
                foreach (var track in childGroup.GetAllTracksRecursive())
                {
                    yield return track;
                }
            }
        }
    }

    /// <summary>
    /// Gets the total count of all tracks in this group and all descendant groups.
    /// </summary>
    /// <returns>The total number of tracks.</returns>
    public int GetTotalTrackCount()
    {
        lock (_lock)
        {
            int count = _memberTrackIds.Count;

            foreach (var childGroup in _childGroups)
            {
                count += childGroup.GetTotalTrackCount();
            }

            return count;
        }
    }

    /// <summary>
    /// Gets the nesting depth of this group in the hierarchy.
    /// </summary>
    /// <returns>0 for top-level groups, 1 for first-level children, etc.</returns>
    public int GetDepth()
    {
        int depth = 0;
        var current = Parent;
        while (current != null)
        {
            depth++;
            current = current.Parent;
        }
        return depth;
    }

    /// <summary>
    /// Checks if this group is a descendant of the specified group.
    /// </summary>
    /// <param name="potentialAncestor">The group to check as a potential ancestor.</param>
    /// <returns>True if this group is a descendant of the specified group.</returns>
    private bool IsDescendantOf(TrackGroup potentialAncestor)
    {
        var current = Parent;
        while (current != null)
        {
            if (current == potentialAncestor)
                return true;
            current = current.Parent;
        }
        return false;
    }

    #endregion

    #region Effective State Methods

    /// <summary>
    /// Checks if this group or any of its parent groups is muted.
    /// </summary>
    /// <returns>True if this group or any ancestor is muted.</returns>
    public bool IsEffectivelyMuted()
    {
        if (Mute)
            return true;

        return Parent?.IsEffectivelyMuted() ?? false;
    }

    /// <summary>
    /// Checks if this group or any of its parent groups is soloed.
    /// </summary>
    /// <returns>True if this group or any ancestor is soloed.</returns>
    public bool IsEffectivelySoloed()
    {
        if (Solo)
            return true;

        return Parent?.IsEffectivelySoloed() ?? false;
    }

    /// <summary>
    /// Gets the effective volume considering all parent group volumes.
    /// </summary>
    /// <returns>The combined volume from this group and all ancestors.</returns>
    public float GetEffectiveVolume()
    {
        float effectiveVolume = Volume;

        if (Parent != null)
        {
            effectiveVolume *= Parent.GetEffectiveVolume();
        }

        return Math.Clamp(effectiveVolume, 0f, 2f);
    }

    /// <summary>
    /// Applies the group settings (volume, mute, solo) to all child tracks.
    /// This method propagates settings through the hierarchy.
    /// </summary>
    public void ApplyGroupSettings()
    {
        lock (_lock)
        {
            // Apply to direct tracks
            foreach (var channel in _memberChannels)
            {
                if (IsEffectivelyMuted())
                    channel.Mute = true;
                if (IsEffectivelySoloed())
                    channel.Solo = true;
            }

            foreach (var track in _genericChannels)
            {
                if (IsEffectivelyMuted())
                    track.Mute = true;
                if (IsEffectivelySoloed())
                    track.Solo = true;
            }

            // Apply to child groups
            foreach (var childGroup in _childGroups)
            {
                childGroup.ApplyGroupSettings();
            }
        }

        GroupSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when a track is added to this group.
    /// </summary>
    public event EventHandler<TrackAddedEventArgs>? TrackAdded;

    /// <summary>
    /// Event raised when a track is removed from this group.
    /// </summary>
    public event EventHandler<TrackRemovedEventArgs>? TrackRemoved;

    /// <summary>
    /// Event raised when group settings change (volume, mute, solo, collapsed).
    /// </summary>
    public event EventHandler? GroupSettingsChanged;

    /// <summary>
    /// Event raised when a child group is added.
    /// </summary>
    public event EventHandler<TrackGroup>? ChildGroupAdded;

    /// <summary>
    /// Event raised when a child group is removed.
    /// </summary>
    public event EventHandler<TrackGroup>? ChildGroupRemoved;

    #endregion

    /// <summary>
    /// Applies the group volume to all member channels.
    /// </summary>
    private void ApplyVolumeToMembers()
    {
        // Note: Group volume is typically handled by the mixer
        // by multiplying individual channel volumes by group volume.
        // The actual application depends on the mixer implementation.
    }

    /// <summary>
    /// Applies the group mute state to all member channels.
    /// </summary>
    private void ApplyMuteToMembers()
    {
        lock (_lock)
        {
            foreach (var channel in _memberChannels)
            {
                // When group is muted, mute all members
                // When group is unmuted, unmute all members (unless individually muted)
                channel.Mute = _mute;
            }
        }
    }

    /// <summary>
    /// Applies the group solo state to all member channels.
    /// </summary>
    private void ApplySoloToMembers()
    {
        lock (_lock)
        {
            foreach (var channel in _memberChannels)
            {
                channel.Solo = _solo;
            }
        }
    }

    /// <summary>
    /// Raises the PropertyChanged event.
    /// </summary>
    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, propertyName);
    }

    /// <summary>
    /// Creates a string representation of this group.
    /// </summary>
    public override string ToString()
    {
        string foldState = _isFolded ? " [Folded]" : "";
        string muteState = _mute ? " [M]" : "";
        string soloState = _solo ? " [S]" : "";
        return $"{Name} ({Count} tracks){muteState}{soloState}{foldState}";
    }

    /// <summary>
    /// Disposes of all resources used by this track group.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_lock)
        {
            _memberTrackIds.Clear();
            _memberChannels.Clear();
            _genericChannels.Clear();
            _childGroups.Clear();
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}


/// <summary>
/// Event arguments for group membership changes.
/// </summary>
public class GroupMembershipChangedEventArgs : EventArgs
{
    /// <summary>
    /// Creates new membership changed event arguments.
    /// </summary>
    public GroupMembershipChangedEventArgs(string trackId, bool added)
    {
        TrackId = trackId;
        Added = added;
    }

    /// <summary>
    /// The track ID that was added or removed.
    /// </summary>
    public string TrackId { get; }

    /// <summary>
    /// True if the track was added, false if removed.
    /// </summary>
    public bool Added { get; }
}


/// <summary>
/// Event arguments for when a track is added to a group.
/// </summary>
public class TrackAddedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the track that was added.
    /// </summary>
    public IAudioChannel Track { get; }

    /// <summary>
    /// Creates new track added event arguments.
    /// </summary>
    /// <param name="track">The track that was added.</param>
    public TrackAddedEventArgs(IAudioChannel track)
    {
        Track = track ?? throw new ArgumentNullException(nameof(track));
    }
}


/// <summary>
/// Event arguments for when a track is removed from a group.
/// </summary>
public class TrackRemovedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the track that was removed.
    /// </summary>
    public IAudioChannel Track { get; }

    /// <summary>
    /// Creates new track removed event arguments.
    /// </summary>
    /// <param name="track">The track that was removed.</param>
    public TrackRemovedEventArgs(IAudioChannel track)
    {
        Track = track ?? throw new ArgumentNullException(nameof(track));
    }
}


/// <summary>
/// Manager for track groups.
/// </summary>
public class TrackGroupManager
{
    private readonly object _lock = new();
    private readonly Dictionary<Guid, TrackGroup> _groups;
    private readonly Dictionary<string, Guid> _trackToGroup; // Track ID to Group ID mapping

    /// <summary>
    /// Event raised when a group is created.
    /// </summary>
    public event EventHandler<TrackGroup>? GroupCreated;

    /// <summary>
    /// Event raised when a group is removed.
    /// </summary>
    public event EventHandler<TrackGroup>? GroupRemoved;

    /// <summary>
    /// Creates a new track group manager.
    /// </summary>
    public TrackGroupManager()
    {
        _groups = new Dictionary<Guid, TrackGroup>();
        _trackToGroup = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets all track groups.
    /// </summary>
    public IReadOnlyList<TrackGroup> Groups
    {
        get
        {
            lock (_lock)
            {
                return _groups.Values.ToList();
            }
        }
    }

    /// <summary>
    /// Gets the count of track groups.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _groups.Count;
            }
        }
    }

    /// <summary>
    /// Creates a new track group.
    /// </summary>
    /// <param name="name">The name of the group.</param>
    /// <param name="color">Optional display color (as ARGB int).</param>
    /// <returns>The created track group.</returns>
    public TrackGroup CreateGroup(string name, int? color = null)
    {
        var group = color.HasValue
            ? new TrackGroup(name, color.Value)
            : new TrackGroup(name);

        // Subscribe to membership changes
        group.MembershipChanged += OnGroupMembershipChanged;

        lock (_lock)
        {
            _groups[group.GroupId] = group;
        }

        GroupCreated?.Invoke(this, group);
        return group;
    }

    /// <summary>
    /// Removes a track group.
    /// </summary>
    /// <param name="groupId">The group ID to remove.</param>
    /// <returns>True if removed, false if not found.</returns>
    public bool RemoveGroup(Guid groupId)
    {
        TrackGroup? group;

        lock (_lock)
        {
            if (!_groups.TryGetValue(groupId, out group))
            {
                return false;
            }

            // Remove track mappings
            foreach (var trackId in group.MemberTrackIds)
            {
                _trackToGroup.Remove(trackId);
            }

            group.MembershipChanged -= OnGroupMembershipChanged;
            _groups.Remove(groupId);
        }

        GroupRemoved?.Invoke(this, group);
        return true;
    }

    /// <summary>
    /// Gets a track group by ID.
    /// </summary>
    /// <param name="groupId">The group ID.</param>
    /// <returns>The track group or null if not found.</returns>
    public TrackGroup? GetGroup(Guid groupId)
    {
        lock (_lock)
        {
            return _groups.TryGetValue(groupId, out var group) ? group : null;
        }
    }

    /// <summary>
    /// Gets a track group by name.
    /// </summary>
    /// <param name="name">The group name.</param>
    /// <returns>The track group or null if not found.</returns>
    public TrackGroup? GetGroupByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        lock (_lock)
        {
            return _groups.Values.FirstOrDefault(
                g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Gets the group that a track belongs to.
    /// </summary>
    /// <param name="trackId">The track identifier.</param>
    /// <returns>The track group or null if not in any group.</returns>
    public TrackGroup? GetGroupForTrack(string trackId)
    {
        if (string.IsNullOrWhiteSpace(trackId))
            return null;

        lock (_lock)
        {
            if (_trackToGroup.TryGetValue(trackId, out var groupId))
            {
                return _groups.TryGetValue(groupId, out var group) ? group : null;
            }
            return null;
        }
    }

    /// <summary>
    /// Adds a track to a group.
    /// </summary>
    /// <param name="groupId">The group ID.</param>
    /// <param name="trackId">The track identifier.</param>
    /// <param name="channel">Optional audio channel.</param>
    /// <returns>True if added, false if group not found or track already in a group.</returns>
    public bool AddTrackToGroup(Guid groupId, string trackId, AudioChannel? channel = null)
    {
        lock (_lock)
        {
            if (!_groups.TryGetValue(groupId, out var group))
            {
                return false;
            }

            // Check if track is already in another group
            if (_trackToGroup.ContainsKey(trackId))
            {
                return false;
            }

            if (group.AddTrack(trackId, channel))
            {
                _trackToGroup[trackId] = groupId;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Removes a track from its group.
    /// </summary>
    /// <param name="trackId">The track identifier.</param>
    /// <returns>True if removed, false if not in any group.</returns>
    public bool RemoveTrackFromGroup(string trackId)
    {
        lock (_lock)
        {
            if (!_trackToGroup.TryGetValue(trackId, out var groupId))
            {
                return false;
            }

            if (_groups.TryGetValue(groupId, out var group))
            {
                group.RemoveTrack(trackId);
            }

            _trackToGroup.Remove(trackId);
            return true;
        }
    }

    /// <summary>
    /// Moves a track from one group to another.
    /// </summary>
    /// <param name="trackId">The track identifier.</param>
    /// <param name="targetGroupId">The target group ID.</param>
    /// <param name="channel">Optional audio channel.</param>
    /// <returns>True if moved successfully.</returns>
    public bool MoveTrackToGroup(string trackId, Guid targetGroupId, AudioChannel? channel = null)
    {
        lock (_lock)
        {
            // Remove from current group if any
            if (_trackToGroup.TryGetValue(trackId, out var currentGroupId))
            {
                if (_groups.TryGetValue(currentGroupId, out var currentGroup))
                {
                    currentGroup.RemoveTrack(trackId);
                }
                _trackToGroup.Remove(trackId);
            }

            // Add to target group
            if (_groups.TryGetValue(targetGroupId, out var targetGroup))
            {
                if (targetGroup.AddTrack(trackId, channel))
                {
                    _trackToGroup[trackId] = targetGroupId;
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Folds all groups.
    /// </summary>
    public void FoldAll()
    {
        lock (_lock)
        {
            foreach (var group in _groups.Values)
            {
                group.IsFolded = true;
            }
        }
    }

    /// <summary>
    /// Unfolds all groups.
    /// </summary>
    public void UnfoldAll()
    {
        lock (_lock)
        {
            foreach (var group in _groups.Values)
            {
                group.IsFolded = false;
            }
        }
    }

    /// <summary>
    /// Clears all groups.
    /// </summary>
    public void Clear()
    {
        List<TrackGroup> removedGroups;

        lock (_lock)
        {
            removedGroups = _groups.Values.ToList();

            foreach (var group in removedGroups)
            {
                group.MembershipChanged -= OnGroupMembershipChanged;
            }

            _groups.Clear();
            _trackToGroup.Clear();
        }

        foreach (var group in removedGroups)
        {
            GroupRemoved?.Invoke(this, group);
        }
    }

    /// <summary>
    /// Handles membership changes in groups.
    /// </summary>
    private void OnGroupMembershipChanged(object? sender, GroupMembershipChangedEventArgs e)
    {
        if (sender is not TrackGroup group)
            return;

        lock (_lock)
        {
            if (e.Added)
            {
                _trackToGroup[e.TrackId] = group.GroupId;
            }
            else
            {
                _trackToGroup.Remove(e.TrackId);
            }
        }
    }
}
