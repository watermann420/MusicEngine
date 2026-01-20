// ============================================================================
// 01 - BASIC SETUP & ENGINE INITIALIZATION
// ============================================================================
// This script demonstrates basic engine setup and variable access
// SYNTAX TO REVIEW: Variable names, initialization patterns
// ============================================================================

// Available global variables (automatically provided):
// - Engine : AudioEngine instance
// - Sequencer : Sequencer instance

Print("=== BASIC SETUP TEST ===");
Print("");

// ============================================================================
// 1. ENGINE INFO
// ============================================================================
Print("1. Engine Information:");
Print($"   Engine initialized: {Engine != null}");
Print($"   Sequencer available: {Sequencer != null}");
Print("");

// ============================================================================
// 2. CURRENT SETTINGS
// ============================================================================
Print("2. Current Settings:");
Print($"   Sample Rate: {Settings.SampleRate} Hz");
Print($"   Channels: {Settings.Channels}");
Print($"   Bit Rate: {Settings.BitRate}");
Print("");

// ============================================================================
// 3. SEQUENCER STATUS
// ============================================================================
Print("3. Sequencer Status:");
Print($"   BPM: {Sequencer.Bpm}");
Print($"   Is Playing: {Sequencer.IsPlaying}");
Print($"   Current Beat: {Sequencer.CurrentBeat}");
Print("");

// ============================================================================
// 4. DEVICE ENUMERATION
// ============================================================================
Print("4. Available Devices:");
// Device info is shown during Engine.Initialize()
Print("   (See device list above)");
Print("");

Print("=== BASIC SETUP TEST COMPLETED ===");

// ============================================================================
// SYNTAX ELEMENTS TO CUSTOMIZE:
// ============================================================================
// - Engine (could be: audio, engine, sound, output)
// - Sequencer (could be: seq, sequence, timeline, clock)
// - Print (could be: log, console, write, output)
// - Settings (could be: config, cfg, setup)
// ============================================================================
