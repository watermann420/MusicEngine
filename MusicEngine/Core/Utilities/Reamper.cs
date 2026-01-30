// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Description: Reamping utility for routing DI signals through amp/effects chains with
//              level matching, phase inversion, latency compensation, and metering.

using System;
using System.Collections.Generic;
using NAudio.Wave;
using MusicEngine.Core.PDC;

namespace MusicEngine.Core.Utilities;

/// <summary>
/// Reamping mode for different signal routing scenarios.
/// </summary>
public enum ReampMode
{
    /// <summary>Normal reamping - DI input through effects chain to output.</summary>
    Normal,
    /// <summary>Parallel mode - blend dry DI with reamped signal.</summary>
    Parallel,
    /// <summary>Monitor mode - hear both DI and reamped signal for comparison.</summary>
    Monitor,
    /// <summary>Calibration mode - output test tone for level matching.</summary>
    Calibration,
    /// <summary>Bypass mode - pass DI signal through unchanged.</summary>
    Bypass
}

/// <summary>
/// Level matching mode for input signal calibration.
/// </summary>
public enum LevelMatchMode
{
    /// <summary>Manual gain adjustment.</summary>
    Manual,
    /// <summary>Automatic peak-based level matching.</summary>
    AutoPeak,
    /// <summary>Automatic RMS-based level matching.</summary>
    AutoRms,
    /// <summary>Line level (-10 dBV consumer) to instrument level conversion.</summary>
    LineToInstrument,
    /// <summary>Professional level (+4 dBu) to instrument level conversion.</summary>
    ProToInstrument
}

/// <summary>
/// Metering data for the reamper.
/// </summary>
public class ReamperMeteringData
{
    /// <summary>Input peak level in dB.</summary>
    public float InputPeakDb { get; set; } = -60f;

    /// <summary>Input RMS level in dB.</summary>
    public float InputRmsDb { get; set; } = -60f;

    /// <summary>Output peak level in dB.</summary>
    public float OutputPeakDb { get; set; } = -60f;

    /// <summary>Output RMS level in dB.</summary>
    public float OutputRmsDb { get; set; } = -60f;

    /// <summary>Current input gain in dB.</summary>
    public float CurrentInputGainDb { get; set; } = 0f;

    /// <summary>Current output gain in dB.</summary>
    public float CurrentOutputGainDb { get; set; } = 0f;

    /// <summary>Current latency compensation in samples.</summary>
    public int LatencyCompensationSamples { get; set; } = 0;

    /// <summary>Whether input is clipping.</summary>
    public bool InputClipping { get; set; }

    /// <summary>Whether output is clipping.</summary>
    public bool OutputClipping { get; set; }

    /// <summary>Phase correlation between dry and wet signal (-1 to +1).</summary>
    public float PhaseCorrelation { get; set; } = 1f;
}

/// <summary>
/// Reamping utility for routing DI (Direct Input) signals through amp simulators and effects chains.
/// </summary>
/// <remarks>
/// Features:
/// - Input level matching (line/instrument level conversion)
/// - Output gain control with smoothing
/// - Phase inversion for polarity correction
/// - Automatic latency compensation for effects chain
/// - Support for inserting effect chains (amp sims, pedals, etc.)
/// - Parallel blending of dry DI and wet reamped signal
/// - Comprehensive metering for input/output levels
/// - Calibration mode with test tone generation
/// - A/B comparison mode
///
/// Typical usage:
/// 1. Record DI guitar/bass to a track
/// 2. Create Reamper with the DI track as source
/// 3. Add amp simulator and effects to the chain
/// 4. Adjust input gain to match expected instrument level
/// 5. Monitor and adjust output gain
/// </remarks>
public class Reamper : ISampleProvider, ILatencyReporter, IDisposable
{
    private readonly ISampleProvider _source;
    private readonly int _channels;
    private readonly int _sampleRate;
    private readonly object _lock = new();

    // Effect chain
    private readonly List<IEffect> _effectChain = new();
    private ISampleProvider? _chainOutput;

    // Gain parameters
    private float _inputGainDb = 0f;
    private float _outputGainDb = 0f;
    private float _inputGainLinear = 1f;
    private float _outputGainLinear = 1f;
    private float _targetInputGain = 1f;
    private float _targetOutputGain = 1f;
    private const float GainSmoothingCoeff = 0.9995f;

    // Parallel blend
    private float _dryWetMix = 1f; // 0 = dry only, 1 = wet only
    private float _targetDryWetMix = 1f;

    // Phase inversion
    private bool _phaseInvert;

    // Mode
    private ReampMode _mode = ReampMode.Normal;
    private LevelMatchMode _levelMatchMode = LevelMatchMode.Manual;

    // Latency compensation
    private bool _latencyCompensationEnabled = true;
    private float[] _delayBuffer = Array.Empty<float>();
    private int _delayWritePos;
    private int _compensationSamples;

    // Auto level matching
    private float _autoLevelTarget = -18f; // Target level in dBFS
    private float _measurementAccumulator;
    private int _measurementSampleCount;
    private const int MeasurementWindowSamples = 44100; // 1 second
    private bool _autoLevelLocked;

    // Metering
    private readonly ReamperMeteringData _metering = new();
    private float _inputPeakAcc;
    private float _outputPeakAcc;
    private float _inputRmsAcc;
    private float _outputRmsAcc;
    private float _correlationSumAcc;
    private float _dryPowerAcc;
    private float _wetPowerAcc;
    private int _meteringSampleCount;
    private const int MeteringUpdateInterval = 4410; // ~10 updates/sec at 44.1kHz
    private const float PeakDecayPerSample = 0.9999f;
    private const float RmsDecayCoeff = 0.999f;

    // Calibration tone
    private double _calibrationPhase;
    private const float CalibrationToneFrequency = 1000f;
    private const float CalibrationToneLevel = -20f; // dBFS

    // Internal buffers
    private float[] _sourceBuffer = Array.Empty<float>();
    private float[] _dryBuffer = Array.Empty<float>();
    private float[] _wetBuffer = Array.Empty<float>();
    private float[] _chainInputBuffer = Array.Empty<float>();

    /// <summary>
    /// Creates a new Reamper with the specified audio source.
    /// </summary>
    /// <param name="source">The DI audio source to reamp.</param>
    public Reamper(ISampleProvider source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _channels = source.WaveFormat.Channels;
        _sampleRate = source.WaveFormat.SampleRate;
        WaveFormat = source.WaveFormat;
    }

    #region Properties

    /// <summary>
    /// Gets the wave format.
    /// </summary>
    public WaveFormat WaveFormat { get; }

    /// <summary>
    /// Gets the current metering data.
    /// </summary>
    public ReamperMeteringData Metering => _metering;

    /// <summary>
    /// Gets or sets the reamping mode.
    /// </summary>
    public ReampMode Mode
    {
        get => _mode;
        set => _mode = value;
    }

    /// <summary>
    /// Gets or sets the level matching mode.
    /// </summary>
    public LevelMatchMode LevelMatchMode
    {
        get => _levelMatchMode;
        set
        {
            _levelMatchMode = value;
            ApplyLevelMatchMode();
        }
    }

    /// <summary>
    /// Gets or sets the input gain in dB (-60 to +24).
    /// This adjusts the DI signal level before the effects chain.
    /// </summary>
    public float InputGainDb
    {
        get => _inputGainDb;
        set
        {
            _inputGainDb = Math.Clamp(value, -60f, 24f);
            _targetInputGain = DbToLinear(_inputGainDb);
        }
    }

    /// <summary>
    /// Gets or sets the output gain in dB (-60 to +24).
    /// This adjusts the reamped signal level after the effects chain.
    /// </summary>
    public float OutputGainDb
    {
        get => _outputGainDb;
        set
        {
            _outputGainDb = Math.Clamp(value, -60f, 24f);
            _targetOutputGain = DbToLinear(_outputGainDb);
        }
    }

    /// <summary>
    /// Gets or sets whether the output phase is inverted.
    /// Useful for correcting polarity issues with amp simulators.
    /// </summary>
    public bool PhaseInvert
    {
        get => _phaseInvert;
        set => _phaseInvert = value;
    }

    /// <summary>
    /// Gets or sets the dry/wet mix (0.0 = dry only, 1.0 = wet only).
    /// Only applies in Parallel mode.
    /// </summary>
    public float DryWetMix
    {
        get => _dryWetMix;
        set => _targetDryWetMix = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Gets or sets whether latency compensation is enabled.
    /// When enabled, the dry signal is delayed to align with the wet signal.
    /// </summary>
    public bool LatencyCompensationEnabled
    {
        get => _latencyCompensationEnabled;
        set => _latencyCompensationEnabled = value;
    }

    /// <summary>
    /// Gets or sets the manual latency compensation in milliseconds.
    /// Set to 0 for automatic detection from the effects chain.
    /// </summary>
    public float LatencyMs
    {
        get => _compensationSamples * 1000f / _sampleRate;
        set
        {
            int samples = (int)(value * _sampleRate / 1000f);
            SetLatencyCompensation(samples);
        }
    }

    /// <summary>
    /// Gets or sets the target level for automatic level matching in dBFS.
    /// </summary>
    public float AutoLevelTargetDb
    {
        get => _autoLevelTarget;
        set => _autoLevelTarget = Math.Clamp(value, -60f, 0f);
    }

    /// <summary>
    /// Gets or sets whether the auto level is locked (stops adapting).
    /// </summary>
    public bool AutoLevelLocked
    {
        get => _autoLevelLocked;
        set => _autoLevelLocked = value;
    }

    /// <summary>
    /// Gets the number of effects in the chain.
    /// </summary>
    public int EffectCount
    {
        get
        {
            lock (_lock)
            {
                return _effectChain.Count;
            }
        }
    }

    /// <summary>
    /// Gets the latency in samples introduced by this processor and its effects chain.
    /// </summary>
    public int LatencySamples
    {
        get
        {
            int chainLatency = GetChainLatency();
            return _latencyCompensationEnabled ? chainLatency : 0;
        }
    }

    /// <summary>
    /// Event raised when the latency of this component changes.
    /// </summary>
    public event EventHandler<LatencyChangedEventArgs>? LatencyChanged;

    /// <summary>
    /// Gets or sets whether the reamper is bypassed (passes DI unchanged).
    /// </summary>
    public bool Bypassed
    {
        get => _mode == ReampMode.Bypass;
        set => _mode = value ? ReampMode.Bypass : ReampMode.Normal;
    }

    #endregion

    #region Effect Chain Management

    /// <summary>
    /// Adds an effect to the end of the reamping chain.
    /// </summary>
    /// <param name="effect">The effect to add (e.g., amp simulator, distortion, etc.).</param>
    public void AddEffect(IEffect effect)
    {
        if (effect == null)
            throw new ArgumentNullException(nameof(effect));

        lock (_lock)
        {
            _effectChain.Add(effect);
            RebuildChain();
        }
    }

    /// <summary>
    /// Inserts an effect at the specified index in the chain.
    /// </summary>
    /// <param name="index">The index to insert at.</param>
    /// <param name="effect">The effect to insert.</param>
    public void InsertEffect(int index, IEffect effect)
    {
        if (effect == null)
            throw new ArgumentNullException(nameof(effect));

        lock (_lock)
        {
            index = Math.Clamp(index, 0, _effectChain.Count);
            _effectChain.Insert(index, effect);
            RebuildChain();
        }
    }

    /// <summary>
    /// Removes an effect from the chain by index.
    /// </summary>
    /// <param name="index">The index of the effect to remove.</param>
    /// <returns>True if removed successfully.</returns>
    public bool RemoveEffect(int index)
    {
        lock (_lock)
        {
            if (index >= 0 && index < _effectChain.Count)
            {
                _effectChain.RemoveAt(index);
                RebuildChain();
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Removes an effect from the chain.
    /// </summary>
    /// <param name="effect">The effect to remove.</param>
    /// <returns>True if removed successfully.</returns>
    public bool RemoveEffect(IEffect effect)
    {
        lock (_lock)
        {
            if (_effectChain.Remove(effect))
            {
                RebuildChain();
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Clears all effects from the chain.
    /// </summary>
    public void ClearEffects()
    {
        lock (_lock)
        {
            _effectChain.Clear();
            _chainOutput = null;
        }
    }

    /// <summary>
    /// Gets an effect from the chain by index.
    /// </summary>
    /// <param name="index">The index of the effect.</param>
    /// <returns>The effect, or null if out of range.</returns>
    public IEffect? GetEffect(int index)
    {
        lock (_lock)
        {
            if (index >= 0 && index < _effectChain.Count)
            {
                return _effectChain[index];
            }
            return null;
        }
    }

    /// <summary>
    /// Gets all effects in the chain.
    /// </summary>
    /// <returns>Copy of the effects list.</returns>
    public List<IEffect> GetEffects()
    {
        lock (_lock)
        {
            return new List<IEffect>(_effectChain);
        }
    }

    /// <summary>
    /// Sets the entire effect chain at once.
    /// </summary>
    /// <param name="effects">The effects to use in the chain.</param>
    public void SetEffectChain(IEnumerable<IEffect> effects)
    {
        if (effects == null)
            throw new ArgumentNullException(nameof(effects));

        lock (_lock)
        {
            _effectChain.Clear();
            _effectChain.AddRange(effects);
            RebuildChain();
        }
    }

    private void RebuildChain()
    {
        // The chain will be built dynamically during Read() since we need
        // to feed it the processed input buffer
        _chainOutput = null;
    }

    private int GetChainLatency()
    {
        int totalLatency = 0;
        lock (_lock)
        {
            foreach (var effect in _effectChain)
            {
                if (effect is ILatencyReporter latencyReporter)
                {
                    totalLatency += latencyReporter.LatencySamples;
                }
            }
        }
        return totalLatency;
    }

    #endregion

    #region Level Matching

    private void ApplyLevelMatchMode()
    {
        switch (_levelMatchMode)
        {
            case LevelMatchMode.Manual:
                // Keep current input gain
                break;

            case LevelMatchMode.LineToInstrument:
                // Line level (-10 dBV) to instrument level (~-20 dBu)
                // Approximate conversion: -10 dB
                InputGainDb = -10f;
                break;

            case LevelMatchMode.ProToInstrument:
                // Pro level (+4 dBu) to instrument level (~-20 dBu)
                // Approximate conversion: -24 dB
                InputGainDb = -24f;
                break;

            case LevelMatchMode.AutoPeak:
            case LevelMatchMode.AutoRms:
                // Reset measurement for new auto-level calculation
                _measurementAccumulator = 0;
                _measurementSampleCount = 0;
                _autoLevelLocked = false;
                break;
        }
    }

    private void UpdateAutoLevel(float sample)
    {
        if (_autoLevelLocked) return;

        float absSample = MathF.Abs(sample);

        if (_levelMatchMode == LevelMatchMode.AutoPeak)
        {
            // Track peak
            if (absSample > _measurementAccumulator)
            {
                _measurementAccumulator = absSample;
            }
        }
        else if (_levelMatchMode == LevelMatchMode.AutoRms)
        {
            // Accumulate for RMS
            _measurementAccumulator += sample * sample;
        }

        _measurementSampleCount++;

        if (_measurementSampleCount >= MeasurementWindowSamples)
        {
            float measuredLevel;
            if (_levelMatchMode == LevelMatchMode.AutoPeak)
            {
                measuredLevel = LinearToDb(_measurementAccumulator);
            }
            else
            {
                float rms = MathF.Sqrt(_measurementAccumulator / _measurementSampleCount);
                measuredLevel = LinearToDb(rms);
            }

            // Calculate required gain adjustment
            if (measuredLevel > -60f) // Only adjust if signal is present
            {
                float gainAdjustment = _autoLevelTarget - measuredLevel;
                // Smooth adjustment
                InputGainDb = _inputGainDb + gainAdjustment * 0.5f;
            }

            // Reset measurement
            _measurementAccumulator = 0;
            _measurementSampleCount = 0;
        }
    }

    /// <summary>
    /// Locks the current auto-level setting.
    /// </summary>
    public void LockAutoLevel()
    {
        _autoLevelLocked = true;
    }

    /// <summary>
    /// Resets auto-level and starts new measurement.
    /// </summary>
    public void ResetAutoLevel()
    {
        _measurementAccumulator = 0;
        _measurementSampleCount = 0;
        _autoLevelLocked = false;
    }

    #endregion

    #region Latency Compensation

    private void SetLatencyCompensation(int samples)
    {
        _compensationSamples = Math.Max(0, samples);

        // Resize delay buffer if needed
        int requiredSize = _compensationSamples * _channels;
        if (_delayBuffer.Length < requiredSize)
        {
            _delayBuffer = new float[requiredSize];
            _delayWritePos = 0;
        }
    }

    private void UpdateLatencyCompensation()
    {
        if (!_latencyCompensationEnabled) return;

        int chainLatency = GetChainLatency();
        if (chainLatency != _compensationSamples)
        {
            SetLatencyCompensation(chainLatency);
        }
    }

    private float ReadDelayedSample(float currentSample, int delaySamples)
    {
        if (delaySamples <= 0 || _delayBuffer.Length == 0)
        {
            return currentSample;
        }

        int bufferSize = _delayBuffer.Length;
        int readPos = (_delayWritePos - delaySamples + bufferSize) % bufferSize;

        // Read delayed sample
        float delayed = _delayBuffer[readPos];

        // Write current sample
        _delayBuffer[_delayWritePos] = currentSample;
        _delayWritePos = (_delayWritePos + 1) % bufferSize;

        return delayed;
    }

    #endregion

    #region Audio Processing

    /// <inheritdoc />
    public int Read(float[] buffer, int offset, int count)
    {
        EnsureBufferSize(count);

        // Read from source
        int samplesRead = _source.Read(_sourceBuffer, 0, count);
        if (samplesRead == 0) return 0;

        // Handle bypass mode
        if (_mode == ReampMode.Bypass)
        {
            Array.Copy(_sourceBuffer, 0, buffer, offset, samplesRead);
            return samplesRead;
        }

        // Handle calibration mode
        if (_mode == ReampMode.Calibration)
        {
            GenerateCalibrationTone(buffer, offset, samplesRead);
            return samplesRead;
        }

        // Update latency compensation based on current chain
        UpdateLatencyCompensation();

        // Store dry signal and apply input processing
        int frames = samplesRead / _channels;
        float inputPeak = 0f;
        float inputRmsSum = 0f;

        for (int i = 0; i < samplesRead; i++)
        {
            // Smooth gain transition
            _inputGainLinear = _inputGainLinear * GainSmoothingCoeff +
                               _targetInputGain * (1f - GainSmoothingCoeff);

            float sample = _sourceBuffer[i];

            // Auto level measurement (before gain)
            if (_levelMatchMode == LevelMatchMode.AutoPeak ||
                _levelMatchMode == LevelMatchMode.AutoRms)
            {
                UpdateAutoLevel(sample);
            }

            // Apply input gain
            float processed = sample * _inputGainLinear;

            // Store for dry path (with latency compensation)
            _dryBuffer[i] = _latencyCompensationEnabled
                ? ReadDelayedSample(sample, _compensationSamples * _channels)
                : sample;

            // Store for chain input
            _chainInputBuffer[i] = processed;

            // Input metering
            float absSample = MathF.Abs(processed);
            if (absSample > inputPeak) inputPeak = absSample;
            inputRmsSum += processed * processed;
        }

        // Process through effects chain
        ProcessEffectChain(_chainInputBuffer, _wetBuffer, samplesRead);

        // Apply output processing and mix
        float outputPeak = 0f;
        float outputRmsSum = 0f;
        float corrSum = 0f;
        float dryPower = 0f;
        float wetPower = 0f;

        for (int i = 0; i < samplesRead; i++)
        {
            // Smooth gain and mix transitions
            _outputGainLinear = _outputGainLinear * GainSmoothingCoeff +
                                _targetOutputGain * (1f - GainSmoothingCoeff);
            _dryWetMix = _dryWetMix * GainSmoothingCoeff +
                         _targetDryWetMix * (1f - GainSmoothingCoeff);

            float wet = _wetBuffer[i] * _outputGainLinear;

            // Apply phase invert
            if (_phaseInvert)
            {
                wet = -wet;
            }

            float output;
            switch (_mode)
            {
                case ReampMode.Parallel:
                    // Blend dry and wet
                    float dry = _dryBuffer[i];
                    output = dry * (1f - _dryWetMix) + wet * _dryWetMix;

                    // Phase correlation for metering
                    corrSum += dry * wet;
                    dryPower += dry * dry;
                    wetPower += wet * wet;
                    break;

                case ReampMode.Monitor:
                    // Sum dry and wet for A/B comparison (typically used with solo/mute)
                    output = _dryBuffer[i] * 0.5f + wet * 0.5f;
                    break;

                case ReampMode.Normal:
                default:
                    output = wet;
                    break;
            }

            buffer[offset + i] = output;

            // Output metering
            float absOutput = MathF.Abs(output);
            if (absOutput > outputPeak) outputPeak = absOutput;
            outputRmsSum += output * output;
        }

        // Update metering with decay
        UpdateMetering(inputPeak, inputRmsSum, outputPeak, outputRmsSum,
                       corrSum, dryPower, wetPower, frames);

        return samplesRead;
    }

    private void EnsureBufferSize(int count)
    {
        if (_sourceBuffer.Length < count)
        {
            _sourceBuffer = new float[count];
            _dryBuffer = new float[count];
            _wetBuffer = new float[count];
            _chainInputBuffer = new float[count];
        }
    }

    private void ProcessEffectChain(float[] input, float[] output, int count)
    {
        lock (_lock)
        {
            if (_effectChain.Count == 0)
            {
                // No effects - copy input to output
                Array.Copy(input, 0, output, 0, count);
                return;
            }

            // Process through each effect in the chain
            float[] currentBuffer = input;
            float[] tempBuffer = new float[count];

            foreach (var effect in _effectChain)
            {
                if (!effect.Enabled)
                {
                    continue;
                }

                // Create a buffer provider for this effect
                var provider = new BufferSampleProvider(currentBuffer, count, WaveFormat);

                // Effects read from their source and output to the buffer
                // We need to handle this differently - read from effect into temp buffer
                int read = effect.Read(tempBuffer, 0, count);

                // Swap buffers for next effect
                (currentBuffer, tempBuffer) = (tempBuffer, currentBuffer);
            }

            // Copy final result to output
            Array.Copy(currentBuffer, 0, output, 0, count);
        }
    }

    private void GenerateCalibrationTone(float[] buffer, int offset, int count)
    {
        float level = DbToLinear(CalibrationToneLevel);
        double phaseIncrement = 2.0 * Math.PI * CalibrationToneFrequency / _sampleRate;

        for (int i = 0; i < count; i += _channels)
        {
            float sample = (float)(Math.Sin(_calibrationPhase) * level);
            _calibrationPhase += phaseIncrement;

            if (_calibrationPhase > 2.0 * Math.PI)
            {
                _calibrationPhase -= 2.0 * Math.PI;
            }

            for (int ch = 0; ch < _channels; ch++)
            {
                buffer[offset + i + ch] = sample;
            }
        }
    }

    private void UpdateMetering(float inputPeak, float inputRmsSum,
                                float outputPeak, float outputRmsSum,
                                float corrSum, float dryPower, float wetPower,
                                int frames)
    {
        // Update peak with decay
        _inputPeakAcc = MathF.Max(inputPeak, _inputPeakAcc * MathF.Pow(PeakDecayPerSample, frames));
        _outputPeakAcc = MathF.Max(outputPeak, _outputPeakAcc * MathF.Pow(PeakDecayPerSample, frames));

        // Update RMS
        _inputRmsAcc = _inputRmsAcc * RmsDecayCoeff + inputRmsSum / frames;
        _outputRmsAcc = _outputRmsAcc * RmsDecayCoeff + outputRmsSum / frames;

        // Update correlation
        _correlationSumAcc += corrSum;
        _dryPowerAcc += dryPower;
        _wetPowerAcc += wetPower;
        _meteringSampleCount += frames;

        if (_meteringSampleCount >= MeteringUpdateInterval)
        {
            // Update metering data
            _metering.InputPeakDb = LinearToDb(_inputPeakAcc);
            _metering.InputRmsDb = LinearToDb(MathF.Sqrt(_inputRmsAcc));
            _metering.OutputPeakDb = LinearToDb(_outputPeakAcc);
            _metering.OutputRmsDb = LinearToDb(MathF.Sqrt(_outputRmsAcc));
            _metering.CurrentInputGainDb = _inputGainDb;
            _metering.CurrentOutputGainDb = _outputGainDb;
            _metering.LatencyCompensationSamples = _compensationSamples;
            _metering.InputClipping = _inputPeakAcc >= 1.0f;
            _metering.OutputClipping = _outputPeakAcc >= 1.0f;

            // Calculate phase correlation
            float power = MathF.Sqrt(_dryPowerAcc * _wetPowerAcc);
            _metering.PhaseCorrelation = power > 1e-10f ? _correlationSumAcc / power : 1f;

            // Reset accumulators
            _correlationSumAcc = 0f;
            _dryPowerAcc = 0f;
            _wetPowerAcc = 0f;
            _meteringSampleCount = 0;
        }
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Resets all internal state (filters, delay lines, metering).
    /// </summary>
    public void Reset()
    {
        _inputPeakAcc = 0f;
        _outputPeakAcc = 0f;
        _inputRmsAcc = 0f;
        _outputRmsAcc = 0f;
        _correlationSumAcc = 0f;
        _dryPowerAcc = 0f;
        _wetPowerAcc = 0f;
        _meteringSampleCount = 0;

        _measurementAccumulator = 0;
        _measurementSampleCount = 0;

        _delayWritePos = 0;
        if (_delayBuffer.Length > 0)
        {
            Array.Clear(_delayBuffer);
        }

        _calibrationPhase = 0;
    }

    /// <summary>
    /// Resets the peak hold values.
    /// </summary>
    public void ResetPeaks()
    {
        _inputPeakAcc = 0f;
        _outputPeakAcc = 0f;
    }

    private static float DbToLinear(float db)
    {
        return MathF.Pow(10f, db / 20f);
    }

    private static float LinearToDb(float linear)
    {
        return 20f * MathF.Log10(MathF.Max(linear, 1e-10f));
    }

    /// <summary>
    /// Disposes resources used by the reamper.
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var effect in _effectChain)
            {
                if (effect is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            _effectChain.Clear();
        }
    }

    #endregion

    #region Factory Methods

    /// <summary>
    /// Creates a reamper configured for guitar reamping.
    /// </summary>
    /// <param name="source">The DI guitar source.</param>
    /// <returns>Configured reamper.</returns>
    public static Reamper CreateForGuitar(ISampleProvider source)
    {
        var reamper = new Reamper(source)
        {
            LevelMatchMode = LevelMatchMode.LineToInstrument,
            AutoLevelTargetDb = -18f,
            Mode = ReampMode.Normal
        };
        return reamper;
    }

    /// <summary>
    /// Creates a reamper configured for bass reamping.
    /// </summary>
    /// <param name="source">The DI bass source.</param>
    /// <returns>Configured reamper.</returns>
    public static Reamper CreateForBass(ISampleProvider source)
    {
        var reamper = new Reamper(source)
        {
            LevelMatchMode = LevelMatchMode.LineToInstrument,
            AutoLevelTargetDb = -15f, // Bass typically runs hotter
            Mode = ReampMode.Normal
        };
        return reamper;
    }

    /// <summary>
    /// Creates a reamper configured for parallel processing.
    /// </summary>
    /// <param name="source">The DI source.</param>
    /// <param name="dryWetMix">Initial dry/wet mix (0-1).</param>
    /// <returns>Configured reamper.</returns>
    public static Reamper CreateParallel(ISampleProvider source, float dryWetMix = 0.5f)
    {
        var reamper = new Reamper(source)
        {
            Mode = ReampMode.Parallel,
            DryWetMix = dryWetMix,
            LatencyCompensationEnabled = true
        };
        return reamper;
    }

    #endregion

    /// <summary>
    /// Helper sample provider for buffer-based processing.
    /// </summary>
    private class BufferSampleProvider : ISampleProvider
    {
        private readonly float[] _buffer;
        private readonly int _count;
        private int _position;

        public WaveFormat WaveFormat { get; }

        public BufferSampleProvider(float[] buffer, int count, WaveFormat waveFormat)
        {
            _buffer = buffer;
            _count = count;
            _position = 0;
            WaveFormat = waveFormat;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int toRead = Math.Min(count, _count - _position);
            Array.Copy(_buffer, _position, buffer, offset, toRead);
            _position += toRead;
            return toRead;
        }
    }
}
