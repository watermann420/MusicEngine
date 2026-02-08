// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Script library for sharing objects and loading modules.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;

namespace MusicEngine.Scripting;

/// <summary>
/// Script library for sharing objects and loading modules across scripts.
/// </summary>
public sealed class ScriptLibrary : DynamicObject
{
    private readonly ScriptHost _host;
    private readonly Dictionary<string, object> _items = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    internal ScriptLibrary(ScriptHost host)
    {
        _host = host;
    }

    /// <summary>
    /// Store a shared value.
    /// </summary>
    public void Set(string name, object value)
    {
        if (string.IsNullOrWhiteSpace(name) || value == null) return;
        lock (_lock)
        {
            _items[name] = value;
        }
    }

    /// <summary>
    /// Get a shared value.
    /// </summary>
    public object? Get(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        lock (_lock)
        {
            return _items.TryGetValue(name, out var value) ? value : null;
        }
    }

    /// <summary>
    /// Get a shared value as a specific type.
    /// </summary>
    public T? Get<T>(string name) where T : class
    {
        return Get(name) as T;
    }

    /// <summary>
    /// Remove a shared value.
    /// </summary>
    public bool Remove(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        lock (_lock)
        {
            return _items.Remove(name);
        }
    }

    /// <summary>
    /// List all shared keys.
    /// </summary>
    public string[] List()
    {
        lock (_lock)
        {
            return _items.Keys.ToArray();
        }
    }

    /// <summary>
    /// Load and run a module script by name.
    /// </summary>
    public Task<bool> Use(string name)
    {
        return _host.ExecuteModuleAsync(name);
    }

    internal void Clear()
    {
        lock (_lock)
        {
            _items.Clear();
        }
    }

    public override bool TryInvokeMember(InvokeMemberBinder binder, object?[]? args, out object? result)
    {
        args ??= Array.Empty<object?>();

        if (args.Length == 0)
        {
            result = Get(binder.Name);
            return true;
        }

        if (args.Length == 1)
        {
            var value = args[0];
            if (value == null)
            {
                result = null;
                return true;
            }

            Set(binder.Name, value);
            result = value;
            return true;
        }

        result = null;
        return false;
    }

    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        result = Get(binder.Name);
        return true;
    }
}
