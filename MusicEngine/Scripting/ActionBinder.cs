// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Utility to bind MIDI values to method calls.

using System;
using System.Reflection;

namespace MusicEngine.Scripting;

internal static class ActionBinder
{
    /// <summary>
    /// Bind a rising-edge trigger to a parameterless method.
    /// </summary>
    public static Action<float> Trigger(object target, string method)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (string.IsNullOrWhiteSpace(method)) throw new ArgumentException("Method name is required.", nameof(method));

        var invoker = ResolveNoParam(target.GetType(), method);
        bool lastHigh = false;
        return value =>
        {
            bool high = value > 0.5f;
            if (high && !lastHigh)
            {
                invoker(target);
            }
            lastHigh = high;
        };
    }

    /// <summary>
    /// Bind a normalized value to a single-parameter method with min/max scaling.
    /// </summary>
    public static Action<float> Call(object target, string method, float min, float max)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (string.IsNullOrWhiteSpace(method)) throw new ArgumentException("Method name is required.", nameof(method));

        var invoker = ResolveSingleParam(target.GetType(), method, out var paramType);
        return value =>
        {
            float scaled = min + value * (max - min);
            invoker(target, ConvertValue(scaled, paramType));
        };
    }

    /// <summary>
    /// Bind a normalized value to a single-parameter method with a custom mapper.
    /// </summary>
    public static Action<float> Call(object target, string method, Func<float, float> map)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (string.IsNullOrWhiteSpace(method)) throw new ArgumentException("Method name is required.", nameof(method));
        if (map == null) throw new ArgumentNullException(nameof(map));

        var invoker = ResolveSingleParam(target.GetType(), method, out var paramType);
        return value =>
        {
            float mapped = map(value);
            invoker(target, ConvertValue(mapped, paramType));
        };
    }

    /// <summary>
    /// Toggle a boolean property/field on rising edge.
    /// </summary>
    public static Action<float> Toggle(object target, string member)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (string.IsNullOrWhiteSpace(member)) throw new ArgumentException("Member name is required.", nameof(member));

        var (getter, setter, memberType) = ResolveBoolMember(target.GetType(), member);
        bool lastHigh = false;
        return value =>
        {
            bool high = value > 0.5f;
            if (high && !lastHigh)
            {
                bool current = (bool)getter(target)!;
                setter(target, !current);
            }
            lastHigh = high;
        };
    }

    /// <summary>
    /// Switch a boolean property/field based on the current value.
    /// </summary>
    public static Action<float> Switch(object target, string member)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (string.IsNullOrWhiteSpace(member)) throw new ArgumentException("Member name is required.", nameof(member));

        var (getter, setter, memberType) = ResolveBoolMember(target.GetType(), member);
        _ = getter;
        return value =>
        {
            setter(target, value > 0.5f);
        };
    }

    /// <summary>
    /// Toggle a boolean getter/setter on rising edge.
    /// </summary>
    public static Action<float> Toggle(Func<bool> getter, Action<bool> setter)
    {
        if (getter == null) throw new ArgumentNullException(nameof(getter));
        if (setter == null) throw new ArgumentNullException(nameof(setter));

        bool lastHigh = false;
        return value =>
        {
            bool high = value > 0.5f;
            if (high && !lastHigh)
            {
                setter(!getter());
            }
            lastHigh = high;
        };
    }

    /// <summary>
    /// Switch a boolean getter/setter based on the current value.
    /// </summary>
    public static Action<float> Switch(Func<bool> getter, Action<bool> setter)
    {
        if (getter == null) throw new ArgumentNullException(nameof(getter));
        if (setter == null) throw new ArgumentNullException(nameof(setter));

        _ = getter;
        return value => setter(value > 0.5f);
    }

    private static Action<object> ResolveNoParam(Type type, string method)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase;
        var info = type.GetMethod(method, flags, binder: null, Type.EmptyTypes, modifiers: null);
        if (info == null)
        {
            throw new InvalidOperationException($"Method '{method}' not found on {type.Name}.");
        }
        return target => info.Invoke(target, Array.Empty<object>());
    }

    private static Action<object, object?> ResolveSingleParam(Type type, string method, out Type paramType)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase;
        foreach (var info in type.GetMethods(flags))
        {
            if (!string.Equals(info.Name, method, StringComparison.OrdinalIgnoreCase)) continue;
            var parameters = info.GetParameters();
            if (parameters.Length != 1) continue;
            paramType = parameters[0].ParameterType;
            return (target, value) => info.Invoke(target, new[] { value });
        }

        throw new InvalidOperationException($"Method '{method}' with one parameter not found on {type.Name}.");
    }

    private static (Func<object, object?> getter, Action<object, object?> setter, Type memberType) ResolveBoolMember(
        Type type, string member)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase;
        var prop = type.GetProperty(member, flags);
        if (prop != null && prop.CanRead && prop.CanWrite && prop.PropertyType == typeof(bool))
        {
            return (obj => prop.GetValue(obj), (obj, value) => prop.SetValue(obj, value), prop.PropertyType);
        }

        var field = type.GetField(member, flags);
        if (field != null && !field.IsInitOnly && field.FieldType == typeof(bool))
        {
            return (obj => field.GetValue(obj), (obj, value) => field.SetValue(obj, value), field.FieldType);
        }

        throw new InvalidOperationException($"Boolean member '{member}' not found or not writable on {type.Name}.");
    }

    private static object? ConvertValue(float value, Type targetType)
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
            int index = (int)Math.Round(value);
            index = Math.Clamp(index, 0, maxIndex);
            return values.GetValue(index);
        }

        return Convert.ChangeType(value, targetType);
    }
}
