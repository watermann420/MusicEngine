# NoiseGenerator

## Overview

Multi-color noise generator for synthesis and sound design. This synthesizer produces various types of noise with different spectral characteristics, optional filtering, and stereo width control. It can be triggered via MIDI or run continuously for ambient textures.

## Noise Types

### White Noise
- **Spectrum**: Flat (equal energy at all frequencies)
- **Sound**: Bright, hissy, like TV static or rushing air
- **Technical**: Random values with uniform distribution
- **Uses**: Hi-hats, snares, wind effects, synthesizer modulation

### Pink Noise
- **Spectrum**: 1/f (energy decreases 3dB per octave)
- **Sound**: Natural, balanced, less harsh than white noise
- **Technical**: Voss-McCartney algorithm with 16 octave bands
- **Uses**: Test signals, ambient textures, natural sounds (waterfalls, rain)

### Brown Noise (Brownian/Red)
- **Spectrum**: 1/f^2 (energy decreases 6dB per octave)
- **Sound**: Deep, rumbling, thunder-like
- **Technical**: Integrated white noise with DC leak prevention
- **Uses**: Thunder, ocean waves, bass textures, relaxation audio

### Blue Noise
- **Spectrum**: f (energy increases 3dB per octave)
- **Sound**: Bright, treble-heavy
- **Technical**: Differentiated white noise (first-order difference)
- **Uses**: High-frequency textures, dithering, masking high-pitched sounds

### Violet Noise (Purple)
- **Spectrum**: f^2 (energy increases 6dB per octave)
- **Sound**: Very bright, almost piercing
- **Technical**: Double-differentiated white noise
- **Uses**: Extreme high-frequency effects, specialized audio testing

## Parameters

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| `NoiseType` | enum | 0-4 | White | Type of noise (White, Pink, Brown, Blue, Violet) |
| `Volume` | float | 0.0 - 1.0 | 0.5 | Master output volume |
| `FilterType` | enum | 0-3 | None | Filter type (None, LowPass, HighPass, BandPass) |
| `FilterFrequency` | float | 20 - 20000 | 1000 | Filter cutoff frequency in Hz |
| `FilterResonance` | float | 0.0 - 1.0 | 0.0 | Filter resonance/Q (0=no resonance, 1=max) |
| `StereoWidth` | float | 0.0 - 1.0 | 1.0 | Stereo spread (0=mono, 1=full stereo) |
| `Continuous` | bool | true/false | false | Run continuously without MIDI triggers |

### Parameter Aliases

The `SetParameter()` method accepts multiple names for convenience:

| Parameter | Aliases |
|-----------|---------|
| NoiseType | `noisetype`, `type` |
| FilterType | `filtertype`, `filter` |
| FilterFrequency | `filterfrequency`, `cutoff`, `frequency` |
| FilterResonance | `filterresonance`, `resonance`, `q` |
| StereoWidth | `stereowidth`, `width`, `stereo` |

## Filter Types

| Filter | Description | Use Case |
|--------|-------------|----------|
| `None` | No filtering applied | Raw noise output |
| `LowPass` | Allows frequencies below cutoff | Soften harsh noise, create muffled textures |
| `HighPass` | Allows frequencies above cutoff | Remove rumble, create airy textures |
| `BandPass` | Allows frequencies around cutoff | Isolate specific frequency bands |

The filter is implemented as a biquad filter using Direct Form II Transposed structure for numerical stability.

## Applications

### Percussion Synthesis
- **Hi-hats**: White noise with high-pass filter and short envelope
- **Snare drums**: Mix with tonal elements, bandpass filtered
- **Cymbals**: White/blue noise with complex filtering

### Wind and Ocean Sounds
- **Wind**: Pink/brown noise with slow volume modulation
- **Ocean waves**: Brown noise with rhythmic volume envelope
- **Rain**: Pink noise with subtle filtering

### Adding Texture to Pads
- **Analog warmth**: Low-level pink noise blended with synth pads
- **Breath**: Filtered white noise added to wind instruments
- **Vinyl crackle**: Low-volume noise with specific filtering

### Test and Calibration
- **Speaker testing**: Pink noise (equal energy per octave)
- **Room acoustics**: Pink/white noise for frequency response
- **Headroom testing**: White noise at various levels

## Usage Example

```csharp
using MusicEngine.Core;

// Create a noise generator
var noise = new NoiseGenerator();

// Configure for hi-hat sound
noise.NoiseType = NoiseType.White;
noise.FilterType = NoiseFilterType.HighPass;
noise.FilterFrequency = 8000f;
noise.FilterResonance = 0.2f;
noise.Volume = 0.7f;
noise.StereoWidth = 0.8f;

// Trigger with MIDI (note is ignored, only velocity matters)
noise.NoteOn(60, 100);  // Velocity controls loudness
// ... later
noise.NoteOff(60);

// Or run continuously for ambient textures
noise.Continuous = true;

// Set parameters by name
noise.SetParameter("type", (float)NoiseType.Pink);
noise.SetParameter("cutoff", 2000f);
noise.SetParameter("resonance", 0.5f);
noise.SetParameter("stereo", 0.5f);

// Read audio samples
float[] buffer = new float[1024];
noise.Read(buffer, 0, buffer.Length);

// Reset state (clears filter history, reinitializes noise generators)
noise.Reset();
```

### Continuous Mode Example

```csharp
// Create ambient wind texture
var wind = new NoiseGenerator
{
    NoiseType = NoiseType.Pink,
    FilterType = NoiseFilterType.LowPass,
    FilterFrequency = 800f,
    FilterResonance = 0.1f,
    Volume = 0.3f,
    StereoWidth = 1.0f,
    Continuous = true  // Plays automatically without NoteOn
};
```

### Layered Noise Example

```csharp
// Create ocean waves by layering noise types
var deepWaves = new NoiseGenerator
{
    NoiseType = NoiseType.Brown,
    FilterType = NoiseFilterType.LowPass,
    FilterFrequency = 200f,
    Volume = 0.5f,
    Continuous = true
};

var surfaceWaves = new NoiseGenerator
{
    NoiseType = NoiseType.Pink,
    FilterType = NoiseFilterType.BandPass,
    FilterFrequency = 1000f,
    FilterResonance = 0.3f,
    Volume = 0.3f,
    Continuous = true
};
```

## Technical Details

### Noise Generation Algorithms

#### White Noise
Simple random number generation with uniform distribution in range [-1, 1].

#### Pink Noise (Voss-McCartney)
Uses 16 octave bands updated at different rates:
- Band 0 updates every sample
- Band 1 updates every 2 samples
- Band n updates every 2^n samples

This creates the characteristic 1/f rolloff with minimal computation.

#### Brown Noise (Integration)
```
brownState = brownState * 0.998 + whiteNoise * 0.02
```
The leak factor (0.998) prevents DC drift while maintaining low-frequency energy.

#### Blue/Violet Noise (Differentiation)
```
blueNoise = (currentWhite - previousWhite) * 0.5
violetNoise = blueNoise * 2.0
```

### Filter Implementation

Biquad filter with coefficients calculated using standard audio EQ cookbook formulas:

- **Q range**: 0.5 (no resonance) to 10.0 (maximum resonance)
- **Frequency range**: 20Hz to 45% of sample rate
- **Structure**: Direct Form II Transposed for stability

### Stereo Width

When `StereoWidth > 0`:
- Left channel: Primary noise source
- Right channel: Blend of primary noise and independent secondary noise
- `StereoWidth = 0`: Mono (identical L/R)
- `StereoWidth = 1`: Full stereo (independent L/R noise)

### Gain Smoothing

Volume changes are smoothed with a coefficient of 0.001 to prevent clicks and pops during parameter changes.

### Memory and CPU Usage

- Minimal memory footprint (~100 bytes state per instance)
- Pink noise: 16 float array for octave bands
- CPU efficient: No FFT or heavy computation
- Independent filter state for left and right channels
