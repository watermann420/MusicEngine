// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Utility to bind MIDI values to object properties.

using System;
using System.Reflection;

namespace MusicEngine.Scripting;

internal static class PropertyBinder
{
    public static Action<float> Create(object target, string member, float min, float max)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (string.IsNullOrWhiteSpace(member)) throw new ArgumentException("Member name is required.", nameof(member));

        var (setter, memberType) = ResolveSetter(target.GetType(), member);
        return value =>
        {
            float scaled = min + value * (max - min);
            setter(target, ConvertValue(scaled, memberType, min, max));
        };
    }

    public static Action<float> Create(object target, string member, Func<float, float> map)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (string.IsNullOrWhiteSpace(member)) throw new ArgumentException("Member name is required.", nameof(member));
        if (map == null) throw new ArgumentNullException(nameof(map));

        var (setter, memberType) = ResolveSetter(target.GetType(), member);
        return value =>
        {
            float mapped = map(value);
            setter(target, ConvertValue(mapped, memberType, 0f, 1f));
        };
    }

    private static (Action<object, object?> setter, Type memberType) ResolveSetter(Type type, string member)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase;
        var prop = type.GetProperty(member, flags);
        if (prop != null && prop.CanWrite)
        {
            return ((obj, value) => prop.SetValue(obj, value), prop.PropertyType);
        }

        var field = type.GetField(member, flags);
        if (field != null && !field.IsInitOnly)
        {
            return ((obj, value) => field.SetValue(obj, value), field.FieldType);
        }

        throw new InvalidOperationException($"Member '{member}' not found or not writable on {type.Name}.");
    }

    private static object? ConvertValue(float value, Type targetType, float min, float max)
    {
        if (targetType == typeof(float)) return value;
        if (targetType == typeof(double)) return (double)value;
        if (targetType == typeof(int)) return (int)Math.Round(value);
        if (targetType == typeof(short)) return (short)Math.Round(value);
        if (targetType == typeof(long)) return (long)Math.Round(value);
        if (targetType == typeof(byte)) return (byte)Math.Max(byte.MinValue, Math.Min(byte.MaxValue, Math.Round(value)));
        if (targetType == typeof(bool)) return value > 0.5f;

        if (targetType.IsEnum)
        {
            var values = Enum.GetValues(targetType);
            int maxIndex = Math.Max(0, values.Length - 1);
            int index = (int)Math.Round(Math.Clamp(value, min, max));
            index = Math.Clamp(index, 0, maxIndex);
            return values.GetValue(index);
        }

        return Convert.ChangeType(value, targetType);
    }
}
