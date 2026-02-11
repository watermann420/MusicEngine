// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Wavetable helper for custom oscillator shapes.

using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;

namespace MusicEngine.Instruments.Modules;

/// <summary>
/// Simple wavetable container with linear interpolation sampling.
/// </summary>
public sealed class Wavetable
{
    public float[] Samples { get; }

    public int Length => Samples.Length;

    public Wavetable(float[] samples)
    {
        if (samples == null || samples.Length < 2)
        {
            throw new ArgumentException("Wavetable samples must have at least 2 values.", nameof(samples));
        }
        Samples = samples;
    }

    public float Sample(float phase)
    {
        phase -= (float)Math.Floor(phase);
        float pos = phase * Samples.Length;
        int i0 = (int)pos;
        int i1 = i0 + 1;
        if (i1 >= Samples.Length) i1 = 0;
        float frac = pos - i0;
        return Samples[i0] + (Samples[i1] - Samples[i0]) * frac;
    }

    public static Wavetable FromSamples(float[] samples, bool copy = true)
    {
        if (samples == null) throw new ArgumentNullException(nameof(samples));
        return new Wavetable(copy ? (float[])samples.Clone() : samples);
    }

    public static Wavetable FromFile(string path, int? maxSamples = null)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path is required.", nameof(path));
        if (!File.Exists(path)) throw new FileNotFoundException("Wavetable file not found.", path);

        using var reader = new AudioFileReader(path);
        int channels = reader.WaveFormat.Channels;
        var buffer = new float[reader.WaveFormat.SampleRate * channels];
        var samples = new List<float>();
        int read;
        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < read; i += channels)
            {
                samples.Add(buffer[i]);
            }
        }

        var data = samples.ToArray();
        if (maxSamples.HasValue && maxSamples.Value > 1 && data.Length > maxSamples.Value)
        {
            int target = maxSamples.Value;
            var down = new float[target];
            float stride = (float)data.Length / target;
            for (int i = 0; i < target; i++)
            {
                down[i] = data[(int)(i * stride)];
            }
            data = down;
        }

        return new Wavetable(data);
    }

    public static Wavetable Sine(int size = 2048) => new(BuildSine(size));

    public static Wavetable Saw(int size = 2048) => new(BuildSaw(size));

    public static Wavetable Square(int size = 2048) => new(BuildSquare(size));

    public static Wavetable Triangle(int size = 2048) => new(BuildTriangle(size));

    public static Wavetable WhiteNoise(int size = 2048) => new(BuildWhiteNoise(size));

    private static float[] BuildSine(int size)
    {
        size = Math.Max(2, size);
        var data = new float[size];
        for (int i = 0; i < size; i++)
        {
            data[i] = (float)Math.Sin(i * 2.0 * Math.PI / size);
        }
        return data;
    }

    private static float[] BuildSaw(int size)
    {
        size = Math.Max(2, size);
        var data = new float[size];
        for (int i = 0; i < size; i++)
        {
            data[i] = 2f * (i / (float)size) - 1f;
        }
        return data;
    }

    private static float[] BuildSquare(int size)
    {
        size = Math.Max(2, size);
        var data = new float[size];
        int half = size / 2;
        for (int i = 0; i < size; i++)
        {
            data[i] = i < half ? 1f : -1f;
        }
        return data;
    }

    private static float[] BuildTriangle(int size)
    {
        size = Math.Max(2, size);
        var data = new float[size];
        for (int i = 0; i < size; i++)
        {
            float phase = i / (float)size;
            data[i] = phase < 0.5f ? 4f * phase - 1f : 3f - 4f * phase;
        }
        return data;
    }

    private static float[] BuildWhiteNoise(int size)
    {
        size = Math.Max(2, size);
        var data = new float[size];
        var rnd = new Random(1337);
        for (int i = 0; i < size; i++)
        {
            data[i] = (float)rnd.NextDouble() * 2f - 1f;
        }
        return data;
    }
}
