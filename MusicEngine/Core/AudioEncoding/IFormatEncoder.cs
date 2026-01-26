// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio encoding/export component.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MusicEngine.Core;

namespace MusicEngine.Core.AudioEncoding;

/// <summary>
/// Interface fuer Audio-Format-Encoder.
/// Implementierungen koennen verschiedene Ausgabeformate unterstuetzen (FLAC, OGG, AIFF, etc.).
/// </summary>
public interface IFormatEncoder : IDisposable
{
    /// <summary>
    /// Gets the supported audio formats for this encoder.
    /// </summary>
    AudioFormat[] SupportedFormats { get; }

    /// <summary>
    /// Gets whether the encoder is available (all dependencies loaded).
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Gets the reason why the encoder is not available (if IsAvailable is false).
    /// </summary>
    string? UnavailableReason { get; }

    /// <summary>
    /// Initializes the encoder with the specified settings.
    /// Must be called before encoding.
    /// </summary>
    /// <param name="settings">Encoder configuration.</param>
    /// <returns>True if initialization succeeded, false otherwise.</returns>
    bool Initialize(EncoderSettings settings);

    /// <summary>
    /// Encodes audio data from the input stream to the output stream.
    /// </summary>
    /// <param name="inputStream">Input stream containing raw PCM audio data.</param>
    /// <param name="outputStream">Output stream to write encoded audio to.</param>
    /// <param name="progress">Optional progress callback (0.0 to 1.0).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if encoding succeeded, false otherwise.</returns>
    Task<bool> EncodeAsync(
        Stream inputStream,
        Stream outputStream,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Encodes audio from an input file to an output file.
    /// </summary>
    /// <param name="inputPath">Path to input WAV file.</param>
    /// <param name="outputPath">Path for output encoded file.</param>
    /// <param name="progress">Optional progress callback (0.0 to 1.0).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if encoding succeeded, false otherwise.</returns>
    Task<bool> EncodeFileAsync(
        string inputPath,
        string outputPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the file extension for the output format (including dot, e.g., ".flac").
    /// </summary>
    string GetFileExtension(AudioFormat format);
}
