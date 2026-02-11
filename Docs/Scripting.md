# Multi-Script Scripting

You can split logic into multiple `.cs` or `.csx` files and share objects between them.

## Script Locations

By default scripts are resolved relative to the main script:

- `Test Project/` subfolder (preferred)
- `Scripts/` subfolder (legacy)
- Same folder as the main script

You can point to a different project folder by setting `MUSICENGINE_PROJECT_DIR`.
The engine looks for `Test Project/` first, then `Scripts/`.
You can also pass `--project-dir <path>` to set the same override.

Example structure:

```
MusicEngine/
  test_script.cs
  Test Project/
    instruments.cs
    patterns.cs
```

## Load Another Script

Use `Use("name")`, `File.Use("name")`, or the shorthand `include name;` from any script:

```csharp
await Use("instruments");
await Use("patterns");
include instruments; // forces a reload each refresh
```

This loads `Test Project/instruments.cs` and `Test Project/patterns.cs` (or the same directory as the main script).
`.csx` is also supported.

## Share Objects Between Scripts

Use the shared library to store and retrieve objects:

```csharp
// instruments.cs
var lead = CreateSynth();
File.Lead(lead); // dynamic store

// patterns.cs
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

## Multi-File Scripts (File / Master)

```csharp
// MainScript.cs
File.Main(); // register main script (namespace = file name)
include instruments;
var inst = Include.Instruments; // Include is an alias for File
midi.Device(0).to(inst.Synth1);

// Instruments.cs
var file = File(); // uses file name (Instruments) and exposes File.Instruments
file.Synth1 = CreateSynth();
```

## Wavetable Usage

```csharp
var synth = CreateSynth();
synth.Osc1Wavetable = Wavetable.Saw(2048);

var wt = Wavetable.FromFile("my_wave.wav", maxSamples: 2048);
var osc = synth.Oscillator();
osc.Wavetable = wt;
osc.Enabled = true;
```

## Speech Instrument (TTS)

```csharp
var speech = CreateSpeech();
speech.Phrase("hello", note: 60);
midi.Device(0).to(speech);
```

Note: `CreateSpeech()` loads `MusicEngine.Library.dll` (optional content project). It uses Windows TTS when available and falls back to offline synthesis.

## Sample Folder Helper (GetSamples)

```csharp
var samples = GetSamples("Samples/Drums");
var kick = samples.Kick;
var clap = samples["Clap-01"];
```

You can also point to a single file:

```csharp
var kick = GetSamples("Samples/Drums/Kick.wav");
```

## Git URLs (Auto Cache)

```csharp
Include "https://github.com/user/ME-Kits/scripts/synthInstance.cs";
var synth = Include.synthInstance.synth;

var drums = GetSamples("https://github.com/user/ME-Kits#samples/909");
var kick = drums.Kick;
```

Git repositories are cached under the project folder in `.gitcache/` and updated automatically.

## VST Presets (State Snapshots)

```csharp
var vital = CreateVst("Vital");

var presets = new List<VstPreset>
{
    vital.Preset("Init"),
    vital.Preset("Bright Lead")
};

vital.ApplyPreset(presets[0]);
```

## Notes

- Shared objects live until you clear state or restart the engine.
- If a module script changes, it is re-run automatically.
- Errors include file + line numbers when a script file path is known.
