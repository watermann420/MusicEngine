// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio to MIDI conversion.

using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Event arguments for real-time note detection.
/// </summary>
public class NoteDetectedEventArgs : EventArgs
{
    /// <summary>
    /// Detected MIDI note number (0-127).
    /// </summary>
    public int MidiNote { get; }

    /// <summary>
    /// Note velocity (0-127) derived from audio amplitude.
    /// </summary>
    public int Velocity { get; }

    /// <summary>
    /// Start time of the note in seconds from the beginning of the audio.
    /// </summary>
    public double StartTime { get; }

    /// <summary>
    /// Duration of the note in seconds.
    /// </summary>
    public double Duration { get; }

    /// <summary>
    /// Confidence level of the pitch detection (0.0 to 1.0).
    /// Higher values indicate more reliable detection.
    /// </summary>
    public float Confidence { get; }

    /// <summary>
    /// The detected fundamental frequency in Hz.
    /// </summary>
    public float Frequency { get; }

    /// <summary>
    /// Creates a new NoteDetectedEventArgs instance.
    /// </summary>
    public NoteDetectedEventArgs(int midiNote, int velocity, double startTime, double duration, float confidence, float frequency)
    {
        MidiNote = midiNote;
        Velocity = velocity;
        StartTime = startTime;
        Duration = duration;
        Confidence = confidence;
        Frequency = frequency;
    }
}

/// <summary>
/// Represents a detected note from audio-to-MIDI conversion.
/// </summary>
public class DetectedNote
{
    /// <summary>
    /// MIDI note number (0-127).
    /// </summary>
    public int MidiNote { get; set; }

    /// <summary>
    /// Velocity (0-127) derived from audio amplitude.
    /// </summary>
    public int Velocity { get; set; }

    /// <summary>
    /// Start time in seconds.
    /// </summary>
    public double StartTime { get; set; }

    /// <summary>
    /// End time in seconds.
    /// </summary>
    public double EndTime { get; set; }

    /// <summary>
    /// Duration in seconds.
    /// </summary>
    public double Duration => EndTime - StartTime;

    /// <summary>
    /// Pitch detection confidence (0.0 to 1.0).
    /// </summary>
    public float Confidence { get; set; }

    /// <summary>
    /// Detected fundamental frequency in Hz.
    /// </summary>
    public float Frequency { get; set; }

    /// <summary>
    /// Average RMS energy of the note.
    /// </summary>
    public float Energy { get; set; }
}

/// <summary>
/// Audio-to-MIDI converter for monophonic audio.
/// Uses YIN pitch detection algorithm and energy-based onset detection
/// to convert audio signals to MIDI note events.
///
/// Best suited for monophonic audio (single notes at a time).
/// Polyphonic audio may produce unreliable results.
/// </summary>
public class AudioToMidiConverter : IAnalyzer
{
    private readonly int _sampleRate;
    private readonly int _frameSize;
    private readonly int _hopSize;
    private readonly float[] _frameBuffer;
    private int _frameBufferPosition;
    private double _currentTime;
    private readonly object _lock = new();

    // YIN algorithm buffers
    private readonly float[] _yinBuffer;
    private readonly int _yinBufferSize;

    // Onset detection
    private readonly float[] _energyHistory;
    private int _energyHistoryPosition;
    private float _previousEnergy;
    private const int EnergyHistorySize = 43; // ~1 second at typical hop size

    // Note tracking state
    private bool _noteIsActive;
    private int _currentMidiNote;
    private double _noteStartTime;
    private float _noteEnergy;
    private float _noteConfidence;
    private float _noteFrequency;
    private int _noteFrameCount;
    private double _lastOnsetTime = double.NegativeInfinity;

    // Detection results
    private readonly List<DetectedNote> _detectedNotes = new();

    // Configuration properties
    private float _minFrequency = 50f;
    private float _maxFrequency = 2000f;
    private float _onsetThreshold = 0.1f;
    private float _minNoteDuration = 0.05f;
    private float _pitchConfidenceThreshold = 0.8f;
    private float _onsetSensitivity = 1.5f;
    private float _releaseThreshold = 0.3f;
    private float _velocityScale = 1.0f;

    /// <summary>
    /// Minimum frequency to detect in Hz (default: 50 Hz, ~G1).
    /// </summary>
    public float MinFrequency
    {
        get => _minFrequency;
        set => _minFrequency = Math.Clamp(value, 20f, 5000f);
    }

    /// <summary>
    /// Maximum frequency to detect in Hz (default: 2000 Hz, ~B6).
    /// </summary>
    public float MaxFrequency
    {
        get => _maxFrequency;
        set => _maxFrequency = Math.Clamp(value, 50f, 10000f);
    }

    /// <summary>
    /// Threshold for onset detection (0.0 to 1.0, default: 0.1).
    /// Lower values detect quieter note attacks.
    /// </summary>
    public float OnsetThreshold
    {
        get => _onsetThreshold;
        set => _onsetThreshold = Math.Clamp(value, 0.01f, 1f);
    }

    /// <summary>
    /// Minimum note duration in seconds (default: 0.05).
    /// Notes shorter than this are filtered out.
    /// </summary>
    public float MinNoteDuration
    {
        get => _minNoteDuration;
        set => _minNoteDuration = Math.Clamp(value, 0.01f, 1f);
    }

    /// <summary>
    /// Confidence threshold for pitch detection (0.0 to 1.0, default: 0.8).
    /// Lower values allow more uncertain pitch estimates.
    /// </summary>
    public float PitchConfidenceThreshold
    {
        get => _pitchConfidenceThreshold;
        set => _pitchConfidenceThreshold = Math.Clamp(value, 0.1f, 1f);
    }

    /// <summary>
    /// Sensitivity multiplier for onset detection (default: 1.5).
    /// Higher values make onset detection more sensitive.
    /// </summary>
    public float OnsetSensitivity
    {
        get => _onsetSensitivity;
        set => _onsetSensitivity = Math.Clamp(value, 0.5f, 5f);
    }

    /// <summary>
    /// Threshold for note release detection as fraction of onset energy (default: 0.3).
    /// Notes end when energy drops below this fraction of onset energy.
    /// </summary>
    public float ReleaseThreshold
    {
        get => _releaseThreshold;
        set => _releaseThreshold = Math.Clamp(value, 0.1f, 0.9f);
    }

    /// <summary>
    /// Scale factor for velocity calculation (default: 1.0).
    /// Increase for louder output, decrease for quieter.
    /// </summary>
    public float VelocityScale
    {
        get => _velocityScale;
        set => _velocityScale = Math.Clamp(value, 0.1f, 3f);
    }

    /// <summary>
    /// Gets the list of detected notes from offline analysis.
    /// </summary>
    public IReadOnlyList<DetectedNote> DetectedNotes
    {
        get
        {
            lock (_lock)
            {
                return new List<DetectedNote>(_detectedNotes);
            }
        }
    }

    /// <summary>
    /// Event raised when a note is detected (for real-time processing).
    /// </summary>
    public event EventHandler<NoteDetectedEventArgs>? NoteDetected;

    /// <summary>
    /// Event raised when a note ends (for real-time processing).
    /// </summary>
    public event EventHandler<NoteDetectedEventArgs>? NoteEnded;

    /// <summary>
    /// Creates a new AudioToMidiConverter with the specified configuration.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate in Hz (default: 44100).</param>
    /// <param name="frameSize">Analysis frame size in samples (default: 2048).</param>
    /// <param name="hopSize">Hop size in samples (default: 512).</param>
    public AudioToMidiConverter(int sampleRate = 44100, int frameSize = 2048, int hopSize = 512)
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
        _energyHistory = new float[EnergyHistorySize];

        // YIN buffer size determines the minimum detectable frequency
        // yinBufferSize = frameSize / 2 to allow for autocorrelation
        _yinBufferSize = frameSize / 2;
        _yinBuffer = new float[_yinBufferSize];
    }

    /// <summary>
    /// Converts an audio buffer to a list of NoteEvent objects.
    /// This is the primary offline conversion method.
    /// </summary>
    /// <param name="samples">Mono audio samples.</param>
    /// <param name="sampleRate">Sample rate of the audio.</param>
    /// <returns>List of detected note events.</returns>
    public List<NoteEvent> ConvertBuffer(float[] samples, int sampleRate)
    {
        // Reset state
        Reset();

        // Process all samples
        ProcessSamples(samples, 0, samples.Length, 1);

        // Finalize any pending note
        FinalizeCurrentNote();

        // Convert to NoteEvent list
        var noteEvents = new List<NoteEvent>();
        foreach (var note in _detectedNotes)
        {
            // Convert time to a simple beat representation (assuming 120 BPM for generic conversion)
            // User should use ConvertToPattern for proper BPM handling
            noteEvents.Add(new NoteEvent
            {
                Note = note.MidiNote,
                Velocity = note.Velocity,
                Beat = note.StartTime * 2.0, // At 120 BPM, 1 second = 2 beats
                Duration = note.Duration * 2.0
            });
        }

        return noteEvents;
    }

    /// <summary>
    /// Converts an audio buffer to a Pattern object with proper timing.
    /// </summary>
    /// <param name="samples">Mono audio samples.</param>
    /// <param name="sampleRate">Sample rate of the audio.</param>
    /// <param name="bpm">Tempo in beats per minute.</param>
    /// <param name="synth">Synthesizer to use for the pattern (optional).</param>
    /// <returns>Pattern containing the detected notes.</returns>
    public Pattern ConvertToPattern(float[] samples, int sampleRate, double bpm, ISynth? synth = null)
    {
        // Reset state
        Reset();

        // Process all samples
        ProcessSamples(samples, 0, samples.Length, 1);

        // Finalize any pending note
        FinalizeCurrentNote();

        // Calculate beats per second
        double beatsPerSecond = bpm / 60.0;

        // Create pattern (use a null-safe synth)
        var pattern = new Pattern(synth ?? new DummySynth())
        {
            Name = "Audio-to-MIDI Conversion",
            IsLooping = false
        };

        double maxBeat = 0;

        foreach (var note in _detectedNotes)
        {
            double beatPosition = note.StartTime * beatsPerSecond;
            double beatDuration = note.Duration * beatsPerSecond;

            pattern.Events.Add(new NoteEvent
            {
                Note = note.MidiNote,
                Velocity = note.Velocity,
                Beat = beatPosition,
                Duration = beatDuration
            });

            double endBeat = beatPosition + beatDuration;
            if (endBeat > maxBeat)
                maxBeat = endBeat;
        }

        // Set loop length to cover all notes
        pattern.LoopLength = Math.Ceiling(maxBeat) + 1;

        return pattern;
    }

    /// <summary>
    /// Processes audio samples for real-time MIDI conversion.
    /// Implements the IAnalyzer interface.
    /// </summary>
    public void ProcessSamples(float[] samples, int offset, int count, int channels)
    {
        for (int i = offset; i < offset + count; i += channels)
        {
            // Mix to mono
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

                // Update current time
                _currentTime += (double)_hopSize / _sampleRate;
            }
        }
    }

    /// <summary>
    /// Resets the converter state.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            Array.Clear(_frameBuffer, 0, _frameBuffer.Length);
            Array.Clear(_energyHistory, 0, _energyHistory.Length);
            Array.Clear(_yinBuffer, 0, _yinBuffer.Length);
            _frameBufferPosition = 0;
            _energyHistoryPosition = 0;
            _previousEnergy = 0;
            _currentTime = 0;
            _noteIsActive = false;
            _currentMidiNote = -1;
            _noteStartTime = 0;
            _noteEnergy = 0;
            _noteConfidence = 0;
            _noteFrequency = 0;
            _noteFrameCount = 0;
            _lastOnsetTime = double.NegativeInfinity;
            _detectedNotes.Clear();
        }
    }

    /// <summary>
    /// Clears detected notes but keeps processing state.
    /// </summary>
    public void ClearNotes()
    {
        lock (_lock)
        {
            _detectedNotes.Clear();
        }
    }

    private void ProcessFrame()
    {
        // Calculate frame energy (RMS)
        float energy = CalculateRmsEnergy(_frameBuffer, _frameSize);

        // Detect onset using spectral flux / energy-based detection
        bool onset = DetectOnset(energy);

        // Detect pitch using YIN algorithm
        var (frequency, confidence) = DetectPitchYin(_frameBuffer, _frameSize);

        // Convert frequency to MIDI note
        int midiNote = FrequencyToMidiNote(frequency);

        // Process note tracking
        ProcessNoteTracking(onset, midiNote, frequency, confidence, energy);

        _previousEnergy = energy;
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

    private bool DetectOnset(float energy)
    {
        // Calculate local average and standard deviation of energy
        float avgEnergy = 0;
        float varEnergy = 0;

        for (int i = 0; i < EnergyHistorySize; i++)
        {
            avgEnergy += _energyHistory[i];
        }
        avgEnergy /= EnergyHistorySize;

        for (int i = 0; i < EnergyHistorySize; i++)
        {
            float diff = _energyHistory[i] - avgEnergy;
            varEnergy += diff * diff;
        }
        varEnergy = (float)Math.Sqrt(varEnergy / EnergyHistorySize);

        // Store current energy in history
        _energyHistory[_energyHistoryPosition] = energy;
        _energyHistoryPosition = (_energyHistoryPosition + 1) % EnergyHistorySize;

        // Spectral flux (simplified using energy difference)
        float flux = Math.Max(0, energy - _previousEnergy);

        // Adaptive threshold based on local statistics
        float adaptiveThreshold = avgEnergy + _onsetThreshold * Math.Max(varEnergy, 0.001f);
        adaptiveThreshold /= _onsetSensitivity;

        // Minimum time between onsets
        double timeSinceLastOnset = _currentTime - _lastOnsetTime;
        bool minTimePassed = timeSinceLastOnset >= _minNoteDuration;

        // Detect onset
        if (flux > adaptiveThreshold && energy > 0.001f && minTimePassed)
        {
            _lastOnsetTime = _currentTime;
            return true;
        }

        return false;
    }

    /// <summary>
    /// YIN pitch detection algorithm.
    /// Detects the fundamental frequency of a signal using autocorrelation
    /// with cumulative mean normalized difference function.
    /// </summary>
    private (float frequency, float confidence) DetectPitchYin(float[] buffer, int length)
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
            _yinBuffer[tau] = _yinBuffer[tau] * tau / runningSum;
        }

        // Step 3: Absolute threshold
        // Find the smallest tau where d'(tau) < threshold
        int tauEstimate = -1;
        float minValue = float.MaxValue;

        // Calculate lag range from frequency limits
        int minTau = Math.Max(2, (int)(_sampleRate / _maxFrequency));
        int maxTau = Math.Min(_yinBufferSize - 1, (int)(_sampleRate / _minFrequency));

        // YIN threshold (typical value is 0.1 to 0.15)
        const float yinThreshold = 0.1f;

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

        // Step 4: Parabolic interpolation for more accurate estimate
        float betterTau = tauEstimate;
        if (tauEstimate > 0 && tauEstimate < _yinBufferSize - 1)
        {
            float s0 = _yinBuffer[tauEstimate - 1];
            float s1 = _yinBuffer[tauEstimate];
            float s2 = _yinBuffer[tauEstimate + 1];
            float adjustment = (s2 - s0) / (2 * (2 * s1 - s2 - s0));
            if (Math.Abs(adjustment) < 1)
            {
                betterTau = tauEstimate + adjustment;
            }
        }

        // Calculate frequency
        float frequency = _sampleRate / betterTau;

        // Calculate confidence (1 - d'(tau))
        float confidence = 1.0f - _yinBuffer[tauEstimate];
        confidence = Math.Clamp(confidence, 0, 1);

        // Validate frequency range
        if (frequency < _minFrequency || frequency > _maxFrequency)
        {
            return (0, 0);
        }

        return (frequency, confidence);
    }

    /// <summary>
    /// Converts a frequency in Hz to the nearest MIDI note number.
    /// </summary>
    private int FrequencyToMidiNote(float frequency)
    {
        if (frequency <= 0)
            return -1;

        // MIDI note 69 = A4 = 440 Hz
        // midiNote = 69 + 12 * log2(f / 440)
        double midiNote = 69 + 12 * Math.Log2(frequency / 440.0);
        int rounded = (int)Math.Round(midiNote);

        return Math.Clamp(rounded, 0, 127);
    }

    /// <summary>
    /// Converts a MIDI note number to frequency in Hz.
    /// </summary>
    public static float MidiNoteToFrequency(int midiNote)
    {
        // f = 440 * 2^((midiNote - 69) / 12)
        return 440f * (float)Math.Pow(2, (midiNote - 69) / 12.0);
    }

    private void ProcessNoteTracking(bool onset, int midiNote, float frequency, float confidence, float energy)
    {
        bool validPitch = confidence >= _pitchConfidenceThreshold && midiNote >= 0;

        if (_noteIsActive)
        {
            // Check for note end conditions:
            // 1. Energy dropped significantly
            // 2. New onset detected (new note starting)
            // 3. Pitch changed significantly
            bool energyDropped = energy < _noteEnergy * _releaseThreshold;
            bool pitchChanged = validPitch && Math.Abs(midiNote - _currentMidiNote) > 1;

            if (energyDropped || (onset && (pitchChanged || !validPitch)))
            {
                // End current note
                FinalizeCurrentNote();
            }
            else if (validPitch)
            {
                // Update running average of note parameters
                _noteFrameCount++;
                _noteEnergy = Math.Max(_noteEnergy, energy);
                _noteConfidence = (_noteConfidence * (_noteFrameCount - 1) + confidence) / _noteFrameCount;
                _noteFrequency = (_noteFrequency * (_noteFrameCount - 1) + frequency) / _noteFrameCount;
            }
        }

        // Check for new note start
        if (!_noteIsActive && onset && validPitch)
        {
            // Start new note
            _noteIsActive = true;
            _currentMidiNote = midiNote;
            _noteStartTime = _currentTime;
            _noteEnergy = energy;
            _noteConfidence = confidence;
            _noteFrequency = frequency;
            _noteFrameCount = 1;

            // Calculate velocity from energy
            int velocity = CalculateVelocity(energy);

            // Raise real-time event
            NoteDetected?.Invoke(this, new NoteDetectedEventArgs(
                midiNote, velocity, _currentTime, 0, confidence, frequency));
        }
    }

    private void FinalizeCurrentNote()
    {
        if (!_noteIsActive)
            return;

        double duration = _currentTime - _noteStartTime;

        // Only keep notes that meet minimum duration
        if (duration >= _minNoteDuration)
        {
            int velocity = CalculateVelocity(_noteEnergy);

            var detectedNote = new DetectedNote
            {
                MidiNote = _currentMidiNote,
                Velocity = velocity,
                StartTime = _noteStartTime,
                EndTime = _currentTime,
                Confidence = _noteConfidence,
                Frequency = _noteFrequency,
                Energy = _noteEnergy
            };

            lock (_lock)
            {
                _detectedNotes.Add(detectedNote);
            }

            // Raise real-time event for note end
            NoteEnded?.Invoke(this, new NoteDetectedEventArgs(
                _currentMidiNote, velocity, _noteStartTime, duration, _noteConfidence, _noteFrequency));
        }

        _noteIsActive = false;
        _currentMidiNote = -1;
    }

    private int CalculateVelocity(float energy)
    {
        // Convert RMS energy to velocity (0-127)
        // Using a logarithmic scale for more natural dynamics
        // energy of ~0.001 -> velocity 1
        // energy of ~0.5 -> velocity 127
        if (energy <= 0)
            return 0;

        double db = 20 * Math.Log10(energy + 1e-10);
        // Map from approximately -60dB to 0dB to velocity 1-127
        double normalizedDb = (db + 60) / 60;
        normalizedDb = Math.Clamp(normalizedDb, 0, 1);
        int velocity = (int)(normalizedDb * 126 * _velocityScale) + 1;
        return Math.Clamp(velocity, 1, 127);
    }
}

/// <summary>
/// Internal dummy synth for creating patterns when no synth is specified.
/// </summary>
internal class DummySynth : ISynth
{
    public string Name { get; set; } = "DummySynth";
    public WaveFormat WaveFormat => WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

    public void NoteOn(int note, int velocity) { }
    public void NoteOff(int note) { }
    public void AllNotesOff() { }
    public void SetParameter(string name, float value) { }

    public int Read(float[] buffer, int offset, int count)
    {
        Array.Clear(buffer, offset, count);
        return count;
    }
}
