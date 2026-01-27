# Modular Effects System - Developer Guide

This guide explains how to use the code-based modular effects system to create custom audio processing chains, synthesizers, and effects.

## Overview

The modular effects system lets you:

- **Build Custom Synths**: Combine oscillators, filters, envelopes, and effects
- **Create Effect Chains**: Chain multiple effects in any order
- **Make Generative Music**: Use sequencers, clocks, and random sources
- **Save/Load Presets**: Serialize effect graphs as patches

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        EffectGraph                               │
│                                                                  │
│  ┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐  │
│  │Oscillator│───►│  Filter  │───►│   VCA    │───►│  Output  │  │
│  └──────────┘    └──────────┘    └──────────┘    └──────────┘  │
│        │              ▲              ▲                          │
│        │              │              │                          │
│        │         ┌────┴────┐    ┌────┴────┐                    │
│        └────────►│   LFO   │    │Envelope │                    │
│                  └─────────┘    └─────────┘                    │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

## Quick Start

```csharp
using MusicEngineEditor.Effects;

// Create a graph
var graph = new EffectGraph { Name = "My Synth" };

// Create nodes
var osc = EffectNodeFactory.CreateNode("Oscillator");
var filter = EffectNodeFactory.CreateNode("Filter");
var output = EffectNodeFactory.CreateNode("AudioOutput");

// Add nodes to graph
graph.AddNode(osc);
graph.AddNode(filter);
graph.AddNode(output);

// Connect: Oscillator -> Filter -> Output
graph.Connect(osc.Id, 0, filter.Id, 0);
graph.Connect(filter.Id, 0, output.Id, 0);

// Process audio
float[] buffer = new float[512];
graph.Process(buffer, 512, 44100);
```

## Node Types

### Generators

| Node | Outputs | Key Parameters |
|------|---------|----------------|
| `Oscillator` | Out, Sub | Frequency, Waveform, PulseWidth, Detune |
| `NoiseGenerator` | White, Pink, Brown | Level |
| `LFO` | Sine, Saw, Square, Triangle | Rate, Depth, Offset, Phase |

### Filters

| Node | Outputs | Key Parameters |
|------|---------|----------------|
| `Filter` | LP, HP, BP, Notch | Cutoff, Resonance, Drive, Mode |
| `EQ3Band` | Out | Low/Mid/High Gain, Frequencies |
| `FormantFilter` | Out | Vowel, Resonance, Mix |

### Dynamics

| Node | Outputs | Key Parameters |
|------|---------|----------------|
| `Gain` | Out | Gain (dB), Smooth |
| `Compressor` | Out, GR | Threshold, Ratio, Attack, Release, Knee, Look-ahead |
| `Limiter` | Out, GR | Ceiling, Release, Look-ahead |
| `Gate` | Out, Gate | Threshold, Attack, Hold, Release, Range, Hysteresis |

### Effects

| Node | Outputs | Key Parameters |
|------|---------|----------------|
| `Delay` | Out, Wet | Time, Feedback, Mix, Mod Rate/Depth, Filter, Ping-Pong |
| `Reverb` | Out, L, R | Size, Damping, Width, Pre-delay, Mix |
| `Chorus` | Out, L, R | Rate, Depth, Voices, Spread, Mix |
| `Flanger` | Out | Rate, Depth, Manual, Feedback, Mix |
| `Phaser` | Out | Rate, Depth, Feedback, Stages, Freq Min/Max |
| `Bitcrusher` | Out | Bits, Downsample, Noise Shape, Mix |
| `Distortion` | Out | Drive, Type, Tone, Mix |
| `RingMod` | Out | Frequency, Waveform, Mix |

### Modulators

| Node | Outputs | Key Parameters |
|------|---------|----------------|
| `Envelope` | Env, Inv, EOC | Attack, Decay, Sustain, Release, Curve |
| `SampleAndHold` | Out, Noise | Glide, Noise Mix |
| `Slew` | Out | Rise, Fall, Shape |
| `Quantizer` | Out, Trigger | Scale, Root, Range |

### Utilities

| Node | Outputs | Key Parameters |
|------|---------|----------------|
| `Mixer` | Out L, Out R, Mono | Level 1-4, Pan 1-4 |
| `VCA` | Out | Gain, Response, CV Depth |
| `Split` | Out 1-4 | (none) |
| `Merge` | Sum, Avg, Max | (none) |
| `Inverter` | Out, +Out | Offset |
| `Offset` | Out | Offset, Scale |
| `Rectifier` | Full, Half+, Half- | (none) |
| `Crossfade` | Out | Mix, Curve |

### Analyzers

| Node | Outputs | Key Parameters |
|------|---------|----------------|
| `Follower` | Env, Gate | Attack, Release, Threshold |
| `PitchDetect` | Pitch, Freq, Confidence | Min/Max Freq |

### Sequencing

| Node | Outputs | Key Parameters |
|------|---------|----------------|
| `Clock` | Beat, 8th, 16th, Phase | BPM, Swing, Pulse Width |
| `ClockDiv` | /2, /4, /8, /16 | (none) |
| `StepSequencer` | CV, Gate, Trigger | Step 1-8, Steps, Gate Length |

### I/O

| Node | Outputs | Key Parameters |
|------|---------|----------------|
| `AudioInput` | L, R, Mono | Gain |
| `AudioOutput` | (none) | Gain |
| `MidiInput` | Pitch, Gate, Velocity, Aftertouch | Channel |
| `MidiOutput` | (none) | Channel |

## Connection System

### Port Types

```csharp
public enum PortDataType
{
    Audio,    // Audio signal (-1.0 to +1.0)
    Control,  // Control voltage (0.0 to 1.0 typically)
    Trigger,  // Momentary pulse (0 or 1)
    Gate      // On/off state (0 or 1)
}
```

### Connecting Nodes

```csharp
// Connect output 0 of node A to input 0 of node B
graph.Connect(nodeA.Id, outputPort: 0, nodeB.Id, inputPort: 0);

// Connect with attenuation
graph.Connect(nodeA.Id, 0, nodeB.Id, 0, amount: 0.5f);

// Disconnect
graph.Disconnect(connectionId);
```

### Multi-Input Connections

A single input can only have one connection. To mix multiple sources, use a Mixer or Merge node:

```csharp
var osc1 = EffectNodeFactory.CreateNode("Oscillator");
var osc2 = EffectNodeFactory.CreateNode("Oscillator");
var mixer = EffectNodeFactory.CreateNode("Mixer");
var filter = EffectNodeFactory.CreateNode("Filter");

graph.AddNode(osc1);
graph.AddNode(osc2);
graph.AddNode(mixer);
graph.AddNode(filter);

// Mix two oscillators
graph.Connect(osc1.Id, 0, mixer.Id, 0);  // Osc1 -> Mixer In 1
graph.Connect(osc2.Id, 0, mixer.Id, 1);  // Osc2 -> Mixer In 2

// Mixer output to filter
graph.Connect(mixer.Id, 2, filter.Id, 0);  // Mixer Mono -> Filter
```

## Parameter Control

### Setting Parameters

```csharp
var osc = EffectNodeFactory.CreateNode("Oscillator");

// By name
osc.Parameters.First(p => p.Name == "Frequency").Value = 440f;
osc.Parameters.First(p => p.Name == "Waveform").Value = 1f;  // Sawtooth

// Using helper
void SetParam(IEffectNode node, string name, float value)
{
    var param = node.Parameters.FirstOrDefault(p => p.Name == name);
    if (param != null) param.Value = value;
}

SetParam(osc, "Frequency", 880f);
```

### Parameter Scales

```csharp
public enum ParameterScale
{
    Linear,      // Linear interpolation
    Logarithmic, // Log scale (for frequencies)
    Exponential, // Exp scale (for some time values)
    Toggle       // 0 or 1 only
}
```

### Normalized Values

```csharp
// Get/set normalized (0-1) value
float normalized = param.NormalizedValue;
param.NormalizedValue = 0.5f;  // Sets to midpoint of range
```

## Building Synthesizers

### Basic Subtractive Synth

```csharp
public class SubtractiveSynth
{
    private readonly EffectGraph _graph;
    private readonly IEffectNode _osc;
    private readonly IEffectNode _filter;
    private readonly IEffectNode _ampEnv;
    private readonly IEffectNode _filterEnv;
    private readonly IEffectNode _vca;

    public SubtractiveSynth()
    {
        _graph = new EffectGraph { Name = "Subtractive Synth" };

        // Create nodes
        _osc = EffectNodeFactory.CreateNode("Oscillator");
        _filter = EffectNodeFactory.CreateNode("Filter");
        _ampEnv = EffectNodeFactory.CreateNode("Envelope");
        _filterEnv = EffectNodeFactory.CreateNode("Envelope");
        _vca = EffectNodeFactory.CreateNode("VCA");
        var output = EffectNodeFactory.CreateNode("AudioOutput");

        // Add nodes
        _graph.AddNode(_osc);
        _graph.AddNode(_filter);
        _graph.AddNode(_ampEnv);
        _graph.AddNode(_filterEnv);
        _graph.AddNode(_vca);
        _graph.AddNode(output);

        // Signal path: Osc -> Filter -> VCA -> Output
        _graph.Connect(_osc.Id, 0, _filter.Id, 0);
        _graph.Connect(_filter.Id, 0, _vca.Id, 0);
        _graph.Connect(_vca.Id, 0, output.Id, 0);

        // Modulation: Filter Envelope -> Filter Cutoff CV
        _graph.Connect(_filterEnv.Id, 0, _filter.Id, 1);

        // Modulation: Amp Envelope -> VCA CV
        _graph.Connect(_ampEnv.Id, 0, _vca.Id, 1);

        // Default settings
        SetParam(_osc, "Waveform", 1f);  // Sawtooth
        SetParam(_filter, "Cutoff", 2000f);
        SetParam(_filter, "Resonance", 0.5f);
        SetParam(_ampEnv, "Attack", 10f);
        SetParam(_ampEnv, "Decay", 100f);
        SetParam(_ampEnv, "Sustain", 0.7f);
        SetParam(_ampEnv, "Release", 200f);
    }

    public void NoteOn(float frequency, float velocity = 1f)
    {
        SetParam(_osc, "Frequency", frequency);

        // Trigger envelopes via gate input
        _ampEnv.SetInputValue(0, 1f);  // Gate high
        _filterEnv.SetInputValue(0, 1f);
    }

    public void NoteOff()
    {
        _ampEnv.SetInputValue(0, 0f);  // Gate low
        _filterEnv.SetInputValue(0, 0f);
    }

    public void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        _graph.Process(buffer, sampleCount, sampleRate);
    }

    private void SetParam(IEffectNode node, string name, float value)
    {
        var param = node.Parameters.FirstOrDefault(p => p.Name == name);
        if (param != null) param.Value = value;
    }
}
```

### FM Synthesizer

```csharp
public class FMSynth
{
    private readonly EffectGraph _graph;

    public FMSynth()
    {
        _graph = new EffectGraph { Name = "FM Synth" };

        // Carrier and Modulator
        var carrier = EffectNodeFactory.CreateNode("Oscillator");
        var modulator = EffectNodeFactory.CreateNode("Oscillator");
        var modDepth = EffectNodeFactory.CreateNode("VCA");
        var ampEnv = EffectNodeFactory.CreateNode("Envelope");
        var modEnv = EffectNodeFactory.CreateNode("Envelope");
        var output = EffectNodeFactory.CreateNode("AudioOutput");

        _graph.AddNode(carrier);
        _graph.AddNode(modulator);
        _graph.AddNode(modDepth);
        _graph.AddNode(ampEnv);
        _graph.AddNode(modEnv);
        _graph.AddNode(output);

        // Modulator -> Mod Depth VCA -> Carrier Freq CV
        _graph.Connect(modulator.Id, 0, modDepth.Id, 0);
        _graph.Connect(modDepth.Id, 0, carrier.Id, 0);  // To freq CV input

        // Mod envelope -> Mod depth
        _graph.Connect(modEnv.Id, 0, modDepth.Id, 1);

        // Carrier -> Amp VCA -> Output
        var ampVca = EffectNodeFactory.CreateNode("VCA");
        _graph.AddNode(ampVca);
        _graph.Connect(carrier.Id, 0, ampVca.Id, 0);
        _graph.Connect(ampEnv.Id, 0, ampVca.Id, 1);
        _graph.Connect(ampVca.Id, 0, output.Id, 0);

        // Set sine waves for both
        SetParam(carrier, "Waveform", 0f);
        SetParam(modulator, "Waveform", 0f);

        // FM ratio (modulator = 2x carrier frequency)
        SetParam(modulator, "Frequency", 880f);
        SetParam(carrier, "Frequency", 440f);
    }

    private void SetParam(IEffectNode node, string name, float value)
    {
        var param = node.Parameters.FirstOrDefault(p => p.Name == name);
        if (param != null) param.Value = value;
    }
}
```

## Building Effect Chains

### Guitar Pedal Board

```csharp
public class PedalBoard
{
    private readonly EffectGraph _graph;

    public PedalBoard()
    {
        _graph = new EffectGraph { Name = "Guitar Pedals" };

        var input = EffectNodeFactory.CreateNode("AudioInput");
        var tuner = EffectNodeFactory.CreateNode("PitchDetect");
        var compressor = EffectNodeFactory.CreateNode("Compressor");
        var overdrive = EffectNodeFactory.CreateNode("Distortion");
        var eq = EffectNodeFactory.CreateNode("EQ3Band");
        var chorus = EffectNodeFactory.CreateNode("Chorus");
        var delay = EffectNodeFactory.CreateNode("Delay");
        var reverb = EffectNodeFactory.CreateNode("Reverb");
        var output = EffectNodeFactory.CreateNode("AudioOutput");

        // Add all nodes
        foreach (var node in new[] { input, tuner, compressor, overdrive, eq, chorus, delay, reverb, output })
            _graph.AddNode(node);

        // Signal chain
        _graph.Connect(input.Id, 2, compressor.Id, 0);  // Mono input
        _graph.Connect(compressor.Id, 0, overdrive.Id, 0);
        _graph.Connect(overdrive.Id, 0, eq.Id, 0);
        _graph.Connect(eq.Id, 0, chorus.Id, 0);
        _graph.Connect(chorus.Id, 0, delay.Id, 0);
        _graph.Connect(delay.Id, 0, reverb.Id, 0);
        _graph.Connect(reverb.Id, 0, output.Id, 0);

        // Tuner (parallel, doesn't affect signal)
        _graph.Connect(input.Id, 2, tuner.Id, 0);

        // Configure effects
        ConfigureCompressor(compressor);
        ConfigureOverdrive(overdrive);
        ConfigureChorus(chorus);
        ConfigureDelay(delay);
        ConfigureReverb(reverb);
    }

    private void ConfigureCompressor(IEffectNode node)
    {
        SetParam(node, "Threshold", -20f);
        SetParam(node, "Ratio", 4f);
        SetParam(node, "Attack", 5f);
        SetParam(node, "Release", 100f);
        SetParam(node, "Makeup", 6f);
    }

    private void ConfigureOverdrive(IEffectNode node)
    {
        SetParam(node, "Drive", 3f);
        SetParam(node, "Type", 2f);  // Tube
        SetParam(node, "Tone", 0.6f);
        SetParam(node, "Mix", 1f);
    }

    private void ConfigureChorus(IEffectNode node)
    {
        SetParam(node, "Rate", 0.8f);
        SetParam(node, "Depth", 0.4f);
        SetParam(node, "Mix", 0.3f);
    }

    private void ConfigureDelay(IEffectNode node)
    {
        SetParam(node, "Time", 375f);  // Dotted 8th @ 120 BPM
        SetParam(node, "Feedback", 0.35f);
        SetParam(node, "Mix", 0.25f);
    }

    private void ConfigureReverb(IEffectNode node)
    {
        SetParam(node, "Size", 0.5f);
        SetParam(node, "Damping", 0.4f);
        SetParam(node, "Mix", 0.2f);
    }

    private void SetParam(IEffectNode node, string name, float value)
    {
        var param = node.Parameters.FirstOrDefault(p => p.Name == name);
        if (param != null) param.Value = value;
    }
}
```

## Generative Music

### Generative Patch

```csharp
public class GenerativePatch
{
    private readonly EffectGraph _graph;

    public GenerativePatch()
    {
        _graph = new EffectGraph { Name = "Generative" };

        // Clock
        var clock = EffectNodeFactory.CreateNode("Clock");
        SetParam(clock, "BPM", 80f);
        SetParam(clock, "Swing", 0.15f);

        // Random source
        var noise = EffectNodeFactory.CreateNode("NoiseGenerator");
        var sampleHold = EffectNodeFactory.CreateNode("SampleAndHold");

        // Quantizer for musical notes
        var quantizer = EffectNodeFactory.CreateNode("Quantizer");
        SetParam(quantizer, "Scale", 10f);  // Minor Pentatonic
        SetParam(quantizer, "Root", 0f);    // C

        // Oscillator
        var osc = EffectNodeFactory.CreateNode("Oscillator");
        SetParam(osc, "Waveform", 0f);  // Sine

        // Envelope
        var env = EffectNodeFactory.CreateNode("Envelope");
        SetParam(env, "Attack", 5f);
        SetParam(env, "Decay", 200f);
        SetParam(env, "Sustain", 0.3f);
        SetParam(env, "Release", 500f);

        // VCA and effects
        var vca = EffectNodeFactory.CreateNode("VCA");
        var delay = EffectNodeFactory.CreateNode("Delay");
        var reverb = EffectNodeFactory.CreateNode("Reverb");
        var output = EffectNodeFactory.CreateNode("AudioOutput");

        // Add all nodes
        foreach (var node in new[] { clock, noise, sampleHold, quantizer, osc, env, vca, delay, reverb, output })
            _graph.AddNode(node);

        // Clock -> Sample & Hold trigger
        _graph.Connect(clock.Id, 1, sampleHold.Id, 1);  // 8th notes

        // Noise -> Sample & Hold -> Quantizer -> Oscillator
        _graph.Connect(noise.Id, 0, sampleHold.Id, 0);
        _graph.Connect(sampleHold.Id, 0, quantizer.Id, 0);
        _graph.Connect(quantizer.Id, 0, osc.Id, 0);  // CV to frequency

        // Clock -> Envelope trigger
        _graph.Connect(clock.Id, 1, env.Id, 0);  // Gate

        // Signal path
        _graph.Connect(osc.Id, 0, vca.Id, 0);
        _graph.Connect(env.Id, 0, vca.Id, 1);
        _graph.Connect(vca.Id, 0, delay.Id, 0);
        _graph.Connect(delay.Id, 0, reverb.Id, 0);
        _graph.Connect(reverb.Id, 0, output.Id, 0);

        // Atmospheric delay/reverb
        SetParam(delay, "Time", 500f);
        SetParam(delay, "Feedback", 0.4f);
        SetParam(delay, "Mix", 0.3f);
        SetParam(reverb, "Size", 0.8f);
        SetParam(reverb, "Mix", 0.4f);
    }

    private void SetParam(IEffectNode node, string name, float value)
    {
        var param = node.Parameters.FirstOrDefault(p => p.Name == name);
        if (param != null) param.Value = value;
    }

    public void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        _graph.Process(buffer, sampleCount, sampleRate);
    }
}
```

## Creating Custom Nodes

### Custom Effect Node

```csharp
public class WaveFolderNode : EffectNodeBase
{
    public override string NodeType => "WaveFolder";
    public override string Category => "Effects";
    public override string Description => "Wave folding distortion";

    protected override void InitializePorts()
    {
        AddInput("In", PortDataType.Audio);
        AddInput("Fold CV", PortDataType.Control);
        AddOutput("Out", PortDataType.Audio);
    }

    protected override void InitializeParameters()
    {
        AddParameter("Folds", 2f, 1f, 8f, "x");
        AddParameter("Symmetry", 0.5f, 0f, 1f, "");
        AddParameter("Mix", 1f, 0f, 1f, "");
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        float folds = GetParam("Folds") + GetInput(1) * 4f;
        float symmetry = GetParam("Symmetry");
        float mix = GetParam("Mix");

        for (int i = 0; i < sampleCount; i++)
        {
            float input = buffer[i] + GetInput(0);

            // Apply asymmetry
            float asymInput = input + (symmetry - 0.5f) * 2f;

            // Wave folding algorithm
            float folded = asymInput * folds;
            while (folded > 1f || folded < -1f)
            {
                if (folded > 1f)
                    folded = 2f - folded;
                else if (folded < -1f)
                    folded = -2f - folded;
            }

            buffer[i] = input * (1f - mix) + folded * mix;
        }

        SetOutput(0, buffer[sampleCount - 1]);
    }
}

// Register the custom node
EffectNodeFactory.RegisterNodeType(
    "WaveFolder",
    "Wave Folder",
    "Effects",
    "Wave folding distortion effect",
    () => new WaveFolderNode()
);
```

## Patch Management

### Save Patch

```csharp
var patchManager = EffectPatchManager.Instance;
patchManager.SetPatchDirectory("C:/MusicEngine/Patches");

// Create patch from graph
var patch = patchManager.CreatePatchFromGraph(graph, "Synths");
patch.Author = "Developer Name";
patch.Tags.Add("bass");
patch.Tags.Add("analog");

// Save to file
await patchManager.SavePatchAsync(patch);
// Saves to: C:/MusicEngine/Patches/Synths/PatchName.mepatch
```

### Load Patch

```csharp
// Load all patches from directory
await patchManager.LoadPatchesAsync();

// Get patches by category
var synthPatches = patchManager.GetPatchesByCategory("Synths");

// Search by tag
var bassPatches = patchManager.SearchPatches("bass");

// Load specific patch
var patch = synthPatches.First();
var graph = patchManager.CreateGraphFromPatch(patch);
```

### Patch File Format

```json
{
  "name": "Fat Bass",
  "category": "Synths",
  "author": "Developer",
  "description": "Thick analog-style bass",
  "tags": ["bass", "analog", "subtractive"],
  "version": "1.0",
  "created": "2026-01-27T12:00:00Z",
  "nodes": [
    {
      "id": "node-1",
      "type": "Oscillator",
      "displayName": "Osc 1",
      "parameters": {
        "Frequency": 220,
        "Waveform": 1,
        "PulseWidth": 0.5
      }
    }
  ],
  "connections": [
    {
      "source": "node-1",
      "sourcePort": 0,
      "target": "node-2",
      "targetPort": 0,
      "amount": 1.0
    }
  ]
}
```

## Integration with Game Engines

```csharp
// Create effect graph
var reverbGraph = new EffectGraph { Name = "Game Reverb" };
var input = EffectNodeFactory.CreateNode("AudioInput");
var reverb = EffectNodeFactory.CreateNode("Reverb");
var output = EffectNodeFactory.CreateNode("AudioOutput");

reverbGraph.AddNode(input);
reverbGraph.AddNode(reverb);
reverbGraph.AddNode(output);

reverbGraph.Connect(input.Id, 0, reverb.Id, 0);
reverbGraph.Connect(reverb.Id, 0, output.Id, 0);

// Connect to ExternalControlService for game control
var control = ExternalControlService.Instance;

control.RegisterVariable("ReverbSize", VariableType.Float, 0.5f, value =>
{
    var reverbNode = reverbGraph.Nodes.Values.First(n => n.NodeType == "Reverb");
    reverbNode.Parameters.First(p => p.Name == "Size").Value = (float)value;
});

control.RegisterVariable("ReverbMix", VariableType.Float, 0.3f, value =>
{
    var reverbNode = reverbGraph.Nodes.Values.First(n => n.NodeType == "Reverb");
    reverbNode.Parameters.First(p => p.Name == "Mix").Value = (float)value;
});

// In game: adjust reverb based on environment
control.SetVariable("ReverbSize", 0.9f);  // Large cave
control.SetVariable("ReverbMix", 0.5f);
```

## Performance Tips

1. **Minimize Node Count**: Combine simple operations when possible
2. **Use Appropriate Buffer Sizes**: 256-1024 samples for real-time
3. **Disable Unused Nodes**: Set `IsEnabled = false`
4. **Avoid Creating Nodes During Playback**: Create all nodes at initialization
5. **Use Factory Presets**: Load pre-built patches instead of building at runtime
