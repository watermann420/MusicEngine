//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Matrix for managing sidechain routing between audio sources and effects.

using NAudio.Wave;

namespace MusicEngine.Core.Routing;

/// <summary>
/// Represents a sidechain routing connection from an audio source to an effect.
/// </summary>
public class SidechainRoute
{
    /// <summary>
    /// Gets the unique identifier for this route.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets or sets the name of the source providing the sidechain signal.
    /// </summary>
    public string SourceName { get; set; }

    /// <summary>
    /// Gets or sets the audio source providing the sidechain signal.
    /// </summary>
    public ISampleProvider? Source { get; set; }

    /// <summary>
    /// Gets or sets the name of the target effect receiving the sidechain signal.
    /// </summary>
    public string TargetEffectName { get; set; }

    /// <summary>
    /// Gets or sets the target effect receiving the sidechain signal.
    /// </summary>
    public IEffect? TargetEffect { get; set; }

    /// <summary>
    /// Gets or sets whether this route is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the gain applied to the sidechain signal (0.1 - 10.0).
    /// </summary>
    public float Gain { get; set; } = 1.0f;

    /// <summary>
    /// Gets the timestamp when this route was created.
    /// </summary>
    public DateTime Created { get; }

    /// <summary>
    /// Creates a new sidechain route.
    /// </summary>
    /// <param name="sourceName">The name of the source.</param>
    /// <param name="targetEffectName">The name of the target effect.</param>
    public SidechainRoute(string sourceName, string targetEffectName)
    {
        Id = Guid.NewGuid();
        SourceName = sourceName ?? throw new ArgumentNullException(nameof(sourceName));
        TargetEffectName = targetEffectName ?? throw new ArgumentNullException(nameof(targetEffectName));
        Created = DateTime.UtcNow;
    }

    /// <summary>
    /// Returns a string representation of this route.
    /// </summary>
    public override string ToString()
    {
        string status = IsActive ? "" : " [INACTIVE]";
        string connected = Source != null && TargetEffect != null ? "" : " [DISCONNECTED]";
        return $"{SourceName} -> {TargetEffectName}{status}{connected}";
    }
}

/// <summary>
/// Manages sidechain routing between audio sources and effects.
/// Provides a centralized way to configure which audio signals control which effects.
/// </summary>
public class SidechainMatrix
{
    private readonly object _lock = new();
    private readonly List<SidechainRoute> _routes = new();
    private readonly Dictionary<IEffect, SidechainRoute> _effectRouteMap = new();

    /// <summary>
    /// Event raised when a route is created.
    /// </summary>
    public event EventHandler<SidechainRoute>? RouteCreated;

    /// <summary>
    /// Event raised when a route is removed.
    /// </summary>
    public event EventHandler<SidechainRoute>? RouteRemoved;

    /// <summary>
    /// Event raised when a route is modified.
    /// </summary>
    public event EventHandler<SidechainRoute>? RouteModified;

    /// <summary>
    /// Gets the list of all sidechain routes.
    /// </summary>
    public IReadOnlyList<SidechainRoute> Routes
    {
        get
        {
            lock (_lock)
            {
                return _routes.ToList().AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Gets the number of routes in the matrix.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _routes.Count;
            }
        }
    }

    /// <summary>
    /// Creates a new sidechain route between a source and target effect.
    /// </summary>
    /// <param name="sourceName">The name of the audio source (e.g., "Kick Drum", "Track 1").</param>
    /// <param name="targetEffectName">The name of the target effect (e.g., "Bass Compressor").</param>
    /// <returns>The created sidechain route.</returns>
    /// <exception cref="ArgumentNullException">Thrown if sourceName or targetEffectName is null.</exception>
    public SidechainRoute CreateRoute(string sourceName, string targetEffectName)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
            throw new ArgumentNullException(nameof(sourceName));
        if (string.IsNullOrWhiteSpace(targetEffectName))
            throw new ArgumentNullException(nameof(targetEffectName));

        var route = new SidechainRoute(sourceName, targetEffectName);

        lock (_lock)
        {
            _routes.Add(route);
        }

        RouteCreated?.Invoke(this, route);
        return route;
    }

    /// <summary>
    /// Creates a new sidechain route with source and target already connected.
    /// </summary>
    /// <param name="sourceName">The name of the audio source.</param>
    /// <param name="source">The audio source sample provider.</param>
    /// <param name="targetEffectName">The name of the target effect.</param>
    /// <param name="effect">The target effect.</param>
    /// <returns>The created sidechain route.</returns>
    public SidechainRoute CreateRoute(string sourceName, ISampleProvider source, string targetEffectName, IEffect effect)
    {
        var route = CreateRoute(sourceName, targetEffectName);
        SetSource(route, source);
        SetTarget(route, effect);
        return route;
    }

    /// <summary>
    /// Removes a sidechain route from the matrix.
    /// </summary>
    /// <param name="route">The route to remove.</param>
    /// <returns>True if the route was removed, false if not found.</returns>
    public bool RemoveRoute(SidechainRoute route)
    {
        if (route == null)
            return false;

        bool removed;
        lock (_lock)
        {
            removed = _routes.Remove(route);
            if (removed && route.TargetEffect != null)
            {
                _effectRouteMap.Remove(route.TargetEffect);
            }
        }

        if (removed)
        {
            RouteRemoved?.Invoke(this, route);
        }

        return removed;
    }

    /// <summary>
    /// Removes a route by its ID.
    /// </summary>
    /// <param name="routeId">The ID of the route to remove.</param>
    /// <returns>True if the route was removed, false if not found.</returns>
    public bool RemoveRoute(Guid routeId)
    {
        SidechainRoute? route;
        lock (_lock)
        {
            route = _routes.Find(r => r.Id == routeId);
        }

        return route != null && RemoveRoute(route);
    }

    /// <summary>
    /// Sets the audio source for a sidechain route.
    /// </summary>
    /// <param name="route">The route to update.</param>
    /// <param name="source">The audio source sample provider.</param>
    /// <exception cref="ArgumentNullException">Thrown if route is null.</exception>
    public void SetSource(SidechainRoute route, ISampleProvider source)
    {
        if (route == null)
            throw new ArgumentNullException(nameof(route));

        lock (_lock)
        {
            if (!_routes.Contains(route))
            {
                throw new InvalidOperationException("Route is not managed by this SidechainMatrix.");
            }

            route.Source = source;
        }

        RouteModified?.Invoke(this, route);
    }

    /// <summary>
    /// Sets the target effect for a sidechain route.
    /// </summary>
    /// <param name="route">The route to update.</param>
    /// <param name="effect">The target effect.</param>
    /// <exception cref="ArgumentNullException">Thrown if route is null.</exception>
    public void SetTarget(SidechainRoute route, IEffect effect)
    {
        if (route == null)
            throw new ArgumentNullException(nameof(route));

        lock (_lock)
        {
            if (!_routes.Contains(route))
            {
                throw new InvalidOperationException("Route is not managed by this SidechainMatrix.");
            }

            // Remove old mapping if exists
            if (route.TargetEffect != null)
            {
                _effectRouteMap.Remove(route.TargetEffect);
            }

            route.TargetEffect = effect;

            // Add new mapping
            if (effect != null)
            {
                _effectRouteMap[effect] = route;
            }
        }

        RouteModified?.Invoke(this, route);
    }

    /// <summary>
    /// Gets the sidechain source for a specific effect.
    /// </summary>
    /// <param name="effect">The effect to get the sidechain source for.</param>
    /// <returns>The sidechain source sample provider, or null if no active route exists.</returns>
    public ISampleProvider? GetSidechainFor(IEffect effect)
    {
        if (effect == null)
            return null;

        lock (_lock)
        {
            if (_effectRouteMap.TryGetValue(effect, out var route))
            {
                if (route.IsActive && route.Source != null)
                {
                    // If gain is applied, wrap the source
                    if (Math.Abs(route.Gain - 1.0f) > 0.001f)
                    {
                        return new GainSampleProvider(route.Source, route.Gain);
                    }
                    return route.Source;
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Gets the route for a specific effect.
    /// </summary>
    /// <param name="effect">The effect to get the route for.</param>
    /// <returns>The sidechain route, or null if not found.</returns>
    public SidechainRoute? GetRouteFor(IEffect effect)
    {
        if (effect == null)
            return null;

        lock (_lock)
        {
            return _effectRouteMap.TryGetValue(effect, out var route) ? route : null;
        }
    }

    /// <summary>
    /// Gets a route by its ID.
    /// </summary>
    /// <param name="routeId">The ID of the route.</param>
    /// <returns>The route, or null if not found.</returns>
    public SidechainRoute? GetRoute(Guid routeId)
    {
        lock (_lock)
        {
            return _routes.Find(r => r.Id == routeId);
        }
    }

    /// <summary>
    /// Gets all routes for a specific source name.
    /// </summary>
    /// <param name="sourceName">The source name to search for.</param>
    /// <returns>List of routes from the specified source.</returns>
    public List<SidechainRoute> GetRoutesForSource(string sourceName)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
            return new List<SidechainRoute>();

        lock (_lock)
        {
            return _routes
                .Where(r => r.SourceName.Equals(sourceName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    /// <summary>
    /// Gets all routes targeting a specific effect name.
    /// </summary>
    /// <param name="targetEffectName">The target effect name to search for.</param>
    /// <returns>List of routes to the specified effect.</returns>
    public List<SidechainRoute> GetRoutesForTarget(string targetEffectName)
    {
        if (string.IsNullOrWhiteSpace(targetEffectName))
            return new List<SidechainRoute>();

        lock (_lock)
        {
            return _routes
                .Where(r => r.TargetEffectName.Equals(targetEffectName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    /// <summary>
    /// Enables or disables a route.
    /// </summary>
    /// <param name="route">The route to modify.</param>
    /// <param name="isActive">Whether the route should be active.</param>
    public void SetRouteActive(SidechainRoute route, bool isActive)
    {
        if (route == null)
            return;

        lock (_lock)
        {
            if (_routes.Contains(route))
            {
                route.IsActive = isActive;
            }
        }

        RouteModified?.Invoke(this, route);
    }

    /// <summary>
    /// Sets the gain for a route.
    /// </summary>
    /// <param name="route">The route to modify.</param>
    /// <param name="gain">The gain value (0.1 - 10.0).</param>
    public void SetRouteGain(SidechainRoute route, float gain)
    {
        if (route == null)
            return;

        lock (_lock)
        {
            if (_routes.Contains(route))
            {
                route.Gain = Math.Clamp(gain, 0.1f, 10f);
            }
        }

        RouteModified?.Invoke(this, route);
    }

    /// <summary>
    /// Clears all routes from the matrix.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _routes.Clear();
            _effectRouteMap.Clear();
        }
    }

    /// <summary>
    /// Helper sample provider that applies gain to a source.
    /// </summary>
    private class GainSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly float _gain;

        public WaveFormat WaveFormat => _source.WaveFormat;

        public GainSampleProvider(ISampleProvider source, float gain)
        {
            _source = source;
            _gain = gain;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = _source.Read(buffer, offset, count);

            for (int i = 0; i < samplesRead; i++)
            {
                buffer[offset + i] *= _gain;
            }

            return samplesRead;
        }
    }
}
