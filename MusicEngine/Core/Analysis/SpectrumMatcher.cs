//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Spectrum matching analyzer that captures reference and target spectrums and generates EQ correction curves.

using System;
using System.Collections.Generic;
using NAudio.Dsp;

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Represents an EQ correction point for spectrum matching.
/// </summary>
public class EqCorrectionPoint
{
    /// <summary>Gets or sets the frequency in Hz.</summary>
    public float Frequency { get; set; }

    /// <summary>Gets or sets the gain correction in dB (positive = boost, negative = cut).</summary>
    public float GainDb { get; set; }

    /// <summary>Gets or sets the Q factor (bandwidth) for parametric EQ.</summary>
    public float Q { get; set; } = 1.0f;

    /// <summary>Gets the linear gain multiplier.</summary>
    public float GainLinear => MathF.Pow(10, GainDb / 20f);
}

/// <summary>
/// Represents a captured spectrum profile.
/// </summary>
public class SpectrumProfile
{
    /// <summary>Gets or sets the name/description of this profile.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the center frequencies for each band in Hz.</summary>
    public float[] Frequencies { get; set; } = Array.Empty<float>();

    /// <summary>Gets or sets the magnitude values in dB for each band.</summary>
    public float[] MagnitudesDb { get; set; } = Array.Empty<float>();

    /// <summary>Gets or sets the sample rate used during capture.</summary>
    public int SampleRate { get; set; }

    /// <summary>Gets or sets the number of frames averaged for this profile.</summary>
    public int FrameCount { get; set; }

    /// <summary>Gets or sets the capture timestamp.</summary>
    public DateTime CaptureTime { get; set; } = DateTime.UtcNow;

    /// <summary>Gets whether the profile has valid data.</summary>
    public bool IsValid => Frequencies.Length > 0 && Frequencies.Length == MagnitudesDb.Length;

    /// <summary>
    /// Gets the magnitude at a specific frequency using linear interpolation.
    /// </summary>
    /// <param name="frequency">Frequency in Hz.</param>
    /// <returns>Interpolated magnitude in dB.</returns>
    public float GetMagnitudeAt(float frequency)
    {
        if (!IsValid) return 0;

        // Handle edge cases
        if (frequency <= Frequencies[0])
            return MagnitudesDb[0];
        if (frequency >= Frequencies[^1])
            return MagnitudesDb[^1];

        // Find surrounding frequencies and interpolate
        for (int i = 0; i < Frequencies.Length - 1; i++)
        {
            if (frequency >= Frequencies[i] && frequency <= Frequencies[i + 1])
            {
                float t = (frequency - Frequencies[i]) / (Frequencies[i + 1] - Frequencies[i]);
                return MagnitudesDb[i] + t * (MagnitudesDb[i + 1] - MagnitudesDb[i]);
            }
        }

        return 0;
    }
}

/// <summary>
/// Result of a spectrum matching analysis.
/// </summary>
public class SpectrumMatchResult
{
    /// <summary>Gets or sets the reference spectrum profile.</summary>
    public SpectrumProfile? ReferenceSpectrum { get; set; }

    /// <summary>Gets or sets the target spectrum profile.</summary>
    public SpectrumProfile? TargetSpectrum { get; set; }

    /// <summary>Gets or sets the EQ correction curve to match target to reference.</summary>
    public EqCorrectionPoint[] MatchCurve { get; set; } = Array.Empty<EqCorrectionPoint>();

    /// <summary>Gets or sets the per-band difference in dB (reference - target).</summary>
    public float[] DifferenceDb { get; set; } = Array.Empty<float>();

    /// <summary>Gets or sets the overall spectral similarity (0.0 = completely different, 1.0 = identical).</summary>
    public float Similarity { get; set; }

    /// <summary>Gets or sets the RMS difference across all bands in dB.</summary>
    public float RmsDifferenceDb { get; set; }

    /// <summary>Gets or sets the maximum difference in dB.</summary>
    public float MaxDifferenceDb { get; set; }

    /// <summary>Gets or sets the frequency at maximum difference.</summary>
    public float MaxDifferenceFrequency { get; set; }

    /// <summary>Gets whether the match result is valid.</summary>
    public bool IsValid => ReferenceSpectrum?.IsValid == true && TargetSpectrum?.IsValid == true;

    /// <summary>Gets the number of bands with significant difference (greater than 3 dB).</summary>
    public int SignificantDifferenceBandCount { get; set; }

    /// <summary>
    /// Gets the EQ correction value at a specific frequency.
    /// </summary>
    /// <param name="frequency">Frequency in Hz.</param>
    /// <returns>Interpolated correction in dB.</returns>
    public float GetCorrectionAt(float frequency)
    {
        if (MatchCurve.Length == 0) return 0;

        // Handle edge cases
        if (frequency <= MatchCurve[0].Frequency)
            return MatchCurve[0].GainDb;
        if (frequency >= MatchCurve[^1].Frequency)
            return MatchCurve[^1].GainDb;

        // Find surrounding points and interpolate
        for (int i = 0; i < MatchCurve.Length - 1; i++)
        {
            if (frequency >= MatchCurve[i].Frequency && frequency <= MatchCurve[i + 1].Frequency)
            {
                float t = (frequency - MatchCurve[i].Frequency) /
                          (MatchCurve[i + 1].Frequency - MatchCurve[i].Frequency);
                return MatchCurve[i].GainDb + t * (MatchCurve[i + 1].GainDb - MatchCurve[i].GainDb);
            }
        }

        return 0;
    }
}

/// <summary>
/// Event arguments for spectrum capture updates.
/// </summary>
public class SpectrumCaptureEventArgs : EventArgs
{
    /// <summary>Gets the captured spectrum profile.</summary>
    public SpectrumProfile Profile { get; }

    /// <summary>Gets whether this is a reference or target capture.</summary>
    public bool IsReference { get; }

    /// <summary>Gets the capture progress (0.0 to 1.0).</summary>
    public float Progress { get; }

    /// <summary>
    /// Creates new spectrum capture event arguments.
    /// </summary>
    public SpectrumCaptureEventArgs(SpectrumProfile profile, bool isReference, float progress)
    {
        Profile = profile;
        IsReference = isReference;
        Progress = progress;
    }
}

/// <summary>
/// Spectrum matcher that analyzes audio to capture reference and target spectrum profiles,
/// calculates the difference, and generates EQ correction curves for matching.
/// </summary>
/// <remarks>
/// This analyzer is useful for:
/// - Matching the tonal balance of one track to another (e.g., matching a mix to a reference master)
/// - Creating EQ curves to apply a "sound" from one source to another
/// - Analyzing spectral differences between recordings
/// - Quality control by comparing before/after processing
/// </remarks>
public class SpectrumMatcher
{
    private readonly int _sampleRate;
    private readonly int _fftLength;
    private readonly int _bandCount;
    private readonly float _minFrequency;
    private readonly float _maxFrequency;
    private readonly float[] _bandFrequencies;

    // FFT buffers
    private readonly Complex[] _fftBuffer;
    private readonly float[] _sampleBuffer;
    private readonly float[] _window;
    private int _sampleCount;

    // Capture accumulation
    private readonly double[] _magnitudeAccumulator;
    private int _captureFrameCount;
    private bool _isCapturing;
    private bool _capturingReference;

    // Stored spectrums
    private SpectrumProfile? _referenceSpectrum;
    private SpectrumProfile? _targetSpectrum;
    private SpectrumMatchResult? _cachedMatchResult;
    private bool _matchResultDirty = true;

    // Settings
    private float _smoothingFactor = 0.3f;
    private float _maxCorrectionDb = 12f;
    private float _minCorrectionDb = -12f;
    private float _correctionStrength = 1.0f;

    private readonly object _lock = new();

    /// <summary>
    /// Gets the sample rate used for analysis.
    /// </summary>
    public int SampleRate => _sampleRate;

    /// <summary>
    /// Gets the FFT length used for analysis.
    /// </summary>
    public int FftLength => _fftLength;

    /// <summary>
    /// Gets the number of frequency bands.
    /// </summary>
    public int BandCount => _bandCount;

    /// <summary>
    /// Gets the center frequencies for each band in Hz.
    /// </summary>
    public float[] BandFrequencies => (float[])_bandFrequencies.Clone();

    /// <summary>
    /// Gets the captured reference spectrum profile.
    /// </summary>
    public SpectrumProfile? ReferenceSpectrum
    {
        get
        {
            lock (_lock) return _referenceSpectrum;
        }
    }

    /// <summary>
    /// Gets the captured target spectrum profile.
    /// </summary>
    public SpectrumProfile? TargetSpectrum
    {
        get
        {
            lock (_lock) return _targetSpectrum;
        }
    }

    /// <summary>
    /// Gets the calculated match curve (EQ correction to transform target to reference).
    /// </summary>
    public SpectrumMatchResult? MatchCurve
    {
        get
        {
            lock (_lock)
            {
                if (_matchResultDirty && _referenceSpectrum?.IsValid == true && _targetSpectrum?.IsValid == true)
                {
                    _cachedMatchResult = CalculateMatchInternal();
                    _matchResultDirty = false;
                }
                return _cachedMatchResult;
            }
        }
    }

    /// <summary>
    /// Gets or sets the smoothing factor for FFT averaging (0.0 to 0.99).
    /// Higher values provide smoother results but slower response.
    /// </summary>
    public float SmoothingFactor
    {
        get => _smoothingFactor;
        set => _smoothingFactor = Math.Clamp(value, 0f, 0.99f);
    }

    /// <summary>
    /// Gets or sets the maximum positive correction (boost) in dB.
    /// </summary>
    public float MaxCorrectionDb
    {
        get => _maxCorrectionDb;
        set => _maxCorrectionDb = Math.Max(0f, value);
    }

    /// <summary>
    /// Gets or sets the maximum negative correction (cut) in dB.
    /// </summary>
    public float MinCorrectionDb
    {
        get => _minCorrectionDb;
        set => _minCorrectionDb = Math.Min(0f, value);
    }

    /// <summary>
    /// Gets or sets the correction strength (0.0 to 1.0).
    /// 1.0 = full correction, 0.5 = half correction, etc.
    /// </summary>
    public float CorrectionStrength
    {
        get => _correctionStrength;
        set => _correctionStrength = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Gets whether a capture is currently in progress.
    /// </summary>
    public bool IsCapturing
    {
        get
        {
            lock (_lock) return _isCapturing;
        }
    }

    /// <summary>
    /// Event raised during spectrum capture with progress updates.
    /// </summary>
    public event EventHandler<SpectrumCaptureEventArgs>? CaptureProgress;

    /// <summary>
    /// Event raised when a spectrum capture is completed.
    /// </summary>
    public event EventHandler<SpectrumCaptureEventArgs>? CaptureCompleted;

    /// <summary>
    /// Event raised when the match result is updated.
    /// </summary>
    public event EventHandler<SpectrumMatchResult>? MatchUpdated;

    /// <summary>
    /// Creates a new spectrum matcher with the specified configuration.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate in Hz (default: 44100).</param>
    /// <param name="bandCount">Number of frequency bands to analyze (default: 31).</param>
    /// <param name="fftLength">FFT window size, must be power of 2 (default: 4096).</param>
    /// <param name="minFrequency">Minimum analysis frequency in Hz (default: 20).</param>
    /// <param name="maxFrequency">Maximum analysis frequency in Hz (default: 20000).</param>
    public SpectrumMatcher(
        int sampleRate = 44100,
        int bandCount = 31,
        int fftLength = 4096,
        float minFrequency = 20f,
        float maxFrequency = 20000f)
    {
        if (!IsPowerOfTwo(fftLength))
            throw new ArgumentException("FFT length must be a power of two.", nameof(fftLength));
        if (bandCount < 1)
            throw new ArgumentOutOfRangeException(nameof(bandCount), "Band count must be at least 1.");
        if (minFrequency >= maxFrequency)
            throw new ArgumentException("Minimum frequency must be less than maximum frequency.");
        if (maxFrequency > sampleRate / 2)
            maxFrequency = sampleRate / 2f;

        _sampleRate = sampleRate;
        _fftLength = fftLength;
        _bandCount = bandCount;
        _minFrequency = minFrequency;
        _maxFrequency = maxFrequency;

        _fftBuffer = new Complex[fftLength];
        _sampleBuffer = new float[fftLength];
        _window = GenerateHannWindow(fftLength);
        _magnitudeAccumulator = new double[bandCount];
        _bandFrequencies = CalculateBandFrequencies(bandCount, minFrequency, maxFrequency);
    }

    /// <summary>
    /// Starts capturing the reference spectrum.
    /// Call ProcessSamples() to feed audio data, then call StopCapture() when done.
    /// </summary>
    public void StartCaptureReference()
    {
        lock (_lock)
        {
            ResetCapture();
            _isCapturing = true;
            _capturingReference = true;
        }
    }

    /// <summary>
    /// Starts capturing the target spectrum.
    /// Call ProcessSamples() to feed audio data, then call StopCapture() when done.
    /// </summary>
    public void StartCaptureTarget()
    {
        lock (_lock)
        {
            ResetCapture();
            _isCapturing = true;
            _capturingReference = false;
        }
    }

    /// <summary>
    /// Processes audio samples during capture.
    /// </summary>
    /// <param name="samples">Audio samples (mono or interleaved stereo).</param>
    /// <param name="count">Number of samples to process.</param>
    /// <param name="channels">Number of audio channels (1 = mono, 2 = stereo).</param>
    public void ProcessSamples(float[] samples, int count, int channels = 1)
    {
        lock (_lock)
        {
            if (!_isCapturing) return;

            for (int i = 0; i < count; i += channels)
            {
                // Mix to mono if stereo
                float sample = channels == 2 && i + 1 < count
                    ? (samples[i] + samples[i + 1]) * 0.5f
                    : samples[i];

                _sampleBuffer[_sampleCount++] = sample;

                if (_sampleCount >= _fftLength)
                {
                    PerformCaptureFft();
                    _sampleCount = 0;
                }
            }
        }
    }

    /// <summary>
    /// Stops the current capture and finalizes the spectrum profile.
    /// </summary>
    /// <returns>The captured spectrum profile.</returns>
    public SpectrumProfile StopCapture()
    {
        lock (_lock)
        {
            _isCapturing = false;

            var profile = FinalizeCapture();

            if (_capturingReference)
            {
                _referenceSpectrum = profile;
            }
            else
            {
                _targetSpectrum = profile;
            }

            _matchResultDirty = true;
            ResetCapture();

            CaptureCompleted?.Invoke(this, new SpectrumCaptureEventArgs(profile, _capturingReference, 1.0f));

            // Recalculate match if both spectrums are available
            if (_referenceSpectrum?.IsValid == true && _targetSpectrum?.IsValid == true)
            {
                var result = CalculateMatchInternal();
                _cachedMatchResult = result;
                _matchResultDirty = false;
                MatchUpdated?.Invoke(this, result);
            }

            return profile;
        }
    }

    /// <summary>
    /// Captures reference spectrum from a complete audio buffer.
    /// </summary>
    /// <param name="samples">Audio samples (mono).</param>
    /// <param name="name">Optional name for the profile.</param>
    /// <returns>The captured reference spectrum profile.</returns>
    public SpectrumProfile CaptureReference(float[] samples, string name = "Reference")
    {
        return CaptureFromBuffer(samples, name, isReference: true);
    }

    /// <summary>
    /// Captures target spectrum from a complete audio buffer.
    /// </summary>
    /// <param name="samples">Audio samples (mono).</param>
    /// <param name="name">Optional name for the profile.</param>
    /// <returns>The captured target spectrum profile.</returns>
    public SpectrumProfile CaptureTarget(float[] samples, string name = "Target")
    {
        return CaptureFromBuffer(samples, name, isReference: false);
    }

    /// <summary>
    /// Calculates the match result between reference and target spectrums.
    /// </summary>
    /// <returns>The spectrum match result with EQ correction curve.</returns>
    public SpectrumMatchResult CalculateMatch()
    {
        lock (_lock)
        {
            if (_referenceSpectrum?.IsValid != true || _targetSpectrum?.IsValid != true)
            {
                return new SpectrumMatchResult
                {
                    ReferenceSpectrum = _referenceSpectrum,
                    TargetSpectrum = _targetSpectrum
                };
            }

            var result = CalculateMatchInternal();
            _cachedMatchResult = result;
            _matchResultDirty = false;
            return result;
        }
    }

    /// <summary>
    /// Applies the match curve to an audio buffer using a simple filter bank.
    /// </summary>
    /// <param name="samples">Audio samples to process (modified in place).</param>
    /// <param name="count">Number of samples to process.</param>
    /// <remarks>
    /// This is a simplified time-domain implementation for demonstration.
    /// For production use, consider using a proper parametric EQ chain or FFT-based processing.
    /// </remarks>
    public void ApplyMatch(float[] samples, int count)
    {
        var matchResult = MatchCurve;
        if (matchResult?.MatchCurve == null || matchResult.MatchCurve.Length == 0)
            return;

        // Apply correction using overlap-add FFT processing
        ApplyMatchFft(samples, count, matchResult.MatchCurve);
    }

    /// <summary>
    /// Gets the EQ correction values as gain multipliers for use with external EQ.
    /// </summary>
    /// <returns>Array of (frequency, gain) tuples for parametric EQ bands.</returns>
    public (float frequency, float gainDb, float q)[] GetEqBands()
    {
        var matchResult = MatchCurve;
        if (matchResult?.MatchCurve == null || matchResult.MatchCurve.Length == 0)
            return Array.Empty<(float, float, float)>();

        var bands = new (float frequency, float gainDb, float q)[matchResult.MatchCurve.Length];
        for (int i = 0; i < matchResult.MatchCurve.Length; i++)
        {
            var point = matchResult.MatchCurve[i];
            bands[i] = (point.Frequency, point.GainDb, point.Q);
        }
        return bands;
    }

    /// <summary>
    /// Clears the reference spectrum.
    /// </summary>
    public void ClearReference()
    {
        lock (_lock)
        {
            _referenceSpectrum = null;
            _matchResultDirty = true;
            _cachedMatchResult = null;
        }
    }

    /// <summary>
    /// Clears the target spectrum.
    /// </summary>
    public void ClearTarget()
    {
        lock (_lock)
        {
            _targetSpectrum = null;
            _matchResultDirty = true;
            _cachedMatchResult = null;
        }
    }

    /// <summary>
    /// Clears both spectrums and the match result.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _referenceSpectrum = null;
            _targetSpectrum = null;
            _cachedMatchResult = null;
            _matchResultDirty = true;
            ResetCapture();
        }
    }

    /// <summary>
    /// Sets a pre-captured reference spectrum profile.
    /// </summary>
    /// <param name="profile">The spectrum profile to use as reference.</param>
    public void SetReferenceSpectrum(SpectrumProfile profile)
    {
        lock (_lock)
        {
            _referenceSpectrum = profile;
            _matchResultDirty = true;
        }
    }

    /// <summary>
    /// Sets a pre-captured target spectrum profile.
    /// </summary>
    /// <param name="profile">The spectrum profile to use as target.</param>
    public void SetTargetSpectrum(SpectrumProfile profile)
    {
        lock (_lock)
        {
            _targetSpectrum = profile;
            _matchResultDirty = true;
        }
    }

    /// <summary>
    /// Creates a spectrum matcher preset for mastering reference matching.
    /// </summary>
    public static SpectrumMatcher CreateMasteringPreset(int sampleRate = 44100)
    {
        return new SpectrumMatcher(
            sampleRate,
            bandCount: 64,
            fftLength: 8192,
            minFrequency: 20f,
            maxFrequency: 20000f)
        {
            MaxCorrectionDb = 6f,
            MinCorrectionDb = -6f,
            CorrectionStrength = 0.7f,
            SmoothingFactor = 0.5f
        };
    }

    /// <summary>
    /// Creates a spectrum matcher preset for vocal tone matching.
    /// </summary>
    public static SpectrumMatcher CreateVocalPreset(int sampleRate = 44100)
    {
        return new SpectrumMatcher(
            sampleRate,
            bandCount: 32,
            fftLength: 4096,
            minFrequency: 80f,
            maxFrequency: 12000f)
        {
            MaxCorrectionDb = 9f,
            MinCorrectionDb = -9f,
            CorrectionStrength = 0.8f,
            SmoothingFactor = 0.4f
        };
    }

    /// <summary>
    /// Creates a spectrum matcher preset for instrument tone matching.
    /// </summary>
    public static SpectrumMatcher CreateInstrumentPreset(int sampleRate = 44100)
    {
        return new SpectrumMatcher(
            sampleRate,
            bandCount: 48,
            fftLength: 4096,
            minFrequency: 40f,
            maxFrequency: 16000f)
        {
            MaxCorrectionDb = 12f,
            MinCorrectionDb = -12f,
            CorrectionStrength = 1.0f,
            SmoothingFactor = 0.3f
        };
    }

    private SpectrumProfile CaptureFromBuffer(float[] samples, string name, bool isReference)
    {
        lock (_lock)
        {
            ResetCapture();

            int hopSize = _fftLength / 2;
            int position = 0;
            int frameCount = 0;

            while (position + _fftLength <= samples.Length)
            {
                Array.Copy(samples, position, _sampleBuffer, 0, _fftLength);
                PerformCaptureFft();
                frameCount++;
                position += hopSize;

                // Report progress
                float progress = (float)position / samples.Length;
                var partialProfile = CreatePartialProfile(name);
                CaptureProgress?.Invoke(this, new SpectrumCaptureEventArgs(partialProfile, isReference, progress));
            }

            var profile = FinalizeCapture();
            profile.Name = name;

            if (isReference)
            {
                _referenceSpectrum = profile;
            }
            else
            {
                _targetSpectrum = profile;
            }

            _matchResultDirty = true;
            ResetCapture();

            CaptureCompleted?.Invoke(this, new SpectrumCaptureEventArgs(profile, isReference, 1.0f));

            // Recalculate match if both spectrums are available
            if (_referenceSpectrum?.IsValid == true && _targetSpectrum?.IsValid == true)
            {
                var result = CalculateMatchInternal();
                _cachedMatchResult = result;
                _matchResultDirty = false;
                MatchUpdated?.Invoke(this, result);
            }

            return profile;
        }
    }

    private void PerformCaptureFft()
    {
        // Apply window and copy to FFT buffer
        for (int i = 0; i < _fftLength; i++)
        {
            _fftBuffer[i].X = _sampleBuffer[i] * _window[i];
            _fftBuffer[i].Y = 0;
        }

        // Perform FFT
        int m = (int)Math.Log(_fftLength, 2.0);
        FastFourierTransform.FFT(true, m, _fftBuffer);

        // Accumulate band magnitudes
        CalculateBandMagnitudes();
        _captureFrameCount++;
    }

    private void CalculateBandMagnitudes()
    {
        float binResolution = (float)_sampleRate / _fftLength;
        int maxBin = _fftLength / 2;

        for (int band = 0; band < _bandCount; band++)
        {
            float lowFreq = band == 0 ? _minFrequency / 2 : (_bandFrequencies[band - 1] + _bandFrequencies[band]) / 2;
            float highFreq = band == _bandCount - 1
                ? Math.Min(_maxFrequency * 1.2f, _sampleRate / 2f)
                : (_bandFrequencies[band] + _bandFrequencies[band + 1]) / 2;

            int lowBin = Math.Max(1, (int)(lowFreq / binResolution));
            int highBin = Math.Min(maxBin - 1, (int)(highFreq / binResolution));

            if (lowBin > highBin)
                lowBin = highBin = Math.Max(1, (int)(_bandFrequencies[band] / binResolution));

            double sum = 0;
            int binCount = 0;

            for (int bin = lowBin; bin <= highBin; bin++)
            {
                double magnitude = Math.Sqrt(
                    _fftBuffer[bin].X * _fftBuffer[bin].X +
                    _fftBuffer[bin].Y * _fftBuffer[bin].Y);
                sum += magnitude * magnitude; // Sum power for RMS
                binCount++;
            }

            // RMS of the band
            double rmsMagnitude = binCount > 0 ? Math.Sqrt(sum / binCount) : 0;

            // Accumulate for averaging
            _magnitudeAccumulator[band] += rmsMagnitude;
        }
    }

    private SpectrumProfile FinalizeCapture()
    {
        var profile = new SpectrumProfile
        {
            Frequencies = (float[])_bandFrequencies.Clone(),
            MagnitudesDb = new float[_bandCount],
            SampleRate = _sampleRate,
            FrameCount = _captureFrameCount,
            CaptureTime = DateTime.UtcNow
        };

        if (_captureFrameCount > 0)
        {
            for (int i = 0; i < _bandCount; i++)
            {
                double avgMagnitude = _magnitudeAccumulator[i] / _captureFrameCount;
                profile.MagnitudesDb[i] = (float)(20 * Math.Log10(Math.Max(avgMagnitude, 1e-10)));
            }
        }

        return profile;
    }

    private SpectrumProfile CreatePartialProfile(string name)
    {
        var profile = new SpectrumProfile
        {
            Name = name,
            Frequencies = (float[])_bandFrequencies.Clone(),
            MagnitudesDb = new float[_bandCount],
            SampleRate = _sampleRate,
            FrameCount = _captureFrameCount
        };

        if (_captureFrameCount > 0)
        {
            for (int i = 0; i < _bandCount; i++)
            {
                double avgMagnitude = _magnitudeAccumulator[i] / _captureFrameCount;
                profile.MagnitudesDb[i] = (float)(20 * Math.Log10(Math.Max(avgMagnitude, 1e-10)));
            }
        }

        return profile;
    }

    private SpectrumMatchResult CalculateMatchInternal()
    {
        var result = new SpectrumMatchResult
        {
            ReferenceSpectrum = _referenceSpectrum,
            TargetSpectrum = _targetSpectrum,
            DifferenceDb = new float[_bandCount],
            MatchCurve = new EqCorrectionPoint[_bandCount]
        };

        if (_referenceSpectrum?.IsValid != true || _targetSpectrum?.IsValid != true)
            return result;

        float sumSquaredDiff = 0;
        float maxDiff = 0;
        float maxDiffFreq = 0;
        int significantCount = 0;

        for (int i = 0; i < _bandCount; i++)
        {
            float refMag = _referenceSpectrum.MagnitudesDb[i];
            float targetMag = _targetSpectrum.MagnitudesDb[i];

            // Difference: how much to add to target to match reference
            float diff = refMag - targetMag;
            result.DifferenceDb[i] = diff;

            // Apply correction strength and clamp
            float correction = diff * _correctionStrength;
            correction = Math.Clamp(correction, _minCorrectionDb, _maxCorrectionDb);

            // Calculate Q based on band spacing (wider Q for wider bands)
            float q = CalculateQForBand(i);

            result.MatchCurve[i] = new EqCorrectionPoint
            {
                Frequency = _bandFrequencies[i],
                GainDb = correction,
                Q = q
            };

            // Statistics
            sumSquaredDiff += diff * diff;

            if (Math.Abs(diff) > Math.Abs(maxDiff))
            {
                maxDiff = diff;
                maxDiffFreq = _bandFrequencies[i];
            }

            if (Math.Abs(diff) > 3.0f)
            {
                significantCount++;
            }
        }

        result.RmsDifferenceDb = MathF.Sqrt(sumSquaredDiff / _bandCount);
        result.MaxDifferenceDb = maxDiff;
        result.MaxDifferenceFrequency = maxDiffFreq;
        result.SignificantDifferenceBandCount = significantCount;

        // Calculate similarity (inverse of normalized RMS difference)
        // 0 dB RMS diff = 1.0 similarity, 20 dB RMS diff = 0.0 similarity
        result.Similarity = Math.Max(0, 1.0f - result.RmsDifferenceDb / 20f);

        return result;
    }

    private float CalculateQForBand(int bandIndex)
    {
        // Calculate Q based on octave spacing
        // For logarithmically spaced bands, Q relates to the number of bands per octave
        float bandsPerOctave = _bandCount / (MathF.Log2(_maxFrequency / _minFrequency));
        return bandsPerOctave * 0.5f; // Approximate Q for 50% overlap between bands
    }

    private void ApplyMatchFft(float[] samples, int count, EqCorrectionPoint[] curve)
    {
        // Simplified FFT-based EQ application using overlap-add
        int hopSize = _fftLength / 2;
        var fftBuffer = new Complex[_fftLength];
        var outputBuffer = new float[count + _fftLength];
        var windowedInput = new float[_fftLength];

        for (int position = 0; position + _fftLength <= count; position += hopSize)
        {
            // Window and copy input
            for (int i = 0; i < _fftLength; i++)
            {
                windowedInput[i] = samples[position + i] * _window[i];
                fftBuffer[i].X = windowedInput[i];
                fftBuffer[i].Y = 0;
            }

            // Forward FFT
            int m = (int)Math.Log(_fftLength, 2.0);
            FastFourierTransform.FFT(true, m, fftBuffer);

            // Apply EQ curve in frequency domain
            float binResolution = (float)_sampleRate / _fftLength;
            for (int bin = 1; bin < _fftLength / 2; bin++)
            {
                float frequency = bin * binResolution;
                float gain = GetInterpolatedGain(curve, frequency);
                float linearGain = MathF.Pow(10, gain / 20f);

                fftBuffer[bin].X *= linearGain;
                fftBuffer[bin].Y *= linearGain;

                // Mirror for negative frequencies
                int mirrorBin = _fftLength - bin;
                fftBuffer[mirrorBin].X *= linearGain;
                fftBuffer[mirrorBin].Y *= linearGain;
            }

            // Inverse FFT
            FastFourierTransform.FFT(false, m, fftBuffer);

            // Overlap-add
            for (int i = 0; i < _fftLength; i++)
            {
                outputBuffer[position + i] += fftBuffer[i].X * _window[i] / _fftLength;
            }
        }

        // Copy result back to input
        int copyCount = Math.Min(count, outputBuffer.Length);
        for (int i = 0; i < copyCount; i++)
        {
            samples[i] = outputBuffer[i];
        }
    }

    private static float GetInterpolatedGain(EqCorrectionPoint[] curve, float frequency)
    {
        if (curve.Length == 0) return 0;

        // Handle edge cases
        if (frequency <= curve[0].Frequency)
            return curve[0].GainDb;
        if (frequency >= curve[^1].Frequency)
            return curve[^1].GainDb;

        // Find surrounding points and interpolate
        for (int i = 0; i < curve.Length - 1; i++)
        {
            if (frequency >= curve[i].Frequency && frequency <= curve[i + 1].Frequency)
            {
                // Logarithmic interpolation for frequency
                float logFreq = MathF.Log10(frequency);
                float logFreq1 = MathF.Log10(curve[i].Frequency);
                float logFreq2 = MathF.Log10(curve[i + 1].Frequency);
                float t = (logFreq - logFreq1) / (logFreq2 - logFreq1);

                return curve[i].GainDb + t * (curve[i + 1].GainDb - curve[i].GainDb);
            }
        }

        return 0;
    }

    private void ResetCapture()
    {
        _sampleCount = 0;
        _captureFrameCount = 0;
        Array.Clear(_sampleBuffer);
        Array.Clear(_fftBuffer);
        Array.Clear(_magnitudeAccumulator);
    }

    private static float[] CalculateBandFrequencies(int bandCount, float minFreq, float maxFreq)
    {
        float[] frequencies = new float[bandCount];
        float logMin = MathF.Log10(minFreq);
        float logMax = MathF.Log10(maxFreq);
        float logStep = (logMax - logMin) / (bandCount - 1);

        for (int i = 0; i < bandCount; i++)
        {
            frequencies[i] = MathF.Pow(10, logMin + i * logStep);
        }

        return frequencies;
    }

    private static float[] GenerateHannWindow(int length)
    {
        float[] window = new float[length];
        for (int i = 0; i < length; i++)
        {
            window[i] = (float)(0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (length - 1))));
        }
        return window;
    }

    private static bool IsPowerOfTwo(int n) => n > 0 && (n & (n - 1)) == 0;
}
