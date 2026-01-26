// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace MusicEngine.Core.Extensions;

/// <summary>
/// Context provided to extensions during initialization and runtime.
/// </summary>
public interface IExtensionContext
{
    /// <summary>
    /// Gets the audio engine instance.
    /// </summary>
    AudioEngine Engine { get; }

    /// <summary>
    /// Gets the sample rate of the audio engine.
    /// </summary>
    int SampleRate { get; }

    /// <summary>
    /// Gets the number of audio channels.
    /// </summary>
    int Channels { get; }

    /// <summary>
    /// Gets the extension manager.
    /// </summary>
    ExtensionManager Extensions { get; }

    /// <summary>
    /// Gets the path to the extension's data directory.
    /// Extensions can store persistent data here.
    /// </summary>
    string DataDirectory { get; }

    /// <summary>
    /// Gets the logger for extension logging.
    /// </summary>
    ILogger? Logger { get; }
}

/// <summary>
/// Default implementation of the extension context.
/// </summary>
public class ExtensionContext : IExtensionContext
{
    /// <inheritdoc/>
    public AudioEngine Engine { get; }

    /// <inheritdoc/>
    public int SampleRate => Settings.SampleRate;

    /// <inheritdoc/>
    public int Channels => Settings.Channels;

    /// <inheritdoc/>
    public ExtensionManager Extensions { get; }

    /// <inheritdoc/>
    public string DataDirectory { get; }

    /// <inheritdoc/>
    public ILogger? Logger { get; }

    /// <summary>
    /// Creates a new extension context.
    /// </summary>
    public ExtensionContext(AudioEngine engine, ExtensionManager extensions, string? dataDirectory = null, ILogger? logger = null)
    {
        Engine = engine ?? throw new ArgumentNullException(nameof(engine));
        Extensions = extensions ?? throw new ArgumentNullException(nameof(extensions));
        DataDirectory = dataDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MusicEngine", "Extensions");
        Logger = logger;

        // Ensure data directory exists
        if (!Directory.Exists(DataDirectory))
        {
            Directory.CreateDirectory(DataDirectory);
        }
    }
}
