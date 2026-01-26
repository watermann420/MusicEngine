// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Reverb effect processor.

using NAudio.Wave;
using MusicEngine.Infrastructure.Memory;

namespace MusicEngine.Core.Effects.TimeBased;

/// <summary>
/// Enhanced reverb effect with early reflections and late reverb.
/// Based on Schroeder reverberator with additional early reflection simulation.
/// </summary>
public class EnhancedReverbEffect : EffectBase
{
    // Early reflections (delays for room simulation)
    private CircularBuffer[][] _earlyReflections; // [channel][tap]
    private readonly int[] _earlyDelays = { 19, 23, 29, 31, 37, 41, 43, 47 }; // Prime numbers for natural sound

    // Late reverb (Schroeder algorithm)
    private CombFilter[][] _combFilters;    // [channel][combFilter]
    private AllpassFilter[][] _allpassFilters; // [channel][allpassFilter]

    private const int NumCombs = 8;
    private const int NumAllpass = 4;
    private const int NumEarlyTaps = 8;

    // Delay times in samples (at 44.1kHz)
    private readonly int[] _combDelays = { 1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617 };
    private readonly int[] _allpassDelays = { 225, 341, 441, 556 };

    /// <summary>
    /// Creates a new enhanced reverb effect
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <param name="name">Effect name</param>
    public EnhancedReverbEffect(ISampleProvider source, string name)
        : base(source, name)
    {
        int channels = source.WaveFormat.Channels;

        // Initialize early reflections
        _earlyReflections = new CircularBuffer[channels][];
        for (int ch = 0; ch < channels; ch++)
        {
            _earlyReflections[ch] = new CircularBuffer[NumEarlyTaps];
            for (int tap = 0; tap < NumEarlyTaps; tap++)
            {
                int delayMs = _earlyDelays[tap];
                _earlyReflections[ch][tap] = new CircularBuffer(delayMs * 50); // Max delay
            }
        }

        // Initialize late reverb
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
        RegisterParameter("RoomSize", 0.5f);       // 50% room size
        RegisterParameter("Damping", 0.5f);        // 50% damping
        RegisterParameter("Width", 1.0f);          // 100% stereo width
        RegisterParameter("EarlyLevel", 0.3f);     // 30% early reflections
        RegisterParameter("LateLevel", 0.7f);      // 70% late reverb
        RegisterParameter("Predelay", 0.0f);       // No predelay
        RegisterParameter("Mix", 0.3f);            // 30% wet
    }

    /// <summary>
    /// Room size (0.0 - 1.0)
    /// Controls the size of the virtual space
    /// </summary>
    public float RoomSize
    {
        get => GetParameter("RoomSize");
        set => SetParameter("RoomSize", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Damping (0.0 - 1.0)
    /// Simulates absorption - higher values = darker sound
    /// </summary>
    public float Damping
    {
        get => GetParameter("Damping");
        set => SetParameter("Damping", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Stereo width (0.0 - 1.0)
    /// Controls the stereo spread of the reverb
    /// </summary>
    public float Width
    {
        get => GetParameter("Width");
        set => SetParameter("Width", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Early reflections level (0.0 - 1.0)
    /// Initial discrete echoes that define room character
    /// </summary>
    public float EarlyLevel
    {
        get => GetParameter("EarlyLevel");
        set => SetParameter("EarlyLevel", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Late reverb level (0.0 - 1.0)
    /// Dense reverb tail
    /// </summary>
    public float LateLevel
    {
        get => GetParameter("LateLevel");
        set => SetParameter("LateLevel", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Predelay in seconds (0.0 - 0.1)
    /// Delay before reverb begins
    /// </summary>
    public float Predelay
    {
        get => GetParameter("Predelay");
        set => SetParameter("Predelay", Math.Clamp(value, 0f, 0.1f));
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

        float roomSize = RoomSize;
        float damping = Damping;
        float width = Width;
        float earlyLevel = EarlyLevel;
        float lateLevel = LateLevel;
        float predelay = Predelay;

        // Calculate reverb parameters
        float feedback = 0.28f + roomSize * 0.7f;  // 0.28 - 0.98
        float dampingCoeff = damping * 0.4f;       // One-pole lowpass coefficient

        // Update comb filter parameters
        for (int ch = 0; ch < channels; ch++)
        {
            for (int i = 0; i < NumCombs; i++)
            {
                _combFilters[ch][i].SetFeedback(feedback);
                _combFilters[ch][i].SetDamping(dampingCoeff);
            }
        }

        for (int i = 0; i < count; i += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                int index = i + ch;
                float input = sourceBuffer[index];

                // Early reflections
                float early = 0f;
                for (int tap = 0; tap < NumEarlyTaps; tap++)
                {
                    int delayMs = _earlyDelays[tap];
                    float tapGain = 1f / (tap + 1); // Decay over time
                    _earlyReflections[ch][tap].Write(input);
                    early += _earlyReflections[ch][tap].Read(delayMs) * tapGain;
                }
                early *= earlyLevel / NumEarlyTaps;

                // Late reverb (Schroeder)
                float late = input;

                // Process through comb filters (parallel)
                float combOutput = 0f;
                for (int c = 0; c < NumCombs; c++)
                {
                    combOutput += _combFilters[ch][c].Process(late);
                }
                late = combOutput / NumCombs;

                // Process through allpass filters (series)
                for (int a = 0; a < NumAllpass; a++)
                {
                    late = _allpassFilters[ch][a].Process(late);
                }
                late *= lateLevel;

                // Apply stereo width (invert right channel partially)
                if (channels == 2 && ch == 1)
                {
                    late = late * (1f - width) + late * width * -1f;
                }

                // Combine early + late
                float reverb = early + late;

                destBuffer[offset + index] = reverb;
            }
        }
    }

    /// <summary>
    /// Comb filter with damping
    /// </summary>
    private class CombFilter
    {
        private readonly CircularBuffer _buffer;
        private float _feedback;
        private float _damping;
        private float _filterState;

        public CombFilter(int size)
        {
            _buffer = new CircularBuffer(size);
            _feedback = 0.5f;
            _damping = 0.5f;
            _filterState = 0f;
        }

        public void SetFeedback(float feedback) => _feedback = feedback;
        public void SetDamping(float damping) => _damping = damping;

        public float Process(float input)
        {
            float delayed = _buffer.Read(0);

            // One-pole lowpass filter (damping)
            _filterState = delayed * (1f - _damping) + _filterState * _damping;

            _buffer.Write(input + _filterState * _feedback);

            return delayed;
        }
    }

    /// <summary>
    /// Allpass filter
    /// </summary>
    private class AllpassFilter
    {
        private readonly CircularBuffer _buffer;
        private const float Gain = 0.5f;

        public AllpassFilter(int size)
        {
            _buffer = new CircularBuffer(size);
        }

        public float Process(float input)
        {
            float delayed = _buffer.Read(0);
            float output = -input + delayed;
            _buffer.Write(input + delayed * Gain);
            return output;
        }
    }

    /// <summary>
    /// Simple circular buffer
    /// </summary>
    private class CircularBuffer
    {
        private readonly float[] _buffer;
        private int _writePos;

        public CircularBuffer(int size)
        {
            _buffer = new float[size];
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
            if (readPos < 0) readPos += _buffer.Length;
            return _buffer[readPos];
        }
    }
}
