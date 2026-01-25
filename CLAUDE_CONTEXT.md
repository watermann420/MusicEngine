# MusicEngine Projekt - Claude Code Kontext

## Projektübersicht
Zwei C# .NET 10 Projekte für Audio/Musik-Produktion:

### 1. MusicEngine (Engine/Library)
**Pfad:** `C:\Users\null\RiderProjects\MusicEngine`

- Audio-Engine mit 13 Synthesizern
- VST2/VST3 Plugin Hosting via VST.NET
- Sequencer mit Pattern-basierter Komposition
- MIDI Input/Output mit NAudio.Midi
- 45+ Effects (Reverb, Delay, Chorus, Distortion, etc.)
- Music Theory (Notes, Chords, Scales, Arpeggiator)
- Session Management (Save/Load als JSON)

### 2. MusicEngineEditor (Desktop App)
**Pfad:** `C:\Users\null\RiderProjects\MusicEditor\MusicEngineEditor`

- WPF Desktop-Anwendung
- Code-Editor mit Roslyn-Integration für Live-Coding
- MVVM Pattern mit CommunityToolkit.Mvvm
- Referenziert MusicEngine als Projekt-Dependency

## Technologie-Stack
| Komponente | Technologie |
|------------|-------------|
| Framework | .NET 10, C# 13 |
| UI | WPF (Windows only) |
| Audio | NAudio 2.2.1 |
| VST Hosting | VST.NET |
| Testing | xUnit 2.9.0, FluentAssertions 6.12.0, Moq 4.20.72 |
| Logging | Microsoft.Extensions.Logging + Serilog |
| DI | Microsoft.Extensions.DependencyInjection |
| Config | Microsoft.Extensions.Configuration.Json |
| MVVM | CommunityToolkit.Mvvm |

## Projektstruktur

```
MusicEngine/
├── Core/
│   ├── AudioEngine.cs          # Haupt-Audio-Engine mit Mixer & PDC
│   ├── Sequencer.cs            # Pattern-Sequencing, BPM, Transport
│   ├── Arrangement.cs          # Timeline mit AudioClip, MidiClip, Region
│   ├── Pattern.cs              # Note Events Container
│   │
│   ├── Synthesizers/
│   │   ├── SimpleSynth.cs       # Monophoner Synthesizer
│   │   ├── PolySynth.cs         # Polyphoner Synthesizer mit Voice Stealing
│   │   ├── FMSynth.cs           # FM Synthesis (6 Operators)
│   │   ├── GranularSynth.cs     # Granular Synthesis
│   │   ├── WavetableSynth.cs    # Wavetable Synthesis
│   │   ├── AdvancedSynth.cs     # Multi-Oscillator Synth
│   │   ├── PhysicalModeling.cs  # Karplus-Strong, Waveguide
│   │   ├── SampleSynth.cs       # Multi-Sample, Velocity Layers, Round-Robin
│   │   ├── SpeechSynth.cs       # Formant Synthesis, TTS, Singing Mode
│   │   ├── SupersawSynth.cs     # JP-8000 Style Supersaw, 1-16 Unison
│   │   ├── AdditiveSynth.cs     # Harmonic Series, Hammond Drawbars
│   │   ├── VectorSynth.cs       # XY Crossfading zwischen 4 Oscillators
│   │   └── NoiseGenerator.cs    # White, Pink, Brown, Blue, Violet Noise
│   │
│   ├── Effects/
│   │   ├── Dynamics/           # Compressor, MultibandCompressor, Gate, Limiter, etc.
│   │   ├── TimeBased/          # Reverb, ConvolutionReverb, Delay, etc.
│   │   ├── Modulation/         # Chorus, Flanger, Phaser, Tremolo, Vibrato
│   │   ├── Distortion/         # Distortion, Bitcrusher, TapeSaturation, HarmonicEnhancer
│   │   ├── Filters/            # Filter, ParametricEQ
│   │   ├── Special/            # Exciter, StereoWidener, Vocoder, SubBassGenerator, Dither
│   │   └── EffectBase.cs       # Base class for effects
│   │
│   ├── Sequencing/
│   │   ├── StepSequencer.cs     # Drum Machine Style, Multi-Row
│   │   ├── ProbabilitySequencer.cs # Per-Step Probability, Ratchet
│   │   ├── EuclideanRhythm.cs   # Bjorklund Algorithm
│   │   └── Humanizer.cs         # Timing/Velocity Randomization
│   │
│   ├── Vst/                    # VST2/VST3 Plugin Hosting
│   ├── Analysis/               # Spectrum, Correlation, TruePeak, TempoDetection
│   ├── AudioEncoding/          # FLAC, OGG, AIFF Encoders
│   ├── PDC/                    # Plugin Delay Compensation
│   ├── Freeze/                 # Track Freeze/Bounce
│   ├── Groove/                 # Groove Extraction & Templates (16 Built-in)
│   ├── Routing/                # Send/Return Buses
│   ├── UndoRedo/               # Command Pattern
│   └── MusicTheory/            # Note, Chord, Scale, Arpeggiator
│
├── Infrastructure/
│   ├── Logging/                # Serilog Configuration
│   ├── DependencyInjection/    # IAudioEngine, ISequencer, IVstHost
│   ├── Configuration/          # MusicEngineOptions, appsettings.json
│   └── Memory/                 # AudioBufferPool
│
└── MusicEngine.Tests/          # Unit Tests (656+ Tests)
```

## Build Status (Januar 2026)
```
MusicEngine:       0 Fehler, 2 Warnungen (NetAnalyzers Version)
MusicEngine.Tests: 656 Tests bestanden, 6 fehlgeschlagen (vorbestehend)
MusicEngineEditor: 0 Fehler, 0 Warnungen
```

## Wichtige Konventionen

### Code Style
- File-scoped namespaces (`namespace MusicEngine.Core;`)
- Deutsche Commit Messages sind OK
- Keine Emojis in Code/Kommentaren

### Bekannte Workarounds
- NAudio.Midi: `NoteOnEvent` mit Velocity 0 für Note-Off
- WPF Shapes: `using Shapes = System.Windows.Shapes;` wegen Konflikten
- MIDI Export: Eigene WriteMidiFile() Methode (MidiFile.Export existiert nicht)
- VST3 SafeScanMode: `VstHost.SafeScanMode = true` (default) verhindert AccessViolationException

### Build Commands (Git Bash)
```bash
# Build Engine
"/c/Program Files/dotnet/dotnet.exe" build "C:/Users/null/RiderProjects/MusicEngine/MusicEngine.csproj"

# Build Editor
"/c/Program Files/dotnet/dotnet.exe" build "C:/Users/null/RiderProjects/MusicEditor/MusicEngineEditor/MusicEngineEditor.csproj"

# Run Tests
"/c/Program Files/dotnet/dotnet.exe" test "C:/Users/null/RiderProjects/MusicEngine/MusicEngine.Tests/MusicEngine.Tests.csproj"
```

## Beispiel-Nutzung der Engine

```csharp
// Audio Engine erstellen
var engine = new AudioEngine();

// Synth hinzufügen
var synth = new PolySynth();
synth.Waveform = WaveType.Sawtooth;
synth.Attack = 0.01;
synth.Release = 0.3;
engine.AddChannel(synth);

// Pattern erstellen
var pattern = new Pattern("Bass", 4.0);
pattern.Note(Note.FromString("C3"), 0.0, 0.5, 100);
pattern.Note(Note.FromString("E3"), 0.5, 0.5, 100);
pattern.Note(Note.FromString("G3"), 1.0, 0.5, 100);

// Sequencer starten
var sequencer = new Sequencer(engine);
sequencer.BPM = 120;
sequencer.AddPattern(pattern, synth);
sequencer.Start();
```

## Implementierte Features (Komplett)

### Synthesizers (13)
| Synth | Beschreibung |
|-------|--------------|
| SimpleSynth | Monophonic mit ADSR |
| PolySynth | Polyphonic mit Voice Stealing |
| FMSynth | 6-Operator FM |
| GranularSynth | Granular Synthesis |
| WavetableSynth | Wavetable mit Morphing |
| AdvancedSynth | Multi-Oscillator |
| PhysicalModeling | Karplus-Strong, Waveguide |
| SampleSynth | Multi-Sample, Velocity, Loop |
| SpeechSynth | Formant, TTS, Singing |
| SupersawSynth | JP-8000 Style, Unison |
| AdditiveSynth | Hammond Drawbars |
| VectorSynth | XY Pad Crossfade |
| NoiseGenerator | 5 Noise Types |

### Effects (45+)
| Kategorie | Effects |
|-----------|---------|
| Dynamics | Compressor, MultibandCompressor, SideChain, Gate, Limiter, TransientShaper, DeEsser, DynamicEQ, SpectralGate |
| Time-Based | Reverb, EnhancedReverb, ConvolutionReverb, ShimmerReverb, ReverseReverb, Delay |
| Modulation | Chorus, Flanger, Phaser, Tremolo, Vibrato, AutoPan |
| Distortion | Distortion, Bitcrusher, TapeSaturation, HarmonicEnhancer (NEW) |
| Filters | Filter, ParametricEQ |
| Special | Exciter, StereoWidener, Vocoder, RingModulator, TapeStop, PitchShifter, SubBassGenerator (NEW), Dither (NEW) |

### MIDI & Sequencing
| Feature | Beschreibung |
|---------|--------------|
| StepSequencer | Drum Machine Style, Multi-Row, Direction Modes |
| ProbabilitySequencer | Per-Step Probability, Ratchet, Conditions |
| EuclideanRhythm | Bjorklund Algorithm, 17 Presets |
| Humanizer | Timing/Velocity Randomization |
| ScaleQuantizer | Force MIDI to Scale |
| MidiEffects | MidiDelay, MidiArpeggiator, MidiChord |

### Audio Processing
| Feature | Beschreibung |
|---------|--------------|
| TimeStretch | Phase Vocoder |
| PitchShifter | Phase Vocoder |
| Audio-to-MIDI | YIN Algorithm |
| ChordDetection | Real-time |
| KeyDetection | Krumhansl-Schmuckler |
| Dithering | RPDF/TPDF/HP-TPDF, Noise Shaping |

### Advanced Features
| Feature | Beschreibung |
|---------|--------------|
| VST2/VST3 Hosting | Full COM Interfaces |
| PDC | Plugin Delay Compensation |
| Track Freeze | Offline Rendering |
| Send/Return Buses | Pre/Post Fader |
| Groove Templates | 16 Built-in (MPC, Shuffle, etc.) |
| Stem Export | Async with Progress |

---

## Session History

Für detaillierte Session-Historie und Implementierungsdetails siehe:
`MusicEngine/CLAUDE_CONTEXT_HISTORY.md`
