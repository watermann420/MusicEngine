# Notes

Notes are the core event type used by patterns and sequences.

## Beat Timing

```csharp
pattern.Note(60, 0.0, 0.5, 100);
```

- `note`: MIDI note number (0-127)
- `beat`: start position in beats
- `duration`: length in beats
- `velocity`: MIDI velocity (1-127)
- `slideTo`: optional slide target note
- `slideTimeMs`: optional slide duration in ms

You can also use note names:

```csharp
pattern.Note(C4, 0.0, 0.5, 100);
pattern.Note(Db4, 0.5, 0.5, 100);
```

## Friendly Named Arguments (No Colons)

You can use spaced names in any order. `speed` maps to `velocity`.

```csharp
pattern.Note(Note 60, beat 0.0, duration 0.5, speed 100);
pattern.Note(note 60, duration 1.0, velocity 90);
pattern.Note(note 60, beat 0.0, duration 0.5, slideTo 67, slideTimeMs 500);
```

More aliases work too:

```csharp
pattern.Note(N60, start 0.0, len 0.5, vel 100);
pattern.Note(Note60, beat 0.0, length 0.5, speed 100);
pattern.Note(C4, beat 0.0, duration 0.5, speed 100);
pattern.Note(NC4, beat 0.0, duration 0.5, speed 100);
```

## Millisecond Timing

```csharp
pattern.NoteMs(60, 0, 500, 100);
```

Friendly names also work with `NoteMs`:

```csharp
pattern.NoteMs(Note 60, time 0, duration 500, speed 100);
```

## Auto ms Heuristic

`Note(...)` will switch to milliseconds when:

- `duration > 8.0`, or
- `beat > 32.0`

```csharp
pattern.Note(72, 1500, 250, 100); // treated as ms
```

## Slides

```csharp
pattern.Note(60, 0.0, 1.0, 100, slideTo: 67, slideTimeMs: 500);
```

## Direct Notes (Outside Patterns)

```csharp
var note60 = Note(60).to.vital;
note60.On(100);
note60.Off();

// Or one-shot play (duration in ms)
await Note(60).To(vital).Play(250, 100);

// Multiple targets + looping
var noteLayer = Note(60).To(vital, piano);
noteLayer.Loop.Speed(20).Gate(120);

// Chain targets by name
Note(60).to.vital.to.piano.Trigger(100);
```

Defaults:
- `Trigger()` / `On()` use velocity 100.
- `Play()` uses 250ms and velocity 100.
- `Loop.Speed()` uses BPM (default 120) and `Gate()` uses 120ms.

You can also use friendly names on direct notes:

```csharp
Note(60).to.vital.Trigger(speed 90);
await Note(60).to.vital.Play(duration 400, speed 90);
```
