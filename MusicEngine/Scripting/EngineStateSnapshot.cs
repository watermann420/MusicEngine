// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Lightweight engine state snapshots for visualization/debugging.

using System;
using MusicEngine.Core;

namespace MusicEngine.Scripting;

public sealed class EngineStateSnapshot
{
    public bool IsSleeping { get; init; }
    public bool SequencerRunning { get; init; }
    public double CurrentBeat { get; init; }
    public double CurrentTimeSeconds { get; init; }
    public PatternStateSnapshot[] Patterns { get; init; } = Array.Empty<PatternStateSnapshot>();
    public MidiDeviceActivitySnapshot[] MidiDevices { get; init; } = Array.Empty<MidiDeviceActivitySnapshot>();
}

public sealed class PatternStateSnapshot
{
    public Guid Id { get; init; }
    public bool Enabled { get; init; }
    public bool IsLooping { get; init; }
    public double LoopLength { get; init; }
    public double? StartBeat { get; init; }
    public double CurrentBeat { get; init; }
    public double? PositionInLoop { get; init; }
    public bool HasActiveNotes { get; init; }
    public NoteActivitySnapshot[] ActiveNotes { get; init; } = Array.Empty<NoteActivitySnapshot>();
    public NoteActivitySnapshot? LastTriggeredNote { get; init; }
    public double? LastTriggeredBeat { get; init; }
    public DateTime? LastTriggeredUtc { get; init; }
    public string[] SynthTargets { get; init; } = Array.Empty<string>();
}

public sealed class NoteActivitySnapshot
{
    public int Note { get; init; }
    public int Velocity { get; init; }
    public DateTime StartedUtc { get; init; }
}
