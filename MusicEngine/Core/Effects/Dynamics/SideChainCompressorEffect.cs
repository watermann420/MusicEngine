// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Dynamic range compressor.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Dynamics;

/// <summary>
/// Side-chain compressor effect - compresses the main signal based on
/// the level of an external side-chain signal.
/// Common for ducking effects (e.g., ducking music when vocals come in).
/// </summary>
public class SideChainCompressorEffect : EffectBase
{
    private ISampleProvider? _sideChainSource;
    private float[] _envelope;
    private float[] _gainSmooth;

    /// <summary>
    /// Creates a new side-chain compressor effect
    /// </summary>
    /// <param name="source">Main audio source to compress</param>
    /// <param name="name">Effect name</param>
    public SideChainCompressorEffect(ISampleProvider source, string name)
        : base(source, name)
    {
        int channels = source.WaveFormat.Channels;
        _envelope = new float[channels];
        _gainSmooth = new float[channels];

        // Initialize parameters
        RegisterParameter("Threshold", -20f);     // -20 dB
        RegisterParameter("Ratio", 4f);           // 4:1 compression
        RegisterParameter("Attack", 0.01f);       // 10ms attack
        RegisterParameter("Release", 0.1f);       // 100ms release
        RegisterParameter("MakeupGain", 0f);      // 0 dB makeup gain
        RegisterParameter("SideChainGain", 1f);   // 1x side-chain gain
        RegisterParameter("Mix", 1.0f);           // 100% wet
    }

    /// <summary>
    /// Sets the side-chain source
    /// This is the signal that controls the compression
    /// </summary>
    public void SetSideChainSource(ISampleProvider sideChainSource)
    {
        if (sideChainSource.WaveFormat.SampleRate != SampleRate ||
            sideChainSource.WaveFormat.Channels != Channels)
        {
            throw new ArgumentException("Side-chain source wave format must match main source");
        }

        _sideChainSource = sideChainSource;
    }

    /// <summary>
    /// Threshold in dB (-60 to 0)
    /// Side-chain level above this triggers compression
    /// </summary>
    public float Threshold
    {
        get => GetParameter("Threshold");
        set => SetParameter("Threshold", Math.Clamp(value, -60f, 0f));
    }

    /// <summary>
    /// Compression ratio (1.0 - 20.0)
    /// How much to compress when triggered
    /// </summary>
    public float Ratio
    {
        get => GetParameter("Ratio");
        set => SetParameter("Ratio", Math.Clamp(value, 1f, 20f));
    }

    /// <summary>
    /// Attack time in seconds (0.0001 - 1.0)
    /// How fast compression engages
    /// </summary>
    public float Attack
    {
        get => GetParameter("Attack");
        set => SetParameter("Attack", Math.Clamp(value, 0.0001f, 1f));
    }

    /// <summary>
    /// Release time in seconds (0.001 - 5.0)
    /// How fast compression disengages
    /// </summary>
    public float Release
    {
        get => GetParameter("Release");
        set => SetParameter("Release", Math.Clamp(value, 0.001f, 5f));
    }

    /// <summary>
    /// Makeup gain in dB (0 - 48)
    /// Compensates for volume loss
    /// </summary>
    public float MakeupGain
    {
        get => GetParameter("MakeupGain");
        set => SetParameter("MakeupGain", Math.Clamp(value, 0f, 48f));
    }

    /// <summary>
    /// Side-chain gain (0.1 - 10.0)
    /// Amplifies side-chain signal for more/less sensitivity
    /// </summary>
    public float SideChainGain
    {
        get => GetParameter("SideChainGain");
        set => SetParameter("SideChainGain", Math.Clamp(value, 0.1f, 10f));
    }

    /// <summary>
    /// Dry/Wet mix (0.0 - 1.0)
    /// 0 = fully dry (original signal), 1 = fully wet (compressed signal)
    /// </summary>
    public float DryWet
    {
        get => Mix;
        set => Mix = value;
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        // If no side-chain source, act as regular compressor
        if (_sideChainSource == null)
        {
            ProcessAsRegularCompressor(sourceBuffer, destBuffer, offset, count);
            return;
        }

        int channels = Channels;
        int sampleRate = SampleRate;

        float threshold = Threshold;
        float ratio = Ratio;
        float attack = Attack;
        float release = Release;
        float makeupGain = MakeupGain;
        float sideChainGain = SideChainGain;

        // Calculate attack and release coefficients
        float attackCoeff = MathF.Exp(-1f / (attack * sampleRate));
        float releaseCoeff = MathF.Exp(-1f / (release * sampleRate));

        // Convert makeup gain from dB to linear
        float makeupGainLinear = MathF.Pow(10f, makeupGain / 20f);

        // Read side-chain signal
        float[] sideChainBuffer = new float[count];
        _sideChainSource.Read(sideChainBuffer, 0, count);

        for (int i = 0; i < count; i += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                int srcIndex = i + ch;
                int scIndex = i + ch;

                float input = sourceBuffer[srcIndex];
                float sideChainInput = sideChainBuffer[scIndex] * sideChainGain;

                // Envelope detection on side-chain signal
                float scAbs = MathF.Abs(sideChainInput);
                float coeff = scAbs > _envelope[ch] ? attackCoeff : releaseCoeff;
                _envelope[ch] = scAbs + coeff * (_envelope[ch] - scAbs);

                // Convert to dB
                float scDb = 20f * MathF.Log10(_envelope[ch] + 1e-6f);

                // Calculate gain reduction based on side-chain level
                float gainReductionDb = 0f;
                if (scDb > threshold)
                {
                    gainReductionDb = (threshold - scDb) * (1f - 1f / ratio);
                }

                // Convert gain reduction to linear and smooth it
                float targetGain = MathF.Pow(10f, gainReductionDb / 20f);
                float smoothCoeff = targetGain < _gainSmooth[ch] ? attackCoeff : releaseCoeff;
                _gainSmooth[ch] = targetGain + smoothCoeff * (_gainSmooth[ch] - targetGain);

                // Apply compression and makeup gain to main signal
                float output = input * _gainSmooth[ch] * makeupGainLinear;

                destBuffer[offset + i + ch] = output;
            }
        }
    }

    /// <summary>
    /// Fallback to regular compression if no side-chain source
    /// </summary>
    private void ProcessAsRegularCompressor(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;
        int sampleRate = SampleRate;

        float threshold = Threshold;
        float ratio = Ratio;
        float attack = Attack;
        float release = Release;
        float makeupGain = MakeupGain;

        float attackCoeff = MathF.Exp(-1f / (attack * sampleRate));
        float releaseCoeff = MathF.Exp(-1f / (release * sampleRate));
        float makeupGainLinear = MathF.Pow(10f, makeupGain / 20f);

        for (int i = 0; i < count; i += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                int srcIndex = i + ch;
                float input = sourceBuffer[srcIndex];

                float inputAbs = MathF.Abs(input);
                float coeff = inputAbs > _envelope[ch] ? attackCoeff : releaseCoeff;
                _envelope[ch] = inputAbs + coeff * (_envelope[ch] - inputAbs);

                float inputDb = 20f * MathF.Log10(_envelope[ch] + 1e-6f);

                float gainReductionDb = 0f;
                if (inputDb > threshold)
                {
                    gainReductionDb = (threshold - inputDb) * (1f - 1f / ratio);
                }

                float targetGain = MathF.Pow(10f, gainReductionDb / 20f);
                float smoothCoeff = targetGain < _gainSmooth[ch] ? attackCoeff : releaseCoeff;
                _gainSmooth[ch] = targetGain + smoothCoeff * (_gainSmooth[ch] - targetGain);

                float output = input * _gainSmooth[ch] * makeupGainLinear;

                destBuffer[offset + i + ch] = output;
            }
        }
    }
}
