//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Freeze reverb effect using FFT-based spectral freezing for infinite sustain.


using System;
using NAudio.Dsp;
using NAudio.Wave;


namespace MusicEngine.Core.Effects.TimeBased;


/// <summary>
/// Supported FFT sizes for the freeze reverb.
/// </summary>
public enum FreezeReverbFftSize
{
    /// <summary>1024 samples - lower latency, less frequency resolution</summary>
    Size1024 = 1024,
    /// <summary>2048 samples - balanced latency and resolution (default)</summary>
    Size2048 = 2048,
    /// <summary>4096 samples - good frequency resolution for smoother freeze</summary>
    Size4096 = 4096,
    /// <summary>8192 samples - highest quality freeze, more latency</summary>
    Size8192 = 8192
}


/// <summary>
/// Freeze reverb effect that captures and infinitely sustains audio using FFT-based
/// spectral freezing. When freeze is enabled, the current spectral content is held
/// and continuously output, creating ethereal, infinite sustain effects.
///
/// The effect uses STFT (Short-Time Fourier Transform) with overlap-add reconstruction
/// to capture and maintain the spectral content of the audio signal.
/// </summary>
/// <remarks>
/// The spectral freeze technique works by:
/// 1. Performing FFT analysis on incoming audio
/// 2. When freeze is triggered, capturing the current magnitude spectrum
/// 3. Continuously resynthesizing the frozen spectrum with randomized/evolving phases
/// 4. Applying damping and decay to create natural-sounding sustained tones
///
/// Common use cases:
/// - Creating infinite sustain on pads and strings
/// - Sound design for drones and ambient textures
/// - Live performance freeze effects
/// - Creating harmonic beds from any audio source
/// </remarks>
public class FreezeReverb : EffectBase
{
    // FFT configuration
    private readonly int _fftSize;
    private readonly int _fftSizeLog2;
    private readonly int _hopSize;
    private readonly int _numBins;

    // FFT buffers per channel
    private readonly Complex[][] _fftBuffer;
    private readonly float[][] _inputBuffer;
    private readonly float[][] _outputAccumulator;
    private readonly int[] _inputWritePos;
    private readonly int[] _outputReadPos;
    private readonly int[] _samplesUntilNextFft;

    // Frozen spectrum storage per channel
    private readonly float[][] _frozenMagnitudes;
    private readonly float[][] _frozenPhases;
    private readonly float[][] _currentMagnitudes;
    private readonly float[][] _phaseAccumulators;

    // Window function
    private readonly float[] _analysisWindow;
    private readonly float[] _synthesisWindow;
    private readonly float _windowSum;

    // Phase randomization state
    private readonly Random _random;
    private readonly float[][] _phaseIncrements;

    // Crossfade state for smooth freeze transitions
    private readonly float[] _freezeCrossfade;
    private const float CrossfadeRate = 0.001f;

    // DC blocker state per channel
    private readonly float[] _dcBlockerX1;
    private readonly float[] _dcBlockerY1;

    // Reverb tail components (pre-freeze diffusion)
    private readonly AllpassDiffuser[][] _diffusers;
    private const int NumDiffusers = 4;
    private readonly int[] _diffuserDelays = { 142, 379, 497, 683 };

    /// <summary>
    /// Creates a new freeze reverb effect with default settings.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    public FreezeReverb(ISampleProvider source) : this(source, "Freeze Reverb")
    {
    }

    /// <summary>
    /// Creates a new freeze reverb effect with a custom name.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <param name="name">Effect name</param>
    /// <param name="fftSize">FFT size for spectral analysis</param>
    public FreezeReverb(ISampleProvider source, string name,
        FreezeReverbFftSize fftSize = FreezeReverbFftSize.Size2048)
        : base(source, name)
    {
        _fftSize = (int)fftSize;
        _fftSizeLog2 = (int)Math.Log2(_fftSize);
        _hopSize = _fftSize / 4;  // 75% overlap for smooth reconstruction
        _numBins = _fftSize / 2 + 1;

        int channels = source.WaveFormat.Channels;
        float sampleRateRatio = SampleRate / 44100f;

        // Initialize random for phase evolution
        _random = new Random();

        // Initialize per-channel buffers
        _fftBuffer = new Complex[channels][];
        _inputBuffer = new float[channels][];
        _outputAccumulator = new float[channels][];
        _frozenMagnitudes = new float[channels][];
        _frozenPhases = new float[channels][];
        _currentMagnitudes = new float[channels][];
        _phaseAccumulators = new float[channels][];
        _phaseIncrements = new float[channels][];
        _freezeCrossfade = new float[channels];
        _dcBlockerX1 = new float[channels];
        _dcBlockerY1 = new float[channels];
        _inputWritePos = new int[channels];
        _outputReadPos = new int[channels];
        _samplesUntilNextFft = new int[channels];

        // Initialize diffusers
        _diffusers = new AllpassDiffuser[channels][];

        for (int ch = 0; ch < channels; ch++)
        {
            _fftBuffer[ch] = new Complex[_fftSize];
            _inputBuffer[ch] = new float[_fftSize];
            _outputAccumulator[ch] = new float[_fftSize * 2];
            _frozenMagnitudes[ch] = new float[_numBins];
            _frozenPhases[ch] = new float[_numBins];
            _currentMagnitudes[ch] = new float[_numBins];
            _phaseAccumulators[ch] = new float[_numBins];
            _phaseIncrements[ch] = new float[_numBins];
            _freezeCrossfade[ch] = 0f;

            _samplesUntilNextFft[ch] = 0;

            // Initialize phase increments based on bin frequency
            for (int bin = 0; bin < _numBins; bin++)
            {
                // Phase increment per hop for each frequency bin
                float frequency = (float)bin * SampleRate / _fftSize;
                _phaseIncrements[ch][bin] = 2f * MathF.PI * frequency * _hopSize / SampleRate;
            }

            // Initialize diffusers with scaled delays
            _diffusers[ch] = new AllpassDiffuser[NumDiffusers];
            for (int d = 0; d < NumDiffusers; d++)
            {
                int scaledDelay = (int)(_diffuserDelays[d] * sampleRateRatio);
                // Slight stereo offset for width
                if (ch == 1)
                {
                    scaledDelay = (int)(scaledDelay * 1.07f);
                }
                _diffusers[ch][d] = new AllpassDiffuser(scaledDelay);
            }
        }

        // Create Hann window for analysis and synthesis
        _analysisWindow = new float[_fftSize];
        _synthesisWindow = new float[_fftSize];
        _windowSum = 0f;

        for (int i = 0; i < _fftSize; i++)
        {
            float window = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (_fftSize - 1)));
            _analysisWindow[i] = window;
            _synthesisWindow[i] = window;
            _windowSum += window * window;
        }
        _windowSum = _windowSum / _hopSize;

        // Register parameters with defaults
        RegisterParameter("FreezeEnabled", 0f);    // 0 = off, 1 = on
        RegisterParameter("DecayTime", 10f);       // 10 second decay when not frozen
        RegisterParameter("Damping", 0.3f);        // 30% high frequency damping
        RegisterParameter("Size", 0.7f);           // Room size / density
        RegisterParameter("Mix", 0.5f);            // 50% wet
        RegisterParameter("Shimmer", 0f);          // Subtle pitch shift feedback (0 = off)
        RegisterParameter("PhaseEvolution", 0.1f); // Phase evolution rate for movement
        RegisterParameter("Diffusion", 0.5f);      // Pre-freeze diffusion amount
    }

    /// <summary>
    /// Gets or sets whether the freeze effect is enabled.
    /// When enabled, the current audio spectrum is captured and sustained indefinitely.
    /// </summary>
    public bool FreezeEnabled
    {
        get => GetParameter("FreezeEnabled") > 0.5f;
        set => SetParameter("FreezeEnabled", value ? 1f : 0f);
    }

    /// <summary>
    /// Decay time in seconds (0.5 - 60.0) when freeze is disabled.
    /// Controls how long the reverb tail lasts in normal mode.
    /// </summary>
    public float DecayTime
    {
        get => GetParameter("DecayTime");
        set => SetParameter("DecayTime", Math.Clamp(value, 0.5f, 60f));
    }

    /// <summary>
    /// High frequency damping (0.0 - 1.0).
    /// Higher values attenuate high frequencies faster, creating a darker sound.
    /// </summary>
    public float Damping
    {
        get => GetParameter("Damping");
        set => SetParameter("Damping", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Size/density parameter (0.0 - 1.0).
    /// Controls the perceived size and density of the frozen sound.
    /// </summary>
    public float Size
    {
        get => GetParameter("Size");
        set => SetParameter("Size", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Dry/wet mix (0.0 - 1.0).
    /// </summary>
    public float DryWet
    {
        get => Mix;
        set => Mix = value;
    }

    /// <summary>
    /// Shimmer amount (0.0 - 1.0).
    /// Adds subtle pitch-shifted harmonics to the frozen sound for added brilliance.
    /// </summary>
    public float Shimmer
    {
        get => GetParameter("Shimmer");
        set => SetParameter("Shimmer", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Phase evolution rate (0.0 - 1.0).
    /// Controls how much the frozen sound evolves over time.
    /// Higher values create more movement in the sustained sound.
    /// </summary>
    public float PhaseEvolution
    {
        get => GetParameter("PhaseEvolution");
        set => SetParameter("PhaseEvolution", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Diffusion amount (0.0 - 1.0).
    /// Controls the amount of pre-freeze diffusion for smoother transitions.
    /// </summary>
    public float Diffusion
    {
        get => GetParameter("Diffusion");
        set => SetParameter("Diffusion", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Gets the FFT size used by this effect.
    /// </summary>
    public int FftSize => _fftSize;

    /// <summary>
    /// Gets the latency introduced by the effect in samples.
    /// </summary>
    public int LatencySamples => _fftSize;

    /// <summary>
    /// Triggers the freeze effect - captures the current spectrum.
    /// </summary>
    public void TriggerFreeze()
    {
        FreezeEnabled = true;
    }

    /// <summary>
    /// Releases the freeze effect - returns to normal reverb mode.
    /// </summary>
    public void ReleaseFreeze()
    {
        FreezeEnabled = false;
    }

    /// <summary>
    /// Resets the effect state, clearing all buffers.
    /// </summary>
    public void Reset()
    {
        for (int ch = 0; ch < Channels; ch++)
        {
            Array.Clear(_fftBuffer[ch], 0, _fftBuffer[ch].Length);
            Array.Clear(_inputBuffer[ch], 0, _inputBuffer[ch].Length);
            Array.Clear(_outputAccumulator[ch], 0, _outputAccumulator[ch].Length);
            Array.Clear(_frozenMagnitudes[ch], 0, _frozenMagnitudes[ch].Length);
            Array.Clear(_frozenPhases[ch], 0, _frozenPhases[ch].Length);
            Array.Clear(_currentMagnitudes[ch], 0, _currentMagnitudes[ch].Length);
            Array.Clear(_phaseAccumulators[ch], 0, _phaseAccumulators[ch].Length);

            _freezeCrossfade[ch] = 0f;
            _dcBlockerX1[ch] = 0f;
            _dcBlockerY1[ch] = 0f;
            _inputWritePos[ch] = 0;
            _outputReadPos[ch] = 0;
            _samplesUntilNextFft[ch] = 0;

            foreach (var diffuser in _diffusers[ch])
            {
                diffuser.Reset();
            }
        }
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;
        int sampleRate = SampleRate;

        // Cache parameters
        bool freezeEnabled = FreezeEnabled;
        float decayTime = DecayTime;
        float damping = Damping;
        float size = Size;
        float shimmer = Shimmer;
        float phaseEvolution = PhaseEvolution;
        float diffusion = Diffusion;

        // Calculate decay coefficient per FFT frame
        float framesPerSecond = (float)sampleRate / _hopSize;
        float decayPerFrame = MathF.Pow(0.001f, 1f / (decayTime * framesPerSecond));

        // Damping coefficient (frequency-dependent decay)
        float dampingCoeff = damping * 0.5f;

        // Phase evolution amount
        float phaseEvolveAmount = phaseEvolution * 0.1f;

        // Process sample by sample
        for (int i = 0; i < count; i += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                float input = sourceBuffer[i + ch];

                // Apply diffusion (series of allpass filters)
                float diffusedInput = input;
                if (diffusion > 0.01f)
                {
                    for (int d = 0; d < NumDiffusers; d++)
                    {
                        diffusedInput = _diffusers[ch][d].Process(diffusedInput, diffusion);
                    }
                    // Blend diffused and direct
                    diffusedInput = input * (1f - diffusion) + diffusedInput * diffusion;
                }

                // Add input to circular buffer
                _inputBuffer[ch][_inputWritePos[ch]] = diffusedInput;
                _inputWritePos[ch] = (_inputWritePos[ch] + 1) % _fftSize;

                // Update freeze crossfade
                float targetCrossfade = freezeEnabled ? 1f : 0f;
                _freezeCrossfade[ch] += (targetCrossfade - _freezeCrossfade[ch]) * CrossfadeRate;

                // Check if we need to process FFT
                _samplesUntilNextFft[ch]--;
                if (_samplesUntilNextFft[ch] <= 0)
                {
                    _samplesUntilNextFft[ch] = _hopSize;

                    // Copy input buffer with window to FFT buffer
                    int readPos = _inputWritePos[ch];
                    for (int j = 0; j < _fftSize; j++)
                    {
                        int srcIdx = (readPos + j) % _fftSize;
                        _fftBuffer[ch][j].X = _inputBuffer[ch][srcIdx] * _analysisWindow[j];
                        _fftBuffer[ch][j].Y = 0f;
                    }

                    // Perform forward FFT
                    FastFourierTransform.FFT(true, _fftSizeLog2, _fftBuffer[ch]);

                    // Extract magnitudes and phases
                    for (int bin = 0; bin < _numBins; bin++)
                    {
                        float real = _fftBuffer[ch][bin].X;
                        float imag = _fftBuffer[ch][bin].Y;
                        float magnitude = MathF.Sqrt(real * real + imag * imag);
                        float phase = MathF.Atan2(imag, real);

                        _currentMagnitudes[ch][bin] = magnitude;

                        // When freeze is triggered or becoming active, capture spectrum
                        if (_freezeCrossfade[ch] > 0.01f)
                        {
                            // Blend current spectrum into frozen spectrum
                            float blendRate = 0.05f * (1f - _freezeCrossfade[ch] * 0.9f);
                            _frozenMagnitudes[ch][bin] = _frozenMagnitudes[ch][bin] * (1f - blendRate)
                                                          + magnitude * blendRate;

                            // Initialize phase accumulator if just starting freeze
                            if (_freezeCrossfade[ch] < 0.1f && _freezeCrossfade[ch] > 0.01f)
                            {
                                _frozenPhases[ch][bin] = phase;
                                _phaseAccumulators[ch][bin] = phase;
                            }
                        }
                        else
                        {
                            // Not frozen - update frozen magnitudes to track input
                            _frozenMagnitudes[ch][bin] = magnitude;
                            _frozenPhases[ch][bin] = phase;
                            _phaseAccumulators[ch][bin] = phase;
                        }
                    }

                    // Apply damping and decay to frozen spectrum
                    for (int bin = 0; bin < _numBins; bin++)
                    {
                        // Frequency-dependent damping (more damping at higher frequencies)
                        float freq = (float)bin / _numBins;
                        float freqDamping = 1f - dampingCoeff * freq * freq;

                        if (_freezeCrossfade[ch] > 0.5f)
                        {
                            // Frozen: apply subtle damping over time
                            _frozenMagnitudes[ch][bin] *= freqDamping * (1f - dampingCoeff * 0.001f);
                        }
                        else
                        {
                            // Not frozen: apply decay
                            _frozenMagnitudes[ch][bin] *= decayPerFrame * freqDamping;
                        }

                        // Prevent magnitude from going negative
                        _frozenMagnitudes[ch][bin] = MathF.Max(0f, _frozenMagnitudes[ch][bin]);
                    }

                    // Apply shimmer (subtle octave up feedback)
                    if (shimmer > 0.01f)
                    {
                        for (int bin = _numBins / 2 - 1; bin >= 0; bin--)
                        {
                            int octaveBin = bin * 2;
                            if (octaveBin < _numBins)
                            {
                                _frozenMagnitudes[ch][octaveBin] += _frozenMagnitudes[ch][bin] * shimmer * 0.3f;
                            }
                        }
                    }

                    // Calculate output spectrum
                    for (int bin = 0; bin < _numBins; bin++)
                    {
                        float magnitude;
                        float phase;

                        float freezeAmount = _freezeCrossfade[ch];

                        if (freezeAmount > 0.01f)
                        {
                            // Blend between live and frozen magnitudes
                            magnitude = _currentMagnitudes[ch][bin] * (1f - freezeAmount)
                                        + _frozenMagnitudes[ch][bin] * freezeAmount * size;

                            // Evolve phase for frozen component
                            _phaseAccumulators[ch][bin] += _phaseIncrements[ch][bin];

                            // Add random phase evolution for movement
                            if (phaseEvolveAmount > 0.001f)
                            {
                                _phaseAccumulators[ch][bin] += (float)(_random.NextDouble() - 0.5)
                                                                * phaseEvolveAmount;
                            }

                            // Wrap phase
                            while (_phaseAccumulators[ch][bin] > MathF.PI)
                                _phaseAccumulators[ch][bin] -= 2f * MathF.PI;
                            while (_phaseAccumulators[ch][bin] < -MathF.PI)
                                _phaseAccumulators[ch][bin] += 2f * MathF.PI;

                            // Blend phases
                            float livePhase = MathF.Atan2(_fftBuffer[ch][bin].Y, _fftBuffer[ch][bin].X);
                            phase = livePhase * (1f - freezeAmount) + _phaseAccumulators[ch][bin] * freezeAmount;
                        }
                        else
                        {
                            // Not frozen - just pass through with decay
                            magnitude = _frozenMagnitudes[ch][bin] * size;
                            phase = _frozenPhases[ch][bin];
                        }

                        // Reconstruct complex spectrum
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

                    // Apply synthesis window and add to output accumulator (overlap-add)
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

                // Apply DC blocking filter
                output = DCBlock(output, ch);

                // Soft clip to prevent harsh distortion
                output = SoftClip(output);

                destBuffer[offset + i + ch] = output;
            }
        }
    }

    /// <summary>
    /// DC blocking filter to prevent low-frequency buildup.
    /// </summary>
    private float DCBlock(float input, int channel)
    {
        const float R = 0.995f;
        float output = input - _dcBlockerX1[channel] + R * _dcBlockerY1[channel];
        _dcBlockerX1[channel] = input;
        _dcBlockerY1[channel] = output;
        return output;
    }

    /// <summary>
    /// Soft clipping function to prevent harsh distortion.
    /// </summary>
    private static float SoftClip(float x)
    {
        if (x > 1f)
            return 1f - MathF.Exp(1f - x);
        if (x < -1f)
            return -1f + MathF.Exp(1f + x);
        return x;
    }

    /// <summary>
    /// Creates a preset for ambient freeze pads.
    /// </summary>
    public static FreezeReverb CreateAmbientPreset(ISampleProvider source)
    {
        var effect = new FreezeReverb(source, "Freeze Reverb (Ambient)",
            FreezeReverbFftSize.Size4096);
        effect.DecayTime = 15f;
        effect.Damping = 0.4f;
        effect.Size = 0.8f;
        effect.Shimmer = 0.2f;
        effect.PhaseEvolution = 0.15f;
        effect.Diffusion = 0.6f;
        effect.Mix = 0.5f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for infinite drone effects.
    /// </summary>
    public static FreezeReverb CreateDronePreset(ISampleProvider source)
    {
        var effect = new FreezeReverb(source, "Freeze Reverb (Drone)",
            FreezeReverbFftSize.Size8192);
        effect.FreezeEnabled = true;
        effect.DecayTime = 60f;
        effect.Damping = 0.2f;
        effect.Size = 1f;
        effect.Shimmer = 0f;
        effect.PhaseEvolution = 0.05f;
        effect.Diffusion = 0.7f;
        effect.Mix = 0.7f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for shimmering freeze with movement.
    /// </summary>
    public static FreezeReverb CreateShimmerFreezePreset(ISampleProvider source)
    {
        var effect = new FreezeReverb(source, "Freeze Reverb (Shimmer)",
            FreezeReverbFftSize.Size2048);
        effect.DecayTime = 20f;
        effect.Damping = 0.3f;
        effect.Size = 0.75f;
        effect.Shimmer = 0.5f;
        effect.PhaseEvolution = 0.2f;
        effect.Diffusion = 0.5f;
        effect.Mix = 0.55f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for dark, moody freeze tones.
    /// </summary>
    public static FreezeReverb CreateDarkFreezePreset(ISampleProvider source)
    {
        var effect = new FreezeReverb(source, "Freeze Reverb (Dark)",
            FreezeReverbFftSize.Size4096);
        effect.DecayTime = 25f;
        effect.Damping = 0.7f;
        effect.Size = 0.6f;
        effect.Shimmer = 0f;
        effect.PhaseEvolution = 0.08f;
        effect.Diffusion = 0.4f;
        effect.Mix = 0.6f;
        return effect;
    }

    /// <summary>
    /// Creates a preset optimized for live performance with fast response.
    /// </summary>
    public static FreezeReverb CreateLivePreset(ISampleProvider source)
    {
        var effect = new FreezeReverb(source, "Freeze Reverb (Live)",
            FreezeReverbFftSize.Size1024);
        effect.DecayTime = 8f;
        effect.Damping = 0.35f;
        effect.Size = 0.7f;
        effect.Shimmer = 0.1f;
        effect.PhaseEvolution = 0.12f;
        effect.Diffusion = 0.55f;
        effect.Mix = 0.45f;
        return effect;
    }

    #region Inner Classes

    /// <summary>
    /// Simple allpass diffuser for pre-freeze processing.
    /// </summary>
    private class AllpassDiffuser
    {
        private readonly float[] _buffer;
        private int _writePos;
        private readonly int _delayLength;

        public AllpassDiffuser(int delaySamples)
        {
            _delayLength = Math.Max(1, delaySamples);
            _buffer = new float[_delayLength];
            _writePos = 0;
        }

        public float Process(float input, float feedback)
        {
            int readPos = (_writePos - _delayLength + _buffer.Length) % _buffer.Length;
            float delayed = _buffer[readPos];

            float output = -input * feedback + delayed;
            _buffer[_writePos] = input + delayed * feedback;

            _writePos = (_writePos + 1) % _buffer.Length;

            return output;
        }

        public void Reset()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _writePos = 0;
        }
    }

    #endregion
}
