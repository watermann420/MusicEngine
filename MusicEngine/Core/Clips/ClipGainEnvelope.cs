//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Per-clip gain automation envelope with interpolation and JSON serialization.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MusicEngine.Core.Clips;

/// <summary>
/// Interpolation mode for gain envelope.
/// </summary>
public enum GainInterpolation
{
    /// <summary>Linear interpolation between points.</summary>
    Linear,
    /// <summary>Smooth (cubic) interpolation for natural curves.</summary>
    Smooth,
    /// <summary>Step/hold - instant jump to next value.</summary>
    Step
}

/// <summary>
/// Represents a single gain point in the envelope.
/// </summary>
public class GainPoint : IComparable<GainPoint>
{
    /// <summary>Time position within the clip in seconds.</summary>
    [JsonPropertyName("time")]
    public double TimeSeconds { get; set; }

    /// <summary>Gain value in dB (-inf to +24 dB).</summary>
    [JsonPropertyName("gainDb")]
    public float GainDb { get; set; }

    /// <summary>Interpolation mode to use when transitioning to the next point.</summary>
    [JsonPropertyName("interpolation")]
    public GainInterpolation Interpolation { get; set; } = GainInterpolation.Linear;

    /// <summary>Linear gain value calculated from GainDb.</summary>
    [JsonIgnore]
    public float GainLinear => GainDb <= -96f ? 0f : MathF.Pow(10f, GainDb / 20f);

    /// <summary>
    /// Creates a new gain point.
    /// </summary>
    public GainPoint() { }

    /// <summary>
    /// Creates a new gain point with specified values.
    /// </summary>
    /// <param name="timeSeconds">Time position in seconds.</param>
    /// <param name="gainDb">Gain value in dB.</param>
    /// <param name="interpolation">Interpolation mode.</param>
    public GainPoint(double timeSeconds, float gainDb, GainInterpolation interpolation = GainInterpolation.Linear)
    {
        TimeSeconds = timeSeconds;
        GainDb = gainDb;
        Interpolation = interpolation;
    }

    public int CompareTo(GainPoint? other)
    {
        if (other == null) return 1;
        return TimeSeconds.CompareTo(other.TimeSeconds);
    }
}

/// <summary>
/// Per-clip gain automation envelope that operates independently of track automation.
/// Provides flexible gain control with multiple interpolation modes and JSON serialization.
/// </summary>
public class ClipGainEnvelope
{
    private readonly List<GainPoint> _points = new();
    private readonly object _lock = new();
    private bool _isDirty = true;
    private GainPoint[]? _sortedPoints;

    /// <summary>
    /// Gets the gain points in the envelope (read-only).
    /// </summary>
    [JsonPropertyName("points")]
    public IReadOnlyList<GainPoint> Points
    {
        get
        {
            lock (_lock)
            {
                return _points.AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Gets or sets the default gain in dB when no points are defined.
    /// </summary>
    [JsonPropertyName("defaultGainDb")]
    public float DefaultGainDb { get; set; } = 0f;

    /// <summary>
    /// Gets or sets whether the envelope is enabled.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the clip length in seconds (for normalization).
    /// </summary>
    [JsonPropertyName("clipLength")]
    public double ClipLengthSeconds { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the global interpolation mode (used if points don't specify their own).
    /// </summary>
    [JsonPropertyName("globalInterpolation")]
    public GainInterpolation GlobalInterpolation { get; set; } = GainInterpolation.Linear;

    /// <summary>
    /// Creates a new empty clip gain envelope.
    /// </summary>
    public ClipGainEnvelope() { }

    /// <summary>
    /// Creates a new clip gain envelope with the specified clip length.
    /// </summary>
    /// <param name="clipLengthSeconds">Length of the clip in seconds.</param>
    public ClipGainEnvelope(double clipLengthSeconds)
    {
        ClipLengthSeconds = clipLengthSeconds;
    }

    /// <summary>
    /// Adds a gain point to the envelope.
    /// </summary>
    /// <param name="timeSeconds">Time position in seconds.</param>
    /// <param name="gainDb">Gain value in dB.</param>
    /// <param name="interpolation">Interpolation mode.</param>
    /// <returns>The created gain point.</returns>
    public GainPoint AddPoint(double timeSeconds, float gainDb, GainInterpolation interpolation = GainInterpolation.Linear)
    {
        var point = new GainPoint(timeSeconds, gainDb, interpolation);
        lock (_lock)
        {
            _points.Add(point);
            _isDirty = true;
        }
        return point;
    }

    /// <summary>
    /// Removes a gain point from the envelope.
    /// </summary>
    /// <param name="point">The point to remove.</param>
    /// <returns>True if the point was removed.</returns>
    public bool RemovePoint(GainPoint point)
    {
        lock (_lock)
        {
            bool removed = _points.Remove(point);
            if (removed) _isDirty = true;
            return removed;
        }
    }

    /// <summary>
    /// Removes the gain point closest to the specified time.
    /// </summary>
    /// <param name="timeSeconds">Time position to search near.</param>
    /// <param name="toleranceSeconds">Maximum time difference to consider.</param>
    /// <returns>True if a point was removed.</returns>
    public bool RemovePointAt(double timeSeconds, double toleranceSeconds = 0.01)
    {
        lock (_lock)
        {
            var point = _points
                .Where(p => Math.Abs(p.TimeSeconds - timeSeconds) <= toleranceSeconds)
                .OrderBy(p => Math.Abs(p.TimeSeconds - timeSeconds))
                .FirstOrDefault();

            if (point != null)
            {
                _points.Remove(point);
                _isDirty = true;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Clears all gain points from the envelope.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _points.Clear();
            _isDirty = true;
        }
    }

    /// <summary>
    /// Resets the envelope to default state (no points, 0 dB default).
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _points.Clear();
            DefaultGainDb = 0f;
            _isDirty = true;
        }
    }

    /// <summary>
    /// Gets the gain value in dB at the specified time.
    /// </summary>
    /// <param name="timeSeconds">Time position in seconds.</param>
    /// <returns>Gain value in dB.</returns>
    public float GetGainDbAt(double timeSeconds)
    {
        var points = GetSortedPoints();

        if (points.Length == 0)
            return DefaultGainDb;

        // Before first point
        if (timeSeconds <= points[0].TimeSeconds)
            return points[0].GainDb;

        // After last point
        if (timeSeconds >= points[^1].TimeSeconds)
            return points[^1].GainDb;

        // Find surrounding points
        for (int i = 0; i < points.Length - 1; i++)
        {
            var p1 = points[i];
            var p2 = points[i + 1];

            if (timeSeconds >= p1.TimeSeconds && timeSeconds < p2.TimeSeconds)
            {
                return InterpolateGain(p1, p2, timeSeconds);
            }
        }

        return DefaultGainDb;
    }

    /// <summary>
    /// Gets the linear gain value at the specified time.
    /// </summary>
    /// <param name="timeSeconds">Time position in seconds.</param>
    /// <returns>Linear gain multiplier.</returns>
    public float GetGainLinearAt(double timeSeconds)
    {
        float db = GetGainDbAt(timeSeconds);
        return db <= -96f ? 0f : MathF.Pow(10f, db / 20f);
    }

    /// <summary>
    /// Applies the gain envelope to audio samples.
    /// </summary>
    /// <param name="samples">Audio samples to process.</param>
    /// <param name="startTimeSeconds">Start time of the samples within the clip.</param>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="channels">Number of audio channels.</param>
    public void ApplyToSamples(float[] samples, double startTimeSeconds, int sampleRate, int channels = 1)
    {
        if (!Enabled) return;

        double deltaTime = 1.0 / sampleRate;
        double time = startTimeSeconds;

        for (int i = 0; i < samples.Length; i += channels)
        {
            float gain = GetGainLinearAt(time);

            for (int ch = 0; ch < channels; ch++)
            {
                if (i + ch < samples.Length)
                {
                    samples[i + ch] *= gain;
                }
            }

            time += deltaTime;
        }
    }

    /// <summary>
    /// Applies the gain envelope to a destination buffer from a source buffer.
    /// </summary>
    /// <param name="source">Source samples.</param>
    /// <param name="destination">Destination buffer.</param>
    /// <param name="destOffset">Offset into destination buffer.</param>
    /// <param name="count">Number of samples to process.</param>
    /// <param name="startTimeSeconds">Start time within the clip.</param>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="channels">Number of audio channels.</param>
    public void ApplyToBuffer(float[] source, float[] destination, int destOffset, int count,
        double startTimeSeconds, int sampleRate, int channels = 1)
    {
        double deltaTime = 1.0 / sampleRate;
        double time = startTimeSeconds;

        for (int i = 0; i < count; i += channels)
        {
            float gain = Enabled ? GetGainLinearAt(time) : 1f;

            for (int ch = 0; ch < channels; ch++)
            {
                int srcIdx = i + ch;
                int dstIdx = destOffset + i + ch;
                if (srcIdx < source.Length && dstIdx < destination.Length)
                {
                    destination[dstIdx] = source[srcIdx] * gain;
                }
            }

            time += deltaTime;
        }
    }

    /// <summary>
    /// Normalizes the envelope so the maximum gain point is at the specified level.
    /// </summary>
    /// <param name="targetMaxDb">Target maximum gain in dB (default: 0 dB).</param>
    public void Normalize(float targetMaxDb = 0f)
    {
        lock (_lock)
        {
            if (_points.Count == 0) return;

            float maxGain = _points.Max(p => p.GainDb);
            float offset = targetMaxDb - maxGain;

            foreach (var point in _points)
            {
                point.GainDb += offset;
            }
            _isDirty = true;
        }
    }

    /// <summary>
    /// Scales all gain values by a fixed amount in dB.
    /// </summary>
    /// <param name="offsetDb">Amount to add to all gain values.</param>
    public void OffsetAll(float offsetDb)
    {
        lock (_lock)
        {
            foreach (var point in _points)
            {
                point.GainDb += offsetDb;
            }
            _isDirty = true;
        }
    }

    /// <summary>
    /// Creates a simple fade-in envelope.
    /// </summary>
    /// <param name="fadeDuration">Duration of fade in seconds.</param>
    /// <param name="startDb">Starting gain in dB (default: -inf).</param>
    /// <param name="endDb">Ending gain in dB (default: 0).</param>
    public void CreateFadeIn(double fadeDuration, float startDb = -96f, float endDb = 0f)
    {
        lock (_lock)
        {
            _points.Clear();
            _points.Add(new GainPoint(0, startDb, GainInterpolation.Smooth));
            _points.Add(new GainPoint(fadeDuration, endDb, GainInterpolation.Linear));
            _isDirty = true;
        }
    }

    /// <summary>
    /// Creates a simple fade-out envelope.
    /// </summary>
    /// <param name="fadeStartTime">Time when fade starts.</param>
    /// <param name="fadeDuration">Duration of fade in seconds.</param>
    /// <param name="startDb">Starting gain in dB (default: 0).</param>
    /// <param name="endDb">Ending gain in dB (default: -inf).</param>
    public void CreateFadeOut(double fadeStartTime, double fadeDuration, float startDb = 0f, float endDb = -96f)
    {
        lock (_lock)
        {
            _points.Clear();
            _points.Add(new GainPoint(0, startDb, GainInterpolation.Linear));
            _points.Add(new GainPoint(fadeStartTime, startDb, GainInterpolation.Smooth));
            _points.Add(new GainPoint(fadeStartTime + fadeDuration, endDb, GainInterpolation.Linear));
            _isDirty = true;
        }
    }

    /// <summary>
    /// Creates a crossfade envelope (fade out, then fade in).
    /// </summary>
    /// <param name="crossfadeCenter">Center point of crossfade in seconds.</param>
    /// <param name="crossfadeDuration">Total duration of crossfade.</param>
    public void CreateCrossfade(double crossfadeCenter, double crossfadeDuration)
    {
        double halfDuration = crossfadeDuration / 2;
        lock (_lock)
        {
            _points.Clear();
            _points.Add(new GainPoint(0, 0f, GainInterpolation.Linear));
            _points.Add(new GainPoint(crossfadeCenter - halfDuration, 0f, GainInterpolation.Smooth));
            _points.Add(new GainPoint(crossfadeCenter, -6f, GainInterpolation.Smooth));
            _points.Add(new GainPoint(crossfadeCenter + halfDuration, 0f, GainInterpolation.Linear));
            _isDirty = true;
        }
    }

    /// <summary>
    /// Serializes the envelope to JSON string.
    /// </summary>
    /// <returns>JSON representation of the envelope.</returns>
    public string ToJson()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var data = new ClipGainEnvelopeData
        {
            Points = _points.ToList(),
            DefaultGainDb = DefaultGainDb,
            Enabled = Enabled,
            ClipLengthSeconds = ClipLengthSeconds,
            GlobalInterpolation = GlobalInterpolation
        };

        return JsonSerializer.Serialize(data, options);
    }

    /// <summary>
    /// Deserializes an envelope from JSON string.
    /// </summary>
    /// <param name="json">JSON string.</param>
    /// <returns>Deserialized envelope.</returns>
    public static ClipGainEnvelope FromJson(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var data = JsonSerializer.Deserialize<ClipGainEnvelopeData>(json, options);
        if (data == null)
            return new ClipGainEnvelope();

        var envelope = new ClipGainEnvelope(data.ClipLengthSeconds)
        {
            DefaultGainDb = data.DefaultGainDb,
            Enabled = data.Enabled,
            GlobalInterpolation = data.GlobalInterpolation
        };

        foreach (var point in data.Points)
        {
            envelope._points.Add(point);
        }
        envelope._isDirty = true;

        return envelope;
    }

    private GainPoint[] GetSortedPoints()
    {
        lock (_lock)
        {
            if (_isDirty || _sortedPoints == null)
            {
                _sortedPoints = _points.OrderBy(p => p.TimeSeconds).ToArray();
                _isDirty = false;
            }
            return _sortedPoints;
        }
    }

    private float InterpolateGain(GainPoint p1, GainPoint p2, double time)
    {
        double t = (time - p1.TimeSeconds) / (p2.TimeSeconds - p1.TimeSeconds);
        t = Math.Clamp(t, 0, 1);

        var interpolation = p1.Interpolation;

        switch (interpolation)
        {
            case GainInterpolation.Step:
                return p1.GainDb;

            case GainInterpolation.Smooth:
                // Cubic smoothstep interpolation
                t = t * t * (3 - 2 * t);
                break;

            case GainInterpolation.Linear:
            default:
                // Linear interpolation (t is already linear)
                break;
        }

        return (float)(p1.GainDb + (p2.GainDb - p1.GainDb) * t);
    }

    /// <summary>
    /// Internal data class for JSON serialization.
    /// </summary>
    private class ClipGainEnvelopeData
    {
        [JsonPropertyName("points")]
        public List<GainPoint> Points { get; set; } = new();

        [JsonPropertyName("defaultGainDb")]
        public float DefaultGainDb { get; set; }

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }

        [JsonPropertyName("clipLengthSeconds")]
        public double ClipLengthSeconds { get; set; }

        [JsonPropertyName("globalInterpolation")]
        public GainInterpolation GlobalInterpolation { get; set; }
    }
}
