# PhysicalModeling Synth

## Overview

Physical modeling synthesis using Karplus-Strong and waveguide algorithms. This synthesizer simulates the behavior of real acoustic instruments by modeling the physics of vibrating strings, membranes, and air columns rather than using samples or traditional oscillators.

## Features

- **Karplus-Strong string synthesis** - Digital waveguide algorithm for plucked strings
- **Multiple physical models** - Plucked strings, bowed strings, drums, bells, and wind instruments
- **Waveguide modeling** - Delay line based simulation with fractional delay interpolation
- **Excitation control** - Adjustable exciter type (noise burst vs impulse) and position
- **Damping and decay control** - Realistic string/membrane damping behavior
- **Body resonance** - Simulated resonant body for acoustic instruments
- **Inharmonicity** - String stiffness modeling for piano-like sounds
- **Nonlinearity** - Metallic sound generation through waveshaping
- **Polyphonic** - Up to 16 voices with voice stealing
- **Factory presets** - Guitar, Piano, Bell, and Drum presets included

## Algorithms

### Karplus-Strong

The Karplus-Strong algorithm is a simple yet effective method for synthesizing plucked string sounds. It works by:

1. **Excitation**: Fill a delay line buffer with an initial excitation signal (noise burst or impulse)
2. **Feedback loop**: Read samples from the delay line, apply a lowpass filter, and write back
3. **Natural decay**: The filtering in the feedback loop causes high frequencies to decay faster than low frequencies, mimicking real string behavior

The delay line length determines the fundamental frequency: `frequency = sampleRate / delayLength`

### Waveguide

Digital waveguides model the traveling waves in physical systems:

- **Delay lines** represent the time it takes for waves to travel along the string
- **Filters** at reflection points model energy loss and frequency-dependent damping
- **Fractional delay** interpolation provides accurate tuning across all frequencies
- **Pickup position** simulation creates comb filtering effects like real instrument pickups

### Bowed String Model

The bowed string model adds continuous excitation through a friction-based bow simulation:

- **Stick-slip friction**: Models the alternating sticking and slipping of bow hair on string
- **Bow pressure**: Controls the force applied to the string
- **Bow velocity**: Controls the speed of the bow across the string

## Model Types

| Model Type | Description | Characteristics |
|------------|-------------|-----------------|
| `PluckedString` | Guitar, harp, harpsichord | Triangle-shaped excitation, variable pluck position |
| `BowedString` | Violin, cello, viola | Continuous friction excitation, stick-slip dynamics |
| `DrumMembrane` | Drums, toms, tympani | Noise burst excitation, circular membrane shape |
| `WindTube` | Flute, clarinet, brass | Air burst excitation, tube resonance |
| `Bell` | Bells, chimes, bars | Sharp impulse excitation, inharmonic partials |

## Parameters

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| `Volume` | float | 0.0 - 1.0 | 0.7 | Master output volume |
| `ModelType` | enum | 0-4 | PluckedString | Physical model type (see Model Types) |
| `Damping` | float | 0.0 - 1.0 | 0.5 | Damping factor - higher values cause faster decay |
| `Brightness` | float | 0.0 - 1.0 | 0.8 | Filter cutoff controlling harmonic content |
| `ExciterType` | float | 0.0 - 1.0 | 0.5 | Blend between noise burst (0) and impulse (1) |
| `ExciterPosition` | float | 0.01 - 0.99 | 0.5 | Position of excitation on the string |
| `PickupPosition` | float | 0.01 - 0.99 | 0.25 | Simulated pickup position (affects timbre) |
| `Nonlinearity` | float | 0.0 - 1.0 | 0.0 | Adds metallic character through waveshaping |
| `Inharmonicity` | float | 0.0 - 1.0 | 0.0 | String stiffness (creates piano-like dispersion) |
| `BodyResonance` | float | 0.0 - 1.0 | 0.3 | Amount of body resonance at ~150Hz |
| `BowPressure` | float | 0.0 - 1.0 | 0.5 | Bow pressure for bowed string model |
| `BowVelocity` | float | 0.0 - 1.0 | 0.5 | Bow speed for bowed string model |
| `MaxVoices` | int | 1-16+ | 16 | Maximum polyphony |

## Instrument Types

### Plucked Strings (Guitar, Harp, Harpsichord)

Best settings for guitar-like sounds:
- `ModelType`: PluckedString
- `ExciterType`: 0.2-0.4 (more noise = softer attack)
- `ExciterPosition`: 0.1-0.2 (near bridge = brighter)
- `PickupPosition`: 0.2-0.3
- `BodyResonance`: 0.4-0.6

### Struck Strings (Piano, Hammered Dulcimer)

Best settings for piano-like sounds:
- `ModelType`: PluckedString
- `ExciterType`: 0.8-1.0 (impulse = hammer strike)
- `ExciterPosition`: 0.1-0.15
- `Inharmonicity`: 0.03-0.08 (higher for bass strings)
- `BodyResonance`: 0.5-0.7

### Bowed Strings (Violin, Cello, Viola)

Best settings for bowed instrument sounds:
- `ModelType`: BowedString
- `BowPressure`: 0.4-0.7
- `BowVelocity`: 0.3-0.7
- `Damping`: 0.3-0.5
- `BodyResonance`: 0.4-0.6

### Bells and Metallic Percussion

Best settings for bell-like sounds:
- `ModelType`: Bell
- `ExciterType`: 1.0 (sharp impulse)
- `Damping`: 0.1-0.3 (long sustain)
- `Nonlinearity`: 0.2-0.4
- `Inharmonicity`: 0.2-0.4 (creates bell-like partials)

## Factory Presets

### CreateGuitarPreset()
Acoustic guitar with warm, natural tone.

### CreatePianoPreset()
Piano string with hammer-like attack and body resonance.

### CreateBellPreset()
Metallic bell with long sustain and inharmonic partials.

### CreateDrumPreset()
Drum membrane with short decay and body resonance.

## Usage Example

```csharp
using MusicEngine.Core;

// Create a physical modeling synth
var synth = new PhysicalModelingSynth();

// Configure for acoustic guitar
synth.ModelType = PhysicalModelType.PluckedString;
synth.Damping = 0.5f;
synth.Brightness = 0.7f;
synth.ExciterType = 0.3f;
synth.ExciterPosition = 0.15f;
synth.BodyResonance = 0.5f;

// Or use a factory preset
var guitar = PhysicalModelingSynth.CreateGuitarPreset();
var piano = PhysicalModelingSynth.CreatePianoPreset();
var bell = PhysicalModelingSynth.CreateBellPreset();

// Play notes
synth.NoteOn(60, 100);  // Middle C, velocity 100
// ... later
synth.NoteOff(60);

// Set parameters by name
synth.SetParameter("damping", 0.6f);
synth.SetParameter("brightness", 0.8f);
synth.SetParameter("bodyresonance", 0.4f);

// Read audio samples
float[] buffer = new float[1024];
synth.Read(buffer, 0, buffer.Length);
```

## Technical Details

### Voice Architecture

Each voice contains:
- Primary delay line for waveguide simulation
- Secondary delay line for pickup position simulation
- Two-stage lowpass filter for damping
- Allpass filter for inharmonicity/dispersion
- Bandpass filter for body resonance (~150Hz)
- Release envelope with exponential decay

### Signal Flow

```
Excitation --> [Delay Line] --> [Damping Filter] --> [Inharmonicity] --> [Nonlinearity]
                    ^                                                          |
                    |                                                          v
                    +<---------------- [Feedback] <----------------------------+
                                                                               |
                                                                               v
                                           [Pickup Position] --> [Body Resonance] --> Output
```

### Performance Considerations

- Each active voice uses approximately 2KB of memory for delay lines
- CPU usage scales linearly with voice count
- Fractional delay interpolation adds minimal overhead
- Body resonance filter adds one biquad calculation per sample
