//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Spectral freeze effect that captures and holds frequency content using FFT analysis.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Special;

/// <summary>
/// Quality modes for spectral freeze algorithm affecting FFT size and latency.
/// </summary>
public enum SpectralFreezeQuality
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
    HighQuality,

    /// <summary>
    /// Ultra quality mode with largest FFT size (8192). Highest latency, smoothest freeze.
    /// </summary>
    Ultra
}

/// <summary>
/// Spectral freeze effect that captures and holds frequency content using FFT-based analysis.
/// </summary>
/// <remarks>
/// The spectral freeze algorithm works by:
/// 1. Performing Short-Time Fourier Transform (STFT) on overlapping windows
/// 2. Capturing magnitude spectrum when freeze is enabled
/// 3. Regenerating audio from frozen magnitudes with randomized or preserved phases
/// 4. Applying spectral blur to smooth the frozen spectrum
/// 5. Crossfading between live and frozen audio for smooth transitions
/// 6. Performing inverse FFT and overlap-add reconstruction
///
/// This effect is useful for creating sustained drone textures, infinite sustain,
/// and experimental sound design.
/// </remarks>
public class SpectralFreeze : EffectBase
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

    // Frozen spectrum data (per channel)
    private float[][] _frozenMagnitude = null!;
    private float[][] _frozenPhase = null!;
    private float[][] _previousMagnitude = null!;
    private float[][] _accumulatedPhase = null!;

    // Phase randomization for frozen playback
    private float[][] _phaseIncrement = null!;
    private Random _random = null!;

    // Transition state
    private float _freezeBlend;
    private float _targetFreezeBlend;
    private float _fadeInRate;
    private float _fadeOutRate;
    private bool _hasCapture;

    // Analysis window
    private float[] _analysisWindow = null!;

    // State
    private bool _initialized;
    private SpectralFreezeQuality _quality;

    /// <summary>
    /// Creates a new spectral freeze effect.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    public SpectralFreeze(ISampleProvider source) : this(source, "Spectral Freeze")
    {
    }

    /// <summary>
    /// Creates a new spectral freeze effect with a custom name.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <param name="name">Effect name</param>
    public SpectralFreeze(ISampleProvider source, string name) : base(source, name)
    {
        // Register parameters with defaults
        RegisterParameter("FreezeEnabled", 0f);    // 0 = off, 1 = on
        RegisterParameter("FadeIn", 100f);         // 10-2000ms: Fade in time when freezing
        RegisterParameter("FadeOut", 100f);        // 10-2000ms: Fade out time when unfreezing
        RegisterParameter("SpectralBlur", 0f);     // 0.0-1.0: Amount of spectral smoothing
        RegisterParameter("Mix", 1f);              // Dry/wet mix
        RegisterParameter("Brightness", 0.5f);     // 0.0-1.0: High frequency emphasis
        RegisterParameter("PhaseMode", 0f);        // 0 = random drift, 1 = preserve original
        RegisterParameter("Jitter", 0f);           // 0.0-1.0: Adds movement to frozen spectrum

        _quality = SpectralFreezeQuality.Normal;
        _initialized = false;
        _freezeBlend = 0f;
        _targetFreezeBlend = 0f;
        _hasCapture = false;
        _random = new Random();
    }

    /// <summary>
    /// Enable or disable the freeze effect (0.0 = off, 1.0 = on).
    /// When enabled, captures and holds the current frequency content.
    /// </summary>
    public float FreezeEnabled
    {
        get => GetParameter("FreezeEnabled");
        set => SetParameter("FreezeEnabled", value >= 0.5f ? 1f : 0f);
    }

    /// <summary>
    /// Gets or sets whether freeze is currently enabled.
    /// </summary>
    public bool IsFreezeEnabled
    {
        get => FreezeEnabled >= 0.5f;
        set => FreezeEnabled = value ? 1f : 0f;
    }

    /// <summary>
    /// Fade in time in milliseconds when freezing (10 - 2000ms).
    /// Controls how quickly the frozen spectrum fades in.
    /// </summary>
    public float FadeIn
    {
        get => GetParameter("FadeIn");
        set => SetParameter("FadeIn", Math.Clamp(value, 10f, 2000f));
    }

    /// <summary>
    /// Fade out time in milliseconds when unfreezing (10 - 2000ms).
    /// Controls how quickly the frozen spectrum fades out.
    /// </summary>
    public float FadeOut
    {
        get => GetParameter("FadeOut");
        set => SetParameter("FadeOut", Math.Clamp(value, 10f, 2000f));
    }

    /// <summary>
    /// Spectral blur amount (0.0 - 1.0).
    /// Higher values smooth the frozen spectrum for a softer, more diffuse sound.
    /// </summary>
    public float SpectralBlur
    {
        get => GetParameter("SpectralBlur");
        set => SetParameter("SpectralBlur", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Brightness control (0.0 - 1.0).
    /// Controls high frequency emphasis in the frozen spectrum.
    /// </summary>
    public float Brightness
    {
        get => GetParameter("Brightness");
        set => SetParameter("Brightness", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Phase mode (0.0 = random drift, 1.0 = preserve original).
    /// Random drift creates evolving textures, preserved phase sounds more static.
    /// </summary>
    public float PhaseMode
    {
        get => GetParameter("PhaseMode");
        set => SetParameter("PhaseMode", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Jitter amount (0.0 - 1.0).
    /// Adds subtle movement and variation to the frozen spectrum.
    /// </summary>
    public float Jitter
    {
        get => GetParameter("Jitter");
        set => SetParameter("Jitter", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Gets or sets the quality mode affecting FFT size and latency.
    /// </summary>
    public SpectralFreezeQuality Quality
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
    /// Gets whether a frozen spectrum has been captured.
    /// </summary>
    public bool HasCapture => _hasCapture;

    /// <summary>
    /// Gets the current freeze blend amount (0.0 = live, 1.0 = fully frozen).
    /// </summary>
    public float CurrentFreezeBlend => _freezeBlend;

    /// <summary>
    /// Captures the current spectrum immediately without waiting for transition.
    /// </summary>
    public void CaptureNow()
    {
        // Will capture on next frame
        _hasCapture = false;
    }

    /// <summary>
    /// Clears the captured frozen spectrum.
    /// </summary>
    public void ClearCapture()
    {
        _hasCapture = false;
        if (_frozenMagnitude != null)
        {
            for (int ch = 0; ch < Channels; ch++)
            {
                Array.Clear(_frozenMagnitude[ch], 0, _frozenMagnitude[ch].Length);
                Array.Clear(_frozenPhase[ch], 0, _frozenPhase[ch].Length);
            }
        }
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
            SpectralFreezeQuality.Fast => 1024,
            SpectralFreezeQuality.Normal => 2048,
            SpectralFreezeQuality.HighQuality => 4096,
            SpectralFreezeQuality.Ultra => 8192,
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
        _frozenMagnitude = new float[channels][];
        _frozenPhase = new float[channels][];
        _previousMagnitude = new float[channels][];
        _accumulatedPhase = new float[channels][];
        _phaseIncrement = new float[channels][];

        for (int ch = 0; ch < channels; ch++)
        {
            // Input buffer needs to hold at least one full FFT frame
            _inputBuffer[ch] = new float[_fftSize * 2];
            // Output buffer for overlap-add
            _outputBuffer[ch] = new float[_fftSize * 4];
            _inputWritePos[ch] = 0;
            _outputReadPos[ch] = 0;

            _fftBuffer[ch] = new Complex[_fftSize];
            _frozenMagnitude[ch] = new float[halfSize];
            _frozenPhase[ch] = new float[halfSize];
            _previousMagnitude[ch] = new float[halfSize];
            _accumulatedPhase[ch] = new float[halfSize];
            _phaseIncrement[ch] = new float[halfSize];

            // Initialize phase increments for random drift
            for (int k = 0; k < halfSize; k++)
            {
                // Phase increment based on bin frequency
                float freq = k * (float)SampleRate / _fftSize;
                _phaseIncrement[ch][k] = 2f * MathF.PI * freq * _hopSize / SampleRate;
                // Add slight random variation
                _phaseIncrement[ch][k] *= 1f + ((float)_random.NextDouble() - 0.5f) * 0.01f;
            }
        }

        // Generate Hann window
        _analysisWindow = new float[_fftSize];
        for (int i = 0; i < _fftSize; i++)
        {
            _analysisWindow[i] = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (_fftSize - 1)));
        }

        // Calculate fade rates based on hop size
        UpdateFadeRates();

        _samplesUntilNextFrame = 0;
        _initialized = true;
    }

    /// <summary>
    /// Updates fade in/out rates based on current parameters.
    /// </summary>
    private void UpdateFadeRates()
    {
        if (!_initialized) return;

        // Calculate fade rate per hop (how much blend changes per FFT frame)
        float framesPerSecond = (float)SampleRate / _hopSize;
        float fadeInFrames = FadeIn / 1000f * framesPerSecond;
        float fadeOutFrames = FadeOut / 1000f * framesPerSecond;

        _fadeInRate = fadeInFrames > 0 ? 1f / fadeInFrames : 1f;
        _fadeOutRate = fadeOutFrames > 0 ? 1f / fadeOutFrames : 1f;
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        if (!_initialized)
        {
            Initialize();
        }

        int channels = Channels;
        bool freezeEnabled = IsFreezeEnabled;

        // Update target blend
        _targetFreezeBlend = freezeEnabled ? 1f : 0f;

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

                // Update freeze blend towards target
                if (_freezeBlend < _targetFreezeBlend)
                {
                    _freezeBlend = MathF.Min(_freezeBlend + _fadeInRate, _targetFreezeBlend);
                }
                else if (_freezeBlend > _targetFreezeBlend)
                {
                    _freezeBlend = MathF.Max(_freezeBlend - _fadeOutRate, _targetFreezeBlend);
                }

                // Process spectral freeze for each channel
                for (int ch = 0; ch < channels; ch++)
                {
                    ProcessSpectralFreezeFrame(ch, freezeEnabled);
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
    /// Processes one spectral freeze frame for a single channel.
    /// </summary>
    private void ProcessSpectralFreezeFrame(int channel, bool freezeEnabled)
    {
        int halfSize = _fftSize / 2 + 1;
        float spectralBlur = SpectralBlur;
        float brightness = Brightness;
        float phaseMode = PhaseMode;
        float jitter = Jitter;

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

        // Extract magnitude and phase from current frame
        float[] liveMagnitude = new float[halfSize];
        float[] livePhase = new float[halfSize];

        for (int k = 0; k < halfSize; k++)
        {
            float real = _fftBuffer[channel][k].Real;
            float imag = _fftBuffer[channel][k].Imag;

            liveMagnitude[k] = MathF.Sqrt(real * real + imag * imag);
            livePhase[k] = MathF.Atan2(imag, real);
        }

        // Capture frozen spectrum if transitioning into freeze and no capture yet
        if (freezeEnabled && !_hasCapture)
        {
            CaptureSpectrum(channel, liveMagnitude, livePhase);
            if (channel == Channels - 1)
            {
                _hasCapture = true;
            }
        }

        // Calculate output magnitude and phase
        float[] outputMagnitude = new float[halfSize];
        float[] outputPhase = new float[halfSize];

        for (int k = 0; k < halfSize; k++)
        {
            // Get frozen magnitude with blur applied
            float frozenMag = GetBlurredMagnitude(channel, k, spectralBlur);

            // Apply brightness control (frequency-dependent gain)
            float freqFactor = (float)k / halfSize;
            float brightnessGain = 1f + (brightness - 0.5f) * 2f * freqFactor;
            brightnessGain = MathF.Max(0.1f, brightnessGain);
            frozenMag *= brightnessGain;

            // Apply jitter (subtle random modulation)
            if (jitter > 0f)
            {
                float jitterAmount = 1f + (float)(_random.NextDouble() - 0.5) * jitter * 0.2f;
                frozenMag *= jitterAmount;
            }

            // Blend between live and frozen magnitude
            outputMagnitude[k] = liveMagnitude[k] * (1f - _freezeBlend) + frozenMag * _freezeBlend;

            // Calculate phase
            if (_freezeBlend > 0f)
            {
                // Update accumulated phase for frozen playback
                _accumulatedPhase[channel][k] += _phaseIncrement[channel][k];
                _accumulatedPhase[channel][k] = WrapPhase(_accumulatedPhase[channel][k]);

                // Blend between live phase and frozen/accumulated phase
                float frozenPhaseValue;
                if (phaseMode > 0.5f)
                {
                    // Preserve original phase
                    frozenPhaseValue = _frozenPhase[channel][k];
                }
                else
                {
                    // Use accumulated (drifting) phase
                    frozenPhaseValue = _accumulatedPhase[channel][k];
                }

                // Interpolate phase (using complex number for smooth blending)
                float liveWeight = 1f - _freezeBlend;
                float frozenWeight = _freezeBlend;

                float liveX = MathF.Cos(livePhase[k]) * liveWeight;
                float liveY = MathF.Sin(livePhase[k]) * liveWeight;
                float frozenX = MathF.Cos(frozenPhaseValue) * frozenWeight;
                float frozenY = MathF.Sin(frozenPhaseValue) * frozenWeight;

                outputPhase[k] = MathF.Atan2(liveY + frozenY, liveX + frozenX);
            }
            else
            {
                outputPhase[k] = livePhase[k];
            }
        }

        // Reconstruct complex spectrum
        for (int k = 0; k < halfSize; k++)
        {
            float mag = outputMagnitude[k];
            float ph = outputPhase[k];
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
    /// Captures the current spectrum for freezing.
    /// </summary>
    private void CaptureSpectrum(int channel, float[] magnitude, float[] phase)
    {
        int halfSize = _fftSize / 2 + 1;

        // Copy magnitude and phase
        Array.Copy(magnitude, _frozenMagnitude[channel], halfSize);
        Array.Copy(phase, _frozenPhase[channel], halfSize);

        // Initialize accumulated phase from frozen phase
        Array.Copy(phase, _accumulatedPhase[channel], halfSize);
    }

    /// <summary>
    /// Gets the blurred (smoothed) magnitude at a given bin.
    /// </summary>
    private float GetBlurredMagnitude(int channel, int bin, float blur)
    {
        if (blur <= 0f || !_hasCapture)
        {
            return _frozenMagnitude[channel][bin];
        }

        int halfSize = _fftSize / 2 + 1;

        // Calculate blur kernel width based on blur amount
        int kernelHalf = (int)(blur * 20) + 1;

        float sum = 0f;
        float weightSum = 0f;

        for (int i = -kernelHalf; i <= kernelHalf; i++)
        {
            int idx = bin + i;
            if (idx >= 0 && idx < halfSize)
            {
                // Gaussian-like weighting
                float dist = (float)Math.Abs(i) / kernelHalf;
                float weight = MathF.Exp(-dist * dist * 2f);
                sum += _frozenMagnitude[channel][idx] * weight;
                weightSum += weight;
            }
        }

        return weightSum > 0f ? sum / weightSum : _frozenMagnitude[channel][bin];
    }

    /// <summary>
    /// Wraps a phase value to the range [-PI, PI].
    /// </summary>
    private static float WrapPhase(float phase)
    {
        while (phase > MathF.PI) phase -= 2f * MathF.PI;
        while (phase < -MathF.PI) phase += 2f * MathF.PI;
        return phase;
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
        if (name.Equals("FadeIn", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("FadeOut", StringComparison.OrdinalIgnoreCase))
        {
            UpdateFadeRates();
        }

        if (name.Equals("FreezeEnabled", StringComparison.OrdinalIgnoreCase))
        {
            _targetFreezeBlend = value >= 0.5f ? 1f : 0f;
        }
    }

    #region Presets

    /// <summary>
    /// Creates a preset for smooth infinite sustain.
    /// Suitable for creating drone textures from any sound.
    /// </summary>
    public static SpectralFreeze CreateInfiniteSustain(ISampleProvider source)
    {
        var effect = new SpectralFreeze(source, "Infinite Sustain");
        effect.FadeIn = 200f;
        effect.FadeOut = 500f;
        effect.SpectralBlur = 0.3f;
        effect.Brightness = 0.5f;
        effect.PhaseMode = 0f; // Random drift for evolving texture
        effect.Jitter = 0.1f;
        effect.Quality = SpectralFreezeQuality.HighQuality;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for ambient pad textures.
    /// Heavily blurred for soft, diffuse pads.
    /// </summary>
    public static SpectralFreeze CreateAmbientPad(ISampleProvider source)
    {
        var effect = new SpectralFreeze(source, "Ambient Pad");
        effect.FadeIn = 500f;
        effect.FadeOut = 1000f;
        effect.SpectralBlur = 0.7f;
        effect.Brightness = 0.4f;
        effect.PhaseMode = 0f;
        effect.Jitter = 0.2f;
        effect.Quality = SpectralFreezeQuality.Ultra;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for quick freeze transitions.
    /// Fast attack/release for rhythmic freeze effects.
    /// </summary>
    public static SpectralFreeze CreateQuickFreeze(ISampleProvider source)
    {
        var effect = new SpectralFreeze(source, "Quick Freeze");
        effect.FadeIn = 20f;
        effect.FadeOut = 50f;
        effect.SpectralBlur = 0.1f;
        effect.Brightness = 0.5f;
        effect.PhaseMode = 1f; // Preserve phase for clearer transients
        effect.Jitter = 0f;
        effect.Quality = SpectralFreezeQuality.Fast;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for bright frozen textures.
    /// Enhanced high frequencies for shimmering effect.
    /// </summary>
    public static SpectralFreeze CreateShimmerFreeze(ISampleProvider source)
    {
        var effect = new SpectralFreeze(source, "Shimmer Freeze");
        effect.FadeIn = 150f;
        effect.FadeOut = 300f;
        effect.SpectralBlur = 0.4f;
        effect.Brightness = 0.8f;
        effect.PhaseMode = 0f;
        effect.Jitter = 0.15f;
        effect.Quality = SpectralFreezeQuality.HighQuality;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for dark frozen textures.
    /// Reduced high frequencies for warm, dark drones.
    /// </summary>
    public static SpectralFreeze CreateDarkFreeze(ISampleProvider source)
    {
        var effect = new SpectralFreeze(source, "Dark Freeze");
        effect.FadeIn = 300f;
        effect.FadeOut = 600f;
        effect.SpectralBlur = 0.5f;
        effect.Brightness = 0.2f;
        effect.PhaseMode = 0f;
        effect.Jitter = 0.05f;
        effect.Quality = SpectralFreezeQuality.HighQuality;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for static frozen sound.
    /// Preserved phase for more natural, static freeze.
    /// </summary>
    public static SpectralFreeze CreateStaticFreeze(ISampleProvider source)
    {
        var effect = new SpectralFreeze(source, "Static Freeze");
        effect.FadeIn = 100f;
        effect.FadeOut = 200f;
        effect.SpectralBlur = 0.2f;
        effect.Brightness = 0.5f;
        effect.PhaseMode = 1f; // Preserve original phase
        effect.Jitter = 0f;
        effect.Quality = SpectralFreezeQuality.Normal;
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
