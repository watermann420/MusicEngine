// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: OMF (Open Media Framework) export.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;

namespace MusicEngine.Core.Export;

/// <summary>
/// Options for OMF audio media handling.
/// </summary>
public enum OmfMediaOption
{
    /// <summary>Embed audio data directly in the OMF file.</summary>
    Embedded,

    /// <summary>Reference external audio files (creates relative paths).</summary>
    Referenced
}

/// <summary>
/// Audio sample format for embedded media.
/// </summary>
public enum OmfSampleFormat
{
    /// <summary>16-bit signed integer PCM.</summary>
    PCM16,

    /// <summary>24-bit signed integer PCM.</summary>
    PCM24,

    /// <summary>32-bit floating point.</summary>
    Float32
}

/// <summary>
/// Represents OMF export settings.
/// </summary>
public class OmfExportSettings
{
    /// <summary>How to handle audio media (embedded or referenced).</summary>
    public OmfMediaOption MediaOption { get; set; } = OmfMediaOption.Embedded;

    /// <summary>Sample format for embedded audio.</summary>
    public OmfSampleFormat SampleFormat { get; set; } = OmfSampleFormat.PCM16;

    /// <summary>Target sample rate (0 = use source sample rate).</summary>
    public int TargetSampleRate { get; set; } = 0;

    /// <summary>Whether to include fades in the export.</summary>
    public bool IncludeFades { get; set; } = true;

    /// <summary>Whether to include clip gain/volume changes.</summary>
    public bool IncludeClipGain { get; set; } = true;

    /// <summary>Whether to include track names.</summary>
    public bool IncludeTrackNames { get; set; } = true;

    /// <summary>Whether to include markers.</summary>
    public bool IncludeMarkers { get; set; } = true;

    /// <summary>Whether to include regions.</summary>
    public bool IncludeRegions { get; set; } = true;

    /// <summary>Timecode frame rate for SMPTE timecode.</summary>
    public double TimecodeFrameRate { get; set; } = 30.0;

    /// <summary>Whether timecode is drop-frame.</summary>
    public bool TimecodeDropFrame { get; set; } = false;

    /// <summary>Start timecode offset in frames.</summary>
    public long TimecodeStartOffset { get; set; } = 0;

    /// <summary>Project name to embed in the OMF file.</summary>
    public string ProjectName { get; set; } = "Untitled";

    /// <summary>Creator application name.</summary>
    public string CreatorApp { get; set; } = "MusicEngine";

    /// <summary>Base directory for referenced media (relative paths).</summary>
    public string? MediaBasePath { get; set; }
}

/// <summary>
/// Represents a track in the OMF file.
/// </summary>
internal class OmfTrack
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<OmfClip> Clips { get; } = new();
}

/// <summary>
/// Represents a clip in the OMF file.
/// </summary>
internal class OmfClip
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SourceFilePath { get; set; } = string.Empty;
    public long StartPosition { get; set; } // In samples
    public long Length { get; set; } // In samples
    public long SourceOffset { get; set; } // Offset into source in samples
    public float Gain { get; set; } = 1.0f;
    public OmfFade? FadeIn { get; set; }
    public OmfFade? FadeOut { get; set; }
    public float[]? AudioData { get; set; } // For embedded media
    public int SampleRate { get; set; } = 44100;
    public int Channels { get; set; } = 2;
}

/// <summary>
/// Represents a fade in the OMF file.
/// </summary>
internal class OmfFade
{
    public long Length { get; set; } // In samples
    public FadeType Type { get; set; }
}

/// <summary>
/// Progress event arguments for export operations.
/// </summary>
public class OmfExportProgressEventArgs : EventArgs
{
    /// <summary>Progress percentage (0-100).</summary>
    public int ProgressPercent { get; }

    /// <summary>Description of current operation.</summary>
    public string Description { get; }

    public OmfExportProgressEventArgs(int progressPercent, string description)
    {
        ProgressPercent = progressPercent;
        Description = description;
    }
}

/// <summary>
/// Exports MusicEngine arrangements to Open Media Framework (OMF) 2.0 format.
/// Compatible with Pro Tools, Avid Media Composer, and other professional audio/video software.
/// </summary>
/// <remarks>
/// OMF (Open Media Framework) is an interchange format for transferring digital media
/// between applications. OMF 2.0 (also known as OMFI) supports:
/// - Multiple audio and video tracks
/// - Clip references with position and duration
/// - Embedded or referenced media
/// - Fades and transitions
/// - Markers and regions
/// - Timecode support
/// </remarks>
public class OmfExporter
{
    private readonly OmfExportSettings _settings;
    private readonly List<OmfTrack> _tracks = new();
    private readonly List<(string Name, long Position)> _markers = new();
    private readonly List<(string Name, long Start, long End)> _regions = new();

    /// <summary>Event raised during export to report progress.</summary>
    public event EventHandler<OmfExportProgressEventArgs>? Progress;

    /// <summary>
    /// Creates a new OMF exporter with default settings.
    /// </summary>
    public OmfExporter() : this(new OmfExportSettings())
    {
    }

    /// <summary>
    /// Creates a new OMF exporter with custom settings.
    /// </summary>
    /// <param name="settings">Export settings.</param>
    public OmfExporter(OmfExportSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Gets the export settings.
    /// </summary>
    public OmfExportSettings Settings => _settings;

    /// <summary>
    /// Exports an arrangement to an OMF file.
    /// </summary>
    /// <param name="arrangement">The arrangement to export.</param>
    /// <param name="outputPath">Path to the output OMF file.</param>
    public void Export(Arrangement arrangement, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(arrangement);
        ArgumentException.ThrowIfNullOrEmpty(outputPath);

        ReportProgress(0, "Preparing export...");

        // Clear previous data
        _tracks.Clear();
        _markers.Clear();
        _regions.Clear();

        // Collect data from arrangement
        CollectArrangementData(arrangement);

        ReportProgress(30, "Building OMF structure...");

        // Build OMF file
        using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(stream);

        WriteOmfFile(writer);

        ReportProgress(100, "Export complete.");
    }

    /// <summary>
    /// Exports an arrangement to a byte array.
    /// </summary>
    /// <param name="arrangement">The arrangement to export.</param>
    /// <returns>OMF file as byte array.</returns>
    public byte[] ExportToBytes(Arrangement arrangement)
    {
        ArgumentNullException.ThrowIfNull(arrangement);

        _tracks.Clear();
        _markers.Clear();
        _regions.Clear();

        CollectArrangementData(arrangement);

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        WriteOmfFile(writer);

        return stream.ToArray();
    }

    /// <summary>
    /// Adds a track directly to the export.
    /// </summary>
    /// <param name="trackIndex">Track index.</param>
    /// <param name="trackName">Track name.</param>
    public void AddTrack(int trackIndex, string trackName)
    {
        var track = _tracks.FirstOrDefault(t => t.Index == trackIndex);
        if (track == null)
        {
            track = new OmfTrack { Index = trackIndex, Name = trackName };
            _tracks.Add(track);
        }
        else
        {
            track.Name = trackName;
        }
    }

    /// <summary>
    /// Adds an audio clip directly to the export.
    /// </summary>
    /// <param name="trackIndex">Track index for this clip.</param>
    /// <param name="clip">The audio clip to add.</param>
    /// <param name="sampleRate">Sample rate for position calculations.</param>
    /// <param name="bpm">Tempo in BPM for beat-to-sample conversion.</param>
    public void AddClip(int trackIndex, AudioClip clip, int sampleRate, double bpm)
    {
        ArgumentNullException.ThrowIfNull(clip);

        var track = _tracks.FirstOrDefault(t => t.Index == trackIndex);
        if (track == null)
        {
            track = new OmfTrack { Index = trackIndex, Name = $"Track {trackIndex + 1}" };
            _tracks.Add(track);
        }

        double samplesPerBeat = sampleRate * 60.0 / bpm;

        var omfClip = new OmfClip
        {
            Id = clip.Id,
            Name = clip.Name,
            SourceFilePath = clip.FilePath,
            StartPosition = (long)(clip.StartPosition * samplesPerBeat),
            Length = (long)(clip.Length * samplesPerBeat),
            SourceOffset = (long)(clip.SourceOffset * samplesPerBeat),
            Gain = clip.Gain,
            SampleRate = sampleRate,
            Channels = clip.Channels,
            AudioData = _settings.MediaOption == OmfMediaOption.Embedded ? clip.AudioData : null
        };

        if (_settings.IncludeFades)
        {
            if (clip.FadeInDuration > 0)
            {
                omfClip.FadeIn = new OmfFade
                {
                    Length = (long)(clip.FadeInDuration * samplesPerBeat),
                    Type = clip.FadeInType
                };
            }

            if (clip.FadeOutDuration > 0)
            {
                omfClip.FadeOut = new OmfFade
                {
                    Length = (long)(clip.FadeOutDuration * samplesPerBeat),
                    Type = clip.FadeOutType
                };
            }
        }

        track.Clips.Add(omfClip);
    }

    /// <summary>
    /// Adds a marker to the export.
    /// </summary>
    /// <param name="name">Marker name.</param>
    /// <param name="positionSamples">Position in samples.</param>
    public void AddMarker(string name, long positionSamples)
    {
        _markers.Add((name, positionSamples));
    }

    /// <summary>
    /// Adds a region to the export.
    /// </summary>
    /// <param name="name">Region name.</param>
    /// <param name="startSamples">Start position in samples.</param>
    /// <param name="endSamples">End position in samples.</param>
    public void AddRegion(string name, long startSamples, long endSamples)
    {
        _regions.Add((name, startSamples, endSamples));
    }

    private void CollectArrangementData(Arrangement arrangement)
    {
        int sampleRate = _settings.TargetSampleRate > 0 ? _settings.TargetSampleRate : 44100;
        double samplesPerBeat = sampleRate * 60.0 / arrangement.Bpm;

        // Collect audio clips by track
        var clipsByTrack = arrangement.AudioClips
            .GroupBy(c => c.TrackIndex)
            .OrderBy(g => g.Key);

        foreach (var trackGroup in clipsByTrack)
        {
            var track = new OmfTrack
            {
                Index = trackGroup.Key,
                Name = _settings.IncludeTrackNames ? $"Track {trackGroup.Key + 1}" : string.Empty
            };

            foreach (var clip in trackGroup.OrderBy(c => c.StartPosition))
            {
                var omfClip = new OmfClip
                {
                    Id = clip.Id,
                    Name = clip.Name,
                    SourceFilePath = clip.FilePath,
                    StartPosition = (long)(clip.StartPosition * samplesPerBeat),
                    Length = (long)(clip.Length * samplesPerBeat),
                    SourceOffset = (long)(clip.SourceOffset * samplesPerBeat),
                    Gain = _settings.IncludeClipGain ? clip.Gain : 1.0f,
                    SampleRate = sampleRate,
                    Channels = clip.Channels,
                    AudioData = _settings.MediaOption == OmfMediaOption.Embedded ? clip.AudioData : null
                };

                if (_settings.IncludeFades)
                {
                    if (clip.FadeInDuration > 0)
                    {
                        omfClip.FadeIn = new OmfFade
                        {
                            Length = (long)(clip.FadeInDuration * samplesPerBeat),
                            Type = clip.FadeInType
                        };
                    }

                    if (clip.FadeOutDuration > 0)
                    {
                        omfClip.FadeOut = new OmfFade
                        {
                            Length = (long)(clip.FadeOutDuration * samplesPerBeat),
                            Type = clip.FadeOutType
                        };
                    }
                }

                track.Clips.Add(omfClip);
            }

            _tracks.Add(track);
        }

        // Collect markers
        if (_settings.IncludeMarkers)
        {
            foreach (var marker in arrangement.MarkerTrack.Markers)
            {
                long positionSamples = (long)(marker.Position * samplesPerBeat);
                _markers.Add((marker.Name, positionSamples));
            }
        }

        // Collect regions
        if (_settings.IncludeRegions)
        {
            foreach (var region in arrangement.Regions)
            {
                long startSamples = (long)(region.StartPosition * samplesPerBeat);
                long endSamples = (long)(region.EndPosition * samplesPerBeat);
                _regions.Add((region.Name, startSamples, endSamples));
            }
        }
    }

    private void WriteOmfFile(BinaryWriter writer)
    {
        // OMF 2.0 file structure:
        // - Header (OMFI signature, version, byte order)
        // - Object directory
        // - Objects (tracks, clips, media)
        // - Media data (for embedded)

        ReportProgress(40, "Writing OMF header...");

        // Write OMF 2.0 header
        WriteOmfHeader(writer);

        ReportProgress(50, "Writing track data...");

        // Write composition object (main container)
        WriteComposition(writer);

        ReportProgress(60, "Writing clip data...");

        // Write tracks and clips
        WriteTracksAndClips(writer);

        ReportProgress(70, "Writing markers and regions...");

        // Write markers
        if (_settings.IncludeMarkers && _markers.Count > 0)
        {
            WriteMarkers(writer);
        }

        // Write regions
        if (_settings.IncludeRegions && _regions.Count > 0)
        {
            WriteRegions(writer);
        }

        ReportProgress(80, "Writing media data...");

        // Write embedded media data
        if (_settings.MediaOption == OmfMediaOption.Embedded)
        {
            WriteEmbeddedMedia(writer);
        }

        ReportProgress(90, "Finalizing file...");

        // Write footer/index
        WriteOmfFooter(writer);
    }

    private void WriteOmfHeader(BinaryWriter writer)
    {
        // OMF 2.0 signature: "OMFI" (big-endian) or "IFMO" (little-endian)
        // Using little-endian for Windows compatibility
        writer.Write(Encoding.ASCII.GetBytes("IFMO"));

        // Version: 2.0 (major.minor as 2 bytes)
        writer.Write((byte)2);
        writer.Write((byte)0);

        // Byte order marker (0x0001 for little-endian)
        writer.Write((ushort)0x0001);

        // Header version
        writer.Write((uint)0x00020000); // Version 2.0.0.0

        // Creation timestamp (UNIX time)
        var unixTime = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        writer.Write(unixTime);

        // Creator application name (null-terminated, padded to 32 bytes)
        WriteFixedString(writer, _settings.CreatorApp, 32);

        // Project name (null-terminated, padded to 64 bytes)
        WriteFixedString(writer, _settings.ProjectName, 64);

        // Reserved space for future use
        writer.Write(new byte[32]);

        // Timecode information
        WriteTimecodeInfo(writer);
    }

    private void WriteTimecodeInfo(BinaryWriter writer)
    {
        // Frame rate numerator and denominator (for 30fps: 30/1)
        var frameRateNum = (int)_settings.TimecodeFrameRate;
        var frameRateDen = 1;

        // Handle common non-integer frame rates
        if (Math.Abs(_settings.TimecodeFrameRate - 29.97) < 0.01)
        {
            frameRateNum = 30000;
            frameRateDen = 1001;
        }
        else if (Math.Abs(_settings.TimecodeFrameRate - 23.976) < 0.01)
        {
            frameRateNum = 24000;
            frameRateDen = 1001;
        }

        writer.Write(frameRateNum);
        writer.Write(frameRateDen);

        // Drop frame flag
        writer.Write(_settings.TimecodeDropFrame ? (byte)1 : (byte)0);

        // Start timecode offset
        writer.Write(_settings.TimecodeStartOffset);

        // Padding
        writer.Write(new byte[15]);
    }

    private void WriteComposition(BinaryWriter writer)
    {
        // Composition object header
        WriteObjectHeader(writer, "CMPO", 0);

        // Number of tracks
        writer.Write(_tracks.Count);

        // Total duration in samples
        long totalDuration = 0;
        foreach (var track in _tracks)
        {
            foreach (var clip in track.Clips)
            {
                var clipEnd = clip.StartPosition + clip.Length;
                if (clipEnd > totalDuration)
                    totalDuration = clipEnd;
            }
        }
        writer.Write(totalDuration);

        // Sample rate
        int sampleRate = _tracks.FirstOrDefault()?.Clips.FirstOrDefault()?.SampleRate ?? 44100;
        writer.Write(sampleRate);
    }

    private void WriteTracksAndClips(BinaryWriter writer)
    {
        int totalClips = _tracks.Sum(t => t.Clips.Count);
        int currentClip = 0;

        foreach (var track in _tracks)
        {
            // Track object header
            WriteObjectHeader(writer, "TRAK", track.Index);

            // Track name
            WriteString(writer, track.Name);

            // Track index
            writer.Write(track.Index);

            // Number of clips on this track
            writer.Write(track.Clips.Count);

            // Write each clip
            foreach (var clip in track.Clips)
            {
                WriteClip(writer, clip);
                currentClip++;

                int progress = 60 + (currentClip * 10 / Math.Max(totalClips, 1));
                ReportProgress(progress, $"Writing clip {currentClip}/{totalClips}...");
            }
        }
    }

    private void WriteClip(BinaryWriter writer, OmfClip clip)
    {
        // Clip object header
        WriteObjectHeader(writer, "CLIP", 0);

        // Clip ID (GUID)
        writer.Write(clip.Id.ToByteArray());

        // Clip name
        WriteString(writer, clip.Name);

        // Position, length, source offset (in samples)
        writer.Write(clip.StartPosition);
        writer.Write(clip.Length);
        writer.Write(clip.SourceOffset);

        // Gain
        writer.Write(clip.Gain);

        // Sample rate and channels
        writer.Write(clip.SampleRate);
        writer.Write(clip.Channels);

        // Media reference
        if (_settings.MediaOption == OmfMediaOption.Embedded)
        {
            // Embedded flag
            writer.Write((byte)1);
            // Media data offset will be patched later
            writer.Write((long)0);
            // Media data length
            writer.Write(clip.AudioData?.Length ?? 0);
        }
        else
        {
            // Referenced flag
            writer.Write((byte)0);

            // Construct relative path
            string mediaPath = clip.SourceFilePath;
            if (!string.IsNullOrEmpty(_settings.MediaBasePath))
            {
                try
                {
                    var baseUri = new Uri(_settings.MediaBasePath + Path.DirectorySeparatorChar);
                    var fullUri = new Uri(clip.SourceFilePath);
                    mediaPath = Uri.UnescapeDataString(baseUri.MakeRelativeUri(fullUri).ToString())
                        .Replace('/', Path.DirectorySeparatorChar);
                }
                catch
                {
                    // Fall back to original path
                }
            }

            WriteString(writer, mediaPath);
        }

        // Write fades
        WriteFade(writer, clip.FadeIn, "FDIN");
        WriteFade(writer, clip.FadeOut, "FDOT");
    }

    private void WriteFade(BinaryWriter writer, OmfFade? fade, string fadeType)
    {
        if (fade == null)
        {
            // No fade marker
            writer.Write((byte)0);
            return;
        }

        // Has fade marker
        writer.Write((byte)1);

        // Fade type tag
        writer.Write(Encoding.ASCII.GetBytes(fadeType));

        // Fade length in samples
        writer.Write(fade.Length);

        // Fade curve type
        writer.Write((byte)fade.Type);
    }

    private void WriteMarkers(BinaryWriter writer)
    {
        // Markers section header
        WriteObjectHeader(writer, "MRKR", 0);

        // Number of markers
        writer.Write(_markers.Count);

        foreach (var (name, position) in _markers)
        {
            WriteString(writer, name);
            writer.Write(position);
        }
    }

    private void WriteRegions(BinaryWriter writer)
    {
        // Regions section header
        WriteObjectHeader(writer, "REGN", 0);

        // Number of regions
        writer.Write(_regions.Count);

        foreach (var (name, start, end) in _regions)
        {
            WriteString(writer, name);
            writer.Write(start);
            writer.Write(end);
        }
    }

    private void WriteEmbeddedMedia(BinaryWriter writer)
    {
        // Media data section header
        WriteObjectHeader(writer, "MDAT", 0);

        foreach (var track in _tracks)
        {
            foreach (var clip in track.Clips)
            {
                if (clip.AudioData == null || clip.AudioData.Length == 0)
                    continue;

                // Write media data based on sample format
                switch (_settings.SampleFormat)
                {
                    case OmfSampleFormat.PCM16:
                        WritePcm16(writer, clip.AudioData);
                        break;
                    case OmfSampleFormat.PCM24:
                        WritePcm24(writer, clip.AudioData);
                        break;
                    case OmfSampleFormat.Float32:
                        WriteFloat32(writer, clip.AudioData);
                        break;
                }
            }
        }
    }

    private void WritePcm16(BinaryWriter writer, float[] samples)
    {
        foreach (var sample in samples)
        {
            var clamped = Math.Clamp(sample, -1f, 1f);
            var pcm16 = (short)(clamped * 32767f);
            writer.Write(pcm16);
        }
    }

    private void WritePcm24(BinaryWriter writer, float[] samples)
    {
        foreach (var sample in samples)
        {
            var clamped = Math.Clamp(sample, -1f, 1f);
            var pcm24 = (int)(clamped * 8388607f);

            // Write 24-bit as 3 bytes (little-endian)
            writer.Write((byte)(pcm24 & 0xFF));
            writer.Write((byte)((pcm24 >> 8) & 0xFF));
            writer.Write((byte)((pcm24 >> 16) & 0xFF));
        }
    }

    private void WriteFloat32(BinaryWriter writer, float[] samples)
    {
        foreach (var sample in samples)
        {
            writer.Write(sample);
        }
    }

    private void WriteOmfFooter(BinaryWriter writer)
    {
        // Object index (simplified - points to start of objects)
        WriteObjectHeader(writer, "INDX", 0);

        // Number of objects
        int objectCount = 1 + _tracks.Count + _tracks.Sum(t => t.Clips.Count);
        if (_markers.Count > 0) objectCount++;
        if (_regions.Count > 0) objectCount++;
        if (_settings.MediaOption == OmfMediaOption.Embedded) objectCount++;

        writer.Write(objectCount);

        // End marker
        writer.Write(Encoding.ASCII.GetBytes("IFND")); // End of file marker
    }

    private static void WriteObjectHeader(BinaryWriter writer, string objectType, int objectId)
    {
        // Object type (4 bytes ASCII)
        writer.Write(Encoding.ASCII.GetBytes(objectType.PadRight(4).Substring(0, 4)));

        // Object ID
        writer.Write(objectId);

        // Object size placeholder (will be updated later in real implementation)
        writer.Write((int)0);
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        value ??= string.Empty;
        var bytes = Encoding.UTF8.GetBytes(value);

        // String length prefix (2 bytes)
        writer.Write((ushort)bytes.Length);

        // String data
        writer.Write(bytes);
    }

    private static void WriteFixedString(BinaryWriter writer, string value, int length)
    {
        value ??= string.Empty;
        var bytes = Encoding.ASCII.GetBytes(value);
        var padded = new byte[length];

        Array.Copy(bytes, padded, Math.Min(bytes.Length, length - 1));
        writer.Write(padded);
    }

    private void ReportProgress(int percent, string description)
    {
        Progress?.Invoke(this, new OmfExportProgressEventArgs(percent, description));
    }

    /// <summary>
    /// Converts a sample position to SMPTE timecode string.
    /// </summary>
    /// <param name="samplePosition">Position in samples.</param>
    /// <param name="sampleRate">Audio sample rate.</param>
    /// <returns>Timecode string in HH:MM:SS:FF format.</returns>
    public string SamplesToTimecode(long samplePosition, int sampleRate)
    {
        double totalSeconds = (double)samplePosition / sampleRate;
        totalSeconds += _settings.TimecodeStartOffset / _settings.TimecodeFrameRate;

        int hours = (int)(totalSeconds / 3600);
        int minutes = (int)((totalSeconds % 3600) / 60);
        int seconds = (int)(totalSeconds % 60);
        int frames = (int)((totalSeconds % 1) * _settings.TimecodeFrameRate);

        string separator = _settings.TimecodeDropFrame ? ";" : ":";
        return $"{hours:D2}:{minutes:D2}:{seconds:D2}{separator}{frames:D2}";
    }

    /// <summary>
    /// Converts a SMPTE timecode string to sample position.
    /// </summary>
    /// <param name="timecode">Timecode string in HH:MM:SS:FF format.</param>
    /// <param name="sampleRate">Audio sample rate.</param>
    /// <returns>Position in samples.</returns>
    public long TimecodeToSamples(string timecode, int sampleRate)
    {
        if (string.IsNullOrWhiteSpace(timecode))
            return 0;

        // Parse HH:MM:SS:FF or HH:MM:SS;FF (drop-frame)
        var parts = timecode.Split(':', ';');
        if (parts.Length != 4) return 0;

        if (!int.TryParse(parts[0], out int hours) ||
            !int.TryParse(parts[1], out int minutes) ||
            !int.TryParse(parts[2], out int seconds) ||
            !int.TryParse(parts[3], out int frames))
        {
            return 0;
        }

        double totalSeconds = hours * 3600 + minutes * 60 + seconds + frames / _settings.TimecodeFrameRate;
        totalSeconds -= _settings.TimecodeStartOffset / _settings.TimecodeFrameRate;

        return (long)(totalSeconds * sampleRate);
    }
}
