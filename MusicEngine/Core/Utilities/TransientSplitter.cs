//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Transient/Sustain separation utility for splitting audio into transient and sustain components.

using NAudio.Wave;
using MusicEngine.Core.Effects.Dynamics;

namespace MusicEngine.Core.Utilities;

/// <summary>
/// Output mode for the transient splitter.
/// </summary>
public enum TransientSplitterOutputMode
{
    /// <summary>
    /// Output both transient and sustain mixed together.
    /// </summary>
    Mixed,

    /// <summary>
    /// Output only the transient component.
    /// </summary>
    TransientOnly,

    /// <summary>
    /// Output only the sustain component.
    /// </summary>
    SustainOnly,

    /// <summary>
    /// Output transient on left channel, sustain on right channel (for stereo output).
    /// </summary>
    ParallelSplit
}

/// <summary>
/// Band configuration for multiband transient splitting.
/// </summary>
public class TransientSplitterBand
{
    /// <summary>
    /// Band index.
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
    /// Transient gain for this band (-24 to +24 dB).
    /// </summary>
    public float TransientGain { get; set; }

    /// <summary>
    /// Sustain gain for this band (-24 to +24 dB).
    /// </summary>
    public float SustainGain { get; set; }

    /// <summary>
    /// Punch amount for this band (0 to 100).
    /// </summary>
    public float Punch { get; set; } = 50f;

    /// <summary>
    /// Smoothness amount for this band (0 to 100).
    /// </summary>
    public float Smoothness { get; set; } = 50f;

    /// <summary>
    /// Whether this band is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Creates a new transient splitter band.
    /// </summary>
    public TransientSplitterBand(int index, float lowFreq, float highFreq)
    {
        Index = index;
        LowFrequency = lowFreq;
        HighFrequency = highFreq;
        TransientGain = 0f;
        SustainGain = 0f;
    }
}

/// <summary>
/// Visualization data for transient vs sustain levels.
/// </summary>
public class TransientVisualizationData
{
    /// <summary>
    /// Current transient level (0 to 1).
    /// </summary>
    public float TransientLevel { get; set; }

    /// <summary>
    /// Current sustain level (0 to 1).
    /// </summary>
    public float SustainLevel { get; set; }

    /// <summary>
    /// Transient detection indicator (true when transient detected).
    /// </summary>
    public bool TransientDetected { get; set; }

    /// <summary>
    /// Peak transient level.
    /// </summary>
    public float PeakTransientLevel { get; set; }

    /// <summary>
    /// Peak sustain level.
    /// </summary>
    public float PeakSustainLevel { get; set; }

    /// <summary>
    /// Transient levels per band (if multiband enabled).
    /// </summary>
    public float[] BandTransientLevels { get; set; } = Array.Empty<float>();

    /// <summary>
    /// Sustain levels per band (if multiband enabled).
    /// </summary>
    public float[] BandSustainLevels { get; set; } = Array.Empty<float>();
}

/// <summary>
/// Transient/Sustain separation utility.
/// Splits audio into transient and sustain components with independent processing.
/// </summary>
/// <remarks>
/// Features:
/// - Split audio into transient and sustain components
/// - Transient sensitivity (detection threshold)
/// - Attack time (transient detection speed)
/// - Sustain time (how long after transient to blend back)
/// - Independent transient and sustain gain controls
/// - Transient and sustain shaping
/// - Multiple output modes (mixed, transient only, sustain only, parallel)
/// - Transient filter (frequency range for detection)
/// - Multiband mode (separate processing per frequency band)
/// - Punch and smoothness controls
/// - Lookahead for accurate transient capture
/// - Real-time visualization data
/// - Integration with TransientShaper
/// - Export transients as separate audio
/// - Gate transients below threshold
/// </remarks>
public class TransientSplitter : EffectBase
{
    private const int MaxBands = 4;
    private const int MaxLookaheadMs = 20;

    // Envelope followers per channel
    private float[] _fastEnvelope = null!;
    private float[] _slowEnvelope = null!;

    // Separated signals per channel
    private float[] _transientSignal = null!;
    private float[] _sustainSignal = null!;

    // Lookahead buffer
    private float[][] _lookaheadBuffer = null!;
    private int _lookaheadWritePos;
    private int _lookaheadSamples;

    // Multiband state
    private readonly TransientSplitterBand[] _bands;
    private float[][] _bandLpState = null!;
    private float[][] _bandLpState2 = null!;
    private float[][] _bandFastEnv = null!;
    private float[][] _bandSlowEnv = null!;
    private float[][][] _bandBuffer = null!;

    // Visualization data
    private readonly TransientVisualizationData _visualizationData = new();
    private float _peakDecay = 0.999f;

    // Detection filter state
    private float[] _detectionHpState = null!;
    private float[] _detectionLpState = null!;

    // Gate state
    private float[] _gateEnvelope = null!;

    // Export buffer
    private readonly List<float> _exportedTransients = new();
    private bool _isExporting;

    private bool _initialized;

    /// <summary>
    /// Creates a new transient splitter.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    public TransientSplitter(ISampleProvider source) : this(source, "Transient Splitter")
    {
    }

    /// <summary>
    /// Creates a new transient splitter with a custom name.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <param name="name">Effect name.</param>
    public TransientSplitter(ISampleProvider source, string name) : base(source, name)
    {
        // Initialize bands with default crossover frequencies
        _bands = new TransientSplitterBand[MaxBands]
        {
            new TransientSplitterBand(0, 20f, 200f),      // Sub/Bass
            new TransientSplitterBand(1, 200f, 2000f),    // Low-Mid
            new TransientSplitterBand(2, 2000f, 8000f),   // High-Mid
            new TransientSplitterBand(3, 8000f, 20000f)   // High
        };

        // Register parameters
        RegisterParameter("Sensitivity", 50f);           // 0-100: Transient detection threshold
        RegisterParameter("AttackTime", 0.5f);           // ms: How fast transients are detected
        RegisterParameter("SustainTime", 50f);           // ms: How long until sustain fully returns
        RegisterParameter("TransientGain", 0f);          // dB: Transient output gain
        RegisterParameter("SustainGain", 0f);            // dB: Sustain output gain
        RegisterParameter("TransientShape", 0f);         // -100 to +100: Transient shaping
        RegisterParameter("SustainShape", 0f);           // -100 to +100: Sustain shaping
        RegisterParameter("OutputMode", 0f);             // 0=Mixed, 1=TransientOnly, 2=SustainOnly, 3=Parallel
        RegisterParameter("FilterLow", 80f);             // Hz: Detection filter low frequency
        RegisterParameter("FilterHigh", 8000f);          // Hz: Detection filter high frequency
        RegisterParameter("MultibandEnabled", 0f);       // 0=Off, 1=On
        RegisterParameter("Punch", 50f);                 // 0-100: Transient emphasis
        RegisterParameter("Smoothness", 50f);            // 0-100: Sustain character
        RegisterParameter("LookaheadMs", 5f);            // ms: Lookahead for transient capture
        RegisterParameter("GateThreshold", -60f);        // dB: Gate transients below this level
        RegisterParameter("GateEnabled", 0f);            // 0=Off, 1=On
        RegisterParameter("FastRelease", 5f);            // ms: Fast envelope release
        RegisterParameter("SlowAttack", 20f);            // ms: Slow envelope attack
        RegisterParameter("SlowRelease", 200f);          // ms: Slow envelope release
        RegisterParameter("OutputGain", 0f);             // dB: Final output gain
        RegisterParameter("Mix", 1f);                    // Dry/Wet mix

        _initialized = false;
    }

    #region Properties

    /// <summary>
    /// Gets or sets the transient detection sensitivity (0-100).
    /// Higher values detect weaker transients.
    /// </summary>
    public float Sensitivity
    {
        get => GetParameter("Sensitivity");
        set => SetParameter("Sensitivity", Math.Clamp(value, 0f, 100f));
    }

    /// <summary>
    /// Gets or sets the attack time in milliseconds (0.1-10).
    /// How quickly transients are detected.
    /// </summary>
    public float AttackTime
    {
        get => GetParameter("AttackTime");
        set => SetParameter("AttackTime", Math.Clamp(value, 0.1f, 10f));
    }

    /// <summary>
    /// Gets or sets the sustain time in milliseconds (10-500).
    /// How long after a transient until sustain fully returns.
    /// </summary>
    public float SustainTime
    {
        get => GetParameter("SustainTime");
        set => SetParameter("SustainTime", Math.Clamp(value, 10f, 500f));
    }

    /// <summary>
    /// Gets or sets the transient output gain in dB (-24 to +24).
    /// </summary>
    public float TransientGain
    {
        get => GetParameter("TransientGain");
        set => SetParameter("TransientGain", Math.Clamp(value, -24f, 24f));
    }

    /// <summary>
    /// Gets or sets the sustain output gain in dB (-24 to +24).
    /// </summary>
    public float SustainGain
    {
        get => GetParameter("SustainGain");
        set => SetParameter("SustainGain", Math.Clamp(value, -24f, 24f));
    }

    /// <summary>
    /// Gets or sets the transient shaping amount (-100 to +100).
    /// Negative values soften transients, positive values emphasize them.
    /// </summary>
    public float TransientShape
    {
        get => GetParameter("TransientShape");
        set => SetParameter("TransientShape", Math.Clamp(value, -100f, 100f));
    }

    /// <summary>
    /// Gets or sets the sustain shaping amount (-100 to +100).
    /// Negative values reduce sustain, positive values enhance it.
    /// </summary>
    public float SustainShape
    {
        get => GetParameter("SustainShape");
        set => SetParameter("SustainShape", Math.Clamp(value, -100f, 100f));
    }

    /// <summary>
    /// Gets or sets the output mode.
    /// </summary>
    public TransientSplitterOutputMode OutputMode
    {
        get => (TransientSplitterOutputMode)(int)GetParameter("OutputMode");
        set => SetParameter("OutputMode", (float)(int)value);
    }

    /// <summary>
    /// Gets or sets the detection filter low frequency in Hz (20-2000).
    /// </summary>
    public float FilterLowFrequency
    {
        get => GetParameter("FilterLow");
        set => SetParameter("FilterLow", Math.Clamp(value, 20f, 2000f));
    }

    /// <summary>
    /// Gets or sets the detection filter high frequency in Hz (500-20000).
    /// </summary>
    public float FilterHighFrequency
    {
        get => GetParameter("FilterHigh");
        set => SetParameter("FilterHigh", Math.Clamp(value, 500f, 20000f));
    }

    /// <summary>
    /// Gets or sets whether multiband mode is enabled.
    /// </summary>
    public bool MultibandEnabled
    {
        get => GetParameter("MultibandEnabled") > 0.5f;
        set => SetParameter("MultibandEnabled", value ? 1f : 0f);
    }

    /// <summary>
    /// Gets or sets the punch control (0-100).
    /// Controls transient emphasis amount.
    /// </summary>
    public float Punch
    {
        get => GetParameter("Punch");
        set => SetParameter("Punch", Math.Clamp(value, 0f, 100f));
    }

    /// <summary>
    /// Gets or sets the smoothness control (0-100).
    /// Controls sustain character.
    /// </summary>
    public float Smoothness
    {
        get => GetParameter("Smoothness");
        set => SetParameter("Smoothness", Math.Clamp(value, 0f, 100f));
    }

    /// <summary>
    /// Gets or sets the lookahead time in milliseconds (0-20).
    /// </summary>
    public float LookaheadMs
    {
        get => GetParameter("LookaheadMs");
        set
        {
            float clamped = Math.Clamp(value, 0f, MaxLookaheadMs);
            SetParameter("LookaheadMs", clamped);
            UpdateLookahead();
        }
    }

    /// <summary>
    /// Gets or sets the gate threshold in dB (-80 to 0).
    /// Transients below this level are gated.
    /// </summary>
    public float GateThreshold
    {
        get => GetParameter("GateThreshold");
        set => SetParameter("GateThreshold", Math.Clamp(value, -80f, 0f));
    }

    /// <summary>
    /// Gets or sets whether the transient gate is enabled.
    /// </summary>
    public bool GateEnabled
    {
        get => GetParameter("GateEnabled") > 0.5f;
        set => SetParameter("GateEnabled", value ? 1f : 0f);
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
    /// Gets the band configurations.
    /// </summary>
    public IReadOnlyList<TransientSplitterBand> Bands => _bands;

    /// <summary>
    /// Gets the current visualization data.
    /// </summary>
    public TransientVisualizationData VisualizationData => _visualizationData;

    /// <summary>
    /// Gets the exported transient audio data.
    /// </summary>
    public IReadOnlyList<float> ExportedTransients => _exportedTransients;

    #endregion

    #region Public Methods

    /// <summary>
    /// Gets the band at the specified index.
    /// </summary>
    public TransientSplitterBand GetBand(int index)
    {
        if (index < 0 || index >= MaxBands)
            throw new ArgumentOutOfRangeException(nameof(index));
        return _bands[index];
    }

    /// <summary>
    /// Sets the crossover frequencies for multiband mode.
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
    /// Resets the transient splitter state.
    /// </summary>
    public void Reset()
    {
        if (!_initialized) return;

        Array.Clear(_fastEnvelope);
        Array.Clear(_slowEnvelope);
        Array.Clear(_transientSignal);
        Array.Clear(_sustainSignal);
        Array.Clear(_detectionHpState);
        Array.Clear(_detectionLpState);
        Array.Clear(_gateEnvelope);

        foreach (var buffer in _lookaheadBuffer)
        {
            Array.Clear(buffer);
        }
        _lookaheadWritePos = 0;

        for (int band = 0; band < MaxBands; band++)
        {
            Array.Clear(_bandFastEnv[band]);
            Array.Clear(_bandSlowEnv[band]);
        }

        _visualizationData.TransientLevel = 0;
        _visualizationData.SustainLevel = 0;
        _visualizationData.PeakTransientLevel = 0;
        _visualizationData.PeakSustainLevel = 0;
        _visualizationData.TransientDetected = false;
    }

    /// <summary>
    /// Starts exporting transients to the internal buffer.
    /// </summary>
    public void StartTransientExport()
    {
        _exportedTransients.Clear();
        _isExporting = true;
    }

    /// <summary>
    /// Stops exporting transients.
    /// </summary>
    public void StopTransientExport()
    {
        _isExporting = false;
    }

    /// <summary>
    /// Clears the exported transient buffer.
    /// </summary>
    public void ClearExportedTransients()
    {
        _exportedTransients.Clear();
    }

    /// <summary>
    /// Gets the transient signal for the current frame.
    /// </summary>
    public float[] GetTransientSignal()
    {
        return _transientSignal ?? Array.Empty<float>();
    }

    /// <summary>
    /// Gets the sustain signal for the current frame.
    /// </summary>
    public float[] GetSustainSignal()
    {
        return _sustainSignal ?? Array.Empty<float>();
    }

    /// <summary>
    /// Creates a TransientDesigner effect configured with matching settings.
    /// </summary>
    /// <param name="source">Audio source for the TransientDesigner.</param>
    /// <returns>A TransientDesigner with matching settings.</returns>
    public TransientDesigner CreateMatchingTransientDesigner(ISampleProvider source)
    {
        var designer = new TransientDesigner(source, $"{Name} - Designer");

        designer.Sensitivity = Sensitivity;
        designer.FastAttack = AttackTime;
        designer.FastRelease = FastRelease;
        designer.SlowAttack = SlowAttack;
        designer.SlowRelease = SlowRelease;
        designer.OutputGain = OutputGain;

        // Transfer band settings if multiband is enabled
        if (MultibandEnabled)
        {
            designer.SetCrossoverFrequencies(
                _bands[0].HighFrequency,
                _bands[1].HighFrequency,
                _bands[2].HighFrequency
            );

            for (int i = 0; i < MaxBands; i++)
            {
                var srcBand = _bands[i];
                var dstBand = designer.GetBand(i);
                dstBand.Attack = srcBand.TransientGain * 100f / 24f;  // Convert dB to percentage
                dstBand.Sustain = srcBand.SustainGain * 100f / 24f;
                dstBand.Enabled = srcBand.Enabled;
            }
        }

        return designer;
    }

    #endregion

    #region Presets

    /// <summary>
    /// Creates a preset for drum transient extraction.
    /// </summary>
    public static TransientSplitter CreateDrumTransientPreset(ISampleProvider source)
    {
        var splitter = new TransientSplitter(source, "Drum Transients");
        splitter.Sensitivity = 70f;
        splitter.AttackTime = 0.5f;
        splitter.SustainTime = 30f;
        splitter.TransientGain = 0f;
        splitter.SustainGain = -6f;
        splitter.Punch = 80f;
        splitter.FilterLowFrequency = 60f;
        splitter.FilterHighFrequency = 12000f;
        splitter.LookaheadMs = 5f;
        return splitter;
    }

    /// <summary>
    /// Creates a preset for isolating sustain/room sound.
    /// </summary>
    public static TransientSplitter CreateSustainIsolationPreset(ISampleProvider source)
    {
        var splitter = new TransientSplitter(source, "Sustain Isolation");
        splitter.Sensitivity = 60f;
        splitter.AttackTime = 1f;
        splitter.SustainTime = 100f;
        splitter.TransientGain = -24f;
        splitter.SustainGain = 3f;
        splitter.Smoothness = 80f;
        splitter.OutputMode = TransientSplitterOutputMode.SustainOnly;
        return splitter;
    }

    /// <summary>
    /// Creates a preset for parallel drum processing.
    /// </summary>
    public static TransientSplitter CreateParallelDrumPreset(ISampleProvider source)
    {
        var splitter = new TransientSplitter(source, "Parallel Drums");
        splitter.Sensitivity = 65f;
        splitter.AttackTime = 0.3f;
        splitter.SustainTime = 40f;
        splitter.TransientGain = 6f;
        splitter.SustainGain = 0f;
        splitter.Punch = 70f;
        splitter.Smoothness = 50f;
        splitter.OutputMode = TransientSplitterOutputMode.ParallelSplit;
        splitter.MultibandEnabled = true;

        // Configure bands for drums
        splitter.GetBand(0).TransientGain = 3f;   // Kick punch
        splitter.GetBand(1).TransientGain = 6f;   // Snare crack
        splitter.GetBand(2).TransientGain = 4f;   // Hi-hat attack
        splitter.GetBand(3).TransientGain = 2f;   // Air/cymbals

        return splitter;
    }

    /// <summary>
    /// Creates a preset for multiband processing.
    /// </summary>
    public static TransientSplitter CreateMultibandPreset(ISampleProvider source)
    {
        var splitter = new TransientSplitter(source, "Multiband Split");
        splitter.MultibandEnabled = true;
        splitter.Sensitivity = 55f;
        splitter.SetCrossoverFrequencies(150f, 2500f, 8000f);

        splitter.GetBand(0).TransientGain = 2f;
        splitter.GetBand(0).SustainGain = -3f;
        splitter.GetBand(0).Punch = 60f;

        splitter.GetBand(1).TransientGain = 4f;
        splitter.GetBand(1).SustainGain = -2f;
        splitter.GetBand(1).Punch = 70f;

        splitter.GetBand(2).TransientGain = 3f;
        splitter.GetBand(2).SustainGain = 0f;
        splitter.GetBand(2).Punch = 65f;

        splitter.GetBand(3).TransientGain = 1f;
        splitter.GetBand(3).SustainGain = 2f;
        splitter.GetBand(3).Punch = 50f;

        return splitter;
    }

    /// <summary>
    /// Creates a preset for gentle transient taming.
    /// </summary>
    public static TransientSplitter CreateTransientTamingPreset(ISampleProvider source)
    {
        var splitter = new TransientSplitter(source, "Transient Taming");
        splitter.Sensitivity = 45f;
        splitter.AttackTime = 1f;
        splitter.SustainTime = 80f;
        splitter.TransientGain = -6f;
        splitter.SustainGain = 0f;
        splitter.TransientShape = -30f;
        splitter.Smoothness = 70f;
        return splitter;
    }

    #endregion

    #region Private Methods

    private void Initialize()
    {
        int channels = Channels;

        _fastEnvelope = new float[channels];
        _slowEnvelope = new float[channels];
        _transientSignal = new float[channels];
        _sustainSignal = new float[channels];
        _detectionHpState = new float[channels];
        _detectionLpState = new float[channels];
        _gateEnvelope = new float[channels];

        // Initialize lookahead buffer
        int maxLookaheadSamples = (int)(MaxLookaheadMs * SampleRate / 1000f) + 1;
        _lookaheadBuffer = new float[channels][];
        for (int ch = 0; ch < channels; ch++)
        {
            _lookaheadBuffer[ch] = new float[maxLookaheadSamples];
        }
        UpdateLookahead();

        // Initialize multiband state
        _bandLpState = new float[channels][];
        _bandLpState2 = new float[channels][];
        for (int ch = 0; ch < channels; ch++)
        {
            _bandLpState[ch] = new float[MaxBands - 1];
            _bandLpState2[ch] = new float[MaxBands - 1];
        }

        _bandFastEnv = new float[MaxBands][];
        _bandSlowEnv = new float[MaxBands][];
        for (int band = 0; band < MaxBands; band++)
        {
            _bandFastEnv[band] = new float[channels];
            _bandSlowEnv[band] = new float[channels];
        }

        // Initialize visualization data
        _visualizationData.BandTransientLevels = new float[MaxBands];
        _visualizationData.BandSustainLevels = new float[MaxBands];

        _initialized = true;
    }

    private void UpdateLookahead()
    {
        if (!_initialized) return;
        _lookaheadSamples = (int)(LookaheadMs * SampleRate / 1000f);
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

        // Get parameters
        float sensitivity = Sensitivity / 100f;
        float attackTimeMs = AttackTime;
        float sustainTimeMs = SustainTime;
        float transientGainDb = TransientGain;
        float sustainGainDb = SustainGain;
        float transientShape = TransientShape / 100f;
        float sustainShape = SustainShape / 100f;
        var outputMode = OutputMode;
        float filterLow = FilterLowFrequency;
        float filterHigh = FilterHighFrequency;
        bool multiband = MultibandEnabled;
        float punch = Punch / 100f;
        float smoothness = Smoothness / 100f;
        float gateThresholdDb = GateThreshold;
        bool gateEnabled = GateEnabled;
        float fastReleaseMs = FastRelease;
        float slowAttackMs = SlowAttack;
        float slowReleaseMs = SlowRelease;
        float outputGainDb = OutputGain;

        // Calculate envelope coefficients
        float fastAttackCoef = MathF.Exp(-1f / (attackTimeMs * sampleRate / 1000f));
        float fastReleaseCoef = MathF.Exp(-1f / (fastReleaseMs * sampleRate / 1000f));
        float slowAttackCoef = MathF.Exp(-1f / (slowAttackMs * sampleRate / 1000f));
        float slowReleaseCoef = MathF.Exp(-1f / (slowReleaseMs * sampleRate / 1000f));
        float sustainBlendCoef = MathF.Exp(-1f / (sustainTimeMs * sampleRate / 1000f));

        // Convert gains to linear
        float transientGainLinear = DbToLinear(transientGainDb);
        float sustainGainLinear = DbToLinear(sustainGainDb);
        float outputGainLinear = DbToLinear(outputGainDb);
        float gateThresholdLinear = DbToLinear(gateThresholdDb);

        // Detection filter coefficients
        float hpCoef = MathF.Exp(-2f * MathF.PI * filterLow / sampleRate);
        float lpCoef = MathF.Exp(-2f * MathF.PI * filterHigh / sampleRate);

        // Gate coefficients
        float gateAttackCoef = MathF.Exp(-1f / (1f * sampleRate / 1000f));
        float gateReleaseCoef = MathF.Exp(-1f / (50f * sampleRate / 1000f));

        // Crossover filter coefficients for multiband
        float[] crossoverCoefs = new float[MaxBands - 1];
        if (multiband)
        {
            crossoverCoefs[0] = MathF.Exp(-2f * MathF.PI * _bands[0].HighFrequency / sampleRate);
            crossoverCoefs[1] = MathF.Exp(-2f * MathF.PI * _bands[1].HighFrequency / sampleRate);
            crossoverCoefs[2] = MathF.Exp(-2f * MathF.PI * _bands[2].HighFrequency / sampleRate);
        }

        // Ensure band buffer is allocated for multiband mode
        if (multiband && (_bandBuffer == null || _bandBuffer[0][0].Length < frames))
        {
            _bandBuffer = new float[MaxBands][][];
            for (int band = 0; band < MaxBands; band++)
            {
                _bandBuffer[band] = new float[channels][];
                for (int ch = 0; ch < channels; ch++)
                {
                    _bandBuffer[band][ch] = new float[frames];
                }
            }
        }

        // Visualization accumulators
        float vizTransientSum = 0f;
        float vizSustainSum = 0f;
        bool vizTransientDetected = false;
        float[] vizBandTransient = new float[MaxBands];
        float[] vizBandSustain = new float[MaxBands];

        if (multiband)
        {
            ProcessMultiband(sourceBuffer, destBuffer, offset, count, frames, channels,
                sensitivity, transientGainLinear, sustainGainLinear, transientShape, sustainShape,
                punch, smoothness, outputMode, outputGainLinear,
                fastAttackCoef, fastReleaseCoef, slowAttackCoef, slowReleaseCoef, sustainBlendCoef,
                crossoverCoefs, gateEnabled, gateThresholdLinear, gateAttackCoef, gateReleaseCoef,
                ref vizTransientSum, ref vizSustainSum, ref vizTransientDetected, vizBandTransient, vizBandSustain);
        }
        else
        {
            ProcessSingleband(sourceBuffer, destBuffer, offset, count, frames, channels,
                sensitivity, transientGainLinear, sustainGainLinear, transientShape, sustainShape,
                punch, smoothness, outputMode, outputGainLinear,
                fastAttackCoef, fastReleaseCoef, slowAttackCoef, slowReleaseCoef, sustainBlendCoef,
                hpCoef, lpCoef, gateEnabled, gateThresholdLinear, gateAttackCoef, gateReleaseCoef,
                ref vizTransientSum, ref vizSustainSum, ref vizTransientDetected);
        }

        // Update visualization data
        _visualizationData.TransientLevel = vizTransientSum / frames;
        _visualizationData.SustainLevel = vizSustainSum / frames;
        _visualizationData.TransientDetected = vizTransientDetected;

        // Decay peak levels
        _visualizationData.PeakTransientLevel = MathF.Max(
            _visualizationData.TransientLevel,
            _visualizationData.PeakTransientLevel * _peakDecay);
        _visualizationData.PeakSustainLevel = MathF.Max(
            _visualizationData.SustainLevel,
            _visualizationData.PeakSustainLevel * _peakDecay);

        if (multiband)
        {
            for (int band = 0; band < MaxBands; band++)
            {
                _visualizationData.BandTransientLevels[band] = vizBandTransient[band] / frames;
                _visualizationData.BandSustainLevels[band] = vizBandSustain[band] / frames;
            }
        }
    }

    private void ProcessSingleband(
        float[] sourceBuffer, float[] destBuffer, int offset, int count, int frames, int channels,
        float sensitivity, float transientGainLinear, float sustainGainLinear,
        float transientShape, float sustainShape, float punch, float smoothness,
        TransientSplitterOutputMode outputMode, float outputGainLinear,
        float fastAttackCoef, float fastReleaseCoef, float slowAttackCoef, float slowReleaseCoef,
        float sustainBlendCoef, float hpCoef, float lpCoef,
        bool gateEnabled, float gateThresholdLinear, float gateAttackCoef, float gateReleaseCoef,
        ref float vizTransientSum, ref float vizSustainSum, ref bool vizTransientDetected)
    {
        for (int frame = 0; frame < frames; frame++)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                int srcIdx = frame * channels + ch;
                int dstIdx = offset + srcIdx;

                float input = sourceBuffer[srcIdx];

                // Apply lookahead - write to buffer and read delayed
                float delayedInput = input;
                if (_lookaheadSamples > 0)
                {
                    int readPos = (_lookaheadWritePos - _lookaheadSamples + _lookaheadBuffer[ch].Length) % _lookaheadBuffer[ch].Length;
                    delayedInput = _lookaheadBuffer[ch][readPos];
                    _lookaheadBuffer[ch][_lookaheadWritePos] = input;
                }

                // Detection filtering (bandpass)
                _detectionHpState[ch] = input + hpCoef * (_detectionHpState[ch] - input);
                float hpFiltered = input - _detectionHpState[ch];
                _detectionLpState[ch] = _detectionLpState[ch] * lpCoef + hpFiltered * (1f - lpCoef);
                float filteredInput = _detectionLpState[ch];

                float inputAbs = MathF.Abs(filteredInput);

                // Update fast envelope (transient detection)
                float fastCoef = inputAbs > _fastEnvelope[ch] ? fastAttackCoef : fastReleaseCoef;
                _fastEnvelope[ch] = inputAbs + fastCoef * (_fastEnvelope[ch] - inputAbs);

                // Update slow envelope (sustain detection)
                float slowCoef = inputAbs > _slowEnvelope[ch] ? slowAttackCoef : slowReleaseCoef;
                _slowEnvelope[ch] = inputAbs + slowCoef * (_slowEnvelope[ch] - inputAbs);

                // Calculate transient/sustain separation
                float envelopeDiff = _fastEnvelope[ch] - _slowEnvelope[ch];
                float signalLevel = MathF.Max(_slowEnvelope[ch], 1e-6f);
                float normalizedDiff = envelopeDiff / signalLevel;

                // Apply sensitivity and punch
                float transientAmount = MathF.Max(0f, normalizedDiff * (0.1f + sensitivity * 0.9f));
                transientAmount = MathF.Min(transientAmount, 1f);
                transientAmount = MathF.Pow(transientAmount, 1f - punch * 0.5f); // Punch shapes the curve

                // Apply smoothness to sustain
                float sustainAmount = 1f - transientAmount;
                sustainAmount = MathF.Pow(sustainAmount, 1f - smoothness * 0.5f);

                // Normalize
                float total = transientAmount + sustainAmount;
                if (total > 0)
                {
                    transientAmount /= total;
                    sustainAmount /= total;
                }
                else
                {
                    transientAmount = 0f;
                    sustainAmount = 1f;
                }

                // Apply transient/sustain shaping
                float transientMult = 1f + transientShape;
                float sustainMult = 1f + sustainShape;

                // Separate signals
                float transientSig = delayedInput * transientAmount * transientMult * transientGainLinear;
                float sustainSig = delayedInput * sustainAmount * sustainMult * sustainGainLinear;

                // Apply gate to transients if enabled
                if (gateEnabled)
                {
                    float transientLevel = MathF.Abs(transientSig);
                    float targetGate = transientLevel > gateThresholdLinear ? 1f : 0f;
                    float gateCoef = targetGate > _gateEnvelope[ch] ? gateAttackCoef : gateReleaseCoef;
                    _gateEnvelope[ch] = targetGate + gateCoef * (_gateEnvelope[ch] - targetGate);
                    transientSig *= _gateEnvelope[ch];
                }

                // Store for visualization
                _transientSignal[ch] = transientSig;
                _sustainSignal[ch] = sustainSig;

                // Export transients if enabled
                if (_isExporting && ch == 0)
                {
                    _exportedTransients.Add(transientSig);
                }

                // Output based on mode
                float output;
                switch (outputMode)
                {
                    case TransientSplitterOutputMode.TransientOnly:
                        output = transientSig;
                        break;
                    case TransientSplitterOutputMode.SustainOnly:
                        output = sustainSig;
                        break;
                    case TransientSplitterOutputMode.ParallelSplit:
                        // Left = transient, Right = sustain (or interleaved)
                        output = (ch % 2 == 0) ? transientSig : sustainSig;
                        break;
                    case TransientSplitterOutputMode.Mixed:
                    default:
                        output = transientSig + sustainSig;
                        break;
                }

                destBuffer[dstIdx] = output * outputGainLinear;

                // Accumulate visualization data
                vizTransientSum += MathF.Abs(transientSig);
                vizSustainSum += MathF.Abs(sustainSig);
                if (transientAmount > 0.5f)
                    vizTransientDetected = true;
            }

            // Update lookahead write position (once per frame, same for all channels)
            if (_lookaheadSamples > 0)
            {
                _lookaheadWritePos = (_lookaheadWritePos + 1) % _lookaheadBuffer[0].Length;
            }
        }
    }

    private void ProcessMultiband(
        float[] sourceBuffer, float[] destBuffer, int offset, int count, int frames, int channels,
        float sensitivity, float transientGainLinear, float sustainGainLinear,
        float transientShape, float sustainShape, float punch, float smoothness,
        TransientSplitterOutputMode outputMode, float outputGainLinear,
        float fastAttackCoef, float fastReleaseCoef, float slowAttackCoef, float slowReleaseCoef,
        float sustainBlendCoef, float[] crossoverCoefs,
        bool gateEnabled, float gateThresholdLinear, float gateAttackCoef, float gateReleaseCoef,
        ref float vizTransientSum, ref float vizSustainSum, ref bool vizTransientDetected,
        float[] vizBandTransient, float[] vizBandSustain)
    {
        // First pass: split into bands
        for (int frame = 0; frame < frames; frame++)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                int srcIdx = frame * channels + ch;
                float input = sourceBuffer[srcIdx];

                // Apply lookahead
                float delayedInput = input;
                if (_lookaheadSamples > 0)
                {
                    int readPos = (_lookaheadWritePos - _lookaheadSamples + _lookaheadBuffer[ch].Length) % _lookaheadBuffer[ch].Length;
                    delayedInput = _lookaheadBuffer[ch][readPos];
                    _lookaheadBuffer[ch][_lookaheadWritePos] = input;
                }

                // Crossover filtering (Linkwitz-Riley style)
                _bandLpState[ch][0] = _bandLpState[ch][0] * crossoverCoefs[0] + delayedInput * (1f - crossoverCoefs[0]);
                _bandLpState2[ch][0] = _bandLpState2[ch][0] * crossoverCoefs[0] + _bandLpState[ch][0] * (1f - crossoverCoefs[0]);
                float band0 = _bandLpState2[ch][0];

                float hp1 = delayedInput - band0;
                _bandLpState[ch][1] = _bandLpState[ch][1] * crossoverCoefs[1] + hp1 * (1f - crossoverCoefs[1]);
                _bandLpState2[ch][1] = _bandLpState2[ch][1] * crossoverCoefs[1] + _bandLpState[ch][1] * (1f - crossoverCoefs[1]);
                float band1 = _bandLpState2[ch][1];

                float hp2 = hp1 - band1;
                _bandLpState[ch][2] = _bandLpState[ch][2] * crossoverCoefs[2] + hp2 * (1f - crossoverCoefs[2]);
                _bandLpState2[ch][2] = _bandLpState2[ch][2] * crossoverCoefs[2] + _bandLpState[ch][2] * (1f - crossoverCoefs[2]);
                float band2 = _bandLpState2[ch][2];

                float band3 = hp2 - band2;

                _bandBuffer[0][ch][frame] = band0;
                _bandBuffer[1][ch][frame] = band1;
                _bandBuffer[2][ch][frame] = band2;
                _bandBuffer[3][ch][frame] = band3;
            }

            if (_lookaheadSamples > 0)
            {
                _lookaheadWritePos = (_lookaheadWritePos + 1) % _lookaheadBuffer[0].Length;
            }
        }

        // Second pass: process each band's transient/sustain
        for (int frame = 0; frame < frames; frame++)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                float totalTransient = 0f;
                float totalSustain = 0f;

                for (int band = 0; band < MaxBands; band++)
                {
                    var bandConfig = _bands[band];
                    if (!bandConfig.Enabled)
                    {
                        // Pass through unchanged
                        totalTransient += _bandBuffer[band][ch][frame] * 0.5f;
                        totalSustain += _bandBuffer[band][ch][frame] * 0.5f;
                        continue;
                    }

                    float sample = _bandBuffer[band][ch][frame];
                    float sampleAbs = MathF.Abs(sample);

                    // Update envelopes per band
                    float fastCoef = sampleAbs > _bandFastEnv[band][ch] ? fastAttackCoef : fastReleaseCoef;
                    _bandFastEnv[band][ch] = sampleAbs + fastCoef * (_bandFastEnv[band][ch] - sampleAbs);

                    float slowCoef = sampleAbs > _bandSlowEnv[band][ch] ? slowAttackCoef : slowReleaseCoef;
                    _bandSlowEnv[band][ch] = sampleAbs + slowCoef * (_bandSlowEnv[band][ch] - sampleAbs);

                    // Calculate transient amount
                    float envDiff = _bandFastEnv[band][ch] - _bandSlowEnv[band][ch];
                    float sigLevel = MathF.Max(_bandSlowEnv[band][ch], 1e-6f);
                    float normDiff = envDiff / sigLevel;

                    float bandPunch = bandConfig.Punch / 100f;
                    float bandSmoothness = bandConfig.Smoothness / 100f;

                    float transAmt = MathF.Max(0f, normDiff * (0.1f + sensitivity * 0.9f));
                    transAmt = MathF.Min(transAmt, 1f);
                    transAmt = MathF.Pow(transAmt, 1f - bandPunch * 0.5f);

                    float sustAmt = 1f - transAmt;
                    sustAmt = MathF.Pow(sustAmt, 1f - bandSmoothness * 0.5f);

                    // Normalize
                    float tot = transAmt + sustAmt;
                    if (tot > 0)
                    {
                        transAmt /= tot;
                        sustAmt /= tot;
                    }

                    // Apply per-band gains
                    float bandTransGain = DbToLinear(bandConfig.TransientGain);
                    float bandSustGain = DbToLinear(bandConfig.SustainGain);

                    float bandTransSig = sample * transAmt * (1f + transientShape) * bandTransGain;
                    float bandSustSig = sample * sustAmt * (1f + sustainShape) * bandSustGain;

                    totalTransient += bandTransSig;
                    totalSustain += bandSustSig;

                    // Visualization
                    vizBandTransient[band] += MathF.Abs(bandTransSig);
                    vizBandSustain[band] += MathF.Abs(bandSustSig);

                    if (transAmt > 0.5f)
                        vizTransientDetected = true;
                }

                // Apply global gains
                totalTransient *= transientGainLinear;
                totalSustain *= sustainGainLinear;

                // Apply gate if enabled
                if (gateEnabled)
                {
                    float transLevel = MathF.Abs(totalTransient);
                    float targetGate = transLevel > gateThresholdLinear ? 1f : 0f;
                    float gateCoef = targetGate > _gateEnvelope[ch] ? gateAttackCoef : gateReleaseCoef;
                    _gateEnvelope[ch] = targetGate + gateCoef * (_gateEnvelope[ch] - targetGate);
                    totalTransient *= _gateEnvelope[ch];
                }

                _transientSignal[ch] = totalTransient;
                _sustainSignal[ch] = totalSustain;

                if (_isExporting && ch == 0)
                {
                    _exportedTransients.Add(totalTransient);
                }

                // Output based on mode
                float output;
                switch (outputMode)
                {
                    case TransientSplitterOutputMode.TransientOnly:
                        output = totalTransient;
                        break;
                    case TransientSplitterOutputMode.SustainOnly:
                        output = totalSustain;
                        break;
                    case TransientSplitterOutputMode.ParallelSplit:
                        output = (ch % 2 == 0) ? totalTransient : totalSustain;
                        break;
                    case TransientSplitterOutputMode.Mixed:
                    default:
                        output = totalTransient + totalSustain;
                        break;
                }

                int dstIdx = offset + frame * channels + ch;
                destBuffer[dstIdx] = output * outputGainLinear;

                vizTransientSum += MathF.Abs(totalTransient);
                vizSustainSum += MathF.Abs(totalSustain);
            }
        }
    }

    private static float DbToLinear(float db) => MathF.Pow(10f, db / 20f);

    #endregion
}
