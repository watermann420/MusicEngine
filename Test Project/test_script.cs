File.Main();



Include synthInstance; // This includes the synthInstance.cs script which creates a basic synthesizer instance with default settings. You can customize it by editing that script.
var synth = Include.synthInstance.synth; // The above line gives us access to the 'synth' variable defined in synthInstance.cs, which is our synthesizer instance with default settings. We can now route MIDI and effects to it, and play patterns on it.
Include general_instruments; // This includes the general_instruments.cs script which creates a basic piano instance with default settings. You can customize it by editing that script.
var GM = Include.general_instruments.piano; // The above line gives us access to the 'piano' variable defined in general_instruments.cs, which is our General MIDI piano instance. We can route MIDI and effects to it, and play patterns on it.

Audio.master.Gain(0.1); // Set master gain to a safe level (adjust as needed)



// Create a VST instrument instance (replace "Vital" with your desired plugin name)
var vital = CreateVst("Vital");
var ch1 = Audio.CreateChannel(1);
ch1.to(master);
ch1.Gain(1.0); 
ch1.Pan(0.0); 
ch1.Route(vital);
ch1.Route(synth);
ch1.Route(piano);



// Create a VST effect instance (replace "Ozone 12 Equalizer" with your desired plugin name)
var eq1 = CreateVstEffect("Ozone 12 Equalizer");
ch1.Effect(eq1);
var fx = CreateVstEffect("ValhallaSupermassive");
ch1.Effect(fx);


//midi setup
midi.Device(0).to(vital, < synth); // MIDI channel 1 (0-based)
Midi.Device(0).Pitchbend().to(val => synth.piano.vital.PitchBend(val * 2f - 1f));
//midi.device(0).log.info(true); // Log MIDI input for debugging and mapping midi controls



// OPTIONAL: PLAY A PATTERN
var playPattern = false;  // Set to true to play

if (playPattern)
{
    var pattern = CreatePattern(synth);// Create a pattern
    pattern.LoopLength = 4.0; // Loop length in seconds (e.g., 4.0 for 4 seconds, 8.0 for 8 seconds, etc.)

    // Add some notes
    pattern.Note(60, 0.0, 0.5, 100);         // C4
    pattern.Note(64, 0.5, 0.5, 90);         // E4
    pattern.Note(67, 1.0, 0.5, 100);       // G4
    pattern.Note(72, 1.5, 0.5, 110);      // C5
    pattern.Note(67, 2.0, 0.5, 90);      // G4
    pattern.Note(64, 2.5, 0.5, 80);     // E4
    pattern.Note(60, 3.0, 1.0, 100);   // C4

    pattern.Play();
}



// OPTIONAL: PLAY TETRIS THEME (Korobeiniki)

var playTetris = false;  // Set to true to play

if (playTetris)
{
    var tetris = CreatePattern(vital); // Create a pattern
    tetris.LoopLength = 16.0; // 4 bars of 4/4 time (16 quarter notes)



    // Bar 1: E - B C - D - C B
    tetris.Note(76, 0.0, 0.9, 100);         // E5 (quarter)
    tetris.Note(71, 1.0, 0.4, 90);         // B4 (eighth)
    tetris.Note(72, 1.5, 0.4, 90);        // C5 (eighth)
    tetris.Note(74, 2.0, 0.9, 100);      // D5 (quarter)
    tetris.Note(72, 3.0, 0.4, 90);      // C5 (eighth)
    tetris.Note(71, 3.5, 0.4, 90);     // B4 (eighth)

    // Bar 2: A - A C - E - D C
    tetris.Note(69, 4.0, 0.9, 100);         // A4 (quarter)
    tetris.Note(69, 5.0, 0.4, 85);         // A4 (eighth)
    tetris.Note(72, 5.5, 0.4, 90);        // C5 (eighth)
    tetris.Note(76, 6.0, 0.9, 100);      // E5 (quarter)
    tetris.Note(74, 7.0, 0.4, 90);      // D5 (eighth)
    tetris.Note(72, 7.5, 0.4, 90);     // C5 (eighth)

    // Bar 3: B - - C - D - E -
    tetris.Note(71, 8.0, 1.4, 100);       // B4 (dotted quarter)
    tetris.Note(72, 9.5, 0.4, 90);       // C5 (eighth)
    tetris.Note(74, 10.0, 0.9, 100);    // D5 (quarter)
    tetris.Note(76, 11.0, 0.9, 100);   // E5 (quarter)

    // Bar 4: C - A - A - - -
    tetris.Note(72, 12.0, 0.9, 100);     // C5 (quarter)
    tetris.Note(69, 13.0, 0.9, 95);     // A4 (quarter)
    tetris.Note(69, 14.0, 1.9, 90);    // A4 (half - held)

    tetris.Play();
}


