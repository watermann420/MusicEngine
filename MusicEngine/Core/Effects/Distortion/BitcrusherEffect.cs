// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Bit crusher/sample rate reducer.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Distortion;

/// <summary>
/// Bitcrusher effect - reduces bit depth and sample rate for lo-fi, retro sounds.
/// </summary>
public class BitcrusherEffect : EffectBase
{
    private float _holdSample;
    private int _holdCounter;

    /// <summary>
    /// Creates a new bitcrusher effect
    /// </summary>
    /// <param name="source">Audio source to crush</param>
    /// <param name="name">Effect name</param>
    public BitcrusherEffect(ISampleProvider source, string name)
        : base(source, name)
    {
        _holdSample = 0f;
        _holdCounter = 0;

        // Initialize parameters
        RegisterParameter("BitDepth", 16f);       // 16-bit (no crushing)
        RegisterParameter("TargetSampleRate", 44100f);  // Full sample rate
        RegisterParameter("Mix", 1.0f);        // 100% wet
    }

    /// <summary>
    /// Bit depth (1 - 16)
    /// Lower values create more distortion
    /// 1 = extreme lo-fi, 16 = no bit reduction
    /// </summary>
    public float BitDepth
    {
        get => GetParameter("BitDepth");
        set => SetParameter("BitDepth", Math.Clamp(value, 1f, 16f));
    }

    /// <summary>
    /// Target sample rate in Hz (100 - 48000)
    /// Lower values create aliasing and metallic artifacts
    /// </summary>
    public float TargetSampleRate
    {
        get => GetParameter("TargetSampleRate");
        set => SetParameter("TargetSampleRate", Math.Clamp(value, 100f, 48000f));
    }

    /// <summary>
    /// Dry/Wet mix (0.0 - 1.0)
    /// 0.0 = dry (no effect), 1.0 = wet (full effect)
    /// </summary>
    public float DryWet
    {
        get => Mix;
        set => Mix = value;
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;
        int sampleRate = SampleRate;

        float bitDepth = BitDepth;
        float targetSampleRate = TargetSampleRate;

        // Calculate bit reduction
        float levels = MathF.Pow(2f, bitDepth);
        float step = 2f / levels;

        // Calculate sample rate reduction
        int downsampleFactor = (int)(sampleRate / targetSampleRate);
        downsampleFactor = Math.Max(1, downsampleFactor);

        for (int i = 0; i < count; i += channels)
        {
            // Sample rate reduction (sample and hold)
            if (_holdCounter <= 0)
            {
                _holdCounter = downsampleFactor;

                // Process each channel
                for (int ch = 0; ch < channels; ch++)
                {
                    float input = sourceBuffer[i + ch];

                    // Bit depth reduction
                    float crushed = MathF.Round(input / step) * step;

                    _holdSample = crushed;
                }
            }
            else
            {
                _holdCounter--;
            }

            // Apply held sample to all channels
            for (int ch = 0; ch < channels; ch++)
            {
                destBuffer[offset + i + ch] = _holdSample;
            }
        }
    }
}
