// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann
// Description: Factory for native VST host bridge.

namespace MusicEngine.CppLayer;

public static class VstHostFactory
{
    public static NativeVstHost? CreateHost(int sampleRate, int blockSize, bool preferNative = true)
    {
        if (!preferNative || !NativeVstHost.IsNativeLibraryAvailable)
        {
            return null;
        }

        return new NativeVstHost(sampleRate, blockSize);
    }

    public static bool IsNativeAvailable => NativeVstHost.IsNativeLibraryAvailable;

    public static VstBridgeInfo GetInfo()
    {
        return new VstBridgeInfo(
            NativeVstHost.IsNativeLibraryAvailable,
            NativeVstHost.NativeVersion,
            NativeVstHost.HasVst2Support,
            NativeVstHost.HasVst3Support);
    }
}
