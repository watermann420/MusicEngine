# FMSynth

## Overview

FMSynth is a 6-operator FM (Frequency Modulation) synthesizer based on the classic Yamaha DX7 architecture. FM synthesis creates complex timbres by modulating the frequency of one oscillator (carrier) with another oscillator (modulator). This technique is particularly effective for creating metallic, bell-like, and electric piano sounds that are difficult to achieve with traditional subtractive synthesis.

The synthesizer supports 20 different algorithms that define how the 6 operators are connected, ranging from simple serial chains to complex parallel/modulator combinations. Each operator has its own waveform, envelope, detuning, and feedback options, providing extensive sound design possibilities.

## Features

- 6 independent operators with individual envelopes and parameters
- 20 DX7-style algorithms defining operator routing
- 5 waveform types per operator (Sine, Triangle, Sawtooth, Square, Feedback)
- Per-operator self-feedback for additional harmonic content
- Polyphonic with configurable voice count (default 16 voices)
- Voice stealing for efficient polyphony management
- Velocity sensitivity per operator
- Key scaling for dynamic envelope response
- Pitch bend with configurable range
- LFO-controlled vibrato
- Soft clipping for overload protection
- Built-in factory presets (E-Piano, Brass, Bell, Bass, Organ)
- Preset save/load via IPresetProvider interface

## Architecture

### 6-Operator System

The FMSynth contains 6 operators numbered 1-6 (indices 0-5 in code). Each operator can function as either:

- **Carrier**: Outputs audio directly to the mix
- **Modulator**: Modulates the frequency of another operator

The routing between operators is determined by the selected algorithm.

### Algorithms

Algorithms define how operators are connected. In the notation below, arrows indicate modulation flow (e.g., 6->5 means operator 6 modulates operator 5), and operators in parentheses output to audio:

| Algorithm | Name | Routing | Carriers |
|-----------|------|---------|----------|
| 1 | Stack6 | 6->5->4->3->2->(1) | 1 |
| 2 | Split2_4 | (6->5) + (4->3->2->1) | 1, 5 |
| 3 | Split3_3 | (6->5->4) + (3->2->1) | 1, 4 |
| 4 | Triple | (6->5) + (4->3) + (2->1) | 1, 3, 5 |
| 5 | ModSplit | 6->5->(4->3->2->1) | 1 |
| 6 | Split4_2 | (6->5->4->3) + (2->1) | 1 |
| 7 | TripleMod | 6->(5+4+3)->2->(1) | 1 |
| 8 | DualPath | 4->3->2->(1), 6->5->(1) | 1 |
| 9 | AllParallel | (1)+(2)+(3)+(4)+(5)+(6) | 1-6 |
| 10 | DualStack | (6->5->4) + (3->2->1) | 1, 4 |
| 11 | StackWithFB | (6->5)->4->3->2->(1) | 1 |
| 12 | OneToThree | 6->5->4->(3+2+1) | 1, 2, 3 |
| 13 | TwoToThree | 6->(5+4)->(3+2+1) | 1, 2, 3 |
| 14 | TwoByTwo | (6+5)->(4+3)->(2+1) | 1, 2 |
| 15 | ThreePairs | (6->5) + (4->3) + (2->1) | 1, 3, 5 |
| 16 | EPiano | Classic electric piano | 1, 2 |
| 17 | Brass | Classic brass | 1, 2 |
| 18 | Bells | Bells/chimes | 1, 2 |
| 19 | Organ | Organ-style | 1, 2, 3 |
| 20 | Bass | FM Bass | 1, 2 |

### Feedback

Each operator can have self-feedback, where its output is fed back into its own phase input. This creates additional harmonics and can produce everything from subtle warmth to aggressive distortion depending on the amount.

## Parameters

### Global Parameters

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| Name | string | - | "FMSynth" | Synth instance name |
| Volume | float | 0-1 | 0.5 | Master output volume |
| Algorithm | FMAlgorithm | 1-20 | Stack6 (1) | Operator routing algorithm |
| FeedbackAmount | float | 0-2 | 1.0 | Global feedback multiplier |
| PitchBend | float | -1 to 1 | 0 | Current pitch bend value |
| PitchBendRange | float | semitones | 2.0 | Pitch bend range in semitones |
| VibratoDepth | float | semitones | 0 | LFO vibrato amount |
| MaxVoices | int | 1-128 | 16 | Maximum polyphony |

### Operator Parameters (per operator, 6 total)

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| Ratio | float | 0.5-32 | 1.0 | Frequency ratio relative to note |
| FixedFrequency | float | Hz | 0 | Fixed frequency (0 = use ratio) |
| Level | float | 0-1 | 1.0/0.5 | Operator output level |
| Detune | float | -100 to 100 | 0 | Fine tuning in cents |
| Feedback | float | 0-1 | 0 | Self-feedback amount |
| Waveform | FMWaveform | enum | Sine | Oscillator waveform |
| VelocitySensitivity | float | 0-1 | 0.5 | How velocity affects level |
| KeyScaling | float | 0-1 | 0 | Higher notes = faster envelopes |
| IsCarrier | bool | - | varies | Whether operator outputs audio |

### Envelope Parameters (per operator)

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| Attack | double | seconds | 0.01 | Attack time |
| Decay | double | seconds | 0.1 | Decay time |
| Sustain | double | 0-1 | 0.7 | Sustain level |
| Release | double | seconds | 0.3 | Release time |

### Waveform Types

| Waveform | Description |
|----------|-------------|
| Sine | Pure sine wave (classic FM) |
| Triangle | Triangle wave |
| Sawtooth | Sawtooth wave |
| Square | Square wave |
| Feedback | Sine with self-modulation |

## Parameter String Names

Parameters can be set via `SetParameter(name, value)` using these string names:

**Global:**
- `volume`, `algorithm`, `feedback`, `pitchbend`, `pitchbendrange`, `vibratodepth`

**Per-Operator (replace N with 1-6):**
- `opN_ratio`, `opN_level`, `opN_detune`, `opN_feedback`
- `opN_attack`, `opN_decay`, `opN_sustain`, `opN_release`
- `opN_velocity`

## Factory Presets

| Preset | Method | Description |
|--------|--------|-------------|
| E-Piano | `CreateEPianoPreset()` | Classic electric piano with bell-like attack |
| Brass | `CreateBrassPreset()` | Synthetic brass with slow attack |
| Bell | `CreateBellPreset()` | Metallic bell/chime with long decay |
| FM Bass | `CreateBassPreset()` | Punchy bass with sub oscillator |
| Organ | `CreateOrganPreset()` | Drawbar-style organ with harmonics |

## Usage Example

```csharp
using MusicEngine.Core;

// Create FM synth with default settings
var fmSynth = new FMSynth();

// Or use a factory preset
var epiano = FMSynth.CreateEPianoPreset();

// Configure algorithm
fmSynth.SetAlgorithm(FMAlgorithm.EPiano);

// Configure operators
fmSynth.SetOperator(0, ratio: 1.0f, level: 0.8f, detune: 0f, feedback: 0f);
fmSynth.SetOperator(1, ratio: 2.0f, level: 0.4f, detune: 5f, feedback: 0f);
fmSynth.SetOperator(2, ratio: 1.0f, level: 0.6f);
fmSynth.SetOperator(3, ratio: 14.0f, level: 0.3f); // Inharmonic for bell tone

// Configure envelopes
fmSynth.SetOperatorEnvelope(0, attack: 0.001, decay: 1.5, sustain: 0.0, release: 0.5);
fmSynth.SetOperatorEnvelope(1, attack: 0.001, decay: 1.0, sustain: 0.0, release: 0.3);

// Set global parameters
fmSynth.Volume = 0.6f;
fmSynth.PitchBendRange = 2f;

// Play notes
fmSynth.NoteOn(60, 100);  // Middle C, velocity 100
// ... later
fmSynth.NoteOff(60);

// Using parameter strings
fmSynth.SetParameter("op1_ratio", 2.0f);
fmSynth.SetParameter("op1_level", 0.7f);
fmSynth.SetParameter("algorithm", 16f); // EPiano algorithm

// Save/load presets
var presetData = fmSynth.GetPresetData();
fmSynth.LoadPresetData(presetData);
```

## Sound Design Tips

### Electric Piano
- Use algorithm 16 (EPiano) or similar with 2 carriers
- Carrier 1: Ratio 1.0, decaying envelope (1-2 seconds)
- Modulator: Ratio 1.0 with fast decay for attack click
- Add an inharmonic modulator (ratio 14.0) for the characteristic "tine" sound
- Short attack, long decay, zero sustain for realistic response

### Bells and Metallic Sounds
- Use algorithm 18 (Bells) or configurations with multiple modulators
- Use inharmonic ratios (1.0, 3.5, 7.0, 11.0) for metallic quality
- Long decay times (2-4 seconds) with zero sustain
- Multiple carriers for rich timbre
- Add slight detuning between operators for shimmer

### Brass and Leads
- Use algorithm 17 (Brass) with parallel carriers
- Slow attack (0.1s) for brass swell
- High sustain levels (0.7-0.9)
- Modulator with faster decay than carrier for evolving tone
- Integer ratios (1, 2, 3) for harmonic content

### Bass Sounds
- Use algorithm 20 (Bass) or simple 2-operator chains
- Add sub-octave carrier (ratio 0.5) for weight
- Fast attack and decay for punch
- Low sustain for percussive feel
- Moderate modulation depth for growl

### Organs
- Use algorithm 19 (Organ) with multiple parallel carriers
- Drawbar-like ratios: 0.5, 1.0, 1.5, 2.0, 3.0, 4.0
- Instant attack, full sustain, short release
- All operators as carriers with minimal modulation
- Cross-modulation adds subtle warmth

### General FM Tips
- Higher modulator levels = brighter, more harmonics
- Faster modulator envelopes = attack transients
- Inharmonic ratios = metallic/bell quality
- Integer ratios = musical/harmonic quality
- Feedback adds edge and complexity
- Key scaling makes high notes brighter/punchier
- Velocity sensitivity controls dynamic expression
