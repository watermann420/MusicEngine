// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio analysis component.

using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Represents the type of drum sound detected.
/// </summary>
public enum DrumType
{
    /// <summary>Bass drum / Kick.</summary>
    Kick,

    /// <summary>Snare drum.</summary>
    Snare,

    /// <summary>Hi-hat (open or closed).</summary>
    HiHat,

    /// <summary>High tom.</summary>
    TomHigh,

    /// <summary>Mid tom.</summary>
    TomMid,

    /// <summary>Low / Floor tom.</summary>
    TomLow,

    /// <summary>Crash cymbal.</summary>
    Crash,

    /// <summary>Ride cymbal.</summary>
    Ride,

    /// <summary>Unknown or unclassified percussion.</summary>
    Unknown
}

/// <summary>
/// Represents a detected drum hit event.
/// </summary>
public class DrumHit
{
    /// <summary>Type of drum detected.</summary>
    public DrumType DrumType { get; set; }

    /// <summary>Time position in seconds.</summary>
    public double TimeSeconds { get; set; }

    /// <summary>Velocity (0-127) derived from amplitude.</summary>
    public int Velocity { get; set; }

    /// <summary>MIDI note number based on note mapping.</summary>
    public int MidiNote { get; set; }

    /// <summary>Confidence level of the detection (0.0 to 1.0).</summary>
    public float Confidence { get; set; }

    /// <summary>Amplitude (RMS energy) of the detected hit.</summary>
    public float Amplitude { get; set; }

    /// <summary>Duration of the detected hit in seconds.</summary>
    public double Duration { get; set; } = 0.1;

    /// <summary>Time position in milliseconds.</summary>
    public double TimeMs => TimeSeconds * 1000.0;

    public override string ToString() =>
        $"{DrumType} @ {TimeSeconds:F3}s (Note: {MidiNote}, Vel: {Velocity})";
}

/// <summary>
/// Configuration for drum detection thresholds per drum type.
/// </summary>
public class DrumDetectionConfig
{
    /// <summary>Energy threshold for detection (0.0 to 1.0).</summary>
    public float Threshold { get; set; } = 0.3f;

    /// <summary>Minimum frequency band for this drum type (Hz).</summary>
    public float MinFrequency { get; set; }

    /// <summary>Maximum frequency band for this drum type (Hz).</summary>
    public float MaxFrequency { get; set; }

    /// <summary>Default MIDI note for this drum type.</summary>
    public int DefaultMidiNote { get; set; }

    /// <summary>Minimum time between consecutive hits in seconds.</summary>
    public double MinIntervalSeconds { get; set; } = 0.03;

    /// <summary>Whether this drum type is enabled for detection.</summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Converts drum audio to MIDI by analyzing frequency bands and detecting onsets.
/// Supports detection of kick, snare, hi-hat, toms, and cymbals.
/// </summary>
public class DrumToMidiConverter : IAnalyzer
{
    private readonly int _sampleRate;
    private readonly int _frameSize;
    private readonly int _hopSize;
    private readonly float[] _frameBuffer;
    private int _frameBufferPosition;
    private double _currentTime;
    private readonly object _lock = new();

    // FFT buffers
    private readonly float[] _fftMagnitude;
    private readonly Complex[] _fftBuffer;
    private readonly float[] _window;

    // Band energy tracking
    private readonly Dictionary<DrumType, float[]> _energyHistory;
    private readonly Dictionary<DrumType, int> _energyHistoryPosition;
    private readonly Dictionary<DrumType, double> _lastHitTime;
    private const int EnergyHistorySize = 20;

    // Detection results
    private readonly List<DrumHit> _detectedHits = new();

    // Configuration
    private readonly Dictionary<DrumType, DrumDetectionConfig> _drumConfigs;
    private readonly Dictionary<DrumType, int> _noteMappings;

    /// <summary>
    /// Gets or sets whether detection is enabled globally.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the overall sensitivity multiplier (0.5 to 2.0).
    /// Higher values detect weaker hits.
    /// </summary>
    public float Sensitivity { get; set; } = 1.0f;

    /// <summary>
    /// Gets the detected drum hits.
    /// </summary>
    public IReadOnlyList<DrumHit> DetectedHits
    {
        get
        {
            lock (_lock)
            {
                return new List<DrumHit>(_detectedHits);
            }
        }
    }

    /// <summary>
    /// Event raised when a drum hit is detected.
    /// </summary>
    public event EventHandler<DrumHit>? DrumHitDetected;

    /// <summary>
    /// Creates a new drum-to-MIDI converter with default settings.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate (default: 44100 Hz).</param>
    /// <param name="frameSize">FFT frame size (default: 2048).</param>
    /// <param name="hopSize">Hop size in samples (default: 512).</param>
    public DrumToMidiConverter(int sampleRate = 44100, int frameSize = 2048, int hopSize = 512)
    {
        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "Sample rate must be positive.");
        if (frameSize <= 0 || (frameSize & (frameSize - 1)) != 0)
            throw new ArgumentOutOfRangeException(nameof(frameSize), "Frame size must be a positive power of 2.");
        if (hopSize <= 0 || hopSize > frameSize)
            throw new ArgumentOutOfRangeException(nameof(hopSize), "Hop size must be positive and <= frame size.");

        _sampleRate = sampleRate;
        _frameSize = frameSize;
        _hopSize = hopSize;
        _frameBuffer = new float[frameSize];
        _fftMagnitude = new float[frameSize / 2 + 1];
        _fftBuffer = new Complex[frameSize];

        // Hann window
        _window = new float[frameSize];
        for (int i = 0; i < frameSize; i++)
        {
            _window[i] = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (frameSize - 1)));
        }

        // Initialize drum configurations with default frequency bands
        _drumConfigs = new Dictionary<DrumType, DrumDetectionConfig>
        {
            [DrumType.Kick] = new DrumDetectionConfig
            {
                MinFrequency = 20f,
                MaxFrequency = 150f,
                DefaultMidiNote = 36, // GM Kick
                Threshold = 0.4f,
                MinIntervalSeconds = 0.05
            },
            [DrumType.Snare] = new DrumDetectionConfig
            {
                MinFrequency = 150f,
                MaxFrequency = 400f,
                DefaultMidiNote = 38, // GM Snare
                Threshold = 0.35f,
                MinIntervalSeconds = 0.04
            },
            [DrumType.HiHat] = new DrumDetectionConfig
            {
                MinFrequency = 6000f,
                MaxFrequency = 16000f,
                DefaultMidiNote = 42, // GM Closed Hi-Hat
                Threshold = 0.25f,
                MinIntervalSeconds = 0.02
            },
            [DrumType.TomHigh] = new DrumDetectionConfig
            {
                MinFrequency = 200f,
                MaxFrequency = 600f,
                DefaultMidiNote = 50, // GM High Tom
                Threshold = 0.35f,
                MinIntervalSeconds = 0.05
            },
            [DrumType.TomMid] = new DrumDetectionConfig
            {
                MinFrequency = 100f,
                MaxFrequency = 400f,
                DefaultMidiNote = 47, // GM Low-Mid Tom
                Threshold = 0.35f,
                MinIntervalSeconds = 0.05
            },
            [DrumType.TomLow] = new DrumDetectionConfig
            {
                MinFrequency = 60f,
                MaxFrequency = 250f,
                DefaultMidiNote = 45, // GM Low Tom
                Threshold = 0.35f,
                MinIntervalSeconds = 0.05
            },
            [DrumType.Crash] = new DrumDetectionConfig
            {
                MinFrequency = 4000f,
                MaxFrequency = 12000f,
                DefaultMidiNote = 49, // GM Crash 1
                Threshold = 0.3f,
                MinIntervalSeconds = 0.1
            },
            [DrumType.Ride] = new DrumDetectionConfig
            {
                MinFrequency = 3000f,
                MaxFrequency = 8000f,
                DefaultMidiNote = 51, // GM Ride
                Threshold = 0.3f,
                MinIntervalSeconds = 0.03
            }
        };

        // Initialize note mappings (default GM drum map)
        _noteMappings = new Dictionary<DrumType, int>
        {
            [DrumType.Kick] = 36,
            [DrumType.Snare] = 38,
            [DrumType.HiHat] = 42,
            [DrumType.TomHigh] = 50,
            [DrumType.TomMid] = 47,
            [DrumType.TomLow] = 45,
            [DrumType.Crash] = 49,
            [DrumType.Ride] = 51,
            [DrumType.Unknown] = 37 // Side Stick
        };

        // Initialize energy tracking
        _energyHistory = new Dictionary<DrumType, float[]>();
        _energyHistoryPosition = new Dictionary<DrumType, int>();
        _lastHitTime = new Dictionary<DrumType, double>();

        foreach (var drumType in _drumConfigs.Keys)
        {
            _energyHistory[drumType] = new float[EnergyHistorySize];
            _energyHistoryPosition[drumType] = 0;
            _lastHitTime[drumType] = double.NegativeInfinity;
        }
    }

    /// <summary>
    /// Gets the detection configuration for a specific drum type.
    /// </summary>
    /// <param name="drumType">The drum type.</param>
    /// <returns>The configuration for this drum type.</returns>
    public DrumDetectionConfig GetConfig(DrumType drumType)
    {
        return _drumConfigs.TryGetValue(drumType, out var config)
            ? config
            : new DrumDetectionConfig();
    }

    /// <summary>
    /// Sets the detection threshold for a specific drum type.
    /// </summary>
    /// <param name="drumType">The drum type.</param>
    /// <param name="threshold">Threshold value (0.0 to 1.0).</param>
    public void SetThreshold(DrumType drumType, float threshold)
    {
        if (_drumConfigs.TryGetValue(drumType, out var config))
        {
            config.Threshold = Math.Clamp(threshold, 0f, 1f);
        }
    }

    /// <summary>
    /// Sets the MIDI note mapping for a specific drum type.
    /// </summary>
    /// <param name="drumType">The drum type.</param>
    /// <param name="midiNote">MIDI note number (0-127).</param>
    public void SetNoteMapping(DrumType drumType, int midiNote)
    {
        _noteMappings[drumType] = Math.Clamp(midiNote, 0, 127);
        if (_drumConfigs.TryGetValue(drumType, out var config))
        {
            config.DefaultMidiNote = midiNote;
        }
    }

    /// <summary>
    /// Gets the MIDI note for a specific drum type.
    /// </summary>
    /// <param name="drumType">The drum type.</param>
    /// <returns>The mapped MIDI note number.</returns>
    public int GetMidiNote(DrumType drumType)
    {
        return _noteMappings.TryGetValue(drumType, out var note) ? note : 37;
    }

    /// <summary>
    /// Enables or disables detection for a specific drum type.
    /// </summary>
    /// <param name="drumType">The drum type.</param>
    /// <param name="enabled">Whether detection is enabled.</param>
    public void SetDrumEnabled(DrumType drumType, bool enabled)
    {
        if (_drumConfigs.TryGetValue(drumType, out var config))
        {
            config.Enabled = enabled;
        }
    }

    /// <summary>
    /// Processes audio samples for drum detection.
    /// </summary>
    /// <param name="samples">Audio samples.</param>
    /// <param name="offset">Offset into the buffer.</param>
    /// <param name="count">Number of samples to process.</param>
    /// <param name="channels">Number of audio channels.</param>
    public void ProcessSamples(float[] samples, int offset, int count, int channels)
    {
        if (!Enabled) return;

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
    /// Analyzes a complete audio buffer and returns all detected drum hits.
    /// </summary>
    /// <param name="samples">Complete audio buffer (mono).</param>
    /// <param name="sampleRate">Sample rate of the audio.</param>
    /// <returns>List of detected drum hits.</returns>
    public List<DrumHit> AnalyzeBuffer(float[] samples, int sampleRate)
    {
        Reset();
        ProcessSamples(samples, 0, samples.Length, 1);

        lock (_lock)
        {
            return new List<DrumHit>(_detectedHits);
        }
    }

    /// <summary>
    /// Converts detected drum hits to NoteEvent objects.
    /// </summary>
    /// <param name="bpm">Tempo in beats per minute.</param>
    /// <returns>List of note events.</returns>
    public List<NoteEvent> ToNoteEvents(double bpm)
    {
        var events = new List<NoteEvent>();
        double beatsPerSecond = bpm / 60.0;

        lock (_lock)
        {
            foreach (var hit in _detectedHits)
            {
                events.Add(new NoteEvent
                {
                    Note = hit.MidiNote,
                    Velocity = hit.Velocity,
                    Beat = hit.TimeSeconds * beatsPerSecond,
                    Duration = hit.Duration * beatsPerSecond
                });
            }
        }

        return events.OrderBy(e => e.Beat).ToList();
    }

    /// <summary>
    /// Converts detected drum hits to a Pattern.
    /// </summary>
    /// <param name="bpm">Tempo in beats per minute.</param>
    /// <param name="synth">Optional synthesizer for the pattern.</param>
    /// <returns>A pattern containing the drum hits.</returns>
    public Pattern ToPattern(double bpm, ISynth? synth = null)
    {
        var events = ToNoteEvents(bpm);

        var pattern = new Pattern(synth ?? new DummySynth())
        {
            Name = "Drum-to-MIDI",
            IsLooping = false
        };

        foreach (var ev in events)
        {
            pattern.Events.Add(ev);
        }

        if (events.Count > 0)
        {
            pattern.LoopLength = events.Max(e => e.Beat + e.Duration) + 1;
        }

        return pattern;
    }

    /// <summary>
    /// Resets the detector state.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            Array.Clear(_frameBuffer, 0, _frameBuffer.Length);
            _frameBufferPosition = 0;
            _currentTime = 0;
            _detectedHits.Clear();

            foreach (var drumType in _drumConfigs.Keys)
            {
                Array.Clear(_energyHistory[drumType], 0, EnergyHistorySize);
                _energyHistoryPosition[drumType] = 0;
                _lastHitTime[drumType] = double.NegativeInfinity;
            }
        }
    }

    /// <summary>
    /// Clears detected hits but keeps detector state.
    /// </summary>
    public void ClearHits()
    {
        lock (_lock)
        {
            _detectedHits.Clear();
        }
    }

    private void ProcessFrame()
    {
        // Apply window and perform FFT
        for (int i = 0; i < _frameSize; i++)
        {
            _fftBuffer[i] = new Complex(_frameBuffer[i] * _window[i], 0f);
        }

        FFT(_fftBuffer, false);

        // Calculate magnitude spectrum
        int halfSize = _frameSize / 2;
        for (int i = 0; i <= halfSize; i++)
        {
            _fftMagnitude[i] = MathF.Sqrt(_fftBuffer[i].Real * _fftBuffer[i].Real +
                                          _fftBuffer[i].Imag * _fftBuffer[i].Imag);
        }

        // Detect each drum type
        foreach (var kvp in _drumConfigs)
        {
            var drumType = kvp.Key;
            var config = kvp.Value;

            if (!config.Enabled) continue;

            DetectDrumType(drumType, config);
        }
    }

    private void DetectDrumType(DrumType drumType, DrumDetectionConfig config)
    {
        // Calculate band energy
        float bandEnergy = CalculateBandEnergy(config.MinFrequency, config.MaxFrequency);

        // Get energy history
        var history = _energyHistory[drumType];
        int historyPos = _energyHistoryPosition[drumType];

        // Calculate adaptive threshold based on history
        float avgEnergy = 0f;
        float maxEnergy = 0f;
        for (int i = 0; i < EnergyHistorySize; i++)
        {
            avgEnergy += history[i];
            if (history[i] > maxEnergy) maxEnergy = history[i];
        }
        avgEnergy /= EnergyHistorySize;

        // Store current energy
        history[historyPos] = bandEnergy;
        _energyHistoryPosition[drumType] = (historyPos + 1) % EnergyHistorySize;

        // Adaptive threshold
        float threshold = config.Threshold / Sensitivity;
        float adaptiveThreshold = avgEnergy + threshold * Math.Max(maxEnergy - avgEnergy, 0.001f);

        // Check for onset (energy spike above threshold)
        double timeSinceLastHit = _currentTime - _lastHitTime[drumType];
        bool minTimePassed = timeSinceLastHit >= config.MinIntervalSeconds;

        if (bandEnergy > adaptiveThreshold && bandEnergy > 0.001f && minTimePassed)
        {
            // Calculate velocity from amplitude
            int velocity = CalculateVelocity(bandEnergy, avgEnergy, maxEnergy);

            // Calculate confidence based on how much above threshold
            float confidence = Math.Min(1f, bandEnergy / (adaptiveThreshold * 2f));

            var hit = new DrumHit
            {
                DrumType = drumType,
                TimeSeconds = _currentTime,
                Velocity = velocity,
                MidiNote = _noteMappings[drumType],
                Confidence = confidence,
                Amplitude = bandEnergy,
                Duration = GetDefaultDuration(drumType)
            };

            lock (_lock)
            {
                _detectedHits.Add(hit);
                _lastHitTime[drumType] = _currentTime;
            }

            DrumHitDetected?.Invoke(this, hit);
        }
    }

    private float CalculateBandEnergy(float minFreq, float maxFreq)
    {
        float freqPerBin = (float)_sampleRate / _frameSize;
        int minBin = Math.Max(1, (int)(minFreq / freqPerBin));
        int maxBin = Math.Min(_frameSize / 2, (int)(maxFreq / freqPerBin));

        if (maxBin <= minBin) return 0f;

        float energy = 0f;
        for (int i = minBin; i <= maxBin; i++)
        {
            energy += _fftMagnitude[i] * _fftMagnitude[i];
        }

        return MathF.Sqrt(energy / (maxBin - minBin + 1));
    }

    private int CalculateVelocity(float energy, float avgEnergy, float maxEnergy)
    {
        // Logarithmic scaling for natural dynamics
        if (energy <= 0) return 0;

        float normalizedEnergy = (energy - avgEnergy) / Math.Max(maxEnergy - avgEnergy, 0.001f);
        normalizedEnergy = Math.Clamp(normalizedEnergy, 0f, 1f);

        // Apply curve for better dynamic range
        float curved = MathF.Pow(normalizedEnergy, 0.7f);

        int velocity = (int)(curved * 126) + 1;
        return Math.Clamp(velocity, 1, 127);
    }

    private static double GetDefaultDuration(DrumType drumType)
    {
        return drumType switch
        {
            DrumType.Kick => 0.15,
            DrumType.Snare => 0.12,
            DrumType.HiHat => 0.05,
            DrumType.TomHigh => 0.1,
            DrumType.TomMid => 0.12,
            DrumType.TomLow => 0.15,
            DrumType.Crash => 0.5,
            DrumType.Ride => 0.2,
            _ => 0.1
        };
    }

    /// <summary>
    /// In-place Cooley-Tukey FFT implementation.
    /// </summary>
    private static void FFT(Complex[] data, bool inverse)
    {
        int n = data.Length;
        if (n <= 1) return;

        // Bit-reversal permutation
        int j = 0;
        for (int i = 0; i < n - 1; i++)
        {
            if (i < j)
            {
                (data[i], data[j]) = (data[j], data[i]);
            }
            int m = n >> 1;
            while (j >= m && m >= 1)
            {
                j -= m;
                m >>= 1;
            }
            j += m;
        }

        // Cooley-Tukey iterative FFT
        float direction = inverse ? 1f : -1f;
        for (int len = 2; len <= n; len <<= 1)
        {
            float theta = direction * 2f * MathF.PI / len;
            Complex wn = new Complex(MathF.Cos(theta), MathF.Sin(theta));

            for (int i = 0; i < n; i += len)
            {
                Complex w = new Complex(1f, 0f);
                int halfLen = len / 2;
                for (int k = 0; k < halfLen; k++)
                {
                    Complex t = w * data[i + k + halfLen];
                    Complex u = data[i + k];
                    data[i + k] = u + t;
                    data[i + k + halfLen] = u - t;
                    w = w * wn;
                }
            }
        }

        // Scale for inverse FFT
        if (inverse)
        {
            for (int i = 0; i < n; i++)
            {
                data[i] = new Complex(data[i].Real / n, data[i].Imag / n);
            }
        }
    }

    /// <summary>
    /// Simple complex number struct for FFT operations.
    /// </summary>
    private readonly struct Complex
    {
        public readonly float Real;
        public readonly float Imag;

        public Complex(float real, float imag)
        {
            Real = real;
            Imag = imag;
        }

        public static Complex operator +(Complex a, Complex b) =>
            new Complex(a.Real + b.Real, a.Imag + b.Imag);

        public static Complex operator -(Complex a, Complex b) =>
            new Complex(a.Real - b.Real, a.Imag - b.Imag);

        public static Complex operator *(Complex a, Complex b) =>
            new Complex(
                a.Real * b.Real - a.Imag * b.Imag,
                a.Real * b.Imag + a.Imag * b.Real);
    }
}
