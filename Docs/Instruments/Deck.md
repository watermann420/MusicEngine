# Audio Deck

AudioDeck is a sample-accurate player for audio files. It supports looping, scrubbing (scratch), and playback speed changes.

## Basic Usage

```csharp
var deckA = CreateDeck("DeckA");
deckA.Load("Samples/Loops/beat.wav");
deckA.Loop = true;
deckA.IsPlaying = true;
```

## Routing + Mixer

```csharp
var deckA = CreateDeck("DeckA");
deckA.Load("Samples/Loops/beat.wav");

var ch1 = audio.CreateChannel(1);
ch1.Route(deckA);
```

## Scratch / Scrub

```csharp
deckA.ScratchSeconds(0.01);  // push forward
deckA.ScratchSeconds(-0.02); // pull back
```

## Jog Wheel Mapping

```csharp
var time = CreateTimeMaster();
time.BindDeck(deckA);
time.JogTicksPerRevolution = 1024;
time.JogSecondsPerRevolution = 1.0;
time.ScratchScale = 1.0;

midi.device(0).jog(16, JogMode.RelativeSigned, scale: 1)
    .to(delta => time.ScratchTicks(delta));
```

## Playback Controls

```csharp
deckA.PlaySpeed = 1.0f;
deckA.Volume = 0.8f;
deckA.Pan = 0f;
```
