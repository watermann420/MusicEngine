# Audio Output

Output devices are listed at engine startup and can be used as routing targets for channels.
This enables multi-output setups (e.g., audience vs. headphones).

Default routing behavior:
- Sources with no channel assignment are routed to the master output.
- Channels are not routed anywhere until you explicitly route them (e.g. to Master or an output device).

## List Devices

```csharp
Audio.Output.List(); // "0: Speakers ... (2ch @ 48000Hz)" etc.
```

## Output Renderer (Windows / Linux)

```csharp
Audio.SetRenderer(waveout);   // Windows default
Audio.SetRenderer(portaudio); // Linux default

Audio.Output.ListAsio();    // "0: Focusrite USB ASIO" etc.
Audio.SetRenderer(asio, "Focusrite USB ASIO");
Audio.SetRenderer(asio, 0); // by index from ListAsio
Audio.SetRenderer(asio, 0, 1); // driver index + master output pair (3/4)
```

Notes:
- ASIO is Windows-only.
- On Linux, PortAudio uses the default device (device selection is planned).
- `Audio.Output.ListAsio()` is Windows-only.

## Per-Channel Outputs (Multiple Devices, Windows)

```csharp
var ch1 = Audio.CreateChannel(1);
var ch2 = Audio.CreateChannel(2);

ch1.SetRenderer(waveout, 0); // channel 1 -> output device 0
ch2.SetRenderer(waveout, 1); // channel 2 -> output device 1
```

## Per-Channel Outputs (ASIO Multi-Out, Windows)

```csharp
Audio.SetRenderer(asio, 0); // enable ASIO renderer first
Settings.AsioOutputChannels = 6; // outputs 1/2, 3/4, 5/6

var ch1 = Audio.CreateChannel(1);
var ch2 = Audio.CreateChannel(2);
var ch3 = Audio.CreateChannel(3);

ch1.SetRenderer(asio, 0); // outputs 1/2
ch2.SetRenderer(asio, 1); // outputs 3/4
ch3.SetRenderer(asio, 2); // outputs 5/6
```

## Route a Channel to an Output

```csharp
var ch1 = Audio.CreateChannel(1);
Audio.Output.Route(1, 0); // channel 1 -> output device 0
```

To hear a channel in the main mix, route it to the master:

```csharp
var ch1 = Audio.CreateChannel(1);
ch1.Route(Master); // or ch1.To(Master)
```

## Multi-Output Soundcards (Channel Offsets, Windows)

If a device exposes multiple output channels, you can target a specific pair with an offset:

```csharp
var deckA = Audio.CreateChannel(1);
var deckB = Audio.CreateChannel(2);

// device 0 has 4 outputs (0/1 and 2/3)
deckA.VirtualOut(0, outputChannelOffset: 0); // out 1/2
deckB.VirtualOut(0, outputChannelOffset: 2); // out 3/4
```

You can also route directly from the channel:

```csharp
var ch1 = Audio.CreateChannel(1);
ch1.VirtualOut(0);
```

If you want a channel to feed another channel:

```csharp
var ch1 = Audio.CreateChannel(1);
var ch2 = Audio.CreateChannel(2);
ch1.Channel = ch2; // output ch1 into ch2
```

## DJ-Style Monitoring Example

```csharp
var audience = Audio.CreateChannel(1);
var phones = Audio.CreateChannel(2);

audience.VirtualOut(0); // speakers
phones.VirtualOut(1);   // headphones

// hear audience mix in headphones
var cue = audience.SideChain(2, gain: 0.3f);
```

## DJ Cue Switch Helper

```csharp
var audience = Audio.CreateChannel(1);
var phones = Audio.CreateChannel(2);

audience.VirtualOut(0);
phones.VirtualOut(1);

var cue = Audio.Cue.Create(1, 2, cueGain: 0.35f);
cue.CueOn();   // monitor audience mix in headphones
cue.CueOff();  // back to headphones-only
```

Notes:
- Use multiple channel virtual outs to route to multiple devices (Windows).
- Windows exposes each render endpoint as a separate output device.
- For multi-output interfaces, use `outputChannelOffset` to target specific pairs.
