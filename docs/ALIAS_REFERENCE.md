# MusicEngine - Complete Alias Reference

This document lists ALL available function aliases in MusicEngine. Every function can be called using multiple names - use whichever feels most natural to you!

---

## Global Functions

### Synth Creation
| Original | Aliases | Example |
|----------|---------|---------|
| `CreateSynth()` | `synth()`, `s()`, `newSynth()` | `var bass = synth();` |

**Usage:**
```csharp
var lead = CreateSynth();    // Explicit
var bass = synth();          // Concise
var kick = s();              // Ultra-short
var pad = newSynth();        // Semantic
```

### General MIDI Creation
| Original | Aliases | Example |
|----------|---------|---------|
| `CreateGeneralMidi()` | `generalMidi()`, `gmi()` | `var piano = CreateGeneralMidi();` |
| `CreateGeneralMidiInstrument(program, channel)` | `gm(program, channel)`, `newGm(program, channel)` | `var ep = gm(GeneralMidiProgram.ElectricPiano1, 1);` |

**Usage:**
```csharp
var piano = CreateGeneralMidi();       // defaults: AcousticGrand, ch0, vol 0.8, fx off
midi.device(0).to(piano);              // route input
piano.Instrument("pad");               // set GM program by name
```

--- 

### Pattern Creation
| Original | Aliases | Example |
|----------|---------|---------|
| `CreatePattern(synth)` | `pattern()`, `p()`, `newPattern()` | `var beat = p(drums);` |

**Usage:**
```csharp
var melody = CreatePattern(lead);     // Explicit
var drums = pattern(kick);            // Concise
var bass = p(basssynth);              // Ultra-short
var arp = newPattern(pad);            // Semantic
```

---

### Transport Control

#### Start/Play
| Original | Aliases | Example |
|----------|---------|---------|
| `Start()` | `play()`, `run()`, `go()` | `play();` |

**Usage:**
```csharp
Start();    // Explicit
play();     // Semantic (like a DAW)
run();      // Code-like
go();       // Ultra-short
```

#### Stop/Pause
| Original | Aliases | Example |
|----------|---------|---------|
| `Stop()` | `pause()`, `halt()` | `pause();` |

**Usage:**
```csharp
Stop();     // Explicit
pause();    // Semantic
halt();     // Dramatic
```

#### BPM/Tempo
| Original | Aliases | Example |
|----------|---------|---------|
| `SetBpm(bpm)` | `bpm()`, `tempo()`, `SetBPM()` | `bpm(140);` |

**Usage:**
```csharp
SetBpm(120);    // Explicit
bpm(140);       // Concise
tempo(160);     // Musical term
SetBPM(180);    // Alternative capitalization
BPM = 200;      // Property syntax
```

#### Skip/Jump
| Original | Aliases | Example |
|----------|---------|---------|
| `Skip(beats)` | `jump()`, `seek()` | `jump(4);` |

**Usage:**
```csharp
Skip(8);     // Explicit
jump(4);     // Action-oriented
seek(16);    // Media player style
```

---

### Sampler Creation
| Original | Aliases | Example |
|----------|---------|---------|
| `CreateSampler(name)` | `sampler()`, `sample()` | `var drums = sampler("drums");` |

**Usage:**
```csharp
var drums = CreateSampler("drums");    // Explicit
var perc = sampler("percussion");      // Concise
var fx = sample("effects");            // Short form
```

---

### Virtual Channel Creation
| Original | Aliases | Example |
|----------|---------|---------|
| `CreateVirtualChannel(name)` | `vchan()`, `channel()` | `var out1 = vchan("output");` |

**Usage:**
```csharp
var stream = CreateVirtualChannel("stream");    // Explicit
var aux1 = vchan("aux1");                       // Short form
var send = channel("send");                     // Semantic
```

---

### Print/Log
| Original | Aliases | Example |
|----------|---------|---------|
| `Print(message)` | `log()`, `write()` | `log("Starting...");` |

**Usage:**
```csharp
Print("Hello");     // Explicit
log("Debug msg");   // Developer style
write("Output");    // Simple
```

---

## FluentApi Aliases

### Audio Control (`audio`)

#### Channel Access
| Original | Aliases | Example |
|----------|---------|---------|
| `audio.channel(n)` | `audio.Channel()`, `audio.ch()`, `audio.track()` | `audio.ch(0).gain(0.8);` |

**Usage:**
```csharp
audio.channel(0).gain(0.8);    // Explicit
audio.Channel(0).gain(0.8);    // PascalCase
audio.ch(0).gain(0.8);         // Short
audio.track(0).gain(0.8);      // DAW terminology
```

#### All Channels
| Original | Aliases | Example |
|----------|---------|---------|
| `audio.all` | `audio.All`, `audio.allChannels` | `audio.All.gain(0.5);` |

**Usage:**
```csharp
audio.all.gain(0.5);           // Concise
audio.All.gain(0.5);           // PascalCase
audio.allChannels.gain(0.5);   // Explicit
```

#### Audio Input
| Original | Aliases | Example |
|----------|---------|---------|
| `audio.input(n)` | `audio.Input()`, `audio.in()`, `audio.capture()` | `audio.in(0).onFrequency(...);` |

**Usage:**
```csharp
audio.input(0).onFrequency(60, 250);     // Explicit
audio.Input(0).onFrequency(60, 250);     // PascalCase
audio.in(0).onFrequency(60, 250);        // Short
audio.capture(0).onFrequency(60, 250);   // Semantic
```

---

### MIDI Control (`midi`)

#### Device Access
| Original | Aliases | Example |
|----------|---------|---------|
| `midi.device(n)` | `midi.Device()`, `midi.dev()`, `midi.d()`, `midi.input()`, `midi.Input()`, `midi.in()` | `midi.dev(0).route(synth);` |

**Usage:**
```csharp
// By index
midi.device(0).route(synth);     // Explicit
midi.Device(0).route(synth);     // PascalCase
midi.dev(0).route(synth);        // Short
midi.d(0).route(synth);          // Ultra-short
midi.input(0).route(synth);      // Semantic
midi.in(0).route(synth);         // Very short

// By name
midi.device("Keyboard").route(synth);
midi.dev("Pad Controller").route(synth);
```

#### MIDI Output
| Original | Aliases | Example |
|----------|---------|---------|
| `midi.output(n)` | `midi.Output()`, `midi.out()` | `midi.out(0).noteOn(60);` |

**Usage:**
```csharp
// By index
midi.output(0).noteOn(60);     // Explicit
midi.Output(0).noteOn(60);     // PascalCase
midi.out(0).noteOn(60);        // Short

// By name
midi.output("Synthesizer").noteOn(60);
midi.out("External Device").cc(74, 127);
```

---

### VST Control (`vst`)

#### Load VST Plugin
| Original | Aliases | Example |
|----------|---------|---------|
| `vst.load(name)` | `vst.Load()`, `vst.LoadPlugin()`, `vst.l()` | `var synth = vst.load("Serum");` |

**Usage:**
```csharp
var plugin = vst.load("Serum");          // Concise
var vst1 = vst.Load("Massive");          // PascalCase
var vst2 = vst.LoadPlugin("Kontakt");    // Explicit
var vst3 = vst.l("Omnisphere");          // Ultra-short
var vst4 = vst.load(0);                  // Load by index
```

#### Get Loaded VST
| Original | Aliases | Example |
|----------|---------|---------|
| `vst.get(name)` | `vst.Get()`, `vst.plugin()` | `var synth = vst.plugin("Serum");` |

**Usage:**
```csharp
var plugin = vst.get("Serum");       // Concise
var vst1 = vst.Get("Massive");       // PascalCase
var vst2 = vst.plugin("Kontakt");    // Semantic
```

#### List VST Plugins
| Original | Aliases | Example |
|----------|---------|---------|
| `vst.list()` | `vst.List()`, `vst.plugins()` | `vst.plugins();` |

**Usage:**
```csharp
vst.list();       // Concise
vst.List();       // PascalCase
vst.plugins();    // Semantic
```

#### List Loaded VST Plugins
| Original | Aliases | Example |
|----------|---------|---------|
| `vst.loaded()` | `vst.Loaded()`, `vst.active()` | `vst.active();` |

**Usage:**
```csharp
vst.loaded();    // Concise
vst.Loaded();    // PascalCase
vst.active();    // Semantic
```

---

### Sample Control (`samples`)

#### Create Sampler
| Original | Aliases | Example |
|----------|---------|---------|
| `samples.create(name)` | `samples.Create()`, `samples.new()`, `samples.make()` | `var drums = samples.new("drums");` |

**Usage:**
```csharp
var s1 = samples.create("drums");     // Concise
var s2 = samples.Create("perc");      // PascalCase
var s3 = samples.new("effects");      // Semantic
var s4 = samples.make("vocals");      // Alternative
```

#### Load Sample
| Original | Aliases | Example |
|----------|---------|---------|
| `samples.load(path)` | `samples.Load()`, `samples.add()` | `var kick = samples.load("kick.wav");` |

**Usage:**
```csharp
var s1 = samples.load("kick.wav");        // Concise
var s2 = samples.Load("snare.wav");       // PascalCase
var s3 = samples.add("hihat.wav", 60);    // Alternative
```

#### Load From Directory
| Original | Aliases | Example |
|----------|---------|---------|
| `samples.fromDirectory(path)` | `samples.FromDirectory()`, `samples.fromDir()`, `samples.dir()` | `var kit = samples.dir("./drums/");` |

**Usage:**
```csharp
var kit1 = samples.fromDirectory("./drums/");    // Explicit
var kit2 = samples.FromDirectory("./perc/");     // PascalCase
var kit3 = samples.fromDir("./fx/");             // Short
var kit4 = samples.dir("./vocals/");             // Ultra-short
```

---

### Virtual Channel Control (`virtualChannels`)

#### Create Virtual Channel
| Original | Aliases | Example |
|----------|---------|---------|
| `virtualChannels.create(name)` | `virtualChannels.Create()`, `virtualChannels.new()`, `virtualChannels.make()` | `var out = virtualChannels.new("stream");` |

**Usage:**
```csharp
var ch1 = virtualChannels.create("output");    // Concise
var ch2 = virtualChannels.Create("aux");       // PascalCase
var ch3 = virtualChannels.new("send");         // Semantic
var ch4 = virtualChannels.make("stream");      // Alternative
```

#### List Virtual Channels
| Original | Aliases | Example |
|----------|---------|---------|
| `virtualChannels.list()` | `virtualChannels.List()`, `virtualChannels.show()` | `virtualChannels.show();` |

**Usage:**
```csharp
virtualChannels.list();    // Concise
virtualChannels.List();    // PascalCase
virtualChannels.show();    // Semantic
```

---

### Pattern Control (`patterns`)

#### Start Pattern
| Original | Aliases | Example |
|----------|---------|---------|
| `patterns.start(p)` | `patterns.Start()`, `patterns.play()`, `patterns.enable()` | `patterns.play(melody);` |

**Usage:**
```csharp
patterns.start(melody);     // Concise
patterns.Start(drums);      // PascalCase
patterns.play(bass);        // Semantic
patterns.enable(arp);       // Toggle-style
```

#### Stop Pattern
| Original | Aliases | Example |
|----------|---------|---------|
| `patterns.stop(p)` | `patterns.Stop()`, `patterns.pause()`, `patterns.disable()` | `patterns.pause(melody);` |

**Usage:**
```csharp
patterns.stop(melody);      // Concise
patterns.Stop(drums);       // PascalCase
patterns.pause(bass);       // Semantic
patterns.disable(arp);      // Toggle-style
```

#### Toggle Pattern
| Original | Aliases | Example |
|----------|---------|---------|
| `patterns.toggle(p)` | `patterns.Toggle()`, `patterns.switch()` | `patterns.toggle(beat);` |

**Usage:**
```csharp
patterns.toggle(melody);     // Concise
patterns.Toggle(drums);      // PascalCase
patterns.switch(bass);       // Alternative
```

---

## Advanced: MIDI Mappings

### Direct Mapping Functions

```csharp
// Route MIDI device to synth
RouteMidi(deviceIndex, synth);

// Map MIDI CC to synth parameter
MapControl(deviceIndex, cc, synth, "parameter");

// Map pitch bend
MapPitchBend(deviceIndex, synth, "parameter");

// Map CC to BPM
MapBpm(deviceIndex, cc);

// Map note to start/stop
MapStart(deviceIndex, note);
MapStop(deviceIndex, note);

// Map CC to skip beats
MapSkip(deviceIndex, cc, beats);

// Map CC to scratching
MapScratch(deviceIndex, cc, scale);
```

### Fluent MIDI Mapping

```csharp
// Route device to synth
midi.device(0).route(synth);

// Map CC to synth parameter
midi.device(0).cc(74).to(synth, "cutoff");

// Map pitch bend
midi.device(0).pitchbend().to(synth, "pitch");

// Map key range
midi.playablekeys.range(21, 108).from(0).map(synth);
midi.playablekeys.range(21, 108).low.to.high.map(synth);
midi.playablekeys.range(21, 108).high.to.low.map(synth);
```

---

## Quick Reference by Category

### 1. Synth Functions
- **CreateSynth** → `synth()`, `s()`, `newSynth()`

### 2. Pattern Functions
- **CreatePattern** → `pattern()`, `p()`, `newPattern()`

### 3. Transport Functions
- **Start** → `play()`, `run()`, `go()`
- **Stop** → `pause()`, `halt()`
- **SetBpm** → `bpm()`, `tempo()`, `SetBPM()`, property `BPM`
- **Skip** → `jump()`, `seek()`

### 4. Sampler Functions
- **CreateSampler** → `sampler()`, `sample()`
- **samples.create** → `samples.Create()`, `samples.new()`, `samples.make()`
- **samples.load** → `samples.Load()`, `samples.add()`
- **samples.fromDirectory** → `samples.FromDirectory()`, `samples.fromDir()`, `samples.dir()`

### 5. Virtual Channel Functions
- **CreateVirtualChannel** → `vchan()`, `channel()`
- **virtualChannels.create** → `virtualChannels.Create()`, `virtualChannels.new()`, `virtualChannels.make()`
- **virtualChannels.list** → `virtualChannels.List()`, `virtualChannels.show()`

### 6. Audio Control
- **audio.channel** → `audio.Channel()`, `audio.ch()`, `audio.track()`
- **audio.all** → `audio.All`, `audio.allChannels`
- **audio.input** → `audio.Input()`, `audio.in()`, `audio.capture()`

### 7. MIDI Control
- **midi.device** → `midi.Device()`, `midi.dev()`, `midi.d()`, `midi.input()`, `midi.in()`
- **midi.output** → `midi.Output()`, `midi.out()`

### 8. VST Control
- **vst.load** → `vst.Load()`, `vst.LoadPlugin()`, `vst.l()`
- **vst.get** → `vst.Get()`, `vst.plugin()`
- **vst.list** → `vst.List()`, `vst.plugins()`
- **vst.loaded** → `vst.Loaded()`, `vst.active()`

### 9. Pattern Control
- **patterns.start** → `patterns.Start()`, `patterns.play()`, `patterns.enable()`
- **patterns.stop** → `patterns.Stop()`, `patterns.pause()`, `patterns.disable()`
- **patterns.toggle** → `patterns.Toggle()`, `patterns.switch()`

### 10. Output Functions
- **Print** → `log()`, `write()`

---

## Examples Showing Different Styles

### Verbose/Explicit Style
```csharp
// Create instruments
var synthesizer = CreateSynth();
var drumMachine = CreateSampler("drums");

// Create patterns
var melodyPattern = CreatePattern(synthesizer);
melodyPattern.Note(60, 0.0, 0.5, 100);
melodyPattern.Note(64, 0.5, 0.5, 100);
melodyPattern.Note(67, 1.0, 0.5, 100);

// Setup audio
audio.channel(0).gain(0.8);
audio.all.gain(0.7);

// Configure MIDI
midi.device(0).route(synthesizer);
midi.device(0).cc(74).to(synthesizer, "cutoff");

// Control transport
SetBpm(140);
Start();
```

### Concise/Balanced Style
```csharp
// Create instruments
var lead = synth();
var drums = sampler("drums");

// Create patterns
var melody = pattern(lead);
melody.Note(60, 0.0, 0.5, 100);
melody.Note(64, 0.5, 0.5, 100);
melody.Note(67, 1.0, 0.5, 100);

// Setup audio
audio.ch(0).gain(0.8);
audio.all.gain(0.7);

// Configure MIDI
midi.dev(0).route(lead);
midi.dev(0).cc(74).to(lead, "cutoff");

// Control transport
bpm(140);
play();
```

### Ultra-Short/Compact Style
```csharp
// Create instruments
var l = s();
var d = sample("drums");

// Create patterns
var m = p(l);
m.Note(60, 0.0, 0.5, 100);
m.Note(64, 0.5, 0.5, 100);
m.Note(67, 1.0, 0.5, 100);

// Setup audio
audio.ch(0).gain(0.8);

// Configure MIDI
midi.d(0).route(l);
midi.d(0).cc(74).to(l, "cutoff");

// Control transport
bpm(140);
go();
```

### Fluent/Chaining Style
```csharp
// Create and configure sampler
var drums = samples.create("drums")
    .directory("./samples/drums/")
    .map("kick.wav", 36)
    .map("snare.wav", 38)
    .map("hihat.wav", 42)
    .volume(0.8);

// Create VST with routing
var vst1 = vst.load("Serum")
    .from(0)
    .param("cutoff", 0.5)
    .volume(0.7);

// Create virtual output
var stream = virtualChannels.create("broadcast")
    .volume(0.9)
    .start();
```

### Mixed Style (Most Common)
```csharp
// Use what feels natural for each operation
var bass = synth();
var drums = samples.dir("./kits/808/");

var bassline = p(bass);
bassline.Note(36, 0, 0.5, 100);

midi.dev("Controller").route(bass);
audio.ch(0).gain(0.8);

bpm(128);
play();
```

---

## All Three Examples Do EXACTLY the Same Thing!

The beauty of MusicEngine's alias system is **complete flexibility**. Choose the style that:
- Matches your background (music production vs. programming)
- Fits your project (quick sketches vs. complex compositions)
- Feels most comfortable to type and read

**There is no "right" way** - all aliases are first-class citizens in the API!

---

## Pattern Method Aliases

Pattern objects also have their own methods:

```csharp
var pattern = CreatePattern(synth);

// Add notes - all equivalent
pattern.Note(60, 0, 0.5, 100);          // Method call
pattern.Events.Add(new NoteEvent {...}); // Direct add

// Control playback
pattern.Play();     // Start this pattern
pattern.Stop();     // Stop this pattern

// Properties
pattern.Loop = true;           // Same as pattern.IsLooping
pattern.LoopLength = 4.0;      // Pattern length in beats
pattern.Enabled = true;        // Enable/disable pattern
```

---

## Case Sensitivity Guide

MusicEngine supports both camelCase and PascalCase for most operations:

```csharp
// These are all valid and equivalent
audio.channel(0)    // camelCase (recommended)
audio.Channel(0)    // PascalCase (works too)

midi.device(0)      // camelCase
midi.Device(0)      // PascalCase

vst.load("Serum")   // camelCase
vst.Load("Serum")   // PascalCase
```

**Recommendation**: Stick with camelCase for consistency with most scripting languages, but PascalCase works if you prefer C# conventions.

---

## Tip: Discover More

To explore what's available in your scripts:
```csharp
// List MIDI devices
// (Check documentation for available MIDI list commands)

// List VST plugins
vst.list();         // All discovered plugins
vst.loaded();       // Currently loaded plugins

// List virtual channels
virtualChannels.list();
```

Happy music making!
