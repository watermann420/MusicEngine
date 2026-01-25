//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Formant-based speech/singing synthesizer with text-to-speech
// and real-time vowel morphing.

using NAudio.Wave;

namespace MusicEngine.Core;

/// <summary>
/// Vowel phonemes for formant synthesis
/// </summary>
public enum Vowel
{
    // Standard vowels
    A,      // as in "father"
    E,      // as in "bed"
    I,      // as in "feet"
    O,      // as in "go"
    U,      // as in "food"

    // Additional vowels
    Schwa,  // as in "about" (unstressed)
    Ae,     // as in "cat"
    Eh,     // as in "bet"
    Ih,     // as in "bit"
    Ah,     // as in "but"
    Uh,     // as in "book"

    // German umlauts
    Oe,     // o-umlaut as in "schon"
    Ue,     // u-umlaut as in "uber"
    Umlaut_A  // a-umlaut as in "Manner"
}

/// <summary>
/// Consonant types for speech synthesis
/// </summary>
public enum Consonant
{
    None,

    // Plosives (stops)
    P, B,   // Bilabial
    T, D,   // Alveolar
    K, G,   // Velar

    // Fricatives
    F, V,   // Labiodental
    S, Z,   // Alveolar
    Sh, Zh, // Postalveolar
    H,      // Glottal
    Th,     // Dental (unvoiced)
    Dh,     // Dental (voiced)

    // Nasals
    M,      // Bilabial
    N,      // Alveolar
    Ng,     // Velar

    // Approximants
    L,      // Lateral
    R,      // Alveolar/Retroflex
    W,      // Labial-velar
    Y       // Palatal
}

/// <summary>
/// Glottal waveform types for voice source
/// </summary>
public enum GlottalWaveform
{
    /// <summary>Simple pulse wave</summary>
    Pulse,
    /// <summary>Liljencrants-Fant model (natural-sounding)</summary>
    LFModel,
    /// <summary>Rosenberg model (classic speech synthesis)</summary>
    Rosenberg,
    /// <summary>Klatt model (formant synthesizer standard)</summary>
    Klatt
}

/// <summary>
/// Language for text-to-phoneme conversion
/// </summary>
public enum SpeechLanguage
{
    English,
    German
}

/// <summary>
/// Formant frequency set for a vowel
/// </summary>
public struct FormantSet
{
    public float F1, F2, F3, F4, F5;
    public float B1, B2, B3, B4, B5; // Bandwidths

    public FormantSet(float f1, float f2, float f3, float f4, float f5,
                      float b1 = 60, float b2 = 90, float b3 = 150, float b4 = 200, float b5 = 200)
    {
        F1 = f1; F2 = f2; F3 = f3; F4 = f4; F5 = f5;
        B1 = b1; B2 = b2; B3 = b3; B4 = b4; B5 = b5;
    }

    public static FormantSet Lerp(FormantSet a, FormantSet b, float t)
    {
        return new FormantSet(
            a.F1 + (b.F1 - a.F1) * t,
            a.F2 + (b.F2 - a.F2) * t,
            a.F3 + (b.F3 - a.F3) * t,
            a.F4 + (b.F4 - a.F4) * t,
            a.F5 + (b.F5 - a.F5) * t,
            a.B1 + (b.B1 - a.B1) * t,
            a.B2 + (b.B2 - a.B2) * t,
            a.B3 + (b.B3 - a.B3) * t,
            a.B4 + (b.B4 - a.B4) * t,
            a.B5 + (b.B5 - a.B5) * t
        );
    }
}

/// <summary>
/// Consonant articulation data
/// </summary>
public struct ConsonantData
{
    public bool IsVoiced;
    public bool IsPlosive;
    public bool IsFricative;
    public bool IsNasal;
    public float NoiseFrequency;    // Center frequency for noise
    public float NoiseBandwidth;    // Bandwidth for noise
    public float Duration;          // Typical duration in seconds
    public float Attack;            // Attack time
    public float Release;           // Release time

    public ConsonantData(bool voiced, bool plosive, bool fricative, bool nasal,
                         float noiseFreq = 4000, float noiseBw = 2000,
                         float duration = 0.08f, float attack = 0.01f, float release = 0.02f)
    {
        IsVoiced = voiced;
        IsPlosive = plosive;
        IsFricative = fricative;
        IsNasal = nasal;
        NoiseFrequency = noiseFreq;
        NoiseBandwidth = noiseBw;
        Duration = duration;
        Attack = attack;
        Release = release;
    }
}

/// <summary>
/// Static database of phoneme formant and consonant data
/// </summary>
public static class PhonemeDatabase
{
    /// <summary>
    /// Vowel formant frequencies (male voice, can be shifted for female/child)
    /// Based on Peterson & Barney (1952) and other acoustic phonetics research
    /// </summary>
    public static readonly Dictionary<Vowel, FormantSet> VowelFormants = new()
    {
        // Standard vowels
        [Vowel.A] = new FormantSet(730, 1090, 2440, 3400, 4500),    // "father"
        [Vowel.E] = new FormantSet(390, 2300, 2850, 3500, 4500),    // close-e "hey"
        [Vowel.I] = new FormantSet(270, 2290, 3010, 3600, 4500),    // "feet"
        [Vowel.O] = new FormantSet(360, 640, 2390, 3400, 4500),     // "go"
        [Vowel.U] = new FormantSet(300, 870, 2240, 3400, 4500),     // "food"

        // Additional vowels
        [Vowel.Schwa] = new FormantSet(500, 1500, 2500, 3500, 4500),// neutral
        [Vowel.Ae] = new FormantSet(660, 1720, 2410, 3400, 4500),   // "cat"
        [Vowel.Eh] = new FormantSet(530, 1840, 2480, 3500, 4500),   // "bet"
        [Vowel.Ih] = new FormantSet(390, 1990, 2550, 3500, 4500),   // "bit"
        [Vowel.Ah] = new FormantSet(640, 1190, 2390, 3400, 4500),   // "but"
        [Vowel.Uh] = new FormantSet(440, 1020, 2240, 3400, 4500),   // "book"

        // German umlauts
        [Vowel.Oe] = new FormantSet(370, 1350, 2400, 3400, 4500),   // o-umlaut
        [Vowel.Ue] = new FormantSet(270, 1800, 2200, 3400, 4500),   // u-umlaut
        [Vowel.Umlaut_A] = new FormantSet(350, 1750, 2500, 3500, 4500) // a-umlaut
    };

    /// <summary>
    /// Consonant articulation data
    /// </summary>
    public static readonly Dictionary<Consonant, ConsonantData> ConsonantInfo = new()
    {
        // Plosives
        [Consonant.P] = new ConsonantData(false, true, false, false, 500, 500, 0.01f, 0.001f, 0.01f),
        [Consonant.B] = new ConsonantData(true, true, false, false, 200, 300, 0.02f, 0.001f, 0.01f),
        [Consonant.T] = new ConsonantData(false, true, false, false, 4000, 1000, 0.01f, 0.001f, 0.01f),
        [Consonant.D] = new ConsonantData(true, true, false, false, 3000, 800, 0.02f, 0.001f, 0.01f),
        [Consonant.K] = new ConsonantData(false, true, false, false, 2000, 800, 0.02f, 0.001f, 0.02f),
        [Consonant.G] = new ConsonantData(true, true, false, false, 1500, 600, 0.03f, 0.001f, 0.02f),

        // Fricatives
        [Consonant.F] = new ConsonantData(false, false, true, false, 8000, 4000, 0.1f, 0.02f, 0.02f),
        [Consonant.V] = new ConsonantData(true, false, true, false, 6000, 3000, 0.1f, 0.02f, 0.02f),
        [Consonant.S] = new ConsonantData(false, false, true, false, 8000, 2000, 0.1f, 0.01f, 0.02f),
        [Consonant.Z] = new ConsonantData(true, false, true, false, 6000, 2000, 0.1f, 0.01f, 0.02f),
        [Consonant.Sh] = new ConsonantData(false, false, true, false, 4000, 2000, 0.12f, 0.02f, 0.03f),
        [Consonant.Zh] = new ConsonantData(true, false, true, false, 3500, 2000, 0.1f, 0.02f, 0.03f),
        [Consonant.H] = new ConsonantData(false, false, true, false, 2000, 4000, 0.08f, 0.01f, 0.02f),
        [Consonant.Th] = new ConsonantData(false, false, true, false, 6000, 4000, 0.1f, 0.02f, 0.02f),
        [Consonant.Dh] = new ConsonantData(true, false, true, false, 4000, 3000, 0.08f, 0.02f, 0.02f),

        // Nasals
        [Consonant.M] = new ConsonantData(true, false, false, true, 250, 100, 0.08f, 0.02f, 0.02f),
        [Consonant.N] = new ConsonantData(true, false, false, true, 280, 100, 0.08f, 0.02f, 0.02f),
        [Consonant.Ng] = new ConsonantData(true, false, false, true, 300, 100, 0.08f, 0.02f, 0.02f),

        // Approximants
        [Consonant.L] = new ConsonantData(true, false, false, false, 350, 200, 0.06f, 0.02f, 0.02f),
        [Consonant.R] = new ConsonantData(true, false, false, false, 1200, 400, 0.06f, 0.02f, 0.02f),
        [Consonant.W] = new ConsonantData(true, false, false, false, 300, 200, 0.05f, 0.03f, 0.03f),
        [Consonant.Y] = new ConsonantData(true, false, false, false, 2300, 400, 0.05f, 0.03f, 0.03f),

        [Consonant.None] = new ConsonantData(false, false, false, false, 0, 0, 0, 0, 0)
    };
}

/// <summary>
/// Formant filter using biquad bandpass
/// </summary>
internal class FormantFilter
{
    private float _z1, _z2;
    private float _a0, _a1, _a2, _b1, _b2;

    public float Frequency { get; private set; }
    public float Bandwidth { get; private set; }

    public void SetFormant(float frequency, float bandwidth, int sampleRate)
    {
        Frequency = frequency;
        Bandwidth = bandwidth;

        // Biquad bandpass coefficients
        float omega = 2f * MathF.PI * frequency / sampleRate;
        float sinOmega = MathF.Sin(omega);
        float cosOmega = MathF.Cos(omega);
        float alpha = sinOmega * MathF.Sinh(MathF.Log(2) / 2 * (bandwidth / frequency) * omega / sinOmega);

        float a0 = 1 + alpha;
        _a0 = alpha / a0;
        _a1 = 0;
        _a2 = -alpha / a0;
        _b1 = -2 * cosOmega / a0;
        _b2 = (1 - alpha) / a0;
    }

    public float Process(float input)
    {
        float output = _a0 * input + _a1 * _z1 + _a2 * _z2 - _b1 * _z1 - _b2 * _z2;
        _z2 = _z1;
        _z1 = output;
        return output;
    }

    public void Reset()
    {
        _z1 = _z2 = 0;
    }
}

/// <summary>
/// Bank of formant filters with morphing support
/// </summary>
internal class FormantFilterBank
{
    private readonly FormantFilter[] _filters = new FormantFilter[5];
    private readonly float[] _gains = new float[5] { 1f, 0.8f, 0.5f, 0.3f, 0.2f };

    private FormantSet _currentFormants;
    private FormantSet _targetFormants;
    private float _morphProgress = 1f;
    private float _morphSpeed = 0.01f;

    public FormantFilterBank()
    {
        for (int i = 0; i < 5; i++)
        {
            _filters[i] = new FormantFilter();
        }
        _currentFormants = PhonemeDatabase.VowelFormants[Vowel.Schwa];
        _targetFormants = _currentFormants;
    }

    public void SetVowel(Vowel vowel, int sampleRate)
    {
        if (PhonemeDatabase.VowelFormants.TryGetValue(vowel, out var formants))
        {
            _targetFormants = formants;
            _morphProgress = 0f;
        }
    }

    public void SetFormants(FormantSet formants)
    {
        _targetFormants = formants;
        _morphProgress = 0f;
    }

    public void SetMorphSpeed(float speed)
    {
        _morphSpeed = Math.Clamp(speed, 0.001f, 1f);
    }

    public float Process(float input, int sampleRate, float formantShift = 0f)
    {
        // Morph towards target
        if (_morphProgress < 1f)
        {
            _morphProgress = Math.Min(1f, _morphProgress + _morphSpeed);
            _currentFormants = FormantSet.Lerp(_currentFormants, _targetFormants, _morphProgress);
            UpdateFilters(sampleRate, formantShift);
        }

        // Sum all formant filter outputs
        float output = 0f;
        for (int i = 0; i < 5; i++)
        {
            output += _filters[i].Process(input) * _gains[i];
        }

        return output;
    }

    private void UpdateFilters(int sampleRate, float formantShift)
    {
        float shift = MathF.Pow(2f, formantShift / 12f);

        _filters[0].SetFormant(_currentFormants.F1 * shift, _currentFormants.B1, sampleRate);
        _filters[1].SetFormant(_currentFormants.F2 * shift, _currentFormants.B2, sampleRate);
        _filters[2].SetFormant(_currentFormants.F3 * shift, _currentFormants.B3, sampleRate);
        _filters[3].SetFormant(_currentFormants.F4 * shift, _currentFormants.B4, sampleRate);
        _filters[4].SetFormant(_currentFormants.F5 * shift, _currentFormants.B5, sampleRate);
    }

    public void Reset()
    {
        foreach (var filter in _filters)
        {
            filter.Reset();
        }
    }
}

/// <summary>
/// Glottal pulse oscillator for voice source
/// </summary>
internal class GlottalOscillator
{
    private double _phase;
    private float _lastOutput;
    private readonly Random _random = new();

    /// <summary>Fundamental frequency in Hz</summary>
    public float Frequency { get; set; } = 120f;

    /// <summary>Glottal pulse waveform type</summary>
    public GlottalWaveform Waveform { get; set; } = GlottalWaveform.LFModel;

    /// <summary>Glottal tension (0-1, higher = tighter)</summary>
    public float Tension { get; set; } = 0.5f;

    /// <summary>Open quotient (0.3-0.7, portion of cycle glottis is open)</summary>
    public float OpenQuotient { get; set; } = 0.55f;

    /// <summary>Breathiness (0-1, amount of aspiration noise)</summary>
    public float Breathiness { get; set; } = 0.1f;

    /// <summary>Jitter (pitch variation, 0-0.05)</summary>
    public float Jitter { get; set; } = 0.01f;

    /// <summary>Shimmer (amplitude variation, 0-0.1)</summary>
    public float Shimmer { get; set; } = 0.02f;

    public float Process(int sampleRate)
    {
        // Add jitter to frequency
        float freq = Frequency * (1f + (float)(_random.NextDouble() - 0.5) * 2 * Jitter);

        // Advance phase
        double phaseIncrement = freq / sampleRate;
        _phase += phaseIncrement;
        if (_phase >= 1.0) _phase -= 1.0;

        // Generate glottal pulse based on waveform type
        float pulse = Waveform switch
        {
            GlottalWaveform.Pulse => GeneratePulse(),
            GlottalWaveform.LFModel => GenerateLFModel(),
            GlottalWaveform.Rosenberg => GenerateRosenberg(),
            GlottalWaveform.Klatt => GenerateKlatt(),
            _ => GenerateLFModel()
        };

        // Add shimmer
        pulse *= 1f + (float)(_random.NextDouble() - 0.5) * 2 * Shimmer;

        // Add breathiness (aspiration noise)
        if (Breathiness > 0)
        {
            float noise = (float)(_random.NextDouble() * 2 - 1) * Breathiness;
            pulse = pulse * (1f - Breathiness) + noise;
        }

        _lastOutput = pulse;
        return pulse;
    }

    private float GeneratePulse()
    {
        float oq = OpenQuotient;
        if (_phase < oq)
        {
            // Opening phase - sine wave rise
            float t = (float)(_phase / oq);
            return MathF.Sin(t * MathF.PI);
        }
        else
        {
            // Closed phase
            return 0f;
        }
    }

    private float GenerateLFModel()
    {
        // Liljencrants-Fant model - more natural sounding
        float oq = OpenQuotient;
        float te = oq * 0.8f; // Point of maximum excitation
        float ta = oq * 0.2f; // Return phase

        if (_phase < te)
        {
            // Opening phase
            float t = (float)(_phase / te);
            float alpha = 1f + Tension * 2f;
            return MathF.Sin(MathF.PI * MathF.Pow(t, alpha));
        }
        else if (_phase < oq)
        {
            // Return phase
            float t = (float)((_phase - te) / ta);
            return MathF.Cos(t * MathF.PI * 0.5f);
        }
        else
        {
            // Closed phase
            return 0f;
        }
    }

    private float GenerateRosenberg()
    {
        // Rosenberg model - classic speech synthesis
        float oq = OpenQuotient;
        float tc = oq * 0.6f; // Closing time ratio

        if (_phase < tc)
        {
            // Opening phase - polynomial
            float t = (float)(_phase / tc);
            return 3f * t * t - 2f * t * t * t;
        }
        else if (_phase < oq)
        {
            // Closing phase
            float t = (float)((_phase - tc) / (oq - tc));
            return (1f - t) * (1f - t);
        }
        else
        {
            return 0f;
        }
    }

    private float GenerateKlatt()
    {
        // Klatt model - formant synthesizer standard
        float oq = OpenQuotient;
        float tp = oq * 0.4f; // Time to peak

        if (_phase < tp)
        {
            // Rise phase
            float t = (float)(_phase / tp);
            return t * t * (3f - 2f * t);
        }
        else if (_phase < oq)
        {
            // Fall phase
            float t = (float)((_phase - tp) / (oq - tp));
            float fallRate = 1f + Tension;
            return MathF.Exp(-t * fallRate * 2f);
        }
        else
        {
            return 0f;
        }
    }

    public void Reset()
    {
        _phase = 0;
        _lastOutput = 0;
    }
}

/// <summary>
/// Single voice for speech synthesis
/// </summary>
internal class SpeechVoice
{
    private readonly int _sampleRate;
    private readonly SpeechSynth _synth;
    private readonly GlottalOscillator _glottal;
    private readonly FormantFilterBank _formantBank;
    private readonly Envelope _ampEnvelope;
    private readonly Random _random = new();

    // Phoneme queue for TTS
    private readonly Queue<(Vowel? vowel, Consonant consonant, float duration)> _phonemeQueue = new();
    private float _phonemeTimeRemaining;
    private Consonant _currentConsonant = Consonant.None;
    private float _consonantTime;

    public int Note { get; private set; }
    public int Velocity { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime TriggerTime { get; private set; }

    // Vibrato
    private double _vibratoPhase;

    public SpeechVoice(int sampleRate, SpeechSynth synth)
    {
        _sampleRate = sampleRate;
        _synth = synth;
        _glottal = new GlottalOscillator();
        _formantBank = new FormantFilterBank();
        _ampEnvelope = new Envelope(0.02, 0.1, 0.9, 0.3);
    }

    public void Trigger(int note, int velocity, Vowel vowel)
    {
        Note = note;
        Velocity = velocity;
        IsActive = true;
        TriggerTime = DateTime.Now;

        // Set pitch from MIDI note
        _glottal.Frequency = 440f * MathF.Pow(2f, (note - 69) / 12f);

        // Set vowel
        _formantBank.SetVowel(vowel, _sampleRate);

        // Trigger envelope
        _ampEnvelope.Trigger(velocity);

        _vibratoPhase = 0;
    }

    public void SetVowel(Vowel vowel)
    {
        _formantBank.SetVowel(vowel, _sampleRate);
    }

    public void SetConsonant(Consonant consonant, float duration)
    {
        _currentConsonant = consonant;
        _consonantTime = duration;
    }

    public void QueuePhoneme(Vowel? vowel, Consonant consonant, float duration)
    {
        _phonemeQueue.Enqueue((vowel, consonant, duration));
    }

    public void ClearPhonemes()
    {
        _phonemeQueue.Clear();
        _phonemeTimeRemaining = 0;
        _currentConsonant = Consonant.None;
    }

    public void Release()
    {
        _ampEnvelope.Release_Gate();
    }

    public float Process(double deltaTime)
    {
        if (!IsActive) return 0;

        // Process envelope
        float envValue = (float)_ampEnvelope.Process(deltaTime);
        if (!_ampEnvelope.IsActive)
        {
            IsActive = false;
            return 0;
        }

        // Process phoneme queue
        ProcessPhonemeQueue((float)deltaTime);

        // Apply pitch bend and vibrato
        float pitchMod = _synth.PitchBend * _synth.PitchBendRange;

        // Vibrato
        if (_synth.VibratoDepth > 0)
        {
            _vibratoPhase += _synth.VibratoRate * deltaTime * 2 * Math.PI;
            pitchMod += MathF.Sin((float)_vibratoPhase) * _synth.VibratoDepth;
        }

        float freq = _glottal.Frequency * MathF.Pow(2f, pitchMod / 12f);
        _glottal.Frequency = freq;

        // Apply synth parameters to glottal
        _glottal.Waveform = _synth.GlottalWaveform;
        _glottal.Tension = _synth.Tension;
        _glottal.OpenQuotient = _synth.OpenQuotient;
        _glottal.Breathiness = _synth.Breathiness;
        _glottal.Jitter = _synth.Jitter;
        _glottal.Shimmer = _synth.Shimmer;

        // Generate glottal pulse
        float glottalOutput = _glottal.Process(_sampleRate);

        // Generate consonant noise if needed
        float consonantOutput = GenerateConsonant((float)deltaTime);

        // Mix glottal and consonant
        float source = glottalOutput;
        if (_currentConsonant != Consonant.None)
        {
            var data = PhonemeDatabase.ConsonantInfo[_currentConsonant];
            if (data.IsFricative || data.IsPlosive)
            {
                source = source * 0.3f + consonantOutput * 0.7f;
            }
            else if (data.IsNasal)
            {
                source *= 0.7f; // Reduce amplitude for nasals
            }
        }

        // Apply formant filtering
        float output = _formantBank.Process(source, _sampleRate, _synth.FormantShift);

        // Apply velocity and envelope
        float velocityGain = Velocity / 127f;
        output *= envValue * velocityGain * _synth.Volume;

        return output;
    }

    private void ProcessPhonemeQueue(float deltaTime)
    {
        // Process current phoneme duration
        if (_phonemeTimeRemaining > 0)
        {
            _phonemeTimeRemaining -= deltaTime;
        }

        // Process consonant duration
        if (_consonantTime > 0)
        {
            _consonantTime -= deltaTime;
            if (_consonantTime <= 0)
            {
                _currentConsonant = Consonant.None;
            }
        }

        // Get next phoneme if needed
        if (_phonemeTimeRemaining <= 0 && _phonemeQueue.Count > 0)
        {
            var (vowel, consonant, duration) = _phonemeQueue.Dequeue();

            if (vowel.HasValue)
            {
                _formantBank.SetVowel(vowel.Value, _sampleRate);
            }

            if (consonant != Consonant.None)
            {
                _currentConsonant = consonant;
                var data = PhonemeDatabase.ConsonantInfo[consonant];
                _consonantTime = data.Duration;
            }

            _phonemeTimeRemaining = duration;
        }
    }

    private float GenerateConsonant(float deltaTime)
    {
        if (_currentConsonant == Consonant.None) return 0;

        var data = PhonemeDatabase.ConsonantInfo[_currentConsonant];

        // Generate filtered noise for fricatives
        if (data.IsFricative || data.IsPlosive)
        {
            // White noise
            float noise = (float)(_random.NextDouble() * 2 - 1);

            // For plosives, add transient
            if (data.IsPlosive && _consonantTime > data.Duration - data.Attack)
            {
                float burst = (data.Duration - _consonantTime) / data.Attack;
                noise *= burst * burst * 3f;
            }

            // Simple noise shaping (bandpass approximation)
            float shaped = noise * 0.5f;
            return shaped;
        }

        return 0;
    }

    public void Reset()
    {
        _glottal.Reset();
        _formantBank.Reset();
        _ampEnvelope.Reset();
        ClearPhonemes();
        IsActive = false;
    }
}

/// <summary>
/// Text-to-phoneme converter
/// </summary>
public class TextToPhoneme
{
    private static readonly Dictionary<string, (Vowel? vowel, Consonant consonant)> EnglishRules = new()
    {
        // Vowels
        ["a"] = (Vowel.Ae, Consonant.None),
        ["e"] = (Vowel.Eh, Consonant.None),
        ["i"] = (Vowel.Ih, Consonant.None),
        ["o"] = (Vowel.O, Consonant.None),
        ["u"] = (Vowel.Ah, Consonant.None),
        ["ee"] = (Vowel.I, Consonant.None),
        ["ea"] = (Vowel.I, Consonant.None),
        ["oo"] = (Vowel.U, Consonant.None),
        ["ou"] = (Vowel.Uh, Consonant.None),
        ["ow"] = (Vowel.O, Consonant.None),
        ["ai"] = (Vowel.E, Consonant.None),
        ["ay"] = (Vowel.E, Consonant.None),
        ["oi"] = (Vowel.O, Consonant.None),
        ["er"] = (Vowel.Schwa, Consonant.None),
        ["ir"] = (Vowel.Schwa, Consonant.None),
        ["ur"] = (Vowel.Schwa, Consonant.None),

        // Consonants
        ["b"] = (null, Consonant.B),
        ["c"] = (null, Consonant.K),
        ["d"] = (null, Consonant.D),
        ["f"] = (null, Consonant.F),
        ["g"] = (null, Consonant.G),
        ["h"] = (null, Consonant.H),
        ["j"] = (null, Consonant.Zh),
        ["k"] = (null, Consonant.K),
        ["l"] = (null, Consonant.L),
        ["m"] = (null, Consonant.M),
        ["n"] = (null, Consonant.N),
        ["p"] = (null, Consonant.P),
        ["r"] = (null, Consonant.R),
        ["s"] = (null, Consonant.S),
        ["t"] = (null, Consonant.T),
        ["v"] = (null, Consonant.V),
        ["w"] = (null, Consonant.W),
        ["x"] = (null, Consonant.K),
        ["y"] = (null, Consonant.Y),
        ["z"] = (null, Consonant.Z),

        // Digraphs
        ["ch"] = (null, Consonant.Sh),
        ["sh"] = (null, Consonant.Sh),
        ["th"] = (null, Consonant.Th),
        ["ng"] = (null, Consonant.Ng),
        ["ph"] = (null, Consonant.F),
        ["wh"] = (null, Consonant.W),
        ["ck"] = (null, Consonant.K),
        ["qu"] = (null, Consonant.K)
    };

    private static readonly Dictionary<string, (Vowel? vowel, Consonant consonant)> GermanRules = new()
    {
        // Vowels
        ["a"] = (Vowel.A, Consonant.None),
        ["e"] = (Vowel.E, Consonant.None),
        ["i"] = (Vowel.I, Consonant.None),
        ["o"] = (Vowel.O, Consonant.None),
        ["u"] = (Vowel.U, Consonant.None),
        ["a:"] = (Vowel.A, Consonant.None),     // Long vowels marked with :
        ["e:"] = (Vowel.E, Consonant.None),
        ["i:"] = (Vowel.I, Consonant.None),
        ["o:"] = (Vowel.O, Consonant.None),
        ["u:"] = (Vowel.U, Consonant.None),
        ["o\u0308"] = (Vowel.Oe, Consonant.None), // o-umlaut
        ["u\u0308"] = (Vowel.Ue, Consonant.None), // u-umlaut
        ["a\u0308"] = (Vowel.Umlaut_A, Consonant.None), // a-umlaut
        ["au"] = (Vowel.O, Consonant.None),
        ["ei"] = (Vowel.E, Consonant.None),
        ["eu"] = (Vowel.Oe, Consonant.None),
        ["ie"] = (Vowel.I, Consonant.None),

        // Consonants
        ["b"] = (null, Consonant.B),
        ["c"] = (null, Consonant.K),
        ["d"] = (null, Consonant.D),
        ["f"] = (null, Consonant.F),
        ["g"] = (null, Consonant.G),
        ["h"] = (null, Consonant.H),
        ["j"] = (null, Consonant.Y),
        ["k"] = (null, Consonant.K),
        ["l"] = (null, Consonant.L),
        ["m"] = (null, Consonant.M),
        ["n"] = (null, Consonant.N),
        ["p"] = (null, Consonant.P),
        ["r"] = (null, Consonant.R),
        ["s"] = (null, Consonant.Z),
        ["t"] = (null, Consonant.T),
        ["v"] = (null, Consonant.F),
        ["w"] = (null, Consonant.V),
        ["z"] = (null, Consonant.T),

        // German digraphs
        ["ch"] = (null, Consonant.Sh),
        ["sch"] = (null, Consonant.Sh),
        ["sp"] = (null, Consonant.Sh),  // At word start
        ["st"] = (null, Consonant.Sh),  // At word start
        ["ss"] = (null, Consonant.S),
        ["\u00df"] = (null, Consonant.S),  // Eszett
        ["pf"] = (null, Consonant.P),
        ["tz"] = (null, Consonant.T),
        ["ng"] = (null, Consonant.Ng)
    };

    /// <summary>
    /// Convert text to phoneme sequence
    /// </summary>
    public static List<(Vowel? vowel, Consonant consonant, float duration)> Convert(string text, SpeechLanguage language)
    {
        var rules = language == SpeechLanguage.German ? GermanRules : EnglishRules;
        var result = new List<(Vowel? vowel, Consonant consonant, float duration)>();

        text = text.ToLowerInvariant();
        int i = 0;

        while (i < text.Length)
        {
            bool matched = false;

            // Try matching longer sequences first
            for (int len = 3; len >= 1 && !matched; len--)
            {
                if (i + len <= text.Length)
                {
                    string sub = text.Substring(i, len);
                    if (rules.TryGetValue(sub, out var phoneme))
                    {
                        float duration = phoneme.vowel.HasValue ? 0.15f : 0.08f;
                        result.Add((phoneme.vowel, phoneme.consonant, duration));
                        i += len;
                        matched = true;
                    }
                }
            }

            if (!matched)
            {
                // Skip spaces and punctuation
                if (char.IsWhiteSpace(text[i]) || char.IsPunctuation(text[i]))
                {
                    result.Add((null, Consonant.None, 0.1f)); // Pause
                }
                i++;
            }
        }

        return result;
    }
}

/// <summary>
/// Maps lyrics syllables to MIDI notes for singing mode
/// </summary>
public class LyricsSyllableMapper
{
    private readonly Dictionary<int, string> _noteToSyllable = new();

    /// <summary>
    /// Parse lyrics format: "60:hel 62:lo 64:world"
    /// </summary>
    public void ParseLyrics(string lyrics)
    {
        _noteToSyllable.Clear();

        var parts = lyrics.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var split = part.Split(':');
            if (split.Length == 2 && int.TryParse(split[0], out int note))
            {
                _noteToSyllable[note] = split[1];
            }
        }
    }

    /// <summary>
    /// Get syllable for a MIDI note
    /// </summary>
    public string? GetSyllable(int note)
    {
        return _noteToSyllable.TryGetValue(note, out var syllable) ? syllable : null;
    }

    /// <summary>
    /// Clear all mappings
    /// </summary>
    public void Clear()
    {
        _noteToSyllable.Clear();
    }
}

/// <summary>
/// Formant-based speech and singing synthesizer with text-to-speech support
/// </summary>
public class SpeechSynth : ISynth
{
    private readonly WaveFormat _waveFormat;
    private readonly List<SpeechVoice> _voices = new();
    private readonly Dictionary<int, SpeechVoice> _noteToVoice = new();
    private readonly object _lock = new();

    /// <summary>Lyrics mapper for singing mode</summary>
    public LyricsSyllableMapper LyricsMapper { get; } = new();

    /// <summary>Synth name</summary>
    public string Name { get; set; } = "SpeechSynth";

    /// <summary>Audio format</summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>Maximum polyphony</summary>
    public int MaxVoices { get; set; } = 8;

    /// <summary>Master volume (0-1)</summary>
    public float Volume { get; set; } = 0.8f;

    // Glottal source parameters
    /// <summary>Glottal waveform type</summary>
    public GlottalWaveform GlottalWaveform { get; set; } = GlottalWaveform.LFModel;

    /// <summary>Glottal tension (0-1)</summary>
    public float Tension { get; set; } = 0.5f;

    /// <summary>Open quotient (0.3-0.7)</summary>
    public float OpenQuotient { get; set; } = 0.55f;

    /// <summary>Breathiness (0-1)</summary>
    public float Breathiness { get; set; } = 0.1f;

    /// <summary>Jitter for natural variation (0-0.05)</summary>
    public float Jitter { get; set; } = 0.01f;

    /// <summary>Shimmer for natural variation (0-0.1)</summary>
    public float Shimmer { get; set; } = 0.02f;

    // Formant parameters
    /// <summary>Current vowel</summary>
    public Vowel CurrentVowel { get; set; } = Vowel.A;

    /// <summary>Formant shift in semitones (-12 to 12)</summary>
    public float FormantShift { get; set; } = 0f;

    /// <summary>Vowel morph speed (0.001-1)</summary>
    public float MorphSpeed { get; set; } = 0.05f;

    // Vibrato
    /// <summary>Vibrato depth in semitones</summary>
    public float VibratoDepth { get; set; } = 0.3f;

    /// <summary>Vibrato rate in Hz</summary>
    public float VibratoRate { get; set; } = 5.5f;

    // MIDI
    /// <summary>Pitch bend range in semitones</summary>
    public float PitchBendRange { get; set; } = 2f;

    /// <summary>Current pitch bend (-1 to 1)</summary>
    public float PitchBend { get; set; } = 0f;

    // TTS
    /// <summary>Language for text-to-speech</summary>
    public SpeechLanguage Language { get; set; } = SpeechLanguage.English;

    /// <summary>Enable singing mode (syllables mapped to notes)</summary>
    public bool SingingMode { get; set; } = false;

    /// <summary>
    /// Creates a new SpeechSynth
    /// </summary>
    public SpeechSynth(int? sampleRate = null)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);
    }

    /// <summary>
    /// Set the current vowel with optional morphing
    /// </summary>
    public void SetVowel(Vowel vowel)
    {
        CurrentVowel = vowel;
        lock (_lock)
        {
            foreach (var voice in _voices.Where(v => v.IsActive))
            {
                voice.SetVowel(vowel);
            }
        }
    }

    /// <summary>
    /// Speak text using TTS
    /// </summary>
    public void Speak(string text, int note = 60, int velocity = 100)
    {
        var phonemes = TextToPhoneme.Convert(text, Language);

        lock (_lock)
        {
            var voice = GetOrCreateVoice();
            if (voice != null)
            {
                // Start with first vowel or default
                var firstVowel = phonemes.FirstOrDefault(p => p.vowel.HasValue);
                voice.Trigger(note, velocity, firstVowel.vowel ?? Vowel.Schwa);

                // Queue remaining phonemes
                foreach (var phoneme in phonemes)
                {
                    voice.QueuePhoneme(phoneme.vowel, phoneme.consonant, phoneme.duration);
                }

                _noteToVoice[note] = voice;
            }
        }
    }

    private SpeechVoice? GetOrCreateVoice()
    {
        // Find inactive voice
        foreach (var voice in _voices)
        {
            if (!voice.IsActive) return voice;
        }

        // Create new if under limit
        if (_voices.Count < MaxVoices)
        {
            var voice = new SpeechVoice(_waveFormat.SampleRate, this);
            _voices.Add(voice);
            return voice;
        }

        // Steal oldest
        SpeechVoice? oldest = null;
        DateTime oldestTime = DateTime.MaxValue;
        foreach (var voice in _voices)
        {
            if (voice.TriggerTime < oldestTime)
            {
                oldestTime = voice.TriggerTime;
                oldest = voice;
            }
        }

        return oldest;
    }

    #region ISynth Implementation

    public void NoteOn(int note, int velocity)
    {
        lock (_lock)
        {
            if (_noteToVoice.TryGetValue(note, out var existingVoice))
            {
                existingVoice.Trigger(note, velocity, CurrentVowel);
                return;
            }

            var voice = GetOrCreateVoice();
            if (voice == null) return;

            // In singing mode, check for lyrics
            Vowel vowel = CurrentVowel;
            if (SingingMode)
            {
                string? syllable = LyricsMapper.GetSyllable(note);
                if (!string.IsNullOrEmpty(syllable))
                {
                    var phonemes = TextToPhoneme.Convert(syllable, Language);
                    var firstVowel = phonemes.FirstOrDefault(p => p.vowel.HasValue);
                    if (firstVowel.vowel.HasValue)
                    {
                        vowel = firstVowel.vowel.Value;
                    }

                    // Queue phonemes for syllable
                    foreach (var phoneme in phonemes)
                    {
                        voice.QueuePhoneme(phoneme.vowel, phoneme.consonant, phoneme.duration);
                    }
                }
            }

            voice.Trigger(note, velocity, vowel);
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
                voice.ClearPhonemes();
            }
            _noteToVoice.Clear();
        }
    }

    public void SetParameter(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "volume":
                Volume = Math.Clamp(value, 0f, 2f);
                break;
            case "pitchbend":
                PitchBend = Math.Clamp(value, -1f, 1f);
                break;
            case "pitchbendrange":
                PitchBendRange = Math.Clamp(value, 0f, 24f);
                break;

            // Glottal parameters
            case "tension":
                Tension = Math.Clamp(value, 0f, 1f);
                break;
            case "openquotient":
                OpenQuotient = Math.Clamp(value, 0.3f, 0.7f);
                break;
            case "breathiness":
                Breathiness = Math.Clamp(value, 0f, 1f);
                break;
            case "jitter":
                Jitter = Math.Clamp(value, 0f, 0.05f);
                break;
            case "shimmer":
                Shimmer = Math.Clamp(value, 0f, 0.1f);
                break;

            // Formant
            case "formantshift":
                FormantShift = Math.Clamp(value, -12f, 12f);
                break;
            case "morphspeed":
                MorphSpeed = Math.Clamp(value, 0.001f, 1f);
                break;

            // Vibrato
            case "vibratodepth":
                VibratoDepth = Math.Clamp(value, 0f, 2f);
                break;
            case "vibratorate":
                VibratoRate = Math.Clamp(value, 0.1f, 15f);
                break;

            // Vowel selection via CC
            case "vowel":
                int vowelIndex = (int)Math.Clamp(value * 14, 0, 13);
                CurrentVowel = (Vowel)vowelIndex;
                SetVowel(CurrentVowel);
                break;

            // Mod wheel -> vibrato
            case "modwheel":
                VibratoDepth = value * 0.5f;
                break;

            // Breath controller -> breathiness
            case "breath":
                Breathiness = value;
                break;

            // Brightness (CC74) -> formant shift
            case "brightness":
                FormantShift = (value - 0.5f) * 12f;
                break;

            // Resonance (CC71) -> tension
            case "resonance":
                Tension = value;
                break;
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        // Clear buffer
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
                    if (voice.IsActive)
                    {
                        sample += voice.Process(deltaTime);
                    }
                }

                // Soft clipping
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

    /// <summary>
    /// Create a choir preset
    /// </summary>
    public static SpeechSynth CreateChoirPreset()
    {
        return new SpeechSynth
        {
            Name = "Choir",
            GlottalWaveform = GlottalWaveform.LFModel,
            Tension = 0.4f,
            OpenQuotient = 0.6f,
            Breathiness = 0.15f,
            VibratoDepth = 0.25f,
            VibratoRate = 5.5f,
            Jitter = 0.015f,
            Shimmer = 0.025f,
            CurrentVowel = Vowel.A
        };
    }

    /// <summary>
    /// Create a robot voice preset
    /// </summary>
    public static SpeechSynth CreateRobotPreset()
    {
        return new SpeechSynth
        {
            Name = "Robot",
            GlottalWaveform = GlottalWaveform.Pulse,
            Tension = 0.9f,
            OpenQuotient = 0.5f,
            Breathiness = 0f,
            VibratoDepth = 0f,
            VibratoRate = 0f,
            Jitter = 0f,
            Shimmer = 0f,
            FormantShift = -2f,
            CurrentVowel = Vowel.O
        };
    }

    /// <summary>
    /// Create a whisper preset
    /// </summary>
    public static SpeechSynth CreateWhisperPreset()
    {
        return new SpeechSynth
        {
            Name = "Whisper",
            GlottalWaveform = GlottalWaveform.Klatt,
            Tension = 0.2f,
            OpenQuotient = 0.4f,
            Breathiness = 0.8f,
            VibratoDepth = 0f,
            VibratoRate = 0f,
            Jitter = 0.03f,
            Shimmer = 0.05f,
            Volume = 0.5f,
            CurrentVowel = Vowel.Schwa
        };
    }

    /// <summary>
    /// Create an opera singer preset
    /// </summary>
    public static SpeechSynth CreateOperaPreset()
    {
        return new SpeechSynth
        {
            Name = "Opera",
            GlottalWaveform = GlottalWaveform.LFModel,
            Tension = 0.7f,
            OpenQuotient = 0.65f,
            Breathiness = 0.05f,
            VibratoDepth = 0.4f,
            VibratoRate = 6f,
            Jitter = 0.005f,
            Shimmer = 0.01f,
            FormantShift = 2f,
            CurrentVowel = Vowel.A
        };
    }

    /// <summary>
    /// Create a bass voice preset
    /// </summary>
    public static SpeechSynth CreateBassVoicePreset()
    {
        return new SpeechSynth
        {
            Name = "Bass Voice",
            GlottalWaveform = GlottalWaveform.Rosenberg,
            Tension = 0.3f,
            OpenQuotient = 0.5f,
            Breathiness = 0.1f,
            VibratoDepth = 0.2f,
            VibratoRate = 5f,
            FormantShift = -6f,
            CurrentVowel = Vowel.O
        };
    }

    /// <summary>
    /// Create a child voice preset
    /// </summary>
    public static SpeechSynth CreateChildVoicePreset()
    {
        return new SpeechSynth
        {
            Name = "Child",
            GlottalWaveform = GlottalWaveform.LFModel,
            Tension = 0.6f,
            OpenQuotient = 0.55f,
            Breathiness = 0.2f,
            VibratoDepth = 0.15f,
            VibratoRate = 6f,
            FormantShift = 8f,
            CurrentVowel = Vowel.I
        };
    }

    #endregion
}
