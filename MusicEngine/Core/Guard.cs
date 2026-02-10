// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Minimal guard helpers.

using System.Runtime.CompilerServices;

namespace MusicEngine.Core;

/// <summary>
/// Small guard helpers for argument validation.
/// </summary>
public static class Guard
{
    /// <summary>
    /// Ensure a value stays within an inclusive range.
    /// </summary>
    /// <typeparam name="T">Comparable value type.</typeparam>
    /// <param name="value">Value to validate.</param>
    /// <param name="min">Inclusive minimum.</param>
    /// <param name="max">Inclusive maximum.</param>
    /// <param name="paramName">Parameter name for exceptions.</param>
    /// <returns>The validated value.</returns>
    public static T InRange<T>(T value, T min, T max, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : IComparable<T>
        => value.CompareTo(min) < 0 || value.CompareTo(max) > 0
            ? throw new ArgumentOutOfRangeException(paramName, value, $"Value must be between {min} and {max}")
            : value;

    /// <summary>
    /// Ensure a value is not negative.
    /// </summary>
    /// <param name="value">Value to validate.</param>
    /// <param name="paramName">Parameter name for exceptions.</param>
    /// <returns>The validated value.</returns>
    public static double NotNegative(double value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        => value < 0
            ? throw new ArgumentOutOfRangeException(paramName, value, "Value cannot be negative")
            : value;
}
