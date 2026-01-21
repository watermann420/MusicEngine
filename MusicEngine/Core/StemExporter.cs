//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Stem export functionality for exporting individual tracks/buses.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace MusicEngine.Core;

/// <summary>
/// Represents a stem (individual audio source) for export.
/// </summary>
/// <param name="Name">Display name of the stem.</param>
/// <param name="Source">The audio source to export.</param>
/// <param name="Enabled">Whether this stem should be exported.</param>
public record StemDefinition(string Name, ISampleProvider Source, bool Enabled = true)
{
    /// <summary>
    /// Creates a safe filename from the stem name.
    /// </summary>
    public string SafeFileName => string.Join("_", Name.Split(Path.GetInvalidFileNameChars()));
}

/// <summary>
/// Progress information for individual stem export.
/// </summary>
/// <param name="StemName">Name of the stem being exported.</param>
/// <param name="StemIndex">Index of the current stem (0-based).</param>
/// <param name="TotalStems">Total number of stems to export.</param>
/// <param name="StemProgress">Progress of current stem (0.0 to 1.0).</param>
/// <param name="OverallProgress">Overall export progress (0.0 to 1.0).</param>
/// <param name="Phase">Current export phase.</param>
/// <param name="Message">Status message.</param>
public record StemExportProgress(
    string StemName,
    int StemIndex,
    int TotalStems,
    double StemProgress,
    double OverallProgress,
    StemExportPhase Phase,
    string Message);

/// <summary>
/// Phases of stem export.
/// </summary>
public enum StemExportPhase
{
    /// <summary>Preparing export</summary>
    Preparing,
    /// <summary>Rendering stem audio</summary>
    Rendering,
    /// <summary>Applying loudness normalization</summary>
    Normalizing,
    /// <summary>Converting to target format</summary>
    Converting,
    /// <summary>Export complete</summary>
    Complete
}

/// <summary>
/// Result of an individual stem export.
/// </summary>
/// <param name="StemName">Name of the exported stem.</param>
/// <param name="OutputPath">Path to the exported file.</param>
/// <param name="Success">Whether the export succeeded.</param>
/// <param name="ErrorMessage">Error message if failed.</param>
/// <param name="Measurement">Loudness measurement of the output.</param>
public record StemExportItemResult(
    string StemName,
    string OutputPath,
    bool Success,
    string? ErrorMessage,
    LoudnessMeasurement? Measurement);

/// <summary>
/// Result of a complete stem export operation.
/// </summary>
/// <param name="Success">Whether all enabled stems were exported successfully.</param>
/// <param name="OutputDirectory">Directory containing exported stems.</param>
/// <param name="StemResults">Results for each stem.</param>
/// <param name="TotalDuration">Total export duration.</param>
public record StemExportResult(
    bool Success,
    string OutputDirectory,
    IReadOnlyList<StemExportItemResult> StemResults,
    TimeSpan TotalDuration)
{
    /// <summary>
    /// Gets the count of successfully exported stems.
    /// </summary>
    public int SuccessCount => StemResults.Count(r => r.Success);

    /// <summary>
    /// Gets the count of failed stems.
    /// </summary>
    public int FailedCount => StemResults.Count(r => !r.Success);

    /// <summary>
    /// Gets a summary of the export result.
    /// </summary>
    public string Summary
    {
        get
        {
            if (Success)
            {
                return $"Exported {SuccessCount} stems successfully in {TotalDuration.TotalSeconds:F1}s";
            }
            return $"Export completed with {FailedCount} failures ({SuccessCount} succeeded)";
        }
    }
}

/// <summary>
/// Exports individual audio stems (tracks/buses) to separate files.
/// Supports loudness normalization and multiple output formats.
/// </summary>
public class StemExporter
{
    private readonly AudioRecorder _recorder;

    /// <summary>
    /// Creates a new stem exporter.
    /// </summary>
    public StemExporter()
    {
        _recorder = new AudioRecorder();
    }

    /// <summary>
    /// Creates a new stem exporter with a shared audio recorder.
    /// </summary>
    /// <param name="recorder">The audio recorder to use.</param>
    public StemExporter(AudioRecorder recorder)
    {
        _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
    }

    /// <summary>
    /// Exports multiple stems to individual files.
    /// </summary>
    /// <param name="stems">The stems to export.</param>
    /// <param name="outputDir">Output directory for the exported files.</param>
    /// <param name="preset">Export preset defining format and loudness settings.</param>
    /// <param name="duration">Duration to render for each stem.</param>
    /// <param name="progress">Optional progress callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Export result with details for each stem.</returns>
    public async Task<StemExportResult> ExportStemsAsync(
        IEnumerable<StemDefinition> stems,
        string outputDir,
        ExportPreset preset,
        TimeSpan duration,
        IProgress<StemExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        var stemList = stems.Where(s => s.Enabled).ToList();
        var results = new List<StemExportItemResult>();

        if (stemList.Count == 0)
        {
            return new StemExportResult(true, outputDir, results, TimeSpan.Zero);
        }

        // Ensure output directory exists
        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        progress?.Report(new StemExportProgress(
            "", 0, stemList.Count, 0, 0,
            StemExportPhase.Preparing, "Preparing stem export..."));

        for (int i = 0; i < stemList.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var stem = stemList[i];
            double baseProgress = (double)i / stemList.Count;

            progress?.Report(new StemExportProgress(
                stem.Name, i, stemList.Count, 0, baseProgress,
                StemExportPhase.Rendering, $"Exporting {stem.Name}..."));

            var result = await ExportSingleStemAsync(
                stem, outputDir, preset, duration,
                new Progress<double>(p =>
                {
                    double overallProgress = baseProgress + (p / stemList.Count);
                    progress?.Report(new StemExportProgress(
                        stem.Name, i, stemList.Count, p, overallProgress,
                        StemExportPhase.Rendering, $"Exporting {stem.Name}: {p * 100:F0}%"));
                }),
                cancellationToken);

            results.Add(result);
        }

        var totalDuration = DateTime.Now - startTime;
        bool allSuccess = results.All(r => r.Success);

        progress?.Report(new StemExportProgress(
            "", stemList.Count, stemList.Count, 1, 1,
            StemExportPhase.Complete,
            allSuccess ? "Stem export complete!" : "Stem export completed with errors"));

        return new StemExportResult(allSuccess, outputDir, results, totalDuration);
    }

    private async Task<StemExportItemResult> ExportSingleStemAsync(
        StemDefinition stem,
        string outputDir,
        ExportPreset preset,
        TimeSpan duration,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string outputPath = Path.Combine(outputDir, stem.SafeFileName + preset.FileExtension);

        try
        {
            // First, render the stem to a temporary WAV file
            string tempPath = Path.Combine(Path.GetTempPath(), $"stem_{Guid.NewGuid()}.wav");

            await RenderStemToFileAsync(stem.Source, tempPath, duration, progress, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                CleanupTempFile(tempPath);
                return new StemExportItemResult(stem.Name, outputPath, false, "Cancelled", null);
            }

            // Now export with preset (applies loudness normalization)
            var exportResult = await _recorder.ExportWithPresetAsync(
                tempPath, outputPath, preset, null, cancellationToken);

            CleanupTempFile(tempPath);

            return new StemExportItemResult(
                stem.Name,
                outputPath,
                exportResult.Success,
                exportResult.ErrorMessage,
                exportResult.OutputMeasurement);
        }
        catch (Exception ex)
        {
            return new StemExportItemResult(stem.Name, outputPath, false, ex.Message, null);
        }
    }

    private async Task RenderStemToFileAsync(
        ISampleProvider source,
        string outputPath,
        TimeSpan duration,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            var format = WaveFormat.CreateIeeeFloatWaveFormat(
                source.WaveFormat.SampleRate,
                source.WaveFormat.Channels);

            using var writer = new WaveFileWriter(outputPath, format);
            float[] buffer = new float[4096];

            long targetSamples = (long)(duration.TotalSeconds * source.WaveFormat.SampleRate * source.WaveFormat.Channels);
            long totalSamples = 0;

            while (totalSamples < targetSamples && !cancellationToken.IsCancellationRequested)
            {
                int toRead = (int)Math.Min(buffer.Length, targetSamples - totalSamples);
                int samplesRead = source.Read(buffer, 0, toRead);

                if (samplesRead == 0)
                {
                    // Source ended before target duration
                    break;
                }

                writer.WriteSamples(buffer, 0, samplesRead);
                totalSamples += samplesRead;

                progress?.Report((double)totalSamples / targetSamples);
            }
        }, cancellationToken);
    }

    private static void CleanupTempFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    /// <summary>
    /// Creates stem definitions from an audio engine's channels.
    /// </summary>
    /// <param name="engine">The audio engine.</param>
    /// <returns>List of stem definitions.</returns>
    public static List<StemDefinition> CreateStemsFromEngine(AudioEngine engine)
    {
        var stems = new List<StemDefinition>();

        // This would need to be extended based on how the engine exposes its channels/buses
        // For now, we provide a simple example

        // Master output
        var masterCapture = engine.GetRecordingCaptureProvider();
        stems.Add(new StemDefinition("Master", masterCapture));

        return stems;
    }

    /// <summary>
    /// Creates stem definitions from a collection of sample providers.
    /// </summary>
    /// <param name="sources">Dictionary of name to sample provider.</param>
    /// <returns>List of stem definitions.</returns>
    public static List<StemDefinition> CreateStemsFromSources(IDictionary<string, ISampleProvider> sources)
    {
        return sources.Select(kvp => new StemDefinition(kvp.Key, kvp.Value)).ToList();
    }
}

/// <summary>
/// Builder for creating stem export configurations.
/// </summary>
public class StemExportBuilder
{
    private readonly List<StemDefinition> _stems = new();
    private string _outputDir = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
    private ExportPreset _preset = ExportPresets.YouTube;
    private TimeSpan _duration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Adds a stem to the export.
    /// </summary>
    public StemExportBuilder AddStem(string name, ISampleProvider source, bool enabled = true)
    {
        _stems.Add(new StemDefinition(name, source, enabled));
        return this;
    }

    /// <summary>
    /// Adds multiple stems from a dictionary.
    /// </summary>
    public StemExportBuilder AddStems(IDictionary<string, ISampleProvider> sources)
    {
        foreach (var kvp in sources)
        {
            _stems.Add(new StemDefinition(kvp.Key, kvp.Value));
        }
        return this;
    }

    /// <summary>
    /// Sets the output directory.
    /// </summary>
    public StemExportBuilder ToDirectory(string outputDir)
    {
        _outputDir = outputDir;
        return this;
    }

    /// <summary>
    /// Sets the export preset.
    /// </summary>
    public StemExportBuilder WithPreset(ExportPreset preset)
    {
        _preset = preset;
        return this;
    }

    /// <summary>
    /// Sets the duration to render.
    /// </summary>
    public StemExportBuilder WithDuration(TimeSpan duration)
    {
        _duration = duration;
        return this;
    }

    /// <summary>
    /// Executes the stem export.
    /// </summary>
    public Task<StemExportResult> ExportAsync(
        IProgress<StemExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var exporter = new StemExporter();
        return exporter.ExportStemsAsync(_stems, _outputDir, _preset, _duration, progress, cancellationToken);
    }

    /// <summary>
    /// Gets the configured stems.
    /// </summary>
    public IReadOnlyList<StemDefinition> Stems => _stems.AsReadOnly();
}
