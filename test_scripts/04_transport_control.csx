// ============================================================================
// 04 - TRANSPORT CONTROL (Sequencer Control)
// ============================================================================
// This script demonstrates all transport/sequencer control functions
// SYNTAX TO REVIEW: Transport commands, tempo control, beat navigation
// ============================================================================

Print("=== TRANSPORT CONTROL TEST ===");
Print("");

// ============================================================================
// 1. SEQUENCER START/STOP
// ============================================================================
Print("1. Basic transport control:");

Start();
Print("   Start() - sequencer started");
Print($"   IsPlaying: {Sequencer.IsPlaying}");

await Task.Delay(1000);

Stop();
Print("   Stop() - sequencer stopped");
Print($"   IsPlaying: {Sequencer.IsPlaying}");
Print("");

// ============================================================================
// 2. TEMPO (BPM) CONTROL
// ============================================================================
Print("2. Tempo (BPM) control:");

Print($"   Current BPM: {Sequencer.Bpm}");

SetBpm(120);
Print($"   SetBpm(120) - set to 120 BPM");
Print($"   Current BPM: {Sequencer.Bpm}");

SetBpm(140);
Print($"   SetBpm(140) - set to 140 BPM");

SetBpm(90);
Print($"   SetBpm(90) - set to 90 BPM");
Print("");

// ============================================================================
// 3. BEAT POSITION CONTROL
// ============================================================================
Print("3. Beat position control:");

Print($"   Current beat: {Sequencer.CurrentBeat}");

Skip(4);
Print("   Skip(4) - jump forward 4 beats");
Print($"   Current beat: {Sequencer.CurrentBeat}");

Skip(-2);
Print("   Skip(-2) - jump backward 2 beats");
Print($"   Current beat: {Sequencer.CurrentBeat}");

Sequencer.CurrentBeat = 0;
Print("   Sequencer.CurrentBeat = 0 - reset to beginning");
Print($"   Current beat: {Sequencer.CurrentBeat}");
Print("");

// ============================================================================
// 4. PATTERN START/STOP (Individual)
// ============================================================================
Print("4. Individual pattern control:");

// Create test pattern
var testSynth = CreateSynth("test");
var testPattern = CreatePattern(testSynth, "test-pattern");
testPattern.AddNote(0, 60, 100, 0.5);
Sequencer.AddPattern(testPattern);

StartPattern(testPattern);
Print("   StartPattern(pattern) - start specific pattern");

await Task.Delay(500);

StopPattern(testPattern);
Print("   StopPattern(pattern) - stop specific pattern");
Print("");

// ============================================================================
// 5. PATTERN CONTROL (By Name)
// ============================================================================
Print("5. Pattern control by name:");

StartPattern("test-pattern");
Print("   StartPattern(\"test-pattern\") - start by name");

await Task.Delay(500);

StopPattern("test-pattern");
Print("   StopPattern(\"test-pattern\") - stop by name");
Print("");

// ============================================================================
// 6. SEQUENCER PROPERTIES
// ============================================================================
Print("6. Sequencer properties:");

Print($"   Sequencer.Bpm: {Sequencer.Bpm}");
Print($"   Sequencer.IsPlaying: {Sequencer.IsPlaying}");
Print($"   Sequencer.CurrentBeat: {Sequencer.CurrentBeat}");
Print($"   Sequencer.PatternCount: {Sequencer.PatternCount}");
Print($"   Sequencer.SampleRate: {Sequencer.SampleRate}");
Print("");

// ============================================================================
// 7. BEAT SUBDIVISION SETTINGS
// ============================================================================
Print("7. Beat subdivision (timing precision):");

Print($"   Current subdivision: {Sequencer.BeatSubdivision}");

// Available subdivisions:
// - Eighth (8 PPQN)
// - Sixteenth (16 PPQN)
// - ThirtySecond (32 PPQN)
// - SixtyFourth (64 PPQN)
// - Standard (96 PPQN)
// - High (192 PPQN)
// - VeryHigh (384 PPQN)
// - UltraHigh (480 PPQN)

Sequencer.BeatSubdivision = BeatSubdivision.Sixteenth;
Print("   Set to Sixteenth (16 PPQN)");
Print("");

// ============================================================================
// 8. TIMING PRECISION MODES
// ============================================================================
Print("8. Timing precision modes:");

Print($"   Current mode: {Sequencer.TimingMode}");

// Available modes:
// - Standard (default, good CPU usage)
// - HighPrecision (better timing, more CPU)
// - AudioRate (best timing, most CPU)

Sequencer.TimingMode = TimingPrecision.HighPrecision;
Print("   Set to HighPrecision mode");
Print("");

Print("=== TRANSPORT CONTROL TEST COMPLETED ===");

// ============================================================================
// SYNTAX ELEMENTS TO CUSTOMIZE:
// ============================================================================
// TRANSPORT FUNCTIONS:
// - Start (could be: start, play, run, go, begin)
// - Stop (could be: stop, pause, halt, end)
// - SetBpm (could be: bpm, tempo, setBpm, setTempo, speed)
// - Skip (could be: skip, jump, seek, move, goto)
//
// PATTERN CONTROL FUNCTIONS:
// - StartPattern (could be: startPattern, playPattern, runPattern)
// - StopPattern (could be: stopPattern, pausePattern, haltPattern)
//
// SEQUENCER PROPERTIES:
// - Bpm (could be: bpm, tempo, speed)
// - IsPlaying (could be: isPlaying, playing, running, active)
// - CurrentBeat (could be: currentBeat, beat, position, pos)
// - PatternCount (could be: patternCount, patterns, count)
// - SampleRate (could be: sampleRate, sr, rate)
// - BeatSubdivision (could be: subdivision, resolution, ppqn)
// - TimingMode (could be: timingMode, precision, mode)
//
// TIMING MODES:
// - TimingPrecision.Standard (could be: standard, normal, default)
// - TimingPrecision.HighPrecision (could be: high, precise, accurate)
// - TimingPrecision.AudioRate (could be: ultra, max, extreme)
//
// BEAT SUBDIVISION TYPES:
// - BeatSubdivision.Eighth (could be: eighth, 8)
// - BeatSubdivision.Sixteenth (could be: sixteenth, 16)
// - BeatSubdivision.Standard (could be: standard, normal, 96)
// ============================================================================
