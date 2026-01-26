// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MusicEngine.Core;

/// <summary>
/// Represents a loaded audio sample with metadata.
/// </summary>
public class Sample
{
    public string Name { get; set; } = "";
    public string FilePath { get; set; } = "";
    public float[] AudioData { get; set; } = Array.Empty<float>();
    public WaveFormat WaveFormat { get; set; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
    public int RootNote { get; set; } = 60; // Middle C by default
    public float Volume { get; set; } = 1.0f;
    public int LowNote { get; set; } = 0;   // Range for multi-sample mapping
    public int HighNote { get; set; } = 127;
}

/// <summary>
/// A voice that plays a sample with pitch shifting.
/// </summary>
public class SampleVoice
{
    public Sample? Sample { get; set; }
    public int Note { get; set; }
    public int Velocity { get; set; }
    public bool IsActive { get; set; }
    public double Position { get; set; }
    public double PlaybackRate { get; set; } = 1.0;
    public float VelocityGain { get; set; } = 1.0f;

    public void Start(Sample sample, int note, int velocity)
    {
        Sample = sample;
        Note = note;
        Velocity = velocity;
        IsActive = true;
        Position = 0;

        // Calculate playback rate for pitch shifting
        // Each semitone is 2^(1/12) ratio
        int semitones = note - sample.RootNote;
        PlaybackRate = Math.Pow(2.0, semitones / 12.0);
        VelocityGain = velocity / 127f;
    }

    public void Stop()
    {
        IsActive = false;
    }
}

/// <summary>
/// Sample-based instrument that can load and play audio files.
/// Supports multi-sampling and pitch-shifting.
/// </summary>
public class SampleInstrument : ISampleProvider, ISynth
{
    private readonly List<Sample> _samples = new();
    private readonly List<SampleVoice> _voices = new();
    private readonly Dictionary<int, Sample> _noteMappings = new(); // Direct note-to-sample mapping
    private readonly object _lock = new();

    private float _masterVolume = 1.0f;
    private int _maxVoices = 32;
    private string _name = "Sampler";
    private string? _sampleDirectory;

    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

    public string Name
    {
        get => _name;
        set => _name = value;
    }

    public float Volume
    {
        get => _masterVolume;
        set => _masterVolume = Math.Clamp(value, 0f, 2f);
    }

    public int MaxVoices
    {
        get => _maxVoices;
        set => _maxVoices = Math.Max(1, value);
    }

    /// <summary>
    /// Sets the directory to search for samples.
    /// </summary>
    public void SetSampleDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            _sampleDirectory = path;
        }
    }

    /// <summary>
    /// Loads a sample from a file path.
    /// </summary>
    public Sample? LoadSample(string pathOrName, int rootNote = 60)
    {
        string filePath = ResolveSamplePath(pathOrName);

        if (!File.Exists(filePath))
        {
            Console.WriteLine($"[SampleInstrument] Sample not found: {filePath}");
            return null;
        }

        try
        {
            using var reader = new AudioFileReader(filePath);
            var sampleData = new List<float>();
            var buffer = new float[reader.WaveFormat.SampleRate * reader.WaveFormat.Channels];
            int samplesRead;

            while ((samplesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < samplesRead; i++)
                {
                    sampleData.Add(buffer[i]);
                }
            }

            // Convert to stereo if mono
            float[] audioData;
            WaveFormat format;

            if (reader.WaveFormat.Channels == 1)
            {
                audioData = new float[sampleData.Count * 2];
                for (int i = 0; i < sampleData.Count; i++)
                {
                    audioData[i * 2] = sampleData[i];
                    audioData[i * 2 + 1] = sampleData[i];
                }
                format = WaveFormat.CreateIeeeFloatWaveFormat(reader.WaveFormat.SampleRate, 2);
            }
            else
            {
                audioData = sampleData.ToArray();
                format = WaveFormat.CreateIeeeFloatWaveFormat(reader.WaveFormat.SampleRate, reader.WaveFormat.Channels);
            }

            var sample = new Sample
            {
                Name = Path.GetFileNameWithoutExtension(filePath),
                FilePath = filePath,
                AudioData = audioData,
                WaveFormat = format,
                RootNote = rootNote
            };

            lock (_lock)
            {
                _samples.Add(sample);
            }

            Console.WriteLine($"[SampleInstrument] Loaded sample: {sample.Name} ({audioData.Length / 2} frames)");
            return sample;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SampleInstrument] Error loading sample {pathOrName}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Maps a sample to a specific MIDI note (for drum pads, etc.).
    /// </summary>
    public void MapSampleToNote(Sample sample, int note)
    {
        lock (_lock)
        {
            _noteMappings[note] = sample;
        }
    }

    /// <summary>
    /// Maps a sample to a specific MIDI note by sample name.
    /// </summary>
    public void MapSampleToNote(string sampleName, int note)
    {
        var sample = _samples.FirstOrDefault(s => s.Name.Equals(sampleName, StringComparison.OrdinalIgnoreCase));
        if (sample != null)
        {
            MapSampleToNote(sample, note);
        }
    }

    /// <summary>
    /// Sets the range of notes a sample should respond to.
    /// </summary>
    public void SetSampleRange(Sample sample, int lowNote, int highNote)
    {
        sample.LowNote = lowNote;
        sample.HighNote = highNote;
    }

    /// <summary>
    /// Gets a sample by name.
    /// </summary>
    public Sample? GetSample(string name)
    {
        lock (_lock)
        {
            return _samples.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }

    private string ResolveSamplePath(string pathOrName)
    {
        // If it's already a full path and exists, use it
        if (Path.IsPathRooted(pathOrName) && File.Exists(pathOrName))
        {
            return pathOrName;
        }

        // Try the sample directory if set
        if (!string.IsNullOrEmpty(_sampleDirectory))
        {
            var dirPath = Path.Combine(_sampleDirectory, pathOrName);
            if (File.Exists(dirPath)) return dirPath;

            // Try common extensions
            foreach (var ext in new[] { ".wav", ".mp3", ".flac", ".ogg", ".aiff" })
            {
                var withExt = Path.Combine(_sampleDirectory, pathOrName + ext);
                if (File.Exists(withExt)) return withExt;
            }
        }

        // Try current directory
        if (File.Exists(pathOrName)) return pathOrName;

        // Try with extensions in current directory
        foreach (var ext in new[] { ".wav", ".mp3", ".flac", ".ogg", ".aiff" })
        {
            var withExt = pathOrName + ext;
            if (File.Exists(withExt)) return withExt;
        }

        return pathOrName;
    }

    private Sample? FindSampleForNote(int note)
    {
        // First check direct mappings
        if (_noteMappings.TryGetValue(note, out var mapped))
        {
            return mapped;
        }

        // Then find a sample whose range includes this note
        lock (_lock)
        {
            return _samples.FirstOrDefault(s => note >= s.LowNote && note <= s.HighNote);
        }
    }

    private SampleVoice? GetFreeVoice()
    {
        lock (_lock)
        {
            // Find inactive voice
            var voice = _voices.FirstOrDefault(v => !v.IsActive);
            if (voice != null) return voice;

            // Create new voice if under limit
            if (_voices.Count < _maxVoices)
            {
                voice = new SampleVoice();
                _voices.Add(voice);
                return voice;
            }

            // Steal oldest voice
            return _voices.OrderBy(v => v.Position).FirstOrDefault();
        }
    }

    #region ISynth Implementation

    public void NoteOn(int note, int velocity)
    {
        var sample = FindSampleForNote(note);
        if (sample == null) return;

        var voice = GetFreeVoice();
        if (voice == null) return;

        voice.Start(sample, note, velocity);
    }

    public void NoteOff(int note)
    {
        lock (_lock)
        {
            foreach (var voice in _voices.Where(v => v.IsActive && v.Note == note))
            {
                voice.Stop();
            }
        }
    }

    public void AllNotesOff()
    {
        lock (_lock)
        {
            foreach (var voice in _voices)
            {
                voice.Stop();
            }
        }
    }

    public void SetParameter(string name, float value)
    {
        switch (name.ToLower())
        {
            case "volume":
            case "gain":
            case "level":
                Volume = value;
                break;
        }
    }

    #endregion

    #region ISampleProvider Implementation

    public int Read(float[] buffer, int offset, int count)
    {
        // Clear buffer
        Array.Clear(buffer, offset, count);

        lock (_lock)
        {
            foreach (var voice in _voices.Where(v => v.IsActive && v.Sample != null))
            {
                var sample = voice.Sample!;
                int channels = sample.WaveFormat.Channels;
                double sampleRateRatio = (double)sample.WaveFormat.SampleRate / WaveFormat.SampleRate;

                for (int i = 0; i < count; i += 2)
                {
                    // Calculate position in source sample
                    double srcPos = voice.Position * sampleRateRatio * voice.PlaybackRate;
                    int srcIndex = (int)srcPos * channels;

                    if (srcIndex >= sample.AudioData.Length - channels)
                    {
                        voice.Stop();
                        break;
                    }

                    // Linear interpolation for smoother playback
                    double frac = srcPos * channels - srcIndex;

                    float left, right;
                    if (channels >= 2)
                    {
                        left = Lerp(sample.AudioData[srcIndex],
                                   srcIndex + 2 < sample.AudioData.Length ? sample.AudioData[srcIndex + 2] : 0,
                                   (float)frac);
                        right = Lerp(sample.AudioData[srcIndex + 1],
                                    srcIndex + 3 < sample.AudioData.Length ? sample.AudioData[srcIndex + 3] : 0,
                                    (float)frac);
                    }
                    else
                    {
                        left = right = Lerp(sample.AudioData[srcIndex],
                                           srcIndex + 1 < sample.AudioData.Length ? sample.AudioData[srcIndex + 1] : 0,
                                           (float)frac);
                    }

                    float gain = _masterVolume * sample.Volume * voice.VelocityGain;
                    buffer[offset + i] += left * gain;
                    buffer[offset + i + 1] += right * gain;

                    voice.Position += 1.0 / WaveFormat.SampleRate;
                }
            }
        }

        return count;
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    #endregion
}
