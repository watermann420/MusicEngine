# Audio Input

Live audio inputs (microphones, line-in, audio interfaces) can be routed like any other source.
Use WASAPI capture devices and route them to channels or the master.

## List Devices

```csharp
Audio.Input.List(); // prints "0: Microphone (USB...)" etc.
```

## Create and Route

```csharp
var mic = CreateMic(0); // or CreateInput(0)

var ch1 = Audio.CreateChannel(1);
ch1.Route(mic);
ch1.Gain(0.7);
ch1.Route(Master);
```

If you don't assign a channel, the input goes straight to the master output:

```csharp
var mic = CreateMic(0); // master output by default
```

## Direct Controls

```csharp
mic.Gain = 0.8f;
mic.Mute = false;
mic.Pan = 0f;
```

## Channel Sends (Routing Between Channels)

```csharp
var ch1 = Audio.CreateChannel(1);
var ch2 = Audio.CreateChannel(2);

// simple send (ch1 -> ch2)
ch1.Route(2);

// output-style routing (ch1 -> ch2, no master)
ch1.Channel = ch2;

// sidechain-style send with gain control
var side = ch1.SideChain(2, gain: 0.5f);
side.Gain = 0.7f;
```

Notes:
- Inputs are routed as normal `ISampleProvider` sources.
- Effects are applied via channel or master effects.
