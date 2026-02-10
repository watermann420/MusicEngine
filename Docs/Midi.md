# MIDI (Routing, Mapping, Toggles)

This doc covers the fluent MIDI API in scripts, including routing, controller mapping, and how to
enable/disable devices, channels, or individual routes.

## Basic Routing

```csharp
var synth = CreateSynth();
midi.Device(0).to(synth); // all channels from device 0
```

Route a specific channel:

```csharp
var synth = CreateSynth();
midi.Device(0).Channel(1).to(synth); // channel 2 (0-based)
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
