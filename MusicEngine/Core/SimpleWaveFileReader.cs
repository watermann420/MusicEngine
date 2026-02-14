#if !WINDOWS
// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann
// Description: Minimal WAV reader for Linux builds.

using System;
using System.IO;
using NAudio.Wave;

namespace MusicEngine.Core;

internal sealed class SimpleWaveFileReader : ISampleProvider, IDisposable
{
    private readonly float[] _samples;
    private int _position;
    private bool _disposed;

    public SimpleWaveFileReader(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Audio file path is required.", nameof(path));
        }

        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);

        if (new string(reader.ReadChars(4)) != "RIFF")
        {
            throw new InvalidDataException("Invalid WAV file (missing RIFF).");
        }

        _ = reader.ReadInt32(); // file size
        if (new string(reader.ReadChars(4)) != "WAVE")
        {
            throw new InvalidDataException("Invalid WAV file (missing WAVE).");
        }

        short audioFormat = 0;
        short channels = 0;
        int sampleRate = 0;
        short bitsPerSample = 0;
        byte[]? data = null;

        while (stream.Position < stream.Length)
        {
            var chunkId = new string(reader.ReadChars(4));
            var chunkSize = reader.ReadInt32();

            if (chunkId == "fmt ")
            {
                audioFormat = reader.ReadInt16();
                channels = reader.ReadInt16();
                sampleRate = reader.ReadInt32();
                _ = reader.ReadInt32(); // byte rate
                _ = reader.ReadInt16(); // block align
                bitsPerSample = reader.ReadInt16();

                if (chunkSize > 16)
                {
                    reader.ReadBytes(chunkSize - 16);
                }
            }
            else if (chunkId == "data")
            {
                data = reader.ReadBytes(chunkSize);
            }
            else
            {
                reader.ReadBytes(chunkSize);
            }

            if (data != null && audioFormat != 0 && sampleRate > 0)
            {
                break;
            }
        }

        if (data == null || audioFormat == 0 || sampleRate <= 0 || channels <= 0)
        {
            throw new InvalidDataException("Invalid WAV file (missing format or data).");
        }

        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);

        _samples = ConvertToFloat(data, audioFormat, bitsPerSample);
    }

    public WaveFormat WaveFormat { get; }

    public float[] Samples => _samples;

    public long Position
    {
        get => (long)_position * sizeof(float);
        set => _position = (int)Math.Clamp(value / sizeof(float), 0, _samples.Length);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        if (_disposed) return 0;
        if (_position >= _samples.Length) return 0;

        int available = _samples.Length - _position;
        int toCopy = Math.Min(count, available);
        Array.Copy(_samples, _position, buffer, offset, toCopy);
        _position += toCopy;
        return toCopy;
    }

    public void Dispose()
    {
        _disposed = true;
    }

    private static float[] ConvertToFloat(byte[] data, short format, short bitsPerSample)
    {
        if (format != 1 && format != 3)
        {
            throw new NotSupportedException($"Unsupported WAV format ({format}).");
        }

        int bytesPerSample = bitsPerSample / 8;
        if (bytesPerSample <= 0)
        {
            throw new InvalidDataException("Invalid WAV bit depth.");
        }

        int sampleCount = data.Length / bytesPerSample;
        var samples = new float[sampleCount];

        int index = 0;
        if (format == 3 && bitsPerSample == 32)
        {
            for (int i = 0; i < sampleCount; i++)
            {
                samples[i] = BitConverter.ToSingle(data, index);
                index += 4;
            }
            return samples;
        }

        if (bitsPerSample == 16)
        {
            for (int i = 0; i < sampleCount; i++)
            {
                short value = (short)(data[index] | (data[index + 1] << 8));
                samples[i] = value / 32768f;
                index += 2;
            }
            return samples;
        }

        if (bitsPerSample == 24)
        {
            for (int i = 0; i < sampleCount; i++)
            {
                int value = data[index] | (data[index + 1] << 8) | (data[index + 2] << 16);
                if ((value & 0x800000) != 0) value |= unchecked((int)0xFF000000);
                samples[i] = value / 8388608f;
                index += 3;
            }
            return samples;
        }

        if (bitsPerSample == 32)
        {
            for (int i = 0; i < sampleCount; i++)
            {
                int value = BitConverter.ToInt32(data, index);
                samples[i] = value / 2147483648f;
                index += 4;
            }
            return samples;
        }

        throw new NotSupportedException($"Unsupported WAV bit depth ({bitsPerSample}).");
    }
}
#endif
