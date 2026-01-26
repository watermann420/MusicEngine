# MusicEngine Projekt - Claude Code Kontext

## Projektübersicht
Zwei C# .NET 10 Projekte für Audio/Musik-Produktion:

### 1. MusicEngine (Engine/Library)
**Pfad:** `C:\Users\null\RiderProjects\MusicEngine`

- Audio-Engine mit 18 Synthesizern
- VST2/VST3/CLAP Plugin Hosting via VST.NET
- Sequencer mit Pattern-basierter Komposition
- MIDI Input/Output mit NAudio.Midi, MPE, MIDI 2.0
- 60+ Effects (Reverb, Delay, Chorus, Distortion, AI/ML, etc.)
- Music Theory (Notes, Chords, Scales, Arpeggiator)
- Session Management (Save/Load als JSON)
- AI/ML Features (Denoiser, Declip, MixAssistant, MasteringAssistant)

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
└── MusicEngine.Tests/          # Unit Tests (774+ Tests)
```

## Build Status (Januar 2026)
```
MusicEngine:       0 Fehler, 2 Warnungen (NetAnalyzers)
MusicEngine.Tests: 760 Tests bestanden, 14 fehlgeschlagen (vorbestehend)
MusicEngineEditor: 0 Fehler, 2 Warnungen (pre-existing)
Phase 2 Update:    73 neue Features implementiert
```

## Massive Feature Update Phase 2 (Januar 2026)
73 zusätzliche Features implementiert:

### Phase 2 Engine Features (34)
| Feature | Pfad | Beschreibung |
|---------|------|--------------|
| LoudnessNormalizerEnhanced | Effects/Dynamics/ | EBU R128, ATSC A/85 Compliance |
| BWFMetadata | AudioEncoding/ | Broadcast WAV mit iXML, bext |
| InputGainProcessor | Routing/ | Pre-insert Gain, Phase Invert |
| PadSynth | Synthesizers/ | Paul Nasca's Algorithm |
| ConvolutionAmpCab | Effects/Special/ | Guitar Amp/Cab IR Loader |
| VocalRider | Effects/Dynamics/ | Auto Gain Riding |
| AudioQuantize | Analysis/ | Snap Transients to Grid |
| ClipGainEnvelope | Clips/ | Per-Clip Gain Automation |
| FrequencyCollisionDetector | Analysis/ | Masking Detection |
| DrumSynth | Synthesizers/ | 808/909 Kick, Snare, Hi-Hat |
| StringResonator | Effects/Special/ | Sympathetic Resonance |
| EnhancedVocoder | Effects/Special/ | Formant Shifting |
| BinauralRenderer | Routing/ | HRTF 3D Audio |
| MachineControl | Sync/ | MMC/MTC External Sync |
| KarplusStrongEnhanced | Synthesizers/ | Body Resonance Modeling |
| Spectrogram3D | Analysis/ | 3D Waterfall Spectrum |
| TransferFunctionAnalyzer | Analysis/ | EQ/Compressor Curves |
| MixRadarAnalyzer | Analysis/ | Frequency Balance |
| PhaseAnalyzer | Analysis/ | Detailed Phase Analysis |
| StretchMarkerProcessor | Clips/ | Multiple Stretch Points |
| DrumToMIDI | Analysis/ | Drum Audio to MIDI |
| CompTakeProcessor | Clips/ | Advanced Comping |
| OMFExporter | Export/ | Open Media Framework |
| AutoTune | Effects/Special/ | Real-time Pitch Correction |
| RoomCorrection | Analysis/ | Acoustic Measurement |
| StemSeparation | Analysis/ | AI Source Separation |
| AAFExporter | Export/ | Advanced Authoring Format |
| MIDI2Processor | Midi/ | MIDI 2.0 Spec |
| CLAPHost | Vst/ | CLAP Plugin Format |
| AIDenoiser | Effects/AI/ | Neural Network Denoising |
| AIDeclip | Effects/AI/ | ML Clipping Restoration |
| ChordSuggestionEngine | AI/ | AI Chord Suggestions |
| MelodyGenerator | AI/ | AI Melody Completion |
| MixAssistant | AI/ | Auto EQ/Compression |
| MasteringAssistant | AI/ | One-Click AI Mastering |

### Phase 2 Editor Features (39)
| Feature | Pfad | Beschreibung |
|---------|------|--------------|
| MarkerListView | Views/ | Sortable Table with Jump-To |
| TempoListEditor | Views/ | Edit Tempo Changes as List |
| TrackSearchPanel | Controls/ | Find Tracks by Name |
| PluginSearchPanel | Controls/ | Search Plugins in Chain |
| SoloClearButton | Controls/ | One-Click Clear All Solos |
| TrackNotesPanel | Controls/ | Per-Track Text Notes |
| ColorPaletteService | Services/ | Track Color Schemes |
| ExportPresetService | Services/ | Save Export Settings |
| MixerUndoService | Services/ | Separate Mixer Undo Stack |
| RecentFilesPanel | Controls/ | Quick Access Thumbnails |
| MackieControlService | Services/ | MCU/HUI Protocol |
| OSCControlSurfaceService | Services/ | TouchOSC/Lemur Mapping |
| MIDIControlSurfaceService | Services/ | Generic MIDI CC |
| BatchProcessorDialog | Views/Dialogs/ | Multi-File Effects |
| AudioFileBrowser | Controls/ | Preview Before Import |
| PluginManagerDialog | Views/Dialogs/ | Favorites, Tags, Search |
| SlipEditControl | Controls/ | Move Under Fixed Boundaries |
| ShuffleEditService | Services/ | Ripple Edit Mode |
| GroupEditingService | Services/ | Edit Multiple Tracks |
| BackupManagerService | Services/ | Auto-Backup with Restore |
| Spectrogram3DView | Controls/ | 3D Waterfall Display |
| PhaseScopeView | Controls/ | Phase Analysis View |
| TransferFunctionView | Controls/ | EQ/Compressor Curves |
| ReferenceOverlayControl | Controls/ | Reference Spectrum Overlay |
| LoudnessGraphView | Controls/ | LUFS History |
| MixRadarView | Controls/ | Frequency Balance Radar |
| ProjectStatisticsDialog | Views/Dialogs/ | Track Count, CPU |
| ProjectCompareDialog | Views/Dialogs/ | Diff Two Versions |
| StretchMarkersControl | Controls/ | Visual Stretch Editing |
| CompTakesView | Views/ | Advanced Comping UI |
| TrackVersioningService | Services/ | Multiple Track Versions |
| TrackImportDialog | Views/Dialogs/ | Import From Projects |
| SessionNotesPanel | Controls/ | Timestamped Notes |
| KeyboardShortcutsDialog | Views/Dialogs/ | Custom Key Mappings |
| TouchOptimization | Themes/ | Large Touch Targets |
| MacroRecorderService | Services/ | Record/Playback Actions |
| LyricSyncEditor | Controls/ | Sync Lyrics to Timeline |
| TranscriptionView | Views/ | Audio to Notation |

---

## Feature Update Phase 1 (Januar 2026)
60 neue Features implementiert über Engine und Editor:

### Neue Engine Features (21)
| Feature | Pfad | Beschreibung |
|---------|------|--------------|
| DCOffsetRemoval | Effects/Restoration/ | High-pass Filter bei 5-20Hz |
| ClickRemoval | Effects/Restoration/ | Derivative Analysis + Interpolation |
| HumRemoval | Effects/Restoration/ | Adaptive Notch bei 50/60Hz |
| BreathRemoval | Effects/Restoration/ | Auto Vocal Breath Detection |
| VCAFaders | Routing/VCAGroup.cs | Linked Gain ohne Audio Routing |
| ChannelStripPresets | Presets/ | Save/Load Effect Chains als JSON |
| MPE Support | Midi/MPEProcessor.cs | Per-Note Pitch Bend, Pressure, Slide |
| ModulationMatrix | Modulation/ | Global LFO/Envelope Routing |
| SidechainMatrix | Routing/ | Flexible Sidechain Source Routing |
| MixerSnapshots | Routing/ | A/B Mixer States, Interpolation |
| SamplerSlicer | Synthesizers/ | REX-style Beat Slicing |
| ExpressionMaps | Midi/ | Keyswitch Management |
| SoundVariations | Midi/ | Articulation Switching |
| VideoTrack | Video/ | SMPTE Timecode Sync |
| ClipLauncher | Session/ | Ableton-style Session View |
| SpectralEditor | Analysis/ | FFT Frequency Editing |
| AudioAlignment | Analysis/ | VocAlign-style DTW |
| SurroundPanner | Routing/ | 5.1/7.1/Atmos VBAP |
| ModularSynth | Synthesizers/Modular/ | Patch-based Synthesis |
| VSTLinkBridge | Integration/ | DAW Inter-Operation |
| PolyphonicPitchEdit | Analysis/ | Melodyne DNA-style |

### Neue Editor Features (39)
- ArrangerTrack, TempoCurveEditor, DrumEditor, EventListEditor
- QuickControlsPanel, LoudnessReportDialog, BounceInPlaceCommand
- AutomationModes, BezierCurves, CrossfadeEditorDialog
- TrackTemplateService, CrashRecovery, MIDIMonitorPanel
- ChordTrackControl, VideoTrackControl, MultiTrackPianoRollView
- NoteExpressionLane, WorkspaceService, RenderQueueDialog, ScoreEditorView
- Plus 19 weitere QoL Features

## Code Quality Fixes (Januar 2026)
Die folgenden kritischen Probleme wurden behoben:

### Thread-Safety Fixes
1. **PolySynth.cs** - Thread-safe Random für Noise-Waveform
   - `new Random()` in Hot-Path entfernt
   - ThreadStatic Random eingeführt (keine Allocations im Audio-Thread)

2. **EffectChain.cs** - Lock-freie Read() Methode
   - Lock-Zeit drastisch reduziert
   - Copy-on-Write Pattern für Effect-Chain Reference

3. **Sequencer.cs** - Volatile Fields für Thread-Safety
   - `_running`, `_isScratching`, `_disposed` als volatile markiert

4. **OggVorbisEncoder.cs** - Thread-safe Random für Stream-ID
   - Statische Random-Instanz mit Lock

### Memory Leak & Exception Handling Fixes
5. **AudioEngine.cs** - StartInputCapture() Memory Leak Fix
   - Cleanup bei Exceptions (waveIn?.Dispose(), Handler-Dictionaries bereinigt)
   - Event-Handler werden erst nach erfolgreicher Initialisierung registriert

6. **AudioEngine.cs** - Robustes Dispose Pattern
   - `_disposed` Flag verhindert doppeltes Dispose
   - Try-Catch um alle Dispose-Aufrufe
   - Logging bei Dispose-Fehlern

7. **EffectChain.cs** - GetVstEffects() als IEnumerable
   - Keine List-Allokation mehr bei jedem Aufruf
   - yield return Pattern für lazy evaluation

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

### Effects (60+)
| Kategorie | Effects |
|-----------|---------|
| Dynamics | Compressor, MultibandCompressor, SideChain, Gate, Limiter, TransientShaper, DeEsser, DynamicEQ, SpectralGate, VocalRider |
| Time-Based | Reverb, EnhancedReverb, ConvolutionReverb, ShimmerReverb, ReverseReverb, Delay |
| Modulation | Chorus, Flanger, Phaser, Tremolo, Vibrato, AutoPan |
| Distortion | Distortion, Bitcrusher, TapeSaturation, HarmonicEnhancer |
| Filters | Filter, ParametricEQ |
| Special | Exciter, StereoWidener, Vocoder, EnhancedVocoder, RingModulator, TapeStop, PitchShifter, SubBassGenerator, Dither, SampleRateConverter, StringResonator, ConvolutionAmpCab, BinauralRenderer, AutoTune |
| Restoration | NoiseReduction, Declipping, DCOffsetRemoval, ClickRemoval, HumRemoval, BreathRemoval |
| AI/ML | AIDenoiser, AIDeclip, ChordSuggestionEngine, MelodyGenerator, MixAssistant, MasteringAssistant |

### Synthesizers (18)
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
| ModularSynth | Patch-based mit VCO/VCF/VCA/LFO/ADSR Modules |
| SamplerSlicer | REX-style Beat Slicing |
| PadSynth (NEW) | Paul Nasca's Algorithm für Evolving Pads |
| DrumSynth (NEW) | 808/909 Style Kick, Snare, Hi-Hat, Clap |
| KarplusStrongEnhanced (NEW) | Body Resonance Modeling |

### Routing & Mixing (NEW)
| Feature | Beschreibung |
|---------|--------------|
| VCAFaders | Linked Gain ohne Audio Routing |
| SidechainMatrix | Flexible Sidechain Routing |
| MixerSnapshots | A/B Compare, Interpolation, Crossfade |
| SurroundPanner | 5.1/7.1/Atmos VBAP Panning |
| ChannelStripPresets | Save/Load Effect Chains |

### Analysis (NEW)
| Feature | Beschreibung |
|---------|--------------|
| SpectralEditor | FFT-based Frequency Editing, STFT, Undo/Redo |
| AudioAlignment | VocAlign-style DTW, Formant Preservation |
| PolyphonicPitchEdit | Melodyne DNA-style, HPS Multi-Pitch Detection |

### MIDI & Expression (NEW)
| Feature | Beschreibung |
|---------|--------------|
| MPEProcessor | Per-Note Pitch Bend, Pressure, Slide |
| ModulationMatrix | Global LFO/Envelope Routing |
| ExpressionMaps | Keyswitch Management |
| SoundVariations | Articulation Switching |

### Session & Video (NEW)
| Feature | Beschreibung |
|---------|--------------|
| ClipLauncher | Ableton-style Session View, Scenes |
| VideoTrack | SMPTE Timecode Sync |
| VSTLinkBridge | DAW Inter-Operation |

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
| SampleRateConverter | Linear/Cubic/Sinc interpolation, Anti-aliasing |
| Audio Warping (NEW) | Elastic Audio mit Warp Markers, Phase Vocoder, Quantize to Grid |

### Advanced Features
| Feature | Beschreibung |
|---------|--------------|
| VST2/VST3 Hosting | Full COM Interfaces |
| PDC | Plugin Delay Compensation |
| Track Freeze | Offline Rendering |
| Send/Return Buses | Pre/Post Fader |
| Groove Templates | 16 Built-in (MPC, Shuffle, etc.) |
| Stem Export | Async with Progress |
| TempoTrack (NEW) | Tempo Changes, Linear/S-Curve Ramps |
| TimeSignatureTrack (NEW) | Mixed Meters, Bar/Beat Calculation |
| TrackGroups (NEW) | Nested Folders, Solo/Mute Propagation |
| TakeLanes (NEW) | Comping, Multi-Take, Crossfades |
| PunchRecording (NEW) | Punch In/Out, Pre/Post-Roll |
| AudioPool (NEW) | Media Management, BPM/Key Detection |
| ReferenceTrack (NEW) | A/B Comparison, LUFS Matching |
| MacroControls (NEW) | 8 Macros, Parameter Mapping, MIDI Learn |

### Network & Collaboration (NEW)
| Feature | Beschreibung |
|---------|--------------|
| LinkSync | Ableton Link-style Tempo Sync (UDP Multicast) |
| NetworkMIDI (NEW) | RTP-MIDI Style, TCP/UDP, Peer Discovery, Latency Compensation |
| CloudStorage (NEW) | Provider Abstraction, Auto-Sync, Offline Queue, Conflict Resolution |
| Collaboration (NEW) | Real-time Multi-User, OT Algorithm, TCP Server/Client, Vector Clocks |

### Editor UI Features (Januar 2026)
| Feature | Beschreibung |
|---------|--------------|
| Note Velocity Colors | Blue→Green→Red Gradient basierend auf Velocity (0-127) |
| Grid Customization | Triplet (1/4T, 1/8T, 1/16T) und Dotted (1/4., 1/8., 1/16.) mit visueller Unterscheidung |
| Command Palette | Ctrl+P für schnellen Zugriff auf alle Befehle (Fuzzy Search) |
| Drag & Drop Audio | Audio-Dateien direkt in Arrangement ziehen (WAV, MP3, FLAC, OGG, AIFF) |
| LUFS Meter | Integrated/Short-term/Momentary Loudness + True Peak im Mixer |
| Track Group Headers | Nested Folders, Solo/Mute Propagation, Volume, Collapse |
| Take Lane Control | Comping UI, Multi-Take Recording, Rating Stars, Flatten |
| Punch Locator | Punch In/Out Markers, Pre/Post-Roll Settings, Visual Feedback |
| Audio Pool View | Media Browser, Search/Tags, BPM/Key Detection, Consolidate |
| Reference Track | A/B Vergleich, LUFS Level Matching, Loop Region, Transport Sync |
| Macro Bank | 8 Macro Knobs, Parameter Mapping, Curves, MIDI Learn |
| Time Signature Marker | Meter Changes auf Timeline, Custom Time Signatures |
| Warp Marker Control (NEW) | Elastic Audio Editor, Draggable Warp Markers, Auto-Warp |
| Elastic Audio Editor (NEW) | BPM Detection, Tempo Matching, Waveform Warping UI |

### Project Management Features (Januar 2026)
| Feature | Beschreibung |
|---------|--------------|
| TempoTrack | Tempo-Änderungen über Zeit, Linear/S-Curve Ramping, Beat↔Time Konvertierung |
| TimeSignatureTrack | Taktart-Änderungen, Bar/Beat Berechnung, Mixed Meters Support |
| TrackGroups | Hierarchische Track-Organisation, Solo/Mute Vererbung, Gruppen-Volume |
| TakeLanes | Multi-Take Recording, Comp Regions, Crossfades, Flatten to AudioClip |
| PunchRecording | Punch In/Out Points, Pre-Roll/Post-Roll, Auto-Punch Mode |
| AudioPool | Media Management, Usage Tracking, Consolidate, BPM/Key Analysis |
| ReferenceTrack | A/B Comparison, LUFS Analysis, Auto Level Matching, Loop/Sync |
| MacroControls | 8 Macros mit Multi-Parameter Mapping, Curves, MIDI Learn |

---

## Synthesizer Dokumentation

Jeder Synthesizer hat eine eigene README-Datei mit vollständiger Dokumentation:

| Synth | README | Inhalt |
|-------|--------|--------|
| SimpleSynth | `README_SimpleSynth.md` | Monophonic, ADSR, 5 Waveforms |
| PolySynth | `README_PolySynth.md` | 16-Voice, Voice Stealing Modes |
| AdvancedSynth | `README_AdvancedSynth.md` | Multi-Oscillator, 5 Filter Types |
| FMSynth | `README_FMSynth.md` | 6-Operator, 20 Algorithmen |
| AdditiveSynth | `README_AdditiveSynth.md` | 64 Partials, Hammond Drawbars |
| WavetableSynth | `README_WavetableSynth.md` | Wavetable Morphing, Custom Loading |
| GranularSynth | `README_GranularSynth.md` | Grain Synthesis, 5 Envelope Shapes |
| SampleSynth | `README_SampleSynth.md` | Multi-Sample, Velocity Layers |
| SpeechSynth | `README_SpeechSynth.md` | Formant/TTS, Singing Mode |
| SupersawSynth | `README_SupersawSynth.md` | JP-8000 Style, 1-16 Unison |
| VectorSynth | `README_VectorSynth.md` | XY Pad, 4 Oscillators |
| PhysicalModeling | `README_PhysicalModeling.md` | Karplus-Strong, Waveguide |
| NoiseGenerator | `README_NoiseGenerator.md` | 5 Noise Colors, Filtering |

Jede README enthält:
- Parameter-Tabellen mit Types, Ranges, Defaults
- C# Code-Beispiele
- Signal Flow Diagramme
- Sound Design Tipps
- Factory Presets

---

## Session History

Für detaillierte Session-Historie und Implementierungsdetails siehe:
`MusicEngine/CLAUDE_CONTEXT_HISTORY.md`
