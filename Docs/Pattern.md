# Pattern

Patterns are time containers for note events and sequences. Each pattern targets one or more synths.

## Create

```csharp
var synth = CreateSynth();
var pattern = CreatePattern(synth);
pattern.LoopLength = 4.0;
```

Layer multiple synths:

```csharp
var synth = CreateSynth();
var piano = CreateGeneralMidi();
var pattern = CreatePattern(synth, piano);
```

Priority / fallback:

```csharp
var synth = CreateSynth();
var piano = CreateGeneralMidi();
var pattern = CreatePattern(synth, < piano);
pattern.Active(synth, false); // fallback to piano
```

Higher priority:

```csharp
var pattern = CreatePattern(synth, > piano);
```

## Notes (Beats)

```csharp
pattern.Note(60, 0.0, 0.5, 100);
pattern.Note(64, 0.5, 0.5, 100);
pattern.Note(67, 1.0, 1.0, 100);
```

You can also use note names:

```csharp
pattern.Note(C4, 0.0, 0.5, 100);
pattern.Note(Db4, 0.5, 0.5, 100);
pattern.Note(G4, 1.0, 1.0, 100);
```

Friendly named arguments (no colons) are supported too:

```csharp
pattern.Note(Note 60, beat 0.0, duration 0.5, speed 100);
pattern.Note(note 64, duration 0.5, velocity 90);
```

## Notes (Milliseconds)

```csharp
pattern.NoteMs(60, 0, 500, 100);
pattern.NoteMs(64, 500, 500, 100);
pattern.NoteMs(67, 1000, 1000, 100);
```

`Note(...)` automatically switches to milliseconds if `duration > 8.0` or `beat > 32.0`:

```csharp
pattern.Note(72, 1500, 250, 100);
```

## Slides

```csharp
pattern.Note(60, 0.0, 1.0, 100, slideTo: 67, slideTimeMs: 500);
```

## Sequenced Notes

```csharp
var seq1 = pattern.Note(60, 0.0, 0.25, 100).Siquenz("0010101001101110");
seq1.Loop = true;
```

## Play / Stop

```csharp
pattern.Play();
pattern.Stop();
```
