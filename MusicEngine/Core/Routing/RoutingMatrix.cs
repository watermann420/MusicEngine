//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Central routing matrix for managing all audio routes, sources, and destinations.


using NAudio.Wave;


namespace MusicEngine.Core.Routing;


/// <summary>
/// Data structure for matrix visualization representing a cell in the routing matrix.
/// </summary>
public class MatrixCell
{
    /// <summary>
    /// The source routing point (row).
    /// </summary>
    public required RoutingPoint Source { get; init; }

    /// <summary>
    /// The destination routing point (column).
    /// </summary>
    public required RoutingPoint Destination { get; init; }

    /// <summary>
    /// The route if one exists, null otherwise.
    /// </summary>
    public AudioRoute? Route { get; set; }

    /// <summary>
    /// Whether a route exists at this cell.
    /// </summary>
    public bool HasRoute => Route != null;

    /// <summary>
    /// The gain of the route in dB, or -infinity if no route.
    /// </summary>
    public float GainDb => Route?.GainDb ?? float.NegativeInfinity;

    /// <summary>
    /// Whether the route at this cell is enabled.
    /// </summary>
    public bool IsEnabled => Route?.Enabled ?? false;
}


/// <summary>
/// Result of feedback detection analysis.
/// </summary>
public class FeedbackDetectionResult
{
    /// <summary>
    /// Whether a potential feedback loop was detected.
    /// </summary>
    public bool HasFeedback { get; init; }

    /// <summary>
    /// Description of the feedback path if one exists.
    /// </summary>
    public string? FeedbackPath { get; init; }

    /// <summary>
    /// The routing points involved in the feedback loop.
    /// </summary>
    public List<RoutingPoint> InvolvedPoints { get; init; } = new();
}


/// <summary>
/// Central routing matrix for managing all audio connections in the system.
/// Provides comprehensive routing management including feedback detection,
/// matrix visualization support, and route queries.
/// </summary>
public class RoutingMatrix : IDisposable
{
    private readonly object _lock = new();
    private readonly Dictionary<Guid, RoutingPoint> _points;
    private readonly Dictionary<Guid, AudioRoute> _routes;
    private readonly Dictionary<Guid, List<AudioRoute>> _routesBySource;
    private readonly Dictionary<Guid, List<AudioRoute>> _routesByDestination;
    private readonly int _sampleRate;
    private readonly int _channels;
    private bool _disposed;

    /// <summary>
    /// Event raised when a route is created.
    /// </summary>
    public event EventHandler<RouteEventArgs>? RouteCreated;

    /// <summary>
    /// Event raised when a route is removed.
    /// </summary>
    public event EventHandler<RouteEventArgs>? RouteRemoved;

    /// <summary>
    /// Event raised when a route is modified.
    /// </summary>
    public event EventHandler<RouteEventArgs>? RouteModified;

    /// <summary>
    /// Event raised when a routing point is added.
    /// </summary>
    public event EventHandler<RoutingPoint>? PointAdded;

    /// <summary>
    /// Event raised when a routing point is removed.
    /// </summary>
    public event EventHandler<RoutingPoint>? PointRemoved;

    /// <summary>
    /// Creates a new routing matrix.
    /// </summary>
    /// <param name="sampleRate">Sample rate for audio processing.</param>
    /// <param name="channels">Default number of channels.</param>
    public RoutingMatrix(int sampleRate = 44100, int channels = 2)
    {
        _sampleRate = sampleRate;
        _channels = channels;
        _points = new Dictionary<Guid, RoutingPoint>();
        _routes = new Dictionary<Guid, AudioRoute>();
        _routesBySource = new Dictionary<Guid, List<AudioRoute>>();
        _routesByDestination = new Dictionary<Guid, List<AudioRoute>>();
    }

    /// <summary>
    /// Gets the sample rate for this routing matrix.
    /// </summary>
    public int SampleRate => _sampleRate;

    /// <summary>
    /// Gets the default channel count.
    /// </summary>
    public int Channels => _channels;

    /// <summary>
    /// Gets the number of routing points.
    /// </summary>
    public int PointCount
    {
        get
        {
            lock (_lock)
            {
                return _points.Count;
            }
        }
    }

    /// <summary>
    /// Gets the number of routes.
    /// </summary>
    public int RouteCount
    {
        get
        {
            lock (_lock)
            {
                return _routes.Count;
            }
        }
    }

    #region Routing Point Management

    /// <summary>
    /// Registers a routing point with the matrix.
    /// </summary>
    /// <param name="point">The routing point to register.</param>
    /// <returns>True if registered, false if already exists.</returns>
    public bool RegisterPoint(RoutingPoint point)
    {
        if (point == null)
            throw new ArgumentNullException(nameof(point));

        lock (_lock)
        {
            if (_points.ContainsKey(point.PointId))
            {
                return false;
            }

            _points[point.PointId] = point;
            _routesBySource[point.PointId] = new List<AudioRoute>();
            _routesByDestination[point.PointId] = new List<AudioRoute>();
        }

        PointAdded?.Invoke(this, point);
        return true;
    }

    /// <summary>
    /// Unregisters a routing point and removes all associated routes.
    /// </summary>
    /// <param name="pointId">The point ID to unregister.</param>
    /// <returns>True if unregistered, false if not found.</returns>
    public bool UnregisterPoint(Guid pointId)
    {
        RoutingPoint? point;
        List<AudioRoute> routesToRemove;

        lock (_lock)
        {
            if (!_points.TryGetValue(pointId, out point))
            {
                return false;
            }

            // Collect routes to remove
            routesToRemove = new List<AudioRoute>();
            if (_routesBySource.TryGetValue(pointId, out var sourceRoutes))
            {
                routesToRemove.AddRange(sourceRoutes);
            }
            if (_routesByDestination.TryGetValue(pointId, out var destRoutes))
            {
                routesToRemove.AddRange(destRoutes.Where(r => !routesToRemove.Contains(r)));
            }

            // Remove all associated routes
            foreach (var route in routesToRemove)
            {
                RemoveRouteInternal(route.RouteId);
            }

            _points.Remove(pointId);
            _routesBySource.Remove(pointId);
            _routesByDestination.Remove(pointId);
        }

        // Raise events outside lock
        foreach (var route in routesToRemove)
        {
            RouteRemoved?.Invoke(this, new RouteEventArgs(route, "Removed"));
        }
        PointRemoved?.Invoke(this, point);

        return true;
    }

    /// <summary>
    /// Gets a routing point by ID.
    /// </summary>
    /// <param name="pointId">The point ID.</param>
    /// <returns>The routing point or null if not found.</returns>
    public RoutingPoint? GetPoint(Guid pointId)
    {
        lock (_lock)
        {
            return _points.TryGetValue(pointId, out var point) ? point : null;
        }
    }

    /// <summary>
    /// Gets a routing point by name.
    /// </summary>
    /// <param name="name">The point name.</param>
    /// <returns>The first matching routing point or null if not found.</returns>
    public RoutingPoint? GetPointByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        lock (_lock)
        {
            return _points.Values.FirstOrDefault(
                p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Gets all routing points of a specific type.
    /// </summary>
    /// <param name="type">The routing point type.</param>
    /// <returns>List of matching routing points.</returns>
    public List<RoutingPoint> GetPointsByType(RoutingPointType type)
    {
        lock (_lock)
        {
            return _points.Values.Where(p => p.Type == type).ToList();
        }
    }

    /// <summary>
    /// Gets all registered routing points.
    /// </summary>
    /// <returns>List of all routing points.</returns>
    public List<RoutingPoint> GetAllPoints()
    {
        lock (_lock)
        {
            return _points.Values.ToList();
        }
    }

    /// <summary>
    /// Gets all routing points that can act as sources.
    /// </summary>
    public List<RoutingPoint> GetSources()
    {
        lock (_lock)
        {
            return _points.Values.Where(p => p.CanBeSource).ToList();
        }
    }

    /// <summary>
    /// Gets all routing points that can act as destinations.
    /// </summary>
    public List<RoutingPoint> GetDestinations()
    {
        lock (_lock)
        {
            return _points.Values.Where(p => p.CanBeDestination).ToList();
        }
    }

    #endregion

    #region Route Management

    /// <summary>
    /// Creates a route between two routing points.
    /// </summary>
    /// <param name="source">The source routing point.</param>
    /// <param name="destination">The destination routing point.</param>
    /// <param name="gainDb">Initial gain in decibels (default: 0 dB).</param>
    /// <returns>The created route.</returns>
    /// <exception cref="ArgumentException">Thrown if points are not registered or routing is invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown if route would create feedback loop.</exception>
    public AudioRoute CreateRoute(RoutingPoint source, RoutingPoint destination, float gainDb = 0f)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (destination == null)
            throw new ArgumentNullException(nameof(destination));

        AudioRoute route;

        lock (_lock)
        {
            // Verify points are registered
            if (!_points.ContainsKey(source.PointId))
            {
                throw new ArgumentException($"Source point '{source.Name}' is not registered.", nameof(source));
            }
            if (!_points.ContainsKey(destination.PointId))
            {
                throw new ArgumentException($"Destination point '{destination.Name}' is not registered.", nameof(destination));
            }

            // Check for existing route
            var existingRoute = _routesBySource[source.PointId]
                .FirstOrDefault(r => r.Destination.PointId == destination.PointId);

            if (existingRoute != null)
            {
                throw new InvalidOperationException(
                    $"Route from '{source.Name}' to '{destination.Name}' already exists.");
            }

            // Check for feedback
            var feedbackResult = DetectFeedbackInternal(source, destination);
            if (feedbackResult.HasFeedback)
            {
                throw new InvalidOperationException(
                    $"Cannot create route: would create feedback loop. Path: {feedbackResult.FeedbackPath}");
            }

            // Create the route
            route = new AudioRoute(source, destination, gainDb);
            _routes[route.RouteId] = route;
            _routesBySource[source.PointId].Add(route);
            _routesByDestination[destination.PointId].Add(route);
        }

        RouteCreated?.Invoke(this, new RouteEventArgs(route, "Created"));
        return route;
    }

    /// <summary>
    /// Creates a route by source and destination IDs.
    /// </summary>
    /// <param name="sourceId">The source routing point ID.</param>
    /// <param name="destinationId">The destination routing point ID.</param>
    /// <param name="gainDb">Initial gain in decibels (default: 0 dB).</param>
    /// <returns>The created route.</returns>
    public AudioRoute CreateRoute(Guid sourceId, Guid destinationId, float gainDb = 0f)
    {
        var source = GetPoint(sourceId) ??
            throw new ArgumentException($"Source point with ID {sourceId} not found.", nameof(sourceId));
        var destination = GetPoint(destinationId) ??
            throw new ArgumentException($"Destination point with ID {destinationId} not found.", nameof(destinationId));

        return CreateRoute(source, destination, gainDb);
    }

    /// <summary>
    /// Removes a route by ID.
    /// </summary>
    /// <param name="routeId">The route ID.</param>
    /// <returns>True if removed, false if not found.</returns>
    public bool RemoveRoute(Guid routeId)
    {
        AudioRoute? route;

        lock (_lock)
        {
            if (!RemoveRouteInternal(routeId, out route))
            {
                return false;
            }
        }

        if (route != null)
        {
            RouteRemoved?.Invoke(this, new RouteEventArgs(route, "Removed"));
        }
        return true;
    }

    /// <summary>
    /// Removes a route between two points.
    /// </summary>
    /// <param name="source">The source routing point.</param>
    /// <param name="destination">The destination routing point.</param>
    /// <returns>True if removed, false if not found.</returns>
    public bool RemoveRoute(RoutingPoint source, RoutingPoint destination)
    {
        if (source == null || destination == null)
            return false;

        AudioRoute? route;

        lock (_lock)
        {
            route = _routesBySource.TryGetValue(source.PointId, out var routes)
                ? routes.FirstOrDefault(r => r.Destination.PointId == destination.PointId)
                : null;

            if (route == null)
            {
                return false;
            }

            RemoveRouteInternal(route.RouteId, out _);
        }

        RouteRemoved?.Invoke(this, new RouteEventArgs(route, "Removed"));
        return true;
    }

    /// <summary>
    /// Internal method to remove a route without raising events.
    /// </summary>
    private bool RemoveRouteInternal(Guid routeId)
    {
        return RemoveRouteInternal(routeId, out _);
    }

    /// <summary>
    /// Internal method to remove a route and return it.
    /// </summary>
    private bool RemoveRouteInternal(Guid routeId, out AudioRoute? removedRoute)
    {
        if (!_routes.TryGetValue(routeId, out removedRoute))
        {
            return false;
        }

        _routes.Remove(routeId);

        if (_routesBySource.TryGetValue(removedRoute.Source.PointId, out var sourceRoutes))
        {
            sourceRoutes.Remove(removedRoute);
        }

        if (_routesByDestination.TryGetValue(removedRoute.Destination.PointId, out var destRoutes))
        {
            destRoutes.Remove(removedRoute);
        }

        removedRoute.CompensationBuffer?.Dispose();
        return true;
    }

    /// <summary>
    /// Gets a route by ID.
    /// </summary>
    /// <param name="routeId">The route ID.</param>
    /// <returns>The route or null if not found.</returns>
    public AudioRoute? GetRoute(Guid routeId)
    {
        lock (_lock)
        {
            return _routes.TryGetValue(routeId, out var route) ? route : null;
        }
    }

    /// <summary>
    /// Gets a route between two points.
    /// </summary>
    /// <param name="source">The source routing point.</param>
    /// <param name="destination">The destination routing point.</param>
    /// <returns>The route or null if not found.</returns>
    public AudioRoute? GetRoute(RoutingPoint source, RoutingPoint destination)
    {
        if (source == null || destination == null)
            return null;

        lock (_lock)
        {
            if (!_routesBySource.TryGetValue(source.PointId, out var routes))
            {
                return null;
            }

            return routes.FirstOrDefault(r => r.Destination.PointId == destination.PointId);
        }
    }

    /// <summary>
    /// Gets all routes originating from a routing point.
    /// </summary>
    /// <param name="source">The source routing point.</param>
    /// <returns>List of routes from the source.</returns>
    public List<AudioRoute> GetRoutesFrom(RoutingPoint source)
    {
        if (source == null)
            return new List<AudioRoute>();

        lock (_lock)
        {
            return _routesBySource.TryGetValue(source.PointId, out var routes)
                ? routes.ToList()
                : new List<AudioRoute>();
        }
    }

    /// <summary>
    /// Gets all routes going to a routing point.
    /// </summary>
    /// <param name="destination">The destination routing point.</param>
    /// <returns>List of routes to the destination.</returns>
    public List<AudioRoute> GetRoutesTo(RoutingPoint destination)
    {
        if (destination == null)
            return new List<AudioRoute>();

        lock (_lock)
        {
            return _routesByDestination.TryGetValue(destination.PointId, out var routes)
                ? routes.ToList()
                : new List<AudioRoute>();
        }
    }

    /// <summary>
    /// Gets all routes.
    /// </summary>
    /// <returns>List of all routes.</returns>
    public List<AudioRoute> GetAllRoutes()
    {
        lock (_lock)
        {
            return _routes.Values.ToList();
        }
    }

    /// <summary>
    /// Gets all routes of a specific type.
    /// </summary>
    /// <param name="type">The route type.</param>
    /// <returns>List of matching routes.</returns>
    public List<AudioRoute> GetRoutesByType(RouteType type)
    {
        lock (_lock)
        {
            return _routes.Values.Where(r => r.Type == type).ToList();
        }
    }

    /// <summary>
    /// Sets the gain for a route.
    /// </summary>
    /// <param name="routeId">The route ID.</param>
    /// <param name="gainDb">The gain in decibels.</param>
    /// <returns>True if successful, false if route not found.</returns>
    public bool SetRouteGain(Guid routeId, float gainDb)
    {
        AudioRoute? route;

        lock (_lock)
        {
            if (!_routes.TryGetValue(routeId, out route))
            {
                return false;
            }

            route.GainDb = gainDb;
        }

        RouteModified?.Invoke(this, new RouteEventArgs(route, "GainChanged"));
        return true;
    }

    /// <summary>
    /// Enables or disables a route.
    /// </summary>
    /// <param name="routeId">The route ID.</param>
    /// <param name="enabled">Whether the route should be enabled.</param>
    /// <returns>True if successful, false if route not found.</returns>
    public bool SetRouteEnabled(Guid routeId, bool enabled)
    {
        AudioRoute? route;

        lock (_lock)
        {
            if (!_routes.TryGetValue(routeId, out route))
            {
                return false;
            }

            route.Enabled = enabled;
        }

        RouteModified?.Invoke(this, new RouteEventArgs(route, enabled ? "Enabled" : "Disabled"));
        return true;
    }

    #endregion

    #region Feedback Detection

    /// <summary>
    /// Detects if creating a route would cause a feedback loop.
    /// </summary>
    /// <param name="source">The potential source point.</param>
    /// <param name="destination">The potential destination point.</param>
    /// <returns>Feedback detection result.</returns>
    public FeedbackDetectionResult DetectFeedback(RoutingPoint source, RoutingPoint destination)
    {
        lock (_lock)
        {
            return DetectFeedbackInternal(source, destination);
        }
    }

    /// <summary>
    /// Internal feedback detection without locking.
    /// Uses depth-first search to find cycles.
    /// </summary>
    private FeedbackDetectionResult DetectFeedbackInternal(RoutingPoint source, RoutingPoint destination)
    {
        // Self-loop is always feedback
        if (source.PointId == destination.PointId)
        {
            return new FeedbackDetectionResult
            {
                HasFeedback = true,
                FeedbackPath = $"{source.Name} -> {source.Name} (self-loop)",
                InvolvedPoints = new List<RoutingPoint> { source }
            };
        }

        // Check if destination can reach source (would create a cycle)
        var visited = new HashSet<Guid>();
        var path = new List<RoutingPoint>();

        if (CanReach(destination.PointId, source.PointId, visited, path))
        {
            path.Insert(0, source);
            path.Add(destination);
            path.Add(source); // Complete the cycle

            return new FeedbackDetectionResult
            {
                HasFeedback = true,
                FeedbackPath = string.Join(" -> ", path.Select(p => p.Name)),
                InvolvedPoints = path
            };
        }

        return new FeedbackDetectionResult { HasFeedback = false };
    }

    /// <summary>
    /// Checks if one point can reach another through existing routes.
    /// </summary>
    private bool CanReach(Guid fromId, Guid toId, HashSet<Guid> visited, List<RoutingPoint> path)
    {
        if (fromId == toId)
        {
            return true;
        }

        if (visited.Contains(fromId))
        {
            return false;
        }

        visited.Add(fromId);

        if (!_routesBySource.TryGetValue(fromId, out var routes))
        {
            return false;
        }

        foreach (var route in routes)
        {
            if (!route.Enabled)
                continue;

            path.Add(route.Source);

            if (CanReach(route.Destination.PointId, toId, visited, path))
            {
                return true;
            }

            path.RemoveAt(path.Count - 1);
        }

        return false;
    }

    #endregion

    #region Matrix Visualization

    /// <summary>
    /// Gets data for matrix visualization.
    /// Returns a 2D array of cells representing all possible source/destination combinations.
    /// </summary>
    /// <returns>Matrix of cells for UI visualization.</returns>
    public MatrixCell[,] GetMatrixVisualization()
    {
        lock (_lock)
        {
            var sources = _points.Values.Where(p => p.CanBeSource).OrderBy(p => p.Name).ToList();
            var destinations = _points.Values.Where(p => p.CanBeDestination).OrderBy(p => p.Name).ToList();

            var matrix = new MatrixCell[sources.Count, destinations.Count];

            for (int row = 0; row < sources.Count; row++)
            {
                for (int col = 0; col < destinations.Count; col++)
                {
                    var source = sources[row];
                    var dest = destinations[col];

                    // Find existing route if any
                    var route = _routesBySource.TryGetValue(source.PointId, out var routes)
                        ? routes.FirstOrDefault(r => r.Destination.PointId == dest.PointId)
                        : null;

                    matrix[row, col] = new MatrixCell
                    {
                        Source = source,
                        Destination = dest,
                        Route = route
                    };
                }
            }

            return matrix;
        }
    }

    /// <summary>
    /// Gets a flat list of matrix data for UI binding.
    /// </summary>
    /// <returns>List of matrix cells.</returns>
    public List<MatrixCell> GetMatrixCells()
    {
        lock (_lock)
        {
            var cells = new List<MatrixCell>();
            var sources = _points.Values.Where(p => p.CanBeSource).OrderBy(p => p.Name).ToList();
            var destinations = _points.Values.Where(p => p.CanBeDestination).OrderBy(p => p.Name).ToList();

            foreach (var source in sources)
            {
                foreach (var dest in destinations)
                {
                    var route = _routesBySource.TryGetValue(source.PointId, out var routes)
                        ? routes.FirstOrDefault(r => r.Destination.PointId == dest.PointId)
                        : null;

                    cells.Add(new MatrixCell
                    {
                        Source = source,
                        Destination = dest,
                        Route = route
                    });
                }
            }

            return cells;
        }
    }

    #endregion

    #region Sidechain Helpers

    /// <summary>
    /// Gets all sidechain routes targeting a specific effect.
    /// </summary>
    /// <param name="effectName">The effect name.</param>
    /// <returns>List of sidechain routes.</returns>
    public List<AudioRoute> GetSidechainRoutesForEffect(string effectName)
    {
        lock (_lock)
        {
            return _routes.Values
                .Where(r => r.Type == RouteType.Sidechain &&
                            r.Destination.AssociatedEffectName?.Equals(effectName, StringComparison.OrdinalIgnoreCase) == true)
                .ToList();
        }
    }

    /// <summary>
    /// Gets the sidechain routing point for a specific effect.
    /// </summary>
    /// <param name="effectName">The effect name.</param>
    /// <returns>The sidechain routing point or null.</returns>
    public RoutingPoint? GetSidechainPointForEffect(string effectName)
    {
        lock (_lock)
        {
            return _points.Values.FirstOrDefault(p =>
                p.Type == RoutingPointType.Sidechain &&
                p.AssociatedEffectName?.Equals(effectName, StringComparison.OrdinalIgnoreCase) == true);
        }
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Clears all routes while keeping routing points.
    /// </summary>
    public void ClearAllRoutes()
    {
        List<AudioRoute> removedRoutes;

        lock (_lock)
        {
            removedRoutes = _routes.Values.ToList();

            foreach (var route in removedRoutes)
            {
                route.CompensationBuffer?.Dispose();
            }

            _routes.Clear();
            foreach (var list in _routesBySource.Values)
            {
                list.Clear();
            }
            foreach (var list in _routesByDestination.Values)
            {
                list.Clear();
            }
        }

        foreach (var route in removedRoutes)
        {
            RouteRemoved?.Invoke(this, new RouteEventArgs(route, "Cleared"));
        }
    }

    /// <summary>
    /// Clears all routing points and routes.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            foreach (var route in _routes.Values)
            {
                route.CompensationBuffer?.Dispose();
            }

            _routes.Clear();
            _points.Clear();
            _routesBySource.Clear();
            _routesByDestination.Clear();
        }
    }

    /// <summary>
    /// Gets a summary of the routing configuration.
    /// </summary>
    public string GetRoutingSummary()
    {
        lock (_lock)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Routing Matrix Summary:");
            sb.AppendLine($"  Points: {_points.Count}");
            sb.AppendLine($"  Routes: {_routes.Count}");
            sb.AppendLine();

            var byType = _points.Values.GroupBy(p => p.Type);
            foreach (var group in byType)
            {
                sb.AppendLine($"  {group.Key}: {group.Count()}");
            }

            sb.AppendLine();
            sb.AppendLine("Routes:");
            foreach (var route in _routes.Values.OrderBy(r => r.Source.Name))
            {
                string status = route.Enabled ? "" : " [DISABLED]";
                sb.AppendLine($"  {route.Source.Name} -> {route.Destination.Name} ({route.GainDb:F1} dB){status}");
            }

            return sb.ToString();
        }
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes of the routing matrix and all resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_lock)
        {
            foreach (var route in _routes.Values)
            {
                route.CompensationBuffer?.Dispose();
            }

            _routes.Clear();
            _points.Clear();
            _routesBySource.Clear();
            _routesByDestination.Clear();
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    #endregion
}
