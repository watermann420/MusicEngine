// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio effect processor.

using NAudio.Wave;

namespace MusicEngine.Core.Effects;

/// <summary>
/// Stereo widening effect using M/S processing and Haas effect
/// </summary>
public class StereoWidener : EffectBase
{
    // Haas delay buffer
    private float[] _delayBufferL = Array.Empty<float>();
    private float[] _delayBufferR = Array.Empty<float>();
    private int _delayIndex = 0;
    private int _delaySamples = 0;

    // Low crossover filter states
    private float _lpStateL, _lpStateR;

    // Correlation metering
    public float Correlation { get; private set; } = 1f;
    private float _correlationSum = 0;
    private float _powerSumL = 0;
    private float _powerSumR = 0;
    private int _correlationCount = 0;

    /// <summary>
    /// Stereo width (0=mono, 1=normal, 2=wide)
    /// </summary>
    public float Width
    {
        get => GetParameter("Width");
        set => SetParameter("Width", Math.Clamp(value, 0f, 2f));
    }

    /// <summary>
    /// Haas delay in milliseconds (0-30)
    /// </summary>
    public float HaasDelay
    {
        get => GetParameter("HaasDelay");
        set => SetParameter("HaasDelay", Math.Clamp(value, 0f, 30f));
    }

    /// <summary>
    /// Amount of Haas effect (0-1)
    /// </summary>
    public float HaasMix
    {
        get => GetParameter("HaasMix");
        set => SetParameter("HaasMix", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Width for low frequencies (0-1, mono bass)
    /// </summary>
    public float LowWidth
    {
        get => GetParameter("LowWidth");
        set => SetParameter("LowWidth", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Crossover frequency for low mono (Hz)
    /// </summary>
    public float CrossoverFreq
    {
        get => GetParameter("CrossoverFreq");
        set => SetParameter("CrossoverFreq", Math.Clamp(value, 50f, 500f));
    }

    /// <summary>
    /// Ensure mono compatibility
    /// </summary>
    public bool MonoCompatible { get; set; } = true;

    /// <summary>
    /// Minimum correlation to maintain
    /// </summary>
    public float MaxCorrelation { get; set; } = -0.5f;

    public StereoWidener(ISampleProvider source) : base(source, "Stereo Widener")
    {
        RegisterParameter("Width", 1f);
        RegisterParameter("HaasDelay", 0f);
        RegisterParameter("HaasMix", 0f);
        RegisterParameter("LowWidth", 1f);
        RegisterParameter("CrossoverFreq", 200f);
        RegisterParameter("Mix", 1f);
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;
        if (channels < 2)
        {
            // Mono input - just copy
            Array.Copy(sourceBuffer, 0, destBuffer, offset, count);
            return;
        }

        int sampleRate = SampleRate;

        float width = Width;
        float haasDelay = HaasDelay;
        float haasMix = HaasMix;
        float lowWidth = LowWidth;
        float crossoverFreq = CrossoverFreq;

        // Update Haas delay buffer if needed
        int newDelaySamples = (int)(haasDelay * sampleRate / 1000);
        if (newDelaySamples != _delaySamples && newDelaySamples > 0)
        {
            _delaySamples = newDelaySamples;
            _delayBufferL = new float[_delaySamples];
            _delayBufferR = new float[_delaySamples];
            _delayIndex = 0;
        }

        float lpCoeff = (float)Math.Exp(-2 * Math.PI * crossoverFreq / sampleRate);

        for (int n = 0; n < count; n += channels)
        {
            float inputL = sourceBuffer[n];
            float inputR = sourceBuffer[n + 1];

            // Split into low and high frequencies
            _lpStateL = _lpStateL * lpCoeff + inputL * (1 - lpCoeff);
            _lpStateR = _lpStateR * lpCoeff + inputR * (1 - lpCoeff);

            float lowL = _lpStateL;
            float lowR = _lpStateR;
            float highL = inputL - lowL;
            float highR = inputR - lowR;

            // Process low frequencies (optionally mono)
            float lowMid = (lowL + lowR) * 0.5f;
            float lowSide = (lowL - lowR) * 0.5f;

            // Apply low width (1 = normal, 0 = mono bass)
            lowSide *= lowWidth;

            float processedLowL = lowMid + lowSide;
            float processedLowR = lowMid - lowSide;

            // Convert high frequencies to M/S
            float mid = (highL + highR) * 0.5f;
            float side = (highL - highR) * 0.5f;

            // Apply stereo width to side signal
            side *= width;

            // Convert back to L/R
            float processedHighL = mid + side;
            float processedHighR = mid - side;

            // Combine low and high
            float outputL = processedLowL + processedHighL;
            float outputR = processedLowR + processedHighR;

            // Apply Haas effect
            if (haasMix > 0 && _delaySamples > 0)
            {
                float delayedL = _delayBufferL[_delayIndex];
                float delayedR = _delayBufferR[_delayIndex];

                _delayBufferL[_delayIndex] = outputR; // Cross-feed
                _delayBufferR[_delayIndex] = outputL;

                _delayIndex = (_delayIndex + 1) % _delaySamples;

                // Add delayed opposite channel for width
                outputL += delayedL * haasMix * 0.5f;
                outputR += delayedR * haasMix * 0.5f;
            }

            // Check mono compatibility
            if (MonoCompatible)
            {
                // Update correlation measurement
                _correlationSum += outputL * outputR;
                _powerSumL += outputL * outputL;
                _powerSumR += outputR * outputR;
                _correlationCount++;

                if (_correlationCount >= sampleRate / 10) // Update 10 times per second
                {
                    float power = (float)Math.Sqrt(_powerSumL * _powerSumR);
                    Correlation = power > 0.0001f ? _correlationSum / power : 1f;

                    _correlationSum = 0;
                    _powerSumL = 0;
                    _powerSumR = 0;
                    _correlationCount = 0;
                }

                // If correlation is too negative, reduce width
                if (Correlation < MaxCorrelation)
                {
                    float reduction = (MaxCorrelation - Correlation) / (1 - MaxCorrelation);
                    float safeWidth = 1f - reduction * 0.5f;

                    mid = (outputL + outputR) * 0.5f;
                    side = (outputL - outputR) * 0.5f;
                    side *= safeWidth;

                    outputL = mid + side;
                    outputR = mid - side;
                }
            }

            destBuffer[offset + n] = outputL;
            destBuffer[offset + n + 1] = outputR;
        }
    }

    /// <summary>
    /// Create preset configurations
    /// </summary>
    public static StereoWidener CreatePreset(ISampleProvider source, string presetName)
    {
        var widener = new StereoWidener(source);

        switch (presetName.ToLowerInvariant())
        {
            case "subtle":
                widener.Width = 1.2f;
                widener.LowWidth = 0.8f;
                widener.CrossoverFreq = 150f;
                break;

            case "wide":
                widener.Width = 1.5f;
                widener.LowWidth = 0.5f;
                widener.CrossoverFreq = 200f;
                break;

            case "superwide":
                widener.Width = 2f;
                widener.LowWidth = 0.3f;
                widener.CrossoverFreq = 250f;
                widener.HaasDelay = 10f;
                widener.HaasMix = 0.2f;
                break;

            case "monobass":
                widener.Width = 1f;
                widener.LowWidth = 0f;
                widener.CrossoverFreq = 150f;
                break;

            case "haas":
                widener.Width = 1.2f;
                widener.HaasDelay = 20f;
                widener.HaasMix = 0.4f;
                widener.LowWidth = 0.7f;
                break;

            case "narrow":
                widener.Width = 0.5f;
                widener.LowWidth = 1f;
                break;

            case "mono":
                widener.Width = 0f;
                widener.LowWidth = 0f;
                break;
        }

        return widener;
    }
}
