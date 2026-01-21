namespace MusicEngine.Core;

/// <summary>
/// Validation helper methods for common guard clauses.
/// </summary>
public static class Guard
{
    /// <summary>
    /// Throws ArgumentNullException if value is null.
    /// </summary>
    public static T NotNull<T>(T? value, string? paramName = null) where T : class
    {
        return value ?? throw new ArgumentNullException(paramName);
    }

    /// <summary>
    /// Throws ArgumentNullException if value is null (for nullable value types).
    /// </summary>
    public static T NotNull<T>(T? value, string? paramName = null) where T : struct
    {
        return value ?? throw new ArgumentNullException(paramName);
    }

    /// <summary>
    /// Throws ArgumentException if string is null or empty.
    /// </summary>
    public static string NotNullOrEmpty(string? value, string? paramName = null)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException("Value cannot be null or empty.", paramName);
        }
        return value;
    }

    /// <summary>
    /// Throws ArgumentException if string is null, empty, or whitespace.
    /// </summary>
    public static string NotNullOrWhiteSpace(string? value, string? paramName = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be null, empty, or whitespace.", paramName);
        }
        return value;
    }

    /// <summary>
    /// Throws ArgumentOutOfRangeException if value is outside the specified range.
    /// </summary>
    public static T InRange<T>(T value, T min, T max, string? paramName = null) where T : IComparable<T>
    {
        if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"Value must be between {min} and {max}.");
        }
        return value;
    }

    /// <summary>
    /// Throws ArgumentOutOfRangeException if value is negative.
    /// </summary>
    public static int NotNegative(int value, string? paramName = null)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Value cannot be negative.");
        }
        return value;
    }

    /// <summary>
    /// Throws ArgumentOutOfRangeException if value is not positive.
    /// </summary>
    public static int Positive(int value, string? paramName = null)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Value must be positive.");
        }
        return value;
    }

    /// <summary>
    /// Throws ArgumentOutOfRangeException if value is not positive.
    /// </summary>
    public static float Positive(float value, string? paramName = null)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Value must be positive.");
        }
        return value;
    }
}
