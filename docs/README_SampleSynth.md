# SampleSynth

## Overview

Multi-sample playback synthesizer with velocity layers, round-robin sample selection, multiple loop modes, filter with envelope, and LFO modulation. SampleSynth is a professional-grade sampler that supports complex sample mapping across the keyboard with automatic pitch shifting based on root note.

## Features

- **Multi-sample mapping** - Map different samples to different key ranges
- **Velocity layers** - Trigger different samples based on note velocity
- **Round-robin** - Cycle through multiple samples for the same note to add variation
- **Loop modes** - One-shot, Forward loop, Ping-Pong loop, Reverse playback
- **Loop crossfade** - Smooth crossfade at loop points for seamless looping
- **Filter with envelope** - State-variable filter (LP/HP/BP) with dedicated ADSR envelope
- **ADSR envelopes** - Separate envelopes for amplitude, filter cutoff, and pitch
- **LFO modulation** - Pitch LFO (vibrato), Filter LFO, and Amplitude LFO (tremolo)
- **Per-zone settings** - Volume, pan, tuning, and fine-tuning per sample zone
- **Voice stealing** - Intelligent oldest-voice stealing when polyphony limit is reached
- **Pitch bend** - MIDI pitch bend support with configurable range

## Sample Mapping

Samples are organized into **zones**. Each zone defines:

- **Key range** (`LowNote` to `HighNote`) - Which MIDI notes trigger this sample
- **Velocity range** (`LowVelocity` to `HighVelocity`) - Which velocities trigger this sample
- **Root note** - The MIDI note at which the sample plays at original pitch

When a note is played, SampleSynth:
1. Finds all zones matching the note and velocity
2. If round-robin is configured, cycles through matching samples
3. Calculates playback rate to pitch-shift from root note to played note
4. Applies sample rate conversion if sample rate differs from output

### SampleZone Properties

| Property | Type | Range | Default | Description |
|----------|------|-------|---------|-------------|
| `AudioData` | `float[]` | - | `[]` | Stereo interleaved sample data |
| `Name` | `string` | - | `""` | Sample name |
| `FilePath` | `string` | - | `""` | Original file path |
| `SampleRate` | `int` | - | `44100` | Sample rate of loaded audio |
| `Channels` | `int` | - | `2` | Number of channels (1 or 2) |
| `LowNote` | `int` | 0-127 | `0` | Lowest MIDI note this zone responds to |
| `HighNote` | `int` | 0-127 | `127` | Highest MIDI note this zone responds to |
| `RootNote` | `int` | 0-127 | `60` | Note where sample plays at original pitch |
| `LowVelocity` | `int` | 0-127 | `0` | Minimum velocity this zone responds to |
| `HighVelocity` | `int` | 0-127 | `127` | Maximum velocity this zone responds to |
| `RoundRobinGroup` | `int` | 0+ | `0` | Round-robin group (0 = disabled) |
| `RoundRobinIndex` | `int` | 0+ | `0` | Sequence index within round-robin group |
| `LoopStart` | `int` | samples | `0` | Loop start position in samples |
| `LoopEnd` | `int` | samples | `0` | Loop end position in samples |
| `LoopCrossfade` | `int` | samples | `0` | Crossfade length for smooth looping |
| `LoopMode` | `SampleLoopMode` | enum | `None` | Loop mode (see below) |
| `Reverse` | `bool` | - | `false` | Play sample in reverse |
| `Volume` | `float` | 0-2 | `1.0` | Volume adjustment |
| `Pan` | `float` | -1 to 1 | `0` | Pan position (-1=left, 0=center, 1=right) |
| `Tune` | `float` | semitones | `0` | Tuning adjustment in semitones |
| `FineTune` | `float` | -100 to 100 | `0` | Fine tuning in cents |

## Parameters

### Main Parameters

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| `Name` | `string` | - | `"SampleSynth"` | Synth name |
| `MaxVoices` | `int` | 1-128 | `32` | Maximum polyphony |
| `Volume` | `float` | 0-2 | `1.0` | Master volume |
| `VelocitySensitivity` | `float` | 0-1 | `1.0` | How much velocity affects volume |

### Amplitude Envelope (AmpEnvelope)

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| `Attack` | `double` | seconds | `0.001` | Attack time |
| `Decay` | `double` | seconds | `0.0` | Decay time |
| `Sustain` | `double` | 0-1 | `1.0` | Sustain level |
| `Release` | `double` | seconds | `0.1` | Release time |

### Filter Parameters

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| `FilterEnabled` | `bool` | - | `false` | Enable/disable filter |
| `FilterType` | `SamplerFilterType` | enum | `Lowpass` | Filter type |
| `FilterCutoff` | `float` | 20-20000 Hz | `10000` | Filter cutoff frequency |
| `FilterResonance` | `float` | 0.1-10 | `0.707` | Filter resonance (Q) |
| `FilterEnvelopeAmount` | `float` | 0-10000 | `0` | Filter envelope modulation depth |
| `FilterMix` | `float` | 0-1 | `1.0` | Filter wet/dry mix |

### Filter Envelope (FilterEnvelope)

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| `Attack` | `double` | seconds | `0.01` | Attack time |
| `Decay` | `double` | seconds | `0.3` | Decay time |
| `Sustain` | `double` | 0-1 | `0.5` | Sustain level |
| `Release` | `double` | seconds | `0.2` | Release time |

### Pitch Envelope (PitchEnvelope)

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| `PitchEnvelopeAmount` | `float` | semitones | `0` | Pitch envelope modulation depth |
| `Attack` | `double` | seconds | `0.001` | Attack time |
| `Decay` | `double` | seconds | `0.1` | Decay time |
| `Sustain` | `double` | 0-1 | `0.0` | Sustain level |
| `Release` | `double` | seconds | `0.1` | Release time |

### LFO Parameters

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| `PitchLFO` | `LFO` | - | `null` | LFO for pitch modulation (vibrato) |
| `PitchLFOAmount` | `float` | semitones | `0` | Pitch LFO depth |
| `FilterLFO` | `LFO` | - | `null` | LFO for filter modulation |
| `FilterLFOAmount` | `float` | Hz | `0` | Filter LFO depth |
| `AmpLFO` | `LFO` | - | `null` | LFO for amplitude modulation (tremolo) |
| `AmpLFOAmount` | `float` | 0-1 | `0` | Amplitude LFO depth |

### MIDI Parameters

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| `PitchBend` | `float` | -1 to 1 | `0` | Current pitch bend value |
| `PitchBendRange` | `float` | 0-24 | `2` | Pitch bend range in semitones |
| `ModWheel` | `float` | 0-1 | `0` | Mod wheel value |

## Loop Modes

| Mode | Value | Description |
|------|-------|-------------|
| `None` | `SampleLoopMode.None` | One-shot playback, sample stops at end |
| `Forward` | `SampleLoopMode.Forward` | Loop continuously from LoopStart to LoopEnd |
| `PingPong` | `SampleLoopMode.PingPong` | Loop forward then backward between loop points |
| `Reverse` | `SampleLoopMode.Reverse` | Play sample in reverse, optionally loop |

## Filter Types

| Type | Value | Description |
|------|-------|-------------|
| `Lowpass` | `SamplerFilterType.Lowpass` | Low-pass filter (removes high frequencies) |
| `Highpass` | `SamplerFilterType.Highpass` | High-pass filter (removes low frequencies) |
| `Bandpass` | `SamplerFilterType.Bandpass` | Band-pass filter (isolates frequency band) |

## Usage Example

```csharp
// Create a sample synth
var sampler = new SampleSynth();

// Load a sample
var zone = sampler.LoadSample("C:/Samples/piano_C4.wav", rootNote: 60);

// Configure the zone for a specific key range
zone.LowNote = 48;   // C3
zone.HighNote = 72;  // C5
zone.RootNote = 60;  // C4

// Set up velocity layers by loading multiple samples
var softZone = sampler.LoadSample("C:/Samples/piano_C4_soft.wav", rootNote: 60);
softZone.LowNote = 48;
softZone.HighNote = 72;
softZone.LowVelocity = 0;
softZone.HighVelocity = 64;

var loudZone = sampler.LoadSample("C:/Samples/piano_C4_loud.wav", rootNote: 60);
loudZone.LowNote = 48;
loudZone.HighNote = 72;
loudZone.LowVelocity = 65;
loudZone.HighVelocity = 127;

// Enable filter with envelope
sampler.FilterEnabled = true;
sampler.FilterType = SamplerFilterType.Lowpass;
sampler.FilterCutoff = 2000f;
sampler.FilterEnvelopeAmount = 5000f;

// Add vibrato
sampler.PitchLFO = new LFO(LfoWaveform.Sine, 5.0) { Depth = 1.0f };
sampler.PitchLFOAmount = 0.15f;

// Configure amp envelope for pad-like sound
sampler.AmpEnvelope.Attack = 0.3;
sampler.AmpEnvelope.Release = 1.5;

// Play a note
sampler.NoteOn(60, 100);  // Middle C, velocity 100
```

### Round-Robin Example

```csharp
// Load multiple variations of the same note for round-robin
var snare1 = sampler.LoadSample("C:/Samples/snare_1.wav", rootNote: 38);
snare1.LowNote = 38;
snare1.HighNote = 38;
snare1.RoundRobinGroup = 1;
snare1.RoundRobinIndex = 0;

var snare2 = sampler.LoadSample("C:/Samples/snare_2.wav", rootNote: 38);
snare2.LowNote = 38;
snare2.HighNote = 38;
snare2.RoundRobinGroup = 1;
snare2.RoundRobinIndex = 1;

var snare3 = sampler.LoadSample("C:/Samples/snare_3.wav", rootNote: 38);
snare3.LowNote = 38;
snare3.HighNote = 38;
snare3.RoundRobinGroup = 1;
snare3.RoundRobinIndex = 2;

// Each hit cycles through the three samples
sampler.NoteOn(38, 100);  // Plays snare_1
sampler.NoteOn(38, 100);  // Plays snare_2
sampler.NoteOn(38, 100);  // Plays snare_3
sampler.NoteOn(38, 100);  // Plays snare_1 again
```

### Looping Sample Example

```csharp
// Load a pad sample with loop
var padZone = sampler.LoadSample("C:/Samples/pad_loop.wav", rootNote: 60);
padZone.LoopMode = SampleLoopMode.Forward;
padZone.LoopStart = 44100;       // Start loop at 1 second
padZone.LoopEnd = 176400;        // End loop at 4 seconds
padZone.LoopCrossfade = 4410;    // 100ms crossfade for smooth loop
```

## Presets

SampleSynth includes factory presets for common use cases:

| Preset | Description |
|--------|-------------|
| `CreatePianoPreset()` | Configured for piano samples with natural decay |
| `CreatePadPreset()` | Long attack/release, filter with envelope, vibrato |
| `CreateDrumKitPreset()` | One-shot playback, instant attack, high velocity sensitivity |
| `CreateAmbientPreset()` | Very long attack/release, slow filter LFO |

```csharp
// Use a preset
var piano = SampleSynth.CreatePianoPreset();
piano.LoadSample("C:/Samples/piano_C4.wav", 60);

var drums = SampleSynth.CreateDrumKitPreset();
drums.LoadSample("C:/Samples/kick.wav", 36);
drums.LoadSample("C:/Samples/snare.wav", 38);
drums.LoadSample("C:/Samples/hihat.wav", 42);
```

## SetParameter Reference

Parameters can be set via `SetParameter(name, value)`:

| Name | Range | Description |
|------|-------|-------------|
| `volume` | 0-2 | Master volume |
| `pitchbend` | -1 to 1 | Pitch bend |
| `pitchbendrange` | 0-24 | Pitch bend range (semitones) |
| `modwheel` | 0-1 | Mod wheel |
| `amp_attack` | seconds | Amp envelope attack |
| `amp_decay` | seconds | Amp envelope decay |
| `amp_sustain` | 0-1 | Amp envelope sustain |
| `amp_release` | seconds | Amp envelope release |
| `filter_enabled` | 0/1 | Filter enable |
| `filter_cutoff` | 20-20000 | Filter cutoff (Hz) |
| `filter_resonance` | 0.1-10 | Filter resonance |
| `filter_env_amount` | Hz | Filter envelope amount |
| `filter_attack` | seconds | Filter envelope attack |
| `filter_decay` | seconds | Filter envelope decay |
| `filter_sustain` | 0-1 | Filter envelope sustain |
| `filter_release` | seconds | Filter envelope release |
| `pitch_env_amount` | semitones | Pitch envelope amount |
| `pitch_attack` | seconds | Pitch envelope attack |
| `pitch_decay` | seconds | Pitch envelope decay |
| `pitch_lfo_amount` | semitones | Pitch LFO depth |
| `filter_lfo_amount` | Hz | Filter LFO depth |
| `amp_lfo_amount` | 0-1 | Amp LFO depth |
