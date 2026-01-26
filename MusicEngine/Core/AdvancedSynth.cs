// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Advanced synthesizer with multiple oscillators and filters.

using System;
using System.Collections.Generic;
using NAudio.Wave;


namespace MusicEngine.Core;


/// <summary>
/// Filter types for the synthesizer voice
/// </summary>
public enum SynthFilterType
{
    /// <summary>No filter</summary>
    None,
    /// <summary>Low-pass filter</summary>
    LowPass,
    /// <summary>High-pass filter</summary>
    HighPass,
    /// <summary>Band-pass filter</summary>
    BandPass,
    /// <summary>Notch filter</summary>
    Notch,
    /// <summary>Moog-style ladder filter</summary>
    MoogLadder
}


/// <summary>
/// Oscillator configuration for multi-oscillator synth
/// </summary>
public class OscillatorConfig
{
    /// <summary>Waveform type</summary>
    public WaveType Waveform { get; set; } = WaveType.Sawtooth;

    /// <summary>Volume level (0-1)</summary>
    public float Level { get; set; } = 1.0f;

    /// <summary>Detune in cents (-100 to 100)</summary>
    public float Detune { get; set; } = 0f;

    /// <summary>Octave offset (-2 to 2)</summary>
    public int Octave { get; set; } = 0;

    /// <summary>Semitone offset (-12 to 12)</summary>
    public int Semitone { get; set; } = 0;

    /// <summary>Pulse width for square wave (0.1 to 0.9)</summary>
    public float PulseWidth { get; set; } = 0.5f;

    /// <summary>Phase offset (0-1)</summary>
    public float Phase { get; set; } = 0f;

    /// <summary>Whether this oscillator is enabled</summary>
    public bool Enabled { get; set; } = true;
}


/// <summary>
/// Advanced synthesizer voice with envelope and filter
/// </summary>
internal class AdvancedVoice
{
    private readonly int _sampleRate;
    private readonly OscillatorConfig[] _oscConfigs;
    private readonly double[] _phases;
    private readonly Random _random = new();

    public int Note { get; private set; }
    public int Velocity { get; private set; }
    public double BaseFrequency { get; private set; }
    public Envelope AmpEnvelope { get; }
    public Envelope FilterEnvelope { get; }
    public bool IsActive => AmpEnvelope.IsActive;
    public bool IsReleasing => AmpEnvelope.Stage == EnvelopeStage.Release;
    public DateTime TriggerTime { get; private set; }

    // Filter state
    private double _filterState1;
    private double _filterState2;
    private double _filterState3;
    private double _filterState4;
    private double _lastInput;

    public AdvancedVoice(int sampleRate, OscillatorConfig[] oscConfigs)
    {
        _sampleRate = sampleRate;
        _oscConfigs = oscConfigs;
        _phases = new double[oscConfigs.Length];

        AmpEnvelope = new Envelope(0.01, 0.1, 0.7, 0.3);
        FilterEnvelope = new Envelope(0.01, 0.2, 0.5, 0.5);
    }

    public void Trigger(int note, int velocity)
    {
        Note = note;
        Velocity = velocity;
        BaseFrequency = 440.0 * Math.Pow(2.0, (note - 69.0) / 12.0);
        TriggerTime = DateTime.Now;

        // Reset phases with configured offsets
        for (int i = 0; i < _phases.Length; i++)
        {
            _phases[i] = _oscConfigs[i].Phase * 2 * Math.PI;
        }

        // Reset filter state
        _filterState1 = _filterState2 = _filterState3 = _filterState4 = 0;
        _lastInput = 0;

        AmpEnvelope.Trigger(velocity);
        FilterEnvelope.Trigger(velocity);
    }

    public void Release()
    {
        AmpEnvelope.Release_Gate();
        FilterEnvelope.Release_Gate();
    }

    public void Reset()
    {
        Note = -1;
        Velocity = 0;
        AmpEnvelope.Reset();
        FilterEnvelope.Reset();
    }

    public double Process(double deltaTime, float pitchMod, float filterCutoff, float resonance,
                          SynthFilterType filterType, float filterEnvAmount, float volume)
    {
        if (!IsActive) return 0;

        // Process envelopes
        double ampEnv = AmpEnvelope.Process(deltaTime);
        double filterEnv = FilterEnvelope.Process(deltaTime);

        if (AmpEnvelope.Stage == EnvelopeStage.Idle) return 0;

        // Mix oscillators
        double sample = 0;
        double totalLevel = 0;

        for (int i = 0; i < _oscConfigs.Length; i++)
        {
            var osc = _oscConfigs[i];
            if (!osc.Enabled || osc.Level <= 0) continue;

            // Calculate frequency with detuning
            double freq = BaseFrequency;
            freq *= Math.Pow(2.0, osc.Octave);
            freq *= Math.Pow(2.0, osc.Semitone / 12.0);
            freq *= Math.Pow(2.0, osc.Detune / 1200.0);
            freq *= Math.Pow(2.0, pitchMod / 12.0); // LFO pitch mod

            // Phase increment
            double phaseInc = 2.0 * Math.PI * freq / _sampleRate;
            _phases[i] += phaseInc;
            if (_phases[i] >= 2.0 * Math.PI) _phases[i] -= 2.0 * Math.PI;

            // Generate waveform
            double oscSample = GenerateWaveform(_phases[i], osc.Waveform, osc.PulseWidth);
            sample += oscSample * osc.Level;
            totalLevel += osc.Level;
        }

        // Normalize mix
        if (totalLevel > 0)
        {
            sample /= Math.Max(1.0, totalLevel * 0.5);
        }

        // Apply filter
        double effectiveCutoff = filterCutoff + (filterEnv * filterEnvAmount);
        effectiveCutoff = Math.Clamp(effectiveCutoff, 0, 1);

        sample = ApplyFilter(sample, effectiveCutoff, resonance, filterType);

        // Apply amplitude envelope and velocity
        sample *= ampEnv * (Velocity / 127.0) * volume;

        return sample;
    }

    private double GenerateWaveform(double phase, WaveType type, float pulseWidth)
    {
        return type switch
        {
            WaveType.Sine => Math.Sin(phase),
            WaveType.Square => phase < (2 * Math.PI * pulseWidth) ? 1.0 : -1.0,
            WaveType.Sawtooth => (phase / Math.PI) - 1.0,
            WaveType.Triangle => phase < Math.PI
                ? (2.0 * phase / Math.PI) - 1.0
                : 3.0 - (2.0 * phase / Math.PI),
            WaveType.Noise => _random.NextDouble() * 2.0 - 1.0,
            _ => 0
        };
    }

    private double ApplyFilter(double input, double cutoff, double resonance, SynthFilterType type)
    {
        if (type == SynthFilterType.None) return input;

        // Calculate filter coefficients
        // Cutoff is 0-1, map to frequency range 20Hz - 20kHz
        double freq = 20.0 * Math.Pow(1000.0, cutoff);
        freq = Math.Min(freq, _sampleRate * 0.45);

        double w0 = 2.0 * Math.PI * freq / _sampleRate;
        double cosW0 = Math.Cos(w0);
        double sinW0 = Math.Sin(w0);
        double alpha = sinW0 / (2.0 * (1.0 - resonance * 0.9 + 0.1));

        double output;

        switch (type)
        {
            case SynthFilterType.LowPass:
                {
                    // Simple one-pole lowpass
                    double rc = 1.0 / (2.0 * Math.PI * freq);
                    double dt = 1.0 / _sampleRate;
                    double a = dt / (rc + dt);
                    _filterState1 = _filterState1 + a * (input - _filterState1);
                    output = _filterState1;
                }
                break;

            case SynthFilterType.HighPass:
                {
                    // Simple one-pole highpass
                    double rc = 1.0 / (2.0 * Math.PI * freq);
                    double dt = 1.0 / _sampleRate;
                    double a = rc / (rc + dt);
                    output = a * (_filterState1 + input - _lastInput);
                    _filterState1 = output;
                    _lastInput = input;
                }
                break;

            case SynthFilterType.BandPass:
                {
                    // Two-pole bandpass
                    double bandwidth = 1.0; // Octaves
                    double q = 1.0 / (2.0 * Math.Sinh(Math.Log(2) / 2.0 * bandwidth * w0 / sinW0));
                    double a0 = 1 + alpha / q;
                    double b1 = sinW0 / 2.0 / a0;
                    double a1 = -2 * cosW0 / a0;
                    double a2 = (1 - alpha / q) / a0;

                    output = b1 * input - b1 * _filterState2 - a1 * _filterState1 - a2 * _filterState3;
                    _filterState3 = _filterState1;
                    _filterState2 = input;
                    _filterState1 = output;
                }
                break;

            case SynthFilterType.Notch:
                {
                    // Notch filter
                    double a0 = 1 + alpha;
                    double b0 = 1 / a0;
                    double b1 = -2 * cosW0 / a0;
                    double a1 = b1;
                    double a2 = (1 - alpha) / a0;

                    output = b0 * input + b1 * _filterState2 + b0 * _filterState1 - a1 * _filterState3 - a2 * _filterState4;
                    _filterState4 = _filterState3;
                    _filterState3 = output;
                    _filterState2 = _filterState1;
                    _filterState1 = input;
                }
                break;

            case SynthFilterType.MoogLadder:
                {
                    // Moog ladder filter approximation
                    double fc = freq / _sampleRate;
                    double f = fc * 1.16;
                    double fb = resonance * 4.0 * (1.0 - 0.15 * f * f);

                    input -= _filterState4 * fb;
                    input *= 0.35013 * (f * f) * (f * f);

                    _filterState1 = input + 0.3 * _filterState1 + (1 - f) * _filterState1;
                    _filterState2 = _filterState1 + 0.3 * _filterState2 + (1 - f) * _filterState2;
                    _filterState3 = _filterState2 + 0.3 * _filterState3 + (1 - f) * _filterState3;
                    _filterState4 = _filterState3 + 0.3 * _filterState4 + (1 - f) * _filterState4;

                    output = _filterState4;
                }
                break;

            default:
                output = input;
                break;
        }

        return output;
    }
}


/// <summary>
/// Advanced synthesizer with multiple oscillators, filters, and modulation.
/// </summary>
public class AdvancedSynth : ISynth
{
    private readonly WaveFormat _waveFormat;
    private readonly AdvancedVoice[] _voices;
    private readonly OscillatorConfig[] _oscillators;
    private readonly Dictionary<int, int> _noteToVoice = new();
    private readonly object _lock = new();
    private const int MaxVoices = 16;

    /// <summary>
    /// Gets or sets the synth name
    /// </summary>
    public string Name { get; set; } = "AdvancedSynth";

    /// <summary>
    /// Gets the audio format
    /// </summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>
    /// Gets the oscillator configurations
    /// </summary>
    public OscillatorConfig[] Oscillators => _oscillators;

    /// <summary>
    /// Gets or sets the master volume (0-1)
    /// </summary>
    public float Volume { get; set; } = 0.5f;

    /// <summary>
    /// Gets or sets the filter type
    /// </summary>
    public SynthFilterType FilterType { get; set; } = SynthFilterType.LowPass;

    /// <summary>
    /// Gets or sets the filter cutoff (0-1)
    /// </summary>
    public float FilterCutoff { get; set; } = 0.8f;

    /// <summary>
    /// Gets or sets the filter resonance (0-1)
    /// </summary>
    public float FilterResonance { get; set; } = 0.2f;

    /// <summary>
    /// Gets or sets the filter envelope amount (-1 to 1)
    /// </summary>
    public float FilterEnvAmount { get; set; } = 0.3f;

    /// <summary>
    /// Gets or sets the voice stealing mode
    /// </summary>
    public VoiceStealMode StealMode { get; set; } = VoiceStealMode.Oldest;

    /// <summary>
    /// LFO for pitch modulation (vibrato)
    /// </summary>
    public LFO? PitchLFO { get; set; }

    /// <summary>
    /// LFO for filter modulation
    /// </summary>
    public LFO? FilterLFO { get; set; }

    /// <summary>
    /// Pitch LFO depth in semitones
    /// </summary>
    public float PitchLFODepth { get; set; } = 0f;

    /// <summary>
    /// Filter LFO depth (0-1)
    /// </summary>
    public float FilterLFODepth { get; set; } = 0f;

    /// <summary>
    /// Glide/portamento time in seconds
    /// </summary>
    public float GlideTime { get; set; } = 0f;

    /// <summary>
    /// Gets or sets the envelope attack time
    /// </summary>
    public double Attack
    {
        get => _voices[0].AmpEnvelope.Attack;
        set { foreach (var v in _voices) v.AmpEnvelope.Attack = value; }
    }

    /// <summary>
    /// Gets or sets the envelope decay time
    /// </summary>
    public double Decay
    {
        get => _voices[0].AmpEnvelope.Decay;
        set { foreach (var v in _voices) v.AmpEnvelope.Decay = value; }
    }

    /// <summary>
    /// Gets or sets the envelope sustain level
    /// </summary>
    public double Sustain
    {
        get => _voices[0].AmpEnvelope.Sustain;
        set { foreach (var v in _voices) v.AmpEnvelope.Sustain = value; }
    }

    /// <summary>
    /// Gets or sets the envelope release time
    /// </summary>
    public double Release
    {
        get => _voices[0].AmpEnvelope.Release;
        set { foreach (var v in _voices) v.AmpEnvelope.Release = value; }
    }

    /// <summary>
    /// Gets or sets the filter envelope attack
    /// </summary>
    public double FilterAttack
    {
        get => _voices[0].FilterEnvelope.Attack;
        set { foreach (var v in _voices) v.FilterEnvelope.Attack = value; }
    }

    /// <summary>
    /// Gets or sets the filter envelope decay
    /// </summary>
    public double FilterDecay
    {
        get => _voices[0].FilterEnvelope.Decay;
        set { foreach (var v in _voices) v.FilterEnvelope.Decay = value; }
    }

    /// <summary>
    /// Gets or sets the filter envelope sustain
    /// </summary>
    public double FilterSustain
    {
        get => _voices[0].FilterEnvelope.Sustain;
        set { foreach (var v in _voices) v.FilterEnvelope.Sustain = value; }
    }

    /// <summary>
    /// Gets or sets the filter envelope release
    /// </summary>
    public double FilterRelease
    {
        get => _voices[0].FilterEnvelope.Release;
        set { foreach (var v in _voices) v.FilterEnvelope.Release = value; }
    }

    /// <summary>
    /// Creates an advanced synth with default 3 oscillators
    /// </summary>
    public AdvancedSynth(int oscillatorCount = 3, int? sampleRate = null)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);

        // Initialize oscillators
        _oscillators = new OscillatorConfig[oscillatorCount];
        for (int i = 0; i < oscillatorCount; i++)
        {
            _oscillators[i] = new OscillatorConfig
            {
                Waveform = i == 0 ? WaveType.Sawtooth : WaveType.Square,
                Level = 1.0f / oscillatorCount,
                Detune = i * 7f, // Slight detune for richness
                Enabled = i == 0 // Only first osc enabled by default
            };
        }

        // Initialize voices
        _voices = new AdvancedVoice[MaxVoices];
        for (int i = 0; i < MaxVoices; i++)
        {
            _voices[i] = new AdvancedVoice(rate, _oscillators);
        }
    }

    /// <summary>
    /// Enable or configure an oscillator
    /// </summary>
    public void SetOscillator(int index, WaveType waveform, float level = 1.0f, float detune = 0f,
                              int octave = 0, int semitone = 0, float pulseWidth = 0.5f)
    {
        if (index < 0 || index >= _oscillators.Length) return;

        var osc = _oscillators[index];
        osc.Waveform = waveform;
        osc.Level = level;
        osc.Detune = detune;
        osc.Octave = octave;
        osc.Semitone = semitone;
        osc.PulseWidth = pulseWidth;
        osc.Enabled = true;
    }

    /// <summary>
    /// Trigger a note
    /// </summary>
    public void NoteOn(int note, int velocity)
    {
        lock (_lock)
        {
            // Check if note is already playing
            if (_noteToVoice.TryGetValue(note, out int existingVoice))
            {
                _voices[existingVoice].Trigger(note, velocity);
                return;
            }

            // Find free voice
            int voiceIndex = FindFreeVoice(note);
            if (voiceIndex < 0) return;

            // Remove old mapping
            var oldMapping = new List<int>();
            foreach (var kvp in _noteToVoice)
            {
                if (kvp.Value == voiceIndex) oldMapping.Add(kvp.Key);
            }
            foreach (var key in oldMapping)
            {
                _noteToVoice.Remove(key);
            }

            _voices[voiceIndex].Trigger(note, velocity);
            _noteToVoice[note] = voiceIndex;
        }
    }

    /// <summary>
    /// Release a note
    /// </summary>
    public void NoteOff(int note)
    {
        lock (_lock)
        {
            if (_noteToVoice.TryGetValue(note, out int voiceIndex))
            {
                _voices[voiceIndex].Release();
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
            case "filterenvamount":
                FilterEnvAmount = Math.Clamp(value, -1f, 1f);
                break;
            case "attack":
                Attack = value;
                break;
            case "decay":
                Decay = value;
                break;
            case "sustain":
                Sustain = value;
                break;
            case "release":
                Release = value;
                break;
            case "filterattack":
                FilterAttack = value;
                break;
            case "filterdecay":
                FilterDecay = value;
                break;
            case "filtersustain":
                FilterSustain = value;
                break;
            case "filterrelease":
                FilterRelease = value;
                break;
            case "pitchlfodepth":
            case "vibrato":
                PitchLFODepth = value;
                break;
            case "filterlfodepth":
                FilterLFODepth = value;
                break;
            case "glide":
            case "portamento":
                GlideTime = value;
                break;
        }
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

        // Get LFO values
        float pitchMod = 0;
        float filterMod = 0;

        if (PitchLFO != null && PitchLFO.Enabled)
        {
            pitchMod = (float)(PitchLFO.GetValue(_waveFormat.SampleRate) * PitchLFODepth);
        }

        if (FilterLFO != null && FilterLFO.Enabled)
        {
            filterMod = (float)(FilterLFO.GetValue(_waveFormat.SampleRate) * FilterLFODepth);
        }

        float effectiveCutoff = Math.Clamp(FilterCutoff + filterMod, 0f, 1f);

        lock (_lock)
        {
            for (int n = 0; n < count; n += channels)
            {
                double sample = 0;

                foreach (var voice in _voices)
                {
                    if (!voice.IsActive) continue;

                    sample += voice.Process(deltaTime, pitchMod, effectiveCutoff,
                                            FilterResonance, FilterType, FilterEnvAmount, Volume);
                }

                // Soft clipping
                sample = Math.Tanh(sample);

                // Output to all channels
                for (int c = 0; c < channels; c++)
                {
                    if (offset + n + c < buffer.Length)
                    {
                        buffer[offset + n + c] = (float)sample;
                    }
                }
            }
        }

        return count;
    }

    private int FindFreeVoice(int newNote)
    {
        // Look for inactive voice
        for (int i = 0; i < _voices.Length; i++)
        {
            if (!_voices[i].IsActive) return i;
        }

        // Voice stealing
        return StealMode switch
        {
            VoiceStealMode.None => -1,
            VoiceStealMode.Oldest => FindOldestVoice(),
            VoiceStealMode.Lowest => FindLowestVoice(),
            VoiceStealMode.Highest => FindHighestVoice(),
            VoiceStealMode.SameNote => FindSameNoteVoice(newNote),
            _ => FindOldestVoice()
        };
    }

    private int FindOldestVoice()
    {
        int oldest = 0;
        var oldestTime = _voices[0].TriggerTime;
        for (int i = 1; i < _voices.Length; i++)
        {
            if (_voices[i].TriggerTime < oldestTime)
            {
                oldest = i;
                oldestTime = _voices[i].TriggerTime;
            }
        }
        return oldest;
    }

    private int FindLowestVoice()
    {
        int lowest = 0;
        int lowestNote = _voices[0].Note;
        for (int i = 1; i < _voices.Length; i++)
        {
            if (_voices[i].Note < lowestNote)
            {
                lowest = i;
                lowestNote = _voices[i].Note;
            }
        }
        return lowest;
    }

    private int FindHighestVoice()
    {
        int highest = 0;
        int highestNote = _voices[0].Note;
        for (int i = 1; i < _voices.Length; i++)
        {
            if (_voices[i].Note > highestNote)
            {
                highest = i;
                highestNote = _voices[i].Note;
            }
        }
        return highest;
    }

    private int FindSameNoteVoice(int note)
    {
        for (int i = 0; i < _voices.Length; i++)
        {
            if (_voices[i].Note == note) return i;
        }
        return FindOldestVoice();
    }

    /// <summary>
    /// Create a preset: Classic analog lead
    /// </summary>
    public static AdvancedSynth CreateLeadPreset()
    {
        var synth = new AdvancedSynth(2);
        synth.Name = "Lead";
        synth.SetOscillator(0, WaveType.Sawtooth, 0.7f, 0);
        synth.SetOscillator(1, WaveType.Square, 0.5f, 7);
        synth.FilterType = SynthFilterType.MoogLadder;
        synth.FilterCutoff = 0.6f;
        synth.FilterResonance = 0.3f;
        synth.FilterEnvAmount = 0.4f;
        synth.Attack = 0.01;
        synth.Decay = 0.2;
        synth.Sustain = 0.6;
        synth.Release = 0.2;
        return synth;
    }

    /// <summary>
    /// Create a preset: Pad sound
    /// </summary>
    public static AdvancedSynth CreatePadPreset()
    {
        var synth = new AdvancedSynth(3);
        synth.Name = "Pad";
        synth.SetOscillator(0, WaveType.Sawtooth, 0.5f, -5);
        synth.SetOscillator(1, WaveType.Sawtooth, 0.5f, 0);
        synth.SetOscillator(2, WaveType.Sawtooth, 0.5f, 5);
        synth.FilterType = SynthFilterType.LowPass;
        synth.FilterCutoff = 0.4f;
        synth.FilterResonance = 0.1f;
        synth.Attack = 0.5;
        synth.Decay = 0.5;
        synth.Sustain = 0.8;
        synth.Release = 1.0;
        return synth;
    }

    /// <summary>
    /// Create a preset: Bass sound
    /// </summary>
    public static AdvancedSynth CreateBassPreset()
    {
        var synth = new AdvancedSynth(2);
        synth.Name = "Bass";
        synth.SetOscillator(0, WaveType.Square, 0.8f, 0, 0, 0, 0.3f);
        synth.SetOscillator(1, WaveType.Sawtooth, 0.4f, 0, -1);
        synth.FilterType = SynthFilterType.MoogLadder;
        synth.FilterCutoff = 0.3f;
        synth.FilterResonance = 0.4f;
        synth.FilterEnvAmount = 0.6f;
        synth.Attack = 0.001;
        synth.Decay = 0.15;
        synth.Sustain = 0.4;
        synth.Release = 0.1;
        synth.FilterAttack = 0.001;
        synth.FilterDecay = 0.2;
        synth.FilterSustain = 0.2;
        synth.FilterRelease = 0.1;
        return synth;
    }

    /// <summary>
    /// Create a preset: Pluck sound
    /// </summary>
    public static AdvancedSynth CreatePluckPreset()
    {
        var synth = new AdvancedSynth(2);
        synth.Name = "Pluck";
        synth.SetOscillator(0, WaveType.Sawtooth, 0.6f, 0);
        synth.SetOscillator(1, WaveType.Triangle, 0.4f, 12);
        synth.FilterType = SynthFilterType.LowPass;
        synth.FilterCutoff = 0.5f;
        synth.FilterResonance = 0.2f;
        synth.FilterEnvAmount = 0.7f;
        synth.Attack = 0.001;
        synth.Decay = 0.3;
        synth.Sustain = 0.0;
        synth.Release = 0.3;
        synth.FilterAttack = 0.001;
        synth.FilterDecay = 0.2;
        synth.FilterSustain = 0.0;
        synth.FilterRelease = 0.2;
        return synth;
    }
}
