# SpeechSynth (Talk Box / Vocoder)

## Overview

Formant-based speech and singing synthesizer with text-to-speech support, vowel morphing, and real-time phoneme control. SpeechSynth uses glottal pulse oscillators filtered through formant filter banks to create realistic vocal sounds, from choir pads to robot voices to whispered speech.

## Features

- **Vowel synthesis** - 14 vowels including A, E, I, O, U, Schwa, and German umlauts
- **Consonant generation** - Plosives, fricatives, nasals, and approximants
- **Text-to-Speech mode** - Convert text to phoneme sequences automatically
- **Singing mode** - Map lyrics syllables to MIDI notes
- **Vowel morphing** - Smooth interpolation between vowels with configurable speed
- **Formant control** - Shift formants up/down for gender/character changes
- **Multiple glottal waveforms** - Pulse, LF Model, Rosenberg, Klatt
- **Vibrato** - Natural pitch variation with rate and depth control
- **Voice characteristics** - Tension, breathiness, jitter, shimmer for natural variation
- **Multi-language support** - English and German phoneme rules

## Vowels and Formants

Formant frequencies define the characteristic sound of each vowel. Based on Peterson & Barney (1952) acoustic phonetics research.

### Standard Vowels

| Vowel | IPA | Example | F1 (Hz) | F2 (Hz) | F3 (Hz) | F4 (Hz) | F5 (Hz) |
|-------|-----|---------|---------|---------|---------|---------|---------|
| `A` | /a:/ | father | 730 | 1090 | 2440 | 3400 | 4500 |
| `E` | /e/ | hey | 390 | 2300 | 2850 | 3500 | 4500 |
| `I` | /i:/ | feet | 270 | 2290 | 3010 | 3600 | 4500 |
| `O` | /o:/ | go | 360 | 640 | 2390 | 3400 | 4500 |
| `U` | /u:/ | food | 300 | 870 | 2240 | 3400 | 4500 |

### Additional Vowels

| Vowel | IPA | Example | F1 (Hz) | F2 (Hz) | F3 (Hz) | F4 (Hz) | F5 (Hz) |
|-------|-----|---------|---------|---------|---------|---------|---------|
| `Schwa` | /@/ | about | 500 | 1500 | 2500 | 3500 | 4500 |
| `Ae` | /ae/ | cat | 660 | 1720 | 2410 | 3400 | 4500 |
| `Eh` | /E/ | bet | 530 | 1840 | 2480 | 3500 | 4500 |
| `Ih` | /I/ | bit | 390 | 1990 | 2550 | 3500 | 4500 |
| `Ah` | /V/ | but | 640 | 1190 | 2390 | 3400 | 4500 |
| `Uh` | /U/ | book | 440 | 1020 | 2240 | 3400 | 4500 |

### German Umlauts

| Vowel | IPA | Example | F1 (Hz) | F2 (Hz) | F3 (Hz) | F4 (Hz) | F5 (Hz) |
|-------|-----|---------|---------|---------|---------|---------|---------|
| `Oe` | /o:/ | schon | 370 | 1350 | 2400 | 3400 | 4500 |
| `Ue` | /y:/ | uber | 270 | 1800 | 2200 | 3400 | 4500 |
| `Umlaut_A` | /E:/ | Manner | 350 | 1750 | 2500 | 3500 | 4500 |

## Consonants

Consonants are generated using filtered noise and voice modulation.

### Consonant Types

| Type | Consonants | Description |
|------|------------|-------------|
| **Plosives** | P, B, T, D, K, G | Stop consonants with burst |
| **Fricatives** | F, V, S, Z, Sh, Zh, H, Th, Dh | Continuous turbulent noise |
| **Nasals** | M, N, Ng | Nasal resonance |
| **Approximants** | L, R, W, Y | Vowel-like consonants |

### Consonant Properties

| Consonant | Voiced | Type | Noise Freq (Hz) | Duration (s) |
|-----------|--------|------|-----------------|--------------|
| P | No | Plosive | 500 | 0.01 |
| B | Yes | Plosive | 200 | 0.02 |
| T | No | Plosive | 4000 | 0.01 |
| D | Yes | Plosive | 3000 | 0.02 |
| K | No | Plosive | 2000 | 0.02 |
| G | Yes | Plosive | 1500 | 0.03 |
| F | No | Fricative | 8000 | 0.10 |
| V | Yes | Fricative | 6000 | 0.10 |
| S | No | Fricative | 8000 | 0.10 |
| Z | Yes | Fricative | 6000 | 0.10 |
| Sh | No | Fricative | 4000 | 0.12 |
| Zh | Yes | Fricative | 3500 | 0.10 |
| H | No | Fricative | 2000 | 0.08 |
| M | Yes | Nasal | 250 | 0.08 |
| N | Yes | Nasal | 280 | 0.08 |
| Ng | Yes | Nasal | 300 | 0.08 |
| L | Yes | Approximant | 350 | 0.06 |
| R | Yes | Approximant | 1200 | 0.06 |
| W | Yes | Approximant | 300 | 0.05 |
| Y | Yes | Approximant | 2300 | 0.05 |

## Parameters

### Main Parameters

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| `Name` | `string` | - | `"SpeechSynth"` | Synth name |
| `MaxVoices` | `int` | 1-128 | `8` | Maximum polyphony |
| `Volume` | `float` | 0-2 | `0.8` | Master volume |

### Glottal Source Parameters

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| `GlottalWaveform` | `GlottalWaveform` | enum | `LFModel` | Voice source waveform type |
| `Tension` | `float` | 0-1 | `0.5` | Glottal tension (higher = tighter voice) |
| `OpenQuotient` | `float` | 0.3-0.7 | `0.55` | Portion of cycle glottis is open |
| `Breathiness` | `float` | 0-1 | `0.1` | Amount of aspiration noise |
| `Jitter` | `float` | 0-0.05 | `0.01` | Pitch variation for natural sound |
| `Shimmer` | `float` | 0-0.1 | `0.02` | Amplitude variation for natural sound |

### Glottal Waveform Types

| Type | Description |
|------|-------------|
| `Pulse` | Simple pulse wave - synthetic sounding |
| `LFModel` | Liljencrants-Fant model - natural sounding (default) |
| `Rosenberg` | Classic speech synthesis model |
| `Klatt` | Formant synthesizer standard |

### Formant Parameters

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| `CurrentVowel` | `Vowel` | enum | `A` | Current vowel phoneme |
| `FormantShift` | `float` | -12 to 12 | `0` | Formant shift in semitones |
| `MorphSpeed` | `float` | 0.001-1 | `0.05` | Vowel transition speed |

### Vibrato Parameters

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| `VibratoDepth` | `float` | 0-2 | `0.3` | Vibrato depth in semitones |
| `VibratoRate` | `float` | 0.1-15 | `5.5` | Vibrato rate in Hz |

### MIDI Parameters

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| `PitchBend` | `float` | -1 to 1 | `0` | Current pitch bend value |
| `PitchBendRange` | `float` | 0-24 | `2` | Pitch bend range in semitones |

### Text-to-Speech Parameters

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| `Language` | `SpeechLanguage` | enum | `English` | Language for TTS conversion |
| `SingingMode` | `bool` | - | `false` | Enable lyrics-to-notes mapping |

## Usage Example

### Basic Vowel Synthesis

```csharp
// Create a speech synth
var speech = new SpeechSynth();

// Set the vowel
speech.SetVowel(Vowel.A);

// Play a note (pitch determines fundamental frequency)
speech.NoteOn(60, 100);  // Middle C, "Aah" sound

// Change vowel while note is playing
speech.SetVowel(Vowel.O);  // Morphs to "Ooh"

// Release
speech.NoteOff(60);
```

### Text-to-Speech

```csharp
var speech = new SpeechSynth();
speech.Language = SpeechLanguage.English;

// Speak text at a specific pitch
speech.Speak("Hello World", note: 60, velocity: 100);

// German text
speech.Language = SpeechLanguage.German;
speech.Speak("Guten Tag", note: 55, velocity: 80);
```

### Singing Mode with Lyrics

```csharp
var speech = new SpeechSynth();
speech.SingingMode = true;

// Map syllables to MIDI notes
// Format: "note:syllable note:syllable ..."
speech.LyricsMapper.ParseLyrics("60:hel 62:lo 64:world");

// Now when notes are played, they trigger the mapped syllables
speech.NoteOn(60, 100);  // Sings "hel"
speech.NoteOff(60);
speech.NoteOn(62, 100);  // Sings "lo"
speech.NoteOff(62);
speech.NoteOn(64, 100);  // Sings "world"
speech.NoteOff(64);
```

### Creating a Choir Pad

```csharp
// Use the choir preset
var choir = SpeechSynth.CreateChoirPreset();

// Add notes for chord
choir.SetVowel(Vowel.A);
choir.NoteOn(48, 80);  // C3
choir.NoteOn(52, 80);  // E3
choir.NoteOn(55, 80);  // G3
choir.NoteOn(60, 80);  // C4

// Slowly morph to "Ooh"
choir.MorphSpeed = 0.01f;  // Slow morph
choir.SetVowel(Vowel.O);
```

### Robot Voice

```csharp
var robot = SpeechSynth.CreateRobotPreset();

// No vibrato, no natural variation
robot.VibratoDepth = 0;
robot.Jitter = 0;
robot.Shimmer = 0;

// Use pulse waveform for synthetic sound
robot.GlottalWaveform = GlottalWaveform.Pulse;

// Shift formants down for deeper robot
robot.FormantShift = -4f;

robot.Speak("I am a robot", note: 48, velocity: 100);
```

## Creating Talking Sounds

### Tips for Realistic Speech

1. **Use the LF Model waveform** - Most natural sounding for speech
2. **Add slight jitter and shimmer** - Human voices have natural variation
3. **Use appropriate breathiness** - Real voices have some aspiration
4. **Morph between vowels** - Don't switch vowels instantly

```csharp
var speech = new SpeechSynth
{
    GlottalWaveform = GlottalWaveform.LFModel,
    Tension = 0.5f,
    OpenQuotient = 0.55f,
    Breathiness = 0.15f,
    Jitter = 0.012f,
    Shimmer = 0.025f,
    MorphSpeed = 0.03f,
    VibratoDepth = 0.2f,
    VibratoRate = 5f
};
```

### Gender/Age Transformation

Use `FormantShift` to change perceived gender/age:

| FormantShift | Effect |
|--------------|--------|
| -6 to -12 | Deep male / bass voice |
| -3 to -6 | Adult male |
| 0 | Neutral |
| +3 to +6 | Adult female |
| +6 to +12 | Child voice |

### Whisper Effect

```csharp
var whisper = SpeechSynth.CreateWhisperPreset();
// High breathiness, low tension, no vibrato
whisper.Breathiness = 0.8f;
whisper.Tension = 0.2f;
whisper.VibratoDepth = 0f;
whisper.Volume = 0.5f;  // Whispers are quieter
```

### Talk Box Effect

For classic "talk box" sounds, modulate the vowel in real-time:

```csharp
var talkbox = new SpeechSynth
{
    GlottalWaveform = GlottalWaveform.LFModel,
    Tension = 0.6f,
    Breathiness = 0.05f,
    MorphSpeed = 0.1f  // Fast morphing for "wah" effects
};

// Start a note
talkbox.NoteOn(60, 100);

// Modulate vowel for "wah" effect
talkbox.SetVowel(Vowel.A);  // "Ah"
// ... wait ...
talkbox.SetVowel(Vowel.U);  // "Oo" - creates "wah" transition
```

## Presets

SpeechSynth includes factory presets:

| Preset | Description |
|--------|-------------|
| `CreateChoirPreset()` | Natural choir "Aah" sound with vibrato |
| `CreateRobotPreset()` | Synthetic robot voice, no variation |
| `CreateWhisperPreset()` | Breathy whisper, very soft |
| `CreateOperaPreset()` | Powerful opera singer with strong vibrato |
| `CreateBassVoicePreset()` | Deep bass voice with low formants |
| `CreateChildVoicePreset()` | High-pitched child voice |

```csharp
var choir = SpeechSynth.CreateChoirPreset();
var robot = SpeechSynth.CreateRobotPreset();
var whisper = SpeechSynth.CreateWhisperPreset();
var opera = SpeechSynth.CreateOperaPreset();
var bass = SpeechSynth.CreateBassVoicePreset();
var child = SpeechSynth.CreateChildVoicePreset();
```

## SetParameter Reference

Parameters can be set via `SetParameter(name, value)`:

| Name | Range | Description |
|------|-------|-------------|
| `volume` | 0-2 | Master volume |
| `pitchbend` | -1 to 1 | Pitch bend |
| `pitchbendrange` | 0-24 | Pitch bend range (semitones) |
| `tension` | 0-1 | Glottal tension |
| `openquotient` | 0.3-0.7 | Glottal open quotient |
| `breathiness` | 0-1 | Aspiration noise amount |
| `jitter` | 0-0.05 | Pitch variation |
| `shimmer` | 0-0.1 | Amplitude variation |
| `formantshift` | -12 to 12 | Formant shift (semitones) |
| `morphspeed` | 0.001-1 | Vowel morph speed |
| `vibratodepth` | 0-2 | Vibrato depth (semitones) |
| `vibratorate` | 0.1-15 | Vibrato rate (Hz) |
| `vowel` | 0-1 | Vowel selection (0-1 maps to all vowels) |
| `modwheel` | 0-1 | Maps to vibrato depth |
| `breath` | 0-1 | Maps to breathiness |
| `brightness` | 0-1 | Maps to formant shift |
| `resonance` | 0-1 | Maps to tension |

### MIDI CC Mappings

| CC | Parameter | Effect |
|----|-----------|--------|
| CC1 (Mod Wheel) | `modwheel` | Controls vibrato depth (0-0.5 semitones) |
| CC2 (Breath) | `breath` | Controls breathiness (0-1) |
| CC71 (Resonance) | `resonance` | Controls tension (0-1) |
| CC74 (Brightness) | `brightness` | Controls formant shift (-6 to +6 semitones) |
