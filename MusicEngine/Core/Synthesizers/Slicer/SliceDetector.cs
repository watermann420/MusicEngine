//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Slice detection algorithms for REX-style beat slicing.

namespace MusicEngine.Core.Synthesizers.Slicer;

/// <summary>
/// Slicing mode for automatic slice detection.
/// </summary>
public enum SliceMode
{
    /// <summary>Detect slices at transient/attack points.</summary>
    Transient,
    /// <summary>Slice at beat divisions based on BPM.</summary>
    Beat,
    /// <summary>Manual slice placement (no auto-detection).</summary>
    Manual,
    /// <summary>Divide audio into equal-length slices.</summary>
    Equal
}

/// <summary>
/// Detects and creates slices from audio data using various algorithms.
/// Supports transient detection, beat-based slicing, and equal division.
/// </summary>
public class SliceDetector
{
    /// <summary>
    /// Threshold for transient detection (0.0 to 1.0).
    /// Higher values require stronger transients for slice points.
    /// </summary>
    public float TransientThreshold { get; set; } = 0.3f;

    /// <summary>
    /// Minimum number of samples between slices.
    /// Prevents creating very short slices.
    /// </summary>
    public int MinSliceSamples { get; set; } = 1000;

    /// <summary>
    /// Sensitivity multiplier for transient detection (0.1 to 10.0).
    /// Higher values detect weaker transients.
    /// </summary>
    public float Sensitivity { get; set; } = 1.5f;

    /// <summary>
    /// Frame size for analysis (in samples).
    /// </summary>
    public int FrameSize { get; set; } = 1024;

    /// <summary>
    /// Hop size for analysis (in samples).
    /// </summary>
    public int HopSize { get; set; } = 512;

    /// <summary>
    /// Detects slices in audio data using the specified mode.
    /// </summary>
    /// <param name="audioData">Mono audio samples.</param>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="mode">Slice detection mode.</param>
    /// <param name="bpm">BPM for beat-based slicing (optional).</param>
    /// <param name="beatsPerSlice">Beats per slice for beat mode (default: 1).</param>
    /// <param name="sliceCount">Number of slices for equal mode (default: 16).</param>
    /// <returns>List of detected slices.</returns>
    public List<Slice> DetectSlices(float[] audioData, int sampleRate, SliceMode mode,
        double bpm = 120, int beatsPerSlice = 1, int sliceCount = 16)
    {
        return mode switch
        {
            SliceMode.Transient => DetectTransients(audioData, sampleRate),
            SliceMode.Beat => SliceByBeats(audioData, sampleRate, bpm, beatsPerSlice),
            SliceMode.Equal => SliceEqual(audioData, sliceCount),
            SliceMode.Manual => new List<Slice>(),
            _ => new List<Slice>()
        };
    }

    /// <summary>
    /// Detects slices at transient/attack points using energy-based onset detection.
    /// </summary>
    /// <param name="audioData">Mono audio samples.</param>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <returns>List of slices starting at each detected transient.</returns>
    public List<Slice> DetectTransients(float[] audioData, int sampleRate)
    {
        var slices = new List<Slice>();
        var transientPositions = new List<long>();

        // Always start at sample 0
        transientPositions.Add(0);

        // Calculate frame energies
        int numFrames = (audioData.Length - FrameSize) / HopSize + 1;
        if (numFrames <= 0)
        {
            // Audio too short for analysis, return single slice
            slices.Add(new Slice(0, 0, audioData.Length));
            return slices;
        }

        var energies = new float[numFrames];
        var spectralFlux = new float[numFrames];

        // Calculate RMS energy for each frame
        for (int frame = 0; frame < numFrames; frame++)
        {
            int startSample = frame * HopSize;
            float energy = 0;

            for (int i = 0; i < FrameSize && startSample + i < audioData.Length; i++)
            {
                float sample = audioData[startSample + i];
                energy += sample * sample;
            }

            energies[frame] = (float)Math.Sqrt(energy / FrameSize);
        }

        // Calculate spectral flux (energy difference)
        for (int frame = 1; frame < numFrames; frame++)
        {
            spectralFlux[frame] = Math.Max(0, energies[frame] - energies[frame - 1]);
        }

        // Calculate adaptive threshold using local statistics
        const int historySize = 43; // ~1 second at 44100Hz with 512 hop
        var energyHistory = new float[historySize];
        int historyPos = 0;

        for (int frame = 1; frame < numFrames; frame++)
        {
            // Update history
            energyHistory[historyPos] = energies[frame];
            historyPos = (historyPos + 1) % historySize;

            // Calculate local average and standard deviation
            float avgEnergy = 0;
            for (int i = 0; i < historySize; i++)
            {
                avgEnergy += energyHistory[i];
            }
            avgEnergy /= historySize;

            float variance = 0;
            for (int i = 0; i < historySize; i++)
            {
                float diff = energyHistory[i] - avgEnergy;
                variance += diff * diff;
            }
            float stdDev = (float)Math.Sqrt(variance / historySize);

            // Adaptive threshold
            float threshold = avgEnergy + (TransientThreshold * 2 + 0.5f) * Math.Max(stdDev, 0.001f);
            threshold /= Sensitivity;

            // Detect transient
            if (spectralFlux[frame] > threshold && energies[frame] > 0.001f)
            {
                long samplePos = (long)frame * HopSize;

                // Check minimum distance from last transient
                if (transientPositions.Count > 0)
                {
                    long lastPos = transientPositions[transientPositions.Count - 1];
                    if (samplePos - lastPos >= MinSliceSamples)
                    {
                        transientPositions.Add(samplePos);
                    }
                }
            }
        }

        // Create slices from transient positions
        for (int i = 0; i < transientPositions.Count; i++)
        {
            long start = transientPositions[i];
            long end = (i < transientPositions.Count - 1)
                ? transientPositions[i + 1]
                : audioData.Length;

            slices.Add(new Slice(i, start, end));
        }

        return slices;
    }

    /// <summary>
    /// Creates slices at beat divisions based on BPM.
    /// </summary>
    /// <param name="audioData">Mono audio samples.</param>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="bpm">Tempo in beats per minute.</param>
    /// <param name="beatsPerSlice">Number of beats per slice (e.g., 1 = quarter notes, 0.5 = eighth notes).</param>
    /// <returns>List of slices at beat boundaries.</returns>
    public List<Slice> SliceByBeats(float[] audioData, int sampleRate, double bpm, int beatsPerSlice = 1)
    {
        var slices = new List<Slice>();

        if (bpm <= 0 || beatsPerSlice <= 0)
        {
            slices.Add(new Slice(0, 0, audioData.Length));
            return slices;
        }

        // Calculate samples per beat
        double secondsPerBeat = 60.0 / bpm;
        double samplesPerBeat = secondsPerBeat * sampleRate;
        double samplesPerSlice = samplesPerBeat * beatsPerSlice;

        // Create slices at beat boundaries
        long currentPos = 0;
        int sliceIndex = 0;

        while (currentPos < audioData.Length)
        {
            long endPos = (long)(currentPos + samplesPerSlice);
            if (endPos > audioData.Length)
            {
                endPos = audioData.Length;
            }

            slices.Add(new Slice(sliceIndex, currentPos, endPos));
            sliceIndex++;
            currentPos = endPos;
        }

        return slices;
    }

    /// <summary>
    /// Divides audio into equal-length slices.
    /// </summary>
    /// <param name="audioData">Mono audio samples.</param>
    /// <param name="sliceCount">Number of slices to create.</param>
    /// <returns>List of equal-length slices.</returns>
    public List<Slice> SliceEqual(float[] audioData, int sliceCount)
    {
        var slices = new List<Slice>();

        if (sliceCount <= 0)
        {
            sliceCount = 1;
        }

        long samplesPerSlice = audioData.Length / sliceCount;
        if (samplesPerSlice < 1)
        {
            samplesPerSlice = 1;
        }

        for (int i = 0; i < sliceCount; i++)
        {
            long start = i * samplesPerSlice;
            long end = (i < sliceCount - 1) ? (i + 1) * samplesPerSlice : audioData.Length;

            slices.Add(new Slice(i, start, end));
        }

        return slices;
    }

    /// <summary>
    /// Refines slice positions by snapping to zero crossings to reduce clicks.
    /// </summary>
    /// <param name="slices">List of slices to refine.</param>
    /// <param name="audioData">Mono audio samples.</param>
    /// <param name="searchRange">Number of samples to search for zero crossing.</param>
    public void SnapToZeroCrossings(List<Slice> slices, float[] audioData, int searchRange = 100)
    {
        foreach (var slice in slices)
        {
            // Snap start position
            if (slice.StartSample > 0)
            {
                slice.StartSample = FindNearestZeroCrossing(audioData, slice.StartSample, searchRange);
            }

            // Snap end position
            if (slice.EndSample < audioData.Length)
            {
                slice.EndSample = FindNearestZeroCrossing(audioData, slice.EndSample, searchRange);
            }
        }
    }

    /// <summary>
    /// Finds the nearest zero crossing to a given sample position.
    /// </summary>
    private long FindNearestZeroCrossing(float[] audioData, long position, int searchRange)
    {
        long bestPos = position;
        float minAbs = float.MaxValue;

        long searchStart = Math.Max(0, position - searchRange);
        long searchEnd = Math.Min(audioData.Length - 1, position + searchRange);

        for (long i = searchStart; i < searchEnd; i++)
        {
            // Check for actual zero crossing
            if (i > 0 && audioData[i - 1] * audioData[i] <= 0)
            {
                // Found zero crossing, check if it's closer
                float absVal = Math.Abs(audioData[i]);
                if (absVal < minAbs)
                {
                    minAbs = absVal;
                    bestPos = i;
                }
            }
        }

        return bestPos;
    }

    /// <summary>
    /// Merges adjacent slices that are shorter than the minimum length.
    /// </summary>
    /// <param name="slices">List of slices to merge.</param>
    /// <param name="minLength">Minimum slice length in samples.</param>
    /// <returns>New list with merged slices.</returns>
    public List<Slice> MergeShortSlices(List<Slice> slices, int minLength)
    {
        if (slices.Count == 0) return new List<Slice>();

        var merged = new List<Slice>();
        Slice? currentSlice = null;

        for (int i = 0; i < slices.Count; i++)
        {
            var slice = slices[i];

            if (currentSlice == null)
            {
                currentSlice = new Slice(merged.Count, slice.StartSample, slice.EndSample);
            }
            else if (slice.LengthSamples < minLength)
            {
                // Merge with current slice
                currentSlice.EndSample = slice.EndSample;
            }
            else
            {
                // Add current slice if it meets minimum length
                if (currentSlice.LengthSamples >= minLength)
                {
                    merged.Add(currentSlice);
                    currentSlice = new Slice(merged.Count, slice.StartSample, slice.EndSample);
                }
                else
                {
                    // Extend current slice
                    currentSlice.EndSample = slice.EndSample;
                }
            }
        }

        // Add final slice
        if (currentSlice != null)
        {
            merged.Add(currentSlice);
        }

        return merged;
    }
}
