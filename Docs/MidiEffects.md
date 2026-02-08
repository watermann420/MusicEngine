# MIDI Effects

`MidiSend` lets you route a device to a synth and then apply simple MIDI effects like random input and modulation.

## Basic Routing + Handle

```csharp
var vital = CreateVst("Vital");
var MidiSend = Midi.Device(0).To(vital);
```

You can ignore the return value if you only need routing:

```csharp
Midi.Device(0).To(vital);
```

## Random Input

```csharp
var send = Midi.Device(0).To(vital);
send.GenerateRandomInput(noteCount: 24, minNote: 48, maxNote: 72);
```

## LFO Modulation

```csharp
var send = Midi.Device(0).To(vital);
send.Lfo("cutoff", min: 0.2f, max: 0.8f, hz: 1.5, durationSeconds: 10);
```

## MIDI Effect Chain

```csharp
var send = Midi.Device(0).To(vital);
send.AddEffect(new TransposeEffect { Semitones = 12 });
send.AddEffect(new VelocityHumanizeEffect { Range = 6 });
send.AddEffect(new RandomGateEffect { Probability = 0.9f });
```

## Manual Notes

```csharp
send.NoteOn(60, 100);
send.NoteOff(60);
```

## Case-Insensitive Syntax

```csharp
var send = Music.MIDI.DEVICE(0).TO(vital);
send.GENERATERANDOMINPUT(16, 36, 72);
```
