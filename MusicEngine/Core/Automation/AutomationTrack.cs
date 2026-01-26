// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System.Collections;

namespace MusicEngine.Core.Automation;

/// <summary>
/// Represents a collection of automation lanes for a single track or object.
/// Acts as a container and manager for multiple AutomationLanes.
/// </summary>
public class AutomationTrack : IEnumerable<AutomationLane>
{
    private static long _nextId;
    private readonly long _id;
    private readonly object _lock = new();
    private readonly List<AutomationLane> _lanes = [];
    private readonly Dictionary<string, AutomationLane> _lanesByParameter = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the unique identifier for this track.
    /// </summary>
    public long Id => _id;

    /// <summary>
    /// Gets or sets the name of this automation track.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target object ID that this track automates.
    /// </summary>
    public string TargetId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target object.
    /// </summary>
    public object? TargetObject { get; private set; }

    /// <summary>
    /// Gets or sets whether this track is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether this track is muted.
    /// </summary>
    public bool IsMuted { get; set; }

    /// <summary>
    /// Gets or sets whether this track is soloed.
    /// </summary>
    public bool IsSoloed { get; set; }

    /// <summary>
    /// Gets or sets whether this track is expanded in the UI.
    /// </summary>
    public bool IsExpanded { get; set; } = true;

    /// <summary>
    /// Gets or sets the display color for this track.
    /// </summary>
    public string Color { get; set; } = "#4B6EAF";

    /// <summary>
    /// Gets the number of lanes in this track.
    /// </summary>
    public int LaneCount
    {
        get
        {
            lock (_lock)
            {
                return _lanes.Count;
            }
        }
    }

    /// <summary>
    /// Gets all lanes in this track.
    /// </summary>
    public IReadOnlyList<AutomationLane> Lanes
    {
        get
        {
            lock (_lock)
            {
                return _lanes.ToList().AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Gets or sets the time mode for all lanes.
    /// </summary>
    public AutomationTimeMode TimeMode { get; set; } = AutomationTimeMode.Beats;

    /// <summary>
    /// Fired when a lane is added.
    /// </summary>
    public event EventHandler<AutomationLane>? LaneAdded;

    /// <summary>
    /// Fired when a lane is removed.
    /// </summary>
    public event EventHandler<AutomationLane>? LaneRemoved;

    /// <summary>
    /// Fired when the track configuration changes.
    /// </summary>
    public event EventHandler? TrackChanged;

    /// <summary>
    /// Creates a new automation track.
    /// </summary>
    public AutomationTrack()
    {
        _id = Interlocked.Increment(ref _nextId);
    }

    /// <summary>
    /// Creates a new automation track for a specific target.
    /// </summary>
    /// <param name="name">The track name.</param>
    /// <param name="targetId">The target object ID.</param>
    public AutomationTrack(string name, string targetId) : this()
    {
        Name = name;
        TargetId = targetId;
    }

    /// <summary>
    /// Creates a new automation track for an IAutomatable target.
    /// </summary>
    /// <param name="target">The automatable target.</param>
    public AutomationTrack(IAutomatable target) : this()
    {
        SetTarget(target);
    }

    /// <summary>
    /// Sets the target object for this track.
    /// </summary>
    /// <param name="target">The IAutomatable target.</param>
    public void SetTarget(IAutomatable target)
    {
        lock (_lock)
        {
            TargetObject = target;
            TargetId = target.AutomationId;
            Name = target.DisplayName;

            // Update all lanes with the new target
            foreach (var lane in _lanes)
            {
                if (target.AutomatableParameters.Contains(lane.ParameterName))
                {
                    lane.SetTarget(target, lane.ParameterName);
                }
            }
        }
        OnTrackChanged();
    }

    /// <summary>
    /// Sets the target object for this track (generic).
    /// </summary>
    /// <param name="target">The target object.</param>
    public void SetTarget(object target)
    {
        if (target is IAutomatable automatable)
        {
            SetTarget(automatable);
            return;
        }

        lock (_lock)
        {
            TargetObject = target;
            TargetId = target.GetType().Name + "_" + target.GetHashCode();
            Name = target.GetType().Name;

            foreach (var lane in _lanes)
            {
                lane.SetTarget(target, lane.ParameterName);
            }
        }
        OnTrackChanged();
    }

    /// <summary>
    /// Adds a lane to this track.
    /// </summary>
    /// <param name="lane">The lane to add.</param>
    public void AddLane(AutomationLane lane)
    {
        lock (_lock)
        {
            if (!_lanes.Contains(lane))
            {
                _lanes.Add(lane);
                lane.TimeMode = TimeMode;

                if (!string.IsNullOrEmpty(lane.ParameterName))
                {
                    _lanesByParameter[lane.ParameterName] = lane;
                }

                lane.LaneChanged += OnLaneLaneChanged;
            }
        }
        LaneAdded?.Invoke(this, lane);
        OnTrackChanged();
    }

    /// <summary>
    /// Creates and adds a new lane for the specified parameter.
    /// </summary>
    /// <param name="parameterName">The parameter name.</param>
    /// <returns>The created lane.</returns>
    public AutomationLane AddLane(string parameterName)
    {
        var lane = new AutomationLane
        {
            ParameterName = parameterName,
            Name = $"{Name}.{parameterName}",
            TargetId = TargetId,
            TimeMode = TimeMode
        };

        if (TargetObject != null)
        {
            if (TargetObject is IAutomatable automatable)
            {
                lane.SetTarget(automatable, parameterName);
            }
            else
            {
                lane.SetTarget(TargetObject, parameterName);
            }
        }

        AddLane(lane);
        return lane;
    }

    /// <summary>
    /// Removes a lane from this track.
    /// </summary>
    /// <param name="lane">The lane to remove.</param>
    /// <returns>True if removed, false otherwise.</returns>
    public bool RemoveLane(AutomationLane lane)
    {
        bool removed;
        lock (_lock)
        {
            removed = _lanes.Remove(lane);
            if (removed)
            {
                lane.LaneChanged -= OnLaneLaneChanged;

                if (!string.IsNullOrEmpty(lane.ParameterName) &&
                    _lanesByParameter.TryGetValue(lane.ParameterName, out var stored) &&
                    stored == lane)
                {
                    _lanesByParameter.Remove(lane.ParameterName);
                }
            }
        }

        if (removed)
        {
            LaneRemoved?.Invoke(this, lane);
            OnTrackChanged();
        }
        return removed;
    }

    /// <summary>
    /// Gets a lane by parameter name.
    /// </summary>
    /// <param name="parameterName">The parameter name.</param>
    /// <returns>The lane, or null if not found.</returns>
    public AutomationLane? GetLane(string parameterName)
    {
        lock (_lock)
        {
            return _lanesByParameter.GetValueOrDefault(parameterName);
        }
    }

    /// <summary>
    /// Gets a lane by index.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns>The lane, or null if index is out of range.</returns>
    public AutomationLane? GetLane(int index)
    {
        lock (_lock)
        {
            if (index >= 0 && index < _lanes.Count)
            {
                return _lanes[index];
            }
            return null;
        }
    }

    /// <summary>
    /// Gets or creates a lane for the specified parameter.
    /// </summary>
    /// <param name="parameterName">The parameter name.</param>
    /// <returns>The existing or newly created lane.</returns>
    public AutomationLane GetOrCreateLane(string parameterName)
    {
        lock (_lock)
        {
            if (_lanesByParameter.TryGetValue(parameterName, out var lane))
            {
                return lane;
            }
        }
        return AddLane(parameterName);
    }

    /// <summary>
    /// Checks if a lane exists for the specified parameter.
    /// </summary>
    /// <param name="parameterName">The parameter name.</param>
    /// <returns>True if a lane exists, false otherwise.</returns>
    public bool HasLane(string parameterName)
    {
        lock (_lock)
        {
            return _lanesByParameter.ContainsKey(parameterName);
        }
    }

    /// <summary>
    /// Clears all lanes from this track.
    /// </summary>
    public void ClearLanes()
    {
        List<AutomationLane> removed;
        lock (_lock)
        {
            removed = [.. _lanes];
            foreach (var lane in _lanes)
            {
                lane.LaneChanged -= OnLaneLaneChanged;
            }
            _lanes.Clear();
            _lanesByParameter.Clear();
        }

        foreach (var lane in removed)
        {
            LaneRemoved?.Invoke(this, lane);
        }
        OnTrackChanged();
    }

    /// <summary>
    /// Applies all lane values at the specified time.
    /// </summary>
    /// <param name="time">The current time.</param>
    /// <param name="hasSoloedLanes">Whether any lanes are soloed.</param>
    public void Apply(double time, bool hasSoloedLanes = false)
    {
        if (!Enabled || IsMuted)
            return;

        lock (_lock)
        {
            foreach (var lane in _lanes)
            {
                // Skip muted lanes or non-soloed lanes when there are soloed lanes
                if (lane.IsMuted)
                    continue;

                if (hasSoloedLanes && !lane.IsSoloed)
                    continue;

                lane.Apply(time);
            }
        }
    }

    /// <summary>
    /// Resets all lanes to their default values.
    /// </summary>
    public void ResetToDefaults()
    {
        lock (_lock)
        {
            foreach (var lane in _lanes)
            {
                lane.ResetToDefault();
            }
        }
    }

    /// <summary>
    /// Gets all armed lanes (ready for recording).
    /// </summary>
    /// <returns>List of armed lanes.</returns>
    public IReadOnlyList<AutomationLane> GetArmedLanes()
    {
        lock (_lock)
        {
            return _lanes.Where(l => l.IsArmed).ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Checks if any lane is soloed.
    /// </summary>
    /// <returns>True if any lane is soloed, false otherwise.</returns>
    public bool HasSoloedLanes()
    {
        lock (_lock)
        {
            return _lanes.Any(l => l.IsSoloed);
        }
    }

    /// <summary>
    /// Moves a lane to a new index.
    /// </summary>
    /// <param name="lane">The lane to move.</param>
    /// <param name="newIndex">The new index.</param>
    /// <returns>True if moved successfully, false otherwise.</returns>
    public bool MoveLane(AutomationLane lane, int newIndex)
    {
        lock (_lock)
        {
            int currentIndex = _lanes.IndexOf(lane);
            if (currentIndex < 0 || newIndex < 0 || newIndex >= _lanes.Count)
                return false;

            _lanes.RemoveAt(currentIndex);
            _lanes.Insert(newIndex, lane);
        }
        OnTrackChanged();
        return true;
    }

    /// <summary>
    /// Creates a deep copy of this track.
    /// </summary>
    /// <returns>A new AutomationTrack with cloned lanes.</returns>
    public AutomationTrack Clone()
    {
        var clone = new AutomationTrack
        {
            Name = Name,
            TargetId = TargetId,
            Enabled = Enabled,
            IsMuted = IsMuted,
            IsSoloed = IsSoloed,
            IsExpanded = IsExpanded,
            Color = Color,
            TimeMode = TimeMode
        };

        lock (_lock)
        {
            foreach (var lane in _lanes)
            {
                clone.AddLane(lane.Clone());
            }
        }

        return clone;
    }

    private void OnLaneLaneChanged(object? sender, EventArgs e)
    {
        OnTrackChanged();
    }

    private void OnTrackChanged()
    {
        TrackChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public IEnumerator<AutomationLane> GetEnumerator()
    {
        lock (_lock)
        {
            return _lanes.ToList().GetEnumerator();
        }
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Indexer to access lanes by index.
    /// </summary>
    /// <param name="index">The lane index.</param>
    /// <returns>The lane at the specified index.</returns>
    public AutomationLane this[int index]
    {
        get
        {
            lock (_lock)
            {
                return _lanes[index];
            }
        }
    }

    /// <summary>
    /// Indexer to access lanes by parameter name.
    /// </summary>
    /// <param name="parameterName">The parameter name.</param>
    /// <returns>The lane for the specified parameter, or null if not found.</returns>
    public AutomationLane? this[string parameterName] => GetLane(parameterName);
}
