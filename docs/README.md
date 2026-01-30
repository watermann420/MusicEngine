

![Banner](https://github.com/user-attachments/assets/382f24fd-e758-454f-b4a2-f442bd4da2b2)

# Music Engine

![License](https://img.shields.io/badge/license-MEL-blue)
![C#](https://img.shields.io/badge/language-C%23-blue)
![.NET](https://img.shields.io/badge/.NET-10.0-purple)
![Status](https://img.shields.io/badge/status-Work_in_Progress-orange)
![Effects](https://img.shields.io/badge/effects-1000+-blue)
![Synths](https://img.shields.io/badge/synthesizers-45+-blue)

**Music Engine** is a modular, open-source live-coding music engine written in C#. It combines code, MIDI, patterns, and real-time audio to enable flexible music production, live performance, and interactive audio programming.


##Music Engine & Editor – Technical Preview
https://youtu.be/t9kZqpMVdsQ


## Project Status (January 2026)

| Component | Status | Details |
|-----------|--------|---------|
| **MusicEngine** | 50% Complete | 1000+ effects, 45+ synthesizers, VST2/VST3/CLAP hosting |
| **MusicEngineEditor** | 50% Complete | 215+ UI features, full WPF desktop application |




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

- **45+ Synthesizers** including FM, Granular, Wavetable, Physical Modeling, Additive, Modular, and more
- **1000+ Effects** across dynamics, time-based, modulation, distortion, restoration, and AI/ML categories
- **VST2/VST3/CLAP plugin hosting** with full COM interfaces and Plugin Delay Compensation
- **128 General MIDI instruments** via Windows built-in synthesizer
- Pattern-based sequencing with Arrangement, AudioClip, MidiClip support
- Advanced MIDI features: MPE, MIDI 2.0, Expression Maps, Modulation Matrix
- **Audio routing system** with Send/Return buses, VCA faders, Surround (5.1/7.1/Atmos)
- **AI/ML Features**: Neural denoising, declipping, mix assistant, mastering assistant, stem separation
- Multi-format export (WAV, MP3, FLAC, OGG, AIFF, BWF, OMF, AAF)
- Network features: Ableton Link sync, Network MIDI, Cloud Storage, Real-time Collaboration
- C# scripting via Roslyn for live coding
- High-precision timing with MIDI clock synchronization

## The Music Engine Editor

A complete WPF desktop application with 215+ features including:
- Arrangement View with Audio/MIDI clips, markers, regions
- Piano Roll with MIDI CC lanes, velocity colors, triplet/dotted grid
- Mixer with VST effects, LUFS loudness metering
- Analysis visualizers (Spectrum, Goniometer, 3D Spectrogram, Phase Scope)
- Score Editor, Drum Editor, Event List Editor
- Command Palette (Ctrl+P), Workspaces, Macro Recorder

Git: https://github.com/watermann420/MusicEngineEditor

---

## Features

### Synthesizers (45+)

| Category | Synthesizers |
|----------|--------------|
| **Basic** | SimpleSynth (Mono), PolySynth (16-voice), AdvancedSynth (Multi-oscillator) |
| **FM/Additive** | FMSynth (6-operator, 20 algorithms), AdditiveSynth (64 partials, Hammond drawbars) |
| **Wavetable/Vector** | WavetableSynth (morphing), VectorSynth (XY crossfade) |
| **Granular** | GranularSynth (5 envelope shapes), SamplerSlicer (REX-style) |
| **Physical** | PhysicalModeling (Karplus-Strong), KarplusStrongEnhanced (body resonance) |
| **Sample-based** | SampleSynth (velocity layers, round-robin), SamplePlayer |
| **Specialty** | SpeechSynth (formant/TTS), SupersawSynth (JP-8000), NoiseGenerator (5 colors) |
| **Modular** | ModularSynth (VCO/VCF/VCA/LFO/ADSR modules with patch cables) |
| **Pad/Ambient** | PadSynth (Paul Nasca's algorithm) |
| **Drums** | DrumSynth (808/909 kick, snare, hi-hat, clap) |
| **Retro** | ChipTuneSynth (NES/GameBoy/C64) |
| **Other** | WavefolderSynth, SubtractiveSynth, BellSynth, OrganSynth, StringSynth, LeadSynth, BassSynth, PluckSynth |

### Effects (1000+)

| Category | Effects |
|----------|---------|
| **Dynamics** | Compressor, MultibandCompressor, SideChainCompressor, SideChainDucker, Gate, Limiter, TransientShaper, TransientDesigner, DeEsser, DynamicEQ, SpectralGate, VocalRider |
| **Time-Based** | Reverb, EnhancedReverb, ConvolutionReverb, ShimmerReverb, ReverseReverb, FreezeReverb, Delay, EnhancedDelay, DualDelay, GranularDelay, PolyrhythmicDelay, MultiTapDelay |
| **Modulation** | Chorus, EnhancedChorus, Flanger, Phaser, Tremolo, Vibrato, AutoPan, RingModulator |
| **Distortion** | Distortion, Bitcrusher, Saturator, TapeSaturation, TapeEmulation, VinylEmulation, HarmonicEnhancer, WavefolderSynth |
| **Filters/EQ** | Filter, ParametricEQ, DynamicEQ, ChannelEQ |
| **Pitch/Time** | PitchShifter, PitchCorrector, Harmonizer, TimeStretch, AudioMorpher, TapeStop |
| **Stereo/Spatial** | StereoWidener, StereoImager, MonoMaker, SurroundPanner, BinauralRenderer (HRTF 3D) |
| **Special** | Exciter, Vocoder, EnhancedVocoder, SubBassGenerator, StringResonator, Dither, SampleRateConverter |
| **Amp/Cab** | AmpSimulator, CabinetSimulator, ConvolutionAmpCab (IR loader) |
| **Restoration** | NoiseReduction, Declipping, DCOffsetRemoval, ClickRemoval, HumRemoval, BreathRemoval |
| **AI/ML** | AIDenoiser (neural network), AIDeclip (ML-based), AutoTune, RoomCorrection |
| **Performance** | BeatRepeat, SpectralFreeze, GlitchMachine, PhaseRotator |

### Audio Engine
- Built on NAudio 2.2.1 for robust Windows audio support
- DirectSound / WASAPI / ASIO audio output
- Multithreaded architecture with thread-safe design
- Plugin Delay Compensation (PDC)
- Track Freeze/Bounce for CPU optimization
- Multi-format export (WAV, MP3, FLAC, OGG, AIFF, BWF, OMF, AAF)

### MIDI Features
- Full MIDI device enumeration and routing
- **MPE Support** - Per-note pitch bend, pressure, slide
- **MIDI 2.0** - Full specification support
- Expression Maps and Sound Variations for orchestral libraries
- Modulation Matrix with global LFO/Envelope routing
- MIDI Effects: Delay, Arpeggiator, Chord, Randomizer, Echo
- Extremely low latency (~0.5 ms)

### Sequencing
- Pattern-based composition with looping
- **Arrangement** with AudioClip, MidiClip, Region support
- Step Sequencer (drum machine style, multi-row)
- Probability Sequencer (per-step probability, ratchet)
- Euclidean Rhythm (Bjorklund algorithm, 17 presets)
- Humanizer (timing/velocity randomization)
- Clip Launcher (Ableton-style session view)
- Tempo Track (tempo automation with curves)
- Time Signature Track (mixed meters)

### Plugin Hosting
- **VST2** (.dll) full support
- **VST3** (.vst3) with complete COM interfaces
- **CLAP** plugin format support
- Automatic plugin scanning with SafeScanMode
- Plugin Delay Compensation
- Parameter automation

### Routing & Mixing
- Send/Return Bus architecture
- VCA Faders (linked gain without audio routing)
- Sidechain Matrix (flexible routing)
- Mixer Snapshots (A/B comparison, interpolation)
- Surround Panning (5.1, 7.1, Atmos VBAP)
- Channel Strip Presets

### Analysis
- Spectrum Analyzer (31-band FFT)
- Correlation Meter, Goniometer
- True Peak (ITU-R BS.1770)
- Loudness Meter (LUFS integrated/short-term/momentary)
- Tempo/Transient/Chord/Key Detection
- Spectrogram 3D (waterfall display)
- Mix Radar Analyzer, Phase Analyzer
- Audio-to-MIDI, Drum-to-MIDI
- Spectral Editor (FFT-based frequency editing)
- Polyphonic Pitch Edit (Melodyne DNA-style)

### AI/ML Features
- **AIDenoiser** - Neural network noise reduction
- **AIDeclip** - ML-based clipping restoration
- **ChordSuggestion** - AI chord suggestions
- **MelodyGenerator** - AI melody completion
- **MixAssistant** - Auto EQ, compression suggestions
- **MasteringAssistant** - One-click AI mastering
- **StemSeparation** - AI-based source separation
- **AutoTune** - Real-time pitch correction
- **RoomCorrection** - Acoustic measurement/correction

### Network & Collaboration
- **LinkSync** - Ableton Link-style tempo sync (UDP multicast)
- **NetworkMIDI** - RTP-MIDI style with peer discovery
- **CloudStorage** - Provider abstraction, auto-sync, offline queue
- **Collaboration** - Real-time multi-user editing with OT algorithm

### Project Management
- TempoTrack, TimeSignatureTrack
- Track Groups (nested folders)
- Take Lanes (comping, multi-take recording)
- Punch Recording (in/out, pre/post-roll)
- Audio Pool (media management)
- Reference Track (A/B comparison)
- Macro Controls (8 assignable with MIDI learn)

### Recording & Export
- Record master output to multiple formats
- Stem Export with progress tracking
- Loudness Normalizer (EBU R128, ATSC A/85)
- BWF Metadata (iXML, bext chunks, SMPTE timecode)
- OMF/AAF/EDL Export for DAW interchange

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
│   ├── AudioEngine.cs           # Central audio routing and mixing (with PDC)
│   ├── Sequencer.cs             # Pattern playback, Arrangement integration
│   ├── Arrangement.cs           # Timeline with AudioClip, MidiClip, Region
│   ├── Pattern.cs               # Note events container
│   │
│   ├── Synthesizers/
│   │   ├── SimpleSynth.cs       # Monophonic synthesizer
│   │   ├── PolySynth.cs         # Polyphonic with voice stealing
│   │   ├── FMSynth.cs           # FM synthesis (6 operators)
│   │   ├── GranularSynth.cs     # Granular synthesis
│   │   ├── WavetableSynth.cs    # Wavetable synthesis
│   │   ├── AdvancedSynth.cs     # Multi-oscillator synth
│   │   ├── PhysicalModeling.cs  # Karplus-Strong, waveguide
│   │   ├── SampleSynth.cs       # Multi-sample, velocity layers
│   │   ├── SpeechSynth.cs       # Formant synthesis, TTS
│   │   ├── SupersawSynth.cs     # JP-8000 style supersaw
│   │   ├── AdditiveSynth.cs     # Hammond drawbars
│   │   ├── ModularSynth.cs      # Patch-based synthesis
│   │   ├── PadSynth.cs          # Paul Nasca's algorithm
│   │   ├── DrumSynth.cs         # 808/909 style drums
│   │   └── ChipTuneSynth.cs     # Retro 8-bit sounds
│   │
│   ├── Effects/
│   │   ├── Dynamics/            # Compressor, Gate, Limiter, etc.
│   │   ├── TimeBased/           # Reverb, Delay, etc.
│   │   ├── Modulation/          # Chorus, Flanger, Phaser, etc.
│   │   ├── Distortion/          # Distortion, Bitcrusher, etc.
│   │   ├── Filters/             # Filter, ParametricEQ
│   │   ├── Special/             # Vocoder, StereoWidener, etc.
│   │   ├── Restoration/         # NoiseReduction, Declipping, etc.
│   │   └── AI/                  # AIDenoiser, AIDeclip, etc.
│   │
│   ├── Sequencing/
│   │   ├── StepSequencer.cs     # Drum machine style
│   │   ├── ProbabilitySequencer.cs
│   │   ├── EuclideanRhythm.cs
│   │   └── Humanizer.cs
│   │
│   ├── Vst/                     # VST2/VST3/CLAP plugin hosting
│   ├── Analysis/                # Spectrum, Tempo, Chord detection
│   ├── AudioEncoding/           # FLAC, OGG, AIFF, BWF encoders
│   ├── PDC/                     # Plugin Delay Compensation
│   ├── Freeze/                  # Track Freeze/Bounce
│   ├── Groove/                  # Groove Extraction & Templates
│   ├── Routing/                 # Send/Return Buses, VCA, Surround
│   ├── Midi/                    # MPE, MIDI 2.0, Expression Maps
│   ├── AI/                      # ML-based features
│   └── Network/                 # Link, CloudStorage, Collaboration
│
├── Infrastructure/
│   ├── Logging/                 # Serilog configuration
│   ├── DependencyInjection/     # IoC container setup
│   ├── Configuration/           # appsettings.json
│   └── Memory/                  # AudioBufferPool
│
├── Scripting/
│   ├── ScriptHost.cs            # Roslyn script execution
│   └── FluentApi/               # Convenient API accessors
│
└── MusicEngine.Tests/           # Unit tests (774+ tests)
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
| VST.NET | - | VST2/VST3 plugin hosting |
| Microsoft.CodeAnalysis.CSharp.Scripting | 5.0.0 | C# scripting via Roslyn |
| Microsoft.Extensions.Logging | - | Logging abstraction |
| Microsoft.Extensions.DependencyInjection | - | Dependency injection |
| Serilog | - | Structured logging |
| xUnit | 2.9.0 | Unit testing framework |
| FluentAssertions | 6.12.0 | Test assertions |
| Moq | 4.20.72 | Mocking framework |

---



## Documentation

### Synthesizer Documentation

Each synthesizer has comprehensive documentation in `docs/`:

| Synth | Documentation |
|-------|---------------|
| SimpleSynth | [README_SimpleSynth.md](README_SimpleSynth.md) |
| PolySynth | [README_PolySynth.md](README_PolySynth.md) |
| AdvancedSynth | [README_AdvancedSynth.md](README_AdvancedSynth.md) |
| FMSynth | [README_FMSynth.md](README_FMSynth.md) |
| AdditiveSynth | [README_AdditiveSynth.md](README_AdditiveSynth.md) |
| WavetableSynth | [README_WavetableSynth.md](README_WavetableSynth.md) |
| GranularSynth | [README_GranularSynth.md](README_GranularSynth.md) |
| SampleSynth | [README_SampleSynth.md](README_SampleSynth.md) |
| SpeechSynth | [README_SpeechSynth.md](README_SpeechSynth.md) |
| SupersawSynth | [README_SupersawSynth.md](README_SupersawSynth.md) |
| VectorSynth | [README_VectorSynth.md](README_VectorSynth.md) |
| PhysicalModeling | [README_PhysicalModeling.md](README_PhysicalModeling.md) |
| NoiseGenerator | [README_NoiseGenerator.md](README_NoiseGenerator.md) |

### Additional Documentation

| Document | Description |
|----------|-------------|
| [API.md](API.md) | Complete API reference |
| [SCRIPTING_API.md](SCRIPTING_API.md) | Scripting documentation |
| [EFFECTS_REFERENCE.md](EFFECTS_REFERENCE.md) | Effects parameter reference |
| [AUDIO_ROUTING.md](AUDIO_ROUTING.md) | Routing system guide |
| [MODULATION_SYSTEM.md](MODULATION_SYSTEM.md) | Modulation matrix guide |
| [GENERAL_MIDI.md](GENERAL_MIDI.md) | General MIDI reference |
| [ALIAS_REFERENCE.md](ALIAS_REFERENCE.md) | Function alias list |

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
- [VST.NET](https://github.com/obiwanjacobi/vst.net) - VST plugin hosting

---

**Music Engine** - Created by Watermann420 and Contributors
