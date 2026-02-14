#if !WINDOWS
// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann
// Description: Simple audio file loader for non-Windows builds.
#else
// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann
// Description: Simple audio file loader.
#endif

using System;
using System.Collections.Generic;
using NAudio.Wave;
#if WINDOWS
using NAudio.Wave.SampleProviders;
#endif

namespace MusicEngine.Core;

internal readonly struct AudioFileData
{
    public AudioFileData(float[] samples, int sampleRate, int channels)
    {
        Samples = samples;
        SampleRate = sampleRate;
        Channels = channels;
    }

    public float[] Samples { get; }
    public int SampleRate { get; }
    public int Channels { get; }
}

internal static class AudioFileLoader
{
    public static AudioFileData Load(string path, int? targetSampleRate = null, int? targetChannels = null)
    {
#if WINDOWS
        using var reader = new AudioFileReader(path);
        ISampleProvider provider = reader;

        int desiredRate = targetSampleRate ?? provider.WaveFormat.SampleRate;
        int desiredChannels = targetChannels ?? provider.WaveFormat.Channels;

        if (provider.WaveFormat.SampleRate != desiredRate)
        {
            provider = new WdlResamplingSampleProvider(provider, desiredRate);
        }

        if (provider.WaveFormat.Channels != desiredChannels)
        {
            provider = provider.WaveFormat.Channels == 1 && desiredChannels == 2
                ? new MonoToStereoSampleProvider(provider)
                : new StereoToMonoSampleProvider(provider);
        }

        var buffer = new float[desiredRate * desiredChannels];
        var data = new List<float>(buffer.Length);
        int read;
        while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < read; i++)
            {
                data.Add(buffer[i]);
            }
        }

        return new AudioFileData(data.ToArray(), desiredRate, desiredChannels);
#else
        using var reader = new SimpleWaveFileReader(path);
        int srcRate = reader.WaveFormat.SampleRate;
        int srcChannels = reader.WaveFormat.Channels;
        int desiredRate = targetSampleRate ?? srcRate;
        int desiredChannels = targetChannels ?? srcChannels;

        var samples = reader.Samples;
        if (srcChannels != desiredChannels)
        {
            samples = ConvertChannels(samples, srcChannels, desiredChannels);
            srcChannels = desiredChannels;
        }

        if (srcRate != desiredRate)
        {
            samples = Resample(samples, srcChannels, srcRate, desiredRate);
        }

        return new AudioFileData(samples, desiredRate, srcChannels);
#endif
    }

#if !WINDOWS
    private static float[] ConvertChannels(float[] samples, int srcChannels, int dstChannels)
    {
        int frames = srcChannels == 0 ? 0 : samples.Length / srcChannels;
        var output = new float[frames * dstChannels];

        for (int frame = 0; frame < frames; frame++)
        {
            int srcIndex = frame * srcChannels;
            int dstIndex = frame * dstChannels;

            if (srcChannels == 1 && dstChannels == 2)
            {
                float mono = samples[srcIndex];
                output[dstIndex] = mono;
                output[dstIndex + 1] = mono;
                continue;
            }

            if (srcChannels == 2 && dstChannels == 1)
            {
                float left = samples[srcIndex];
                float right = samples[srcIndex + 1];
                output[dstIndex] = (left + right) * 0.5f;
                continue;
            }

            for (int ch = 0; ch < dstChannels; ch++)
            {
                int srcCh = Math.Min(ch, srcChannels - 1);
                output[dstIndex + ch] = samples[srcIndex + srcCh];
            }
        }

        return output;
    }

    private static float[] Resample(float[] samples, int channels, int srcRate, int dstRate)
    {
        int srcFrames = channels == 0 ? 0 : samples.Length / channels;
        int dstFrames = (int)Math.Max(1, Math.Round(srcFrames * (double)dstRate / srcRate));
        var output = new float[dstFrames * channels];

        double ratio = (double)srcRate / dstRate;
        for (int frame = 0; frame < dstFrames; frame++)
        {
            double srcPos = frame * ratio;
            int i0 = (int)srcPos;
            int i1 = Math.Min(i0 + 1, srcFrames - 1);
            float frac = (float)(srcPos - i0);

            int srcIndex0 = i0 * channels;
            int srcIndex1 = i1 * channels;
            int dstIndex = frame * channels;

            for (int ch = 0; ch < channels; ch++)
            {
                float s0 = samples[srcIndex0 + ch];
                float s1 = samples[srcIndex1 + ch];
                output[dstIndex + ch] = s0 + (s1 - s0) * frac;
            }
        }

        return output;
    }
#endif
}
