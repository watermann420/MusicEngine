// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Shared MIDI output pool to avoid double-opening devices.

using System;
using System.Collections.Generic;
using NAudio.Midi;

namespace MusicEngine.Core;

/// <summary>
/// Provides shared, reference-counted access to <see cref="MidiOut"/> devices.
/// Ensures each MIDI output is opened at most once per process to avoid
/// MMSYSERR_ALLOCATED / "AlreadyAllocated" errors when multiple components
/// (engine, scripts, GM instrument) need the same device.
/// </summary>
internal static class MidiOutPool
{
    private static readonly object _lock = new();
    private static readonly Dictionary<int, (MidiOut midiOut, int refCount)> _pool = new();

    /// <summary>Acquire (or reuse) a MIDI output for the given device index.</summary>
    public static MidiOut Rent(int deviceId)
    {
        lock (_lock)
        {
            if (_pool.TryGetValue(deviceId, out var entry))
            {
                _pool[deviceId] = (entry.midiOut, entry.refCount + 1);
                return entry.midiOut;
            }

            var midiOut = new MidiOut(deviceId);
            _pool[deviceId] = (midiOut, 1);
            return midiOut;
        }
    }

    /// <summary>Decrement ref count and dispose when last renter releases.</summary>
    public static void Return(int deviceId)
    {
        lock (_lock)
        {
            if (!_pool.TryGetValue(deviceId, out var entry)) return;

            var newCount = entry.refCount - 1;
            if (newCount <= 0)
            {
                entry.midiOut.Dispose();
                _pool.Remove(deviceId);
            }
            else
            {
                _pool[deviceId] = (entry.midiOut, newCount);
            }
        }
    }

    /// <summary>Dispose and clear everything (used on shutdown).</summary>
    public static void DisposeAll()
    {
        lock (_lock)
        {
            foreach (var entry in _pool.Values)
            {
                entry.midiOut.Dispose();
            }
            _pool.Clear();
        }
    }
}
