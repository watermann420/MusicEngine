# MusicEngine Modular Parameter System

## Overview

MusicEngine features a fully modular parameter system inspired by **VCV Rack** and other Eurorack-style modular synthesizers. Every parameter on every instrument, effect, and synthesizer can be:

1. **A modulation destination** - receiving control signals from any modulation source
2. **A modulation source** - sending its value to control other parameters

This creates an incredibly flexible and powerful system for sound design, where almost anything can control almost anything else.

## Key Concepts

### ModularParameter

The `ModularParameter` class is the foundation of the system. Every controllable value in MusicEngine is a `ModularParameter`:

```csharp
// Creating a parameter
var cutoff = new ModularParameter("cutoff", "Filter Cutoff", 20, 20000, 1000)
{
    Type = ParameterType.Filter,
    Unit = "Hz",
    AudioRate = true,
    Description = "Filter cutoff frequency"
};

// Setting values
cutoff.Value = 5000;          // Direct value
cutoff.NormalizedValue = 0.5; // Normalized (0-1)
cutoff.BaseValue = 1000;      // Base value (before modulation)
```

### IModulationSource

Anything that can output a modulation signal implements `IModulationSource`:

```csharp
public interface IModulationSource
{
    string Id { get; }
    string Name { get; }
    bool IsBipolar { get; }  // -1 to 1 (bipolar) or 0 to 1 (unipolar)
    double GetValue();
    double GetValueAtSample(int sampleOffset);
}
```

Built-in modulation sources include:
- **LFOs** (ModularLFO)
- **Envelopes** (ModularEnvelope)
- **Other parameters** (ParameterModulationSource)
- **MIDI CC** (MidiCCModulationSource)
- **Velocity** (VelocityModulationSource)
- **Key Tracking** (KeyTrackModulationSource)
- **Random/Noise** (NoiseModulationSource)
- **Constants** (ConstantModulationSource)

### Modulation Connections

Connecting sources to destinations is simple:

```csharp
// Connect an LFO to filter cutoff with 50% depth
var connection = synth.Connect("lfo1", "cutoff", 0.5);

// Or using objects directly
var lfo = new ModularLFO("lfo1", "LFO 1", sampleRate);
synth.Connect(lfo, cutoff, 0.5);

// Disconnect
synth.Disconnect(connection);

// Clear all modulation
synth.ClearAllModulation();
```

## Modulation Sources

### LFO (Low Frequency Oscillator)

```csharp
var lfo = new ModularLFO("lfo1", "LFO 1", sampleRate);

// Parameters:
lfo.Rate.Value = 2.0;      // 2 Hz
lfo.Depth.Value = 0.5;     // 50% depth
lfo.Waveform.Value = 0;    // 0=Sine, 1=Tri, 2=Saw, 3=Square, 4=S&H, 5=Smooth
lfo.Phase.Value = 0;       // Phase offset (0-1)
lfo.Offset.Value = 0;      // DC offset

// Tempo sync
lfo.TempoSync = true;
lfo.Tempo = 120;           // BPM
```

### Envelope

```csharp
var env = new ModularEnvelope("env1", "Amp Env", sampleRate);

// ADSR Parameters:
env.Attack.Value = 0.01;   // 10ms attack
env.Decay.Value = 0.2;     // 200ms decay
env.Sustain.Value = 0.7;   // 70% sustain level
env.Release.Value = 0.3;   // 300ms release

// Curve shapes (-1 to 1, 0 = linear)
env.AttackCurve.Value = 0.5;   // Exponential attack
env.DecayCurve.Value = -0.3;   // Logarithmic decay

// Trigger and release
env.Trigger();
env.ReleaseGate();
```

### MIDI CC

```csharp
var modWheel = new MidiCCModulationSource("cc1", 1, "Mod Wheel");
modWheel.SetValue(64); // Set CC value (0-127)

// Connect to vibrato depth
synth.Connect(modWheel, synth.GetParameter("vibratoDepth"), 1.0);
```

### Velocity

```csharp
var velocity = new VelocityModulationSource();
velocity.SetVelocity(100); // Set velocity (0-127)

// Velocity to filter cutoff
synth.Connect(velocity, cutoff, 0.3);
```

### Key Tracking

```csharp
var keyTrack = new KeyTrackModulationSource();
keyTrack.CenterNote = 60;  // Middle C as center
keyTrack.Scale = 12;       // 1 octave = 1.0 output

keyTrack.SetNote(72);      // Playing C5

// Key tracking to filter (higher notes = brighter)
synth.Connect(keyTrack, cutoff, 0.5);
```

### Random/Noise

```csharp
var noise = new NoiseModulationSource("noise", "Random", 1.0, 0.5);
noise.Rate = 4.0;          // Generate new value 4 times per second
noise.Smoothing = 0.5;     // Interpolation between values
noise.IsBipolar = true;    // -1 to 1 output

synth.Connect(noise, pan, 0.2);  // Random panning
```

## Modular Synth Components

### ModularOscillator

A VCV Rack-style oscillator with full modulation:

```csharp
var osc = new ModularOscillator(sampleRate);

// All parameters are modulatable:
osc.Frequency.Value = 440;     // Base frequency
osc.FineTune.Value = 0;        // Fine tune in cents
osc.PulseWidth.Value = 0.5;    // PWM
osc.Level.Value = 1.0;         // Output level
osc.FM.Value = 0;              // FM amount
osc.PM.Value = 0;              // Phase mod amount
osc.Waveform.Value = 1;        // 0=Sine, 1=Saw, 2=Square, 3=Tri, 4=Noise

// Sub oscillator
osc.SubOscLevel.Value = 0.3;
osc.SubOscOctave.Value = -1;   // One octave down

// Hard sync
osc.HardSync = true;
osc.SyncSource = masterOsc;
```

### ModularFilter

```csharp
var filter = new ModularFilter(sampleRate);

filter.Cutoff.Value = 1000;       // Hz
filter.Resonance.Value = 0.3;     // 0-1
filter.Drive.Value = 1.0;         // Input saturation
filter.EnvAmount.Value = 0.5;     // Envelope depth
filter.KeyTrack.Value = 0.5;      // Keyboard tracking
filter.FilterType.Value = 4;      // 0=LP, 1=HP, 2=BP, 3=Notch, 4=Moog
```

## Creating a Modular Synth

Extend `ModularSynthBase` to create your own fully modular instrument:

```csharp
public class MyModularSynth : ModularSynthBase
{
    private readonly ModularOscillator _osc1, _osc2;
    private readonly ModularFilter _filter;
    private readonly ModularEnvelope _ampEnv, _filterEnv;
    private readonly ModularLFO _lfo1, _lfo2;

    public MyModularSynth(int sampleRate)
    {
        // Create components
        _osc1 = new ModularOscillator(sampleRate);
        _osc2 = new ModularOscillator(sampleRate);
        _filter = new ModularFilter(sampleRate);
        _ampEnv = new ModularEnvelope("ampEnv", "Amp Env", sampleRate);
        _filterEnv = new ModularEnvelope("filterEnv", "Filter Env", sampleRate);
        _lfo1 = new ModularLFO("lfo1", "LFO 1", sampleRate);
        _lfo2 = new ModularLFO("lfo2", "LFO 2", sampleRate);

        // Register all parameters
        RegisterOscillatorParameters("osc1", _osc1);
        RegisterOscillatorParameters("osc2", _osc2);
        foreach (var p in _filter.GetAllParameters())
            _parameters[p.Id] = p;
        foreach (var p in _ampEnv.GetAllParameters())
            _parameters[p.Id] = p;
        // ... etc

        // Register modulation sources
        RegisterModulationSource(_ampEnv);
        RegisterModulationSource(_filterEnv);
        RegisterModulationSource(_lfo1);
        RegisterModulationSource(_lfo2);

        // Default modulation routing
        Connect(_filterEnv, _filter.Cutoff, 0.5);  // Filter env -> cutoff
        Connect(_lfo1, _osc1.PulseWidth, 0.3);     // LFO1 -> PWM
    }
}
```

## Preset System

Save and load complete synth states including modulation routing:

```csharp
// Save preset
var preset = synth.SavePreset("My Awesome Lead");
preset.Author = "Your Name";
preset.Description = "Fat detuned lead with filter modulation";
preset.Tags.Add("lead");
preset.Tags.Add("analog");

// Save to JSON (example)
var json = JsonSerializer.Serialize(preset);

// Load preset
synth.LoadPreset(preset);
```

## Inline Code Sliders (Strudel-style)

MusicEngine Editor includes Strudel.cc-style inline sliders. Numbers in your code become interactive:

- **Hover** over a number to see its range
- **Drag left/right** to change the value in real-time
- **Shift+Drag** for fine control
- **Ctrl+Drag** for coarse control
- **Escape** to cancel a drag

```csharp
// In your script, numbers are interactive:
synth.SetParameter("cutoff", 0.5);  // <- Drag this 0.5!
var bpm = 120;                      // <- Drag this 120!
pattern.Note(60, 0.25, 100);        // <- Drag any of these!
```

You can also add slider annotations:

```csharp
var volume = 0.8; // @slider(0, 1, 0.01, "Volume")
var bpm = 120;    // @slider(60, 200, 1, "BPM")
```

## Audio-Rate Modulation

For parameters that support audio-rate modulation (like FM synthesis):

```csharp
// Mark parameter as audio-rate capable
osc.FM.AudioRate = true;

// In your processing loop:
for (int i = 0; i < sampleCount; i++)
{
    // Get modulated value at exact sample offset
    var fmAmount = osc.FM.GetValueAtSample(i);

    // Use for FM synthesis
    var modulator = modOsc.Process();
    var carrier = carrierOsc.Process(modulator * fmAmount);
}
```

## Best Practices

1. **Use meaningful parameter IDs**: `"osc1.freq"` not `"p1"`
2. **Set appropriate ranges**: Match real-world values (Hz for frequency, dB for levels)
3. **Document parameters**: Use the Description field
4. **Group by type**: Use ParameterType for UI organization
5. **Save modulation routing**: Include connections in your presets
6. **Consider audio-rate needs**: Only enable AudioRate for parameters that need it

## Example: Complete Patch

```csharp
// Create synth
var synth = new MyModularSynth(44100);

// Configure oscillators
synth.SetParameter("osc1.wave", 1);      // Sawtooth
synth.SetParameter("osc2.wave", 1);      // Sawtooth
synth.SetParameter("osc2.fine", 7);      // Slight detune

// Configure filter
synth.SetParameter("cutoff", 800);
synth.SetParameter("reso", 0.4);

// Configure envelopes
synth.SetParameter("ampEnv.attack", 0.01);
synth.SetParameter("ampEnv.decay", 0.2);
synth.SetParameter("ampEnv.sustain", 0.6);
synth.SetParameter("ampEnv.release", 0.3);

synth.SetParameter("filterEnv.attack", 0.05);
synth.SetParameter("filterEnv.decay", 0.3);
synth.SetParameter("filterEnv.sustain", 0.2);

// Setup modulation
synth.Connect("filterEnv", "cutoff", 0.7);    // Filter envelope
synth.Connect("lfo1", "osc1.pw", 0.3);        // PWM
synth.Connect("lfo2", "cutoff", 0.2);         // Filter wobble
synth.Connect("velocity", "cutoff", 0.3);     // Velocity sensitivity

// Play!
synth.NoteOn(60, 100);
```

## API Reference

See the source files for complete API documentation:
- `ModularParameter.cs` - Parameter and modulation source classes
- `ModularSynthBase.cs` - Base class for modular instruments
- `ModularOscillator.cs` - Oscillator, filter, envelope, LFO components
