//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Drum hit detection and replacement tool with sample layering, velocity scaling, and MIDI export.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NAudio.Wave;

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Blend mode for drum replacement.
/// </summary>
public enum DrumBlendMode
{
    /// <summary>Completely replace original audio with samples.</summary>
    Replace,

    /// <summary>Layer samples on top of original audio.</summary>
    Layer,

    /// <summary>Duck original audio when samples play.</summary>
    Duck
}

/// <summary>
/// Velocity curve type for mapping detected amplitude to MIDI velocity.
/// </summary>
public enum VelocityCurveType
{
    /// <summary>Linear mapping from amplitude to velocity.</summary>
    Linear,

    /// <summary>Logarithmic curve for more natural dynamics.</summary>
    Logarithmic,

    /// <summary>Exponential curve for aggressive dynamics.</summary>
    Exponential,

    /// <summary>S-curve for compressed dynamics.</summary>
    SCurve,

    /// <summary>Hard curve with more contrast.</summary>
    Hard,

    /// <summary>Soft curve with less contrast.</summary>
    Soft
}

/// <summary>
/// Represents a detected drum trigger event.
/// </summary>
public class DrumTrigger
{
    /// <summary>Time position in seconds from the start.</summary>
    public double TimeSeconds { get; set; }

    /// <summary>Time position in milliseconds.</summary>
    public double TimeMs => TimeSeconds * 1000.0;

    /// <summary>Detected drum type.</summary>
    public DrumType DrumType { get; set; }

    /// <summary>MIDI velocity (0-127) derived from amplitude.</summary>
    public int Velocity { get; set; }

    /// <summary>Raw amplitude of the detected hit.</summary>
    public float Amplitude { get; set; }

    /// <summary>Detection confidence (0.0 to 1.0).</summary>
    public float Confidence { get; set; }

    /// <summary>MIDI note number for this trigger.</summary>
    public int MidiNote { get; set; }

    /// <summary>Index of the replacement sample to use (for round-robin).</summary>
    public int SampleIndex { get; set; }

    /// <summary>Sample start offset in seconds.</summary>
    public double SampleStartOffset { get; set; }

    /// <summary>Humanization timing offset in seconds.</summary>
    public double HumanizeOffset { get; set; }

    /// <summary>Frequency band energy values used for classification.</summary>
    public float[]? BandEnergies { get; set; }

    /// <summary>Whether this trigger has been processed for replacement.</summary>
    public bool Processed { get; set; }

    public override string ToString() =>
        $"{DrumType} @ {TimeSeconds:F3}s (Vel: {Velocity}, Conf: {Confidence:F2})";
}

/// <summary>
/// Configuration for a frequency-based trigger zone.
/// </summary>
public class TriggerZone
{
    /// <summary>Drum type this zone detects.</summary>
    public DrumType DrumType { get; set; }

    /// <summary>Minimum frequency in Hz.</summary>
    public float MinFrequency { get; set; }

    /// <summary>Maximum frequency in Hz.</summary>
    public float MaxFrequency { get; set; }

    /// <summary>Detection threshold (0.0 to 1.0).</summary>
    public float Threshold { get; set; } = 0.3f;

    /// <summary>Whether this zone is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>MIDI note mapping for this zone.</summary>
    public int MidiNote { get; set; }

    /// <summary>Minimum retrigger time in seconds.</summary>
    public double RetriggerTime { get; set; } = 0.03;
}

/// <summary>
/// Replacement sample configuration.
/// </summary>
public class ReplacementSample
{
    /// <summary>Sample name or identifier.</summary>
    public string Name { get; set; } = "";

    /// <summary>File path to the sample.</summary>
    public string FilePath { get; set; } = "";

    /// <summary>Loaded audio data (stereo interleaved).</summary>
    public float[]? AudioData { get; set; }

    /// <summary>Sample rate of the audio.</summary>
    public int SampleRate { get; set; } = 44100;

    /// <summary>Number of channels.</summary>
    public int Channels { get; set; } = 2;

    /// <summary>Volume multiplier for this sample.</summary>
    public float Volume { get; set; } = 1.0f;

    /// <summary>Minimum velocity this sample responds to (for velocity layers).</summary>
    public int MinVelocity { get; set; } = 0;

    /// <summary>Maximum velocity this sample responds to (for velocity layers).</summary>
    public int MaxVelocity { get; set; } = 127;

    /// <summary>Priority for round-robin selection.</summary>
    public int Priority { get; set; } = 0;
}

/// <summary>
/// Sample library for a specific drum type.
/// </summary>
public class DrumSampleLibrary
{
    /// <summary>Drum type this library is for.</summary>
    public DrumType DrumType { get; set; }

    /// <summary>List of replacement samples.</summary>
    public List<ReplacementSample> Samples { get; } = new();

    /// <summary>Current round-robin index.</summary>
    public int RoundRobinIndex { get; set; }

    /// <summary>Whether round-robin is enabled.</summary>
    public bool RoundRobinEnabled { get; set; } = true;

    /// <summary>Gets the next sample using round-robin or velocity selection.</summary>
    public ReplacementSample? GetSample(int velocity)
    {
        if (Samples.Count == 0) return null;

        // Filter by velocity range
        var eligible = Samples.Where(s => velocity >= s.MinVelocity && velocity <= s.MaxVelocity).ToList();
        if (eligible.Count == 0)
        {
            // Fallback to any sample
            eligible = Samples;
        }

        if (eligible.Count == 1) return eligible[0];

        if (RoundRobinEnabled)
        {
            RoundRobinIndex = (RoundRobinIndex + 1) % eligible.Count;
            return eligible[RoundRobinIndex];
        }

        // Random selection
        return eligible[Random.Shared.Next(eligible.Count)];
    }
}

/// <summary>
/// Learn mode result containing auto-detected thresholds.
/// </summary>
public class LearnModeResult
{
    /// <summary>Learned thresholds per drum type.</summary>
    public Dictionary<DrumType, float> Thresholds { get; } = new();

    /// <summary>Detected hits during learning.</summary>
    public List<DrumTrigger> DetectedHits { get; } = new();

    /// <summary>Average amplitude per drum type.</summary>
    public Dictionary<DrumType, float> AverageAmplitudes { get; } = new();

    /// <summary>Peak amplitude per drum type.</summary>
    public Dictionary<DrumType, float> PeakAmplitudes { get; } = new();

    /// <summary>Whether learning was successful.</summary>
    public bool Success { get; set; }

    /// <summary>Learning duration in seconds.</summary>
    public double DurationSeconds { get; set; }
}

/// <summary>
/// Drum hit detection and replacement tool.
/// Analyzes audio to detect drum hits, classifies them by type,
/// and can replace or layer with sample libraries.
/// </summary>
public class DrumReplacer : IAnalyzer
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

    // Trigger zones
    private readonly Dictionary<DrumType, TriggerZone> _triggerZones;
    private readonly Dictionary<DrumType, float[]> _energyHistory;
    private readonly Dictionary<DrumType, int> _energyHistoryPosition;
    private readonly Dictionary<DrumType, double> _lastTriggerTime;
    private const int EnergyHistorySize = 20;

    // Detection results
    private readonly List<DrumTrigger> _triggers = new();

    // Sample libraries
    private readonly Dictionary<DrumType, DrumSampleLibrary> _sampleLibraries = new();

    // Sidechain output
    private readonly List<DrumTrigger> _sidechainTriggers = new();

    // Learn mode
    private bool _isLearning;
    private readonly List<(DrumType type, float amplitude)> _learnData = new();

    // Random for humanization
    private readonly Random _random = new();

    /// <summary>
    /// Gets or sets whether detection is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the overall sensitivity multiplier (0.5 to 2.0).
    /// Higher values detect weaker hits.
    /// </summary>
    public float Sensitivity { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the global threshold (0.0 to 1.0).
    /// </summary>
    public float Threshold { get; set; } = 0.3f;

    /// <summary>
    /// Gets or sets the minimum retrigger time in seconds.
    /// </summary>
    public double RetriggerTime { get; set; } = 0.03;

    /// <summary>
    /// Gets or sets the blend mode for replacement.
    /// </summary>
    public DrumBlendMode BlendMode { get; set; } = DrumBlendMode.Replace;

    /// <summary>
    /// Gets or sets the blend amount (0.0 to 1.0).
    /// For Layer mode: mix ratio. For Duck mode: duck amount.
    /// </summary>
    public float BlendAmount { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the velocity curve type.
    /// </summary>
    public VelocityCurveType VelocityCurve { get; set; } = VelocityCurveType.Logarithmic;

    /// <summary>
    /// Gets or sets the velocity scale multiplier.
    /// </summary>
    public float VelocityScale { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the minimum output velocity.
    /// </summary>
    public int MinVelocity { get; set; } = 1;

    /// <summary>
    /// Gets or sets the maximum output velocity.
    /// </summary>
    public int MaxVelocity { get; set; } = 127;

    /// <summary>
    /// Gets or sets the sample start offset in seconds.
    /// </summary>
    public double SampleStartOffset { get; set; } = 0.0;

    /// <summary>
    /// Gets or sets the humanization amount (0.0 to 1.0).
    /// </summary>
    public float HumanizeAmount { get; set; } = 0.0f;

    /// <summary>
    /// Gets or sets the maximum humanization timing offset in seconds.
    /// </summary>
    public double HumanizeMaxOffset { get; set; } = 0.01;

    /// <summary>
    /// Gets or sets whether round-robin is enabled globally.
    /// </summary>
    public bool RoundRobinEnabled { get; set; } = true;

    /// <summary>
    /// Gets the detected triggers.
    /// </summary>
    public IReadOnlyList<DrumTrigger> Triggers
    {
        get
        {
            lock (_lock)
            {
                return new List<DrumTrigger>(_triggers);
            }
        }
    }

    /// <summary>
    /// Gets the sidechain trigger output.
    /// </summary>
    public IReadOnlyList<DrumTrigger> SidechainTriggers
    {
        get
        {
            lock (_lock)
            {
                return new List<DrumTrigger>(_sidechainTriggers);
            }
        }
    }

    /// <summary>
    /// Event raised when a drum trigger is detected.
    /// </summary>
    public event EventHandler<DrumTrigger>? TriggerDetected;

    /// <summary>
    /// Event raised for sidechain triggers.
    /// </summary>
    public event EventHandler<DrumTrigger>? SidechainTriggered;

    /// <summary>
    /// Creates a new drum replacer with default settings.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate (default: 44100 Hz).</param>
    /// <param name="frameSize">FFT frame size (default: 2048).</param>
    /// <param name="hopSize">Hop size in samples (default: 512).</param>
    public DrumReplacer(int sampleRate = 44100, int frameSize = 2048, int hopSize = 512)
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

        // Initialize default trigger zones
        _triggerZones = new Dictionary<DrumType, TriggerZone>
        {
            [DrumType.Kick] = new TriggerZone
            {
                DrumType = DrumType.Kick,
                MinFrequency = 20f,
                MaxFrequency = 150f,
                MidiNote = 36,
                Threshold = 0.4f,
                RetriggerTime = 0.05
            },
            [DrumType.Snare] = new TriggerZone
            {
                DrumType = DrumType.Snare,
                MinFrequency = 150f,
                MaxFrequency = 400f,
                MidiNote = 38,
                Threshold = 0.35f,
                RetriggerTime = 0.04
            },
            [DrumType.HiHat] = new TriggerZone
            {
                DrumType = DrumType.HiHat,
                MinFrequency = 6000f,
                MaxFrequency = 16000f,
                MidiNote = 42,
                Threshold = 0.25f,
                RetriggerTime = 0.02
            },
            [DrumType.TomHigh] = new TriggerZone
            {
                DrumType = DrumType.TomHigh,
                MinFrequency = 200f,
                MaxFrequency = 600f,
                MidiNote = 50,
                Threshold = 0.35f,
                RetriggerTime = 0.05
            },
            [DrumType.TomMid] = new TriggerZone
            {
                DrumType = DrumType.TomMid,
                MinFrequency = 100f,
                MaxFrequency = 400f,
                MidiNote = 47,
                Threshold = 0.35f,
                RetriggerTime = 0.05
            },
            [DrumType.TomLow] = new TriggerZone
            {
                DrumType = DrumType.TomLow,
                MinFrequency = 60f,
                MaxFrequency = 250f,
                MidiNote = 45,
                Threshold = 0.35f,
                RetriggerTime = 0.05
            },
            [DrumType.Crash] = new TriggerZone
            {
                DrumType = DrumType.Crash,
                MinFrequency = 4000f,
                MaxFrequency = 12000f,
                MidiNote = 49,
                Threshold = 0.3f,
                RetriggerTime = 0.1
            },
            [DrumType.Ride] = new TriggerZone
            {
                DrumType = DrumType.Ride,
                MinFrequency = 3000f,
                MaxFrequency = 8000f,
                MidiNote = 51,
                Threshold = 0.3f,
                RetriggerTime = 0.03
            }
        };

        // Initialize energy tracking
        _energyHistory = new Dictionary<DrumType, float[]>();
        _energyHistoryPosition = new Dictionary<DrumType, int>();
        _lastTriggerTime = new Dictionary<DrumType, double>();

        foreach (var zone in _triggerZones.Values)
        {
            _energyHistory[zone.DrumType] = new float[EnergyHistorySize];
            _energyHistoryPosition[zone.DrumType] = 0;
            _lastTriggerTime[zone.DrumType] = double.NegativeInfinity;
            _sampleLibraries[zone.DrumType] = new DrumSampleLibrary { DrumType = zone.DrumType };
        }
    }

    #region Trigger Zone Configuration

    /// <summary>
    /// Gets the trigger zone configuration for a drum type.
    /// </summary>
    public TriggerZone? GetTriggerZone(DrumType drumType)
    {
        return _triggerZones.TryGetValue(drumType, out var zone) ? zone : null;
    }

    /// <summary>
    /// Sets the trigger zone configuration for a drum type.
    /// </summary>
    public void SetTriggerZone(DrumType drumType, TriggerZone zone)
    {
        zone.DrumType = drumType;
        _triggerZones[drumType] = zone;

        if (!_energyHistory.ContainsKey(drumType))
        {
            _energyHistory[drumType] = new float[EnergyHistorySize];
            _energyHistoryPosition[drumType] = 0;
            _lastTriggerTime[drumType] = double.NegativeInfinity;
        }

        if (!_sampleLibraries.ContainsKey(drumType))
        {
            _sampleLibraries[drumType] = new DrumSampleLibrary { DrumType = drumType };
        }
    }

    /// <summary>
    /// Sets the threshold for a specific drum type.
    /// </summary>
    public void SetThreshold(DrumType drumType, float threshold)
    {
        if (_triggerZones.TryGetValue(drumType, out var zone))
        {
            zone.Threshold = Math.Clamp(threshold, 0f, 1f);
        }
    }

    /// <summary>
    /// Enables or disables detection for a specific drum type.
    /// </summary>
    public void SetDrumEnabled(DrumType drumType, bool enabled)
    {
        if (_triggerZones.TryGetValue(drumType, out var zone))
        {
            zone.Enabled = enabled;
        }
    }

    /// <summary>
    /// Sets the MIDI note mapping for a drum type.
    /// </summary>
    public void SetMidiNote(DrumType drumType, int note)
    {
        if (_triggerZones.TryGetValue(drumType, out var zone))
        {
            zone.MidiNote = Math.Clamp(note, 0, 127);
        }
    }

    /// <summary>
    /// Sets the retrigger time for a specific drum type.
    /// </summary>
    public void SetRetriggerTime(DrumType drumType, double seconds)
    {
        if (_triggerZones.TryGetValue(drumType, out var zone))
        {
            zone.RetriggerTime = Math.Max(0.001, seconds);
        }
    }

    #endregion

    #region Sample Library Management

    /// <summary>
    /// Gets the sample library for a drum type.
    /// </summary>
    public DrumSampleLibrary GetSampleLibrary(DrumType drumType)
    {
        if (!_sampleLibraries.TryGetValue(drumType, out var library))
        {
            library = new DrumSampleLibrary { DrumType = drumType };
            _sampleLibraries[drumType] = library;
        }
        return library;
    }

    /// <summary>
    /// Loads a replacement sample from a file.
    /// </summary>
    public ReplacementSample? LoadSample(string filePath, DrumType drumType, int minVelocity = 0, int maxVelocity = 127)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            using var reader = new AudioFileReader(filePath);
            var sampleData = new List<float>();
            var buffer = new float[reader.WaveFormat.SampleRate * reader.WaveFormat.Channels];
            int samplesRead;

            while ((samplesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < samplesRead; i++)
                {
                    sampleData.Add(buffer[i]);
                }
            }

            // Convert to stereo if mono
            float[] audioData;
            int channels;

            if (reader.WaveFormat.Channels == 1)
            {
                audioData = new float[sampleData.Count * 2];
                for (int i = 0; i < sampleData.Count; i++)
                {
                    audioData[i * 2] = sampleData[i];
                    audioData[i * 2 + 1] = sampleData[i];
                }
                channels = 2;
            }
            else
            {
                audioData = sampleData.ToArray();
                channels = reader.WaveFormat.Channels;
            }

            var sample = new ReplacementSample
            {
                Name = Path.GetFileNameWithoutExtension(filePath),
                FilePath = filePath,
                AudioData = audioData,
                SampleRate = reader.WaveFormat.SampleRate,
                Channels = channels,
                MinVelocity = minVelocity,
                MaxVelocity = maxVelocity
            };

            var library = GetSampleLibrary(drumType);
            library.Samples.Add(sample);

            return sample;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Loads all samples from a directory for a drum type.
    /// </summary>
    public int LoadSamplesFromDirectory(string directory, DrumType drumType)
    {
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        int count = 0;
        var extensions = new[] { "*.wav", "*.mp3", "*.flac", "*.aiff", "*.ogg" };

        foreach (var ext in extensions)
        {
            foreach (var file in Directory.GetFiles(directory, ext))
            {
                if (LoadSample(file, drumType) != null)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>
    /// Clears all samples for a drum type.
    /// </summary>
    public void ClearSamples(DrumType drumType)
    {
        if (_sampleLibraries.TryGetValue(drumType, out var library))
        {
            library.Samples.Clear();
            library.RoundRobinIndex = 0;
        }
    }

    /// <summary>
    /// Clears all sample libraries.
    /// </summary>
    public void ClearAllSamples()
    {
        foreach (var library in _sampleLibraries.Values)
        {
            library.Samples.Clear();
            library.RoundRobinIndex = 0;
        }
    }

    #endregion

    #region Audio Processing

    /// <summary>
    /// Processes audio samples for trigger detection.
    /// </summary>
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
    /// Analyzes a complete audio buffer and returns all detected triggers.
    /// </summary>
    public List<DrumTrigger> AnalyzeBuffer(float[] samples, int sampleRate)
    {
        Reset();
        ProcessSamples(samples, 0, samples.Length, 1);

        lock (_lock)
        {
            return new List<DrumTrigger>(_triggers);
        }
    }

    /// <summary>
    /// Analyzes stereo audio buffer.
    /// </summary>
    public List<DrumTrigger> AnalyzeBuffer(float[] samples, int sampleRate, int channels)
    {
        Reset();
        ProcessSamples(samples, 0, samples.Length, channels);

        lock (_lock)
        {
            return new List<DrumTrigger>(_triggers);
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

        // Detect triggers for each zone
        foreach (var zone in _triggerZones.Values)
        {
            if (!zone.Enabled) continue;
            DetectTrigger(zone);
        }
    }

    private void DetectTrigger(TriggerZone zone)
    {
        var drumType = zone.DrumType;

        // Calculate band energy
        float bandEnergy = CalculateBandEnergy(zone.MinFrequency, zone.MaxFrequency);

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
        float threshold = zone.Threshold / Sensitivity;
        float adaptiveThreshold = avgEnergy + threshold * Math.Max(maxEnergy - avgEnergy, 0.001f);

        // Check for onset (energy spike above threshold)
        double timeSinceLastTrigger = _currentTime - _lastTriggerTime[drumType];
        double retriggerTime = Math.Max(zone.RetriggerTime, RetriggerTime);
        bool minTimePassed = timeSinceLastTrigger >= retriggerTime;

        if (bandEnergy > adaptiveThreshold && bandEnergy > 0.001f && minTimePassed)
        {
            // Calculate velocity using the configured curve
            int velocity = CalculateVelocity(bandEnergy, avgEnergy, maxEnergy);

            // Calculate confidence based on how much above threshold
            float confidence = Math.Min(1f, bandEnergy / (adaptiveThreshold * 2f));

            // Get replacement sample for round-robin index
            var library = _sampleLibraries[drumType];
            int sampleIndex = 0;
            if (library.Samples.Count > 0 && RoundRobinEnabled)
            {
                sampleIndex = (library.RoundRobinIndex + 1) % library.Samples.Count;
            }

            // Calculate humanization offset
            double humanizeOffset = 0;
            if (HumanizeAmount > 0)
            {
                humanizeOffset = (_random.NextDouble() * 2 - 1) * HumanizeMaxOffset * HumanizeAmount;
            }

            var trigger = new DrumTrigger
            {
                TimeSeconds = _currentTime,
                DrumType = drumType,
                Velocity = velocity,
                Amplitude = bandEnergy,
                Confidence = confidence,
                MidiNote = zone.MidiNote,
                SampleIndex = sampleIndex,
                SampleStartOffset = SampleStartOffset,
                HumanizeOffset = humanizeOffset,
                BandEnergies = new[] { bandEnergy }
            };

            lock (_lock)
            {
                _triggers.Add(trigger);
                _sidechainTriggers.Add(trigger);
                _lastTriggerTime[drumType] = _currentTime;

                if (_isLearning)
                {
                    _learnData.Add((drumType, bandEnergy));
                }
            }

            TriggerDetected?.Invoke(this, trigger);
            SidechainTriggered?.Invoke(this, trigger);
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
        if (energy <= 0) return MinVelocity;

        float normalizedEnergy = (energy - avgEnergy) / Math.Max(maxEnergy - avgEnergy, 0.001f);
        normalizedEnergy = Math.Clamp(normalizedEnergy, 0f, 1f);

        // Apply velocity curve
        float curved = VelocityCurve switch
        {
            VelocityCurveType.Linear => normalizedEnergy,
            VelocityCurveType.Logarithmic => MathF.Log10(1f + 9f * normalizedEnergy) / MathF.Log10(10f),
            VelocityCurveType.Exponential => normalizedEnergy * normalizedEnergy,
            VelocityCurveType.SCurve => 0.5f * (1f + MathF.Tanh(4f * (normalizedEnergy - 0.5f))),
            VelocityCurveType.Hard => MathF.Pow(normalizedEnergy, 0.5f),
            VelocityCurveType.Soft => MathF.Pow(normalizedEnergy, 1.5f),
            _ => normalizedEnergy
        };

        // Apply velocity scale
        curved *= VelocityScale;

        int velocity = (int)(curved * (MaxVelocity - MinVelocity)) + MinVelocity;
        return Math.Clamp(velocity, MinVelocity, MaxVelocity);
    }

    #endregion

    #region Replacement Processing

    /// <summary>
    /// Processes the original audio and applies drum replacement.
    /// Returns the processed audio buffer.
    /// </summary>
    public float[] ProcessReplacement(float[] originalSamples, int sampleRate, int channels = 2)
    {
        // First analyze to get triggers
        var triggers = AnalyzeBuffer(originalSamples, sampleRate, channels);

        // Create output buffer
        float[] output = new float[originalSamples.Length];

        // Handle blend mode for original audio
        switch (BlendMode)
        {
            case DrumBlendMode.Replace:
                // Don't copy original, just use replacement samples
                break;

            case DrumBlendMode.Layer:
                // Copy original audio
                Array.Copy(originalSamples, output, originalSamples.Length);
                break;

            case DrumBlendMode.Duck:
                // Copy original audio (will be ducked when samples play)
                Array.Copy(originalSamples, output, originalSamples.Length);
                break;
        }

        // Apply replacement samples for each trigger
        foreach (var trigger in triggers)
        {
            ApplyReplacementSample(output, trigger, sampleRate, channels);
            trigger.Processed = true;
        }

        return output;
    }

    private void ApplyReplacementSample(float[] output, DrumTrigger trigger, int sampleRate, int channels)
    {
        var library = _sampleLibraries[trigger.DrumType];
        var sample = library.GetSample(trigger.Velocity);

        if (sample?.AudioData == null) return;

        // Calculate start position in output
        double triggerTime = trigger.TimeSeconds + trigger.HumanizeOffset + trigger.SampleStartOffset;
        int startSample = (int)(triggerTime * sampleRate) * channels;

        if (startSample < 0) startSample = 0;
        if (startSample >= output.Length) return;

        // Calculate velocity-based gain
        float velocityGain = trigger.Velocity / 127f;

        // Resample if necessary
        double sampleRateRatio = (double)sample.SampleRate / sampleRate;

        int sampleChannels = sample.Channels;
        int outputChannels = channels;

        for (int i = 0; i < sample.AudioData.Length / sampleChannels; i++)
        {
            int outputIndex = startSample + (int)(i / sampleRateRatio) * outputChannels;

            if (outputIndex >= output.Length - outputChannels) break;

            int sourceIndex = i * sampleChannels;

            for (int ch = 0; ch < outputChannels; ch++)
            {
                int srcCh = ch % sampleChannels;
                float sampleValue = sample.AudioData[sourceIndex + srcCh];
                float gain = sample.Volume * velocityGain * BlendAmount;

                if (BlendMode == DrumBlendMode.Duck)
                {
                    // Duck original audio
                    output[outputIndex + ch] *= (1f - BlendAmount);
                }

                output[outputIndex + ch] += sampleValue * gain;
            }
        }
    }

    #endregion

    #region Learn Mode

    /// <summary>
    /// Starts learn mode to automatically detect thresholds.
    /// </summary>
    public void StartLearnMode()
    {
        lock (_lock)
        {
            _isLearning = true;
            _learnData.Clear();
        }
    }

    /// <summary>
    /// Stops learn mode and returns the learned thresholds.
    /// </summary>
    public LearnModeResult StopLearnMode()
    {
        var result = new LearnModeResult
        {
            DurationSeconds = _currentTime
        };

        lock (_lock)
        {
            _isLearning = false;

            if (_learnData.Count == 0)
            {
                result.Success = false;
                return result;
            }

            // Group by drum type and calculate statistics
            var grouped = _learnData.GroupBy(d => d.type);

            foreach (var group in grouped)
            {
                var amplitudes = group.Select(g => g.amplitude).ToList();

                if (amplitudes.Count == 0) continue;

                float avg = amplitudes.Average();
                float peak = amplitudes.Max();
                float min = amplitudes.Min();

                // Calculate threshold as a percentage between min and average
                float threshold = (avg - min) / Math.Max(peak - min, 0.001f) * 0.5f;
                threshold = Math.Clamp(threshold, 0.1f, 0.8f);

                result.Thresholds[group.Key] = threshold;
                result.AverageAmplitudes[group.Key] = avg;
                result.PeakAmplitudes[group.Key] = peak;
            }

            // Copy detected triggers
            result.DetectedHits.AddRange(_triggers);
            result.Success = result.Thresholds.Count > 0;
        }

        return result;
    }

    /// <summary>
    /// Applies learned thresholds to the trigger zones.
    /// </summary>
    public void ApplyLearnedThresholds(LearnModeResult result)
    {
        foreach (var kvp in result.Thresholds)
        {
            SetThreshold(kvp.Key, kvp.Value);
        }
    }

    #endregion

    #region Preview / Audition

    /// <summary>
    /// Previews a trigger by returning the replacement sample audio.
    /// </summary>
    public float[]? PreviewTrigger(DrumTrigger trigger, int sampleRate = 44100, int channels = 2)
    {
        var library = _sampleLibraries[trigger.DrumType];
        var sample = library.GetSample(trigger.Velocity);

        if (sample?.AudioData == null) return null;

        // Resample if necessary
        double ratio = (double)sample.SampleRate / sampleRate;
        int outputLength = (int)(sample.AudioData.Length / sample.Channels / ratio) * channels;

        float[] output = new float[outputLength];
        float velocityGain = trigger.Velocity / 127f;

        for (int i = 0; i < outputLength / channels; i++)
        {
            int sourceIndex = (int)(i * ratio) * sample.Channels;
            if (sourceIndex >= sample.AudioData.Length - sample.Channels) break;

            for (int ch = 0; ch < channels; ch++)
            {
                int srcCh = ch % sample.Channels;
                output[i * channels + ch] = sample.AudioData[sourceIndex + srcCh] * sample.Volume * velocityGain;
            }
        }

        return output;
    }

    /// <summary>
    /// Auditions a specific drum type with a given velocity.
    /// </summary>
    public float[]? AuditionDrum(DrumType drumType, int velocity = 100, int sampleRate = 44100, int channels = 2)
    {
        var trigger = new DrumTrigger
        {
            DrumType = drumType,
            Velocity = velocity
        };

        return PreviewTrigger(trigger, sampleRate, channels);
    }

    #endregion

    #region MIDI Export

    /// <summary>
    /// Converts detected triggers to NoteEvent objects.
    /// </summary>
    public List<NoteEvent> ToNoteEvents(double bpm)
    {
        var events = new List<NoteEvent>();
        double beatsPerSecond = bpm / 60.0;

        lock (_lock)
        {
            foreach (var trigger in _triggers)
            {
                double beat = trigger.TimeSeconds * beatsPerSecond;
                events.Add(new NoteEvent
                {
                    Note = trigger.MidiNote,
                    Velocity = trigger.Velocity,
                    Beat = beat,
                    Duration = 0.25 // Quarter note default duration for drums
                });
            }
        }

        return events.OrderBy(e => e.Beat).ToList();
    }

    /// <summary>
    /// Converts detected triggers to a Pattern.
    /// </summary>
    public Pattern ToPattern(double bpm, ISynth? synth = null)
    {
        var events = ToNoteEvents(bpm);

        var pattern = new Pattern(synth ?? new DummySynthInternal())
        {
            Name = "Drum Replacement",
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
    /// Exports detected triggers to a MIDI file.
    /// </summary>
    public void ExportMidi(string filePath, double bpm = 120, int channel = 9)
    {
        var midiData = new MidiFileData
        {
            Format = MidiFileFormat.SingleTrack,
            TicksPerQuarterNote = 480,
            InitialTempo = bpm
        };

        var track = new MidiTrack { Name = "Drum Replacement" };

        lock (_lock)
        {
            foreach (var trigger in _triggers.OrderBy(t => t.TimeSeconds))
            {
                double beat = trigger.TimeSeconds * bpm / 60.0;
                long ticks = midiData.BeatsToTicks(beat);

                // Note On
                track.Events.Add(new MidiFileEvent
                {
                    AbsoluteTime = ticks,
                    EventType = MidiEventType.NoteOn,
                    Channel = channel,
                    Data1 = trigger.MidiNote,
                    Data2 = trigger.Velocity
                });

                // Note Off (short duration for drums)
                long noteOffTicks = ticks + 120; // 1/4 beat
                track.Events.Add(new MidiFileEvent
                {
                    AbsoluteTime = noteOffTicks,
                    EventType = MidiEventType.NoteOff,
                    Channel = channel,
                    Data1 = trigger.MidiNote,
                    Data2 = 0
                });
            }
        }

        // Sort events and calculate delta times
        var sortedEvents = track.Events.OrderBy(e => e.AbsoluteTime).ThenByDescending(e => e.EventType).ToList();
        long lastTime = 0;
        foreach (var evt in sortedEvents)
        {
            evt.DeltaTime = evt.AbsoluteTime - lastTime;
            lastTime = evt.AbsoluteTime;
        }

        track.Events.Clear();
        track.Events.AddRange(sortedEvents);

        midiData.Tracks.Add(track);
        MidiFile.Save(midiData, filePath);
    }

    /// <summary>
    /// Exports the replaced audio to a WAV file.
    /// </summary>
    public void ExportReplacedAudio(float[] originalSamples, string filePath, int sampleRate = 44100, int channels = 2)
    {
        var processed = ProcessReplacement(originalSamples, sampleRate, channels);

        var format = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        using var writer = new WaveFileWriter(filePath, format);
        writer.WriteSamples(processed, 0, processed.Length);
    }

    #endregion

    #region State Management

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
            _triggers.Clear();
            _sidechainTriggers.Clear();

            foreach (var drumType in _triggerZones.Keys)
            {
                Array.Clear(_energyHistory[drumType], 0, EnergyHistorySize);
                _energyHistoryPosition[drumType] = 0;
                _lastTriggerTime[drumType] = double.NegativeInfinity;
            }

            foreach (var library in _sampleLibraries.Values)
            {
                library.RoundRobinIndex = 0;
            }
        }
    }

    /// <summary>
    /// Clears detected triggers but keeps detector state.
    /// </summary>
    public void ClearTriggers()
    {
        lock (_lock)
        {
            _triggers.Clear();
            _sidechainTriggers.Clear();
        }
    }

    /// <summary>
    /// Clears the sidechain trigger buffer.
    /// </summary>
    public void ClearSidechainTriggers()
    {
        lock (_lock)
        {
            _sidechainTriggers.Clear();
        }
    }

    #endregion

    #region FFT Implementation

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

    #endregion

    /// <summary>
    /// Internal dummy synth for pattern creation.
    /// </summary>
    private class DummySynthInternal : ISynth
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
}
