// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio effect interface for routing chains.

using NAudio.Wave;

namespace MusicEngine.Effects.Audio;

/// <summary>
/// Base interface for effects that wrap an input and emit processed audio.
/// </summary>
public interface IAudioEffect : IDisposable
{
    /// <summary>
    /// Display name for the effect instance.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Attach the effect to a source and return a processed provider.
    /// </summary>
    /// <param name="input">Input source to process.</param>
    /// <param name="targetFormat">Desired output format.</param>
    /// <returns>Processed sample provider.</returns>
    ISampleProvider Attach(ISampleProvider input, WaveFormat targetFormat);

    /// <summary>
    /// Detach and release any processing state.
    /// </summary>
    void Detach();
}
