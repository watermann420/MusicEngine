//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Console-style channel strip processor with full signal chain.

using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using NAudio.Wave;
using MusicEngine.Core.Dsp;

namespace MusicEngine.Core.Routing;

/// <summary>
/// Console emulation style for the channel strip.
/// </summary>
public enum ConsoleStyle
{
    /// <summary>
    /// Clean/transparent processing.
    /// </summary>
    Clean,

    /// <summary>
    /// SSL-style: punchy, aggressive EQ and compression.
    /// </summary>
    SSL,

    /// <summary>
    /// Neve-style: warm, musical, with harmonic richness.
    /// </summary>
    Neve,

    /// <summary>
    /// API-style: punchy, with character in the mids.
    /// </summary>
    API
}

/// <summary>
/// Saturation mode for the channel strip.
/// </summary>
public enum SaturationMode
{
    /// <summary>
    /// No saturation.
    /// </summary>
    Off,

    /// <summary>
    /// Subtle tape-style saturation.
    /// </summary>
    Tape,

    /// <summary>
    /// Tube/valve saturation.
    /// </summary>
    Tube,

    /// <summary>
    /// Transformer saturation.
    /// </summary>
    Transformer
}

/// <summary>
/// Insert point location in the signal chain.
/// </summary>
public enum InsertPoint
{
    /// <summary>
    /// Pre-EQ insert point.
    /// </summary>
    PreEQ,

    /// <summary>
    /// Post-EQ insert point.
    /// </summary>
    PostEQ
}

/// <summary>
/// Console-style channel strip processor with full signal chain.
/// Provides complete channel processing similar to a hardware mixing console.
/// </summary>
/// <remarks>
/// Signal flow:
/// Input -> Input Gain/Trim -> Phase Invert -> High Pass Filter -> Noise Gate ->
/// [Pre-EQ Insert] -> 4-Band EQ -> [Post-EQ Insert] -> Compressor -> De-Esser ->
/// Saturation -> Stereo Width -> Pan -> Output Level/Fader -> Aux Sends -> Output
/// </remarks>
public class ChannelStrip : ISampleProvider, IDisposable
{
    private readonly ISampleProvider _source;
    private readonly int _channels;
    private readonly int _sampleRate;
    private readonly object _lock = new();

    // A/B comparison state storage
    private ChannelStripState? _stateA;
    private ChannelStripState? _stateB;
    private bool _isStateA = true;

    // Signal processing buffers
    private float[] _processBuffer;
    private float[] _sidechainBuffer;
    private float[] _insertBuffer;

    // Linked channel for stereo linking
    private ChannelStrip? _linkedChannel;

    #region Input Section

    // Input gain
    private float _inputGainDb = 0f;
    private float _inputGainLinear = 1f;
    private float _targetInputGain = 1f;
    private const float GainSmoothingCoeff = 0.9995f;

    // Phase
    private bool _phaseInvertLeft;
    private bool _phaseInvertRight;

    // High pass filter
    private bool _highPassEnabled;
    private float _highPassFrequency = 80f;
    private BiquadCoeffs _highPassCoeffs;
    private BiquadState[] _highPassStates;

    // Input section bypass
    private bool _inputSectionBypassed;

    #endregion

    #region Gate Section

    private bool _gateEnabled;
    private float _gateThreshold = -40f;
    private float _gateAttack = 0.001f;
    private float _gateRelease = 0.1f;
    private float _gateRange = -80f;
    private float _gateHold = 0.05f;
    private float[] _gateEnvelope;
    private float[] _gateState;
    private int[] _gateHoldCounter;
    private bool _gateSectionBypassed;

    // Gate sidechain filter
    private bool _gateSidechainFilterEnabled;
    private float _gateSidechainFilterFreq = 1000f;
    private BiquadCoeffs _gateSidechainFilterCoeffs;
    private BiquadState[] _gateSidechainFilterStates;

    #endregion

    #region EQ Section

    // Low shelf
    private float _eqLowFreq = 80f;
    private float _eqLowGain = 0f;
    private float _eqLowQ = 0.707f;
    private BiquadCoeffs _eqLowCoeffs;
    private BiquadState[] _eqLowStates;

    // Low mid parametric
    private float _eqLowMidFreq = 400f;
    private float _eqLowMidGain = 0f;
    private float _eqLowMidQ = 1.0f;
    private BiquadCoeffs _eqLowMidCoeffs;
    private BiquadState[] _eqLowMidStates;

    // High mid parametric
    private float _eqHighMidFreq = 2500f;
    private float _eqHighMidGain = 0f;
    private float _eqHighMidQ = 1.0f;
    private BiquadCoeffs _eqHighMidCoeffs;
    private BiquadState[] _eqHighMidStates;

    // High shelf
    private float _eqHighFreq = 12000f;
    private float _eqHighGain = 0f;
    private float _eqHighQ = 0.707f;
    private BiquadCoeffs _eqHighCoeffs;
    private BiquadState[] _eqHighStates;

    private bool _eqEnabled = true;
    private bool _eqSectionBypassed;

    #endregion

    #region Compressor Section

    private bool _compressorEnabled;
    private float _compThreshold = -20f;
    private float _compRatio = 4f;
    private float _compAttack = 0.005f;
    private float _compRelease = 0.1f;
    private float _compMakeupGain = 0f;
    private float _compKneeWidth = 0f;
    private float[] _compEnvelope;
    private float[] _compGainSmooth;
    private bool _compSectionBypassed;
    private float _compGainReductionDb;

    // Compressor sidechain filter (for focusing compression)
    private bool _compSidechainFilterEnabled;
    private float _compSidechainFilterFreq = 100f;
    private FilterType _compSidechainFilterType = FilterType.HighPass;
    private BiquadCoeffs _compSidechainFilterCoeffs;
    private BiquadState[] _compSidechainFilterStates;

    // External sidechain
    private ISampleProvider? _externalSidechain;
    private bool _sidechainListen;

    #endregion

    #region De-Esser Section

    private bool _deEsserEnabled;
    private float _deEsserFrequency = 6000f;
    private float _deEsserThreshold = -20f;
    private float _deEsserReduction = 6f;
    private float _deEsserBandwidth = 2000f;
    private float[] _deEsserEnvelope;
    private float[] _deEsserGainSmooth;
    private BiquadState[] _deEsserDetectionStates;
    private float _deEsserB0, _deEsserB1, _deEsserB2, _deEsserA1, _deEsserA2;
    private bool _deEsserSectionBypassed;

    #endregion

    #region Saturation Section

    private SaturationMode _saturationMode = SaturationMode.Off;
    private float _saturationDrive = 0.5f;
    private float _saturationMix = 1f;
    private float[] _satLpState;
    private bool _saturationSectionBypassed;

    #endregion

    #region Width Section

    private float _stereoWidth = 1f;
    private float _monoLowFreq = 200f;
    private bool _widthSectionBypassed;

    #endregion

    #region Output Section

    private float _pan;
    private float _outputLevel = 1f;
    private float _fader = 1f;
    private bool _mute;
    private bool _solo;
    private bool _outputSectionBypassed;

    #endregion

    #region Aux Sends

    private readonly AuxSend[] _auxSends;
    private const int MaxAuxSends = 4;

    #endregion

    #region Insert Points

    private ISampleProvider? _preEqInsert;
    private ISampleProvider? _postEqInsert;
    private bool _preEqInsertEnabled;
    private bool _postEqInsertEnabled;

    #endregion

    #region Metering

    private float _inputPeakLeft;
    private float _inputPeakRight;
    private float _outputPeakLeft;
    private float _outputPeakRight;
    private float _inputRmsLeft;
    private float _inputRmsRight;
    private float _outputRmsLeft;
    private float _outputRmsRight;
    private float _inputRmsAccumLeft;
    private float _inputRmsAccumRight;
    private float _outputRmsAccumLeft;
    private float _outputRmsAccumRight;
    private int _rmsSampleCount;
    private const int RmsWindowSize = 4410;

    #endregion

    #region Console Style

    private ConsoleStyle _consoleStyle = ConsoleStyle.Clean;

    #endregion

    /// <summary>
    /// Creates a new console-style channel strip.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <param name="name">Channel name.</param>
    public ChannelStrip(ISampleProvider source, string name = "Channel")
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        Name = name;
        _channels = source.WaveFormat.Channels;
        _sampleRate = source.WaveFormat.SampleRate;

        // Initialize buffers
        int bufferSize = _sampleRate * _channels;
        _processBuffer = new float[bufferSize];
        _sidechainBuffer = new float[bufferSize];
        _insertBuffer = new float[bufferSize];

        // Initialize filter states
        _highPassStates = new BiquadState[_channels];
        _gateSidechainFilterStates = new BiquadState[_channels];

        _eqLowStates = new BiquadState[_channels];
        _eqLowMidStates = new BiquadState[_channels];
        _eqHighMidStates = new BiquadState[_channels];
        _eqHighStates = new BiquadState[_channels];

        _compSidechainFilterStates = new BiquadState[_channels];

        _deEsserDetectionStates = new BiquadState[_channels];

        for (int i = 0; i < _channels; i++)
        {
            _highPassStates[i] = BiquadState.Create();
            _gateSidechainFilterStates[i] = BiquadState.Create();
            _eqLowStates[i] = BiquadState.Create();
            _eqLowMidStates[i] = BiquadState.Create();
            _eqHighMidStates[i] = BiquadState.Create();
            _eqHighStates[i] = BiquadState.Create();
            _compSidechainFilterStates[i] = BiquadState.Create();
            _deEsserDetectionStates[i] = BiquadState.Create();
        }

        // Initialize dynamics states
        _gateEnvelope = new float[_channels];
        _gateState = new float[_channels];
        _gateHoldCounter = new int[_channels];

        _compEnvelope = new float[_channels];
        _compGainSmooth = new float[_channels];

        _deEsserEnvelope = new float[_channels];
        _deEsserGainSmooth = new float[_channels];

        _satLpState = new float[_channels];

        for (int i = 0; i < _channels; i++)
        {
            _gateState[i] = 1f;
            _compGainSmooth[i] = 1f;
            _deEsserGainSmooth[i] = 1f;
        }

        // Initialize aux sends
        _auxSends = new AuxSend[MaxAuxSends];
        for (int i = 0; i < MaxAuxSends; i++)
        {
            _auxSends[i] = new AuxSend { Index = i };
        }

        // Calculate initial filter coefficients
        UpdateHighPassCoefficients();
        UpdateGateSidechainFilterCoefficients();
        UpdateEqCoefficients();
        UpdateCompressorSidechainFilterCoefficients();
        UpdateDeEsserCoefficients();
    }

    /// <inheritdoc />
    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>
    /// Gets or sets the channel name.
    /// </summary>
    public string Name { get; set; }

    #region Input Section Properties

    /// <summary>
    /// Gets or sets the input gain/trim in dB (-24 to +24).
    /// </summary>
    public float InputGainDb
    {
        get => _inputGainDb;
        set
        {
            _inputGainDb = Math.Clamp(value, -24f, 24f);
            _targetInputGain = MathF.Pow(10f, _inputGainDb / 20f);
        }
    }

    /// <summary>
    /// Gets or sets whether the left channel phase is inverted.
    /// </summary>
    public bool PhaseInvertLeft
    {
        get => _phaseInvertLeft;
        set => _phaseInvertLeft = value;
    }

    /// <summary>
    /// Gets or sets whether the right channel phase is inverted.
    /// </summary>
    public bool PhaseInvertRight
    {
        get => _phaseInvertRight;
        set => _phaseInvertRight = value;
    }

    /// <summary>
    /// Gets or sets whether phase is inverted for both channels.
    /// </summary>
    public bool PhaseInvert
    {
        get => _phaseInvertLeft && _phaseInvertRight;
        set
        {
            _phaseInvertLeft = value;
            _phaseInvertRight = value;
        }
    }

    /// <summary>
    /// Gets or sets whether the high pass filter is enabled.
    /// </summary>
    public bool HighPassEnabled
    {
        get => _highPassEnabled;
        set => _highPassEnabled = value;
    }

    /// <summary>
    /// Gets or sets the high pass filter frequency (20 - 500 Hz).
    /// </summary>
    public float HighPassFrequency
    {
        get => _highPassFrequency;
        set
        {
            _highPassFrequency = Math.Clamp(value, 20f, 500f);
            UpdateHighPassCoefficients();
        }
    }

    /// <summary>
    /// Gets or sets whether the input section is bypassed.
    /// </summary>
    public bool InputSectionBypassed
    {
        get => _inputSectionBypassed;
        set => _inputSectionBypassed = value;
    }

    #endregion

    #region Gate Section Properties

    /// <summary>
    /// Gets or sets whether the noise gate is enabled.
    /// </summary>
    public bool GateEnabled
    {
        get => _gateEnabled;
        set => _gateEnabled = value;
    }

    /// <summary>
    /// Gets or sets the gate threshold in dB (-80 to 0).
    /// </summary>
    public float GateThreshold
    {
        get => _gateThreshold;
        set => _gateThreshold = Math.Clamp(value, -80f, 0f);
    }

    /// <summary>
    /// Gets or sets the gate attack time in seconds (0.0001 - 0.1).
    /// </summary>
    public float GateAttack
    {
        get => _gateAttack;
        set => _gateAttack = Math.Clamp(value, 0.0001f, 0.1f);
    }

    /// <summary>
    /// Gets or sets the gate release time in seconds (0.001 - 5.0).
    /// </summary>
    public float GateRelease
    {
        get => _gateRelease;
        set => _gateRelease = Math.Clamp(value, 0.001f, 5f);
    }

    /// <summary>
    /// Gets or sets the gate range/floor in dB (-80 to 0).
    /// </summary>
    public float GateRange
    {
        get => _gateRange;
        set => _gateRange = Math.Clamp(value, -80f, 0f);
    }

    /// <summary>
    /// Gets or sets the gate hold time in seconds (0 - 2.0).
    /// </summary>
    public float GateHold
    {
        get => _gateHold;
        set => _gateHold = Math.Clamp(value, 0f, 2f);
    }

    /// <summary>
    /// Gets or sets whether the gate sidechain filter is enabled.
    /// </summary>
    public bool GateSidechainFilterEnabled
    {
        get => _gateSidechainFilterEnabled;
        set => _gateSidechainFilterEnabled = value;
    }

    /// <summary>
    /// Gets or sets the gate sidechain filter frequency.
    /// </summary>
    public float GateSidechainFilterFrequency
    {
        get => _gateSidechainFilterFreq;
        set
        {
            _gateSidechainFilterFreq = Math.Clamp(value, 20f, 20000f);
            UpdateGateSidechainFilterCoefficients();
        }
    }

    /// <summary>
    /// Gets or sets whether the gate section is bypassed.
    /// </summary>
    public bool GateSectionBypassed
    {
        get => _gateSectionBypassed;
        set => _gateSectionBypassed = value;
    }

    #endregion

    #region EQ Section Properties

    /// <summary>
    /// Gets or sets whether the EQ is enabled.
    /// </summary>
    public bool EqEnabled
    {
        get => _eqEnabled;
        set => _eqEnabled = value;
    }

    /// <summary>
    /// Gets or sets the low shelf frequency (20 - 500 Hz).
    /// </summary>
    public float EqLowFrequency
    {
        get => _eqLowFreq;
        set { _eqLowFreq = Math.Clamp(value, 20f, 500f); UpdateEqCoefficients(); }
    }

    /// <summary>
    /// Gets or sets the low shelf gain (-24 to +24 dB).
    /// </summary>
    public float EqLowGain
    {
        get => _eqLowGain;
        set { _eqLowGain = Math.Clamp(value, -24f, 24f); UpdateEqCoefficients(); }
    }

    /// <summary>
    /// Gets or sets the low shelf Q (0.1 - 10).
    /// </summary>
    public float EqLowQ
    {
        get => _eqLowQ;
        set { _eqLowQ = Math.Clamp(value, 0.1f, 10f); UpdateEqCoefficients(); }
    }

    /// <summary>
    /// Gets or sets the low-mid parametric frequency (100 - 2000 Hz).
    /// </summary>
    public float EqLowMidFrequency
    {
        get => _eqLowMidFreq;
        set { _eqLowMidFreq = Math.Clamp(value, 100f, 2000f); UpdateEqCoefficients(); }
    }

    /// <summary>
    /// Gets or sets the low-mid parametric gain (-24 to +24 dB).
    /// </summary>
    public float EqLowMidGain
    {
        get => _eqLowMidGain;
        set { _eqLowMidGain = Math.Clamp(value, -24f, 24f); UpdateEqCoefficients(); }
    }

    /// <summary>
    /// Gets or sets the low-mid parametric Q (0.1 - 10).
    /// </summary>
    public float EqLowMidQ
    {
        get => _eqLowMidQ;
        set { _eqLowMidQ = Math.Clamp(value, 0.1f, 10f); UpdateEqCoefficients(); }
    }

    /// <summary>
    /// Gets or sets the high-mid parametric frequency (500 - 8000 Hz).
    /// </summary>
    public float EqHighMidFrequency
    {
        get => _eqHighMidFreq;
        set { _eqHighMidFreq = Math.Clamp(value, 500f, 8000f); UpdateEqCoefficients(); }
    }

    /// <summary>
    /// Gets or sets the high-mid parametric gain (-24 to +24 dB).
    /// </summary>
    public float EqHighMidGain
    {
        get => _eqHighMidGain;
        set { _eqHighMidGain = Math.Clamp(value, -24f, 24f); UpdateEqCoefficients(); }
    }

    /// <summary>
    /// Gets or sets the high-mid parametric Q (0.1 - 10).
    /// </summary>
    public float EqHighMidQ
    {
        get => _eqHighMidQ;
        set { _eqHighMidQ = Math.Clamp(value, 0.1f, 10f); UpdateEqCoefficients(); }
    }

    /// <summary>
    /// Gets or sets the high shelf frequency (2000 - 20000 Hz).
    /// </summary>
    public float EqHighFrequency
    {
        get => _eqHighFreq;
        set { _eqHighFreq = Math.Clamp(value, 2000f, 20000f); UpdateEqCoefficients(); }
    }

    /// <summary>
    /// Gets or sets the high shelf gain (-24 to +24 dB).
    /// </summary>
    public float EqHighGain
    {
        get => _eqHighGain;
        set { _eqHighGain = Math.Clamp(value, -24f, 24f); UpdateEqCoefficients(); }
    }

    /// <summary>
    /// Gets or sets the high shelf Q (0.1 - 10).
    /// </summary>
    public float EqHighQ
    {
        get => _eqHighQ;
        set { _eqHighQ = Math.Clamp(value, 0.1f, 10f); UpdateEqCoefficients(); }
    }

    /// <summary>
    /// Gets or sets whether the EQ section is bypassed.
    /// </summary>
    public bool EqSectionBypassed
    {
        get => _eqSectionBypassed;
        set => _eqSectionBypassed = value;
    }

    #endregion

    #region Compressor Section Properties

    /// <summary>
    /// Gets or sets whether the compressor is enabled.
    /// </summary>
    public bool CompressorEnabled
    {
        get => _compressorEnabled;
        set => _compressorEnabled = value;
    }

    /// <summary>
    /// Gets or sets the compressor threshold in dB (-60 to 0).
    /// </summary>
    public float CompressorThreshold
    {
        get => _compThreshold;
        set => _compThreshold = Math.Clamp(value, -60f, 0f);
    }

    /// <summary>
    /// Gets or sets the compressor ratio (1 - 20).
    /// </summary>
    public float CompressorRatio
    {
        get => _compRatio;
        set => _compRatio = Math.Clamp(value, 1f, 20f);
    }

    /// <summary>
    /// Gets or sets the compressor attack time in seconds (0.0001 - 1.0).
    /// </summary>
    public float CompressorAttack
    {
        get => _compAttack;
        set => _compAttack = Math.Clamp(value, 0.0001f, 1f);
    }

    /// <summary>
    /// Gets or sets the compressor release time in seconds (0.001 - 5.0).
    /// </summary>
    public float CompressorRelease
    {
        get => _compRelease;
        set => _compRelease = Math.Clamp(value, 0.001f, 5f);
    }

    /// <summary>
    /// Gets or sets the compressor makeup gain in dB (0 - 48).
    /// </summary>
    public float CompressorMakeupGain
    {
        get => _compMakeupGain;
        set => _compMakeupGain = Math.Clamp(value, 0f, 48f);
    }

    /// <summary>
    /// Gets or sets the compressor knee width in dB (0 - 20). 0 = hard knee.
    /// </summary>
    public float CompressorKneeWidth
    {
        get => _compKneeWidth;
        set => _compKneeWidth = Math.Clamp(value, 0f, 20f);
    }

    /// <summary>
    /// Gets or sets whether the compressor sidechain filter is enabled.
    /// </summary>
    public bool CompressorSidechainFilterEnabled
    {
        get => _compSidechainFilterEnabled;
        set => _compSidechainFilterEnabled = value;
    }

    /// <summary>
    /// Gets or sets the compressor sidechain filter frequency.
    /// </summary>
    public float CompressorSidechainFilterFrequency
    {
        get => _compSidechainFilterFreq;
        set
        {
            _compSidechainFilterFreq = Math.Clamp(value, 20f, 20000f);
            UpdateCompressorSidechainFilterCoefficients();
        }
    }

    /// <summary>
    /// Gets or sets the compressor sidechain filter type.
    /// </summary>
    public FilterType CompressorSidechainFilterType
    {
        get => _compSidechainFilterType;
        set
        {
            _compSidechainFilterType = value;
            UpdateCompressorSidechainFilterCoefficients();
        }
    }

    /// <summary>
    /// Gets or sets the external sidechain source.
    /// </summary>
    public ISampleProvider? ExternalSidechain
    {
        get => _externalSidechain;
        set => _externalSidechain = value;
    }

    /// <summary>
    /// Gets or sets whether to listen to the sidechain signal.
    /// </summary>
    public bool SidechainListen
    {
        get => _sidechainListen;
        set => _sidechainListen = value;
    }

    /// <summary>
    /// Gets the current compressor gain reduction in dB.
    /// </summary>
    public float CompressorGainReductionDb => _compGainReductionDb;

    /// <summary>
    /// Gets or sets whether the compressor section is bypassed.
    /// </summary>
    public bool CompressorSectionBypassed
    {
        get => _compSectionBypassed;
        set => _compSectionBypassed = value;
    }

    #endregion

    #region De-Esser Section Properties

    /// <summary>
    /// Gets or sets whether the de-esser is enabled.
    /// </summary>
    public bool DeEsserEnabled
    {
        get => _deEsserEnabled;
        set => _deEsserEnabled = value;
    }

    /// <summary>
    /// Gets or sets the de-esser center frequency (2000 - 16000 Hz).
    /// </summary>
    public float DeEsserFrequency
    {
        get => _deEsserFrequency;
        set { _deEsserFrequency = Math.Clamp(value, 2000f, 16000f); UpdateDeEsserCoefficients(); }
    }

    /// <summary>
    /// Gets or sets the de-esser threshold in dB (-60 to 0).
    /// </summary>
    public float DeEsserThreshold
    {
        get => _deEsserThreshold;
        set => _deEsserThreshold = Math.Clamp(value, -60f, 0f);
    }

    /// <summary>
    /// Gets or sets the de-esser reduction amount in dB (0 - 24).
    /// </summary>
    public float DeEsserReduction
    {
        get => _deEsserReduction;
        set => _deEsserReduction = Math.Clamp(value, 0f, 24f);
    }

    /// <summary>
    /// Gets or sets the de-esser bandwidth in Hz (500 - 8000).
    /// </summary>
    public float DeEsserBandwidth
    {
        get => _deEsserBandwidth;
        set { _deEsserBandwidth = Math.Clamp(value, 500f, 8000f); UpdateDeEsserCoefficients(); }
    }

    /// <summary>
    /// Gets or sets whether the de-esser section is bypassed.
    /// </summary>
    public bool DeEsserSectionBypassed
    {
        get => _deEsserSectionBypassed;
        set => _deEsserSectionBypassed = value;
    }

    #endregion

    #region Saturation Section Properties

    /// <summary>
    /// Gets or sets the saturation mode.
    /// </summary>
    public SaturationMode SaturationMode
    {
        get => _saturationMode;
        set => _saturationMode = value;
    }

    /// <summary>
    /// Gets or sets the saturation drive amount (0 - 1).
    /// </summary>
    public float SaturationDrive
    {
        get => _saturationDrive;
        set => _saturationDrive = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Gets or sets the saturation dry/wet mix (0 - 1).
    /// </summary>
    public float SaturationMix
    {
        get => _saturationMix;
        set => _saturationMix = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Gets or sets whether the saturation section is bypassed.
    /// </summary>
    public bool SaturationSectionBypassed
    {
        get => _saturationSectionBypassed;
        set => _saturationSectionBypassed = value;
    }

    #endregion

    #region Width Section Properties

    /// <summary>
    /// Gets or sets the stereo width (0 = mono, 1 = normal, 2 = wide).
    /// </summary>
    public float StereoWidth
    {
        get => _stereoWidth;
        set => _stereoWidth = Math.Clamp(value, 0f, 2f);
    }

    /// <summary>
    /// Gets or sets the frequency below which audio is mono (50 - 500 Hz).
    /// </summary>
    public float MonoLowFrequency
    {
        get => _monoLowFreq;
        set => _monoLowFreq = Math.Clamp(value, 50f, 500f);
    }

    /// <summary>
    /// Gets or sets whether the width section is bypassed.
    /// </summary>
    public bool WidthSectionBypassed
    {
        get => _widthSectionBypassed;
        set => _widthSectionBypassed = value;
    }

    #endregion

    #region Output Section Properties

    /// <summary>
    /// Gets or sets the pan position (-1 = full left, 0 = center, 1 = full right).
    /// </summary>
    public float Pan
    {
        get => _pan;
        set => _pan = Math.Clamp(value, -1f, 1f);
    }

    /// <summary>
    /// Gets or sets the output level (0 - 2).
    /// </summary>
    public float OutputLevel
    {
        get => _outputLevel;
        set => _outputLevel = Math.Clamp(value, 0f, 2f);
    }

    /// <summary>
    /// Gets or sets the fader level (0 - 1.5).
    /// </summary>
    public float Fader
    {
        get => _fader;
        set => _fader = Math.Clamp(value, 0f, 1.5f);
    }

    /// <summary>
    /// Gets or sets whether the channel is muted.
    /// </summary>
    public bool Mute
    {
        get => _mute;
        set => _mute = value;
    }

    /// <summary>
    /// Gets or sets whether the channel is soloed.
    /// </summary>
    public bool Solo
    {
        get => _solo;
        set => _solo = value;
    }

    /// <summary>
    /// Gets or sets whether the output section is bypassed.
    /// </summary>
    public bool OutputSectionBypassed
    {
        get => _outputSectionBypassed;
        set => _outputSectionBypassed = value;
    }

    #endregion

    #region Aux Sends Properties

    /// <summary>
    /// Gets the aux sends configuration.
    /// </summary>
    public IReadOnlyList<AuxSend> AuxSends => _auxSends;

    /// <summary>
    /// Sets the level of an aux send.
    /// </summary>
    /// <param name="index">Send index (0-3).</param>
    /// <param name="level">Send level (0-1).</param>
    public void SetAuxSendLevel(int index, float level)
    {
        if (index >= 0 && index < MaxAuxSends)
        {
            _auxSends[index].Level = Math.Clamp(level, 0f, 1f);
        }
    }

    /// <summary>
    /// Sets whether an aux send is pre-fader.
    /// </summary>
    /// <param name="index">Send index (0-3).</param>
    /// <param name="preFader">True for pre-fader, false for post-fader.</param>
    public void SetAuxSendPreFader(int index, bool preFader)
    {
        if (index >= 0 && index < MaxAuxSends)
        {
            _auxSends[index].PreFader = preFader;
        }
    }

    /// <summary>
    /// Gets the signal buffer for an aux send.
    /// </summary>
    /// <param name="index">Send index (0-3).</param>
    /// <returns>The send buffer, or null if not available.</returns>
    public float[]? GetAuxSendBuffer(int index)
    {
        if (index >= 0 && index < MaxAuxSends && _auxSends[index].Enabled && _auxSends[index].Level > 0)
        {
            return _auxSends[index].Buffer;
        }
        return null;
    }

    #endregion

    #region Insert Points Properties

    /// <summary>
    /// Gets or sets the pre-EQ insert effect chain.
    /// </summary>
    public ISampleProvider? PreEqInsert
    {
        get => _preEqInsert;
        set => _preEqInsert = value;
    }

    /// <summary>
    /// Gets or sets whether the pre-EQ insert is enabled.
    /// </summary>
    public bool PreEqInsertEnabled
    {
        get => _preEqInsertEnabled;
        set => _preEqInsertEnabled = value;
    }

    /// <summary>
    /// Gets or sets the post-EQ insert effect chain.
    /// </summary>
    public ISampleProvider? PostEqInsert
    {
        get => _postEqInsert;
        set => _postEqInsert = value;
    }

    /// <summary>
    /// Gets or sets whether the post-EQ insert is enabled.
    /// </summary>
    public bool PostEqInsertEnabled
    {
        get => _postEqInsertEnabled;
        set => _postEqInsertEnabled = value;
    }

    #endregion

    #region Metering Properties

    /// <summary>
    /// Gets the input peak level for the left channel.
    /// </summary>
    public float InputPeakLeft => _inputPeakLeft;

    /// <summary>
    /// Gets the input peak level for the right channel.
    /// </summary>
    public float InputPeakRight => _inputPeakRight;

    /// <summary>
    /// Gets the output peak level for the left channel.
    /// </summary>
    public float OutputPeakLeft => _outputPeakLeft;

    /// <summary>
    /// Gets the output peak level for the right channel.
    /// </summary>
    public float OutputPeakRight => _outputPeakRight;

    /// <summary>
    /// Gets the input RMS level for the left channel.
    /// </summary>
    public float InputRmsLeft => _inputRmsLeft;

    /// <summary>
    /// Gets the input RMS level for the right channel.
    /// </summary>
    public float InputRmsRight => _inputRmsRight;

    /// <summary>
    /// Gets the output RMS level for the left channel.
    /// </summary>
    public float OutputRmsLeft => _outputRmsLeft;

    /// <summary>
    /// Gets the output RMS level for the right channel.
    /// </summary>
    public float OutputRmsRight => _outputRmsRight;

    /// <summary>
    /// Gets the input peak level in dB for the left channel.
    /// </summary>
    public float InputPeakLeftDb => _inputPeakLeft > 0 ? 20f * MathF.Log10(_inputPeakLeft) : float.NegativeInfinity;

    /// <summary>
    /// Gets the input peak level in dB for the right channel.
    /// </summary>
    public float InputPeakRightDb => _inputPeakRight > 0 ? 20f * MathF.Log10(_inputPeakRight) : float.NegativeInfinity;

    /// <summary>
    /// Gets the output peak level in dB for the left channel.
    /// </summary>
    public float OutputPeakLeftDb => _outputPeakLeft > 0 ? 20f * MathF.Log10(_outputPeakLeft) : float.NegativeInfinity;

    /// <summary>
    /// Gets the output peak level in dB for the right channel.
    /// </summary>
    public float OutputPeakRightDb => _outputPeakRight > 0 ? 20f * MathF.Log10(_outputPeakRight) : float.NegativeInfinity;

    /// <summary>
    /// Resets all meters.
    /// </summary>
    public void ResetMeters()
    {
        _inputPeakLeft = 0;
        _inputPeakRight = 0;
        _outputPeakLeft = 0;
        _outputPeakRight = 0;
        _inputRmsLeft = 0;
        _inputRmsRight = 0;
        _outputRmsLeft = 0;
        _outputRmsRight = 0;
        _inputRmsAccumLeft = 0;
        _inputRmsAccumRight = 0;
        _outputRmsAccumLeft = 0;
        _outputRmsAccumRight = 0;
        _rmsSampleCount = 0;
    }

    #endregion

    #region Console Style Properties

    /// <summary>
    /// Gets or sets the console emulation style.
    /// </summary>
    public ConsoleStyle ConsoleStyle
    {
        get => _consoleStyle;
        set
        {
            _consoleStyle = value;
            ApplyConsoleStyle(value);
        }
    }

    #endregion

    #region Channel Linking

    /// <summary>
    /// Gets or sets the linked channel for stereo linking.
    /// </summary>
    public ChannelStrip? LinkedChannel
    {
        get => _linkedChannel;
        set => _linkedChannel = value;
    }

    /// <summary>
    /// Links this channel with another for stereo operation.
    /// </summary>
    /// <param name="other">The channel to link with.</param>
    public void LinkWith(ChannelStrip other)
    {
        if (other == this) return;
        _linkedChannel = other;
        if (other != null)
        {
            other._linkedChannel = this;
        }
    }

    /// <summary>
    /// Unlinks this channel from its linked partner.
    /// </summary>
    public void Unlink()
    {
        if (_linkedChannel != null)
        {
            _linkedChannel._linkedChannel = null;
            _linkedChannel = null;
        }
    }

    #endregion

    #region A/B Comparison

    /// <summary>
    /// Gets whether state A is currently active.
    /// </summary>
    public bool IsStateA => _isStateA;

    /// <summary>
    /// Stores the current settings to state A.
    /// </summary>
    public void StoreStateA()
    {
        _stateA = CaptureCurrentState();
    }

    /// <summary>
    /// Stores the current settings to state B.
    /// </summary>
    public void StoreStateB()
    {
        _stateB = CaptureCurrentState();
    }

    /// <summary>
    /// Switches to state A.
    /// </summary>
    public void SwitchToStateA()
    {
        if (_stateA != null)
        {
            ApplyState(_stateA);
            _isStateA = true;
        }
    }

    /// <summary>
    /// Switches to state B.
    /// </summary>
    public void SwitchToStateB()
    {
        if (_stateB != null)
        {
            ApplyState(_stateB);
            _isStateA = false;
        }
    }

    /// <summary>
    /// Toggles between state A and B.
    /// </summary>
    public void ToggleAB()
    {
        if (_isStateA)
            SwitchToStateB();
        else
            SwitchToStateA();
    }

    /// <summary>
    /// Copies state A to state B.
    /// </summary>
    public void CopyAToB()
    {
        _stateB = _stateA?.Clone();
    }

    /// <summary>
    /// Copies state B to state A.
    /// </summary>
    public void CopyBToA()
    {
        _stateA = _stateB?.Clone();
    }

    #endregion

    #region Preset Management

    /// <summary>
    /// Saves the current channel strip settings to a preset.
    /// </summary>
    /// <returns>The channel strip preset.</returns>
    public ChannelStripPreset SavePreset(string name = "Custom Preset")
    {
        var preset = new ChannelStripPreset
        {
            Name = name,
            InputGainDb = _inputGainDb,
            PhaseInvertLeft = _phaseInvertLeft,
            PhaseInvertRight = _phaseInvertRight,
            HighPassEnabled = _highPassEnabled,
            HighPassFrequency = _highPassFrequency,
            GateEnabled = _gateEnabled,
            GateThreshold = _gateThreshold,
            GateAttack = _gateAttack,
            GateRelease = _gateRelease,
            GateRange = _gateRange,
            GateHold = _gateHold,
            EqEnabled = _eqEnabled,
            EqLowFrequency = _eqLowFreq,
            EqLowGain = _eqLowGain,
            EqLowQ = _eqLowQ,
            EqLowMidFrequency = _eqLowMidFreq,
            EqLowMidGain = _eqLowMidGain,
            EqLowMidQ = _eqLowMidQ,
            EqHighMidFrequency = _eqHighMidFreq,
            EqHighMidGain = _eqHighMidGain,
            EqHighMidQ = _eqHighMidQ,
            EqHighFrequency = _eqHighFreq,
            EqHighGain = _eqHighGain,
            EqHighQ = _eqHighQ,
            CompressorEnabled = _compressorEnabled,
            CompressorThreshold = _compThreshold,
            CompressorRatio = _compRatio,
            CompressorAttack = _compAttack,
            CompressorRelease = _compRelease,
            CompressorMakeupGain = _compMakeupGain,
            CompressorKneeWidth = _compKneeWidth,
            DeEsserEnabled = _deEsserEnabled,
            DeEsserFrequency = _deEsserFrequency,
            DeEsserThreshold = _deEsserThreshold,
            DeEsserReduction = _deEsserReduction,
            SaturationMode = _saturationMode,
            SaturationDrive = _saturationDrive,
            SaturationMix = _saturationMix,
            StereoWidth = _stereoWidth,
            MonoLowFrequency = _monoLowFreq,
            Pan = _pan,
            OutputLevel = _outputLevel,
            ConsoleStyle = _consoleStyle
        };

        return preset;
    }

    /// <summary>
    /// Loads settings from a preset.
    /// </summary>
    /// <param name="preset">The preset to load.</param>
    public void LoadPreset(ChannelStripPreset preset)
    {
        if (preset == null) return;

        InputGainDb = preset.InputGainDb;
        PhaseInvertLeft = preset.PhaseInvertLeft;
        PhaseInvertRight = preset.PhaseInvertRight;
        HighPassEnabled = preset.HighPassEnabled;
        HighPassFrequency = preset.HighPassFrequency;
        GateEnabled = preset.GateEnabled;
        GateThreshold = preset.GateThreshold;
        GateAttack = preset.GateAttack;
        GateRelease = preset.GateRelease;
        GateRange = preset.GateRange;
        GateHold = preset.GateHold;
        EqEnabled = preset.EqEnabled;
        EqLowFrequency = preset.EqLowFrequency;
        EqLowGain = preset.EqLowGain;
        EqLowQ = preset.EqLowQ;
        EqLowMidFrequency = preset.EqLowMidFrequency;
        EqLowMidGain = preset.EqLowMidGain;
        EqLowMidQ = preset.EqLowMidQ;
        EqHighMidFrequency = preset.EqHighMidFrequency;
        EqHighMidGain = preset.EqHighMidGain;
        EqHighMidQ = preset.EqHighMidQ;
        EqHighFrequency = preset.EqHighFrequency;
        EqHighGain = preset.EqHighGain;
        EqHighQ = preset.EqHighQ;
        CompressorEnabled = preset.CompressorEnabled;
        CompressorThreshold = preset.CompressorThreshold;
        CompressorRatio = preset.CompressorRatio;
        CompressorAttack = preset.CompressorAttack;
        CompressorRelease = preset.CompressorRelease;
        CompressorMakeupGain = preset.CompressorMakeupGain;
        CompressorKneeWidth = preset.CompressorKneeWidth;
        DeEsserEnabled = preset.DeEsserEnabled;
        DeEsserFrequency = preset.DeEsserFrequency;
        DeEsserThreshold = preset.DeEsserThreshold;
        DeEsserReduction = preset.DeEsserReduction;
        SaturationMode = preset.SaturationMode;
        SaturationDrive = preset.SaturationDrive;
        SaturationMix = preset.SaturationMix;
        StereoWidth = preset.StereoWidth;
        MonoLowFrequency = preset.MonoLowFrequency;
        Pan = preset.Pan;
        OutputLevel = preset.OutputLevel;
        ConsoleStyle = preset.ConsoleStyle;
    }

    #endregion

    #region Audio Processing

    /// <inheritdoc />
    public int Read(float[] buffer, int offset, int count)
    {
        // Read from source
        int samplesRead = _source.Read(buffer, offset, count);
        if (samplesRead == 0) return 0;

        int frames = samplesRead / _channels;

        // Update input meters
        UpdateInputMeters(buffer, offset, samplesRead);

        // Process signal chain
        ProcessInputSection(buffer, offset, samplesRead);
        ProcessGateSection(buffer, offset, samplesRead);

        // Pre-EQ Insert
        if (_preEqInsertEnabled && _preEqInsert != null)
        {
            ProcessInsert(_preEqInsert, buffer, offset, samplesRead);
        }

        ProcessEqSection(buffer, offset, samplesRead);

        // Post-EQ Insert
        if (_postEqInsertEnabled && _postEqInsert != null)
        {
            ProcessInsert(_postEqInsert, buffer, offset, samplesRead);
        }

        ProcessCompressorSection(buffer, offset, samplesRead);
        ProcessDeEsserSection(buffer, offset, samplesRead);
        ProcessSaturationSection(buffer, offset, samplesRead);
        ProcessWidthSection(buffer, offset, samplesRead);

        // Process pre-fader sends
        ProcessPreFaderSends(buffer, offset, samplesRead);

        ProcessOutputSection(buffer, offset, samplesRead);

        // Process post-fader sends
        ProcessPostFaderSends(buffer, offset, samplesRead);

        // Update output meters
        UpdateOutputMeters(buffer, offset, samplesRead);

        return samplesRead;
    }

    private void ProcessInputSection(float[] buffer, int offset, int count)
    {
        if (_inputSectionBypassed) return;

        for (int i = 0; i < count; i += _channels)
        {
            // Smooth gain transition
            _inputGainLinear = _inputGainLinear * GainSmoothingCoeff + _targetInputGain * (1f - GainSmoothingCoeff);

            for (int ch = 0; ch < _channels; ch++)
            {
                int idx = offset + i + ch;
                float sample = buffer[idx];

                // Apply input gain
                sample *= _inputGainLinear;

                // Apply phase invert
                if ((ch == 0 && _phaseInvertLeft) || (ch == 1 && _phaseInvertRight))
                {
                    sample = -sample;
                }

                // Apply high pass filter
                if (_highPassEnabled)
                {
                    sample = ProcessBiquad(ref _highPassStates[ch], _highPassCoeffs, sample);
                }

                buffer[idx] = sample;
            }
        }
    }

    private void ProcessGateSection(float[] buffer, int offset, int count)
    {
        if (_gateSectionBypassed || !_gateEnabled) return;

        float attackCoeff = MathF.Exp(-1f / (_gateAttack * _sampleRate));
        float releaseCoeff = MathF.Exp(-1f / (_gateRelease * _sampleRate));
        float rangeLinear = MathF.Pow(10f, _gateRange / 20f);
        int holdSamples = (int)(_gateHold * _sampleRate);

        for (int i = 0; i < count; i += _channels)
        {
            for (int ch = 0; ch < _channels; ch++)
            {
                int idx = offset + i + ch;
                float input = buffer[idx];

                // Sidechain filter if enabled
                float detectSample = input;
                if (_gateSidechainFilterEnabled)
                {
                    detectSample = ProcessBiquad(ref _gateSidechainFilterStates[ch], _gateSidechainFilterCoeffs, input);
                }

                // Envelope detection
                float inputAbs = MathF.Abs(detectSample);
                float coeff = inputAbs > _gateEnvelope[ch] ? attackCoeff : releaseCoeff;
                _gateEnvelope[ch] = inputAbs + coeff * (_gateEnvelope[ch] - inputAbs);

                // Convert to dB
                float inputDb = 20f * MathF.Log10(_gateEnvelope[ch] + 1e-6f);

                // Calculate target gate gain
                float targetGain = 1f;
                if (inputDb < _gateThreshold)
                {
                    targetGain = rangeLinear;
                    _gateHoldCounter[ch] = 0;
                }
                else
                {
                    _gateHoldCounter[ch]++;
                    if (_gateHoldCounter[ch] < holdSamples)
                    {
                        targetGain = _gateState[ch]; // Hold current state
                    }
                }

                // Smooth gate transitions
                float gateCoeff = targetGain > _gateState[ch] ? attackCoeff : releaseCoeff;
                _gateState[ch] = targetGain + gateCoeff * (_gateState[ch] - targetGain);

                buffer[idx] = input * _gateState[ch];
            }
        }
    }

    private void ProcessEqSection(float[] buffer, int offset, int count)
    {
        if (_eqSectionBypassed || !_eqEnabled) return;

        for (int i = 0; i < count; i += _channels)
        {
            for (int ch = 0; ch < _channels; ch++)
            {
                int idx = offset + i + ch;
                float sample = buffer[idx];

                // Low shelf
                if (MathF.Abs(_eqLowGain) > 0.01f)
                {
                    sample = ProcessBiquad(ref _eqLowStates[ch], _eqLowCoeffs, sample);
                }

                // Low-mid parametric
                if (MathF.Abs(_eqLowMidGain) > 0.01f)
                {
                    sample = ProcessBiquad(ref _eqLowMidStates[ch], _eqLowMidCoeffs, sample);
                }

                // High-mid parametric
                if (MathF.Abs(_eqHighMidGain) > 0.01f)
                {
                    sample = ProcessBiquad(ref _eqHighMidStates[ch], _eqHighMidCoeffs, sample);
                }

                // High shelf
                if (MathF.Abs(_eqHighGain) > 0.01f)
                {
                    sample = ProcessBiquad(ref _eqHighStates[ch], _eqHighCoeffs, sample);
                }

                buffer[idx] = sample;
            }
        }
    }

    private void ProcessCompressorSection(float[] buffer, int offset, int count)
    {
        if (_compSectionBypassed || !_compressorEnabled) return;

        float attackCoeff = MathF.Exp(-1f / (_compAttack * _sampleRate));
        float releaseCoeff = MathF.Exp(-1f / (_compRelease * _sampleRate));
        float makeupGainLinear = MathF.Pow(10f, _compMakeupGain / 20f);

        // Read external sidechain if connected
        bool useExternalSidechain = _externalSidechain != null;
        if (useExternalSidechain)
        {
            EnsureBufferSize(ref _sidechainBuffer, count);
            int scRead = _externalSidechain!.Read(_sidechainBuffer, 0, count);
            if (scRead < count)
            {
                Array.Clear(_sidechainBuffer, scRead, count - scRead);
            }
        }

        float maxGainReduction = 0f;

        for (int i = 0; i < count; i += _channels)
        {
            // Get detection signal
            float detectLevel = 0f;
            for (int ch = 0; ch < _channels; ch++)
            {
                float detectSample;
                if (useExternalSidechain)
                {
                    detectSample = _sidechainBuffer[i + ch];
                }
                else
                {
                    detectSample = buffer[offset + i + ch];
                }

                // Apply sidechain filter
                if (_compSidechainFilterEnabled)
                {
                    detectSample = ProcessBiquad(ref _compSidechainFilterStates[ch], _compSidechainFilterCoeffs, detectSample);
                }

                detectLevel = MathF.Max(detectLevel, MathF.Abs(detectSample));
            }

            // Envelope follower
            float coeff = detectLevel > _compEnvelope[0] ? attackCoeff : releaseCoeff;
            _compEnvelope[0] = detectLevel + coeff * (_compEnvelope[0] - detectLevel);

            // Convert to dB
            float inputDb = 20f * MathF.Log10(_compEnvelope[0] + 1e-6f);

            // Calculate gain reduction
            float gainReductionDb = CalculateCompressorGainReduction(inputDb, _compThreshold, _compRatio, _compKneeWidth);

            // Convert to linear
            float targetGain = MathF.Pow(10f, gainReductionDb / 20f);

            // Smooth gain changes
            float smoothCoeff = targetGain < _compGainSmooth[0] ? attackCoeff : releaseCoeff;
            _compGainSmooth[0] = targetGain + smoothCoeff * (_compGainSmooth[0] - targetGain);

            maxGainReduction = MathF.Max(maxGainReduction, 1f - _compGainSmooth[0]);

            // Apply to all channels
            for (int ch = 0; ch < _channels; ch++)
            {
                int idx = offset + i + ch;

                if (_sidechainListen && useExternalSidechain)
                {
                    // Output the sidechain signal for monitoring
                    buffer[idx] = _sidechainBuffer[i + ch];
                }
                else
                {
                    buffer[idx] *= _compGainSmooth[0] * makeupGainLinear;
                }
            }
        }

        _compGainReductionDb = maxGainReduction > 0 ? 20f * MathF.Log10(1f - maxGainReduction + 0.001f) : 0f;
    }

    private void ProcessDeEsserSection(float[] buffer, int offset, int count)
    {
        if (_deEsserSectionBypassed || !_deEsserEnabled) return;

        float thresholdLinear = MathF.Pow(10f, _deEsserThreshold / 20f);
        float attackCoeff = MathF.Exp(-1f / (0.001f * _sampleRate));
        float releaseCoeff = MathF.Exp(-1f / (0.05f * _sampleRate));

        for (int i = 0; i < count; i += _channels)
        {
            for (int ch = 0; ch < _channels; ch++)
            {
                int idx = offset + i + ch;
                float input = buffer[idx];

                // Detection filter (bandpass)
                float detected = ApplyDeEsserBiquad(input, ch);

                // Envelope follower
                float detectedAbs = MathF.Abs(detected);
                float coeff = detectedAbs > _deEsserEnvelope[ch] ? attackCoeff : releaseCoeff;
                _deEsserEnvelope[ch] = detectedAbs + coeff * (_deEsserEnvelope[ch] - detectedAbs);

                // Calculate gain reduction
                float gainReduction = 1f;
                if (_deEsserEnvelope[ch] > thresholdLinear)
                {
                    float overThresholdDb = 20f * MathF.Log10(_deEsserEnvelope[ch] / thresholdLinear);
                    float reductionDb = MathF.Min(overThresholdDb * (_deEsserReduction / 20f), _deEsserReduction);
                    gainReduction = MathF.Pow(10f, -reductionDb / 20f);
                }

                // Smooth
                float smoothCoeff = gainReduction < _deEsserGainSmooth[ch] ? attackCoeff : releaseCoeff;
                _deEsserGainSmooth[ch] = gainReduction + smoothCoeff * (_deEsserGainSmooth[ch] - gainReduction);

                // Apply (wideband for simplicity)
                buffer[idx] = input * _deEsserGainSmooth[ch];
            }
        }
    }

    private void ProcessSaturationSection(float[] buffer, int offset, int count)
    {
        if (_saturationSectionBypassed || _saturationMode == SaturationMode.Off) return;

        float drive = 1f + _saturationDrive * 3f;

        for (int i = 0; i < count; i += _channels)
        {
            for (int ch = 0; ch < _channels; ch++)
            {
                int idx = offset + i + ch;
                float input = buffer[idx];
                float dry = input;

                // Apply drive
                float driven = input * drive;

                // Apply saturation curve based on mode
                float saturated = _saturationMode switch
                {
                    SaturationMode.Tape => TapeSaturate(driven),
                    SaturationMode.Tube => TubeSaturate(driven),
                    SaturationMode.Transformer => TransformerSaturate(driven),
                    _ => driven
                };

                // Simple lowpass for warmth
                float lpCoeff = 0.9f;
                _satLpState[ch] = _satLpState[ch] * lpCoeff + saturated * (1f - lpCoeff);

                // Mix dry/wet
                buffer[idx] = dry * (1f - _saturationMix) + _satLpState[ch] * _saturationMix;
            }
        }
    }

    private void ProcessWidthSection(float[] buffer, int offset, int count)
    {
        if (_widthSectionBypassed || _channels < 2) return;

        float lpCoeff = MathF.Exp(-2f * MathF.PI * _monoLowFreq / _sampleRate);

        for (int i = 0; i < count; i += _channels)
        {
            float left = buffer[offset + i];
            float right = buffer[offset + i + 1];

            // M/S encoding
            float mid = (left + right) * 0.5f;
            float side = (left - right) * 0.5f;

            // Apply width to side signal
            side *= _stereoWidth;

            // M/S decoding
            buffer[offset + i] = mid + side;
            buffer[offset + i + 1] = mid - side;
        }
    }

    private void ProcessOutputSection(float[] buffer, int offset, int count)
    {
        if (_outputSectionBypassed) return;

        float totalGain = _mute ? 0f : _outputLevel * _fader;

        // Calculate pan gains using constant power panning
        float leftGain = totalGain;
        float rightGain = totalGain;

        if (_channels == 2 && _pan != 0f)
        {
            float panAngle = (_pan + 1f) * MathF.PI * 0.25f;
            leftGain *= MathF.Cos(panAngle);
            rightGain *= MathF.Sin(panAngle);
        }

        for (int i = 0; i < count; i += _channels)
        {
            if (_channels == 1)
            {
                buffer[offset + i] *= leftGain;
            }
            else if (_channels >= 2)
            {
                buffer[offset + i] *= leftGain;
                buffer[offset + i + 1] *= rightGain;
            }
        }
    }

    private void ProcessPreFaderSends(float[] buffer, int offset, int count)
    {
        foreach (var send in _auxSends)
        {
            if (!send.Enabled || !send.PreFader || send.Level <= 0) continue;

            var sendBuffer = send.Buffer;
            EnsureBufferSize(ref sendBuffer, count);
            send.Buffer = sendBuffer;
            float sendGain = send.Level * _outputLevel;

            for (int i = 0; i < count; i++)
            {
                send.Buffer[i] = buffer[offset + i] * sendGain;
            }
        }
    }

    private void ProcessPostFaderSends(float[] buffer, int offset, int count)
    {
        foreach (var send in _auxSends)
        {
            if (!send.Enabled || send.PreFader || send.Level <= 0) continue;

            var sendBuffer = send.Buffer;
            EnsureBufferSize(ref sendBuffer, count);
            send.Buffer = sendBuffer;

            for (int i = 0; i < count; i++)
            {
                send.Buffer[i] = buffer[offset + i] * send.Level;
            }
        }
    }

    private void ProcessInsert(ISampleProvider insert, float[] buffer, int offset, int count)
    {
        EnsureBufferSize(ref _insertBuffer, count);
        Array.Copy(buffer, offset, _insertBuffer, 0, count);

        // Create a buffer provider for the insert
        var insertRead = insert.Read(_insertBuffer, 0, count);
        if (insertRead > 0)
        {
            Array.Copy(_insertBuffer, 0, buffer, offset, insertRead);
        }
    }

    #endregion

    #region Metering

    private void UpdateInputMeters(float[] buffer, int offset, int count)
    {
        float maxLeft = 0f;
        float maxRight = 0f;

        for (int i = 0; i < count; i += _channels)
        {
            float left = MathF.Abs(buffer[offset + i]);
            maxLeft = MathF.Max(maxLeft, left);
            _inputRmsAccumLeft += left * left;

            if (_channels >= 2)
            {
                float right = MathF.Abs(buffer[offset + i + 1]);
                maxRight = MathF.Max(maxRight, right);
                _inputRmsAccumRight += right * right;
            }
        }

        _inputPeakLeft = MathF.Max(_inputPeakLeft * 0.9995f, maxLeft);
        _inputPeakRight = MathF.Max(_inputPeakRight * 0.9995f, maxRight);
    }

    private void UpdateOutputMeters(float[] buffer, int offset, int count)
    {
        float maxLeft = 0f;
        float maxRight = 0f;

        for (int i = 0; i < count; i += _channels)
        {
            float left = MathF.Abs(buffer[offset + i]);
            maxLeft = MathF.Max(maxLeft, left);
            _outputRmsAccumLeft += left * left;

            if (_channels >= 2)
            {
                float right = MathF.Abs(buffer[offset + i + 1]);
                maxRight = MathF.Max(maxRight, right);
                _outputRmsAccumRight += right * right;
            }
        }

        _outputPeakLeft = MathF.Max(_outputPeakLeft * 0.9995f, maxLeft);
        _outputPeakRight = MathF.Max(_outputPeakRight * 0.9995f, maxRight);

        _rmsSampleCount += count / _channels;
        if (_rmsSampleCount >= RmsWindowSize)
        {
            _inputRmsLeft = MathF.Sqrt(_inputRmsAccumLeft / _rmsSampleCount);
            _inputRmsRight = MathF.Sqrt(_inputRmsAccumRight / _rmsSampleCount);
            _outputRmsLeft = MathF.Sqrt(_outputRmsAccumLeft / _rmsSampleCount);
            _outputRmsRight = MathF.Sqrt(_outputRmsAccumRight / _rmsSampleCount);

            _inputRmsAccumLeft = 0;
            _inputRmsAccumRight = 0;
            _outputRmsAccumLeft = 0;
            _outputRmsAccumRight = 0;
            _rmsSampleCount = 0;
        }
    }

    #endregion

    #region Filter Coefficient Calculations

    private void UpdateHighPassCoefficients()
    {
        _highPassCoeffs = BiquadCoeffs.Highpass(_sampleRate, _highPassFrequency, 0.707f);
    }

    private void UpdateGateSidechainFilterCoefficients()
    {
        _gateSidechainFilterCoeffs = BiquadCoeffs.Highpass(_sampleRate, _gateSidechainFilterFreq, 0.707f);
    }

    private void UpdateEqCoefficients()
    {
        _eqLowCoeffs = BiquadCoeffs.LowShelf(_sampleRate, _eqLowFreq, _eqLowQ, _eqLowGain);
        _eqLowMidCoeffs = BiquadCoeffs.PeakingEQ(_sampleRate, _eqLowMidFreq, _eqLowMidQ, _eqLowMidGain);
        _eqHighMidCoeffs = BiquadCoeffs.PeakingEQ(_sampleRate, _eqHighMidFreq, _eqHighMidQ, _eqHighMidGain);
        _eqHighCoeffs = BiquadCoeffs.HighShelf(_sampleRate, _eqHighFreq, _eqHighQ, _eqHighGain);
    }

    private void UpdateCompressorSidechainFilterCoefficients()
    {
        _compSidechainFilterCoeffs = _compSidechainFilterType switch
        {
            FilterType.HighPass => BiquadCoeffs.Highpass(_sampleRate, _compSidechainFilterFreq, 0.707f),
            FilterType.LowPass => BiquadCoeffs.Lowpass(_sampleRate, _compSidechainFilterFreq, 0.707f),
            _ => BiquadCoeffs.Highpass(_sampleRate, _compSidechainFilterFreq, 0.707f)
        };
    }

    private void UpdateDeEsserCoefficients()
    {
        float q = _deEsserFrequency / _deEsserBandwidth;
        q = Math.Clamp(q, 0.5f, 10f);

        float w0 = 2f * MathF.PI * _deEsserFrequency / _sampleRate;
        float cosW0 = MathF.Cos(w0);
        float sinW0 = MathF.Sin(w0);
        float alpha = sinW0 / (2f * q);

        float b0 = alpha;
        float b1 = 0f;
        float b2 = -alpha;
        float a0 = 1f + alpha;
        float a1 = -2f * cosW0;
        float a2 = 1f - alpha;

        _deEsserB0 = b0 / a0;
        _deEsserB1 = b1 / a0;
        _deEsserB2 = b2 / a0;
        _deEsserA1 = a1 / a0;
        _deEsserA2 = a2 / a0;
    }

    #endregion

    #region DSP Helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ProcessBiquad(ref BiquadState state, BiquadCoeffs coeffs, float input)
    {
        float output = coeffs.B0 * input + state.Z1;
        state.Z1 = coeffs.B1 * input - coeffs.A1 * output + state.Z2;
        state.Z2 = coeffs.B2 * input - coeffs.A2 * output;
        return output;
    }

    private float ApplyDeEsserBiquad(float input, int channel)
    {
        ref var state = ref _deEsserDetectionStates[channel];
        float output = _deEsserB0 * input + state.Z1;
        state.Z1 = _deEsserB1 * input - _deEsserA1 * output + state.Z2;
        state.Z2 = _deEsserB2 * input - _deEsserA2 * output;
        return output;
    }

    private static float CalculateCompressorGainReduction(float inputDb, float threshold, float ratio, float kneeWidth)
    {
        float gainReductionDb = 0f;

        if (kneeWidth > 0f)
        {
            float kneeMin = threshold - kneeWidth / 2f;
            float kneeMax = threshold + kneeWidth / 2f;

            if (inputDb > kneeMin && inputDb < kneeMax)
            {
                float kneeInput = inputDb - kneeMin;
                float kneeFactor = kneeInput / kneeWidth;
                gainReductionDb = kneeFactor * kneeFactor * (threshold - inputDb + kneeWidth / 2f) * (1f - 1f / ratio);
            }
            else if (inputDb >= kneeMax)
            {
                gainReductionDb = (threshold - inputDb) * (1f - 1f / ratio);
            }
        }
        else
        {
            if (inputDb > threshold)
            {
                gainReductionDb = (threshold - inputDb) * (1f - 1f / ratio);
            }
        }

        return gainReductionDb;
    }

    private static float TapeSaturate(float x)
    {
        if (MathF.Abs(x) < 0.5f)
            return x;
        else if (x > 0)
            return 0.5f + 0.5f * MathF.Tanh((x - 0.5f) * 2f);
        else
            return -0.5f - 0.5f * MathF.Tanh((-x - 0.5f) * 2f);
    }

    private static float TubeSaturate(float x)
    {
        // Asymmetric tube saturation with even harmonics
        float k = 2f;
        if (x >= 0)
            return MathF.Tanh(k * x);
        else
            return MathF.Tanh(k * x * 0.8f); // Asymmetric
    }

    private static float TransformerSaturate(float x)
    {
        // Soft clipping with slight asymmetry
        float threshold = 0.7f;
        if (MathF.Abs(x) < threshold)
            return x;

        float sign = MathF.Sign(x);
        float abs = MathF.Abs(x);
        return sign * (threshold + (1f - threshold) * MathF.Tanh((abs - threshold) / (1f - threshold)));
    }

    private static void EnsureBufferSize(ref float[] buffer, int requiredSize)
    {
        if (buffer == null || buffer.Length < requiredSize)
        {
            buffer = new float[requiredSize];
        }
    }

    #endregion

    #region Console Style Presets

    private void ApplyConsoleStyle(ConsoleStyle style)
    {
        switch (style)
        {
            case ConsoleStyle.SSL:
                // SSL: Punchy, aggressive, tight bottom, crisp highs
                _eqLowQ = 0.5f;
                _eqHighQ = 0.5f;
                _compAttack = 0.003f;
                _compRelease = 0.1f;
                _compKneeWidth = 0f; // Hard knee
                _saturationMode = SaturationMode.Transformer;
                _saturationDrive = 0.2f;
                break;

            case ConsoleStyle.Neve:
                // Neve: Warm, musical, rich harmonics
                _eqLowQ = 0.707f;
                _eqHighQ = 0.707f;
                _compAttack = 0.01f;
                _compRelease = 0.15f;
                _compKneeWidth = 6f; // Soft knee
                _saturationMode = SaturationMode.Tube;
                _saturationDrive = 0.3f;
                break;

            case ConsoleStyle.API:
                // API: Punchy mids, character
                _eqLowQ = 1.0f;
                _eqHighQ = 1.0f;
                _compAttack = 0.005f;
                _compRelease = 0.2f;
                _compKneeWidth = 3f;
                _saturationMode = SaturationMode.Tape;
                _saturationDrive = 0.25f;
                break;

            case ConsoleStyle.Clean:
            default:
                // Clean: Transparent
                _eqLowQ = 0.707f;
                _eqHighQ = 0.707f;
                _compAttack = 0.005f;
                _compRelease = 0.1f;
                _compKneeWidth = 0f;
                _saturationMode = SaturationMode.Off;
                _saturationDrive = 0f;
                break;
        }

        UpdateEqCoefficients();
    }

    #endregion

    #region State Management

    private ChannelStripState CaptureCurrentState()
    {
        return new ChannelStripState
        {
            InputGainDb = _inputGainDb,
            PhaseInvertLeft = _phaseInvertLeft,
            PhaseInvertRight = _phaseInvertRight,
            HighPassEnabled = _highPassEnabled,
            HighPassFrequency = _highPassFrequency,
            GateEnabled = _gateEnabled,
            GateThreshold = _gateThreshold,
            GateAttack = _gateAttack,
            GateRelease = _gateRelease,
            GateRange = _gateRange,
            GateHold = _gateHold,
            EqEnabled = _eqEnabled,
            EqLowFrequency = _eqLowFreq,
            EqLowGain = _eqLowGain,
            EqLowQ = _eqLowQ,
            EqLowMidFrequency = _eqLowMidFreq,
            EqLowMidGain = _eqLowMidGain,
            EqLowMidQ = _eqLowMidQ,
            EqHighMidFrequency = _eqHighMidFreq,
            EqHighMidGain = _eqHighMidGain,
            EqHighMidQ = _eqHighMidQ,
            EqHighFrequency = _eqHighFreq,
            EqHighGain = _eqHighGain,
            EqHighQ = _eqHighQ,
            CompressorEnabled = _compressorEnabled,
            CompressorThreshold = _compThreshold,
            CompressorRatio = _compRatio,
            CompressorAttack = _compAttack,
            CompressorRelease = _compRelease,
            CompressorMakeupGain = _compMakeupGain,
            CompressorKneeWidth = _compKneeWidth,
            DeEsserEnabled = _deEsserEnabled,
            DeEsserFrequency = _deEsserFrequency,
            DeEsserThreshold = _deEsserThreshold,
            DeEsserReduction = _deEsserReduction,
            SaturationMode = _saturationMode,
            SaturationDrive = _saturationDrive,
            SaturationMix = _saturationMix,
            StereoWidth = _stereoWidth,
            MonoLowFrequency = _monoLowFreq,
            Pan = _pan,
            OutputLevel = _outputLevel,
            Fader = _fader
        };
    }

    private void ApplyState(ChannelStripState state)
    {
        InputGainDb = state.InputGainDb;
        PhaseInvertLeft = state.PhaseInvertLeft;
        PhaseInvertRight = state.PhaseInvertRight;
        HighPassEnabled = state.HighPassEnabled;
        HighPassFrequency = state.HighPassFrequency;
        GateEnabled = state.GateEnabled;
        GateThreshold = state.GateThreshold;
        GateAttack = state.GateAttack;
        GateRelease = state.GateRelease;
        GateRange = state.GateRange;
        GateHold = state.GateHold;
        EqEnabled = state.EqEnabled;
        EqLowFrequency = state.EqLowFrequency;
        EqLowGain = state.EqLowGain;
        EqLowQ = state.EqLowQ;
        EqLowMidFrequency = state.EqLowMidFrequency;
        EqLowMidGain = state.EqLowMidGain;
        EqLowMidQ = state.EqLowMidQ;
        EqHighMidFrequency = state.EqHighMidFrequency;
        EqHighMidGain = state.EqHighMidGain;
        EqHighMidQ = state.EqHighMidQ;
        EqHighFrequency = state.EqHighFrequency;
        EqHighGain = state.EqHighGain;
        EqHighQ = state.EqHighQ;
        CompressorEnabled = state.CompressorEnabled;
        CompressorThreshold = state.CompressorThreshold;
        CompressorRatio = state.CompressorRatio;
        CompressorAttack = state.CompressorAttack;
        CompressorRelease = state.CompressorRelease;
        CompressorMakeupGain = state.CompressorMakeupGain;
        CompressorKneeWidth = state.CompressorKneeWidth;
        DeEsserEnabled = state.DeEsserEnabled;
        DeEsserFrequency = state.DeEsserFrequency;
        DeEsserThreshold = state.DeEsserThreshold;
        DeEsserReduction = state.DeEsserReduction;
        SaturationMode = state.SaturationMode;
        SaturationDrive = state.SaturationDrive;
        SaturationMix = state.SaturationMix;
        StereoWidth = state.StereoWidth;
        MonoLowFrequency = state.MonoLowFrequency;
        Pan = state.Pan;
        OutputLevel = state.OutputLevel;
        Fader = state.Fader;
    }

    #endregion

    #region Factory Methods

    /// <summary>
    /// Creates a channel strip with SSL-style console emulation.
    /// </summary>
    public static ChannelStrip CreateSSL(ISampleProvider source, string name = "SSL Channel")
    {
        var channel = new ChannelStrip(source, name);
        channel.ConsoleStyle = ConsoleStyle.SSL;
        return channel;
    }

    /// <summary>
    /// Creates a channel strip with Neve-style console emulation.
    /// </summary>
    public static ChannelStrip CreateNeve(ISampleProvider source, string name = "Neve Channel")
    {
        var channel = new ChannelStrip(source, name);
        channel.ConsoleStyle = ConsoleStyle.Neve;
        return channel;
    }

    /// <summary>
    /// Creates a channel strip with API-style console emulation.
    /// </summary>
    public static ChannelStrip CreateAPI(ISampleProvider source, string name = "API Channel")
    {
        var channel = new ChannelStrip(source, name);
        channel.ConsoleStyle = ConsoleStyle.API;
        return channel;
    }

    /// <summary>
    /// Creates a vocal-optimized channel strip.
    /// </summary>
    public static ChannelStrip CreateVocal(ISampleProvider source, string name = "Vocal")
    {
        var channel = new ChannelStrip(source, name);
        channel.HighPassEnabled = true;
        channel.HighPassFrequency = 80f;
        channel.CompressorEnabled = true;
        channel.CompressorThreshold = -18f;
        channel.CompressorRatio = 3f;
        channel.CompressorAttack = 0.01f;
        channel.CompressorRelease = 0.15f;
        channel.DeEsserEnabled = true;
        channel.DeEsserFrequency = 6500f;
        channel.DeEsserThreshold = -24f;
        channel.EqEnabled = true;
        channel.EqLowGain = -2f; // Slight low cut
        channel.EqHighMidGain = 2f; // Presence boost
        channel.EqHighGain = 1.5f; // Air
        return channel;
    }

    /// <summary>
    /// Creates a drum bus-optimized channel strip.
    /// </summary>
    public static ChannelStrip CreateDrumBus(ISampleProvider source, string name = "Drum Bus")
    {
        var channel = new ChannelStrip(source, name);
        channel.GateEnabled = false;
        channel.CompressorEnabled = true;
        channel.CompressorThreshold = -12f;
        channel.CompressorRatio = 4f;
        channel.CompressorAttack = 0.002f;
        channel.CompressorRelease = 0.1f;
        channel.CompressorMakeupGain = 3f;
        channel.EqEnabled = true;
        channel.EqLowGain = 3f; // Punch
        channel.EqHighMidGain = -2f; // Reduce harshness
        channel.SaturationMode = SaturationMode.Tape;
        channel.SaturationDrive = 0.3f;
        return channel;
    }

    /// <summary>
    /// Creates a bass-optimized channel strip.
    /// </summary>
    public static ChannelStrip CreateBass(ISampleProvider source, string name = "Bass")
    {
        var channel = new ChannelStrip(source, name);
        channel.HighPassEnabled = true;
        channel.HighPassFrequency = 30f;
        channel.CompressorEnabled = true;
        channel.CompressorThreshold = -15f;
        channel.CompressorRatio = 4f;
        channel.CompressorAttack = 0.005f;
        channel.CompressorRelease = 0.2f;
        channel.EqEnabled = true;
        channel.EqLowGain = 2f;
        channel.EqLowMidFrequency = 700f;
        channel.EqLowMidGain = -3f; // Reduce mud
        channel.StereoWidth = 0.5f; // Narrow bass
        channel.MonoLowFrequency = 150f;
        return channel;
    }

    #endregion

    /// <summary>
    /// Resets all processing states to initial values.
    /// </summary>
    public void Reset()
    {
        // Reset filter states
        for (int i = 0; i < _channels; i++)
        {
            _highPassStates[i].Reset();
            _gateSidechainFilterStates[i].Reset();
            _eqLowStates[i].Reset();
            _eqLowMidStates[i].Reset();
            _eqHighMidStates[i].Reset();
            _eqHighStates[i].Reset();
            _compSidechainFilterStates[i].Reset();
            _deEsserDetectionStates[i].Reset();

            _gateEnvelope[i] = 0;
            _gateState[i] = 1f;
            _gateHoldCounter[i] = 0;
            _compEnvelope[i] = 0;
            _compGainSmooth[i] = 1f;
            _deEsserEnvelope[i] = 0;
            _deEsserGainSmooth[i] = 1f;
            _satLpState[i] = 0;
        }

        ResetMeters();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Unlink();
        _stateA = null;
        _stateB = null;
        _processBuffer = Array.Empty<float>();
        _sidechainBuffer = Array.Empty<float>();
        _insertBuffer = Array.Empty<float>();
    }
}

/// <summary>
/// Filter type for sidechain filters.
/// </summary>
public enum FilterType
{
    /// <summary>
    /// High pass filter.
    /// </summary>
    HighPass,

    /// <summary>
    /// Low pass filter.
    /// </summary>
    LowPass
}

/// <summary>
/// Aux send configuration.
/// </summary>
public class AuxSend
{
    /// <summary>
    /// Gets or sets the send index.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Gets or sets the send level (0-1).
    /// </summary>
    public float Level { get; set; }

    /// <summary>
    /// Gets or sets whether this is a pre-fader send.
    /// </summary>
    public bool PreFader { get; set; }

    /// <summary>
    /// Gets or sets whether this send is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets the send buffer (filled during processing).
    /// </summary>
    internal float[] Buffer { get; set; } = Array.Empty<float>();
}

/// <summary>
/// Channel strip state for A/B comparison.
/// </summary>
internal class ChannelStripState
{
    public float InputGainDb { get; set; }
    public bool PhaseInvertLeft { get; set; }
    public bool PhaseInvertRight { get; set; }
    public bool HighPassEnabled { get; set; }
    public float HighPassFrequency { get; set; }
    public bool GateEnabled { get; set; }
    public float GateThreshold { get; set; }
    public float GateAttack { get; set; }
    public float GateRelease { get; set; }
    public float GateRange { get; set; }
    public float GateHold { get; set; }
    public bool EqEnabled { get; set; }
    public float EqLowFrequency { get; set; }
    public float EqLowGain { get; set; }
    public float EqLowQ { get; set; }
    public float EqLowMidFrequency { get; set; }
    public float EqLowMidGain { get; set; }
    public float EqLowMidQ { get; set; }
    public float EqHighMidFrequency { get; set; }
    public float EqHighMidGain { get; set; }
    public float EqHighMidQ { get; set; }
    public float EqHighFrequency { get; set; }
    public float EqHighGain { get; set; }
    public float EqHighQ { get; set; }
    public bool CompressorEnabled { get; set; }
    public float CompressorThreshold { get; set; }
    public float CompressorRatio { get; set; }
    public float CompressorAttack { get; set; }
    public float CompressorRelease { get; set; }
    public float CompressorMakeupGain { get; set; }
    public float CompressorKneeWidth { get; set; }
    public bool DeEsserEnabled { get; set; }
    public float DeEsserFrequency { get; set; }
    public float DeEsserThreshold { get; set; }
    public float DeEsserReduction { get; set; }
    public SaturationMode SaturationMode { get; set; }
    public float SaturationDrive { get; set; }
    public float SaturationMix { get; set; }
    public float StereoWidth { get; set; }
    public float MonoLowFrequency { get; set; }
    public float Pan { get; set; }
    public float OutputLevel { get; set; }
    public float Fader { get; set; }

    public ChannelStripState Clone()
    {
        return (ChannelStripState)MemberwiseClone();
    }
}

/// <summary>
/// Channel strip preset for saving/loading settings.
/// </summary>
public class ChannelStripPreset
{
    /// <summary>
    /// Gets or sets the preset name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "Default";

    /// <summary>
    /// Gets or sets the preset category.
    /// </summary>
    [JsonPropertyName("category")]
    public string Category { get; set; } = "General";

    /// <summary>
    /// Gets or sets the preset description.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    // Input section
    [JsonPropertyName("inputGainDb")]
    public float InputGainDb { get; set; }

    [JsonPropertyName("phaseInvertLeft")]
    public bool PhaseInvertLeft { get; set; }

    [JsonPropertyName("phaseInvertRight")]
    public bool PhaseInvertRight { get; set; }

    [JsonPropertyName("highPassEnabled")]
    public bool HighPassEnabled { get; set; }

    [JsonPropertyName("highPassFrequency")]
    public float HighPassFrequency { get; set; } = 80f;

    // Gate section
    [JsonPropertyName("gateEnabled")]
    public bool GateEnabled { get; set; }

    [JsonPropertyName("gateThreshold")]
    public float GateThreshold { get; set; } = -40f;

    [JsonPropertyName("gateAttack")]
    public float GateAttack { get; set; } = 0.001f;

    [JsonPropertyName("gateRelease")]
    public float GateRelease { get; set; } = 0.1f;

    [JsonPropertyName("gateRange")]
    public float GateRange { get; set; } = -80f;

    [JsonPropertyName("gateHold")]
    public float GateHold { get; set; } = 0.05f;

    // EQ section
    [JsonPropertyName("eqEnabled")]
    public bool EqEnabled { get; set; } = true;

    [JsonPropertyName("eqLowFrequency")]
    public float EqLowFrequency { get; set; } = 80f;

    [JsonPropertyName("eqLowGain")]
    public float EqLowGain { get; set; }

    [JsonPropertyName("eqLowQ")]
    public float EqLowQ { get; set; } = 0.707f;

    [JsonPropertyName("eqLowMidFrequency")]
    public float EqLowMidFrequency { get; set; } = 400f;

    [JsonPropertyName("eqLowMidGain")]
    public float EqLowMidGain { get; set; }

    [JsonPropertyName("eqLowMidQ")]
    public float EqLowMidQ { get; set; } = 1f;

    [JsonPropertyName("eqHighMidFrequency")]
    public float EqHighMidFrequency { get; set; } = 2500f;

    [JsonPropertyName("eqHighMidGain")]
    public float EqHighMidGain { get; set; }

    [JsonPropertyName("eqHighMidQ")]
    public float EqHighMidQ { get; set; } = 1f;

    [JsonPropertyName("eqHighFrequency")]
    public float EqHighFrequency { get; set; } = 12000f;

    [JsonPropertyName("eqHighGain")]
    public float EqHighGain { get; set; }

    [JsonPropertyName("eqHighQ")]
    public float EqHighQ { get; set; } = 0.707f;

    // Compressor section
    [JsonPropertyName("compressorEnabled")]
    public bool CompressorEnabled { get; set; }

    [JsonPropertyName("compressorThreshold")]
    public float CompressorThreshold { get; set; } = -20f;

    [JsonPropertyName("compressorRatio")]
    public float CompressorRatio { get; set; } = 4f;

    [JsonPropertyName("compressorAttack")]
    public float CompressorAttack { get; set; } = 0.005f;

    [JsonPropertyName("compressorRelease")]
    public float CompressorRelease { get; set; } = 0.1f;

    [JsonPropertyName("compressorMakeupGain")]
    public float CompressorMakeupGain { get; set; }

    [JsonPropertyName("compressorKneeWidth")]
    public float CompressorKneeWidth { get; set; }

    // De-esser section
    [JsonPropertyName("deEsserEnabled")]
    public bool DeEsserEnabled { get; set; }

    [JsonPropertyName("deEsserFrequency")]
    public float DeEsserFrequency { get; set; } = 6000f;

    [JsonPropertyName("deEsserThreshold")]
    public float DeEsserThreshold { get; set; } = -20f;

    [JsonPropertyName("deEsserReduction")]
    public float DeEsserReduction { get; set; } = 6f;

    // Saturation section
    [JsonPropertyName("saturationMode")]
    public SaturationMode SaturationMode { get; set; }

    [JsonPropertyName("saturationDrive")]
    public float SaturationDrive { get; set; } = 0.5f;

    [JsonPropertyName("saturationMix")]
    public float SaturationMix { get; set; } = 1f;

    // Width section
    [JsonPropertyName("stereoWidth")]
    public float StereoWidth { get; set; } = 1f;

    [JsonPropertyName("monoLowFrequency")]
    public float MonoLowFrequency { get; set; } = 200f;

    // Output section
    [JsonPropertyName("pan")]
    public float Pan { get; set; }

    [JsonPropertyName("outputLevel")]
    public float OutputLevel { get; set; } = 1f;

    // Console style
    [JsonPropertyName("consoleStyle")]
    public ConsoleStyle ConsoleStyle { get; set; }

    /// <summary>
    /// Serializes this preset to JSON.
    /// </summary>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Deserializes a preset from JSON.
    /// </summary>
    public static ChannelStripPreset? FromJson(string json)
    {
        return JsonSerializer.Deserialize<ChannelStripPreset>(json);
    }

    /// <summary>
    /// Creates an SSL-style preset.
    /// </summary>
    public static ChannelStripPreset CreateSSLPreset()
    {
        return new ChannelStripPreset
        {
            Name = "SSL E-Channel",
            Category = "Console",
            Description = "Punchy SSL-style channel strip",
            ConsoleStyle = ConsoleStyle.SSL,
            EqEnabled = true,
            EqLowQ = 0.5f,
            EqHighQ = 0.5f,
            CompressorEnabled = true,
            CompressorThreshold = -15f,
            CompressorRatio = 4f,
            CompressorAttack = 0.003f,
            CompressorRelease = 0.1f,
            SaturationMode = SaturationMode.Transformer,
            SaturationDrive = 0.2f
        };
    }

    /// <summary>
    /// Creates a Neve-style preset.
    /// </summary>
    public static ChannelStripPreset CreateNevePreset()
    {
        return new ChannelStripPreset
        {
            Name = "Neve 1073",
            Category = "Console",
            Description = "Warm Neve-style channel strip",
            ConsoleStyle = ConsoleStyle.Neve,
            EqEnabled = true,
            CompressorEnabled = true,
            CompressorThreshold = -12f,
            CompressorRatio = 3f,
            CompressorAttack = 0.01f,
            CompressorRelease = 0.15f,
            CompressorKneeWidth = 6f,
            SaturationMode = SaturationMode.Tube,
            SaturationDrive = 0.3f
        };
    }

    /// <summary>
    /// Creates an API-style preset.
    /// </summary>
    public static ChannelStripPreset CreateAPIPreset()
    {
        return new ChannelStripPreset
        {
            Name = "API 550",
            Category = "Console",
            Description = "Punchy API-style channel strip",
            ConsoleStyle = ConsoleStyle.API,
            EqEnabled = true,
            EqLowQ = 1f,
            EqHighQ = 1f,
            CompressorEnabled = true,
            CompressorThreshold = -18f,
            CompressorRatio = 4f,
            CompressorAttack = 0.005f,
            CompressorRelease = 0.2f,
            CompressorKneeWidth = 3f,
            SaturationMode = SaturationMode.Tape,
            SaturationDrive = 0.25f
        };
    }
}
