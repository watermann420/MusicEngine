# MusicEngine Projekt - Claude Code Kontext

## Projektübersicht
Zwei C# .NET 10 Projekte für Audio/Musik-Produktion:

### 1. MusicEngine (Engine/Library)
**Pfad:** `C:\Users\null\RiderProjects\MusicEngine`

- Audio-Engine mit Synthesizern (SimpleSynth, PolySynth, SFZ Sampler)
- VST2/VST3 Plugin Hosting via VST.NET
- Sequencer mit Pattern-basierter Komposition
- MIDI Input/Output mit NAudio.Midi
- Effects (Reverb, Delay, Chorus, Distortion, Flanger, Phaser, etc.)
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
│   ├── AudioEngine.cs          # Haupt-Audio-Engine mit Mixer
│   ├── Sequencer.cs            # Pattern-Sequencing, BPM, Transport
│   ├── Pattern.cs              # Note Events Container
│   ├── SimpleSynth.cs          # Monophoner Synthesizer
│   ├── PolySynth.cs            # Polyphoner Synthesizer mit Voice Stealing
│   ├── SfzSampler.cs           # SFZ Sample Player
│   ├── VstHost.cs              # VST Plugin Management
│   ├── VstPlugin.cs            # VST2 Plugin Wrapper
│   ├── Vst3Plugin.cs           # VST3 Plugin Wrapper
│   ├── MidiExporter.cs         # MIDI File Export (.mid)
│   ├── PatternTransform.cs     # Scale-Lock, Humanization, Groove
│   ├── Session.cs              # Project Save/Load
│   ├── Settings.cs             # Global Settings
│   ├── Guard.cs                # Argument Validation
│   ├── MidiValidation.cs       # MIDI Value Validation
│   ├── MusicTheory/
│   │   ├── Note.cs             # Note representation
│   │   ├── Chord.cs            # Chord types and inversions
│   │   ├── Scale.cs            # Scale types
│   │   └── Arpeggiator.cs      # Arpeggio patterns
│   └── Effects/
│       ├── EffectBase.cs       # Base class for effects
│       ├── Reverb.cs, Delay.cs, Chorus.cs, etc.
├── Infrastructure/
│   ├── Logging/                # Serilog Configuration
│   ├── DependencyInjection/    # DI Setup, Interfaces
│   └── Configuration/          # Options, Config Management
├── MusicEngine.Tests/          # Unit Tests (136 Tests)
└── appsettings.json            # Configuration
```

```
MusicEngineEditor/
├── Views/
│   └── MixerView.xaml/.cs      # Mixer Panel
├── ViewModels/
│   └── MixerViewModel.cs       # Mixer ViewModel
├── Models/
│   ├── CodeSnippet.cs          # Code Snippet Model
│   └── MixerChannel.cs         # Mixer Channel Model
├── Controls/
│   ├── LevelMeter.xaml/.cs     # VU/Peak Meter Control
│   └── MixerChannelControl.xaml/.cs  # Single Channel Strip
├── Services/
│   └── SnippetService.cs       # Code Snippets (12 built-in)
└── Themes/                     # WPF Styles
```

## Abgeschlossene Features

### Enterprise Infrastructure (Phase 1-4)
- [x] Logging mit Serilog (Console + File Sinks)
- [x] Dependency Injection mit Microsoft.Extensions.DI
- [x] Configuration mit appsettings.json + Hot Reload
- [x] Unit Testing mit xUnit (136 Tests bestehen)
- [x] Code Quality mit Guard.cs, MidiValidation.cs
- [x] API Events (ChannelAdded, PluginLoaded, etc.)

### Quick Wins Features
- [x] **MIDI File Export** - `MidiExporter.cs`
  - ExportPattern(), ExportPatterns(), ExportSession()
  - Standard MIDI File Type 1, 480 PPQN

- [x] **Pattern Transforms** - `PatternTransform.cs`
  - Scale-Lock: QuantizeToScale(pattern, scale, root)
  - Humanization: Humanize(pattern, options)
  - Groove: ApplySwing(), ApplyGroove(MPC, Ableton, Logic, Vintage)
  - Transform: Transpose, Reverse, Invert

- [x] **VU/Peak Meter** - `LevelMeter.xaml/.cs`
  - Stereo VU meter with peak hold
  - Clip indicators
  - Vertical/Horizontal orientation

- [x] **Code Snippets** - `SnippetService.cs`
  - 12 built-in snippets (syn, poly, pat, arp, fx, midi, drum, loop, etc.)
  - Placeholder support ($CURSOR$, $1$, $2$)

- [x] **Mixer View** - `MixerView.xaml/.cs`, `MixerChannelControl.xaml/.cs`
  - Professionelles Mixer UI mit Fader, Pan, M/S/R Buttons
  - Integration mit LevelMeter für VU-Anzeige
  - 8 Standard-Channels + Master
  - MixerChannel Model und MixerViewModel

## Build Status
```
MusicEngine:       0 Fehler, 0 Warnungen
MusicEngineEditor: 0 Fehler, 0 Warnungen
Tests:             136/136 bestanden
```

## Offene Features (Optional)

### Mittel Priorität
- [ ] Async Operations (InitializeAsync, ScanForPluginsAsync)
- [ ] Extension System (ISynthExtension, IEffectExtension, Discovery)
- [ ] Piano Roll Editor (visueller MIDI Editor)

### Niedrig Priorität
- [ ] Memory Pooling (AudioBufferPool)
- [ ] Undo/Redo System (Command Pattern)
- [ ] Audio Recording (WAV/MP3 Export)
- [ ] Project Browser

## Wichtige Konventionen

### Code Style
- File-scoped namespaces (`namespace MusicEngine.Core;`)
- Deutsche Commit Messages sind OK
- Keine Emojis in Code/Kommentaren

### Bekannte Workarounds
- NAudio.Midi: `NoteOnEvent` mit Velocity 0 für Note-Off
- WPF Shapes: `using Shapes = System.Windows.Shapes;` wegen Konflikten
- MIDI Export: Eigene WriteMidiFile() Methode (MidiFile.Export existiert nicht)

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

// MIDI exportieren
var exporter = new MidiExporter();
exporter.ExportPattern(pattern, "output.mid", 120);
```

## Letzte Änderungen (Session vom 21.01.2026)
1. MidiExporter.cs erstellt - MIDI File Export funktioniert
2. PatternTransform.cs erstellt - Scale-Lock, Humanization, Groove
3. LevelMeter Control erstellt - VU Meter für Editor
4. SnippetService erstellt - 12 Code Snippets
5. Diverse Build-Fehler behoben (NAudio Typen, WPF Shapes)
6. Alle Warnungen behoben
7. **Mixer View** komplett implementiert:
   - MixerChannel.cs Model (mit MasterChannel)
   - MixerChannelControl.xaml/.cs (Fader, Pan, M/S/R, LevelMeter)
   - MixerViewModel.cs (8 Channels + Master, Commands)
   - MixerView.xaml/.cs (Scrollbare Channel-Liste, Toolbar)

---
*Erstellt für Claude Code Terminal Kontext-Wiederherstellung*
