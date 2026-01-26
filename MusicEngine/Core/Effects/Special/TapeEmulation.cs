//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Comprehensive analog tape machine emulation with authentic tape characteristics.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Special;

/// <summary>
/// Tape speed settings in inches per second (IPS).
/// Different speeds affect frequency response and noise characteristics.
/// </summary>
public enum TapeSpeedIPS
{
    /// <summary>
    /// 7.5 IPS - Consumer/prosumer tape speed. More warmth, earlier HF rolloff, more hiss.
    /// </summary>
    IPS_7_5 = 0,

    /// <summary>
    /// 15 IPS - Professional tape speed. Balanced frequency response.
    /// </summary>
    IPS_15 = 1,

    /// <summary>
    /// 30 IPS - Mastering tape speed. Extended frequency response, lowest noise.
    /// </summary>
    IPS_30 = 2
}

/// <summary>
/// Tape machine type presets affecting overall character.
/// </summary>
public enum TapeMachineType
{
    /// <summary>
    /// Clean, transparent tape character. Minimal coloration.
    /// </summary>
    Modern,

    /// <summary>
    /// Classic American tape sound. Punchy low end, smooth highs.
    /// </summary>
    American,

    /// <summary>
    /// European tape character. Open, detailed sound.
    /// </summary>
    European,

    /// <summary>
    /// Vintage cassette deck. Lo-fi character with pronounced artifacts.
    /// </summary>
    Cassette
}

/// <summary>
/// Comprehensive analog tape machine emulation effect.
/// Models tape saturation, wow/flutter, hiss, head bump, and high-frequency rolloff.
/// </summary>
/// <remarks>
/// This effect emulates the following tape characteristics:
/// - Magnetic saturation with hysteresis modeling
/// - Wow (slow pitch variation) and flutter (fast pitch variation)
/// - Tape hiss with frequency-shaped noise
/// - Head bump (low frequency resonance around 60-100Hz)
/// - High-frequency rolloff dependent on tape speed
/// - Tape bias affecting distortion character
/// - Natural tape compression
/// </remarks>
public class TapeEmulation : EffectBase
{
    // Random for noise generation
    private readonly Random _random = new();

    // Tape speed settings
    private TapeSpeedIPS _tapeSpeed = TapeSpeedIPS.IPS_15;
    private TapeMachineType _machineType = TapeMachineType.American;

    // Filter states per channel
    private float _hfRolloffStateL, _hfRolloffStateR;
    private float _headBumpStateL, _headBumpStateR;
    private float _headBumpState2L, _headBumpState2R;
    private float _dcBlockStateL, _dcBlockStateR;
    private float _hissLpStateL, _hissLpStateR;
    private float _hissHpStateL, _hissHpStateR;
    private float _inputHpStateL, _inputHpStateR;

    // Wow and flutter oscillators
    private double _wowPhase;
    private double _flutterPhase1;
    private double _flutterPhase2;
    private double _flutterPhase3;

    // Compression envelope followers
    private float _envL, _envR;

    // Delay line for wow/flutter pitch modulation
    private float[] _delayBufferL = Array.Empty<float>();
    private float[] _delayBufferR = Array.Empty<float>();
    private int _delayWritePos;
    private const int MaxDelayMs = 50;
    private int _maxDelaySamples;

    // Hysteresis state for saturation
    private float _hysteresisStateL, _hysteresisStateR;

    // Noise generation state (for pink noise)
    private float _pinkB0L, _pinkB1L, _pinkB2L;
    private float _pinkB0R, _pinkB1R, _pinkB2R;

    // Configuration cached from tape speed
    private float _hfCutoff;
    private float _headBumpFreq;
    private float _noiseLevel;
    private float _saturationKnee;

    private bool _configDirty = true;

    /// <summary>
    /// Creates a new tape emulation effect.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    public TapeEmulation(ISampleProvider source) : this(source, "Tape Emulation")
    {
    }

    /// <summary>
    /// Creates a new tape emulation effect with a custom name.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <param name="name">Effect name.</param>
    public TapeEmulation(ISampleProvider source, string name) : base(source, name)
    {
        RegisterParameter("Saturation", 0.5f);
        RegisterParameter("Bias", 0.0f);
        RegisterParameter("WowAmount", 0.0f);
        RegisterParameter("FlutterAmount", 0.0f);
        RegisterParameter("Hiss", 0.0f);
        RegisterParameter("HeadBump", 0.5f);
        RegisterParameter("HFRolloff", 0.5f);
        RegisterParameter("Compression", 0.3f);
        RegisterParameter("OutputLevel", 1.0f);
        RegisterParameter("Mix", 1.0f);

        InitializeDelayBuffers();
    }

    #region Properties

    /// <summary>
    /// Gets or sets the tape speed (7.5, 15, or 30 IPS).
    /// Affects frequency response and noise characteristics.
    /// </summary>
    public TapeSpeedIPS TapeSpeed
    {
        get => _tapeSpeed;
        set
        {
            if (_tapeSpeed != value)
            {
                _tapeSpeed = value;
                _configDirty = true;
            }
        }
    }

    /// <summary>
    /// Gets or sets the tape machine type preset.
    /// </summary>
    public TapeMachineType MachineType
    {
        get => _machineType;
        set
        {
            if (_machineType != value)
            {
                _machineType = value;
                _configDirty = true;
            }
        }
    }

    /// <summary>
    /// Gets or sets the saturation amount (0.0 - 1.0).
    /// Controls tape drive and harmonic distortion.
    /// </summary>
    public float Saturation
    {
        get => GetParameter("Saturation");
        set => SetParameter("Saturation", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Gets or sets the tape bias (-1.0 to 1.0).
    /// Affects the distortion character and asymmetry.
    /// </summary>
    public float Bias
    {
        get => GetParameter("Bias");
        set => SetParameter("Bias", Math.Clamp(value, -1f, 1f));
    }

    /// <summary>
    /// Gets or sets the wow amount (0.0 - 1.0).
    /// Slow, periodic pitch variation from tape transport.
    /// </summary>
    public float WowAmount
    {
        get => GetParameter("WowAmount");
        set => SetParameter("WowAmount", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Gets or sets the flutter amount (0.0 - 1.0).
    /// Fast, subtle pitch variation from capstan and motor.
    /// </summary>
    public float FlutterAmount
    {
        get => GetParameter("FlutterAmount");
        set => SetParameter("FlutterAmount", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Gets or sets the tape hiss amount (0.0 - 1.0).
    /// Adds authentic tape noise with frequency shaping.
    /// </summary>
    public float Hiss
    {
        get => GetParameter("Hiss");
        set => SetParameter("Hiss", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Gets or sets the head bump amount (0.0 - 1.0).
    /// Low frequency resonance from tape head gap.
    /// </summary>
    public float HeadBump
    {
        get => GetParameter("HeadBump");
        set => SetParameter("HeadBump", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Gets or sets the high-frequency rolloff amount (0.0 - 1.0).
    /// Controls the tape's natural HF loss.
    /// </summary>
    public float HFRolloff
    {
        get => GetParameter("HFRolloff");
        set => SetParameter("HFRolloff", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Gets or sets the tape compression amount (0.0 - 1.0).
    /// Natural dynamic range compression from tape saturation.
    /// </summary>
    public float Compression
    {
        get => GetParameter("Compression");
        set => SetParameter("Compression", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Gets or sets the output level (0.0 - 2.0).
    /// </summary>
    public float OutputLevel
    {
        get => GetParameter("OutputLevel");
        set => SetParameter("OutputLevel", Math.Clamp(value, 0f, 2f));
    }

    #endregion

    /// <summary>
    /// Initializes delay buffers for wow/flutter modulation.
    /// </summary>
    private void InitializeDelayBuffers()
    {
        _maxDelaySamples = (int)(MaxDelayMs * SampleRate / 1000.0);
        _delayBufferL = new float[_maxDelaySamples];
        _delayBufferR = new float[_maxDelaySamples];
        _delayWritePos = 0;
    }

    /// <summary>
    /// Updates configuration based on tape speed and machine type.
    /// </summary>
    private void UpdateConfiguration()
    {
        if (!_configDirty) return;

        // Set parameters based on tape speed
        switch (_tapeSpeed)
        {
            case TapeSpeedIPS.IPS_7_5:
                _hfCutoff = 12000f;
                _headBumpFreq = 60f;
                _noiseLevel = 0.015f;
                _saturationKnee = 0.6f;
                break;
            case TapeSpeedIPS.IPS_15:
                _hfCutoff = 16000f;
                _headBumpFreq = 80f;
                _noiseLevel = 0.008f;
                _saturationKnee = 0.7f;
                break;
            case TapeSpeedIPS.IPS_30:
                _hfCutoff = 20000f;
                _headBumpFreq = 100f;
                _noiseLevel = 0.004f;
                _saturationKnee = 0.8f;
                break;
        }

        // Adjust for machine type
        switch (_machineType)
        {
            case TapeMachineType.Modern:
                _noiseLevel *= 0.5f;
                _saturationKnee += 0.1f;
                break;
            case TapeMachineType.American:
                _headBumpFreq *= 0.9f;
                break;
            case TapeMachineType.European:
                _hfCutoff *= 1.05f;
                _saturationKnee += 0.05f;
                break;
            case TapeMachineType.Cassette:
                _hfCutoff = 10000f;
                _noiseLevel *= 3f;
                _saturationKnee = 0.5f;
                break;
        }

        _configDirty = false;
    }

    /// <inheritdoc/>
    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        UpdateConfiguration();

        if (_delayBufferL.Length == 0)
        {
            InitializeDelayBuffers();
        }

        int channels = Channels;
        int sampleRate = SampleRate;

        // Get parameters
        float saturation = Saturation;
        float bias = Bias;
        float wowAmount = WowAmount;
        float flutterAmount = FlutterAmount;
        float hiss = Hiss;
        float headBump = HeadBump;
        float hfRolloff = HFRolloff;
        float compression = Compression;
        float outputLevel = OutputLevel;

        // Pre-calculate coefficients
        float hfCoeff = CalculateLowpassCoeff(_hfCutoff * (1f - hfRolloff * 0.5f), sampleRate);
        float headBumpCoeff = CalculateBandpassCoeff(_headBumpFreq, 2.0f, sampleRate);
        float dcBlockCoeff = CalculateHighpassCoeff(10f, sampleRate);
        float hissLpCoeff = CalculateLowpassCoeff(8000f, sampleRate);
        float hissHpCoeff = CalculateHighpassCoeff(200f, sampleRate);
        float inputHpCoeff = CalculateHighpassCoeff(20f, sampleRate);

        // Wow/flutter rates
        double wowRate = 0.5; // Hz - slow tape speed variation
        double flutterRate1 = 5.0; // Hz - capstan wobble
        double flutterRate2 = 12.0; // Hz - motor cogging
        double flutterRate3 = 25.0; // Hz - secondary flutter

        double wowInc = 2.0 * Math.PI * wowRate / sampleRate;
        double flutterInc1 = 2.0 * Math.PI * flutterRate1 / sampleRate;
        double flutterInc2 = 2.0 * Math.PI * flutterRate2 / sampleRate;
        double flutterInc3 = 2.0 * Math.PI * flutterRate3 / sampleRate;

        // Compression time constants
        float attackCoeff = (float)Math.Exp(-1.0 / (0.003 * sampleRate)); // 3ms attack
        float releaseCoeff = (float)Math.Exp(-1.0 / (0.1 * sampleRate));  // 100ms release

        // Drive calculation
        float drive = 1f + saturation * 4f;
        float biasOffset = bias * 0.15f;

        for (int n = 0; n < count; n += channels)
        {
            // Calculate modulation values
            float wowMod = (float)(Math.Sin(_wowPhase) * wowAmount * 0.003);
            float flutterMod = (float)((Math.Sin(_flutterPhase1) * 0.6 +
                                        Math.Sin(_flutterPhase2) * 0.3 +
                                        Math.Sin(_flutterPhase3) * 0.1) * flutterAmount * 0.001);

            float totalMod = wowMod + flutterMod;

            // Update oscillator phases
            _wowPhase += wowInc;
            if (_wowPhase > 2 * Math.PI) _wowPhase -= 2 * Math.PI;
            _flutterPhase1 += flutterInc1;
            if (_flutterPhase1 > 2 * Math.PI) _flutterPhase1 -= 2 * Math.PI;
            _flutterPhase2 += flutterInc2;
            if (_flutterPhase2 > 2 * Math.PI) _flutterPhase2 -= 2 * Math.PI;
            _flutterPhase3 += flutterInc3;
            if (_flutterPhase3 > 2 * Math.PI) _flutterPhase3 -= 2 * Math.PI;

            // Generate tape hiss (pink noise shaped)
            float noiseL = 0, noiseR = 0;
            if (hiss > 0)
            {
                noiseL = GeneratePinkNoise(ref _pinkB0L, ref _pinkB1L, ref _pinkB2L) * _noiseLevel * hiss;
                noiseR = GeneratePinkNoise(ref _pinkB0R, ref _pinkB1R, ref _pinkB2R) * _noiseLevel * hiss;

                // Shape noise - less low end, focused in mids
                noiseL = ApplyHighpass(noiseL, ref _hissHpStateL, hissHpCoeff);
                noiseL = ApplyLowpass(noiseL, ref _hissLpStateL, hissLpCoeff);
                noiseR = ApplyHighpass(noiseR, ref _hissHpStateR, hissHpCoeff);
                noiseR = ApplyLowpass(noiseR, ref _hissLpStateR, hissLpCoeff);
            }

            // Process each channel
            for (int ch = 0; ch < channels; ch++)
            {
                float input = sourceBuffer[n + ch];

                // Input highpass to remove DC and subsonic content
                ref float inputHpState = ref (ch == 0 ? ref _inputHpStateL : ref _inputHpStateR);
                input = ApplyHighpass(input, ref inputHpState, inputHpCoeff);

                // Apply wow/flutter via delay interpolation
                float modulated = ApplyWowFlutter(input, totalMod, ch);

                // Apply drive and bias
                float driven = modulated * drive + biasOffset;

                // Tape compression before saturation
                if (compression > 0)
                {
                    ref float env = ref (ch == 0 ? ref _envL : ref _envR);
                    float level = Math.Abs(driven);
                    float coeff = level > env ? attackCoeff : releaseCoeff;
                    env = level + coeff * (env - level);

                    float gainReduction = 1f / (1f + env * compression * 3f);
                    driven *= MathF.Sqrt(gainReduction); // Soft compression curve
                }

                // Tape saturation with hysteresis
                ref float hysteresisState = ref (ch == 0 ? ref _hysteresisStateL : ref _hysteresisStateR);
                float saturated = ApplyTapeSaturation(driven, hysteresisState, saturation, biasOffset);
                hysteresisState = saturated * 0.1f; // Hysteresis memory

                // Head bump (low frequency resonance)
                if (headBump > 0)
                {
                    ref float hbState1 = ref (ch == 0 ? ref _headBumpStateL : ref _headBumpStateR);
                    ref float hbState2 = ref (ch == 0 ? ref _headBumpState2L : ref _headBumpState2R);
                    float bump = ApplyBandpass(saturated, ref hbState1, ref hbState2, headBumpCoeff);
                    saturated += bump * headBump * 0.4f;
                }

                // High frequency rolloff
                ref float hfState = ref (ch == 0 ? ref _hfRolloffStateL : ref _hfRolloffStateR);
                float output = ApplyLowpass(saturated, ref hfState, hfCoeff);

                // Add tape hiss
                output += (ch == 0 ? noiseL : noiseR);

                // DC blocking
                ref float dcState = ref (ch == 0 ? ref _dcBlockStateL : ref _dcBlockStateR);
                output = ApplyHighpass(output, ref dcState, dcBlockCoeff);

                // Output level
                destBuffer[offset + n + ch] = output * outputLevel;
            }

            // Update delay buffer write position
            _delayWritePos = (_delayWritePos + 1) % _maxDelaySamples;
        }
    }

    /// <summary>
    /// Applies wow/flutter pitch modulation using delay line interpolation.
    /// </summary>
    private float ApplyWowFlutter(float input, float modulation, int channel)
    {
        float[] delayBuffer = channel == 0 ? _delayBufferL : _delayBufferR;

        // Write current sample to delay buffer
        delayBuffer[_delayWritePos] = input;

        if (Math.Abs(modulation) < 0.0001f)
        {
            return input;
        }

        // Calculate modulated read position
        float delaySamples = (1f + modulation) * 10f; // Base delay of 10 samples with modulation
        delaySamples = Math.Clamp(delaySamples, 1f, _maxDelaySamples - 1f);

        float readPosF = _delayWritePos - delaySamples;
        if (readPosF < 0) readPosF += _maxDelaySamples;

        // Linear interpolation for smooth pitch modulation
        int readPos0 = (int)readPosF;
        int readPos1 = (readPos0 + 1) % _maxDelaySamples;
        float frac = readPosF - readPos0;

        return delayBuffer[readPos0] * (1f - frac) + delayBuffer[readPos1] * frac;
    }

    /// <summary>
    /// Applies tape saturation with magnetic hysteresis modeling.
    /// </summary>
    private float ApplyTapeSaturation(float input, float hysteresisState, float saturation, float bias)
    {
        // Add hysteresis influence
        input += hysteresisState;

        // Tape saturation curve - asymmetric soft clipping
        float knee = _saturationKnee;
        float absInput = Math.Abs(input);

        if (absInput < knee)
        {
            // Below knee - mostly linear with slight curve
            return input * (1f + saturation * 0.1f * absInput);
        }

        // Above knee - soft saturation
        float sign = input >= 0 ? 1f : -1f;
        float excess = absInput - knee;

        // Asymmetric saturation based on bias
        float positiveGain = 1f + bias * 0.2f;
        float negativeGain = 1f - bias * 0.2f;
        float asymmetry = sign > 0 ? positiveGain : negativeGain;

        // Soft saturation using tanh
        float saturated = knee + (1f - knee) * (float)Math.Tanh(excess / (1f - knee) * (1f + saturation));
        saturated *= asymmetry;

        // Apply final clipping
        saturated = Math.Clamp(saturated, -1.2f, 1.2f);

        return saturated * sign;
    }

    /// <summary>
    /// Generates pink noise using the Voss-McCartney algorithm.
    /// </summary>
    private float GeneratePinkNoise(ref float b0, ref float b1, ref float b2)
    {
        float white = (float)(_random.NextDouble() * 2 - 1);

        b0 = 0.99886f * b0 + white * 0.0555179f;
        b1 = 0.99332f * b1 + white * 0.0750759f;
        b2 = 0.96900f * b2 + white * 0.1538520f;

        float pink = b0 + b1 + b2 + white * 0.5362f;
        return pink * 0.11f; // Normalize
    }

    #region Filter Helpers

    private static float CalculateLowpassCoeff(float cutoff, int sampleRate)
    {
        float w = 2f * MathF.PI * cutoff / sampleRate;
        return 1f - MathF.Exp(-w);
    }

    private static float CalculateHighpassCoeff(float cutoff, int sampleRate)
    {
        return MathF.Exp(-2f * MathF.PI * cutoff / sampleRate);
    }

    private static float CalculateBandpassCoeff(float freq, float q, int sampleRate)
    {
        float w = 2f * MathF.PI * freq / sampleRate;
        return w / q;
    }

    private static float ApplyLowpass(float input, ref float state, float coeff)
    {
        state += coeff * (input - state);
        return state;
    }

    private static float ApplyHighpass(float input, ref float state, float coeff)
    {
        float output = input - state;
        state = input - output * coeff;
        return output;
    }

    private static float ApplyBandpass(float input, ref float state1, ref float state2, float coeff)
    {
        state1 += coeff * (input - state1);
        state2 += coeff * (state1 - state2);
        return state1 - state2;
    }

    #endregion

    #region Presets

    /// <summary>
    /// Creates a subtle tape warmth preset.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <returns>Configured TapeEmulation effect.</returns>
    public static TapeEmulation CreateSubtle(ISampleProvider source)
    {
        var effect = new TapeEmulation(source, "Subtle Tape");
        effect.TapeSpeed = TapeSpeedIPS.IPS_30;
        effect.MachineType = TapeMachineType.Modern;
        effect.Saturation = 0.2f;
        effect.HeadBump = 0.3f;
        effect.HFRolloff = 0.2f;
        effect.Compression = 0.1f;
        return effect;
    }

    /// <summary>
    /// Creates a warm analog preset.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <returns>Configured TapeEmulation effect.</returns>
    public static TapeEmulation CreateWarm(ISampleProvider source)
    {
        var effect = new TapeEmulation(source, "Warm Tape");
        effect.TapeSpeed = TapeSpeedIPS.IPS_15;
        effect.MachineType = TapeMachineType.American;
        effect.Saturation = 0.5f;
        effect.HeadBump = 0.6f;
        effect.HFRolloff = 0.4f;
        effect.Compression = 0.3f;
        return effect;
    }

    /// <summary>
    /// Creates a hot/driven tape preset.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <returns>Configured TapeEmulation effect.</returns>
    public static TapeEmulation CreateHot(ISampleProvider source)
    {
        var effect = new TapeEmulation(source, "Hot Tape");
        effect.TapeSpeed = TapeSpeedIPS.IPS_15;
        effect.MachineType = TapeMachineType.American;
        effect.Saturation = 0.8f;
        effect.Bias = 0.2f;
        effect.HeadBump = 0.5f;
        effect.HFRolloff = 0.3f;
        effect.Compression = 0.5f;
        return effect;
    }

    /// <summary>
    /// Creates a vintage lo-fi preset with artifacts.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <returns>Configured TapeEmulation effect.</returns>
    public static TapeEmulation CreateLoFi(ISampleProvider source)
    {
        var effect = new TapeEmulation(source, "Lo-Fi Tape");
        effect.TapeSpeed = TapeSpeedIPS.IPS_7_5;
        effect.MachineType = TapeMachineType.Cassette;
        effect.Saturation = 0.6f;
        effect.WowAmount = 0.3f;
        effect.FlutterAmount = 0.4f;
        effect.Hiss = 0.5f;
        effect.HeadBump = 0.7f;
        effect.HFRolloff = 0.6f;
        effect.Compression = 0.4f;
        return effect;
    }

    /// <summary>
    /// Creates a vintage reel-to-reel preset.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <returns>Configured TapeEmulation effect.</returns>
    public static TapeEmulation CreateVintage(ISampleProvider source)
    {
        var effect = new TapeEmulation(source, "Vintage Tape");
        effect.TapeSpeed = TapeSpeedIPS.IPS_15;
        effect.MachineType = TapeMachineType.European;
        effect.Saturation = 0.4f;
        effect.Bias = 0.1f;
        effect.WowAmount = 0.15f;
        effect.FlutterAmount = 0.1f;
        effect.Hiss = 0.2f;
        effect.HeadBump = 0.5f;
        effect.HFRolloff = 0.35f;
        effect.Compression = 0.3f;
        return effect;
    }

    /// <summary>
    /// Creates a mastering-grade tape preset.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <returns>Configured TapeEmulation effect.</returns>
    public static TapeEmulation CreateMastering(ISampleProvider source)
    {
        var effect = new TapeEmulation(source, "Mastering Tape");
        effect.TapeSpeed = TapeSpeedIPS.IPS_30;
        effect.MachineType = TapeMachineType.Modern;
        effect.Saturation = 0.25f;
        effect.HeadBump = 0.4f;
        effect.HFRolloff = 0.15f;
        effect.Compression = 0.2f;
        return effect;
    }

    /// <summary>
    /// Creates a tape effect from a preset name.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <param name="presetName">Name of the preset (subtle, warm, hot, lofi, vintage, mastering).</param>
    /// <returns>Configured TapeEmulation effect.</returns>
    public static TapeEmulation CreatePreset(ISampleProvider source, string presetName)
    {
        return presetName.ToLowerInvariant() switch
        {
            "subtle" => CreateSubtle(source),
            "warm" => CreateWarm(source),
            "hot" => CreateHot(source),
            "lofi" or "lo-fi" => CreateLoFi(source),
            "vintage" => CreateVintage(source),
            "mastering" => CreateMastering(source),
            _ => new TapeEmulation(source)
        };
    }

    #endregion
}
