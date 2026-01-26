// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio analysis component.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using NAudio.Wave;

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Target response curves for room correction.
/// </summary>
public enum TargetCurve
{
    /// <summary>Flat frequency response</summary>
    Flat,
    /// <summary>X-curve (cinema standard, gentle roll-off)</summary>
    XCurve,
    /// <summary>B&K house curve (slight bass boost, treble roll-off)</summary>
    BKHouseCurve,
    /// <summary>Harman target curve (headphone-like response)</summary>
    HarmanCurve,
    /// <summary>Custom user-defined curve</summary>
    Custom
}

/// <summary>
/// Represents a room mode (resonance).
/// </summary>
public record RoomMode(
    float Frequency,
    float Q,
    float GainDb,
    string Type // "Axial", "Tangential", "Oblique"
);

/// <summary>
/// Represents a single EQ band for correction.
/// </summary>
public record CorrectionBand(
    float Frequency,
    float GainDb,
    float Q,
    string FilterType // "Peak", "LowShelf", "HighShelf", "LowPass", "HighPass"
);

/// <summary>
/// Represents a complete room measurement.
/// </summary>
public class RoomMeasurement
{
    /// <summary>
    /// Measurement position name (e.g., "Left Speaker", "Center")
    /// </summary>
    public string PositionName { get; set; } = "";

    /// <summary>
    /// Raw impulse response samples
    /// </summary>
    public float[] ImpulseResponse { get; set; } = Array.Empty<float>();

    /// <summary>
    /// Frequency response magnitude (dB) per frequency bin
    /// </summary>
    public float[] FrequencyResponseDb { get; set; } = Array.Empty<float>();

    /// <summary>
    /// Frequency values for each bin
    /// </summary>
    public float[] Frequencies { get; set; } = Array.Empty<float>();

    /// <summary>
    /// Sample rate of the measurement
    /// </summary>
    public int SampleRate { get; set; }

    /// <summary>
    /// RT60 reverberation time in seconds
    /// </summary>
    public float RT60 { get; set; }

    /// <summary>
    /// Detected room modes
    /// </summary>
    public List<RoomMode> RoomModes { get; set; } = new();

    /// <summary>
    /// Timestamp of the measurement
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

/// <summary>
/// Complete room correction profile.
/// </summary>
public class RoomCorrectionProfile
{
    /// <summary>
    /// Profile name
    /// </summary>
    public string Name { get; set; } = "Room Correction";

    /// <summary>
    /// Individual measurements from multiple positions
    /// </summary>
    public List<RoomMeasurement> Measurements { get; set; } = new();

    /// <summary>
    /// Averaged frequency response (dB)
    /// </summary>
    public float[] AveragedResponseDb { get; set; } = Array.Empty<float>();

    /// <summary>
    /// Correction curve (inverse of room response + target)
    /// </summary>
    public float[] CorrectionCurveDb { get; set; } = Array.Empty<float>();

    /// <summary>
    /// Generated parametric EQ bands
    /// </summary>
    public List<CorrectionBand> EQBands { get; set; } = new();

    /// <summary>
    /// Target curve used
    /// </summary>
    public TargetCurve TargetCurve { get; set; } = TargetCurve.Flat;

    /// <summary>
    /// Custom target curve (if TargetCurve == Custom)
    /// </summary>
    public float[]? CustomTargetCurve { get; set; }

    /// <summary>
    /// Maximum correction in dB (positive or negative)
    /// </summary>
    public float MaxCorrectionDb { get; set; } = 12f;

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Last modified timestamp
    /// </summary>
    public DateTime ModifiedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// Acoustic room measurement and correction system.
/// Measures room response using sweep or impulse and generates correction EQ.
/// </summary>
public class RoomCorrection
{
    private readonly int _sampleRate;
    private readonly int _fftSize;
    private readonly Complex[] _fftBuffer;
    private readonly Complex[] _fftResult;
    private readonly float[] _sweepSignal;
    private readonly float[] _inverseSweep;

    // Measurement state
    private bool _isMeasuring;
    private float[] _measurementBuffer = Array.Empty<float>();
    private int _measurementPosition;

    /// <summary>
    /// Gets the current room correction profile.
    /// </summary>
    public RoomCorrectionProfile? CurrentProfile { get; private set; }

    /// <summary>
    /// Gets whether a measurement is in progress.
    /// </summary>
    public bool IsMeasuring => _isMeasuring;

    /// <summary>
    /// Event raised when measurement progress updates.
    /// </summary>
    public event Action<float>? MeasurementProgress;

    /// <summary>
    /// Event raised when a measurement is complete.
    /// </summary>
    public event Action<RoomMeasurement>? MeasurementComplete;

    /// <summary>
    /// Creates a new room correction system.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate</param>
    /// <param name="fftSize">FFT size for analysis (default 8192)</param>
    public RoomCorrection(int sampleRate = 44100, int fftSize = 8192)
    {
        _sampleRate = sampleRate;
        _fftSize = fftSize;
        _fftBuffer = new Complex[_fftSize];
        _fftResult = new Complex[_fftSize];

        // Generate measurement sweep
        int sweepLength = sampleRate * 3; // 3 second sweep
        _sweepSignal = GenerateLogSweep(20f, 20000f, sweepLength, sampleRate);
        _inverseSweep = GenerateInverseSweep(_sweepSignal, 20f, 20000f, sampleRate);
    }

    /// <summary>
    /// Generates a logarithmic sine sweep for measurement.
    /// </summary>
    private float[] GenerateLogSweep(float startFreq, float endFreq, int lengthSamples, int sampleRate)
    {
        var sweep = new float[lengthSamples];
        float k = (float)(lengthSamples / Math.Log(endFreq / startFreq));
        float l = lengthSamples / MathF.Log(endFreq / startFreq);

        for (int i = 0; i < lengthSamples; i++)
        {
            float t = (float)i / sampleRate;
            float phase = 2f * MathF.PI * startFreq * l * (MathF.Pow(endFreq / startFreq, t * sampleRate / lengthSamples) - 1f);
            sweep[i] = MathF.Sin(phase);

            // Apply fade in/out
            int fadeSamples = sampleRate / 10; // 100ms fade
            if (i < fadeSamples)
                sweep[i] *= (float)i / fadeSamples;
            else if (i > lengthSamples - fadeSamples)
                sweep[i] *= (float)(lengthSamples - i) / fadeSamples;
        }

        return sweep;
    }

    /// <summary>
    /// Generates the inverse sweep filter for deconvolution.
    /// </summary>
    private float[] GenerateInverseSweep(float[] sweep, float startFreq, float endFreq, int sampleRate)
    {
        var inverse = new float[sweep.Length];

        // Time-reverse and apply amplitude envelope
        float logRatio = MathF.Log(endFreq / startFreq);

        for (int i = 0; i < sweep.Length; i++)
        {
            int reverseIdx = sweep.Length - 1 - i;
            float t = (float)i / sweep.Length;
            float envelope = MathF.Exp(t * logRatio);
            inverse[i] = sweep[reverseIdx] * envelope;
        }

        // Normalize
        float maxVal = inverse.Max(MathF.Abs);
        if (maxVal > 0)
        {
            for (int i = 0; i < inverse.Length; i++)
                inverse[i] /= maxVal;
        }

        return inverse;
    }

    /// <summary>
    /// Gets the measurement sweep signal for playback.
    /// </summary>
    public float[] GetMeasurementSweep()
    {
        return (float[])_sweepSignal.Clone();
    }

    /// <summary>
    /// Starts a new measurement session.
    /// </summary>
    /// <param name="positionName">Name of the measurement position</param>
    public void StartMeasurement(string positionName = "Position 1")
    {
        if (_isMeasuring)
            throw new InvalidOperationException("Measurement already in progress");

        _isMeasuring = true;
        _measurementBuffer = new float[_sweepSignal.Length + _sampleRate]; // Extra second for reverb tail
        _measurementPosition = 0;
    }

    /// <summary>
    /// Adds recorded samples to the current measurement.
    /// </summary>
    /// <param name="samples">Recorded audio samples</param>
    /// <param name="count">Number of samples</param>
    public void AddMeasurementSamples(float[] samples, int count)
    {
        if (!_isMeasuring)
            return;

        int samplesToAdd = Math.Min(count, _measurementBuffer.Length - _measurementPosition);
        Array.Copy(samples, 0, _measurementBuffer, _measurementPosition, samplesToAdd);
        _measurementPosition += samplesToAdd;

        // Report progress
        float progress = (float)_measurementPosition / _measurementBuffer.Length;
        MeasurementProgress?.Invoke(progress);

        // Check if complete
        if (_measurementPosition >= _measurementBuffer.Length)
        {
            CompleteMeasurement();
        }
    }

    /// <summary>
    /// Processes the recorded measurement and extracts room response.
    /// </summary>
    private void CompleteMeasurement()
    {
        _isMeasuring = false;

        // Deconvolve to get impulse response
        var impulseResponse = Deconvolve(_measurementBuffer, _inverseSweep);

        // Create measurement object
        var measurement = new RoomMeasurement
        {
            PositionName = $"Position {CurrentProfile?.Measurements.Count + 1}",
            ImpulseResponse = impulseResponse,
            SampleRate = _sampleRate,
            Timestamp = DateTime.Now
        };

        // Calculate frequency response
        CalculateFrequencyResponse(measurement);

        // Detect room modes
        DetectRoomModes(measurement);

        // Calculate RT60
        measurement.RT60 = CalculateRT60(impulseResponse, _sampleRate);

        // Add to profile
        CurrentProfile ??= new RoomCorrectionProfile();
        CurrentProfile.Measurements.Add(measurement);

        MeasurementComplete?.Invoke(measurement);
    }

    /// <summary>
    /// Performs deconvolution to extract impulse response.
    /// </summary>
    private float[] Deconvolve(float[] recorded, float[] inverse)
    {
        int convLength = recorded.Length + inverse.Length - 1;
        int fftLength = 1;
        while (fftLength < convLength)
            fftLength *= 2;

        var fftRecorded = new Complex[fftLength];
        var fftInverse = new Complex[fftLength];
        var fftResult = new Complex[fftLength];

        // Zero-pad and copy to complex arrays
        for (int i = 0; i < recorded.Length; i++)
            fftRecorded[i] = new Complex(recorded[i], 0);

        for (int i = 0; i < inverse.Length; i++)
            fftInverse[i] = new Complex(inverse[i], 0);

        // FFT both signals
        FFT(fftRecorded, false);
        FFT(fftInverse, false);

        // Multiply in frequency domain
        for (int i = 0; i < fftLength; i++)
            fftResult[i] = fftRecorded[i] * fftInverse[i];

        // Inverse FFT
        FFT(fftResult, true);

        // Extract real part
        var result = new float[recorded.Length];
        for (int i = 0; i < result.Length; i++)
            result[i] = (float)fftResult[i].Real / fftLength;

        // Normalize
        float maxVal = result.Max(MathF.Abs);
        if (maxVal > 0)
        {
            for (int i = 0; i < result.Length; i++)
                result[i] /= maxVal;
        }

        return result;
    }

    /// <summary>
    /// Calculates frequency response from impulse response.
    /// </summary>
    private void CalculateFrequencyResponse(RoomMeasurement measurement)
    {
        var ir = measurement.ImpulseResponse;

        // Apply window and FFT
        for (int i = 0; i < _fftSize; i++)
        {
            float window = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (_fftSize - 1)));
            float sample = i < ir.Length ? ir[i] * window : 0f;
            _fftBuffer[i] = new Complex(sample, 0);
        }

        Array.Copy(_fftBuffer, _fftResult, _fftSize);
        FFT(_fftResult, false);

        // Calculate magnitude in dB
        int numBins = _fftSize / 2;
        measurement.FrequencyResponseDb = new float[numBins];
        measurement.Frequencies = new float[numBins];

        for (int i = 0; i < numBins; i++)
        {
            measurement.Frequencies[i] = (float)i * _sampleRate / _fftSize;
            double magnitude = _fftResult[i].Magnitude;
            measurement.FrequencyResponseDb[i] = magnitude > 1e-10
                ? (float)(20.0 * Math.Log10(magnitude))
                : -100f;
        }
    }

    /// <summary>
    /// Detects room modes (resonances) in the measurement.
    /// </summary>
    private void DetectRoomModes(RoomMeasurement measurement)
    {
        measurement.RoomModes.Clear();

        var freqResp = measurement.FrequencyResponseDb;
        var freqs = measurement.Frequencies;

        // Smooth the response for peak detection
        var smoothed = new float[freqResp.Length];
        int smoothWindow = 5;
        for (int i = 0; i < freqResp.Length; i++)
        {
            float sum = 0;
            int count = 0;
            for (int j = Math.Max(0, i - smoothWindow); j <= Math.Min(freqResp.Length - 1, i + smoothWindow); j++)
            {
                sum += freqResp[j];
                count++;
            }
            smoothed[i] = sum / count;
        }

        // Find peaks (potential room modes)
        for (int i = 2; i < freqResp.Length - 2; i++)
        {
            // Only look in bass/low-mid range for room modes (20-500 Hz)
            if (freqs[i] < 20 || freqs[i] > 500)
                continue;

            // Local maximum detection
            if (freqResp[i] > freqResp[i - 1] && freqResp[i] > freqResp[i + 1] &&
                freqResp[i] > freqResp[i - 2] && freqResp[i] > freqResp[i + 2])
            {
                // Check if it's significantly above the smoothed average
                float deviation = freqResp[i] - smoothed[i];
                if (deviation > 3f) // At least 3dB above average
                {
                    // Estimate Q from peak width
                    float peakLevel = freqResp[i];
                    float targetLevel = peakLevel - 3f; // -3dB points

                    // Find lower -3dB point
                    float lowerFreq = freqs[i];
                    for (int j = i - 1; j >= 0 && freqResp[j] > targetLevel; j--)
                        lowerFreq = freqs[j];

                    // Find upper -3dB point
                    float upperFreq = freqs[i];
                    for (int j = i + 1; j < freqResp.Length && freqResp[j] > targetLevel; j++)
                        upperFreq = freqs[j];

                    float bandwidth = upperFreq - lowerFreq;
                    float q = bandwidth > 0 ? freqs[i] / bandwidth : 10f;

                    // Classify mode type based on frequency
                    string modeType = freqs[i] < 100 ? "Axial" :
                                      freqs[i] < 200 ? "Tangential" : "Oblique";

                    measurement.RoomModes.Add(new RoomMode(
                        freqs[i],
                        Math.Clamp(q, 1f, 20f),
                        deviation,
                        modeType
                    ));
                }
            }
        }
    }

    /// <summary>
    /// Calculates RT60 reverberation time.
    /// </summary>
    private float CalculateRT60(float[] impulseResponse, int sampleRate)
    {
        // Calculate energy decay curve (Schroeder integration)
        var energyDecay = new float[impulseResponse.Length];
        float totalEnergy = 0;

        for (int i = impulseResponse.Length - 1; i >= 0; i--)
        {
            totalEnergy += impulseResponse[i] * impulseResponse[i];
            energyDecay[i] = totalEnergy;
        }

        // Convert to dB
        float maxEnergy = energyDecay.Max();
        if (maxEnergy <= 0)
            return 0;

        var decayDb = new float[energyDecay.Length];
        for (int i = 0; i < energyDecay.Length; i++)
        {
            decayDb[i] = energyDecay[i] > 0
                ? 10f * MathF.Log10(energyDecay[i] / maxEnergy)
                : -100f;
        }

        // Find -5dB and -35dB points (EDT -> RT60 extrapolation)
        int idx5dB = -1, idx35dB = -1;
        for (int i = 0; i < decayDb.Length; i++)
        {
            if (idx5dB < 0 && decayDb[i] <= -5f)
                idx5dB = i;
            if (idx35dB < 0 && decayDb[i] <= -35f)
                idx35dB = i;
        }

        if (idx5dB >= 0 && idx35dB > idx5dB)
        {
            // Linear regression to estimate decay rate
            float timeDiff = (float)(idx35dB - idx5dB) / sampleRate;
            float rt60 = 2f * timeDiff; // Extrapolate to 60dB
            return Math.Clamp(rt60, 0.1f, 10f);
        }

        return 0.5f; // Default
    }

    /// <summary>
    /// Generates a correction EQ curve from the averaged measurements.
    /// </summary>
    /// <param name="targetCurve">Target response curve</param>
    /// <param name="maxBands">Maximum number of EQ bands</param>
    /// <param name="maxCorrectionDb">Maximum correction in dB</param>
    public void GenerateCorrectionEQ(
        TargetCurve targetCurve = TargetCurve.Flat,
        int maxBands = 10,
        float maxCorrectionDb = 12f)
    {
        if (CurrentProfile == null || CurrentProfile.Measurements.Count == 0)
            throw new InvalidOperationException("No measurements available");

        CurrentProfile.TargetCurve = targetCurve;
        CurrentProfile.MaxCorrectionDb = maxCorrectionDb;

        // Average all measurements
        AverageMeasurements();

        // Generate target curve
        var target = GenerateTargetCurve(targetCurve, CurrentProfile.AveragedResponseDb.Length);

        // Calculate correction curve (target - measured)
        CurrentProfile.CorrectionCurveDb = new float[CurrentProfile.AveragedResponseDb.Length];
        for (int i = 0; i < CurrentProfile.CorrectionCurveDb.Length; i++)
        {
            float correction = target[i] - CurrentProfile.AveragedResponseDb[i];
            CurrentProfile.CorrectionCurveDb[i] = Math.Clamp(correction, -maxCorrectionDb, maxCorrectionDb);
        }

        // Fit parametric EQ bands to the correction curve
        FitEQBands(maxBands);

        CurrentProfile.ModifiedAt = DateTime.Now;
    }

    /// <summary>
    /// Averages all measurements in the profile.
    /// </summary>
    private void AverageMeasurements()
    {
        if (CurrentProfile == null || CurrentProfile.Measurements.Count == 0)
            return;

        var first = CurrentProfile.Measurements[0];
        CurrentProfile.AveragedResponseDb = new float[first.FrequencyResponseDb.Length];

        foreach (var measurement in CurrentProfile.Measurements)
        {
            for (int i = 0; i < CurrentProfile.AveragedResponseDb.Length; i++)
            {
                if (i < measurement.FrequencyResponseDb.Length)
                    CurrentProfile.AveragedResponseDb[i] += measurement.FrequencyResponseDb[i];
            }
        }

        float count = CurrentProfile.Measurements.Count;
        for (int i = 0; i < CurrentProfile.AveragedResponseDb.Length; i++)
            CurrentProfile.AveragedResponseDb[i] /= count;
    }

    /// <summary>
    /// Generates a target response curve.
    /// </summary>
    private float[] GenerateTargetCurve(TargetCurve curve, int length)
    {
        var target = new float[length];
        float freqPerBin = (float)_sampleRate / (_fftSize * 2);

        for (int i = 0; i < length; i++)
        {
            float freq = i * freqPerBin;

            switch (curve)
            {
                case TargetCurve.Flat:
                    target[i] = 0f;
                    break;

                case TargetCurve.XCurve:
                    // X-curve: flat to 2kHz, then -3dB/octave roll-off
                    if (freq > 2000)
                        target[i] = -3f * MathF.Log2(freq / 2000f);
                    else
                        target[i] = 0f;
                    break;

                case TargetCurve.BKHouseCurve:
                    // B&K: +3dB at 50Hz, flat 200Hz-2kHz, -3dB at 10kHz
                    if (freq < 200)
                        target[i] = 3f * (1f - MathF.Log2(freq / 50f + 1f) / MathF.Log2(4f + 1f));
                    else if (freq > 2000)
                        target[i] = -3f * MathF.Log2(freq / 2000f) / MathF.Log2(5f);
                    else
                        target[i] = 0f;
                    break;

                case TargetCurve.HarmanCurve:
                    // Harman: bass boost, slight presence dip, treble roll-off
                    if (freq < 100)
                        target[i] = 4f;
                    else if (freq < 200)
                        target[i] = 4f * (1f - (freq - 100f) / 100f);
                    else if (freq > 3000 && freq < 5000)
                        target[i] = -2f * (freq - 3000f) / 2000f;
                    else if (freq >= 5000)
                        target[i] = -2f - 2f * MathF.Log2(freq / 5000f);
                    else
                        target[i] = 0f;
                    break;

                case TargetCurve.Custom:
                    if (CurrentProfile?.CustomTargetCurve != null && i < CurrentProfile.CustomTargetCurve.Length)
                        target[i] = CurrentProfile.CustomTargetCurve[i];
                    else
                        target[i] = 0f;
                    break;
            }
        }

        return target;
    }

    /// <summary>
    /// Fits parametric EQ bands to the correction curve.
    /// </summary>
    private void FitEQBands(int maxBands)
    {
        if (CurrentProfile == null)
            return;

        CurrentProfile.EQBands.Clear();

        var correction = CurrentProfile.CorrectionCurveDb;
        float freqPerBin = (float)_sampleRate / (_fftSize * 2);

        // Find significant peaks and dips in correction curve
        var candidates = new List<(int bin, float magnitude, bool isPeak)>();

        for (int i = 2; i < correction.Length - 2; i++)
        {
            float freq = i * freqPerBin;
            if (freq < 20 || freq > 20000)
                continue;

            // Detect local extrema
            bool isLocalMax = correction[i] > correction[i - 1] && correction[i] > correction[i + 1] &&
                              correction[i] > correction[i - 2] && correction[i] > correction[i + 2];
            bool isLocalMin = correction[i] < correction[i - 1] && correction[i] < correction[i + 1] &&
                              correction[i] < correction[i - 2] && correction[i] < correction[i + 2];

            if ((isLocalMax || isLocalMin) && MathF.Abs(correction[i]) > 2f)
            {
                candidates.Add((i, MathF.Abs(correction[i]), isLocalMax));
            }
        }

        // Sort by magnitude and take top N
        var sorted = candidates.OrderByDescending(c => c.magnitude).Take(maxBands).ToList();

        foreach (var (bin, _, isPeak) in sorted)
        {
            float freq = bin * freqPerBin;
            float gain = correction[bin];

            // Estimate Q from the width of the correction region
            float q = 2f; // Default Q
            float targetLevel = gain * 0.5f; // Half-power point

            int lowerBin = bin, upperBin = bin;
            for (int j = bin - 1; j >= 0; j--)
            {
                if (MathF.Abs(correction[j]) < MathF.Abs(targetLevel))
                    break;
                lowerBin = j;
            }
            for (int j = bin + 1; j < correction.Length; j++)
            {
                if (MathF.Abs(correction[j]) < MathF.Abs(targetLevel))
                    break;
                upperBin = j;
            }

            float lowerFreq = lowerBin * freqPerBin;
            float upperFreq = upperBin * freqPerBin;
            float bandwidth = upperFreq - lowerFreq;

            if (bandwidth > 0)
                q = freq / bandwidth;
            q = Math.Clamp(q, 0.5f, 10f);

            // Determine filter type
            string filterType = "Peak";
            if (freq < 100)
                filterType = "LowShelf";
            else if (freq > 10000)
                filterType = "HighShelf";

            CurrentProfile.EQBands.Add(new CorrectionBand(freq, gain, q, filterType));
        }

        // Sort bands by frequency
        CurrentProfile.EQBands = CurrentProfile.EQBands.OrderBy(b => b.Frequency).ToList();
    }

    /// <summary>
    /// Exports the correction profile to a JSON file.
    /// </summary>
    public void ExportProfile(string path)
    {
        if (CurrentProfile == null)
            throw new InvalidOperationException("No profile to export");

        var json = JsonSerializer.Serialize(CurrentProfile, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Imports a correction profile from a JSON file.
    /// </summary>
    public void ImportProfile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Profile file not found", path);

        var json = File.ReadAllText(path);
        CurrentProfile = JsonSerializer.Deserialize<RoomCorrectionProfile>(json);
    }

    /// <summary>
    /// Creates a new empty profile.
    /// </summary>
    public void CreateNewProfile(string name = "Room Correction")
    {
        CurrentProfile = new RoomCorrectionProfile
        {
            Name = name,
            CreatedAt = DateTime.Now,
            ModifiedAt = DateTime.Now
        };
    }

    /// <summary>
    /// Clears all measurements from the current profile.
    /// </summary>
    public void ClearMeasurements()
    {
        CurrentProfile?.Measurements.Clear();
    }

    /// <summary>
    /// In-place Cooley-Tukey FFT.
    /// </summary>
    private void FFT(Complex[] data, bool inverse)
    {
        int n = data.Length;
        int bits = (int)Math.Log2(n);

        // Bit-reversal permutation
        for (int i = 0; i < n; i++)
        {
            int j = BitReverse(i, bits);
            if (j > i)
                (data[i], data[j]) = (data[j], data[i]);
        }

        // Cooley-Tukey iterative FFT
        for (int size = 2; size <= n; size *= 2)
        {
            double angle = (inverse ? 2 : -2) * Math.PI / size;
            var wn = new Complex(Math.Cos(angle), Math.Sin(angle));

            for (int start = 0; start < n; start += size)
            {
                var w = Complex.One;
                for (int k = 0; k < size / 2; k++)
                {
                    var t = w * data[start + k + size / 2];
                    var u = data[start + k];
                    data[start + k] = u + t;
                    data[start + k + size / 2] = u - t;
                    w *= wn;
                }
            }
        }
    }

    private int BitReverse(int x, int bits)
    {
        int result = 0;
        for (int i = 0; i < bits; i++)
        {
            result = (result << 1) | (x & 1);
            x >>= 1;
        }
        return result;
    }
}
