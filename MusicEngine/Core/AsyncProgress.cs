//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Progress reporting classes for async operations.


using System;


namespace MusicEngine.Core;


/// <summary>
/// Represents the progress of an initialization operation.
/// Used with <see cref="IProgress{T}"/> to report initialization status.
/// </summary>
/// <remarks>
/// This class extends the basic progress reporting with additional properties
/// for detailed initialization tracking. For event-based progress reporting,
/// see <see cref="Events.InitializationProgressEventArgs"/>.
/// </remarks>
/// <example>
/// <code>
/// progress.Report(new InitializationProgress
/// {
///     Stage = "MIDI Devices",
///     Percentage = 50.0,
///     Message = "Enumerating MIDI inputs..."
/// });
/// </code>
/// </example>
public sealed class InitializationProgress
{
    /// <summary>
    /// Gets or sets the current initialization stage name.
    /// </summary>
    /// <remarks>
    /// Common stages include: "Audio Output", "Audio Devices", "MIDI Devices", "VST Plugins", "Complete".
    /// </remarks>
    public string Stage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the overall initialization progress percentage (0-100).
    /// </summary>
    /// <value>A value between 0 and 100 representing the completion percentage.</value>
    public double Percentage { get; set; }

    /// <summary>
    /// Gets or sets an optional descriptive message about the current operation.
    /// </summary>
    /// <value>A human-readable message describing what is currently being initialized, or null.</value>
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets the current step number within the initialization process.
    /// </summary>
    public int CurrentStep { get; set; }

    /// <summary>
    /// Gets or sets the total number of steps in the initialization process.
    /// </summary>
    public int TotalSteps { get; set; }

    /// <summary>
    /// Gets whether the initialization is complete.
    /// </summary>
    public bool IsComplete => Stage == "Complete" || (TotalSteps > 0 && CurrentStep >= TotalSteps);

    /// <summary>
    /// Creates a new instance of <see cref="InitializationProgress"/> with default values.
    /// </summary>
    public InitializationProgress()
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="InitializationProgress"/> with the specified values.
    /// </summary>
    /// <param name="stage">The current initialization stage.</param>
    /// <param name="percentage">The progress percentage (0-100).</param>
    /// <param name="message">An optional descriptive message.</param>
    public InitializationProgress(string stage, double percentage, string? message = null)
    {
        Stage = stage ?? string.Empty;
        Percentage = Math.Clamp(percentage, 0.0, 100.0);
        Message = message;
    }

    /// <summary>
    /// Creates a new instance of <see cref="InitializationProgress"/> with step-based progress.
    /// </summary>
    /// <param name="stage">The current initialization stage.</param>
    /// <param name="currentStep">The current step number.</param>
    /// <param name="totalSteps">The total number of steps.</param>
    /// <param name="message">An optional descriptive message.</param>
    public InitializationProgress(string stage, int currentStep, int totalSteps, string? message = null)
    {
        Stage = stage ?? string.Empty;
        CurrentStep = currentStep;
        TotalSteps = totalSteps;
        Percentage = totalSteps > 0 ? (currentStep * 100.0 / totalSteps) : 0.0;
        Message = message;
    }

    /// <summary>
    /// Returns a string representation of the progress.
    /// </summary>
    /// <returns>A string containing the stage, percentage, and message.</returns>
    public override string ToString()
    {
        var result = $"[{Stage}] {Percentage:F1}%";
        if (!string.IsNullOrEmpty(Message))
        {
            result += $" - {Message}";
        }
        return result;
    }
}


/// <summary>
/// Represents the progress of a VST plugin scanning operation.
/// Used with <see cref="IProgress{T}"/> to report VST scan status.
/// </summary>
/// <remarks>
/// This class provides detailed progress information for VST scanning operations,
/// including validation status and error reporting. For event-based progress reporting,
/// see <see cref="Events.VstScanProgressEventArgs"/>.
/// </remarks>
public sealed class VstScanProgress
{
    /// <summary>
    /// Gets or sets the index of the currently scanned plugin (1-based).
    /// </summary>
    public int CurrentPlugin { get; set; }

    /// <summary>
    /// Gets or sets the total number of plugins to scan.
    /// </summary>
    public int TotalPlugins { get; set; }

    /// <summary>
    /// Gets or sets the name or path of the currently scanned plugin.
    /// </summary>
    public string PluginName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the current plugin is valid/loadable.
    /// </summary>
    /// <value>
    /// <c>true</c> if the plugin was successfully probed and appears valid;
    /// <c>false</c> if the plugin failed validation or could not be loaded;
    /// <c>null</c> if validation has not yet been performed.
    /// </value>
    public bool? IsValid { get; set; }

    /// <summary>
    /// Gets or sets the current scan path being searched.
    /// </summary>
    public string? CurrentPath { get; set; }

    /// <summary>
    /// Gets or sets an optional error message if the plugin failed to load.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets whether the scanned plugin is a VST3 plugin.
    /// </summary>
    public bool IsVst3 { get; set; }

    /// <summary>
    /// Gets the progress percentage (0-100).
    /// </summary>
    public double Percentage => TotalPlugins > 0 ? (CurrentPlugin * 100.0 / TotalPlugins) : 0.0;

    /// <summary>
    /// Gets whether the scan is complete.
    /// </summary>
    public bool IsComplete => TotalPlugins > 0 && CurrentPlugin >= TotalPlugins;

    /// <summary>
    /// Creates a new instance of <see cref="VstScanProgress"/> with default values.
    /// </summary>
    public VstScanProgress()
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="VstScanProgress"/> with the specified values.
    /// </summary>
    /// <param name="currentPlugin">The index of the current plugin being scanned.</param>
    /// <param name="totalPlugins">The total number of plugins to scan.</param>
    /// <param name="pluginName">The name or path of the current plugin.</param>
    /// <param name="isValid">Whether the current plugin is valid.</param>
    public VstScanProgress(int currentPlugin, int totalPlugins, string pluginName, bool? isValid = null)
    {
        CurrentPlugin = currentPlugin;
        TotalPlugins = totalPlugins;
        PluginName = pluginName ?? string.Empty;
        IsValid = isValid;
    }

    /// <summary>
    /// Creates a progress instance indicating scan start for a specific path.
    /// </summary>
    /// <param name="path">The path being scanned.</param>
    /// <returns>A new <see cref="VstScanProgress"/> instance.</returns>
    public static VstScanProgress StartingPath(string path)
    {
        return new VstScanProgress
        {
            CurrentPath = path,
            PluginName = $"Scanning: {path}"
        };
    }

    /// <summary>
    /// Creates a progress instance indicating scan completion.
    /// </summary>
    /// <param name="totalFound">The total number of valid plugins found.</param>
    /// <returns>A new <see cref="VstScanProgress"/> instance.</returns>
    public static VstScanProgress Complete(int totalFound)
    {
        return new VstScanProgress
        {
            CurrentPlugin = totalFound,
            TotalPlugins = totalFound,
            PluginName = "Scan complete",
            IsValid = true
        };
    }

    /// <summary>
    /// Returns a string representation of the scan progress.
    /// </summary>
    /// <returns>A string containing plugin progress information.</returns>
    public override string ToString()
    {
        var validStatus = IsValid.HasValue ? (IsValid.Value ? " [Valid]" : " [Invalid]") : "";
        return $"[{CurrentPlugin}/{TotalPlugins}] {PluginName}{validStatus}";
    }
}


/// <summary>
/// Represents the progress of a session load/save operation.
/// Used with <see cref="IProgress{T}"/> to report session I/O status.
/// </summary>
public sealed class SessionProgress
{
    /// <summary>
    /// Gets or sets the current operation stage.
    /// </summary>
    /// <remarks>
    /// Common stages for loading: "Reading File", "Parsing JSON", "Validating", "Applying", "Complete".
    /// Common stages for saving: "Preparing", "Serializing", "Writing File", "Complete".
    /// </remarks>
    public string Stage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the progress percentage (0-100).
    /// </summary>
    public double Percentage { get; set; }

    /// <summary>
    /// Gets or sets an optional descriptive message.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets the number of bytes processed (for file I/O operations).
    /// </summary>
    public long BytesProcessed { get; set; }

    /// <summary>
    /// Gets or sets the total number of bytes (for file I/O operations).
    /// </summary>
    public long TotalBytes { get; set; }

    /// <summary>
    /// Gets whether the operation is complete.
    /// </summary>
    public bool IsComplete => Stage == "Complete" || Percentage >= 100.0;

    /// <summary>
    /// Creates a new instance of <see cref="SessionProgress"/> with default values.
    /// </summary>
    public SessionProgress()
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="SessionProgress"/> with the specified values.
    /// </summary>
    /// <param name="stage">The current operation stage.</param>
    /// <param name="percentage">The progress percentage.</param>
    /// <param name="message">An optional descriptive message.</param>
    public SessionProgress(string stage, double percentage, string? message = null)
    {
        Stage = stage ?? string.Empty;
        Percentage = Math.Clamp(percentage, 0.0, 100.0);
        Message = message;
    }

    /// <summary>
    /// Creates a progress instance for a completed operation.
    /// </summary>
    /// <param name="message">An optional completion message.</param>
    /// <returns>A new <see cref="SessionProgress"/> instance.</returns>
    public static SessionProgress Complete(string? message = null)
    {
        return new SessionProgress("Complete", 100.0, message ?? "Operation completed successfully");
    }

    /// <summary>
    /// Returns a string representation of the progress.
    /// </summary>
    /// <returns>A string containing the stage, percentage, and message.</returns>
    public override string ToString()
    {
        var result = $"[{Stage}] {Percentage:F1}%";
        if (!string.IsNullOrEmpty(Message))
        {
            result += $" - {Message}";
        }
        return result;
    }
}
