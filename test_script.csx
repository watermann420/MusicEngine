// Simple MIDI-controlled Synthesizer
// Play your MIDI keyboard and hear the synth!

Print("=== Simple MIDI Synth ===");
Print("Connecting MIDI device 0 to synthesizer...");

// Set tempo (optional, not needed for live playing)
SetBpm(120);

// Create a synth with sawtooth wave
var synth = CreateSynth();
synth.Waveform = WaveType.Sawtooth;

// Configure the synth parameters
synth.SetParameter("cutoff", 0.7f);    // Filter cutoff (0.0 - 1.0)
synth.SetParameter("resonance", 0.2f); // Filter resonance (0.0 - 1.0)

// Route MIDI device 0 to the synth
midi.device(0).route(synth);

// Map MIDI CC 1 (Modulation Wheel) to filter cutoff
midi.device(0).cc(1).to(synth, "cutoff");

// Map MIDI CC 74 (typically brightness/cutoff on many controllers) to cutoff
midi.device(0).cc(74).to(synth, "cutoff");

// Optional: Map pitch bend to cutoff for expressive control
// Uncomment the line below if you want pitch bend to affect the filter
// midi.device(0).pitchbend().to(synth, "cutoff");

Print("✓ Synth ready!");
Print("Play your MIDI keyboard (device 0)");
Print("");
Print("Controls:");
Print("  - Play keys to hear notes");
Print("  - Mod wheel (CC1) controls filter cutoff");
Print("  - CC74 also controls cutoff");
Print("");
Print("To change waveform, modify synth.Waveform:");
Print("  WaveType.Sine, Square, Sawtooth, Triangle, Noise");