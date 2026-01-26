// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio/MIDI routing component.

namespace MusicEngine.Core.Routing;

/// <summary>
/// Manages all VCA groups and provides methods for creating, deleting,
/// and linking faders to groups. This is the central point for VCA
/// operations in the mixer.
/// </summary>
public class VCAManager
{
    private readonly object _lock = new();
    private readonly List<VCAGroup> _groups = new();
    private readonly Dictionary<Guid, VCAFader> _allFaders = new();

    /// <summary>
    /// Creates a new VCA manager.
    /// </summary>
    public VCAManager()
    {
    }

    /// <summary>
    /// Gets a read-only list of all VCA groups.
    /// </summary>
    public IReadOnlyList<VCAGroup> Groups
    {
        get
        {
            lock (_lock)
            {
                return _groups.ToList();
            }
        }
    }

    /// <summary>
    /// Gets the number of VCA groups.
    /// </summary>
    public int GroupCount
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
    /// Gets the number of registered faders.
    /// </summary>
    public int FaderCount
    {
        get
        {
            lock (_lock)
            {
                return _allFaders.Count;
            }
        }
    }

    /// <summary>
    /// Event raised when a group is created.
    /// </summary>
    public event EventHandler<VCAGroup>? GroupCreated;

    /// <summary>
    /// Event raised when a group is deleted.
    /// </summary>
    public event EventHandler<VCAGroup>? GroupDeleted;

    /// <summary>
    /// Event raised when a fader is linked to a group.
    /// </summary>
    public event EventHandler<VCAFaderLinkedEventArgs>? FaderLinked;

    /// <summary>
    /// Event raised when a fader is unlinked from a group.
    /// </summary>
    public event EventHandler<VCAFaderLinkedEventArgs>? FaderUnlinked;

    /// <summary>
    /// Creates a new VCA group with the specified name.
    /// </summary>
    /// <param name="name">The name of the group.</param>
    /// <returns>The created VCA group.</returns>
    public VCAGroup CreateGroup(string name)
    {
        var group = new VCAGroup(name);

        lock (_lock)
        {
            _groups.Add(group);
        }

        GroupCreated?.Invoke(this, group);
        return group;
    }

    /// <summary>
    /// Creates a new VCA group with the specified name and color.
    /// </summary>
    /// <param name="name">The name of the group.</param>
    /// <param name="color">The display color (ARGB).</param>
    /// <returns>The created VCA group.</returns>
    public VCAGroup CreateGroup(string name, int color)
    {
        var group = new VCAGroup(name) { Color = color };

        lock (_lock)
        {
            _groups.Add(group);
        }

        GroupCreated?.Invoke(this, group);
        return group;
    }

    /// <summary>
    /// Deletes a VCA group and unlinks all its members.
    /// </summary>
    /// <param name="group">The group to delete.</param>
    /// <returns>True if the group was deleted, false if it wasn't found.</returns>
    public bool DeleteGroup(VCAGroup group)
    {
        if (group == null)
            return false;

        bool removed;

        lock (_lock)
        {
            removed = _groups.Remove(group);
            if (removed)
            {
                // Clear all members (this will unlink them from the group)
                group.ClearMembers();
            }
        }

        if (removed)
        {
            GroupDeleted?.Invoke(this, group);
        }

        return removed;
    }

    /// <summary>
    /// Deletes a VCA group by ID.
    /// </summary>
    /// <param name="groupId">The group ID.</param>
    /// <returns>True if the group was deleted.</returns>
    public bool DeleteGroup(Guid groupId)
    {
        var group = GetGroup(groupId);
        return group != null && DeleteGroup(group);
    }

    /// <summary>
    /// Gets a VCA group by ID.
    /// </summary>
    /// <param name="groupId">The group ID.</param>
    /// <returns>The group, or null if not found.</returns>
    public VCAGroup? GetGroup(Guid groupId)
    {
        lock (_lock)
        {
            return _groups.FirstOrDefault(g => g.Id == groupId);
        }
    }

    /// <summary>
    /// Gets a VCA group by name.
    /// </summary>
    /// <param name="name">The group name.</param>
    /// <returns>The group, or null if not found.</returns>
    public VCAGroup? GetGroupByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        lock (_lock)
        {
            return _groups.FirstOrDefault(g =>
                g.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Gets a VCA group by index.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns>The group at the index.</returns>
    public VCAGroup GetGroup(int index)
    {
        lock (_lock)
        {
            return _groups[index];
        }
    }

    /// <summary>
    /// Creates and registers a new VCA fader.
    /// </summary>
    /// <param name="name">The fader name.</param>
    /// <returns>The created fader.</returns>
    public VCAFader CreateFader(string name)
    {
        var fader = new VCAFader(name);

        lock (_lock)
        {
            _allFaders[fader.Id] = fader;
        }

        return fader;
    }

    /// <summary>
    /// Registers an existing fader with the manager.
    /// </summary>
    /// <param name="fader">The fader to register.</param>
    public void RegisterFader(VCAFader fader)
    {
        ArgumentNullException.ThrowIfNull(fader);

        lock (_lock)
        {
            _allFaders[fader.Id] = fader;
        }
    }

    /// <summary>
    /// Unregisters a fader from the manager.
    /// </summary>
    /// <param name="fader">The fader to unregister.</param>
    /// <returns>True if the fader was unregistered.</returns>
    public bool UnregisterFader(VCAFader fader)
    {
        if (fader == null)
            return false;

        lock (_lock)
        {
            // First unlink from any group
            if (fader.Group != null)
            {
                fader.Group.RemoveMember(fader);
            }

            return _allFaders.Remove(fader.Id);
        }
    }

    /// <summary>
    /// Gets a registered fader by ID.
    /// </summary>
    /// <param name="faderId">The fader ID.</param>
    /// <returns>The fader, or null if not found.</returns>
    public VCAFader? GetFader(Guid faderId)
    {
        lock (_lock)
        {
            return _allFaders.TryGetValue(faderId, out var fader) ? fader : null;
        }
    }

    /// <summary>
    /// Links a fader to a VCA group.
    /// </summary>
    /// <param name="fader">The fader to link.</param>
    /// <param name="group">The group to link to.</param>
    /// <exception cref="ArgumentNullException">Thrown if fader or group is null.</exception>
    public void LinkFaderToGroup(VCAFader fader, VCAGroup group)
    {
        ArgumentNullException.ThrowIfNull(fader);
        ArgumentNullException.ThrowIfNull(group);

        VCAGroup? oldGroup;

        lock (_lock)
        {
            // Verify group is managed by this manager
            if (!_groups.Contains(group))
            {
                throw new InvalidOperationException(
                    $"Group '{group.Name}' is not managed by this VCAManager.");
            }

            oldGroup = fader.Group;

            // Remove from old group if different
            if (oldGroup != null && oldGroup != group)
            {
                oldGroup.RemoveMember(fader);
                FaderUnlinked?.Invoke(this, new VCAFaderLinkedEventArgs(fader, oldGroup));
            }

            // Add to new group
            if (oldGroup != group)
            {
                group.AddMember(fader);
                FaderLinked?.Invoke(this, new VCAFaderLinkedEventArgs(fader, group));
            }

            // Ensure fader is registered
            _allFaders[fader.Id] = fader;
        }
    }

    /// <summary>
    /// Unlinks a fader from its current group.
    /// </summary>
    /// <param name="fader">The fader to unlink.</param>
    /// <returns>True if the fader was unlinked, false if it wasn't in a group.</returns>
    public bool UnlinkFader(VCAFader fader)
    {
        if (fader == null)
            return false;

        VCAGroup? oldGroup;

        lock (_lock)
        {
            oldGroup = fader.Group;
            if (oldGroup == null)
                return false;

            oldGroup.RemoveMember(fader);
        }

        FaderUnlinked?.Invoke(this, new VCAFaderLinkedEventArgs(fader, oldGroup));
        return true;
    }

    /// <summary>
    /// Gets all faders that are linked to a specific group.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <returns>A list of faders in the group.</returns>
    public IReadOnlyList<VCAFader> GetFadersInGroup(VCAGroup group)
    {
        if (group == null)
            return Array.Empty<VCAFader>();

        return group.Members;
    }

    /// <summary>
    /// Gets all faders that are not linked to any group.
    /// </summary>
    /// <returns>A list of unlinked faders.</returns>
    public IReadOnlyList<VCAFader> GetUnlinkedFaders()
    {
        lock (_lock)
        {
            return _allFaders.Values
                .Where(f => f.Group == null)
                .ToList();
        }
    }

    /// <summary>
    /// Gets all registered faders.
    /// </summary>
    /// <returns>A list of all faders.</returns>
    public IReadOnlyList<VCAFader> GetAllFaders()
    {
        lock (_lock)
        {
            return _allFaders.Values.ToList();
        }
    }

    /// <summary>
    /// Clears all groups and unlinks all faders.
    /// </summary>
    public void Clear()
    {
        List<VCAGroup> removedGroups;

        lock (_lock)
        {
            removedGroups = _groups.ToList();

            foreach (var group in _groups)
            {
                group.ClearMembers();
            }

            _groups.Clear();
        }

        foreach (var group in removedGroups)
        {
            GroupDeleted?.Invoke(this, group);
        }
    }

    /// <summary>
    /// Clears all groups and removes all faders from the manager.
    /// </summary>
    public void ClearAll()
    {
        Clear();

        lock (_lock)
        {
            _allFaders.Clear();
        }
    }
}

/// <summary>
/// Event arguments for VCA fader link/unlink events.
/// </summary>
public class VCAFaderLinkedEventArgs : EventArgs
{
    /// <summary>
    /// Creates new event arguments.
    /// </summary>
    /// <param name="fader">The fader that was linked or unlinked.</param>
    /// <param name="group">The group involved in the operation.</param>
    public VCAFaderLinkedEventArgs(VCAFader fader, VCAGroup group)
    {
        Fader = fader ?? throw new ArgumentNullException(nameof(fader));
        Group = group ?? throw new ArgumentNullException(nameof(group));
    }

    /// <summary>
    /// Gets the fader that was linked or unlinked.
    /// </summary>
    public VCAFader Fader { get; }

    /// <summary>
    /// Gets the group involved in the operation.
    /// </summary>
    public VCAGroup Group { get; }
}
