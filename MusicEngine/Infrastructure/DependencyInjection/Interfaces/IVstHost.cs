// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: VST plugin hosting.

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
