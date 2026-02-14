#if !WINDOWS
// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann
// Description: Linux stub for VST3 editor window.

using System;

namespace MusicEngine.Vst;

/// <summary>
/// No-op VST3 editor window on Linux (UI not supported).
/// </summary>
public sealed class Vst3EditorWindow
{
    public static void Open(string pluginPath, string displayName)
    {
        Console.WriteLine("VST editor UI is not supported on Linux.");
    }

    public static void OpenExisting(IntPtr hostHandle, string displayName, string pluginPath)
    {
        Console.WriteLine("VST editor UI is not supported on Linux.");
    }
}
#endif
