//Engine License (MEL) – Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Harmonic exciter/enhancer for adding brightness and presence.

using NAudio.Wave;

namespace MusicEngine.Core.Effects;

/// <summary>
/// Harmonic exciter/enhancer for adding brightness and presence
/// </summary>
public class Exciter : EffectBase
{
    // Filter states
    private float _hpStateL1, _hpStateR1;
    private float _hsStateL1, _hsStateL2;
    private float _hsStateR1, _hsStateR2;
    private float _presStateL1, _presStateL2;
    private float _presStateR1, _presStateR2;

    // Filter coefficients
    private float _hpCoeff1;
    private float _hsCoeffA0, _hsCoeffA1, _hsCoeffA2, _hsCoeffB1, _hsCoeffB2;
    private float _presCoeffA0, _presCoeffA1, _presCoeffA2, _presCoeffB1, _presCoeffB2;

    private int _lastSampleRate = 0;
    private float _lastFreq = 0;
    private float _lastAir = 0;
    private float _lastPresence = 0;

    /// <summary>
    /// Amount of harmonic generation (0-1)
    /// </summary>
    public float Drive
    {
        get => GetParameter("Drive");
        set => SetParameter("Drive", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Highpass frequency for excitation in Hz
    /// </summary>
    public float Frequency
    {
        get => GetParameter("Frequency");
        set => SetParameter("Frequency", Math.Clamp(value, 500f, 10000f));
    }

    /// <summary>
    /// Odd vs even harmonic balance (0 = even, 1 = odd)
    /// </summary>
    public float Harmonics
    {
        get => GetParameter("Harmonics");
        set => SetParameter("Harmonics", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// High shelf boost (10kHz+) in relative amount (0-1)
    /// </summary>
    public float Air
    {
        get => GetParameter("Air");
        set => SetParameter("Air", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Mid-high boost (2-5kHz) in relative amount (0-1)
    /// </summary>
    public float Presence
    {
        get => GetParameter("Presence");
        set => SetParameter("Presence", Math.Clamp(value, 0f, 1f));
    }

    public Exciter(ISampleProvider source) : base(source, "Exciter")
    {
        RegisterParameter("Drive", 0.5f);
        RegisterParameter("Frequency", 3000f);
        RegisterParameter("Harmonics", 0.5f);
        RegisterParameter("Air", 0f);
        RegisterParameter("Presence", 0f);
        RegisterParameter("Mix", 0.3f);
    }

    private void UpdateFilters()
    {
        int sampleRate = SampleRate;
        float freq = Frequency;
        float air = Air;
        float presence = Presence;

        if (sampleRate == _lastSampleRate &&
            Math.Abs(freq - _lastFreq) < 0.1f &&
            Math.Abs(air - _lastAir) < 0.01f &&
            Math.Abs(presence - _lastPresence) < 0.01f)
            return;

        _lastSampleRate = sampleRate;
        _lastFreq = freq;
        _lastAir = air;
        _lastPresence = presence;

        // Highpass filter coefficient (1-pole)
        float hpOmega = (float)(2 * Math.PI * freq / sampleRate);
        _hpCoeff1 = (float)Math.Exp(-hpOmega);

        // High shelf at 10kHz for Air
        CalculateHighShelf(10000f, 0.7f, air * 6f, sampleRate,
            out _hsCoeffA0, out _hsCoeffA1, out _hsCoeffA2, out _hsCoeffB1, out _hsCoeffB2);

        // Peak EQ at 3.5kHz for Presence
        CalculatePeakEQ(3500f, 1.5f, presence * 6f, sampleRate,
            out _presCoeffA0, out _presCoeffA1, out _presCoeffA2, out _presCoeffB1, out _presCoeffB2);
    }

    private void CalculateHighShelf(float freq, float q, float gainDb, int sampleRate,
        out float a0, out float a1, out float a2, out float b1, out float b2)
    {
        if (Math.Abs(gainDb) < 0.01f)
        {
            a0 = 1; a1 = 0; a2 = 0; b1 = 0; b2 = 0;
            return;
        }

        float A = (float)Math.Pow(10, gainDb / 40);
        float omega = (float)(2 * Math.PI * freq / sampleRate);
        float sinOmega = (float)Math.Sin(omega);
        float cosOmega = (float)Math.Cos(omega);
        float alpha = sinOmega / (2 * q);
        float sqrtA = (float)Math.Sqrt(A);

        float norm = (A + 1) - (A - 1) * cosOmega + 2 * sqrtA * alpha;
        a0 = (A * ((A + 1) + (A - 1) * cosOmega + 2 * sqrtA * alpha)) / norm;
        a1 = (-2 * A * ((A - 1) + (A + 1) * cosOmega)) / norm;
        a2 = (A * ((A + 1) + (A - 1) * cosOmega - 2 * sqrtA * alpha)) / norm;
        b1 = (2 * ((A - 1) - (A + 1) * cosOmega)) / norm;
        b2 = ((A + 1) - (A - 1) * cosOmega - 2 * sqrtA * alpha) / norm;
    }

    private void CalculatePeakEQ(float freq, float q, float gainDb, int sampleRate,
        out float a0, out float a1, out float a2, out float b1, out float b2)
    {
        if (Math.Abs(gainDb) < 0.01f)
        {
            a0 = 1; a1 = 0; a2 = 0; b1 = 0; b2 = 0;
            return;
        }

        float A = (float)Math.Pow(10, gainDb / 40);
        float omega = (float)(2 * Math.PI * freq / sampleRate);
        float sinOmega = (float)Math.Sin(omega);
        float cosOmega = (float)Math.Cos(omega);
        float alpha = sinOmega / (2 * q);

        float norm = 1 + alpha / A;
        a0 = (1 + alpha * A) / norm;
        a1 = (-2 * cosOmega) / norm;
        a2 = (1 - alpha * A) / norm;
        b1 = (-2 * cosOmega) / norm;
        b2 = (1 - alpha / A) / norm;
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        UpdateFilters();

        int channels = Channels;
        float drive = Drive;
        float harmonics = Harmonics;
        float air = Air;
        float presence = Presence;
        float mix = Mix;

        for (int n = 0; n < count; n += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                float input = sourceBuffer[n + ch];

                // Highpass filter to isolate high frequencies for excitation
                float hpState = ch == 0 ? _hpStateL1 : _hpStateR1;
                float hp = input - hpState;
                hpState = hpState * _hpCoeff1 + input * (1 - _hpCoeff1);

                if (ch == 0) _hpStateL1 = hpState;
                else _hpStateR1 = hpState;

                // Generate harmonics using soft saturation
                float excited = GenerateHarmonics(hp * drive * 3, harmonics);

                // Mix harmonics back
                float output = input + excited * mix;

                // Apply Air (high shelf)
                if (air > 0.01f)
                {
                    float hsState1 = ch == 0 ? _hsStateL1 : _hsStateR1;
                    float hsState2 = ch == 0 ? _hsStateL2 : _hsStateR2;

                    float airOut = _hsCoeffA0 * output + _hsCoeffA1 * hsState1 + _hsCoeffA2 * hsState2
                                 - _hsCoeffB1 * hsState1 - _hsCoeffB2 * hsState2;

                    if (ch == 0)
                    {
                        _hsStateL2 = _hsStateL1;
                        _hsStateL1 = output;
                    }
                    else
                    {
                        _hsStateR2 = _hsStateR1;
                        _hsStateR1 = output;
                    }
                    output = airOut;
                }

                // Apply Presence (peak EQ)
                if (presence > 0.01f)
                {
                    float presState1 = ch == 0 ? _presStateL1 : _presStateR1;
                    float presState2 = ch == 0 ? _presStateL2 : _presStateR2;

                    float presOut = _presCoeffA0 * output + _presCoeffA1 * presState1 + _presCoeffA2 * presState2
                                  - _presCoeffB1 * presState1 - _presCoeffB2 * presState2;

                    if (ch == 0)
                    {
                        _presStateL2 = _presStateL1;
                        _presStateL1 = output;
                    }
                    else
                    {
                        _presStateR2 = _presStateR1;
                        _presStateR1 = output;
                    }
                    output = presOut;
                }

                destBuffer[offset + n + ch] = output;
            }
        }
    }

    private float GenerateHarmonics(float input, float harmonicBalance)
    {
        // Asymmetric saturation for both odd and even harmonics
        float oddHarmonics = (float)Math.Tanh(input);

        // Even harmonics through asymmetric waveshaping
        float evenHarmonics = input > 0
            ? (float)(1 - Math.Exp(-input))
            : (float)(-1 + Math.Exp(input));

        // Blend based on Harmonics parameter
        return oddHarmonics * harmonicBalance + evenHarmonics * (1 - harmonicBalance);
    }

    /// <summary>
    /// Create preset exciter configurations
    /// </summary>
    public static Exciter CreatePreset(ISampleProvider source, string presetName)
    {
        var exciter = new Exciter(source);

        switch (presetName.ToLowerInvariant())
        {
            case "subtle":
                exciter.Drive = 0.3f;
                exciter.Mix = 0.2f;
                exciter.Frequency = 4000f;
                exciter.Harmonics = 0.6f;
                exciter.Air = 0.1f;
                exciter.Presence = 0.1f;
                break;

            case "bright":
                exciter.Drive = 0.5f;
                exciter.Mix = 0.35f;
                exciter.Frequency = 3000f;
                exciter.Harmonics = 0.7f;
                exciter.Air = 0.3f;
                exciter.Presence = 0.2f;
                break;

            case "aggressive":
                exciter.Drive = 0.8f;
                exciter.Mix = 0.4f;
                exciter.Frequency = 2500f;
                exciter.Harmonics = 0.8f;
                exciter.Air = 0.2f;
                exciter.Presence = 0.4f;
                break;

            case "vocal":
                exciter.Drive = 0.4f;
                exciter.Mix = 0.25f;
                exciter.Frequency = 2000f;
                exciter.Harmonics = 0.5f;
                exciter.Air = 0.2f;
                exciter.Presence = 0.5f;
                break;

            case "air":
                exciter.Drive = 0.2f;
                exciter.Mix = 0.15f;
                exciter.Frequency = 6000f;
                exciter.Harmonics = 0.6f;
                exciter.Air = 0.6f;
                exciter.Presence = 0f;
                break;
        }

        return exciter;
    }
}
