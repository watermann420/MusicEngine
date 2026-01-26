

![Banner](https://github.com/user-attachments/assets/382f24fd-e758-454f-b4a2-f442bd4da2b2)

# Music Engine

![License](https://img.shields.io/badge/license-MEL-blue)
![C#](https://img.shields.io/badge/language-C%23-blue)
![.NET](https://img.shields.io/badge/.NET-10.0-purple)
![Status](https://img.shields.io/badge/status-Work_in_Progress-orange)

**Music Engine** is a modular, open-source live-coding music engine written in C#. It combines code, MIDI, patterns, and real-time audio to enable flexible music production, live performance, and interactive audio programming.

## Multiple Function Aliases

MusicEngine supports multiple aliases for all functions, allowing you to choose the syntax you prefer:

### Examples
```csharp
// All of these work identically:
CreateSynth()  // Full name
synth()        // Short name
s()            // Very short
newSynth()     // Alternative

// General MIDI instruments:
CreateGeneralMidiInstrument(GeneralMidiProgram.AcousticGrandPiano)
gm(GeneralMidiProgram.AcousticGrandPiano)     // Short form
newGm(GeneralMidiProgram.AcousticGrandPiano)  // Alternative

// Transport control:
Start()  or  play()  or  run()  or  go()
Stop()   or  pause() or  halt()
SetBpm(120)  or  bpm(120)  or  tempo(120)

// Navigation:
Skip(4)  or  jump(4)  or  seek(4)

// MIDI routing:
midi.device(0)  or  midi.input(0)

// VST plugins:
vst.load("MySynth")  or  vst.get("MySynth")  or  vst.plugin("MySynth")
```

Use whatever feels natural to you - all aliases are fully supported!

## Overview

MusicEngine provides a complete audio production framework with:

- Real-time audio synthesis and sample playback
- **128 General MIDI instruments** via Windows built-in synthesizer
- Pattern-based sequencing with loop support
- MIDI input/output routing and control mapping
- VST plugin hosting (VST2 and VST3)
- **14 professional audio effects** (Reverb, Delay, Chorus, Compressor, EQ, Distortion, and more)
- **Audio routing system** with buses, channels, and sends
- C# scripting via Roslyn for live coding
- High-precision timing with MIDI clock synchronization
- Audio recording and export capabilities

## The Music Engine Editor

The Editor is a work in progress.

Git: https://github.com/watermann420/MusicEngineEditor

---

## Features

### Audio Engine
- Built on NAudio for robust Windows audio support
- DirectSound / WASAPI / ASIO audio output
- Multithreaded architecture: audio, pattern processing, and UI run in parallel
- Configurable sample rate (default 44100 Hz)
- Multi-instrument mixing with volume control per channel
- Master volume with hard clipping protection

### MIDI Input/Output
- Full MIDI device enumeration and routing
- Note On/Off, Control Change, Pitch Bend support
- MIDI output for controlling external hardware/software
- Control mapping to synth parameters
- Transport controls (start, stop, BPM mapping)
- Range mapping for keyboard splits
- Extremely low latency (~0.5 ms)

### Sequencer
- Pattern-based composition with looping
- Multiple timing precision modes (Standard, HighPrecision, AudioRate)
- Beat subdivisions from 1/8 to 1/480 PPQN
- Jitter compensation for stable timing
- MIDI clock sync (internal and external)
- Scratching mode for DJ-style control
- Event emission for visualization

### VST Plugin Hosting
- VST2 (.dll) and VST3 (.vst3) support
- Automatic plugin scanning
- Load plugins by name or path
- Route MIDI to VST instruments
- Parameter control via scripting

### Sample Instruments
- Load WAV, MP3, FLAC, OGG, AIFF samples
- Pitch-shifting based on root note
- Multi-sample mapping with note ranges
- Velocity sensitivity
- 32-voice polyphony (configurable)

### Effects System
- **Reverb** - Schroeder algorithm with room size and damping controls
- **Delay** - Up to 5 seconds with feedback control
- **Chorus** - LFO-modulated delay for rich detuned sounds
- Effect chains for complex signal processing
- Per-effect bypass and wet/dry mix

### Recording
- Record master output to WAV
- Export to MP3 (with NAudio.Lame)
- Sample rate and bit depth conversion
- Real-time recording duration tracking

### Virtual Audio Channels
- Create named audio channels
- Route audio between applications via named pipes

### Scripting
- C# scripting powered by Roslyn
- Real-time code execution
- Access to all engine components
- Fluent API for common operations

---

## Getting Started

### Requirements

- **Windows 10/11** (required for audio APIs)
- **.NET 10.0** or later
- **Visual Studio 2022** or **Rider** (recommended for development)

### Installation

1. Clone the repository:
```bash
git clone https://github.com/watermann420/MusicEngine.git
cd MusicEngine
```

2. Restore dependencies:
```bash
dotnet restore
```

3. Build the project:
```bash
dotnet build
```

4. Run:
```bash
dotnet run --project MusicEngine
```

### Basic Usage Example

```csharp
// Using built-in synthesizer
var synth = CreateSynth();
synth.Waveform = WaveType.Sawtooth;

// Create a pattern with notes
var pattern = CreatePattern(synth);
pattern.LoopLength = 4.0; // 4 beats

pattern.Note(60, 0.0, 0.5, 100);   // C4 at beat 0
pattern.Note(64, 1.0, 0.5, 100);   // E4 at beat 1
pattern.Note(67, 2.0, 0.5, 100);   // G4 at beat 2
pattern.Note(72, 3.0, 0.5, 100);   // C5 at beat 3

// Start playback
SetBpm(120);
pattern.Play();
```

### General MIDI Example

```csharp
// Use Windows built-in piano sound
var piano = gm(GeneralMidiProgram.AcousticGrandPiano);

// Create a pattern
var pattern = CreatePattern(piano);
pattern.AddNote(0.0, 60, 100, 0.5);  // C4
pattern.AddNote(0.5, 64, 100, 0.5);  // E4
pattern.AddNote(1.0, 67, 100, 0.5);  // G4
pattern.AddNote(1.5, 72, 100, 1.0);  // C5
pattern.Loop = true;

// Start playback
SetBpm(120);
pattern.Play();
Start();
```

---

## Scripting API

### Available Globals

When running scripts, the following globals are available:

| Global | Type | Description |
|--------|------|-------------|
| `Engine` | `AudioEngine` | Core audio engine instance |
| `Sequencer` | `Sequencer` | Pattern sequencer |
| `Synth` | `SimpleSynth` | Default synthesizer (created on first access) |

### Creating Patterns

```csharp
// Create a pattern with the default synth
var pattern = CreatePattern();
pattern.LoopLength = 4.0;

// Add notes: Note(midiNote, beat, duration, velocity)
pattern.Note(60, 0.0, 0.25, 100);
pattern.Note(62, 0.5, 0.25, 90);
pattern.Note(64, 1.0, 0.5, 110);

// Start the pattern
pattern.Play();

// Stop the pattern
pattern.Stop();
```

### MIDI Routing

```csharp
// Route MIDI input device 0 to the synth
RouteMidi(0, Synth);

// Map MIDI CC to synth parameters
MapControl(0, 1, Synth, "cutoff");     // Mod wheel to filter cutoff
MapPitchBend(0, Synth, "pitchbend");   // Pitch bend

// Map transport controls
MapStart(0, 60);  // Note 60 starts sequencer
MapStop(0, 61);   // Note 61 stops sequencer
MapBpm(0, 7);     // CC 7 controls BPM (60-200)
```

### Sample Instruments

```csharp
// Create a sampler
var sampler = CreateSampler("DrumKit");

// Load samples to specific notes
LoadSampleToNote(sampler, "kick.wav", 36);
LoadSampleToNote(sampler, "snare.wav", 38);
LoadSampleToNote(sampler, "hihat.wav", 42);

// Route MIDI to the sampler
RouteMidi(0, sampler);

// Or use in patterns
var drumPattern = CreatePattern(sampler);
drumPattern.Note(36, 0.0, 0.25, 100);  // Kick
drumPattern.Note(42, 0.5, 0.25, 80);   // Hi-hat
drumPattern.Note(38, 1.0, 0.25, 100);  // Snare
```

### VST Plugins

```csharp
// List available VST plugins
ListVstPlugins();

// Load a VST instrument
var vst = LoadVst("Serum");

// Route MIDI to the VST
RouteToVst(0, vst);

// Or load by index
var vst2 = LoadVstByIndex(0);
```

### Effects Usage

```csharp
// Create an effect chain
var synth = CreateSynth();
var chain = EffectChain.CreateStandardChain(synth,
    includeReverb: true,
    includeDelay: true,
    includeChorus: false);

// Configure effects
var reverb = chain.GetEffect<ReverbEffect>();
reverb.Enabled = true;
reverb.RoomSize = 0.7f;
reverb.Damping = 0.5f;
reverb.Mix = 0.3f;

var delay = chain.GetEffect<DelayEffect>();
delay.Enabled = true;
delay.DelayTime = 375;  // ms (dotted eighth at 120 BPM)
delay.Feedback = 0.4f;
delay.Mix = 0.25f;

// Add the chain to the engine
Engine.AddSampleProvider(chain);
```

### Recording

```csharp
// Start recording
Engine.StartRecording("output.wav");

// Play some music...
pattern.Play();

// Stop recording
var filePath = Engine.StopRecording();
Print($"Recorded to: {filePath}");

// Export to MP3
Engine.ExportToMp3("output.wav", "output.mp3", 320);
```

### Virtual Channels

```csharp
// Create a virtual audio channel
var channel = CreateVirtualChannel("MusicEngine_Out");

// List all virtual channels
ListVirtualChannels();
```

---

## Architecture

### Core Components

```
MusicEngine/
├── Core/
│   ├── AudioEngine.cs       # Central audio routing and mixing
│   ├── Sequencer.cs         # Pattern playback and timing
│   ├── Pattern.cs           # Musical pattern with note events
│   ├── SimpleSynth.cs       # Built-in synthesizer
│   ├── SampleInstrument.cs  # Sample-based playback
│   ├── VstHost.cs           # VST plugin management
│   ├── EffectChain.cs       # Effect routing
│   ├── ReverbEffect.cs      # Schroeder reverb
│   ├── DelayEffect.cs       # Delay/echo effect
│   ├── ChorusEffect.cs      # Chorus effect
│   ├── AudioRecorder.cs     # Recording functionality
│   ├── VirtualAudioChannel.cs # Inter-app audio routing
│   ├── MidiClockSync.cs     # MIDI clock synchronization
│   └── HighResolutionTimer.cs # Precise timing
│
└── Scripting/
    ├── ScriptHost.cs        # Roslyn script execution
    ├── ScriptGlobals.cs     # Available script globals
    └── FluentApi/
        ├── AudioControl.cs   # Audio helper methods
        ├── MidiControl.cs    # MIDI helper methods
        ├── PatternControl.cs # Pattern helper methods
        ├── VstControl.cs     # VST helper methods
        └── SampleControl.cs  # Sample helper methods
```

### FluentApi Structure

The Fluent API provides convenient accessors for common operations:

```csharp
// Audio control
audio.SetMasterVolume(0.8f);

// MIDI control
midi.Route(0, synth);
midi.MapControl(0, 1, synth, "cutoff");

// Pattern control
patterns.Create(synth).Note(60, 0, 0.5, 100).Play();

// VST control
vst.Load("Serum").RouteFrom(0);

// Sample control
samples.Load("kick.wav", 36);
```

---

## Building from Source

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- Windows 10/11 (required for Windows audio APIs)
- Git

### Build Commands

```bash
# Clone the repository
git clone https://github.com/watermann420/MusicEngine.git
cd MusicEngine

# Restore NuGet packages
dotnet restore

# Build in Debug mode
dotnet build

# Build in Release mode
dotnet build -c Release

# Run the application
dotnet run --project MusicEngine

# Publish a self-contained executable
dotnet publish -c Release -r win-x64 --self-contained
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run with verbose output
dotnet test -v detailed

# Run specific test project
dotnet test MusicEngine.Tests
```

### Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| NAudio | 2.2.1 | Core audio functionality |
| NAudio.Asio | 2.2.1 | ASIO driver support |
| NAudio.Midi | 2.2.1 | MIDI device support |
| NAudio.Wasapi | 2.2.1 | WASAPI audio output |
| NAudio.WinForms | 2.2.1 | Windows Forms integration |
| NAudio.WinMM | 2.2.1 | Windows Multimedia support |
| Microsoft.CodeAnalysis.CSharp.Scripting | 5.0.0 | C# scripting via Roslyn |

---

## License

This project is licensed under the **MusicEngine License (MEL)** - an Honor-Based Commercial Support license.

Please read the [LICENSE](LICENSE) file for full details.

---

## Contributing

Contributions are welcome! Please read [CONTRIBUTING](CONTRIBUTING.md) for guidelines.

---

## Acknowledgments

- [NAudio](https://github.com/naudio/NAudio) - .NET audio library
- [Roslyn](https://github.com/dotnet/roslyn) - .NET compiler platform

---

**Music Engine** - Created by Watermann420 and Contributors
