# SupersawSynth

## Overview

JP-8000 style supersaw synthesizer with unison detuning. Creates the classic trance/EDM lead sound by layering multiple detuned sawtooth oscillators.

## Features

- 1-16 unison oscillators
- Variable detune amount (0-50 cents spread)
- Stereo spread control
- State-variable lowpass filter with resonance
- Filter envelope with adjustable amount
- Full ADSR amplitude envelope
- Full ADSR filter envelope
- Pitch bend support
- Voice stealing (oldest voice)
- Soft clipping output for warmth

## The Supersaw Sound

The supersaw is a signature sound that originated in the Roland JP-8000 synthesizer (1996) and became the defining sound of trance and EDM music. The effect is achieved by layering multiple sawtooth oscillators that are slightly detuned from each other.

### How It Works

1. **Multiple Oscillators**: Instead of a single sawtooth wave, the supersaw uses multiple oscillators (typically 7, but this synth supports 1-16).

2. **Symmetric Detuning**: The oscillators are spread evenly around the center pitch. With 7 oscillators, one stays at the exact frequency while 3 are detuned sharp and 3 are detuned flat.

3. **Phase Randomization**: Each oscillator starts with a random phase to create a richer, more organic sound and avoid phase cancellation.

4. **Amplitude Normalization**: The output is normalized by the square root of the oscillator count to maintain consistent perceived loudness regardless of unison count.

5. **Stereo Spread**: Each oscillator is panned to a different position in the stereo field, creating width and movement.

The result is a massive, lush, animated sound that cuts through a mix while maintaining harmonic richness.

## Parameters

### Main Parameters

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| Volume | float | 0-1 | 0.5 | Master output volume |
| UnisonCount | int | 1-16 | 7 | Number of stacked oscillators |
| Detune | float | 0-1 | 0.5 | Detune spread (0=none, 1=max ~50 cents) |
| StereoSpread | float | 0-1 | 0.7 | Stereo width of oscillators |
| MaxVoices | int | 1+ | 8 | Maximum polyphony |

### Filter Parameters

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| FilterCutoff | float | 20-20000 Hz | 8000 | Lowpass filter cutoff frequency |
| FilterResonance | float | 0.5-10 | 1.0 | Filter resonance/Q factor |
| FilterEnvelopeAmount | float | Hz | 3000 | Filter envelope modulation depth |

### Amplitude Envelope (AmpEnvelope)

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| Attack | double | seconds | 0.01 | Attack time |
| Decay | double | seconds | 0.1 | Decay time |
| Sustain | double | 0-1 | 0.8 | Sustain level |
| Release | double | seconds | 0.3 | Release time |

### Filter Envelope (FilterEnvelope)

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| Attack | double | seconds | 0.01 | Filter attack time |
| Decay | double | seconds | 0.3 | Filter decay time |
| Sustain | double | 0-1 | 0.5 | Filter sustain level |
| Release | double | seconds | 0.2 | Filter release time |

### Pitch Bend

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| PitchBend | float | -1 to 1 | 0 | Current pitch bend value |
| PitchBendRange | float | 0-24 | 2 | Pitch bend range in semitones |

## Presets / Sweet Spots

The synth includes four factory presets accessible via static methods:

### Trance Lead (`CreateTranceLead()`)
Classic trance lead sound with punchy filter envelope.
- UnisonCount: 7
- Detune: 0.4
- StereoSpread: 0.8
- FilterCutoff: 4000 Hz
- FilterResonance: 1.5
- FilterEnvelopeAmount: 5000 Hz
- Short attack, moderate decay, filter sweep

### EDM Chord (`CreateEDMChord()`)
Massive chord stabs for modern EDM.
- UnisonCount: 9
- Detune: 0.6
- StereoSpread: 0.9
- FilterCutoff: 6000 Hz
- Very wide stereo field, high sustain

### Soft Pad (`CreateSoftPad()`)
Gentle, evolving pad sound.
- UnisonCount: 5
- Detune: 0.3
- StereoSpread: 0.6
- FilterCutoff: 2000 Hz
- Slow attack (0.5s), long release (1.5s)

### Aggressive Bass (`CreateAggressiveBass()`)
Punchy bass with filter bite.
- UnisonCount: 3
- Detune: 0.15
- StereoSpread: 0.3
- FilterCutoff: 1500 Hz
- FilterResonance: 2.0
- FilterEnvelopeAmount: 4000 Hz
- Very fast attack (0.001s), short release

### Custom Sweet Spots

| Style | Unison | Detune | Spread | Notes |
|-------|--------|--------|--------|-------|
| Classic Trance | 7 | 0.3-0.5 | 0.7-0.8 | The original JP-8000 sound |
| Modern EDM | 9-12 | 0.5-0.7 | 0.9 | Extra wide and thick |
| Mono Lead | 1-3 | 0.1-0.2 | 0.3 | Focused, cutting lead |
| Huge Pad | 5-7 | 0.2-0.3 | 0.6 | With slow attack/release |

## Usage Example

```csharp
// Create a basic supersaw synth
var synth = new SupersawSynth();

// Configure for trance lead
synth.UnisonCount = 7;
synth.Detune = 0.4f;
synth.StereoSpread = 0.8f;
synth.FilterCutoff = 4000f;
synth.FilterResonance = 1.5f;
synth.FilterEnvelopeAmount = 5000f;

// Set amplitude envelope
synth.AmpEnvelope.Attack = 0.01;
synth.AmpEnvelope.Decay = 0.2;
synth.AmpEnvelope.Sustain = 0.7;
synth.AmpEnvelope.Release = 0.3;

// Set filter envelope
synth.FilterEnvelope.Attack = 0.01;
synth.FilterEnvelope.Decay = 0.5;
synth.FilterEnvelope.Sustain = 0.3;
synth.FilterEnvelope.Release = 0.3;

// Play a note
synth.NoteOn(60, 100); // Middle C, velocity 100

// Or use SetParameter for runtime control
synth.SetParameter("detune", 0.6f);
synth.SetParameter("filtercutoff", 6000f);

// Use a preset
var tranceLead = SupersawSynth.CreateTranceLead();
var edmChord = SupersawSynth.CreateEDMChord();
```

### SetParameter String Names

| String Name | Maps To |
|-------------|---------|
| `volume` | Volume |
| `detune` | Detune |
| `stereospread` | StereoSpread |
| `filtercutoff` | FilterCutoff |
| `filterresonance` | FilterResonance |
| `filterenvamount` | FilterEnvelopeAmount |
| `pitchbend` | PitchBend |
| `pitchbendrange` | PitchBendRange |
| `attack` | AmpEnvelope.Attack |
| `decay` | AmpEnvelope.Decay |
| `sustain` | AmpEnvelope.Sustain |
| `release` | AmpEnvelope.Release |
| `filter_attack` | FilterEnvelope.Attack |
| `filter_decay` | FilterEnvelope.Decay |
| `filter_sustain` | FilterEnvelope.Sustain |
| `filter_release` | FilterEnvelope.Release |
