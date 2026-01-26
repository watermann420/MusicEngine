//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Envelope follower utility for extracting amplitude envelope from audio signals.
//              Useful for ducking, gating, dynamic modulation, and sidechain effects.

using System;
using NAudio.Wave;

namespace MusicEngine.Core.Utilities;

/// <summary>
/// Detection mode for envelope follower.
/// </summary>
public enum EnvelopeDetectionMode
{
    /// <summary>
    /// Peak detection - follows instantaneous peaks.
    /// </summary>
    Peak,

    /// <summary>
    /// RMS detection - smoother, follows average power.
    /// </summary>
    RMS,

    /// <summary>
    /// True peak detection with interpolation.
    /// </summary>
    TruePeak
}

/// <summary>
/// Event arguments for envelope level changes.
/// </summary>
public class EnvelopeLevelChangedEventArgs : EventArgs
{
    /// <summary>
    /// The current envelope level (0.0 to 1.0).
    /// </summary>
    public float CurrentLevel { get; }

    /// <summary>
    /// The current envelope level mapped to the output range.
    /// </summary>
    public float MappedLevel { get; }

    /// <summary>
    /// The peak envelope level since last reset.
    /// </summary>
    public float PeakLevel { get; }

    /// <summary>
    /// The current envelope level in decibels.
    /// </summary>
    public float LevelDb { get; }

    /// <summary>
    /// Creates a new EnvelopeLevelChangedEventArgs.
    /// </summary>
    public EnvelopeLevelChangedEventArgs(float currentLevel, float mappedLevel, float peakLevel, float levelDb)
    {
        CurrentLevel = currentLevel;
        MappedLevel = mappedLevel;
        PeakLevel = peakLevel;
        LevelDb = levelDb;
    }
}

/// <summary>
/// Event arguments for envelope threshold crossing.
/// </summary>
public class EnvelopeThresholdEventArgs : EventArgs
{
    /// <summary>
    /// True if level crossed above threshold, false if crossed below.
    /// </summary>
    public bool AboveThreshold { get; }

    /// <summary>
    /// The current envelope level.
    /// </summary>
    public float CurrentLevel { get; }

    /// <summary>
    /// The threshold that was crossed.
    /// </summary>
    public float Threshold { get; }

    /// <summary>
    /// Creates a new EnvelopeThresholdEventArgs.
    /// </summary>
    public EnvelopeThresholdEventArgs(bool aboveThreshold, float currentLevel, float threshold)
    {
        AboveThreshold = aboveThreshold;
        CurrentLevel = currentLevel;
        Threshold = threshold;
    }
}

/// <summary>
/// Extracts amplitude envelope from an audio signal for modulation and analysis purposes.
/// </summary>
/// <remarks>
/// Features:
/// - Peak, RMS, and True Peak detection modes
/// - Configurable attack and release times
/// - Sensitivity control for response curve shaping
/// - Output range mapping (min/max)
/// - Threshold detection with events
/// - Hold time support
/// - Stereo link option
/// - Per-channel or summed operation
/// - Real-time level monitoring
/// - Sidechain input support
/// </remarks>
public class EnvelopeFollower : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly int _channels;
    private readonly int _sampleRate;

    // Envelope state per channel
    private readonly float[] _envelopeState;
    private float _linkedEnvelope;

    // RMS calculation state
    private readonly float[][] _rmsBuffers;
    private readonly int[] _rmsWritePos;
    private int _rmsWindowSamples;

    // True peak interpolation state
    private readonly float[][] _truePeakHistory;
    private readonly int[] _truePeakWritePos;

    // Peak hold state
    private float _heldPeakLevel;
    private int _holdSamplesRemaining;
    private int _holdSamples;

    // Threshold tracking
    private bool _wasAboveThreshold;

    // Event update tracking
    private int _eventUpdateCounter;
    private int _eventUpdateInterval;

    // Peak level tracking
    private float _peakLevelSinceReset;

    /// <summary>
    /// Creates a new EnvelopeFollower with the specified audio source.
    /// </summary>
    /// <param name="source">Audio source to extract envelope from.</param>
    public EnvelopeFollower(ISampleProvider source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _channels = source.WaveFormat.Channels;
        _sampleRate = source.WaveFormat.SampleRate;

        _envelopeState = new float[_channels];
        _rmsBuffers = new float[_channels][];
        _rmsWritePos = new int[_channels];
        _truePeakHistory = new float[_channels][];
        _truePeakWritePos = new int[_channels];

        // Initialize RMS buffers (10ms window default)
        _rmsWindowSamples = Math.Max(1, (int)(_sampleRate * 0.01));
        for (int ch = 0; ch < _channels; ch++)
        {
            _rmsBuffers[ch] = new float[_rmsWindowSamples];
            _truePeakHistory[ch] = new float[4]; // 4-point interpolation
        }

        // Set defaults
        AttackMs = 10f;
        ReleaseMs = 100f;
        Sensitivity = 1f;
        OutputMin = 0f;
        OutputMax = 1f;
        DetectionMode = EnvelopeDetectionMode.Peak;
        StereoLink = true;
        HoldTimeMs = 0f;
        Threshold = 0f;
        EventUpdateRateHz = 60f;

        UpdateCoefficients();
    }

    /// <summary>
    /// Gets the wave format of the audio stream.
    /// </summary>
    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>
    /// Gets or sets the attack time in milliseconds (0.1 to 500).
    /// Controls how fast the envelope responds to increasing levels.
    /// </summary>
    public float AttackMs
    {
        get => _attackMs;
        set
        {
            _attackMs = Math.Clamp(value, 0.1f, 500f);
            UpdateCoefficients();
        }
    }
    private float _attackMs;

    /// <summary>
    /// Gets or sets the release time in milliseconds (1 to 5000).
    /// Controls how fast the envelope responds to decreasing levels.
    /// </summary>
    public float ReleaseMs
    {
        get => _releaseMs;
        set
        {
            _releaseMs = Math.Clamp(value, 1f, 5000f);
            UpdateCoefficients();
        }
    }
    private float _releaseMs;

    /// <summary>
    /// Gets or sets the sensitivity/curve control (0.1 to 10).
    /// Values less than 1 compress the response, greater than 1 expand it.
    /// </summary>
    public float Sensitivity
    {
        get => _sensitivity;
        set => _sensitivity = Math.Clamp(value, 0.1f, 10f);
    }
    private float _sensitivity;

    /// <summary>
    /// Gets or sets the minimum output value (0 to 1).
    /// </summary>
    public float OutputMin
    {
        get => _outputMin;
        set => _outputMin = Math.Clamp(value, 0f, 1f);
    }
    private float _outputMin;

    /// <summary>
    /// Gets or sets the maximum output value (0 to 1).
    /// </summary>
    public float OutputMax
    {
        get => _outputMax;
        set => _outputMax = Math.Clamp(value, 0f, 1f);
    }
    private float _outputMax;

    /// <summary>
    /// Gets or sets the detection mode (Peak, RMS, or TruePeak).
    /// </summary>
    public EnvelopeDetectionMode DetectionMode { get; set; }

    /// <summary>
    /// Gets or sets whether stereo channels are linked.
    /// When true, uses the maximum envelope across all channels.
    /// </summary>
    public bool StereoLink { get; set; }

    /// <summary>
    /// Gets or sets the hold time in milliseconds (0 to 1000).
    /// The envelope will hold at peak level for this duration before releasing.
    /// </summary>
    public float HoldTimeMs
    {
        get => _holdTimeMs;
        set
        {
            _holdTimeMs = Math.Clamp(value, 0f, 1000f);
            _holdSamples = (int)(_sampleRate * _holdTimeMs / 1000f);
        }
    }
    private float _holdTimeMs;

    /// <summary>
    /// Gets or sets the threshold level (0 to 1) for threshold crossing events.
    /// Set to 0 to disable threshold events.
    /// </summary>
    public float Threshold
    {
        get => _threshold;
        set => _threshold = Math.Clamp(value, 0f, 1f);
    }
    private float _threshold;

    /// <summary>
    /// Gets or sets how often envelope change events are fired in Hz (1 to 120).
    /// </summary>
    public float EventUpdateRateHz
    {
        get => _eventUpdateRateHz;
        set
        {
            _eventUpdateRateHz = Math.Clamp(value, 1f, 120f);
            _eventUpdateInterval = Math.Max(1, (int)(_sampleRate / _eventUpdateRateHz));
        }
    }
    private float _eventUpdateRateHz;

    /// <summary>
    /// Gets or sets the RMS window size in milliseconds (1 to 100).
    /// Only used when DetectionMode is RMS.
    /// </summary>
    public float RmsWindowMs
    {
        get => _rmsWindowMs;
        set
        {
            _rmsWindowMs = Math.Clamp(value, 1f, 100f);
            int newWindowSamples = Math.Max(1, (int)(_sampleRate * _rmsWindowMs / 1000f));
            if (newWindowSamples != _rmsWindowSamples)
            {
                _rmsWindowSamples = newWindowSamples;
                for (int ch = 0; ch < _channels; ch++)
                {
                    _rmsBuffers[ch] = new float[_rmsWindowSamples];
                    _rmsWritePos[ch] = 0;
                }
            }
        }
    }
    private float _rmsWindowMs = 10f;

    /// <summary>
    /// Gets the current envelope level (0 to 1) before output mapping.
    /// </summary>
    public float CurrentLevel { get; private set; }

    /// <summary>
    /// Gets the current envelope level mapped to the output range.
    /// </summary>
    public float CurrentMappedLevel { get; private set; }

    /// <summary>
    /// Gets the current envelope level in decibels.
    /// </summary>
    public float CurrentLevelDb => LinearToDb(CurrentLevel);

    /// <summary>
    /// Gets the peak envelope level since the last reset.
    /// </summary>
    public float PeakLevel => _peakLevelSinceReset;

    /// <summary>
    /// Gets the peak envelope level in decibels since the last reset.
    /// </summary>
    public float PeakLevelDb => LinearToDb(_peakLevelSinceReset);

    /// <summary>
    /// Gets whether the current level is above the threshold.
    /// </summary>
    public bool IsAboveThreshold => CurrentLevel > Threshold;

    /// <summary>
    /// Event fired when the envelope level changes (at EventUpdateRateHz).
    /// </summary>
    public event EventHandler<EnvelopeLevelChangedEventArgs>? LevelChanged;

    /// <summary>
    /// Event fired when the envelope crosses the threshold.
    /// </summary>
    public event EventHandler<EnvelopeThresholdEventArgs>? ThresholdCrossed;

    // Coefficient cache
    private float _attackCoeff;
    private float _releaseCoeff;

    /// <summary>
    /// Reads and processes audio samples, extracting the envelope.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = _source.Read(buffer, offset, count);
        if (samplesRead == 0)
            return 0;

        int frames = samplesRead / _channels;

        for (int frame = 0; frame < frames; frame++)
        {
            float maxEnvelope = 0f;

            for (int ch = 0; ch < _channels; ch++)
            {
                int sampleIndex = offset + frame * _channels + ch;
                float sample = buffer[sampleIndex];
                float inputLevel = DetectLevel(sample, ch);

                // Apply attack/release envelope following
                float targetEnvelope = inputLevel;
                float currentEnv = _envelopeState[ch];

                float coeff = targetEnvelope > currentEnv ? _attackCoeff : _releaseCoeff;
                _envelopeState[ch] = currentEnv + coeff * (targetEnvelope - currentEnv);

                if (_envelopeState[ch] > maxEnvelope)
                {
                    maxEnvelope = _envelopeState[ch];
                }
            }

            // Handle stereo linking
            float envelope;
            if (StereoLink || _channels == 1)
            {
                // Apply hold if enabled
                if (_holdSamples > 0)
                {
                    if (maxEnvelope >= _heldPeakLevel)
                    {
                        _heldPeakLevel = maxEnvelope;
                        _holdSamplesRemaining = _holdSamples;
                    }
                    else if (_holdSamplesRemaining > 0)
                    {
                        _holdSamplesRemaining--;
                        maxEnvelope = _heldPeakLevel;
                    }
                    else
                    {
                        // Release phase - decay the held level
                        _heldPeakLevel = maxEnvelope;
                    }
                }

                // Update linked envelope with smoothing
                float linkCoeff = maxEnvelope > _linkedEnvelope ? _attackCoeff : _releaseCoeff;
                _linkedEnvelope = _linkedEnvelope + linkCoeff * (maxEnvelope - _linkedEnvelope);
                envelope = _linkedEnvelope;
            }
            else
            {
                // Use first channel for non-linked operation
                envelope = _envelopeState[0];
            }

            // Apply sensitivity curve
            float shapedEnvelope = MathF.Pow(envelope, Sensitivity);

            // Update current level
            CurrentLevel = shapedEnvelope;

            // Track peak level
            if (shapedEnvelope > _peakLevelSinceReset)
            {
                _peakLevelSinceReset = shapedEnvelope;
            }

            // Map to output range
            CurrentMappedLevel = OutputMin + shapedEnvelope * (OutputMax - OutputMin);

            // Check threshold crossing
            bool currentlyAbove = shapedEnvelope > Threshold;
            if (Threshold > 0 && currentlyAbove != _wasAboveThreshold)
            {
                OnThresholdCrossed(currentlyAbove, shapedEnvelope, Threshold);
                _wasAboveThreshold = currentlyAbove;
            }

            // Fire level changed event at specified rate
            _eventUpdateCounter++;
            if (_eventUpdateCounter >= _eventUpdateInterval)
            {
                _eventUpdateCounter = 0;
                OnLevelChanged(shapedEnvelope, CurrentMappedLevel, _peakLevelSinceReset, CurrentLevelDb);
            }
        }

        return samplesRead;
    }

    /// <summary>
    /// Resets the envelope follower state.
    /// </summary>
    public void Reset()
    {
        for (int ch = 0; ch < _channels; ch++)
        {
            _envelopeState[ch] = 0f;
            Array.Clear(_rmsBuffers[ch], 0, _rmsBuffers[ch].Length);
            _rmsWritePos[ch] = 0;
            Array.Clear(_truePeakHistory[ch], 0, _truePeakHistory[ch].Length);
            _truePeakWritePos[ch] = 0;
        }

        _linkedEnvelope = 0f;
        _heldPeakLevel = 0f;
        _holdSamplesRemaining = 0;
        _peakLevelSinceReset = 0f;
        CurrentLevel = 0f;
        CurrentMappedLevel = OutputMin;
        _wasAboveThreshold = false;
        _eventUpdateCounter = 0;
    }

    /// <summary>
    /// Resets only the peak level tracking.
    /// </summary>
    public void ResetPeak()
    {
        _peakLevelSinceReset = 0f;
    }

    /// <summary>
    /// Gets the current envelope value that can be used to modulate other parameters.
    /// This is equivalent to CurrentMappedLevel.
    /// </summary>
    /// <returns>The current envelope value mapped to the output range.</returns>
    public float GetModulationValue()
    {
        return CurrentMappedLevel;
    }

    /// <summary>
    /// Gets the current envelope value inverted (1 - value) for ducking effects.
    /// </summary>
    /// <returns>The inverted envelope value mapped to the output range.</returns>
    public float GetDuckingValue()
    {
        // Invert within the output range
        return OutputMax - (CurrentMappedLevel - OutputMin);
    }

    /// <summary>
    /// Calculates what the output would be for a given input level without processing audio.
    /// Useful for visualizing the response curve.
    /// </summary>
    /// <param name="inputLevel">Input level (0 to 1).</param>
    /// <returns>Output level mapped to the output range.</returns>
    public float CalculateOutput(float inputLevel)
    {
        float shaped = MathF.Pow(Math.Clamp(inputLevel, 0f, 1f), Sensitivity);
        return OutputMin + shaped * (OutputMax - OutputMin);
    }

    /// <summary>
    /// Sets attack and release times for common use cases.
    /// </summary>
    /// <param name="preset">The preset name: "Fast", "Medium", "Slow", "Percussion", "Vocal", "Bass".</param>
    public void ApplyPreset(string preset)
    {
        switch (preset?.ToLowerInvariant())
        {
            case "fast":
                AttackMs = 1f;
                ReleaseMs = 50f;
                break;
            case "medium":
                AttackMs = 10f;
                ReleaseMs = 150f;
                break;
            case "slow":
                AttackMs = 50f;
                ReleaseMs = 500f;
                break;
            case "percussion":
                AttackMs = 0.5f;
                ReleaseMs = 100f;
                DetectionMode = EnvelopeDetectionMode.Peak;
                break;
            case "vocal":
                AttackMs = 10f;
                ReleaseMs = 200f;
                DetectionMode = EnvelopeDetectionMode.RMS;
                RmsWindowMs = 30f;
                break;
            case "bass":
                AttackMs = 20f;
                ReleaseMs = 300f;
                DetectionMode = EnvelopeDetectionMode.RMS;
                RmsWindowMs = 50f;
                break;
            default:
                // Default/unknown preset - use medium
                AttackMs = 10f;
                ReleaseMs = 150f;
                break;
        }
    }

    private float DetectLevel(float sample, int channel)
    {
        return DetectionMode switch
        {
            EnvelopeDetectionMode.Peak => MathF.Abs(sample),
            EnvelopeDetectionMode.RMS => CalculateRms(sample, channel),
            EnvelopeDetectionMode.TruePeak => CalculateTruePeak(sample, channel),
            _ => MathF.Abs(sample)
        };
    }

    private float CalculateRms(float sample, int channel)
    {
        // Update circular buffer
        float squared = sample * sample;
        _rmsBuffers[channel][_rmsWritePos[channel]] = squared;
        _rmsWritePos[channel] = (_rmsWritePos[channel] + 1) % _rmsWindowSamples;

        // Calculate RMS
        float sum = 0f;
        for (int i = 0; i < _rmsWindowSamples; i++)
        {
            sum += _rmsBuffers[channel][i];
        }

        return MathF.Sqrt(sum / _rmsWindowSamples);
    }

    private float CalculateTruePeak(float sample, int channel)
    {
        // Store sample in history
        int pos = _truePeakWritePos[channel];
        _truePeakHistory[channel][pos] = sample;
        _truePeakWritePos[channel] = (pos + 1) % 4;

        // Get the four most recent samples for interpolation
        float[] h = _truePeakHistory[channel];
        int p = _truePeakWritePos[channel];
        float s0 = h[(p + 0) % 4];
        float s1 = h[(p + 1) % 4];
        float s2 = h[(p + 2) % 4];
        float s3 = h[(p + 3) % 4];

        // Find max including interpolated values (4x oversampling via cubic interpolation)
        float maxPeak = MathF.Abs(sample);

        // Interpolate at 0.25, 0.5, 0.75 points between s1 and s2
        for (int i = 1; i <= 3; i++)
        {
            float t = i * 0.25f;
            float interpolated = CubicInterpolate(s0, s1, s2, s3, t);
            float absPeak = MathF.Abs(interpolated);
            if (absPeak > maxPeak)
            {
                maxPeak = absPeak;
            }
        }

        return maxPeak;
    }

    private static float CubicInterpolate(float y0, float y1, float y2, float y3, float t)
    {
        // Catmull-Rom spline interpolation
        float t2 = t * t;
        float t3 = t2 * t;

        float a0 = -0.5f * y0 + 1.5f * y1 - 1.5f * y2 + 0.5f * y3;
        float a1 = y0 - 2.5f * y1 + 2f * y2 - 0.5f * y3;
        float a2 = -0.5f * y0 + 0.5f * y2;
        float a3 = y1;

        return a0 * t3 + a1 * t2 + a2 * t + a3;
    }

    private void UpdateCoefficients()
    {
        // Calculate attack coefficient (how fast envelope rises)
        // Coefficient = exp(-1 / (time_in_samples))
        // time_in_samples = sample_rate * time_ms / 1000
        float attackSamples = _sampleRate * _attackMs / 1000f;
        _attackCoeff = attackSamples > 0 ? 1f - MathF.Exp(-1f / attackSamples) : 1f;

        // Calculate release coefficient (how fast envelope falls)
        float releaseSamples = _sampleRate * _releaseMs / 1000f;
        _releaseCoeff = releaseSamples > 0 ? 1f - MathF.Exp(-1f / releaseSamples) : 1f;
    }

    private static float LinearToDb(float linear)
    {
        return linear > 1e-10f ? 20f * MathF.Log10(linear) : -200f;
    }

    private void OnLevelChanged(float currentLevel, float mappedLevel, float peakLevel, float levelDb)
    {
        LevelChanged?.Invoke(this, new EnvelopeLevelChangedEventArgs(currentLevel, mappedLevel, peakLevel, levelDb));
    }

    private void OnThresholdCrossed(bool aboveThreshold, float currentLevel, float threshold)
    {
        ThresholdCrossed?.Invoke(this, new EnvelopeThresholdEventArgs(aboveThreshold, currentLevel, threshold));
    }

    /// <summary>
    /// Creates an envelope follower configured for sidechain ducking.
    /// </summary>
    /// <param name="source">Audio source to follow.</param>
    /// <returns>Configured EnvelopeFollower.</returns>
    public static EnvelopeFollower CreateForDucking(ISampleProvider source)
    {
        var follower = new EnvelopeFollower(source)
        {
            AttackMs = 5f,
            ReleaseMs = 100f,
            DetectionMode = EnvelopeDetectionMode.Peak,
            Sensitivity = 1.5f,
            StereoLink = true
        };
        return follower;
    }

    /// <summary>
    /// Creates an envelope follower configured for gate triggering.
    /// </summary>
    /// <param name="source">Audio source to follow.</param>
    /// <param name="threshold">Threshold for gate triggering (0-1).</param>
    /// <returns>Configured EnvelopeFollower.</returns>
    public static EnvelopeFollower CreateForGate(ISampleProvider source, float threshold = 0.1f)
    {
        var follower = new EnvelopeFollower(source)
        {
            AttackMs = 1f,
            ReleaseMs = 50f,
            HoldTimeMs = 20f,
            DetectionMode = EnvelopeDetectionMode.Peak,
            Threshold = threshold,
            StereoLink = true
        };
        return follower;
    }

    /// <summary>
    /// Creates an envelope follower configured for modulation (LFO-like but audio-driven).
    /// </summary>
    /// <param name="source">Audio source to follow.</param>
    /// <returns>Configured EnvelopeFollower.</returns>
    public static EnvelopeFollower CreateForModulation(ISampleProvider source)
    {
        var follower = new EnvelopeFollower(source)
        {
            AttackMs = 10f,
            ReleaseMs = 200f,
            DetectionMode = EnvelopeDetectionMode.RMS,
            RmsWindowMs = 20f,
            Sensitivity = 0.8f,
            StereoLink = true
        };
        return follower;
    }

    /// <summary>
    /// Creates an envelope follower configured for metering display.
    /// </summary>
    /// <param name="source">Audio source to follow.</param>
    /// <returns>Configured EnvelopeFollower.</returns>
    public static EnvelopeFollower CreateForMetering(ISampleProvider source)
    {
        var follower = new EnvelopeFollower(source)
        {
            AttackMs = 0.1f,
            ReleaseMs = 1500f,
            HoldTimeMs = 500f,
            DetectionMode = EnvelopeDetectionMode.TruePeak,
            EventUpdateRateHz = 30f,
            StereoLink = false
        };
        return follower;
    }
}
