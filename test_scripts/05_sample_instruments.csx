// ============================================================================
// 05 - SAMPLE INSTRUMENTS
// ============================================================================
// This script demonstrates sample loading and sample-based instruments
// SYNTAX TO REVIEW: Sample functions, file loading, sample mapping
// ============================================================================

Print("=== SAMPLE INSTRUMENTS TEST ===");
Print("");

// ============================================================================
// 1. CREATE SAMPLE INSTRUMENT (Empty)
// ============================================================================
Print("1. Creating empty sample instrument:");

var sampler1 = CreateSampleInstrument("drums");
Print($"   Created: {sampler1.Name}");
Print("");

// ============================================================================
// 2. LOAD SINGLE SAMPLE FILE
// ============================================================================
Print("2. Loading single sample file:");

// LoadSample(sampleInstrument, filePath)
// LoadSample(sampleInstrument, filePath, rootNote)
// LoadSample(sampleInstrument, filePath, rootNote, lowNote, highNote)

// Example (assuming samples exist):
// LoadSample(sampler1, "C:\\Samples\\kick.wav");
// LoadSample(sampler1, "C:\\Samples\\snare.wav", 38);  // D2
// LoadSample(sampler1, "C:\\Samples\\hihat.wav", 42, 42, 42);  // F#2

Print("   LoadSample(sampler, \"path/to/kick.wav\")");
Print("   LoadSample(sampler, \"path/to/snare.wav\", 38)");
Print("   LoadSample(sampler, \"path/to/hihat.wav\", 42, 42, 42)");
Print("");

// ============================================================================
// 3. LOAD SAMPLES FROM DIRECTORY
// ============================================================================
Print("3. Loading samples from directory:");

// LoadSamplesFromDirectory(sampleInstrument, directoryPath)
// Loads all .wav files from directory

// Example:
// LoadSamplesFromDirectory(sampler1, "C:\\Samples\\DrumKit");

Print("   LoadSamplesFromDirectory(sampler, \"path/to/samples\")");
Print("");

// ============================================================================
// 4. SAMPLE INSTRUMENT WITH FLUENT API
// ============================================================================
Print("4. Sample instrument with Fluent API:");

var drumKit = Sample.Create("drum-kit")
    .Load("C:\\Samples\\kick.wav", 36)      // C2 - Kick
    .Load("C:\\Samples\\snare.wav", 38)     // D2 - Snare
    .Load("C:\\Samples\\hihat.wav", 42)     // F#2 - HiHat
    .Volume(0.8)
    .Build();

Print("   Sample.Create(\"drum-kit\")");
Print("       .Load(\"kick.wav\", 36)");
Print("       .Load(\"snare.wav\", 38)");
Print("       .Load(\"hihat.wav\", 42)");
Print("       .Volume(0.8)");
Print("       .Build()");
Print("");

// ============================================================================
// 5. LOAD FROM DIRECTORY WITH FLUENT API
// ============================================================================
Print("5. Load directory with Fluent API:");

var sampler2 = Sample.Create("multi-sampler")
    .FromDirectory("C:\\Samples\\Piano")
    .Volume(0.6)
    .Build();

Print("   Sample.Create(\"multi-sampler\")");
Print("       .FromDirectory(\"C:\\\\Samples\\\\Piano\")");
Print("       .Volume(0.6)");
Print("       .Build()");
Print("");

// ============================================================================
// 6. MAP SAMPLES TO SPECIFIC NOTES
// ============================================================================
Print("6. Map samples to specific notes:");

var sampler3 = Sample.Create("mapped-sampler")
    .MapSample("C:\\Samples\\bass_c.wav", 36, 36, 40)   // C2-E2
    .MapSample("C:\\Samples\\bass_f.wav", 41, 41, 45)   // F2-A2
    .MapSample("C:\\Samples\\bass_g.wav", 46, 46, 50)   // A#2-D3
    .Build();

Print("   Sample.Create(\"mapped-sampler\")");
Print("       .MapSample(\"bass_c.wav\", 36, 36, 40)");
Print("       .MapSample(\"bass_f.wav\", 41, 41, 45)");
Print("       .MapSample(\"bass_g.wav\", 46, 46, 50)");
Print("       .Build()");
Print("");

// ============================================================================
// 7. SAMPLE INSTRUMENT PROPERTIES
// ============================================================================
Print("7. Sample instrument properties:");

Print($"   sampler1.Name: {sampler1.Name}");
Print($"   sampler1.SampleCount: {sampler1.SampleCount}");
Print($"   sampler1.Volume: {sampler1.Volume.Value}");
Print("");

// ============================================================================
// 8. PLAY SAMPLES
// ============================================================================
Print("8. Playing samples:");

// Same as synths - use NoteOn/NoteOff
sampler1.NoteOn(36, 100);  // Play kick
Print("   sampler1.NoteOn(36, 100) - play kick");

await Task.Delay(500);

sampler1.NoteOn(38, 90);   // Play snare
Print("   sampler1.NoteOn(38, 90) - play snare");

await Task.Delay(500);

sampler1.AllNotesOff();
Print("   sampler1.AllNotesOff()");
Print("");

// ============================================================================
// 9. CREATE PATTERN WITH SAMPLES
// ============================================================================
Print("9. Create pattern with sample instrument:");

var drumPattern = CreatePattern(sampler1, "drum-beat");
drumPattern.Loop = true;

// 4/4 beat
drumPattern.AddNote(0.0, 36, 100, 0.5);   // Kick
drumPattern.AddNote(1.0, 38, 90, 0.5);    // Snare
drumPattern.AddNote(2.0, 36, 100, 0.5);   // Kick
drumPattern.AddNote(3.0, 38, 90, 0.5);    // Snare

// HiHat on eighth notes
for (var i = 0; i < 8; i++)
{
    drumPattern.AddNote(i * 0.5, 42, 70, 0.25);
}

Print($"   Created pattern with {drumPattern.NoteCount} notes");
Print("");

Print("=== SAMPLE INSTRUMENTS TEST COMPLETED ===");

// ============================================================================
// SYNTAX ELEMENTS TO CUSTOMIZE:
// ============================================================================
// SAMPLE CREATION FUNCTIONS:
// - CreateSampleInstrument (could be: sampler, sample, newSampler, makeSampler)
// - LoadSample (could be: load, loadSample, addSample, sample)
// - LoadSamplesFromDirectory (could be: loadDir, fromDir, loadFolder, importDir)
//
// FLUENT API:
// - Sample.Create (could be: Sample.New, Sample.Make, Sampler.Create)
// - Load (could be: load, add, sample, with)
// - FromDirectory (could be: fromDir, directory, folder, importDir)
// - MapSample (could be: map, mapSample, assign, bind)
// - Volume (could be: volume, vol, gain, level)
// - Build (could be: build, create, make, done, finish)
//
// SAMPLE INSTRUMENT PROPERTIES:
// - Name (could be: name, id, label)
// - SampleCount (could be: sampleCount, count, samples, size)
// - Volume (could be: volume, vol, gain, level)
//
// SAMPLE INSTRUMENT METHODS:
// - NoteOn (could be: play, noteOn, trigger, start)
// - NoteOff (could be: stop, noteOff, release, end)
// - AllNotesOff (could be: stopAll, silence, killAll, panic)
// ============================================================================
