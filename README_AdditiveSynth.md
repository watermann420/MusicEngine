# AdditiveSynth

## Overview

AdditiveSynth is a harmonic series synthesizer that creates sounds by combining multiple sine waves (partials) at different frequencies and amplitudes. Unlike subtractive synthesis which starts with a harmonically rich waveform and filters it, additive synthesis builds sounds from the ground up by precisely controlling each harmonic component.

This approach offers unprecedented control over timbre and is particularly effective for creating organ sounds, bells, and any tone where precise harmonic control is desired. The synthesizer includes a Hammond-style drawbar system for quick organ sounds, as well as individual control over up to 64 harmonic partials.

## Features

- Up to 64 harmonic partials with individual control
- Hammond-style 9-drawbar system for classic organ sounds
- Per-partial amplitude, phase, and detune control
- Per-partial envelope option (or global envelope)
- Polyphonic with configurable voice count (default 16 voices)
- Voice stealing for efficient polyphony management
- ADSR amplitude envelope
- Pitch bend with configurable range
- Anti-aliasing (partials above Nyquist are automatically filtered)
- Soft clipping for overload protection
- 7 built-in factory presets
- String-based drawbar preset notation (e.g., "888000000")

## Architecture

### Harmonic Series

The synthesizer generates sound by summing sine waves at harmonic frequencies:

```
Output = Sum(sin(2*PI*f*n) * amplitude[n])
```

Where `f` is the fundamental frequency and `n` is the harmonic number (1 = fundamental, 2 = octave, 3 = fifth+octave, etc.).

### Partial System

Each partial (harmonic) can be configured with:
- **Harmonic Number**: Which harmonic of the fundamental (1-64)
- **Amplitude**: Volume of this partial (0-1)
- **Phase**: Phase offset (0-1, representing 0-360 degrees)
- **Detune**: Fine tuning in cents (-100 to +100)
- **Envelope**: Optional per-partial envelope (defaults to global)

### Drawbar System

The Hammond-style drawbar system provides quick access to classic organ tones. Each drawbar controls a specific harmonic ratio:

| Drawbar | Index | Footage | Harmonic Ratio | Musical Interval |
|---------|-------|---------|----------------|------------------|
| 1 | 0 | 16' | 0.5 | Sub-octave |
| 2 | 1 | 5 1/3' | 1.5 | Fifth below |
| 3 | 2 | 8' | 1.0 | Fundamental |
| 4 | 3 | 4' | 2.0 | Octave |
| 5 | 4 | 2 2/3' | 3.0 | Octave + Fifth |
| 6 | 5 | 2' | 4.0 | 2 Octaves |
| 7 | 6 | 1 3/5' | 5.0 | 2 Octaves + Major 3rd |
| 8 | 7 | 1 1/3' | 6.0 | 2 Octaves + Fifth |
| 9 | 8 | 1' | 8.0 | 3 Octaves |

Drawbar values range from 0 (off) to 8 (full volume).

## Parameters

### Global Parameters

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| Name | string | - | "AdditiveSynth" | Synth instance name |
| Volume | float | 0-1 | 0.5 | Master output volume |
| MaxVoices | int | 1-128 | 16 | Maximum polyphony |
| PitchBend | float | -1 to 1 | 0 | Current pitch bend value |
| PitchBendRange | float | 0-24 | 2.0 | Pitch bend range in semitones |

### Amplitude Envelope (Global)

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| Attack | double | seconds | 0.01 | Attack time |
| Decay | double | seconds | 0.01 | Decay time |
| Sustain | double | 0-1 | 1.0 | Sustain level |
| Release | double | seconds | 0.05 | Release time |

### Partial Parameters (per harmonic)

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| HarmonicNumber | int | 1-64 | varies | Which harmonic |
| Amplitude | float | 0-1 | varies | Partial volume |
| Phase | float | 0-1 | 0 | Phase offset (0-360 degrees) |
| Detune | float | -100 to 100 | 0 | Fine tuning in cents |
| Envelope | Envelope | - | null | Optional per-partial envelope |

### Drawbar Parameters

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| Drawbars[0-8] | int | 0-8 | 8,8,8,0,0,0,0,0,0 | Hammond-style drawbar levels |

## Parameter String Names

Parameters can be set via `SetParameter(name, value)` using these string names:

**Global:**
- `volume`, `pitchbend`, `pitchbendrange`

**Envelope:**
- `attack`, `decay`, `sustain`, `release`

**Harmonics (replace N with harmonic number):**
- `hN` - Sets harmonic N amplitude (e.g., `h1`, `h2`, `h16`)

**Drawbars (replace N with 0-8):**
- `dN` - Sets drawbar N value (e.g., `d0`, `d1`, `d8`)

## Factory Presets

| Preset | Method | Description |
|--------|--------|-------------|
| Hammond Organ | `CreateHammondOrgan()` | Classic 888000000 drawbar setting |
| Full Organ | `CreateFullOrgan()` | All drawbars at maximum (888888888) |
| Pipe Organ | `CreatePipeOrgan()` | Church organ with slow attack |
| Pure Sine | `CreatePureSine()` | Single fundamental tone |
| Bell Tone | `CreateBellTone()` | Decaying harmonics for bell sound |
| Sawtooth Approx | `CreateSawtoothApprox()` | 32 harmonics at 1/n amplitude |
| Square Approx | `CreateSquareApprox()` | Odd harmonics at 1/n amplitude |

## Drawbar Preset Notation

Drawbar presets can be specified as 9-digit strings where each digit (0-8) represents a drawbar level:

| Notation | Sound Type | Description |
|----------|------------|-------------|
| 888000000 | Jazz Organ | Classic Hammond "full" registration |
| 888888888 | Full Organ | All harmonics at maximum |
| 800000000 | Sub Bass | Just the 16' drawbar |
| 008000000 | Flute | Pure fundamental |
| 008800000 | Flute + Octave | 8' and 4' |
| 888800000 | Blues/Rock | Popular rock organ sound |
| 838000000 | Gospel | Third harmonic emphasized |
| 888000888 | Full + Upper | Bright organ with overtones |

## Usage Example

```csharp
using MusicEngine.Core;

// Create additive synth with default settings
var synth = new AdditiveSynth();

// Or use a factory preset
var hammond = AdditiveSynth.CreateHammondOrgan();

// Configure using drawbars (Hammond style)
synth.SetDrawbars("888000000");

// Or set individual drawbars
synth.SetDrawbar(0, 8);  // 16' at full
synth.SetDrawbar(1, 8);  // 5 1/3' at full
synth.SetDrawbar(2, 8);  // 8' at full
synth.SetDrawbar(3, 4);  // 4' at half

// Configure individual harmonics
synth.SetHarmonic(1, 1.0f);   // Fundamental at full
synth.SetHarmonic(2, 0.5f);   // Octave at half
synth.SetHarmonic(3, 0.3f);   // Fifth at 30%
synth.SetHarmonic(4, 0.25f);  // 2 octaves at 25%

// Or set multiple harmonics at once
synth.SetHarmonics(
    (1, 1.0f),
    (2, 0.5f),
    (3, 0.3f),
    (4, 0.25f),
    (5, 0.2f)
);

// Configure envelope
synth.AmpEnvelope.Attack = 0.005;
synth.AmpEnvelope.Decay = 0.0;
synth.AmpEnvelope.Sustain = 1.0;
synth.AmpEnvelope.Release = 0.05;

// Set global parameters
synth.Volume = 0.6f;
synth.PitchBendRange = 2f;

// Play notes
synth.NoteOn(60, 100);  // Middle C, velocity 100
// ... later
synth.NoteOff(60);

// Using parameter strings
synth.SetParameter("volume", 0.7f);
synth.SetParameter("h1", 1.0f);     // Set harmonic 1
synth.SetParameter("h8", 0.2f);     // Set harmonic 8
synth.SetParameter("d0", 8f);       // Set drawbar 0 to max
synth.SetParameter("attack", 0.01f);
```

## Sound Design Tips

### Classic Hammond Organ
- Use drawbar presets like "888000000" for jazz or "888888888" for full organ
- Keep attack very short (0.001-0.01s) for immediate response
- Full sustain (1.0) for held notes
- Short release (0.05s) for clean note endings
- The 16' (sub) and 5 1/3' (fifth) drawbars add body
- Upper drawbars (1 3/5', 1 1/3', 1') add brightness

### Pipe/Church Organ
- Slower attack (0.1s) for pipe swell characteristic
- Use harmonics 1, 2, 3, 4, 5, 6, 8 with decreasing amplitudes
- Longer release (0.3s) for natural room decay
- Add slight detuning between partials for chorus effect

### Bell and Chime Sounds
- Use many harmonics with gradually decreasing amplitudes
- Long decay (2-4s) with zero sustain
- Very short attack (0.001s) for percussive onset
- Consider inharmonic detuning for metallic character
- Per-partial envelopes with different decay times add realism

### Waveform Approximation

**Sawtooth Wave:**
All harmonics present at amplitude = 1/n
```csharp
for (int i = 1; i <= 32; i++)
    synth.SetHarmonic(i, 1.0f / i);
```

**Square Wave:**
Only odd harmonics at amplitude = 1/n
```csharp
for (int i = 1; i <= 31; i += 2)
    synth.SetHarmonic(i, 1.0f / i);
```

**Triangle Wave:**
Only odd harmonics at amplitude = 1/n^2
```csharp
for (int i = 1; i <= 31; i += 2)
    synth.SetHarmonic(i, 1.0f / (i * i));
```

### Flute/Woodwind
- Emphasize fundamental (harmonic 1) strongly
- Add harmonics 2 and 3 at low levels (0.1-0.2)
- Moderate attack (0.05-0.1s) for breath onset
- Some sustain variation for realism

### Vocal/Choir Pads
- Multiple harmonics at varying levels
- Slow attack (0.3-0.5s) for pad swells
- Per-partial envelopes with staggered attacks
- Slight detuning creates natural chorus
- Add harmonics 1-8 at descending amplitudes

### General Tips
- More harmonics = brighter sound
- Odd harmonics only = hollow/clarinet-like
- Even harmonics = warmer/smoother
- Higher harmonics require lower amplitudes to avoid harshness
- Partials above Nyquist (sample_rate/2) are automatically filtered
- Use detuning sparingly for chorus effects
- Per-partial envelopes add movement and realism
- Velocity affects overall amplitude, not harmonic balance
