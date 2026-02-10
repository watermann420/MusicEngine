// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Case-insensitive dynamic proxy for scripting APIs.

using System;
using System.Dynamic;
using System.Reflection;

namespace MusicEngine.Scripting;

internal sealed class CaseInsensitiveProxy : DynamicObject
{
    private readonly object _target;

    public CaseInsensitiveProxy(object target)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
    }

    public object Target => _target;

    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        var type = _target.GetType();
        var flags = BindingFlags.Instance | BindingFlags.Public;
        var prop = GetProperty(type, binder.Name, flags);
        if (prop != null)
        {
            result = Wrap(prop.GetValue(_target));
            return true;
        }

        var field = GetField(type, binder.Name, flags);
        if (field != null)
        {
            result = Wrap(field.GetValue(_target));
            return true;
        }

        result = null;
        return false;
    }

    public override bool TrySetMember(SetMemberBinder binder, object? value)
    {
        var type = _target.GetType();
        var flags = BindingFlags.Instance | BindingFlags.Public;
        var prop = GetProperty(type, binder.Name, flags);
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(_target, Unwrap(value));
            return true;
        }

        var field = GetField(type, binder.Name, flags);
        if (field != null)
        {
            field.SetValue(_target, Unwrap(value));
            return true;
        }

        return false;
    }

    public override bool TryInvokeMember(InvokeMemberBinder binder, object?[]? args, out object? result)
    {
        var type = _target.GetType();
        var flags = BindingFlags.Instance | BindingFlags.Public;
        var callArgs = args == null ? Array.Empty<object?>() : UnwrapArgs(args);

        var method = GetMethod(type, binder.Name, callArgs, flags);
        if (method == null)
        {
            result = null;
            return false;
        }

        result = Wrap(method.Invoke(_target, callArgs));
        return true;
    }

    public override bool TryConvert(ConvertBinder binder, out object? result)
    {
        if (binder.Type.IsInstanceOfType(_target))
        {
            result = _target;
            return true;
        }

        result = null;
        return false;
    }

    private static object?[] UnwrapArgs(object?[] args)
    {
        var unwrapped = new object?[args.Length];
        for (int i = 0; i < args.Length; i++)
        {
            unwrapped[i] = Unwrap(args[i]);
        }
        return unwrapped;
    }

    private static PropertyInfo? GetProperty(Type type, string name, BindingFlags flags)
    {
        var exact = type.GetProperty(name, flags);
        if (exact != null) return exact;

        foreach (var prop in type.GetProperties(flags))
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return prop;
            }
        }

        return null;
    }

    private static FieldInfo? GetField(Type type, string name, BindingFlags flags)
    {
        var exact = type.GetField(name, flags);
        if (exact != null) return exact;

        foreach (var field in type.GetFields(flags))
        {
            if (string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return field;
            }
        }

        return null;
    }

    private static MethodInfo? GetMethod(Type type, string name, object?[] args, BindingFlags flags)
    {
        var methods = type.GetMethods(flags);
        MethodInfo? best = null;
        int bestScore = -1;

        foreach (var method in methods)
        {
            if (!string.Equals(method.Name, name, StringComparison.Ordinal)) continue;
            var score = ScoreMethod(method, args);
            if (score > bestScore)
            {
                bestScore = score;
                best = method;
            }
        }

        if (best != null) return best;

        foreach (var method in methods)
        {
            if (!string.Equals(method.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
            var score = ScoreMethod(method, args);
            if (score > bestScore)
            {
                bestScore = score;
                best = method;
            }
        }

        return best;
    }

    private static int ScoreMethod(MethodInfo method, object?[] args)
    {
        var parameters = method.GetParameters();
        if (parameters.Length != args.Length) return -1;

        int score = 0;
        for (int i = 0; i < parameters.Length; i++)
        {
            var paramType = parameters[i].ParameterType;
            var arg = args[i];
            if (arg == null)
            {
                if (paramType.IsValueType && Nullable.GetUnderlyingType(paramType) == null)
                {
                    return -1;
                }
                score += 1;
                continue;
            }

            var argType = arg.GetType();
            if (paramType == argType)
            {
                score += 3;
                continue;
            }

            if (paramType.IsInstanceOfType(arg))
            {
                score += 2;
                continue;
            }

            if (arg is IConvertible && typeof(IConvertible).IsAssignableFrom(paramType))
            {
                score += 1;
                continue;
            }

            return -1;
        }

        return score;
    }

    private static object? Unwrap(object? value)
    {
        return value is CaseInsensitiveProxy proxy ? proxy._target : value;
    }

    private static object? Wrap(object? value)
    {
        if (value == null) return null;
        if (value is CaseInsensitiveProxy) return value;

        var type = value.GetType();
        if (type.IsPrimitive || type.IsEnum || value is string || value is decimal || value is Delegate ||
            value is System.Threading.Tasks.Task)
        {
            return value;
        }

        return new CaseInsensitiveProxy(value);
    }
}
