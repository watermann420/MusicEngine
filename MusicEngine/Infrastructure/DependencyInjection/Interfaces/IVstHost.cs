using MusicEngine.Core;

namespace MusicEngine.Infrastructure.DependencyInjection.Interfaces;

/// <summary>
/// Interface for the VST host.
/// </summary>
public interface IVstHost : IDisposable
{
    IReadOnlyList<VstPluginInfo> DiscoveredPlugins { get; }
    IReadOnlyDictionary<string, IVstPlugin> LoadedPlugins { get; }

    List<VstPluginInfo> ScanForPlugins();
    IVstPlugin? LoadPlugin(string nameOrPath);
    IVstPlugin? GetPlugin(string name);
    void UnloadPlugin(string name);
    void PrintDiscoveredPlugins();
}
