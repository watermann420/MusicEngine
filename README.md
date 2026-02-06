<img width="1536" height="307" alt="BannerMusicEngine" src="https://github.com/user-attachments/assets/9b00e2f3-6971-41c3-8242-967f77d9e31d" />

![License](https://img.shields.io/badge/license-MEL-blue)
![C#](https://img.shields.io/badge/language-C%23-blue)
![.NET](https://img.shields.io/badge/.NET-10.0-purple)
[![Join Discord](https://img.shields.io/badge/Discord-Join%20Server-5865F2?logo=discord&logoColor=white)](https://discord.gg/tWkqHMsB6a)
![Status](https://img.shields.io/badge/status-Work_in_Progress-orange)


# MusicEngine 

Quick entry points and practical examples for the main instruments and the VST3 integration.

## What is MusicEngine?

MusicEngine is a lightweight C# audio engine for scripting and realtime playback. It provides built-in instruments, routing, and VST3 hosting so you can play notes, shape sound, and build small music tools quickly.

## Quickstart (C#)

```csharp
using var engine = new MusicEngine.Core.AudioEngine();
engine.Initialize();

var synth = new MusicEngine.Instruments.SimpleSynth();
engine.AddSampleProvider(synth);

synth.NoteOn(60, 100);
System.Threading.Thread.Sleep(300);
synth.NoteOff(60);
```

## Notes

- The project uses NAudio for audio/MIDI and provides its own instrument wrappers.
- VST3 scanning can be configured via `MUSICENGINE_VST3_PATHS` (semicolon-separated).

## Includes

```csharp
using MusicEngine.Core;
using MusicEngine.Instruments;
using MusicEngine.Vst;
```

## Mentions

- NAudio: https://github.com/naudio/NAudio
- Steinberg VST3 format: https://www.steinberg.net/en/company/technologies/vst3.html

## Links

- License: https://github.com/watermann420/MusicEngine/blob/master/LICENSE
- Contributing: https://github.com/watermann420/MusicEngine/blob/master/CONTRIBUTING

Copyright 2026 watermann429 and contributers.

