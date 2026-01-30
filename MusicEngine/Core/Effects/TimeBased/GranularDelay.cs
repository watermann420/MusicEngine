// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Granular delay effect combining delay with granular synthesis processing.

using NAudio.Wave;
using MusicEngine.Infrastructure.Memory;

namespace MusicEngine.Core.Effects.TimeBased;

/// <summary>
/// Grain envelope shapes for granular delay processing.
/// </summary>
public enum GranularDelayEnvelope
{
    /// <summary>Gaussian bell curve (smooth)</summary>
    Gaussian,
    /// <summary>Hann window (smooth)</summary>
    Hann,
    /// <summary>Triangle</summary>
    Triangle,
    /// <summary>Trapezoid with attack/release</summary>
    Trapezoid
}

/// <summary>
/// Granular delay effect that processes delayed audio through granular synthesis.
/// Creates evolving, textural delays by splitting the delayed signal into grains
/// with adjustable size, density, pitch, and scatter parameters.
/// </summary>
public class GranularDelay : EffectBase
{
    private readonly CircularDelayBuffer[] _delayBuffers;
    private readonly List<DelayGrain>[] _grains;
    private readonly Random _random;
    private readonly object _lock = new();

    private double[] _timeSinceLastGrain;
    private const int MaxDelaySamples = 441000; // 10 seconds at 44.1kHz
    private const int MaxGrainsPerChannel = 64;

    /// <summary>
    /// Creates a new granular delay effect.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <param name="name">Effect name</param>
    public GranularDelay(ISampleProvider source, string name = "Granular Delay")
        : base(source, name)
    {
        int channels = source.WaveFormat.Channels;

        _delayBuffers = new CircularDelayBuffer[channels];
        _grains = new List<DelayGrain>[channels];
        _timeSinceLastGrain = new double[channels];
        _random = new Random();

        for (int i = 0; i < channels; i++)
        {
            _delayBuffers[i] = new CircularDelayBuffer(MaxDelaySamples);
            _grains[i] = new List<DelayGrain>();
            _timeSinceLastGrain[i] = 0;
        }

        // Initialize parameters with defaults
        RegisterParameter("DelayTime", 0.3f);       // 300ms default
        RegisterParameter("GrainSize", 50f);        // 50ms grain size
        RegisterParameter("GrainDensity", 30f);     // 30 grains per second
        RegisterParameter("Pitch", 0f);             // No pitch shift (semitones)
        RegisterParameter("Scatter", 0.1f);         // 10% position scatter
        RegisterParameter("Feedback", 0.3f);        // 30% feedback
        RegisterParameter("Mix", 0.5f);             // 50/50 dry/wet
        RegisterParameter("PitchRandom", 0f);       // No pitch randomization
        RegisterParameter("PanSpread", 0.3f);       // Stereo spread
        RegisterParameter("Envelope", 0f);          // Gaussian envelope
        RegisterParameter("Reverse", 0f);           // No reverse grains
        RegisterParameter("Freeze", 0f);            // Freeze mode off
    }

    /// <summary>
    /// Delay time in seconds (0.01 - 10.0).
    /// </summary>
    public float DelayTime
    {
        get => GetParameter("DelayTime");
        set => SetParameter("DelayTime", Math.Clamp(value, 0.01f, 10f));
    }

    /// <summary>
    /// Grain size in milliseconds (5 - 500).
    /// Smaller grains create more textural effects, larger grains are more recognizable.
    /// </summary>
    public float GrainSize
    {
        get => GetParameter("GrainSize");
        set => SetParameter("GrainSize", Math.Clamp(value, 5f, 500f));
    }

    /// <summary>
    /// Grain density in grains per second (1 - 200).
    /// Higher density creates denser, more continuous textures.
    /// </summary>
    public float GrainDensity
    {
        get => GetParameter("GrainDensity");
        set => SetParameter("GrainDensity", Math.Clamp(value, 1f, 200f));
    }

    /// <summary>
    /// Pitch shift in semitones (-24 to +24).
    /// Affects the playback pitch of grains.
    /// </summary>
    public float Pitch
    {
        get => GetParameter("Pitch");
        set => SetParameter("Pitch", Math.Clamp(value, -24f, 24f));
    }

    /// <summary>
    /// Position scatter amount (0.0 - 1.0).
    /// Randomizes grain start positions within the delay buffer.
    /// </summary>
    public float Scatter
    {
        get => GetParameter("Scatter");
        set => SetParameter("Scatter", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Feedback amount (0.0 - 0.95).
    /// Controls how much processed signal is fed back into the delay buffer.
    /// </summary>
    public float Feedback
    {
        get => GetParameter("Feedback");
        set => SetParameter("Feedback", Math.Clamp(value, 0f, 0.95f));
    }

    /// <summary>
    /// Pitch randomization in semitones (0 - 12).
    /// Adds random pitch variation to each grain.
    /// </summary>
    public float PitchRandom
    {
        get => GetParameter("PitchRandom");
        set => SetParameter("PitchRandom", Math.Clamp(value, 0f, 12f));
    }

    /// <summary>
    /// Stereo pan spread (0.0 - 1.0).
    /// Randomizes grain panning for wider stereo image.
    /// </summary>
    public float PanSpread
    {
        get => GetParameter("PanSpread");
        set => SetParameter("PanSpread", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Grain envelope shape.
    /// </summary>
    public GranularDelayEnvelope Envelope
    {
        get => (GranularDelayEnvelope)(int)GetParameter("Envelope");
        set => SetParameter("Envelope", (float)value);
    }

    /// <summary>
    /// Reverse grain probability (0.0 - 1.0).
    /// Percentage of grains that play in reverse.
    /// </summary>
    public float Reverse
    {
        get => GetParameter("Reverse");
        set => SetParameter("Reverse", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Freeze mode (0.0 or 1.0).
    /// When enabled, stops writing to delay buffer, creating infinite sustain.
    /// </summary>
    public bool Freeze
    {
        get => GetParameter("Freeze") > 0.5f;
        set => SetParameter("Freeze", value ? 1f : 0f);
    }

    /// <summary>
    /// Dry/Wet mix (0.0 - 1.0).
    /// Maps to base class Mix for compatibility.
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
        float grainSizeMs = GrainSize;
        float density = GrainDensity;
        float pitch = Pitch;
        float scatter = Scatter;
        float feedback = Feedback;
        float pitchRandom = PitchRandom;
        float panSpread = PanSpread;
        var envelope = Envelope;
        float reverse = Reverse;
        bool freeze = Freeze;

        // Calculate delay samples
        int delaySamples = (int)(delayTime * sampleRate);
        delaySamples = Math.Min(delaySamples, MaxDelaySamples - 1);

        // Calculate grain length in samples
        int grainLengthSamples = (int)(grainSizeMs * sampleRate / 1000f);
        grainLengthSamples = Math.Max(64, grainLengthSamples);

        // Calculate grain spawn interval
        double grainInterval = 1.0 / density;
        double deltaTime = 1.0 / sampleRate;

        lock (_lock)
        {
            for (int i = 0; i < count; i += channels)
            {
                // Process each channel
                for (int ch = 0; ch < channels; ch++)
                {
                    int index = i + ch;
                    float input = sourceBuffer[index];

                    // Write input to delay buffer (unless frozen)
                    if (!freeze)
                    {
                        _delayBuffers[ch].Write(input);
                    }
                    else
                    {
                        // In freeze mode, advance write position without writing
                        _delayBuffers[ch].AdvanceWrite();
                    }

                    // Spawn new grains based on density
                    _timeSinceLastGrain[ch] += deltaTime;

                    if (_timeSinceLastGrain[ch] >= grainInterval)
                    {
                        SpawnGrain(ch, delaySamples, grainLengthSamples, pitch, pitchRandom,
                                   scatter, panSpread, envelope, reverse);
                        _timeSinceLastGrain[ch] = 0;
                    }

                    // Process active grains and accumulate output
                    float grainOutput = ProcessGrains(ch, envelope);

                    // Apply feedback (granular output back into delay)
                    if (!freeze && feedback > 0f)
                    {
                        _delayBuffers[ch].AddToLast(grainOutput * feedback);
                    }

                    // Output granular processed signal
                    destBuffer[offset + index] = grainOutput;
                }
            }
        }
    }

    private void SpawnGrain(int channel, int delaySamples, int grainLength, float pitch,
                            float pitchRandom, float scatter, float panSpread,
                            GranularDelayEnvelope envelope, float reverse)
    {
        var grainList = _grains[channel];

        // Find inactive grain or create new one
        DelayGrain? grain = null;
        foreach (var g in grainList)
        {
            if (!g.IsActive)
            {
                grain = g;
                break;
            }
        }

        if (grain == null && grainList.Count < MaxGrainsPerChannel)
        {
            grain = new DelayGrain();
            grainList.Add(grain);
        }

        if (grain == null) return; // Max grains reached

        // Calculate start position with scatter
        float scatterRange = delaySamples * scatter;
        int scatterOffset = (int)((_random.NextDouble() * 2 - 1) * scatterRange);
        int startOffset = delaySamples + scatterOffset;
        startOffset = Math.Clamp(startOffset, grainLength, MaxDelaySamples - 1);

        // Calculate pitch with randomization
        float finalPitch = MathF.Pow(2f, pitch / 12f);
        if (pitchRandom > 0)
        {
            float randPitch = (float)(_random.NextDouble() * 2 - 1) * pitchRandom;
            finalPitch *= MathF.Pow(2f, randPitch / 12f);
        }

        // Determine if this grain should play in reverse
        bool isReverse = _random.NextDouble() < reverse;

        // Random pan position
        float pan = (float)(_random.NextDouble() * 2 - 1) * panSpread;

        // Initialize grain
        grain.StartOffset = startOffset;
        grain.Length = grainLength;
        grain.CurrentSample = 0;
        grain.Phase = 0;
        grain.Pitch = finalPitch;
        grain.Pan = pan;
        grain.IsReverse = isReverse;
        grain.Envelope = envelope;
        grain.IsActive = true;
    }

    private float ProcessGrains(int channel, GranularDelayEnvelope envelope)
    {
        var grainList = _grains[channel];
        var delayBuffer = _delayBuffers[channel];

        float output = 0f;
        int activeCount = 0;

        foreach (var grain in grainList)
        {
            if (!grain.IsActive) continue;

            // Calculate read position in delay buffer
            float readPhase = grain.IsReverse
                ? (float)(grain.Length - grain.Phase - 1)
                : (float)grain.Phase;

            int readOffset = grain.StartOffset - (int)readPhase;
            readOffset = Math.Clamp(readOffset, 0, MaxDelaySamples - 1);

            // Read sample with interpolation
            float frac = readPhase - (int)readPhase;
            float sample = delayBuffer.ReadInterpolated(readOffset, frac);

            // Apply grain envelope
            float env = GetEnvelopeValue(grain.CurrentSample, grain.Length, grain.Envelope);
            sample *= env;

            // Apply panning (for stereo, adjust based on channel)
            if (Channels == 2)
            {
                float panGain = channel == 0
                    ? MathF.Cos((grain.Pan + 1f) * MathF.PI / 4f)
                    : MathF.Sin((grain.Pan + 1f) * MathF.PI / 4f);
                sample *= panGain;
            }

            output += sample;
            activeCount++;

            // Advance grain playback
            grain.Phase += grain.Pitch;
            grain.CurrentSample++;

            // Check if grain has finished
            if (grain.CurrentSample >= grain.Length || grain.Phase >= grain.Length)
            {
                grain.IsActive = false;
            }
        }

        // Normalize output to prevent clipping with many grains
        if (activeCount > 1)
        {
            output *= 1f / MathF.Sqrt(activeCount);
        }

        return output;
    }

    private static float GetEnvelopeValue(int currentSample, int length, GranularDelayEnvelope envelope)
    {
        if (length <= 0) return 0f;

        float position = (float)currentSample / length;

        return envelope switch
        {
            GranularDelayEnvelope.Gaussian => MathF.Exp(-18f * MathF.Pow(position - 0.5f, 2)),
            GranularDelayEnvelope.Hann => 0.5f * (1f - MathF.Cos(2f * MathF.PI * position)),
            GranularDelayEnvelope.Triangle => position < 0.5f ? position * 2f : (1f - position) * 2f,
            GranularDelayEnvelope.Trapezoid => position < 0.1f ? position * 10f :
                                               position > 0.9f ? (1f - position) * 10f : 1f,
            _ => 1f
        };
    }

    /// <summary>
    /// Creates a subtle granular delay preset for ambient textures.
    /// </summary>
    public static GranularDelay CreateAmbientPreset(ISampleProvider source)
    {
        var effect = new GranularDelay(source, "Ambient Granular Delay");
        effect.DelayTime = 0.5f;
        effect.GrainSize = 80f;
        effect.GrainDensity = 25f;
        effect.Pitch = 0f;
        effect.Scatter = 0.2f;
        effect.Feedback = 0.4f;
        effect.PanSpread = 0.6f;
        effect.Envelope = GranularDelayEnvelope.Gaussian;
        effect.Mix = 0.4f;
        return effect;
    }

    /// <summary>
    /// Creates a shimmer delay preset with pitch-shifted grains.
    /// </summary>
    public static GranularDelay CreateShimmerPreset(ISampleProvider source)
    {
        var effect = new GranularDelay(source, "Shimmer Granular Delay");
        effect.DelayTime = 0.4f;
        effect.GrainSize = 60f;
        effect.GrainDensity = 35f;
        effect.Pitch = 12f; // Octave up
        effect.PitchRandom = 0.2f;
        effect.Scatter = 0.15f;
        effect.Feedback = 0.5f;
        effect.PanSpread = 0.8f;
        effect.Envelope = GranularDelayEnvelope.Hann;
        effect.Mix = 0.35f;
        return effect;
    }

    /// <summary>
    /// Creates a chaotic granular delay preset with reverse grains.
    /// </summary>
    public static GranularDelay CreateChaoticPreset(ISampleProvider source)
    {
        var effect = new GranularDelay(source, "Chaotic Granular Delay");
        effect.DelayTime = 0.6f;
        effect.GrainSize = 40f;
        effect.GrainDensity = 50f;
        effect.Pitch = -5f;
        effect.PitchRandom = 3f;
        effect.Scatter = 0.5f;
        effect.Feedback = 0.35f;
        effect.PanSpread = 1.0f;
        effect.Reverse = 0.4f;
        effect.Envelope = GranularDelayEnvelope.Triangle;
        effect.Mix = 0.5f;
        return effect;
    }

    /// <summary>
    /// Creates a freeze delay preset for infinite sustain effects.
    /// </summary>
    public static GranularDelay CreateFreezePreset(ISampleProvider source)
    {
        var effect = new GranularDelay(source, "Freeze Granular Delay");
        effect.DelayTime = 1.0f;
        effect.GrainSize = 100f;
        effect.GrainDensity = 20f;
        effect.Pitch = 0f;
        effect.Scatter = 0.05f;
        effect.Feedback = 0f;
        effect.PanSpread = 0.4f;
        effect.Envelope = GranularDelayEnvelope.Gaussian;
        effect.Mix = 0.6f;
        // Note: Freeze should be enabled when needed via effect.Freeze = true
        return effect;
    }

    /// <summary>
    /// Internal grain state for delay processing.
    /// </summary>
    private class DelayGrain
    {
        public int StartOffset { get; set; }
        public int Length { get; set; }
        public int CurrentSample { get; set; }
        public double Phase { get; set; }
        public float Pitch { get; set; } = 1.0f;
        public float Pan { get; set; }
        public bool IsReverse { get; set; }
        public GranularDelayEnvelope Envelope { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// Circular buffer for delay with interpolated reading.
    /// </summary>
    private class CircularDelayBuffer
    {
        private readonly float[] _buffer;
        private int _writePos;

        public CircularDelayBuffer(int size)
        {
            _buffer = new float[size];
            _writePos = 0;
        }

        public void Write(float sample)
        {
            _buffer[_writePos] = sample;
            _writePos = (_writePos + 1) % _buffer.Length;
        }

        public void AdvanceWrite()
        {
            _writePos = (_writePos + 1) % _buffer.Length;
        }

        public void AddToLast(float sample)
        {
            int lastPos = (_writePos - 1 + _buffer.Length) % _buffer.Length;
            _buffer[lastPos] += sample;
        }

        public float ReadInterpolated(int offsetFromWrite, float fractional)
        {
            // Calculate read position relative to write position
            int readPos = (_writePos - offsetFromWrite - 1 + _buffer.Length) % _buffer.Length;

            // Clamp to valid range
            readPos = Math.Clamp(readPos, 0, _buffer.Length - 1);

            // Linear interpolation
            int nextPos = (readPos + 1) % _buffer.Length;
            float frac = Math.Clamp(fractional, 0f, 1f);

            return _buffer[readPos] * (1f - frac) + _buffer[nextPos] * frac;
        }
    }
}
