# Multi-Script Scripting

You can split logic into multiple `.csx` files and share objects between them.

## Script Locations

By default scripts are resolved relative to the main script:

- `Scripts/` subfolder (preferred)
- Same folder as the main script

Example structure:

```
MusicEngine/
  test_script.csx
  Scripts/
    instruments.csx
    patterns.csx
```

## Load Another Script

Use `Use("name")` or `File.Use("name")` from any script:

```csharp
await Use("instruments");
await Use("patterns");
```

This loads `Scripts/instruments.csx` and `Scripts/patterns.csx` (or the same directory as the main script).

## Share Objects Between Scripts

Use the shared library to store and retrieve objects:

```csharp
// instruments.csx
var lead = CreateSynth();
File.Lead(lead); // dynamic store

// patterns.csx
var lead = File.Lead() as SimpleSynth;
var pattern = CreatePattern(lead);
pattern.Note(60, 0.0, 0.5, 110);
pattern.Note(60, 0.5, 0.5, 110, slideTo: 67, slideTimeMs: 250);
pattern.NoteMs(72, 750, 250, 110);
// Note(...) auto-uses ms when duration > 8.0 or beat > 32.0
pattern.Note(72, 750, 250, 110);
var seq1 = pattern.Note(60, 1.0, 0.25, 100).Siquenz("00101101");
seq1.Loop = false;
pattern.Play();
```

Typed access is also available:

```csharp
Library.Set("Bass", CreateSynth());
var bass = Library.Get<SimpleSynth>("Bass");
```

## Notes

- Shared objects live until you clear state or restart the engine.
- If a module script changes, it is re-run automatically.
- Errors include file + line numbers when a script file path is known.
