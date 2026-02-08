// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Sample-based instrument with MIDI note mapping.

using System;
using System.Collections.Generic;
using System.IO;
using MusicEngine.Core;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MusicEngine.Instruments;

/// <summary>
/// Sample-based instrument with MIDI note mapping and basic playback controls.
/// </summary>
public sealed class SamplerInstrument : ISynth
{
    private readonly WaveFormat _waveFormat;
    private readonly Dictionary<string, SampleData> _samples = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, string> _noteMap = new();
    private readonly List<Voice> _voices = new();
    private readonly object _voiceLock = new();

    /// <summary>
    /// Display name for the sampler instance.
    /// </summary>
    public string Name { get; set; } = "Sampler";

    /// <summary>
    /// Output wave format.
    /// </summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>
    /// Master volume (0..1).
    /// </summary>
    public float Volume { get; set; } = 0.8f;

    /// <summary>
    /// Master pan (-1..1).
    /// </summary>
    public float Pan { get; set; } = 0f;

    /// <summary>
    /// Mod wheel value (0..1).
    /// </summary>
    public float ModWheel { get; set; } = 0f;

    /// <summary>
    /// MIDI channel (0..15), or -1 for all.
    /// </summary>
    public int Channel { get; set; } = -1;

    /// <summary>
    /// Reverb amount (0..1).
    /// </summary>
    public float Reverb { get; set; } = 0f;

    /// <summary>
    /// Chorus amount (0..1).
    /// </summary>
    public float Chorus { get; set; } = 0f;

    /// <summary>
    /// Global pitch offset in semitones.
    /// </summary>
    public float PitchSemitones { get; set; } = 0f;

    /// <summary>
    /// Global playback speed multiplier.
    /// </summary>
    public float PlaySpeed { get; set; } = 1f;

    /// <summary>
    /// Release time in seconds for non-one-shot samples.
    /// </summary>
    public float ReleaseSeconds { get; set; } = 0.05f;

    /// <summary>
    /// Maximum number of voices.
    /// </summary>
    public int MaxPolyphony { get; set; } = 32;

    /// <summary>
    /// Use velocity to scale volume.
    /// </summary>
    public bool VelocityToVolume { get; set; } = true;

    /// <summary>
    /// When true, uses the nearest mapped sample if a note is not explicitly mapped.
    /// </summary>
    public bool UseNearestSample { get; set; } = true;

    public SamplerInstrument(int? sampleRate = null)
    {
        var rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);
    }

    /// <summary>
    /// Load a sample from disk and register it by name.
    /// </summary>
    public void LoadSample(string name, string path, int rootNote = 60)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Sample name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Sample path is required.", nameof(path));
        if (!File.Exists(path)) throw new FileNotFoundException($"Sample not found: {path}", path);

        var data = ReadSampleData(path);
        data.Name = name;
        data.Settings.RootNote = rootNote;
        _samples[name] = data;
    }

    /// <summary>
    /// Load all samples from a folder.
    /// </summary>
    public int LoadSamplesFromFolder(string folder, string searchPattern = "*.wav", bool recursive = true)
    {
        if (string.IsNullOrWhiteSpace(folder)) return 0;
        if (!Directory.Exists(folder)) return 0;

        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.GetFiles(folder, searchPattern, option);
        int loaded = 0;

        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            try
            {
                var data = ReadSampleData(file);
                data.Name = name;
                _samples[name] = data;
                loaded++;
            }
            catch
            {
            }
        }

        return loaded;
    }

    /// <summary>
    /// Map a MIDI note to a sample name.
    /// </summary>
    public void MapSample(int note, string sampleName)
    {
        MidiValidation.ValidateNote(note);
        if (string.IsNullOrWhiteSpace(sampleName)) return;
        _noteMap[note] = sampleName;
    }

    /// <summary>
    /// Map a range of MIDI notes to the same sample name.
    /// </summary>
    public void MapRange(int startNote, int endNote, string sampleName)
    {
        if (endNote < startNote)
        {
            (startNote, endNote) = (endNote, startNote);
        }
        MidiValidation.ValidateNote(startNote);
        MidiValidation.ValidateNote(endNote);
        if (string.IsNullOrWhiteSpace(sampleName)) return;

        for (int note = startNote; note <= endNote; note++)
        {
            _noteMap[note] = sampleName;
        }
    }

    /// <summary>
    /// Clear all note mappings.
    /// </summary>
    public void ClearMapping() => _noteMap.Clear();

    /// <summary>
    /// Update sample settings.
    /// </summary>
    public void SetSampleSettings(string sampleName, Action<SampleSettings> configure)
    {
        if (!_samples.TryGetValue(sampleName, out var data)) return;
        configure?.Invoke(data.Settings);
    }

    /// <summary>
    /// Find sample names that contain the query.
    /// </summary>
    public IReadOnlyList<string> FindSamples(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<string>();
        var results = new List<string>();
        foreach (var name in _samples.Keys)
        {
            if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(name);
            }
        }
        return results;
    }

    /// <summary>
    /// Trigger a MIDI note-on.
    /// </summary>
    public void NoteOn(int note, int velocity)
    {
        note = Math.Clamp(note, 0, 127);
        velocity = Math.Clamp(velocity, 1, 127);
        var sample = ResolveSample(note);
        if (sample == null) return;

        float vel = VelocityToVolume ? velocity / 127f : 1f;
        float semitoneOffset = note - sample.Settings.RootNote + PitchSemitones + sample.Settings.PitchSemitones;
        float pitchRatio = (float)Math.Pow(2, semitoneOffset / 12f);
        float rate = pitchRatio * PlaySpeed * sample.Settings.PlaySpeed;

        var voice = new Voice(sample, rate, vel, note);

        lock (_voiceLock)
        {
            if (_voices.Count >= MaxPolyphony)
            {
                _voices.RemoveAt(0);
            }
            _voices.Add(voice);
        }
    }

    /// <summary>
    /// Trigger a MIDI note-off.
    /// </summary>
    public void NoteOff(int note)
    {
        note = Math.Clamp(note, 0, 127);
        lock (_voiceLock)
        {
            for (int i = 0; i < _voices.Count; i++)
            {
                if (_voices[i].Note == note)
                {
                    _voices[i].StartRelease(ReleaseSeconds);
                }
            }
        }
    }

    /// <summary>
    /// Stop all notes.
    /// </summary>
    public void AllNotesOff()
    {
        lock (_voiceLock)
        {
            for (int i = 0; i < _voices.Count; i++)
            {
                _voices[i].StartRelease(ReleaseSeconds);
            }
        }
    }

    /// <summary>
    /// Set a named parameter.
    /// </summary>
    public void SetParameter(string name, float value)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        switch (name.ToLowerInvariant())
        {
            case "volume":
                Volume = value;
                break;
            case "pan":
                Pan = value;
                break;
            case "reverb":
                Reverb = value;
                break;
            case "chorus":
                Chorus = value;
                break;
            case "channel":
                Channel = (int)value;
                break;
            case "pitch":
            case "pitchsemitones":
                PitchSemitones = value;
                break;
            case "playspeed":
            case "speed":
                PlaySpeed = Math.Max(0f, value);
                break;
            case "release":
            case "releaseseconds":
                ReleaseSeconds = value;
                break;
            case "maxpolyphony":
                MaxPolyphony = (int)Math.Max(1f, value);
                break;
        }
    }

    /// <summary>
    /// Scratch (scrub) all active voices by delta seconds.
    /// </summary>
    public void ScratchSeconds(double deltaSeconds)
    {
        if (deltaSeconds == 0) return;
        var frames = (float)(deltaSeconds * _waveFormat.SampleRate);
        ScratchFrames(frames);
    }

    /// <summary>
    /// Scratch (scrub) all active voices by delta frames.
    /// </summary>
    public void ScratchFrames(float deltaFrames)
    {
        if (deltaFrames == 0) return;
        lock (_voiceLock)
        {
            for (int i = 0; i < _voices.Count; i++)
            {
                _voices[i].AdjustPosition(deltaFrames);
            }
        }
    }

    /// <summary>
    /// Read audio samples into the buffer.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        Array.Clear(buffer, offset, count);
        int channels = _waveFormat.Channels;
        int frames = count / channels;

        float panL = Math.Min(1f, 1f - Pan);
        float panR = Math.Min(1f, 1f + Pan);

        lock (_voiceLock)
        {
            for (int i = _voices.Count - 1; i >= 0; i--)
            {
                var voice = _voices[i];
                if (voice.IsFinished)
                {
                    _voices.RemoveAt(i);
                    continue;
                }

                voice.Mix(buffer, offset, frames, channels, Volume, panL, panR);
            }
        }

        return count;
    }

    private SampleData? ResolveSample(int note)
    {
        if (_noteMap.TryGetValue(note, out var name) && _samples.TryGetValue(name, out var mapped))
        {
            return mapped;
        }

        if (!UseNearestSample || _noteMap.Count == 0) return null;

        int bestDistance = int.MaxValue;
        string? bestName = null;
        foreach (var entry in _noteMap)
        {
            int distance = Math.Abs(entry.Key - note);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestName = entry.Value;
            }
        }

        if (bestName == null) return null;
        return _samples.TryGetValue(bestName, out var best) ? best : null;
    }

    private SampleData ReadSampleData(string path)
    {
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

        return new SampleData
        {
            Path = path,
            Data = data.ToArray(),
            Channels = _waveFormat.Channels,
            SampleRate = _waveFormat.SampleRate
        };
    }

    private sealed class SampleData
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; init; } = string.Empty;
        public float[] Data { get; init; } = Array.Empty<float>();
        public int Channels { get; init; }
        public int SampleRate { get; init; }
        public SampleSettings Settings { get; } = new();
        public int FrameCount => Channels == 0 ? 0 : Data.Length / Channels;
    }

    public sealed class SampleSettings
    {
        public int RootNote { get; set; } = 60;
        public float Gain { get; set; } = 1f;
        public float Pan { get; set; } = 0f;
        public float PitchSemitones { get; set; } = 0f;
        public float PlaySpeed { get; set; } = 1f;
        public bool Loop { get; set; }
        public bool OneShot { get; set; } = true;
    }

    private sealed class Voice
    {
        private readonly SampleData _sample;
        private readonly float _rate;
        private readonly float _velocity;
        private float _position;
        private bool _releasing;
        private float _releaseGain = 1f;
        private float _releaseStep;

        public int Note { get; }
        public bool IsFinished { get; private set; }

        public Voice(SampleData sample, float rate, float velocity, int note)
        {
            _sample = sample;
            _rate = Math.Max(0f, rate);
            _velocity = Math.Clamp(velocity, 0f, 1f);
            Note = note;
        }

        public void StartRelease(float seconds)
        {
            if (_sample.Settings.OneShot) return;
            _releasing = true;
            if (seconds <= 0f)
            {
                _releaseGain = 0f;
                IsFinished = true;
                return;
            }

            _releaseStep = 1f / (seconds * Settings.SampleRate);
        }

        public void Mix(float[] buffer, int offset, int frames, int channels, float masterVolume, float panL, float panR)
        {
            if (IsFinished) return;
            var data = _sample.Data;
            int sampleChannels = _sample.Channels;
            int frameCount = _sample.FrameCount;
            if (frameCount == 0) return;

            float baseGain = masterVolume * _sample.Settings.Gain * _velocity;
            float samplePanL = Math.Min(1f, 1f - _sample.Settings.Pan);
            float samplePanR = Math.Min(1f, 1f + _sample.Settings.Pan);

            for (int i = 0; i < frames; i++)
            {
                int frameIndex = (int)_position;
                if (frameIndex >= frameCount)
                {
                    if (_sample.Settings.Loop)
                    {
                        _position = 0;
                        frameIndex = 0;
                    }
                    else
                    {
                        IsFinished = true;
                        break;
                    }
                }

                int nextIndex = Math.Min(frameIndex + 1, frameCount - 1);
                float frac = _position - frameIndex;

                int baseIdx = frameIndex * sampleChannels;
                int nextBase = nextIndex * sampleChannels;

                float left = data[baseIdx];
                float right = sampleChannels > 1 ? data[baseIdx + 1] : left;
                float leftNext = data[nextBase];
                float rightNext = sampleChannels > 1 ? data[nextBase + 1] : leftNext;

                float sampleL = left + (leftNext - left) * frac;
                float sampleR = right + (rightNext - right) * frac;

                float gain = baseGain;
                if (_releasing)
                {
                    _releaseGain = Math.Max(0f, _releaseGain - _releaseStep);
                    gain *= _releaseGain;
                    if (_releaseGain <= 0f)
                    {
                        IsFinished = true;
                        break;
                    }
                }

                int outIndex = offset + i * channels;
                buffer[outIndex] += sampleL * gain * panL * samplePanL;
                if (channels > 1)
                {
                    buffer[outIndex + 1] += sampleR * gain * panR * samplePanR;
                }

                _position += _rate;
            }
        }

        public void AdjustPosition(float deltaFrames)
        {
            if (IsFinished) return;
            _position = WrapOrClamp(_position + deltaFrames);
        }

        private float WrapOrClamp(float position)
        {
            int frameCount = _sample.FrameCount;
            if (frameCount == 0) return 0f;
            if (_sample.Settings.Loop)
            {
                float length = frameCount;
                position %= length;
                if (position < 0) position += length;
                return position;
            }
            return Math.Clamp(position, 0f, frameCount - 1);
        }
    }
}
