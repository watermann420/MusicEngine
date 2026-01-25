//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Audio route representing a connection between two routing points with gain and latency compensation.


using NAudio.Wave;
using MusicEngine.Core.PDC;


namespace MusicEngine.Core.Routing;


/// <summary>
/// Specifies the type of audio route.
/// </summary>
public enum RouteType
{
    /// <summary>
    /// A standard audio signal route.
    /// </summary>
    Audio,

    /// <summary>
    /// A sidechain control signal route.
    /// </summary>
    Sidechain,

    /// <summary>
    /// A send/return effects route.
    /// </summary>
    SendReturn,

    /// <summary>
    /// A group bus route.
    /// </summary>
    Group
}


/// <summary>
/// Represents an audio connection between two routing points.
/// Supports gain control, enable/disable, and latency compensation.
/// </summary>
public class AudioRoute : IEquatable<AudioRoute>
{
    private readonly object _lock = new();
    private float _gainDb;
    private float _gainLinear;
    private bool _enabled;
    private int _latencyCompensationSamples;

    /// <summary>
    /// Creates a new audio route between two routing points.
    /// </summary>
    /// <param name="source">The source routing point.</param>
    /// <param name="destination">The destination routing point.</param>
    /// <param name="gainDb">Initial gain in decibels (default: 0 dB).</param>
    /// <exception cref="ArgumentNullException">Thrown if source or destination is null.</exception>
    /// <exception cref="ArgumentException">Thrown if routing is not valid between the points.</exception>
    public AudioRoute(RoutingPoint source, RoutingPoint destination, float gainDb = 0f)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Destination = destination ?? throw new ArgumentNullException(nameof(destination));

        if (!source.IsCompatibleWith(destination))
        {
            throw new ArgumentException(
                $"Cannot create route from {source.Name} ({source.Type}) to {destination.Name} ({destination.Type}). " +
                "Source must be output-type and destination must be input-type.");
        }

        RouteId = Guid.NewGuid();
        _gainDb = Math.Clamp(gainDb, -96f, 24f);
        _gainLinear = DbToLinear(_gainDb);
        _enabled = true;
        _latencyCompensationSamples = 0;

        // Determine route type based on destination
        Type = destination.Type switch
        {
            RoutingPointType.Sidechain => RouteType.Sidechain,
            RoutingPointType.Return => RouteType.SendReturn,
            RoutingPointType.Group => RouteType.Group,
            _ => RouteType.Audio
        };
    }

    /// <summary>
    /// Gets the unique identifier for this route.
    /// </summary>
    public Guid RouteId { get; }

    /// <summary>
    /// Gets the source routing point.
    /// </summary>
    public RoutingPoint Source { get; }

    /// <summary>
    /// Gets the destination routing point.
    /// </summary>
    public RoutingPoint Destination { get; }

    /// <summary>
    /// Gets the type of this route.
    /// </summary>
    public RouteType Type { get; }

    /// <summary>
    /// Gets or sets the route gain in decibels (-96 to +24 dB).
    /// </summary>
    public float GainDb
    {
        get
        {
            lock (_lock)
            {
                return _gainDb;
            }
        }
        set
        {
            lock (_lock)
            {
                _gainDb = Math.Clamp(value, -96f, 24f);
                _gainLinear = DbToLinear(_gainDb);
            }
        }
    }

    /// <summary>
    /// Gets the route gain as a linear multiplier.
    /// </summary>
    public float GainLinear
    {
        get
        {
            lock (_lock)
            {
                return _gainLinear;
            }
        }
    }

    /// <summary>
    /// Gets or sets whether this route is enabled.
    /// Disabled routes do not pass audio.
    /// </summary>
    public bool Enabled
    {
        get
        {
            lock (_lock)
            {
                return _enabled;
            }
        }
        set
        {
            lock (_lock)
            {
                _enabled = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets the latency compensation delay in samples.
    /// Used for plugin delay compensation (PDC).
    /// </summary>
    public int LatencyCompensationSamples
    {
        get
        {
            lock (_lock)
            {
                return _latencyCompensationSamples;
            }
        }
        set
        {
            lock (_lock)
            {
                _latencyCompensationSamples = Math.Max(0, value);
            }
        }
    }

    /// <summary>
    /// Gets the latency compensation delay in milliseconds.
    /// </summary>
    /// <param name="sampleRate">The sample rate to use for conversion.</param>
    public double GetLatencyCompensationMs(int sampleRate)
    {
        if (sampleRate <= 0) return 0;
        lock (_lock)
        {
            return (_latencyCompensationSamples * 1000.0) / sampleRate;
        }
    }

    /// <summary>
    /// Gets or sets whether this route is pre-fader.
    /// Pre-fader routes are not affected by the source track's fader.
    /// </summary>
    public bool PreFader { get; set; }

    /// <summary>
    /// Gets or sets whether this route is pre-insert.
    /// Pre-insert routes tap the signal before insert effects.
    /// </summary>
    public bool PreInsert { get; set; }

    /// <summary>
    /// Gets or sets arbitrary user data associated with this route.
    /// </summary>
    public object? Tag { get; set; }

    /// <summary>
    /// Gets or sets the display color for UI visualization (as ARGB int).
    /// </summary>
    public int DisplayColor { get; set; } = unchecked((int)0xFF4488FF); // Default blue

    /// <summary>
    /// Compensation delay buffer for PDC.
    /// </summary>
    public DelayCompensationBuffer? CompensationBuffer { get; set; }

    /// <summary>
    /// Calculates the effective gain considering enabled state.
    /// </summary>
    /// <returns>The effective gain multiplier (0 if disabled).</returns>
    public float GetEffectiveGain()
    {
        lock (_lock)
        {
            if (!_enabled || !Source.IsActive || !Destination.IsActive)
            {
                return 0f;
            }

            return _gainLinear;
        }
    }

    /// <summary>
    /// Processes audio through this route, applying gain.
    /// </summary>
    /// <param name="input">Input buffer.</param>
    /// <param name="output">Output buffer.</param>
    /// <param name="offset">Offset into buffers.</param>
    /// <param name="count">Number of samples to process.</param>
    public void ProcessRoute(float[] input, float[] output, int offset, int count)
    {
        float gain = GetEffectiveGain();

        if (gain <= 0f)
        {
            // Route is disabled or muted
            Array.Clear(output, offset, count);
            return;
        }

        // Apply latency compensation if configured
        float[] processInput = input;
        if (CompensationBuffer != null && _latencyCompensationSamples > 0)
        {
            processInput = new float[count];
            CompensationBuffer.Process(input, processInput, offset, count);
            offset = 0; // Reset offset since we're using a new buffer
        }

        // Apply gain
        if (Math.Abs(gain - 1f) < 0.0001f)
        {
            // Unity gain - direct copy
            Array.Copy(processInput, offset, output, offset, count);
        }
        else
        {
            // Apply gain scaling
            for (int i = 0; i < count; i++)
            {
                output[offset + i] = processInput[offset + i] * gain;
            }
        }
    }

    /// <summary>
    /// Adds audio from this route to an existing buffer (for mixing).
    /// </summary>
    /// <param name="input">Input buffer.</param>
    /// <param name="output">Output buffer to mix into.</param>
    /// <param name="offset">Offset into buffers.</param>
    /// <param name="count">Number of samples to process.</param>
    public void MixIntoBuffer(float[] input, float[] output, int offset, int count)
    {
        float gain = GetEffectiveGain();

        if (gain <= 0f)
        {
            return; // Nothing to add
        }

        // Apply latency compensation if configured
        float[] processInput = input;
        int processOffset = offset;
        if (CompensationBuffer != null && _latencyCompensationSamples > 0)
        {
            processInput = new float[count];
            CompensationBuffer.Process(input, processInput, offset, count);
            processOffset = 0;
        }

        // Mix with gain
        for (int i = 0; i < count; i++)
        {
            output[offset + i] += processInput[processOffset + i] * gain;
        }
    }

    /// <summary>
    /// Resets the latency compensation buffer.
    /// </summary>
    public void ResetCompensationBuffer()
    {
        CompensationBuffer?.Clear();
    }

    /// <summary>
    /// Converts decibels to linear gain.
    /// </summary>
    private static float DbToLinear(float db)
    {
        if (db <= -96f) return 0f;
        return MathF.Pow(10f, db / 20f);
    }

    /// <summary>
    /// Converts linear gain to decibels.
    /// </summary>
    public static float LinearToDb(float linear)
    {
        if (linear <= 0f) return -96f;
        return 20f * MathF.Log10(linear);
    }

    /// <summary>
    /// Creates a display string for this route.
    /// </summary>
    public override string ToString()
    {
        string enabledStr = _enabled ? "" : " [DISABLED]";
        return $"{Source.Name} -> {Destination.Name} ({_gainDb:F1} dB){enabledStr}";
    }

    /// <summary>
    /// Determines whether this route equals another.
    /// </summary>
    public bool Equals(AudioRoute? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return RouteId == other.RouteId;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as AudioRoute);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return RouteId.GetHashCode();
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(AudioRoute? left, AudioRoute? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(AudioRoute? left, AudioRoute? right)
    {
        return !(left == right);
    }
}


/// <summary>
/// Event arguments for route-related events.
/// </summary>
public class RouteEventArgs : EventArgs
{
    /// <summary>
    /// Creates new route event arguments.
    /// </summary>
    public RouteEventArgs(AudioRoute route, string action)
    {
        Route = route;
        Action = action;
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// The route associated with the event.
    /// </summary>
    public AudioRoute Route { get; }

    /// <summary>
    /// The action that occurred (Created, Removed, Modified, etc.).
    /// </summary>
    public string Action { get; }

    /// <summary>
    /// When the event occurred.
    /// </summary>
    public DateTime Timestamp { get; }
}
