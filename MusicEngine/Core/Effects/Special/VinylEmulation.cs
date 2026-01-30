//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Vinyl record playback emulation with crackle, pops, surface noise, wow, and RIAA EQ.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Special;

/// <summary>
/// Vinyl condition presets affecting overall character.
/// </summary>
public enum VinylCondition
{
    /// <summary>Mint condition - minimal artifacts, pristine sound.</summary>
    Mint,
    /// <summary>Very good condition - light wear, occasional crackle.</summary>
    VeryGood,
    /// <summary>Good condition - moderate wear, regular crackle and pops.</summary>
    Good,
    /// <summary>Fair condition - heavy wear, frequent crackle and pops.</summary>
    Fair,
    /// <summary>Poor condition - very worn, constant noise and artifacts.</summary>
    Poor
}

/// <summary>
/// Vinyl emulation effect that simulates the character of vinyl record playback.
/// </summary>
/// <remarks>
/// Features include:
/// - Crackle and pop generation (random impulse noise)
/// - Surface noise (filtered noise floor)
/// - Wow effect (slow pitch/amplitude modulation from turntable speed variation)
/// - RIAA equalization curve (bass rolloff, treble boost characteristic of vinyl)
/// - Dust and scratch simulation
/// - Authentic vinyl warmth and harmonic coloration
/// </remarks>
public class VinylEmulation : EffectBase
{
    private readonly Random _random;

    // Crackle/pop state
    private double _crackleAccumulator;
    private double _popAccumulator;
    private float _lastCrackle;
    private float _crackleDecay;

    // Surface noise state (pink noise filtered)
    private readonly float[] _pinkRows = new float[16];
    private int _pinkIndex;
    private float _pinkRunningSum;

    // Wow state (slow pitch modulation)
    private double _wowPhase;
    private double _wowPhase2; // Secondary wow oscillator for complexity
    private double _flutterPhase; // Higher frequency flutter

    // RIAA EQ filter state (biquad shelving filters)
    private float _riaaLowZ1, _riaaLowZ2;
    private float _riaaHighZ1, _riaaHighZ2;
    private float _riaaLowB0, _riaaLowB1, _riaaLowB2;
    private float _riaaLowA1, _riaaLowA2;
    private float _riaaHighB0, _riaaHighB1, _riaaHighB2;
    private float _riaaHighA1, _riaaHighA2;

    // Dust/scratch state
    private float _scratchPhase;
    private float _scratchIntensity;
    private int _scratchDuration;
    private int _scratchCounter;

    // Warmth saturation state
    private float _saturationState;

    // Stereo state for independent channel noise
    private float _noiseStateL;
    private float _noiseStateR;

    // Parameter backing fields
    private float _wearAmount = 0.3f;
    private float _crackleRate = 0.3f;
    private float _noiseLevel = 0.2f;
    private float _wowDepth = 0.15f;
    private float _riaaAmount = 0.5f;
    private float _dustAmount = 0.2f;
    private float _warmth = 0.4f;
    private VinylCondition _condition = VinylCondition.Good;

    private bool _riaaCoefficientsValid;

    /// <summary>
    /// Gets or sets the overall wear amount (0.0 - 1.0).
    /// Higher values simulate more worn vinyl with increased artifacts.
    /// </summary>
    public float WearAmount
    {
        get => _wearAmount;
        set
        {
            _wearAmount = Math.Clamp(value, 0f, 1f);
            SetParameter("WearAmount", _wearAmount);
        }
    }

    /// <summary>
    /// Gets or sets the crackle rate (0.0 - 1.0).
    /// Controls the density of crackle and pop sounds.
    /// </summary>
    public float CrackleRate
    {
        get => _crackleRate;
        set
        {
            _crackleRate = Math.Clamp(value, 0f, 1f);
            SetParameter("CrackleRate", _crackleRate);
        }
    }

    /// <summary>
    /// Gets or sets the surface noise level (0.0 - 1.0).
    /// Controls the amplitude of background vinyl hiss.
    /// </summary>
    public float NoiseLevel
    {
        get => _noiseLevel;
        set
        {
            _noiseLevel = Math.Clamp(value, 0f, 1f);
            SetParameter("NoiseLevel", _noiseLevel);
        }
    }

    /// <summary>
    /// Gets or sets the wow depth (0.0 - 1.0).
    /// Controls the amount of slow pitch variation from turntable speed fluctuation.
    /// </summary>
    public float WowDepth
    {
        get => _wowDepth;
        set
        {
            _wowDepth = Math.Clamp(value, 0f, 1f);
            SetParameter("WowDepth", _wowDepth);
        }
    }

    /// <summary>
    /// Gets or sets the RIAA EQ curve amount (0.0 - 1.0).
    /// Controls the strength of vinyl equalization characteristic.
    /// </summary>
    public float RiaaAmount
    {
        get => _riaaAmount;
        set
        {
            _riaaAmount = Math.Clamp(value, 0f, 1f);
            SetParameter("RiaaAmount", _riaaAmount);
            _riaaCoefficientsValid = false;
        }
    }

    /// <summary>
    /// Gets or sets the dust/scratch amount (0.0 - 1.0).
    /// Controls the frequency of dust particles and minor scratches.
    /// </summary>
    public float DustAmount
    {
        get => _dustAmount;
        set
        {
            _dustAmount = Math.Clamp(value, 0f, 1f);
            SetParameter("DustAmount", _dustAmount);
        }
    }

    /// <summary>
    /// Gets or sets the warmth/saturation amount (0.0 - 1.0).
    /// Adds subtle harmonic distortion for analog warmth.
    /// </summary>
    public float Warmth
    {
        get => _warmth;
        set
        {
            _warmth = Math.Clamp(value, 0f, 1f);
            SetParameter("Warmth", _warmth);
        }
    }

    /// <summary>
    /// Gets or sets the vinyl condition preset.
    /// Automatically configures parameters for realistic vinyl states.
    /// </summary>
    public VinylCondition Condition
    {
        get => _condition;
        set
        {
            _condition = value;
            ApplyConditionPreset(value);
        }
    }

    /// <summary>
    /// Creates a new vinyl emulation effect.
    /// </summary>
    /// <param name="source">The audio source to process.</param>
    public VinylEmulation(ISampleProvider source) : base(source, "Vinyl Emulation")
    {
        _random = new Random();

        // Register parameters with defaults
        RegisterParameter("WearAmount", 0.3f);
        RegisterParameter("CrackleRate", 0.3f);
        RegisterParameter("NoiseLevel", 0.2f);
        RegisterParameter("WowDepth", 0.15f);
        RegisterParameter("RiaaAmount", 0.5f);
        RegisterParameter("DustAmount", 0.2f);
        RegisterParameter("Warmth", 0.4f);
        RegisterParameter("Mix", 1.0f);

        // Initialize pink noise state
        for (int i = 0; i < _pinkRows.Length; i++)
        {
            _pinkRows[i] = (float)(_random.NextDouble() * 2.0 - 1.0);
            _pinkRunningSum += _pinkRows[i];
        }

        _riaaCoefficientsValid = false;
    }

    /// <summary>
    /// Applies parameter presets based on vinyl condition.
    /// </summary>
    private void ApplyConditionPreset(VinylCondition condition)
    {
        switch (condition)
        {
            case VinylCondition.Mint:
                _wearAmount = 0.05f;
                _crackleRate = 0.05f;
                _noiseLevel = 0.05f;
                _wowDepth = 0.02f;
                _dustAmount = 0.02f;
                _warmth = 0.2f;
                break;

            case VinylCondition.VeryGood:
                _wearAmount = 0.15f;
                _crackleRate = 0.15f;
                _noiseLevel = 0.1f;
                _wowDepth = 0.05f;
                _dustAmount = 0.1f;
                _warmth = 0.3f;
                break;

            case VinylCondition.Good:
                _wearAmount = 0.3f;
                _crackleRate = 0.3f;
                _noiseLevel = 0.2f;
                _wowDepth = 0.1f;
                _dustAmount = 0.2f;
                _warmth = 0.4f;
                break;

            case VinylCondition.Fair:
                _wearAmount = 0.5f;
                _crackleRate = 0.5f;
                _noiseLevel = 0.35f;
                _wowDepth = 0.2f;
                _dustAmount = 0.4f;
                _warmth = 0.5f;
                break;

            case VinylCondition.Poor:
                _wearAmount = 0.8f;
                _crackleRate = 0.8f;
                _noiseLevel = 0.5f;
                _wowDepth = 0.35f;
                _dustAmount = 0.6f;
                _warmth = 0.6f;
                break;
        }

        // Update registered parameters
        SetParameter("WearAmount", _wearAmount);
        SetParameter("CrackleRate", _crackleRate);
        SetParameter("NoiseLevel", _noiseLevel);
        SetParameter("WowDepth", _wowDepth);
        SetParameter("DustAmount", _dustAmount);
        SetParameter("Warmth", _warmth);
    }

    /// <summary>
    /// Calculates RIAA equalization filter coefficients.
    /// </summary>
    /// <remarks>
    /// RIAA curve characteristics:
    /// - Bass rolloff below 50Hz (rumble filter)
    /// - Slight bass boost around 500Hz
    /// - Treble rolloff above 2122Hz
    /// </remarks>
    private void CalculateRiaaCoefficients()
    {
        int sampleRate = SampleRate;
        float strength = _riaaAmount;

        // Low shelf filter for bass character (around 500Hz)
        float lowFreq = 500f;
        float lowGain = 2f * strength; // dB boost
        CalculateLowShelfCoefficients(lowFreq, lowGain, sampleRate,
            out _riaaLowB0, out _riaaLowB1, out _riaaLowB2,
            out _riaaLowA1, out _riaaLowA2);

        // High shelf filter for treble rolloff (around 2122Hz - RIAA time constant)
        float highFreq = 2122f;
        float highGain = -3f * strength; // dB cut
        CalculateHighShelfCoefficients(highFreq, highGain, sampleRate,
            out _riaaHighB0, out _riaaHighB1, out _riaaHighB2,
            out _riaaHighA1, out _riaaHighA2);

        _riaaCoefficientsValid = true;
    }

    /// <summary>
    /// Calculates low shelf biquad filter coefficients.
    /// </summary>
    private void CalculateLowShelfCoefficients(float frequency, float gainDb, int sampleRate,
        out float b0, out float b1, out float b2, out float a1, out float a2)
    {
        float a = MathF.Pow(10f, gainDb / 40f);
        float omega = 2f * MathF.PI * frequency / sampleRate;
        float sinOmega = MathF.Sin(omega);
        float cosOmega = MathF.Cos(omega);
        float alpha = sinOmega / 2f * MathF.Sqrt(2f);

        float a0 = (a + 1f) + (a - 1f) * cosOmega + 2f * MathF.Sqrt(a) * alpha;
        b0 = a * ((a + 1f) - (a - 1f) * cosOmega + 2f * MathF.Sqrt(a) * alpha) / a0;
        b1 = 2f * a * ((a - 1f) - (a + 1f) * cosOmega) / a0;
        b2 = a * ((a + 1f) - (a - 1f) * cosOmega - 2f * MathF.Sqrt(a) * alpha) / a0;
        a1 = -2f * ((a - 1f) + (a + 1f) * cosOmega) / a0;
        a2 = ((a + 1f) + (a - 1f) * cosOmega - 2f * MathF.Sqrt(a) * alpha) / a0;
    }

    /// <summary>
    /// Calculates high shelf biquad filter coefficients.
    /// </summary>
    private void CalculateHighShelfCoefficients(float frequency, float gainDb, int sampleRate,
        out float b0, out float b1, out float b2, out float a1, out float a2)
    {
        float a = MathF.Pow(10f, gainDb / 40f);
        float omega = 2f * MathF.PI * frequency / sampleRate;
        float sinOmega = MathF.Sin(omega);
        float cosOmega = MathF.Cos(omega);
        float alpha = sinOmega / 2f * MathF.Sqrt(2f);

        float a0 = (a + 1f) - (a - 1f) * cosOmega + 2f * MathF.Sqrt(a) * alpha;
        b0 = a * ((a + 1f) + (a - 1f) * cosOmega + 2f * MathF.Sqrt(a) * alpha) / a0;
        b1 = -2f * a * ((a - 1f) + (a + 1f) * cosOmega) / a0;
        b2 = a * ((a + 1f) + (a - 1f) * cosOmega - 2f * MathF.Sqrt(a) * alpha) / a0;
        a1 = 2f * ((a - 1f) - (a + 1f) * cosOmega) / a0;
        a2 = ((a + 1f) - (a - 1f) * cosOmega - 2f * MathF.Sqrt(a) * alpha) / a0;
    }

    /// <summary>
    /// Generates pink noise for surface hiss.
    /// </summary>
    private float GeneratePinkNoise()
    {
        int lastIndex = _pinkIndex;
        _pinkIndex++;

        int diff = lastIndex ^ _pinkIndex;

        for (int i = 0; i < _pinkRows.Length; i++)
        {
            if ((diff & (1 << i)) != 0)
            {
                _pinkRunningSum -= _pinkRows[i];
                _pinkRows[i] = (float)(_random.NextDouble() * 2.0 - 1.0);
                _pinkRunningSum += _pinkRows[i];
                break;
            }
        }

        float white = (float)(_random.NextDouble() * 2.0 - 1.0);
        return (_pinkRunningSum + white) / (_pinkRows.Length + 1) * 0.5f;
    }

    /// <summary>
    /// Generates crackle and pop artifacts.
    /// </summary>
    private float GenerateCrackle()
    {
        float sampleRate = SampleRate;

        // Crackle probability based on rate and wear
        float crackleProb = _crackleRate * _wearAmount * 0.001f;
        float popProb = _crackleRate * _wearAmount * 0.0002f;

        // Accumulate probability for crackles
        _crackleAccumulator += crackleProb;
        if (_crackleAccumulator > _random.NextDouble())
        {
            _crackleAccumulator = 0;
            // Generate sharp crackle impulse
            _lastCrackle = (float)(_random.NextDouble() * 2.0 - 1.0) * (0.3f + _wearAmount * 0.4f);
            _crackleDecay = 0.85f + (float)_random.NextDouble() * 0.1f;
        }

        // Accumulate probability for pops (larger, louder events)
        _popAccumulator += popProb;
        if (_popAccumulator > _random.NextDouble())
        {
            _popAccumulator = 0;
            // Generate larger pop with longer decay
            _lastCrackle = (float)(_random.NextDouble() > 0.5 ? 1.0 : -1.0) * (0.5f + _wearAmount * 0.3f);
            _crackleDecay = 0.95f;
        }

        // Decay the crackle
        float output = _lastCrackle;
        _lastCrackle *= _crackleDecay;

        // Kill very small values
        if (MathF.Abs(_lastCrackle) < 0.001f)
        {
            _lastCrackle = 0;
        }

        return output;
    }

    /// <summary>
    /// Generates dust and scratch artifacts.
    /// </summary>
    private float GenerateDustScratch()
    {
        float output = 0;

        // Random dust particle clicks
        if (_random.NextDouble() < _dustAmount * _wearAmount * 0.0005)
        {
            output = (float)(_random.NextDouble() * 2.0 - 1.0) * 0.15f * _dustAmount;
        }

        // Scratch simulation (brief periodic disturbance)
        if (_scratchCounter > 0)
        {
            _scratchCounter--;
            _scratchPhase += 0.3f;
            output += MathF.Sin(_scratchPhase) * _scratchIntensity * (float)_scratchCounter / _scratchDuration;
        }
        else if (_random.NextDouble() < _dustAmount * _wearAmount * 0.00005)
        {
            // Start a new scratch
            _scratchDuration = 50 + _random.Next(200);
            _scratchCounter = _scratchDuration;
            _scratchIntensity = 0.05f + (float)_random.NextDouble() * 0.1f * _dustAmount;
            _scratchPhase = 0;
        }

        return output;
    }

    /// <summary>
    /// Calculates wow modulation value (slow turntable speed variation).
    /// </summary>
    private float CalculateWow()
    {
        int sampleRate = SampleRate;

        // Primary wow - very slow (0.5-2 Hz, typical turntable wow)
        float wowFreq1 = 0.5f + _wowDepth * 1.5f;
        _wowPhase += 2.0 * Math.PI * wowFreq1 / sampleRate;
        if (_wowPhase > 2.0 * Math.PI) _wowPhase -= 2.0 * Math.PI;

        // Secondary wow - slightly different frequency for complexity
        float wowFreq2 = 0.7f + _wowDepth * 1.2f;
        _wowPhase2 += 2.0 * Math.PI * wowFreq2 / sampleRate;
        if (_wowPhase2 > 2.0 * Math.PI) _wowPhase2 -= 2.0 * Math.PI;

        // Flutter - faster variation (around 10Hz)
        float flutterFreq = 9f + _wowDepth * 3f;
        _flutterPhase += 2.0 * Math.PI * flutterFreq / sampleRate;
        if (_flutterPhase > 2.0 * Math.PI) _flutterPhase -= 2.0 * Math.PI;

        // Combine wow and flutter
        float wow = (float)(Math.Sin(_wowPhase) * 0.6 + Math.Sin(_wowPhase2) * 0.3);
        float flutter = (float)Math.Sin(_flutterPhase) * 0.1f;

        // Scale by depth parameter
        float totalModulation = (wow + flutter) * _wowDepth * 0.02f;

        return totalModulation;
    }

    /// <summary>
    /// Applies soft saturation for analog warmth.
    /// </summary>
    private float ApplyWarmth(float sample)
    {
        if (_warmth < 0.01f) return sample;

        // Soft clipping saturation curve
        float drive = 1f + _warmth * 2f;
        float driven = sample * drive;

        // Tanh-like soft saturation
        float saturated;
        if (MathF.Abs(driven) < 1f)
        {
            saturated = driven - (driven * driven * driven) / 3f;
        }
        else
        {
            saturated = MathF.Sign(driven) * 2f / 3f;
        }

        // Add subtle even harmonics (tube-like character)
        float evenHarmonic = sample * sample * MathF.Sign(sample) * 0.1f * _warmth;

        // Blend with original
        float result = sample * (1f - _warmth * 0.5f) + saturated * _warmth * 0.5f + evenHarmonic;

        // Simple DC blocking
        float dcBlocked = result - _saturationState;
        _saturationState = _saturationState * 0.995f + result * 0.005f;

        return dcBlocked;
    }

    /// <summary>
    /// Applies RIAA equalization filter.
    /// </summary>
    private float ApplyRiaaEq(float sample)
    {
        if (_riaaAmount < 0.01f) return sample;

        // Low shelf
        float lowOut = _riaaLowB0 * sample + _riaaLowZ1;
        _riaaLowZ1 = _riaaLowB1 * sample - _riaaLowA1 * lowOut + _riaaLowZ2;
        _riaaLowZ2 = _riaaLowB2 * sample - _riaaLowA2 * lowOut;

        // High shelf
        float highOut = _riaaHighB0 * lowOut + _riaaHighZ1;
        _riaaHighZ1 = _riaaHighB1 * lowOut - _riaaHighA1 * highOut + _riaaHighZ2;
        _riaaHighZ2 = _riaaHighB2 * lowOut - _riaaHighA2 * highOut;

        // Blend with original based on amount
        return sample * (1f - _riaaAmount) + highOut * _riaaAmount;
    }

    /// <inheritdoc/>
    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        if (!_riaaCoefficientsValid)
        {
            CalculateRiaaCoefficients();
        }

        int channels = Channels;

        for (int n = 0; n < count; n += channels)
        {
            // Calculate wow modulation for this sample
            float wowMod = CalculateWow();
            float wowMultiplier = 1f + wowMod;

            // Generate vinyl artifacts (same for both channels - centered)
            float crackle = GenerateCrackle();
            float dustScratch = GenerateDustScratch();

            // Generate surface noise (slightly different per channel for stereo width)
            float surfaceNoiseBase = GeneratePinkNoise() * _noiseLevel * 0.15f;
            float noiseVariation = (float)(_random.NextDouble() * 2.0 - 1.0) * 0.02f * _noiseLevel;

            float surfaceNoiseL = surfaceNoiseBase + noiseVariation;
            float surfaceNoiseR = surfaceNoiseBase - noiseVariation;

            // Smooth channel noise for more realistic hiss
            _noiseStateL = _noiseStateL * 0.7f + surfaceNoiseL * 0.3f;
            _noiseStateR = _noiseStateR * 0.7f + surfaceNoiseR * 0.3f;

            // Process left channel
            float inputL = sourceBuffer[n];
            float processedL = inputL * wowMultiplier;
            processedL = ApplyWarmth(processedL);
            processedL = ApplyRiaaEq(processedL);
            processedL += _noiseStateL + crackle + dustScratch;
            destBuffer[offset + n] = processedL;

            // Process right channel if stereo
            if (channels > 1)
            {
                float inputR = sourceBuffer[n + 1];
                float processedR = inputR * wowMultiplier;
                processedR = ApplyWarmth(processedR);
                processedR = ApplyRiaaEq(processedR);
                processedR += _noiseStateR + crackle + dustScratch;
                destBuffer[offset + n + 1] = processedR;
            }
        }
    }

    /// <inheritdoc/>
    protected override void OnParameterChanged(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "wearamount":
                _wearAmount = value;
                break;
            case "cracklerate":
                _crackleRate = value;
                break;
            case "noiselevel":
                _noiseLevel = value;
                break;
            case "wowdepth":
                _wowDepth = value;
                break;
            case "riaaamount":
                _riaaAmount = value;
                _riaaCoefficientsValid = false;
                break;
            case "dustamount":
                _dustAmount = value;
                break;
            case "warmth":
                _warmth = value;
                break;
        }
    }

    /// <summary>
    /// Resets the effect state.
    /// </summary>
    public void Reset()
    {
        _crackleAccumulator = 0;
        _popAccumulator = 0;
        _lastCrackle = 0;
        _wowPhase = 0;
        _wowPhase2 = 0;
        _flutterPhase = 0;
        _scratchCounter = 0;
        _saturationState = 0;
        _noiseStateL = 0;
        _noiseStateR = 0;

        // Reset RIAA filter state
        _riaaLowZ1 = 0;
        _riaaLowZ2 = 0;
        _riaaHighZ1 = 0;
        _riaaHighZ2 = 0;

        // Re-initialize pink noise
        _pinkIndex = 0;
        _pinkRunningSum = 0;
        for (int i = 0; i < _pinkRows.Length; i++)
        {
            _pinkRows[i] = (float)(_random.NextDouble() * 2.0 - 1.0);
            _pinkRunningSum += _pinkRows[i];
        }
    }

    /// <summary>
    /// Creates a vintage 1960s vinyl preset.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <returns>Configured VinylEmulation effect.</returns>
    public static VinylEmulation CreateVintage60s(ISampleProvider source)
    {
        var effect = new VinylEmulation(source);
        effect.Condition = VinylCondition.Good;
        effect.RiaaAmount = 0.7f;
        effect.Warmth = 0.6f;
        effect.WowDepth = 0.2f;
        effect.Mix = 1.0f;
        return effect;
    }

    /// <summary>
    /// Creates a clean modern vinyl preset.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <returns>Configured VinylEmulation effect.</returns>
    public static VinylEmulation CreateModernClean(ISampleProvider source)
    {
        var effect = new VinylEmulation(source);
        effect.Condition = VinylCondition.Mint;
        effect.RiaaAmount = 0.3f;
        effect.Warmth = 0.25f;
        effect.WowDepth = 0.03f;
        effect.Mix = 1.0f;
        return effect;
    }

    /// <summary>
    /// Creates a lo-fi hip-hop style vinyl preset.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <returns>Configured VinylEmulation effect.</returns>
    public static VinylEmulation CreateLoFiHipHop(ISampleProvider source)
    {
        var effect = new VinylEmulation(source);
        effect.WearAmount = 0.6f;
        effect.CrackleRate = 0.5f;
        effect.NoiseLevel = 0.4f;
        effect.WowDepth = 0.25f;
        effect.RiaaAmount = 0.5f;
        effect.DustAmount = 0.3f;
        effect.Warmth = 0.5f;
        effect.Mix = 1.0f;
        return effect;
    }

    /// <summary>
    /// Creates a dusty crate-digger vinyl preset.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <returns>Configured VinylEmulation effect.</returns>
    public static VinylEmulation CreateDustyCrate(ISampleProvider source)
    {
        var effect = new VinylEmulation(source);
        effect.Condition = VinylCondition.Poor;
        effect.RiaaAmount = 0.6f;
        effect.Warmth = 0.55f;
        effect.Mix = 1.0f;
        return effect;
    }

    /// <summary>
    /// Creates a subtle warmth-only vinyl preset.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <returns>Configured VinylEmulation effect.</returns>
    public static VinylEmulation CreateSubtleWarmth(ISampleProvider source)
    {
        var effect = new VinylEmulation(source);
        effect.WearAmount = 0.0f;
        effect.CrackleRate = 0.0f;
        effect.NoiseLevel = 0.02f;
        effect.WowDepth = 0.0f;
        effect.RiaaAmount = 0.3f;
        effect.DustAmount = 0.0f;
        effect.Warmth = 0.35f;
        effect.Mix = 1.0f;
        return effect;
    }
}
