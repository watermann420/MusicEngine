//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Simplified sidechain ducker effect for automatic gain reduction.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Dynamics;

/// <summary>
/// Simplified sidechain ducker effect for automatic gain reduction.
/// Reduces the main signal level when the sidechain signal exceeds the threshold.
/// Optimized for ducking scenarios like lowering music when voice/narration comes in,
/// or creating pumping effects triggered by kick drums.
/// </summary>
/// <remarks>
/// Unlike a full compressor, the ducker uses a simpler gain reduction model:
/// - When sidechain exceeds threshold, gain is reduced by the Amount parameter
/// - Hold time keeps the ducking active after the sidechain drops below threshold
/// - Built-in high-pass filter on sidechain for frequency-focused detection
/// </remarks>
public class SideChainDucker : EffectBase, ISidechainable
{
    private ISampleProvider? _sidechainSource;
    private bool _sidechainEnabled = true;
    private float _sidechainGain = 1.0f;
    private float _sidechainFilterFrequency = 0f;

    // Per-channel state
    private float[] _envelope;
    private float[] _gainSmooth;
    private float[] _holdCounter;

    // High-pass filter state for sidechain (per channel)
    private float[] _hpfPrevInput;
    private float[] _hpfPrevOutput;
    private float _hpfCoefficient;

    /// <summary>
    /// Creates a new sidechain ducker effect.
    /// </summary>
    /// <param name="source">Main audio source to duck</param>
    public SideChainDucker(ISampleProvider source)
        : this(source, "SideChainDucker")
    {
    }

    /// <summary>
    /// Creates a new sidechain ducker effect with a custom name.
    /// </summary>
    /// <param name="source">Main audio source to duck</param>
    /// <param name="name">Effect name</param>
    public SideChainDucker(ISampleProvider source, string name)
        : base(source, name)
    {
        int channels = source.WaveFormat.Channels;
        _envelope = new float[channels];
        _gainSmooth = new float[channels];
        _holdCounter = new float[channels];
        _hpfPrevInput = new float[channels];
        _hpfPrevOutput = new float[channels];

        // Initialize gain smoothing to 1.0 (no reduction)
        for (int i = 0; i < channels; i++)
        {
            _gainSmooth[i] = 1.0f;
        }

        // Register parameters with defaults
        RegisterParameter("Threshold", -30f);      // -30 dB - when sidechain triggers ducking
        RegisterParameter("Amount", -12f);         // -12 dB - how much to reduce gain
        RegisterParameter("Attack", 0.005f);       // 5ms - fast attack for responsive ducking
        RegisterParameter("Release", 0.3f);        // 300ms - smooth release
        RegisterParameter("Hold", 0.05f);          // 50ms - hold time after sidechain drops
        RegisterParameter("SidechainFilter", 0f);  // 0 Hz = disabled, >0 = high-pass filter frequency

        UpdateFilterCoefficient();
    }

    #region ISidechainable Implementation

    /// <inheritdoc />
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

    /// <inheritdoc />
    public bool SidechainEnabled
    {
        get => _sidechainEnabled;
        set => _sidechainEnabled = value;
    }

    /// <inheritdoc />
    public float SidechainGain
    {
        get => _sidechainGain;
        set => _sidechainGain = Math.Clamp(value, 0.1f, 10f);
    }

    /// <inheritdoc />
    public float SidechainFilterFrequency
    {
        get => _sidechainFilterFrequency;
        set
        {
            _sidechainFilterFrequency = Math.Clamp(value, 0f, 5000f);
            UpdateFilterCoefficient();
        }
    }

    /// <inheritdoc />
    public bool IsSidechainConnected => _sidechainSource != null;

    /// <inheritdoc />
    public void ConnectSidechain(ISampleProvider source, bool validateFormat = true)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (validateFormat)
        {
            if (source.WaveFormat.SampleRate != SampleRate)
            {
                throw new ArgumentException(
                    $"Sidechain sample rate ({source.WaveFormat.SampleRate}) must match main source ({SampleRate})",
                    nameof(source));
            }

            if (source.WaveFormat.Channels != Channels)
            {
                throw new ArgumentException(
                    $"Sidechain channel count ({source.WaveFormat.Channels}) must match main source ({Channels})",
                    nameof(source));
            }
        }

        _sidechainSource = source;
    }

    /// <inheritdoc />
    public void DisconnectSidechain()
    {
        _sidechainSource = null;
    }

    #endregion

    #region Parameters

    /// <summary>
    /// Threshold in dB (-60 to 0).
    /// When the sidechain signal exceeds this level, ducking is triggered.
    /// Lower values make the ducker more sensitive.
    /// </summary>
    public float Threshold
    {
        get => GetParameter("Threshold");
        set => SetParameter("Threshold", Math.Clamp(value, -60f, 0f));
    }

    /// <summary>
    /// Ducking amount in dB (-48 to 0).
    /// How much to reduce the gain when ducking is active.
    /// More negative values create stronger ducking.
    /// </summary>
    public float Amount
    {
        get => GetParameter("Amount");
        set => SetParameter("Amount", Math.Clamp(value, -48f, 0f));
    }

    /// <summary>
    /// Attack time in seconds (0.0001 to 0.5).
    /// How quickly the ducking engages when triggered.
    /// Faster attack = more immediate ducking response.
    /// </summary>
    public float Attack
    {
        get => GetParameter("Attack");
        set => SetParameter("Attack", Math.Clamp(value, 0.0001f, 0.5f));
    }

    /// <summary>
    /// Release time in seconds (0.01 to 5.0).
    /// How quickly the gain returns to normal after ducking.
    /// Longer release = smoother fade back in.
    /// </summary>
    public float Release
    {
        get => GetParameter("Release");
        set => SetParameter("Release", Math.Clamp(value, 0.01f, 5f));
    }

    /// <summary>
    /// Hold time in seconds (0.0 to 1.0).
    /// How long to maintain ducking after the sidechain drops below threshold.
    /// Prevents rapid gain changes from choppy sidechain signals.
    /// </summary>
    public float Hold
    {
        get => GetParameter("Hold");
        set => SetParameter("Hold", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Sidechain high-pass filter frequency in Hz (0 to 5000).
    /// When greater than 0, filters low frequencies from the sidechain signal.
    /// Useful for focusing detection on specific frequency ranges.
    /// Set to 0 to disable the filter.
    /// </summary>
    public float SidechainFilter
    {
        get => GetParameter("SidechainFilter");
        set
        {
            SetParameter("SidechainFilter", Math.Clamp(value, 0f, 5000f));
            _sidechainFilterFrequency = value;
            UpdateFilterCoefficient();
        }
    }

    #endregion

    /// <summary>
    /// Updates the high-pass filter coefficient based on the current filter frequency.
    /// </summary>
    private void UpdateFilterCoefficient()
    {
        float filterFreq = Math.Max(_sidechainFilterFrequency, GetParameter("SidechainFilter"));
        if (filterFreq > 0 && SampleRate > 0)
        {
            // First-order high-pass filter coefficient
            // RC = 1 / (2 * PI * fc)
            float rc = 1f / (2f * MathF.PI * filterFreq);
            float dt = 1f / SampleRate;
            _hpfCoefficient = rc / (rc + dt);
        }
        else
        {
            _hpfCoefficient = 0f; // Filter disabled
        }
    }

    /// <summary>
    /// Applies high-pass filter to a sidechain sample.
    /// </summary>
    private float ApplyHighPassFilter(float input, int channel)
    {
        if (_hpfCoefficient <= 0f)
        {
            return input; // Filter disabled
        }

        // First-order high-pass filter: y[n] = alpha * (y[n-1] + x[n] - x[n-1])
        float output = _hpfCoefficient * (_hpfPrevOutput[channel] + input - _hpfPrevInput[channel]);
        _hpfPrevInput[channel] = input;
        _hpfPrevOutput[channel] = output;
        return output;
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        // If sidechain is not connected or disabled, pass through unchanged
        if (_sidechainSource == null || !_sidechainEnabled)
        {
            Array.Copy(sourceBuffer, 0, destBuffer, offset, count);
            return;
        }

        int channels = Channels;
        int sampleRate = SampleRate;

        // Get parameters
        float threshold = Threshold;
        float amount = Amount;
        float attack = Attack;
        float release = Release;
        float hold = Hold;
        float scGain = _sidechainGain;

        // Calculate time constants
        float attackCoeff = MathF.Exp(-1f / (attack * sampleRate));
        float releaseCoeff = MathF.Exp(-1f / (release * sampleRate));
        float holdSamples = hold * sampleRate;

        // Convert amount from dB to linear target gain
        float duckGainTarget = MathF.Pow(10f, amount / 20f);

        // Read sidechain signal
        float[] sideChainBuffer = new float[count];
        int scRead = _sidechainSource.Read(sideChainBuffer, 0, count);

        // If sidechain read failed, fill with zeros
        if (scRead < count)
        {
            Array.Clear(sideChainBuffer, scRead, count - scRead);
        }

        for (int i = 0; i < count; i += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                int idx = i + ch;
                float input = sourceBuffer[idx];
                float scInput = sideChainBuffer[idx] * scGain;

                // Apply high-pass filter to sidechain if enabled
                scInput = ApplyHighPassFilter(scInput, ch);

                // Envelope detection on sidechain signal (peak detection)
                float scAbs = MathF.Abs(scInput);
                float envCoeff = scAbs > _envelope[ch] ? (1f - attackCoeff) : (1f - releaseCoeff);
                _envelope[ch] += envCoeff * (scAbs - _envelope[ch]);

                // Convert envelope to dB
                float scDb = 20f * MathF.Log10(_envelope[ch] + 1e-10f);

                // Determine target gain based on threshold
                float targetGain;
                if (scDb > threshold)
                {
                    // Above threshold - apply ducking
                    targetGain = duckGainTarget;
                    _holdCounter[ch] = holdSamples; // Reset hold counter
                }
                else if (_holdCounter[ch] > 0)
                {
                    // In hold period - maintain ducking
                    targetGain = duckGainTarget;
                    _holdCounter[ch]--;
                }
                else
                {
                    // Below threshold and hold expired - no ducking
                    targetGain = 1.0f;
                }

                // Smooth gain changes
                float smoothCoeff = targetGain < _gainSmooth[ch] ? attackCoeff : releaseCoeff;
                _gainSmooth[ch] = targetGain + smoothCoeff * (_gainSmooth[ch] - targetGain);

                // Apply gain to main signal
                destBuffer[offset + idx] = input * _gainSmooth[ch];
            }
        }
    }

    /// <summary>
    /// Resets the internal state of the ducker.
    /// Call this when starting playback from a new position.
    /// </summary>
    public void Reset()
    {
        for (int i = 0; i < Channels; i++)
        {
            _envelope[i] = 0f;
            _gainSmooth[i] = 1.0f;
            _holdCounter[i] = 0f;
            _hpfPrevInput[i] = 0f;
            _hpfPrevOutput[i] = 0f;
        }
    }

    /// <summary>
    /// Gets the current gain reduction in dB for the specified channel.
    /// Useful for metering/visualization.
    /// </summary>
    /// <param name="channel">Channel index (0 for left, 1 for right)</param>
    /// <returns>Gain reduction in dB (negative values)</returns>
    public float GetGainReduction(int channel = 0)
    {
        if (channel < 0 || channel >= Channels)
        {
            return 0f;
        }

        float gain = _gainSmooth[channel];
        if (gain >= 1.0f)
        {
            return 0f;
        }

        return 20f * MathF.Log10(gain);
    }

    /// <summary>
    /// Gets the current sidechain envelope level in dB for the specified channel.
    /// Useful for metering/visualization.
    /// </summary>
    /// <param name="channel">Channel index (0 for left, 1 for right)</param>
    /// <returns>Envelope level in dB</returns>
    public float GetSidechainLevel(int channel = 0)
    {
        if (channel < 0 || channel >= Channels)
        {
            return -100f;
        }

        return 20f * MathF.Log10(_envelope[channel] + 1e-10f);
    }
}
