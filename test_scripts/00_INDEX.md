# MusicEngine Test Scripts - Syntax Reference

This directory contains comprehensive test scripts demonstrating **all** MusicEngine functions and their current syntax. These scripts serve as a reference for syntax customization.

## Purpose

These test scripts allow you to:
1. **Review current syntax** - See how every function is currently named and used
2. **Test functionality** - Verify that all features work correctly
3. **Plan syntax changes** - Identify which function names you want to change
4. **Guide refactoring** - Provide Claude with examples to implement your custom syntax

## Script Index

### 01_basic_setup.csx
**Topics**: Engine initialization, global variables, settings, device enumeration
- `Engine` variable
- `Sequencer` variable
- `Settings` class
- `Print()` function

### 02_synth_creation.csx
**Topics**: Synthesizer creation, waveform types, parameter control
- `CreateSynth()` function
- `SynthWaveform` enum (Sine, Square, Sawtooth, Triangle, Noise)
- Synth parameters: Volume, FilterCutoff, FilterResonance, Pitch
- `NoteOn()`, `NoteOff()`, `AllNotesOff()` methods

### 03_patterns.csx
**Topics**: Pattern creation, note events, pattern management
- `CreatePattern()` function
- `AddNote()`, `RemoveNote()`, `Clear()` methods
- `Start()`, `Stop()`, `Toggle()` methods
- Pattern properties: Loop, IsPlaying, NoteCount, Name

### 04_transport_control.csx
**Topics**: Sequencer control, tempo, beat navigation, timing precision
- `Start()`, `Stop()` functions
- `SetBpm()` function
- `Skip()` function
- `StartPattern()`, `StopPattern()` functions
- `BeatSubdivision` enum
- `TimingPrecision` enum

### 05_sample_instruments.csx
**Topics**: Sample loading, sample-based instruments, sample mapping
- `CreateSampleInstrument()` function
- `LoadSample()`, `LoadSamplesFromDirectory()` functions
- `Sample.Create()` fluent API
- `.Load()`, `.FromDirectory()`, `.MapSample()` builder methods

### 06_midi_control.csx
**Topics**: MIDI routing, device access, CC mapping, keyboard splits
- `Midi.Device()` fluent API
- `.To()`, `.MapCC()`, `.MapPitchBend()`, `.MapRange()` methods
- `SendNoteOn()`, `SendNoteOff()`, `SendControlChange()` functions
- Transport mapping: `MapNoteToStart()`, `MapNoteToStop()`, `MapCCToBpm()`
- Direct routing: `RouteMidiInput()`, `MapMidiControl()`, `MapPitchBend()`

### 07_audio_control.csx
**Topics**: Audio mixing, volume control, frequency triggers
- `Audio.MasterVolume()` function
- `Audio.ChannelVolume()`, `Audio.AllChannels()` functions
- `Audio.StartInputCapture()`, `Audio.StopInputCapture()` functions
- `Audio.Input()` fluent API for frequency triggers
- `.Frequency()`, `.Threshold()`, `.Trigger()`, `.TriggerNote()` methods

### 08_vst_plugins.csx
**Topics**: VST plugin loading, parameter control, MIDI routing
- `Vst.ListPlugins()`, `Vst.LoadedPlugins()` functions
- `Vst.Load()`, `Vst.Get()`, `Vst.Unload()` functions
- `Vst.Plugin()` fluent API
- `.SetParameter()`, `.Param()` methods
- `.NoteOn()`, `.NoteOff()`, `.AllNotesOff()` methods
- `.ControlChange()`, `.ProgramChange()` methods

### 09_virtual_channels.csx
**Topics**: Virtual audio channels, named pipes, audio routing
- `VirtualChannel.Create()` function
- `VirtualChannel.List()` function
- `.Volume()`, `.Start()` builder methods
- Channel methods: `Start()`, `Stop()`, `SetVolume()`, `GetPipeName()`, `GetChannel()`

### 10_helper_functions.csx
**Topics**: Utility functions, random generation, output
- `Print()` function
- `Random()` function (0.0-1.0 or min-max range)
- `RandomInt()` function
- Practical examples for random patterns, parameters, melodies

## How to Use These Scripts

### 1. Run a Test Script
```bash
cd C:\Users\null\RiderProjects\MusicEngine
dotnet run --project MusicEngine.csproj test_scripts/01_basic_setup.csx
```

### 2. Review Syntax
Open any script to see:
- **Current function names** at the top of each section
- **Alternative naming suggestions** at the bottom in the "SYNTAX ELEMENTS TO CUSTOMIZE" section
- **Usage examples** showing all parameters and variations

### 3. Mark Your Changes
Edit the scripts or create a separate document listing your desired changes:
```
CURRENT → DESIRED
- CreateSynth → synth
- NoteOn → play
- Print → log
- Midi.Device(0).To(synth) → midi[0] > synth
```

### 4. Provide to Claude
Give Claude your desired syntax changes along with these test scripts. Claude will:
- Understand the current implementation from these examples
- Implement your custom syntax throughout the codebase
- Update the scripting API to match your preferences
- Ensure all functionality remains intact

## Syntax Customization Categories

Each script's bottom section lists customizable elements in these categories:

1. **Function Names** - Main API functions (CreateSynth, CreatePattern, etc.)
2. **Method Names** - Object methods (NoteOn, Start, Stop, etc.)
3. **Property Names** - Object properties (Volume, Loop, Name, etc.)
4. **Enum Values** - Named constants (SynthWaveform.Sine, TimingPrecision.High, etc.)
5. **Fluent API Syntax** - Chaining methods (Sample.Create().Load().Build())
6. **Parameter Names** - Function parameters (note, velocity, duration, etc.)

## Multiple Aliases Support

You can request multiple names for the same function:
- `CreateSynth` could also be `synth`, `newSynth`, `makeSynth`
- `NoteOn` could also be `play`, `trigger`, `on`
- Claude can implement all aliases simultaneously

## Notes

- **All scripts are independent** - Run them in any order
- **No hardware required** - Scripts will work even without MIDI devices or audio input
- **Commented sections** - Areas requiring sample files or plugins are marked
- **Error safe** - Scripts handle missing resources gracefully

## Examples of Syntax Changes You Might Want

### Shorter Function Names
```csharp
// Current
var s = CreateSynth("bass");
s.NoteOn(60, 100);

// Possible
var s = synth("bass");
s.play(60, 100);
```

### Operator-based Routing
```csharp
// Current
Midi.Device(0).To(synth);

// Possible
midi[0] > synth;
midi[0] >> synth;
```

### Simplified Fluent APIs
```csharp
// Current
Sample.Create("drums").Load("kick.wav", 36).Build();

// Possible
sample("drums").add("kick.wav", 36);
sampler("drums") << "kick.wav";
```

### Abbreviated Properties
```csharp
// Current
synth.FilterCutoff.Value = 800;
synth.FilterResonance.Value = 2.0;

// Possible
synth.cutoff = 800;
synth.res = 2.0;
synth.fc = 800;  // filter cutoff
```

## Ready for Customization

Review these scripts, note the syntax you want to change, and Claude will implement your custom scripting language while maintaining all functionality!
