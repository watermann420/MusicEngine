// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Modulation helpers for binding variables.

using System;
using MusicEngine.Effects.Audio;
using MusicEngine.Instruments;
using MusicEngine.Vst;

namespace MusicEngine.Core.Modulation;

/// <summary>
/// Modulation helpers for binding variables.
/// </summary>
public static class Mod
{
    public static ModVar Bind(Func<float> get, Action<float> set, float? initial = null)
        => new ModVar(get, set, initial);

    public static ModVar Var(Func<float> get, Action<float> set, float? initial = null)
        => Bind(get, set, initial);

    public static ModGroup Group(params ModVar[] vars)
        => new ModGroup().Add(vars);

    public static ModVar Volume(IInstrumentControls instrument, float? initial = null)
        => Bind(() => instrument.Volume, v => instrument.Volume = v, initial);

    public static ModVar Pan(IInstrumentControls instrument, float? initial = null)
        => Bind(() => instrument.Pan, v => instrument.Pan = v, initial);

    public static ModVar Reverb(IInstrumentControls instrument, float? initial = null)
        => Bind(() => instrument.Reverb, v => instrument.Reverb = v, initial);

    public static ModVar Chorus(IInstrumentControls instrument, float? initial = null)
        => Bind(() => instrument.Chorus, v => instrument.Chorus = v, initial);

    public static ModVar ModWheel(IInstrumentControls instrument, float? initial = null)
        => Bind(() => instrument.ModWheel, v => instrument.ModWheel = v, initial);

    public static ModVar Gain(AudioInput input, float? initial = null)
        => Bind(() => input.Gain, v => input.Gain = v, initial);

    public static ModVar Mix(SimpleDelayEffect effect, float? initial = null)
        => Bind(() => effect.Mix, v => effect.Mix = v, initial);

    public static ModVar TimeMs(SimpleDelayEffect effect, float? initial = null)
        => Bind(() => effect.TimeMs, v => effect.TimeMs = v, initial);

    public static ModVar Feedback(SimpleDelayEffect effect, float? initial = null)
        => Bind(() => effect.Feedback, v => effect.Feedback = v, initial);

    public static ModVar Mix(SimpleReverbEffect effect, float? initial = null)
        => Bind(() => effect.Mix, v => effect.Mix = v, initial);

    public static ModVar Size(SimpleReverbEffect effect, float? initial = null)
        => Bind(() => effect.Size, v => effect.Size = v, initial);

    public static ModVar Damping(SimpleReverbEffect effect, float? initial = null)
        => Bind(() => effect.Damping, v => effect.Damping = v, initial);

    public static ModVar Depth(TremoloEffect effect, float? initial = null)
        => Bind(() => effect.Depth, v => effect.Depth = v, initial);

    public static ModVar Rate(TremoloEffect effect, float? initial = null)
        => Bind(() => effect.Rate, v => effect.Rate = v, initial);
}
