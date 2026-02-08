// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Minimal audio control fluent API.

using MusicEngine.Core;
using MusicEngine.Effects.Audio;
using MusicEngine.Effects.Vst;

namespace MusicEngine.Scripting.FluentApi;

/// <summary>
/// Fluent API entry point for audio routing and controls.
/// </summary>
public sealed class AudioControl
{
    private readonly ScriptGlobals _globals;
    public AudioControl(ScriptGlobals globals) => _globals = globals;

    /// <summary>
    /// Master channel controls.
    /// </summary>
    public MasterAudioControl master => new MasterAudioControl(_globals.Engine);
    /// <summary>
    /// Master channel controls.
    /// </summary>
    public MasterAudioControl Master => new MasterAudioControl(_globals.Engine);
    /// <summary>
    /// Controls that apply to all channels.
    /// </summary>
    public AllChannelsControl all => new AllChannelsControl(_globals.Engine);
    /// <summary>
    /// Controls that apply to all channels.
    /// </summary>
    public AllChannelsControl All => new AllChannelsControl(_globals.Engine);
    /// <summary>
    /// Access a specific channel control by index.
    /// </summary>
    public AudioChannelControl channel(int index) => new AudioChannelControl(_globals, index);
    /// <summary>
    /// Access a specific channel control by index.
    /// </summary>
    public AudioChannelControl Channel(int index) => new AudioChannelControl(_globals, index);

    /// <summary>
    /// Create or access a channel control by index.
    /// </summary>
    public AudioChannelControl createchannel(int index) => new AudioChannelControl(_globals, index);

    /// <summary>
    /// Create or access a channel control by index.
    /// </summary>
    public AudioChannelControl CreateChannel(int index) => new AudioChannelControl(_globals, index);
}

/// <summary>
/// Controls that apply to all channels.
/// </summary>
public sealed class AllChannelsControl
{
    private readonly AudioEngine _engine;
    public AllChannelsControl(AudioEngine engine) => _engine = engine;

    /// <summary>
    /// Set gain for all channels.
    /// </summary>
    public void gain(float value) => _engine.SetAllChannelsGain(value);
    /// <summary>
    /// Set gain for all channels.
    /// </summary>
    public void Gain(float value) => _engine.SetAllChannelsGain(value);
    /// <summary>
    /// Set gain for all channels (double overload).
    /// </summary>
    public void gain(double value) => gain((float)value);
    /// <summary>
    /// Set gain for all channels (double overload).
    /// </summary>
    public void Gain(double value) => gain(value);
}

/// <summary>
/// Controls for the master output.
/// </summary>
public sealed class MasterAudioControl
{
    private readonly AudioEngine _engine;
    private RecordingSession? _lastRecording;
    public MasterAudioControl(AudioEngine engine) => _engine = engine;

    /// <summary>
    /// Set master gain.
    /// </summary>
    public void gain(float value) => _engine.SetAllChannelsGain(value);
    /// <summary>
    /// Set master gain.
    /// </summary>
    public void Gain(float value) => _engine.SetAllChannelsGain(value);
    /// <summary>
    /// Set master gain (double overload).
    /// </summary>
    public void gain(double value) => gain((float)value);
    /// <summary>
    /// Set master gain (double overload).
    /// </summary>
    public void Gain(double value) => gain(value);

    /// <summary>
    /// Add an effect to the master chain.
    /// </summary>
    public void effect(IAudioEffect effect) => _engine.AddMasterEffect(effect);
    /// <summary>
    /// Add an effect to the master chain.
    /// </summary>
    public void Effect(IAudioEffect effect) => _engine.AddMasterEffect(effect);
    /// <summary>
    /// Clear all master effects.
    /// </summary>
    public void clearEffects() => _engine.ClearMasterEffects();
    /// <summary>
    /// Clear all master effects.
    /// </summary>
    public void ClearEffects() => _engine.ClearMasterEffects();

    /// <summary>
    /// Recording controls for the master output.
    /// </summary>
    public RecordingControl record => new RecordingControl(
        start: (path, format) => _lastRecording = _engine.StartMasterRecording(path, format),
        stop: session => _engine.StopMasterRecording(session ?? _lastRecording)
    );
    /// <summary>
    /// Recording controls for the master output.
    /// </summary>
    public RecordingControl Record => new RecordingControl(
        start: (path, format) => _lastRecording = _engine.StartMasterRecording(path, format),
        stop: session => _engine.StopMasterRecording(session ?? _lastRecording)
    );
}

/// <summary>
/// Controls for a specific audio channel.
/// </summary>
public sealed class AudioChannelControl
{
    private readonly ScriptGlobals _globals;
    private readonly int _index;
    private RecordingSession? _lastRecording;

    public AudioChannelControl(ScriptGlobals globals, int index)
    {
        _globals = globals;
        _index = index < 1 ? 1 : index;
    }

    /// <summary>
    /// Set gain for the channel.
    /// </summary>
    public void gain(float value) => _globals.Engine.SetChannelGain(_index, value);
    /// <summary>
    /// Set gain for the channel.
    /// </summary>
    public void Gain(float value) => _globals.Engine.SetChannelGain(_index, value);
    /// <summary>
    /// Set gain for the channel (double overload).
    /// </summary>
    public void gain(double value) => gain((float)value);
    /// <summary>
    /// Set gain for the channel (double overload).
    /// </summary>
    public void Gain(double value) => gain(value);

    /// <summary>
    /// Route a synth to this channel.
    /// </summary>
    public void route(ISynth synth) => _globals.Engine.RouteToChannel(synth, _index);
    /// <summary>
    /// Route a synth to this channel.
    /// </summary>
    public void Route(ISynth synth) => _globals.Engine.RouteToChannel(synth, _index);
    /// <summary>
    /// Route all synth targets from a pattern to this channel.
    /// </summary>
    public void route(Pattern pattern)
    {
        if (pattern == null) return;
        foreach (var target in pattern.SynthTargets)
        {
            _globals.Engine.RouteToChannel(target, _index);
        }
    }
    /// <summary>
    /// Route all synth targets from a pattern to this channel.
    /// </summary>
    public void Route(Pattern pattern) => route(pattern);

    /// <summary>
    /// Add an effect to this channel.
    /// </summary>
    public void effect(IAudioEffect effect) => _globals.Engine.AddChannelEffect(_index, effect);
    /// <summary>
    /// Add an effect to this channel.
    /// </summary>
    public void Effect(IAudioEffect effect) => _globals.Engine.AddChannelEffect(_index, effect);
    /// <summary>
    /// Clear all effects on this channel.
    /// </summary>
    public void clearEffects() => _globals.Engine.ClearChannelEffects(_index);
    /// <summary>
    /// Clear all effects on this channel.
    /// </summary>
    public void ClearEffects() => _globals.Engine.ClearChannelEffects(_index);

    /// <summary>
    /// Recording controls for this channel.
    /// </summary>
    public RecordingControl record => new RecordingControl(
        start: (path, format) => _lastRecording = _globals.Engine.StartChannelRecording(_index, path, format),
        stop: session => _globals.Engine.StopChannelRecording(_index, session ?? _lastRecording)
    );
    /// <summary>
    /// Recording controls for this channel.
    /// </summary>
    public RecordingControl Record => new RecordingControl(
        start: (path, format) => _lastRecording = _globals.Engine.StartChannelRecording(_index, path, format),
        stop: session => _globals.Engine.StopChannelRecording(_index, session ?? _lastRecording)
    );

    /// <summary>
    /// Create and add a VST3 effect to this channel.
    /// </summary>
    public Vst3Effect vsteffect(string name)
    {
        var effect = _globals.CreateVstEffect(name);
        _globals.Engine.AddChannelEffect(_index, effect);
        return effect;
    }

    /// <summary>
    /// Create and add a VST3 effect to this channel.
    /// </summary>
    public Vst3Effect vsteffekt(string name) => vsteffect(name);

    /// <summary>
    /// Create and add a VST3 effect to this channel.
    /// </summary>
    public Vst3Effect VstEffect(string name) => vsteffect(name);
}

/// <summary>
/// Recording controls used by fluent API.
/// </summary>
public sealed class RecordingControl
{
    private readonly Func<string, string?, RecordingSession> _start;
    private readonly Action<RecordingSession?> _stop;

    public RecordingControl(Func<string, string?, RecordingSession> start, Action<RecordingSession?> stop)
    {
        _start = start;
        _stop = stop;
    }

    /// <summary>
    /// Start a new recording session.
    /// </summary>
    public RecordingSession start(string path, string? format = null) => _start(path, format);
    /// <summary>
    /// Stop the last recording session.
    /// </summary>
    public void stop() => _stop(null);
    /// <summary>
    /// Stop a specific recording session.
    /// </summary>
    public void stop(RecordingSession session) => _stop(session);
}
