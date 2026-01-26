//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: VCA (Voltage Controlled Amplifier) group for controlling linked gain without audio routing.

namespace MusicEngine.Core.Routing;

/// <summary>
/// A VCA (Voltage Controlled Amplifier) group that controls linked gain without audio routing.
/// VCA groups allow multiple faders to be controlled together by a master fader,
/// without the need to route audio through a subgroup bus. This is useful for
/// controlling the overall level of related tracks while maintaining their
/// individual routing and panning.
/// </summary>
public class VCAGroup
{
    private readonly object _lock = new();
    private readonly List<VCAFader> _members = new();
    private float _volume = 1.0f;
    private bool _mute;
    private string _name;

    /// <summary>
    /// Creates a new VCA group.
    /// </summary>
    /// <param name="name">The name of the group.</param>
    public VCAGroup(string name)
    {
        Id = Guid.NewGuid();
        _name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// Creates a new VCA group with the specified ID.
    /// </summary>
    /// <param name="id">The unique identifier.</param>
    /// <param name="name">The name of the group.</param>
    public VCAGroup(Guid id, string name)
    {
        Id = id;
        _name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// Gets the unique identifier for this group.
    /// </summary>
    public Guid Id { get; }

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

            NameChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gets or sets the master volume for this group (0.0 - 2.0).
    /// This volume is multiplied with each member fader's local volume.
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

            VolumeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gets or sets the master volume in decibels (-inf to +6 dB).
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
    /// Gets or sets whether this group is muted.
    /// When muted, all member faders have an effective volume of 0.
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

            VolumeChanged?.Invoke(this, EventArgs.Empty);
            MuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gets or sets the display color for this group (as ARGB int).
    /// </summary>
    public int Color { get; set; } = unchecked((int)0xFF2196F3); // Default blue

    /// <summary>
    /// Gets or sets arbitrary user data associated with this group.
    /// </summary>
    public object? Tag { get; set; }

    /// <summary>
    /// Gets a read-only list of member faders in this group.
    /// </summary>
    public IReadOnlyList<VCAFader> Members
    {
        get
        {
            lock (_lock)
            {
                return _members.ToList();
            }
        }
    }

    /// <summary>
    /// Gets the number of members in this group.
    /// </summary>
    public int MemberCount
    {
        get
        {
            lock (_lock)
            {
                return _members.Count;
            }
        }
    }

    /// <summary>
    /// Event raised when the group volume or mute state changes.
    /// Member faders listen to this to update their effective volume.
    /// </summary>
    public event EventHandler? VolumeChanged;

    /// <summary>
    /// Event raised when the mute state changes.
    /// </summary>
    public event EventHandler? MuteChanged;

    /// <summary>
    /// Event raised when the group name changes.
    /// </summary>
    public event EventHandler? NameChanged;

    /// <summary>
    /// Event raised when a fader is added to the group.
    /// </summary>
    public event EventHandler<VCAFader>? FaderAdded;

    /// <summary>
    /// Event raised when a fader is removed from the group.
    /// </summary>
    public event EventHandler<VCAFader>? FaderRemoved;

    /// <summary>
    /// Applies the current volume to all member faders.
    /// This triggers volume change notifications on all members.
    /// </summary>
    public void ApplyGain()
    {
        VolumeChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Adds a fader to this group.
    /// </summary>
    /// <param name="fader">The fader to add.</param>
    /// <exception cref="ArgumentNullException">Thrown if fader is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if fader is already in another group.</exception>
    public void AddMember(VCAFader fader)
    {
        ArgumentNullException.ThrowIfNull(fader);

        lock (_lock)
        {
            if (fader.Group != null && fader.Group != this)
            {
                throw new InvalidOperationException(
                    $"Fader '{fader.Name}' is already a member of group '{fader.Group.Name}'. " +
                    "Remove it from that group first.");
            }

            if (_members.Contains(fader))
                return;

            _members.Add(fader);
            fader.Group = this;
        }

        FaderAdded?.Invoke(this, fader);
    }

    /// <summary>
    /// Removes a fader from this group.
    /// </summary>
    /// <param name="fader">The fader to remove.</param>
    /// <returns>True if the fader was removed, false if it wasn't a member.</returns>
    public bool RemoveMember(VCAFader fader)
    {
        if (fader == null)
            return false;

        bool removed;

        lock (_lock)
        {
            removed = _members.Remove(fader);
            if (removed)
            {
                fader.Group = null;
            }
        }

        if (removed)
        {
            FaderRemoved?.Invoke(this, fader);
        }

        return removed;
    }

    /// <summary>
    /// Checks if a fader is a member of this group.
    /// </summary>
    /// <param name="fader">The fader to check.</param>
    /// <returns>True if the fader is a member.</returns>
    public bool Contains(VCAFader fader)
    {
        if (fader == null)
            return false;

        lock (_lock)
        {
            return _members.Contains(fader);
        }
    }

    /// <summary>
    /// Clears all members from this group.
    /// </summary>
    public void ClearMembers()
    {
        List<VCAFader> removedFaders;

        lock (_lock)
        {
            removedFaders = _members.ToList();

            foreach (var fader in _members)
            {
                fader.Group = null;
            }

            _members.Clear();
        }

        foreach (var fader in removedFaders)
        {
            FaderRemoved?.Invoke(this, fader);
        }
    }

    /// <summary>
    /// Gets the fader at the specified index.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns>The fader at the index.</returns>
    public VCAFader GetMember(int index)
    {
        lock (_lock)
        {
            return _members[index];
        }
    }

    /// <summary>
    /// Finds a member fader by name.
    /// </summary>
    /// <param name="name">The fader name.</param>
    /// <returns>The fader, or null if not found.</returns>
    public VCAFader? FindMember(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        lock (_lock)
        {
            return _members.FirstOrDefault(f =>
                f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Finds a member fader by ID.
    /// </summary>
    /// <param name="id">The fader ID.</param>
    /// <returns>The fader, or null if not found.</returns>
    public VCAFader? FindMember(Guid id)
    {
        lock (_lock)
        {
            return _members.FirstOrDefault(f => f.Id == id);
        }
    }

    /// <summary>
    /// Creates a string representation of this group.
    /// </summary>
    public override string ToString()
    {
        string muteState = _mute ? " [M]" : "";
        return $"VCA Group: {Name} [{VolumeDb:F1} dB] ({MemberCount} members){muteState}";
    }
}
