// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Shared MIDI output pool to avoid double-opening devices.

using System.Collections.Generic;
using NAudio.Midi;

namespace MusicEngine.Core;

internal static class MidiOutPool
{
    private static readonly object LockObj = new();
    private static readonly Dictionary<int, (MidiOut midiOut, int refCount)> Pool = new();

    public static MidiOut Rent(int deviceId)
    {
        lock (LockObj)
        {
            if (Pool.TryGetValue(deviceId, out var entry))
            {
                Pool[deviceId] = (entry.midiOut, entry.refCount + 1);
                return entry.midiOut;
            }

            var midiOut = new MidiOut(deviceId);
            Pool[deviceId] = (midiOut, 1);
            return midiOut;
        }
    }

    public static void Return(int deviceId)
    {
        lock (LockObj)
        {
            if (!Pool.TryGetValue(deviceId, out var entry)) return;

            var newCount = entry.refCount - 1;
            if (newCount <= 0)
            {
                entry.midiOut.Dispose();
                Pool.Remove(deviceId);
            }
            else
            {
                Pool[deviceId] = (entry.midiOut, newCount);
            }
        }
    }

    public static void DisposeAll()
    {
        lock (LockObj)
        {
            foreach (var entry in Pool.Values)
            {
                entry.midiOut.Dispose();
            }
            Pool.Clear();
        }
    }
}
