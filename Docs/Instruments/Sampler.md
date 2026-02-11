# Sampler Instrument

The sampler lets you load audio files and map them to MIDI notes. You can use it like a sample piano (one sample per note) or like a drum pad (one sample per pad).

## Basic Example

```csharp
var sampler = CreateSampler();
sampler.LoadSample("PianoC4", "Samples/Piano/C4.wav", rootNote: 60);
sampler.MapSample(60, "PianoC4");

midi.device(0).to(sampler);
```

## Folder Loading + Mapping

```csharp
var sampler = CreateSampler();
sampler.LoadSamplesFromFolder("Samples/Drums", "*.wav", recursive: true);

sampler.MapSample(36, "Kick");
sampler.MapSample(38, "Snare");
sampler.MapSample(42, "ClosedHat");
sampler.MapSample(46, "OpenHat");
```

## Sample Folder Helper (GetSamples)

```csharp
var samples = GetSamples("Samples/Drums");

var sampler = CreateSampler();
sampler.LoadSample("Kick", samples.Kick);
sampler.LoadSample("Snare", samples.Snare);

sampler.MapSample(36, "Kick");
sampler.MapSample(38, "Snare");
```

File names are exposed as properties (invalid chars become `_`).
Use `samples["Kick-01"]` when you need the raw file name.

## Sample Piano (Nearest Sample Mode)

```csharp
var sampler = CreateSampler();
sampler.LoadSamplesFromFolder("Samples/Piano", "*.wav");

sampler.MapSample(48, "PianoC3");
sampler.MapSample(60, "PianoC4");
sampler.MapSample(72, "PianoC5");

sampler.UseNearestSample = true;
midi.device(0).to(sampler);
```

## Pattern Triggering

```csharp
var sampler = CreateSampler();
sampler.LoadSample("Kick", "Samples/Drums/kick.wav");
sampler.MapSample(36, "Kick");

var pattern = CreatePattern(sampler);
pattern.Note(36, 0.0, 0.25, 110);
pattern.Note(36, 1.0, 0.25, 110);
pattern.Play();
```

## Sample Settings (Pitch, Speed, Gain, Loop)

```csharp
sampler.SetSampleSettings("Kick", s =>
{
    s.Gain = 0.9f;
    s.PitchSemitones = -2f;
    s.PlaySpeed = 0.9f;
    s.Loop = false;
    s.OneShot = true;
});
```

## Global Playback Controls

Notes:
- Most parameters accept any float; the engine does not clamp to musical ranges.
- Play speed is kept >= 0 to avoid invalid playback direction in the current sampler.

```csharp
sampler.Volume = 0.8f;
sampler.Pan = 0f;
sampler.PitchSemitones = 0f;
sampler.PlaySpeed = 1f;
sampler.ReleaseSeconds = 0.05f;
```

## Case-Insensitive Syntax (Optional)

```csharp
var sampler = Music.CREATESAMPLER();
Music.MIDI.DEVICE(0).TO(sampler);
```
