// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Dynamics;

/// <summary>
/// Band configuration for multiband transient shaping.
/// </summary>
public class TransientBand
{
    /// <summary>
    /// Band index (0-3).
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Lower frequency bound in Hz.
    /// </summary>
    public float LowFrequency { get; set; }

    /// <summary>
    /// Upper frequency bound in Hz.
    /// </summary>
    public float HighFrequency { get; set; }

    /// <summary>
    /// Attack enhancement (-100 to +100).
    /// Negative values reduce transients, positive values enhance.
    /// </summary>
    public float Attack { get; set; }

    /// <summary>
    /// Sustain enhancement (-100 to +100).
    /// Negative values reduce sustain, positive values enhance.
    /// </summary>
    public float Sustain { get; set; }

    /// <summary>
    /// Whether this band is enabled for processing.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether this band is soloed for monitoring.
    /// </summary>
    public bool Solo { get; set; }

    /// <summary>
    /// Creates a new transient band configuration.
    /// </summary>
    public TransientBand(int index, float lowFreq, float highFreq)
    {
        Index = index;
        LowFrequency = lowFreq;
        HighFrequency = highFreq;
        Attack = 0f;
        Sustain = 0f;
    }
}

/// <summary>
/// Multiband transient shaper with 4 frequency bands and per-band attack/sustain control.
/// </summary>
/// <remarks>
/// Features:
/// - 4 configurable frequency bands
/// - Per-band attack and sustain control
/// - Transient detection per band
/// - Solo/bypass per band
/// - Mix control for parallel processing
/// </remarks>
public class TransientDesigner : EffectBase
{
    private const int NumBands = 4;

    // Band configurations
    private readonly TransientBand[] _bands;

    // Crossover filter states (per channel, per crossover)
    private float[][] _lpState = null!;
    private float[][] _hpState = null!;
    private float[][] _lpState2 = null!;  // 2nd order
    private float[][] _hpState2 = null!;

    // Envelope followers per band per channel
    private float[][] _fastEnvelope = null!;
    private float[][] _slowEnvelope = null!;
    private float[][] _transientGain = null!;
    private float[][] _sustainGain = null!;

    // Band buffers
    private float[][][] _bandBuffer = null!;  // [band][channel][sample]

    private bool _initialized;

    /// <summary>
    /// Creates a new multiband transient designer.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    public TransientDesigner(ISampleProvider source) : this(source, "Transient Designer")
    {
    }

    /// <summary>
    /// Creates a new multiband transient designer with a custom name.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <param name="name">Effect name.</param>
    public TransientDesigner(ISampleProvider source, string name) : base(source, name)
    {
        // Initialize bands with default crossover frequencies
        _bands = new TransientBand[NumBands]
        {
            new TransientBand(0, 20f, 200f),       // Sub/Bass
            new TransientBand(1, 200f, 2000f),    // Low-Mid
            new TransientBand(2, 2000f, 8000f),   // High-Mid
            new TransientBand(3, 8000f, 20000f)   // High
        };

        RegisterParameter("Sensitivity", 50f);      // 0-100
        RegisterParameter("FastAttack", 0.1f);      // ms
        RegisterParameter("FastRelease", 5f);       // ms
        RegisterParameter("SlowAttack", 20f);       // ms
        RegisterParameter("SlowRelease", 200f);     // ms
        RegisterParameter("OutputGain", 0f);        // dB
        RegisterParameter("Mix", 1f);

        _initialized = false;
    }

    /// <summary>
    /// Gets the band configurations.
    /// </summary>
    public IReadOnlyList<TransientBand> Bands => _bands;

    /// <summary>
    /// Gets or sets the transient detection sensitivity (0-100).
    /// </summary>
    public float Sensitivity
    {
        get => GetParameter("Sensitivity");
        set => SetParameter("Sensitivity", Math.Clamp(value, 0f, 100f));
    }

    /// <summary>
    /// Gets or sets the fast envelope attack time in milliseconds.
    /// </summary>
    public float FastAttack
    {
        get => GetParameter("FastAttack");
        set => SetParameter("FastAttack", Math.Clamp(value, 0.01f, 10f));
    }

    /// <summary>
    /// Gets or sets the fast envelope release time in milliseconds.
    /// </summary>
    public float FastRelease
    {
        get => GetParameter("FastRelease");
        set => SetParameter("FastRelease", Math.Clamp(value, 1f, 50f));
    }

    /// <summary>
    /// Gets or sets the slow envelope attack time in milliseconds.
    /// </summary>
    public float SlowAttack
    {
        get => GetParameter("SlowAttack");
        set => SetParameter("SlowAttack", Math.Clamp(value, 5f, 100f));
    }

    /// <summary>
    /// Gets or sets the slow envelope release time in milliseconds.
    /// </summary>
    public float SlowRelease
    {
        get => GetParameter("SlowRelease");
        set => SetParameter("SlowRelease", Math.Clamp(value, 50f, 1000f));
    }

    /// <summary>
    /// Gets or sets the output gain in dB (-24 to +24).
    /// </summary>
    public float OutputGain
    {
        get => GetParameter("OutputGain");
        set => SetParameter("OutputGain", Math.Clamp(value, -24f, 24f));
    }

    /// <summary>
    /// Gets the band at the specified index.
    /// </summary>
    public TransientBand GetBand(int index) => _bands[index];

    /// <summary>
    /// Sets attack and sustain for all bands.
    /// </summary>
    public void SetAllBands(float attack, float sustain)
    {
        foreach (var band in _bands)
        {
            band.Attack = attack;
            band.Sustain = sustain;
        }
    }

    /// <summary>
    /// Sets the crossover frequencies.
    /// </summary>
    public void SetCrossoverFrequencies(float low, float mid, float high)
    {
        _bands[0].HighFrequency = low;
        _bands[1].LowFrequency = low;
        _bands[1].HighFrequency = mid;
        _bands[2].LowFrequency = mid;
        _bands[2].HighFrequency = high;
        _bands[3].LowFrequency = high;
    }

    /// <summary>
    /// Initializes internal buffers.
    /// </summary>
    private void Initialize()
    {
        int channels = Channels;

        // Crossover filter states (3 crossovers for 4 bands)
        _lpState = new float[channels][];
        _hpState = new float[channels][];
        _lpState2 = new float[channels][];
        _hpState2 = new float[channels][];

        for (int ch = 0; ch < channels; ch++)
        {
            _lpState[ch] = new float[3];
            _hpState[ch] = new float[3];
            _lpState2[ch] = new float[3];
            _hpState2[ch] = new float[3];
        }

        // Envelope followers per band per channel
        _fastEnvelope = new float[NumBands][];
        _slowEnvelope = new float[NumBands][];
        _transientGain = new float[NumBands][];
        _sustainGain = new float[NumBands][];

        for (int band = 0; band < NumBands; band++)
        {
            _fastEnvelope[band] = new float[channels];
            _slowEnvelope[band] = new float[channels];
            _transientGain[band] = new float[channels];
            _sustainGain[band] = new float[channels];

            for (int ch = 0; ch < channels; ch++)
            {
                _transientGain[band][ch] = 1f;
                _sustainGain[band][ch] = 1f;
            }
        }

        _initialized = true;
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        if (!_initialized)
        {
            Initialize();
        }

        int channels = Channels;
        int sampleRate = SampleRate;
        int frames = count / channels;

        // Ensure band buffers are large enough
        if (_bandBuffer == null || _bandBuffer[0][0].Length < frames)
        {
            _bandBuffer = new float[NumBands][][];
            for (int band = 0; band < NumBands; band++)
            {
                _bandBuffer[band] = new float[channels][];
                for (int ch = 0; ch < channels; ch++)
                {
                    _bandBuffer[band][ch] = new float[frames];
                }
            }
        }

        // Get parameters
        float sensitivity = Sensitivity / 100f;
        float fastAttackMs = FastAttack;
        float fastReleaseMs = FastRelease;
        float slowAttackMs = SlowAttack;
        float slowReleaseMs = SlowRelease;
        float outputGainLinear = DbToLinear(OutputGain);

        // Calculate envelope coefficients
        float fastAttackCoef = MathF.Exp(-1f / (fastAttackMs * sampleRate / 1000f));
        float fastReleaseCoef = MathF.Exp(-1f / (fastReleaseMs * sampleRate / 1000f));
        float slowAttackCoef = MathF.Exp(-1f / (slowAttackMs * sampleRate / 1000f));
        float slowReleaseCoef = MathF.Exp(-1f / (slowReleaseMs * sampleRate / 1000f));

        // Gain smoothing
        float gainSmoothCoef = MathF.Exp(-1f / (5f * sampleRate / 1000f));

        // Get crossover frequencies
        float crossover1 = _bands[0].HighFrequency;
        float crossover2 = _bands[1].HighFrequency;
        float crossover3 = _bands[2].HighFrequency;

        // Calculate crossover filter coefficients (Linkwitz-Riley style)
        float lp1Coef = MathF.Exp(-2f * MathF.PI * crossover1 / sampleRate);
        float lp2Coef = MathF.Exp(-2f * MathF.PI * crossover2 / sampleRate);
        float lp3Coef = MathF.Exp(-2f * MathF.PI * crossover3 / sampleRate);

        // Check for solo
        bool anySolo = _bands.Any(b => b.Solo);

        // Split into bands and process
        for (int frame = 0; frame < frames; frame++)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                float input = sourceBuffer[frame * channels + ch];

                // Crossover filtering (cascaded lowpass/highpass)
                // Band 0: LP at crossover1
                _lpState[ch][0] = _lpState[ch][0] * lp1Coef + input * (1f - lp1Coef);
                _lpState2[ch][0] = _lpState2[ch][0] * lp1Coef + _lpState[ch][0] * (1f - lp1Coef);
                float band0 = _lpState2[ch][0];

                // Band 1: HP at crossover1, LP at crossover2
                float hp1 = input - _lpState2[ch][0];
                _lpState[ch][1] = _lpState[ch][1] * lp2Coef + hp1 * (1f - lp2Coef);
                _lpState2[ch][1] = _lpState2[ch][1] * lp2Coef + _lpState[ch][1] * (1f - lp2Coef);
                float band1 = _lpState2[ch][1];

                // Band 2: HP at crossover2, LP at crossover3
                float hp2 = hp1 - _lpState2[ch][1];
                _lpState[ch][2] = _lpState[ch][2] * lp3Coef + hp2 * (1f - lp3Coef);
                _lpState2[ch][2] = _lpState2[ch][2] * lp3Coef + _lpState[ch][2] * (1f - lp3Coef);
                float band2 = _lpState2[ch][2];

                // Band 3: HP at crossover3
                float band3 = hp2 - _lpState2[ch][2];

                // Store in band buffers
                _bandBuffer[0][ch][frame] = band0;
                _bandBuffer[1][ch][frame] = band1;
                _bandBuffer[2][ch][frame] = band2;
                _bandBuffer[3][ch][frame] = band3;
            }
        }

        // Process transients per band
        for (int band = 0; band < NumBands; band++)
        {
            var bandConfig = _bands[band];
            if (!bandConfig.Enabled && !bandConfig.Solo)
                continue;

            float attackAmount = bandConfig.Attack / 100f;
            float sustainAmount = bandConfig.Sustain / 100f;

            for (int frame = 0; frame < frames; frame++)
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    float sample = _bandBuffer[band][ch][frame];
                    float sampleAbs = MathF.Abs(sample);

                    // Update fast envelope
                    float fastCoef = sampleAbs > _fastEnvelope[band][ch] ? fastAttackCoef : fastReleaseCoef;
                    _fastEnvelope[band][ch] = sampleAbs + fastCoef * (_fastEnvelope[band][ch] - sampleAbs);

                    // Update slow envelope
                    float slowCoef = sampleAbs > _slowEnvelope[band][ch] ? slowAttackCoef : slowReleaseCoef;
                    _slowEnvelope[band][ch] = sampleAbs + slowCoef * (_slowEnvelope[band][ch] - sampleAbs);

                    // Calculate transient amount
                    float envelopeDiff = _fastEnvelope[band][ch] - _slowEnvelope[band][ch];
                    float signalLevel = MathF.Max(_slowEnvelope[band][ch], 1e-6f);
                    float normalizedDiff = envelopeDiff / signalLevel;

                    // Apply sensitivity
                    float transientAmount = MathF.Max(0f, normalizedDiff * (0.1f + sensitivity * 0.9f));
                    transientAmount = MathF.Min(transientAmount, 1f);

                    float sustainAmountNorm = 1f - transientAmount;

                    // Calculate target gains
                    float targetTransientGain = 1f + attackAmount;    // 0 to 2
                    float targetSustainGain = 1f + sustainAmount;     // 0 to 2

                    // Smooth gains
                    _transientGain[band][ch] = targetTransientGain + gainSmoothCoef * (_transientGain[band][ch] - targetTransientGain);
                    _sustainGain[band][ch] = targetSustainGain + gainSmoothCoef * (_sustainGain[band][ch] - targetSustainGain);

                    // Apply transient shaping
                    float combinedGain = (transientAmount * _transientGain[band][ch]) +
                                        (sustainAmountNorm * _sustainGain[band][ch]);

                    _bandBuffer[band][ch][frame] = sample * combinedGain;
                }
            }
        }

        // Sum bands back together
        for (int frame = 0; frame < frames; frame++)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                float output = 0f;

                for (int band = 0; band < NumBands; band++)
                {
                    var bandConfig = _bands[band];

                    // Solo logic
                    if (anySolo)
                    {
                        if (bandConfig.Solo)
                        {
                            output += _bandBuffer[band][ch][frame];
                        }
                    }
                    else if (bandConfig.Enabled)
                    {
                        output += _bandBuffer[band][ch][frame];
                    }
                }

                output *= outputGainLinear;
                destBuffer[offset + frame * channels + ch] = output;
            }
        }
    }

    private static float DbToLinear(float db) => MathF.Pow(10f, db / 20f);

    #region Presets

    /// <summary>
    /// Creates a preset for punchy drums.
    /// </summary>
    public static TransientDesigner CreatePunchyDrumsPreset(ISampleProvider source)
    {
        var effect = new TransientDesigner(source, "Punchy Drums");
        effect.SetCrossoverFrequencies(150f, 2500f, 8000f);

        effect.GetBand(0).Attack = 30f;   // Sub punch
        effect.GetBand(0).Sustain = -20f;

        effect.GetBand(1).Attack = 50f;   // Snare crack
        effect.GetBand(1).Sustain = -30f;

        effect.GetBand(2).Attack = 40f;   // Hi-hat definition
        effect.GetBand(2).Sustain = -40f;

        effect.GetBand(3).Attack = 30f;   // Air/presence
        effect.GetBand(3).Sustain = -20f;

        effect.Sensitivity = 60f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for sustaining guitars.
    /// </summary>
    public static TransientDesigner CreateSustainGuitarPreset(ISampleProvider source)
    {
        var effect = new TransientDesigner(source, "Sustain Guitar");
        effect.SetCrossoverFrequencies(200f, 2000f, 6000f);

        effect.GetBand(0).Attack = 0f;
        effect.GetBand(0).Sustain = 30f;

        effect.GetBand(1).Attack = -20f;
        effect.GetBand(1).Sustain = 40f;

        effect.GetBand(2).Attack = -10f;
        effect.GetBand(2).Sustain = 30f;

        effect.GetBand(3).Attack = 0f;
        effect.GetBand(3).Sustain = 20f;

        effect.Sensitivity = 50f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for reducing room sound.
    /// </summary>
    public static TransientDesigner CreateReduceRoomPreset(ISampleProvider source)
    {
        var effect = new TransientDesigner(source, "Reduce Room");

        foreach (var band in effect.Bands)
        {
            band.Attack = 20f;
            band.Sustain = -40f;
        }

        effect.Sensitivity = 70f;
        effect.SlowRelease = 300f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for adding snap to percussion.
    /// </summary>
    public static TransientDesigner CreatePercussionSnapPreset(ISampleProvider source)
    {
        var effect = new TransientDesigner(source, "Percussion Snap");
        effect.SetCrossoverFrequencies(100f, 1000f, 5000f);

        effect.GetBand(0).Attack = 20f;
        effect.GetBand(0).Sustain = 0f;

        effect.GetBand(1).Attack = 60f;
        effect.GetBand(1).Sustain = -20f;

        effect.GetBand(2).Attack = 70f;
        effect.GetBand(2).Sustain = -30f;

        effect.GetBand(3).Attack = 50f;
        effect.GetBand(3).Sustain = -20f;

        effect.Sensitivity = 65f;
        effect.FastRelease = 8f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for bass definition.
    /// </summary>
    public static TransientDesigner CreateBassDefinitionPreset(ISampleProvider source)
    {
        var effect = new TransientDesigner(source, "Bass Definition");
        effect.SetCrossoverFrequencies(80f, 250f, 800f);

        effect.GetBand(0).Attack = 40f;   // Sub attack
        effect.GetBand(0).Sustain = -10f;

        effect.GetBand(1).Attack = 50f;   // Punch
        effect.GetBand(1).Sustain = -20f;

        effect.GetBand(2).Attack = 30f;   // Midrange clarity
        effect.GetBand(2).Sustain = 0f;

        effect.GetBand(3).Attack = 20f;   // String noise
        effect.GetBand(3).Sustain = -10f;

        effect.Sensitivity = 55f;
        return effect;
    }

    #endregion
}
