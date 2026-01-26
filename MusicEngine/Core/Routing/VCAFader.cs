// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio/MIDI routing component.

namespace MusicEngine.Core.Routing;

/// <summary>
/// A virtual fader that applies gain control without audio routing.
/// VCA faders can be linked to a VCA group for coordinated volume control.
/// Unlike subgroups, VCA faders don't route audio through a bus - they simply
/// apply gain scaling to the tracks they're assigned to.
/// </summary>
public class VCAFader
{
    private readonly object _lock = new();
    private float _localVolume = 1.0f;
    private VCAGroup? _group;

    /// <summary>
    /// Creates a new VCA fader.
    /// </summary>
    /// <param name="name">The name of the fader.</param>
    public VCAFader(string name)
    {
        Id = Guid.NewGuid();
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// Creates a new VCA fader with the specified ID.
    /// </summary>
    /// <param name="id">The unique identifier.</param>
    /// <param name="name">The name of the fader.</param>
    public VCAFader(Guid id, string name)
    {
        Id = id;
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// Gets the unique identifier for this fader.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets or sets the name of this fader.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the local volume of this fader (0.0 - 2.0).
    /// This is the fader's own volume level before group scaling is applied.
    /// </summary>
    public float LocalVolume
    {
        get
        {
            lock (_lock)
            {
                return _localVolume;
            }
        }
        set
        {
            float oldEffective;
            float newEffective;

            lock (_lock)
            {
                oldEffective = EffectiveVolumeInternal;
                _localVolume = Math.Clamp(value, 0f, 2f);
                newEffective = EffectiveVolumeInternal;
            }

            if (Math.Abs(oldEffective - newEffective) > float.Epsilon)
            {
                VolumeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Gets or sets the local volume in decibels (-inf to +6 dB).
    /// </summary>
    public float LocalVolumeDb
    {
        get
        {
            float vol = LocalVolume;
            if (vol <= 0f) return -100f;
            return 20f * MathF.Log10(vol);
        }
        set
        {
            float db = Math.Clamp(value, -100f, 6f);
            LocalVolume = db <= -100f ? 0f : MathF.Pow(10f, db / 20f);
        }
    }

    /// <summary>
    /// Gets the VCA group this fader is linked to, if any.
    /// </summary>
    public VCAGroup? Group
    {
        get
        {
            lock (_lock)
            {
                return _group;
            }
        }
        internal set
        {
            float oldEffective;
            float newEffective;

            lock (_lock)
            {
                if (_group == value)
                    return;

                oldEffective = EffectiveVolumeInternal;

                // Unsubscribe from old group events
                if (_group != null)
                {
                    _group.VolumeChanged -= OnGroupVolumeChanged;
                }

                _group = value;

                // Subscribe to new group events
                if (_group != null)
                {
                    _group.VolumeChanged += OnGroupVolumeChanged;
                }

                newEffective = EffectiveVolumeInternal;
            }

            if (Math.Abs(oldEffective - newEffective) > float.Epsilon)
            {
                VolumeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Gets the effective volume after applying the group's volume.
    /// This is LocalVolume * Group.Volume (or just LocalVolume if not in a group).
    /// </summary>
    public float EffectiveVolume
    {
        get
        {
            lock (_lock)
            {
                return EffectiveVolumeInternal;
            }
        }
    }

    /// <summary>
    /// Gets the effective volume in decibels.
    /// </summary>
    public float EffectiveVolumeDb
    {
        get
        {
            float vol = EffectiveVolume;
            if (vol <= 0f) return -100f;
            return 20f * MathF.Log10(vol);
        }
    }

    // Internal helper to avoid lock re-entry
    private float EffectiveVolumeInternal =>
        _localVolume * (_group?.Volume ?? 1.0f) * ((_group?.Mute ?? false) ? 0f : 1f);

    /// <summary>
    /// Gets or sets arbitrary user data associated with this fader.
    /// </summary>
    public object? Tag { get; set; }

    /// <summary>
    /// Event raised when the effective volume changes.
    /// This can be due to local volume changes or group volume changes.
    /// </summary>
    public event EventHandler? VolumeChanged;

    /// <summary>
    /// Handles volume changes from the linked group.
    /// </summary>
    private void OnGroupVolumeChanged(object? sender, EventArgs e)
    {
        VolumeChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Creates a string representation of this fader.
    /// </summary>
    public override string ToString()
    {
        string groupInfo = _group != null ? $" (Group: {_group.Name})" : "";
        return $"VCA: {Name} [{EffectiveVolumeDb:F1} dB]{groupInfo}";
    }
}
