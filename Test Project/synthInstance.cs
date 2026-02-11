


// Create the synthesizer instance
var synth = CreateSynth();
var file = File();
file.synth = synth;

// Oscillator 1 
synth.Waveform = WaveType.Sawtooth;
synth.Osc1Octave = 0;
synth.Osc1Semi = 0;
synth.Osc1Fine = 0f;
synth.Osc1Level = 0.8f;
synth.Osc1PulseWidth = 0.5f;
synth.Osc1Enabled = true;

// Oscillator 2 (detuned width)
synth.Osc2Waveform = WaveType.Sawtooth;
synth.Osc2Octave = 0;
synth.Osc2Semi = 0;
synth.Osc2Fine = 12f;
synth.Osc2Level = 0.6f;
synth.Osc2PulseWidth = 0.5f;
synth.Osc2Enabled = true;

// Sub/noise (defaults)
synth.SubOscLevel = 0.2f;
synth.SubOscWaveform = WaveType.Sine;
synth.SubOscEnabled = true;
synth.NoiseLevel = 0f;
synth.NoiseEnabled = false;

// Filter (Kernkraft bite)
synth.Cutoff = 0.65f;
synth.Resonance = 0.4f;
synth.FilterEnvAmount = 0.55f;
synth.FilterKeyTrack = 0.5f;
synth.FilterDrive = 0f;

// Amp envelope (punchy lead)
synth.Attack = 0.003f;
synth.Decay = 0.22f;
synth.Sustain = 0.6f;
synth.Release = 0.18f;

// Filter envelope (plucky cutoff sweep)
synth.FilterAttack = 0.002f;
synth.FilterDecay = 0.28f;
synth.FilterSustain = 0.25f;
synth.FilterRelease = 0.18f;

// LFO (defaults)
synth.LfoRate = 5f;
synth.LfoWaveform = WaveType.Sine;
synth.LfoToPitch = 0f;
synth.LfoToFilter = 0f;
synth.LfoToAmp = 0f;
synth.LfoToPulseWidth = 0f;

// Modulation (defaults)
synth.PitchBend = 0f;
synth.PitchBendRange = 2;
synth.ModWheel = 0f;
synth.VibratoRate = 5f;
synth.VibratoDepth = 0.3f;
synth.Portamento = 0f;

// Unison (wide + fat)
synth.UnisonVoices = 3;
synth.UnisonDetune = 22f;
synth.UnisonSpread = 0.9f;

// Output (safe defaults)
synth.Volume = 0.65f;
synth.Pan = 0f;
synth.Channel = -1;
synth.Reverb = 0f;
synth.Chorus = 0f;
synth.MaxPolyphony = 16;
synth.VelocitySensitivity = 0.7f;
synth.Name = "SimpleSynth";

// Extra oscillators (examples, disabled)
var osc1 = synth.Oscillator();
osc1.Waveform = WaveType.Sawtooth;
osc1.Octave = 0;
osc1.Semi = 0;
osc1.Fine = 0f;
osc1.Level = 0.3f;
osc1.PulseWidth = 0.5f;
osc1.Pan = 0f;
osc1.ModToPitch = 0f;
osc1.ModToFilter = 0f;
osc1.ModToAmp = 0f;
osc1.ModToPulseWidth = 0f;
osc1.Enabled = true;

var osc2 = synth.Oscillator();
osc2.Waveform = WaveType.Sawtooth;
osc2.Octave = 0;
osc2.Semi = 0;
osc2.Fine = 0f;
osc2.Level = 0.25f;
osc2.PulseWidth = 0.5f;
osc2.Pan = 0f;
osc2.ModToPitch = 0.015f;
osc2.ModToFilter = 0f;
osc2.ModToAmp = 0f;
osc2.ModToPulseWidth = 0f;
osc2.Enabled = true;

var osc3 = synth.Oscillator();
osc3.Waveform = WaveType.Square;
osc3.Octave = 0;
osc3.Semi = 0;
osc3.Fine = 0f;
osc3.Level = 0.25f;
osc3.PulseWidth = 0.45f;
osc3.Pan = 0f;
osc3.ModToPitch = -0.008f;
osc3.ModToFilter = 0f;
osc3.ModToAmp = 0f;
osc3.ModToPulseWidth = 0f;
osc3.Enabled = true;

var osc4 = synth.Oscillator();
osc4.Waveform = WaveType.Sine;
osc4.Octave = 0;
osc4.Semi = 0;
osc4.Fine = 0f;
osc4.Level = 0.5f;
osc4.PulseWidth = 0.5f;
osc4.Pan = 0f;
osc4.ModToPitch = 0f;
osc4.ModToFilter = 0f;
osc4.ModToAmp = 0f;
osc4.ModToPulseWidth = 0f;
osc4.Enabled = false;
