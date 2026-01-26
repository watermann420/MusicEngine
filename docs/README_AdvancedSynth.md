# AdvancedSynth

## Overview

AdvancedSynth is a professional-grade polyphonic synthesizer featuring multiple configurable oscillators, five filter types including a Moog-style ladder filter, dual ADSR envelopes (amplitude and filter), LFO modulation, and factory presets. It is designed for complex sound design tasks requiring precise control over oscillator mixing, detuning, and filter characteristics.

## Features

- Up to 16-voice polyphony with voice stealing
- Multiple oscillators (default 3) with independent configuration
- Per-oscillator controls: waveform, level, detune, octave, semitone, pulse width, phase
- Five filter types: None, LowPass, HighPass, BandPass, Notch, MoogLadder
- Dual ADSR envelopes for amplitude and filter
- Filter envelope modulation with positive/negative depth
- Pitch LFO (vibrato) and Filter LFO modulation
- Glide/portamento time control
- Soft clipping (tanh) for warm overdrive characteristics
- Four factory presets: Lead, Pad, Bass, Pluck

## Parameters

### Global Parameters

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| Volume | float | 0.0 - 1.0 | 0.5 | Master output volume |
| Name | string | - | "AdvancedSynth" | Synth identifier |
| StealMode | VoiceStealMode | enum | Oldest | Voice stealing strategy |
| GlideTime | float | 0.0 - 10.0+ (seconds) | 0.0 | Portamento time |

### Filter Parameters

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| FilterType | SynthFilterType | enum | LowPass | Filter algorithm type |
| FilterCutoff | float | 0.0 - 1.0 | 0.8 | Filter cutoff (20Hz - 20kHz mapped) |
| FilterResonance | float | 0.0 - 1.0 | 0.2 | Filter resonance/Q factor |
| FilterEnvAmount | float | -1.0 - 1.0 | 0.3 | Filter envelope modulation depth |

### Amplitude Envelope (ADSR)

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| Attack | double | 0.0 - 10.0+ (seconds) | 0.01 | Attack time |
| Decay | double | 0.0 - 10.0+ (seconds) | 0.1 | Decay time |
| Sustain | double | 0.0 - 1.0 | 0.7 | Sustain level |
| Release | double | 0.0 - 10.0+ (seconds) | 0.3 | Release time |

### Filter Envelope (ADSR)

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| FilterAttack | double | 0.0 - 10.0+ (seconds) | 0.01 | Filter envelope attack |
| FilterDecay | double | 0.0 - 10.0+ (seconds) | 0.2 | Filter envelope decay |
| FilterSustain | double | 0.0 - 1.0 | 0.5 | Filter envelope sustain |
| FilterRelease | double | 0.0 - 10.0+ (seconds) | 0.5 | Filter envelope release |

### LFO Parameters

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| PitchLFO | LFO | object | null | LFO for vibrato modulation |
| PitchLFODepth | float | semitones | 0.0 | Pitch modulation depth |
| FilterLFO | LFO | object | null | LFO for filter modulation |
| FilterLFODepth | float | 0.0 - 1.0 | 0.0 | Filter modulation depth |

### Oscillator Configuration (OscillatorConfig)

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| Waveform | WaveType | enum | Sawtooth | Oscillator waveform |
| Level | float | 0.0 - 1.0 | 1/n | Oscillator mix level |
| Detune | float | -100 to +100 cents | 0-7 (per osc) | Fine tuning |
| Octave | int | -2 to +2 | 0 | Octave transpose |
| Semitone | int | -12 to +12 | 0 | Semitone transpose |
| PulseWidth | float | 0.1 - 0.9 | 0.5 | Square wave duty cycle |
| Phase | float | 0.0 - 1.0 | 0.0 | Initial phase offset |
| Enabled | bool | true/false | osc0: true | Oscillator active state |

## Filter Types

| SynthFilterType | Description |
|-----------------|-------------|
| None | Bypass filter processing |
| LowPass | One-pole lowpass - removes high frequencies |
| HighPass | One-pole highpass - removes low frequencies |
| BandPass | Two-pole bandpass - isolates frequency band |
| Notch | Notch filter - removes specific frequency |
| MoogLadder | Moog-style 4-pole ladder filter - classic analog warmth |

## Waveforms

| WaveType | Description |
|----------|-------------|
| Sine | Pure fundamental - clean, organ-like |
| Square | Variable pulse width - hollow, woody |
| Sawtooth | Rich harmonics - bright, brassy |
| Triangle | Odd harmonics only - soft, flute-like |
| Noise | White noise - for textures and effects |

## Usage Example

```csharp
using MusicEngine.Core;
using NAudio.Wave;

// Create a 3-oscillator synth
var synth = new AdvancedSynth(oscillatorCount: 3);

// Configure oscillators for a rich pad
synth.SetOscillator(0, WaveType.Sawtooth, level: 0.5f, detune: -5);
synth.SetOscillator(1, WaveType.Sawtooth, level: 0.5f, detune: 0);
synth.SetOscillator(2, WaveType.Sawtooth, level: 0.5f, detune: 5);

// Configure filter
synth.FilterType = SynthFilterType.MoogLadder;
synth.FilterCutoff = 0.5f;
synth.FilterResonance = 0.3f;
synth.FilterEnvAmount = 0.4f;

// Configure amplitude envelope
synth.Attack = 0.3;
synth.Decay = 0.5;
synth.Sustain = 0.8;
synth.Release = 1.5;

// Configure filter envelope
synth.FilterAttack = 0.1;
synth.FilterDecay = 0.4;
synth.FilterSustain = 0.3;
synth.FilterRelease = 0.8;

// Add vibrato
synth.PitchLFO = new LFO { Rate = 5.0f, Enabled = true };
synth.PitchLFODepth = 0.1f;

// Play notes
synth.NoteOn(60, 100);
synth.NoteOn(64, 90);
synth.NoteOn(67, 85);

// Use with NAudio
using var waveOut = new WaveOutEvent();
waveOut.Init(synth);
waveOut.Play();

// Release
Thread.Sleep(3000);
synth.AllNotesOff();
```

## Factory Presets

```csharp
// Classic analog lead
var lead = AdvancedSynth.CreateLeadPreset();

// Lush pad
var pad = AdvancedSynth.CreatePadPreset();

// Punchy bass
var bass = AdvancedSynth.CreateBassPreset();

// Plucky sound
var pluck = AdvancedSynth.CreatePluckPreset();
```

### Preset Details

| Preset | Oscillators | Filter | Envelope Character |
|--------|-------------|--------|-------------------|
| Lead | Saw + Square (detuned) | MoogLadder, cutoff 0.6 | Punchy attack, moderate sustain |
| Pad | 3x Saw (spread detune) | LowPass, cutoff 0.4 | Slow attack, long release |
| Bass | Square (narrow PW) + Saw (-1 oct) | MoogLadder, cutoff 0.3 | Instant attack, short release |
| Pluck | Saw + Triangle (+1 oct) | LowPass, cutoff 0.5 | Zero sustain, percussive |

## Audio Signal Flow

```
MIDI Note Input
      |
      v
+----------------------+
|    Voice Allocator   |  16 voices with stealing
+----------------------+
      |
      v (per voice)
+----------------------+
|  Oscillator Bank     |  Multiple oscillators mixed
|  +------------------+|
|  | OSC 1: Waveform  ||  Each with:
|  | Level, Detune    ||  - Independent waveform
|  | Octave, Semitone ||  - Level mixing
|  | PulseWidth       ||  - Pitch offset (oct/semi/detune)
|  +------------------+|  - Phase offset
|  | OSC 2            ||  - Pulse width (square)
|  +------------------+|
|  | OSC 3            ||
|  +------------------+|
+----------------------+
      |
      + Pitch LFO (vibrato modulation)
      |
      v
+----------------------+
|   Oscillator Mixer   |  Normalized mix of enabled oscillators
+----------------------+
      |
      v
+----------------------+
|   Multi-Mode Filter  |  LowPass / HighPass / BandPass /
|                      |  Notch / MoogLadder
|   + Filter Envelope  |  Cutoff modulated by filter ADSR
|   + Filter LFO       |  Additional LFO modulation
+----------------------+
      |
      v
+----------------------+
|   Amplitude Envelope |  ADSR with velocity scaling
+----------------------+
      |
      v
+----------------------+
|   Master Volume      |  Global level control
+----------------------+
      |
      v
+----------------------+
|   Soft Clipping      |  tanh() saturation
|   (Voice Sum)        |  Prevents digital clipping
+----------------------+
      |
      v
   Audio Output
```

## Internal Architecture

### Voice System (AdvancedVoice)
- Each voice maintains independent filter state (4 state variables for ladder filter)
- Dual envelope processing per sample
- Per-voice oscillator phase tracking
- Trigger time tracking for voice stealing

### Filter Implementation
- **LowPass/HighPass**: One-pole RC filter approximation
- **BandPass**: Two-pole biquad implementation
- **Notch**: Biquad notch filter
- **MoogLadder**: 4-stage cascade with feedback, classic Moog character

### Oscillator Mixing
- All enabled oscillators are summed
- Output normalized by total active level (divided by max(1, totalLevel * 0.5))
- Prevents clipping while maintaining punch

## Tips & Tricks

- **Thick pads**: Use 3 oscillators with slight detune spread (-5, 0, +5 cents) and all set to Sawtooth.

- **Aggressive bass**: Use MoogLadder filter with high resonance (0.5-0.7), fast filter envelope decay, and FilterEnvAmount > 0.5.

- **PWM sound**: Use Square wave, then automate or LFO-modulate PulseWidth between 0.3 and 0.7 for classic PWM.

- **Sub bass layer**: Set one oscillator to Sine at -1 or -2 octaves for a solid sub foundation.

- **Filter envelope tricks**:
  - Positive FilterEnvAmount: "wow" sound, opens filter on attack
  - Negative FilterEnvAmount: "wah" sound, closes filter on attack

- **Resonance sweetspot**: For MoogLadder, 0.3-0.5 resonance gives warmth without self-oscillation.

- **Soft clipping**: The built-in tanh() saturation adds subtle warmth when voices are pushed. Don't be afraid to use higher volumes for gentle overdrive.

- **CPU optimization**: Disable unused oscillators (set Enabled = false) to save processing.

## Comparison with Other Synths

| Feature | SimpleSynth | PolySynth | AdvancedSynth |
|---------|-------------|-----------|---------------|
| Polyphony | Mono | 16 voices | 16 voices |
| Oscillators | 1 | 1 | 1-N (default 3) |
| Envelope | Fixed | 1x ADSR | 2x ADSR |
| Filter types | 1 (LP) | 1 (LP) | 5 types |
| Filter envelope | No | No | Yes |
| LFO | No | 2 (Vib/Flt) | 2 (Pitch/Flt) |
| Oscillator detune | No | Global | Per-oscillator |
| Pulse width | No | No | Yes |
| Presets | No | Yes | Yes (factory) |
| Soft clip | No | No | Yes |

## Parameter Automation

```csharp
// All parameters can be set by name for automation
synth.SetParameter("volume", 0.8f);
synth.SetParameter("cutoff", 0.6f);        // or "filtercutoff"
synth.SetParameter("resonance", 0.4f);     // or "filterresonance"
synth.SetParameter("filterenvamount", 0.5f);
synth.SetParameter("attack", 0.1f);
synth.SetParameter("decay", 0.3f);
synth.SetParameter("sustain", 0.7f);
synth.SetParameter("release", 0.5f);
synth.SetParameter("filterattack", 0.05f);
synth.SetParameter("filterdecay", 0.2f);
synth.SetParameter("filtersustain", 0.4f);
synth.SetParameter("filterrelease", 0.3f);
synth.SetParameter("vibrato", 0.15f);      // or "pitchlfodepth"
synth.SetParameter("filterlfodepth", 0.2f);
synth.SetParameter("glide", 0.1f);         // or "portamento"
```
