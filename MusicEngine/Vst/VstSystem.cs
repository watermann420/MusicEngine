// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: VST system status and scanning helpers.

namespace MusicEngine.Vst;

/// <summary>
/// VST system status and scanning helpers.
/// </summary>
public static class VstSystem
{
    private static readonly Dictionary<string, VstPluginKind> KindCache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Try to scan for VST3 plugins and populate a registry.
    /// </summary>
    /// <param name="registry">Registry populated with scan results.</param>
    /// <param name="message">Status message for diagnostics.</param>
    /// <returns>True when scanning succeeds and VST is available.</returns>
    public static bool TryScan(out Vst3Registry registry, out string message)
    {
        registry = new Vst3Registry();

        if (!Vst3Native.TryValidate(out var validation))
        {
            message = $"VST disabled: {validation}";
            return false;
        }

        registry.SetPlugins(Vst3Scanner.Scan());
        message = $"VST3 plugins found: {registry.Plugins.Count}";
        return true;
    }

    public static (List<Vst3PluginInfo> Instruments, List<Vst3PluginInfo> Effects, List<Vst3PluginInfo> Unknown)
        SplitByKind(Vst3Registry registry)
    {
        var instruments = new List<Vst3PluginInfo>();
        var effects = new List<Vst3PluginInfo>();
        var unknown = new List<Vst3PluginInfo>();

        foreach (var plugin in registry.Plugins)
        {
            var kind = GetPluginKind(plugin);
            switch (kind)
            {
                case VstPluginKind.Instrument:
                    instruments.Add(plugin);
                    break;
                case VstPluginKind.Effect:
                    effects.Add(plugin);
                    break;
                default:
                    unknown.Add(plugin);
                    break;
            }
        }

        return (instruments, effects, unknown);
    }

    private static VstPluginKind GetPluginKind(Vst3PluginInfo plugin)
    {
        if (plugin == null || string.IsNullOrWhiteSpace(plugin.Path))
        {
            return VstPluginKind.Unknown;
        }

        if (KindCache.TryGetValue(plugin.Path, out var cached))
        {
            return cached;
        }

        VstPluginKind kind;
        try
        {
            kind = VstUiContext.Shared.Invoke(() =>
            {
                IntPtr handle = IntPtr.Zero;
                try
                {
                    handle = Vst3Native.Vst3Host_Create(plugin.Path);
                    if (handle == IntPtr.Zero) return VstPluginKind.Unknown;

                    var inputChannels = Vst3Native.Vst3Host_GetInputChannels(handle);
                    var outputChannels = Vst3Native.Vst3Host_GetOutputChannels(handle);
                    if (outputChannels <= 0) return VstPluginKind.Unknown;
                    return inputChannels > 0 ? VstPluginKind.Effect : VstPluginKind.Instrument;
                }
                finally
                {
                    if (handle != IntPtr.Zero)
                    {
                        Vst3Native.Vst3Host_Close(handle);
                    }
                }
            });
        }
        catch
        {
            kind = VstPluginKind.Unknown;
        }

        KindCache[plugin.Path] = kind;
        return kind;
    }
}

public enum VstPluginKind
{
    Instrument,
    Effect,
    Unknown
}
