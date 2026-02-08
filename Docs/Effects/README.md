# Effects Overview

All effects live under `MusicEngine/Effects/` and are modular.

## Audio Effects

Location: `MusicEngine/Effects/Audio/`

Examples:

- `SimpleDelayEffect`
- `SimpleReverbEffect`
- `TremoloEffect`

Audio effects implement `IAudioEffect` and can be attached to:

```csharp
var ch = Audio.CreateChannel(1);
ch.Effect(new SimpleDelayEffect { Mix = 0.2f, TimeMs = 250f });
ch.Effect(new SimpleReverbEffect { Mix = 0.1f, Size = 0.4f });
```

## MIDI Effects

Location: `MusicEngine/Effects/Midi/`

Examples:

- `MidiSend` (routing + random input + LFO modulation)

```csharp
var send = Midi.Device(0).To(vital);
send.GenerateRandomInput(16);
send.Lfo("cutoff", 0.2f, 0.8f);
```

## Modulation

Location: `MusicEngine/Effects/Modulation/`

Examples:

- `LfoModulator`

## VST Effects

Location: `MusicEngine/Effects/Vst/`

Examples:

- `Vst3Effect`

```csharp
var ch = Audio.CreateChannel(1);
ch.VstEffect("ValhallaSupermassive");
```
