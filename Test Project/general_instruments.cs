

// General MIDI instrument (defaults)
var piano = CreateGeneralMidi();
piano.Program = GeneralMidiProgram.AcousticGrandPiano;
piano.Channel = 0;
piano.Volume = 0.8f;
piano.Pan = 0f;
piano.Reverb = 0f;
piano.Chorus = 0f;
piano.ModWheel = 0f;
piano.Name = "GM_AcousticGrandPiano";
var file = File();
file.piano = piano;
