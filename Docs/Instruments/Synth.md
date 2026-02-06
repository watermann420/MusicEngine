# SimpleSynth (Synth)

Polyphonic synth with two oscillators, ADSR, filter, LFO, and effects. Ideal for direct sound design via properties or `SetParameter`.

## Concept

- `SimpleSynth` implements `ISynth` and is an `ISampleProvider`.
- Parameters can be set via properties or through `SetParameter` (string).
- The synth can be routed directly into `AudioEngine`.

## Syntax Example (Script)

```csharp
var synth = CreateSynth();

synth.Waveform = WaveType.Sawtooth;
synth.Cutoff = 0.6f;
synth.Resonance = 0.25f;
synth.Attack = 0.01f;
synth.Release = 0.2f;
synth.UnisonVoices = 3;
synth.UnisonSpread = 0.6f;

var pattern = CreatePattern(synth);
pattern.LoopLength = 2.0;
pattern.Note(60, 0.0, 0.5, 110);
pattern.Note(63, 0.5, 0.5, 110);
pattern.Note(67, 1.0, 0.5, 110);
pattern.Note(72, 1.5, 0.5, 110);
pattern.Play();
```

## Parameter Shortlist (SetParameter)

```csharp
synth.SetParameter("waveform", 2);       // 0..5 -> WaveType
synth.SetParameter("cutoff", 0.4f);      // 0..1
synth.SetParameter("resonance", 0.2f);   // 0..1
synth.SetParameter("attack", 0.01f);     // seconds
synth.SetParameter("release", 0.25f);    // seconds
synth.SetParameter("delaymix", 0.2f);    // 0..1
```

Copyright 2026 watermann429 and contributers.
