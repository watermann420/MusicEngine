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
