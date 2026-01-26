// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using NAudio.Wave;

namespace MusicEngine.Core;

/// <summary>
/// Ring modulator effect that multiplies carrier and modulator signals
/// </summary>
public class RingModulator : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly WaveFormat _waveFormat;
    private readonly object _lock = new();

    private double _phase = 0;

    // Parameters
    public float Frequency { get; set; } = 440f;          // Modulator frequency
    public float Depth { get; set; } = 1f;                // Modulation depth (0-1)
    public float Mix { get; set; } = 0.5f;                // Dry/Wet mix
    public WaveType ModulatorWaveform { get; set; } = WaveType.Sine;

    // LFO for frequency modulation
    public LFO? FrequencyLFO { get; set; }
    public float FrequencyLFODepth { get; set; } = 0f;    // In Hz

    public WaveFormat WaveFormat => _waveFormat;

    public RingModulator(ISampleProvider source)
    {
        _source = source;
        _waveFormat = source.WaveFormat;
    }

    public RingModulator(ISampleProvider source, float frequency) : this(source)
    {
        Frequency = frequency;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = _source.Read(buffer, offset, count);

        if (samplesRead == 0) return 0;

        int channels = _waveFormat.Channels;
        double sampleRate = _waveFormat.SampleRate;

        // Get LFO modulation
        float freqMod = 0;
        if (FrequencyLFO != null && FrequencyLFO.Enabled)
        {
            freqMod = (float)FrequencyLFO.GetValue(_waveFormat.SampleRate) * FrequencyLFODepth;
        }

        float effectiveFreq = Math.Max(0.1f, Frequency + freqMod);
        double phaseIncrement = 2.0 * Math.PI * effectiveFreq / sampleRate;

        lock (_lock)
        {
            for (int n = 0; n < samplesRead; n += channels)
            {
                // Generate modulator signal
                float modulator = GenerateModulator(_phase);

                // Apply depth
                float modulatorWithDepth = 1f - Depth + Depth * (modulator * 0.5f + 0.5f);

                // Process each channel
                for (int c = 0; c < channels && (n + c) < samplesRead; c++)
                {
                    int index = offset + n + c;
                    float dry = buffer[index];
                    float wet = dry * modulator * Depth + dry * (1 - Depth);

                    // Mix dry and wet
                    buffer[index] = dry * (1 - Mix) + wet * Mix;
                }

                // Advance phase
                _phase += phaseIncrement;
                if (_phase > 2 * Math.PI)
                {
                    _phase -= 2 * Math.PI;
                }
            }
        }

        return samplesRead;
    }

    private float GenerateModulator(double phase)
    {
        return ModulatorWaveform switch
        {
            WaveType.Sine => (float)Math.Sin(phase),
            WaveType.Square => phase < Math.PI ? 1f : -1f,
            WaveType.Sawtooth => (float)(2.0 * (phase / (2 * Math.PI)) - 1.0),
            WaveType.Triangle => phase < Math.PI
                ? (float)(2.0 * phase / Math.PI - 1.0)
                : (float)(3.0 - 2.0 * phase / Math.PI),
            _ => (float)Math.Sin(phase)
        };
    }
}

/// <summary>
/// Ring modulator as a synth that modulates one synth with another
/// </summary>
public class RingModulatorSynth : ISynth, ISampleProvider
{
    private readonly WaveFormat _waveFormat;
    private readonly object _lock = new();

    // ISynth Name property
    public string Name { get; set; } = "RingModulatorSynth";

    private readonly ISynth _carrier;
    private readonly ISynth _modulator;

    private float[] _carrierBuffer = Array.Empty<float>();
    private float[] _modulatorBuffer = Array.Empty<float>();

    public float Depth { get; set; } = 1f;
    public float Mix { get; set; } = 1f;
    public float Volume { get; set; } = 0.8f;

    // Carrier/Modulator frequency ratios
    public float CarrierRatio { get; set; } = 1f;
    public float ModulatorRatio { get; set; } = 2f;

    public WaveFormat WaveFormat => _waveFormat;

    public RingModulatorSynth(int sampleRate = 44100)
    {
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2);

        // Create internal synths
        _carrier = new SimpleSynth(sampleRate);
        _modulator = new SimpleSynth(sampleRate);

        // Set modulator to sine
        _modulator.SetParameter("waveform", 0);
    }

    public RingModulatorSynth(ISynth carrier, ISynth modulator, int sampleRate = 44100)
    {
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2);
        _carrier = carrier;
        _modulator = modulator;
    }

    public void NoteOn(int note, int velocity)
    {
        // Calculate carrier and modulator notes based on ratios
        int carrierNote = note + (int)(12 * Math.Log2(CarrierRatio));
        int modulatorNote = note + (int)(12 * Math.Log2(ModulatorRatio));

        _carrier.NoteOn(Math.Clamp(carrierNote, 0, 127), velocity);
        _modulator.NoteOn(Math.Clamp(modulatorNote, 0, 127), velocity);
    }

    public void NoteOff(int note)
    {
        int carrierNote = note + (int)(12 * Math.Log2(CarrierRatio));
        int modulatorNote = note + (int)(12 * Math.Log2(ModulatorRatio));

        _carrier.NoteOff(Math.Clamp(carrierNote, 0, 127));
        _modulator.NoteOff(Math.Clamp(modulatorNote, 0, 127));
    }

    public void AllNotesOff()
    {
        _carrier.AllNotesOff();
        _modulator.AllNotesOff();
    }

    public void SetParameter(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "depth":
                Depth = Math.Clamp(value, 0f, 1f);
                break;
            case "mix":
                Mix = Math.Clamp(value, 0f, 1f);
                break;
            case "volume":
                Volume = Math.Clamp(value, 0f, 1f);
                break;
            case "carrierratio":
                CarrierRatio = Math.Max(0.1f, value);
                break;
            case "modulatorratio":
                ModulatorRatio = Math.Max(0.1f, value);
                break;
            default:
                // Pass to carrier synth
                _carrier.SetParameter(name, value);
                break;
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        // Ensure buffers are large enough
        if (_carrierBuffer.Length < count)
        {
            _carrierBuffer = new float[count];
            _modulatorBuffer = new float[count];
        }

        // Clear buffers
        Array.Clear(_carrierBuffer, 0, count);
        Array.Clear(_modulatorBuffer, 0, count);

        // Read from both synths
        if (_carrier is ISampleProvider carrierProvider)
        {
            carrierProvider.Read(_carrierBuffer, 0, count);
        }

        if (_modulator is ISampleProvider modulatorProvider)
        {
            modulatorProvider.Read(_modulatorBuffer, 0, count);
        }

        // Ring modulation
        int channels = _waveFormat.Channels;
        for (int n = 0; n < count; n += channels)
        {
            for (int c = 0; c < channels && n + c < count; c++)
            {
                float carrier = _carrierBuffer[n + c];
                float modulator = _modulatorBuffer[n + c];

                // Ring modulation: carrier * modulator
                float ringMod = carrier * modulator * Depth + carrier * (1 - Depth);

                // Mix and output
                buffer[offset + n + c] = ringMod * Mix * Volume;
            }
        }

        return count;
    }
}

/// <summary>
/// Amplitude modulator (tremolo at audio rate)
/// </summary>
public class AmplitudeModulator : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly WaveFormat _waveFormat;

    private double _phase = 0;

    public float Frequency { get; set; } = 5f;            // Modulation frequency
    public float Depth { get; set; } = 0.5f;              // Modulation depth (0-1)
    public WaveType Waveform { get; set; } = WaveType.Sine;

    public WaveFormat WaveFormat => _waveFormat;

    public AmplitudeModulator(ISampleProvider source)
    {
        _source = source;
        _waveFormat = source.WaveFormat;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = _source.Read(buffer, offset, count);
        if (samplesRead == 0) return 0;

        int channels = _waveFormat.Channels;
        double sampleRate = _waveFormat.SampleRate;
        double phaseIncrement = 2.0 * Math.PI * Frequency / sampleRate;

        for (int n = 0; n < samplesRead; n += channels)
        {
            float modulator = GenerateModulator(_phase);

            // Scale modulator to 0-1 range and apply depth
            float gain = 1f - Depth * 0.5f + Depth * 0.5f * modulator;

            for (int c = 0; c < channels && (n + c) < samplesRead; c++)
            {
                buffer[offset + n + c] *= gain;
            }

            _phase += phaseIncrement;
            if (_phase > 2 * Math.PI) _phase -= 2 * Math.PI;
        }

        return samplesRead;
    }

    private float GenerateModulator(double phase)
    {
        return Waveform switch
        {
            WaveType.Sine => (float)Math.Sin(phase),
            WaveType.Square => phase < Math.PI ? 1f : -1f,
            WaveType.Sawtooth => (float)(2.0 * (phase / (2 * Math.PI)) - 1.0),
            WaveType.Triangle => phase < Math.PI
                ? (float)(2.0 * phase / Math.PI - 1.0)
                : (float)(3.0 - 2.0 * phase / Math.PI),
            _ => (float)Math.Sin(phase)
        };
    }
}

/// <summary>
/// Frequency shifter effect
/// </summary>
public class FrequencyShifter : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly WaveFormat _waveFormat;

    private double _phase = 0;
    private float _hilbertI = 0;
    private float _hilbertQ = 0;

    // Hilbert transform coefficients (simple approximation)
    private readonly float[] _hilbertCoeffs = { 0.94657f, 0.94657f };
    private float _prevI = 0;
    private float _prevQ = 0;

    public float ShiftHz { get; set; } = 0f;              // Frequency shift in Hz
    public float Mix { get; set; } = 1f;                  // Dry/Wet mix

    public WaveFormat WaveFormat => _waveFormat;

    public FrequencyShifter(ISampleProvider source)
    {
        _source = source;
        _waveFormat = source.WaveFormat;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = _source.Read(buffer, offset, count);
        if (samplesRead == 0 || Math.Abs(ShiftHz) < 0.001f) return samplesRead;

        int channels = _waveFormat.Channels;
        double sampleRate = _waveFormat.SampleRate;
        double phaseIncrement = 2.0 * Math.PI * Math.Abs(ShiftHz) / sampleRate;
        bool shiftUp = ShiftHz > 0;

        for (int n = 0; n < samplesRead; n += channels)
        {
            // Simple Hilbert transform approximation
            float input = buffer[offset + n];

            // All-pass based Hilbert (simplified)
            _hilbertI = input * 0.94657f + _prevI * 0.94657f - input;
            _hilbertQ = _hilbertI * 0.94657f + _prevQ * 0.94657f - _hilbertI;

            _prevI = input;
            _prevQ = _hilbertI;

            // Frequency shift using complex multiplication
            float cosPhase = (float)Math.Cos(_phase);
            float sinPhase = (float)Math.Sin(_phase);

            float shifted;
            if (shiftUp)
            {
                shifted = input * cosPhase - _hilbertQ * sinPhase;
            }
            else
            {
                shifted = input * cosPhase + _hilbertQ * sinPhase;
            }

            // Mix
            float dry = buffer[offset + n];
            buffer[offset + n] = dry * (1 - Mix) + shifted * Mix;

            // Copy to other channels
            for (int c = 1; c < channels && (n + c) < samplesRead; c++)
            {
                buffer[offset + n + c] = buffer[offset + n];
            }

            _phase += phaseIncrement;
            if (_phase > 2 * Math.PI) _phase -= 2 * Math.PI;
        }

        return samplesRead;
    }
}
