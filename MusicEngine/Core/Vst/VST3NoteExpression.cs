// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace MusicEngine.Core.Vst;

/// <summary>
/// Standard VST3 Note Expression types as defined in the VST3 SDK.
/// </summary>
public enum NoteExpressionTypeId : uint
{
    /// <summary>Volume expression (0.0 = silence, 0.5 = normal, 1.0 = +6dB).</summary>
    Volume = 0,

    /// <summary>Pan expression (0.0 = left, 0.5 = center, 1.0 = right).</summary>
    Pan = 1,

    /// <summary>Tuning expression in semitones (0.0 = -120, 0.5 = 0, 1.0 = +120 semitones).</summary>
    Tuning = 2,

    /// <summary>Vibrato depth (0.0 = none, 1.0 = maximum).</summary>
    Vibrato = 3,

    /// <summary>Expression controller (similar to MIDI CC#11).</summary>
    Expression = 4,

    /// <summary>Brightness (similar to MIDI CC#74/timbre).</summary>
    Brightness = 5,

    /// <summary>Pressure/aftertouch per note.</summary>
    Pressure = 6,

    /// <summary>Custom expression starting ID.</summary>
    CustomStart = 100000
}

/// <summary>
/// Represents a single note expression event.
/// </summary>
public class NoteExpressionEvent
{
    /// <summary>Note ID this expression applies to (-1 for all notes on channel).</summary>
    public int NoteId { get; set; } = -1;

    /// <summary>Expression type ID.</summary>
    public uint TypeId { get; set; }

    /// <summary>Normalized value (0.0 to 1.0).</summary>
    public double Value { get; set; }

    /// <summary>Sample offset within the processing block.</summary>
    public int SampleOffset { get; set; }

    /// <summary>PPQ position if applicable.</summary>
    public double PpqPosition { get; set; }

    /// <summary>MIDI channel (0-15).</summary>
    public int Channel { get; set; }
}

/// <summary>
/// Note expression curve point for automation.
/// </summary>
public struct NoteExpressionPoint
{
    /// <summary>Time position in beats.</summary>
    public double Time { get; set; }

    /// <summary>Normalized value (0.0 to 1.0).</summary>
    public double Value { get; set; }

    /// <summary>Curve type for interpolation to next point.</summary>
    public NoteExpressionCurveType CurveType { get; set; }

    /// <summary>Creates a new expression point.</summary>
    public NoteExpressionPoint(double time, double value, NoteExpressionCurveType curveType = NoteExpressionCurveType.Linear)
    {
        Time = time;
        Value = value;
        CurveType = curveType;
    }
}

/// <summary>
/// Curve types for note expression interpolation.
/// </summary>
public enum NoteExpressionCurveType
{
    /// <summary>No interpolation (step).</summary>
    Step,

    /// <summary>Linear interpolation.</summary>
    Linear,

    /// <summary>Exponential curve (fast start).</summary>
    Exponential,

    /// <summary>Logarithmic curve (slow start).</summary>
    Logarithmic,

    /// <summary>S-curve (smooth).</summary>
    SCurve
}

/// <summary>
/// Represents an expression curve for a specific note and expression type.
/// </summary>
public class NoteExpressionCurve
{
    private readonly List<NoteExpressionPoint> _points = new();
    private readonly object _lock = new();

    /// <summary>Note ID this curve applies to.</summary>
    public int NoteId { get; }

    /// <summary>Expression type ID.</summary>
    public uint TypeId { get; }

    /// <summary>Gets the curve points.</summary>
    public IReadOnlyList<NoteExpressionPoint> Points
    {
        get
        {
            lock (_lock)
            {
                return _points.AsReadOnly();
            }
        }
    }

    /// <summary>Creates a new expression curve.</summary>
    public NoteExpressionCurve(int noteId, uint typeId)
    {
        NoteId = noteId;
        TypeId = typeId;
    }

    /// <summary>Adds a point to the curve.</summary>
    public void AddPoint(double time, double value, NoteExpressionCurveType curveType = NoteExpressionCurveType.Linear)
    {
        lock (_lock)
        {
            var point = new NoteExpressionPoint(time, Math.Clamp(value, 0.0, 1.0), curveType);

            // Insert in sorted order
            int index = _points.FindIndex(p => p.Time > time);
            if (index < 0)
            {
                _points.Add(point);
            }
            else
            {
                _points.Insert(index, point);
            }
        }
    }

    /// <summary>Removes a point at the specified index.</summary>
    public void RemovePoint(int index)
    {
        lock (_lock)
        {
            if (index >= 0 && index < _points.Count)
            {
                _points.RemoveAt(index);
            }
        }
    }

    /// <summary>Clears all points.</summary>
    public void Clear()
    {
        lock (_lock)
        {
            _points.Clear();
        }
    }

    /// <summary>Gets the interpolated value at a specific time.</summary>
    public double GetValueAtTime(double time)
    {
        lock (_lock)
        {
            if (_points.Count == 0)
                return 0.5; // Default center value

            if (_points.Count == 1)
                return _points[0].Value;

            // Find surrounding points
            int afterIndex = _points.FindIndex(p => p.Time > time);

            if (afterIndex < 0)
            {
                // Time is after all points
                return _points[^1].Value;
            }

            if (afterIndex == 0)
            {
                // Time is before all points
                return _points[0].Value;
            }

            var before = _points[afterIndex - 1];
            var after = _points[afterIndex];

            // Interpolate
            double t = (time - before.Time) / (after.Time - before.Time);
            return Interpolate(before.Value, after.Value, t, before.CurveType);
        }
    }

    private static double Interpolate(double from, double to, double t, NoteExpressionCurveType curveType)
    {
        t = Math.Clamp(t, 0.0, 1.0);

        switch (curveType)
        {
            case NoteExpressionCurveType.Step:
                return from;

            case NoteExpressionCurveType.Linear:
                return from + (to - from) * t;

            case NoteExpressionCurveType.Exponential:
                return from + (to - from) * (1.0 - Math.Pow(1.0 - t, 2.0));

            case NoteExpressionCurveType.Logarithmic:
                return from + (to - from) * Math.Pow(t, 2.0);

            case NoteExpressionCurveType.SCurve:
                // Hermite S-curve
                double s = t * t * (3.0 - 2.0 * t);
                return from + (to - from) * s;

            default:
                return from + (to - from) * t;
        }
    }
}

/// <summary>
/// Tracks active note states for note expression management.
/// </summary>
public class NoteExpressionNoteState
{
    /// <summary>Note ID.</summary>
    public int NoteId { get; set; }

    /// <summary>MIDI note number.</summary>
    public int NoteNumber { get; set; }

    /// <summary>MIDI channel.</summary>
    public int Channel { get; set; }

    /// <summary>Current expression values by type ID.</summary>
    public Dictionary<uint, double> ExpressionValues { get; } = new();

    /// <summary>Note start time in beats.</summary>
    public double StartTime { get; set; }

    /// <summary>Whether the note is still active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Gets an expression value, returning default if not set.</summary>
    public double GetExpression(uint typeId, double defaultValue = 0.5)
    {
        return ExpressionValues.TryGetValue(typeId, out var value) ? value : defaultValue;
    }

    /// <summary>Sets an expression value.</summary>
    public void SetExpression(uint typeId, double value)
    {
        ExpressionValues[typeId] = Math.Clamp(value, 0.0, 1.0);
    }
}

/// <summary>
/// Manages VST3 Note Expression for per-note modulation.
/// Provides MPE-like functionality for VST3 plugins that support note expression.
/// </summary>
public class VST3NoteExpressionManager
{
    private readonly ConcurrentDictionary<int, NoteExpressionNoteState> _activeNotes = new();
    private readonly ConcurrentDictionary<(int noteId, uint typeId), NoteExpressionCurve> _curves = new();
    private readonly ConcurrentQueue<NoteExpressionEvent> _pendingEvents = new();
    private int _nextNoteId = 0;
    private readonly object _lock = new();

    /// <summary>Default tuning range in semitones (for value normalization).</summary>
    public double TuningRange { get; set; } = 120.0;

    /// <summary>Event raised when a note expression value changes.</summary>
    public event EventHandler<NoteExpressionEvent>? ExpressionChanged;

    /// <summary>
    /// Allocates a new note ID for an incoming note.
    /// </summary>
    /// <param name="noteNumber">MIDI note number.</param>
    /// <param name="channel">MIDI channel.</param>
    /// <param name="startTime">Start time in beats.</param>
    /// <returns>The allocated note ID.</returns>
    public int AllocateNoteId(int noteNumber, int channel, double startTime)
    {
        lock (_lock)
        {
            int noteId = _nextNoteId++;

            var state = new NoteExpressionNoteState
            {
                NoteId = noteId,
                NoteNumber = noteNumber,
                Channel = channel,
                StartTime = startTime
            };

            // Set default expression values
            state.SetExpression((uint)NoteExpressionTypeId.Volume, 0.5);
            state.SetExpression((uint)NoteExpressionTypeId.Pan, 0.5);
            state.SetExpression((uint)NoteExpressionTypeId.Tuning, 0.5);
            state.SetExpression((uint)NoteExpressionTypeId.Pressure, 0.0);

            _activeNotes[noteId] = state;
            return noteId;
        }
    }

    /// <summary>
    /// Releases a note ID when the note ends.
    /// </summary>
    /// <param name="noteId">The note ID to release.</param>
    public void ReleaseNoteId(int noteId)
    {
        if (_activeNotes.TryGetValue(noteId, out var state))
        {
            state.IsActive = false;
        }

        // Note: We don't immediately remove to allow tail processing
        // Could implement cleanup of old notes periodically
    }

    /// <summary>
    /// Sends a note expression value for a specific note.
    /// </summary>
    /// <param name="noteId">Target note ID (-1 for all notes on channel).</param>
    /// <param name="typeId">Expression type ID.</param>
    /// <param name="value">Normalized value (0.0 to 1.0).</param>
    /// <param name="sampleOffset">Sample offset in current block.</param>
    public void SendExpression(int noteId, uint typeId, double value, int sampleOffset = 0)
    {
        value = Math.Clamp(value, 0.0, 1.0);

        var evt = new NoteExpressionEvent
        {
            NoteId = noteId,
            TypeId = typeId,
            Value = value,
            SampleOffset = sampleOffset
        };

        _pendingEvents.Enqueue(evt);

        // Update state
        if (noteId >= 0 && _activeNotes.TryGetValue(noteId, out var state))
        {
            state.SetExpression(typeId, value);
        }

        ExpressionChanged?.Invoke(this, evt);
    }

    /// <summary>
    /// Sends tuning expression in semitones.
    /// </summary>
    /// <param name="noteId">Target note ID.</param>
    /// <param name="semitones">Tuning offset in semitones.</param>
    /// <param name="sampleOffset">Sample offset in current block.</param>
    public void SendTuning(int noteId, double semitones, int sampleOffset = 0)
    {
        // Convert semitones to normalized value
        // Range is -TuningRange/2 to +TuningRange/2 mapped to 0.0-1.0
        double normalized = (semitones / TuningRange) + 0.5;
        SendExpression(noteId, (uint)NoteExpressionTypeId.Tuning, normalized, sampleOffset);
    }

    /// <summary>
    /// Sends volume expression.
    /// </summary>
    /// <param name="noteId">Target note ID.</param>
    /// <param name="volume">Volume (0.0 = silence, 0.5 = unity, 1.0 = +6dB).</param>
    /// <param name="sampleOffset">Sample offset in current block.</param>
    public void SendVolume(int noteId, double volume, int sampleOffset = 0)
    {
        SendExpression(noteId, (uint)NoteExpressionTypeId.Volume, volume, sampleOffset);
    }

    /// <summary>
    /// Sends pan expression.
    /// </summary>
    /// <param name="noteId">Target note ID.</param>
    /// <param name="pan">Pan position (0.0 = left, 0.5 = center, 1.0 = right).</param>
    /// <param name="sampleOffset">Sample offset in current block.</param>
    public void SendPan(int noteId, double pan, int sampleOffset = 0)
    {
        SendExpression(noteId, (uint)NoteExpressionTypeId.Pan, pan, sampleOffset);
    }

    /// <summary>
    /// Sends pressure/aftertouch expression.
    /// </summary>
    /// <param name="noteId">Target note ID.</param>
    /// <param name="pressure">Pressure value (0.0 to 1.0).</param>
    /// <param name="sampleOffset">Sample offset in current block.</param>
    public void SendPressure(int noteId, double pressure, int sampleOffset = 0)
    {
        SendExpression(noteId, (uint)NoteExpressionTypeId.Pressure, pressure, sampleOffset);
    }

    /// <summary>
    /// Sends brightness/timbre expression.
    /// </summary>
    /// <param name="noteId">Target note ID.</param>
    /// <param name="brightness">Brightness value (0.0 to 1.0).</param>
    /// <param name="sampleOffset">Sample offset in current block.</param>
    public void SendBrightness(int noteId, double brightness, int sampleOffset = 0)
    {
        SendExpression(noteId, (uint)NoteExpressionTypeId.Brightness, brightness, sampleOffset);
    }

    /// <summary>
    /// Gets or creates an expression curve for a note.
    /// </summary>
    /// <param name="noteId">Note ID.</param>
    /// <param name="typeId">Expression type ID.</param>
    /// <returns>The expression curve.</returns>
    public NoteExpressionCurve GetOrCreateCurve(int noteId, uint typeId)
    {
        var key = (noteId, typeId);
        return _curves.GetOrAdd(key, _ => new NoteExpressionCurve(noteId, typeId));
    }

    /// <summary>
    /// Removes an expression curve.
    /// </summary>
    /// <param name="noteId">Note ID.</param>
    /// <param name="typeId">Expression type ID.</param>
    public void RemoveCurve(int noteId, uint typeId)
    {
        _curves.TryRemove((noteId, typeId), out _);
    }

    /// <summary>
    /// Processes expression curves at a given time, generating events.
    /// </summary>
    /// <param name="time">Current time in beats.</param>
    /// <param name="sampleOffset">Sample offset for generated events.</param>
    public void ProcessCurves(double time, int sampleOffset = 0)
    {
        foreach (var kvp in _curves)
        {
            var curve = kvp.Value;
            double value = curve.GetValueAtTime(time);

            if (_activeNotes.TryGetValue(curve.NoteId, out var state))
            {
                double currentValue = state.GetExpression(curve.TypeId);

                // Only send if value has changed significantly
                if (Math.Abs(value - currentValue) > 0.001)
                {
                    SendExpression(curve.NoteId, curve.TypeId, value, sampleOffset);
                }
            }
        }
    }

    /// <summary>
    /// Gets pending expression events for the current processing block.
    /// </summary>
    /// <returns>List of pending events.</returns>
    public List<NoteExpressionEvent> GetPendingEvents()
    {
        var events = new List<NoteExpressionEvent>();
        while (_pendingEvents.TryDequeue(out var evt))
        {
            events.Add(evt);
        }
        return events;
    }

    /// <summary>
    /// Gets the current state of an active note.
    /// </summary>
    /// <param name="noteId">Note ID.</param>
    /// <returns>Note state or null if not found.</returns>
    public NoteExpressionNoteState? GetNoteState(int noteId)
    {
        return _activeNotes.TryGetValue(noteId, out var state) ? state : null;
    }

    /// <summary>
    /// Gets all active notes.
    /// </summary>
    /// <returns>List of active note states.</returns>
    public List<NoteExpressionNoteState> GetActiveNotes()
    {
        var result = new List<NoteExpressionNoteState>();
        foreach (var state in _activeNotes.Values)
        {
            if (state.IsActive)
            {
                result.Add(state);
            }
        }
        return result;
    }

    /// <summary>
    /// Clears all note states and pending events.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _activeNotes.Clear();
            _curves.Clear();
            while (_pendingEvents.TryDequeue(out _)) { }
            _nextNoteId = 0;
        }
    }

    /// <summary>
    /// Converts a normalized tuning value to semitones.
    /// </summary>
    /// <param name="normalizedValue">Normalized value (0.0 to 1.0).</param>
    /// <returns>Tuning in semitones.</returns>
    public double NormalizedToSemitones(double normalizedValue)
    {
        return (normalizedValue - 0.5) * TuningRange;
    }

    /// <summary>
    /// Converts semitones to a normalized tuning value.
    /// </summary>
    /// <param name="semitones">Tuning in semitones.</param>
    /// <returns>Normalized value (0.0 to 1.0).</returns>
    public double SemitonesToNormalized(double semitones)
    {
        return (semitones / TuningRange) + 0.5;
    }
}
