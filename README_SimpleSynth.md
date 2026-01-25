# SimpleSynth

## Overview

SimpleSynth is a lightweight monophonic synthesizer designed for basic sound generation tasks. It provides essential waveform generation with a simple lowpass filter, making it ideal for learning, prototyping, or situations where a minimal-footprint synth is needed. The synth implements the `ISynth` interface and can be used directly as an `ISampleProvider` with NAudio.

## Features

- Monophonic synthesis with automatic fade-in/fade-out for click-free note transitions
- Five classic waveforms (Sine, Square, Sawtooth, Triangle, Noise)
- Simple one-pole lowpass filter with cutoff and resonance controls
- Thread-safe note management
- MIDI note and velocity support
- Real-time parameter control via `SetParameter()` method
- Automatic voice management with smooth release

## Parameters

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| Waveform | WaveType | 0-4 (enum) | Sine | Oscillator waveform type |
| Cutoff | float | 0.0 - 1.0 | 1.0 | Lowpass filter cutoff frequency (normalized) |
| Resonance | float | 0.0 - 1.0 | 0.0 | Lowpass filter resonance (currently affects alpha calculation) |
| Name | string | - | "SimpleSynth" | Synth identifier for routing/display |

## Waveforms

| WaveType | Value | Description |
|----------|-------|-------------|
| Sine | 0 | Pure sine wave - smooth, fundamental tone |
| Square | 1 | 50% duty cycle square wave - hollow, clarinet-like tone |
| Sawtooth | 2 | Ramp-up sawtooth - bright, buzzy tone rich in harmonics |
| Triangle | 3 | Linear up/down triangle - softer than square, flute-like |
| Noise | 4 | White noise - random samples, useful for percussion/effects |

## Usage Example

```csharp
using MusicEngine.Core;
using NAudio.Wave;

// Create a SimpleSynth instance
var synth = new SimpleSynth();

// Configure the synth
synth.Waveform = WaveType.Sawtooth;
synth.Cutoff = 0.7f;
synth.Resonance = 0.2f;

// Play a note (Middle C, velocity 100)
synth.NoteOn(60, 100);

// Use with NAudio for playback
using var waveOut = new WaveOutEvent();
waveOut.Init(synth);
waveOut.Play();

// Wait, then release the note
Thread.Sleep(1000);
synth.NoteOff(60);

// Or stop all notes at once
synth.AllNotesOff();

// Alternative: Set parameters by name (useful for automation)
synth.SetParameter("waveform", 2); // Sawtooth
synth.SetParameter("cutoff", 0.5f);
synth.SetParameter("resonance", 0.3f);
```

## Audio Signal Flow

```
MIDI Note Input
      |
      v
+------------------+
|   Oscillator     |  Generates waveform based on WaveType
|  (Frequency from |  (Sine/Square/Sawtooth/Triangle/Noise)
|   MIDI note)     |
+------------------+
      |
      v
+------------------+
|   Amplitude      |  Velocity-scaled amplitude
|   (Velocity/127) |  with fade-in envelope
+------------------+
      |
      v
+------------------+
|   Lowpass Filter |  One-pole filter controlled by
|   (Cutoff)       |  Cutoff parameter (alpha = cutoff^2 * 0.5)
+------------------+
      |
      v
+------------------+
|   Fade Control   |  Linear fade-in on NoteOn
|   (Gain)         |  Exponential fade-out on NoteOff
+------------------+
      |
      v
   Audio Output
```

## Internal Architecture

SimpleSynth uses an internal `Oscillator` class that manages:
- Phase accumulation for continuous waveform generation
- Per-voice amplitude with velocity scaling
- Filter state (`LastSample`) for the lowpass filter
- Automatic fade-in (linear, 0.01 increment per sample)
- Automatic fade-out (exponential, 0.995 multiplier)

Multiple simultaneous notes are supported through the `_activeOscillators` list, though this is typically used in monophonic context.

## Tips & Tricks

- **For bass sounds**: Use Sawtooth or Square waveform with Cutoff around 0.3-0.5 for a warm, filtered bass tone.

- **For lead sounds**: Use Sawtooth with higher Cutoff (0.7-0.9) for a bright, cutting lead sound.

- **For pads**: Use Sine with full Cutoff for a pure, organ-like tone.

- **For percussion**: Use Noise waveform with quick NoteOff to create hi-hat or snare textures.

- **Prevent clicks**: The built-in fade-in/fade-out handles click prevention automatically. Allow the fade-out to complete before removing the synth from the audio chain.

- **Real-time control**: Use `SetParameter()` for automation-friendly parameter changes that can be controlled by MIDI CC or other sources.

- **Sample rate**: The synth uses `Settings.SampleRate` by default (typically 44100 Hz), but you can specify a custom sample rate in the constructor: `new SimpleSynth(48000)`.

## Limitations

- Monophonic design - for polyphonic playback, consider using `PolySynth` instead
- Simple one-pole filter - for more complex filtering, use `AdvancedSynth` with its multi-mode filter
- No envelope control - attack/decay/sustain/release is fixed (quick attack, exponential release)
- No modulation (LFO, etc.) - for modulation capabilities, use `PolySynth` or `AdvancedSynth`
