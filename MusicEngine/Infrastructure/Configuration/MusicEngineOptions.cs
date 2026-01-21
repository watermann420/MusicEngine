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
