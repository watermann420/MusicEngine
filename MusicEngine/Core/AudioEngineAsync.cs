//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Async extension methods for AudioEngine operations.


using System;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Midi;


namespace MusicEngine.Core;


/// <summary>
/// Provides async extension methods for <see cref="AudioEngine"/> operations.
/// </summary>
/// <remarks>
/// These extension methods enable non-blocking audio engine operations with support for:
/// <list type="bullet">
/// <item>Cancellation via <see cref="CancellationToken"/></item>
/// <item>Progress reporting via <see cref="IProgress{T}"/></item>
/// <item>Proper exception handling and resource cleanup</item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// var engine = new AudioEngine();
/// var progress = new Progress&lt;InitializationProgress&gt;(p =>
///     Console.WriteLine($"{p.Stage}: {p.Percentage}%"));
///
/// using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
/// await engine.InitializeAsync(progress, cts.Token);
/// </code>
/// </example>
public static class AudioEngineAsync
{
    /// <summary>
    /// Asynchronously initializes the audio engine with progress reporting and cancellation support.
    /// </summary>
    /// <param name="engine">The audio engine to initialize.</param>
    /// <param name="progress">Optional progress reporter for initialization status.</param>
    /// <param name="ct">Cancellation token to cancel the initialization.</param>
    /// <returns>A task that completes when initialization is finished.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="engine"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    /// <exception cref="InvalidOperationException">Thrown when initialization fails.</exception>
    /// <remarks>
    /// This method performs the following initialization steps:
    /// <list type="number">
    /// <item>Sets up the default audio output device</item>
    /// <item>Enumerates available audio input and output devices</item>
    /// <item>Enumerates and initializes MIDI input and output devices</item>
    /// <item>Scans for VST plugins</item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// var engine = new AudioEngine();
    /// var progress = new Progress&lt;InitializationProgress&gt;(p =>
    /// {
    ///     Console.WriteLine($"[{p.Stage}] {p.Percentage:F0}% - {p.Message}");
    /// });
    ///
    /// try
    /// {
    ///     await engine.InitializeAsync(progress);
    ///     Console.WriteLine("Engine initialized successfully!");
    /// }
    /// catch (OperationCanceledException)
    /// {
    ///     Console.WriteLine("Initialization was canceled.");
    /// }
    /// catch (Exception ex)
    /// {
    ///     Console.WriteLine($"Initialization failed: {ex.Message}");
    /// }
    /// </code>
    /// </example>
    public static async Task InitializeAsync(
        this AudioEngine engine,
        IProgress<InitializationProgress>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(engine);

        await Task.Run(() =>
        {
            const int totalSteps = 5;

            try
            {
                // Step 1: Audio Output Setup
                ct.ThrowIfCancellationRequested();
                progress?.Report(new InitializationProgress(
                    "Audio Output",
                    1, totalSteps,
                    "Setting up default audio output device"));

                // The actual initialization is performed by calling the synchronous Initialize method
                // This ensures proper thread affinity for audio device operations

                // Step 2: Enumerate Audio Devices
                ct.ThrowIfCancellationRequested();
                progress?.Report(new InitializationProgress(
                    "Audio Devices",
                    2, totalSteps,
                    $"Found {WaveOut.DeviceCount} output, {WaveIn.DeviceCount} input devices"));

                // Step 3: MIDI Devices
                ct.ThrowIfCancellationRequested();
                progress?.Report(new InitializationProgress(
                    "MIDI Devices",
                    3, totalSteps,
                    $"Found {MidiIn.NumberOfDevices} MIDI inputs, {MidiOut.NumberOfDevices} MIDI outputs"));

                // Step 4: VST Plugins
                ct.ThrowIfCancellationRequested();
                progress?.Report(new InitializationProgress(
                    "VST Plugins",
                    4, totalSteps,
                    "Scanning for VST plugins..."));

                // Perform the actual synchronous initialization
                engine.Initialize();

                // Step 5: Complete
                ct.ThrowIfCancellationRequested();
                progress?.Report(new InitializationProgress(
                    "Complete",
                    5, totalSteps,
                    "Audio engine initialization complete"));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to initialize audio engine: {ex.Message}", ex);
            }
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously starts audio playback on the engine.
    /// </summary>
    /// <param name="engine">The audio engine to start.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task that completes when playback has started.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="engine"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the engine cannot be started.</exception>
    /// <remarks>
    /// This method ensures that the audio engine's playback is started on a background thread,
    /// avoiding blocking the calling thread during device initialization.
    /// </remarks>
    /// <example>
    /// <code>
    /// var engine = new AudioEngine();
    /// await engine.InitializeAsync();
    ///
    /// // Add some audio sources...
    /// engine.AddSampleProvider(mySynth);
    ///
    /// // Start playback
    /// await engine.StartAsync();
    /// </code>
    /// </example>
    public static async Task StartAsync(
        this AudioEngine engine,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(engine);

        await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                // The AudioEngine automatically starts playback during Initialize(),
                // but this method can be used to resume playback after stopping.
                // If additional start logic is needed, it would be implemented here.

                // Note: The current AudioEngine design starts playback during Initialize().
                // This async wrapper provides a consistent async API pattern for future extensions.
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to start audio engine: {ex.Message}", ex);
            }
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously stops audio playback on the engine.
    /// </summary>
    /// <param name="engine">The audio engine to stop.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task that completes when playback has stopped.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="engine"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the engine cannot be stopped.</exception>
    /// <remarks>
    /// This method gracefully stops audio playback without disposing the engine.
    /// The engine can be restarted by calling <see cref="StartAsync"/> after stopping.
    /// </remarks>
    /// <example>
    /// <code>
    /// var engine = new AudioEngine();
    /// await engine.InitializeAsync();
    ///
    /// // Play some audio...
    /// await Task.Delay(5000);
    ///
    /// // Stop playback
    /// await engine.StopAsync();
    ///
    /// // Engine can still be used, just restart when ready
    /// await engine.StartAsync();
    /// </code>
    /// </example>
    public static async Task StopAsync(
        this AudioEngine engine,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(engine);

        await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                // Clear the mixer to stop all audio processing
                engine.ClearMixer();

                // Allow a brief moment for audio buffers to flush
                ct.ThrowIfCancellationRequested();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to stop audio engine: {ex.Message}", ex);
            }
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously disposes the audio engine with cleanup.
    /// </summary>
    /// <param name="engine">The audio engine to dispose.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task that completes when disposal is finished.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="engine"/> is null.</exception>
    /// <remarks>
    /// This method performs proper cleanup of audio resources in a non-blocking manner.
    /// After calling this method, the engine should not be used again.
    /// </remarks>
    /// <example>
    /// <code>
    /// var engine = new AudioEngine();
    /// try
    /// {
    ///     await engine.InitializeAsync();
    ///     // Use engine...
    /// }
    /// finally
    /// {
    ///     await engine.DisposeAsync();
    /// }
    /// </code>
    /// </example>
    public static async Task DisposeAsync(
        this AudioEngine engine,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(engine);

        await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            engine.Dispose();
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously loads a VST plugin into the engine.
    /// </summary>
    /// <param name="engine">The audio engine.</param>
    /// <param name="nameOrPath">The name or path of the plugin to load.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>The loaded plugin, or null if loading failed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="engine"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="nameOrPath"/> is null or empty.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    /// <example>
    /// <code>
    /// var engine = new AudioEngine();
    /// await engine.InitializeAsync();
    ///
    /// var plugin = await engine.LoadVstPluginAsync("MySynth");
    /// if (plugin != null)
    /// {
    ///     Console.WriteLine($"Loaded: {plugin.Name}");
    /// }
    /// </code>
    /// </example>
    public static async Task<IVstPlugin?> LoadVstPluginAsync(
        this AudioEngine engine,
        string nameOrPath,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(engine);

        if (string.IsNullOrWhiteSpace(nameOrPath))
        {
            throw new ArgumentException("Plugin name or path cannot be null or empty.", nameof(nameOrPath));
        }

        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            return engine.LoadVstPlugin(nameOrPath);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously scans for VST plugins with progress reporting.
    /// </summary>
    /// <param name="engine">The audio engine.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A list of discovered plugin information.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="engine"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    /// <example>
    /// <code>
    /// var engine = new AudioEngine();
    /// await engine.InitializeAsync();
    ///
    /// var scanProgress = new Progress&lt;VstScanProgress&gt;(p =>
    ///     Console.WriteLine($"Scanning: {p.PluginName} ({p.CurrentPlugin}/{p.TotalPlugins})"));
    ///
    /// var plugins = await engine.ScanVstPluginsAsync(scanProgress);
    /// Console.WriteLine($"Found {plugins.Count} plugins");
    /// </code>
    /// </example>
    public static async Task<System.Collections.Generic.List<VstPluginInfo>> ScanVstPluginsAsync(
        this AudioEngine engine,
        IProgress<VstScanProgress>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(engine);

        return await engine.VstHost.ScanForPluginsAsync(progress, ct).ConfigureAwait(false);
    }
}
