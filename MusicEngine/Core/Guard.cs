// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using System.Runtime.CompilerServices;

namespace MusicEngine.Core;

public static class Guard
{
    public static T NotNull<T>(T? value, [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : class
        => value ?? throw new ArgumentNullException(paramName);

    public static T InRange<T>(T value, T min, T max, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : IComparable<T>
        => value.CompareTo(min) < 0 || value.CompareTo(max) > 0
            ? throw new ArgumentOutOfRangeException(paramName, value, $"Value must be between {min} and {max}")
            : value;

    public static string NotNullOrEmpty(string? value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        => string.IsNullOrEmpty(value)
            ? throw new ArgumentException("Value cannot be null or empty", paramName)
            : value;

    public static T NotDefault<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : struct
        => EqualityComparer<T>.Default.Equals(value, default)
            ? throw new ArgumentException("Value cannot be default", paramName)
            : value;

    public static int NotNegative(int value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        => value < 0
            ? throw new ArgumentOutOfRangeException(paramName, value, "Value cannot be negative")
            : value;

    public static double NotNegative(double value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        => value < 0
            ? throw new ArgumentOutOfRangeException(paramName, value, "Value cannot be negative")
            : value;
}
