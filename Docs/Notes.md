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

## Millisecond Timing

```csharp
pattern.NoteMs(60, 0, 500, 100);
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
