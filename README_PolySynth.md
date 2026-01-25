# PolySynth

## Overview

PolySynth is a polyphonic synthesizer with configurable voice count, ADSR envelope, voice stealing strategies, and LFO modulation. It builds upon the foundation of SimpleSynth while adding true polyphony, making it suitable for pads, chords, and any situation requiring multiple simultaneous notes. The synth implements both `ISynth` and `IPresetProvider` interfaces, supporting preset save/load functionality.

## Features

- Configurable polyphony (default 16 voices)
- Five voice stealing modes for intelligent voice management
- Full ADSR envelope control (Attack, Decay, Sustain, Release)
- Five classic waveforms (Sine, Square, Sawtooth, Triangle, Noise)
- Lowpass filter with cutoff and resonance
- Detune control in cents
- Vibrato LFO with configurable depth
- Filter LFO modulation
- Preset save/load support via IPresetProvider
- Thread-safe voice management with note-to-voice mapping
- Retrigger support for same-note playing

## Parameters

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| Waveform | WaveType | 0-4 (enum) | Sawtooth | Oscillator waveform type |
| Volume | float | 0.0 - 1.0 | 0.5 | Master output volume |
| Cutoff | float | 0.0 - 1.0 | 1.0 | Lowpass filter cutoff (normalized) |
| Resonance | float | 0.0 - 1.0 | 0.0 | Lowpass filter resonance |
| Detune | float | -1200 to +1200 | 0.0 | Global detune in cents |
| Attack | double | 0.0 - 10.0+ (seconds) | 0.01 | Envelope attack time |
| Decay | double | 0.0 - 10.0+ (seconds) | 0.1 | Envelope decay time |
| Sustain | double | 0.0 - 1.0 | 0.7 | Envelope sustain level |
| Release | double | 0.0 - 10.0+ (seconds) | 0.3 | Envelope release time |
| VibratoDepth | float | semitones | 0.0 | Vibrato LFO depth |
| FilterLFODepth | float | 0.0 - 1.0 | 0.0 | Filter modulation depth |
| StealMode | VoiceStealMode | 0-5 (enum) | Oldest | Voice stealing strategy |
| Name | string | - | "PolySynth" | Synth identifier |

## Waveforms

| WaveType | Value | Description |
|----------|-------|-------------|
| Sine | 0 | Pure sine wave - smooth, fundamental tone |
| Square | 1 | 50% duty cycle square wave - hollow, clarinet-like |
| Sawtooth | 2 | Ramp-up sawtooth - bright, rich in harmonics (default) |
| Triangle | 3 | Linear up/down triangle - softer, flute-like |
| Noise | 4 | White noise - random samples for effects |

## Voice Stealing Modes

| VoiceStealMode | Description |
|----------------|-------------|
| None | No stealing - new notes are ignored when all voices are active |
| Oldest | Steal the voice that was triggered earliest |
| Quietest | Steal the voice with the lowest current amplitude |
| Lowest | Steal the voice playing the lowest note |
| Highest | Steal the voice playing the highest note |
| SameNote | Steal if the same note is already playing, otherwise fall back to Oldest |

## Usage Example

```csharp
using MusicEngine.Core;
using NAudio.Wave;

// Create a 16-voice polyphonic synth
var synth = new PolySynth(maxVoices: 16);

// Configure the synth for a pad sound
synth.Waveform = WaveType.Sawtooth;
synth.Volume = 0.6f;
synth.Cutoff = 0.5f;
synth.Attack = 0.3;
synth.Decay = 0.5;
synth.Sustain = 0.8;
synth.Release = 1.0;
synth.StealMode = VoiceStealMode.Quietest;

// Add vibrato
synth.VibratoLFO = new LFO { Rate = 5.0f, Enabled = true };
synth.VibratoDepth = 0.1f; // 0.1 semitones

// Play a chord (C Major)
synth.NoteOn(60, 100); // C4
synth.NoteOn(64, 90);  // E4
synth.NoteOn(67, 85);  // G4

// Use with NAudio
using var waveOut = new WaveOutEvent();
waveOut.Init(synth);
waveOut.Play();

// Release notes
Thread.Sleep(2000);
synth.NoteOff(60);
synth.NoteOff(64);
synth.NoteOff(67);

// Or release all notes at once
synth.AllNotesOff();

// Check voice usage
Console.WriteLine($"Active voices: {synth.ActiveVoiceCount} / {synth.MaxVoices}");

// Set parameters by name (useful for MIDI CC / automation)
synth.SetParameter("attack", 0.5f);
synth.SetParameter("cutoff", 0.7f);
synth.SetParameter("vibrato", 0.2f);
```

## Preset Management

```csharp
// Save current settings as preset data
Dictionary<string, object> presetData = synth.GetPresetData();

// Load preset data
synth.LoadPresetData(presetData);

// Listen for preset changes
synth.PresetChanged += (sender, args) =>
{
    Console.WriteLine("Preset was loaded!");
};
```

## Audio Signal Flow

```
MIDI Note Input (per voice)
      |
      v
+------------------+
|   Voice Pool     |  Up to MaxVoices simultaneous voices
|   (16 default)   |  with voice stealing when full
+------------------+
      |
      v (each voice)
+------------------+
|   Oscillator     |  Generates waveform based on WaveType
|   + Detune       |  Global detune + vibrato LFO
|   + Vibrato LFO  |
+------------------+
      |
      v
+------------------+
|   ADSR Envelope  |  Attack -> Decay -> Sustain -> Release
|                  |  Velocity-scaled output
+------------------+
      |
      v
+------------------+
|   Voice Mixer    |  Sum all active voices
+------------------+
      |
      v
+------------------+
|   Lowpass Filter |  One-pole filter
|   + Filter LFO   |  Cutoff modulated by LFO
+------------------+
      |
      v
+------------------+
|   Master Volume  |  Final output level
+------------------+
      |
      v
   Audio Output
```

## Internal Architecture

### Voice Management
- Each voice has its own `Envelope` instance for independent amplitude control
- Note-to-voice mapping (`_noteToVoice`) tracks which note is playing on which voice
- Retrigger: Playing a note that's already active retriggers the same voice
- Thread-safe operation via lock on all voice operations

### Voice Lifecycle
1. **NoteOn**: Find free voice or steal based on StealMode
2. **Playing**: Voice processes samples with envelope modulation
3. **NoteOff**: Voice enters release stage
4. **Idle**: Voice becomes available for reuse when envelope completes

### LFO Integration
- `VibratoLFO`: Modulates pitch of all voices (depth in semitones)
- `FilterLFO`: Modulates filter cutoff (depth normalized 0-1)

## Tips & Tricks

- **For pads**: Use long Attack (0.3-0.8s), high Sustain (0.7-0.9), long Release (1.0-2.0s), and lower Cutoff (0.3-0.5) with Sawtooth waveform.

- **For leads**: Short Attack (0.01s), moderate Decay (0.2s), medium Sustain (0.6), short Release (0.2s) with Sawtooth and higher Cutoff.

- **For bass**: Use Square wave with low Cutoff (0.2-0.4), short envelope times, and consider using Detune for a thicker sound.

- **Voice stealing strategy**:
  - Use `Oldest` for most musical results (default)
  - Use `Quietest` for pads where fading notes can be sacrificed
  - Use `SameNote` for monophonic-style playing with sustain

- **Detune for thickness**: Even small detune values (5-15 cents) can add warmth and presence to the sound.

- **Vibrato**: Start with subtle values (0.05-0.15 semitones) at slow rates (4-6 Hz) for natural vibrato.

- **Filter LFO**: Use at low depths (0.1-0.3) with slow rates for subtle movement, or higher for wah-wah effects.

- **Performance**: Monitor `ActiveVoiceCount` to ensure you're not hitting voice limits. Consider reducing `MaxVoices` on lower-powered systems.

## Comparison with SimpleSynth

| Feature | SimpleSynth | PolySynth |
|---------|-------------|-----------|
| Polyphony | Mono (stacking) | True poly (16 voices) |
| Envelope | Fixed fade | Full ADSR |
| Voice stealing | None | 5 modes |
| LFO | None | Vibrato + Filter |
| Detune | None | Global detune |
| Presets | No | Yes (IPresetProvider) |
| Complexity | Simple | Moderate |
