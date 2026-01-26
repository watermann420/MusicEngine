// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Special;

/// <summary>
/// Quality modes for pitch shifting algorithm.
/// </summary>
public enum PitchShifterQuality
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
/// Real-time pitch shifter effect using phase vocoder technique.
/// Changes pitch without affecting tempo using STFT analysis/synthesis.
/// </summary>
/// <remarks>
/// The phase vocoder implementation:
/// 1. Performs Short-Time Fourier Transform (STFT) on overlapping windows
/// 2. Analyzes magnitude and phase of each frequency bin
/// 3. Calculates instantaneous frequency from phase differences
/// 4. Scales frequencies by the pitch ratio
/// 5. Accumulates phase for synthesis
/// 6. Performs inverse FFT and overlap-add reconstruction
///
/// For formant preservation, envelope detection is performed to maintain
/// the spectral shape (vocal character) while shifting the pitch.
/// </remarks>
public class PitchShifterEffect : EffectBase
{
    // FFT size based on quality mode
    private int _fftSize;
    private int _hopSize;
    private int _overlapFactor;

    // FFT working buffers
    private float[][] _inputBuffer = null!;        // Circular input buffer per channel
    private float[][] _outputBuffer = null!;       // Overlap-add output buffer per channel
    private int[] _inputWritePos = null!;          // Write position in input buffer
    private int[] _outputReadPos = null!;          // Read position in output buffer
    private int _samplesUntilNextFrame;    // Counter for hop size

    // FFT data (per channel)
    private Complex[][] _fftBuffer = null!;        // FFT working buffer
    private float[][] _lastInputPhase = null!;     // Phase from previous frame
    private float[][] _accumulatedPhase = null!;   // Accumulated output phase

    // Analysis window
    private float[] _analysisWindow = null!;       // Hann window

    // Formant preservation
    private float[][] _spectralEnvelope = null!;   // Smoothed spectral envelope for formants
    private int _envelopeOrder;            // LPC order for envelope estimation

    // State
    private bool _initialized;
    private PitchShifterQuality _quality;

    /// <summary>
    /// Creates a new pitch shifter effect.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    public PitchShifterEffect(ISampleProvider source) : this(source, "Pitch Shifter")
    {
    }

    /// <summary>
    /// Creates a new pitch shifter effect with a custom name.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <param name="name">Effect name</param>
    public PitchShifterEffect(ISampleProvider source, string name) : base(source, name)
    {
        // Register parameters with defaults
        RegisterParameter("Semitones", 0f);           // -24 to +24
        RegisterParameter("Cents", 0f);               // -100 to +100
        RegisterParameter("FormantPreserve", 0f);     // 0 = off, 1 = on
        RegisterParameter("WindowSize", 0.5f);        // 0-1 maps to quality modes
        RegisterParameter("Mix", 1f);                 // Dry/wet mix

        _quality = PitchShifterQuality.Normal;
        _initialized = false;
    }

    /// <summary>
    /// Pitch shift amount in semitones (-24 to +24).
    /// </summary>
    public float Semitones
    {
        get => GetParameter("Semitones");
        set => SetParameter("Semitones", Math.Clamp(value, -24f, 24f));
    }

    /// <summary>
    /// Fine tune in cents (-100 to +100).
    /// 100 cents = 1 semitone.
    /// </summary>
    public float Cents
    {
        get => GetParameter("Cents");
        set => SetParameter("Cents", Math.Clamp(value, -100f, 100f));
    }

    /// <summary>
    /// Enable formant preservation (0.0 = off, 1.0 = full preservation).
    /// When enabled, maintains the spectral envelope (vocal character) during pitch shift.
    /// </summary>
    public float FormantPreserve
    {
        get => GetParameter("FormantPreserve");
        set => SetParameter("FormantPreserve", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Window/grain size control (0.0 - 1.0).
    /// Affects transient response: smaller = better transients, larger = smoother.
    /// </summary>
    public float WindowSize
    {
        get => GetParameter("WindowSize");
        set => SetParameter("WindowSize", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Quality mode selection.
    /// </summary>
    public PitchShifterQuality Quality
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
    /// Gets the total pitch ratio based on semitones and cents.
    /// </summary>
    private float PitchRatio
    {
        get
        {
            float totalSemitones = Semitones + Cents / 100f;
            return MathF.Pow(2f, totalSemitones / 12f);
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
            PitchShifterQuality.Fast => 1024,
            PitchShifterQuality.Normal => 2048,
            PitchShifterQuality.HighQuality => 4096,
            _ => 2048
        };

        // Overlap factor: 4x for good quality (75% overlap)
        _overlapFactor = 4;
        _hopSize = _fftSize / _overlapFactor;

        // Envelope order for formant estimation (based on sample rate)
        _envelopeOrder = Math.Min(SampleRate / 1000 + 4, _fftSize / 8);

        // Allocate per-channel buffers
        _inputBuffer = new float[channels][];
        _outputBuffer = new float[channels][];
        _inputWritePos = new int[channels];
        _outputReadPos = new int[channels];
        _fftBuffer = new Complex[channels][];
        _lastInputPhase = new float[channels][];
        _accumulatedPhase = new float[channels][];
        _spectralEnvelope = new float[channels][];

        for (int ch = 0; ch < channels; ch++)
        {
            // Input buffer needs to hold at least one full FFT frame
            _inputBuffer[ch] = new float[_fftSize * 2];
            // Output buffer for overlap-add (needs room for max pitch up shift)
            _outputBuffer[ch] = new float[_fftSize * 4];
            _inputWritePos[ch] = 0;
            _outputReadPos[ch] = 0;

            _fftBuffer[ch] = new Complex[_fftSize];
            _lastInputPhase[ch] = new float[_fftSize / 2 + 1];
            _accumulatedPhase[ch] = new float[_fftSize / 2 + 1];
            _spectralEnvelope[ch] = new float[_fftSize / 2 + 1];
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

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        if (!_initialized)
        {
            Initialize();
        }

        int channels = Channels;
        float pitchRatio = PitchRatio;
        float formantAmount = FormantPreserve;

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

                // Process phase vocoder for each channel
                for (int ch = 0; ch < channels; ch++)
                {
                    ProcessPhaseVocoderFrame(ch, pitchRatio, formantAmount);
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
    /// Processes one phase vocoder frame for a single channel.
    /// </summary>
    private void ProcessPhaseVocoderFrame(int channel, float pitchRatio, float formantAmount)
    {
        int halfSize = _fftSize / 2;
        float freqPerBin = (float)SampleRate / _fftSize;
        float expectedPhaseDiff = 2f * MathF.PI * _hopSize / _fftSize;

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

        // Extract spectral envelope for formant preservation
        if (formantAmount > 0f)
        {
            ExtractSpectralEnvelope(channel);
        }

        // Analysis: Calculate magnitude and true frequency for each bin
        float[] magnitude = new float[halfSize + 1];
        float[] trueFreq = new float[halfSize + 1];

        for (int k = 0; k <= halfSize; k++)
        {
            float real = _fftBuffer[channel][k].Real;
            float imag = _fftBuffer[channel][k].Imag;

            magnitude[k] = MathF.Sqrt(real * real + imag * imag);
            float phase = MathF.Atan2(imag, real);

            // Calculate phase difference from last frame
            float phaseDiff = phase - _lastInputPhase[channel][k];
            _lastInputPhase[channel][k] = phase;

            // Remove expected phase increment
            phaseDiff -= k * expectedPhaseDiff;

            // Wrap to [-PI, PI]
            phaseDiff = WrapPhase(phaseDiff);

            // Calculate true frequency (deviation from bin center)
            float deviation = phaseDiff * _overlapFactor / (2f * MathF.PI);
            trueFreq[k] = k + deviation;
        }

        // Synthesis: Shift frequencies and accumulate phase
        float[] newMagnitude = new float[halfSize + 1];
        float[] newPhase = new float[halfSize + 1];

        // Clear new magnitudes
        Array.Clear(newMagnitude, 0, newMagnitude.Length);

        // Resample spectrum according to pitch ratio
        for (int k = 0; k <= halfSize; k++)
        {
            // Target bin after pitch shift
            int targetBin = (int)MathF.Round(k * pitchRatio);

            if (targetBin >= 0 && targetBin <= halfSize)
            {
                // Accumulate magnitude (handle multiple sources mapping to same target)
                newMagnitude[targetBin] += magnitude[k];

                // Accumulate phase based on scaled frequency
                float scaledFreq = trueFreq[k] * pitchRatio;
                float phaseDelta = scaledFreq * expectedPhaseDiff;
                _accumulatedPhase[channel][targetBin] += phaseDelta;
                _accumulatedPhase[channel][targetBin] = WrapPhase(_accumulatedPhase[channel][targetBin]);
                newPhase[targetBin] = _accumulatedPhase[channel][targetBin];
            }
        }

        // Apply formant correction if enabled
        if (formantAmount > 0f)
        {
            ApplyFormantCorrection(channel, newMagnitude, pitchRatio, formantAmount);
        }

        // Convert back to complex for inverse FFT
        for (int k = 0; k <= halfSize; k++)
        {
            float mag = newMagnitude[k];
            float ph = newPhase[k];
            _fftBuffer[channel][k] = new Complex(mag * MathF.Cos(ph), mag * MathF.Sin(ph));

            // Mirror for negative frequencies (conjugate symmetric)
            if (k > 0 && k < halfSize)
            {
                _fftBuffer[channel][_fftSize - k] = new Complex(mag * MathF.Cos(ph), -mag * MathF.Sin(ph));
            }
        }

        // Inverse FFT
        FFT(_fftBuffer[channel], true);

        // Overlap-add to output buffer
        float normFactor = 1f / (_overlapFactor * 0.5f); // Synthesis window normalization
        for (int i = 0; i < _fftSize; i++)
        {
            int outputPos = (_outputReadPos[channel] + i) % _outputBuffer[channel].Length;
            _outputBuffer[channel][outputPos] += _fftBuffer[channel][i].Real * _analysisWindow[i] * normFactor;
        }
    }

    /// <summary>
    /// Extracts the spectral envelope using cepstral smoothing for formant preservation.
    /// </summary>
    private void ExtractSpectralEnvelope(int channel)
    {
        int halfSize = _fftSize / 2;

        // Calculate log magnitude spectrum
        float[] logMag = new float[_fftSize];
        for (int k = 0; k < _fftSize; k++)
        {
            float mag = MathF.Sqrt(_fftBuffer[channel][k].Real * _fftBuffer[channel][k].Real +
                                   _fftBuffer[channel][k].Imag * _fftBuffer[channel][k].Imag);
            logMag[k] = MathF.Log(mag + 1e-10f);
        }

        // Apply low-pass liftering in cepstral domain for envelope
        // This smooths the spectrum to extract formants
        Complex[] cepstrum = new Complex[_fftSize];
        for (int i = 0; i < _fftSize; i++)
        {
            cepstrum[i] = new Complex(logMag[i], 0f);
        }

        FFT(cepstrum, false);

        // Low-pass lifter: keep only first few cepstral coefficients
        int lifterCutoff = _envelopeOrder;
        for (int i = lifterCutoff; i < _fftSize - lifterCutoff; i++)
        {
            cepstrum[i] = new Complex(0f, 0f);
        }

        FFT(cepstrum, true);

        // Convert back to linear magnitude for envelope
        for (int k = 0; k <= halfSize; k++)
        {
            _spectralEnvelope[channel][k] = MathF.Exp(cepstrum[k].Real / _fftSize);
        }
    }

    /// <summary>
    /// Applies formant correction to preserve vocal character during pitch shift.
    /// </summary>
    private void ApplyFormantCorrection(int channel, float[] magnitude, float pitchRatio, float amount)
    {
        int halfSize = _fftSize / 2;
        float[] correctedMag = new float[halfSize + 1];

        for (int k = 0; k <= halfSize; k++)
        {
            // Find the original envelope at the source position
            float sourcePos = k / pitchRatio;
            int sourceIdx = (int)sourcePos;
            float frac = sourcePos - sourceIdx;

            float sourceEnvelope = 1f;
            if (sourceIdx >= 0 && sourceIdx < halfSize)
            {
                // Linear interpolation of source envelope
                sourceEnvelope = _spectralEnvelope[channel][sourceIdx];
                if (sourceIdx + 1 <= halfSize)
                {
                    sourceEnvelope = sourceEnvelope * (1f - frac) + _spectralEnvelope[channel][sourceIdx + 1] * frac;
                }
            }

            // Get target envelope
            float targetEnvelope = _spectralEnvelope[channel][k];

            // Calculate correction factor: restore original envelope after pitch shift
            float correction = 1f;
            if (sourceEnvelope > 1e-10f)
            {
                correction = targetEnvelope / sourceEnvelope;
            }

            // Apply correction with amount control
            correction = 1f + (correction - 1f) * amount;

            // Clamp to prevent extreme values
            correction = Math.Clamp(correction, 0.1f, 10f);

            correctedMag[k] = magnitude[k] * correction;
        }

        // Copy back
        Array.Copy(correctedMag, magnitude, halfSize + 1);
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
        // WindowSize affects quality mode
        if (name.Equals("WindowSize", StringComparison.OrdinalIgnoreCase))
        {
            PitchShifterQuality newQuality;
            if (value < 0.33f)
                newQuality = PitchShifterQuality.Fast;
            else if (value < 0.66f)
                newQuality = PitchShifterQuality.Normal;
            else
                newQuality = PitchShifterQuality.HighQuality;

            if (newQuality != _quality)
            {
                _quality = newQuality;
                _initialized = false;
            }
        }
    }

    /// <summary>
    /// Creates a preset for subtle vocal pitch correction.
    /// </summary>
    public static PitchShifterEffect CreateVocalCorrection(ISampleProvider source)
    {
        var effect = new PitchShifterEffect(source, "Vocal Pitch Correction");
        effect.Semitones = 0f;
        effect.Cents = 0f;
        effect.FormantPreserve = 1f;
        effect.Quality = PitchShifterQuality.HighQuality;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for octave up harmonizer effect.
    /// </summary>
    public static PitchShifterEffect CreateOctaveUp(ISampleProvider source)
    {
        var effect = new PitchShifterEffect(source, "Octave Up");
        effect.Semitones = 12f;
        effect.Cents = 0f;
        effect.FormantPreserve = 0.5f;
        effect.Quality = PitchShifterQuality.Normal;
        effect.Mix = 0.5f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for octave down effect.
    /// </summary>
    public static PitchShifterEffect CreateOctaveDown(ISampleProvider source)
    {
        var effect = new PitchShifterEffect(source, "Octave Down");
        effect.Semitones = -12f;
        effect.Cents = 0f;
        effect.FormantPreserve = 0.5f;
        effect.Quality = PitchShifterQuality.Normal;
        effect.Mix = 0.5f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for chipmunk-style high pitch effect.
    /// </summary>
    public static PitchShifterEffect CreateChipmunk(ISampleProvider source)
    {
        var effect = new PitchShifterEffect(source, "Chipmunk");
        effect.Semitones = 12f;
        effect.Cents = 0f;
        effect.FormantPreserve = 0f; // No formant preservation for cartoon effect
        effect.Quality = PitchShifterQuality.Fast;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for deep voice effect.
    /// </summary>
    public static PitchShifterEffect CreateDeepVoice(ISampleProvider source)
    {
        var effect = new PitchShifterEffect(source, "Deep Voice");
        effect.Semitones = -7f;
        effect.Cents = 0f;
        effect.FormantPreserve = 0.8f; // Preserve formants to keep voice natural
        effect.Quality = PitchShifterQuality.HighQuality;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for harmony generation (fifth up).
    /// </summary>
    public static PitchShifterEffect CreateHarmonyFifth(ISampleProvider source)
    {
        var effect = new PitchShifterEffect(source, "Harmony Fifth");
        effect.Semitones = 7f;
        effect.Cents = 0f;
        effect.FormantPreserve = 0.7f;
        effect.Quality = PitchShifterQuality.HighQuality;
        effect.Mix = 0.5f; // Mix with dry for parallel harmony
        return effect;
    }

    /// <summary>
    /// Creates a preset for detune/chorus-like effect.
    /// </summary>
    public static PitchShifterEffect CreateDetune(ISampleProvider source)
    {
        var effect = new PitchShifterEffect(source, "Detune");
        effect.Semitones = 0f;
        effect.Cents = 15f; // Slight detune
        effect.FormantPreserve = 0f;
        effect.Quality = PitchShifterQuality.Fast;
        effect.Mix = 0.5f;
        return effect;
    }

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
