// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

namespace MusicEngine.Infrastructure.Configuration;

/// <summary>
/// Root configuration options for MusicEngine.
/// </summary>
public class MusicEngineOptions
{
    /// <summary>
    /// The configuration section name for MusicEngine options.
    /// </summary>
    public const string SectionName = "MusicEngine";

    /// <summary>
    /// Gets or sets the audio configuration options.
    /// </summary>
    public AudioOptions Audio { get; set; } = new();

    /// <summary>
    /// Gets or sets the MIDI configuration options.
    /// </summary>
    public MidiOptions Midi { get; set; } = new();

    /// <summary>
    /// Gets or sets the VST plugin configuration options.
    /// </summary>
    public VstOptions Vst { get; set; } = new();

    /// <summary>
    /// Gets or sets the logging configuration options.
    /// </summary>
    public LoggingOptions Logging { get; set; } = new();

    /// <summary>
    /// Gets or sets the buffer pool configuration options.
    /// </summary>
    public BufferPoolOptions BufferPool { get; set; } = new();
}

/// <summary>
/// Configuration options for audio processing.
/// </summary>
public class AudioOptions
{
    /// <summary>
    /// Gets or sets the sample rate in Hz. Default is 44100.
    /// </summary>
    public int SampleRate { get; set; } = 44100;

    /// <summary>
    /// Gets or sets the audio buffer size in samples. Default is 512.
    /// </summary>
    public int BufferSize { get; set; } = 512;

    /// <summary>
    /// Gets or sets the number of audio channels. Default is 2 (stereo).
    /// </summary>
    public int Channels { get; set; } = 2;

    /// <summary>
    /// Gets or sets the bit depth for audio processing. Default is 32.
    /// </summary>
    public int BitDepth { get; set; } = 32;
}

/// <summary>
/// Configuration options for MIDI processing.
/// </summary>
public class MidiOptions
{
    /// <summary>
    /// Gets or sets the MIDI refresh rate in milliseconds. Default is 1.
    /// </summary>
    public int RefreshRateMs { get; set; } = 1;

    /// <summary>
    /// Gets or sets the MIDI buffer size. Default is 1024.
    /// </summary>
    public int BufferSize { get; set; } = 1024;
}

/// <summary>
/// Configuration options for VST plugin management.
/// </summary>
public class VstOptions
{
    /// <summary>
    /// Gets or sets the list of paths to search for VST plugins.
    /// </summary>
    public List<string> SearchPaths { get; set; } = new()
    {
        @"C:\Program Files\VSTPlugins",
        @"C:\Program Files\Common Files\VST3"
    };

    /// <summary>
    /// Gets or sets the VST processing buffer size. Default is 512.
    /// </summary>
    public int BufferSize { get; set; } = 512;

    /// <summary>
    /// Gets or sets the VST processing timeout in milliseconds. Default is 100.
    /// </summary>
    public int ProcessingTimeout { get; set; } = 100;
}

/// <summary>
/// Configuration options for logging.
/// </summary>
public class LoggingOptions
{
    /// <summary>
    /// Gets or sets the minimum logging level. Default is "Information".
    /// </summary>
    public string MinimumLevel { get; set; } = "Information";

    /// <summary>
    /// Gets or sets whether file logging is enabled. Default is true.
    /// </summary>
    public bool EnableFile { get; set; } = true;

    /// <summary>
    /// Gets or sets whether console logging is enabled. Default is true.
    /// </summary>
    public bool EnableConsole { get; set; } = true;

    /// <summary>
    /// Gets or sets the directory for log files. Default is "logs".
    /// </summary>
    public string LogDirectory { get; set; } = "logs";
}

/// <summary>
/// Configuration options for the audio buffer pool.
/// </summary>
public class BufferPoolOptions
{
    /// <summary>
    /// Gets or sets the maximum number of buffers to retain per size bucket.
    /// Default is 50.
    /// </summary>
    public int MaxBuffersPerBucket { get; set; } = 50;

    /// <summary>
    /// Gets or sets the maximum total pool size in bytes.
    /// Default is 100MB.
    /// </summary>
    public long MaxPoolSizeBytes { get; set; } = 100 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the interval in milliseconds for periodic trimming.
    /// Default is 30 seconds. Set to 0 to disable.
    /// </summary>
    public int TrimIntervalMs { get; set; } = 30000;

    /// <summary>
    /// Gets or sets whether to clear buffers when returning to pool.
    /// Default is false for performance.
    /// </summary>
    public bool ClearOnReturn { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to pre-allocate buffers at startup.
    /// Default is false.
    /// </summary>
    public bool PreAllocate { get; set; } = false;

    /// <summary>
    /// Gets or sets the number of buffers to pre-allocate per size.
    /// Default is 5.
    /// </summary>
    public int PreAllocateCount { get; set; } = 5;

    /// <summary>
    /// Gets or sets whether to track detailed statistics.
    /// Default is true.
    /// </summary>
    public bool EnableStatistics { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to monitor memory pressure.
    /// Default is true.
    /// </summary>
    public bool EnableMemoryPressureMonitoring { get; set; } = true;
}
