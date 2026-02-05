// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann
// Description: Information about native VST bridge capabilities.

namespace MusicEngine.CppLayer;

public sealed class VstBridgeInfo
{
    public bool IsNativeAvailable { get; }
    public string Version { get; }
    public bool HasVst2Support { get; }
    public bool HasVst3Support { get; }

    public VstBridgeInfo(bool isNativeAvailable, string version, bool hasVst2Support, bool hasVst3Support)
    {
        IsNativeAvailable = isNativeAvailable;
        Version = version ?? string.Empty;
        HasVst2Support = hasVst2Support;
        HasVst3Support = hasVst3Support;
    }
}
