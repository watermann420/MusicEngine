// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Synthesizer component.

using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace MusicEngine.Core.Synthesizers;

/// <summary>
/// Analog synth model types for different vintage character.
/// </summary>
public enum AnalogModel
{
    /// <summary>Moog-style with ladder filter and warm character.</summary>
    Moog,
    /// <summary>Prophet-style with Curtis filter and poly mod.</summary>
    Prophet,
    /// <summary>Juno-style with IR3109 filter and chorus.</summary>
    Juno,
    /// <summary>Oberheim-style with SEM filter and fat sound.</summary>
    Oberheim,
    /// <summary>Generic clean VA with no specific modeling.</summary>
    Clean
}

/// <summary>
/// Filter types available in the analog modeling synth.
/// </summary>
public enum AnalogFilterType
{
    /// <summary>4-pole 24dB/oct ladder lowpass (Moog style).</summary>
    Ladder24,
    /// <summary>2-pole 12dB/oct ladder lowpass.</summary>
    Ladder12,
    /// <summary>State variable lowpass.</summary>
    SVFLowpass,
    /// <summary>State variable highpass.</summary>
    SVFHighpass,
    /// <summary>State variable bandpass.</summary>
    SVFBandpass,
    /// <summary>State variable notch.</summary>
    SVFNotch
}

/// <summary>
/// Internal voice for analog modeling synthesis.
/// </summary>
internal class AnalogVoice
{
    private readonly int _sampleRate;
    private readonly AnalogModelingSynth _synth;
    private readonly Random _random;

    // Oscillator state
    private double _osc1Phase;
    private double _osc2Phase;
    private double _subOscPhase;
    private double _driftPhase;
    private double _driftLfo;

    // Filter state (ladder filter)
    private readonly double[] _filterStages = new double[4];
    private double _filterDelay;

    // Envelope
    private readonly Envelope _ampEnvelope;
    private readonly Envelope _filterEnvelope;

    public int Note { get; private set; }
    public int Velocity { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime TriggerTime { get; private set; }
    public double BaseFrequency { get; private set; }

    public AnalogVoice(int sampleRate, AnalogModelingSynth synth)
    {
        _sampleRate = sampleRate;
        _synth = synth;
        _random = new Random();
        _ampEnvelope = new Envelope(0.01, 0.1, 0.7, 0.3);
        _filterEnvelope = new Envelope(0.01, 0.2, 0.5, 0.3);
    }

    public void Trigger(int note, int velocity)
    {
        Note = note;
        Velocity = velocity;
        IsActive = true;
        TriggerTime = DateTime.Now;
        BaseFrequency = 440.0 * Math.Pow(2.0, (note - 69.0) / 12.0);

        // Reset oscillator phases with slight randomization for analog feel
        _osc1Phase = _random.NextDouble() * 0.1;
        _osc2Phase = _random.NextDouble() * 0.1;
        _subOscPhase = 0;
        _driftPhase = _random.NextDouble() * Math.PI * 2;

        // Copy envelope settings
        _ampEnvelope.Attack = _synth.Attack;
        _ampEnvelope.Decay = _synth.Decay;
        _ampEnvelope.Sustain = _synth.Sustain;
        _ampEnvelope.Release = _synth.Release;
        _ampEnvelope.Trigger(velocity);

        _filterEnvelope.Attack = _synth.FilterAttack;
        _filterEnvelope.Decay = _synth.FilterDecay;
        _filterEnvelope.Sustain = _synth.FilterSustain;
        _filterEnvelope.Release = _synth.FilterRelease;
        _filterEnvelope.Trigger(velocity);
    }

    public void Release()
    {
        _ampEnvelope.Release_Gate();
        _filterEnvelope.Release_Gate();
    }

    public float Process(double deltaTime)
    {
        if (!IsActive) return 0f;

        // Process envelopes
        double ampEnv = _ampEnvelope.Process(deltaTime);
        double filterEnv = _filterEnvelope.Process(deltaTime);

        if (!_ampEnvelope.IsActive)
        {
            IsActive = false;
            return 0f;
        }

        // Calculate drift (slow random pitch variation)
        _driftPhase += deltaTime * 0.3; // Very slow drift
        _driftLfo = Math.Sin(_driftPhase) * _synth.Drift * 0.01;

        // Calculate frequencies with drift
        double freq1 = BaseFrequency * (1.0 + _driftLfo);
        double freq2 = BaseFrequency * _synth.Osc2Ratio * (1.0 + _driftLfo * 0.7);
        freq2 *= Math.Pow(2.0, _synth.Osc2Detune / 1200.0);

        // Generate oscillators
        double osc1 = GenerateOscillator(ref _osc1Phase, freq1, _synth.Osc1Waveform, _synth.Osc1PulseWidth);
        double osc2 = GenerateOscillator(ref _osc2Phase, freq2, _synth.Osc2Waveform, _synth.Osc2PulseWidth);

        // Sub oscillator (one octave down, square)
        double subOsc = 0;
        if (_synth.SubOscLevel > 0)
        {
            double subPhaseInc = freq1 * 0.5 / _sampleRate;
            _subOscPhase += subPhaseInc;
            if (_subOscPhase >= 1.0) _subOscPhase -= 1.0;
            subOsc = _subOscPhase < 0.5 ? 1.0 : -1.0;
        }

        // Mix oscillators
        double mix = osc1 * _synth.Osc1Level + osc2 * _synth.Osc2Level + subOsc * _synth.SubOscLevel;

        // Add noise
        if (_synth.NoiseLevel > 0)
        {
            mix += (_random.NextDouble() * 2.0 - 1.0) * _synth.NoiseLevel;
        }

        // Apply filter with envelope modulation
        double cutoff = _synth.FilterCutoff * Math.Pow(2.0, filterEnv * _synth.FilterEnvAmount * 4.0);
        cutoff = Math.Clamp(cutoff, 0.001, 0.99);

        double filtered = ApplyLadderFilter(mix, cutoff, _synth.FilterResonance, _synth.FilterDrive);

        // Apply amp envelope and velocity
        double velocityGain = Velocity / 127.0;
        float output = (float)(filtered * ampEnv * velocityGain);

        return output;
    }

    private double GenerateOscillator(ref double phase, double frequency, WaveType waveform, float pulseWidth)
    {
        double phaseInc = frequency / _sampleRate;
        phase += phaseInc;
        if (phase >= 1.0) phase -= 1.0;

        double sample = waveform switch
        {
            WaveType.Sine => Math.Sin(phase * 2.0 * Math.PI),
            WaveType.Square => phase < pulseWidth ? 1.0 : -1.0,
            WaveType.Sawtooth => 2.0 * phase - 1.0,
            WaveType.Triangle => phase < 0.5 ? (4.0 * phase - 1.0) : (3.0 - 4.0 * phase),
            WaveType.Noise => _random.NextDouble() * 2.0 - 1.0,
            _ => Math.Sin(phase * 2.0 * Math.PI)
        };

        return sample;
    }

    private double ApplyLadderFilter(double input, double cutoff, double resonance, double drive)
    {
        // Moog-style ladder filter implementation
        // 4-pole lowpass with resonance feedback

        // Apply drive/saturation to input
        if (drive > 0)
        {
            input = Math.Tanh(input * (1.0 + drive * 3.0));
        }

        // Calculate filter coefficient
        double fc = cutoff * _sampleRate * 0.5;
        double g = 1.0 - Math.Exp(-2.0 * Math.PI * fc / _sampleRate);

        // Resonance with compensation
        double k = resonance * 4.0;

        // Feedback with delay compensation
        double feedback = _filterStages[3] * k;

        // Input with resonance feedback
        double u = input - feedback;

        // Nonlinear saturation for analog character
        u = Math.Tanh(u);

        // Four cascaded one-pole lowpass stages
        _filterStages[0] += g * (u - _filterStages[0]);
        _filterStages[1] += g * (_filterStages[0] - _filterStages[1]);
        _filterStages[2] += g * (_filterStages[1] - _filterStages[2]);
        _filterStages[3] += g * (_filterStages[2] - _filterStages[3]);

        return _filterStages[3];
    }
}

/// <summary>
/// Virtual analog synthesizer with Moog/Prophet/Juno style modeling.
/// Features ladder filter, oscillator drift, filter drive/saturation.
/// </summary>
public class AnalogModelingSynth : ISynth
{
    private readonly WaveFormat _waveFormat;
    private readonly List<AnalogVoice> _voices = new();
    private readonly Dictionary<int, AnalogVoice> _noteToVoice = new();
    private readonly object _lock = new();

    /// <summary>Gets or sets the synth name.</summary>
    public string Name { get; set; } = "AnalogModelingSynth";

    /// <summary>Gets the audio format.</summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>Gets or sets the maximum number of voices.</summary>
    public int MaxVoices { get; set; } = 8;

    /// <summary>Gets or sets the master volume (0-1).</summary>
    public float Volume { get; set; } = 0.5f;

    /// <summary>Gets or sets the analog model type.</summary>
    public AnalogModel Model { get; set; } = AnalogModel.Moog;

    // Oscillator 1 parameters
    /// <summary>Oscillator 1 waveform.</summary>
    public WaveType Osc1Waveform { get; set; } = WaveType.Sawtooth;
    /// <summary>Oscillator 1 level (0-1).</summary>
    public float Osc1Level { get; set; } = 1.0f;
    /// <summary>Oscillator 1 pulse width for square wave (0-1).</summary>
    public float Osc1PulseWidth { get; set; } = 0.5f;

    // Oscillator 2 parameters
    /// <summary>Oscillator 2 waveform.</summary>
    public WaveType Osc2Waveform { get; set; } = WaveType.Sawtooth;
    /// <summary>Oscillator 2 level (0-1).</summary>
    public float Osc2Level { get; set; } = 0.5f;
    /// <summary>Oscillator 2 frequency ratio (0.5 = -1 octave, 2.0 = +1 octave).</summary>
    public float Osc2Ratio { get; set; } = 1.0f;
    /// <summary>Oscillator 2 detune in cents (-100 to 100).</summary>
    public float Osc2Detune { get; set; } = 7.0f;
    /// <summary>Oscillator 2 pulse width for square wave (0-1).</summary>
    public float Osc2PulseWidth { get; set; } = 0.5f;

    // Sub oscillator and noise
    /// <summary>Sub oscillator level (0-1).</summary>
    public float SubOscLevel { get; set; } = 0.0f;
    /// <summary>Noise level (0-1).</summary>
    public float NoiseLevel { get; set; } = 0.0f;

    // Filter parameters
    /// <summary>Filter cutoff frequency (0-1).</summary>
    public float FilterCutoff { get; set; } = 0.8f;
    /// <summary>Filter resonance (0-1).</summary>
    public float FilterResonance { get; set; } = 0.2f;
    /// <summary>Filter drive/saturation (0-1).</summary>
    public float FilterDrive { get; set; } = 0.0f;
    /// <summary>Filter type.</summary>
    public AnalogFilterType FilterType { get; set; } = AnalogFilterType.Ladder24;

    // Filter envelope
    /// <summary>Filter envelope amount (-1 to 1).</summary>
    public float FilterEnvAmount { get; set; } = 0.5f;
    /// <summary>Filter envelope attack time.</summary>
    public double FilterAttack { get; set; } = 0.01;
    /// <summary>Filter envelope decay time.</summary>
    public double FilterDecay { get; set; } = 0.2;
    /// <summary>Filter envelope sustain level.</summary>
    public double FilterSustain { get; set; } = 0.5;
    /// <summary>Filter envelope release time.</summary>
    public double FilterRelease { get; set; } = 0.3;

    // Amp envelope
    /// <summary>Amp envelope attack time.</summary>
    public double Attack { get; set; } = 0.01;
    /// <summary>Amp envelope decay time.</summary>
    public double Decay { get; set; } = 0.1;
    /// <summary>Amp envelope sustain level.</summary>
    public double Sustain { get; set; } = 0.7;
    /// <summary>Amp envelope release time.</summary>
    public double Release { get; set; } = 0.3;

    // Analog character
    /// <summary>Oscillator drift amount (0-1) for analog instability.</summary>
    public float Drift { get; set; } = 0.1f;

    /// <summary>
    /// Creates a new AnalogModelingSynth.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz (default: from Settings).</param>
    public AnalogModelingSynth(int? sampleRate = null)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);
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
            case "model": Model = (AnalogModel)(int)value; break;

            case "osc1waveform": Osc1Waveform = (WaveType)(int)value; break;
            case "osc1level": Osc1Level = Math.Clamp(value, 0f, 1f); break;
            case "osc1pulsewidth": Osc1PulseWidth = Math.Clamp(value, 0.01f, 0.99f); break;

            case "osc2waveform": Osc2Waveform = (WaveType)(int)value; break;
            case "osc2level": Osc2Level = Math.Clamp(value, 0f, 1f); break;
            case "osc2ratio": Osc2Ratio = Math.Clamp(value, 0.25f, 4f); break;
            case "osc2detune": Osc2Detune = Math.Clamp(value, -100f, 100f); break;
            case "osc2pulsewidth": Osc2PulseWidth = Math.Clamp(value, 0.01f, 0.99f); break;

            case "subosclevel": SubOscLevel = Math.Clamp(value, 0f, 1f); break;
            case "noiselevel": NoiseLevel = Math.Clamp(value, 0f, 1f); break;

            case "filtercutoff": FilterCutoff = Math.Clamp(value, 0.001f, 0.99f); break;
            case "filterresonance": FilterResonance = Math.Clamp(value, 0f, 1f); break;
            case "filterdrive": FilterDrive = Math.Clamp(value, 0f, 1f); break;
            case "filterenvamount": FilterEnvAmount = Math.Clamp(value, -1f, 1f); break;

            case "filterattack": FilterAttack = value; break;
            case "filterdecay": FilterDecay = value; break;
            case "filtersustain": FilterSustain = value; break;
            case "filterrelease": FilterRelease = value; break;

            case "attack": Attack = value; break;
            case "decay": Decay = value; break;
            case "sustain": Sustain = value; break;
            case "release": Release = value; break;

            case "drift": Drift = Math.Clamp(value, 0f, 1f); break;
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

    private AnalogVoice? GetFreeVoice()
    {
        foreach (var voice in _voices)
        {
            if (!voice.IsActive) return voice;
        }

        if (_voices.Count < MaxVoices)
        {
            var voice = new AnalogVoice(_waveFormat.SampleRate, this);
            _voices.Add(voice);
            return voice;
        }

        // Voice stealing - oldest voice
        AnalogVoice? oldest = null;
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

    /// <summary>Creates a Moog-style bass preset.</summary>
    public static AnalogModelingSynth CreateMoogBass()
    {
        return new AnalogModelingSynth
        {
            Name = "Moog Bass",
            Model = AnalogModel.Moog,
            Osc1Waveform = WaveType.Sawtooth,
            Osc1Level = 1.0f,
            Osc2Waveform = WaveType.Square,
            Osc2Level = 0.5f,
            Osc2Ratio = 0.5f,
            Osc2Detune = 5f,
            SubOscLevel = 0.3f,
            FilterCutoff = 0.3f,
            FilterResonance = 0.4f,
            FilterDrive = 0.3f,
            FilterEnvAmount = 0.6f,
            FilterAttack = 0.001,
            FilterDecay = 0.3,
            FilterSustain = 0.2,
            FilterRelease = 0.2,
            Attack = 0.001,
            Decay = 0.1,
            Sustain = 0.8,
            Release = 0.1,
            Drift = 0.1f
        };
    }

    /// <summary>Creates a Prophet-style pad preset.</summary>
    public static AnalogModelingSynth CreateProphetPad()
    {
        return new AnalogModelingSynth
        {
            Name = "Prophet Pad",
            Model = AnalogModel.Prophet,
            Osc1Waveform = WaveType.Sawtooth,
            Osc1Level = 0.7f,
            Osc2Waveform = WaveType.Sawtooth,
            Osc2Level = 0.7f,
            Osc2Ratio = 1.0f,
            Osc2Detune = 12f,
            FilterCutoff = 0.5f,
            FilterResonance = 0.2f,
            FilterEnvAmount = 0.3f,
            FilterAttack = 0.5,
            FilterDecay = 1.0,
            FilterSustain = 0.6,
            FilterRelease = 1.0,
            Attack = 0.3,
            Decay = 0.5,
            Sustain = 0.8,
            Release = 1.0,
            Drift = 0.15f
        };
    }

    /// <summary>Creates a Juno-style brass preset.</summary>
    public static AnalogModelingSynth CreateJunoBrass()
    {
        return new AnalogModelingSynth
        {
            Name = "Juno Brass",
            Model = AnalogModel.Juno,
            Osc1Waveform = WaveType.Square,
            Osc1Level = 0.8f,
            Osc1PulseWidth = 0.3f,
            Osc2Waveform = WaveType.Sawtooth,
            Osc2Level = 0.6f,
            Osc2Ratio = 1.0f,
            Osc2Detune = 8f,
            FilterCutoff = 0.4f,
            FilterResonance = 0.3f,
            FilterEnvAmount = 0.7f,
            FilterAttack = 0.05,
            FilterDecay = 0.2,
            FilterSustain = 0.5,
            FilterRelease = 0.2,
            Attack = 0.05,
            Decay = 0.1,
            Sustain = 0.9,
            Release = 0.2,
            Drift = 0.08f
        };
    }

    /// <summary>Creates an Oberheim-style lead preset.</summary>
    public static AnalogModelingSynth CreateOberheimLead()
    {
        return new AnalogModelingSynth
        {
            Name = "OB Lead",
            Model = AnalogModel.Oberheim,
            Osc1Waveform = WaveType.Sawtooth,
            Osc1Level = 1.0f,
            Osc2Waveform = WaveType.Square,
            Osc2Level = 0.7f,
            Osc2Ratio = 1.0f,
            Osc2Detune = 10f,
            Osc2PulseWidth = 0.4f,
            FilterCutoff = 0.6f,
            FilterResonance = 0.5f,
            FilterDrive = 0.2f,
            FilterEnvAmount = 0.4f,
            FilterAttack = 0.01,
            FilterDecay = 0.3,
            FilterSustain = 0.4,
            FilterRelease = 0.3,
            Attack = 0.01,
            Decay = 0.1,
            Sustain = 0.8,
            Release = 0.3,
            Drift = 0.12f
        };
    }

    #endregion
}
