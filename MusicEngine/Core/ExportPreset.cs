//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Export preset definitions for various platforms (YouTube, Spotify, etc.).

using System;

namespace MusicEngine.Core;

/// <summary>
/// Supported audio export formats.
/// </summary>
public enum AudioFormat
{
    /// <summary>WAV - Uncompressed PCM audio</summary>
    Wav,
    /// <summary>MP3 - Lossy compressed audio (requires NAudio.Lame)</summary>
    Mp3,
    /// <summary>FLAC - Lossless compressed audio</summary>
    Flac,
    /// <summary>OGG Vorbis - Lossy compressed audio</summary>
    Ogg
}

/// <summary>
/// Represents an audio export preset with format, quality, and loudness normalization settings.
/// Designed for platform-specific requirements (YouTube, Spotify, etc.).
/// </summary>
/// <param name="Name">Display name of the preset</param>
/// <param name="Description">Description of the preset and its intended use</param>
/// <param name="Format">Audio format (WAV, MP3, FLAC, OGG)</param>
/// <param name="SampleRate">Sample rate in Hz (e.g., 44100, 48000, 96000)</param>
/// <param name="BitDepth">Bit depth for PCM formats (16, 24, 32)</param>
/// <param name="BitRate">Bit rate in kbps for lossy formats (null for lossless)</param>
/// <param name="TargetLufs">Target integrated loudness in LUFS (null for no normalization)</param>
/// <param name="MaxTruePeak">Maximum true peak in dBTP (null for no limiting)</param>
/// <param name="NormalizeLoudness">Whether to apply loudness normalization</param>
public record ExportPreset(
    string Name,
    string Description,
    AudioFormat Format,
    int SampleRate,
    int BitDepth,
    int? BitRate,
    float? TargetLufs,
    float? MaxTruePeak,
    bool NormalizeLoudness)
{
    /// <summary>
    /// Creates a copy of this preset with modified values.
    /// </summary>
    public ExportPreset WithTargetLufs(float? targetLufs) =>
        this with { TargetLufs = targetLufs, NormalizeLoudness = targetLufs.HasValue };

    /// <summary>
    /// Creates a copy of this preset with modified true peak limit.
    /// </summary>
    public ExportPreset WithMaxTruePeak(float? maxTruePeak) =>
        this with { MaxTruePeak = maxTruePeak };

    /// <summary>
    /// Creates a copy of this preset with modified format settings.
    /// </summary>
    public ExportPreset WithFormat(AudioFormat format, int? bitRate = null) =>
        this with { Format = format, BitRate = bitRate };

    /// <summary>
    /// Creates a copy of this preset with modified sample rate.
    /// </summary>
    public ExportPreset WithSampleRate(int sampleRate) =>
        this with { SampleRate = sampleRate };

    /// <summary>
    /// Creates a copy of this preset with modified bit depth.
    /// </summary>
    public ExportPreset WithBitDepth(int bitDepth) =>
        this with { BitDepth = bitDepth };

    /// <summary>
    /// Gets the file extension for this preset's format.
    /// </summary>
    public string FileExtension => Format switch
    {
        AudioFormat.Wav => ".wav",
        AudioFormat.Mp3 => ".mp3",
        AudioFormat.Flac => ".flac",
        AudioFormat.Ogg => ".ogg",
        _ => ".wav"
    };

    /// <summary>
    /// Gets a human-readable description of the format settings.
    /// </summary>
    public string FormatDescription
    {
        get
        {
            var format = Format switch
            {
                AudioFormat.Wav => $"WAV {BitDepth}-bit",
                AudioFormat.Mp3 => $"MP3 {BitRate}kbps",
                AudioFormat.Flac => $"FLAC {BitDepth}-bit",
                AudioFormat.Ogg => $"OGG {BitRate}kbps",
                _ => "Unknown"
            };

            return $"{format} / {SampleRate / 1000.0:F1}kHz";
        }
    }

    /// <summary>
    /// Gets a human-readable description of the loudness settings.
    /// </summary>
    public string LoudnessDescription
    {
        get
        {
            if (!NormalizeLoudness || !TargetLufs.HasValue)
            {
                return "No normalization";
            }

            var desc = $"{TargetLufs:F1} LUFS";
            if (MaxTruePeak.HasValue)
            {
                desc += $" / {MaxTruePeak:F1} dBTP";
            }

            return desc;
        }
    }
}

/// <summary>
/// Standard export presets optimized for various platforms and use cases.
/// </summary>
public static class ExportPresets
{
    /// <summary>
    /// Preset optimized for YouTube uploads.
    /// YouTube normalizes to -14 LUFS with -1 dBTP true peak limit.
    /// Uses MP3 320kbps at 48kHz as recommended by YouTube.
    /// </summary>
    public static ExportPreset YouTube => new(
        Name: "YouTube",
        Description: "Optimized for YouTube (-14 LUFS, -1 dBTP)",
        Format: AudioFormat.Mp3,
        SampleRate: 48000,
        BitDepth: 16,
        BitRate: 320,
        TargetLufs: -14f,
        MaxTruePeak: -1f,
        NormalizeLoudness: true);

    /// <summary>
    /// Preset optimized for Spotify.
    /// Spotify normalizes to -14 LUFS (default mode) with -1 dBTP limit.
    /// Uses high-quality OGG Vorbis format.
    /// </summary>
    public static ExportPreset Spotify => new(
        Name: "Spotify",
        Description: "Optimized for Spotify streaming (-14 LUFS, -1 dBTP)",
        Format: AudioFormat.Ogg,
        SampleRate: 44100,
        BitDepth: 16,
        BitRate: 320,
        TargetLufs: -14f,
        MaxTruePeak: -1f,
        NormalizeLoudness: true);

    /// <summary>
    /// Preset optimized for Apple Music / iTunes.
    /// Apple Music normalizes to -16 LUFS with -1 dBTP limit.
    /// Uses AAC 256kbps (exported as high-quality source for encoding).
    /// </summary>
    public static ExportPreset AppleMusic => new(
        Name: "Apple Music",
        Description: "Optimized for Apple Music (-16 LUFS, -1 dBTP)",
        Format: AudioFormat.Wav,
        SampleRate: 44100,
        BitDepth: 24,
        BitRate: null,
        TargetLufs: -16f,
        MaxTruePeak: -1f,
        NormalizeLoudness: true);

    /// <summary>
    /// Preset optimized for Amazon Music.
    /// Amazon normalizes to -14 LUFS with -2 dBTP limit.
    /// </summary>
    public static ExportPreset AmazonMusic => new(
        Name: "Amazon Music",
        Description: "Optimized for Amazon Music (-14 LUFS, -2 dBTP)",
        Format: AudioFormat.Wav,
        SampleRate: 44100,
        BitDepth: 24,
        BitRate: null,
        TargetLufs: -14f,
        MaxTruePeak: -2f,
        NormalizeLoudness: true);

    /// <summary>
    /// Preset for CD mastering.
    /// Standard Red Book CD format: 44.1kHz, 16-bit, no loudness normalization.
    /// Masters should typically target around -9 to -14 LUFS depending on genre.
    /// </summary>
    public static ExportPreset CDMaster => new(
        Name: "CD Master",
        Description: "Standard CD format (44.1kHz, 16-bit, no normalization)",
        Format: AudioFormat.Wav,
        SampleRate: 44100,
        BitDepth: 16,
        BitRate: null,
        TargetLufs: null,
        MaxTruePeak: -0.3f,
        NormalizeLoudness: false);

    /// <summary>
    /// Preset for high-resolution audio archives.
    /// 96kHz, 24-bit WAV for maximum quality archival.
    /// </summary>
    public static ExportPreset HiRes => new(
        Name: "Hi-Res Archive",
        Description: "High-resolution archive (96kHz, 24-bit WAV)",
        Format: AudioFormat.Wav,
        SampleRate: 96000,
        BitDepth: 24,
        BitRate: null,
        TargetLufs: null,
        MaxTruePeak: -0.3f,
        NormalizeLoudness: false);

    /// <summary>
    /// Preset for podcast/voice content.
    /// Podcasts typically target -16 LUFS (ATSC A/85) with -1 dBTP.
    /// </summary>
    public static ExportPreset Podcast => new(
        Name: "Podcast",
        Description: "Optimized for podcasts (-16 LUFS, mono-compatible)",
        Format: AudioFormat.Mp3,
        SampleRate: 44100,
        BitDepth: 16,
        BitRate: 192,
        TargetLufs: -16f,
        MaxTruePeak: -1f,
        NormalizeLoudness: true);

    /// <summary>
    /// Preset for broadcast (EBU R128).
    /// European Broadcasting Union standard: -23 LUFS, -1 dBTP.
    /// </summary>
    public static ExportPreset Broadcast => new(
        Name: "Broadcast (EBU R128)",
        Description: "European broadcast standard (-23 LUFS, -1 dBTP)",
        Format: AudioFormat.Wav,
        SampleRate: 48000,
        BitDepth: 24,
        BitRate: null,
        TargetLufs: -23f,
        MaxTruePeak: -1f,
        NormalizeLoudness: true);

    /// <summary>
    /// Preset for SoundCloud.
    /// SoundCloud streams at 128kbps MP3, normalizes to -14 LUFS.
    /// </summary>
    public static ExportPreset SoundCloud => new(
        Name: "SoundCloud",
        Description: "Optimized for SoundCloud (-14 LUFS)",
        Format: AudioFormat.Mp3,
        SampleRate: 44100,
        BitDepth: 16,
        BitRate: 320,
        TargetLufs: -14f,
        MaxTruePeak: -1f,
        NormalizeLoudness: true);

    /// <summary>
    /// Preset for Tidal (standard quality).
    /// Tidal normalizes to -14 LUFS.
    /// </summary>
    public static ExportPreset Tidal => new(
        Name: "Tidal",
        Description: "Optimized for Tidal streaming (-14 LUFS)",
        Format: AudioFormat.Flac,
        SampleRate: 44100,
        BitDepth: 16,
        BitRate: null,
        TargetLufs: -14f,
        MaxTruePeak: -1f,
        NormalizeLoudness: true);

    /// <summary>
    /// Gets all available presets.
    /// </summary>
    public static ExportPreset[] All => new[]
    {
        YouTube,
        Spotify,
        AppleMusic,
        AmazonMusic,
        SoundCloud,
        Tidal,
        Podcast,
        Broadcast,
        CDMaster,
        HiRes
    };

    /// <summary>
    /// Gets a preset by name (case-insensitive).
    /// </summary>
    /// <param name="name">The preset name to find.</param>
    /// <returns>The matching preset, or null if not found.</returns>
    public static ExportPreset? GetByName(string name)
    {
        foreach (var preset in All)
        {
            if (preset.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return preset;
            }
        }
        return null;
    }

    /// <summary>
    /// Creates a custom preset with the specified settings.
    /// </summary>
    public static ExportPreset Custom(
        string name = "Custom",
        AudioFormat format = AudioFormat.Wav,
        int sampleRate = 44100,
        int bitDepth = 24,
        int? bitRate = null,
        float? targetLufs = null,
        float? maxTruePeak = -0.3f,
        bool normalizeLoudness = false)
    {
        return new ExportPreset(
            Name: name,
            Description: "Custom export settings",
            Format: format,
            SampleRate: sampleRate,
            BitDepth: bitDepth,
            BitRate: bitRate,
            TargetLufs: targetLufs,
            MaxTruePeak: maxTruePeak,
            NormalizeLoudness: normalizeLoudness);
    }
}
