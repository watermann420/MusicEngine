namespace MusicEngine.Core.Routing;

/// <summary>
/// Surround sound format definitions.
/// Defines standard speaker configurations from stereo to immersive audio.
/// </summary>
public enum SurroundFormat
{
    /// <summary>
    /// 2.0 - Left, Right
    /// </summary>
    Stereo,

    /// <summary>
    /// 3.0 - Left, Center, Right
    /// </summary>
    LCR,

    /// <summary>
    /// 4.0 - Left, Right, Left Surround, Right Surround
    /// </summary>
    Quad,

    /// <summary>
    /// 5.1 - Left, Center, Right, Left Surround, Right Surround, LFE
    /// </summary>
    Surround_5_1,

    /// <summary>
    /// 7.1 - Left, Center, Right, Left Side Surround, Right Side Surround, Left Rear Surround, Right Rear Surround, LFE
    /// </summary>
    Surround_7_1,

    /// <summary>
    /// 7.1.4 - 7.1 + 4 height channels (Top Front Left/Right, Top Rear Left/Right)
    /// </summary>
    Atmos_7_1_4
}

/// <summary>
/// Extension methods for SurroundFormat enum.
/// </summary>
public static class SurroundFormatExtensions
{
    /// <summary>
    /// Gets the total number of channels for this surround format.
    /// </summary>
    public static int GetChannelCount(this SurroundFormat format) => format switch
    {
        SurroundFormat.Stereo => 2,
        SurroundFormat.LCR => 3,
        SurroundFormat.Quad => 4,
        SurroundFormat.Surround_5_1 => 6,
        SurroundFormat.Surround_7_1 => 8,
        SurroundFormat.Atmos_7_1_4 => 12,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown surround format")
    };

    /// <summary>
    /// Gets the channel names for this surround format in order.
    /// </summary>
    public static string[] GetChannelNames(this SurroundFormat format) => format switch
    {
        SurroundFormat.Stereo => ["L", "R"],
        SurroundFormat.LCR => ["L", "C", "R"],
        SurroundFormat.Quad => ["L", "R", "Ls", "Rs"],
        SurroundFormat.Surround_5_1 => ["L", "C", "R", "Ls", "Rs", "LFE"],
        SurroundFormat.Surround_7_1 => ["L", "C", "R", "Lss", "Rss", "Lsr", "Rsr", "LFE"],
        SurroundFormat.Atmos_7_1_4 => ["L", "C", "R", "Lss", "Rss", "Lsr", "Rsr", "LFE", "TFL", "TFR", "TRL", "TRR"],
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown surround format")
    };

    /// <summary>
    /// Gets the index of the LFE channel for this format, or -1 if none exists.
    /// </summary>
    public static int GetLFEChannel(this SurroundFormat format) => format switch
    {
        SurroundFormat.Stereo => -1,
        SurroundFormat.LCR => -1,
        SurroundFormat.Quad => -1,
        SurroundFormat.Surround_5_1 => 5,
        SurroundFormat.Surround_7_1 => 7,
        SurroundFormat.Atmos_7_1_4 => 7,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown surround format")
    };

    /// <summary>
    /// Gets the SurroundChannelType for each channel index in the format.
    /// </summary>
    public static SurroundChannelType[] GetChannelTypes(this SurroundFormat format) => format switch
    {
        SurroundFormat.Stereo =>
            [SurroundChannelType.Left, SurroundChannelType.Right],
        SurroundFormat.LCR =>
            [SurroundChannelType.Left, SurroundChannelType.Center, SurroundChannelType.Right],
        SurroundFormat.Quad =>
            [SurroundChannelType.Left, SurroundChannelType.Right,
             SurroundChannelType.LeftSurround, SurroundChannelType.RightSurround],
        SurroundFormat.Surround_5_1 =>
            [SurroundChannelType.Left, SurroundChannelType.Center, SurroundChannelType.Right,
             SurroundChannelType.LeftSurround, SurroundChannelType.RightSurround, SurroundChannelType.LFE],
        SurroundFormat.Surround_7_1 =>
            [SurroundChannelType.Left, SurroundChannelType.Center, SurroundChannelType.Right,
             SurroundChannelType.LeftSideSurround, SurroundChannelType.RightSideSurround,
             SurroundChannelType.LeftRearSurround, SurroundChannelType.RightRearSurround, SurroundChannelType.LFE],
        SurroundFormat.Atmos_7_1_4 =>
            [SurroundChannelType.Left, SurroundChannelType.Center, SurroundChannelType.Right,
             SurroundChannelType.LeftSideSurround, SurroundChannelType.RightSideSurround,
             SurroundChannelType.LeftRearSurround, SurroundChannelType.RightRearSurround, SurroundChannelType.LFE,
             SurroundChannelType.TopFrontLeft, SurroundChannelType.TopFrontRight,
             SurroundChannelType.TopRearLeft, SurroundChannelType.TopRearRight],
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown surround format")
    };

    /// <summary>
    /// Checks if this format has height channels (for immersive audio).
    /// </summary>
    public static bool HasHeightChannels(this SurroundFormat format) => format == SurroundFormat.Atmos_7_1_4;

    /// <summary>
    /// Checks if this format has an LFE channel.
    /// </summary>
    public static bool HasLFE(this SurroundFormat format) => format.GetLFEChannel() >= 0;
}
