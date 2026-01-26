// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Tremolo/amplitude modulation effect.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Modulation;

/// <summary>
/// Tremolo effect - amplitude modulation creates rhythmic volume changes.
/// </summary>
public class TremoloEffect : EffectBase
{
    private float _lfoPhase;

    /// <summary>
    /// Creates a new tremolo effect
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <param name="name">Effect name</param>
    public TremoloEffect(ISampleProvider source, string name)
        : base(source, name)
    {
        _lfoPhase = 0f;

        // Initialize parameters
        RegisterParameter("Rate", 5f);            // 5 Hz modulation rate
        RegisterParameter("Depth", 0.5f);         // 50% depth
        RegisterParameter("Waveform", 0f);        // 0 = sine, 1 = triangle, 2 = square
        RegisterParameter("Stereo", 0f);          // Stereo phase offset
        RegisterParameter("Mix", 1.0f);           // 100% wet
    }

    /// <summary>
    /// Modulation rate in Hz (0.1 - 20.0)
    /// Controls the speed of volume changes
    /// </summary>
    public float Rate
    {
        get => GetParameter("Rate");
        set => SetParameter("Rate", Math.Clamp(value, 0.1f, 20f));
    }

    /// <summary>
    /// Modulation depth (0.0 - 1.0)
    /// 0.0 = no effect, 1.0 = full amplitude modulation
    /// </summary>
    public float Depth
    {
        get => GetParameter("Depth");
        set => SetParameter("Depth", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// LFO waveform (0 = sine, 1 = triangle, 2 = square)
    /// </summary>
    public int Waveform
    {
        get => (int)GetParameter("Waveform");
        set => SetParameter("Waveform", Math.Clamp(value, 0, 2));
    }

    /// <summary>
    /// Stereo phase offset (0.0 - 1.0)
    /// Creates stereo width by offsetting modulation between channels
    /// </summary>
    public float Stereo
    {
        get => GetParameter("Stereo");
        set => SetParameter("Stereo", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Dry/wet mix (0.0 - 1.0)
    /// Maps to Mix parameter
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

        float rate = Rate;
        float depth = Depth;
        int waveform = Waveform;
        float stereo = Stereo;

        float lfoIncrement = 2f * MathF.PI * rate / sampleRate;

        for (int i = 0; i < count; i += channels)
        {
            // Advance LFO phase
            _lfoPhase += lfoIncrement;
            if (_lfoPhase > 2f * MathF.PI)
                _lfoPhase -= 2f * MathF.PI;

            for (int ch = 0; ch < channels; ch++)
            {
                int index = i + ch;
                float input = sourceBuffer[index];

                // Calculate LFO value for this channel
                float channelPhase = _lfoPhase + (ch * stereo * MathF.PI);
                float lfo = waveform switch
                {
                    0 => MathF.Sin(channelPhase),                              // Sine
                    1 => (2f / MathF.PI) * MathF.Asin(MathF.Sin(channelPhase)), // Triangle approximation
                    2 => MathF.Sin(channelPhase) > 0f ? 1f : -1f,              // Square
                    _ => MathF.Sin(channelPhase)
                };

                // Convert LFO from -1..1 to modulation range
                float modulation = 1f - depth + depth * (lfo + 1f) * 0.5f;

                // Apply amplitude modulation
                float output = input * modulation;

                destBuffer[offset + index] = output;
            }
        }
    }
}
