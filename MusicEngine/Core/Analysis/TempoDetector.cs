// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: BPM/tempo detection.

using System;
using System.Collections.Generic;

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Tempo/BPM detector using autocorrelation analysis.
/// Analyzes audio data to estimate the beats per minute within a configurable range.
/// </summary>
public class TempoDetector
{
    private readonly int _sampleRate;
    private readonly int _minBpm;
    private readonly int _maxBpm;
    private readonly int _analysisWindowSamples;
    private readonly float[] _onsetBuffer;
    private int _onsetBufferPosition;
    private readonly object _lock = new();

    // Onset detection parameters
    private float _previousEnergy;
    private readonly float[] _energyHistory;
    private int _energyHistoryPosition;
    private const int EnergyHistorySize = 43; // ~1 second at typical hop size

    // Detection results
    private double _detectedBpm;
    private double _confidence;
    private readonly List<double> _bpmHistory = new();
    private const int BpmHistorySize = 10;

    /// <summary>
    /// Gets the detected BPM (beats per minute).
    /// </summary>
    public double DetectedBpm
    {
        get
        {
            lock (_lock)
            {
                return _detectedBpm;
            }
        }
    }

    /// <summary>
    /// Gets the confidence level of the detection (0.0 to 1.0).
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
    /// Gets the minimum BPM in the detection range.
    /// </summary>
    public int MinBpm => _minBpm;

    /// <summary>
    /// Gets the maximum BPM in the detection range.
    /// </summary>
    public int MaxBpm => _maxBpm;

    /// <summary>
    /// Event raised when BPM is detected or updated.
    /// </summary>
    public event EventHandler<TempoEventArgs>? TempoDetected;

    /// <summary>
    /// Creates a new tempo detector with the specified configuration.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate in Hz (default: 44100).</param>
    /// <param name="minBpm">Minimum BPM to detect (default: 60).</param>
    /// <param name="maxBpm">Maximum BPM to detect (default: 200).</param>
    /// <param name="analysisWindowSeconds">Analysis window duration in seconds (default: 5).</param>
    public TempoDetector(
        int sampleRate = 44100,
        int minBpm = 60,
        int maxBpm = 200,
        double analysisWindowSeconds = 5.0)
    {
        if (minBpm >= maxBpm)
            throw new ArgumentException("Minimum BPM must be less than maximum BPM.");
        if (analysisWindowSeconds < 1.0)
            throw new ArgumentOutOfRangeException(nameof(analysisWindowSeconds), "Analysis window must be at least 1 second.");

        _sampleRate = sampleRate;
        _minBpm = minBpm;
        _maxBpm = maxBpm;

        // Calculate buffer size based on analysis window
        // Using a hop size of ~23ms (1024 samples at 44100Hz)
        int hopSize = 1024;
        int numHops = (int)(analysisWindowSeconds * sampleRate / hopSize);
        _analysisWindowSamples = numHops;
        _onsetBuffer = new float[_analysisWindowSamples];
        _energyHistory = new float[EnergyHistorySize];
    }

    /// <summary>
    /// Processes audio samples for tempo detection.
    /// Samples should be mono or will be mixed to mono internally.
    /// </summary>
    /// <param name="samples">Audio samples.</param>
    /// <param name="count">Number of samples.</param>
    /// <param name="channels">Number of audio channels.</param>
    public void ProcessSamples(float[] samples, int count, int channels = 1)
    {
        // Process in frames (hop size of 1024)
        const int frameSize = 1024;
        int frames = count / (frameSize * channels);

        for (int frame = 0; frame < frames; frame++)
        {
            // Calculate frame energy
            float energy = 0;
            int frameStart = frame * frameSize * channels;

            for (int i = 0; i < frameSize; i++)
            {
                float sample = 0;
                for (int ch = 0; ch < channels; ch++)
                {
                    int idx = frameStart + i * channels + ch;
                    if (idx < count)
                    {
                        sample += samples[idx];
                    }
                }
                sample /= channels; // Mix to mono
                energy += sample * sample;
            }

            energy = (float)Math.Sqrt(energy / frameSize);

            // Onset detection using spectral flux approximation
            float onset = CalculateOnset(energy);

            // Store onset value
            _onsetBuffer[_onsetBufferPosition] = onset;
            _onsetBufferPosition = (_onsetBufferPosition + 1) % _analysisWindowSamples;

            // Perform autocorrelation periodically
            if (_onsetBufferPosition == 0)
            {
                PerformAutocorrelation();
            }
        }
    }

    /// <summary>
    /// Analyzes a complete audio buffer and returns the detected tempo.
    /// </summary>
    /// <param name="samples">Complete audio buffer (mono).</param>
    /// <param name="sampleRate">Sample rate of the audio.</param>
    /// <returns>BeatAnalysisResult containing detected BPM and confidence.</returns>
    public BeatAnalysisResult AnalyzeBuffer(float[] samples, int sampleRate)
    {
        // Reset state
        Reset();

        // Process all samples
        ProcessSamples(samples, samples.Length, 1);

        // Force final analysis
        PerformAutocorrelation();

        return new BeatAnalysisResult
        {
            DetectedBpm = _detectedBpm,
            Confidence = _confidence,
            Beats = new List<double>() // No beat positions for buffer analysis
        };
    }

    /// <summary>
    /// Resets the detector state.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            Array.Clear(_onsetBuffer, 0, _onsetBuffer.Length);
            Array.Clear(_energyHistory, 0, _energyHistory.Length);
            _onsetBufferPosition = 0;
            _energyHistoryPosition = 0;
            _previousEnergy = 0;
            _detectedBpm = 0;
            _confidence = 0;
            _bpmHistory.Clear();
        }
    }

    private float CalculateOnset(float energy)
    {
        // Calculate local average energy
        float avgEnergy = 0;
        for (int i = 0; i < EnergyHistorySize; i++)
        {
            avgEnergy += _energyHistory[i];
        }
        avgEnergy /= EnergyHistorySize;

        // Store current energy in history
        _energyHistory[_energyHistoryPosition] = energy;
        _energyHistoryPosition = (_energyHistoryPosition + 1) % EnergyHistorySize;

        // Calculate onset strength (half-wave rectified difference)
        float onset = Math.Max(0, energy - _previousEnergy);

        // Apply adaptive threshold
        float threshold = avgEnergy * 1.5f;
        if (onset < threshold)
        {
            onset = 0;
        }

        _previousEnergy = energy;
        return onset;
    }

    private void PerformAutocorrelation()
    {
        // Calculate autocorrelation for different lag values corresponding to BPM range
        double hopDuration = 1024.0 / _sampleRate; // Duration of one hop in seconds
        int minLag = (int)(60.0 / (_maxBpm * hopDuration)); // Lag for max BPM
        int maxLag = (int)(60.0 / (_minBpm * hopDuration)); // Lag for min BPM

        minLag = Math.Max(1, minLag);
        maxLag = Math.Min(_analysisWindowSamples - 1, maxLag);

        double maxCorrelation = 0;
        int bestLag = minLag;

        // Calculate mean and variance of onset buffer
        double mean = 0;
        for (int i = 0; i < _analysisWindowSamples; i++)
        {
            mean += _onsetBuffer[i];
        }
        mean /= _analysisWindowSamples;

        double variance = 0;
        for (int i = 0; i < _analysisWindowSamples; i++)
        {
            double diff = _onsetBuffer[i] - mean;
            variance += diff * diff;
        }
        variance /= _analysisWindowSamples;

        if (variance < 1e-10)
        {
            // No significant signal
            return;
        }

        // Perform autocorrelation
        for (int lag = minLag; lag <= maxLag; lag++)
        {
            double correlation = 0;
            int count = _analysisWindowSamples - lag;

            for (int i = 0; i < count; i++)
            {
                int idx1 = (_onsetBufferPosition + i) % _analysisWindowSamples;
                int idx2 = (_onsetBufferPosition + i + lag) % _analysisWindowSamples;
                correlation += (_onsetBuffer[idx1] - mean) * (_onsetBuffer[idx2] - mean);
            }

            correlation /= count * variance;

            if (correlation > maxCorrelation)
            {
                maxCorrelation = correlation;
                bestLag = lag;
            }
        }

        // Convert lag to BPM
        double bpm = 60.0 / (bestLag * hopDuration);

        // Ensure BPM is within range (handle octave errors)
        while (bpm < _minBpm && bpm > 0)
        {
            bpm *= 2;
        }
        while (bpm > _maxBpm)
        {
            bpm /= 2;
        }

        // Calculate confidence based on correlation strength
        double newConfidence = Math.Clamp(maxCorrelation, 0, 1);

        // Update BPM history for smoothing
        lock (_lock)
        {
            if (_bpmHistory.Count >= BpmHistorySize)
            {
                _bpmHistory.RemoveAt(0);
            }
            _bpmHistory.Add(bpm);

            // Calculate smoothed BPM (median filter)
            if (_bpmHistory.Count >= 3)
            {
                List<double> sorted = new(_bpmHistory);
                sorted.Sort();
                _detectedBpm = sorted[sorted.Count / 2];
            }
            else
            {
                _detectedBpm = bpm;
            }

            _confidence = newConfidence;
        }

        // Raise event
        TempoDetected?.Invoke(this, new TempoEventArgs(_detectedBpm, _confidence));
    }
}

/// <summary>
/// Event arguments for tempo detection updates.
/// </summary>
public class TempoEventArgs : EventArgs
{
    /// <summary>
    /// Detected BPM (beats per minute).
    /// </summary>
    public double Bpm { get; }

    /// <summary>
    /// Confidence level of the detection (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; }

    public TempoEventArgs(double bpm, double confidence)
    {
        Bpm = bpm;
        Confidence = confidence;
    }
}
