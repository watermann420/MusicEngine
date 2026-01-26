// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Flanger effect processor.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Modulation;

/// <summary>
/// Flanger effect - creates a sweeping, jet-like sound by mixing the signal
/// with a delayed copy modulated by an LFO.
/// </summary>
public class FlangerEffect : EffectBase
{
    private CircularBuffer[] _delayBuffers;
    private float _lfoPhase;

    private const int MaxDelaySamples = 441; // 10ms at 44.1kHz

    /// <summary>
    /// Creates a new flanger effect
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <param name="name">Effect name</param>
    public FlangerEffect(ISampleProvider source, string name)
        : base(source, name)
    {
        int channels = source.WaveFormat.Channels;

        _delayBuffers = new CircularBuffer[channels];
        for (int i = 0; i < channels; i++)
        {
            _delayBuffers[i] = new CircularBuffer(MaxDelaySamples);
        }

        _lfoPhase = 0f;

        // Initialize parameters
        RegisterParameter("Rate", 0.5f);          // 0.5 Hz LFO rate
        RegisterParameter("Depth", 0.002f);       // 2ms modulation depth
        RegisterParameter("Feedback", 0.5f);      // 50% feedback
        RegisterParameter("BaseDelay", 0.003f);   // 3ms base delay
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
    /// Modulation depth in seconds (0.0001 - 0.01)
    /// How much the delay time varies
    /// </summary>
    public float Depth
    {
        get => GetParameter("Depth");
        set => SetParameter("Depth", Math.Clamp(value, 0.0001f, 0.01f));
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
    /// Base delay time in seconds (0.001 - 0.01)
    /// Center point of the modulation
    /// </summary>
    public float BaseDelay
    {
        get => GetParameter("BaseDelay");
        set => SetParameter("BaseDelay", Math.Clamp(value, 0.001f, 0.01f));
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
        float baseDelay = BaseDelay;
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

                // Calculate LFO value for this channel (with stereo offset)
                float channelPhase = _lfoPhase + (ch * stereo * MathF.PI);
                float lfo = MathF.Sin(channelPhase);

                // Calculate modulated delay time
                float delayTime = baseDelay + depth * lfo;
                float delaySamples = delayTime * sampleRate;
                delaySamples = Math.Clamp(delaySamples, 0f, MaxDelaySamples - 1);

                // Read delayed sample with interpolation
                float delayed = _delayBuffers[ch].ReadInterpolated(delaySamples);

                // Write to delay buffer (input + feedback)
                _delayBuffers[ch].Write(input + delayed * feedback);

                // Output is the delayed signal (dry/wet mix in base class)
                destBuffer[offset + index] = delayed;
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
