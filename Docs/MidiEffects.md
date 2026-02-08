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

## Preset MIDI Effects (Factory)

```csharp
var send = Midi.Device(0).To(vital);
send.AddEffect(MidiEffect.Transpose);
send.AddEffect(MidiEffect.Humanize);
send.AddEffect(MidiEffect.Gate);
```

## Custom MIDI Effect Rack

```csharp
var rack = CreateMidiEffect()
    .Transpose(12)
    .Humanize(10)
    .Gate(0.8f)
    .Trigger(
        noteOn: (note, vel) => Console.WriteLine($"ON {note} {vel}"),
        noteOff: note => Console.WriteLine($"OFF {note}")
    );

var send = Midi.Device(0).To(vital);
send.AddEffect(rack);
```

## Custom MIDI Logic (Callbacks)

```csharp
var rack = CreateMidiEffect()
    .Custom(
        noteOn: (ref int note, ref int vel) =>
        {
            if (note < 48) return false; // block low notes
            vel = Math.Min(127, vel + 10);
            return true;
        },
        noteOff: (ref int note) => true
    );
```

## Map MIDI to Any Property

```csharp
var fx = Effect.Reverb;
var send = Midi.Device(0).To(vital);

Midi.Device(0).Control(1).To(Bind(fx, "Mix", 0f, 1f));
Midi.Device(0).Control(2).To(Bind(fx, "Size", 0f, 1f));

var rec = Audio.Master.Record;
Midi.Device(0).Control(7).To(Bind(rec, "BitRateKbps", 64f, 320f));
```

## Map MIDI to Methods / Switches

```csharp
var rec = Audio.Master.Record;
Midi.Device(0).Control(20).To(BindTrigger(rec, "Start"));
Midi.Device(0).Control(21).To(BindTrigger(rec, "Stop"));
Midi.Device(0).Control(22).To(BindTrigger(rec, "Render"));
Midi.Device(0).Control(23).To(BindTrigger(rec, "Delete"));

Midi.Device(0).Control(24).To(BindToggle(rec, "Loop"));   // toggle on press
Midi.Device(0).Control(25).To(BindSwitch(rec, "Loop"));   // value -> true/false

// With getter/setter (if you prefer variables):
Midi.Device(0).cc(26).To(BindSwitch(() => rec.Loop, v => rec.Loop = v));
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
