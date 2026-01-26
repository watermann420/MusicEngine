# Audio Effects Reference

This document provides a complete reference for all audio effects available in MusicEngine.

## Table of Contents

1. [Filter Effects](#filter-effects)
2. [Dynamics Effects](#dynamics-effects)
3. [Modulation Effects](#modulation-effects)
4. [Time-Based Effects](#time-based-effects)
5. [Distortion Effects](#distortion-effects)

---

## Filter Effects

### FilterEffect

State-variable filter with multiple filter types using the Chamberlin algorithm.

**Parameters:**
- `Cutoff` (20 - 20000 Hz): Filter cutoff frequency
- `Resonance` (0.1 - 10.0): Filter resonance/Q factor
- `Type`: Filter type (Lowpass, Highpass, Bandpass, Bandreject, Allpass)
- `DryWet` (0.0 - 1.0): Effect mix

**Example:**
```csharp
var synth = CreateSynth("bass");

// Create lowpass filter
var filter = fx.Filter("bass-filter", synth)
    .Cutoff(500)
    .Resonance(2.0)
    .Type(FilterType.Lowpass)
    .DryWet(1.0)
    .Build();
```

### ParametricEQEffect

3-band parametric equalizer with independent control over low, mid, and high frequencies.

**Parameters:**
- **Low Band:**
  - `LowFrequency` (20 - 500 Hz): Center frequency
  - `LowGain` (-24 to +24 dB): Boost/cut amount
  - `LowQ` (0.1 - 10.0): Bandwidth
- **Mid Band:**
  - `MidFrequency` (200 - 5000 Hz): Center frequency
  - `MidGain` (-24 to +24 dB): Boost/cut amount
  - `MidQ` (0.1 - 10.0): Bandwidth
- **High Band:**
  - `HighFrequency` (2000 - 20000 Hz): Center frequency
  - `HighGain` (-24 to +24 dB): Boost/cut amount
  - `HighQ` (0.1 - 10.0): Bandwidth
- `DryWet` (0.0 - 1.0): Effect mix

**Example:**
```csharp
var synth = CreateSynth("vocal");

// Create EQ with bass boost and treble cut
var eq = fx.EQ("vocal-eq", synth)
    .Low(100, 6, 0.707)     // Boost bass by 6dB at 100Hz
    .Mid(1000, 0, 0.707)    // No change to mids
    .High(8000, -3, 0.707)  // Cut treble by 3dB at 8kHz
    .DryWet(1.0)
    .Build();
```

---

## Dynamics Effects

### CompressorEffect

Dynamic range compressor with soft knee and auto makeup gain.

**Parameters:**
- `Threshold` (-60 to 0 dB): Compression threshold
- `Ratio` (1.0 - 20.0): Compression ratio (4:1, 10:1, etc.)
- `Attack` (0.0001 - 1.0 s): How fast compression engages
- `Release` (0.001 - 5.0 s): How fast compression disengages
- `MakeupGain` (0 - 48 dB): Compensates for volume loss
- `KneeWidth` (0 - 20 dB): Soft knee for smooth transition
- `AutoGain` (0.0 - 1.0): Automatic makeup gain calculation
- `DryWet` (0.0 - 1.0): Effect mix

**Example:**
```csharp
var drums = CreateSampleInstrument("drums");

// Create drum compressor
var comp = fx.Compressor("drum-comp", drums)
    .Threshold(-20)
    .Ratio(4)
    .Attack(0.005)
    .Release(0.1)
    .MakeupGain(6)
    .Knee(6)
    .DryWet(1.0)
    .Build();
```

### LimiterEffect

Brick-wall limiter with lookahead to prevent clipping.

**Parameters:**
- `Ceiling` (-12 to 0 dB): Maximum output level
- `Release` (0.001 - 1.0 s): Release time
- `Lookahead` (0 - 0.05 s): Lookahead time for peak detection
- `DryWet` (0.0 - 1.0): Effect mix

**Example:**
```csharp
var master = GetMasterOutput();

// Create master limiter
var limiter = fx.Limiter("master-limiter", master)
    .Ceiling(-0.3)
    .Release(0.05)
    .Lookahead(0.005)
    .DryWet(1.0)
    .Build();
```

### GateEffect

Noise gate attenuates signals below a threshold.

**Parameters:**
- `Threshold` (-80 to 0 dB): Gate threshold
- `Ratio` (1.0 - 100.0): Gating ratio
- `Attack` (0.0001 - 0.1 s): How fast gate opens
- `Hold` (0.001 - 2.0 s): Minimum time gate stays open
- `Release` (0.001 - 5.0 s): How fast gate closes
- `Range` (-80 to 0 dB): Maximum attenuation when closed
- `DryWet` (0.0 - 1.0): Effect mix

**Example:**
```csharp
var vocal = CreateSynth("vocal");

// Create noise gate for vocal
var gate = fx.Gate("vocal-gate", vocal)
    .Threshold(-40)
    .Ratio(10)
    .Attack(0.001)
    .Hold(0.05)
    .Release(0.2)
    .Range(-60)
    .DryWet(1.0)
    .Build();
```

### SideChainCompressorEffect

Compressor controlled by an external side-chain signal (e.g., ducking).

**Parameters:**
- `Threshold` (-60 to 0 dB): Side-chain trigger threshold
- `Ratio` (1.0 - 20.0): Compression ratio
- `Attack` (0.0001 - 1.0 s): Attack time
- `Release` (0.001 - 5.0 s): Release time
- `MakeupGain` (0 - 48 dB): Makeup gain
- `SideChainGain` (0.1 - 10.0): Side-chain sensitivity
- `DryWet` (0.0 - 1.0): Effect mix

**Example:**
```csharp
var music = CreateSynth("music");
var vocal = CreateSynth("vocal");

// Duck music when vocal plays
var sidechain = fx.SideChainCompressor("ducking", music)
    .SideChain(vocal)
    .Threshold(-20)
    .Ratio(6)
    .Attack(0.01)
    .Release(0.2)
    .DryWet(1.0)
    .Build();
```

---

## Modulation Effects

### ChorusEffect

Multi-voice chorus with stereo spreading.

**Parameters:**
- `Rate` (0.01 - 5.0 Hz): LFO modulation rate
- `Depth` (0.001 - 0.01 s): Modulation depth
- `BaseDelay` (0.01 - 0.05 s): Base delay time
- `Voices` (1 - 4): Number of chorus voices
- `Spread` (0.0 - 1.0): Stereo/frequency spread
- `Feedback` (0.0 - 0.5): Feedback amount
- `DryWet` (0.0 - 1.0): Effect mix

**Example:**
```csharp
var synth = CreateSynth("lead");

// Create rich chorus
var chorus = fx.Chorus("lead-chorus", synth)
    .Rate(0.8)
    .Depth(0.003)
    .BaseDelay(0.02)
    .Voices(3)
    .Spread(0.5)
    .Feedback(0.2)
    .DryWet(0.5)
    .Build();
```

### FlangerEffect

Sweeping jet-like effect.

**Parameters:**
- `Rate` (0.01 - 10.0 Hz): LFO rate
- `Depth` (0.0001 - 0.01 s): Modulation depth
- `Feedback` (0.0 - 0.95): Feedback for intensity
- `BaseDelay` (0.001 - 0.01 s): Center delay time
- `Stereo` (0.0 - 1.0): Stereo phase offset
- `DryWet` (0.0 - 1.0): Effect mix

**Example:**
```csharp
var synth = CreateSynth("fx");

// Create dramatic flanger
var flanger = fx.Flanger("fx-flanger", synth)
    .Rate(0.3)
    .Depth(0.005)
    .Feedback(0.7)
    .BaseDelay(0.003)
    .Stereo(0.5)
    .DryWet(0.5)
    .Build();
```

### PhaserEffect

Sweeping phase-shift effect using allpass filters.

**Parameters:**
- `Rate` (0.01 - 10.0 Hz): LFO rate
- `Depth` (0.0 - 1.0): Modulation depth
- `Feedback` (0.0 - 0.95): Feedback amount
- `MinFrequency` (20 - 5000 Hz): Lower sweep bound
- `MaxFrequency` (100 - 10000 Hz): Upper sweep bound
- `Stages` (2 - 12): Number of allpass stages
- `Stereo` (0.0 - 1.0): Stereo phase offset
- `DryWet` (0.0 - 1.0): Effect mix

**Example:**
```csharp
var synth = CreateSynth("pad");

// Create lush phaser
var phaser = fx.Phaser("pad-phaser", synth)
    .Rate(0.5)
    .Depth(1.0)
    .Feedback(0.7)
    .MinFrequency(200)
    .MaxFrequency(2000)
    .Stages(6)
    .Stereo(0.5)
    .DryWet(0.5)
    .Build();
```

### TremoloEffect

Amplitude modulation for rhythmic volume changes.

**Parameters:**
- `Rate` (0.1 - 20.0 Hz): Modulation rate
- `Depth` (0.0 - 1.0): Modulation depth
- `Waveform` (0-2): LFO waveform (0=sine, 1=triangle, 2=square)
- `Stereo` (0.0 - 1.0): Stereo phase offset
- `DryWet` (0.0 - 1.0): Effect mix

**Example:**
```csharp
var synth = CreateSynth("organ");

// Create tremolo effect
var tremolo = fx.Tremolo("organ-tremolo", synth)
    .Rate(5)
    .Depth(0.5)
    .Waveform(0)  // Sine wave
    .Stereo(0)
    .DryWet(1.0)
    .Build();
```

### VibratoEffect

Pitch modulation for wavering pitch.

**Parameters:**
- `Rate` (1.0 - 14.0 Hz): Modulation rate
- `Depth` (0.0001 - 0.005 s): Pitch variation amount
- `BaseDelay` (0.001 - 0.01 s): Center delay time
- `Waveform` (0-1): LFO waveform (0=sine, 1=triangle)
- `DryWet` (0.0 - 1.0): Effect mix

**Example:**
```csharp
var synth = CreateSynth("string");

// Create subtle vibrato
var vibrato = fx.Vibrato("string-vibrato", synth)
    .Rate(5)
    .Depth(0.002)
    .BaseDelay(0.003)
    .Waveform(0)
    .DryWet(1.0)
    .Build();
```

---

## Time-Based Effects

### EnhancedDelayEffect

Stereo delay with ping-pong, cross-feedback, and damping.

**Parameters:**
- `DelayTime` (0.001 - 10.0 s): Delay time
- `Feedback` (0.0 - 0.95): Feedback amount
- `CrossFeedback` (0.0 - 0.95): Cross-channel feedback (stereo)
- `Damping` (0.0 - 1.0): Lowpass filtering in feedback path
- `StereoSpread` (0.0 - 1.0): Delay time offset between channels
- `PingPong` (0.0 - 1.0): Ping-pong mode strength
- `DryWet` (0.0 - 1.0): Effect mix

**Example:**
```csharp
var synth = CreateSynth("lead");

// Create ping-pong delay
var delay = fx.Delay("lead-delay", synth)
    .Time(0.375)         // Dotted eighth note at 120 BPM
    .Feedback(0.5)
    .CrossFeedback(0.3)
    .Damping(0.5)
    .StereoSpread(0.2)
    .PingPong(0.8)
    .DryWet(0.4)
    .Build();
```

### EnhancedReverbEffect

Algorithmic reverb with early reflections and late reverb tail.

**Parameters:**
- `RoomSize` (0.0 - 1.0): Virtual room size
- `Damping` (0.0 - 1.0): High-frequency absorption
- `Width` (0.0 - 1.0): Stereo width
- `EarlyLevel` (0.0 - 1.0): Early reflections level
- `LateLevel` (0.0 - 1.0): Late reverb tail level
- `Predelay` (0.0 - 0.1 s): Delay before reverb begins
- `DryWet` (0.0 - 1.0): Effect mix

**Example:**
```csharp
var drums = CreateSampleInstrument("drums");

// Create room reverb
var reverb = fx.Reverb("drum-reverb", drums)
    .RoomSize(0.5)
    .Damping(0.5)
    .Width(1.0)
    .EarlyLevel(0.3)
    .LateLevel(0.7)
    .Predelay(0.0)
    .DryWet(0.3)
    .Build();
```

---

## Distortion Effects

### DistortionEffect

Multi-algorithm distortion with tone control.

**Parameters:**
- `Drive` (1.0 - 100.0): Pre-gain amount
- `Tone` (0.0 - 1.0): Tone control (0=dark, 1=bright)
- `OutputGain` (0.0 - 1.0): Post-gain compensation
- `Type`: Distortion algorithm (HardClip, SoftClip, Overdrive, Fuzz, Waveshaper)
- `DryWet` (0.0 - 1.0): Effect mix

**Example:**
```csharp
var synth = CreateSynth("bass");

// Create overdrive distortion
var dist = fx.Distortion("bass-dist", synth)
    .Drive(10)
    .Tone(0.6)
    .OutputGain(0.5)
    .Type(DistortionType.Overdrive)
    .DryWet(1.0)
    .Build();
```

### BitcrusherEffect

Lo-fi effect reducing bit depth and sample rate.

**Parameters:**
- `BitDepth` (1 - 16): Bit resolution (lower=more distortion)
- `SampleRate` (100 - 48000 Hz): Target sample rate (lower=more aliasing)
- `DryWet` (0.0 - 1.0): Effect mix

**Example:**
```csharp
var synth = CreateSynth("lead");

// Create lo-fi bitcrusher
var crusher = fx.Bitcrusher("lead-crush", synth)
    .BitDepth(8)
    .SampleRate(8000)
    .DryWet(0.7)
    .Build();
```

---

## Common Usage Patterns

### Effect Chaining

```csharp
var synth = CreateSynth("pad");

// Chain multiple effects
var filtered = fx.Filter("filter", synth)
    .Cutoff(1000)
    .Resonance(2)
    .Type(FilterType.Lowpass)
    .Build();

var chorused = fx.Chorus("chorus", filtered)
    .Rate(0.8)
    .Voices(3)
    .Build();

var reverbed = fx.Reverb("reverb", chorused)
    .RoomSize(0.7)
    .DryWet(0.4)
    .Build();
```

### Parallel Effects (Dry/Wet Mix)

```csharp
var synth = CreateSynth("vocal");

// Add reverb with 30% wet mix
var reverb = fx.Reverb("vocal-reverb", synth)
    .RoomSize(0.6)
    .DryWet(0.3)  // 30% reverb, 70% dry
    .Build();
```

### Dynamic Effects

```csharp
var drums = CreateSampleInstrument("drums");

// Compress drums
var comp = fx.Compressor("drum-comp", drums)
    .Threshold(-20)
    .Ratio(4)
    .Attack(0.005)
    .Release(0.1)
    .Build();

// Then limit to prevent clipping
var limited = fx.Limiter("drum-limiter", comp)
    .Ceiling(-0.5)
    .Build();
```

---

## DSP Notes

### Filter Types
- **Lowpass**: Passes frequencies below cutoff, attenuates higher frequencies
- **Highpass**: Passes frequencies above cutoff, attenuates lower frequencies
- **Bandpass**: Passes frequencies around cutoff, attenuates both sides
- **Bandreject**: Rejects frequencies around cutoff (notch filter)
- **Allpass**: Passes all frequencies but changes phase relationships

### Compression Ratio Guide
- **1:1** - No compression
- **2:1** - Gentle compression (vocals, mastering)
- **4:1** - Moderate compression (drums, bass)
- **8:1** - Heavy compression (limiting)
- **20:1** - Brick-wall limiting

### Delay Time Calculation
```
Dotted quarter = 60 / BPM * 1.5
Quarter note = 60 / BPM
Eighth note = 60 / BPM / 2
Dotted eighth = 60 / BPM / 2 * 1.5
```

Example at 120 BPM:
- Quarter = 0.5s
- Eighth = 0.25s
- Dotted eighth = 0.375s
