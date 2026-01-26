// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Delay/echo effect processor.

using NAudio.Wave;
using MusicEngine.Infrastructure.Memory;

namespace MusicEngine.Core.Effects.TimeBased;

/// <summary>
/// Enhanced stereo delay effect with ping-pong, cross-feedback, and filtering.
/// Supports interpolation for smooth delay time changes.
/// </summary>
public class EnhancedDelayEffect : EffectBase
{
    private CircularBuffer[] _delayBuffers;
    private float[] _filterStates; // One-pole lowpass filter state per channel

    private const int MaxDelaySamples = 441000; // 10 seconds at 44.1kHz

    /// <summary>
    /// Creates a new enhanced delay effect
    /// </summary>
    /// <param name="source">Audio source to delay</param>
    /// <param name="name">Effect name</param>
    public EnhancedDelayEffect(ISampleProvider source, string name)
        : base(source, name)
    {
        int channels = source.WaveFormat.Channels;

        _delayBuffers = new CircularBuffer[channels];
        _filterStates = new float[channels];

        for (int i = 0; i < channels; i++)
        {
            _delayBuffers[i] = new CircularBuffer(MaxDelaySamples);
        }

        // Initialize parameters
        RegisterParameter("DelayTime", 0.25f);      // 250ms default
        RegisterParameter("Feedback", 0.4f);        // 40% feedback
        RegisterParameter("CrossFeedback", 0.0f);   // No cross-feedback (stereo only)
        RegisterParameter("Damping", 0.5f);         // Lowpass filter in feedback path
        RegisterParameter("StereoSpread", 0.0f);    // No stereo offset
        RegisterParameter("PingPong", 0.0f);        // Ping-pong mode off
        RegisterParameter("Mix", 0.5f);             // 50/50 mix
    }

    /// <summary>
    /// Delay time in seconds (0.001 - 10.0)
    /// </summary>
    public float DelayTime
    {
        get => GetParameter("DelayTime");
        set => SetParameter("DelayTime", Math.Clamp(value, 0.001f, 10f));
    }

    /// <summary>
    /// Feedback amount (0.0 - 0.95)
    /// Higher values create more repeats
    /// </summary>
    public float Feedback
    {
        get => GetParameter("Feedback");
        set => SetParameter("Feedback", Math.Clamp(value, 0f, 0.95f));
    }

    /// <summary>
    /// Cross-feedback between stereo channels (0.0 - 0.95)
    /// Only applies to stereo sources
    /// </summary>
    public float CrossFeedback
    {
        get => GetParameter("CrossFeedback");
        set => SetParameter("CrossFeedback", Math.Clamp(value, 0f, 0.95f));
    }

    /// <summary>
    /// Damping (lowpass filtering) in feedback path (0.0 - 1.0)
    /// 0.0 = no filtering, 1.0 = maximum damping (dark repeats)
    /// </summary>
    public float Damping
    {
        get => GetParameter("Damping");
        set => SetParameter("Damping", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Stereo spread - delay time offset between channels (0.0 - 1.0)
    /// 0.0 = same delay time, 1.0 = maximum offset
    /// </summary>
    public float StereoSpread
    {
        get => GetParameter("StereoSpread");
        set => SetParameter("StereoSpread", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Ping-pong mode strength (0.0 - 1.0)
    /// Creates alternating stereo delays
    /// </summary>
    public float PingPong
    {
        get => GetParameter("PingPong");
        set => SetParameter("PingPong", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Dry/Wet mix (0.0 - 1.0)
    /// Maps to Mix parameter for compatibility
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

        float delayTime = DelayTime;
        float feedback = Feedback;
        float crossFeedback = CrossFeedback;
        float damping = Damping;
        float stereoSpread = StereoSpread;
        float pingPong = PingPong;

        // Calculate delay times for each channel using pooled buffer
        float baseDelaySamples = delayTime * sampleRate;
        using var channelDelaysBuffer = AudioBufferPool.Instance.RentScoped(channels);
        var channelDelays = channelDelaysBuffer.Data;

        for (int ch = 0; ch < channels; ch++)
        {
            // Apply stereo spread
            float spreadOffset = (ch == 1 && channels == 2) ? stereoSpread * baseDelaySamples * 0.1f : 0f;
            channelDelays[ch] = baseDelaySamples + spreadOffset;
        }

        // Use pooled buffer for delayed samples
        using var delayedSamplesBuffer = AudioBufferPool.Instance.RentScoped(channels);
        var delayedSamples = delayedSamplesBuffer.Data;

        for (int i = 0; i < count; i += channels)
        {
            // Read delayed samples (with interpolation)
            for (int ch = 0; ch < channels; ch++)
            {
                delayedSamples[ch] = _delayBuffers[ch].ReadInterpolated(channelDelays[ch]);
            }

            // Process each channel
            for (int ch = 0; ch < channels; ch++)
            {
                int index = i + ch;
                float input = sourceBuffer[index];

                // Calculate feedback signal
                float feedbackSignal = delayedSamples[ch] * feedback;

                // Add cross-feedback from other channel (stereo only)
                if (channels == 2 && crossFeedback > 0f)
                {
                    int otherCh = 1 - ch;
                    feedbackSignal += delayedSamples[otherCh] * crossFeedback;
                }

                // Apply ping-pong (alternate channels)
                if (channels == 2 && pingPong > 0f)
                {
                    int otherCh = 1 - ch;
                    float pingPongSignal = delayedSamples[otherCh] * pingPong * feedback;
                    feedbackSignal = feedbackSignal * (1f - pingPong) + pingPongSignal;
                }

                // Apply damping (lowpass filter in feedback path)
                if (damping > 0f)
                {
                    float alpha = 1f - damping;
                    feedbackSignal = _filterStates[ch] + alpha * (feedbackSignal - _filterStates[ch]);
                    _filterStates[ch] = feedbackSignal;
                }

                // Write to delay buffer
                _delayBuffers[ch].Write(input + feedbackSignal);

                // Output is delayed signal (dry/wet mix applied in base class)
                destBuffer[offset + index] = delayedSamples[ch];
            }
        }
    }

    /// <summary>
    /// Circular buffer with linear interpolation for fractional delay times
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
            // Clamp delay to buffer size
            delaySamples = Math.Clamp(delaySamples, 0f, _buffer.Length - 1);

            // Calculate read position
            float readPos = _writePos - delaySamples;
            if (readPos < 0) readPos += _buffer.Length;

            // Linear interpolation between two samples
            int pos1 = (int)readPos;
            int pos2 = (pos1 + 1) % _buffer.Length;
            float frac = readPos - pos1;

            return _buffer[pos1] * (1f - frac) + _buffer[pos2] * frac;
        }
    }
}
