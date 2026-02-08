# Scripting Syntax (Case-Insensitive)

This doc describes the optional case-insensitive scripting syntax and the available aliases.

## Quick Start (Copy/Paste)

```csharp
var vital = Music.CREATEVST("Vital");

Music.MIDI.DIVICE(0).PITCHBEND().TO(val => vital.PitchBend(val * 2f - 1f));

var ch1 = Music.AUDIO.CREATECHANNEL(1);
ch1.VSTEFFECT("ValhallaSupermassive");
ch1.ROUTE(vital);
ch1.GAIN(0.8f);

var time = Music.CREATETIMEMASTER();
time.START();
```

## Important Notes

- Case-insensitive calls are available through the dynamic root: `Music` / `music` / `MUSIC`.
- Variables you create (like `piano`, `vital`) are still case-sensitive.
- The original typed API still works as-is.
- Aliases are always supported; use them when you want shorter code.

## Style Guideline (Recommended, Optional)

The engine is permissive, but this keeps scripts readable and consistent:

- Use `Midi.Device`, `Audio.CreateChannel`, `CreateVst` as the default spelling.
- Actions are methods (`Route`, `Effect`, `Play`, `Stop`), values are properties (`Pan`, `Volume`).
- Keep top-level groups: `Audio.*`, `Midi.*`, `Time.*`, `Mod.*`.
- Short aliases are okay, but keep the long form as the "standard" in docs and examples.

## Case-Insensitive Root

`Music` exposes the same members as the script globals, but case-insensitive:

```csharp
var s1 = Music.CREATESYNTH();
var v1 = Music.CREATEVST("Vital");
var fx = Music.CREATEVSTEFFECT("ValhallaRoom");
var deckA = Music.CREATEDECK("DeckA");
var time = Music.CREATETIMEMASTER();
```

Aliases:

```csharp
var v1 = Music.VST("Vital");
```

## Audio (Fluent API)

All of these are case-insensitive when accessed via `Music.AUDIO` / `Music.Audio` / `Music.audio`.

```csharp
var ch1 = Music.AUDIO.CREATECHANNEL(1);
ch1.ROUTE(v1);
ch1.GAIN(0.7f);
ch1.VSTEFFECT("ValhallaRoom");
ch1.EFFECT(new SomeEffect());
ch1.EFFECT(Effect.Reverb);
ch1.CLEAREFFECTS();

Music.AUDIO.ALL.GAIN(0.9f);
Music.AUDIO.MASTER.GAIN(1.0f);
```

Supported Audio members (case-insensitive):

- `Audio.CreateChannel(int)`
- `Audio.Channel(int)`
- `Audio.All`
- `Audio.Master`
- `Channel.Route(ISynth)`
- `Channel.Gain(float|double)`
- `Channel.Effect(IAudioEffect)`
- `Channel.ClearEffects()`
- `Channel.Record`
- `Channel.VstEffect(string)`
- `CreateEffect()`
- `CreateMidiEffect()`

## MIDI (Fluent API)

All of these are case-insensitive when accessed via `Music.MIDI` / `Music.Midi` / `Music.midi`.

```csharp
Music.MIDI.DIVICE(0).PITCHBEND().TO(val => v1.PitchBend(val * 2f - 1f));
Music.MIDI.DEVICE(0).CONTROL(1).TO(val => s1.SetParameter("cutoff", val));
Music.MIDI.DEVICE(0).CHANNEL(0).TO(v1);
```

Supported MIDI members (case-insensitive):

- `Midi.Device(int)` (alias: `Divice`)
- `Midi.Map`
- `Device.Channel(int)`
- `Device.Pitchbend()` (alias: `PitchBend`)
- `Device.Control(int)`
- `Device.To(ISynth)` (alias: `To`, `TO`) -> returns `MidiSend`
- `ControlMapping.To(Action<float>)` (alias: `To`, `TO`)
- `Device.Jog(int, JogMode, int)`

## MIDI Mapping Helper

```csharp
var midimap = Music.MIDI.MAP;
midimap.SET("JogWheel", 21);
midimap.SETJOG("JogWheel", 21, JogMode.RelativeSigned, 1);

Music.MIDI.DEVICE(0).JOG(midimap, "JogWheel")
    .TO(delta => time.SCRATCHTICKS(delta));
```

## VST Helpers

```csharp
var v1 = Music.CREATEVST("Vital");
var fx = Music.CREATEVSTEFFECT("ValhallaRoom");

var v2 = Music.VST("Vital");
```

## Fluent Setup

```csharp
CreateGeneralMidi()
    .Pan(0.2f)
    .Channel(0)
    .Name("GM_AcousticGrandPiano");

CreateSynth()
    .Volume(0.7f)
    .Pan(-0.2f);

CreateVst("Vital")
    .Volume(0.9f)
    .Pan(0.1f);

CreateMic(0)
    .Gain(0.8f)
    .Mute(false);
```

## Custom Effects

```csharp
var fx = CreateEffect()
    .Reverb(r => r.Mix = 0.2f)
    .BitCrush(b => b.BitDepth = 6)
    .Noise(n => n.Amount = 0.02f);

Audio.CreateChannel(1).Effect(fx);

var preset = Effect.Reverb;
Audio.CreateChannel(2).Effect(preset);
```

## Custom MIDI Effects

```csharp
var rack = CreateMidiEffect()
    .Transpose(12)
    .Humanize(8)
    .Gate(0.9f);

var send = Midi.Device(0).To(synth);
send.AddEffect(rack);

send.AddEffect(MidiEffect.Transpose);
```

## MIDI -> Property Binding

```csharp
var fx = Effect.Reverb;
Midi.Device(0).Control(1).To(Bind(fx, "Mix", 0f, 1f));
Midi.Device(0).Control(2).To(Bind(fx, "Size", 0f, 1f));

var rec = Audio.Master.Record;
Midi.Device(0).Control(7).To(Bind(rec, "BitRateKbps", 64f, 320f));
```

## MIDI -> Methods / Switches

```csharp
var rec = Audio.Master.Record;
Midi.Device(0).Control(20).To(BindTrigger(rec, "Start"));
Midi.Device(0).Control(21).To(BindTrigger(rec, "Stop"));
Midi.Device(0).Control(22).To(BindTrigger(rec, "Render"));

Midi.Device(0).Control(23).To(BindToggle(rec, "Loop"));
Midi.Device(0).Control(24).To(BindSwitch(rec, "Loop"));

// getter/setter form
Midi.Device(0).cc(25).To(BindSwitch(() => rec.Loop, v => rec.Loop = v));
```

## Recording / Render

```csharp
var ch1 = Audio.CreateChannel(1);

var rec = ch1.Record;
rec.Start("Renders.ch1.wav"); // start recording
// ... audio plays ...
rec.Render(); // finalize (alias for Stop)

rec.Delete(); // delete last rendered file

rec.Override = true; // overwrite existing files on Start
rec.Loop = true;     // auto-restart after Stop (looping capture)

rec.OneShot = true;
rec.DurationSeconds = 4.0;
rec.Start("Renders.one_shot.wav"); // auto-stops after 4s

rec.Render("Renders.quick.wav", seconds: 2.0); // one-shot render

rec.BitRateKbps = 256;
rec.SampleRate = 48000;
rec.Channels = 2;
rec.WavBitDepth = 24;
rec.ResamplerQuality = 60;

// Quick default path:
rec.Start(); // writes to ./Recordings/record_ch1_yyyyMMdd-HHmmss.wav
```

Supported VST members (case-insensitive):

- `CreateVst(string)` (alias: `Vst(string)`)
- `CreateVstEffect(string)`

## Deck + Time Master

```csharp
var deckA = Music.CREATEDECK("DeckA");
deckA.LOAD("Samples/Loops/beat.wav");

var time = Music.CREATETIMEMASTER();
time.BINDDECK(deckA);
time.START();
```

Supported members (case-insensitive):

- `CreateDeck(string)`
- `CreateTimeMaster()`
