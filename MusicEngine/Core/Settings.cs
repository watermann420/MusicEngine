// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Global audio engine settings.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using MusicEngine.Infrastructure.Configuration;


namespace MusicEngine.Core;


public static class Settings
{
    // Environment variable name for VST paths configuration
    private const string VstPathsEnvVar = "MUSICENGINE_VST_PATHS";

    // Default VST plugin search paths (used as fallback)
    private static readonly List<string> DefaultVstPluginSearchPaths = new()
    {
        @"C:\Program Files\VSTPlugins",
        @"C:\Program Files\Common Files\VST3",
        @"C:\Program Files\Steinberg\VSTPlugins",
        @"C:\Program Files (x86)\VSTPlugins",
        @"C:\Program Files (x86)\Common Files\VST3"
    };

    // Lock object for thread-safe VST path operations
    private static readonly object VstPathsLock = new();

    // Audio Settings
    public static int SampleRate { get; set; } = 44100; // Standard CD quality (44.1 kHz)
    public static int BitRate { get; set; } = 32; // Typically for 16-bit, though we use float internally
    public static int Channels { get; set; } = 2; // Stereo by default and can be changed to mono if needed


    // MIDI and Analysis Settings
    public static int MidiRefreshRateMs { get; set; } = 1; // MIDI device refresh rate EVERY millisecond
    public static int MidiCaptRefreshRateInMs { get; set; } = 10; // MIDI capture refresh rate in milliseconds
    public static int MidiCaptRefreshRateOutMs { get; set; } = 10; // MIDI output refresh rate in milliseconds
    public static int MidiBufferSize { get; set; } = 1024; // MIDI buffer size for processing
    public static int MidiCaptRefreshRate { get; set; } = 5; // General MIDI capture refresh rate in milliseconds

    // VST Plugin Settings
    public static string VstPluginPath { get; set; } = ""; // Default VST plugin search path
    private static List<string>? _vstPluginSearchPaths;

    /// <summary>
    /// Gets or sets the VST plugin search paths.
    /// On first access, paths are loaded from environment variable (MUSICENGINE_VST_PATHS)
    /// or fall back to default hardcoded paths.
    /// </summary>
    public static List<string> VstPluginSearchPaths
    {
        get
        {
            lock (VstPathsLock)
            {
                if (_vstPluginSearchPaths == null)
                {
                    _vstPluginSearchPaths = LoadVstPaths();
                }
                return _vstPluginSearchPaths;
            }
        }
        set
        {
            lock (VstPathsLock)
            {
                _vstPluginSearchPaths = value ?? new List<string>();
            }
        }
    }

    public static int VstBufferSize { get; set; } = 512; // VST processing buffer size
    public static int VstProcessingTimeout { get; set; } = 100; // VST processing timeout in milliseconds

    // FFT Settings // For frequency analysis
    public static int FftSize { get; set; } = 1024; // FFT size for frequency analysis

    /// <summary>
    /// Loads VST plugin paths from environment variable or returns default paths.
    /// The environment variable MUSICENGINE_VST_PATHS should contain semicolon-separated paths.
    /// </summary>
    /// <returns>List of VST plugin search paths</returns>
    private static List<string> LoadVstPaths()
    {
        var envPaths = Environment.GetEnvironmentVariable(VstPathsEnvVar);

        if (!string.IsNullOrWhiteSpace(envPaths))
        {
            var paths = envPaths.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (paths.Count > 0)
            {
                return paths;
            }
        }

        // Return a copy of defaults to prevent external modification of the defaults
        return new List<string>(DefaultVstPluginSearchPaths);
    }

    /// <summary>
    /// Adds a VST plugin search path at runtime.
    /// The path is validated to ensure the directory exists before adding.
    /// </summary>
    /// <param name="path">The directory path to add</param>
    /// <returns>True if the path was added successfully, false if it doesn't exist or is already in the list</returns>
    public static bool AddVstPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var normalizedPath = path.Trim();

        // Validate that the directory exists
        if (!Directory.Exists(normalizedPath))
        {
            return false;
        }

        lock (VstPathsLock)
        {
            // Ensure paths are loaded
            if (_vstPluginSearchPaths == null)
            {
                _vstPluginSearchPaths = LoadVstPaths();
            }

            // Check if path already exists (case-insensitive comparison for Windows)
            if (_vstPluginSearchPaths.Any(p => string.Equals(p, normalizedPath, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            _vstPluginSearchPaths.Add(normalizedPath);
            return true;
        }
    }

    /// <summary>
    /// Removes a VST plugin search path at runtime.
    /// </summary>
    /// <param name="path">The directory path to remove</param>
    /// <returns>True if the path was removed successfully, false if it was not found</returns>
    public static bool RemoveVstPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var normalizedPath = path.Trim();

        lock (VstPathsLock)
        {
            // Ensure paths are loaded
            if (_vstPluginSearchPaths == null)
            {
                _vstPluginSearchPaths = LoadVstPaths();
            }

            // Find and remove the path (case-insensitive comparison for Windows)
            var pathToRemove = _vstPluginSearchPaths
                .FirstOrDefault(p => string.Equals(p, normalizedPath, StringComparison.OrdinalIgnoreCase));

            if (pathToRemove != null)
            {
                return _vstPluginSearchPaths.Remove(pathToRemove);
            }

            return false;
        }
    }

    /// <summary>
    /// Reloads VST paths from environment variable, discarding any runtime modifications.
    /// </summary>
    public static void ReloadVstPaths()
    {
        lock (VstPathsLock)
        {
            _vstPluginSearchPaths = LoadVstPaths();
        }
    }

    /// <summary>
    /// Gets a copy of the default VST plugin search paths.
    /// </summary>
    /// <returns>A new list containing the default paths</returns>
    public static List<string> GetDefaultVstPaths()
    {
        return new List<string>(DefaultVstPluginSearchPaths);
    }

    /// <summary>
    /// Resets VST paths to the default hardcoded values.
    /// </summary>
    public static void ResetVstPathsToDefaults()
    {
        lock (VstPathsLock)
        {
            _vstPluginSearchPaths = new List<string>(DefaultVstPluginSearchPaths);
        }
    }

    /// <summary>
    /// Initializes settings from MusicEngineOptions configuration.
    /// </summary>
    /// <param name="options">The configuration options to apply.</param>
    public static void Initialize(MusicEngineOptions options)
    {
        if (options == null)
            return;

        // Audio settings
        if (options.Audio != null)
        {
            SampleRate = options.Audio.SampleRate;
            BitRate = options.Audio.BitDepth;
            Channels = options.Audio.Channels;
        }

        // MIDI settings
        if (options.Midi != null)
        {
            MidiRefreshRateMs = options.Midi.RefreshRateMs;
            MidiBufferSize = options.Midi.BufferSize;
        }

        // VST settings
        if (options.Vst != null)
        {
            VstBufferSize = options.Vst.BufferSize;
            VstProcessingTimeout = options.Vst.ProcessingTimeout;

            if (options.Vst.SearchPaths != null && options.Vst.SearchPaths.Count > 0)
            {
                lock (VstPathsLock)
                {
                    _vstPluginSearchPaths = new List<string>(options.Vst.SearchPaths);
                }
            }
        }
    }

    /// <summary>
    /// Initializes settings from an IConfiguration instance.
    /// </summary>
    /// <param name="configuration">The configuration to read from.</param>
    public static void Initialize(IConfiguration configuration)
    {
        if (configuration == null)
            return;

        var options = new MusicEngineOptions();
        configuration.GetSection(MusicEngineOptions.SectionName).Bind(options);
        Initialize(options);
    }
}
