// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio effect interface for routing chains.

using NAudio.Wave;

namespace MusicEngine.Core;

public interface IAudioEffect : IDisposable
{
    string Name { get; }
    ISampleProvider Attach(ISampleProvider input, WaveFormat targetFormat);
    void Detach();
}
