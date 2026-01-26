// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Brickwall limiter.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Dynamics;

/// <summary>
/// Limiter effect - prevents audio from exceeding a specified ceiling.
/// A limiter is essentially a compressor with a very high ratio and fast attack.
/// Includes lookahead to prevent clipping on fast transients.
/// </summary>
public class LimiterEffect : EffectBase
{
    private CircularBuffer[] _lookaheadBuffers;
    private float[] _envelope;

    private const int MaxLookaheadSamples = 2205; // 50ms at 44.1kHz

    /// <summary>
    /// Creates a new limiter effect
    /// </summary>
    /// <param name="source">Audio source to limit</param>
    /// <param name="name">Effect name</param>
    public LimiterEffect(ISampleProvider source, string name)
        : base(source, name)
    {
        int channels = source.WaveFormat.Channels;

        _lookaheadBuffers = new CircularBuffer[channels];
        _envelope = new float[channels];

        for (int i = 0; i < channels; i++)
        {
            _lookaheadBuffers[i] = new CircularBuffer(MaxLookaheadSamples);
        }

        // Initialize parameters
        RegisterParameter("Ceiling", -0.3f);      // -0.3 dB ceiling (prevents clipping)
        RegisterParameter("Release", 0.05f);      // 50ms release
        RegisterParameter("Lookahead", 0.005f);   // 5ms lookahead
        RegisterParameter("Mix", 1.0f);           // 100% wet
    }

    /// <summary>
    /// Ceiling level in dB (-12 to 0)
    /// Maximum output level - signals above this will be limited
    /// </summary>
    public float Ceiling
    {
        get => GetParameter("Ceiling");
        set => SetParameter("Ceiling", Math.Clamp(value, -12f, 0f));
    }

    /// <summary>
    /// Release time in seconds (0.001 - 1.0)
    /// How fast the limiter recovers after limiting
    /// </summary>
    public float Release
    {
        get => GetParameter("Release");
        set => SetParameter("Release", Math.Clamp(value, 0.001f, 1f));
    }

    /// <summary>
    /// Lookahead time in seconds (0 - 0.05)
    /// Allows limiter to anticipate peaks and respond smoothly
    /// </summary>
    public float Lookahead
    {
        get => GetParameter("Lookahead");
        set => SetParameter("Lookahead", Math.Clamp(value, 0f, 0.05f));
    }

    /// <summary>
    /// Dry/Wet mix (0 = dry, 1 = wet)
    /// Maps to Mix property for compatibility
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

        float ceiling = Ceiling;
        float release = Release;
        float lookahead = Lookahead;

        // Convert ceiling from dB to linear
        float ceilingLinear = MathF.Pow(10f, ceiling / 20f);

        // Calculate release coefficient
        float releaseCoeff = MathF.Exp(-1f / (release * sampleRate));

        // Calculate lookahead samples
        int lookaheadSamples = (int)(lookahead * sampleRate);
        lookaheadSamples = Math.Clamp(lookaheadSamples, 0, MaxLookaheadSamples - 1);

        for (int i = 0; i < count; i += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                float input = sourceBuffer[i + ch];

                // Write to lookahead buffer
                _lookaheadBuffers[ch].Write(input);

                // Read from lookahead buffer (delayed signal)
                float delayedSignal = _lookaheadBuffers[ch].Read(lookaheadSamples);

                // Envelope detection on current input (lookahead)
                float inputAbs = MathF.Abs(input);

                // Instant attack, release-controlled decay
                if (inputAbs > _envelope[ch])
                {
                    _envelope[ch] = inputAbs;
                }
                else
                {
                    _envelope[ch] = inputAbs + releaseCoeff * (_envelope[ch] - inputAbs);
                }

                // Calculate gain reduction (brick-wall limiting)
                float gain = 1f;
                if (_envelope[ch] > ceilingLinear)
                {
                    gain = ceilingLinear / _envelope[ch];
                }

                // Apply limiting to delayed signal
                float output = delayedSignal * gain;

                destBuffer[offset + i + ch] = output;
            }
        }
    }

    /// <summary>
    /// Simple circular buffer for lookahead
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

        public float Read(int delaySamples)
        {
            int readPos = _writePos - delaySamples - 1;
            if (readPos < 0) readPos += _buffer.Length;
            return _buffer[readPos];
        }
    }
}
