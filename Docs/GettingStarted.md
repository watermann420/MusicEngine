# Getting Started

This short guide shows where the default script lives and the basic workflow to start making sound.

## 1) Script Location

The engine loads the default script from:

```
Scripts/test_script.cs
```

If the file does not exist, it is created on first launch. You can also use `Scripts/test_script.csx`.

## 2) Basic Workflow

1. Start the engine (debug or release).
2. Edit `Scripts/test_script.cs` (or `Scripts/test_script.csx`).
3. In the console, use `S` to reload and run the script.
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

## 3b) Speech (Text-to-Speech)

```csharp
var speech = CreateSpeech();
speech.Phrase("hello", note: 60);
speech.Phrase("world", note: 62);

midi.Device(0).to(speech);
```

Note: `CreateSpeech()` requires the optional `MusicEngine.Library` build (it loads `MusicEngine.Library.dll`). It uses Windows TTS when available and falls back to offline synthesis.

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
