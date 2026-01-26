//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Classic subtractive synthesizer with multiple oscillators, multi-mode filter,
// dual ADSR envelopes, and dual LFOs with flexible routing.

using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace MusicEngine.Core.Synthesizers;

/// <summary>
/// Filter mode for the subtractive synth.
/// </summary>
public enum SubtractiveFilterMode
{
    /// <summary>Low-pass filter - attenuates frequencies above cutoff.</summary>
    LowPass,
    /// <summary>High-pass filter - attenuates frequencies below cutoff.</summary>
    HighPass,
    /// <summary>Band-pass filter - passes frequencies around cutoff.</summary>
    BandPass,
    /// <summary>Notch filter - attenuates frequencies around cutoff.</summary>
    Notch
}

/// <summary>
/// LFO waveform shapes.
/// </summary>
public enum LfoWaveform
{
    /// <summary>Sine wave - smooth modulation.</summary>
    Sine,
    /// <summary>Triangle wave - linear ramps up and down.</summary>
    Triangle,
    /// <summary>Sawtooth wave - rising ramp.</summary>
    Sawtooth,
    /// <summary>Square wave - instant transitions.</summary>
    Square,
    /// <summary>Sample and hold - stepped random values.</summary>
    SampleAndHold
}

/// <summary>
/// LFO modulation destination.
/// </summary>
public enum LfoDestination
{
    /// <summary>No modulation.</summary>
    None,
    /// <summary>Modulate oscillator pitch.</summary>
    Pitch,
    /// <summary>Modulate filter cutoff frequency.</summary>
    FilterCutoff,
    /// <summary>Modulate amplitude/volume.</summary>
    Amplitude,
    /// <summary>Modulate pulse width (for square waves).</summary>
    PulseWidth,
    /// <summary>Modulate stereo pan position.</summary>
    Pan
}

/// <summary>
/// Internal LFO implementation for modulation.
/// </summary>
internal class SubtractiveLfo
{
    private double _phase;
    private double _sampleAndHoldValue;
    private double _lastSampleAndHoldPhase;
    private readonly Random _random;

    public LfoWaveform Waveform { get; set; } = LfoWaveform.Sine;
    public LfoDestination Destination { get; set; } = LfoDestination.None;
    public float Rate { get; set; } = 1f; // Hz
    public float Depth { get; set; } = 0.5f; // 0-1
    public bool Sync { get; set; } = false; // Retrigger on note

    public SubtractiveLfo()
    {
        _random = new Random();
        _sampleAndHoldValue = _random.NextDouble() * 2.0 - 1.0;
    }

    public void Reset()
    {
        _phase = 0;
        _lastSampleAndHoldPhase = 0;
        _sampleAndHoldValue = _random.NextDouble() * 2.0 - 1.0;
    }

    public double Process(double deltaTime)
    {
        _phase += Rate * deltaTime;
        if (_phase >= 1.0)
        {
            _phase -= 1.0;
        }

        double value = Waveform switch
        {
            LfoWaveform.Sine => Math.Sin(_phase * 2.0 * Math.PI),
            LfoWaveform.Triangle => _phase < 0.5 ? (4.0 * _phase - 1.0) : (3.0 - 4.0 * _phase),
            LfoWaveform.Sawtooth => 2.0 * _phase - 1.0,
            LfoWaveform.Square => _phase < 0.5 ? 1.0 : -1.0,
            LfoWaveform.SampleAndHold => GetSampleAndHoldValue(),
            _ => 0
        };

        return value * Depth;
    }

    private double GetSampleAndHoldValue()
    {
        // Update value once per cycle
        if (_phase < _lastSampleAndHoldPhase)
        {
            _sampleAndHoldValue = _random.NextDouble() * 2.0 - 1.0;
        }
        _lastSampleAndHoldPhase = _phase;
        return _sampleAndHoldValue;
    }
}

/// <summary>
/// Internal voice for the subtractive synthesizer.
/// </summary>
internal class SubtractiveVoice
{
    private readonly int _sampleRate;
    private readonly SubtractiveSynth _synth;
    private readonly Random _random;

    // Oscillator state
    private double _osc1Phase;
    private double _osc2Phase;
    private double _osc3Phase;
    private double _masterOscPhase; // For sync

    // Filter state (state variable filter)
    private double _filterLp;
    private double _filterBp;
    private double _filterHp;

    // Envelopes
    private readonly Envelope _ampEnvelope;
    private readonly Envelope _filterEnvelope;

    // LFOs
    private readonly SubtractiveLfo _lfo1;
    private readonly SubtractiveLfo _lfo2;

    public int Note { get; private set; }
    public int Velocity { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime TriggerTime { get; private set; }
    public double BaseFrequency { get; private set; }

    public SubtractiveVoice(int sampleRate, SubtractiveSynth synth)
    {
        _sampleRate = sampleRate;
        _synth = synth;
        _random = new Random();
        _ampEnvelope = new Envelope(0.01, 0.1, 0.7, 0.3);
        _filterEnvelope = new Envelope(0.01, 0.2, 0.5, 0.3);
        _lfo1 = new SubtractiveLfo();
        _lfo2 = new SubtractiveLfo();
    }

    public void Trigger(int note, int velocity)
    {
        Note = note;
        Velocity = velocity;
        IsActive = true;
        TriggerTime = DateTime.Now;
        BaseFrequency = 440.0 * Math.Pow(2.0, (note - 69.0) / 12.0);

        // Reset phases with slight randomization
        _osc1Phase = _random.NextDouble() * 0.01;
        _osc2Phase = _random.NextDouble() * 0.01;
        _osc3Phase = _random.NextDouble() * 0.01;
        _masterOscPhase = 0;

        // Reset filter state
        _filterLp = 0;
        _filterBp = 0;
        _filterHp = 0;

        // Copy envelope settings from synth
        _ampEnvelope.Attack = _synth.AmpAttack;
        _ampEnvelope.Decay = _synth.AmpDecay;
        _ampEnvelope.Sustain = _synth.AmpSustain;
        _ampEnvelope.Release = _synth.AmpRelease;
        _ampEnvelope.Trigger(velocity);

        _filterEnvelope.Attack = _synth.FilterAttack;
        _filterEnvelope.Decay = _synth.FilterDecay;
        _filterEnvelope.Sustain = _synth.FilterSustain;
        _filterEnvelope.Release = _synth.FilterRelease;
        _filterEnvelope.Trigger(velocity);

        // Copy LFO settings
        _lfo1.Waveform = _synth.Lfo1Waveform;
        _lfo1.Destination = _synth.Lfo1Destination;
        _lfo1.Rate = _synth.Lfo1Rate;
        _lfo1.Depth = _synth.Lfo1Depth;
        _lfo1.Sync = _synth.Lfo1Sync;

        _lfo2.Waveform = _synth.Lfo2Waveform;
        _lfo2.Destination = _synth.Lfo2Destination;
        _lfo2.Rate = _synth.Lfo2Rate;
        _lfo2.Depth = _synth.Lfo2Depth;
        _lfo2.Sync = _synth.Lfo2Sync;

        // Reset LFOs if sync is enabled
        if (_lfo1.Sync) _lfo1.Reset();
        if (_lfo2.Sync) _lfo2.Reset();
    }

    public void Release()
    {
        _ampEnvelope.Release_Gate();
        _filterEnvelope.Release_Gate();
    }

    public (float left, float right) Process(double deltaTime)
    {
        if (!IsActive) return (0f, 0f);

        // Process envelopes
        double ampEnv = _ampEnvelope.Process(deltaTime);
        double filterEnv = _filterEnvelope.Process(deltaTime);

        if (!_ampEnvelope.IsActive)
        {
            IsActive = false;
            return (0f, 0f);
        }

        // Process LFOs
        double lfo1Value = _lfo1.Process(deltaTime);
        double lfo2Value = _lfo2.Process(deltaTime);

        // Calculate LFO modulation amounts
        double pitchMod = 0;
        double filterMod = 0;
        double ampMod = 0;
        double pwMod = 0;
        double panMod = 0;

        ApplyLfoModulation(_lfo1.Destination, lfo1Value, ref pitchMod, ref filterMod, ref ampMod, ref pwMod, ref panMod);
        ApplyLfoModulation(_lfo2.Destination, lfo2Value, ref pitchMod, ref filterMod, ref ampMod, ref pwMod, ref panMod);

        // Calculate frequencies with pitch modulation (in semitones)
        double pitchMultiplier = Math.Pow(2.0, pitchMod * 2.0 / 12.0); // Max 2 semitones

        double freq1 = BaseFrequency * pitchMultiplier * Math.Pow(2.0, _synth.Osc1Octave) *
                       Math.Pow(2.0, _synth.Osc1Detune / 1200.0);
        double freq2 = BaseFrequency * pitchMultiplier * Math.Pow(2.0, _synth.Osc2Octave) *
                       Math.Pow(2.0, _synth.Osc2Detune / 1200.0);
        double freq3 = BaseFrequency * pitchMultiplier * Math.Pow(2.0, _synth.Osc3Octave) *
                       Math.Pow(2.0, _synth.Osc3Detune / 1200.0);

        // Generate oscillators
        double pulseWidth1 = Math.Clamp(_synth.Osc1PulseWidth + pwMod * 0.3, 0.05, 0.95);
        double pulseWidth2 = Math.Clamp(_synth.Osc2PulseWidth + pwMod * 0.3, 0.05, 0.95);
        double pulseWidth3 = Math.Clamp(_synth.Osc3PulseWidth + pwMod * 0.3, 0.05, 0.95);

        // Process master oscillator for sync
        double masterPhaseInc = freq1 / _sampleRate;
        _masterOscPhase += masterPhaseInc;
        bool syncReset = false;
        if (_masterOscPhase >= 1.0)
        {
            _masterOscPhase -= 1.0;
            syncReset = true;
        }

        // Generate oscillator outputs
        double osc1 = GenerateOscillator(ref _osc1Phase, freq1, _synth.Osc1Waveform, pulseWidth1, false);
        double osc2 = GenerateOscillator(ref _osc2Phase, freq2, _synth.Osc2Waveform, pulseWidth2,
            _synth.Osc2Sync && syncReset);
        double osc3 = GenerateOscillator(ref _osc3Phase, freq3, _synth.Osc3Waveform, pulseWidth3,
            _synth.Osc3Sync && syncReset);

        // Mix oscillators
        double mix = osc1 * _synth.Osc1Level +
                     osc2 * _synth.Osc2Level +
                     osc3 * _synth.Osc3Level;

        // Add noise if enabled
        if (_synth.NoiseLevel > 0)
        {
            mix += (_random.NextDouble() * 2.0 - 1.0) * _synth.NoiseLevel;
        }

        // Apply filter with envelope and LFO modulation
        double cutoff = _synth.FilterCutoff;
        cutoff *= Math.Pow(2.0, filterEnv * _synth.FilterEnvAmount * 4.0); // Envelope mod
        cutoff *= Math.Pow(2.0, filterMod * 2.0); // LFO mod
        cutoff = Math.Clamp(cutoff, 0.001, 0.99);

        double filtered = ApplyFilter(mix, cutoff, _synth.FilterResonance);

        // Apply amp envelope and LFO amplitude modulation
        double velocityGain = Velocity / 127.0;
        double ampGain = ampEnv * (1.0 + ampMod * 0.5);
        float monoOutput = (float)(filtered * ampGain * velocityGain);

        // Apply panning with LFO modulation
        float basePan = _synth.Pan;
        float pan = (float)Math.Clamp(basePan + panMod, -1.0, 1.0);

        // Calculate stereo from pan position using equal power law
        float leftGain = MathF.Cos((pan + 1f) * MathF.PI / 4f);
        float rightGain = MathF.Sin((pan + 1f) * MathF.PI / 4f);

        return (monoOutput * leftGain, monoOutput * rightGain);
    }

    private void ApplyLfoModulation(LfoDestination destination, double value,
        ref double pitchMod, ref double filterMod, ref double ampMod, ref double pwMod, ref double panMod)
    {
        switch (destination)
        {
            case LfoDestination.Pitch:
                pitchMod += value;
                break;
            case LfoDestination.FilterCutoff:
                filterMod += value;
                break;
            case LfoDestination.Amplitude:
                ampMod += value;
                break;
            case LfoDestination.PulseWidth:
                pwMod += value;
                break;
            case LfoDestination.Pan:
                panMod += value;
                break;
        }
    }

    private double GenerateOscillator(ref double phase, double frequency, WaveType waveform, double pulseWidth,
        bool syncReset)
    {
        if (syncReset)
        {
            phase = 0;
        }

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

    private double ApplyFilter(double input, double cutoff, double resonance)
    {
        // State variable filter implementation
        // Supports LP, HP, BP, and Notch modes
        double fc = cutoff * _sampleRate * 0.5;
        double f = 2.0 * Math.Sin(Math.PI * fc / _sampleRate);
        f = Math.Clamp(f, 0.0, 1.0);

        double q = 1.0 - resonance * 0.99;
        q = Math.Max(q, 0.01);

        // SVF equations
        _filterLp += f * _filterBp;
        _filterHp = input - _filterLp - q * _filterBp;
        _filterBp += f * _filterHp;

        // Apply soft saturation to prevent runaway resonance
        _filterBp = Math.Tanh(_filterBp);

        // Select output based on filter mode
        return _synth.FilterMode switch
        {
            SubtractiveFilterMode.LowPass => _filterLp,
            SubtractiveFilterMode.HighPass => _filterHp,
            SubtractiveFilterMode.BandPass => _filterBp,
            SubtractiveFilterMode.Notch => _filterLp + _filterHp,
            _ => _filterLp
        };
    }
}

/// <summary>
/// Classic subtractive synthesizer with multiple oscillators, multi-mode filter,
/// dual ADSR envelopes (amp and filter), and dual LFOs with flexible routing.
/// </summary>
/// <remarks>
/// Architecture:
/// - 3 oscillators with independent waveform, octave, detune, pulse width, and level
/// - Oscillator 2 and 3 can sync to oscillator 1
/// - Multi-mode state variable filter (LP, HP, BP, Notch) with resonance
/// - Dedicated ADSR envelope for amplitude
/// - Dedicated ADSR envelope for filter with adjustable amount
/// - 2 LFOs with multiple waveforms and destinations
/// - White noise generator
/// - Polyphonic with voice stealing
/// </remarks>
public class SubtractiveSynth : ISynth
{
    private readonly WaveFormat _waveFormat;
    private readonly List<SubtractiveVoice> _voices = new();
    private readonly Dictionary<int, SubtractiveVoice> _noteToVoice = new();
    private readonly object _lock = new();

    /// <summary>Gets or sets the synth name.</summary>
    public string Name { get; set; } = "SubtractiveSynth";

    /// <summary>Gets the audio format.</summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>Gets or sets the maximum number of voices.</summary>
    public int MaxVoices { get; set; } = 8;

    /// <summary>Gets or sets the master volume (0-1).</summary>
    public float Volume { get; set; } = 0.5f;

    /// <summary>Gets or sets the pan position (-1 to 1).</summary>
    public float Pan { get; set; } = 0f;

    // Oscillator 1 parameters
    /// <summary>Oscillator 1 waveform.</summary>
    public WaveType Osc1Waveform { get; set; } = WaveType.Sawtooth;

    /// <summary>Oscillator 1 level (0-1).</summary>
    public float Osc1Level { get; set; } = 1.0f;

    /// <summary>Oscillator 1 octave offset (-2 to +2).</summary>
    public int Osc1Octave { get; set; } = 0;

    /// <summary>Oscillator 1 detune in cents (-100 to +100).</summary>
    public float Osc1Detune { get; set; } = 0f;

    /// <summary>Oscillator 1 pulse width for square wave (0.05-0.95).</summary>
    public float Osc1PulseWidth { get; set; } = 0.5f;

    // Oscillator 2 parameters
    /// <summary>Oscillator 2 waveform.</summary>
    public WaveType Osc2Waveform { get; set; } = WaveType.Square;

    /// <summary>Oscillator 2 level (0-1).</summary>
    public float Osc2Level { get; set; } = 0.5f;

    /// <summary>Oscillator 2 octave offset (-2 to +2).</summary>
    public int Osc2Octave { get; set; } = 0;

    /// <summary>Oscillator 2 detune in cents (-100 to +100).</summary>
    public float Osc2Detune { get; set; } = 7f;

    /// <summary>Oscillator 2 pulse width for square wave (0.05-0.95).</summary>
    public float Osc2PulseWidth { get; set; } = 0.5f;

    /// <summary>Oscillator 2 sync to oscillator 1.</summary>
    public bool Osc2Sync { get; set; } = false;

    // Oscillator 3 parameters
    /// <summary>Oscillator 3 waveform.</summary>
    public WaveType Osc3Waveform { get; set; } = WaveType.Sine;

    /// <summary>Oscillator 3 level (0-1).</summary>
    public float Osc3Level { get; set; } = 0f;

    /// <summary>Oscillator 3 octave offset (-2 to +2).</summary>
    public int Osc3Octave { get; set; } = -1;

    /// <summary>Oscillator 3 detune in cents (-100 to +100).</summary>
    public float Osc3Detune { get; set; } = 0f;

    /// <summary>Oscillator 3 pulse width for square wave (0.05-0.95).</summary>
    public float Osc3PulseWidth { get; set; } = 0.5f;

    /// <summary>Oscillator 3 sync to oscillator 1.</summary>
    public bool Osc3Sync { get; set; } = false;

    /// <summary>Noise level (0-1).</summary>
    public float NoiseLevel { get; set; } = 0f;

    // Filter parameters
    /// <summary>Filter mode (LP, HP, BP, Notch).</summary>
    public SubtractiveFilterMode FilterMode { get; set; } = SubtractiveFilterMode.LowPass;

    /// <summary>Filter cutoff frequency (0-1).</summary>
    public float FilterCutoff { get; set; } = 0.8f;

    /// <summary>Filter resonance (0-1).</summary>
    public float FilterResonance { get; set; } = 0.2f;

    /// <summary>Filter envelope amount (-1 to 1).</summary>
    public float FilterEnvAmount { get; set; } = 0.5f;

    // Amp envelope (ADSR)
    /// <summary>Amp envelope attack time in seconds.</summary>
    public double AmpAttack { get; set; } = 0.01;

    /// <summary>Amp envelope decay time in seconds.</summary>
    public double AmpDecay { get; set; } = 0.1;

    /// <summary>Amp envelope sustain level (0-1).</summary>
    public double AmpSustain { get; set; } = 0.7;

    /// <summary>Amp envelope release time in seconds.</summary>
    public double AmpRelease { get; set; } = 0.3;

    // Filter envelope (ADSR)
    /// <summary>Filter envelope attack time in seconds.</summary>
    public double FilterAttack { get; set; } = 0.01;

    /// <summary>Filter envelope decay time in seconds.</summary>
    public double FilterDecay { get; set; } = 0.2;

    /// <summary>Filter envelope sustain level (0-1).</summary>
    public double FilterSustain { get; set; } = 0.5;

    /// <summary>Filter envelope release time in seconds.</summary>
    public double FilterRelease { get; set; } = 0.3;

    // LFO 1 parameters
    /// <summary>LFO 1 waveform.</summary>
    public LfoWaveform Lfo1Waveform { get; set; } = LfoWaveform.Sine;

    /// <summary>LFO 1 destination.</summary>
    public LfoDestination Lfo1Destination { get; set; } = LfoDestination.None;

    /// <summary>LFO 1 rate in Hz.</summary>
    public float Lfo1Rate { get; set; } = 1f;

    /// <summary>LFO 1 depth (0-1).</summary>
    public float Lfo1Depth { get; set; } = 0.5f;

    /// <summary>LFO 1 sync to note trigger.</summary>
    public bool Lfo1Sync { get; set; } = false;

    // LFO 2 parameters
    /// <summary>LFO 2 waveform.</summary>
    public LfoWaveform Lfo2Waveform { get; set; } = LfoWaveform.Triangle;

    /// <summary>LFO 2 destination.</summary>
    public LfoDestination Lfo2Destination { get; set; } = LfoDestination.None;

    /// <summary>LFO 2 rate in Hz.</summary>
    public float Lfo2Rate { get; set; } = 0.5f;

    /// <summary>LFO 2 depth (0-1).</summary>
    public float Lfo2Depth { get; set; } = 0.5f;

    /// <summary>LFO 2 sync to note trigger.</summary>
    public bool Lfo2Sync { get; set; } = false;

    /// <summary>
    /// Creates a new SubtractiveSynth.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz (default: from Settings).</param>
    public SubtractiveSynth(int? sampleRate = null)
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
            case "volume":
                Volume = Math.Clamp(value, 0f, 1f);
                break;
            case "pan":
                Pan = Math.Clamp(value, -1f, 1f);
                break;

            // Oscillator 1
            case "osc1waveform":
                Osc1Waveform = (WaveType)(int)value;
                break;
            case "osc1level":
                Osc1Level = Math.Clamp(value, 0f, 1f);
                break;
            case "osc1octave":
                Osc1Octave = Math.Clamp((int)value, -2, 2);
                break;
            case "osc1detune":
                Osc1Detune = Math.Clamp(value, -100f, 100f);
                break;
            case "osc1pulsewidth":
                Osc1PulseWidth = Math.Clamp(value, 0.05f, 0.95f);
                break;

            // Oscillator 2
            case "osc2waveform":
                Osc2Waveform = (WaveType)(int)value;
                break;
            case "osc2level":
                Osc2Level = Math.Clamp(value, 0f, 1f);
                break;
            case "osc2octave":
                Osc2Octave = Math.Clamp((int)value, -2, 2);
                break;
            case "osc2detune":
                Osc2Detune = Math.Clamp(value, -100f, 100f);
                break;
            case "osc2pulsewidth":
                Osc2PulseWidth = Math.Clamp(value, 0.05f, 0.95f);
                break;
            case "osc2sync":
                Osc2Sync = value > 0.5f;
                break;

            // Oscillator 3
            case "osc3waveform":
                Osc3Waveform = (WaveType)(int)value;
                break;
            case "osc3level":
                Osc3Level = Math.Clamp(value, 0f, 1f);
                break;
            case "osc3octave":
                Osc3Octave = Math.Clamp((int)value, -2, 2);
                break;
            case "osc3detune":
                Osc3Detune = Math.Clamp(value, -100f, 100f);
                break;
            case "osc3pulsewidth":
                Osc3PulseWidth = Math.Clamp(value, 0.05f, 0.95f);
                break;
            case "osc3sync":
                Osc3Sync = value > 0.5f;
                break;

            case "noiselevel":
                NoiseLevel = Math.Clamp(value, 0f, 1f);
                break;

            // Filter
            case "filtermode":
                FilterMode = (SubtractiveFilterMode)(int)value;
                break;
            case "filtercutoff":
                FilterCutoff = Math.Clamp(value, 0.001f, 0.99f);
                break;
            case "filterresonance":
                FilterResonance = Math.Clamp(value, 0f, 1f);
                break;
            case "filterenvamount":
                FilterEnvAmount = Math.Clamp(value, -1f, 1f);
                break;

            // Amp envelope
            case "ampattack":
            case "attack":
                AmpAttack = Math.Max(0.001, value);
                break;
            case "ampdecay":
            case "decay":
                AmpDecay = Math.Max(0.001, value);
                break;
            case "ampsustain":
            case "sustain":
                AmpSustain = Math.Clamp(value, 0, 1);
                break;
            case "amprelease":
            case "release":
                AmpRelease = Math.Max(0.001, value);
                break;

            // Filter envelope
            case "filterattack":
                FilterAttack = Math.Max(0.001, value);
                break;
            case "filterdecay":
                FilterDecay = Math.Max(0.001, value);
                break;
            case "filtersustain":
                FilterSustain = Math.Clamp(value, 0, 1);
                break;
            case "filterrelease":
                FilterRelease = Math.Max(0.001, value);
                break;

            // LFO 1
            case "lfo1waveform":
                Lfo1Waveform = (LfoWaveform)(int)value;
                break;
            case "lfo1destination":
                Lfo1Destination = (LfoDestination)(int)value;
                break;
            case "lfo1rate":
                Lfo1Rate = Math.Clamp(value, 0.01f, 50f);
                break;
            case "lfo1depth":
                Lfo1Depth = Math.Clamp(value, 0f, 1f);
                break;
            case "lfo1sync":
                Lfo1Sync = value > 0.5f;
                break;

            // LFO 2
            case "lfo2waveform":
                Lfo2Waveform = (LfoWaveform)(int)value;
                break;
            case "lfo2destination":
                Lfo2Destination = (LfoDestination)(int)value;
                break;
            case "lfo2rate":
                Lfo2Rate = Math.Clamp(value, 0.01f, 50f);
                break;
            case "lfo2depth":
                Lfo2Depth = Math.Clamp(value, 0f, 1f);
                break;
            case "lfo2sync":
                Lfo2Sync = value > 0.5f;
                break;
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
                float leftSample = 0f;
                float rightSample = 0f;

                foreach (var voice in _voices)
                {
                    if (voice.IsActive)
                    {
                        var (left, right) = voice.Process(deltaTime);
                        leftSample += left;
                        rightSample += right;
                    }
                }

                // Apply volume and soft clipping
                leftSample *= Volume;
                rightSample *= Volume;
                leftSample = MathF.Tanh(leftSample);
                rightSample = MathF.Tanh(rightSample);

                // Output to channels
                if (channels >= 2)
                {
                    if (offset + n < buffer.Length)
                        buffer[offset + n] = leftSample;
                    if (offset + n + 1 < buffer.Length)
                        buffer[offset + n + 1] = rightSample;
                }
                else
                {
                    if (offset + n < buffer.Length)
                        buffer[offset + n] = (leftSample + rightSample) * 0.5f;
                }
            }
        }

        return count;
    }

    private SubtractiveVoice? GetFreeVoice()
    {
        foreach (var voice in _voices)
        {
            if (!voice.IsActive) return voice;
        }

        if (_voices.Count < MaxVoices)
        {
            var voice = new SubtractiveVoice(_waveFormat.SampleRate, this);
            _voices.Add(voice);
            return voice;
        }

        // Voice stealing - oldest voice
        SubtractiveVoice? oldest = null;
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

    /// <summary>Creates a classic analog bass preset.</summary>
    public static SubtractiveSynth CreateAnalogBass()
    {
        return new SubtractiveSynth
        {
            Name = "Analog Bass",
            Osc1Waveform = WaveType.Sawtooth,
            Osc1Level = 1.0f,
            Osc2Waveform = WaveType.Square,
            Osc2Level = 0.5f,
            Osc2Detune = 5f,
            Osc3Waveform = WaveType.Sine,
            Osc3Level = 0.3f,
            Osc3Octave = -1,
            FilterCutoff = 0.3f,
            FilterResonance = 0.4f,
            FilterEnvAmount = 0.7f,
            FilterAttack = 0.001,
            FilterDecay = 0.3,
            FilterSustain = 0.2,
            FilterRelease = 0.2,
            AmpAttack = 0.001,
            AmpDecay = 0.1,
            AmpSustain = 0.8,
            AmpRelease = 0.1
        };
    }

    /// <summary>Creates a classic pad preset with LFO modulation.</summary>
    public static SubtractiveSynth CreateWarmPad()
    {
        return new SubtractiveSynth
        {
            Name = "Warm Pad",
            Osc1Waveform = WaveType.Sawtooth,
            Osc1Level = 0.7f,
            Osc2Waveform = WaveType.Sawtooth,
            Osc2Level = 0.7f,
            Osc2Detune = 12f,
            Osc3Level = 0f,
            FilterCutoff = 0.4f,
            FilterResonance = 0.2f,
            FilterEnvAmount = 0.3f,
            FilterAttack = 0.5,
            FilterDecay = 1.0,
            FilterSustain = 0.6,
            FilterRelease = 1.5,
            AmpAttack = 0.5,
            AmpDecay = 0.5,
            AmpSustain = 0.8,
            AmpRelease = 1.5,
            Lfo1Waveform = LfoWaveform.Triangle,
            Lfo1Destination = LfoDestination.FilterCutoff,
            Lfo1Rate = 0.3f,
            Lfo1Depth = 0.2f
        };
    }

    /// <summary>Creates a sync lead preset.</summary>
    public static SubtractiveSynth CreateSyncLead()
    {
        return new SubtractiveSynth
        {
            Name = "Sync Lead",
            Osc1Waveform = WaveType.Sawtooth,
            Osc1Level = 0.8f,
            Osc2Waveform = WaveType.Sawtooth,
            Osc2Level = 0.8f,
            Osc2Octave = 1,
            Osc2Detune = 0f,
            Osc2Sync = true,
            Osc3Level = 0f,
            FilterCutoff = 0.6f,
            FilterResonance = 0.4f,
            FilterEnvAmount = 0.5f,
            FilterAttack = 0.01,
            FilterDecay = 0.3,
            FilterSustain = 0.4,
            FilterRelease = 0.3,
            AmpAttack = 0.01,
            AmpDecay = 0.2,
            AmpSustain = 0.7,
            AmpRelease = 0.3,
            Lfo1Waveform = LfoWaveform.Sine,
            Lfo1Destination = LfoDestination.Pitch,
            Lfo1Rate = 5f,
            Lfo1Depth = 0.1f,
            Lfo1Sync = true
        };
    }

    /// <summary>Creates a PWM string preset.</summary>
    public static SubtractiveSynth CreatePwmStrings()
    {
        return new SubtractiveSynth
        {
            Name = "PWM Strings",
            Osc1Waveform = WaveType.Square,
            Osc1Level = 0.6f,
            Osc1PulseWidth = 0.3f,
            Osc2Waveform = WaveType.Square,
            Osc2Level = 0.6f,
            Osc2Detune = 10f,
            Osc2PulseWidth = 0.7f,
            Osc3Level = 0f,
            FilterCutoff = 0.5f,
            FilterResonance = 0.1f,
            FilterEnvAmount = 0.2f,
            FilterAttack = 0.3,
            FilterDecay = 0.5,
            FilterSustain = 0.7,
            FilterRelease = 0.8,
            AmpAttack = 0.3,
            AmpDecay = 0.3,
            AmpSustain = 0.9,
            AmpRelease = 0.5,
            Lfo1Waveform = LfoWaveform.Triangle,
            Lfo1Destination = LfoDestination.PulseWidth,
            Lfo1Rate = 0.5f,
            Lfo1Depth = 0.4f
        };
    }

    /// <summary>Creates an auto-pan arpeggio preset.</summary>
    public static SubtractiveSynth CreateAutoPanArp()
    {
        return new SubtractiveSynth
        {
            Name = "Auto Pan Arp",
            Osc1Waveform = WaveType.Sawtooth,
            Osc1Level = 0.8f,
            Osc2Waveform = WaveType.Square,
            Osc2Level = 0.4f,
            Osc2Octave = 1,
            Osc3Level = 0f,
            FilterCutoff = 0.6f,
            FilterResonance = 0.3f,
            FilterEnvAmount = 0.4f,
            FilterAttack = 0.01,
            FilterDecay = 0.2,
            FilterSustain = 0.3,
            FilterRelease = 0.2,
            AmpAttack = 0.01,
            AmpDecay = 0.15,
            AmpSustain = 0.5,
            AmpRelease = 0.2,
            Lfo1Waveform = LfoWaveform.Triangle,
            Lfo1Destination = LfoDestination.Pan,
            Lfo1Rate = 2f,
            Lfo1Depth = 0.8f,
            Lfo2Waveform = LfoWaveform.Sine,
            Lfo2Destination = LfoDestination.FilterCutoff,
            Lfo2Rate = 0.25f,
            Lfo2Depth = 0.3f
        };
    }

    /// <summary>Creates a resonant pluck preset.</summary>
    public static SubtractiveSynth CreateResonantPluck()
    {
        return new SubtractiveSynth
        {
            Name = "Resonant Pluck",
            Osc1Waveform = WaveType.Sawtooth,
            Osc1Level = 1.0f,
            Osc2Waveform = WaveType.Triangle,
            Osc2Level = 0.3f,
            Osc2Detune = 3f,
            Osc3Level = 0f,
            FilterMode = SubtractiveFilterMode.LowPass,
            FilterCutoff = 0.2f,
            FilterResonance = 0.7f,
            FilterEnvAmount = 0.8f,
            FilterAttack = 0.001,
            FilterDecay = 0.4,
            FilterSustain = 0.0,
            FilterRelease = 0.3,
            AmpAttack = 0.001,
            AmpDecay = 0.4,
            AmpSustain = 0.0,
            AmpRelease = 0.3
        };
    }

    #endregion
}
