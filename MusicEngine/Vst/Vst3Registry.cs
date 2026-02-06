// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: VST3 registry for scanned plugins.

namespace MusicEngine.Vst;

public sealed class Vst3Registry
{
    private readonly List<Vst3PluginInfo> _plugins = new();

    public IReadOnlyList<Vst3PluginInfo> Plugins => _plugins;

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

    public Vst3PluginInfo? FindByName(string name)
    {
        return _plugins.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}
