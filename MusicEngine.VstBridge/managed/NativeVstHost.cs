using System.Runtime.InteropServices;
using System.Text;

namespace MusicEngine.VstBridge;

/// <summary>
/// Native VST host for loading and managing VST plugins.
/// </summary>
public sealed class NativeVstHost : IDisposable
{
    private nint _handle;
    private bool _disposed;
    private readonly List<NativeVstPlugin> _plugins = [];
    private VstBridgeNative.LogCallback? _logCallback;

    /// <summary>
    /// Event raised when a log message is received from the native library.
    /// </summary>
    public event Action<int, string>? LogReceived;

    /// <summary>
    /// Gets whether the native library is available.
    /// </summary>
    public static bool IsNativeLibraryAvailable { get; }

    /// <summary>
    /// Gets the native library version.
    /// </summary>
    public static string? NativeVersion { get; }

    /// <summary>
    /// Gets whether VST2 support is available.
    /// </summary>
    public static bool HasVst2Support { get; }

    /// <summary>
    /// Gets whether VST3 support is available.
    /// </summary>
    public static bool HasVst3Support { get; }

    static NativeVstHost()
    {
        try
        {
            int version = VstBridgeNative.GetVersion();
            IsNativeLibraryAvailable = true;

            int major = (version >> 16) & 0xFF;
            int minor = (version >> 8) & 0xFF;
            int patch = version & 0xFF;
            NativeVersion = $"{major}.{minor}.{patch}";

            HasVst2Support = VstBridgeNative.HasVst2Support() != 0;
            HasVst3Support = VstBridgeNative.HasVst3Support() != 0;
        }
        catch (DllNotFoundException)
        {
            IsNativeLibraryAvailable = false;
            NativeVersion = null;
            HasVst2Support = false;
            HasVst3Support = false;
        }
    }

    /// <summary>
    /// Creates a new native VST host.
    /// </summary>
    /// <param name="sampleRate">The audio sample rate.</param>
    /// <param name="blockSize">The audio block size in samples.</param>
    /// <exception cref="InvalidOperationException">Thrown if the native library is not available.</exception>
    public NativeVstHost(int sampleRate = 44100, int blockSize = 512)
    {
        if (!IsNativeLibraryAvailable)
        {
            throw new InvalidOperationException(
                "Native Audio Layer library is not available. " +
                "Ensure NativeAudioLayer.dll is in the application directory.");
        }

        _handle = VstBridgeNative.Create(sampleRate, blockSize);
        if (_handle == nint.Zero)
        {
            throw new InvalidOperationException("Failed to create native VST host.");
        }

        // Set up logging
        _logCallback = OnLogMessage;
        VstBridgeNative.SetLogCallback(_handle, _logCallback);
    }

    /// <summary>
    /// Gets the sample rate.
    /// </summary>
    public int SampleRate { get; private set; }

    /// <summary>
    /// Gets the block size.
    /// </summary>
    public int BlockSize { get; private set; }

    /// <summary>
    /// Gets the loaded plugins.
    /// </summary>
    public IReadOnlyList<INativeVstPlugin> Plugins => _plugins;

    /// <summary>
    /// Sets the sample rate for all loaded plugins.
    /// </summary>
    public void SetSampleRate(int sampleRate)
    {
        ThrowIfDisposed();
        SampleRate = sampleRate;
        VstBridgeNative.SetSampleRate(_handle, sampleRate);
    }

    /// <summary>
    /// Sets the block size for all loaded plugins.
    /// </summary>
    public void SetBlockSize(int blockSize)
    {
        ThrowIfDisposed();
        BlockSize = blockSize;
        VstBridgeNative.SetBlockSize(_handle, blockSize);
    }

    /// <summary>
    /// Loads a VST plugin from a file path.
    /// </summary>
    /// <param name="path">Path to the VST plugin (.dll for VST2, .vst3 for VST3).</param>
    /// <returns>The loaded plugin, or null if loading failed.</returns>
    public INativeVstPlugin? LoadPlugin(string path)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(path);

        if (!File.Exists(path) && !Directory.Exists(path))
        {
            throw new FileNotFoundException("Plugin file not found.", path);
        }

        var pluginHandle = VstBridgeNative.LoadPlugin(_handle, path);
        if (pluginHandle == nint.Zero)
        {
            var errorPtr = VstBridgeNative.GetLastError(_handle);
            var error = errorPtr != nint.Zero
                ? Marshal.PtrToStringAnsi(errorPtr)
                : "Unknown error";
            throw new InvalidOperationException($"Failed to load plugin: {error}");
        }

        var plugin = new NativeVstPlugin(pluginHandle, this);
        _plugins.Add(plugin);
        return plugin;
    }

    /// <summary>
    /// Unloads a plugin.
    /// </summary>
    public void UnloadPlugin(INativeVstPlugin plugin)
    {
        ThrowIfDisposed();
        if (plugin is not NativeVstPlugin nativePlugin)
        {
            throw new ArgumentException("Plugin was not created by this host.", nameof(plugin));
        }

        if (_plugins.Remove(nativePlugin))
        {
            nativePlugin.Dispose();
        }
    }

    /// <summary>
    /// Unloads all plugins.
    /// </summary>
    public void UnloadAllPlugins()
    {
        ThrowIfDisposed();
        foreach (var plugin in _plugins.ToArray())
        {
            plugin.Dispose();
        }
        _plugins.Clear();
    }

    /// <summary>
    /// Gets the last error message from the native library.
    /// </summary>
    public string? GetLastError()
    {
        if (_handle == nint.Zero) return null;
        var errorPtr = VstBridgeNative.GetLastError(_handle);
        return errorPtr != nint.Zero ? Marshal.PtrToStringAnsi(errorPtr) : null;
    }

    private void OnLogMessage(int level, string message)
    {
        LogReceived?.Invoke(level, message);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Unload all plugins first
        foreach (var plugin in _plugins)
        {
            plugin.Dispose();
        }
        _plugins.Clear();

        // Remove log callback
        if (_handle != nint.Zero)
        {
            VstBridgeNative.SetLogCallback(_handle, null);
            _logCallback = null;
        }

        // Destroy the host
        if (_handle != nint.Zero)
        {
            VstBridgeNative.Destroy(_handle);
            _handle = nint.Zero;
        }
    }
}

/// <summary>
/// Factory for creating VST hosts with fallback support.
/// </summary>
public static class VstHostFactory
{
    /// <summary>
    /// Creates a VST host, preferring native implementation with managed fallback.
    /// </summary>
    /// <param name="sampleRate">The audio sample rate.</param>
    /// <param name="blockSize">The audio block size in samples.</param>
    /// <param name="preferNative">Whether to prefer the native implementation.</param>
    /// <returns>A VST host instance.</returns>
    public static NativeVstHost? CreateHost(
        int sampleRate = 44100,
        int blockSize = 512,
        bool preferNative = true)
    {
        if (preferNative && NativeVstHost.IsNativeLibraryAvailable)
        {
            try
            {
                return new NativeVstHost(sampleRate, blockSize);
            }
            catch
            {
                // Fall through to return null
            }
        }

        // Native not available or failed - return null to allow fallback
        return null;
    }

    /// <summary>
    /// Checks if native VST support is available.
    /// </summary>
    public static bool IsNativeAvailable => NativeVstHost.IsNativeLibraryAvailable;

    /// <summary>
    /// Gets information about the VST bridge capabilities.
    /// </summary>
    public static VstBridgeInfo GetInfo()
    {
        return new VstBridgeInfo(
            IsNativeAvailable: NativeVstHost.IsNativeLibraryAvailable,
            NativeVersion: NativeVstHost.NativeVersion,
            SupportsVst2: NativeVstHost.HasVst2Support,
            SupportsVst3: NativeVstHost.HasVst3Support
        );
    }
}

/// <summary>
/// Information about the VST bridge capabilities.
/// </summary>
public readonly record struct VstBridgeInfo(
    bool IsNativeAvailable,
    string? NativeVersion,
    bool SupportsVst2,
    bool SupportsVst3
);
