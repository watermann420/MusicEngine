// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

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
    Mp3_320kbps,

    /// <summary>
    /// FLAC format with 16-bit samples.
    /// Lossless compression with typical 50-70% file size reduction.
    /// Requires NAudio.Flac package.
    /// </summary>
    Flac16Bit,

    /// <summary>
    /// FLAC format with 24-bit samples.
    /// Lossless compression, professional quality.
    /// Requires NAudio.Flac package.
    /// </summary>
    Flac24Bit,

    /// <summary>
    /// OGG Vorbis format at 96 kbps.
    /// Low quality lossy compression, very small files.
    /// Requires OggVorbisEncoder package.
    /// </summary>
    Ogg_96kbps,

    /// <summary>
    /// OGG Vorbis format at 128 kbps.
    /// Medium-low quality lossy compression.
    /// Requires OggVorbisEncoder package.
    /// </summary>
    Ogg_128kbps,

    /// <summary>
    /// OGG Vorbis format at 192 kbps.
    /// Good quality lossy compression.
    /// Requires OggVorbisEncoder package.
    /// </summary>
    Ogg_192kbps,

    /// <summary>
    /// OGG Vorbis format at 320 kbps.
    /// High quality lossy compression.
    /// Requires OggVorbisEncoder package.
    /// </summary>
    Ogg_320kbps,

    /// <summary>
    /// AIFF format with 16-bit integer samples.
    /// Uncompressed format, compatible with Apple/Mac systems.
    /// </summary>
    Aiff16Bit,

    /// <summary>
    /// AIFF format with 24-bit integer samples.
    /// Uncompressed, professional quality.
    /// </summary>
    Aiff24Bit,

    /// <summary>
    /// AIFF format with 32-bit floating point samples.
    /// Uncompressed, maximum precision.
    /// </summary>
    Aiff32BitFloat
}

/// <summary>
/// Extension methods for RecordingFormat enum.
/// </summary>
public static class RecordingFormatExtensions
{
    /// <summary>
    /// Gets the bit depth for PCM-based formats (WAV, FLAC, AIFF).
    /// </summary>
    /// <param name="format">The recording format.</param>
    /// <returns>Bit depth (16, 24, or 32), or 0 for lossy formats.</returns>
    public static int GetBitDepth(this RecordingFormat format) => format switch
    {
        RecordingFormat.Wav16Bit => 16,
        RecordingFormat.Wav24Bit => 24,
        RecordingFormat.Wav32BitFloat => 32,
        RecordingFormat.Flac16Bit => 16,
        RecordingFormat.Flac24Bit => 24,
        RecordingFormat.Aiff16Bit => 16,
        RecordingFormat.Aiff24Bit => 24,
        RecordingFormat.Aiff32BitFloat => 32,
        _ => 0
    };

    /// <summary>
    /// Gets the bitrate in kbps for lossy formats (MP3, OGG).
    /// </summary>
    /// <param name="format">The recording format.</param>
    /// <returns>Bitrate in kbps, or 0 for lossless formats.</returns>
    public static int GetBitrate(this RecordingFormat format) => format switch
    {
        RecordingFormat.Mp3_128kbps => 128,
        RecordingFormat.Mp3_192kbps => 192,
        RecordingFormat.Mp3_320kbps => 320,
        RecordingFormat.Ogg_96kbps => 96,
        RecordingFormat.Ogg_128kbps => 128,
        RecordingFormat.Ogg_192kbps => 192,
        RecordingFormat.Ogg_320kbps => 320,
        _ => 0
    };

    /// <summary>
    /// Gets the MP3 bitrate in kbps.
    /// </summary>
    /// <param name="format">The recording format.</param>
    /// <returns>Bitrate in kbps, or 0 for non-MP3 formats.</returns>
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
    /// <returns>True if WAV format.</returns>
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
    /// <returns>True if MP3 format.</returns>
    public static bool IsMp3Format(this RecordingFormat format) => format switch
    {
        RecordingFormat.Mp3_128kbps => true,
        RecordingFormat.Mp3_192kbps => true,
        RecordingFormat.Mp3_320kbps => true,
        _ => false
    };

    /// <summary>
    /// Gets whether the format is a FLAC format.
    /// </summary>
    /// <param name="format">The recording format.</param>
    /// <returns>True if FLAC format.</returns>
    public static bool IsFlacFormat(this RecordingFormat format) => format switch
    {
        RecordingFormat.Flac16Bit => true,
        RecordingFormat.Flac24Bit => true,
        _ => false
    };

    /// <summary>
    /// Gets whether the format is an OGG Vorbis format.
    /// </summary>
    /// <param name="format">The recording format.</param>
    /// <returns>True if OGG format.</returns>
    public static bool IsOggFormat(this RecordingFormat format) => format switch
    {
        RecordingFormat.Ogg_96kbps => true,
        RecordingFormat.Ogg_128kbps => true,
        RecordingFormat.Ogg_192kbps => true,
        RecordingFormat.Ogg_320kbps => true,
        _ => false
    };

    /// <summary>
    /// Gets whether the format is an AIFF format.
    /// </summary>
    /// <param name="format">The recording format.</param>
    /// <returns>True if AIFF format.</returns>
    public static bool IsAiffFormat(this RecordingFormat format) => format switch
    {
        RecordingFormat.Aiff16Bit => true,
        RecordingFormat.Aiff24Bit => true,
        RecordingFormat.Aiff32BitFloat => true,
        _ => false
    };

    /// <summary>
    /// Gets whether the format is a lossless format (WAV, FLAC, AIFF).
    /// </summary>
    /// <param name="format">The recording format.</param>
    /// <returns>True if lossless format.</returns>
    public static bool IsLossless(this RecordingFormat format) =>
        format.IsWavFormat() || format.IsFlacFormat() || format.IsAiffFormat();

    /// <summary>
    /// Gets whether the format is a lossy format (MP3, OGG).
    /// </summary>
    /// <param name="format">The recording format.</param>
    /// <returns>True if lossy format.</returns>
    public static bool IsLossy(this RecordingFormat format) =>
        format.IsMp3Format() || format.IsOggFormat();

    /// <summary>
    /// Gets whether the format uses floating point samples.
    /// </summary>
    /// <param name="format">The recording format.</param>
    /// <returns>True if 32-bit float format.</returns>
    public static bool IsFloatFormat(this RecordingFormat format) => format switch
    {
        RecordingFormat.Wav32BitFloat => true,
        RecordingFormat.Aiff32BitFloat => true,
        _ => false
    };

    /// <summary>
    /// Gets the file extension for the format.
    /// </summary>
    /// <param name="format">The recording format.</param>
    /// <returns>File extension including the dot (e.g., ".wav", ".mp3", ".flac").</returns>
    public static string GetFileExtension(this RecordingFormat format) => format switch
    {
        RecordingFormat.Wav16Bit => ".wav",
        RecordingFormat.Wav24Bit => ".wav",
        RecordingFormat.Wav32BitFloat => ".wav",
        RecordingFormat.Mp3_128kbps => ".mp3",
        RecordingFormat.Mp3_192kbps => ".mp3",
        RecordingFormat.Mp3_320kbps => ".mp3",
        RecordingFormat.Flac16Bit => ".flac",
        RecordingFormat.Flac24Bit => ".flac",
        RecordingFormat.Ogg_96kbps => ".ogg",
        RecordingFormat.Ogg_128kbps => ".ogg",
        RecordingFormat.Ogg_192kbps => ".ogg",
        RecordingFormat.Ogg_320kbps => ".ogg",
        RecordingFormat.Aiff16Bit => ".aiff",
        RecordingFormat.Aiff24Bit => ".aiff",
        RecordingFormat.Aiff32BitFloat => ".aiff",
        _ => ".wav"
    };

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
        RecordingFormat.Flac16Bit => "FLAC 16-bit Lossless",
        RecordingFormat.Flac24Bit => "FLAC 24-bit Lossless",
        RecordingFormat.Ogg_96kbps => "OGG Vorbis 96 kbps",
        RecordingFormat.Ogg_128kbps => "OGG Vorbis 128 kbps",
        RecordingFormat.Ogg_192kbps => "OGG Vorbis 192 kbps",
        RecordingFormat.Ogg_320kbps => "OGG Vorbis 320 kbps",
        RecordingFormat.Aiff16Bit => "AIFF 16-bit PCM",
        RecordingFormat.Aiff24Bit => "AIFF 24-bit PCM",
        RecordingFormat.Aiff32BitFloat => "AIFF 32-bit Float",
        _ => "Unknown"
    };

    /// <summary>
    /// Gets the corresponding AudioFormat for this RecordingFormat.
    /// </summary>
    /// <param name="format">The recording format.</param>
    /// <returns>The AudioFormat enum value.</returns>
    public static AudioFormat ToAudioFormat(this RecordingFormat format) => format switch
    {
        RecordingFormat.Wav16Bit => AudioFormat.Wav,
        RecordingFormat.Wav24Bit => AudioFormat.Wav,
        RecordingFormat.Wav32BitFloat => AudioFormat.Wav,
        RecordingFormat.Mp3_128kbps => AudioFormat.Mp3,
        RecordingFormat.Mp3_192kbps => AudioFormat.Mp3,
        RecordingFormat.Mp3_320kbps => AudioFormat.Mp3,
        RecordingFormat.Flac16Bit => AudioFormat.Flac,
        RecordingFormat.Flac24Bit => AudioFormat.Flac,
        RecordingFormat.Ogg_96kbps => AudioFormat.Ogg,
        RecordingFormat.Ogg_128kbps => AudioFormat.Ogg,
        RecordingFormat.Ogg_192kbps => AudioFormat.Ogg,
        RecordingFormat.Ogg_320kbps => AudioFormat.Ogg,
        RecordingFormat.Aiff16Bit => AudioFormat.Aiff,
        RecordingFormat.Aiff24Bit => AudioFormat.Aiff,
        RecordingFormat.Aiff32BitFloat => AudioFormat.Aiff,
        _ => AudioFormat.Wav
    };
}
