//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Compressor with sidechain input for ducking and pumping effects.

using NAudio.Wave;

namespace MusicEngine.Core;

/// <summary>
/// A dynamic range compressor with sidechain input support.
/// Compresses the main signal based on the level of an external sidechain signal.
/// Ideal for ducking effects (lowering music when voice comes in) and
/// EDM-style pumping effects triggered by kick drums.
/// </summary>
public class SidechainCompressor : EffectBase, ISidechainable
{
    private ISampleProvider? _sidechainSource;
    private float[] _sidechainBuffer = Array.Empty<float>();
    private float[] _envelope;
    private float[] _gainSmooth;

    // High-pass filter state for sidechain filtering
    private float[] _filterState;

    /// <summary>
    /// Creates a new sidechain compressor effect.
    /// </summary>
    /// <param name="source">The main audio source to compress</param>
    /// <param name="name">Effect name</param>
    public SidechainCompressor(ISampleProvider source, string name = "SidechainCompressor")
        : base(source, name)
    {
        int channels = source.WaveFormat.Channels;
        _envelope = new float[channels];
        _gainSmooth = new float[channels];
        _filterState = new float[channels];

        // Initialize default parameters
        RegisterParameter("Threshold", -20f);      // -20 dB threshold
        RegisterParameter("Ratio", 4f);            // 4:1 compression ratio
        RegisterParameter("Attack", 0.01f);        // 10ms attack
        RegisterParameter("Release", 0.1f);        // 100ms release
        RegisterParameter("MakeupGain", 0f);       // 0 dB makeup gain
        RegisterParameter("KneeWidth", 6f);        // 6 dB soft knee
        RegisterParameter("DuckAmount", 1f);       // Full ducking
        RegisterParameter("Hold", 0.05f);          // 50ms hold time
        RegisterParameter("Range", -40f);          // Maximum gain reduction in dB
        RegisterParameter("SidechainGain", 1f);    // Sidechain input gain
        RegisterParameter("SidechainFilter", 0f); // Sidechain high-pass filter frequency

        Mix = 1.0f;
    }

    #region Compressor Parameters

    /// <summary>
    /// Threshold in dB (-60 to 0).
    /// Sidechain level above this triggers compression of the main signal.
    /// </summary>
    public float Threshold
    {
        get => GetParameter("Threshold");
        set => SetParameter("Threshold", Math.Clamp(value, -60f, 0f));
    }

    /// <summary>
    /// Compression ratio (1.0 - 20.0).
    /// Higher values result in more aggressive compression.
    /// Use 20+ for limiting behavior.
    /// </summary>
    public float Ratio
    {
        get => GetParameter("Ratio");
        set => SetParameter("Ratio", Math.Clamp(value, 1f, 20f));
    }

    /// <summary>
    /// Attack time in seconds (0.0001 - 1.0).
    /// How quickly the compressor responds when sidechain exceeds threshold.
    /// Shorter attacks create tighter pumping effects.
    /// </summary>
    public float Attack
    {
        get => GetParameter("Attack");
        set => SetParameter("Attack", Math.Clamp(value, 0.0001f, 1f));
    }

    /// <summary>
    /// Release time in seconds (0.001 - 5.0).
    /// How quickly the compressor returns to normal after sidechain drops.
    /// Longer releases create smoother, more musical ducking.
    /// </summary>
    public float Release
    {
        get => GetParameter("Release");
        set => SetParameter("Release", Math.Clamp(value, 0.001f, 5f));
    }

    /// <summary>
    /// Makeup gain in dB (0 - 24).
    /// Compensates for volume loss from compression.
    /// </summary>
    public float MakeupGain
    {
        get => GetParameter("MakeupGain");
        set => SetParameter("MakeupGain", Math.Clamp(value, 0f, 24f));
    }

    /// <summary>
    /// Knee width in dB (0 - 20).
    /// 0 = hard knee (abrupt compression), higher = soft knee (gradual).
    /// Soft knee creates smoother, more transparent compression.
    /// </summary>
    public float KneeWidth
    {
        get => GetParameter("KneeWidth");
        set => SetParameter("KneeWidth", Math.Clamp(value, 0f, 20f));
    }

    /// <summary>
    /// Duck amount (0.0 - 1.0).
    /// Controls how much the sidechain affects the main signal.
    /// 0 = no ducking, 1 = full ducking based on compressor settings.
    /// </summary>
    public float DuckAmount
    {
        get => GetParameter("DuckAmount");
        set => SetParameter("DuckAmount", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Hold time in seconds (0 - 1.0).
    /// Time to maintain compression after sidechain drops below threshold.
    /// Prevents pumping artifacts on transient material.
    /// </summary>
    public float Hold
    {
        get => GetParameter("Hold");
        set => SetParameter("Hold", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Maximum gain reduction in dB (-60 to 0).
    /// Limits how much the signal can be attenuated.
    /// -40 dB is effectively silent, -6 dB is subtle.
    /// </summary>
    public float Range
    {
        get => GetParameter("Range");
        set => SetParameter("Range", Math.Clamp(value, -60f, 0f));
    }

    /// <summary>
    /// Dry/Wet mix (0.0 - 1.0).
    /// Allows parallel compression when less than 1.0.
    /// </summary>
    public float DryWet
    {
        get => Mix;
        set => Mix = value;
    }

    #endregion

    #region ISidechainable Implementation

    /// <summary>
    /// Gets or sets the sidechain source.
    /// </summary>
    public ISampleProvider? SidechainSource
    {
        get => _sidechainSource;
        set
        {
            if (value != null)
            {
                ConnectSidechain(value, validateFormat: true);
            }
            else
            {
                DisconnectSidechain();
            }
        }
    }

    /// <summary>
    /// Gets or sets whether the sidechain is enabled.
    /// </summary>
    public bool SidechainEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the sidechain input gain.
    /// </summary>
    public float SidechainGain
    {
        get => GetParameter("SidechainGain");
        set => SetParameter("SidechainGain", Math.Clamp(value, 0.1f, 10f));
    }

    /// <summary>
    /// Gets or sets the sidechain high-pass filter frequency.
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

        if (validateFormat)
        {
            if (source.WaveFormat.SampleRate != SampleRate)
            {
                throw new ArgumentException(
                    $"Sidechain sample rate ({source.WaveFormat.SampleRate}) must match main source ({SampleRate})");
            }

            // Channels can differ - we'll handle mono-to-stereo conversion
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
    }

    #endregion

    #region Processing

    /// <summary>
    /// Processes the audio buffer with sidechain compression.
    /// </summary>
    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        // If sidechain is not connected or disabled, act as regular compressor
        if (_sidechainSource == null || !SidechainEnabled)
        {
            ProcessAsRegularCompressor(sourceBuffer, destBuffer, offset, count);
            return;
        }

        // Ensure sidechain buffer is large enough
        if (_sidechainBuffer.Length < count)
        {
            _sidechainBuffer = new float[count];
        }

        // Read sidechain signal
        int sidechainRead = _sidechainSource.Read(_sidechainBuffer, 0, count);
        if (sidechainRead < count)
        {
            // Fill remaining with zeros
            Array.Clear(_sidechainBuffer, sidechainRead, count - sidechainRead);
        }

        ProcessWithSidechain(sourceBuffer, destBuffer, offset, count);
    }

    private void ProcessWithSidechain(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;
        int sampleRate = SampleRate;

        float threshold = Threshold;
        float ratio = Ratio;
        float attack = Attack;
        float release = Release;
        float makeupGain = MakeupGain;
        float kneeWidth = KneeWidth;
        float duckAmount = DuckAmount;
        float range = Range;
        float sidechainGain = SidechainGain;
        float filterFreq = SidechainFilterFrequency;

        // Calculate coefficients
        float attackCoeff = MathF.Exp(-1f / (attack * sampleRate));
        float releaseCoeff = MathF.Exp(-1f / (release * sampleRate));
        float makeupGainLinear = MathF.Pow(10f, makeupGain / 20f);
        float rangeLinear = MathF.Pow(10f, range / 20f);

        // Calculate filter coefficient if filtering enabled
        float filterCoeff = 0f;
        if (filterFreq > 0f)
        {
            float rc = 1f / (2f * MathF.PI * filterFreq);
            float dt = 1f / sampleRate;
            filterCoeff = dt / (rc + dt);
        }

        int sidechainChannels = _sidechainSource?.WaveFormat.Channels ?? channels;

        for (int i = 0; i < count; i += channels)
        {
            // Get sidechain level (handle mono/stereo mismatch)
            float sidechainLevel = 0f;
            int scBaseIndex = (sidechainChannels == channels) ? i : (i / channels) * sidechainChannels;

            for (int ch = 0; ch < Math.Min(channels, sidechainChannels); ch++)
            {
                int scIndex = scBaseIndex + ch;
                if (scIndex < _sidechainBuffer.Length)
                {
                    float scSample = _sidechainBuffer[scIndex] * sidechainGain;

                    // Apply high-pass filter to sidechain if enabled
                    if (filterFreq > 0f && ch < _filterState.Length)
                    {
                        float filtered = scSample - _filterState[ch];
                        _filterState[ch] += filtered * filterCoeff;
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
            float gainReductionDb = 0f;

            if (kneeWidth > 0f)
            {
                // Soft knee compression
                float kneeMin = threshold - kneeWidth / 2f;
                float kneeMax = threshold + kneeWidth / 2f;

                if (envDb > kneeMin && envDb < kneeMax)
                {
                    float kneeInput = envDb - kneeMin;
                    float kneeFactor = kneeInput / kneeWidth;
                    gainReductionDb = kneeFactor * kneeFactor * (threshold - envDb + kneeWidth / 2f) * (1f - 1f / ratio);
                }
                else if (envDb >= kneeMax)
                {
                    gainReductionDb = (threshold - envDb) * (1f - 1f / ratio);
                }
            }
            else
            {
                // Hard knee compression
                if (envDb > threshold)
                {
                    gainReductionDb = (threshold - envDb) * (1f - 1f / ratio);
                }
            }

            // Apply range limiting
            gainReductionDb = MathF.Max(gainReductionDb, range);

            // Convert to linear and apply duck amount
            float targetGain = MathF.Pow(10f, gainReductionDb / 20f);
            targetGain = 1f - duckAmount * (1f - targetGain);

            // Ensure we don't go below the range limit
            targetGain = MathF.Max(targetGain, rangeLinear);

            // Process each channel
            for (int ch = 0; ch < channels; ch++)
            {
                // Smooth the gain change
                float smoothCoeff = targetGain < _gainSmooth[ch] ? attackCoeff : releaseCoeff;
                _gainSmooth[ch] = targetGain + smoothCoeff * (_gainSmooth[ch] - targetGain);

                float input = sourceBuffer[i + ch];
                float output = input * _gainSmooth[ch] * makeupGainLinear;

                destBuffer[offset + i + ch] = output;
            }
        }
    }

    private void ProcessAsRegularCompressor(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;
        int sampleRate = SampleRate;

        float threshold = Threshold;
        float ratio = Ratio;
        float attack = Attack;
        float release = Release;
        float makeupGain = MakeupGain;
        float kneeWidth = KneeWidth;

        float attackCoeff = MathF.Exp(-1f / (attack * sampleRate));
        float releaseCoeff = MathF.Exp(-1f / (release * sampleRate));
        float makeupGainLinear = MathF.Pow(10f, makeupGain / 20f);

        for (int i = 0; i < count; i += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                float input = sourceBuffer[i + ch];

                // Envelope detection on input signal
                float inputAbs = MathF.Abs(input);
                float coeff = inputAbs > _envelope[ch] ? attackCoeff : releaseCoeff;
                _envelope[ch] = inputAbs + coeff * (_envelope[ch] - inputAbs);

                float inputDb = 20f * MathF.Log10(_envelope[ch] + 1e-6f);

                float gainReductionDb = 0f;
                if (kneeWidth > 0f)
                {
                    float kneeMin = threshold - kneeWidth / 2f;
                    float kneeMax = threshold + kneeWidth / 2f;

                    if (inputDb > kneeMin && inputDb < kneeMax)
                    {
                        float kneeInput = inputDb - kneeMin;
                        float kneeFactor = kneeInput / kneeWidth;
                        gainReductionDb = kneeFactor * kneeFactor * (threshold - inputDb + kneeWidth / 2f) * (1f - 1f / ratio);
                    }
                    else if (inputDb >= kneeMax)
                    {
                        gainReductionDb = (threshold - inputDb) * (1f - 1f / ratio);
                    }
                }
                else
                {
                    if (inputDb > threshold)
                    {
                        gainReductionDb = (threshold - inputDb) * (1f - 1f / ratio);
                    }
                }

                float targetGain = MathF.Pow(10f, gainReductionDb / 20f);
                float smoothCoeff = targetGain < _gainSmooth[ch] ? attackCoeff : releaseCoeff;
                _gainSmooth[ch] = targetGain + smoothCoeff * (_gainSmooth[ch] - targetGain);

                float output = input * _gainSmooth[ch] * makeupGainLinear;

                destBuffer[offset + i + ch] = output;
            }
        }
    }

    /// <summary>
    /// Gets the current gain reduction in dB.
    /// Useful for metering displays.
    /// </summary>
    /// <param name="channel">Channel index</param>
    /// <returns>Gain reduction in dB (negative values)</returns>
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
        Array.Clear(_filterState);

        // Initialize gain to unity
        for (int i = 0; i < _gainSmooth.Length; i++)
        {
            _gainSmooth[i] = 1f;
        }
    }

    #endregion

    #region Preset Configurations

    /// <summary>
    /// Configures the compressor for voice-over-music ducking.
    /// Music ducks when voice is detected.
    /// </summary>
    public void ConfigureForVoiceDucking()
    {
        Threshold = -30f;
        Ratio = 8f;
        Attack = 0.005f;
        Release = 0.3f;
        KneeWidth = 6f;
        DuckAmount = 0.8f;
        Range = -20f;
        SidechainFilterFrequency = 300f; // Focus on voice frequencies
    }

    /// <summary>
    /// Configures the compressor for EDM-style pumping.
    /// Creates rhythmic volume pumping triggered by kick drums.
    /// </summary>
    public void ConfigureForPumping()
    {
        Threshold = -20f;
        Ratio = 10f;
        Attack = 0.001f;
        Release = 0.15f;
        KneeWidth = 0f; // Hard knee for punchy response
        DuckAmount = 1f;
        Range = -30f;
        SidechainFilterFrequency = 0f; // Full range for kick
    }

    /// <summary>
    /// Configures the compressor for subtle background music reduction.
    /// Gently lowers background music during dialog.
    /// </summary>
    public void ConfigureForSubtleDucking()
    {
        Threshold = -25f;
        Ratio = 3f;
        Attack = 0.02f;
        Release = 0.5f;
        KneeWidth = 10f;
        DuckAmount = 0.5f;
        Range = -10f;
        SidechainFilterFrequency = 200f;
    }

    #endregion
}
