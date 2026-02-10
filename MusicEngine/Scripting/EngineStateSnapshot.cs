// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Lightweight engine state snapshots for visualization/debugging.

using System;
using MusicEngine.Core;

namespace MusicEngine.Scripting;

/// <summary>
/// Lightweight engine state snapshot for visualization/debugging.
/// </summary>
public sealed class EngineStateSnapshot
{
    /// <summary>
    /// True when the engine is sleeping.
    /// </summary>
    public bool IsSleeping { get; init; }
    /// <summary>
    /// True when the sequencer is running.
    /// </summary>
    public bool SequencerRunning { get; init; }
    /// <summary>
    /// Current sequencer beat.
    /// </summary>
    public double CurrentBeat { get; init; }
    /// <summary>
    /// Current sequencer time in seconds.
    /// </summary>
    public double CurrentTimeSeconds { get; init; }
    /// <summary>
    /// Pattern snapshots.
    /// </summary>
    public PatternStateSnapshot[] Patterns { get; init; } = Array.Empty<PatternStateSnapshot>();
    /// <summary>
    /// MIDI device activity snapshots.
    /// </summary>
    public MidiDeviceActivitySnapshot[] MidiDevices { get; init; } = Array.Empty<MidiDeviceActivitySnapshot>();
}

/// <summary>
/// Snapshot of a pattern state.
/// </summary>
public sealed class PatternStateSnapshot
{
    /// <summary>
    /// Pattern identifier.
    /// </summary>
    public Guid Id { get; init; }
    /// <summary>
    /// Whether the pattern is enabled.
    /// </summary>
    public bool Enabled { get; init; }
    /// <summary>
    /// Whether the pattern is looping.
    /// </summary>
    public bool IsLooping { get; init; }
    /// <summary>
    /// Pattern loop length in beats.
    /// </summary>
    public double LoopLength { get; init; }
    /// <summary>
    /// Optional start beat.
    /// </summary>
    public double? StartBeat { get; init; }
    /// <summary>
    /// Current beat position.
    /// </summary>
    public double CurrentBeat { get; init; }
    /// <summary>
    /// Position within the loop, if applicable.
    /// </summary>
    public double? PositionInLoop { get; init; }
    /// <summary>
    /// True when there are active notes.
    /// </summary>
    public bool HasActiveNotes { get; init; }
    /// <summary>
    /// Active note snapshots.
    /// </summary>
    public NoteActivitySnapshot[] ActiveNotes { get; init; } = Array.Empty<NoteActivitySnapshot>();
    /// <summary>
    /// Last triggered note, if any.
    /// </summary>
    public NoteActivitySnapshot? LastTriggeredNote { get; init; }
    /// <summary>
    /// Beat position of the last triggered note.
    /// </summary>
    public double? LastTriggeredBeat { get; init; }
    /// <summary>
    /// UTC timestamp of the last triggered note.
    /// </summary>
    public DateTime? LastTriggeredUtc { get; init; }
    /// <summary>
    /// Names of synth targets.
    /// </summary>
    public string[] SynthTargets { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Snapshot of a single note activity.
/// </summary>
public sealed class NoteActivitySnapshot
{
    /// <summary>
    /// MIDI note number.
    /// </summary>
    public int Note { get; init; }
    /// <summary>
    /// MIDI velocity.
    /// </summary>
    public int Velocity { get; init; }
    /// <summary>
    /// UTC timestamp when the note started.
    /// </summary>
    public DateTime StartedUtc { get; init; }
}
