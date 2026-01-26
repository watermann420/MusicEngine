// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Dynamic range compressor.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Dynamics;

/// <summary>
/// Dynamic range compressor effect with optional sidechain support.
/// Reduces the volume of loud sounds above a threshold, with configurable ratio,
/// attack, release, and makeup gain. Supports external sidechain input for
/// ducking and pumping effects.
/// </summary>
public class CompressorEffect : EffectBase, ISidechainable
{
    private float[] _envelope;    // Envelope follower state per channel
    private float[] _gainSmooth;  // Smoothed gain reduction per channel

    // Sidechain support
    private ISampleProvider? _sidechainSource;
    private float[] _sidechainBuffer = Array.Empty<float>();
    private float[] _sidechainFilterState;

    /// <summary>
    /// Creates a new compressor effect
    /// </summary>
    /// <param name="source">Audio source to compress</param>
    /// <param name="name">Effect name</param>
    public CompressorEffect(ISampleProvider source, string name)
        : base(source, name)
    {
        int channels = source.WaveFormat.Channels;
        _envelope = new float[channels];
        _gainSmooth = new float[channels];
        _sidechainFilterState = new float[channels];

        // Initialize parameters
        RegisterParameter("Threshold", -20f);     // -20 dB
        RegisterParameter("Ratio", 4f);           // 4:1 compression
        RegisterParameter("Attack", 0.005f);      // 5ms attack
        RegisterParameter("Release", 0.1f);       // 100ms release
        RegisterParameter("MakeupGain", 0f);      // 0 dB makeup gain
        RegisterParameter("KneeWidth", 0f);       // Hard knee (0 = hard, >0 = soft)
        RegisterParameter("AutoGain", 0f);        // Auto makeup gain off
        RegisterParameter("SidechainGain", 1f);   // Sidechain input gain
        RegisterParameter("SidechainFilter", 0f); // Sidechain high-pass filter
        Mix = 1.0f;                               // 100% wet
    }

    /// <summary>
    /// Threshold in dB (-60 to 0)
    /// Signals above this level will be compressed
    /// </summary>
    public float Threshold
    {
        get => GetParameter("Threshold");
        set => SetParameter("Threshold", Math.Clamp(value, -60f, 0f));
    }

    /// <summary>
    /// Compression ratio (1.0 - 20.0)
    /// 1.0 = no compression, 4.0 = 4:1 ratio, 20.0 = limiting
    /// </summary>
    public float Ratio
    {
        get => GetParameter("Ratio");
        set => SetParameter("Ratio", Math.Clamp(value, 1f, 20f));
    }

    /// <summary>
    /// Attack time in seconds (0.0001 - 1.0)
    /// How fast the compressor responds to loud signals
    /// </summary>
    public float Attack
    {
        get => GetParameter("Attack");
        set => SetParameter("Attack", Math.Clamp(value, 0.0001f, 1f));
    }

    /// <summary>
    /// Release time in seconds (0.001 - 5.0)
    /// How fast the compressor returns to normal after loud signal ends
    /// </summary>
    public float Release
    {
        get => GetParameter("Release");
        set => SetParameter("Release", Math.Clamp(value, 0.001f, 5f));
    }

    /// <summary>
    /// Makeup gain in dB (0 - 48)
    /// Compensates for volume loss from compression
    /// </summary>
    public float MakeupGain
    {
        get => GetParameter("MakeupGain");
        set => SetParameter("MakeupGain", Math.Clamp(value, 0f, 48f));
    }

    /// <summary>
    /// Knee width in dB (0 - 20)
    /// 0 = hard knee (abrupt), >0 = soft knee (smooth transition)
    /// </summary>
    public float KneeWidth
    {
        get => GetParameter("KneeWidth");
        set => SetParameter("KneeWidth", Math.Clamp(value, 0f, 20f));
    }

    /// <summary>
    /// Auto makeup gain (0 = off, 1 = full auto)
    /// Automatically calculates makeup gain to compensate for compression
    /// </summary>
    public float AutoGain
    {
        get => GetParameter("AutoGain");
        set => SetParameter("AutoGain", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Dry/Wet mix (0.0 - 1.0)
    /// 0.0 = fully dry (no compression), 1.0 = fully wet (full compression)
    /// </summary>
    public float DryWet
    {
        get => Mix;
        set => Mix = value;
    }

    #region ISidechainable Implementation

    /// <summary>
    /// Gets or sets the sidechain source.
    /// When set, compression is triggered by this external signal instead of the input.
    /// </summary>
    public ISampleProvider? SidechainSource
    {
        get => _sidechainSource;
        set
        {
            if (value != null)
                ConnectSidechain(value);
            else
                DisconnectSidechain();
        }
    }

    /// <summary>
    /// Gets or sets whether the sidechain is enabled.
    /// </summary>
    public bool SidechainEnabled { get; set; }

    /// <summary>
    /// Gets or sets the sidechain input gain (0.1 - 10.0).
    /// </summary>
    public float SidechainGain
    {
        get => GetParameter("SidechainGain");
        set => SetParameter("SidechainGain", Math.Clamp(value, 0.1f, 10f));
    }

    /// <summary>
    /// Gets or sets the sidechain high-pass filter frequency in Hz.
    /// </summary>
    public float SidechainFilterFrequency
    {
        get => GetParameter("SidechainFilter");
        set => SetParameter("SidechainFilter", Math.Clamp(value, 0f, 5000f));
    }

    /// <summary>
    /// Gets whether a sidechain source is connected.
    /// </summary>
    public bool IsSidechainConnected => _sidechainSource != null;

    /// <summary>
    /// Connects a sidechain source.
    /// </summary>
    public void ConnectSidechain(ISampleProvider source, bool validateFormat = true)
    {
        if (source == null)
        {
            DisconnectSidechain();
            return;
        }

        if (validateFormat && source.WaveFormat.SampleRate != SampleRate)
        {
            throw new ArgumentException(
                $"Sidechain sample rate ({source.WaveFormat.SampleRate}) must match main source ({SampleRate})");
        }

        _sidechainSource = source;
        SidechainEnabled = true;
    }

    /// <summary>
    /// Disconnects the sidechain source.
    /// </summary>
    public void DisconnectSidechain()
    {
        _sidechainSource = null;
        SidechainEnabled = false;
    }

    #endregion

    /// <summary>
    /// Gets the current gain reduction in dB for metering.
    /// </summary>
    /// <param name="channel">Channel index</param>
    /// <returns>Gain reduction in dB (negative value)</returns>
    public float GetGainReductionDb(int channel = 0)
    {
        if (channel < 0 || channel >= _gainSmooth.Length)
            return 0f;

        float gain = _gainSmooth[channel];
        if (gain <= 0f)
            return -100f;

        return 20f * MathF.Log10(gain);
    }

    /// <summary>
    /// Resets the compressor state.
    /// </summary>
    public void Reset()
    {
        Array.Clear(_envelope);
        Array.Clear(_gainSmooth);
        Array.Clear(_sidechainFilterState);

        for (int i = 0; i < _gainSmooth.Length; i++)
            _gainSmooth[i] = 1f;
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        // Use sidechain processing if enabled and connected
        if (SidechainEnabled && _sidechainSource != null)
        {
            ProcessWithSidechain(sourceBuffer, destBuffer, offset, count);
            return;
        }

        ProcessStandard(sourceBuffer, destBuffer, offset, count);
    }

    private void ProcessStandard(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;
        int sampleRate = SampleRate;

        float threshold = Threshold;
        float ratio = Ratio;
        float attack = Attack;
        float release = Release;
        float makeupGain = MakeupGain;
        float kneeWidth = KneeWidth;
        float autoGain = AutoGain;

        // Calculate attack and release coefficients
        float attackCoeff = MathF.Exp(-1f / (attack * sampleRate));
        float releaseCoeff = MathF.Exp(-1f / (release * sampleRate));

        // Convert makeup gain from dB to linear
        float makeupGainLinear = MathF.Pow(10f, makeupGain / 20f);

        // Calculate auto makeup gain if enabled
        if (autoGain > 0f)
        {
            float autoMakeup = (threshold * (1f - 1f / ratio)) * autoGain;
            makeupGainLinear *= MathF.Pow(10f, autoMakeup / 20f);
        }

        for (int i = 0; i < count; i += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                float input = sourceBuffer[i + ch];

                // Envelope detection (peak detector)
                float inputAbs = MathF.Abs(input);
                float coeff = inputAbs > _envelope[ch] ? attackCoeff : releaseCoeff;
                _envelope[ch] = inputAbs + coeff * (_envelope[ch] - inputAbs);

                // Convert to dB
                float inputDb = 20f * MathF.Log10(_envelope[ch] + 1e-6f);

                // Calculate gain reduction
                float gainReductionDb = CalculateGainReduction(inputDb, threshold, ratio, kneeWidth);

                // Convert gain reduction to linear and smooth it
                float targetGain = MathF.Pow(10f, gainReductionDb / 20f);
                float smoothCoeff = targetGain < _gainSmooth[ch] ? attackCoeff : releaseCoeff;
                _gainSmooth[ch] = targetGain + smoothCoeff * (_gainSmooth[ch] - targetGain);

                // Apply compression and makeup gain
                float output = input * _gainSmooth[ch] * makeupGainLinear;

                destBuffer[offset + i + ch] = output;
            }
        }
    }

    private void ProcessWithSidechain(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        // Ensure sidechain buffer is large enough
        if (_sidechainBuffer.Length < count)
        {
            _sidechainBuffer = new float[count];
        }

        // Read sidechain signal
        int sidechainRead = _sidechainSource!.Read(_sidechainBuffer, 0, count);
        if (sidechainRead < count)
        {
            Array.Clear(_sidechainBuffer, sidechainRead, count - sidechainRead);
        }

        int channels = Channels;
        int sampleRate = SampleRate;

        float threshold = Threshold;
        float ratio = Ratio;
        float attack = Attack;
        float release = Release;
        float makeupGain = MakeupGain;
        float kneeWidth = KneeWidth;
        float autoGain = AutoGain;
        float sidechainGain = SidechainGain;
        float filterFreq = SidechainFilterFrequency;

        float attackCoeff = MathF.Exp(-1f / (attack * sampleRate));
        float releaseCoeff = MathF.Exp(-1f / (release * sampleRate));
        float makeupGainLinear = MathF.Pow(10f, makeupGain / 20f);

        if (autoGain > 0f)
        {
            float autoMakeup = (threshold * (1f - 1f / ratio)) * autoGain;
            makeupGainLinear *= MathF.Pow(10f, autoMakeup / 20f);
        }

        // Calculate filter coefficient
        float filterCoeff = 0f;
        if (filterFreq > 0f)
        {
            float rc = 1f / (2f * MathF.PI * filterFreq);
            float dt = 1f / sampleRate;
            filterCoeff = dt / (rc + dt);
        }

        int sidechainChannels = _sidechainSource.WaveFormat.Channels;

        for (int i = 0; i < count; i += channels)
        {
            // Get sidechain level
            float sidechainLevel = 0f;
            int scBaseIndex = (sidechainChannels == channels) ? i : (i / channels) * sidechainChannels;

            for (int ch = 0; ch < Math.Min(channels, sidechainChannels); ch++)
            {
                int scIndex = scBaseIndex + ch;
                if (scIndex < _sidechainBuffer.Length)
                {
                    float scSample = _sidechainBuffer[scIndex] * sidechainGain;

                    // Apply high-pass filter if enabled
                    if (filterFreq > 0f && ch < _sidechainFilterState.Length)
                    {
                        float filtered = scSample - _sidechainFilterState[ch];
                        _sidechainFilterState[ch] += filtered * filterCoeff;
                        scSample = filtered;
                    }

                    sidechainLevel = MathF.Max(sidechainLevel, MathF.Abs(scSample));
                }
            }

            // Envelope follower on sidechain
            for (int ch = 0; ch < channels; ch++)
            {
                float coeff = sidechainLevel > _envelope[ch] ? attackCoeff : releaseCoeff;
                _envelope[ch] = sidechainLevel + coeff * (_envelope[ch] - sidechainLevel);
            }

            // Calculate gain reduction based on sidechain envelope
            float envDb = 20f * MathF.Log10(_envelope[0] + 1e-6f);
            float gainReductionDb = CalculateGainReduction(envDb, threshold, ratio, kneeWidth);

            // Convert to linear and smooth
            float targetGain = MathF.Pow(10f, gainReductionDb / 20f);

            // Apply to each channel of the main signal
            for (int ch = 0; ch < channels; ch++)
            {
                float smoothCoeff = targetGain < _gainSmooth[ch] ? attackCoeff : releaseCoeff;
                _gainSmooth[ch] = targetGain + smoothCoeff * (_gainSmooth[ch] - targetGain);

                float input = sourceBuffer[i + ch];
                float output = input * _gainSmooth[ch] * makeupGainLinear;
                destBuffer[offset + i + ch] = output;
            }
        }
    }

    private static float CalculateGainReduction(float inputDb, float threshold, float ratio, float kneeWidth)
    {
        float gainReductionDb = 0f;

        if (kneeWidth > 0f)
        {
            // Soft knee
            float kneeMin = threshold - kneeWidth / 2f;
            float kneeMax = threshold + kneeWidth / 2f;

            if (inputDb > kneeMin && inputDb < kneeMax)
            {
                // In the knee region
                float kneeInput = inputDb - kneeMin;
                float kneeFactor = kneeInput / kneeWidth;
                gainReductionDb = kneeFactor * kneeFactor * (threshold - inputDb + kneeWidth / 2f) * (1f - 1f / ratio);
            }
            else if (inputDb >= kneeMax)
            {
                // Above knee
                gainReductionDb = (threshold - inputDb) * (1f - 1f / ratio);
            }
        }
        else
        {
            // Hard knee
            if (inputDb > threshold)
            {
                gainReductionDb = (threshold - inputDb) * (1f - 1f / ratio);
            }
        }

        return gainReductionDb;
    }
}
