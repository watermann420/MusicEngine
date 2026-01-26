//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Wave folding synthesizer with multiple folding algorithms for distortion synthesis.

using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace MusicEngine.Core.Synthesizers;

/// <summary>
/// Oscillator types available for the wave folder input.
/// </summary>
public enum WavefolderOscillatorType
{
    /// <summary>Pure sine wave - excellent for showcasing folding effects.</summary>
    Sine,
    /// <summary>Sawtooth wave - harmonically rich input.</summary>
    Saw,
    /// <summary>Triangle wave - softer than saw, folds nicely.</summary>
    Triangle,
    /// <summary>Square wave - harsh digital character.</summary>
    Square
}

/// <summary>
/// Wave folding algorithm types.
/// </summary>
public enum WavefoldingAlgorithm
{
    /// <summary>Classic sine-based folding - smooth, musical harmonics.</summary>
    Sine,
    /// <summary>Triangle/linear folding - sharper, more aggressive.</summary>
    Triangle,
    /// <summary>Soft clipping with folding - warm distortion character.</summary>
    SoftClip,
    /// <summary>Hard folding - aggressive digital character.</summary>
    Hard,
    /// <summary>Asymmetric folding - adds even harmonics.</summary>
    Asymmetric,
    /// <summary>Multi-stage folding - cascaded folders for complex timbres.</summary>
    MultiStage,
    /// <summary>Serge-style folding - classic analog wavefolder emulation.</summary>
    Serge
}

/// <summary>
/// Wave folding synthesizer that creates complex harmonics by folding waveforms back on themselves.
/// Wave folding is a form of distortion synthesis that creates harmonically rich timbres
/// by reflecting the waveform when it exceeds certain thresholds.
/// </summary>
/// <remarks>
/// Wave folding differs from clipping in that instead of flattening peaks,
/// the waveform is "folded" back, creating additional harmonics and complex timbres.
/// This technique was popularized by the Buchla and Serge modular synthesizers.
/// </remarks>
public class WavefolderSynth : ISynth
{
    private readonly WaveFormat _waveFormat;
    private readonly List<WavefolderVoice> _voices = new();
    private readonly Dictionary<int, WavefolderVoice> _noteToVoice = new();
    private readonly object _lock = new();

    // Oscillator settings
    private WavefolderOscillatorType _oscillatorType = WavefolderOscillatorType.Sine;

    // Wave folding parameters
    private WavefoldingAlgorithm _algorithm = WavefoldingAlgorithm.Sine;
    private float _foldAmount = 0.5f;
    private float _symmetry = 0.5f;
    private float _drive = 0.5f;
    private float _mix = 1.0f;

    /// <summary>
    /// Synth name for identification.
    /// </summary>
    public string Name { get; set; } = "WavefolderSynth";

    /// <summary>
    /// Audio format.
    /// </summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>
    /// Maximum polyphony.
    /// </summary>
    public int MaxVoices { get; set; } = 16;

    /// <summary>
    /// Master volume (0-1).
    /// </summary>
    public float Volume { get; set; } = 0.5f;

    /// <summary>
    /// Input oscillator type.
    /// </summary>
    public WavefolderOscillatorType OscillatorType
    {
        get => _oscillatorType;
        set => _oscillatorType = value;
    }

    /// <summary>
    /// Wave folding algorithm.
    /// </summary>
    public WavefoldingAlgorithm Algorithm
    {
        get => _algorithm;
        set => _algorithm = value;
    }

    /// <summary>
    /// Amount of wave folding (0-1).
    /// Higher values create more folds and harmonics.
    /// </summary>
    public float FoldAmount
    {
        get => _foldAmount;
        set => _foldAmount = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Folding symmetry (0-1).
    /// 0.5 = symmetric, other values create asymmetric folding adding even harmonics.
    /// </summary>
    public float Symmetry
    {
        get => _symmetry;
        set => _symmetry = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Input drive/gain before folding (0-1).
    /// Higher drive pushes the signal harder into the folder.
    /// </summary>
    public float Drive
    {
        get => _drive;
        set => _drive = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Dry/wet mix (0-1).
    /// 0 = dry (unprocessed), 1 = wet (fully folded).
    /// </summary>
    public float Mix
    {
        get => _mix;
        set => _mix = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Detune amount in cents.
    /// </summary>
    public float Detune { get; set; } = 0f;

    /// <summary>
    /// Pulse width for square wave oscillator (0-1, default 0.5).
    /// </summary>
    public float PulseWidth { get; set; } = 0.5f;

    /// <summary>
    /// Number of fold stages for multi-stage algorithm (1-8).
    /// </summary>
    public int FoldStages { get; set; } = 3;

    /// <summary>
    /// Amplitude envelope.
    /// </summary>
    public Envelope AmpEnvelope { get; }

    /// <summary>
    /// Fold amount envelope modulation depth (0-1).
    /// </summary>
    public float FoldEnvelopeDepth { get; set; } = 0f;

    /// <summary>
    /// Creates a new WavefolderSynth with default settings.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz (default: from Settings).</param>
    public WavefolderSynth(int? sampleRate = null)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);

        AmpEnvelope = new Envelope(0.01, 0.1, 0.8, 0.3);
    }

    /// <summary>
    /// Triggers a note.
    /// </summary>
    public void NoteOn(int note, int velocity)
    {
        if (velocity == 0)
        {
            NoteOff(note);
            return;
        }

        lock (_lock)
        {
            if (_noteToVoice.TryGetValue(note, out var existingVoice))
            {
                existingVoice.Trigger(note, velocity);
                return;
            }

            WavefolderVoice? voice = null;

            // Find an inactive voice
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
                voice = new WavefolderVoice(_waveFormat.SampleRate, this);
                _voices.Add(voice);
            }

            // Voice stealing: find oldest voice
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

                int oldNote = voice.Note;
                _noteToVoice.Remove(oldNote);
            }

            if (voice != null)
            {
                voice.Trigger(note, velocity);
                _noteToVoice[note] = voice;
            }
        }
    }

    /// <summary>
    /// Releases a note.
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
    /// Releases all notes.
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
    /// Sets a parameter by name.
    /// </summary>
    public void SetParameter(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "volume":
                Volume = Math.Clamp(value, 0f, 1f);
                break;
            case "foldamount":
            case "fold":
                FoldAmount = value;
                break;
            case "symmetry":
                Symmetry = value;
                break;
            case "drive":
                Drive = value;
                break;
            case "mix":
                Mix = value;
                break;
            case "detune":
                Detune = value;
                break;
            case "pulsewidth":
            case "pw":
                PulseWidth = Math.Clamp(value, 0.01f, 0.99f);
                break;
            case "foldstages":
            case "stages":
                FoldStages = Math.Clamp((int)value, 1, 8);
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
            case "oscillator":
            case "osc":
                OscillatorType = (WavefolderOscillatorType)Math.Clamp((int)value, 0, 3);
                break;
            case "algorithm":
            case "algo":
                Algorithm = (WavefoldingAlgorithm)Math.Clamp((int)value, 0, 6);
                break;
            case "foldenvelopedepth":
            case "foldenvdepth":
                FoldEnvelopeDepth = Math.Clamp(value, 0f, 1f);
                break;
        }
    }

    /// <summary>
    /// Applies wave folding to a sample based on current algorithm.
    /// </summary>
    internal float ApplyWaveFolding(float input, float foldAmount, float symmetry)
    {
        // Apply asymmetry offset
        float asymmetryOffset = (symmetry - 0.5f) * 2f;
        float offsetInput = input + asymmetryOffset * 0.5f;

        // Scale fold amount to useful range (1-16 folds)
        float foldGain = 1f + foldAmount * 15f;
        float driven = offsetInput * foldGain;

        float folded = _algorithm switch
        {
            WavefoldingAlgorithm.Sine => FoldSine(driven),
            WavefoldingAlgorithm.Triangle => FoldTriangle(driven),
            WavefoldingAlgorithm.SoftClip => FoldSoftClip(driven),
            WavefoldingAlgorithm.Hard => FoldHard(driven),
            WavefoldingAlgorithm.Asymmetric => FoldAsymmetric(driven, asymmetryOffset),
            WavefoldingAlgorithm.MultiStage => FoldMultiStage(driven, FoldStages),
            WavefoldingAlgorithm.Serge => FoldSerge(driven),
            _ => FoldSine(driven)
        };

        return folded;
    }

    /// <summary>
    /// Sine-based wave folding - smooth, musical character.
    /// </summary>
    private static float FoldSine(float x)
    {
        // Use sine function for smooth folding
        return MathF.Sin(x * MathF.PI * 0.5f);
    }

    /// <summary>
    /// Triangle/linear wave folding - sharper harmonics.
    /// </summary>
    private static float FoldTriangle(float x)
    {
        // Fold using triangle wave function
        float wrapped = x - MathF.Floor(x * 0.25f + 0.25f) * 4f;
        if (wrapped > 2f) wrapped = 4f - wrapped;
        if (wrapped > 1f) wrapped = 2f - wrapped;
        if (wrapped < -1f) wrapped = -2f - wrapped;
        return Math.Clamp(wrapped, -1f, 1f);
    }

    /// <summary>
    /// Soft clipping with folding - warm character.
    /// </summary>
    private static float FoldSoftClip(float x)
    {
        // Soft clip using tanh, then fold the remainder
        float soft = MathF.Tanh(x);
        float excess = x - soft * 3f;
        if (MathF.Abs(excess) > 0.01f)
        {
            soft += MathF.Sin(excess * MathF.PI) * 0.3f;
        }
        return Math.Clamp(soft, -1f, 1f);
    }

    /// <summary>
    /// Hard folding - aggressive digital character.
    /// </summary>
    private static float FoldHard(float x)
    {
        // Hard fold at +/- 1
        while (x > 1f || x < -1f)
        {
            if (x > 1f) x = 2f - x;
            if (x < -1f) x = -2f - x;
        }
        return x;
    }

    /// <summary>
    /// Asymmetric folding - adds even harmonics.
    /// </summary>
    private static float FoldAsymmetric(float x, float asymmetry)
    {
        // Different fold thresholds for positive and negative
        float upperThreshold = 1f + asymmetry * 0.5f;
        float lowerThreshold = -1f + asymmetry * 0.5f;

        while (x > upperThreshold || x < lowerThreshold)
        {
            if (x > upperThreshold) x = 2f * upperThreshold - x;
            if (x < lowerThreshold) x = 2f * lowerThreshold - x;
        }

        // Normalize output
        return Math.Clamp(x / Math.Max(upperThreshold, -lowerThreshold), -1f, 1f);
    }

    /// <summary>
    /// Multi-stage cascaded folding.
    /// </summary>
    private static float FoldMultiStage(float x, int stages)
    {
        float result = x;
        float stageGain = 1f / stages;

        for (int i = 0; i < stages; i++)
        {
            // Each stage folds with decreasing intensity
            float foldedStage = FoldSine(result * (1f + i * 0.5f));
            result = result * (1f - stageGain) + foldedStage * stageGain;
        }

        return Math.Clamp(result, -1f, 1f);
    }

    /// <summary>
    /// Serge-style wavefolder emulation.
    /// Based on the classic Serge Wave Multiplier circuit behavior.
    /// </summary>
    private static float FoldSerge(float x)
    {
        // Serge-style uses a combination of diode clipping and folding
        // This creates a characteristic "buzzy" tone
        float input = x * 2f;
        float folded = 0f;

        // Emulate the Serge circuit's multiple fold stages
        float stage1 = MathF.Sin(input * MathF.PI * 0.5f);
        float stage2 = MathF.Sin(input * MathF.PI);
        float stage3 = MathF.Sin(input * MathF.PI * 1.5f);

        // Mix the stages with decreasing amounts
        folded = stage1 * 0.6f + stage2 * 0.3f + stage3 * 0.1f;

        // Add some asymmetry characteristic of the original circuit
        if (folded > 0)
        {
            folded = MathF.Pow(folded, 0.9f);
        }
        else
        {
            folded = -MathF.Pow(-folded, 1.1f);
        }

        return Math.Clamp(folded, -1f, 1f);
    }

    /// <summary>
    /// Reads audio samples.
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

        lock (_lock)
        {
            for (int n = 0; n < count; n += channels)
            {
                float sample = 0f;

                foreach (var voice in _voices)
                {
                    if (!voice.IsActive) continue;

                    sample += voice.Process(deltaTime);
                }

                sample *= Volume;

                // Soft clipping for safety
                sample = MathF.Tanh(sample);

                // Output to all channels
                for (int c = 0; c < channels && offset + n + c < buffer.Length; c++)
                {
                    buffer[offset + n + c] = sample;
                }
            }
        }

        return count;
    }

    #region Presets

    /// <summary>
    /// Creates a smooth, musical wavefolder preset.
    /// </summary>
    public static WavefolderSynth CreateSmoothPreset(int? sampleRate = null)
    {
        var synth = new WavefolderSynth(sampleRate)
        {
            Name = "Smooth Folder",
            OscillatorType = WavefolderOscillatorType.Sine,
            Algorithm = WavefoldingAlgorithm.Sine,
            FoldAmount = 0.4f,
            Symmetry = 0.5f,
            Drive = 0.3f,
            Mix = 0.8f
        };
        synth.AmpEnvelope.SetADSR(0.02, 0.2, 0.7, 0.4);
        return synth;
    }

    /// <summary>
    /// Creates an aggressive, buzzy wavefolder preset.
    /// </summary>
    public static WavefolderSynth CreateAggressivePreset(int? sampleRate = null)
    {
        var synth = new WavefolderSynth(sampleRate)
        {
            Name = "Aggressive Folder",
            OscillatorType = WavefolderOscillatorType.Saw,
            Algorithm = WavefoldingAlgorithm.Hard,
            FoldAmount = 0.7f,
            Symmetry = 0.5f,
            Drive = 0.6f,
            Mix = 1.0f
        };
        synth.AmpEnvelope.SetADSR(0.001, 0.15, 0.6, 0.2);
        return synth;
    }

    /// <summary>
    /// Creates a classic Serge-style wavefolder preset.
    /// </summary>
    public static WavefolderSynth CreateSergePreset(int? sampleRate = null)
    {
        var synth = new WavefolderSynth(sampleRate)
        {
            Name = "Serge Style",
            OscillatorType = WavefolderOscillatorType.Triangle,
            Algorithm = WavefoldingAlgorithm.Serge,
            FoldAmount = 0.5f,
            Symmetry = 0.5f,
            Drive = 0.5f,
            Mix = 1.0f
        };
        synth.AmpEnvelope.SetADSR(0.01, 0.3, 0.8, 0.5);
        return synth;
    }

    /// <summary>
    /// Creates a warm, asymmetric wavefolder preset.
    /// </summary>
    public static WavefolderSynth CreateWarmPreset(int? sampleRate = null)
    {
        var synth = new WavefolderSynth(sampleRate)
        {
            Name = "Warm Folder",
            OscillatorType = WavefolderOscillatorType.Sine,
            Algorithm = WavefoldingAlgorithm.SoftClip,
            FoldAmount = 0.35f,
            Symmetry = 0.4f,
            Drive = 0.4f,
            Mix = 0.7f
        };
        synth.AmpEnvelope.SetADSR(0.05, 0.25, 0.75, 0.6);
        return synth;
    }

    /// <summary>
    /// Creates a complex, multi-stage wavefolder preset.
    /// </summary>
    public static WavefolderSynth CreateComplexPreset(int? sampleRate = null)
    {
        var synth = new WavefolderSynth(sampleRate)
        {
            Name = "Complex Folder",
            OscillatorType = WavefolderOscillatorType.Saw,
            Algorithm = WavefoldingAlgorithm.MultiStage,
            FoldAmount = 0.6f,
            Symmetry = 0.5f,
            Drive = 0.5f,
            Mix = 0.9f,
            FoldStages = 4
        };
        synth.AmpEnvelope.SetADSR(0.01, 0.4, 0.7, 0.5);
        return synth;
    }

    #endregion
}

/// <summary>
/// Internal voice for WavefolderSynth.
/// </summary>
internal class WavefolderVoice
{
    private readonly int _sampleRate;
    private readonly WavefolderSynth _synth;
    private readonly Envelope _ampEnv;

    private double _phase;

    public int Note { get; private set; }
    public int Velocity { get; private set; }
    public double Frequency { get; private set; }
    public DateTime TriggerTime { get; private set; }
    public bool IsActive => _ampEnv.IsActive;

    public WavefolderVoice(int sampleRate, WavefolderSynth synth)
    {
        _sampleRate = sampleRate;
        _synth = synth;
        _ampEnv = new Envelope(0.01, 0.1, 0.8, 0.3);
    }

    public void Trigger(int note, int velocity)
    {
        Note = note;
        Velocity = velocity;
        Frequency = 440.0 * Math.Pow(2.0, (note - 69.0) / 12.0);
        TriggerTime = DateTime.Now;

        // Apply detune
        if (_synth.Detune != 0)
        {
            Frequency *= Math.Pow(2.0, _synth.Detune / 1200.0);
        }

        // Copy envelope settings
        _ampEnv.Attack = _synth.AmpEnvelope.Attack;
        _ampEnv.Decay = _synth.AmpEnvelope.Decay;
        _ampEnv.Sustain = _synth.AmpEnvelope.Sustain;
        _ampEnv.Release = _synth.AmpEnvelope.Release;

        _phase = 0;
        _ampEnv.Trigger(velocity);
    }

    public void Release()
    {
        _ampEnv.Release_Gate();
    }

    public float Process(double deltaTime)
    {
        if (!IsActive) return 0f;

        double ampEnv = _ampEnv.Process(deltaTime);
        if (_ampEnv.Stage == EnvelopeStage.Idle) return 0f;

        // Generate oscillator
        double phaseInc = 2.0 * Math.PI * Frequency / _sampleRate;
        _phase += phaseInc;
        if (_phase >= 2.0 * Math.PI)
            _phase -= 2.0 * Math.PI;

        // Generate input waveform
        float input = GenerateOscillator(_phase);

        // Apply drive
        float driveGain = 1f + _synth.Drive * 4f;
        float driven = input * driveGain;

        // Calculate effective fold amount (with envelope modulation)
        float effectiveFold = _synth.FoldAmount;
        if (_synth.FoldEnvelopeDepth > 0)
        {
            effectiveFold = _synth.FoldAmount * (1f - _synth.FoldEnvelopeDepth + _synth.FoldEnvelopeDepth * (float)ampEnv);
        }

        // Apply wave folding
        float folded = _synth.ApplyWaveFolding(driven, effectiveFold, _synth.Symmetry);

        // Mix dry/wet
        float output = input * (1f - _synth.Mix) + folded * _synth.Mix;

        // Apply envelope and velocity
        float velocityGain = Velocity / 127f;
        output *= (float)ampEnv * velocityGain;

        return output;
    }

    private float GenerateOscillator(double phase)
    {
        return _synth.OscillatorType switch
        {
            WavefolderOscillatorType.Sine => MathF.Sin((float)phase),
            WavefolderOscillatorType.Saw => GenerateSaw(phase),
            WavefolderOscillatorType.Triangle => GenerateTriangle(phase),
            WavefolderOscillatorType.Square => GenerateSquare(phase),
            _ => MathF.Sin((float)phase)
        };
    }

    private static float GenerateSaw(double phase)
    {
        // Naive sawtooth: phase goes from 0 to 2pi, output goes from -1 to 1
        return (float)(phase / Math.PI - 1.0);
    }

    private static float GenerateTriangle(double phase)
    {
        // Triangle wave
        double normalized = phase / (2.0 * Math.PI);
        if (normalized < 0.25)
            return (float)(normalized * 4.0);
        else if (normalized < 0.75)
            return (float)(1.0 - (normalized - 0.25) * 4.0);
        else
            return (float)(-1.0 + (normalized - 0.75) * 4.0);
    }

    private float GenerateSquare(double phase)
    {
        // Square wave with adjustable pulse width
        double normalized = phase / (2.0 * Math.PI);
        return normalized < _synth.PulseWidth ? 1f : -1f;
    }
}
