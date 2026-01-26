// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

namespace MusicEngine.Core.Progress;


/// <summary>
/// Represents the progress of an initialization operation using immutable record semantics.
/// </summary>
/// <param name="Stage">The current initialization stage (e.g., "Audio Output", "MIDI Devices", "VST Plugins").</param>
/// <param name="CurrentStep">The current step number in the initialization process.</param>
/// <param name="TotalSteps">The total number of steps in the initialization process.</param>
/// <param name="Message">An optional descriptive message about the current operation.</param>
/// <example>
/// <code>
/// var progress = new InitializationProgress("MIDI Devices", 2, 5, "Enumerating MIDI inputs...");
/// Console.WriteLine($"{progress.Stage}: {progress.PercentComplete:F1}%");
/// </code>
/// </example>
public record InitializationProgress(
    string Stage,
    int CurrentStep,
    int TotalSteps,
    string? Message = null)
{
    /// <summary>
    /// Gets the completion percentage (0-100) based on current and total steps.
    /// </summary>
    public double PercentComplete => TotalSteps > 0 ? (double)CurrentStep / TotalSteps * 100 : 0;

    /// <summary>
    /// Gets whether the initialization is complete.
    /// </summary>
    public bool IsComplete => TotalSteps > 0 && CurrentStep >= TotalSteps;

    /// <summary>
    /// Creates a progress instance indicating completion.
    /// </summary>
    /// <param name="totalSteps">The total number of steps completed.</param>
    /// <param name="message">An optional completion message.</param>
    /// <returns>A new <see cref="InitializationProgress"/> instance representing completion.</returns>
    public static InitializationProgress Complete(int totalSteps, string? message = null)
        => new("Complete", totalSteps, totalSteps, message ?? "Initialization complete");

    /// <summary>
    /// Returns a string representation of the progress.
    /// </summary>
    public override string ToString()
    {
        var result = $"[{Stage}] {PercentComplete:F1}% ({CurrentStep}/{TotalSteps})";
        if (!string.IsNullOrEmpty(Message))
        {
            result += $" - {Message}";
        }
        return result;
    }
}


/// <summary>
/// Represents the progress of a VST plugin scanning operation using immutable record semantics.
/// </summary>
/// <param name="CurrentPlugin">The name or path of the currently scanned plugin.</param>
/// <param name="ScannedCount">The number of plugins scanned so far.</param>
/// <param name="TotalCount">The total number of plugins to scan.</param>
/// <param name="IsValid">Whether the current plugin is valid/loadable.</param>
/// <param name="ErrorMessage">An optional error message if the plugin failed validation.</param>
/// <example>
/// <code>
/// var progress = new VstScanProgress("MySynth.dll", 5, 20, true);
/// Console.WriteLine($"[{progress.PercentComplete:F0}%] {progress.CurrentPlugin}");
/// </code>
/// </example>
public record VstScanProgress(
    string CurrentPlugin,
    int ScannedCount,
    int TotalCount,
    bool IsValid,
    string? ErrorMessage = null)
{
    /// <summary>
    /// Gets the completion percentage (0-100) based on scanned and total count.
    /// </summary>
    public double PercentComplete => TotalCount > 0 ? (double)ScannedCount / TotalCount * 100 : 0;

    /// <summary>
    /// Gets whether the scan is complete.
    /// </summary>
    public bool IsComplete => TotalCount > 0 && ScannedCount >= TotalCount;

    /// <summary>
    /// Gets or sets whether the scanned plugin is a VST3 plugin.
    /// </summary>
    public bool IsVst3 { get; init; }

    /// <summary>
    /// Gets or sets the current search path being scanned.
    /// </summary>
    public string? CurrentPath { get; init; }

    /// <summary>
    /// Creates a progress instance indicating scan start.
    /// </summary>
    /// <param name="totalCount">The total number of plugins to scan.</param>
    /// <returns>A new <see cref="VstScanProgress"/> instance.</returns>
    public static VstScanProgress Starting(int totalCount)
        => new("Starting scan...", 0, totalCount, true);

    /// <summary>
    /// Creates a progress instance indicating scan completion.
    /// </summary>
    /// <param name="totalFound">The total number of valid plugins found.</param>
    /// <returns>A new <see cref="VstScanProgress"/> instance.</returns>
    public static VstScanProgress Complete(int totalFound)
        => new("Scan complete", totalFound, totalFound, true);

    /// <summary>
    /// Returns a string representation of the scan progress.
    /// </summary>
    public override string ToString()
    {
        var validStatus = IsValid ? "[Valid]" : "[Invalid]";
        var result = $"[{ScannedCount}/{TotalCount}] {CurrentPlugin} {validStatus}";
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            result += $" - {ErrorMessage}";
        }
        return result;
    }
}


/// <summary>
/// Represents the progress of a session load/save operation using immutable record semantics.
/// </summary>
/// <param name="Stage">The current operation stage (e.g., "Reading File", "Parsing JSON", "Validating").</param>
/// <param name="CurrentItem">The current item number being processed.</param>
/// <param name="TotalItems">The total number of items to process.</param>
/// <param name="CurrentFile">The path of the current file being processed.</param>
/// <example>
/// <code>
/// var progress = new SessionLoadProgress("Loading Patterns", 3, 10, "pattern_drums.json");
/// Console.WriteLine($"{progress.Stage}: {progress.CurrentItem}/{progress.TotalItems}");
/// </code>
/// </example>
public record SessionLoadProgress(
    string Stage,
    int CurrentItem,
    int TotalItems,
    string? CurrentFile = null)
{
    /// <summary>
    /// Gets the completion percentage (0-100) based on current and total items.
    /// </summary>
    public double PercentComplete => TotalItems > 0 ? (double)CurrentItem / TotalItems * 100 : 0;

    /// <summary>
    /// Gets whether the operation is complete.
    /// </summary>
    public bool IsComplete => TotalItems > 0 && CurrentItem >= TotalItems;

    /// <summary>
    /// Creates a progress instance for file reading stage.
    /// </summary>
    /// <param name="filePath">The path of the file being read.</param>
    /// <returns>A new <see cref="SessionLoadProgress"/> instance.</returns>
    public static SessionLoadProgress ReadingFile(string filePath)
        => new("Reading File", 0, 1, filePath);

    /// <summary>
    /// Creates a progress instance for parsing stage.
    /// </summary>
    /// <returns>A new <see cref="SessionLoadProgress"/> instance.</returns>
    public static SessionLoadProgress Parsing()
        => new("Parsing JSON", 0, 1);

    /// <summary>
    /// Creates a progress instance for validation stage.
    /// </summary>
    /// <returns>A new <see cref="SessionLoadProgress"/> instance.</returns>
    public static SessionLoadProgress Validating()
        => new("Validating", 0, 1);

    /// <summary>
    /// Creates a progress instance indicating completion.
    /// </summary>
    /// <param name="totalItems">The total number of items processed.</param>
    /// <returns>A new <see cref="SessionLoadProgress"/> instance.</returns>
    public static SessionLoadProgress Complete(int totalItems)
        => new("Complete", totalItems, totalItems);

    /// <summary>
    /// Returns a string representation of the progress.
    /// </summary>
    public override string ToString()
    {
        var result = $"[{Stage}] {PercentComplete:F1}% ({CurrentItem}/{TotalItems})";
        if (!string.IsNullOrEmpty(CurrentFile))
        {
            result += $" - {CurrentFile}";
        }
        return result;
    }
}
