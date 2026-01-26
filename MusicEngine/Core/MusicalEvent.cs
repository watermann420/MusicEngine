// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using System;

namespace MusicEngine.Core;

/// <summary>
/// Represents a unique identifier for a musical event that remains stable across cycles.
/// </summary>
public readonly struct EventId : IEquatable<EventId>
{
    public Guid Id { get; }
    public int PatternIndex { get; }
    public int NoteIndex { get; }

    public EventId(int patternIndex, int noteIndex)
    {
        Id = Guid.NewGuid();
        PatternIndex = patternIndex;
        NoteIndex = noteIndex;
    }

    public EventId(Guid id, int patternIndex, int noteIndex)
    {
        Id = id;
        PatternIndex = patternIndex;
        NoteIndex = noteIndex;
    }

    public bool Equals(EventId other) => Id.Equals(other.Id);
    public override bool Equals(object? obj) => obj is EventId other && Equals(other);
    public override int GetHashCode() => Id.GetHashCode();
    public static bool operator ==(EventId left, EventId right) => left.Equals(right);
    public static bool operator !=(EventId left, EventId right) => !left.Equals(right);
    public override string ToString() => $"Event({PatternIndex}:{NoteIndex})";
}

/// <summary>
/// Represents the source location in code that generated an event.
/// </summary>
public class CodeSourceInfo
{
    /// <summary>Start character index in the source code (0-based).</summary>
    public int StartIndex { get; set; }

    /// <summary>End character index in the source code (exclusive).</summary>
    public int EndIndex { get; set; }

    /// <summary>The line number where this code starts (1-based).</summary>
    public int StartLine { get; set; }

    /// <summary>The column where this code starts (1-based).</summary>
    public int StartColumn { get; set; }

    /// <summary>The line number where this code ends (1-based).</summary>
    public int EndLine { get; set; }

    /// <summary>The column where this code ends (1-based).</summary>
    public int EndColumn { get; set; }

    /// <summary>The actual code text that generated this event.</summary>
    public string? SourceText { get; set; }

    /// <summary>Name of the instrument/synth associated with this code.</summary>
    public string? InstrumentName { get; set; }

    /// <summary>Creates a CodeSourceInfo from character indices.</summary>
    public static CodeSourceInfo FromIndices(int startIndex, int endIndex, string? sourceText = null)
    {
        return new CodeSourceInfo
        {
            StartIndex = startIndex,
            EndIndex = endIndex,
            SourceText = sourceText
        };
    }

    /// <summary>Creates a CodeSourceInfo from line/column positions.</summary>
    public static CodeSourceInfo FromPosition(int startLine, int startColumn, int endLine, int endColumn)
    {
        return new CodeSourceInfo
        {
            StartLine = startLine,
            StartColumn = startColumn,
            EndLine = endLine,
            EndColumn = endColumn
        };
    }

    public override string ToString() =>
        $"Source({StartLine}:{StartColumn}-{EndLine}:{EndColumn})";
}

/// <summary>
/// Represents a musical event with full timing and source information for visualization.
/// </summary>
public class MusicalEvent
{
    /// <summary>Stable identifier for this event.</summary>
    public EventId Id { get; set; }

    /// <summary>The pattern that contains this event.</summary>
    public Pattern? SourcePattern { get; set; }

    /// <summary>The note event definition.</summary>
    public NoteEvent? NoteEvent { get; set; }

    /// <summary>MIDI note number (0-127).</summary>
    public int Note { get; set; }

    /// <summary>Note name (e.g., "C4", "F#3").</summary>
    public string NoteName { get; set; } = "";

    /// <summary>Velocity (0-127).</summary>
    public int Velocity { get; set; }

    /// <summary>Duration in beats.</summary>
    public double Duration { get; set; }

    /// <summary>Beat position within the pattern cycle (0 to LoopLength).</summary>
    public double CyclePosition { get; set; }

    /// <summary>Absolute beat position in the global timeline.</summary>
    public double AbsoluteBeat { get; set; }

    /// <summary>Which cycle iteration this event is in (0-based).</summary>
    public int CycleNumber { get; set; }

    /// <summary>Total loop length of the pattern in beats.</summary>
    public double LoopLength { get; set; }

    /// <summary>Normalized position in cycle (0.0 to 1.0).</summary>
    public double NormalizedPosition => LoopLength > 0 ? CyclePosition / LoopLength : 0;

    /// <summary>Name of the instrument playing this event.</summary>
    public string InstrumentName { get; set; } = "";

    /// <summary>Code source information for editor highlighting.</summary>
    public CodeSourceInfo? SourceInfo { get; set; }

    /// <summary>Timestamp when this event was triggered.</summary>
    public DateTime TriggeredAt { get; set; }

    /// <summary>Timestamp when this event should end.</summary>
    public DateTime EndsAt { get; set; }

    /// <summary>Whether this is a NoteOn (true) or NoteOff (false) event.</summary>
    public bool IsNoteOn { get; set; }

    /// <summary>Current BPM at the time of the event.</summary>
    public double Bpm { get; set; }

    /// <summary>Checks if this event is currently playing.</summary>
    public bool IsPlaying => IsNoteOn && DateTime.Now < EndsAt;

    /// <summary>Progress through this note (0.0 to 1.0).</summary>
    public double PlayProgress
    {
        get
        {
            if (!IsNoteOn) return 1.0;
            var total = (EndsAt - TriggeredAt).TotalMilliseconds;
            var elapsed = (DateTime.Now - TriggeredAt).TotalMilliseconds;
            return Math.Clamp(elapsed / total, 0.0, 1.0);
        }
    }

    /// <summary>Converts MIDI note number to note name.</summary>
    public static string GetNoteName(int midiNote)
    {
        string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        int octave = (midiNote / 12) - 1;
        int noteIndex = midiNote % 12;
        return $"{noteNames[noteIndex]}{octave}";
    }

    public override string ToString() =>
        $"{NoteName} v{Velocity} @{CyclePosition:F2}/{LoopLength:F1} [{InstrumentName}]";
}

/// <summary>
/// Event arguments for when a musical event is triggered.
/// </summary>
public class MusicalEventArgs : EventArgs
{
    public MusicalEvent Event { get; }
    public MusicalEventArgs(MusicalEvent musicalEvent) => Event = musicalEvent;
}

/// <summary>
/// Event arguments for beat changes.
/// </summary>
public class BeatChangedEventArgs : EventArgs
{
    public double CurrentBeat { get; }
    public double CyclePosition { get; }
    public double LoopLength { get; }
    public double NormalizedPosition { get; }
    public int CycleNumber { get; }
    public double Bpm { get; }

    public BeatChangedEventArgs(double currentBeat, double cyclePosition, double loopLength, double bpm)
    {
        CurrentBeat = currentBeat;
        CyclePosition = cyclePosition;
        LoopLength = loopLength;
        NormalizedPosition = loopLength > 0 ? cyclePosition / loopLength : 0;
        CycleNumber = loopLength > 0 ? (int)(currentBeat / loopLength) : 0;
        Bpm = bpm;
    }
}

/// <summary>
/// Event arguments for playback state changes.
/// </summary>
public class PlaybackStateEventArgs : EventArgs
{
    public bool IsPlaying { get; }
    public double CurrentBeat { get; }
    public double Bpm { get; }

    public PlaybackStateEventArgs(bool isPlaying, double currentBeat, double bpm)
    {
        IsPlaying = isPlaying;
        CurrentBeat = currentBeat;
        Bpm = bpm;
    }
}

/// <summary>
/// Event arguments for parameter changes (for live slider updates).
/// </summary>
public class ParameterChangedEventArgs : EventArgs
{
    public string ParameterName { get; }
    public double OldValue { get; }
    public double NewValue { get; }
    public CodeSourceInfo? SourceInfo { get; }

    public ParameterChangedEventArgs(string parameterName, double oldValue, double newValue, CodeSourceInfo? sourceInfo = null)
    {
        ParameterName = parameterName;
        OldValue = oldValue;
        NewValue = newValue;
        SourceInfo = sourceInfo;
    }
}
