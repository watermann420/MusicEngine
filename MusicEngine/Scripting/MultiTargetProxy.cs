using System;
using System.Dynamic;
using System.Linq;
using System.Reflection;

namespace MusicEngine.Scripting;

internal sealed class MultiTargetProxy : DynamicObject
{
    private readonly object[] _targets;

    public MultiTargetProxy(params object?[] targets)
    {
        _targets = targets.Where(target => target != null).ToArray()!;
    }

    public override bool TryInvokeMember(InvokeMemberBinder binder, object?[]? args, out object? result)
    {
        result = null;
        if (_targets.Length == 0)
        {
            return true;
        }

        if (args == null)
        {
            return true;
        }

        foreach (var target in _targets)
        {
            var method = FindMethod(target, binder.Name, args, out var convertedArgs);
            if (method == null)
            {
                continue;
            }

            result = method.Invoke(target, convertedArgs);
        }

        return true;
    }

    private static MethodInfo? FindMethod(object target, string name, object?[] args, out object?[] convertedArgs)
    {
        convertedArgs = Array.Empty<object?>();
        var type = target.GetType();
        var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public);
        foreach (var method in methods)
        {
            if (!string.Equals(method.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parameters = method.GetParameters();
            if (parameters.Length != args.Length)
            {
                continue;
            }

            if (TryConvertArgs(args, parameters, out var converted))
            {
                convertedArgs = converted;
                return method;
            }
        }

        return null;
    }

    private static bool TryConvertArgs(object?[] args, ParameterInfo[] parameters, out object?[] convertedArgs)
    {
        convertedArgs = new object?[args.Length];
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            var targetType = parameters[i].ParameterType;
            if (arg == null)
            {
                if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null)
                {
                    return false;
                }
                convertedArgs[i] = null;
                continue;
            }

            if (targetType.IsInstanceOfType(arg))
            {
                convertedArgs[i] = arg;
                continue;
            }

            try
            {
                convertedArgs[i] = Convert.ChangeType(arg, targetType);
            }
            catch (Exception)
            {
                return false;
            }
        }

        return true;
    }
}
