//Engine License (MEL) – Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Granular synthesizer for grain-based sound manipulation.


using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;


namespace MusicEngine.Core;


/// <summary>
/// Grain envelope/window shape
/// </summary>
public enum GrainEnvelope
{
    /// <summary>Gaussian bell curve (smooth)</summary>
    Gaussian,
    /// <summary>Hann window (smooth)</summary>
    Hann,
    /// <summary>Trapezoid with attack/release</summary>
    Trapezoid,
    /// <summary>Triangle</summary>
    Triangle,
    /// <summary>Rectangle (hard edges)</summary>
    Rectangle
}


/// <summary>
/// Playback mode for granular synthesis
/// </summary>
public enum GranularPlayMode
{
    /// <summary>Forward playback</summary>
    Forward,
    /// <summary>Reverse playback</summary>
    Reverse,
    /// <summary>Alternating forward/reverse</summary>
    PingPong,
    /// <summary>Random direction per grain</summary>
    Random
}


/// <summary>
/// A single grain of audio
/// </summary>
internal class Grain
{
    public int StartSample { get; set; }
    public int CurrentSample { get; set; }
    public int Length { get; set; }
    public float Pitch { get; set; } = 1.0f;
    public float Pan { get; set; } = 0f;
    public float Amplitude { get; set; } = 1.0f;
    public bool IsReverse { get; set; } = false;
    public bool IsActive { get; set; } = false;
    public double Phase { get; set; } = 0;

    public float GetEnvelopeValue(GrainEnvelope envelope)
    {
        if (Length <= 0) return 0f;

        float position = (float)CurrentSample / Length;

        return envelope switch
        {
            GrainEnvelope.Gaussian => MathF.Exp(-18f * MathF.Pow(position - 0.5f, 2)),
            GrainEnvelope.Hann => 0.5f * (1f - MathF.Cos(2f * MathF.PI * position)),
            GrainEnvelope.Trapezoid => position < 0.1f ? position * 10f :
                                       position > 0.9f ? (1f - position) * 10f : 1f,
            GrainEnvelope.Triangle => position < 0.5f ? position * 2f : (1f - position) * 2f,
            GrainEnvelope.Rectangle => 1f,
            _ => 1f
        };
    }
}


/// <summary>
/// Granular synthesizer that generates sound from small audio fragments (grains).
/// Can load samples or generate grains from built-in waveforms.
/// </summary>
public class GranularSynth : ISynth
{
    private readonly WaveFormat _waveFormat;
    private readonly List<Grain> _grains = new();
    private readonly Random _random = new();
    private readonly object _lock = new();

    // Source audio buffer (mono)
    private float[] _sourceBuffer = Array.Empty<float>();
    private int _sourceSampleRate;

    // Grain scheduling
    private double _timeSinceLastGrain = 0;
    private int _activeNotes = 0;
    private float _currentBaseFrequency = 440f;

    /// <summary>Synth name</summary>
    public string Name { get; set; } = "GranularSynth";

    /// <summary>Audio format</summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>Maximum number of simultaneous grains</summary>
    public int MaxGrains { get; set; } = 64;

    /// <summary>Master volume (0-1)</summary>
    public float Volume { get; set; } = 0.5f;

    /// <summary>
    /// Playback position in source buffer (0-1)
    /// </summary>
    public float Position { get; set; } = 0f;

    /// <summary>
    /// Position randomization amount (0-1)
    /// </summary>
    public float PositionRandom { get; set; } = 0.05f;

    /// <summary>
    /// Grain size in milliseconds
    /// </summary>
    public float GrainSize { get; set; } = 50f;

    /// <summary>
    /// Grain size randomization (0-1)
    /// </summary>
    public float GrainSizeRandom { get; set; } = 0.2f;

    /// <summary>
    /// Grain density (grains per second)
    /// </summary>
    public float Density { get; set; } = 30f;

    /// <summary>
    /// Density randomization (0-1)
    /// </summary>
    public float DensityRandom { get; set; } = 0.1f;

    /// <summary>
    /// Pitch shift in semitones
    /// </summary>
    public float PitchShift { get; set; } = 0f;

    /// <summary>
    /// Pitch randomization in semitones
    /// </summary>
    public float PitchRandom { get; set; } = 0f;

    /// <summary>
    /// Pan spread (0 = mono, 1 = full stereo spread)
    /// </summary>
    public float PanSpread { get; set; } = 0.5f;

    /// <summary>
    /// Grain envelope shape
    /// </summary>
    public GrainEnvelope Envelope { get; set; } = GrainEnvelope.Gaussian;

    /// <summary>
    /// Playback mode
    /// </summary>
    public GranularPlayMode PlayMode { get; set; } = GranularPlayMode.Forward;

    /// <summary>
    /// Reverse probability when PlayMode is Random (0-1)
    /// </summary>
    public float ReverseProbability { get; set; } = 0.3f;

    /// <summary>
    /// Whether to track pitch from MIDI notes
    /// </summary>
    public bool PitchTracking { get; set; } = true;

    /// <summary>
    /// Position modulation by LFO
    /// </summary>
    public LFO? PositionLFO { get; set; }

    /// <summary>
    /// Position LFO depth
    /// </summary>
    public float PositionLFODepth { get; set; } = 0f;

    /// <summary>
    /// Creates a granular synth
    /// </summary>
    public GranularSynth(int? sampleRate = null)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);
        _sourceSampleRate = rate;

        // Generate default source (sine wave table)
        GenerateDefaultSource();
    }

    private void GenerateDefaultSource()
    {
        // Generate a few cycles of different waveforms
        int length = 44100; // 1 second of audio
        _sourceBuffer = new float[length];
        _sourceSampleRate = 44100;

        for (int i = 0; i < length; i++)
        {
            float t = (float)i / length;
            float phase = t * 2f * MathF.PI * 4f; // 4 cycles

            // Mix of sine and saw for interesting texture
            _sourceBuffer[i] = MathF.Sin(phase) * 0.7f +
                              ((phase % (2f * MathF.PI)) / MathF.PI - 1f) * 0.3f;
        }
    }

    /// <summary>
    /// Load a sample from a WAV file
    /// </summary>
    public void LoadSample(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Sample file not found: {path}");

        using var reader = new AudioFileReader(path);
        _sourceSampleRate = reader.WaveFormat.SampleRate;

        var samples = new List<float>();
        var buffer = new float[4096];
        int read;

        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < read; i++)
            {
                samples.Add(buffer[i]);
            }
        }

        // Convert to mono if stereo
        if (reader.WaveFormat.Channels == 2)
        {
            var monoSamples = new List<float>();
            for (int i = 0; i < samples.Count - 1; i += 2)
            {
                monoSamples.Add((samples[i] + samples[i + 1]) * 0.5f);
            }
            _sourceBuffer = monoSamples.ToArray();
        }
        else
        {
            _sourceBuffer = samples.ToArray();
        }
    }

    /// <summary>
    /// Load a sample from raw data
    /// </summary>
    public void LoadSample(float[] samples, int sampleRate = 44100)
    {
        _sourceBuffer = new float[samples.Length];
        Array.Copy(samples, _sourceBuffer, samples.Length);
        _sourceSampleRate = sampleRate;
    }

    /// <summary>
    /// Generate a waveform as source
    /// </summary>
    public void GenerateSource(WaveType waveType, float frequency = 440f, float duration = 1f)
    {
        int length = (int)(_sourceSampleRate * duration);
        _sourceBuffer = new float[length];

        for (int i = 0; i < length; i++)
        {
            float phase = (float)i / _sourceSampleRate * frequency * 2f * MathF.PI;

            _sourceBuffer[i] = waveType switch
            {
                WaveType.Sine => MathF.Sin(phase),
                WaveType.Square => phase % (2f * MathF.PI) < MathF.PI ? 1f : -1f,
                WaveType.Sawtooth => (phase % (2f * MathF.PI)) / MathF.PI - 1f,
                WaveType.Triangle => MathF.Abs((phase % (2f * MathF.PI)) / MathF.PI - 1f) * 2f - 1f,
                WaveType.Noise => (float)(_random.NextDouble() * 2.0 - 1.0),
                _ => MathF.Sin(phase)
            };
        }
    }

    /// <summary>
    /// Trigger a note
    /// </summary>
    public void NoteOn(int note, int velocity)
    {
        lock (_lock)
        {
            _activeNotes++;
            _currentBaseFrequency = (float)(440.0 * Math.Pow(2.0, (note - 69.0) / 12.0));

            // Spawn initial grains
            int initialGrains = Math.Min(5, MaxGrains / 4);
            for (int i = 0; i < initialGrains; i++)
            {
                SpawnGrain(velocity / 127f);
            }
        }
    }

    /// <summary>
    /// Release a note
    /// </summary>
    public void NoteOff(int note)
    {
        lock (_lock)
        {
            _activeNotes = Math.Max(0, _activeNotes - 1);
        }
    }

    /// <summary>
    /// Release all notes
    /// </summary>
    public void AllNotesOff()
    {
        lock (_lock)
        {
            _activeNotes = 0;
            foreach (var grain in _grains)
            {
                grain.IsActive = false;
            }
        }
    }

    /// <summary>
    /// Set parameter by name
    /// </summary>
    public void SetParameter(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "volume":
                Volume = Math.Clamp(value, 0f, 1f);
                break;
            case "position":
                Position = Math.Clamp(value, 0f, 1f);
                break;
            case "positionrandom":
                PositionRandom = Math.Clamp(value, 0f, 1f);
                break;
            case "grainsize":
                GrainSize = Math.Max(1f, value);
                break;
            case "grainsizerand":
            case "grainsizeRandom":
                GrainSizeRandom = Math.Clamp(value, 0f, 1f);
                break;
            case "density":
                Density = Math.Max(1f, value);
                break;
            case "densityrandom":
                DensityRandom = Math.Clamp(value, 0f, 1f);
                break;
            case "pitch":
            case "pitchshift":
                PitchShift = value;
                break;
            case "pitchrandom":
                PitchRandom = Math.Max(0f, value);
                break;
            case "panspread":
                PanSpread = Math.Clamp(value, 0f, 1f);
                break;
            case "envelope":
                Envelope = (GrainEnvelope)(int)value;
                break;
            case "playmode":
                PlayMode = (GranularPlayMode)(int)value;
                break;
            case "reverseprobability":
                ReverseProbability = Math.Clamp(value, 0f, 1f);
                break;
            case "pitchtracking":
                PitchTracking = value > 0.5f;
                break;
            case "positionlfodepth":
                PositionLFODepth = Math.Clamp(value, 0f, 1f);
                break;
        }
    }

    private void SpawnGrain(float velocityScale = 1f)
    {
        if (_sourceBuffer.Length == 0) return;

        // Find inactive grain or create new
        Grain? grain = null;
        foreach (var g in _grains)
        {
            if (!g.IsActive)
            {
                grain = g;
                break;
            }
        }

        if (grain == null && _grains.Count < MaxGrains)
        {
            grain = new Grain();
            _grains.Add(grain);
        }

        if (grain == null) return; // Max grains reached

        // Calculate grain position with LFO modulation
        float effectivePosition = Position;
        if (PositionLFO != null && PositionLFO.Enabled)
        {
            effectivePosition += (float)PositionLFO.GetValue(_waveFormat.SampleRate) * PositionLFODepth;
        }

        // Add randomization
        float posRand = (float)(_random.NextDouble() * 2 - 1) * PositionRandom;
        effectivePosition = Math.Clamp(effectivePosition + posRand, 0f, 1f);

        // Calculate grain length with randomization
        float sizeRand = 1f + (float)(_random.NextDouble() * 2 - 1) * GrainSizeRandom;
        int grainLength = (int)(GrainSize * sizeRand * _waveFormat.SampleRate / 1000f);
        grainLength = Math.Max(64, grainLength);

        // Calculate pitch
        float pitch = MathF.Pow(2f, PitchShift / 12f);
        if (PitchRandom > 0)
        {
            float pitchRand = (float)(_random.NextDouble() * 2 - 1) * PitchRandom;
            pitch *= MathF.Pow(2f, pitchRand / 12f);
        }

        // Pitch tracking
        if (PitchTracking)
        {
            pitch *= _currentBaseFrequency / 440f;
        }

        // Determine playback direction
        bool reverse = PlayMode switch
        {
            GranularPlayMode.Forward => false,
            GranularPlayMode.Reverse => true,
            GranularPlayMode.PingPong => _grains.Count(g => g.IsActive) % 2 == 1,
            GranularPlayMode.Random => _random.NextDouble() < ReverseProbability,
            _ => false
        };

        // Calculate start position in source
        int startSample = (int)(effectivePosition * (_sourceBuffer.Length - grainLength));
        startSample = Math.Clamp(startSample, 0, _sourceBuffer.Length - grainLength - 1);

        // Configure grain
        grain.StartSample = startSample;
        grain.CurrentSample = 0;
        grain.Length = grainLength;
        grain.Pitch = pitch;
        grain.Pan = (float)(_random.NextDouble() * 2 - 1) * PanSpread;
        grain.Amplitude = velocityScale;
        grain.IsReverse = reverse;
        grain.IsActive = true;
        grain.Phase = 0;
    }

    /// <summary>
    /// Read audio samples
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        // Clear buffer
        for (int i = 0; i < count; i++)
        {
            buffer[offset + i] = 0;
        }

        if (_sourceBuffer.Length == 0) return count;

        int channels = _waveFormat.Channels;
        double deltaTime = 1.0 / _waveFormat.SampleRate;

        lock (_lock)
        {
            for (int n = 0; n < count; n += channels)
            {
                // Spawn new grains based on density
                if (_activeNotes > 0)
                {
                    _timeSinceLastGrain += deltaTime;

                    float effectiveDensity = Density * (1f + (float)(_random.NextDouble() * 2 - 1) * DensityRandom);
                    double grainInterval = 1.0 / effectiveDensity;

                    if (_timeSinceLastGrain >= grainInterval)
                    {
                        SpawnGrain();
                        _timeSinceLastGrain = 0;
                    }
                }

                // Process all active grains
                float leftSample = 0f;
                float rightSample = 0f;

                foreach (var grain in _grains)
                {
                    if (!grain.IsActive) continue;

                    // Calculate source sample position with pitch
                    grain.Phase += grain.Pitch;
                    int sampleIndex;

                    if (grain.IsReverse)
                    {
                        sampleIndex = grain.StartSample + grain.Length - 1 - (int)grain.Phase;
                    }
                    else
                    {
                        sampleIndex = grain.StartSample + (int)grain.Phase;
                    }

                    // Check bounds
                    if (sampleIndex < 0 || sampleIndex >= _sourceBuffer.Length ||
                        grain.Phase >= grain.Length)
                    {
                        grain.IsActive = false;
                        continue;
                    }

                    // Get sample with linear interpolation
                    float frac = (float)(grain.Phase - (int)grain.Phase);
                    int nextIndex = Math.Min(sampleIndex + 1, _sourceBuffer.Length - 1);
                    float sample = _sourceBuffer[sampleIndex] * (1f - frac) +
                                  _sourceBuffer[nextIndex] * frac;

                    // Apply grain envelope
                    grain.CurrentSample = (int)grain.Phase;
                    float env = grain.GetEnvelopeValue(Envelope);
                    sample *= env * grain.Amplitude;

                    // Apply panning
                    float leftGain = MathF.Cos((grain.Pan + 1f) * MathF.PI / 4f);
                    float rightGain = MathF.Sin((grain.Pan + 1f) * MathF.PI / 4f);

                    leftSample += sample * leftGain;
                    rightSample += sample * rightGain;
                }

                // Normalize by approximate grain count to prevent clipping
                int activeCount = _grains.Count(g => g.IsActive);
                if (activeCount > 1)
                {
                    float normalize = 1f / MathF.Sqrt(activeCount);
                    leftSample *= normalize;
                    rightSample *= normalize;
                }

                // Apply volume
                leftSample *= Volume;
                rightSample *= Volume;

                // Soft clipping
                leftSample = MathF.Tanh(leftSample);
                rightSample = MathF.Tanh(rightSample);

                // Output
                if (channels >= 2)
                {
                    buffer[offset + n] = leftSample;
                    buffer[offset + n + 1] = rightSample;
                }
                else
                {
                    buffer[offset + n] = (leftSample + rightSample) * 0.5f;
                }
            }
        }

        return count;
    }

    /// <summary>
    /// Create a pad preset
    /// </summary>
    public static GranularSynth CreatePadPreset()
    {
        var synth = new GranularSynth();
        synth.Name = "Granular Pad";
        synth.GrainSize = 80f;
        synth.GrainSizeRandom = 0.3f;
        synth.Density = 25f;
        synth.PositionRandom = 0.1f;
        synth.PitchRandom = 0.1f;
        synth.PanSpread = 0.8f;
        synth.Envelope = GrainEnvelope.Gaussian;
        return synth;
    }

    /// <summary>
    /// Create a texture preset
    /// </summary>
    public static GranularSynth CreateTexturePreset()
    {
        var synth = new GranularSynth();
        synth.Name = "Granular Texture";
        synth.GrainSize = 30f;
        synth.GrainSizeRandom = 0.5f;
        synth.Density = 50f;
        synth.PositionRandom = 0.3f;
        synth.PitchRandom = 0.5f;
        synth.PanSpread = 1.0f;
        synth.PlayMode = GranularPlayMode.Random;
        synth.ReverseProbability = 0.4f;
        synth.Envelope = GrainEnvelope.Hann;
        return synth;
    }

    /// <summary>
    /// Create a freeze/sustain preset
    /// </summary>
    public static GranularSynth CreateFreezePreset()
    {
        var synth = new GranularSynth();
        synth.Name = "Granular Freeze";
        synth.GrainSize = 100f;
        synth.GrainSizeRandom = 0.1f;
        synth.Density = 20f;
        synth.PositionRandom = 0.02f; // Very small position variance
        synth.PitchRandom = 0f;
        synth.PanSpread = 0.3f;
        synth.Envelope = GrainEnvelope.Gaussian;
        synth.PitchTracking = false;
        return synth;
    }
}
