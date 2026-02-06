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

public sealed class VstAccess : DynamicObject
{
    private ScriptGlobals _globals;
    private readonly Dictionary<string, Vst3Instrument> _instances = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Vst3Effect> _effects = new(StringComparer.OrdinalIgnoreCase);

    public bool KeepInstances { get; set; }

    public VstAccess(ScriptGlobals globals)
    {
        _globals = globals;
    }

    public void UpdateGlobals(ScriptGlobals globals)
    {
        _globals = globals;
    }

    public Vst3Instrument this[string name] => Get(name);

    public override bool TryGetMember(GetMemberBinder binder, out object result)
    {
        result = Get(binder.Name);
        return true;
    }

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

    public bool TryOpenEditor(string name)
    {
        if (!_instances.TryGetValue(name, out var existing))
        {
            return false;
        }

        existing.OpenEditor();
        return true;
    }

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

    public void Reattach(AudioEngine engine)
    {
        foreach (var instrument in _instances.Values)
        {
            engine.AddSampleProvider(instrument);
        }
    }

    public void ResetState()
    {
        foreach (var instrument in _instances.Values)
        {
            instrument.ResetState();
        }
    }
}
