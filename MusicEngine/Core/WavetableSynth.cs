// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;


namespace MusicEngine.Core;


/// <summary>
/// Built-in wavetable types
/// </summary>
public enum WavetableType
{
    /// <summary>Basic waveforms: Sine, Triangle, Saw, Square</summary>
    Basic,
    /// <summary>PWM sweep from thin to full square</summary>
    PWM,
    /// <summary>Vocal formants</summary>
    Vocal,
    /// <summary>Digital/harsh waveforms</summary>
    Digital,
    /// <summary>Soft analog-style waveforms</summary>
    Analog,
    /// <summary>Harmonic series</summary>
    Harmonic
}


/// <summary>
/// Wavetable synthesizer with morphing between wave positions.
/// Supports loading custom wavetables or using built-in ones.
/// </summary>
public class WavetableSynth : ISynth
{
    private readonly WaveFormat _waveFormat;
    private readonly List<WavetableVoice> _voices = new();
    private readonly Dictionary<int, WavetableVoice> _noteToVoice = new();
    private readonly object _lock = new();

    // Wavetable data: [frame][sample]
    private float[][] _wavetable = Array.Empty<float[]>();
    private int _frameSize = 2048; // Samples per wavetable frame
    private int _frameCount = 256; // Number of frames in wavetable

    /// <summary>Synth name for identification</summary>
    public string Name { get; set; } = "WavetableSynth";

    /// <summary>Audio format</summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>Maximum polyphony</summary>
    public int MaxVoices { get; set; } = 16;

    /// <summary>
    /// Wavetable position (0-1). Morphs between frames.
    /// </summary>
    public float Position { get; set; } = 0f;

    /// <summary>Master volume (0-1)</summary>
    public float Volume { get; set; } = 0.5f;

    /// <summary>Filter cutoff (0-1)</summary>
    public float FilterCutoff { get; set; } = 1.0f;

    /// <summary>Filter resonance (0-1)</summary>
    public float FilterResonance { get; set; } = 0f;

    /// <summary>Amplitude envelope</summary>
    public Envelope AmpEnvelope { get; }

    /// <summary>Position modulation envelope amount</summary>
    public float PositionEnvAmount { get; set; } = 0f;

    /// <summary>Position modulation envelope</summary>
    public Envelope PositionEnvelope { get; }

    /// <summary>LFO for position modulation</summary>
    public LFO? PositionLFO { get; set; }

    /// <summary>Position LFO depth (0-1)</summary>
    public float PositionLFODepth { get; set; } = 0f;

    /// <summary>Detune amount in cents</summary>
    public float Detune { get; set; } = 0f;

    /// <summary>Number of unison voices</summary>
    public int UnisonVoices { get; set; } = 1;

    /// <summary>Unison detune spread in cents</summary>
    public float UnisonDetune { get; set; } = 10f;

    /// <summary>Unison stereo spread (0-1)</summary>
    public float UnisonSpread { get; set; } = 0.5f;

    /// <summary>
    /// Creates a wavetable synth with a built-in wavetable
    /// </summary>
    public WavetableSynth(WavetableType type = WavetableType.Basic, int? sampleRate = null)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);

        AmpEnvelope = new Envelope(0.01, 0.1, 0.7, 0.3);
        PositionEnvelope = new Envelope(0.01, 0.5, 0.0, 0.3);

        // Generate built-in wavetable
        GenerateBuiltInWavetable(type);
    }

    /// <summary>
    /// Generates a built-in wavetable
    /// </summary>
    public void GenerateBuiltInWavetable(WavetableType type)
    {
        _frameCount = 256;
        _frameSize = 2048;
        _wavetable = new float[_frameCount][];

        for (int frame = 0; frame < _frameCount; frame++)
        {
            _wavetable[frame] = new float[_frameSize];
            float morphPosition = (float)frame / (_frameCount - 1);

            for (int i = 0; i < _frameSize; i++)
            {
                float phase = (float)i / _frameSize * 2f * MathF.PI;
                _wavetable[frame][i] = GenerateWavetableSample(type, phase, morphPosition);
            }
        }
    }

    private float GenerateWavetableSample(WavetableType type, float phase, float morph)
    {
        return type switch
        {
            WavetableType.Basic => GenerateBasicSample(phase, morph),
            WavetableType.PWM => GeneratePWMSample(phase, morph),
            WavetableType.Vocal => GenerateVocalSample(phase, morph),
            WavetableType.Digital => GenerateDigitalSample(phase, morph),
            WavetableType.Analog => GenerateAnalogSample(phase, morph),
            WavetableType.Harmonic => GenerateHarmonicSample(phase, morph),
            _ => MathF.Sin(phase)
        };
    }

    private float GenerateBasicSample(float phase, float morph)
    {
        // Morph: 0=Sine, 0.33=Triangle, 0.66=Saw, 1=Square
        if (morph < 0.33f)
        {
            float t = morph / 0.33f;
            float sine = MathF.Sin(phase);
            float tri = phase < MathF.PI
                ? (2f * phase / MathF.PI - 1f)
                : (3f - 2f * phase / MathF.PI);
            return sine * (1f - t) + tri * t;
        }
        else if (morph < 0.66f)
        {
            float t = (morph - 0.33f) / 0.33f;
            float tri = phase < MathF.PI
                ? (2f * phase / MathF.PI - 1f)
                : (3f - 2f * phase / MathF.PI);
            float saw = (phase / MathF.PI) - 1f;
            return tri * (1f - t) + saw * t;
        }
        else
        {
            float t = (morph - 0.66f) / 0.34f;
            float saw = (phase / MathF.PI) - 1f;
            float square = phase < MathF.PI ? 1f : -1f;
            return saw * (1f - t) + square * t;
        }
    }

    private float GeneratePWMSample(float phase, float morph)
    {
        // PWM from 5% to 95% duty cycle
        float pulseWidth = 0.05f + morph * 0.9f;
        return phase < (2f * MathF.PI * pulseWidth) ? 1f : -1f;
    }

    private float GenerateVocalSample(float phase, float morph)
    {
        // Simulate vocal formants using additive synthesis
        float sample = 0f;

        // Base formant frequencies shift with morph (A -> E -> I -> O -> U)
        float[] formant1 = { 800f, 400f, 280f, 450f, 325f };
        float[] formant2 = { 1200f, 2000f, 2250f, 800f, 700f };

        int vowel1 = (int)(morph * 4f);
        int vowel2 = Math.Min(vowel1 + 1, 4);
        float vowelMix = (morph * 4f) - vowel1;

        float f1 = formant1[vowel1] * (1f - vowelMix) + formant1[vowel2] * vowelMix;
        float f2 = formant2[vowel1] * (1f - vowelMix) + formant2[vowel2] * vowelMix;

        // Generate harmonics with formant envelope
        for (int h = 1; h <= 32; h++)
        {
            float freq = h * 100f; // Base frequency
            float amp = 1f / h;

            // Apply formant envelope
            float dist1 = MathF.Abs(freq - f1) / 200f;
            float dist2 = MathF.Abs(freq - f2) / 300f;
            float formantAmp = MathF.Exp(-dist1 * dist1) * 0.7f + MathF.Exp(-dist2 * dist2) * 0.5f;

            sample += MathF.Sin(phase * h) * amp * formantAmp;
        }

        return sample * 0.5f;
    }

    private float GenerateDigitalSample(float phase, float morph)
    {
        // Harsh digital waveforms with aliasing and bit reduction
        float sample;

        if (morph < 0.5f)
        {
            // Staircase wave (quantized sine)
            int steps = 4 + (int)(morph * 24f);
            sample = MathF.Sin(phase);
            sample = MathF.Round(sample * steps) / steps;
        }
        else
        {
            // Ring modulated waveforms
            float modFreq = 2f + (morph - 0.5f) * 14f;
            sample = MathF.Sin(phase) * MathF.Sin(phase * modFreq);
        }

        return sample;
    }

    private float GenerateAnalogSample(float phase, float morph)
    {
        // Soft analog-style waveforms with slight imperfections
        float sample = 0f;

        // Supersaw-style with detuning
        int numOscs = 3 + (int)(morph * 4f);
        float detuneAmount = 0.01f + morph * 0.03f;

        for (int i = 0; i < numOscs; i++)
        {
            float detune = (i - numOscs / 2f) * detuneAmount;
            float oscPhase = phase * (1f + detune);
            oscPhase = oscPhase % (2f * MathF.PI);

            // Soft saw with rounded edges
            float saw = (oscPhase / MathF.PI) - 1f;
            saw = MathF.Tanh(saw * 2f) * 0.7f; // Soft saturation

            sample += saw / numOscs;
        }

        return sample;
    }

    private float GenerateHarmonicSample(float phase, float morph)
    {
        // Additive synthesis with harmonic series
        float sample = 0f;
        int maxHarmonics = 1 + (int)(morph * 31f);

        for (int h = 1; h <= maxHarmonics; h++)
        {
            float amp = 1f / h;
            // Alternate between odd and even harmonics based on morph
            if (morph > 0.5f && h % 2 == 0)
            {
                amp *= (morph - 0.5f) * 2f;
            }
            sample += MathF.Sin(phase * h) * amp;
        }

        return sample / MathF.Sqrt(maxHarmonics);
    }

    /// <summary>
    /// Load a custom wavetable from a WAV file.
    /// The WAV should contain multiple cycles concatenated.
    /// </summary>
    public void LoadWavetable(string path, int frameSize = 2048)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Wavetable file not found: {path}");

        using var reader = new AudioFileReader(path);
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

        // If stereo, convert to mono
        if (reader.WaveFormat.Channels == 2)
        {
            var monoSamples = new List<float>();
            for (int i = 0; i < samples.Count - 1; i += 2)
            {
                monoSamples.Add((samples[i] + samples[i + 1]) * 0.5f);
            }
            samples = monoSamples;
        }

        _frameSize = frameSize;
        _frameCount = samples.Count / frameSize;

        if (_frameCount < 1)
        {
            _frameCount = 1;
            _frameSize = samples.Count;
        }

        _wavetable = new float[_frameCount][];

        for (int frame = 0; frame < _frameCount; frame++)
        {
            _wavetable[frame] = new float[_frameSize];
            int startIdx = frame * frameSize;

            for (int i = 0; i < _frameSize && startIdx + i < samples.Count; i++)
            {
                _wavetable[frame][i] = samples[startIdx + i];
            }
        }
    }

    /// <summary>
    /// Load a wavetable from raw sample data
    /// </summary>
    public void LoadWavetable(float[] samples, int frameSize = 2048)
    {
        _frameSize = frameSize;
        _frameCount = samples.Length / frameSize;

        if (_frameCount < 1)
        {
            _frameCount = 1;
            _frameSize = samples.Length;
        }

        _wavetable = new float[_frameCount][];

        for (int frame = 0; frame < _frameCount; frame++)
        {
            _wavetable[frame] = new float[_frameSize];
            int startIdx = frame * frameSize;

            for (int i = 0; i < _frameSize && startIdx + i < samples.Length; i++)
            {
                _wavetable[frame][i] = samples[startIdx + i];
            }
        }
    }

    /// <summary>
    /// Trigger a note
    /// </summary>
    public void NoteOn(int note, int velocity)
    {
        lock (_lock)
        {
            // Check if note already playing
            if (_noteToVoice.TryGetValue(note, out var existingVoice))
            {
                existingVoice.Trigger(note, velocity);
                return;
            }

            // Find or create voice
            WavetableVoice? voice = null;

            // Look for inactive voice
            foreach (var v in _voices)
            {
                if (!v.IsActive)
                {
                    voice = v;
                    break;
                }
            }

            // Create new voice if needed
            if (voice == null && _voices.Count < MaxVoices)
            {
                voice = new WavetableVoice(_waveFormat.SampleRate, this);
                _voices.Add(voice);
            }

            // Voice stealing: steal oldest
            if (voice == null && _voices.Count > 0)
            {
                voice = _voices[0];
                DateTime oldest = voice.TriggerTime;
                foreach (var v in _voices)
                {
                    if (v.TriggerTime < oldest)
                    {
                        oldest = v.TriggerTime;
                        voice = v;
                    }
                }

                // Remove old mapping
                int oldNote = voice.Note;
                if (_noteToVoice.ContainsKey(oldNote))
                {
                    _noteToVoice.Remove(oldNote);
                }
            }

            if (voice != null)
            {
                voice.Trigger(note, velocity);
                _noteToVoice[note] = voice;
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
            if (_noteToVoice.TryGetValue(note, out var voice))
            {
                voice.Release();
                _noteToVoice.Remove(note);
            }
        }
    }

    /// <summary>
    /// Release all notes
    /// </summary>
    public void AllNotesOff()
    {
        lock (_lock)
        {
            foreach (var voice in _voices)
            {
                voice.Release();
            }
            _noteToVoice.Clear();
        }
    }

    /// <summary>
    /// Set a parameter by name
    /// </summary>
    public void SetParameter(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "position":
            case "wavetableposition":
                Position = Math.Clamp(value, 0f, 1f);
                break;
            case "volume":
                Volume = Math.Clamp(value, 0f, 1f);
                break;
            case "cutoff":
            case "filtercutoff":
                FilterCutoff = Math.Clamp(value, 0f, 1f);
                break;
            case "resonance":
            case "filterresonance":
                FilterResonance = Math.Clamp(value, 0f, 1f);
                break;
            case "attack":
                AmpEnvelope.Attack = value;
                break;
            case "decay":
                AmpEnvelope.Decay = value;
                break;
            case "sustain":
                AmpEnvelope.Sustain = value;
                break;
            case "release":
                AmpEnvelope.Release = value;
                break;
            case "detune":
                Detune = value;
                break;
            case "unisonvoices":
                UnisonVoices = Math.Clamp((int)value, 1, 8);
                break;
            case "unisondetune":
                UnisonDetune = value;
                break;
            case "unisonspread":
                UnisonSpread = Math.Clamp(value, 0f, 1f);
                break;
            case "positionenvamount":
                PositionEnvAmount = Math.Clamp(value, -1f, 1f);
                break;
            case "positionlfodepth":
                PositionLFODepth = Math.Clamp(value, 0f, 1f);
                break;
        }
    }

    /// <summary>
    /// Get a sample from the wavetable with interpolation
    /// </summary>
    internal float GetSample(float phase, float position)
    {
        if (_wavetable == null || _wavetable.Length == 0)
            return 0f;

        // Clamp position
        position = Math.Clamp(position, 0f, 1f);

        // Calculate frame indices for interpolation
        float framePos = position * (_frameCount - 1);
        int frame1 = (int)framePos;
        int frame2 = Math.Min(frame1 + 1, _frameCount - 1);
        float frameMix = framePos - frame1;

        // Calculate sample index
        float samplePos = (phase / (2f * MathF.PI)) * _frameSize;
        int sample1 = (int)samplePos % _frameSize;
        int sample2 = (sample1 + 1) % _frameSize;
        float sampleMix = samplePos - (int)samplePos;

        // Bilinear interpolation
        float s1 = _wavetable[frame1][sample1] * (1f - sampleMix) +
                   _wavetable[frame1][sample2] * sampleMix;
        float s2 = _wavetable[frame2][sample1] * (1f - sampleMix) +
                   _wavetable[frame2][sample2] * sampleMix;

        return s1 * (1f - frameMix) + s2 * frameMix;
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

        int channels = _waveFormat.Channels;
        double deltaTime = 1.0 / _waveFormat.SampleRate;

        // Calculate modulated position
        float positionMod = 0f;
        if (PositionLFO != null && PositionLFO.Enabled)
        {
            positionMod = (float)PositionLFO.GetValue(_waveFormat.SampleRate) * PositionLFODepth;
        }

        lock (_lock)
        {
            for (int n = 0; n < count; n += channels)
            {
                float leftSample = 0f;
                float rightSample = 0f;

                foreach (var voice in _voices)
                {
                    if (!voice.IsActive) continue;

                    var (left, right) = voice.Process(deltaTime, Position + positionMod);
                    leftSample += left;
                    rightSample += right;
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
    /// Create a preset: Classic wavetable pad
    /// </summary>
    public static WavetableSynth CreatePadPreset()
    {
        var synth = new WavetableSynth(WavetableType.Analog);
        synth.Name = "WT Pad";
        synth.Position = 0.3f;
        synth.AmpEnvelope.Attack = 0.5;
        synth.AmpEnvelope.Decay = 0.5;
        synth.AmpEnvelope.Sustain = 0.8;
        synth.AmpEnvelope.Release = 1.0;
        synth.UnisonVoices = 4;
        synth.UnisonDetune = 15f;
        synth.UnisonSpread = 0.7f;
        synth.FilterCutoff = 0.6f;
        return synth;
    }

    /// <summary>
    /// Create a preset: Digital lead
    /// </summary>
    public static WavetableSynth CreateLeadPreset()
    {
        var synth = new WavetableSynth(WavetableType.Digital);
        synth.Name = "WT Lead";
        synth.Position = 0.5f;
        synth.AmpEnvelope.Attack = 0.01;
        synth.AmpEnvelope.Decay = 0.2;
        synth.AmpEnvelope.Sustain = 0.7;
        synth.AmpEnvelope.Release = 0.2;
        synth.FilterCutoff = 0.8f;
        synth.FilterResonance = 0.3f;
        return synth;
    }

    /// <summary>
    /// Create a preset: Vocal synth
    /// </summary>
    public static WavetableSynth CreateVocalPreset()
    {
        var synth = new WavetableSynth(WavetableType.Vocal);
        synth.Name = "WT Vocal";
        synth.Position = 0f;
        synth.PositionLFO = new LFO { Frequency = 0.5, Waveform = LfoWaveform.Triangle, Enabled = true };
        synth.PositionLFODepth = 0.3f;
        synth.AmpEnvelope.Attack = 0.1;
        synth.AmpEnvelope.Decay = 0.3;
        synth.AmpEnvelope.Sustain = 0.6;
        synth.AmpEnvelope.Release = 0.4;
        return synth;
    }
}


/// <summary>
/// Internal voice for wavetable synth
/// </summary>
internal class WavetableVoice
{
    private readonly int _sampleRate;
    private readonly WavetableSynth _synth;
    private readonly Envelope _ampEnv;
    private readonly Envelope _posEnv;
    private double[] _phases;
    private float[] _panPositions;

    // Filter state
    private float _filterState1;
    private float _filterState2;

    public int Note { get; private set; }
    public int Velocity { get; private set; }
    public double Frequency { get; private set; }
    public DateTime TriggerTime { get; private set; }
    public bool IsActive => _ampEnv.IsActive;

    public WavetableVoice(int sampleRate, WavetableSynth synth)
    {
        _sampleRate = sampleRate;
        _synth = synth;
        _ampEnv = new Envelope(0.01, 0.1, 0.7, 0.3);
        _posEnv = new Envelope(0.01, 0.5, 0.0, 0.3);
        _phases = new double[8];
        _panPositions = new float[8];
    }

    public void Trigger(int note, int velocity)
    {
        Note = note;
        Velocity = velocity;
        Frequency = 440.0 * Math.Pow(2.0, (note - 69.0) / 12.0);
        TriggerTime = DateTime.Now;

        // Copy envelope settings
        _ampEnv.Attack = _synth.AmpEnvelope.Attack;
        _ampEnv.Decay = _synth.AmpEnvelope.Decay;
        _ampEnv.Sustain = _synth.AmpEnvelope.Sustain;
        _ampEnv.Release = _synth.AmpEnvelope.Release;

        _posEnv.Attack = _synth.PositionEnvelope.Attack;
        _posEnv.Decay = _synth.PositionEnvelope.Decay;
        _posEnv.Sustain = _synth.PositionEnvelope.Sustain;
        _posEnv.Release = _synth.PositionEnvelope.Release;

        // Reset phases
        for (int i = 0; i < _phases.Length; i++)
        {
            _phases[i] = 0;
        }

        // Calculate pan positions for unison voices
        int unisonCount = _synth.UnisonVoices;
        for (int i = 0; i < _panPositions.Length; i++)
        {
            if (unisonCount == 1)
            {
                _panPositions[i] = 0f; // Center
            }
            else
            {
                float t = (float)i / (unisonCount - 1);
                _panPositions[i] = (t * 2f - 1f) * _synth.UnisonSpread;
            }
        }

        // Reset filter
        _filterState1 = 0;
        _filterState2 = 0;

        _ampEnv.Trigger(velocity);
        _posEnv.Trigger(velocity);
    }

    public void Release()
    {
        _ampEnv.Release_Gate();
        _posEnv.Release_Gate();
    }

    public (float left, float right) Process(double deltaTime, float basePosition)
    {
        if (!IsActive) return (0f, 0f);

        double ampEnv = _ampEnv.Process(deltaTime);
        double posEnv = _posEnv.Process(deltaTime);

        if (_ampEnv.Stage == EnvelopeStage.Idle) return (0f, 0f);

        // Calculate effective position
        float position = basePosition + (float)(posEnv * _synth.PositionEnvAmount);
        position = Math.Clamp(position, 0f, 1f);

        float leftSample = 0f;
        float rightSample = 0f;

        int unisonCount = _synth.UnisonVoices;

        for (int u = 0; u < unisonCount; u++)
        {
            // Calculate detuned frequency
            float detuneCents = _synth.Detune;
            if (unisonCount > 1)
            {
                float t = (float)u / (unisonCount - 1) - 0.5f;
                detuneCents += t * _synth.UnisonDetune * 2f;
            }

            double freq = Frequency * Math.Pow(2.0, detuneCents / 1200.0);
            double phaseInc = 2.0 * Math.PI * freq / _sampleRate;

            _phases[u] += phaseInc;
            if (_phases[u] >= 2.0 * Math.PI)
                _phases[u] -= 2.0 * Math.PI;

            // Get wavetable sample
            float sample = _synth.GetSample((float)_phases[u], position);

            // Apply panning
            float pan = _panPositions[u];
            float leftGain = MathF.Cos((pan + 1f) * MathF.PI / 4f);
            float rightGain = MathF.Sin((pan + 1f) * MathF.PI / 4f);

            leftSample += sample * leftGain / unisonCount;
            rightSample += sample * rightGain / unisonCount;
        }

        // Apply filter
        float cutoff = _synth.FilterCutoff;
        float resonance = _synth.FilterResonance;

        if (cutoff < 0.99f)
        {
            float freq = 20f * MathF.Pow(1000f, cutoff);
            freq = MathF.Min(freq, _sampleRate * 0.45f);

            float rc = 1f / (2f * MathF.PI * freq);
            float dt = 1f / _sampleRate;
            float a = dt / (rc + dt);

            // Simple one-pole lowpass with resonance feedback
            leftSample += (leftSample - _filterState1) * resonance * 0.5f;
            _filterState1 = _filterState1 + a * (leftSample - _filterState1);
            leftSample = _filterState1;

            rightSample += (rightSample - _filterState2) * resonance * 0.5f;
            _filterState2 = _filterState2 + a * (rightSample - _filterState2);
            rightSample = _filterState2;
        }

        // Apply envelope and velocity
        float envGain = (float)(ampEnv * (Velocity / 127.0));
        leftSample *= envGain;
        rightSample *= envGain;

        return (leftSample, rightSample);
    }
}
