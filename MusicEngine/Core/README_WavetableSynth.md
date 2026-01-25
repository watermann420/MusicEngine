# WavetableSynth

## Overview

WavetableSynth is a polyphonic wavetable synthesizer with morphing capabilities. It generates sound by reading through pre-computed waveform tables (wavetables) and smoothly interpolating between different frames (morph positions) within the table. This technique allows for evolving, complex timbres that would be difficult to achieve with traditional oscillators.

### Main Use Cases
- Evolving pads with movement and texture
- Complex leads with timbral variation
- Sound design with dynamic waveform morphing
- Recreating classic wavetable synthesizers (PPG, Waldorf, Serum)

## Features

- **6 Built-in Wavetable Types**: Basic, PWM, Vocal, Digital, Analog, Harmonic
- **Custom Wavetable Loading**: Import from WAV files or raw sample data
- **Smooth Morphing**: Bilinear interpolation between frames and samples
- **Polyphonic**: Up to 16 voices with voice stealing
- **Unison Mode**: Up to 8 unison oscillators with detune and stereo spread
- **Position Modulation**: Envelope and LFO modulation of wavetable position
- **Low-pass Filter**: Simple filter with cutoff and resonance
- **ADSR Envelopes**: Amplitude and position modulation envelopes
- **Soft Clipping**: Built-in tanh saturation for warm overdrive

## Parameters

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| Position | float | 0-1 | 0 | Wavetable position (morphs between frames) |
| Volume | float | 0-1 | 0.5 | Master output volume |
| FilterCutoff | float | 0-1 | 1.0 | Low-pass filter cutoff frequency |
| FilterResonance | float | 0-1 | 0 | Filter resonance amount |
| Detune | float | cents | 0 | Global detune in cents |
| UnisonVoices | int | 1-8 | 1 | Number of unison oscillators per voice |
| UnisonDetune | float | cents | 10 | Detune spread between unison voices |
| UnisonSpread | float | 0-1 | 0.5 | Stereo spread of unison voices |
| PositionEnvAmount | float | -1 to 1 | 0 | Position envelope modulation depth |
| PositionLFODepth | float | 0-1 | 0 | Position LFO modulation depth |
| MaxVoices | int | 1-16 | 16 | Maximum polyphony |

### Envelope Parameters (AmpEnvelope)

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| Attack | double | seconds | 0.01 | Attack time |
| Decay | double | seconds | 0.1 | Decay time |
| Sustain | double | 0-1 | 0.7 | Sustain level |
| Release | double | seconds | 0.3 | Release time |

### Position Envelope (PositionEnvelope)

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| Attack | double | seconds | 0.01 | Attack time |
| Decay | double | seconds | 0.5 | Decay time |
| Sustain | double | 0-1 | 0.0 | Sustain level |
| Release | double | seconds | 0.3 | Release time |

## Wavetables

### Built-in Wavetable Types (WavetableType enum)

| Type | Description |
|------|-------------|
| Basic | Morphs through Sine -> Triangle -> Saw -> Square |
| PWM | Pulse Width Modulation sweep from 5% to 95% duty cycle |
| Vocal | Vocal formant simulation (A -> E -> I -> O -> U vowels) |
| Digital | Harsh digital waveforms: quantized sine and ring modulation |
| Analog | Soft analog-style supersaw with detuning and saturation |
| Harmonic | Additive synthesis with increasing harmonic content |

### Wavetable Structure
- **Frame Size**: 2048 samples per frame
- **Frame Count**: 256 frames per wavetable
- **Interpolation**: Bilinear (both between frames and samples)

### Loading Custom Wavetables

The synth supports loading custom wavetables from:
1. **WAV files**: Automatically converts stereo to mono, splits by frame size
2. **Raw float arrays**: Direct sample data loading

## Usage Example

```csharp
// Create a wavetable synth with the Basic wavetable
var synth = new WavetableSynth(WavetableType.Basic);

// Configure the synth
synth.Position = 0.5f;        // Middle of wavetable (Saw)
synth.Volume = 0.6f;
synth.UnisonVoices = 4;       // 4 unison oscillators
synth.UnisonDetune = 12f;     // 12 cents spread
synth.UnisonSpread = 0.8f;    // Wide stereo

// Configure envelope
synth.AmpEnvelope.Attack = 0.05;
synth.AmpEnvelope.Decay = 0.2;
synth.AmpEnvelope.Sustain = 0.6;
synth.AmpEnvelope.Release = 0.5;

// Add position modulation
synth.PositionLFO = new LFO
{
    Frequency = 0.3,
    Waveform = LfoWaveform.Triangle,
    Enabled = true
};
synth.PositionLFODepth = 0.2f;

// Play notes
synth.NoteOn(60, 100);  // C4, velocity 100
// ... later
synth.NoteOff(60);

// Load custom wavetable from file
synth.LoadWavetable("path/to/wavetable.wav", frameSize: 2048);

// Or load from raw data
float[] myWavetable = GenerateCustomWavetable();
synth.LoadWavetable(myWavetable, frameSize: 2048);

// Use preset factory methods
var padSynth = WavetableSynth.CreatePadPreset();
var leadSynth = WavetableSynth.CreateLeadPreset();
var vocalSynth = WavetableSynth.CreateVocalPreset();
```

## Sound Design Tips

### Evolving Pads
- Use the **Analog** or **Vocal** wavetable type
- Set Position to 0.3-0.5 for starting point
- Add slow LFO modulation to Position (0.1-0.5 Hz, depth 0.2-0.4)
- Use 4+ unison voices with 15-25 cents detune
- Long attack (0.3-1.0s) and release (0.5-2.0s)
- Lower FilterCutoff to 0.5-0.7 for warmth

### Digital Leads
- Use the **Digital** wavetable type
- Higher Position values (0.5-1.0) for aggressive tones
- Minimal unison (1-2 voices) for cleaner sound
- Fast attack, moderate decay, lower sustain
- Add resonance (0.2-0.4) for presence

### PWM Strings
- Use the **PWM** wavetable type
- Set Position around 0.5 (50% pulse width)
- Add slow Position LFO (0.3-0.7 Hz) for classic PWM movement
- Multiple unison voices (3-5) with moderate spread
- Moderate filter cutoff with slight resonance

### Vocal/Choir Sounds
- Use the **Vocal** wavetable type
- Automate Position to sweep through vowels
- Use Position envelope (Attack: 0.01, Decay: 1.0, Sustain: 0.3)
- Moderate unison for thickness without losing clarity

### Bass Sounds
- Use **Basic** wavetable (Saw/Square region: Position 0.66-1.0)
- Single voice (no unison) for tight low end
- Fast envelope (Attack: 0.001, Decay: 0.1)
- Low FilterCutoff with moderate resonance for sub-bass emphasis

### Tips for Custom Wavetables
- Wavetable WAV files should contain multiple single-cycle waveforms concatenated
- Frame size of 2048 samples works well for most applications
- Normalize your wavetables to prevent clipping or low output
- Smooth transitions between frames reduce clicking artifacts
