// ============================================================================
// 07 - AUDIO CONTROL (Mixing, Volume, Input)
// ============================================================================
// This script demonstrates audio mixing and volume control
// SYNTAX TO REVIEW: Audio functions, volume control, input routing
// ============================================================================

Print("=== AUDIO CONTROL TEST ===");
Print("");

// ============================================================================
// 1. MASTER VOLUME CONTROL
// ============================================================================
Print("1. Master volume control:");

// Get current master volume
var currentVolume = Audio.MasterVolume();
Print($"   Current master volume: {currentVolume}");

// Set master volume
Audio.MasterVolume(0.8);
Print("   Audio.MasterVolume(0.8) - set to 80%");

Audio.MasterVolume(1.0);
Print("   Audio.MasterVolume(1.0) - set to 100%");

Audio.MasterVolume(0.5);
Print("   Audio.MasterVolume(0.5) - set to 50%");
Print("");

// ============================================================================
// 2. CHANNEL VOLUME CONTROL (Individual)
// ============================================================================
Print("2. Individual channel volume:");

var synth1 = CreateSynth("channel1");
var synth2 = CreateSynth("channel2");

// Set volume for specific channel
Audio.ChannelVolume(0, 0.6);
Print("   Audio.ChannelVolume(0, 0.6) - channel 0 to 60%");

Audio.ChannelVolume(1, 0.8);
Print("   Audio.ChannelVolume(1, 0.8) - channel 1 to 80%");

// Get channel volume
var ch0Volume = Audio.ChannelVolume(0);
Print($"   Channel 0 volume: {ch0Volume}");
Print("");

// ============================================================================
// 3. SET ALL CHANNELS VOLUME
// ============================================================================
Print("3. Set all channels volume:");

Audio.AllChannels(0.7);
Print("   Audio.AllChannels(0.7) - all channels to 70%");
Print("");

// ============================================================================
// 4. AUDIO INPUT CAPTURE
// ============================================================================
Print("4. Audio input capture:");

// Start capturing from input device 0
Audio.StartInputCapture(0);
Print("   Audio.StartInputCapture(0)");
Print("   → Started capturing from input device 0");

// Stop capturing
Audio.StopInputCapture(0);
Print("   Audio.StopInputCapture(0)");
Print("   → Stopped capturing");
Print("");

// ============================================================================
// 5. FREQUENCY TRIGGER (From Audio Input)
// ============================================================================
Print("5. Frequency trigger (audio input):");

var kickDrum = CreateSampleInstrument("kick");

// AddFrequencyTrigger(inputIndex, minFreq, maxFreq, threshold, action)
Audio.Input(0)
    .Frequency(20, 100)      // Low frequencies (20-100 Hz)
    .Threshold(0.5)          // 50% threshold
    .Trigger(() => {
        kickDrum.NoteOn(36, 100);  // Trigger kick on C2
    });

Print("   Audio.Input(0)");
Print("       .Frequency(20, 100)");
Print("       .Threshold(0.5)");
Print("       .Trigger(() => kickDrum.NoteOn(36, 100))");
Print("   → Triggers kick when low frequencies detected");
Print("");

// ============================================================================
// 6. FREQUENCY TRIGGER WITH NOTE
// ============================================================================
Print("6. Frequency trigger with note:");

var synth = CreateSynth("freq-synth");

Audio.Input(0)
    .Frequency(100, 500)     // Mid frequencies
    .Threshold(0.6)
    .TriggerNote(synth, 60, 100);  // Trigger C4

Print("   Audio.Input(0)");
Print("       .Frequency(100, 500)");
Print("       .Threshold(0.6)");
Print("       .TriggerNote(synth, 60, 100)");
Print("   → Triggers C4 on mid frequencies");
Print("");

// ============================================================================
// 7. MULTIPLE FREQUENCY BANDS
// ============================================================================
Print("7. Multiple frequency bands:");

var drums = CreateSampleInstrument("drums");

// Low (Kick)
Audio.Input(0)
    .Frequency(20, 100)
    .Threshold(0.5)
    .TriggerNote(drums, 36, 100);  // C2 - Kick

// Mid (Snare)
Audio.Input(0)
    .Frequency(150, 300)
    .Threshold(0.6)
    .TriggerNote(drums, 38, 90);   // D2 - Snare

// High (HiHat)
Audio.Input(0)
    .Frequency(5000, 10000)
    .Threshold(0.4)
    .TriggerNote(drums, 42, 70);   // F#2 - HiHat

Print("   Set up 3 frequency bands:");
Print("   - Low (20-100 Hz) → Kick");
Print("   - Mid (150-300 Hz) → Snare");
Print("   - High (5000-10000 Hz) → HiHat");
Print("");

// ============================================================================
// 8. DIRECT FREQUENCY TRIGGER (Function-based)
// ============================================================================
Print("8. Direct frequency trigger functions:");

// AddFrequencyTrigger(inputIndex, minFreq, maxFreq, threshold, action)
AddFrequencyTrigger(0, 20, 100, 0.5, (magnitude) => {
    Print($"Kick detected! Strength: {magnitude}");
    kickDrum.NoteOn(36, (int)(magnitude * 127));
});

Print("   AddFrequencyTrigger(0, 20, 100, 0.5, callback)");
Print("   → Callback receives magnitude value");
Print("");

Print("=== AUDIO CONTROL TEST COMPLETED ===");

// ============================================================================
// SYNTAX ELEMENTS TO CUSTOMIZE:
// ============================================================================
// AUDIO VOLUME FUNCTIONS:
// - Audio.MasterVolume (could be: master, volume, mainVolume, outputVolume)
// - Audio.ChannelVolume (could be: channel, channelVol, chVolume, track)
// - Audio.AllChannels (could be: allChannels, allVol, setAll, volumeAll)
//
// AUDIO INPUT FUNCTIONS:
// - Audio.StartInputCapture (could be: startInput, captureInput, recordInput)
// - Audio.StopInputCapture (could be: stopInput, stopCapture, endInput)
//
// FREQUENCY TRIGGER FLUENT API:
// - Audio.Input (could be: input, audioInput, capture, source)
// - Frequency (could be: freq, frequency, range, band, between)
// - Threshold (could be: threshold, level, sensitivity, trigger)
// - Trigger (could be: trigger, on, when, action, callback)
// - TriggerNote (could be: note, playNote, triggerNote, sendNote)
//
// DIRECT FREQUENCY FUNCTIONS:
// - AddFrequencyTrigger (could be: freqTrigger, onFrequency, listenFreq)
//
// PARAMETER NAMES:
// - inputIndex (could be: input, device, source, channel)
// - minFreq (could be: min, low, start, from)
// - maxFreq (could be: max, high, end, to)
// - threshold (could be: threshold, level, trigger, sensitivity)
// - magnitude (could be: magnitude, strength, level, intensity, power)
// ============================================================================
