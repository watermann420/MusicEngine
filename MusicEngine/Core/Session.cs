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

    /// <summary>Type of effect (e.g., "ReverbEffect", "DelayEffect", "CompressorEffect").</summary>
    public string Type { get; set; } = "";

    /// <summary>Display name for the effect.</summary>
    public string Name { get; set; } = "Effect";

    /// <summary>Whether the effect is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Dry/wet mix ratio (0.0 = fully dry, 1.0 = fully wet).</summary>
    public float Mix { get; set; } = 1.0f;

    /// <summary>Parameter values for the effect.</summary>
    public Dictionary<string, object> Parameters { get; set; } = new();
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
public class Session
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
    public Session()
    {
        Data = new SessionData();
    }

    /// <summary>
    /// Creates a new session from a template.
    /// </summary>
    /// <param name="template">The template to use.</param>
    public Session(SessionTemplate template)
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
    public static Session CreateFromTemplate(string templateName)
    {
        var template = Templates.Find(t =>
            t.Name.Equals(templateName, StringComparison.OrdinalIgnoreCase));

        if (template == null)
        {
            throw new ArgumentException($"Template '{templateName}' not found.", nameof(templateName));
        }

        return new Session(template);
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
