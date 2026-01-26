// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Factory for audio encoders.

using System;
using System.Collections.Generic;
using System.Linq;
using MusicEngine.Core;

namespace MusicEngine.Core.AudioEncoding;

/// <summary>
/// Factory for creating audio format encoders.
/// Uses reflection to load optional encoder dependencies (NAudio.Flac, NVorbis, etc.).
/// </summary>
public static class EncoderFactory
{
    private static readonly Dictionary<AudioFormat, Func<IFormatEncoder>> EncoderCreators = new();
    private static readonly object InitLock = new();
    private static bool _initialized;

    /// <summary>
    /// Gets all available encoders and their supported formats.
    /// </summary>
    public static IReadOnlyDictionary<AudioFormat, string> AvailableEncoders
    {
        get
        {
            EnsureInitialized();
            var result = new Dictionary<AudioFormat, string>();

            foreach (var format in EncoderCreators.Keys)
            {
                using var encoder = EncoderCreators[format]();
                result[format] = encoder.IsAvailable
                    ? "Available"
                    : encoder.UnavailableReason ?? "Unknown";
            }

            return result;
        }
    }

    /// <summary>
    /// Gets whether an encoder is available for the specified format.
    /// </summary>
    public static bool IsFormatSupported(AudioFormat format)
    {
        EnsureInitialized();

        if (!EncoderCreators.TryGetValue(format, out var creator))
            return false;

        using var encoder = creator();
        return encoder.IsAvailable;
    }

    /// <summary>
    /// Creates an encoder for the specified format.
    /// </summary>
    /// <param name="format">The audio format to encode to.</param>
    /// <returns>An encoder instance, or null if format is not supported.</returns>
    public static IFormatEncoder? CreateEncoder(AudioFormat format)
    {
        EnsureInitialized();

        if (!EncoderCreators.TryGetValue(format, out var creator))
            return null;

        return creator();
    }

    /// <summary>
    /// Creates and initializes an encoder for the specified settings.
    /// </summary>
    /// <param name="settings">Encoder settings.</param>
    /// <returns>An initialized encoder, or null if initialization failed.</returns>
    public static IFormatEncoder? CreateAndInitialize(EncoderSettings settings)
    {
        var encoder = CreateEncoder(settings.Format);
        if (encoder == null)
            return null;

        if (!encoder.Initialize(settings))
        {
            encoder.Dispose();
            return null;
        }

        return encoder;
    }

    /// <summary>
    /// Gets all supported formats that have available encoders.
    /// </summary>
    public static AudioFormat[] GetSupportedFormats()
    {
        EnsureInitialized();
        return EncoderCreators.Keys
            .Where(IsFormatSupported)
            .ToArray();
    }

    /// <summary>
    /// Registers a custom encoder for a format.
    /// </summary>
    /// <param name="format">The format this encoder handles.</param>
    /// <param name="creator">Factory function to create the encoder.</param>
    public static void RegisterEncoder(AudioFormat format, Func<IFormatEncoder> creator)
    {
        lock (InitLock)
        {
            EncoderCreators[format] = creator;
        }
    }

    /// <summary>
    /// Ensures the factory is initialized with default encoders.
    /// </summary>
    private static void EnsureInitialized()
    {
        if (_initialized) return;

        lock (InitLock)
        {
            if (_initialized) return;

            // Register built-in encoders
            EncoderCreators[AudioFormat.Aiff] = () => new AiffEncoder();
            EncoderCreators[AudioFormat.Flac] = () => new FlacEncoder();
            EncoderCreators[AudioFormat.Ogg] = () => new OggVorbisEncoder();

            _initialized = true;
        }
    }

    /// <summary>
    /// Resets the factory to uninitialized state (for testing).
    /// </summary>
    internal static void Reset()
    {
        lock (InitLock)
        {
            EncoderCreators.Clear();
            _initialized = false;
        }
    }
}
