# Getting Started

This short guide shows where the default script lives and the basic workflow to start making sound.

## 1) Script Location

The engine scans the default project folder:

```
Test Project/
```

Any `.cs` or `.csx` file can be a main script if it calls `File.Main();` or `File(Main, "Name");`.
If you want a starter file, create `Test Project/test_script.cs` and add `File.Main();` at the top.

To use a different project folder, set `MUSICENGINE_PROJECT_DIR`.
The engine will look for `Test Project/` (and the legacy `Scripts/`) inside that folder.

## 2) Basic Workflow

1. Start the engine (debug or release).
2. Create or edit a main script in `Test Project/` (add `File.Main();`).
3. In the console, press `S` to refresh and run all main scripts.
4. Use `exit` to quit and persist VST state.
5. Open VST editors by variable name: `open vital` or just `vital`.

## 3) First Sound (Synth)

```csharp
var synth = CreateSynth();
midi.Device(0).to(synth);

// quick pattern
var pattern = CreatePattern(synth);
pattern.Note(60, 0.0, 0.5, 110);
pattern.Note(64, 0.5, 0.5, 110);
pattern.Note(67, 1.0, 1.0, 110);
pattern.Play();
```

## 4) VST Example

```csharp
var vital = CreateVst("Vital");
var ch1 = Audio.CreateChannel(1);
ch1.Route(vital);

Midi.Device(0).to(vital);
Midi.Device(0).Pitchbend().to((Action<float>)(val => vital.PitchBend(val * 2f - 1f)));
```

## 5) MIDI Controls

```csharp
var synth = CreateSynth();
midi.Device(0).to(synth);
midi.Device(0).CC(1).to((Action<float>)(val => synth.ModWheel = val));
```

## 6) Notes In Milliseconds

```csharp
var pattern = CreatePattern(CreateSynth());
pattern.NoteMs(60, 0, 500, 100);
pattern.Note(72, 1500, 250, 100); // auto-ms when duration > 8.0 or beat > 32.0
pattern.Play();
```
