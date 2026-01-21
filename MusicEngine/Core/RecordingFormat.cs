//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Enum defining supported recording output formats.

namespace MusicEngine.Core;

/// <summary>
/// Specifies the output format for audio recording.
/// </summary>
public enum RecordingFormat
{
    /// <summary>
    /// WAV format with 16-bit integer samples.
    /// Standard CD quality bit depth.
    /// </summary>
    Wav16Bit,

    /// <summary>
    /// WAV format with 24-bit integer samples.
    /// Professional audio quality with higher dynamic range.
    /// </summary>
    Wav24Bit,

    /// <summary>
    /// WAV format with 32-bit floating point samples.
    /// Maximum precision for further processing.
    /// </summary>
    Wav32BitFloat,

    /// <summary>
    /// MP3 format at 128 kbps.
    /// Lower quality, smaller file size.
    /// Requires NAudio.Lame package.
    /// </summary>
    Mp3_128kbps,

    /// <summary>
    /// MP3 format at 192 kbps.
    /// Medium quality, balanced file size.
    /// Requires NAudio.Lame package.
    /// </summary>
    Mp3_192kbps,

    /// <summary>
    /// MP3 format at 320 kbps.
    /// High quality MP3, larger file size.
    /// Requires NAudio.Lame package.
    /// </summary>
    Mp3_320kbps
}

/// <summary>
/// Extension methods for RecordingFormat enum.
/// </summary>
public static class RecordingFormatExtensions
{
    /// <summary>
    /// Gets the bit depth for WAV formats.
    /// </summary>
    /// <param name="format">The recording format.</param>
    /// <returns>Bit depth (16, 24, or 32), or 0 for MP3 formats.</returns>
    public static int GetBitDepth(this RecordingFormat format) => format switch
    {
        RecordingFormat.Wav16Bit => 16,
        RecordingFormat.Wav24Bit => 24,
        RecordingFormat.Wav32BitFloat => 32,
        _ => 0
    };

    /// <summary>
    /// Gets the MP3 bitrate in kbps.
    /// </summary>
    /// <param name="format">The recording format.</param>
    /// <returns>Bitrate in kbps, or 0 for WAV formats.</returns>
    public static int GetMp3Bitrate(this RecordingFormat format) => format switch
    {
        RecordingFormat.Mp3_128kbps => 128,
        RecordingFormat.Mp3_192kbps => 192,
        RecordingFormat.Mp3_320kbps => 320,
        _ => 0
    };

    /// <summary>
    /// Gets whether the format is a WAV format.
    /// </summary>
    /// <param name="format">The recording format.</param>
    /// <returns>True if WAV format, false if MP3.</returns>
    public static bool IsWavFormat(this RecordingFormat format) => format switch
    {
        RecordingFormat.Wav16Bit => true,
        RecordingFormat.Wav24Bit => true,
        RecordingFormat.Wav32BitFloat => true,
        _ => false
    };

    /// <summary>
    /// Gets whether the format is an MP3 format.
    /// </summary>
    /// <param name="format">The recording format.</param>
    /// <returns>True if MP3 format, false if WAV.</returns>
    public static bool IsMp3Format(this RecordingFormat format) => !format.IsWavFormat();

    /// <summary>
    /// Gets whether the format uses floating point samples.
    /// </summary>
    /// <param name="format">The recording format.</param>
    /// <returns>True if 32-bit float format.</returns>
    public static bool IsFloatFormat(this RecordingFormat format) => format == RecordingFormat.Wav32BitFloat;

    /// <summary>
    /// Gets the file extension for the format.
    /// </summary>
    /// <param name="format">The recording format.</param>
    /// <returns>File extension including the dot (e.g., ".wav" or ".mp3").</returns>
    public static string GetFileExtension(this RecordingFormat format) =>
        format.IsWavFormat() ? ".wav" : ".mp3";

    /// <summary>
    /// Gets a human-readable description of the format.
    /// </summary>
    /// <param name="format">The recording format.</param>
    /// <returns>Description string.</returns>
    public static string GetDescription(this RecordingFormat format) => format switch
    {
        RecordingFormat.Wav16Bit => "WAV 16-bit PCM",
        RecordingFormat.Wav24Bit => "WAV 24-bit PCM",
        RecordingFormat.Wav32BitFloat => "WAV 32-bit Float",
        RecordingFormat.Mp3_128kbps => "MP3 128 kbps",
        RecordingFormat.Mp3_192kbps => "MP3 192 kbps",
        RecordingFormat.Mp3_320kbps => "MP3 320 kbps",
        _ => "Unknown"
    };
}
