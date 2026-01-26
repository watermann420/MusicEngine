# MusicEngine API Reference

**Version 1.0** | **License: MusicEngine License (MEL) - Honor-Based Commercial Support**

This document provides a comprehensive API reference for MusicEngine's public classes, methods, and interfaces.

---

## Table of Contents

1. [Core Classes](#core-classes)
   - [AudioEngine](#audioengine)
   - [Sequencer](#sequencer)
   - [Pattern](#pattern)
   - [SimpleSynth](#simplesynth)
   - [ISynth Interface](#isynth-interface)
2. [Effects](#effects)
   - [IEffect Interface](#ieffect-interface)
   - [EffectBase](#effectbase)
   - [ReverbEffect](#reverbeffect)
   - [DelayEffect](#delayeffect)
   - [ChorusEffect](#choruseffect)
   - [EffectChain](#effectchain)
3. [Recording](#recording)
   - [AudioRecorder](#audiorecorder)
   - [RecordingCaptureSampleProvider](#recordingcapturesampleprovider)
4. [VST](#vst)
   - [VstPlugin](#vstplugin)
   - [VstHost](#vsthost)
   - [VstPluginInfo](#vstplugininfo)
5. [MIDI](#midi)
   - [MIDI Routing Methods](#midi-routing-methods)
   - [MIDI Output Methods](#midi-output-methods)
   - [MidiClockSync](#midiclocksync)
6. [Scripting](#scripting)
   - [ScriptGlobals](#scriptglobals)
   - [Fluent API Classes](#fluent-api-classes)
7. [Sample Instruments](#sample-instruments)
   - [SampleInstrument](#sampleinstrument)
   - [Sample](#sample)

---

## Core Classes

### AudioEngine

The central class for handling audio and MIDI routing, mixing, and processing.

**Namespace:** `MusicEngine.Core`

#### Constructor

```csharp
public AudioEngine(int? sampleRate = null)
```

**Parameters:**
- `sampleRate` (optional): Sample rate in Hz. Defaults to `Settings.SampleRate`.

**Description:** Creates a new AudioEngine instance with the specified sample rate. Initializes the internal mixer and master volume control.

---

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `VstHost` | `VstHost` | Gets the VST plugin host instance |
| `VirtualChannels` | `VirtualChannelManager` | Gets the virtual channel manager |
| `Recorder` | `AudioRecorder` | Gets the audio recorder instance |
| `IsRecording` | `bool` | Whether recording is currently in progress |
| `RecordingDuration` | `TimeSpan` | Current recording duration |

---

#### Methods

##### Initialize

```csharp
public void Initialize()
```

**Description:** Initializes the audio engine. Sets up the default audio output, enumerates all audio/MIDI devices, and scans for VST plugins.

**Example:**
```csharp
var engine = new AudioEngine();
engine.Initialize();
```

---

##### AddSampleProvider

```csharp
public void AddSampleProvider(ISampleProvider provider)
```

**Parameters:**
- `provider`: Any ISampleProvider (synth, sampler, effect chain, etc.)

**Description:** Adds a sample provider to the mixer. Automatically resamples if the sample rate differs from the engine.

**Example:**
```csharp
var synth = new SimpleSynth();
engine.AddSampleProvider(synth);
```

---

##### SetChannelGain

```csharp
public void SetChannelGain(int index, float gain)
```

**Parameters:**
- `index`: Channel index (0-based)
- `gain`: Volume gain (0.0 to 1.0+)

**Description:** Sets the volume for a specific mixer channel.

---

##### SetAllChannelsGain

```csharp
public void SetAllChannelsGain(float gain)
```

**Parameters:**
- `gain`: Master volume gain

**Description:** Sets the master volume affecting all channels.

---

##### ClearMixer

```csharp
public void ClearMixer()
```

**Description:** Removes all inputs from the mixer.

---

##### ClearMappings

```csharp
public void ClearMappings()
```

**Description:** Clears all MIDI routings, control mappings, transport mappings, range mappings, and frequency mappings.

---

##### Dispose

```csharp
public void Dispose()
```

**Description:** Releases all resources including audio devices, MIDI devices, VST plugins, and recording resources.

---

### Sequencer

Controls timing, patterns, and playback with high-resolution timing support.

**Namespace:** `MusicEngine.Core`

#### Constructors

```csharp
public Sequencer()
public Sequencer(TimingPrecision precision)
public Sequencer(TimingPrecision precision, BeatSubdivision subdivision)
```

**Parameters:**
- `precision`: Timing precision mode (Standard, HighPrecision, or AudioRate)
- `subdivision`: Beat subdivision for tighter timing

---

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Bpm` | `double` | Gets/sets beats per minute (minimum 1.0) |
| `CurrentBeat` | `double` | Gets/sets the current beat position |
| `IsRunning` | `bool` | Whether the sequencer is currently running |
| `ActiveEvents` | `IReadOnlyList<MusicalEvent>` | Currently playing events for visualization |
| `Patterns` | `IReadOnlyList<Pattern>` | All patterns in the sequencer |
| `DefaultLoopLength` | `double` | Default loop length when no patterns exist |
| `IsScratching` | `bool` | Enables/disables scratching mode |
| `TimingPrecision` | `TimingPrecision` | Current timing precision mode |
| `Subdivision` | `BeatSubdivision` | Beat subdivision setting |
| `SampleRate` | `int` | Sample rate for audio-rate scheduling |
| `JitterCompensationEnabled` | `bool` | Whether jitter compensation is enabled |
| `AverageTimingJitter` | `double` | Current average timing jitter in ms |
| `MidiClock` | `MidiClockSync?` | MIDI clock sync instance |
| `UseMidiClockSync` | `bool` | Whether to use MIDI clock for timing |

---

#### Events

| Event | Type | Description |
|-------|------|-------------|
| `NoteTriggered` | `EventHandler<MusicalEventArgs>` | Fired when a note starts |
| `NoteEnded` | `EventHandler<MusicalEventArgs>` | Fired when a note ends |
| `BeatChanged` | `EventHandler<BeatChangedEventArgs>` | Fired on beat updates (~60fps) |
| `PlaybackStarted` | `EventHandler<PlaybackStateEventArgs>` | Fired when playback starts |
| `PlaybackStopped` | `EventHandler<PlaybackStateEventArgs>` | Fired when playback stops |
| `BpmChanged` | `EventHandler<ParameterChangedEventArgs>` | Fired when BPM changes |
| `PatternAdded` | `EventHandler<Pattern>` | Fired when a pattern is added |
| `PatternRemoved` | `EventHandler<Pattern>` | Fired when a pattern is removed |
| `PatternsCleared` | `EventHandler` | Fired when patterns are cleared |
| `TimingJitterDetected` | `EventHandler<TimingJitterEventArgs>` | Fired on timing jitter |

---

#### Methods

##### AddPattern

```csharp
public void AddPattern(Pattern pattern)
```

**Parameters:**
- `pattern`: The pattern to add

**Description:** Adds a pattern to the sequencer and links it for event emission.

---

##### RemovePattern

```csharp
public void RemovePattern(Pattern pattern)
```

**Parameters:**
- `pattern`: The pattern to remove

**Description:** Removes a pattern and stops its notes.

---

##### ClearPatterns

```csharp
public void ClearPatterns()
```

**Description:** Removes all patterns and stops all notes.

---

##### Start

```csharp
public void Start()
```

**Description:** Starts the sequencer playback.

---

##### Stop

```csharp
public void Stop()
```

**Description:** Stops the sequencer playback.

---

##### Skip

```csharp
public void Skip(double beats)
```

**Parameters:**
- `beats`: Number of beats to skip (can be negative)

**Description:** Advances the current beat position.

---

##### GetTimingStatistics

```csharp
public TimingStatistics GetTimingStatistics()
```

**Returns:** `TimingStatistics` object with timing diagnostics.

**Example:**
```csharp
sequencer.Bpm = 140;
sequencer.Start();

var stats = sequencer.GetTimingStatistics();
Console.WriteLine($"Jitter: {stats.AverageJitterMs}ms");
```

---

### Pattern

Represents a musical pattern with note events and playback properties.

**Namespace:** `MusicEngine.Core`

#### Constructor

```csharp
public Pattern(ISynth synth)
```

**Parameters:**
- `synth`: The synthesizer to play the pattern

---

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Synth` | `ISynth` | The synthesizer for this pattern |
| `Events` | `List<NoteEvent>` | Note events in the pattern |
| `LoopLength` | `double` | Loop length in beats (default 4.0) |
| `IsLooping` | `bool` | Whether the pattern loops (default true) |
| `Loop` | `bool` | Alias for IsLooping |
| `StartBeat` | `double?` | When to start the pattern |
| `Enabled` | `bool` | Whether the pattern is enabled |
| `Name` | `string` | Display name for the pattern |
| `InstrumentName` | `string` | Name of the instrument |
| `Id` | `Guid` | Unique identifier |

---

#### Methods

##### Note

```csharp
public Pattern Note(int note, double beat, double duration, int velocity)
```

**Parameters:**
- `note`: MIDI note number (0-127)
- `beat`: Beat position in pattern (0 to LoopLength)
- `duration`: Duration in beats
- `velocity`: Velocity (0-127)

**Returns:** The pattern instance for chaining.

**Description:** Adds a note to the pattern.

**Example:**
```csharp
var pattern = new Pattern(synth);
pattern.LoopLength = 4;
pattern.Note(60, 0, 0.5, 100)    // C4 at beat 0
       .Note(64, 1, 0.5, 100)    // E4 at beat 1
       .Note(67, 2, 0.5, 100);   // G4 at beat 2
```

---

##### Play

```csharp
public void Play()
```

**Description:** Registers the pattern with the sequencer and starts playback if not already running.

---

##### Stop

```csharp
public void Stop()
```

**Description:** Removes the pattern from the sequencer and stops its notes.

---

##### Process

```csharp
public void Process(double startBeat, double endBeat, double bpm = 120.0)
```

**Parameters:**
- `startBeat`: Start of the beat range to process
- `endBeat`: End of the beat range
- `bpm`: Current tempo

**Description:** Processes the pattern for the given beat range, triggering notes as needed.

---

### SimpleSynth

A simple synthesizer with multiple waveforms and basic filter.

**Namespace:** `MusicEngine.Core`

#### Constructor

```csharp
public SimpleSynth(int? sampleRate = null)
```

**Parameters:**
- `sampleRate` (optional): Sample rate. Defaults to `Settings.SampleRate`.

---

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Waveform` | `WaveType` | Current waveform (Sine, Square, Sawtooth, Triangle, Noise) |
| `Cutoff` | `float` | Lowpass filter cutoff (0.0 to 1.0) |
| `Resonance` | `float` | Filter resonance (0.0 to 1.0) |
| `Name` | `string` | Synth name for identification |
| `WaveFormat` | `WaveFormat` | Audio format |

---

#### WaveType Enum

```csharp
public enum WaveType
{
    Sine,      // Smooth sine wave (default)
    Square,    // 50% duty cycle square wave
    Sawtooth,  // Ramp up waveform
    Triangle,  // Linear up and down
    Noise      // White noise
}
```

---

#### Methods

##### NoteOn

```csharp
public void NoteOn(int note, int velocity)
```

**Parameters:**
- `note`: MIDI note number (0-127)
- `velocity`: Velocity (0-127)

**Description:** Starts a note with the specified velocity.

---

##### NoteOff

```csharp
public void NoteOff(int note)
```

**Parameters:**
- `note`: MIDI note number

**Description:** Stops the specified note with fade out.

---

##### AllNotesOff

```csharp
public void AllNotesOff()
```

**Description:** Stops all currently playing notes.

---

##### SetParameter

```csharp
public void SetParameter(string name, float value)
```

**Parameters:**
- `name`: Parameter name (case-insensitive): "waveform", "cutoff", "resonance"
- `value`: Parameter value

**Description:** Sets a synthesizer parameter by name.

**Example:**
```csharp
var synth = new SimpleSynth();
synth.Waveform = WaveType.Sawtooth;
synth.Cutoff = 0.7f;
engine.AddSampleProvider(synth);

synth.NoteOn(60, 100);  // Play middle C
```

---

### ISynth Interface

Interface for all synthesizer implementations.

**Namespace:** `MusicEngine.Core`

```csharp
public interface ISynth : ISampleProvider
{
    string Name { get; set; }
    void NoteOn(int note, int velocity);
    void NoteOff(int note);
    void AllNotesOff();
    void SetParameter(string name, float value);
}
```

---

## Effects

### IEffect Interface

Interface for audio effects in the processing chain.

**Namespace:** `MusicEngine.Core`

```csharp
public interface IEffect : ISampleProvider
{
    string Name { get; }
    float Mix { get; set; }        // Dry/wet mix (0.0 = dry, 1.0 = wet)
    bool Enabled { get; set; }     // Bypass when disabled
    void SetParameter(string name, float value);
    float GetParameter(string name);
}
```

---

### EffectBase

Abstract base class for implementing effects.

**Namespace:** `MusicEngine.Core`

#### Constructor

```csharp
protected EffectBase(ISampleProvider source, string name)
```

**Parameters:**
- `source`: The audio source to process
- `name`: Effect name

---

#### Protected Members

| Member | Type | Description |
|--------|------|-------------|
| `Source` | `ISampleProvider` | The source sample provider |
| `Channels` | `int` | Number of audio channels |
| `SampleRate` | `int` | Sample rate |

---

#### Protected Methods

##### RegisterParameter

```csharp
protected void RegisterParameter(string name, float initialValue)
```

**Description:** Registers a parameter with an initial value.

---

##### OnParameterChanged

```csharp
protected virtual void OnParameterChanged(string name, float value)
```

**Description:** Override to react to parameter changes.

---

##### ProcessSample

```csharp
protected virtual float ProcessSample(float sample, int channel)
```

**Description:** Override for simple per-sample effects.

---

##### ProcessBuffer

```csharp
protected virtual void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
```

**Description:** Override for buffer-based processing.

---

### ReverbEffect

Schroeder reverb using comb filters and allpass filters.

**Namespace:** `MusicEngine.Core`

#### Constructor

```csharp
public ReverbEffect(ISampleProvider source)
```

---

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `RoomSize` | `float` | Room size (0.0-1.0). Larger = longer tail |
| `Damping` | `float` | Damping (0.0-1.0). Higher = darker sound |
| `Mix` | `float` | Dry/wet mix (inherited from EffectBase) |

**Example:**
```csharp
var reverb = new ReverbEffect(synth);
reverb.RoomSize = 0.7f;
reverb.Damping = 0.3f;
reverb.Mix = 0.4f;
engine.AddSampleProvider(reverb);
```

---

### DelayEffect

Delay/echo effect with feedback.

**Namespace:** `MusicEngine.Core`

#### Constructor

```csharp
public DelayEffect(ISampleProvider source)
```

---

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `DelayTime` | `float` | Delay time in milliseconds (1-5000) |
| `Feedback` | `float` | Feedback amount (0.0-0.99) |
| `Mix` | `float` | Dry/wet mix |

---

#### Methods

##### Clear

```csharp
public void Clear()
```

**Description:** Clears the delay buffers.

**Example:**
```csharp
var delay = new DelayEffect(synth);
delay.DelayTime = 375;   // Dotted eighth at 120 BPM
delay.Feedback = 0.5f;
delay.Mix = 0.3f;
```

---

### ChorusEffect

LFO-modulated delay for rich, detuned sounds.

**Namespace:** `MusicEngine.Core`

#### Constructor

```csharp
public ChorusEffect(ISampleProvider source)
```

---

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Rate` | `float` | LFO rate in Hz (0.1-10.0) |
| `Depth` | `float` | Modulation depth (0.0-1.0) |
| `Mix` | `float` | Dry/wet mix |

---

#### Methods

##### Clear

```csharp
public void Clear()
```

**Description:** Clears buffers and resets LFO phases.

---

### EffectChain

Chains multiple effects in series.

**Namespace:** `MusicEngine.Core`

#### Constructor

```csharp
public EffectChain(ISampleProvider source)
```

---

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Count` | `int` | Number of effects in the chain |
| `Bypassed` | `bool` | Bypass the entire chain |
| `this[int]` | `IEffect?` | Get effect by index |

---

#### Methods

##### AddEffect<T>

```csharp
public T AddEffect<T>() where T : class, IEffect
```

**Returns:** The created effect instance.

**Description:** Creates and adds an effect of type T.

---

##### AddEffect

```csharp
public void AddEffect(IEffect effect)
```

**Description:** Adds an existing effect instance.

---

##### InsertEffect<T>

```csharp
public T InsertEffect<T>(int index) where T : class, IEffect
```

**Description:** Inserts an effect at the specified index.

---

##### RemoveEffect

```csharp
public bool RemoveEffect(int index)
public bool RemoveEffect(string name)
```

**Description:** Removes an effect by index or name.

---

##### GetEffect

```csharp
public IEffect? GetEffect(string name)
public T? GetEffect<T>() where T : class, IEffect
```

**Description:** Gets an effect by name or type.

---

##### SetEffectEnabled

```csharp
public bool SetEffectEnabled(string name, bool enabled)
public bool SetEffectEnabled(int index, bool enabled)
```

**Description:** Enables or disables an effect.

---

##### Clear

```csharp
public void Clear()
```

**Description:** Removes all effects from the chain.

---

##### CreateStandardChain (static)

```csharp
public static EffectChain CreateStandardChain(
    ISampleProvider source,
    bool includeReverb = true,
    bool includeDelay = true,
    bool includeChorus = true)
```

**Description:** Creates a pre-configured effect chain with common effects (disabled by default).

**Example:**
```csharp
var chain = new EffectChain(synth);
chain.AddEffect<ChorusEffect>().Depth = 0.5f;
chain.AddEffect<DelayEffect>().DelayTime = 250;
chain.AddEffect<ReverbEffect>().RoomSize = 0.6f;

chain.SetEffectEnabled("Reverb", true);
engine.AddSampleProvider(chain);
```

---

## Recording

### AudioRecorder

Records master output to WAV or MP3 files.

**Namespace:** `MusicEngine.Core`

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `IsRecording` | `bool` | Whether recording is in progress |
| `RecordingDuration` | `TimeSpan` | Current recording duration |
| `CurrentOutputPath` | `string` | Current output file path |
| `SampleRate` | `int` | Sample rate for recording |
| `Channels` | `int` | Number of channels |
| `BitDepth` | `int` | Bit depth (16, 24, or 32) |
| `CaptureProvider` | `RecordingCaptureSampleProvider?` | Current capture provider |

---

#### Events

| Event | Type | Description |
|-------|------|-------------|
| `RecordingStarted` | `EventHandler<RecordingEventArgs>` | Fired when recording starts |
| `RecordingStopped` | `EventHandler<RecordingEventArgs>` | Fired when recording stops |

---

#### Methods

##### CreateCaptureProvider

```csharp
public RecordingCaptureSampleProvider CreateCaptureProvider(ISampleProvider source)
```

**Parameters:**
- `source`: The audio source to capture (e.g., master output)

**Returns:** A capture provider that wraps the source.

**Description:** Creates a capture wrapper for recording.

---

##### StartRecording

```csharp
public void StartRecording(string outputPath, RecordingCaptureSampleProvider? captureProvider = null)
```

**Parameters:**
- `outputPath`: Path for the output WAV file
- `captureProvider` (optional): Capture provider to use

**Description:** Starts recording to the specified file.

---

##### StopRecording

```csharp
public string? StopRecording()
```

**Returns:** The path of the recorded file, or null if not recording.

**Description:** Stops the current recording.

---

##### ExportToMp3

```csharp
public bool ExportToMp3(string wavPath, string? mp3Path = null, int bitRate = 320)
```

**Parameters:**
- `wavPath`: Path to the source WAV file
- `mp3Path` (optional): Output MP3 path. Defaults to same name with .mp3 extension
- `bitRate`: MP3 bit rate in kbps (default 320)

**Returns:** True if export succeeded.

**Description:** Exports a WAV file to MP3. Requires NAudio.Lame package.

---

##### ExportWav

```csharp
public bool ExportWav(string inputPath, string outputPath, int? sampleRate = null, int? bitDepth = null)
```

**Parameters:**
- `inputPath`: Source WAV file
- `outputPath`: Output WAV file
- `sampleRate` (optional): Target sample rate
- `bitDepth` (optional): Target bit depth

**Returns:** True if export succeeded.

**Description:** Exports a WAV file with different sample rate or bit depth.

**Example:**
```csharp
// Using AudioEngine's recording methods
engine.StartRecording("output.wav");
// ... play music ...
string path = engine.StopRecording();

// Export to MP3
engine.ExportToMp3(path);

// Or directly with AudioRecorder
var recorder = new AudioRecorder();
recorder.BitDepth = 24;
var capture = recorder.CreateCaptureProvider(masterOutput);
recorder.StartRecording("recording.wav", capture);
```

---

### RecordingCaptureSampleProvider

Sample provider wrapper that captures audio while passing it through.

**Namespace:** `MusicEngine.Core`

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `WaveFormat` | `WaveFormat` | Wave format of the source |
| `TotalSamplesCaptured` | `long` | Total samples captured |
| `RecordingDuration` | `TimeSpan` | Duration based on samples captured |

---

#### Methods

##### StartCapture

```csharp
public void StartCapture(int bufferSizeInSeconds = 60)
```

**Description:** Starts capturing to the internal buffer.

---

##### StopCapture

```csharp
public void StopCapture()
```

**Description:** Stops capturing.

---

##### ReadCapturedSamples

```csharp
public int ReadCapturedSamples(float[] buffer, int offset, int count)
```

**Description:** Reads captured samples from the buffer.

---

## VST

### VstPlugin

VST plugin wrapper implementing ISynth for seamless integration.

**Namespace:** `MusicEngine.Core`

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Info` | `VstPluginInfo` | Plugin information |
| `Name` | `string` | Plugin name |
| `IsInstrument` | `bool` | True for VSTi, false for effects |
| `WaveFormat` | `WaveFormat` | Audio format |

---

#### Methods

##### NoteOn

```csharp
public void NoteOn(int note, int velocity)
```

**Description:** Sends a MIDI Note On to the plugin.

---

##### NoteOff

```csharp
public void NoteOff(int note)
```

**Description:** Sends a MIDI Note Off to the plugin.

---

##### AllNotesOff

```csharp
public void AllNotesOff()
```

**Description:** Stops all notes and sends All Notes Off CC.

---

##### SendControlChange

```csharp
public void SendControlChange(int channel, int controller, int value)
```

**Parameters:**
- `channel`: MIDI channel (0-15)
- `controller`: CC number (0-127)
- `value`: CC value (0-127)

**Description:** Sends a Control Change message.

---

##### SendPitchBend

```csharp
public void SendPitchBend(int channel, int value)
```

**Parameters:**
- `channel`: MIDI channel
- `value`: Pitch bend value (0-16383, 8192 = center)

**Description:** Sends a Pitch Bend message.

---

##### SendProgramChange

```csharp
public void SendProgramChange(int channel, int program)
```

**Description:** Sends a Program Change message for preset selection.

---

##### SetParameter

```csharp
public void SetParameter(string name, float value)
```

**Parameters:**
- `name`: Parameter name ("volume", "gain", "level", "pitchbend", or numeric index)
- `value`: Parameter value (0.0-1.0)

**Description:** Sets a plugin parameter by name.

---

##### SetParameterByIndex

```csharp
public void SetParameterByIndex(int index, float value)
```

**Description:** Sets a parameter by its numeric index.

---

##### ProcessInput

```csharp
public void ProcessInput(float[] input, float[] output, int sampleCount)
```

**Description:** Processes audio through the VST effect.

---

### VstHost

Central VST host for plugin discovery, loading, and lifecycle management.

**Namespace:** `MusicEngine.Core`

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `DiscoveredPlugins` | `IReadOnlyList<VstPluginInfo>` | All discovered plugins |
| `LoadedPlugins` | `IReadOnlyDictionary<string, VstPlugin>` | Currently loaded plugins |

---

#### Methods

##### ScanForPlugins

```csharp
public List<VstPluginInfo> ScanForPlugins()
```

**Returns:** List of discovered plugin info.

**Description:** Scans configured paths for VST2 and VST3 plugins.

---

##### LoadPlugin

```csharp
public VstPlugin? LoadPlugin(string nameOrPath)
```

**Parameters:**
- `nameOrPath`: Plugin name (partial match supported) or full path

**Returns:** The loaded plugin, or null if not found.

**Description:** Loads a VST plugin by name or path.

---

##### LoadPluginByIndex

```csharp
public VstPlugin? LoadPluginByIndex(int index)
```

**Description:** Loads a plugin by its index in the discovered list.

---

##### GetPlugin

```csharp
public VstPlugin? GetPlugin(string name)
```

**Description:** Gets a loaded plugin by name.

---

##### UnloadPlugin

```csharp
public void UnloadPlugin(string name)
```

**Description:** Unloads a plugin and releases its resources.

---

##### PrintDiscoveredPlugins

```csharp
public void PrintDiscoveredPlugins()
```

**Description:** Prints all discovered plugins to the console.

---

##### PrintLoadedPlugins

```csharp
public void PrintLoadedPlugins()
```

**Description:** Prints all loaded plugins to the console.

**Example:**
```csharp
// Scan and list plugins
var plugins = engine.ScanVstPlugins();
engine.PrintVstPlugins();

// Load a plugin
var synth = engine.LoadVstPlugin("Serum");
if (synth != null)
{
    engine.RouteMidiToVst(0, synth);
    synth.SendProgramChange(0, 5);  // Select preset 5
    synth.NoteOn(60, 100);
}
```

---

### VstPluginInfo

Information about a discovered VST plugin.

**Namespace:** `MusicEngine.Core`

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Plugin name |
| `Path` | `string` | Full path to the plugin file |
| `Vendor` | `string` | Plugin vendor/manufacturer |
| `Version` | `string` | Plugin version |
| `UniqueId` | `int` | Plugin unique ID |
| `IsInstrument` | `bool` | True for VSTi instruments |
| `IsLoaded` | `bool` | Whether currently loaded |
| `NumInputs` | `int` | Number of audio inputs |
| `NumOutputs` | `int` | Number of audio outputs |
| `NumParameters` | `int` | Number of parameters |

---

## MIDI

### MIDI Routing Methods

Methods on AudioEngine for MIDI routing and control mapping.

#### RouteMidiInput

```csharp
public void RouteMidiInput(int deviceIndex, ISynth synth)
```

**Description:** Routes all MIDI notes from a device to a synthesizer.

---

#### MapMidiControl

```csharp
public void MapMidiControl(int deviceIndex, int controlNumber, ISynth synth, string parameter)
```

**Description:** Maps a MIDI CC to a synth parameter. Use -1 for pitch bend.

---

#### MapTransportControl

```csharp
public void MapTransportControl(int deviceIndex, int controlNumber, Action<float> action)
```

**Description:** Maps a MIDI CC to a transport action with normalized value (0-1).

---

#### MapTransportNote

```csharp
public void MapTransportNote(int deviceIndex, int noteNumber, Action<float> action)
```

**Description:** Maps a MIDI note to a transport action (1.0 for note on, 0.0 for note off).

---

#### MapRange

```csharp
public void MapRange(int deviceIndex, int startNote, int endNote, ISynth synth, bool reversed = false)
```

**Description:** Maps a range of MIDI notes to a synth, with optional reversal.

---

#### AddFrequencyMapping

```csharp
public void AddFrequencyMapping(FrequencyMidiMapping mapping)
```

**Description:** Adds an audio frequency to MIDI trigger mapping.

---

#### GetMidiDeviceIndex

```csharp
public int GetMidiDeviceIndex(string name)
```

**Returns:** Device index or -1 if not found.

**Description:** Gets MIDI input device index by name (partial match).

---

#### GetMidiOutputDeviceIndex

```csharp
public int GetMidiOutputDeviceIndex(string name)
```

**Returns:** Device index or -1 if not found.

**Description:** Gets MIDI output device index by name.

---

### MIDI Output Methods

Methods for sending MIDI to external devices.

#### SendNoteOn

```csharp
public void SendNoteOn(int outputIndex, int channel, int note, int velocity)
```

**Description:** Sends Note On to a MIDI output.

---

#### SendNoteOff

```csharp
public void SendNoteOff(int outputIndex, int channel, int note)
```

**Description:** Sends Note Off to a MIDI output.

---

#### SendControlChange

```csharp
public void SendControlChange(int outputIndex, int channel, int controller, int value)
```

**Description:** Sends Control Change to a MIDI output.

---

#### SendMidiMessage

```csharp
public void SendMidiMessage(int outputIndex, int status, int data1, int data2)
```

**Description:** Sends a raw MIDI message.

**Example:**
```csharp
// Route MIDI input 0 to synth
engine.RouteMidiInput(0, synth);

// Map CC1 (mod wheel) to filter cutoff
engine.MapMidiControl(0, 1, synth, "cutoff");

// Map CC7 to volume
engine.MapTransportControl(0, 7, val => engine.SetAllChannelsGain(val));

// Send MIDI to external device
int outputIndex = engine.GetMidiOutputDeviceIndex("loopMIDI");
engine.SendNoteOn(outputIndex, 0, 60, 100);
```

---

### MidiClockSync

MIDI clock synchronization for professional-grade timing.

**Namespace:** `MusicEngine.Core`

#### Constants

| Constant | Value | Description |
|----------|-------|-------------|
| `PulsesPerQuarterNote` | 24 | Standard MIDI clock PPQN |
| `SppUnitsPerBeat` | 4 | Song Position Pointer units per beat |

---

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Mode` | `MidiClockMode` | Internal, External, or Both |
| `Bpm` | `double` | Internal BPM (20-300) |
| `ExternalBpm` | `double` | Estimated BPM from external source |
| `EffectiveBpm` | `double` | External if External mode, else internal |
| `IsRunning` | `bool` | Whether clock is running |
| `CurrentBeatPosition` | `double` | Current beat position |
| `SongPositionPointer` | `int` | Current SPP value |
| `PulseCount` | `long` | Total pulses since start |

---

#### Events

| Event | Type | Description |
|-------|------|-------------|
| `ClockPulse` | `EventHandler<MidiClockEventArgs>` | Fired on each clock pulse |
| `Started` | `EventHandler<MidiClockEventArgs>` | Fired on Start message |
| `Stopped` | `EventHandler<MidiClockEventArgs>` | Fired on Stop message |
| `Continued` | `EventHandler<MidiClockEventArgs>` | Fired on Continue message |
| `PositionChanged` | `EventHandler<MidiClockEventArgs>` | Fired on SPP change |
| `ExternalBpmDetected` | `EventHandler<MidiClockEventArgs>` | Fired when external BPM detected |

---

#### Methods

##### AddMidiOutput

```csharp
public void AddMidiOutput(int deviceIndex, MidiOut midiOut)
```

**Description:** Adds a MIDI output for sending clock messages.

---

##### RemoveMidiOutput

```csharp
public void RemoveMidiOutput(int deviceIndex)
```

**Description:** Removes a MIDI output from clock sending.

---

##### Start

```csharp
public void Start(bool fromBeginning = true)
```

**Parameters:**
- `fromBeginning`: If true, sends Start. If false, sends Continue.

**Description:** Starts the MIDI clock.

---

##### Stop

```csharp
public void Stop()
```

**Description:** Stops the MIDI clock.

---

##### SetPosition

```csharp
public void SetPosition(double beatPosition)
```

**Description:** Sets the song position and sends SPP message.

---

##### ProcessIncomingMidi

```csharp
public void ProcessIncomingMidi(int deviceIndex, MidiEvent midiEvent)
```

**Description:** Processes incoming MIDI for clock sync.

---

##### ProcessRawMidiMessage

```csharp
public void ProcessRawMidiMessage(int deviceIndex, int status, int data1 = 0, int data2 = 0)
```

**Description:** Processes a raw MIDI message for clock sync.

**Example:**
```csharp
var midiClock = new MidiClockSync();
midiClock.Bpm = 120;
midiClock.Mode = MidiClockMode.Internal;

// Add output device
midiClock.AddMidiOutput(0, engine.GetMidiOutput(0));

// Link to sequencer
sequencer.MidiClock = midiClock;
sequencer.UseMidiClockSync = true;

midiClock.Start();
```

---

## Scripting

### ScriptGlobals

Global objects and helper methods available in scripts.

**Namespace:** `MusicEngine.Scripting`

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Engine` | `AudioEngine` | The audio engine instance |
| `Sequencer` | `Sequencer` | The sequencer instance |
| `engine` | `AudioEngine` | Lowercase alias |
| `sequencer` | `Sequencer` | Lowercase alias |
| `Synth` | `SimpleSynth` | Default synth (created on first access) |
| `BPM` | `double` | Get/set sequencer BPM |

---

#### Fluent API Accessors

| Property | Type | Description |
|----------|------|-------------|
| `audio` | `AudioControl` | Audio channel control |
| `midi` | `MidiControl` | MIDI routing control |
| `vst` | `VstControl` | VST plugin control |
| `samples` | `SampleControl` | Sample instrument control |
| `patterns` | `PatternControl` | Pattern control |
| `virtualChannels` | `VirtualChannelControl` | Virtual channel control |

---

#### Methods

##### CreateSynth

```csharp
public SimpleSynth CreateSynth()
```

**Returns:** A new SimpleSynth added to the engine.

---

##### CreatePattern

```csharp
public Pattern CreatePattern()
public Pattern CreatePattern(ISynth synth)
```

**Returns:** A new Pattern linked to the sequencer.

---

##### RouteMidi

```csharp
public void RouteMidi(int deviceIndex, ISynth synth)
```

**Description:** Routes MIDI input to a synth.

---

##### MapControl

```csharp
public void MapControl(int deviceIndex, int cc, ISynth synth, string param)
```

**Description:** Maps a CC to a synth parameter.

---

##### MapPitchBend

```csharp
public void MapPitchBend(int deviceIndex, ISynth synth, string param)
```

**Description:** Maps pitch bend to a synth parameter.

---

##### MapBpm

```csharp
public void MapBpm(int deviceIndex, int cc)
```

**Description:** Maps a CC to BPM control (60-200).

---

##### MapStart / MapStop

```csharp
public void MapStart(int deviceIndex, int note)
public void MapStop(int deviceIndex, int note)
```

**Description:** Maps MIDI notes to transport controls.

---

##### MapSkip

```csharp
public void MapSkip(int deviceIndex, int cc, double beats)
```

**Description:** Maps a CC to skip beats.

---

##### MapScratch

```csharp
public void MapScratch(int deviceIndex, int cc, double scale = 16.0)
```

**Description:** Maps a CC to scratching behavior.

---

##### Start / Stop

```csharp
public void Start()
public void Stop()
```

**Description:** Start/stop the sequencer.

---

##### SetBpm / SetBPM

```csharp
public void SetBpm(double bpm)
public void SetBPM(double bpm)
```

**Description:** Sets the sequencer BPM.

---

##### Skip

```csharp
public void Skip(double beats)
```

**Description:** Skips beats in the sequencer.

---

##### Random / RandomInt

```csharp
public float Random(float min, float max)
public int RandomInt(int min, int max)
```

**Description:** Generate random numbers.

---

##### Print

```csharp
public void Print(string message)
```

**Description:** Prints to the console.

---

##### AddFrequencyTrigger

```csharp
public void AddFrequencyTrigger(int deviceIndex, float low, float high, float threshold, Action<float> action)
```

**Description:** Adds a frequency-based trigger.

---

##### LoadVst / GetVst

```csharp
public VstPlugin? LoadVst(string nameOrPath)
public VstPlugin? LoadVstByIndex(int index)
public VstPlugin? GetVst(string name)
```

**Description:** VST plugin management.

---

##### CreateSampler

```csharp
public SampleInstrument CreateSampler(string? name = null)
public SampleInstrument CreateSamplerFromFile(string filePath, int rootNote = 60)
public SampleInstrument CreateSamplerFromDirectory(string directoryPath)
```

**Description:** Creates sample instruments.

---

##### CreateVirtualChannel

```csharp
public VirtualAudioChannel CreateVirtualChannel(string name)
```

**Description:** Creates a virtual audio channel.

**Example Script:**
```csharp
// Create instruments
var lead = CreateSynth();
lead.Waveform = WaveType.Sawtooth;

var bass = CreateSynth();
bass.Waveform = WaveType.Square;

// Create patterns
var leadPattern = CreatePattern(lead);
leadPattern.LoopLength = 4;
leadPattern.Note(72, 0, 1, 100)
           .Note(74, 1, 0.5, 90)
           .Note(76, 2, 1.5, 100);

var bassPattern = CreatePattern(bass);
bassPattern.LoopLength = 4;
bassPattern.Note(36, 0, 0.5, 100)
           .Note(36, 2, 0.5, 100);

// Start playback
Sequencer.AddPattern(leadPattern);
Sequencer.AddPattern(bassPattern);
BPM = 128;
Start();

// Map MIDI controls
midi.device(0).route(lead);
midi.device(0).cc(1).to(lead, "cutoff");
```

---

### Fluent API Classes

#### MidiControl

```csharp
midi.device(0).route(synth);           // Route device to synth
midi.device("Launchpad").route(synth); // Route by name
midi.device(0).cc(1).to(synth, "cutoff");  // Map CC
midi.device(0).pitchbend().to(synth, "pitch");
midi.playablekeys.range(21, 108).from(0).map(synth);
midi.output(0).noteOn(60, 100).noteOff(60);
```

---

#### AudioControl

```csharp
audio.channel(0).gain(0.8f);    // Set channel volume
audio.all.gain(0.9f);           // Set master volume
audio.input(0).onFrequency(100, 200).threshold(0.1f).trigger(synth, 36);
```

---

#### VstControl

```csharp
var plugin = vst.load("Serum");        // Load plugin
plugin.from(0);                         // Route MIDI
plugin.param("cutoff", 0.5f);          // Set parameter
plugin.volume(0.8f);                   // Set volume
plugin.noteOn(60, 100);                // Play note
vst.list();                            // List all plugins
vst.loaded();                          // List loaded plugins
```

---

#### SampleControl

```csharp
var drums = samples.create("Drums")
    .directory("C:/Samples/Drums")
    .map("kick.wav", 36)
    .map("snare.wav", 38)
    .map("hihat.wav", 42)
    .volume(1.0f);

var piano = samples.load("piano.wav", 60);  // Load single sample
```

---

#### PatternControl

```csharp
patterns.start(myPattern);    // Enable pattern
patterns.stop(myPattern);     // Disable pattern
patterns.toggle(myPattern);   // Toggle enabled state
```

---

#### VirtualChannelControl

```csharp
var channel = virtualChannels.create("Output1")
    .volume(1.0f)
    .start();

string pipeName = channel.pipeName;  // For connecting external apps
```

---

## Sample Instruments

### SampleInstrument

Sample-based instrument for playing audio files.

**Namespace:** `MusicEngine.Core`

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Instrument name |
| `Volume` | `float` | Master volume (0.0-2.0) |
| `MaxVoices` | `int` | Maximum polyphony (default 32) |
| `WaveFormat` | `WaveFormat` | Audio format |

---

#### Methods

##### SetSampleDirectory

```csharp
public void SetSampleDirectory(string path)
```

**Description:** Sets the directory for loading samples.

---

##### LoadSample

```csharp
public Sample? LoadSample(string pathOrName, int rootNote = 60)
```

**Parameters:**
- `pathOrName`: File path or name (with auto extension detection)
- `rootNote`: MIDI note for original pitch (default 60/C4)

**Returns:** The loaded sample, or null on failure.

**Description:** Loads an audio file as a sample.

---

##### MapSampleToNote

```csharp
public void MapSampleToNote(Sample sample, int note)
public void MapSampleToNote(string sampleName, int note)
```

**Description:** Maps a sample to a specific MIDI note (for drum pads).

---

##### SetSampleRange

```csharp
public void SetSampleRange(Sample sample, int lowNote, int highNote)
```

**Description:** Sets the note range a sample responds to.

---

##### GetSample

```csharp
public Sample? GetSample(string name)
```

**Description:** Gets a loaded sample by name.

---

##### NoteOn / NoteOff / AllNotesOff

```csharp
public void NoteOn(int note, int velocity)
public void NoteOff(int note)
public void AllNotesOff()
```

**Description:** ISynth implementation for playing samples.

---

##### SetParameter

```csharp
public void SetParameter(string name, float value)
```

**Parameters:** Supports "volume", "gain", "level".

**Example:**
```csharp
var sampler = new SampleInstrument();
sampler.SetSampleDirectory("C:/Samples");
sampler.Volume = 0.8f;

// Load and map drum samples
var kick = sampler.LoadSample("kick.wav", 36);
sampler.MapSampleToNote(kick, 36);

var snare = sampler.LoadSample("snare.wav", 38);
sampler.MapSampleToNote(snare, 38);

// Load a melodic sample with pitch shifting
var piano = sampler.LoadSample("piano_c4.wav", 60);
piano.LowNote = 48;
piano.HighNote = 72;

engine.AddSampleProvider(sampler);
engine.RouteMidiInput(0, sampler);
```

---

### Sample

Represents a loaded audio sample.

**Namespace:** `MusicEngine.Core`

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Sample name |
| `FilePath` | `string` | Source file path |
| `AudioData` | `float[]` | Raw audio data |
| `WaveFormat` | `WaveFormat` | Audio format |
| `RootNote` | `int` | MIDI note for original pitch (default 60) |
| `Volume` | `float` | Sample volume (default 1.0) |
| `LowNote` | `int` | Lowest note in range (default 0) |
| `HighNote` | `int` | Highest note in range (default 127) |

---

## Enums Reference

### TimingPrecision

```csharp
public enum TimingPrecision
{
    Standard,      // Thread.Sleep timing (low CPU)
    HighPrecision, // Sub-millisecond accuracy
    AudioRate      // Sample-accurate (highest CPU)
}
```

### BeatSubdivision

```csharp
public enum BeatSubdivision
{
    None = 1,
    Eighth = 2,
    Sixteenth = 4,
    ThirtySecond = 8,
    SixtyFourth = 16,
    Tick = 24,           // 24 PPQN (MIDI standard)
    HighResolution = 96, // 96 PPQN (DAW standard)
    UltraResolution = 480 // 480 PPQN (professional)
}
```

### MidiClockMode

```csharp
public enum MidiClockMode
{
    Internal,  // Generate clock internally
    External,  // Sync to external clock
    Both       // Generate and receive
}
```

### WaveType

```csharp
public enum WaveType
{
    Sine,
    Square,
    Sawtooth,
    Triangle,
    Noise
}
```

---

## Complete Example

```csharp
using MusicEngine.Core;
using MusicEngine.Scripting;

// Initialize the engine
var engine = new AudioEngine(44100);
engine.Initialize();

var sequencer = new Sequencer(TimingPrecision.HighPrecision);

// Create instruments
var lead = new SimpleSynth();
lead.Waveform = WaveType.Sawtooth;
lead.Cutoff = 0.8f;

var bass = new SimpleSynth();
bass.Waveform = WaveType.Square;
bass.Cutoff = 0.5f;

// Add effects
var reverbChain = new EffectChain(lead);
var reverb = new ReverbEffect(lead);
reverb.RoomSize = 0.6f;
reverb.Mix = 0.3f;
reverbChain.AddEffect(reverb);

// Add to engine
engine.AddSampleProvider(reverbChain);
engine.AddSampleProvider(bass);

// Create patterns
var leadPattern = new Pattern(lead)
{
    LoopLength = 8,
    Name = "Lead"
};
leadPattern.Note(72, 0, 1, 100)
           .Note(74, 1, 0.5, 90)
           .Note(76, 2, 2, 100)
           .Note(74, 4, 1, 85)
           .Note(72, 6, 2, 100);

var bassPattern = new Pattern(bass)
{
    LoopLength = 8,
    Name = "Bass"
};
for (int i = 0; i < 8; i++)
{
    bassPattern.Note(36, i, 0.25, 100);
}

// Add patterns to sequencer
sequencer.AddPattern(leadPattern);
sequencer.AddPattern(bassPattern);

// Setup MIDI
int midiDevice = engine.GetMidiDeviceIndex("Launchpad");
if (midiDevice >= 0)
{
    engine.RouteMidiInput(midiDevice, lead);
    engine.MapMidiControl(midiDevice, 1, lead, "cutoff");
}

// Setup recording
engine.StartRecording("output.wav");

// Start playback
sequencer.Bpm = 128;
sequencer.Start();

// Wait for user input
Console.WriteLine("Press Enter to stop...");
Console.ReadLine();

// Stop and cleanup
sequencer.Stop();
string recordedFile = engine.StopRecording();
engine.ExportToMp3(recordedFile);

engine.Dispose();
```

---

*MusicEngine API Reference - Copyright (c) 2026 MusicEngine Watermann420 and Contributors*
