# Sequencer (Step Sequence)

Sequences turn a single note into a 0/1 step grid. The step duration comes from the note duration.

## Create a Sequence

```csharp
var pattern = CreatePattern(CreateSynth());
var seq = pattern.Note(60, 0.0, 0.25, 100).Siquenz("00101101");
```

## Options

```csharp
seq.Loop = true;   // keep repeating the step pattern
seq.Enabled = true;
```

## How It Plays

- `1` triggers the note.
- `0` is a rest.
- The sequence starts when the note would trigger.
- If `Loop = false`, it plays once.
- If `Loop = true`, it repeats until the pattern stops.
