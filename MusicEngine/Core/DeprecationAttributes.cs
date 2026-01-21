namespace MusicEngine.Core;

/// <summary>
/// Indicates when a feature became obsolete.
/// </summary>
[AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = false)]
public class ObsoleteSinceAttribute : Attribute
{
    /// <summary>
    /// Version when this became obsolete.
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// Suggested replacement.
    /// </summary>
    public string? Replacement { get; set; }

    /// <summary>
    /// Reason for deprecation.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Version when this will be removed.
    /// </summary>
    public string? RemovalVersion { get; set; }

    public ObsoleteSinceAttribute(string version)
    {
        Version = version;
    }
}

/// <summary>
/// Indicates when a feature was introduced.
/// </summary>
[AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = false)]
public class IntroducedInAttribute : Attribute
{
    /// <summary>
    /// Version when this was introduced.
    /// </summary>
    public string Version { get; }

    public IntroducedInAttribute(string version)
    {
        Version = version;
    }
}

/// <summary>
/// Indicates an experimental feature that may change.
/// </summary>
[AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = false)]
public class ExperimentalAttribute : Attribute
{
    /// <summary>
    /// Reason why this is experimental.
    /// </summary>
    public string? Reason { get; set; }
}
