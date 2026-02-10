# Recording and Render

MusicEngine can record the master output or any channel in real time. "Render" here means finalize the recording to disk (not offline/bounce).

## Master Recording

```csharp
var rec = Audio.Master.Record;
rec.Start("Renders.master.wav");
// ... audio plays ...
rec.Render(); // finalize (alias for Stop)
```

## Channel Recording

```csharp
var ch1 = Audio.CreateChannel(1);
var rec = ch1.Record;

rec.Start("Renders.ch1.wav");
// ...
rec.Stop(); // same as Render
```

## Default Paths

```csharp
var rec = Audio.Master.Record;
rec.Start(); // ./Recordings/record_master_yyyyMMdd-HHmmss.wav
```

## Overwrite and Loop

```csharp
var rec = Audio.Master.Record;
rec.Override = true; // overwrite existing file
rec.Loop = true;     // auto-restart after Stop
rec.DefaultFormat = "mp3";

rec.Start("Renders.loop.wav");
// ...
rec.Stop(); // auto-restarts because Loop = true
```

## Delete Last Render

```csharp
var rec = Audio.Master.Record;
rec.Start("Renders.temp.wav");
// ...
rec.Render();

rec.Delete(); // delete last rendered file
```

## One-Shot Recording

```csharp
var rec = Audio.Master.Record;
rec.OneShot = true;
rec.DurationSeconds = 3.0;

rec.Start("Renders.oneshot.wav"); // auto-stops after 3s
```

## Render Shortcut (One-Shot)

```csharp
var rec = Audio.Master.Record;
rec.Render("Renders.quick.wav", seconds: 2.0); // start + auto-stop
```

## Supported Formats

- `wav` (float32)
- `wav16` / `wav24` / `wav32`
- `mp3`
- `m4a` / `aac`
- `wma`

Format can be set via the file extension or the optional `format` argument.

```csharp
var rec = Audio.Master.Record;
rec.Start("Renders.master.mp3"); // inferred

rec.Start("Renders.master", format: "mp3"); // explicit
```

## Render Quality Settings

```csharp
var rec = Audio.Master.Record;
rec.BitRateKbps = 256;   // mp3/aac/wma
rec.SampleRate = 48000;  // resample on render
rec.Channels = 2;        // force mono/stereo
rec.WavBitDepth = 24;    // wav16 / wav24 / wav32
rec.ResamplerQuality = 60; // 1..60 (higher = better)

rec.Start("Renders.quality.wav24");
```

## Case-Insensitive Syntax (Optional)

```csharp
var rec = Music.AUDIO.MASTER.RECORD;
rec.START("Renders/master.wav");
rec.RENDER();
```
