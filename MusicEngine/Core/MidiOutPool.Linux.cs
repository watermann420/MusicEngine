#if !WINDOWS
// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann
// Description: Linux stub for MIDI output pool (not supported yet).

namespace MusicEngine.Core;

internal static class MidiOutPool
{
    public static object? Rent(int deviceId) => null;
    public static void Return(int deviceId)
    {
    }
    public static void DisposeAll()
    {
    }
}
#endif
