// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Synthesizer component.

using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace MusicEngine.Core.Synthesizers;

/// <summary>
/// Exciter types for modal synthesis.
/// </summary>
public enum ModalExciter
{
    /// <summary>Short impulse (strike).</summary>
    Impulse,
    /// <summary>Noise burst (mallet).</summary>
    NoiseBurst,
    /// <summary>Continuous bowing.</summary>
    Bow,
    /// <summary>Pluck excitation.</summary>
    Pluck,
    /// <summary>Friction/scrape excitation.</summary>
    Friction
}

/// <summary>
/// Material presets affecting modal characteristics.
/// </summary>
public enum ModalMaterial
{
    /// <summary>Steel - bright, long decay.</summary>
    Steel,
    /// <summary>Aluminum - lighter, medium decay.</summary>
    Aluminum,
    /// <summary>Glass - very bright, long decay.</summary>
    Glass,
    /// <summary>Wood - warm, short decay.</summary>
    Wood,
    /// <summary>Brass - warm, medium decay.</summary>
    Brass,
    /// <summary>Bronze - bell-like, long decay.</summary>
    Bronze,
    /// <summary>Ceramic - bright, medium decay.</summary>
    Ceramic,
    /// <summary>Custom - user-defined parameters.</summary>
    Custom
}

/// <summary>
/// Single resonant mode for modal synthesis.
/// </summary>
public class ResonantMode
{
    /// <summary>Frequency ratio relative to fundamental (1.0 = fundamental).</summary>
    public double FrequencyRatio { get; set; } = 1.0;
    /// <summary>Amplitude of this mode (0-1).</summary>
    public double Amplitude { get; set; } = 1.0;
    /// <summary>Decay time in seconds.</summary>
    public double DecayTime { get; set; } = 2.0;
    /// <summary>Phase offset in radians.</summary>
    public double PhaseOffset { get; set; } = 0.0;
    /// <summary>Bandwidth for resonance (Q factor inverse).</summary>
    public double Bandwidth { get; set; } = 0.01;

    public ResonantMode() { }

    public ResonantMode(double freqRatio, double amplitude, double decay)
    {
        FrequencyRatio = freqRatio;
        Amplitude = amplitude;
        DecayTime = decay;
    }
}

/// <summary>
/// Internal modal voice state.
/// </summary>
internal class ModalVoice
{
    private readonly int _sampleRate;
    private readonly ModalSynth _synth;
    private readonly Random _random;

    // Mode state
    private readonly double[] _modePhases;
    private readonly double[] _modeEnvelopes;
    private readonly double[] _modeVelocities; // For bowed excitation

    // Exciter state
    private double _exciterTime;
    private double _exciterEnvelope;
    private double _bowPosition;
    private double _bowVelocity;
    private double _lastBowOutput;

    private const int MaxModes = 32;

    public int Note { get; private set; }
    public int Velocity { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime TriggerTime { get; private set; }
    public double BaseFrequency { get; private set; }

    public ModalVoice(int sampleRate, ModalSynth synth)
    {
        _sampleRate = sampleRate;
        _synth = synth;
        _random = new Random();
        _modePhases = new double[MaxModes];
        _modeEnvelopes = new double[MaxModes];
        _modeVelocities = new double[MaxModes];
    }

    public void Trigger(int note, int velocity)
    {
        Note = note;
        Velocity = velocity;
        IsActive = true;
        TriggerTime = DateTime.Now;
        BaseFrequency = 440.0 * Math.Pow(2.0, (note - 69.0) / 12.0);

        // Reset mode state
        for (int i = 0; i < MaxModes; i++)
        {
            _modePhases[i] = _random.NextDouble() * 0.1; // Slight phase randomization
            _modeEnvelopes[i] = 1.0;
            _modeVelocities[i] = 0;
        }

        _exciterTime = 0;
        _exciterEnvelope = 1.0;
        _bowPosition = 0.2; // Bow position along string
        _bowVelocity = 0.5;
        _lastBowOutput = 0;
    }

    public void Release()
    {
        // For bow exciter, start decay
        if (_synth.Exciter == ModalExciter.Bow)
        {
            _exciterEnvelope = 0; // Stop bowing
        }
        // Other exciters just let modes decay naturally
    }

    public float Process(double deltaTime)
    {
        if (!IsActive) return 0f;

        _exciterTime += deltaTime;

        // Generate excitation
        double excitation = GenerateExcitation(deltaTime);

        // Process all modes
        double output = 0;
        bool anyActive = false;

        var modes = _synth.Modes;
        for (int i = 0; i < Math.Min(modes.Count, MaxModes); i++)
        {
            var mode = modes[i];

            // Apply stiffness (inharmonicity)
            double freqRatio = mode.FrequencyRatio;
            freqRatio *= 1.0 + _synth.Stiffness * (freqRatio - 1.0) * (freqRatio - 1.0) * 0.01;

            double modeFreq = BaseFrequency * freqRatio;

            // Skip if above Nyquist
            if (modeFreq > _sampleRate * 0.45) continue;

            // Update envelope (exponential decay)
            double decayRate = 1.0 / (mode.DecayTime * _synth.DecayScale);
            _modeEnvelopes[i] *= Math.Exp(-deltaTime * decayRate);

            if (_modeEnvelopes[i] < 0.0001) continue;
            anyActive = true;

            // For bow exciter, use stick-slip model
            if (_synth.Exciter == ModalExciter.Bow && _exciterEnvelope > 0)
            {
                // Simplified bowed string model
                double bowForce = excitation * mode.Amplitude;
                double relVel = _bowVelocity - _modeVelocities[i];
                double friction = Math.Tanh(relVel * 5.0) * bowForce;

                _modeVelocities[i] += friction * deltaTime * 1000.0;
                _modeVelocities[i] *= 0.999; // Damping

                output += _modeVelocities[i] * _modeEnvelopes[i];
            }
            else
            {
                // Standard resonator
                double phaseInc = modeFreq / _sampleRate;
                _modePhases[i] += phaseInc;
                if (_modePhases[i] >= 1.0) _modePhases[i] -= 1.0;

                double modeOutput = Math.Sin(_modePhases[i] * 2.0 * Math.PI + mode.PhaseOffset);

                // Apply excitation (only at attack)
                if (_exciterTime < 0.05)
                {
                    modeOutput += excitation * 0.5;
                }

                output += modeOutput * mode.Amplitude * _modeEnvelopes[i];
            }
        }

        if (!anyActive && _exciterTime > 0.1)
        {
            IsActive = false;
            return 0f;
        }

        // Apply velocity
        double velocityGain = Velocity / 127.0;
        output *= velocityGain;

        return (float)output;
    }

    private double GenerateExcitation(double deltaTime)
    {
        switch (_synth.Exciter)
        {
            case ModalExciter.Impulse:
                // Very short impulse
                if (_exciterTime < 0.001)
                    return 1.0 - _exciterTime / 0.001;
                return 0;

            case ModalExciter.NoiseBurst:
                // Noise with exponential decay
                double noiseEnv = Math.Exp(-_exciterTime * _synth.ExciterDecay * 50);
                if (noiseEnv < 0.001) return 0;
                return (_random.NextDouble() * 2.0 - 1.0) * noiseEnv;

            case ModalExciter.Pluck:
                // Pluck: noise burst filtered
                double pluckEnv = Math.Exp(-_exciterTime * _synth.ExciterDecay * 30);
                if (pluckEnv < 0.001) return 0;
                double noise = _random.NextDouble() * 2.0 - 1.0;
                // Simple lowpass
                _lastBowOutput = _lastBowOutput * 0.8 + noise * 0.2;
                return _lastBowOutput * pluckEnv;

            case ModalExciter.Bow:
                // Continuous bowing - return bow force
                return _exciterEnvelope * _synth.ExciterLevel;

            case ModalExciter.Friction:
                // Friction/scrape - modulated noise
                double fricEnv = _exciterEnvelope > 0 ? 1.0 : Math.Exp(-_exciterTime * 5);
                double fricNoise = _random.NextDouble() * 2.0 - 1.0;
                // Add some periodicity
                fricNoise *= 0.5 + 0.5 * Math.Sin(_exciterTime * 100);
                return fricNoise * fricEnv * _synth.ExciterLevel;

            default:
                return 0;
        }
    }
}

/// <summary>
/// Modal synthesis for bells, metals, and resonant objects.
/// Features resonant modes with frequency/amplitude/decay, various exciter types, and material presets.
/// </summary>
public class ModalSynth : ISynth
{
    private readonly WaveFormat _waveFormat;
    private readonly List<ModalVoice> _voices = new();
    private readonly Dictionary<int, ModalVoice> _noteToVoice = new();
    private readonly object _lock = new();

    /// <summary>Gets or sets the synth name.</summary>
    public string Name { get; set; } = "ModalSynth";

    /// <summary>Gets the audio format.</summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>Gets or sets the master volume (0-1).</summary>
    public float Volume { get; set; } = 0.5f;

    /// <summary>Gets or sets the maximum number of voices.</summary>
    public int MaxVoices { get; set; } = 8;

    /// <summary>The list of resonant modes.</summary>
    public List<ResonantMode> Modes { get; } = new();

    /// <summary>Gets or sets the material preset.</summary>
    public ModalMaterial Material { get; set; } = ModalMaterial.Steel;

    /// <summary>Gets or sets the exciter type.</summary>
    public ModalExciter Exciter { get; set; } = ModalExciter.Impulse;

    /// <summary>Gets or sets the exciter level/intensity (0-1).</summary>
    public float ExciterLevel { get; set; } = 1.0f;

    /// <summary>Gets or sets the exciter decay rate.</summary>
    public float ExciterDecay { get; set; } = 1.0f;

    /// <summary>Gets or sets the stiffness (inharmonicity) amount (0-1).</summary>
    public float Stiffness { get; set; } = 0.0f;

    /// <summary>Gets or sets the global decay scale multiplier.</summary>
    public float DecayScale { get; set; } = 1.0f;

    /// <summary>Gets or sets the brightness (high frequency emphasis).</summary>
    public float Brightness { get; set; } = 0.5f;

    /// <summary>
    /// Creates a new ModalSynth.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz (default: from Settings).</param>
    public ModalSynth(int? sampleRate = null)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);

        // Initialize with default steel bell modes
        ApplyMaterial(ModalMaterial.Steel);
    }

    /// <summary>
    /// Applies a material preset.
    /// </summary>
    public void ApplyMaterial(ModalMaterial material)
    {
        Material = material;
        Modes.Clear();

        switch (material)
        {
            case ModalMaterial.Steel:
                // Steel bar/tube modes (nearly harmonic)
                Modes.Add(new ResonantMode(1.0, 1.0, 3.0));
                Modes.Add(new ResonantMode(2.0, 0.6, 2.5));
                Modes.Add(new ResonantMode(3.0, 0.4, 2.0));
                Modes.Add(new ResonantMode(4.0, 0.3, 1.8));
                Modes.Add(new ResonantMode(5.0, 0.2, 1.5));
                Modes.Add(new ResonantMode(6.0, 0.15, 1.3));
                Stiffness = 0.1f;
                Brightness = 0.6f;
                break;

            case ModalMaterial.Aluminum:
                Modes.Add(new ResonantMode(1.0, 1.0, 2.0));
                Modes.Add(new ResonantMode(2.756, 0.5, 1.5));
                Modes.Add(new ResonantMode(5.404, 0.3, 1.2));
                Modes.Add(new ResonantMode(8.933, 0.2, 1.0));
                Stiffness = 0.2f;
                Brightness = 0.7f;
                break;

            case ModalMaterial.Glass:
                // Glass - very inharmonic
                Modes.Add(new ResonantMode(1.0, 1.0, 4.0));
                Modes.Add(new ResonantMode(2.32, 0.7, 3.5));
                Modes.Add(new ResonantMode(3.88, 0.5, 3.0));
                Modes.Add(new ResonantMode(5.59, 0.35, 2.5));
                Modes.Add(new ResonantMode(7.44, 0.25, 2.0));
                Modes.Add(new ResonantMode(9.44, 0.15, 1.5));
                Stiffness = 0.3f;
                Brightness = 0.9f;
                break;

            case ModalMaterial.Wood:
                // Wood - warm, short decay
                Modes.Add(new ResonantMode(1.0, 1.0, 0.5));
                Modes.Add(new ResonantMode(2.0, 0.4, 0.3));
                Modes.Add(new ResonantMode(3.0, 0.2, 0.2));
                Modes.Add(new ResonantMode(4.5, 0.1, 0.15));
                Stiffness = 0.05f;
                Brightness = 0.3f;
                break;

            case ModalMaterial.Brass:
                // Brass - warm, rich harmonics
                Modes.Add(new ResonantMode(1.0, 1.0, 2.5));
                Modes.Add(new ResonantMode(2.0, 0.7, 2.0));
                Modes.Add(new ResonantMode(3.0, 0.5, 1.8));
                Modes.Add(new ResonantMode(4.0, 0.4, 1.5));
                Modes.Add(new ResonantMode(5.0, 0.3, 1.3));
                Modes.Add(new ResonantMode(6.0, 0.2, 1.0));
                Stiffness = 0.08f;
                Brightness = 0.4f;
                break;

            case ModalMaterial.Bronze:
                // Bronze bell - classic bell partials
                Modes.Add(new ResonantMode(0.5, 0.5, 4.0));   // Hum
                Modes.Add(new ResonantMode(1.0, 1.0, 5.0));   // Prime
                Modes.Add(new ResonantMode(1.183, 0.8, 4.0)); // Tierce
                Modes.Add(new ResonantMode(1.506, 0.7, 3.5)); // Quint
                Modes.Add(new ResonantMode(2.0, 0.9, 4.5));   // Nominal
                Modes.Add(new ResonantMode(2.514, 0.5, 3.0));
                Modes.Add(new ResonantMode(2.662, 0.4, 2.8));
                Modes.Add(new ResonantMode(3.011, 0.35, 2.5));
                Modes.Add(new ResonantMode(4.166, 0.25, 2.0));
                Stiffness = 0.15f;
                Brightness = 0.5f;
                break;

            case ModalMaterial.Ceramic:
                Modes.Add(new ResonantMode(1.0, 1.0, 1.5));
                Modes.Add(new ResonantMode(2.2, 0.6, 1.2));
                Modes.Add(new ResonantMode(3.6, 0.4, 1.0));
                Modes.Add(new ResonantMode(5.2, 0.25, 0.8));
                Stiffness = 0.25f;
                Brightness = 0.75f;
                break;

            case ModalMaterial.Custom:
                // Keep existing modes
                if (Modes.Count == 0)
                {
                    Modes.Add(new ResonantMode(1.0, 1.0, 2.0));
                }
                break;
        }

        // Apply brightness to mode amplitudes
        for (int i = 0; i < Modes.Count; i++)
        {
            double freqFactor = Modes[i].FrequencyRatio;
            double brightFactor = Math.Pow(freqFactor, (Brightness - 0.5) * 2);
            Modes[i].Amplitude *= Math.Clamp(brightFactor, 0.1, 2.0);
        }
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

            var voice = GetFreeVoice();
            if (voice == null) return;

            voice.Trigger(note, velocity);
            _noteToVoice[note] = voice;
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
            case "volume": Volume = Math.Clamp(value, 0f, 1f); break;
            case "material": ApplyMaterial((ModalMaterial)(int)value); break;
            case "exciter": Exciter = (ModalExciter)(int)value; break;
            case "exciterlevel": ExciterLevel = Math.Clamp(value, 0f, 1f); break;
            case "exciterdecay": ExciterDecay = Math.Clamp(value, 0.1f, 10f); break;
            case "stiffness": Stiffness = Math.Clamp(value, 0f, 1f); break;
            case "decayscale": DecayScale = Math.Clamp(value, 0.1f, 10f); break;
            case "brightness": Brightness = Math.Clamp(value, 0f, 1f); break;
        }
    }

    /// <summary>
    /// Reads audio samples.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        Array.Clear(buffer, offset, count);

        int channels = _waveFormat.Channels;
        double deltaTime = 1.0 / _waveFormat.SampleRate;

        lock (_lock)
        {
            for (int n = 0; n < count; n += channels)
            {
                float sample = 0f;

                foreach (var voice in _voices)
                {
                    if (voice.IsActive)
                    {
                        sample += voice.Process(deltaTime);
                    }
                }

                // Apply volume and soft clipping
                sample *= Volume;
                sample = MathF.Tanh(sample);

                // Output to all channels
                for (int c = 0; c < channels; c++)
                {
                    if (offset + n + c < buffer.Length)
                    {
                        buffer[offset + n + c] = sample;
                    }
                }
            }
        }

        return count;
    }

    private ModalVoice? GetFreeVoice()
    {
        foreach (var voice in _voices)
        {
            if (!voice.IsActive) return voice;
        }

        if (_voices.Count < MaxVoices)
        {
            var voice = new ModalVoice(_waveFormat.SampleRate, this);
            _voices.Add(voice);
            return voice;
        }

        // Voice stealing
        ModalVoice? oldest = null;
        DateTime oldestTime = DateTime.MaxValue;
        foreach (var voice in _voices)
        {
            if (voice.TriggerTime < oldestTime)
            {
                oldestTime = voice.TriggerTime;
                oldest = voice;
            }
        }

        if (oldest != null)
        {
            _noteToVoice.Remove(oldest.Note);
        }

        return oldest;
    }

    #region Presets

    /// <summary>Creates a church bell preset.</summary>
    public static ModalSynth CreateChurchBell()
    {
        var synth = new ModalSynth { Name = "Church Bell" };
        synth.ApplyMaterial(ModalMaterial.Bronze);
        synth.Exciter = ModalExciter.Impulse;
        synth.DecayScale = 1.5f;
        synth.Stiffness = 0.1f;
        return synth;
    }

    /// <summary>Creates a tubular bell preset.</summary>
    public static ModalSynth CreateTubularBell()
    {
        var synth = new ModalSynth { Name = "Tubular Bell" };
        synth.ApplyMaterial(ModalMaterial.Steel);
        synth.Exciter = ModalExciter.NoiseBurst;
        synth.ExciterDecay = 2.0f;
        synth.DecayScale = 1.2f;
        return synth;
    }

    /// <summary>Creates a vibraphone preset.</summary>
    public static ModalSynth CreateVibraphone()
    {
        var synth = new ModalSynth { Name = "Vibraphone" };
        synth.ApplyMaterial(ModalMaterial.Aluminum);
        synth.Exciter = ModalExciter.NoiseBurst;
        synth.ExciterDecay = 1.5f;
        synth.Brightness = 0.6f;
        return synth;
    }

    /// <summary>Creates a marimba preset.</summary>
    public static ModalSynth CreateMarimba()
    {
        var synth = new ModalSynth { Name = "Marimba" };
        synth.ApplyMaterial(ModalMaterial.Wood);
        synth.Exciter = ModalExciter.NoiseBurst;
        synth.ExciterDecay = 0.5f;
        synth.Brightness = 0.35f;
        return synth;
    }

    /// <summary>Creates a bowed glass preset.</summary>
    public static ModalSynth CreateBowedGlass()
    {
        var synth = new ModalSynth { Name = "Bowed Glass" };
        synth.ApplyMaterial(ModalMaterial.Glass);
        synth.Exciter = ModalExciter.Bow;
        synth.ExciterLevel = 0.3f;
        synth.DecayScale = 2.0f;
        return synth;
    }

    /// <summary>Creates a glockenspiel preset.</summary>
    public static ModalSynth CreateGlockenspiel()
    {
        var synth = new ModalSynth { Name = "Glockenspiel" };
        synth.ApplyMaterial(ModalMaterial.Steel);
        synth.Exciter = ModalExciter.Impulse;
        synth.Brightness = 0.9f;
        synth.DecayScale = 0.8f;
        return synth;
    }

    #endregion
}
