//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Manages a collection of markers for navigation and arrangement in a track.

using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicEngine.Core;

/// <summary>
/// Event arguments for marker-related events.
/// </summary>
public class MarkerEventArgs : EventArgs
{
    /// <summary>The marker associated with this event.</summary>
    public Marker Marker { get; }

    /// <summary>The previous position (for move events).</summary>
    public double? PreviousPosition { get; }

    public MarkerEventArgs(Marker marker, double? previousPosition = null)
    {
        Marker = marker;
        PreviousPosition = previousPosition;
    }
}

/// <summary>
/// Event arguments for jump-to-marker events.
/// </summary>
public class MarkerJumpEventArgs : EventArgs
{
    /// <summary>The target marker.</summary>
    public Marker Marker { get; }

    /// <summary>The position to jump to (in beats).</summary>
    public double Position { get; }

    public MarkerJumpEventArgs(Marker marker)
    {
        Marker = marker;
        Position = marker.Position;
    }
}

/// <summary>
/// Manages a collection of markers for a track.
/// Provides functionality for adding, removing, and navigating between markers.
/// </summary>
public class MarkerTrack
{
    private readonly List<Marker> _markers = [];
    private readonly object _lock = new();

    /// <summary>Gets all markers in the track, sorted by position.</summary>
    public IReadOnlyList<Marker> Markers
    {
        get
        {
            lock (_lock)
            {
                return _markers.OrderBy(m => m.Position).ToList().AsReadOnly();
            }
        }
    }

    /// <summary>Gets the number of markers in the track.</summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _markers.Count;
            }
        }
    }

    /// <summary>Event raised when a marker is added.</summary>
    public event EventHandler<MarkerEventArgs>? MarkerAdded;

    /// <summary>Event raised when a marker is removed.</summary>
    public event EventHandler<MarkerEventArgs>? MarkerRemoved;

    /// <summary>Event raised when a marker is moved.</summary>
    public event EventHandler<MarkerEventArgs>? MarkerMoved;

    /// <summary>Event raised when jumping to a marker.</summary>
    public event EventHandler<MarkerJumpEventArgs>? JumpRequested;

    /// <summary>Event raised when the marker collection changes.</summary>
    public event EventHandler? MarkersChanged;

    /// <summary>
    /// Adds a marker to the track.
    /// </summary>
    /// <param name="marker">The marker to add.</param>
    /// <returns>True if added successfully, false if marker already exists.</returns>
    public bool AddMarker(Marker marker)
    {
        ArgumentNullException.ThrowIfNull(marker);

        lock (_lock)
        {
            if (_markers.Any(m => m.Id == marker.Id))
                return false;

            _markers.Add(marker);
        }

        MarkerAdded?.Invoke(this, new MarkerEventArgs(marker));
        MarkersChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// Creates and adds a new marker at the specified position.
    /// </summary>
    /// <param name="position">Position in beats.</param>
    /// <param name="name">Marker name.</param>
    /// <param name="type">Marker type.</param>
    /// <returns>The created marker.</returns>
    public Marker AddMarker(double position, string name = "Marker", MarkerType type = MarkerType.Cue)
    {
        var marker = new Marker(position, name, type);
        AddMarker(marker);
        return marker;
    }

    /// <summary>
    /// Removes a marker from the track.
    /// </summary>
    /// <param name="marker">The marker to remove.</param>
    /// <returns>True if removed successfully, false if not found.</returns>
    public bool RemoveMarker(Marker marker)
    {
        ArgumentNullException.ThrowIfNull(marker);

        if (marker.IsLocked)
            return false;

        bool removed;
        lock (_lock)
        {
            removed = _markers.Remove(marker);
        }

        if (removed)
        {
            MarkerRemoved?.Invoke(this, new MarkerEventArgs(marker));
            MarkersChanged?.Invoke(this, EventArgs.Empty);
        }

        return removed;
    }

    /// <summary>
    /// Removes a marker by its ID.
    /// </summary>
    /// <param name="markerId">The marker ID to remove.</param>
    /// <returns>True if removed successfully, false if not found.</returns>
    public bool RemoveMarker(Guid markerId)
    {
        Marker? marker;
        lock (_lock)
        {
            marker = _markers.FirstOrDefault(m => m.Id == markerId);
        }

        return marker != null && RemoveMarker(marker);
    }

    /// <summary>
    /// Removes all markers at the specified position.
    /// </summary>
    /// <param name="position">Position in beats.</param>
    /// <param name="tolerance">Position tolerance for matching.</param>
    /// <returns>Number of markers removed.</returns>
    public int RemoveMarkersAt(double position, double tolerance = 0.001)
    {
        List<Marker> toRemove;
        lock (_lock)
        {
            toRemove = _markers
                .Where(m => !m.IsLocked && Math.Abs(m.Position - position) <= tolerance)
                .ToList();
        }

        foreach (var marker in toRemove)
        {
            RemoveMarker(marker);
        }

        return toRemove.Count;
    }

    /// <summary>
    /// Gets markers within a specified range.
    /// </summary>
    /// <param name="startPosition">Start position in beats (inclusive).</param>
    /// <param name="endPosition">End position in beats (inclusive).</param>
    /// <returns>List of markers within the range.</returns>
    public IReadOnlyList<Marker> GetMarkersInRange(double startPosition, double endPosition)
    {
        lock (_lock)
        {
            return _markers
                .Where(m => m.Position >= startPosition && m.Position <= endPosition)
                .OrderBy(m => m.Position)
                .ToList()
                .AsReadOnly();
        }
    }

    /// <summary>
    /// Gets markers of a specific type.
    /// </summary>
    /// <param name="type">The marker type to filter by.</param>
    /// <returns>List of markers of the specified type.</returns>
    public IReadOnlyList<Marker> GetMarkersByType(MarkerType type)
    {
        lock (_lock)
        {
            return _markers
                .Where(m => m.Type == type)
                .OrderBy(m => m.Position)
                .ToList()
                .AsReadOnly();
        }
    }

    /// <summary>
    /// Gets a marker by its ID.
    /// </summary>
    /// <param name="markerId">The marker ID.</param>
    /// <returns>The marker, or null if not found.</returns>
    public Marker? GetMarker(Guid markerId)
    {
        lock (_lock)
        {
            return _markers.FirstOrDefault(m => m.Id == markerId);
        }
    }

    /// <summary>
    /// Gets the marker closest to the specified position.
    /// </summary>
    /// <param name="position">Position in beats.</param>
    /// <returns>The closest marker, or null if no markers exist.</returns>
    public Marker? GetClosestMarker(double position)
    {
        lock (_lock)
        {
            if (_markers.Count == 0)
                return null;

            return _markers.MinBy(m => Math.Abs(m.Position - position));
        }
    }

    /// <summary>
    /// Gets the next marker after the specified position.
    /// </summary>
    /// <param name="position">Current position in beats.</param>
    /// <returns>The next marker, or null if none exists.</returns>
    public Marker? GetNextMarker(double position)
    {
        lock (_lock)
        {
            return _markers
                .Where(m => m.Position > position)
                .OrderBy(m => m.Position)
                .FirstOrDefault();
        }
    }

    /// <summary>
    /// Gets the previous marker before the specified position.
    /// </summary>
    /// <param name="position">Current position in beats.</param>
    /// <returns>The previous marker, or null if none exists.</returns>
    public Marker? GetPreviousMarker(double position)
    {
        lock (_lock)
        {
            return _markers
                .Where(m => m.Position < position)
                .OrderByDescending(m => m.Position)
                .FirstOrDefault();
        }
    }

    /// <summary>
    /// Requests a jump to the specified marker.
    /// </summary>
    /// <param name="marker">The marker to jump to.</param>
    public void JumpToMarker(Marker marker)
    {
        ArgumentNullException.ThrowIfNull(marker);

        lock (_lock)
        {
            if (!_markers.Contains(marker))
                throw new InvalidOperationException("Marker not found in track.");
        }

        JumpRequested?.Invoke(this, new MarkerJumpEventArgs(marker));
    }

    /// <summary>
    /// Requests a jump to a marker by name.
    /// </summary>
    /// <param name="name">The marker name.</param>
    /// <returns>True if marker found and jump requested, false otherwise.</returns>
    public bool JumpToMarker(string name)
    {
        Marker? marker;
        lock (_lock)
        {
            marker = _markers.FirstOrDefault(m =>
                m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        if (marker == null)
            return false;

        JumpToMarker(marker);
        return true;
    }

    /// <summary>
    /// Requests a jump to the next marker.
    /// </summary>
    /// <param name="currentPosition">Current position in beats.</param>
    /// <returns>True if jump requested, false if no next marker.</returns>
    public bool JumpToNextMarker(double currentPosition)
    {
        var nextMarker = GetNextMarker(currentPosition);
        if (nextMarker == null)
            return false;

        JumpToMarker(nextMarker);
        return true;
    }

    /// <summary>
    /// Requests a jump to the previous marker.
    /// </summary>
    /// <param name="currentPosition">Current position in beats.</param>
    /// <returns>True if jump requested, false if no previous marker.</returns>
    public bool JumpToPreviousMarker(double currentPosition)
    {
        var previousMarker = GetPreviousMarker(currentPosition);
        if (previousMarker == null)
            return false;

        JumpToMarker(previousMarker);
        return true;
    }

    /// <summary>
    /// Moves a marker to a new position.
    /// </summary>
    /// <param name="marker">The marker to move.</param>
    /// <param name="newPosition">New position in beats.</param>
    /// <returns>True if moved, false if marker is locked or not found.</returns>
    public bool MoveMarker(Marker marker, double newPosition)
    {
        ArgumentNullException.ThrowIfNull(marker);

        if (marker.IsLocked)
            return false;

        lock (_lock)
        {
            if (!_markers.Contains(marker))
                return false;
        }

        var previousPosition = marker.Position;
        marker.Position = newPosition;
        marker.Touch();

        MarkerMoved?.Invoke(this, new MarkerEventArgs(marker, previousPosition));
        MarkersChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// Clears all markers from the track.
    /// </summary>
    /// <param name="includeLockedMarkers">Whether to remove locked markers.</param>
    /// <returns>Number of markers removed.</returns>
    public int Clear(bool includeLockedMarkers = false)
    {
        int count;
        lock (_lock)
        {
            if (includeLockedMarkers)
            {
                count = _markers.Count;
                _markers.Clear();
            }
            else
            {
                var toRemove = _markers.Where(m => !m.IsLocked).ToList();
                count = toRemove.Count;
                foreach (var marker in toRemove)
                {
                    _markers.Remove(marker);
                }
            }
        }

        if (count > 0)
        {
            MarkersChanged?.Invoke(this, EventArgs.Empty);
        }

        return count;
    }

    /// <summary>
    /// Gets all loop markers.
    /// </summary>
    public IReadOnlyList<Marker> LoopMarkers => GetMarkersByType(MarkerType.Loop);

    /// <summary>
    /// Gets all section markers.
    /// </summary>
    public IReadOnlyList<Marker> SectionMarkers => GetMarkersByType(MarkerType.Section);

    /// <summary>
    /// Gets all cue markers.
    /// </summary>
    public IReadOnlyList<Marker> CueMarkers => GetMarkersByType(MarkerType.Cue);

    /// <summary>
    /// Finds the active loop at the specified position.
    /// </summary>
    /// <param name="position">Position in beats.</param>
    /// <returns>The active loop marker, or null if not in a loop.</returns>
    public Marker? GetActiveLoop(double position)
    {
        lock (_lock)
        {
            return _markers
                .Where(m => m.Type == MarkerType.Loop && m.ContainsPosition(position))
                .FirstOrDefault();
        }
    }

    /// <summary>
    /// Gets the section at the specified position.
    /// </summary>
    /// <param name="position">Position in beats.</param>
    /// <returns>The section marker, or null if not in a section.</returns>
    public Marker? GetSectionAt(double position)
    {
        lock (_lock)
        {
            return _markers
                .Where(m => m.Type == MarkerType.Section && m.Position <= position)
                .OrderByDescending(m => m.Position)
                .FirstOrDefault();
        }
    }
}
