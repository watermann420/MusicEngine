// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Global audio engine settings.

using MusicEngine.Core;

namespace MusicEngine.Core.AudioEncoding;

/// <summary>
/// Settings for audio encoding operations.
/// </summary>
public class EncoderSettings
{
    /// <summary>
    /// Gets or sets the target audio format.
    /// </summary>
    public AudioFormat Format { get; set; } = AudioFormat.Wav;

    /// <summary>
    /// Gets or sets the bit depth for PCM-based formats (16, 24, 32).
    /// For lossy formats, this is typically ignored.
    /// </summary>
    public int BitDepth { get; set; } = 16;

    /// <summary>
    /// Gets or sets the sample rate in Hz.
    /// </summary>
    public int SampleRate { get; set; } = 44100;

    /// <summary>
    /// Gets or sets the number of audio channels.
    /// </summary>
    public int Channels { get; set; } = 2;

    /// <summary>
    /// Gets or sets the quality level for lossy encoders (0.0 to 1.0).
    /// 0.0 = lowest quality/smallest file, 1.0 = highest quality/largest file.
    /// For FLAC, this affects compression level (not quality).
    /// </summary>
    public float Quality { get; set; } = 0.8f;

    /// <summary>
    /// Gets or sets the bitrate in kbps for lossy formats.
    /// If null, the encoder uses VBR based on Quality setting.
    /// </summary>
    public int? Bitrate { get; set; }

    /// <summary>
    /// Gets or sets whether to use variable bitrate encoding (for lossy formats).
    /// </summary>
    public bool UseVbr { get; set; } = true;

    /// <summary>
    /// Gets or sets the FLAC compression level (0-8, higher = more compression).
    /// Only used for FLAC encoding.
    /// </summary>
    public int FlacCompressionLevel { get; set; } = 5;

    /// <summary>
    /// Gets or sets metadata to embed in the output file.
    /// </summary>
    public AudioMetadata? Metadata { get; set; }

    /// <summary>
    /// Creates default encoder settings.
    /// </summary>
    public EncoderSettings() { }

    /// <summary>
    /// Creates encoder settings for a specific format.
    /// </summary>
    public EncoderSettings(AudioFormat format, int sampleRate = 44100, int channels = 2)
    {
        Format = format;
        SampleRate = sampleRate;
        Channels = channels;

        // Set reasonable defaults based on format
        switch (format)
        {
            case AudioFormat.Flac:
                BitDepth = 24;
                break;
            case AudioFormat.Ogg:
                Quality = 0.7f;
                Bitrate = 192;
                break;
            case AudioFormat.Mp3:
                Quality = 0.8f;
                Bitrate = 320;
                break;
            default:
                BitDepth = 16;
                break;
        }
    }

    /// <summary>
    /// Creates settings from a RecordingFormat enum value.
    /// </summary>
    public static EncoderSettings FromRecordingFormat(RecordingFormat format, int sampleRate = 44100, int channels = 2)
    {
        var settings = new EncoderSettings
        {
            SampleRate = sampleRate,
            Channels = channels
        };

        // Konfiguration basierend auf RecordingFormat
        switch (format)
        {
            case RecordingFormat.Wav16Bit:
                settings.Format = AudioFormat.Wav;
                settings.BitDepth = 16;
                break;
            case RecordingFormat.Wav24Bit:
                settings.Format = AudioFormat.Wav;
                settings.BitDepth = 24;
                break;
            case RecordingFormat.Wav32BitFloat:
                settings.Format = AudioFormat.Wav;
                settings.BitDepth = 32;
                break;
            case RecordingFormat.Mp3_128kbps:
                settings.Format = AudioFormat.Mp3;
                settings.Bitrate = 128;
                settings.UseVbr = false;
                break;
            case RecordingFormat.Mp3_192kbps:
                settings.Format = AudioFormat.Mp3;
                settings.Bitrate = 192;
                settings.UseVbr = false;
                break;
            case RecordingFormat.Mp3_320kbps:
                settings.Format = AudioFormat.Mp3;
                settings.Bitrate = 320;
                settings.UseVbr = false;
                break;
            case RecordingFormat.Flac16Bit:
                settings.Format = AudioFormat.Flac;
                settings.BitDepth = 16;
                break;
            case RecordingFormat.Flac24Bit:
                settings.Format = AudioFormat.Flac;
                settings.BitDepth = 24;
                break;
            case RecordingFormat.Ogg_96kbps:
                settings.Format = AudioFormat.Ogg;
                settings.Bitrate = 96;
                settings.Quality = 0.3f;
                break;
            case RecordingFormat.Ogg_128kbps:
                settings.Format = AudioFormat.Ogg;
                settings.Bitrate = 128;
                settings.Quality = 0.4f;
                break;
            case RecordingFormat.Ogg_192kbps:
                settings.Format = AudioFormat.Ogg;
                settings.Bitrate = 192;
                settings.Quality = 0.6f;
                break;
            case RecordingFormat.Ogg_320kbps:
                settings.Format = AudioFormat.Ogg;
                settings.Bitrate = 320;
                settings.Quality = 0.9f;
                break;
            case RecordingFormat.Aiff16Bit:
                settings.Format = AudioFormat.Aiff;
                settings.BitDepth = 16;
                break;
            case RecordingFormat.Aiff24Bit:
                settings.Format = AudioFormat.Aiff;
                settings.BitDepth = 24;
                break;
            case RecordingFormat.Aiff32BitFloat:
                settings.Format = AudioFormat.Aiff;
                settings.BitDepth = 32;
                break;
            default:
                settings.Format = AudioFormat.Wav;
                settings.BitDepth = 16;
                break;
        }

        return settings;
    }

    /// <summary>
    /// Creates settings from an ExportPreset.
    /// </summary>
    public static EncoderSettings FromExportPreset(ExportPreset preset)
    {
        var settings = new EncoderSettings
        {
            Format = preset.Format,
            SampleRate = preset.SampleRate,
            BitDepth = preset.BitDepth,
            Channels = 2 // Stereo default
        };

        if (preset.BitRate.HasValue)
        {
            settings.Bitrate = preset.BitRate.Value;
            settings.UseVbr = false;
        }

        return settings;
    }
}

/// <summary>
/// Metadata for audio files.
/// </summary>
public class AudioMetadata
{
    /// <summary>Track title</summary>
    public string? Title { get; set; }

    /// <summary>Artist name</summary>
    public string? Artist { get; set; }

    /// <summary>Album name</summary>
    public string? Album { get; set; }

    /// <summary>Year of release</summary>
    public int? Year { get; set; }

    /// <summary>Track number</summary>
    public int? TrackNumber { get; set; }

    /// <summary>Genre</summary>
    public string? Genre { get; set; }

    /// <summary>Comment</summary>
    public string? Comment { get; set; }
}
