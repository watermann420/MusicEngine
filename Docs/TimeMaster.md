# Time Master Controller

TimeMasterController is a global time driver that can control patterns, audio decks, and samplers. It supports scrubbing (scratch) and jog-wheel mapping.

## Basic Usage

```csharp
var time = CreateTimeMaster();
time.Start();
```

## Bind Targets

```csharp
var time = CreateTimeMaster();

time.BindPattern(pattern);
time.BindDeck(deckA);
time.BindSampler(sampler);
```

## Jog Wheel Scratch (Ticks)

```csharp
var time = CreateTimeMaster();
time.JogTicksPerRevolution = 1024;
time.JogSecondsPerRevolution = 1.0;
time.ScratchScale = 1.0;
time.MaxScratchSeconds = 2.0;

midi.device(0).jog(16, JogMode.RelativeSigned, scale: 1)
    .to(delta => time.ScratchTicks(delta));
```

## Direct Scrub

```csharp
time.ScratchSeconds(0.02);
time.ScratchSeconds(-0.05);
```

## Speed + Loop

```csharp
time.Speed = 1.0;
time.LoopEnabled = true;
time.LoopStartSeconds = 0.0;
time.LoopEndSeconds = 4.0;
```

## Randomize

```csharp
time.SetRandomSeed(1234);
time.Randomize(0.2);
```

## Case-Insensitive Syntax (Optional)

```csharp
var time = Music.CREATETIMEMASTER();
Music.MIDI.DEVICE(0).JOG(16).TO(d => time.SCRATCHTICKS(d));
```
