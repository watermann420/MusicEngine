// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann
// Description: Cross-platform audio output abstraction.

using System;
using NAudio.Wave;

namespace MusicEngine.Core;

internal interface IAudioOutput : IDisposable
{
    void Init(ISampleProvider provider);
    void Play();
    void Stop();
}
