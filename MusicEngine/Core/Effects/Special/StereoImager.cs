//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Comprehensive stereo imaging/widening effect with multiband processing, M/S manipulation, and advanced features.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Special;

/// <summary>
/// Stereo imaging processing mode.
/// </summary>
public enum StereoImagerMode
{
    /// <summary>
    /// Normal stereo output.
    /// </summary>
    Normal,

    /// <summary>
    /// Monitor left channel only (mono).
    /// </summary>
    MonitorLeft,

    /// <summary>
    /// Monitor right channel only (mono).
    /// </summary>
    MonitorRight,

    /// <summary>
    /// Monitor mid channel only (mono).
    /// </summary>
    MonitorMid,

    /// <summary>
    /// Monitor side channel only.
    /// </summary>
    MonitorSide,

    /// <summary>
    /// Mono compatibility preview (sum to mono).
    /// </summary>
    MonoPreview
}

/// <summary>
/// Comprehensive stereo imaging and widening effect with multiband processing,
/// M/S manipulation, Haas effect, transient detection, and advanced features.
/// </summary>
public class StereoImager : EffectBase
{
    // Crossover filter states (2-pole Linkwitz-Riley)
    private float _lowCrossLpL1, _lowCrossLpL2, _lowCrossLpR1, _lowCrossLpR2;
    private float _lowCrossHpL1, _lowCrossHpL2, _lowCrossHpR1, _lowCrossHpR2;
    private float _highCrossLpL1, _highCrossLpL2, _highCrossLpR1, _highCrossLpR2;
    private float _highCrossHpL1, _highCrossHpL2, _highCrossHpR1, _highCrossHpR2;

    // Side EQ filter states
    private float _sideEqLowState, _sideEqHighState;

    // Bass mono filter states
    private float _bassMonoLpStateL, _bassMonoLpStateR;

    // Haas delay buffers
    private float[] _haasDelayBufferL = Array.Empty<float>();
    private float[] _haasDelayBufferR = Array.Empty<float>();
    private int _haasDelayIndex;
    private int _haasDelaySamples;

    // Phase rotation all-pass filter states
    private float _phaseAllpassL1, _phaseAllpassL2;
    private float _phaseAllpassR1, _phaseAllpassR2;

    // Transient detection envelope followers
    private float _fastEnvelopeL, _fastEnvelopeR;
    private float _slowEnvelopeL, _slowEnvelopeR;

    // Auto-width frequency analysis
    private float _lowBandEnergy, _midBandEnergy, _highBandEnergy;
    private float _autoWidthSmoothed;

    // Correlation metering
    private float _correlationSum;
    private float _powerSumL;
    private float _powerSumR;
    private int _correlationSampleCount;
    private const int CorrelationWindowSamples = 4410;

    // Output metering
    private float _peakL, _peakR, _peakM, _peakS;
    private float _rmsL, _rmsR, _rmsM, _rmsS;
    private float _rmsSumL, _rmsSumR, _rmsSumM, _rmsSumS;
    private int _meterSampleCount;
    private const int MeterWindowSamples = 4410;

    public StereoImager(ISampleProvider source) : this(source, "Stereo Imager") { }

    public StereoImager(ISampleProvider source, string name) : base(source, name)
    {
        if (source.WaveFormat.Channels != 2)
            throw new ArgumentException("Source must be stereo (2 channels)", nameof(source));

        RegisterParameter("LowCrossover", 200f);
        RegisterParameter("HighCrossover", 4000f);
        RegisterParameter("LowWidth", 100f);
        RegisterParameter("MidWidth", 100f);
        RegisterParameter("HighWidth", 100f);
        RegisterParameter("MidSideBalance", 0f);
        RegisterParameter("MidLevel", 0f);
        RegisterParameter("SideLevel", 0f);
        RegisterParameter("BassMonoEnabled", 0f);
        RegisterParameter("BassMonoFrequency", 100f);
        RegisterParameter("HaasEnabled", 0f);
        RegisterParameter("HaasDelayTime", 10f);
        RegisterParameter("HaasMix", 50f);
        RegisterParameter("PhaseRotation", 0f);
        RegisterParameter("Balance", 0f);
        RegisterParameter("ChannelSwap", 0f);
        RegisterParameter("SideEqLowGain", 0f);
        RegisterParameter("SideEqLowFreq", 200f);
        RegisterParameter("SideEqHighGain", 0f);
        RegisterParameter("SideEqHighFreq", 4000f);
        RegisterParameter("TransientWidthEnabled", 0f);
        RegisterParameter("TransientWidth", 150f);
        RegisterParameter("SustainWidth", 100f);
        RegisterParameter("TransientSensitivity", 50f);
        RegisterParameter("AutoWidthEnabled", 0f);
        RegisterParameter("AutoWidthAmount", 50f);
        RegisterParameter("SafeBassEnabled", 0f);
        RegisterParameter("SafeBassFrequency", 120f);
        RegisterParameter("OutputGain", 0f);
        RegisterParameter("Mode", (float)StereoImagerMode.Normal);
        RegisterParameter("Mix", 1f);

        UpdateHaasDelayBuffer();
    }

    public float LowCrossover { get => GetParameter("LowCrossover"); set => SetParameter("LowCrossover", Math.Clamp(value, 20f, 500f)); }
    public float HighCrossover { get => GetParameter("HighCrossover"); set => SetParameter("HighCrossover", Math.Clamp(value, 1000f, 16000f)); }
    public float LowWidth { get => GetParameter("LowWidth"); set => SetParameter("LowWidth", Math.Clamp(value, 0f, 200f)); }
    public float MidWidth { get => GetParameter("MidWidth"); set => SetParameter("MidWidth", Math.Clamp(value, 0f, 200f)); }
    public float HighWidth { get => GetParameter("HighWidth"); set => SetParameter("HighWidth", Math.Clamp(value, 0f, 200f)); }
    public float MidSideBalance { get => GetParameter("MidSideBalance"); set => SetParameter("MidSideBalance", Math.Clamp(value, -100f, 100f)); }
    public float MidLevel { get => GetParameter("MidLevel"); set => SetParameter("MidLevel", Math.Clamp(value, -24f, 24f)); }
    public float SideLevel { get => GetParameter("SideLevel"); set => SetParameter("SideLevel", Math.Clamp(value, -24f, 24f)); }
    public bool BassMonoEnabled { get => GetParameter("BassMonoEnabled") > 0.5f; set => SetParameter("BassMonoEnabled", value ? 1f : 0f); }
    public float BassMonoFrequency { get => GetParameter("BassMonoFrequency"); set => SetParameter("BassMonoFrequency", Math.Clamp(value, 20f, 300f)); }
    public bool HaasEnabled { get => GetParameter("HaasEnabled") > 0.5f; set => SetParameter("HaasEnabled", value ? 1f : 0f); }
    public float HaasDelayTime { get => GetParameter("HaasDelayTime"); set { SetParameter("HaasDelayTime", Math.Clamp(value, 0f, 30f)); UpdateHaasDelayBuffer(); } }
    public float HaasMix { get => GetParameter("HaasMix"); set => SetParameter("HaasMix", Math.Clamp(value, 0f, 100f)); }
    public float PhaseRotation { get => GetParameter("PhaseRotation"); set => SetParameter("PhaseRotation", Math.Clamp(value, -180f, 180f)); }
    public float Balance { get => GetParameter("Balance"); set => SetParameter("Balance", Math.Clamp(value, -100f, 100f)); }
    public bool ChannelSwap { get => GetParameter("ChannelSwap") > 0.5f; set => SetParameter("ChannelSwap", value ? 1f : 0f); }
    public float SideEqLowGain { get => GetParameter("SideEqLowGain"); set => SetParameter("SideEqLowGain", Math.Clamp(value, -12f, 12f)); }
    public float SideEqLowFrequency { get => GetParameter("SideEqLowFreq"); set => SetParameter("SideEqLowFreq", Math.Clamp(value, 20f, 1000f)); }
    public float SideEqHighGain { get => GetParameter("SideEqHighGain"); set => SetParameter("SideEqHighGain", Math.Clamp(value, -12f, 12f)); }
    public float SideEqHighFrequency { get => GetParameter("SideEqHighFreq"); set => SetParameter("SideEqHighFreq", Math.Clamp(value, 1000f, 16000f)); }
    public bool TransientWidthEnabled { get => GetParameter("TransientWidthEnabled") > 0.5f; set => SetParameter("TransientWidthEnabled", value ? 1f : 0f); }
    public float TransientWidth { get => GetParameter("TransientWidth"); set => SetParameter("TransientWidth", Math.Clamp(value, 0f, 200f)); }
    public float SustainWidth { get => GetParameter("SustainWidth"); set => SetParameter("SustainWidth", Math.Clamp(value, 0f, 200f)); }
    public float TransientSensitivity { get => GetParameter("TransientSensitivity"); set => SetParameter("TransientSensitivity", Math.Clamp(value, 0f, 100f)); }
    public bool AutoWidthEnabled { get => GetParameter("AutoWidthEnabled") > 0.5f; set => SetParameter("AutoWidthEnabled", value ? 1f : 0f); }
    public float AutoWidthAmount { get => GetParameter("AutoWidthAmount"); set => SetParameter("AutoWidthAmount", Math.Clamp(value, 0f, 100f)); }
    public bool SafeBassEnabled { get => GetParameter("SafeBassEnabled") > 0.5f; set => SetParameter("SafeBassEnabled", value ? 1f : 0f); }
    public float SafeBassFrequency { get => GetParameter("SafeBassFrequency"); set => SetParameter("SafeBassFrequency", Math.Clamp(value, 20f, 300f)); }
    public float OutputGain { get => GetParameter("OutputGain"); set => SetParameter("OutputGain", Math.Clamp(value, -24f, 24f)); }
    public StereoImagerMode Mode { get => (StereoImagerMode)GetParameter("Mode"); set => SetParameter("Mode", (float)value); }

    public float Correlation { get; private set; } = 1f;
    public float PeakLeft => _peakL;
    public float PeakRight => _peakR;
    public float PeakMid => _peakM;
    public float PeakSide => _peakS;
    public float RmsLeftDb => LinearToDb(_rmsL);
    public float RmsRightDb => LinearToDb(_rmsR);
    public float RmsMidDb => LinearToDb(_rmsM);
    public float RmsSideDb => LinearToDb(_rmsS);

    private void UpdateHaasDelayBuffer()
    {
        int newDelaySamples = (int)(HaasDelayTime * SampleRate / 1000f);
        if (newDelaySamples != _haasDelaySamples && newDelaySamples > 0)
        {
            _haasDelaySamples = newDelaySamples;
            _haasDelayBufferL = new float[_haasDelaySamples];
            _haasDelayBufferR = new float[_haasDelaySamples];
            _haasDelayIndex = 0;
        }
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int sampleRate = SampleRate;
        var mode = Mode;

        float lowCrossover = LowCrossover;
        float highCrossover = HighCrossover;
        float lowWidth = LowWidth / 100f;
        float midWidth = MidWidth / 100f;
        float highWidth = HighWidth / 100f;
        float midSideBalance = MidSideBalance / 100f;
        float midLevel = DbToLinear(MidLevel);
        float sideLevel = DbToLinear(SideLevel);
        bool bassMonoEnabled = BassMonoEnabled;
        float bassMonoFreq = BassMonoFrequency;
        bool haasEnabled = HaasEnabled;
        float haasMix = HaasMix / 100f;
        float phaseRotation = PhaseRotation * MathF.PI / 180f;
        float balance = Balance / 100f;
        bool channelSwap = ChannelSwap;
        bool transientWidthEnabled = TransientWidthEnabled;
        float transientWidth = TransientWidth / 100f;
        float sustainWidth = SustainWidth / 100f;
        float transientSensitivity = TransientSensitivity / 100f;
        bool autoWidthEnabled = AutoWidthEnabled;
        float autoWidthAmount = AutoWidthAmount / 100f;
        bool safeBassEnabled = SafeBassEnabled;
        float safeBassFreq = SafeBassFrequency;
        float outputGain = DbToLinear(OutputGain);

        float lowCrossCoef = CalculateLpCoefficient(lowCrossover, sampleRate);
        float highCrossCoef = CalculateLpCoefficient(highCrossover, sampleRate);
        float bassMonoCoef = CalculateLpCoefficient(bassMonoFreq, sampleRate);
        float safeBassCoef = CalculateLpCoefficient(safeBassFreq, sampleRate);
        float phaseAllpassCoef = MathF.Tan(phaseRotation * 0.25f);
        float autoWidthSmoothCoef = MathF.Exp(-1f / (0.1f * sampleRate));

        float fastAttackCoef = MathF.Exp(-1f / (0.0001f * sampleRate));
        float fastReleaseCoef = MathF.Exp(-1f / (0.005f * sampleRate));
        float slowAttackCoef = MathF.Exp(-1f / (0.02f * sampleRate));
        float slowReleaseCoef = MathF.Exp(-1f / (0.2f * sampleRate));

        for (int i = 0; i < count; i += 2)
        {
            float left = sourceBuffer[i];
            float right = sourceBuffer[i + 1];

            if (channelSwap) (left, right) = (right, left);

            float lowL = ProcessLowpass2Pole(left, ref _lowCrossLpL1, ref _lowCrossLpL2, lowCrossCoef);
            float lowR = ProcessLowpass2Pole(right, ref _lowCrossLpR1, ref _lowCrossLpR2, lowCrossCoef);
            float highL = ProcessHighpass2Pole(left, ref _highCrossHpL1, ref _highCrossHpL2, highCrossCoef);
            float highR = ProcessHighpass2Pole(right, ref _highCrossHpR1, ref _highCrossHpR2, highCrossCoef);
            float midBandL = left - lowL - highL;
            float midBandR = right - lowR - highR;

            if (autoWidthEnabled)
            {
                _lowBandEnergy = _lowBandEnergy * 0.999f + (lowL * lowL + lowR * lowR) * 0.001f;
                _midBandEnergy = _midBandEnergy * 0.999f + (midBandL * midBandL + midBandR * midBandR) * 0.001f;
                _highBandEnergy = _highBandEnergy * 0.999f + (highL * highL + highR * highR) * 0.001f;
            }

            float transientAmount = 0f;
            if (transientWidthEnabled)
            {
                float inputAbs = MathF.Abs(left) + MathF.Abs(right);
                float fastCoef = inputAbs > _fastEnvelopeL ? fastAttackCoef : fastReleaseCoef;
                _fastEnvelopeL = inputAbs + fastCoef * (_fastEnvelopeL - inputAbs);
                float slowCoef = inputAbs > _slowEnvelopeL ? slowAttackCoef : slowReleaseCoef;
                _slowEnvelopeL = inputAbs + slowCoef * (_slowEnvelopeL - inputAbs);
                float envelopeDiff = _fastEnvelopeL - _slowEnvelopeL;
                float signalLevel = MathF.Max(_slowEnvelopeL, 1e-6f);
                transientAmount = Math.Clamp(envelopeDiff / signalLevel * transientSensitivity * 2f, 0f, 1f);
            }

            float autoWidthModifier = 1f;
            if (autoWidthEnabled)
            {
                float totalEnergy = _lowBandEnergy + _midBandEnergy + _highBandEnergy + 1e-10f;
                float highRatio = _highBandEnergy / totalEnergy;
                float lowRatio = _lowBandEnergy / totalEnergy;
                float targetAutoWidth = Math.Clamp(0.7f + highRatio * 0.6f - lowRatio * 0.3f, 0.5f, 1.5f);
                _autoWidthSmoothed = targetAutoWidth + autoWidthSmoothCoef * (_autoWidthSmoothed - targetAutoWidth);
                autoWidthModifier = 1f + (_autoWidthSmoothed - 1f) * autoWidthAmount;
            }

            float effectiveLowWidth = lowWidth * autoWidthModifier;
            float effectiveMidWidth = midWidth * autoWidthModifier;
            float effectiveHighWidth = highWidth * autoWidthModifier;

            if (transientWidthEnabled)
            {
                float widthMod = transientWidth * transientAmount + sustainWidth * (1f - transientAmount);
                effectiveLowWidth *= widthMod;
                effectiveMidWidth *= widthMod;
                effectiveHighWidth *= widthMod;
            }

            ProcessBandWidth(ref lowL, ref lowR, effectiveLowWidth);
            ProcessBandWidth(ref midBandL, ref midBandR, effectiveMidWidth);
            ProcessBandWidth(ref highL, ref highR, effectiveHighWidth);

            float processedL = lowL + midBandL + highL;
            float processedR = lowR + midBandR + highR;

            if (bassMonoEnabled)
            {
                float bassL = ProcessLowpass(processedL, ref _bassMonoLpStateL, bassMonoCoef);
                float bassR = ProcessLowpass(processedR, ref _bassMonoLpStateR, bassMonoCoef);
                float bassMono = (bassL + bassR) * 0.5f;
                processedL = processedL - bassL + bassMono;
                processedR = processedR - bassR + bassMono;
            }

            if (safeBassEnabled && !bassMonoEnabled)
            {
                float mid = (processedL + processedR) * 0.5f;
                float side = (processedL - processedR) * 0.5f;
                float sideBass = ProcessLowpass(side, ref _sideEqLowState, safeBassCoef);
                side = side - sideBass * 0.9f;
                processedL = mid + side;
                processedR = mid - side;
            }

            float midCh = (processedL + processedR) * 0.5f;
            float sideCh = (processedL - processedR) * 0.5f;

            midCh *= midLevel;
            sideCh *= sideLevel;

            if (midSideBalance < 0) sideCh *= 1f + midSideBalance;
            else if (midSideBalance > 0) midCh *= 1f - midSideBalance;

            processedL = midCh + sideCh;
            processedR = midCh - sideCh;

            if (haasEnabled && _haasDelaySamples > 0 && haasMix > 0)
            {
                float delayedL = _haasDelayBufferL[_haasDelayIndex];
                float delayedR = _haasDelayBufferR[_haasDelayIndex];
                _haasDelayBufferL[_haasDelayIndex] = processedR;
                _haasDelayBufferR[_haasDelayIndex] = processedL;
                _haasDelayIndex = (_haasDelayIndex + 1) % _haasDelaySamples;
                processedL += delayedL * haasMix * 0.5f;
                processedR += delayedR * haasMix * 0.5f;
            }

            if (MathF.Abs(phaseRotation) > 0.01f)
                processedL = ProcessAllpass(processedL, ref _phaseAllpassL1, ref _phaseAllpassL2, phaseAllpassCoef);

            if (balance < 0) processedR *= 1f + balance;
            else if (balance > 0) processedL *= 1f - balance;

            processedL *= outputGain;
            processedR *= outputGain;

            UpdateMetering(processedL, processedR);

            float outputL, outputR;
            switch (mode)
            {
                case StereoImagerMode.MonitorLeft: outputL = outputR = processedL; break;
                case StereoImagerMode.MonitorRight: outputL = outputR = processedR; break;
                case StereoImagerMode.MonitorMid: outputL = outputR = (processedL + processedR) * 0.5f; break;
                case StereoImagerMode.MonitorSide: var s = (processedL - processedR) * 0.5f; outputL = s; outputR = -s; break;
                case StereoImagerMode.MonoPreview: outputL = outputR = (processedL + processedR) * 0.5f; break;
                default: outputL = processedL; outputR = processedR; break;
            }

            destBuffer[offset + i] = outputL;
            destBuffer[offset + i + 1] = outputR;
        }
    }

    private static void ProcessBandWidth(ref float left, ref float right, float width)
    {
        float mid = (left + right) * 0.5f;
        float side = (left - right) * 0.5f;
        side *= width;
        left = mid + side;
        right = mid - side;
    }

    private void UpdateMetering(float left, float right)
    {
        float mid = (left + right) * 0.5f;
        float side = (left - right) * 0.5f;

        float absL = MathF.Abs(left), absR = MathF.Abs(right), absM = MathF.Abs(mid), absS = MathF.Abs(side);
        if (absL > _peakL) _peakL = absL;
        if (absR > _peakR) _peakR = absR;
        if (absM > _peakM) _peakM = absM;
        if (absS > _peakS) _peakS = absS;

        _rmsSumL += left * left; _rmsSumR += right * right; _rmsSumM += mid * mid; _rmsSumS += side * side;
        _correlationSum += left * right; _powerSumL += left * left; _powerSumR += right * right;
        _correlationSampleCount++; _meterSampleCount++;

        if (_meterSampleCount >= MeterWindowSamples)
        {
            _rmsL = MathF.Sqrt(_rmsSumL / _meterSampleCount);
            _rmsR = MathF.Sqrt(_rmsSumR / _meterSampleCount);
            _rmsM = MathF.Sqrt(_rmsSumM / _meterSampleCount);
            _rmsS = MathF.Sqrt(_rmsSumS / _meterSampleCount);
            _peakL *= 0.95f; _peakR *= 0.95f; _peakM *= 0.95f; _peakS *= 0.95f;
            _rmsSumL = _rmsSumR = _rmsSumM = _rmsSumS = 0f;
            _meterSampleCount = 0;
        }

        if (_correlationSampleCount >= CorrelationWindowSamples)
        {
            float power = MathF.Sqrt(_powerSumL * _powerSumR);
            Correlation = power > 1e-6f ? _correlationSum / power : 1f;
            _correlationSum = _powerSumL = _powerSumR = 0f;
            _correlationSampleCount = 0;
        }
    }

    private static float CalculateLpCoefficient(float frequency, int sampleRate) => MathF.Exp(-2f * MathF.PI * frequency / sampleRate);
    private static float ProcessLowpass(float input, ref float state, float coef) { state = state * coef + input * (1f - coef); return state; }
    private static float ProcessLowpass2Pole(float input, ref float s1, ref float s2, float coef) { s1 = s1 * coef + input * (1f - coef); s2 = s2 * coef + s1 * (1f - coef); return s2; }
    private static float ProcessHighpass2Pole(float input, ref float s1, ref float s2, float coef) => input - ProcessLowpass2Pole(input, ref s1, ref s2, coef);
    private static float ProcessAllpass(float input, ref float s1, ref float s2, float coef) { float t = input - coef * s1; float o = coef * t + s1; s1 = t; t = o - coef * s2; float o2 = coef * t + s2; s2 = t; return o2; }
    private static float DbToLinear(float db) => MathF.Pow(10f, db / 20f);
    private static float LinearToDb(float linear) => 20f * MathF.Log10(linear + 1e-10f);

    public void Reset()
    {
        _lowCrossLpL1 = _lowCrossLpL2 = _lowCrossLpR1 = _lowCrossLpR2 = 0f;
        _lowCrossHpL1 = _lowCrossHpL2 = _lowCrossHpR1 = _lowCrossHpR2 = 0f;
        _highCrossLpL1 = _highCrossLpL2 = _highCrossLpR1 = _highCrossLpR2 = 0f;
        _highCrossHpL1 = _highCrossHpL2 = _highCrossHpR1 = _highCrossHpR2 = 0f;
        _sideEqLowState = _sideEqHighState = 0f;
        _bassMonoLpStateL = _bassMonoLpStateR = 0f;
        _phaseAllpassL1 = _phaseAllpassL2 = _phaseAllpassR1 = _phaseAllpassR2 = 0f;
        Array.Clear(_haasDelayBufferL); Array.Clear(_haasDelayBufferR);
        _haasDelayIndex = 0;
        _fastEnvelopeL = _fastEnvelopeR = _slowEnvelopeL = _slowEnvelopeR = 0f;
        _lowBandEnergy = _midBandEnergy = _highBandEnergy = 0f;
        _autoWidthSmoothed = 1f;
        _correlationSum = _powerSumL = _powerSumR = 0f;
        _correlationSampleCount = 0; Correlation = 1f;
        _peakL = _peakR = _peakM = _peakS = 0f;
        _rmsL = _rmsR = _rmsM = _rmsS = 0f;
        _rmsSumL = _rmsSumR = _rmsSumM = _rmsSumS = 0f;
        _meterSampleCount = 0;
    }
}
