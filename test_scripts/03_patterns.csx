// ============================================================================
// 03 - PATTERN CREATION & SEQUENCING
// ============================================================================
// This script demonstrates pattern creation and sequencing
// SYNTAX TO REVIEW: Pattern functions, note events, sequencer control
// ============================================================================

Print("=== PATTERN CREATION TEST ===");
Print("");

// ============================================================================
// 1. CREATE SYNTH FOR PATTERNS
// ============================================================================
var bass = CreateSynth("bass", SynthWaveform.Sawtooth);
bass.Volume.Value = 0.6;
bass.FilterCutoff.Value = 400;
Print("1. Created bass synth");
Print("");

// ============================================================================
// 2. CREATE SIMPLE PATTERN
// ============================================================================
Print("2. Creating simple pattern:");

var pattern1 = CreatePattern(bass, "simple-bass");
pattern1.Loop = true;

// Add notes: AddNote(beat, note, velocity, duration)
pattern1.AddNote(0.0, 36, 100, 0.5);   // C2 on beat 0
pattern1.AddNote(1.0, 36, 100, 0.5);   // C2 on beat 1
pattern1.AddNote(2.0, 38, 100, 0.5);   // D2 on beat 2
pattern1.AddNote(3.0, 36, 100, 0.5);   // C2 on beat 3

Print($"   Pattern '{pattern1.Name}' created with {pattern1.NoteCount} notes");
Print("");

// ============================================================================
// 3. CREATE COMPLEX PATTERN
// ============================================================================
Print("3. Creating complex pattern:");

var lead = CreateSynth("lead", SynthWaveform.Square);
lead.Volume.Value = 0.4;

var pattern2 = CreatePattern(lead, "melody");
pattern2.Loop = true;

// Melody pattern
pattern2.AddNote(0.0, 60, 90, 0.25);   // C4
pattern2.AddNote(0.5, 62, 80, 0.25);   // D4
pattern2.AddNote(1.0, 64, 85, 0.25);   // E4
pattern2.AddNote(1.5, 65, 75, 0.25);   // F4
pattern2.AddNote(2.0, 67, 90, 0.5);    // G4
pattern2.AddNote(3.0, 65, 80, 0.5);    // F4
pattern2.AddNote(4.0, 64, 85, 1.0);    // E4

Print($"   Pattern '{pattern2.Name}' created with {pattern2.NoteCount} notes");
Print("");

// ============================================================================
// 4. PATTERN MANAGEMENT
// ============================================================================
Print("4. Pattern management:");

// Add patterns to sequencer
Sequencer.AddPattern(pattern1);
Sequencer.AddPattern(pattern2);
Print($"   Added patterns to sequencer");
Print($"   Total patterns: {Sequencer.PatternCount}");
Print("");

// ============================================================================
// 5. PATTERN CONTROL (Individual)
// ============================================================================
Print("5. Pattern control methods:");

// Start individual pattern
pattern1.Start();
Print("   pattern1.Start() - starts pattern");

// Stop individual pattern
pattern1.Stop();
Print("   pattern1.Stop() - stops pattern");

// Toggle pattern
pattern1.Toggle();
Print("   pattern1.Toggle() - toggles pattern on/off");
Print("");

// ============================================================================
// 6. PATTERN PROPERTIES
// ============================================================================
Print("6. Pattern properties:");

Print($"   pattern1.Loop: {pattern1.Loop}");
Print($"   pattern1.IsPlaying: {pattern1.IsPlaying}");
Print($"   pattern1.NoteCount: {pattern1.NoteCount}");
Print($"   pattern1.Name: {pattern1.Name}");
Print("");

// ============================================================================
// 7. REMOVE NOTES FROM PATTERN
// ============================================================================
Print("7. Note removal:");

pattern1.RemoveNote(0.0, 36);  // Remove note at beat 0, note 36
Print("   Removed note from pattern1");
Print($"   New note count: {pattern1.NoteCount}");
Print("");

// ============================================================================
// 8. CLEAR PATTERN
// ============================================================================
Print("8. Clear pattern:");

pattern1.Clear();
Print("   Cleared all notes from pattern1");
Print($"   Note count after clear: {pattern1.NoteCount}");
Print("");

Print("=== PATTERN CREATION TEST COMPLETED ===");

// ============================================================================
// SYNTAX ELEMENTS TO CUSTOMIZE:
// ============================================================================
// PATTERN FUNCTIONS:
// - CreatePattern (could be: pattern, newPattern, makePattern, addPattern)
// - AddNote (could be: note, addNote, add, n)
// - RemoveNote (could be: removeNote, remove, delete, del)
// - Clear (could be: clear, reset, empty, removeAll)
//
// PATTERN METHODS:
// - Start (could be: start, play, run, begin)
// - Stop (could be: stop, pause, halt, end)
// - Toggle (could be: toggle, switch, flip)
//
// PATTERN PROPERTIES:
// - Loop (could be: loop, repeat, cycle)
// - IsPlaying (could be: isPlaying, playing, active, running)
// - NoteCount (could be: noteCount, count, length, size)
// - Name (could be: name, id, label)
//
// SEQUENCER METHODS:
// - AddPattern (could be: add, addPattern, register)
// - PatternCount (could be: patternCount, count, size)
// ============================================================================
