// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Noise gate processor.

using System;
using NAudio.Dsp;
using NAudio.Wave;


namespace MusicEngine.Core.Effects.Dynamics;


/// <summary>
/// Supported FFT sizes for the spectral gate.
/// </summary>
public enum SpectralGateFftSize
{
    /// <summary>512 samples - lowest latency, less frequency resolution</summary>
    Size512 = 512,
    /// <summary>1024 samples - balanced latency and resolution</summary>
    Size1024 = 1024,
    /// <summary>2048 samples - good frequency resolution</summary>
    Size2048 = 2048,
    /// <summary>4096 samples - highest frequency resolution, more latency</summary>
    Size4096 = 4096
}


/// <summary>
/// Threshold mode for the spectral gate.
/// </summary>
public enum SpectralGateThresholdMode
{
    /// <summary>Single global threshold applied to all frequency bins</summary>
    Global,
    /// <summary>Frequency-dependent threshold curve (lower threshold for bass, higher for treble)</summary>
    FrequencyCurve,
    /// <summary>Adaptive threshold based on average spectrum energy</summary>
    Adaptive
}


/// <summary>
/// Spectral gate effect that performs frequency-selective gating using FFT analysis.
/// Useful for noise reduction, isolating specific frequencies, and creative sound design.
///
/// The effect uses STFT (Short-Time Fourier Transform) with overlap-add reconstruction
/// to analyze and gate individual frequency bins based on their magnitude.
/// </summary>
public class SpectralGateEffect : EffectBase
{
    // FFT configuration
    private readonly int _fftSize;
    private readonly int _fftSizeLog2;
    private readonly int _hopSize;          // Overlap amount (typically FFT size / 4)
    private readonly int _numBins;          // Number of frequency bins (FFT size / 2 + 1)

    // FFT buffers per channel
    private readonly Complex[][] _fftBuffer;
    private readonly float[][] _windowedInput;
    private readonly float[][] _outputAccumulator;
    private readonly float[][] _inputBuffer;
    private readonly float[][] _magnitudes;
    private readonly float[][] _phases;
    private readonly float[][] _binGains;           // Current gain per bin
    private readonly float[][] _binGainTargets;     // Target gain per bin
    private readonly int[] _inputWritePos;
    private readonly int[] _outputReadPos;
    private readonly int[] _samplesUntilNextFft;

    // Window function
    private readonly float[] _analysisWindow;
    private readonly float[] _synthesisWindow;
    private readonly float _windowSum;              // For normalization

    // Lookahead buffer
    private readonly float[][] _lookaheadBuffer;
    private readonly int[] _lookaheadWritePos;
    private int _lookaheadSamples;

    // Hold time state per bin
    private readonly float[][] _holdCounters;

    // Threshold curve (for FrequencyCurve mode)
    private readonly float[] _thresholdCurve;

    // Adaptive threshold state
    private readonly float[] _adaptiveThreshold;
    private float _adaptiveSmoothing = 0.99f;

    /// <summary>
    /// Creates a new spectral gate effect.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <param name="name">Effect name</param>
    /// <param name="fftSize">FFT size (512, 1024, 2048, or 4096)</param>
    public SpectralGateEffect(ISampleProvider source, string name = "Spectral Gate",
        SpectralGateFftSize fftSize = SpectralGateFftSize.Size2048)
        : base(source, name)
    {
        _fftSize = (int)fftSize;
        _fftSizeLog2 = (int)Math.Log2(_fftSize);
        _hopSize = _fftSize / 4;  // 75% overlap
        _numBins = _fftSize / 2 + 1;

        int channels = source.WaveFormat.Channels;

        // Initialize per-channel buffers
        _fftBuffer = new Complex[channels][];
        _windowedInput = new float[channels][];
        _outputAccumulator = new float[channels][];
        _inputBuffer = new float[channels][];
        _magnitudes = new float[channels][];
        _phases = new float[channels][];
        _binGains = new float[channels][];
        _binGainTargets = new float[channels][];
        _holdCounters = new float[channels][];
        _lookaheadBuffer = new float[channels][];
        _inputWritePos = new int[channels];
        _outputReadPos = new int[channels];
        _samplesUntilNextFft = new int[channels];
        _lookaheadWritePos = new int[channels];

        for (int ch = 0; ch < channels; ch++)
        {
            _fftBuffer[ch] = new Complex[_fftSize];
            _windowedInput[ch] = new float[_fftSize];
            _outputAccumulator[ch] = new float[_fftSize * 2];  // Double size for overlap-add
            _inputBuffer[ch] = new float[_fftSize];
            _magnitudes[ch] = new float[_numBins];
            _phases[ch] = new float[_numBins];
            _binGains[ch] = new float[_numBins];
            _binGainTargets[ch] = new float[_numBins];
            _holdCounters[ch] = new float[_numBins];
            _lookaheadBuffer[ch] = new float[_fftSize];  // Max lookahead = FFT size

            // Initialize gains to 1 (open)
            for (int i = 0; i < _numBins; i++)
            {
                _binGains[ch][i] = 1f;
                _binGainTargets[ch][i] = 1f;
            }

            _samplesUntilNextFft[ch] = 0;
        }

        // Create Hann window for analysis and synthesis
        _analysisWindow = new float[_fftSize];
        _synthesisWindow = new float[_fftSize];
        _windowSum = 0f;

        for (int i = 0; i < _fftSize; i++)
        {
            // Hann window
            float window = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (_fftSize - 1)));
            _analysisWindow[i] = window;
            _synthesisWindow[i] = window;
            _windowSum += window * window;
        }

        // Normalize for overlap-add
        _windowSum = _windowSum / _hopSize;

        // Initialize threshold curve (flat by default)
        _thresholdCurve = new float[_numBins];
        _adaptiveThreshold = new float[channels];

        // Register parameters
        RegisterParameter("Threshold", -40f);           // Global threshold in dB
        RegisterParameter("ThresholdMode", (float)SpectralGateThresholdMode.Global);
        RegisterParameter("Attack", 0.005f);            // Attack time in seconds
        RegisterParameter("Release", 0.05f);            // Release time in seconds
        RegisterParameter("Hold", 0.02f);               // Hold time in seconds
        RegisterParameter("Range", -60f);               // Attenuation in dB when gated
        RegisterParameter("FreqLow", 20f);              // Low frequency limit in Hz
        RegisterParameter("FreqHigh", 20000f);          // High frequency limit in Hz
        RegisterParameter("Lookahead", 0f);             // Lookahead in ms (0 = disabled)
        RegisterParameter("CurveSlope", 6f);            // Threshold curve slope in dB/octave
        RegisterParameter("AdaptiveRatio", 0.7f);       // Ratio below adaptive threshold

        Mix = 1.0f;

        UpdateThresholdCurve();
    }

    /// <summary>
    /// Global threshold in dB (-80 to 0).
    /// Frequency bins with magnitude below this level will be attenuated.
    /// </summary>
    public float Threshold
    {
        get => GetParameter("Threshold");
        set
        {
            SetParameter("Threshold", Math.Clamp(value, -80f, 0f));
            UpdateThresholdCurve();
        }
    }

    /// <summary>
    /// Threshold mode (Global, FrequencyCurve, or Adaptive).
    /// </summary>
    public SpectralGateThresholdMode ThresholdMode
    {
        get => (SpectralGateThresholdMode)GetParameter("ThresholdMode");
        set
        {
            SetParameter("ThresholdMode", (float)value);
            UpdateThresholdCurve();
        }
    }

    /// <summary>
    /// Attack time in seconds (0.0001 - 1.0).
    /// How fast the gate opens when signal exceeds threshold.
    /// </summary>
    public float Attack
    {
        get => GetParameter("Attack");
        set => SetParameter("Attack", Math.Clamp(value, 0.0001f, 1f));
    }

    /// <summary>
    /// Release time in seconds (0.001 - 5.0).
    /// How fast the gate closes after signal falls below threshold.
    /// </summary>
    public float Release
    {
        get => GetParameter("Release");
        set => SetParameter("Release", Math.Clamp(value, 0.001f, 5f));
    }

    /// <summary>
    /// Hold time in seconds (0 - 2.0).
    /// Minimum time the gate stays open after being triggered.
    /// </summary>
    public float Hold
    {
        get => GetParameter("Hold");
        set => SetParameter("Hold", Math.Clamp(value, 0f, 2f));
    }

    /// <summary>
    /// Range (attenuation) in dB (-80 to 0).
    /// How much gated frequencies are attenuated.
    /// </summary>
    public float Range
    {
        get => GetParameter("Range");
        set => SetParameter("Range", Math.Clamp(value, -80f, 0f));
    }

    /// <summary>
    /// Low frequency limit in Hz (20 - 20000).
    /// Only frequencies above this will be gated.
    /// </summary>
    public float FrequencyLow
    {
        get => GetParameter("FreqLow");
        set => SetParameter("FreqLow", Math.Clamp(value, 20f, 20000f));
    }

    /// <summary>
    /// High frequency limit in Hz (20 - 20000).
    /// Only frequencies below this will be gated.
    /// </summary>
    public float FrequencyHigh
    {
        get => GetParameter("FreqHigh");
        set => SetParameter("FreqHigh", Math.Clamp(value, 20f, 20000f));
    }

    /// <summary>
    /// Lookahead time in milliseconds (0 - 50).
    /// Allows the gate to "see ahead" for smoother operation.
    /// Adds latency equal to the lookahead time.
    /// </summary>
    public float Lookahead
    {
        get => GetParameter("Lookahead");
        set
        {
            SetParameter("Lookahead", Math.Clamp(value, 0f, 50f));
            UpdateLookahead();
        }
    }

    /// <summary>
    /// Threshold curve slope in dB/octave (for FrequencyCurve mode).
    /// Positive values mean higher threshold for higher frequencies.
    /// </summary>
    public float CurveSlope
    {
        get => GetParameter("CurveSlope");
        set
        {
            SetParameter("CurveSlope", Math.Clamp(value, -24f, 24f));
            UpdateThresholdCurve();
        }
    }

    /// <summary>
    /// Adaptive ratio (0.0 - 1.0) for Adaptive threshold mode.
    /// Determines how far below the average spectrum level the threshold is set.
    /// </summary>
    public float AdaptiveRatio
    {
        get => GetParameter("AdaptiveRatio");
        set => SetParameter("AdaptiveRatio", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Gets the FFT size used by this effect.
    /// </summary>
    public int FftSize => _fftSize;

    /// <summary>
    /// Gets the number of frequency bins.
    /// </summary>
    public int NumBins => _numBins;

    /// <summary>
    /// Gets the frequency resolution in Hz per bin.
    /// </summary>
    public float FrequencyResolution => (float)SampleRate / _fftSize;

    /// <summary>
    /// Gets the latency introduced by the effect in samples.
    /// </summary>
    public int LatencySamples => _fftSize + _lookaheadSamples;

    /// <summary>
    /// Gets the current bin gains for visualization (channel 0).
    /// </summary>
    /// <returns>Array of gain values (0.0 to 1.0) for each frequency bin</returns>
    public float[] GetBinGains()
    {
        return (float[])_binGains[0].Clone();
    }

    /// <summary>
    /// Gets the frequency in Hz for a given bin index.
    /// </summary>
    /// <param name="binIndex">The bin index (0 to NumBins-1)</param>
    /// <returns>Frequency in Hz</returns>
    public float GetBinFrequency(int binIndex)
    {
        return binIndex * FrequencyResolution;
    }

    /// <summary>
    /// Resets the effect state.
    /// </summary>
    public void Reset()
    {
        for (int ch = 0; ch < Channels; ch++)
        {
            Array.Clear(_fftBuffer[ch], 0, _fftBuffer[ch].Length);
            Array.Clear(_windowedInput[ch], 0, _windowedInput[ch].Length);
            Array.Clear(_outputAccumulator[ch], 0, _outputAccumulator[ch].Length);
            Array.Clear(_inputBuffer[ch], 0, _inputBuffer[ch].Length);
            Array.Clear(_magnitudes[ch], 0, _magnitudes[ch].Length);
            Array.Clear(_phases[ch], 0, _phases[ch].Length);
            Array.Clear(_holdCounters[ch], 0, _holdCounters[ch].Length);
            Array.Clear(_lookaheadBuffer[ch], 0, _lookaheadBuffer[ch].Length);

            for (int i = 0; i < _numBins; i++)
            {
                _binGains[ch][i] = 1f;
                _binGainTargets[ch][i] = 1f;
            }

            _inputWritePos[ch] = 0;
            _outputReadPos[ch] = 0;
            _samplesUntilNextFft[ch] = 0;
            _lookaheadWritePos[ch] = 0;
            _adaptiveThreshold[ch] = 0f;
        }
    }

    protected override void OnParameterChanged(string name, float value)
    {
        if (name.Equals("Threshold", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("ThresholdMode", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("CurveSlope", StringComparison.OrdinalIgnoreCase))
        {
            UpdateThresholdCurve();
        }
        else if (name.Equals("Lookahead", StringComparison.OrdinalIgnoreCase))
        {
            UpdateLookahead();
        }
    }

    private void UpdateThresholdCurve()
    {
        float baseThreshold = Threshold;
        float slope = CurveSlope;
        float refFreq = 1000f;  // Reference frequency (1kHz)
        float freqResolution = FrequencyResolution;

        for (int i = 0; i < _numBins; i++)
        {
            float freq = i * freqResolution;
            if (freq < 20f) freq = 20f;  // Avoid log(0)

            if (ThresholdMode == SpectralGateThresholdMode.FrequencyCurve)
            {
                // Calculate octave distance from reference frequency
                float octaves = MathF.Log2(freq / refFreq);
                // Apply slope
                _thresholdCurve[i] = baseThreshold + octaves * slope;
            }
            else
            {
                // Global mode: flat threshold
                _thresholdCurve[i] = baseThreshold;
            }
        }
    }

    private void UpdateLookahead()
    {
        float lookaheadMs = Lookahead;
        _lookaheadSamples = (int)(lookaheadMs * SampleRate / 1000f);
        _lookaheadSamples = Math.Min(_lookaheadSamples, _fftSize);  // Cap at FFT size
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;
        int sampleRate = SampleRate;

        // Cache parameters
        float attack = Attack;
        float release = Release;
        float hold = Hold;
        float range = Range;
        float freqLow = FrequencyLow;
        float freqHigh = FrequencyHigh;
        float adaptiveRatio = AdaptiveRatio;
        var thresholdMode = ThresholdMode;

        // Convert range to linear
        float rangeLinear = MathF.Pow(10f, range / 20f);

        // Calculate attack/release coefficients per FFT frame
        float framesPerSecond = (float)sampleRate / _hopSize;
        float attackCoeff = 1f - MathF.Exp(-1f / (attack * framesPerSecond));
        float releaseCoeff = 1f - MathF.Exp(-1f / (release * framesPerSecond));

        // Hold time in FFT frames
        float holdFrames = hold * framesPerSecond;

        // Calculate frequency bin limits
        float freqResolution = FrequencyResolution;
        int binLow = Math.Max(0, (int)(freqLow / freqResolution));
        int binHigh = Math.Min(_numBins - 1, (int)(freqHigh / freqResolution));

        // Process sample by sample
        for (int i = 0; i < count; i += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                float input = sourceBuffer[i + ch];

                // Handle lookahead
                float delayedInput;
                if (_lookaheadSamples > 0)
                {
                    // Write to lookahead buffer
                    delayedInput = _lookaheadBuffer[ch][_lookaheadWritePos[ch]];
                    _lookaheadBuffer[ch][_lookaheadWritePos[ch]] = input;
                    _lookaheadWritePos[ch] = (_lookaheadWritePos[ch] + 1) % _lookaheadSamples;
                }
                else
                {
                    delayedInput = input;
                }

                // Add input to circular buffer
                _inputBuffer[ch][_inputWritePos[ch]] = input;
                _inputWritePos[ch] = (_inputWritePos[ch] + 1) % _fftSize;

                // Check if we need to process FFT
                _samplesUntilNextFft[ch]--;
                if (_samplesUntilNextFft[ch] <= 0)
                {
                    _samplesUntilNextFft[ch] = _hopSize;

                    // Copy input buffer to windowed buffer with window function
                    int readPos = _inputWritePos[ch];
                    for (int j = 0; j < _fftSize; j++)
                    {
                        int srcIdx = (readPos + j) % _fftSize;
                        _windowedInput[ch][j] = _inputBuffer[ch][srcIdx] * _analysisWindow[j];
                    }

                    // Copy to FFT buffer
                    for (int j = 0; j < _fftSize; j++)
                    {
                        _fftBuffer[ch][j].X = _windowedInput[ch][j];
                        _fftBuffer[ch][j].Y = 0f;
                    }

                    // Perform FFT
                    FastFourierTransform.FFT(true, _fftSizeLog2, _fftBuffer[ch]);

                    // Calculate magnitudes and phases
                    float avgMagnitude = 0f;
                    for (int bin = 0; bin < _numBins; bin++)
                    {
                        float real = _fftBuffer[ch][bin].X;
                        float imag = _fftBuffer[ch][bin].Y;
                        _magnitudes[ch][bin] = MathF.Sqrt(real * real + imag * imag);
                        _phases[ch][bin] = MathF.Atan2(imag, real);
                        avgMagnitude += _magnitudes[ch][bin];
                    }
                    avgMagnitude /= _numBins;

                    // Update adaptive threshold
                    if (thresholdMode == SpectralGateThresholdMode.Adaptive)
                    {
                        _adaptiveThreshold[ch] = _adaptiveThreshold[ch] * _adaptiveSmoothing +
                                                 avgMagnitude * (1f - _adaptiveSmoothing);
                    }

                    // Calculate gate targets for each bin
                    for (int bin = 0; bin < _numBins; bin++)
                    {
                        float targetGain;

                        // Check if bin is within frequency range
                        if (bin < binLow || bin > binHigh)
                        {
                            // Outside range - pass through unchanged
                            targetGain = 1f;
                        }
                        else
                        {
                            // Get threshold for this bin
                            float thresholdDb;
                            if (thresholdMode == SpectralGateThresholdMode.Adaptive)
                            {
                                // Adaptive: threshold relative to average
                                float adaptiveLevel = _adaptiveThreshold[ch] * adaptiveRatio;
                                thresholdDb = 20f * MathF.Log10(adaptiveLevel + 1e-10f);
                            }
                            else
                            {
                                // Global or FrequencyCurve mode
                                thresholdDb = _thresholdCurve[bin];
                            }

                            // Convert magnitude to dB
                            float magnitudeDb = 20f * MathF.Log10(_magnitudes[ch][bin] + 1e-10f);

                            // Determine gate state
                            if (magnitudeDb >= thresholdDb)
                            {
                                // Above threshold - open gate
                                targetGain = 1f;
                                _holdCounters[ch][bin] = holdFrames;
                            }
                            else if (_holdCounters[ch][bin] > 0)
                            {
                                // In hold period - keep gate open
                                targetGain = 1f;
                                _holdCounters[ch][bin]--;
                            }
                            else
                            {
                                // Below threshold - close gate
                                targetGain = rangeLinear;
                            }
                        }

                        _binGainTargets[ch][bin] = targetGain;

                        // Smooth gain transition
                        float currentGain = _binGains[ch][bin];
                        if (targetGain > currentGain)
                        {
                            // Opening (attack)
                            _binGains[ch][bin] = currentGain + (targetGain - currentGain) * attackCoeff;
                        }
                        else
                        {
                            // Closing (release)
                            _binGains[ch][bin] = currentGain + (targetGain - currentGain) * releaseCoeff;
                        }
                    }

                    // Apply gains to frequency bins
                    for (int bin = 0; bin < _numBins; bin++)
                    {
                        float gain = _binGains[ch][bin];
                        float magnitude = _magnitudes[ch][bin] * gain;
                        float phase = _phases[ch][bin];

                        _fftBuffer[ch][bin].X = magnitude * MathF.Cos(phase);
                        _fftBuffer[ch][bin].Y = magnitude * MathF.Sin(phase);

                        // Mirror for negative frequencies (conjugate symmetry)
                        if (bin > 0 && bin < _numBins - 1)
                        {
                            int mirrorBin = _fftSize - bin;
                            _fftBuffer[ch][mirrorBin].X = _fftBuffer[ch][bin].X;
                            _fftBuffer[ch][mirrorBin].Y = -_fftBuffer[ch][bin].Y;
                        }
                    }

                    // Perform inverse FFT
                    FastFourierTransform.FFT(false, _fftSizeLog2, _fftBuffer[ch]);

                    // Apply synthesis window and add to output accumulator
                    int writePos = _outputReadPos[ch];
                    for (int j = 0; j < _fftSize; j++)
                    {
                        int destIdx = (writePos + j) % (_fftSize * 2);
                        _outputAccumulator[ch][destIdx] += _fftBuffer[ch][j].X * _synthesisWindow[j] / _windowSum;
                    }
                }

                // Read from output accumulator
                float output = _outputAccumulator[ch][_outputReadPos[ch]];
                _outputAccumulator[ch][_outputReadPos[ch]] = 0f;  // Clear for next overlap-add
                _outputReadPos[ch] = (_outputReadPos[ch] + 1) % (_fftSize * 2);

                destBuffer[offset + i + ch] = output;
            }
        }
    }

    /// <summary>
    /// Creates a preset for gentle noise reduction.
    /// </summary>
    public static SpectralGateEffect CreateNoiseReductionPreset(ISampleProvider source)
    {
        var effect = new SpectralGateEffect(source, "Spectral Gate (Noise Reduction)",
            SpectralGateFftSize.Size2048);
        effect.Threshold = -50f;
        effect.ThresholdMode = SpectralGateThresholdMode.FrequencyCurve;
        effect.CurveSlope = 3f;
        effect.Attack = 0.01f;
        effect.Release = 0.1f;
        effect.Hold = 0.05f;
        effect.Range = -40f;
        effect.FrequencyLow = 100f;
        effect.FrequencyHigh = 16000f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for isolating low frequencies.
    /// </summary>
    public static SpectralGateEffect CreateLowPassGatePreset(ISampleProvider source)
    {
        var effect = new SpectralGateEffect(source, "Spectral Gate (Low Pass)",
            SpectralGateFftSize.Size1024);
        effect.Threshold = -60f;
        effect.ThresholdMode = SpectralGateThresholdMode.Global;
        effect.Attack = 0.001f;
        effect.Release = 0.02f;
        effect.Hold = 0.01f;
        effect.Range = -80f;
        effect.FrequencyLow = 20f;
        effect.FrequencyHigh = 500f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for creative spectral effects.
    /// </summary>
    public static SpectralGateEffect CreateCreativePreset(ISampleProvider source)
    {
        var effect = new SpectralGateEffect(source, "Spectral Gate (Creative)",
            SpectralGateFftSize.Size4096);
        effect.Threshold = -30f;
        effect.ThresholdMode = SpectralGateThresholdMode.Adaptive;
        effect.AdaptiveRatio = 0.5f;
        effect.Attack = 0.05f;
        effect.Release = 0.2f;
        effect.Hold = 0.1f;
        effect.Range = -60f;
        effect.FrequencyLow = 20f;
        effect.FrequencyHigh = 20000f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for aggressive gating with fast response.
    /// </summary>
    public static SpectralGateEffect CreateAggressivePreset(ISampleProvider source)
    {
        var effect = new SpectralGateEffect(source, "Spectral Gate (Aggressive)",
            SpectralGateFftSize.Size512);
        effect.Threshold = -20f;
        effect.ThresholdMode = SpectralGateThresholdMode.Global;
        effect.Attack = 0.0005f;
        effect.Release = 0.01f;
        effect.Hold = 0.005f;
        effect.Range = -80f;
        effect.Lookahead = 5f;
        return effect;
    }
}
