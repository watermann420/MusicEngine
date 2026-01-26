//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Audio quantization that snaps transients to a musical grid with time-stretching.

using System;
using System.Collections.Generic;

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Grid resolution for audio quantization.
/// </summary>
public enum QuantizeGrid
{
    /// <summary>Quarter notes (1/4)</summary>
    Quarter = 4,
    /// <summary>Eighth notes (1/8)</summary>
    Eighth = 8,
    /// <summary>Sixteenth notes (1/16)</summary>
    Sixteenth = 16,
    /// <summary>Thirty-second notes (1/32)</summary>
    ThirtySecond = 32,
    /// <summary>Eighth note triplets</summary>
    EighthTriplet = 12,
    /// <summary>Sixteenth note triplets</summary>
    SixteenthTriplet = 24
}

/// <summary>
/// Represents a detected transient with its original and quantized positions.
/// </summary>
public class QuantizedTransient
{
    /// <summary>Original position in samples.</summary>
    public long OriginalPositionSamples { get; set; }

    /// <summary>Quantized (snapped) position in samples.</summary>
    public long QuantizedPositionSamples { get; set; }

    /// <summary>Original position in seconds.</summary>
    public double OriginalTimeSeconds { get; set; }

    /// <summary>Quantized position in seconds.</summary>
    public double QuantizedTimeSeconds { get; set; }

    /// <summary>Offset applied in samples (positive = moved later, negative = moved earlier).</summary>
    public long OffsetSamples => QuantizedPositionSamples - OriginalPositionSamples;

    /// <summary>Transient strength (0.0 to 1.0).</summary>
    public float Strength { get; set; }

    /// <summary>Whether this transient was adjusted.</summary>
    public bool WasAdjusted => OffsetSamples != 0;
}

/// <summary>
/// Result of audio quantization analysis.
/// </summary>
public class AudioQuantizeResult
{
    /// <summary>List of detected and quantized transients.</summary>
    public List<QuantizedTransient> Transients { get; set; } = new();

    /// <summary>The grid resolution used.</summary>
    public QuantizeGrid Grid { get; set; }

    /// <summary>The BPM used for quantization.</summary>
    public double Bpm { get; set; }

    /// <summary>The sample rate of the audio.</summary>
    public int SampleRate { get; set; }

    /// <summary>The quantization strength applied (0-100%).</summary>
    public float Strength { get; set; }

    /// <summary>Number of transients that were adjusted.</summary>
    public int AdjustedCount => Transients.FindAll(t => t.WasAdjusted).Count;

    /// <summary>Total number of detected transients.</summary>
    public int TotalCount => Transients.Count;
}

/// <summary>
/// Audio quantizer that detects transients and snaps them to a musical grid.
/// Supports time-stretching to preserve audio between transients.
/// </summary>
public class AudioQuantize
{
    private readonly int _sampleRate;
    private readonly TransientDetector _transientDetector;

    /// <summary>
    /// Gets or sets the tempo in BPM for grid calculation.
    /// </summary>
    public double Bpm { get; set; } = 120.0;

    /// <summary>
    /// Gets or sets the grid resolution for quantization.
    /// </summary>
    public QuantizeGrid Grid { get; set; } = QuantizeGrid.Sixteenth;

    /// <summary>
    /// Gets or sets the quantization strength (0-100%).
    /// 0% = no movement, 100% = snap exactly to grid.
    /// </summary>
    public float Strength { get; set; } = 100f;

    /// <summary>
    /// Gets or sets the transient detection threshold (0.0 to 1.0).
    /// Higher values require stronger transients.
    /// </summary>
    public float Threshold
    {
        get => _transientDetector.Threshold;
        set => _transientDetector.Threshold = value;
    }

    /// <summary>
    /// Gets or sets the transient detection sensitivity (0.1 to 10.0).
    /// </summary>
    public float Sensitivity
    {
        get => _transientDetector.Sensitivity;
        set => _transientDetector.Sensitivity = value;
    }

    /// <summary>
    /// Gets or sets whether to preserve timing of audio between transients
    /// using time-stretching (true) or simply move audio slices (false).
    /// </summary>
    public bool PreserveAudioBetweenTransients { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum shift allowed in grid units.
    /// Transients farther from the grid than this won't be moved.
    /// </summary>
    public float MaxShiftGridUnits { get; set; } = 0.5f;

    /// <summary>
    /// Creates a new audio quantizer.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate in Hz.</param>
    public AudioQuantize(int sampleRate = 44100)
    {
        _sampleRate = sampleRate;
        _transientDetector = new TransientDetector(sampleRate);
        _transientDetector.Threshold = 0.4f;
        _transientDetector.Sensitivity = 1.5f;
        _transientDetector.MinimumIntervalMs = 30;
    }

    /// <summary>
    /// Analyzes audio and returns quantization data without modifying the audio.
    /// </summary>
    /// <param name="samples">Audio samples (mono).</param>
    /// <returns>Analysis result with transient positions and quantization data.</returns>
    public AudioQuantizeResult Analyze(float[] samples)
    {
        var result = new AudioQuantizeResult
        {
            Grid = Grid,
            Bpm = Bpm,
            SampleRate = _sampleRate,
            Strength = Strength
        };

        // Detect transients
        var transients = _transientDetector.AnalyzeBuffer(samples, _sampleRate);

        // Calculate grid interval in samples
        double beatsPerSecond = Bpm / 60.0;
        double gridBeats = 1.0 / (int)Grid; // e.g., 1/16 for sixteenth notes
        double gridSeconds = gridBeats / beatsPerSecond;
        long gridSamples = (long)(gridSeconds * _sampleRate);

        float strengthFactor = Strength / 100f;
        float maxShiftSamples = gridSamples * MaxShiftGridUnits;

        foreach (var transient in transients)
        {
            long originalSamples = (long)(transient.TimeSeconds * _sampleRate);

            // Find nearest grid position
            long nearestGrid = (long)Math.Round((double)originalSamples / gridSamples) * gridSamples;

            // Calculate offset
            long offset = nearestGrid - originalSamples;

            // Check if within max shift range
            if (Math.Abs(offset) > maxShiftSamples)
            {
                // Don't move this transient
                offset = 0;
                nearestGrid = originalSamples;
            }

            // Apply strength factor
            long adjustedOffset = (long)(offset * strengthFactor);
            long quantizedSamples = originalSamples + adjustedOffset;

            var quantized = new QuantizedTransient
            {
                OriginalPositionSamples = originalSamples,
                QuantizedPositionSamples = quantizedSamples,
                OriginalTimeSeconds = transient.TimeSeconds,
                QuantizedTimeSeconds = (double)quantizedSamples / _sampleRate,
                Strength = transient.Strength
            };

            result.Transients.Add(quantized);
        }

        return result;
    }

    /// <summary>
    /// Quantizes audio by moving transients to the grid.
    /// Returns a new audio buffer with quantized timing.
    /// </summary>
    /// <param name="samples">Input audio samples (mono).</param>
    /// <returns>Quantized audio samples.</returns>
    public float[] Process(float[] samples)
    {
        var analysis = Analyze(samples);

        if (analysis.Transients.Count == 0)
        {
            // No transients detected, return copy of original
            var copy = new float[samples.Length];
            Array.Copy(samples, copy, samples.Length);
            return copy;
        }

        if (PreserveAudioBetweenTransients)
        {
            return ProcessWithTimeStretch(samples, analysis);
        }
        else
        {
            return ProcessWithSlicing(samples, analysis);
        }
    }

    /// <summary>
    /// Processes audio by time-stretching regions between transients.
    /// </summary>
    private float[] ProcessWithTimeStretch(float[] samples, AudioQuantizeResult analysis)
    {
        var output = new float[samples.Length];
        var transients = analysis.Transients;

        // Add virtual transients at start and end
        var allPoints = new List<(long original, long quantized)>
        {
            (0, 0)
        };

        foreach (var t in transients)
        {
            allPoints.Add((t.OriginalPositionSamples, t.QuantizedPositionSamples));
        }

        allPoints.Add((samples.Length, samples.Length));

        // Process each region between transients
        for (int i = 0; i < allPoints.Count - 1; i++)
        {
            long srcStart = allPoints[i].original;
            long srcEnd = allPoints[i + 1].original;
            long dstStart = allPoints[i].quantized;
            long dstEnd = allPoints[i + 1].quantized;

            long srcLength = srcEnd - srcStart;
            long dstLength = dstEnd - dstStart;

            if (srcLength <= 0 || dstLength <= 0)
                continue;

            // Time-stretch this region
            double stretchRatio = (double)dstLength / srcLength;

            for (long dstPos = 0; dstPos < dstLength; dstPos++)
            {
                long outputPos = dstStart + dstPos;
                if (outputPos < 0 || outputPos >= output.Length)
                    continue;

                // Calculate source position with linear interpolation
                double srcPos = srcStart + (dstPos / stretchRatio);
                int srcIndex = (int)srcPos;
                float frac = (float)(srcPos - srcIndex);

                // Linear interpolation
                float sample = 0;
                if (srcIndex >= 0 && srcIndex < samples.Length)
                {
                    sample = samples[srcIndex];
                    if (srcIndex + 1 < samples.Length && frac > 0)
                    {
                        sample = sample * (1f - frac) + samples[srcIndex + 1] * frac;
                    }
                }

                output[outputPos] = sample;
            }
        }

        return output;
    }

    /// <summary>
    /// Processes audio by simply moving slices (no time-stretch).
    /// May cause discontinuities.
    /// </summary>
    private float[] ProcessWithSlicing(float[] samples, AudioQuantizeResult analysis)
    {
        var output = new float[samples.Length];
        var transients = analysis.Transients;

        // Add virtual transient at start
        var sliceStarts = new List<(long original, long quantized)>
        {
            (0, 0)
        };

        foreach (var t in transients)
        {
            sliceStarts.Add((t.OriginalPositionSamples, t.QuantizedPositionSamples));
        }

        // Process each slice
        for (int i = 0; i < sliceStarts.Count; i++)
        {
            long srcStart = sliceStarts[i].original;
            long dstStart = sliceStarts[i].quantized;

            // Determine slice end
            long srcEnd = i < sliceStarts.Count - 1
                ? sliceStarts[i + 1].original
                : samples.Length;

            long sliceLength = srcEnd - srcStart;

            // Copy slice to output
            for (long j = 0; j < sliceLength; j++)
            {
                long srcPos = srcStart + j;
                long dstPos = dstStart + j;

                if (srcPos >= 0 && srcPos < samples.Length &&
                    dstPos >= 0 && dstPos < output.Length)
                {
                    output[dstPos] = samples[srcPos];
                }
            }

            // Apply short crossfade at slice boundaries to reduce clicks
            if (dstStart > 0 && dstStart < output.Length)
            {
                int fadeLength = Math.Min(64, (int)(sliceLength / 4));
                for (int f = 0; f < fadeLength; f++)
                {
                    if (dstStart + f < output.Length)
                    {
                        float fade = (float)f / fadeLength;
                        output[dstStart + f] *= fade;
                    }
                }
            }
        }

        return output;
    }

    /// <summary>
    /// Gets the grid interval in samples for the current BPM and grid setting.
    /// </summary>
    public long GetGridIntervalSamples()
    {
        double beatsPerSecond = Bpm / 60.0;
        double gridBeats = 1.0 / (int)Grid;
        double gridSeconds = gridBeats / beatsPerSecond;
        return (long)(gridSeconds * _sampleRate);
    }

    /// <summary>
    /// Gets the grid interval in seconds for the current BPM and grid setting.
    /// </summary>
    public double GetGridIntervalSeconds()
    {
        double beatsPerSecond = Bpm / 60.0;
        double gridBeats = 1.0 / (int)Grid;
        return gridBeats / beatsPerSecond;
    }

    /// <summary>
    /// Creates a quantizer preset for drums.
    /// </summary>
    public static AudioQuantize CreateDrumPreset(int sampleRate = 44100)
    {
        return new AudioQuantize(sampleRate)
        {
            Grid = QuantizeGrid.Sixteenth,
            Strength = 75f,
            Threshold = 0.3f,
            Sensitivity = 2f,
            PreserveAudioBetweenTransients = true,
            MaxShiftGridUnits = 0.25f
        };
    }

    /// <summary>
    /// Creates a quantizer preset for bass.
    /// </summary>
    public static AudioQuantize CreateBassPreset(int sampleRate = 44100)
    {
        return new AudioQuantize(sampleRate)
        {
            Grid = QuantizeGrid.Eighth,
            Strength = 50f,
            Threshold = 0.4f,
            Sensitivity = 1.5f,
            PreserveAudioBetweenTransients = true,
            MaxShiftGridUnits = 0.3f
        };
    }

    /// <summary>
    /// Creates a quantizer preset for percussion loops.
    /// </summary>
    public static AudioQuantize CreatePercussionPreset(int sampleRate = 44100)
    {
        return new AudioQuantize(sampleRate)
        {
            Grid = QuantizeGrid.Sixteenth,
            Strength = 100f,
            Threshold = 0.35f,
            Sensitivity = 1.8f,
            PreserveAudioBetweenTransients = true,
            MaxShiftGridUnits = 0.5f
        };
    }
}
