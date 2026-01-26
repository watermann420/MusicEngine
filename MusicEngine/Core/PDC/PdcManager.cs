// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace MusicEngine.Core.PDC;

/// <summary>
/// Manages Plugin Delay Compensation (PDC) across all audio tracks.
/// Calculates the maximum latency and applies appropriate compensation
/// to tracks with lower latency to ensure sample-accurate alignment.
/// </summary>
public class PdcManager : IDisposable
{
    private readonly Dictionary<string, TrackLatencyInfo> _tracks = new();
    private readonly Dictionary<string, DelayCompensationBuffer> _compensationBuffers = new();
    private readonly object _lock = new();
    private readonly ILogger? _logger;
    private int _maxLatencySamples;
    private int _sampleRate;
    private int _channels;
    private bool _enabled = true;
    private bool _disposed;

    /// <summary>
    /// Event raised when the overall PDC latency changes.
    /// </summary>
    public event EventHandler<LatencyChangedEventArgs>? TotalLatencyChanged;

    /// <summary>
    /// Event raised when compensation values are recalculated.
    /// </summary>
    public event EventHandler? CompensationRecalculated;

    /// <summary>
    /// Gets the maximum latency across all registered tracks in samples.
    /// </summary>
    public int MaxLatencySamples
    {
        get
        {
            lock (_lock)
            {
                return _maxLatencySamples;
            }
        }
    }

    /// <summary>
    /// Gets the maximum latency across all registered tracks in milliseconds.
    /// </summary>
    public double MaxLatencyMs
    {
        get
        {
            lock (_lock)
            {
                return _sampleRate > 0 ? (_maxLatencySamples * 1000.0) / _sampleRate : 0;
            }
        }
    }

    /// <summary>
    /// Gets or sets whether PDC is enabled.
    /// When disabled, no delay compensation is applied.
    /// </summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            lock (_lock)
            {
                if (_enabled != value)
                {
                    _enabled = value;
                    if (!_enabled)
                    {
                        // Clear all compensation buffers when disabled
                        foreach (var buffer in _compensationBuffers.Values)
                        {
                            buffer.SetDelay(0);
                            buffer.Clear();
                        }
                    }
                    else
                    {
                        // Recalculate when re-enabled
                        RecalculateCompensationInternal();
                    }
                }
            }
        }
    }

    /// <summary>
    /// Gets the number of registered tracks.
    /// </summary>
    public int TrackCount
    {
        get
        {
            lock (_lock)
            {
                return _tracks.Count;
            }
        }
    }

    /// <summary>
    /// Creates a new PDC Manager.
    /// </summary>
    /// <param name="sampleRate">Sample rate for latency calculations.</param>
    /// <param name="channels">Number of audio channels (default: 2 for stereo).</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public PdcManager(int sampleRate, int channels = 2, ILogger? logger = null)
    {
        _sampleRate = sampleRate > 0 ? sampleRate : Settings.SampleRate;
        _channels = channels > 0 ? channels : 2;
        _logger = logger;
    }

    /// <summary>
    /// Registers a track with its latency reporters.
    /// </summary>
    /// <param name="trackId">Unique identifier for the track.</param>
    /// <param name="latencyReporters">Collection of latency reporters (effects, plugins, etc.).</param>
    /// <param name="maxCompensationSamples">Maximum compensation delay capacity (default: 1 second worth of samples).</param>
    public void RegisterTrack(string trackId, IEnumerable<ILatencyReporter> latencyReporters, int maxCompensationSamples = 0)
    {
        if (string.IsNullOrWhiteSpace(trackId))
            throw new ArgumentNullException(nameof(trackId));

        lock (_lock)
        {
            if (_disposed)
                return;

            // Default max compensation to 1 second
            if (maxCompensationSamples <= 0)
                maxCompensationSamples = _sampleRate;

            // Create track info
            var trackInfo = new TrackLatencyInfo(trackId);

            // Subscribe to latency changes from all reporters
            var reporters = latencyReporters?.ToList() ?? new List<ILatencyReporter>();
            foreach (var reporter in reporters)
            {
                trackInfo.AddReporter(reporter);
                reporter.LatencyChanged += OnReporterLatencyChanged;
            }

            // Calculate initial total latency for this track
            trackInfo.RecalculateTotalLatency();

            // Store track info
            _tracks[trackId] = trackInfo;

            // Create compensation buffer for this track
            _compensationBuffers[trackId] = new DelayCompensationBuffer(maxCompensationSamples, _channels);

            _logger?.LogDebug("Registered track '{TrackId}' with {ReporterCount} latency reporters, total latency: {Latency} samples",
                trackId, reporters.Count, trackInfo.TotalLatencySamples);

            // Recalculate compensation for all tracks
            RecalculateCompensationInternal();
        }
    }

    /// <summary>
    /// Registers a track with a single latency reporter.
    /// </summary>
    /// <param name="trackId">Unique identifier for the track.</param>
    /// <param name="latencyReporter">The latency reporter for this track.</param>
    /// <param name="maxCompensationSamples">Maximum compensation delay capacity.</param>
    public void RegisterTrack(string trackId, ILatencyReporter latencyReporter, int maxCompensationSamples = 0)
    {
        RegisterTrack(trackId, new[] { latencyReporter }, maxCompensationSamples);
    }

    /// <summary>
    /// Registers a track with no initial latency reporters.
    /// Reporters can be added later using AddLatencyReporter.
    /// </summary>
    /// <param name="trackId">Unique identifier for the track.</param>
    /// <param name="maxCompensationSamples">Maximum compensation delay capacity.</param>
    public void RegisterTrack(string trackId, int maxCompensationSamples = 0)
    {
        RegisterTrack(trackId, Enumerable.Empty<ILatencyReporter>(), maxCompensationSamples);
    }

    /// <summary>
    /// Adds a latency reporter to an existing track.
    /// </summary>
    /// <param name="trackId">The track identifier.</param>
    /// <param name="reporter">The latency reporter to add.</param>
    public void AddLatencyReporter(string trackId, ILatencyReporter reporter)
    {
        if (string.IsNullOrWhiteSpace(trackId) || reporter == null)
            return;

        lock (_lock)
        {
            if (_disposed || !_tracks.TryGetValue(trackId, out var trackInfo))
                return;

            trackInfo.AddReporter(reporter);
            reporter.LatencyChanged += OnReporterLatencyChanged;
            trackInfo.RecalculateTotalLatency();

            _logger?.LogDebug("Added latency reporter to track '{TrackId}', new total latency: {Latency} samples",
                trackId, trackInfo.TotalLatencySamples);

            RecalculateCompensationInternal();
        }
    }

    /// <summary>
    /// Removes a latency reporter from a track.
    /// </summary>
    /// <param name="trackId">The track identifier.</param>
    /// <param name="reporter">The latency reporter to remove.</param>
    public void RemoveLatencyReporter(string trackId, ILatencyReporter reporter)
    {
        if (string.IsNullOrWhiteSpace(trackId) || reporter == null)
            return;

        lock (_lock)
        {
            if (_disposed || !_tracks.TryGetValue(trackId, out var trackInfo))
                return;

            reporter.LatencyChanged -= OnReporterLatencyChanged;
            trackInfo.RemoveReporter(reporter);
            trackInfo.RecalculateTotalLatency();

            _logger?.LogDebug("Removed latency reporter from track '{TrackId}', new total latency: {Latency} samples",
                trackId, trackInfo.TotalLatencySamples);

            RecalculateCompensationInternal();
        }
    }

    /// <summary>
    /// Unregisters a track from PDC management.
    /// </summary>
    /// <param name="trackId">The track identifier.</param>
    public void UnregisterTrack(string trackId)
    {
        if (string.IsNullOrWhiteSpace(trackId))
            return;

        lock (_lock)
        {
            if (_disposed)
                return;

            if (_tracks.TryGetValue(trackId, out var trackInfo))
            {
                // Unsubscribe from all reporters
                foreach (var reporter in trackInfo.Reporters)
                {
                    reporter.LatencyChanged -= OnReporterLatencyChanged;
                }

                _tracks.Remove(trackId);

                _logger?.LogDebug("Unregistered track '{TrackId}' from PDC", trackId);
            }

            if (_compensationBuffers.TryGetValue(trackId, out var buffer))
            {
                buffer.Dispose();
                _compensationBuffers.Remove(trackId);
            }

            // Recalculate compensation
            RecalculateCompensationInternal();
        }
    }

    /// <summary>
    /// Gets the compensation delay buffer for a track.
    /// </summary>
    /// <param name="trackId">The track identifier.</param>
    /// <returns>The delay compensation buffer, or null if track not found.</returns>
    public DelayCompensationBuffer? GetCompensationBuffer(string trackId)
    {
        lock (_lock)
        {
            return _compensationBuffers.TryGetValue(trackId, out var buffer) ? buffer : null;
        }
    }

    /// <summary>
    /// Gets the compensation delay in samples for a specific track.
    /// </summary>
    /// <param name="trackId">The track identifier.</param>
    /// <returns>The compensation delay in samples, or 0 if track not found.</returns>
    public int GetTrackCompensation(string trackId)
    {
        lock (_lock)
        {
            if (_compensationBuffers.TryGetValue(trackId, out var buffer))
            {
                return buffer.DelaySamples;
            }
            return 0;
        }
    }

    /// <summary>
    /// Gets the total latency for a specific track (before compensation).
    /// </summary>
    /// <param name="trackId">The track identifier.</param>
    /// <returns>The total latency in samples, or 0 if track not found.</returns>
    public int GetTrackLatency(string trackId)
    {
        lock (_lock)
        {
            if (_tracks.TryGetValue(trackId, out var trackInfo))
            {
                return trackInfo.TotalLatencySamples;
            }
            return 0;
        }
    }

    /// <summary>
    /// Processes audio through the compensation buffer for a track.
    /// </summary>
    /// <param name="trackId">The track identifier.</param>
    /// <param name="input">Input audio samples.</param>
    /// <param name="output">Output buffer for compensated audio.</param>
    /// <param name="offset">Offset into the output buffer.</param>
    /// <param name="count">Number of samples to process.</param>
    /// <returns>Number of samples processed.</returns>
    public int ProcessCompensation(string trackId, float[] input, float[] output, int offset, int count)
    {
        if (!_enabled)
        {
            Array.Copy(input, 0, output, offset, count);
            return count;
        }

        lock (_lock)
        {
            if (_disposed || !_compensationBuffers.TryGetValue(trackId, out var buffer))
            {
                Array.Copy(input, 0, output, offset, count);
                return count;
            }

            return buffer.Process(input, output, offset, count);
        }
    }

    /// <summary>
    /// Manually triggers recalculation of compensation values.
    /// </summary>
    /// <remarks>
    /// This is automatically called when track latencies change.
    /// Manual calls may be needed when tracks are added/removed in bulk.
    /// </remarks>
    public void RecalculateCompensation()
    {
        lock (_lock)
        {
            if (!_disposed)
            {
                RecalculateCompensationInternal();
            }
        }
    }

    /// <summary>
    /// Internal method to recalculate compensation (called within lock).
    /// </summary>
    private void RecalculateCompensationInternal()
    {
        if (_tracks.Count == 0)
        {
            int oldMax = _maxLatencySamples;
            _maxLatencySamples = 0;
            if (oldMax != 0)
            {
                TotalLatencyChanged?.Invoke(this, new LatencyChangedEventArgs(oldMax, 0));
            }
            return;
        }

        // Find maximum latency across all tracks
        int newMaxLatency = _tracks.Values.Max(t => t.TotalLatencySamples);
        int oldMaxLatency = _maxLatencySamples;

        if (newMaxLatency != oldMaxLatency)
        {
            _maxLatencySamples = newMaxLatency;

            _logger?.LogInformation("PDC max latency changed from {OldLatency} to {NewLatency} samples ({LatencyMs:F2} ms)",
                oldMaxLatency, newMaxLatency, MaxLatencyMs);

            TotalLatencyChanged?.Invoke(this, new LatencyChangedEventArgs(oldMaxLatency, newMaxLatency));
        }

        // Calculate and apply compensation for each track
        foreach (var kvp in _tracks)
        {
            string trackId = kvp.Key;
            int trackLatency = kvp.Value.TotalLatencySamples;
            int compensation = _enabled ? _maxLatencySamples - trackLatency : 0;

            if (_compensationBuffers.TryGetValue(trackId, out var buffer))
            {
                int oldCompensation = buffer.DelaySamples;
                if (oldCompensation != compensation)
                {
                    buffer.SetDelay(compensation);

                    _logger?.LogDebug("Track '{TrackId}' compensation: {OldComp} -> {NewComp} samples (track latency: {TrackLatency})",
                        trackId, oldCompensation, compensation, trackLatency);
                }
            }
        }

        CompensationRecalculated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Handles latency change events from registered reporters.
    /// </summary>
    private void OnReporterLatencyChanged(object? sender, LatencyChangedEventArgs e)
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            // Find which track this reporter belongs to and recalculate
            foreach (var trackInfo in _tracks.Values)
            {
                if (sender is ILatencyReporter reporter && trackInfo.Reporters.Contains(reporter))
                {
                    trackInfo.RecalculateTotalLatency();

                    _logger?.LogDebug("Track '{TrackId}' reporter latency changed: {Old} -> {New}, new total: {Total}",
                        trackInfo.TrackId, e.OldLatency, e.NewLatency, trackInfo.TotalLatencySamples);

                    break;
                }
            }

            RecalculateCompensationInternal();
        }
    }

    /// <summary>
    /// Gets a summary of all track latencies and compensations.
    /// </summary>
    /// <returns>Dictionary mapping track IDs to their latency info.</returns>
    public Dictionary<string, (int Latency, int Compensation)> GetLatencySummary()
    {
        lock (_lock)
        {
            var summary = new Dictionary<string, (int, int)>();

            foreach (var kvp in _tracks)
            {
                string trackId = kvp.Key;
                int latency = kvp.Value.TotalLatencySamples;
                int compensation = _compensationBuffers.TryGetValue(trackId, out var buffer) ? buffer.DelaySamples : 0;
                summary[trackId] = (latency, compensation);
            }

            return summary;
        }
    }

    /// <summary>
    /// Updates the sample rate used for latency calculations.
    /// </summary>
    /// <param name="sampleRate">New sample rate.</param>
    public void SetSampleRate(int sampleRate)
    {
        lock (_lock)
        {
            _sampleRate = sampleRate > 0 ? sampleRate : Settings.SampleRate;
        }
    }

    /// <summary>
    /// Disposes all resources and clears all tracks.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_lock)
        {
            _disposed = true;

            // Unsubscribe from all reporters
            foreach (var trackInfo in _tracks.Values)
            {
                foreach (var reporter in trackInfo.Reporters)
                {
                    reporter.LatencyChanged -= OnReporterLatencyChanged;
                }
            }

            // Dispose all compensation buffers
            foreach (var buffer in _compensationBuffers.Values)
            {
                buffer.Dispose();
            }

            _tracks.Clear();
            _compensationBuffers.Clear();
        }

        GC.SuppressFinalize(this);
    }

    ~PdcManager()
    {
        Dispose();
    }

    /// <summary>
    /// Internal class to track latency information for a single track.
    /// </summary>
    private class TrackLatencyInfo
    {
        private readonly List<ILatencyReporter> _reporters = new();

        public string TrackId { get; }
        public int TotalLatencySamples { get; private set; }
        public IReadOnlyList<ILatencyReporter> Reporters => _reporters;

        public TrackLatencyInfo(string trackId)
        {
            TrackId = trackId;
        }

        public void AddReporter(ILatencyReporter reporter)
        {
            if (!_reporters.Contains(reporter))
            {
                _reporters.Add(reporter);
            }
        }

        public void RemoveReporter(ILatencyReporter reporter)
        {
            _reporters.Remove(reporter);
        }

        public void RecalculateTotalLatency()
        {
            TotalLatencySamples = _reporters.Sum(r => r.LatencySamples);
        }
    }
}
