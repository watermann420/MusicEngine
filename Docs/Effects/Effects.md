# Effects Overview

All effects live under `MusicEngine/Effects/` and are modular.

## Audio Effects

Location: `MusicEngine/Effects/Audio/`

Examples:

- `SimpleDelayEffect`
- `SimpleReverbEffect`
- `TremoloEffect`
- `BitCrusherEffect`
- `NoiseEffect`
- `GainEffect`
- `DriveEffect`
- `SimpleFilterEffect`

Audio effects implement `IAudioEffect` and can be attached to:

```csharp
var ch = Audio.CreateChannel(1);
ch.Effect(new SimpleDelayEffect { Mix = 0.2f, TimeMs = 250f });
ch.Effect(new SimpleReverbEffect { Mix = 0.1f, Size = 0.4f });
```

## Preset Effects (Factory)

```csharp
var ch = Audio.CreateChannel(1);
ch.Effect(Effect.Reverb);
ch.Effect(Effect.BitCrush);
ch.Effect(Effect.Drive);
```

## Custom Effect Rack

```csharp
var fx = CreateEffect()
    .Reverb(r => r.Mix = 0.2f)
    .BitCrush(b => b.BitDepth = 6)
    .Noise(n => n.Amount = 0.02f)
    .Filter(f => { f.Type = SimpleFilterType.LowPass; f.CutoffHz = 1200f; })
    .Gain(g => g.Gain = 0.9f);

var ch = Audio.CreateChannel(1);
ch.Effect(fx);
```

## Preset Names

```csharp
var fx = CreateEffect()
    .ReverbPreset("Hall")
    .DelayPreset("Echo")
    .DrivePreset("Warm");

Audio.CreateChannel(1).Effect(fx);
```

Preset with overrides:

```csharp
var fx = CreateEffect()
    .ReverbPreset("Hall", r => r.Mix = 0.4f)
    .DelayPreset("Echo", d => d.TimeMs = 500f)
    .FilterPreset("Low", f => f.CutoffHz = 800f);
```

Standalone preset (for modulation):

```csharp
var reverbFx3 = ReverbPreset("Hall", r => r.Mix = 0.4f);
var mix = Mod.Var(reverbFx3, "Mix");
mix.Lfo(0.2f, 0.6f, rateHz: 0.5f);
```

Available presets (by effect):
- Reverb: `Room`, `Hall`, `Large`, `Plate`
- Delay: `Slap`, `Echo`, `PingPong`
- Tremolo: `Soft`, `Hard`, `Slow`
- BitCrush: `Lofi`, `Mild`
- Noise: `Light`, `Tape`
- Drive: `Warm`, `Hard`
- Filter: `Low`, `High`

## Custom DSP Callback

```csharp
var fx = CreateEffect()
    .Custom((buffer, offset, count, format) =>
    {
        for (int i = offset; i < offset + count; i++)
        {
            buffer[i] *= 0.9f;
        }
    }, name: "SoftGain");
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

State snapshot (copy/share) works the same as instruments:
```csharp
var fx = CreateVstEffect("ValhallaRoom");
fx.State(); // on /S or exit, writes base64 into the ()
```

```csharp
var ch = Audio.CreateChannel(1);
ch.VstEffect("ValhallaSupermassive");
```
