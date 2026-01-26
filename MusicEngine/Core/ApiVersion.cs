// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

namespace MusicEngine.Core;

/// <summary>
/// API version information for MusicEngine.
/// </summary>
public static class ApiVersion
{
    /// <summary>
    /// Major version number.
    /// </summary>
    public const int Major = 1;

    /// <summary>
    /// Minor version number.
    /// </summary>
    public const int Minor = 0;

    /// <summary>
    /// Patch version number.
    /// </summary>
    public const int Patch = 0;

    /// <summary>
    /// Full version object.
    /// </summary>
    public static Version Version { get; } = new(Major, Minor, Patch);

    /// <summary>
    /// Version string.
    /// </summary>
    public static string VersionString => $"{Major}.{Minor}.{Patch}";

    /// <summary>
    /// Determines if the specified version is compatible with this API.
    /// </summary>
    public static bool IsCompatible(Version version)
    {
        return version.Major == Major;
    }

    /// <summary>
    /// Determines if the specified major version is compatible.
    /// </summary>
    public static bool IsCompatible(int major)
    {
        return major == Major;
    }
}
