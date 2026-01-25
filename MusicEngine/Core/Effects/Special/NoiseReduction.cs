//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Real-time noise reduction effect using spectral subtraction algorithm.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Special;

/// <summary>
/// Noise reduction mode determining the processing behavior.
/// </summary>
public enum NoiseReductionMode
{
    /// <summary>
    /// Normal mode - actively reducing noise using learned or estimated noise profile.
    /// </summary>
    Normal,

    /// <summary>
    /// Learn mode - capturing noise profile from input signal (e.g., during silence or noise-only sections).
    /// </summary>
    Learn,

    /// <summary>
    /// Bypass mode - passes audio through without processing (for A/B comparison).
    /// </summary>
    Bypass
}

/// <summary>
/// Quality preset for noise reduction algorithm affecting FFT size and latency.
/// </summary>
public enum NoiseReductionQuality
{
    /// <summary>
    /// Fast mode with smaller FFT size (1024). Lower latency, lower quality.
    /// </summary>
    Fast,

    /// <summary>
    /// Normal mode with medium FFT size (2048). Balanced latency and quality.
    /// </summary>
    Normal,

    /// <summary>
    /// High quality mode with larger FFT size (4096). Higher latency, best quality.
    /// </summary>
    HighQuality
}

/// <summary>
/// Real-time noise reduction effect using spectral subtraction.
/// </summary>
/// <remarks>
/// The spectral subtraction algorithm works by:
/// 1. Performing Short-Time Fourier Transform (STFT) on overlapping windows
/// 2. Estimating or using a learned noise spectrum
/// 3. Subtracting the noise magnitude from the signal magnitude in each frequency bin
/// 4. Applying spectral smoothing to reduce musical noise artifacts
/// 5. Performing inverse FFT and overlap-add reconstruction
///
/// The effect includes adaptive attack/release envelope following to prevent
/// artifacts when noise levels change suddenly.
/// </remarks>
public class NoiseReduction : EffectBase
{
    // FFT configuration
    private int _fftSize;
    private int _hopSize;
    private int _overlapFactor;

    // FFT working buffers (per channel)
    private float[][] _inputBuffer = null!;
    private float[][] _outputBuffer = null!;
    private int[] _inputWritePos = null!;
    private int[] _outputReadPos = null!;
    private int _samplesUntilNextFrame;

    // FFT data (per channel)
    private Complex[][] _fftBuffer = null!;

    // Noise profile (per channel)
    private float[][] _noiseProfile = null!;
    private float[][] _smoothedNoiseProfile = null!;
    private int _learnFrameCount;
    private const int MinLearnFrames = 10;

    // Envelope followers for attack/release (per channel, per bin)
    private float[][] _envelopeState = null!;

    // Analysis window
    private float[] _analysisWindow = null!;

    // State
    private bool _initialized;
    private NoiseReductionQuality _quality;
    private NoiseReductionMode _mode;

    // Smoothing history for spectral smoothing
    private float[][] _previousMagnitude = null!;

    /// <summary>
    /// Creates a new noise reduction effect.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    public NoiseReduction(ISampleProvider source) : this(source, "Noise Reduction")
    {
    }

    /// <summary>
    /// Creates a new noise reduction effect with a custom name.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <param name="name">Effect name</param>
    public NoiseReduction(ISampleProvider source, string name) : base(source, name)
    {
        // Register parameters with defaults
        RegisterParameter("NoiseThreshold", 0.5f);      // 0.0-1.0: Sensitivity for noise detection
        RegisterParameter("Reduction", 0.8f);           // 0.0-1.0: Amount of noise reduction
        RegisterParameter("Attack", 10f);               // 1-100ms: How fast to respond to noise
        RegisterParameter("Release", 100f);             // 10-500ms: How fast to recover
        RegisterParameter("NoiseFloor", -60f);          // -80 to -20 dB: Minimum noise level
        RegisterParameter("Smoothing", 0.5f);           // 0.0-1.0: Spectral smoothing amount
        RegisterParameter("Mix", 1f);                   // Dry/wet mix

        _quality = NoiseReductionQuality.Normal;
        _mode = NoiseReductionMode.Normal;
        _initialized = false;
        _learnFrameCount = 0;
    }

    /// <summary>
    /// Noise detection threshold (0.0 - 1.0).
    /// Higher values require stronger signals to be considered non-noise.
    /// </summary>
    public float NoiseThreshold
    {
        get => GetParameter("NoiseThreshold");
        set => SetParameter("NoiseThreshold", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Amount of noise reduction applied (0.0 - 1.0).
    /// Higher values remove more noise but may introduce artifacts.
    /// </summary>
    public float Reduction
    {
        get => GetParameter("Reduction");
        set => SetParameter("Reduction", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Attack time in milliseconds (1 - 100ms).
    /// Controls how quickly the effect responds to increasing noise levels.
    /// </summary>
    public float Attack
    {
        get => GetParameter("Attack");
        set => SetParameter("Attack", Math.Clamp(value, 1f, 100f));
    }

    /// <summary>
    /// Release time in milliseconds (10 - 500ms).
    /// Controls how quickly the effect recovers when noise decreases.
    /// </summary>
    public float Release
    {
        get => GetParameter("Release");
        set => SetParameter("Release", Math.Clamp(value, 10f, 500f));
    }

    /// <summary>
    /// Noise floor in dB (-80 to -20 dB).
    /// Signals below this level are treated as noise.
    /// </summary>
    public float NoiseFloor
    {
        get => GetParameter("NoiseFloor");
        set => SetParameter("NoiseFloor", Math.Clamp(value, -80f, -20f));
    }

    /// <summary>
    /// Spectral smoothing amount (0.0 - 1.0).
    /// Higher values reduce musical noise artifacts but may blur transients.
    /// </summary>
    public float Smoothing
    {
        get => GetParameter("Smoothing");
        set => SetParameter("Smoothing", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Gets or sets the current operating mode.
    /// </summary>
    public NoiseReductionMode Mode
    {
        get => _mode;
        set
        {
            if (_mode != value)
            {
                if (value == NoiseReductionMode.Learn)
                {
                    // Reset learn state when entering learn mode
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
    /// Gets or sets the quality mode affecting FFT size and latency.
    /// </summary>
    public NoiseReductionQuality Quality
    {
        get => _quality;
        set
        {
            if (_quality != value)
            {
                _quality = value;
                _initialized = false; // Force reinitialization
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
    /// Call this when the input contains only noise (e.g., silence with background noise).
    /// </summary>
    public void StartLearning()
    {
        Mode = NoiseReductionMode.Learn;
    }

    /// <summary>
    /// Stops learning and switches to normal noise reduction mode.
    /// </summary>
    public void StopLearning()
    {
        if (_mode == NoiseReductionMode.Learn)
        {
            Mode = NoiseReductionMode.Normal;
        }
    }

    /// <summary>
    /// Resets the learned noise profile.
    /// </summary>
    public void ResetNoiseProfile()
    {
        if (_noiseProfile != null)
        {
            for (int ch = 0; ch < Channels; ch++)
            {
                Array.Clear(_noiseProfile[ch], 0, _noiseProfile[ch].Length);
                Array.Clear(_smoothedNoiseProfile[ch], 0, _smoothedNoiseProfile[ch].Length);
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

        // Set FFT size based on quality
        _fftSize = _quality switch
        {
            NoiseReductionQuality.Fast => 1024,
            NoiseReductionQuality.Normal => 2048,
            NoiseReductionQuality.HighQuality => 4096,
            _ => 2048
        };

        // Overlap factor: 4x for good quality (75% overlap)
        _overlapFactor = 4;
        _hopSize = _fftSize / _overlapFactor;

        int halfSize = _fftSize / 2 + 1;

        // Allocate per-channel buffers
        _inputBuffer = new float[channels][];
        _outputBuffer = new float[channels][];
        _inputWritePos = new int[channels];
        _outputReadPos = new int[channels];
        _fftBuffer = new Complex[channels][];
        _noiseProfile = new float[channels][];
        _smoothedNoiseProfile = new float[channels][];
        _envelopeState = new float[channels][];
        _previousMagnitude = new float[channels][];

        for (int ch = 0; ch < channels; ch++)
        {
            // Input buffer needs to hold at least one full FFT frame
            _inputBuffer[ch] = new float[_fftSize * 2];
            // Output buffer for overlap-add
            _outputBuffer[ch] = new float[_fftSize * 4];
            _inputWritePos[ch] = 0;
            _outputReadPos[ch] = 0;

            _fftBuffer[ch] = new Complex[_fftSize];
            _noiseProfile[ch] = new float[halfSize];
            _smoothedNoiseProfile[ch] = new float[halfSize];
            _envelopeState[ch] = new float[halfSize];
            _previousMagnitude[ch] = new float[halfSize];

            // Initialize envelope state with noise floor
            float noiseFloorLinear = DbToLinear(NoiseFloor);
            for (int k = 0; k < halfSize; k++)
            {
                _envelopeState[ch][k] = noiseFloorLinear;
                _noiseProfile[ch][k] = noiseFloorLinear;
            }
        }

        // Generate Hann window
        _analysisWindow = new float[_fftSize];
        for (int i = 0; i < _fftSize; i++)
        {
            _analysisWindow[i] = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (_fftSize - 1)));
        }

        _samplesUntilNextFrame = 0;
        _learnFrameCount = 0;
        _initialized = true;
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        if (!_initialized)
        {
            Initialize();
        }

        // Bypass mode - just copy input to output
        if (_mode == NoiseReductionMode.Bypass)
        {
            Array.Copy(sourceBuffer, 0, destBuffer, offset, count);
            return;
        }

        int channels = Channels;

        // Calculate envelope coefficients
        float attackCoef = MathF.Exp(-1f / (Attack * SampleRate / 1000f / _hopSize));
        float releaseCoef = MathF.Exp(-1f / (Release * SampleRate / 1000f / _hopSize));

        // Process interleaved samples
        for (int i = 0; i < count; i += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                int sampleIndex = i + ch;
                float inputSample = sourceBuffer[sampleIndex];

                // Write to circular input buffer
                _inputBuffer[ch][_inputWritePos[ch]] = inputSample;
                _inputWritePos[ch] = (_inputWritePos[ch] + 1) % _inputBuffer[ch].Length;
            }

            _samplesUntilNextFrame--;

            // Time to process a new frame?
            if (_samplesUntilNextFrame <= 0)
            {
                _samplesUntilNextFrame = _hopSize;

                // Process spectral subtraction for each channel
                for (int ch = 0; ch < channels; ch++)
                {
                    if (_mode == NoiseReductionMode.Learn)
                    {
                        ProcessLearnFrame(ch);
                    }
                    else
                    {
                        ProcessNoiseReductionFrame(ch, attackCoef, releaseCoef);
                    }
                }

                if (_mode == NoiseReductionMode.Learn)
                {
                    _learnFrameCount++;
                }
            }

            // Read from output buffer
            for (int ch = 0; ch < channels; ch++)
            {
                float outputSample = _outputBuffer[ch][_outputReadPos[ch]];
                _outputBuffer[ch][_outputReadPos[ch]] = 0f; // Clear after reading
                _outputReadPos[ch] = (_outputReadPos[ch] + 1) % _outputBuffer[ch].Length;

                destBuffer[offset + i + ch] = outputSample;
            }
        }
    }

    /// <summary>
    /// Processes one frame for learning noise profile.
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

        // Forward FFT
        FFT(_fftBuffer[channel], false);

        // Update noise profile with running average
        float alpha = _learnFrameCount == 0 ? 1f : 1f / (_learnFrameCount + 1f);

        for (int k = 0; k < halfSize; k++)
        {
            float real = _fftBuffer[channel][k].Real;
            float imag = _fftBuffer[channel][k].Imag;
            float magnitude = MathF.Sqrt(real * real + imag * imag);

            // Running average for noise profile
            _noiseProfile[channel][k] = _noiseProfile[channel][k] * (1f - alpha) + magnitude * alpha;
        }

        // Update smoothed noise profile
        UpdateSmoothedNoiseProfile(channel);

        // In learn mode, pass audio through unmodified (overlap-add the input)
        float normFactor = 1f / (_overlapFactor * 0.5f);
        for (int i = 0; i < _fftSize; i++)
        {
            int readPos = (readStart + i) % _inputBuffer[channel].Length;
            int outputPos = (_outputReadPos[channel] + i) % _outputBuffer[channel].Length;
            _outputBuffer[channel][outputPos] += _inputBuffer[channel][readPos] * _analysisWindow[i] * normFactor;
        }
    }

    /// <summary>
    /// Updates the smoothed noise profile for better noise estimation.
    /// </summary>
    private void UpdateSmoothedNoiseProfile(int channel)
    {
        int halfSize = _fftSize / 2 + 1;
        float smoothing = Smoothing * 0.9f + 0.05f; // Map to 0.05-0.95 for frequency smoothing

        // Apply frequency-domain smoothing
        _smoothedNoiseProfile[channel][0] = _noiseProfile[channel][0];
        for (int k = 1; k < halfSize - 1; k++)
        {
            _smoothedNoiseProfile[channel][k] = _noiseProfile[channel][k - 1] * (smoothing / 2f) +
                                                 _noiseProfile[channel][k] * (1f - smoothing) +
                                                 _noiseProfile[channel][k + 1] * (smoothing / 2f);
        }
        _smoothedNoiseProfile[channel][halfSize - 1] = _noiseProfile[channel][halfSize - 1];
    }

    /// <summary>
    /// Processes one frame for noise reduction using spectral subtraction.
    /// </summary>
    private void ProcessNoiseReductionFrame(int channel, float attackCoef, float releaseCoef)
    {
        int halfSize = _fftSize / 2 + 1;
        float threshold = NoiseThreshold;
        float reduction = Reduction;
        float smoothing = Smoothing;
        float noiseFloorLinear = DbToLinear(NoiseFloor);

        // Copy windowed input to FFT buffer
        int readStart = (_inputWritePos[channel] - _fftSize + _inputBuffer[channel].Length) % _inputBuffer[channel].Length;
        for (int i = 0; i < _fftSize; i++)
        {
            int readPos = (readStart + i) % _inputBuffer[channel].Length;
            float windowedSample = _inputBuffer[channel][readPos] * _analysisWindow[i];
            _fftBuffer[channel][i] = new Complex(windowedSample, 0f);
        }

        // Forward FFT
        FFT(_fftBuffer[channel], false);

        // Spectral subtraction
        float[] magnitude = new float[halfSize];
        float[] phase = new float[halfSize];

        for (int k = 0; k < halfSize; k++)
        {
            float real = _fftBuffer[channel][k].Real;
            float imag = _fftBuffer[channel][k].Imag;

            magnitude[k] = MathF.Sqrt(real * real + imag * imag);
            phase[k] = MathF.Atan2(imag, real);

            // Get noise estimate (use smoothed profile if available)
            float noiseEstimate = _smoothedNoiseProfile[channel][k];
            if (noiseEstimate < noiseFloorLinear)
            {
                noiseEstimate = noiseFloorLinear;
            }

            // Scale noise estimate by threshold
            noiseEstimate *= (1f + threshold * 2f);

            // Spectral subtraction with oversubtraction factor
            float oversubFactor = 1f + reduction * 3f; // 1x to 4x oversubtraction
            float subtracted = magnitude[k] - noiseEstimate * oversubFactor;

            // Spectral floor to prevent negative magnitudes (musical noise reduction)
            float spectralFloor = noiseEstimate * (1f - reduction) * 0.1f;
            subtracted = MathF.Max(subtracted, spectralFloor);

            // Apply envelope following for smooth transitions
            float envTarget = subtracted;
            if (envTarget > _envelopeState[channel][k])
            {
                // Attack - noise increasing
                _envelopeState[channel][k] = _envelopeState[channel][k] * attackCoef +
                                              envTarget * (1f - attackCoef);
            }
            else
            {
                // Release - noise decreasing
                _envelopeState[channel][k] = _envelopeState[channel][k] * releaseCoef +
                                              envTarget * (1f - releaseCoef);
            }

            // Apply temporal smoothing between frames
            float smoothedMag = _previousMagnitude[channel][k] * smoothing +
                               _envelopeState[channel][k] * (1f - smoothing);
            _previousMagnitude[channel][k] = smoothedMag;

            magnitude[k] = smoothedMag;
        }

        // Reconstruct complex spectrum
        for (int k = 0; k < halfSize; k++)
        {
            float mag = magnitude[k];
            float ph = phase[k];
            _fftBuffer[channel][k] = new Complex(mag * MathF.Cos(ph), mag * MathF.Sin(ph));

            // Mirror for negative frequencies (conjugate symmetric)
            if (k > 0 && k < halfSize - 1)
            {
                _fftBuffer[channel][_fftSize - k] = new Complex(mag * MathF.Cos(ph), -mag * MathF.Sin(ph));
            }
        }

        // Inverse FFT
        FFT(_fftBuffer[channel], true);

        // Overlap-add to output buffer
        float normFactor = 1f / (_overlapFactor * 0.5f);
        for (int i = 0; i < _fftSize; i++)
        {
            int outputPos = (_outputReadPos[channel] + i) % _outputBuffer[channel].Length;
            _outputBuffer[channel][outputPos] += _fftBuffer[channel][i].Real * _analysisWindow[i] * normFactor;
        }
    }

    /// <summary>
    /// Converts decibels to linear amplitude.
    /// </summary>
    private static float DbToLinear(float db)
    {
        return MathF.Pow(10f, db / 20f);
    }

    /// <summary>
    /// In-place Cooley-Tukey FFT implementation.
    /// </summary>
    /// <param name="data">Complex array (length must be power of 2)</param>
    /// <param name="inverse">True for inverse FFT</param>
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

        // Scale for inverse FFT
        if (inverse)
        {
            for (int i = 0; i < n; i++)
            {
                data[i] = new Complex(data[i].Real / n, data[i].Imag / n);
            }
        }
    }

    protected override void OnParameterChanged(string name, float value)
    {
        // Update noise floor in envelope state if changed
        if (name.Equals("NoiseFloor", StringComparison.OrdinalIgnoreCase) && _initialized)
        {
            float noiseFloorLinear = DbToLinear(value);
            for (int ch = 0; ch < Channels; ch++)
            {
                for (int k = 0; k < _envelopeState[ch].Length; k++)
                {
                    if (_envelopeState[ch][k] < noiseFloorLinear)
                    {
                        _envelopeState[ch][k] = noiseFloorLinear;
                    }
                    if (_noiseProfile[ch][k] < noiseFloorLinear)
                    {
                        _noiseProfile[ch][k] = noiseFloorLinear;
                    }
                }
            }
        }
    }

    #region Presets

    /// <summary>
    /// Creates a preset for light noise reduction.
    /// Suitable for removing subtle background noise without affecting the signal.
    /// </summary>
    public static NoiseReduction CreateLight(ISampleProvider source)
    {
        var effect = new NoiseReduction(source, "Light Noise Reduction");
        effect.NoiseThreshold = 0.3f;
        effect.Reduction = 0.5f;
        effect.Attack = 5f;
        effect.Release = 50f;
        effect.NoiseFloor = -70f;
        effect.Smoothing = 0.3f;
        effect.Quality = NoiseReductionQuality.Normal;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for medium noise reduction.
    /// Balanced settings for general-purpose noise removal.
    /// </summary>
    public static NoiseReduction CreateMedium(ISampleProvider source)
    {
        var effect = new NoiseReduction(source, "Medium Noise Reduction");
        effect.NoiseThreshold = 0.5f;
        effect.Reduction = 0.7f;
        effect.Attack = 10f;
        effect.Release = 100f;
        effect.NoiseFloor = -60f;
        effect.Smoothing = 0.5f;
        effect.Quality = NoiseReductionQuality.Normal;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for heavy noise reduction.
    /// Aggressive noise removal for severely noisy recordings.
    /// </summary>
    public static NoiseReduction CreateHeavy(ISampleProvider source)
    {
        var effect = new NoiseReduction(source, "Heavy Noise Reduction");
        effect.NoiseThreshold = 0.7f;
        effect.Reduction = 0.95f;
        effect.Attack = 20f;
        effect.Release = 200f;
        effect.NoiseFloor = -50f;
        effect.Smoothing = 0.7f;
        effect.Quality = NoiseReductionQuality.HighQuality;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset optimized for voice cleanup.
    /// Removes background noise while preserving speech clarity.
    /// </summary>
    public static NoiseReduction CreateVoiceCleanup(ISampleProvider source)
    {
        var effect = new NoiseReduction(source, "Voice Cleanup");
        effect.NoiseThreshold = 0.4f;
        effect.Reduction = 0.75f;
        effect.Attack = 8f;
        effect.Release = 80f;
        effect.NoiseFloor = -55f;
        effect.Smoothing = 0.4f;
        effect.Quality = NoiseReductionQuality.HighQuality;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for music restoration.
    /// Gentle noise reduction preserving musical dynamics and transients.
    /// </summary>
    public static NoiseReduction CreateMusicRestoration(ISampleProvider source)
    {
        var effect = new NoiseReduction(source, "Music Restoration");
        effect.NoiseThreshold = 0.35f;
        effect.Reduction = 0.6f;
        effect.Attack = 3f;
        effect.Release = 40f;
        effect.NoiseFloor = -65f;
        effect.Smoothing = 0.25f;
        effect.Quality = NoiseReductionQuality.HighQuality;
        effect.Mix = 1f;
        return effect;
    }

    #endregion

    #region Complex Number Struct

    /// <summary>
    /// Simple complex number struct for FFT operations.
    /// </summary>
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
