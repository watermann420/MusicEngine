# MusicEngine - Complete Syntax Customization Guide

This guide lists **EVERY** function, method, and property in MusicEngine with syntax customization options.

---

## Table of Contents

1. [Global Variables & Objects](#1-global-variables--objects)
2. [Synth Functions](#2-synth-functions)
3. [Synth Methods](#3-synth-methods)
4. [Synth Properties](#4-synth-properties)
5. [Pattern Functions](#5-pattern-functions)
6. [Pattern Methods](#6-pattern-methods)
7. [Pattern Properties](#7-pattern-properties)
8. [Transport Functions](#8-transport-functions)
9. [Sequencer Properties](#9-sequencer-properties)
10. [Sample Instrument Functions](#10-sample-instrument-functions)
11. [Sample Fluent API](#11-sample-fluent-api)
12. [MIDI Functions (Fluent API)](#12-midi-functions-fluent-api)
13. [MIDI Functions (Direct)](#13-midi-functions-direct)
14. [MIDI Output Functions](#14-midi-output-functions)
15. [Audio Control Functions](#15-audio-control-functions)
16. [Frequency Trigger API](#16-frequency-trigger-api)
17. [VST Plugin Functions](#17-vst-plugin-functions)
18. [VST Plugin Control API](#18-vst-plugin-control-api)
19. [Virtual Channel Functions](#19-virtual-channel-functions)
20. [Helper Functions](#20-helper-functions)
21. [Enums & Constants](#21-enums--constants)

---

## 1. Global Variables & Objects

### Current Syntax
```csharp
Engine          // AudioEngine instance
Sequencer       // Sequencer instance
Settings        // Static settings class
```

### Customization Options
| Current | Alternative Options |
|---------|---------------------|
| `Engine` | `audio`, `engine`, `sound`, `output`, `ae`, `e` |
| `Sequencer` | `seq`, `sequence`, `timeline`, `clock`, `s` |
| `Settings` | `config`, `cfg`, `setup`, `settings` |

### Your Choice
```
Engine     →  _________________
Sequencer  →  _________________
Settings   →  _________________
```

---

## 2. Synth Functions

### Current Syntax
```csharp
CreateSynth(name)
CreateSynth(name, waveform)
```

### Usage Examples
```csharp
var s = CreateSynth("bass");
var s = CreateSynth("lead", SynthWaveform.Square);
```

### Customization Options
| Current | Alternative Options |
|---------|---------------------|
| `CreateSynth` | `synth`, `newSynth`, `makeSynth`, `addSynth`, `s`, `voice`, `instrument` |

### Fluent/Alternative Syntax Ideas
```csharp
// Current
CreateSynth("bass", SynthWaveform.Sawtooth)

// Possible alternatives
synth("bass", wave.saw)
synth("bass").wave(saw)
new Synth("bass", saw)
@bass.saw
```

### Your Choice
```
CreateSynth  →  _________________

Preferred syntax style:
_____________________________________
```

---

## 3. Synth Methods

### Current Syntax
```csharp
synth.NoteOn(note, velocity)
synth.NoteOff(note)
synth.AllNotesOff()
```

### Usage Examples
```csharp
synth.NoteOn(60, 100);      // Play C4 at velocity 100
synth.NoteOff(60);          // Stop C4
synth.AllNotesOff();        // Stop all notes
```

### Customization Options
| Current | Alternative Options |
|---------|---------------------|
| `NoteOn` | `play`, `noteOn`, `on`, `trigger`, `start`, `hit`, `p` |
| `NoteOff` | `stop`, `noteOff`, `off`, `release`, `end`, `kill` |
| `AllNotesOff` | `stopAll`, `silence`, `killAll`, `panic`, `clear`, `mute` |

### Alternative Syntax Ideas
```csharp
// Current
synth.NoteOn(60, 100);
synth.NoteOff(60);

// Possible alternatives
synth.play(60, 100);
synth.stop(60);
synth > 60 @ 100;           // Operator syntax
synth << note(60, 100);     // Stream syntax
synth.trigger(c4, ff);      // Named notes/velocities
```

### Your Choice
```
NoteOn        →  _________________
NoteOff       →  _________________
AllNotesOff   →  _________________

Preferred syntax:
_____________________________________
```

---

## 4. Synth Properties

### Current Syntax
```csharp
synth.Volume.Value = 0.8;
synth.FilterCutoff.Value = 800;
synth.FilterResonance.Value = 2.0;
synth.Pitch.Value = 1.2;
synth.Name
```

### Usage Examples
```csharp
synth.Volume.Value = 0.5;           // 50% volume
synth.FilterCutoff.Value = 1200;    // 1200 Hz cutoff
synth.FilterResonance.Value = 3.0;  // Resonance 3.0
synth.Pitch.Value = 0.9;            // Pitch down 10%
var name = synth.Name;              // Get name
```

### Customization Options
| Current | Alternative Options |
|---------|---------------------|
| `Volume.Value` | `vol`, `volume`, `gain`, `level`, `amp`, `v` |
| `FilterCutoff.Value` | `cutoff`, `filter`, `freq`, `fc`, `lpf` |
| `FilterResonance.Value` | `resonance`, `res`, `q`, `reso`, `r` |
| `Pitch.Value` | `pitch`, `tune`, `detune`, `transpose`, `p` |
| `Name` | `name`, `id`, `label` |

### Alternative Syntax Ideas
```csharp
// Current
synth.Volume.Value = 0.8;
synth.FilterCutoff.Value = 1000;

// Possible alternatives (direct access)
synth.vol = 0.8;
synth.cutoff = 1000;
synth.fc = 1000;

// Fluent style
synth.volume(0.8).cutoff(1000);

// Short notation
synth.v(0.8).fc(1000);
```

### Your Choice
```
Volume          →  _________________
FilterCutoff    →  _________________
FilterResonance →  _________________
Pitch           →  _________________
Name            →  _________________

Preferred property access:
  [ ] .Property.Value
  [ ] .property
  [ ] .property()
```

---

## 5. Pattern Functions

### Current Syntax
```csharp
CreatePattern(synth, name)
```

### Usage Examples
```csharp
var p = CreatePattern(synth, "bassline");
```

### Customization Options
| Current | Alternative Options |
|---------|---------------------|
| `CreatePattern` | `pattern`, `newPattern`, `makePattern`, `addPattern`, `p`, `seq`, `loop` |

### Alternative Syntax Ideas
```csharp
// Current
CreatePattern(synth, "bass")

// Possible alternatives
pattern(synth, "bass")
synth.pattern("bass")
new Pattern(synth, "bass")
@bass.pattern
```

### Your Choice
```
CreatePattern  →  _________________

Preferred syntax:
_____________________________________
```

---

## 6. Pattern Methods

### Current Syntax
```csharp
pattern.AddNote(beat, note, velocity, duration)
pattern.RemoveNote(beat, note)
pattern.Clear()
pattern.Start()
pattern.Stop()
pattern.Toggle()
```

### Usage Examples
```csharp
pattern.AddNote(0.0, 60, 100, 0.5);  // Add C4 at beat 0
pattern.RemoveNote(0.0, 60);          // Remove note
pattern.Clear();                       // Clear all notes
pattern.Start();                       // Start playing
pattern.Stop();                        // Stop playing
pattern.Toggle();                      // Toggle on/off
```

### Customization Options
| Current | Alternative Options |
|---------|---------------------|
| `AddNote` | `note`, `add`, `n`, `addNote`, `insert`, `put`, `@` |
| `RemoveNote` | `remove`, `delete`, `del`, `removeNote`, `erase` |
| `Clear` | `clear`, `reset`, `empty`, `removeAll`, `wipe` |
| `Start` | `start`, `play`, `run`, `begin`, `go` |
| `Stop` | `stop`, `pause`, `halt`, `end` |
| `Toggle` | `toggle`, `switch`, `flip`, `t` |

### Alternative Syntax Ideas
```csharp
// Current
pattern.AddNote(0, 60, 100, 0.5);

// Possible alternatives
pattern.note(0, 60, 100, 0.5);
pattern.add(0, 60, 100, 0.5);
pattern @ (0, 60, 100, 0.5);
pattern << note(0, 60, 100, 0.5);
pattern[0] = note(60, 100, 0.5);

// Chord notation
pattern.chord(0, [60, 64, 67], 100, 0.5);  // C major
```

### Your Choice
```
AddNote      →  _________________
RemoveNote   →  _________________
Clear        →  _________________
Start        →  _________________
Stop         →  _________________
Toggle       →  _________________

Preferred note syntax:
_____________________________________
```

---

## 7. Pattern Properties

### Current Syntax
```csharp
pattern.Loop = true;
pattern.IsPlaying
pattern.NoteCount
pattern.Name
```

### Customization Options
| Current | Alternative Options |
|---------|---------------------|
| `Loop` | `loop`, `repeat`, `cycle`, `looping` |
| `IsPlaying` | `isPlaying`, `playing`, `active`, `running` |
| `NoteCount` | `noteCount`, `count`, `length`, `size`, `notes` |
| `Name` | `name`, `id`, `label` |

### Your Choice
```
Loop       →  _________________
IsPlaying  →  _________________
NoteCount  →  _________________
Name       →  _________________
```

---

## 8. Transport Functions

### Current Syntax
```csharp
Start()
Stop()
SetBpm(bpm)
Skip(beats)
StartPattern(pattern)
StartPattern(name)
StopPattern(pattern)
StopPattern(name)
```

### Usage Examples
```csharp
Start();                    // Start sequencer
Stop();                     // Stop sequencer
SetBpm(120);                // Set tempo to 120 BPM
Skip(4);                    // Skip forward 4 beats
Skip(-2);                   // Skip backward 2 beats
StartPattern(pattern);      // Start specific pattern
StartPattern("bass");       // Start pattern by name
StopPattern(pattern);       // Stop pattern
```

### Customization Options
| Current | Alternative Options |
|---------|---------------------|
| `Start` | `start`, `play`, `run`, `go`, `begin` |
| `Stop` | `stop`, `pause`, `halt`, `end` |
| `SetBpm` | `bpm`, `tempo`, `setBpm`, `setTempo`, `speed` |
| `Skip` | `skip`, `jump`, `seek`, `move`, `goto` |
| `StartPattern` | `startPattern`, `playPattern`, `runPattern`, `start` |
| `StopPattern` | `stopPattern`, `pausePattern`, `haltPattern`, `stop` |

### Alternative Syntax Ideas
```csharp
// Current
Start();
SetBpm(140);
Skip(4);

// Possible alternatives
play();
bpm(140);
jump(4);

// Property style
sequencer.playing = true;
sequencer.bpm = 140;
sequencer.position += 4;
```

### Your Choice
```
Start          →  _________________
Stop           →  _________________
SetBpm         →  _________________
Skip           →  _________________
StartPattern   →  _________________
StopPattern    →  _________________
```

---

## 9. Sequencer Properties

### Current Syntax
```csharp
Sequencer.Bpm
Sequencer.IsPlaying
Sequencer.CurrentBeat
Sequencer.PatternCount
Sequencer.SampleRate
Sequencer.BeatSubdivision
Sequencer.TimingMode
```

### Customization Options
| Current | Alternative Options |
|---------|---------------------|
| `Bpm` | `bpm`, `tempo`, `speed` |
| `IsPlaying` | `isPlaying`, `playing`, `running`, `active` |
| `CurrentBeat` | `currentBeat`, `beat`, `position`, `pos` |
| `PatternCount` | `patternCount`, `patterns`, `count` |
| `SampleRate` | `sampleRate`, `sr`, `rate` |
| `BeatSubdivision` | `subdivision`, `resolution`, `ppqn`, `grid` |
| `TimingMode` | `timingMode`, `precision`, `mode`, `timing` |

### Your Choice
```
Bpm             →  _________________
IsPlaying       →  _________________
CurrentBeat     →  _________________
PatternCount    →  _________________
SampleRate      →  _________________
BeatSubdivision →  _________________
TimingMode      →  _________________
```

---

## 10. Sample Instrument Functions

### Current Syntax
```csharp
CreateSampleInstrument(name)
LoadSample(instrument, path)
LoadSample(instrument, path, rootNote)
LoadSample(instrument, path, rootNote, lowNote, highNote)
LoadSamplesFromDirectory(instrument, directory)
```

### Usage Examples
```csharp
var drums = CreateSampleInstrument("drums");
LoadSample(drums, "kick.wav");
LoadSample(drums, "snare.wav", 38);
LoadSample(drums, "bass.wav", 48, 36, 60);
LoadSamplesFromDirectory(drums, "C:\\Samples");
```

### Customization Options
| Current | Alternative Options |
|---------|---------------------|
| `CreateSampleInstrument` | `sampler`, `sample`, `newSampler`, `makeSampler`, `sampleInst` |
| `LoadSample` | `load`, `loadSample`, `addSample`, `sample`, `import` |
| `LoadSamplesFromDirectory` | `loadDir`, `fromDir`, `loadFolder`, `importDir`, `scanDir` |

### Your Choice
```
CreateSampleInstrument    →  _________________
LoadSample                →  _________________
LoadSamplesFromDirectory  →  _________________
```

---

## 11. Sample Fluent API

### Current Syntax
```csharp
Sample.Create(name)
  .Load(path, note)
  .FromDirectory(path)
  .MapSample(path, note, lowNote, highNote)
  .Volume(volume)
  .Build()
```

### Usage Examples
```csharp
var drums = Sample.Create("drums")
    .Load("kick.wav", 36)
    .Load("snare.wav", 38)
    .Volume(0.8)
    .Build();

var piano = Sample.Create("piano")
    .FromDirectory("C:\\Samples\\Piano")
    .Volume(0.6)
    .Build();
```

### Customization Options
| Current | Alternative Options |
|---------|---------------------|
| `Sample.Create` | `Sample`, `Sampler`, `NewSample`, `S` |
| `Load` | `load`, `add`, `sample`, `with`, `file` |
| `FromDirectory` | `fromDir`, `directory`, `folder`, `scan`, `import` |
| `MapSample` | `map`, `mapSample`, `assign`, `bind`, `zone` |
| `Volume` | `volume`, `vol`, `gain`, `level` |
| `Build` | `build`, `create`, `make`, `done`, `finish`, `go` |

### Alternative Syntax Ideas
```csharp
// Current
Sample.Create("drums").Load("kick.wav", 36).Build();

// Possible alternatives
sampler("drums") << "kick.wav" @ 36;
sample("drums").add("kick.wav", 36);
drums = sampler().load("kick.wav", 36);
```

### Your Choice
```
Sample.Create   →  _________________
.Load           →  _________________
.FromDirectory  →  _________________
.MapSample      →  _________________
.Volume         →  _________________
.Build          →  _________________

Preferred fluent style:
_____________________________________
```

---

## 12. MIDI Functions (Fluent API)

### Current Syntax
```csharp
Midi.Device(index)
Midi.Device(name)
  .To(synth)
  .MapCC(cc, parameter, min, max)
  .MapPitchBend(parameter, min, max)
  .MapRange(lowNote, highNote, synth)
  .MapNoteToStart(note)
  .MapNoteToStop(note)
  .MapCCToBpm(cc, minBpm, maxBpm)
  .MapCCToSkip(cc, beats)
  .MapCCToScratch(cc, range)
```

### Usage Examples
```csharp
Midi.Device(0).To(synth);
Midi.Device("Keyboard").To(synth);
Midi.Device(0).MapCC(1, synth.FilterCutoff, 200, 2000);
Midi.Device(0).MapPitchBend(synth.Pitch, 0.5, 2.0);
Midi.Device(0).MapRange(0, 59, bass);
Midi.Device(0).MapRange(60, 127, lead);
Midi.Device(0).MapNoteToStart(60);
Midi.Device(0).MapCCToBpm(20, 60, 200);
```

### Customization Options
| Current | Alternative Options |
|---------|---------------------|
| `Midi.Device` | `midi`, `m`, `input`, `device`, `midiIn` |
| `.To` | `to`, `route`, `send`, `connect`, `link`, `>`, `>>` |
| `.MapCC` | `cc`, `mapCC`, `control`, `bindCC`, `ctrl` |
| `.MapPitchBend` | `pitchBend`, `bend`, `pb`, `mapBend` |
| `.MapRange` | `range`, `split`, `zone`, `keys`, `mapRange` |
| `.MapNoteToStart` | `startNote`, `onStart`, `triggerStart` |
| `.MapNoteToStop` | `stopNote`, `onStop`, `triggerStop` |
| `.MapCCToBpm` | `bpmCC`, `tempoCC`, `mapBpm` |
| `.MapCCToSkip` | `skipCC`, `jumpCC`, `mapSkip` |
| `.MapCCToScratch` | `scratchCC`, `jogCC`, `mapScratch` |

### Alternative Syntax Ideas
```csharp
// Current
Midi.Device(0).To(synth);
Midi.Device(0).MapCC(1, synth.FilterCutoff, 200, 2000);

// Possible alternatives (operator syntax)
midi[0] > synth;
midi[0] >> synth;
midi[0].cc[1] > synth.cutoff(200, 2000);

// Short notation
m[0] > synth;
m[0].cc1 > synth.fc;

// Named devices
midi.keyboard > synth;
```

### Your Choice
```
Midi.Device      →  _________________
.To              →  _________________
.MapCC           →  _________________
.MapPitchBend    →  _________________
.MapRange        →  _________________
.MapNoteToStart  →  _________________
.MapNoteToStop   →  _________________
.MapCCToBpm      →  _________________

Preferred MIDI syntax:
_____________________________________
```

---

## 13. MIDI Functions (Direct)

### Current Syntax
```csharp
RouteMidiInput(deviceIndex, synth)
MapMidiControl(deviceIndex, cc, parameter, min, max)
MapPitchBend(deviceIndex, parameter, min, max)
MapRange(deviceIndex, lowNote, highNote, synth)
```

### Customization Options
| Current | Alternative Options |
|---------|---------------------|
| `RouteMidiInput` | `route`, `routeMidi`, `connect`, `link` |
| `MapMidiControl` | `mapCC`, `bindCC`, `controlMap`, `ccMap` |
| `MapPitchBend` | `bendMap`, `pitchMap`, `mapBend` |
| `MapRange` | `rangeMap`, `splitMap`, `zoneMap` |

### Your Choice
```
RouteMidiInput   →  _________________
MapMidiControl   →  _________________
MapPitchBend     →  _________________
MapRange         →  _________________
```

---

## 14. MIDI Output Functions

### Current Syntax
```csharp
SendNoteOn(device, note, velocity, channel)
SendNoteOff(device, note, channel)
SendControlChange(device, cc, value, channel)
```

### Usage Examples
```csharp
SendNoteOn(0, 60, 100, 0);
SendNoteOff(0, 60, 0);
SendControlChange(0, 1, 64, 0);
```

### Customization Options
| Current | Alternative Options |
|---------|---------------------|
| `SendNoteOn` | `noteOn`, `sendNote`, `playNote`, `send` |
| `SendNoteOff` | `noteOff`, `stopNote`, `releaseNote`, `off` |
| `SendControlChange` | `cc`, `sendCC`, `control`, `ctrl` |

### Your Choice
```
SendNoteOn        →  _________________
SendNoteOff       →  _________________
SendControlChange →  _________________
```

---

## 15. Audio Control Functions

### Current Syntax
```csharp
Audio.MasterVolume()
Audio.MasterVolume(volume)
Audio.ChannelVolume(channel)
Audio.ChannelVolume(channel, volume)
Audio.AllChannels(volume)
Audio.StartInputCapture(inputIndex)
Audio.StopInputCapture(inputIndex)
```

### Usage Examples
```csharp
var vol = Audio.MasterVolume();
Audio.MasterVolume(0.8);
Audio.ChannelVolume(0, 0.6);
Audio.AllChannels(0.7);
Audio.StartInputCapture(0);
Audio.StopInputCapture(0);
```

### Customization Options
| Current | Alternative Options |
|---------|---------------------|
| `Audio.MasterVolume` | `master`, `volume`, `mainVol`, `output` |
| `Audio.ChannelVolume` | `channel`, `track`, `chVol`, `ch` |
| `Audio.AllChannels` | `all`, `allChannels`, `setAll`, `global` |
| `Audio.StartInputCapture` | `startInput`, `capture`, `record`, `in` |
| `Audio.StopInputCapture` | `stopInput`, `stopCapture`, `endInput` |

### Your Choice
```
Audio.MasterVolume     →  _________________
Audio.ChannelVolume    →  _________________
Audio.AllChannels      →  _________________
Audio.StartInputCapture →  _________________
Audio.StopInputCapture  →  _________________
```

---

## 16. Frequency Trigger API

### Current Syntax
```csharp
Audio.Input(index)
  .Frequency(min, max)
  .Threshold(threshold)
  .Trigger(action)
  .TriggerNote(synth, note, velocity)

AddFrequencyTrigger(input, minFreq, maxFreq, threshold, action)
```

### Usage Examples
```csharp
Audio.Input(0)
    .Frequency(20, 100)
    .Threshold(0.5)
    .TriggerNote(drums, 36, 100);

AddFrequencyTrigger(0, 20, 100, 0.5, mag => {
    Print($"Kick! {mag}");
});
```

### Customization Options
| Current | Alternative Options |
|---------|---------------------|
| `Audio.Input` | `input`, `audioIn`, `capture`, `source` |
| `.Frequency` | `freq`, `frequency`, `range`, `band`, `hz` |
| `.Threshold` | `threshold`, `level`, `trigger`, `sensitivity` |
| `.Trigger` | `trigger`, `on`, `when`, `action`, `do` |
| `.TriggerNote` | `note`, `play`, `triggerNote`, `send` |
| `AddFrequencyTrigger` | `freqTrigger`, `onFreq`, `listenFreq`, `watchFreq` |

### Your Choice
```
Audio.Input          →  _________________
.Frequency           →  _________________
.Threshold           →  _________________
.Trigger             →  _________________
.TriggerNote         →  _________________
AddFrequencyTrigger  →  _________________
```

---

## 17. VST Plugin Functions

### Current Syntax
```csharp
Vst.ListPlugins()
Vst.LoadedPlugins()
Vst.Load(name)
Vst.Load(path)
Vst.Load(index)
Vst.Get(name)
Vst.Unload(name)
```

### Usage Examples
```csharp
var available = Vst.ListPlugins();
var loaded = Vst.LoadedPlugins();
var synth = Vst.Load("MySynth");
var plugin = Vst.Load(0);
var existing = Vst.Get("MySynth");
Vst.Unload("MySynth");
```

### Customization Options
| Current | Alternative Options |
|---------|---------------------|
| `Vst.ListPlugins` | `list`, `plugins`, `available`, `scan` |
| `Vst.LoadedPlugins` | `loaded`, `active`, `running`, `instances` |
| `Vst.Load` | `load`, `open`, `create`, `add`, `plugin` |
| `Vst.Get` | `get`, `find`, `retrieve`, `plugin` |
| `Vst.Unload` | `unload`, `close`, `remove`, `delete` |

### Your Choice
```
Vst.ListPlugins   →  _________________
Vst.LoadedPlugins →  _________________
Vst.Load          →  _________________
Vst.Get           →  _________________
Vst.Unload        →  _________________
```

---

## 18. VST Plugin Control API

### Current Syntax
```csharp
Vst.Plugin(plugin)
  .Midi().From(device)
  .SetParameter(name, value)
  .SetParameter(index, value)
  .Param(name, value)
  .NoteOn(note, velocity)
  .NoteOff(note)
  .AllNotesOff()
  .ControlChange(cc, value, channel)
  .ProgramChange(program, channel)
```

### Usage Examples
```csharp
Vst.Plugin(synth).Midi().From(0);
Vst.Plugin(synth).SetParameter("Volume", 0.8);
Vst.Plugin(synth).Param("Cutoff", 0.7);
Vst.Plugin(synth).NoteOn(60, 100);
Vst.Plugin(synth).ControlChange(1, 64, 0);
```

### Customization Options
| Current | Alternative Options |
|---------|---------------------|
| `Vst.Plugin` | `plugin`, `vst`, `p`, `instance` |
| `.Midi().From` | `midi`, `from`, `input`, `source` |
| `.SetParameter` | `param`, `set`, `value`, `control` |
| `.Param` | `param`, `set`, `p`, `val` |
| `.NoteOn` | `noteOn`, `play`, `trigger`, `on` |
| `.NoteOff` | `noteOff`, `stop`, `release`, `off` |
| `.AllNotesOff` | `allOff`, `panic`, `silence`, `stop` |
| `.ControlChange` | `cc`, `control`, `ctrl`, `midi` |
| `.ProgramChange` | `program`, `preset`, `patch`, `pc` |

### Your Choice
```
Vst.Plugin     →  _________________
.Midi().From   →  _________________
.SetParameter  →  _________________
.Param         →  _________________
.NoteOn        →  _________________
.NoteOff       →  _________________
.AllNotesOff   →  _________________
.ControlChange →  _________________
.ProgramChange →  _________________
```

---

## 19. Virtual Channel Functions

### Current Syntax
```csharp
VirtualChannel.Create(name)
  .Volume(volume)
  .Start()

VirtualChannel.List()
channel.Start()
channel.Stop()
channel.SetVolume(volume)
channel.GetPipeName()
channel.GetChannel()
```

### Usage Examples
```csharp
var ch = VirtualChannel.Create("stream")
    .Volume(0.8)
    .Start();

VirtualChannel.List();
ch.Start();
ch.Stop();
ch.SetVolume(0.9);
var pipe = ch.GetPipeName();
```

### Customization Options
| Current | Alternative Options |
|---------|---------------------|
| `VirtualChannel.Create` | `vchan`, `channel`, `pipe`, `output`, `stream` |
| `VirtualChannel.List` | `list`, `show`, `all`, `channels` |
| `.Volume` | `volume`, `vol`, `gain`, `level` |
| `.Start` | `start`, `begin`, `open`, `run` |
| `.Stop` | `stop`, `close`, `end`, `kill` |
| `.SetVolume` | `volume`, `vol`, `setVol`, `gain` |
| `.GetPipeName` | `pipe`, `name`, `pipeName`, `path` |
| `.GetChannel` | `channel`, `object`, `instance` |

### Your Choice
```
VirtualChannel.Create →  _________________
VirtualChannel.List   →  _________________
.Volume               →  _________________
.Start                →  _________________
.Stop                 →  _________________
.SetVolume            →  _________________
.GetPipeName          →  _________________
```

---

## 20. Helper Functions

### Current Syntax
```csharp
Print(message)
Random()
Random(min, max)
RandomInt(min, max)
```

### Usage Examples
```csharp
Print("Hello");
var r1 = Random();           // 0.0 - 1.0
var r2 = Random(0.5, 1.5);   // 0.5 - 1.5
var i = RandomInt(1, 10);    // 1 - 10
```

### Customization Options
| Current | Alternative Options |
|---------|---------------------|
| `Print` | `print`, `log`, `console`, `write`, `echo`, `say` |
| `Random` | `random`, `rand`, `rnd`, `rng`, `r` |
| `RandomInt` | `randomInt`, `randInt`, `int`, `dice`, `roll` |

### Alternative Syntax Ideas
```csharp
// Current
Print("Message");
Random(0, 1);

// Possible alternatives
log("Message");
>> "Message";           // Output operator
rnd(0, 1);
r(0, 1);
rand[0, 1];
```

### Your Choice
```
Print      →  _________________
Random     →  _________________
RandomInt  →  _________________
```

---

## 21. Enums & Constants

### A. SynthWaveform

**Current Syntax:**
```csharp
SynthWaveform.Sine
SynthWaveform.Square
SynthWaveform.Sawtooth
SynthWaveform.Triangle
SynthWaveform.Noise
```

**Customization Options:**
| Current | Alternative Options |
|---------|---------------------|
| `SynthWaveform.Sine` | `sin`, `sine`, `wave.sin` |
| `SynthWaveform.Square` | `square`, `sqr`, `pulse`, `wave.sqr` |
| `SynthWaveform.Sawtooth` | `saw`, `sawtooth`, `wave.saw` |
| `SynthWaveform.Triangle` | `tri`, `triangle`, `wave.tri` |
| `SynthWaveform.Noise` | `noise`, `white`, `random`, `wave.noise` |

**Your Choice:**
```
SynthWaveform.Sine     →  _________________
SynthWaveform.Square   →  _________________
SynthWaveform.Sawtooth →  _________________
SynthWaveform.Triangle →  _________________
SynthWaveform.Noise    →  _________________
```

---

### B. BeatSubdivision

**Current Syntax:**
```csharp
BeatSubdivision.Eighth        // 8 PPQN
BeatSubdivision.Sixteenth     // 16 PPQN
BeatSubdivision.ThirtySecond  // 32 PPQN
BeatSubdivision.SixtyFourth   // 64 PPQN
BeatSubdivision.Standard      // 96 PPQN
BeatSubdivision.High          // 192 PPQN
BeatSubdivision.VeryHigh      // 384 PPQN
BeatSubdivision.UltraHigh     // 480 PPQN
```

**Customization Options:**
| Current | Alternative Options |
|---------|---------------------|
| All values | `ppqn8`, `ppqn16`, `sub8`, `sub16`, `res8`, `res16` |
| Or direct numbers | `8`, `16`, `32`, `96`, etc. |

**Your Choice:**
```
BeatSubdivision enum →  _________________
Preferred style: [ ] Named values [ ] Numbers
```

---

### C. TimingPrecision

**Current Syntax:**
```csharp
TimingPrecision.Standard
TimingPrecision.HighPrecision
TimingPrecision.AudioRate
```

**Customization Options:**
| Current | Alternative Options |
|---------|---------------------|
| `Standard` | `standard`, `normal`, `default`, `low` |
| `HighPrecision` | `high`, `precise`, `accurate`, `medium` |
| `AudioRate` | `ultra`, `max`, `extreme`, `highest` |

**Your Choice:**
```
TimingPrecision.Standard      →  _________________
TimingPrecision.HighPrecision →  _________________
TimingPrecision.AudioRate     →  _________________
```

---

## Special Syntax Patterns

### Operator Overloading Ideas

```csharp
// MIDI routing
midi[0] > synth              // Route MIDI to synth
midi[0] >> synth             // Alternative

// Pattern notes
pattern @ (0, 60, 100, 0.5)  // Add note at beat 0
pattern << note(0, 60)       // Stream notation

// Sample loading
sampler << "kick.wav"        // Load sample
sampler += "snare.wav"       // Add sample

// Output
>> "message"                 // Print message
<< value                     // Log value

// Volume control
synth * 0.8                  // Set volume to 80%
synth / 2                    // Half volume
```

**Would you like operator syntax?**
```
[ ] Yes, use operators where possible
[ ] No, keep function calls
[ ] Mix - operators for common operations only
```

---

### Named Note System

```csharp
// Current (MIDI numbers)
synth.NoteOn(60, 100);

// Possible alternatives
synth.play(c4, 100);
synth.play(note.c4, 100);
synth > c4 @ ff;             // Note + velocity as dynamic
synth.play("C4", 100);
```

**Would you like named notes?**
```
[ ] Yes - c3, d3, e3, etc.
[ ] Yes - note.c3, note.d3, etc.
[ ] No - keep MIDI numbers
```

---

### Velocity Names

```csharp
// Current (0-127)
synth.NoteOn(60, 100);

// Possible alternatives
synth.play(60, fff);         // Fortissimo
synth.play(60, vel.loud);
synth.play(60, "loud");

// Common velocities
ppp = 16    // Pianississimo
pp = 33     // Pianissimo
p = 49      // Piano
mp = 64     // Mezzo-piano
mf = 80     // Mezzo-forte
f = 96      // Forte
ff = 112    // Fortissimo
fff = 127   // Fortississimo
```

**Would you like named velocities?**
```
[ ] Yes - musical notation (ppp, pp, p, mf, f, ff, fff)
[ ] Yes - descriptive (soft, medium, loud)
[ ] No - keep numbers
```

---

## Your Custom Syntax Design

### Complete Example (Fill in your preferences)

```csharp
// Engine access
_________ = your Engine name

// Create synth
var bass = _________(  "bass", _________ );  // Function, waveform

// Set parameters
bass._________ = 0.8;           // Volume
bass._________ = 400;           // Filter cutoff
bass._________ = 2.0;           // Resonance

// Create pattern
var p = _________( bass, "bassline" );

// Add notes
p._________( 0, 36, 100, 0.5 );   // AddNote

// Pattern control
p._________();                     // Start
p._________();                     // Stop

// Transport
_________();                       // Start sequencer
_________(  120 );                 // Set BPM

// MIDI routing
_________[0]._________(bass);      // MIDI device to synth

// Sample instrument
var drums = _________.________("drums")
    ._________("kick.wav", 36)
    ._________("snare.wav", 38)
    ._________();

// Audio control
_________._________(0.8);          // Master volume

// Print
_________("Ready!");
```

---

## Next Steps

1. **Fill in your preferences** in each "Your Choice" section
2. **Design your ideal syntax** in the final example
3. **Provide this document to Claude** with your choices marked
4. Claude will implement all your custom syntax choices throughout the codebase

---

## Notes

- You can have **multiple aliases** for the same function
- **Backwards compatibility** can be maintained if desired
- **IDE autocomplete** will work with all chosen names
- All **functionality remains identical** - only naming changes

---

**Ready to customize? Start filling in your choices above! 🎵**
