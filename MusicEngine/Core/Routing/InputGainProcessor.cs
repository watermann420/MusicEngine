// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio/MIDI routing component.

using System;
using NAudio.Wave;

namespace MusicEngine.Core.Routing;

/// <summary>
/// Channel mode for input processing.
/// </summary>
public enum InputChannelMode
{
    /// <summary>
    /// Stereo mode - processes both channels independently.
    /// </summary>
    Stereo,

    /// <summary>
    /// Mono mode - sums channels to mono.
    /// </summary>
    Mono,

    /// <summary>
    /// Left only - uses only left channel.
    /// </summary>
    LeftOnly,

    /// <summary>
    /// Right only - uses only right channel.
    /// </summary>
    RightOnly,

    /// <summary>
    /// Mid/Side mode - converts stereo to M/S.
    /// </summary>
    MidSide,

    /// <summary>
    /// Swap channels - swaps left and right.
    /// </summary>
    SwapChannels
}

/// <summary>
/// Pre-insert input gain processor with phase invert, channel modes, and metering tap point.
/// </summary>
/// <remarks>
/// This processor is typically placed at the beginning of the signal chain before insert effects.
/// Provides gain control from -infinity to +24dB, phase inversion, and various channel routing modes.
/// Includes metering tap points for input level monitoring.
/// </remarks>
public class InputGainProcessor : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly int _channels;
    private readonly int _sampleRate;

    // Gain parameters
    private float _gainDb = 0f;
    private float _gainLinear = 1f;
    private float _targetGain = 1f;
    private const float GainSmoothingCoeff = 0.9995f;

    // Phase invert
    private bool _phaseInvertLeft;
    private bool _phaseInvertRight;

    // Channel mode
    private InputChannelMode _channelMode = InputChannelMode.Stereo;

    // Metering
    private float _peakLeft;
    private float _peakRight;
    private float _rmsLeft;
    private float _rmsRight;
    private float _rmsAccumLeft;
    private float _rmsAccumRight;
    private int _rmsSampleCount;
    private const int RmsWindowSize = 4410; // ~100ms at 44.1kHz

    // Peak hold
    private float _peakHoldLeft;
    private float _peakHoldRight;
    private int _peakHoldSamples;
    private int _peakHoldCounter;
    private const int DefaultPeakHoldMs = 2000;

    // Mute/Solo
    private bool _muted;

    /// <summary>
    /// Creates a new input gain processor.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    public InputGainProcessor(ISampleProvider source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _channels = source.WaveFormat.Channels;
        _sampleRate = source.WaveFormat.SampleRate;
        _peakHoldSamples = (int)(_sampleRate * DefaultPeakHoldMs / 1000.0);
    }

    /// <summary>
    /// Gets the wave format.
    /// </summary>
    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>
    /// Gets or sets the input gain in dB (-infinity to +24).
    /// Use float.NegativeInfinity for complete silence.
    /// </summary>
    public float GainDb
    {
        get => _gainDb;
        set
        {
            _gainDb = Math.Clamp(value, float.NegativeInfinity, 24f);
            _targetGain = float.IsNegativeInfinity(_gainDb) ? 0f : MathF.Pow(10f, _gainDb / 20f);
        }
    }

    /// <summary>
    /// Gets or sets the input gain as a linear value (0 to ~15.85).
    /// </summary>
    public float GainLinear
    {
        get => _gainLinear;
        set
        {
            _targetGain = Math.Clamp(value, 0f, MathF.Pow(10f, 24f / 20f));
            _gainDb = _targetGain > 0 ? 20f * MathF.Log10(_targetGain) : float.NegativeInfinity;
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
    /// Gets or sets whether both channels' phases are inverted.
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
    /// Gets or sets the channel mode.
    /// </summary>
    public InputChannelMode ChannelMode
    {
        get => _channelMode;
        set => _channelMode = value;
    }

    /// <summary>
    /// Gets or sets whether the input is muted.
    /// </summary>
    public bool Muted
    {
        get => _muted;
        set => _muted = value;
    }

    /// <summary>
    /// Gets or sets the peak hold time in milliseconds.
    /// </summary>
    public int PeakHoldTimeMs
    {
        get => (int)(_peakHoldSamples * 1000.0 / _sampleRate);
        set => _peakHoldSamples = (int)(_sampleRate * value / 1000.0);
    }

    #region Metering Properties

    /// <summary>
    /// Gets the current peak level for the left channel (linear).
    /// </summary>
    public float PeakLeft => _peakLeft;

    /// <summary>
    /// Gets the current peak level for the right channel (linear).
    /// </summary>
    public float PeakRight => _peakRight;

    /// <summary>
    /// Gets the current peak level for the left channel in dB.
    /// </summary>
    public float PeakLeftDb => _peakLeft > 0 ? 20f * MathF.Log10(_peakLeft) : float.NegativeInfinity;

    /// <summary>
    /// Gets the current peak level for the right channel in dB.
    /// </summary>
    public float PeakRightDb => _peakRight > 0 ? 20f * MathF.Log10(_peakRight) : float.NegativeInfinity;

    /// <summary>
    /// Gets the peak hold level for the left channel (linear).
    /// </summary>
    public float PeakHoldLeft => _peakHoldLeft;

    /// <summary>
    /// Gets the peak hold level for the right channel (linear).
    /// </summary>
    public float PeakHoldRight => _peakHoldRight;

    /// <summary>
    /// Gets the peak hold level for the left channel in dB.
    /// </summary>
    public float PeakHoldLeftDb => _peakHoldLeft > 0 ? 20f * MathF.Log10(_peakHoldLeft) : float.NegativeInfinity;

    /// <summary>
    /// Gets the peak hold level for the right channel in dB.
    /// </summary>
    public float PeakHoldRightDb => _peakHoldRight > 0 ? 20f * MathF.Log10(_peakHoldRight) : float.NegativeInfinity;

    /// <summary>
    /// Gets the RMS level for the left channel (linear).
    /// </summary>
    public float RmsLeft => _rmsLeft;

    /// <summary>
    /// Gets the RMS level for the right channel (linear).
    /// </summary>
    public float RmsRight => _rmsRight;

    /// <summary>
    /// Gets the RMS level for the left channel in dB.
    /// </summary>
    public float RmsLeftDb => _rmsLeft > 0 ? 20f * MathF.Log10(_rmsLeft) : float.NegativeInfinity;

    /// <summary>
    /// Gets the RMS level for the right channel in dB.
    /// </summary>
    public float RmsRightDb => _rmsRight > 0 ? 20f * MathF.Log10(_rmsRight) : float.NegativeInfinity;

    /// <summary>
    /// Gets whether the left channel is clipping (>= 1.0).
    /// </summary>
    public bool ClippingLeft => _peakLeft >= 1.0f;

    /// <summary>
    /// Gets whether the right channel is clipping (>= 1.0).
    /// </summary>
    public bool ClippingRight => _peakRight >= 1.0f;

    #endregion

    /// <summary>
    /// Resets the metering values.
    /// </summary>
    public void ResetMeters()
    {
        _peakLeft = 0;
        _peakRight = 0;
        _peakHoldLeft = 0;
        _peakHoldRight = 0;
        _peakHoldCounter = 0;
        _rmsLeft = 0;
        _rmsRight = 0;
        _rmsAccumLeft = 0;
        _rmsAccumRight = 0;
        _rmsSampleCount = 0;
    }

    /// <summary>
    /// Resets peak hold values only.
    /// </summary>
    public void ResetPeakHold()
    {
        _peakHoldLeft = 0;
        _peakHoldRight = 0;
        _peakHoldCounter = 0;
    }

    /// <summary>
    /// Reads and processes audio samples.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = _source.Read(buffer, offset, count);
        if (samplesRead == 0) return 0;

        // Process samples
        int frames = samplesRead / _channels;

        // Reset peak for this buffer
        float bufferPeakLeft = 0;
        float bufferPeakRight = 0;

        for (int frame = 0; frame < frames; frame++)
        {
            int baseIndex = offset + frame * _channels;

            // Read input samples
            float left = _channels >= 1 ? buffer[baseIndex] : 0;
            float right = _channels >= 2 ? buffer[baseIndex + 1] : left;

            // Apply channel mode
            (left, right) = ApplyChannelMode(left, right);

            // Apply phase invert
            if (_phaseInvertLeft) left = -left;
            if (_phaseInvertRight) right = -right;

            // Smooth gain transition
            _gainLinear = _gainLinear * GainSmoothingCoeff + _targetGain * (1f - GainSmoothingCoeff);

            // Apply gain
            float gain = _muted ? 0f : _gainLinear;
            left *= gain;
            right *= gain;

            // Update metering (after gain)
            float absLeft = MathF.Abs(left);
            float absRight = MathF.Abs(right);

            if (absLeft > bufferPeakLeft) bufferPeakLeft = absLeft;
            if (absRight > bufferPeakRight) bufferPeakRight = absRight;

            // RMS accumulation
            _rmsAccumLeft += left * left;
            _rmsAccumRight += right * right;
            _rmsSampleCount++;

            if (_rmsSampleCount >= RmsWindowSize)
            {
                _rmsLeft = MathF.Sqrt(_rmsAccumLeft / _rmsSampleCount);
                _rmsRight = MathF.Sqrt(_rmsAccumRight / _rmsSampleCount);
                _rmsAccumLeft = 0;
                _rmsAccumRight = 0;
                _rmsSampleCount = 0;
            }

            // Write output
            if (_channels >= 1) buffer[baseIndex] = left;
            if (_channels >= 2) buffer[baseIndex + 1] = right;
        }

        // Update peak with decay
        const float peakDecay = 0.9995f;
        _peakLeft = MathF.Max(bufferPeakLeft, _peakLeft * peakDecay);
        _peakRight = MathF.Max(bufferPeakRight, _peakRight * peakDecay);

        // Update peak hold
        if (bufferPeakLeft >= _peakHoldLeft)
        {
            _peakHoldLeft = bufferPeakLeft;
            _peakHoldCounter = 0;
        }
        if (bufferPeakRight >= _peakHoldRight)
        {
            _peakHoldRight = bufferPeakRight;
            _peakHoldCounter = 0;
        }

        _peakHoldCounter += frames;
        if (_peakHoldCounter >= _peakHoldSamples)
        {
            _peakHoldLeft *= 0.95f; // Gradual decay after hold
            _peakHoldRight *= 0.95f;
        }

        return samplesRead;
    }

    private (float left, float right) ApplyChannelMode(float left, float right)
    {
        return _channelMode switch
        {
            InputChannelMode.Stereo => (left, right),
            InputChannelMode.Mono => ((left + right) * 0.5f, (left + right) * 0.5f),
            InputChannelMode.LeftOnly => (left, left),
            InputChannelMode.RightOnly => (right, right),
            InputChannelMode.MidSide => ((left + right) * 0.5f, (left - right) * 0.5f), // Mid, Side
            InputChannelMode.SwapChannels => (right, left),
            _ => (left, right)
        };
    }

    /// <summary>
    /// Gets the current metering state as a snapshot.
    /// </summary>
    public InputMeterSnapshot GetMeterSnapshot()
    {
        return new InputMeterSnapshot
        {
            PeakLeft = _peakLeft,
            PeakRight = _peakRight,
            PeakHoldLeft = _peakHoldLeft,
            PeakHoldRight = _peakHoldRight,
            RmsLeft = _rmsLeft,
            RmsRight = _rmsRight,
            GainDb = _gainDb,
            ClippingLeft = ClippingLeft,
            ClippingRight = ClippingRight
        };
    }

    /// <summary>
    /// Creates an input gain processor with unity gain.
    /// </summary>
    public static InputGainProcessor CreateUnity(ISampleProvider source)
    {
        return new InputGainProcessor(source) { GainDb = 0 };
    }

    /// <summary>
    /// Creates an input gain processor with specified gain.
    /// </summary>
    public static InputGainProcessor Create(ISampleProvider source, float gainDb)
    {
        return new InputGainProcessor(source) { GainDb = gainDb };
    }

    /// <summary>
    /// Creates an input gain processor in mono mode.
    /// </summary>
    public static InputGainProcessor CreateMono(ISampleProvider source)
    {
        return new InputGainProcessor(source) { ChannelMode = InputChannelMode.Mono };
    }
}

/// <summary>
/// Snapshot of input metering values.
/// </summary>
public struct InputMeterSnapshot
{
    /// <summary>
    /// Current peak level for left channel (linear).
    /// </summary>
    public float PeakLeft { get; set; }

    /// <summary>
    /// Current peak level for right channel (linear).
    /// </summary>
    public float PeakRight { get; set; }

    /// <summary>
    /// Peak hold level for left channel (linear).
    /// </summary>
    public float PeakHoldLeft { get; set; }

    /// <summary>
    /// Peak hold level for right channel (linear).
    /// </summary>
    public float PeakHoldRight { get; set; }

    /// <summary>
    /// RMS level for left channel (linear).
    /// </summary>
    public float RmsLeft { get; set; }

    /// <summary>
    /// RMS level for right channel (linear).
    /// </summary>
    public float RmsRight { get; set; }

    /// <summary>
    /// Current gain in dB.
    /// </summary>
    public float GainDb { get; set; }

    /// <summary>
    /// Whether left channel is clipping.
    /// </summary>
    public bool ClippingLeft { get; set; }

    /// <summary>
    /// Whether right channel is clipping.
    /// </summary>
    public bool ClippingRight { get; set; }

    /// <summary>
    /// Gets peak left in dB.
    /// </summary>
    public readonly float PeakLeftDb => PeakLeft > 0 ? 20f * MathF.Log10(PeakLeft) : float.NegativeInfinity;

    /// <summary>
    /// Gets peak right in dB.
    /// </summary>
    public readonly float PeakRightDb => PeakRight > 0 ? 20f * MathF.Log10(PeakRight) : float.NegativeInfinity;

    /// <summary>
    /// Gets RMS left in dB.
    /// </summary>
    public readonly float RmsLeftDb => RmsLeft > 0 ? 20f * MathF.Log10(RmsLeft) : float.NegativeInfinity;

    /// <summary>
    /// Gets RMS right in dB.
    /// </summary>
    public readonly float RmsRightDb => RmsRight > 0 ? 20f * MathF.Log10(RmsRight) : float.NegativeInfinity;
}
