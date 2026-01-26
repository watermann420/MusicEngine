//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Algorithmic guitar/bass cabinet simulator with speaker breakup simulation.

using System;
using NAudio.Wave;

namespace MusicEngine.Core.Effects.Special;

/// <summary>
/// Cabinet size types representing common guitar/bass cabinet configurations.
/// </summary>
public enum CabinetType
{
    /// <summary>
    /// Single 12-inch speaker - tight, focused, great for recording.
    /// </summary>
    Cab1x12,

    /// <summary>
    /// Two 12-inch speakers - more low-end, wider stereo image.
    /// </summary>
    Cab2x12,

    /// <summary>
    /// Four 10-inch speakers - classic bass cabinet, punchy mids.
    /// </summary>
    Cab4x10,

    /// <summary>
    /// Four 12-inch speakers - classic rock/metal cabinet, full range.
    /// </summary>
    Cab4x12,

    /// <summary>
    /// Eight 10-inch speakers - large bass cabinet, massive low-end.
    /// </summary>
    Cab8x10,

    /// <summary>
    /// Single 15-inch speaker - deep bass, vintage tone.
    /// </summary>
    Cab1x15,

    /// <summary>
    /// Two 15-inch speakers - huge low-end, dub/reggae style.
    /// </summary>
    Cab2x15
}

/// <summary>
/// Speaker type presets affecting frequency response and breakup characteristics.
/// </summary>
public enum SpeakerType
{
    /// <summary>
    /// Vintage-style speaker with warm, smooth breakup. Classic rock tone.
    /// </summary>
    Vintage,

    /// <summary>
    /// British-style speaker (Celestion-inspired). Pronounced upper-mids.
    /// </summary>
    British,

    /// <summary>
    /// American-style speaker (Jensen/Eminence-inspired). Bright and clean.
    /// </summary>
    American,

    /// <summary>
    /// Modern high-power speaker. Extended range, tight response.
    /// </summary>
    Modern,

    /// <summary>
    /// Bass-optimized speaker. Extended low-end, controlled highs.
    /// </summary>
    Bass,

    /// <summary>
    /// Greenback-style speaker. Classic 60s/70s crunch tone.
    /// </summary>
    Greenback
}

/// <summary>
/// Microphone position for cabinet miking simulation.
/// </summary>
public enum MicPosition
{
    /// <summary>
    /// Center of speaker cone - bright, direct sound.
    /// </summary>
    Center,

    /// <summary>
    /// Between center and edge - balanced tone.
    /// </summary>
    OffCenter,

    /// <summary>
    /// Edge of speaker cone - darker, warmer sound.
    /// </summary>
    Edge,

    /// <summary>
    /// Slightly back from speaker - more room, less direct.
    /// </summary>
    RoomBlend,

    /// <summary>
    /// Combination of close and room mics.
    /// </summary>
    Mixed
}

/// <summary>
/// Algorithmic guitar/bass cabinet simulator with speaker breakup simulation.
/// </summary>
/// <remarks>
/// This effect simulates the frequency response and non-linear characteristics
/// of guitar and bass speaker cabinets without using convolution (impulse responses).
/// Instead, it uses a combination of:
/// - Multi-band EQ curves based on cabinet type
/// - Speaker cone resonance modeling
/// - Non-linear speaker breakup/distortion
/// - Room simulation for cabinet box resonance
/// - Microphone position simulation via filtering
/// </remarks>
public class CabinetSimulator : EffectBase
{
    // Cabinet parameters
    private CabinetType _cabinetType = CabinetType.Cab4x12;
    private SpeakerType _speakerType = SpeakerType.British;
    private MicPosition _micPosition = MicPosition.OffCenter;
    private float _roomSize = 0.3f;

    // Filter states for EQ curves (per channel)
    private BiquadState[] _lowShelfState;
    private BiquadState[] _lowMidState;
    private BiquadState[] _highMidState;
    private BiquadState[] _highShelfState;
    private BiquadState[] _presenceState;
    private BiquadState[] _speakerRolloffState;

    // Speaker resonance state
    private float[] _resonanceState1;
    private float[] _resonanceState2;

    // Room simulation (simple comb filter)
    private float[][] _roomDelayBuffer;
    private int[] _roomDelayPos;
    private int _roomDelayLength;

    // DC blocking
    private float[] _dcBlockState;

    // Breakup state for speaker distortion
    private float[] _breakupState;

    /// <summary>
    /// Creates a new cabinet simulator effect.
    /// </summary>
    /// <param name="source">The audio source to process.</param>
    public CabinetSimulator(ISampleProvider source) : base(source, "Cabinet Simulator")
    {
        int channels = Channels;

        // Initialize filter states
        _lowShelfState = new BiquadState[channels];
        _lowMidState = new BiquadState[channels];
        _highMidState = new BiquadState[channels];
        _highShelfState = new BiquadState[channels];
        _presenceState = new BiquadState[channels];
        _speakerRolloffState = new BiquadState[channels];

        for (int c = 0; c < channels; c++)
        {
            _lowShelfState[c] = new BiquadState();
            _lowMidState[c] = new BiquadState();
            _highMidState[c] = new BiquadState();
            _highShelfState[c] = new BiquadState();
            _presenceState[c] = new BiquadState();
            _speakerRolloffState[c] = new BiquadState();
        }

        // Initialize resonance state
        _resonanceState1 = new float[channels];
        _resonanceState2 = new float[channels];

        // Initialize room delay (max 50ms)
        _roomDelayLength = (int)(SampleRate * 0.05f);
        _roomDelayBuffer = new float[channels][];
        _roomDelayPos = new int[channels];
        for (int c = 0; c < channels; c++)
        {
            _roomDelayBuffer[c] = new float[_roomDelayLength];
        }

        // Initialize DC blocking
        _dcBlockState = new float[channels];

        // Initialize breakup state
        _breakupState = new float[channels];

        // Register parameters
        RegisterParameter("cabinettype", (float)CabinetType.Cab4x12);
        RegisterParameter("speakertype", (float)SpeakerType.British);
        RegisterParameter("micposition", (float)MicPosition.OffCenter);
        RegisterParameter("roomsize", 0.3f);
        RegisterParameter("breakup", 0.2f);
        RegisterParameter("mix", 1.0f);
    }

    /// <summary>
    /// Cabinet size type affecting resonance and frequency response.
    /// </summary>
    public CabinetType CabinetType
    {
        get => _cabinetType;
        set => _cabinetType = value;
    }

    /// <summary>
    /// Speaker type affecting tonal character and breakup characteristics.
    /// </summary>
    public SpeakerType SpeakerType
    {
        get => _speakerType;
        set => _speakerType = value;
    }

    /// <summary>
    /// Microphone position simulation affecting high-frequency response.
    /// </summary>
    public MicPosition MicPosition
    {
        get => _micPosition;
        set => _micPosition = value;
    }

    /// <summary>
    /// Room size (0-1). Controls cabinet box resonance and ambience amount.
    /// </summary>
    public float RoomSize
    {
        get => _roomSize;
        set => _roomSize = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Speaker breakup amount (0-1). Simulates speaker cone distortion.
    /// </summary>
    public float Breakup
    {
        get => GetParameter("breakup");
        set => SetParameter("breakup", Math.Clamp(value, 0f, 1f));
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;
        int sampleRate = SampleRate;
        float breakup = Breakup;

        // Get EQ characteristics based on cabinet, speaker, and mic position
        var eqParams = GetEqParameters();

        // Calculate biquad coefficients for each filter band
        var lowShelfCoeffs = CalculateLowShelfCoeffs(eqParams.lowShelfFreq, eqParams.lowShelfGain, sampleRate);
        var lowMidCoeffs = CalculatePeakingEqCoeffs(eqParams.lowMidFreq, eqParams.lowMidGain, eqParams.lowMidQ, sampleRate);
        var highMidCoeffs = CalculatePeakingEqCoeffs(eqParams.highMidFreq, eqParams.highMidGain, eqParams.highMidQ, sampleRate);
        var highShelfCoeffs = CalculateHighShelfCoeffs(eqParams.highShelfFreq, eqParams.highShelfGain, sampleRate);
        var presenceCoeffs = CalculatePeakingEqCoeffs(eqParams.presenceFreq, eqParams.presenceGain, eqParams.presenceQ, sampleRate);
        var rolloffCoeffs = CalculateLowpassCoeffs(eqParams.rolloffFreq, 0.707f, sampleRate);

        // Get speaker resonance parameters
        var resonanceParams = GetSpeakerResonanceParams();

        // Get room delay time based on cabinet size
        int roomDelayTime = GetRoomDelayTime();

        for (int i = 0; i < count; i += channels)
        {
            for (int c = 0; c < channels; c++)
            {
                float sample = sourceBuffer[i + c];

                // Apply DC blocking
                float dcBlocked = sample - _dcBlockState[c];
                _dcBlockState[c] = sample * 0.995f + _dcBlockState[c] * 0.005f;
                sample = dcBlocked;

                // Apply speaker breakup (before EQ)
                if (breakup > 0.01f)
                {
                    sample = ApplySpeakerBreakup(sample, breakup, c);
                }

                // Apply speaker resonance modeling
                sample = ApplySpeakerResonance(sample, c, resonanceParams.frequency, resonanceParams.damping);

                // Apply EQ chain (cabinet and speaker voicing)
                sample = ApplyBiquad(sample, ref _lowShelfState[c], lowShelfCoeffs);
                sample = ApplyBiquad(sample, ref _lowMidState[c], lowMidCoeffs);
                sample = ApplyBiquad(sample, ref _highMidState[c], highMidCoeffs);
                sample = ApplyBiquad(sample, ref _presenceState[c], presenceCoeffs);
                sample = ApplyBiquad(sample, ref _highShelfState[c], highShelfCoeffs);

                // Apply speaker rolloff (high-frequency limit)
                sample = ApplyBiquad(sample, ref _speakerRolloffState[c], rolloffCoeffs);

                // Apply room simulation
                if (_roomSize > 0.01f)
                {
                    sample = ApplyRoomSimulation(sample, c, roomDelayTime, _roomSize * 0.4f);
                }

                destBuffer[offset + i + c] = sample;
            }
        }
    }

    private float ApplySpeakerBreakup(float sample, float amount, int channel)
    {
        // Speaker cone has limited excursion - non-linear behavior at high levels
        float drive = 1f + amount * 3f;
        sample *= drive;

        // Asymmetric soft-clipping to simulate speaker cone behavior
        // Positive excursion compresses more than negative (speaker cone asymmetry)
        if (sample > 0)
        {
            // Positive half - more compression
            float threshold = 0.7f - amount * 0.3f;
            if (sample > threshold)
            {
                float excess = sample - threshold;
                float knee = 1f - amount * 0.5f;
                sample = threshold + MathF.Tanh(excess * 2f) * knee;
            }
        }
        else
        {
            // Negative half - slightly less compression
            float threshold = -0.8f + amount * 0.2f;
            if (sample < threshold)
            {
                float excess = sample - threshold;
                float knee = 1f - amount * 0.3f;
                sample = threshold + MathF.Tanh(excess * 1.5f) * knee;
            }
        }

        // Low-pass the breakup signal slightly (speaker mass)
        float lpCoeff = 0.3f + amount * 0.3f;
        _breakupState[channel] = _breakupState[channel] * lpCoeff + sample * (1f - lpCoeff);
        sample = _breakupState[channel];

        return sample / drive;
    }

    private float ApplySpeakerResonance(float sample, int channel, float resonanceFreq, float damping)
    {
        // Simple 2-pole resonator for speaker cone resonance
        float w0 = 2f * MathF.PI * resonanceFreq / SampleRate;
        float r = 1f - damping;

        // State-variable resonator
        float s1 = _resonanceState1[channel];
        float s2 = _resonanceState2[channel];

        float cosW0 = MathF.Cos(w0);
        float sinW0 = MathF.Sin(w0);

        float newS1 = r * (cosW0 * s1 - sinW0 * s2) + (1f - r) * sample;
        float newS2 = r * (sinW0 * s1 + cosW0 * s2);

        _resonanceState1[channel] = newS1;
        _resonanceState2[channel] = newS2;

        // Mix resonance with dry signal
        return sample * 0.7f + newS1 * 0.3f;
    }

    private float ApplyRoomSimulation(float sample, int channel, int delayTime, float feedback)
    {
        // Simple comb filter for cabinet box resonance
        int delayPos = _roomDelayPos[channel];
        int readPos = (delayPos - delayTime + _roomDelayLength) % _roomDelayLength;

        float delayed = _roomDelayBuffer[channel][readPos];
        float output = sample + delayed * feedback;

        // Low-pass the feedback (room absorption)
        float lpOutput = output * 0.7f + delayed * 0.3f;
        _roomDelayBuffer[channel][delayPos] = lpOutput;

        _roomDelayPos[channel] = (delayPos + 1) % _roomDelayLength;

        return output;
    }

    private (float lowShelfFreq, float lowShelfGain, float lowMidFreq, float lowMidGain, float lowMidQ,
        float highMidFreq, float highMidGain, float highMidQ, float presenceFreq, float presenceGain,
        float presenceQ, float highShelfFreq, float highShelfGain, float rolloffFreq) GetEqParameters()
    {
        // Base cabinet characteristics
        var cabParams = _cabinetType switch
        {
            CabinetType.Cab1x12 => (lowFreq: 100f, lowGain: -2f, resonance: 120f),
            CabinetType.Cab2x12 => (lowFreq: 90f, lowGain: 0f, resonance: 100f),
            CabinetType.Cab4x10 => (lowFreq: 80f, lowGain: -1f, resonance: 90f),
            CabinetType.Cab4x12 => (lowFreq: 70f, lowGain: 2f, resonance: 80f),
            CabinetType.Cab8x10 => (lowFreq: 50f, lowGain: 4f, resonance: 60f),
            CabinetType.Cab1x15 => (lowFreq: 60f, lowGain: 3f, resonance: 70f),
            CabinetType.Cab2x15 => (lowFreq: 45f, lowGain: 5f, resonance: 55f),
            _ => (lowFreq: 70f, lowGain: 2f, resonance: 80f)
        };

        // Speaker type characteristics
        var speakerParams = _speakerType switch
        {
            SpeakerType.Vintage => (midFreq: 800f, midGain: 2f, highFreq: 3500f, highGain: -4f, rolloff: 5000f),
            SpeakerType.British => (midFreq: 1200f, midGain: 3f, highFreq: 3000f, highGain: -2f, rolloff: 6000f),
            SpeakerType.American => (midFreq: 2000f, midGain: 1f, highFreq: 4000f, highGain: 0f, rolloff: 7000f),
            SpeakerType.Modern => (midFreq: 2500f, midGain: 0f, highFreq: 5000f, highGain: 1f, rolloff: 8000f),
            SpeakerType.Bass => (midFreq: 500f, midGain: -2f, highFreq: 2500f, highGain: -6f, rolloff: 4000f),
            SpeakerType.Greenback => (midFreq: 1000f, midGain: 4f, highFreq: 2800f, highGain: -3f, rolloff: 5500f),
            _ => (midFreq: 1200f, midGain: 3f, highFreq: 3000f, highGain: -2f, rolloff: 6000f)
        };

        // Mic position affects high-frequency response
        var micParams = _micPosition switch
        {
            MicPosition.Center => (presenceFreq: 3500f, presenceGain: 4f, highCut: 1.0f),
            MicPosition.OffCenter => (presenceFreq: 3000f, presenceGain: 1f, highCut: 0.9f),
            MicPosition.Edge => (presenceFreq: 2500f, presenceGain: -2f, highCut: 0.7f),
            MicPosition.RoomBlend => (presenceFreq: 2000f, presenceGain: -3f, highCut: 0.6f),
            MicPosition.Mixed => (presenceFreq: 2800f, presenceGain: 0f, highCut: 0.85f),
            _ => (presenceFreq: 3000f, presenceGain: 1f, highCut: 0.9f)
        };

        float adjustedRolloff = speakerParams.rolloff * micParams.highCut;

        return (
            lowShelfFreq: cabParams.lowFreq,
            lowShelfGain: cabParams.lowGain,
            lowMidFreq: 400f,
            lowMidGain: -1f,
            lowMidQ: 1.5f,
            highMidFreq: speakerParams.midFreq,
            highMidGain: speakerParams.midGain,
            highMidQ: 1.2f,
            presenceFreq: micParams.presenceFreq,
            presenceGain: micParams.presenceGain,
            presenceQ: 2.0f,
            highShelfFreq: speakerParams.highFreq,
            highShelfGain: speakerParams.highGain,
            rolloffFreq: adjustedRolloff
        );
    }

    private (float frequency, float damping) GetSpeakerResonanceParams()
    {
        return _cabinetType switch
        {
            CabinetType.Cab1x12 => (frequency: 120f, damping: 0.15f),
            CabinetType.Cab2x12 => (frequency: 100f, damping: 0.12f),
            CabinetType.Cab4x10 => (frequency: 90f, damping: 0.10f),
            CabinetType.Cab4x12 => (frequency: 80f, damping: 0.08f),
            CabinetType.Cab8x10 => (frequency: 60f, damping: 0.06f),
            CabinetType.Cab1x15 => (frequency: 70f, damping: 0.10f),
            CabinetType.Cab2x15 => (frequency: 55f, damping: 0.08f),
            _ => (frequency: 80f, damping: 0.08f)
        };
    }

    private int GetRoomDelayTime()
    {
        // Larger cabinets have more internal reflections (longer delay)
        float delayMs = _cabinetType switch
        {
            CabinetType.Cab1x12 => 0.5f,
            CabinetType.Cab2x12 => 1.0f,
            CabinetType.Cab4x10 => 1.5f,
            CabinetType.Cab4x12 => 2.0f,
            CabinetType.Cab8x10 => 3.0f,
            CabinetType.Cab1x15 => 1.5f,
            CabinetType.Cab2x15 => 2.5f,
            _ => 2.0f
        };

        return (int)(SampleRate * delayMs / 1000f * (0.5f + _roomSize));
    }

    #region Biquad Filter Implementation

    private struct BiquadState
    {
        public float x1, x2; // Input history
        public float y1, y2; // Output history
    }

    private struct BiquadCoeffs
    {
        public float b0, b1, b2;
        public float a1, a2;
    }

    private static float ApplyBiquad(float input, ref BiquadState state, BiquadCoeffs coeffs)
    {
        float output = coeffs.b0 * input + coeffs.b1 * state.x1 + coeffs.b2 * state.x2
                     - coeffs.a1 * state.y1 - coeffs.a2 * state.y2;

        state.x2 = state.x1;
        state.x1 = input;
        state.y2 = state.y1;
        state.y1 = output;

        return output;
    }

    private static BiquadCoeffs CalculateLowShelfCoeffs(float frequency, float gainDb, int sampleRate)
    {
        float A = MathF.Pow(10f, gainDb / 40f);
        float w0 = 2f * MathF.PI * frequency / sampleRate;
        float cosW0 = MathF.Cos(w0);
        float sinW0 = MathF.Sin(w0);
        float alpha = sinW0 / 2f * MathF.Sqrt((A + 1f / A) * (1f / 0.707f - 1f) + 2f);
        float sqrtA2Alpha = 2f * MathF.Sqrt(A) * alpha;

        float a0 = (A + 1f) + (A - 1f) * cosW0 + sqrtA2Alpha;

        return new BiquadCoeffs
        {
            b0 = (A * ((A + 1f) - (A - 1f) * cosW0 + sqrtA2Alpha)) / a0,
            b1 = (2f * A * ((A - 1f) - (A + 1f) * cosW0)) / a0,
            b2 = (A * ((A + 1f) - (A - 1f) * cosW0 - sqrtA2Alpha)) / a0,
            a1 = (-2f * ((A - 1f) + (A + 1f) * cosW0)) / a0,
            a2 = ((A + 1f) + (A - 1f) * cosW0 - sqrtA2Alpha) / a0
        };
    }

    private static BiquadCoeffs CalculateHighShelfCoeffs(float frequency, float gainDb, int sampleRate)
    {
        float A = MathF.Pow(10f, gainDb / 40f);
        float w0 = 2f * MathF.PI * frequency / sampleRate;
        float cosW0 = MathF.Cos(w0);
        float sinW0 = MathF.Sin(w0);
        float alpha = sinW0 / 2f * MathF.Sqrt((A + 1f / A) * (1f / 0.707f - 1f) + 2f);
        float sqrtA2Alpha = 2f * MathF.Sqrt(A) * alpha;

        float a0 = (A + 1f) - (A - 1f) * cosW0 + sqrtA2Alpha;

        return new BiquadCoeffs
        {
            b0 = (A * ((A + 1f) + (A - 1f) * cosW0 + sqrtA2Alpha)) / a0,
            b1 = (-2f * A * ((A - 1f) + (A + 1f) * cosW0)) / a0,
            b2 = (A * ((A + 1f) + (A - 1f) * cosW0 - sqrtA2Alpha)) / a0,
            a1 = (2f * ((A - 1f) - (A + 1f) * cosW0)) / a0,
            a2 = ((A + 1f) - (A - 1f) * cosW0 - sqrtA2Alpha) / a0
        };
    }

    private static BiquadCoeffs CalculatePeakingEqCoeffs(float frequency, float gainDb, float q, int sampleRate)
    {
        float A = MathF.Pow(10f, gainDb / 40f);
        float w0 = 2f * MathF.PI * frequency / sampleRate;
        float cosW0 = MathF.Cos(w0);
        float sinW0 = MathF.Sin(w0);
        float alpha = sinW0 / (2f * q);

        float a0 = 1f + alpha / A;

        return new BiquadCoeffs
        {
            b0 = (1f + alpha * A) / a0,
            b1 = (-2f * cosW0) / a0,
            b2 = (1f - alpha * A) / a0,
            a1 = (-2f * cosW0) / a0,
            a2 = (1f - alpha / A) / a0
        };
    }

    private static BiquadCoeffs CalculateLowpassCoeffs(float frequency, float q, int sampleRate)
    {
        float w0 = 2f * MathF.PI * frequency / sampleRate;
        float cosW0 = MathF.Cos(w0);
        float sinW0 = MathF.Sin(w0);
        float alpha = sinW0 / (2f * q);

        float a0 = 1f + alpha;

        return new BiquadCoeffs
        {
            b0 = ((1f - cosW0) / 2f) / a0,
            b1 = (1f - cosW0) / a0,
            b2 = ((1f - cosW0) / 2f) / a0,
            a1 = (-2f * cosW0) / a0,
            a2 = (1f - alpha) / a0
        };
    }

    #endregion

    #region Presets

    /// <summary>
    /// Creates a classic British 4x12 cabinet preset.
    /// </summary>
    public static CabinetSimulator CreateBritish4x12(ISampleProvider source)
    {
        var cab = new CabinetSimulator(source)
        {
            CabinetType = CabinetType.Cab4x12,
            SpeakerType = SpeakerType.British,
            MicPosition = MicPosition.OffCenter,
            RoomSize = 0.3f,
            Breakup = 0.2f,
            Mix = 1.0f
        };
        return cab;
    }

    /// <summary>
    /// Creates a vintage American 2x12 cabinet preset.
    /// </summary>
    public static CabinetSimulator CreateVintageAmerican2x12(ISampleProvider source)
    {
        var cab = new CabinetSimulator(source)
        {
            CabinetType = CabinetType.Cab2x12,
            SpeakerType = SpeakerType.American,
            MicPosition = MicPosition.Center,
            RoomSize = 0.2f,
            Breakup = 0.15f,
            Mix = 1.0f
        };
        return cab;
    }

    /// <summary>
    /// Creates a tight studio 1x12 cabinet preset.
    /// </summary>
    public static CabinetSimulator CreateStudio1x12(ISampleProvider source)
    {
        var cab = new CabinetSimulator(source)
        {
            CabinetType = CabinetType.Cab1x12,
            SpeakerType = SpeakerType.Modern,
            MicPosition = MicPosition.OffCenter,
            RoomSize = 0.1f,
            Breakup = 0.1f,
            Mix = 1.0f
        };
        return cab;
    }

    /// <summary>
    /// Creates a classic bass 8x10 cabinet preset.
    /// </summary>
    public static CabinetSimulator CreateBass8x10(ISampleProvider source)
    {
        var cab = new CabinetSimulator(source)
        {
            CabinetType = CabinetType.Cab8x10,
            SpeakerType = SpeakerType.Bass,
            MicPosition = MicPosition.OffCenter,
            RoomSize = 0.4f,
            Breakup = 0.05f,
            Mix = 1.0f
        };
        return cab;
    }

    /// <summary>
    /// Creates a vintage 1x15 bass cabinet preset.
    /// </summary>
    public static CabinetSimulator CreateVintageBass1x15(ISampleProvider source)
    {
        var cab = new CabinetSimulator(source)
        {
            CabinetType = CabinetType.Cab1x15,
            SpeakerType = SpeakerType.Vintage,
            MicPosition = MicPosition.Edge,
            RoomSize = 0.35f,
            Breakup = 0.1f,
            Mix = 1.0f
        };
        return cab;
    }

    /// <summary>
    /// Creates a Greenback 4x12 cabinet preset for classic rock tones.
    /// </summary>
    public static CabinetSimulator CreateGreenback4x12(ISampleProvider source)
    {
        var cab = new CabinetSimulator(source)
        {
            CabinetType = CabinetType.Cab4x12,
            SpeakerType = SpeakerType.Greenback,
            MicPosition = MicPosition.OffCenter,
            RoomSize = 0.25f,
            Breakup = 0.25f,
            Mix = 1.0f
        };
        return cab;
    }

    /// <summary>
    /// Creates a modern high-gain 4x12 cabinet preset.
    /// </summary>
    public static CabinetSimulator CreateModernHighGain(ISampleProvider source)
    {
        var cab = new CabinetSimulator(source)
        {
            CabinetType = CabinetType.Cab4x12,
            SpeakerType = SpeakerType.Modern,
            MicPosition = MicPosition.Center,
            RoomSize = 0.15f,
            Breakup = 0.3f,
            Mix = 1.0f
        };
        return cab;
    }

    #endregion

    protected override void OnParameterChanged(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "cabinettype":
                _cabinetType = (CabinetType)(int)value;
                break;
            case "speakertype":
                _speakerType = (SpeakerType)(int)value;
                break;
            case "micposition":
                _micPosition = (MicPosition)(int)value;
                break;
            case "roomsize":
                _roomSize = value;
                break;
        }
    }
}
