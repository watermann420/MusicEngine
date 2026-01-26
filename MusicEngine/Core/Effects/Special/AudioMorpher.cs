//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Audio morphing effect that blends between two audio sources using spectral interpolation.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Special;

/// <summary>
/// Quality mode for the audio morpher algorithm.
/// </summary>
public enum AudioMorpherQuality
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
/// Morph mode defining how amplitude and phase are interpolated.
/// </summary>
public enum MorphMode
{
    /// <summary>
    /// Linear interpolation of both magnitude and phase.
    /// </summary>
    Linear,

    /// <summary>
    /// Interpolates magnitude while taking phase from the dominant source.
    /// </summary>
    MagnitudeOnly,

    /// <summary>
    /// Crossfade with logarithmic magnitude interpolation.
    /// </summary>
    Logarithmic,

    /// <summary>
    /// Spectral envelope morphing with independent fine structure.
    /// </summary>
    Formant
}

/// <summary>
/// Audio morphing effect that blends between two audio sources using FFT-based spectral interpolation.
/// Allows smooth transitions between different sounds by interpolating amplitude and phase in the frequency domain.
/// </summary>
/// <remarks>
/// Features:
/// - Real-time spectral morphing between Source A and Source B
/// - Multiple morph modes (Linear, MagnitudeOnly, Logarithmic, Formant)
/// - Formant preservation option to maintain vocal character
/// - Configurable FFT size for quality/latency tradeoff
/// - Smooth crossfade with configurable morph amount (0.0 = Source A, 1.0 = Source B)
/// </remarks>
public class AudioMorpher : EffectBase
{
    // FFT configuration
    private int _fftSize;
    private int _hopSize;
    private int _overlapFactor;

    // FFT working buffers for primary source (from EffectBase)
    private float[][] _inputBufferA = null!;
    private int[] _inputWritePosA = null!;

    // FFT working buffers for Source B
    private float[][] _inputBufferB = null!;
    private int[] _inputWritePosB = null!;
    private float[] _sourceBBuffer = Array.Empty<float>();

    // Output buffers
    private float[][] _outputBuffer = null!;
    private int[] _outputReadPos = null!;
    private int _samplesUntilNextFrame;

    // FFT data per channel
    private MorphComplex[][] _fftBufferA = null!;
    private MorphComplex[][] _fftBufferB = null!;
    private MorphComplex[][] _fftBufferOutput = null!;
    private float[][] _lastPhaseA = null!;
    private float[][] _lastPhaseB = null!;
    private float[][] _accumulatedPhase = null!;

    // Spectral envelope for formant preservation
    private float[][] _envelopeA = null!;
    private float[][] _envelopeB = null!;
    private int _envelopeOrder;

    // Analysis window
    private float[] _analysisWindow = null!;

    // Source B provider
    private ISampleProvider? _sourceB;
    private bool _hasSourceB;

    // State
    private bool _initialized;
    private AudioMorpherQuality _quality;

    /// <summary>
    /// Creates a new audio morpher effect.
    /// </summary>
    /// <param name="sourceA">Primary audio source (Source A).</param>
    public AudioMorpher(ISampleProvider sourceA) : this(sourceA, "Audio Morpher")
    {
    }

    /// <summary>
    /// Creates a new audio morpher effect with a custom name.
    /// </summary>
    /// <param name="sourceA">Primary audio source (Source A).</param>
    /// <param name="name">Effect name.</param>
    public AudioMorpher(ISampleProvider sourceA, string name) : base(sourceA, name)
    {
        RegisterParameter("MorphAmount", 0.5f);
        RegisterParameter("PreserveFormants", 0f);
        RegisterParameter("Smoothing", 0.1f);
        RegisterParameter("Mix", 1f);

        MorphMode = MorphMode.Linear;
        _quality = AudioMorpherQuality.Normal;
        _initialized = false;
    }

    /// <summary>
    /// Gets or sets the morph amount (0.0 = Source A, 1.0 = Source B).
    /// </summary>
    public float MorphAmount
    {
        get => GetParameter("MorphAmount");
        set => SetParameter("MorphAmount", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Gets or sets Source B for morphing.
    /// </summary>
    public ISampleProvider? SourceB
    {
        get => _sourceB;
        set
        {
            _sourceB = value;
            _hasSourceB = value != null;
        }
    }

    /// <summary>
    /// Gets or sets whether formants should be preserved during morphing (0.0 = off, 1.0 = full preservation).
    /// </summary>
    public float PreserveFormants
    {
        get => GetParameter("PreserveFormants");
        set => SetParameter("PreserveFormants", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Gets or sets the spectral smoothing amount (0.0 - 1.0).
    /// Higher values produce smoother but slower transitions.
    /// </summary>
    public float Smoothing
    {
        get => GetParameter("Smoothing");
        set => SetParameter("Smoothing", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Gets or sets the morph mode.
    /// </summary>
    public MorphMode MorphMode { get; set; }

    /// <summary>
    /// Gets or sets the quality mode affecting FFT size.
    /// </summary>
    public AudioMorpherQuality Quality
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
    /// Sets Source B from a float array buffer (for offline or pre-loaded audio).
    /// </summary>
    /// <param name="samples">Audio samples for Source B.</param>
    public void SetSourceBBuffer(float[] samples)
    {
        if (samples == null || samples.Length == 0)
        {
            _hasSourceB = false;
            return;
        }

        _sourceBBuffer = (float[])samples.Clone();
        _hasSourceB = true;
        _sourceB = null;
    }

    /// <summary>
    /// Feeds real-time samples for Source B (for live input).
    /// </summary>
    /// <param name="samples">Audio samples.</param>
    /// <param name="offset">Start offset.</param>
    /// <param name="count">Number of samples.</param>
    public void FeedSourceB(float[] samples, int offset, int count)
    {
        if (samples == null || count <= 0)
            return;

        if (_sourceBBuffer.Length < count)
        {
            _sourceBBuffer = new float[count * 2];
        }

        Array.Copy(samples, offset, _sourceBBuffer, 0, count);
        _hasSourceB = true;
    }

    /// <summary>
    /// Initializes internal buffers based on quality setting.
    /// </summary>
    private void Initialize()
    {
        int channels = Channels;

        _fftSize = _quality switch
        {
            AudioMorpherQuality.Fast => 1024,
            AudioMorpherQuality.Normal => 2048,
            AudioMorpherQuality.HighQuality => 4096,
            _ => 2048
        };

        _overlapFactor = 4;
        _hopSize = _fftSize / _overlapFactor;
        _envelopeOrder = Math.Min(SampleRate / 1000 + 4, _fftSize / 8);

        // Allocate per-channel buffers
        _inputBufferA = new float[channels][];
        _inputBufferB = new float[channels][];
        _inputWritePosA = new int[channels];
        _inputWritePosB = new int[channels];
        _outputBuffer = new float[channels][];
        _outputReadPos = new int[channels];

        _fftBufferA = new MorphComplex[channels][];
        _fftBufferB = new MorphComplex[channels][];
        _fftBufferOutput = new MorphComplex[channels][];
        _lastPhaseA = new float[channels][];
        _lastPhaseB = new float[channels][];
        _accumulatedPhase = new float[channels][];
        _envelopeA = new float[channels][];
        _envelopeB = new float[channels][];

        for (int ch = 0; ch < channels; ch++)
        {
            _inputBufferA[ch] = new float[_fftSize * 2];
            _inputBufferB[ch] = new float[_fftSize * 2];
            _inputWritePosA[ch] = 0;
            _inputWritePosB[ch] = 0;
            _outputBuffer[ch] = new float[_fftSize * 4];
            _outputReadPos[ch] = 0;

            _fftBufferA[ch] = new MorphComplex[_fftSize];
            _fftBufferB[ch] = new MorphComplex[_fftSize];
            _fftBufferOutput[ch] = new MorphComplex[_fftSize];
            _lastPhaseA[ch] = new float[_fftSize / 2 + 1];
            _lastPhaseB[ch] = new float[_fftSize / 2 + 1];
            _accumulatedPhase[ch] = new float[_fftSize / 2 + 1];
            _envelopeA[ch] = new float[_fftSize / 2 + 1];
            _envelopeB[ch] = new float[_fftSize / 2 + 1];
        }

        // Generate Hann window
        _analysisWindow = new float[_fftSize];
        for (int i = 0; i < _fftSize; i++)
        {
            _analysisWindow[i] = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (_fftSize - 1)));
        }

        _samplesUntilNextFrame = 0;
        _initialized = true;
    }

    /// <inheritdoc/>
    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        if (!_initialized)
        {
            Initialize();
        }

        int channels = Channels;
        float morphAmount = MorphAmount;

        // Read Source B samples if we have a provider
        float[] sourceBSamples = new float[count];
        int sourceBRead = 0;
        if (_sourceB != null)
        {
            sourceBRead = _sourceB.Read(sourceBSamples, 0, count);
        }
        else if (_hasSourceB && _sourceBBuffer.Length > 0)
        {
            // Use pre-loaded buffer (looping)
            for (int i = 0; i < count; i++)
            {
                sourceBSamples[i] = _sourceBBuffer[i % _sourceBBuffer.Length];
            }
            sourceBRead = count;
        }

        // If no Source B, just pass through Source A
        if (!_hasSourceB && _sourceB == null)
        {
            Array.Copy(sourceBuffer, 0, destBuffer, offset, count);
            return;
        }

        // Process samples
        for (int i = 0; i < count; i += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                int sampleIndex = i + ch;

                // Write to circular input buffers
                float sampleA = sourceBuffer[sampleIndex];
                float sampleB = sampleIndex < sourceBRead ? sourceBSamples[sampleIndex] : 0f;

                _inputBufferA[ch][_inputWritePosA[ch]] = sampleA;
                _inputWritePosA[ch] = (_inputWritePosA[ch] + 1) % _inputBufferA[ch].Length;

                _inputBufferB[ch][_inputWritePosB[ch]] = sampleB;
                _inputWritePosB[ch] = (_inputWritePosB[ch] + 1) % _inputBufferB[ch].Length;
            }

            _samplesUntilNextFrame--;

            // Process new frame
            if (_samplesUntilNextFrame <= 0)
            {
                _samplesUntilNextFrame = _hopSize;

                for (int ch = 0; ch < channels; ch++)
                {
                    ProcessMorphFrame(ch, morphAmount);
                }
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
    /// Processes one morph frame for a single channel.
    /// </summary>
    private void ProcessMorphFrame(int channel, float morphAmount)
    {
        int halfSize = _fftSize / 2;
        float preserveFormants = PreserveFormants;

        // Copy windowed input to FFT buffers
        int readStartA = (_inputWritePosA[channel] - _fftSize + _inputBufferA[channel].Length) % _inputBufferA[channel].Length;
        int readStartB = (_inputWritePosB[channel] - _fftSize + _inputBufferB[channel].Length) % _inputBufferB[channel].Length;

        for (int i = 0; i < _fftSize; i++)
        {
            int readPosA = (readStartA + i) % _inputBufferA[channel].Length;
            int readPosB = (readStartB + i) % _inputBufferB[channel].Length;

            float windowedA = _inputBufferA[channel][readPosA] * _analysisWindow[i];
            float windowedB = _inputBufferB[channel][readPosB] * _analysisWindow[i];

            _fftBufferA[channel][i] = new MorphComplex(windowedA, 0f);
            _fftBufferB[channel][i] = new MorphComplex(windowedB, 0f);
        }

        // Forward FFT for both sources
        FFT(_fftBufferA[channel], false);
        FFT(_fftBufferB[channel], false);

        // Extract spectral envelopes if formant preservation is enabled
        if (preserveFormants > 0f)
        {
            ExtractSpectralEnvelope(channel, _fftBufferA[channel], _envelopeA[channel]);
            ExtractSpectralEnvelope(channel, _fftBufferB[channel], _envelopeB[channel]);
        }

        // Morph spectra based on mode
        MorphSpectra(channel, morphAmount, preserveFormants);

        // Inverse FFT
        FFT(_fftBufferOutput[channel], true);

        // Overlap-add to output buffer
        float normFactor = 1f / (_overlapFactor * 0.5f);
        for (int i = 0; i < _fftSize; i++)
        {
            int outputPos = (_outputReadPos[channel] + i) % _outputBuffer[channel].Length;
            _outputBuffer[channel][outputPos] += _fftBufferOutput[channel][i].Real * _analysisWindow[i] * normFactor;
        }
    }

    /// <summary>
    /// Morphs the spectra from Source A and Source B based on morph amount and mode.
    /// </summary>
    private void MorphSpectra(int channel, float morphAmount, float preserveFormants)
    {
        int halfSize = _fftSize / 2;
        float smoothing = Smoothing;
        float smoothCoeff = 1f - smoothing * 0.99f;

        for (int k = 0; k <= halfSize; k++)
        {
            // Extract magnitude and phase from both sources
            float magA = _fftBufferA[channel][k].Magnitude;
            float magB = _fftBufferB[channel][k].Magnitude;
            float phaseA = _fftBufferA[channel][k].Phase;
            float phaseB = _fftBufferB[channel][k].Phase;

            float morphedMag;
            float morphedPhase;

            switch (MorphMode)
            {
                case MorphMode.Linear:
                    // Linear interpolation of magnitude and phase
                    morphedMag = magA * (1f - morphAmount) + magB * morphAmount;
                    morphedPhase = InterpolatePhase(phaseA, phaseB, morphAmount);
                    break;

                case MorphMode.MagnitudeOnly:
                    // Interpolate magnitude, use dominant source's phase
                    morphedMag = magA * (1f - morphAmount) + magB * morphAmount;
                    morphedPhase = morphAmount < 0.5f ? phaseA : phaseB;
                    break;

                case MorphMode.Logarithmic:
                    // Logarithmic magnitude interpolation (more natural for audio)
                    float logMagA = MathF.Log(magA + 1e-10f);
                    float logMagB = MathF.Log(magB + 1e-10f);
                    float logMorphed = logMagA * (1f - morphAmount) + logMagB * morphAmount;
                    morphedMag = MathF.Exp(logMorphed);
                    morphedPhase = InterpolatePhase(phaseA, phaseB, morphAmount);
                    break;

                case MorphMode.Formant:
                    // Morph spectral envelope separately from fine structure
                    if (preserveFormants > 0f)
                    {
                        // Get envelopes
                        float envA = _envelopeA[channel][k];
                        float envB = _envelopeB[channel][k];

                        // Morph envelope
                        float morphedEnv = envA * (1f - morphAmount) + envB * morphAmount;

                        // Get fine structure (magnitude / envelope)
                        float fineA = envA > 1e-10f ? magA / envA : 0f;
                        float fineB = envB > 1e-10f ? magB / envB : 0f;

                        // Morph fine structure
                        float morphedFine = fineA * (1f - morphAmount) + fineB * morphAmount;

                        // Reconstruct magnitude
                        morphedMag = morphedEnv * morphedFine;
                    }
                    else
                    {
                        morphedMag = magA * (1f - morphAmount) + magB * morphAmount;
                    }
                    morphedPhase = InterpolatePhase(phaseA, phaseB, morphAmount);
                    break;

                default:
                    morphedMag = magA * (1f - morphAmount) + magB * morphAmount;
                    morphedPhase = phaseA;
                    break;
            }

            // Apply formant preservation correction if enabled and not in Formant mode
            if (preserveFormants > 0f && MorphMode != MorphMode.Formant)
            {
                // Blend envelopes
                float envA = _envelopeA[channel][k];
                float envB = _envelopeB[channel][k];
                float targetEnv = envA * (1f - morphAmount) + envB * morphAmount;

                // Current morphed envelope approximation
                float currentEnv = morphedMag;

                // Apply correction
                if (currentEnv > 1e-10f)
                {
                    float correction = targetEnv / currentEnv;
                    correction = 1f + (correction - 1f) * preserveFormants;
                    correction = Math.Clamp(correction, 0.1f, 10f);
                    morphedMag *= correction;
                }
            }

            // Accumulate phase with smoothing
            float phaseDelta = morphedPhase - _accumulatedPhase[channel][k];
            phaseDelta = WrapPhase(phaseDelta);
            _accumulatedPhase[channel][k] += phaseDelta * smoothCoeff;
            _accumulatedPhase[channel][k] = WrapPhase(_accumulatedPhase[channel][k]);

            // Convert back to complex
            float finalPhase = _accumulatedPhase[channel][k];
            _fftBufferOutput[channel][k] = new MorphComplex(
                morphedMag * MathF.Cos(finalPhase),
                morphedMag * MathF.Sin(finalPhase)
            );

            // Mirror for negative frequencies (conjugate symmetric)
            if (k > 0 && k < halfSize)
            {
                _fftBufferOutput[channel][_fftSize - k] = new MorphComplex(
                    morphedMag * MathF.Cos(finalPhase),
                    -morphedMag * MathF.Sin(finalPhase)
                );
            }
        }
    }

    /// <summary>
    /// Extracts the spectral envelope using cepstral smoothing.
    /// </summary>
    private void ExtractSpectralEnvelope(int channel, MorphComplex[] fftBuffer, float[] envelope)
    {
        int halfSize = _fftSize / 2;

        // Calculate log magnitude spectrum
        MorphComplex[] cepstrum = new MorphComplex[_fftSize];
        for (int k = 0; k < _fftSize; k++)
        {
            float mag = fftBuffer[k].Magnitude;
            float logMag = MathF.Log(mag + 1e-10f);
            cepstrum[k] = new MorphComplex(logMag, 0f);
        }

        // FFT to get cepstrum
        FFT(cepstrum, false);

        // Low-pass lifter: keep only first few cepstral coefficients
        int lifterCutoff = _envelopeOrder;
        for (int i = lifterCutoff; i < _fftSize - lifterCutoff; i++)
        {
            cepstrum[i] = new MorphComplex(0f, 0f);
        }

        // Inverse FFT
        FFT(cepstrum, true);

        // Convert back to linear magnitude for envelope
        for (int k = 0; k <= halfSize; k++)
        {
            envelope[k] = MathF.Exp(cepstrum[k].Real / _fftSize);
        }
    }

    /// <summary>
    /// Interpolates between two phase values, handling wraparound.
    /// </summary>
    private static float InterpolatePhase(float phaseA, float phaseB, float amount)
    {
        // Find shortest path between phases
        float diff = phaseB - phaseA;
        diff = WrapPhase(diff);

        return WrapPhase(phaseA + diff * amount);
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
    private static void FFT(MorphComplex[] data, bool inverse)
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
            MorphComplex wn = new MorphComplex(MathF.Cos(theta), MathF.Sin(theta));

            for (int i = 0; i < n; i += len)
            {
                MorphComplex w = new MorphComplex(1f, 0f);
                int halfLen = len / 2;
                for (int k = 0; k < halfLen; k++)
                {
                    MorphComplex t = w * data[i + k + halfLen];
                    MorphComplex u = data[i + k];
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
                data[i] = new MorphComplex(data[i].Real / n, data[i].Imag / n);
            }
        }
    }

    /// <summary>
    /// Creates a preset for subtle vocal morphing between two voices.
    /// </summary>
    public static AudioMorpher CreateVocalMorph(ISampleProvider sourceA)
    {
        var morpher = new AudioMorpher(sourceA, "Vocal Morph");
        morpher.MorphAmount = 0.5f;
        morpher.PreserveFormants = 0.8f;
        morpher.MorphMode = MorphMode.Formant;
        morpher.Quality = AudioMorpherQuality.HighQuality;
        morpher.Smoothing = 0.2f;
        morpher.Mix = 1f;
        return morpher;
    }

    /// <summary>
    /// Creates a preset for instrument morphing (e.g., between synth patches).
    /// </summary>
    public static AudioMorpher CreateInstrumentMorph(ISampleProvider sourceA)
    {
        var morpher = new AudioMorpher(sourceA, "Instrument Morph");
        morpher.MorphAmount = 0.5f;
        morpher.PreserveFormants = 0f;
        morpher.MorphMode = MorphMode.Linear;
        morpher.Quality = AudioMorpherQuality.Normal;
        morpher.Smoothing = 0.1f;
        morpher.Mix = 1f;
        return morpher;
    }

    /// <summary>
    /// Creates a preset for percussive morphing with fast response.
    /// </summary>
    public static AudioMorpher CreatePercussionMorph(ISampleProvider sourceA)
    {
        var morpher = new AudioMorpher(sourceA, "Percussion Morph");
        morpher.MorphAmount = 0.5f;
        morpher.PreserveFormants = 0f;
        morpher.MorphMode = MorphMode.MagnitudeOnly;
        morpher.Quality = AudioMorpherQuality.Fast;
        morpher.Smoothing = 0.05f;
        morpher.Mix = 1f;
        return morpher;
    }

    /// <summary>
    /// Creates a preset for pad/ambient sound morphing with smooth transitions.
    /// </summary>
    public static AudioMorpher CreatePadMorph(ISampleProvider sourceA)
    {
        var morpher = new AudioMorpher(sourceA, "Pad Morph");
        morpher.MorphAmount = 0.5f;
        morpher.PreserveFormants = 0.3f;
        morpher.MorphMode = MorphMode.Logarithmic;
        morpher.Quality = AudioMorpherQuality.HighQuality;
        morpher.Smoothing = 0.5f;
        morpher.Mix = 1f;
        return morpher;
    }

    /// <summary>
    /// Creates a preset for special effects and sound design.
    /// </summary>
    public static AudioMorpher CreateSoundDesign(ISampleProvider sourceA)
    {
        var morpher = new AudioMorpher(sourceA, "Sound Design Morph");
        morpher.MorphAmount = 0.5f;
        morpher.PreserveFormants = 0.5f;
        morpher.MorphMode = MorphMode.Formant;
        morpher.Quality = AudioMorpherQuality.Normal;
        morpher.Smoothing = 0.15f;
        morpher.Mix = 1f;
        return morpher;
    }

    #region MorphComplex Struct

    /// <summary>
    /// Complex number struct for FFT operations in audio morphing.
    /// </summary>
    private readonly struct MorphComplex
    {
        public readonly float Real;
        public readonly float Imag;

        public MorphComplex(float real, float imag)
        {
            Real = real;
            Imag = imag;
        }

        /// <summary>
        /// Gets the magnitude of the complex number.
        /// </summary>
        public float Magnitude => MathF.Sqrt(Real * Real + Imag * Imag);

        /// <summary>
        /// Gets the phase of the complex number.
        /// </summary>
        public float Phase => MathF.Atan2(Imag, Real);

        public static MorphComplex operator +(MorphComplex a, MorphComplex b)
        {
            return new MorphComplex(a.Real + b.Real, a.Imag + b.Imag);
        }

        public static MorphComplex operator -(MorphComplex a, MorphComplex b)
        {
            return new MorphComplex(a.Real - b.Real, a.Imag - b.Imag);
        }

        public static MorphComplex operator *(MorphComplex a, MorphComplex b)
        {
            return new MorphComplex(
                a.Real * b.Real - a.Imag * b.Imag,
                a.Real * b.Imag + a.Imag * b.Real
            );
        }
    }

    #endregion
}
