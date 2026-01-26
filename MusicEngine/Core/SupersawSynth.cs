// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using NAudio.Wave;

namespace MusicEngine.Core;

/// <summary>
/// Supersaw oscillator with multiple detuned sawtooth waves
/// </summary>
internal class SupersawOscillator
{
    private readonly double[] _phases;
    private readonly double[] _detuneFactors;
    private readonly int _unisonCount;
    private readonly Random _random = new();

    public int UnisonCount => _unisonCount;

    public SupersawOscillator(int unisonCount = 7)
    {
        _unisonCount = Math.Clamp(unisonCount, 1, 16);
        _phases = new double[_unisonCount];
        _detuneFactors = new double[_unisonCount];

        // Initialize random phases for organic sound
        for (int i = 0; i < _unisonCount; i++)
        {
            _phases[i] = _random.NextDouble();
        }

        UpdateDetuning(0.5f);
    }

    /// <summary>
    /// Update detuning spread (0-1)
    /// </summary>
    public void UpdateDetuning(float detune)
    {
        // Spread oscillators evenly around center
        // detune = 0 means all in tune, detune = 1 means max spread (~50 cents)
        float maxCents = detune * 50f;

        for (int i = 0; i < _unisonCount; i++)
        {
            float normalizedPos = (float)i / (_unisonCount - 1) - 0.5f; // -0.5 to 0.5
            if (_unisonCount == 1) normalizedPos = 0;

            float cents = normalizedPos * 2f * maxCents;
            _detuneFactors[i] = Math.Pow(2.0, cents / 1200.0);
        }
    }

    /// <summary>
    /// Generate supersaw sample
    /// </summary>
    public float Process(double baseFrequency, int sampleRate, float[] panPositions)
    {
        float outputL = 0f;
        float outputR = 0f;
        float amplitude = 1f / MathF.Sqrt(_unisonCount); // Normalize by sqrt for perceived loudness

        for (int i = 0; i < _unisonCount; i++)
        {
            double freq = baseFrequency * _detuneFactors[i];
            double phaseIncrement = freq / sampleRate;

            _phases[i] += phaseIncrement;
            if (_phases[i] >= 1.0) _phases[i] -= 1.0;

            // Generate sawtooth: 2 * phase - 1
            float saw = (float)(_phases[i] * 2.0 - 1.0);

            // Apply pan position
            float pan = panPositions[i];
            float gainL = MathF.Cos(pan * MathF.PI * 0.5f);
            float gainR = MathF.Sin(pan * MathF.PI * 0.5f);

            outputL += saw * amplitude * gainL;
            outputR += saw * amplitude * gainR;
        }

        // Return mono mix (stereo handled at voice level)
        return (outputL + outputR) * 0.5f;
    }

    /// <summary>
    /// Generate stereo supersaw sample
    /// </summary>
    public (float left, float right) ProcessStereo(double baseFrequency, int sampleRate, float[] panPositions)
    {
        float outputL = 0f;
        float outputR = 0f;
        float amplitude = 1f / MathF.Sqrt(_unisonCount);

        for (int i = 0; i < _unisonCount; i++)
        {
            double freq = baseFrequency * _detuneFactors[i];
            double phaseIncrement = freq / sampleRate;

            _phases[i] += phaseIncrement;
            if (_phases[i] >= 1.0) _phases[i] -= 1.0;

            float saw = (float)(_phases[i] * 2.0 - 1.0);

            float pan = panPositions[i];
            float gainL = MathF.Cos(pan * MathF.PI * 0.5f);
            float gainR = MathF.Sin(pan * MathF.PI * 0.5f);

            outputL += saw * amplitude * gainL;
            outputR += saw * amplitude * gainR;
        }

        return (outputL, outputR);
    }

    public void Reset()
    {
        for (int i = 0; i < _unisonCount; i++)
        {
            _phases[i] = _random.NextDouble();
        }
    }
}

/// <summary>
/// Supersaw voice with envelopes and filter
/// </summary>
internal class SupersawVoice
{
    private readonly int _sampleRate;
    private readonly SupersawSynth _synth;
    private readonly SupersawOscillator _oscillator;
    private readonly Envelope _ampEnvelope;
    private readonly Envelope _filterEnvelope;
    private readonly float[] _panPositions;

    // Filter state (state-variable)
    private float _filterLowL, _filterBandL, _filterHighL;
    private float _filterLowR, _filterBandR, _filterHighR;

    public int Note { get; private set; }
    public int Velocity { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime TriggerTime { get; private set; }
    public double BaseFrequency { get; private set; }

    public SupersawVoice(int sampleRate, SupersawSynth synth, int unisonCount)
    {
        _sampleRate = sampleRate;
        _synth = synth;
        _oscillator = new SupersawOscillator(unisonCount);
        _ampEnvelope = new Envelope(0.01, 0.1, 0.8, 0.3);
        _filterEnvelope = new Envelope(0.01, 0.3, 0.5, 0.2);
        _panPositions = new float[unisonCount];

        // Spread oscillators across stereo field
        UpdateStereoSpread(0.7f);
    }

    public void UpdateStereoSpread(float spread)
    {
        int count = _oscillator.UnisonCount;
        for (int i = 0; i < count; i++)
        {
            float normalizedPos = count > 1 ? (float)i / (count - 1) : 0.5f;
            _panPositions[i] = 0.5f + (normalizedPos - 0.5f) * spread;
        }
    }

    public void Trigger(int note, int velocity)
    {
        Note = note;
        Velocity = velocity;
        IsActive = true;
        TriggerTime = DateTime.Now;

        BaseFrequency = 440.0 * Math.Pow(2.0, (note - 69.0) / 12.0);

        _oscillator.UpdateDetuning(_synth.Detune);

        // Copy envelope settings
        _ampEnvelope.Attack = _synth.AmpEnvelope.Attack;
        _ampEnvelope.Decay = _synth.AmpEnvelope.Decay;
        _ampEnvelope.Sustain = _synth.AmpEnvelope.Sustain;
        _ampEnvelope.Release = _synth.AmpEnvelope.Release;

        _filterEnvelope.Attack = _synth.FilterEnvelope.Attack;
        _filterEnvelope.Decay = _synth.FilterEnvelope.Decay;
        _filterEnvelope.Sustain = _synth.FilterEnvelope.Sustain;
        _filterEnvelope.Release = _synth.FilterEnvelope.Release;

        _ampEnvelope.Trigger(velocity);
        _filterEnvelope.Trigger(velocity);

        // Reset filter state
        _filterLowL = _filterBandL = _filterHighL = 0;
        _filterLowR = _filterBandR = _filterHighR = 0;
    }

    public void Release()
    {
        _ampEnvelope.Release_Gate();
        _filterEnvelope.Release_Gate();
    }

    public (float left, float right) Process(double deltaTime)
    {
        if (!IsActive) return (0, 0);

        // Process envelopes
        float ampEnv = (float)_ampEnvelope.Process(deltaTime);
        float filterEnv = (float)_filterEnvelope.Process(deltaTime);

        if (!_ampEnvelope.IsActive)
        {
            IsActive = false;
            return (0, 0);
        }

        // Apply pitch bend
        double pitchMod = _synth.PitchBend * _synth.PitchBendRange;
        double freq = BaseFrequency * Math.Pow(2.0, pitchMod / 12.0);

        // Update detuning in real-time
        _oscillator.UpdateDetuning(_synth.Detune);
        UpdateStereoSpread(_synth.StereoSpread);

        // Generate oscillator
        var (oscL, oscR) = _oscillator.ProcessStereo(freq, _sampleRate, _panPositions);

        // Apply filter
        float cutoff = _synth.FilterCutoff + filterEnv * _synth.FilterEnvelopeAmount;
        cutoff = Math.Clamp(cutoff, 20f, 20000f);

        float f = 2f * MathF.Sin(MathF.PI * cutoff / _sampleRate);
        float q = 1f / _synth.FilterResonance;

        // Left channel filter
        _filterLowL += f * _filterBandL;
        _filterHighL = oscL - _filterLowL - q * _filterBandL;
        _filterBandL += f * _filterHighL;

        // Right channel filter
        _filterLowR += f * _filterBandR;
        _filterHighR = oscR - _filterLowR - q * _filterBandR;
        _filterBandR += f * _filterHighR;

        float outL = _filterLowL;
        float outR = _filterLowR;

        // Apply amplitude envelope and velocity
        float velocityGain = Velocity / 127f;
        outL *= ampEnv * velocityGain;
        outR *= ampEnv * velocityGain;

        return (outL, outR);
    }
}

/// <summary>
/// Supersaw synthesizer with multiple detuned unison oscillators.
/// Creates the classic trance/EDM lead sound (JP-8000 style).
/// </summary>
public class SupersawSynth : ISynth
{
    private readonly WaveFormat _waveFormat;
    private readonly List<SupersawVoice> _voices = new();
    private readonly Dictionary<int, SupersawVoice> _noteToVoice = new();
    private readonly object _lock = new();

    /// <summary>Synth name</summary>
    public string Name { get; set; } = "SupersawSynth";

    /// <summary>Audio format</summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>Maximum polyphony</summary>
    public int MaxVoices { get; set; } = 8;

    /// <summary>Master volume (0-1)</summary>
    public float Volume { get; set; } = 0.5f;

    /// <summary>Number of unison oscillators (1-16)</summary>
    public int UnisonCount { get; set; } = 7;

    /// <summary>Detune amount (0-1, 0=no detune, 1=max ~50 cents)</summary>
    public float Detune { get; set; } = 0.5f;

    /// <summary>Stereo spread (0-1)</summary>
    public float StereoSpread { get; set; } = 0.7f;

    /// <summary>Filter cutoff frequency (20-20000 Hz)</summary>
    public float FilterCutoff { get; set; } = 8000f;

    /// <summary>Filter resonance (0.5-10)</summary>
    public float FilterResonance { get; set; } = 1.0f;

    /// <summary>Filter envelope amount in Hz</summary>
    public float FilterEnvelopeAmount { get; set; } = 3000f;

    /// <summary>Amplitude envelope</summary>
    public Envelope AmpEnvelope { get; }

    /// <summary>Filter envelope</summary>
    public Envelope FilterEnvelope { get; }

    /// <summary>Pitch bend range in semitones</summary>
    public float PitchBendRange { get; set; } = 2f;

    /// <summary>Current pitch bend (-1 to 1)</summary>
    public float PitchBend { get; set; } = 0f;

    /// <summary>
    /// Creates a new SupersawSynth
    /// </summary>
    public SupersawSynth(int? sampleRate = null)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, 2);

        AmpEnvelope = new Envelope(0.01, 0.1, 0.8, 0.3);
        FilterEnvelope = new Envelope(0.01, 0.3, 0.5, 0.2);
    }

    private SupersawVoice? GetFreeVoice()
    {
        lock (_lock)
        {
            foreach (var voice in _voices)
            {
                if (!voice.IsActive) return voice;
            }

            if (_voices.Count < MaxVoices)
            {
                var voice = new SupersawVoice(_waveFormat.SampleRate, this, UnisonCount);
                _voices.Add(voice);
                return voice;
            }

            // Voice stealing
            SupersawVoice? oldest = null;
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
    }

    #region ISynth Implementation

    public void NoteOn(int note, int velocity)
    {
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

    public void SetParameter(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "volume": Volume = Math.Clamp(value, 0f, 1f); break;
            case "detune": Detune = Math.Clamp(value, 0f, 1f); break;
            case "stereospread": StereoSpread = Math.Clamp(value, 0f, 1f); break;
            case "filtercutoff": FilterCutoff = Math.Clamp(value, 20f, 20000f); break;
            case "filterresonance": FilterResonance = Math.Clamp(value, 0.5f, 10f); break;
            case "filterenvamount": FilterEnvelopeAmount = value; break;
            case "pitchbend": PitchBend = Math.Clamp(value, -1f, 1f); break;
            case "pitchbendrange": PitchBendRange = Math.Clamp(value, 0f, 24f); break;

            case "attack": AmpEnvelope.Attack = value; break;
            case "decay": AmpEnvelope.Decay = value; break;
            case "sustain": AmpEnvelope.Sustain = value; break;
            case "release": AmpEnvelope.Release = value; break;

            case "filter_attack": FilterEnvelope.Attack = value; break;
            case "filter_decay": FilterEnvelope.Decay = value; break;
            case "filter_sustain": FilterEnvelope.Sustain = value; break;
            case "filter_release": FilterEnvelope.Release = value; break;
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        Array.Clear(buffer, offset, count);

        double deltaTime = 1.0 / _waveFormat.SampleRate;

        lock (_lock)
        {
            for (int n = 0; n < count; n += 2)
            {
                float sampleL = 0f;
                float sampleR = 0f;

                foreach (var voice in _voices)
                {
                    if (!voice.IsActive) continue;
                    var (left, right) = voice.Process(deltaTime);
                    sampleL += left;
                    sampleR += right;
                }

                // Apply volume and soft clipping
                sampleL *= Volume;
                sampleR *= Volume;
                sampleL = MathF.Tanh(sampleL);
                sampleR = MathF.Tanh(sampleR);

                buffer[offset + n] = sampleL;
                buffer[offset + n + 1] = sampleR;
            }
        }

        return count;
    }

    #endregion

    #region Presets

    /// <summary>Classic trance lead preset</summary>
    public static SupersawSynth CreateTranceLead()
    {
        var synth = new SupersawSynth
        {
            Name = "Trance Lead",
            UnisonCount = 7,
            Detune = 0.4f,
            StereoSpread = 0.8f,
            FilterCutoff = 4000f,
            FilterResonance = 1.5f,
            FilterEnvelopeAmount = 5000f
        };

        synth.AmpEnvelope.Attack = 0.01;
        synth.AmpEnvelope.Decay = 0.2;
        synth.AmpEnvelope.Sustain = 0.7;
        synth.AmpEnvelope.Release = 0.3;

        synth.FilterEnvelope.Attack = 0.01;
        synth.FilterEnvelope.Decay = 0.5;
        synth.FilterEnvelope.Sustain = 0.3;
        synth.FilterEnvelope.Release = 0.3;

        return synth;
    }

    /// <summary>Massive EDM chord preset</summary>
    public static SupersawSynth CreateEDMChord()
    {
        var synth = new SupersawSynth
        {
            Name = "EDM Chord",
            UnisonCount = 9,
            Detune = 0.6f,
            StereoSpread = 0.9f,
            FilterCutoff = 6000f,
            FilterResonance = 0.8f,
            FilterEnvelopeAmount = 2000f
        };

        synth.AmpEnvelope.Attack = 0.005;
        synth.AmpEnvelope.Decay = 0.1;
        synth.AmpEnvelope.Sustain = 0.9;
        synth.AmpEnvelope.Release = 0.2;

        synth.FilterEnvelope.Attack = 0.01;
        synth.FilterEnvelope.Decay = 0.3;
        synth.FilterEnvelope.Sustain = 0.5;
        synth.FilterEnvelope.Release = 0.2;

        return synth;
    }

    /// <summary>Soft pad preset</summary>
    public static SupersawSynth CreateSoftPad()
    {
        var synth = new SupersawSynth
        {
            Name = "Soft Pad",
            UnisonCount = 5,
            Detune = 0.3f,
            StereoSpread = 0.6f,
            FilterCutoff = 2000f,
            FilterResonance = 0.7f,
            FilterEnvelopeAmount = 1000f
        };

        synth.AmpEnvelope.Attack = 0.5;
        synth.AmpEnvelope.Decay = 0.5;
        synth.AmpEnvelope.Sustain = 0.8;
        synth.AmpEnvelope.Release = 1.5;

        synth.FilterEnvelope.Attack = 0.8;
        synth.FilterEnvelope.Decay = 1.0;
        synth.FilterEnvelope.Sustain = 0.4;
        synth.FilterEnvelope.Release = 1.0;

        return synth;
    }

    /// <summary>Aggressive bass preset</summary>
    public static SupersawSynth CreateAggressiveBass()
    {
        var synth = new SupersawSynth
        {
            Name = "Aggressive Bass",
            UnisonCount = 3,
            Detune = 0.15f,
            StereoSpread = 0.3f,
            FilterCutoff = 1500f,
            FilterResonance = 2.0f,
            FilterEnvelopeAmount = 4000f
        };

        synth.AmpEnvelope.Attack = 0.001;
        synth.AmpEnvelope.Decay = 0.15;
        synth.AmpEnvelope.Sustain = 0.6;
        synth.AmpEnvelope.Release = 0.1;

        synth.FilterEnvelope.Attack = 0.001;
        synth.FilterEnvelope.Decay = 0.2;
        synth.FilterEnvelope.Sustain = 0.2;
        synth.FilterEnvelope.Release = 0.1;

        return synth;
    }

    #endregion
}
