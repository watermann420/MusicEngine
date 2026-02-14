# Settings (Global Defaults)

All engine settings live in `MusicEngine.Core.Settings`. They are global defaults used when
creating audio devices, routing, and instruments. You can set them in your main script.

## Audio / Output

```csharp
Settings.SampleRate = 44100;
Settings.Channels = 2;
Settings.OutputBitDepth = 32;
Settings.OutputLatencyMs = 100;
Settings.OutputBufferCount = 3;
Settings.BufferSizeFrames = 512;
Settings.AutoBufferEnabled = true;
Settings.AutoBufferExtraLatencyMs = 50;
Settings.AutoBufferExtraBuffers = 1;
Settings.VirtualOutputLatencyMs = 80;
Settings.OutputRenderer = "waveout"; // or "asio", "portaudio" (Linux)
Settings.AsioDeviceName = "Focusrite USB ASIO"; // optional
Settings.AsioOutputChannels = 2;
Settings.AsioOutputChannelOffset = 0; // 0 = outputs 1/2
```

Buffer helper:

```csharp
Settings.Buffer(128);          // sets BufferSizeFrames + derived OutputLatencyMs
Settings.Buffer(128).Buffers(4).Virtual(120).VstEditor(256);
```

## Recording / Export

```csharp
Settings.WavBitDepth = 32;
Settings.BitRateKbps = 192;
```

## Safety / Silence

```csharp
Settings.MasterSafetyEnabled = false;
Settings.AudioSilenceThreshold = 1e-5f;
```

## Audio / VST / Sequencer Toggles

```csharp
Settings.AudioEffectsEnabled = true;
Settings.VstInstrumentsEnabled = true;
Settings.VstEffectsEnabled = true;
Settings.SequencerEnabled = true;
```

## VST Idle / Editor

```csharp
Settings.VstInstrumentSleepWhenIdle = true;
Settings.VstEffectSleepWhenIdle = true;
Settings.VstIdleThreshold = 2e-4f;
Settings.VstIdleTimeoutSeconds = 0.15;
Settings.VstEditorBlockSize = 512;
Settings.VstCloseOnDispose = false;
```
