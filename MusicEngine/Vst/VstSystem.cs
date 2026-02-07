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
}
