// ============================================================================
// 10 - HELPER FUNCTIONS & UTILITIES
// ============================================================================
// This script demonstrates all helper/utility functions
// SYNTAX TO REVIEW: Print, random functions, utility helpers
// ============================================================================

Print("=== HELPER FUNCTIONS TEST ===");
Print("");

// ============================================================================
// 1. PRINT FUNCTION
// ============================================================================
Print("1. Print function:");

Print("   Simple message");
Print($"   Formatted message: {1 + 1}");
Print($"   Current time: {DateTime.Now}");
Print("");

// ============================================================================
// 2. RANDOM FLOAT (0.0 - 1.0)
// ============================================================================
Print("2. Random float (0.0 - 1.0):");

var rand1 = Random();
var rand2 = Random();
var rand3 = Random();

Print($"   Random() → {rand1:F4}");
Print($"   Random() → {rand2:F4}");
Print($"   Random() → {rand3:F4}");
Print("");

// ============================================================================
// 3. RANDOM FLOAT (Min - Max Range)
// ============================================================================
Print("3. Random float (with range):");

var rand4 = Random(0.5, 1.0);
var rand5 = Random(100, 500);
var rand6 = Random(-1.0, 1.0);

Print($"   Random(0.5, 1.0) → {rand4:F4}");
Print($"   Random(100, 500) → {rand5:F2}");
Print($"   Random(-1.0, 1.0) → {rand6:F4}");
Print("");

// ============================================================================
// 4. RANDOM INTEGER
// ============================================================================
Print("4. Random integer:");

var int1 = RandomInt(1, 10);
var int2 = RandomInt(0, 127);  // MIDI range
var int3 = RandomInt(60, 72);  // Note range C4-C5

Print($"   RandomInt(1, 10) → {int1}");
Print($"   RandomInt(0, 127) → {int2}");
Print($"   RandomInt(60, 72) → {int3}");
Print("");

// ============================================================================
// 5. PRACTICAL EXAMPLE - RANDOM NOTES
// ============================================================================
Print("5. Practical example - Generate random notes:");

var synth = CreateSynth("random-synth");
var pattern = CreatePattern(synth, "random-pattern");

for (var i = 0; i < 16; i++)
{
    var beat = i * 0.25;              // Every quarter beat
    var note = RandomInt(48, 72);     // Random note C3-C5
    var velocity = RandomInt(60, 100); // Random velocity
    var duration = Random(0.1, 0.5);   // Random duration

    pattern.AddNote(beat, note, velocity, duration);
}

Print($"   Created pattern with {pattern.NoteCount} random notes");
Print("");

// ============================================================================
// 6. PRACTICAL EXAMPLE - RANDOM RHYTHMS
// ============================================================================
Print("6. Practical example - Random rhythm generator:");

var drums = CreateSampleInstrument("drums");
var drumPattern = CreatePattern(drums, "random-drums");

for (var beat = 0.0; beat < 4.0; beat += 0.25)
{
    // Random chance to place a note
    if (Random() > 0.5)
    {
        var drum = RandomInt(36, 42);  // Random drum (kick, snare, hihat)
        var vel = RandomInt(70, 110);
        drumPattern.AddNote(beat, drum, vel, 0.1);
    }
}

Print($"   Created random drum pattern with {drumPattern.NoteCount} hits");
Print("");

// ============================================================================
// 7. PRACTICAL EXAMPLE - RANDOM SYNTH PARAMETERS
// ============================================================================
Print("7. Practical example - Randomize synth parameters:");

var testSynth = CreateSynth("evolving-synth");

// Randomize filter cutoff
testSynth.FilterCutoff.Value = Random(200, 2000);
Print($"   Filter Cutoff: {testSynth.FilterCutoff.Value:F0} Hz");

// Randomize resonance
testSynth.FilterResonance.Value = Random(0.1, 5.0);
Print($"   Filter Resonance: {testSynth.FilterResonance.Value:F2}");

// Randomize pitch
testSynth.Pitch.Value = Random(0.8, 1.2);
Print($"   Pitch: {testSynth.Pitch.Value:F2}");

// Randomize volume
testSynth.Volume.Value = Random(0.3, 0.9);
Print($"   Volume: {testSynth.Volume.Value:F2}");
Print("");

// ============================================================================
// 8. PRACTICAL EXAMPLE - RANDOM BPM
// ============================================================================
Print("8. Practical example - Random tempo changes:");

var randomBpm = RandomInt(80, 160);
SetBpm(randomBpm);
Print($"   Random BPM: {randomBpm}");
Print("");

// ============================================================================
// 9. PRACTICAL EXAMPLE - PROBABILITY-BASED PATTERNS
// ============================================================================
Print("9. Practical example - Probability-based note placement:");

var probSynth = CreateSynth("prob-synth");
var probPattern = CreatePattern(probSynth, "prob-pattern");

for (var i = 0; i < 16; i++)
{
    var beat = i * 0.25;

    // 70% chance for a note
    if (Random() < 0.7)
    {
        var note = 60 + (i % 12);  // Scale pattern
        var velocity = RandomInt(80, 120);
        probPattern.AddNote(beat, note, velocity, 0.2);
    }
}

Print($"   Created probability-based pattern with {probPattern.NoteCount} notes");
Print("");

// ============================================================================
// 10. PRACTICAL EXAMPLE - RANDOM SCALE GENERATOR
// ============================================================================
Print("10. Practical example - Random melody in scale:");

// C Major scale notes
var cMajorScale = new[] { 60, 62, 64, 65, 67, 69, 71, 72 };

var melodySynth = CreateSynth("melody");
var melodyPattern = CreatePattern(melodySynth, "melody-pattern");

for (var i = 0; i < 8; i++)
{
    var beat = i * 0.5;
    var scaleIndex = RandomInt(0, cMajorScale.Length);
    var note = cMajorScale[scaleIndex];
    var velocity = RandomInt(80, 100);

    melodyPattern.AddNote(beat, note, velocity, 0.4);
}

Print($"   Created melody in C Major scale with {melodyPattern.NoteCount} notes");
Print("");

// ============================================================================
// 11. UTILITY - NOTE NUMBER TO NAME
// ============================================================================
Print("11. Note number to name conversion:");

Print("   // Helper function examples:");
Print("   // Note 60 = C4 (Middle C)");
Print("   // Note 69 = A4 (440 Hz)");
Print("   // Note 48 = C3");
Print("   // Note 72 = C5");
Print("");

// ============================================================================
// 12. USEFUL CONSTANTS
// ============================================================================
Print("12. Useful constants:");

Print("   MIDI Note Numbers:");
Print("   - Middle C (C4) = 60");
Print("   - A4 (440 Hz) = 69");
Print("   - Octave range = 12 notes");
Print("");

Print("   MIDI Velocity:");
Print("   - Range: 0-127");
Print("   - 0 = Note Off");
Print("   - 127 = Maximum velocity");
Print("");

Print("   Standard Drum Notes (General MIDI):");
Print("   - Kick (Bass Drum) = 36 (C2)");
Print("   - Snare = 38 (D2)");
Print("   - Closed HiHat = 42 (F#2)");
Print("   - Open HiHat = 46 (A#2)");
Print("   - Crash Cymbal = 49 (C#3)");
Print("");

Print("=== HELPER FUNCTIONS TEST COMPLETED ===");

// ============================================================================
// SYNTAX ELEMENTS TO CUSTOMIZE:
// ============================================================================
// PRINT FUNCTION:
// - Print (could be: print, log, console, write, output, echo)
//
// RANDOM FUNCTIONS:
// - Random() (could be: random, rand, rnd, rng, randomFloat)
// - Random(min, max) (could be: random, rand, between, range)
// - RandomInt(min, max) (could be: randomInt, randInt, int, randomInteger)
//
// ALTERNATIVE NAMING IDEAS:
// - Print → log, console, out, write, say, echo
// - Random → rand, rnd, rng, chance
// - RandomInt → randInt, int, randomInteger, dice, roll
//
// PARAMETER NAMES:
// - min (could be: min, low, from, start)
// - max (could be: max, high, to, end)
// - message (could be: message, text, msg, str, output)
//
// POTENTIAL ADDITIONAL HELPERS:
// - Sleep(ms) → wait, delay, pause
// - NoteToName(note) → noteName, n2n, toName
// - NameToNote(name) → noteNumber, name2n, toNote
// - BpmToMs(bpm) → tempo2ms, beatTime
// - FreqToNote(freq) → pitch2note, hz2midi
// ============================================================================
