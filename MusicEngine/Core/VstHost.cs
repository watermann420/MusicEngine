// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: VST plugin hosting.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MusicEngine.Core.Events;
using MusicEngine.Core.Progress;
using MusicEngine.Core.Vst.Vst3.Interfaces;
using MusicEngine.Core.Vst.Vst3.Structures;
using NAudio.Wave;


namespace MusicEngine.Core;


/// <summary>
/// Central VST Host that manages plugin discovery, loading, and lifecycle.
/// Provides utilities for preset management and parameter discovery across plugins.
/// Supports both VST2 and VST3 plugin formats.
/// </summary>
public class VstHost : IDisposable
{
    private readonly List<VstPluginInfo> _discoveredPlugins = new();
    private readonly List<Vst3PluginInfo> _discoveredVst3Plugins = new();
    private readonly Dictionary<string, IVstPlugin> _loadedPlugins = new();
    private readonly object _lock = new();
    private bool _isDisposed;

    // Logging
    private readonly ILogger? _logger;

    /// <summary>
    /// Creates a new VstHost with logging support.
    /// </summary>
    public VstHost(ILogger? logger = null)
    {
        _logger = logger;
    }

    // P/Invoke for VST3 plugin loading
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibraryW(string lpFileName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr hModule);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    // VST3 entry point delegate
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr GetPluginFactoryDelegate();

    public IReadOnlyList<VstPluginInfo> DiscoveredPlugins => _discoveredPlugins.AsReadOnly();
    public IReadOnlyList<Vst3PluginInfo> DiscoveredVst3Plugins => _discoveredVst3Plugins.AsReadOnly();
    public IReadOnlyDictionary<string, IVstPlugin> LoadedPlugins => _loadedPlugins;

    /// <summary>
    /// When true, VST scanning skips native DLL loading to prevent crashes from corrupt plugins.
    /// Plugin info is derived from filename only. Full probing occurs when loading a plugin.
    /// Default: true (safe mode enabled)
    /// </summary>
    public bool SafeScanMode { get; set; } = true;

    /// <summary>
    /// Scan for VST plugins in configured paths and return discovered plugins.
    /// Scans for both VST2 (.dll) and VST3 (.vst3 files and bundles) plugins.
    /// </summary>
    public List<VstPluginInfo> ScanForPlugins()
    {
        lock (_lock)
        {
            _discoveredPlugins.Clear();
            _discoveredVst3Plugins.Clear();

            var searchPaths = new List<string>(Settings.VstPluginSearchPaths);

            // Add custom path if configured
            if (!string.IsNullOrEmpty(Settings.VstPluginPath))
            {
                searchPaths.Insert(0, Settings.VstPluginPath);
            }

            Console.WriteLine($"[VstHost] Scanning {searchPaths.Count} VST path(s)...");

            foreach (var basePath in searchPaths)
            {
                if (!Directory.Exists(basePath))
                {
                    Console.WriteLine($"[VstHost]   Path not found: {basePath}");
                    continue;
                }

                Console.WriteLine($"[VstHost]   Scanning: {basePath}");

                try
                {
                    // Scan for VST2 plugins (.dll)
                    var dllFiles = Directory.GetFiles(basePath, "*.dll", SearchOption.AllDirectories);
                    Console.WriteLine($"[VstHost]     Found {dllFiles.Length} DLL file(s)");
                    foreach (var file in dllFiles)
                    {
                        Console.WriteLine($"[VstHost]       Probing VST2: {Path.GetFileName(file)}");
                        var pluginInfo = ProbePlugin(file, false);
                        if (pluginInfo != null)
                        {
                            Console.WriteLine($"[VstHost]         -> Valid VST2: {pluginInfo.Name}");
                            _discoveredPlugins.Add(pluginInfo);
                        }
                    }

                    // Scan for VST3 single-file plugins (.vst3 files)
                    var vst3Files = Directory.GetFiles(basePath, "*.vst3", SearchOption.AllDirectories);
                    Console.WriteLine($"[VstHost]     Found {vst3Files.Length} VST3 file(s)");
                    foreach (var file in vst3Files)
                    {
                        // Skip files inside bundle directories (we'll handle bundles separately)
                        var parentDir = Path.GetDirectoryName(file);
                        if (parentDir != null && (parentDir.EndsWith("x86_64-win") || parentDir.EndsWith("Win64")))
                        {
                            continue;
                        }

                        Console.WriteLine($"[VstHost]       Probing VST3 file: {Path.GetFileName(file)}");
                        var pluginInfo = ProbeVst3Plugin(file);
                        if (pluginInfo != null)
                        {
                            Console.WriteLine($"[VstHost]         -> Valid VST3: {pluginInfo.Name}");
                            _discoveredVst3Plugins.Add(pluginInfo);
                        }
                    }

                    // Scan for VST3 bundle directories (*.vst3 folders)
                    var vst3Dirs = Directory.GetDirectories(basePath, "*.vst3", SearchOption.AllDirectories);
                    Console.WriteLine($"[VstHost]     Found {vst3Dirs.Length} VST3 bundle(s)");
                    foreach (var dir in vst3Dirs)
                    {
                        Console.WriteLine($"[VstHost]       Probing VST3 bundle: {Path.GetFileName(dir)}");
                        var pluginInfo = ScanVst3Bundle(dir);
                        if (pluginInfo != null)
                        {
                            Console.WriteLine($"[VstHost]         -> Valid VST3: {pluginInfo.Name}");
                            _discoveredVst3Plugins.Add(pluginInfo);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[VstHost]     ERROR: {ex.Message}");
                    _logger?.LogWarning(ex, "Error scanning VST path '{Path}'", basePath);
                }
            }

            Console.WriteLine($"[VstHost] Scan complete: {_discoveredPlugins.Count} VST2, {_discoveredVst3Plugins.Count} VST3");
            return new List<VstPluginInfo>(_discoveredPlugins);
        }
    }

    /// <summary>
    /// Asynchronously scans for VST plugins with progress reporting.
    /// </summary>
    public async Task<List<VstPluginInfo>> ScanForPluginsAsync(
        IProgress<VstScanProgressEventArgs>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                _discoveredPlugins.Clear();
                _discoveredVst3Plugins.Clear();

                var searchPaths = new List<string>(Settings.VstPluginSearchPaths);
                if (!string.IsNullOrEmpty(Settings.VstPluginPath))
                {
                    searchPaths.Insert(0, Settings.VstPluginPath);
                }

                int totalPaths = searchPaths.Count;
                int currentPath = 0;

                foreach (var basePath in searchPaths)
                {
                    currentPath++;

                    if (!Directory.Exists(basePath)) continue;

                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // Scan for VST2 plugins
                        var dllFiles = Directory.GetFiles(basePath, "*.dll", SearchOption.AllDirectories);
                        foreach (var file in dllFiles)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            progress?.Report(new VstScanProgressEventArgs(
                                basePath,
                                _discoveredPlugins.Count + _discoveredVst3Plugins.Count,
                                totalPaths,
                                Path.GetFileName(file)));

                            var pluginInfo = ProbePlugin(file, false);
                            if (pluginInfo != null)
                            {
                                _discoveredPlugins.Add(pluginInfo);
                            }
                        }

                        // Scan for VST3 plugins
                        var vst3Files = Directory.GetFiles(basePath, "*.vst3", SearchOption.AllDirectories);
                        foreach (var file in vst3Files)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var parentDir = Path.GetDirectoryName(file);
                            if (parentDir != null && (parentDir.EndsWith("x86_64-win") || parentDir.EndsWith("Win64")))
                            {
                                continue;
                            }

                            progress?.Report(new VstScanProgressEventArgs(
                                basePath,
                                _discoveredPlugins.Count + _discoveredVst3Plugins.Count,
                                totalPaths,
                                Path.GetFileName(file)));

                            var pluginInfo = ProbeVst3Plugin(file);
                            if (pluginInfo != null)
                            {
                                _discoveredVst3Plugins.Add(pluginInfo);
                            }
                        }

                        // Scan for VST3 bundles
                        var vst3Dirs = Directory.GetDirectories(basePath, "*.vst3", SearchOption.AllDirectories);
                        foreach (var dir in vst3Dirs)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            progress?.Report(new VstScanProgressEventArgs(
                                basePath,
                                _discoveredPlugins.Count + _discoveredVst3Plugins.Count,
                                totalPaths,
                                Path.GetFileName(dir)));

                            var pluginInfo = ScanVst3Bundle(dir);
                            if (pluginInfo != null)
                            {
                                _discoveredVst3Plugins.Add(pluginInfo);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Error scanning VST path '{Path}'", basePath);
                    }
                }

                _logger?.LogInformation("VST scan complete: {Vst2Count} VST2, {Vst3Count} VST3 plugins found",
                    _discoveredPlugins.Count, _discoveredVst3Plugins.Count);

                return new List<VstPluginInfo>(_discoveredPlugins);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Asynchronously scans for VST plugins with progress reporting using the new record-based progress type.
    /// </summary>
    /// <param name="progress">Optional progress reporter using the <see cref="Progress.VstScanProgress"/> record.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the scan.</param>
    /// <returns>A list of discovered VST2 plugin information.</returns>
    /// <remarks>
    /// This overload uses the immutable <see cref="Progress.VstScanProgress"/> record type for progress reporting,
    /// which provides a cleaner API with computed properties like <see cref="Progress.VstScanProgress.PercentComplete"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// var vstHost = new VstHost();
    /// var progress = new Progress&lt;VstScanProgress&gt;(p =>
    ///     Console.WriteLine($"[{p.PercentComplete:F0}%] {p.CurrentPlugin} - {(p.IsValid ? "Valid" : "Invalid")}"));
    ///
    /// var plugins = await vstHost.ScanForPluginsAsync(progress, cancellationToken);
    /// </code>
    /// </example>
    public async Task<List<VstPluginInfo>> ScanForPluginsAsync(
        IProgress<Progress.VstScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                _discoveredPlugins.Clear();
                _discoveredVst3Plugins.Clear();

                var searchPaths = new List<string>(Settings.VstPluginSearchPaths);
                if (!string.IsNullOrEmpty(Settings.VstPluginPath))
                {
                    searchPaths.Insert(0, Settings.VstPluginPath);
                }

                // First, collect all files to get accurate total count
                var allFiles = new List<(string path, bool isVst3, bool isBundle)>();

                foreach (var basePath in searchPaths)
                {
                    if (!Directory.Exists(basePath)) continue;

                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // Collect VST2 plugins (.dll)
                        foreach (var file in Directory.GetFiles(basePath, "*.dll", SearchOption.AllDirectories))
                        {
                            allFiles.Add((file, false, false));
                        }

                        // Collect VST3 single-file plugins
                        foreach (var file in Directory.GetFiles(basePath, "*.vst3", SearchOption.AllDirectories))
                        {
                            var parentDir = Path.GetDirectoryName(file);
                            if (parentDir != null && !parentDir.EndsWith("x86_64-win") && !parentDir.EndsWith("Win64"))
                            {
                                allFiles.Add((file, true, false));
                            }
                        }

                        // Collect VST3 bundles
                        foreach (var dir in Directory.GetDirectories(basePath, "*.vst3", SearchOption.AllDirectories))
                        {
                            allFiles.Add((dir, true, true));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Error collecting files from VST path '{Path}'", basePath);
                    }
                }

                int totalCount = allFiles.Count;
                int scannedCount = 0;

                progress?.Report(Progress.VstScanProgress.Starting(totalCount));

                foreach (var (path, isVst3, isBundle) in allFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    scannedCount++;

                    var pluginName = Path.GetFileNameWithoutExtension(path);
                    bool isValid = false;
                    string? errorMessage = null;

                    try
                    {
                        if (isVst3)
                        {
                            Vst3PluginInfo? pluginInfo;
                            if (isBundle)
                            {
                                pluginInfo = ScanVst3Bundle(path);
                            }
                            else
                            {
                                pluginInfo = ProbeVst3Plugin(path);
                            }

                            if (pluginInfo != null)
                            {
                                _discoveredVst3Plugins.Add(pluginInfo);
                                isValid = true;
                                pluginName = pluginInfo.Name;
                            }
                        }
                        else
                        {
                            var pluginInfo = ProbePlugin(path, false);
                            if (pluginInfo != null)
                            {
                                _discoveredPlugins.Add(pluginInfo);
                                isValid = true;
                                pluginName = pluginInfo.Name;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        errorMessage = ex.Message;
                        _logger?.LogWarning(ex, "Error probing plugin '{Path}'", path);
                    }

                    progress?.Report(new Progress.VstScanProgress(
                        pluginName,
                        scannedCount,
                        totalCount,
                        isValid,
                        errorMessage)
                    {
                        IsVst3 = isVst3,
                        CurrentPath = Path.GetDirectoryName(path)
                    });
                }

                progress?.Report(Progress.VstScanProgress.Complete(_discoveredPlugins.Count + _discoveredVst3Plugins.Count));

                _logger?.LogInformation("VST scan complete: {Vst2Count} VST2, {Vst3Count} VST3 plugins found",
                    _discoveredPlugins.Count, _discoveredVst3Plugins.Count);

                return new List<VstPluginInfo>(_discoveredPlugins);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Get all discovered plugins (both VST2 and VST3) as a combined list.
    /// </summary>
    public List<object> GetAllDiscoveredPlugins()
    {
        lock (_lock)
        {
            var result = new List<object>();
            result.AddRange(_discoveredPlugins);
            result.AddRange(_discoveredVst3Plugins);
            return result;
        }
    }

    /// <summary>
    /// Probe a potential VST plugin file to get its info
    /// </summary>
    private VstPluginInfo? ProbePlugin(string path, bool isVst3)
    {
        try
        {
            // Basic validation - check if file exists and is accessible
            if (!File.Exists(path)) return null;

            var info = new FileInfo(path);
            if (info.Length < 1024) return null; // Too small to be a VST

            // For now, create basic info from filename
            // Real implementation would load and query the plugin
            var name = Path.GetFileNameWithoutExtension(path);

            return new VstPluginInfo
            {
                Name = name,
                Path = path,
                Vendor = "Unknown",
                Version = "1.0",
                UniqueId = path.GetHashCode(),
                IsInstrument = GuessIsInstrument(name),
                IsLoaded = false,
                NumInputs = 2,
                NumOutputs = 2,
                NumParameters = 0,
                NumPrograms = 1
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Scan a VST3 bundle directory and probe the plugin inside.
    /// </summary>
    private Vst3PluginInfo? ScanVst3Bundle(string bundlePath)
    {
        try
        {
            if (!Directory.Exists(bundlePath)) return null;

            var resolvedPath = ResolveBundlePath(bundlePath);
            if (resolvedPath == null || !File.Exists(resolvedPath))
            {
                // No valid binary found, create basic info
                var name = Path.GetFileNameWithoutExtension(bundlePath);
                return new Vst3PluginInfo
                {
                    Name = name,
                    Path = bundlePath,
                    ResolvedPath = "",
                    Vendor = "Unknown",
                    Version = "1.0",
                    IsInstrument = GuessIsInstrument(name),
                    IsBundle = true,
                    NumInputs = 2,
                    NumOutputs = 2
                };
            }

            // Probe the resolved plugin file
            var info = ProbeVst3Plugin(resolvedPath);
            if (info != null)
            {
                info.Path = bundlePath;
                info.ResolvedPath = resolvedPath;
                info.IsBundle = true;
            }
            return info;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error scanning VST3 bundle '{Path}'", bundlePath);
            return null;
        }
    }

    /// <summary>
    /// Resolve a VST3 bundle path to the actual DLL inside.
    /// For bundles, looks in Contents/x86_64-win/*.vst3 or Contents/Win64/*.vst3
    /// </summary>
    /// <param name="path">Path to the .vst3 file or bundle directory</param>
    /// <returns>Resolved path to the actual DLL, or null if not found</returns>
    public string? ResolveBundlePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        // If it's a file, return as-is
        if (File.Exists(path))
        {
            return path;
        }

        // If it's a directory (bundle), look for the actual DLL inside
        if (Directory.Exists(path))
        {
            // Try x86_64-win first (64-bit Windows standard)
            var contentsPath = Path.Combine(path, "Contents", "x86_64-win");
            if (Directory.Exists(contentsPath))
            {
                var dlls = Directory.GetFiles(contentsPath, "*.vst3");
                if (dlls.Length > 0) return dlls[0];
            }

            // Try Win64 (alternative folder name)
            contentsPath = Path.Combine(path, "Contents", "Win64");
            if (Directory.Exists(contentsPath))
            {
                var dlls = Directory.GetFiles(contentsPath, "*.vst3");
                if (dlls.Length > 0) return dlls[0];
            }

            // Try i386-win (32-bit Windows)
            contentsPath = Path.Combine(path, "Contents", "i386-win");
            if (Directory.Exists(contentsPath))
            {
                var dlls = Directory.GetFiles(contentsPath, "*.vst3");
                if (dlls.Length > 0) return dlls[0];
            }

            // Try Win32 (alternative 32-bit folder name)
            contentsPath = Path.Combine(path, "Contents", "Win32");
            if (Directory.Exists(contentsPath))
            {
                var dlls = Directory.GetFiles(contentsPath, "*.vst3");
                if (dlls.Length > 0) return dlls[0];
            }
        }

        return null;
    }

    /// <summary>
    /// Probe a VST3 plugin file to get its info.
    /// Loads the factory temporarily, reads class info, and unloads without full initialization.
    /// </summary>
    /// <param name="path">Path to the VST3 plugin file</param>
    /// <returns>VST3 plugin info or null if probing failed</returns>
    public Vst3PluginInfo? ProbeVst3Plugin(string path)
    {
        IntPtr moduleHandle = IntPtr.Zero;
        PluginFactoryWrapper? factory = null;

        try
        {
            // Basic validation
            if (!File.Exists(path)) return null;

            var fileInfo = new FileInfo(path);
            if (fileInfo.Length < 1024) return null; // Too small to be a VST3

            // Safe scan mode: Skip native DLL loading to prevent crashes from corrupt plugins
            if (SafeScanMode)
            {
                Console.WriteLine($"[VstHost]         (Safe mode: skipping native probe)");
                return CreateBasicVst3Info(path);
            }

            // Load the DLL
            moduleHandle = LoadLibraryW(path);
            if (moduleHandle == IntPtr.Zero)
            {
                return CreateBasicVst3Info(path);
            }

            // Get the factory entry point
            IntPtr getFactoryProc = GetProcAddress(moduleHandle, "GetPluginFactory");
            if (getFactoryProc == IntPtr.Zero)
            {
                FreeLibrary(moduleHandle);
                return CreateBasicVst3Info(path);
            }

            // Call GetPluginFactory
            var getFactory = Marshal.GetDelegateForFunctionPointer<GetPluginFactoryDelegate>(getFactoryProc);
            IntPtr factoryPtr = getFactory();
            if (factoryPtr == IntPtr.Zero)
            {
                FreeLibrary(moduleHandle);
                return CreateBasicVst3Info(path);
            }

            // Wrap the factory
            factory = new PluginFactoryWrapper(factoryPtr);

            // Get factory info (vendor, url, email)
            string vendor = "Unknown";
            if (factory.GetFactoryInfo(out Vst3FactoryInfo factoryInfo) == (int)Vst3Result.Ok)
            {
                vendor = factoryInfo.Vendor ?? "Unknown";
            }

            // Get class count
            int classCount = factory.CountClasses();
            if (classCount <= 0)
            {
                factory.Dispose();
                FreeLibrary(moduleHandle);
                return CreateBasicVst3Info(path);
            }

            // Find the first audio processor class
            Vst3PluginInfo? result = null;

            for (int i = 0; i < classCount; i++)
            {
                // Try to get ClassInfo2 first (has more info)
                if (factory.IsFactory2 && factory.GetClassInfo2(i, out Vst3ClassInfo2 classInfo2) == (int)Vst3Result.Ok)
                {
                    // Check if this is an audio processor (kVstAudioEffectClass)
                    if (classInfo2.Category == "Audio Module Class" ||
                        classInfo2.Category?.Contains("Audio", StringComparison.OrdinalIgnoreCase) == true ||
                        string.IsNullOrEmpty(classInfo2.Category))
                    {
                        result = new Vst3PluginInfo
                        {
                            Name = classInfo2.Name ?? Path.GetFileNameWithoutExtension(path),
                            Path = path,
                            ResolvedPath = path,
                            Vendor = !string.IsNullOrEmpty(classInfo2.Vendor) ? classInfo2.Vendor : vendor,
                            Version = classInfo2.Version ?? "1.0",
                            SdkVersion = classInfo2.SdkVersion ?? "",
                            ClassId = classInfo2.Cid.ToGuid(),
                            Category = classInfo2.Category ?? "",
                            SubCategories = classInfo2.SubCategories ?? "",
                            ClassFlags = classInfo2.ClassFlags,
                            IsInstrument = Vst3PluginInfo.DetermineIsInstrument(classInfo2.Category ?? "", classInfo2.SubCategories ?? ""),
                            IsBundle = false,
                            NumInputs = 2,
                            NumOutputs = 2
                        };
                        break;
                    }
                }
                else if (factory.GetClassInfo(i, out Vst3ClassInfo classInfo) == (int)Vst3Result.Ok)
                {
                    // Basic ClassInfo (version 1)
                    if (classInfo.Category == "Audio Module Class" ||
                        classInfo.Category?.Contains("Audio", StringComparison.OrdinalIgnoreCase) == true ||
                        string.IsNullOrEmpty(classInfo.Category))
                    {
                        result = new Vst3PluginInfo
                        {
                            Name = classInfo.Name ?? Path.GetFileNameWithoutExtension(path),
                            Path = path,
                            ResolvedPath = path,
                            Vendor = vendor,
                            Version = "1.0",
                            ClassId = classInfo.Cid.ToGuid(),
                            Category = classInfo.Category ?? "",
                            IsInstrument = GuessIsInstrument(classInfo.Name ?? ""),
                            IsBundle = false,
                            NumInputs = 2,
                            NumOutputs = 2
                        };
                        break;
                    }
                }
            }

            // Cleanup
            factory.Dispose();
            FreeLibrary(moduleHandle);

            return result ?? CreateBasicVst3Info(path);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error probing VST3 plugin '{Path}'", path);

            // Cleanup on error
            try
            {
                factory?.Dispose();
                if (moduleHandle != IntPtr.Zero)
                {
                    FreeLibrary(moduleHandle);
                }
            }
            catch (Exception cleanupEx)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to cleanup VST3 plugin resources for '{path}': {cleanupEx.Message}");
                // Continue execution - cleanup failure is non-critical, we'll return basic info anyway
            }

            return CreateBasicVst3Info(path);
        }
    }

    /// <summary>
    /// Create basic VST3 plugin info from filename when probing fails.
    /// </summary>
    private Vst3PluginInfo CreateBasicVst3Info(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return new Vst3PluginInfo
        {
            Name = name,
            Path = path,
            ResolvedPath = path,
            Vendor = "Unknown",
            Version = "1.0",
            IsInstrument = GuessIsInstrument(name),
            IsBundle = false,
            NumInputs = 2,
            NumOutputs = 2
        };
    }

    /// <summary>
    /// Guess if a plugin is an instrument based on its name
    /// </summary>
    private bool GuessIsInstrument(string name)
    {
        var lowerName = name.ToLowerInvariant();
        var instrumentKeywords = new[] { "synth", "piano", "organ", "bass", "lead", "pad", "strings", "brass", "drum", "sampler", "rompler", "keys", "vsti" };
        foreach (var keyword in instrumentKeywords)
        {
            if (lowerName.Contains(keyword)) return true;
        }
        return false;
    }

    /// <summary>
    /// Load a VST plugin by name (partial match supported).
    /// Returns IVstPlugin interface which works for both VST2 and VST3.
    /// </summary>
    public IVstPlugin? LoadPlugin(string nameOrPath)
    {
        lock (_lock)
        {
            // Check if already loaded
            if (_loadedPlugins.TryGetValue(nameOrPath, out var existing))
            {
                return existing;
            }

            // First check if it's a VST3 plugin
            bool isVst3 = nameOrPath.EndsWith(".vst3", StringComparison.OrdinalIgnoreCase) ||
                          (Directory.Exists(nameOrPath) && nameOrPath.EndsWith(".vst3", StringComparison.OrdinalIgnoreCase));

            // If explicit path provided
            if (File.Exists(nameOrPath) || Directory.Exists(nameOrPath))
            {
                if (isVst3)
                {
                    return LoadVst3PluginByPath(nameOrPath);
                }
                else
                {
                    return LoadVst2PluginByPath(nameOrPath);
                }
            }

            // Try to find in discovered VST3 plugins first
            var vst3Info = _discoveredVst3Plugins.Find(p =>
                p.Name.Equals(nameOrPath, StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains(nameOrPath, StringComparison.OrdinalIgnoreCase));

            if (vst3Info != null)
            {
                return LoadVst3Plugin(vst3Info);
            }

            // Try to find in discovered VST2 plugins
            var vst2Info = _discoveredPlugins.Find(p =>
                p.Name.Equals(nameOrPath, StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains(nameOrPath, StringComparison.OrdinalIgnoreCase));

            if (vst2Info != null)
            {
                return LoadVst2Plugin(vst2Info);
            }

            _logger?.LogWarning("VST plugin not found: {NameOrPath}", nameOrPath);
            return null;
        }
    }

    /// <summary>
    /// Load a VST2 plugin by path.
    /// </summary>
    private IVstPlugin? LoadVst2PluginByPath(string path)
    {
        var info = _discoveredPlugins.Find(p => p.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (info == null)
        {
            // Not in discovered list, probe it
            info = ProbePlugin(path, false);
            if (info != null) _discoveredPlugins.Add(info);
        }

        return info != null ? LoadVst2Plugin(info) : null;
    }

    /// <summary>
    /// Load a VST3 plugin by path.
    /// </summary>
    private IVstPlugin? LoadVst3PluginByPath(string path)
    {
        var info = _discoveredVst3Plugins.Find(p =>
            p.Path.Equals(path, StringComparison.OrdinalIgnoreCase) ||
            p.ResolvedPath.Equals(path, StringComparison.OrdinalIgnoreCase));

        if (info == null)
        {
            // Not in discovered list, probe it
            if (Directory.Exists(path))
            {
                info = ScanVst3Bundle(path);
            }
            else
            {
                info = ProbeVst3Plugin(path);
            }

            if (info != null) _discoveredVst3Plugins.Add(info);
        }

        return info != null ? LoadVst3Plugin(info) : null;
    }

    /// <summary>
    /// Load a VST2 plugin from its info.
    /// </summary>
    private IVstPlugin? LoadVst2Plugin(VstPluginInfo info)
    {
        try
        {
            // Check if already loaded
            if (_loadedPlugins.TryGetValue(info.Name, out var existing))
            {
                return existing;
            }

            var plugin = new VstPlugin(info);
            info.IsLoaded = true;
            _loadedPlugins[info.Name] = plugin;
            _logger?.LogInformation("Loaded VST2 plugin: {Name} ({Type})", info.Name, info.IsInstrument ? "Instrument" : "Effect");
            return plugin;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load VST2 plugin '{Name}'", info.Name);
            return null;
        }
    }

    /// <summary>
    /// Load a VST3 plugin from its info.
    /// Creates a Vst3Plugin instance with full VST3 support including
    /// audio processing, parameter management, and GUI support.
    /// </summary>
    private IVstPlugin? LoadVst3Plugin(Vst3PluginInfo info)
    {
        try
        {
            // Check if already loaded
            if (_loadedPlugins.TryGetValue(info.Name, out var existing))
            {
                return existing;
            }

            // Resolve the actual plugin path for bundles
            string pluginPath = info.IsBundle && !string.IsNullOrEmpty(info.ResolvedPath)
                ? info.ResolvedPath
                : info.Path;

            // Create a proper Vst3Plugin instance for full VST3 support
            var plugin = new Vst3Plugin(pluginPath);
            info.IsLoaded = true;
            _loadedPlugins[info.Name] = plugin;
            _logger?.LogInformation("Loaded VST3 plugin: {Name} ({Type})", info.Name, info.IsInstrument ? "Instrument" : "Effect");
            return plugin;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load VST3 plugin '{Name}'", info.Name);
            return null;
        }
    }

    /// <summary>
    /// Load a VST plugin by name, returning VstPlugin for backwards compatibility.
    /// For new code, prefer using LoadPlugin which returns IVstPlugin.
    /// </summary>
    public VstPlugin? LoadVst2Plugin(string nameOrPath)
    {
        var plugin = LoadPlugin(nameOrPath);
        return plugin as VstPlugin;
    }

    /// <summary>
    /// Load a VST2 plugin by index from the discovered VST2 list.
    /// </summary>
    public IVstPlugin? LoadPluginByIndex(int index)
    {
        lock (_lock)
        {
            if (index < 0 || index >= _discoveredPlugins.Count)
            {
                _logger?.LogWarning("Invalid VST2 plugin index: {Index}", index);
                return null;
            }

            return LoadVst2Plugin(_discoveredPlugins[index]);
        }
    }

    /// <summary>
    /// Load a VST3 plugin by index from the discovered VST3 list.
    /// </summary>
    public IVstPlugin? LoadVst3PluginByIndex(int index)
    {
        lock (_lock)
        {
            if (index < 0 || index >= _discoveredVst3Plugins.Count)
            {
                _logger?.LogWarning("Invalid VST3 plugin index: {Index}", index);
                return null;
            }

            return LoadVst3Plugin(_discoveredVst3Plugins[index]);
        }
    }

    /// <summary>
    /// Get a loaded plugin by name (returns IVstPlugin interface).
    /// </summary>
    public IVstPlugin? GetPlugin(string name)
    {
        lock (_lock)
        {
            if (_loadedPlugins.TryGetValue(name, out var plugin))
            {
                return plugin;
            }

            // Try partial match
            foreach (var kvp in _loadedPlugins)
            {
                if (kvp.Key.Contains(name, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Get a loaded VST2 plugin by name (for backwards compatibility).
    /// </summary>
    public VstPlugin? GetVstPlugin(string name)
    {
        return GetPlugin(name) as VstPlugin;
    }

    /// <summary>
    /// Unload a VST plugin.
    /// </summary>
    public void UnloadPlugin(string name)
    {
        lock (_lock)
        {
            if (_loadedPlugins.TryGetValue(name, out var plugin))
            {
                plugin.Dispose();
                _loadedPlugins.Remove(name);

                // Update VST2 discovered list
                var vst2Info = _discoveredPlugins.Find(p => p.Name == name);
                if (vst2Info != null) vst2Info.IsLoaded = false;

                // Update VST3 discovered list
                var vst3Info = _discoveredVst3Plugins.Find(p => p.Name == name);
                if (vst3Info != null) vst3Info.IsLoaded = false;

                _logger?.LogInformation("Unloaded VST plugin: {Name}", name);
            }
        }
    }

    /// <summary>
    /// Print all discovered plugins to console.
    /// </summary>
    public void PrintDiscoveredPlugins()
    {
        Console.WriteLine("\n=== Discovered VST2 Plugins ===");
        if (_discoveredPlugins.Count == 0)
        {
            Console.WriteLine("  No VST2 plugins found.");
        }
        else
        {
            for (int i = 0; i < _discoveredPlugins.Count; i++)
            {
                var p = _discoveredPlugins[i];
                var type = p.IsInstrument ? "VSTi" : "VST";
                var loaded = p.IsLoaded ? " [LOADED]" : "";
                Console.WriteLine($"  [{i}] {p.Name} ({type}){loaded}");
                Console.WriteLine($"      Path: {p.Path}");
            }
        }
        Console.WriteLine("===============================\n");

        Console.WriteLine("=== Discovered VST3 Plugins ===");
        if (_discoveredVst3Plugins.Count == 0)
        {
            Console.WriteLine("  No VST3 plugins found.");
        }
        else
        {
            for (int i = 0; i < _discoveredVst3Plugins.Count; i++)
            {
                var p = _discoveredVst3Plugins[i];
                var type = p.IsInstrument ? "VSTi" : "VST";
                var loaded = p.IsLoaded ? " [LOADED]" : "";
                var bundle = p.IsBundle ? " [BUNDLE]" : "";
                Console.WriteLine($"  [{i}] {p.Name} (VST3 {type}){loaded}{bundle}");
                Console.WriteLine($"      Vendor: {p.Vendor}");
                Console.WriteLine($"      Path: {p.Path}");
                if (p.IsBundle && !string.IsNullOrEmpty(p.ResolvedPath))
                {
                    Console.WriteLine($"      Binary: {p.ResolvedPath}");
                }
                if (!string.IsNullOrEmpty(p.SubCategories))
                {
                    Console.WriteLine($"      Categories: {p.SubCategories}");
                }
            }
        }

        if (_discoveredPlugins.Count == 0 && _discoveredVst3Plugins.Count == 0)
        {
            Console.WriteLine("  Search paths:");
            foreach (var path in Settings.VstPluginSearchPaths)
            {
                Console.WriteLine($"    - {path}");
            }
        }
        Console.WriteLine("===============================\n");
    }

    /// <summary>
    /// Print all loaded plugins to console.
    /// </summary>
    public void PrintLoadedPlugins()
    {
        Console.WriteLine("\n=== Loaded VST Plugins ===");
        if (_loadedPlugins.Count == 0)
        {
            Console.WriteLine("  No plugins currently loaded.");
        }
        else
        {
            foreach (var kvp in _loadedPlugins)
            {
                var p = kvp.Value;
                var type = p.IsInstrument ? "VSTi" : "VST";
                var format = p.IsVst3 ? "VST3" : "VST2";
                Console.WriteLine($"  - {p.Name} ({format} {type})");
            }
        }
        Console.WriteLine("==========================\n");
    }

    #region Preset Loading Utilities

    /// <summary>
    /// Scan a directory for preset files (.fxp, .fxb) compatible with a plugin
    /// </summary>
    /// <param name="directory">Directory to scan</param>
    /// <param name="pluginName">Optional plugin name to filter by (searches in subdirectories)</param>
    /// <returns>List of preset file paths</returns>
    public List<string> ScanForPresets(string directory, string? pluginName = null)
    {
        var presets = new List<string>();

        if (!Directory.Exists(directory))
        {
            return presets;
        }

        try
        {
            // If plugin name specified, try to find plugin-specific folder first
            if (!string.IsNullOrEmpty(pluginName))
            {
                var pluginDir = Path.Combine(directory, pluginName);
                if (Directory.Exists(pluginDir))
                {
                    presets.AddRange(Directory.GetFiles(pluginDir, "*.fxp", SearchOption.AllDirectories));
                    presets.AddRange(Directory.GetFiles(pluginDir, "*.fxb", SearchOption.AllDirectories));
                }
            }

            // Also scan the main directory
            presets.AddRange(Directory.GetFiles(directory, "*.fxp", SearchOption.AllDirectories));
            presets.AddRange(Directory.GetFiles(directory, "*.fxb", SearchOption.AllDirectories));

            // Remove duplicates
            presets = presets.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error scanning for presets");
        }

        return presets;
    }

    /// <summary>
    /// Get common preset directories for a platform
    /// </summary>
    /// <returns>List of common preset directories</returns>
    public static List<string> GetCommonPresetDirectories()
    {
        var directories = new List<string>();

        // Windows common preset locations
        string? userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string? appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string? localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string? documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        if (!string.IsNullOrEmpty(documents))
        {
            directories.Add(Path.Combine(documents, "VST Presets"));
            directories.Add(Path.Combine(documents, "VST3 Presets"));
            directories.Add(Path.Combine(documents, "Presets"));
        }

        if (!string.IsNullOrEmpty(appData))
        {
            directories.Add(Path.Combine(appData, "VST Presets"));
            directories.Add(Path.Combine(appData, "VST3 Presets"));
        }

        // Steinberg standard location
        directories.Add(@"C:\Users\Public\Documents\Steinberg\VST Presets");
        directories.Add(@"C:\ProgramData\Steinberg\VST Presets");

        return directories.Where(Directory.Exists).ToList();
    }

    /// <summary>
    /// Load a preset file into a plugin by name
    /// </summary>
    /// <param name="pluginName">Name of the loaded plugin</param>
    /// <param name="presetPath">Path to the preset file</param>
    /// <returns>True if successful</returns>
    public bool LoadPresetForPlugin(string pluginName, string presetPath)
    {
        var plugin = GetPlugin(pluginName);
        if (plugin == null)
        {
            _logger?.LogWarning("Plugin '{Name}' not loaded", pluginName);
            return false;
        }

        return plugin.LoadPreset(presetPath);
    }

    /// <summary>
    /// Save a plugin's current state to a preset file
    /// </summary>
    /// <param name="pluginName">Name of the loaded plugin</param>
    /// <param name="presetPath">Path for the preset file</param>
    /// <returns>True if successful</returns>
    public bool SavePresetForPlugin(string pluginName, string presetPath)
    {
        var plugin = GetPlugin(pluginName);
        if (plugin == null)
        {
            _logger?.LogWarning("Plugin '{Name}' not loaded", pluginName);
            return false;
        }

        return plugin.SavePreset(presetPath);
    }

    /// <summary>
    /// Create a preset bank from multiple preset files
    /// </summary>
    /// <param name="presetPaths">List of .fxp preset file paths</param>
    /// <param name="outputPath">Output .fxb bank file path</param>
    /// <returns>True if successful</returns>
    public bool CreatePresetBank(IEnumerable<string> presetPaths, string outputPath)
    {
        try
        {
            var presets = presetPaths.ToList();
            if (presets.Count == 0)
            {
                _logger?.LogWarning("No presets provided for bank creation");
                return false;
            }

            // Read and combine preset data (simplified implementation)
            // Full implementation would parse and combine FXP files properly
            using var output = File.Create(outputPath);
            using var writer = new BinaryWriter(output);

            // Write FXB header
            uint chunkMagic = 0x4B6E6343; // 'CcnK'
            uint bankMagic = 0x68436246;  // 'FBCh'

            writer.Write(SwapEndian(chunkMagic));
            // Size will be written at the end
            long sizePosition = output.Position;
            writer.Write(0); // Placeholder for size
            writer.Write(SwapEndian(bankMagic));
            writer.Write(SwapEndian(1u)); // Version
            writer.Write(0); // FX ID (placeholder)
            writer.Write(SwapEndian(1u)); // FX Version
            writer.Write(SwapEndian((uint)presets.Count)); // Num programs

            // Write current program (0)
            writer.Write(SwapEndian(0u));

            // Reserved (124 bytes)
            writer.Write(new byte[124]);

            // For each preset, read and write the program data
            foreach (var presetPath in presets)
            {
                if (File.Exists(presetPath))
                {
                    var presetData = File.ReadAllBytes(presetPath);
                    // Skip the header and write the program data
                    if (presetData.Length > 60)
                    {
                        writer.Write(presetData, 28, presetData.Length - 28);
                    }
                }
            }

            // Go back and write the size
            long endPosition = output.Position;
            output.Position = sizePosition;
            writer.Write(SwapEndian((uint)(endPosition - 8)));

            _logger?.LogInformation("Created preset bank: {Path}", outputPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error creating preset bank");
            return false;
        }
    }

    private static uint SwapEndian(uint value)
    {
        return ((value & 0xFF) << 24) |
               ((value & 0xFF00) << 8) |
               ((value & 0xFF0000) >> 8) |
               ((value & 0xFF000000) >> 24);
    }

    #endregion

    #region Parameter Discovery Helpers

    /// <summary>
    /// Discover all parameters for a loaded plugin
    /// </summary>
    /// <param name="pluginName">Name of the loaded plugin</param>
    /// <returns>List of parameter info tuples (index, name, value, display)</returns>
    public List<(int Index, string Name, float Value, string Display)> DiscoverParameters(string pluginName)
    {
        var parameters = new List<(int Index, string Name, float Value, string Display)>();

        var plugin = GetPlugin(pluginName);
        if (plugin == null)
        {
            _logger?.LogWarning("Plugin '{Name}' not loaded", pluginName);
            return parameters;
        }

        int count = plugin.GetParameterCount();
        for (int i = 0; i < count; i++)
        {
            parameters.Add((
                i,
                plugin.GetParameterName(i),
                plugin.GetParameterValue(i),
                plugin.GetParameterDisplay(i)
            ));
        }

        return parameters;
    }

    /// <summary>
    /// Print all parameters for a plugin to console
    /// </summary>
    /// <param name="pluginName">Name of the loaded plugin</param>
    public void PrintParameters(string pluginName)
    {
        var plugin = GetPlugin(pluginName);
        if (plugin == null)
        {
            Console.WriteLine($"Plugin '{pluginName}' not loaded");
            return;
        }

        Console.WriteLine($"\n=== Parameters for {pluginName} ===");
        int count = plugin.GetParameterCount();

        if (count == 0)
        {
            Console.WriteLine("  No parameters available.");
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                string name = plugin.GetParameterName(i);
                float value = plugin.GetParameterValue(i);
                string display = plugin.GetParameterDisplay(i);
                Console.WriteLine($"  [{i}] {name}: {display} ({value:F3})");
            }
        }
        Console.WriteLine("==============================\n");
    }

    /// <summary>
    /// Find parameters by name pattern
    /// </summary>
    /// <param name="pluginName">Name of the loaded plugin</param>
    /// <param name="pattern">Search pattern (case-insensitive)</param>
    /// <returns>List of matching parameter indices</returns>
    public List<int> FindParameters(string pluginName, string pattern)
    {
        var matches = new List<int>();

        var plugin = GetPlugin(pluginName);
        if (plugin == null)
        {
            return matches;
        }

        int count = plugin.GetParameterCount();
        for (int i = 0; i < count; i++)
        {
            string name = plugin.GetParameterName(i);
            if (name.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(i);
            }
        }

        return matches;
    }

    /// <summary>
    /// Set multiple parameters at once
    /// </summary>
    /// <param name="pluginName">Name of the loaded plugin</param>
    /// <param name="parameters">Dictionary of parameter index to value</param>
    public void SetParameters(string pluginName, Dictionary<int, float> parameters)
    {
        var plugin = GetPlugin(pluginName);
        if (plugin == null)
        {
            _logger?.LogWarning("Plugin '{Name}' not loaded", pluginName);
            return;
        }

        foreach (var kvp in parameters)
        {
            plugin.SetParameterValue(kvp.Key, kvp.Value);
        }
    }

    /// <summary>
    /// Copy parameter values from one plugin to another (useful for A/B comparison)
    /// </summary>
    /// <param name="sourcePluginName">Source plugin name</param>
    /// <param name="destPluginName">Destination plugin name</param>
    /// <returns>Number of parameters copied</returns>
    public int CopyParameters(string sourcePluginName, string destPluginName)
    {
        var source = GetPlugin(sourcePluginName);
        var dest = GetPlugin(destPluginName);

        if (source == null || dest == null)
        {
            _logger?.LogWarning("Source or destination plugin not loaded");
            return 0;
        }

        int count = Math.Min(source.GetParameterCount(), dest.GetParameterCount());
        for (int i = 0; i < count; i++)
        {
            dest.SetParameterValue(i, source.GetParameterValue(i));
        }

        _logger?.LogInformation("Copied {Count} parameters from {Source} to {Destination}", count, sourcePluginName, destPluginName);
        return count;
    }

    /// <summary>
    /// Create a snapshot of all parameter values for a plugin
    /// </summary>
    /// <param name="pluginName">Name of the loaded plugin</param>
    /// <returns>Dictionary of parameter index to value</returns>
    public Dictionary<int, float> CreateParameterSnapshot(string pluginName)
    {
        var snapshot = new Dictionary<int, float>();

        var plugin = GetPlugin(pluginName);
        if (plugin == null)
        {
            return snapshot;
        }

        int count = plugin.GetParameterCount();
        for (int i = 0; i < count; i++)
        {
            snapshot[i] = plugin.GetParameterValue(i);
        }

        return snapshot;
    }

    /// <summary>
    /// Restore parameters from a snapshot
    /// </summary>
    /// <param name="pluginName">Name of the loaded plugin</param>
    /// <param name="snapshot">Parameter snapshot to restore</param>
    public void RestoreParameterSnapshot(string pluginName, Dictionary<int, float> snapshot)
    {
        SetParameters(pluginName, snapshot);
    }

    /// <summary>
    /// Randomize parameters within specified ranges
    /// </summary>
    /// <param name="pluginName">Name of the loaded plugin</param>
    /// <param name="minValue">Minimum random value (0-1)</param>
    /// <param name="maxValue">Maximum random value (0-1)</param>
    /// <param name="excludeIndices">Parameter indices to exclude from randomization</param>
    public void RandomizeParameters(string pluginName, float minValue = 0f, float maxValue = 1f, HashSet<int>? excludeIndices = null)
    {
        var plugin = GetPlugin(pluginName);
        if (plugin == null)
        {
            _logger?.LogWarning("Plugin '{Name}' not loaded", pluginName);
            return;
        }

        var random = new Random();
        int count = plugin.GetParameterCount();

        for (int i = 0; i < count; i++)
        {
            if (excludeIndices != null && excludeIndices.Contains(i))
            {
                continue;
            }

            float value = minValue + (float)(random.NextDouble() * (maxValue - minValue));
            plugin.SetParameterValue(i, value);
        }

        _logger?.LogInformation("Randomized {Count} parameters for {Name}", count, pluginName);
    }

    #endregion

    public void Dispose()
    {
        if (_isDisposed) return;

        lock (_lock)
        {
            _isDisposed = true;

            foreach (var plugin in _loadedPlugins.Values)
            {
                plugin.Dispose();
            }
            _loadedPlugins.Clear();
            _discoveredPlugins.Clear();
            _discoveredVst3Plugins.Clear();
        }

        GC.SuppressFinalize(this);
    }
}
