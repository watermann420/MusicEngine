// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann
// Description: Native VST host wrapper.

using System.Text;

namespace MusicEngine.CppLayer;

public sealed class NativeVstHost : IDisposable
{
    private readonly IntPtr _handle;
    private readonly List<INativeVstPlugin> _plugins = new();
    private bool _isDisposed;

    public event EventHandler<string>? LogReceived;

    public static bool IsNativeLibraryAvailable { get; } = VstBridgeNative.IsAvailable();

    public static string NativeVersion
    {
        get
        {
            if (!IsNativeLibraryAvailable)
            {
                return "Unavailable";
            }

            var buffer = new StringBuilder(64);
            VstBridgeNative.me_vst_get_version(buffer, buffer.Capacity);
            return buffer.ToString();
        }
    }

    public static bool HasVst2Support => IsNativeLibraryAvailable && VstBridgeNative.me_vst_has_vst2() != 0;
    public static bool HasVst3Support => IsNativeLibraryAvailable && VstBridgeNative.me_vst_has_vst3() != 0;

    public NativeVstHost(int sampleRate, int blockSize)
    {
        if (!IsNativeLibraryAvailable)
        {
            throw new InvalidOperationException("Native VST bridge library not available.");
        }

        _handle = VstBridgeNative.me_vst_host_create(sampleRate, blockSize);
        if (_handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create native VST host.");
        }

        SampleRate = sampleRate;
        BlockSize = blockSize;
    }

    public int SampleRate { get; private set; }
    public int BlockSize { get; private set; }
    public IReadOnlyList<INativeVstPlugin> Plugins => _plugins.AsReadOnly();

    public void SetSampleRate(int sampleRate)
    {
        EnsureNotDisposed();
        SampleRate = sampleRate;
        VstBridgeNative.me_vst_host_set_sample_rate(_handle, sampleRate);
    }

    public void SetBlockSize(int blockSize)
    {
        EnsureNotDisposed();
        BlockSize = blockSize;
        VstBridgeNative.me_vst_host_set_block_size(_handle, blockSize);
    }

    public INativeVstPlugin? LoadPlugin(string path)
    {
        EnsureNotDisposed();

        var pluginHandle = VstBridgeNative.me_vst_host_load_plugin(_handle, path);
        if (pluginHandle == IntPtr.Zero)
        {
            var error = GetLastError();
            if (!string.IsNullOrWhiteSpace(error))
            {
                LogReceived?.Invoke(this, error);
            }
            return null;
        }

        var plugin = new NativeVstPlugin(pluginHandle);
        _plugins.Add(plugin);
        return plugin;
    }

    public void UnloadPlugin(INativeVstPlugin plugin)
    {
        EnsureNotDisposed();

        if (plugin is not NativeVstPlugin nativePlugin)
        {
            return;
        }

        VstBridgeNative.me_vst_host_unload_plugin(_handle, nativePlugin.Handle);
        _plugins.Remove(plugin);
    }

    public void UnloadAllPlugins()
    {
        EnsureNotDisposed();
        VstBridgeNative.me_vst_host_unload_all(_handle);
        _plugins.Clear();
    }

    public string GetLastError()
    {
        if (_handle == IntPtr.Zero)
        {
            return string.Empty;
        }

        var buffer = new StringBuilder(512);
        VstBridgeNative.me_vst_host_get_last_error(_handle, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private void EnsureNotDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(NativeVstHost));
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        VstBridgeNative.me_vst_host_destroy(_handle);
        _plugins.Clear();
        _isDisposed = true;
    }

}
