// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Sample-accurate audio deck for scrubbing and scratch playback.

using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MusicEngine.Core;

/// <summary>
/// Sample-accurate audio deck for scrubbing, looping, and scratch playback.
/// </summary>
public sealed class AudioDeck : ISampleProvider
{
    private readonly WaveFormat _waveFormat;
    private float[] _data = Array.Empty<float>();
    private int _frameCount;
    private float _positionFrames;

    /// <summary>
    /// Create a deck with a name and optional sample rate override.
    /// </summary>
    public AudioDeck(string name, int? sampleRate = null)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "Deck" : name;
        var rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);
    }

    /// <summary>
    /// Display name for this deck.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Output wave format.
    /// </summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>
    /// Whether playback is currently running.
    /// </summary>
    public bool IsPlaying { get; set; } = true;

    /// <summary>
    /// When true, playback loops at the end.
    /// </summary>
    public bool Loop { get; set; }

    /// <summary>
    /// Playback speed multiplier.
    /// </summary>
    public float PlaySpeed { get; set; } = 1f;

    /// <summary>
    /// Master volume.
    /// </summary>
    public float Volume { get; set; } = 1f;

    /// <summary>
    /// Master pan (-1..1).
    /// </summary>
    public float Pan { get; set; } = 0f;

    /// <summary>
    /// Duration of the loaded audio in seconds.
    /// </summary>
    public double DurationSeconds => _frameCount == 0 ? 0 : (double)_frameCount / _waveFormat.SampleRate;

    /// <summary>
    /// Load an audio file into the deck.
    /// </summary>
    public void Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Audio path is required.", nameof(path));
        if (!File.Exists(path)) throw new FileNotFoundException($"Audio file not found: {path}", path);

        using var reader = new AudioFileReader(path);
        ISampleProvider provider = reader;

        if (provider.WaveFormat.SampleRate != _waveFormat.SampleRate)
        {
            provider = new WdlResamplingSampleProvider(provider, _waveFormat.SampleRate);
        }

        if (provider.WaveFormat.Channels != _waveFormat.Channels)
        {
            provider = provider.WaveFormat.Channels == 1
                ? new MonoToStereoSampleProvider(provider)
                : new StereoToMonoSampleProvider(provider);
        }

        var buffer = new float[_waveFormat.SampleRate * _waveFormat.Channels];
        var data = new List<float>(buffer.Length);
        int read;
        while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < read; i++)
            {
                data.Add(buffer[i]);
            }
        }

        _data = data.ToArray();
        _frameCount = _waveFormat.Channels == 0 ? 0 : _data.Length / _waveFormat.Channels;
        _positionFrames = 0f;
    }

    /// <summary>
    /// Seek to an absolute position in seconds.
    /// </summary>
    public void SeekSeconds(double seconds)
    {
        if (_frameCount == 0) return;
        var frames = seconds * _waveFormat.SampleRate;
        _positionFrames = (float)Math.Clamp(frames, 0, _frameCount - 1);
    }

    /// <summary>
    /// Scratch (scrub) by delta seconds.
    /// </summary>
    public void ScratchSeconds(double deltaSeconds)
    {
        if (_frameCount == 0) return;
        var frames = deltaSeconds * _waveFormat.SampleRate;
        _positionFrames = WrapOrClamp(_positionFrames + (float)frames);
    }

    /// <summary>
    /// Read audio samples into the buffer.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        Array.Clear(buffer, offset, count);
        if (!IsPlaying || _frameCount == 0) return count;

        int channels = _waveFormat.Channels;
        int frames = count / channels;
        float panL = Math.Min(1f, 1f - Pan);
        float panR = Math.Min(1f, 1f + Pan);

        for (int i = 0; i < frames; i++)
        {
            int frameIndex = (int)_positionFrames;
            if (frameIndex >= _frameCount)
            {
                if (Loop)
                {
                    _positionFrames = 0f;
                    frameIndex = 0;
                }
                else
                {
                    IsPlaying = false;
                    break;
                }
            }

            int nextIndex = Math.Min(frameIndex + 1, _frameCount - 1);
            float frac = _positionFrames - frameIndex;

            int baseIdx = frameIndex * channels;
            int nextBase = nextIndex * channels;

            float left = _data[baseIdx];
            float right = channels > 1 ? _data[baseIdx + 1] : left;
            float leftNext = _data[nextBase];
            float rightNext = channels > 1 ? _data[nextBase + 1] : leftNext;

            float sampleL = left + (leftNext - left) * frac;
            float sampleR = right + (rightNext - right) * frac;

            int outIndex = offset + i * channels;
            buffer[outIndex] += sampleL * Volume * panL;
            if (channels > 1)
            {
                buffer[outIndex + 1] += sampleR * Volume * panR;
            }

            _positionFrames += Math.Max(0.000001f, PlaySpeed);
        }

        return count;
    }

    private float WrapOrClamp(float position)
    {
        if (_frameCount == 0) return 0f;
        if (Loop)
        {
            float length = _frameCount;
            position %= length;
            if (position < 0) position += length;
            return position;
        }
        return Math.Clamp(position, 0f, _frameCount - 1);
    }
}
