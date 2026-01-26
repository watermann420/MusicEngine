// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: AI-based audio denoising.

using NAudio.Wave;

namespace MusicEngine.Core.AI;

/// <summary>
/// Quality mode for AI denoiser processing.
/// </summary>
public enum AIDenoiserQuality
{
    /// <summary>
    /// Fast mode with smaller FFT (1024). Lower latency, lower quality.
    /// </summary>
    Fast,

    /// <summary>
    /// Balanced mode with medium FFT (2048). Good balance of quality and performance.
    /// </summary>
    Balanced,

    /// <summary>
    /// Quality mode with larger FFT (4096). Best quality, higher latency.
    /// </summary>
    Quality
}

/// <summary>
/// Operating mode for AI denoiser.
/// </summary>
public enum AIDenoiserMode
{
    /// <summary>
    /// Normal denoising mode using learned or estimated noise profile.
    /// </summary>
    Denoise,

    /// <summary>
    /// Learning mode - captures noise profile from input (e.g., during silence).
    /// </summary>
    Learn,

    /// <summary>
    /// Bypass mode - passes audio through without processing.
    /// </summary>
    Bypass
}

/// <summary>
/// Neural network-style noise reduction effect using spectral gating with learned noise profiles
/// and adaptive thresholds. Mimics AI-like behavior using algorithmic approaches.
/// </summary>
/// <remarks>
/// The algorithm implements several AI-inspired techniques:
/// 1. Adaptive noise profile learning with statistical modeling
/// 2. Per-bin confidence weighting based on noise variance
/// 3. Transient detection and preservation
/// 4. Spectral masking with soft transitions
/// 5. Temporal smoothing to reduce musical noise artifacts
/// </remarks>
public class AIDenoiser : EffectBase
{
    // FFT configuration
    private int _fftSize;
    private int _hopSize;
    private readonly int _overlapFactor = 4;

    // FFT working buffers (per channel)
    private float[][] _inputBuffer = null!;
    private float[][] _outputBuffer = null!;
    private int[] _inputWritePos = null!;
    private int[] _outputReadPos = null!;
    private int _samplesUntilNextFrame;

    // FFT data
    private Complex[][] _fftBuffer = null!;

    // Noise profile with statistics (per channel)
    private float[][] _noiseProfileMean = null!;
    private float[][] _noiseProfileVariance = null!;
    private float[][] _noiseProfileMin = null!;
    private float[][] _noiseProfileMax = null!;
    private int _learnFrameCount;
    private const int MinLearnFrames = 20;

    // Adaptive state (per channel, per bin)
    private float[][] _adaptiveThreshold = null!;
    private float[][] _gainSmooth = null!;
    private float[][] _previousMagnitude = null!;

    // Transient detection
    private float[][] _transientEnergy = null!;
    private float[] _channelTransientFlag = null!;
    private const float TransientThreshold = 3.0f;

    // Window function
    private float[] _analysisWindow = null!;
    private float[] _synthesisWindow = null!;

    // State
    private bool _initialized;
    private AIDenoiserQuality _quality = AIDenoiserQuality.Balanced;
    private AIDenoiserMode _mode = AIDenoiserMode.Denoise;

    /// <summary>
    /// Creates a new AI denoiser effect.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    public AIDenoiser(ISampleProvider source) : this(source, "AI Denoiser")
    {
    }

    /// <summary>
    /// Creates a new AI denoiser effect with a custom name.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <param name="name">Effect name.</param>
    public AIDenoiser(ISampleProvider source, string name) : base(source, name)
    {
        // Register parameters with defaults
        RegisterParameter("Strength", 0.7f);           // 0.0-1.0: Denoising strength (0-100%)
        RegisterParameter("PreserveTransients", 1f);   // 0 or 1: Enable transient preservation
        RegisterParameter("AdaptiveRate", 0.1f);       // 0.01-0.5: How fast the model adapts
        RegisterParameter("NoiseFloor", -60f);         // -80 to -20 dB: Minimum noise level
        RegisterParameter("SpectralSmoothing", 0.5f);  // 0.0-1.0: Frequency smoothing
        RegisterParameter("TemporalSmoothing", 0.3f);  // 0.0-1.0: Time smoothing
        RegisterParameter("Mix", 1f);

        _initialized = false;
        _learnFrameCount = 0;
    }

    /// <summary>
    /// Denoising strength (0.0 - 1.0, representing 0% - 100%).
    /// Higher values remove more noise but may affect signal quality.
    /// </summary>
    public float Strength
    {
        get => GetParameter("Strength");
        set => SetParameter("Strength", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Gets or sets whether transient preservation is enabled.
    /// When enabled, the denoiser protects transients (attacks, percussion) from being reduced.
    /// </summary>
    public bool PreserveTransients
    {
        get => GetParameter("PreserveTransients") > 0.5f;
        set => SetParameter("PreserveTransients", value ? 1f : 0f);
    }

    /// <summary>
    /// Adaptive rate (0.01 - 0.5).
    /// Controls how fast the noise profile adapts to changing noise conditions.
    /// </summary>
    public float AdaptiveRate
    {
        get => GetParameter("AdaptiveRate");
        set => SetParameter("AdaptiveRate", Math.Clamp(value, 0.01f, 0.5f));
    }

    /// <summary>
    /// Noise floor in dB (-80 to -20 dB).
    /// Signals below this level are treated as potential noise.
    /// </summary>
    public float NoiseFloor
    {
        get => GetParameter("NoiseFloor");
        set => SetParameter("NoiseFloor", Math.Clamp(value, -80f, -20f));
    }

    /// <summary>
    /// Spectral smoothing amount (0.0 - 1.0).
    /// Higher values reduce isolated frequency artifacts.
    /// </summary>
    public float SpectralSmoothing
    {
        get => GetParameter("SpectralSmoothing");
        set => SetParameter("SpectralSmoothing", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Temporal smoothing amount (0.0 - 1.0).
    /// Higher values reduce musical noise but may blur transients.
    /// </summary>
    public float TemporalSmoothing
    {
        get => GetParameter("TemporalSmoothing");
        set => SetParameter("TemporalSmoothing", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Gets or sets the quality mode.
    /// </summary>
    public AIDenoiserQuality Quality
    {
        get => _quality;
        set
        {
            if (_quality != value)
            {
                _quality = value;
                _initialized = false;
            }
        }
    }

    /// <summary>
    /// Gets or sets the operating mode.
    /// </summary>
    public AIDenoiserMode Mode
    {
        get => _mode;
        set
        {
            if (_mode != value)
            {
                if (value == AIDenoiserMode.Learn)
                {
                    _learnFrameCount = 0;
                    if (_initialized)
                    {
                        ResetNoiseProfile();
                    }
                }
                _mode = value;
            }
        }
    }

    /// <summary>
    /// Gets whether the noise profile has been learned.
    /// </summary>
    public bool NoiseProfileLearned => _learnFrameCount >= MinLearnFrames;

    /// <summary>
    /// Gets the number of frames learned for the noise profile.
    /// </summary>
    public int LearnedFrameCount => _learnFrameCount;

    /// <summary>
    /// Starts learning the noise profile from input.
    /// </summary>
    public void StartLearning()
    {
        Mode = AIDenoiserMode.Learn;
    }

    /// <summary>
    /// Stops learning and switches to denoising mode.
    /// </summary>
    public void StopLearning()
    {
        if (_mode == AIDenoiserMode.Learn)
        {
            Mode = AIDenoiserMode.Denoise;
        }
    }

    /// <summary>
    /// Resets the learned noise profile.
    /// </summary>
    public void ResetNoiseProfile()
    {
        if (_noiseProfileMean != null)
        {
            float noiseFloorLinear = DbToLinear(NoiseFloor);
            for (int ch = 0; ch < Channels; ch++)
            {
                for (int k = 0; k < _noiseProfileMean[ch].Length; k++)
                {
                    _noiseProfileMean[ch][k] = noiseFloorLinear;
                    _noiseProfileVariance[ch][k] = noiseFloorLinear * 0.1f;
                    _noiseProfileMin[ch][k] = noiseFloorLinear;
                    _noiseProfileMax[ch][k] = noiseFloorLinear * 2f;
                }
            }
        }
        _learnFrameCount = 0;
    }

    /// <summary>
    /// Initializes internal buffers based on quality setting.
    /// </summary>
    private void Initialize()
    {
        int channels = Channels;

        _fftSize = _quality switch
        {
            AIDenoiserQuality.Fast => 1024,
            AIDenoiserQuality.Balanced => 2048,
            AIDenoiserQuality.Quality => 4096,
            _ => 2048
        };

        _hopSize = _fftSize / _overlapFactor;
        int halfSize = _fftSize / 2 + 1;

        // Allocate per-channel buffers
        _inputBuffer = new float[channels][];
        _outputBuffer = new float[channels][];
        _inputWritePos = new int[channels];
        _outputReadPos = new int[channels];
        _fftBuffer = new Complex[channels][];

        _noiseProfileMean = new float[channels][];
        _noiseProfileVariance = new float[channels][];
        _noiseProfileMin = new float[channels][];
        _noiseProfileMax = new float[channels][];
        _adaptiveThreshold = new float[channels][];
        _gainSmooth = new float[channels][];
        _previousMagnitude = new float[channels][];
        _transientEnergy = new float[channels][];
        _channelTransientFlag = new float[channels];

        float noiseFloorLinear = DbToLinear(NoiseFloor);

        for (int ch = 0; ch < channels; ch++)
        {
            _inputBuffer[ch] = new float[_fftSize * 2];
            _outputBuffer[ch] = new float[_fftSize * 4];
            _inputWritePos[ch] = 0;
            _outputReadPos[ch] = 0;
            _fftBuffer[ch] = new Complex[_fftSize];

            _noiseProfileMean[ch] = new float[halfSize];
            _noiseProfileVariance[ch] = new float[halfSize];
            _noiseProfileMin[ch] = new float[halfSize];
            _noiseProfileMax[ch] = new float[halfSize];
            _adaptiveThreshold[ch] = new float[halfSize];
            _gainSmooth[ch] = new float[halfSize];
            _previousMagnitude[ch] = new float[halfSize];
            _transientEnergy[ch] = new float[halfSize];

            for (int k = 0; k < halfSize; k++)
            {
                _noiseProfileMean[ch][k] = noiseFloorLinear;
                _noiseProfileVariance[ch][k] = noiseFloorLinear * 0.1f;
                _noiseProfileMin[ch][k] = noiseFloorLinear;
                _noiseProfileMax[ch][k] = noiseFloorLinear * 2f;
                _adaptiveThreshold[ch][k] = noiseFloorLinear;
                _gainSmooth[ch][k] = 1f;
            }
        }

        // Generate windows (Hann for analysis, square root Hann for synthesis)
        _analysisWindow = new float[_fftSize];
        _synthesisWindow = new float[_fftSize];
        for (int i = 0; i < _fftSize; i++)
        {
            float hannValue = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (_fftSize - 1)));
            _analysisWindow[i] = hannValue;
            _synthesisWindow[i] = hannValue; // Both Hann for overlap-add
        }

        _samplesUntilNextFrame = 0;
        _initialized = true;
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        if (!_initialized)
        {
            Initialize();
        }

        if (_mode == AIDenoiserMode.Bypass)
        {
            Array.Copy(sourceBuffer, 0, destBuffer, offset, count);
            return;
        }

        int channels = Channels;

        for (int i = 0; i < count; i += channels)
        {
            // Transient detection across all channels
            float totalEnergy = 0;
            for (int ch = 0; ch < channels; ch++)
            {
                float sample = sourceBuffer[i + ch];
                totalEnergy += sample * sample;
            }

            for (int ch = 0; ch < channels; ch++)
            {
                float inputSample = sourceBuffer[i + ch];
                _inputBuffer[ch][_inputWritePos[ch]] = inputSample;
                _inputWritePos[ch] = (_inputWritePos[ch] + 1) % _inputBuffer[ch].Length;
            }

            _samplesUntilNextFrame--;

            if (_samplesUntilNextFrame <= 0)
            {
                _samplesUntilNextFrame = _hopSize;

                // Process each channel
                for (int ch = 0; ch < channels; ch++)
                {
                    if (_mode == AIDenoiserMode.Learn)
                    {
                        ProcessLearnFrame(ch);
                    }
                    else
                    {
                        ProcessDenoiseFrame(ch);
                    }
                }

                if (_mode == AIDenoiserMode.Learn)
                {
                    _learnFrameCount++;
                }
            }

            // Read output
            for (int ch = 0; ch < channels; ch++)
            {
                float outputSample = _outputBuffer[ch][_outputReadPos[ch]];
                _outputBuffer[ch][_outputReadPos[ch]] = 0f;
                _outputReadPos[ch] = (_outputReadPos[ch] + 1) % _outputBuffer[ch].Length;
                destBuffer[offset + i + ch] = outputSample;
            }
        }
    }

    /// <summary>
    /// Processes one frame for learning noise profile with statistical modeling.
    /// </summary>
    private void ProcessLearnFrame(int channel)
    {
        int halfSize = _fftSize / 2 + 1;

        // Copy windowed input to FFT buffer
        int readStart = (_inputWritePos[channel] - _fftSize + _inputBuffer[channel].Length) % _inputBuffer[channel].Length;
        for (int i = 0; i < _fftSize; i++)
        {
            int readPos = (readStart + i) % _inputBuffer[channel].Length;
            float windowedSample = _inputBuffer[channel][readPos] * _analysisWindow[i];
            _fftBuffer[channel][i] = new Complex(windowedSample, 0f);
        }

        FFT(_fftBuffer[channel], false);

        // Update noise profile statistics using Welford's online algorithm
        for (int k = 0; k < halfSize; k++)
        {
            float real = _fftBuffer[channel][k].Real;
            float imag = _fftBuffer[channel][k].Imag;
            float magnitude = MathF.Sqrt(real * real + imag * imag);

            if (_learnFrameCount == 0)
            {
                _noiseProfileMean[channel][k] = magnitude;
                _noiseProfileVariance[channel][k] = 0;
                _noiseProfileMin[channel][k] = magnitude;
                _noiseProfileMax[channel][k] = magnitude;
            }
            else
            {
                // Welford's online algorithm for mean and variance
                float delta = magnitude - _noiseProfileMean[channel][k];
                _noiseProfileMean[channel][k] += delta / (_learnFrameCount + 1);
                float delta2 = magnitude - _noiseProfileMean[channel][k];
                _noiseProfileVariance[channel][k] += delta * delta2;

                // Track min/max
                _noiseProfileMin[channel][k] = MathF.Min(_noiseProfileMin[channel][k], magnitude);
                _noiseProfileMax[channel][k] = MathF.Max(_noiseProfileMax[channel][k], magnitude);
            }
        }

        // Pass through audio in learn mode
        float normFactor = 1f / (_overlapFactor * 0.5f);
        for (int i = 0; i < _fftSize; i++)
        {
            int readPos = (readStart + i) % _inputBuffer[channel].Length;
            int outputPos = (_outputReadPos[channel] + i) % _outputBuffer[channel].Length;
            _outputBuffer[channel][outputPos] += _inputBuffer[channel][readPos] * _synthesisWindow[i] * normFactor;
        }
    }

    /// <summary>
    /// Processes one frame for denoising with AI-like adaptive behavior.
    /// </summary>
    private void ProcessDenoiseFrame(int channel)
    {
        int halfSize = _fftSize / 2 + 1;
        float strength = Strength;
        float adaptiveRate = AdaptiveRate;
        float spectralSmoothing = SpectralSmoothing;
        float temporalSmoothing = TemporalSmoothing;
        float noiseFloorLinear = DbToLinear(NoiseFloor);
        bool preserveTransients = PreserveTransients;

        // Copy windowed input to FFT buffer
        int readStart = (_inputWritePos[channel] - _fftSize + _inputBuffer[channel].Length) % _inputBuffer[channel].Length;
        for (int i = 0; i < _fftSize; i++)
        {
            int readPos = (readStart + i) % _inputBuffer[channel].Length;
            float windowedSample = _inputBuffer[channel][readPos] * _analysisWindow[i];
            _fftBuffer[channel][i] = new Complex(windowedSample, 0f);
        }

        FFT(_fftBuffer[channel], false);

        // Compute current frame energy for transient detection
        float frameEnergy = 0;
        for (int k = 0; k < halfSize; k++)
        {
            float real = _fftBuffer[channel][k].Real;
            float imag = _fftBuffer[channel][k].Imag;
            float mag = real * real + imag * imag;
            float prevMag = _previousMagnitude[channel][k] * _previousMagnitude[channel][k];
            _transientEnergy[channel][k] = prevMag > 1e-10f ? mag / (prevMag + 1e-10f) : 1f;
            frameEnergy += _transientEnergy[channel][k];
        }
        frameEnergy /= halfSize;

        // Detect transient (sudden increase in energy)
        bool isTransient = frameEnergy > TransientThreshold;
        float transientProtection = (preserveTransients && isTransient) ? 0.5f : 0f;

        // Process each frequency bin
        float[] magnitude = new float[halfSize];
        float[] phase = new float[halfSize];
        float[] gain = new float[halfSize];

        for (int k = 0; k < halfSize; k++)
        {
            float real = _fftBuffer[channel][k].Real;
            float imag = _fftBuffer[channel][k].Imag;
            magnitude[k] = MathF.Sqrt(real * real + imag * imag);
            phase[k] = MathF.Atan2(imag, real);

            // Get noise statistics
            float noiseMean = _noiseProfileMean[channel][k];
            float noiseStd = _learnFrameCount > 1
                ? MathF.Sqrt(_noiseProfileVariance[channel][k] / _learnFrameCount)
                : noiseMean * 0.1f;

            // Adaptive threshold based on noise statistics
            // Uses mean + weighted standard deviation (AI-like confidence interval)
            float confidenceMultiplier = 1.5f + strength * 2f; // 1.5 to 3.5 std devs
            float adaptiveThresh = noiseMean + noiseStd * confidenceMultiplier;

            // Smooth threshold adaptation
            _adaptiveThreshold[channel][k] = _adaptiveThreshold[channel][k] * (1f - adaptiveRate) +
                                              adaptiveThresh * adaptiveRate;

            // Calculate gain based on spectral masking
            float threshold = MathF.Max(_adaptiveThreshold[channel][k], noiseFloorLinear);
            float signalAboveNoise = magnitude[k] - threshold * strength;

            if (signalAboveNoise > 0)
            {
                // Soft gain curve (Wiener-like filtering)
                float snr = signalAboveNoise / (threshold + 1e-10f);
                gain[k] = snr / (snr + 1f);
            }
            else
            {
                // Below noise threshold - apply stronger attenuation
                float ratio = magnitude[k] / (threshold + 1e-10f);
                gain[k] = MathF.Max(0, ratio * ratio * (1f - strength));
            }

            // Apply transient protection
            if (isTransient && _transientEnergy[channel][k] > TransientThreshold)
            {
                gain[k] = MathF.Max(gain[k], transientProtection + (1f - transientProtection) * gain[k]);
            }

            // Ensure minimum gain to prevent artifacts
            gain[k] = MathF.Max(gain[k], 0.01f);
        }

        // Apply spectral smoothing (across frequency bins)
        if (spectralSmoothing > 0.01f)
        {
            float[] smoothedGain = new float[halfSize];
            smoothedGain[0] = gain[0];
            for (int k = 1; k < halfSize - 1; k++)
            {
                smoothedGain[k] = gain[k - 1] * (spectralSmoothing / 2f) +
                                  gain[k] * (1f - spectralSmoothing) +
                                  gain[k + 1] * (spectralSmoothing / 2f);
            }
            smoothedGain[halfSize - 1] = gain[halfSize - 1];
            gain = smoothedGain;
        }

        // Apply temporal smoothing
        for (int k = 0; k < halfSize; k++)
        {
            // Asymmetric smoothing: faster attack, slower release
            float targetGain = gain[k];
            float currentSmooth = _gainSmooth[channel][k];

            if (targetGain > currentSmooth)
            {
                // Attack: faster response to increasing signal
                _gainSmooth[channel][k] = currentSmooth + (targetGain - currentSmooth) * (1f - temporalSmoothing * 0.5f);
            }
            else
            {
                // Release: slower response to decreasing signal
                _gainSmooth[channel][k] = currentSmooth + (targetGain - currentSmooth) * (1f - temporalSmoothing);
            }

            gain[k] = _gainSmooth[channel][k];
            _previousMagnitude[channel][k] = magnitude[k];
        }

        // Apply gain and reconstruct
        for (int k = 0; k < halfSize; k++)
        {
            float newMag = magnitude[k] * gain[k];
            _fftBuffer[channel][k] = new Complex(newMag * MathF.Cos(phase[k]), newMag * MathF.Sin(phase[k]));

            // Mirror for negative frequencies
            if (k > 0 && k < halfSize - 1)
            {
                _fftBuffer[channel][_fftSize - k] = new Complex(newMag * MathF.Cos(phase[k]), -newMag * MathF.Sin(phase[k]));
            }
        }

        // Inverse FFT
        FFT(_fftBuffer[channel], true);

        // Overlap-add
        float normFactor = 1f / (_overlapFactor * 0.5f);
        for (int i = 0; i < _fftSize; i++)
        {
            int outputPos = (_outputReadPos[channel] + i) % _outputBuffer[channel].Length;
            _outputBuffer[channel][outputPos] += _fftBuffer[channel][i].Real * _synthesisWindow[i] * normFactor;
        }
    }

    private static float DbToLinear(float db)
    {
        return MathF.Pow(10f, db / 20f);
    }

    /// <summary>
    /// In-place Cooley-Tukey FFT implementation.
    /// </summary>
    private static void FFT(Complex[] data, bool inverse)
    {
        int n = data.Length;
        if (n <= 1) return;

        // Bit-reversal permutation
        int j = 0;
        for (int i = 0; i < n - 1; i++)
        {
            if (i < j)
            {
                (data[i], data[j]) = (data[j], data[i]);
            }
            int m = n >> 1;
            while (j >= m && m >= 1)
            {
                j -= m;
                m >>= 1;
            }
            j += m;
        }

        // Cooley-Tukey iterative FFT
        float direction = inverse ? 1f : -1f;
        for (int len = 2; len <= n; len <<= 1)
        {
            float theta = direction * 2f * MathF.PI / len;
            Complex wn = new Complex(MathF.Cos(theta), MathF.Sin(theta));

            for (int i = 0; i < n; i += len)
            {
                Complex w = new Complex(1f, 0f);
                int halfLen = len / 2;
                for (int k = 0; k < halfLen; k++)
                {
                    Complex t = w * data[i + k + halfLen];
                    Complex u = data[i + k];
                    data[i + k] = u + t;
                    data[i + k + halfLen] = u - t;
                    w = w * wn;
                }
            }
        }

        if (inverse)
        {
            for (int i = 0; i < n; i++)
            {
                data[i] = new Complex(data[i].Real / n, data[i].Imag / n);
            }
        }
    }

    #region Presets

    /// <summary>
    /// Creates a preset for light denoising.
    /// </summary>
    public static AIDenoiser CreateLight(ISampleProvider source)
    {
        var effect = new AIDenoiser(source, "Light AI Denoise");
        effect.Strength = 0.4f;
        effect.PreserveTransients = true;
        effect.AdaptiveRate = 0.15f;
        effect.NoiseFloor = -65f;
        effect.SpectralSmoothing = 0.3f;
        effect.TemporalSmoothing = 0.2f;
        effect.Quality = AIDenoiserQuality.Balanced;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for balanced denoising.
    /// </summary>
    public static AIDenoiser CreateBalanced(ISampleProvider source)
    {
        var effect = new AIDenoiser(source, "Balanced AI Denoise");
        effect.Strength = 0.7f;
        effect.PreserveTransients = true;
        effect.AdaptiveRate = 0.1f;
        effect.NoiseFloor = -60f;
        effect.SpectralSmoothing = 0.5f;
        effect.TemporalSmoothing = 0.3f;
        effect.Quality = AIDenoiserQuality.Balanced;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for aggressive denoising.
    /// </summary>
    public static AIDenoiser CreateAggressive(ISampleProvider source)
    {
        var effect = new AIDenoiser(source, "Aggressive AI Denoise");
        effect.Strength = 0.95f;
        effect.PreserveTransients = true;
        effect.AdaptiveRate = 0.05f;
        effect.NoiseFloor = -50f;
        effect.SpectralSmoothing = 0.7f;
        effect.TemporalSmoothing = 0.5f;
        effect.Quality = AIDenoiserQuality.Quality;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset optimized for voice/dialog.
    /// </summary>
    public static AIDenoiser CreateVoice(ISampleProvider source)
    {
        var effect = new AIDenoiser(source, "Voice AI Denoise");
        effect.Strength = 0.65f;
        effect.PreserveTransients = true;
        effect.AdaptiveRate = 0.12f;
        effect.NoiseFloor = -55f;
        effect.SpectralSmoothing = 0.4f;
        effect.TemporalSmoothing = 0.25f;
        effect.Quality = AIDenoiserQuality.Quality;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset optimized for music.
    /// </summary>
    public static AIDenoiser CreateMusic(ISampleProvider source)
    {
        var effect = new AIDenoiser(source, "Music AI Denoise");
        effect.Strength = 0.5f;
        effect.PreserveTransients = true;
        effect.AdaptiveRate = 0.08f;
        effect.NoiseFloor = -70f;
        effect.SpectralSmoothing = 0.35f;
        effect.TemporalSmoothing = 0.15f;
        effect.Quality = AIDenoiserQuality.Quality;
        effect.Mix = 1f;
        return effect;
    }

    #endregion

    #region Complex Number Struct

    private readonly struct Complex
    {
        public readonly float Real;
        public readonly float Imag;

        public Complex(float real, float imag)
        {
            Real = real;
            Imag = imag;
        }

        public static Complex operator +(Complex a, Complex b)
        {
            return new Complex(a.Real + b.Real, a.Imag + b.Imag);
        }

        public static Complex operator -(Complex a, Complex b)
        {
            return new Complex(a.Real - b.Real, a.Imag - b.Imag);
        }

        public static Complex operator *(Complex a, Complex b)
        {
            return new Complex(
                a.Real * b.Real - a.Imag * b.Imag,
                a.Real * b.Imag + a.Imag * b.Real
            );
        }
    }

    #endregion
}
