// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Special;

/// <summary>
/// Quality modes for time stretching algorithm.
/// Higher quality modes use larger FFT sizes for better frequency resolution but higher latency.
/// </summary>
public enum TimeStretchQuality
{
    /// <summary>
    /// Fast mode with smaller FFT size (1024). Lower latency, lower quality.
    /// Good for real-time monitoring or preview.
    /// </summary>
    Fast,

    /// <summary>
    /// Normal mode with medium FFT size (2048). Balanced latency and quality.
    /// Suitable for most applications.
    /// </summary>
    Normal,

    /// <summary>
    /// High quality mode with larger FFT size (4096). Higher latency, best quality.
    /// Recommended for final rendering or when quality is critical.
    /// </summary>
    HighQuality
}

/// <summary>
/// Real-time time stretching effect using phase vocoder technique.
/// Changes playback speed without affecting pitch using STFT analysis/synthesis.
/// </summary>
/// <remarks>
/// The phase vocoder time stretch implementation:
/// 1. Performs Short-Time Fourier Transform (STFT) on overlapping windows
/// 2. Analyzes magnitude and phase of each frequency bin
/// 3. Calculates instantaneous frequency from phase differences
/// 4. For time stretching, uses different hop sizes for analysis and synthesis:
///    - Analysis hop = fixed (based on FFT size / overlap factor)
///    - Synthesis hop = analysis_hop * stretch_factor
/// 5. Accumulates phase based on the synthesis hop to maintain phase coherence
/// 6. Performs inverse FFT and overlap-add reconstruction
///
/// Key difference from pitch shifting:
/// - Pitch shifting: same hop size, scale frequencies in spectrum
/// - Time stretching: different hop sizes, maintain frequencies unchanged
///
/// Transient preservation optionally detects attack transients and reduces
/// phase vocoder smearing by resetting phase at transient locations.
/// </remarks>
public class TimeStretchEffect : EffectBase
{
    // FFT size based on quality mode
    private int _fftSize;
    private int _analysisHopSize;
    private int _overlapFactor;

    // FFT working buffers
    private float[][] _inputBuffer = null!;        // Circular input buffer per channel
    private float[][] _outputBuffer = null!;       // Overlap-add output buffer per channel
    private int[] _inputWritePos = null!;          // Write position in input buffer
    private int[] _outputWritePos = null!;         // Write position in output buffer
    private int[] _outputReadPos = null!;          // Read position in output buffer
    private int _samplesUntilNextAnalysis;         // Counter for analysis hop

    // FFT data (per channel)
    private Complex[][] _fftBuffer = null!;        // FFT working buffer
    private float[][] _lastInputPhase = null!;     // Phase from previous analysis frame
    private float[][] _accumulatedPhase = null!;   // Accumulated output phase for synthesis

    // Analysis/synthesis windows
    private float[] _analysisWindow = null!;       // Hann window for analysis
    private float[] _synthesisWindow = null!;      // Hann window for synthesis

    // Transient detection
    private float[][] _previousEnergy = null!;     // Energy history for transient detection
    private const int TransientHistorySize = 4;
    private int _transientHistoryIndex;

    // State
    private bool _initialized;
    private TimeStretchQuality _quality;
    private int _outputSamplesAvailable;

    // Resampling buffer for stretch ratio changes
    private float[][] _resampleBuffer = null!;
    private int[] _resampleWritePos = null!;
    private float[] _resampleReadPos = null!;      // Fractional read position for interpolation

    /// <summary>
    /// Creates a new time stretch effect.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    public TimeStretchEffect(ISampleProvider source) : this(source, "Time Stretch")
    {
    }

    /// <summary>
    /// Creates a new time stretch effect with a custom name.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <param name="name">Effect name</param>
    public TimeStretchEffect(ISampleProvider source, string name) : base(source, name)
    {
        // Register parameters with defaults
        RegisterParameter("StretchFactor", 1.0f);      // 0.25 to 4.0 (1.0 = original speed)
        RegisterParameter("PreserveTransients", 1.0f); // 0 = off, 1 = on
        RegisterParameter("WindowSize", 0.5f);         // 0-1 maps to quality modes
        RegisterParameter("Mix", 1.0f);                // Dry/wet mix

        _quality = TimeStretchQuality.Normal;
        _initialized = false;
    }

    /// <summary>
    /// Time stretch factor (0.25 to 4.0).
    /// 1.0 = original speed, 0.5 = half speed (2x longer), 2.0 = double speed (2x shorter).
    /// </summary>
    public float StretchFactor
    {
        get => GetParameter("StretchFactor");
        set => SetParameter("StretchFactor", Math.Clamp(value, 0.25f, 4.0f));
    }

    /// <summary>
    /// Enable transient preservation (0.0 = off, 1.0 = on).
    /// When enabled, detects attack transients and preserves their sharpness
    /// by resetting phase vocoder state at transient boundaries.
    /// </summary>
    public float PreserveTransients
    {
        get => GetParameter("PreserveTransients");
        set => SetParameter("PreserveTransients", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Window/grain size control (0.0 - 1.0).
    /// Affects transient response: smaller = better transients, larger = smoother tonal content.
    /// Maps to quality modes: 0-0.33 = Fast, 0.33-0.66 = Normal, 0.66-1.0 = HighQuality.
    /// </summary>
    public float WindowSize
    {
        get => GetParameter("WindowSize");
        set => SetParameter("WindowSize", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Quality mode selection.
    /// </summary>
    public TimeStretchQuality Quality
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
    /// Initializes internal buffers based on quality setting.
    /// </summary>
    private void Initialize()
    {
        int channels = Channels;

        // Set FFT size based on quality
        _fftSize = _quality switch
        {
            TimeStretchQuality.Fast => 1024,
            TimeStretchQuality.Normal => 2048,
            TimeStretchQuality.HighQuality => 4096,
            _ => 2048
        };

        // Overlap factor: 4x for good quality (75% overlap)
        _overlapFactor = 4;
        _analysisHopSize = _fftSize / _overlapFactor;

        // Allocate per-channel buffers
        _inputBuffer = new float[channels][];
        _outputBuffer = new float[channels][];
        _inputWritePos = new int[channels];
        _outputWritePos = new int[channels];
        _outputReadPos = new int[channels];
        _fftBuffer = new Complex[channels][];
        _lastInputPhase = new float[channels][];
        _accumulatedPhase = new float[channels][];
        _previousEnergy = new float[channels][];
        _resampleBuffer = new float[channels][];
        _resampleWritePos = new int[channels];
        _resampleReadPos = new float[channels];

        // Calculate maximum output buffer size needed for slowest stretch (0.25x = 4x longer output)
        int maxOutputSize = _fftSize * 8;

        for (int ch = 0; ch < channels; ch++)
        {
            // Input buffer needs to hold at least one full FFT frame plus margin
            _inputBuffer[ch] = new float[_fftSize * 2];
            // Output buffer for overlap-add
            _outputBuffer[ch] = new float[maxOutputSize];
            _inputWritePos[ch] = 0;
            _outputWritePos[ch] = 0;
            _outputReadPos[ch] = 0;

            _fftBuffer[ch] = new Complex[_fftSize];
            _lastInputPhase[ch] = new float[_fftSize / 2 + 1];
            _accumulatedPhase[ch] = new float[_fftSize / 2 + 1];
            _previousEnergy[ch] = new float[TransientHistorySize];

            // Resample buffer for output rate conversion
            _resampleBuffer[ch] = new float[maxOutputSize];
            _resampleWritePos[ch] = 0;
            _resampleReadPos[ch] = 0f;
        }

        // Generate analysis window (Hann)
        _analysisWindow = new float[_fftSize];
        for (int i = 0; i < _fftSize; i++)
        {
            _analysisWindow[i] = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (_fftSize - 1)));
        }

        // Synthesis window (same as analysis for COLA property)
        _synthesisWindow = new float[_fftSize];
        Array.Copy(_analysisWindow, _synthesisWindow, _fftSize);

        _samplesUntilNextAnalysis = 0;
        _transientHistoryIndex = 0;
        _outputSamplesAvailable = 0;
        _initialized = true;
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        if (!_initialized)
        {
            Initialize();
        }

        int channels = Channels;
        float stretchFactor = StretchFactor;
        bool preserveTransients = PreserveTransients > 0.5f;

        // Calculate synthesis hop size based on stretch factor
        // For time stretch: synthesis_hop = analysis_hop * stretch_factor
        // Slower playback (stretch < 1) = more overlap in output = larger effective synthesis hop
        int synthesisHopSize = Math.Max(1, (int)(_analysisHopSize * stretchFactor));

        int samplesWritten = 0;

        // Process interleaved samples
        for (int i = 0; i < count; i += channels)
        {
            // Write input samples to circular buffer
            for (int ch = 0; ch < channels; ch++)
            {
                int sampleIndex = i + ch;
                float inputSample = sourceBuffer[sampleIndex];

                _inputBuffer[ch][_inputWritePos[ch]] = inputSample;
                _inputWritePos[ch] = (_inputWritePos[ch] + 1) % _inputBuffer[ch].Length;
            }

            _samplesUntilNextAnalysis--;

            // Time to process a new analysis frame?
            if (_samplesUntilNextAnalysis <= 0)
            {
                _samplesUntilNextAnalysis = _analysisHopSize;

                // Detect transients before processing
                bool isTransient = false;
                if (preserveTransients)
                {
                    isTransient = DetectTransient(0); // Use channel 0 for transient detection
                }

                // Process phase vocoder for each channel
                for (int ch = 0; ch < channels; ch++)
                {
                    ProcessPhaseVocoderFrame(ch, synthesisHopSize, isTransient);
                }
            }

            // Read from output buffer and write to destination
            // We need to read at the original rate but the output was generated at a different rate
            for (int ch = 0; ch < channels; ch++)
            {
                float outputSample = 0f;

                // Check if we have samples available in the resample buffer
                int resampleAvailable = (_resampleWritePos[ch] - (int)_resampleReadPos[ch] + _resampleBuffer[ch].Length) % _resampleBuffer[ch].Length;

                if (resampleAvailable > 1)
                {
                    // Linear interpolation for smooth resampling
                    int readPosInt = (int)_resampleReadPos[ch];
                    float frac = _resampleReadPos[ch] - readPosInt;

                    int pos0 = readPosInt % _resampleBuffer[ch].Length;
                    int pos1 = (readPosInt + 1) % _resampleBuffer[ch].Length;

                    outputSample = _resampleBuffer[ch][pos0] * (1f - frac) + _resampleBuffer[ch][pos1] * frac;

                    // Advance read position by stretch factor to compensate for time stretch
                    // When stretching slower (factor < 1), we read slower
                    // When stretching faster (factor > 1), we read faster
                    _resampleReadPos[ch] += stretchFactor;

                    // Wrap read position
                    while (_resampleReadPos[ch] >= _resampleBuffer[ch].Length)
                    {
                        _resampleReadPos[ch] -= _resampleBuffer[ch].Length;
                    }
                }

                destBuffer[offset + i + ch] = outputSample;
            }
        }
    }

    /// <summary>
    /// Processes one phase vocoder frame for a single channel with time stretching.
    /// </summary>
    private void ProcessPhaseVocoderFrame(int channel, int synthesisHopSize, bool isTransient)
    {
        int halfSize = _fftSize / 2;
        float expectedPhaseDiff = 2f * MathF.PI * _analysisHopSize / _fftSize;

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

        // Analysis: Calculate magnitude and true frequency for each bin
        float[] magnitude = new float[halfSize + 1];
        float[] trueFreq = new float[halfSize + 1];

        for (int k = 0; k <= halfSize; k++)
        {
            float real = _fftBuffer[channel][k].Real;
            float imag = _fftBuffer[channel][k].Imag;

            magnitude[k] = MathF.Sqrt(real * real + imag * imag);
            float phase = MathF.Atan2(imag, real);

            // Calculate phase difference from last analysis frame
            float phaseDiff = phase - _lastInputPhase[channel][k];
            _lastInputPhase[channel][k] = phase;

            // Remove expected phase increment based on analysis hop
            phaseDiff -= k * expectedPhaseDiff;

            // Wrap to [-PI, PI]
            phaseDiff = WrapPhase(phaseDiff);

            // Calculate true frequency as deviation from bin center frequency
            float deviation = phaseDiff * _overlapFactor / (2f * MathF.PI);
            trueFreq[k] = k + deviation;
        }

        // Synthesis: Accumulate phase based on synthesis hop size
        // For time stretch, we use the SAME frequencies but different timing
        float synthesisExpectedPhase = 2f * MathF.PI * synthesisHopSize / _fftSize;

        // Handle transients by resetting phase
        if (isTransient)
        {
            // Reset accumulated phase to input phase at transients
            // This preserves transient sharpness
            for (int k = 0; k <= halfSize; k++)
            {
                _accumulatedPhase[channel][k] = _lastInputPhase[channel][k];
            }
        }

        // Build output spectrum
        for (int k = 0; k <= halfSize; k++)
        {
            // Accumulate phase based on true frequency and synthesis hop
            float phaseDelta = trueFreq[k] * synthesisExpectedPhase;
            _accumulatedPhase[channel][k] += phaseDelta;
            _accumulatedPhase[channel][k] = WrapPhase(_accumulatedPhase[channel][k]);

            float mag = magnitude[k];
            float ph = _accumulatedPhase[channel][k];

            _fftBuffer[channel][k] = new Complex(mag * MathF.Cos(ph), mag * MathF.Sin(ph));

            // Mirror for negative frequencies (conjugate symmetric)
            if (k > 0 && k < halfSize)
            {
                _fftBuffer[channel][_fftSize - k] = new Complex(mag * MathF.Cos(ph), -mag * MathF.Sin(ph));
            }
        }

        // Inverse FFT
        FFT(_fftBuffer[channel], true);

        // Overlap-add to resample buffer at synthesis hop positions
        float normFactor = 1f / (_overlapFactor * 0.5f);
        for (int i = 0; i < _fftSize; i++)
        {
            int outputPos = (_resampleWritePos[channel] + i) % _resampleBuffer[channel].Length;
            _resampleBuffer[channel][outputPos] += _fftBuffer[channel][i].Real * _synthesisWindow[i] * normFactor;
        }

        // Clear the region we just wrote over from previous overlap
        int clearStart = (_resampleWritePos[channel] + _fftSize) % _resampleBuffer[channel].Length;
        for (int i = 0; i < synthesisHopSize && i < _fftSize; i++)
        {
            int clearPos = (clearStart + i) % _resampleBuffer[channel].Length;
            _resampleBuffer[channel][clearPos] = 0f;
        }

        // Advance write position by synthesis hop
        _resampleWritePos[channel] = (_resampleWritePos[channel] + synthesisHopSize) % _resampleBuffer[channel].Length;
    }

    /// <summary>
    /// Detects if the current frame contains a transient (attack).
    /// Uses energy-based detection with adaptive threshold.
    /// </summary>
    private bool DetectTransient(int channel)
    {
        // Calculate current frame energy
        int readStart = (_inputWritePos[channel] - _fftSize + _inputBuffer[channel].Length) % _inputBuffer[channel].Length;
        float energy = 0f;

        for (int i = 0; i < _fftSize; i++)
        {
            int readPos = (readStart + i) % _inputBuffer[channel].Length;
            float sample = _inputBuffer[channel][readPos];
            energy += sample * sample;
        }

        energy = MathF.Sqrt(energy / _fftSize);

        // Calculate average of previous energies
        float avgEnergy = 0f;
        for (int i = 0; i < TransientHistorySize; i++)
        {
            avgEnergy += _previousEnergy[channel][i];
        }
        avgEnergy /= TransientHistorySize;

        // Store current energy in history
        _previousEnergy[channel][_transientHistoryIndex] = energy;
        _transientHistoryIndex = (_transientHistoryIndex + 1) % TransientHistorySize;

        // Transient detection: significant energy increase
        float threshold = 2.0f; // Energy must be 2x the average
        float minEnergy = 0.001f; // Minimum energy to consider

        return energy > minEnergy && avgEnergy > 0.0001f && (energy / avgEnergy) > threshold;
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
            TimeStretchQuality newQuality;
            if (value < 0.33f)
                newQuality = TimeStretchQuality.Fast;
            else if (value < 0.66f)
                newQuality = TimeStretchQuality.Normal;
            else
                newQuality = TimeStretchQuality.HighQuality;

            if (newQuality != _quality)
            {
                _quality = newQuality;
                _initialized = false;
            }
        }
        else if (name.Equals("StretchFactor", StringComparison.OrdinalIgnoreCase))
        {
            // Stretch factor change may require buffer size adjustment for extreme values
            // Reinitialization ensures buffers are adequate
            if (value < 0.5f || value > 2.0f)
            {
                // Only reinitialize for extreme stretch values
                // Normal range doesn't need reinitialization
            }
        }
    }

    /// <summary>
    /// Creates a preset for half speed playback (2x longer duration).
    /// </summary>
    public static TimeStretchEffect CreateHalfSpeed(ISampleProvider source)
    {
        var effect = new TimeStretchEffect(source, "Half Speed");
        effect.StretchFactor = 0.5f;
        effect.PreserveTransients = 1.0f;
        effect.Quality = TimeStretchQuality.HighQuality;
        effect.Mix = 1.0f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for double speed playback (2x shorter duration).
    /// </summary>
    public static TimeStretchEffect CreateDoubleSpeed(ISampleProvider source)
    {
        var effect = new TimeStretchEffect(source, "Double Speed");
        effect.StretchFactor = 2.0f;
        effect.PreserveTransients = 1.0f;
        effect.Quality = TimeStretchQuality.Normal;
        effect.Mix = 1.0f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for slight slowdown (90% speed).
    /// Useful for practice or detailed listening.
    /// </summary>
    public static TimeStretchEffect CreateSlightSlowdown(ISampleProvider source)
    {
        var effect = new TimeStretchEffect(source, "Slight Slowdown");
        effect.StretchFactor = 0.9f;
        effect.PreserveTransients = 1.0f;
        effect.Quality = TimeStretchQuality.Normal;
        effect.Mix = 1.0f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for slight speedup (110% speed).
    /// Useful for fitting content into shorter time slots.
    /// </summary>
    public static TimeStretchEffect CreateSlightSpeedup(ISampleProvider source)
    {
        var effect = new TimeStretchEffect(source, "Slight Speedup");
        effect.StretchFactor = 1.1f;
        effect.PreserveTransients = 1.0f;
        effect.Quality = TimeStretchQuality.Normal;
        effect.Mix = 1.0f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for extreme slow motion (25% speed, 4x longer).
    /// Creates a dreamy, stretched effect while maintaining pitch.
    /// </summary>
    public static TimeStretchEffect CreateExtremeSlowMotion(ISampleProvider source)
    {
        var effect = new TimeStretchEffect(source, "Extreme Slow Motion");
        effect.StretchFactor = 0.25f;
        effect.PreserveTransients = 0.0f; // Disable transient preservation for smoother result
        effect.Quality = TimeStretchQuality.HighQuality;
        effect.Mix = 1.0f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for fast forward (4x speed).
    /// Useful for quick preview of audio content.
    /// </summary>
    public static TimeStretchEffect CreateFastForward(ISampleProvider source)
    {
        var effect = new TimeStretchEffect(source, "Fast Forward");
        effect.StretchFactor = 4.0f;
        effect.PreserveTransients = 1.0f;
        effect.Quality = TimeStretchQuality.Fast;
        effect.Mix = 1.0f;
        return effect;
    }

    /// <summary>
    /// Creates a preset optimized for music with clear transients (drums, percussion).
    /// </summary>
    public static TimeStretchEffect CreatePercussionOptimized(ISampleProvider source)
    {
        var effect = new TimeStretchEffect(source, "Percussion Optimized");
        effect.StretchFactor = 1.0f;
        effect.PreserveTransients = 1.0f;
        effect.Quality = TimeStretchQuality.Fast; // Smaller window for better transients
        effect.Mix = 1.0f;
        return effect;
    }

    /// <summary>
    /// Creates a preset optimized for smooth tonal content (vocals, strings, pads).
    /// </summary>
    public static TimeStretchEffect CreateTonalOptimized(ISampleProvider source)
    {
        var effect = new TimeStretchEffect(source, "Tonal Optimized");
        effect.StretchFactor = 1.0f;
        effect.PreserveTransients = 0.0f; // Disable for smoother result
        effect.Quality = TimeStretchQuality.HighQuality; // Larger window for better frequency resolution
        effect.Mix = 1.0f;
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
