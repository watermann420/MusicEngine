// ============================================================================
// 06 - MIDI CONTROL & ROUTING
// ============================================================================
// This script demonstrates all MIDI input/output and routing functions
// SYNTAX TO REVIEW: MIDI functions, device access, routing methods
// ============================================================================

Print("=== MIDI CONTROL TEST ===");
Print("");

// ============================================================================
// 1. MIDI DEVICE ACCESS
// ============================================================================
Print("1. MIDI device access:");

// Device enumeration happens during Engine.Initialize()
Print("   MIDI devices are shown during initialization");
Print("");

// ============================================================================
// 2. ROUTE MIDI TO SYNTH (By Device Index)
// ============================================================================
Print("2. Route MIDI to synth (by index):");

var synth1 = CreateSynth("midi-synth");

// Midi.Device(index).To(synth)
Midi.Device(0).To(synth1);
Print("   Midi.Device(0).To(synth1)");
Print("   → Routes MIDI device 0 to synth1");
Print("");

// ============================================================================
// 3. ROUTE MIDI TO SYNTH (By Device Name)
// ============================================================================
Print("3. Route MIDI to synth (by name):");

var synth2 = CreateSynth("keyboard-synth");

// Midi.Device("DeviceName").To(synth)
Midi.Device("My MIDI Keyboard").To(synth2);
Print("   Midi.Device(\"My MIDI Keyboard\").To(synth2)");
Print("   → Routes specific MIDI device to synth2");
Print("");

// ============================================================================
// 4. MAP MIDI CC TO SYNTH PARAMETER
// ============================================================================
Print("4. Map MIDI CC to synth parameter:");

// Map CC1 (Modwheel) to filter cutoff
Midi.Device(0).MapCC(1, synth1.FilterCutoff, 200, 2000);
Print("   Midi.Device(0).MapCC(1, synth.FilterCutoff, 200, 2000)");
Print("   → CC1 controls filter cutoff (200-2000 Hz)");

// Map CC7 (Volume) to synth volume
Midi.Device(0).MapCC(7, synth1.Volume, 0, 1);
Print("   Midi.Device(0).MapCC(7, synth.Volume, 0, 1)");
Print("   → CC7 controls volume (0.0-1.0)");

// Map CC74 (Filter) to resonance
Midi.Device(0).MapCC(74, synth1.FilterResonance, 0.1, 10);
Print("   Midi.Device(0).MapCC(74, synth.FilterResonance, 0.1, 10)");
Print("   → CC74 controls resonance (0.1-10.0)");
Print("");

// ============================================================================
// 5. MAP PITCH BEND TO PARAMETER
// ============================================================================
Print("5. Map pitch bend to parameter:");

// MapPitchBend(synth, parameter, minValue, maxValue)
Midi.Device(0).MapPitchBend(synth1.Pitch, 0.5, 2.0);
Print("   Midi.Device(0).MapPitchBend(synth.Pitch, 0.5, 2.0)");
Print("   → Pitch bend controls pitch (0.5-2.0)");
Print("");

// ============================================================================
// 6. MAP NOTE RANGE TO SYNTH (Keyboard Split)
// ============================================================================
Print("6. Map note range to synth (keyboard split):");

var bass = CreateSynth("bass", SynthWaveform.Sawtooth);
var lead = CreateSynth("lead", SynthWaveform.Square);

// MapRange(lowNote, highNote, synth)
Midi.Device(0).MapRange(0, 59, bass);     // C0-B3 → bass
Midi.Device(0).MapRange(60, 127, lead);   // C4-G9 → lead

Print("   Midi.Device(0).MapRange(0, 59, bass)");
Print("   → Notes C0-B3 play bass");
Print("   Midi.Device(0).MapRange(60, 127, lead)");
Print("   → Notes C4-G9 play lead");
Print("");

// ============================================================================
// 7. SEND MIDI OUTPUT
// ============================================================================
Print("7. Send MIDI output:");

// Send note on
SendNoteOn(0, 60, 100, 0);  // device, note, velocity, channel
Print("   SendNoteOn(0, 60, 100, 0)");
Print("   → Send Note On to device 0");

await Task.Delay(500);

// Send note off
SendNoteOff(0, 60, 0);  // device, note, channel
Print("   SendNoteOff(0, 60, 0)");
Print("   → Send Note Off to device 0");

// Send CC
SendControlChange(0, 1, 64, 0);  // device, cc, value, channel
Print("   SendControlChange(0, 1, 64, 0)");
Print("   → Send CC1 = 64 to device 0");
Print("");

// ============================================================================
// 8. MAP TRANSPORT CONTROL TO MIDI
// ============================================================================
Print("8. Map transport control to MIDI:");

// Map note to start sequencer
Midi.Device(0).MapNoteToStart(60);  // C4 starts sequencer
Print("   Midi.Device(0).MapNoteToStart(60)");
Print("   → C4 starts sequencer");

// Map note to stop sequencer
Midi.Device(0).MapNoteToStop(61);   // C#4 stops sequencer
Print("   Midi.Device(0).MapNoteToStop(61)");
Print("   → C#4 stops sequencer");

// Map CC to BPM control
Midi.Device(0).MapCCToBpm(20, 60, 200);  // CC20 controls BPM (60-200)
Print("   Midi.Device(0).MapCCToBpm(20, 60, 200)");
Print("   → CC20 controls BPM (60-200)");

// Map CC to beat skip
Midi.Device(0).MapCCToSkip(21, 4);  // CC21 skips 4 beats
Print("   Midi.Device(0).MapCCToSkip(21, 4)");
Print("   → CC21 skips 4 beats");

// Map CC to scratching
Midi.Device(0).MapCCToScratch(22, 32);  // CC22 for scratching, 32 beat range
Print("   Midi.Device(0).MapCCToScratch(22, 32)");
Print("   → CC22 scratches within 32 beats");
Print("");

// ============================================================================
// 9. DIRECT MIDI ROUTING (Low-level)
// ============================================================================
Print("9. Direct MIDI routing (low-level):");

// RouteMidiInput(deviceIndex, synth)
RouteMidiInput(0, synth1);
Print("   RouteMidiInput(0, synth1)");
Print("   → Routes all MIDI from device 0 to synth1");

// MapMidiControl(deviceIndex, cc, parameter, min, max)
MapMidiControl(0, 1, synth1.FilterCutoff, 100, 5000);
Print("   MapMidiControl(0, 1, synth.FilterCutoff, 100, 5000)");
Print("   → Maps CC1 to filter cutoff");

// MapPitchBend(deviceIndex, parameter, min, max)
MapPitchBend(0, synth1.Pitch, 0.5, 2.0);
Print("   MapPitchBend(0, synth.Pitch, 0.5, 2.0)");
Print("   → Maps pitch bend to pitch");

// MapRange(deviceIndex, lowNote, highNote, synth)
MapRange(0, 48, 72, synth1);
Print("   MapRange(0, 48, 72, synth1)");
Print("   → Maps notes C3-C5 to synth1");
Print("");

Print("=== MIDI CONTROL TEST COMPLETED ===");

// ============================================================================
// SYNTAX ELEMENTS TO CUSTOMIZE:
// ============================================================================
// MIDI FLUENT API:
// - Midi.Device (could be: Midi.Dev, MIDI.Device, midi, device)
// - To (could be: to, route, send, connect, link)
// - MapCC (could be: cc, mapCC, controlChange, bindCC)
// - MapPitchBend (could be: pitchBend, bend, mapBend, bindBend)
// - MapRange (could be: range, mapRange, split, bindRange, zone)
// - MapNoteToStart (could be: startNote, mapStart, bindStart)
// - MapNoteToStop (could be: stopNote, mapStop, bindStop)
// - MapCCToBpm (could be: bpmCC, mapBpm, tempoCC)
// - MapCCToSkip (could be: skipCC, jumpCC, mapSkip)
// - MapCCToScratch (could be: scratchCC, mapScratch, jogCC)
//
// MIDI OUTPUT FUNCTIONS:
// - SendNoteOn (could be: noteOn, sendNote, playNote, triggerNote)
// - SendNoteOff (could be: noteOff, stopNote, releaseNote)
// - SendControlChange (could be: cc, sendCC, controlChange, sendControl)
//
// DIRECT ROUTING FUNCTIONS:
// - RouteMidiInput (could be: route, routeMIDI, connectMIDI, linkMIDI)
// - MapMidiControl (could be: mapCC, bindCC, controlMap, ccMap)
// - MapPitchBend (could be: bendMap, pitchMap, mapBend)
// - MapRange (could be: rangeMap, splitMap, zoneMap, keyRange)
// ============================================================================
