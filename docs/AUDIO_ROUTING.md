# Audio Routing Guide

This document explains the audio routing system in MusicEngine, including buses, channels, sends/returns, and side-chain compression.

## Table of Contents

1. [Overview](#overview)
2. [Audio Channels](#audio-channels)
3. [Audio Buses](#audio-buses)
4. [Send/Return Effects](#sendreturn-effects)
5. [Side-Chain Routing](#side-chain-routing)
6. [Practical Examples](#practical-examples)

---

## Overview

MusicEngine's routing system provides flexible signal flow for complex mixes:

```
[Instruments] → [Channels] → [Buses] → [Master]
                    ↓           ↓
                [Effects]  [Send/Return]
```

**Key Concepts:**
- **AudioChannel**: Individual track with volume, pan, mute, solo, and insert effects
- **AudioBus**: Mixing point for grouping channels with its own effects
- **Send/Return**: Parallel routing for effects (e.g., shared reverb)
- **Side-Chain**: Using one signal to control processing of another

---

## Audio Channels

`AudioChannel` wraps any audio source (synth, sample, etc.) with:
- Volume control (0.0 - 2.0)
- Pan control (-1.0 left, 0.0 center, 1.0 right)
- Mute/Solo states
- Fader automation
- Insert effects chain

### Creating Channels

```csharp
var synth = CreateSynth("bass");

// Create channel
var bassChannel = new AudioChannel("bass-ch", synth);

// Configure channel
bassChannel.Volume = 0.8f;
bassChannel.Pan = -0.2f;  // Slightly left

// Add insert effects
var eq = fx.EQ("bass-eq", bassChannel)
    .Low(80, 6, 0.707)
    .Build();

bassChannel.AddInsertEffect(eq);

var comp = fx.Compressor("bass-comp", bassChannel)
    .Threshold(-20)
    .Ratio(4)
    .Build();

bassChannel.AddInsertEffect(comp);
```

### Signal Flow in Channels

```
Input → Insert Effect 1 → Insert Effect 2 → ... → Volume/Pan → Output
```

Insert effects are processed in series, in the order they were added.

### Channel Controls

```csharp
// Volume
channel.Volume = 0.5f;  // 50% volume
channel.Volume = 2.0f;  // 200% volume (boost)

// Pan (stereo only)
channel.Pan = -1.0f;  // Full left
channel.Pan = 0.0f;   // Center
channel.Pan = 1.0f;   // Full right

// Mute/Solo
channel.Mute = true;   // Silence this channel
channel.Solo = true;   // Solo this channel (mutes others)

// Fader (automation)
channel.Fader = 0.0f;  // Fade out
channel.Fader = 1.0f;  // Normal
```

---

## Audio Buses

`AudioBus` is a submix that can:
- Mix multiple inputs
- Apply insert effects to the group
- Send audio to other buses (parallel routing)
- Be routed to other buses (cascading)

### Creating Buses

```csharp
// Create drum bus
var drumBus = new AudioBus("drums", waveFormat);

// Add inputs
drumBus.AddInput(kickChannel);
drumBus.AddInput(snareChannel);
drumBus.AddInput(hihatChannel);

// Configure bus
drumBus.Volume = 0.9f;
drumBus.Pan = 0.0f;

// Add bus-level effects
var drumComp = fx.Compressor("drum-comp", drumBus)
    .Threshold(-20)
    .Ratio(4)
    .Build();

drumBus.AddInsertEffect(drumComp);
```

### Bus Signal Flow

```
Input 1 ┐
Input 2 ├→ Sum → Insert Effects → Volume/Pan → Sends → Output
Input 3 ┘
```

### Nested Buses (Submixes)

```csharp
// Create instrument group buses
var drumBus = new AudioBus("drums", waveFormat);
var synthBus = new AudioBus("synths", waveFormat);
var vocalBus = new AudioBus("vocals", waveFormat);

// Create master bus
var masterBus = new AudioBus("master", waveFormat);

// Route group buses to master
masterBus.AddInput(drumBus);
masterBus.AddInput(synthBus);
masterBus.AddInput(vocalBus);

// Add master chain
var masterEQ = fx.EQ("master-eq", masterBus)
    .Low(60, -3, 0.707)
    .Build();

var masterLimiter = fx.Limiter("master-limiter", masterBus)
    .Ceiling(-0.3)
    .Build();

masterBus.AddInsertEffect(masterEQ);
masterBus.AddInsertEffect(masterLimiter);
```

---

## Send/Return Effects

Send/return routing allows multiple channels to share a single effect processor (e.g., one reverb for all vocals).

### Setup

```csharp
// Create a return bus for reverb
var reverbBus = new AudioBus("reverb-return", waveFormat);

// Add reverb to return bus
var reverb = fx.Reverb("main-reverb", reverbBus)
    .RoomSize(0.7)
    .DryWet(1.0)  // 100% wet (no dry signal)
    .Build();

reverbBus.AddInsertEffect(reverb);

// Send vocal channel to reverb bus
vocalChannel.AddSend(reverbBus, 0.4f);  // 40% send level

// Send synth channel to same reverb
synthChannel.AddSend(reverbBus, 0.2f);  // 20% send level

// Route reverb bus to master
masterBus.AddInput(reverbBus);
```

### Send/Return Benefits

1. **CPU Efficiency**: One reverb instance instead of multiple
2. **Cohesion**: All instruments in the same acoustic space
3. **Control**: Adjust overall reverb level at the return bus

### Multiple Returns Example

```csharp
// Create multiple return buses
var reverbBus = new AudioBus("reverb", waveFormat);
var delayBus = new AudioBus("delay", waveFormat);
var chorusBus = new AudioBus("chorus", waveFormat);

// Add effects (100% wet)
reverbBus.AddInsertEffect(fx.Reverb("reverb", reverbBus).DryWet(1.0).Build());
delayBus.AddInsertEffect(fx.Delay("delay", delayBus).DryWet(1.0).Build());
chorusBus.AddInsertEffect(fx.Chorus("chorus", chorusBus).DryWet(1.0).Build());

// Send vocal to multiple effects
vocalChannel.AddSend(reverbBus, 0.3f);
vocalChannel.AddSend(delayBus, 0.15f);
vocalChannel.AddSend(chorusBus, 0.1f);

// Route all returns to master
masterBus.AddInput(reverbBus);
masterBus.AddInput(delayBus);
masterBus.AddInput(chorusBus);
```

---

## Side-Chain Routing

Side-chain compression uses one signal to control processing of another. Common uses:
- **Ducking**: Lower music volume when vocals play
- **Pumping**: Rhythmic compression driven by kick drum

### Side-Chain Ducking Example

```csharp
var music = CreateSynth("music");
var vocal = CreateSynth("vocal");

// Compress music based on vocal level
var sidechain = fx.SideChainCompressor("ducking", music)
    .SideChain(vocal)      // Vocal triggers compression
    .Threshold(-20)        // When vocal is above -20dB
    .Ratio(6)              // Compress music 6:1
    .Attack(0.01)          // Fast attack
    .Release(0.3)          // Smooth release
    .DryWet(1.0)
    .Build();

// Now music automatically ducks when vocal plays
```

### Side-Chain Pumping Example

```csharp
var kick = CreateSampleInstrument("kick");
var synth = CreateSynth("pad");

// Pump synth with kick rhythm
var pumping = fx.SideChainCompressor("pump", synth)
    .SideChain(kick)       // Kick triggers compression
    .Threshold(-30)        // Sensitive threshold
    .Ratio(10)             // Heavy compression
    .Attack(0.001)         // Instant attack
    .Release(0.15)         // Quick release
    .DryWet(1.0)
    .Build();

// Synth now "pumps" with each kick hit
```

### Side-Chain Parameters

- **Threshold**: How loud side-chain must be to trigger compression
- **Ratio**: How much compression to apply (higher = more ducking/pumping)
- **Attack**: How fast compression engages (fast = tight response)
- **Release**: How fast compression disengages (fast = quick recovery, slow = smooth)
- **SideChainGain**: Amplifies side-chain signal for more/less sensitivity

---

## Practical Examples

### Complete Mixing Setup

```csharp
// ===== CREATE INSTRUMENTS =====
var kick = CreateSampleInstrument("kick");
var snare = CreateSampleInstrument("snare");
var hihat = CreateSampleInstrument("hihat");
var bass = CreateSynth("bass");
var lead = CreateSynth("lead");
var pad = CreateSynth("pad");
var vocal = CreateSynth("vocal");

// ===== CREATE CHANNELS =====
var kickChannel = new AudioChannel("kick-ch", kick);
var snareChannel = new AudioChannel("snare-ch", snare);
var hihatChannel = new AudioChannel("hihat-ch", hihat);
var bassChannel = new AudioChannel("bass-ch", bass);
var leadChannel = new AudioChannel("lead-ch", lead);
var padChannel = new AudioChannel("pad-ch", pad);
var vocalChannel = new AudioChannel("vocal-ch", vocal);

// ===== CONFIGURE CHANNELS =====
kickChannel.Volume = 1.0f;
snareChannel.Volume = 0.9f;
hihatChannel.Volume = 0.7f;
bassChannel.Volume = 0.8f;
bassChannel.Pan = -0.1f;
leadChannel.Volume = 0.7f;
leadChannel.Pan = 0.2f;
padChannel.Volume = 0.5f;
vocalChannel.Volume = 0.9f;

// ===== CREATE GROUP BUSES =====
var drumBus = new AudioBus("drums", waveFormat);
drumBus.AddInput(kickChannel);
drumBus.AddInput(snareChannel);
drumBus.AddInput(hihatChannel);

var synthBus = new AudioBus("synths", waveFormat);
synthBus.AddInput(bassChannel);
synthBus.AddInput(leadChannel);
synthBus.AddInput(padChannel);

// ===== ADD BUS COMPRESSION =====
var drumComp = fx.Compressor("drum-comp", drumBus)
    .Threshold(-18)
    .Ratio(4)
    .Attack(0.005)
    .Release(0.1)
    .Build();
drumBus.AddInsertEffect(drumComp);

var synthComp = fx.Compressor("synth-comp", synthBus)
    .Threshold(-15)
    .Ratio(3)
    .Attack(0.01)
    .Release(0.15)
    .Build();
synthBus.AddInsertEffect(synthComp);

// ===== CREATE SEND/RETURN BUSES =====
var reverbBus = new AudioBus("reverb", waveFormat);
var delayBus = new AudioBus("delay", waveFormat);

var reverb = fx.Reverb("main-reverb", reverbBus)
    .RoomSize(0.7)
    .Damping(0.5)
    .DryWet(1.0)
    .Build();
reverbBus.AddInsertEffect(reverb);

var delay = fx.Delay("main-delay", delayBus)
    .Time(0.375)
    .Feedback(0.4)
    .PingPong(0.8)
    .DryWet(1.0)
    .Build();
delayBus.AddInsertEffect(delay);

// ===== CONFIGURE SENDS =====
vocalChannel.AddSend(reverbBus, 0.4f);
vocalChannel.AddSend(delayBus, 0.2f);
leadChannel.AddSend(reverbBus, 0.3f);
leadChannel.AddSend(delayBus, 0.15f);
padChannel.AddSend(reverbBus, 0.5f);
snareChannel.AddSend(reverbBus, 0.2f);

// ===== CREATE MASTER BUS =====
var masterBus = new AudioBus("master", waveFormat);
masterBus.AddInput(drumBus);
masterBus.AddInput(synthBus);
masterBus.AddInput(vocalChannel);
masterBus.AddInput(reverbBus);
masterBus.AddInput(delayBus);

// ===== MASTER CHAIN =====
var masterEQ = fx.EQ("master-eq", masterBus)
    .Low(60, -3, 0.707)
    .High(12000, 2, 0.707)
    .Build();
masterBus.AddInsertEffect(masterEQ);

var masterComp = fx.Compressor("master-comp", masterBus)
    .Threshold(-10)
    .Ratio(2)
    .Attack(0.02)
    .Release(0.2)
    .Knee(6)
    .AutoGain(1.0)
    .Build();
masterBus.AddInsertEffect(masterComp);

var masterLimiter = fx.Limiter("master-limiter", masterBus)
    .Ceiling(-0.3)
    .Release(0.05)
    .Lookahead(0.005)
    .Build();
masterBus.AddInsertEffect(masterLimiter);

// ===== SIDE-CHAIN DUCKING =====
var duckSynths = fx.SideChainCompressor("vocal-duck", synthBus)
    .SideChain(vocalChannel)
    .Threshold(-20)
    .Ratio(4)
    .Attack(0.01)
    .Release(0.3)
    .Build();
synthBus.AddInsertEffect(duckSynths);

// ===== OUTPUT TO ENGINE =====
Engine.AddSampleProvider(masterBus);
```

### Dynamic Effects (Automation Example)

```csharp
// Fade in a channel over time
float fadeTime = 2.0f;  // 2 seconds
float elapsed = 0f;

while (elapsed < fadeTime)
{
    channel.Fader = elapsed / fadeTime;  // 0.0 → 1.0
    await Task.Delay(50);  // Update every 50ms
    elapsed += 0.05f;
}

channel.Fader = 1.0f;  // Fully faded in
```

### Solo/Mute Workflow

```csharp
// Solo a channel (mutes all others)
leadChannel.Solo = true;

// Mute specific channels
bassChannel.Mute = true;
drumBus.Mute = true;

// Un-solo
leadChannel.Solo = false;

// Un-mute
bassChannel.Mute = false;
drumBus.Mute = false;
```

---

## Routing Best Practices

### 1. **Use Buses for Grouping**
Group similar instruments (drums, synths, vocals) on buses for:
- Easier mixing (control entire group at once)
- Group-level processing (compress all drums together)
- CPU efficiency

### 2. **Send/Return for Shared Effects**
Use send/return buses for:
- Reverb (shared acoustic space)
- Delay (consistent timing)
- Chorus (ensemble effect)

Avoid for:
- Dynamics (compressor, gate, limiter)
- EQ (unless creative effect)
- Distortion (unless parallel processing)

### 3. **Insert Effects Order**
Common insert effect order:
1. Gate/Expander
2. EQ (corrective)
3. Compressor
4. EQ (creative)
5. Saturation/Distortion
6. Modulation (chorus, flanger, phaser)

### 4. **Master Chain**
Typical master bus processing:
1. Corrective EQ (remove mud, harshness)
2. Multiband compression (optional)
3. Gentle compression (glue)
4. Stereo widening (optional)
5. Limiter (prevent clipping)

### 5. **Side-Chain Uses**
- **Ducking**: Lower backing tracks when vocals/lead plays
- **Pumping**: Rhythmic compression for dance music
- **De-essing**: Compress only high frequencies when sibilants detected
- **Frequency masking**: Duck bass when kick hits

### 6. **CPU Management**
- One reverb on return bus > reverb on every channel
- Freeze/render buses that don't change
- Use simpler effects where possible (FilterEffect vs ParametricEQEffect)

---

## Signal Flow Diagrams

### Simple Routing
```
[Synth] → [Channel] → [Master] → [Output]
            ↓
        [Effects]
```

### Bus Routing
```
[Synth 1] → [Channel 1] ┐
[Synth 2] → [Channel 2] ├→ [Bus] → [Master] → [Output]
[Synth 3] → [Channel 3] ┘            ↓
                                 [Effects]
```

### Send/Return
```
[Synth] → [Channel] → [Master] → [Output]
            ↓              ↑
          [Send] → [Return Bus]
                       ↓
                   [Effect]
```

### Side-Chain
```
[Kick] ────────────┐
                   ↓ (triggers)
[Bass] → [SideChain Compressor] → [Output]
```

---

## Troubleshooting

### No Sound
- Check channel/bus mute states
- Check volume levels (not set to 0)
- Verify inputs are connected to buses
- Check master bus is routed to engine

### Clipping/Distortion
- Reduce channel volumes
- Add limiter to master bus
- Check for feedback loops in sends
- Reduce effect drive/gain parameters

### Phase Issues
- Check pan settings (extreme panning can cause mono collapse)
- Verify stereo width settings on reverb/delay
- Check for inverted polarity

### CPU Overload
- Reduce number of effect instances
- Use send/return for shared effects
- Simplify effect chains
- Lower sample rate if possible
