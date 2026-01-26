//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Comprehensive Mid/Side encoding, decoding, and processing utility with
//              independent M/S manipulation, filtering, effect slots, metering, and preset management.

using System.Text.Json;
using System.Text.Json.Serialization;
using MusicEngine.Core;
using NAudio.Wave;

namespace MusicEngine.Core.Utilities;

/// <summary>
/// Processing mode for Mid/Side operations.
/// </summary>
public enum MidSideProcessingMode
{
    /// <summary>Normal stereo input, M/S processing, stereo output.</summary>
    Normal,
    /// <summary>Output only the mid channel (mono sum).</summary>
    MidSolo,
    /// <summary>Output only the side channel (stereo difference).</summary>
    SideSolo,
    /// <summary>Output M/S encoded signal (left=mid, right=side).</summary>
    EncodeOnly,
    /// <summary>Input is M/S encoded, decode to stereo.</summary>
    DecodeOnly,
    /// <summary>Bypass all processing.</summary>
    Bypass
}

/// <summary>
/// Auto-width mode for frequency-dependent stereo width.
/// </summary>
public enum AutoWidthMode
{
    /// <summary>Auto-width disabled.</summary>
    Off,
    /// <summary>Reduce width at low frequencies only.</summary>
    LowOnly,
    /// <summary>Increase width at high frequencies only.</summary>
    HighOnly,
    /// <summary>Full frequency-dependent width control.</summary>
    Full
}

/// <summary>
/// Transient focus mode for controlling mid/side transient balance.
/// </summary>
public enum TransientFocusMode
{
    /// <summary>No transient focus processing.</summary>
    Off,
    /// <summary>Emphasize transients in mid channel.</summary>
    MidTransients,
    /// <summary>Emphasize transients in side channel.</summary>
    SideTransients,
    /// <summary>Balance transients between mid and side.</summary>
    Balanced
}

/// <summary>
/// Delegate for external effect processing on mid or side channel.
/// </summary>
/// <param name="samples">The samples to process (mono buffer).</param>
/// <param name="sampleCount">Number of samples to process.</param>
public delegate void MidSideEffectProcessor(float[] samples, int sampleCount);

/// <summary>
/// Metering data for Mid/Side processor output.
/// </summary>
public class MidSideMeteringData
{
    /// <summary>Mid channel peak level in dB.</summary>
    public float MidPeakDb { get; set; } = -60f;

    /// <summary>Side channel peak level in dB.</summary>
    public float SidePeakDb { get; set; } = -60f;

    /// <summary>Mid channel RMS level in dB.</summary>
    public float MidRmsDb { get; set; } = -60f;

    /// <summary>Side channel RMS level in dB.</summary>
    public float SideRmsDb { get; set; } = -60f;

    /// <summary>Left output peak level in dB.</summary>
    public float OutputLeftPeakDb { get; set; } = -60f;

    /// <summary>Right output peak level in dB.</summary>
    public float OutputRightPeakDb { get; set; } = -60f;

    /// <summary>Left output RMS level in dB.</summary>
    public float OutputLeftRmsDb { get; set; } = -60f;

    /// <summary>Right output RMS level in dB.</summary>
    public float OutputRightRmsDb { get; set; } = -60f;

    /// <summary>Stereo correlation (-1 to +1). +1=mono, 0=uncorrelated, -1=out of phase.</summary>
    public float Correlation { get; set; } = 1f;

    /// <summary>M/S balance (-1=full side, 0=balanced, +1=full mid).</summary>
    public float MidSideBalance { get; set; } = 0f;

    /// <summary>True if the signal is mono-compatible (correlation > 0).</summary>
    public bool IsMonoCompatible { get; set; } = true;

    /// <summary>Instantaneous stereo width (0=mono, 1=normal, >1=wide).</summary>
    public float InstantWidth { get; set; } = 1f;
}

/// <summary>
/// Preset data for Mid/Side processor settings.
/// </summary>
public class MidSidePreset
{
    /// <summary>Preset name.</summary>
    public string Name { get; set; } = "Default";

    /// <summary>Preset author.</summary>
    public string Author { get; set; } = "";

    /// <summary>Preset description.</summary>
    public string Description { get; set; } = "";

    /// <summary>Category for organization.</summary>
    public string Category { get; set; } = "General";

    /// <summary>Creation date.</summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Last modified date.</summary>
    public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;

    // All processor parameters
    public float MidLevel { get; set; } = 0f;
    public float SideLevel { get; set; } = 0f;
    public float Width { get; set; } = 1f;
    public float MidSideBalanceParam { get; set; } = 0f;
    public bool MidMute { get; set; } = false;
    public bool SideMute { get; set; } = false;
    public bool MidSolo { get; set; } = false;
    public bool SideSolo { get; set; } = false;
    public bool MidPhaseInvert { get; set; } = false;
    public bool SidePhaseInvert { get; set; } = false;
    public float MonoMakerFrequency { get; set; } = 0f;
    public float SideHighPassFrequency { get; set; } = 20f;
    public float SideLowPassFrequency { get; set; } = 20000f;
    public float MidHighPassFrequency { get; set; } = 20f;
    public float MidLowPassFrequency { get; set; } = 20000f;
    public float OutputGain { get; set; } = 0f;
    public AutoWidthMode AutoWidth { get; set; } = AutoWidthMode.Off;
    public float AutoWidthLowFreq { get; set; } = 150f;
    public float AutoWidthHighFreq { get; set; } = 8000f;
    public float AutoWidthLowAmount { get; set; } = 0f;
    public float AutoWidthHighAmount { get; set; } = 1f;
    public TransientFocusMode TransientFocus { get; set; } = TransientFocusMode.Off;
    public float TransientFocusAmount { get; set; } = 50f;
    public MidSideProcessingMode ProcessingMode { get; set; } = MidSideProcessingMode.Normal;

    /// <summary>
    /// Serializes the preset to JSON.
    /// </summary>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        });
    }

    /// <summary>
    /// Deserializes a preset from JSON.
    /// </summary>
    public static MidSidePreset? FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<MidSidePreset>(json, new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() }
            });
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Comprehensive Mid/Side stereo processing utility.
/// </summary>
/// <remarks>
/// Features:
/// - Stereo to Mid/Side encoding and decoding
/// - Independent mid and side level adjustment
/// - Mid/Side solo, mute, and phase invert
/// - Stereo width control via side level
/// - Mid/Side balance control
/// - Mono maker with frequency threshold
/// - Side high-pass and low-pass filtering
/// - Mid high-pass and low-pass filtering
/// - Effect processing slots for mid and side channels
/// - Mid EQ / Side EQ integration points
/// - Mid compression / Side compression integration points
/// - Stereo correlation metering
/// - Mid/Side level metering
/// - Output level metering
/// - Auto-width (frequency-dependent width)
/// - Mono compatibility checking
/// - Transient focus (mid vs side transient balance)
/// - Preset management with save/load
/// </remarks>
public class MidSideProcessor : EffectBase
{
    // Filter states for mid channel
    private float _midHpState1;
    private float _midHpState2;
    private float _midLpState1;
    private float _midLpState2;

    // Filter states for side channel
    private float _sideHpState1;
    private float _sideHpState2;
    private float _sideLpState1;
    private float _sideLpState2;

    // Mono maker filter states
    private float _monoMakerLpStateL;
    private float _monoMakerLpStateR;

    // Auto-width crossover filter states
    private float _autoWidthLowLpStateL;
    private float _autoWidthLowLpStateR;
    private float _autoWidthHighHpStateL;
    private float _autoWidthHighHpStateR;

    // Transient detection envelope states
    private float _midFastEnv;
    private float _midSlowEnv;
    private float _sideFastEnv;
    private float _sideSlowEnv;

    // Metering accumulators
    private float _midPeakAcc;
    private float _sidePeakAcc;
    private float _midRmsAcc;
    private float _sideRmsAcc;
    private float _outputLeftPeakAcc;
    private float _outputRightPeakAcc;
    private float _outputLeftRmsAcc;
    private float _outputRightRmsAcc;
    private float _correlationSumAcc;
    private float _powerLeftAcc;
    private float _powerRightAcc;
    private int _meteringSampleCount;
    private const int MeteringUpdateInterval = 4410; // ~10 updates per second at 44.1kHz

    // Metering decay
    private const float PeakDecayPerSample = 0.9999f;
    private const float RmsDecayPerSample = 0.999f;

    // Processing buffers for effect slots
    private float[] _midBuffer = Array.Empty<float>();
    private float[] _sideBuffer = Array.Empty<float>();

    // Effect processor slots
    private readonly List<MidSideEffectProcessor> _midEffects = new();
    private readonly List<MidSideEffectProcessor> _sideEffects = new();

    // Preset management
    private readonly List<MidSidePreset> _presets = new();
    private MidSidePreset? _currentPreset;

    /// <summary>
    /// Creates a new Mid/Side processor.
    /// </summary>
    /// <param name="source">Stereo audio source to process.</param>
    public MidSideProcessor(ISampleProvider source) : this(source, "Mid/Side Processor")
    {
    }

    /// <summary>
    /// Creates a new Mid/Side processor with a custom name.
    /// </summary>
    /// <param name="source">Stereo audio source to process.</param>
    /// <param name="name">Effect name.</param>
    public MidSideProcessor(ISampleProvider source, string name) : base(source, name)
    {
        if (source.WaveFormat.Channels != 2)
        {
            throw new ArgumentException("Source must be stereo (2 channels)", nameof(source));
        }

        // Register all parameters with defaults
        RegisterParameter("MidLevel", 0f);
        RegisterParameter("SideLevel", 0f);
        RegisterParameter("Width", 1f);
        RegisterParameter("MidSideBalance", 0f);
        RegisterParameter("MidMute", 0f);
        RegisterParameter("SideMute", 0f);
        RegisterParameter("MidSolo", 0f);
        RegisterParameter("SideSolo", 0f);
        RegisterParameter("MidPhaseInvert", 0f);
        RegisterParameter("SidePhaseInvert", 0f);
        RegisterParameter("MonoMakerFreq", 0f);
        RegisterParameter("SideHighPass", 20f);
        RegisterParameter("SideLowPass", 20000f);
        RegisterParameter("MidHighPass", 20f);
        RegisterParameter("MidLowPass", 20000f);
        RegisterParameter("OutputGain", 0f);
        RegisterParameter("AutoWidthMode", (float)AutoWidthMode.Off);
        RegisterParameter("AutoWidthLowFreq", 150f);
        RegisterParameter("AutoWidthHighFreq", 8000f);
        RegisterParameter("AutoWidthLowAmount", 0f);
        RegisterParameter("AutoWidthHighAmount", 1f);
        RegisterParameter("TransientFocusMode", (float)TransientFocusMode.Off);
        RegisterParameter("TransientFocusAmount", 50f);
        RegisterParameter("ProcessingMode", (float)MidSideProcessingMode.Normal);
        RegisterParameter("Mix", 1f);

        // Initialize metering data
        Metering = new MidSideMeteringData();

        // Load factory presets
        InitializeFactoryPresets();
    }

    #region Properties

    /// <summary>
    /// Gets the current metering data.
    /// </summary>
    public MidSideMeteringData Metering { get; }

    /// <summary>
    /// Gets the list of available presets.
    /// </summary>
    public IReadOnlyList<MidSidePreset> Presets => _presets.AsReadOnly();

    /// <summary>
    /// Gets or sets the current preset.
    /// </summary>
    public MidSidePreset? CurrentPreset
    {
        get => _currentPreset;
        set
        {
            if (value != null)
            {
                ApplyPreset(value);
            }
            _currentPreset = value;
        }
    }

    /// <summary>
    /// Gets or sets the mid channel level in dB (-60 to +24).
    /// </summary>
    public float MidLevel
    {
        get => GetParameter("MidLevel");
        set => SetParameter("MidLevel", Math.Clamp(value, -60f, 24f));
    }

    /// <summary>
    /// Gets or sets the side channel level in dB (-60 to +24).
    /// </summary>
    public float SideLevel
    {
        get => GetParameter("SideLevel");
        set => SetParameter("SideLevel", Math.Clamp(value, -60f, 24f));
    }

    /// <summary>
    /// Gets or sets the stereo width (0=mono, 1=normal, 2=extra wide).
    /// This is a convenience property that adjusts SideLevel.
    /// </summary>
    public float Width
    {
        get => GetParameter("Width");
        set => SetParameter("Width", Math.Clamp(value, 0f, 2f));
    }

    /// <summary>
    /// Gets or sets the Mid/Side balance (-1=full side, 0=balanced, +1=full mid).
    /// </summary>
    public float MidSideBalance
    {
        get => GetParameter("MidSideBalance");
        set => SetParameter("MidSideBalance", Math.Clamp(value, -1f, 1f));
    }

    /// <summary>
    /// Gets or sets whether the mid channel is muted.
    /// </summary>
    public bool MidMute
    {
        get => GetParameter("MidMute") > 0.5f;
        set => SetParameter("MidMute", value ? 1f : 0f);
    }

    /// <summary>
    /// Gets or sets whether the side channel is muted.
    /// </summary>
    public bool SideMute
    {
        get => GetParameter("SideMute") > 0.5f;
        set => SetParameter("SideMute", value ? 1f : 0f);
    }

    /// <summary>
    /// Gets or sets whether the mid channel is soloed (outputs mono).
    /// </summary>
    public bool MidSolo
    {
        get => GetParameter("MidSolo") > 0.5f;
        set => SetParameter("MidSolo", value ? 1f : 0f);
    }

    /// <summary>
    /// Gets or sets whether the side channel is soloed.
    /// </summary>
    public bool SideSolo
    {
        get => GetParameter("SideSolo") > 0.5f;
        set => SetParameter("SideSolo", value ? 1f : 0f);
    }

    /// <summary>
    /// Gets or sets whether the mid channel phase is inverted.
    /// </summary>
    public bool MidPhaseInvert
    {
        get => GetParameter("MidPhaseInvert") > 0.5f;
        set => SetParameter("MidPhaseInvert", value ? 1f : 0f);
    }

    /// <summary>
    /// Gets or sets whether the side channel phase is inverted.
    /// </summary>
    public bool SidePhaseInvert
    {
        get => GetParameter("SidePhaseInvert") > 0.5f;
        set => SetParameter("SidePhaseInvert", value ? 1f : 0f);
    }

    /// <summary>
    /// Gets or sets the mono maker frequency threshold in Hz (0=disabled, 20-500).
    /// Frequencies below this are summed to mono for better bass mono compatibility.
    /// </summary>
    public float MonoMakerFrequency
    {
        get => GetParameter("MonoMakerFreq");
        set => SetParameter("MonoMakerFreq", Math.Clamp(value, 0f, 500f));
    }

    /// <summary>
    /// Gets or sets the side channel high-pass filter frequency in Hz (20-2000).
    /// </summary>
    public float SideHighPassFrequency
    {
        get => GetParameter("SideHighPass");
        set => SetParameter("SideHighPass", Math.Clamp(value, 20f, 2000f));
    }

    /// <summary>
    /// Gets or sets the side channel low-pass filter frequency in Hz (1000-20000).
    /// </summary>
    public float SideLowPassFrequency
    {
        get => GetParameter("SideLowPass");
        set => SetParameter("SideLowPass", Math.Clamp(value, 1000f, 20000f));
    }

    /// <summary>
    /// Gets or sets the mid channel high-pass filter frequency in Hz (20-2000).
    /// </summary>
    public float MidHighPassFrequency
    {
        get => GetParameter("MidHighPass");
        set => SetParameter("MidHighPass", Math.Clamp(value, 20f, 2000f));
    }

    /// <summary>
    /// Gets or sets the mid channel low-pass filter frequency in Hz (1000-20000).
    /// </summary>
    public float MidLowPassFrequency
    {
        get => GetParameter("MidLowPass");
        set => SetParameter("MidLowPass", Math.Clamp(value, 1000f, 20000f));
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
    /// Gets or sets the auto-width mode.
    /// </summary>
    public AutoWidthMode AutoWidthSetting
    {
        get => (AutoWidthMode)GetParameter("AutoWidthMode");
        set => SetParameter("AutoWidthMode", (float)value);
    }

    /// <summary>
    /// Gets or sets the auto-width low frequency threshold in Hz (50-500).
    /// </summary>
    public float AutoWidthLowFrequency
    {
        get => GetParameter("AutoWidthLowFreq");
        set => SetParameter("AutoWidthLowFreq", Math.Clamp(value, 50f, 500f));
    }

    /// <summary>
    /// Gets or sets the auto-width high frequency threshold in Hz (2000-16000).
    /// </summary>
    public float AutoWidthHighFrequency
    {
        get => GetParameter("AutoWidthHighFreq");
        set => SetParameter("AutoWidthHighFreq", Math.Clamp(value, 2000f, 16000f));
    }

    /// <summary>
    /// Gets or sets the auto-width amount for low frequencies (0-1).
    /// 0 = full mono at low frequencies, 1 = no change.
    /// </summary>
    public float AutoWidthLowAmount
    {
        get => GetParameter("AutoWidthLowAmount");
        set => SetParameter("AutoWidthLowAmount", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Gets or sets the auto-width amount for high frequencies (0-2).
    /// 1 = normal width, 2 = extra wide at high frequencies.
    /// </summary>
    public float AutoWidthHighAmount
    {
        get => GetParameter("AutoWidthHighAmount");
        set => SetParameter("AutoWidthHighAmount", Math.Clamp(value, 0f, 2f));
    }

    /// <summary>
    /// Gets or sets the transient focus mode.
    /// </summary>
    public TransientFocusMode TransientFocus
    {
        get => (TransientFocusMode)GetParameter("TransientFocusMode");
        set => SetParameter("TransientFocusMode", (float)value);
    }

    /// <summary>
    /// Gets or sets the transient focus amount (0-100).
    /// </summary>
    public float TransientFocusAmount
    {
        get => GetParameter("TransientFocusAmount");
        set => SetParameter("TransientFocusAmount", Math.Clamp(value, 0f, 100f));
    }

    /// <summary>
    /// Gets or sets the processing mode.
    /// </summary>
    public MidSideProcessingMode ProcessingMode
    {
        get => (MidSideProcessingMode)GetParameter("ProcessingMode");
        set => SetParameter("ProcessingMode", (float)value);
    }

    #endregion

    #region Effect Slots

    /// <summary>
    /// Adds an effect processor to the mid channel processing chain.
    /// </summary>
    /// <param name="processor">The effect processor delegate.</param>
    public void AddMidEffect(MidSideEffectProcessor processor)
    {
        ArgumentNullException.ThrowIfNull(processor);
        _midEffects.Add(processor);
    }

    /// <summary>
    /// Removes an effect processor from the mid channel processing chain.
    /// </summary>
    /// <param name="processor">The effect processor delegate.</param>
    public void RemoveMidEffect(MidSideEffectProcessor processor)
    {
        _midEffects.Remove(processor);
    }

    /// <summary>
    /// Clears all mid channel effect processors.
    /// </summary>
    public void ClearMidEffects()
    {
        _midEffects.Clear();
    }

    /// <summary>
    /// Adds an effect processor to the side channel processing chain.
    /// </summary>
    /// <param name="processor">The effect processor delegate.</param>
    public void AddSideEffect(MidSideEffectProcessor processor)
    {
        ArgumentNullException.ThrowIfNull(processor);
        _sideEffects.Add(processor);
    }

    /// <summary>
    /// Removes an effect processor from the side channel processing chain.
    /// </summary>
    /// <param name="processor">The effect processor delegate.</param>
    public void RemoveSideEffect(MidSideEffectProcessor processor)
    {
        _sideEffects.Remove(processor);
    }

    /// <summary>
    /// Clears all side channel effect processors.
    /// </summary>
    public void ClearSideEffects()
    {
        _sideEffects.Clear();
    }

    /// <summary>
    /// Gets the number of mid channel effects.
    /// </summary>
    public int MidEffectCount => _midEffects.Count;

    /// <summary>
    /// Gets the number of side channel effects.
    /// </summary>
    public int SideEffectCount => _sideEffects.Count;

    #endregion

    #region Processing

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        var mode = ProcessingMode;

        // Bypass mode - just copy
        if (mode == MidSideProcessingMode.Bypass)
        {
            Array.Copy(sourceBuffer, 0, destBuffer, offset, count);
            return;
        }

        int sampleRate = SampleRate;
        int frames = count / 2;

        // Ensure processing buffers are large enough
        if (_midBuffer.Length < frames)
        {
            _midBuffer = new float[frames];
            _sideBuffer = new float[frames];
        }

        // Get parameters
        float midLevelLinear = DbToLinear(MidLevel);
        float sideLevelLinear = DbToLinear(SideLevel);
        float width = Width;
        float msBalance = MidSideBalance;
        bool midMute = MidMute;
        bool sideMute = SideMute;
        bool midSolo = MidSolo;
        bool sideSolo = SideSolo;
        bool midPhaseInvert = MidPhaseInvert;
        bool sidePhaseInvert = SidePhaseInvert;
        float monoMakerFreq = MonoMakerFrequency;
        float sideHpFreq = SideHighPassFrequency;
        float sideLpFreq = SideLowPassFrequency;
        float midHpFreq = MidHighPassFrequency;
        float midLpFreq = MidLowPassFrequency;
        float outputGainLinear = DbToLinear(OutputGain);
        var autoWidthMode = AutoWidthSetting;
        float autoWidthLowFreq = AutoWidthLowFrequency;
        float autoWidthHighFreq = AutoWidthHighFrequency;
        float autoWidthLowAmt = AutoWidthLowAmount;
        float autoWidthHighAmt = AutoWidthHighAmount;
        var transientMode = TransientFocus;
        float transientAmount = TransientFocusAmount / 100f;

        // Calculate filter coefficients (2nd order for smoother response)
        float monoMakerCoef = monoMakerFreq > 0 ? CalculateFilterCoef(monoMakerFreq, sampleRate) : 0f;
        float sideHpCoef = CalculateFilterCoef(sideHpFreq, sampleRate);
        float sideLpCoef = CalculateFilterCoef(sideLpFreq, sampleRate);
        float midHpCoef = CalculateFilterCoef(midHpFreq, sampleRate);
        float midLpCoef = CalculateFilterCoef(midLpFreq, sampleRate);
        float autoWidthLowCoef = CalculateFilterCoef(autoWidthLowFreq, sampleRate);
        float autoWidthHighCoef = CalculateFilterCoef(autoWidthHighFreq, sampleRate);

        // Transient detection coefficients
        float fastAttackCoef = MathF.Exp(-1f / (0.5f * sampleRate / 1000f));
        float fastReleaseCoef = MathF.Exp(-1f / (10f * sampleRate / 1000f));
        float slowAttackCoef = MathF.Exp(-1f / (50f * sampleRate / 1000f));
        float slowReleaseCoef = MathF.Exp(-1f / (200f * sampleRate / 1000f));

        // Calculate balance gains
        float midBalanceGain = msBalance >= 0 ? 1f : 1f + msBalance;
        float sideBalanceGain = msBalance <= 0 ? 1f : 1f - msBalance;

        // Metering accumulators for this buffer
        float midPeak = 0f, sidePeak = 0f;
        float midRmsSum = 0f, sideRmsSum = 0f;
        float outLeftPeak = 0f, outRightPeak = 0f;
        float outLeftRmsSum = 0f, outRightRmsSum = 0f;
        float corrSum = 0f, powerL = 0f, powerR = 0f;

        // Process each stereo frame
        for (int frame = 0; frame < frames; frame++)
        {
            int srcIdx = frame * 2;
            float left = sourceBuffer[srcIdx];
            float right = sourceBuffer[srcIdx + 1];

            float mid, side;

            // Decode only mode - input is already M/S
            if (mode == MidSideProcessingMode.DecodeOnly)
            {
                mid = left;
                side = right;
            }
            else
            {
                // Encode to M/S
                mid = (left + right) * 0.5f;
                side = (left - right) * 0.5f;

                // Apply mono maker (sum bass to mono)
                if (monoMakerCoef > 0)
                {
                    // Extract low frequencies
                    _monoMakerLpStateL = _monoMakerLpStateL * monoMakerCoef + left * (1f - monoMakerCoef);
                    _monoMakerLpStateR = _monoMakerLpStateR * monoMakerCoef + right * (1f - monoMakerCoef);

                    float bassL = _monoMakerLpStateL;
                    float bassR = _monoMakerLpStateR;
                    float bassMono = (bassL + bassR) * 0.5f;

                    // High frequencies remain stereo
                    float highL = left - bassL;
                    float highR = right - bassR;

                    // Re-encode with mono bass
                    mid = bassMono + (highL + highR) * 0.5f;
                    side = (highL - highR) * 0.5f;
                }

                // Auto-width processing
                if (autoWidthMode != AutoWidthMode.Off)
                {
                    // Split into low, mid, high bands
                    _autoWidthLowLpStateL = _autoWidthLowLpStateL * autoWidthLowCoef + left * (1f - autoWidthLowCoef);
                    _autoWidthLowLpStateR = _autoWidthLowLpStateR * autoWidthLowCoef + right * (1f - autoWidthLowCoef);

                    float lowL = _autoWidthLowLpStateL;
                    float lowR = _autoWidthLowLpStateR;

                    _autoWidthHighHpStateL = _autoWidthHighHpStateL * autoWidthHighCoef + left * (1f - autoWidthHighCoef);
                    _autoWidthHighHpStateR = _autoWidthHighHpStateR * autoWidthHighCoef + right * (1f - autoWidthHighCoef);

                    float highL = left - _autoWidthHighHpStateL;
                    float highR = right - _autoWidthHighHpStateR;

                    float midL = left - lowL - highL;
                    float midR = right - lowR - highR;

                    // Calculate M/S for each band
                    float lowMid = (lowL + lowR) * 0.5f;
                    float lowSide = (lowL - lowR) * 0.5f;
                    float midMid = (midL + midR) * 0.5f;
                    float midSide = (midL - midR) * 0.5f;
                    float highMid = (highL + highR) * 0.5f;
                    float highSide = (highL - highR) * 0.5f;

                    // Apply frequency-dependent width
                    switch (autoWidthMode)
                    {
                        case AutoWidthMode.LowOnly:
                            lowSide *= autoWidthLowAmt;
                            break;
                        case AutoWidthMode.HighOnly:
                            highSide *= autoWidthHighAmt;
                            break;
                        case AutoWidthMode.Full:
                            lowSide *= autoWidthLowAmt;
                            highSide *= autoWidthHighAmt;
                            break;
                    }

                    // Recombine
                    mid = lowMid + midMid + highMid;
                    side = lowSide + midSide + highSide;
                }
            }

            // Apply mid channel filtering
            if (midHpFreq > 20f)
            {
                _midHpState1 = _midHpState1 * midHpCoef + mid * (1f - midHpCoef);
                _midHpState2 = _midHpState2 * midHpCoef + _midHpState1 * (1f - midHpCoef);
                mid = mid - _midHpState2;
            }
            if (midLpFreq < 20000f)
            {
                _midLpState1 = _midLpState1 * (1f - (1f - midLpCoef)) + mid * (1f - midLpCoef);
                _midLpState2 = _midLpState2 * (1f - (1f - midLpCoef)) + _midLpState1 * (1f - midLpCoef);
                mid = _midLpState2;
            }

            // Apply side channel filtering
            if (sideHpFreq > 20f)
            {
                _sideHpState1 = _sideHpState1 * sideHpCoef + side * (1f - sideHpCoef);
                _sideHpState2 = _sideHpState2 * sideHpCoef + _sideHpState1 * (1f - sideHpCoef);
                side = side - _sideHpState2;
            }
            if (sideLpFreq < 20000f)
            {
                _sideLpState1 = _sideLpState1 * (1f - (1f - sideLpCoef)) + side * (1f - sideLpCoef);
                _sideLpState2 = _sideLpState2 * (1f - (1f - sideLpCoef)) + _sideLpState1 * (1f - sideLpCoef);
                side = _sideLpState2;
            }

            // Transient focus processing
            if (transientMode != TransientFocusMode.Off && transientAmount > 0)
            {
                float midAbs = MathF.Abs(mid);
                float sideAbs = MathF.Abs(side);

                // Update mid envelope
                float midFastCoef = midAbs > _midFastEnv ? fastAttackCoef : fastReleaseCoef;
                _midFastEnv = midAbs + midFastCoef * (_midFastEnv - midAbs);
                float midSlowCoef = midAbs > _midSlowEnv ? slowAttackCoef : slowReleaseCoef;
                _midSlowEnv = midAbs + midSlowCoef * (_midSlowEnv - midAbs);
                float midTransient = MathF.Max(0f, _midFastEnv - _midSlowEnv);

                // Update side envelope
                float sideFastCoef = sideAbs > _sideFastEnv ? fastAttackCoef : fastReleaseCoef;
                _sideFastEnv = sideAbs + sideFastCoef * (_sideFastEnv - sideAbs);
                float sideSlowCoef = sideAbs > _sideSlowEnv ? slowAttackCoef : slowReleaseCoef;
                _sideSlowEnv = sideAbs + sideSlowCoef * (_sideSlowEnv - sideAbs);
                float sideTransient = MathF.Max(0f, _sideFastEnv - _sideSlowEnv);

                // Apply transient focus
                float midGain = 1f;
                float sideGain = 1f;

                switch (transientMode)
                {
                    case TransientFocusMode.MidTransients:
                        midGain = 1f + midTransient * transientAmount * 2f;
                        sideGain = 1f - sideTransient * transientAmount * 0.5f;
                        break;
                    case TransientFocusMode.SideTransients:
                        midGain = 1f - midTransient * transientAmount * 0.5f;
                        sideGain = 1f + sideTransient * transientAmount * 2f;
                        break;
                    case TransientFocusMode.Balanced:
                        float totalTransient = midTransient + sideTransient;
                        if (totalTransient > 0.001f)
                        {
                            float midRatio = midTransient / totalTransient;
                            midGain = 1f + (midRatio - 0.5f) * transientAmount;
                            sideGain = 1f + ((1f - midRatio) - 0.5f) * transientAmount;
                        }
                        break;
                }

                mid *= MathF.Max(0.1f, midGain);
                side *= MathF.Max(0.1f, sideGain);
            }

            // Store in processing buffers for effect slots
            _midBuffer[frame] = mid;
            _sideBuffer[frame] = side;
        }

        // Apply mid channel effects
        foreach (var effect in _midEffects)
        {
            effect(_midBuffer, frames);
        }

        // Apply side channel effects
        foreach (var effect in _sideEffects)
        {
            effect(_sideBuffer, frames);
        }

        // Continue processing with potentially modified buffers
        for (int frame = 0; frame < frames; frame++)
        {
            float mid = _midBuffer[frame];
            float side = _sideBuffer[frame];

            // Apply phase inversion
            if (midPhaseInvert) mid = -mid;
            if (sidePhaseInvert) side = -side;

            // Apply level adjustments
            mid *= midLevelLinear;
            side *= sideLevelLinear;

            // Apply width (affects side channel)
            side *= width;

            // Apply balance
            mid *= midBalanceGain;
            side *= sideBalanceGain;

            // Apply mute
            if (midMute) mid = 0f;
            if (sideMute) side = 0f;

            // Metering before output
            float midAbsForMeter = MathF.Abs(mid);
            float sideAbsForMeter = MathF.Abs(side);
            midPeak = MathF.Max(midPeak, midAbsForMeter);
            sidePeak = MathF.Max(sidePeak, sideAbsForMeter);
            midRmsSum += mid * mid;
            sideRmsSum += side * side;

            // Calculate output based on mode
            float outputL, outputR;

            if (midSolo || mode == MidSideProcessingMode.MidSolo)
            {
                outputL = mid;
                outputR = mid;
            }
            else if (sideSolo || mode == MidSideProcessingMode.SideSolo)
            {
                outputL = side;
                outputR = -side; // Proper stereo monitoring
            }
            else if (mode == MidSideProcessingMode.EncodeOnly)
            {
                outputL = mid;
                outputR = side;
            }
            else
            {
                // Normal decode to L/R
                outputL = mid + side;
                outputR = mid - side;
            }

            // Apply output gain
            outputL *= outputGainLinear;
            outputR *= outputGainLinear;

            // Output metering
            outLeftPeak = MathF.Max(outLeftPeak, MathF.Abs(outputL));
            outRightPeak = MathF.Max(outRightPeak, MathF.Abs(outputR));
            outLeftRmsSum += outputL * outputL;
            outRightRmsSum += outputR * outputR;

            // Correlation metering
            corrSum += outputL * outputR;
            powerL += outputL * outputL;
            powerR += outputR * outputR;

            // Write output
            destBuffer[offset + frame * 2] = outputL;
            destBuffer[offset + frame * 2 + 1] = outputR;
        }

        // Update metering with decay
        _midPeakAcc = MathF.Max(midPeak, _midPeakAcc * MathF.Pow(PeakDecayPerSample, frames));
        _sidePeakAcc = MathF.Max(sidePeak, _sidePeakAcc * MathF.Pow(PeakDecayPerSample, frames));
        _midRmsAcc = _midRmsAcc * RmsDecayPerSample + midRmsSum / frames;
        _sideRmsAcc = _sideRmsAcc * RmsDecayPerSample + sideRmsSum / frames;
        _outputLeftPeakAcc = MathF.Max(outLeftPeak, _outputLeftPeakAcc * MathF.Pow(PeakDecayPerSample, frames));
        _outputRightPeakAcc = MathF.Max(outRightPeak, _outputRightPeakAcc * MathF.Pow(PeakDecayPerSample, frames));
        _outputLeftRmsAcc = _outputLeftRmsAcc * RmsDecayPerSample + outLeftRmsSum / frames;
        _outputRightRmsAcc = _outputRightRmsAcc * RmsDecayPerSample + outRightRmsSum / frames;
        _correlationSumAcc += corrSum;
        _powerLeftAcc += powerL;
        _powerRightAcc += powerR;
        _meteringSampleCount += frames;

        // Update metering data periodically
        if (_meteringSampleCount >= MeteringUpdateInterval)
        {
            UpdateMeteringData();
        }
    }

    private void UpdateMeteringData()
    {
        Metering.MidPeakDb = LinearToDb(_midPeakAcc);
        Metering.SidePeakDb = LinearToDb(_sidePeakAcc);
        Metering.MidRmsDb = LinearToDb(MathF.Sqrt(_midRmsAcc));
        Metering.SideRmsDb = LinearToDb(MathF.Sqrt(_sideRmsAcc));
        Metering.OutputLeftPeakDb = LinearToDb(_outputLeftPeakAcc);
        Metering.OutputRightPeakDb = LinearToDb(_outputRightPeakAcc);
        Metering.OutputLeftRmsDb = LinearToDb(MathF.Sqrt(_outputLeftRmsAcc));
        Metering.OutputRightRmsDb = LinearToDb(MathF.Sqrt(_outputRightRmsAcc));

        // Calculate correlation
        float power = MathF.Sqrt(_powerLeftAcc * _powerRightAcc);
        Metering.Correlation = power > 1e-10f ? _correlationSumAcc / power : 1f;
        Metering.IsMonoCompatible = Metering.Correlation > 0f;

        // Calculate M/S balance
        float midPower = _midRmsAcc;
        float sidePower = _sideRmsAcc;
        float totalPower = midPower + sidePower;
        Metering.MidSideBalance = totalPower > 1e-10f ? (midPower - sidePower) / totalPower : 0f;

        // Calculate instant width
        Metering.InstantWidth = midPower > 1e-10f ? MathF.Sqrt(sidePower / midPower) : 0f;

        // Reset accumulators
        _correlationSumAcc = 0f;
        _powerLeftAcc = 0f;
        _powerRightAcc = 0f;
        _meteringSampleCount = 0;
    }

    private static float CalculateFilterCoef(float frequency, int sampleRate)
    {
        return MathF.Exp(-2f * MathF.PI * frequency / sampleRate);
    }

    private static float DbToLinear(float db) => MathF.Pow(10f, db / 20f);

    private static float LinearToDb(float linear) => 20f * MathF.Log10(MathF.Max(linear, 1e-10f));

    #endregion

    #region Utility Methods

    /// <summary>
    /// Encodes a stereo sample pair to Mid/Side.
    /// </summary>
    /// <param name="left">Left channel sample.</param>
    /// <param name="right">Right channel sample.</param>
    /// <param name="mid">Output mid sample.</param>
    /// <param name="side">Output side sample.</param>
    public static void EncodeToMidSide(float left, float right, out float mid, out float side)
    {
        mid = (left + right) * 0.5f;
        side = (left - right) * 0.5f;
    }

    /// <summary>
    /// Decodes a Mid/Side sample pair to stereo.
    /// </summary>
    /// <param name="mid">Mid channel sample.</param>
    /// <param name="side">Side channel sample.</param>
    /// <param name="left">Output left sample.</param>
    /// <param name="right">Output right sample.</param>
    public static void DecodeToStereo(float mid, float side, out float left, out float right)
    {
        left = mid + side;
        right = mid - side;
    }

    /// <summary>
    /// Encodes a stereo buffer to Mid/Side in-place.
    /// </summary>
    /// <param name="buffer">Interleaved stereo buffer (L,R,L,R,...).</param>
    /// <param name="count">Number of samples (must be even).</param>
    public static void EncodeBufferToMidSide(float[] buffer, int count)
    {
        for (int i = 0; i < count; i += 2)
        {
            float left = buffer[i];
            float right = buffer[i + 1];
            buffer[i] = (left + right) * 0.5f;     // Mid
            buffer[i + 1] = (left - right) * 0.5f; // Side
        }
    }

    /// <summary>
    /// Decodes a Mid/Side buffer to stereo in-place.
    /// </summary>
    /// <param name="buffer">Interleaved M/S buffer (M,S,M,S,...).</param>
    /// <param name="count">Number of samples (must be even).</param>
    public static void DecodeBufferToStereo(float[] buffer, int count)
    {
        for (int i = 0; i < count; i += 2)
        {
            float mid = buffer[i];
            float side = buffer[i + 1];
            buffer[i] = mid + side;     // Left
            buffer[i + 1] = mid - side; // Right
        }
    }

    /// <summary>
    /// Checks if a stereo buffer is mono-compatible (positive correlation).
    /// </summary>
    /// <param name="buffer">Interleaved stereo buffer.</param>
    /// <param name="count">Number of samples.</param>
    /// <returns>True if mono-compatible, false if phase issues detected.</returns>
    public static bool CheckMonoCompatibility(float[] buffer, int count)
    {
        float corrSum = 0f;
        float powerL = 0f;
        float powerR = 0f;

        for (int i = 0; i < count; i += 2)
        {
            float left = buffer[i];
            float right = buffer[i + 1];
            corrSum += left * right;
            powerL += left * left;
            powerR += right * right;
        }

        float power = MathF.Sqrt(powerL * powerR);
        float correlation = power > 1e-10f ? corrSum / power : 1f;
        return correlation > 0f;
    }

    /// <summary>
    /// Calculates the stereo correlation of a buffer.
    /// </summary>
    /// <param name="buffer">Interleaved stereo buffer.</param>
    /// <param name="count">Number of samples.</param>
    /// <returns>Correlation value (-1 to +1).</returns>
    public static float CalculateCorrelation(float[] buffer, int count)
    {
        float corrSum = 0f;
        float powerL = 0f;
        float powerR = 0f;

        for (int i = 0; i < count; i += 2)
        {
            float left = buffer[i];
            float right = buffer[i + 1];
            corrSum += left * right;
            powerL += left * left;
            powerR += right * right;
        }

        float power = MathF.Sqrt(powerL * powerR);
        return power > 1e-10f ? corrSum / power : 1f;
    }

    /// <summary>
    /// Resets all filter states and metering accumulators.
    /// </summary>
    public void Reset()
    {
        _midHpState1 = 0f;
        _midHpState2 = 0f;
        _midLpState1 = 0f;
        _midLpState2 = 0f;
        _sideHpState1 = 0f;
        _sideHpState2 = 0f;
        _sideLpState1 = 0f;
        _sideLpState2 = 0f;
        _monoMakerLpStateL = 0f;
        _monoMakerLpStateR = 0f;
        _autoWidthLowLpStateL = 0f;
        _autoWidthLowLpStateR = 0f;
        _autoWidthHighHpStateL = 0f;
        _autoWidthHighHpStateR = 0f;
        _midFastEnv = 0f;
        _midSlowEnv = 0f;
        _sideFastEnv = 0f;
        _sideSlowEnv = 0f;

        _midPeakAcc = 0f;
        _sidePeakAcc = 0f;
        _midRmsAcc = 0f;
        _sideRmsAcc = 0f;
        _outputLeftPeakAcc = 0f;
        _outputRightPeakAcc = 0f;
        _outputLeftRmsAcc = 0f;
        _outputRightRmsAcc = 0f;
        _correlationSumAcc = 0f;
        _powerLeftAcc = 0f;
        _powerRightAcc = 0f;
        _meteringSampleCount = 0;
    }

    #endregion

    #region Preset Management

    /// <summary>
    /// Saves the current settings to a preset.
    /// </summary>
    /// <param name="name">Preset name.</param>
    /// <param name="author">Optional author name.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="category">Optional category.</param>
    /// <returns>The created preset.</returns>
    public MidSidePreset SavePreset(string name, string author = "", string description = "", string category = "User")
    {
        var preset = new MidSidePreset
        {
            Name = name,
            Author = author,
            Description = description,
            Category = category,
            CreatedDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow,

            MidLevel = MidLevel,
            SideLevel = SideLevel,
            Width = Width,
            MidSideBalanceParam = MidSideBalance,
            MidMute = MidMute,
            SideMute = SideMute,
            MidSolo = MidSolo,
            SideSolo = SideSolo,
            MidPhaseInvert = MidPhaseInvert,
            SidePhaseInvert = SidePhaseInvert,
            MonoMakerFrequency = MonoMakerFrequency,
            SideHighPassFrequency = SideHighPassFrequency,
            SideLowPassFrequency = SideLowPassFrequency,
            MidHighPassFrequency = MidHighPassFrequency,
            MidLowPassFrequency = MidLowPassFrequency,
            OutputGain = OutputGain,
            AutoWidth = AutoWidthSetting,
            AutoWidthLowFreq = AutoWidthLowFrequency,
            AutoWidthHighFreq = AutoWidthHighFrequency,
            AutoWidthLowAmount = AutoWidthLowAmount,
            AutoWidthHighAmount = AutoWidthHighAmount,
            TransientFocus = TransientFocus,
            TransientFocusAmount = TransientFocusAmount,
            ProcessingMode = ProcessingMode
        };

        // Add to internal list if not already present
        var existing = _presets.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            _presets.Remove(existing);
        }
        _presets.Add(preset);
        _currentPreset = preset;

        return preset;
    }

    /// <summary>
    /// Loads settings from a preset.
    /// </summary>
    /// <param name="preset">The preset to load.</param>
    public void ApplyPreset(MidSidePreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);

        MidLevel = preset.MidLevel;
        SideLevel = preset.SideLevel;
        Width = preset.Width;
        MidSideBalance = preset.MidSideBalanceParam;
        MidMute = preset.MidMute;
        SideMute = preset.SideMute;
        MidSolo = preset.MidSolo;
        SideSolo = preset.SideSolo;
        MidPhaseInvert = preset.MidPhaseInvert;
        SidePhaseInvert = preset.SidePhaseInvert;
        MonoMakerFrequency = preset.MonoMakerFrequency;
        SideHighPassFrequency = preset.SideHighPassFrequency;
        SideLowPassFrequency = preset.SideLowPassFrequency;
        MidHighPassFrequency = preset.MidHighPassFrequency;
        MidLowPassFrequency = preset.MidLowPassFrequency;
        OutputGain = preset.OutputGain;
        AutoWidthSetting = preset.AutoWidth;
        AutoWidthLowFrequency = preset.AutoWidthLowFreq;
        AutoWidthHighFrequency = preset.AutoWidthHighFreq;
        AutoWidthLowAmount = preset.AutoWidthLowAmount;
        AutoWidthHighAmount = preset.AutoWidthHighAmount;
        TransientFocus = preset.TransientFocus;
        TransientFocusAmount = preset.TransientFocusAmount;
        ProcessingMode = preset.ProcessingMode;

        _currentPreset = preset;
    }

    /// <summary>
    /// Adds a preset to the preset list.
    /// </summary>
    /// <param name="preset">The preset to add.</param>
    public void AddPreset(MidSidePreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        var existing = _presets.FirstOrDefault(p => p.Name.Equals(preset.Name, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            _presets.Remove(existing);
        }
        _presets.Add(preset);
    }

    /// <summary>
    /// Removes a preset from the preset list.
    /// </summary>
    /// <param name="presetName">The name of the preset to remove.</param>
    /// <returns>True if removed, false if not found.</returns>
    public bool RemovePreset(string presetName)
    {
        var preset = _presets.FirstOrDefault(p => p.Name.Equals(presetName, StringComparison.OrdinalIgnoreCase));
        if (preset != null)
        {
            _presets.Remove(preset);
            if (_currentPreset == preset)
            {
                _currentPreset = null;
            }
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets a preset by name.
    /// </summary>
    /// <param name="name">The preset name.</param>
    /// <returns>The preset, or null if not found.</returns>
    public MidSidePreset? GetPreset(string name)
    {
        return _presets.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets presets by category.
    /// </summary>
    /// <param name="category">The category to filter by.</param>
    /// <returns>Matching presets.</returns>
    public IEnumerable<MidSidePreset> GetPresetsByCategory(string category)
    {
        return _presets.Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Exports a preset to JSON string.
    /// </summary>
    /// <param name="presetName">The name of the preset to export.</param>
    /// <returns>JSON string, or null if preset not found.</returns>
    public string? ExportPreset(string presetName)
    {
        var preset = GetPreset(presetName);
        return preset?.ToJson();
    }

    /// <summary>
    /// Imports a preset from JSON string.
    /// </summary>
    /// <param name="json">The JSON string.</param>
    /// <returns>True if import successful.</returns>
    public bool ImportPreset(string json)
    {
        var preset = MidSidePreset.FromJson(json);
        if (preset != null)
        {
            AddPreset(preset);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Saves all presets to a JSON file.
    /// </summary>
    /// <param name="filePath">The file path to save to.</param>
    public void SavePresetsToFile(string filePath)
    {
        var json = JsonSerializer.Serialize(_presets, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        });
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Loads presets from a JSON file.
    /// </summary>
    /// <param name="filePath">The file path to load from.</param>
    /// <returns>Number of presets loaded.</returns>
    public int LoadPresetsFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            return 0;

        try
        {
            var json = File.ReadAllText(filePath);
            var presets = JsonSerializer.Deserialize<List<MidSidePreset>>(json, new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() }
            });

            if (presets != null)
            {
                foreach (var preset in presets)
                {
                    AddPreset(preset);
                }
                return presets.Count;
            }
        }
        catch
        {
            // Ignore load errors
        }

        return 0;
    }

    private void InitializeFactoryPresets()
    {
        // Default / Bypass
        _presets.Add(new MidSidePreset
        {
            Name = "Default",
            Category = "Factory",
            Description = "Default settings with no processing"
        });

        // Subtle Wide
        _presets.Add(new MidSidePreset
        {
            Name = "Subtle Wide",
            Category = "Factory",
            Description = "Subtle stereo widening with mono-compatible bass",
            MidLevel = -1f,
            SideLevel = 2f,
            Width = 1.2f,
            MonoMakerFrequency = 100f
        });

        // Extra Wide
        _presets.Add(new MidSidePreset
        {
            Name = "Extra Wide",
            Category = "Factory",
            Description = "Dramatic stereo widening",
            MidLevel = -3f,
            SideLevel = 4f,
            Width = 1.8f,
            MonoMakerFrequency = 150f,
            SideHighPassFrequency = 200f
        });

        // Mono Bass
        _presets.Add(new MidSidePreset
        {
            Name = "Mono Bass",
            Category = "Factory",
            Description = "Sum low frequencies to mono for better club/vinyl compatibility",
            MonoMakerFrequency = 120f
        });

        // Vocal Isolation
        _presets.Add(new MidSidePreset
        {
            Name = "Vocal Isolation",
            Category = "Factory",
            Description = "Emphasize center content (vocals)",
            MidLevel = 3f,
            SideLevel = -12f,
            Width = 0.3f,
            MidHighPassFrequency = 100f,
            MidLowPassFrequency = 8000f
        });

        // Karaoke
        _presets.Add(new MidSidePreset
        {
            Name = "Karaoke",
            Category = "Factory",
            Description = "Remove center content (vocals)",
            MidLevel = -60f,
            SideLevel = 3f,
            Width = 1.5f
        });

        // Narrow
        _presets.Add(new MidSidePreset
        {
            Name = "Narrow",
            Category = "Factory",
            Description = "Reduce stereo width",
            MidLevel = 2f,
            SideLevel = -6f,
            Width = 0.5f
        });

        // Mono
        _presets.Add(new MidSidePreset
        {
            Name = "Mono",
            Category = "Factory",
            Description = "Convert to mono",
            Width = 0f
        });

        // Auto-Width Full
        _presets.Add(new MidSidePreset
        {
            Name = "Auto-Width Full",
            Category = "Factory",
            Description = "Frequency-dependent width: narrow lows, wide highs",
            AutoWidth = AutoWidthMode.Full,
            AutoWidthLowFreq = 150f,
            AutoWidthHighFreq = 6000f,
            AutoWidthLowAmount = 0.3f,
            AutoWidthHighAmount = 1.5f
        });

        // Bass Tightener
        _presets.Add(new MidSidePreset
        {
            Name = "Bass Tightener",
            Category = "Factory",
            Description = "Tighten low end with side HP and mono maker",
            MonoMakerFrequency = 100f,
            SideHighPassFrequency = 150f
        });

        // Air Enhancer
        _presets.Add(new MidSidePreset
        {
            Name = "Air Enhancer",
            Category = "Factory",
            Description = "Widen high frequencies for more air",
            AutoWidth = AutoWidthMode.HighOnly,
            AutoWidthHighFreq = 8000f,
            AutoWidthHighAmount = 1.6f
        });

        // Mid Focus
        _presets.Add(new MidSidePreset
        {
            Name = "Mid Focus",
            Category = "Factory",
            Description = "Emphasize transients in the center",
            TransientFocus = TransientFocusMode.MidTransients,
            TransientFocusAmount = 60f
        });

        // Side Ambience
        _presets.Add(new MidSidePreset
        {
            Name = "Side Ambience",
            Category = "Factory",
            Description = "Emphasize room and ambience in sides",
            TransientFocus = TransientFocusMode.SideTransients,
            TransientFocusAmount = 50f,
            SideLevel = 2f
        });

        // Mastering Wide
        _presets.Add(new MidSidePreset
        {
            Name = "Mastering Wide",
            Category = "Mastering",
            Description = "Subtle width increase suitable for mastering",
            MidLevel = -0.5f,
            SideLevel = 1f,
            Width = 1.1f,
            MonoMakerFrequency = 80f,
            SideHighPassFrequency = 100f,
            AutoWidth = AutoWidthMode.Full,
            AutoWidthLowAmount = 0.5f,
            AutoWidthHighAmount = 1.2f
        });

        // Broadcast Safe
        _presets.Add(new MidSidePreset
        {
            Name = "Broadcast Safe",
            Category = "Mastering",
            Description = "Ensure mono compatibility for broadcast",
            MonoMakerFrequency = 120f,
            Width = 0.9f,
            SideHighPassFrequency = 150f
        });
    }

    #endregion

    #region Factory Methods

    /// <summary>
    /// Creates a processor with subtle stereo widening.
    /// </summary>
    public static MidSideProcessor CreateSubtleWide(ISampleProvider source)
    {
        var processor = new MidSideProcessor(source, "Subtle Wide");
        processor.ApplyPreset(processor.GetPreset("Subtle Wide")!);
        return processor;
    }

    /// <summary>
    /// Creates a processor with extra wide stereo.
    /// </summary>
    public static MidSideProcessor CreateExtraWide(ISampleProvider source)
    {
        var processor = new MidSideProcessor(source, "Extra Wide");
        processor.ApplyPreset(processor.GetPreset("Extra Wide")!);
        return processor;
    }

    /// <summary>
    /// Creates a processor for mono bass compatibility.
    /// </summary>
    public static MidSideProcessor CreateMonoBass(ISampleProvider source)
    {
        var processor = new MidSideProcessor(source, "Mono Bass");
        processor.ApplyPreset(processor.GetPreset("Mono Bass")!);
        return processor;
    }

    /// <summary>
    /// Creates a processor for vocal isolation.
    /// </summary>
    public static MidSideProcessor CreateVocalIsolation(ISampleProvider source)
    {
        var processor = new MidSideProcessor(source, "Vocal Isolation");
        processor.ApplyPreset(processor.GetPreset("Vocal Isolation")!);
        return processor;
    }

    /// <summary>
    /// Creates a processor for karaoke (center removal).
    /// </summary>
    public static MidSideProcessor CreateKaraoke(ISampleProvider source)
    {
        var processor = new MidSideProcessor(source, "Karaoke");
        processor.ApplyPreset(processor.GetPreset("Karaoke")!);
        return processor;
    }

    /// <summary>
    /// Creates a processor for converting to mono.
    /// </summary>
    public static MidSideProcessor CreateMono(ISampleProvider source)
    {
        var processor = new MidSideProcessor(source, "Mono");
        processor.ApplyPreset(processor.GetPreset("Mono")!);
        return processor;
    }

    /// <summary>
    /// Creates a processor with frequency-dependent auto-width.
    /// </summary>
    public static MidSideProcessor CreateAutoWidth(ISampleProvider source)
    {
        var processor = new MidSideProcessor(source, "Auto-Width");
        processor.ApplyPreset(processor.GetPreset("Auto-Width Full")!);
        return processor;
    }

    /// <summary>
    /// Creates a processor optimized for mastering.
    /// </summary>
    public static MidSideProcessor CreateMasteringWide(ISampleProvider source)
    {
        var processor = new MidSideProcessor(source, "Mastering Wide");
        processor.ApplyPreset(processor.GetPreset("Mastering Wide")!);
        return processor;
    }

    /// <summary>
    /// Creates a processor for broadcast-safe stereo.
    /// </summary>
    public static MidSideProcessor CreateBroadcastSafe(ISampleProvider source)
    {
        var processor = new MidSideProcessor(source, "Broadcast Safe");
        processor.ApplyPreset(processor.GetPreset("Broadcast Safe")!);
        return processor;
    }

    #endregion
}
