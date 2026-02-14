#if !WINDOWS
// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann
// Description: Linux VST UI context stub.

using System;

namespace MusicEngine.Vst;

internal sealed class VstUiContext
{
    private static readonly Lazy<VstUiContext> LazyInstance = new(() => new VstUiContext());
    public static VstUiContext Shared => LazyInstance.Value;

    private VstUiContext()
    {
    }

    public T Invoke<T>(Func<T> action) => action();

    public void BeginInvoke(Action action) => action();
}
#endif
