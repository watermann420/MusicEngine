// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio/MIDI routing component.

namespace MusicEngine.Core.Routing;

/// <summary>
/// Defines the type/position of a channel in a surround sound configuration.
/// </summary>
public enum SurroundChannelType
{
    /// <summary>Front Left speaker (typically at -30 degrees)</summary>
    Left,

    /// <summary>Front Center speaker (0 degrees)</summary>
    Center,

    /// <summary>Front Right speaker (typically at +30 degrees)</summary>
    Right,

    /// <summary>Left Surround speaker (typically at -110 degrees) - used in 5.1 and Quad</summary>
    LeftSurround,

    /// <summary>Right Surround speaker (typically at +110 degrees) - used in 5.1 and Quad</summary>
    RightSurround,

    /// <summary>Left Side Surround speaker (typically at -90 degrees) - used in 7.1</summary>
    LeftSideSurround,

    /// <summary>Right Side Surround speaker (typically at +90 degrees) - used in 7.1</summary>
    RightSideSurround,

    /// <summary>Left Rear Surround speaker (typically at -150 degrees) - used in 7.1</summary>
    LeftRearSurround,

    /// <summary>Right Rear Surround speaker (typically at +150 degrees) - used in 7.1</summary>
    RightRearSurround,

    /// <summary>Low Frequency Effects channel (subwoofer)</summary>
    LFE,

    /// <summary>Top Front Left height speaker - used in Atmos</summary>
    TopFrontLeft,

    /// <summary>Top Front Right height speaker - used in Atmos</summary>
    TopFrontRight,

    /// <summary>Top Rear Left height speaker - used in Atmos</summary>
    TopRearLeft,

    /// <summary>Top Rear Right height speaker - used in Atmos</summary>
    TopRearRight
}

/// <summary>
/// Represents a single channel in a surround sound configuration.
/// Contains position information (azimuth, elevation) and level control.
/// </summary>
public class SurroundChannel
{
    /// <summary>
    /// The type of this channel (Left, Center, Right, etc.)
    /// </summary>
    public SurroundChannelType Type { get; }

    /// <summary>
    /// Horizontal angle in degrees (-180 to 180).
    /// 0 = front center, -90 = left, +90 = right, +/-180 = rear
    /// </summary>
    public float Azimuth { get; }

    /// <summary>
    /// Vertical angle in degrees (-90 to 90).
    /// 0 = ear level, +90 = directly above, -90 = below
    /// </summary>
    public float Elevation { get; }

    /// <summary>
    /// Output level for this channel (0.0 to 1.0).
    /// Used for trim/calibration.
    /// </summary>
    public float Level { get; set; } = 1.0f;

    /// <summary>
    /// Indicates if this is the LFE (subwoofer) channel.
    /// </summary>
    public bool IsLFE => Type == SurroundChannelType.LFE;

    /// <summary>
    /// Creates a new surround channel with the specified type.
    /// Position is automatically assigned based on standard speaker layouts.
    /// </summary>
    public SurroundChannel(SurroundChannelType type)
    {
        Type = type;
        (Azimuth, Elevation) = GetStandardPosition(type);
    }

    /// <summary>
    /// Creates a new surround channel with custom position.
    /// </summary>
    public SurroundChannel(SurroundChannelType type, float azimuth, float elevation)
    {
        Type = type;
        Azimuth = Math.Clamp(azimuth, -180f, 180f);
        Elevation = Math.Clamp(elevation, -90f, 90f);
    }

    /// <summary>
    /// Gets the standard ITU-R BS.775 / Dolby speaker position for a channel type.
    /// Returns (azimuth, elevation) in degrees.
    /// </summary>
    public static (float Azimuth, float Elevation) GetStandardPosition(SurroundChannelType type) => type switch
    {
        SurroundChannelType.Left => (-30f, 0f),
        SurroundChannelType.Center => (0f, 0f),
        SurroundChannelType.Right => (30f, 0f),
        SurroundChannelType.LeftSurround => (-110f, 0f),
        SurroundChannelType.RightSurround => (110f, 0f),
        SurroundChannelType.LeftSideSurround => (-90f, 0f),
        SurroundChannelType.RightSideSurround => (90f, 0f),
        SurroundChannelType.LeftRearSurround => (-150f, 0f),
        SurroundChannelType.RightRearSurround => (150f, 0f),
        SurroundChannelType.LFE => (0f, 0f), // LFE has no directional position
        SurroundChannelType.TopFrontLeft => (-45f, 45f),
        SurroundChannelType.TopFrontRight => (45f, 45f),
        SurroundChannelType.TopRearLeft => (-135f, 45f),
        SurroundChannelType.TopRearRight => (135f, 45f),
        _ => (0f, 0f)
    };

    /// <summary>
    /// Converts azimuth and elevation to a unit direction vector (x, y, z).
    /// Uses a right-handed coordinate system: +X = right, +Y = up, +Z = front.
    /// </summary>
    public (float X, float Y, float Z) ToUnitVector()
    {
        float azimuthRad = Azimuth * MathF.PI / 180f;
        float elevationRad = Elevation * MathF.PI / 180f;

        float cosElevation = MathF.Cos(elevationRad);
        float x = MathF.Sin(azimuthRad) * cosElevation;
        float y = MathF.Sin(elevationRad);
        float z = MathF.Cos(azimuthRad) * cosElevation;

        return (x, y, z);
    }

    /// <summary>
    /// Creates an array of SurroundChannel objects for the specified format.
    /// </summary>
    public static SurroundChannel[] CreateChannelsForFormat(SurroundFormat format)
    {
        var channelTypes = format.GetChannelTypes();
        var channels = new SurroundChannel[channelTypes.Length];

        for (int i = 0; i < channelTypes.Length; i++)
        {
            channels[i] = new SurroundChannel(channelTypes[i]);
        }

        return channels;
    }
}
