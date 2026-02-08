# Variable Modulation

Variable modulation lets you bind any float property and automate it with random, LFO, math, or custom logic.

## Bind Any Property

```csharp
var pan = Mod.Bind(
    get: () => piano.Pan,
    set: v => piano.Pan = v,
    initial: 0f);
```

## Short Helpers

```csharp
var pan = Mod.Pan(piano, 0f);
var vol = Mod.Volume(synth, 0.7f);
var mix = Mod.Mix(new SimpleDelayEffect(), 0.2f);
```

## Random + LFO

```csharp
pan.Random(-0.5f, 0.5f, everyMs: 400);
vol.Lfo(0.2f, 0.9f, rateHz: 0.5f);
```

## Math + If

```csharp
pan.Map(v => v * 0.5f);
pan.If(() => playPattern, -0.2f, 0.2f);
```

## Groups

```csharp
var g = Mod.Group(pan, vol);
g.Enable(false);
```

Notes:
- Modulation runs on a small timer (about 30 Hz).
- You can chain multiple modulators: `Random().Map(...).If(...)`.
