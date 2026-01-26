// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using System;

namespace MusicEngine.Core;

/// <summary>
/// Event arguments for recording progress updates.
/// Provides real-time information about the ongoing recording.
/// </summary>
public class RecordingProgressEventArgs : EventArgs
{
    /// <summary>
    /// Gets the total duration of audio recorded so far.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Gets the total number of samples recorded so far.
    /// </summary>
    public long SamplesRecorded { get; }

    /// <summary>
    /// Gets the current peak level in dB (0 dB = full scale).
    /// Typically ranges from -infinity to 0 dB.
    /// </summary>
    public float PeakLevel { get; }

    /// <summary>
    /// Gets the peak level as a linear value (0.0 to 1.0).
    /// </summary>
    public float PeakLevelLinear { get; }

    /// <summary>
    /// Gets the estimated file size in bytes based on current recording.
    /// </summary>
    public long EstimatedFileSize { get; }

    /// <summary>
    /// Initializes a new instance of RecordingProgressEventArgs.
    /// </summary>
    /// <param name="duration">The recording duration.</param>
    /// <param name="samplesRecorded">Total samples recorded.</param>
    /// <param name="peakLevel">Peak level in dB.</param>
    public RecordingProgressEventArgs(TimeSpan duration, long samplesRecorded, float peakLevel)
    {
        Duration = duration;
        SamplesRecorded = samplesRecorded;
        PeakLevel = peakLevel;
        PeakLevelLinear = (float)Math.Pow(10, peakLevel / 20.0);
        EstimatedFileSize = 0;
    }

    /// <summary>
    /// Initializes a new instance of RecordingProgressEventArgs with file size estimate.
    /// </summary>
    /// <param name="duration">The recording duration.</param>
    /// <param name="samplesRecorded">Total samples recorded.</param>
    /// <param name="peakLevel">Peak level in dB.</param>
    /// <param name="estimatedFileSize">Estimated file size in bytes.</param>
    public RecordingProgressEventArgs(TimeSpan duration, long samplesRecorded, float peakLevel, long estimatedFileSize)
        : this(duration, samplesRecorded, peakLevel)
    {
        EstimatedFileSize = estimatedFileSize;
    }

    /// <summary>
    /// Returns a formatted string representation of the recording progress.
    /// </summary>
    public override string ToString()
    {
        return $"Duration: {Duration:hh\\:mm\\:ss\\.fff}, Samples: {SamplesRecorded:N0}, Peak: {PeakLevel:F1} dB";
    }
}

/// <summary>
/// Event arguments for recording completion.
/// Provides final information about the completed recording.
/// </summary>
public class RecordingCompletedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the path to the output file.
    /// </summary>
    public string OutputPath { get; }

    /// <summary>
    /// Gets the total duration of the recording.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Gets the size of the output file in bytes.
    /// </summary>
    public long FileSize { get; }

    /// <summary>
    /// Gets whether the recording completed successfully.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Gets the error that occurred during recording, if any.
    /// Null if recording was successful.
    /// </summary>
    public Exception? Error { get; }

    /// <summary>
    /// Gets an error message if the recording failed, otherwise null.
    /// </summary>
    public string? ErrorMessage => Error?.Message;

    /// <summary>
    /// Gets the recording format used.
    /// </summary>
    public RecordingFormat Format { get; }

    /// <summary>
    /// Gets the sample rate of the recording in Hz.
    /// </summary>
    public int SampleRate { get; }

    /// <summary>
    /// Gets the number of channels in the recording.
    /// </summary>
    public int Channels { get; }

    /// <summary>
    /// Gets the total number of samples recorded.
    /// </summary>
    public long TotalSamples { get; }

    /// <summary>
    /// Initializes a new instance of RecordingCompletedEventArgs for a successful recording.
    /// </summary>
    /// <param name="outputPath">Path to the output file.</param>
    /// <param name="duration">Total duration of the recording.</param>
    /// <param name="fileSize">Size of the output file in bytes.</param>
    /// <param name="format">The recording format used.</param>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="channels">Number of channels.</param>
    /// <param name="totalSamples">Total samples recorded.</param>
    public RecordingCompletedEventArgs(
        string outputPath,
        TimeSpan duration,
        long fileSize,
        RecordingFormat format,
        int sampleRate,
        int channels,
        long totalSamples)
    {
        OutputPath = outputPath;
        Duration = duration;
        FileSize = fileSize;
        Success = true;
        Error = null;
        Format = format;
        SampleRate = sampleRate;
        Channels = channels;
        TotalSamples = totalSamples;
    }

    /// <summary>
    /// Initializes a new instance of RecordingCompletedEventArgs for a failed recording.
    /// </summary>
    /// <param name="outputPath">Path to the output file (may be incomplete).</param>
    /// <param name="duration">Duration recorded before failure.</param>
    /// <param name="error">The exception that caused the failure.</param>
    public RecordingCompletedEventArgs(string outputPath, TimeSpan duration, Exception error)
    {
        OutputPath = outputPath;
        Duration = duration;
        FileSize = 0;
        Success = false;
        Error = error;
        Format = RecordingFormat.Wav16Bit;
        SampleRate = 0;
        Channels = 0;
        TotalSamples = 0;
    }

    /// <summary>
    /// Returns a formatted string representation of the recording completion status.
    /// </summary>
    public override string ToString()
    {
        if (Success)
        {
            return $"Recording completed: {OutputPath}, Duration: {Duration:hh\\:mm\\:ss\\.fff}, Size: {FileSize / 1024.0 / 1024.0:F2} MB";
        }
        return $"Recording failed: {ErrorMessage}";
    }
}
