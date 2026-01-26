// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Chorus effect processor.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Modulation;

/// <summary>
/// Enhanced chorus effect with multiple voices and stereo spreading.
/// Creates a rich, ensemble sound by layering multiple modulated delays.
/// </summary>
public class EnhancedChorusEffect : EffectBase
{
    private CircularBuffer[] _delayBuffers;
    private float[] _lfoPhases; // One LFO phase per voice

    private const int MaxDelaySamples = 2205; // 50ms at 44.1kHz
    private const int MaxVoices = 4;

    /// <summary>
    /// Creates a new enhanced chorus effect
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <param name="name">Effect name</param>
    public EnhancedChorusEffect(ISampleProvider source, string name)
        : base(source, name)
    {
        int channels = source.WaveFormat.Channels;

        _delayBuffers = new CircularBuffer[channels];
        for (int i = 0; i < channels; i++)
        {
            _delayBuffers[i] = new CircularBuffer(MaxDelaySamples);
        }

        _lfoPhases = new float[MaxVoices];

        // Initialize parameters
        RegisterParameter("Rate", 0.8f);          // 0.8 Hz LFO rate
        RegisterParameter("Depth", 0.003f);       // 3ms modulation depth
        RegisterParameter("BaseDelay", 0.02f);    // 20ms base delay
        RegisterParameter("Voices", 3f);          // 3 chorus voices
        RegisterParameter("Spread", 0.5f);        // 50% voice spread
        RegisterParameter("Feedback", 0.2f);      // 20% feedback
        RegisterParameter("Mix", 0.5f);           // 50/50 mix
    }

    /// <summary>
    /// LFO rate in Hz (0.01 - 5.0)
    /// Controls the speed of the chorus modulation
    /// </summary>
    public float Rate
    {
        get => GetParameter("Rate");
        set => SetParameter("Rate", Math.Clamp(value, 0.01f, 5f));
    }

    /// <summary>
    /// Modulation depth in seconds (0.001 - 0.01)
    /// How much the delay time varies
    /// </summary>
    public float Depth
    {
        get => GetParameter("Depth");
        set => SetParameter("Depth", Math.Clamp(value, 0.001f, 0.01f));
    }

    /// <summary>
    /// Base delay time in seconds (0.01 - 0.05)
    /// Center point of the modulation
    /// </summary>
    public float BaseDelay
    {
        get => GetParameter("BaseDelay");
        set => SetParameter("BaseDelay", Math.Clamp(value, 0.01f, 0.05f));
    }

    /// <summary>
    /// Number of chorus voices (1 - 4)
    /// More voices create a richer sound
    /// </summary>
    public int Voices
    {
        get => (int)GetParameter("Voices");
        set => SetParameter("Voices", Math.Clamp(value, 1, MaxVoices));
    }

    /// <summary>
    /// Voice spread (0.0 - 1.0)
    /// Distributes voices across stereo field and frequency
    /// </summary>
    public float Spread
    {
        get => GetParameter("Spread");
        set => SetParameter("Spread", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Feedback amount (0.0 - 0.5)
    /// Adds depth to the chorus effect
    /// </summary>
    public float Feedback
    {
        get => GetParameter("Feedback");
        set => SetParameter("Feedback", Math.Clamp(value, 0f, 0.5f));
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
        float baseDelay = BaseDelay;
        int voices = Voices;
        float spread = Spread;
        float feedback = Feedback;

        float lfoIncrement = 2f * MathF.PI * rate / sampleRate;

        for (int i = 0; i < count; i += channels)
        {
            // Advance all LFO phases
            for (int v = 0; v < voices; v++)
            {
                _lfoPhases[v] += lfoIncrement;
                if (_lfoPhases[v] > 2f * MathF.PI)
                    _lfoPhases[v] -= 2f * MathF.PI;
            }

            for (int ch = 0; ch < channels; ch++)
            {
                int index = i + ch;
                float input = sourceBuffer[index];

                // Mix all chorus voices
                float chorusOutput = 0f;
                for (int v = 0; v < voices; v++)
                {
                    // Phase offset for each voice (creates richness)
                    float phaseOffset = (v * 2f * MathF.PI / voices) * spread;

                    // Stereo panning for each voice
                    float pan = (v / (float)(voices - 1)) - 0.5f; // -0.5 to 0.5
                    float voiceGain = 1f;
                    if (channels == 2)
                    {
                        // Apply stereo panning
                        voiceGain = ch == 0 ? (1f - pan * spread) : (1f + pan * spread);
                    }

                    // Calculate LFO value for this voice
                    float lfo = MathF.Sin(_lfoPhases[v] + phaseOffset);

                    // Calculate modulated delay time with voice-specific offset
                    float voiceDelay = baseDelay + depth * lfo + (v * 0.002f * spread);
                    float delaySamples = voiceDelay * sampleRate;
                    delaySamples = Math.Clamp(delaySamples, 0f, MaxDelaySamples - 1);

                    // Read delayed sample
                    float delayed = _delayBuffers[ch].ReadInterpolated(delaySamples);

                    // Add to chorus output with voice gain
                    chorusOutput += delayed * voiceGain / voices;
                }

                // Write to delay buffer (input + feedback)
                _delayBuffers[ch].Write(input + chorusOutput * feedback);

                // Output is the chorus signal (dry/wet mix in base class)
                destBuffer[offset + index] = chorusOutput;
            }
        }
    }

    /// <summary>
    /// Circular buffer with linear interpolation
    /// </summary>
    private class CircularBuffer
    {
        private readonly float[] _buffer;
        private int _writePos;

        public CircularBuffer(int size)
        {
            _buffer = new float[size];
            _writePos = 0;
        }

        public void Write(float sample)
        {
            _buffer[_writePos] = sample;
            _writePos = (_writePos + 1) % _buffer.Length;
        }

        public float ReadInterpolated(float delaySamples)
        {
            float readPos = _writePos - delaySamples;
            if (readPos < 0) readPos += _buffer.Length;

            int pos1 = (int)readPos;
            int pos2 = (pos1 + 1) % _buffer.Length;
            float frac = readPos - pos1;

            return _buffer[pos1] * (1f - frac) + _buffer[pos2] * frac;
        }
    }
}
