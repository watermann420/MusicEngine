// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Transient/onset detection.

using System;
using System.Collections.Generic;

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Transient/beat detector using energy-based onset detection.
/// Detects sudden increases in audio energy that typically correspond to drum hits,
/// note attacks, and other transient events.
/// </summary>
public class TransientDetector : IAnalyzer
{
    private readonly int _sampleRate;
    private readonly int _hopSize;
    private readonly int _frameSize;
    private readonly float[] _frameBuffer;
    private int _frameBufferPosition;
    private readonly float[] _energyHistory;
    private int _energyHistoryPosition;
    private float _previousEnergy;
    private double _currentTime;
    private readonly object _lock = new();

    // Detection parameters
    private float _threshold = 0.5f;
    private float _sensitivity = 1.0f;
    private double _minimumIntervalMs = 50; // Minimum time between detected transients

    // Detection results
    private readonly List<TransientEvent> _detectedTransients = new();
    private double _lastTransientTime = double.NegativeInfinity;

    /// <summary>
    /// Energy history size for adaptive threshold (in frames, ~1 second).
    /// </summary>
    private const int EnergyHistorySize = 43;

    /// <summary>
    /// Gets the list of detected transient events.
    /// </summary>
    public IReadOnlyList<TransientEvent> DetectedTransients
    {
        get
        {
            lock (_lock)
            {
                return new List<TransientEvent>(_detectedTransients);
            }
        }
    }

    /// <summary>
    /// Gets or sets the detection threshold (0.0 to 1.0).
    /// Higher values require stronger transients for detection.
    /// </summary>
    public float Threshold
    {
        get => _threshold;
        set => _threshold = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Gets or sets the detection sensitivity multiplier (0.1 to 10.0).
    /// Higher values detect weaker transients.
    /// </summary>
    public float Sensitivity
    {
        get => _sensitivity;
        set => _sensitivity = Math.Clamp(value, 0.1f, 10f);
    }

    /// <summary>
    /// Gets or sets the minimum interval between detected transients in milliseconds.
    /// </summary>
    public double MinimumIntervalMs
    {
        get => _minimumIntervalMs;
        set => _minimumIntervalMs = Math.Max(0, value);
    }

    /// <summary>
    /// Event raised when a transient is detected.
    /// </summary>
    public event EventHandler<TransientEventArgs>? TransientDetected;

    /// <summary>
    /// Creates a new transient detector with the specified configuration.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate in Hz (default: 44100).</param>
    /// <param name="frameSize">Analysis frame size in samples (default: 1024).</param>
    /// <param name="hopSize">Hop size in samples (default: 512, 50% overlap).</param>
    public TransientDetector(int sampleRate = 44100, int frameSize = 1024, int hopSize = 512)
    {
        _sampleRate = sampleRate;
        _frameSize = frameSize;
        _hopSize = hopSize;
        _frameBuffer = new float[frameSize];
        _energyHistory = new float[EnergyHistorySize];
    }

    /// <summary>
    /// Processes audio samples for transient detection.
    /// </summary>
    /// <param name="samples">Audio samples.</param>
    /// <param name="offset">Offset into the buffer.</param>
    /// <param name="count">Number of samples to process.</param>
    /// <param name="channels">Number of audio channels.</param>
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
    /// Analyzes a complete audio buffer and returns all detected transients.
    /// </summary>
    /// <param name="samples">Complete audio buffer (mono).</param>
    /// <param name="sampleRate">Sample rate of the audio.</param>
    /// <returns>List of detected transient events.</returns>
    public List<TransientEvent> AnalyzeBuffer(float[] samples, int sampleRate)
    {
        // Reset state
        Reset();

        // Process all samples
        ProcessSamples(samples, 0, samples.Length, 1);

        lock (_lock)
        {
            return new List<TransientEvent>(_detectedTransients);
        }
    }

    /// <summary>
    /// Resets the detector state.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            Array.Clear(_frameBuffer, 0, _frameBuffer.Length);
            Array.Clear(_energyHistory, 0, _energyHistory.Length);
            _frameBufferPosition = 0;
            _energyHistoryPosition = 0;
            _previousEnergy = 0;
            _currentTime = 0;
            _lastTransientTime = double.NegativeInfinity;
            _detectedTransients.Clear();
        }
    }

    /// <summary>
    /// Clears detected transients but keeps detector state.
    /// </summary>
    public void ClearTransients()
    {
        lock (_lock)
        {
            _detectedTransients.Clear();
        }
    }

    private void ProcessFrame()
    {
        // Calculate frame energy (RMS)
        float energy = 0;
        for (int i = 0; i < _frameSize; i++)
        {
            energy += _frameBuffer[i] * _frameBuffer[i];
        }
        energy = (float)Math.Sqrt(energy / _frameSize);

        // Calculate spectral flux (simplified using energy difference)
        float flux = Math.Max(0, energy - _previousEnergy);

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

        // Adaptive threshold based on local statistics
        float adaptiveThreshold = avgEnergy + (_threshold * 2 + 0.5f) * Math.Max(varEnergy, 0.001f);
        adaptiveThreshold /= _sensitivity;

        // Detect transient
        if (flux > adaptiveThreshold && energy > 0.001f)
        {
            double timeSinceLastTransient = (_currentTime - _lastTransientTime) * 1000; // in ms

            if (timeSinceLastTransient >= _minimumIntervalMs)
            {
                // Calculate transient strength (normalized)
                float strength = Math.Min(1.0f, flux / (adaptiveThreshold * 2));

                // Determine if this might be a downbeat (stronger transient)
                bool isStrong = strength > 0.7f;

                var transient = new TransientEvent
                {
                    TimeSeconds = _currentTime,
                    Strength = strength,
                    IsStrong = isStrong,
                    Energy = energy
                };

                lock (_lock)
                {
                    _detectedTransients.Add(transient);
                    _lastTransientTime = _currentTime;
                }

                TransientDetected?.Invoke(this, new TransientEventArgs(transient));
            }
        }

        _previousEnergy = energy;
    }
}

/// <summary>
/// Represents a detected transient event.
/// </summary>
public class TransientEvent
{
    /// <summary>
    /// Time position of the transient in seconds from the start.
    /// </summary>
    public double TimeSeconds { get; set; }

    /// <summary>
    /// Transient strength (0.0 to 1.0).
    /// </summary>
    public float Strength { get; set; }

    /// <summary>
    /// Whether this is a strong transient (potential downbeat).
    /// </summary>
    public bool IsStrong { get; set; }

    /// <summary>
    /// RMS energy at the transient position.
    /// </summary>
    public float Energy { get; set; }

    /// <summary>
    /// Time position in milliseconds.
    /// </summary>
    public double TimeMs => TimeSeconds * 1000.0;
}

/// <summary>
/// Event arguments for transient detection.
/// </summary>
public class TransientEventArgs : EventArgs
{
    /// <summary>
    /// The detected transient event.
    /// </summary>
    public TransientEvent Transient { get; }

    public TransientEventArgs(TransientEvent transient)
    {
        Transient = transient;
    }
}
