// ============================================================================
// 12 - GENERAL MIDI INSTRUMENTS
// ============================================================================
// This script demonstrates the 128 General MIDI instruments available
// through Windows built-in synthesizer (Microsoft GS Wavetable Synth)
// SYNTAX TO REVIEW: GM instrument creation, MIDI control, parameter control
// ============================================================================

Print("");
Print("=== GENERAL MIDI INSTRUMENTS TEST ===");
Print("");

// ============================================================================
// 1. CREATING GM INSTRUMENTS
// ============================================================================
Print("1. Creating GM instruments:");

// Create a piano using General MIDI
var piano = CreateGeneralMidiInstrument(GeneralMidiProgram.AcousticGrandPiano);
Print($"   Created: {piano.Name}");
Print($"   Program: {piano.Program}");
Print($"   Channel: {piano.Channel}");
Print("");

// Short form using gm() alias
var guitar = gm(GeneralMidiProgram.AcousticGuitarSteel);
Print($"   Created guitar: {guitar.Name}");

// Using newGm() alias
var strings = newGm(GeneralMidiProgram.StringEnsemble1);
Print($"   Created strings: {strings.Name}");
Print("");

// ============================================================================
// 2. PIANO FAMILY (0-7)
// ============================================================================
Print("2. Piano family:");

var acousticPiano = gm(GeneralMidiProgram.AcousticGrandPiano);
var brightPiano = gm(GeneralMidiProgram.BrightAcousticPiano);
var electricPiano = gm(GeneralMidiProgram.ElectricPiano1);
var honkyTonk = gm(GeneralMidiProgram.HonkyTonkPiano);
var harpsichord = gm(GeneralMidiProgram.Harpsichord);

Print("   - Acoustic Grand Piano");
Print("   - Bright Acoustic Piano");
Print("   - Electric Piano 1");
Print("   - Honky-Tonk Piano");
Print("   - Harpsichord");
Print("");

// ============================================================================
// 3. CHROMATIC PERCUSSION (8-15)
// ============================================================================
Print("3. Chromatic percussion:");

var vibes = gm(GeneralMidiProgram.Vibraphone);
var marimba = gm(GeneralMidiProgram.Marimba);
var xylophone = gm(GeneralMidiProgram.Xylophone);
var bells = gm(GeneralMidiProgram.TubularBells);

Print("   - Vibraphone");
Print("   - Marimba");
Print("   - Xylophone");
Print("   - Tubular Bells");
Print("");

// ============================================================================
// 4. ORGAN FAMILY (16-23)
// ============================================================================
Print("4. Organ family:");

var drawbarOrgan = gm(GeneralMidiProgram.DrawbarOrgan);
var rockOrgan = gm(GeneralMidiProgram.RockOrgan);
var churchOrgan = gm(GeneralMidiProgram.ChurchOrgan);
var accordion = gm(GeneralMidiProgram.Accordion);

Print("   - Drawbar Organ");
Print("   - Rock Organ");
Print("   - Church Organ");
Print("   - Accordion");
Print("");

// ============================================================================
// 5. GUITAR FAMILY (24-31)
// ============================================================================
Print("5. Guitar family:");

var nylonGuitar = gm(GeneralMidiProgram.AcousticGuitarNylon);
var steelGuitar = gm(GeneralMidiProgram.AcousticGuitarSteel);
var jazzGuitar = gm(GeneralMidiProgram.ElectricGuitarJazz);
var cleanGuitar = gm(GeneralMidiProgram.ElectricGuitarClean);
var overdrive = gm(GeneralMidiProgram.OverdrivenGuitar);
var distGuitar = gm(GeneralMidiProgram.DistortionGuitar);

Print("   - Acoustic Guitar (Nylon)");
Print("   - Acoustic Guitar (Steel)");
Print("   - Electric Guitar (Jazz)");
Print("   - Electric Guitar (Clean)");
Print("   - Overdriven Guitar");
Print("   - Distortion Guitar");
Print("");

// ============================================================================
// 6. BASS FAMILY (32-39)
// ============================================================================
Print("6. Bass family:");

var acousticBass = gm(GeneralMidiProgram.AcousticBass);
var fingerBass = gm(GeneralMidiProgram.ElectricBassFinger);
var pickBass = gm(GeneralMidiProgram.ElectricBassPick);
var fretless = gm(GeneralMidiProgram.FretlessBass);
var slapBass = gm(GeneralMidiProgram.SlapBass1);
var synthBass = gm(GeneralMidiProgram.SynthBass1);

Print("   - Acoustic Bass");
Print("   - Electric Bass (Finger)");
Print("   - Electric Bass (Pick)");
Print("   - Fretless Bass");
Print("   - Slap Bass 1");
Print("   - Synth Bass 1");
Print("");

// ============================================================================
// 7. STRINGS FAMILY (40-47)
// ============================================================================
Print("7. Strings family:");

var violin = gm(GeneralMidiProgram.Violin);
var viola = gm(GeneralMidiProgram.Viola);
var cello = gm(GeneralMidiProgram.Cello);
var contrabass = gm(GeneralMidiProgram.Contrabass);
var tremolo = gm(GeneralMidiProgram.TremoloStrings);
var pizzicato = gm(GeneralMidiProgram.PizzicatoStrings);
var harp = gm(GeneralMidiProgram.OrchestralHarp);

Print("   - Violin");
Print("   - Viola");
Print("   - Cello");
Print("   - Contrabass");
Print("   - Tremolo Strings");
Print("   - Pizzicato Strings");
Print("   - Orchestral Harp");
Print("");

// ============================================================================
// 8. ENSEMBLE (48-55)
// ============================================================================
Print("8. Ensemble:");

var stringEns1 = gm(GeneralMidiProgram.StringEnsemble1);
var stringEns2 = gm(GeneralMidiProgram.StringEnsemble2);
var choir = gm(GeneralMidiProgram.ChoirAahs);
var voiceOohs = gm(GeneralMidiProgram.VoiceOohs);
var orchHit = gm(GeneralMidiProgram.OrchestraHit);

Print("   - String Ensemble 1");
Print("   - String Ensemble 2");
Print("   - Choir Aahs");
Print("   - Voice Oohs");
Print("   - Orchestra Hit");
Print("");

// ============================================================================
// 9. BRASS FAMILY (56-63)
// ============================================================================
Print("9. Brass family:");

var trumpet = gm(GeneralMidiProgram.Trumpet);
var trombone = gm(GeneralMidiProgram.Trombone);
var tuba = gm(GeneralMidiProgram.Tuba);
var mutedTrumpet = gm(GeneralMidiProgram.MutedTrumpet);
var frenchHorn = gm(GeneralMidiProgram.FrenchHorn);
var brassSection = gm(GeneralMidiProgram.BrassSection);

Print("   - Trumpet");
Print("   - Trombone");
Print("   - Tuba");
Print("   - Muted Trumpet");
Print("   - French Horn");
Print("   - Brass Section");
Print("");

// ============================================================================
// 10. REED FAMILY (64-71)
// ============================================================================
Print("10. Reed family:");

var sopranoSax = gm(GeneralMidiProgram.SopranoSax);
var altoSax = gm(GeneralMidiProgram.AltoSax);
var tenorSax = gm(GeneralMidiProgram.TenorSax);
var baritoneSax = gm(GeneralMidiProgram.BaritoneSax);
var oboe = gm(GeneralMidiProgram.Oboe);
var clarinet = gm(GeneralMidiProgram.Clarinet);

Print("   - Soprano Sax");
Print("   - Alto Sax");
Print("   - Tenor Sax");
Print("   - Baritone Sax");
Print("   - Oboe");
Print("   - Clarinet");
Print("");

// ============================================================================
// 11. PIPE FAMILY (72-79)
// ============================================================================
Print("11. Pipe family:");

var piccolo = gm(GeneralMidiProgram.Piccolo);
var flute = gm(GeneralMidiProgram.Flute);
var recorder = gm(GeneralMidiProgram.Recorder);
var panFlute = gm(GeneralMidiProgram.PanFlute);
var shakuhachi = gm(GeneralMidiProgram.Shakuhachi);
var whistle = gm(GeneralMidiProgram.Whistle);
var ocarina = gm(GeneralMidiProgram.Ocarina);

Print("   - Piccolo");
Print("   - Flute");
Print("   - Recorder");
Print("   - Pan Flute");
Print("   - Shakuhachi");
Print("   - Whistle");
Print("   - Ocarina");
Print("");

// ============================================================================
// 12. SYNTH LEAD (80-87)
// ============================================================================
Print("12. Synth lead:");

var square = gm(GeneralMidiProgram.Lead1Square);
var sawtooth = gm(GeneralMidiProgram.Lead2Sawtooth);
var calliope = gm(GeneralMidiProgram.Lead3Calliope);
var chiff = gm(GeneralMidiProgram.Lead4Chiff);

Print("   - Lead 1 (Square)");
Print("   - Lead 2 (Sawtooth)");
Print("   - Lead 3 (Calliope)");
Print("   - Lead 4 (Chiff)");
Print("");

// ============================================================================
// 13. SYNTH PAD (88-95)
// ============================================================================
Print("13. Synth pad:");

var padNewAge = gm(GeneralMidiProgram.Pad1NewAge);
var padWarm = gm(GeneralMidiProgram.Pad2Warm);
var padPoly = gm(GeneralMidiProgram.Pad3Polysynth);
var padChoir = gm(GeneralMidiProgram.Pad4Choir);

Print("   - Pad 1 (New Age)");
Print("   - Pad 2 (Warm)");
Print("   - Pad 3 (Polysynth)");
Print("   - Pad 4 (Choir)");
Print("");

// ============================================================================
// 14. SYNTH EFFECTS (96-103)
// ============================================================================
Print("14. Synth effects:");

var rain = gm(GeneralMidiProgram.FX1Rain);
var soundtrack = gm(GeneralMidiProgram.FX2Soundtrack);
var crystal = gm(GeneralMidiProgram.FX3Crystal);
var atmosphere = gm(GeneralMidiProgram.FX4Atmosphere);

Print("   - FX 1 (Rain)");
Print("   - FX 2 (Soundtrack)");
Print("   - FX 3 (Crystal)");
Print("   - FX 4 (Atmosphere)");
Print("");

// ============================================================================
// 15. ETHNIC INSTRUMENTS (104-111)
// ============================================================================
Print("15. Ethnic instruments:");

var sitar = gm(GeneralMidiProgram.Sitar);
var banjo = gm(GeneralMidiProgram.Banjo);
var shamisen = gm(GeneralMidiProgram.Shamisen);
var koto = gm(GeneralMidiProgram.Koto);
var kalimba = gm(GeneralMidiProgram.Kalimba);
var bagpipe = gm(GeneralMidiProgram.BagPipe);

Print("   - Sitar");
Print("   - Banjo");
Print("   - Shamisen");
Print("   - Koto");
Print("   - Kalimba");
Print("   - Bagpipe");
Print("");

// ============================================================================
// 16. PERCUSSIVE (112-119)
// ============================================================================
Print("16. Percussive:");

var tinkleBell = gm(GeneralMidiProgram.TinkleBell);
var steelDrums = gm(GeneralMidiProgram.SteelDrums);
var woodblock = gm(GeneralMidiProgram.Woodblock);
var taiko = gm(GeneralMidiProgram.TaikoDrum);

Print("   - Tinkle Bell");
Print("   - Steel Drums");
Print("   - Woodblock");
Print("   - Taiko Drum");
Print("");

// ============================================================================
// 17. SOUND EFFECTS (120-127)
// ============================================================================
Print("17. Sound effects:");

var fretNoise = gm(GeneralMidiProgram.GuitarFretNoise);
var seashore = gm(GeneralMidiProgram.Seashore);
var birdTweet = gm(GeneralMidiProgram.BirdTweet);
var helicopter = gm(GeneralMidiProgram.Helicopter);
var applause = gm(GeneralMidiProgram.Applause);

Print("   - Guitar Fret Noise");
Print("   - Seashore");
Print("   - Bird Tweet");
Print("   - Helicopter");
Print("   - Applause");
Print("");

// ============================================================================
// 18. USING GM INSTRUMENTS WITH PATTERNS
// ============================================================================
Print("18. Using GM instruments with patterns:");

var gmPiano = gm(GeneralMidiProgram.AcousticGrandPiano);
var pianoPattern = CreatePattern(gmPiano, "piano-pattern");
pianoPattern.AddNote(0.0, 60, 100, 0.5);  // C4
pianoPattern.AddNote(0.5, 64, 100, 0.5);  // E4
pianoPattern.AddNote(1.0, 67, 100, 0.5);  // G4
pianoPattern.AddNote(1.5, 72, 100, 1.0);  // C5

Print("   Created piano pattern with chord progression");
Print("");

// ============================================================================
// 19. VOLUME CONTROL
// ============================================================================
Print("19. Volume control:");

var gmOrgan = gm(GeneralMidiProgram.DrawbarOrgan);
gmOrgan.Volume = 0.75f;  // 75% volume

Print($"   Set organ volume to {gmOrgan.Volume * 100}%");
Print("");

// ============================================================================
// 20. PARAMETER CONTROL
// ============================================================================
Print("20. Parameter control:");

var gmString = gm(GeneralMidiProgram.StringEnsemble1);

// Pan control (-1.0 = left, 0.0 = center, 1.0 = right)
gmString.SetParameter("pan", -0.5f);
Print("   Set string pan to -0.5 (left)");

// Reverb control
gmString.SetParameter("reverb", 0.6f);
Print("   Set reverb to 0.6");

// Chorus control
gmString.SetParameter("chorus", 0.4f);
Print("   Set chorus to 0.4");

// Expression control
gmString.SetParameter("expression", 0.8f);
Print("   Set expression to 0.8");
Print("");

// ============================================================================
// 21. PITCH BEND
// ============================================================================
Print("21. Pitch bend:");

var gmLead = gm(GeneralMidiProgram.Lead2Sawtooth);
gmLead.NoteOn(60, 100);
Print("   Playing note 60");

// Bend up by 1 semitone
gmLead.PitchBend(0.5f);
Print("   Pitch bend: +0.5 (up 1 semitone)");

// Reset pitch bend
gmLead.PitchBend(0.0f);
Print("   Pitch bend: 0.0 (center)");

gmLead.NoteOff(60);
Print("");

// ============================================================================
// 22. MIDI CONTROL CHANGES
// ============================================================================
Print("22. MIDI control changes:");

var gmBass = gm(GeneralMidiProgram.SynthBass1);

// Send MIDI CC 1 (Modulation)
gmBass.SendControlChange(1, 64);
Print("   Sent CC 1 (Modulation) = 64");

// Send MIDI CC 64 (Sustain Pedal)
gmBass.SendControlChange(64, 127);
Print("   Sent CC 64 (Sustain) = 127 (on)");
Print("");

// ============================================================================
// 23. MULTI-CHANNEL SETUP
// ============================================================================
Print("23. Multi-channel setup:");

// Use different MIDI channels for layering
var pianoLayer1 = gm(GeneralMidiProgram.AcousticGrandPiano, 0);
var pianoLayer2 = gm(GeneralMidiProgram.ElectricPiano1, 1);
var bassLayer = gm(GeneralMidiProgram.FretlessBass, 2);

Print($"   Piano layer 1 on channel {pianoLayer1.Channel}");
Print($"   Piano layer 2 on channel {pianoLayer2.Channel}");
Print($"   Bass layer on channel {bassLayer.Channel}");
Print("");

// ============================================================================
// 24. PRACTICAL EXAMPLE - FULL BAND
// ============================================================================
Print("24. Practical example - full band:");

var drums = gm(GeneralMidiProgram.SynthDrum, 9);  // Channel 10 is drums
var bassPart = gm(GeneralMidiProgram.ElectricBassPick);
var rhythm = gm(GeneralMidiProgram.ElectricGuitarClean);
var leadPart = gm(GeneralMidiProgram.Lead2Sawtooth);
var padPart = gm(GeneralMidiProgram.Pad2Warm);

Print("   Created full band:");
Print("   - Synth Drum (Channel 9)");
Print("   - Electric Bass (Pick)");
Print("   - Electric Guitar (Clean)");
Print("   - Lead 2 (Sawtooth)");
Print("   - Pad 2 (Warm)");
Print("");

// ============================================================================
// 25. COMBINING WITH EFFECTS
// ============================================================================
Print("25. Combining GM instruments with effects:");

var gmElectricPiano = gm(GeneralMidiProgram.ElectricPiano1);

// Add reverb effect
var reverbedPiano = fx.Reverb(gmElectricPiano, "piano-reverb")
    .RoomSize(0.7)
    .DryWet(0.3)
    .Build();

Print("   Added reverb to electric piano");

// Add chorus effect
var chorusedGuitar = fx.Chorus(
    gm(GeneralMidiProgram.ElectricGuitarClean),
    "guitar-chorus")
    .Rate(0.8)
    .Voices(3)
    .DryWet(0.5)
    .Build();

Print("   Added chorus to clean guitar");
Print("");

// ============================================================================
// 26. ALL 128 GM INSTRUMENTS REFERENCE
// ============================================================================
Print("26. Complete GM instrument list:");
Print("");
Print("   PIANO (0-7):");
Print("   0: AcousticGrandPiano, 1: BrightAcousticPiano, 2: ElectricGrandPiano");
Print("   3: HonkyTonkPiano, 4: ElectricPiano1, 5: ElectricPiano2");
Print("   6: Harpsichord, 7: Clavinet");
Print("");
Print("   CHROMATIC PERCUSSION (8-15):");
Print("   8: Celesta, 9: Glockenspiel, 10: MusicBox, 11: Vibraphone");
Print("   12: Marimba, 13: Xylophone, 14: TubularBells, 15: Dulcimer");
Print("");
Print("   ORGAN (16-23):");
Print("   16: DrawbarOrgan, 17: PercussiveOrgan, 18: RockOrgan, 19: ChurchOrgan");
Print("   20: ReedOrgan, 21: Accordion, 22: Harmonica, 23: TangoAccordion");
Print("");
Print("   GUITAR (24-31):");
Print("   24: AcousticGuitarNylon, 25: AcousticGuitarSteel, 26: ElectricGuitarJazz");
Print("   27: ElectricGuitarClean, 28: ElectricGuitarMuted, 29: OverdrivenGuitar");
Print("   30: DistortionGuitar, 31: GuitarHarmonics");
Print("");
Print("   BASS (32-39):");
Print("   32: AcousticBass, 33: ElectricBassFinger, 34: ElectricBassPick");
Print("   35: FretlessBass, 36: SlapBass1, 37: SlapBass2");
Print("   38: SynthBass1, 39: SynthBass2");
Print("");
Print("   STRINGS (40-47):");
Print("   40: Violin, 41: Viola, 42: Cello, 43: Contrabass");
Print("   44: TremoloStrings, 45: PizzicatoStrings, 46: OrchestralHarp, 47: Timpani");
Print("");
Print("   ENSEMBLE (48-55):");
Print("   48: StringEnsemble1, 49: StringEnsemble2, 50: SynthStrings1, 51: SynthStrings2");
Print("   52: ChoirAahs, 53: VoiceOohs, 54: SynthVoice, 55: OrchestraHit");
Print("");
Print("   BRASS (56-63):");
Print("   56: Trumpet, 57: Trombone, 58: Tuba, 59: MutedTrumpet");
Print("   60: FrenchHorn, 61: BrassSection, 62: SynthBrass1, 63: SynthBrass2");
Print("");
Print("   REED (64-71):");
Print("   64: SopranoSax, 65: AltoSax, 66: TenorSax, 67: BaritoneSax");
Print("   68: Oboe, 69: EnglishHorn, 70: Bassoon, 71: Clarinet");
Print("");
Print("   PIPE (72-79):");
Print("   72: Piccolo, 73: Flute, 74: Recorder, 75: PanFlute");
Print("   76: BlownBottle, 77: Shakuhachi, 78: Whistle, 79: Ocarina");
Print("");
Print("   SYNTH LEAD (80-87):");
Print("   80: Lead1Square, 81: Lead2Sawtooth, 82: Lead3Calliope, 83: Lead4Chiff");
Print("   84: Lead5Charang, 85: Lead6Voice, 86: Lead7Fifths, 87: Lead8BassLead");
Print("");
Print("   SYNTH PAD (88-95):");
Print("   88: Pad1NewAge, 89: Pad2Warm, 90: Pad3Polysynth, 91: Pad4Choir");
Print("   92: Pad5Bowed, 93: Pad6Metallic, 94: Pad7Halo, 95: Pad8Sweep");
Print("");
Print("   SYNTH EFFECTS (96-103):");
Print("   96: FX1Rain, 97: FX2Soundtrack, 98: FX3Crystal, 99: FX4Atmosphere");
Print("   100: FX5Brightness, 101: FX6Goblins, 102: FX7Echoes, 103: FX8SciFi");
Print("");
Print("   ETHNIC (104-111):");
Print("   104: Sitar, 105: Banjo, 106: Shamisen, 107: Koto");
Print("   108: Kalimba, 109: BagPipe, 110: Fiddle, 111: Shanai");
Print("");
Print("   PERCUSSIVE (112-119):");
Print("   112: TinkleBell, 113: Agogo, 114: SteelDrums, 115: Woodblock");
Print("   116: TaikoDrum, 117: MelodicTom, 118: SynthDrum, 119: ReverseCymbal");
Print("");
Print("   SOUND EFFECTS (120-127):");
Print("   120: GuitarFretNoise, 121: BreathNoise, 122: Seashore, 123: BirdTweet");
Print("   124: TelephoneRing, 125: Helicopter, 126: Applause, 127: Gunshot");
Print("");

Print("=== GENERAL MIDI INSTRUMENTS TEST COMPLETED ===");

// ============================================================================
// NOTES:
// ============================================================================
// - Windows built-in synthesizer (Microsoft GS Wavetable Synth) is used
// - All 128 General MIDI instruments are available
// - Supports full MIDI control: volume, pan, expression, reverb, chorus, etc.
// - Can be combined with MusicEngine effects (reverb, delay, chorus, etc.)
// - Use different MIDI channels (0-15) for layering instruments
// - Channel 9 (10th channel) is reserved for drums in GM standard
// - GM instruments can be used with patterns just like SimpleSynth
// ============================================================================
