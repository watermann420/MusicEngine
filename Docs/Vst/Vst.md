# VST3 Instruments

VST3 plugins are loaded through a native host and exposed as `ISynth`.

## Setup

- Default scan paths are standard Windows VST3 folders.
- Optional: set `MUSICENGINE_VST3_PATHS` (semicolon-separated) to scan custom locations.

## Syntax Example (Script)

```csharp
var vital = CreateVst("Vital");
vital.SetParameterNormalized("Cutoff", 0.5f);

midi.device(0).to(vital);
midi.device(0).pitchbend().to(value => vital.PitchBend(value * 2f - 1f));

var pattern = CreatePattern(vital);
pattern.LoopLength = 4.0;
pattern.Note(60, 0.0, 0.5, 110);
pattern.Note(64, 0.5, 0.5, 110);
pattern.Note(67, 1.0, 1.0, 110);
pattern.Play();
```

## Editor and Parameters

```csharp
vst.OpenEditor();
var drive = vst.Param("Drive", 0f, 1f);
drive(0.7f);
```

## Save / Load State

```csharp
var vital = CreateVst("Vital");
vital.State(); // on /S or exit, writes base64 into the ()
```

Notes:
- The inline `State()` call is updated on refresh or exit, so you can copy/share the script.
- States are stored per script under `.musicengine/states/<script>/<name>.state`.
- Missing VSTs warn and stay silent instead of crashing.

Manual overrides still work:
```csharp
vital.LoadState("States/vital.state");
vital.SaveState("States/vital.state");
```

## Disable Auto Save

```csharp
var vital = CreateVst("Vital");
vital.NoSave();
```

Copyright 2026 watermann429 and contributers.
