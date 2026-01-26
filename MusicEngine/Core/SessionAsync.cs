// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;


namespace MusicEngine.Core;


/// <summary>
/// Provides async extension methods for <see cref="EngineSession"/> operations.
/// </summary>
/// <remarks>
/// These extension methods enable non-blocking session I/O operations with support for:
/// <list type="bullet">
/// <item>Cancellation via <see cref="CancellationToken"/></item>
/// <item>Progress reporting via <see cref="IProgress{T}"/></item>
/// <item>Proper exception handling and validation</item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// var session = new EngineSession();
/// session.Data.BPM = 140f;
/// session.Data.Metadata.Name = "My Project";
///
/// var progress = new Progress&lt;SessionProgress&gt;(p =>
///     Console.WriteLine($"{p.Stage}: {p.Percentage}%"));
///
/// await session.SaveAsync(@"C:\Projects\myproject.mep", progress);
/// </code>
/// </example>
public static class SessionAsync
{
    private static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions ReadJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Asynchronously loads a session from a file with progress reporting and cancellation support.
    /// </summary>
    /// <param name="session">The session instance to load into.</param>
    /// <param name="path">The path to the session file.</param>
    /// <param name="progress">Optional progress reporter for load status.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task that completes when the session is loaded.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="session"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the session file does not exist.</exception>
    /// <exception cref="InvalidDataException">Thrown when the session file is invalid or corrupted.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    /// <remarks>
    /// This method performs the following steps:
    /// <list type="number">
    /// <item>Validates the file exists and is accessible</item>
    /// <item>Reads the file contents asynchronously</item>
    /// <item>Parses and deserializes the JSON data</item>
    /// <item>Validates the session data</item>
    /// <item>Updates the session state</item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// var session = new EngineSession();
    /// var progress = new Progress&lt;SessionProgress&gt;(p =>
    /// {
    ///     Console.WriteLine($"[{p.Stage}] {p.Percentage:F0}% - {p.Message}");
    /// });
    ///
    /// using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    ///
    /// try
    /// {
    ///     await session.LoadAsync(@"C:\Projects\myproject.mep", progress, cts.Token);
    ///     Console.WriteLine($"Loaded project: {session.Data.Metadata.Name}");
    ///     Console.WriteLine($"BPM: {session.Data.BPM}");
    /// }
    /// catch (FileNotFoundException)
    /// {
    ///     Console.WriteLine("Session file not found.");
    /// }
    /// catch (InvalidDataException ex)
    /// {
    ///     Console.WriteLine($"Invalid session file: {ex.Message}");
    /// }
    /// catch (OperationCanceledException)
    /// {
    ///     Console.WriteLine("Load operation was canceled.");
    /// }
    /// </code>
    /// </example>
    public static async Task LoadAsync(
        this EngineSession session,
        string path,
        IProgress<SessionProgress>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }

        // Step 1: Validate file exists
        ct.ThrowIfCancellationRequested();
        progress?.Report(new SessionProgress("Validating", 0, "Checking file exists..."));

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Session file not found.", path);
        }

        // Step 2: Read file
        ct.ThrowIfCancellationRequested();
        progress?.Report(new SessionProgress("Reading File", 20, "Reading session file..."));

        string json;
        try
        {
            json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            throw new InvalidDataException($"Failed to read session file: {ex.Message}", ex);
        }

        // Step 3: Parse JSON
        ct.ThrowIfCancellationRequested();
        progress?.Report(new SessionProgress("Parsing JSON", 50, "Deserializing session data..."));

        SessionData? data;
        try
        {
            data = JsonSerializer.Deserialize<SessionData>(json, ReadJsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Failed to parse session file: {ex.Message}", ex);
        }

        if (data == null)
        {
            throw new InvalidDataException("Session file contains no valid data.");
        }

        // Step 4: Validate
        ct.ThrowIfCancellationRequested();
        progress?.Report(new SessionProgress("Validating", 70, "Validating session data..."));

        // Update session with temporary data to validate
        var tempSession = new EngineSession { };

        // Use reflection or direct property setting to validate
        // Basic validation
        if (data.BPM <= 0 || data.BPM > 999)
        {
            throw new InvalidDataException($"Invalid BPM value: {data.BPM}");
        }

        if (data.SampleRate < 8000 || data.SampleRate > 192000)
        {
            throw new InvalidDataException($"Invalid sample rate: {data.SampleRate}");
        }

        // Step 5: Apply
        ct.ThrowIfCancellationRequested();
        progress?.Report(new SessionProgress("Applying", 90, "Applying session data..."));

        // Update the session's internal state
        // Note: We need to use the existing Load method or directly set properties
        // The Session class has a LoadAsync method that we enhance with progress
        await session.LoadAsync(path, ct).ConfigureAwait(false);

        // Step 6: Complete
        progress?.Report(SessionProgress.Complete($"Loaded: {data.Metadata?.Name ?? "Untitled"}"));
    }

    /// <summary>
    /// Asynchronously loads a session from a file with cancellation support.
    /// </summary>
    /// <param name="session">The session instance to load into.</param>
    /// <param name="path">The path to the session file.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task that completes when the session is loaded.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="session"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the session file does not exist.</exception>
    /// <exception cref="InvalidDataException">Thrown when the session file is invalid or corrupted.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    /// <example>
    /// <code>
    /// var session = new EngineSession();
    /// using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    ///
    /// await session.LoadAsync(@"C:\Projects\myproject.mep", cts.Token);
    /// Console.WriteLine($"Loaded: {session.Data.Metadata.Name}");
    /// </code>
    /// </example>
    public static async Task LoadAsync(
        this EngineSession session,
        string path,
        CancellationToken ct)
    {
        await session.LoadAsync(path, null, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously saves the session to a file with progress reporting and cancellation support.
    /// </summary>
    /// <param name="session">The session instance to save.</param>
    /// <param name="path">The path to save the session file.</param>
    /// <param name="progress">Optional progress reporter for save status.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task that completes when the session is saved.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="session"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or empty.</exception>
    /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    /// <remarks>
    /// This method performs the following steps:
    /// <list type="number">
    /// <item>Validates and prepares the session data</item>
    /// <item>Serializes the data to JSON</item>
    /// <item>Creates the target directory if necessary</item>
    /// <item>Writes the file atomically (via temporary file)</item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// var session = new EngineSession();
    /// session.Data.BPM = 140f;
    /// session.Data.Metadata.Name = "My EDM Track";
    /// session.Data.Metadata.Author = "DJ Producer";
    ///
    /// var progress = new Progress&lt;SessionProgress&gt;(p =>
    /// {
    ///     Console.WriteLine($"[{p.Stage}] {p.Percentage:F0}%");
    /// });
    ///
    /// try
    /// {
    ///     await session.SaveAsync(@"C:\Projects\edm_track.mep", progress);
    ///     Console.WriteLine("Session saved successfully!");
    /// }
    /// catch (IOException ex)
    /// {
    ///     Console.WriteLine($"Failed to save: {ex.Message}");
    /// }
    /// </code>
    /// </example>
    public static async Task SaveAsync(
        this EngineSession session,
        string path,
        IProgress<SessionProgress>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }

        // Step 1: Prepare
        ct.ThrowIfCancellationRequested();
        progress?.Report(new SessionProgress("Preparing", 0, "Preparing session data..."));

        // Update modification timestamp
        session.Data.Metadata.ModifiedDate = DateTime.Now;

        // Validate session before saving
        var validationErrors = session.Validate();
        if (validationErrors.Count > 0)
        {
            throw new InvalidDataException($"Session validation failed: {string.Join(", ", validationErrors)}");
        }

        // Step 2: Serialize
        ct.ThrowIfCancellationRequested();
        progress?.Report(new SessionProgress("Serializing", 30, "Converting to JSON..."));

        string json;
        try
        {
            json = JsonSerializer.Serialize(session.Data, DefaultJsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Failed to serialize session: {ex.Message}", ex);
        }

        // Step 3: Ensure directory exists
        ct.ThrowIfCancellationRequested();
        progress?.Report(new SessionProgress("Preparing", 50, "Creating directory..."));

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            try
            {
                Directory.CreateDirectory(directory);
            }
            catch (IOException ex)
            {
                throw new IOException($"Failed to create directory: {ex.Message}", ex);
            }
        }

        // Step 4: Write file atomically
        ct.ThrowIfCancellationRequested();
        progress?.Report(new SessionProgress("Writing File", 70, "Writing session file..."));

        // Write to a temporary file first, then rename for atomic operation
        var tempPath = path + ".tmp";

        try
        {
            await File.WriteAllTextAsync(tempPath, json, ct).ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();

            // Delete existing file if it exists
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            // Rename temp file to target
            File.Move(tempPath, path);
        }
        catch (IOException ex)
        {
            // Clean up temp file on failure
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* Ignore cleanup errors */ }
            }
            throw new IOException($"Failed to write session file: {ex.Message}", ex);
        }

        // Step 5: Complete
        progress?.Report(SessionProgress.Complete($"Saved: {session.Data.Metadata.Name}"));
    }

    /// <summary>
    /// Asynchronously saves the session to a file with cancellation support.
    /// </summary>
    /// <param name="session">The session instance to save.</param>
    /// <param name="path">The path to save the session file.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task that completes when the session is saved.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="session"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or empty.</exception>
    /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    /// <example>
    /// <code>
    /// var session = new EngineSession();
    /// session.Data.BPM = 128f;
    ///
    /// using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    /// await session.SaveAsync(@"C:\Projects\myproject.mep", cts.Token);
    /// </code>
    /// </example>
    public static async Task SaveAsync(
        this EngineSession session,
        string path,
        CancellationToken ct)
    {
        await session.SaveAsync(path, null, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously creates a backup of the current session.
    /// </summary>
    /// <param name="session">The session instance to backup.</param>
    /// <param name="backupPath">The path for the backup file (optional, auto-generated if not provided).</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task containing the path to the backup file.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="session"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the session has no file path and no backup path is provided.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    /// <example>
    /// <code>
    /// var session = new EngineSession();
    /// session.Data.Metadata.Name = "Important Project";
    ///
    /// // Create a backup before making changes
    /// string backupPath = await session.CreateBackupAsync();
    /// Console.WriteLine($"Backup created: {backupPath}");
    ///
    /// // Make risky changes...
    /// try
    /// {
    ///     // ... potentially destructive operations ...
    /// }
    /// catch
    /// {
    ///     // Restore from backup
    ///     await session.LoadAsync(backupPath);
    /// }
    /// </code>
    /// </example>
    public static async Task<string> CreateBackupAsync(
        this EngineSession session,
        string? backupPath = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrEmpty(backupPath))
        {
            if (string.IsNullOrEmpty(session.FilePath))
            {
                throw new InvalidOperationException("Session has no file path. Please provide a backup path.");
            }

            var directory = Path.GetDirectoryName(session.FilePath);
            var fileName = Path.GetFileNameWithoutExtension(session.FilePath);
            var extension = Path.GetExtension(session.FilePath);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            backupPath = Path.Combine(
                directory ?? Environment.CurrentDirectory,
                $"{fileName}_backup_{timestamp}{extension}");
        }

        await session.SaveAsync(backupPath, ct).ConfigureAwait(false);
        return backupPath;
    }

    /// <summary>
    /// Asynchronously exports the session to a different format.
    /// </summary>
    /// <param name="session">The session instance to export.</param>
    /// <param name="path">The export file path.</param>
    /// <param name="format">The export format (currently only "json" is supported).</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task that completes when the export is finished.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="session"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or empty.</exception>
    /// <exception cref="NotSupportedException">Thrown when the export format is not supported.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    /// <example>
    /// <code>
    /// var session = new EngineSession();
    /// await session.LoadAsync(@"C:\Projects\myproject.mep");
    ///
    /// // Export to a different location/format
    /// await session.ExportAsync(@"C:\Exports\myproject.json", "json");
    /// </code>
    /// </example>
    public static async Task ExportAsync(
        this EngineSession session,
        string path,
        string format = "json",
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }

        switch (format.ToLowerInvariant())
        {
            case "json":
                await session.SaveAsync(path, ct).ConfigureAwait(false);
                break;
            default:
                throw new NotSupportedException($"Export format '{format}' is not supported.");
        }
    }

    /// <summary>
    /// Asynchronously gets basic info about a session file without fully loading it.
    /// </summary>
    /// <param name="path">The path to the session file.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task containing the session metadata, or null if the file cannot be read.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or empty.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    /// <example>
    /// <code>
    /// var files = Directory.GetFiles(@"C:\Projects", "*.mep");
    ///
    /// foreach (var file in files)
    /// {
    ///     var info = await SessionAsync.GetSessionInfoAsync(file);
    ///     if (info != null)
    ///     {
    ///         Console.WriteLine($"{info.Name} by {info.Author} - Modified: {info.ModifiedDate}");
    ///     }
    /// }
    /// </code>
    /// </example>
    public static async Task<SessionMetadata?> GetSessionInfoAsync(
        string path,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }

        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            ct.ThrowIfCancellationRequested();

            string json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("metadata", out var metadataElement))
            {
                return JsonSerializer.Deserialize<SessionMetadata>(
                    metadataElement.GetRawText(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            // Fallback for older format
            return new SessionMetadata
            {
                Name = root.TryGetProperty("name", out var name) ? name.GetString() ?? "Untitled" : "Untitled",
                ModifiedDate = File.GetLastWriteTime(path)
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Asynchronously validates a session file without loading it into the current session.
    /// </summary>
    /// <param name="path">The path to the session file to validate.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task containing a tuple of (isValid, errors) where errors is a list of validation messages.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or empty.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    /// <example>
    /// <code>
    /// var (isValid, errors) = await SessionAsync.ValidateSessionFileAsync(@"C:\Projects\myproject.mep");
    ///
    /// if (isValid)
    /// {
    ///     Console.WriteLine("Session file is valid!");
    /// }
    /// else
    /// {
    ///     Console.WriteLine("Validation errors:");
    ///     foreach (var error in errors)
    ///     {
    ///         Console.WriteLine($"  - {error}");
    ///     }
    /// }
    /// </code>
    /// </example>
    public static async Task<(bool IsValid, System.Collections.Generic.List<string> Errors)> ValidateSessionFileAsync(
        string path,
        CancellationToken ct = default)
    {
        var errors = new System.Collections.Generic.List<string>();

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }

        if (!File.Exists(path))
        {
            errors.Add("File does not exist.");
            return (false, errors);
        }

        try
        {
            ct.ThrowIfCancellationRequested();

            string json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();

            var data = JsonSerializer.Deserialize<SessionData>(json, ReadJsonOptions);
            if (data == null)
            {
                errors.Add("Failed to deserialize session data.");
                return (false, errors);
            }

            // Create a temporary session to use its Validate method
            var tempSession = new EngineSession();
            // Use reflection or copy data to validate
            // For now, perform basic validation directly

            if (data.BPM <= 0 || data.BPM > 999)
            {
                errors.Add($"Invalid BPM value: {data.BPM} (must be between 1 and 999)");
            }

            if (data.SampleRate < 8000 || data.SampleRate > 192000)
            {
                errors.Add($"Invalid sample rate: {data.SampleRate} (must be between 8000 and 192000)");
            }

            if (data.MasterVolume < 0 || data.MasterVolume > 2)
            {
                errors.Add($"Invalid master volume: {data.MasterVolume} (must be between 0 and 2)");
            }

            // Validate patterns
            foreach (var pattern in data.Patterns)
            {
                if (pattern.LoopLength <= 0)
                {
                    errors.Add($"Pattern '{pattern.Name}' has invalid loop length: {pattern.LoopLength}");
                }

                foreach (var note in pattern.Events)
                {
                    if (note.Note < 0 || note.Note > 127)
                    {
                        errors.Add($"Pattern '{pattern.Name}' contains invalid MIDI note: {note.Note}");
                    }

                    if (note.Velocity < 0 || note.Velocity > 127)
                    {
                        errors.Add($"Pattern '{pattern.Name}' contains invalid velocity: {note.Velocity}");
                    }
                }
            }

            return (errors.Count == 0, errors);
        }
        catch (JsonException ex)
        {
            errors.Add($"JSON parsing error: {ex.Message}");
            return (false, errors);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            errors.Add($"Unexpected error: {ex.Message}");
            return (false, errors);
        }
    }
}
