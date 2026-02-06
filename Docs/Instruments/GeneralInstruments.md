# General Instruments (General MIDI)

`GeneralMidiInstrument` is a light wrapper around system MIDI output. It controls a General MIDI-capable device or softsynth via Program Change and controllers.

## Behavior

- Uses the first available MIDI-Out device (device 0).
- If no MIDI-Out is available, the instrument stays silent.
- Parameters can be set directly or via `SetParameter`.
- Use `Program` to switch instruments; `Name` is just a label for your setup.

## Syntax Example (Script)

```csharp
var piano = CreateGeneralMidi();
piano.Program = GeneralMidiProgram.AcousticGrandPiano;
piano.Name = "GM_AcousticGrandPiano";
piano.Volume = 0.9f;
piano.Pan = 0f;
piano.Reverb = 0.2f;
piano.Channel = 0;

midi.device(0).to(piano);
midi.device(0).pitchbend().to(value => piano.PitchBend(value * 2f - 1f));

var pattern = CreatePattern(piano);
pattern.LoopLength = 4.0;
pattern.Note(60, 0.0, 1.0, 100);
pattern.Note(67, 1.0, 1.0, 100);
pattern.Note(72, 2.0, 2.0, 100);
pattern.Play();
```

## General MIDI Program List

| Category | Enum Names (Copy/Paste) |
| --- | --- |
| Pianos | AcousticGrandPiano; BrightAcousticPiano; ElectricGrandPiano; HonkyTonkPiano; ElectricPiano1; ElectricPiano2; Harpsichord; Clavinet |
| Chromatic Percussion | Celesta; Glockenspiel; MusicBox; Vibraphone; Marimba; Xylophone; TubularBells; Dulcimer |
| Organs | DrawbarOrgan; PercussiveOrgan; RockOrgan; ChurchOrgan; ReedOrgan; Accordion; Harmonica; TangoAccordion |
| Guitars | AcousticGuitarNylon; AcousticGuitarSteel; ElectricGuitarJazz; ElectricGuitarClean; ElectricGuitarMuted; OverdrivenGuitar; DistortionGuitar; GuitarHarmonics |
| Basses | AcousticBass; ElectricBassFinger; ElectricBassPick; FretlessBass; SlapBass1; SlapBass2; SynthBass1; SynthBass2 |
| Strings | Violin; Viola; Cello; Contrabass; TremoloStrings; PizzicatoStrings; OrchestralHarp; Timpani |
| Ensemble | StringEnsemble1; StringEnsemble2; SynthStrings1; SynthStrings2; ChoirAahs; VoiceOohs; SynthChoir; OrchestraHit |
| Brass | Trumpet; Trombone; Tuba; MutedTrumpet; FrenchHorn; BrassSection; SynthBrass1; SynthBrass2 |
| Reeds | SopranoSax; AltoSax; TenorSax; BaritoneSax; Oboe; EnglishHorn; Bassoon; Clarinet |
| Pipes | Piccolo; Flute; Recorder; PanFlute; BlownBottle; Shakuhachi; Whistle; Ocarina |
| Synth Leads | Lead1Square; Lead2Sawtooth; Lead3Calliope; Lead4Chiff; Lead5Charang; Lead6Voice; Lead7Fifths; Lead8BassLead |
| Synth Pads | Pad1NewAge; Pad2Warm; Pad3Polysynth; Pad4Choir; Pad5Bowed; Pad6Metallic; Pad7Halo; Pad8Sweep |
| Synth Effects | FX1Rain; FX2Soundtrack; FX3Crystal; FX4Atmosphere; FX5Brightness; FX6Goblins; FX7Echoes; FX8SciFi |
| Ethnic | Sitar; Banjo; Shamisen; Koto; Kalimba; Bagpipe; Fiddle; Shanai |
| Percussive | TinkleBell; Agogo; SteelDrums; Woodblock; TaikoDrum; MelodicTom; SynthDrum; ReverseCymbal |
| Sound Effects | GuitarFretNoise; BreathNoise; Seashore; BirdTweet; TelephoneRing; Helicopter; Applause; Gunshot |

## SetParameter Shortcuts

```csharp
gm.SetParameter("volume", 0.8f);
gm.SetParameter("pan", -0.2f);
gm.SetParameter("reverb", 0.25f);
gm.SetParameter("chorus", 0.1f);
gm.SetParameter("modulation", 0.5f);
gm.SetParameter("pitchbend", 0.1f);
```

Copyright 2026 watermann429 and contributers.
