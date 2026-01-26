// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio restoration processor.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Restoration;

/// <summary>
/// Voice activity detection mode.
/// </summary>
public enum VoiceActivityMode
{
    /// <summary>
    /// Automatic voice detection using energy and spectral analysis.
    /// </summary>
    Automatic,

    /// <summary>
    /// Manual mode - always apply full processing.
    /// </summary>
    AlwaysOn,

    /// <summary>
    /// External control via API.
    /// </summary>
    External
}

/// <summary>
/// De-reverb intensity preset.
/// </summary>
public enum DeReverbIntensity
{
    /// <summary>
    /// No de-reverb processing.
    /// </summary>
    Off,

    /// <summary>
    /// Light de-reverb for subtle room tone.
    /// </summary>
    Light,

    /// <summary>
    /// Medium de-reverb for moderate room sound.
    /// </summary>
    Medium,

    /// <summary>
    /// Heavy de-reverb for highly reverberant spaces.
    /// </summary>
    Heavy
}

/// <summary>
/// Speech/podcast noise reduction with voice activity detection and de-reverb.
/// </summary>
/// <remarks>
/// Features:
/// - Voice activity detection for intelligent processing
/// - Adaptive noise floor estimation
/// - Frequency-selective attenuation preserving speech clarity
/// - Optional de-reverb processing
/// - Optimized for dialogue/podcast content
/// </remarks>
public class DialogueDenoiser : EffectBase
{
    // FFT configuration
    private int _fftSize = 2048;
    private int _hopSize;
    private float[] _analysisWindow = null!;
    private Complex[][] _fftBuffer = null!;

    // Processing buffers (per channel)
    private float[][] _inputBuffer = null!;
    private float[][] _outputBuffer = null!;
    private int[] _inputWritePos = null!;
    private int[] _outputReadPos = null!;
    private int _samplesUntilNextFrame;

    // Noise estimation
    private float[][] _noiseFloor = null!;
    private float[][] _noiseFloorSmooth = null!;
    private float[][] _signalEnvelope = null!;
    private const int NoiseUpdateFrames = 50;
    private int _frameCount;

    // Voice activity detection
    private float _vadEnergy;
    private float _vadThreshold;
    private float _vadSmoothed;
    private bool _voiceActive;
    private float[] _vadHistory = null!;
    private int _vadHistoryIndex;
    private const int VadHistorySize = 10;

    // Speech frequency bands for VAD
    private const float SpeechLowFreq = 300f;
    private const float SpeechHighFreq = 3400f;
    private const float SibilantLowFreq = 4000f;
    private const float SibilantHighFreq = 8000f;

    // De-reverb state
    private float[][] _spectralFlux = null!;
    private float[][] _previousMagnitude = null!;

    // Smoothing states
    private float[][] _gainSmooth = null!;

    private bool _initialized;

    /// <summary>
    /// Creates a new dialogue denoiser effect.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    public DialogueDenoiser(ISampleProvider source) : this(source, "Dialogue Denoiser")
    {
    }

    /// <summary>
    /// Creates a new dialogue denoiser effect with a custom name.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <param name="name">Effect name.</param>
    public DialogueDenoiser(ISampleProvider source, string name) : base(source, name)
    {
        RegisterParameter("NoiseReduction", 0.7f);
        RegisterParameter("VoiceActivity", (float)VoiceActivityMode.Automatic);
        RegisterParameter("DeReverb", (float)DeReverbIntensity.Off);
        RegisterParameter("DeReverbAmount", 0.5f);
        RegisterParameter("HighPassFreq", 80f);
        RegisterParameter("SibilancePreserve", 0.7f);
        RegisterParameter("Attack", 5f);
        RegisterParameter("Release", 50f);
        RegisterParameter("NoiseFloorAdapt", 0.5f);
        RegisterParameter("Mix", 1f);

        _initialized = false;
    }

    /// <summary>
    /// Gets or sets the noise reduction amount (0-1).
    /// </summary>
    public float NoiseReduction
    {
        get => GetParameter("NoiseReduction");
        set => SetParameter("NoiseReduction", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Gets or sets the voice activity detection mode.
    /// </summary>
    public VoiceActivityMode VoiceActivityMode
    {
        get => (VoiceActivityMode)GetParameter("VoiceActivity");
        set => SetParameter("VoiceActivity", (float)value);
    }

    /// <summary>
    /// Gets or sets the de-reverb intensity.
    /// </summary>
    public DeReverbIntensity DeReverb
    {
        get => (DeReverbIntensity)GetParameter("DeReverb");
        set => SetParameter("DeReverb", (float)value);
    }

    /// <summary>
    /// Gets or sets the de-reverb amount (0-1).
    /// </summary>
    public float DeReverbAmount
    {
        get => GetParameter("DeReverbAmount");
        set => SetParameter("DeReverbAmount", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Gets or sets the high-pass filter frequency in Hz.
    /// </summary>
    public float HighPassFrequency
    {
        get => GetParameter("HighPassFreq");
        set => SetParameter("HighPassFreq", Math.Clamp(value, 20f, 300f));
    }

    /// <summary>
    /// Gets or sets the sibilance preservation amount (0-1).
    /// Higher values preserve more high-frequency speech content.
    /// </summary>
    public float SibilancePreserve
    {
        get => GetParameter("SibilancePreserve");
        set => SetParameter("SibilancePreserve", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Gets or sets the attack time in milliseconds.
    /// </summary>
    public float Attack
    {
        get => GetParameter("Attack");
        set => SetParameter("Attack", Math.Clamp(value, 1f, 50f));
    }

    /// <summary>
    /// Gets or sets the release time in milliseconds.
    /// </summary>
    public float Release
    {
        get => GetParameter("Release");
        set => SetParameter("Release", Math.Clamp(value, 10f, 500f));
    }

    /// <summary>
    /// Gets or sets the noise floor adaptation rate (0-1).
    /// Higher values adapt faster to changing noise conditions.
    /// </summary>
    public float NoiseFloorAdaptation
    {
        get => GetParameter("NoiseFloorAdapt");
        set => SetParameter("NoiseFloorAdapt", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Gets whether voice is currently detected.
    /// </summary>
    public bool IsVoiceActive => _voiceActive;

    /// <summary>
    /// Gets the current voice activity level (0-1).
    /// </summary>
    public float VoiceActivityLevel => _vadSmoothed;

    /// <summary>
    /// Sets external voice activity state (when mode is External).
    /// </summary>
    public void SetVoiceActive(bool active)
    {
        if (VoiceActivityMode == VoiceActivityMode.External)
        {
            _voiceActive = active;
        }
    }

    /// <summary>
    /// Initializes internal buffers.
    /// </summary>
    private void Initialize()
    {
        int channels = Channels;
        _hopSize = _fftSize / 4;
        int halfSize = _fftSize / 2 + 1;

        // Allocate per-channel buffers
        _inputBuffer = new float[channels][];
        _outputBuffer = new float[channels][];
        _inputWritePos = new int[channels];
        _outputReadPos = new int[channels];
        _fftBuffer = new Complex[channels][];
        _noiseFloor = new float[channels][];
        _noiseFloorSmooth = new float[channels][];
        _signalEnvelope = new float[channels][];
        _spectralFlux = new float[channels][];
        _previousMagnitude = new float[channels][];
        _gainSmooth = new float[channels][];

        for (int ch = 0; ch < channels; ch++)
        {
            _inputBuffer[ch] = new float[_fftSize * 2];
            _outputBuffer[ch] = new float[_fftSize * 4];
            _inputWritePos[ch] = 0;
            _outputReadPos[ch] = 0;
            _fftBuffer[ch] = new Complex[_fftSize];
            _noiseFloor[ch] = new float[halfSize];
            _noiseFloorSmooth[ch] = new float[halfSize];
            _signalEnvelope[ch] = new float[halfSize];
            _spectralFlux[ch] = new float[halfSize];
            _previousMagnitude[ch] = new float[halfSize];
            _gainSmooth[ch] = new float[halfSize];

            // Initialize noise floor estimate
            float initialNoise = 1e-6f;
            for (int k = 0; k < halfSize; k++)
            {
                _noiseFloor[ch][k] = initialNoise;
                _noiseFloorSmooth[ch][k] = initialNoise;
                _gainSmooth[ch][k] = 1f;
            }
        }

        // Generate Hann window
        _analysisWindow = new float[_fftSize];
        for (int i = 0; i < _fftSize; i++)
        {
            _analysisWindow[i] = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (_fftSize - 1)));
        }

        // VAD initialization
        _vadHistory = new float[VadHistorySize];
        _vadHistoryIndex = 0;
        _vadThreshold = 0.02f;
        _vadSmoothed = 0f;
        _voiceActive = false;

        _samplesUntilNextFrame = 0;
        _frameCount = 0;
        _initialized = true;
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        if (!_initialized)
        {
            Initialize();
        }

        int channels = Channels;

        // Calculate envelope coefficients
        float attackCoef = MathF.Exp(-1f / (Attack * SampleRate / 1000f / _hopSize));
        float releaseCoef = MathF.Exp(-1f / (Release * SampleRate / 1000f / _hopSize));

        // Process samples
        for (int i = 0; i < count; i += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                float inputSample = sourceBuffer[i + ch];

                // Write to input buffer
                _inputBuffer[ch][_inputWritePos[ch]] = inputSample;
                _inputWritePos[ch] = (_inputWritePos[ch] + 1) % _inputBuffer[ch].Length;
            }

            _samplesUntilNextFrame--;

            // Process spectral frame
            if (_samplesUntilNextFrame <= 0)
            {
                _samplesUntilNextFrame = _hopSize;

                // Update VAD before processing
                UpdateVoiceActivityDetection();

                for (int ch = 0; ch < channels; ch++)
                {
                    ProcessSpectralFrame(ch, attackCoef, releaseCoef);
                }

                _frameCount++;
            }

            // Read from output buffer
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
    /// Updates voice activity detection.
    /// </summary>
    private void UpdateVoiceActivityDetection()
    {
        if (VoiceActivityMode == VoiceActivityMode.AlwaysOn)
        {
            _voiceActive = true;
            _vadSmoothed = 1f;
            return;
        }

        if (VoiceActivityMode == VoiceActivityMode.External)
        {
            return;
        }

        // Calculate energy in speech band
        float freqResolution = (float)SampleRate / _fftSize;
        int speechLowBin = (int)(SpeechLowFreq / freqResolution);
        int speechHighBin = (int)(SpeechHighFreq / freqResolution);
        int halfSize = _fftSize / 2 + 1;

        speechLowBin = Math.Clamp(speechLowBin, 1, halfSize - 1);
        speechHighBin = Math.Clamp(speechHighBin, speechLowBin + 1, halfSize - 1);

        float speechEnergy = 0f;
        float totalEnergy = 0f;

        // Use first channel for VAD
        if (_fftBuffer[0] != null)
        {
            for (int k = 1; k < halfSize; k++)
            {
                float mag = MathF.Sqrt(_fftBuffer[0][k].Real * _fftBuffer[0][k].Real +
                                       _fftBuffer[0][k].Imag * _fftBuffer[0][k].Imag);
                float power = mag * mag;
                totalEnergy += power;

                if (k >= speechLowBin && k <= speechHighBin)
                {
                    speechEnergy += power;
                }
            }
        }

        // Speech-to-total energy ratio
        float speechRatio = totalEnergy > 1e-10f ? speechEnergy / totalEnergy : 0f;

        // Update VAD history
        _vadHistory[_vadHistoryIndex] = speechRatio;
        _vadHistoryIndex = (_vadHistoryIndex + 1) % VadHistorySize;

        // Smoothed VAD
        float avgRatio = 0f;
        for (int i = 0; i < VadHistorySize; i++)
        {
            avgRatio += _vadHistory[i];
        }
        avgRatio /= VadHistorySize;

        // Adaptive threshold
        _vadEnergy = avgRatio;
        float vadDecision = avgRatio > _vadThreshold ? 1f : 0f;

        // Smooth the VAD decision
        float vadSmoothCoef = vadDecision > _vadSmoothed ? 0.9f : 0.95f;
        _vadSmoothed = _vadSmoothed * vadSmoothCoef + vadDecision * (1f - vadSmoothCoef);

        _voiceActive = _vadSmoothed > 0.5f;
    }

    /// <summary>
    /// Processes a single spectral frame.
    /// </summary>
    private void ProcessSpectralFrame(int channel, float attackCoef, float releaseCoef)
    {
        int halfSize = _fftSize / 2 + 1;
        float freqResolution = (float)SampleRate / _fftSize;

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

        // Extract magnitudes and phases
        float[] magnitude = new float[halfSize];
        float[] phase = new float[halfSize];

        for (int k = 0; k < halfSize; k++)
        {
            float real = _fftBuffer[channel][k].Real;
            float imag = _fftBuffer[channel][k].Imag;
            magnitude[k] = MathF.Sqrt(real * real + imag * imag);
            phase[k] = MathF.Atan2(imag, real);
        }

        // Update noise floor estimate during non-voice periods
        UpdateNoiseFloor(channel, magnitude, halfSize);

        // Apply dialogue-optimized noise reduction
        float reduction = NoiseReduction;
        float sibilancePreserve = SibilancePreserve;
        float highPassFreq = HighPassFrequency;

        int highPassBin = (int)(highPassFreq / freqResolution);
        int sibilantLowBin = (int)(SibilantLowFreq / freqResolution);
        int sibilantHighBin = (int)(SibilantHighFreq / freqResolution);

        for (int k = 0; k < halfSize; k++)
        {
            float freq = k * freqResolution;

            // High-pass filter
            if (k < highPassBin)
            {
                float hpGain = (float)k / highPassBin;
                hpGain = hpGain * hpGain; // Smooth rolloff
                magnitude[k] *= hpGain;
                continue;
            }

            // Calculate gain reduction
            float noiseEstimate = _noiseFloorSmooth[channel][k];
            float snr = magnitude[k] / (noiseEstimate + 1e-10f);

            // Spectral subtraction with over-subtraction
            float overSubFactor = 2f + reduction * 4f;
            float gain = MathF.Max(0f, 1f - (noiseEstimate * overSubFactor) / (magnitude[k] + 1e-10f));

            // Preserve sibilance
            if (k >= sibilantLowBin && k <= sibilantHighBin)
            {
                float sibilanceBoost = sibilancePreserve * 0.5f;
                gain = gain * (1f - sibilanceBoost) + sibilanceBoost;
            }

            // Apply voice activity modulation
            if (!_voiceActive && VoiceActivityMode != VoiceActivityMode.AlwaysOn)
            {
                // More aggressive reduction during non-speech
                gain *= (1f - reduction * 0.5f);
            }

            // Smooth gain changes
            float targetGain = gain;
            float smoothCoef = targetGain > _gainSmooth[channel][k] ? attackCoef : releaseCoef;
            _gainSmooth[channel][k] = _gainSmooth[channel][k] * smoothCoef + targetGain * (1f - smoothCoef);

            magnitude[k] *= _gainSmooth[channel][k];
        }

        // Apply de-reverb if enabled
        if (DeReverb != DeReverbIntensity.Off)
        {
            ApplyDeReverb(channel, magnitude, halfSize);
        }

        // Store for next frame
        Array.Copy(magnitude, _previousMagnitude[channel], halfSize);

        // Reconstruct complex spectrum
        for (int k = 0; k < halfSize; k++)
        {
            float mag = magnitude[k];
            float ph = phase[k];
            _fftBuffer[channel][k] = new Complex(mag * MathF.Cos(ph), mag * MathF.Sin(ph));

            // Mirror for negative frequencies
            if (k > 0 && k < halfSize - 1)
            {
                _fftBuffer[channel][_fftSize - k] = new Complex(mag * MathF.Cos(ph), -mag * MathF.Sin(ph));
            }
        }

        // Inverse FFT
        FFT(_fftBuffer[channel], true);

        // Overlap-add to output buffer
        float normFactor = 1f / (4f * 0.5f);
        for (int i = 0; i < _fftSize; i++)
        {
            int outputPos = (_outputReadPos[channel] + i) % _outputBuffer[channel].Length;
            _outputBuffer[channel][outputPos] += _fftBuffer[channel][i].Real * _analysisWindow[i] * normFactor;
        }
    }

    /// <summary>
    /// Updates the noise floor estimate.
    /// </summary>
    private void UpdateNoiseFloor(int channel, float[] magnitude, int halfSize)
    {
        float adaptRate = NoiseFloorAdaptation * 0.01f;

        // Update noise floor only during non-voice periods or initial frames
        bool updateNoise = !_voiceActive || _frameCount < NoiseUpdateFrames;

        for (int k = 0; k < halfSize; k++)
        {
            if (updateNoise)
            {
                // Minimum statistics approach
                if (magnitude[k] < _noiseFloor[channel][k] || _frameCount < NoiseUpdateFrames)
                {
                    _noiseFloor[channel][k] = magnitude[k];
                }
                else
                {
                    // Slowly increase noise floor
                    _noiseFloor[channel][k] *= (1f + adaptRate * 0.1f);
                }
            }

            // Smooth the noise floor
            _noiseFloorSmooth[channel][k] = _noiseFloorSmooth[channel][k] * 0.95f +
                                             _noiseFloor[channel][k] * 0.05f;
        }
    }

    /// <summary>
    /// Applies de-reverb processing.
    /// </summary>
    private void ApplyDeReverb(int channel, float[] magnitude, int halfSize)
    {
        float amount = DeReverbAmount;
        float intensity = DeReverb switch
        {
            DeReverbIntensity.Light => 0.3f,
            DeReverbIntensity.Medium => 0.5f,
            DeReverbIntensity.Heavy => 0.8f,
            _ => 0f
        };

        float effectAmount = amount * intensity;

        for (int k = 0; k < halfSize; k++)
        {
            // Calculate spectral flux (transient detection)
            float flux = magnitude[k] - _previousMagnitude[channel][k];
            flux = MathF.Max(0f, flux);

            // Smooth flux
            _spectralFlux[channel][k] = _spectralFlux[channel][k] * 0.9f + flux * 0.1f;

            // Reduce sustained energy (reverb tail) while preserving transients
            float transientness = flux / (magnitude[k] + 1e-10f);
            transientness = Math.Clamp(transientness, 0f, 1f);

            // Apply de-reverb: reduce magnitude for sustained (non-transient) content
            float deReverbGain = 1f - effectAmount * (1f - transientness);
            magnitude[k] *= deReverbGain;
        }
    }

    /// <summary>
    /// In-place Cooley-Tukey FFT implementation.
    /// </summary>
    private static void FFT(Complex[] data, bool inverse)
    {
        int n = data.Length;
        if (n <= 1) return;

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

        public static Complex operator +(Complex a, Complex b) =>
            new Complex(a.Real + b.Real, a.Imag + b.Imag);

        public static Complex operator -(Complex a, Complex b) =>
            new Complex(a.Real - b.Real, a.Imag - b.Imag);

        public static Complex operator *(Complex a, Complex b) =>
            new Complex(a.Real * b.Real - a.Imag * b.Imag,
                        a.Real * b.Imag + a.Imag * b.Real);
    }

    #endregion

    #region Presets

    /// <summary>
    /// Creates a preset for podcast editing.
    /// </summary>
    public static DialogueDenoiser CreatePodcastPreset(ISampleProvider source)
    {
        var effect = new DialogueDenoiser(source, "Podcast Cleanup");
        effect.NoiseReduction = 0.6f;
        effect.HighPassFrequency = 80f;
        effect.SibilancePreserve = 0.8f;
        effect.DeReverb = DeReverbIntensity.Light;
        effect.DeReverbAmount = 0.4f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for interview/dialogue cleanup.
    /// </summary>
    public static DialogueDenoiser CreateInterviewPreset(ISampleProvider source)
    {
        var effect = new DialogueDenoiser(source, "Interview Cleanup");
        effect.NoiseReduction = 0.7f;
        effect.HighPassFrequency = 100f;
        effect.SibilancePreserve = 0.7f;
        effect.DeReverb = DeReverbIntensity.Medium;
        effect.DeReverbAmount = 0.5f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for voiceover/narration.
    /// </summary>
    public static DialogueDenoiser CreateVoiceoverPreset(ISampleProvider source)
    {
        var effect = new DialogueDenoiser(source, "Voiceover Cleanup");
        effect.NoiseReduction = 0.8f;
        effect.HighPassFrequency = 60f;
        effect.SibilancePreserve = 0.6f;
        effect.Attack = 3f;
        effect.Release = 30f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for reverberant room cleanup.
    /// </summary>
    public static DialogueDenoiser CreateRoomyPreset(ISampleProvider source)
    {
        var effect = new DialogueDenoiser(source, "Roomy Cleanup");
        effect.NoiseReduction = 0.5f;
        effect.DeReverb = DeReverbIntensity.Heavy;
        effect.DeReverbAmount = 0.7f;
        effect.SibilancePreserve = 0.5f;
        return effect;
    }

    #endregion
}
