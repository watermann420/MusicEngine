# VectorSynth

## Overview

Vector synthesis with XY pad crossfading between 4 oscillators. Inspired by classic vector synthesizers like the Sequential Circuits Prophet VS and Korg Wavestation.

## Features

- 4 independent oscillators at corners of a 2D grid
- XY pad morphing between oscillators
- Real-time crossfading with smooth interpolation
- Per-oscillator waveform, detune, octave, and level control
- Vector envelope for automated XY movement over time
- Multi-point vector envelope with custom paths
- Full ADSR amplitude envelope
- State-variable filter with cutoff and resonance
- Multiple voice stealing modes
- Automation support via SetParameter

## Vector Synthesis Concept

Vector synthesis was pioneered by Sequential Circuits with the Prophet VS (1986) and later refined by Korg in the Wavestation. The concept places four sound sources at the corners of a two-dimensional grid, with a joystick or automation controlling the blend between them.

### The Grid Layout

```
        Y=0 (Top)

X=0     A -------- B     X=1
(Left)  |          |     (Right)
        |    XY    |
        |          |
        C -------- D

        Y=1 (Bottom)
```

- **Oscillator A**: Top-left corner (X=0, Y=0)
- **Oscillator B**: Top-right corner (X=1, Y=0)
- **Oscillator C**: Bottom-left corner (X=0, Y=1)
- **Oscillator D**: Bottom-right corner (X=1, Y=1)

### Crossfading Mathematics

The mix levels are calculated using bilinear interpolation:

```
Gain A = (1 - X) * (1 - Y)
Gain B = X * (1 - Y)
Gain C = (1 - X) * Y
Gain D = X * Y
```

Examples:
- Center (0.5, 0.5): Equal 25% mix of all four oscillators
- Top-left (0, 0): 100% Oscillator A
- Right edge (1, 0.5): 50% B, 50% D

### Why Vector Synthesis?

1. **Timbral Complexity**: Four distinct waveforms create rich, evolving textures
2. **Motion**: Moving through the vector space creates animated, living sounds
3. **Expressiveness**: Real-time joystick/automation control adds performance expression
4. **Efficiency**: Fewer oscillators than additive synthesis, more control than subtractive

## Parameters

### Vector Position

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| VectorX | float | 0-1 | 0.5 | X position (0=left A/C, 1=right B/D) |
| VectorY | float | 0-1 | 0.5 | Y position (0=top A/B, 1=bottom C/D) |

### Master Parameters

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| Volume | float | 0-1 | 0.5 | Master output volume |
| MaxVoices | int | 1+ | 16 | Maximum polyphony |
| StealMode | VoiceStealMode | enum | Oldest | Voice stealing strategy |

### Filter Parameters

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| FilterCutoff | float | 0-1 | 1.0 | Filter cutoff (0=20Hz, 1=bypass) |
| FilterResonance | float | 0-1 | 0 | Filter resonance |

### Amplitude Envelope

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| Attack | double | seconds | 0.01 | Attack time (min 0.001s) |
| Decay | double | seconds | 0.1 | Decay time (min 0.001s) |
| Sustain | double | 0-1 | 0.7 | Sustain level |
| Release | double | seconds | 0.3 | Release time (min 0.001s) |

### Oscillator Parameters (A, B, C, D)

Each of the four oscillators has identical parameters:

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| Waveform | VectorWaveform | enum | varies | Waveform type |
| Detune | float | -100 to +100 | 0 | Detune in cents |
| Octave | int | -2 to +2 | 0 | Octave offset |
| Level | float | 0-1 | 1.0 | Oscillator level |

Default waveforms:
- Oscillator A: Saw
- Oscillator B: Square
- Oscillator C: Triangle
- Oscillator D: Sine

### Vector Waveform Types

| Value | Name | Description |
|-------|------|-------------|
| 0 | Sine | Pure sine wave |
| 1 | Saw | Sawtooth wave |
| 2 | Square | Square wave |
| 3 | Triangle | Triangle wave |
| 4 | Noise | White noise |

### Voice Steal Modes

| Mode | Description |
|------|-------------|
| None | No voice stealing (notes are dropped) |
| Oldest | Steal the oldest playing voice |
| Quietest | Steal the voice with lowest amplitude |
| Lowest | Steal the voice playing the lowest note |
| Highest | Steal the voice playing the highest note |
| SameNote | Steal voice playing the same note, or oldest |

## The XY Pad

The XY pad is the heart of vector synthesis. It controls the blend between four oscillators in real-time.

### Manual Control

Set VectorX and VectorY directly for static positions or real-time control:

```csharp
synth.VectorX = 0.5f; // Center horizontal
synth.VectorY = 0.5f; // Center vertical
```

### Vector Envelope

For automated movement, use the VectorEnvelope:

```csharp
// Enable the vector envelope
synth.VectorEnvelope.Enabled = true;

// Set start position (when note triggers)
synth.VectorEnvelope.AttackPosition = (0.1f, 0.1f);

// Set end position (after EnvelopeTime)
synth.VectorEnvelope.SustainPosition = (0.9f, 0.9f);

// Time to travel from attack to sustain
synth.VectorEnvelope.EnvelopeTime = 3.0; // 3 seconds
```

### Multi-Point Paths

Create complex movement paths with intermediate points:

```csharp
// Clear existing points
synth.VectorEnvelope.Clear();

// Define a circular path
synth.VectorEnvelope.AttackPosition = (0f, 0.5f);    // Start: left center
synth.VectorEnvelope.AddPoint(1.0, 0.5f, 0f);        // 1s: top center
synth.VectorEnvelope.AddPoint(2.0, 1f, 0.5f);        // 2s: right center
synth.VectorEnvelope.AddPoint(3.0, 0.5f, 1f);        // 3s: bottom center
synth.VectorEnvelope.SustainPosition = (0f, 0.5f);   // End: left center
synth.VectorEnvelope.EnvelopeTime = 4.0;             // Total time
synth.VectorEnvelope.Enabled = true;
```

The envelope uses cosine interpolation for smooth transitions between points.

## Presets

### Vector Pad (`CreatePadPreset()`)

Classic vector pad with sweeping motion from corner to corner.

- Oscillators: Sine, Triangle (+5c detune), Saw (-1 oct), Square (-5c detune)
- Envelope: Slow attack (0.5s), long release (1.5s)
- Vector: Sweeps from (0.1, 0.1) to (0.9, 0.9) over 3 seconds

### Vector Lead (`CreateLeadPreset()`)

Aggressive lead with centered vector position.

- Oscillators: Saw, Saw (+7c), Square, Saw (+1 oct, -7c)
- Envelope: Fast attack (0.01s), short release (0.2s)
- Vector: Static center position, no envelope

### Vector Texture (`CreateTexturePreset()`)

Evolving texture with circular vector motion.

- Oscillators: Triangle (+3c), Noise (30% level), Sine (+1 oct), Saw (-1 oct, -3c)
- Envelope: Very slow attack (1.0s), long release (2.0s)
- Vector: Circular path over 4 seconds

### Vector Bass (`CreateBassPreset()`)

Punchy bass with harmonic movement.

- Oscillators: Sine (-1 oct), Square (-1 oct), Saw (-10c), Saw (+10c)
- Envelope: Very fast attack (0.005s), short release (0.15s)
- Vector: Quick sweep from (0.7, 0.7) to (0.2, 0.2) in 0.3s

## Usage Example

```csharp
// Create a vector synth
var synth = new VectorSynth(maxVoices: 8);

// Configure oscillators
synth.OscillatorA.Waveform = VectorWaveform.Sine;
synth.OscillatorA.Octave = 0;
synth.OscillatorA.Level = 1.0f;

synth.OscillatorB.Waveform = VectorWaveform.Saw;
synth.OscillatorB.Detune = 7f; // 7 cents sharp

synth.OscillatorC.Waveform = VectorWaveform.Triangle;
synth.OscillatorC.Octave = -1; // One octave down

synth.OscillatorD.Waveform = VectorWaveform.Square;
synth.OscillatorD.Detune = -7f; // 7 cents flat

// Set up amplitude envelope
synth.Attack = 0.1;
synth.Decay = 0.3;
synth.Sustain = 0.7;
synth.Release = 0.5;

// Configure filter
synth.FilterCutoff = 0.7f;
synth.FilterResonance = 0.2f;

// Option 1: Manual vector control
synth.VectorX = 0.3f;
synth.VectorY = 0.6f;

// Option 2: Automated vector movement
synth.VectorEnvelope.AttackPosition = (0f, 0f);
synth.VectorEnvelope.SustainPosition = (1f, 1f);
synth.VectorEnvelope.EnvelopeTime = 2.0;
synth.VectorEnvelope.Enabled = true;

// Play notes
synth.NoteOn(60, 100); // Middle C
synth.NoteOn(64, 90);  // E
synth.NoteOn(67, 80);  // G

// Release
synth.NoteOff(60);

// Or use presets
var pad = VectorSynth.CreatePadPreset();
var lead = VectorSynth.CreateLeadPreset();
```

### SetParameter String Names

| String Name | Maps To |
|-------------|---------|
| `vectorx`, `x` | VectorX |
| `vectory`, `y` | VectorY |
| `volume` | Volume |
| `cutoff`, `filtercutoff` | FilterCutoff |
| `resonance`, `filterresonance` | FilterResonance |
| `attack` | Attack |
| `decay` | Decay |
| `sustain` | Sustain |
| `release` | Release |
| `osca.waveform`, `a.waveform` | OscillatorA.Waveform |
| `osca.detune`, `a.detune` | OscillatorA.Detune |
| `osca.octave`, `a.octave` | OscillatorA.Octave |
| `osca.level`, `a.level` | OscillatorA.Level |
| `oscb.waveform`, `b.waveform` | OscillatorB.Waveform |
| `oscb.detune`, `b.detune` | OscillatorB.Detune |
| `oscb.octave`, `b.octave` | OscillatorB.Octave |
| `oscb.level`, `b.level` | OscillatorB.Level |
| `oscc.waveform`, `c.waveform` | OscillatorC.Waveform |
| `oscc.detune`, `c.detune` | OscillatorC.Detune |
| `oscc.octave`, `c.octave` | OscillatorC.Octave |
| `oscc.level`, `c.level` | OscillatorC.Level |
| `oscd.waveform`, `d.waveform` | OscillatorD.Waveform |
| `oscd.detune`, `d.detune` | OscillatorD.Detune |
| `oscd.octave`, `d.octave` | OscillatorD.Octave |
| `oscd.level`, `d.level` | OscillatorD.Level |
| `vectorenv.enabled` | VectorEnvelope.Enabled |
| `vectorenv.time` | VectorEnvelope.EnvelopeTime |
