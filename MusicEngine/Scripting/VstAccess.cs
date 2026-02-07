// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Script helper for creating VST instruments.

using System;
using System.Collections.Generic;
using System.Dynamic;
using MusicEngine.Core;
using MusicEngine.Vst;

namespace MusicEngine.Scripting;

/// <summary>
/// Script helper for creating and reusing VST instruments/effects.
/// </summary>
public sealed class VstAccess : DynamicObject
{
    private ScriptGlobals _globals;
    private readonly Dictionary<string, Vst3Instrument> _instances = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Vst3Effect> _effects = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// When true, instances are kept across script refreshes.
    /// </summary>
    public bool KeepInstances { get; set; }

    /// <summary>
    /// Create a new VST access helper bound to script globals.
    /// </summary>
    public VstAccess(ScriptGlobals globals)
    {
        _globals = globals;
    }

    /// <summary>
    /// Update the globals reference after a script refresh.
    /// </summary>
    public void UpdateGlobals(ScriptGlobals globals)
    {
        _globals = globals;
    }

    /// <summary>
    /// Indexer access to VST instruments by name.
    /// </summary>
    public Vst3Instrument this[string name] => Get(name);

    /// <summary>
    /// Dynamic member access to VST instruments by name.
    /// </summary>
    public override bool TryGetMember(GetMemberBinder binder, out object result)
    {
        result = Get(binder.Name);
        return true;
    }

    /// <summary>
    /// Get or create a VST3 instrument by name.
    /// </summary>
    public Vst3Instrument Get(string name)
    {
        if (_instances.TryGetValue(name, out var existing))
        {
            _globals.Engine.AddSampleProvider(existing);
            return existing;
        }

        var registry = _globals.VstRegistry;
        if (registry == null)
        {
            throw new InvalidOperationException("VST registry not available. Ensure VST scan is enabled.");
        }

        var plugin = registry.FindByName(name);
        if (plugin == null)
        {
            throw new InvalidOperationException($"VST not found: {name}");
        }

        var instrument = new Vst3Instrument(plugin.Path, plugin.Name);
        _globals.Engine.AddSampleProvider(instrument);
        _instances[name] = instrument;
        return instrument;
    }

    /// <summary>
    /// Get or create a VST3 effect by name.
    /// </summary>
    public Vst3Effect GetEffect(string name)
    {
        if (_effects.TryGetValue(name, out var existing))
        {
            return existing;
        }

        var registry = _globals.VstRegistry;
        if (registry == null)
        {
            throw new InvalidOperationException("VST registry not available. Ensure VST scan is enabled.");
        }

        var plugin = registry.FindByName(name);
        if (plugin == null)
        {
            throw new InvalidOperationException($"VST not found: {name}");
        }

        var effect = new Vst3Effect(plugin.Path, plugin.Name);
        _effects[name] = effect;
        return effect;
    }

    /// <summary>
    /// Try to open the editor for a loaded instrument.
    /// </summary>
    public bool TryOpenEditor(string name)
    {
        if (!_instances.TryGetValue(name, out var existing))
        {
            return false;
        }

        existing.OpenEditor();
        return true;
    }

    /// <summary>
    /// Dispose and clear instances unless <see cref="KeepInstances"/> is true.
    /// </summary>
    public void Clear()
    {
        if (KeepInstances)
        {
            return;
        }

        foreach (var entry in _instances.Values)
        {
            entry.Dispose();
        }
        foreach (var entry in _effects.Values)
        {
            entry.Dispose();
        }
        _instances.Clear();
        _effects.Clear();
    }

    /// <summary>
    /// Dispose and clear instances unless <paramref name="keepInstances"/> is true.
    /// </summary>
    public void Clear(bool keepInstances)
    {
        KeepInstances = keepInstances;
        if (keepInstances)
        {
            return;
        }

        foreach (var entry in _instances.Values)
        {
            entry.Dispose();
        }
        foreach (var entry in _effects.Values)
        {
            entry.Dispose();
        }
        _instances.Clear();
        _effects.Clear();
    }

    /// <summary>
    /// Reattach cached instruments to a new engine.
    /// </summary>
    public void Reattach(AudioEngine engine)
    {
        foreach (var instrument in _instances.Values)
        {
            engine.AddSampleProvider(instrument);
        }
    }

    /// <summary>
    /// Reset state for all cached instruments.
    /// </summary>
    public void ResetState()
    {
        foreach (var instrument in _instances.Values)
        {
            instrument.ResetState();
        }
    }
}
