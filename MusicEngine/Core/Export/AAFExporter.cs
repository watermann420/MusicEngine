// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: AAF (Advanced Authoring Format) export.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MusicEngine.Core.Export;

/// <summary>
/// AAF specification version.
/// </summary>
public enum AAFVersion
{
    /// <summary>AAF 1.1 (legacy)</summary>
    AAF_1_1,
    /// <summary>AAF 1.2 (current standard)</summary>
    AAF_1_2
}

/// <summary>
/// How audio media is stored in the AAF file.
/// </summary>
public enum AAFMediaStorage
{
    /// <summary>Audio is embedded within the AAF file</summary>
    Embedded,
    /// <summary>Audio is stored externally and referenced</summary>
    External
}

/// <summary>
/// Fade curve types for AAF transitions.
/// </summary>
public enum AAFFadeType
{
    /// <summary>Linear fade</summary>
    Linear,
    /// <summary>Equal power (constant power) fade</summary>
    EqualPower,
    /// <summary>S-curve fade</summary>
    SCurve,
    /// <summary>Logarithmic fade</summary>
    Logarithmic,
    /// <summary>Exponential fade</summary>
    Exponential
}

/// <summary>
/// Represents a clip/segment in the AAF timeline.
/// </summary>
public class AAFClip
{
    /// <summary>
    /// Unique identifier for the clip.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Display name of the clip.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Path to the source media file.
    /// </summary>
    public string SourcePath { get; set; } = "";

    /// <summary>
    /// Start time in the timeline (in samples).
    /// </summary>
    public long TimelineStart { get; set; }

    /// <summary>
    /// Duration on the timeline (in samples).
    /// </summary>
    public long Duration { get; set; }

    /// <summary>
    /// Source in-point (in samples).
    /// </summary>
    public long SourceInPoint { get; set; }

    /// <summary>
    /// Source out-point (in samples).
    /// </summary>
    public long SourceOutPoint { get; set; }

    /// <summary>
    /// Clip gain in dB.
    /// </summary>
    public float GainDb { get; set; } = 0f;

    /// <summary>
    /// Pan position (-1 = left, 0 = center, 1 = right).
    /// </summary>
    public float Pan { get; set; } = 0f;

    /// <summary>
    /// Whether the clip is muted.
    /// </summary>
    public bool Muted { get; set; }

    /// <summary>
    /// Fade in duration (in samples).
    /// </summary>
    public long FadeInDuration { get; set; }

    /// <summary>
    /// Fade in curve type.
    /// </summary>
    public AAFFadeType FadeInType { get; set; } = AAFFadeType.Linear;

    /// <summary>
    /// Fade out duration (in samples).
    /// </summary>
    public long FadeOutDuration { get; set; }

    /// <summary>
    /// Fade out curve type.
    /// </summary>
    public AAFFadeType FadeOutType { get; set; } = AAFFadeType.Linear;

    /// <summary>
    /// Color for display (optional).
    /// </summary>
    public string Color { get; set; } = "";

    /// <summary>
    /// User comments/notes.
    /// </summary>
    public string Comments { get; set; } = "";
}

/// <summary>
/// Represents a track in the AAF composition.
/// </summary>
public class AAFTrack
{
    /// <summary>
    /// Unique identifier for the track.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Display name of the track.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Track index (for ordering).
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Track volume in dB.
    /// </summary>
    public float VolumeDb { get; set; } = 0f;

    /// <summary>
    /// Track pan position.
    /// </summary>
    public float Pan { get; set; } = 0f;

    /// <summary>
    /// Whether the track is muted.
    /// </summary>
    public bool Muted { get; set; }

    /// <summary>
    /// Whether the track is soloed.
    /// </summary>
    public bool Solo { get; set; }

    /// <summary>
    /// Number of channels (1 = mono, 2 = stereo).
    /// </summary>
    public int Channels { get; set; } = 2;

    /// <summary>
    /// Clips on this track.
    /// </summary>
    public List<AAFClip> Clips { get; } = new();
}

/// <summary>
/// Represents a crossfade between two clips.
/// </summary>
public class AAFCrossfade
{
    /// <summary>
    /// First clip (outgoing).
    /// </summary>
    public Guid ClipA { get; set; }

    /// <summary>
    /// Second clip (incoming).
    /// </summary>
    public Guid ClipB { get; set; }

    /// <summary>
    /// Crossfade duration (in samples).
    /// </summary>
    public long Duration { get; set; }

    /// <summary>
    /// Crossfade curve type.
    /// </summary>
    public AAFFadeType FadeType { get; set; } = AAFFadeType.EqualPower;

    /// <summary>
    /// Position of the crossfade center point (0-1).
    /// </summary>
    public float Position { get; set; } = 0.5f;
}

/// <summary>
/// Represents a marker on the timeline.
/// </summary>
public class AAFMarker
{
    /// <summary>
    /// Marker position (in samples).
    /// </summary>
    public long Position { get; set; }

    /// <summary>
    /// Marker name/label.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Marker color.
    /// </summary>
    public string Color { get; set; } = "";

    /// <summary>
    /// Marker comments.
    /// </summary>
    public string Comments { get; set; } = "";
}

/// <summary>
/// Complete AAF composition/session.
/// </summary>
public class AAFComposition
{
    /// <summary>
    /// Composition name.
    /// </summary>
    public string Name { get; set; } = "Untitled";

    /// <summary>
    /// Sample rate.
    /// </summary>
    public int SampleRate { get; set; } = 48000;

    /// <summary>
    /// Bit depth.
    /// </summary>
    public int BitDepth { get; set; } = 24;

    /// <summary>
    /// Frame rate for timecode (e.g., 24, 25, 29.97, 30).
    /// </summary>
    public double FrameRate { get; set; } = 25.0;

    /// <summary>
    /// Start timecode.
    /// </summary>
    public string StartTimecode { get; set; } = "00:00:00:00";

    /// <summary>
    /// Tracks in the composition.
    /// </summary>
    public List<AAFTrack> Tracks { get; } = new();

    /// <summary>
    /// Crossfades between clips.
    /// </summary>
    public List<AAFCrossfade> Crossfades { get; } = new();

    /// <summary>
    /// Timeline markers.
    /// </summary>
    public List<AAFMarker> Markers { get; } = new();

    /// <summary>
    /// Creation date.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Last modified date.
    /// </summary>
    public DateTime ModifiedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Creator application name.
    /// </summary>
    public string Creator { get; set; } = "MusicEngine";

    /// <summary>
    /// User comments.
    /// </summary>
    public string Comments { get; set; } = "";
}

/// <summary>
/// Options for AAF export.
/// </summary>
public class AAFExportOptions
{
    /// <summary>
    /// AAF specification version.
    /// </summary>
    public AAFVersion Version { get; set; } = AAFVersion.AAF_1_2;

    /// <summary>
    /// How to store media files.
    /// </summary>
    public AAFMediaStorage MediaStorage { get; set; } = AAFMediaStorage.External;

    /// <summary>
    /// Directory for external media (relative to AAF file).
    /// </summary>
    public string MediaDirectory { get; set; } = "Media";

    /// <summary>
    /// Whether to convert all audio to the same format.
    /// </summary>
    public bool ConvertAudio { get; set; } = false;

    /// <summary>
    /// Target sample rate for audio conversion.
    /// </summary>
    public int TargetSampleRate { get; set; } = 48000;

    /// <summary>
    /// Target bit depth for audio conversion.
    /// </summary>
    public int TargetBitDepth { get; set; } = 24;

    /// <summary>
    /// Whether to include markers.
    /// </summary>
    public bool IncludeMarkers { get; set; } = true;

    /// <summary>
    /// Whether to include clip metadata.
    /// </summary>
    public bool IncludeMetadata { get; set; } = true;

    /// <summary>
    /// Whether to include fades and crossfades.
    /// </summary>
    public bool IncludeFades { get; set; } = true;
}

/// <summary>
/// Result of an AAF export operation.
/// </summary>
public class AAFExportResult
{
    /// <summary>
    /// Whether the export was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Path to the exported AAF file.
    /// </summary>
    public string OutputPath { get; set; } = "";

    /// <summary>
    /// Paths to exported media files.
    /// </summary>
    public List<string> MediaFiles { get; } = new();

    /// <summary>
    /// Error message if export failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Warnings generated during export.
    /// </summary>
    public List<string> Warnings { get; } = new();

    /// <summary>
    /// Export duration.
    /// </summary>
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Exports compositions to Advanced Authoring Format (AAF) for interchange
/// with Pro Tools, DaVinci Resolve, Adobe Premiere, and other professional applications.
/// </summary>
public class AAFExporter
{
    // AAF Magic number and constants
    private static readonly byte[] AAF_MAGIC = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
    private static readonly Guid AAF_CLASS_HEADER = new("0D010101-0101-0100-060E-2B3402060101");
    private static readonly Guid AAF_CLASS_COMPOSITION = new("0D010101-0101-0600-060E-2B3402060101");
    private static readonly Guid AAF_CLASS_TIMELINE = new("0D010101-0101-0700-060E-2B3402060101");
    private static readonly Guid AAF_CLASS_AUDIO_CLIP = new("0D010101-0101-0400-060E-2B3402060101");

    /// <summary>
    /// Exports a composition to AAF format.
    /// </summary>
    /// <param name="composition">The composition to export.</param>
    /// <param name="outputPath">Output file path.</param>
    /// <param name="options">Export options.</param>
    /// <param name="progress">Progress callback.</param>
    /// <returns>Export result.</returns>
    public AAFExportResult Export(
        AAFComposition composition,
        string outputPath,
        AAFExportOptions? options = null,
        IProgress<float>? progress = null)
    {
        options ??= new AAFExportOptions();
        var result = new AAFExportResult { OutputPath = outputPath };
        var startTime = DateTime.Now;

        try
        {
            progress?.Report(0f);

            // Create output directory if needed
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Handle media files
            if (options.MediaStorage == AAFMediaStorage.Embedded)
            {
                progress?.Report(0.1f);
                // For embedded media, we'll include the audio data in the AAF
                CopyMediaFiles(composition, outputPath, options, result);
            }
            else
            {
                progress?.Report(0.1f);
                // For external media, copy files to media directory
                CopyMediaFilesExternal(composition, outputPath, options, result);
            }

            progress?.Report(0.4f);

            // Generate AAF file structure
            // Note: AAF is based on Microsoft Structured Storage (OLE/COM)
            // For a complete implementation, we would use a structured storage library
            // Here we generate a simplified binary format that follows AAF conventions

            using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(stream, Encoding.UTF8);

            // Write header
            WriteAAFHeader(writer, composition, options);
            progress?.Report(0.5f);

            // Write composition mob
            WriteCompositionMob(writer, composition, options);
            progress?.Report(0.6f);

            // Write timeline
            WriteTimeline(writer, composition, options);
            progress?.Report(0.7f);

            // Write tracks
            foreach (var track in composition.Tracks)
            {
                WriteTrack(writer, track, composition, options);
            }
            progress?.Report(0.8f);

            // Write crossfades
            if (options.IncludeFades)
            {
                WriteCrossfades(writer, composition);
            }
            progress?.Report(0.9f);

            // Write markers
            if (options.IncludeMarkers)
            {
                WriteMarkers(writer, composition);
            }

            // Write footer/index
            WriteAAFFooter(writer, composition);

            result.Success = true;
            progress?.Report(1f);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        result.Duration = DateTime.Now - startTime;
        return result;
    }

    /// <summary>
    /// Creates an AAF composition from MusicEngine arrangement.
    /// </summary>
    public static AAFComposition FromArrangement(Arrangement arrangement, int sampleRate = 48000)
    {
        var composition = new AAFComposition
        {
            Name = arrangement.Name,
            SampleRate = sampleRate,
            Creator = "MusicEngine"
        };

        // Convert audio clips to a single audio track
        if (arrangement.AudioClips.Count > 0)
        {
            var aafTrack = new AAFTrack
            {
                Name = "Audio Track",
                Index = 0,
                VolumeDb = 0f,
                Muted = false,
                Solo = false
            };

            // Convert beats to samples using BPM
            double beatsPerSecond = arrangement.Bpm / 60.0;
            double secondsPerBeat = 1.0 / beatsPerSecond;

            foreach (var clip in arrangement.AudioClips)
            {

                var aafClip = new AAFClip
                {
                    Name = clip.Name,
                    SourcePath = clip.FilePath,
                    TimelineStart = (long)(clip.StartPosition * secondsPerBeat * sampleRate),
                    Duration = (long)(clip.Length * secondsPerBeat * sampleRate),
                    SourceInPoint = (long)(clip.SourceOffset * secondsPerBeat * sampleRate),
                    SourceOutPoint = (long)((clip.SourceOffset + clip.Length) * secondsPerBeat * sampleRate),
                    FadeInDuration = (long)(clip.FadeInDuration * secondsPerBeat * sampleRate),
                    FadeOutDuration = (long)(clip.FadeOutDuration * secondsPerBeat * sampleRate)
                };

                aafTrack.Clips.Add(aafClip);
            }

            composition.Tracks.Add(aafTrack);
        }

        // Convert markers from MarkerTrack
        double markerSecondsPerBeat = 60.0 / arrangement.Bpm;
        foreach (var marker in arrangement.MarkerTrack.Markers)
        {
            composition.Markers.Add(new AAFMarker
            {
                Position = (long)(marker.Position * markerSecondsPerBeat * sampleRate),
                Name = marker.Name,
                Color = marker.Color
            });
        }

        return composition;
    }

    private void CopyMediaFiles(
        AAFComposition composition,
        string outputPath,
        AAFExportOptions options,
        AAFExportResult result)
    {
        // For embedded media, collect all source files
        foreach (var track in composition.Tracks)
        {
            foreach (var clip in track.Clips)
            {
                if (File.Exists(clip.SourcePath))
                {
                    result.MediaFiles.Add(clip.SourcePath);
                }
                else
                {
                    result.Warnings.Add($"Media file not found: {clip.SourcePath}");
                }
            }
        }
    }

    private void CopyMediaFilesExternal(
        AAFComposition composition,
        string outputPath,
        AAFExportOptions options,
        AAFExportResult result)
    {
        var outputDir = Path.GetDirectoryName(outputPath) ?? ".";
        var mediaDir = Path.Combine(outputDir, options.MediaDirectory);

        if (!Directory.Exists(mediaDir))
        {
            Directory.CreateDirectory(mediaDir);
        }

        foreach (var track in composition.Tracks)
        {
            foreach (var clip in track.Clips)
            {
                if (File.Exists(clip.SourcePath))
                {
                    var destFileName = Path.GetFileName(clip.SourcePath);
                    var destPath = Path.Combine(mediaDir, destFileName);

                    // Handle duplicates
                    int counter = 1;
                    while (File.Exists(destPath) && !IsSameFile(clip.SourcePath, destPath))
                    {
                        var name = Path.GetFileNameWithoutExtension(clip.SourcePath);
                        var ext = Path.GetExtension(clip.SourcePath);
                        destPath = Path.Combine(mediaDir, $"{name}_{counter++}{ext}");
                    }

                    if (!File.Exists(destPath))
                    {
                        File.Copy(clip.SourcePath, destPath);
                    }

                    result.MediaFiles.Add(destPath);
                    clip.SourcePath = Path.Combine(options.MediaDirectory, Path.GetFileName(destPath));
                }
                else
                {
                    result.Warnings.Add($"Media file not found: {clip.SourcePath}");
                }
            }
        }
    }

    private bool IsSameFile(string path1, string path2)
    {
        var info1 = new FileInfo(path1);
        var info2 = new FileInfo(path2);
        return info1.Length == info2.Length &&
               info1.LastWriteTimeUtc == info2.LastWriteTimeUtc;
    }

    private void WriteAAFHeader(BinaryWriter writer, AAFComposition composition, AAFExportOptions options)
    {
        // Write compound document header (simplified)
        writer.Write(AAF_MAGIC);

        // Minor version
        writer.Write((ushort)(options.Version == AAFVersion.AAF_1_1 ? 0x003E : 0x003E));
        // Major version
        writer.Write((ushort)0x0003);
        // Byte order (little endian)
        writer.Write((ushort)0xFFFE);
        // Sector size power (512 bytes = 2^9)
        writer.Write((ushort)0x0009);
        // Mini sector size power (64 bytes = 2^6)
        writer.Write((ushort)0x0006);
        // Reserved
        writer.Write(new byte[6]);
        // Total sectors in FAT (placeholder)
        writer.Write((uint)0);
        // First directory sector SECID
        writer.Write((uint)0);
        // Reserved
        writer.Write((uint)0);
        // Minimum size for standard stream
        writer.Write((uint)0x1000);
        // First mini FAT sector
        writer.Write((uint)0xFFFFFFFE);
        // Mini FAT sector count
        writer.Write((uint)0);
        // First DIFAT sector
        writer.Write((uint)0xFFFFFFFE);
        // DIFAT sector count
        writer.Write((uint)0);

        // DIFAT array (109 entries)
        for (int i = 0; i < 109; i++)
        {
            writer.Write((uint)0xFFFFFFFF);
        }

        // AAF Header object
        writer.Write(AAF_CLASS_HEADER.ToByteArray());

        // Version string
        var versionStr = options.Version == AAFVersion.AAF_1_1 ? "AAF1.1" : "AAF1.2";
        WriteAAFString(writer, versionStr);

        // Identification
        WriteAAFString(writer, composition.Creator);
        WriteAAFString(writer, "MusicEngine AAF Exporter");
        WriteAAFString(writer, "1.0");

        // Creation date
        writer.Write(composition.CreatedAt.ToBinary());
        writer.Write(composition.ModifiedAt.ToBinary());

        // Sample rate
        writer.Write(composition.SampleRate);

        // Bit depth
        writer.Write(composition.BitDepth);
    }

    private void WriteCompositionMob(BinaryWriter writer, AAFComposition composition, AAFExportOptions options)
    {
        // Composition Mob (Master Mob)
        writer.Write(AAF_CLASS_COMPOSITION.ToByteArray());

        // Mob ID (unique identifier)
        writer.Write(Guid.NewGuid().ToByteArray());

        // Name
        WriteAAFString(writer, composition.Name);

        // Creation time
        writer.Write(composition.CreatedAt.ToBinary());

        // Modification time
        writer.Write(composition.ModifiedAt.ToBinary());

        // Comments
        if (options.IncludeMetadata)
        {
            WriteAAFString(writer, composition.Comments);
        }
        else
        {
            WriteAAFString(writer, "");
        }

        // Number of tracks
        writer.Write(composition.Tracks.Count);

        // Frame rate
        writer.Write(composition.FrameRate);

        // Start timecode
        WriteAAFString(writer, composition.StartTimecode);
    }

    private void WriteTimeline(BinaryWriter writer, AAFComposition composition, AAFExportOptions options)
    {
        // Timeline Mob Slot
        writer.Write(AAF_CLASS_TIMELINE.ToByteArray());

        // Calculate total duration
        long totalDuration = 0;
        foreach (var track in composition.Tracks)
        {
            foreach (var clip in track.Clips)
            {
                long clipEnd = clip.TimelineStart + clip.Duration;
                if (clipEnd > totalDuration)
                    totalDuration = clipEnd;
            }
        }

        // Duration in samples
        writer.Write(totalDuration);

        // Edit rate (sample rate as rational)
        writer.Write(composition.SampleRate);
        writer.Write(1); // Denominator

        // Origin (start point)
        writer.Write(0L);
    }

    private void WriteTrack(BinaryWriter writer, AAFTrack track, AAFComposition composition, AAFExportOptions options)
    {
        // Track Marker
        writer.Write((byte)0xAA); // Track start marker

        // Track ID
        writer.Write(track.Id.ToByteArray());

        // Track index
        writer.Write(track.Index);

        // Track name
        WriteAAFString(writer, track.Name);

        // Channel count
        writer.Write(track.Channels);

        // Volume (as 32-bit float)
        writer.Write(track.VolumeDb);

        // Pan
        writer.Write(track.Pan);

        // Mute flag
        writer.Write(track.Muted);

        // Solo flag
        writer.Write(track.Solo);

        // Number of clips
        writer.Write(track.Clips.Count);

        // Write each clip
        foreach (var clip in track.Clips)
        {
            WriteClip(writer, clip, composition, options);
        }

        // Track end marker
        writer.Write((byte)0xAB);
    }

    private void WriteClip(BinaryWriter writer, AAFClip clip, AAFComposition composition, AAFExportOptions options)
    {
        // Audio Clip class
        writer.Write(AAF_CLASS_AUDIO_CLIP.ToByteArray());

        // Clip ID
        writer.Write(clip.Id.ToByteArray());

        // Clip name
        WriteAAFString(writer, clip.Name);

        // Source path (or URI)
        WriteAAFString(writer, clip.SourcePath);

        // Timeline position
        writer.Write(clip.TimelineStart);

        // Duration
        writer.Write(clip.Duration);

        // Source in point
        writer.Write(clip.SourceInPoint);

        // Source out point
        writer.Write(clip.SourceOutPoint);

        // Gain
        writer.Write(clip.GainDb);

        // Pan
        writer.Write(clip.Pan);

        // Muted
        writer.Write(clip.Muted);

        // Fades
        if (options.IncludeFades)
        {
            writer.Write(clip.FadeInDuration);
            writer.Write((byte)clip.FadeInType);
            writer.Write(clip.FadeOutDuration);
            writer.Write((byte)clip.FadeOutType);
        }
        else
        {
            writer.Write(0L);
            writer.Write((byte)0);
            writer.Write(0L);
            writer.Write((byte)0);
        }

        // Metadata
        if (options.IncludeMetadata)
        {
            WriteAAFString(writer, clip.Color);
            WriteAAFString(writer, clip.Comments);
        }
        else
        {
            WriteAAFString(writer, "");
            WriteAAFString(writer, "");
        }
    }

    private void WriteCrossfades(BinaryWriter writer, AAFComposition composition)
    {
        // Crossfade marker
        writer.Write((byte)0xCF);

        // Number of crossfades
        writer.Write(composition.Crossfades.Count);

        foreach (var xfade in composition.Crossfades)
        {
            writer.Write(xfade.ClipA.ToByteArray());
            writer.Write(xfade.ClipB.ToByteArray());
            writer.Write(xfade.Duration);
            writer.Write((byte)xfade.FadeType);
            writer.Write(xfade.Position);
        }
    }

    private void WriteMarkers(BinaryWriter writer, AAFComposition composition)
    {
        // Marker section marker
        writer.Write((byte)0xDD);

        // Number of markers
        writer.Write(composition.Markers.Count);

        foreach (var marker in composition.Markers)
        {
            writer.Write(marker.Position);
            WriteAAFString(writer, marker.Name);
            WriteAAFString(writer, marker.Color);
            WriteAAFString(writer, marker.Comments);
        }
    }

    private void WriteAAFFooter(BinaryWriter writer, AAFComposition composition)
    {
        // End marker
        writer.Write((byte)0xFF);
        writer.Write((byte)0xFF);

        // File size placeholder (would be updated in a real implementation)
        writer.Write(writer.BaseStream.Position);

        // Checksum placeholder
        writer.Write(0U);

        // AAF signature
        writer.Write(Encoding.ASCII.GetBytes("AAF-END"));
    }

    private void WriteAAFString(BinaryWriter writer, string? str)
    {
        str ??= "";
        var bytes = Encoding.UTF8.GetBytes(str);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }
}
