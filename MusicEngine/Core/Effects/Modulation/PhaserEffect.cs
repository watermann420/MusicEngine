// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Phaser effect processor.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Modulation;

/// <summary>
/// Phaser effect - creates a sweeping sound by modulating a series of
/// all-pass filters with an LFO.
/// </summary>
public class PhaserEffect : EffectBase
{
    private AllpassFilter[][] _allpassFilters; // [channel][stage]
    private float _lfoPhase;

    private const int NumStages = 6; // Number of allpass filter stages

    /// <summary>
    /// Creates a new phaser effect
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <param name="name">Effect name</param>
    public PhaserEffect(ISampleProvider source, string name)
        : base(source, name)
    {
        int channels = source.WaveFormat.Channels;

        _allpassFilters = new AllpassFilter[channels][];
        for (int ch = 0; ch < channels; ch++)
        {
            _allpassFilters[ch] = new AllpassFilter[NumStages];
            for (int stage = 0; stage < NumStages; stage++)
            {
                _allpassFilters[ch][stage] = new AllpassFilter();
            }
        }

        _lfoPhase = 0f;

        // Initialize parameters
        RegisterParameter("Rate", 0.5f);          // 0.5 Hz LFO rate
        RegisterParameter("Depth", 1.0f);         // 100% modulation depth
        RegisterParameter("Feedback", 0.7f);      // 70% feedback
        RegisterParameter("MinFreq", 200f);       // 200 Hz minimum frequency
        RegisterParameter("MaxFreq", 2000f);      // 2000 Hz maximum frequency
        RegisterParameter("Stages", 6f);          // 6 allpass stages
        RegisterParameter("Stereo", 0.5f);        // 50% stereo phase offset
        RegisterParameter("Mix", 0.5f);           // 50/50 mix
    }

    /// <summary>
    /// LFO rate in Hz (0.01 - 10.0)
    /// Controls the speed of the sweeping effect
    /// </summary>
    public float Rate
    {
        get => GetParameter("Rate");
        set => SetParameter("Rate", Math.Clamp(value, 0.01f, 10f));
    }

    /// <summary>
    /// Modulation depth (0.0 - 1.0)
    /// How much the filter frequency varies
    /// </summary>
    public float Depth
    {
        get => GetParameter("Depth");
        set => SetParameter("Depth", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Feedback amount (0.0 - 0.95)
    /// Creates resonance and intensity
    /// </summary>
    public float Feedback
    {
        get => GetParameter("Feedback");
        set => SetParameter("Feedback", Math.Clamp(value, 0f, 0.95f));
    }

    /// <summary>
    /// Minimum filter frequency in Hz (20 - 5000)
    /// Lower bound of the sweep range
    /// </summary>
    public float MinFrequency
    {
        get => GetParameter("MinFreq");
        set => SetParameter("MinFreq", Math.Clamp(value, 20f, 5000f));
    }

    /// <summary>
    /// Maximum filter frequency in Hz (100 - 10000)
    /// Upper bound of the sweep range
    /// </summary>
    public float MaxFrequency
    {
        get => GetParameter("MaxFreq");
        set => SetParameter("MaxFreq", Math.Clamp(value, 100f, 10000f));
    }

    /// <summary>
    /// Number of allpass filter stages (2 - 12)
    /// More stages create a more dramatic effect
    /// </summary>
    public int Stages
    {
        get => (int)GetParameter("Stages");
        set => SetParameter("Stages", Math.Clamp(value, 2, 12));
    }

    /// <summary>
    /// Stereo phase offset (0.0 - 1.0)
    /// Creates stereo width by offsetting LFO phase between channels
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
        float feedback = Feedback;
        float minFreq = MinFrequency;
        float maxFreq = MaxFrequency;
        int stages = Stages;
        float stereo = Stereo;

        float lfoIncrement = 2f * MathF.PI * rate / sampleRate;
        float freqRange = maxFreq - minFreq;

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

                // Calculate LFO value for this channel (with stereo offset)
                float channelPhase = _lfoPhase + (ch * stereo * MathF.PI);
                float lfo = (MathF.Sin(channelPhase) + 1f) * 0.5f; // 0.0 to 1.0

                // Calculate modulated frequency
                float frequency = minFreq + lfo * depth * freqRange;

                // Process through allpass filter cascade
                float output = input;
                for (int stage = 0; stage < stages && stage < NumStages; stage++)
                {
                    output = _allpassFilters[ch][stage].Process(output, frequency, sampleRate);
                }

                // Add feedback
                output = input + output * feedback;

                destBuffer[offset + index] = output;
            }
        }
    }

    /// <summary>
    /// First-order allpass filter
    /// </summary>
    private class AllpassFilter
    {
        private float _a1;
        private float _x1, _y1;

        public float Process(float input, float frequency, int sampleRate)
        {
            // Calculate allpass coefficient
            float tan = MathF.Tan(MathF.PI * frequency / sampleRate);
            _a1 = (tan - 1f) / (tan + 1f);

            // Allpass filter (Direct Form I)
            float output = _a1 * input + _x1 - _a1 * _y1;
            _x1 = input;
            _y1 = output;

            return output;
        }
    }
}
