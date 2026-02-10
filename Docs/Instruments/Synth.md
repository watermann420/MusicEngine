# SimpleSynth (Synth)

Polyphonic synth with two oscillators, ADSR, filter, and LFO. Effects are handled through shared audio effects.

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

// Add extra oscillators (modular)
var osc3 = synth.Oscillator();
osc3.Waveform = WaveType.Sine;
osc3.Level = 0.2f;
osc3.Pan = -0.3f;

var osc4 = synth.Oscillator();
osc4.Waveform = WaveType.Sine;
osc4.Level = 0.2f;
osc4.Pan = 0.3f;
osc4.ModToFilter = 0.2f; // audio-rate filter modulation

var pattern = CreatePattern(synth);
pattern.LoopLength = 2.0;
pattern.Note(60, 0.0, 0.5, 110);
pattern.Note(63, 0.5, 0.5, 110);
pattern.Note(67, 1.0, 0.5, 110);
pattern.Note(72, 1.5, 0.5, 110);
pattern.Play();
```

## Parameter Shortlist (SetParameter)

Notes:
- Most parameters accept any float; the engine does not clamp to musical ranges.
- Filter cutoff uses a normalized value but is only clamped at render time for stability (0 Hz .. ~Nyquist).
- LFO/Vibrato rates can exceed audible frequencies for extreme modulation.

```csharp
synth.SetParameter("waveform", 2);       // 0..5 -> WaveType
synth.SetParameter("cutoff", 0.4f);      // 0..1
synth.SetParameter("resonance", 0.2f);   // 0..1
synth.SetParameter("attack", 0.01f);     // seconds
synth.SetParameter("release", 0.25f);    // seconds
```

## Modular Oscillators

```csharp
var osc = synth.Oscillator();
osc.Waveform = WaveType.Pulse;
osc.Level = 0.4f;
osc.PulseWidth = 0.2f;
osc.ModToPitch = 0.5f;
```

## Shared Effects

```csharp
var ch = Audio.CreateChannel(1);
ch.Route(synth);
ch.Effect(new SimpleDelayEffect { Mix = 0.2f, TimeMs = 250f });
ch.Effect(new SimpleReverbEffect { Mix = 0.1f, Size = 0.4f });
```

Copyright 2026 watermann429 and contributers.
