//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Additive synthesizer using harmonic series synthesis.
// Organ-style drawbar control with per-harmonic amplitude and phase.

using NAudio.Wave;

namespace MusicEngine.Core;

/// <summary>
/// Configuration for a single harmonic partial
/// </summary>
public class HarmonicPartial
{
    /// <summary>Harmonic number (1 = fundamental, 2 = octave, etc.)</summary>
    public int HarmonicNumber { get; set; } = 1;

    /// <summary>Amplitude (0-1)</summary>
    public float Amplitude { get; set; } = 1.0f;

    /// <summary>Phase offset (0-1, representing 0-360 degrees)</summary>
    public float Phase { get; set; } = 0f;

    /// <summary>Detune in cents (-100 to 100)</summary>
    public float Detune { get; set; } = 0f;

    /// <summary>Envelope for this partial (null = use global)</summary>
    public Envelope? Envelope { get; set; }

    public HarmonicPartial() { }

    public HarmonicPartial(int harmonicNumber, float amplitude, float phase = 0f)
    {
        HarmonicNumber = harmonicNumber;
        Amplitude = amplitude;
        Phase = phase;
    }
}

/// <summary>
/// Voice for additive synthesis
/// </summary>
internal class AdditiveVoice
{
    private readonly int _sampleRate;
    private readonly AdditiveSynth _synth;
    private readonly double[] _phases;
    private readonly Envelope[] _partialEnvelopes;
    private readonly Envelope _ampEnvelope;

    public int Note { get; private set; }
    public int Velocity { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime TriggerTime { get; private set; }
    public double BaseFrequency { get; private set; }

    public AdditiveVoice(int sampleRate, AdditiveSynth synth)
    {
        _sampleRate = sampleRate;
        _synth = synth;
        _phases = new double[64]; // Support up to 64 partials
        _partialEnvelopes = new Envelope[64];
        _ampEnvelope = new Envelope(0.01, 0.1, 0.8, 0.3);

        for (int i = 0; i < 64; i++)
        {
            _partialEnvelopes[i] = new Envelope(0.01, 0.1, 0.8, 0.3);
        }
    }

    public void Trigger(int note, int velocity)
    {
        Note = note;
        Velocity = velocity;
        IsActive = true;
        TriggerTime = DateTime.Now;
        BaseFrequency = 440.0 * Math.Pow(2.0, (note - 69.0) / 12.0);

        // Reset phases
        for (int i = 0; i < _phases.Length; i++)
        {
            _phases[i] = 0;
        }

        // Copy and trigger envelopes
        _ampEnvelope.Attack = _synth.AmpEnvelope.Attack;
        _ampEnvelope.Decay = _synth.AmpEnvelope.Decay;
        _ampEnvelope.Sustain = _synth.AmpEnvelope.Sustain;
        _ampEnvelope.Release = _synth.AmpEnvelope.Release;
        _ampEnvelope.Trigger(velocity);

        // Trigger partial envelopes
        var partials = _synth.Partials;
        for (int i = 0; i < partials.Count && i < _partialEnvelopes.Length; i++)
        {
            var partial = partials[i];
            var env = _partialEnvelopes[i];

            if (partial.Envelope != null)
            {
                env.Attack = partial.Envelope.Attack;
                env.Decay = partial.Envelope.Decay;
                env.Sustain = partial.Envelope.Sustain;
                env.Release = partial.Envelope.Release;
            }
            else
            {
                env.Attack = _synth.AmpEnvelope.Attack;
                env.Decay = _synth.AmpEnvelope.Decay;
                env.Sustain = _synth.AmpEnvelope.Sustain;
                env.Release = _synth.AmpEnvelope.Release;
            }

            env.Trigger(velocity);
        }
    }

    public void Release()
    {
        _ampEnvelope.Release_Gate();
        foreach (var env in _partialEnvelopes)
        {
            env.Release_Gate();
        }
    }

    public float Process(double deltaTime)
    {
        if (!IsActive) return 0f;

        float ampEnvValue = (float)_ampEnvelope.Process(deltaTime);

        if (!_ampEnvelope.IsActive)
        {
            IsActive = false;
            return 0f;
        }

        // Apply pitch bend
        double pitchMod = _synth.PitchBend * _synth.PitchBendRange;
        double freq = BaseFrequency * Math.Pow(2.0, pitchMod / 12.0);

        // Sum all partials
        float output = 0f;
        var partials = _synth.Partials;

        for (int i = 0; i < partials.Count; i++)
        {
            var partial = partials[i];
            if (partial.Amplitude <= 0.001f) continue;

            // Calculate partial frequency
            double partialFreq = freq * partial.HarmonicNumber;
            partialFreq *= Math.Pow(2.0, partial.Detune / 1200.0);

            // Skip if above Nyquist
            if (partialFreq > _sampleRate / 2) continue;

            // Process partial envelope
            float partialEnv = i < _partialEnvelopes.Length
                ? (float)_partialEnvelopes[i].Process(deltaTime)
                : 1f;

            // Generate sine for this partial
            double phaseIncrement = partialFreq / _sampleRate;
            _phases[i] += phaseIncrement;
            if (_phases[i] >= 1.0) _phases[i] -= 1.0;

            float phase = (float)((_phases[i] + partial.Phase) * 2 * Math.PI);
            float sample = MathF.Sin(phase);

            output += sample * partial.Amplitude * partialEnv;
        }

        // Apply velocity and envelope
        float velocityGain = Velocity / 127f;
        output *= ampEnvValue * velocityGain;

        return output;
    }
}

/// <summary>
/// Additive synthesizer using harmonic series synthesis.
/// Each partial can have individual amplitude, phase, detune, and envelope.
/// Includes organ-style drawbar presets.
/// </summary>
public class AdditiveSynth : ISynth
{
    private readonly WaveFormat _waveFormat;
    private readonly List<AdditiveVoice> _voices = new();
    private readonly Dictionary<int, AdditiveVoice> _noteToVoice = new();
    private readonly object _lock = new();

    /// <summary>Synth name</summary>
    public string Name { get; set; } = "AdditiveSynth";

    /// <summary>Audio format</summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>Maximum polyphony</summary>
    public int MaxVoices { get; set; } = 16;

    /// <summary>Master volume (0-1)</summary>
    public float Volume { get; set; } = 0.5f;

    /// <summary>List of harmonic partials</summary>
    public List<HarmonicPartial> Partials { get; } = new();

    /// <summary>Amplitude envelope</summary>
    public Envelope AmpEnvelope { get; }

    /// <summary>Pitch bend range in semitones</summary>
    public float PitchBendRange { get; set; } = 2f;

    /// <summary>Current pitch bend (-1 to 1)</summary>
    public float PitchBend { get; set; } = 0f;

    /// <summary>Drawbar levels (Hammond style, 0-8 for each drawbar)</summary>
    public int[] Drawbars { get; } = new int[9];

    /// <summary>
    /// Creates a new AdditiveSynth
    /// </summary>
    public AdditiveSynth(int? sampleRate = null)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, 2);

        AmpEnvelope = new Envelope(0.01, 0.01, 1.0, 0.05);

        // Initialize with default 16 harmonics
        for (int i = 1; i <= 16; i++)
        {
            Partials.Add(new HarmonicPartial(i, i == 1 ? 1.0f : 0f));
        }

        // Initialize drawbars to 888000000 (full organ)
        Drawbars[0] = 8; // 16'
        Drawbars[1] = 8; // 5 1/3'
        Drawbars[2] = 8; // 8'
        Drawbars[3] = 0; // 4'
        Drawbars[4] = 0; // 2 2/3'
        Drawbars[5] = 0; // 2'
        Drawbars[6] = 0; // 1 3/5'
        Drawbars[7] = 0; // 1 1/3'
        Drawbars[8] = 0; // 1'
    }

    /// <summary>
    /// Set harmonic amplitude
    /// </summary>
    public void SetHarmonic(int harmonicNumber, float amplitude)
    {
        var partial = Partials.FirstOrDefault(p => p.HarmonicNumber == harmonicNumber);
        if (partial != null)
        {
            partial.Amplitude = Math.Clamp(amplitude, 0f, 1f);
        }
        else if (harmonicNumber > 0 && harmonicNumber <= 64)
        {
            Partials.Add(new HarmonicPartial(harmonicNumber, amplitude));
            Partials.Sort((a, b) => a.HarmonicNumber.CompareTo(b.HarmonicNumber));
        }
    }

    /// <summary>
    /// Set multiple harmonics at once (for presets)
    /// </summary>
    public void SetHarmonics(params (int harmonic, float amplitude)[] harmonics)
    {
        // Clear all amplitudes first
        foreach (var partial in Partials)
        {
            partial.Amplitude = 0f;
        }

        foreach (var (harmonic, amplitude) in harmonics)
        {
            SetHarmonic(harmonic, amplitude);
        }
    }

    /// <summary>
    /// Apply drawbar settings (Hammond-style organ)
    /// </summary>
    public void ApplyDrawbars()
    {
        // Hammond drawbar footage to harmonic mapping:
        // 16'  = 0.5 (sub-octave)
        // 5 1/3' = 1.5 (fifth)
        // 8'   = 1 (fundamental)
        // 4'   = 2 (octave)
        // 2 2/3' = 3 (fifth + octave)
        // 2'   = 4 (2 octaves)
        // 1 3/5' = 5 (major third + 2 octaves)
        // 1 1/3' = 6 (fifth + 2 octaves)
        // 1'   = 8 (3 octaves)

        float[] drawbarHarmonics = { 0.5f, 1.5f, 1f, 2f, 3f, 4f, 5f, 6f, 8f };

        // Clear existing
        foreach (var partial in Partials)
        {
            partial.Amplitude = 0f;
        }

        // Apply drawbar levels
        for (int i = 0; i < 9; i++)
        {
            if (Drawbars[i] > 0)
            {
                float harmonic = drawbarHarmonics[i];
                float amplitude = Drawbars[i] / 8f;

                // Find or create partial
                var partial = Partials.FirstOrDefault(p =>
                    Math.Abs(p.HarmonicNumber - harmonic) < 0.01f);

                if (partial != null)
                {
                    partial.Amplitude = amplitude;
                }
                else
                {
                    Partials.Add(new HarmonicPartial((int)Math.Round(harmonic), amplitude));
                }
            }
        }
    }

    /// <summary>
    /// Set drawbar value (0-8)
    /// </summary>
    public void SetDrawbar(int index, int value)
    {
        if (index >= 0 && index < 9)
        {
            Drawbars[index] = Math.Clamp(value, 0, 8);
            ApplyDrawbars();
        }
    }

    /// <summary>
    /// Set all drawbars at once (string like "888000000")
    /// </summary>
    public void SetDrawbars(string preset)
    {
        for (int i = 0; i < Math.Min(preset.Length, 9); i++)
        {
            if (char.IsDigit(preset[i]))
            {
                Drawbars[i] = preset[i] - '0';
            }
        }
        ApplyDrawbars();
    }

    private AdditiveVoice? GetFreeVoice()
    {
        lock (_lock)
        {
            foreach (var voice in _voices)
            {
                if (!voice.IsActive) return voice;
            }

            if (_voices.Count < MaxVoices)
            {
                var voice = new AdditiveVoice(_waveFormat.SampleRate, this);
                _voices.Add(voice);
                return voice;
            }

            // Voice stealing
            AdditiveVoice? oldest = null;
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
            case "pitchbend": PitchBend = Math.Clamp(value, -1f, 1f); break;
            case "pitchbendrange": PitchBendRange = Math.Clamp(value, 0f, 24f); break;

            case "attack": AmpEnvelope.Attack = value; break;
            case "decay": AmpEnvelope.Decay = value; break;
            case "sustain": AmpEnvelope.Sustain = value; break;
            case "release": AmpEnvelope.Release = value; break;

            default:
                // Check for harmonic parameters (h1, h2, etc.)
                if (name.StartsWith("h") && int.TryParse(name.Substring(1), out int harmonic))
                {
                    SetHarmonic(harmonic, value);
                }
                // Check for drawbar parameters (d0-d8)
                else if (name.StartsWith("d") && int.TryParse(name.Substring(1), out int drawbar))
                {
                    SetDrawbar(drawbar, (int)value);
                }
                break;
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        Array.Clear(buffer, offset, count);

        double deltaTime = 1.0 / _waveFormat.SampleRate;
        int channels = _waveFormat.Channels;

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

                // Apply volume and soft clipping
                sample *= Volume;
                sample = MathF.Tanh(sample);

                // Output to all channels
                for (int c = 0; c < channels; c++)
                {
                    buffer[offset + n + c] = sample;
                }
            }
        }

        return count;
    }

    #endregion

    #region Presets

    /// <summary>Classic Hammond organ preset</summary>
    public static AdditiveSynth CreateHammondOrgan()
    {
        var synth = new AdditiveSynth { Name = "Hammond Organ" };
        synth.SetDrawbars("888000000");
        synth.AmpEnvelope.Attack = 0.005;
        synth.AmpEnvelope.Decay = 0.0;
        synth.AmpEnvelope.Sustain = 1.0;
        synth.AmpEnvelope.Release = 0.05;
        return synth;
    }

    /// <summary>Full organ preset (all drawbars)</summary>
    public static AdditiveSynth CreateFullOrgan()
    {
        var synth = new AdditiveSynth { Name = "Full Organ" };
        synth.SetDrawbars("888888888");
        synth.AmpEnvelope.Attack = 0.005;
        synth.AmpEnvelope.Decay = 0.0;
        synth.AmpEnvelope.Sustain = 1.0;
        synth.AmpEnvelope.Release = 0.05;
        return synth;
    }

    /// <summary>Pipe organ preset (church organ)</summary>
    public static AdditiveSynth CreatePipeOrgan()
    {
        var synth = new AdditiveSynth { Name = "Pipe Organ" };
        synth.SetHarmonics(
            (1, 1.0f),   // Fundamental
            (2, 0.5f),   // Octave
            (3, 0.3f),   // Fifth
            (4, 0.25f),  // 2 octaves
            (5, 0.2f),
            (6, 0.15f),
            (8, 0.1f)
        );
        synth.AmpEnvelope.Attack = 0.1;
        synth.AmpEnvelope.Decay = 0.0;
        synth.AmpEnvelope.Sustain = 1.0;
        synth.AmpEnvelope.Release = 0.3;
        return synth;
    }

    /// <summary>Pure sine wave preset</summary>
    public static AdditiveSynth CreatePureSine()
    {
        var synth = new AdditiveSynth { Name = "Pure Sine" };
        synth.SetHarmonics((1, 1.0f));
        synth.AmpEnvelope.Attack = 0.01;
        synth.AmpEnvelope.Decay = 0.0;
        synth.AmpEnvelope.Sustain = 1.0;
        synth.AmpEnvelope.Release = 0.1;
        return synth;
    }

    /// <summary>Bell-like tone preset</summary>
    public static AdditiveSynth CreateBellTone()
    {
        var synth = new AdditiveSynth { Name = "Bell Tone" };
        synth.SetHarmonics(
            (1, 1.0f),
            (2, 0.8f),
            (3, 0.6f),
            (4, 0.5f),
            (5, 0.4f),
            (6, 0.3f),
            (7, 0.2f),
            (8, 0.15f),
            (9, 0.1f),
            (10, 0.08f)
        );
        synth.AmpEnvelope.Attack = 0.001;
        synth.AmpEnvelope.Decay = 2.0;
        synth.AmpEnvelope.Sustain = 0.0;
        synth.AmpEnvelope.Release = 1.0;
        return synth;
    }

    /// <summary>Sawtooth approximation (many harmonics)</summary>
    public static AdditiveSynth CreateSawtoothApprox()
    {
        var synth = new AdditiveSynth { Name = "Sawtooth Approx" };
        var harmonics = new List<(int, float)>();

        // Sawtooth: all harmonics at 1/n amplitude
        for (int i = 1; i <= 32; i++)
        {
            harmonics.Add((i, 1.0f / i));
        }

        synth.SetHarmonics(harmonics.ToArray());
        synth.AmpEnvelope.Attack = 0.01;
        synth.AmpEnvelope.Decay = 0.1;
        synth.AmpEnvelope.Sustain = 0.8;
        synth.AmpEnvelope.Release = 0.2;
        return synth;
    }

    /// <summary>Square wave approximation (odd harmonics)</summary>
    public static AdditiveSynth CreateSquareApprox()
    {
        var synth = new AdditiveSynth { Name = "Square Approx" };
        var harmonics = new List<(int, float)>();

        // Square: odd harmonics at 1/n amplitude
        for (int i = 1; i <= 31; i += 2)
        {
            harmonics.Add((i, 1.0f / i));
        }

        synth.SetHarmonics(harmonics.ToArray());
        synth.AmpEnvelope.Attack = 0.01;
        synth.AmpEnvelope.Decay = 0.1;
        synth.AmpEnvelope.Sustain = 0.8;
        synth.AmpEnvelope.Release = 0.2;
        return synth;
    }

    #endregion
}
