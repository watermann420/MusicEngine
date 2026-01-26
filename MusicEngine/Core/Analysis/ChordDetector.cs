// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Chord recognition.

using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.Dsp;
using NAudio.Wave;

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Represents a chord template used for chord detection via template matching.
/// Each template defines which pitch classes are present in a chord type.
/// </summary>
public class ChordTemplate
{
    /// <summary>
    /// Gets the chord type this template represents.
    /// </summary>
    public ChordType ChordType { get; }

    /// <summary>
    /// Gets the display name of this chord template.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the pitch class profile (12-element array, one per semitone from C).
    /// Values represent the expected relative energy for each pitch class (0.0 to 1.0).
    /// </summary>
    public float[] Profile { get; }

    /// <summary>
    /// Gets the chord symbol suffix (e.g., "", "m", "dim", "7").
    /// </summary>
    public string Symbol { get; }

    /// <summary>
    /// Creates a new chord template.
    /// </summary>
    /// <param name="chordType">The chord type.</param>
    /// <param name="name">Display name.</param>
    /// <param name="symbol">Chord symbol suffix.</param>
    /// <param name="intervals">Semitone intervals from root (e.g., 0, 4, 7 for major).</param>
    public ChordTemplate(ChordType chordType, string name, string symbol, params int[] intervals)
    {
        ChordType = chordType;
        Name = name;
        Symbol = symbol;
        Profile = CreateProfile(intervals);
    }

    /// <summary>
    /// Creates a chord template with custom profile weights.
    /// </summary>
    /// <param name="chordType">The chord type.</param>
    /// <param name="name">Display name.</param>
    /// <param name="symbol">Chord symbol suffix.</param>
    /// <param name="profile">12-element pitch class profile.</param>
    public ChordTemplate(ChordType chordType, string name, string symbol, float[] profile)
    {
        if (profile.Length != 12)
            throw new ArgumentException("Profile must have exactly 12 elements.", nameof(profile));

        ChordType = chordType;
        Name = name;
        Symbol = symbol;
        Profile = (float[])profile.Clone();
    }

    private static float[] CreateProfile(int[] intervals)
    {
        float[] profile = new float[12];
        float weight = 1.0f;

        foreach (int interval in intervals)
        {
            int pitchClass = interval % 12;
            profile[pitchClass] = weight;
            weight *= 0.9f; // Slightly reduce weight for higher chord tones
        }

        // Normalize the profile
        float sum = profile.Sum();
        if (sum > 0)
        {
            for (int i = 0; i < 12; i++)
            {
                profile[i] /= sum;
            }
        }

        return profile;
    }

    /// <summary>
    /// Gets a rotated version of this template for a specific root note.
    /// </summary>
    /// <param name="rootPitchClass">Root pitch class (0 = C, 1 = C#, etc.).</param>
    /// <returns>Rotated profile array.</returns>
    public float[] GetProfileForRoot(int rootPitchClass)
    {
        float[] rotated = new float[12];
        for (int i = 0; i < 12; i++)
        {
            int targetIndex = (i + rootPitchClass) % 12;
            rotated[targetIndex] = Profile[i];
        }
        return rotated;
    }
}

/// <summary>
/// Represents a detected chord with root note, type, and confidence.
/// </summary>
public class DetectedChord
{
    /// <summary>
    /// Gets the root note as a pitch class (0 = C, 1 = C#, ..., 11 = B).
    /// </summary>
    public int RootPitchClass { get; }

    /// <summary>
    /// Gets the root note name.
    /// </summary>
    public NoteName RootNote => (NoteName)RootPitchClass;

    /// <summary>
    /// Gets the detected chord type.
    /// </summary>
    public ChordType ChordType { get; }

    /// <summary>
    /// Gets the confidence level of the detection (0.0 to 1.0).
    /// </summary>
    public float Confidence { get; }

    /// <summary>
    /// Gets the bass note pitch class (may differ from root for inversions).
    /// </summary>
    public int BassPitchClass { get; }

    /// <summary>
    /// Gets whether the chord is in an inversion (bass note differs from root).
    /// </summary>
    public bool IsInversion => BassPitchClass != RootPitchClass;

    /// <summary>
    /// Gets the chromagram that was used to detect this chord.
    /// </summary>
    public float[] Chromagram { get; }

    /// <summary>
    /// Gets the timestamp when this chord was detected (in seconds from start).
    /// </summary>
    public double Timestamp { get; }

    /// <summary>
    /// Creates a new detected chord result.
    /// </summary>
    public DetectedChord(
        int rootPitchClass,
        ChordType chordType,
        float confidence,
        int bassPitchClass,
        float[] chromagram,
        double timestamp)
    {
        RootPitchClass = rootPitchClass;
        ChordType = chordType;
        Confidence = confidence;
        BassPitchClass = bassPitchClass;
        Chromagram = (float[])chromagram.Clone();
        Timestamp = timestamp;
    }

    /// <summary>
    /// Gets the chord name (e.g., "C", "Am", "F#dim7").
    /// </summary>
    public string GetChordName()
    {
        string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        string root = noteNames[RootPitchClass];
        string suffix = GetChordSuffix();
        string bass = IsInversion ? $"/{noteNames[BassPitchClass]}" : "";
        return $"{root}{suffix}{bass}";
    }

    private string GetChordSuffix()
    {
        return ChordType switch
        {
            ChordType.Major => "",
            ChordType.Minor => "m",
            ChordType.Diminished => "dim",
            ChordType.Augmented => "aug",
            ChordType.Major7 => "maj7",
            ChordType.Minor7 => "m7",
            ChordType.Dominant7 => "7",
            ChordType.Diminished7 => "dim7",
            ChordType.HalfDiminished7 => "m7b5",
            ChordType.MinorMajor7 => "m(maj7)",
            ChordType.Augmented7 => "aug7",
            ChordType.Major9 => "maj9",
            ChordType.Minor9 => "m9",
            ChordType.Dominant9 => "9",
            ChordType.Add9 => "add9",
            ChordType.Sus2 => "sus2",
            ChordType.Sus4 => "sus4",
            ChordType.Power => "5",
            ChordType.Major6 => "6",
            ChordType.Minor6 => "m6",
            ChordType.Dominant11 => "11",
            ChordType.Major13 => "maj13",
            ChordType.Minor13 => "m13",
            _ => ""
        };
    }

    public override string ToString()
    {
        return $"{GetChordName()} ({Confidence:P0})";
    }

    /// <summary>
    /// Returns true if this chord is functionally equivalent to another chord
    /// (same root and type, ignoring confidence and timestamp).
    /// </summary>
    public bool IsSameChord(DetectedChord? other)
    {
        if (other == null) return false;
        return RootPitchClass == other.RootPitchClass && ChordType == other.ChordType;
    }
}

/// <summary>
/// Event arguments for chord detection events.
/// </summary>
public class ChordDetectedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the detected chord.
    /// </summary>
    public DetectedChord Chord { get; }

    /// <summary>
    /// Gets the previous chord (null if this is the first detection).
    /// </summary>
    public DetectedChord? PreviousChord { get; }

    /// <summary>
    /// Creates new chord detected event arguments.
    /// </summary>
    public ChordDetectedEventArgs(DetectedChord chord, DetectedChord? previousChord)
    {
        Chord = chord;
        PreviousChord = previousChord;
    }
}

/// <summary>
/// Real-time chord detector using chromagram analysis and template matching.
/// Implements ISampleProvider for inline audio processing and IAnalyzer for use with AnalysisChain.
/// </summary>
public class ChordDetector : ISampleProvider, IAnalyzer
{
    // FFT configuration
    private readonly int _fftLength;
    private readonly int _sampleRate;
    private readonly Complex[] _fftBuffer;
    private readonly float[] _sampleBuffer;
    private int _sampleCount;
    private readonly int _channels;

    // Chromagram
    private readonly float[] _chromagram;
    private readonly float[] _smoothedChromagram;
    private readonly float[][] _chromagramHistory;
    private int _chromagramHistoryIndex;
    private const int ChromagramHistorySize = 4;

    // Bass detection
    private readonly float[] _bassChromagram;

    // Chord templates
    private readonly List<ChordTemplate> _templates;
    private readonly Dictionary<ChordType, ChordTemplate> _templatesByType;

    // Detection state
    private DetectedChord? _currentChord;
    private DetectedChord? _previousChord;
    private int _stableFrameCount;
    private double _totalSamplesProcessed;
    private readonly object _lock = new();

    // Configuration
    private float _chromaSmoothingFactor = 0.7f;
    private float _minimumConfidence = 0.4f;
    private int _stabilityFrames = 3;
    private float _changeThreshold = 0.15f;

    // Source provider for pass-through
    private readonly ISampleProvider? _source;

    /// <summary>
    /// Gets the wave format.
    /// </summary>
    public WaveFormat WaveFormat { get; }

    /// <summary>
    /// Gets the current detected chord.
    /// </summary>
    public DetectedChord? CurrentChord
    {
        get
        {
            lock (_lock)
            {
                return _currentChord;
            }
        }
    }

    /// <summary>
    /// Gets the current chromagram (12-element pitch class profile).
    /// </summary>
    public float[] Chromagram
    {
        get
        {
            lock (_lock)
            {
                return (float[])_smoothedChromagram.Clone();
            }
        }
    }

    /// <summary>
    /// Gets the bass chromagram (focused on lower frequencies).
    /// </summary>
    public float[] BassChromagram
    {
        get
        {
            lock (_lock)
            {
                return (float[])_bassChromagram.Clone();
            }
        }
    }

    /// <summary>
    /// Gets or sets the chromagram smoothing factor (0.0 = no smoothing, 0.99 = maximum smoothing).
    /// Higher values reduce rapid fluctuations but increase latency.
    /// </summary>
    public float ChromaSmoothingFactor
    {
        get => _chromaSmoothingFactor;
        set => _chromaSmoothingFactor = Math.Clamp(value, 0f, 0.99f);
    }

    /// <summary>
    /// Gets or sets the minimum confidence threshold for chord detection.
    /// Chords below this threshold will not trigger change events.
    /// </summary>
    public float MinimumConfidence
    {
        get => _minimumConfidence;
        set => _minimumConfidence = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Gets or sets the number of consecutive frames a chord must be detected
    /// before it is considered stable and reported.
    /// </summary>
    public int StabilityFrames
    {
        get => _stabilityFrames;
        set => _stabilityFrames = Math.Max(1, value);
    }

    /// <summary>
    /// Gets or sets the confidence difference threshold for detecting chord changes.
    /// A new chord must exceed the current chord's confidence by this amount.
    /// </summary>
    public float ChangeThreshold
    {
        get => _changeThreshold;
        set => _changeThreshold = Math.Clamp(value, 0f, 0.5f);
    }

    /// <summary>
    /// Gets all registered chord templates.
    /// </summary>
    public IReadOnlyList<ChordTemplate> Templates => _templates.AsReadOnly();

    /// <summary>
    /// Event raised when the detected chord changes.
    /// </summary>
    public event EventHandler<ChordDetectedEventArgs>? ChordChanged;

    /// <summary>
    /// Event raised on every analysis frame, regardless of chord change.
    /// </summary>
    public event EventHandler<ChordDetectedEventArgs>? ChordAnalyzed;

    /// <summary>
    /// Creates a new chord detector with the specified configuration.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate in Hz (default: 44100).</param>
    /// <param name="channels">Number of audio channels (default: 2).</param>
    /// <param name="fftLength">FFT window size, must be power of 2 (default: 4096).</param>
    public ChordDetector(
        int sampleRate = 44100,
        int channels = 2,
        int fftLength = 4096)
    {
        if (!IsPowerOfTwo(fftLength))
            throw new ArgumentException("FFT length must be a power of two.", nameof(fftLength));
        if (sampleRate < 8000 || sampleRate > 192000)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "Sample rate must be between 8000 and 192000 Hz.");
        if (channels < 1 || channels > 2)
            throw new ArgumentOutOfRangeException(nameof(channels), "Channels must be 1 or 2.");

        _fftLength = fftLength;
        _sampleRate = sampleRate;
        _channels = channels;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);

        _fftBuffer = new Complex[fftLength];
        _sampleBuffer = new float[fftLength];
        _chromagram = new float[12];
        _smoothedChromagram = new float[12];
        _bassChromagram = new float[12];

        _chromagramHistory = new float[ChromagramHistorySize][];
        for (int i = 0; i < ChromagramHistorySize; i++)
        {
            _chromagramHistory[i] = new float[12];
        }

        _templates = new List<ChordTemplate>();
        _templatesByType = new Dictionary<ChordType, ChordTemplate>();
        InitializeDefaultTemplates();
    }

    /// <summary>
    /// Creates a chord detector that wraps an audio source for inline processing.
    /// </summary>
    /// <param name="source">The audio source to analyze.</param>
    /// <param name="fftLength">FFT window size (default: 4096).</param>
    public ChordDetector(ISampleProvider source, int fftLength = 4096)
        : this(source.WaveFormat.SampleRate, source.WaveFormat.Channels, fftLength)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    /// <summary>
    /// Initializes the default chord templates for common chord types.
    /// </summary>
    private void InitializeDefaultTemplates()
    {
        // Triads
        AddTemplate(new ChordTemplate(ChordType.Major, "Major", "", 0, 4, 7));
        AddTemplate(new ChordTemplate(ChordType.Minor, "Minor", "m", 0, 3, 7));
        AddTemplate(new ChordTemplate(ChordType.Diminished, "Diminished", "dim", 0, 3, 6));
        AddTemplate(new ChordTemplate(ChordType.Augmented, "Augmented", "aug", 0, 4, 8));

        // Suspended
        AddTemplate(new ChordTemplate(ChordType.Sus2, "Suspended 2nd", "sus2", 0, 2, 7));
        AddTemplate(new ChordTemplate(ChordType.Sus4, "Suspended 4th", "sus4", 0, 5, 7));

        // Seventh chords
        AddTemplate(new ChordTemplate(ChordType.Major7, "Major 7th", "maj7", 0, 4, 7, 11));
        AddTemplate(new ChordTemplate(ChordType.Minor7, "Minor 7th", "m7", 0, 3, 7, 10));
        AddTemplate(new ChordTemplate(ChordType.Dominant7, "Dominant 7th", "7", 0, 4, 7, 10));
        AddTemplate(new ChordTemplate(ChordType.Diminished7, "Diminished 7th", "dim7", 0, 3, 6, 9));
        AddTemplate(new ChordTemplate(ChordType.HalfDiminished7, "Half-Diminished 7th", "m7b5", 0, 3, 6, 10));
        AddTemplate(new ChordTemplate(ChordType.MinorMajor7, "Minor Major 7th", "m(maj7)", 0, 3, 7, 11));
        AddTemplate(new ChordTemplate(ChordType.Augmented7, "Augmented 7th", "aug7", 0, 4, 8, 10));

        // Sixth chords
        AddTemplate(new ChordTemplate(ChordType.Major6, "Major 6th", "6", 0, 4, 7, 9));
        AddTemplate(new ChordTemplate(ChordType.Minor6, "Minor 6th", "m6", 0, 3, 7, 9));

        // Extended chords
        AddTemplate(new ChordTemplate(ChordType.Add9, "Add 9", "add9", 0, 4, 7, 14));
        AddTemplate(new ChordTemplate(ChordType.Major9, "Major 9th", "maj9", 0, 4, 7, 11, 14));
        AddTemplate(new ChordTemplate(ChordType.Minor9, "Minor 9th", "m9", 0, 3, 7, 10, 14));
        AddTemplate(new ChordTemplate(ChordType.Dominant9, "Dominant 9th", "9", 0, 4, 7, 10, 14));
        AddTemplate(new ChordTemplate(ChordType.Dominant11, "Dominant 11th", "11", 0, 4, 7, 10, 14, 17));
        AddTemplate(new ChordTemplate(ChordType.Major13, "Major 13th", "maj13", 0, 4, 7, 11, 14, 21));
        AddTemplate(new ChordTemplate(ChordType.Minor13, "Minor 13th", "m13", 0, 3, 7, 10, 14, 21));

        // Power chord
        AddTemplate(new ChordTemplate(ChordType.Power, "Power Chord", "5", 0, 7));
    }

    /// <summary>
    /// Adds a chord template to the detector.
    /// </summary>
    /// <param name="template">The template to add.</param>
    public void AddTemplate(ChordTemplate template)
    {
        if (template == null)
            throw new ArgumentNullException(nameof(template));

        _templates.Add(template);
        _templatesByType[template.ChordType] = template;
    }

    /// <summary>
    /// Removes a chord template from the detector.
    /// </summary>
    /// <param name="chordType">The chord type to remove.</param>
    /// <returns>True if the template was removed, false if not found.</returns>
    public bool RemoveTemplate(ChordType chordType)
    {
        if (_templatesByType.TryGetValue(chordType, out var template))
        {
            _templates.Remove(template);
            _templatesByType.Remove(chordType);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Clears all chord templates.
    /// </summary>
    public void ClearTemplates()
    {
        _templates.Clear();
        _templatesByType.Clear();
    }

    /// <summary>
    /// Reads and processes audio samples from the source.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        if (_source == null)
            throw new InvalidOperationException("ChordDetector was not initialized with an audio source.");

        int samplesRead = _source.Read(buffer, offset, count);
        if (samplesRead > 0)
        {
            ProcessSamplesInternal(buffer, offset, samplesRead);
        }
        return samplesRead;
    }

    /// <summary>
    /// Processes audio samples for chord detection (IAnalyzer implementation).
    /// </summary>
    public void ProcessSamples(float[] samples, int offset, int count, int channels)
    {
        ProcessSamplesInternal(samples, offset, count);
    }

    /// <summary>
    /// Processes audio samples for chord detection.
    /// </summary>
    /// <param name="samples">Audio samples buffer.</param>
    /// <param name="count">Number of samples.</param>
    public void ProcessSamples(float[] samples, int count)
    {
        ProcessSamplesInternal(samples, 0, count);
    }

    private void ProcessSamplesInternal(float[] samples, int offset, int count)
    {
        // Mix to mono and accumulate samples
        for (int i = 0; i < count; i += _channels)
        {
            float sample = 0;
            for (int ch = 0; ch < _channels; ch++)
            {
                int idx = offset + i + ch;
                if (idx < samples.Length)
                {
                    sample += samples[idx];
                }
            }
            sample /= _channels;

            _sampleBuffer[_sampleCount++] = sample;

            if (_sampleCount >= _fftLength)
            {
                PerformAnalysis();
                // Overlap by 50%
                Array.Copy(_sampleBuffer, _fftLength / 2, _sampleBuffer, 0, _fftLength / 2);
                _sampleCount = _fftLength / 2;
            }
        }

        _totalSamplesProcessed += count;
    }

    /// <summary>
    /// Analyzes a complete audio buffer and returns all detected chords.
    /// </summary>
    /// <param name="samples">Complete audio buffer (mono).</param>
    /// <param name="sampleRate">Sample rate of the audio.</param>
    /// <returns>List of detected chords with timestamps.</returns>
    public List<DetectedChord> AnalyzeBuffer(float[] samples, int sampleRate)
    {
        var results = new List<DetectedChord>();

        // Reset state
        Reset();

        // Subscribe to chord changes temporarily
        void OnChordChanged(object? sender, ChordDetectedEventArgs e)
        {
            results.Add(e.Chord);
        }

        ChordChanged += OnChordChanged;

        try
        {
            // Process all samples
            for (int i = 0; i < samples.Length; i++)
            {
                _sampleBuffer[_sampleCount++] = samples[i];
                _totalSamplesProcessed++;

                if (_sampleCount >= _fftLength)
                {
                    PerformAnalysis();
                    Array.Copy(_sampleBuffer, _fftLength / 2, _sampleBuffer, 0, _fftLength / 2);
                    _sampleCount = _fftLength / 2;
                }
            }

            // Add final chord if detected
            if (_currentChord != null && (results.Count == 0 || !results[^1].IsSameChord(_currentChord)))
            {
                results.Add(_currentChord);
            }
        }
        finally
        {
            ChordChanged -= OnChordChanged;
        }

        return results;
    }

    /// <summary>
    /// Resets the detector state.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _sampleCount = 0;
            _totalSamplesProcessed = 0;
            _stableFrameCount = 0;
            _currentChord = null;
            _previousChord = null;
            _chromagramHistoryIndex = 0;

            Array.Clear(_sampleBuffer, 0, _sampleBuffer.Length);
            Array.Clear(_fftBuffer, 0, _fftBuffer.Length);
            Array.Clear(_chromagram, 0, _chromagram.Length);
            Array.Clear(_smoothedChromagram, 0, _smoothedChromagram.Length);
            Array.Clear(_bassChromagram, 0, _bassChromagram.Length);

            for (int i = 0; i < ChromagramHistorySize; i++)
            {
                Array.Clear(_chromagramHistory[i], 0, 12);
            }
        }
    }

    private void PerformAnalysis()
    {
        // Apply Hann window and copy to FFT buffer
        for (int i = 0; i < _fftLength; i++)
        {
            float window = (float)(0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (_fftLength - 1))));
            _fftBuffer[i].X = _sampleBuffer[i] * window;
            _fftBuffer[i].Y = 0;
        }

        // Perform FFT
        int m = (int)Math.Log(_fftLength, 2.0);
        FastFourierTransform.FFT(true, m, _fftBuffer);

        // Compute chromagram from FFT
        ComputeChromagram();

        // Apply temporal smoothing
        ApplySmoothing();

        // Detect chord from chromagram
        DetectChord();
    }

    private void ComputeChromagram()
    {
        Array.Clear(_chromagram, 0, 12);
        Array.Clear(_bassChromagram, 0, 12);

        float binResolution = (float)_sampleRate / _fftLength;

        // Frequency range for chord detection (approximately C2 to C6, ~65Hz to ~1047Hz)
        float minFreq = 65.41f; // C2
        float maxFreq = 2093.0f; // C7

        // Bass frequency range (C1 to C3, ~32Hz to ~131Hz)
        float bassMinFreq = 32.7f; // C1
        float bassMaxFreq = 261.63f; // C4

        int minBin = Math.Max(1, (int)(minFreq / binResolution));
        int maxBin = Math.Min(_fftLength / 2 - 1, (int)(maxFreq / binResolution));
        int bassMinBin = Math.Max(1, (int)(bassMinFreq / binResolution));
        int bassMaxBin = Math.Min(_fftLength / 2 - 1, (int)(bassMaxFreq / binResolution));

        // Map FFT bins to pitch classes
        for (int bin = minBin; bin <= maxBin; bin++)
        {
            float frequency = bin * binResolution;
            float magnitude = (float)Math.Sqrt(
                _fftBuffer[bin].X * _fftBuffer[bin].X +
                _fftBuffer[bin].Y * _fftBuffer[bin].Y);

            // Convert frequency to pitch class using equal temperament
            // MIDI note = 69 + 12 * log2(f / 440)
            double midiNote = 69.0 + 12.0 * Math.Log2(frequency / 440.0);
            int pitchClass = ((int)Math.Round(midiNote) % 12 + 12) % 12;

            // Weight by magnitude (log scale for better dynamics)
            float weight = (float)Math.Max(0, 20 * Math.Log10(magnitude + 1e-10) + 60) / 60f;
            weight = Math.Clamp(weight, 0f, 1f);

            _chromagram[pitchClass] += weight * magnitude;
        }

        // Compute bass chromagram separately
        for (int bin = bassMinBin; bin <= bassMaxBin; bin++)
        {
            float frequency = bin * binResolution;
            float magnitude = (float)Math.Sqrt(
                _fftBuffer[bin].X * _fftBuffer[bin].X +
                _fftBuffer[bin].Y * _fftBuffer[bin].Y);

            double midiNote = 69.0 + 12.0 * Math.Log2(frequency / 440.0);
            int pitchClass = ((int)Math.Round(midiNote) % 12 + 12) % 12;

            _bassChromagram[pitchClass] += magnitude;
        }

        // Normalize both chromagrams
        NormalizeArray(_chromagram);
        NormalizeArray(_bassChromagram);
    }

    private void ApplySmoothing()
    {
        // Store current chromagram in history
        Array.Copy(_chromagram, _chromagramHistory[_chromagramHistoryIndex], 12);
        _chromagramHistoryIndex = (_chromagramHistoryIndex + 1) % ChromagramHistorySize;

        // Apply exponential moving average
        for (int i = 0; i < 12; i++)
        {
            _smoothedChromagram[i] = _smoothedChromagram[i] * _chromaSmoothingFactor +
                                     _chromagram[i] * (1f - _chromaSmoothingFactor);
        }
    }

    private void DetectChord()
    {
        if (_templates.Count == 0)
            return;

        float bestScore = float.MinValue;
        int bestRoot = 0;
        ChordTemplate? bestTemplate = null;

        // Try each template at each root position
        foreach (var template in _templates)
        {
            for (int root = 0; root < 12; root++)
            {
                float score = CalculateTemplateMatch(template, root);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestRoot = root;
                    bestTemplate = template;
                }
            }
        }

        if (bestTemplate == null)
            return;

        // Detect bass note
        int bassNote = DetectBassNote();

        // Convert score to confidence (0-1 range)
        float confidence = Math.Clamp((bestScore + 1f) / 2f, 0f, 1f);

        // Get current timestamp
        double timestamp = _totalSamplesProcessed / (_sampleRate * _channels);

        // Create candidate chord
        var candidateChord = new DetectedChord(
            bestRoot,
            bestTemplate.ChordType,
            confidence,
            bassNote,
            _smoothedChromagram,
            timestamp);

        // Apply stability filtering
        lock (_lock)
        {
            bool isNewChord = !candidateChord.IsSameChord(_currentChord);
            bool meetsConfidence = confidence >= _minimumConfidence;

            if (isNewChord && meetsConfidence)
            {
                _stableFrameCount++;

                if (_stableFrameCount >= _stabilityFrames)
                {
                    // Check if new chord significantly better than current
                    bool shouldChange = _currentChord == null ||
                                       confidence > _currentChord.Confidence + _changeThreshold;

                    if (shouldChange)
                    {
                        _previousChord = _currentChord;
                        _currentChord = candidateChord;
                        _stableFrameCount = 0;

                        // Raise chord changed event
                        ChordChanged?.Invoke(this, new ChordDetectedEventArgs(_currentChord, _previousChord));
                    }
                }
            }
            else if (!isNewChord)
            {
                // Same chord, update confidence
                if (_currentChord != null && confidence > _currentChord.Confidence)
                {
                    _currentChord = candidateChord;
                }
                _stableFrameCount = 0;
            }
            else
            {
                // Below confidence threshold or chord changed, reset stability counter
                _stableFrameCount = 0;
            }

            // Always raise analysis event
            ChordAnalyzed?.Invoke(this, new ChordDetectedEventArgs(candidateChord, _currentChord));
        }
    }

    private float CalculateTemplateMatch(ChordTemplate template, int root)
    {
        // Get the template profile rotated to the specified root
        float[] rotatedProfile = template.GetProfileForRoot(root);

        // Calculate cosine similarity between chromagram and template
        float dotProduct = 0;
        float chromaNorm = 0;
        float templateNorm = 0;

        for (int i = 0; i < 12; i++)
        {
            dotProduct += _smoothedChromagram[i] * rotatedProfile[i];
            chromaNorm += _smoothedChromagram[i] * _smoothedChromagram[i];
            templateNorm += rotatedProfile[i] * rotatedProfile[i];
        }

        chromaNorm = (float)Math.Sqrt(chromaNorm);
        templateNorm = (float)Math.Sqrt(templateNorm);

        if (chromaNorm < 1e-10 || templateNorm < 1e-10)
            return 0;

        return dotProduct / (chromaNorm * templateNorm);
    }

    private int DetectBassNote()
    {
        // Find the pitch class with highest energy in the bass range
        int bassNote = 0;
        float maxEnergy = 0;

        for (int i = 0; i < 12; i++)
        {
            if (_bassChromagram[i] > maxEnergy)
            {
                maxEnergy = _bassChromagram[i];
                bassNote = i;
            }
        }

        return bassNote;
    }

    private static void NormalizeArray(float[] array)
    {
        float max = array.Max();
        if (max > 1e-10)
        {
            for (int i = 0; i < array.Length; i++)
            {
                array[i] /= max;
            }
        }
    }

    private static bool IsPowerOfTwo(int n) => n > 0 && (n & (n - 1)) == 0;

    /// <summary>
    /// Gets a formatted string representation of the current chromagram.
    /// </summary>
    public string GetChromagramDisplay()
    {
        string[] noteNames = { "C ", "C#", "D ", "D#", "E ", "F ", "F#", "G ", "G#", "A ", "A#", "B " };
        var display = new System.Text.StringBuilder();

        lock (_lock)
        {
            for (int i = 0; i < 12; i++)
            {
                int barLength = (int)(_smoothedChromagram[i] * 20);
                display.AppendLine($"{noteNames[i]} [{new string('|', barLength).PadRight(20)}] {_smoothedChromagram[i]:F2}");
            }
        }

        return display.ToString();
    }
}
