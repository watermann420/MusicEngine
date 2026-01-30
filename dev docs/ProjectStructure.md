# MusicEngine Project Structure

This document explains how MusicEngine projects are structured and how to reference scripts, instruments, and assets across files.

## Project File Layout

When you create a new project, this structure is generated:

```
MyProject/
├── MyProject.meproj           # Project manifest (JSON)
├── Scripts/                   # Your .me script files
│   ├── Main.me               # Entry point script
│   ├── MySynth.me            # Custom synth definition
│   ├── Patterns/             # Subfolder for organization
│   │   └── DrumPattern.me
│   └── Instruments/
│       └── BassSynth.me
├── Audio/                     # Imported audio samples
│   ├── Drums/
│   │   ├── kick.wav
│   │   └── snare.wav
│   └── Synths/
│       └── pad_sample.wav
├── bin/                       # Build output
└── obj/                       # Intermediate files
```

## The .meproj Manifest

The `.meproj` file is JSON that defines your project:

```json
{
  "$schema": "https://musicengine.dev/schema/meproj-1.0.json",
  "name": "MyProject",
  "version": "1.0.0",
  "guid": "a1b2c3d4-...",
  "namespace": "MyProject",
  "created": "2026-01-27T10:00:00Z",
  "modified": "2026-01-27T12:30:00Z",
  "musicEngineVersion": "1.0.0",
  "scripts": [
    { "path": "Scripts/Main.me", "entryPoint": true, "namespace": "MyProject.Scripts" },
    { "path": "Scripts/MySynth.me", "entryPoint": false, "namespace": "MyProject.Scripts" },
    { "path": "Scripts/Instruments/BassSynth.me", "entryPoint": false, "namespace": "MyProject.Scripts.Instruments" }
  ],
  "audioAssets": [
    { "path": "Audio/Drums/kick.wav", "alias": "Kick", "category": "Drums" },
    { "path": "Audio/Drums/snare.wav", "alias": "Snare", "category": "Drums" }
  ],
  "references": [
    { "type": "project", "path": "../SharedInstruments/SharedInstruments.meproj", "alias": "Shared" },
    { "type": "nuget", "path": "MusicEngine.Synths", "version": "1.2.0", "alias": "Synths" }
  ],
  "settings": {
    "sampleRate": 44100,
    "bufferSize": 512,
    "defaultBpm": 120
  }
}
```

## Scripts (.me Files)

### Entry Point Script (Main.me)

The entry point script is where playback starts:

```csharp
// Main.me - Entry point for MyProject
// Namespace: MyProject.Scripts

using MusicEngine;
using MusicEngine.Synths;
using MyProject.Scripts.Instruments;  // Reference other scripts

public class Main : MusicScript
{
    // Declare instruments from other scripts
    private BassSynth _bass;
    private LeadSynth _lead;

    // Declare audio samples
    private Sample _kick;
    private Sample _snare;

    public override void Setup()
    {
        // Initialize instruments from other scripts
        _bass = new BassSynth();
        _lead = new LeadSynth();

        // Load audio samples by alias
        _kick = Audio.Load("Kick");
        _snare = Audio.Load("Snare");

        // Configure playback
        BPM = 120;
        TimeSignature = (4, 4);
    }

    public override void Play()
    {
        // Use instruments
        _bass.PlayNote(Note.C2, 1.0, 0.8f);
        _lead.PlayNote(Note.E4, 0.5, 0.6f);

        // Trigger samples
        _kick.Play();

        // Schedule events
        At(Beat(2), () => _snare.Play());
    }

    public override void Stop()
    {
        _bass.Release();
        _lead.Release();
    }
}
```

### Instrument Script (BassSynth.me)

Define reusable instruments in separate scripts:

```csharp
// BassSynth.me - Reusable bass synthesizer
// Namespace: MyProject.Scripts.Instruments

using MusicEngine;
using MusicEngine.Synths;
using MusicEngine.Effects;

namespace MyProject.Scripts.Instruments
{
    public class BassSynth
    {
        private Oscillator _osc1;
        private Oscillator _osc2;
        private Filter _filter;
        private Envelope _ampEnv;
        private Envelope _filterEnv;

        // Public settings that can be adjusted from other scripts
        public float Cutoff { get; set; } = 800f;
        public float Resonance { get; set; } = 0.3f;
        public float SubLevel { get; set; } = 0.5f;
        public float Attack { get; set; } = 0.01f;
        public float Release { get; set; } = 0.3f;

        public BassSynth()
        {
            // Main oscillator - sawtooth
            _osc1 = new Oscillator
            {
                Waveform = WaveformType.Sawtooth,
                Volume = 0.7f
            };

            // Sub oscillator - sine, one octave down
            _osc2 = new Oscillator
            {
                Waveform = WaveformType.Sine,
                Octave = -1,
                Volume = SubLevel
            };

            // Low-pass filter
            _filter = new Filter
            {
                Type = FilterType.LowPass24,
                Cutoff = Cutoff,
                Resonance = Resonance
            };

            // Amplitude envelope
            _ampEnv = new Envelope
            {
                Attack = Attack,
                Decay = 0.1f,
                Sustain = 0.8f,
                Release = Release
            };

            // Filter envelope
            _filterEnv = new Envelope
            {
                Attack = 0.01f,
                Decay = 0.2f,
                Sustain = 0.3f,
                Release = 0.2f
            };

            // Route filter envelope to cutoff
            _filter.CutoffModulation = _filterEnv;
            _filter.ModulationAmount = 2000f;  // Sweep up to 2000Hz
        }

        public void PlayNote(Note note, double duration, float velocity = 1.0f)
        {
            _osc1.Frequency = note.Frequency;
            _osc2.Frequency = note.Frequency;
            _osc2.Volume = SubLevel * velocity;

            _ampEnv.Trigger(velocity);
            _filterEnv.Trigger(velocity);

            // Schedule note off
            Schedule(duration, () => Release());
        }

        public void Release()
        {
            _ampEnv.Release();
            _filterEnv.Release();
        }

        // Get the audio output (called by engine)
        public float Process()
        {
            float signal = _osc1.Process() + _osc2.Process();
            signal = _filter.Process(signal);
            signal *= _ampEnv.Process();
            return signal;
        }
    }
}
```

### Using Instruments from Other Scripts

```csharp
// Main.me
using MyProject.Scripts.Instruments;

public class Main : MusicScript
{
    private BassSynth _bass;

    public override void Setup()
    {
        _bass = new BassSynth();

        // Customize the instrument
        _bass.Cutoff = 1200f;
        _bass.Resonance = 0.5f;
        _bass.SubLevel = 0.7f;
        _bass.Attack = 0.005f;
    }

    public override void Play()
    {
        // Play a bass line
        _bass.PlayNote(Note.C2, 0.5);
        At(Beat(0.5), () => _bass.PlayNote(Note.C2, 0.25));
        At(Beat(1), () => _bass.PlayNote(Note.Eb2, 0.5));
        At(Beat(2), () => _bass.PlayNote(Note.G2, 1.0));
    }
}
```

## Referencing Other Projects

You can reference instruments and scripts from other MusicEngine projects:

### 1. Add Reference in .meproj

```json
{
  "references": [
    {
      "type": "project",
      "path": "../SharedInstruments/SharedInstruments.meproj",
      "alias": "Shared"
    }
  ]
}
```

### 2. Use in Your Scripts

```csharp
// Main.me
using MusicEngine;
using Shared.Synths;  // From referenced project

public class Main : MusicScript
{
    // Use synth from SharedInstruments project
    private Shared.Synths.AnalogBass _sharedBass;

    public override void Setup()
    {
        _sharedBass = new Shared.Synths.AnalogBass();
    }
}
```

### Project Reference Types

| Type | Description | Example |
|------|-------------|---------|
| `project` | Another .meproj | `"../OtherProject/OtherProject.meproj"` |
| `nuget` | NuGet package | `"MusicEngine.Synths"` with version |

## Namespace Conventions

Namespaces are automatically generated based on folder structure:

| File Location | Namespace |
|--------------|-----------|
| `Scripts/Main.me` | `MyProject.Scripts` |
| `Scripts/Instruments/Bass.me` | `MyProject.Scripts.Instruments` |
| `Scripts/Patterns/Drums/Kick.me` | `MyProject.Scripts.Patterns.Drums` |

## Audio Assets

### Importing Audio

Audio files are copied to the project and registered in the manifest:

```csharp
// The ProjectService handles this automatically when you import
// Files are organized into: Audio/[Category]/filename.wav
```

### Using Audio in Scripts

```csharp
public class Main : MusicScript
{
    private Sample _kick;
    private Sample _snare;
    private Sample _hihat;

    public override void Setup()
    {
        // Load by alias (defined in .meproj)
        _kick = Audio.Load("Kick");
        _snare = Audio.Load("Snare");
        _hihat = Audio.Load("HiHat");

        // Or load by path
        _kick = Audio.LoadPath("Audio/Drums/kick.wav");
    }

    public override void Play()
    {
        // Simple playback
        _kick.Play();

        // With velocity
        _snare.Play(0.8f);

        // Pitched playback
        _hihat.PlayPitched(Note.C4, 0.5f);

        // Looped playback
        var pad = Audio.Load("Pad");
        pad.PlayLoop();
    }
}
```

## Complete Example: Multi-Script Project

### Project Structure

```
DrumMachine/
├── DrumMachine.meproj
├── Scripts/
│   ├── Main.me              # Entry point
│   ├── Sequencer.me         # Step sequencer logic
│   └── Kits/
│       ├── Kit808.me        # TR-808 style kit
│       └── KitAcoustic.me   # Acoustic kit
└── Audio/
    ├── 808/
    │   ├── kick.wav
    │   ├── snare.wav
    │   └── hihat.wav
    └── Acoustic/
        ├── kick.wav
        ├── snare.wav
        └── hihat.wav
```

### Sequencer.me

```csharp
// Sequencer.me - Reusable step sequencer
// Namespace: DrumMachine.Scripts

namespace DrumMachine.Scripts
{
    public class Sequencer
    {
        public int Steps { get; set; } = 16;
        public int CurrentStep { get; private set; } = 0;

        private bool[,] _pattern;  // [track, step]
        private Action<int>[] _triggers;  // Callbacks per track

        public Sequencer(int tracks = 8)
        {
            _pattern = new bool[tracks, 16];
            _triggers = new Action<int>[tracks];
        }

        public void SetTrigger(int track, Action<int> callback)
        {
            _triggers[track] = callback;
        }

        public void SetStep(int track, int step, bool active)
        {
            _pattern[track, step] = active;
        }

        public void SetPattern(int track, string pattern)
        {
            // Pattern string: "x---x---x---x---" (x = hit, - = rest)
            for (int i = 0; i < Math.Min(pattern.Length, Steps); i++)
            {
                _pattern[track, i] = pattern[i] == 'x' || pattern[i] == 'X';
            }
        }

        public void Tick()
        {
            for (int track = 0; track < _triggers.Length; track++)
            {
                if (_pattern[track, CurrentStep] && _triggers[track] != null)
                {
                    _triggers[track](CurrentStep);
                }
            }

            CurrentStep = (CurrentStep + 1) % Steps;
        }

        public void Reset()
        {
            CurrentStep = 0;
        }
    }
}
```

### Kit808.me

```csharp
// Kit808.me - TR-808 style drum kit
// Namespace: DrumMachine.Scripts.Kits

using MusicEngine;

namespace DrumMachine.Scripts.Kits
{
    public class Kit808
    {
        public Sample Kick { get; private set; }
        public Sample Snare { get; private set; }
        public Sample ClosedHat { get; private set; }
        public Sample OpenHat { get; private set; }
        public Sample Clap { get; private set; }
        public Sample Tom { get; private set; }
        public Sample Rim { get; private set; }
        public Sample Cowbell { get; private set; }

        // Global kit settings
        public float Volume { get; set; } = 1.0f;
        public float Pitch { get; set; } = 1.0f;

        public Kit808()
        {
            Kick = Audio.Load("808_Kick");
            Snare = Audio.Load("808_Snare");
            ClosedHat = Audio.Load("808_CHat");
            OpenHat = Audio.Load("808_OHat");
            Clap = Audio.Load("808_Clap");
            Tom = Audio.Load("808_Tom");
            Rim = Audio.Load("808_Rim");
            Cowbell = Audio.Load("808_Cowbell");
        }

        public void PlayKick(float velocity = 1.0f)
        {
            Kick.Play(velocity * Volume, Pitch);
        }

        public void PlaySnare(float velocity = 1.0f)
        {
            Snare.Play(velocity * Volume, Pitch);
        }

        public void PlayHat(bool open = false, float velocity = 1.0f)
        {
            if (open)
                OpenHat.Play(velocity * Volume, Pitch);
            else
                ClosedHat.Play(velocity * Volume, Pitch);
        }

        // ... other methods
    }
}
```

### Main.me

```csharp
// Main.me - Drum machine entry point
// Namespace: DrumMachine.Scripts

using MusicEngine;
using DrumMachine.Scripts;
using DrumMachine.Scripts.Kits;

public class Main : MusicScript
{
    private Sequencer _sequencer;
    private Kit808 _kit;

    public override void Setup()
    {
        BPM = 120;

        // Initialize components from other scripts
        _sequencer = new Sequencer(4);  // 4 tracks
        _kit = new Kit808();

        // Connect sequencer to kit
        _sequencer.SetTrigger(0, step => _kit.PlayKick());
        _sequencer.SetTrigger(1, step => _kit.PlaySnare());
        _sequencer.SetTrigger(2, step => _kit.PlayHat(false));
        _sequencer.SetTrigger(3, step => _kit.PlayHat(true));

        // Program patterns
        _sequencer.SetPattern(0, "x---x---x---x---");  // Kick: four-on-floor
        _sequencer.SetPattern(1, "----x-------x---");  // Snare: 2 and 4
        _sequencer.SetPattern(2, "x-x-x-x-x-x-x-x-");  // Closed hat: 8ths
        _sequencer.SetPattern(3, "--------x-------");  // Open hat
    }

    public override void Play()
    {
        // Tick sequencer on every 16th note
        Every(Beat(0.25), () => _sequencer.Tick());
    }

    public override void Stop()
    {
        _sequencer.Reset();
    }
}
```

## Best Practices

### 1. Organize by Function

```
Scripts/
├── Instruments/     # Synths, samplers, drum machines
├── Effects/         # Custom effect chains
├── Patterns/        # Reusable musical patterns
├── Utilities/       # Helper classes
└── Main.me          # Entry point
```

### 2. Use Clear Namespaces

```csharp
// Good: Clear namespace hierarchy
namespace MyProject.Scripts.Instruments.Bass { }
namespace MyProject.Scripts.Patterns.Drums { }

// Avoid: Flat namespaces for large projects
namespace MyProject { }
```

### 3. Make Instruments Configurable

```csharp
public class MySynth
{
    // Expose settings as properties
    public float Attack { get; set; } = 0.01f;
    public float FilterCutoff { get; set; } = 1000f;
    public WaveformType Waveform { get; set; } = WaveformType.Sawtooth;

    // Allow preset loading
    public void LoadPreset(string name) { }
    public void SavePreset(string name) { }
}
```

### 4. Separate Logic from Sound

```csharp
// Sequencer handles timing logic
public class Sequencer { }

// Kit handles sounds
public class DrumKit { }

// Main connects them
_sequencer.SetTrigger(0, _ => _kit.PlayKick());
```

### 5. Use References for Shared Assets

If multiple projects use the same instruments:

```
SharedAssets/
├── SharedAssets.meproj
└── Scripts/
    └── Synths/
        ├── AnalogBass.me
        └── DigitalLead.me

Project1/
├── Project1.meproj  (references SharedAssets)
└── Scripts/
    └── Main.me

Project2/
├── Project2.meproj  (references SharedAssets)
└── Scripts/
    └── Main.me
```
