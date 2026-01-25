//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Routing point that can send or receive audio signals in the routing matrix.


using NAudio.Wave;


namespace MusicEngine.Core.Routing;


/// <summary>
/// Specifies the type of routing point in the audio signal flow.
/// </summary>
public enum RoutingPointType
{
    /// <summary>
    /// A track or channel input receiving audio from an external source.
    /// </summary>
    Input,

    /// <summary>
    /// A track or channel output sending audio to the mix.
    /// </summary>
    Output,

    /// <summary>
    /// An auxiliary send point for routing to effects buses.
    /// </summary>
    Send,

    /// <summary>
    /// A return point receiving audio from an effects bus.
    /// </summary>
    Return,

    /// <summary>
    /// A sidechain point for routing control signals to dynamics processors.
    /// </summary>
    Sidechain,

    /// <summary>
    /// A group bus point for routing multiple tracks.
    /// </summary>
    Group,

    /// <summary>
    /// The master output bus.
    /// </summary>
    Master
}


/// <summary>
/// Represents a point in the audio routing matrix that can send or receive signals.
/// Routing points are the endpoints of audio routes and can be associated with
/// tracks, effects, buses, or other audio components.
/// </summary>
public class RoutingPoint : IEquatable<RoutingPoint>
{
    private readonly object _lock = new();
    private string _name;
    private int _channelCount;
    private bool _isActive;

    /// <summary>
    /// Creates a new routing point.
    /// </summary>
    /// <param name="name">The name of the routing point.</param>
    /// <param name="type">The type of routing point.</param>
    /// <param name="channelCount">Number of audio channels (default: 2 for stereo).</param>
    /// <exception cref="ArgumentNullException">Thrown if name is null or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if channelCount is less than 1.</exception>
    public RoutingPoint(string name, RoutingPointType type, int channelCount = 2)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentNullException(nameof(name));

        if (channelCount < 1)
            throw new ArgumentOutOfRangeException(nameof(channelCount), "Channel count must be at least 1.");

        PointId = Guid.NewGuid();
        _name = name;
        Type = type;
        _channelCount = channelCount;
        _isActive = true;
    }

    /// <summary>
    /// Gets the unique identifier for this routing point.
    /// </summary>
    public Guid PointId { get; }

    /// <summary>
    /// Gets or sets the name of this routing point.
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
        }
    }

    /// <summary>
    /// Gets the type of this routing point.
    /// </summary>
    public RoutingPointType Type { get; }

    /// <summary>
    /// Gets or sets the number of audio channels for this routing point.
    /// </summary>
    public int ChannelCount
    {
        get
        {
            lock (_lock)
            {
                return _channelCount;
            }
        }
        set
        {
            if (value < 1)
                throw new ArgumentOutOfRangeException(nameof(value), "Channel count must be at least 1.");

            lock (_lock)
            {
                _channelCount = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets whether this routing point is active.
    /// Inactive points do not process or route audio.
    /// </summary>
    public bool IsActive
    {
        get
        {
            lock (_lock)
            {
                return _isActive;
            }
        }
        set
        {
            lock (_lock)
            {
                _isActive = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets the associated track identifier.
    /// Can be null if not associated with a specific track.
    /// </summary>
    public string? AssociatedTrackId { get; set; }

    /// <summary>
    /// Gets or sets the associated effect name.
    /// Can be null if not associated with a specific effect.
    /// </summary>
    public string? AssociatedEffectName { get; set; }

    /// <summary>
    /// Gets or sets the associated sample provider for this routing point.
    /// </summary>
    public ISampleProvider? SampleProvider { get; set; }

    /// <summary>
    /// Gets or sets the wave format for this routing point.
    /// </summary>
    public WaveFormat? WaveFormat { get; set; }

    /// <summary>
    /// Gets or sets arbitrary user data associated with this routing point.
    /// </summary>
    public object? Tag { get; set; }

    /// <summary>
    /// Gets whether this routing point can act as a source (output-type points).
    /// </summary>
    public bool CanBeSource => Type is RoutingPointType.Output or RoutingPointType.Send
        or RoutingPointType.Group or RoutingPointType.Master;

    /// <summary>
    /// Gets whether this routing point can act as a destination (input-type points).
    /// </summary>
    public bool CanBeDestination => Type is RoutingPointType.Input or RoutingPointType.Return
        or RoutingPointType.Sidechain or RoutingPointType.Group or RoutingPointType.Master;

    /// <summary>
    /// Checks if this routing point is compatible with another point for routing.
    /// </summary>
    /// <param name="other">The other routing point.</param>
    /// <returns>True if routing between the points is valid.</returns>
    public bool IsCompatibleWith(RoutingPoint other)
    {
        if (other == null)
            return false;

        // Cannot route to self
        if (PointId == other.PointId)
            return false;

        // Check type compatibility
        // Source must be able to output, destination must be able to receive
        return CanBeSource && other.CanBeDestination;
    }

    /// <summary>
    /// Creates a display string for this routing point.
    /// </summary>
    public override string ToString()
    {
        string trackInfo = string.IsNullOrEmpty(AssociatedTrackId) ? "" : $" [{AssociatedTrackId}]";
        string effectInfo = string.IsNullOrEmpty(AssociatedEffectName) ? "" : $" ({AssociatedEffectName})";
        return $"{Name}{trackInfo}{effectInfo} ({Type}, {ChannelCount}ch)";
    }

    /// <summary>
    /// Determines whether this routing point equals another.
    /// </summary>
    public bool Equals(RoutingPoint? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return PointId == other.PointId;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as RoutingPoint);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return PointId.GetHashCode();
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(RoutingPoint? left, RoutingPoint? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(RoutingPoint? left, RoutingPoint? right)
    {
        return !(left == right);
    }
}


/// <summary>
/// Factory methods for creating common routing point configurations.
/// </summary>
public static class RoutingPointFactory
{
    /// <summary>
    /// Creates a track output routing point.
    /// </summary>
    /// <param name="trackId">The track identifier.</param>
    /// <param name="trackName">The track name.</param>
    /// <param name="sampleProvider">The track's sample provider.</param>
    /// <param name="channels">Number of channels (default: 2).</param>
    public static RoutingPoint CreateTrackOutput(string trackId, string trackName,
        ISampleProvider? sampleProvider = null, int channels = 2)
    {
        return new RoutingPoint($"{trackName} Out", RoutingPointType.Output, channels)
        {
            AssociatedTrackId = trackId,
            SampleProvider = sampleProvider,
            WaveFormat = sampleProvider?.WaveFormat
        };
    }

    /// <summary>
    /// Creates a sidechain input routing point.
    /// </summary>
    /// <param name="effectName">The name of the sidechainable effect.</param>
    /// <param name="channels">Number of channels (default: 2).</param>
    public static RoutingPoint CreateSidechainInput(string effectName, int channels = 2)
    {
        return new RoutingPoint($"{effectName} SC In", RoutingPointType.Sidechain, channels)
        {
            AssociatedEffectName = effectName
        };
    }

    /// <summary>
    /// Creates a send routing point.
    /// </summary>
    /// <param name="sendName">The name of the send.</param>
    /// <param name="trackId">The associated track identifier.</param>
    /// <param name="channels">Number of channels (default: 2).</param>
    public static RoutingPoint CreateSend(string sendName, string trackId, int channels = 2)
    {
        return new RoutingPoint($"{sendName}", RoutingPointType.Send, channels)
        {
            AssociatedTrackId = trackId
        };
    }

    /// <summary>
    /// Creates a return routing point.
    /// </summary>
    /// <param name="returnName">The name of the return.</param>
    /// <param name="channels">Number of channels (default: 2).</param>
    public static RoutingPoint CreateReturn(string returnName, int channels = 2)
    {
        return new RoutingPoint($"{returnName}", RoutingPointType.Return, channels);
    }

    /// <summary>
    /// Creates a group bus routing point.
    /// </summary>
    /// <param name="groupName">The name of the group.</param>
    /// <param name="channels">Number of channels (default: 2).</param>
    public static RoutingPoint CreateGroup(string groupName, int channels = 2)
    {
        return new RoutingPoint($"{groupName} Bus", RoutingPointType.Group, channels);
    }

    /// <summary>
    /// Creates a master output routing point.
    /// </summary>
    /// <param name="channels">Number of channels (default: 2).</param>
    public static RoutingPoint CreateMaster(int channels = 2)
    {
        return new RoutingPoint("Master", RoutingPointType.Master, channels);
    }
}
