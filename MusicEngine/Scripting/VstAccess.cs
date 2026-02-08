// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Script helper for creating VST instruments.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using MusicEngine.Core;
using MusicEngine.Effects.Vst;
using MusicEngine.Vst;

namespace MusicEngine.Scripting;

/// <summary>
/// Script helper for creating and reusing VST instruments/effects.
/// </summary>
public sealed class VstAccess : DynamicObject
{
    private ScriptGlobals _globals;
    private readonly Dictionary<string, IVstInstrument> _instances = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IVstInstrument> _allInstances = new();
    private readonly Dictionary<string, Vst3Effect> _effects = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Vst3Effect> _allEffects = new();
    private readonly HashSet<string> _activeNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, byte[]> _cachedStates = new(StringComparer.OrdinalIgnoreCase);

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
    /// Reset active tracking for a new script run.
    /// </summary>
    public void BeginScriptRun()
    {
        _activeNames.Clear();
    }

    /// <summary>
    /// Indexer access to VST instruments by name.
    /// </summary>
    public IVstInstrument this[string name] => Get(name);

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
    public IVstInstrument Get(string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            _activeNames.Add(name);
        }

        if (_instances.TryGetValue(name, out var existing))
        {
            _globals.Engine.AddSampleProvider(existing);
            return existing;
        }

        var instrument = CreateInstrument(name);
        _globals.Engine.AddSampleProvider(instrument);
        _instances[name] = instrument;
        _allInstances.Add(instrument);
        return instrument;
    }

    /// <summary>
    /// Create a new VST3 instrument instance by name.
    /// </summary>
    public IVstInstrument Create(string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            _activeNames.Add(name);
        }

        var instrument = CreateInstrument(name);
        _globals.Engine.AddSampleProvider(instrument);
        _allInstances.Add(instrument);
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
        _allEffects.Add(effect);
        return effect;
    }

    /// <summary>
    /// Create a new VST3 effect instance by name.
    /// </summary>
    public Vst3Effect CreateEffect(string name)
    {
        var effect = CreateEffectInstance(name);
        _allEffects.Add(effect);
        return effect;
    }

    /// <summary>
    /// Try to open the editor for a loaded instrument.
    /// </summary>
    public bool TryOpenEditor(string name)
    {
        if (_instances.TryGetValue(name, out var existing))
        {
            existing.OpenEditor();
            return true;
        }

        for (int i = _allInstances.Count - 1; i >= 0; i--)
        {
            var instance = _allInstances[i];
            if (!string.Equals(instance.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
            instance.OpenEditor();
            return true;
        }

        return false;
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
            if (entry is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        foreach (var entry in _allInstances)
        {
            if (_instances.ContainsValue(entry)) continue;
            if (entry is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        foreach (var entry in _effects.Values)
        {
            entry.Dispose();
        }
        foreach (var entry in _allEffects)
        {
            if (_effects.ContainsValue(entry)) continue;
            entry.Dispose();
        }
        _instances.Clear();
        _allInstances.Clear();
        _effects.Clear();
        _allEffects.Clear();
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
            if (entry is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        foreach (var entry in _allInstances)
        {
            if (_instances.ContainsValue(entry)) continue;
            if (entry is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        foreach (var entry in _effects.Values)
        {
            entry.Dispose();
        }
        foreach (var entry in _allEffects)
        {
            if (_effects.ContainsValue(entry)) continue;
            entry.Dispose();
        }
        _instances.Clear();
        _allInstances.Clear();
        _effects.Clear();
        _allEffects.Clear();
    }

    /// <summary>
    /// Reattach cached instruments to a new engine.
    /// </summary>
    public void Reattach(AudioEngine engine)
    {
        foreach (var instrument in _allInstances)
        {
            engine.AddSampleProvider(instrument);
        }
    }

    /// <summary>
    /// Reset state for all cached instruments.
    /// </summary>
    public void ResetState()
    {
        foreach (var instrument in _allInstances)
        {
            instrument.ResetState();
        }
    }

    /// <summary>
    /// Persist state for all cached instruments.
    /// </summary>
    public void SaveAllStates()
    {
        foreach (var instrument in _allInstances)
        {
            instrument.SaveStateNow();
        }
    }

    /// <summary>
    /// Remove saved states that no longer match active instances.
    /// </summary>
    public void PruneUnusedStates()
    {
        var stateDir = Vst3Instrument.GetScriptStateDirectory(_globals.ScriptFilePath);
        if (string.IsNullOrWhiteSpace(stateDir)) return;
        if (!Directory.Exists(stateDir)) return;

        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in _activeNames)
        {
            var path = Vst3Instrument.GetScriptStatePath(name, _globals.ScriptFilePath);
            if (string.IsNullOrWhiteSpace(path)) continue;
            keep.Add(Path.GetFullPath(path));
        }

        foreach (var instrument in _allInstances)
        {
            if (_activeNames.Contains(instrument.Name)) continue;
            if (instrument is Vst3Instrument vst)
            {
                var statePath = Vst3Instrument.GetScriptStatePath(instrument.Name, _globals.ScriptFilePath);
                if (string.IsNullOrWhiteSpace(statePath)) continue;
                statePath = Path.GetFullPath(statePath);
                var data = vst.GetState();
                if (data.Length > 0)
                {
                    _cachedStates[statePath] = data;
                }
            }
        }

        foreach (var file in Directory.GetFiles(stateDir, "*.state", SearchOption.TopDirectoryOnly))
        {
            var full = Path.GetFullPath(file);
            if (!keep.Contains(full))
            {
                if (!_cachedStates.ContainsKey(full))
                {
                    try
                    {
                        var data = File.ReadAllBytes(full);
                        if (data.Length > 0)
                        {
                            _cachedStates[full] = data;
                        }
                    }
                    catch
                    {
                    }
                }

                try
                {
                    File.Delete(file);
                }
                catch
                {
                }
            }
        }

        try
        {
            if (Directory.Exists(stateDir) &&
                Directory.GetFiles(stateDir).Length == 0 &&
                Directory.GetDirectories(stateDir).Length == 0)
            {
                Directory.Delete(stateDir);
            }

            var parent = Path.GetDirectoryName(stateDir);
            if (!string.IsNullOrWhiteSpace(parent) &&
                Directory.Exists(parent) &&
                Directory.GetFiles(parent).Length == 0 &&
                Directory.GetDirectories(parent).Length == 0)
            {
                Directory.Delete(parent);
            }
        }
        catch
        {
        }
    }

    private IVstInstrument CreateInstrument(string name)
    {
        var registry = _globals.VstRegistry;
        if (registry == null)
        {
            Console.WriteLine($"VST warning: registry not available, disabling {name}.");
            return new MissingVstInstrument(name);
        }

        var plugin = registry.FindByName(name);
        if (plugin == null)
        {
            Console.WriteLine($"VST warning: plugin not found: {name}. Instance is silent.");
            return new MissingVstInstrument(name);
        }

        var statePath = Vst3Instrument.GetScriptStatePath(name, _globals.ScriptFilePath);
        var instrument = new Vst3Instrument(plugin.Path, name, statePath);
        if (!string.IsNullOrWhiteSpace(statePath))
        {
            var fullStatePath = Path.GetFullPath(statePath);
            if (_cachedStates.TryGetValue(fullStatePath, out var cached) && cached.Length > 0)
            {
                instrument.SetState(cached);
                instrument.SaveStateNow();
                _cachedStates.Remove(fullStatePath);
            }
        }
        return instrument;
    }

    private Vst3Effect CreateEffectInstance(string name)
    {
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

        return new Vst3Effect(plugin.Path, plugin.Name);
    }
}
