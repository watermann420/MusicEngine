# GranularSynth

## Overview

GranularSynth is a granular synthesizer that generates sound by playing back many small fragments (grains) of audio. Each grain is a short snippet (typically 10-200ms) extracted from a source buffer, individually enveloped, and layered together. By controlling grain parameters like size, density, position, and pitch, you can create textures ranging from smooth pads to glitchy, abstract soundscapes.

### Main Use Cases
- Ambient textures and evolving soundscapes
- Time-stretching without pitch change
- Pitch-shifting without time change
- Freeze/sustain effects on samples
- Glitchy, experimental sound design
- Transforming any audio source into pad-like textures

## Features

- **Sample Loading**: Import WAV files or use raw sample data
- **Built-in Waveform Generation**: Create sources from basic waveforms
- **5 Grain Envelope Shapes**: Gaussian, Hann, Trapezoid, Triangle, Rectangle
- **4 Playback Modes**: Forward, Reverse, PingPong, Random
- **Pitch Tracking**: MIDI note-controlled pitch
- **Position Modulation**: LFO modulation of grain position
- **Stereo Spread**: Random panning per grain
- **Extensive Randomization**: Position, size, pitch, density, direction
- **Up to 64 Simultaneous Grains**: Configurable maximum
- **Automatic Normalization**: Prevents clipping with many grains

## Parameters

### Core Parameters

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| Volume | float | 0-1 | 0.5 | Master output volume |
| MaxGrains | int | 1-64+ | 64 | Maximum simultaneous grains |

### Grain Parameters

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| GrainSize | float | ms | 50 | Grain duration in milliseconds |
| GrainSizeRandom | float | 0-1 | 0.2 | Grain size randomization amount |
| Density | float | grains/sec | 30 | Grain spawn rate (grains per second) |
| DensityRandom | float | 0-1 | 0.1 | Density randomization amount |
| Envelope | GrainEnvelope | enum | Gaussian | Grain envelope/window shape |

### Position Parameters

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| Position | float | 0-1 | 0 | Playback position in source buffer |
| PositionRandom | float | 0-1 | 0.05 | Position randomization amount |
| PositionLFODepth | float | 0-1 | 0 | LFO modulation depth for position |

### Pitch Parameters

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| PitchShift | float | semitones | 0 | Global pitch shift in semitones |
| PitchRandom | float | semitones | 0 | Pitch randomization in semitones |
| PitchTracking | bool | true/false | true | Enable MIDI note pitch tracking |

### Stereo and Playback

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| PanSpread | float | 0-1 | 0.5 | Stereo spread (0=mono, 1=full) |
| PlayMode | GranularPlayMode | enum | Forward | Grain playback direction mode |
| ReverseProbability | float | 0-1 | 0.3 | Reverse chance when PlayMode=Random |

## Grain Envelopes

The grain envelope shapes how each grain fades in and out, affecting smoothness and character.

| Envelope | Description | Best For |
|----------|-------------|----------|
| Gaussian | Bell curve, very smooth | Pads, ambient textures |
| Hann | Cosine window, smooth | General purpose, clean sound |
| Trapezoid | Linear attack/release, flat sustain | Rhythmic, percussive textures |
| Triangle | Linear fade in/out | Moderate smoothness |
| Rectangle | No fade (hard edges) | Glitchy, clicky effects |

## Playback Modes

| Mode | Description |
|------|-------------|
| Forward | All grains play forward |
| Reverse | All grains play backward |
| PingPong | Alternates forward/reverse per grain |
| Random | Random direction based on ReverseProbability |

## Usage Example

```csharp
// Create a granular synth
var synth = new GranularSynth();

// Load a sample
synth.LoadSample("path/to/sample.wav");

// Or load from raw data
float[] samples = GetAudioSamples();
synth.LoadSample(samples, sampleRate: 44100);

// Or generate a waveform source
synth.GenerateSource(WaveType.Sawtooth, frequency: 220f, duration: 2f);

// Configure grain parameters
synth.Position = 0.3f;           // 30% into the sample
synth.PositionRandom = 0.1f;     // +/- 10% position variance
synth.GrainSize = 60f;           // 60ms grains
synth.GrainSizeRandom = 0.25f;   // 25% size variance
synth.Density = 40f;             // 40 grains per second
synth.PanSpread = 0.7f;          // Wide stereo

// Pitch settings
synth.PitchShift = 0f;           // No pitch shift
synth.PitchRandom = 0.2f;        // Slight pitch randomization
synth.PitchTracking = true;      // Follow MIDI notes

// Playback settings
synth.Envelope = GrainEnvelope.Gaussian;
synth.PlayMode = GranularPlayMode.Random;
synth.ReverseProbability = 0.3f;

// Add position LFO modulation
synth.PositionLFO = new LFO
{
    Frequency = 0.1,
    Waveform = LfoWaveform.Sine,
    Enabled = true
};
synth.PositionLFODepth = 0.2f;

// Play notes
synth.NoteOn(60, 100);  // C4, velocity 100
// ... later
synth.NoteOff(60);

// Use preset factory methods
var padSynth = GranularSynth.CreatePadPreset();
var textureSynth = GranularSynth.CreateTexturePreset();
var freezeSynth = GranularSynth.CreateFreezePreset();
```

## Sound Design Tips

### Smooth Pads
- **Grain Size**: 60-100ms (larger = smoother)
- **Density**: 20-30 grains/sec
- **Position Random**: 0.05-0.1 (low for cohesion)
- **Pitch Random**: 0-0.1 semitones
- **Envelope**: Gaussian or Hann
- **Pan Spread**: 0.6-0.8

### Evolving Textures
- **Grain Size**: 30-50ms with high randomization (0.4-0.5)
- **Density**: 40-60 grains/sec
- **Position Random**: 0.2-0.4 (more variety)
- **Pitch Random**: 0.3-0.8 semitones
- **PlayMode**: Random with 0.3-0.5 reverse probability
- **Position LFO**: Slow (0.05-0.2 Hz), moderate depth

### Freeze/Sustain Effect
- **Grain Size**: 80-120ms (longer for smoothness)
- **Density**: 15-25 grains/sec
- **Position Random**: 0.01-0.03 (minimal movement)
- **Pitch Random**: 0 (no variation)
- **Pitch Tracking**: false
- **Envelope**: Gaussian
- Keep Position fixed at the point you want to freeze

### Glitchy/Experimental
- **Grain Size**: 5-20ms (very short)
- **Grain Size Random**: 0.5-0.8 (high variance)
- **Density**: 60-100+ grains/sec
- **Position Random**: 0.3-0.5
- **Pitch Random**: 1-3 semitones
- **Envelope**: Rectangle or Trapezoid
- **PlayMode**: Random with 0.5 reverse probability

### Time Stretching (Without Pitch Change)
- Load your audio sample
- Set **Position** to sweep through the sample slowly (automate or use LFO)
- **Grain Size**: 50-80ms
- **Position Random**: 0.02-0.05 (low)
- **Pitch Shift**: 0
- **Pitch Tracking**: false
- Higher density for smoother result

### Pitch Shifting (Without Time Change)
- Use relatively large **Grain Size** (80-120ms)
- Keep **Position** scanning through the source
- Set **Pitch Shift** to desired semitones
- Low **Position Random** for cleaner result
- **Envelope**: Gaussian or Hann

### Tips for Source Material
- **Tonal sources** (synths, vocals): Great for pads and evolving textures
- **Percussive sources**: Can create rhythmic textures at high density
- **Noise/texture sources**: Good for ambient backgrounds
- **Short loops**: Work well as the position cycles naturally
- Longer samples give more variety when using position randomization
- Normalize your source audio for consistent grain amplitudes

### Performance Considerations
- Higher grain counts (MaxGrains) increase CPU usage
- Very short grains (<10ms) at high density are CPU-intensive
- Balance density and grain count based on your target system
