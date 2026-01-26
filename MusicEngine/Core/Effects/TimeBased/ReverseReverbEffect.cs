// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Reverb effect processor.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.TimeBased;

/// <summary>
/// Reverse reverb effect that creates a "swell" before the sound.
/// Captures audio, reverses it, applies reverb, then outputs the result
/// so the reverb tail builds up BEFORE the original sound occurs.
/// </summary>
public class ReverseReverbEffect : EffectBase
{
    // Capture buffers for incoming audio (per channel)
    private float[][] _captureBuffers;
    private int _captureWritePos;
    private int _captureSamplesCollected;

    // Reverb processing buffers (per channel)
    private float[][] _reverbBuffers;
    private int _reverbReadPos;
    private int _reverbSamplesAvailable;

    // Pre-delay buffer
    private float[][] _preDelayBuffers;
    private int _preDelayWritePos;

    // Reverb components (Schroeder algorithm per channel)
    private CombFilter[][] _combFilters;
    private AllpassFilter[][] _allpassFilters;

    private const int NumCombs = 8;
    private const int NumAllpass = 4;

    // Comb filter delay times (samples at 44.1kHz)
    private readonly int[] _combDelays = { 1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617 };
    private readonly int[] _allpassDelays = { 225, 341, 441, 556 };

    // Maximum sizes
    private const int MaxBufferSizeSamples = 441000; // 10 seconds at 44.1kHz
    private const int MaxPreDelaySamples = 44100;    // 1 second at 44.1kHz

    private int _currentBufferSize;
    private bool _isProcessingReverse;

    /// <summary>
    /// Creates a new reverse reverb effect
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <param name="name">Effect name</param>
    public ReverseReverbEffect(ISampleProvider source, string name)
        : base(source, name)
    {
        int channels = source.WaveFormat.Channels;
        int sampleRate = source.WaveFormat.SampleRate;

        // Initialize capture buffers
        _captureBuffers = new float[channels][];
        for (int ch = 0; ch < channels; ch++)
        {
            _captureBuffers[ch] = new float[MaxBufferSizeSamples];
        }

        // Initialize reverb output buffers
        _reverbBuffers = new float[channels][];
        for (int ch = 0; ch < channels; ch++)
        {
            _reverbBuffers[ch] = new float[MaxBufferSizeSamples];
        }

        // Initialize pre-delay buffers
        _preDelayBuffers = new float[channels][];
        for (int ch = 0; ch < channels; ch++)
        {
            _preDelayBuffers[ch] = new float[MaxPreDelaySamples];
        }

        // Initialize reverb components (per channel)
        _combFilters = new CombFilter[channels][];
        _allpassFilters = new AllpassFilter[channels][];

        for (int ch = 0; ch < channels; ch++)
        {
            _combFilters[ch] = new CombFilter[NumCombs];
            for (int i = 0; i < NumCombs; i++)
            {
                _combFilters[ch][i] = new CombFilter(_combDelays[i]);
            }

            _allpassFilters[ch] = new AllpassFilter[NumAllpass];
            for (int i = 0; i < NumAllpass; i++)
            {
                _allpassFilters[ch][i] = new AllpassFilter(_allpassDelays[i]);
            }
        }

        // Initialize parameters
        RegisterParameter("BufferSize", 0.5f);      // 500ms capture buffer
        RegisterParameter("PreDelay", 0.0f);        // No pre-delay
        RegisterParameter("ReverbTime", 0.5f);      // Reverb decay (0.0 - 1.0)
        RegisterParameter("Damping", 0.5f);         // High frequency damping
        RegisterParameter("Mix", 0.5f);             // 50% wet

        // Set initial buffer size
        _currentBufferSize = (int)(0.5f * sampleRate);
        _captureWritePos = 0;
        _captureSamplesCollected = 0;
        _reverbReadPos = 0;
        _reverbSamplesAvailable = 0;
        _preDelayWritePos = 0;
        _isProcessingReverse = false;
    }

    /// <summary>
    /// Buffer size in seconds (0.1 - 10.0)
    /// Controls how much audio is captured before reversing
    /// Larger values create longer reverse swell effects
    /// </summary>
    public float BufferSize
    {
        get => GetParameter("BufferSize");
        set => SetParameter("BufferSize", Math.Clamp(value, 0.1f, 10f));
    }

    /// <summary>
    /// Pre-delay in seconds (0.0 - 1.0)
    /// Delay before the reversed reverb begins
    /// </summary>
    public float PreDelay
    {
        get => GetParameter("PreDelay");
        set => SetParameter("PreDelay", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Reverb time / decay (0.0 - 1.0)
    /// Controls the length of the reverb tail
    /// </summary>
    public float ReverbTime
    {
        get => GetParameter("ReverbTime");
        set => SetParameter("ReverbTime", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Damping (0.0 - 1.0)
    /// Controls high frequency absorption in reverb
    /// Higher values = darker reverb sound
    /// </summary>
    public float Damping
    {
        get => GetParameter("Damping");
        set => SetParameter("Damping", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Dry/Wet mix (0.0 - 1.0)
    /// Maps to Mix parameter for compatibility
    /// </summary>
    public float DryWet
    {
        get => Mix;
        set => Mix = value;
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;
        int sampleRate = SampleRate;

        float bufferSizeSeconds = BufferSize;
        float preDelaySeconds = PreDelay;
        float reverbTime = ReverbTime;
        float damping = Damping;

        // Calculate buffer size in samples
        int targetBufferSize = (int)(bufferSizeSeconds * sampleRate);
        targetBufferSize = Math.Clamp(targetBufferSize, sampleRate / 10, MaxBufferSizeSamples);

        // Update buffer size if changed
        if (_currentBufferSize != targetBufferSize)
        {
            _currentBufferSize = targetBufferSize;
            ResetBuffers();
        }

        // Calculate pre-delay in samples
        int preDelaySamples = (int)(preDelaySeconds * sampleRate);
        preDelaySamples = Math.Clamp(preDelaySamples, 0, MaxPreDelaySamples - 1);

        // Calculate reverb feedback based on reverb time
        float feedback = 0.28f + reverbTime * 0.7f; // 0.28 - 0.98
        float dampingCoeff = damping * 0.4f;

        // Update comb filter parameters
        for (int ch = 0; ch < channels; ch++)
        {
            for (int i = 0; i < NumCombs; i++)
            {
                _combFilters[ch][i].SetFeedback(feedback);
                _combFilters[ch][i].SetDamping(dampingCoeff);
            }
        }

        // Process sample by sample
        for (int i = 0; i < count; i += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                int index = i + ch;
                float input = sourceBuffer[index];

                // Capture input audio
                _captureBuffers[ch][_captureWritePos] = input;

                // Get reverse reverb output (from previously processed buffer)
                float reverseReverbOutput = 0f;
                if (_reverbSamplesAvailable > 0)
                {
                    reverseReverbOutput = _reverbBuffers[ch][_reverbReadPos];
                }

                // Apply pre-delay to the reverse reverb
                float delayedReverb;
                if (preDelaySamples > 0)
                {
                    // Write current reverb to pre-delay buffer
                    _preDelayBuffers[ch][_preDelayWritePos] = reverseReverbOutput;

                    // Read delayed reverb
                    int readPos = _preDelayWritePos - preDelaySamples;
                    if (readPos < 0) readPos += MaxPreDelaySamples;
                    delayedReverb = _preDelayBuffers[ch][readPos];
                }
                else
                {
                    delayedReverb = reverseReverbOutput;
                }

                // Output the delayed reverse reverb
                destBuffer[offset + index] = delayedReverb;
            }

            // Advance capture write position (shared across channels)
            _captureWritePos++;
            _captureSamplesCollected++;

            // Advance pre-delay write position
            _preDelayWritePos = (_preDelayWritePos + 1) % MaxPreDelaySamples;

            // Advance reverb read position if we have samples
            if (_reverbSamplesAvailable > 0)
            {
                _reverbReadPos++;
                _reverbSamplesAvailable--;
            }

            // Check if we've collected enough samples to process
            if (_captureSamplesCollected >= _currentBufferSize)
            {
                ProcessReverseReverb();
            }
        }
    }

    /// <summary>
    /// Processes the captured audio: reverses it and applies reverb
    /// </summary>
    private void ProcessReverseReverb()
    {
        if (_isProcessingReverse)
            return;

        _isProcessingReverse = true;

        int channels = Channels;
        int captureLength = _captureSamplesCollected;

        // Process each channel
        for (int ch = 0; ch < channels; ch++)
        {
            // Step 1: Reverse the captured audio into a temp buffer
            float[] reversedBuffer = new float[captureLength];
            for (int i = 0; i < captureLength; i++)
            {
                reversedBuffer[i] = _captureBuffers[ch][captureLength - 1 - i];
            }

            // Step 2: Apply reverb to the reversed audio
            float[] reverbedBuffer = new float[captureLength];

            // Reset reverb filters for fresh processing
            ResetReverbFilters(ch);

            for (int i = 0; i < captureLength; i++)
            {
                float input = reversedBuffer[i];

                // Process through comb filters (parallel)
                float combOutput = 0f;
                for (int c = 0; c < NumCombs; c++)
                {
                    combOutput += _combFilters[ch][c].Process(input);
                }
                float late = combOutput / NumCombs;

                // Process through allpass filters (series)
                for (int a = 0; a < NumAllpass; a++)
                {
                    late = _allpassFilters[ch][a].Process(late);
                }

                reverbedBuffer[i] = late;
            }

            // Step 3: Reverse the reverbed audio again so it plays "forward"
            // but with the reverb tail appearing BEFORE the sound
            for (int i = 0; i < captureLength; i++)
            {
                _reverbBuffers[ch][i] = reverbedBuffer[captureLength - 1 - i];
            }
        }

        // Reset capture position and set reverb output available
        _captureWritePos = 0;
        _captureSamplesCollected = 0;
        _reverbReadPos = 0;
        _reverbSamplesAvailable = captureLength;

        _isProcessingReverse = false;
    }

    /// <summary>
    /// Resets the reverb filters for a channel
    /// </summary>
    private void ResetReverbFilters(int channel)
    {
        for (int i = 0; i < NumCombs; i++)
        {
            _combFilters[channel][i].Reset();
        }
        for (int i = 0; i < NumAllpass; i++)
        {
            _allpassFilters[channel][i].Reset();
        }
    }

    /// <summary>
    /// Resets all capture and output buffers
    /// </summary>
    private void ResetBuffers()
    {
        int channels = Channels;

        for (int ch = 0; ch < channels; ch++)
        {
            Array.Clear(_captureBuffers[ch], 0, _captureBuffers[ch].Length);
            Array.Clear(_reverbBuffers[ch], 0, _reverbBuffers[ch].Length);
            Array.Clear(_preDelayBuffers[ch], 0, _preDelayBuffers[ch].Length);
            ResetReverbFilters(ch);
        }

        _captureWritePos = 0;
        _captureSamplesCollected = 0;
        _reverbReadPos = 0;
        _reverbSamplesAvailable = 0;
        _preDelayWritePos = 0;
    }

    /// <summary>
    /// Comb filter with damping for reverb
    /// </summary>
    private class CombFilter
    {
        private readonly float[] _buffer;
        private int _writePos;
        private float _feedback;
        private float _damping;
        private float _filterState;

        public CombFilter(int size)
        {
            _buffer = new float[size];
            _writePos = 0;
            _feedback = 0.5f;
            _damping = 0.5f;
            _filterState = 0f;
        }

        public void SetFeedback(float feedback) => _feedback = feedback;
        public void SetDamping(float damping) => _damping = damping;

        public float Process(float input)
        {
            float delayed = _buffer[_writePos];

            // One-pole lowpass filter (damping)
            _filterState = delayed * (1f - _damping) + _filterState * _damping;

            _buffer[_writePos] = input + _filterState * _feedback;
            _writePos = (_writePos + 1) % _buffer.Length;

            return delayed;
        }

        public void Reset()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _writePos = 0;
            _filterState = 0f;
        }
    }

    /// <summary>
    /// Allpass filter for reverb diffusion
    /// </summary>
    private class AllpassFilter
    {
        private readonly float[] _buffer;
        private int _writePos;
        private const float Gain = 0.5f;

        public AllpassFilter(int size)
        {
            _buffer = new float[size];
            _writePos = 0;
        }

        public float Process(float input)
        {
            float delayed = _buffer[_writePos];
            float output = -input + delayed;
            _buffer[_writePos] = input + delayed * Gain;
            _writePos = (_writePos + 1) % _buffer.Length;
            return output;
        }

        public void Reset()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _writePos = 0;
        }
    }
}
