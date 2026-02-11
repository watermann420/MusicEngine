// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Minimal settings for the simplified engine.

using System;

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
    /// Default buffer size in frames.
    /// </summary>
    public static int BufferSizeFrames { get; set; } = 20512;

    /// <summary>
    /// Enable or disable automatic extra buffering.
    /// </summary>
    public static bool AutoBufferEnabled { get; set; } = true;

    /// <summary>
    /// Extra output latency in milliseconds when auto buffering is enabled.
    /// </summary>
    public static int AutoBufferExtraLatencyMs { get; set; } = 50;

    /// <summary>
    /// Extra output buffers when auto buffering is enabled.
    /// </summary>
    public static int AutoBufferExtraBuffers { get; set; } = 1;


    /// <summary>
    /// Output latency in milliseconds for the main audio device.
    /// </summary>
    public static int OutputLatencyMs { get; set; } = 100;

    /// <summary>
    /// Output buffer count for the main audio device.
    /// </summary>
    public static int OutputBufferCount { get; set; } = 3;

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
    public static int OutputBitDepth { get; set; } = 32;

    /// <summary>
    /// Default latency in milliseconds for virtual outputs.
    /// </summary>
    public static int VirtualOutputLatencyMs { get; set; } = 80;

    /// <summary>
    /// Enable or disable master safety processing (limiter/soft-clip).
    /// </summary>
    public static bool MasterSafetyEnabled { get; set; } = false;

    /// <summary>
    /// Global silence threshold used for idle detection.
    /// </summary>
    public static float AudioSilenceThreshold { get; set; } = 1e-5f;

    /// <summary>
    /// Enable or disable processing of non-VST audio effects.
    /// </summary>
    public static bool AudioEffectsEnabled { get; set; } = true;

    /// <summary>
    /// Enable or disable processing of VST instruments.
    /// </summary>
    public static bool VstInstrumentsEnabled { get; set; } = true;

    /// <summary>
    /// Enable or disable processing of VST effects.
    /// </summary>
    public static bool VstEffectsEnabled { get; set; } = true;

    /// <summary>
    /// Enable or disable the sequencer processing loop.
    /// </summary>
    public static bool SequencerEnabled { get; set; } = true;

    /// <summary>
    /// Default idle sleep behavior for VST instruments.
    /// </summary>
    public static bool VstInstrumentSleepWhenIdle { get; set; } = true;

    /// <summary>
    /// Default idle sleep behavior for VST effects.
    /// </summary>
    public static bool VstEffectSleepWhenIdle { get; set; } = true;

    /// <summary>
    /// Default idle threshold for VST sleep detection.
    /// </summary>
    public static float VstIdleThreshold { get; set; } = 2e-4f;

    /// <summary>
    /// Default idle timeout in seconds for VST sleep detection.
    /// </summary>
    public static double VstIdleTimeoutSeconds { get; set; } = 0.15;

    /// <summary>
    /// Default auto-save interval in seconds for VST instruments.
    /// </summary>
    public static double VstAutoSaveIntervalSeconds { get; set; } = 30.0;

    /// <summary>
    /// Default VST editor block size in frames.
    /// </summary>
    public static int VstEditorBlockSize { get; set; } = 512;

    /// <summary>
    /// Close native VST instances on dispose (disable if plugins crash on close).
    /// </summary>
    public static bool VstCloseOnDispose { get; set; } = false;

    /// <summary>
    /// Update buffer size and return a configurator for related settings.
    /// </summary>
    public static BufferConfig Buffer(int frames)
    {
        BufferSizeFrames = Math.Max(1, frames);
        var buffers = Math.Max(1, OutputBufferCount);
        var latencyMs = (int)Math.Round(BufferSizeFrames * 1000.0 / SampleRate * buffers);
        OutputLatencyMs = Math.Max(1, latencyMs);
        return new BufferConfig();
    }

    /// <summary>
    /// Buffer settings helper for chaining configuration.
    /// </summary>
    public sealed class BufferConfig
    {
        /// <summary>
        /// Set output latency in milliseconds.
        /// </summary>
        public BufferConfig Buffer(int latencyMs)
        {
            OutputLatencyMs = Math.Max(1, latencyMs);
            return this;
        }

        /// <summary>
        /// Set output latency in milliseconds.
        /// </summary>
        public BufferConfig buffer(int latencyMs) => Buffer(latencyMs);

        /// <summary>
        /// Set output buffer count.
        /// </summary>
        public BufferConfig Buffers(int count)
        {
            OutputBufferCount = Math.Max(1, count);
            return this;
        }

        /// <summary>
        /// Set output buffer count.
        /// </summary>
        public BufferConfig buffers(int count) => Buffers(count);

        /// <summary>
        /// Set virtual output latency in milliseconds.
        /// </summary>
        public BufferConfig Virtual(int latencyMs)
        {
            VirtualOutputLatencyMs = Math.Max(1, latencyMs);
            return this;
        }

        /// <summary>
        /// Set virtual output latency in milliseconds.
        /// </summary>
        public BufferConfig virtual_(int latencyMs) => Virtual(latencyMs);

        /// <summary>
        /// Set VST editor block size in frames.
        /// </summary>
        public BufferConfig VstEditor(int frames)
        {
            VstEditorBlockSize = Math.Max(1, frames);
            return this;
        }
    }
}
