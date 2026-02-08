<img width="1536" height="307" alt="BannerMusicEngine" src="https://github.com/user-attachments/assets/9b00e2f3-6971-41c3-8242-967f77d9e31d" />

![License](https://img.shields.io/badge/license-MEL-blue)
![C#](https://img.shields.io/badge/language-C%23-blue)
![.NET](https://img.shields.io/badge/.NET-10.0-purple)
[![Join Discord](https://img.shields.io/badge/Discord-Join%20Server-5865F2?logo=discord&logoColor=white)](https://discord.gg/tWkqHMsB6a)
![Status](https://img.shields.io/badge/status-Work_in_Progress-orange)


# MusicEngine

Scriptable C# music engine for realtime playback, instruments, and VST3 hosting.

## What is MusicEngine?

MusicEngine is a lightweight C# audio engine for scripting and realtime playback. It provides built-in instruments, routing, and VST3 hosting so you can play notes, shape sound, and build small music tools quickly.

## Pattern Example (Script)

```csharp
var synth = CreateSynth();
var pattern = CreatePattern(synth);

pattern.LoopLength = 4.0;
pattern.Note(60, 0.0, 0.5, 110);
pattern.Note(64, 0.5, 0.5, 110);
pattern.Note(67, 1.0, 1.0, 110);
pattern.Note(60, 2.0, 1.0, 110, slideTo: 67, slideTimeMs: 500);
pattern.NoteMs(72, 1500, 250, 110);
// Note(...) auto-uses ms when duration > 8.0 or beat > 32.0 (still supports beats)
pattern.Note(72, 1500, 250, 110);

var seq1 = pattern.Note(60, 0.0, 0.25, 100).Siquenz("0010101001101110");
seq1.Loop = true;

pattern.Play();
```

## MIDI Routing Example (Script)

```csharp
var synth = CreateSynth();

midi.device(0).to(synth);
midi.device(0).cc(1).to(value => synth.SetParameter("cutoff", value));
midi.device(0).pitchbend().to(value => synth.PitchBend(value * 2f - 1f));
```

## Modular Effects Example (Script)

```csharp
var fx = CreateEffect()
    .ReverbPreset("Hall")
    .BitCrushPreset("Lofi")
    .NoisePreset("Light")
    .FilterPreset("Low");

var ch1 = Audio.CreateChannel(1);
ch1.Effect(fx);
```

Preset factory:

```csharp
var ch2 = Audio.CreateChannel(2);
ch2.Effect(Effect.Reverb);
ch2.Effect(Effect.Drive);
```

Preset names:

```csharp
var fx2 = CreateEffect()
    .ReverbPreset("Hall")
    .DelayPreset("Echo")
    .DrivePreset("Warm");
Audio.CreateChannel(3).Effect(fx2);
```

Preset with overrides:

```csharp
var fx3 = CreateEffect()
    .ReverbPreset("Hall", r => r.Mix = 0.4f)
    .DelayPreset("Echo", d => d.TimeMs = 500f)
    .FilterPreset("Low", f => f.CutoffHz = 800f);
Audio.CreateChannel(4).Effect(fx3);
```

Standalone preset (for modulation):

```csharp
var reverbFx3 = ReverbPreset("Hall", r => r.Mix = 0.4f);
var mix = Mod.Var(reverbFx3, "Mix");
mix.Lfo(0.2f, 0.6f, rateHz: 0.5f);
```

## Modular Oscillators (Hybrid Synth)

```csharp
var synth = CreateSynth();
var osc3 = synth.Oscillator();
osc3.Waveform = WaveType.Sine;
osc3.Level = 0.2f;
osc3.Pan = -0.2f;

var osc4 = synth.Oscillator();
osc4.Waveform = WaveType.Sine;
osc4.Level = 0.2f;
osc4.Pan = 0.2f;
osc4.ModToFilter = 0.2f;
```

## VST3 Example (Script)

```csharp
var vital = CreateVst("Vital");
vital.SetParameterNormalized("Cutoff", 0.5f);

midi.device(0).to(vital);
midi.device(0).pitchbend().to(value => vital.PitchBend(value * 2f - 1f));

var pattern = CreatePattern(vital);
pattern.LoopLength = 4.0;
pattern.Note(60, 0.0, 0.5, 110);
pattern.Note(64, 0.5, 0.5, 110);
pattern.Note(67, 1.0, 1.0, 110);
pattern.Play();
```

## VST State Save/Load

```csharp
var vital = CreateVst("Vital");
vital.State(); // on /S or exit, writes base64 into the ()
```

Notes:
- The inline `State()` call is updated on refresh or exit, so you can copy/share the script.
- States are stored per script under `.musicengine/states/<script>/<name>.state`.
- Missing VSTs warn and stay silent instead of crashing.

Manual overrides still work:
```csharp
vital.LoadState("States.vital.state");
vital.SaveState("States.vital.state");
```

## Virtual Output (Discord / Virtual Mic)

To send audio into Discord as a microphone, you need a virtual audio device (e.g. VB-CABLE or VoiceMeeter).
MusicEngine can route the master or a channel to any Windows output device.

```csharp
Audio.Output.List(); // list render devices

Audio.Master.VirtualOut("CABLE Input"); // route master to virtual mic

var ch1 = Audio.CreateChannel(1);
ch1.VirtualOut("CABLE Input"); // route channel to virtual mic
```

Notes:
- Without a virtual audio device installed, Windows has no "virtual mic" target.
- In Discord, set Input Device to the virtual cable.

## Audio Input (Mics / Line-In)

```csharp
Audio.Input.List(); // list capture devices

var mic = CreateMic(0); // or CreateInput(0)
var ch1 = Audio.CreateChannel(1);
ch1.Route(mic);
ch1.Gain(0.7);
```

## Fluent Instrument Setup

```csharp
CreateGeneralMidi()
    .Pan(0.2f)
    .Channel(0)
    .Name("GM_AcousticGrandPiano");

CreateSynth()
    .Volume(0.7f)
    .Pan(-0.2f);

CreateVst("Vital")
    .Volume(0.9f)
    .Pan(0.1f);

CreateMic(0)
    .Gain(0.8f)
    .Mute(false);
```

## Variable Modulation

```csharp
var pan = Mod.Pan(piano, 0f);
pan.Random(-0.5f, 0.5f, everyMs: 400);

var vol = Mod.Volume(synth, 0.7f);
vol.Lfo(0.2f, 0.9f, rateHz: 0.5f);

// Any property
var cutoff = Mod.Var(synth, "Cutoff");
cutoff.Random(0.2f, 0.9f, everyMs: 300);
```

## General Instruments Example (Script)

```csharp
var piano = CreateGeneralMidi();
piano.Program = GeneralMidiProgram.AcousticGrandPiano;
piano.Volume = 0.8f;
piano.Pan = 0f;
piano.Reverb = 0.2f;
piano.Channel = 0;

midi.device(0).to(piano);
midi.device(0).pitchbend().to(value => piano.PitchBend(value * 2f - 1f));

var pattern = CreatePattern(piano);
pattern.LoopLength = 4.0;
pattern.Note(60, 0.0, 1.0, 100);
pattern.Note(67, 1.0, 1.0, 100);
pattern.Note(72, 2.0, 2.0, 100);
pattern.Play();
```

## Features (Current)

- Low-latency MIDI routing and controller mapping.
- Scriptable pattern sequencer with loop length and per-note velocity.
- Built-in polyphonic synth with oscillators, ADSR, filter, LFO, and effects.
- General MIDI output wrapper for system MIDI devices.
- VST3 hosting with parameter control and editor support.
- Channel routing with per-channel effects and master processing.
- Recording taps for master and per-channel audio.

## Planned Features

- **Lighting and show control:** DMX512, Art-Net, sACN (E1.31), MIDI Show Control.
- **MIDI and timing:** MIDI 1.0, MIDI 2.0, MTC, SMPTE/LTC sync, OSC control, MIDI screen/LED feedback with live data.
- **Audio routing:** ASIO, WASAPI, CoreAudio, JACK, ALSA, loopback/virtual devices.
- **Game engine integration:** Unity/Unreal hooks, custom engine integration, realtime music logic control.
- **Modular control:** CV rack-style modularity via code for every variable and setting.
- **DSP and hardware:** planned DSP control surface and library to drive custom sound cards for full flexibility.

## Notes

- The project uses NAudio for audio/MIDI and provides its own instrument wrappers.
- VST3 scanning can be configured via `MUSICENGINE_VST3_PATHS` (semicolon-separated).

## Install

- Install .NET SDK 10.0 (or newer).
- Install Visual Studio 2022 with Desktop development with C++ (for the native VST3 host).
- Rider works fine, but MSBuild from Visual Studio Build Tools is still required for the native layer.

## Build

**Managed build (C#):**
- Open the solution in Rider or Visual Studio and build the `MusicEngine` project.

**Native VST3 host (C++):**
- Rider: open `MusicEngine.CppLayer/native/MusicEngine.CppLayer.Native.vcxproj` and build x64 Debug/Release.
- Visual Studio: open the same `.vcxproj`, select x64 + Debug/Release, then Build.
- Command line (PowerShell): `./build_native.ps1 -Configuration Debug`

## Mentions

- NAudio: https://github.com/naudio/NAudio
- Steinberg VST3 format: https://www.steinberg.net/en/company/technologies/vst3.html


## Links

- License: https://github.com/watermann420/MusicEngine/blob/master/LICENSE
- Contributing: https://github.com/watermann420/MusicEngine/blob/master/CONTRIBUTING

Copyright 2026 watermann429 and contributers.

