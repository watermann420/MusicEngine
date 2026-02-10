// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: VST3 registry for scanned plugins.

namespace MusicEngine.Vst;

/// <summary>
/// Registry for scanned VST3 plugins.
/// </summary>
public sealed class Vst3Registry
{
    private readonly List<Vst3PluginInfo> _plugins = new();

    /// <summary>
    /// Current list of plugins in the registry.
    /// </summary>
    public IReadOnlyList<Vst3PluginInfo> Plugins => _plugins;

    /// <summary>
    /// Replace the registry contents with a new plugin list.
    /// </summary>
    /// <param name="plugins">Plugins to store.</param>
    public void SetPlugins(IEnumerable<Vst3PluginInfo> plugins)
    {
        _plugins.Clear();
        _plugins.AddRange(plugins.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase));
        for (int i = 0; i < _plugins.Count; i++)
        {
            _plugins[i] = new Vst3PluginInfo
            {
                Name = _plugins[i].Name,
                Path = _plugins[i].Path,
                Index = i
            };
        }
    }

    /// <summary>
    /// Find a plugin by display name.
    /// </summary>
    /// <param name="name">Plugin display name.</param>
    /// <returns>Matching plugin info or null.</returns>
    public Vst3PluginInfo? FindByName(string name)
    {
        return _plugins.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}
