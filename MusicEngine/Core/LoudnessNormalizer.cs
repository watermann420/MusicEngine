// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using System;
using NAudio.Wave;

namespace MusicEngine.Core;

/// <summary>
/// Sample provider that applies gain to reach target LUFS with true peak limiting.
/// Implements a two-pass approach: first measures loudness, then applies normalization.
/// For real-time use, use the single-pass mode with pre-measured loudness.
/// </summary>
public class LoudnessNormalizer : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly int _sampleRate;
    private readonly int _channels;

    // Normalization settings
    private float _targetLufs;
    private float _maxTruePeak;
    private float _gain;
    private bool _gainCalculated;

    // True peak limiter state
    private readonly float[] _lookaheadBuffer;
    private readonly float[] _gainReduction;
    private int _lookaheadWritePos;
    private readonly int _lookaheadSamples;
    private float _limiterEnvelope;

    // Source loudness (must be set before processing for single-pass mode)
    private double _sourceLoudness = double.NegativeInfinity;
    private double _sourceTruePeak = double.NegativeInfinity;

    /// <summary>
    /// Gets the wave format of the audio stream.
    /// </summary>
    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>
    /// Gets or sets the target integrated loudness in LUFS.
    /// </summary>
    public float TargetLufs
    {
        get => _targetLufs;
        set
        {
            _targetLufs = Math.Clamp(value, -70f, 0f);
            _gainCalculated = false;
        }
    }

    /// <summary>
    /// Gets or sets the maximum true peak level in dBTP.
    /// </summary>
    public float MaxTruePeak
    {
        get => _maxTruePeak;
        set => _maxTruePeak = Math.Clamp(value, -20f, 0f);
    }

    /// <summary>
    /// Gets the calculated gain in dB that will be applied.
    /// </summary>
    public float GainDb => 20f * MathF.Log10(Math.Max(_gain, 1e-10f));

    /// <summary>
    /// Gets the calculated linear gain.
    /// </summary>
    public float GainLinear => _gain;

    /// <summary>
    /// Gets or sets the source integrated loudness in LUFS.
    /// Set this before processing if using single-pass mode.
    /// </summary>
    public double SourceLoudness
    {
        get => _sourceLoudness;
        set
        {
            _sourceLoudness = value;
            _gainCalculated = false;
        }
    }

    /// <summary>
    /// Gets or sets the source true peak in dBTP.
    /// Set this to ensure the output doesn't exceed the true peak limit.
    /// </summary>
    public double SourceTruePeak
    {
        get => _sourceTruePeak;
        set => _sourceTruePeak = value;
    }

    /// <summary>
    /// Creates a loudness normalizer wrapping the specified audio source.
    /// </summary>
    /// <param name="source">The audio source to normalize.</param>
    /// <param name="targetLufs">Target integrated loudness in LUFS (default: -14 LUFS).</param>
    /// <param name="maxTruePeak">Maximum true peak in dBTP (default: -1 dBTP).</param>
    public LoudnessNormalizer(ISampleProvider source, float targetLufs = -14f, float maxTruePeak = -1f)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _sampleRate = source.WaveFormat.SampleRate;
        _channels = source.WaveFormat.Channels;

        _targetLufs = Math.Clamp(targetLufs, -70f, 0f);
        _maxTruePeak = Math.Clamp(maxTruePeak, -20f, 0f);
        _gain = 1f;

        // Lookahead limiter (5ms lookahead for smooth limiting)
        _lookaheadSamples = (int)(_sampleRate * 0.005);
        _lookaheadBuffer = new float[_lookaheadSamples * _channels];
        _gainReduction = new float[_lookaheadSamples];

        // Initialize gain reduction buffer
        for (int i = 0; i < _lookaheadSamples; i++)
        {
            _gainReduction[i] = 1f;
        }
    }

    /// <summary>
    /// Calculates the gain needed to reach the target loudness.
    /// Call this after setting SourceLoudness if known.
    /// </summary>
    public void CalculateGain()
    {
        if (double.IsNegativeInfinity(_sourceLoudness) || double.IsNaN(_sourceLoudness))
        {
            _gain = 1f;
            _gainCalculated = true;
            return;
        }

        // Calculate required gain in dB
        double gainDb = _targetLufs - _sourceLoudness;

        // Convert to linear gain
        _gain = (float)Math.Pow(10.0, gainDb / 20.0);

        // Check if applying this gain would exceed true peak limit
        if (!double.IsNegativeInfinity(_sourceTruePeak))
        {
            // Calculate what the new peak would be
            double newPeakDb = _sourceTruePeak + gainDb;

            // If it exceeds the limit, reduce gain accordingly
            if (newPeakDb > _maxTruePeak)
            {
                double adjustmentDb = _maxTruePeak - newPeakDb;
                _gain *= (float)Math.Pow(10.0, adjustmentDb / 20.0);
            }
        }

        _gainCalculated = true;
    }

    /// <summary>
    /// Reads audio samples and applies loudness normalization with true peak limiting.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        // Calculate gain if not already done
        if (!_gainCalculated)
        {
            CalculateGain();
        }

        int samplesRead = _source.Read(buffer, offset, count);
        if (samplesRead == 0) return 0;

        // Apply gain and true peak limiting
        ProcessBuffer(buffer, offset, samplesRead);

        return samplesRead;
    }

    private void ProcessBuffer(float[] buffer, int offset, int count)
    {
        float ceiling = MathF.Pow(10f, _maxTruePeak / 20f);
        float releaseCoeff = MathF.Exp(-1f / (_sampleRate * 0.05f)); // 50ms release

        int frames = count / _channels;

        for (int frame = 0; frame < frames; frame++)
        {
            // Find peak across all channels for this frame after gain
            float framePeak = 0;
            for (int ch = 0; ch < _channels; ch++)
            {
                int idx = offset + frame * _channels + ch;
                float amplified = buffer[idx] * _gain;
                float absSample = MathF.Abs(amplified);
                if (absSample > framePeak)
                {
                    framePeak = absSample;
                }
            }

            // Calculate required gain reduction for this frame
            float targetGainReduction = 1f;
            if (framePeak > ceiling)
            {
                targetGainReduction = ceiling / framePeak;
            }

            // Smooth envelope (instant attack, slow release)
            if (targetGainReduction < _limiterEnvelope)
            {
                _limiterEnvelope = targetGainReduction;
            }
            else
            {
                _limiterEnvelope = targetGainReduction + releaseCoeff * (_limiterEnvelope - targetGainReduction);
            }

            // Store gain reduction in lookahead buffer
            _gainReduction[_lookaheadWritePos] = _limiterEnvelope;

            // Get the delayed gain reduction (from lookahead samples ago)
            int readPos = (_lookaheadWritePos - _lookaheadSamples + _gainReduction.Length) % _gainReduction.Length;
            float delayedGainReduction = _gainReduction[readPos];

            // Apply gain and delayed limiting to each channel
            for (int ch = 0; ch < _channels; ch++)
            {
                int bufferIdx = offset + frame * _channels + ch;
                int lookaheadIdx = _lookaheadWritePos * _channels + ch;

                // Read from lookahead buffer (delayed signal)
                float delayedSample = _lookaheadBuffer[lookaheadIdx];

                // Write current amplified sample to lookahead buffer
                _lookaheadBuffer[lookaheadIdx] = buffer[bufferIdx] * _gain;

                // Output: delayed sample with delayed gain reduction
                buffer[bufferIdx] = delayedSample * delayedGainReduction;
            }

            _lookaheadWritePos = (_lookaheadWritePos + 1) % _lookaheadSamples;
        }
    }

    /// <summary>
    /// Resets the limiter state (call when seeking or starting new audio).
    /// </summary>
    public void Reset()
    {
        Array.Clear(_lookaheadBuffer, 0, _lookaheadBuffer.Length);
        for (int i = 0; i < _lookaheadSamples; i++)
        {
            _gainReduction[i] = 1f;
        }
        _lookaheadWritePos = 0;
        _limiterEnvelope = 1f;
    }

    /// <summary>
    /// Creates a loudness normalizer with pre-measured loudness values.
    /// </summary>
    /// <param name="source">The audio source to normalize.</param>
    /// <param name="sourceLoudness">Measured integrated loudness in LUFS.</param>
    /// <param name="sourceTruePeak">Measured true peak in dBTP.</param>
    /// <param name="targetLufs">Target integrated loudness in LUFS.</param>
    /// <param name="maxTruePeak">Maximum true peak in dBTP.</param>
    /// <returns>A configured LoudnessNormalizer ready for processing.</returns>
    public static LoudnessNormalizer CreateWithMeasuredLoudness(
        ISampleProvider source,
        double sourceLoudness,
        double sourceTruePeak,
        float targetLufs = -14f,
        float maxTruePeak = -1f)
    {
        var normalizer = new LoudnessNormalizer(source, targetLufs, maxTruePeak)
        {
            SourceLoudness = sourceLoudness,
            SourceTruePeak = sourceTruePeak
        };
        normalizer.CalculateGain();
        return normalizer;
    }
}

/// <summary>
/// Utility class for measuring loudness of an entire audio file.
/// </summary>
public static class LoudnessAnalyzer
{
    /// <summary>
    /// Measures the integrated loudness and true peak of an audio source.
    /// This reads through the entire source.
    /// </summary>
    /// <param name="source">The audio source to measure.</param>
    /// <param name="progress">Optional progress callback (0.0 to 1.0).</param>
    /// <returns>Loudness measurement results.</returns>
    public static LoudnessMeasurement Measure(ISampleProvider source, Action<double>? progress = null)
    {
        var meter = new LoudnessMeter(source);
        float[] buffer = new float[4096];
        int totalSamples = 0;
        int samplesRead;

        // Estimate total samples if possible (for progress reporting)
        long estimatedTotal = 0;
        if (source is IWaveProvider waveProvider)
        {
            // Rough estimate based on buffer size
            estimatedTotal = 10_000_000; // Default estimate
        }

        while ((samplesRead = meter.Read(buffer, 0, buffer.Length)) > 0)
        {
            totalSamples += samplesRead;

            if (progress != null && estimatedTotal > 0)
            {
                progress(Math.Min(1.0, (double)totalSamples / estimatedTotal));
            }
        }

        progress?.Invoke(1.0);

        return new LoudnessMeasurement(
            IntegratedLoudness: meter.IntegratedLoudness,
            ShortTermLoudness: meter.ShortTermLoudness,
            MomentaryLoudness: meter.MomentaryLoudness,
            TruePeak: meter.TruePeak,
            TruePeakLinear: meter.TruePeakLinear);
    }

    /// <summary>
    /// Measures loudness from a WAV file.
    /// </summary>
    /// <param name="filePath">Path to the WAV file.</param>
    /// <param name="progress">Optional progress callback.</param>
    /// <returns>Loudness measurement results.</returns>
    public static LoudnessMeasurement MeasureFile(string filePath, Action<double>? progress = null)
    {
        using var reader = new NAudio.Wave.AudioFileReader(filePath);
        return Measure(reader, progress);
    }
}

/// <summary>
/// Results of a loudness measurement.
/// </summary>
/// <param name="IntegratedLoudness">Integrated loudness in LUFS.</param>
/// <param name="ShortTermLoudness">Short-term loudness in LUFS (last 3 seconds).</param>
/// <param name="MomentaryLoudness">Momentary loudness in LUFS (last 400ms).</param>
/// <param name="TruePeak">Maximum true peak in dBTP.</param>
/// <param name="TruePeakLinear">Maximum true peak in linear scale.</param>
public record LoudnessMeasurement(
    double IntegratedLoudness,
    double ShortTermLoudness,
    double MomentaryLoudness,
    double TruePeak,
    double TruePeakLinear)
{
    /// <summary>
    /// Calculates the gain in dB needed to reach a target loudness.
    /// </summary>
    /// <param name="targetLufs">Target integrated loudness in LUFS.</param>
    /// <returns>Gain in dB.</returns>
    public double GetGainToTarget(double targetLufs)
    {
        if (double.IsNegativeInfinity(IntegratedLoudness))
        {
            return 0;
        }
        return targetLufs - IntegratedLoudness;
    }

    /// <summary>
    /// Checks if the audio is within streaming platform loudness range.
    /// </summary>
    /// <param name="targetLufs">Target loudness (typically -14 LUFS).</param>
    /// <param name="tolerance">Tolerance in dB (typically 1.0).</param>
    /// <returns>True if within range.</returns>
    public bool IsWithinRange(double targetLufs, double tolerance = 1.0)
    {
        if (double.IsNegativeInfinity(IntegratedLoudness))
        {
            return false;
        }
        return Math.Abs(IntegratedLoudness - targetLufs) <= tolerance;
    }

    /// <summary>
    /// Gets a human-readable summary of the measurement.
    /// </summary>
    public override string ToString()
    {
        return $"Integrated: {IntegratedLoudness:F1} LUFS, True Peak: {TruePeak:F1} dBTP";
    }
}
