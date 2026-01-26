// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using NAudio.Wave;
using MusicEngine.Core.Routing;

namespace MusicEngine.Core;

/// <summary>
/// Manages audio routing between channels, buses, and effects.
/// Provides a flexible routing matrix for complex audio signal flow.
/// </summary>
public class AudioRouter : IDisposable
{
    private readonly Dictionary<string, AudioChannel> _channels;
    private readonly Dictionary<string, AudioBus> _buses;
    private readonly List<SimpleAudioRoute> _routes;
    private readonly object _routingLock = new();
    private readonly WaveFormat _defaultFormat;

    private AudioBus? _masterBus;
    private bool _disposed;

    /// <summary>
    /// Creates a new audio router with the specified format.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz (default 44100)</param>
    /// <param name="channels">Number of channels (default 2 for stereo)</param>
    public AudioRouter(int sampleRate = 44100, int channels = 2)
    {
        _defaultFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        _channels = new Dictionary<string, AudioChannel>(StringComparer.OrdinalIgnoreCase);
        _buses = new Dictionary<string, AudioBus>(StringComparer.OrdinalIgnoreCase);
        _routes = new List<SimpleAudioRoute>();

        // Create default master bus
        _masterBus = CreateBus("Master");
    }

    /// <summary>
    /// Gets the default wave format for this router.
    /// </summary>
    public WaveFormat DefaultFormat => _defaultFormat;

    /// <summary>
    /// Gets the master output bus.
    /// </summary>
    public AudioBus MasterBus => _masterBus ?? throw new InvalidOperationException("Master bus not initialized");

    /// <summary>
    /// Gets all registered channels.
    /// </summary>
    public IReadOnlyDictionary<string, AudioChannel> Channels => _channels;

    /// <summary>
    /// Gets all registered buses.
    /// </summary>
    public IReadOnlyDictionary<string, AudioBus> Buses => _buses;

    /// <summary>
    /// Gets all active routes.
    /// </summary>
    public IReadOnlyList<SimpleAudioRoute> Routes
    {
        get
        {
            lock (_routingLock)
            {
                return _routes.ToList().AsReadOnly();
            }
        }
    }

    #region Channel Management

    /// <summary>
    /// Creates a new audio channel from a sample provider.
    /// </summary>
    /// <param name="name">Channel name</param>
    /// <param name="source">Audio source</param>
    /// <returns>The created channel</returns>
    public AudioChannel CreateChannel(string name, ISampleProvider source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(source);

        lock (_routingLock)
        {
            if (_channels.ContainsKey(name))
            {
                throw new InvalidOperationException($"Channel '{name}' already exists");
            }

            var channel = new AudioChannel(name, source);
            _channels[name] = channel;

            Console.WriteLine($"[AudioRouter] Created channel: {name}");
            return channel;
        }
    }

    /// <summary>
    /// Gets a channel by name.
    /// </summary>
    /// <param name="name">Channel name</param>
    /// <returns>The channel, or null if not found</returns>
    public AudioChannel? GetChannel(string name)
    {
        lock (_routingLock)
        {
            return _channels.TryGetValue(name, out var channel) ? channel : null;
        }
    }

    /// <summary>
    /// Removes a channel and all its routes.
    /// </summary>
    /// <param name="name">Channel name</param>
    /// <returns>True if the channel was removed</returns>
    public bool RemoveChannel(string name)
    {
        lock (_routingLock)
        {
            if (!_channels.TryGetValue(name, out var channel))
                return false;

            // Remove all routes involving this channel
            _routes.RemoveAll(r => r.SourceName == name || r.DestinationName == name);
            _channels.Remove(name);

            Console.WriteLine($"[AudioRouter] Removed channel: {name}");
            return true;
        }
    }

    #endregion

    #region Bus Management

    /// <summary>
    /// Creates a new audio bus.
    /// </summary>
    /// <param name="name">Bus name</param>
    /// <returns>The created bus</returns>
    public AudioBus CreateBus(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (_routingLock)
        {
            if (_buses.ContainsKey(name))
            {
                throw new InvalidOperationException($"Bus '{name}' already exists");
            }

            var bus = new AudioBus(name, _defaultFormat.SampleRate);
            _buses[name] = bus;

            Console.WriteLine($"[AudioRouter] Created bus: {name}");
            return bus;
        }
    }

    /// <summary>
    /// Gets a bus by name.
    /// </summary>
    /// <param name="name">Bus name</param>
    /// <returns>The bus, or null if not found</returns>
    public AudioBus? GetBus(string name)
    {
        lock (_routingLock)
        {
            return _buses.TryGetValue(name, out var bus) ? bus : null;
        }
    }

    /// <summary>
    /// Removes a bus and all its routes.
    /// Cannot remove the master bus.
    /// </summary>
    /// <param name="name">Bus name</param>
    /// <returns>True if the bus was removed</returns>
    public bool RemoveBus(string name)
    {
        if (string.Equals(name, "Master", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("[AudioRouter] Cannot remove master bus");
            return false;
        }

        lock (_routingLock)
        {
            if (!_buses.TryGetValue(name, out var bus))
                return false;

            // Remove all routes involving this bus
            _routes.RemoveAll(r => r.SourceName == name || r.DestinationName == name);
            _buses.Remove(name);

            Console.WriteLine($"[AudioRouter] Removed bus: {name}");
            return true;
        }
    }

    #endregion

    #region Routing

    /// <summary>
    /// Creates a route from a source channel to a destination bus.
    /// </summary>
    /// <param name="sourceChannel">Source channel name</param>
    /// <param name="destBus">Destination bus name</param>
    /// <param name="level">Route level (0.0 - 1.0)</param>
    /// <returns>The created route</returns>
    public SimpleAudioRoute Route(string sourceChannel, string destBus, float level = 1.0f)
    {
        lock (_routingLock)
        {
            // Get source (can be channel or bus)
            ISampleProvider? source = GetChannel(sourceChannel) ?? (ISampleProvider?)GetBus(sourceChannel);

            if (source == null)
            {
                throw new ArgumentException($"Source '{sourceChannel}' not found (neither channel nor bus)");
            }

            // Get destination bus
            var dest = GetBus(destBus);
            if (dest == null)
            {
                throw new ArgumentException($"Destination bus '{destBus}' not found");
            }

            // Check for duplicate routes
            var existingRoute = _routes.FirstOrDefault(r =>
                r.SourceName == sourceChannel && r.DestinationName == destBus);

            if (existingRoute != null)
            {
                existingRoute.Level = level;
                Console.WriteLine($"[AudioRouter] Updated route: {sourceChannel} -> {destBus} (level: {level:F2})");
                return existingRoute;
            }

            // Create the route
            var route = new SimpleAudioRoute(sourceChannel, destBus, source, dest, level);

            // Create a level-controlled wrapper
            var leveledSource = new LeveledSampleProvider(source, level);
            route.LeveledSource = leveledSource;

            // Add to destination bus
            dest.AddInput(leveledSource);
            _routes.Add(route);

            Console.WriteLine($"[AudioRouter] Created route: {sourceChannel} -> {destBus} (level: {level:F2})");
            return route;
        }
    }

    /// <summary>
    /// Creates a route from a source to the master bus.
    /// </summary>
    /// <param name="sourceName">Source channel or bus name</param>
    /// <param name="level">Route level</param>
    /// <returns>The created route</returns>
    public SimpleAudioRoute RouteToMaster(string sourceName, float level = 1.0f)
    {
        return Route(sourceName, "Master", level);
    }

    /// <summary>
    /// Removes a route between source and destination.
    /// </summary>
    /// <param name="sourceChannel">Source name</param>
    /// <param name="destBus">Destination bus name</param>
    /// <returns>True if the route was removed</returns>
    public bool RemoveRoute(string sourceChannel, string destBus)
    {
        lock (_routingLock)
        {
            var route = _routes.FirstOrDefault(r =>
                r.SourceName == sourceChannel && r.DestinationName == destBus);

            if (route == null)
                return false;

            // Remove from destination bus
            var dest = GetBus(destBus);
            if (dest != null && route.LeveledSource != null)
            {
                dest.RemoveInput(route.LeveledSource);
            }

            _routes.Remove(route);
            Console.WriteLine($"[AudioRouter] Removed route: {sourceChannel} -> {destBus}");
            return true;
        }
    }

    /// <summary>
    /// Gets the routing matrix as a 2D representation.
    /// </summary>
    /// <returns>Routing matrix information</returns>
    public SimpleRoutingMatrix GetRoutingMatrix()
    {
        lock (_routingLock)
        {
            var sources = new List<string>();
            sources.AddRange(_channels.Keys);
            sources.AddRange(_buses.Keys.Where(k => !k.Equals("Master", StringComparison.OrdinalIgnoreCase)));

            var destinations = _buses.Keys.ToList();

            var matrix = new float[sources.Count, destinations.Count];
            var routeInfo = new Dictionary<(string Source, string Dest), SimpleAudioRoute>();

            for (int s = 0; s < sources.Count; s++)
            {
                for (int d = 0; d < destinations.Count; d++)
                {
                    var route = _routes.FirstOrDefault(r =>
                        r.SourceName == sources[s] && r.DestinationName == destinations[d]);

                    matrix[s, d] = route?.Level ?? 0f;

                    if (route != null)
                    {
                        routeInfo[(sources[s], destinations[d])] = route;
                    }
                }
            }

            return new SimpleRoutingMatrix
            {
                Sources = sources.ToArray(),
                Destinations = destinations.ToArray(),
                Levels = matrix,
                Routes = routeInfo
            };
        }
    }

    /// <summary>
    /// Sets the level of an existing route.
    /// </summary>
    /// <param name="sourceChannel">Source name</param>
    /// <param name="destBus">Destination bus name</param>
    /// <param name="level">New level</param>
    /// <returns>True if the route was found and updated</returns>
    public bool SetRouteLevel(string sourceChannel, string destBus, float level)
    {
        lock (_routingLock)
        {
            var route = _routes.FirstOrDefault(r =>
                r.SourceName == sourceChannel && r.DestinationName == destBus);

            if (route == null)
                return false;

            route.Level = Math.Clamp(level, 0f, 2f);
            if (route.LeveledSource != null)
            {
                route.LeveledSource.Level = route.Level;
            }

            return true;
        }
    }

    #endregion

    #region Sidechain Routing

    /// <summary>
    /// Creates a sidechain connection from a source to a sidechainable effect.
    /// </summary>
    /// <param name="sidechainSourceName">Name of the sidechain source (channel or bus)</param>
    /// <param name="targetEffect">The effect to receive the sidechain</param>
    public void ConnectSidechain(string sidechainSourceName, ISidechainable targetEffect)
    {
        lock (_routingLock)
        {
            ISampleProvider? source = GetChannel(sidechainSourceName) ?? (ISampleProvider?)GetBus(sidechainSourceName);

            if (source == null)
            {
                throw new ArgumentException($"Sidechain source '{sidechainSourceName}' not found");
            }

            targetEffect.ConnectSidechain(source);
            Console.WriteLine($"[AudioRouter] Connected sidechain: {sidechainSourceName} -> effect");
        }
    }

    /// <summary>
    /// Creates a sidechain input from a channel or bus.
    /// </summary>
    /// <param name="sourceName">Name of the source channel or bus</param>
    /// <returns>A SidechainInput that can be connected to effects</returns>
    public SidechainInput CreateSidechainInput(string sourceName)
    {
        lock (_routingLock)
        {
            ISampleProvider? source = GetChannel(sourceName) ?? (ISampleProvider?)GetBus(sourceName);

            if (source == null)
            {
                throw new ArgumentException($"Source '{sourceName}' not found");
            }

            var sidechainInput = new SidechainInput(source)
            {
                Name = $"SC_{sourceName}"
            };

            Console.WriteLine($"[AudioRouter] Created sidechain input from: {sourceName}");
            return sidechainInput;
        }
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Prints the current routing configuration to the console.
    /// </summary>
    public void PrintRoutingInfo()
    {
        lock (_routingLock)
        {
            Console.WriteLine("\n=== Audio Router Configuration ===");

            Console.WriteLine($"\nChannels ({_channels.Count}):");
            foreach (var ch in _channels.Values)
            {
                Console.WriteLine($"  [{ch.Name}] Vol: {ch.Volume:F2} Pan: {ch.Pan:F2} Mute: {ch.Mute} Solo: {ch.Solo}");
            }

            Console.WriteLine($"\nBuses ({_buses.Count}):");
            foreach (var bus in _buses.Values)
            {
                Console.WriteLine($"  [{bus.Name}] Vol: {bus.Volume:F2} Pan: {bus.Pan:F2} Muted: {bus.Muted}");
            }

            Console.WriteLine($"\nRoutes ({_routes.Count}):");
            foreach (var route in _routes)
            {
                Console.WriteLine($"  {route.SourceName} -> {route.DestinationName} (Level: {route.Level:F2})");
            }

            Console.WriteLine();
        }
    }

    /// <summary>
    /// Clears all routes but keeps channels and buses.
    /// </summary>
    public void ClearRoutes()
    {
        lock (_routingLock)
        {
            foreach (var bus in _buses.Values)
            {
                // Clear all inputs from buses
                foreach (var route in _routes.Where(r => r.DestinationName == bus.Name))
                {
                    if (route.LeveledSource != null)
                    {
                        bus.RemoveInput(route.LeveledSource);
                    }
                }
            }

            _routes.Clear();
            Console.WriteLine("[AudioRouter] Cleared all routes");
        }
    }

    /// <summary>
    /// Resets the router to initial state.
    /// </summary>
    public void Reset()
    {
        lock (_routingLock)
        {
            ClearRoutes();
            _channels.Clear();
            _buses.Clear();

            // Recreate master bus
            _masterBus = CreateBus("Master");

            Console.WriteLine("[AudioRouter] Reset complete");
        }
    }

    /// <summary>
    /// Disposes of the router and all resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_routingLock)
        {
            ClearRoutes();
            _channels.Clear();
            _buses.Clear();
            _masterBus = null;
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }

    #endregion
}

/// <summary>
/// Represents a simple route between a source and destination.
/// </summary>
public class SimpleAudioRoute
{
    /// <summary>
    /// Creates a new simple audio route.
    /// </summary>
    public SimpleAudioRoute(string sourceName, string destName, ISampleProvider source, AudioBus destination, float level)
    {
        SourceName = sourceName;
        DestinationName = destName;
        Source = source;
        Destination = destination;
        Level = level;
    }

    /// <summary>
    /// Name of the source channel or bus.
    /// </summary>
    public string SourceName { get; }

    /// <summary>
    /// Name of the destination bus.
    /// </summary>
    public string DestinationName { get; }

    /// <summary>
    /// The source sample provider.
    /// </summary>
    public ISampleProvider Source { get; }

    /// <summary>
    /// The destination bus.
    /// </summary>
    public AudioBus Destination { get; }

    /// <summary>
    /// Route level (0.0 - 2.0).
    /// </summary>
    public float Level { get; set; }

    /// <summary>
    /// The level-controlled source used in the route.
    /// </summary>
    internal LeveledSampleProvider? LeveledSource { get; set; }
}

/// <summary>
/// Represents a simple routing matrix for visualization.
/// </summary>
public class SimpleRoutingMatrix
{
    /// <summary>
    /// Source names (channels and buses).
    /// </summary>
    public required string[] Sources { get; init; }

    /// <summary>
    /// Destination bus names.
    /// </summary>
    public required string[] Destinations { get; init; }

    /// <summary>
    /// Level matrix [source, destination].
    /// </summary>
    public required float[,] Levels { get; init; }

    /// <summary>
    /// Route objects indexed by (source, destination) tuple.
    /// </summary>
    public required Dictionary<(string Source, string Dest), SimpleAudioRoute> Routes { get; init; }

    /// <summary>
    /// Prints the matrix to console.
    /// </summary>
    public void Print()
    {
        Console.WriteLine("\n=== Routing Matrix ===");

        // Header
        Console.Write("           ");
        foreach (var dest in Destinations)
        {
            Console.Write($"{dest,10}");
        }
        Console.WriteLine();

        // Rows
        for (int s = 0; s < Sources.Length; s++)
        {
            Console.Write($"{Sources[s],10} ");
            for (int d = 0; d < Destinations.Length; d++)
            {
                float level = Levels[s, d];
                if (level > 0)
                {
                    Console.Write($"{level,9:F2} ");
                }
                else
                {
                    Console.Write($"{"---",9} ");
                }
            }
            Console.WriteLine();
        }

        Console.WriteLine();
    }
}

/// <summary>
/// A sample provider wrapper that applies a level/gain.
/// </summary>
internal class LeveledSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;

    public LeveledSampleProvider(ISampleProvider source, float level)
    {
        _source = source;
        Level = level;
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public float Level { get; set; }

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = _source.Read(buffer, offset, count);

        if (Math.Abs(Level - 1f) > 0.001f)
        {
            for (int i = offset; i < offset + samplesRead; i++)
            {
                buffer[i] *= Level;
            }
        }

        return samplesRead;
    }
}
