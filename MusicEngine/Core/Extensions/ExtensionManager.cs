// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System.Reflection;

namespace MusicEngine.Core.Extensions;

/// <summary>
/// Manages discovery and registration of extensions.
/// </summary>
public class ExtensionManager
{
    private readonly Dictionary<string, Type> _synthExtensions = new();
    private readonly Dictionary<string, Type> _effectExtensions = new();
    private readonly List<Assembly> _loadedAssemblies = new();

    /// <summary>
    /// Gets all registered synth extensions.
    /// </summary>
    public IReadOnlyDictionary<string, Type> SynthExtensions => _synthExtensions;

    /// <summary>
    /// Gets all registered effect extensions.
    /// </summary>
    public IReadOnlyDictionary<string, Type> EffectExtensions => _effectExtensions;

    /// <summary>
    /// Discovers extensions in the specified assembly.
    /// </summary>
    public void DiscoverExtensions(Assembly assembly)
    {
        if (_loadedAssemblies.Contains(assembly))
            return;

        _loadedAssemblies.Add(assembly);

        foreach (var type in assembly.GetTypes())
        {
            var synthAttr = type.GetCustomAttribute<SynthExtensionAttribute>();
            if (synthAttr != null && typeof(ISynth).IsAssignableFrom(type))
            {
                _synthExtensions[synthAttr.Id] = type;
            }

            var effectAttr = type.GetCustomAttribute<EffectExtensionAttribute>();
            if (effectAttr != null && typeof(IEffect).IsAssignableFrom(type))
            {
                _effectExtensions[effectAttr.Id] = type;
            }
        }
    }

    /// <summary>
    /// Discovers extensions from a directory of assemblies.
    /// </summary>
    public void DiscoverExtensionsFromDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;

        foreach (var dll in Directory.GetFiles(path, "*.dll"))
        {
            try
            {
                var assembly = Assembly.LoadFrom(dll);
                DiscoverExtensions(assembly);
            }
            catch
            {
                // Skip assemblies that can't be loaded
            }
        }
    }

    /// <summary>
    /// Registers a synth extension type.
    /// </summary>
    public void RegisterSynth<T>(string id) where T : ISynth
    {
        _synthExtensions[id] = typeof(T);
    }

    /// <summary>
    /// Registers an effect extension type.
    /// </summary>
    public void RegisterEffect<T>(string id) where T : IEffect
    {
        _effectExtensions[id] = typeof(T);
    }

    /// <summary>
    /// Creates a synth instance by ID.
    /// </summary>
    public ISynth? CreateSynth(string id, params object[] args)
    {
        if (!_synthExtensions.TryGetValue(id, out var type))
            return null;

        return Activator.CreateInstance(type, args) as ISynth;
    }

    /// <summary>
    /// Creates an effect instance by ID.
    /// </summary>
    public IEffect? CreateEffect(string id, params object[] args)
    {
        if (!_effectExtensions.TryGetValue(id, out var type))
            return null;

        return Activator.CreateInstance(type, args) as IEffect;
    }

    /// <summary>
    /// Gets extension info by ID.
    /// </summary>
    public (string Id, string Name, string? Author, string? Description)? GetSynthInfo(string id)
    {
        if (!_synthExtensions.TryGetValue(id, out var type))
            return null;

        var attr = type.GetCustomAttribute<SynthExtensionAttribute>();
        if (attr == null)
            return null;

        return (attr.Id, attr.Name, attr.Author, attr.Description);
    }

    /// <summary>
    /// Gets effect info by ID.
    /// </summary>
    public (string Id, string Name, string? Category, string? Author)? GetEffectInfo(string id)
    {
        if (!_effectExtensions.TryGetValue(id, out var type))
            return null;

        var attr = type.GetCustomAttribute<EffectExtensionAttribute>();
        if (attr == null)
            return null;

        return (attr.Id, attr.Name, attr.Category, attr.Author);
    }
}
