// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: VST plugin hosting.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;


namespace MusicEngine.Core;


/// <summary>
/// Provides async extension methods for <see cref="VstHost"/> operations.
/// </summary>
/// <remarks>
/// These extension methods enable non-blocking VST plugin operations with support for:
/// <list type="bullet">
/// <item>Cancellation via <see cref="CancellationToken"/></item>
/// <item>Progress reporting via <see cref="IProgress{T}"/></item>
/// <item>Proper exception handling</item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// var vstHost = new VstHost();
/// var progress = new Progress&lt;VstScanProgress&gt;(p =>
///     Console.WriteLine($"[{p.CurrentPlugin}/{p.TotalPlugins}] {p.PluginName}"));
///
/// using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
/// var plugins = await vstHost.ScanForPluginsAsync(progress, cts.Token);
/// </code>
/// </example>
public static class VstHostAsync
{
    /// <summary>
    /// Asynchronously scans for VST plugins with detailed progress reporting and cancellation support.
    /// </summary>
    /// <param name="vstHost">The VST host instance.</param>
    /// <param name="progress">Optional progress reporter for scan status.</param>
    /// <param name="ct">Cancellation token to cancel the scan.</param>
    /// <returns>A task containing a list of discovered VST2 plugin information.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="vstHost"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    /// <remarks>
    /// This method scans all configured VST plugin paths for both VST2 (.dll) and VST3 (.vst3) plugins.
    /// The scan is performed on a background thread to avoid blocking the UI.
    ///
    /// Progress is reported for each plugin file discovered, including:
    /// <list type="bullet">
    /// <item>The plugin name and path</item>
    /// <item>Whether the plugin is valid</item>
    /// <item>Current progress percentage</item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// var vstHost = new VstHost();
    ///
    /// var progress = new Progress&lt;VstScanProgress&gt;(p =>
    /// {
    ///     if (p.IsValid == true)
    ///     {
    ///         Console.ForegroundColor = ConsoleColor.Green;
    ///     }
    ///     else if (p.IsValid == false)
    ///     {
    ///         Console.ForegroundColor = ConsoleColor.Red;
    ///     }
    ///
    ///     Console.WriteLine($"[{p.Percentage:F0}%] {p.PluginName}");
    ///     Console.ResetColor();
    /// });
    ///
    /// try
    /// {
    ///     var plugins = await vstHost.ScanForPluginsAsync(progress);
    ///     Console.WriteLine($"Found {plugins.Count} VST2 plugins");
    ///     Console.WriteLine($"Found {vstHost.DiscoveredVst3Plugins.Count} VST3 plugins");
    /// }
    /// catch (OperationCanceledException)
    /// {
    ///     Console.WriteLine("Scan was canceled");
    /// }
    /// </code>
    /// </example>
    public static async Task<List<VstPluginInfo>> ScanForPluginsAsync(
        this VstHost vstHost,
        IProgress<VstScanProgress>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(vstHost);

        return await Task.Run(() =>
        {
            var discoveredPlugins = new List<VstPluginInfo>();
            var discoveredVst3Plugins = new List<Vst3PluginInfo>();

            // Get search paths
            var searchPaths = new List<string>(Settings.VstPluginSearchPaths);
            if (!string.IsNullOrEmpty(Settings.VstPluginPath))
            {
                searchPaths.Insert(0, Settings.VstPluginPath);
            }

            // First pass: count total files to scan
            int totalFiles = 0;
            var allDllFiles = new List<string>();
            var allVst3Files = new List<string>();
            var allVst3Bundles = new List<string>();

            foreach (var basePath in searchPaths)
            {
                if (!Directory.Exists(basePath)) continue;

                try
                {
                    ct.ThrowIfCancellationRequested();

                    // Collect VST2 DLLs
                    var dllFiles = Directory.GetFiles(basePath, "*.dll", SearchOption.AllDirectories);
                    allDllFiles.AddRange(dllFiles);

                    // Collect VST3 files
                    var vst3Files = Directory.GetFiles(basePath, "*.vst3", SearchOption.AllDirectories);
                    foreach (var file in vst3Files)
                    {
                        var parentDir = Path.GetDirectoryName(file);
                        if (parentDir != null && !parentDir.EndsWith("x86_64-win") && !parentDir.EndsWith("Win64"))
                        {
                            allVst3Files.Add(file);
                        }
                    }

                    // Collect VST3 bundles
                    var vst3Dirs = Directory.GetDirectories(basePath, "*.vst3", SearchOption.AllDirectories);
                    allVst3Bundles.AddRange(vst3Dirs);
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip paths we cannot access
                }
                catch (DirectoryNotFoundException)
                {
                    // Skip paths that don't exist
                }
            }

            totalFiles = allDllFiles.Count + allVst3Files.Count + allVst3Bundles.Count;
            int currentFile = 0;

            progress?.Report(new VstScanProgress
            {
                CurrentPlugin = 0,
                TotalPlugins = totalFiles,
                PluginName = $"Found {totalFiles} potential plugins to scan"
            });

            // Scan VST2 DLLs
            foreach (var dllPath in allDllFiles)
            {
                ct.ThrowIfCancellationRequested();
                currentFile++;

                var pluginName = Path.GetFileNameWithoutExtension(dllPath);

                progress?.Report(new VstScanProgress
                {
                    CurrentPlugin = currentFile,
                    TotalPlugins = totalFiles,
                    PluginName = pluginName,
                    CurrentPath = Path.GetDirectoryName(dllPath),
                    IsVst3 = false,
                    IsValid = null // Will be set after probe
                });

                try
                {
                    // Check if file is a valid VST plugin (basic validation)
                    var fileInfo = new FileInfo(dllPath);
                    if (fileInfo.Length >= 1024) // Minimum size for a real DLL
                    {
                        var pluginInfo = new VstPluginInfo
                        {
                            Name = pluginName,
                            Path = dllPath,
                            Vendor = "Unknown",
                            Version = "1.0",
                            UniqueId = dllPath.GetHashCode(),
                            IsInstrument = GuessIsInstrument(pluginName),
                            IsLoaded = false,
                            NumInputs = 2,
                            NumOutputs = 2,
                            NumParameters = 0,
                            NumPrograms = 1
                        };

                        discoveredPlugins.Add(pluginInfo);

                        progress?.Report(new VstScanProgress
                        {
                            CurrentPlugin = currentFile,
                            TotalPlugins = totalFiles,
                            PluginName = pluginName,
                            CurrentPath = Path.GetDirectoryName(dllPath),
                            IsVst3 = false,
                            IsValid = true
                        });
                    }
                    else
                    {
                        progress?.Report(new VstScanProgress
                        {
                            CurrentPlugin = currentFile,
                            TotalPlugins = totalFiles,
                            PluginName = pluginName,
                            CurrentPath = Path.GetDirectoryName(dllPath),
                            IsVst3 = false,
                            IsValid = false,
                            ErrorMessage = "File too small to be a valid VST"
                        });
                    }
                }
                catch (Exception ex)
                {
                    progress?.Report(new VstScanProgress
                    {
                        CurrentPlugin = currentFile,
                        TotalPlugins = totalFiles,
                        PluginName = pluginName,
                        IsVst3 = false,
                        IsValid = false,
                        ErrorMessage = ex.Message
                    });
                }
            }

            // Scan VST3 files
            foreach (var vst3Path in allVst3Files)
            {
                ct.ThrowIfCancellationRequested();
                currentFile++;

                var pluginName = Path.GetFileNameWithoutExtension(vst3Path);

                progress?.Report(new VstScanProgress
                {
                    CurrentPlugin = currentFile,
                    TotalPlugins = totalFiles,
                    PluginName = pluginName,
                    CurrentPath = Path.GetDirectoryName(vst3Path),
                    IsVst3 = true,
                    IsValid = null
                });

                try
                {
                    var pluginInfo = vstHost.ProbeVst3Plugin(vst3Path);
                    if (pluginInfo != null)
                    {
                        discoveredVst3Plugins.Add(pluginInfo);

                        progress?.Report(new VstScanProgress
                        {
                            CurrentPlugin = currentFile,
                            TotalPlugins = totalFiles,
                            PluginName = pluginInfo.Name,
                            CurrentPath = Path.GetDirectoryName(vst3Path),
                            IsVst3 = true,
                            IsValid = true
                        });
                    }
                    else
                    {
                        progress?.Report(new VstScanProgress
                        {
                            CurrentPlugin = currentFile,
                            TotalPlugins = totalFiles,
                            PluginName = pluginName,
                            IsVst3 = true,
                            IsValid = false,
                            ErrorMessage = "Failed to probe VST3 plugin"
                        });
                    }
                }
                catch (Exception ex)
                {
                    progress?.Report(new VstScanProgress
                    {
                        CurrentPlugin = currentFile,
                        TotalPlugins = totalFiles,
                        PluginName = pluginName,
                        IsVst3 = true,
                        IsValid = false,
                        ErrorMessage = ex.Message
                    });
                }
            }

            // Scan VST3 bundles
            foreach (var bundlePath in allVst3Bundles)
            {
                ct.ThrowIfCancellationRequested();
                currentFile++;

                var pluginName = Path.GetFileNameWithoutExtension(bundlePath);

                progress?.Report(new VstScanProgress
                {
                    CurrentPlugin = currentFile,
                    TotalPlugins = totalFiles,
                    PluginName = pluginName,
                    CurrentPath = bundlePath,
                    IsVst3 = true,
                    IsValid = null
                });

                try
                {
                    var resolvedPath = vstHost.ResolveBundlePath(bundlePath);
                    if (!string.IsNullOrEmpty(resolvedPath) && File.Exists(resolvedPath))
                    {
                        var pluginInfo = vstHost.ProbeVst3Plugin(resolvedPath);
                        if (pluginInfo != null)
                        {
                            pluginInfo.Path = bundlePath;
                            pluginInfo.ResolvedPath = resolvedPath;
                            pluginInfo.IsBundle = true;
                            discoveredVst3Plugins.Add(pluginInfo);

                            progress?.Report(new VstScanProgress
                            {
                                CurrentPlugin = currentFile,
                                TotalPlugins = totalFiles,
                                PluginName = pluginInfo.Name,
                                CurrentPath = bundlePath,
                                IsVst3 = true,
                                IsValid = true
                            });
                        }
                        else
                        {
                            // Create basic info for unreadable bundle
                            discoveredVst3Plugins.Add(new Vst3PluginInfo
                            {
                                Name = pluginName,
                                Path = bundlePath,
                                ResolvedPath = resolvedPath,
                                Vendor = "Unknown",
                                Version = "1.0",
                                IsInstrument = GuessIsInstrument(pluginName),
                                IsBundle = true
                            });

                            progress?.Report(new VstScanProgress
                            {
                                CurrentPlugin = currentFile,
                                TotalPlugins = totalFiles,
                                PluginName = pluginName,
                                IsVst3 = true,
                                IsValid = true
                            });
                        }
                    }
                    else
                    {
                        progress?.Report(new VstScanProgress
                        {
                            CurrentPlugin = currentFile,
                            TotalPlugins = totalFiles,
                            PluginName = pluginName,
                            IsVst3 = true,
                            IsValid = false,
                            ErrorMessage = "Bundle does not contain a valid plugin binary"
                        });
                    }
                }
                catch (Exception ex)
                {
                    progress?.Report(new VstScanProgress
                    {
                        CurrentPlugin = currentFile,
                        TotalPlugins = totalFiles,
                        PluginName = pluginName,
                        IsVst3 = true,
                        IsValid = false,
                        ErrorMessage = ex.Message
                    });
                }
            }

            // Report completion
            progress?.Report(VstScanProgress.Complete(discoveredPlugins.Count + discoveredVst3Plugins.Count));

            // Update the host's internal lists by calling the synchronous scan
            // This ensures proper thread safety with the VstHost's internal locking
            return vstHost.ScanForPlugins();

        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously loads a VST plugin by name or path with cancellation support.
    /// </summary>
    /// <param name="vstHost">The VST host instance.</param>
    /// <param name="path">The name or path of the plugin to load.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task containing the loaded plugin, or null if loading failed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="vstHost"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or empty.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    /// <remarks>
    /// Loading a VST plugin can be time-consuming as it involves:
    /// <list type="bullet">
    /// <item>Loading the plugin DLL into memory</item>
    /// <item>Initializing the plugin's audio processing</item>
    /// <item>Querying plugin parameters and capabilities</item>
    /// </list>
    /// This async method prevents blocking during these operations.
    /// </remarks>
    /// <example>
    /// <code>
    /// var vstHost = new VstHost();
    /// await vstHost.ScanForPluginsAsync();
    ///
    /// using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    ///
    /// try
    /// {
    ///     var plugin = await vstHost.LoadPluginAsync("MySynth", cts.Token);
    ///     if (plugin != null)
    ///     {
    ///         Console.WriteLine($"Loaded: {plugin.Name}");
    ///         Console.WriteLine($"Parameters: {plugin.GetParameterCount()}");
    ///     }
    ///     else
    ///     {
    ///         Console.WriteLine("Plugin not found");
    ///     }
    /// }
    /// catch (OperationCanceledException)
    /// {
    ///     Console.WriteLine("Plugin loading timed out");
    /// }
    /// </code>
    /// </example>
    public static async Task<IVstPlugin?> LoadPluginAsync(
        this VstHost vstHost,
        string path,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(vstHost);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Plugin path cannot be null or empty.", nameof(path));
        }

        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            return vstHost.LoadPlugin(path);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously loads a VST plugin by index from the discovered list.
    /// </summary>
    /// <param name="vstHost">The VST host instance.</param>
    /// <param name="index">The index of the plugin in the discovered list.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task containing the loaded plugin, or null if loading failed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="vstHost"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is invalid.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    /// <example>
    /// <code>
    /// var vstHost = new VstHost();
    /// var plugins = await vstHost.ScanForPluginsAsync();
    ///
    /// // Load the first discovered plugin
    /// if (plugins.Count > 0)
    /// {
    ///     var plugin = await vstHost.LoadPluginByIndexAsync(0);
    ///     Console.WriteLine($"Loaded: {plugin?.Name}");
    /// }
    /// </code>
    /// </example>
    public static async Task<IVstPlugin?> LoadPluginByIndexAsync(
        this VstHost vstHost,
        int index,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(vstHost);

        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Index must be non-negative.");
        }

        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            return vstHost.LoadPluginByIndex(index);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously loads a VST3 plugin by index from the discovered list.
    /// </summary>
    /// <param name="vstHost">The VST host instance.</param>
    /// <param name="index">The index of the VST3 plugin in the discovered list.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task containing the loaded plugin, or null if loading failed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="vstHost"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is invalid.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    /// <example>
    /// <code>
    /// var vstHost = new VstHost();
    /// await vstHost.ScanForPluginsAsync();
    ///
    /// // Load the first discovered VST3 plugin
    /// if (vstHost.DiscoveredVst3Plugins.Count > 0)
    /// {
    ///     var plugin = await vstHost.LoadVst3PluginByIndexAsync(0);
    ///     Console.WriteLine($"Loaded VST3: {plugin?.Name}");
    /// }
    /// </code>
    /// </example>
    public static async Task<IVstPlugin?> LoadVst3PluginByIndexAsync(
        this VstHost vstHost,
        int index,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(vstHost);

        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Index must be non-negative.");
        }

        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            return vstHost.LoadVst3PluginByIndex(index);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously unloads a VST plugin.
    /// </summary>
    /// <param name="vstHost">The VST host instance.</param>
    /// <param name="pluginName">The name of the plugin to unload.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task that completes when the plugin is unloaded.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="vstHost"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="pluginName"/> is null or empty.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    /// <example>
    /// <code>
    /// var vstHost = new VstHost();
    /// var plugin = await vstHost.LoadPluginAsync("MySynth");
    ///
    /// // Use the plugin...
    ///
    /// // Unload when done
    /// await vstHost.UnloadPluginAsync("MySynth");
    /// </code>
    /// </example>
    public static async Task UnloadPluginAsync(
        this VstHost vstHost,
        string pluginName,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(vstHost);

        if (string.IsNullOrWhiteSpace(pluginName))
        {
            throw new ArgumentException("Plugin name cannot be null or empty.", nameof(pluginName));
        }

        await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            vstHost.UnloadPlugin(pluginName);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously loads a preset file into a plugin.
    /// </summary>
    /// <param name="vstHost">The VST host instance.</param>
    /// <param name="pluginName">The name of the loaded plugin.</param>
    /// <param name="presetPath">The path to the preset file.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task containing true if the preset was loaded successfully.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="vstHost"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    /// <example>
    /// <code>
    /// var vstHost = new VstHost();
    /// var plugin = await vstHost.LoadPluginAsync("MySynth");
    ///
    /// bool loaded = await vstHost.LoadPresetAsync("MySynth", @"C:\Presets\MyPreset.fxp");
    /// if (loaded)
    /// {
    ///     Console.WriteLine("Preset loaded successfully");
    /// }
    /// </code>
    /// </example>
    public static async Task<bool> LoadPresetAsync(
        this VstHost vstHost,
        string pluginName,
        string presetPath,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(vstHost);

        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            return vstHost.LoadPresetForPlugin(pluginName, presetPath);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously saves a plugin's current state to a preset file.
    /// </summary>
    /// <param name="vstHost">The VST host instance.</param>
    /// <param name="pluginName">The name of the loaded plugin.</param>
    /// <param name="presetPath">The path for the preset file.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task containing true if the preset was saved successfully.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="vstHost"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    /// <example>
    /// <code>
    /// var vstHost = new VstHost();
    /// var plugin = await vstHost.LoadPluginAsync("MySynth");
    ///
    /// // Configure the plugin...
    ///
    /// bool saved = await vstHost.SavePresetAsync("MySynth", @"C:\Presets\MyPreset.fxp");
    /// if (saved)
    /// {
    ///     Console.WriteLine("Preset saved successfully");
    /// }
    /// </code>
    /// </example>
    public static async Task<bool> SavePresetAsync(
        this VstHost vstHost,
        string pluginName,
        string presetPath,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(vstHost);

        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            return vstHost.SavePresetForPlugin(pluginName, presetPath);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Guesses if a plugin is an instrument based on its name.
    /// </summary>
    private static bool GuessIsInstrument(string name)
    {
        var lowerName = name.ToLowerInvariant();
        var instrumentKeywords = new[] { "synth", "piano", "organ", "bass", "lead", "pad", "strings", "brass", "drum", "sampler", "rompler", "keys", "vsti" };
        foreach (var keyword in instrumentKeywords)
        {
            if (lowerName.Contains(keyword)) return true;
        }
        return false;
    }
}
