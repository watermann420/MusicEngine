//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Session management system for saving/loading the entire engine state.


using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MusicEngine.Core.Progress;


namespace MusicEngine.Core;


/// <summary>
/// Metadata for a session file.
/// </summary>
public class SessionMetadata
{
    /// <summary>Session/project name.</summary>
    public string Name { get; set; } = "Untitled";

    /// <summary>Author/creator of the session.</summary>
    public string Author { get; set; } = "";

    /// <summary>Date when the session was created.</summary>
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    /// <summary>Date when the session was last modified.</summary>
    public DateTime ModifiedDate { get; set; } = DateTime.Now;

    /// <summary>Session file format version.</summary>
    public string Version { get; set; } = "1.0";

    /// <summary>Optional description or notes.</summary>
    public string Description { get; set; } = "";

    /// <summary>Optional tags for categorization.</summary>
    public List<string> Tags { get; set; } = new();
}


/// <summary>
/// Configuration for an instrument instance.
/// </summary>
public class InstrumentConfig
{
    /// <summary>Unique identifier for this instrument.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Type of instrument (e.g., "WavetableSynth", "FMSynth", "PolySynth").</summary>
    public string Type { get; set; } = "";

    /// <summary>Display name for the instrument.</summary>
    public string Name { get; set; } = "Instrument";

    /// <summary>Parameter values for the instrument.</summary>
    public Dictionary<string, float> Parameters { get; set; } = new();

    /// <summary>Optional VST plugin path if this is a VST instrument.</summary>
    public string? VstPath { get; set; }

    /// <summary>Optional VST plugin state data.</summary>
    public byte[]? VstState { get; set; }
}


/// <summary>
/// Configuration for an effect instance.
/// </summary>
public class EffectConfig
{
    /// <summary>Unique identifier for this effect.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Type of effect (e.g., "ReverbEffect", "DelayEffect", "CompressorEffect", "VstEffect").</summary>
    public string Type { get; set; } = "";

    /// <summary>Display name for the effect.</summary>
    public string Name { get; set; } = "Effect";

    /// <summary>Whether the effect is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Dry/wet mix ratio (0.0 = fully dry, 1.0 = fully wet).</summary>
    public float Mix { get; set; } = 1.0f;

    /// <summary>Parameter values for the effect.</summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>Whether this is a VST effect.</summary>
    public bool IsVstEffect { get; set; }

    /// <summary>Path to the VST plugin file (for VST effects).</summary>
    public string? VstPath { get; set; }

    /// <summary>VST format ("VST2" or "VST3").</summary>
    public string VstFormat { get; set; } = "";

    /// <summary>VST plugin state data (for restoring plugin state).</summary>
    public byte[]? VstState { get; set; }

    /// <summary>Slot index in the channel's effect chain.</summary>
    public int SlotIndex { get; set; }

    /// <summary>Effect category (e.g., "Dynamics", "Time-Based", "Modulation", "VST").</summary>
    public string Category { get; set; } = "";

    /// <summary>Effect color for visual representation.</summary>
    public string EffectColor { get; set; } = "#6B7280";
}


/// <summary>
/// Configuration for audio/MIDI routing.
/// </summary>
public class RoutingConfig
{
    /// <summary>MIDI input to instrument routing.</summary>
    public Dictionary<int, string> MidiInputRouting { get; set; } = new();

    /// <summary>Instrument to channel routing.</summary>
    public Dictionary<string, int> InstrumentChannelRouting { get; set; } = new();

    /// <summary>Channel to effect chain routing.</summary>
    public Dictionary<int, List<string>> ChannelEffectChains { get; set; } = new();

    /// <summary>Send/bus routing configuration.</summary>
    public Dictionary<string, List<string>> SendRouting { get; set; } = new();

    /// <summary>Master channel effect chain.</summary>
    public List<string> MasterEffectChain { get; set; } = new();
}


/// <summary>
/// Serializable pattern data for session storage.
/// </summary>
public class PatternConfig
{
    /// <summary>Unique identifier for this pattern.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Display name for the pattern.</summary>
    public string Name { get; set; } = "Pattern";

    /// <summary>ID of the instrument assigned to this pattern.</summary>
    public string InstrumentId { get; set; } = "";

    /// <summary>Loop length in beats.</summary>
    public double LoopLength { get; set; } = 4.0;

    /// <summary>Whether the pattern is looping.</summary>
    public bool IsLooping { get; set; } = true;

    /// <summary>Whether the pattern is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Note events in the pattern.</summary>
    public List<NoteEventConfig> Events { get; set; } = new();
}


/// <summary>
/// Serializable note event data.
/// </summary>
public class NoteEventConfig
{
    /// <summary>MIDI note number (0-127).</summary>
    public int Note { get; set; }

    /// <summary>Beat position in the pattern.</summary>
    public double Beat { get; set; }

    /// <summary>Duration in beats.</summary>
    public double Duration { get; set; }

    /// <summary>Velocity (0-127).</summary>
    public int Velocity { get; set; }
}


/// <summary>
/// Serializable automation data for session storage.
/// </summary>
public class AutomationConfig
{
    /// <summary>Automation lanes.</summary>
    public List<AutomationLaneConfig> Lanes { get; set; } = new();
}


/// <summary>
/// Serializable automation lane data.
/// </summary>
public class AutomationLaneConfig
{
    /// <summary>Target identifier.</summary>
    public string TargetId { get; set; } = "";

    /// <summary>Property name to automate.</summary>
    public string PropertyName { get; set; } = "";

    /// <summary>Minimum value.</summary>
    public float MinValue { get; set; } = 0f;

    /// <summary>Maximum value.</summary>
    public float MaxValue { get; set; } = 1f;

    /// <summary>Whether the lane is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Whether time is measured in beats (vs seconds).</summary>
    public bool UseBeats { get; set; } = true;

    /// <summary>Automation points.</summary>
    public List<AutomationPointConfig> Points { get; set; } = new();
}


/// <summary>
/// Serializable automation point data.
/// </summary>
public class AutomationPointConfig
{
    /// <summary>Time position.</summary>
    public double Time { get; set; }

    /// <summary>Parameter value.</summary>
    public float Value { get; set; }

    /// <summary>Curve type for interpolation.</summary>
    public string CurveType { get; set; } = "Linear";

    /// <summary>Bezier control point X1.</summary>
    public float BezierX1 { get; set; } = 0.5f;

    /// <summary>Bezier control point Y1.</summary>
    public float BezierY1 { get; set; } = 0f;

    /// <summary>Bezier control point X2.</summary>
    public float BezierX2 { get; set; } = 0.5f;

    /// <summary>Bezier control point Y2.</summary>
    public float BezierY2 { get; set; } = 1f;
}


/// <summary>
/// Configuration for freeze/bounce operations.
/// </summary>
public class FreezeConfig
{
    /// <summary>Directory where frozen audio files are stored.</summary>
    public string FrozenTracksDirectory { get; set; } = "";

    /// <summary>Whether to automatically freeze tracks that exceed CPU threshold.</summary>
    public bool AutoFreeze { get; set; } = false;

    /// <summary>CPU usage threshold (0-100) for auto-freeze.</summary>
    public float AutoFreezeCpuThreshold { get; set; } = 80f;

    /// <summary>Whether to save frozen audio to disk.</summary>
    public bool SaveToFile { get; set; } = true;

    /// <summary>Whether to keep frozen audio in memory.</summary>
    public bool KeepInMemory { get; set; } = true;

    /// <summary>Whether to include effects in the freeze.</summary>
    public bool FreezeWithEffects { get; set; } = true;

    /// <summary>Tail length in seconds for capturing reverb/delay tails.</summary>
    public double TailLengthSeconds { get; set; } = 2.0;

    /// <summary>Track indices that are currently frozen.</summary>
    public List<int> FrozenTrackIndices { get; set; } = new();

    /// <summary>Frozen track data for each frozen track (key is track index).</summary>
    public Dictionary<int, FrozenTrackConfig> FrozenTracks { get; set; } = new();
}


/// <summary>
/// Configuration data for a frozen track.
/// </summary>
public class FrozenTrackConfig
{
    /// <summary>Track index that was frozen.</summary>
    public int TrackIndex { get; set; }

    /// <summary>Path to the frozen audio file.</summary>
    public string? AudioFilePath { get; set; }

    /// <summary>Duration of the frozen audio in seconds.</summary>
    public double DurationSeconds { get; set; }

    /// <summary>BPM at which the track was frozen.</summary>
    public double FreezeBpm { get; set; } = 120.0;

    /// <summary>Start position in beats where the freeze begins.</summary>
    public double StartPositionBeats { get; set; }

    /// <summary>End position in beats where the freeze ends.</summary>
    public double EndPositionBeats { get; set; }

    /// <summary>Sample rate of the frozen audio.</summary>
    public int SampleRate { get; set; } = 44100;

    /// <summary>Number of channels in the frozen audio.</summary>
    public int Channels { get; set; } = 2;

    /// <summary>Timestamp when the track was frozen.</summary>
    public DateTime FreezeTimestamp { get; set; }

    /// <summary>Original synth type name for restoration.</summary>
    public string OriginalSynthTypeName { get; set; } = "";

    /// <summary>Original synth parameters for restoration.</summary>
    public Dictionary<string, float> OriginalSynthParameters { get; set; } = new();

    /// <summary>Original effect chain configuration for restoration.</summary>
    public List<EffectConfig> OriginalEffectChain { get; set; } = new();
}


/// <summary>
/// Complete session data for serialization.
/// Contains all engine state that can be saved and loaded.
/// </summary>
public class SessionData
{
    /// <summary>Session metadata (name, author, dates).</summary>
    public SessionMetadata Metadata { get; set; } = new();

    /// <summary>Tempo in beats per minute.</summary>
    public float BPM { get; set; } = 120f;

    /// <summary>Audio sample rate.</summary>
    public int SampleRate { get; set; } = 44100;

    /// <summary>Time signature numerator.</summary>
    public int TimeSignatureNumerator { get; set; } = 4;

    /// <summary>Time signature denominator.</summary>
    public int TimeSignatureDenominator { get; set; } = 4;

    /// <summary>Master volume level.</summary>
    public float MasterVolume { get; set; } = 1.0f;

    /// <summary>Pattern configurations.</summary>
    public List<PatternConfig> Patterns { get; set; } = new();

    /// <summary>Instrument configurations.</summary>
    public List<InstrumentConfig> InstrumentConfigs { get; set; } = new();

    /// <summary>Effect configurations.</summary>
    public List<EffectConfig> EffectConfigs { get; set; } = new();

    /// <summary>Audio/MIDI routing configuration.</summary>
    public RoutingConfig RoutingConfig { get; set; } = new();

    /// <summary>Automation data.</summary>
    public AutomationConfig AutomationData { get; set; } = new();

    /// <summary>Freeze/bounce configuration and state.</summary>
    public FreezeConfig FreezeConfig { get; set; } = new();

    /// <summary>Custom data for extensions.</summary>
    public Dictionary<string, string> CustomData { get; set; } = new();
}


/// <summary>
/// Session template for creating new sessions with predefined settings.
/// </summary>
public class SessionTemplate
{
    /// <summary>Template name.</summary>
    public string Name { get; set; } = "Default";

    /// <summary>Template description.</summary>
    public string Description { get; set; } = "";

    /// <summary>Default BPM for new sessions.</summary>
    public float BPM { get; set; } = 120f;

    /// <summary>Default sample rate.</summary>
    public int SampleRate { get; set; } = 44100;

    /// <summary>Default time signature numerator.</summary>
    public int TimeSignatureNumerator { get; set; } = 4;

    /// <summary>Default time signature denominator.</summary>
    public int TimeSignatureDenominator { get; set; } = 4;

    /// <summary>Predefined instruments.</summary>
    public List<InstrumentConfig> Instruments { get; set; } = new();

    /// <summary>Predefined effects.</summary>
    public List<EffectConfig> Effects { get; set; } = new();

    /// <summary>Predefined routing.</summary>
    public RoutingConfig Routing { get; set; } = new();
}


/// <summary>
/// Main session management class for saving/loading engine state.
/// </summary>
public class EngineSession
{
    private static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Current session data.</summary>
    public SessionData Data { get; private set; } = new();

    /// <summary>Current file path (null if not saved).</summary>
    public string? FilePath { get; private set; }

    /// <summary>Whether the session has unsaved changes.</summary>
    public bool HasUnsavedChanges { get; private set; }

    /// <summary>Available session templates.</summary>
    public static List<SessionTemplate> Templates { get; } = new()
    {
        new SessionTemplate
        {
            Name = "Default",
            Description = "Standard session with default settings",
            BPM = 120f,
            SampleRate = 44100
        },
        new SessionTemplate
        {
            Name = "EDM",
            Description = "Electronic dance music template",
            BPM = 128f,
            SampleRate = 44100
        },
        new SessionTemplate
        {
            Name = "Hip Hop",
            Description = "Hip hop / trap template",
            BPM = 90f,
            SampleRate = 44100
        },
        new SessionTemplate
        {
            Name = "Orchestral",
            Description = "Film scoring / orchestral template",
            BPM = 100f,
            SampleRate = 48000
        },
        new SessionTemplate
        {
            Name = "High Quality",
            Description = "High sample rate for mastering",
            BPM = 120f,
            SampleRate = 96000
        }
    };

    /// <summary>Event fired when session is loaded.</summary>
    public event EventHandler? SessionLoaded;

    /// <summary>Event fired when session is saved.</summary>
    public event EventHandler? SessionSaved;

    /// <summary>Event fired when session data changes.</summary>
    public event EventHandler? SessionChanged;

    /// <summary>
    /// Creates a new empty session.
    /// </summary>
    public EngineSession()
    {
        Data = new SessionData();
    }

    /// <summary>
    /// Creates a new session from a template.
    /// </summary>
    /// <param name="template">The template to use.</param>
    public EngineSession(SessionTemplate template)
    {
        Data = new SessionData
        {
            BPM = template.BPM,
            SampleRate = template.SampleRate,
            TimeSignatureNumerator = template.TimeSignatureNumerator,
            TimeSignatureDenominator = template.TimeSignatureDenominator,
            InstrumentConfigs = new List<InstrumentConfig>(template.Instruments),
            EffectConfigs = new List<EffectConfig>(template.Effects),
            RoutingConfig = template.Routing
        };
    }

    /// <summary>
    /// Saves the session to a JSON file.
    /// </summary>
    /// <param name="path">Path to save the session file.</param>
    public void Save(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }

        // Update modification date
        Data.Metadata.ModifiedDate = DateTime.Now;

        // Ensure directory exists
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Serialize to JSON
        string json = JsonSerializer.Serialize(Data, DefaultJsonOptions);
        File.WriteAllText(path, json);

        FilePath = path;
        HasUnsavedChanges = false;
        SessionSaved?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Loads a session from a JSON file.
    /// </summary>
    /// <param name="path">Path to the session file.</param>
    public void Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Session file not found.", path);
        }

        string json = File.ReadAllText(path);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        var data = JsonSerializer.Deserialize<SessionData>(json, options);
        if (data == null)
        {
            throw new InvalidDataException("Failed to deserialize session file.");
        }

        Data = data;
        FilePath = path;
        HasUnsavedChanges = false;
        SessionLoaded?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Asynchronously saves the session to a JSON file.
    /// </summary>
    public async Task SaveAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }

        Data.Metadata.ModifiedDate = DateTime.Now;

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(Data, DefaultJsonOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);

        FilePath = path;
        HasUnsavedChanges = false;
        SessionSaved?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Asynchronously loads a session from a JSON file.
    /// </summary>
    public async Task LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Session file not found.", path);
        }

        string json = await File.ReadAllTextAsync(path, cancellationToken);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        var data = JsonSerializer.Deserialize<SessionData>(json, options);
        if (data == null)
        {
            throw new InvalidDataException("Failed to deserialize session file.");
        }

        Data = data;
        FilePath = path;
        HasUnsavedChanges = false;
        SessionLoaded?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Asynchronously loads a session from a JSON file with progress reporting.
    /// </summary>
    /// <param name="path">Path to the session file.</param>
    /// <param name="progress">Optional progress reporter using the <see cref="SessionLoadProgress"/> record.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task that completes when the session is loaded.</returns>
    /// <remarks>
    /// This overload provides detailed progress reporting through the stages:
    /// Reading File, Parsing JSON, Validating, and Complete.
    /// Uses <see cref="SessionLoadProgress"/> record for structured progress reporting.
    /// </remarks>
    /// <example>
    /// <code>
    /// var session = new Session();
    /// var progress = new Progress&lt;SessionLoadProgress&gt;(p =>
    ///     Console.WriteLine($"{p.Stage}: {p.PercentComplete:F1}%"));
    ///
    /// await session.LoadAsync("project.mep", progress, cancellationToken);
    /// </code>
    /// </example>
    public async Task LoadAsync(
        string path,
        IProgress<SessionLoadProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }

        const int totalSteps = 4;

        // Step 1: Validate file exists
        ct.ThrowIfCancellationRequested();
        progress?.Report(new SessionLoadProgress("Validating", 0, totalSteps, path));

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Session file not found.", path);
        }

        // Step 2: Read file
        ct.ThrowIfCancellationRequested();
        progress?.Report(new SessionLoadProgress("Reading File", 1, totalSteps, path));

        string json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);

        // Step 3: Parse JSON
        ct.ThrowIfCancellationRequested();
        progress?.Report(new SessionLoadProgress("Parsing JSON", 2, totalSteps, path));

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        var data = JsonSerializer.Deserialize<SessionData>(json, options);
        if (data == null)
        {
            throw new InvalidDataException("Failed to deserialize session file.");
        }

        // Step 4: Validate and apply
        ct.ThrowIfCancellationRequested();
        progress?.Report(new SessionLoadProgress("Validating", 3, totalSteps, path));

        // Basic validation
        if (data.BPM <= 0 || data.BPM > 999)
        {
            throw new InvalidDataException($"Invalid BPM value: {data.BPM}");
        }

        if (data.SampleRate < 8000 || data.SampleRate > 192000)
        {
            throw new InvalidDataException($"Invalid sample rate: {data.SampleRate}");
        }

        Data = data;
        FilePath = path;
        HasUnsavedChanges = false;

        progress?.Report(SessionLoadProgress.Complete(totalSteps));
        SessionLoaded?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Asynchronously saves the session to a JSON file with progress reporting.
    /// </summary>
    /// <param name="path">Path to save the session file.</param>
    /// <param name="progress">Optional progress reporter using the <see cref="SessionLoadProgress"/> record.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task that completes when the session is saved.</returns>
    /// <remarks>
    /// This overload provides detailed progress reporting through the stages:
    /// Preparing, Serializing, Writing File, and Complete.
    /// Uses <see cref="SessionLoadProgress"/> record for structured progress reporting.
    /// </remarks>
    /// <example>
    /// <code>
    /// var session = new Session();
    /// session.Data.BPM = 140f;
    ///
    /// var progress = new Progress&lt;SessionLoadProgress&gt;(p =>
    ///     Console.WriteLine($"{p.Stage}: {p.PercentComplete:F1}%"));
    ///
    /// await session.SaveAsync("project.mep", progress, cancellationToken);
    /// </code>
    /// </example>
    public async Task SaveAsync(
        string path,
        IProgress<SessionLoadProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }

        const int totalSteps = 4;

        // Step 1: Prepare
        ct.ThrowIfCancellationRequested();
        progress?.Report(new SessionLoadProgress("Preparing", 0, totalSteps, path));

        // Update modification timestamp
        Data.Metadata.ModifiedDate = DateTime.Now;

        // Validate session before saving
        var validationErrors = Validate();
        if (validationErrors.Count > 0)
        {
            throw new InvalidDataException($"Session validation failed: {string.Join(", ", validationErrors)}");
        }

        // Step 2: Serialize
        ct.ThrowIfCancellationRequested();
        progress?.Report(new SessionLoadProgress("Serializing", 1, totalSteps, path));

        string json = JsonSerializer.Serialize(Data, DefaultJsonOptions);

        // Step 3: Ensure directory exists and write
        ct.ThrowIfCancellationRequested();
        progress?.Report(new SessionLoadProgress("Writing File", 2, totalSteps, path));

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Write atomically using a temporary file
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
        catch
        {
            // Clean up temp file on failure
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* Ignore cleanup errors */ }
            }
            throw;
        }

        // Step 4: Complete
        FilePath = path;
        HasUnsavedChanges = false;

        progress?.Report(SessionLoadProgress.Complete(totalSteps));
        SessionSaved?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Creates session data from the current engine state.
    /// </summary>
    /// <param name="engine">The audio engine to capture state from.</param>
    /// <returns>The populated session data.</returns>
    public static SessionData CreateFromEngine(AudioEngine engine)
    {
        if (engine == null)
        {
            throw new ArgumentNullException(nameof(engine));
        }

        var data = new SessionData
        {
            SampleRate = Settings.SampleRate,
            Metadata = new SessionMetadata
            {
                CreatedDate = DateTime.Now,
                ModifiedDate = DateTime.Now
            }
        };

        // Note: Actual implementation would extract state from engine components
        // This is a template that would need to be extended based on engine internals

        return data;
    }

    /// <summary>
    /// Creates session data from the engine and sequencer.
    /// </summary>
    /// <param name="engine">The audio engine.</param>
    /// <param name="sequencer">The sequencer with patterns.</param>
    /// <returns>The populated session data.</returns>
    public static SessionData CreateFromEngine(AudioEngine engine, Sequencer sequencer)
    {
        if (engine == null)
        {
            throw new ArgumentNullException(nameof(engine));
        }

        if (sequencer == null)
        {
            throw new ArgumentNullException(nameof(sequencer));
        }

        var data = CreateFromEngine(engine);
        data.BPM = (float)sequencer.Bpm;

        // Extract patterns from sequencer
        foreach (var pattern in sequencer.Patterns)
        {
            var patternConfig = new PatternConfig
            {
                Id = pattern.Id.ToString(),
                Name = pattern.Name,
                LoopLength = pattern.LoopLength,
                IsLooping = pattern.IsLooping,
                Enabled = pattern.Enabled
            };

            // Extract note events
            foreach (var noteEvent in pattern.Events)
            {
                patternConfig.Events.Add(new NoteEventConfig
                {
                    Note = noteEvent.Note,
                    Beat = noteEvent.Beat,
                    Duration = noteEvent.Duration,
                    Velocity = noteEvent.Velocity
                });
            }

            data.Patterns.Add(patternConfig);
        }

        return data;
    }

    /// <summary>
    /// Applies session data to an audio engine.
    /// </summary>
    /// <param name="engine">The audio engine to configure.</param>
    public void ApplyToEngine(AudioEngine engine)
    {
        if (engine == null)
        {
            throw new ArgumentNullException(nameof(engine));
        }

        // Apply master volume
        engine.SetAllChannelsGain(Data.MasterVolume);

        // Note: Full implementation would recreate instruments, effects, and routing
        // This requires factory methods for creating synths/effects by type name
    }

    /// <summary>
    /// Applies session data to an audio engine and sequencer.
    /// </summary>
    /// <param name="engine">The audio engine to configure.</param>
    /// <param name="sequencer">The sequencer to configure.</param>
    public void ApplyToEngine(AudioEngine engine, Sequencer sequencer)
    {
        if (engine == null)
        {
            throw new ArgumentNullException(nameof(engine));
        }

        if (sequencer == null)
        {
            throw new ArgumentNullException(nameof(sequencer));
        }

        ApplyToEngine(engine);

        // Apply sequencer settings
        sequencer.Bpm = Data.BPM;

        // Clear existing patterns
        sequencer.ClearPatterns();

        // Note: Pattern recreation would require instrument references
        // This is a template for the implementation
    }

    /// <summary>
    /// Creates a new session from a template.
    /// </summary>
    /// <param name="templateName">Name of the template to use.</param>
    /// <returns>A new session initialized from the template.</returns>
    public static EngineSession CreateFromTemplate(string templateName)
    {
        var template = Templates.Find(t =>
            t.Name.Equals(templateName, StringComparison.OrdinalIgnoreCase));

        if (template == null)
        {
            throw new ArgumentException($"Template '{templateName}' not found.", nameof(templateName));
        }

        return new EngineSession(template);
    }

    /// <summary>
    /// Saves the current session as a template.
    /// </summary>
    /// <param name="name">Name for the template.</param>
    /// <param name="description">Description for the template.</param>
    /// <returns>The created template.</returns>
    public SessionTemplate SaveAsTemplate(string name, string description = "")
    {
        var template = new SessionTemplate
        {
            Name = name,
            Description = description,
            BPM = Data.BPM,
            SampleRate = Data.SampleRate,
            TimeSignatureNumerator = Data.TimeSignatureNumerator,
            TimeSignatureDenominator = Data.TimeSignatureDenominator,
            Instruments = new List<InstrumentConfig>(Data.InstrumentConfigs),
            Effects = new List<EffectConfig>(Data.EffectConfigs),
            Routing = Data.RoutingConfig
        };

        Templates.Add(template);
        return template;
    }

    /// <summary>
    /// Marks the session as having unsaved changes.
    /// </summary>
    public void MarkChanged()
    {
        HasUnsavedChanges = true;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Gets basic info about a session file without fully loading it.
    /// </summary>
    /// <param name="path">Path to the session file.</param>
    /// <returns>Session metadata, or null if the file cannot be read.</returns>
    public static SessionMetadata? GetSessionInfo(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            string json = File.ReadAllText(path);
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
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Exports the session to a different format.
    /// </summary>
    /// <param name="path">Output path.</param>
    /// <param name="format">Export format (json, xml).</param>
    public void Export(string path, string format = "json")
    {
        switch (format.ToLowerInvariant())
        {
            case "json":
                Save(path);
                break;
            default:
                throw new NotSupportedException($"Export format '{format}' is not supported.");
        }
    }

    /// <summary>
    /// Validates the session data for consistency.
    /// </summary>
    /// <returns>List of validation errors, empty if valid.</returns>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (Data.BPM <= 0 || Data.BPM > 999)
        {
            errors.Add("BPM must be between 1 and 999.");
        }

        if (Data.SampleRate < 8000 || Data.SampleRate > 192000)
        {
            errors.Add("Sample rate must be between 8000 and 192000 Hz.");
        }

        if (Data.MasterVolume < 0 || Data.MasterVolume > 2)
        {
            errors.Add("Master volume must be between 0 and 2.");
        }

        // Validate patterns
        foreach (var pattern in Data.Patterns)
        {
            if (pattern.LoopLength <= 0)
            {
                errors.Add($"Pattern '{pattern.Name}' has invalid loop length.");
            }

            foreach (var note in pattern.Events)
            {
                if (note.Note < 0 || note.Note > 127)
                {
                    errors.Add($"Pattern '{pattern.Name}' contains invalid MIDI note number.");
                }

                if (note.Velocity < 0 || note.Velocity > 127)
                {
                    errors.Add($"Pattern '{pattern.Name}' contains invalid velocity.");
                }
            }
        }

        return errors;
    }
}
