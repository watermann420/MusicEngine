//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Musical key detection using Krumhansl-Schmuckler algorithm with chromagram analysis.

using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.Dsp;

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Musical mode (Major or Minor).
/// </summary>
public enum KeyMode
{
    /// <summary>Major key (Ionian mode).</summary>
    Major,

    /// <summary>Minor key (Aeolian/Natural Minor mode).</summary>
    Minor
}

/// <summary>
/// Key profile types for the Krumhansl-Schmuckler algorithm.
/// Different profiles have been empirically derived from various research.
/// </summary>
public enum KeyProfileType
{
    /// <summary>Original Krumhansl-Kessler profiles (1982).</summary>
    Krumhansl,

    /// <summary>Temperley-Kostka-Payne profiles.</summary>
    Temperley,

    /// <summary>Albrecht-Shanahan profiles (2013).</summary>
    AlbrechtShanahan,

    /// <summary>Simple binary profiles (scale tones only).</summary>
    Simple
}

/// <summary>
/// Provides key profile weights for different pitch classes in major and minor keys.
/// These profiles are used in the Krumhansl-Schmuckler key-finding algorithm.
/// </summary>
public static class KeyProfiles
{
    /// <summary>
    /// Gets the major key profile for the specified profile type.
    /// Index 0 = tonic (root), index 1 = minor second, etc.
    /// </summary>
    public static double[] GetMajorProfile(KeyProfileType profileType)
    {
        return profileType switch
        {
            KeyProfileType.Krumhansl => KrumhanslMajor,
            KeyProfileType.Temperley => TemperleyMajor,
            KeyProfileType.AlbrechtShanahan => AlbrechtShanahanMajor,
            KeyProfileType.Simple => SimpleMajor,
            _ => KrumhanslMajor
        };
    }

    /// <summary>
    /// Gets the minor key profile for the specified profile type.
    /// Index 0 = tonic (root), index 1 = minor second, etc.
    /// </summary>
    public static double[] GetMinorProfile(KeyProfileType profileType)
    {
        return profileType switch
        {
            KeyProfileType.Krumhansl => KrumhanslMinor,
            KeyProfileType.Temperley => TemperleyMinor,
            KeyProfileType.AlbrechtShanahan => AlbrechtShanahanMinor,
            KeyProfileType.Simple => SimpleMinor,
            _ => KrumhanslMinor
        };
    }

    // Krumhansl-Kessler profiles (1982) - derived from probe tone experiments
    private static readonly double[] KrumhanslMajor =
    {
        6.35, 2.23, 3.48, 2.33, 4.38, 4.09,  // C, C#, D, D#, E, F
        2.52, 5.19, 2.39, 3.66, 2.29, 2.88   // F#, G, G#, A, A#, B
    };

    private static readonly double[] KrumhanslMinor =
    {
        6.33, 2.68, 3.52, 5.38, 2.60, 3.53,  // C, C#, D, D#, E, F
        2.54, 4.75, 3.98, 2.69, 3.34, 3.17   // F#, G, G#, A, A#, B
    };

    // Temperley-Kostka-Payne profiles - derived from corpus analysis
    private static readonly double[] TemperleyMajor =
    {
        0.748, 0.060, 0.488, 0.082, 0.670, 0.460,  // C, C#, D, D#, E, F
        0.096, 0.715, 0.104, 0.366, 0.057, 0.400   // F#, G, G#, A, A#, B
    };

    private static readonly double[] TemperleyMinor =
    {
        0.712, 0.084, 0.474, 0.618, 0.049, 0.460,  // C, C#, D, D#, E, F
        0.105, 0.747, 0.404, 0.067, 0.400, 0.110   // F#, G, G#, A, A#, B
    };

    // Albrecht-Shanahan profiles (2013) - more recent corpus analysis
    private static readonly double[] AlbrechtShanahanMajor =
    {
        0.238, 0.006, 0.111, 0.006, 0.137, 0.094,  // C, C#, D, D#, E, F
        0.016, 0.214, 0.009, 0.080, 0.008, 0.081   // F#, G, G#, A, A#, B
    };

    private static readonly double[] AlbrechtShanahanMinor =
    {
        0.220, 0.006, 0.104, 0.123, 0.019, 0.103,  // C, C#, D, D#, E, F
        0.012, 0.214, 0.062, 0.022, 0.061, 0.052   // F#, G, G#, A, A#, B
    };

    // Simple binary profiles (1 for scale tones, 0.1 for non-scale tones)
    private static readonly double[] SimpleMajor =
    {
        1.0, 0.1, 1.0, 0.1, 1.0, 1.0,  // C, C#, D, D#, E, F
        0.1, 1.0, 0.1, 1.0, 0.1, 1.0   // F#, G, G#, A, A#, B
    };

    private static readonly double[] SimpleMinor =
    {
        1.0, 0.1, 1.0, 1.0, 0.1, 1.0,  // C, C#, D, Eb, E, F
        0.1, 1.0, 1.0, 0.1, 1.0, 0.1   // F#, G, Ab, A, Bb, B
    };
}

/// <summary>
/// Represents a detected musical key with confidence information.
/// </summary>
public class KeyDetectionResult
{
    /// <summary>
    /// Gets the detected root note (pitch class 0-11, where 0=C).
    /// </summary>
    public int Root { get; init; }

    /// <summary>
    /// Gets the detected root note as a NoteName enum.
    /// </summary>
    public NoteName RootNote => (NoteName)Root;

    /// <summary>
    /// Gets the detected mode (Major or Minor).
    /// </summary>
    public KeyMode Mode { get; init; }

    /// <summary>
    /// Gets the confidence level of the detection (0.0 to 1.0).
    /// Higher values indicate more certainty in the detected key.
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Gets the correlation coefficient for the best-matching key profile.
    /// </summary>
    public double Correlation { get; init; }

    /// <summary>
    /// Gets the Camelot wheel notation (e.g., "8A" for A Minor, "8B" for A Major).
    /// Used by DJs for harmonic mixing.
    /// </summary>
    public string CamelotNotation => GetCamelotNotation();

    /// <summary>
    /// Gets the Open Key notation (alternative to Camelot, e.g., "1m" for C Minor).
    /// </summary>
    public string OpenKeyNotation => GetOpenKeyNotation();

    /// <summary>
    /// Gets the key name as a formatted string (e.g., "C Major", "A Minor").
    /// </summary>
    public string KeyName => $"{GetNoteName(Root)} {Mode}";

    /// <summary>
    /// Gets the chromagram (pitch class distribution) used for detection.
    /// </summary>
    public double[]? Chromagram { get; init; }

    /// <summary>
    /// Gets all correlation values for each possible key (24 keys total).
    /// Useful for analyzing alternative key candidates.
    /// </summary>
    public KeyCorrelation[]? AllCorrelations { get; init; }

    /// <summary>
    /// Gets suggested related keys that are harmonically compatible.
    /// </summary>
    public RelatedKey[] RelatedKeys => GetRelatedKeys();

    private string GetCamelotNotation()
    {
        // Camelot wheel: Major keys are "B", Minor keys are "A"
        // The wheel starts at C Major = 8B, and moves in fifths
        // Circle of fifths order for the wheel positions
        int[] camelotNumbers = { 8, 3, 10, 5, 12, 7, 2, 9, 4, 11, 6, 1 }; // C, C#, D, D#, E, F, F#, G, G#, A, A#, B
        int number = camelotNumbers[Root];
        string letter = Mode == KeyMode.Major ? "B" : "A";
        return $"{number}{letter}";
    }

    private string GetOpenKeyNotation()
    {
        // Open Key notation: similar concept, different numbering
        // Major keys end with "d" (dur), Minor keys end with "m" (moll)
        int[] openKeyNumbers = { 1, 8, 3, 10, 5, 12, 7, 2, 9, 4, 11, 6 }; // C, C#, D, D#, E, F, F#, G, G#, A, A#, B (for minor)
        int number = openKeyNumbers[Root];
        string suffix = Mode == KeyMode.Major ? "d" : "m";
        return $"{number}{suffix}";
    }

    private RelatedKey[] GetRelatedKeys()
    {
        var related = new List<RelatedKey>();

        // Relative major/minor (same key signature)
        int relativeRoot = Mode == KeyMode.Major ? (Root + 9) % 12 : (Root + 3) % 12;
        KeyMode relativeMode = Mode == KeyMode.Major ? KeyMode.Minor : KeyMode.Major;
        related.Add(new RelatedKey
        {
            Root = relativeRoot,
            Mode = relativeMode,
            Relationship = "Relative",
            Description = Mode == KeyMode.Major ? "Relative Minor" : "Relative Major"
        });

        // Parallel major/minor (same root, different mode)
        related.Add(new RelatedKey
        {
            Root = Root,
            Mode = Mode == KeyMode.Major ? KeyMode.Minor : KeyMode.Major,
            Relationship = "Parallel",
            Description = Mode == KeyMode.Major ? "Parallel Minor" : "Parallel Major"
        });

        // Dominant (fifth above)
        int dominantRoot = (Root + 7) % 12;
        related.Add(new RelatedKey
        {
            Root = dominantRoot,
            Mode = Mode,
            Relationship = "Dominant",
            Description = "Fifth Above"
        });

        // Subdominant (fourth above / fifth below)
        int subdominantRoot = (Root + 5) % 12;
        related.Add(new RelatedKey
        {
            Root = subdominantRoot,
            Mode = Mode,
            Relationship = "Subdominant",
            Description = "Fourth Above"
        });

        // Camelot wheel neighbors (for DJ mixing)
        // +1 semitone (energy boost)
        related.Add(new RelatedKey
        {
            Root = (Root + 1) % 12,
            Mode = Mode,
            Relationship = "Semitone Up",
            Description = "Energy Boost (+1)"
        });

        // -1 semitone (energy drop)
        related.Add(new RelatedKey
        {
            Root = (Root + 11) % 12,
            Mode = Mode,
            Relationship = "Semitone Down",
            Description = "Energy Drop (-1)"
        });

        return related.ToArray();
    }

    private static string GetNoteName(int pitchClass)
    {
        return pitchClass switch
        {
            0 => "C",
            1 => "C#",
            2 => "D",
            3 => "D#",
            4 => "E",
            5 => "F",
            6 => "F#",
            7 => "G",
            8 => "G#",
            9 => "A",
            10 => "A#",
            11 => "B",
            _ => "?"
        };
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{KeyName} (Confidence: {Confidence:P0}, Camelot: {CamelotNotation})";
    }
}

/// <summary>
/// Represents a correlation value for a specific key.
/// </summary>
public class KeyCorrelation
{
    /// <summary>Gets the root pitch class (0-11).</summary>
    public int Root { get; init; }

    /// <summary>Gets the mode (Major or Minor).</summary>
    public KeyMode Mode { get; init; }

    /// <summary>Gets the correlation coefficient.</summary>
    public double Correlation { get; init; }

    /// <summary>Gets the key name.</summary>
    public string KeyName => $"{GetNoteName(Root)} {Mode}";

    private static string GetNoteName(int pitchClass)
    {
        string[] names = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        return names[pitchClass % 12];
    }
}

/// <summary>
/// Represents a related key suggestion.
/// </summary>
public class RelatedKey
{
    /// <summary>Gets the root pitch class (0-11).</summary>
    public int Root { get; init; }

    /// <summary>Gets the mode (Major or Minor).</summary>
    public KeyMode Mode { get; init; }

    /// <summary>Gets the relationship type (e.g., "Relative", "Parallel", "Dominant").</summary>
    public string Relationship { get; init; } = string.Empty;

    /// <summary>Gets a description of the relationship.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Gets the key name.</summary>
    public string KeyName => $"{GetNoteName(Root)} {Mode}";

    private static string GetNoteName(int pitchClass)
    {
        string[] names = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        return names[pitchClass % 12];
    }
}

/// <summary>
/// Represents a key change detected during temporal analysis.
/// </summary>
public class KeyChangePoint
{
    /// <summary>Gets the time position in seconds where the key change occurs.</summary>
    public double TimeSeconds { get; init; }

    /// <summary>Gets the sample position where the key change occurs.</summary>
    public long SamplePosition { get; init; }

    /// <summary>Gets the detected key at this point.</summary>
    public KeyDetectionResult Key { get; init; } = null!;
}

/// <summary>
/// Event arguments for key detection updates during real-time analysis.
/// </summary>
public class KeyDetectionEventArgs : EventArgs
{
    /// <summary>Gets the detected key result.</summary>
    public KeyDetectionResult Result { get; }

    /// <summary>
    /// Creates new key detection event arguments.
    /// </summary>
    public KeyDetectionEventArgs(KeyDetectionResult result)
    {
        Result = result;
    }
}

/// <summary>
/// Musical key detector using the Krumhansl-Schmuckler algorithm.
/// Analyzes audio to determine the most likely musical key (e.g., "C Major", "A Minor").
/// </summary>
/// <remarks>
/// The algorithm works by:
/// 1. Building a chromagram (pitch class histogram) from audio using FFT
/// 2. Normalizing the chromagram
/// 3. Correlating with empirically-derived key profiles
/// 4. Selecting the key with the highest correlation as the detected key
/// </remarks>
public class KeyDetector
{
    private readonly int _sampleRate;
    private readonly int _fftLength;
    private readonly KeyProfileType _profileType;
    private readonly double[] _majorProfile;
    private readonly double[] _minorProfile;
    private readonly Complex[] _fftBuffer;
    private readonly float[] _sampleBuffer;
    private readonly double[] _chromagram;
    private readonly double[] _accumulatedChromagram;
    private int _sampleCount;
    private int _frameCount;
    private readonly object _lock = new();

    // Reference frequencies for pitch classes (A4 = 440 Hz standard)
    private readonly double[] _pitchClassFrequencies;

    // Detection state
    private KeyDetectionResult? _currentResult;

    /// <summary>
    /// Gets the current detected key result, or null if no detection has been performed.
    /// </summary>
    public KeyDetectionResult? CurrentResult
    {
        get
        {
            lock (_lock)
            {
                return _currentResult;
            }
        }
    }

    /// <summary>
    /// Gets the sample rate used for analysis.
    /// </summary>
    public int SampleRate => _sampleRate;

    /// <summary>
    /// Gets the FFT length used for analysis.
    /// </summary>
    public int FftLength => _fftLength;

    /// <summary>
    /// Gets the key profile type used for correlation.
    /// </summary>
    public KeyProfileType ProfileType => _profileType;

    /// <summary>
    /// Event raised when a key is detected or updated during real-time analysis.
    /// </summary>
    public event EventHandler<KeyDetectionEventArgs>? KeyDetected;

    /// <summary>
    /// Creates a new key detector with the specified configuration.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate in Hz (default: 44100).</param>
    /// <param name="fftLength">FFT window size, must be power of 2 (default: 8192 for better frequency resolution).</param>
    /// <param name="profileType">Key profile type to use for correlation (default: Krumhansl).</param>
    public KeyDetector(
        int sampleRate = 44100,
        int fftLength = 8192,
        KeyProfileType profileType = KeyProfileType.Krumhansl)
    {
        if (!IsPowerOfTwo(fftLength))
            throw new ArgumentException("FFT length must be a power of two.", nameof(fftLength));
        if (sampleRate < 8000 || sampleRate > 192000)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "Sample rate must be between 8000 and 192000 Hz.");

        _sampleRate = sampleRate;
        _fftLength = fftLength;
        _profileType = profileType;

        _majorProfile = KeyProfiles.GetMajorProfile(profileType);
        _minorProfile = KeyProfiles.GetMinorProfile(profileType);

        _fftBuffer = new Complex[fftLength];
        _sampleBuffer = new float[fftLength];
        _chromagram = new double[12];
        _accumulatedChromagram = new double[12];

        // Pre-calculate pitch class reference frequencies
        _pitchClassFrequencies = CalculatePitchClassFrequencies();
    }

    /// <summary>
    /// Processes audio samples for real-time key detection.
    /// Call this continuously with incoming audio for streaming analysis.
    /// </summary>
    /// <param name="samples">Audio samples (mono or interleaved - first channel used).</param>
    /// <param name="count">Number of samples to process.</param>
    /// <param name="channels">Number of audio channels (default: 1 for mono).</param>
    public void ProcessSamples(float[] samples, int count, int channels = 1)
    {
        for (int i = 0; i < count; i += channels)
        {
            _sampleBuffer[_sampleCount] = samples[i];
            _sampleCount++;

            if (_sampleCount >= _fftLength)
            {
                ProcessFrame();
                _sampleCount = 0;
            }
        }
    }

    /// <summary>
    /// Analyzes a complete audio buffer and returns the detected key.
    /// This is the preferred method for offline (non-real-time) analysis.
    /// </summary>
    /// <param name="samples">Complete audio buffer (mono).</param>
    /// <param name="sampleRate">Sample rate of the audio (uses detector's sample rate if 0).</param>
    /// <returns>Key detection result with confidence information.</returns>
    public KeyDetectionResult AnalyzeBuffer(float[] samples, int sampleRate = 0)
    {
        if (samples == null || samples.Length == 0)
            throw new ArgumentException("Samples cannot be null or empty.", nameof(samples));

        if (sampleRate == 0)
            sampleRate = _sampleRate;

        // Reset state for fresh analysis
        Reset();

        // Process entire buffer
        int hopSize = _fftLength / 2; // 50% overlap
        int position = 0;

        while (position + _fftLength <= samples.Length)
        {
            // Copy frame to buffer
            Array.Copy(samples, position, _sampleBuffer, 0, _fftLength);
            ProcessFrame();
            position += hopSize;
        }

        // Ensure we have at least one frame even for short buffers
        if (_frameCount == 0 && samples.Length > 0)
        {
            Array.Clear(_sampleBuffer, 0, _sampleBuffer.Length);
            int copyLength = Math.Min(samples.Length, _fftLength);
            Array.Copy(samples, 0, _sampleBuffer, 0, copyLength);
            ProcessFrame();
        }

        return _currentResult ?? CreateEmptyResult();
    }

    /// <summary>
    /// Analyzes audio for key changes over time, detecting multiple keys in sections.
    /// Useful for songs with modulations or key changes.
    /// </summary>
    /// <param name="samples">Complete audio buffer (mono).</param>
    /// <param name="windowSeconds">Analysis window size in seconds (default: 10).</param>
    /// <param name="hopSeconds">Hop size between windows in seconds (default: 5).</param>
    /// <returns>List of key change points detected throughout the audio.</returns>
    public List<KeyChangePoint> DetectKeyChanges(float[] samples, double windowSeconds = 10.0, double hopSeconds = 5.0)
    {
        if (samples == null || samples.Length == 0)
            throw new ArgumentException("Samples cannot be null or empty.", nameof(samples));

        var keyChanges = new List<KeyChangePoint>();
        int windowSamples = (int)(windowSeconds * _sampleRate);
        int hopSamples = (int)(hopSeconds * _sampleRate);

        KeyDetectionResult? previousKey = null;
        int position = 0;

        while (position + windowSamples <= samples.Length)
        {
            // Extract window
            float[] window = new float[windowSamples];
            Array.Copy(samples, position, window, 0, windowSamples);

            // Analyze window
            var windowDetector = new KeyDetector(_sampleRate, _fftLength, _profileType);
            var result = windowDetector.AnalyzeBuffer(window);

            // Check for key change
            if (previousKey == null ||
                result.Root != previousKey.Root ||
                result.Mode != previousKey.Mode)
            {
                // Only add if confidence is reasonable
                if (result.Confidence > 0.3)
                {
                    keyChanges.Add(new KeyChangePoint
                    {
                        TimeSeconds = (double)position / _sampleRate,
                        SamplePosition = position,
                        Key = result
                    });
                    previousKey = result;
                }
            }

            position += hopSamples;
        }

        // Handle edge case: no key changes detected, add the overall key
        if (keyChanges.Count == 0)
        {
            var overallResult = AnalyzeBuffer(samples);
            keyChanges.Add(new KeyChangePoint
            {
                TimeSeconds = 0,
                SamplePosition = 0,
                Key = overallResult
            });
        }

        return keyChanges;
    }

    /// <summary>
    /// Resets the detector state for a new analysis.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _sampleCount = 0;
            _frameCount = 0;
            Array.Clear(_sampleBuffer, 0, _sampleBuffer.Length);
            Array.Clear(_fftBuffer, 0, _fftBuffer.Length);
            Array.Clear(_chromagram, 0, _chromagram.Length);
            Array.Clear(_accumulatedChromagram, 0, _accumulatedChromagram.Length);
            _currentResult = null;
        }
    }

    /// <summary>
    /// Gets the current accumulated chromagram (pitch class distribution).
    /// </summary>
    /// <returns>Array of 12 values representing the strength of each pitch class.</returns>
    public double[] GetChromagram()
    {
        lock (_lock)
        {
            double[] result = new double[12];
            if (_frameCount > 0)
            {
                for (int i = 0; i < 12; i++)
                {
                    result[i] = _accumulatedChromagram[i] / _frameCount;
                }
            }
            return result;
        }
    }

    /// <summary>
    /// Converts a NoteName enum to the equivalent pitch class integer (0-11).
    /// </summary>
    public static int NoteNameToPitchClass(NoteName note)
    {
        return (int)note;
    }

    /// <summary>
    /// Converts a pitch class integer (0-11) to the equivalent NoteName enum.
    /// </summary>
    public static NoteName PitchClassToNoteName(int pitchClass)
    {
        return (NoteName)(pitchClass % 12);
    }

    private void ProcessFrame()
    {
        // Apply Hann window and copy to FFT buffer
        for (int i = 0; i < _fftLength; i++)
        {
            double window = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (_fftLength - 1)));
            _fftBuffer[i].X = (float)(_sampleBuffer[i] * window);
            _fftBuffer[i].Y = 0;
        }

        // Perform FFT
        int m = (int)Math.Log(_fftLength, 2.0);
        FastFourierTransform.FFT(true, m, _fftBuffer);

        // Build chromagram from FFT magnitudes
        BuildChromagram();

        // Accumulate chromagram
        lock (_lock)
        {
            for (int i = 0; i < 12; i++)
            {
                _accumulatedChromagram[i] += _chromagram[i];
            }
            _frameCount++;

            // Perform key detection
            DetectKey();
        }
    }

    private void BuildChromagram()
    {
        // Clear chromagram
        Array.Clear(_chromagram, 0, 12);

        double binResolution = (double)_sampleRate / _fftLength;
        int maxBin = _fftLength / 2;

        // For each pitch class, accumulate energy from all octaves
        for (int pitchClass = 0; pitchClass < 12; pitchClass++)
        {
            double totalEnergy = 0;

            // Sum energy across multiple octaves (C1 to C8 approximately)
            for (int octave = 1; octave <= 8; octave++)
            {
                double targetFreq = _pitchClassFrequencies[pitchClass] * Math.Pow(2, octave - 4);

                // Skip frequencies outside our analysis range
                if (targetFreq < 20 || targetFreq > _sampleRate / 2)
                    continue;

                // Find the bin corresponding to this frequency
                int centerBin = (int)(targetFreq / binResolution);
                if (centerBin <= 0 || centerBin >= maxBin)
                    continue;

                // Use a small window around the center bin to capture energy
                // This helps with frequency smearing from the FFT
                int windowBins = Math.Max(1, (int)(targetFreq * 0.03 / binResolution)); // ~3% bandwidth

                for (int bin = Math.Max(1, centerBin - windowBins);
                     bin <= Math.Min(maxBin - 1, centerBin + windowBins);
                     bin++)
                {
                    double magnitude = Math.Sqrt(
                        _fftBuffer[bin].X * _fftBuffer[bin].X +
                        _fftBuffer[bin].Y * _fftBuffer[bin].Y);

                    // Weight by proximity to center frequency (triangular window)
                    double binFreq = bin * binResolution;
                    double weight = 1.0 - Math.Abs(binFreq - targetFreq) / (windowBins * binResolution + 1);
                    weight = Math.Max(0, weight);

                    totalEnergy += magnitude * weight;
                }
            }

            _chromagram[pitchClass] = totalEnergy;
        }

        // Normalize chromagram
        double maxEnergy = _chromagram.Max();
        if (maxEnergy > 1e-10)
        {
            for (int i = 0; i < 12; i++)
            {
                _chromagram[i] /= maxEnergy;
            }
        }
    }

    private void DetectKey()
    {
        // Get normalized accumulated chromagram
        double[] normalizedChroma = new double[12];
        double total = _accumulatedChromagram.Sum();

        if (total < 1e-10)
        {
            _currentResult = CreateEmptyResult();
            return;
        }

        for (int i = 0; i < 12; i++)
        {
            normalizedChroma[i] = _accumulatedChromagram[i] / total;
        }

        // Correlate with all 24 possible keys (12 major + 12 minor)
        var correlations = new List<KeyCorrelation>();
        double bestCorrelation = double.MinValue;
        int bestRoot = 0;
        KeyMode bestMode = KeyMode.Major;

        for (int root = 0; root < 12; root++)
        {
            // Rotate the key profiles to match the current root
            double majorCorr = CalculateCorrelation(normalizedChroma, _majorProfile, root);
            double minorCorr = CalculateCorrelation(normalizedChroma, _minorProfile, root);

            correlations.Add(new KeyCorrelation
            {
                Root = root,
                Mode = KeyMode.Major,
                Correlation = majorCorr
            });

            correlations.Add(new KeyCorrelation
            {
                Root = root,
                Mode = KeyMode.Minor,
                Correlation = minorCorr
            });

            if (majorCorr > bestCorrelation)
            {
                bestCorrelation = majorCorr;
                bestRoot = root;
                bestMode = KeyMode.Major;
            }

            if (minorCorr > bestCorrelation)
            {
                bestCorrelation = minorCorr;
                bestRoot = root;
                bestMode = KeyMode.Minor;
            }
        }

        // Calculate confidence based on how much the best key stands out
        var sortedCorrelations = correlations.OrderByDescending(c => c.Correlation).ToArray();
        double secondBest = sortedCorrelations.Length > 1 ? sortedCorrelations[1].Correlation : 0;

        // Confidence is based on the gap between best and second-best
        // and the absolute strength of the correlation
        double correlationGap = bestCorrelation - secondBest;
        double confidence = Math.Clamp(
            (correlationGap * 2.0 + (bestCorrelation + 1.0) / 2.0) / 2.0,
            0.0, 1.0);

        _currentResult = new KeyDetectionResult
        {
            Root = bestRoot,
            Mode = bestMode,
            Confidence = confidence,
            Correlation = bestCorrelation,
            Chromagram = (double[])normalizedChroma.Clone(),
            AllCorrelations = sortedCorrelations
        };

        // Raise event
        KeyDetected?.Invoke(this, new KeyDetectionEventArgs(_currentResult));
    }

    private double CalculateCorrelation(double[] chromagram, double[] profile, int root)
    {
        // Pearson correlation coefficient between rotated chromagram and profile
        double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0, sumY2 = 0;
        int n = 12;

        for (int i = 0; i < n; i++)
        {
            // Rotate chromagram by root to align with profile (profile is always C-based)
            double x = chromagram[(i + root) % 12];
            double y = profile[i];

            sumX += x;
            sumY += y;
            sumXY += x * y;
            sumX2 += x * x;
            sumY2 += y * y;
        }

        double meanX = sumX / n;
        double meanY = sumY / n;

        double numerator = sumXY - n * meanX * meanY;
        double denominator = Math.Sqrt((sumX2 - n * meanX * meanX) * (sumY2 - n * meanY * meanY));

        if (denominator < 1e-10)
            return 0;

        return numerator / denominator;
    }

    private double[] CalculatePitchClassFrequencies()
    {
        // Reference: A4 = 440 Hz, A is pitch class 9
        // Calculate frequencies for pitch classes in octave 4 (middle octave)
        double a4 = 440.0;
        double[] frequencies = new double[12];

        for (int i = 0; i < 12; i++)
        {
            // Distance in semitones from A4
            int semitones = i - 9; // A is pitch class 9
            frequencies[i] = a4 * Math.Pow(2.0, semitones / 12.0);
        }

        return frequencies;
    }

    private KeyDetectionResult CreateEmptyResult()
    {
        return new KeyDetectionResult
        {
            Root = 0,
            Mode = KeyMode.Major,
            Confidence = 0,
            Correlation = 0,
            Chromagram = new double[12],
            AllCorrelations = Array.Empty<KeyCorrelation>()
        };
    }

    private static bool IsPowerOfTwo(int n) => n > 0 && (n & (n - 1)) == 0;
}
