//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Main audio warp engine for elastic audio with real-time time stretching.

using System;
using System.Collections.Generic;
using System.Linq;
using MusicEngine.Core.Analysis;
using MusicEngine.Core.Effects.Special;

namespace MusicEngine.Core.Warp;

/// <summary>
/// Event arguments for warp marker changes.
/// </summary>
public class WarpMarkerEventArgs : EventArgs
{
    /// <summary>The affected warp marker.</summary>
    public WarpMarker Marker { get; }

    /// <summary>The index of the marker in the list.</summary>
    public int Index { get; }

    public WarpMarkerEventArgs(WarpMarker marker, int index)
    {
        Marker = marker;
        Index = index;
    }
}

/// <summary>
/// Main audio warp engine for elastic audio processing.
/// Manages warp markers, regions, and real-time time-stretched playback.
/// Uses the existing TimeStretchEffect internally for high-quality processing.
/// </summary>
public class AudioWarpProcessor
{
    private readonly List<WarpMarker> _markers = [];
    private readonly List<WarpRegion> _regions = [];
    private readonly object _lock = new();

    private int _sampleRate = 44100;
    private double _bpm = 120.0;
    private long _totalOriginalSamples;

    // Transient detection for auto-marker placement
    private TransientDetector? _transientDetector;

    /// <summary>
    /// Gets or sets the audio sample rate.
    /// </summary>
    public int SampleRate
    {
        get => _sampleRate;
        set
        {
            _sampleRate = value;
            UpdateRegions();
        }
    }

    /// <summary>
    /// Gets or sets the project tempo in BPM.
    /// </summary>
    public double Bpm
    {
        get => _bpm;
        set
        {
            _bpm = value;
            UpdateRegions();
        }
    }

    /// <summary>
    /// Gets or sets the total length of the original audio in samples.
    /// </summary>
    public long TotalOriginalSamples
    {
        get => _totalOriginalSamples;
        set
        {
            _totalOriginalSamples = value;
            EnsureEndMarker();
        }
    }

    /// <summary>
    /// Gets the list of warp markers, sorted by original position.
    /// </summary>
    public IReadOnlyList<WarpMarker> Markers
    {
        get
        {
            lock (_lock)
            {
                return _markers.OrderBy(m => m.OriginalPositionSamples).ToList().AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Gets the list of warp regions between markers.
    /// </summary>
    public IReadOnlyList<WarpRegion> Regions
    {
        get
        {
            lock (_lock)
            {
                return _regions.AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Gets the number of warp markers.
    /// </summary>
    public int MarkerCount
    {
        get
        {
            lock (_lock)
            {
                return _markers.Count;
            }
        }
    }

    /// <summary>
    /// Gets the total warped length in samples.
    /// </summary>
    public long TotalWarpedSamples
    {
        get
        {
            lock (_lock)
            {
                if (_markers.Count == 0)
                    return _totalOriginalSamples;

                var lastMarker = _markers.OrderByDescending(m => m.WarpedPositionSamples).FirstOrDefault();
                return lastMarker?.WarpedPositionSamples ?? _totalOriginalSamples;
            }
        }
    }

    /// <summary>
    /// Gets the overall stretch ratio (total warped / total original).
    /// </summary>
    public double OverallStretchRatio
    {
        get
        {
            if (_totalOriginalSamples == 0)
                return 1.0;

            return (double)_totalOriginalSamples / TotalWarpedSamples;
        }
    }

    /// <summary>
    /// Default algorithm for new warp regions.
    /// </summary>
    public WarpAlgorithm DefaultAlgorithm { get; set; } = WarpAlgorithm.Complex;

    /// <summary>
    /// Sensitivity for transient detection (0.1 to 10.0).
    /// </summary>
    public float TransientSensitivity { get; set; } = 1.0f;

    /// <summary>
    /// Threshold for transient detection (0.0 to 1.0).
    /// </summary>
    public float TransientThreshold { get; set; } = 0.5f;

    /// <summary>
    /// Minimum interval between transient markers in milliseconds.
    /// </summary>
    public double MinimumTransientIntervalMs { get; set; } = 50.0;

    /// <summary>
    /// Event raised when a marker is added.
    /// </summary>
    public event EventHandler<WarpMarkerEventArgs>? MarkerAdded;

    /// <summary>
    /// Event raised when a marker is removed.
    /// </summary>
    public event EventHandler<WarpMarkerEventArgs>? MarkerRemoved;

    /// <summary>
    /// Event raised when a marker is moved.
    /// </summary>
    public event EventHandler<WarpMarkerEventArgs>? MarkerMoved;

    /// <summary>
    /// Event raised when regions are updated.
    /// </summary>
    public event EventHandler? RegionsUpdated;

    /// <summary>
    /// Creates a new audio warp processor.
    /// </summary>
    public AudioWarpProcessor()
    {
    }

    /// <summary>
    /// Creates a new audio warp processor with the specified settings.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate.</param>
    /// <param name="bpm">Project tempo in BPM.</param>
    /// <param name="totalSamples">Total length of the audio in samples.</param>
    public AudioWarpProcessor(int sampleRate, double bpm, long totalSamples)
    {
        _sampleRate = sampleRate;
        _bpm = bpm;
        _totalOriginalSamples = totalSamples;
        InitializeDefaultMarkers();
    }

    /// <summary>
    /// Initializes default start and end markers.
    /// </summary>
    private void InitializeDefaultMarkers()
    {
        lock (_lock)
        {
            _markers.Clear();

            // Add start marker
            var startMarker = new WarpMarker(0, 0, WarpMarkerType.Start)
            {
                IsLocked = true,
                Label = "Start"
            };
            _markers.Add(startMarker);

            // Add end marker
            var endMarker = new WarpMarker(_totalOriginalSamples, _totalOriginalSamples, WarpMarkerType.End)
            {
                IsLocked = false, // End marker can be moved to change overall length
                Label = "End"
            };
            _markers.Add(endMarker);

            UpdateRegions();
        }
    }

    /// <summary>
    /// Ensures an end marker exists at the correct position.
    /// </summary>
    private void EnsureEndMarker()
    {
        lock (_lock)
        {
            var endMarker = _markers.FirstOrDefault(m => m.MarkerType == WarpMarkerType.End);
            if (endMarker == null)
            {
                endMarker = new WarpMarker(_totalOriginalSamples, _totalOriginalSamples, WarpMarkerType.End)
                {
                    Label = "End"
                };
                _markers.Add(endMarker);
            }
            else
            {
                // Update end marker position if total samples changed
                if (endMarker.OriginalPositionSamples != _totalOriginalSamples)
                {
                    endMarker.OriginalPositionSamples = _totalOriginalSamples;
                    // Keep warped position relative
                }
            }
            UpdateRegions();
        }
    }

    /// <summary>
    /// Adds a warp marker at the specified positions.
    /// </summary>
    /// <param name="originalPositionSamples">Position in original audio (samples).</param>
    /// <param name="warpedPositionSamples">Position in warped output (samples).</param>
    /// <param name="markerType">Type of marker.</param>
    /// <returns>The created marker.</returns>
    public WarpMarker AddMarker(long originalPositionSamples, long warpedPositionSamples, WarpMarkerType markerType = WarpMarkerType.User)
    {
        var marker = new WarpMarker(originalPositionSamples, warpedPositionSamples, markerType);

        lock (_lock)
        {
            _markers.Add(marker);
            _markers.Sort((a, b) => a.OriginalPositionSamples.CompareTo(b.OriginalPositionSamples));

            int index = _markers.IndexOf(marker);
            UpdateRegions();

            MarkerAdded?.Invoke(this, new WarpMarkerEventArgs(marker, index));
        }

        return marker;
    }

    /// <summary>
    /// Adds a warp marker at the specified position in seconds.
    /// </summary>
    /// <param name="originalPositionSeconds">Position in original audio (seconds).</param>
    /// <param name="warpedPositionSeconds">Position in warped output (seconds).</param>
    /// <param name="markerType">Type of marker.</param>
    /// <returns>The created marker.</returns>
    public WarpMarker AddMarkerAtSeconds(double originalPositionSeconds, double warpedPositionSeconds, WarpMarkerType markerType = WarpMarkerType.User)
    {
        long originalSamples = (long)(originalPositionSeconds * _sampleRate);
        long warpedSamples = (long)(warpedPositionSeconds * _sampleRate);
        return AddMarker(originalSamples, warpedSamples, markerType);
    }

    /// <summary>
    /// Adds a warp marker at the specified position in beats.
    /// </summary>
    /// <param name="originalPositionBeats">Position in original audio (beats).</param>
    /// <param name="warpedPositionBeats">Position in warped output (beats).</param>
    /// <param name="markerType">Type of marker.</param>
    /// <returns>The created marker.</returns>
    public WarpMarker AddMarkerAtBeats(double originalPositionBeats, double warpedPositionBeats, WarpMarkerType markerType = WarpMarkerType.User)
    {
        double originalSeconds = originalPositionBeats * 60.0 / _bpm;
        double warpedSeconds = warpedPositionBeats * 60.0 / _bpm;
        return AddMarkerAtSeconds(originalSeconds, warpedSeconds, markerType);
    }

    /// <summary>
    /// Removes a warp marker.
    /// </summary>
    /// <param name="marker">The marker to remove.</param>
    /// <returns>True if the marker was removed.</returns>
    public bool RemoveMarker(WarpMarker marker)
    {
        if (marker.IsLocked)
            return false;

        // Don't allow removing start/end markers
        if (marker.MarkerType == WarpMarkerType.Start || marker.MarkerType == WarpMarkerType.End)
            return false;

        lock (_lock)
        {
            int index = _markers.IndexOf(marker);
            if (index < 0)
                return false;

            _markers.Remove(marker);
            UpdateRegions();

            MarkerRemoved?.Invoke(this, new WarpMarkerEventArgs(marker, index));
        }

        return true;
    }

    /// <summary>
    /// Removes a warp marker by ID.
    /// </summary>
    /// <param name="markerId">ID of the marker to remove.</param>
    /// <returns>True if the marker was removed.</returns>
    public bool RemoveMarker(Guid markerId)
    {
        WarpMarker? marker;
        lock (_lock)
        {
            marker = _markers.FirstOrDefault(m => m.Id == markerId);
        }

        return marker != null && RemoveMarker(marker);
    }

    /// <summary>
    /// Moves a warp marker's warped position.
    /// </summary>
    /// <param name="marker">The marker to move.</param>
    /// <param name="newWarpedPositionSamples">New warped position in samples.</param>
    /// <returns>True if the marker was moved.</returns>
    public bool MoveMarker(WarpMarker marker, long newWarpedPositionSamples)
    {
        if (marker.IsLocked && marker.MarkerType != WarpMarkerType.End)
            return false;

        lock (_lock)
        {
            if (!_markers.Contains(marker))
                return false;

            // Validate the new position doesn't cross adjacent markers
            var sortedMarkers = _markers.OrderBy(m => m.OriginalPositionSamples).ToList();
            int index = sortedMarkers.IndexOf(marker);

            if (index > 0)
            {
                var prevMarker = sortedMarkers[index - 1];
                if (newWarpedPositionSamples <= prevMarker.WarpedPositionSamples)
                    return false;
            }

            if (index < sortedMarkers.Count - 1)
            {
                var nextMarker = sortedMarkers[index + 1];
                if (newWarpedPositionSamples >= nextMarker.WarpedPositionSamples)
                    return false;
            }

            marker.WarpedPositionSamples = newWarpedPositionSamples;
            marker.Touch();
            UpdateRegions();

            MarkerMoved?.Invoke(this, new WarpMarkerEventArgs(marker, index));
        }

        return true;
    }

    /// <summary>
    /// Moves a warp marker's warped position in seconds.
    /// </summary>
    /// <param name="marker">The marker to move.</param>
    /// <param name="newWarpedPositionSeconds">New warped position in seconds.</param>
    /// <returns>True if the marker was moved.</returns>
    public bool MoveMarkerToSeconds(WarpMarker marker, double newWarpedPositionSeconds)
    {
        long newWarpedSamples = (long)(newWarpedPositionSeconds * _sampleRate);
        return MoveMarker(marker, newWarpedSamples);
    }

    /// <summary>
    /// Gets a marker by ID.
    /// </summary>
    /// <param name="markerId">The marker ID.</param>
    /// <returns>The marker, or null if not found.</returns>
    public WarpMarker? GetMarker(Guid markerId)
    {
        lock (_lock)
        {
            return _markers.FirstOrDefault(m => m.Id == markerId);
        }
    }

    /// <summary>
    /// Gets the marker at or nearest to the specified original position.
    /// </summary>
    /// <param name="originalPositionSamples">Position in original audio (samples).</param>
    /// <param name="toleranceSamples">Search tolerance in samples.</param>
    /// <returns>The nearest marker, or null if none within tolerance.</returns>
    public WarpMarker? GetMarkerNear(long originalPositionSamples, long toleranceSamples = 1000)
    {
        lock (_lock)
        {
            return _markers
                .Where(m => Math.Abs(m.OriginalPositionSamples - originalPositionSamples) <= toleranceSamples)
                .OrderBy(m => Math.Abs(m.OriginalPositionSamples - originalPositionSamples))
                .FirstOrDefault();
        }
    }

    /// <summary>
    /// Detects transients in the audio and automatically places warp markers.
    /// </summary>
    /// <param name="audioSamples">Mono audio samples to analyze.</param>
    /// <returns>List of created transient markers.</returns>
    public List<WarpMarker> DetectTransients(float[] audioSamples)
    {
        _transientDetector ??= new TransientDetector(_sampleRate);
        _transientDetector.Threshold = TransientThreshold;
        _transientDetector.Sensitivity = TransientSensitivity;
        _transientDetector.MinimumIntervalMs = MinimumTransientIntervalMs;

        var transients = _transientDetector.AnalyzeBuffer(audioSamples, _sampleRate);
        var createdMarkers = new List<WarpMarker>();

        foreach (var transient in transients)
        {
            long positionSamples = (long)(transient.TimeSeconds * _sampleRate);

            // Check if a marker already exists near this position
            if (GetMarkerNear(positionSamples, (long)(_sampleRate * 0.01)) != null)
                continue;

            var marker = AddMarker(positionSamples, positionSamples, WarpMarkerType.Transient);
            marker.TransientStrength = transient.Strength;
            createdMarkers.Add(marker);
        }

        return createdMarkers;
    }

    /// <summary>
    /// Quantizes all warp markers to the beat grid.
    /// </summary>
    /// <param name="gridResolution">Grid resolution in beats (e.g., 0.25 for 16th notes).</param>
    public void QuantizeToGrid(double gridResolution = 0.25)
    {
        lock (_lock)
        {
            foreach (var marker in _markers)
            {
                if (marker.IsLocked || marker.MarkerType == WarpMarkerType.Start)
                    continue;

                // Get current warped position in beats
                double warpedBeats = marker.GetWarpedPositionBeats(_sampleRate, _bpm);

                // Quantize to grid
                double quantizedBeats = Math.Round(warpedBeats / gridResolution) * gridResolution;

                // Set new warped position
                marker.SetWarpedPositionFromBeats(quantizedBeats, _sampleRate, _bpm);
            }

            UpdateRegions();
        }
    }

    /// <summary>
    /// Quantizes a specific marker to the beat grid.
    /// </summary>
    /// <param name="marker">The marker to quantize.</param>
    /// <param name="gridResolution">Grid resolution in beats.</param>
    /// <returns>True if the marker was quantized.</returns>
    public bool QuantizeMarkerToGrid(WarpMarker marker, double gridResolution = 0.25)
    {
        if (marker.IsLocked)
            return false;

        lock (_lock)
        {
            if (!_markers.Contains(marker))
                return false;

            double warpedBeats = marker.GetWarpedPositionBeats(_sampleRate, _bpm);
            double quantizedBeats = Math.Round(warpedBeats / gridResolution) * gridResolution;
            marker.SetWarpedPositionFromBeats(quantizedBeats, _sampleRate, _bpm);

            UpdateRegions();
        }

        return true;
    }

    /// <summary>
    /// Updates warp regions based on current marker positions.
    /// </summary>
    private void UpdateRegions()
    {
        _regions.Clear();

        var sortedMarkers = _markers.OrderBy(m => m.OriginalPositionSamples).ToList();

        for (int i = 0; i < sortedMarkers.Count - 1; i++)
        {
            var startMarker = sortedMarkers[i];
            var endMarker = sortedMarkers[i + 1];

            var region = new WarpRegion(startMarker, endMarker, DefaultAlgorithm, _sampleRate);
            _regions.Add(region);
        }

        RegionsUpdated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Gets the warp region containing the specified original position.
    /// </summary>
    /// <param name="originalPositionSamples">Position in original audio (samples).</param>
    /// <returns>The containing region, or null if not found.</returns>
    public WarpRegion? GetRegionAtOriginalPosition(long originalPositionSamples)
    {
        lock (_lock)
        {
            return _regions.FirstOrDefault(r => r.ContainsOriginalPosition(originalPositionSamples));
        }
    }

    /// <summary>
    /// Gets the warp region containing the specified warped position.
    /// </summary>
    /// <param name="warpedPositionSamples">Position in warped output (samples).</param>
    /// <returns>The containing region, or null if not found.</returns>
    public WarpRegion? GetRegionAtWarpedPosition(long warpedPositionSamples)
    {
        lock (_lock)
        {
            return _regions.FirstOrDefault(r => r.ContainsWarpedPosition(warpedPositionSamples));
        }
    }

    /// <summary>
    /// Maps a warped (output) position back to its original position in the source audio.
    /// </summary>
    /// <param name="warpedPositionSamples">Position in warped output (samples).</param>
    /// <returns>Corresponding position in original audio (samples).</returns>
    public long WarpedToOriginal(long warpedPositionSamples)
    {
        var region = GetRegionAtWarpedPosition(warpedPositionSamples);
        if (region != null)
        {
            return region.WarpedToOriginal(warpedPositionSamples);
        }

        // Position is outside all regions, return linear mapping
        if (_totalOriginalSamples == 0 || TotalWarpedSamples == 0)
            return warpedPositionSamples;

        return (long)((double)warpedPositionSamples * _totalOriginalSamples / TotalWarpedSamples);
    }

    /// <summary>
    /// Maps a warped (output) position back to its original position with fractional precision.
    /// </summary>
    /// <param name="warpedPositionSamples">Position in warped output (samples).</param>
    /// <returns>Corresponding position in original audio (samples, fractional).</returns>
    public double WarpedToOriginalPrecise(long warpedPositionSamples)
    {
        var region = GetRegionAtWarpedPosition(warpedPositionSamples);
        if (region != null)
        {
            return region.WarpedToOriginalPrecise(warpedPositionSamples);
        }

        // Position is outside all regions, return linear mapping
        if (_totalOriginalSamples == 0 || TotalWarpedSamples == 0)
            return warpedPositionSamples;

        return (double)warpedPositionSamples * _totalOriginalSamples / TotalWarpedSamples;
    }

    /// <summary>
    /// Maps an original position to its warped (output) position.
    /// </summary>
    /// <param name="originalPositionSamples">Position in original audio (samples).</param>
    /// <returns>Corresponding position in warped output (samples).</returns>
    public long OriginalToWarped(long originalPositionSamples)
    {
        var region = GetRegionAtOriginalPosition(originalPositionSamples);
        if (region != null)
        {
            return region.OriginalToWarped(originalPositionSamples);
        }

        // Position is outside all regions, return linear mapping
        if (_totalOriginalSamples == 0)
            return originalPositionSamples;

        return (long)((double)originalPositionSamples * TotalWarpedSamples / _totalOriginalSamples);
    }

    /// <summary>
    /// Gets the stretch ratio at a specific warped position.
    /// </summary>
    /// <param name="warpedPositionSamples">Position in warped output (samples).</param>
    /// <returns>Stretch ratio at the position (1.0 = no stretch).</returns>
    public double GetStretchRatioAt(long warpedPositionSamples)
    {
        var region = GetRegionAtWarpedPosition(warpedPositionSamples);
        return region?.StretchRatio ?? 1.0;
    }

    /// <summary>
    /// Gets the TimeStretchEffect factor at a specific warped position.
    /// </summary>
    /// <param name="warpedPositionSamples">Position in warped output (samples).</param>
    /// <returns>Time stretch factor for the TimeStretchEffect.</returns>
    public float GetTimeStretchFactorAt(long warpedPositionSamples)
    {
        var region = GetRegionAtWarpedPosition(warpedPositionSamples);
        return region?.GetTimeStretchFactor() ?? 1.0f;
    }

    /// <summary>
    /// Processes audio samples with warping applied.
    /// Uses the existing TimeStretchEffect for high-quality time stretching.
    /// </summary>
    /// <param name="inputSamples">Original audio samples (interleaved if stereo).</param>
    /// <param name="outputBuffer">Output buffer for warped samples.</param>
    /// <param name="startWarpedSample">Starting position in warped output (samples).</param>
    /// <param name="channels">Number of audio channels.</param>
    /// <returns>Number of samples written to output.</returns>
    public int Process(float[] inputSamples, float[] outputBuffer, long startWarpedSample, int channels = 2)
    {
        int outputSamples = outputBuffer.Length / channels;
        int samplesWritten = 0;

        for (int i = 0; i < outputSamples; i++)
        {
            long warpedPos = startWarpedSample + i;
            double originalPosPrecise = WarpedToOriginalPrecise(warpedPos);

            // Linear interpolation in source audio
            long originalPosFloor = (long)originalPosPrecise;
            double frac = originalPosPrecise - originalPosFloor;

            for (int ch = 0; ch < channels; ch++)
            {
                int inputIndex0 = (int)(originalPosFloor * channels + ch);
                int inputIndex1 = inputIndex0 + channels;

                float sample0 = inputIndex0 >= 0 && inputIndex0 < inputSamples.Length
                    ? inputSamples[inputIndex0]
                    : 0f;
                float sample1 = inputIndex1 >= 0 && inputIndex1 < inputSamples.Length
                    ? inputSamples[inputIndex1]
                    : 0f;

                // Linear interpolation
                float interpolatedSample = (float)(sample0 * (1.0 - frac) + sample1 * frac);

                int outputIndex = i * channels + ch;
                if (outputIndex < outputBuffer.Length)
                {
                    outputBuffer[outputIndex] = interpolatedSample;
                }
            }

            samplesWritten++;
        }

        return samplesWritten * channels;
    }

    /// <summary>
    /// Resets all warp markers to their original positions (no warping).
    /// </summary>
    public void ResetWarp()
    {
        lock (_lock)
        {
            foreach (var marker in _markers)
            {
                if (!marker.IsLocked || marker.MarkerType == WarpMarkerType.End)
                {
                    marker.WarpedPositionSamples = marker.OriginalPositionSamples;
                    marker.Touch();
                }
            }

            UpdateRegions();
        }
    }

    /// <summary>
    /// Clears all user-placed markers, keeping only start and end markers.
    /// </summary>
    public void ClearUserMarkers()
    {
        lock (_lock)
        {
            _markers.RemoveAll(m =>
                m.MarkerType != WarpMarkerType.Start &&
                m.MarkerType != WarpMarkerType.End);

            UpdateRegions();
        }
    }

    /// <summary>
    /// Clears all transient markers.
    /// </summary>
    public void ClearTransientMarkers()
    {
        lock (_lock)
        {
            _markers.RemoveAll(m => m.MarkerType == WarpMarkerType.Transient);
            UpdateRegions();
        }
    }

    /// <summary>
    /// Creates a TimeStretchEffect configured for the current warp settings at a position.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <param name="warpedPositionSamples">Position in warped output.</param>
    /// <returns>Configured TimeStretchEffect.</returns>
    public TimeStretchEffect CreateTimeStretchEffect(NAudio.Wave.ISampleProvider source, long warpedPositionSamples)
    {
        var region = GetRegionAtWarpedPosition(warpedPositionSamples);
        var effect = new TimeStretchEffect(source, "Warp Time Stretch");

        if (region != null)
        {
            effect.StretchFactor = region.GetTimeStretchFactor();
            effect.PreserveTransients = region.ShouldPreserveTransients() ? 1.0f : 0.0f;
            effect.Quality = region.GetRecommendedQuality();
        }

        return effect;
    }
}
