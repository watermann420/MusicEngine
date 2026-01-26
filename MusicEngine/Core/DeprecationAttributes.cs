// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

namespace MusicEngine.Core;

/// <summary>
/// Indicates when a feature became obsolete.
/// </summary>
[AttributeUsage(AttributeTargets.All, Inherited = false)]
public sealed class ObsoleteSinceAttribute : Attribute
{
    /// <summary>
    /// Version when this became obsolete.
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// Suggested replacement.
    /// </summary>
    public string? Replacement { get; init; }

    /// <summary>
    /// Reason for deprecation.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Version when this will be removed.
    /// </summary>
    public string? RemovalVersion { get; init; }

    public ObsoleteSinceAttribute(string version) => Version = version;
}

/// <summary>
/// Indicates when a feature was introduced.
/// </summary>
[AttributeUsage(AttributeTargets.All, Inherited = false)]
public sealed class IntroducedInAttribute : Attribute
{
    /// <summary>
    /// Version when this was introduced.
    /// </summary>
    public string Version { get; }

    public IntroducedInAttribute(string version) => Version = version;
}

/// <summary>
/// Indicates an experimental feature that may change.
/// </summary>
[AttributeUsage(AttributeTargets.All, Inherited = false)]
public sealed class ExperimentalAttribute : Attribute
{
    /// <summary>
    /// Reason why this is experimental.
    /// </summary>
    public string? Reason { get; init; }
}
