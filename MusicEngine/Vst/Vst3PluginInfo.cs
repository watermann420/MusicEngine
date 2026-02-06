// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: VST3 plugin info container.

namespace MusicEngine.Vst;

public sealed class Vst3PluginInfo
{
    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public int Index { get; init; }
}
