//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Parameter automation system for automating any object property over time.
//              Supports recording, playback, and multiple curve interpolation types.


using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;


namespace MusicEngine.Core;


/// <summary>
/// Curve type for automation point interpolation.
/// </summary>
public enum CurveType
{
    /// <summary>Linear interpolation between points.</summary>
    Linear,

    /// <summary>Cubic bezier curve interpolation.</summary>
    Bezier,

    /// <summary>Instant step change at the point time.</summary>
    Step,

    /// <summary>Exponential curve interpolation.</summary>
    Exponential
}


/// <summary>
/// Represents a single automation point with time, value, and curve type.
/// </summary>
public class AutomationDataPoint
{
    /// <summary>Time position in beats or seconds.</summary>
    public double Time { get; set; }

    /// <summary>Parameter value at this point.</summary>
    public float Value { get; set; }

    /// <summary>Interpolation curve type from this point to the next.</summary>
    public CurveType CurveType { get; set; } = CurveType.Linear;

    // Bezier control points (normalized 0-1 relative to segment)
    /// <summary>Bezier control point 1 X coordinate.</summary>
    public float BezierX1 { get; set; } = 0.25f;

    /// <summary>Bezier control point 1 Y coordinate.</summary>
    public float BezierY1 { get; set; } = 0.1f;

    /// <summary>Bezier control point 2 X coordinate.</summary>
    public float BezierX2 { get; set; } = 0.75f;

    /// <summary>Bezier control point 2 Y coordinate.</summary>
    public float BezierY2 { get; set; } = 0.9f;

    /// <summary>
    /// Creates an empty automation point at time 0 with value 0.
    /// </summary>
    public AutomationDataPoint()
    {
    }

    /// <summary>
    /// Creates an automation point with specified time, value, and optional curve type.
    /// </summary>
    /// <param name="time">Time position in beats or seconds.</param>
    /// <param name="value">Parameter value at this point.</param>
    /// <param name="curveType">Interpolation curve type.</param>
    public AutomationDataPoint(double time, float value, CurveType curveType = CurveType.Linear)
    {
        Time = time;
        Value = value;
        CurveType = curveType;
    }

    /// <summary>
    /// Creates a deep copy of this automation point.
    /// </summary>
    public AutomationDataPoint Clone()
    {
        return new AutomationDataPoint
        {
            Time = Time,
            Value = Value,
            CurveType = CurveType,
            BezierX1 = BezierX1,
            BezierY1 = BezierY1,
            BezierX2 = BezierX2,
            BezierY2 = BezierY2
        };
    }
}


/// <summary>
/// Automation lane that stores automation points for a single parameter on a target object.
/// Thread-safe for concurrent read/write operations.
/// </summary>
public class AutomationLane
{
    private readonly object _lock = new();
    private readonly List<AutomationDataPoint> _points = new();
    private PropertyInfo? _cachedProperty;

    /// <summary>Target object whose property will be automated.</summary>
    public object? TargetObject { get; set; }

    /// <summary>Name of the property to automate on the target object.</summary>
    public string PropertyName { get; set; } = "";

    /// <summary>Minimum allowed value for the parameter.</summary>
    public float MinValue { get; set; } = 0f;

    /// <summary>Maximum allowed value for the parameter.</summary>
    public float MaxValue { get; set; } = 1f;

    /// <summary>Whether this lane is enabled for playback.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Whether time values are in beats (true) or seconds (false).</summary>
    public bool UseBeats { get; set; } = true;

    /// <summary>Gets a copy of all automation points, sorted by time.</summary>
    public IReadOnlyList<AutomationDataPoint> Points
    {
        get
        {
            lock (_lock)
            {
                return _points.OrderBy(p => p.Time).ToList().AsReadOnly();
            }
        }
    }

    /// <summary>Gets the number of automation points.</summary>
    public int PointCount
    {
        get
        {
            lock (_lock)
            {
                return _points.Count;
            }
        }
    }

    /// <summary>
    /// Creates an empty automation lane.
    /// </summary>
    public AutomationLane()
    {
    }

    /// <summary>
    /// Creates an automation lane for the specified target and property.
    /// </summary>
    /// <param name="targetObject">Target object to automate.</param>
    /// <param name="propertyName">Property name to automate.</param>
    public AutomationLane(object targetObject, string propertyName)
    {
        TargetObject = targetObject;
        PropertyName = propertyName;
        CacheProperty();
    }

    /// <summary>
    /// Adds an automation point at the specified time with the given value.
    /// If a point already exists at that time (within tolerance), it will be replaced.
    /// </summary>
    /// <param name="time">Time position in beats or seconds.</param>
    /// <param name="value">Parameter value.</param>
    /// <param name="curveType">Interpolation curve type.</param>
    public void AddPoint(double time, float value, CurveType curveType = CurveType.Linear)
    {
        lock (_lock)
        {
            // Remove existing point at same time (within small tolerance)
            _points.RemoveAll(p => Math.Abs(p.Time - time) < 0.0001);

            // Clamp value to valid range
            value = Math.Clamp(value, MinValue, MaxValue);

            _points.Add(new AutomationDataPoint(time, value, curveType));
        }
    }

    /// <summary>
    /// Adds an automation point.
    /// </summary>
    /// <param name="point">The automation point to add.</param>
    public void AddPoint(AutomationDataPoint point)
    {
        lock (_lock)
        {
            _points.RemoveAll(p => Math.Abs(p.Time - point.Time) < 0.0001);
            _points.Add(point.Clone());
        }
    }

    /// <summary>
    /// Removes an automation point at the specified time (within tolerance).
    /// </summary>
    /// <param name="time">Time position to remove.</param>
    /// <param name="tolerance">Time tolerance for matching.</param>
    /// <returns>True if a point was removed, false otherwise.</returns>
    public bool RemovePoint(double time, double tolerance = 0.01)
    {
        lock (_lock)
        {
            int removed = _points.RemoveAll(p => Math.Abs(p.Time - time) < tolerance);
            return removed > 0;
        }
    }

    /// <summary>
    /// Removes all automation points.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _points.Clear();
        }
    }

    /// <summary>
    /// Gets the interpolated value at the specified time.
    /// </summary>
    /// <param name="time">Time position in beats or seconds.</param>
    /// <returns>Interpolated parameter value.</returns>
    public float GetValueAtTime(double time)
    {
        lock (_lock)
        {
            if (_points.Count == 0)
            {
                return MinValue;
            }

            var sorted = _points.OrderBy(p => p.Time).ToList();

            // Before first point
            if (time <= sorted[0].Time)
            {
                return sorted[0].Value;
            }

            // After last point
            if (time >= sorted[^1].Time)
            {
                return sorted[^1].Value;
            }

            // Find surrounding points and interpolate
            for (int i = 0; i < sorted.Count - 1; i++)
            {
                var p1 = sorted[i];
                var p2 = sorted[i + 1];

                if (time >= p1.Time && time < p2.Time)
                {
                    double t = (time - p1.Time) / (p2.Time - p1.Time);
                    return Interpolate(p1, p2, (float)t);
                }
            }

            return sorted[^1].Value;
        }
    }

    /// <summary>
    /// Applies the automation value at the specified time to the target object.
    /// </summary>
    /// <param name="time">Current time position.</param>
    public void Apply(double time)
    {
        if (!Enabled || TargetObject == null || string.IsNullOrEmpty(PropertyName))
        {
            return;
        }

        float value = GetValueAtTime(time);

        try
        {
            CacheProperty();
            if (_cachedProperty != null && _cachedProperty.CanWrite)
            {
                // Convert to the property's type
                object convertedValue = Convert.ChangeType(value, _cachedProperty.PropertyType);
                _cachedProperty.SetValue(TargetObject, convertedValue);
            }
        }
        catch
        {
            // Silently ignore type conversion or access errors
        }
    }

    private void CacheProperty()
    {
        if (_cachedProperty == null && TargetObject != null && !string.IsNullOrEmpty(PropertyName))
        {
            _cachedProperty = TargetObject.GetType().GetProperty(PropertyName);
        }
    }

    private float Interpolate(AutomationDataPoint p1, AutomationDataPoint p2, float t)
    {
        float interpT = p1.CurveType switch
        {
            CurveType.Linear => t,
            CurveType.Step => 0f, // Step holds p1 value until we reach p2
            CurveType.Exponential => ExponentialInterpolation(t),
            CurveType.Bezier => BezierInterpolation(t, p1.BezierX1, p1.BezierY1, p1.BezierX2, p1.BezierY2),
            _ => t
        };

        return p1.Value + (p2.Value - p1.Value) * interpT;
    }

    private static float ExponentialInterpolation(float t)
    {
        // Attempt an exponential ease-in curve
        if (t <= 0) return 0;
        if (t >= 1) return 1;
        return (float)(Math.Pow(2, 10 * (t - 1)));
    }

    private static float BezierInterpolation(float t, float x1, float y1, float x2, float y2)
    {
        // Cubic bezier from (0,0) through (x1,y1), (x2,y2) to (1,1)
        // Approximate Y value for given T using cubic bezier formula
        float t2 = t * t;
        float t3 = t2 * t;
        float mt = 1 - t;
        float mt2 = mt * mt;
        float mt3 = mt2 * mt;

        // Calculate Y coordinate on the bezier curve
        float y = 3 * mt2 * t * y1 + 3 * mt * t2 * y2 + t3;
        return Math.Clamp(y, 0f, 1f);
    }
}


/// <summary>
/// Records automation data by capturing parameter changes over time.
/// Thread-safe for concurrent recording operations.
/// </summary>
public class AutomationRecorder
{
    private readonly object _lock = new();
    private AutomationLane? _recordingLane;
    private object? _targetObject;
    private string _propertyName = "";
    private bool _isRecording;
    private double _recordStartTime;
    private float _lastRecordedValue = float.NaN;
    private double _lastRecordedTime = double.NegativeInfinity;

    /// <summary>Minimum value change required to record a new point.</summary>
    public float ValueChangeThreshold { get; set; } = 0.001f;

    /// <summary>Minimum time between recorded points.</summary>
    public double MinTimeBetweenPoints { get; set; } = 0.01;

    /// <summary>Whether recording is currently active.</summary>
    public bool IsRecording
    {
        get
        {
            lock (_lock)
            {
                return _isRecording;
            }
        }
    }

    /// <summary>
    /// Starts recording automation for the specified target object and property.
    /// </summary>
    /// <param name="target">The object containing the property to record.</param>
    /// <param name="property">The name of the property to record.</param>
    public void StartRecording(object target, string property)
    {
        lock (_lock)
        {
            if (_isRecording)
            {
                StopRecording();
            }

            _targetObject = target;
            _propertyName = property;
            _recordingLane = new AutomationLane(target, property);
            _isRecording = true;
            _recordStartTime = 0;
            _lastRecordedValue = float.NaN;
            _lastRecordedTime = double.NegativeInfinity;
        }
    }

    /// <summary>
    /// Stops the current recording session.
    /// </summary>
    public void StopRecording()
    {
        lock (_lock)
        {
            _isRecording = false;
        }
    }

    /// <summary>
    /// Records a value at the specified time.
    /// Values are only recorded if they differ significantly from the last recorded value.
    /// </summary>
    /// <param name="time">Current time position.</param>
    /// <param name="value">Parameter value to record.</param>
    public void RecordValue(double time, float value)
    {
        lock (_lock)
        {
            if (!_isRecording || _recordingLane == null)
            {
                return;
            }

            // Check if enough time has passed and value has changed enough
            bool timeOk = (time - _lastRecordedTime) >= MinTimeBetweenPoints;
            bool valueChanged = float.IsNaN(_lastRecordedValue) ||
                               Math.Abs(value - _lastRecordedValue) >= ValueChangeThreshold;

            if (timeOk && valueChanged)
            {
                _recordingLane.AddPoint(time, value, CurveType.Linear);
                _lastRecordedValue = value;
                _lastRecordedTime = time;
            }
        }
    }

    /// <summary>
    /// Gets the recorded automation lane. Returns null if no recording has been made.
    /// </summary>
    /// <returns>The recorded automation lane, or null if none exists.</returns>
    public AutomationLane? GetRecordedLane()
    {
        lock (_lock)
        {
            return _recordingLane;
        }
    }

    /// <summary>
    /// Clears the current recording and resets the recorder.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _isRecording = false;
            _recordingLane = null;
            _targetObject = null;
            _propertyName = "";
            _lastRecordedValue = float.NaN;
            _lastRecordedTime = double.NegativeInfinity;
        }
    }
}


/// <summary>
/// Plays back automation lanes, applying parameter values to targets at each time position.
/// Supports synchronization with the Sequencer for beat-accurate automation.
/// Thread-safe for concurrent lane management and playback.
/// </summary>
public class AutomationPlayer
{
    private readonly object _lock = new();
    private readonly List<AutomationLane> _lanes = new();
    private Sequencer? _sequencer;
    private bool _isPlaying;
    private double _currentTime;
    private bool _syncWithSequencer;

    /// <summary>Whether the automation player is currently playing.</summary>
    public bool IsPlaying
    {
        get
        {
            lock (_lock)
            {
                return _isPlaying;
            }
        }
    }

    /// <summary>Current playback time position.</summary>
    public double CurrentTime
    {
        get
        {
            lock (_lock)
            {
                return _currentTime;
            }
        }
    }

    /// <summary>Whether to synchronize time with the attached Sequencer.</summary>
    public bool SyncWithSequencer
    {
        get
        {
            lock (_lock)
            {
                return _syncWithSequencer;
            }
        }
        set
        {
            lock (_lock)
            {
                _syncWithSequencer = value;
            }
        }
    }

    /// <summary>Gets the number of automation lanes.</summary>
    public int LaneCount
    {
        get
        {
            lock (_lock)
            {
                return _lanes.Count;
            }
        }
    }

    /// <summary>Gets a read-only list of all automation lanes.</summary>
    public IReadOnlyList<AutomationLane> Lanes
    {
        get
        {
            lock (_lock)
            {
                return _lanes.ToList().AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Creates an automation player without sequencer synchronization.
    /// </summary>
    public AutomationPlayer()
    {
    }

    /// <summary>
    /// Creates an automation player synchronized with the specified sequencer.
    /// </summary>
    /// <param name="sequencer">The sequencer to synchronize with.</param>
    public AutomationPlayer(Sequencer sequencer)
    {
        SetSequencer(sequencer);
    }

    /// <summary>
    /// Sets the sequencer for synchronization.
    /// </summary>
    /// <param name="sequencer">The sequencer to synchronize with.</param>
    public void SetSequencer(Sequencer sequencer)
    {
        lock (_lock)
        {
            if (_sequencer != null)
            {
                _sequencer.BeatChanged -= OnBeatChanged;
                _sequencer.PlaybackStarted -= OnPlaybackStarted;
                _sequencer.PlaybackStopped -= OnPlaybackStopped;
            }

            _sequencer = sequencer;
            _syncWithSequencer = true;

            if (_sequencer != null)
            {
                _sequencer.BeatChanged += OnBeatChanged;
                _sequencer.PlaybackStarted += OnPlaybackStarted;
                _sequencer.PlaybackStopped += OnPlaybackStopped;
            }
        }
    }

    /// <summary>
    /// Adds an automation lane to the player.
    /// </summary>
    /// <param name="lane">The automation lane to add.</param>
    public void AddLane(AutomationLane lane)
    {
        lock (_lock)
        {
            if (!_lanes.Contains(lane))
            {
                _lanes.Add(lane);
            }
        }
    }

    /// <summary>
    /// Removes an automation lane from the player.
    /// </summary>
    /// <param name="lane">The automation lane to remove.</param>
    /// <returns>True if the lane was removed, false if it was not found.</returns>
    public bool RemoveLane(AutomationLane lane)
    {
        lock (_lock)
        {
            return _lanes.Remove(lane);
        }
    }

    /// <summary>
    /// Removes all automation lanes.
    /// </summary>
    public void ClearLanes()
    {
        lock (_lock)
        {
            _lanes.Clear();
        }
    }

    /// <summary>
    /// Gets an automation lane by index.
    /// </summary>
    /// <param name="index">The index of the lane.</param>
    /// <returns>The automation lane at the specified index.</returns>
    public AutomationLane? GetLane(int index)
    {
        lock (_lock)
        {
            if (index >= 0 && index < _lanes.Count)
            {
                return _lanes[index];
            }
            return null;
        }
    }

    /// <summary>
    /// Starts automation playback.
    /// </summary>
    public void Play()
    {
        lock (_lock)
        {
            _isPlaying = true;
        }
    }

    /// <summary>
    /// Stops automation playback.
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            _isPlaying = false;
        }
    }

    /// <summary>
    /// Resets the playback position to zero.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _currentTime = 0;
        }
    }

    /// <summary>
    /// Processes all automation lanes at the specified time, applying values to their targets.
    /// Call this method each frame or at regular intervals during playback.
    /// </summary>
    /// <param name="currentTime">The current playback time in beats or seconds.</param>
    public void Process(double currentTime)
    {
        lock (_lock)
        {
            if (!_isPlaying)
            {
                return;
            }

            _currentTime = currentTime;

            foreach (var lane in _lanes)
            {
                if (lane.Enabled)
                {
                    lane.Apply(currentTime);
                }
            }
        }
    }

    private void OnBeatChanged(object? sender, BeatChangedEventArgs e)
    {
        if (_syncWithSequencer && _isPlaying)
        {
            Process(e.CurrentBeat);
        }
    }

    private void OnPlaybackStarted(object? sender, PlaybackStateEventArgs e)
    {
        if (_syncWithSequencer)
        {
            Play();
        }
    }

    private void OnPlaybackStopped(object? sender, PlaybackStateEventArgs e)
    {
        if (_syncWithSequencer)
        {
            Stop();
        }
    }
}
