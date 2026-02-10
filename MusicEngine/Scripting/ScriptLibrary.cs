// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Script library for sharing objects and loading modules.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MusicEngine.Scripting;

/// <summary>
/// Script library for sharing objects and loading modules across scripts.
/// </summary>
public sealed class ScriptLibrary : DynamicObject
{
    private readonly ScriptHost _host;
    private readonly string? _scopeName;
    private readonly Dictionary<string, object> _items = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    internal ScriptLibrary(ScriptHost host, string? scopeName = null)
    {
        _host = host;
        _scopeName = scopeName;
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
    /// Create or access a named namespace scope.
    /// </summary>
    public ScriptLibrary Scope(string name) => GetOrCreateScope(name);

    /// <summary>
    /// Register a script alias and run it immediately.
    /// </summary>
    public ScriptLibrary File(string alias, string scriptName)
        => File(alias, scriptName, master: false);

    /// <summary>
    /// Register a script alias and optionally mark it as master.
    /// </summary>
    public ScriptLibrary File(string alias, string scriptName, bool master)
    {
        if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(scriptName))
        {
            return this;
        }

        return RegisterScript(alias, scriptName, master);
    }

    /// <summary>
    /// Run a script by name and bind it to a same-named scope.
    /// </summary>
    public ScriptLibrary File(string scriptName)
        => File(scriptName, scriptName, master: false);

    /// <summary>
    /// Get the scope for the current script file name.
    /// </summary>
    public ScriptLibrary File()
    {
        var path = _host.CurrentScriptFilePath;
        if (string.IsNullOrWhiteSpace(path)) return this;
        var name = Path.GetFileNameWithoutExtension(path);
        var alias = _host.GetAliasForScript(name) ?? name;
        _host.RegisterScriptAlias(alias, name);
        return GetOrCreateScope(alias);
    }

    /// <summary>
    /// Register the current script or an alias as main.
    /// </summary>
    public ScriptFileBuilder Main() => new ScriptFileBuilder(this, "Main", master: true);

    /// <summary>
    /// Register the current script or an alias as main.
    /// </summary>
    public ScriptFileBuilder main() => Main();

    /// <summary>
    /// Register a script for this scope.
    /// </summary>
    public ScriptLibrary Name(string scriptName)
    {
        var alias = _scopeName ?? scriptName;
        var resolved = ResolveScriptName(scriptName);
        if (!string.Equals(resolved, scriptName, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(resolved))
        {
            return RegisterScript(alias, resolved, master: false);
        }

        var currentName = _host.CurrentScriptFilePath == null
            ? null
            : Path.GetFileNameWithoutExtension(_host.CurrentScriptFilePath);
        if (!string.IsNullOrWhiteSpace(currentName) &&
            _host.ResolveScriptPath(scriptName) == null)
        {
            return RegisterScript(scriptName, currentName, master: false);
        }

        return RegisterScript(alias, resolved, master: false);
    }

    /// <summary>
    /// Register a script for this scope.
    /// </summary>
    public ScriptLibrary name(string scriptName) => Name(scriptName);

    /// <summary>
    /// Create or access a named namespace.
    /// </summary>
    public ScriptLibrary NameSpace(string name) => Scope(name);


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
        result = Get(binder.Name) ?? GetOrCreateScope(binder.Name);
        return true;
    }

    internal string? ScopeName => _scopeName;

    internal ScriptLibrary RegisterScript(string alias, string scriptName, bool master)
    {
        if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(scriptName))
        {
            return this;
        }

        _host.RegisterScriptAlias(alias, scriptName);
        if (master)
        {
            _host.RegisterMasterScript(scriptName);
        }

        var currentName = _host.CurrentScriptFilePath == null
            ? null
            : Path.GetFileNameWithoutExtension(_host.CurrentScriptFilePath);
        if (string.IsNullOrWhiteSpace(currentName) ||
            !string.Equals(currentName, scriptName, StringComparison.OrdinalIgnoreCase))
        {
            _ = _host.ExecuteModuleAsync(scriptName);
        }

        return GetOrCreateScope(alias);
    }

    internal string ResolveScriptName(string scriptName)
    {
        if (string.IsNullOrWhiteSpace(scriptName)) return scriptName;
        if (_host.ResolveScriptPath(scriptName) != null) return scriptName;
        if (!string.IsNullOrWhiteSpace(_scopeName)) return _scopeName;
        return scriptName;
    }

    internal string? HostCurrentScriptName
        => _host.CurrentScriptFilePath == null
            ? null
            : Path.GetFileNameWithoutExtension(_host.CurrentScriptFilePath);

    internal bool HostHasScript(string scriptName)
        => _host.ResolveScriptPath(scriptName) != null;

    public override bool TrySetMember(SetMemberBinder binder, object? value)
    {
        if (value == null)
        {
            Remove(binder.Name);
            return true;
        }

        Set(binder.Name, value);
        return true;
    }

    private ScriptLibrary GetOrCreateScope(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return this;
        lock (_lock)
        {
            if (_items.TryGetValue(name, out var existing) && existing is ScriptLibrary existingScope)
            {
                return existingScope;
            }

            var scope = new ScriptLibrary(_host, name);
            _items[name] = scope;
            return scope;
        }
    }
}

/// <summary>
/// Builder for script registration.
/// </summary>
public sealed class ScriptFileBuilder
{
    private readonly ScriptLibrary _library;
    private readonly string _alias;
    private readonly bool _master;

    public ScriptFileBuilder(ScriptLibrary library, string alias, bool master)
    {
        _library = library;
        _alias = alias;
        _master = master;
    }

    /// <summary>
    /// Register a script name for this alias.
    /// </summary>
    public ScriptLibrary Name(string scriptName)
    {
        var resolved = _library.ResolveScriptName(scriptName);
        var currentName = _library.HostCurrentScriptName;
        if (_master && !string.IsNullOrWhiteSpace(currentName) &&
            _library.HostHasScript(scriptName) == false)
        {
            _library.RegisterScript(_alias, currentName, master: true);
            return _library.RegisterScript(scriptName, currentName, master: false);
        }

        return _library.RegisterScript(_alias, resolved, _master);
    }

    /// <summary>
    /// Register a script name for this alias.
    /// </summary>
    public ScriptLibrary name(string scriptName) => Name(scriptName);

    /// <summary>
    /// Access a namespace scope.
    /// </summary>
    public ScriptLibrary NameSpace(string scope) => _library.Scope(scope);

}
