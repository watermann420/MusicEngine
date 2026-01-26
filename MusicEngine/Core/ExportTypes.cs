// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using System;

namespace MusicEngine.Core;

/// <summary>
/// Phases of the export process.
/// </summary>
public enum ExportPhase
{
    /// <summary>Starting export</summary>
    Starting,
    /// <summary>Analyzing source audio</summary>
    Analyzing,
    /// <summary>Applying loudness normalization</summary>
    Normalizing,
    /// <summary>Writing output file</summary>
    Writing,
    /// <summary>Converting format</summary>
    Converting,
    /// <summary>Verifying output</summary>
    Verifying,
    /// <summary>Export complete</summary>
    Complete,
    /// <summary>Export failed</summary>
    Failed
}

/// <summary>
/// Progress information during export.
/// </summary>
public class ExportProgress
{
    /// <summary>
    /// Current export phase.
    /// </summary>
    public ExportPhase Phase { get; set; }

    /// <summary>
    /// Progress value from 0.0 to 1.0.
    /// </summary>
    public double Progress { get; set; }

    /// <summary>
    /// Status message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Source loudness measurement (if available).
    /// </summary>
    public LoudnessMeasurement? SourceMeasurement { get; set; }

    public ExportProgress() { }

    public ExportProgress(ExportPhase phase, double progress, string message)
    {
        Phase = phase;
        Progress = progress;
        Message = message;
    }
}

/// <summary>
/// Result of an export operation.
/// </summary>
public class ExportResult
{
    /// <summary>
    /// Whether the export succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Path to the output file.
    /// </summary>
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>
    /// Error message if export failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Exception if export failed.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Loudness measurement of the source audio.
    /// </summary>
    public LoudnessMeasurement? SourceMeasurement { get; set; }

    /// <summary>
    /// Loudness measurement of the output audio.
    /// </summary>
    public LoudnessMeasurement? OutputMeasurement { get; set; }

    /// <summary>
    /// Duration of the export process.
    /// </summary>
    public TimeSpan ExportDuration { get; set; }

    /// <summary>
    /// Size of the output file in bytes.
    /// </summary>
    public long OutputFileSize { get; set; }

    /// <summary>
    /// Creates a successful export result.
    /// </summary>
    public static ExportResult Succeeded(string outputPath, LoudnessMeasurement? sourceMeasurement = null, LoudnessMeasurement? outputMeasurement = null)
    {
        return new ExportResult
        {
            Success = true,
            OutputPath = outputPath,
            SourceMeasurement = sourceMeasurement,
            OutputMeasurement = outputMeasurement
        };
    }

    /// <summary>
    /// Creates a failed export result.
    /// </summary>
    public static ExportResult Failed(string errorMessage, Exception? exception = null)
    {
        return new ExportResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            Exception = exception
        };
    }
}
