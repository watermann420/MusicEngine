// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using NAudio.Wave;
using MusicEngine.Core;
using MusicEngine.Core.Effects;
using MusicEngine.Core.Effects.Filters;
using MusicEngine.Core.Effects.Dynamics;
using MusicEngine.Core.Effects.Modulation;
using MusicEngine.Core.Effects.TimeBased;
using MusicEngine.Core.Effects.Distortion;

namespace MusicEngine.Scripting.FluentApi;

/// <summary>
/// Fluent API for creating and configuring audio effects.
/// Provides chainable methods for effect creation and parameter control.
/// </summary>
public class EffectControl
{
    /// <summary>
    /// Creates a filter effect builder
    /// </summary>
    public FilterBuilder Filter(string name, ISampleProvider source)
    {
        return new FilterBuilder(name, source);
    }

    /// <summary>
    /// Creates a parametric EQ effect builder
    /// </summary>
    public ParametricEQBuilder EQ(string name, ISampleProvider source)
    {
        return new ParametricEQBuilder(name, source);
    }

    /// <summary>
    /// Creates a compressor effect builder
    /// </summary>
    public CompressorBuilder Compressor(string name, ISampleProvider source)
    {
        return new CompressorBuilder(name, source);
    }

    /// <summary>
    /// Creates a limiter effect builder
    /// </summary>
    public LimiterBuilder Limiter(string name, ISampleProvider source)
    {
        return new LimiterBuilder(name, source);
    }

    /// <summary>
    /// Creates a gate effect builder
    /// </summary>
    public GateBuilder Gate(string name, ISampleProvider source)
    {
        return new GateBuilder(name, source);
    }

    /// <summary>
    /// Creates a side-chain compressor effect builder
    /// </summary>
    public SideChainCompressorBuilder SideChainCompressor(string name, ISampleProvider source)
    {
        return new SideChainCompressorBuilder(name, source);
    }

    /// <summary>
    /// Creates a delay effect builder
    /// </summary>
    public DelayBuilder Delay(string name, ISampleProvider source)
    {
        return new DelayBuilder(name, source);
    }

    /// <summary>
    /// Creates a reverb effect builder
    /// </summary>
    public ReverbBuilder Reverb(string name, ISampleProvider source)
    {
        return new ReverbBuilder(name, source);
    }

    /// <summary>
    /// Creates a chorus effect builder
    /// </summary>
    public ChorusBuilder Chorus(string name, ISampleProvider source)
    {
        return new ChorusBuilder(name, source);
    }

    /// <summary>
    /// Creates a flanger effect builder
    /// </summary>
    public FlangerBuilder Flanger(string name, ISampleProvider source)
    {
        return new FlangerBuilder(name, source);
    }

    /// <summary>
    /// Creates a phaser effect builder
    /// </summary>
    public PhaserBuilder Phaser(string name, ISampleProvider source)
    {
        return new PhaserBuilder(name, source);
    }

    /// <summary>
    /// Creates a tremolo effect builder
    /// </summary>
    public TremoloBuilder Tremolo(string name, ISampleProvider source)
    {
        return new TremoloBuilder(name, source);
    }

    /// <summary>
    /// Creates a vibrato effect builder
    /// </summary>
    public VibratoBuilder Vibrato(string name, ISampleProvider source)
    {
        return new VibratoBuilder(name, source);
    }

    /// <summary>
    /// Creates a distortion effect builder
    /// </summary>
    public DistortionBuilder Distortion(string name, ISampleProvider source)
    {
        return new DistortionBuilder(name, source);
    }

    /// <summary>
    /// Creates a bitcrusher effect builder
    /// </summary>
    public BitcrusherBuilder Bitcrusher(string name, ISampleProvider source)
    {
        return new BitcrusherBuilder(name, source);
    }
}

#region Effect Builders

/// <summary>
/// Filter effect builder with fluent API
/// </summary>
public class FilterBuilder
{
    private readonly FilterEffect _effect;

    public FilterBuilder(string name, ISampleProvider source)
    {
        _effect = new FilterEffect(source, name);
    }

    public FilterBuilder Cutoff(float frequency)
    {
        _effect.Cutoff = frequency;
        return this;
    }

    public FilterBuilder Resonance(float resonance)
    {
        _effect.Resonance = resonance;
        return this;
    }

    public FilterBuilder Type(FilterType filterType)
    {
        _effect.Type = filterType;
        return this;
    }

    public FilterBuilder DryWet(float mix)
    {
        _effect.DryWet = mix;
        return this;
    }

    public FilterEffect Build() => _effect;

    public static implicit operator FilterEffect(FilterBuilder builder) => builder._effect;
}

/// <summary>
/// Parametric EQ effect builder with fluent API
/// </summary>
public class ParametricEQBuilder
{
    private readonly ParametricEQEffect _effect;

    public ParametricEQBuilder(string name, ISampleProvider source)
    {
        _effect = new ParametricEQEffect(source, name);
    }

    public ParametricEQBuilder Low(float freq, float gain, float q)
    {
        _effect.LowFrequency = freq;
        _effect.LowGain = gain;
        _effect.LowQ = q;
        return this;
    }

    public ParametricEQBuilder Mid(float freq, float gain, float q)
    {
        _effect.MidFrequency = freq;
        _effect.MidGain = gain;
        _effect.MidQ = q;
        return this;
    }

    public ParametricEQBuilder High(float freq, float gain, float q)
    {
        _effect.HighFrequency = freq;
        _effect.HighGain = gain;
        _effect.HighQ = q;
        return this;
    }

    public ParametricEQBuilder DryWet(float mix)
    {
        _effect.DryWet = mix;
        return this;
    }

    public ParametricEQEffect Build() => _effect;

    public static implicit operator ParametricEQEffect(ParametricEQBuilder builder) => builder._effect;
}

/// <summary>
/// Compressor effect builder with fluent API
/// </summary>
public class CompressorBuilder
{
    private readonly CompressorEffect _effect;

    public CompressorBuilder(string name, ISampleProvider source)
    {
        _effect = new CompressorEffect(source, name);
    }

    public CompressorBuilder Threshold(float db)
    {
        _effect.Threshold = db;
        return this;
    }

    public CompressorBuilder Ratio(float ratio)
    {
        _effect.Ratio = ratio;
        return this;
    }

    public CompressorBuilder Attack(float seconds)
    {
        _effect.Attack = seconds;
        return this;
    }

    public CompressorBuilder Release(float seconds)
    {
        _effect.Release = seconds;
        return this;
    }

    public CompressorBuilder MakeupGain(float db)
    {
        _effect.MakeupGain = db;
        return this;
    }

    public CompressorBuilder Knee(float width)
    {
        _effect.KneeWidth = width;
        return this;
    }

    public CompressorBuilder AutoGain(float amount)
    {
        _effect.AutoGain = amount;
        return this;
    }

    public CompressorBuilder DryWet(float mix)
    {
        _effect.DryWet = mix;
        return this;
    }

    public CompressorEffect Build() => _effect;

    public static implicit operator CompressorEffect(CompressorBuilder builder) => builder._effect;
}

/// <summary>
/// Limiter effect builder with fluent API
/// </summary>
public class LimiterBuilder
{
    private readonly LimiterEffect _effect;

    public LimiterBuilder(string name, ISampleProvider source)
    {
        _effect = new LimiterEffect(source, name);
    }

    public LimiterBuilder Ceiling(float db)
    {
        _effect.Ceiling = db;
        return this;
    }

    public LimiterBuilder Release(float seconds)
    {
        _effect.Release = seconds;
        return this;
    }

    public LimiterBuilder Lookahead(float seconds)
    {
        _effect.Lookahead = seconds;
        return this;
    }

    public LimiterBuilder DryWet(float mix)
    {
        _effect.DryWet = mix;
        return this;
    }

    public LimiterEffect Build() => _effect;

    public static implicit operator LimiterEffect(LimiterBuilder builder) => builder._effect;
}

/// <summary>
/// Gate effect builder with fluent API
/// </summary>
public class GateBuilder
{
    private readonly GateEffect _effect;

    public GateBuilder(string name, ISampleProvider source)
    {
        _effect = new GateEffect(source, name);
    }

    public GateBuilder Threshold(float db)
    {
        _effect.Threshold = db;
        return this;
    }

    public GateBuilder Ratio(float ratio)
    {
        _effect.Ratio = ratio;
        return this;
    }

    public GateBuilder Attack(float seconds)
    {
        _effect.Attack = seconds;
        return this;
    }

    public GateBuilder Hold(float seconds)
    {
        _effect.Hold = seconds;
        return this;
    }

    public GateBuilder Release(float seconds)
    {
        _effect.Release = seconds;
        return this;
    }

    public GateBuilder Range(float db)
    {
        _effect.Range = db;
        return this;
    }

    public GateBuilder DryWet(float mix)
    {
        _effect.DryWet = mix;
        return this;
    }

    public GateEffect Build() => _effect;

    public static implicit operator GateEffect(GateBuilder builder) => builder._effect;
}

/// <summary>
/// Side-chain compressor effect builder with fluent API
/// </summary>
public class SideChainCompressorBuilder
{
    private readonly SideChainCompressorEffect _effect;

    public SideChainCompressorBuilder(string name, ISampleProvider source)
    {
        _effect = new SideChainCompressorEffect(source, name);
    }

    public SideChainCompressorBuilder SideChain(ISampleProvider sideChainSource)
    {
        _effect.SetSideChainSource(sideChainSource);
        return this;
    }

    public SideChainCompressorBuilder Threshold(float db)
    {
        _effect.Threshold = db;
        return this;
    }

    public SideChainCompressorBuilder Ratio(float ratio)
    {
        _effect.Ratio = ratio;
        return this;
    }

    public SideChainCompressorBuilder Attack(float seconds)
    {
        _effect.Attack = seconds;
        return this;
    }

    public SideChainCompressorBuilder Release(float seconds)
    {
        _effect.Release = seconds;
        return this;
    }

    public SideChainCompressorBuilder MakeupGain(float db)
    {
        _effect.MakeupGain = db;
        return this;
    }

    public SideChainCompressorBuilder SideChainGain(float gain)
    {
        _effect.SideChainGain = gain;
        return this;
    }

    public SideChainCompressorBuilder DryWet(float mix)
    {
        _effect.DryWet = mix;
        return this;
    }

    public SideChainCompressorEffect Build() => _effect;

    public static implicit operator SideChainCompressorEffect(SideChainCompressorBuilder builder) => builder._effect;
}

/// <summary>
/// Delay effect builder with fluent API
/// </summary>
public class DelayBuilder
{
    private readonly EnhancedDelayEffect _effect;

    public DelayBuilder(string name, ISampleProvider source)
    {
        _effect = new EnhancedDelayEffect(source, name);
    }

    public DelayBuilder Time(float seconds)
    {
        _effect.DelayTime = seconds;
        return this;
    }

    public DelayBuilder Feedback(float amount)
    {
        _effect.Feedback = amount;
        return this;
    }

    public DelayBuilder CrossFeedback(float amount)
    {
        _effect.CrossFeedback = amount;
        return this;
    }

    public DelayBuilder Damping(float amount)
    {
        _effect.Damping = amount;
        return this;
    }

    public DelayBuilder StereoSpread(float amount)
    {
        _effect.StereoSpread = amount;
        return this;
    }

    public DelayBuilder PingPong(float amount)
    {
        _effect.PingPong = amount;
        return this;
    }

    public DelayBuilder DryWet(float mix)
    {
        _effect.DryWet = mix;
        return this;
    }

    public EnhancedDelayEffect Build() => _effect;

    public static implicit operator EnhancedDelayEffect(DelayBuilder builder) => builder._effect;
}

/// <summary>
/// Reverb effect builder with fluent API
/// </summary>
public class ReverbBuilder
{
    private readonly EnhancedReverbEffect _effect;

    public ReverbBuilder(string name, ISampleProvider source)
    {
        _effect = new EnhancedReverbEffect(source, name);
    }

    public ReverbBuilder RoomSize(float size)
    {
        _effect.RoomSize = size;
        return this;
    }

    public ReverbBuilder Damping(float amount)
    {
        _effect.Damping = amount;
        return this;
    }

    public ReverbBuilder Width(float width)
    {
        _effect.Width = width;
        return this;
    }

    public ReverbBuilder EarlyLevel(float level)
    {
        _effect.EarlyLevel = level;
        return this;
    }

    public ReverbBuilder LateLevel(float level)
    {
        _effect.LateLevel = level;
        return this;
    }

    public ReverbBuilder Predelay(float seconds)
    {
        _effect.Predelay = seconds;
        return this;
    }

    public ReverbBuilder DryWet(float mix)
    {
        _effect.DryWet = mix;
        return this;
    }

    public EnhancedReverbEffect Build() => _effect;

    public static implicit operator EnhancedReverbEffect(ReverbBuilder builder) => builder._effect;
}

/// <summary>
/// Chorus effect builder with fluent API
/// </summary>
public class ChorusBuilder
{
    private readonly EnhancedChorusEffect _effect;

    public ChorusBuilder(string name, ISampleProvider source)
    {
        _effect = new EnhancedChorusEffect(source, name);
    }

    public ChorusBuilder Rate(float hz)
    {
        _effect.Rate = hz;
        return this;
    }

    public ChorusBuilder Depth(float seconds)
    {
        _effect.Depth = seconds;
        return this;
    }

    public ChorusBuilder BaseDelay(float seconds)
    {
        _effect.BaseDelay = seconds;
        return this;
    }

    public ChorusBuilder Voices(int count)
    {
        _effect.Voices = count;
        return this;
    }

    public ChorusBuilder Spread(float amount)
    {
        _effect.Spread = amount;
        return this;
    }

    public ChorusBuilder Feedback(float amount)
    {
        _effect.Feedback = amount;
        return this;
    }

    public ChorusBuilder DryWet(float mix)
    {
        _effect.DryWet = mix;
        return this;
    }

    public EnhancedChorusEffect Build() => _effect;

    public static implicit operator EnhancedChorusEffect(ChorusBuilder builder) => builder._effect;
}

/// <summary>
/// Flanger effect builder with fluent API
/// </summary>
public class FlangerBuilder
{
    private readonly FlangerEffect _effect;

    public FlangerBuilder(string name, ISampleProvider source)
    {
        _effect = new FlangerEffect(source, name);
    }

    public FlangerBuilder Rate(float hz)
    {
        _effect.Rate = hz;
        return this;
    }

    public FlangerBuilder Depth(float seconds)
    {
        _effect.Depth = seconds;
        return this;
    }

    public FlangerBuilder Feedback(float amount)
    {
        _effect.Feedback = amount;
        return this;
    }

    public FlangerBuilder BaseDelay(float seconds)
    {
        _effect.BaseDelay = seconds;
        return this;
    }

    public FlangerBuilder Stereo(float amount)
    {
        _effect.Stereo = amount;
        return this;
    }

    public FlangerBuilder DryWet(float mix)
    {
        _effect.DryWet = mix;
        return this;
    }

    public FlangerEffect Build() => _effect;

    public static implicit operator FlangerEffect(FlangerBuilder builder) => builder._effect;
}

/// <summary>
/// Phaser effect builder with fluent API
/// </summary>
public class PhaserBuilder
{
    private readonly PhaserEffect _effect;

    public PhaserBuilder(string name, ISampleProvider source)
    {
        _effect = new PhaserEffect(source, name);
    }

    public PhaserBuilder Rate(float hz)
    {
        _effect.Rate = hz;
        return this;
    }

    public PhaserBuilder Depth(float amount)
    {
        _effect.Depth = amount;
        return this;
    }

    public PhaserBuilder Feedback(float amount)
    {
        _effect.Feedback = amount;
        return this;
    }

    public PhaserBuilder MinFrequency(float hz)
    {
        _effect.MinFrequency = hz;
        return this;
    }

    public PhaserBuilder MaxFrequency(float hz)
    {
        _effect.MaxFrequency = hz;
        return this;
    }

    public PhaserBuilder Stages(int count)
    {
        _effect.Stages = count;
        return this;
    }

    public PhaserBuilder Stereo(float amount)
    {
        _effect.Stereo = amount;
        return this;
    }

    public PhaserBuilder DryWet(float mix)
    {
        _effect.DryWet = mix;
        return this;
    }

    public PhaserEffect Build() => _effect;

    public static implicit operator PhaserEffect(PhaserBuilder builder) => builder._effect;
}

/// <summary>
/// Tremolo effect builder with fluent API
/// </summary>
public class TremoloBuilder
{
    private readonly TremoloEffect _effect;

    public TremoloBuilder(string name, ISampleProvider source)
    {
        _effect = new TremoloEffect(source, name);
    }

    public TremoloBuilder Rate(float hz)
    {
        _effect.Rate = hz;
        return this;
    }

    public TremoloBuilder Depth(float amount)
    {
        _effect.Depth = amount;
        return this;
    }

    public TremoloBuilder Waveform(int waveform)
    {
        _effect.Waveform = waveform;
        return this;
    }

    public TremoloBuilder Stereo(float amount)
    {
        _effect.Stereo = amount;
        return this;
    }

    public TremoloBuilder DryWet(float mix)
    {
        _effect.DryWet = mix;
        return this;
    }

    public TremoloEffect Build() => _effect;

    public static implicit operator TremoloEffect(TremoloBuilder builder) => builder._effect;
}

/// <summary>
/// Vibrato effect builder with fluent API
/// </summary>
public class VibratoBuilder
{
    private readonly VibratoEffect _effect;

    public VibratoBuilder(string name, ISampleProvider source)
    {
        _effect = new VibratoEffect(source, name);
    }

    public VibratoBuilder Rate(float hz)
    {
        _effect.Rate = hz;
        return this;
    }

    public VibratoBuilder Depth(float seconds)
    {
        _effect.Depth = seconds;
        return this;
    }

    public VibratoBuilder BaseDelay(float seconds)
    {
        _effect.BaseDelay = seconds;
        return this;
    }

    public VibratoBuilder Waveform(int waveform)
    {
        _effect.Waveform = waveform;
        return this;
    }

    public VibratoBuilder DryWet(float mix)
    {
        _effect.DryWet = mix;
        return this;
    }

    public VibratoEffect Build() => _effect;

    public static implicit operator VibratoEffect(VibratoBuilder builder) => builder._effect;
}

/// <summary>
/// Distortion effect builder with fluent API
/// </summary>
public class DistortionBuilder
{
    private readonly DistortionEffect _effect;

    public DistortionBuilder(string name, ISampleProvider source)
    {
        _effect = new DistortionEffect(source, name);
    }

    public DistortionBuilder Drive(float amount)
    {
        _effect.Drive = amount;
        return this;
    }

    public DistortionBuilder Tone(float amount)
    {
        _effect.Tone = amount;
        return this;
    }

    public DistortionBuilder OutputGain(float gain)
    {
        _effect.OutputGain = gain;
        return this;
    }

    public DistortionBuilder Type(DistortionType type)
    {
        _effect.Type = type;
        return this;
    }

    public DistortionBuilder DryWet(float mix)
    {
        _effect.DryWet = mix;
        return this;
    }

    public DistortionEffect Build() => _effect;

    public static implicit operator DistortionEffect(DistortionBuilder builder) => builder._effect;
}

/// <summary>
/// Bitcrusher effect builder with fluent API
/// </summary>
public class BitcrusherBuilder
{
    private readonly BitcrusherEffect _effect;

    public BitcrusherBuilder(string name, ISampleProvider source)
    {
        _effect = new BitcrusherEffect(source, name);
    }

    public BitcrusherBuilder BitDepth(float bits)
    {
        _effect.BitDepth = bits;
        return this;
    }

    public BitcrusherBuilder SampleRate(float rate)
    {
        _effect.TargetSampleRate = rate;
        return this;
    }

    public BitcrusherBuilder DryWet(float mix)
    {
        _effect.DryWet = mix;
        return this;
    }

    public BitcrusherEffect Build() => _effect;

    public static implicit operator BitcrusherEffect(BitcrusherBuilder builder) => builder._effect;
}

#endregion
