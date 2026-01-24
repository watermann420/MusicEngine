//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Shimmer reverb effect with pitch-shifted feedback for ethereal, evolving tails.


using NAudio.Wave;


namespace MusicEngine.Core.Effects.TimeBased;


/// <summary>
/// Shimmer reverb effect that creates ethereal, evolving reverb tails by feeding
/// pitch-shifted audio back into the reverb. The cascading harmonics create a
/// distinctive shimmering, crystalline sound often used in ambient and cinematic music.
/// </summary>
/// <remarks>
/// The effect combines:
/// 1. A Schroeder-style reverb network (comb filters + allpass filters)
/// 2. A granular pitch shifter in the feedback path
/// 3. Optional secondary pitch shift for intervals (fifths, octaves, etc.)
///
/// The pitch-shifted reverb is continuously fed back, creating ascending or descending
/// harmonic cascades depending on the pitch shift direction.
/// </remarks>
public class ShimmerReverbEffect : EffectBase
{
    // Reverb network components
    private CombFilter[][] _combFilters;       // [channel][combFilter]
    private AllpassFilter[][] _allpassFilters; // [channel][allpassFilter]

    // Early reflections
    private CircularBuffer[][] _earlyReflections; // [channel][tap]
    private readonly int[] _earlyDelays = { 17, 23, 31, 37, 47, 53, 61, 67 }; // Prime delays in ms

    // Pitch shifter components (granular approach)
    private GranularPitchShifter[] _primaryPitchShifters;
    private GranularPitchShifter[]? _secondaryPitchShifters;

    // Pre-delay buffer
    private CircularBuffer[] _preDelayBuffers;

    // Feedback accumulators
    private float[] _feedbackAccumulator;

    // DC blocker state (per channel)
    private float[] _dcBlockerX1;
    private float[] _dcBlockerY1;

    private const int NumCombs = 8;
    private const int NumAllpass = 4;
    private const int NumEarlyTaps = 8;

    // Comb filter delay times (in samples at 44.1kHz, scaled for sample rate)
    private readonly int[] _combBaseDelays = { 1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617 };
    private readonly int[] _allpassDelays = { 225, 341, 441, 556 };

    // Maximum pre-delay in samples (500ms at 48kHz)
    private const int MaxPreDelaySamples = 24000;

    /// <summary>
    /// Creates a new shimmer reverb effect.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    public ShimmerReverbEffect(ISampleProvider source) : this(source, "Shimmer Reverb")
    {
    }

    /// <summary>
    /// Creates a new shimmer reverb effect with a custom name.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <param name="name">Effect name</param>
    public ShimmerReverbEffect(ISampleProvider source, string name) : base(source, name)
    {
        int channels = source.WaveFormat.Channels;
        float sampleRateRatio = SampleRate / 44100f;

        // Initialize early reflections
        _earlyReflections = new CircularBuffer[channels][];
        for (int ch = 0; ch < channels; ch++)
        {
            _earlyReflections[ch] = new CircularBuffer[NumEarlyTaps];
            for (int tap = 0; tap < NumEarlyTaps; tap++)
            {
                // Scale delay times for sample rate and add some margin
                int maxDelay = (int)(_earlyDelays[tap] * SampleRate / 1000f) + 100;
                _earlyReflections[ch][tap] = new CircularBuffer(maxDelay);
            }
        }

        // Initialize comb filters (scaled for sample rate)
        _combFilters = new CombFilter[channels][];
        for (int ch = 0; ch < channels; ch++)
        {
            _combFilters[ch] = new CombFilter[NumCombs];
            for (int i = 0; i < NumCombs; i++)
            {
                int scaledDelay = (int)(_combBaseDelays[i] * sampleRateRatio);
                // Slightly detune left/right for wider stereo
                if (channels == 2 && ch == 1)
                {
                    scaledDelay = (int)(scaledDelay * 1.0123f); // ~2% detune
                }
                _combFilters[ch][i] = new CombFilter(scaledDelay * 2); // Extra headroom for size
            }
        }

        // Initialize allpass filters
        _allpassFilters = new AllpassFilter[channels][];
        for (int ch = 0; ch < channels; ch++)
        {
            _allpassFilters[ch] = new AllpassFilter[NumAllpass];
            for (int i = 0; i < NumAllpass; i++)
            {
                int scaledDelay = (int)(_allpassDelays[i] * sampleRateRatio);
                _allpassFilters[ch][i] = new AllpassFilter(scaledDelay * 2);
            }
        }

        // Initialize pitch shifters
        _primaryPitchShifters = new GranularPitchShifter[channels];
        for (int ch = 0; ch < channels; ch++)
        {
            _primaryPitchShifters[ch] = new GranularPitchShifter(SampleRate);
        }

        // Initialize pre-delay buffers
        _preDelayBuffers = new CircularBuffer[channels];
        for (int ch = 0; ch < channels; ch++)
        {
            _preDelayBuffers[ch] = new CircularBuffer(MaxPreDelaySamples);
        }

        // Initialize feedback accumulators and DC blockers
        _feedbackAccumulator = new float[channels];
        _dcBlockerX1 = new float[channels];
        _dcBlockerY1 = new float[channels];

        // Register parameters with defaults
        RegisterParameter("DecayTime", 3.0f);          // 3 second decay
        RegisterParameter("RoomSize", 0.7f);           // 70% room size
        RegisterParameter("Damping", 0.4f);            // 40% damping
        RegisterParameter("ShimmerAmount", 0.5f);      // 50% shimmer feedback
        RegisterParameter("PitchShift", 12f);          // +12 semitones (1 octave)
        RegisterParameter("SecondaryPitch", 0f);       // No secondary pitch (0 = disabled)
        RegisterParameter("SecondaryAmount", 0f);      // Secondary pitch mix
        RegisterParameter("PreDelay", 20f);            // 20ms pre-delay
        RegisterParameter("EarlyLevel", 0.2f);         // 20% early reflections
        RegisterParameter("Width", 1.0f);              // Full stereo width
        RegisterParameter("Mix", 0.35f);               // 35% wet
    }

    /// <summary>
    /// Reverb decay time in seconds (0.5 - 20.0).
    /// Controls how long the reverb tail lasts.
    /// </summary>
    public float DecayTime
    {
        get => GetParameter("DecayTime");
        set => SetParameter("DecayTime", Math.Clamp(value, 0.5f, 20f));
    }

    /// <summary>
    /// Room size (0.0 - 1.0).
    /// Controls the perceived size of the virtual space.
    /// </summary>
    public float RoomSize
    {
        get => GetParameter("RoomSize");
        set => SetParameter("RoomSize", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// High frequency damping (0.0 - 1.0).
    /// Higher values create a darker, more absorbed sound.
    /// </summary>
    public float Damping
    {
        get => GetParameter("Damping");
        set => SetParameter("Damping", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Shimmer amount (0.0 - 1.0).
    /// Controls how much pitch-shifted signal is fed back into the reverb.
    /// Higher values create more pronounced harmonic cascades.
    /// </summary>
    public float ShimmerAmount
    {
        get => GetParameter("ShimmerAmount");
        set => SetParameter("ShimmerAmount", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Primary pitch shift in semitones (-24 to +24).
    /// Typical values: +12 (octave up), +7 (fifth up), -12 (octave down).
    /// </summary>
    public float PitchShift
    {
        get => GetParameter("PitchShift");
        set
        {
            SetParameter("PitchShift", Math.Clamp(value, -24f, 24f));
            UpdatePitchShifters();
        }
    }

    /// <summary>
    /// Secondary pitch shift in semitones (-24 to +24).
    /// Set to 0 to disable. Common values: +7 (fifth), +19 (octave + fifth).
    /// </summary>
    public float SecondaryPitch
    {
        get => GetParameter("SecondaryPitch");
        set
        {
            SetParameter("SecondaryPitch", Math.Clamp(value, -24f, 24f));
            UpdateSecondaryPitchShifters();
        }
    }

    /// <summary>
    /// Secondary pitch shifter mix amount (0.0 - 1.0).
    /// Controls the blend of the secondary pitch shift.
    /// </summary>
    public float SecondaryAmount
    {
        get => GetParameter("SecondaryAmount");
        set => SetParameter("SecondaryAmount", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Pre-delay time in milliseconds (0 - 500).
    /// Adds a gap before the reverb onset.
    /// </summary>
    public float PreDelay
    {
        get => GetParameter("PreDelay");
        set => SetParameter("PreDelay", Math.Clamp(value, 0f, 500f));
    }

    /// <summary>
    /// Early reflections level (0.0 - 1.0).
    /// Controls the prominence of initial room reflections.
    /// </summary>
    public float EarlyLevel
    {
        get => GetParameter("EarlyLevel");
        set => SetParameter("EarlyLevel", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Stereo width (0.0 - 1.0).
    /// Controls the stereo spread of the reverb.
    /// </summary>
    public float Width
    {
        get => GetParameter("Width");
        set => SetParameter("Width", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Dry/wet mix (0.0 - 1.0).
    /// </summary>
    public float DryWet
    {
        get => Mix;
        set => Mix = value;
    }

    private void UpdatePitchShifters()
    {
        float semitones = GetParameter("PitchShift");
        float pitchRatio = MathF.Pow(2f, semitones / 12f);

        foreach (var shifter in _primaryPitchShifters)
        {
            shifter.PitchRatio = pitchRatio;
        }
    }

    private void UpdateSecondaryPitchShifters()
    {
        float semitones = GetParameter("SecondaryPitch");

        // If secondary pitch is 0, disable secondary shifters
        if (MathF.Abs(semitones) < 0.01f)
        {
            _secondaryPitchShifters = null;
            return;
        }

        // Initialize secondary pitch shifters if needed
        if (_secondaryPitchShifters == null)
        {
            _secondaryPitchShifters = new GranularPitchShifter[Channels];
            for (int ch = 0; ch < Channels; ch++)
            {
                _secondaryPitchShifters[ch] = new GranularPitchShifter(SampleRate);
            }
        }

        float pitchRatio = MathF.Pow(2f, semitones / 12f);
        foreach (var shifter in _secondaryPitchShifters)
        {
            shifter.PitchRatio = pitchRatio;
        }
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;
        int sampleRate = SampleRate;

        // Get current parameter values
        float decayTime = DecayTime;
        float roomSize = RoomSize;
        float damping = Damping;
        float shimmerAmount = ShimmerAmount;
        float preDelayMs = PreDelay;
        float earlyLevel = EarlyLevel;
        float width = Width;
        float secondaryAmount = SecondaryAmount;

        // Calculate reverb coefficients
        // Feedback based on room size and decay time
        float baseFeedback = 0.28f + roomSize * 0.7f;
        float decayFactor = 1f - (1f / (decayTime * 2f));
        float feedback = baseFeedback * decayFactor;
        feedback = Math.Clamp(feedback, 0.1f, 0.98f);

        float dampingCoeff = damping * 0.5f;

        // Pre-delay in samples
        int preDelaySamples = (int)(preDelayMs * sampleRate / 1000f);
        preDelaySamples = Math.Clamp(preDelaySamples, 1, MaxPreDelaySamples - 1);

        // Update comb filter parameters
        for (int ch = 0; ch < channels; ch++)
        {
            for (int i = 0; i < NumCombs; i++)
            {
                _combFilters[ch][i].SetFeedback(feedback);
                _combFilters[ch][i].SetDamping(dampingCoeff);
            }
        }

        // Process samples
        for (int i = 0; i < count; i += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                int index = i + ch;
                float input = sourceBuffer[index];

                // Apply pre-delay
                _preDelayBuffers[ch].Write(input);
                float delayedInput = _preDelayBuffers[ch].Read(preDelaySamples);

                // Mix in shimmer feedback
                float reverbInput = delayedInput + _feedbackAccumulator[ch] * shimmerAmount;

                // Soft clip to prevent runaway feedback
                reverbInput = SoftClip(reverbInput);

                // Early reflections
                float early = 0f;
                for (int tap = 0; tap < NumEarlyTaps; tap++)
                {
                    int delaySamples = (int)(_earlyDelays[tap] * sampleRate / 1000f);
                    float tapGain = 1f / (1f + tap * 0.3f); // Gradual decay
                    _earlyReflections[ch][tap].Write(reverbInput);
                    early += _earlyReflections[ch][tap].Read(delaySamples) * tapGain;
                }
                early = early * earlyLevel / NumEarlyTaps;

                // Late reverb (Schroeder network)
                // Parallel comb filters
                float combSum = 0f;
                for (int c = 0; c < NumCombs; c++)
                {
                    combSum += _combFilters[ch][c].Process(reverbInput);
                }
                float late = combSum / NumCombs;

                // Series allpass filters (diffusion)
                for (int a = 0; a < NumAllpass; a++)
                {
                    late = _allpassFilters[ch][a].Process(late);
                }

                // Combine early and late reverb
                float reverb = early + late;

                // Apply pitch shifting to reverb output for feedback
                float pitched = _primaryPitchShifters[ch].Process(reverb);

                // Add secondary pitch if enabled
                if (_secondaryPitchShifters != null && secondaryAmount > 0f)
                {
                    float secondaryPitched = _secondaryPitchShifters[ch].Process(reverb);
                    pitched = pitched * (1f - secondaryAmount) + secondaryPitched * secondaryAmount;
                }

                // DC blocking filter to prevent buildup
                float dcBlocked = DCBlock(pitched, ch);

                // Store for next iteration's feedback
                _feedbackAccumulator[ch] = dcBlocked;

                // Apply stereo width
                float output = reverb;
                if (channels == 2)
                {
                    if (ch == 1)
                    {
                        // Right channel: blend with inverted signal for width
                        float mono = (destBuffer[offset + i] + reverb) * 0.5f;
                        float side = (reverb - destBuffer[offset + i]) * 0.5f;
                        output = mono + side * width;
                    }
                }

                destBuffer[offset + index] = output;
            }

            // Cross-feed for stereo width on left channel (after right is calculated)
            if (channels == 2)
            {
                float left = destBuffer[offset + i];
                float right = destBuffer[offset + i + 1];
                float mono = (left + right) * 0.5f;
                float side = (left - right) * 0.5f;
                destBuffer[offset + i] = mono + side * width;
            }
        }
    }

    /// <summary>
    /// Soft clipping function to prevent harsh distortion in feedback.
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
    /// DC blocking filter to prevent low-frequency buildup.
    /// </summary>
    private float DCBlock(float input, int channel)
    {
        // High-pass filter at ~5Hz
        const float R = 0.995f;
        float output = input - _dcBlockerX1[channel] + R * _dcBlockerY1[channel];
        _dcBlockerX1[channel] = input;
        _dcBlockerY1[channel] = output;
        return output;
    }

    protected override void OnParameterChanged(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "pitchshift":
                UpdatePitchShifters();
                break;
            case "secondarypitch":
                UpdateSecondaryPitchShifters();
                break;
        }
    }

    /// <summary>
    /// Creates a classic shimmer preset (octave up).
    /// </summary>
    public static ShimmerReverbEffect CreateClassicShimmer(ISampleProvider source)
    {
        var effect = new ShimmerReverbEffect(source, "Classic Shimmer");
        effect.DecayTime = 4f;
        effect.RoomSize = 0.8f;
        effect.Damping = 0.3f;
        effect.ShimmerAmount = 0.45f;
        effect.PitchShift = 12f; // Octave up
        effect.PreDelay = 30f;
        effect.Mix = 0.4f;
        return effect;
    }

    /// <summary>
    /// Creates an ethereal pad preset (octave + fifth).
    /// </summary>
    public static ShimmerReverbEffect CreateEtherealPad(ISampleProvider source)
    {
        var effect = new ShimmerReverbEffect(source, "Ethereal Pad");
        effect.DecayTime = 8f;
        effect.RoomSize = 0.9f;
        effect.Damping = 0.4f;
        effect.ShimmerAmount = 0.6f;
        effect.PitchShift = 12f; // Octave
        effect.SecondaryPitch = 7f; // Fifth
        effect.SecondaryAmount = 0.5f;
        effect.PreDelay = 50f;
        effect.Mix = 0.5f;
        return effect;
    }

    /// <summary>
    /// Creates a dark shimmer preset (octave down).
    /// </summary>
    public static ShimmerReverbEffect CreateDarkShimmer(ISampleProvider source)
    {
        var effect = new ShimmerReverbEffect(source, "Dark Shimmer");
        effect.DecayTime = 6f;
        effect.RoomSize = 0.85f;
        effect.Damping = 0.7f;
        effect.ShimmerAmount = 0.35f;
        effect.PitchShift = -12f; // Octave down
        effect.PreDelay = 40f;
        effect.Mix = 0.35f;
        return effect;
    }

    /// <summary>
    /// Creates a cinematic shimmer preset with long decay.
    /// </summary>
    public static ShimmerReverbEffect CreateCinematic(ISampleProvider source)
    {
        var effect = new ShimmerReverbEffect(source, "Cinematic Shimmer");
        effect.DecayTime = 12f;
        effect.RoomSize = 0.95f;
        effect.Damping = 0.25f;
        effect.ShimmerAmount = 0.55f;
        effect.PitchShift = 12f;
        effect.SecondaryPitch = 19f; // Octave + fifth
        effect.SecondaryAmount = 0.3f;
        effect.PreDelay = 60f;
        effect.EarlyLevel = 0.1f;
        effect.Width = 1f;
        effect.Mix = 0.45f;
        return effect;
    }

    #region Inner Classes

    /// <summary>
    /// Comb filter with damping for reverb.
    /// </summary>
    private class CombFilter
    {
        private readonly CircularBuffer _buffer;
        private float _feedback;
        private float _damping;
        private float _filterState;
        private int _delayLength;

        public CombFilter(int maxSize)
        {
            _buffer = new CircularBuffer(maxSize);
            _delayLength = maxSize / 2;
            _feedback = 0.5f;
            _damping = 0.5f;
            _filterState = 0f;
        }

        public void SetFeedback(float feedback) => _feedback = feedback;
        public void SetDamping(float damping) => _damping = damping;
        public void SetDelay(int samples) => _delayLength = Math.Min(samples, _buffer.Length - 1);

        public float Process(float input)
        {
            float delayed = _buffer.Read(_delayLength);

            // One-pole lowpass filter for damping
            _filterState = delayed * (1f - _damping) + _filterState * _damping;

            _buffer.Write(input + _filterState * _feedback);

            return delayed;
        }
    }

    /// <summary>
    /// Allpass filter for diffusion.
    /// </summary>
    private class AllpassFilter
    {
        private readonly CircularBuffer _buffer;
        private readonly int _delayLength;
        private const float Gain = 0.5f;

        public AllpassFilter(int size)
        {
            _buffer = new CircularBuffer(size);
            _delayLength = size - 1;
        }

        public float Process(float input)
        {
            float delayed = _buffer.Read(_delayLength);
            float output = -input * Gain + delayed;
            _buffer.Write(input + delayed * Gain);
            return output;
        }
    }

    /// <summary>
    /// Circular buffer with linear interpolation support.
    /// </summary>
    private class CircularBuffer
    {
        private readonly float[] _buffer;
        private int _writePos;

        public int Length => _buffer.Length;

        public CircularBuffer(int size)
        {
            _buffer = new float[Math.Max(1, size)];
            _writePos = 0;
        }

        public void Write(float sample)
        {
            _buffer[_writePos] = sample;
            _writePos = (_writePos + 1) % _buffer.Length;
        }

        public float Read(int delaySamples)
        {
            int readPos = _writePos - delaySamples - 1;
            while (readPos < 0) readPos += _buffer.Length;
            readPos %= _buffer.Length;
            return _buffer[readPos];
        }

        public float ReadInterpolated(float delaySamples)
        {
            float readPos = _writePos - delaySamples - 1;
            while (readPos < 0) readPos += _buffer.Length;

            int pos1 = (int)readPos % _buffer.Length;
            int pos2 = (pos1 + 1) % _buffer.Length;
            float frac = readPos - MathF.Floor(readPos);

            return _buffer[pos1] * (1f - frac) + _buffer[pos2] * frac;
        }
    }

    /// <summary>
    /// Granular pitch shifter using overlap-add with Hann windowing.
    /// Provides high-quality pitch shifting suitable for shimmer reverb feedback.
    /// </summary>
    private class GranularPitchShifter
    {
        private readonly int _sampleRate;
        private readonly float[] _inputBuffer;
        private readonly float[] _outputBuffer;
        private readonly float[] _window;

        private int _inputWritePos;
        private int _grainReadPos1;
        private int _grainReadPos2;
        private float _grainPhase;

        private const int GrainSize = 2048;      // Grain size in samples
        private const int HopSize = GrainSize / 4; // 75% overlap
        private const int BufferSize = GrainSize * 4;

        public float PitchRatio { get; set; } = 2f; // Default to octave up

        public GranularPitchShifter(int sampleRate)
        {
            _sampleRate = sampleRate;
            _inputBuffer = new float[BufferSize];
            _outputBuffer = new float[BufferSize];

            // Generate Hann window
            _window = new float[GrainSize];
            for (int i = 0; i < GrainSize; i++)
            {
                _window[i] = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (GrainSize - 1)));
            }

            _inputWritePos = 0;
            _grainReadPos1 = 0;
            _grainReadPos2 = GrainSize / 2; // Offset for overlap
            _grainPhase = 0f;
        }

        public float Process(float input)
        {
            // Write input to circular buffer
            _inputBuffer[_inputWritePos] = input;

            // Calculate output from two overlapping grains
            float output = 0f;

            // Grain 1
            int readIdx1 = (int)_grainReadPos1 % BufferSize;
            int windowIdx1 = (int)_grainPhase % GrainSize;
            float sample1 = _inputBuffer[readIdx1] * _window[windowIdx1];

            // Grain 2 (offset by half grain size for smooth crossfade)
            int readIdx2 = (int)_grainReadPos2 % BufferSize;
            int windowIdx2 = (windowIdx1 + GrainSize / 2) % GrainSize;
            float sample2 = _inputBuffer[readIdx2] * _window[windowIdx2];

            output = sample1 + sample2;

            // Advance positions
            _inputWritePos = (_inputWritePos + 1) % BufferSize;

            // Advance grain read positions based on pitch ratio
            float increment = PitchRatio;
            _grainReadPos1 = (_grainReadPos1 + (int)increment) % BufferSize;
            _grainReadPos2 = (_grainReadPos2 + (int)increment) % BufferSize;

            // Fractional part handling for smoother pitch
            _grainPhase += increment;

            // Reset grain when window completes
            if (_grainPhase >= GrainSize)
            {
                _grainPhase -= GrainSize;
                // Jump grain read position to maintain continuity
                _grainReadPos1 = (_inputWritePos - GrainSize + BufferSize) % BufferSize;
            }

            if (_grainPhase >= GrainSize / 2 && _grainPhase < GrainSize / 2 + increment)
            {
                // Reset second grain at halfway point
                _grainReadPos2 = (_inputWritePos - GrainSize + BufferSize) % BufferSize;
            }

            return output * 0.5f; // Normalize for overlap
        }
    }

    #endregion
}
