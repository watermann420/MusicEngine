// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Minimal settings for the simplified engine.

namespace MusicEngine.Core;

/// <summary>
/// Global engine defaults used when creating audio devices and signal paths.
/// </summary>
public static class Settings
{
    /// <summary>
    /// Global sample rate in Hz for playback and processing.
    /// </summary>
    public static int SampleRate { get; set; } = 44100;

    /// <summary>
    /// Default WAV bit depth for recordings.
    /// </summary>
    public static int WavBitDepth { get; set; } = 32;
    
    /// <summary>
    /// Global output channel count (typically 1 for mono or 2 for stereo).
    /// </summary>
    public static int Channels { get; set; } = 2;

    /// <summary>
    /// Default bitrate in kbps for compressed formats (mp3/aac/wma).
    /// </summary>
    public static int BitRateKbps { get; set; } = 192;

    /// <summary>
    /// Output bit depth for engine audio (set to 32 to disable quantization).
    /// </summary>
    public static int OutputBitDepth { get; set; } = 16;
}
