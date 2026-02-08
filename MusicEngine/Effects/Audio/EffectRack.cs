// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Modular effect rack for building custom chains.

using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace MusicEngine.Effects.Audio;

/// <summary>
/// Modular effect rack for building custom chains.
/// </summary>
public sealed class EffectRack : IAudioEffect
{
    private readonly List<IAudioEffect> _effects = new();
    private AudioEffectChain? _chain;

    public string Name { get; set; } = "EffectRack";

    public EffectRack Add(IAudioEffect effect)
    {
        if (effect != null)
        {
            _effects.Add(effect);
        }
        return this;
    }

    public EffectRack Clear()
    {
        _effects.Clear();
        return this;
    }

    public EffectRack Reverb(Action<SimpleReverbEffect>? configure = null)
    {
        var effect = new SimpleReverbEffect();
        configure?.Invoke(effect);
        return Add(effect);
    }

    public EffectRack Delay(Action<SimpleDelayEffect>? configure = null)
    {
        var effect = new SimpleDelayEffect();
        configure?.Invoke(effect);
        return Add(effect);
    }

    public EffectRack Tremolo(Action<TremoloEffect>? configure = null)
    {
        var effect = new TremoloEffect();
        configure?.Invoke(effect);
        return Add(effect);
    }

    public EffectRack BitCrush(Action<BitCrusherEffect>? configure = null)
    {
        var effect = new BitCrusherEffect();
        configure?.Invoke(effect);
        return Add(effect);
    }

    public EffectRack Noise(Action<NoiseEffect>? configure = null)
    {
        var effect = new NoiseEffect();
        configure?.Invoke(effect);
        return Add(effect);
    }

    public EffectRack Gain(Action<GainEffect>? configure = null)
    {
        var effect = new GainEffect();
        configure?.Invoke(effect);
        return Add(effect);
    }

    public EffectRack Drive(Action<DriveEffect>? configure = null)
    {
        var effect = new DriveEffect();
        configure?.Invoke(effect);
        return Add(effect);
    }

    public EffectRack Filter(Action<SimpleFilterEffect>? configure = null)
    {
        var effect = new SimpleFilterEffect();
        configure?.Invoke(effect);
        return Add(effect);
    }

    public EffectRack Custom(Action<float[], int, int, WaveFormat> process, string? name = null)
    {
        var effect = new CustomEffect(process, name ?? "Custom");
        return Add(effect);
    }

    public ISampleProvider Attach(ISampleProvider input, WaveFormat targetFormat)
    {
        _chain?.ClearEffects();
        _chain = new AudioEffectChain(input, targetFormat);
        foreach (var effect in _effects)
        {
            _chain.AddEffect(effect);
        }
        return _chain;
    }

    public void Detach()
    {
        _chain?.ClearEffects();
        _chain = null;
    }

    public void Dispose() => Detach();
}
