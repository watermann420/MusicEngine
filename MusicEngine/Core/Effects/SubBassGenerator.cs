// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio effect processor.

using NAudio.Wave;

namespace MusicEngine.Core.Effects;

/// <summary>
/// Sub-harmonic generation mode
/// </summary>
public enum SubBassMode
{
    /// <summary>One octave below (-12 semitones)</summary>
    Octave1,
    /// <summary>Two octaves below (-24 semitones)</summary>
    Octave2,
    /// <summary>Fifth below (-7 semitones)</summary>
    Fifth,
    /// <summary>Both octaves combined</summary>
    DualOctave
}

/// <summary>
/// Sub Bass Generator effect that synthesizes sub-harmonics from the input signal.
/// Creates powerful low-end by generating frequencies one or two octaves below the input.
/// </summary>
public class SubBassGenerator : EffectBase
{
    // Envelope follower state
    private float _envelopeL, _envelopeR;

    // Sub oscillator state
    private double _phaseL, _phaseR;
    private float _lastInputL, _lastInputR;
    private int _zeroCrossCountL, _zeroCrossCountR;
    private double _detectedFreqL, _detectedFreqR;
    private int _samplesSinceZeroCrossL, _samplesSinceZeroCrossR;

    // Lowpass filter for sub output
    private float _lpStateL, _lpStateR;

    /// <summary>Sub bass generation mode</summary>
    public SubBassMode Mode { get; set; } = SubBassMode.Octave1;

    /// <summary>Sub bass level (0-1)</summary>
    public float SubLevel { get; set; } = 0.5f;

    /// <summary>Original signal level (0-1)</summary>
    public float DryLevel { get; set; } = 1.0f;

    /// <summary>Lowpass filter frequency for sub output (20-200 Hz)</summary>
    public float LowpassFrequency { get; set; } = 80f;

    /// <summary>Attack time for envelope follower in ms</summary>
    public float Attack { get; set; } = 10f;

    /// <summary>Release time for envelope follower in ms</summary>
    public float Release { get; set; } = 100f;

    /// <summary>Threshold for sub generation (0-1)</summary>
    public float Threshold { get; set; } = 0.1f;

    /// <summary>Drive/saturation for sub signal (1-5)</summary>
    public float Drive { get; set; } = 1.0f;

    public SubBassGenerator(ISampleProvider source) : base(source, "SubBassGenerator")
    {
        RegisterParameter("SubLevel", 0.5f);
        RegisterParameter("DryLevel", 1.0f);
        RegisterParameter("LowpassFrequency", 80f);
        RegisterParameter("Threshold", 0.1f);
        RegisterParameter("Drive", 1.0f);
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;
        int sampleRate = SampleRate;

        // Calculate envelope follower coefficients
        float attackCoeff = MathF.Exp(-1f / (Attack * 0.001f * sampleRate));
        float releaseCoeff = MathF.Exp(-1f / (Release * 0.001f * sampleRate));

        // Lowpass coefficient for sub signal
        float lpCoeff = MathF.Exp(-2f * MathF.PI * LowpassFrequency / sampleRate);

        for (int n = 0; n < count; n += channels)
        {
            float inputL = sourceBuffer[n];
            float inputR = channels > 1 ? sourceBuffer[n + 1] : inputL;

            // Envelope follower
            float absL = MathF.Abs(inputL);
            float absR = MathF.Abs(inputR);

            _envelopeL = absL > _envelopeL
                ? absL + attackCoeff * (_envelopeL - absL)
                : absL + releaseCoeff * (_envelopeL - absL);

            _envelopeR = absR > _envelopeR
                ? absR + attackCoeff * (_envelopeR - absR)
                : absR + releaseCoeff * (_envelopeR - absR);

            // Zero-crossing detection for pitch tracking
            DetectPitch(inputL, ref _lastInputL, ref _zeroCrossCountL,
                       ref _samplesSinceZeroCrossL, ref _detectedFreqL, sampleRate);
            DetectPitch(inputR, ref _lastInputR, ref _zeroCrossCountR,
                       ref _samplesSinceZeroCrossR, ref _detectedFreqR, sampleRate);

            // Generate sub oscillator
            float subL = GenerateSubOscillator(ref _phaseL, _detectedFreqL, sampleRate);
            float subR = GenerateSubOscillator(ref _phaseR, _detectedFreqR, sampleRate);

            // Apply envelope to sub
            float gateL = _envelopeL > Threshold ? 1f : _envelopeL / Threshold;
            float gateR = _envelopeR > Threshold ? 1f : _envelopeR / Threshold;

            subL *= _envelopeL * gateL;
            subR *= _envelopeR * gateR;

            // Apply drive/saturation
            if (Drive > 1f)
            {
                subL = MathF.Tanh(subL * Drive) / MathF.Tanh(Drive);
                subR = MathF.Tanh(subR * Drive) / MathF.Tanh(Drive);
            }

            // Lowpass filter the sub signal
            _lpStateL = subL + lpCoeff * (_lpStateL - subL);
            _lpStateR = subR + lpCoeff * (_lpStateR - subR);

            subL = _lpStateL;
            subR = _lpStateR;

            // Mix dry and sub
            float outL = inputL * DryLevel + subL * SubLevel;
            float outR = inputR * DryLevel + subR * SubLevel;

            destBuffer[offset + n] = outL;
            if (channels > 1)
                destBuffer[offset + n + 1] = outR;
        }
    }

    private void DetectPitch(float input, ref float lastInput, ref int zeroCrossCount,
                             ref int samplesSinceZeroCross, ref double detectedFreq, int sampleRate)
    {
        samplesSinceZeroCross++;

        // Zero-crossing detection
        if ((lastInput < 0 && input >= 0) || (lastInput > 0 && input <= 0))
        {
            zeroCrossCount++;

            // After 2 zero crossings, we have one cycle
            if (zeroCrossCount >= 2)
            {
                // Calculate frequency from period
                if (samplesSinceZeroCross > 0)
                {
                    double freq = (double)sampleRate / samplesSinceZeroCross;
                    // Smooth the frequency detection
                    detectedFreq = detectedFreq * 0.9 + freq * 0.1;
                    // Clamp to reasonable range
                    detectedFreq = Math.Clamp(detectedFreq, 20, 2000);
                }
                zeroCrossCount = 0;
                samplesSinceZeroCross = 0;
            }
        }

        lastInput = input;
    }

    private float GenerateSubOscillator(ref double phase, double detectedFreq, int sampleRate)
    {
        if (detectedFreq < 20) return 0f;

        // Calculate sub frequency based on mode
        double subFreq = Mode switch
        {
            SubBassMode.Octave1 => detectedFreq / 2,
            SubBassMode.Octave2 => detectedFreq / 4,
            SubBassMode.Fifth => detectedFreq / 1.5,
            SubBassMode.DualOctave => detectedFreq / 2, // Will add second later
            _ => detectedFreq / 2
        };

        // Generate sine wave for clean sub
        double phaseIncrement = subFreq / sampleRate;
        phase += phaseIncrement;
        if (phase >= 1.0) phase -= 1.0;

        float output = MathF.Sin((float)(phase * 2 * Math.PI));

        // For dual octave mode, add second octave
        if (Mode == SubBassMode.DualOctave)
        {
            double phase2 = phase * 0.5; // One more octave down
            output = output * 0.7f + MathF.Sin((float)(phase2 * 2 * Math.PI)) * 0.3f;
        }

        return output;
    }

    protected override void OnParameterChanged(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "sublevel": SubLevel = Math.Clamp(value, 0f, 2f); break;
            case "drylevel": DryLevel = Math.Clamp(value, 0f, 2f); break;
            case "lowpassfrequency": LowpassFrequency = Math.Clamp(value, 20f, 200f); break;
            case "threshold": Threshold = Math.Clamp(value, 0f, 1f); break;
            case "drive": Drive = Math.Clamp(value, 1f, 5f); break;
        }
    }
}
