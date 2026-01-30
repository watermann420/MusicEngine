//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Guitar tuner using YIN pitch detection algorithm optimized for guitar frequencies.

using System;
using System.Collections.Generic;

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Represents a guitar string with its target note and frequency.
/// </summary>
public class GuitarString
{
    /// <summary>
    /// Gets or sets the string number (1 = highest, 6 = lowest for standard 6-string).
    /// </summary>
    public int StringNumber { get; init; }

    /// <summary>
    /// Gets or sets the target note name (e.g., "E", "A", "D").
    /// </summary>
    public string NoteName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the MIDI note number for this string.
    /// </summary>
    public int MidiNote { get; init; }

    /// <summary>
    /// Gets or sets the target frequency in Hz.
    /// </summary>
    public double Frequency { get; init; }

    /// <summary>
    /// Gets or sets the octave number.
    /// </summary>
    public int Octave { get; init; }

    /// <summary>
    /// Gets the full note name including octave (e.g., "E4").
    /// </summary>
    public string FullNoteName => $"{NoteName}{Octave}";
}

/// <summary>
/// Predefined guitar tuning configurations.
/// </summary>
public enum GuitarTuning
{
    /// <summary>Standard tuning: E A D G B E (E2 A2 D3 G3 B3 E4)</summary>
    Standard,

    /// <summary>Drop D tuning: D A D G B E (D2 A2 D3 G3 B3 E4)</summary>
    DropD,

    /// <summary>DADGAD tuning: D A D G A D (D2 A2 D3 G3 A3 D4)</summary>
    DADGAD,

    /// <summary>Open G tuning: D G D G B D (D2 G2 D3 G3 B3 D4)</summary>
    OpenG,

    /// <summary>Open D tuning: D A D F# A D (D2 A2 D3 F#3 A3 D4)</summary>
    OpenD,

    /// <summary>Open E tuning: E B E G# B E (E2 B2 E3 G#3 B3 E4)</summary>
    OpenE,

    /// <summary>Half step down: Eb Ab Db Gb Bb Eb</summary>
    HalfStepDown,

    /// <summary>Full step down: D G C F A D</summary>
    FullStepDown,

    /// <summary>Drop C tuning: C G C F A D</summary>
    DropC,

    /// <summary>Double Drop D: D A D G B D (D2 A2 D3 G3 B3 D4)</summary>
    DoubleDropD,

    /// <summary>Open C tuning: C G C G C E (C2 G2 C3 G3 C4 E4)</summary>
    OpenC,

    /// <summary>Custom tuning defined by user.</summary>
    Custom
}

/// <summary>
/// Event arguments for pitch detection updates.
/// </summary>
public class PitchDetectedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the detected frequency in Hz.
    /// </summary>
    public double Frequency { get; }

    /// <summary>
    /// Gets the detected note name (e.g., "A", "C#").
    /// </summary>
    public string NoteName { get; }

    /// <summary>
    /// Gets the MIDI note number (0-127).
    /// </summary>
    public int MidiNote { get; }

    /// <summary>
    /// Gets the octave number.
    /// </summary>
    public int Octave { get; }

    /// <summary>
    /// Gets the cents offset from the nearest note (-50 to +50).
    /// Negative values indicate flat, positive indicate sharp.
    /// </summary>
    public double CentsOffset { get; }

    /// <summary>
    /// Gets whether the note is considered in tune (within tolerance).
    /// </summary>
    public bool IsInTune { get; }

    /// <summary>
    /// Gets the confidence level of the pitch detection (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; }

    /// <summary>
    /// Gets the closest matching guitar string, if any.
    /// </summary>
    public GuitarString? ClosestString { get; }

    /// <summary>
    /// Creates new pitch detection event arguments.
    /// </summary>
    public PitchDetectedEventArgs(
        double frequency,
        string noteName,
        int midiNote,
        int octave,
        double centsOffset,
        bool isInTune,
        double confidence,
        GuitarString? closestString)
    {
        Frequency = frequency;
        NoteName = noteName;
        MidiNote = midiNote;
        Octave = octave;
        CentsOffset = centsOffset;
        IsInTune = isInTune;
        Confidence = confidence;
        ClosestString = closestString;
    }
}

/// <summary>
/// Event arguments for tuning status changes.
/// </summary>
public class TuningStatusEventArgs : EventArgs
{
    /// <summary>
    /// Gets the guitar string that changed tuning status.
    /// </summary>
    public GuitarString String { get; }

    /// <summary>
    /// Gets whether the string is now in tune.
    /// </summary>
    public bool IsInTune { get; }

    /// <summary>
    /// Gets the current cents offset.
    /// </summary>
    public double CentsOffset { get; }

    /// <summary>
    /// Creates new tuning status event arguments.
    /// </summary>
    public TuningStatusEventArgs(GuitarString guitarString, bool isInTune, double centsOffset)
    {
        String = guitarString;
        IsInTune = isInTune;
        CentsOffset = centsOffset;
    }
}

/// <summary>
/// Real-time guitar tuner using YIN pitch detection algorithm.
/// Optimized for guitar frequencies with support for various tunings.
/// </summary>
/// <remarks>
/// The tuner uses the YIN pitch detection algorithm which provides highly accurate
/// fundamental frequency detection for monophonic signals. The algorithm works by:
/// 1. Computing the cumulative mean normalized difference function
/// 2. Finding the first minimum below an absolute threshold
/// 3. Using parabolic interpolation for sub-sample accuracy
///
/// The tuner is optimized for the guitar frequency range (roughly 80 Hz to 1200 Hz)
/// and provides real-time feedback on pitch, cents offset, and tuning status.
/// </remarks>
public class GuitarTuner
{
    private readonly int _sampleRate;
    private readonly int _frameSize;
    private readonly int _hopSize;
    private readonly float[] _frameBuffer;
    private int _frameBufferPosition;
    private readonly object _lock = new();

    // YIN algorithm buffers
    private readonly float[] _yinBuffer;
    private readonly int _yinBufferSize;

    // Reference frequency (A4 = 440 Hz by default)
    private double _referenceA4 = 440.0;

    // Current tuning configuration
    private GuitarTuning _currentTuning = GuitarTuning.Standard;
    private List<GuitarString> _tuningStrings;

    // Detection state
    private double _detectedFrequency;
    private string _detectedNote = string.Empty;
    private int _detectedMidiNote;
    private int _detectedOctave;
    private double _centsOffset;
    private bool _isInTune;
    private double _confidence;
    private GuitarString? _closestString;

    // Smoothing for stability
    private readonly Queue<double> _frequencyHistory = new();
    private const int FrequencyHistorySize = 5;

    // Tuning tolerance in cents
    private double _tuningTolerance = 5.0;

    // Minimum confidence threshold
    private double _minConfidence = 0.85;

    // Frequency range for guitar (E2 to E5 with some margin)
    private const double MinGuitarFrequency = 70.0;   // Below E2 (82.41 Hz)
    private const double MaxGuitarFrequency = 1200.0; // Above E5 (659.25 Hz)

    // Note names for display
    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    /// <summary>
    /// Gets the currently detected frequency in Hz.
    /// Returns 0 if no valid pitch is detected.
    /// </summary>
    public double DetectedFrequency
    {
        get
        {
            lock (_lock)
            {
                return _detectedFrequency;
            }
        }
    }

    /// <summary>
    /// Gets the detected note name (e.g., "A", "C#", "E").
    /// Returns empty string if no valid pitch is detected.
    /// </summary>
    public string DetectedNote
    {
        get
        {
            lock (_lock)
            {
                return _detectedNote;
            }
        }
    }

    /// <summary>
    /// Gets the detected MIDI note number (0-127).
    /// Returns -1 if no valid pitch is detected.
    /// </summary>
    public int DetectedMidiNote
    {
        get
        {
            lock (_lock)
            {
                return _detectedMidiNote;
            }
        }
    }

    /// <summary>
    /// Gets the detected octave number.
    /// </summary>
    public int DetectedOctave
    {
        get
        {
            lock (_lock)
            {
                return _detectedOctave;
            }
        }
    }

    /// <summary>
    /// Gets the full detected note name with octave (e.g., "A4", "E2").
    /// </summary>
    public string DetectedNoteWithOctave
    {
        get
        {
            lock (_lock)
            {
                return string.IsNullOrEmpty(_detectedNote) ? string.Empty : $"{_detectedNote}{_detectedOctave}";
            }
        }
    }

    /// <summary>
    /// Gets the cents offset from the nearest note (-50 to +50).
    /// Negative values indicate the pitch is flat, positive values indicate sharp.
    /// </summary>
    public double CentsOffset
    {
        get
        {
            lock (_lock)
            {
                return _centsOffset;
            }
        }
    }

    /// <summary>
    /// Gets whether the current pitch is considered in tune (within tolerance).
    /// </summary>
    public bool IsInTune
    {
        get
        {
            lock (_lock)
            {
                return _isInTune;
            }
        }
    }

    /// <summary>
    /// Gets the confidence level of the current pitch detection (0.0 to 1.0).
    /// Higher values indicate more reliable detection.
    /// </summary>
    public double Confidence
    {
        get
        {
            lock (_lock)
            {
                return _confidence;
            }
        }
    }

    /// <summary>
    /// Gets the closest matching guitar string for the detected pitch.
    /// Returns null if no string is close enough or no pitch is detected.
    /// </summary>
    public GuitarString? ClosestString
    {
        get
        {
            lock (_lock)
            {
                return _closestString;
            }
        }
    }

    /// <summary>
    /// Gets or sets the reference frequency for A4 (default: 440 Hz).
    /// Common alternatives include 432 Hz and 442 Hz.
    /// </summary>
    public double ReferenceA4
    {
        get => _referenceA4;
        set
        {
            if (value < 400 || value > 480)
                throw new ArgumentOutOfRangeException(nameof(value), "Reference A4 must be between 400 and 480 Hz.");
            _referenceA4 = value;
            UpdateTuningStrings();
        }
    }

    /// <summary>
    /// Gets or sets the tuning tolerance in cents (default: 5 cents).
    /// Notes within this tolerance are considered "in tune".
    /// </summary>
    public double TuningTolerance
    {
        get => _tuningTolerance;
        set => _tuningTolerance = Math.Clamp(value, 1.0, 50.0);
    }

    /// <summary>
    /// Gets or sets the minimum confidence threshold for valid detection (default: 0.85).
    /// Lower values allow more uncertain pitch estimates.
    /// </summary>
    public double MinConfidence
    {
        get => _minConfidence;
        set => _minConfidence = Math.Clamp(value, 0.5, 0.99);
    }

    /// <summary>
    /// Gets or sets the current guitar tuning.
    /// </summary>
    public GuitarTuning CurrentTuning
    {
        get => _currentTuning;
        set
        {
            _currentTuning = value;
            UpdateTuningStrings();
        }
    }

    /// <summary>
    /// Gets the guitar strings for the current tuning configuration.
    /// </summary>
    public IReadOnlyList<GuitarString> TuningStrings
    {
        get
        {
            lock (_lock)
            {
                return _tuningStrings.AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Gets the sample rate used for analysis.
    /// </summary>
    public int SampleRate => _sampleRate;

    /// <summary>
    /// Gets the frame size used for analysis.
    /// </summary>
    public int FrameSize => _frameSize;

    /// <summary>
    /// Event raised when a pitch is detected or updated.
    /// </summary>
    public event EventHandler<PitchDetectedEventArgs>? PitchDetected;

    /// <summary>
    /// Event raised when a string's tuning status changes (goes in or out of tune).
    /// </summary>
    public event EventHandler<TuningStatusEventArgs>? TuningStatusChanged;

    /// <summary>
    /// Event raised when no pitch is detected (silence or noise).
    /// </summary>
    public event EventHandler? PitchLost;

    /// <summary>
    /// Creates a new guitar tuner with the specified configuration.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate in Hz (default: 44100).</param>
    /// <param name="frameSize">Analysis frame size in samples (default: 4096 for good low-frequency resolution).</param>
    /// <param name="hopSize">Hop size in samples (default: 1024 for responsive updates).</param>
    /// <param name="tuning">Initial guitar tuning (default: Standard).</param>
    public GuitarTuner(
        int sampleRate = 44100,
        int frameSize = 4096,
        int hopSize = 1024,
        GuitarTuning tuning = GuitarTuning.Standard)
    {
        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "Sample rate must be positive.");
        if (frameSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(frameSize), "Frame size must be positive.");
        if (hopSize <= 0 || hopSize > frameSize)
            throw new ArgumentOutOfRangeException(nameof(hopSize), "Hop size must be positive and <= frame size.");

        _sampleRate = sampleRate;
        _frameSize = frameSize;
        _hopSize = hopSize;
        _frameBuffer = new float[frameSize];

        // YIN buffer size determines the minimum detectable frequency
        // Use half the frame size to allow for autocorrelation
        _yinBufferSize = frameSize / 2;
        _yinBuffer = new float[_yinBufferSize];

        _currentTuning = tuning;
        _tuningStrings = new List<GuitarString>();
        UpdateTuningStrings();
    }

    /// <summary>
    /// Processes audio samples for pitch detection.
    /// </summary>
    /// <param name="samples">Audio samples (mono or interleaved multi-channel).</param>
    /// <param name="offset">Offset into the samples array.</param>
    /// <param name="count">Number of samples to process.</param>
    /// <param name="channels">Number of audio channels (default: 1 for mono).</param>
    public void ProcessSamples(float[] samples, int offset, int count, int channels = 1)
    {
        for (int i = offset; i < offset + count; i += channels)
        {
            // Mix to mono if multi-channel
            float sample = 0;
            for (int ch = 0; ch < channels; ch++)
            {
                if (i + ch < offset + count)
                {
                    sample += samples[i + ch];
                }
            }
            sample /= channels;

            // Add to frame buffer
            _frameBuffer[_frameBufferPosition] = sample;
            _frameBufferPosition++;

            // Process frame when full
            if (_frameBufferPosition >= _frameSize)
            {
                ProcessFrame();

                // Shift buffer by hop size
                int remaining = _frameSize - _hopSize;
                Array.Copy(_frameBuffer, _hopSize, _frameBuffer, 0, remaining);
                _frameBufferPosition = remaining;
            }
        }
    }

    /// <summary>
    /// Processes audio samples for pitch detection (simplified overload).
    /// </summary>
    /// <param name="samples">Mono audio samples.</param>
    /// <param name="count">Number of samples to process.</param>
    public void ProcessSamples(float[] samples, int count)
    {
        ProcessSamples(samples, 0, count, 1);
    }

    /// <summary>
    /// Analyzes a single buffer and returns the detected frequency.
    /// Useful for one-shot analysis rather than streaming.
    /// </summary>
    /// <param name="samples">Mono audio samples.</param>
    /// <returns>Detected frequency in Hz, or 0 if no valid pitch detected.</returns>
    public double AnalyzeBuffer(float[] samples)
    {
        if (samples == null || samples.Length == 0)
            return 0;

        Reset();

        // Process the buffer
        ProcessSamples(samples, 0, samples.Length, 1);

        return _detectedFrequency;
    }

    /// <summary>
    /// Resets the tuner state.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            Array.Clear(_frameBuffer, 0, _frameBuffer.Length);
            Array.Clear(_yinBuffer, 0, _yinBuffer.Length);
            _frameBufferPosition = 0;
            _detectedFrequency = 0;
            _detectedNote = string.Empty;
            _detectedMidiNote = -1;
            _detectedOctave = 0;
            _centsOffset = 0;
            _isInTune = false;
            _confidence = 0;
            _closestString = null;
            _frequencyHistory.Clear();
        }
    }

    /// <summary>
    /// Sets a custom tuning with the specified string frequencies.
    /// </summary>
    /// <param name="stringFrequencies">Array of frequencies for each string (low to high).</param>
    /// <param name="stringNames">Optional array of note names for each string.</param>
    public void SetCustomTuning(double[] stringFrequencies, string[]? stringNames = null)
    {
        if (stringFrequencies == null || stringFrequencies.Length == 0)
            throw new ArgumentException("String frequencies cannot be null or empty.", nameof(stringFrequencies));

        lock (_lock)
        {
            _currentTuning = GuitarTuning.Custom;
            _tuningStrings = new List<GuitarString>();

            for (int i = 0; i < stringFrequencies.Length; i++)
            {
                double freq = stringFrequencies[i];
                int midiNote = FrequencyToMidiNote(freq);
                int pitchClass = midiNote % 12;
                int octave = (midiNote / 12) - 1;

                string noteName = stringNames != null && i < stringNames.Length
                    ? stringNames[i]
                    : NoteNames[pitchClass];

                _tuningStrings.Add(new GuitarString
                {
                    StringNumber = stringFrequencies.Length - i, // Reverse: low string = high number
                    NoteName = noteName,
                    MidiNote = midiNote,
                    Frequency = freq,
                    Octave = octave
                });
            }
        }
    }

    /// <summary>
    /// Gets the target frequency for a specific note using the current reference A4.
    /// </summary>
    /// <param name="midiNote">MIDI note number.</param>
    /// <returns>Frequency in Hz.</returns>
    public double GetNoteFrequency(int midiNote)
    {
        // MIDI note 69 = A4
        return _referenceA4 * Math.Pow(2.0, (midiNote - 69) / 12.0);
    }

    /// <summary>
    /// Converts a frequency to the nearest MIDI note number.
    /// </summary>
    /// <param name="frequency">Frequency in Hz.</param>
    /// <returns>MIDI note number (0-127).</returns>
    public int FrequencyToMidiNote(double frequency)
    {
        if (frequency <= 0)
            return -1;

        double midiNote = 69 + 12 * Math.Log2(frequency / _referenceA4);
        return (int)Math.Round(midiNote);
    }

    /// <summary>
    /// Calculates the cents offset between a frequency and a target note.
    /// </summary>
    /// <param name="frequency">Measured frequency in Hz.</param>
    /// <param name="targetMidiNote">Target MIDI note number.</param>
    /// <returns>Cents offset (-50 to +50 for within half a semitone).</returns>
    public double CalculateCentsOffset(double frequency, int targetMidiNote)
    {
        double targetFrequency = GetNoteFrequency(targetMidiNote);
        return 1200 * Math.Log2(frequency / targetFrequency);
    }

    private void ProcessFrame()
    {
        // Calculate frame energy (RMS) to check for silence
        float energy = CalculateRmsEnergy(_frameBuffer, _frameSize);

        // Skip processing if signal is too quiet
        if (energy < 0.001f)
        {
            HandlePitchLost();
            return;
        }

        // Detect pitch using YIN algorithm
        var (frequency, confidence) = DetectPitchYin(_frameBuffer, _frameSize);

        // Validate detection
        if (frequency < MinGuitarFrequency || frequency > MaxGuitarFrequency || confidence < _minConfidence)
        {
            HandlePitchLost();
            return;
        }

        // Apply smoothing
        double smoothedFrequency = SmoothFrequency(frequency);

        // Calculate note information
        int midiNote = FrequencyToMidiNote(smoothedFrequency);
        if (midiNote < 0 || midiNote > 127)
        {
            HandlePitchLost();
            return;
        }

        int pitchClass = midiNote % 12;
        int octave = (midiNote / 12) - 1;
        string noteName = NoteNames[pitchClass];
        double centsOffset = CalculateCentsOffset(smoothedFrequency, midiNote);

        // Find closest guitar string
        GuitarString? closestString = FindClosestString(smoothedFrequency);

        // Determine if in tune
        bool isInTune = Math.Abs(centsOffset) <= _tuningTolerance;

        // Check for tuning status change
        bool wasInTune;
        GuitarString? previousClosestString;

        lock (_lock)
        {
            wasInTune = _isInTune;
            previousClosestString = _closestString;

            // Update state
            _detectedFrequency = smoothedFrequency;
            _detectedNote = noteName;
            _detectedMidiNote = midiNote;
            _detectedOctave = octave;
            _centsOffset = centsOffset;
            _isInTune = isInTune;
            _confidence = confidence;
            _closestString = closestString;
        }

        // Raise pitch detected event
        PitchDetected?.Invoke(this, new PitchDetectedEventArgs(
            smoothedFrequency,
            noteName,
            midiNote,
            octave,
            centsOffset,
            isInTune,
            confidence,
            closestString));

        // Raise tuning status changed event if applicable
        if (closestString != null && (wasInTune != isInTune || closestString != previousClosestString))
        {
            TuningStatusChanged?.Invoke(this, new TuningStatusEventArgs(closestString, isInTune, centsOffset));
        }
    }

    private void HandlePitchLost()
    {
        bool hadPitch;
        lock (_lock)
        {
            hadPitch = _detectedFrequency > 0;
            _detectedFrequency = 0;
            _detectedNote = string.Empty;
            _detectedMidiNote = -1;
            _detectedOctave = 0;
            _centsOffset = 0;
            _isInTune = false;
            _confidence = 0;
            _closestString = null;
        }

        if (hadPitch)
        {
            PitchLost?.Invoke(this, EventArgs.Empty);
        }
    }

    private float CalculateRmsEnergy(float[] buffer, int length)
    {
        float sum = 0;
        for (int i = 0; i < length; i++)
        {
            sum += buffer[i] * buffer[i];
        }
        return (float)Math.Sqrt(sum / length);
    }

    /// <summary>
    /// YIN pitch detection algorithm optimized for guitar frequencies.
    /// </summary>
    private (double frequency, double confidence) DetectPitchYin(float[] buffer, int length)
    {
        // Step 1: Calculate the difference function d(tau)
        // d(tau) = sum(j=0 to W-1) [ (x[j] - x[j+tau])^2 ]
        for (int tau = 0; tau < _yinBufferSize; tau++)
        {
            _yinBuffer[tau] = 0;
            for (int j = 0; j < _yinBufferSize; j++)
            {
                float diff = buffer[j] - buffer[j + tau];
                _yinBuffer[tau] += diff * diff;
            }
        }

        // Step 2: Cumulative mean normalized difference function d'(tau)
        // d'(0) = 1
        // d'(tau) = d(tau) / [(1/tau) * sum(j=1 to tau) d(j)]
        _yinBuffer[0] = 1;
        float runningSum = 0;
        for (int tau = 1; tau < _yinBufferSize; tau++)
        {
            runningSum += _yinBuffer[tau];
            if (runningSum > 0)
            {
                _yinBuffer[tau] = _yinBuffer[tau] * tau / runningSum;
            }
            else
            {
                _yinBuffer[tau] = 1;
            }
        }

        // Step 3: Absolute threshold - find the smallest tau where d'(tau) < threshold
        // Calculate lag range from guitar frequency limits
        int minTau = Math.Max(2, (int)(_sampleRate / MaxGuitarFrequency));
        int maxTau = Math.Min(_yinBufferSize - 1, (int)(_sampleRate / MinGuitarFrequency));

        // YIN threshold (typical value is 0.1 to 0.15 for guitar)
        const float yinThreshold = 0.10f;

        int tauEstimate = -1;
        float minValue = float.MaxValue;

        for (int tau = minTau; tau < maxTau; tau++)
        {
            if (_yinBuffer[tau] < yinThreshold)
            {
                // Look for local minimum
                while (tau + 1 < maxTau && _yinBuffer[tau + 1] < _yinBuffer[tau])
                {
                    tau++;
                }
                tauEstimate = tau;
                break;
            }
        }

        // If no estimate found below threshold, find global minimum
        if (tauEstimate < 0)
        {
            for (int tau = minTau; tau < maxTau; tau++)
            {
                if (_yinBuffer[tau] < minValue)
                {
                    minValue = _yinBuffer[tau];
                    tauEstimate = tau;
                }
            }
        }

        if (tauEstimate < 0 || tauEstimate >= maxTau)
        {
            return (0, 0); // No pitch detected
        }

        // Step 4: Parabolic interpolation for sub-sample accuracy
        float betterTau = tauEstimate;
        if (tauEstimate > 0 && tauEstimate < _yinBufferSize - 1)
        {
            float s0 = _yinBuffer[tauEstimate - 1];
            float s1 = _yinBuffer[tauEstimate];
            float s2 = _yinBuffer[tauEstimate + 1];
            float denominator = 2 * (2 * s1 - s2 - s0);
            if (Math.Abs(denominator) > 1e-10)
            {
                float adjustment = (s2 - s0) / denominator;
                if (Math.Abs(adjustment) < 1)
                {
                    betterTau = tauEstimate + adjustment;
                }
            }
        }

        // Calculate frequency
        double frequency = _sampleRate / betterTau;

        // Calculate confidence (1 - d'(tau))
        double confidence = 1.0 - _yinBuffer[tauEstimate];
        confidence = Math.Clamp(confidence, 0, 1);

        // Validate frequency range
        if (frequency < MinGuitarFrequency || frequency > MaxGuitarFrequency)
        {
            return (0, 0);
        }

        return (frequency, confidence);
    }

    private double SmoothFrequency(double frequency)
    {
        lock (_lock)
        {
            _frequencyHistory.Enqueue(frequency);
            while (_frequencyHistory.Count > FrequencyHistorySize)
            {
                _frequencyHistory.Dequeue();
            }

            // Use median filter for robustness against outliers
            if (_frequencyHistory.Count < 3)
            {
                return frequency;
            }

            var sorted = new List<double>(_frequencyHistory);
            sorted.Sort();
            return sorted[sorted.Count / 2];
        }
    }

    private GuitarString? FindClosestString(double frequency)
    {
        lock (_lock)
        {
            if (_tuningStrings.Count == 0)
                return null;

            GuitarString? closest = null;
            double minCentsDiff = double.MaxValue;

            foreach (var guitarString in _tuningStrings)
            {
                double centsDiff = Math.Abs(1200 * Math.Log2(frequency / guitarString.Frequency));

                // Only consider strings within 100 cents (1 semitone)
                if (centsDiff < minCentsDiff && centsDiff < 100)
                {
                    minCentsDiff = centsDiff;
                    closest = guitarString;
                }
            }

            return closest;
        }
    }

    private void UpdateTuningStrings()
    {
        lock (_lock)
        {
            _tuningStrings = GetTuningStrings(_currentTuning);
        }
    }

    private List<GuitarString> GetTuningStrings(GuitarTuning tuning)
    {
        // Standard guitar string MIDI notes (6-string guitar, low to high)
        // E2=40, A2=45, D3=50, G3=55, B3=59, E4=64
        int[] midiNotes = tuning switch
        {
            GuitarTuning.Standard => new[] { 40, 45, 50, 55, 59, 64 },       // E A D G B E
            GuitarTuning.DropD => new[] { 38, 45, 50, 55, 59, 64 },          // D A D G B E
            GuitarTuning.DADGAD => new[] { 38, 45, 50, 55, 57, 62 },         // D A D G A D
            GuitarTuning.OpenG => new[] { 38, 43, 50, 55, 59, 62 },          // D G D G B D
            GuitarTuning.OpenD => new[] { 38, 45, 50, 54, 57, 62 },          // D A D F# A D
            GuitarTuning.OpenE => new[] { 40, 47, 52, 56, 59, 64 },          // E B E G# B E
            GuitarTuning.HalfStepDown => new[] { 39, 44, 49, 54, 58, 63 },   // Eb Ab Db Gb Bb Eb
            GuitarTuning.FullStepDown => new[] { 38, 43, 48, 53, 57, 62 },   // D G C F A D
            GuitarTuning.DropC => new[] { 36, 43, 48, 53, 57, 62 },          // C G C F A D
            GuitarTuning.DoubleDropD => new[] { 38, 45, 50, 55, 59, 62 },    // D A D G B D
            GuitarTuning.OpenC => new[] { 36, 43, 48, 55, 60, 64 },          // C G C G C E
            GuitarTuning.Custom => Array.Empty<int>(),
            _ => new[] { 40, 45, 50, 55, 59, 64 } // Default to standard
        };

        var strings = new List<GuitarString>();

        for (int i = 0; i < midiNotes.Length; i++)
        {
            int midiNote = midiNotes[i];
            int pitchClass = midiNote % 12;
            int octave = (midiNote / 12) - 1;
            double frequency = GetNoteFrequency(midiNote);

            strings.Add(new GuitarString
            {
                StringNumber = midiNotes.Length - i, // String 6 (low E) to String 1 (high E)
                NoteName = NoteNames[pitchClass],
                MidiNote = midiNote,
                Frequency = frequency,
                Octave = octave
            });
        }

        return strings;
    }
}
