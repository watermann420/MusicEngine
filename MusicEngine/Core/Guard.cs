// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Minimal guard helpers.

using System.Runtime.CompilerServices;

namespace MusicEngine.Core;

public static class Guard
{
    public static T InRange<T>(T value, T min, T max, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : IComparable<T>
        => value.CompareTo(min) < 0 || value.CompareTo(max) > 0
            ? throw new ArgumentOutOfRangeException(paramName, value, $"Value must be between {min} and {max}")
            : value;

    public static double NotNegative(double value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        => value < 0
            ? throw new ArgumentOutOfRangeException(paramName, value, "Value cannot be negative")
            : value;
}
