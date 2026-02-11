# MIDI (Routing, Mapping, Toggles)

This doc covers the fluent MIDI API in scripts, including routing, controller mapping, and how to
enable/disable devices, channels, or individual routes.

## Basic Routing

```csharp
var synth = CreateSynth();
midi.Device(0).to(synth); // all channels from device 0
```

Layer multiple synths:

```csharp
var synth = CreateSynth();
var piano = CreateGeneralMidi();
var pad = CreateSynth();

var stack = midi.Device(0).to(synth, piano, pad);
stack.add(CreateSampler());
stack.remove(piano);
stack.remove();    // removes last added
stack.removeAll(); // clears all routes
```

Notes:
- Missing VSTs are ignored in layered routes, so fallbacks still play.

Priority / fallback routing:

```csharp
var vital = CreateVst("Vital");
var synth = CreateSynth();
var piano = CreateGeneralMidi();

var stack = midi.Device(0).to(vital, < synth, < piano);
stack.active(vital, true);  // only vital plays
stack.active(vital, false); // fallback to synth
```

Notes:
- If the VST plugin is missing, the fallback synth is used automatically.

Route a specific channel:

```csharp
var synth = CreateSynth();
midi.Device(0).Channel(1).to(synth); // channel 2 (0-based)
```

Layer multiple synths on a specific channel:

```csharp
var lead = CreateSynth();
var bass = CreateSynth();

var stack = midi.Device(0).Channel(1).to(lead, bass);
stack.add(CreateSampler());
stack.remove(); // removes last added
```

## Controller Mapping

```csharp
var synth = CreateSynth();

midi.Device(0).cc(1).to(value => synth.SetParameter("cutoff", value));
midi.Device(0).pitchbend().to(value => synth.PitchBend(value * 2f - 1f));
```

## Device/Channel/Route Toggles

Use handles to enable/disable input at different levels. Useful when multiple musicians share
devices, or when you want to mute a specific route without removing it.

```csharp
var synth = CreateSynth();

var midi1 = midi.Device(0);        // device handle
var route1 = midi1.to(synth);      // route handle

midi1.Active(false);               // disable device 0 input
midi1.Active(true);                // enable device 0 input

midi1.Channel(1).Active(false);    // disable channel 2 on device 0
route1.Active(false);              // disable only this route
```

Notes:
- Disabling a device/channel/route optionally sends All Notes Off (default true).
- Device/channel indices are 0-based.
