// ============================================================================
// 02 - SYNTH CREATION & WAVEFORMS
// ============================================================================
// This script demonstrates all synth creation methods and waveform types
// SYNTAX TO REVIEW: Function names, parameter names, waveform types
// ============================================================================

Print("=== SYNTH CREATION TEST ===");
Print("");

// ============================================================================
// 1. CREATE SIMPLE SYNTH (Default Sine Wave)
// ============================================================================
Print("1. Creating synths with different waveforms:");

var sine = CreateSynth("sine");
Print($"   Created: {sine.Name} (Sine wave - default)");

var square = CreateSynth("square", SynthWaveform.Square);
Print($"   Created: {square.Name} (Square wave)");

var saw = CreateSynth("saw", SynthWaveform.Sawtooth);
Print($"   Created: {saw.Name} (Sawtooth wave)");

var triangle = CreateSynth("triangle", SynthWaveform.Triangle);
Print($"   Created: {triangle.Name} (Triangle wave)");

var noise = CreateSynth("noise", SynthWaveform.Noise);
Print($"   Created: {noise.Name} (Noise)");
Print("");

// ============================================================================
// 2. SYNTH PARAMETERS
// ============================================================================
Print("2. Synth parameter control:");

// Volume control
sine.Volume.Value = 0.5;  // 50% volume
Print($"   Sine volume set to: {sine.Volume.Value}");

// Filter controls
sine.FilterCutoff.Value = 800.0;
Print($"   Sine filter cutoff: {sine.FilterCutoff.Value} Hz");

sine.FilterResonance.Value = 2.0;
Print($"   Sine filter resonance: {sine.FilterResonance.Value}");

// Pitch control
sine.Pitch.Value = 1.2;  // 20% higher pitch
Print($"   Sine pitch: {sine.Pitch.Value}");
Print("");

// ============================================================================
// 3. PLAY NOTES ON SYNTH
// ============================================================================
Print("3. Playing notes:");

// Note numbers (MIDI standard):
// C4 = 60 (middle C)
// C3 = 48
// C5 = 72

sine.NoteOn(60, 100);  // Note 60 (C4), velocity 100
Print("   Playing C4 on sine synth...");

await Task.Delay(500);

sine.NoteOff(60);
Print("   Note off");
Print("");

// ============================================================================
// 4. PLAY CHORD
// ============================================================================
Print("4. Playing chord (C major):");

sine.NoteOn(60, 80);  // C
sine.NoteOn(64, 80);  // E
sine.NoteOn(67, 80);  // G
Print("   C major chord playing...");

await Task.Delay(1000);

sine.AllNotesOff();
Print("   All notes off");
Print("");

Print("=== SYNTH CREATION TEST COMPLETED ===");

// ============================================================================
// SYNTAX ELEMENTS TO CUSTOMIZE:
// ============================================================================
// FUNCTION NAMES:
// - CreateSynth (could be: synth, newSynth, makeSynth, addSynth)
//
// WAVEFORM TYPES:
// - SynthWaveform.Sine (could be: sin, sine)
// - SynthWaveform.Square (could be: sqr, square, pulse)
// - SynthWaveform.Sawtooth (could be: saw, sawtooth)
// - SynthWaveform.Triangle (could be: tri, triangle)
// - SynthWaveform.Noise (could be: noise, white, random)
//
// SYNTH METHODS:
// - NoteOn (could be: play, noteOn, on, trigger)
// - NoteOff (could be: stop, noteOff, off, release)
// - AllNotesOff (could be: stopAll, silence, killAll, panic)
//
// SYNTH PARAMETERS:
// - Volume (could be: vol, volume, gain, level, amp)
// - FilterCutoff (could be: cutoff, filter, freq, filterFreq)
// - FilterResonance (could be: resonance, res, q, filterQ)
// - Pitch (could be: pitch, tune, detune, transpose)
// ============================================================================
