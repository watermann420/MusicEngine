// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using System;
using System.Collections.Generic;
using NAudio.Wave;


namespace MusicEngine.Core;


/// <summary>
/// Type of physical model
/// </summary>
public enum PhysicalModelType
{
    /// <summary>Karplus-Strong plucked string</summary>
    PluckedString,
    /// <summary>Bowed string simulation</summary>
    BowedString,
    /// <summary>Metal/drum membrane</summary>
    DrumMembrane,
    /// <summary>Wind instrument tube</summary>
    WindTube,
    /// <summary>Bell/bar percussion</summary>
    Bell
}


/// <summary>
/// Physical modeling synthesizer using waveguide synthesis.
/// Simulates plucked strings, bowed strings, drums, and wind instruments.
/// </summary>
public class PhysicalModelingSynth : ISynth
{
    private readonly WaveFormat _waveFormat;
    private readonly List<PhysicalVoice> _voices = new();
    private readonly Dictionary<int, PhysicalVoice> _noteToVoice = new();
    private readonly object _lock = new();

    /// <summary>Synth name</summary>
    public string Name { get; set; } = "PhysicalModeling";

    /// <summary>Audio format</summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>Maximum polyphony</summary>
    public int MaxVoices { get; set; } = 16;

    /// <summary>Master volume</summary>
    public float Volume { get; set; } = 0.7f;

    /// <summary>Physical model type</summary>
    public PhysicalModelType ModelType { get; set; } = PhysicalModelType.PluckedString;

    /// <summary>Damping factor (0-1). Higher = faster decay</summary>
    public float Damping { get; set; } = 0.5f;

    /// <summary>Brightness (filter cutoff, 0-1)</summary>
    public float Brightness { get; set; } = 0.8f;

    /// <summary>Exciter type: 0 = noise burst, 1 = impulse</summary>
    public float ExciterType { get; set; } = 0.5f;

    /// <summary>Exciter position on string (0-1)</summary>
    public float ExciterPosition { get; set; } = 0.5f;

    /// <summary>Pickup position on string (0-1)</summary>
    public float PickupPosition { get; set; } = 0.25f;

    /// <summary>Nonlinearity for metallic sounds (0-1)</summary>
    public float Nonlinearity { get; set; } = 0f;

    /// <summary>Inharmonicity (string stiffness, 0-1)</summary>
    public float Inharmonicity { get; set; } = 0f;

    /// <summary>Body resonance amount (0-1)</summary>
    public float BodyResonance { get; set; } = 0.3f;

    /// <summary>
    /// For bowed string: bow pressure (0-1)
    /// </summary>
    public float BowPressure { get; set; } = 0.5f;

    /// <summary>
    /// For bowed string: bow velocity (0-1)
    /// </summary>
    public float BowVelocity { get; set; } = 0.5f;

    /// <summary>
    /// Creates a physical modeling synth
    /// </summary>
    public PhysicalModelingSynth(int? sampleRate = null)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);
    }

    /// <summary>
    /// Trigger a note
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

            PhysicalVoice? voice = null;

            // Find inactive voice
            foreach (var v in _voices)
            {
                if (!v.IsActive)
                {
                    voice = v;
                    break;
                }
            }

            // Create new voice
            if (voice == null && _voices.Count < MaxVoices)
            {
                voice = new PhysicalVoice(_waveFormat.SampleRate, this);
                _voices.Add(voice);
            }

            // Voice stealing
            if (voice == null && _voices.Count > 0)
            {
                voice = _voices[0];
                foreach (var v in _voices)
                {
                    if (v.TriggerTime < voice.TriggerTime)
                    {
                        voice = v;
                    }
                }

                _noteToVoice.Remove(voice.Note);
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
    /// Set parameter by name
    /// </summary>
    public void SetParameter(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "volume":
                Volume = Math.Clamp(value, 0f, 1f);
                break;
            case "damping":
                Damping = Math.Clamp(value, 0f, 1f);
                break;
            case "brightness":
                Brightness = Math.Clamp(value, 0f, 1f);
                break;
            case "excitertype":
                ExciterType = Math.Clamp(value, 0f, 1f);
                break;
            case "exciterposition":
                ExciterPosition = Math.Clamp(value, 0.01f, 0.99f);
                break;
            case "pickupposition":
                PickupPosition = Math.Clamp(value, 0.01f, 0.99f);
                break;
            case "nonlinearity":
                Nonlinearity = Math.Clamp(value, 0f, 1f);
                break;
            case "inharmonicity":
                Inharmonicity = Math.Clamp(value, 0f, 1f);
                break;
            case "bodyresonance":
                BodyResonance = Math.Clamp(value, 0f, 1f);
                break;
            case "bowpressure":
                BowPressure = Math.Clamp(value, 0f, 1f);
                break;
            case "bowvelocity":
                BowVelocity = Math.Clamp(value, 0f, 1f);
                break;
            case "modeltype":
                ModelType = (PhysicalModelType)(int)value;
                break;
        }
    }

    /// <summary>
    /// Read audio samples
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        for (int i = 0; i < count; i++)
        {
            buffer[offset + i] = 0;
        }

        int channels = _waveFormat.Channels;

        lock (_lock)
        {
            for (int n = 0; n < count; n += channels)
            {
                float sample = 0f;

                foreach (var voice in _voices)
                {
                    if (!voice.IsActive) continue;
                    sample += voice.Process();
                }

                // Apply volume
                sample *= Volume;

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

    /// <summary>
    /// Create a plucked string preset (guitar-like)
    /// </summary>
    public static PhysicalModelingSynth CreateGuitarPreset()
    {
        var synth = new PhysicalModelingSynth();
        synth.Name = "Acoustic Guitar";
        synth.ModelType = PhysicalModelType.PluckedString;
        synth.Damping = 0.5f;
        synth.Brightness = 0.7f;
        synth.ExciterType = 0.3f;
        synth.ExciterPosition = 0.15f;
        synth.PickupPosition = 0.25f;
        synth.BodyResonance = 0.5f;
        synth.Inharmonicity = 0.02f;
        return synth;
    }

    /// <summary>
    /// Create a piano-like preset
    /// </summary>
    public static PhysicalModelingSynth CreatePianoPreset()
    {
        var synth = new PhysicalModelingSynth();
        synth.Name = "Piano String";
        synth.ModelType = PhysicalModelType.PluckedString;
        synth.Damping = 0.4f;
        synth.Brightness = 0.8f;
        synth.ExciterType = 0.9f; // Hammer-like impulse
        synth.ExciterPosition = 0.12f;
        synth.PickupPosition = 0.5f;
        synth.BodyResonance = 0.6f;
        synth.Inharmonicity = 0.05f;
        return synth;
    }

    /// <summary>
    /// Create a bell/metallic preset
    /// </summary>
    public static PhysicalModelingSynth CreateBellPreset()
    {
        var synth = new PhysicalModelingSynth();
        synth.Name = "Bell";
        synth.ModelType = PhysicalModelType.Bell;
        synth.Damping = 0.2f;
        synth.Brightness = 0.9f;
        synth.ExciterType = 1.0f;
        synth.Nonlinearity = 0.3f;
        synth.Inharmonicity = 0.3f;
        synth.BodyResonance = 0.4f;
        return synth;
    }

    /// <summary>
    /// Create a drum preset
    /// </summary>
    public static PhysicalModelingSynth CreateDrumPreset()
    {
        var synth = new PhysicalModelingSynth();
        synth.Name = "Drum";
        synth.ModelType = PhysicalModelType.DrumMembrane;
        synth.Damping = 0.7f;
        synth.Brightness = 0.5f;
        synth.ExciterType = 0.8f;
        synth.ExciterPosition = 0.3f;
        synth.Nonlinearity = 0.1f;
        synth.BodyResonance = 0.7f;
        return synth;
    }
}


/// <summary>
/// Internal voice for physical modeling
/// </summary>
internal class PhysicalVoice
{
    private readonly int _sampleRate;
    private readonly PhysicalModelingSynth _synth;
    private readonly Random _random = new();

    // Delay line for Karplus-Strong
    private float[] _delayLine;
    private int _delayLength;
    private int _writeIndex;

    // Secondary delay for pickup position
    private float[] _delayLine2;
    private int _delayLength2;
#pragma warning disable CS0414 // Reserved for future pickup position feature
    private int _writeIndex2;
#pragma warning restore CS0414

    // Filter states
    private float _filterState1;
    private float _filterState2;
    private float _prevOutput;

    // Body resonance filter
    private float _bodyState1;
    private float _bodyState2;

    // Bowed string state - reserved for future bowed instrument synthesis
#pragma warning disable CS0169
    private float _bowPosition;
    private float _bowForce;
#pragma warning restore CS0169

    // Envelope
    private float _exciterEnv;
    private float _releaseGain;
    private bool _isReleasing;

    public int Note { get; private set; }
    public int Velocity { get; private set; }
    public double Frequency { get; private set; }
    public DateTime TriggerTime { get; private set; }
    public bool IsActive { get; private set; }

    public PhysicalVoice(int sampleRate, PhysicalModelingSynth synth)
    {
        _sampleRate = sampleRate;
        _synth = synth;

        // Allocate delay lines for lowest frequency
        int maxDelay = sampleRate / 20; // ~20Hz minimum
        _delayLine = new float[maxDelay];
        _delayLine2 = new float[maxDelay];
    }

    public void Trigger(int note, int velocity)
    {
        Note = note;
        Velocity = velocity;
        Frequency = 440.0 * Math.Pow(2.0, (note - 69.0) / 12.0);
        TriggerTime = DateTime.Now;
        IsActive = true;
        _isReleasing = false;
        _releaseGain = 1f;

        // Calculate delay length for this frequency
        _delayLength = (int)(_sampleRate / Frequency);
        _delayLength = Math.Max(2, Math.Min(_delayLength, _delayLine.Length - 1));

        // Secondary delay for pickup position
        _delayLength2 = (int)(_delayLength * _synth.PickupPosition);
        _delayLength2 = Math.Max(1, Math.Min(_delayLength2, _delayLine2.Length - 1));

        // Clear delay lines
        Array.Clear(_delayLine, 0, _delayLine.Length);
        Array.Clear(_delayLine2, 0, _delayLine2.Length);

        _writeIndex = 0;
        _writeIndex2 = 0;

        // Reset filter states
        _filterState1 = 0;
        _filterState2 = 0;
        _prevOutput = 0;
        _bodyState1 = 0;
        _bodyState2 = 0;

        // Initialize delay line with excitation
        InitializeExcitation(velocity / 127f);

        // Set exciter envelope
        _exciterEnv = 1f;
    }

    private void InitializeExcitation(float velocityScale)
    {
        int exciterPos = (int)(_delayLength * _synth.ExciterPosition);

        for (int i = 0; i < _delayLength; i++)
        {
            float excitation = 0f;

            switch (_synth.ModelType)
            {
                case PhysicalModelType.PluckedString:
                    // Mix of noise and impulse based on ExciterType
                    float noise = (float)(_random.NextDouble() * 2 - 1);
                    float impulse = i == exciterPos ? 1f : 0f;
                    excitation = noise * (1f - _synth.ExciterType) + impulse * _synth.ExciterType;

                    // Shape with triangle envelope
                    float pos = (float)i / _delayLength;
                    float envelope = pos < _synth.ExciterPosition
                        ? pos / _synth.ExciterPosition
                        : (1f - pos) / (1f - _synth.ExciterPosition);
                    excitation *= envelope;
                    break;

                case PhysicalModelType.DrumMembrane:
                    // Noise burst
                    excitation = (float)(_random.NextDouble() * 2 - 1);
                    // Circular membrane shape
                    float r = Math.Abs((float)i / _delayLength - 0.5f) * 2f;
                    excitation *= 1f - r * r;
                    break;

                case PhysicalModelType.Bell:
                    // Sharp impulse for metallic sound
                    excitation = i < 3 ? 1f : 0f;
                    break;

                case PhysicalModelType.WindTube:
                    // Air burst
                    excitation = (float)(_random.NextDouble() * 2 - 1) * 0.3f;
                    break;

                case PhysicalModelType.BowedString:
                    // Small noise to start oscillation
                    excitation = (float)(_random.NextDouble() * 2 - 1) * 0.1f;
                    break;
            }

            _delayLine[i] = excitation * velocityScale;
        }
    }

    public void Release()
    {
        _isReleasing = true;
    }

    public float Process()
    {
        if (!IsActive) return 0f;

        // Read from delay line with fractional delay for tuning accuracy
        float delay = _sampleRate / (float)Frequency;
        int intDelay = (int)delay;
        float frac = delay - intDelay;

        int readIndex = (_writeIndex - intDelay + _delayLine.Length) % _delayLine.Length;
        int readIndex2 = (readIndex + 1) % _delayLine.Length;

        // Linear interpolation for fractional delay
        float sample = _delayLine[readIndex] * (1f - frac) + _delayLine[readIndex2] * frac;

        // Apply damping filter (lowpass)
        float dampCoeff = 1f - _synth.Damping * 0.5f;
        float brightnessCoeff = _synth.Brightness * 0.5f + 0.5f;

        // Two-point averaging filter with brightness control
        float filtered = (_filterState1 + sample) * 0.5f;
        filtered = _filterState2 + brightnessCoeff * (filtered - _filterState2);
        _filterState1 = sample;
        _filterState2 = filtered;

        // Apply damping
        filtered *= dampCoeff;

        // Add inharmonicity (allpass filter for dispersion)
        if (_synth.Inharmonicity > 0)
        {
            float inharm = _synth.Inharmonicity * 0.3f;
            float allpassOut = inharm * filtered + _prevOutput - inharm * _prevOutput;
            _prevOutput = filtered;
            filtered = allpassOut;
        }

        // Add nonlinearity for metallic sounds
        if (_synth.Nonlinearity > 0)
        {
            filtered = filtered - _synth.Nonlinearity * filtered * filtered * filtered;
        }

        // Bowed string continuous excitation
        if (_synth.ModelType == PhysicalModelType.BowedString && !_isReleasing)
        {
            float bowExcitation = ProcessBowing(sample);
            filtered += bowExcitation;
        }

        // Write back to delay line
        _delayLine[_writeIndex] = filtered;
        _writeIndex = (_writeIndex + 1) % _delayLength;

        // Secondary pickup (comb filtering effect)
        float output = sample;
        if (_synth.PickupPosition != 0.5f)
        {
            int pickup = (_writeIndex - _delayLength2 + _delayLine.Length) % _delayLine.Length;
            float pickupSample = _delayLine[pickup];
            output = (sample + pickupSample) * 0.5f;
        }

        // Body resonance filter (simple bandpass)
        if (_synth.BodyResonance > 0)
        {
            float bodyFreq = 150f; // Body resonance frequency
            float bodyQ = 2f + _synth.BodyResonance * 5f;

            float w0 = 2f * MathF.PI * bodyFreq / _sampleRate;
            float alpha = MathF.Sin(w0) / (2f * bodyQ);

            float b0 = alpha;
            float a1 = -2f * MathF.Cos(w0);
            float a2 = 1f - alpha;

            float bodyOut = b0 * output - a1 * _bodyState1 - a2 * _bodyState2;
            _bodyState2 = _bodyState1;
            _bodyState1 = bodyOut;

            output += bodyOut * _synth.BodyResonance;
        }

        // Release envelope
        if (_isReleasing)
        {
            _releaseGain *= 0.9995f;
            output *= _releaseGain;

            if (_releaseGain < 0.001f)
            {
                IsActive = false;
            }
        }

        // Natural decay check
        if (Math.Abs(output) < 0.0001f && _exciterEnv < 0.001f)
        {
            IsActive = false;
        }

        // Decay exciter envelope
        _exciterEnv *= 0.999f;

        return output;
    }

    private float ProcessBowing(float stringVelocity)
    {
        // Simplified bowing model
        float bowVel = _synth.BowVelocity * 2f - 1f; // -1 to 1
        float pressure = _synth.BowPressure;

        // Relative velocity between bow and string
        float relVelocity = bowVel - stringVelocity;

        // Friction curve (stick-slip)
        float friction;
        float absRel = Math.Abs(relVelocity);

        if (absRel < 0.1f)
        {
            // Static friction (sticking)
            friction = relVelocity * 10f * pressure;
        }
        else
        {
            // Dynamic friction (slipping)
            friction = Math.Sign(relVelocity) * (0.3f + 0.1f / absRel) * pressure;
        }

        return friction * 0.1f;
    }
}
