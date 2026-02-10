# Functions and Settings (Current Project State)

Short overview of existing functions, settings, and script APIs.
Focus: public APIs, script glue, engine and audio settings. Details and examples
live in `Docs/`.

---

## 1) Global Engine Settings (`MusicEngine.Core.Settings`)

- `SampleRate` (int, default 44100): global sample rate.
- `WavBitDepth` (int, default 32): WAV bit depth for recordings.
- `Channels` (int, default 2): global channel count (1=mono, 2=stereo).
- `BitRateKbps` (int, default 192): default bitrate for compressed formats.
- `OutputBitDepth` (int, default 16): output bit depth (32 = no quantization).
- `MasterSafetyEnabled` (bool, default false): limiter/soft-clip enabled.
- `AudioSilenceThreshold` (float, default 1e-5): silence threshold for idle.
- `AudioEffectsEnabled` (bool, default true): non-VST audio effects enabled.
- `VstInstrumentsEnabled` (bool, default true): VST instruments enabled.
- `VstEffectsEnabled` (bool, default true): VST effects enabled.
- `SequencerEnabled` (bool, default true): sequencer loop enabled.
- `VstInstrumentSleepWhenIdle` (bool, default true): idle sleep for VST instruments.
- `VstEffectSleepWhenIdle` (bool, default true): idle sleep for VST effects.
- `VstIdleThreshold` (float, default 2e-4): idle threshold for VST.
- `VstIdleTimeoutSeconds` (double, default 0.15): idle timeout for VST.

---

## 2) Timing/Groove (`MusicEngine.Timing`)

### `TimingMaster`
- `Bpm` (double, default 120.0)
- `Groove` -> `GrooveSettings`
- `EnableGroove` (bool, default true)
- `EnableHumanize` (bool, default true)

### `GrooveSettings`
- `Swing` (double, 0..1)
- `Humanize` -> `HumanizeSettings`

### `HumanizeSettings`
- `TimeMs` (double): max timing jitter in ms
- `Velocity` (double): velocity jitter multiplier
- `Seed` (int?): RNG seed for deterministic humanize

### `TimingSettings` (per pattern)
- `Bpm` (double?)
- `UseMasterGroove` (bool, default true)
- `Groove` -> `GrooveSettings`

---

## 3) Engine Embed API (`MusicEngine.Scripting.EngineScriptInterface`)

### Options (`EngineScriptInterfaceOptions`)
- `EnableVstScanning` (bool, default true)
- `StartSequencerOnStartup` (bool, default true)
- `SampleRate` (int?)

### Interface Methods (`IEngineScriptInterface`)
- `StartupAsync(startupScript?)`
- `RunScriptAsync(code, clearState?, skipIfUnchanged?)`
- `GetStateSnapshot()`
- `SetEditorMode(bool)`
- `RegisterPatternForEditor(Pattern)`
- `Sleep()`, `Wake()`
- Properties: `Engine`, `Sequencer`, `Host`, `VstRegistry`, `IsSleeping`
- Events: `EditorPatternNote`, `EditorMidiNote`, `EditorMidiDeviceActive`

---

## 4) Script Globals / Main API (`MusicEngine.Scripting.ScriptGlobals`)

### Instrument/Audio Creation
- `CreateSynth()`, `CreateGeneralMidi()`, `CreateSampler()`
- `CreateMic(int|string)` / `CreateInput(int|string)` / `Mic(...)` / `Input(...)`
- `CreateDeck(string)` / `Deck(string)`
- `CreateAudioClip(path)` -> `AudioClip`
- `CreateTimeMaster()`
- Default shortcuts: `synth/Synth`, `piano/Piano`, `sampler`, `instrument/Instrument`

### Pattern/Notes
- `CreatePattern()` / `CreatePattern(ISynth)`
- `Note(int)` + aliases `note/NOTE`

### VST
- `CreateVst(name, alias?)` / `Vst(name, alias?)`
- `CreateVstEffect(name, alias?)`
- Default shortcuts: `vsti/Vsti`, `vstfx/VstFx`

### Audio/MIDI/Effect Racks
- `CreateEffect()` -> `EffectRack`
- `CreateMidiEffect()` -> `MidiEffectRack`
- Preset helpers: `ReverbPreset`, `DelayPreset`, `TremoloPreset`,
  `BitCrushPreset`, `NoisePreset`, `DrivePreset`, `FilterPreset`
- Default shortcuts: `effect`, `midiefx`, `deck`, `clip`, `mic`

### Routing/Helpers
- `Master` / `master` / `MASTER` (MasterBus marker)
- `Activity` / `activity` (ActivityController)
- `audio` / `Audio` / `AUDIO` (AudioControl, case-insensitive)
- `midi` / `Midi` / `MIDI` (MidiControl)
- `vst` (VstAccess)
- `Music` / `music` / `MUSIC` (CaseInsensitiveProxy)
- `Random` (RandomSource)
- `MidiMap` / `Map`
- `Use(name)` (load module)

### MIDI Bindings (Mapping from scripts)
- `Bind(target, member, min?, max?)`
- `Bind(target, member, map)`
- `BindTrigger(target, method)`
- `BindCall(target, method, min?, max?)`
- `BindCall(target, method, map)`
- `BindToggle(target, member)`
- `BindSwitch(target, member)`
- `BindToggle(getter, setter)`
- `BindSwitch(getter, setter)`

### Modulation
- `Var(target, member, initial?)`
- `Param(target, member, initial?)` (alias)

---

## 5) Script Library (`MusicEngine.Scripting.ScriptLibrary`)

- `Set(name, value)`, `Get(name)`, `Get<T>(name)`, `Remove(name)`
- `List()` -> keys
- `Scope(name)` / `NameSpace(name)`
- `File(alias, scriptName, master?)` / `File(scriptName)`
- `File()` -> scope of current script
- `Main()` / `main()` -> `ScriptFileBuilder`
- `Use(name)` -> run module

`ScriptFileBuilder`:
- `Name(scriptName)` / `name(scriptName)`
- `NameSpace(scope)`

---

## 6) VST Access (`MusicEngine.Scripting.VstAccess`)

- `KeepInstances` (bool)
- `Get(name)` / `Create(name, alias?)` (instruments)
- `GetEffect(name)` / `CreateEffect(name, alias?)` (effects)
- `TryOpenEditor(name)`
- `ApplySleepSettings()`
- `Clear()` / `Clear(keepInstances)`
- `Reattach(AudioEngine)` / `ResetState()` / `SaveAllStates()`
- `TryGetInstrument(name, out ...)`, `TryGetEffect(name, out ...)`
- `PruneUnusedStates()`

---

## 7) Fluent Audio API (`MusicEngine.Scripting.FluentApi.AudioControl`)

### Root
- `master`/`Master` -> `MasterAudioControl`
- `all`/`All` -> `AllChannelsControl`
- `channel(index)` / `createchannel(index)`
- `output`/`Output` (OutputDeviceControl)
- `input`/`Input` (InputDeviceControl)
- `cue`/`Cue` (DjCueControl)

### `MasterAudioControl`
- `gain(value)`, `effect(IAudioEffect)`, `clearEffects()`
- `record`/`Record` -> RecordingControl
- `virtualout(deviceIndex|deviceName, outputChannelOffset?)`
- `stopvirtualout()`

### `AllChannelsControl`
- `gain(value)`, `pan(value)` (float/double)

### `AudioChannelControl`
- `gain(value)`, `pan(value)`
- `route(ISynth|Pattern|int|MasterBus|AudioChannelControl)`
- `to(MasterBus|AudioChannelControl)`
- `send(targetIndex, gain?)` / `sidechain(...)`
- `effect(IAudioEffect)`, `clearEffects()`
- `record`/`Record` -> RecordingControl
- `virtualout(deviceIndex|deviceName, outputChannelOffset?)`
- `stopvirtualout()`
- `vsteffect(name)` (VST3 effect directly in channel)

### `RecordingControl` (Audio and Channel)
Functions:
- `start(path?, format?)`, `stop(session?)`
- `render(path?, seconds?, format?)`
- `delete()` / `del()`
Settings:
- `Overwrite`/`Override`
- `Loop`, `OneShot`, `DurationSeconds`
- `DefaultFormat`
- `SampleRate`, `Channels`, `WavBitDepth`, `BitRateKbps`, `ResamplerQuality`

### Device Controls
- `OutputDeviceControl.list()` / `InputDeviceControl.list()`
- `OutputDeviceControl.route(channelIndex, deviceIndex|deviceName, outputChannelOffset?)`

---

## 8) Fluent MIDI API (`MusicEngine.Scripting.FluentApi.MidiControl`)

### Root
- `device(index)` -> `DeviceControl`
- `Map` / `map` -> `MidiMap`

### `DeviceControl`
- `to(ISynth)` -> `MidiSend`
- `channel(channel)` -> `ChannelControl`
- `control(ccId)` / `cc(ccId)` -> `ControlMapping`
- `pitchbend()` -> `ControlMapping`
- `jog(ccId, mode?, scale?)` -> `JogControl`
- `jog(map, name, fallbackMode?, fallbackScale?)`

### `ChannelControl`
Same as `DeviceControl`, but channel-specific.

### `ControlMapping`
- `to(Action<float>)`

### `JogControl`
- `to(Action<int> onDelta)` (delta ticks)

### `MidiMap` + `MidiMapLibrary`
- `Set(name, ccId)`, `SetNote(name, note)`
- `SetJog(name, ccId, mode?, scale?)`
- `Get(name, fallback?)`, `GetNote(name, fallback?)`
- `TryGetJog(name, out mapping)`
- `MidiMapLibrary.Register(name, map)`, `Get(name)`, `List()`

---

## 9) Sequencer/Pattern (`MusicEngine.Core`)

### `Sequencer`
- `Start()`, `Stop()`, `Dispose()`
- `AddPattern(Pattern)`, `RemovePattern(Pattern)`, `ClearPatterns()`
- `Timing` (TimingMaster), `Bpm`
- `CurrentBeat`, `CurrentTimeSeconds`, `Patterns`, `IsRunning`

### `Pattern`
Settings/Props:
- `LoopLength`, `IsLooping`, `StartBeat`, `Enabled`
- `Timing` (TimingSettings)
- `Synth`, `SynthTargets`, `Events`, `Sequences`
Functions:
- `Note(note, beat, duration, velocity, slideTo?, slideTimeMs?)`
- `NoteMs(note, timeMs, durationMs, velocity, slideTo?, slideTimeMs?)`
- `Siquenz(steps)` / `Sequenz(steps)` -> `NoteSequence`
- `SeekBeat(beat, stopNotes?)`
- `Play()` / `Stop()`
- `GetActiveNotesSnapshot()`
Events:
- `EditorNoteEvent` (when editor mode is enabled)

### `NoteEvent`
- `Note`, `Velocity`, `Beat`, `Duration`, `BeatMs`, `DurationMs`, `SlideTo`, `SlideTimeMs`

### `NoteSequence`
- `Steps`, `Loop`, `Enabled`, `Note`

---

## 10) Audio Engine (`MusicEngine.Core.AudioEngine`)

### Lifecycle
- `Initialize()`, `SuspendOutput()`, `ResumeOutput()`, `TrySuspendOutput()`
- `Dispose()`

### Routing/Channels
- `AddSampleProvider(provider)` / `RouteToMaster(provider)`
- `RouteToChannel(provider, channelIndex)`
- `RouteChannelToMaster(index)` / `UnrouteChannelFromMaster(index)`
- `RouteChannelToChannel(source, target, gain?)`
- `SetChannelSendGain(source, target, gain)`
- `UnrouteChannelFromChannel(source, target)` / `ClearChannelSends(source)`

### Gain/Pan/Mute
- `SetAllChannelsGain(value)`, `SetAllChannelsPan(value)`
- `SetMasterGain(value)` / `MasterGain`
- `SetChannelGain(index, value)` / `SetChannelPan(index, value)`
- `SetTransportMuted(bool)`

### Effects
- `AddMasterEffect(effect)` / `ClearMasterEffects()`
- `AddChannelEffect(index, effect)` / `ClearChannelEffects(index)`

### Recording
- `StartMasterRecording(path, format?, options?)` / `StopMasterRecording(session?)`
- `StartChannelRecording(index, path, format?, options?)`
- `StopChannelRecording(index, session?)`

### Virtual Outputs
- `StartMasterVirtualOutput(deviceIndex|deviceName, outputChannelOffset?, latencyMs?)`
- `StopMasterVirtualOutputs()`
- `StartChannelVirtualOutput(channelIndex, deviceIndex|deviceName, outputChannelOffset?, latencyMs?)`
- `StopChannelVirtualOutputs(channelIndex)`

### MIDI
- `SetMidiEnabled(bool, sendAllNotesOff?)`
- `RouteMidiInput(deviceIndex, channel?, synth)`
- `MapControlAction(deviceIndex, channel?, controlId, action)`
- `ClearMappings()`
- `GetMidiActivitySnapshot()`

### Editor/Events
- `SetEditorMode(bool)`
- `RegisterPatternForEditor(pattern)`
- Events: `EditorPatternNote`, `EditorMidiNote`, `EditorMidiDeviceActive`

### Device Listing
- `ListOutputDevices()` -> `AudioOutputDeviceInfo`
- `ListInputDevices()` -> `AudioInputDeviceInfo`

---

## 11) Recording Options (`MusicEngine.Core.RecordingOptions`)

- `SampleRate`, `Channels`, `WavBitDepth`
- `BitRateKbps`, `ResamplerQuality`

---

## 12) Instruments (`MusicEngine.Instruments`)

### `SimpleSynth` (Settings)
Oscillators:
- `Waveform`, `Osc1Octave`, `Osc1Semi`, `Osc1Fine`, `Osc1Level`, `Osc1PulseWidth`
- `Osc2Waveform`, `Osc2Octave`, `Osc2Semi`, `Osc2Fine`, `Osc2Level`, `Osc2PulseWidth`, `Osc2Enabled`
Sub/Noise:
- `SubOscLevel`, `SubOscWaveform`, `NoiseLevel`
Filter:
- `Cutoff`, `Resonance`, `FilterEnvAmount`, `FilterKeyTrack`, `FilterDrive`
Amp Env:
- `Attack`, `Decay`, `Sustain`, `Release`
Filter Env:
- `FilterAttack`, `FilterDecay`, `FilterSustain`, `FilterRelease`
LFO:
- `LfoRate`, `LfoWaveform`, `LfoToPitch`, `LfoToFilter`, `LfoToAmp`, `LfoToPulseWidth`
Mod/Unison/Output:
- `PitchBend`, `PitchBendRange`, `ModWheel`, `VibratoRate`, `VibratoDepth`, `Portamento`
- `UnisonVoices`, `UnisonDetune`, `UnisonSpread`
- `Volume`, `Pan`, `Channel`, `Reverb`, `Chorus`, `MaxPolyphony`, `VelocitySensitivity`
Functions:
- `NoteOn(note, velocity)`, `NoteOff(note)`, `AllNotesOff()`
- `SetParameter(name, value)`
- `Oscillator()` / `Oscelater()`, `ClearOscillators()`

### `SamplerInstrument`
Settings:
- `Volume`, `Pan`, `ModWheel`, `Channel`, `Reverb`, `Chorus`
- `PitchSemitones`, `PlaySpeed`, `ReleaseSeconds`
- `MaxPolyphony`, `VelocityToVolume`, `UseNearestSample`
Functions:
- `LoadSample(name, path, rootNote?)`
- `LoadSamplesFromFolder(folder, pattern?, recursive?)`
- `MapSample(note, sampleName)`, `MapRange(startNote, endNote, sampleName)`
- `ClearMapping()`, `SetSampleSettings(sampleName, configure)`
- `FindSamples(query)`
- `NoteOn/NoteOff/AllNotesOff`, `SetParameter(name, value)`
- `ScratchSeconds(delta)` / `ScratchFrames(delta)`
SampleSettings:
- `RootNote`, `Gain`, `Pan`, `PitchSemitones`, `PlaySpeed`, `Loop`, `OneShot`

### `GeneralMidiInstrument`
Settings:
- `Program` (GeneralMidiProgram)
- `Channel`, `Volume`, `Pan`, `Reverb`, `Chorus`, `ModWheel`
Functions:
- `NoteOn/NoteOff/AllNotesOff`
- `PitchBend(bend)`
- `SetParameter(name, value)`
- `Param(name, min?, max?)` (mapper)

### `AudioInput`
- `Gain`, `Mute`, `Pan`, `Volume`, `Channel`, `ModWheel`, `Reverb`, `Chorus`

### `AudioDeck`
- `Name`, `IsPlaying`, `Loop`, `PlaySpeed`, `Volume`, `Pan`
- `Load(path)`, `SeekSeconds(seconds)`, `ScratchSeconds(delta)`

### `AudioClip`
- `Loop`

---

## 13) Audio Effects (`MusicEngine.Effects.Audio`)

### Effect Types + Settings
- `SimpleReverbEffect`: `Mix`, `Size`, `Damping`
- `SimpleDelayEffect`: `Mix`, `TimeMs`, `Feedback`
- `TremoloEffect`: `Depth`, `Rate`
- `BitCrusherEffect`: `BitDepth`, `Downsample`, `Mix`
- `NoiseEffect`: `Amount`, `Mix`
- `GainEffect`: `Gain`
- `DriveEffect`: `Drive`, `Mix`
- `SimpleFilterEffect`: `Type` (LowPass/HighPass), `CutoffHz`

### Preset Factory (`Effect`)
- `Reverb`, `Delay`, `Tremolo`, `BitCrush`, `Noise`, `Gain`, `Drive`, `Filter`
- `ReverbPreset(name, configure?)`, `DelayPreset(...)`, `TremoloPreset(...)`,
  `BitCrushPreset(...)`, `NoisePreset(...)`, `DrivePreset(...)`, `FilterPreset(...)`
- `Create()` -> `EffectRack`

---

## 14) MIDI Effects (`MusicEngine.Effects.Midi`)

### Effect Types + Settings
- `TransposeEffect`: `Semitones`
- `VelocityHumanizeEffect`: `Range`
- `RandomGateEffect`: `Probability`
- `MidiTriggerEffect` / `CustomMidiEffect` (callbacks)

### Rack/Presets
- `MidiEffectRack`: `Add`, `Clear`, `Transpose`, `Humanize`, `Gate`, `Trigger`, `Custom`
- `MidiEffect`: `Transpose`, `Humanize`, `Gate`, `Trigger`, `Create`

---

## 15) Modulation (`MusicEngine.Core.Modulation`)

### `Mod` (Helper)
- `Bind(get, set, initial?)` / `Var(...)` / `Param(...)`
- `Var(target, member, initial?)`
- `Group(params ModVar[])`
- Helpers: `Volume/Pan/Reverb/Chorus/ModWheel` (IInstrumentControls)
- Helpers: `Gain(AudioInput)`, `Mix/TimeMs/Feedback(SimpleDelayEffect)`,
  `Mix/Size/Damping(SimpleReverbEffect)`, `Depth/Rate(TremoloEffect)`

### `ModVar`
- `Enabled`, `Value`
- `Enable(bool)`, `Set(value)`, `Clear()`
- `Random(min, max, everyMs)`
- `Lfo(min, max, rateHz)`
- `Map(func)`, `If(predicate, whenTrue, whenFalse)`

---

## 16) MIDI Send (`MusicEngine.Effects.Midi.MidiSend`)

- `AddEffect(effect)`, `ClearEffects()`
- `NoteOn(note, velocity)`, `NoteOff(note)`, `AllNotesOff()`
- `GenerateRandomInput(...)` / `GenerateRandomInputAsync(...)`
- `Lfo(parameter, min, max, hz?, duration?)` / `LfoAsync(...)`

---

## 17) Activity + Random + Note Builder

### `ActivityController`
- `AudioEnabled`, `MidiEnabled`, `SequencerEnabled`
- `AudioEffectsEnabled`, `VstInstrumentsEnabled`, `VstEffectsEnabled`
- `VstInstrumentSleepWhenIdle`, `VstEffectSleepWhenIdle`
- `VstIdleThreshold`, `VstIdleTimeoutSeconds`
- `ApplyVstSleepSettings()`, `AggressiveVstSleep()`

### `RandomSource`
- `Range(min, max)`, `Steps(n)`, `Bool(chance)`, `Int(min, max)`, `Reset()`
- `NextFloat()`, `NextBool()`, `NextInt()`

### `NoteBuilder` / `NoteBinding`
- `Note(int).To(ISynth|params)` / `Note(int).to.<name>`
- `On/Off`, `Play(durationMs, velocity)`
- `Loop` -> `Speed(bpm)`, `Gate(ms)`, `Start()`, `Stop()`

---

## 18) VST System (`MusicEngine.Vst`)

- `VstSystem.TryScan(out registry, out message)`
- `VstSystem.SplitByKind(registry)` -> Instruments/Effects/Unknown
