#if !WINDOWS
// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann
// Description: Linux stub for virtual outputs (not supported yet).

using System;

namespace MusicEngine.Core;

internal sealed class AudioVirtualOutput : IDisposable
{
    public string DeviceId => string.Empty;
    public string DeviceName => string.Empty;
    public int OutputChannelOffset => 0;

    public void Push(float[] buffer, int offset, int count)
    {
    }

    public void Dispose()
    {
    }
}
#endif
