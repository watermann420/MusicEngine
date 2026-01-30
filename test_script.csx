audio.all.gain(0.1); // Master volume //0 to 1.0 // Adjust as needed // Default is 0.1 is 10% volume





// Create the synthesizer
var synth = CreateSynth();

var synth2 = CreateSynth();


// OSCILLATOR 1 SETTINGS
synth.Waveform = WaveType.Square;    // Sine, Square, Sawtooth, Triangle, Pulse, Noise
synth.Osc1Octave = 0;                // Octave offset: -3 to +3
synth.Osc1Semi = 0;                  // Semitone detune: -12 to +12
synth.Osc1Fine = 0f;                 // Fine tune in cents: -100 to +100
synth.Osc1Level = 0.7f;              // Volume: 0 to 1
synth.Osc1PulseWidth = 0.9f;         // Pulse width (for Pulse wave): 0.1 to 0.9

// OSCILLATOR 2 SETTINGS
synth.Osc2Enabled = true;               // Enable second oscillator
synth.Osc2Waveform = WaveType.Sine; // Waveform type
synth.Osc2Octave = 0;                   // Octave offset: -3 to +3
synth.Osc2Semi = 0;                     // Semitone detune: -12 to +12
synth.Osc2Fine = 3f;                    // Slight detune for fat sound
synth.Osc2Level = 0.5f;                 // Volume: 0 to 1
synth.Osc2PulseWidth = 0.5f;            // Pulse width (for Pulse wave): 0.1 to 0.9

// SUB OSCILLATOR & NOISE
synth.SubOscLevel = 0.9f;             // Sub oscillator (1 octave down): 0 to 1
synth.SubOscWaveform = WaveType.Sine; // Sine or Square work best
synth.NoiseLevel = 0.0f;              // White noise: 0 to 1

// FILTER SETTINGS
synth.Cutoff = 1f;                   // Filter cutoff: 0 to 1 (maps to 20-20000 Hz)
synth.Resonance = 0.0f;              // Resonance/Q: 0 to 1
synth.FilterEnvAmount = 0.0f;        // Envelope to filter: -1 to +1
synth.FilterKeyTrack = 0.5f;         // Keyboard tracking: 0 to 1
synth.FilterDrive = 0.0f;            // Filter saturation: 0 to 1

// AMPLITUDE ENVELOPE (ADSR)
synth.Attack = 0.01f;                 // Attack time in seconds: 0.001 to 10
synth.Decay = 0.01f;                  // Decay time: 0.001 to 10
synth.Sustain = 0.2f;                 // Sustain level: 0 to 1
synth.Release = 0.08f;                // Release time: 0.001 to 10

// FILTER ENVELOPE (ADSR)
synth.FilterAttack = 0.01f;           // Filter envelope attack
synth.FilterDecay = 0.03f;            // Filter envelope decay
synth.FilterSustain = 0.03f;          // Filter envelope sustain
synth.FilterRelease = 0.03f;          // Filter envelope release

// LFO SETTINGS
synth.LfoRate = 5.0f;                // LFO speed in Hz: 0.01 to 50
synth.LfoWaveform = WaveType.Sine;   // Sine, Triangle, Square, Sawtooth
synth.LfoToPitch = 0.0f;             // LFO to pitch (semitones): 0 to 12
synth.LfoToFilter = 0.0f;            // LFO to filter cutoff: 0 to 1
synth.LfoToAmp = 0.0f;               // LFO to amplitude (tremolo): 0 to 1
synth.LfoToPulseWidth = 0.0f;        // LFO to pulse width: 0 to 0.4

// PITCH BEND & MODULATION
synth.PitchBendRange = 2;            // Pitch bend range in semitones: 1 to 24
// synth.PitchBend = 0.0f;           // Current pitch bend value: -1 to +1 (set by MIDI wheel)

// MOD WHEEL (CC#1 is auto-routed)
synth.VibratoRate = 20.0f;            // Vibrato speed (mod wheel controls depth): 0.1 to 20 Hz
synth.VibratoDepth = 2f;            // Max vibrato depth (semitones): 0 to 2
synth.ModWheel = 0.0f;             // Current mod wheel value: 0 to 1 (set by MIDI CC#1)

// PERFORMANCE
synth.Portamento = 0.0f;             // Glide time in seconds: 0 to 2 (0 = off)

// UNISON SETTINGS
synth.UnisonVoices = 5;              // Number of unison voices: 1 to 8
synth.UnisonDetune = 15f;            // Detune amount in cents: 0 to 50
synth.UnisonSpread = 1f;           // Stereo spread: 0 to 1

// EFFECTS

// Delay
synth.DelayMix = 0.0f;               // Delay wet/dry mix: 0 to 1
synth.DelayTime = 1f;              // Delay time in ms: 1 to 2000
synth.DelayFeedback = 0.4f;          // Delay feedback: 0 to 0.95

// Reverb
synth.ReverbMix = 0f;                // Reverb wet/dry mix: 0 to 1
synth.ReverbSize = 0.1f;             // Room size: 0 to 1
synth.ReverbDamping = 0.5f;          // High frequency damping: 0 to 1

// OUTPUT SETTINGS
synth.Volume = 0.7f;                 // Synth master volume: 0 to 1
synth.Pan = 0.0f;                    // Pan: -1 (left) to +1 (right)
synth.MaxPolyphony = 10;             // Max simultaneous notes: 1 to 64
synth.VelocitySensitivity = 0.7f;    // Velocity response: 0 to 1



// ROUTE MIDI TO SYNTH
midi.device(0).route(synth);









// OPTIONAL: PLAY A PATTERN
var playPattern = false;  // Set to true to play

if (playPattern)
{
    var pattern = CreatePattern(synth);
    pattern.LoopLength = 4.0;

    // Add some notes
    pattern.Note(60, 0.0, 0.5, 100);   // C4
    pattern.Note(64, 0.5, 0.5, 90);    // E4
    pattern.Note(67, 1.0, 0.5, 100);   // G4
    pattern.Note(72, 1.5, 0.5, 110);   // C5
    pattern.Note(67, 2.0, 0.5, 90);    // G4
    pattern.Note(64, 2.5, 0.5, 80);    // E4
    pattern.Note(60, 3.0, 1.0, 100);   // C4

    pattern.Play();
}






// OPTIONAL: PLAY TETRIS THEME (Korobeiniki)
var playTetris = false;  // Set to true to play

if (playTetris)
{
    var tetris = CreatePattern(synth);
    tetris.LoopLength = 16.0;



    // Bar 1: E - B C - D - C B
    tetris.Note(76, 0.0, 0.9, 100);    // E5 (quarter)
    tetris.Note(71, 1.0, 0.4, 90);     // B4 (eighth)
    tetris.Note(72, 1.5, 0.4, 90);     // C5 (eighth)
    tetris.Note(74, 2.0, 0.9, 100);    // D5 (quarter)
    tetris.Note(72, 3.0, 0.4, 90);     // C5 (eighth)
    tetris.Note(71, 3.5, 0.4, 90);     // B4 (eighth)

    // Bar 2: A - A C - E - D C
    tetris.Note(69, 4.0, 0.9, 100);    // A4 (quarter)
    tetris.Note(69, 5.0, 0.4, 85);     // A4 (eighth)
    tetris.Note(72, 5.5, 0.4, 90);     // C5 (eighth)
    tetris.Note(76, 6.0, 0.9, 100);    // E5 (quarter)
    tetris.Note(74, 7.0, 0.4, 90);     // D5 (eighth)
    tetris.Note(72, 7.5, 0.4, 90);     // C5 (eighth)

    // Bar 3: B - - C - D - E -
    tetris.Note(71, 8.0, 1.4, 100);    // B4 (dotted quarter)
    tetris.Note(72, 9.5, 0.4, 90);     // C5 (eighth)
    tetris.Note(74, 10.0, 0.9, 100);   // D5 (quarter)
    tetris.Note(76, 11.0, 0.9, 100);   // E5 (quarter)

    // Bar 4: C - A - A - - -
    tetris.Note(72, 12.0, 0.9, 100);   // C5 (quarter)
    tetris.Note(69, 13.0, 0.9, 95);    // A4 (quarter)
    tetris.Note(69, 14.0, 1.9, 90);    // A4 (half - held)

    tetris.Play();
}


