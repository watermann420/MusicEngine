// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Vocoder effect processor.

using NAudio.Wave;

namespace MusicEngine.Core.Effects;

/// <summary>
/// Classic vocoder effect using filterbank analysis/synthesis
/// </summary>
public class Vocoder : EffectBase
{
    private readonly int _bandCount;
    private readonly VocoderBand[] _bands;

    // Parameters - use new to hide base Mix
    public new float Mix { get; set; } = 1f;               // Dry/Wet mix
    public float Attack { get; set; } = 0.01f;         // Envelope attack time
    public float Release { get; set; } = 0.1f;         // Envelope release time
    public float FormantShift { get; set; } = 0f;      // Formant shift in bands (-8 to +8)
    public float Sibilance { get; set; } = 0.5f;       // High frequency sibilance pass-through
    public float OutputGain { get; set; } = 1f;

    // Internal carrier (can be replaced with external)
    private float _carrierPhase = 0;
    public float CarrierFrequency { get; set; } = 220f;
    public bool UseInternalCarrier { get; set; } = true;

    // External carrier buffer
    private float[] _carrierBuffer = Array.Empty<float>();
    private int _carrierIndex = 0;

    private int _lastSampleRate = 44100;

    public Vocoder(ISampleProvider source, int bandCount = 16) : base(source, "Vocoder")
    {
        _bandCount = Math.Clamp(bandCount, 4, 32);
        _bands = new VocoderBand[_bandCount];

        InitializeBands(source.WaveFormat.SampleRate);
    }

    private void InitializeBands(int sampleRate)
    {
        _lastSampleRate = sampleRate;

        // Logarithmically spaced bands from ~100Hz to ~8kHz
        float minFreq = 100f;
        float maxFreq = 8000f;
        float freqRatio = (float)Math.Pow(maxFreq / minFreq, 1.0 / (_bandCount - 1));

        for (int i = 0; i < _bandCount; i++)
        {
            float centerFreq = minFreq * (float)Math.Pow(freqRatio, i);
            float bandwidth = centerFreq / 2; // Q ~ 2

            _bands[i] = new VocoderBand
            {
                CenterFrequency = centerFreq,
                Bandwidth = bandwidth,
                Envelope = 0,
                AnalysisFilter = new StateVariableFilter(),
                SynthesisFilter = new StateVariableFilter()
            };

            _bands[i].AnalysisFilter.SetBandpass(centerFreq, bandwidth, sampleRate);
            _bands[i].SynthesisFilter.SetBandpass(centerFreq, bandwidth, sampleRate);
        }
    }

    /// <summary>
    /// Set external carrier signal
    /// </summary>
    public void SetCarrier(float[] carrierSamples)
    {
        _carrierBuffer = (float[])carrierSamples.Clone();
        _carrierIndex = 0;
        UseInternalCarrier = false;
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int sampleRate = SampleRate;
        int channels = Channels;

        if (sampleRate != _lastSampleRate)
        {
            InitializeBands(sampleRate);
        }

        float attackCoeff = (float)Math.Exp(-1.0 / (Attack * sampleRate));
        float releaseCoeff = (float)Math.Exp(-1.0 / (Release * sampleRate));

        int formantShiftBands = (int)FormantShift;

        for (int n = 0; n < count; n += channels)
        {
            // Modulator (input signal, usually voice)
            float modulatorL = sourceBuffer[n];
            float modulatorR = channels > 1 ? sourceBuffer[n + 1] : sourceBuffer[n];
            float modulator = (modulatorL + modulatorR) * 0.5f; // Mono modulator

            // Get carrier signal
            float carrier;
            if (UseInternalCarrier)
            {
                // Internal sawtooth carrier
                carrier = (float)(_carrierPhase / Math.PI - 1);
                _carrierPhase += (float)(2 * Math.PI * CarrierFrequency / sampleRate);
                if (_carrierPhase > 2 * Math.PI) _carrierPhase -= (float)(2 * Math.PI);
            }
            else if (_carrierBuffer.Length > 0)
            {
                carrier = _carrierBuffer[_carrierIndex];
                _carrierIndex = (_carrierIndex + 1) % _carrierBuffer.Length;
            }
            else
            {
                carrier = 0;
            }

            // Process each band
            float outputL = 0;
            float outputR = 0;

            for (int b = 0; b < _bandCount; b++)
            {
                var band = _bands[b];

                // Analyze modulator - extract band energy
                float bandSignal = band.AnalysisFilter.Process(modulator);
                float bandEnergy = Math.Abs(bandSignal);

                // Envelope follower
                if (bandEnergy > band.Envelope)
                    band.Envelope = bandEnergy + attackCoeff * (band.Envelope - bandEnergy);
                else
                    band.Envelope = bandEnergy + releaseCoeff * (band.Envelope - bandEnergy);

                // Apply formant shift
                int targetBand = b + formantShiftBands;
                if (targetBand < 0 || targetBand >= _bandCount) continue;

                // Filter carrier through synthesis filter
                float synthOutput = _bands[targetBand].SynthesisFilter.Process(carrier);

                // Apply envelope to carrier
                float vocodedSignal = synthOutput * band.Envelope;

                outputL += vocodedSignal;
                outputR += vocodedSignal;
            }

            // Add sibilance (high frequency pass-through from modulator)
            if (Sibilance > 0)
            {
                // Simple highpass for sibilance (last few bands)
                float sibilanceSignal = 0;
                for (int b = _bandCount - 3; b < _bandCount; b++)
                {
                    sibilanceSignal += _bands[b].AnalysisFilter.Process(modulator);
                }
                outputL += sibilanceSignal * Sibilance * 0.5f;
                outputR += sibilanceSignal * Sibilance * 0.5f;
            }

            // Apply gain and mix
            outputL *= OutputGain;
            outputR *= OutputGain;

            destBuffer[offset + n] = modulatorL * (1 - Mix) + outputL * Mix;
            if (channels > 1)
                destBuffer[offset + n + 1] = modulatorR * (1 - Mix) + outputR * Mix;
        }
    }

    /// <summary>
    /// Create preset vocoder configurations
    /// </summary>
    public static Vocoder CreatePreset(ISampleProvider source, string presetName)
    {
        Vocoder vocoder;

        switch (presetName.ToLowerInvariant())
        {
            case "classic":
                vocoder = new Vocoder(source, 16);
                vocoder.Attack = 0.01f;
                vocoder.Release = 0.1f;
                vocoder.Sibilance = 0.3f;
                break;

            case "robot":
                vocoder = new Vocoder(source, 12);
                vocoder.Attack = 0.005f;
                vocoder.Release = 0.05f;
                vocoder.Sibilance = 0.1f;
                vocoder.CarrierFrequency = 110f;
                break;

            case "whisper":
                vocoder = new Vocoder(source, 24);
                vocoder.Attack = 0.02f;
                vocoder.Release = 0.2f;
                vocoder.Sibilance = 0.8f;
                vocoder.OutputGain = 0.7f;
                break;

            case "choir":
                vocoder = new Vocoder(source, 20);
                vocoder.Attack = 0.03f;
                vocoder.Release = 0.15f;
                vocoder.Sibilance = 0.4f;
                vocoder.CarrierFrequency = 330f;
                break;

            case "alien":
                vocoder = new Vocoder(source, 8);
                vocoder.Attack = 0.001f;
                vocoder.Release = 0.02f;
                vocoder.FormantShift = 4f;
                vocoder.Sibilance = 0.2f;
                break;

            default:
                vocoder = new Vocoder(source, 16);
                break;
        }

        return vocoder;
    }

    private class VocoderBand
    {
        public float CenterFrequency { get; set; }
        public float Bandwidth { get; set; }
        public float Envelope { get; set; }
        public StateVariableFilter AnalysisFilter { get; set; } = new();
        public StateVariableFilter SynthesisFilter { get; set; } = new();
    }
}

/// <summary>
/// State variable filter for vocoder band processing
/// </summary>
internal class StateVariableFilter
{
    private float _low, _band, _high;
    private float _f, _q;

    public void SetBandpass(float frequency, float bandwidth, int sampleRate)
    {
        _f = (float)(2 * Math.Sin(Math.PI * frequency / sampleRate));
        _q = 1f / (bandwidth / frequency);
        _f = Math.Min(_f, 1f);
    }

    public float Process(float input)
    {
        _low += _f * _band;
        _high = input - _low - _q * _band;
        _band += _f * _high;

        return _band; // Bandpass output
    }

    public void Reset()
    {
        _low = _band = _high = 0;
    }
}

/// <summary>
/// Talk box effect (simpler than full vocoder)
/// </summary>
public class TalkBox : EffectBase
{
    public float Formant1 { get; set; } = 500f;        // First formant frequency
    public float Formant2 { get; set; } = 1500f;       // Second formant frequency
    public float Q { get; set; } = 5f;                 // Resonance
    public new float Mix { get; set; } = 1f;

    private float _f1Low, _f1Band, _f1High;
    private float _f2Low, _f2Band, _f2High;

    public TalkBox(ISampleProvider source) : base(source, "TalkBox")
    {
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int sampleRate = SampleRate;
        int channels = Channels;

        float f1 = (float)(2 * Math.Sin(Math.PI * Formant1 / sampleRate));
        float f2 = (float)(2 * Math.Sin(Math.PI * Formant2 / sampleRate));
        float q = 1f / Q;

        f1 = Math.Min(f1, 1f);
        f2 = Math.Min(f2, 1f);

        for (int n = 0; n < count; n += channels)
        {
            float inputL = sourceBuffer[n];
            float inputR = channels > 1 ? sourceBuffer[n + 1] : sourceBuffer[n];
            float input = (inputL + inputR) * 0.5f;

            // First formant filter
            _f1Low += f1 * _f1Band;
            _f1High = input - _f1Low - q * _f1Band;
            _f1Band += f1 * _f1High;

            // Second formant filter
            _f2Low += f2 * _f2Band;
            _f2High = input - _f2Low - q * _f2Band;
            _f2Band += f2 * _f2High;

            // Combine formants
            float output = (_f1Band + _f2Band * 0.7f) * 0.5f;

            destBuffer[offset + n] = inputL * (1 - Mix) + output * Mix;
            if (channels > 1)
                destBuffer[offset + n + 1] = inputR * (1 - Mix) + output * Mix;
        }
    }

    /// <summary>
    /// Set vowel formants
    /// </summary>
    public void SetVowel(string vowel)
    {
        switch (vowel.ToLowerInvariant())
        {
            case "a":
                Formant1 = 800f;
                Formant2 = 1200f;
                break;
            case "e":
                Formant1 = 400f;
                Formant2 = 2200f;
                break;
            case "i":
                Formant1 = 300f;
                Formant2 = 2500f;
                break;
            case "o":
                Formant1 = 500f;
                Formant2 = 900f;
                break;
            case "u":
                Formant1 = 350f;
                Formant2 = 700f;
                break;
        }
    }
}
