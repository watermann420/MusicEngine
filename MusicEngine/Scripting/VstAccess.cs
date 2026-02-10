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
    private readonly Dictionary<string, IVstInstrument> _instanceAliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Vst3Effect> _effects = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Vst3Effect> _allEffects = new();
    private readonly Dictionary<string, Vst3Effect> _effectAliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _activeNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _activeAliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _declaredStateKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Vst3Effect, string> _effectStatePaths = new();
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
        _activeAliases.Clear();
    }

    /// <summary>
    /// Update which VST state keys are declared in the current script.
    /// </summary>
    public void UpdateDeclaredStateKeys(IEnumerable<string> keys)
    {
        _declaredStateKeys.Clear();
        if (keys == null) return;
        foreach (var key in keys)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                _declaredStateKeys.Add(key);
            }
        }
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

        var instrument = CreateInstrument(name, alias: null);
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
        return Create(name, alias: null);
    }

    /// <summary>
    /// Create or reuse a VST3 instrument instance by alias.
    /// </summary>
    public IVstInstrument Create(string name, string? alias)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            _activeNames.Add(name);
        }

        if (!string.IsNullOrWhiteSpace(alias))
        {
            _activeAliases.Add(alias);
        }

        if (!string.IsNullOrWhiteSpace(alias) && _instanceAliases.TryGetValue(alias, out var aliased))
        {
            if (string.Equals(aliased.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                _globals.Engine.AddSampleProvider(aliased);
                return aliased;
            }

            if (aliased is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _instanceAliases.Remove(alias);
        }

        var instrument = CreateInstrument(name, alias);
        _globals.Engine.AddSampleProvider(instrument);
        _allInstances.Add(instrument);
        if (!string.IsNullOrWhiteSpace(alias))
        {
            _instanceAliases[alias] = instrument;
        }
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
        return CreateEffect(name, alias: null);
    }

    /// <summary>
    /// Create or reuse a VST3 effect instance by alias.
    /// </summary>
    public Vst3Effect CreateEffect(string name, string? alias)
    {
        if (!string.IsNullOrWhiteSpace(alias))
        {
            _activeAliases.Add(alias);
        }

        if (!string.IsNullOrWhiteSpace(alias) && _effectAliases.TryGetValue(alias, out var aliased))
        {
            if (string.Equals(aliased.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return aliased;
            }

            aliased.Dispose();
            _effectAliases.Remove(alias);
        }

        var effect = CreateEffectInstance(name, alias);
        _allEffects.Add(effect);
        if (!string.IsNullOrWhiteSpace(alias))
        {
            _effectAliases[alias] = effect;
        }
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

        if (_effects.TryGetValue(name, out var effect))
        {
            effect.OpenEditor();
            return true;
        }

        for (int i = _allEffects.Count - 1; i >= 0; i--)
        {
            var entry = _allEffects[i];
            if (!string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
            entry.OpenEditor();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Apply current sleep settings to all loaded VST instances and effects.
    /// </summary>
    public void ApplySleepSettings()
    {
        foreach (var instrument in _allInstances)
        {
            if (instrument is Vst3Instrument vst)
            {
                vst.SleepWhenIdle = Settings.VstInstrumentSleepWhenIdle;
                vst.IdleThreshold = Settings.VstIdleThreshold;
                vst.IdleTimeoutSeconds = Settings.VstIdleTimeoutSeconds;
            }
        }

        foreach (var effect in _allEffects)
        {
            effect.SleepWhenIdle = Settings.VstEffectSleepWhenIdle;
            effect.IdleThreshold = Settings.VstIdleThreshold;
            effect.IdleTimeoutSeconds = Settings.VstIdleTimeoutSeconds;
        }
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
        _instanceAliases.Clear();
        _effects.Clear();
        _allEffects.Clear();
        _effectAliases.Clear();
        _effectStatePaths.Clear();
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
        _instanceAliases.Clear();
        _effects.Clear();
        _allEffects.Clear();
        _effectAliases.Clear();
        _effectStatePaths.Clear();
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

        foreach (var effect in _allEffects)
        {
            SaveEffectState(effect);
        }
    }

    /// <summary>
    /// Try to get an existing instrument by name.
    /// </summary>
    public bool TryGetInstrument(string name, out IVstInstrument instrument)
    {
        if (_instances.TryGetValue(name, out var existing))
        {
            instrument = existing;
            return true;
        }

        foreach (var entry in _allInstances)
        {
            if (string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                instrument = entry;
                return true;
            }
        }

        instrument = null!;
        return false;
    }

    /// <summary>
    /// Try to get an existing effect by name.
    /// </summary>
    public bool TryGetEffect(string name, out Vst3Effect effect)
    {
        if (_effects.TryGetValue(name, out var existing))
        {
            effect = existing;
            return true;
        }

        foreach (var entry in _allEffects)
        {
            if (string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                effect = entry;
                return true;
            }
        }

        effect = null!;
        return false;
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
            AddKeepStatePath(keep, name);
        }
        foreach (var alias in _activeAliases)
        {
            AddKeepStatePath(keep, alias);
        }
        foreach (var key in _declaredStateKeys)
        {
            AddKeepStatePath(keep, key);
        }

        foreach (var instrument in _allInstances)
        {
            var stateKey = GetStateKeyForInstance(instrument);
            if (_activeNames.Contains(instrument.Name)) continue;
            if (!string.IsNullOrWhiteSpace(stateKey) && _activeAliases.Contains(stateKey)) continue;
            if (!string.IsNullOrWhiteSpace(stateKey) && _declaredStateKeys.Contains(stateKey)) continue;
            if (instrument is Vst3Instrument vst)
            {
                var statePath = Vst3Instrument.GetScriptStatePath(stateKey, _globals.ScriptFilePath);
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

        CleanupInactiveAliases();
    }

    private IVstInstrument CreateInstrument(string name, string? alias)
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

        var stateKey = !string.IsNullOrWhiteSpace(alias) ? alias : name;
        var statePath = Vst3Instrument.GetScriptStatePath(stateKey, _globals.ScriptFilePath);
        var legacyStatePath = Vst3Instrument.GetLegacyScriptStatePath(name, _globals.ScriptFilePath);
        TryMigrateLegacyState(statePath, legacyStatePath);
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

    private void CleanupInactiveAliases()
    {
        if (_instanceAliases.Count > 0)
        {
            var toRemove = new List<string>();
            foreach (var entry in _instanceAliases)
            {
                if (_activeAliases.Contains(entry.Key)) continue;
                if (_activeNames.Contains(entry.Value.Name)) continue;
                toRemove.Add(entry.Key);
            }

            foreach (var alias in toRemove)
            {
                if (_instanceAliases.TryGetValue(alias, out var instance))
                {
                    RemoveInstance(instance);
                    _instanceAliases.Remove(alias);
                }
            }
        }

        if (_effectAliases.Count > 0)
        {
            var toRemove = new List<string>();
            foreach (var entry in _effectAliases)
            {
                if (_activeAliases.Contains(entry.Key)) continue;
                toRemove.Add(entry.Key);
            }

            foreach (var alias in toRemove)
            {
                if (_effectAliases.TryGetValue(alias, out var effect))
                {
                    RemoveEffect(effect);
                    _effectAliases.Remove(alias);
                }
            }
        }
    }

    private void RemoveInstance(IVstInstrument instance)
    {
        for (int i = _allInstances.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(_allInstances[i], instance))
            {
                _allInstances.RemoveAt(i);
            }
        }

        var keys = new List<string>();
        foreach (var entry in _instances)
        {
            if (ReferenceEquals(entry.Value, instance))
            {
                keys.Add(entry.Key);
            }
        }

        foreach (var key in keys)
        {
            _instances.Remove(key);
        }

        if (instance is IDisposable disposable)
        {
            if (instance is Vst3Instrument vst)
            {
                vst.SaveStateNow();
            }
            disposable.Dispose();
        }
    }

    private void RemoveEffect(Vst3Effect effect)
    {
        for (int i = _allEffects.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(_allEffects[i], effect))
            {
                _allEffects.RemoveAt(i);
            }
        }

        var keys = new List<string>();
        foreach (var entry in _effects)
        {
            if (ReferenceEquals(entry.Value, effect))
            {
                keys.Add(entry.Key);
            }
        }

        foreach (var key in keys)
        {
            _effects.Remove(key);
        }

        SaveEffectState(effect);
        _effectStatePaths.Remove(effect);
        effect.Dispose();
    }

    private static void TryLoadEffectState(Vst3Effect effect, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || effect == null) return;
        if (!File.Exists(path)) return;
        try
        {
            var data = File.ReadAllBytes(path);
            if (data.Length > 0)
            {
                effect.SetState(data);
            }
        }
        catch
        {
        }
    }

    private void SaveEffectState(Vst3Effect effect)
    {
        if (effect == null) return;
        if (!_effectStatePaths.TryGetValue(effect, out var path)) return;
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            var data = effect.GetState();
            if (data.Length == 0) return;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllBytes(path, data);
        }
        catch
        {
        }
    }

    private string GetStateKeyForInstance(IVstInstrument instrument)
    {
        if (instrument == null) return string.Empty;
        foreach (var entry in _instanceAliases)
        {
            if (ReferenceEquals(entry.Value, instrument))
            {
                return entry.Key;
            }
        }
        return instrument.Name;
    }

    private void AddKeepStatePath(HashSet<string> keep, string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        var path = Vst3Instrument.GetScriptStatePath(key, _globals.ScriptFilePath);
        if (string.IsNullOrWhiteSpace(path)) return;
        keep.Add(Path.GetFullPath(path));
    }

    private static void TryMigrateLegacyState(string? statePath, string? legacyStatePath)
    {
        if (string.IsNullOrWhiteSpace(statePath) || string.IsNullOrWhiteSpace(legacyStatePath)) return;
        if (File.Exists(statePath) || !File.Exists(legacyStatePath)) return;

        try
        {
            var dir = Path.GetDirectoryName(statePath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.Copy(legacyStatePath, statePath, overwrite: true);
        }
        catch
        {
        }
    }

    private Vst3Effect CreateEffectInstance(string name, string? alias)
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

        var effect = new Vst3Effect(plugin.Path, plugin.Name);
        var stateKey = !string.IsNullOrWhiteSpace(alias) ? alias : name;
        var statePath = Vst3Instrument.GetScriptStatePath(stateKey, _globals.ScriptFilePath);
        if (!string.IsNullOrWhiteSpace(statePath))
        {
            TryLoadEffectState(effect, statePath);
            _effectStatePaths[effect] = statePath;
        }
        return effect;
    }
}
