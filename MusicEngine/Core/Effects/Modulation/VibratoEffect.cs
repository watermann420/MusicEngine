// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Vibrato/pitch modulation effect.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Modulation;

/// <summary>
/// Vibrato effect - pitch modulation creates wavering pitch changes.
/// Implemented using a modulated delay line.
/// </summary>
public class VibratoEffect : EffectBase
{
    private CircularBuffer[] _delayBuffers;
    private float _lfoPhase;

    private const int MaxDelaySamples = 441; // 10ms at 44.1kHz

    /// <summary>
    /// Creates a new vibrato effect
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <param name="name">Effect name</param>
    public VibratoEffect(ISampleProvider source, string name)
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
        RegisterParameter("Rate", 5f);            // 5 Hz modulation rate
        RegisterParameter("Depth", 0.002f);       // 2ms modulation depth (pitch variation)
        RegisterParameter("BaseDelay", 0.003f);   // 3ms base delay
        RegisterParameter("Waveform", 0f);        // 0 = sine, 1 = triangle
        RegisterParameter("Mix", 1.0f);           // 100% wet (pure vibrato)
    }

    /// <summary>
    /// Modulation rate in Hz (1.0 - 14.0)
    /// Controls the speed of pitch changes
    /// </summary>
    public float Rate
    {
        get => GetParameter("Rate");
        set => SetParameter("Rate", Math.Clamp(value, 1f, 14f));
    }

    /// <summary>
    /// Modulation depth in seconds (0.0001 - 0.005)
    /// Controls the amount of pitch variation
    /// </summary>
    public float Depth
    {
        get => GetParameter("Depth");
        set => SetParameter("Depth", Math.Clamp(value, 0.0001f, 0.005f));
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
    /// LFO waveform (0 = sine, 1 = triangle)
    /// </summary>
    public int Waveform
    {
        get => (int)GetParameter("Waveform");
        set => SetParameter("Waveform", Math.Clamp(value, 0, 1));
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
        int waveform = Waveform;

        float lfoIncrement = 2f * MathF.PI * rate / sampleRate;

        for (int i = 0; i < count; i += channels)
        {
            // Advance LFO phase
            _lfoPhase += lfoIncrement;
            if (_lfoPhase > 2f * MathF.PI)
                _lfoPhase -= 2f * MathF.PI;

            // Calculate LFO value
            float lfo = waveform switch
            {
                0 => MathF.Sin(_lfoPhase),                              // Sine
                1 => (2f / MathF.PI) * MathF.Asin(MathF.Sin(_lfoPhase)), // Triangle approximation
                _ => MathF.Sin(_lfoPhase)
            };

            for (int ch = 0; ch < channels; ch++)
            {
                int index = i + ch;
                float input = sourceBuffer[index];

                // Calculate modulated delay time
                float delayTime = baseDelay + depth * lfo;
                float delaySamples = delayTime * sampleRate;
                delaySamples = Math.Clamp(delaySamples, 0f, MaxDelaySamples - 1);

                // Write input to delay buffer
                _delayBuffers[ch].Write(input);

                // Read delayed sample with interpolation (creates pitch shift)
                float output = _delayBuffers[ch].ReadInterpolated(delaySamples);

                destBuffer[offset + index] = output;
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
