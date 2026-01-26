// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace MusicEngine.Core.Export;

/// <summary>
/// OTIO schema types.
/// </summary>
public static class OTIOSchemaType
{
    /// <summary>Timeline schema.</summary>
    public const string Timeline = "Timeline.1";
    /// <summary>Stack schema (container for tracks).</summary>
    public const string Stack = "Stack.1";
    /// <summary>Track schema.</summary>
    public const string Track = "Track.1";
    /// <summary>Clip schema.</summary>
    public const string Clip = "Clip.1";
    /// <summary>Gap/filler schema.</summary>
    public const string Gap = "Gap.1";
    /// <summary>Transition schema.</summary>
    public const string Transition = "Transition.1";
    /// <summary>External reference schema.</summary>
    public const string ExternalReference = "ExternalReference.1";
    /// <summary>Missing reference schema.</summary>
    public const string MissingReference = "MissingReference.1";
    /// <summary>Generator reference schema.</summary>
    public const string GeneratorReference = "GeneratorReference.1";
    /// <summary>Rational time schema.</summary>
    public const string RationalTime = "RationalTime.1";
    /// <summary>Time range schema.</summary>
    public const string TimeRange = "TimeRange.1";
    /// <summary>Marker schema.</summary>
    public const string Marker = "Marker.1";
    /// <summary>Effect schema.</summary>
    public const string Effect = "Effect.1";
    /// <summary>Linear time warp effect.</summary>
    public const string LinearTimeWarp = "LinearTimeWarp.1";
}

/// <summary>
/// OTIO track kinds.
/// </summary>
public static class OTIOTrackKind
{
    /// <summary>Audio track.</summary>
    public const string Audio = "Audio";
    /// <summary>Video track.</summary>
    public const string Video = "Video";
}

/// <summary>
/// OTIO transition types.
/// </summary>
public static class OTIOTransitionType
{
    /// <summary>Crossfade/dissolve transition.</summary>
    public const string Dissolve = "SMPTE_Dissolve";
    /// <summary>Fade from black.</summary>
    public const string FadeFromBlack = "Fade_From_Black";
    /// <summary>Fade to black.</summary>
    public const string FadeToBlack = "Fade_To_Black";
}

/// <summary>
/// OTIO marker color values.
/// </summary>
public static class OTIOMarkerColor
{
    /// <summary>Red marker.</summary>
    public const string Red = "RED";
    /// <summary>Orange marker.</summary>
    public const string Orange = "ORANGE";
    /// <summary>Yellow marker.</summary>
    public const string Yellow = "YELLOW";
    /// <summary>Green marker.</summary>
    public const string Green = "GREEN";
    /// <summary>Cyan marker.</summary>
    public const string Cyan = "CYAN";
    /// <summary>Blue marker.</summary>
    public const string Blue = "BLUE";
    /// <summary>Purple marker.</summary>
    public const string Purple = "PURPLE";
    /// <summary>Magenta marker.</summary>
    public const string Magenta = "MAGENTA";
    /// <summary>White marker.</summary>
    public const string White = "WHITE";
    /// <summary>Black marker.</summary>
    public const string Black = "BLACK";
}

/// <summary>
/// Options for OTIO export.
/// </summary>
public class OTIOExportOptions
{
    /// <summary>Output file path.</summary>
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>Timeline name.</summary>
    public string TimelineName { get; set; } = "Untitled";

    /// <summary>Frame rate (default 48000 for audio sample rate).</summary>
    public double FrameRate { get; set; } = 48000;

    /// <summary>Whether to use sample rate as frame rate (for audio).</summary>
    public bool UseSampleRateAsFrameRate { get; set; } = true;

    /// <summary>Whether to include markers.</summary>
    public bool IncludeMarkers { get; set; } = true;

    /// <summary>Whether to include effects metadata.</summary>
    public bool IncludeEffects { get; set; } = true;

    /// <summary>Base path for media references (for relative paths).</summary>
    public string? MediaBasePath { get; set; }

    /// <summary>Whether to use relative paths for media.</summary>
    public bool UseRelativePaths { get; set; } = true;

    /// <summary>Whether to pretty-print the JSON output.</summary>
    public bool PrettyPrint { get; set; } = true;
}

/// <summary>
/// Represents an OTIO-compatible clip for export.
/// </summary>
public class OTIOClip
{
    /// <summary>Clip name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Source media path.</summary>
    public string MediaPath { get; set; } = string.Empty;

    /// <summary>Start time in timeline (samples or frames).</summary>
    public long TimelineStart { get; set; }

    /// <summary>Duration (samples or frames).</summary>
    public long Duration { get; set; }

    /// <summary>Source start offset (samples or frames).</summary>
    public long SourceStart { get; set; }

    /// <summary>Source duration available (samples or frames).</summary>
    public long SourceDuration { get; set; }

    /// <summary>Track index.</summary>
    public int TrackIndex { get; set; }

    /// <summary>Gain/volume (1.0 = unity).</summary>
    public double Gain { get; set; } = 1.0;

    /// <summary>Pan position (-1 to 1).</summary>
    public double Pan { get; set; } = 0.0;

    /// <summary>Time stretch ratio (1.0 = no stretch).</summary>
    public double TimeStretch { get; set; } = 1.0;

    /// <summary>Fade in duration.</summary>
    public long FadeInDuration { get; set; }

    /// <summary>Fade out duration.</summary>
    public long FadeOutDuration { get; set; }

    /// <summary>Whether source media exists.</summary>
    public bool HasMedia => !string.IsNullOrEmpty(MediaPath);
}

/// <summary>
/// Represents an OTIO-compatible marker.
/// </summary>
public class OTIOMarker
{
    /// <summary>Marker name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Position (samples or frames).</summary>
    public long Position { get; set; }

    /// <summary>Duration (0 for point marker).</summary>
    public long Duration { get; set; }

    /// <summary>Marker color.</summary>
    public string Color { get; set; } = OTIOMarkerColor.Blue;

    /// <summary>Comment/notes.</summary>
    public string Comment { get; set; } = string.Empty;
}

/// <summary>
/// Represents an OTIO track for export.
/// </summary>
public class OTIOTrack
{
    /// <summary>Track name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Track kind (Audio/Video).</summary>
    public string Kind { get; set; } = OTIOTrackKind.Audio;

    /// <summary>Clips on this track.</summary>
    public List<OTIOClip> Clips { get; } = new();

    /// <summary>Track index.</summary>
    public int Index { get; set; }

    /// <summary>Whether track is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Track metadata.</summary>
    public Dictionary<string, object> Metadata { get; } = new();
}

/// <summary>
/// Exports arrangements to OpenTimelineIO (OTIO) format.
/// </summary>
/// <remarks>
/// OpenTimelineIO is an open-source API and interchange format for
/// editorial timeline information. It is developed by Pixar and is
/// supported by many video editing applications including:
/// - DaVinci Resolve
/// - Adobe Premiere
/// - Avid Media Composer
/// - Nuke
/// - Hiero
///
/// This exporter creates OTIO JSON files that can be imported into
/// these applications for further editing or conform workflows.
/// </remarks>
public class OpenTLExporter
{
    private readonly OTIOExportOptions _options;
    private readonly List<OTIOTrack> _tracks = new();
    private readonly List<OTIOMarker> _markers = new();

    /// <summary>Event raised during export to report progress.</summary>
    public event EventHandler<(int Percent, string Message)>? Progress;

    /// <summary>
    /// Creates a new OTIO exporter with default options.
    /// </summary>
    public OpenTLExporter() : this(new OTIOExportOptions())
    {
    }

    /// <summary>
    /// Creates a new OTIO exporter with custom options.
    /// </summary>
    /// <param name="options">Export options.</param>
    public OpenTLExporter(OTIOExportOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Gets the export options.
    /// </summary>
    public OTIOExportOptions Options => _options;

    /// <summary>
    /// Adds a track to the export.
    /// </summary>
    /// <param name="name">Track name.</param>
    /// <param name="kind">Track kind (Audio/Video).</param>
    /// <returns>The created track.</returns>
    public OTIOTrack AddTrack(string name, string kind = OTIOTrackKind.Audio)
    {
        var track = new OTIOTrack
        {
            Name = name,
            Kind = kind,
            Index = _tracks.Count
        };
        _tracks.Add(track);
        return track;
    }

    /// <summary>
    /// Adds a clip to a track.
    /// </summary>
    /// <param name="trackIndex">Track index.</param>
    /// <param name="clip">Clip to add.</param>
    public void AddClip(int trackIndex, OTIOClip clip)
    {
        while (_tracks.Count <= trackIndex)
        {
            AddTrack($"Track {_tracks.Count + 1}");
        }

        clip.TrackIndex = trackIndex;
        _tracks[trackIndex].Clips.Add(clip);
    }

    /// <summary>
    /// Adds a marker to the timeline.
    /// </summary>
    /// <param name="marker">Marker to add.</param>
    public void AddMarker(OTIOMarker marker)
    {
        _markers.Add(marker);
    }

    /// <summary>
    /// Exports an arrangement to OTIO format.
    /// </summary>
    /// <param name="arrangement">Arrangement to export.</param>
    /// <param name="outputPath">Output file path.</param>
    public void Export(Arrangement arrangement, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(arrangement);
        ArgumentException.ThrowIfNullOrEmpty(outputPath);

        _options.OutputPath = outputPath;
        _tracks.Clear();
        _markers.Clear();

        ReportProgress(0, "Collecting arrangement data...");

        // Calculate frame rate based on sample rate
        double frameRate = _options.UseSampleRateAsFrameRate ? 48000 : _options.FrameRate;
        double samplesPerBeat = 48000 * 60.0 / arrangement.Bpm;

        // Collect clips by track
        var clipsByTrack = arrangement.AudioClips
            .GroupBy(c => c.TrackIndex)
            .OrderBy(g => g.Key);

        foreach (var trackGroup in clipsByTrack)
        {
            var track = AddTrack($"Track {trackGroup.Key + 1}");

            foreach (var clip in trackGroup.OrderBy(c => c.StartPosition))
            {
                var otioClip = new OTIOClip
                {
                    Name = clip.Name,
                    MediaPath = clip.FilePath,
                    TimelineStart = (long)(clip.StartPosition * samplesPerBeat),
                    Duration = (long)(clip.Length * samplesPerBeat),
                    SourceStart = (long)(clip.SourceOffset * samplesPerBeat),
                    Gain = clip.Gain,
                    FadeInDuration = (long)(clip.FadeInDuration * samplesPerBeat),
                    FadeOutDuration = (long)(clip.FadeOutDuration * samplesPerBeat)
                };

                track.Clips.Add(otioClip);
            }
        }

        ReportProgress(30, "Processing markers...");

        // Collect markers
        if (_options.IncludeMarkers)
        {
            foreach (var marker in arrangement.MarkerTrack.Markers)
            {
                _markers.Add(new OTIOMarker
                {
                    Name = marker.Name,
                    Position = (long)(marker.Position * samplesPerBeat),
                    Color = OTIOMarkerColor.Blue
                });
            }
        }

        ReportProgress(50, "Building OTIO structure...");

        // Build and write OTIO
        string json = BuildOTIOJson(frameRate);

        ReportProgress(80, "Writing file...");

        File.WriteAllText(outputPath, json, Encoding.UTF8);

        ReportProgress(100, "Export complete.");
    }

    /// <summary>
    /// Exports to OTIO and returns the JSON string.
    /// </summary>
    /// <param name="arrangement">Arrangement to export.</param>
    /// <returns>OTIO JSON string.</returns>
    public string ExportToString(Arrangement arrangement)
    {
        using var tempStream = new MemoryStream();
        var tempPath = Path.GetTempFileName();

        try
        {
            Export(arrangement, tempPath);
            return File.ReadAllText(tempPath);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private string BuildOTIOJson(double frameRate)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = _options.PrettyPrint
        });

        // Root timeline object
        writer.WriteStartObject();
        writer.WriteString("OTIO_SCHEMA", OTIOSchemaType.Timeline);
        writer.WriteString("name", _options.TimelineName);

        // Global start time
        WriteRationalTime(writer, "global_start_time", 0, frameRate);

        // Metadata
        writer.WriteStartObject("metadata");
        writer.WriteString("creator", "MusicEngine");
        writer.WriteString("created", DateTime.UtcNow.ToString("O"));
        writer.WriteEndObject();

        // Tracks (as a Stack)
        writer.WriteStartObject("tracks");
        writer.WriteString("OTIO_SCHEMA", OTIOSchemaType.Stack);
        writer.WriteString("name", "tracks");

        // Children (tracks)
        writer.WriteStartArray("children");

        foreach (var track in _tracks)
        {
            WriteTrack(writer, track, frameRate);
        }

        writer.WriteEndArray(); // children

        // Stack metadata
        writer.WriteStartObject("metadata");
        writer.WriteEndObject();

        writer.WriteEndObject(); // tracks

        // Timeline markers
        if (_markers.Count > 0)
        {
            writer.WriteStartArray("markers");
            foreach (var marker in _markers)
            {
                WriteMarker(writer, marker, frameRate);
            }
            writer.WriteEndArray();
        }

        writer.WriteEndObject(); // root

        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private void WriteTrack(Utf8JsonWriter writer, OTIOTrack track, double frameRate)
    {
        writer.WriteStartObject();
        writer.WriteString("OTIO_SCHEMA", OTIOSchemaType.Track);
        writer.WriteString("name", track.Name);
        writer.WriteString("kind", track.Kind);

        // Track children (clips with gaps)
        writer.WriteStartArray("children");

        // Sort clips by start time
        var sortedClips = track.Clips.OrderBy(c => c.TimelineStart).ToList();

        long currentPosition = 0;

        foreach (var clip in sortedClips)
        {
            // Insert gap if needed
            if (clip.TimelineStart > currentPosition)
            {
                long gapDuration = clip.TimelineStart - currentPosition;
                WriteGap(writer, gapDuration, frameRate);
            }

            WriteClip(writer, clip, frameRate);
            currentPosition = clip.TimelineStart + clip.Duration;
        }

        writer.WriteEndArray(); // children

        // Track metadata
        writer.WriteStartObject("metadata");
        foreach (var kvp in track.Metadata)
        {
            writer.WritePropertyName(kvp.Key);
            JsonSerializer.Serialize(writer, kvp.Value);
        }
        writer.WriteEndObject();

        writer.WriteEndObject(); // track
    }

    private void WriteClip(Utf8JsonWriter writer, OTIOClip clip, double frameRate)
    {
        writer.WriteStartObject();
        writer.WriteString("OTIO_SCHEMA", OTIOSchemaType.Clip);
        writer.WriteString("name", clip.Name);

        // Source range
        WriteTimeRange(writer, "source_range", clip.SourceStart, clip.Duration, frameRate);

        // Media reference
        if (clip.HasMedia)
        {
            writer.WriteStartObject("media_reference");
            writer.WriteString("OTIO_SCHEMA", OTIOSchemaType.ExternalReference);

            string targetUrl = clip.MediaPath;
            if (_options.UseRelativePaths && !string.IsNullOrEmpty(_options.MediaBasePath))
            {
                try
                {
                    var baseUri = new Uri(_options.MediaBasePath + Path.DirectorySeparatorChar);
                    var fullUri = new Uri(clip.MediaPath);
                    targetUrl = Uri.UnescapeDataString(baseUri.MakeRelativeUri(fullUri).ToString());
                }
                catch
                {
                    // Use absolute path
                }
            }

            writer.WriteString("target_url", targetUrl);

            // Available range
            if (clip.SourceDuration > 0)
            {
                WriteTimeRange(writer, "available_range", 0, clip.SourceDuration, frameRate);
            }

            writer.WriteStartObject("metadata");
            writer.WriteEndObject();

            writer.WriteEndObject(); // media_reference
        }
        else
        {
            // Missing reference
            writer.WriteStartObject("media_reference");
            writer.WriteString("OTIO_SCHEMA", OTIOSchemaType.MissingReference);
            writer.WriteString("name", clip.Name);
            writer.WriteStartObject("metadata");
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        // Effects
        if (_options.IncludeEffects)
        {
            writer.WriteStartArray("effects");

            // Gain effect
            if (Math.Abs(clip.Gain - 1.0) > 0.001)
            {
                writer.WriteStartObject();
                writer.WriteString("OTIO_SCHEMA", OTIOSchemaType.Effect);
                writer.WriteString("name", "Gain");
                writer.WriteString("effect_name", "AudioGain");
                writer.WriteStartObject("metadata");
                writer.WriteNumber("gain", clip.Gain);
                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            // Time warp effect
            if (Math.Abs(clip.TimeStretch - 1.0) > 0.001)
            {
                writer.WriteStartObject();
                writer.WriteString("OTIO_SCHEMA", OTIOSchemaType.LinearTimeWarp);
                writer.WriteString("name", "TimeStretch");
                writer.WriteString("effect_name", "LinearTimeWarp");
                writer.WriteNumber("time_scalar", clip.TimeStretch);
                writer.WriteStartObject("metadata");
                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        // Clip metadata
        writer.WriteStartObject("metadata");
        if (Math.Abs(clip.Pan) > 0.001)
        {
            writer.WriteNumber("pan", clip.Pan);
        }
        if (clip.FadeInDuration > 0)
        {
            writer.WriteNumber("fade_in_samples", clip.FadeInDuration);
        }
        if (clip.FadeOutDuration > 0)
        {
            writer.WriteNumber("fade_out_samples", clip.FadeOutDuration);
        }
        writer.WriteEndObject();

        writer.WriteEndObject(); // clip
    }

    private void WriteGap(Utf8JsonWriter writer, long duration, double frameRate)
    {
        writer.WriteStartObject();
        writer.WriteString("OTIO_SCHEMA", OTIOSchemaType.Gap);
        writer.WriteString("name", "Gap");
        WriteTimeRange(writer, "source_range", 0, duration, frameRate);
        writer.WriteStartObject("metadata");
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private void WriteMarker(Utf8JsonWriter writer, OTIOMarker marker, double frameRate)
    {
        writer.WriteStartObject();
        writer.WriteString("OTIO_SCHEMA", OTIOSchemaType.Marker);
        writer.WriteString("name", marker.Name);
        writer.WriteString("color", marker.Color);

        // Marked range
        WriteTimeRange(writer, "marked_range", marker.Position, Math.Max(1, marker.Duration), frameRate);

        // Metadata
        writer.WriteStartObject("metadata");
        if (!string.IsNullOrEmpty(marker.Comment))
        {
            writer.WriteString("comment", marker.Comment);
        }
        writer.WriteEndObject();

        writer.WriteEndObject();
    }

    private static void WriteRationalTime(Utf8JsonWriter writer, string propertyName, long value, double rate)
    {
        writer.WriteStartObject(propertyName);
        writer.WriteString("OTIO_SCHEMA", OTIOSchemaType.RationalTime);
        writer.WriteNumber("value", value);
        writer.WriteNumber("rate", rate);
        writer.WriteEndObject();
    }

    private static void WriteTimeRange(Utf8JsonWriter writer, string propertyName, long start, long duration, double rate)
    {
        writer.WriteStartObject(propertyName);
        writer.WriteString("OTIO_SCHEMA", OTIOSchemaType.TimeRange);

        writer.WriteStartObject("start_time");
        writer.WriteString("OTIO_SCHEMA", OTIOSchemaType.RationalTime);
        writer.WriteNumber("value", start);
        writer.WriteNumber("rate", rate);
        writer.WriteEndObject();

        writer.WriteStartObject("duration");
        writer.WriteString("OTIO_SCHEMA", OTIOSchemaType.RationalTime);
        writer.WriteNumber("value", duration);
        writer.WriteNumber("rate", rate);
        writer.WriteEndObject();

        writer.WriteEndObject();
    }

    private void ReportProgress(int percent, string message)
    {
        Progress?.Invoke(this, (percent, message));
    }

    /// <summary>
    /// Clears all tracks, clips, and markers.
    /// </summary>
    public void Clear()
    {
        _tracks.Clear();
        _markers.Clear();
    }
}
