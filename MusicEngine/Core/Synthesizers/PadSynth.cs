//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: PadSynth synthesizer implementing Paul Nasca's algorithm for smooth evolving pads.

using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace MusicEngine.Core.Synthesizers;

/// <summary>
/// Harmonic profile type for PadSynth.
/// </summary>
public enum PadHarmonicProfile
{
    /// <summary>
    /// Simple sawtooth harmonic series (1/n).
    /// </summary>
    Saw,

    /// <summary>
    /// Square wave harmonics (odd only, 1/n).
    /// </summary>
    Square,

    /// <summary>
    /// Triangle wave harmonics (odd only, 1/n^2).
    /// </summary>
    Triangle,

    /// <summary>
    /// Custom user-defined harmonics.
    /// </summary>
    Custom,

    /// <summary>
    /// Vocal-like formant profile.
    /// </summary>
    Vocal,

    /// <summary>
    /// Organ-like profile with specific harmonic emphasis.
    /// </summary>
    Organ,

    /// <summary>
    /// String-like profile with bright harmonics.
    /// </summary>
    Strings,

    /// <summary>
    /// Soft pad profile with rolled-off harmonics.
    /// </summary>
    SoftPad
}

/// <summary>
/// PadSynth synthesizer implementing Paul Nasca's algorithm.
/// Generates smooth, evolving pad sounds using wavetable synthesis with bandwidth-spread harmonics.
/// </summary>
/// <remarks>
/// The PadSynth algorithm creates a wavetable by:
/// 1. Defining a harmonic profile (amplitudes for each harmonic)
/// 2. Spreading each harmonic over a frequency bandwidth using a bell curve
/// 3. Assigning random phases to each frequency component
/// 4. Performing inverse FFT to generate the wavetable
/// The result is a seamless, evolving sound without audible loops.
/// </remarks>
public class PadSynth : ISynth
{
    private readonly WaveFormat _waveFormat;
    private readonly List<PadVoice> _voices = new();
    private readonly Dictionary<int, PadVoice> _noteToVoice = new();
    private readonly object _lock = new();

    // Wavetable
    private float[] _wavetable = Array.Empty<float>();
    private int _wavetableSize = 262144; // 2^18 samples for smooth sound
    private float _fundamentalFreq = 261.63f; // C4 by default

    // Harmonic profile
    private float[] _harmonicAmplitudes = new float[128];
    private PadHarmonicProfile _profile = PadHarmonicProfile.Saw;

    // Bandwidth
    private float _bandwidth = 50f; // cents
    private float _bandwidthScale = 1f; // How bandwidth increases with frequency

    // Randomization
    private int _seed = 42;
    private Random _random;

    /// <summary>
    /// Synth name for identification.
    /// </summary>
    public string Name { get; set; } = "PadSynth";

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
    /// Bandwidth in cents for harmonic spreading.
    /// Higher values create thicker, more chorus-like sounds.
    /// </summary>
    public float Bandwidth
    {
        get => _bandwidth;
        set
        {
            _bandwidth = Math.Clamp(value, 1f, 200f);
        }
    }

    /// <summary>
    /// Bandwidth scale - how much bandwidth increases with harmonic number.
    /// 1.0 = linear increase, 0.5 = slower increase, 2.0 = faster increase.
    /// </summary>
    public float BandwidthScale
    {
        get => _bandwidthScale;
        set
        {
            _bandwidthScale = Math.Clamp(value, 0.1f, 3f);
        }
    }

    /// <summary>
    /// Harmonic profile type.
    /// </summary>
    public PadHarmonicProfile Profile
    {
        get => _profile;
        set
        {
            _profile = value;
            if (_profile != PadHarmonicProfile.Custom)
            {
                GenerateHarmonicProfile(_profile);
            }
        }
    }

    /// <summary>
    /// Random seed for phase generation. Different seeds produce different variations.
    /// </summary>
    public int Seed
    {
        get => _seed;
        set
        {
            _seed = value;
            _random = new Random(_seed);
        }
    }

    /// <summary>
    /// Detune amount in cents.
    /// </summary>
    public float Detune { get; set; } = 0f;

    /// <summary>
    /// Number of unison voices.
    /// </summary>
    public int UnisonVoices { get; set; } = 1;

    /// <summary>
    /// Unison detune spread in cents.
    /// </summary>
    public float UnisonDetune { get; set; } = 10f;

    /// <summary>
    /// Unison stereo spread (0-1).
    /// </summary>
    public float UnisonSpread { get; set; } = 0.5f;

    /// <summary>
    /// Amplitude envelope.
    /// </summary>
    public Envelope AmpEnvelope { get; }

    /// <summary>
    /// Creates a new PadSynth with default settings.
    /// </summary>
    public PadSynth(int? sampleRate = null)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);
        _random = new Random(_seed);

        AmpEnvelope = new Envelope(0.5, 0.5, 0.8, 1.0);

        GenerateHarmonicProfile(PadHarmonicProfile.Saw);
        GenerateWavetable();
    }

    /// <summary>
    /// Sets the amplitude of a specific harmonic (1-based index).
    /// </summary>
    public void SetHarmonic(int harmonic, float amplitude)
    {
        if (harmonic < 1 || harmonic > _harmonicAmplitudes.Length)
            return;

        _harmonicAmplitudes[harmonic - 1] = Math.Clamp(amplitude, 0f, 1f);
        _profile = PadHarmonicProfile.Custom;
    }

    /// <summary>
    /// Gets the amplitude of a specific harmonic (1-based index).
    /// </summary>
    public float GetHarmonic(int harmonic)
    {
        if (harmonic < 1 || harmonic > _harmonicAmplitudes.Length)
            return 0f;
        return _harmonicAmplitudes[harmonic - 1];
    }

    /// <summary>
    /// Sets all harmonics at once.
    /// </summary>
    public void SetHarmonics(float[] amplitudes)
    {
        int count = Math.Min(amplitudes.Length, _harmonicAmplitudes.Length);
        for (int i = 0; i < count; i++)
        {
            _harmonicAmplitudes[i] = Math.Clamp(amplitudes[i], 0f, 1f);
        }
        _profile = PadHarmonicProfile.Custom;
    }

    /// <summary>
    /// Gets a copy of all harmonic amplitudes.
    /// </summary>
    public float[] GetHarmonics()
    {
        return (float[])_harmonicAmplitudes.Clone();
    }

    /// <summary>
    /// Generates the wavetable based on current settings.
    /// Call this after changing harmonics or bandwidth.
    /// </summary>
    public void GenerateWavetable()
    {
        _random = new Random(_seed);

        // Allocate frequency domain arrays
        var freqReal = new double[_wavetableSize / 2 + 1];
        var freqImag = new double[_wavetableSize / 2 + 1];

        // Calculate the fundamental frequency for the wavetable
        double fundamentalBin = _fundamentalFreq * _wavetableSize / _waveFormat.SampleRate;

        // Process each harmonic
        for (int h = 1; h <= _harmonicAmplitudes.Length; h++)
        {
            float amp = _harmonicAmplitudes[h - 1];
            if (amp < 0.0001f) continue;

            // Calculate center bin for this harmonic
            double harmonicBin = fundamentalBin * h;

            // Calculate bandwidth for this harmonic
            double bw = CalculateBandwidth(h);
            int bwBins = Math.Max(1, (int)(bw * _wavetableSize / _waveFormat.SampleRate));

            // Spread the harmonic energy using a Gaussian profile
            for (int bin = Math.Max(1, (int)(harmonicBin - bwBins * 3));
                 bin <= Math.Min(_wavetableSize / 2, (int)(harmonicBin + bwBins * 3));
                 bin++)
            {
                double distance = (bin - harmonicBin) / bwBins;
                double profile = Math.Exp(-distance * distance / 2.0);

                // Random phase
                double phase = _random.NextDouble() * 2.0 * Math.PI;

                // Add contribution
                double magnitude = amp * profile;
                freqReal[bin] += magnitude * Math.Cos(phase);
                freqImag[bin] += magnitude * Math.Sin(phase);
            }
        }

        // Convert to polar and apply normalization
        double maxMag = 0;
        for (int i = 0; i < freqReal.Length; i++)
        {
            double mag = Math.Sqrt(freqReal[i] * freqReal[i] + freqImag[i] * freqImag[i]);
            if (mag > maxMag) maxMag = mag;
        }

        if (maxMag > 0)
        {
            double normFactor = 1.0 / maxMag;
            for (int i = 0; i < freqReal.Length; i++)
            {
                freqReal[i] *= normFactor;
                freqImag[i] *= normFactor;
            }
        }

        // Perform inverse FFT
        _wavetable = InverseFFT(freqReal, freqImag);

        // Normalize wavetable
        float maxSample = 0;
        for (int i = 0; i < _wavetable.Length; i++)
        {
            float abs = MathF.Abs(_wavetable[i]);
            if (abs > maxSample) maxSample = abs;
        }

        if (maxSample > 0)
        {
            float norm = 0.95f / maxSample;
            for (int i = 0; i < _wavetable.Length; i++)
            {
                _wavetable[i] *= norm;
            }
        }
    }

    private double CalculateBandwidth(int harmonic)
    {
        // Bandwidth formula based on harmonic number
        // bw(n) = bandwidth_cents * pow(n, bandwidthScale) * 2^(bandwidth/1200)
        double bwCents = _bandwidth * MathF.Pow(harmonic, _bandwidthScale);
        double bwHz = _fundamentalFreq * harmonic * (Math.Pow(2.0, bwCents / 1200.0) - 1.0);
        return bwHz;
    }

    private void GenerateHarmonicProfile(PadHarmonicProfile profile)
    {
        Array.Clear(_harmonicAmplitudes, 0, _harmonicAmplitudes.Length);

        switch (profile)
        {
            case PadHarmonicProfile.Saw:
                for (int h = 1; h <= _harmonicAmplitudes.Length; h++)
                {
                    _harmonicAmplitudes[h - 1] = 1f / h;
                }
                break;

            case PadHarmonicProfile.Square:
                for (int h = 1; h <= _harmonicAmplitudes.Length; h += 2)
                {
                    _harmonicAmplitudes[h - 1] = 1f / h;
                }
                break;

            case PadHarmonicProfile.Triangle:
                for (int h = 1; h <= _harmonicAmplitudes.Length; h += 2)
                {
                    _harmonicAmplitudes[h - 1] = 1f / (h * h);
                }
                break;

            case PadHarmonicProfile.Vocal:
                // Formant-like profile with peaks around vocal formants
                float[] formantPeaks = { 1, 5, 9, 14, 20 };
                for (int h = 1; h <= _harmonicAmplitudes.Length; h++)
                {
                    float amp = 0;
                    foreach (float peak in formantPeaks)
                    {
                        float dist = MathF.Abs(h - peak) / 3f;
                        amp += MathF.Exp(-dist * dist);
                    }
                    _harmonicAmplitudes[h - 1] = amp / (1 + h * 0.1f);
                }
                break;

            case PadHarmonicProfile.Organ:
                // Hammond-like drawbar profile
                int[] drawbarHarmonics = { 1, 2, 3, 4, 5, 6, 8, 10, 12 };
                float[] drawbarLevels = { 0.8f, 0.8f, 0.8f, 0.8f, 0.7f, 0.6f, 0.5f, 0.4f, 0.3f };
                for (int i = 0; i < drawbarHarmonics.Length && drawbarHarmonics[i] <= _harmonicAmplitudes.Length; i++)
                {
                    _harmonicAmplitudes[drawbarHarmonics[i] - 1] = drawbarLevels[i];
                }
                break;

            case PadHarmonicProfile.Strings:
                // Bright string-like profile
                for (int h = 1; h <= _harmonicAmplitudes.Length; h++)
                {
                    _harmonicAmplitudes[h - 1] = 1f / MathF.Sqrt(h) * MathF.Exp(-h * 0.02f);
                }
                break;

            case PadHarmonicProfile.SoftPad:
                // Soft, mellow profile with rolled-off highs
                for (int h = 1; h <= _harmonicAmplitudes.Length; h++)
                {
                    _harmonicAmplitudes[h - 1] = MathF.Exp(-h * 0.15f);
                }
                break;
        }
    }

    private float[] InverseFFT(double[] real, double[] imag)
    {
        int n = _wavetableSize;
        var result = new float[n];

        // Build full spectrum (mirror for real signal)
        var fullReal = new double[n];
        var fullImag = new double[n];

        for (int i = 0; i < real.Length; i++)
        {
            fullReal[i] = real[i];
            fullImag[i] = imag[i];
        }

        // Mirror conjugate for negative frequencies
        for (int i = 1; i < real.Length - 1; i++)
        {
            fullReal[n - i] = real[i];
            fullImag[n - i] = -imag[i];
        }

        // Cooley-Tukey IFFT
        int bits = (int)Math.Log2(n);

        // Bit reversal permutation
        for (int i = 0; i < n; i++)
        {
            int j = BitReverse(i, bits);
            if (j > i)
            {
                (fullReal[i], fullReal[j]) = (fullReal[j], fullReal[i]);
                (fullImag[i], fullImag[j]) = (fullImag[j], fullImag[i]);
            }
        }

        // FFT butterflies
        for (int size = 2; size <= n; size *= 2)
        {
            double angle = 2.0 * Math.PI / size; // Positive for IFFT
            double wReal = Math.Cos(angle);
            double wImag = Math.Sin(angle);

            for (int start = 0; start < n; start += size)
            {
                double wnReal = 1;
                double wnImag = 0;

                for (int k = 0; k < size / 2; k++)
                {
                    int evenIdx = start + k;
                    int oddIdx = start + k + size / 2;

                    double tReal = wnReal * fullReal[oddIdx] - wnImag * fullImag[oddIdx];
                    double tImag = wnReal * fullImag[oddIdx] + wnImag * fullReal[oddIdx];

                    fullReal[oddIdx] = fullReal[evenIdx] - tReal;
                    fullImag[oddIdx] = fullImag[evenIdx] - tImag;
                    fullReal[evenIdx] += tReal;
                    fullImag[evenIdx] += tImag;

                    double newWnReal = wnReal * wReal - wnImag * wImag;
                    wnImag = wnReal * wImag + wnImag * wReal;
                    wnReal = newWnReal;
                }
            }
        }

        // Normalize and extract real part
        for (int i = 0; i < n; i++)
        {
            result[i] = (float)(fullReal[i] / n);
        }

        return result;
    }

    private static int BitReverse(int x, int bits)
    {
        int result = 0;
        for (int i = 0; i < bits; i++)
        {
            result = (result << 1) | (x & 1);
            x >>= 1;
        }
        return result;
    }

    /// <summary>
    /// Triggers a note.
    /// </summary>
    public void NoteOn(int note, int velocity)
    {
        lock (_lock)
        {
            if (_noteToVoice.TryGetValue(note, out var existingVoice))
            {
                existingVoice.Trigger(note, velocity);
                return;
            }

            PadVoice? voice = null;

            foreach (var v in _voices)
            {
                if (!v.IsActive)
                {
                    voice = v;
                    break;
                }
            }

            if (voice == null && _voices.Count < MaxVoices)
            {
                voice = new PadVoice(_waveFormat.SampleRate, this);
                _voices.Add(voice);
            }

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
            case "bandwidth":
                Bandwidth = value;
                break;
            case "bandwidthscale":
                BandwidthScale = value;
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
            case "seed":
                Seed = (int)value;
                break;
        }
    }

    /// <summary>
    /// Gets a sample from the wavetable with interpolation.
    /// </summary>
    internal float GetSample(double phase)
    {
        if (_wavetable == null || _wavetable.Length == 0)
            return 0f;

        // Convert phase (0-2pi) to sample position
        double position = (phase / (2.0 * Math.PI)) * _wavetable.Length;
        int idx1 = (int)position % _wavetable.Length;
        if (idx1 < 0) idx1 += _wavetable.Length;
        int idx2 = (idx1 + 1) % _wavetable.Length;
        float frac = (float)(position - Math.Floor(position));

        // Linear interpolation
        return _wavetable[idx1] * (1f - frac) + _wavetable[idx2] * frac;
    }

    /// <summary>
    /// Reads audio samples.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
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
                float leftSample = 0f;
                float rightSample = 0f;

                foreach (var voice in _voices)
                {
                    if (!voice.IsActive) continue;

                    var (left, right) = voice.Process(deltaTime);
                    leftSample += left;
                    rightSample += right;
                }

                leftSample *= Volume;
                rightSample *= Volume;

                // Soft clipping
                leftSample = MathF.Tanh(leftSample);
                rightSample = MathF.Tanh(rightSample);

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

    #region Presets

    /// <summary>
    /// Creates a smooth evolving pad.
    /// </summary>
    public static PadSynth CreateEvolvingPad()
    {
        var synth = new PadSynth();
        synth.Name = "Evolving Pad";
        synth.Profile = PadHarmonicProfile.SoftPad;
        synth.Bandwidth = 60f;
        synth.BandwidthScale = 1.2f;
        synth.AmpEnvelope.Attack = 1.0;
        synth.AmpEnvelope.Decay = 0.5;
        synth.AmpEnvelope.Sustain = 0.8;
        synth.AmpEnvelope.Release = 2.0;
        synth.UnisonVoices = 3;
        synth.UnisonDetune = 8f;
        synth.UnisonSpread = 0.6f;
        synth.GenerateWavetable();
        return synth;
    }

    /// <summary>
    /// Creates a thick chorus pad.
    /// </summary>
    public static PadSynth CreateChorusPad()
    {
        var synth = new PadSynth();
        synth.Name = "Chorus Pad";
        synth.Profile = PadHarmonicProfile.Strings;
        synth.Bandwidth = 100f;
        synth.BandwidthScale = 0.8f;
        synth.AmpEnvelope.Attack = 0.3;
        synth.AmpEnvelope.Decay = 0.3;
        synth.AmpEnvelope.Sustain = 0.9;
        synth.AmpEnvelope.Release = 1.5;
        synth.UnisonVoices = 5;
        synth.UnisonDetune = 15f;
        synth.UnisonSpread = 0.8f;
        synth.GenerateWavetable();
        return synth;
    }

    /// <summary>
    /// Creates a vocal choir pad.
    /// </summary>
    public static PadSynth CreateChoirPad()
    {
        var synth = new PadSynth();
        synth.Name = "Choir Pad";
        synth.Profile = PadHarmonicProfile.Vocal;
        synth.Bandwidth = 40f;
        synth.BandwidthScale = 1.5f;
        synth.AmpEnvelope.Attack = 0.5;
        synth.AmpEnvelope.Decay = 0.5;
        synth.AmpEnvelope.Sustain = 0.7;
        synth.AmpEnvelope.Release = 1.2;
        synth.UnisonVoices = 4;
        synth.UnisonDetune = 6f;
        synth.UnisonSpread = 0.5f;
        synth.GenerateWavetable();
        return synth;
    }

    /// <summary>
    /// Creates a dark ambient pad.
    /// </summary>
    public static PadSynth CreateAmbientPad()
    {
        var synth = new PadSynth();
        synth.Name = "Ambient Pad";
        synth.Profile = PadHarmonicProfile.Triangle;
        synth.Bandwidth = 80f;
        synth.BandwidthScale = 1.8f;
        synth.AmpEnvelope.Attack = 2.0;
        synth.AmpEnvelope.Decay = 1.0;
        synth.AmpEnvelope.Sustain = 0.6;
        synth.AmpEnvelope.Release = 3.0;
        synth.UnisonVoices = 2;
        synth.UnisonDetune = 4f;
        synth.UnisonSpread = 0.3f;
        synth.GenerateWavetable();
        return synth;
    }

    #endregion
}

/// <summary>
/// Internal voice for PadSynth.
/// </summary>
internal class PadVoice
{
    private readonly int _sampleRate;
    private readonly PadSynth _synth;
    private readonly Envelope _ampEnv;
    private readonly double[] _phases;
    private readonly float[] _panPositions;

    public int Note { get; private set; }
    public int Velocity { get; private set; }
    public double Frequency { get; private set; }
    public DateTime TriggerTime { get; private set; }
    public bool IsActive => _ampEnv.IsActive;

    public PadVoice(int sampleRate, PadSynth synth)
    {
        _sampleRate = sampleRate;
        _synth = synth;
        _ampEnv = new Envelope(0.5, 0.5, 0.8, 1.0);
        _phases = new double[8];
        _panPositions = new float[8];
    }

    public void Trigger(int note, int velocity)
    {
        Note = note;
        Velocity = velocity;
        Frequency = 440.0 * Math.Pow(2.0, (note - 69.0) / 12.0);
        TriggerTime = DateTime.Now;

        _ampEnv.Attack = _synth.AmpEnvelope.Attack;
        _ampEnv.Decay = _synth.AmpEnvelope.Decay;
        _ampEnv.Sustain = _synth.AmpEnvelope.Sustain;
        _ampEnv.Release = _synth.AmpEnvelope.Release;

        // Randomize starting phases for each unison voice
        var random = new Random();
        for (int i = 0; i < _phases.Length; i++)
        {
            _phases[i] = random.NextDouble() * 2.0 * Math.PI;
        }

        int unisonCount = _synth.UnisonVoices;
        for (int i = 0; i < _panPositions.Length; i++)
        {
            if (unisonCount == 1)
            {
                _panPositions[i] = 0f;
            }
            else
            {
                float t = (float)i / (unisonCount - 1);
                _panPositions[i] = (t * 2f - 1f) * _synth.UnisonSpread;
            }
        }

        _ampEnv.Trigger(velocity);
    }

    public void Release()
    {
        _ampEnv.Release_Gate();
    }

    public (float left, float right) Process(double deltaTime)
    {
        if (!IsActive) return (0f, 0f);

        double ampEnv = _ampEnv.Process(deltaTime);
        if (_ampEnv.Stage == EnvelopeStage.Idle) return (0f, 0f);

        float leftSample = 0f;
        float rightSample = 0f;
        int unisonCount = _synth.UnisonVoices;

        for (int u = 0; u < unisonCount; u++)
        {
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

            float sample = _synth.GetSample(_phases[u]);

            float pan = _panPositions[u];
            float leftGain = MathF.Cos((pan + 1f) * MathF.PI / 4f);
            float rightGain = MathF.Sin((pan + 1f) * MathF.PI / 4f);

            leftSample += sample * leftGain / unisonCount;
            rightSample += sample * rightGain / unisonCount;
        }

        float envGain = (float)(ampEnv * (Velocity / 127.0));
        leftSample *= envGain;
        rightSample *= envGain;

        return (leftSample, rightSample);
    }
}
