// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio analysis component.

using System;
using NAudio.Wave;

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Provides X/Y coordinate data for goniometer/vectorscope display.
/// Generates Lissajous-style visualization data for stereo image analysis.
/// X-axis represents Side (L-R), Y-axis represents Mid (L+R).
/// </summary>
public class GoniometerDataProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly int _maxPoints;
    private readonly GoniometerPoint[] _points;
    private int _writeIndex;
    private int _pointCount;
    private readonly object _lock = new();

    // Rotation and scaling
    private double _rotationAngle = Math.PI / 4; // 45 degrees for standard goniometer display
    private float _scale = 1.0f;
    private float _decay = 0.95f;

    /// <summary>
    /// Gets the wave format of the audio stream.
    /// </summary>
    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>
    /// Gets or sets the rotation angle in radians (default: PI/4 for 45-degree rotation).
    /// </summary>
    public double RotationAngle
    {
        get => _rotationAngle;
        set => _rotationAngle = value;
    }

    /// <summary>
    /// Gets or sets the display scale factor.
    /// </summary>
    public float Scale
    {
        get => _scale;
        set => _scale = Math.Max(0.1f, value);
    }

    /// <summary>
    /// Gets or sets the point decay factor (0 = instant, 1 = no decay).
    /// </summary>
    public float Decay
    {
        get => _decay;
        set => _decay = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Gets the maximum number of points in the display buffer.
    /// </summary>
    public int MaxPoints => _maxPoints;

    /// <summary>
    /// Gets the current number of valid points.
    /// </summary>
    public int PointCount
    {
        get
        {
            lock (_lock)
            {
                return _pointCount;
            }
        }
    }

    /// <summary>
    /// Event raised when new goniometer data is available.
    /// </summary>
    public event EventHandler<GoniometerEventArgs>? DataUpdated;

    /// <summary>
    /// Creates a new goniometer data provider wrapping the specified stereo audio source.
    /// </summary>
    /// <param name="source">The stereo audio source to analyze. Must have 2 channels.</param>
    /// <param name="maxPoints">Maximum number of points to store (default: 512).</param>
    public GoniometerDataProvider(ISampleProvider source, int maxPoints = 512)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));

        if (source.WaveFormat.Channels != 2)
            throw new ArgumentException("Goniometer requires a stereo (2-channel) source.", nameof(source));

        _maxPoints = maxPoints;
        _points = new GoniometerPoint[maxPoints];
        for (int i = 0; i < maxPoints; i++)
        {
            _points[i] = new GoniometerPoint();
        }
    }

    /// <summary>
    /// Reads audio samples, generates goniometer data, and passes through unchanged.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = _source.Read(buffer, offset, count);
        if (samplesRead == 0) return 0;

        ProcessSamples(buffer, offset, samplesRead);
        return samplesRead;
    }

    /// <summary>
    /// Process samples without being in the signal chain (for external analysis).
    /// </summary>
    /// <param name="samples">Interleaved stereo samples.</param>
    /// <param name="count">Number of samples (must be even for stereo).</param>
    public void AnalyzeSamples(float[] samples, int count)
    {
        ProcessSamples(samples, 0, count);
    }

    /// <summary>
    /// Gets a copy of the current goniometer points.
    /// </summary>
    public GoniometerPoint[] GetPoints()
    {
        lock (_lock)
        {
            GoniometerPoint[] result = new GoniometerPoint[_pointCount];
            for (int i = 0; i < _pointCount; i++)
            {
                int idx = (_writeIndex - _pointCount + i + _maxPoints) % _maxPoints;
                result[i] = new GoniometerPoint
                {
                    X = _points[idx].X,
                    Y = _points[idx].Y,
                    Intensity = _points[idx].Intensity
                };
            }
            return result;
        }
    }

    /// <summary>
    /// Clears all stored points.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _writeIndex = 0;
            _pointCount = 0;
            for (int i = 0; i < _maxPoints; i++)
            {
                _points[i].X = 0;
                _points[i].Y = 0;
                _points[i].Intensity = 0;
            }
        }
    }

    private void ProcessSamples(float[] buffer, int offset, int count)
    {
        int frames = count / 2; // Stereo interleaved
        double cosAngle = Math.Cos(_rotationAngle);
        double sinAngle = Math.Sin(_rotationAngle);

        lock (_lock)
        {
            // Apply decay to existing points
            for (int i = 0; i < _pointCount; i++)
            {
                int idx = (_writeIndex - _pointCount + i + _maxPoints) % _maxPoints;
                _points[idx].Intensity *= _decay;
            }

            // Process new samples
            for (int i = 0; i < frames; i++)
            {
                float left = buffer[offset + i * 2];
                float right = buffer[offset + i * 2 + 1];

                // Calculate Mid/Side
                float mid = (left + right) * 0.5f;
                float side = (left - right) * 0.5f;

                // Apply rotation (standard goniometer is rotated 45 degrees)
                float x = (float)(side * cosAngle - mid * sinAngle) * _scale;
                float y = (float)(side * sinAngle + mid * cosAngle) * _scale;

                // Calculate intensity based on signal level
                float intensity = (float)Math.Sqrt(left * left + right * right);

                // Store point
                _points[_writeIndex].X = x;
                _points[_writeIndex].Y = y;
                _points[_writeIndex].Intensity = Math.Min(1.0f, intensity);

                _writeIndex = (_writeIndex + 1) % _maxPoints;
                if (_pointCount < _maxPoints)
                {
                    _pointCount++;
                }
            }
        }

        // Raise event
        DataUpdated?.Invoke(this, new GoniometerEventArgs(GetPoints()));
    }
}

/// <summary>
/// Represents a single point in the goniometer display.
/// </summary>
public class GoniometerPoint
{
    /// <summary>
    /// X coordinate (Side / L-R difference).
    /// Range: -1 to +1 (left to right).
    /// </summary>
    public float X { get; set; }

    /// <summary>
    /// Y coordinate (Mid / L+R sum).
    /// Range: -1 to +1 (negative to positive correlation).
    /// </summary>
    public float Y { get; set; }

    /// <summary>
    /// Intensity/brightness of the point (0 to 1).
    /// Based on signal amplitude, decays over time.
    /// </summary>
    public float Intensity { get; set; }
}

/// <summary>
/// Event arguments for goniometer data updates.
/// </summary>
public class GoniometerEventArgs : EventArgs
{
    /// <summary>
    /// Array of goniometer points for visualization.
    /// </summary>
    public GoniometerPoint[] Points { get; }

    public GoniometerEventArgs(GoniometerPoint[] points)
    {
        Points = points;
    }
}
