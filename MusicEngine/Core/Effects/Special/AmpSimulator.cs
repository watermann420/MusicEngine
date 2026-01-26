//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Guitar/bass amplifier simulator with tube-style saturation, preamp and power amp stages.

using System;
using NAudio.Wave;

namespace MusicEngine.Core.Effects.Special;

/// <summary>
/// Amp simulation model type for the AmpSimulator.
/// </summary>
public enum AmpSimulatorType
{
    /// <summary>
    /// Clean amp with minimal distortion - ideal for jazz and clean tones.
    /// </summary>
    Clean,

    /// <summary>
    /// Edge of breakup - warm overdrive when pushed.
    /// </summary>
    Crunch,

    /// <summary>
    /// High gain - aggressive distortion for rock and metal.
    /// </summary>
    HighGain,

    /// <summary>
    /// Vintage tube amp character - warm, dynamic response.
    /// </summary>
    Vintage,

    /// <summary>
    /// Modern high-gain with tight low end.
    /// </summary>
    Modern,

    /// <summary>
    /// Bass amp simulation - optimized for bass guitar.
    /// </summary>
    Bass
}

/// <summary>
/// Guitar/bass amplifier simulator with tube-style saturation and compression.
/// </summary>
/// <remarks>
/// Features:
/// - Multiple amp types (Clean, Crunch, High Gain, Vintage, Modern, Bass)
/// - Tube-style asymmetric saturation
/// - Preamp stage with gain control
/// - Power amp stage with sag simulation
/// - 3-band tone stack (Bass, Mid, Treble)
/// - Presence control for high-frequency emphasis
/// - Master volume control
/// </remarks>
public class AmpSimulator : EffectBase
{
    // Filter states per channel
    private readonly float[] _inputHighpassState;
    private readonly float[] _preampLowpassState;
    private readonly float[] _bassFilterState;
    private readonly float[] _midFilterState1;
    private readonly float[] _midFilterState2;
    private readonly float[] _trebleFilterState;
    private readonly float[] _presenceFilterState;
    private readonly float[] _dcBlockState;

    // Power amp sag state
    private readonly float[] _sagEnvelope;

    // Tube bias state (for asymmetric clipping)
    private readonly float[] _tubeBias;

    // Parameters
    private AmpSimulatorType _ampType = AmpSimulatorType.Crunch;
    private float _gain = 0.5f;
    private float _bass = 0.5f;
    private float _mid = 0.5f;
    private float _treble = 0.5f;
    private float _presence = 0.5f;
    private float _master = 0.5f;
    private float _sag = 0.3f;
    private bool _brightSwitch;
    private bool _tightSwitch;

    /// <summary>
    /// Amp type/model selection.
    /// </summary>
    public AmpSimulatorType AmpType
    {
        get => _ampType;
        set => _ampType = value;
    }

    /// <summary>
    /// Preamp gain (0-1). Controls the amount of saturation.
    /// </summary>
    public float Gain
    {
        get => _gain;
        set => _gain = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Bass control (0-1). Boost/cut around 80-150Hz.
    /// </summary>
    public float Bass
    {
        get => _bass;
        set => _bass = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Mid control (0-1). Boost/cut around 400-800Hz.
    /// </summary>
    public float Mid
    {
        get => _mid;
        set => _mid = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Treble control (0-1). Boost/cut around 2-4kHz.
    /// </summary>
    public float Treble
    {
        get => _treble;
        set => _treble = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Presence control (0-1). High-frequency emphasis after power amp.
    /// </summary>
    public float Presence
    {
        get => _presence;
        set => _presence = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Master volume (0-1). Controls output level and power amp saturation.
    /// </summary>
    public float Master
    {
        get => _master;
        set => _master = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Power amp sag amount (0-1). Simulates power supply compression.
    /// </summary>
    public float Sag
    {
        get => _sag;
        set => _sag = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Bright switch - adds high frequency boost before the preamp.
    /// </summary>
    public bool BrightSwitch
    {
        get => _brightSwitch;
        set => _brightSwitch = value;
    }

    /// <summary>
    /// Tight switch - adds low frequency cut for tighter bass response.
    /// </summary>
    public bool TightSwitch
    {
        get => _tightSwitch;
        set => _tightSwitch = value;
    }

    /// <summary>
    /// Creates a new amp simulator effect.
    /// </summary>
    /// <param name="source">The audio source to process.</param>
    public AmpSimulator(ISampleProvider source) : base(source, "Amp Simulator")
    {
        int channels = Channels;

        // Initialize filter states
        _inputHighpassState = new float[channels];
        _preampLowpassState = new float[channels];
        _bassFilterState = new float[channels];
        _midFilterState1 = new float[channels];
        _midFilterState2 = new float[channels];
        _trebleFilterState = new float[channels];
        _presenceFilterState = new float[channels];
        _dcBlockState = new float[channels];
        _sagEnvelope = new float[channels];
        _tubeBias = new float[channels];

        // Initialize tube bias per channel
        for (int i = 0; i < channels; i++)
        {
            _tubeBias[i] = 0.02f; // Small positive bias for asymmetric clipping
        }

        // Register parameters
        RegisterParameter("gain", 0.5f);
        RegisterParameter("bass", 0.5f);
        RegisterParameter("mid", 0.5f);
        RegisterParameter("treble", 0.5f);
        RegisterParameter("presence", 0.5f);
        RegisterParameter("master", 0.5f);
        RegisterParameter("sag", 0.3f);
        RegisterParameter("type", (float)AmpSimulatorType.Crunch);
        RegisterParameter("bright", 0f);
        RegisterParameter("tight", 0f);
    }

    /// <summary>
    /// Processes the audio buffer through the amp simulation.
    /// </summary>
    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;
        int sampleRate = SampleRate;

        // Get amp characteristics based on type
        var ampChar = GetAmpCharacteristics();

        // Calculate filter coefficients
        float inputHpFreq = _tightSwitch ? 120f : 60f;
        float inputHpCoeff = CalculateHighpassCoeff(inputHpFreq, sampleRate);

        float brightFreq = 3000f;
        float brightCoeff = CalculateLowpassCoeff(brightFreq, sampleRate);

        // Sag envelope coefficients
        float sagAttack = MathF.Exp(-1f / (0.002f * sampleRate));  // 2ms attack
        float sagRelease = MathF.Exp(-1f / (0.1f * sampleRate));   // 100ms release

        for (int i = 0; i < count; i += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                float sample = sourceBuffer[i + ch];

                // Input stage: high-pass filter to remove DC and subsonic frequencies
                float hp = sample - _inputHighpassState[ch];
                _inputHighpassState[ch] += inputHpCoeff * hp;
                sample = hp;

                // Bright switch: boost high frequencies before preamp
                if (_brightSwitch)
                {
                    float brightLp = _preampLowpassState[ch];
                    _preampLowpassState[ch] = brightLp + brightCoeff * (sample - brightLp);
                    float highFreq = sample - _preampLowpassState[ch];
                    sample += highFreq * 0.5f;
                }

                // Preamp stage
                sample = ProcessPreampStage(sample, ch, ampChar);

                // Tone stack
                sample = ProcessToneStack(sample, ch, sampleRate, ampChar);

                // Power amp stage with sag
                sample = ProcessPowerAmpStage(sample, ch, sagAttack, sagRelease, ampChar);

                // Presence control (post power amp high shelf)
                sample = ProcessPresence(sample, ch, sampleRate);

                // DC blocking
                float dc = _dcBlockState[ch];
                _dcBlockState[ch] = dc * 0.995f + sample * 0.005f;
                sample -= _dcBlockState[ch];

                // Master volume
                sample *= _master * 2f;

                destBuffer[offset + i + ch] = sample;
            }
        }
    }

    private float ProcessPreampStage(float sample, int channel, AmpCharacteristics amp)
    {
        // Calculate effective preamp gain
        float preampGain = amp.PreampGainBase + _gain * amp.PreampGainRange;

        // Apply gain
        sample *= preampGain;

        // Add tube bias for asymmetric clipping
        sample += _tubeBias[channel] * amp.BiasAmount;

        // First tube stage saturation
        sample = TubeSaturate(sample, amp.FirstStageHardness);

        // Second tube stage (cascaded gain stages for high gain amps)
        if (amp.CascadedStages > 1)
        {
            sample *= 1.5f;
            sample = TubeSaturate(sample, amp.FirstStageHardness * 0.8f);
        }

        // Third tube stage for high gain amps
        if (amp.CascadedStages > 2)
        {
            sample *= 1.3f;
            sample = TubeSaturate(sample, amp.FirstStageHardness * 0.6f);
        }

        // Remove DC offset from bias
        sample -= _tubeBias[channel] * amp.BiasAmount * 0.3f;

        return sample;
    }

    private float ProcessToneStack(float sample, int channel, int sampleRate, AmpCharacteristics amp)
    {
        // Calculate EQ gains from knob positions
        float bassGain = (_bass - 0.5f) * 2f * amp.BassRange;   // +/- range in dB
        float midGain = (_mid - 0.5f) * 2f * amp.MidRange;
        float trebleGain = (_treble - 0.5f) * 2f * amp.TrebleRange;

        // Convert dB to linear
        float bassLinear = MathF.Pow(10f, bassGain / 20f);
        float midLinear = MathF.Pow(10f, midGain / 20f);
        float trebleLinear = MathF.Pow(10f, trebleGain / 20f);

        // Bass shelf (low shelf around 100Hz)
        float bassFreq = amp.BassFreq;
        float bassCoeff = CalculateLowpassCoeff(bassFreq, sampleRate);
        float bassLp = _bassFilterState[channel];
        _bassFilterState[channel] = bassLp + bassCoeff * (sample - bassLp);
        float bassContent = _bassFilterState[channel];
        float highContent = sample - bassContent;
        sample = bassContent * bassLinear + highContent;

        // Mid bandpass (around 400-800Hz)
        float midFreq = amp.MidFreq;
        float midLowCoeff = CalculateLowpassCoeff(midFreq * 0.5f, sampleRate);
        float midHighCoeff = CalculateLowpassCoeff(midFreq * 2f, sampleRate);

        _midFilterState1[channel] = _midFilterState1[channel] + midLowCoeff * (sample - _midFilterState1[channel]);
        _midFilterState2[channel] = _midFilterState2[channel] + midHighCoeff * (sample - _midFilterState2[channel]);

        float midBand = _midFilterState2[channel] - _midFilterState1[channel];
        sample += midBand * (midLinear - 1f);

        // Treble shelf (high shelf around 2-3kHz)
        float trebleFreq = amp.TrebleFreq;
        float trebleCoeff = CalculateLowpassCoeff(trebleFreq, sampleRate);
        float trebleLp = _trebleFilterState[channel];
        _trebleFilterState[channel] = trebleLp + trebleCoeff * (sample - trebleLp);
        float trebleContent = sample - _trebleFilterState[channel];
        sample = _trebleFilterState[channel] + trebleContent * trebleLinear;

        return sample;
    }

    private float ProcessPowerAmpStage(float sample, int channel, float sagAttack, float sagRelease, AmpCharacteristics amp)
    {
        // Power amp input level
        float powerAmpLevel = sample * amp.PowerAmpGain;

        // Calculate sag envelope (power supply compression)
        float rectified = MathF.Abs(powerAmpLevel);
        float envelope = _sagEnvelope[channel];

        if (rectified > envelope)
        {
            envelope = envelope + (1f - sagAttack) * (rectified - envelope);
        }
        else
        {
            envelope = envelope + (1f - sagRelease) * (rectified - envelope);
        }
        _sagEnvelope[channel] = envelope;

        // Apply sag (reduces power amp gain as signal increases)
        float sagAmount = 1f - _sag * 0.5f * envelope;
        sagAmount = MathF.Max(0.5f, sagAmount); // Don't reduce too much
        powerAmpLevel *= sagAmount;

        // Power tube saturation (softer than preamp)
        sample = TubeSaturate(powerAmpLevel, amp.PowerStageHardness);

        // Push-pull crossover distortion simulation for vintage amps
        if (amp.CrossoverDistortion > 0f)
        {
            float crossover = amp.CrossoverDistortion * 0.05f;
            if (MathF.Abs(sample) < crossover)
            {
                sample *= 0.7f; // Reduce gain near zero crossing
            }
        }

        return sample;
    }

    private float ProcessPresence(float sample, int channel, int sampleRate)
    {
        // Presence: high shelf around 4-5kHz
        float presFreq = 4500f;
        float presCoeff = CalculateLowpassCoeff(presFreq, sampleRate);

        float presLp = _presenceFilterState[channel];
        _presenceFilterState[channel] = presLp + presCoeff * (sample - presLp);
        float highContent = sample - _presenceFilterState[channel];

        // Presence range: -6dB to +6dB
        float presGain = (_presence - 0.5f) * 2f * 6f;
        float presLinear = MathF.Pow(10f, presGain / 20f);

        return _presenceFilterState[channel] + highContent * presLinear;
    }

    /// <summary>
    /// Tube-style saturation with asymmetric soft clipping.
    /// </summary>
    private static float TubeSaturate(float sample, float hardness)
    {
        // Hardness controls the transition from soft to hard clipping
        // Lower values = softer, more tube-like
        // Higher values = harder, more aggressive

        float drive = 1f + hardness * 2f;

        if (sample > 0f)
        {
            // Positive half: soft knee compression
            sample = 1f - MathF.Exp(-sample * drive);
        }
        else
        {
            // Negative half: slightly different curve for asymmetry
            sample = -(1f - MathF.Exp(sample * drive * 0.9f));
        }

        return sample;
    }

    private static float CalculateLowpassCoeff(float frequency, int sampleRate)
    {
        float omega = 2f * MathF.PI * frequency / sampleRate;
        return 1f - MathF.Exp(-omega);
    }

    private static float CalculateHighpassCoeff(float frequency, int sampleRate)
    {
        float omega = 2f * MathF.PI * frequency / sampleRate;
        return MathF.Exp(-omega);
    }

    private AmpCharacteristics GetAmpCharacteristics()
    {
        return _ampType switch
        {
            AmpSimulatorType.Clean => new AmpCharacteristics
            {
                PreampGainBase = 1.0f,
                PreampGainRange = 2.0f,
                FirstStageHardness = 0.15f,
                CascadedStages = 1,
                BiasAmount = 0.5f,
                PowerAmpGain = 1.0f,
                PowerStageHardness = 0.1f,
                CrossoverDistortion = 0f,
                BassFreq = 100f,
                MidFreq = 600f,
                TrebleFreq = 2500f,
                BassRange = 10f,
                MidRange = 8f,
                TrebleRange = 10f
            },
            AmpSimulatorType.Crunch => new AmpCharacteristics
            {
                PreampGainBase = 1.5f,
                PreampGainRange = 4.0f,
                FirstStageHardness = 0.3f,
                CascadedStages = 2,
                BiasAmount = 1.0f,
                PowerAmpGain = 1.2f,
                PowerStageHardness = 0.2f,
                CrossoverDistortion = 0.2f,
                BassFreq = 120f,
                MidFreq = 650f,
                TrebleFreq = 2800f,
                BassRange = 12f,
                MidRange = 10f,
                TrebleRange = 12f
            },
            AmpSimulatorType.HighGain => new AmpCharacteristics
            {
                PreampGainBase = 2.0f,
                PreampGainRange = 8.0f,
                FirstStageHardness = 0.5f,
                CascadedStages = 3,
                BiasAmount = 1.5f,
                PowerAmpGain = 1.5f,
                PowerStageHardness = 0.3f,
                CrossoverDistortion = 0f,
                BassFreq = 80f,
                MidFreq = 500f,
                TrebleFreq = 3200f,
                BassRange = 15f,
                MidRange = 12f,
                TrebleRange = 14f
            },
            AmpSimulatorType.Vintage => new AmpCharacteristics
            {
                PreampGainBase = 1.2f,
                PreampGainRange = 3.0f,
                FirstStageHardness = 0.25f,
                CascadedStages = 2,
                BiasAmount = 0.8f,
                PowerAmpGain = 1.3f,
                PowerStageHardness = 0.25f,
                CrossoverDistortion = 0.5f,
                BassFreq = 110f,
                MidFreq = 700f,
                TrebleFreq = 2200f,
                BassRange = 10f,
                MidRange = 8f,
                TrebleRange = 10f
            },
            AmpSimulatorType.Modern => new AmpCharacteristics
            {
                PreampGainBase = 2.5f,
                PreampGainRange = 10.0f,
                FirstStageHardness = 0.6f,
                CascadedStages = 3,
                BiasAmount = 1.2f,
                PowerAmpGain = 1.4f,
                PowerStageHardness = 0.2f,
                CrossoverDistortion = 0f,
                BassFreq = 70f,
                MidFreq = 450f,
                TrebleFreq = 3500f,
                BassRange = 15f,
                MidRange = 15f,
                TrebleRange = 15f
            },
            AmpSimulatorType.Bass => new AmpCharacteristics
            {
                PreampGainBase = 1.3f,
                PreampGainRange = 3.5f,
                FirstStageHardness = 0.2f,
                CascadedStages = 2,
                BiasAmount = 0.6f,
                PowerAmpGain = 1.1f,
                PowerStageHardness = 0.15f,
                CrossoverDistortion = 0.1f,
                BassFreq = 60f,
                MidFreq = 400f,
                TrebleFreq = 2000f,
                BassRange = 15f,
                MidRange = 10f,
                TrebleRange = 8f
            },
            _ => new AmpCharacteristics
            {
                PreampGainBase = 1.5f,
                PreampGainRange = 4.0f,
                FirstStageHardness = 0.3f,
                CascadedStages = 2,
                BiasAmount = 1.0f,
                PowerAmpGain = 1.2f,
                PowerStageHardness = 0.2f,
                CrossoverDistortion = 0.2f,
                BassFreq = 120f,
                MidFreq = 650f,
                TrebleFreq = 2800f,
                BassRange = 12f,
                MidRange = 10f,
                TrebleRange = 12f
            }
        };
    }

    /// <inheritdoc />
    protected override void OnParameterChanged(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "gain": Gain = value; break;
            case "bass": Bass = value; break;
            case "mid": Mid = value; break;
            case "treble": Treble = value; break;
            case "presence": Presence = value; break;
            case "master": Master = value; break;
            case "sag": Sag = value; break;
            case "type": AmpType = (AmpSimulatorType)(int)value; break;
            case "bright": BrightSwitch = value > 0.5f; break;
            case "tight": TightSwitch = value > 0.5f; break;
        }
    }

    #region Presets

    /// <summary>
    /// Creates a clean amp preset suitable for jazz and clean tones.
    /// </summary>
    public static AmpSimulator CreateCleanPreset(ISampleProvider source)
    {
        var amp = new AmpSimulator(source)
        {
            AmpType = AmpSimulatorType.Clean,
            Gain = 0.3f,
            Bass = 0.5f,
            Mid = 0.6f,
            Treble = 0.5f,
            Presence = 0.4f,
            Master = 0.6f,
            Sag = 0.1f,
            BrightSwitch = false,
            TightSwitch = false
        };
        amp.Mix = 1.0f;
        return amp;
    }

    /// <summary>
    /// Creates a crunch preset for blues and rock rhythm.
    /// </summary>
    public static AmpSimulator CreateCrunchPreset(ISampleProvider source)
    {
        var amp = new AmpSimulator(source)
        {
            AmpType = AmpSimulatorType.Crunch,
            Gain = 0.5f,
            Bass = 0.5f,
            Mid = 0.55f,
            Treble = 0.55f,
            Presence = 0.5f,
            Master = 0.5f,
            Sag = 0.3f,
            BrightSwitch = false,
            TightSwitch = false
        };
        amp.Mix = 1.0f;
        return amp;
    }

    /// <summary>
    /// Creates a high gain preset for heavy rock and metal.
    /// </summary>
    public static AmpSimulator CreateHighGainPreset(ISampleProvider source)
    {
        var amp = new AmpSimulator(source)
        {
            AmpType = AmpSimulatorType.HighGain,
            Gain = 0.7f,
            Bass = 0.45f,
            Mid = 0.4f,
            Treble = 0.6f,
            Presence = 0.55f,
            Master = 0.45f,
            Sag = 0.2f,
            BrightSwitch = false,
            TightSwitch = true
        };
        amp.Mix = 1.0f;
        return amp;
    }

    /// <summary>
    /// Creates a vintage preset for classic rock tones.
    /// </summary>
    public static AmpSimulator CreateVintagePreset(ISampleProvider source)
    {
        var amp = new AmpSimulator(source)
        {
            AmpType = AmpSimulatorType.Vintage,
            Gain = 0.55f,
            Bass = 0.55f,
            Mid = 0.6f,
            Treble = 0.5f,
            Presence = 0.45f,
            Master = 0.55f,
            Sag = 0.4f,
            BrightSwitch = true,
            TightSwitch = false
        };
        amp.Mix = 1.0f;
        return amp;
    }

    /// <summary>
    /// Creates a modern high-gain preset for djent and progressive metal.
    /// </summary>
    public static AmpSimulator CreateModernPreset(ISampleProvider source)
    {
        var amp = new AmpSimulator(source)
        {
            AmpType = AmpSimulatorType.Modern,
            Gain = 0.75f,
            Bass = 0.4f,
            Mid = 0.35f,
            Treble = 0.65f,
            Presence = 0.6f,
            Master = 0.4f,
            Sag = 0.1f,
            BrightSwitch = false,
            TightSwitch = true
        };
        amp.Mix = 1.0f;
        return amp;
    }

    /// <summary>
    /// Creates a bass amp preset.
    /// </summary>
    public static AmpSimulator CreateBassPreset(ISampleProvider source)
    {
        var amp = new AmpSimulator(source)
        {
            AmpType = AmpSimulatorType.Bass,
            Gain = 0.4f,
            Bass = 0.65f,
            Mid = 0.5f,
            Treble = 0.45f,
            Presence = 0.35f,
            Master = 0.6f,
            Sag = 0.25f,
            BrightSwitch = false,
            TightSwitch = false
        };
        amp.Mix = 1.0f;
        return amp;
    }

    #endregion

    /// <summary>
    /// Internal structure holding amp model characteristics.
    /// </summary>
    private struct AmpCharacteristics
    {
        public float PreampGainBase;
        public float PreampGainRange;
        public float FirstStageHardness;
        public int CascadedStages;
        public float BiasAmount;
        public float PowerAmpGain;
        public float PowerStageHardness;
        public float CrossoverDistortion;
        public float BassFreq;
        public float MidFreq;
        public float TrebleFreq;
        public float BassRange;
        public float MidRange;
        public float TrebleRange;
    }
}
