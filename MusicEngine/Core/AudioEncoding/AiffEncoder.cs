// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio encoding/export component.

using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MusicEngine.Core;
using NAudio.Wave;

namespace MusicEngine.Core.AudioEncoding;

/// <summary>
/// Encoder for AIFF (Audio Interchange File Format).
/// Pure .NET implementation supporting 16-bit, 24-bit, and 32-bit float formats.
/// AIFF uses big-endian byte order.
/// </summary>
public class AiffEncoder : IFormatEncoder
{
    private EncoderSettings? _settings;
    private bool _disposed;

    /// <inheritdoc />
    public AudioFormat[] SupportedFormats => [AudioFormat.Aiff];

    /// <inheritdoc />
    public bool IsAvailable => true; // Pure .NET, always available

    /// <inheritdoc />
    public string? UnavailableReason => null;

    /// <inheritdoc />
    public bool Initialize(EncoderSettings settings)
    {
        if (settings.Format != AudioFormat.Aiff)
            return false;

        if (settings.BitDepth != 16 && settings.BitDepth != 24 && settings.BitDepth != 32)
            return false;

        if (settings.SampleRate <= 0 || settings.Channels <= 0)
            return false;

        _settings = settings;
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> EncodeAsync(
        Stream inputStream,
        Stream outputStream,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (_settings == null)
            throw new InvalidOperationException("Encoder not initialized. Call Initialize() first.");

        return await Task.Run(() =>
        {
            try
            {
                long totalBytes = inputStream.CanSeek ? inputStream.Length : 0;
                long processedBytes = 0;

                // AIFF file structure:
                // FORM chunk (container)
                //   - AIFF chunk ID
                //   - COMM chunk (common: channels, frames, bits, sample rate)
                //   - SSND chunk (sound data)

                // We need to buffer all data to calculate sizes
                using var dataBuffer = new MemoryStream();

                // Read and convert all audio data
                byte[] readBuffer = new byte[4096];
                int bytesRead;

                while ((bytesRead = inputStream.Read(readBuffer, 0, readBuffer.Length)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Convert from little-endian (WAV) to big-endian (AIFF)
                    byte[] convertedData = ConvertToBigEndian(readBuffer, bytesRead, _settings.BitDepth);
                    dataBuffer.Write(convertedData, 0, convertedData.Length);

                    processedBytes += bytesRead;
                    if (totalBytes > 0)
                    {
                        progress?.Report((double)processedBytes / totalBytes * 0.8);
                    }
                }

                // Calculate sizes
                int bytesPerSample = _settings.BitDepth / 8;
                int numSampleFrames = (int)(dataBuffer.Length / bytesPerSample / _settings.Channels);
                int soundDataSize = (int)dataBuffer.Length + 8; // +8 for offset and blockSize
                int commChunkSize = 18;
                int formChunkSize = 4 + 8 + commChunkSize + 8 + soundDataSize; // AIFF + COMM chunk + SSND chunk

                // Write AIFF header
                WriteAiffHeader(outputStream, formChunkSize, commChunkSize, numSampleFrames, soundDataSize);

                // Write audio data
                dataBuffer.Position = 0;
                dataBuffer.CopyTo(outputStream);

                // Pad to even length if necessary
                if (dataBuffer.Length % 2 != 0)
                {
                    outputStream.WriteByte(0);
                }

                progress?.Report(1.0);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AIFF encoding error: {ex.Message}");
                return false;
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> EncodeFileAsync(
        string inputPath,
        string outputPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (_settings == null)
            throw new InvalidOperationException("Encoder not initialized. Call Initialize() first.");

        return await Task.Run(() =>
        {
            try
            {
                using var reader = new AudioFileReader(inputPath);

                // Update settings from input file if needed
                if (_settings.SampleRate == 0)
                {
                    _settings = new EncoderSettings
                    {
                        Format = _settings.Format,
                        BitDepth = _settings.BitDepth,
                        SampleRate = reader.WaveFormat.SampleRate,
                        Channels = reader.WaveFormat.Channels,
                        Quality = _settings.Quality
                    };
                }

                long totalSamples = reader.Length / (reader.WaveFormat.BitsPerSample / 8);
                long processedSamples = 0;

                // Read all audio data and convert to target bit depth
                using var dataBuffer = new MemoryStream();
                float[] floatBuffer = new float[4096];
                int samplesRead;

                while ((samplesRead = reader.Read(floatBuffer, 0, floatBuffer.Length)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    byte[] convertedData = ConvertFloatToBigEndian(floatBuffer, samplesRead, _settings.BitDepth);
                    dataBuffer.Write(convertedData, 0, convertedData.Length);

                    processedSamples += samplesRead;
                    progress?.Report((double)processedSamples / totalSamples * 0.8);
                }

                // Calculate sizes
                int bytesPerSample = _settings.BitDepth / 8;
                int numSampleFrames = (int)(dataBuffer.Length / bytesPerSample / _settings.Channels);
                int soundDataSize = (int)dataBuffer.Length + 8; // +8 for offset and blockSize
                int commChunkSize = 18;
                int formChunkSize = 4 + 8 + commChunkSize + 8 + soundDataSize;

                // Write AIFF file
                using var outputStream = File.Create(outputPath);
                WriteAiffHeader(outputStream, formChunkSize, commChunkSize, numSampleFrames, soundDataSize);

                dataBuffer.Position = 0;
                dataBuffer.CopyTo(outputStream);

                // Pad to even length if necessary
                if (dataBuffer.Length % 2 != 0)
                {
                    outputStream.WriteByte(0);
                }

                progress?.Report(1.0);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AIFF encoding error: {ex.Message}");
                return false;
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public string GetFileExtension(AudioFormat format) => format == AudioFormat.Aiff ? ".aiff" : ".aif";

    /// <summary>
    /// Writes the AIFF file header.
    /// </summary>
    private void WriteAiffHeader(Stream stream, int formChunkSize, int commChunkSize, int numSampleFrames, int soundDataSize)
    {
        // FORM chunk
        WriteChunkId(stream, "FORM");
        WriteBigEndianInt32(stream, formChunkSize);
        WriteChunkId(stream, "AIFF");

        // COMM chunk (Common)
        WriteChunkId(stream, "COMM");
        WriteBigEndianInt32(stream, commChunkSize);
        WriteBigEndianInt16(stream, (short)_settings!.Channels);
        WriteBigEndianInt32(stream, numSampleFrames);
        WriteBigEndianInt16(stream, (short)_settings.BitDepth);
        WriteExtended80(stream, _settings.SampleRate);

        // SSND chunk (Sound Data)
        WriteChunkId(stream, "SSND");
        WriteBigEndianInt32(stream, soundDataSize);
        WriteBigEndianInt32(stream, 0); // offset
        WriteBigEndianInt32(stream, 0); // blockSize
    }

    /// <summary>
    /// Converts little-endian PCM data to big-endian.
    /// </summary>
    private static byte[] ConvertToBigEndian(byte[] data, int length, int bitDepth)
    {
        byte[] result = new byte[length];
        int bytesPerSample = bitDepth / 8;

        for (int i = 0; i < length; i += bytesPerSample)
        {
            if (i + bytesPerSample > length) break;

            // Reverse byte order for each sample
            for (int j = 0; j < bytesPerSample; j++)
            {
                result[i + j] = data[i + bytesPerSample - 1 - j];
            }
        }

        return result;
    }

    /// <summary>
    /// Converts float samples to big-endian PCM data.
    /// </summary>
    private static byte[] ConvertFloatToBigEndian(float[] samples, int count, int bitDepth)
    {
        int bytesPerSample = bitDepth / 8;
        byte[] result = new byte[count * bytesPerSample];

        for (int i = 0; i < count; i++)
        {
            float sample = Math.Clamp(samples[i], -1.0f, 1.0f);
            int offset = i * bytesPerSample;

            switch (bitDepth)
            {
                case 16:
                    short int16 = (short)(sample * 32767f);
                    result[offset] = (byte)(int16 >> 8);
                    result[offset + 1] = (byte)(int16 & 0xFF);
                    break;

                case 24:
                    int int24 = (int)(sample * 8388607f);
                    result[offset] = (byte)((int24 >> 16) & 0xFF);
                    result[offset + 1] = (byte)((int24 >> 8) & 0xFF);
                    result[offset + 2] = (byte)(int24 & 0xFF);
                    break;

                case 32:
                    // 32-bit float, IEEE 754 big-endian
                    byte[] floatBytes = BitConverter.GetBytes(sample);
                    if (BitConverter.IsLittleEndian)
                    {
                        result[offset] = floatBytes[3];
                        result[offset + 1] = floatBytes[2];
                        result[offset + 2] = floatBytes[1];
                        result[offset + 3] = floatBytes[0];
                    }
                    else
                    {
                        result[offset] = floatBytes[0];
                        result[offset + 1] = floatBytes[1];
                        result[offset + 2] = floatBytes[2];
                        result[offset + 3] = floatBytes[3];
                    }
                    break;
            }
        }

        return result;
    }

    /// <summary>
    /// Writes a 4-character chunk ID.
    /// </summary>
    private static void WriteChunkId(Stream stream, string id)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(id);
        stream.Write(bytes, 0, 4);
    }

    /// <summary>
    /// Writes a 32-bit integer in big-endian format.
    /// </summary>
    private static void WriteBigEndianInt32(Stream stream, int value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        stream.Write(bytes, 0, 4);
    }

    /// <summary>
    /// Writes a 16-bit integer in big-endian format.
    /// </summary>
    private static void WriteBigEndianInt16(Stream stream, short value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteInt16BigEndian(bytes, value);
        stream.Write(bytes, 0, 2);
    }

    /// <summary>
    /// Writes an 80-bit extended precision floating point number (IEEE 754).
    /// Used for sample rate in AIFF.
    /// </summary>
    private static void WriteExtended80(Stream stream, double value)
    {
        // Simplified implementation for common sample rates
        // The 80-bit extended format: 1 sign bit, 15 exponent bits, 64 mantissa bits

        byte[] result = new byte[10];

        if (value == 0.0)
        {
            stream.Write(result, 0, 10);
            return;
        }

        int sign = value < 0 ? 1 : 0;
        value = Math.Abs(value);

        // Find exponent
        int exponent = (int)Math.Floor(Math.Log2(value)) + 16383;

        // Calculate mantissa
        double mantissa = value / Math.Pow(2, exponent - 16383);
        ulong mantissaBits = (ulong)(mantissa * (1UL << 63));

        // Pack into bytes (big-endian)
        result[0] = (byte)((sign << 7) | ((exponent >> 8) & 0x7F));
        result[1] = (byte)(exponent & 0xFF);

        // Mantissa (big-endian)
        for (int i = 0; i < 8; i++)
        {
            result[2 + i] = (byte)((mantissaBits >> (56 - i * 8)) & 0xFF);
        }

        stream.Write(result, 0, 10);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _settings = null;
        GC.SuppressFinalize(this);
    }
}
