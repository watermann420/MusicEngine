// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio analysis component.

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Represents a single frame of spectral data from STFT analysis.
/// Contains magnitude and phase information for each FFT bin, enabling
/// detailed spectral editing and manipulation.
/// </summary>
public class SpectralFrame
{
    private readonly float[] _magnitudes;
    private readonly float[] _phases;
    private readonly int _fftSize;
    private readonly int _sampleRate;
    private readonly double _timePosition;

    /// <summary>
    /// Gets the time position of this frame in seconds.
    /// </summary>
    public double TimePosition => _timePosition;

    /// <summary>
    /// Gets the FFT bin magnitudes (half FFT size + 1 for DC to Nyquist).
    /// </summary>
    public float[] Magnitudes => _magnitudes;

    /// <summary>
    /// Gets the FFT bin phases in radians (-PI to PI).
    /// </summary>
    public float[] Phases => _phases;

    /// <summary>
    /// Gets the FFT size used for analysis.
    /// </summary>
    public int FftSize => _fftSize;

    /// <summary>
    /// Gets the sample rate of the audio.
    /// </summary>
    public int SampleRate => _sampleRate;

    /// <summary>
    /// Gets the number of frequency bins (FftSize / 2 + 1).
    /// </summary>
    public int BinCount => _magnitudes.Length;

    /// <summary>
    /// Gets the frequency resolution in Hz per bin.
    /// </summary>
    public float FrequencyResolution => (float)_sampleRate / _fftSize;

    /// <summary>
    /// Gets the Nyquist frequency (maximum representable frequency).
    /// </summary>
    public float NyquistFrequency => _sampleRate / 2f;

    /// <summary>
    /// Creates a new spectral frame with the specified parameters.
    /// </summary>
    /// <param name="time">Time position in seconds</param>
    /// <param name="fftSize">FFT window size (must be power of 2)</param>
    /// <param name="sampleRate">Audio sample rate in Hz</param>
    public SpectralFrame(double time, int fftSize, int sampleRate)
    {
        if (!IsPowerOfTwo(fftSize))
            throw new ArgumentException("FFT size must be a power of two.", nameof(fftSize));
        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "Sample rate must be positive.");

        _timePosition = time;
        _fftSize = fftSize;
        _sampleRate = sampleRate;

        int binCount = fftSize / 2 + 1;
        _magnitudes = new float[binCount];
        _phases = new float[binCount];
    }

    /// <summary>
    /// Creates a spectral frame from existing magnitude and phase data.
    /// </summary>
    /// <param name="time">Time position in seconds</param>
    /// <param name="fftSize">FFT window size</param>
    /// <param name="sampleRate">Sample rate in Hz</param>
    /// <param name="magnitudes">Magnitude values for each bin</param>
    /// <param name="phases">Phase values for each bin</param>
    public SpectralFrame(double time, int fftSize, int sampleRate, float[] magnitudes, float[] phases)
        : this(time, fftSize, sampleRate)
    {
        int expectedBins = fftSize / 2 + 1;
        if (magnitudes.Length != expectedBins)
            throw new ArgumentException($"Magnitudes array must have {expectedBins} elements.", nameof(magnitudes));
        if (phases.Length != expectedBins)
            throw new ArgumentException($"Phases array must have {expectedBins} elements.", nameof(phases));

        Array.Copy(magnitudes, _magnitudes, expectedBins);
        Array.Copy(phases, _phases, expectedBins);
    }

    /// <summary>
    /// Gets the frequency in Hz for a given bin index.
    /// </summary>
    /// <param name="binIndex">The FFT bin index</param>
    /// <returns>Frequency in Hz</returns>
    public float GetFrequencyForBin(int binIndex)
    {
        if (binIndex < 0 || binIndex >= BinCount)
            throw new ArgumentOutOfRangeException(nameof(binIndex));
        return binIndex * FrequencyResolution;
    }

    /// <summary>
    /// Gets the bin index for a given frequency.
    /// </summary>
    /// <param name="frequency">Frequency in Hz</param>
    /// <returns>Nearest bin index</returns>
    public int GetBinForFrequency(float frequency)
    {
        if (frequency < 0)
            throw new ArgumentOutOfRangeException(nameof(frequency), "Frequency must be non-negative.");

        int bin = (int)MathF.Round(frequency / FrequencyResolution);
        return Math.Clamp(bin, 0, BinCount - 1);
    }

    /// <summary>
    /// Gets the magnitude at a specific frequency using linear interpolation.
    /// </summary>
    /// <param name="frequency">Frequency in Hz</param>
    /// <returns>Interpolated magnitude value</returns>
    public float GetMagnitudeAtFrequency(float frequency)
    {
        if (frequency < 0 || frequency > NyquistFrequency)
            return 0f;

        float exactBin = frequency / FrequencyResolution;
        int lowBin = (int)exactBin;
        int highBin = Math.Min(lowBin + 1, BinCount - 1);
        float fraction = exactBin - lowBin;

        // Linear interpolation
        return _magnitudes[lowBin] * (1f - fraction) + _magnitudes[highBin] * fraction;
    }

    /// <summary>
    /// Sets the magnitude at a specific frequency.
    /// Affects the nearest bin or uses interpolated distribution for sub-bin precision.
    /// </summary>
    /// <param name="frequency">Frequency in Hz</param>
    /// <param name="magnitude">New magnitude value</param>
    public void SetMagnitudeAtFrequency(float frequency, float magnitude)
    {
        if (frequency < 0 || frequency > NyquistFrequency)
            return;

        float exactBin = frequency / FrequencyResolution;
        int lowBin = (int)exactBin;
        int highBin = Math.Min(lowBin + 1, BinCount - 1);
        float fraction = exactBin - lowBin;

        // Distribute magnitude to adjacent bins based on fractional position
        if (lowBin < BinCount)
            _magnitudes[lowBin] = magnitude * (1f - fraction);
        if (highBin < BinCount && highBin != lowBin)
            _magnitudes[highBin] = magnitude * fraction;
    }

    /// <summary>
    /// Scales magnitudes within a frequency range by a given factor.
    /// </summary>
    /// <param name="minFreq">Minimum frequency in Hz</param>
    /// <param name="maxFreq">Maximum frequency in Hz</param>
    /// <param name="scale">Scale factor (0 = silence, 1 = unchanged, 2 = double)</param>
    public void ScaleMagnitudeRange(float minFreq, float maxFreq, float scale)
    {
        if (minFreq >= maxFreq)
            throw new ArgumentException("Minimum frequency must be less than maximum frequency.");

        int minBin = GetBinForFrequency(Math.Max(0, minFreq));
        int maxBin = GetBinForFrequency(Math.Min(NyquistFrequency, maxFreq));

        for (int bin = minBin; bin <= maxBin; bin++)
        {
            _magnitudes[bin] *= scale;
        }
    }

    /// <summary>
    /// Gets the phase at a specific frequency using linear interpolation.
    /// </summary>
    /// <param name="frequency">Frequency in Hz</param>
    /// <returns>Interpolated phase value in radians</returns>
    public float GetPhaseAtFrequency(float frequency)
    {
        if (frequency < 0 || frequency > NyquistFrequency)
            return 0f;

        float exactBin = frequency / FrequencyResolution;
        int lowBin = (int)exactBin;
        int highBin = Math.Min(lowBin + 1, BinCount - 1);
        float fraction = exactBin - lowBin;

        // Phase interpolation requires unwrapping
        float phase1 = _phases[lowBin];
        float phase2 = _phases[highBin];

        // Simple linear interpolation (may need unwrapping for accuracy)
        return phase1 * (1f - fraction) + phase2 * fraction;
    }

    /// <summary>
    /// Sets the phase at a specific frequency.
    /// </summary>
    /// <param name="frequency">Frequency in Hz</param>
    /// <param name="phase">Phase value in radians</param>
    public void SetPhaseAtFrequency(float frequency, float phase)
    {
        if (frequency < 0 || frequency > NyquistFrequency)
            return;

        int bin = GetBinForFrequency(frequency);
        _phases[bin] = WrapPhase(phase);
    }

    /// <summary>
    /// Copies magnitude and phase data from another frame.
    /// </summary>
    /// <param name="source">Source frame to copy from</param>
    public void CopyFrom(SpectralFrame source)
    {
        if (source.BinCount != BinCount)
            throw new ArgumentException("Source frame must have the same bin count.", nameof(source));

        Array.Copy(source._magnitudes, _magnitudes, BinCount);
        Array.Copy(source._phases, _phases, BinCount);
    }

    /// <summary>
    /// Creates a deep copy of this frame.
    /// </summary>
    /// <returns>A new SpectralFrame with copied data</returns>
    public SpectralFrame Clone()
    {
        return new SpectralFrame(_timePosition, _fftSize, _sampleRate, _magnitudes, _phases);
    }

    /// <summary>
    /// Clears all magnitude and phase data (sets to zero).
    /// </summary>
    public void Clear()
    {
        Array.Clear(_magnitudes, 0, _magnitudes.Length);
        Array.Clear(_phases, 0, _phases.Length);
    }

    /// <summary>
    /// Gets the total energy (sum of squared magnitudes) in a frequency range.
    /// </summary>
    /// <param name="minFreq">Minimum frequency in Hz</param>
    /// <param name="maxFreq">Maximum frequency in Hz</param>
    /// <returns>Total energy in the range</returns>
    public float GetEnergyInRange(float minFreq, float maxFreq)
    {
        int minBin = GetBinForFrequency(Math.Max(0, minFreq));
        int maxBin = GetBinForFrequency(Math.Min(NyquistFrequency, maxFreq));

        float energy = 0f;
        for (int bin = minBin; bin <= maxBin; bin++)
        {
            energy += _magnitudes[bin] * _magnitudes[bin];
        }

        return energy;
    }

    /// <summary>
    /// Finds the dominant frequency (highest magnitude) in a range.
    /// </summary>
    /// <param name="minFreq">Minimum frequency in Hz</param>
    /// <param name="maxFreq">Maximum frequency in Hz</param>
    /// <returns>Frequency with highest magnitude</returns>
    public float GetDominantFrequency(float minFreq, float maxFreq)
    {
        int minBin = GetBinForFrequency(Math.Max(0, minFreq));
        int maxBin = GetBinForFrequency(Math.Min(NyquistFrequency, maxFreq));

        int peakBin = minBin;
        float peakMagnitude = _magnitudes[minBin];

        for (int bin = minBin + 1; bin <= maxBin; bin++)
        {
            if (_magnitudes[bin] > peakMagnitude)
            {
                peakMagnitude = _magnitudes[bin];
                peakBin = bin;
            }
        }

        // Parabolic interpolation for sub-bin accuracy
        if (peakBin > 0 && peakBin < BinCount - 1)
        {
            float alpha = _magnitudes[peakBin - 1];
            float beta = _magnitudes[peakBin];
            float gamma = _magnitudes[peakBin + 1];

            float p = 0.5f * (alpha - gamma) / (alpha - 2f * beta + gamma);
            return (peakBin + p) * FrequencyResolution;
        }

        return peakBin * FrequencyResolution;
    }

    private static float WrapPhase(float phase)
    {
        while (phase > MathF.PI) phase -= 2f * MathF.PI;
        while (phase < -MathF.PI) phase += 2f * MathF.PI;
        return phase;
    }

    private static bool IsPowerOfTwo(int n) => n > 0 && (n & (n - 1)) == 0;
}
